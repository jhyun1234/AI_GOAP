using AIVillage.M0;
using NUnit.Framework;
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
        public void M2_A_AnchorPriority_FirstBuiltWins()
        {
            var world = new WorldModel(new DiscoveryService(), Config(0, 0));
            var construction = new ConstructionService(world);
            // [A, B] 우선순위 — A=후반 건물(미건설 가정), B=모닥불. 폴백(기지)은 러너 담당이라 여기선 false만 본다.
            var priority = new[] { SlotId.AtBuildSite, SlotId.CampfireBuilt };

            Assert.IsFalse(construction.TryGetFirstBuiltAnchor(priority, out _), "둘 다 미건설 → false (러너가 기지 폴백)");

            construction.Complete(FlagBuilding("모닥불", SlotId.CampfireBuilt), 3, 4);
            Assert.IsTrue(construction.TryGetFirstBuiltAnchor(priority, out Vector2Int tile));
            Assert.AreEqual(new Vector2Int(3, 4), tile, "A 미건설 → B(모닥불)로");

            construction.Complete(FlagBuilding("후반건물", SlotId.AtBuildSite), 7, 7);
            construction.TryGetFirstBuiltAnchor(priority, out tile);
            Assert.AreEqual(new Vector2Int(7, 7), tile, "A 건설 후에는 A가 우선 — 앵커 승격 (ADR-M2-2)");

            // 배열 1원소 = 기존 단일 앵커와 동일 동작 (기존 식사·여가 무변경 보증)
            Assert.IsTrue(construction.TryGetFirstBuiltAnchor(new[] { SlotId.CampfireBuilt }, out tile));
            Assert.AreEqual(new Vector2Int(3, 4), tile);
        }
    }
}
