using System.Collections.Generic;
using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// 목표 도달 가능성 게이트 (2026-07-23 신설) — **에셋 결함이 Burst 크래시로 나타나는 것**을 막는다.
    /// 경위: M11-E에서 밭 goal의 목표를 개인 슬롯(MyFarmPlotCount)으로 바꾸면서 BuildFarmPlot 액션에
    /// 대응하는 플래너 픽션 효과를 넣지 않았다 → maxGain[27]==0 → CalculateHeuristic의 정수 올림
    /// 나눗셈이 0으로 나눔 → 잡에서 DivideByZeroException (스택만 나오고 원인을 못 짚는 최악의 실패).
    /// 이 게이트는 씬에 등록된 모든 goal + 부탁 주입 goal을 카탈로그와 대조해 사전에 잡는다.
    /// </summary>
    public class M11_GoalReachabilityGate
    {
        private static ActionCatalog Catalog()
            => AssetDatabase.LoadAssetAtPath<ActionCatalog>("Assets/M0Config/ActionCatalog.asset");

        private static IEnumerable<GoalSO> AllGoalAssets()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:GoalSO", new[] { "Assets/M0Config/Goals" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var g = AssetDatabase.LoadAssetAtPath<GoalSO>(path);
                if (g != null) yield return g;
            }
        }

        /// <summary>씬 _goals 등록분만 실사용이지만, 휴면 goal도 되살릴 때 터지면 안 되므로 전수 검사한다.
        /// 파킹된 goal(Goal_BuildIrrigation 등)이 걸리면 그건 되살릴 때 고쳐야 할 실제 결함이다.</summary>
        [Test]
        public void GoalSlots_AreReachableByCatalogActions()
        {
            ActionCatalog catalog = Catalog();
            Assert.IsNotNull(catalog, "ActionCatalog 에셋 없음");

            AIVillage.Core.GOAP.GOAPActionDef[] defs = ActionCompiler.CompileManaged(catalog);
            ActionCompiler.ComputeMaxGainDrop(defs, PlanningConfig.TotalSlots,
                                              out float[] maxGain, out float[] maxDrop);

            var broken = new List<string>();
            foreach (GoalSO g in AllGoalAssets())
            {
                if (g.GoalConditions == null || g.GoalConditions.Length == 0) continue; // 일과·심부름 goal
                foreach (SlotCondition c in g.GoalConditions)
                {
                    int s = (int)c.Slot;
                    if (c.Op == CompareOp.GreaterOrEqual && maxGain[s] <= 0f)
                        broken.Add($"{g.name}: 목표 {c.Slot} ≥ {c.Value} — 올리는 액션 없음");
                    else if (c.Op == CompareOp.LessOrEqual && maxDrop[s] <= 0f)
                        broken.Add($"{g.name}: 목표 {c.Slot} ≤ {c.Value} — 내리는 액션 없음");
                }
            }
            Assert.IsEmpty(broken,
                "목표 슬롯을 움직일 액션이 카탈로그에 없습니다 (플래너 픽션 효과 누락). " +
                "이대로 goal이 발동하면 Burst 잡에서 0으로 나눔 크래시:\n" + string.Join("\n", broken));
        }

        /// <summary>순수 판정 함수 자체의 계약 — 게이트웨이가 같은 규칙으로 막는다.</summary>
        [Test]
        public void HasReachableGoalSlots_RejectsUnproducibleSlot()
        {
            var goal = ScriptableObject.CreateInstance<GoalSO>();
            goal.name = "TestGoal";
            goal.GoalConditions = new[]
            {
                new SlotCondition { Slot = SlotId.MyFarmPlotCount, Op = CompareOp.GreaterOrEqual, Value = 1 },
            };

            var zero = new float[PlanningConfig.TotalSlots];
            var gain = new float[PlanningConfig.TotalSlots];
            gain[(int)SlotId.MyFarmPlotCount] = 1f;

            LogAssert.ignoreFailingMessages = true; // 에러 로그는 의도된 산출물
            Assert.IsFalse(PlannerGateway.HasReachableGoalSlots(goal, zero, zero), "생산 액션 없음 = 거부");
            Assert.IsTrue(PlannerGateway.HasReachableGoalSlots(goal, gain, zero), "생산 액션 있음 = 통과");
            LogAssert.ignoreFailingMessages = false;

            Object.DestroyImmediate(goal);
        }
    }
}
