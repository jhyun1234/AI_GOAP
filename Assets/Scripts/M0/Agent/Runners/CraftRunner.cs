using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 제작 러너 (M21-W5, 소형) — 제자리에서 DurationSec 동안 만들고, 완료 순간 무기 소유를
    /// 에이전트의 문(AcquireWeapon)으로 쓴다. 재료 차감은 EffectApplier의 몫이다.
    ///
    /// 왜 지불 선검사가 여기 있나: 소유 쓰기는 Succeeded 직후 EffectApplier의 재료 차감보다
    /// **먼저** 일어난다. 계획~실행 사이에 남이 재료를 써 버렸으면 차감이 실패해 플랜이
    /// 중단되는데, 그때 이미 소유를 줬으면 공짜 무기가 된다 (부분 성공 — ADR-M0-8 위반).
    /// 같은 프레임 안에서는 아무도 끼어들지 않으므로(단일 스레드 순차 실행) 여기서 성립한
    /// 선검사는 EffectApplier에서도 반드시 성립한다 — 판정은 같은 스톡(IsStock·Sub)을 읽고
    /// 수치는 에셋 Effects에서만 온다 (발명 없음).
    /// </summary>
    public sealed class CraftRunner : ActionRunnerBase
    {
        private readonly CraftActionSO _so;

        public CraftRunner(CraftActionSO so) : base(so)
        {
            _so = so;
        }

        public override RunnerResult Tick(VillagerAgent agent, float dt)
        {
            if (!DurationElapsed(dt)) return RunnerResult.Running;

            // 전역 스톡 Sub 효과의 지불 가능성 — EffectApplier 1단계 선검사와 같은 판정
            if (_so.Effects != null)
                foreach (SlotEffect e in _so.Effects)
                    if (SlotIds.IsStock(e.Slot) && e.Op == EffectOp.SubClamp0
                        && agent.World.GetStock(e.Slot) < e.Value)
                        return Fail("재료 부족 — 만들다 말았다 (재계획)");

            agent.AcquireWeapon(); // 소유의 유일한 문 (MyWasAttacked 패턴 — 영구·세이브 대상)
            Debug.Log($"[Craft] {agent.AgentId}: 원시 무기를 만들었다 — 타격이 빨라진다 (MyHasWeapon=1)");
            return RunnerResult.Succeeded;
        }
    }
}
