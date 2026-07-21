using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 도망 러너 (M10-D) — 피신처 곁 통행 가능 타일로 이동, 도착 즉시 완료.
    /// 별도 인터럽트 경로 없음: 위협 감지(ThreatNear)로 Goal_Flee가 뜨면 기존
    /// "상위 goal 전환"(0.5초 재검사)이 하던 일을 끊는다 (명세 M10-D ⚠️① — 새 경로 금지).
    /// 위협이 여전히 근처면 다음 재평가가 다시 도망을 선택한다 (모닥불 곁 서성임 = 연출).
    /// </summary>
    public sealed class FleeRunner : ActionRunnerBase
    {
        private readonly FleeActionSO _so;

        public FleeRunner(FleeActionSO so) : base(so)
        {
            _so = so;
        }

        public override bool Prepare(VillagerAgent agent)
        {
            int cx, cy;
            if (agent.ResolveAnchor(_so.AnchorPriority, out Vector2Int safe)) // 내 집 우선 (M8-C)
            {
                cx = safe.x; cy = safe.y;
            }
            else if (agent.WorldConfig != null)
            {
                cx = agent.WorldConfig.BaseTileX; cy = agent.WorldConfig.BaseTileY; // 기지 폴백
            }
            else return true; // 방어 — 제자리 완료 (미배선 테스트 등)

            MoveTarget = MapBounds.PickWalkableNear(agent.IsWalkable, cx, cy, _so.AnchorRadius);
            return true;
        }

        public override RunnerResult Tick(VillagerAgent agent, float dt)
            => RunnerResult.Succeeded; // 도착 즉시 완료 (효과는 플래너 픽션 — 실제 해소는 위협 퇴장)
    }
}
