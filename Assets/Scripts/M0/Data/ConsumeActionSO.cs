using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 소비 계열 액션 (식사 등). 제자리 실행 — 필요한 데이터는 Effects로 충분하다
    /// (예: EatRawFood = RawFoodStock Sub 1 + MySatiety Add 15).
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Action/Consume", fileName = "ConsumeAction")]
    public sealed class ConsumeActionSO : ActionSO
    {
    }
}
