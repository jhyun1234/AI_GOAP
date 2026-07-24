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

        private static WorldConfigSO Config(int wood)
        {
            var c = ScriptableObject.CreateInstance<WorldConfigSO>();
            c.InitialWoodStock = wood;
            return c;
        }

        /// <summary>수량형 밭 (비용 Wood 5) — 제거 대상.</summary>
        private static BuildingSO FarmBuilding()
        {
            var b = ScriptableObject.CreateInstance<BuildingSO>();
            b.DisplayName = "밭";
            b.Costs = new[] { new ResourceCost { StockSlot = SlotId.WoodStock, Amount = 5 } };
            b.IsCountable = true;
            b.CountSlot = SlotId.FarmPlotCount;
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

        // ── M9-T2: 파괴 문 (등록과 대칭) ─────────────────────────────────────────

        [Test]
        public void M9_T2_RemoveCountableAt_CountListEvent_DoubleRemoveGuarded()
        {
            var world = new WorldModel(new DiscoveryService(), Config(10));
            var c = new ConstructionService(world);
            int removed = 0;
            SlotId lastSlot = default;
            c.OnRemoved += (s, x, y) => { removed++; lastSlot = s; };

            Assert.IsTrue(c.Complete(FarmBuilding(), 2, 2));
            Assert.IsTrue(c.Complete(FarmBuilding(), 8, 8));
            Assert.AreEqual(2, world.GetStock(SlotId.FarmPlotCount));

            Assert.IsTrue(c.RemoveCountableAt(SlotId.FarmPlotCount, 2, 2), "완공 타일 제거 성공");
            Assert.AreEqual(1, world.GetStock(SlotId.FarmPlotCount), "카운트 -1");
            Assert.AreEqual(1, removed, "OnRemoved 1회");
            Assert.AreEqual(SlotId.FarmPlotCount, lastSlot);
            Assert.IsFalse(c.HasBuildingAt(2, 2), "목록에서 제거 — 그 타일에 건물 없음");
            Assert.IsTrue(c.HasBuildingAt(8, 8), "다른 밭은 유지");

            Assert.IsFalse(c.RemoveCountableAt(SlotId.FarmPlotCount, 99, 99), "없는 타일 false");
            Assert.IsFalse(c.RemoveCountableAt(SlotId.FarmPlotCount, 2, 2), "이중 제거 false");
            Assert.AreEqual(1, removed, "실패는 이벤트 없음");
            Assert.AreEqual(1, world.GetStock(SlotId.FarmPlotCount), "실패는 카운트 불변");
        }

        [Test]
        public void M9_T2_BlightCrop_GrowingRipeToEmpty_ReleasesOccupant()
        {
            var farm = new FarmService(growthDays: 1.5f);
            farm.RegisterPlot(3, 3, "A");
            FarmPlot p = farm.NearestEmpty(0, 0);

            Assert.IsFalse(farm.BlightCrop(p), "Empty는 소실 대상 아님 (작물 없음)");

            Assert.IsTrue(farm.TryPlant(p));            // Growing
            Assert.IsTrue(p.TryClaim("A"));
            Assert.IsTrue(farm.BlightCrop(p), "Growing 소실");
            Assert.AreEqual(FarmState.Empty, p.State);
            Assert.IsNull(p.OccupantId, "소실 시 점유 해제");
            Assert.AreEqual(1, farm.CountEmpty(), "시설은 남아 빈 밭 (재파종 가능)");

            farm.TryPlant(p);
            farm.TickGrowth(2f);
            Assert.IsTrue(farm.HasRipe);
            Assert.IsTrue(farm.BlightCrop(p), "Ripe 소실");
            Assert.AreEqual(FarmState.Empty, p.State);
        }

        [Test]
        public void M9_T2_RemovedPlot_Invalidated_NoGhostPlantOrHarvest()
        {
            var farm = new FarmService(growthDays: 1.5f);
            farm.RegisterPlot(4, 4, "A");
            FarmPlot p = farm.NearestEmpty(0, 0);
            farm.TryPlant(p);
            farm.TickGrowth(2f);
            Assert.AreEqual(FarmState.Ripe, p.State);

            int removedEvt = 0;
            farm.OnPlotRemoved += _ => removedEvt++;
            Assert.IsTrue(farm.RemovePlot(4, 4), "시설 제거 성공");
            Assert.AreEqual(1, removedEvt, "OnPlotRemoved 1회");
            Assert.AreEqual(0, farm.Plots.Count, "목록에서 제거");

            // 러너가 들고 있던 참조로도 유령 수확·심기 불가 (⚠️①)
            Assert.IsFalse(farm.TryHarvest(p), "제거된 플롯 수확 불가");
            Assert.IsFalse(farm.TryPlant(p), "제거된 플롯 심기 불가");
            Assert.IsFalse(farm.RemovePlot(4, 4), "이미 제거 → false");
        }

        [Test]
        public void M9_T2_RegisterRemove_RoundTrip_CountAndPlotsConsistent()
        {
            // ADR-M9-3 대칭 배선: OnCompleted→RegisterPlot / OnRemoved→RemovePlot (SimulationLoop 미러)
            var farm = new FarmService(growthDays: 1.5f);
            var world = new WorldModel(new DiscoveryService(), Config(10), farm);
            var c = new ConstructionService(world);
            c.OnCompleted += (b, x, y, _) =>
            {
                if (b.IsCountable && b.CountSlot == SlotId.FarmPlotCount) farm.RegisterPlot(x, y, "A");
            };
            c.OnRemoved += (slot, x, y) =>
            {
                if (slot == SlotId.FarmPlotCount) farm.RemovePlot(x, y);
            };

            Assert.IsTrue(c.Complete(FarmBuilding(), 5, 5));
            Assert.AreEqual(1, world.GetStock(SlotId.FarmPlotCount));
            Assert.AreEqual(1, farm.CountEmpty(), "등록 → 빈 밭 1");

            Assert.IsTrue(c.RemoveCountableAt(SlotId.FarmPlotCount, 5, 5));
            Assert.AreEqual(0, world.GetStock(SlotId.FarmPlotCount), "카운트 0");
            Assert.AreEqual(0, farm.Plots.Count, "RemovePlot 연쇄 — 밭 시설도 제거");
            Assert.AreEqual(0, farm.CountEmpty());
        }

        // ── M9-T4: N명 대화 릴레이 + 정지 상한 (순수 헬퍼) ──────────────────────────

        [Test]
        public void M9_T4_RelayDelay_IncrementsPerResponder()
        {
            // i번째 응답자 = ReplyDelaySec × (1+i) — 상대 1×, 청중 2×·3× (한 명씩 이어지는 릴레이)
            Assert.AreEqual(2.5f, ChatterService.RelayDelay(2.5f, 0), 1e-4f, "상대 = 1×");
            Assert.AreEqual(5.0f, ChatterService.RelayDelay(2.5f, 1), 1e-4f, "청중1 = 2×");
            Assert.AreEqual(7.5f, ChatterService.RelayDelay(2.5f, 2), 1e-4f, "청중2 = 3×");
        }

        [Test]
        public void M9_T4_PauserCount_CappedAtMaxAndNeutralAtOneToOne()
        {
            Assert.AreEqual(0, ChatterService.PauserCount(0), "응답자 0 = 대화 아님 → 정지 0");
            Assert.AreEqual(2, ChatterService.PauserCount(1), "1:1 = 화자+상대 2 (중립 불변식 = 기존 동작)");
            Assert.AreEqual(3, ChatterService.PauserCount(2), "화자+청중 = 3 (상한)");
            Assert.AreEqual(3, ChatterService.PauserCount(3), "상한 클램프");
            Assert.AreEqual(3, ChatterService.PauserCount(10), "청중 아무리 많아도 정지 ≤ 3 (ADR-M9-7)");
            Assert.AreEqual(3, ChatterService.MAX_CHAT_PAUSERS, "상수 = 3");
        }

        [Test]
        public void M9_T4_CollectEligible_RespectsCapAndOrder()
        {
            var outIdx = new System.Collections.Generic.List<int>();

            // 후보 6명 전원 자격 → 상한 3까지만 앞에서부터
            ChatterService.CollectEligible(6, 3, _ => true, outIdx);
            Assert.AreEqual(3, outIdx.Count, "MaxExtraListeners 초과 미수집");
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, outIdx, "앞에서부터 채운다");

            // 홀수 인덱스만 자격 → 자격자만, 상한 내
            ChatterService.CollectEligible(6, 2, i => i % 2 == 1, outIdx);
            CollectionAssert.AreEqual(new[] { 1, 3 }, outIdx, "자격 통과분만 상한까지");
        }

        [Test]
        public void M9_T4_MaxZero_IsNeutral_NoListeners()
        {
            var outIdx = new System.Collections.Generic.List<int>();
            // MaxExtraListeners 0 = 청중 미수집 → 응답자 = 상대뿐 (기존 1:1 Fire와 동일 경로)
            ChatterService.CollectEligible(5, 0, _ => true, outIdx);
            Assert.AreEqual(0, outIdx.Count, "max 0 → 청중 없음 (중립 불변식)");
            // 그때 정지 인원·릴레이 지연도 기존 1:1과 동일
            Assert.AreEqual(2, ChatterService.PauserCount(1), "상대뿐이면 정지 2 = 기존");
            Assert.AreEqual(2.5f, ChatterService.RelayDelay(2.5f, 0), 1e-4f, "상대 지연 = 기존 ReplyDelaySec");

            // 새 필드의 기본값이 곧 중립 — 기존 Chatter 3종은 무수정으로 M7 호흡 유지
            var fresh = ScriptableObject.CreateInstance<ChatterSO>();
            Assert.AreEqual(0, fresh.MaxExtraListeners, "청중 상한 기본 0 = 1:1");
            Assert.AreEqual(1f, fresh.SceneTempoMult, 1e-4f, "연출 배속 기본 1 = 기존 호흡");
        }
    }
}
