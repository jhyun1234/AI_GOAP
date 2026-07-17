using AIVillage.Core.GOAP;
using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M6 계절 게이트 (명세 M6-A~B). 이 파일이 M6-T1~T2의 집이다 —
    /// A(계절 시계 순수 계산·슬롯 중립 불변식)를 시작으로 B(배율 산식) 게이트가 뒤에 추가된다.
    /// </summary>
    public class M6_SeasonGates
    {
        private static SeasonSO MakeSeason(string name, float days, bool crisis)
        {
            var s = ScriptableObject.CreateInstance<SeasonSO>();
            s.name = name;
            s.DisplayName = name;
            s.DurationDays = days;
            s.IsCrisis = crisis;
            return s;
        }

        private static void DestroyAll(params Object[] objs)
        {
            foreach (Object o in objs) Object.DestroyImmediate(o);
        }

        // ── M6-T1: Compute 순수 계산 (사이클 순환 + 위기 카운트다운) ─────────

        [Test]
        public void M6_T1_Compute_CycleAndCrisisCountdown()
        {
            SeasonSO mild = MakeSeason("온화", 6f, false);
            SeasonSO winter = MakeSeason("겨울", 3f, true);
            var cycle = new[] { mild, winter };

            void AssertAt(float t, int expectIndex, float expectDays, string why)
            {
                SeasonService.Compute(cycle, t, out int index, out float days, out _);
                Assert.AreEqual(expectIndex, index, $"t={t}: {why} (계절)");
                Assert.AreEqual(expectDays, days, 1e-4f, $"t={t}: {why} (위기까지)");
            }

            AssertAt(0f,    0, 6f,   "시작 = 온화, 겨울까지 6일");
            AssertAt(5.5f,  0, 0.5f, "예고 구간 — 스냅샷 올림이 1일이 되는 값");
            AssertAt(6f,    1, 0f,   "겨울 진입 경계 — 위기 중 DaysToCrisis 0 (⚠️② 99 되돌림 금지)");
            AssertAt(8.99f, 1, 0f,   "겨울 끝단");
            AssertAt(9f,    0, 6f,   "사이클 순환 — 두 번째 온화");
            AssertAt(20f,   0, 4f,   "2바퀴째 위상 (20 % 9 = 2 → 겨울까지 4)");

            DestroyAll(mild, winter);
        }

        [Test]
        public void M6_T1_Compute_NoCrisisCycle_ReturnsNoCrisis()
        {
            // 위기 계절이 없는 사이클 — 카운트다운 대신 관례값 (트리거 "≤N" 전부 불발)
            SeasonSO mild = MakeSeason("온화", 6f, false);
            SeasonSO autumn = MakeSeason("가을", 2f, false);
            var cycle = new[] { mild, autumn };

            SeasonService.Compute(cycle, 1f, out _, out float days, out _);
            Assert.AreEqual(SeasonService.NO_CRISIS, days, "위기 없음 = NO_CRISIS(99)");

            DestroyAll(mild, autumn);
        }

        [Test]
        public void M6_T1_Service_TickCachesAndFiresChangeOnce()
        {
            SeasonSO mild = MakeSeason("온화", 6f, false);
            SeasonSO winter = MakeSeason("겨울", 3f, true);
            var service = new SeasonService(new[] { mild, winter });
            Assert.IsTrue(service.IsActive);

            int changes = 0;
            SeasonSO last = null;
            service.OnSeasonChanged += s => { changes++; last = s; };

            service.Tick(0f);
            Assert.AreSame(mild, service.Current);
            Assert.AreEqual(1, changes, "첫 Tick도 전환 1회 (HUD 초기화용)");

            service.Tick(3f);
            Assert.AreEqual(1, changes, "같은 계절 내 재발화 없음");
            Assert.AreEqual(3f, service.DaysToCrisis, 1e-4f, "카운트다운은 매 틱 갱신");

            service.Tick(6.5f);
            Assert.AreSame(winter, service.Current);
            Assert.AreSame(winter, last);
            Assert.AreEqual(2, changes, "겨울 진입 시 전환 1회");
            Assert.AreEqual(0f, service.DaysToCrisis, 1e-4f, "위기 중 0");

            DestroyAll(mild, winter);
        }

        [Test]
        public void M6_T1_Service_EmptyCycle_InactiveNoThrow()
        {
            // 빈/전부 null 사이클 = 비활성 — 크래시 없음 (명세 M6-A DoD)
            var empty = new SeasonService(null);
            Assert.IsFalse(empty.IsActive);
            Assert.DoesNotThrow(() => empty.Tick(5f));
            Assert.IsNull(empty.Current);
            Assert.AreEqual(SeasonService.NO_CRISIS, empty.DaysToCrisis);

            var allNull = new SeasonService(new SeasonSO[] { null, null });
            Assert.IsFalse(allNull.IsActive);
        }

        // ── M6-T1b: 중립 불변식 — 계절 미배선 = M5와 동일 판정 ───────────────

        private static WorldSnapshot Snap(params (SlotId slot, int value)[] pairs)
        {
            var slots = new int[PlanningConfig.TotalSlots];
            foreach ((SlotId slot, int value) in pairs) slots[(int)slot] = value;
            return new WorldSnapshot(slots);
        }

        [Test]
        public void M6_T1b_NeutralInvariant_UnwiredSeasonSlotDefaults()
        {
            // season 미배선 WorldModel — 새 슬롯이 "위기 없음" 중립값 (99/0)
            var world = new WorldModel(null, null);
            WorldSnapshot snap = world.BuildSnapshot(50, 50);
            Assert.AreEqual(99, snap.Get(SlotId.DaysToCrisis), "미배선 = NO_CRISIS 99");
            Assert.AreEqual(0, snap.Get(SlotId.CrisisActive), "미배선 = 위기 아님");

            // 위기 트리거 goal(DaysToCrisis ≤ 3)이 중립값에서 절대 발동하지 않는다
            var prep = ScriptableObject.CreateInstance<GoalSO>();
            prep.Priority = 30;
            prep.TriggerConditions = new[]
            {
                new SlotCondition { Slot = SlotId.DaysToCrisis, Op = CompareOp.LessOrEqual, Value = 3 },
            };
            prep.GoalConditions = new[]
            {
                new SlotCondition { Slot = SlotId.CookedFoodStock, Op = CompareOp.GreaterOrEqual, Value = 10 },
            };
            var selector = new GoalSelector(new[] { prep });

            Assert.IsNull(selector.Select(snap), "계절 미배선 세계에서 위기 goal 미발동 (M6-S6)");

            // 발동 조건이 실제로 성립하는 스냅샷에서는 선택된다 (트리거 배선 자체의 검증)
            Assert.AreSame(prep, selector.Select(Snap((SlotId.DaysToCrisis, 2))),
                           "예고 구간(≤3) 진입 시 발동");
            Assert.IsNull(selector.Select(Snap((SlotId.DaysToCrisis, 2), (SlotId.CookedFoodStock, 10))),
                          "비축 목표 달성 시 스킵 (기존 규칙 2 그대로)");

            Object.DestroyImmediate(prep);
        }

        // ── M6-T2: 배율 — null-안전 접근자 + 시간 입력 스케일 (겨울 = 정지) ──

        [Test]
        public void M6_T2_SeasonMults_NullSafeAndWinterValues()
        {
            SeasonSO mild = MakeSeason("온화", 6f, false);
            SeasonSO winter = MakeSeason("겨울", 3f, true);
            winter.RegenMult = 0f;
            winter.GrowthMult = 0f;
            winter.SatietyDecayMult = 1.5f;
            var service = new SeasonService(new[] { mild, winter });

            // 첫 Tick 전 = 중립 1 (Current null — 소비처가 안전)
            Assert.AreEqual(1f, service.RegenMult);
            Assert.AreEqual(1f, service.GrowthMult);
            Assert.AreEqual(1f, service.SatietyDecayMult);

            service.Tick(0f); // 온화 (에셋 기본값 1)
            Assert.AreEqual(1f, service.RegenMult);

            service.Tick(7f); // 겨울
            Assert.AreEqual(0f, service.RegenMult, "겨울 재생 정지");
            Assert.AreEqual(0f, service.GrowthMult, "겨울 성장 정지");
            Assert.AreEqual(1.5f, service.SatietyDecayMult, "겨울 허기 가속");

            DestroyAll(mild, winter);
        }

        [Test]
        public void M6_T2_ScaledDelta_FreezesRegeneration()
        {
            // 곱 지점은 호출부 — 스케일된 delta 0이면 서비스는 재생하지 않는다 (M6-S1의 절반)
            var discovery = new DiscoveryService();
            var node = new AIVillage.Core.ResourceNode(
                "gate_node", AIVillage.Core.ResourceType.RawFood, 0, 0,
                maxAmount: 10f, regenPerDay: 3f);
            node.CurrentAmount = 1f;
            discovery.AddResourceNode(node);

            discovery.TickRegeneration(1f * 0f); // 겨울: delta × RegenMult 0
            Assert.AreEqual(1f, node.CurrentAmount, 1e-5f, "겨울 1일 — 잔량 불변");

            discovery.TickRegeneration(1f * 1f); // 온화: 기존과 동일 증가
            Assert.AreEqual(4f, node.CurrentAmount, 1e-5f, "온화 1일 — +3 (기존 재생 동작 유지)");
        }

        [Test]
        public void M6_T2b_SatietyDecay_SeasonMultScales()
        {
            // 감쇠 산식 단일 지점 (VillagerAgent.SatietyDecay) — 25/day 기준 (2026-07-13 승인값)
            Assert.AreEqual(25f, VillagerAgent.SatietyDecay(25f, 1f, 1f), 1e-5f, "중립 1일 = 25");
            Assert.AreEqual(37.5f, VillagerAgent.SatietyDecay(25f, 1.5f, 1f), 1e-5f, "겨울 1일 = 1.5배");
            Assert.AreEqual(0.0375f, VillagerAgent.SatietyDecay(25f, 1.5f, 0.001f), 1e-6f, "틱 단위 비례");
        }

        // ── M6-T5: 성격 위기 대응 분화 — 동시 돌입(전원 같은 판단)을 흩는다 ──

        [Test]
        public void M6_T5_BuildGoalBias_CombinesJobAndPersonality()
        {
            var goal = ScriptableObject.CreateInstance<GoalSO>();

            Assert.IsNull(VillagerAgent.BuildGoalBias(null, null), "둘 다 없음 = null (중립 불변식)");

            var job = ScriptableObject.CreateInstance<JobSO>();
            job.GoalBoosts = new[] { new GoalBoost { Goal = goal, Boost = 30 } };
            Assert.AreEqual(30, VillagerAgent.BuildGoalBias(job, null)(goal), "직업만 = M5 동작 유지");

            var p = ScriptableObject.CreateInstance<PersonalitySO>();
            p.GoalBoosts = new[] { new GoalBoost { Goal = goal, Boost = -25 } };
            Assert.AreEqual(-25, VillagerAgent.BuildGoalBias(null, p)(goal), "성격만");
            Assert.AreEqual(5, VillagerAgent.BuildGoalBias(job, p)(goal), "직업+성격 합산");

            var other = ScriptableObject.CreateInstance<GoalSO>();
            Assert.AreEqual(0, VillagerAgent.BuildGoalBias(job, p)(other), "미등록 goal은 0");

            DestroyAll(other, p, job, goal);
        }

        [Test]
        public void M6_T5_PersonalityAssets_CrisisResponseDiverges()
        {
            // 성격 5종의 겨울 비축 실효 우선순위가 갈려야 한다 — 전원 같으면 동시 돌입 재발
            GoalSO prep = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_WinterPrep.asset");
            Assert.IsNotNull(prep);

            string[] names = { "Docile", "Stubborn", "Farmer", "Wanderer", "Prickly" };
            var distinct = new System.Collections.Generic.HashSet<int>();
            bool anyNegative = false;
            foreach (string n in names)
            {
                var p = AssetDatabase.LoadAssetAtPath<PersonalitySO>(
                    $"Assets/M0Config/Personalities/Personality_{n}.asset");
                Assert.IsNotNull(p, $"성격 에셋 없음: {n}");
                int boost = p.BoostFor(prep);
                distinct.Add(boost);
                if (boost < 0) anyNegative = true;
                Assert.Less(prep.Priority + boost, 60,
                            $"{n}: 비축 실효 우선순위가 명령 대역(60) 침범 (M5-T5 안전 대역)");
                Assert.Greater(prep.Priority + boost, 2,
                               $"{n}: 비축이 일과(2) 아래로 추락 — goal이 죽는다");
            }
            Assert.GreaterOrEqual(distinct.Count, 3, "위기 대응이 최소 3갈래로 갈려야 함 (분화 정책)");
            Assert.IsTrue(anyNegative, "위기를 미루는 성격(고집쟁이류)이 최소 1명 있어야 함");
        }

        // ── M6-C: HUD 달력 문구 — 표시 정책 단일 지점 (순수 Compose) ─────────

        [Test]
        public void M6_C_HudCompose_ForecastAndCrisisText()
        {
            SeasonSO mild = MakeSeason("온화", 6f, false);
            SeasonSO winter = MakeSeason("겨울", 3f, true);
            var service = new SeasonService(new[] { mild, winter });

            Assert.AreEqual("Day 4", SeasonHud.Compose(4.2f, null, 3f), "계절 없음 = Day만 (중립)");

            service.Tick(1f); // 평시 — 겨울까지 5일 (> 예고 3일)
            StringAssert.Contains("온화", SeasonHud.Compose(1f, service, 3f));
            StringAssert.DoesNotContain("까지", SeasonHud.Compose(1f, service, 3f), "평시엔 카운트다운 없음");

            service.Tick(4f); // 예고 구간 — 겨울까지 2일
            StringAssert.Contains("겨울까지 2일", SeasonHud.Compose(4f, service, 3f));

            service.Tick(7f); // 겨울 진행 중 — 남은 2일
            string crisis = SeasonHud.Compose(7f, service, 3f);
            StringAssert.Contains("겨울", crisis);
            StringAssert.Contains("남은 2일", crisis);

            DestroyAll(mild, winter);
        }

        [Test]
        public void M6_T1b_WiredSeason_SnapshotReflectsClock()
        {
            SeasonSO mild = MakeSeason("온화", 6f, false);
            SeasonSO winter = MakeSeason("겨울", 3f, true);
            var service = new SeasonService(new[] { mild, winter });
            var world = new WorldModel(null, null, null, service);

            service.Tick(5.5f); // 겨울까지 0.5일
            WorldSnapshot forecast = world.BuildSnapshot(50, 50);
            Assert.AreEqual(1, forecast.Get(SlotId.DaysToCrisis), "0.5일 → 올림 1 (명세 §4)");
            Assert.AreEqual(0, forecast.Get(SlotId.CrisisActive));

            service.Tick(7f); // 겨울 진행 중
            WorldSnapshot crisis = world.BuildSnapshot(50, 50);
            Assert.AreEqual(0, crisis.Get(SlotId.DaysToCrisis), "위기 중 0 — 트리거 겨울 내내 유지");
            Assert.AreEqual(1, crisis.Get(SlotId.CrisisActive));

            DestroyAll(mild, winter);
        }
    }
}
