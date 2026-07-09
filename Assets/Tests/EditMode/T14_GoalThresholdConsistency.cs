using NUnit.Framework;
using AIVillage.AI;
using AIVillage.Core.GOAP;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// T14: Goal 발동 임계값이 항상 Goal의 목표치보다 도달 가능한 방향에 있는지 검증한다.
    /// 어긋나면 Goal 발동 → 즉시 alreadySatisfied → Idle → 다시 발동 → 무한 루프.
    /// (B23~B25 재발 방지 게이트 — 근본 진단 5개 층 이중 진리 문제의 자동 강제)
    ///
    /// 계약 방향:
    ///   - P2 자원 채집(GreaterEq 목표): threshold ≤ target 이어야 한다.
    ///     예) Wood 임계 30, 목표 30 → 임계 발동 시 (stock < 30) 이므로 목표 (>= 30) 미달성 상태.
    ///   - P0 생존(LessEq/GreaterEq 반대 방향): threshold 발동 상태에서 이미 target 미달성이어야 한다.
    ///     예) Hunger LessEq 30, 임계 > 80 → 발동 시점 hunger(81~100) > target(30) 항상 미달성.
    ///     예) Injury GreaterEq 60, 임계 < 20 → 발동 시점 health(0~19) < target(60) 항상 미달성.
    ///
    /// ADR-T5: 리플렉션 사용 금지. GatherGoalSelector 상수는 public, VillagerFSM은 internal.
    /// AssemblyInfo.cs의 InternalsVisibleTo가 AIVillage.Tests.EditMode에 internal 접근을 허용한다.
    /// </summary>
    public class T14_GoalThresholdConsistency
    {
        // ──────────────────────────────────────────────────────────────────
        // P2 자원 채집: 임계값 ≤ 목표치 (GreaterEq 방향)
        // ──────────────────────────────────────────────────────────────────

        [Test]
        public void GatherWood_threshold_at_most_goal_target()
        {
            // GatherGoalSelector: Wood stock < T_COMMON(30) → GatherWood 발동
            // BuildGoalState:     GatherWood 목표 WoodStock GreaterEq 30
            float threshold = GatherGoalSelector.T_COMMON;
            int   target    = GOAPPlanningSlots.GoalTarget.GatherWood_WoodStock;
            Assert.LessOrEqual(threshold, (float)target,
                $"[T14 GatherWood] 임계값({threshold}) > 목표치({target}). " +
                "발동 시점에 이미 목표 달성 → alreadySatisfied 무한 루프. " +
                "GatherGoalSelector.T_COMMON 또는 GoalTarget.GatherWood_WoodStock 중 하나를 정정하세요.");
        }

        [Test]
        public void GatherStone_threshold_at_most_goal_target()
        {
            float threshold = GatherGoalSelector.T_COMMON;
            int   target    = GOAPPlanningSlots.GoalTarget.GatherStone_StoneStock;
            Assert.LessOrEqual(threshold, (float)target,
                $"[T14 GatherStone] 임계값({threshold}) > 목표치({target}). " +
                "발동 시점에 이미 목표 달성 → alreadySatisfied 무한 루프. " +
                "GatherGoalSelector.T_COMMON 또는 GoalTarget.GatherStone_StoneStock 중 하나를 정정하세요.");
        }

        [Test]
        public void GatherIron_threshold_at_most_goal_target()
        {
            float threshold = GatherGoalSelector.T_RARE;
            int   target    = GOAPPlanningSlots.GoalTarget.GatherIron_IronStock;
            Assert.LessOrEqual(threshold, (float)target,
                $"[T14 GatherIron] 임계값({threshold}) > 목표치({target}). " +
                "발동 시점에 이미 목표 달성 → alreadySatisfied 무한 루프. " +
                "GatherGoalSelector.T_RARE 또는 GoalTarget.GatherIron_IronStock 중 하나를 정정하세요.");
        }

        [Test]
        public void GatherCopper_threshold_at_most_goal_target()
        {
            float threshold = GatherGoalSelector.T_RARE;
            int   target    = GOAPPlanningSlots.GoalTarget.GatherCopper_CopperStock;
            Assert.LessOrEqual(threshold, (float)target,
                $"[T14 GatherCopper] 임계값({threshold}) > 목표치({target}). " +
                "발동 시점에 이미 목표 달성 → alreadySatisfied 무한 루프. " +
                "GatherGoalSelector.T_RARE 또는 GoalTarget.GatherCopper_CopperStock 중 하나를 정정하세요.");
        }

        [Test]
        public void GatherFood_threshold_at_most_goal_target()
        {
            float threshold = GatherGoalSelector.T_FOOD;
            int   target    = GOAPPlanningSlots.GoalTarget.GatherFood_RawFoodStock;
            Assert.LessOrEqual(threshold, (float)target,
                $"[T14 GatherFood] 임계값({threshold}) > 목표치({target}). " +
                "발동 시점에 이미 목표 달성 → alreadySatisfied 무한 루프. " +
                "GatherGoalSelector.T_FOOD 또는 GoalTarget.GatherFood_RawFoodStock 중 하나를 정정하세요.");
        }

        // ──────────────────────────────────────────────────────────────────
        // P0 생존: 발동 시점에 반드시 목표 미달성 상태여야 한다 (방향 반대)
        // ──────────────────────────────────────────────────────────────────

        [Test]
        public void SurviveHunger_trigger_implies_target_unreached()
        {
            // 발동: SatietyLevel < 20 → 발동 최대 satiety = 19
            // 목표: MySatiety GreaterEq 70
            // 정상 조건: 발동 최대값(19) < 목표치(70) — 발동 시점 항상 미달성.
            float triggerMax = VillagerFSM.P0_HUNGER_TRIGGER_SATIETY - 1f;
            int   target     = GOAPPlanningSlots.GoalTarget.SurviveHunger_MySatiety;
            Assert.Less(triggerMax, (float)target,
                $"[T14 SurviveHunger] 발동 최대 satiety({triggerMax}) ≥ 목표치({target}). " +
                "발동 시점이 이미 목표(GreaterEq) 달성 상태 → alreadySatisfied 무한 루프. " +
                "VillagerFSM.P0_HUNGER_TRIGGER_SATIETY 또는 GoalTarget.SurviveHunger_MySatiety를 정정하세요.");
        }

        [Test]
        public void SurviveInjury_trigger_implies_target_unreached()
        {
            // 발동: HealthLevel < 20 → 발동 최대 health = 19
            // 목표: MyHealth GreaterEq 60
            // 정상 조건: 발동 최대값(19) < 목표치(60) — 발동 시점 항상 미달성.
            float triggerMax = VillagerFSM.P0_INJURY_TRIGGER_HEALTH - 1f;
            int   target     = GOAPPlanningSlots.GoalTarget.SurviveInjury_MyHealth;
            Assert.Less(triggerMax, (float)target,
                $"[T14 SurviveInjury] 발동 최대 health({triggerMax}) ≥ 목표치({target}). " +
                "발동 시점이 이미 목표(GreaterEq) 달성 상태 → alreadySatisfied 무한 루프. " +
                "VillagerFSM.P0_INJURY_TRIGGER_HEALTH 또는 GoalTarget.SurviveInjury_MyHealth를 정정하세요.");
        }

        [Test]
        public void SurviveFatigue_trigger_implies_target_unreached()
        {
            // 발동: FatigueLevel > 90 → 발동 최소 fatigue = 91
            // 목표: MyFatigue LessEq 30
            // 정상 조건: 발동 최소값(91) > 목표치(30) — 발동 시점 항상 미달성.
            float triggerMin = VillagerFSM.P0_FATIGUE_TRIGGER_FATIGUE + 1f;
            int   target     = GOAPPlanningSlots.GoalTarget.SurviveFatigue_MyFatigue;
            Assert.Greater(triggerMin, (float)target,
                $"[T14 SurviveFatigue] 발동 최소 fatigue({triggerMin}) ≤ 목표치({target}). " +
                "발동 시점이 이미 목표(LessEq) 달성 상태 → alreadySatisfied 무한 루프. " +
                "VillagerFSM.P0_FATIGUE_TRIGGER_FATIGUE 또는 GoalTarget.SurviveFatigue_MyFatigue를 정정하세요.");
        }
    }
}
