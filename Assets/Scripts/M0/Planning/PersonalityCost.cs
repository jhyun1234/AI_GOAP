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

        /// <summary>성격+편차 → 배율 배열 (M4 호환 진입점 — 직업 없음).</summary>
        public static float[] Build(ActionCatalog catalog, PersonalitySO p, float[] jitter)
            => Build(catalog, p, null, jitter);

        /// <summary>
        /// 성격×직업×편차 → 배율 배열 (ADR-M5-3 — 배율 결합 지점은 여기가 유일).
        /// 성격·직업 둘 다 null이면 null 반환 = 완전 중립 (ADR-M4-2 불변식 경로).
        /// </summary>
        public static float[] Build(ActionCatalog catalog, PersonalitySO p, JobSO job, float[] jitter)
        {
            if ((p == null && job == null) || catalog == null || catalog.Actions == null) return null;

            var mult = new float[catalog.Actions.Length];
            for (int i = 0; i < mult.Length; i++)
                mult[i] = MultiplierFor(catalog.Actions[i], p, job, jitter);
            return mult;
        }

        private static float MultiplierFor(ActionSO action, PersonalitySO p, JobSO job, float[] jitter)
        {
            switch (action)
            {
                case GatherActionSO _:  return Mul(p != null ? p.GatherCostMult  : 1f, job != null ? job.GatherCostMult  : 1f, jitter, JITTER_GATHER);
                case FarmActionSO _:    return Mul(p != null ? p.FarmCostMult    : 1f, job != null ? job.FarmCostMult    : 1f, jitter, JITTER_FARM);
                case BuildActionSO _:   return Mul(p != null ? p.BuildCostMult   : 1f, job != null ? job.BuildCostMult   : 1f, jitter, JITTER_BUILD);
                case ExploreActionSO _: return Mul(p != null ? p.ExploreCostMult : 1f, job != null ? job.ExploreCostMult : 1f, jitter, JITTER_EXPLORE);
                default:                return 1f; // Consume/Rest/Wander — 생존·여가 중립 (ADR-M4-3·ADR-M5-3)
            }
        }

        private static float Mul(float personalityMult, float jobMult, float[] jitter, int idx)
            => personalityMult * jobMult * J(jitter, idx);

        private static float J(float[] jitter, int idx)
            => jitter != null && jitter.Length > idx ? jitter[idx] : 1f;
    }
}
