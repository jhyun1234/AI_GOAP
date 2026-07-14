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
                if (agent.Construction.TryGetAnchorTile(_so.AnchorPriority, agent.TileX, agent.TileY, out Vector2Int built))
                {
                    cx = built.x; cy = built.y;
                }
                else if (agent.WorldConfig != null)
                {
                    cx = agent.WorldConfig.BaseTileX; cy = agent.WorldConfig.BaseTileY;
                }
            }

            // 맵 경계 클램프 — MapBounds 단일 출처 (M3-F)
            int r = Mathf.Max(1, _so.WanderRadius);
            MoveTarget = MapBounds.Clamp(cx + Random.Range(-r, r + 1), cy + Random.Range(-r, r + 1));
            // 현재 타일과 같으면 제자리 머무름
            return true;
        }

        public override RunnerResult Tick(VillagerAgent agent, float dt)
            => DurationElapsed(dt) ? RunnerResult.Succeeded : RunnerResult.Running;
    }
}
