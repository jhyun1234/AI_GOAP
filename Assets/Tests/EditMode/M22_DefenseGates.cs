using System.Collections.Generic;
using AIVillage.Core;   // M22-4차 — ResourceNode·ResourceType (개간 게이트)
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
            Assert.AreEqual(4, d.AddFencePlan(line, Buildable).Added,
                "막힌 칸(2,0) 제외 — 계획과 시공의 점유 어휘는 같다");
            Assert.AreEqual(4, d.PlannedCount);
            Assert.AreEqual(0, d.AddFencePlan(line, Buildable).Added, "겹쳐 그은 줄은 이중 계획이 안 된다 (dedup)");
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
            Assert.AreEqual(0, d.AddFencePlan(new List<Vector2Int> { new Vector2Int(4, 4) }, null).Added,
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
            // 🔑 M24-1차 개정 — 舊 판은 위협 이름 셋을 배열로 박아 뒀다. 콘텐츠가 바뀌면
            // 새 위협이 검사에서 조용히 빠진다(더 나쁘다: red 가 아니라 green 이 된다).
            string[] threatGuids = AssetDatabase.FindAssets("t:ThreatSO");
            Assert.Greater(threatGuids.Length, 0, "배포 위협 에셋이 하나도 없다");
            foreach (string guid in threatGuids)
            {
                var t = AssetDatabase.LoadAssetAtPath<ThreatSO>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsNotNull(t, $"{guid} 로드 실패");
                string name = t.name;
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
            Assert.IsTrue(d.RemovePlanAt(new Vector2Int(1, 0), out _));
            Assert.AreEqual(3, d.PlannedCount, "계획 취소 = 계획에서만 빠진다");
            Assert.IsFalse(d.RemovePlanAt(new Vector2Int(1, 0), out _), "이미 없는 칸은 false");

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
        public void M22_T13_Planner_TrapTowerKinds_NoStarvation()
        {
            // M22-3차 W2 — 종류별 계획 전제 + BaseCost 서열이 기아를 막는다.
            // 🔴 울타리는 전용 계획 슬롯이 없어 전제도 없다 — 다른 종이 울타리보다 비싸면
            // "망루 계획만 남은 판"에서 플래너가 영원히 울타리(풀 없음)를 골라 공회전한다.
            // 처방 = 전제 있는 종(문·함정·망루)은 전부 울타리보다 싸다 (구현 중 발견·박제).
            var catalog = AssetDatabase.LoadAssetAtPath<ActionCatalog>("Assets/M0Config/ActionCatalog.asset");
            var goal = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_BuildDefense.asset");
            var fenceA = AssetDatabase.LoadAssetAtPath<ActionSO>("Assets/M0Config/Actions/BuildFence.asset");
            var gateA = AssetDatabase.LoadAssetAtPath<ActionSO>("Assets/M0Config/Actions/BuildGate.asset");
            var trapA = AssetDatabase.LoadAssetAtPath<ActionSO>("Assets/M0Config/Actions/BuildTrap.asset");
            var towerA = AssetDatabase.LoadAssetAtPath<ActionSO>("Assets/M0Config/Actions/BuildTower.asset");
            Assert.IsNotNull(trapA); Assert.IsNotNull(towerA);
            Assert.Less(gateA.BaseCost, trapA.BaseCost, "서열: 문 < 함정");
            Assert.Less(trapA.BaseCost, towerA.BaseCost, "서열: 함정 < 망루");
            Assert.Less(towerA.BaseCost, fenceA.BaseCost,
                "서열: 망루 < 울타리 — 뒤집으면 망루 계획만 남은 판이 공회전한다 (기아 방지)");

            var gw = new PlannerGateway(catalog);
            ActionSO[] PlanWith(int planned, int traps, int towers)
            {
                var slots = new int[PlanningConfig.TotalSlots];
                slots[(int)SlotId.MySatiety] = 80;
                slots[(int)SlotId.WoodStock] = 50;
                slots[(int)SlotId.StoneStock] = 50;
                slots[(int)SlotId.DefensePlannedCount] = planned;
                slots[(int)SlotId.TrapPlannedCount] = traps;
                slots[(int)SlotId.TowerPlannedCount] = towers;
                GoalSO resolved = ScriptableObject.Instantiate(goal);
                resolved.RelativeToCurrent = false;
                resolved.GoalConditions[0].Value = VillagerAgent.ResolveRelativeTarget(
                    planned, goal.GoalConditions[0].Value, 0);
                PlannerGateway.PendingPlan pending = gw.RequestPlan(new WorldSnapshot(slots), resolved);
                gw.CompleteNow(pending);
                Assert.IsTrue(gw.TryGetResult(pending, out PlanStatus status, out ActionSO[] plan, out _));
                Assert.AreEqual(PlanStatus.Success, status, $"planned={planned} tr={traps} tw={towers}");
                return plan;
            }

            Assert.AreEqual("BuildTrap", PlanWith(planned: 3, traps: 1, towers: 0)[0].name,
                "함정 계획이 있으면 함정이 울타리보다 먼저 (6 < 8)");
            Assert.AreEqual("BuildTower", PlanWith(planned: 1, traps: 0, towers: 1)[0].name,
                "망루 계획만 남은 판 — 울타리가 아니라 망루가 뽑힌다 (기아 방지의 실전형)");
            Assert.AreEqual("BuildFence", PlanWith(planned: 2, traps: 0, towers: 0)[0].name,
                "종류 전제가 다 꺼지면 울타리 (기존 동작 무변)");
        }

        [Test]
        public void M22_T13b_TrapTowerPlans_PoolsAndConsumableRegistry()
        {
            // 종류별 계획·풀·함정 등록부 (M22-3차 W2) — 문 선례(T4b) 동형 + 소모 규약.
            var d = new DefenseService();
            var trapSO = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/Trap.asset");
            var towerSO = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/Watchtower.asset");

            // 함정 줄 + 망루 1칸 — 합계·종류별 수가 따로 선다
            Assert.AreEqual(3, d.AddTrapPlan(DefenseService.LineTiles(new Vector2Int(0, 0), new Vector2Int(2, 0)), null).Added);
            Assert.IsTrue(d.TryAddTowerPlan(new Vector2Int(5, 5), null));
            Assert.AreEqual(3, d.TrapPlannedCount);
            Assert.AreEqual(1, d.TowerPlannedCount);
            Assert.AreEqual(4, d.PlannedCount, "합계 = 네 종 총합 (Goal_BuildDefense 트리거)");
            Assert.IsFalse(d.TryAddTowerPlan(new Vector2Int(1, 0), null), "함정 계획 칸에 망루 불가 (한 칸 = 한 계획)");

            // 시공 풀 — 에셋 CountSlot이 풀을 고른다 (T4b 동형)
            Assert.IsTrue(d.TryGetNextBuildTile(trapSO, new Vector2Int(9, 0), null, out Vector2Int tt));
            Assert.AreEqual(new Vector2Int(2, 0), tt, "함정 에셋 = 함정 풀 최근접");
            Assert.IsTrue(d.TryGetNextBuildTile(towerSO, new Vector2Int(0, 0), null, out Vector2Int wt));
            Assert.AreEqual(new Vector2Int(5, 5), wt, "망루 에셋 = 망루 풀");

            // 완공 — 차감 + 함정 등록부 (내구도 아님) / 망루는 내구도 등록
            d.NotifyBuilt(trapSO, 2, 0);
            Assert.AreEqual(2, d.TrapPlannedCount);
            Assert.IsTrue(d.HasTrapAt(new Vector2Int(2, 0)), "함정 = 설치 등록부");
            Assert.IsFalse(d.HasStructureAt(SlotId.TrapCount, new Vector2Int(2, 0)),
                "함정은 내구도 등록부에 없다 (소모품 — 공성 대상 아님, ADR-M22-13)");
            d.NotifyBuilt(towerSO, 5, 5);
            Assert.IsTrue(d.HasStructureAt(SlotId.WatchtowerCount, new Vector2Int(5, 5)), "망루 = 내구도(공성 대상)");

            // 함정 소모 (발동·철거 공통 문법): Demolish 선행 → NotifyRemoved — **계획 복귀 없음**
            int planned = d.PlannedCount;
            Assert.IsTrue(d.Demolish(SlotId.TrapCount, new Vector2Int(2, 0)));
            d.NotifyRemoved(SlotId.TrapCount, 2, 0);
            Assert.IsFalse(d.HasTrapAt(new Vector2Int(2, 0)));
            Assert.AreEqual(planned, d.PlannedCount, "함정 소모 = 계획 복귀 없음 (재설치는 플레이어 몫)");

            // 망루 파괴 = 계획 복귀 (울타리 동형 — 재건)
            d.NotifyRemoved(SlotId.WatchtowerCount, 5, 5);
            Assert.AreEqual(1, d.TowerPlannedCount, "망루 파괴 → 재건 계획 복귀");
        }

        [Test]
        public void M22_T16_ThreatSpriteSets_ShippedWiring()
        {
            // M22-3차 W5c — 위협 그림 배선 검산. **실 로드**까지 봐야 한 자리 오타가 잡힌다 (M23_T1 문법).
            // 🔑 아트 교체 지점이 여기 하나임을 박제한다 — 팩을 갈면 세트 에셋만 갈린다.
            //
            // 🔁 M24-1차 개정: 舊 판은 (위협, 세트) 이름 쌍을 박아 두고 **전원 배선**을 요구했다.
            // 늑대·곰을 종족으로 갈아 끼우는 구간에서는 그림이 아직 없어(W6 몫) 그대로면 red 다.
            // 그렇다고 검사를 지우면 "색 원으로 조용히 회귀"를 못 잡는다.
            // → 지키는 것을 **"전원 배선"에서 "반쪽 배선 금지"**로 옮긴다. 0개(색 원 폴백)와
            //   전원 배선은 둘 다 의도된 상태이고, **일부만 배선된 상태가 사고**다.
            //   그리고 배선된 것은 예전과 똑같이 끝까지 검사한다.
            string[] guids = AssetDatabase.FindAssets("t:ThreatSO");
            Assert.Greater(guids.Length, 0, "배포 위협 에셋이 하나도 없다");
            int wired = 0;
            foreach (string guid in guids)
            {
                var so = AssetDatabase.LoadAssetAtPath<ThreatSO>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsNotNull(so, $"{guid} 로드 실패");
                if (so.SpriteSet == null) continue;
                wired++;
                string threat = so.name, set = so.SpriteSet.name;

                AgentSpriteSetSO s = so.SpriteSet;
                foreach ((string label, Sprite[] frames) in new[]
                {
                    ("WalkDown", s.WalkDown), ("WalkSide", s.WalkSide), ("WalkUp", s.WalkUp),
                    ("IdleDown", s.IdleDown), ("IdleSide", s.IdleSide), ("IdleUp", s.IdleUp),
                })
                {
                    Assert.IsTrue(frames != null && frames.Length > 0, $"{set}.{label} 비어 있음");
                    for (int i = 0; i < frames.Length; i++)
                        Assert.IsNotNull(frames[i], $"{set}.{label}[{i}] null — fileID 배선 오류");
                }
                Assert.IsTrue(s.SideFacesRight, $"{set}: 옆모습은 오른쪽을 본다 (왼쪽은 flipX)");
                Assert.IsTrue(s.Actions != null && s.Actions.Length > 0, $"{set}: 공격 몸짓 미배선");
                bool hasAttack = false;
                foreach (var a in s.Actions)
                    if (a.Kind == AnimKind.Attack)
                    {
                        hasAttack = true;
                        Assert.IsTrue(a.Down != null && a.Down.Length > 0 && a.Down[0] != null, $"{set}: 공격 Down");
                        Assert.IsTrue(a.Side != null && a.Side.Length > 0 && a.Side[0] != null, $"{set}: 공격 Side");
                        Assert.IsTrue(a.Up != null && a.Up.Length > 0 && a.Up[0] != null, $"{set}: 공격 Up");
                    }
                Assert.IsTrue(hasAttack, $"{set}: AnimKind.Attack 항목이 없다 — 무는 그림이 안 나온다");
            }

            // 🔴 반쪽 배선 금지 — 0(색 원 폴백)이거나 전원이거나 둘 중 하나여야 한다.
            // 일부만 배선되면 화면에서 어떤 위협은 그림이고 어떤 위협은 색 원이라 사고로 읽힌다.
            Assert.IsTrue(wired == 0 || wired == guids.Length,
                $"위협 {guids.Length}종 중 {wired}종만 그림이 배선됐다 — 반쪽 상태 " +
                "(전원 배선하거나, 전원 비워 색 원 폴백으로 둘 것)");
        }

        [Test]
        public void M22_T15_TrapTrigger_SequenceAndConsumption()
        {
            // M22-3차 W4 — 발동 시퀀스 (ADR-M22-13). 씬 없는 EditMode라 ThreatService 경유
            // 실행은 리뷰① 몫 — 서비스 문(조회+소멸 준비)과 소모 규약을 검산한다.
            var d = new DefenseService();
            var trapSO = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/Trap.asset");
            var site = new Vector2Int(4, 1);

            d.AddTrapPlan(new[] { site }, null);
            d.NotifyBuilt(trapSO, site.x, site.y);
            Assert.IsTrue(d.HasTrapAt(site));
            Assert.AreEqual(1, d.BuiltTrapCount);
            int plannedAfterBuild = d.PlannedCount;

            // 발동 = 조회 + 소멸 준비가 한 호출, 피해값 원천은 에셋 (동봉 값)
            Assert.IsTrue(d.TryTriggerTrapAt(site, out float dmg));
            Assert.AreEqual(trapSO.TrapDamage, dmg, 1e-3f, "피해값의 단일 출처 = Trap.asset");
            Assert.IsFalse(d.HasTrapAt(site), "발동 = 소멸");
            Assert.AreEqual(0, d.BuiltTrapCount);
            Assert.IsFalse(d.TryTriggerTrapAt(site, out _), "두 번 터지지 않는다 (소모품)");

            // 소모 = 계획 복귀 없음 (파괴와의 갈림길 — 주민이 도로 깔지 않는다)
            d.NotifyRemoved(SlotId.TrapCount, site.x, site.y); // 제거 문 경유 시 뒤따르는 통지
            Assert.AreEqual(plannedAfterBuild, d.PlannedCount,
                "함정 소모 = 계획 복귀 없음 (재설치는 플레이어 몫, ADR-M22-13)");
            Assert.AreEqual(0, d.TrapPlannedCount);

            // 대사 배선 — 배포 위협 3종에 함정 대사가 있다 (침묵 발동 방지, W5 1차 교훈)
            foreach (string guid in AssetDatabase.FindAssets("t:ThreatSO", new[] { "Assets/M0Config/Threats" }))
            {
                var so = AssetDatabase.LoadAssetAtPath<ThreatSO>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsTrue(so.TrapHitLines != null && so.TrapHitLines.Length > 0,
                    $"{so.name}: TrapHitLines 비어 있음 — 함정은 한 번 쓰고 사라져 침묵하면 관측이 불가능하다");
            }
        }

        [Test]
        public void M22_T14_ManTower_GoalWiring_AndNearestTower()
        {
            // M22-3차 W3 — 탑승 goal·액션 배선 검산 (씬 없는 EditMode — 동선·희생 제외 실행은
            // 리뷰⓪ Play 몫. 배선 오타면 기본값 0/null로 풀려 red).
            var goal = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_ManTower.asset");
            Assert.IsNotNull(goal, "Goal_ManTower 에셋 없음");
            Assert.AreEqual(0, goal.GoalConditions.Length, "탑승은 체류형 — GoalConditions 비움 (Goal_Fight 문법)");
            Assert.AreEqual(2, goal.TriggerConditions.Length, "트리거 = ThreatNear + WatchtowerCount");
            Assert.AreEqual(SlotId.ThreatNear, goal.TriggerConditions[0].Slot,
                "감지는 주민의 ThreatNear다 (ADR-M22-12 — 시설이 부르지 않는다)");
            Assert.AreEqual(SlotId.WatchtowerCount, goal.TriggerConditions[1].Slot,
                "망루가 있어야 오른다 — 단 안전 전제가 아니라 존재 전제다 (ADR-M22-11)");
            Assert.Greater(goal.TraitWeights.Length, 0, "TraitWeights 배선 의무 (M12_T3)");

            // DirectActionPool → ManTowerActionSO → 다형 디스패치 + 수치 단일 출처
            Assert.AreEqual(1, goal.DirectActionPool.Length);
            var act = goal.DirectActionPool[0] as ManTowerActionSO;
            Assert.IsNotNull(act, "탑승 액션은 ManTowerActionSO");
            Assert.IsTrue(act.CreateRunner(null) is ManTowerRunner, "CreateRunner 다형 디스패치 (ADR-M0-1)");
            var fight = AssetDatabase.LoadAssetAtPath<FightActionSO>("Assets/M0Config/Actions/Action_Fight.asset");
            Assert.AreSame(fight, act.CombatSource,
                "타격 수치의 단일 출처 = Action_Fight (망루가 주는 것은 사거리와 안전뿐 — §3)");
            Assert.Greater(act.WatchGiveUpSec, 0f, "감시 상한 없는 탑승은 매복 아사");

            // 최근접 망루 선정 (순수 — 탑승 러너의 목적지)
            var d = new DefenseService();
            var towerSO = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/Watchtower.asset");
            d.NotifyBuilt(towerSO, 10, 0);
            d.NotifyBuilt(towerSO, 3, 0);
            Assert.IsTrue(d.TryGetNearestBuiltTile(SlotId.WatchtowerCount, new Vector2Int(0, 0), out Vector2Int t));
            Assert.AreEqual(new Vector2Int(3, 0), t, "최근접 망루");
            Assert.IsFalse(d.TryGetNearestBuiltTile(SlotId.TrapCount, new Vector2Int(0, 0), out _),
                "함정은 내구도 등록부에 없다 — 슬롯 지정판이 빈 종을 안 섞는다");
        }

        [Test]
        public void M22_T12_TowerTrap_ShippedAssets()
        {
            // M22-3차 W1 — 배포 에셋 검산 (T2 동형). YAML 필드명 오타면 기본값 0으로 풀려 red.
            var tower = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/Watchtower.asset");
            var trap = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/Trap.asset");
            Assert.IsNotNull(tower, "Watchtower 에셋 없음");
            Assert.IsNotNull(trap, "Trap 에셋 없음");

            // 망루: 내구도 있는 공성 대상 + 탑승 사거리 (ADR-M22-11)
            Assert.AreEqual(SlotId.WatchtowerCount, tower.CountSlot);
            Assert.IsTrue(tower.IsCountable && tower.PlaceOnDefensePlan, "망루 = 방어 계획 수량형");
            Assert.Greater(tower.MaxDurability, 100f, "망루는 울타리(100)보다 단단 — 공성이 시간을 사게");
            Assert.Greater(tower.RepairCost, 0, "망루도 수리 대상");
            Assert.IsTrue(tower.BlocksMovement && !tower.BlocksThreatMovement, "망루 = 양쪽 차단 몸체");
            Assert.AreEqual(0f, tower.TrapDamage, "망루는 함정이 아니다 (배타)");
            // 탑승 사거리 > 기존 지상 교전 사거리 (FightActionSO.StrikeRangeTiles, 기본 2) —
            // 망루가 맨몸 교전보다 멀리 닿아야 올라갈 이유가 있다 (명세 전제 ③ 정정:
            // 주민 사거리의 실물은 ThreatSO가 아니라 FightActionSO다)
            var fight = AssetDatabase.LoadAssetAtPath<FightActionSO>("Assets/M0Config/Actions/Action_Fight.asset");
            Assert.IsNotNull(fight, "Action_Fight 에셋 없음");
            Assert.Greater(tower.TowerRangeTiles, fight.StrikeRangeTiles,
                "망루 사거리는 지상 교전 사거리보다 길어야 존재 이유가 있다");

            // 함정: 소모형 소프트닝 (ADR-M22-13 + 사용자 확정 15 — 한 방 격퇴 금지)
            Assert.AreEqual(SlotId.TrapCount, trap.CountSlot);
            Assert.IsTrue(trap.IsCountable && trap.PlaceOnDefensePlan, "함정 = 방어 계획 수량형");
            Assert.Greater(trap.TrapDamage, 0f, "함정은 피해를 준다");
            Assert.AreEqual(0f, trap.MaxDurability, "함정은 소모지 내구도가 아니다 (배타·다회용 금지)");
            Assert.IsFalse(trap.BlocksMovement || trap.BlocksThreatMovement,
                "함정은 밟혀야 한다 — 통행 양쪽 허용");
            // 🔑 M24-1차 개정 — 가장 약한 위협(UnlockDay 최소)이 구속 조건이다. 이름으로 붙잡지 않는다.
            ThreatSO wolf = null;
            foreach (string g in AssetDatabase.FindAssets("t:ThreatSO"))
            {
                var t = AssetDatabase.LoadAssetAtPath<ThreatSO>(AssetDatabase.GUIDToAssetPath(g));
                if (t != null && (wolf == null || t.UnlockDay < wolf.UnlockDay)) wolf = t;
            }
            Assert.IsNotNull(wolf, "배포 위협 에셋이 하나도 없다");
            Assert.Greater(wolf.MaxHp - trap.TrapDamage, wolf.MaxHp * wolf.FleeBelowHpPct,
                "한 방 격퇴 금지 (사용자 확정 15) — 함정은 소프트닝이다. 값을 올려 한 방이 되면 red");

            // W5b 실그림 배선 (M23_T1 문법) — 임시 색 사각으로 되돌아가면 red.
            Assert.IsNotNull(tower.MarkerSprite, "망루 실그림 미배선 — 임시 색 사각으로 회귀");
            Assert.IsNotNull(trap.MarkerSprite, "함정 실그림 미배선 — 임시 색 사각으로 회귀");
            // 🔁 2026-08-09 개정 (W5d, 구매 팩 교체): 舊 규칙은 **배율 1 고정**이었다 —
            // 자작 시트를 타일에 맞춰 구웠으니 배율이 1이 아니면 오배선이라는 뜻이었다.
            // 남의 팩은 제 크기로 그려져 온다(망루 48×128 = 3×8타일) 탓에 그 전제가 깨졌다.
            // 불변식을 "배율 1"에서 **"화면에 서는 크기"**로 옮긴다 — 지키려던 것은 배율이
            // 아니라 한 타일짜리 시설이 화면에서 말이 되는 크기라는 것이었다.
            foreach (var b in new[] { tower, trap })
            {
                Assert.Greater(b.FallbackSize, 0f, $"{b.name}: 배율 0 = 안 보인다");
                Assert.IsNotNull(b.MarkerSprite.texture, $"{b.name}: 스프라이트 텍스처 없음");
                float wTiles = b.MarkerSprite.rect.width  * b.FallbackSize / b.MarkerSprite.pixelsPerUnit;
                float hTiles = b.MarkerSprite.rect.height * b.FallbackSize / b.MarkerSprite.pixelsPerUnit;
                Assert.That(wTiles, Is.InRange(0.5f, 4f), $"{b.name}: 화면 폭 {wTiles:0.00}타일 — 한 칸 시설이 아니다");
                Assert.That(hTiles, Is.InRange(0.5f, 9f), $"{b.name}: 화면 높이 {hTiles:0.00}타일 — 한 칸 시설이 아니다");
                AssertStandsOnTile(b, hTiles);
            }
            // 탑승 높이는 **그림이 정한다** — 코드 상수(舊 0.45)로 되돌리면 사람이 다리 사이에 뜬다
            Assert.Greater(tower.MountOffsetTiles, 1f,
                "망루 탑승 높이(MountOffsetTiles)가 발판 높이와 어긋난다 — 그림과 짝인 값이다");
            Assert.AreEqual(0f, trap.MountOffsetTiles, "함정은 올라서는 시설이 아니다");
        }

        /// <summary>그림이 제 타일 위에 서 있는가 (W5d). 피벗은 늘 한가운데라, 키 큰 그림을
        /// 보정 없이 놓으면 절반이 땅에 묻힌다 (망루 5.2타일 → 밑동 −2.6타일).
        /// ⚠️ rect 높이로 재는 값이라 **아래 여백까지 키에 포함**된다(함정 rect 48px 중 20px가
        /// 여백) — 그래서 기준을 빡빡하게 잡지 않는다. 잡으려는 것은 오배선(보정 누락)이지
        /// 픽셀 단위 정렬이 아니다. 정렬은 사람이 화면에서 본다.</summary>
        private static void AssertStandsOnTile(BuildingSO b, float hTiles)
        {
            float bottom = b.MarkerOffsetTiles.y - hTiles * 0.5f; // 그림 아랫변의 타일 좌표
            Assert.GreaterOrEqual(bottom, -1.5f,
                $"{b.name}: 그림 아랫변이 {bottom:0.00}타일 — 땅에 묻혔다. " +
                "키운 그림을 넣었으면 MarkerOffsetTiles.y로 올려라 (표현 수치는 에셋의 몫).");
        }

        [Test]
        public void M22_T17_PurchasedPackArt_ShippedWiring()
        {
            // M22-3차 W5d — 구매 팩(Kenmi War Camp) 교체 배선 검산. M22_T16과 같은 정신:
            // **아트 교체 지점이 에셋 한 칸임**을 박제한다. 팩 재임포트로 fileID가 갈리면
            // 여기가 먼저 red가 된다 (게임을 켜서 "왜 원이 뜨지"를 겪기 전에).
            BuildingSO Load(string n) =>
                AssetDatabase.LoadAssetAtPath<BuildingSO>($"Assets/M0Config/Buildings/{n}.asset");

            // ── 울타리: 16조각 전수 + 가로/세로 분화 ──
            // 조각 종류가 3개로 줄어도(팰리세이드 판독) **가로와 세로는 반드시 달라야** 한다 —
            // 같아지면 오토타일이 죽고 줄이 한 방향으로만 읽힌다.
            BuildingSO fence = Load("Fence");
            Assert.IsNotNull(fence, "Fence 에셋 없음");
            Assert.IsNotNull(fence.TileSprites, "울타리: TileSprites 미배선");
            Assert.AreEqual(16, fence.TileSprites.Length, "울타리: 4방 마스크는 16칸이다");
            for (int i = 0; i < 16; i++)
                Assert.IsNotNull(fence.TileSprites[i], $"울타리: TileSprites[{i}] null — fileID 오배선");
            Assert.AreNotSame(fence.TileSprites[2], fence.TileSprites[4],
                "울타리: 가로중간(2)과 세로중간(4)이 같은 그림 — 오토타일이 죽었다");
            Assert.IsNotNull(fence.MarkerSprite, "울타리: 대표 그림 미배선 — 계획 고스트가 원으로 떨어진다");

            // ── 문: 제 그림이 상태를 말한다 (2026-08-09 개정) ──
            // 舊 규약은 "문도 울타리 조각을 입고 금빛 색조로 구분"이었다(M22-2차 W3). 그때는 문에
            // 제 그림이 없어 그게 최선이었지만, 같은 팩에 여닫는 문 그림이 들어오면서 **잠금 표식을
            // 지우고 문이 스스로 말하게** 바꿨다. 조각을 다시 채우면 그 표현이 죽으므로 red 로 잡는다.
            BuildingSO gate = Load("Gate");
            Assert.IsNotNull(gate, "Gate 에셋 없음");
            Assert.IsTrue(gate.TileSprites == null || gate.TileSprites.Length == 0,
                "문은 제 그림을 쓴다 — TileSprites를 채우면 울타리 조각이 문 그림을 덮는다");
            Assert.IsNotNull(gate.MarkerSprite, "문: 대표 그림 미배선 — 계획 고스트가 원으로 떨어진다");
            Assert.IsTrue(gate.MarkerFrames != null && gate.MarkerFrames.Length > 0
                          && gate.MarkerFrames[0] != null, "문: 평상(열린 문) 프레임 미배선");
            Assert.IsTrue(gate.AltFrames != null && gate.AltFrames.Length > 0
                          && gate.AltFrames[0] != null, "문: 잠김(닫힌 문) 프레임 미배선 — L이 화면에서 사라진다");
            Assert.AreNotSame(gate.MarkerFrames[0], gate.AltFrames[0],
                "문: 열림과 잠김이 같은 그림 — 잠갔는지 알 수 없다");
            // 밑동이 같아야 갈아입을 때 문이 튀지 않는다 (평상·대체 규격 규약)
            Assert.AreEqual(gate.MarkerFrames[0].rect.height, gate.AltFrames[0].rect.height, 1e-3f,
                "문: 열림·잠김 프레임의 세로 규격이 다르다 — 갈아입을 때 밑동이 튄다");

            // ── 모닥불: 평상 애니 + 조리 전환 ──
            // BusyFrames가 비면 "요리를 시작하면 솥이 걸린다"가 조용히 사라진다 (사용자 요청 지점).
            BuildingSO fire = Load("Campfire");
            Assert.IsNotNull(fire, "Campfire 에셋 없음");
            Assert.IsNotNull(fire.MarkerSprite, "모닥불: 대표 그림 미배선 — 주황 원으로 회귀");
            Assert.IsTrue(fire.MarkerFrames != null && fire.MarkerFrames.Length >= 2,
                "모닥불: 평상 프레임 2장 미만 — 불이 안 흔들린다");
            Assert.IsTrue(fire.AltFrames != null && fire.AltFrames.Length >= 1,
                "모닥불: AltFrames 미배선 — 조리 중 솥 전환이 사라진다");
            foreach (Sprite s in fire.MarkerFrames) Assert.IsNotNull(s, "모닥불 평상 프레임에 null");
            foreach (Sprite s in fire.AltFrames) Assert.IsNotNull(s, "모닥불 조리 프레임에 null");
            Assert.Greater(fire.MarkerFps, 0f, "모닥불: FPS 0이면 첫 프레임에 굳는다");
            // 평상/조리는 세로 규격이 같아야 밑동이 안 튄다 (갈아입을 때 불이 뛰어오르면 안 된다)
            Assert.AreEqual(fire.MarkerFrames[0].rect.height, fire.AltFrames[0].rect.height, 1e-3f,
                "모닥불: 평상·조리 프레임 높이가 다르다 — 갈아입을 때 밑동이 튄다");

            // ── 집: 실그림 + 파묻힘 방지 ──
            BuildingSO house = Load("House");
            Assert.IsNotNull(house, "House 에셋 없음");
            Assert.IsNotNull(house.MarkerSprite, "집: 실그림 미배선");
            float houseH = house.MarkerSprite.rect.height * house.FallbackSize
                           / house.MarkerSprite.pixelsPerUnit;
            Assert.That(houseH, Is.InRange(1f, 5f), $"집 화면 높이 {houseH:0.00}타일 — 한 칸 건물이 아니다");
            AssertStandsOnTile(house, houseH);
            float fireH = fire.MarkerSprite.rect.height * fire.FallbackSize
                          / fire.MarkerSprite.pixelsPerUnit;
            AssertStandsOnTile(fire, fireH);
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

        // ── M22 4차: 개간 (Docs/M22_4차_개간_실행명세서.md) ────────────────────
        //
        // 🔴 이 절이 막는 것은 **조용한 구멍**이다. 사용자 Play(2026-08-10): "울타리를 설치하는데
        //    근처에 나무가 있으면 설치가 안 됐어. 그 사이로 고블린이 들어오고 밭을 다 없애고 다시
        //    그 빈틈으로 나갔어." 계획이 막힌 칸을 말없이 건너뛴 것이 원인이었다.

        /// <summary>시험용 노드 — 배포 스폰과 무관하게 손으로 세운다 (스폰 설정이 바뀌어도 안 흔들린다).
        /// ⚠️ 고정한 축을 적는다 (방법론 M20): 여기서 고정한 것은 **좌표와 잔량**뿐이고,
        /// 맵 크기·마을 위치는 이 검사와 무관하다 (서비스가 좌표를 그대로 받는다).</summary>
        private static ResourceNode Node(int x, int y, float amount = 10f)
            => new ResourceNode($"gate_{x}_{y}", ResourceType.Wood, x, y, 50f, 1f, true)
               { CurrentAmount = amount };

        /// <summary>소스에서 **실제 호출**만 센다 — 주석(`//`·`///`·`*`) 줄은 빼고 본다.
        /// 산문에 적힌 같은 문자열까지 세면, 규약을 설명한 주석 때문에 게이트가 빨개진다
        /// (2026-08-10 실제로 그랬다 — 이 헬퍼가 그 자리에서 생겼다).</summary>
        private static int CountCalls(string src, string needle)
        {
            int n = 0;
            foreach (string raw in src.Split('\n'))
            {
                string line = raw.TrimStart();
                if (line.StartsWith("//") || line.StartsWith("*") || line.StartsWith("/*")) continue;
                int i = 0;
                while ((i = line.IndexOf(needle, i, System.StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            }
            return n;
        }

        [Test]
        public void M22_T18_RemoveNode_IsTheOnlyDoor()
        {
            // ADR-C-2 — 제거는 이력·지정 해제·점유 해제·뷰 소멸을 한꺼번에 끌고 다닌다.
            // 문이 둘이면 반드시 한쪽이 빠뜨린다. 그래서 **소스를 전수로 읽어** 문을 센다.
            // 🔑 **주석은 안 센다.** 첫 실행에서 이 게이트가 잡은 것은 코드가 아니라 내가
            //    RemoveNode 위에 적어 둔 "다른 곳에서 부르지 말 것" **주석 속의 같은 문자열**이었다.
            //    문자열 검사 게이트는 산문을 코드로 읽는다 — 세는 대상을 먼저 좁힌다.
            string src = System.IO.File.ReadAllText("Assets/Scripts/M0/World/DiscoveryService.cs");
            int doors = CountCalls(src, "_nodes.Remove(");
            Assert.AreEqual(1, doors,
                $"_nodes.Remove( 호출이 {doors}곳 — 제거의 문은 RemoveNode 하나여야 한다 (ADR-C-2)");

            // 실패 가능성 증명 — 검사 대상이 실제로 소스에 있다 (오타면 0곳이라 조용히 '통과'한다).
            Assert.IsTrue(src.Contains("public int RemoveNode("), "RemoveNode 자체가 없다 — 빈 검사다");

            // 다른 파일이 우회로 지우는 것도 막는다.
            foreach (string path in System.IO.Directory.GetFiles("Assets/Scripts", "*.cs",
                                                                 System.IO.SearchOption.AllDirectories))
            {
                if (path.Replace('\\', '/').EndsWith("World/DiscoveryService.cs")) continue;
                Assert.AreEqual(0, CountCalls(System.IO.File.ReadAllText(path), "_nodes.Remove("),
                    $"{path}: 노드 제거는 DiscoveryService.RemoveNode 를 지나야 한다");
            }
        }

        [Test]
        public void M22_T19_ClearedTilesAreRecordedAndNodeIsGone()
        {
            var d = new DiscoveryService();
            ResourceNode n = Node(3, 4, 7.9f);
            d.AddResourceNode(n);

            // 실패 가능성 증명 — 지우기 전에는 있다·이력에 없다 (이게 거짓이면 아래가 빈 검사다).
            Assert.IsTrue(d.HasNodeAt(3, 4), "지우기 전에 노드가 없다 — 빈 검사다");
            Assert.IsFalse(d.WasCleared(3, 4), "지우기 전에 이미 개간 이력이 있다 — 빈 검사다");

            int recovered = d.RemoveNode(n);
            Assert.AreEqual(7, recovered, "회수 = 잔량의 내림 (7.9 → 7, ADR-C-6)");
            Assert.IsFalse(d.HasNodeAt(3, 4), "제거 후에도 타일이 막혀 있다 — 울타리가 못 선다");
            Assert.IsTrue(d.WasCleared(3, 4), "개간 이력이 없다 — M26 리스폰이 여기 나무를 도로 심는다 (ADR-C-3)");
            Assert.IsTrue(n.IsRemoved, "IsRemoved 가 안 섰다 — 뷰가 영영 안 사라진다");

            // 두 번째 제거는 조용히 0 (이벤트도 안 뜬다 — 승격이 두 번 돌면 계획이 겹친다).
            int again = 0, fired = 0;
            d.OnNodeRemoved += (_, __) => fired++;
            again = d.RemoveNode(n);
            Assert.AreEqual(0, again, "이미 없는 노드를 또 지웠는데 회수가 났다");
            Assert.AreEqual(0, fired, "이미 없는 노드인데 알림이 떴다");
        }

        [Test]
        public void M22_T20_ClearPendingNodesDoNotRegenerate()
        {
            // ADR-C-7 — 차오르면 회수량이 지연 시간에 비례해 "늦게 칠수록 이득"이 된다.
            var d = new DiscoveryService();
            ResourceNode pending = Node(1, 1, 5f), normal = Node(2, 2, 5f);
            d.AddResourceNode(pending);
            d.AddResourceNode(normal);
            Assert.IsTrue(d.MarkForClearing(pending), "개간 지정이 안 걸렸다");
            Assert.AreEqual(1, d.ClearPendingCount, "지정 수가 안 맞는다");
            Assert.IsFalse(d.MarkForClearing(pending), "같은 노드가 두 번 지정됐다");
            Assert.AreEqual(1, d.ClearPendingCount, "중복 지정으로 수가 늘었다");

            d.TickRegeneration(10f);
            Assert.AreEqual(5f, pending.CurrentAmount, 1e-3f, "개간 대기 노드가 재생했다 (ADR-C-7 위반)");
            // 실패 가능성 증명 — 같은 틱에서 **비지정 노드는 실제로 오른다**.
            Assert.Greater(normal.CurrentAmount, 5f, "비지정 노드도 안 올랐다 — 이 게이트는 빈 검사다");
        }

        [Test]
        public void M22_T21_FenceLineReportsBlockedCount()
        {
            // 🔴 이 차수의 이유. 舊 코드는 막힌 칸을 조용히 건너뛰고 "8칸"이라고만 말했다.
            var def = new DefenseService();
            var line = new List<Vector2Int>();
            for (int x = 0; x < 8; x++) line.Add(new Vector2Int(x, 0));
            bool IsNode(int x, int y) => x == 3;                    // (3,0) 한 칸만 나무
            bool Buildable(int x, int y) => !IsNode(x, y);

            DefenseService.PlanResult r = def.AddFencePlan(line, Buildable, IsNode);
            Assert.AreEqual(7, r.Added, "계획 칸 수가 안 맞는다");
            Assert.AreEqual(1, r.Blocked, "막힌 칸을 안 셌다 — 플레이어는 구멍을 모른 채 넘어간다");
            Assert.IsFalse(r.Nothing, "계획도 대기도 있는데 Nothing 이다");
            CollectionAssert.Contains(def.BlockedFenceTiles, new Vector2Int(3, 0), "대기 목록에 없다");

            // 실패 가능성 증명 — 막힌 칸이 0이면 Blocked 도 0이어야 한다 (늘 1이면 빈 검사다).
            var clean = new DefenseService();
            DefenseService.PlanResult r2 = clean.AddFencePlan(line, (x, y) => true, (x, y) => false);
            Assert.AreEqual(8, r2.Added);
            Assert.AreEqual(0, r2.Blocked, "안 막혔는데 대기가 생겼다 — 화면에 헛경고가 뜬다");

            // 같은 줄을 다시 그어도 대기가 중복되지 않는다.
            def.AddFencePlan(line, Buildable, IsNode);
            Assert.AreEqual(1, def.BlockedFenceTiles.Count, "대기 칸이 중복됐다");
        }

        [Test]
        public void M22_T22_BlockedTilesAreNotCountedAsPlanned()
        {
            // ADR-C-4 — 대기를 DefensePlannedCount 에 세면 목수 goal 이 켜지고 **공회전**한다.
            var def = new DefenseService();
            var line = new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(1, 0) };
            def.AddFencePlan(line, (x, y) => x != 1, (x, y) => x == 1);
            Assert.AreEqual(1, def.PlannedCount,
                "대기 칸이 방어 계획 수에 섞였다 — 목수가 못 짓는 칸을 보고 goal 이 공회전한다");
            Assert.AreEqual(1, def.BlockedFenceTiles.Count, "대기 칸이 안 잡혔다 — 빈 검사다");
        }

        [Test]
        public void M22_T23_ClearedTileIsPromotedToFencePlan()
        {
            var def = new DefenseService();
            var line = new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(1, 0) };
            def.AddFencePlan(line, (x, y) => x != 1, (x, y) => x == 1);
            int before = def.PlannedCount;

            Assert.IsTrue(def.PromoteClearedTile(new Vector2Int(1, 0)), "대기 칸인데 승격이 안 됐다");
            Assert.AreEqual(before + 1, def.PlannedCount, "승격했는데 계획 수가 안 늘었다 — 담이 안 이어진다");
            CollectionAssert.Contains(def.PlannedFenceTiles, new Vector2Int(1, 0));
            Assert.AreEqual(0, def.BlockedFenceTiles.Count, "승격 후에도 대기에 남아 있다");

            // 대기가 아니었던 칸은 승격되지 않는다 (아무 노드나 치웠다고 담이 생기면 안 된다).
            Assert.IsFalse(def.PromoteClearedTile(new Vector2Int(9, 9)),
                "대기한 적 없는 칸이 승격됐다 — 그은 적 없는 곳에 담이 선다");
        }

        [Test]
        public void M22_T24_NonNodeBlockersDoNotBecomePending()
        {
            // 물·맵 밖·기존 건물은 개간해도 못 짓는다 — 대기로 넣으면 **영영 안 닫히는 계획**이 된다.
            var def = new DefenseService();
            var line = new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(1, 0) };
            // (1,0)은 막혔지만 노드 때문이 아니다 (blockedByNode 가 false).
            DefenseService.PlanResult r = def.AddFencePlan(line, (x, y) => x != 1, (x, y) => false);
            Assert.AreEqual(1, r.Added);
            Assert.AreEqual(0, r.Blocked, "노드가 아닌 이유로 막힌 칸이 개간 대기가 됐다");
            Assert.AreEqual(0, def.BlockedFenceTiles.Count, "〃 대기 목록에 들어갔다");

            // 배선 전 중립 — blockedByNode 를 안 넘기면 舊 동작 그대로다.
            var old = new DefenseService();
            DefenseService.PlanResult r2 = old.AddFencePlan(line, (x, y) => x != 1);
            Assert.AreEqual(1, r2.Added);
            Assert.AreEqual(0, r2.Blocked, "미배선인데 대기가 생겼다 (중립 불변식 위반)");
        }

        [Test]
        public void M22_T25_ClearCostsSameAsGathering()
        {
            // 🔴 착취 방지 (ADR-C-6). 회수를 전부 주면서 소요를 채집 1회로 두면 개간이
            //    **5배 빠른 벌목**이 된다 — 플레이어가 담이 아니라 자원을 노리고 줄을 긋는다.
            //    배포 에셋에서 읽어 검사한다 (상수를 박으면 에셋이 바뀔 때 착취가 조용히 되살아난다).
            var clear = AssetDatabase.LoadAssetAtPath<ClearActionSO>("Assets/M0Config/Actions/ClearNode.asset");
            var chop  = AssetDatabase.LoadAssetAtPath<GatherActionSO>("Assets/M0Config/Actions/ChopWood.asset");
            Assert.IsNotNull(clear, "ClearNode 액션 에셋 없음 — 개간 goal 이 계획을 못 세운다");
            Assert.IsNotNull(chop, "ChopWood 에셋 없음");
            Assert.Greater(chop.DurationSec, 0f, "채집 소요가 0 — 이 검사는 나눗셈이 안 된다");

            int yield = 0;
            foreach (SlotEffect e in chop.Effects)
                if (e.Op == EffectOp.Add && SlotIds.IsStock(e.Slot)) { yield = e.Value; break; }
            Assert.Greater(yield, 0, "채집 1회 수확량을 에셋에서 못 읽었다 — 배수의 근거가 없다");

            // 기준 1회분이 같아야 배수가 그대로 총 작업량이 된다.
            Assert.AreEqual(chop.DurationSec, clear.DurationSec, 1e-3f,
                "개간의 기준 소요가 채집 1회와 다르다 — 배수를 곱해도 효율이 안 맞는다");

            // 계획이 이 액션으로 goal 을 만족시킬 수 있는가 (플래너 계약 — ADR-C-1의 기각 근거).
            bool declares = false;
            foreach (SlotEffect e in clear.Effects)
                if (e.Slot == SlotId.ClearPendingCount && e.Op == EffectOp.SubClamp0) declares = true;
            Assert.IsTrue(declares,
                "개간 액션이 ClearPendingCount 감소를 선언하지 않았다 — 플래너가 Goal_Clear 를 못 푼다");

            // goal 쪽도 한 걸음 증분이어야 한다 (ADR-M0-12).
            var goal = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_Clear.asset");
            Assert.IsNotNull(goal, "Goal_Clear 에셋 없음");
            Assert.IsTrue(goal.RelativeToCurrent, "Goal_Clear 가 절대목표다 — 대기가 많으면 플래너가 무너진다");
        }

        [Test]
        public void M22_T26_ClearGestureFollowsTheResource()
        {
            // 사용자 관측 2026-08-10: "돌을 도끼로 치는 건 좀 거슬리긴 하지."
            // 액션 하나가 나무·돌을 다 상대하는데 ActionSO.Anim 은 한 값뿐이라 생긴 자리다.
            // 🔑 코드가 몸짓을 고르지 않는다 (ADR-M23-1) — **그 자원을 캐는 액션의 Anim 을 빌린다.**
            var cat = AssetDatabase.LoadAssetAtPath<ActionCatalog>("Assets/M0Config/ActionCatalog.asset");
            Assert.IsNotNull(cat, "ActionCatalog 없음");

            AnimKind wood  = ClearRunner.AnimForResource(cat, ResourceType.Wood);
            AnimKind stone = ClearRunner.AnimForResource(cat, ResourceType.Stone);

            // 실패 가능성 증명 — 둘 다 None 이면 "다르다"가 무의미해진다.
            Assert.AreNotEqual(AnimKind.None, wood, "나무 개간 몸짓을 못 찾았다 — 빈 검사다");
            Assert.AreNotEqual(AnimKind.None, stone, "돌 개간 몸짓을 못 찾았다 — 빈 검사다");
            Assert.AreNotEqual(wood, stone,
                $"나무와 돌의 개간 몸짓이 같다 ({wood}) — 돌을 도끼로 팬다");

            // 출처가 채집 액션임을 못박는다 (여기가 어긋나면 코드가 몰래 표를 갖게 된 것이다).
            var chop = AssetDatabase.LoadAssetAtPath<GatherActionSO>("Assets/M0Config/Actions/ChopWood.asset");
            var mine = AssetDatabase.LoadAssetAtPath<GatherActionSO>("Assets/M0Config/Actions/MineStone.asset");
            Assert.AreEqual(chop.Anim, wood, "나무 개간 몸짓이 ChopWood 에셋과 다르다");
            Assert.AreEqual(mine.Anim, stone, "돌 개간 몸짓이 MineStone 에셋과 다르다");

            // 짝이 없는 자원은 None → 액션 에셋 값 그대로 (중립 폴백).
            Assert.AreEqual(AnimKind.None, ClearRunner.AnimForResource(null, ResourceType.Wood),
                "카탈로그 미배선인데 몸짓을 지목했다 (중립 불변식 위반)");
        }

        [Test]
        public void M22_T27_CancellingThePlanCancelsTheClearing()
        {
            // 🔴 2차 Play 관측 → 사용자 판정 안 B (2026-08-10). 舊 동작은 계획을 취소해도
            //    개간 지정이 남아, 담을 세울 이유가 없어진 자리의 나무를 주민이 끝내 벴다.
            var def = new DefenseService();
            var line = new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(1, 0) };
            def.AddFencePlan(line, (x, y) => x != 1, (x, y) => x == 1);

            // 실패 가능성 증명 — 취소 전에는 대기 칸이 실제로 있다.
            Assert.AreEqual(1, def.BlockedFenceTiles.Count, "대기 칸이 없다 — 빈 검사다");

            Assert.IsTrue(def.RemovePlanAt(new Vector2Int(1, 0), out bool wasBlocked),
                "대기 칸인데 취소가 안 됐다");
            Assert.IsTrue(wasBlocked,
                "대기 칸을 지웠는데 그 사실을 안 알려 준다 — 호출자가 노드 지정을 못 푼다");
            Assert.AreEqual(0, def.BlockedFenceTiles.Count);

            // 일반 계획 칸을 지운 것은 개간과 무관하다 (아무 취소나 나무를 살려 주면 안 된다).
            Assert.IsTrue(def.RemovePlanAt(new Vector2Int(0, 0), out bool plainBlocked));
            Assert.IsFalse(plainBlocked, "노드와 무관한 계획 칸인데 개간 대기였다고 답했다");

            // 서비스 쪽 — 지정 해제와 재확인이 짝을 이룬다.
            var d = new DiscoveryService();
            ResourceNode n = Node(1, 0);
            d.AddResourceNode(n);
            d.MarkForClearing(n);
            Assert.IsTrue(d.IsPendingClear(n), "지정했는데 대기가 아니다 — 빈 검사다");
            Assert.IsTrue(d.UnmarkForClearing(n), "지정 해제가 안 됐다");
            Assert.IsFalse(d.IsPendingClear(n),
                "해제했는데 여전히 대기다 — 도끼질 중인 주민이 끝내 베어 버린다");
            Assert.AreEqual(0, d.ClearPendingCount);
            Assert.IsFalse(d.UnmarkForClearing(n), "이미 푼 지정을 또 풀었다고 답했다");
        }
    }
}
