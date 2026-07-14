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

            if (!Occupied(agent.TileX, agent.TileY))
                return true; // MoveTarget null = 제자리

            // 맵 경계 (ExploreRunner와 동일 규칙)
            AIVillage.Core.MapConfig map = AIVillage.Core.MapConfig.Active;
            int minX = -50, maxX = 49, minY = -50, maxY = 49;
            if (map != null)
            {
                minX = -map.mapOffset;
                maxX = map.mapSize - map.mapOffset - 1;
                minY = -map.mapOffset;
                maxY = map.mapSize - map.mapOffset - 1;
            }

            if (TryFindFreeTileNear(Occupied, agent.TileX, agent.TileY, minX, maxX, minY, maxY, out Vector2Int free))
            {
                MoveTarget = free;
                return true;
            }
            FailReason = $"{_so.Building.DisplayName}: 주변 {SEARCH_RADIUS}칸 내 건설 가능한 빈 타일 없음";
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

            return agent.Construction.Complete(_so.Building, agent.TileX, agent.TileY)
                ? RunnerResult.Succeeded
                : Fail($"{_so.Building.DisplayName} 완공 실패 (비용 부족 또는 중복)");
        }
    }
}
