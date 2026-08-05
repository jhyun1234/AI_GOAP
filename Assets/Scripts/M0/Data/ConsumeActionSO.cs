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
        [Tooltip("true면 AnchorPriority에서 지어진 첫 건물 곁에서 먹는다 (전부 미완공이면 기지 폴백). false면 제자리.")]
        public bool EatAtAnchor;

        [Tooltip("식사 장소 건물 우선순위 (ADR-M2-2) — 완공 플래그 슬롯 목록, 앞이 우선. " +
                 "후반 전용 건물(부엌 등) 도입 시 맨 앞에 추가만 하면 이주 완료 — 코드 0줄.")]
        public SlotId[] AnchorPriority = { SlotId.CampfireCount };

        [Tooltip("식사 위치 산개 반경(타일). 정확히 같은 타일을 노리면 타일 예약 충돌 + 불 위에 서게 된다.")]
        public int AnchorRadius = 1;

        [Tooltip("조리 노동인가 (M20 — ADR-M20-3). true면 이 액션의 실행 시간에 직업의 " +
                 "CookDurationMult가 곱해진다.\n" +
                 "⚠️ 식사·휴식 액션은 절대 켜지 않는다 — 직업이 몸값(먹는 속도)을 바꾸면 " +
                 "ADR-M5-3 위반이다. 켜도 되는 곳은 CookMeal·CookMealScarce 2곳뿐이며 " +
                 "게이트 M20-T3이 그 밖을 red로 잡는다.")]
        public bool IsCookingWork;

        public override IActionRunner CreateRunner(VillagerAgent agent) => new ConsumeRunner(this);
    }
}
