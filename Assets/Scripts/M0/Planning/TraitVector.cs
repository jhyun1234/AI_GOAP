using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 성향 → 행동값 유도의 단일 지점 (M12-A, 순수 — 게이트 M12-T1).
    ///
    /// 4작용형식(ADR-M12-1)의 유도식은 **형식마다 하나**이고 전부 여기서 나온다:
    ///   편향 bias = clamp(Σ(v/100 × w), -1, +1)
    ///   ① 우선순위 = round(bias × PriorityScale)   ② 비용 = clamp(1 - bias × CostScale, …)
    ///   ③ 문턱     = base + bias × Sensitivity     ④ 대상 = 후보 점수화(①과 같은 식)
    ///
    /// ⚠️ 소비처가 PersonalitySO.Traits를 직접 순회하면 반려 — 두 번째 유도 경로다
    /// (ADR-M12-5 · ADR-M0-3 상태 쓰기 단일 지점의 정신).
    /// </summary>
    public static class TraitVector
    {
        /// <summary>축 값의 정규화 분모 — TraitValue의 Range와 짝 (알고리즘 상수, 밸런스 수치 아님).</summary>
        private const float AXIS_MAX = 100f;

        /// <summary>
        /// 벡터에서 축 값을 읽는다. **미등록 = 0 = 중립** (ADR-M12-3의 물리적 근거 —
        /// 새 축을 추가해도 기존 에셋이 그 축에서 0이라 행동이 불변이다).
        /// 축이 6개 안팎이라 선형 탐색으로 충분하다 (스폰 시 1회·문턱 판정 시 소수 호출).
        /// </summary>
        public static int ValueOf(TraitValue[] traits, TraitId id)
        {
            if (traits == null) return 0;
            for (int i = 0; i < traits.Length; i++)
                if (traits[i].Trait == id) return traits[i].Value;
            return 0;
        }

        /// <summary>
        /// 정규화 편향 (-1 ~ +1). 벡터·가중치 어느 쪽이 비어도 0 = 완전 중립.
        /// 정규화라야 통로마다 단위가 달라도 유도식 하나로 통일된다 — 단위 환산은 소비처의 몫.
        /// </summary>
        public static float Bias(TraitValue[] traits, TraitWeight[] weights)
        {
            if (traits == null || weights == null) return 0f;

            float sum = 0f;
            for (int i = 0; i < weights.Length; i++)
                sum += ValueOf(traits, weights[i].Trait) / AXIS_MAX * weights[i].Weight;

            return Mathf.Clamp(sum, -1f, 1f);
        }

        /// <summary>
        /// ③문턱 환산 — base + bias × Sensitivity. 가중치가 비면 base를 그대로 돌려준다
        /// (중립 불변식: 소비처가 이 함수를 통과시켜도 현행 판정과 완전히 같다).
        /// </summary>
        public static float Threshold(TraitValue[] traits, in TraitBias bias, float baseValue)
            => baseValue + Bias(traits, bias.Weights) * bias.Sensitivity;

        /// <summary>
        /// 성향 조건 판정 (M12-G의 부탁 성립 등). 조건이 비면 항상 성립 = 현행 동작.
        /// 결정적이다 — 랜덤 금지 (ADR-M1-2: 플레이어가 학습 가능해야 협상이 성립한다).
        /// </summary>
        public static bool Meets(TraitValue[] traits, TraitCondition[] conditions)
        {
            if (conditions == null || conditions.Length == 0) return true;
            for (int i = 0; i < conditions.Length; i++)
                if (ValueOf(traits, conditions[i].Trait) < conditions[i].MinValue) return false;
            return true;
        }
    }
}
