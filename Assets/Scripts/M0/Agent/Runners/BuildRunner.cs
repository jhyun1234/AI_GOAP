namespace AIVillage.M0
{
    /// <summary>
    /// 건설 러너: 제자리 실행 (舊 BC4 — Build 액션 이동 스킵 계승).
    /// 세계 반영은 ConstructionService.Complete()가 전담하므로 AppliesOwnEffects = true —
    /// 에이전트의 EffectApplier 일괄 적용을 건너뛴다 (이중 차감 방지, ADR-M0-3).
    /// </summary>
    public sealed class BuildRunner : ActionRunnerBase
    {
        private readonly BuildActionSO _so;

        public override bool AppliesOwnEffects => true;

        public BuildRunner(BuildActionSO so) : base(so)
        {
            _so = so;
        }

        public override bool Prepare(VillagerAgent agent)
        {
            if (_so.Building == null)
            {
                FailReason = $"{_so.name}: BuildingSO 미연결";
                return false;
            }
            return true; // MoveTarget null = 제자리
        }

        public override RunnerResult Tick(VillagerAgent agent, float dt)
        {
            if (!DurationElapsed(dt)) return RunnerResult.Running;

            return agent.Construction.Complete(_so.Building, agent.TileX, agent.TileY)
                ? RunnerResult.Succeeded
                : Fail($"{_so.Building.DisplayName} 완공 실패 (비용 부족 또는 중복)");
        }
    }
}
