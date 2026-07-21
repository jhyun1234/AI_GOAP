using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 위협 개체 (M10-C) — 표현+이동만. 주민이 아니다 (ADR-M10-4): VillagerAgent 미재사용,
    /// 타일 예약 비참여 (일과성 통과자 — 주민과 겹쳐도 무해, M11 전투에서 재검토), 타격 판정은
    /// ThreatService(NotifyArrived)가 수행한다 (명세 M10-C ⚠️③). 경로는 스폰 시 서비스가
    /// IPathfinder로 구해 넘긴다 — 이 클래스는 웨이포인트를 걸을 뿐이다.
    /// 세이브 대상 아님 (ADR-M10-10 — 로드 후 다음 스케줄에서 재출몰).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThreatAgent : MonoBehaviour
    {
        private const float ARRIVE_EPSILON_SQR = 0.0001f; // 도착 판정 (알고리즘 상수)

        public ThreatSO So { get; private set; }
        public Vector2Int EntryTile { get; private set; }
        public Vector2Int TargetTile { get; private set; }

        /// <summary>현재 논리 타일 — IsNearThreat(감지)·거리 판정용 (표현 위치의 반올림).</summary>
        public int TileX => Mathf.RoundToInt(transform.position.x);
        public int TileY => Mathf.RoundToInt(transform.position.y);

        private ThreatService _svc;
        private List<Vector2Int> _path; // null·소진 = 도착
        private int _wp;
        private bool _exiting;

        public void Init(ThreatSO so, ThreatService svc, Vector2Int entry, Vector2Int target,
                         List<Vector2Int> waypoints)
        {
            So = so;
            _svc = svc;
            EntryTile = entry;
            TargetTile = target;
            _path = waypoints; // null = 이미 목표 (AlreadyThere) — 첫 Update가 즉시 도착 처리
            _wp = 0;

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

            if (_path == null || _wp >= _path.Count)
            {
                Arrive();
                return;
            }

            var target = new Vector3(_path[_wp].x, _path[_wp].y, 0f); // ADR-M0-9 — X-Y 평면
            transform.position = Vector3.MoveTowards(transform.position, target,
                                                     So.MoveSpeed * Time.deltaTime);
            if ((transform.position - target).sqrMagnitude <= ARRIVE_EPSILON_SQR) _wp++;
        }

        private void Arrive()
        {
            if (!_exiting)
            {
                _exiting = true;
                _svc.NotifyArrived(this); // 타격 + 퇴장 경로 부여 (또는 즉시 소멸)
            }
            else DespawnNow(); // 퇴장 경로 소진 — 가장자리 도달
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
