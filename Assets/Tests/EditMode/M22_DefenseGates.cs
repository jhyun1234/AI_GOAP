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
        public void M22_T4_Planner_BuildsGateFirstThenFence()
        {
            // W4 건설 사슬 — 문(BaseCost 5)이 울타리(8)보다 싸고 GateCount==0 전제가 있어
            // 플래너는 문부터 세운다 (문 먼저 = 주민 동선이 항상 열려 있다). 문이 서면
            // 전제가 죽어 울타리로 넘어간다. 상대 goal은 VillagerAgent 해석을 본떠 절대화한다.
            var catalog = AssetDatabase.LoadAssetAtPath<ActionCatalog>("Assets/M0Config/ActionCatalog.asset");
            var goal = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_BuildDefense.asset");
            Assert.IsNotNull(catalog); Assert.IsNotNull(goal);
            Assert.IsTrue(goal.RelativeToCurrent, "방어 건설 goal은 한 걸음 (ADR-M0-12)");
            var gw = new PlannerGateway(catalog);

            ActionSO[] PlanWith(int planned, int gates)
            {
                var slots = new int[PlanningConfig.TotalSlots];
                slots[(int)SlotId.MySatiety] = 80;
                slots[(int)SlotId.WoodStock] = 50;
                slots[(int)SlotId.DefensePlannedCount] = planned;
                slots[(int)SlotId.GateCount] = gates;
                GoalSO resolved = ScriptableObject.Instantiate(goal);
                resolved.RelativeToCurrent = false;
                resolved.GoalConditions[0].Value = VillagerAgent.ResolveRelativeTarget(
                    planned, goal.GoalConditions[0].Value, 0);
                PlannerGateway.PendingPlan pending = gw.RequestPlan(new WorldSnapshot(slots), resolved);
                gw.CompleteNow(pending);
                Assert.IsTrue(gw.TryGetResult(pending, out PlanStatus status, out ActionSO[] plan, out _));
                Assert.AreEqual(PlanStatus.Success, status, $"planned={planned} gates={gates}");
                return plan;
            }

            ActionSO[] first = PlanWith(planned: 40, gates: 0);
            Assert.AreEqual(1, first.Length, "한 걸음 goal = 1액션 플랜");
            Assert.AreEqual("BuildGate", first[0].name, "문이 먼저 — 전제(GateCount==0)와 싼 비용");

            ActionSO[] second = PlanWith(planned: 39, gates: 1);
            Assert.AreEqual(1, second.Length);
            Assert.AreEqual("BuildFence", second[0].name, "문이 서면 울타리 — 문 전제가 죽는다");
        }

        [Test]
        public void M22_T4b_NextBuildTile_GateForGateAsset_NearestFenceForFence()
        {
            var d = new DefenseService();
            d.EstablishPlan(new Vector2Int(0, 0), 2, new Vector2Int(0, -10), null);
            var fence = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/Fence.asset");
            var gate = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/Gate.asset");

            Assert.IsTrue(d.TryGetNextBuildTile(gate, new Vector2Int(9, 9), null, out Vector2Int g));
            Assert.AreEqual(d.PlannedGateTile.Value, g, "문 에셋 = 문 자리 (시공자 위치 무관)");

            Assert.IsTrue(d.TryGetNextBuildTile(fence, new Vector2Int(3, 3), null, out Vector2Int f));
            Assert.AreEqual(new Vector2Int(2, 2), f, "울타리 = 시공자 최근접 계획 타일");

            // 점유 필터 — 시공 시점에 막힌 자리는 건너뛴다 (계획 수립 후 상태 변화 대응)
            Assert.IsTrue(d.TryGetNextBuildTile(fence, new Vector2Int(3, 3),
                (x, y) => x == 2 && y == 2, out Vector2Int f2));
            Assert.AreNotEqual(new Vector2Int(2, 2), f2, "점유 타일은 건너뛴다");
        }

        [Test]
        public void M22_T5_Durability_DamageRepairDestroyAndPlanReturn()
        {
            var d = new DefenseService();
            d.EstablishPlan(new Vector2Int(0, 0), 2, new Vector2Int(0, -10), null);
            var fence = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/Fence.asset");
            var site = new Vector2Int(2, 2); // 계획 둘레 타일
            int plannedBefore = d.PlannedCount;

            // 완공 = 내구도 등록 + 계획 차감
            d.NotifyBuilt(fence, site.x, site.y);
            Assert.IsTrue(d.HasStructures);
            Assert.IsTrue(d.HasStructureAt(SlotId.FenceCount, site));
            Assert.AreEqual(plannedBefore - 1, d.PlannedCount);
            Assert.AreEqual(0, d.DamagedCount, "갓 지은 시설은 손상이 아니다");

            // 타격 — 검산: 100 − 34×2 = 32 (수치 관계식 §3)
            Assert.AreEqual(fence.MaxDurability - 34f, d.ApplyDamage(SlotId.FenceCount, site, 34f), 1e-3f);
            Assert.AreEqual(fence.MaxDurability - 68f, d.ApplyDamage(SlotId.FenceCount, site, 34f), 1e-3f);
            Assert.AreEqual(1, d.DamagedCount, "0 < 내구도 < 최대 = 손상 1건");
            Assert.IsTrue(d.TryGetMostDamaged(out SlotId ms, out Vector2Int mt));
            Assert.AreEqual(site, mt);

            // 수리 = 전량 복원 (한 걸음, ADR-M0-12)
            Assert.IsTrue(d.Repair(SlotId.FenceCount, site));
            d.TryGetDurability(SlotId.FenceCount, site, out float cur, out float max);
            Assert.AreEqual(max, cur, 1e-3f, "수리 = MaxDurability 복원");
            Assert.AreEqual(0, d.DamagedCount);
            Assert.IsFalse(d.Repair(SlotId.FenceCount, site), "멀쩡한 시설은 수리 대상이 아니다");

            // 3타 파괴 → 제거 통지 → 계획 복귀 (ADR-M22-6 — 파괴는 손상이 아니라 소멸+복귀)
            d.ApplyDamage(SlotId.FenceCount, site, 34f);
            d.ApplyDamage(SlotId.FenceCount, site, 34f);
            Assert.LessOrEqual(d.ApplyDamage(SlotId.FenceCount, site, 34f), 0f, "34×3 = 102 > 100 — 3타 파괴");
            d.NotifyRemoved(SlotId.FenceCount, site.x, site.y);
            Assert.IsFalse(d.HasStructureAt(SlotId.FenceCount, site));
            Assert.AreEqual(0, d.DamagedCount, "파괴된 시설은 수리 목록에 없다 — 재건은 건설 goal 몫");
            Assert.AreEqual(plannedBefore, d.PlannedCount, "부서진 자리는 다시 '지을 자리'다 (계획 복귀)");
        }

        [Test]
        public void M22_T5b_ShippedThreats_OneSurgeBuysOneBreach()
        {
            // §3 핵심 검산 — 체류 상한 안의 타격 수(0, 0.25, 0.5 = 상한/주기 회)로 울타리 한 칸을
            // 뚫을 수 있어야 한다. 못 뚫으면 "완성되면 안전" = ADR-M22-2 위반 (34→25로 내리면 red).
            var fence = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/Fence.asset");
            foreach (string name in new[] { "Threat_Tier1_Wolf", "Threat_Tier2_Pack", "Threat_Tier3_Bear" })
            {
                var t = AssetDatabase.LoadAssetAtPath<ThreatSO>($"Assets/M0Config/Threats/{name}.asset");
                Assert.IsNotNull(t, $"{name} 에셋 없음");
                Assert.Greater(t.StructureDamage, 0f, $"{name}: 공성 없는 위협은 울타리 앞에서 무적을 만든다");
                Assert.IsTrue(t.StrikeLinesStructure != null && t.StrikeLinesStructure.Length > 0,
                    $"{name}: 침묵 공성 금지 (W2R '내용 없는 분기' 함정)");
                int strikesPerStay = Mathf.FloorToInt(t.MaxStayDays / t.RepeatStrikePeriodDays);
                int strikesToBreach = Mathf.CeilToInt(fence.MaxDurability / t.StructureDamage);
                Assert.LessOrEqual(strikesToBreach, strikesPerStay,
                    $"{name}: 한 출몰(타격 {strikesPerStay}회)로 울타리(내구 {fence.MaxDurability})를 " +
                    $"못 뚫는다 — 시설이 시간을 '벌' 수는 있어도 '무적'이면 안 된다 (ADR-M22-2)");
            }
        }

        [Test]
        public void M22_T5c_Breach_ReopensJpsPath()
        {
            // 파괴 = 통행 복구의 원자 (ADR-M22-6) — 링이 막은 경로가 한 칸 뚫리면 다시 열린다.
            int size = 100, off = 50;
            var walkable = new bool[size, size];
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                    walkable[x, y] = true;

            var anchor = new Vector2Int(10, 0);
            var ring = DefenseService.PerimeterTiles(anchor, 2);
            foreach (Vector2Int t in ring) walkable[t.x + off, t.y + off] = false;

            var pf = new AIVillage.Core.JpsPathfinder(() => walkable);
            Assert.AreEqual(AIVillage.Core.PathResultKind.Unreachable,
                pf.FindPath(-10, 0, anchor.x, anchor.y).Kind, "완주 링 = 진입 불가");

            Vector2Int broken = ring[0];
            walkable[broken.x + off, broken.y + off] = true; // 파괴 → 복구 (OnRemoved 구독의 등가물)
            Assert.AreEqual(AIVillage.Core.PathResultKind.PathFound,
                pf.FindPath(-10, 0, anchor.x, anchor.y).Kind, "한 칸 뚫리면 경로가 다시 열린다");
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
