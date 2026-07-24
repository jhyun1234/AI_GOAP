using AIVillage.M0;
using NUnit.Framework;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M11-K 탈중심 마을 게이트 — 맵-비례 배치 산식(순수)과 MyHasCampfire 스냅샷·소유 배정.
    /// 핵심 = **맵이 커지면 마을·선호 거리가 비율로 따라 커진다**(사용자 지시 — 맵 확장 대비).
    /// </summary>
    public class M11_HomesteadGates
    {
        // ── 맵-비례 산식 (WorldConfigMath — 순수) ────────────────────────────────

        [Test]
        public void MapRelative_VillageRadiusScalesWithMap()
        {
            // 반경 = 맵 절반 × 비율. 맵이 커지면 반경도 비례로 커진다.
            Assert.AreEqual(40, WorldConfigMath.VillageRadius(50, 0.8f), "맵 절반 50 × 0.8 = 40");
            Assert.AreEqual(80, WorldConfigMath.VillageRadius(100, 0.8f), "맵 2배 → 반경 2배 (자동 확산)");
            Assert.AreEqual(1, WorldConfigMath.VillageRadius(0, 0.8f), "0 방어 — 최소 1");
            Assert.AreEqual(50, WorldConfigMath.VillageRadius(50, 2f), "비율 1 초과는 클램프 (맵 절반)");
        }

        [Test]
        public void MapRelative_PreferredDistScalesWithRadius()
        {
            Assert.AreEqual(4, WorldConfigMath.PreferredDist(40, 0.1f), "온순 0.1 × 반경 40 = 4 (이웃 곁)");
            Assert.AreEqual(38, WorldConfigMath.PreferredDist(40, 0.95f), "방랑벽 0.95 × 40 = 38 (외딴집)");
            Assert.AreEqual(0, WorldConfigMath.PreferredDist(40, 0f), "비율 0 = 중립(최근접)");
            // 맵이 2배가 되면 같은 성격도 2배 멀리
            Assert.AreEqual(76, WorldConfigMath.PreferredDist(80, 0.95f), "맵 2배 → 방랑벽도 2배 멀리");
        }

        [Test]
        public void MapRelative_DifferentPersonalitiesStillDistinct()
        {
            // 舊 절대값(1·9)이 아니라 비율이어도 성격별 링이 갈린다 (반경 40 기준).
            int docile = WorldConfigMath.PreferredDist(40, 0.1f);
            int stubborn = WorldConfigMath.PreferredDist(40, 0.5f);
            int wanderer = WorldConfigMath.PreferredDist(40, 0.95f);
            Assert.Less(docile, stubborn);
            Assert.Less(stubborn, wanderer);
            Assert.AreEqual(4, docile);
            Assert.AreEqual(20, stubborn);
            Assert.AreEqual(38, wanderer);
        }

        // ── MyHasCampfire 스냅샷 (소유 → 1) ──────────────────────────────────────

        [Test]
        public void MyHasCampfire_NeutralWhenNoOwnership()
        {
            var world = new WorldModel(new DiscoveryService(),
                                       ScriptableObject.CreateInstance<WorldConfigSO>());
            WorldSnapshot snap = world.BuildSnapshot(50, 10);
            Assert.AreEqual(0, snap.Get(SlotId.MyHasCampfire), "미소유 = 0 (노숙자는 조리 불가)");

            WorldSnapshot has = world.BuildSnapshot(50, 10, hasCampfire: true);
            Assert.AreEqual(1, has.Get(SlotId.MyHasCampfire), "소유 주입이 슬롯 32에 도달");
        }

        [Test]
        public void CampfireSlots_AreCorrectlyTyped()
        {
            Assert.IsTrue(SlotIds.IsNumeric(SlotId.CampfireCount), "모닥불 카운트는 수치형(소유 키)");
            Assert.IsFalse(SlotIds.IsNumeric(SlotId.MyHasCampfire), "내 모닥불 소유는 논리형");
            Assert.IsFalse(SlotIds.IsPersonalStock(SlotId.CampfireCount), "스톡 아님");
            Assert.AreEqual(31, (int)SlotId.CampfireCount, "append — 기존 인덱스 뒤");
            Assert.AreEqual(32, (int)SlotId.MyHasCampfire);
        }

        // ── OwnedBuilding = 자가 소유 배정 (집·모닥불) ────────────────────────────

        [Test]
        public void OwnedBuilding_AssignsToSelfWithoutRequest()
        {
            // BuildRunner의 자가 배정은 인스턴스 없이 직접 못 부르므로 OwnershipService 규약으로 검증:
            // OwnedBuilding 완공 = Assign(tile, CountSlot, agentId). 집·모닥불 각각 소유가 갈린다.
            var own = new OwnershipService();
            own.Assign(new Vector2Int(3, 3), SlotId.HouseCount, "A", "자가 건축");
            own.Assign(new Vector2Int(4, 3), SlotId.CampfireCount, "A", "자가 건축");

            Assert.IsTrue(own.TryGetOwned("A", SlotId.HouseCount, out _), "A는 집 소유");
            Assert.IsTrue(own.TryGetOwned("A", SlotId.CampfireCount, out _), "A는 모닥불도 소유 (별개 슬롯)");
            Assert.IsFalse(own.TryGetOwned("B", SlotId.CampfireCount, out _), "B는 미소유");
        }
    }
}
