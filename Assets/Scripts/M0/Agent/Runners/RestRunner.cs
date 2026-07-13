namespace AIVillage.M0
{
    /// <summary>휴식 러너: 제자리 실행. 피로 감소는 EffectApplier가 수행한다.</summary>
    public sealed class RestRunner : ActionRunnerBase
    {
        public RestRunner(RestActionSO so) : base(so) { }

        public override RunnerResult Tick(VillagerAgent agent, float dt)
            => DurationElapsed(dt) ? RunnerResult.Succeeded : RunnerResult.Running;
    }
}
