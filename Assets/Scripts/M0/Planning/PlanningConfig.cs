using AIVillage.Core.GOAP;

namespace AIVillage.M0
{
    /// <summary>
    /// M0 플래닝 상수. 잡 무변경 재사용(ADR-M0-5)을 위한 브릿지.
    ///
    /// GOAPPlannerJob은 슬롯 배열을 GOAPPlanningSlots.TOTAL_SLOTS 길이로 해석하므로
    /// M0도 그 크기로 배열을 할당하고, 앞쪽 SlotIds.Count개 인덱스만 사용한다
    /// (나머지 슬롯은 0 유지 — GoalMask 0이라 판정에서 무시됨).
    /// W8 폐기 시 TOTAL_SLOTS 값을 이 클래스로 이전하고 잡의 참조를 교체한다.
    /// </summary>
    public static class PlanningConfig
    {
        public static readonly int TotalSlots = AIVillage.Core.GOAP.GOAPPlanningSlots.TOTAL_SLOTS;

        public const int MaxPlanLen = GOAPPlannerJob.MAX_PLAN_LEN;
        public const int MaxNodes   = GOAPPlannerJob.MAX_NODES;
    }
}
