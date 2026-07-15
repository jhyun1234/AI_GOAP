namespace AIVillage.M0
{
    /// <summary>
    /// 성격 → 카탈로그 인덱스별 비용 배율 (M4-B, 순수 — 게이트 대상).
    /// 계열 판정은 ActionSO 서브타입 is-검사만 (ADR-M4-4 — 이름/문자열 분기 금지).
    /// 소비·휴식·배회는 항상 중립 1 — 굶주림 앞에 성격 없음 (ADR-M4-3).
    /// </summary>
    public static class PersonalityCost
    {
        // 개체 편차 배열 인덱스 규약 — VillagerAgent 스폰 편차(_multJitter)와 공유
        public const int JITTER_GATHER  = 0;
        public const int JITTER_FARM    = 1;
        public const int JITTER_BUILD   = 2;
        public const int JITTER_EXPLORE = 3;

        /// <summary>성격+편차 → 배율 배열. 성격 null이면 null 반환 = 완전 중립 (ADR-M4-2 불변식 경로).</summary>
        public static float[] Build(ActionCatalog catalog, PersonalitySO p, float[] jitter)
        {
            if (p == null || catalog == null || catalog.Actions == null) return null;

            var mult = new float[catalog.Actions.Length];
            for (int i = 0; i < mult.Length; i++)
                mult[i] = MultiplierFor(catalog.Actions[i], p, jitter);
            return mult;
        }

        private static float MultiplierFor(ActionSO action, PersonalitySO p, float[] jitter)
        {
            switch (action)
            {
                case GatherActionSO _:  return p.GatherCostMult  * J(jitter, JITTER_GATHER);
                case FarmActionSO _:    return p.FarmCostMult    * J(jitter, JITTER_FARM);
                case BuildActionSO _:   return p.BuildCostMult   * J(jitter, JITTER_BUILD);
                case ExploreActionSO _: return p.ExploreCostMult * J(jitter, JITTER_EXPLORE);
                default:                return 1f; // Consume/Rest/Wander — 생존·여가 중립 (ADR-M4-3)
            }
        }

        private static float J(float[] jitter, int idx)
            => jitter != null && jitter.Length > idx ? jitter[idx] : 1f;
    }
}
