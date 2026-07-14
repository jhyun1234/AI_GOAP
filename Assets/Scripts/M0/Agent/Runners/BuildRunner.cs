using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 건설 러너: 제자리 실행 (舊 BC4 — 건설 지점으로의 장거리 이동 스킵 계승).
    /// 단, 현재 타일에 자원 노드/기존 건물이 있으면 인접 빈 타일로 한 발짝 옮겨 짓는다
    /// (M2-C 리뷰 ① 발견 — 밭이 노드를 덮는 문제. BC4의 본질은 유지).
    /// 세계 반영은 ConstructionService.Complete()가 전담하므로 AppliesOwnEffects = true —
    /// 에이전트의 EffectApplier 일괄 적용을 건너뛴다 (이중 차감 방지, ADR-M0-3).
    /// </summary>
    public sealed class BuildRunner : ActionRunnerBase
    {
        private const int SEARCH_RADIUS = 3; // 알고리즘 상수 — 인접 빈 타일 링 탐색 한계

        private readonly BuildActionSO _so;
        private Vector2Int _buildTile; // 완공 위치 — 차단 건물은 서는 타일(MoveTarget)과 분리 (ADR-M3-3)

        public override bool AppliesOwnEffects => true;

        public BuildRunner(BuildActionSO so) : base(so)
        {
            _so = so;
        }

        public override bool Prepare(VillagerAgent agent)
        {
            if (_so.Building == null)
            {
                FailReason = $"{_so.name}: BuildingSO 미연결";
                return false;
            }

            bool Occupied(int x, int y)
                => agent.Discovery.HasNodeAt(x, y) || agent.Construction.HasBuildingAt(x, y);

            // 맵 경계 — MapBounds 단일 출처 (M3-F)
            MapBounds.Get(out int minX, out int maxX, out int minY, out int maxY);

            // 군집 앵커: 동종 수량형 건물이 이미 있으면 그 곁부터 — "밭은 밭 옆에" (상식적 자율)
            Vector2Int cluster = default;
            bool hasCluster = _so.Building.IsCountable
                && agent.Construction.TryGetNearestBuiltTile(
                       _so.Building.CountSlot, agent.TileX, agent.TileY, out cluster);

            if (!TryPickBuildTile(Occupied, hasCluster, cluster,
                    new Vector2Int(agent.TileX, agent.TileY), minX, maxX, minY, maxY,
                    out _buildTile, out bool needMove))
            {
                FailReason = $"{_so.Building.DisplayName}: 주변 {SEARCH_RADIUS}칸 내 건설 가능한 빈 타일 없음";
                return false;
            }

            if (_so.Building.BlocksMovement)
            {
                // 차단 건물은 자기가 만든 벽 위에 설 수 없다 — 건설 타일 인접 빈 칸에서 짓는다 (ADR-M3-3)
                if (!TryPickStandTile(Occupied, _buildTile, minX, maxX, minY, maxY, out Vector2Int stand))
                {
                    FailReason = $"{_so.Building.DisplayName}: 건설 타일 곁에 설 자리 없음";
                    return false;
                }
                MoveTarget = stand;
            }
            else if (needMove)
            {
                MoveTarget = _buildTile;
            }
            return true;
        }

        /// <summary>차단 건물 건설자가 설 타일 — 건설 타일 자신을 제외한 인접 빈 칸 (순수 함수, 게이트 대상).</summary>
        public static bool TryPickStandTile(System.Func<int, int, bool> occupied, Vector2Int buildTile,
            int minX, int maxX, int minY, int maxY, out Vector2Int standTile)
        {
            bool Blocked(int x, int y) => (x == buildTile.x && y == buildTile.y) || occupied(x, y);
            return TryFindFreeTileNear(Blocked, buildTile.x, buildTile.y, minX, maxX, minY, maxY, out standTile);
        }

        /// <summary>
        /// 건설 타일 결정 (순수 함수 — EditMode 게이트 대상):
        /// ① 군집 앵커(동종 건물) 곁 빈 타일 → ② 현재 타일 비점유면 제자리 →
        /// ③ 현재 위치 곁 빈 타일 → ④ 실패 (좌표 스냅 없이 재계획).
        /// </summary>
        public static bool TryPickBuildTile(System.Func<int, int, bool> occupied,
            bool hasClusterAnchor, Vector2Int clusterAnchor, Vector2Int agentTile,
            int minX, int maxX, int minY, int maxY, out Vector2Int tile, out bool needMove)
        {
            if (hasClusterAnchor
                && TryFindFreeTileNear(occupied, clusterAnchor.x, clusterAnchor.y, minX, maxX, minY, maxY, out tile))
            {
                needMove = true;
                return true;
            }
            if (!occupied(agentTile.x, agentTile.y))
            {
                tile = agentTile;
                needMove = false;
                return true;
            }
            if (TryFindFreeTileNear(occupied, agentTile.x, agentTile.y, minX, maxX, minY, maxY, out tile))
            {
                needMove = true;
                return true;
            }
            needMove = false;
            return false;
        }

        /// <summary>
        /// 중심 기준 링 순회(반경 1→3)로 첫 비점유 타일 탐색. 순수 함수 — EditMode 게이트 대상.
        /// </summary>
        public static bool TryFindFreeTileNear(System.Func<int, int, bool> occupied,
            int cx, int cy, int minX, int maxX, int minY, int maxY, out Vector2Int tile)
        {
            for (int r = 1; r <= SEARCH_RADIUS; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue; // 링 테두리만
                        int x = cx + dx, y = cy + dy;
                        if (x < minX || x > maxX || y < minY || y > maxY) continue;
                        if (occupied(x, y)) continue;
                        tile = new Vector2Int(x, y);
                        return true;
                    }
                }
            }
            tile = default;
            return false;
        }

        public override RunnerResult Tick(VillagerAgent agent, float dt)
        {
            if (!DurationElapsed(dt)) return RunnerResult.Running;

            // 완공 위치 = Prepare에서 정한 건설 타일 (비차단은 도착 타일과 동일, 차단은 인접에서 시공)
            return agent.Construction.Complete(_so.Building, _buildTile.x, _buildTile.y)
                ? RunnerResult.Succeeded
                : Fail($"{_so.Building.DisplayName} 완공 실패 (비용 부족 또는 중복)");
        }
    }
}
