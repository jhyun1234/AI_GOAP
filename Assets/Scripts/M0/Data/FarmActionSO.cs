using UnityEngine;

namespace AIVillage.M0
{
    public enum FarmActionKind { Plant = 0, Harvest = 1 }

    /// <summary>
    /// 농사 계열 액션 (M2-C). 심기/수확은 같은 계열의 Kind 분기다.
    /// 성장 대기가 끼므로 둘은 절대 한 플랜으로 묶이지 않는다 — 심기 goal과 수확 goal이
    /// 별개이고, 사이는 FarmService의 게임일 성장 틱이 잇는다 (ADR-M2-1).
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Action/Farm", fileName = "FarmAction")]
    public sealed class FarmActionSO : ActionSO
    {
        [Tooltip("Plant=빈 밭에 심기, Harvest=익은 작물 수확")]
        public FarmActionKind Kind;

        public override IActionRunner CreateRunner(VillagerAgent agent) => new FarmRunner(this);
    }
}
