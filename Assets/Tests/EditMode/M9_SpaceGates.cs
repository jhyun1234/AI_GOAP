using AIVillage.M0;
using NUnit.Framework;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M9 공간 축 게이트 (명세 M9-A~E). 이 파일이 M9-T1(구역)의 집이다 — 이후 M9-T4(N명 대화)가
    /// 합류한다. 파괴 문(M9-T2)·재해(M9-T3)는 별도 파일.
    /// M9-T1은 ZoneService의 앵커 확정 결정성과 BuildRunner 구역 배치(군집 대체)를 검증한다.
    /// </summary>
    public class M9_SpaceGates
    {
        /// <summary>수량형 + 구역 반경 건물 (밭 상당).</summary>
        private static BuildingSO ZoneBuilding(int radius)
        {
            var b = ScriptableObject.CreateInstance<BuildingSO>();
            b.DisplayName = "구역밭";
            b.IsCountable = true;
            b.CountSlot = SlotId.FarmPlotCount;
            b.ZoneRadius = radius;
            return b;
        }

        [Test]
        public void M9_T1_Zone_FirstBuilt_EstablishesAnchor_SecondUnchanged()
        {
            var zones = new ZoneService();
            int fired = 0;
            Vector2Int firedAnchor = default;
            int firedRadius = 0;
            zones.OnZoneEstablished += (slot, a, r) => { fired++; firedAnchor = a; firedRadius = r; };

            BuildingSO b = ZoneBuilding(3);
            zones.NotifyBuilt(b, 10, 10);
            Assert.AreEqual(1, fired, "첫 완공 = 구역 확정 이벤트 1회");
            Assert.AreEqual(new Vector2Int(10, 10), firedAnchor, "앵커 = 첫 완공 타일 (ADR-M9-2)");
            Assert.AreEqual(3, firedRadius);

            // 두 번째 완공은 앵커를 바꾸지 않는다 (이벤트도 없음)
            zones.NotifyBuilt(b, 20, 20);
            Assert.AreEqual(1, fired, "두 번째 완공은 재확정 없음");
            Assert.IsTrue(zones.TryGetZone(SlotId.FarmPlotCount, out Vector2Int anchor, out int radius));
            Assert.AreEqual(new Vector2Int(10, 10), anchor, "앵커 불변");
            Assert.AreEqual(3, radius);
        }

        [Test]
        public void M9_T1_Zone_NonCountable_OrZeroRadius_NoZone()
        {
            var zones = new ZoneService();

            // 반경 0 = 구역 없음 (기존 에셋 무수정 경로)
            zones.NotifyBuilt(ZoneBuilding(0), 5, 5);
            Assert.IsFalse(zones.TryGetZone(SlotId.FarmPlotCount, out _, out _), "ZoneRadius 0 → 미확정");

            // 단일형은 구역 대상 아님
            var single = ScriptableObject.CreateInstance<BuildingSO>();
            single.IsCountable = false;
            single.ZoneRadius = 3;
            single.CountSlot = SlotId.HouseCount;
            zones.NotifyBuilt(single, 5, 5);
            Assert.IsFalse(zones.TryGetZone(SlotId.HouseCount, out _, out _), "단일형 → 미확정");
        }

        [Test]
        public void M9_T1_Zone_Contains_UndeterminedIsFree_ThenChebyshev()
        {
            var zones = new ZoneService();
            // 미확정 구역은 어디든 자유 (M9-A ⚠️③ — 첫 건설을 막지 않는다)
            Assert.IsTrue(zones.Contains(SlotId.FarmPlotCount, 99, 99), "미확정 = true");

            zones.NotifyBuilt(ZoneBuilding(3), 10, 10);
            Assert.IsTrue(zones.Contains(SlotId.FarmPlotCount, 13, 13), "반경 3 경계 = 안");
            Assert.IsTrue(zones.Contains(SlotId.FarmPlotCount, 7, 10), "반경 3 경계 = 안");
            Assert.IsFalse(zones.Contains(SlotId.FarmPlotCount, 14, 10), "반경 3 밖 = 밖");
        }

        [Test]
        public void M9_T1_TryPickBuildTile_ZonePath_WithinRadius_FullFails()
        {
            // 구역 있음: 앵커 중심 반경 내 빈 타일 반환 + needMove true
            bool AnchorOccupied(int x, int y) => x == 10 && y == 10; // 앵커에 밭 존재
            Assert.IsTrue(BuildRunner.TryPickBuildTile(AnchorOccupied, hasZone: true,
                zoneAnchor: new Vector2Int(10, 10), zoneRadius: 3, agentTile: new Vector2Int(0, 0),
                -50, 49, -50, 49, out Vector2Int tile, out bool needMove));
            Assert.IsTrue(needMove, "구역 타일로 이동 필요");
            Assert.LessOrEqual(Mathf.Max(Mathf.Abs(tile.x - 10), Mathf.Abs(tile.y - 10)), 3,
                "반환 타일은 반드시 구역 안 (구역 밖 미반환 — 게이트 핵심)");

            // 구역 만원 → false (곁 확장 폴백 없음 = 소프트 상한, M9-A ⚠️②)
            bool ZoneFull(int x, int y) => Mathf.Max(Mathf.Abs(x - 10), Mathf.Abs(y - 10)) <= 3;
            Assert.IsFalse(BuildRunner.TryPickBuildTile(ZoneFull, hasZone: true,
                zoneAnchor: new Vector2Int(10, 10), zoneRadius: 3, agentTile: new Vector2Int(0, 0),
                -50, 49, -50, 49, out _, out _), "구역 만원이면 실패 — 구역 밖으로 새지 않는다");
        }

        [Test]
        public void M9_T1_TryPickBuildTile_UndeterminedZone_InPlacePath()
        {
            // 구역 미확정(hasZone=false) → 기존 제자리 경로 (첫 밭이 여기서 완공되어 앵커가 된다)
            Assert.IsTrue(BuildRunner.TryPickBuildTile((x, y) => false, hasZone: false,
                zoneAnchor: default, zoneRadius: 0, agentTile: new Vector2Int(3, 3),
                -50, 49, -50, 49, out Vector2Int tile, out bool needMove));
            Assert.IsFalse(needMove, "미확정 = 제자리 (기존 동작)");
            Assert.AreEqual(new Vector2Int(3, 3), tile);

            // 제자리 점유면 곁 링 폴백 (SEARCH_RADIUS)
            bool HereOccupied(int x, int y) => x == 3 && y == 3;
            Assert.IsTrue(BuildRunner.TryPickBuildTile(HereOccupied, hasZone: false,
                zoneAnchor: default, zoneRadius: 0, agentTile: new Vector2Int(3, 3),
                -50, 49, -50, 49, out tile, out needMove));
            Assert.IsTrue(needMove, "제자리 점유 → 곁 이동");
            Assert.AreEqual(1, Mathf.Max(Mathf.Abs(tile.x - 3), Mathf.Abs(tile.y - 3)), "반경 1 링");
        }
    }
}
