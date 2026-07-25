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
    ///   주민 타격형 = 추격: 주기 재타겟(최근접 비부상 주민) + 사거리 진입 시 그 자리에서 타격.
    ///   늑대 2.5 > 주민 2.0 속도라 수렴이 결정적 (ADR-M10-1 — 확률 아님), 늦게 뛴 고집쟁이·
    ///   절뚝이는 도망자가 먼저 잡힌다. 기브업 상한은 도달 불가 지형의 안전장치일 뿐.
    /// 세이브 대상 아님 (ADR-M10-10 — 로드 후 다음 스케줄에서 재출몰).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThreatAgent : MonoBehaviour
    {
        private const float ARRIVE_EPSILON_SQR = 0.0001f; // 도착 판정 (알고리즘 상수)
        private const float RETARGET_SEC = 1f;            // 추격 재타겟 주기 (알고리즘 상수)
        private const float CHASE_GIVEUP_SEC = 90f;       // 추격 안전 상한 — 속도 우위라 정상 케이스 미도달

        public ThreatSO So { get; private set; }
        public Vector2Int EntryTile { get; private set; }
        public Vector2Int TargetTile { get; private set; }

        /// <summary>이번 출몰이 주민을 노리는가 (M10R, ADR-M10R-4) — 출몰 시 서비스가 롤해 주입.
        /// 이동 모드(추격/고정)·타격·HUD 분기가 모두 이 값을 읽는다. 밴드 고정 아님.</summary>
        public bool TargetsVillagers { get; private set; }

        /// <summary>현재 논리 타일 — IsNearThreat(감지)·사거리·타격 지점의 기준 (표현 위치의 반올림).</summary>
        public int TileX => Mathf.RoundToInt(transform.position.x);
        public int TileY => Mathf.RoundToInt(transform.position.y);

        private ThreatService _svc;
        private IPathfinder _pathfinder;
        private List<Vector2Int> _path; // null·소진 = 목적지 도달 (모드별 해석)
        private int _wp;
        private bool _exiting;
        private float _nextRetargetAt;
        private float _chaseGiveUpAt;
        private Vector2Int _lastChaseTile;

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

            // 추격 틱 (주민 타격형만) — 사거리·재타겟 판정. 타격으로 전환되면 이번 프레임 종료.
            if (!_exiting && TargetsVillagers && Time.time >= _nextRetargetAt)
            {
                _nextRetargetAt = Time.time + RETARGET_SEC;
                TickChase();
                if (_exiting) return;
            }

            if (_path == null || _wp >= _path.Count)
            {
                if (_exiting) { DespawnNow(); return; }              // 퇴장 경로 소진 = 가장자리 도달
                if (!TargetsVillagers) { StrikeAndExit(); return; } // 밭형: 고정 목표 도착 = 타격
                return; // 주민형: 경로 소진 = 제자리 대기 — 다음 재타겟 틱이 잇는다
            }

            var target = new Vector3(_path[_wp].x, _path[_wp].y, 0f); // ADR-M0-9 — X-Y 평면
            transform.position = Vector3.MoveTowards(transform.position, target,
                                                     So.MoveSpeed * Time.deltaTime);
            if ((transform.position - target).sqrMagnitude <= ARRIVE_EPSILON_SQR) _wp++;
        }

        /// <summary>추격 판정 1틱 — 순서: 사거리(잡았다) → 기브업 → 대상 없음(빈 타격) → 재경로.</summary>
        private void TickChase()
        {
            if (_svc.IsInStrikeRange(this) || Time.time >= _chaseGiveUpAt)
            {
                StrikeAndExit();
                return;
            }
            if (!_svc.TryPickChaseTile(TileX, TileY, out Vector2Int chase))
            {
                StrikeAndExit(); // 부상 가능 주민 없음 — 빈 타격(0명) 후 퇴장 (재해 0/0 관례)
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
                StrikeAndExit(); // 같은 타일 = 사거리 내 (방어 — 정상은 IsInStrikeRange가 먼저 잡는다)
            // Unreachable: 기존 경로 유지, 다음 틱 재시도 — 기브업 상한이 무한 추격을 막는다
        }

        /// <summary>타격 전환의 유일한 지점 — 서비스 통지 후 퇴장 모드 (이중 전환 방어).</summary>
        private void StrikeAndExit()
        {
            if (_exiting) return;
            _exiting = true;
            _svc.NotifyArrived(this); // 타격 + 퇴장 경로 부여 (또는 즉시 소멸)
        }

        /// <summary>퇴장 경로 설정 (ThreatService 전용) — 타격 후 진입점으로 되돌아간다.</summary>
        public void SetExitPath(List<Vector2Int> waypoints)
        {
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
