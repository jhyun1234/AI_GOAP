using AIVillage.Core.GOAP;
using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M2 생산 체인 게이트 (명세 M2-A~E). 이 파일이 M2-T1~T4의 집이다 —
    /// M2-A 시점에는 수량형 건물(M2-T1)·최근접 질의·스냅샷 신규 슬롯·앵커 우선순위(ADR-M2-2)를 검증하고,
    /// M2-B(T2 요리 플랜)·M2-C(T3 농사 상태머신, T4 Tech Tree 게이트)가 뒤에 추가된다.
    /// </summary>
    public class M2_ProductionGates
    {
        private static WorldConfigSO Config(int wood, int raw)
        {
            var c = ScriptableObject.CreateInstance<WorldConfigSO>();
            c.InitialWoodStock = wood;
            c.InitialRawFoodStock = raw;
            return c;
        }

        /// <summary>수량형 밭 (ADR-M2-3): CountSlot=FarmPlotCount, 비용 Wood 5 (§4 제안치).</summary>
        private static BuildingSO FarmBuilding()
        {
            var b = ScriptableObject.CreateInstance<BuildingSO>();
            b.DisplayName = "테스트밭";
            b.Costs = new[] { new ResourceCost { StockSlot = SlotId.WoodStock, Amount = 5 } };
            b.IsCountable = true;
            b.CountSlot = SlotId.FarmPlotCount;
            return b;
        }

        private static BuildingSO FlagBuilding(string name, SlotId flag)
        {
            var b = ScriptableObject.CreateInstance<BuildingSO>();
            b.DisplayName = name;
            b.BuiltFlagSlot = flag;
            return b;
        }

        [Test]
        public void M2_T1_Countable_CompleteTwice_CountPositionsEvents()
        {
            var world = new WorldModel(new DiscoveryService(), Config(10, 0));
            var construction = new ConstructionService(world);
            int fired = 0;
            construction.OnCompleted += (b, x, y) => fired++;

            Assert.IsTrue(construction.Complete(FarmBuilding(), 2, 2), "1번째 완공 — 중복 거부 없음");
            Assert.IsTrue(construction.Complete(FarmBuilding(), 8, 8), "2번째 완공 — 중복 거부 없음");
            Assert.AreEqual(2, world.GetStock(SlotId.FarmPlotCount), "CountSlot = 완공 횟수");
            Assert.AreEqual(0, world.GetStock(SlotId.WoodStock), "Wood 10 - 5×2");
            Assert.AreEqual(2, fired, "완공 이벤트 2회");

            Assert.IsTrue(construction.TryGetNearestBuiltTile(SlotId.FarmPlotCount, 0, 0, out Vector2Int near),
                          "위치 목록 2개 기록");
            Assert.AreEqual(new Vector2Int(2, 2), near);

            // 비용 부족 3번째 — 원자성: 아무 것도 변하지 않음 (M0-T4 규칙 계승)
            Assert.IsFalse(construction.Complete(FarmBuilding(), 5, 5));
            Assert.AreEqual(2, world.GetStock(SlotId.FarmPlotCount));
            Assert.AreEqual(2, fired);
        }

        [Test]
        public void M2_A_NearestBuiltTile_ReturnsNearest()
        {
            var world = new WorldModel(new DiscoveryService(), Config(20, 0));
            var construction = new ConstructionService(world);
            construction.Complete(FarmBuilding(), 0, 0);
            construction.Complete(FarmBuilding(), 10, 10);

            construction.TryGetNearestBuiltTile(SlotId.FarmPlotCount, 2, 2, out Vector2Int a);
            Assert.AreEqual(new Vector2Int(0, 0), a);
            construction.TryGetNearestBuiltTile(SlotId.FarmPlotCount, 9, 9, out Vector2Int b);
            Assert.AreEqual(new Vector2Int(10, 10), b);

            Assert.IsFalse(construction.TryGetNearestBuiltTile(SlotId.CookedFoodStock, 0, 0, out _),
                           "기록 없는 슬롯은 false");
        }

        [Test]
        public void M2_A_Snapshot_NewSlots()
        {
            var world = new WorldModel(new DiscoveryService(), Config(10, 0));
            var construction = new ConstructionService(world);
            construction.Complete(FarmBuilding(), 1, 1);
            world.AddStock(SlotId.CookedFoodStock, 4);

            WorldSnapshot snap = world.BuildSnapshot(satiety: 50, fatigue: 50);
            Assert.AreEqual(4, snap.Get(SlotId.CookedFoodStock));
            Assert.AreEqual(1, snap.Get(SlotId.FarmPlotCount));
            Assert.AreEqual(0, snap.Get(SlotId.EmptyFarmPlot), "FarmService 배선(M2-C) 전까지 0");
            Assert.AreEqual(0, snap.Get(SlotId.RipeCropAvailable), "FarmService 배선(M2-C) 전까지 0");
        }

        [Test]
        public void M2_T2_Hungry_WithCookedStock_PrefersCookedOverRaw()
        {
            // 실제 에셋 + 실제 잡 (M0-T2 하네스 계승). 조리 식사는 goal 무수정으로
            // 비용(5<8)·효율(+50)의 자연 결과로 선택되어야 한다 (M2-S2, ADR-M2-6).
            var catalog = AssetDatabase.LoadAssetAtPath<ActionCatalog>("Assets/M0Config/ActionCatalog.asset");
            var goal = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_P0_Hunger.asset");
            Assert.IsNotNull(catalog); Assert.IsNotNull(goal);
            var gw = new PlannerGateway(catalog);

            var slots = new int[PlanningConfig.TotalSlots];
            slots[(int)SlotId.MySatiety] = 10;
            slots[(int)SlotId.RawFoodStock] = 5;
            slots[(int)SlotId.CookedFoodStock] = 4;
            PlannerGateway.PendingPlan pending = gw.RequestPlan(new WorldSnapshot(slots), goal);
            gw.CompleteNow(pending);
            Assert.IsTrue(gw.TryGetResult(pending, out PlanStatus status, out ActionSO[] plan, out _));

            Assert.AreEqual(PlanStatus.Success, status);
            Assert.AreEqual(2, plan.Length, "EatCookedFood ×2(비용 10, +100)가 생식 조합보다 저렴해야 함");
            foreach (ActionSO a in plan)
                Assert.AreEqual("EatCookedFood", a.name, "배고픔 플랜이 생식 대신 조리 식사를 선택 (M2-S2)");
        }

        [Test]
        public void M2_T3_Farm_StateMachine_RoundTrip_And_SingleClaim()
        {
            var farm = new FarmService(growthDays: 1.5f);
            farm.RegisterPlot(3, 3);
            farm.RegisterPlot(6, 6);
            int events = 0;
            farm.OnPlotStateChanged += _ => events++;

            Assert.IsTrue(farm.HasEmpty);
            Assert.IsFalse(farm.HasRipe);
            FarmPlot p = farm.NearestEmpty(2, 2);
            Assert.AreEqual(new Vector2Int(3, 3), p.Tile, "최근접 빈 밭");

            // 점유 경쟁 — 1인 보장 + 점유 밭은 탐색에서 제외
            Assert.IsTrue(p.TryClaim("A"));
            Assert.IsFalse(p.TryClaim("B"), "타 주민 점유 거부 (1인 점유)");
            Assert.AreEqual(new Vector2Int(6, 6), farm.NearestEmpty(2, 2).Tile, "점유된 밭은 다음 후보로");

            Assert.IsTrue(farm.TryPlant(p));
            Assert.IsFalse(farm.TryPlant(p), "빈 밭이 아니면 심기 거부");
            p.Release();

            farm.TickGrowth(1.0f);
            Assert.IsFalse(farm.HasRipe, "누적 1.0일 < 1.5일 — 아직 성장 중");
            farm.TickGrowth(0.6f);
            Assert.IsTrue(farm.HasRipe, "누적 1.6일 ≥ 1.5일 — 결실");

            FarmPlot r = farm.NearestRipe(0, 0);
            Assert.AreSame(p, r);
            Assert.IsTrue(farm.TryHarvest(r));
            Assert.AreEqual(FarmState.Empty, r.State, "수확 후 빈 밭 복귀 (재심기 가능)");
            Assert.IsFalse(farm.TryHarvest(r), "익지 않은 밭 수확 거부");

            Assert.AreEqual(3, events, "전이 이벤트: 심기·결실·수확 각 1회 (M2-D 시각 구독 계약)");
        }

        [Test]
        public void M2_C_Snapshot_EmptyRipe_FromFarmService()
        {
            // ADR-M2-4: Empty/Ripe의 유일한 원천 = FarmService — 스냅샷이 그대로 파생하는지
            var farm = new FarmService(growthDays: 1.5f);
            var world = new WorldModel(new DiscoveryService(), Config(0, 0), farm);
            Assert.AreEqual(0, world.BuildSnapshot(50, 50).Get(SlotId.EmptyFarmPlot), "밭 없음 → 0");

            farm.RegisterPlot(1, 1);
            Assert.AreEqual(1, world.BuildSnapshot(50, 50).Get(SlotId.EmptyFarmPlot), "빈 밭 존재 → 1");

            farm.TryPlant(farm.NearestEmpty(0, 0));
            WorldSnapshot growing = world.BuildSnapshot(50, 50);
            Assert.AreEqual(0, growing.Get(SlotId.EmptyFarmPlot), "재배 중 → 빈 밭 없음");
            Assert.AreEqual(0, growing.Get(SlotId.RipeCropAvailable), "아직 미성숙");

            farm.TickGrowth(2f);
            Assert.AreEqual(1, world.BuildSnapshot(50, 50).Get(SlotId.RipeCropAvailable), "결실 → 1");
        }

        [Test]
        public void M2_T4_BuildFarm_RequiresCampfire_TechTreeGate()
        {
            var goal = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_BuildFarm.asset");
            Assert.IsNotNull(goal, "Goal_BuildFarm 에셋 없음");
            var selector = new GoalSelector(new[] { goal });

            var slots = new int[PlanningConfig.TotalSlots];
            slots[(int)SlotId.WoodStock] = 50;
            slots[(int)SlotId.MySatiety] = 80;
            Assert.IsNull(selector.Select(new WorldSnapshot(slots)),
                          "모닥불 미완공이면 밭 goal 미발동 (M2-S4 Tech Tree 첫 사례)");

            slots[(int)SlotId.CampfireBuilt] = 1;
            Assert.AreEqual("Goal_BuildFarm", selector.Select(new WorldSnapshot(slots)).name,
                            "모닥불 완공 후 발동");

            slots[(int)SlotId.FarmPlotCount] = 2;
            Assert.IsNull(selector.Select(new WorldSnapshot(slots)), "목표 2개 도달 시 재발동 없음");
        }

        [Test]
        public void M2_Fix_BuildSite_AvoidsOccupiedTiles()
        {
            // 리뷰 ① 발견: 밭이 자원 노드 타일을 덮는 문제 — 링 탐색이 비점유 최근접 링을 찾는다
            var occupiedTiles = new System.Collections.Generic.HashSet<Vector2Int>
            {
                new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(-1, 0),
            };
            bool Occ(int x, int y) => occupiedTiles.Contains(new Vector2Int(x, y));

            Assert.IsTrue(BuildRunner.TryFindFreeTileNear(Occ, 0, 0, -50, 49, -50, 49, out Vector2Int tile));
            Assert.IsFalse(occupiedTiles.Contains(tile), "점유 타일 회피");
            Assert.AreEqual(1, Mathf.Max(Mathf.Abs(tile.x), Mathf.Abs(tile.y)), "반경 1 링에 빈 타일 존재 → 최근접 링 선택");

            // 반경 3 내 전부 점유면 false (러너는 Fail — 좌표 스냅 없음)
            Assert.IsFalse(BuildRunner.TryFindFreeTileNear((x, y) => true, 0, 0, -50, 49, -50, 49, out _));

            // 맵 경계 밖은 후보에서 제외
            Assert.IsTrue(BuildRunner.TryFindFreeTileNear((x, y) => false, -50, -50, -50, 49, -50, 49, out Vector2Int corner));
            Assert.GreaterOrEqual(corner.x, -50);
            Assert.GreaterOrEqual(corner.y, -50);
        }

        [Test]
        public void M2_Fix_BuildSite_ClustersWithSameKind()
        {
            // "밭은 밭 옆에" — 동종 건물이 있으면 그 인접 링에 배치 (상식적 자율, 사용자 결정 2026-07-14)
            bool OneFarm(int x, int y) => x == 10 && y == 10;
            Assert.IsTrue(BuildRunner.TryPickBuildTile(OneFarm, true, new Vector2Int(10, 10),
                new Vector2Int(0, 0), -50, 49, -50, 49, out Vector2Int tile, out bool move));
            Assert.IsTrue(move);
            Assert.AreEqual(1, Mathf.Max(Mathf.Abs(tile.x - 10), Mathf.Abs(tile.y - 10)),
                            "동종 건물 인접 링에 배치");

            // 군집 없으면 기존 동작 무변경: 비점유 제자리
            Assert.IsTrue(BuildRunner.TryPickBuildTile((x, y) => false, false, default,
                new Vector2Int(3, 3), -50, 49, -50, 49, out tile, out move));
            Assert.IsFalse(move);
            Assert.AreEqual(new Vector2Int(3, 3), tile);

            // 군집 곁 포화(반경 3) → 현재 위치 폴백 (밭 N+1개째가 막히지 않는다)
            bool NearTenFull(int x, int y) => Mathf.Max(Mathf.Abs(x - 10), Mathf.Abs(y - 10)) <= 3;
            Assert.IsTrue(BuildRunner.TryPickBuildTile(NearTenFull, true, new Vector2Int(10, 10),
                new Vector2Int(0, 0), -50, 49, -50, 49, out tile, out move));
            Assert.IsFalse(move);
            Assert.AreEqual(new Vector2Int(0, 0), tile);
        }

        [Test]
        public void M2_E_P0Goals_SkipFailureCooldown()
        {
            var hunger  = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_P0_Hunger.asset");
            var fatigue = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_P0_Fatigue.asset");
            var wood    = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_GatherWood.asset");
            Assert.IsTrue(hunger.SkipFailureCooldown, "ADR-M2-5: P0 생존 goal 2종 면제 설정");
            Assert.IsTrue(fatigue.SkipFailureCooldown, "ADR-M2-5: P0 생존 goal 2종 면제 설정");
            Assert.IsFalse(wood.SkipFailureCooldown, "P0 외 goal 남용 금지 — 공회전 방지 유지");

            Assert.IsFalse(VillagerAgent.ShouldRecordFailureCooldown(hunger, cooldownRequested: true),
                           "P0 goal은 실패 직후에도 즉시 재선택 (쿨다운 미기록)");
            Assert.IsTrue(VillagerAgent.ShouldRecordFailureCooldown(wood, cooldownRequested: true),
                          "일반 goal은 실패 쿨다운 유지");
            Assert.IsFalse(VillagerAgent.ShouldRecordFailureCooldown(wood, cooldownRequested: false),
                           "전환·복귀 중단은 원래 벌칙 없음 — 회귀 방지");
        }

        [Test]
        public void M2_A_AnchorPriority_FirstBuiltWins()
        {
            var world = new WorldModel(new DiscoveryService(), Config(0, 0));
            var construction = new ConstructionService(world);
            // [A, B] 우선순위 — A=후반 건물(미건설 가정), B=모닥불. 폴백(기지)은 러너 담당이라 여기선 false만 본다.
            var priority = new[] { SlotId.AtBuildSite, SlotId.CampfireBuilt };

            Assert.IsFalse(construction.TryGetAnchorTile(priority, 0, 0, out _), "둘 다 미건설 → false (러너가 기지 폴백)");

            construction.Complete(FlagBuilding("모닥불", SlotId.CampfireBuilt), 3, 4);
            Assert.IsTrue(construction.TryGetAnchorTile(priority, 0, 0, out Vector2Int tile));
            Assert.AreEqual(new Vector2Int(3, 4), tile, "A 미건설 → B(모닥불)로");

            construction.Complete(FlagBuilding("후반건물", SlotId.AtBuildSite), 7, 7);
            construction.TryGetAnchorTile(priority, 0, 0, out tile);
            Assert.AreEqual(new Vector2Int(7, 7), tile, "A 건설 후에는 A가 우선 — 앵커 승격 (ADR-M2-2)");

            // 배열 1원소 = 기존 단일 앵커와 동일 동작 (기존 식사·여가 무변경 보증)
            Assert.IsTrue(construction.TryGetAnchorTile(new[] { SlotId.CampfireBuilt }, 0, 0, out tile));
            Assert.AreEqual(new Vector2Int(3, 4), tile);
        }
    }
}
