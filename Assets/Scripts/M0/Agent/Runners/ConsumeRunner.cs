using AIVillage.Core;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 소비 러너 (식사 등). EatAtAnchor면 건물(모닥불) 곁으로 이동 후 먹는다 —
    /// 연속 식사(플랜 내 Eat×N)는 첫 액션만 걷고 나머지는 제자리 (이미 도착).
    /// 스톡 차감·욕구 회복은 EffectApplier가 수행한다.
    /// </summary>
    public sealed class ConsumeRunner : ActionRunnerBase
    {
        private readonly ConsumeActionSO _so;

        public ConsumeRunner(ConsumeActionSO so) : base(so)
        {
            _so = so;
        }

        public override bool Prepare(VillagerAgent agent)
        {
            if (!_so.EatAtAnchor) return true; // 제자리 식사

            int cx, cy;
            if (agent.Construction.TryGetAnchorTile(_so.AnchorPriority, agent.TileX, agent.TileY, out Vector2Int built))
            {
                cx = built.x; cy = built.y;
            }
            else if (agent.WorldConfig != null)
            {
                cx = agent.WorldConfig.BaseTileX; cy = agent.WorldConfig.BaseTileY;
            }
            else return true;

            // 반경 내 산개 — 정확히 같은 타일을 노리면 타일 예약 충돌 + 불 위에 서게 됨.
            // 통행 불가 타일(집 등)은 목표에서 제외 (M4-E — 경로 실패 소음 제거)
            MoveTarget = MapBounds.PickWalkableNear(agent.IsWalkable, cx, cy, _so.AnchorRadius);
            return true;
        }

        public override RunnerResult Tick(VillagerAgent agent, float dt)
            => DurationElapsed(dt) ? RunnerResult.Succeeded : RunnerResult.Running;
    }
}
