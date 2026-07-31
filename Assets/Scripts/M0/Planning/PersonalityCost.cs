namespace AIVillage.M0
{
    /// <summary>
    /// 성격 → 카탈로그 인덱스별 비용 배율 (M4-B, 순수 — 게이트 대상).
    /// 계열 판정은 ActionSO 서브타입 is-검사만 (ADR-M4-4 — 이름/문자열 분기 금지).
    /// 소비·휴식·배회는 항상 중립 1 — **몸값 불가침**(ADR-M12-4 ①, 舊 ADR-M4-3 개정):
    /// 성향은 '무엇을 할지'를 바꾸고 '몸이 무엇을 할 수 있는지'는 못 바꾼다. 영구 고정이다.
    /// </summary>
    public static class PersonalityCost
    {
        // 개체 편차 배열 인덱스 규약 — VillagerAgent 스폰 편차(_multJitter)와 공유
        public const int JITTER_GATHER  = 0;
        public const int JITTER_FARM    = 1;
        public const int JITTER_BUILD   = 2;
        public const int JITTER_EXPLORE = 3;

        /// <summary>성격+편차 → 배율 배열 (M4 호환 진입점 — 직업 없음, 벡터 = 성격 원본).</summary>
        public static float[] Build(ActionCatalog catalog, PersonalitySO p, float[] jitter)
            => Build(catalog, p, p != null ? p.Traits : null, null, jitter);

        /// <summary>
        /// 성격×직업×편차 → 배율 배열 (ADR-M5-3 — 배율 결합 지점은 여기가 유일).
        /// 성격·직업 둘 다 null이면 null 반환 = 완전 중립 (ADR-M4-2 불변식 경로).
        /// rules(M12-C) = 성향 벡터의 계열 가중치표. null이면 벡터 유도 없음 = 기존 동작.
        /// ⚠️ 성격의 舊 개별 필드(GatherCostMult 등)와 성향 벡터는 **M12-F까지 병존**하며 곱해진다 —
        /// 이식 커밋에서 개별 필드를 1로 비우면 벡터만 남는다.
        /// </summary>
        /// <summary>호환 진입점 (게이트·구형 호출 전용 — 벡터 = 성격 원본, 편차 없음).
        /// 런타임(VillagerAgent 스폰)은 traits 인자판(MyTraits)을 쓴다 (⚠️W3-⑤).</summary>
        public static float[] Build(ActionCatalog catalog, PersonalitySO p, JobSO job, float[] jitter,
                                    TraitRulesSO rules = null)
            => Build(catalog, p, p != null ? p.Traits : null, job, jitter, rules);

        public static float[] Build(ActionCatalog catalog, PersonalitySO p, TraitValue[] traits,
                                    JobSO job, float[] jitter, TraitRulesSO rules = null)
        {
            if ((p == null && job == null) || catalog == null || catalog.Actions == null) return null;

            var mult = new float[catalog.Actions.Length];
            for (int i = 0; i < mult.Length; i++)
                mult[i] = MultiplierFor(catalog.Actions[i], p, traits, job, jitter, rules);
            return mult;
        }

        private static float MultiplierFor(ActionSO action, PersonalitySO p, TraitValue[] traits,
                                           JobSO job, float[] jitter, TraitRulesSO rules)
        {
            switch (action)
            {
                case GatherActionSO _:  return Mul(p != null ? p.GatherCostMult  : 1f, job != null ? job.GatherCostMult  : 1f, jitter, JITTER_GATHER,  Trait(p, traits, rules, rules != null ? rules.GatherWeights  : null));
                case FarmActionSO _:    return Mul(p != null ? p.FarmCostMult    : 1f, job != null ? job.FarmCostMult    : 1f, jitter, JITTER_FARM,    Trait(p, traits, rules, rules != null ? rules.FarmWeights    : null));
                case BuildActionSO _:   return Mul(p != null ? p.BuildCostMult   : 1f, job != null ? job.BuildCostMult   : 1f, jitter, JITTER_BUILD,   Trait(p, traits, rules, rules != null ? rules.BuildWeights   : null));
                case ExploreActionSO _: return Mul(p != null ? p.ExploreCostMult : 1f, job != null ? job.ExploreCostMult : 1f, jitter, JITTER_EXPLORE, Trait(p, traits, rules, rules != null ? rules.ExploreWeights : null));
                default:                return 1f; // Consume/Rest/Wander — 몸값 불가침 (ADR-M12-4 ①·ADR-M5-3)
            }
        }

        /// <summary>성향 벡터의 계열 배율 (M12-C). 규칙표·성격 어느 쪽이 없으면 1 = 중립.
        /// 벡터는 인자(M14-W3 개체 편차 포함) — SO 직접 읽기는 반쪽 개체 (⚠️W3-⑤).</summary>
        private static float Trait(PersonalitySO p, TraitValue[] traits, TraitRulesSO rules, TraitWeight[] weights)
            => rules == null || p == null ? 1f : rules.CostMult(traits, weights);

        private static float Mul(float personalityMult, float jobMult, float[] jitter, int idx, float traitMult)
            => personalityMult * jobMult * traitMult * J(jitter, idx);

        private static float J(float[] jitter, int idx)
            => jitter != null && jitter.Length > idx ? jitter[idx] : 1f;
    }
}
