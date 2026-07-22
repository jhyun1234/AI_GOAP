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
        public void M9_T5_ComputeMyFoodDays_BodyPlusHome_SeasonMult_Neutrals()
        {
            // M11-D 개정: 마을 합산(인원 비례) → 개인(몸+집) 산식. 산식 골격(Σ스톡×가치÷일소요)은 동일.
            var fv = new (SlotId, int)[] { (SlotId.MyCookedFood, 50), (SlotId.MyRawFood, 15) };

            // 몸: 조리6=300 + 생식15=225 → 525 포만, 일소요 25 → 21일치
            Assert.AreEqual(21, WorldModel.ComputeMyFoodDays(fv, 15, 6, 0, 0, 25f, 1f), "몸만·평시");
            // 겨울 배율 1.75 → 525/43.75 = 12
            Assert.AreEqual(12, WorldModel.ComputeMyFoodDays(fv, 15, 6, 0, 0, 25f, 1.75f), "겨울 배율");
            // 집 저장은 몸과 같은 가치 — 생식 4+집 6 = 150 포만 → 6일치
            Assert.AreEqual(6, WorldModel.ComputeMyFoodDays(fv, 4, 0, 6, 0, 25f, 1f), "몸+집 합산");

            Assert.AreEqual(WorldModel.NO_ESTIMATE,
                WorldModel.ComputeMyFoodDays(null, 4, 0, 0, 0, 25f, 1f), "가치표 없음 = 99");
            Assert.AreEqual(WorldModel.NO_ESTIMATE,
                WorldModel.ComputeMyFoodDays(new (SlotId, int)[0], 4, 0, 0, 0, 25f, 1f), "빈 가치표 = 99");
            Assert.AreEqual(WorldModel.NO_ESTIMATE,
                WorldModel.ComputeMyFoodDays(fv, 4, 0, 0, 0, 0f, 1f), "일소요 0 = 99 (중립)");
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
            var fv = new (SlotId, int)[] { (SlotId.MyCookedFood, 7) };
            // 조리 10개 = 70 포만, need = 25×1 = 25 → floor(2.8) = 2
            Assert.AreEqual(2, WorldModel.ComputeMyFoodDays(fv, 0, 10, 0, 0, 25f, 1f),
                "일수는 주입 가치(7)에서 계산 — 산식에 50/15 리터럴 없음");
        }

        [Test]
        public void M9_T5_Snapshot_Neutral_WhenUnwired_And_ComputedWhenWired()
        {
            // 미배선(FoodSources 없음) = 99 (중립, 기존 스냅샷과 diff 0)
            var neutral = new WorldModel(new DiscoveryService(), Config());
            Assert.AreEqual(WorldModel.NO_ESTIMATE, neutral.BuildSnapshot(50, 50).Get(SlotId.MyFoodDaysLeft),
                "미배선 = 99 (중립 불변식)");

            // 배선 시 산출 (M11-D 개인) — 내 생식 5 = 75 포만 ÷ 25 = 3일치
            var cfg = Config();
            cfg.FoodSources = new[] { FoodAction(SlotId.MyCookedFood, 50), FoodAction(SlotId.MyRawFood, 15) };
            var agentCfg = ScriptableObject.CreateInstance<AgentConfigSO>(); // SatietyDecayPerGameDay 기본 25
            var world = new WorldModel(new DiscoveryService(), cfg, null, null, agentCfg);

            Assert.AreEqual(3, world.BuildSnapshot(50, 50, false, false, myRaw: 5).Get(SlotId.MyFoodDaysLeft),
                "배선 스냅샷 = 개인 3일치");
            Assert.AreEqual(3, world.EstimatePersonalFoodDays(5, 0, 0, 0), "HUD 창구도 같은 값 (판정 단일)");
            // 집 저장 포함 — 생식 5 + 집 조리 2 = 75+100 = 175 ÷ 25 = 7일치
            Assert.AreEqual(7, world.EstimatePersonalFoodDays(5, 0, 0, 2), "몸+집 합산 창구");
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
                new SlotCondition { Slot = SlotId.MyFoodDaysLeft, Op = CompareOp.LessOrEqual, Value = 2 },
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
            slots[(int)SlotId.MyFoodDaysLeft] = foodDaysLeft;
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

            // 배선 값 표기 (M11-D — 전 주민 최솟값 라벨) + ≤2일치 붉은 강조
            StringAssert.Contains("식량 최소 5일치", SeasonHud.Compose(4.2f, null, 3f, 5));
            string low = SeasonHud.Compose(4.2f, null, 3f, 2);
            StringAssert.Contains("식량 최소 2일치", low);
            StringAssert.Contains("FF6B6B", low, "2일치 이하 붉은 강조");
            StringAssert.DoesNotContain("FF6B6B", SeasonHud.Compose(4.2f, null, 3f, 3),
                "3일치는 강조 없음");
        }
    }
}
