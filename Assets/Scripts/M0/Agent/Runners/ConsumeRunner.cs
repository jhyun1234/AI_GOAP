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
        // 조리 앵커 (M22-3차 W5d, 표현 전용) — 도착해서 실제로 끓이기 시작한 뒤에만 켠다.
        // 걸어가는 동안 솥이 걸리면 "요리를 시작하면"이 아니라 "요리하러 나서면"이 된다.
        private Vector2Int? _cookAnchor;

        public ConsumeRunner(ConsumeActionSO so) : base(so)
        {
            _so = so;
        }

        public override bool Prepare(VillagerAgent agent)
        {
            // 조리 플래그가 꺼진 액션(식사·간식)은 배율 경로 자체를 타지 않는다 —
            // 여기서 분기하지 않으면 요리사가 밥도 빨리 먹게 되어 몸값 불가침이 깨진다.
            // M20 — 조리 노동에만 직업 효율이 곱해진다 (ADR-M20-3). 식사는 영원히 1 (ADR-M5-3).
            DurationMult = _so.IsCookingWork ? agent.CookDurationMultOfJob() : 1f;

            if (!_so.EatAtAnchor) return true; // 제자리 식사

            int cx, cy;
            if (agent.ResolveAnchor(_so.AnchorPriority, out Vector2Int built)) // 내 집 우선 (M8-C)
            {
                cx = built.x; cy = built.y;
                // 조리 노동만 기억한다 — 밥 먹으러 온 사람 때문에 솥이 걸리면 거짓말이 된다
                if (_so.IsCookingWork) _cookAnchor = built;
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
        {
            // Tick은 도착 후에만 돈다(FSM Acting) — 여기가 "끓이기 시작한" 순간이다
            if (_cookAnchor.HasValue && !agent.CookingAt.HasValue) agent.SetCookingAt(_cookAnchor);
            return DurationElapsed(dt, DurationMult) ? RunnerResult.Succeeded : RunnerResult.Running;
        }

        /// <summary>성공·실패·중단 어느 쪽으로 끝나도 솥은 내린다 (⚠️ 여기를 빼면 조리 그림이
        /// 영원히 남는다 — 플랜 취소·사망도 이 문을 지난다).</summary>
        public override void Cleanup(VillagerAgent agent)
        {
            if (_cookAnchor.HasValue) agent.SetCookingAt(null);
        }
    }
}
