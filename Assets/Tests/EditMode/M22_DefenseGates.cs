using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M22 방어 건설 게이트 (Docs/M22_방어건설_실행명세서.md). 이 파일이 M22-T의 집이다 —
    /// W1(개체별 통행 규칙, ADR-M22-1)부터 시작해 W2(데이터 층)·W5(공성·내구도)가 뒤에 추가된다.
    /// </summary>
    public class M22_DefenseGates
    {
        [Test]
        public void M22_T1_PassageRules_ShippedBuildings()
        {
            // ADR-M22-1: 개체별 통행 규칙은 순수 함수 한 쌍이 단일 소유한다.
            // 집·울타리 = 양쪽 차단 / 문 = 위협만 차단 (두 답이 갈라지는 유일한 배포 에셋, ADR-M22-5)
            // / 모닥불·밭·관개 = 양쪽 통행.
            foreach (string name in new[] { "House", "Fence" })
            {
                var blocker = AssetDatabase.LoadAssetAtPath<BuildingSO>($"Assets/M0Config/Buildings/{name}.asset");
                Assert.IsNotNull(blocker, $"{name} 에셋 없음");
                Assert.IsTrue(M0SimulationLoop.BlocksVillagerPassage(blocker), $"{name}은(는) 주민 통행 차단");
                Assert.IsTrue(M0SimulationLoop.BlocksThreatPassage(blocker), $"{name}은(는) 위협 통행도 차단");
            }

            var gate = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/Gate.asset");
            Assert.IsNotNull(gate, "Gate 에셋 없음");
            Assert.IsFalse(M0SimulationLoop.BlocksVillagerPassage(gate), "문은 주민 통행 가능 (ADR-M22-5)");
            Assert.IsTrue(M0SimulationLoop.BlocksThreatPassage(gate), "문은 위협 통행 차단 — 이 비대칭이 문의 존재 이유");

            foreach (string name in new[] { "Campfire", "FarmPlot", "Irrigation" })
            {
                var b = AssetDatabase.LoadAssetAtPath<BuildingSO>($"Assets/M0Config/Buildings/{name}.asset");
                Assert.IsNotNull(b, $"{name} 에셋 없음");
                Assert.IsFalse(M0SimulationLoop.BlocksVillagerPassage(b), $"{name}은(는) 주민 통행 가능 유지");
                Assert.IsFalse(M0SimulationLoop.BlocksThreatPassage(b), $"{name}은(는) 위협 통행 가능 유지");
            }
        }

        [Test]
        public void M22_T2_ShippedDefenseBuildings_DataCoherent()
        {
            // W2 데이터 층 검산 — 내구도·비용·플래그가 배포 에셋에서 일관적인가 (M21_T6 동형).
            var fence = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/Fence.asset");
            var gate = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/Gate.asset");
            Assert.IsNotNull(fence); Assert.IsNotNull(gate);

            foreach (var b in new[] { fence, gate })
            {
                Assert.Greater(b.MaxDurability, 0f, $"{b.name}: 방어 시설은 내구도를 가진다 (ADR-M22-3)");
                Assert.Greater(b.RepairCost, 0, $"{b.name}: 수리는 공짜가 아니다 — Wood 소모가 수리 노동의 무게");
                Assert.IsTrue(b.IsCountable, $"{b.name}: 방어 시설은 수량형 (타일 키 상태의 전제)");
                Assert.IsTrue(b.PlaceOnDefensePlan, $"{b.name}: 배치는 방어 계획이 정한다 (W3)");
                Assert.IsFalse(b.BlocksMovement && b.BlocksThreatMovement, $"{b.name}: 통행 플래그 배타");
                Assert.IsFalse(b.OwnedBuilding, $"{b.name}: 방어는 공용이다 (ADR-M20-9 전이 통로) — 개인 소유 금지");
                Assert.Greater(b.Costs.Length, 0, $"{b.name}: 건설 비용 0은 노동이 아니다");
            }
            Assert.AreEqual(SlotId.FenceCount, fence.CountSlot, "울타리 카운트 슬롯");
            Assert.AreEqual(SlotId.GateCount, gate.CountSlot, "문 카운트 슬롯");
            // 문이 약점: 내구도가 울타리보다 낮다 — 위협 최근접 선정에서 자연히 표적이 되는 날의 근거
            Assert.Less(gate.MaxDurability, fence.MaxDurability, "문 내구도 < 울타리 내구도 (§3 수치 관계)");
        }

        [Test]
        public void M22_T3_Perimeter_DeterministicRingWithGateNearBase()
        {
            // 둘레는 결정적 (같은 입력 = 같은 목록·같은 순서) — W3 계획의 재현성 (게이트가 씬 없이 검산)
            var anchor = new Vector2Int(10, -5);
            var a = DefenseService.PerimeterTiles(anchor, 5);
            var b = DefenseService.PerimeterTiles(anchor, 5);
            Assert.AreEqual(8 * 5, a.Count, "반경 5 둘레 = 40타일 (11×11 경계)");
            CollectionAssert.AreEqual(a, b, "같은 앵커·반경이면 언제나 같은 목록 (결정적)");
            foreach (Vector2Int t in a)
                Assert.AreEqual(5, Mathf.Max(Mathf.Abs(t.x - anchor.x), Mathf.Abs(t.y - anchor.y)),
                    "둘레 타일의 체비쇼프 거리 = 반경 (안쪽·바깥쪽 오염 없음)");

            // 문 = 기지 최근접 (맨해튼) — 기지 (0,0)에서 최근접은 좌상 모서리 (5,0): 거리 5.
            // (왼변 중앙 (5,-5)는 거리 10 — 첫 기대값의 검산 오류를 게이트가 잡았다. 코드가 옳았다.)
            Vector2Int gateTile = DefenseService.PickGateTile(a, Vector2Int.zero);
            Assert.AreEqual(new Vector2Int(5, 0), gateTile, "문 자리 = 기지 최근접 둘레 타일");
        }

        [Test]
        public void M22_T3b_EstablishPlan_FiltersAndConsumes()
        {
            var d = new DefenseService();
            var anchor = new Vector2Int(0, 0);
            // (2,-2) 하나만 막힘 (기존 건물 가정) — 계획에서 제외돼야 한다
            d.EstablishPlan(anchor, 2, new Vector2Int(0, -10), (x, y) => !(x == 2 && y == -2));
            Assert.IsTrue(d.HasPlan);
            Assert.AreEqual(8 * 2 - 1, d.PlannedCount, "막힌 타일 1개 제외 (울타리 14 + 문 1)");
            Assert.IsTrue(d.PlannedGateTile.HasValue);
            Assert.AreEqual(new Vector2Int(0, -2), d.PlannedGateTile.Value, "문 = 기지(남쪽) 최근접");

            // 완공 차감 — 방어 계획 건물만 (PlaceOnDefensePlan)
            var fence = ScriptableObject.CreateInstance<BuildingSO>();
            fence.PlaceOnDefensePlan = true;
            var house = ScriptableObject.CreateInstance<BuildingSO>();
            int before = d.PlannedCount;
            d.NotifyBuilt(house, -2, -2);
            Assert.AreEqual(before, d.PlannedCount, "방어 계획 밖 건물(집)은 계획을 건드리지 않는다");
            d.NotifyBuilt(fence, -2, -2);
            Assert.AreEqual(before - 1, d.PlannedCount, "울타리 완공 = 계획 1 차감");
            d.NotifyBuilt(fence, 0, -2);
            Assert.IsFalse(d.PlannedGateTile.HasValue, "문 자리 완공 = 문 계획 소진");

            // 재수립 금지 (ADR-M22-4) — 두 번째 EstablishPlan은 무시된다
            int after = d.PlannedCount;
            d.EstablishPlan(new Vector2Int(30, 30), 3, Vector2Int.zero, null);
            Assert.AreEqual(after, d.PlannedCount, "계획은 판당 1회 (재수립 무시)");
        }

        [Test]
        public void M22_T3c_PlayerZone_OncePerSlot_AndSnapshotWiring()
        {
            // 플레이어 구역 등록 문 (ZoneService.EstablishPlayerZone) — 판당 1개 불변 (ADR-M22-4)
            var zones = new ZoneService();
            int fired = 0;
            zones.OnZoneEstablished += (slot, anchor, radius) => fired++;
            Assert.IsTrue(zones.EstablishPlayerZone(SlotId.FenceCount, new Vector2Int(3, 3), 5));
            Assert.IsFalse(zones.EstablishPlayerZone(SlotId.FenceCount, new Vector2Int(9, 9), 4),
                "이미 확정된 슬롯은 거부 — 방어 구역은 판당 하나");
            Assert.AreEqual(1, fired, "확정 이벤트는 정확히 1회");
            Assert.IsTrue(zones.TryGetZone(SlotId.FenceCount, out Vector2Int a, out int r));
            Assert.AreEqual(new Vector2Int(3, 3), a);
            Assert.AreEqual(5, r);

            // 스냅샷 배선 (provider 패턴, InjuredCount 동형) — DefensePlannedCount가 실린다
            var cfg = ScriptableObject.CreateInstance<WorldConfigSO>();
            var world = new WorldModel(new DiscoveryService(), cfg, defensePlannedCount: () => 7);
            Assert.AreEqual(7, world.BuildSnapshot(50, 50).Get(SlotId.DefensePlannedCount),
                "방어 계획 잔여가 스냅샷에 실린다 (Goal_BuildDefense 트리거의 전제)");
            var neutral = new WorldModel(new DiscoveryService(), cfg);
            Assert.AreEqual(0, neutral.BuildSnapshot(50, 50).Get(SlotId.DefensePlannedCount),
                "미배선 = 0 (중립 불변식)");
        }

        [Test]
        public void M22_T1b_PassageRules_DefaultBuildingBlocksNeither()
        {
            // 새 건물 에셋의 기본값은 "아무도 안 막음" — 플래그를 켜기 전에는 통행이 바뀌지 않는다.
            var b = ScriptableObject.CreateInstance<BuildingSO>();
            Assert.IsFalse(M0SimulationLoop.BlocksVillagerPassage(b), "기본값은 주민 통행 가능");
            Assert.IsFalse(M0SimulationLoop.BlocksThreatPassage(b), "기본값은 위협 통행 가능");
        }
    }
}
