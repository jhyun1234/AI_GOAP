using AIVillage.M0;
using NUnit.Framework;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M6-E 보상 재설득 게이트 (M6-T4) — 판정 오프셋(순수)과 에스크로 스톡 산수.
    /// TryGiveOrder 전체 흐름(약속→지급/반환)의 배선은 Play 로그 체인(M6-S5)이 담당한다.
    /// </summary>
    public class M6_RewardGates
    {
        private static AgentConfigSO Cfg()
        {
            var cfg = ScriptableObject.CreateInstance<AgentConfigSO>();
            cfg.OrderRefuseSatiety = 35f; // M1-C 값 고정 (에셋 기본과 동일)
            cfg.OrderRefuseFatigue = 70f;
            return cfg;
        }

        private static RewardSO Reward(float satOffset = -20f, float fatOffset = 20f)
        {
            var r = ScriptableObject.CreateInstance<RewardSO>();
            r.RefuseSatietyOffset = satOffset;
            r.RefuseFatigueOffset = fatOffset;
            return r;
        }

        [Test]
        public void M6_T4_NullReward_IdenticalToM4Judgement()
        {
            // r=null = 기존 판정과 완전 동일 (중립 불변식 — 성격 조합 포함 전수)
            AgentConfigSO cfg = Cfg();
            var p = ScriptableObject.CreateInstance<PersonalitySO>();
            p.RefuseSatietyOffset = 10f; // 새침이 계열 — 더 쉽게 배고픔 거부

            (float sat, float fat)[] cases =
                { (80f, 10f), (34f, 10f), (44f, 10f), (80f, 71f), (20f, 90f), (35f, 70f) };
            foreach ((float sat, float fat) in cases)
            {
                Assert.AreEqual(VillagerAgent.JudgeOrder(sat, fat, cfg),
                                VillagerAgent.JudgeOrder(sat, fat, cfg, null, null),
                                $"성격 없음 ({sat},{fat})");
                Assert.AreEqual(VillagerAgent.JudgeOrder(sat, fat, cfg, p),
                                VillagerAgent.JudgeOrder(sat, fat, cfg, p, null),
                                $"성격 있음 ({sat},{fat})");
            }

            Object.DestroyImmediate(p);
            Object.DestroyImmediate(cfg);
        }

        [Test]
        public void M6_T4_RewardOffsets_FlipRefusalDeterministically()
        {
            AgentConfigSO cfg = Cfg();
            RewardSO r = Reward(); // 배고픔 문턱 35→15, 피로 문턱 70→90

            // 배고픔 구간 (15 ≤ 포만 < 35): 보상 없이는 거부, 보상이면 수락 — 협상의 핵심 구간
            Assert.AreEqual(VillagerAgent.OrderResult.RefusedHungry,
                            VillagerAgent.JudgeOrder(20f, 10f, cfg, null, null), "보상 없음 = 거부");
            Assert.AreEqual(VillagerAgent.OrderResult.Accepted,
                            VillagerAgent.JudgeOrder(20f, 10f, cfg, null, r), "보상 = 수락");

            // 문턱 아래 (포만 < 15): 보상을 걸어도 거부 — 결정적 학습 가능성 유지 (M6-E DoD)
            Assert.AreEqual(VillagerAgent.OrderResult.RefusedHungry,
                            VillagerAgent.JudgeOrder(10f, 10f, cfg, null, r), "한계 이하는 보상도 소용없음");

            // 피로 구간 (70 < 피로 ≤ 90): 대칭 동작
            Assert.AreEqual(VillagerAgent.OrderResult.RefusedTired,
                            VillagerAgent.JudgeOrder(80f, 80f, cfg, null, null));
            Assert.AreEqual(VillagerAgent.OrderResult.Accepted,
                            VillagerAgent.JudgeOrder(80f, 80f, cfg, null, r));
            Assert.AreEqual(VillagerAgent.OrderResult.RefusedTired,
                            VillagerAgent.JudgeOrder(80f, 95f, cfg, null, r), "피로 한계 초과는 거부 유지");

            // 성격 오프셋과 합산 (고집쟁이 +10 → 보상 합산 문턱 25): 포만 20은 고집쟁이면 거부
            var stubborn = ScriptableObject.CreateInstance<PersonalitySO>();
            stubborn.RefuseSatietyOffset = 10f;
            Assert.AreEqual(VillagerAgent.OrderResult.RefusedHungry,
                            VillagerAgent.JudgeOrder(20f, 10f, cfg, stubborn, r),
                            "성격+보상 오프셋 합산 (35+10-20=25 > 20)");

            Object.DestroyImmediate(stubborn);
            Object.DestroyImmediate(r);
            Object.DestroyImmediate(cfg);
        }

        [Test]
        public void M6_T4b_EscrowArithmetic_SpendRefundAtomicity()
        {
            // 에스크로의 스톡 산수 — TrySpend 원자성(부족 시 무변경) + 반환 왕복
            var world = new WorldModel(null, null);
            world.AddStock(SlotId.CookedFoodStock, 3);

            Assert.IsFalse(world.TrySpendStock(SlotId.CookedFoodStock, 5), "부족 = 하달 불가");
            Assert.AreEqual(3, world.GetStock(SlotId.CookedFoodStock), "실패 시 차감 0 (원자성)");

            Assert.IsTrue(world.TrySpendStock(SlotId.CookedFoodStock, 2), "수락 = 에스크로 차감");
            Assert.AreEqual(1, world.GetStock(SlotId.CookedFoodStock));

            world.AddStock(SlotId.CookedFoodStock, 2); // 취소/이탈 = 반환 (ClearOrderInstance 경로)
            Assert.AreEqual(3, world.GetStock(SlotId.CookedFoodStock), "반환으로 원상 복구");
        }
    }
}
