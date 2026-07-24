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
            // 내 밭만 후보 (M11-E) — 개간·파종·수확은 전부 집주인 본인의 일이다. 남의 밭은
            // 아예 조회되지 않으므로 "타인 밭 노동"이 구조적으로 불가능하다.
            _plot = _so.Kind == FarmActionKind.Plant
                ? agent.Farm.NearestEmptyOf(agent.AgentId, agent.TileX, agent.TileY)
                : agent.Farm.NearestRipeOf(agent.AgentId, agent.TileX, agent.TileY);

            if (_plot == null)
            {
                FailReason = _so.Kind == FarmActionKind.Plant
                    ? "내 빈 밭 없음 (전부 점유/재배 중)"
                    : "내 익은 밭 없음 (전부 점유/미성숙)";
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
