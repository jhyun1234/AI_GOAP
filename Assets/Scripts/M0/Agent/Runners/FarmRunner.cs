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
            DurationMult = agent.FarmDurationMultOfJob(); // M20 — 실행 시점 직업 기준 (중립 = 1)
            MoveTarget = _plot.Tile;
            return true;
        }

        public override RunnerResult Tick(VillagerAgent agent, float dt)
        {
            if (!DurationElapsed(dt, DurationMult)) return RunnerResult.Running;

            bool ok = _so.Kind == FarmActionKind.Plant
                ? agent.Farm.TryPlant(_plot)
                : agent.Farm.TryHarvest(_plot);
            return ok ? RunnerResult.Succeeded : Fail(DescribeMismatch(agent));
        }

        /// <summary>
        /// 실패 사유를 스스로 진단한다 (2026-08-06 — 舊 문구는 "점유 중 외부 전이 (버그 의심)"
        /// 한 줄이라 재현 안 되면 영영 못 밝힌다. 실제로 한 번 놓쳤다).
        ///
        /// 점유(TryClaim)는 1인이라 주민 경합은 불가능하고, 점유 중 상태를 바꿀 수 있는 문은
        /// 둘뿐이다 — BlightCrop(재해·작물 소실)과 RemovePlot(위협·밭 시설 파괴). **둘 다 설계된
        /// 경로이므로 "버그 의심"은 대개 오진이다**: 수확하러 간 사이 홍수가 작물을 쓸어간 것은
        /// 이야기이지 결함이 아니다. 그래서 문구에서 그 딱지를 떼고, 남은 상태로 원인을 가른다.
        /// </summary>
        private string DescribeMismatch(VillagerAgent agent)
        {
            bool registered = false;
            foreach (FarmPlot p in agent.Farm.Plots)
                if (p == _plot) { registered = true; break; }

            string want = _so.Kind == FarmActionKind.Plant ? "Empty" : "Ripe";
            if (!registered)
                return $"밭이 사라졌다 @({_plot.Tile.x},{_plot.Tile.y}) — 시설 파괴(위협·재해) 중 {want} 작업";
            return $"밭 상태가 바뀌었다 @({_plot.Tile.x},{_plot.Tile.y}) {want} 기대 → 실제 {_plot.State}" +
                   $" — 작물 소실(재해)이면 정상, 아니면 전이 문 점검 (BlightCrop/RemovePlot 외 경로 없음)";
        }

        public override void Cleanup(VillagerAgent agent)
        {
            _plot?.Release();
            _plot = null;
        }
    }
}
