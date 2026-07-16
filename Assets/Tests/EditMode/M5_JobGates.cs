using AIVillage.Core.GOAP;
using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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

        private static GoalSO LoadGoal(string name)
        {
            var g = AssetDatabase.LoadAssetAtPath<GoalSO>($"Assets/M0Config/Goals/{name}.asset");
            Assert.IsNotNull(g, $"goal 에셋 없음: {name}");
            return g;
        }

        private static WorldSnapshot Snap(params (SlotId slot, int value)[] pairs)
        {
            var slots = new int[PlanningConfig.TotalSlots];
            foreach ((SlotId slot, int value) in pairs) slots[(int)slot] = value;
            return new WorldSnapshot(slots);
        }

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
            WorldSnapshot snap = Snap((SlotId.MySatiety, 30), (SlotId.EmptyFarmPlot, 1));

            Assert.AreSame(snack, selector.Select(snap), "무직: 기존 순서 (Snack 30 > Plant 22)");

            var farmer = ScriptableObject.CreateInstance<JobSO>();
            farmer.GoalBoosts = new[] { new GoalBoost { Goal = plant, Boost = 30 } };
            int priorityBefore = plant.Priority;

            Assert.AreSame(plant, selector.Select(snap, null, null, farmer.BoostFor),
                           "농부: Plant 실효 52 > Snack 30 — 선점 (M5-S1)");
            Assert.AreEqual(priorityBefore, plant.Priority,
                            "에셋 Priority 원본 불변 (ADR-M5-1 — bias는 어디에도 저장 안 됨)");

            // bias는 순위에만 — 발동 판정(Passes)에는 불개입: 빈 밭 없으면 boost가 있어도 Plant 제외
            WorldSnapshot noPlot = Snap((SlotId.MySatiety, 30), (SlotId.EmptyFarmPlot, 0));
            Assert.AreSame(snack, selector.Select(noPlot, null, null, farmer.BoostFor),
                           "미발동 goal은 boost로 부활하지 않는다");

            Object.DestroyImmediate(farmer);
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
