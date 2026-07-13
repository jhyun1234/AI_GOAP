using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 소비 계열 액션 (식사 등). 수치는 Effects로 표현
    /// (예: EatRawFood = RawFoodStock Sub 1 + MySatiety Add 15).
    /// EatAtAnchor면 지정 건물(기본 모닥불)로 가서 먹는다 — 식사를 '보이는 행위'로 (M1-C 리뷰).
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Action/Consume", fileName = "ConsumeAction")]
    public sealed class ConsumeActionSO : ActionSO
    {
        [Tooltip("true면 AnchorFlagSlot 건물 곁에서 먹는다 (미완공이면 기지 폴백). false면 제자리.")]
        public bool EatAtAnchor;

        [Tooltip("식사 장소 건물의 완공 플래그 슬롯 (기본 모닥불 — 마을의 부엌)")]
        public SlotId AnchorFlagSlot = SlotId.CampfireBuilt;

        public override IActionRunner CreateRunner(VillagerAgent agent) => new ConsumeRunner(this);
    }
}
