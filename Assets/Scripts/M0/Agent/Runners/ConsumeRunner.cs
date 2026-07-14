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
            if (agent.Construction.TryGetFirstBuiltAnchor(_so.AnchorPriority, out Vector2Int built))
            {
                cx = built.x; cy = built.y;
            }
            else if (agent.WorldConfig != null)
            {
                cx = agent.WorldConfig.BaseTileX; cy = agent.WorldConfig.BaseTileY;
            }
            else return true;

            // 반경 내 산개 — 정확히 같은 타일을 노리면 타일 예약 충돌 + 불 위에 서게 됨
            MapConfig map = MapConfig.Active;
            int minX = -50, maxX = 49, minY = -50, maxY = 49;
            if (map != null)
            {
                minX = -map.mapOffset;
                maxX = map.mapSize - map.mapOffset - 1;
                minY = -map.mapOffset;
                maxY = map.mapSize - map.mapOffset - 1;
            }
            int r = Mathf.Max(1, _so.AnchorRadius);
            MoveTarget = new Vector2Int(
                Mathf.Clamp(cx + Random.Range(-r, r + 1), minX, maxX),
                Mathf.Clamp(cy + Random.Range(-r, r + 1), minY, maxY));
            return true;
        }

        public override RunnerResult Tick(VillagerAgent agent, float dt)
            => DurationElapsed(dt) ? RunnerResult.Succeeded : RunnerResult.Running;
    }
}
