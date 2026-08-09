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
    ///   위협 2.5 > 주민 2.0 속도라 수렴이 결정적 (ADR-M10-1 — 확률 아님), 늦게 뛴 고집쟁이·
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

        /// <summary>출몰 무리 키 (M21-W6) = 출몰 서수. 같은 발동으로 태어난 개체들이 같은 값을 갖고,
        /// 무리 도주선(잔존율) 판정이 이 키로 무리를 센다 (등록·집계는 ThreatService).</summary>
        public int GroupKey { get; private set; }

        /// <summary>현재 논리 타일 — IsNearThreat(감지)·사거리·타격 지점의 기준 (표현 위치의 반올림).</summary>
        public int TileX => Mathf.RoundToInt(transform.position.x);
        public int TileY => Mathf.RoundToInt(transform.position.y);

        /// <summary>현재 체력 (M21-W2). 쓰기는 ApplyHit 하나 — 차감할지 말지는 CombatService가
        /// 정한다 (ADR-M21-8: 위협의 죽음도 문 하나).</summary>
        public float Hp { get; private set; }

        /// <summary>등급 배율 (M24-1차 W4) — 체력·타격 피해에 곱해진 값. 1 = 종족 기본.
        /// 도주선 판정이 `MaxHp × 배율`을 분모로 써야 하므로 개체가 기억한다.</summary>
        public float GradeMult { get; private set; } = 1f;

        /// <summary>도주 전환 여부 (M21-W2) — 격퇴 사건의 표식. 도주한 개체는 다시 싸우지 않는다.</summary>
        public bool IsFleeing { get; private set; }

        /// <summary>퇴장 진행 중 — 서비스가 중복 퇴장 지시를 거르는 판독점.</summary>
        public bool IsExiting => _exiting;

        /// <summary>체류 중 (M21-W8 — HUD 습격 프롬프트 판독점). 도착~퇴장 전 = "습격 중".</summary>
        public bool Staying => _arrived && !_exiting;

        /// <summary>체류 시작 시각 (게임일) — MaxStayDays 판정의 원본. 쓰기는 MarkArrived.</summary>
        public float ArrivedDay { get; private set; }

        /// <summary>마지막 타격 시각 (게임일) — 재타격 주기 판정의 원본. 쓰기는 MarkStruck.</summary>
        public float LastStrikeDay { get; private set; }

        /// <summary>배회 앵커 (M21-W2R) — 도착 지점. 배회는 이 타일 반경 안에서만 돈다.
        /// 앵커를 현재 위치로 갱신하면 위협이 맵을 가로질러 표류한다 (배회가 아니라 유랑이 된다).</summary>
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

        /// <param name="gradeMult">등급 배율 (M24-1차 W4) — 체력·타격 피해에만 곱한다.
        /// ⚠️ 이동·공격 속도에는 안 건다: 개입 창 불변식(I1)이 그 둘에 걸려 있어서, 배율이
        /// 거기까지 가면 압력이 오를수록 플레이어가 반응할 시간이 사라진다.</param>
        public void Init(ThreatSO so, ThreatService svc, Vector2Int entry, Vector2Int target,
                         List<Vector2Int> waypoints, IPathfinder pathfinder, bool targetsVillagers,
                         int groupKey, float gradeMult = 1f)
        {
            So = so;
            _svc = svc;
            EntryTile = entry;
            TargetTile = target;
            TargetsVillagers = targetsVillagers;
            GroupKey = groupKey;
            _path = waypoints; // null = 이미 목표 (AlreadyThere)
            _wp = 0;
            _pathfinder = pathfinder;
            _lastChaseTile = target;
            _chaseGiveUpAt = Time.time + CHASE_GIVEUP_SEC;
            // M21-W2 — 개체 편차 없음 (몸값 불가침, 주민 체력과 같은 사상).
            // M24-1차 W4: 편차가 아니라 **등급**은 있다 (두목은 잡졸보다 단단하다). 배율은
            // 예산이 산 등급에서 오고, 같은 등급끼리는 여전히 편차가 0이다.
            GradeMult = Mathf.Max(0.01f, gradeMult);
            Hp = so.MaxHp * GradeMult;

            // 시각 (M22-3차 W5c): 스프라이트 세트가 있으면 주민과 **같은 애니메이터**를 쓴다
            // (AgentAnimator는 주민 타입을 모른다 — 후반 확장 규칙 ①의 배당금).
            // 세트가 없으면 기존 색 원 폴백 = 배선 전과 완전히 동일 (중립 불변식).
            var sr = gameObject.AddComponent<SpriteRenderer>();
            _sr = sr; // 깊이는 매 프레임 갱신 (움직인다 — 앞뒤는 월드 Y가 정한다)
            if (so.SpriteSet != null)
            {
                _anim = new AgentAnimator(sr, so.SpriteSet, Color.white); // 색조 없음 — 그림 그대로
                transform.localScale = Vector3.one;
            }
            else
            {
                sr.sprite = M0Sprites.Circle;
                sr.color = so.BodyColor;
                transform.localScale = Vector3.one * 0.9f;
            }
        }

        private SpriteRenderer _sr;       // 깊이 갱신 대상
        private AgentAnimator _anim;      // null = 색 원 폴백
        private Vector2 _facing = Vector2.down;  // 마지막 이동 방향 (멈춰도 보던 쪽을 본다)
        private float _attackUntil;       // 이 시각까지 공격 몸짓 (실시간 초)

        /// <summary>타격 순간 공격 몸짓을 켠다 (ThreatService의 타격 지점이 부른다) —
        /// 몸짓 길이는 표현 상수다 (판정과 무관).</summary>
        public void PlayAttack() => _attackUntil = Time.time + 0.4f;

        private void Update()
        {
            if (So == null) return; // Init 전 방어

            // 깊이 = 월드 Y (WorldSort). 같은 칸이면 주민 위 — 무는 장면이 가려지면 안 된다.
            if (_sr != null) _sr.sortingOrder = WorldSort.Order(transform.position.y, WorldSort.Threat);

            // 그림 틱 (M22-3차 W5c) — 걷는가/무엇을 하는가만 넘기고 프레임 선택은 애니메이터가.
            // 판정은 아래 로직이 그대로 소유한다 (표현은 읽기만 — M10-C ⚠️③).
            if (_anim != null)
            {
                bool walking = !_exiting && _path != null && _wp < _path.Count;
                AnimKind act = Time.time < _attackUntil ? AnimKind.Attack : AnimKind.None;
                _anim.Tick(Time.deltaTime, walking && act == AnimKind.None, _facing, act);
            }

            // 체류 틱 (M21-W2) — 재타격 주기·체류 상한은 게임일 기준이라 서비스가 판정한다.
            if (_arrived && !_exiting)
            {
                _svc.NotifyStrikeTick(this);
                if (_exiting) return;
            }

            // 추격 틱 (주민 타격형만) — 사거리·재타겟 판정. 체류 중에도 돈다: 도망친 주민을
            // 다시 쫓아가야 "눌러앉았다"가 성립한다 (제자리에 굳으면 첫 희생자만 계속 맞는다).
            // ⚠️ 공성 중엔 멈춘다 (M22-W5) — 시설로 가는 길에 주민을 재조준하면 공성이 영영 안
            // 시작된다. 돌파(공성 해제) 후 추격이 자연 재개된다.
            bool sieging = _svc.IsGroupSieging(GroupKey);
            if (!_exiting && TargetsVillagers && !sieging && Time.time >= _nextRetargetAt)
            {
                _nextRetargetAt = Time.time + RETARGET_SEC;
                TickChase();
                if (_exiting) return;
            }

            if (_path == null || _wp >= _path.Count)
            {
                if (_exiting) { DespawnNow(); return; }               // 퇴장 경로 소진 = 가장자리 도달
                // 밭형·공성형: 고정 목표 도착 = 체류 시작. 공성 무리는 원래 롤이 주민이어도
                // 시설 곁이 목적지다 (M22-W5) — 사거리 진입(IsInStrikeRange)을 기다리면
                // 주민이 울타리에서 먼 판에서 영영 도착하지 못한다.
                if ((!TargetsVillagers || sieging) && !_arrived) { Arrive(); return; }
                // 체류 중 목적지 도달 = 다음 배회 지점 (M21-W2R). ⚠️ 목적지에 **도착했을 때만**
                // 재선정한다 — 매 프레임이면 제자리 떨림 + 경로탐색 폭증 (명세 ⚠️ 오해 위험 2).
                if (_arrived) RepickWander();
                return; // 미도착 주민형은 제자리 — 재타겟 틱이 잇는다
            }

            var target = new Vector3(_path[_wp].x, _path[_wp].y, 0f); // ADR-M0-9 — X-Y 평면
            Vector3 before = transform.position;
            transform.position = Vector3.MoveTowards(transform.position, target,
                                                     So.MoveSpeed * Time.deltaTime);
            Vector3 moved = transform.position - before;
            if (moved.sqrMagnitude > 1e-8f) _facing = new Vector2(moved.x, moved.y).normalized;
            if ((transform.position - target).sqrMagnitude <= ARRIVE_EPSILON_SQR)
            {
                Vector2Int entered = _path[_wp];
                _wp++;
                // 밟았다 (M22-3차 W4) — 판정은 서비스가 한다 (개체는 표현+이동뿐, M10-C ⚠️③).
                // 웨이포인트는 중간 타일까지 보간되므로(JPS 계약) 지나간 칸이 새지 않는다.
                _svc.NoteThreatEnteredTile(this, entered);
            }
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

        /// <summary>체류 시각 기록 (ThreatService 전용) — 재타격 주기·체류 상한 판정의 원본.
        /// **한 출몰에 한 번만 시작한다** (M22-W5) — 공성 돌파 후 재진격(ResumeAdvance)이 다시
        /// 도착해도 시계는 이어진다. 리셋하면 공성 0.75일 + 체류 0.75일 = 시설이 시간을 **버는**
        /// 게 아니라 **늘려 주는** 역설이 된다 (ADR-M22-2 위반).</summary>
        public void MarkArrived(float gameDay)
        {
            if (_everArrived) return;
            _everArrived = true;
            ArrivedDay = gameDay;
        }
        private bool _everArrived;

        /// <summary>공성 돌파 후 재진격 (ThreatService 전용, M22-W5) — 체류를 풀고 새 목표로 걷는다.
        /// 체류 시계(ArrivedDay)는 리셋되지 않는다 (MarkArrived 1회 규약) — 남은 체류가 곧
        /// 뚫고 들어가서 할 수 있는 일의 전부다. waypoints null = 이미 도착 (다음 프레임 Arrive).</summary>
        public void ResumeAdvance(Vector2Int target, List<Vector2Int> waypoints)
        {
            if (_exiting) return;
            _arrived = false;
            TargetTile = target;
            _path = waypoints;
            _wp = 0;
            _lastChaseTile = new Vector2Int(int.MinValue, int.MinValue);
            _chaseGiveUpAt = Time.time + CHASE_GIVEUP_SEC; // 재진격 기브업 재장전 (도달 불가 안전장치)
        }

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
