using AIVillage.Core;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 여가 배회 러너 (M1-A): 기준점 반경 내 랜덤 타일로 이동 → DurationSec 머무름.
    /// 세계 상태를 일절 바꾸지 않는다 — 순수 표현/리듬용 (ADR-M1-3).
    /// </summary>
    public sealed class WanderRunner : ActionRunnerBase
    {
        private readonly WanderActionSO _so;

        public WanderRunner(WanderActionSO so) : base(so)
        {
            _so = so;
        }

        public override bool Prepare(VillagerAgent agent)
        {
            // 기준점: 건물 완공 위치 → (미완공 폴백) 기지 → (건물 앵커 아님) 현재 위치
            int cx = agent.TileX, cy = agent.TileY;
            if (_so.AnchorAtBuilding)
            {
                if (agent.Construction.TryGetBuiltTile(_so.AnchorFlagSlot, out Vector2Int built))
                {
                    cx = built.x; cy = built.y;
                }
                else if (agent.WorldConfig != null)
                {
                    cx = agent.WorldConfig.BaseTileX; cy = agent.WorldConfig.BaseTileY;
                }
            }

            // 맵 경계 클램프 (ExploreRunner와 동일 규칙)
            MapConfig map = MapConfig.Active;
            int minX = -50, maxX = 49, minY = -50, maxY = 49;
            if (map != null)
            {
                minX = -map.mapOffset;
                maxX = map.mapSize - map.mapOffset - 1;
                minY = -map.mapOffset;
                maxY = map.mapSize - map.mapOffset - 1;
            }

            int r = Mathf.Max(1, _so.WanderRadius);
            int tx = Mathf.Clamp(cx + Random.Range(-r, r + 1), minX, maxX);
            int ty = Mathf.Clamp(cy + Random.Range(-r, r + 1), minY, maxY);
            MoveTarget = new Vector2Int(tx, ty); // 현재 타일과 같으면 제자리 머무름
            return true;
        }

        public override RunnerResult Tick(VillagerAgent agent, float dt)
            => DurationElapsed(dt) ? RunnerResult.Succeeded : RunnerResult.Running;
    }
}
