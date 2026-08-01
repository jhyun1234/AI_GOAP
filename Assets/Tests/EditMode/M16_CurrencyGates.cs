using AIVillage.M0;
using NUnit.Framework;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M16 화폐 게이트 — 회계 축의 3대 보장:
    /// 지갑 슬롯 분류(T1 — IsStock에 새면 EffectApplier가 전역 라우팅해 돈이 증발한다),
    /// 상한 예외(T1 — 돈에 BodyCarryCap이 걸리면 임금이 8에서 막힌다, 명세 실사 ④),
    /// 통화량 계단(T2 — 소멸이 발행을 넘어도 음수 금지).
    /// </summary>
    public class M16_CurrencyGates
    {
        [Test]
        public void M16_T1_MyMoney_SlotClassification()
        {
            Assert.IsTrue(SlotIds.IsPersonalStock(SlotId.MyMoney), "이전·지급 판정 공유 (TransferTo)");
            Assert.IsFalse(SlotIds.IsStock(SlotId.MyMoney), "전역 스톡 아님 — W1 ⚠️ (라우팅 오염 금지)");
            Assert.IsFalse(SlotIds.IsHomeStock(SlotId.MyMoney), "집 저장 없음 — 돈은 몸만");
            Assert.IsTrue(SlotIds.IsNumeric(SlotId.MyMoney));
        }

        [Test]
        public void M16_T1_PersonalCap_MoneyUnlimited_FoodStillCapped()
        {
            // 돈은 부피가 없다 — 상한 예외는 PersonalCapOf 한 곳 (BodyCarryCap 인상 금지)
            Assert.AreEqual(int.MaxValue, VillagerAgent.PersonalCapOf(SlotId.MyMoney, 8));
            Assert.AreEqual(8, VillagerAgent.PersonalCapOf(SlotId.MyRawFood, 8), "식량은 여전히 상한 8");

            // 큰 임금 적립이 상한에 막히지 않는다 / 식량은 기존 그대로 막힌다
            (bool okMoney, int nextMoney) = VillagerAgent.NextPersonalStock(
                1000, EffectOp.Add, 999999, VillagerAgent.PersonalCapOf(SlotId.MyMoney, 8));
            Assert.IsTrue(okMoney);
            Assert.AreEqual(1000999, nextMoney);
            (bool okFood, _) = VillagerAgent.NextPersonalStock(
                7, EffectOp.Add, 2, VillagerAgent.PersonalCapOf(SlotId.MyRawFood, 8));
            Assert.IsFalse(okFood, "식량 상한 초과는 기존대로 실패 (클램프 아님)");
        }

        [Test]
        public void M16_T2_NextSupply_ClampsAtZero()
        {
            Assert.AreEqual(5, WorldModel.NextSupply(0, 5), "발행");
            Assert.AreEqual(7, WorldModel.NextSupply(5, 2), "누적");
            Assert.AreEqual(2, WorldModel.NextSupply(5, -3), "소멸");
            Assert.AreEqual(0, WorldModel.NextSupply(3, -10), "소멸이 발행을 넘으면 0 (음수 금지)");
        }

        [Test]
        public void M16_T5_ComputePricePct_FloorCapAndProportion()
        {
            // 기준가 3, 상한 400 (명세 제안치와 동일한 산식 검증 — 값 자체는 에셋이 정본)
            Assert.AreEqual(100, WorldModel.ComputePricePct(0, 10, 3, 400), "M=0 → 100 (화폐 없던 판과 연속)");
            Assert.AreEqual(100, WorldModel.ComputePricePct(30, 10, 3, 400), "M = Q×기준가 → 정확히 100");
            Assert.AreEqual(200, WorldModel.ComputePricePct(60, 10, 3, 400), "M 2배 → 200");
            Assert.AreEqual(400, WorldModel.ComputePricePct(9999, 10, 3, 400), "상한 클램프");
            Assert.AreEqual(400, WorldModel.ComputePricePct(9999, 0, 3, 400), "Q=0 방어 (denom 1) — 상한으로");
            Assert.AreEqual(100, WorldModel.ComputePricePct(15, 10, 3, 400), "M이 작으면 하한 100 (디플레 없음)");
        }

        [Test]
        public void M16_T4_EffectiveRewardScale_AndPriceFlipsVerdict()
        {
            Assert.AreEqual(1f, VillagerAgent.EffectiveRewardScale(100), 1e-4f);
            Assert.AreEqual(0.5f, VillagerAgent.EffectiveRewardScale(200), 1e-4f);
            Assert.AreEqual(1f, VillagerAgent.EffectiveRewardScale(50), 1e-4f, "100 미만 방어 (디플레 없음)");

            // 물가가 판정을 뒤집는다 (ADR-M16-4): 화폐 웃돈(-20 오프셋)으로 기준 물가에선
            // 수락하던 배고픔이, 물가 200%(실효 -10)에선 거부로. 실물 보상은 물가 무관.
            var cfg = UnityEngine.ScriptableObject.CreateInstance<AgentConfigSO>();
            var money = UnityEngine.ScriptableObject.CreateInstance<RewardSO>();
            money.MoneyGain = 5; money.RefuseSatietyOffset = -20f; money.RefuseFatigueOffset = 20f;
            var food = UnityEngine.ScriptableObject.CreateInstance<RewardSO>();
            food.MoneyGain = 0; food.RefuseSatietyOffset = -20f; food.RefuseFatigueOffset = 20f;
            // 기준 문턱과 실효 문턱 사이의 포만 = 물가에만 반응하는 회색 지대
            float satiety = cfg.OrderRefuseSatiety - 15f; // 기준 -20이면 수락, 실효 -10이면 거부
            float fatigue = 0f;

            Assert.AreEqual(VillagerAgent.OrderResult.Accepted,
                VillagerAgent.JudgeOrder(satiety, fatigue, cfg, null, null, money, 100), "기준 물가 = 수락");
            Assert.AreEqual(VillagerAgent.OrderResult.RefusedHungry,
                VillagerAgent.JudgeOrder(satiety, fatigue, cfg, null, null, money, 200), "물가 200% = 거부 (실질 가치 미달)");
            Assert.AreEqual(VillagerAgent.OrderResult.Accepted,
                VillagerAgent.JudgeOrder(satiety, fatigue, cfg, null, null, food, 200), "실물 보상은 물가 무관");
            Assert.AreEqual(
                VillagerAgent.JudgeOrder(satiety, fatigue, cfg, null, null, money),
                VillagerAgent.JudgeOrder(satiety, fatigue, cfg, null, null, money, 100),
                "호환 진입점 = 물가 100 위임 (기존 게이트 동작 불변)");

            UnityEngine.Object.DestroyImmediate(cfg);
            UnityEngine.Object.DestroyImmediate(money);
            UnityEngine.Object.DestroyImmediate(food);
        }

        [Test]
        public void M16_T6_TradePrice_CeilingScalesWithInflation()
        {
            Assert.AreEqual(3, RequestService.TradePrice(3, 100), "기준 물가 = 기준가");
            Assert.AreEqual(5, RequestService.TradePrice(3, 150), "150% — 4.5 올림 = 5 (저액도 착탄)");
            Assert.AreEqual(6, RequestService.TradePrice(3, 200));
            Assert.AreEqual(3, RequestService.TradePrice(3, 50), "100 미만 방어 (디플레 없음)");
            Assert.AreEqual(4, RequestService.TradePrice(3, 101), "1%만 올라도 올림으로 반영");
        }

        [Test]
        public void M16_T9_WageEffect_PlannerSeesMoneyWithoutCap()
        {
            // 유급 액션: 플래너 입력에는 임금이 보이고, 실행 입력에는 안 보인다 (이중 지급 차단)
            var paid = UnityEngine.ScriptableObject.CreateInstance<GatherActionSO>();
            paid.WagePay = 5;
            var runEffs = new System.Collections.Generic.List<SlotEffect>();
            var effs = new System.Collections.Generic.List<SlotEffect>();
            paid.CollectEffects(runEffs);
            paid.CollectPlannerEffects(effs);
            Assert.AreEqual(0, runEffs.Count, "실행 경로엔 임금 없음 — 지급은 Mint 1곳");
            Assert.AreEqual(1, effs.Count, "플래너는 '이 일을 하면 돈이 생긴다'를 안다");
            Assert.AreEqual(SlotId.MyMoney, effs[0].Slot);
            Assert.AreEqual(EffectOp.Add, effs[0].Op);
            Assert.AreEqual(5, effs[0].Value);

            // 무급 = 중립 (기존 액션 전부 동작 불변)
            var free = UnityEngine.ScriptableObject.CreateInstance<GatherActionSO>();
            var freeEffs = new System.Collections.Generic.List<SlotEffect>();
            free.CollectPlannerEffects(freeEffs);
            Assert.AreEqual(0, freeEffs.Count, "WagePay 0 = 주입 없음");

            // 🔴 상한 전제가 붙으면 안 된다 — 붙으면 "지갑 8동 넘으면 벌목 불가" 플랜이 나온다
            var precs = new System.Collections.Generic.List<SlotCondition>();
            ActionCompiler.InjectCapPreconditions(precs, effs, bodyCap: 8, homeCap: 12);
            Assert.AreEqual(0, precs.Count, "돈은 부피가 없다 — 상한 전제 주입 금지");

            UnityEngine.Object.DestroyImmediate(paid);
            UnityEngine.Object.DestroyImmediate(free);

            // 대조: 식량 Add는 여전히 상한 전제가 붙는다 (M11-A 불변)
            var foodEffs = new System.Collections.Generic.List<SlotEffect>
                { new SlotEffect { Slot = SlotId.MyRawFood, Op = EffectOp.Add, Value = 4 } };
            var foodPrecs = new System.Collections.Generic.List<SlotCondition>();
            ActionCompiler.InjectCapPreconditions(foodPrecs, foodEffs, bodyCap: 8, homeCap: 12);
            Assert.AreEqual(1, foodPrecs.Count, "식량 상한 주입은 그대로 (ADR-M11-3)");
            Assert.AreEqual(4, foodPrecs[0].Value, "8 - 4");
        }

        [Test]
        public void M16_T8_ComposeMoney_TieredDisplay()
        {
            // M17-W5 진법 개편: 1은 = 100동, 1금 = 100은 = 10,000동 (舊 1은 = 10동).
            // 이 게이트는 ComposeMoney의 짝이다 — 한쪽만 고치면 화면과 기대가 갈린다 (방법론 M17).
            Assert.AreEqual("0동", SeasonHud.ComposeMoney(0));
            Assert.AreEqual("7동", SeasonHud.ComposeMoney(7));
            Assert.AreEqual("13동", SeasonHud.ComposeMoney(13), "100 미만은 전부 동");
            Assert.AreEqual("50동", SeasonHud.ComposeMoney(50), "집값 50동 = 아직 은에 못 미친다");
            Assert.AreEqual("1은", SeasonHud.ComposeMoney(100), "0 단위 생략");
            Assert.AreEqual("2은 47동", SeasonHud.ComposeMoney(247));
            Assert.AreEqual("1금", SeasonHud.ComposeMoney(10000));
            Assert.AreEqual("1금 2은 47동", SeasonHud.ComposeMoney(10247));
            Assert.AreEqual("1금 47동", SeasonHud.ComposeMoney(10047), "가운데 단위만 0이면 건너뛴다");
            Assert.AreEqual("0동", SeasonHud.ComposeMoney(-5), "음수 방어");
        }
    }
}
