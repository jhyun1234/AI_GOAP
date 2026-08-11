using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 제작 계열 액션 (M21-W5) — 재료(전제·Sub 효과)는 이 에셋이 정의하고, 산출물(무기 소유)은
    /// 러너 완료 시 에이전트의 문(AcquireWeapon)으로 쓴다. Effects의 `Set MyHasWeapon 1`은
    /// **플래너 픽션**이다 — EffectApplier는 논리형 슬롯을 무시하므로 실행 세계의 원천은
    /// 에이전트뿐이다 (BuildCampfire의 MyHasCampfire 픽션과 같은 규약).
    ///
    /// ⚠️ ConsumeRunner를 재사용하지 않는 이유 (명세 W5 ⚠️③): 식사 러너에 제작을 얹으면
    /// 몸값 불가침(ADR-M5-3) 감시 경계가 흐려진다 — 소형 CraftRunner 신설이 결정 사항.
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Action/Craft", fileName = "CraftAction")]
    public sealed class CraftActionSO : ActionSO
    {
        public override ActionRunnerBase CreateRunner(VillagerAgent agent) => new CraftRunner(this);
    }
}
