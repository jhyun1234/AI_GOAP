using AIVillage.Core;
using AIVillage.M0;
using NUnit.Framework;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M0 월드 게이트 (명세 W3 DoD, M0-T4).
    ///   - ConstructionService: 원자적 완공 1회 (차감·플래그·이벤트 정확히 1회씩)
    ///   - 비용 부족/중복 완공 시 아무 것도 변하지 않음
    ///   - WorldModel 음수 클램프, DiscoveryService 발견·최근접·고갈 판정
    /// </summary>
    public class M0_WorldGates
    {
        private static WorldConfigSO Config(int wood, int raw)
        {
            var c = ScriptableObject.CreateInstance<WorldConfigSO>();
            c.InitialWoodStock = wood;
            c.InitialRawFoodStock = raw;
            return c;
        }

        private static BuildingSO CampfireBuilding()
        {
            var b = ScriptableObject.CreateInstance<BuildingSO>();
            b.DisplayName = "테스트모닥불";
            b.Costs = new[] { new ResourceCost { StockSlot = SlotId.WoodStock, Amount = 5 } };
            b.BuiltFlagSlot = SlotId.CampfireBuilt;
            return b;
        }

        [Test]
        public void M0_T4_Complete_AtomicExactlyOnce()
        {
            var discovery = new DiscoveryService();
            var world = new WorldModel(discovery, Config(10, 30));
            var construction = new ConstructionService(world);
            int fired = 0;
            construction.OnCompleted += (b, x, y, _) => fired++;

            Assert.IsTrue(construction.Complete(CampfireBuilding(), 3, 4));
            Assert.AreEqual(5, world.GetStock(SlotId.WoodStock), "Wood 10 - 비용 5");
            Assert.IsTrue(world.GetFlag(SlotId.CampfireBuilt));
            Assert.AreEqual(1, fired, "완공 이벤트는 정확히 1회");

            // 중복 완공 거부 — 상태 무변화
            Assert.IsFalse(construction.Complete(CampfireBuilding(), 3, 4));
            Assert.AreEqual(5, world.GetStock(SlotId.WoodStock));
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void M0_T4_Complete_InsufficientCost_NoChange()
        {
            var world = new WorldModel(new DiscoveryService(), Config(3, 0));
            var construction = new ConstructionService(world);
            int fired = 0;
            construction.OnCompleted += (b, x, y, _) => fired++;

            Assert.IsFalse(construction.Complete(CampfireBuilding(), 0, 0));
            Assert.AreEqual(3, world.GetStock(SlotId.WoodStock), "부족 시 차감 없음 (원자성)");
            Assert.IsFalse(world.GetFlag(SlotId.CampfireBuilt));
            Assert.AreEqual(0, fired);
        }

        [Test]
        public void M0_T4_WorldModel_NegativeClamp()
        {
            var world = new WorldModel(new DiscoveryService(), Config(10, 30));
            world.AddStock(SlotId.WoodStock, -999);
            Assert.AreEqual(0, world.GetStock(SlotId.WoodStock), "음수 결과는 0 클램프 (舊 B26 계승)");
            Assert.IsFalse(world.TrySpendStock(SlotId.RawFoodStock, 31), "부족 시 false");
            Assert.AreEqual(30, world.GetStock(SlotId.RawFoodStock), "실패 시 무변화");
        }

        [Test]
        public void M0_T4_Discovery_RadiusNearestDepletion()
        {
            var d = new DiscoveryService();
            var near = new ResourceNode("near", ResourceType.Wood, 5, 0, 50f);
            var far  = new ResourceNode("far",  ResourceType.Wood, 20, 0, 50f);
            d.AddResourceNode(near);
            d.AddResourceNode(far);

            Assert.IsFalse(d.HasDiscovered(ResourceType.Wood), "발견 전");

            Assert.AreEqual(1, d.DiscoverArea(0, 0, 10), "반경 10 내 1개만 발견");
            Assert.IsTrue(near.IsDiscovered);
            Assert.IsFalse(far.IsDiscovered);
            Assert.AreSame(near, d.FindNearestDiscovered(ResourceType.Wood, 0, 0));

            near.CurrentAmount = 0f; // 고갈 → 발견 노드여도 후보 제외
            Assert.IsFalse(d.HasDiscovered(ResourceType.Wood));
            Assert.IsNull(d.FindNearestDiscovered(ResourceType.Wood, 0, 0));
        }

        [Test]
        public void M0_T4_Snapshot_ReflectsWorldAndNeeds()
        {
            var d = new DiscoveryService();
            d.AddResourceNode(new ResourceNode("n", ResourceType.RawFood, 3, 3, 50f, isDiscovered: true));
            var world = new WorldModel(d, Config(10, 30));

            WorldSnapshot snap = world.BuildSnapshot(satiety: 70, fatigue: 20);
            Assert.AreEqual(10, snap.Get(SlotId.WoodStock));
            Assert.AreEqual(30, snap.Get(SlotId.RawFoodStock));
            Assert.AreEqual(70, snap.Get(SlotId.MySatiety));
            Assert.AreEqual(20, snap.Get(SlotId.MyFatigue));
            Assert.AreEqual(0,  snap.Get(SlotId.NearDiscoveredWood));
            Assert.AreEqual(1,  snap.Get(SlotId.NearDiscoveredFood));
            Assert.AreEqual(0,  snap.Get(SlotId.CampfireBuilt));
        }
    }
}
