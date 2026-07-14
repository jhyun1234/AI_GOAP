using AIVillage.M0;
using NUnit.Framework;
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
    }
}
