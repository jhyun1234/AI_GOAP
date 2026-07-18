using AIVillage.M0;
using NUnit.Framework;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M9 식량 수지 게이트 (보완가이드 M9-G·H, M9-T5·T6). 이 파일이 FoodDaysLeft 산식의
    /// 인원 비례·계절 배율·중립, 가치의 액션 파생(리터럴 0), 상대 씬 goal 확장을 검증한다.
    /// </summary>
    public class M9_FoodGates
    {
        private static WorldConfigSO Config()
        {
            var c = ScriptableObject.CreateInstance<WorldConfigSO>();
            c.InitialWoodStock = 0;
            c.InitialRawFoodStock = 0;
            c.InitialStoneStock = 0;
            return c;
        }

        /// <summary>소비 액션 목킹 — 스톡 SubClamp0 1 + MySatiety Add gain (EatCooked/EatRaw 구조).</summary>
        private static ConsumeActionSO FoodAction(SlotId stock, int gain)
        {
            var a = ScriptableObject.CreateInstance<ConsumeActionSO>();
            a.DisplayName = $"먹기_{stock}";
            a.Effects = new[]
            {
                new SlotEffect { Slot = stock, Op = EffectOp.SubClamp0, Value = 1 },
                new SlotEffect { Slot = SlotId.MySatiety, Op = EffectOp.Add, Value = gain },
            };
            return a;
        }

        [Test]
        public void M9_T5_ComputeFoodDaysLeft_CheckCases_ProportionalToHeadcount()
        {
            var fv = new (SlotId, int)[] { (SlotId.CookedFoodStock, 50), (SlotId.RawFoodStock, 15) };
            var slots = new int[PlanningConfig.TotalSlots];
            slots[(int)SlotId.CookedFoodStock] = 6;   // 300 포만
            slots[(int)SlotId.RawFoodStock] = 15;     // 225 포만 → 합 525

            Assert.AreEqual(5, WorldModel.ComputeFoodDaysLeft(fv, slots, 4, 25f, 1f), "4명·평시 = 5일치");
            Assert.AreEqual(3, WorldModel.ComputeFoodDaysLeft(fv, slots, 4, 25f, 1.75f), "겨울 배율 = 3일치");
            Assert.AreEqual(2, WorldModel.ComputeFoodDaysLeft(fv, slots, 8, 25f, 1f), "인원 2배 = 절반 (M9-S12)");

            Assert.AreEqual(WorldModel.NO_ESTIMATE,
                WorldModel.ComputeFoodDaysLeft(fv, slots, 0, 25f, 1f), "인원 0 = 99 (중립)");
            Assert.AreEqual(WorldModel.NO_ESTIMATE,
                WorldModel.ComputeFoodDaysLeft(null, slots, 4, 25f, 1f), "가치표 없음 = 99");
            Assert.AreEqual(WorldModel.NO_ESTIMATE,
                WorldModel.ComputeFoodDaysLeft(new (SlotId, int)[0], slots, 4, 25f, 1f), "빈 가치표 = 99");
        }

        [Test]
        public void M9_T5_TryGetFoodValue_DerivesFromEffects_NonFoodFalse()
        {
            Assert.IsTrue(WorldModel.TryGetFoodValue(FoodAction(SlotId.CookedFoodStock, 50),
                out SlotId s, out int g));
            Assert.AreEqual(SlotId.CookedFoodStock, s);
            Assert.AreEqual(50, g, "가치는 액션 효과에서 파생 (MySatiety Add 값)");

            // 스톡 차감 없이 포만만 = 식량 아님
            var noStock = ScriptableObject.CreateInstance<ConsumeActionSO>();
            noStock.Effects = new[] { new SlotEffect { Slot = SlotId.MySatiety, Op = EffectOp.Add, Value = 10 } };
            Assert.IsFalse(WorldModel.TryGetFoodValue(noStock, out _, out _), "스톡 차감 없으면 식량 아님");

            // 포만 획득 없이 스톡 차감만 (건설 재료류) = 식량 아님
            var noGain = ScriptableObject.CreateInstance<ConsumeActionSO>();
            noGain.Effects = new[] { new SlotEffect { Slot = SlotId.WoodStock, Op = EffectOp.SubClamp0, Value = 1 } };
            Assert.IsFalse(WorldModel.TryGetFoodValue(noGain, out _, out _), "포만 획득 없으면 식량 아님");

            Assert.IsFalse(WorldModel.TryGetFoodValue(null, out _, out _));
        }

        [Test]
        public void M9_T5_NoValueLiteral_UsesInjectedGain()
        {
            // 가치를 임의값(7 — 50/15 아님)으로 주입 → 산식이 그 값을 그대로 쓴다 (리터럴 없음, M9-S13)
            var fv = new (SlotId, int)[] { (SlotId.CookedFoodStock, 7) };
            var slots = new int[PlanningConfig.TotalSlots];
            slots[(int)SlotId.CookedFoodStock] = 10; // 70 포만, need = 2×25×1 = 50 → floor(1.4)=1
            Assert.AreEqual(1, WorldModel.ComputeFoodDaysLeft(fv, slots, 2, 25f, 1f),
                "일수는 주입 가치(7)에서 계산 — 산식에 50/15 리터럴 없음");
        }

        [Test]
        public void M9_T5_Snapshot_Neutral_WhenUnwired_And_ComputedWhenWired()
        {
            // 미배선(aliveCount·FoodSources 없음) = 99 (중립, 기존 스냅샷과 diff 0)
            var neutral = new WorldModel(new DiscoveryService(), Config());
            Assert.AreEqual(WorldModel.NO_ESTIMATE, neutral.BuildSnapshot(50, 50).Get(SlotId.FoodDaysLeft),
                "미배선 = 99 (중립 불변식)");

            // 배선 시 산출 — 조리6·생식15·4명·평시 = 5
            var cfg = Config();
            cfg.FoodSources = new[] { FoodAction(SlotId.CookedFoodStock, 50), FoodAction(SlotId.RawFoodStock, 15) };
            var agentCfg = ScriptableObject.CreateInstance<AgentConfigSO>(); // SatietyDecayPerGameDay 기본 25
            var world = new WorldModel(new DiscoveryService(), cfg, null, null, () => 4, agentCfg);
            world.AddStock(SlotId.CookedFoodStock, 6);
            world.AddStock(SlotId.RawFoodStock, 15);

            Assert.AreEqual(5, world.BuildSnapshot(50, 50).Get(SlotId.FoodDaysLeft), "배선 스냅샷 = 5일치");
            Assert.AreEqual(5, world.EstimateFoodDaysLeft(), "HUD 창구도 같은 값 (판정 단일)");
        }

        // ── M9-T6: RelativeToCurrent 씬 goal 확장 (펌프 순환의 전제) ──────────────

        /// <summary>펌프형 상대 goal — 트리거 FoodDaysLeft≤2, 목표 Cooked+2(증분), MaxWorkers 1.</summary>
        private static GoalSO PumpGoal(bool relative)
        {
            var g = ScriptableObject.CreateInstance<GoalSO>();
            g.DisplayName = relative ? "상대비축" : "절대비축";
            g.Priority = 18;
            g.TriggerConditions = new[]
            {
                new SlotCondition { Slot = SlotId.FoodDaysLeft, Op = CompareOp.LessOrEqual, Value = 2 },
            };
            g.GoalConditions = new[]
            {
                new SlotCondition { Slot = SlotId.CookedFoodStock, Op = CompareOp.GreaterOrEqual, Value = 2 },
            };
            g.RelativeToCurrent = relative;
            g.MaxWorkers = 1;
            return g;
        }

        private static WorldSnapshot Snap(int foodDaysLeft, int cooked)
        {
            var slots = new int[PlanningConfig.TotalSlots];
            slots[(int)SlotId.FoodDaysLeft] = foodDaysLeft;
            slots[(int)SlotId.CookedFoodStock] = cooked;
            return new WorldSnapshot(slots);
        }

        [Test]
        public void M9_T6_RelativeSceneGoal_ExemptFromAlreadySatisfied_AbsoluteSkips()
        {
            GoalSO rel = PumpGoal(relative: true);
            GoalSO abs = PumpGoal(relative: false);
            // 트리거 발동(FoodDaysLeft 1) + 절대 판정이면 "Cooked 5 ≥ 2 이미 달성"
            WorldSnapshot snap = Snap(foodDaysLeft: 1, cooked: 5);

            Assert.AreSame(rel, new GoalSelector(new[] { rel }).Select(snap),
                "상대 goal은 '이미 달성' 면제 → 후보 통과 (⚠①, 가장 그럴듯한 실패 모드)");
            Assert.IsNull(new GoalSelector(new[] { abs }).Select(snap),
                "절대 goal은 이미 달성으로 스킵 — 기존 동작 diff 0 (중립 불변식)");
        }

        [Test]
        public void M9_T6_RelativeSceneGoal_TriggerStillGates_PumpTerminates()
        {
            GoalSO rel = PumpGoal(relative: true);
            // 트리거 불발(FoodDaysLeft 5 > 2) — 실물 식량이 늘어 트리거를 벗어난 상태
            Assert.IsNull(new GoalSelector(new[] { rel }).Select(Snap(foodDaysLeft: 5, cooked: 5)),
                "트리거 불발 = 후보 제외 (펌프 종결 — 무한 재발동 없음)");
        }

        [Test]
        public void M9_T6_RelativeSceneGoal_QuotaPreserved_ClaimKeyIsOriginal()
        {
            GoalSO rel = PumpGoal(relative: true); // MaxWorkers 1
            var sel = new GoalSelector(new[] { rel });
            WorldSnapshot snap = Snap(foodDaysLeft: 1, cooked: 5);

            GoalSO first = sel.Select(snap);
            Assert.AreSame(rel, first, "첫 주민 선택 = 원본 에셋");
            sel.Claim(first); // VillagerAgent가 _goal=원본으로 클레임하는 것의 근거
            Assert.IsTrue(sel.IsFull(rel), "MaxWorkers 1 정원 참");
            Assert.IsNull(sel.Select(snap),
                "정원 참 → 두 번째 주민 제외 (⚠② — 사본을 _goal에 넣으면 매번 새 키라 무력화될 경로)");
        }

        // ── M9-I: HUD 식량 일수 표기 (표현, 중립 불변식) ─────────────────────────

        [Test]
        public void M9_I_Compose_FoodSuffix_NeutralWhenUnwired_RedWhenLow()
        {
            // 미배선(99) = 표기 없음 (기존 달력과 diff 0 — M6 게이트 불변)
            Assert.AreEqual("Day 4", SeasonHud.Compose(4.2f, null, 3f),
                "3-arg 기존 호출 = 식량 표기 없음");
            Assert.AreEqual("Day 4", SeasonHud.Compose(4.2f, null, 3f, WorldModel.NO_ESTIMATE),
                "99(중립) = 표기 없음");

            // 배선 값 표기 + ≤2일치 붉은 강조
            StringAssert.Contains("식량 5일치", SeasonHud.Compose(4.2f, null, 3f, 5));
            string low = SeasonHud.Compose(4.2f, null, 3f, 2);
            StringAssert.Contains("식량 2일치", low);
            StringAssert.Contains("FF6B6B", low, "2일치 이하 붉은 강조");
            StringAssert.DoesNotContain("FF6B6B", SeasonHud.Compose(4.2f, null, 3f, 3),
                "3일치는 강조 없음");
        }
    }
}
