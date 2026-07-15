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

            // 2순위: 랜덤 방향 MoveDistanceTiles. 도착점이 통행 불가(집 등)면 그 곁 타일로 보정 (M4-E)
            Vector2 dir = Random.insideUnitCircle.normalized;
            Vector2Int raw = MapBounds.Clamp(
                agent.TileX + Mathf.RoundToInt(dir.x * _so.MoveDistanceTiles),
                agent.TileY + Mathf.RoundToInt(dir.y * _so.MoveDistanceTiles));
            MoveTarget = agent.IsWalkable(raw.x, raw.y)
                ? raw
                : MapBounds.PickWalkableNear(agent.IsWalkable, raw.x, raw.y, 2);
            return true;
        }

        public override RunnerResult Tick(VillagerAgent agent, float dt)
            => DurationElapsed(dt) ? RunnerResult.Succeeded : RunnerResult.Running;
    }
}
