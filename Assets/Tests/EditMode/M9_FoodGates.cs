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
    }
}
