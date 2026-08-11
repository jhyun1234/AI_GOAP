using System.Collections.Generic;
using AIVillage.Core.GOAP;
using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

using static AIVillage.Tests.EditMode.GateHelpers;

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

        private static ActionCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ActionCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, $"카탈로그 에셋 없음: {CatalogPath}");
            return catalog;
        }

        // LoadGoal·Snap·RunPlan → GateHelpers (2026-08-11 2차 감사 통합)

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
                // 컴파일러와 같은 입력 (M19-W4: 임금 철거로 플래너 효과 = 에셋 효과 그대로)
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

            // M11-K: 모닥불이 개인 수량형(IsCountable)이 됐다 → BuiltFlag 전제 없음, 재고 전제만.
            // 파생 전제: 재고(WoodStock>=5). 수량형은 중복 완공이 정상이라 미완공 전제를 생략한다.
            Assert.IsTrue(precs.Exists(c => c.Slot == SlotId.WoodStock && c.Op == CompareOp.GreaterOrEqual && c.Value == 5));
            Assert.IsFalse(precs.Exists(c => c.Slot == SlotId.CampfireBuilt), "수량형은 BuiltFlag 전제 없음");
            // 파생 효과: 재고 차감(BuildingSO) + 카운트 +1(수량형) + 소유 픽션(에셋 Effects의 MyHasCampfire Set 1)
            Assert.IsTrue(effs.Exists(e => e.Slot == SlotId.WoodStock && e.Op == EffectOp.SubClamp0 && e.Value == 5));
            Assert.IsTrue(effs.Exists(e => e.Slot == SlotId.CampfireCount && e.Op == EffectOp.Add && e.Value == 1));
            Assert.IsTrue(effs.Exists(e => e.Slot == SlotId.MyHasCampfire && e.Op == EffectOp.Set && e.Value == 1),
                          "BuildCampfire 액션 자체 Effects의 개인 모닥불 픽션 (BuildMyHouse 패턴)");
        }

        // ── M0-T2: 플래너 스모크 (실제 잡 실행) ──────────────────────────────

        [Test]
        public void M0_T2_Hungry_PlansEatRawFood()
        {
            ActionCatalog catalog = LoadCatalog();
            var gw = new PlannerGateway(catalog);

            // 포만감 10 (발동 20 이하), 몸 소지 생식 5 (M11-B 개인화).
            // 목표는 **증분 +15 = 한 끼** (2026-07-24 개정). 舊 절대 목표(포만 70)는 식량이 모자라면
            // 도달 불가라 플래너가 아무 계획도 못 세웠고, 그래서 생식을 쥐고도 한 입 못 먹고 굶었다.
            // 넉넉할 때 70까지 채우는 몫은 Goal_Snack(트리거 ≤35 → 목표 70)이 이어받는다.
            WorldSnapshot snap = Snap((SlotId.MySatiety, 10), (SlotId.MyRawFood, 5));
            (PlanStatus status, ActionSO[] plan) = RunPlan(gw, snap, LoadGoal("Goal_P0_Hunger"));

            Assert.AreEqual(PlanStatus.Success, status);
            Assert.AreEqual(1, plan.Length, "증분 목표(+15) = 생식 한 끼로 충족");
            foreach (ActionSO a in plan)
                Assert.IsInstanceOf<ConsumeActionSO>(a, $"플랜에 식사 외 액션: {a.name}");

            // 핵심 불변식 — 식량이 딱 1개여도 계획이 선다 (舊 절대 목표에선 4개 미만이면 NoSolution).
            WorldSnapshot scarce = Snap((SlotId.MySatiety, 10), (SlotId.MyRawFood, 1));
            (PlanStatus s2, ActionSO[] p2) = RunPlan(gw, scarce, LoadGoal("Goal_P0_Hunger"));
            Assert.AreEqual(PlanStatus.Success, s2, "생식 1개여도 굶지 않는다 — 증분 목표의 존재 이유");
            Assert.AreEqual(1, p2.Length);
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

            // M11-K: 모닥불 개인화 → 트리거가 MyHasHome==1 & MyHasCampfire==0. 옛 CampfireBuilt 자리.
            // 전부 만족/미발동 → null (정상 Idle). 舊 BuildStructure 루프 방지의 핵심.
            WorldSnapshot allDone = Snap((SlotId.MySatiety, 80), (SlotId.MyFatigue, 10),
                                         (SlotId.MyHasCampfire, 1), (SlotId.WoodStock, 50));
            Assert.IsNull(selector.Select(allDone), "모든 goal 만족 시 null이어야 함");

            // 집 있음 + 목재 5 + 화덕 없음 → 내 모닥불 짓기가 GatherWood보다 우선 (P50)
            WorldSnapshot canBuild = Snap((SlotId.MySatiety, 80), (SlotId.MyHasHome, 1), (SlotId.WoodStock, 5));
            Assert.AreEqual("Goal_BuildCampfire", selector.Select(canBuild).name);

            // 화덕 완공 후에는 재선택 금지 → GatherWood로 넘어감
            WorldSnapshot built = Snap((SlotId.MySatiety, 80), (SlotId.MyHasHome, 1),
                                       (SlotId.WoodStock, 5), (SlotId.MyHasCampfire, 1));
            Assert.AreEqual("Goal_GatherWood", selector.Select(built).name);

            // 배고픔은 모든 것에 우선
            WorldSnapshot hungry = Snap((SlotId.MySatiety, 15), (SlotId.WoodStock, 5));
            Assert.AreEqual("Goal_P0_Hunger", selector.Select(hungry).name);
        }

        // ── M0-T2c: 탐색 폭발 회귀 (2026-08-06 Play 관측) ────────────────────
        //
        // 관측: `M0_Villager_F: NoSolutionFound (goal=Goal_P0_Fatigue, 노드 4096/4096)`.
        // 해가 없는 게 아니라 **못 찾은** 것이었다 — 중립 배율로는 12노드에 풀린다.
        //
        // 기구: 절대 목표(피로 90→≤30)는 −20짜리 휴식 3번 = 실비용 36을 요구하는데
        // 휴리스틱은 `steps × minActionCost(5) × 0.99 = 14.85`로 41%밖에 못 본다. 거기에
        // 성격·직업 배율이 채집·농사·건설·탐험 비용을 `EffectiveCost`의 바닥값 5로 눌러붙이고,
        // 중반 마을(집·모닥불·밭·부상자)이 그 액션들의 전제를 전부 열어 준다 →
        // A*가 f<36 구간을 훑다가 4096을 소진한다. **성격×직업 조합에 따라 갈리므로
        // 주민 한 명만 마비되고, 그 주민은 우선순위 1짜리 Goal_Leisure로도 못 내려간다**
        // (SkipFailureCooldown=1이라 쿨다운조차 안 걸린다).
        //
        // 이 게이트가 지키는 것: **중립 스냅샷이 아니라 배율이 걸린 실제 조합에서** 계획이 서는가.
        // 중립만 보는 스모크(M0-T2)는 이 결함을 통과시켰다 — 그게 이 게이트가 따로 있는 이유다.
        [Test]
        public void M0_T2c_FatigueGoal_NoSearchExplosion_AcrossPersonalityAndJob()
        {
            ActionCatalog catalog = LoadCatalog();
            GoalSO fatigue = LoadGoal("Goal_P0_Fatigue");
            var rules = AssetDatabase.LoadAssetAtPath<TraitRulesSO>("Assets/M0Config/TraitRules.asset");

            Assert.IsTrue(fatigue.RelativeToCurrent,
                "피로 goal은 증분 목표여야 한다 — 절대 목표(≤30)로 되돌리면 배율 걸린 조합 91/126이 " +
                "4096노드를 소진하고 그 주민이 영구 마비된다 (2026-08-06 실측).");

            // 중반 마을 주민: 자원 발견·전역/개인/집 스톡·집·모닥불·밭·부상자까지 전부 열린 상태.
            // 적용 가능 액션이 넓어야 폭발이 재현된다 — 슬롯이 비면 분기가 좁아 그냥 통과한다.
            WorldSnapshot rich = Snap(
                (SlotId.MySatiety, 55), (SlotId.WoodStock, 30), (SlotId.RawFoodStock, 12),
                (SlotId.StoneStock, 12), (SlotId.CookedFoodStock, 4),
                (SlotId.MyRawFood, 3), (SlotId.MyCookedFood, 1),
                (SlotId.NearDiscoveredWood, 1), (SlotId.NearDiscoveredFood, 1),
                (SlotId.NearDiscoveredStone, 1),
                (SlotId.MyHasHome, 1), (SlotId.HouseCount, 2),
                (SlotId.MyHasCampfire, 1), (SlotId.CampfireCount, 1), (SlotId.CampfireBuilt, 1),
                (SlotId.MyHomeRawFood, 4), (SlotId.MyHomeCookedFood, 2),
                (SlotId.MyFarmPlotCount, 3), (SlotId.MyEmptyPlot, 2), (SlotId.MyRipeCrop, 1),
                (SlotId.FarmPlotCount, 5), (SlotId.EmptyFarmPlot, 2), (SlotId.RipeCropAvailable, 1),
                (SlotId.InjuredCount, 1), (SlotId.UntendedInjuredCount, 1),
                (SlotId.MyFoodDaysLeft, 3), (SlotId.DaysToCrisis, 5));

            PersonalitySO[] ps = LoadAllIn<PersonalitySO>("Assets/M0Config/Personalities");
            JobSO[] jobs = LoadAllIn<JobSO>("Assets/M0Config/Jobs");
            Assert.Greater(ps.Length, 0, "성격 에셋 없음");
            Assert.Greater(jobs.Length, 0, "직업 에셋 없음");

            // 절벽 바로 아래(3,688~4,004노드)에서 통과하던 조합들이 있었다 — 한계의 1/4을
            // 넘으면 아직 안 터졌어도 실패로 본다. "통과했다"와 "여유가 있다"는 다르다.
            int budget = PlanningConfig.MaxNodes / 4;
            var gw = new PlannerGateway(catalog);
            int worst = 0; string worstTag = "";

            foreach (PersonalitySO p in ps)
                foreach (JobSO j in jobs)
                {
                    float[] mult = PersonalityCost.Build(catalog, p, p.Traits, j, null, rules);
                    foreach (int f in new[] { 90, 95, 100 })
                    {
                        // 런타임과 같은 해석 경로 (VillagerAgent.ResolveRelativeGoal의 산식)
                        GoalSO resolved = Object.Instantiate(fatigue);
                        resolved.RelativeToCurrent = false;
                        resolved.GoalConditions[0].Value =
                            VillagerAgent.ResolveRelativeTarget(f, fatigue.GoalConditions[0].Value, 0);

                        WorldSnapshot snap = WithFatigue(rich, f);
                        PlannerGateway.PendingPlan pending = gw.RequestPlan(snap, resolved, mult);
                        gw.CompleteNow(pending);
                        gw.TryGetResult(pending, out PlanStatus st, out ActionSO[] plan, out int nodes);
                        Object.DestroyImmediate(resolved);

                        string tag = $"{p.name}/{j.name}@피로{f}";
                        Assert.AreNotEqual(PlanStatus.NoSolution, st,
                            $"{tag}: 계획 실패 — 쉬는 방법이 있는데 못 찾았다 ({nodes}노드 소진)");
                        Assert.IsNotNull(plan);
                        Assert.Greater(plan.Length, 0, $"{tag}: 빈 플랜");
                        if (nodes > worst) { worst = nodes; worstTag = tag; }
                    }
                }

            Assert.LessOrEqual(worst, budget,
                $"최악 {worst}노드 ({worstTag}) — 한계 {PlanningConfig.MaxNodes}의 1/4({budget})을 넘었다. " +
                "아직 안 터졌어도 절벽에 붙은 것이다 (2026-08-06 관측: 3,949노드로 통과하던 조합이 있었다).");
        }

        /// <summary>피로만 바꾼 스냅샷 사본 (나머지 슬롯 보존).</summary>
        private static WorldSnapshot WithFatigue(WorldSnapshot src, int fatigue)
        {
            var slots = new int[PlanningConfig.TotalSlots];
            for (int s = 0; s < slots.Length; s++) slots[s] = src.Get((SlotId)s);
            slots[(int)SlotId.MyFatigue] = fatigue;
            return new WorldSnapshot(slots);
        }

        private static T[] LoadAllIn<T>(string dir) where T : Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { dir });
            var list = new List<T>(guids.Length);
            foreach (string g in guids)
                list.Add(AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)));
            return list.ToArray();
        }
    }
}
