using AIVillage.Core.GOAP;
using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M3 기반 보강 게이트 (명세 M3-A~F). 이 파일이 M3-T1~T5의 집이다 —
    /// M3-A 시점에는 앵커 일반화(수량형+최근접, ADR-M3-1)를 검증하고
    /// M3-B(수확 연속 플랜)·M3-C(통행 에셋)·M3-E(작업 클레임)가 뒤에 추가된다.
    /// </summary>
    public class M3_FoundationGates
    {
        private static WorldConfigSO Config(int wood, int raw)
        {
            var c = ScriptableObject.CreateInstance<WorldConfigSO>();
            c.InitialWoodStock = wood;
            c.InitialRawFoodStock = raw;
            return c;
        }

        /// <summary>수량형 대역 — HouseCount 슬롯은 M3-D에서 생기므로 FarmPlotCount로 메커니즘을 검증한다.</summary>
        private static BuildingSO Countable()
        {
            var b = ScriptableObject.CreateInstance<BuildingSO>();
            b.DisplayName = "테스트수량형";
            b.IsCountable = true;
            b.CountSlot = SlotId.FarmPlotCount;
            return b;
        }

        private static BuildingSO Campfire()
        {
            var b = ScriptableObject.CreateInstance<BuildingSO>();
            b.DisplayName = "테스트모닥불";
            b.BuiltFlagSlot = SlotId.CampfireBuilt;
            return b;
        }

        [Test]
        public void M3_T1_AnchorTile_CountableNearest_ThenSingleFallback()
        {
            var world = new WorldModel(new DiscoveryService(), Config(0, 0));
            var construction = new ConstructionService(world);
            var priority = new[] { SlotId.FarmPlotCount, SlotId.CampfireBuilt };

            // 전부 미완공 → false (러너가 기지 폴백)
            Assert.IsFalse(construction.TryGetAnchorTile(priority, 0, 0, out _));

            // 수량형 0개 + 모닥불만 → 다음 순위(단일형)로
            construction.Complete(Campfire(), 5, 5);
            Assert.IsTrue(construction.TryGetAnchorTile(priority, 0, 0, out Vector2Int tile));
            Assert.AreEqual(new Vector2Int(5, 5), tile, "수량형 미건설 → 단일형 폴백");

            // 수량형 2개(원근) → 목록 앞 순위 + 최근접 (ADR-M3-1)
            construction.Complete(Countable(), 2, 2);
            construction.Complete(Countable(), 20, 20);
            construction.TryGetAnchorTile(priority, 0, 0, out tile);
            Assert.AreEqual(new Vector2Int(2, 2), tile, "가까운 쪽 선택");
            construction.TryGetAnchorTile(priority, 19, 19, out tile);
            Assert.AreEqual(new Vector2Int(20, 20), tile, "위치가 바뀌면 최근접도 바뀜 — 조용한 기지 폴백 함정 해소");
        }

        [Test]
        public void M3_T2_RipeCount_MultipleHarvestsInOnePlan()
        {
            // ADR-M3-2: Ripe가 개수라서 익은 밭 3개 → 한 플랜에 수확 3회 (재계획 폭주 해소, M3-S3)
            var catalog = AssetDatabase.LoadAssetAtPath<ActionCatalog>("Assets/M0Config/ActionCatalog.asset");
            var goal = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_HarvestCrop.asset");
            Assert.IsNotNull(catalog); Assert.IsNotNull(goal);
            var gw = new PlannerGateway(catalog);

            var slots = new int[PlanningConfig.TotalSlots];
            slots[(int)SlotId.MySatiety] = 80;
            slots[(int)SlotId.RipeCropAvailable] = 3;
            PlannerGateway.PendingPlan pending = gw.RequestPlan(new WorldSnapshot(slots), goal);
            gw.CompleteNow(pending);
            Assert.IsTrue(gw.TryGetResult(pending, out PlanStatus status, out ActionSO[] plan, out _));

            Assert.AreEqual(PlanStatus.Success, status);
            Assert.AreEqual(3, plan.Length, "익은 밭 3개 → HarvestCrop ×3 연속 플랜");
            foreach (ActionSO a in plan)
                Assert.AreEqual("HarvestCrop", a.name);
        }

        [Test]
        public void M3_T2b_Snapshot_CountsFromFarmService()
        {
            var farm = new FarmService(growthDays: 1.5f);
            var world = new WorldModel(new DiscoveryService(), Config(0, 0), farm);
            farm.RegisterPlot(1, 1);
            farm.RegisterPlot(2, 2);
            farm.RegisterPlot(3, 3);
            Assert.AreEqual(3, world.BuildSnapshot(50, 50).Get(SlotId.EmptyFarmPlot), "빈 밭 개수 반영");

            farm.TryPlant(farm.NearestEmpty(0, 0));
            farm.TryPlant(farm.NearestEmpty(0, 0));
            farm.TickGrowth(2f);
            WorldSnapshot snap = world.BuildSnapshot(50, 50);
            Assert.AreEqual(1, snap.Get(SlotId.EmptyFarmPlot), "빈 1 (심은 2 제외)");
            Assert.AreEqual(2, snap.Get(SlotId.RipeCropAvailable), "익은 2");
        }

        [Test]
        public void M3_T4_ExistingBuildings_DoNotBlockMovement()
        {
            // ADR-M3-3: 통행 차단은 건물 속성 — 기존 건물(모닥불·밭)은 밟고 지나다닌다 (동작 무변경 보증)
            var campfire = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/Campfire.asset");
            var farm = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/FarmPlot.asset");
            Assert.IsNotNull(campfire); Assert.IsNotNull(farm);
            Assert.IsFalse(campfire.BlocksMovement, "모닥불은 통행 가능 유지");
            Assert.IsFalse(farm.BlocksMovement, "밭은 통행 가능 유지");
        }

        [Test]
        public void M3_T4b_StandTile_AdjacentButNotOnBuildSite()
        {
            // 차단 건물 건설자는 자기가 만든 벽 위에 설 수 없다 — 인접 빈 칸 선택 (순수 게이트)
            var site = new Vector2Int(5, 5);
            Assert.IsTrue(BuildRunner.TryPickStandTile((x, y) => false, site, -50, 49, -50, 49, out Vector2Int stand));
            Assert.AreNotEqual(site, stand, "건설 타일 자신은 제외");
            Assert.AreEqual(1, Mathf.Max(Mathf.Abs(stand.x - 5), Mathf.Abs(stand.y - 5)), "인접 링에서 선택");

            // 주변 전부 점유(건설 타일 포함) → false (좌표 스냅 없이 실패)
            Assert.IsFalse(BuildRunner.TryPickStandTile((x, y) => true, site, -50, 49, -50, 49, out _));
        }

        [Test]
        public void M3_T5_MaxWorkers_ClaimLimitsSelection()
        {
            var goal = ScriptableObject.CreateInstance<GoalSO>();
            goal.name = "테스트클레임";
            goal.Priority = 10;
            goal.MaxWorkers = 1;
            goal.GoalConditions = new[]
            {
                new SlotCondition { Slot = SlotId.WoodStock, Op = CompareOp.GreaterOrEqual, Value = 999 },
            };
            var selector = new GoalSelector(new[] { goal });
            var snap = new WorldSnapshot(new int[PlanningConfig.TotalSlots]);

            Assert.AreEqual(goal, selector.Select(snap), "정원 여유 → 선택");
            selector.Claim(goal);
            Assert.IsNull(selector.Select(snap), "정원 1 가득 → 스킵 (ADR-M3-4)");
            selector.Release(goal);
            Assert.AreEqual(goal, selector.Select(snap), "해제 후 재선택");

            // 이중 해제 → 음수 잠김 없음: 0 클램프라 클레임 1회로 다시 가득이어야 한다
            selector.Release(goal);
            selector.Release(goal);
            selector.Claim(goal);
            Assert.IsNull(selector.Select(snap), "이중 Release에도 카운트 0 클램프 보증");
        }

        [Test]
        public void M3_T5b_MaxWorkers_AssetPolicy()
        {
            // ADR-M3-4: P0 생존·명령은 무제한(0), 건설·비축 goal은 1
            foreach (string free in new[] { "Goal_P0_Hunger", "Goal_P0_Fatigue", "Order_ChopWood", "Order_MineStone", "Order_HarvestBerries" })
            {
                var g = AssetDatabase.LoadAssetAtPath<GoalSO>($"Assets/M0Config/Goals/{free}.asset");
                Assert.IsNotNull(g, $"{free} 에셋 없음");
                Assert.AreEqual(0, g.MaxWorkers, $"{free}: 생존·명령은 인원 제한 금지");
            }
            foreach (string limited in new[] { "Goal_BuildFarm", "Goal_ExpandFarm", "Goal_CookAhead" })
            {
                var g = AssetDatabase.LoadAssetAtPath<GoalSO>($"Assets/M0Config/Goals/{limited}.asset");
                Assert.IsNotNull(g, $"{limited} 에셋 없음");
                Assert.AreEqual(1, g.MaxWorkers, $"{limited}: 초과 달성 방지 정원 1");
            }
        }

        [Test]
        public void M3_T3_ExpandFarm_TriggersOnFoodPressure()
        {
            var goal = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_ExpandFarm.asset");
            Assert.IsNotNull(goal, "Goal_ExpandFarm 에셋 없음");
            var selector = new GoalSelector(new[] { goal });

            var slots = new int[PlanningConfig.TotalSlots];
            slots[(int)SlotId.MySatiety] = 80;
            slots[(int)SlotId.CampfireBuilt] = 1;
            slots[(int)SlotId.FarmPlotCount] = 2;
            slots[(int)SlotId.RawFoodStock] = 8;
            Assert.AreEqual("Goal_ExpandFarm", selector.Select(new WorldSnapshot(slots)).name,
                            "식량 압박(생식 8 ≤ 10) → 밭 확장 발동");

            slots[(int)SlotId.RawFoodStock] = 30;
            Assert.IsNull(selector.Select(new WorldSnapshot(slots)), "식량 여유 → 미발동");

            slots[(int)SlotId.RawFoodStock] = 8;
            slots[(int)SlotId.FarmPlotCount] = 4;
            Assert.IsNull(selector.Select(new WorldSnapshot(slots)), "목표 4개 도달 → 재발동 없음");
        }
    }
}
