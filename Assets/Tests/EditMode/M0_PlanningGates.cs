using System.Collections.Generic;
using AIVillage.Core.GOAP;
using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M0 플래닝 게이트 (명세 W2 DoD).
    ///   M0-T1  : 카탈로그 → GOAPActionDef 컴파일 라운드트립 정합
    ///   M0-T2  : 배고픔/목재 스냅샷 플래너 스모크 (실제 에셋 + 실제 잡)
    ///   M0-T2b : GoalSelector — 이미 만족된 goal 스킵 (舊 NoSolutionFound 루프 방지)
    /// 실제 M0Config 에셋을 로드하므로 에셋 수치 오기입도 함께 잡는다.
    /// </summary>
    public class M0_PlanningGates
    {
        private const string CatalogPath   = "Assets/M0Config/ActionCatalog.asset";
        private const string GoalDir       = "Assets/M0Config/Goals/";

        private static ActionCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ActionCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, $"카탈로그 에셋 없음: {CatalogPath}");
            return catalog;
        }

        private static GoalSO LoadGoal(string name)
        {
            var goal = AssetDatabase.LoadAssetAtPath<GoalSO>($"{GoalDir}{name}.asset");
            Assert.IsNotNull(goal, $"Goal 에셋 없음: {name}");
            return goal;
        }

        /// <summary>지정 슬롯만 세팅한 스냅샷 생성. 나머지는 0.</summary>
        private static WorldSnapshot Snap(params (SlotId slot, int value)[] pairs)
        {
            var slots = new int[PlanningConfig.TotalSlots];
            foreach ((SlotId slot, int value) in pairs) slots[(int)slot] = value;
            return new WorldSnapshot(slots);
        }

        private static (PlanStatus status, ActionSO[] plan) RunPlan(PlannerGateway gw, WorldSnapshot snap, GoalSO goal)
        {
            PlannerGateway.PendingPlan pending = gw.RequestPlan(snap, goal);
            Assert.IsNotNull(pending, "RequestPlan이 null을 반환했습니다.");
            gw.CompleteNow(pending);
            bool done = gw.TryGetResult(pending, out PlanStatus status, out ActionSO[] plan, out _);
            Assert.IsTrue(done, "CompleteNow 이후에도 TryGetResult가 false입니다.");
            return (status, plan);
        }

        // ── M0-T1: 컴파일 라운드트립 ─────────────────────────────────────────

        [Test]
        public void M0_T1_CompileRoundTrip_DefsMatchAssets()
        {
            ActionCatalog catalog = LoadCatalog();
            GOAPActionDef[] defs = ActionCompiler.CompileManaged(catalog);
            Assert.AreEqual(catalog.Actions.Length, defs.Length);

            var precs = new List<SlotCondition>();
            var effs  = new List<SlotEffect>();

            for (int i = 0; i < defs.Length; i++)
            {
                ActionSO so = catalog.Actions[i];
                precs.Clear(); effs.Clear();
                so.CollectPreconditions(precs);
                so.CollectEffects(effs);

                Assert.AreEqual(i, defs[i].ActionStringHash, $"{so.name}: 인덱스 신원(ADR-M0-6) 불일치");
                Assert.AreEqual(so.BaseCost, defs[i].BaseCost, 0.001f, $"{so.name}: BaseCost 불일치");
                Assert.AreEqual(precs.Count, defs[i].PrecCount, $"{so.name}: 전제 수 불일치");
                Assert.AreEqual(effs.Count, defs[i].EffectCount, $"{so.name}: 효과 수 불일치");

                for (int p = 0; p < precs.Count; p++)
                {
                    (int s, int v, int op) = ActionCompiler.GetPrec(defs[i], p);
                    Assert.AreEqual((int)precs[p].Slot, s, $"{so.name} 전제{p} 슬롯");
                    Assert.AreEqual(precs[p].Value, v, $"{so.name} 전제{p} 값");
                    Assert.AreEqual((int)precs[p].Op, op, $"{so.name} 전제{p} 연산");
                }
                for (int e = 0; e < effs.Count; e++)
                {
                    (int s, int v, int op) = ActionCompiler.GetEff(defs[i], e);
                    Assert.AreEqual((int)effs[e].Slot, s, $"{so.name} 효과{e} 슬롯");
                    Assert.AreEqual(effs[e].Value, v, $"{so.name} 효과{e} 값");
                    Assert.AreEqual((int)effs[e].Op, op, $"{so.name} 효과{e} 연산");
                }
            }
        }

        [Test]
        public void M0_T1_BuildAction_DerivesCostFromBuildingSO()
        {
            ActionCatalog catalog = LoadCatalog();
            var build = AssetDatabase.LoadAssetAtPath<BuildActionSO>("Assets/M0Config/Actions/BuildCampfire.asset");
            Assert.IsNotNull(build);
            Assert.IsNotNull(build.Building, "BuildCampfire에 BuildingSO 미연결");

            var precs = new List<SlotCondition>();
            var effs  = new List<SlotEffect>();
            build.CollectPreconditions(precs);
            build.CollectEffects(effs);

            // 파생 전제: 미완공(CampfireBuilt==0) + 재고(WoodStock>=5)
            Assert.IsTrue(precs.Exists(c => c.Slot == SlotId.CampfireBuilt && c.Op == CompareOp.Equal && c.Value == 0));
            Assert.IsTrue(precs.Exists(c => c.Slot == SlotId.WoodStock && c.Op == CompareOp.GreaterOrEqual && c.Value == 5));
            // 파생 효과: 재고 차감 + 완공 플래그
            Assert.IsTrue(effs.Exists(e => e.Slot == SlotId.WoodStock && e.Op == EffectOp.SubClamp0 && e.Value == 5));
            Assert.IsTrue(effs.Exists(e => e.Slot == SlotId.CampfireBuilt && e.Op == EffectOp.Set && e.Value == 1));
        }

        // ── M0-T2: 플래너 스모크 (실제 잡 실행) ──────────────────────────────

        [Test]
        public void M0_T2_Hungry_PlansEatRawFood()
        {
            ActionCatalog catalog = LoadCatalog();
            var gw = new PlannerGateway(catalog);

            // 포만감 10 (발동 20 이하), 몸 소지 생식 5 (M11-B 개인화) → 목표 70: +15 × 4회 식사
            WorldSnapshot snap = Snap((SlotId.MySatiety, 10), (SlotId.MyRawFood, 5));
            (PlanStatus status, ActionSO[] plan) = RunPlan(gw, snap, LoadGoal("Goal_P0_Hunger"));

            Assert.AreEqual(PlanStatus.Success, status);
            Assert.AreEqual(4, plan.Length, "70 도달에 EatRawFood 4회가 최적");
            foreach (ActionSO a in plan)
                Assert.IsInstanceOf<ConsumeActionSO>(a, $"플랜에 식사 외 액션: {a.name}");
        }

        [Test]
        public void M0_T2_NoWoodUndiscovered_PlansExploreThenChop()
        {
            ActionCatalog catalog = LoadCatalog();
            var gw = new PlannerGateway(catalog);

            // 목재 0, 미발견 → 목표 30: Explore 선행 + ChopWood(+10) × 3
            WorldSnapshot snap = Snap((SlotId.MySatiety, 70));
            (PlanStatus status, ActionSO[] plan) = RunPlan(gw, snap, LoadGoal("Goal_GatherWood"));

            Assert.AreEqual(PlanStatus.Success, status);
            Assert.AreEqual(4, plan.Length);
            Assert.IsInstanceOf<ExploreActionSO>(plan[0], "발견 체인: 첫 액션은 Explore여야 함");
            for (int i = 1; i < plan.Length; i++)
                Assert.IsInstanceOf<GatherActionSO>(plan[i]);
        }

        [Test]
        public void M0_T2_GoalAlreadyMet_ReturnsAlreadySatisfied()
        {
            ActionCatalog catalog = LoadCatalog();
            var gw = new PlannerGateway(catalog);

            WorldSnapshot snap = Snap((SlotId.WoodStock, 50));
            (PlanStatus status, ActionSO[] plan) = RunPlan(gw, snap, LoadGoal("Goal_GatherWood"));

            Assert.AreEqual(PlanStatus.AlreadySatisfied, status);
            Assert.AreEqual(0, plan.Length);
        }

        // ── M0-T2b: GoalSelector 이미 만족 스킵 ─────────────────────────────

        [Test]
        public void M0_T2b_Selector_SkipsSatisfiedAndUntriggered()
        {
            GoalSO[] goals =
            {
                LoadGoal("Goal_P0_Hunger"), LoadGoal("Goal_P0_Fatigue"),
                LoadGoal("Goal_BuildCampfire"), LoadGoal("Goal_GatherWood"),
            };
            var selector = new GoalSelector(goals);

            // 전부 만족/미발동 → null (정상 Idle). 舊 BuildStructure 루프 방지의 핵심.
            WorldSnapshot allDone = Snap((SlotId.MySatiety, 80), (SlotId.MyFatigue, 10),
                                         (SlotId.CampfireBuilt, 1), (SlotId.WoodStock, 50));
            Assert.IsNull(selector.Select(allDone), "모든 goal 만족 시 null이어야 함");

            // 목재 5 + 미완공 → BuildCampfire가 GatherWood보다 우선
            WorldSnapshot canBuild = Snap((SlotId.MySatiety, 80), (SlotId.WoodStock, 5));
            Assert.AreEqual("Goal_BuildCampfire", selector.Select(canBuild).name);

            // 완공 후에는 BuildCampfire 재선택 금지 → GatherWood로 넘어감
            WorldSnapshot built = Snap((SlotId.MySatiety, 80), (SlotId.WoodStock, 5), (SlotId.CampfireBuilt, 1));
            Assert.AreEqual("Goal_GatherWood", selector.Select(built).name);

            // 배고픔은 모든 것에 우선
            WorldSnapshot hungry = Snap((SlotId.MySatiety, 15), (SlotId.WoodStock, 5));
            Assert.AreEqual("Goal_P0_Hunger", selector.Select(hungry).name);
        }
    }
}
