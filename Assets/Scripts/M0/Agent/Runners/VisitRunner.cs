using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 방문 러너 (M8 후속): 지정 주민 곁까지 이동 → DurationSec 머무름 → 도착 통지.
    /// 대상이 사라졌으면(이탈) 실패 — 심부름 자체의 정리는 RequestService 타임아웃이 담당.
    /// </summary>
    public sealed class VisitRunner : ActionRunnerBase
    {
        private readonly VisitActionSO _so;
        private bool _arrivedNotified;

        public VisitRunner(VisitActionSO so) : base(so)
        {
            _so = so;
        }

        public override bool Prepare(VillagerAgent agent)
        {
            VillagerAgent target = agent.FindVisitTarget();
            if (target == null)
            {
                FailReason = $"방문 대상 없음 ({agent.VisitTargetAgentId ?? "?"} — 이탈했거나 미지정)";
                return false;
            }

            int dist = Mathf.Abs(agent.TileX - target.TileX) + Mathf.Abs(agent.TileY - target.TileY);
            if (dist <= _so.ArriveRadiusTiles) return true; // 이미 곁 — 제자리 실행

            // 대상 타일 정확히가 아니라 곁의 통행 가능 타일 — 예약 충돌·겹쳐 서기 방지 (ConsumeRunner 패턴)
            MoveTarget = MapBounds.PickWalkableNear(agent.IsWalkable, target.TileX, target.TileY,
                                                    _so.ArriveRadiusTiles);
            return true;
        }

        public override RunnerResult Tick(VillagerAgent agent, float dt)
        {
            if (!DurationElapsed(dt)) return RunnerResult.Running;
            if (!_arrivedNotified)
            {
                _arrivedNotified = true;
                agent.CompleteVisit(); // 도착 통지 — 장면·보상·심부름 정리는 RequestService
            }
            return RunnerResult.Succeeded;
        }
    }
}
