using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 농사 러너 (M2-C): Kind별 최근접 빈/익은 밭으로 이동 → DurationSec 후 심기/수확.
    /// 밭 상태 전이만 러너 경유(FarmService.TryPlant/TryHarvest) — 수확 스톡(+RawFood)은
    /// SO Effects를 EffectApplier가 적용한다 (플래너와 같은 수치, 단일 출처).
    /// 점유는 1인 (ResourceNode.TryOccupy 패턴 미러), 해제는 Cleanup에서 반드시.
    /// </summary>
    public sealed class FarmRunner : ActionRunnerBase
    {
        private readonly FarmActionSO _so;
        private FarmPlot _plot;

        public FarmRunner(FarmActionSO so) : base(so)
        {
            _so = so;
        }

        public override bool Prepare(VillagerAgent agent)
        {
            _plot = _so.Kind == FarmActionKind.Plant
                ? agent.Farm.NearestEmpty(agent.TileX, agent.TileY)
                : agent.Farm.NearestRipe(agent.TileX, agent.TileY);

            if (_plot == null)
            {
                FailReason = _so.Kind == FarmActionKind.Plant
                    ? "빈 밭 없음 (전부 점유/재배 중)"
                    : "익은 밭 없음 (전부 점유/미성숙)";
                return false;
            }
            if (!_plot.TryClaim(agent.AgentId))
            {
                FailReason = "밭 점유 중 (다른 주민 작업)";
                _plot = null;
                return false;
            }
            MoveTarget = _plot.Tile;
            return true;
        }

        public override RunnerResult Tick(VillagerAgent agent, float dt)
        {
            if (!DurationElapsed(dt)) return RunnerResult.Running;

            bool ok = _so.Kind == FarmActionKind.Plant
                ? agent.Farm.TryPlant(_plot)
                : agent.Farm.TryHarvest(_plot);
            return ok ? RunnerResult.Succeeded : Fail("밭 상태 불일치 — 점유 중 외부 전이 (버그 의심)");
        }

        public override void Cleanup(VillagerAgent agent)
        {
            _plot?.Release();
            _plot = null;
        }
    }
}
