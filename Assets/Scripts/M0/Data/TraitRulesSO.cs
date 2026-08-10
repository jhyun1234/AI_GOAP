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

        [Header("④ 대상 — 직업 배정 편향 (M12-H)")]
        [Tooltip("편향(-1~+1)이 추첨 가중치를 얼마나 기울이는가. 가중치 = max(하한, 1 + bias × 이 값).\n" +
                 "0이면 전 직업 균등 = 현행 독립 랜덤 (중립 불변식). 제안치 1.")]
        [Range(0f, 2f)]
        public float JobBiasStrength = 1f;

        [Tooltip("근면이 이 값 미만이면 '무직'이 추첨 후보로 올라온다 (M12-H). 제안치 -50 — " +
                 "게으름뱅이(근면 -80)만 걸리고 고집쟁이·떠돌이는 안 걸리는 자리.")]
        [Range(-100, 100)]
        public int NoJobBelowDiligence = -50;

        [Tooltip("위 문턱에 걸렸을 때 '무직' 후보의 가중치. **0이면 무직 후보 없음 = 현행 동작**이라 " +
                 "기본값은 0이다 (미배선 중립 — ADR-M4-2). 에셋에서 켠다. 제안치 3 ≈ 직업 하나의 3배.\n" +
                 "⚠️ 확률이지 규칙이 아니다 — 게으름뱅이도 직업을 가질 수 있다.")]
        [Min(0f)]
        public float NoJobWeight;

        [Header("개체 편차 (M14-W3 — 같은 성격, 다른 사람)")]
        [Tooltip("스폰 1회, 전 축값(±100 스케일)에 ± 이 값 안에서 결정적으로 더한다 (FNV-1a — " +
                 "ADR-M14-1: 정체성이지 주사위가 아니다). 0 = 편차 없음(현행 동일).\n" +
                 "25 근거 = 검산(2026-07-31): 게으름뱅이 표본 100명 중 13명이 파종 문턱(Plant>Leisure)을 " +
                 "넘는다 — 10이면 0명 = 확정사 유지. 게이트 M14-T2-c가 이 에셋 값을 직접 검증하므로 " +
                 "20 미만으로 내리면 게이트가 알려준다.")]
        [Range(0, 30)]
        public int TraitJitterAmp = 25;

        [Header("축별 혼잣말 풀 (M12-F — 새 성격이 대사 0줄로도 말이 통하게)")]
        [Tooltip("성격 전용 MoodLines가 있으면 그것이 우선하고, 없을 때 여기서 뽑는다. " +
                 "같은 축을 공유하는 성격끼리 말투가 닮아 '계열'이 느껴지는 부수효과가 있다.")]
        public TraitMoodPool[] MoodPools;

        /// <summary>
        /// 이 벡터를 가장 잘 대표하는 축의 혼잣말 풀 (순수 — 게이트 M12-T8).
        /// 선택 규칙은 **결정적**이다: |값|이 가장 큰 축 → 동률이면 TraitId 순 → 그 축의 부호와
        /// 맞는 풀. 전 축 0이거나 맞는 풀이 없으면 null (호출자가 기본 대사로 폴백 — 침묵 금지).
        /// </summary>
        public string[] MoodPoolFor(TraitValue[] traits)
        {
            if (MoodPools == null || MoodPools.Length == 0 || traits == null) return null;

            // |값| 최대 축 (동률이면 TraitId 작은 쪽 — 열거 순서가 아니라 값으로 판정해야 결정적)
            bool found = false;
            TraitId best = default;
            int bestAbs = 0, bestVal = 0;
            for (int i = 0; i < traits.Length; i++)
            {
                int abs = Mathf.Abs(traits[i].Value);
                if (abs == 0) continue;
                if (!found || abs > bestAbs || (abs == bestAbs && traits[i].Trait < best))
                {
                    found = true; best = traits[i].Trait; bestAbs = abs; bestVal = traits[i].Value;
                }
            }
            if (!found) return null;

            for (int i = 0; i < MoodPools.Length; i++)
                if (MoodPools[i].Trait == best && MoodPools[i].ForHighValue == (bestVal > 0)
                    && MoodPools[i].Lines != null && MoodPools[i].Lines.Length > 0)
                    return MoodPools[i].Lines;
            return null;
        }

        [Header("성격 서술 (M25-1차 — 축마다 적는다. 조합표는 만들지 않는다, ADR-M25-1)")]
        [Tooltip("6축 × 방향 2 × 강도 2 = 최대 24칸. 비면 서술 없음 = 배선 전과 동일 (중립 불변식).\n" +
                 "🔑 위 MoodPools(M12-F)와 같은 규약이다 — 새 성격이 문장 0줄로도 말이 통하게.")]
        public TraitLine[] TraitLines;

        [Tooltip("단정 문턱 — |축값| 이 이상이면 단정형, 미만이면 완곡형. 제안치 60 " +
                 "(배포 6종 실측 역산 — 이 값에서 6종이 각각 1~3개의 단정 문장을 얻는다).")]
        [Range(0, 100)]
        public int ProseStrongAt = 60;

        [Tooltip("침묵 문턱 — |축값| 이 미만이면 그 축은 **아무 말도 하지 않는다** (ADR-M25-3).\n" +
                 "제안치 35: 20으로 내리면 떠돌이가 자존 20·사교 −20까지 말해 약한 문장이 강한 " +
                 "문장을 묻고, 60으로 올리면 새침이·고집쟁이가 문장을 잃는다.")]
        [Range(0, 100)]
        public int ProseSilentBelow = 35;

        [Tooltip("한 사람이 말하는 축 문장의 최대 수. 제안치 3 — 여섯 축을 다 읊으면 사람이 안 보인다.")]
        [Range(1, 6)]
        public int ProseMaxAxisLines = 3;

        [Tooltip("goal 문장 문턱 — 성향 편향(-1~+1)이 이 이상인 goal만 후보. 제안치 0.30:\n" +
                 "새침이의 최고 편향이 0.32(Goal_BuildMyHouse)라 이보다 높이면 새침이가 goal 문장을 " +
                 "한 개도 못 얻는다 (실측 하한).")]
        [Range(0f, 1f)]
        public float ProseGoalBiasAt = 0.30f;

        /// <summary>이 축값에 맞는 서술 한 칸 (순수 — 게이트 M25-T5). 없으면 null = 침묵.
        /// `MoodPoolFor`와 같은 판정 규약(부호·강도로 고른다)이라 새 문법을 만들지 않는다.</summary>
        public TraitLine? LineFor(TraitId trait, int value)
        {
            if (TraitLines == null) return null;
            int abs = Mathf.Abs(value);
            if (abs < ProseSilentBelow) return null;   // 중립은 침묵 (ADR-M25-3)
            bool positive = value > 0, strong = abs >= ProseStrongAt;
            for (int i = 0; i < TraitLines.Length; i++)
                if (TraitLines[i].Trait == trait && TraitLines[i].Positive == positive
                    && TraitLines[i].Strong == strong
                    && !string.IsNullOrEmpty(TraitLines[i].Ending))
                    return TraitLines[i];
            return null;                                // 빈칸 = 침묵 (빈 문자열이 아니다)
        }

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
