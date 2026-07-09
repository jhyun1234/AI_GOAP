namespace AIVillage.AI
{
    /// <summary>
    /// 자원 부족 상태를 평가해 가장 시급한 채집 Goal ID를 반환하는 순수 정적 유틸리티.
    ///
    /// VillagerFSM.SelectGatherGoalId()에서 추출. MonoBehaviour 의존성이 없어 EditMode 테스트 가능.
    /// N4 GoalArbiter로 흡수할 때 이 클래스의 Consider 로직이 Utility 점수 계산의 원형이 된다.
    ///
    /// 임계값(T_*)은 GOAPPlanningSlots.BuildGoalState의 수치형 Goal 목표치와 반드시 일치해야 한다.
    /// 목표치 < 임계값이면 alreadySatisfied 루프가 발생한다.
    /// </summary>
    public static class GatherGoalSelector
    {
        public const float T_COMMON = 30f; // Wood/Stone: GatherWood/Stone 목표치 30과 일치
        public const float T_RARE   = 15f; // Iron/Copper: GatherIron/Copper 목표치 15와 일치
        public const float T_FOOD   = 15f; // RawFood: GatherFood 목표치 15와 일치

        /// <summary>
        /// [방향 ③ F1] 발견되지 않은 자원 타입은 후보에서 제외한다.
        /// 미발견 자원의 컨텍스트 배율(FULL_NODE_PENALTY×2=10)이 액션 비용을 폭발시켜
        /// admissible 휴리스틱이 A*를 안내하지 못하고 MAX_NODES 4096을 소진하며
        /// NoSolutionFound가 발생한다. 근거: Docs/이슈_GatherIron_초반_무해.md P1-A 진단.
        /// 모든 자원이 임계값 이상이거나 전부 미발견이면 null.
        /// </summary>
        public static string Select(
            float woodStock,
            float stoneStock,
            float ironStock,
            float copperStock,
            float rawFoodStock,
            bool  hasTool,
            bool  woodDiscovered,
            bool  stoneDiscovered,
            bool  ironDiscovered,
            bool  copperDiscovered,
            bool  foodDiscovered)
        {
            string best       = null;
            float  worstRatio = 1f;

            void Consider(float stock, float threshold, string goalId)
            {
                if (stock >= threshold) return;
                float ratio = stock / threshold;
                if (ratio < worstRatio) { worstRatio = ratio; best = goalId; }
            }

            // [S3] 도구가 없으면 벌목·채굴 Goal은 수학적으로 해가 없다 (CraftTool은 Phase 3 과제).
            // [방향 ③ F1] 미발견 자원 타입은 상위 게이팅으로 후보 제외 (ADR-③-1, ADR-10).
            if (hasTool && woodDiscovered)   Consider(woodStock,   T_COMMON, "GatherWood");
            if (hasTool && stoneDiscovered)  Consider(stoneStock,  T_COMMON, "GatherStone");
            if (hasTool && ironDiscovered)   Consider(ironStock,   T_RARE,   "GatherIron");
            if (hasTool && copperDiscovered) Consider(copperStock, T_RARE,   "GatherCopper");
            if (foodDiscovered)              Consider(rawFoodStock, T_FOOD,  "GatherFood");
            return best;
        }
    }
}
