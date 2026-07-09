/// <summary>
/// ConflictScoreCalculator.cs - 플레이어 명령과 주민 현재 상태의 충돌 점수 계산기
///
/// 역할(Role): CommandConflict 상태에서 VillagerFSM이 호출하는 순수 계산 유틸리티.
///             VillagerBrain의 생존 수치와 PlayerOrder의 속성을 조합하여
///             ConflictScore와 Threshold를 계산하고 거부 여부를 판정한다.
///             로직을 FSM 밖으로 분리하여 유닛 테스트 및 향후 조정이 용이하다.
/// 사용법(Usage): ConflictScoreCalculator.Calculate(brain, order) 정적 호출.
///               MonoBehaviour 없음. 씬에 붙이지 않는다.
/// 의존성(Dependencies): VillagerEnums.cs, VillagerBrain.cs
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-25
/// </summary>

using UnityEngine;

namespace AIVillage.AI
{
    /// <summary>
    /// ConflictScore 계산 및 거부 이유 판정 정적 유틸리티.
    /// 모든 메서드는 순수 함수(Pure function)에 가깝도록 설계한다 — 부수효과 없음.
    /// </summary>
    public static class ConflictScoreCalculator
    {
        // ══════════════════════════════════════════════════════════════════════
        // 기획서 수치 상수
        // ══════════════════════════════════════════════════════════════════════

        // 기획서 수치: ConflictScore 임계값 기준 배율
        private const float THRESHOLD_BASE = 2.5f;      // Threshold = THRESHOLD_BASE × (loyalty / LOYALTY_PIVOT)
        private const float LOYALTY_PIVOT  = 50f;       // 기준 충성도 (이 값일 때 Threshold = 2.5)

        // 긴급도(Urgency) 발동 임계값
        private const float HUNGER_URGENCY_THRESHOLD  = 20f;  // satietyLevel 이 값 미만 시 긴급도 발동 (낮을수록 배고픔)
        private const float HEALTH_URGENCY_THRESHOLD  = 20f;  // healthLevel 이 값 미만 시 긴급도 발동
        private const float FATIGUE_URGENCY_THRESHOLD = 90f;  // fatigueLevel 이 값 초과 시 긴급도 발동
        private const float SAFETY_URGENCY_FIXED      = 0.8f; // nearEnemy 시 고정 긴급도 값

        // 명령 충격(Impact) 가중치
        private const float IMPACT_AWAY_FROM_BASE    = 0.5f;  // 기지 이탈 필요 명령
        private const float IMPACT_LOW_HEALTH_ACTION = 0.7f;  // 저체력(< 40) 상태에서 체력 소모 명령
        private const float IMPACT_ATTACK_NO_WEAPON  = 1.0f;  // 무기 없이 전투 명령
        private const float IMPACT_GATHER_NO_TOOL    = 0.3f;  // 도구 없이 채집 명령

        // 저체력 판정 기준 (Impact 계산용)
        private const float LOW_HEALTH_THRESHOLD = 40f;

        // ══════════════════════════════════════════════════════════════════════
        // 공개 메서드
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 주민 현재 상태와 플레이어 명령을 기반으로 ConflictScoreData를 계산한다.
        ///
        /// 계산 공식:
        ///   NeedUrgency = 각 생존 수치 위반 시 선형 긴급도 (0~1 이상)
        ///   OrderImpact = 명령 속성별 충격 가중치 합산
        ///
        ///   [PR Fix]: F-001 — 설계서 공식 Σ(urgency_i × impact_i)를 구조적 사유로 근사 구현.
        ///   urgency(상태 기반)와 impact(명령 기반)는 1:1 매핑이 불가하므로
        ///   (HungerUrgency + HealthUrgency + FatigueUrgency + SafetyUrgency) × orderImpact
        ///   형태로 근사한다. orderImpact = 0이면 score = 0(충돌 없음)이 자동으로 보장된다.
        ///
        ///   Threshold = 2.5 × (loyaltyLevel / 50)
        ///   ShouldRefuse = ConflictScore >= Threshold
        /// </summary>
        /// <param name="brain">판정할 주민의 상태 데이터</param>
        /// <param name="order">평가할 플레이어 명령</param>
        /// <returns>계산 결과 및 중간값을 담은 ConflictScoreData</returns>
        public static ConflictScoreData Calculate(VillagerBrain brain, PlayerOrder order)
        {
            if (brain == null)
            {
                Debug.LogError("[ConflictScoreCalculator] Calculate: brain이 null입니다. 기본 거부 결과를 반환합니다.");
                return new ConflictScoreData { ShouldRefuse = true };
            }

            var result = new ConflictScoreData();

            // ── 1단계: 긴급도(Urgency) 계산 ──────────────────────────────────
            // 각 생존 수치가 임계값을 벗어난 정도를 0~1(이상) 범위로 선형 변환한다.
            // 범위 이탈이 없으면 0 (긴급도 없음).

            // 배고픔 긴급도: satietyLevel이 20 미만으로 떨어진 비율 (최대 1.0f, 0일 때)
            result.HungerUrgency = brain.SatietyLevel < HUNGER_URGENCY_THRESHOLD
                ? (HUNGER_URGENCY_THRESHOLD - brain.SatietyLevel) / HUNGER_URGENCY_THRESHOLD
                : 0f;

            // 부상 긴급도: healthLevel이 20 미만으로 떨어진 비율 (최대 1.0f, 0일 때)
            result.HealthUrgency = brain.HealthLevel < HEALTH_URGENCY_THRESHOLD
                ? (HEALTH_URGENCY_THRESHOLD - brain.HealthLevel) / HEALTH_URGENCY_THRESHOLD
                : 0f;

            // 피로 긴급도: fatigueLevel이 90을 초과한 비율 (최대 1.0f, 100일 때)
            result.FatigueUrgency = brain.FatigueLevel > FATIGUE_URGENCY_THRESHOLD
                ? (brain.FatigueLevel - FATIGUE_URGENCY_THRESHOLD) / (100f - FATIGUE_URGENCY_THRESHOLD)
                : 0f;

            // 안전 긴급도: 적 근처이면 고정값 0.8f, 없으면 0
            result.SafetyUrgency = brain.NearEnemy ? SAFETY_URGENCY_FIXED : 0f;

            // ── 2단계: 명령 충격(Impact) 계산 ────────────────────────────────
            // 명령이 현재 상태에서 수행하기 어려운 정도를 가중치 합산으로 계산한다.

            result.OrderImpact = 0f;

            // 기지를 벗어나야 하는 명령인데 이미 기지에 없는 경우
            // → 복귀 후 다시 이동해야 하므로 추가 부담
            if (RequiresBase(order.OrderType) && !brain.AtBase)
            {
                result.OrderImpact += IMPACT_AWAY_FROM_BASE;
            }

            // 체력 소모 명령인데 이미 저체력(< 40) 상태인 경우
            if (IsPhysicallyDemanding(order.OrderType) && brain.HealthLevel < LOW_HEALTH_THRESHOLD)
            {
                result.OrderImpact += IMPACT_LOW_HEALTH_ACTION;
            }

            // 전투 명령인데 무기가 없는 경우 (원시 무기도 없음)
            if (order.OrderType == OrderType.Attack
                && !brain.HasWeapon
                && !brain.HasPrimitiveWeapon)
            {
                result.OrderImpact += IMPACT_ATTACK_NO_WEAPON;
            }

            // 채집 명령인데 도구가 없는 경우
            if (IsGatherOrder(order.OrderType) && !brain.HasTool)
            {
                result.OrderImpact += IMPACT_GATHER_NO_TOOL;
            }

            // ── 3단계: ConflictScore 최종 계산 ───────────────────────────────
            // [PR Fix]: F-001 — 설계서 Σ(urgency_i × impact_i) 공식을
            //   totalUrgency × orderImpact 로 근사 구현.
            //   urgency(상태 기반 4종)와 impact(명령 기반 합산)는 1:1 매핑 구조가 아니기 때문에
            //   각 urgency_i에 동일한 orderImpact를 곱한 뒤 합산하면
            //   결국 (ΣuRgency_i) × orderImpact 와 동일하다 — 수식적으로 동치.
            //   orderImpact = 0 이면 score = 0 → '충돌 없음'이 자동 보장된다.
            float totalUrgency = result.HungerUrgency
                               + result.HealthUrgency
                               + result.FatigueUrgency
                               + result.SafetyUrgency;

            // 긴급도가 0이면 OrderImpact와 곱해도 0 → 명령 충격만으로는 거부 안 됨
            // orderImpact가 0이면 아무리 긴급도가 높아도 ConflictScore = 0 → 충돌 없음
            result.ConflictScore = totalUrgency * result.OrderImpact;

            // ── 4단계: Threshold 및 거부 판정 ────────────────────────────────
            // 충성도가 낮을수록 Threshold도 낮아진다 → 더 쉽게 거부한다
            // loyaltyLevel = 50 → Threshold = 2.5 (기준)
            // loyaltyLevel = 25 → Threshold = 1.25 (절반 = 더 쉽게 거부)
            // loyaltyLevel = 100 → Threshold = 5.0 (두 배 = 잘 거부 안 함)
            result.Threshold = THRESHOLD_BASE * (Mathf.Max(0f, brain.LoyaltyLevel) / LOYALTY_PIVOT);

            // Threshold가 0이 되면 항상 거부 → 최소값 보호
            if (result.Threshold < 0.01f)
            {
                result.Threshold = 0.01f;
            }

            result.ShouldRefuse = result.ConflictScore >= result.Threshold;

            return result;
        }

        /// <summary>
        /// 현재 생존 수치를 기반으로 가장 적절한 거부 이유 코드를 반환한다.
        /// 우선순위: 부상 > 배고픔 > 피로 > 충성도 > 위험 > 도구 없음 순.
        /// </summary>
        /// <param name="brain">판정할 주민의 상태 데이터</param>
        /// <returns>거부 이유 코드. 해당 없으면 REFUSE_LOYALTY 반환(폴백).</returns>
        public static RefusalReasonCode DetermineReason(VillagerBrain brain)
        {
            if (brain == null)
            {
                Debug.LogError("[ConflictScoreCalculator] DetermineReason: brain이 null입니다. REFUSE_LOYALTY를 반환합니다.");
                return RefusalReasonCode.REFUSE_LOYALTY;
            }

            // 우선순위 1: 부상 (즉각적 생존 위협)
            if (brain.HealthLevel < HEALTH_URGENCY_THRESHOLD)
                return RefusalReasonCode.REFUSE_INJURY;

            // 우선순위 2: 배고픔 (행동력 저하)
            if (brain.SatietyLevel < HUNGER_URGENCY_THRESHOLD)
                return RefusalReasonCode.REFUSE_HUNGER;

            // 우선순위 3: 피로 (탈진)
            if (brain.FatigueLevel > FATIGUE_URGENCY_THRESHOLD)
                return RefusalReasonCode.REFUSE_FATIGUE;

            // 우선순위 4: 낮은 충성도 (심리적 거부)
            // 기획서 수치: 30 미만 = 반란 가능성 → 명령 거부
            if (brain.LoyaltyLevel < 30f)
                return RefusalReasonCode.REFUSE_LOYALTY;

            // 우선순위 5: 적 근처 + 무기 없음 (위험한 상황)
            if (brain.NearEnemy && !brain.HasWeapon && !brain.HasPrimitiveWeapon)
                return RefusalReasonCode.REFUSE_DANGER;

            // 우선순위 6: 도구 없음 (채집 불가)
            if (!brain.HasTool)
                return RefusalReasonCode.REFUSE_NO_TOOL;

            // [PR Fix]: F-008 — 폴백에 도달한 경우는 ConflictScore는 거부인데 명시적 이유가 없는 상태.
            // 예기치 않은 호출 경로를 탐지하여 기획자·개발자가 원인을 추적할 수 있도록 경고를 출력한다.
            // 폴백 값 REFUSE_LOYALTY는 그대로 유지 (기존 플레이어 UX 변화 없음).
            Debug.LogWarning($"[ConflictScoreCalculator] DetermineReason: 어떤 생존 임계값도 위반하지 않았으나 " +
                             $"거부 이유 판정이 호출되었습니다. ConflictScore 계산 로직을 확인하세요. " +
                             $"HP={brain.HealthLevel:F1}, SA={brain.SatietyLevel:F1}, " +
                             $"FT={brain.FatigueLevel:F1}, LY={brain.LoyaltyLevel:F1}. " +
                             $"폴백으로 REFUSE_LOYALTY를 반환합니다.");
            return RefusalReasonCode.REFUSE_LOYALTY;
        }

        // ══════════════════════════════════════════════════════════════════════
        // 비공개 헬퍼 메서드
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 해당 명령 수행을 위해 기지(Base) 위치가 필요한지 여부.
        /// 건설, 요리는 기지 기반 시설이 필요하다.
        /// </summary>
        private static bool RequiresBase(OrderType orderType)
        {
            return orderType == OrderType.BuildStructure
                || orderType == OrderType.Cook;
        }

        /// <summary>
        /// 해당 명령이 신체적으로 힘든(체력 소모) 명령인지 여부.
        /// 저체력 상태에서 이런 명령을 받으면 추가 충격 가중치가 붙는다.
        /// </summary>
        private static bool IsPhysicallyDemanding(OrderType orderType)
        {
            return orderType == OrderType.GatherWood
                || orderType == OrderType.GatherStone
                || orderType == OrderType.GatherIron
                || orderType == OrderType.GatherCopper
                || orderType == OrderType.Attack;
        }

        /// <summary>
        /// 해당 명령이 자원 채집 명령인지 여부. 도구 없이 채집을 시도하면 충격 가중치 추가.
        /// </summary>
        private static bool IsGatherOrder(OrderType orderType)
        {
            return orderType == OrderType.GatherWood
                || orderType == OrderType.GatherStone
                || orderType == OrderType.GatherIron
                || orderType == OrderType.GatherCopper;
        }
    }
}
