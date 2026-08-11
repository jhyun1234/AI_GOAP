using AIVillage.Core.GOAP;
using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

using static AIVillage.Tests.EditMode.GateHelpers;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M5 직업 게이트 (명세 M5-A~E). 이 파일이 M5-T1~T5의 집이다 —
    /// A(스키마 중립 기본값)를 시작으로 B(중립 불변식·실효 우선순위·배율 결합)·
    /// C(일과 대역)·D(에셋 5종 정책) 게이트가 뒤에 추가된다.
    /// </summary>
    public class M5_JobGates
    {
        [Test]
        public void M5_A_FreshJobSO_DefaultsAreNeutral()
        {
            // 신규 인스턴스의 기본값 = 중립 (M5-S3 불변식의 데이터 절반 — ADR-M4-2 패턴 계승)
            var fresh = ScriptableObject.CreateInstance<JobSO>();
            Assert.AreEqual(1f, fresh.GatherCostMult);
            Assert.AreEqual(1f, fresh.FarmCostMult);
            Assert.AreEqual(1f, fresh.BuildCostMult);
            Assert.AreEqual(1f, fresh.ExploreCostMult);
            Assert.IsNull(fresh.RoutineGoal, "기본 일과 없음");

            // GoalBoosts 미배선(null)·빈 배열·무관 goal 전부 보정 0 (참조 동일성 비교만)
            var someGoal = ScriptableObject.CreateInstance<GoalSO>();
            Assert.AreEqual(0, fresh.BoostFor(someGoal), "GoalBoosts null → 0");
            Assert.AreEqual(0, fresh.BoostFor(null), "null goal → 0");

            fresh.GoalBoosts = new GoalBoost[0];
            Assert.AreEqual(0, fresh.BoostFor(someGoal), "빈 배열 → 0");

            var otherGoal = ScriptableObject.CreateInstance<GoalSO>();
            fresh.GoalBoosts = new[] { new GoalBoost { Goal = someGoal, Boost = 30 } };
            Assert.AreEqual(30, fresh.BoostFor(someGoal), "등록된 goal은 보정치 반환");
            Assert.AreEqual(0, fresh.BoostFor(otherGoal), "미등록 goal은 0 — 참조 동일성");

            Object.DestroyImmediate(otherGoal);
            Object.DestroyImmediate(someGoal);
            Object.DestroyImmediate(fresh);
        }

        // ── M5-B: 실효 우선순위 + 배율 결합 ─────────────────────────────────

        // LoadGoal·Snap → GateHelpers (2026-08-11 2차 감사 통합)

        [Test]
        public void M5_T1_NeutralInvariant_SelectIdenticalToM4()
        {
            // bias/routine null = M0-T2b 기존 케이스 전부 동일 결과 (M5-S3 — ADR-M5-1 불변식)
            GoalSO[] goals =
            {
                LoadGoal("Goal_P0_Hunger"), LoadGoal("Goal_P0_Fatigue"),
                LoadGoal("Goal_BuildCampfire"), LoadGoal("Goal_GatherWood"),
            };
            var selector = new GoalSelector(goals);

            WorldSnapshot[] cases =
            {
                Snap((SlotId.MySatiety, 80), (SlotId.MyFatigue, 10),
                     (SlotId.CampfireBuilt, 1), (SlotId.WoodStock, 50)),   // 전부 만족 → null
                Snap((SlotId.MySatiety, 80), (SlotId.WoodStock, 5)),        // BuildCampfire
                Snap((SlotId.MySatiety, 80), (SlotId.WoodStock, 5), (SlotId.CampfireBuilt, 1)), // GatherWood
                Snap((SlotId.MySatiety, 15), (SlotId.WoodStock, 5)),        // P0_Hunger
            };
            foreach (WorldSnapshot snap in cases)
                Assert.AreSame(selector.Select(snap),
                               selector.Select(snap, null, null, null, null),
                               "중립 불변식 위반 — bias/routine null이 기존 선택을 바꿈");
        }

        [Test]
        public void M5_T2_JobBias_EffectivePriorityPreemption()
        {
            // 심기 발동 상황 (허기 함께 발동): 무직은 Snack(30) > Plant(22),
            // 농부 bias(+30)는 Plant(실효 52)가 Snack을 이긴다 (M5-S1)
            GoalSO snack = LoadGoal("Goal_Snack");
            GoalSO plant = LoadGoal("Goal_Plant");
            var selector = new GoalSelector(new[] { snack, plant });
            // M14-W2 재배선: Plant 트리거 = 심기 창(舊 식량일수 ≤3 — 수동 스냅샷은 기본 0이라
            // 우연히 발동하던 구조). 창 열림을 명시해야 발동한다 (WorldModel 경유 시 미배선 중립 = 1).
            WorldSnapshot snap = Snap((SlotId.MySatiety, 30), (SlotId.MyEmptyPlot, 1),
                                      (SlotId.PlantWindowOpen, 1));

            Assert.AreSame(snack, selector.Select(snap), "무직: 기존 순서 (Snack 30 > Plant 22)");

            var farmer = ScriptableObject.CreateInstance<JobSO>();
            farmer.GoalBoosts = new[] { new GoalBoost { Goal = plant, Boost = 30 } };
            int priorityBefore = plant.Priority;

            Assert.AreSame(plant, selector.Select(snap, null, null, farmer.BoostFor),
                           "농부: Plant 실효 52 > Snack 30 — 선점 (M5-S1)");
            Assert.AreEqual(priorityBefore, plant.Priority,
                            "에셋 Priority 원본 불변 (ADR-M5-1 — bias는 어디에도 저장 안 됨)");

            // bias는 순위에만 — 발동 판정(Passes)에는 불개입: 빈 밭 없으면 boost가 있어도 Plant 제외
            WorldSnapshot noPlot = Snap((SlotId.MySatiety, 30), (SlotId.MyEmptyPlot, 0),
                                        (SlotId.PlantWindowOpen, 1));
            Assert.AreSame(snack, selector.Select(noPlot, null, null, farmer.BoostFor),
                           "미발동 goal은 boost로 부활하지 않는다");

            Object.DestroyImmediate(farmer);
        }

        [Test]
        public void M5_T4_RoutineGoal_PriorityBand()
        {
            // 일과 대역 (ADR-M5-2): 여가(1) < 일과(2) < 공용 노동(8+) — "할 일 없을 때"만 잡힌다
            GoalSO leisure = LoadGoal("Goal_Leisure");
            GoalSO wood    = LoadGoal("Goal_GatherWood");
            var routine = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_Routine_Farmer.asset");
            var explorerRoutine = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_Routine_Explorer.asset");
            Assert.IsNotNull(routine); Assert.IsNotNull(explorerRoutine);

            // 에셋 정책: P2, 조건 비움("항상 미달성" 특례 — 여가와 동일), 풀 비어있지 않음
            foreach (GoalSO r in new[] { routine, explorerRoutine })
            {
                Assert.AreEqual(2, r.Priority, $"{r.name}: 일과 대역 P2");
                Assert.IsTrue(r.TriggerConditions == null || r.TriggerConditions.Length == 0);
                Assert.IsTrue(r.GoalConditions == null || r.GoalConditions.Length == 0);
                Assert.IsTrue(r.DirectActionPool != null && r.DirectActionPool.Length > 0,
                              $"{r.name}: 직접 실행 풀 필수 (플래너 미경유)");
            }

            var selector = new GoalSelector(new[] { leisure, wood });

            // 다른 goal 전부 만족/미발동 → routine이 여가(P1)를 이긴다
            WorldSnapshot idle = Snap((SlotId.MySatiety, 80), (SlotId.WoodStock, 50), (SlotId.CampfireBuilt, 1));
            Assert.AreSame(leisure, selector.Select(idle), "무직: routine 후보 없음 — 기존 여가 동작");
            Assert.AreSame(routine, selector.Select(idle, null, null, null, routine),
                           "한가할 때 일과 > 여가 (P2 > P1)");

            // 공용 노동 발동 시 일과 밀림 (P2 < 8+)
            WorldSnapshot work = Snap((SlotId.MySatiety, 80), (SlotId.WoodStock, 5), (SlotId.CampfireBuilt, 1));
            Assert.AreSame(wood, selector.Select(work, null, null, null, routine),
                           "노동 발동 시 일과는 밀린다 — 씬 사다리 위의 보정일 뿐 (ADR-M5-4)");
        }

        [Test]
        public void M5_T5_JobAssets_LoadAndPolicy()
        {
            // 직업 5종 로드 (§4 수치표) + 축 배치 검증 (M5-D)
            JobSO Load(string name)
            {
                var j = AssetDatabase.LoadAssetAtPath<JobSO>($"Assets/M0Config/Jobs/{name}.asset");
                Assert.IsNotNull(j, $"직업 에셋 없음: {name}");
                return j;
            }
            JobSO farmer = Load("Job_Farmer"), lumber = Load("Job_Lumberjack"),
                  miner  = Load("Job_Miner"),  cook   = Load("Job_Cook"),
                  explorer = Load("Job_Explorer");

            Assert.Less(farmer.FarmCostMult, 1f, "농부는 밭일 선호");
            Assert.Greater(farmer.BoostFor(LoadGoal("Goal_Plant")), 0, "농부 심기 선점");
            Assert.Greater(farmer.BoostFor(LoadGoal("Goal_HarvestCrop")), 0, "농부 수확 선점");
            Assert.IsNotNull(farmer.RoutineGoal, "농부 일과 = 밭 곁 배회");

            Assert.Greater(lumber.BoostFor(LoadGoal("Goal_GatherWood")), 0, "나무꾼 = goal boost가 주 차별화 (배율은 보조)");
            Assert.Greater(miner.BoostFor(LoadGoal("Goal_GatherStone")), 0, "광부 = goal boost가 주 차별화");
            Assert.Greater(cook.BoostFor(LoadGoal("Goal_CookAhead")), 0, "요리사 선비축 선점");

            Assert.Less(explorer.ExploreCostMult, 1f, "탐험가는 탐험 선호");
            Assert.IsNotNull(explorer.RoutineGoal, "탐험가 일과 = 지도 밝히기");

            // 안전 대역 (§4): 실효 우선순위(Priority+Boost)가 명령(60)·P0(90+) 아래
            foreach (JobSO j in new[] { farmer, lumber, miner, cook, explorer })
                if (j.GoalBoosts != null)
                    foreach (GoalBoost b in j.GoalBoosts)
                    {
                        Assert.IsNotNull(b.Goal, $"{j.name}: GoalBoosts에 빈 참조");
                        Assert.Less(b.Goal.Priority + b.Boost, 60,
                                    $"{j.name}→{b.Goal.name}: 실효 우선순위가 명령 대역(60) 침범");
                    }
        }

        [Test]
        public void M5_B_CostMult_JobCombinesWithPersonality()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ActionCatalog>("Assets/M0Config/ActionCatalog.asset");
            Assert.IsNotNull(catalog);

            // 성격 null·직업만 있어도 배열 생성 (탐험가 Explore 0.6 반영)
            var explorer = ScriptableObject.CreateInstance<JobSO>();
            explorer.ExploreCostMult = 0.6f;
            float[] jobOnly = PersonalityCost.Build(catalog, null, explorer, null);
            Assert.IsNotNull(jobOnly, "직업만으로도 배율 배열 생성");
            for (int i = 0; i < catalog.Actions.Length; i++)
            {
                float expected = catalog.Actions[i] is ExploreActionSO ? 0.6f : 1f;
                Assert.AreEqual(expected, jobOnly[i], 1e-5f,
                                $"{catalog.Actions[i].name}: 탐험만 0.6, 나머지 중립");
            }

            // 성격×직업 곱 결합 (ADR-M5-3): Farm 0.7 × 0.6 = 0.42
            var p = ScriptableObject.CreateInstance<PersonalitySO>();
            p.FarmCostMult = 0.7f;
            var farmerJob = ScriptableObject.CreateInstance<JobSO>();
            farmerJob.FarmCostMult = 0.6f;
            float[] combined = PersonalityCost.Build(catalog, p, farmerJob, null);
            for (int i = 0; i < catalog.Actions.Length; i++)
                if (catalog.Actions[i] is FarmActionSO)
                    Assert.AreEqual(0.42f, combined[i], 1e-5f, "성격×직업 곱 결합");

            // 둘 다 null = null (중립 경로 — ADR-M4-2 계승)
            Assert.IsNull(PersonalityCost.Build(catalog, null, null, null));

            Object.DestroyImmediate(farmerJob);
            Object.DestroyImmediate(p);
            Object.DestroyImmediate(explorer);
        }
    }
}
