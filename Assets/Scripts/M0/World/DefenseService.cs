using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 방어 계획의 집 (M22-W3, Docs/M22_방어건설_실행명세서.md) — W5에서 시설 내구도까지 얹는다
    /// (ADR-M22-3: 내구도 상태의 단일 소유자).
    /// W3 범위: 플레이어가 확정한 방어 구역(ZoneService.OnZoneEstablished 구독)의 둘레를
    /// 결정적으로 계산해 "지을 자리"(울타리 N + 문 1)를 만들고, 완공마다 계획에서 차감한다.
    /// 시공은 W4의 건설 goal 몫 — 이 서비스는 자리만 안다 (구역 지정 ≠ 즉시 건설, 합의 2).
    /// 세이브 대상 (ADR-M0-10: 계획 잔여·문 자리 — 내구도는 W5에서 함께 선언).
    /// </summary>
    public sealed class DefenseService
    {
        private readonly List<Vector2Int> _plannedFences = new List<Vector2Int>();
        private Vector2Int? _plannedGate;

        /// <summary>계획이 수립됐는가 (구역 확정 후 true — 전량 완공돼도 유지. 재수립 없음, ADR-M22-4).</summary>
        public bool HasPlan { get; private set; }

        /// <summary>미건설 울타리 자리 (읽기 전용 — W4 BuildRunner가 시공자 최근접을 고른다).</summary>
        public IReadOnlyList<Vector2Int> PlannedFenceTiles => _plannedFences;

        /// <summary>미건설 문 자리. null = 없음(미계획 또는 완공됨).</summary>
        public Vector2Int? PlannedGateTile => _plannedGate;

        /// <summary>미건설 잔여 총수 — DefensePlannedCount 슬롯의 유일한 원천 (Goal_BuildDefense 트리거).</summary>
        public int PlannedCount => _plannedFences.Count + (_plannedGate.HasValue ? 1 : 0);

        /// <summary>
        /// 체비쇼프 사각(anchor ± radius)의 경계 타일 — 결정적 순서 (아랫변 좌→우, 오른변 아래→위,
        /// 윗변 우→좌, 왼변 위→아래). 같은 앵커·반경이면 언제나 같은 목록 (순수, 게이트 대상).
        /// radius ≥ 1이면 타일 수 = 8 × radius.
        /// </summary>
        public static List<Vector2Int> PerimeterTiles(Vector2Int anchor, int radius)
        {
            var tiles = new List<Vector2Int>(Mathf.Max(1, 8 * radius));
            if (radius <= 0) { tiles.Add(anchor); return tiles; }
            int minX = anchor.x - radius, maxX = anchor.x + radius;
            int minY = anchor.y - radius, maxY = anchor.y + radius;
            for (int x = minX; x <= maxX; x++) tiles.Add(new Vector2Int(x, minY));          // 아랫변
            for (int y = minY + 1; y <= maxY; y++) tiles.Add(new Vector2Int(maxX, y));      // 오른변
            for (int x = maxX - 1; x >= minX; x--) tiles.Add(new Vector2Int(x, maxY));      // 윗변
            for (int y = maxY - 1; y >= minY + 1; y--) tiles.Add(new Vector2Int(minX, y));  // 왼변
            return tiles;
        }

        /// <summary>문 자리 = 기지 최근접(맨해튼) 둘레 타일 (순수, 결정적 — 동률이면 목록 앞 우선).
        /// 주민들이 아침마다 기지 쪽 문으로 줄지어 나가는 동선의 근거 (§7 재미 검증).</summary>
        public static Vector2Int PickGateTile(IReadOnlyList<Vector2Int> perimeter, Vector2Int baseTile)
        {
            Vector2Int best = perimeter[0];
            int bestDist = int.MaxValue;
            foreach (Vector2Int t in perimeter)
            {
                int d = Mathf.Abs(t.x - baseTile.x) + Mathf.Abs(t.y - baseTile.y);
                if (d < bestDist) { bestDist = d; best = t; }
            }
            return best;
        }

        /// <summary>
        /// 구역 확정 → 계획 수립 (1회 — 이미 있으면 무시, ADR-M22-4). tileBuildable 필터(맵 밖·
        /// 기존 건물·통행 불가 제외)는 조립 배선이 주입한다 — 서비스는 맵을 모른다.
        /// 문은 필터를 통과한 둘레에서 고른다 (막힌 자리에 문을 계획하면 영원히 못 짓는다).
        /// </summary>
        public void EstablishPlan(Vector2Int anchor, int radius, Vector2Int baseTile,
                                  Func<int, int, bool> tileBuildable)
        {
            if (HasPlan) return;
            List<Vector2Int> perimeter = PerimeterTiles(anchor, radius);
            var buildable = new List<Vector2Int>(perimeter.Count);
            foreach (Vector2Int t in perimeter)
                if (tileBuildable == null || tileBuildable(t.x, t.y))
                    buildable.Add(t);
            if (buildable.Count == 0)
            {
                Debug.LogWarning($"[Defense] 방어 구역 @ ({anchor.x},{anchor.y}) r={radius} — " +
                                 "지을 수 있는 둘레 타일이 0개라 계획 없음.");
                return;
            }

            Vector2Int gate = PickGateTile(buildable, baseTile);
            _plannedGate = gate;
            _plannedFences.Clear();
            foreach (Vector2Int t in buildable)
                if (t != gate) _plannedFences.Add(t);
            HasPlan = true;
            Debug.Log($"[Defense] 방어 계획 수립 — 울타리 {_plannedFences.Count} + 문 1 " +
                      $"(제외 {perimeter.Count - buildable.Count}) @ ({anchor.x},{anchor.y}) r={radius}");
        }

        /// <summary>
        /// 다음 시공 자리 (M22-W4) — 문 에셋(GateCount)은 문 자리, 울타리는 from 최근접(맨해튼)
        /// 계획 타일. occupied 필터(노드·기존 건물·타 주민 예약)는 BuildRunner가 주입 — 계획
        /// 수립 후 상태가 변했을 수 있어 시공 시점에 다시 거른다. 문/울타리 구분이 여기 있는
        /// 이유: 계획 의미의 단일 소유자가 이 서비스다 (러너에 슬롯 분기를 흩뿌리지 않는다).
        /// </summary>
        public bool TryGetNextBuildTile(BuildingSO b, Vector2Int from,
                                        Func<int, int, bool> occupied, out Vector2Int tile)
        {
            tile = default;
            if (b == null || !b.PlaceOnDefensePlan) return false;
            if (b.CountSlot == SlotId.GateCount)
            {
                if (!_plannedGate.HasValue) return false;
                Vector2Int g = _plannedGate.Value;
                if (occupied != null && occupied(g.x, g.y)) return false;
                tile = g;
                return true;
            }
            int bestDist = int.MaxValue;
            bool found = false;
            foreach (Vector2Int t in _plannedFences)
            {
                if (occupied != null && occupied(t.x, t.y)) continue;
                int d = Mathf.Abs(t.x - from.x) + Mathf.Abs(t.y - from.y);
                if (d < bestDist) { bestDist = d; tile = t; found = true; }
            }
            return found;
        }

        /// <summary>
        /// 완공 통지 — 방어 계획 건물(PlaceOnDefensePlan)만 해당 타일을 계획에서 차감한다.
        /// ConstructionService.OnCompleted 구독 배선이 호출 (완공 자체는 Complete()만이 한다, ADR-M0-3).
        /// </summary>
        public void NotifyBuilt(BuildingSO b, int x, int y)
        {
            if (b == null || !b.PlaceOnDefensePlan) return;
            var tile = new Vector2Int(x, y);
            if (_plannedGate.HasValue && _plannedGate.Value == tile) _plannedGate = null;
            else _plannedFences.Remove(tile);
        }
    }
}
