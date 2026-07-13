using System.IO;
using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M1 명령/거부 게이트 (명세 M1-E).
    ///   M1-T1: 거부 판정 경계 — 실제 AgentConfig 에셋 값 기준 동적 (에셋 튜닝에 자동 추종)
    ///   M1-T2: 명령의 사다리 합류 — P0보다 낮고 작업보다 높으며, 충족·쿨다운 시 스킵
    ///   M1-T3: 재생률 코드 상수 부재 (M1-B ADR-M1-4 회귀 감시)
    /// </summary>
    public class M1_OrderGates
    {
        private static AgentConfigSO LoadCfg()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<AgentConfigSO>("Assets/M0Config/AgentConfig.asset");
            Assert.IsNotNull(cfg, "AgentConfig 에셋 없음");
            return cfg;
        }

        private static GoalSO MakeGoal(string name, int priority, SlotCondition[] trigger, SlotCondition[] goal)
        {
            var g = ScriptableObject.CreateInstance<GoalSO>();
            g.name = name;
            g.Priority = priority;
            g.TriggerConditions = trigger;
            g.GoalConditions = goal;
            return g;
        }

        private static SlotCondition C(SlotId slot, CompareOp op, int v)
            => new SlotCondition { Slot = slot, Op = op, Value = v };

        private static WorldSnapshot Snap(params (SlotId slot, int value)[] pairs)
        {
            var slots = new int[PlanningConfig.TotalSlots];
            foreach ((SlotId slot, int value) in pairs) slots[(int)slot] = value;
            return new WorldSnapshot(slots);
        }

        // ── M1-T1: 거부 판정 경계 (ADR-M1-2) ────────────────────────────────

        [Test]
        public void M1_T1_RefusalBoundaries_FollowConfigAsset()
        {
            AgentConfigSO cfg = LoadCfg();
            float satLimit = cfg.OrderRefuseSatiety;
            float fatLimit = cfg.OrderRefuseFatigue;

            // 배고픔: 임계 '미만'이 거부 (경계값 자체는 수락)
            Assert.AreEqual(VillagerAgent.OrderResult.RefusedHungry,
                VillagerAgent.JudgeOrder(satLimit - 1f, 0f, cfg), "임계 미만은 거부");
            Assert.AreEqual(VillagerAgent.OrderResult.Accepted,
                VillagerAgent.JudgeOrder(satLimit, 0f, cfg), "경계값은 수락");

            // 피로: 임계 '초과'가 거부 (경계값 자체는 수락)
            Assert.AreEqual(VillagerAgent.OrderResult.RefusedTired,
                VillagerAgent.JudgeOrder(100f, fatLimit + 1f, cfg), "임계 초과는 거부");
            Assert.AreEqual(VillagerAgent.OrderResult.Accepted,
                VillagerAgent.JudgeOrder(100f, fatLimit, cfg), "경계값은 수락");

            // 복합: 배고픔 사유가 우선
            Assert.AreEqual(VillagerAgent.OrderResult.RefusedHungry,
                VillagerAgent.JudgeOrder(satLimit - 1f, fatLimit + 1f, cfg));
        }

        // ── M1-T2: 명령의 사다리 합류 (ADR-M1-1) ────────────────────────────

        [Test]
        public void M1_T2_OrderJoinsLadder_BetweenP0AndWork()
        {
            GoalSO p0 = MakeGoal("P0", 100,
                new[] { C(SlotId.MySatiety, CompareOp.LessOrEqual, 20) },
                new[] { C(SlotId.MySatiety, CompareOp.GreaterOrEqual, 70) });
            GoalSO work = MakeGoal("Work", 10,
                null,
                new[] { C(SlotId.WoodStock, CompareOp.GreaterOrEqual, 30) });
            var selector = new GoalSelector(new[] { p0, work });

            GoalSO order = MakeGoal("Order", 60,
                null,
                new[] { C(SlotId.StoneStock, CompareOp.GreaterOrEqual, 20) });

            // 평시: 명령(60) > 작업(10)
            WorldSnapshot calm = Snap((SlotId.MySatiety, 80), (SlotId.StoneStock, 5));
            Assert.AreSame(order, selector.Select(calm, null, order), "명령이 작업보다 우선");

            // 기아: P0(100) > 명령(60) — 그러나 명령은 사라지지 않는다 (호출자 유지)
            WorldSnapshot starving = Snap((SlotId.MySatiety, 10), (SlotId.StoneStock, 5));
            Assert.AreSame(p0, selector.Select(starving, null, order), "P0가 명령을 인터럽트");

            // 명령 충족: 스킵되고 작업으로 하강
            WorldSnapshot done = Snap((SlotId.MySatiety, 80), (SlotId.StoneStock, 25));
            Assert.AreSame(work, selector.Select(done, null, order), "충족된 명령은 스킵");

            // 쿨다운(skip 판정)에 걸린 명령도 하강
            Assert.AreSame(work, selector.Select(calm, g => g == order, order), "쿨다운 명령은 스킵");

            // 명령 없음 = 기존 동작 그대로
            Assert.AreSame(work, selector.Select(calm), "extra 없이 기존 사다리 유지");
        }

        // ── M1-T3: 재생률 코드 상수 부재 (ADR-M1-4 회귀 감시) ────────────────

        [Test]
        public void M1_T3_NoRegenConstantsInCode()
        {
            string root = Path.Combine(Application.dataPath, "Scripts");
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                StringAssert.DoesNotContain("GetDefaultRegenRate", text,
                    $"{file}: 재생률 코드 상수 부활 금지 — ResourceNodeSpawnConfig 에셋이 단일 출처 (ADR-M1-4)");
            }
        }
    }
}
