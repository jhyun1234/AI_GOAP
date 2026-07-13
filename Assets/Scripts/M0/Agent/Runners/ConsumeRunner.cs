namespace AIVillage.M0
{
    /// <summary>소비 러너 (식사 등): 제자리 실행. 스톡 차감·욕구 회복은 EffectApplier가 수행한다.</summary>
    public sealed class ConsumeRunner : ActionRunnerBase
    {
        public ConsumeRunner(ConsumeActionSO so) : base(so) { }

        public override RunnerResult Tick(VillagerAgent agent, float dt)
            => DurationElapsed(dt) ? RunnerResult.Succeeded : RunnerResult.Running;
    }
}
