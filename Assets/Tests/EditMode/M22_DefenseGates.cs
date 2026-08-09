using System.Collections.Generic;
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
        public void M22_T3_LineSnap_Deterministic()
        {
            // 우세축 스냅 (순수) — 대각 드래그는 수평/수직 직선으로 (대각 줄은 벽이 아니다)
            Assert.AreEqual(new Vector2Int(7, 2), DefenseService.SnapLineEnd(new Vector2Int(0, 2), new Vector2Int(7, 5)),
                "|Δx| ≥ |Δy| → 수평 스냅");
            Assert.AreEqual(new Vector2Int(0, 9), DefenseService.SnapLineEnd(new Vector2Int(0, 2), new Vector2Int(3, 9)),
                "|Δy| > |Δx| → 수직 스냅");

            // 줄 타일 (순수·결정적 — 시작→끝 순서, 양 끝 포함)
            var a = DefenseService.LineTiles(new Vector2Int(2, 3), new Vector2Int(-2, 3));
            var b = DefenseService.LineTiles(new Vector2Int(2, 3), new Vector2Int(-2, 3));
            Assert.AreEqual(5, a.Count, "2→−2 수평 줄 = 5칸");
            CollectionAssert.AreEqual(a, b, "같은 입력이면 언제나 같은 목록 (결정적)");
            Assert.AreEqual(new Vector2Int(2, 3), a[0]);
            Assert.AreEqual(new Vector2Int(-2, 3), a[4]);
            Assert.AreEqual(1, DefenseService.LineTiles(new Vector2Int(5, 5), new Vector2Int(5, 5)).Count,
                "제자리 = 1칸 (UI가 최소 2칸으로 거른다)");
        }

        [Test]
        public void M22_T3b_FencePlan_FilterDedup_GateConvert_AndConsume()
        {
            var d = new DefenseService();
            var line = DefenseService.LineTiles(new Vector2Int(0, 0), new Vector2Int(4, 0)); // 5칸
            bool Buildable(int x, int y) => !(x == 2 && y == 0);
            Assert.AreEqual(4, d.AddFencePlan(line, Buildable),
                "막힌 칸(2,0) 제외 — 계획과 시공의 점유 어휘는 같다");
            Assert.AreEqual(4, d.PlannedCount);
            Assert.AreEqual(0, d.AddFencePlan(line, Buildable), "겹쳐 그은 줄은 이중 계획이 안 된다 (dedup)");
            // (같은 줄을 필터 없이 다시 그으면 아까 막혔던 (2,0)만 추가되는 것이 옳다 —
            //  첫 기대값 0은 검산 오류였고 게이트 red가 잡았다)

            // 우클릭 문: 줄 위 칸은 울타리 → 문 전환, 총수 불변·문 수 +1
            Assert.IsTrue(d.TryAddGatePlan(new Vector2Int(1, 0), null));
            Assert.AreEqual(4, d.PlannedCount, "전환은 총수를 안 바꾼다");
            Assert.AreEqual(1, d.GatePlannedCount);
            Assert.IsFalse(d.TryAddGatePlan(new Vector2Int(1, 0), null), "같은 칸 문 중복 금지");

            // 완공 차감 — 방어 계획 건물만 (PlaceOnDefensePlan), 문/울타리는 CountSlot이 가른다
            var fence = ScriptableObject.CreateInstance<BuildingSO>();
            fence.PlaceOnDefensePlan = true;
            fence.CountSlot = SlotId.FenceCount;
            var gate = ScriptableObject.CreateInstance<BuildingSO>();
            gate.PlaceOnDefensePlan = true;
            gate.CountSlot = SlotId.GateCount;
            var house = ScriptableObject.CreateInstance<BuildingSO>();
            d.NotifyBuilt(house, 0, 0);
            Assert.AreEqual(4, d.PlannedCount, "방어 계획 밖 건물(집)은 계획을 건드리지 않는다");
            d.NotifyBuilt(fence, 0, 0);
            Assert.AreEqual(3, d.PlannedCount, "울타리 완공 = 계획 1 차감");
            d.NotifyBuilt(gate, 1, 0);
            Assert.AreEqual(0, d.GatePlannedCount, "문 완공 = 문 계획 소진");
            Assert.AreEqual(2, d.PlannedCount);

            // 시설이 선 칸은 재계획 거부 (완공 울타리 위에 줄을 그어도 무시)
            var built = ScriptableObject.CreateInstance<BuildingSO>();
            built.PlaceOnDefensePlan = true;
            built.IsCountable = true;
            built.CountSlot = SlotId.FenceCount;
            built.MaxDurability = 100f;
            d.NotifyBuilt(built, 4, 4);
            Assert.AreEqual(0, d.AddFencePlan(new List<Vector2Int> { new Vector2Int(4, 4) }, null),
                "서 있는 시설 칸은 계획 대상이 아니다");
            Assert.IsFalse(d.TryAddGatePlan(new Vector2Int(4, 4), null), "서 있는 시설 칸에 문 계획 금지 (철거 축은 2차+)");
        }

        [Test]
        public void M22_T4_Planner_GateWhenGatePlanned_ElseFence()
        {
            // W4 건설 사슬 (W3R2 개정) — 문 전제 = GatePlannedCount ≥ 1 (문 계획이 있어야만 문
            // 액션이 후보). 문이 싸서(5<8) 계획이 있으면 문 먼저, 없으면 울타리. 舊 GateCount==0
            // 전제는 문이 여러 개(우클릭)가 되며 폐기 — 두 번째 문이 영영 안 서는 공회전 함정.
            var catalog = AssetDatabase.LoadAssetAtPath<ActionCatalog>("Assets/M0Config/ActionCatalog.asset");
            var goal = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_BuildDefense.asset");
            Assert.IsNotNull(catalog); Assert.IsNotNull(goal);
            Assert.IsTrue(goal.RelativeToCurrent, "방어 건설 goal은 한 걸음 (ADR-M0-12)");
            var gw = new PlannerGateway(catalog);

            ActionSO[] PlanWith(int planned, int gatesPlanned, int gatesBuilt)
            {
                var slots = new int[PlanningConfig.TotalSlots];
                slots[(int)SlotId.MySatiety] = 80;
                slots[(int)SlotId.WoodStock] = 50;
                slots[(int)SlotId.DefensePlannedCount] = planned;
                slots[(int)SlotId.GatePlannedCount] = gatesPlanned;
                slots[(int)SlotId.GateCount] = gatesBuilt;
                GoalSO resolved = ScriptableObject.Instantiate(goal);
                resolved.RelativeToCurrent = false;
                resolved.GoalConditions[0].Value = VillagerAgent.ResolveRelativeTarget(
                    planned, goal.GoalConditions[0].Value, 0);
                PlannerGateway.PendingPlan pending = gw.RequestPlan(new WorldSnapshot(slots), resolved);
                gw.CompleteNow(pending);
                Assert.IsTrue(gw.TryGetResult(pending, out PlanStatus status, out ActionSO[] plan, out _));
                Assert.AreEqual(PlanStatus.Success, status, $"planned={planned} gp={gatesPlanned}");
                return plan;
            }

            ActionSO[] first = PlanWith(planned: 10, gatesPlanned: 1, gatesBuilt: 0);
            Assert.AreEqual(1, first.Length, "한 걸음 goal = 1액션 플랜");
            Assert.AreEqual("BuildGate", first[0].name, "문 계획이 있으면 문 먼저 (싼 비용)");

            ActionSO[] second = PlanWith(planned: 9, gatesPlanned: 0, gatesBuilt: 1);
            Assert.AreEqual(1, second.Length);
            Assert.AreEqual("BuildFence", second[0].name, "문 계획이 없으면 울타리");

            // 두 번째 문도 선다 — 문이 이미 1개 있어도 새 문 계획이 있으면 문 액션이 후보다
            ActionSO[] third = PlanWith(planned: 5, gatesPlanned: 1, gatesBuilt: 1);
            Assert.AreEqual("BuildGate", third[0].name, "문 여러 개 허용 (舊 GateCount==0 전제의 폐기 근거)");
        }

        [Test]
        public void M22_T4b_NextBuildTile_GatePoolForGateAsset_NearestForFence()
        {
            var d = new DefenseService();
            d.AddFencePlan(DefenseService.LineTiles(new Vector2Int(-2, 2), new Vector2Int(2, 2)), null);
            Assert.IsTrue(d.TryAddGatePlan(new Vector2Int(0, 2), null)); // 줄 가운데를 문으로
            var fence = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/Fence.asset");
            var gate = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/Gate.asset");

            Assert.IsTrue(d.TryGetNextBuildTile(gate, new Vector2Int(9, 9), null, out Vector2Int g));
            Assert.AreEqual(new Vector2Int(0, 2), g, "문 에셋 = 문 계획 풀");

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
            d.AddFencePlan(DefenseService.LineTiles(new Vector2Int(0, 2), new Vector2Int(4, 2)), null);
            var fence = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/Fence.asset");
            var site = new Vector2Int(2, 2); // 계획 줄 위 타일
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
        public void M22_T6_RepairDurationMult_NeutralByDefault_CarpenterOneToFive()
        {
            // ADR-M22-7 — 수리 전용 배율의 중립 불변식 (M19_T3b 동형). 신규 JobSO 기본 = 1,
            // 에셋 전수: 목수만 0.2 (사용자 확정 1:5), 나머지 전부 정확히 1 (페널티 방향 금지).
            var fresh = ScriptableObject.CreateInstance<JobSO>();
            Assert.AreEqual(1f, fresh.RepairDurationMult, "신규 JobSO 기본 = 1 (중립)");
            Object.DestroyImmediate(fresh);

            int carpenters = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:JobSO", new[] { "Assets/M0Config/Jobs" }))
            {
                var job = AssetDatabase.LoadAssetAtPath<JobSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (job == null) continue;
                if (job.name == "Job_Carpenter")
                {
                    carpenters++;
                    Assert.AreEqual(0.2f, job.RepairDurationMult,
                        "목수 = 0.2 — 1:5 격차 (사용자 확정: '주민이 1이면 목수는 5'). " +
                        "BuildDurationMult(0.5)와 별개 축 (재사용은 M19_T3b 충돌)");
                }
                else
                {
                    Assert.AreEqual(1f, job.RepairDurationMult,
                        $"{job.name}: 목수 외 수리 배율은 1 — 페널티 방향은 중립 불변식 위반");
                }
            }
            Assert.AreEqual(1, carpenters, "Job_Carpenter 에셋 존재");
        }

        [Test]
        public void M22_T6b_Planner_RepairsWhenDamaged()
        {
            // W6 수리 사슬 — 손상(DefenseDamagedCount ≥ 1)이 있고 Wood가 있으면 수리 플랜이 선다.
            var catalog = AssetDatabase.LoadAssetAtPath<ActionCatalog>("Assets/M0Config/ActionCatalog.asset");
            var goal = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_RepairDefense.asset");
            Assert.IsNotNull(catalog); Assert.IsNotNull(goal);
            Assert.IsTrue(goal.RelativeToCurrent, "수리 goal은 한 걸음 (ADR-M0-12)");
            var gw = new PlannerGateway(catalog);

            var slots = new int[PlanningConfig.TotalSlots];
            slots[(int)SlotId.MySatiety] = 80;
            slots[(int)SlotId.WoodStock] = 5;
            slots[(int)SlotId.DefenseDamagedCount] = 1;
            GoalSO resolved = ScriptableObject.Instantiate(goal);
            resolved.RelativeToCurrent = false;
            resolved.GoalConditions[0].Value = VillagerAgent.ResolveRelativeTarget(
                1, goal.GoalConditions[0].Value, 0);
            PlannerGateway.PendingPlan pending = gw.RequestPlan(new WorldSnapshot(slots), resolved);
            gw.CompleteNow(pending);
            Assert.IsTrue(gw.TryGetResult(pending, out PlanStatus status, out ActionSO[] plan, out _));
            Assert.AreEqual(PlanStatus.Success, status);
            Assert.AreEqual(1, plan.Length, "한 걸음 goal = 1액션 플랜");
            Assert.AreEqual("RepairDefense", plan[0].name);
        }

        [Test]
        public void M22_T6c_RepairCost_FromShippedAsset()
        {
            // 수리 비용의 단일 출처 = BuildingSO.RepairCost — 완공 시 등록돼 러너 선검사의 원천이 된다
            var d = new DefenseService();
            var fence = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/Fence.asset");
            d.NotifyBuilt(fence, 7, 7);
            Assert.IsTrue(d.TryGetRepairCost(SlotId.FenceCount, new Vector2Int(7, 7), out int cost));
            Assert.AreEqual(fence.RepairCost, cost, "수리 비용 = 에셋 값 (ADR-M0-2)");
        }

        [Test]
        public void M22_T7_Carpenter_BoostsDefenseGoals()
        {
            // W7 목수 부활 — 방어 건설·수리가 목수의 선호 goal이어야 "목수가 앞장서는" 그림이 된다
            // (스폰 풀 재편입은 씬 직렬화라 게이트 밖 — 커밋 diff가 증거).
            var carpenter = AssetDatabase.LoadAssetAtPath<JobSO>("Assets/M0Config/Jobs/Job_Carpenter.asset");
            Assert.IsNotNull(carpenter);
            var build = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_BuildDefense.asset");
            var repair = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_RepairDefense.asset");
            bool boostsBuild = false, boostsRepair = false;
            foreach (GoalBoost b in carpenter.GoalBoosts)
            {
                if (b.Goal == build && b.Boost > 0) boostsBuild = true;
                if (b.Goal == repair && b.Boost > 0) boostsRepair = true;
            }
            Assert.IsTrue(boostsBuild, "목수는 방어 건설을 선호한다 (GoalBoost > 0)");
            Assert.IsTrue(boostsRepair, "목수는 수리를 선호한다 — 부재 시나리오의 짝 (ADR-M22-7)");
        }

        [Test]
        public void M22_T8_Demolish_NoPlanReturn_UnlikeDestruction()
        {
            // 철거(플레이어)와 파괴(위협)의 갈림길 (M22-W8) — 파괴는 계획으로 되돌아가 재건되고,
            // 철거는 되돌아가면 안 된다 (주민이 도로 짓는다). 순서 = Demolish 먼저, NotifyRemoved 뒤.
            var d = new DefenseService();
            var fence = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/Fence.asset");

            // 계획 취소 — 무료, 즉시
            d.AddFencePlan(DefenseService.LineTiles(new Vector2Int(0, 0), new Vector2Int(3, 0)), null);
            Assert.IsTrue(d.RemovePlanAt(new Vector2Int(1, 0)));
            Assert.AreEqual(3, d.PlannedCount, "계획 취소 = 계획에서만 빠진다");
            Assert.IsFalse(d.RemovePlanAt(new Vector2Int(1, 0)), "이미 없는 칸은 false");

            // 파괴 경로 (대조군): NotifyRemoved만 → 계획 복귀
            var site = new Vector2Int(2, 0);
            d.NotifyBuilt(fence, site.x, site.y);
            Assert.AreEqual(2, d.PlannedCount);
            d.NotifyRemoved(SlotId.FenceCount, site.x, site.y);
            Assert.AreEqual(3, d.PlannedCount, "파괴 = 계획 복귀 (재건)");
            Assert.IsFalse(d.HasStructureAt(SlotId.FenceCount, site));

            // 철거 경로: Demolish 먼저 → NotifyRemoved는 조기 반환 → 계획 복귀 없음
            d.NotifyBuilt(fence, site.x, site.y); // 다시 지음 (계획 차감 3→2)
            Assert.AreEqual(2, d.PlannedCount);
            Assert.IsTrue(d.Demolish(SlotId.FenceCount, site));
            d.NotifyRemoved(SlotId.FenceCount, site.x, site.y); // 제거 문 경유 시 뒤따르는 통지
            Assert.AreEqual(2, d.PlannedCount, "철거 = 계획 복귀 없음 — 주민이 도로 짓지 않는다");
            Assert.IsFalse(d.HasStructureAt(SlotId.FenceCount, site));
            Assert.IsFalse(d.Demolish(SlotId.FenceCount, site), "이미 없는 시설은 false");
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

            // 줄 4개로 두른 링 (W3R2 — 플레이어가 긋는 방식 그대로): anchor ± 2 사각의 경계
            var anchor = new Vector2Int(10, 0);
            var ring = new List<Vector2Int>();
            ring.AddRange(DefenseService.LineTiles(new Vector2Int(8, -2), new Vector2Int(12, -2)));
            ring.AddRange(DefenseService.LineTiles(new Vector2Int(8, 2), new Vector2Int(12, 2)));
            ring.AddRange(DefenseService.LineTiles(new Vector2Int(8, -1), new Vector2Int(8, 1)));
            ring.AddRange(DefenseService.LineTiles(new Vector2Int(12, -1), new Vector2Int(12, 1)));
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
        public void M22_T3c_IncrementalPlan_SnapConnect_AndSnapshotWiring()
        {
            // 줄 누적 (ADR-M22-4 재개정) — 여러 줄이 쌓이고, 변경 알림이 그때마다 나간다
            var d = new DefenseService();
            int changed = 0;
            d.OnPlanChanged += () => changed++;
            d.AddFencePlan(DefenseService.LineTiles(new Vector2Int(0, 0), new Vector2Int(4, 0)), null);
            d.AddFencePlan(DefenseService.LineTiles(new Vector2Int(4, 1), new Vector2Int(4, 4)), null);
            Assert.AreEqual(9, d.PlannedCount, "두 줄 누적 (5 + 4)");
            Assert.AreEqual(2, changed, "줄마다 변경 알림 1회");

            // 시작점 달라붙기 (줄 연결) — 기존 줄 곁(체비쇼프 1)이면 그 칸을 돌려준다
            Assert.IsTrue(d.TryGetNearestPlanOrStructureTile(new Vector2Int(5, 5), 1, out Vector2Int snap));
            Assert.AreEqual(new Vector2Int(4, 4), snap, "곁 시작점은 기존 줄 끝에 달라붙는다");
            Assert.IsFalse(d.TryGetNearestPlanOrStructureTile(new Vector2Int(9, 9), 1, out _),
                "먼 곳은 달라붙지 않는다");

            // 스냅샷 배선 (provider 패턴, InjuredCount 동형) — 방어 파생 슬롯 3종이 실린다
            var cfg = ScriptableObject.CreateInstance<WorldConfigSO>();
            var world = new WorldModel(new DiscoveryService(), cfg,
                defensePlannedCount: () => 7, defenseDamagedCount: () => 2, gatePlannedCount: () => 1);
            Assert.AreEqual(7, world.BuildSnapshot(50, 50).Get(SlotId.DefensePlannedCount),
                "방어 계획 잔여가 스냅샷에 실린다 (Goal_BuildDefense 트리거의 전제)");
            Assert.AreEqual(2, world.BuildSnapshot(50, 50).Get(SlotId.DefenseDamagedCount),
                "손상 수가 스냅샷에 실린다 (Goal_RepairDefense 트리거의 전제, W5)");
            Assert.AreEqual(1, world.BuildSnapshot(50, 50).Get(SlotId.GatePlannedCount),
                "문 계획 수가 스냅샷에 실린다 (BuildGate 전제의 원천, W3R2)");
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

        [Test]
        public void M22_T9_GateLock_PathCrossing_PureRule()
        {
            // 선별 재계획의 판정 (M22-2차 W1, ADR-M22-9) — 잔여 경로가 문을 지나는 주민만 중단.
            // startIndex 앞은 이미 밟았거나 예약한 칸 = 한 걸음 관용.
            var gate = new HashSet<Vector2Int> { new Vector2Int(5, 0) };
            var wp = new List<Vector2Int>
            {
                new Vector2Int(3, 0), new Vector2Int(4, 0), new Vector2Int(5, 0), new Vector2Int(6, 0)
            };
            Assert.IsTrue(VillagerAgent.PathCrossesAny(wp, 0, gate), "잔여 경로에 문 포함 → 중단 대상");
            Assert.IsFalse(VillagerAgent.PathCrossesAny(wp, 3, gate),
                "문(인덱스 2)을 이미 예약했으면 그 뒤부터 검사 → 관용 통과");
            Assert.IsFalse(VillagerAgent.PathCrossesAny(
                new List<Vector2Int> { new Vector2Int(1, 1), new Vector2Int(2, 1) }, 0, gate),
                "문 미경유 경로는 건드리지 않는다 (전원 Abort 금지 — ⚠️②)");
            Assert.IsFalse(VillagerAgent.PathCrossesAny(null, 0, gate), "경로 없음(비이동) → false");
            Assert.IsFalse(VillagerAgent.PathCrossesAny(wp, 0, new HashSet<Vector2Int>()), "문 0개 → false");
        }

        [Test]
        public void M22_T9b_GateLock_StateMachine_AndBreachWins()
        {
            // 잠금 상태의 집 = DefenseService (ADR-M22-9). 문 등록부는 내구도에서 파생 — 전용 장부 없음.
            var d = new DefenseService();
            var gateSO = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/Gate.asset");
            var fenceSO = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/Fence.asset");
            Assert.IsFalse(d.GatesLocked, "시작은 열림");
            Assert.IsFalse(d.HasBuiltGates, "문 없음");

            int events = 0; bool lastState = false;
            d.OnGateLockChanged += v => { events++; lastState = v; };
            Assert.IsTrue(d.ToggleGateLock(), "토글 1회 = 잠김");
            Assert.IsTrue(d.GatesLocked && lastState && events == 1, "이벤트 1회·상태 일치");

            d.NotifyBuilt(gateSO, 5, 0);
            d.NotifyBuilt(fenceSO, 6, 0);
            Assert.IsTrue(d.HasBuiltGates);
            var gates = new List<Vector2Int>(d.BuiltGateTiles);
            Assert.AreEqual(1, gates.Count, "울타리는 문 등록부에 안 나온다");
            Assert.AreEqual(new Vector2Int(5, 0), gates[0]);

            // 파괴는 잠금을 이긴다 (ADR-M22-9) — 등록부에서 소멸 (통행 복구는 기존 구독·T5c 등가물 몫)
            d.NotifyRemoved(SlotId.GateCount, 5, 0);
            Assert.IsFalse(d.HasBuiltGates, "잠금 중 파괴 → 문 소멸 (잠금이 파괴를 막지 않는다)");
            Assert.IsTrue(d.GatesLocked, "잠금 상태 자체는 유지 — 해제는 플레이어 몫");
            Assert.IsFalse(d.ToggleGateLock(), "토글 2회 = 해제 (문 0개여도 해제는 된다)");
            Assert.AreEqual(2, events);

            // 잠금 중 신규 문 완공 규칙 (전제 확인 ④의 유일한 신규 훅 — 순수 함수 검산)
            Assert.IsTrue(M0SimulationLoop.LockedGateBlocksVillager(gateSO, gatesLocked: true),
                "잠금 중 완공된 문은 태어나면서 잠긴다");
            Assert.IsFalse(M0SimulationLoop.LockedGateBlocksVillager(gateSO, gatesLocked: false),
                "열림 상태의 문 완공은 기존 규칙 그대로 (주민 통행 가능)");
            Assert.IsFalse(M0SimulationLoop.LockedGateBlocksVillager(fenceSO, gatesLocked: true),
                "울타리는 이 규칙과 무관 (BlocksMovement가 이미 막는다)");
        }

        [Test]
        public void M22_T9c_GateLock_JpsDetour_Equivalence()
        {
            // 잠금 = 문 칸의 주민 배열 false (ADR-M22-9) — 배선 등가물 검산 (T5c 동형: 씬 없는
            // EditMode라 ToggleGateLock 창구의 실행은 Play 실측 몫, 배열↔JPS 규칙을 검산한다).
            int size = 100, off = 50;
            var walkable = new bool[size, size];
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                    walkable[x, y] = true;

            // 맵을 세로로 가르는 벽 x=10, 문 하나 (10,0) — 문이 유일한 통로
            var gateTile = new Vector2Int(10, 0);
            for (int y = 0; y < size; y++) walkable[10 + off, y] = false;
            walkable[gateTile.x + off, gateTile.y + off] = true;

            var pf = new AIVillage.Core.JpsPathfinder(() => walkable);
            Assert.AreEqual(AIVillage.Core.PathResultKind.PathFound,
                pf.FindPath(5, 0, 15, 0).Kind, "열림: 문 경유 경로가 있다");

            walkable[gateTile.x + off, gateTile.y + off] = false; // 잠금 (ToggleGateLock의 등가물)
            Assert.AreEqual(AIVillage.Core.PathResultKind.Unreachable,
                pf.FindPath(5, 0, 15, 0).Kind, "잠금: 문 경유 경로가 사라진다 (성공 기준 1)");

            walkable[gateTile.x + off, gateTile.y + off] = true; // 해제
            Assert.AreEqual(AIVillage.Core.PathResultKind.PathFound,
                pf.FindPath(5, 0, 15, 0).Kind, "해제: 경로 복원 (성공 기준 3)");
        }

        [Test]
        public void M22_T10_ConvertFenceToGate_Atomic_AndNetCost()
        {
            // 원클릭 전환 (M22-2차 W2, ADR-M22-10) — 기존 두 창구의 원자 합성. 씬 없는 EditMode라
            // 창구(TryConvertFenceToGateAt) 실행은 Play 몫 — 사전 검사와 합성 시퀀스 등가물을 검산.
            var d = new DefenseService();
            var fenceSO = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/Fence.asset");

            // 전환 대상 판정: 빈 칸·계획 칸은 아님, 완공 울타리만
            var site = new Vector2Int(6, 0);
            Assert.IsFalse(d.CanConvertToGateAt(site), "빈 칸은 전환 대상 아님");
            d.AddFencePlan(DefenseService.LineTiles(new Vector2Int(5, 0), new Vector2Int(7, 0)), null);
            Assert.IsFalse(d.CanConvertToGateAt(site), "계획 칸은 전환 대상 아님 — 기존 우클릭 전환 문법 몫");
            d.NotifyBuilt(fenceSO, site.x, site.y); // 완공 (계획 차감)
            Assert.IsTrue(d.CanConvertToGateAt(site), "완공 울타리 = 전환 대상");

            // 합성 시퀀스 등가물: 철거(Demolish 선행 = 계획 복귀 무장해제) → 문 계획
            int gateBefore = d.GatePlannedCount;
            Assert.IsTrue(d.Demolish(SlotId.FenceCount, site));
            d.NotifyRemoved(SlotId.FenceCount, site.x, site.y); // 제거 문 경유 시 뒤따르는 통지 — 조기 반환
            Assert.IsTrue(d.TryAddGatePlan(site, null), "철거된 칸에 문 계획이 선다 (사전 검사의 보증)");
            Assert.AreEqual(gateBefore + 1, d.GatePlannedCount, "문 계획 +1");
            Assert.IsFalse(d.HasStructureAt(SlotId.FenceCount, site), "울타리는 사라졌다");
            Assert.IsFalse(d.CanConvertToGateAt(site), "전환 후 같은 칸 재전환 불가 (문 계획 중복 방어)");

            // 순 비용 규칙 (사용자 확정 2026-08-09) — 회수를 셈에 넣는다
            Assert.IsTrue(M0SimulationLoop.ConversionAffordable(stock: 2, fenceRefund: 1, gateCost: 3),
                "보유 2 + 회수 1 = 문 3 — 허용 (회수를 빼면 거짓 음성)");
            Assert.IsFalse(M0SimulationLoop.ConversionAffordable(stock: 1, fenceRefund: 1, gateCost: 3),
                "보유 1은 부족");
            Assert.IsFalse(M0SimulationLoop.ConversionAffordable(stock: 2, fenceRefund: 0, gateCost: 3),
                "일반 문 지정(회수 없음)은 기존 판정 그대로");
        }

        [Test]
        public void M22_T11_ResourceHud_ShippedConfig()
        {
            // 자원 HUD (M22-2차 W4, Play 피드백 "보유 자원이 안 보인다") — 배포 에셋 검산.
            // 목록·라벨의 유일한 원천 = WorldConfigSO.HudResources (새 자원 = 행 추가, 코드 0줄).
            var cfg = AssetDatabase.LoadAssetAtPath<WorldConfigSO>("Assets/M0Config/WorldConfig.asset");
            Assert.IsNotNull(cfg, "WorldConfig 에셋 없음");
            Assert.IsNotNull(cfg.HudResources, "HudResources 미배선 — 자원 줄이 통째로 사라진다");
            Assert.GreaterOrEqual(cfg.HudResources.Length, 2, "최소 나무·돌 두 줄 (요청 원문)");

            var slots = new HashSet<SlotId>();
            foreach (WorldConfigSO.ResourceHudEntry e in cfg.HudResources)
            {
                Assert.IsTrue(SlotIds.IsStock(e.Slot),
                    $"{e.Slot}: 전역 스톡만 표시한다 — 개인·집 스톡을 넣으면 항상 0이 찍힌다 (IsStock 아님)");
                Assert.IsFalse(string.IsNullOrEmpty(e.Label), $"{e.Slot}: 라벨 비어 있음 (YAML 오타면 빈 문자열로 풀린다)");
                Assert.IsTrue(slots.Add(e.Slot), $"{e.Slot}: 중복 행");
            }
            Assert.IsTrue(slots.Contains(SlotId.WoodStock) && slots.Contains(SlotId.StoneStock),
                "나무·돌은 반드시 포함 (건설·수리 비용의 두 축)");
        }
    }
}
