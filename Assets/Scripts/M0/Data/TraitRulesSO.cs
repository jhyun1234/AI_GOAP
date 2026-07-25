using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 성향 → 행동 유도의 전역 규칙표 (M12-C, 에셋 1개). 4작용형식 중 **②비용·④대상**처럼
    /// 소비처가 goal·액션 단위가 아니라 "계열 하나"인 것들이 여기 산다 (ADR-M12-1).
    ///
    /// ①우선순위는 GoalSO.TraitWeights(goal마다 다름), ③문턱은 소비처 SO의 TraitBias가 갖는다 —
    /// 그래야 새 goal·새 소비처가 늘어도 이 에셋을 안 고친다 (O(성격+goal) 유지).
    ///
    /// 미배선(null)이거나 가중치가 비면 전 계열 배율 1 = 완전 중립 (ADR-M4-2 계승).
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/TraitRules", fileName = "TraitRules")]
    public sealed class TraitRulesSO : ScriptableObject
    {
        [Header("② 비용 — 노동 4계열의 성향 가중치")]
        [Tooltip("배율 = clamp(1 - bias × CostScale, 0.5, 1.5). bias가 +면 싸지고(선호) -면 비싸진다.\n" +
                 "⚠️ 소비·휴식·배회 계열은 여기 없다 — 몸값 불가침 (ADR-M12-4 ①). 필드를 추가하지 말 것.")]
        public TraitWeight[] GatherWeights;
        public TraitWeight[] FarmWeights;
        public TraitWeight[] BuildWeights;
        public TraitWeight[] ExploreWeights;

        [Tooltip("편향(-1~+1)을 배율로 옮기는 폭. 제안치 0.4 = 현행 배율 폭(0.6~1.2)에서 역산.\n" +
                 "0.5 미만으로 두면 |bias| ≤ 1이라 clamp(0.5, 1.5)에 절대 닿지 않는다 — " +
                 "클램프는 안전망이지 일상 경로가 아니다 (게이트 M12-T5가 이 불변식을 지킨다).")]
        [Range(0f, 0.5f)]
        public float CostScale = 0.4f;

        [Header("④ 대상 — 택지 선호 거리 (M12-E에서 사용, 지금은 미사용)")]
        [Tooltip("마을 앵커로부터의 선호 거리 **비율**(0~1, M11-K 규약)에 더해지는 편향. " +
                 "모험↑ 외딴집 / 사교↑ 이웃 곁.")]
        public TraitBias HomeDistanceBias;

        /// <summary>
        /// ②비용 유도 (순수 — 게이트 M12-T5). 규칙표·성격 어느 쪽이 없어도 1 = 중립.
        /// 클램프는 안전망 — CostScale ≤ 0.5면 |bias| ≤ 1이라 실제로는 닿지 않는다.
        /// </summary>
        public float CostMult(TraitValue[] traits, TraitWeight[] weights)
            => weights == null || weights.Length == 0
                ? 1f
                : Mathf.Clamp(1f - TraitVector.Bias(traits, weights) * CostScale, 0.5f, 1.5f);
    }
}
