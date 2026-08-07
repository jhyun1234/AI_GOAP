using System.Collections.Generic;
using AIVillage.Core;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 위협 개체 (M10-C) — 표현+이동만. 주민이 아니다 (ADR-M10-4): VillagerAgent 미재사용,
    /// 타일 예약 비참여 (일과성 통과자 — 주민과 겹쳐도 무해, M11 전투에서 재검토), 타격·대상
    /// 판정은 ThreatService가 수행한다 (명세 M10-C ⚠️③).
    ///
    /// 이동 모드 2종 (M10-C 개정 2026-07-22 — 고정 타깃은 도망이 구조적으로 100% 이겨 부상
    /// 착탄 불가, Day40+ 관측으로 추격 도입):
    ///   밭 타격형 = 고정 목표 도보 (기존) — 시설은 도망가지 않는다.
    ///   주민 타격형 = 추격: 주기 재타겟(최근접 생존 주민) + 사거리 진입 시 그 자리에서 타격.
    ///   늑대 2.5 > 주민 2.0 속도라 수렴이 결정적 (ADR-M10-1 — 확률 아님), 늦게 뛴 고집쟁이·
    ///   절뚝이는 도망자가 먼저 잡힌다. 기브업 상한은 도달 불가 지형의 안전장치일 뿐.
    ///
    /// **수명 개정 (M21-W2)**: 舊 수명은 "도착 = 타격 = 퇴장"이었다 — 한 대 치고 사라지니
    /// 주민이 맞설 대상 자체가 존재하지 않았다(격퇴 불가의 실제 정체). 이제 도착하면 머무르며
    /// RepeatStrikePeriodDays마다 다시 친다. 퇴장 경로는 세 가지로만 열린다:
    ///   ① 도주 (BeginFlee — 체력이 도주선 아래, 판정은 CombatService)
    ///   ② 체류 상한 도달 (MaxStayDays — 자비가 아니라 지형 보루)
    ///   ③ 칠 대상 소멸 (주민형은 전멸·전원 이탈, 밭형은 사거리 내 밭 소진)
    ///
    /// **배회 개정 (M21-W2R)**: W2의 체류는 **정지**였고, 화면에 나온 것은 "원 하나가 밭 위에
    /// 서서 콘솔에만 로그를 뿜는 것"이었다 (2026-08-06 사용자 판정 — 게이트 320개가 green인 채로).
    /// 이제 도착 지점을 앵커로 WanderRadiusTiles 안을 돈다. 우선순위는 **추격 > 배회**다:
    /// 주민형은 사거리 밖이면 추격이 경로를 덮고, 배회는 사거리 안에 있을 때의 기본 행동이다
    /// (뒤집으면 주민을 못 잡는다 — 명세 ⚠️ 오해 위험 3).
    ///
    /// 체력(Hp)은 여기 있지만 **깎는 판정은 CombatService**다 (ADR-M21-8) — 개체는 문만 연다.
    /// 세이브 대상 아님 (ADR-M10-10 — 로드 후 다음 스케줄에서 재출몰).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThreatAgent : MonoBehaviour
    {
        private const float ARRIVE_EPSILON_SQR = 0.0001f; // 도착 판정 (알고리즘 상수)
        private const float RETARGET_SEC = 1f;            // 추격 재타겟 주기 (알고리즘 상수)
        private const float CHASE_GIVEUP_SEC = 90f;       // 추격 안전 상한 — 속도 우위라 정상 케이스 미도달

        /// <summary>배회 목적지 재선정 최소 간격 (실시간 초, 알고리즘 상수 — 밸런스 아님).
        /// 경로탐색 예산 보호가 목적이다: 뽑힌 타일이 현재 타일이면 경로가 즉시 소진되고,
        /// 간격이 없으면 매 프레임 재선정 = 제자리 떨림 + JPS 호출 폭증이 된다
        /// (Docs/ADR_경로탐색_확장경계.md, 명세 ⚠️ 오해 위험 2).</summary>
        private const float WANDER_REPICK_SEC = 0.5f;

        public ThreatSO So { get; private set; }
        public Vector2Int EntryTile { get; private set; }
        public Vector2Int TargetTile { get; private set; }

        /// <summary>이번 출몰이 주민을 노리는가 (M10R, ADR-M10R-4) — 출몰 시 서비스가 롤해 주입.
        /// 이동 모드(추격/고정)·타격·HUD 분기가 모두 이 값을 읽는다. 밴드 고정 아님.</summary>
        public bool TargetsVillagers { get; private set; }

        /// <summary>현재 논리 타일 — IsNearThreat(감지)·사거리·타격 지점의 기준 (표현 위치의 반올림).</summary>
        public int TileX => Mathf.RoundToInt(transform.position.x);
        public int TileY => Mathf.RoundToInt(transform.position.y);

        /// <summary>현재 체력 (M21-W2). 쓰기는 ApplyHit 하나 — 차감할지 말지는 CombatService가
        /// 정한다 (ADR-M21-8: 위협의 죽음도 문 하나).</summary>
        public float Hp { get; private set; }

        /// <summary>도주 전환 여부 (M21-W2) — 격퇴 사건의 표식. 도주한 개체는 다시 싸우지 않는다.</summary>
        public bool IsFleeing { get; private set; }

        /// <summary>퇴장 진행 중 — 서비스가 중복 퇴장 지시를 거르는 판독점.</summary>
        public bool IsExiting => _exiting;

        /// <summary>체류 시작 시각 (게임일) — MaxStayDays 판정의 원본. 쓰기는 MarkArrived.</summary>
        public float ArrivedDay { get; private set; }

        /// <summary>마지막 타격 시각 (게임일) — 재타격 주기 판정의 원본. 쓰기는 MarkStruck.</summary>
        public float LastStrikeDay { get; private set; }

        /// <summary>배회 앵커 (M21-W2R) — 도착 지점. 배회는 이 타일 반경 안에서만 돈다.
        /// 앵커를 현재 위치로 갱신하면 늑대가 맵을 가로질러 표류한다 (배회가 아니라 유랑이 된다).</summary>
        public Vector2Int WanderAnchor { get; private set; }

        private ThreatService _svc;
        private IPathfinder _pathfinder;
        private List<Vector2Int> _path; // null·소진 = 목적지 도달 (모드별 해석)
        private int _wp;
        private bool _exiting;
        private bool _arrived;          // 체류 시작 — 이후 이동은 배회·재추격, 퇴장은 3경로만 (M21-W2R)
        private float _nextRetargetAt;
        private float _chaseGiveUpAt;
        private Vector2Int _lastChaseTile;
        private float _nextWanderAt;    // 배회 재선정 쿨다운 (WANDER_REPICK_SEC)

        public void Init(ThreatSO so, ThreatService svc, Vector2Int entry, Vector2Int target,
                         List<Vector2Int> waypoints, IPathfinder pathfinder, bool targetsVillagers)
        {
            So = so;
            _svc = svc;
            EntryTile = entry;
            TargetTile = target;
            TargetsVillagers = targetsVillagers;
            _path = waypoints; // null = 이미 목표 (AlreadyThere)
            _wp = 0;
            _pathfinder = pathfinder;
            _lastChaseTile = target;
            _chaseGiveUpAt = Time.time + CHASE_GIVEUP_SEC;
            Hp = so.MaxHp; // M21-W2 — 개체 편차 없음 (몸값 불가침, 주민 체력과 같은 사상)

            // 시각: 원형 마커 폴백 (주민 마커 패턴 — 아트 교체는 후속 에셋)
            var sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = M0Sprites.Circle;
            sr.color = so.BodyColor;
            sr.sortingOrder = 11; // 주민(10) 위 — 위협은 항상 보인다
            transform.localScale = Vector3.one * 0.9f;
        }

        private void Update()
        {
            if (So == null) return; // Init 전 방어

            // 체류 틱 (M21-W2) — 재타격 주기·체류 상한은 게임일 기준이라 서비스가 판정한다.
            if (_arrived && !_exiting)
            {
                _svc.NotifyStrikeTick(this);
                if (_exiting) return;
            }

            // 추격 틱 (주민 타격형만) — 사거리·재타겟 판정. 체류 중에도 돈다: 도망친 주민을
            // 다시 쫓아가야 "눌러앉았다"가 성립한다 (제자리에 굳으면 첫 희생자만 계속 맞는다).
            if (!_exiting && TargetsVillagers && Time.time >= _nextRetargetAt)
            {
                _nextRetargetAt = Time.time + RETARGET_SEC;
                TickChase();
                if (_exiting) return;
            }

            if (_path == null || _wp >= _path.Count)
            {
                if (_exiting) { DespawnNow(); return; }               // 퇴장 경로 소진 = 가장자리 도달
                if (!TargetsVillagers && !_arrived) { Arrive(); return; } // 밭형: 고정 목표 도착 = 체류 시작
                // 체류 중 목적지 도달 = 다음 배회 지점 (M21-W2R). ⚠️ 목적지에 **도착했을 때만**
                // 재선정한다 — 매 프레임이면 제자리 떨림 + 경로탐색 폭증 (명세 ⚠️ 오해 위험 2).
                if (_arrived) RepickWander();
                return; // 미도착 주민형은 제자리 — 재타겟 틱이 잇는다
            }

            var target = new Vector3(_path[_wp].x, _path[_wp].y, 0f); // ADR-M0-9 — X-Y 평면
            transform.position = Vector3.MoveTowards(transform.position, target,
                                                     So.MoveSpeed * Time.deltaTime);
            if ((transform.position - target).sqrMagnitude <= ARRIVE_EPSILON_SQR) _wp++;
        }

        /// <summary>추격 판정 1틱 — 순서: 사거리(잡았다) → 기브업(도착 전만) → 대상 없음 → 재경로.
        /// 도착 후의 상한은 실시간 기브업이 아니라 MaxStayDays다 (체류가 곧 위협의 몸이므로
        /// 90초 실시간 상한을 그대로 두면 눌러앉기가 시작되자마자 끝난다).</summary>
        private void TickChase()
        {
            if (_svc.IsInStrikeRange(this))
            {
                Arrive(); // 첫 진입이면 체류 시작 + 첫 타격, 이미 체류 중이면 제자리 (중복 방어는 Arrive가)
                return;
            }
            if (!_arrived && Time.time >= _chaseGiveUpAt)
            {
                _svc.BeginExit(this, "닿지 못했다"); // 도달 불가 지형 — 무한 추격 차단
                return;
            }
            if (!_svc.TryPickChaseTile(TileX, TileY, out Vector2Int chase))
            {
                _svc.BeginExit(this, "칠 대상이 없다"); // 생존 주민 없음 — 빈 타격 없이 물러난다
                return;
            }
            if (chase == _lastChaseTile) return; // 대상 위치 그대로 — 기존 경로 유지

            PathResult p = _pathfinder.FindPath(TileX, TileY, chase.x, chase.y);
            if (p.Kind == PathResultKind.PathFound)
            {
                _path = p.Waypoints;
                _wp = 0;
                _lastChaseTile = chase;
            }
            else if (p.Kind == PathResultKind.AlreadyThere)
                Arrive(); // 같은 타일 = 사거리 내 (방어 — 정상은 IsInStrikeRange가 먼저 잡는다)
            // Unreachable: 기존 경로 유지, 다음 틱 재시도 — 기브업 상한이 무한 추격을 막는다
        }

        /// <summary>체류 진입의 유일한 지점 (M21-W2R) — 도착 지점을 배회 앵커로 삼고 서비스가 첫 타격을
        /// 실행한 뒤, **곧바로 배회를 시작한다**.
        ///
        /// W2에서 여기가 `_path = null`로 굳혔고 그것이 화면에 "원 하나가 밭 위에 서 있는 것"으로
        /// 나왔다 (2026-08-06 사용자 판정). 체류가 틀린 게 아니라 **정지**가 틀렸다 — 머무르되
        /// 움직인다.</summary>
        private void Arrive()
        {
            if (_arrived || _exiting) return;
            _arrived = true;
            _path = null;
            _wp = 0;
            WanderAnchor = new Vector2Int(TileX, TileY); // 배회의 중심 — 이후 갱신하지 않는다(표류 방지)
            // 추격 기억을 지운다: 남겨 두면 도망친 주민이 마지막으로 있던 타일로 되돌아왔을 때
            // "이미 그 타일로 가는 중"이라 판단해 경로를 안 깔고, 경로는 null이라 영영 굳는다.
            _lastChaseTile = new Vector2Int(int.MinValue, int.MinValue);
            _svc.NotifyArrived(this);
            if (_exiting) return;  // 칠 대상 없음 → 서비스가 즉시 퇴장시켰다 (W2 결함 Y)
            RepickWander();
        }

        /// <summary>다음 배회 목적지를 깐다 (M21-W2R). 쿨다운은 경로탐색 예산 보호다 —
        /// 뽑힌 타일이 현재 타일이면 경로가 즉시 소진되므로, 간격이 없으면 매 프레임 재선정이 된다.
        /// 목적지 선정 자체는 서비스가(통행 배열을 아는 쪽) 수행한다 — 개체는 표현+이동만 (M10-C ⚠️③).</summary>
        private void RepickWander()
        {
            if (_exiting || Time.time < _nextWanderAt) return;
            _nextWanderAt = Time.time + WANDER_REPICK_SEC;

            Vector2Int dest = _svc.PickWanderTile(this);
            if (dest.x == TileX && dest.y == TileY) return; // 제자리 — 다음 쿨다운에 다시 뽑는다

            PathResult p = _pathfinder.FindPath(TileX, TileY, dest.x, dest.y);
            if (p.Kind != PathResultKind.PathFound) return; // 못 가면 그대로 — 다음 쿨다운에 재시도
            _path = p.Waypoints;
            _wp = 0;
        }

        /// <summary>피해 적용의 문 (CombatService 전용, ADR-M21-8) — 차감만 한다.
        /// 도주할지·죽었는지 판정은 호출자(CombatService)의 몫이다. 남은 체력을 돌려준다.</summary>
        public float ApplyHit(float damage)
        {
            if (damage > 0f) Hp = Mathf.Max(0f, Hp - damage);
            return Hp;
        }

        /// <summary>도주 전환의 문 (CombatService 전용) — 격퇴 성립. 퇴장 경로는 서비스가 부여한다.</summary>
        public void BeginFlee()
        {
            if (IsFleeing || _exiting) return;
            IsFleeing = true;
            _svc.BeginExit(this, "물러난다");
        }

        /// <summary>체류 시각 기록 (ThreatService 전용) — 재타격 주기·체류 상한 판정의 원본.</summary>
        public void MarkArrived(float gameDay) => ArrivedDay = gameDay;

        /// <summary>타격 시각 기록 (ThreatService 전용).</summary>
        public void MarkStruck(float gameDay) => LastStrikeDay = gameDay;

        /// <summary>퇴장 개시 (ThreatService 전용) — 진입점으로 되돌아간다. 퇴장 모드 전환은
        /// 여기 한 곳: 경로만 주고 플래그를 안 세우면 체류 틱이 계속 돌아 영영 못 나간다.</summary>
        public void SetExitPath(List<Vector2Int> waypoints)
        {
            _exiting = true;
            _path = waypoints;
            _wp = 0;
        }

        /// <summary>즉시 소멸 (ThreatService·자체 퇴장 공용) — 활성 목록 정리는 서비스가.</summary>
        public void DespawnNow()
        {
            _svc?.NotifyDespawn(this);
            _svc = null; // 중복 통지 방어 (Destroy 지연 프레임)
            Destroy(gameObject);
        }
    }
}
