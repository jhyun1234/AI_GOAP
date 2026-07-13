using AIVillage.Core;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 탐험 러너: 미발견 노드 방향(1순위) 또는 랜덤 방향(2순위)으로 MoveDistanceTiles 이동.
    /// 실제 발견은 이동 중 에이전트가 타일마다 수행(FoW 공개 + DiscoverArea) —
    /// SO Effects의 NearDiscovered*는 플래너 전용이라 여기서 아무 것도 세팅하지 않는다.
    /// (舊 FindExplorationTarget 로직 이관)
    /// </summary>
    public sealed class ExploreRunner : ActionRunnerBase
    {
        private readonly ExploreActionSO _so;

        public ExploreRunner(ExploreActionSO so) : base(so)
        {
            _so = so;
        }

        public override bool Prepare(VillagerAgent agent)
        {
            // 1순위: 가장 가까운 미발견 노드 방향
            ResourceNode target = agent.Discovery.FindNearestUndiscovered(agent.TileX, agent.TileY);
            if (target != null)
            {
                MoveTarget = new Vector2Int(target.TileX, target.TileY);
                return true;
            }

            // 2순위: 랜덤 방향 MoveDistanceTiles (맵 경계 클램프)
            MapConfig map = MapConfig.Active;
            int minX = -50, maxX = 49, minY = -50, maxY = 49;
            if (map != null)
            {
                minX = -map.mapOffset;
                maxX = map.mapSize - map.mapOffset - 1;
                minY = -map.mapOffset;
                maxY = map.mapSize - map.mapOffset - 1;
            }
            Vector2 dir = Random.insideUnitCircle.normalized;
            int tx = Mathf.Clamp(agent.TileX + Mathf.RoundToInt(dir.x * _so.MoveDistanceTiles), minX, maxX);
            int ty = Mathf.Clamp(agent.TileY + Mathf.RoundToInt(dir.y * _so.MoveDistanceTiles), minY, maxY);
            MoveTarget = new Vector2Int(tx, ty);
            return true;
        }

        public override RunnerResult Tick(VillagerAgent agent, float dt)
            => DurationElapsed(dt) ? RunnerResult.Succeeded : RunnerResult.Running;
    }
}
