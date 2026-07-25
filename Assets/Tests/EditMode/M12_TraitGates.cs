using System.Collections.Generic;
using System.Linq;
using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M12 성향 축 게이트 (명세 M12-A~).
    ///   M12-T1: 유도기의 중립 불변식·클램프·미등록 축 — 4작용형식의 공통 토대
    ///   M12-T2: 축 추가 내성 (S3) — 7번째 축이 생겨도 기존 벡터의 유도값이 불변
    /// </summary>
    public class M12_TraitGates
    {
        private static TraitValue V(TraitId t, int v) => new TraitValue { Trait = t, Value = v };
        private static TraitWeight W(TraitId t, float w) => new TraitWeight { Trait = t, Weight = w };

        // ── M12-T1: 유도기 ───────────────────────────────────────────────────

        [Test]
        public void M12_T1_Bias_NeutralWhenEitherSideMissing()
        {
            var traits = new[] { V(TraitId.Diligence, 100) };
            var weights = new[] { W(TraitId.Diligence, 1f) };

            Assert.AreEqual(0f, TraitVector.Bias(null, null), 0.0001f, "둘 다 없음 = 중립");
            Assert.AreEqual(0f, TraitVector.Bias(traits, null), 0.0001f, "가중치 없음 = 중립 (통로가 성향을 안 읽는다)");
            Assert.AreEqual(0f, TraitVector.Bias(null, weights), 0.0001f, "벡터 없음 = 중립 (성격 미배선 경로)");
            Assert.AreEqual(0f, TraitVector.Bias(new TraitValue[0], new TraitWeight[0]), 0.0001f, "빈 배열 = 중립");
        }

        [Test]
        public void M12_T1_ValueOf_UnregisteredAxisIsZero()
        {
            // ADR-M12-3의 물리적 근거: 미등록 축 = 0 = 중립.
            var traits = new[] { V(TraitId.Diligence, 100) };

            Assert.AreEqual(100, TraitVector.ValueOf(traits, TraitId.Diligence), "등록된 축은 그 값");
            Assert.AreEqual(0, TraitVector.ValueOf(traits, TraitId.Caution), "미등록 축은 0");
            Assert.AreEqual(0, TraitVector.ValueOf(null, TraitId.Diligence), "벡터 자체가 없어도 0");

            // 등록 안 된 축만 읽는 통로는 이 성격에 대해 완전 중립이어야 한다
            Assert.AreEqual(0f, TraitVector.Bias(traits, new[] { W(TraitId.Caution, 1f) }), 0.0001f,
                "가중치가 미등록 축만 보면 편향 0");
        }

        [Test]
        public void M12_T1_Bias_ClampedToUnitRange()
        {
            // 6축 전부 +100에 가중치 1.0이면 합이 6 — 클램프가 없으면 유도값이 6배로 터진다.
            var all = new[]
            {
                V(TraitId.Diligence, 100), V(TraitId.Foresight, 100), V(TraitId.Sociability, 100),
                V(TraitId.Wanderlust, 100), V(TraitId.Willfulness, 100), V(TraitId.Caution, 100),
            };
            var allW = new[]
            {
                W(TraitId.Diligence, 1f), W(TraitId.Foresight, 1f), W(TraitId.Sociability, 1f),
                W(TraitId.Wanderlust, 1f), W(TraitId.Willfulness, 1f), W(TraitId.Caution, 1f),
            };

            Assert.AreEqual(1f, TraitVector.Bias(all, allW), 0.0001f, "상한 +1");

            var allLow = new[]
            {
                V(TraitId.Diligence, -100), V(TraitId.Foresight, -100), V(TraitId.Sociability, -100),
                V(TraitId.Wanderlust, -100), V(TraitId.Willfulness, -100), V(TraitId.Caution, -100),
            };
            Assert.AreEqual(-1f, TraitVector.Bias(allLow, allW), 0.0001f, "하한 -1");

            // 단일 축 정규화: 값 100 × 가중치 1.0 = 정확히 1.0, 절반이면 절반
            Assert.AreEqual(1f, TraitVector.Bias(new[] { V(TraitId.Caution, 100) },
                                                 new[] { W(TraitId.Caution, 1f) }), 0.0001f);
            Assert.AreEqual(0.5f, TraitVector.Bias(new[] { V(TraitId.Caution, 50) },
                                                   new[] { W(TraitId.Caution, 1f) }), 0.0001f);
            Assert.AreEqual(-0.5f, TraitVector.Bias(new[] { V(TraitId.Caution, 100) },
                                                    new[] { W(TraitId.Caution, -0.5f) }), 0.0001f,
                "음수 가중치는 방향을 뒤집는다");
        }

        [Test]
        public void M12_T1_Threshold_ReturnsBaseWhenNeutral()
        {
            var traits = new[] { V(TraitId.Willfulness, 100) };

            var empty = new TraitBias { Weights = null, Sensitivity = 15f };
            Assert.AreEqual(30f, TraitVector.Threshold(traits, empty, 30f), 0.0001f,
                "가중치가 비면 base 그대로 — 소비처가 이 함수를 통과시켜도 현행 판정과 동일");

            var willful = new TraitBias { Weights = new[] { W(TraitId.Willfulness, 1f) }, Sensitivity = 15f };
            Assert.AreEqual(45f, TraitVector.Threshold(traits, willful, 30f), 0.0001f,
                "자존 +100 → base + 1.0 × 15");

            var sensZero = new TraitBias { Weights = new[] { W(TraitId.Willfulness, 1f) }, Sensitivity = 0f };
            Assert.AreEqual(30f, TraitVector.Threshold(traits, sensZero, 30f), 0.0001f,
                "민감도 0 = 그 소비처는 성향을 안 쓴다");
        }

        [Test]
        public void M12_T1_Meets_EmptyConditionAlwaysPassesAndIsMonotonic()
        {
            var lazy = new[] { V(TraitId.Foresight, -70) };
            var farmer = new[] { V(TraitId.Foresight, 80) };
            var need30 = new[] { new TraitCondition { Trait = TraitId.Foresight, MinValue = 30 } };

            Assert.IsTrue(TraitVector.Meets(lazy, null), "조건 없음 = 항상 성립 (현행 동작)");
            Assert.IsTrue(TraitVector.Meets(lazy, new TraitCondition[0]), "빈 조건 = 항상 성립");
            Assert.IsFalse(TraitVector.Meets(lazy, need30), "게으름뱅이(대비 -70)는 대비 30 조건에 미달");
            Assert.IsTrue(TraitVector.Meets(farmer, need30), "농사꾼(대비 80)은 성립");
            Assert.IsFalse(TraitVector.Meets(null, need30), "벡터 미배선은 0으로 취급 → 미달");
        }

        // ── M12-T2: 축 추가 내성 (성공 기준 S3) ──────────────────────────────

        [Test]
        public void M12_T2_AddingNewAxisLeavesExistingVectorsUnchanged()
        {
            // S3: 7번째 축이 생겨도 기존 에셋을 한 개도 고치지 않고 행동이 불변이어야 한다.
            // 새 축의 스탠드인 = 기존 벡터에 '등록되지 않은' 축 (여기서는 Caution을 그 역할로 쓴다).
            var existing = new[] { V(TraitId.Diligence, 60), V(TraitId.Foresight, -40) };

            var before = new[] { W(TraitId.Diligence, 0.5f), W(TraitId.Foresight, 0.5f) };
            var after = new[] { W(TraitId.Diligence, 0.5f), W(TraitId.Foresight, 0.5f), W(TraitId.Caution, 0.8f) };

            Assert.AreEqual(TraitVector.Bias(existing, before), TraitVector.Bias(existing, after), 0.0001f,
                "새 축을 읽는 통로가 생겨도, 그 축이 없는 기존 성격의 유도값은 불변 (ADR-M12-3)");

            var bias = new TraitBias { Weights = after, Sensitivity = 15f };
            Assert.AreEqual(TraitVector.Threshold(existing, new TraitBias { Weights = before, Sensitivity = 15f }, 30f),
                            TraitVector.Threshold(existing, bias, 30f), 0.0001f,
                "③문턱에서도 동일");
        }

        // ── M12-T3: ①우선순위 goal 태그 (M12-B) ──────────────────────────────

        private const string GOALS_DIR = "Assets/M0Config/Goals";

        /// <summary>먹는 행동 + 플레이어 명령 = 성향 면제 대상 (ADR-M12-4 / ③문턱이 담당).</summary>
        private static readonly string[] ExemptGoals =
        {
            "Goal_P0_Hunger", "Goal_P0_Fatigue", "Goal_Snack",
            "Order_ChopWood", "Order_HarvestBerries", "Order_MineStone",
        };

        private static List<GoalSO> LoadAllGoals()
        {
            var goals = AssetDatabase.FindAssets("t:GoalSO", new[] { GOALS_DIR })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GoalSO>)
                .Where(g => g != null)
                .ToList();
            Assert.IsNotEmpty(goals, $"{GOALS_DIR}에서 goal 에셋 로드");
            return goals;
        }

        /// <summary>모든 축이 극단인 벡터 — 유도값의 최댓값을 실측하는 데 쓴다.</summary>
        private static TraitValue[] Extreme(int sign) => System.Enum.GetValues(typeof(TraitId))
            .Cast<TraitId>().Select(t => V(t, 100 * sign)).ToArray();

        [Test]
        public void M12_T3_GoalTraitWeights_CoverageAndEatingExempt()
        {
            List<GoalSO> goals = LoadAllGoals();
            var tagged = goals.Where(g => g.TraitWeights != null && g.TraitWeights.Length > 0).ToList();

            // S1: 성향이 닿는 goal 수 — 착수 전 3개(WinterPrep·SaveForHome·StoreFood)에서 대폭 확대.
            Assert.GreaterOrEqual(tagged.Count, 24,
                $"성향이 닿는 goal이 {tagged.Count}개뿐 — 면제 대상(먹는 행동 3 + 명령 3)을 뺀 전부에 붙어야 한다 (S1)");

            // 면제 대상은 반드시 비어 있어야 한다 (ADR-M12-4 — 굶주림 앞에 성격 없음)
            foreach (string name in ExemptGoals)
            {
                GoalSO g = goals.FirstOrDefault(x => x.name == name);
                Assert.IsNotNull(g, $"{name} 에셋 존재");
                Assert.IsTrue(g.TraitWeights == null || g.TraitWeights.Length == 0,
                    $"{name}: 성향 면제 대상인데 TraitWeights가 있다 (ADR-M12-4 / 명령은 ③문턱이 담당)");
            }

            // 면제 대상을 뺀 나머지는 전부 태그돼야 한다 (빠뜨림 탐지)
            foreach (GoalSO g in goals)
            {
                if (ExemptGoals.Contains(g.name)) continue;
                Assert.IsTrue(g.TraitWeights != null && g.TraitWeights.Length > 0,
                    $"{g.name}: 성향 가중치가 없다 — 이 goal에서는 모든 성격이 똑같이 행동한다");
            }
        }

        [Test]
        public void M12_T3_TraitBoost_NeutralAndWithinP0Guard()
        {
            List<GoalSO> goals = LoadAllGoals();
            GoalSO hunger = goals.First(g => g.name == "Goal_P0_Hunger");
            GoalSO flee = goals.First(g => g.name == "Goal_Flee");
            TraitValue[] hi = Extreme(+1), lo = Extreme(-1);

            foreach (GoalSO g in goals)
            {
                // 중립 불변식: 벡터가 없으면(성격 미배선) 언제나 0
                Assert.AreEqual(0, g.TraitBoost(null), $"{g.name}: 벡터 없음 = 보정 0");

                int up = g.TraitBoost(hi), down = g.TraitBoost(lo);

                // ADR-M12-7: |boost| ≤ 30
                Assert.LessOrEqual(Mathf.Abs(up), 30, $"{g.name}: 유도값 {up}이 상한 30 초과 (ADR-M12-7)");
                Assert.LessOrEqual(Mathf.Abs(down), 30, $"{g.name}: 유도값 {down}이 상한 30 초과 (ADR-M12-7)");

                // P0 보호: 어떤 성격도 배고픔보다 '일'을 앞세우지 못한다.
                // 피신은 유일한 예외 — 아래에서 따로 검사한다 (ADR-M12-8).
                if (g == hunger || g == flee) continue;
                Assert.Less(g.Priority + Mathf.Max(up, down), hunger.Priority,
                    $"{g.name}: 극단 성격에서 실효 우선순위가 배고픔({hunger.Priority})을 넘는다 " +
                    "— 굶으면서 다른 일을 하러 간다 (ADR-M12-4의 수치적 보증)");
            }
        }

        [Test]
        public void M12_T3_Flee_OutranksHungerExceptForTheReckless()
        {
            // ADR-M12-8 — 위험 앞에서는 밥보다 도망이 먼저다. 단 겁이 아주 낮은 주민만 예외.
            // 개정 전(피신 92 < 배고픔 100): 포만 20 이하 주민에게 늑대가 와도 배고픔이 이기고,
            // P0의 SkipFailureCooldown 때문에 겨울엔 재선택이 무한 반복돼 피신이 영영 안 뽑혔다
            // = 굶주린 주민이 늑대 앞에 굳어 서서 물린다.
            List<GoalSO> goals = LoadAllGoals();
            GoalSO hunger = goals.First(g => g.name == "Goal_P0_Hunger");
            GoalSO flee = goals.First(g => g.name == "Goal_Flee");

            Assert.Greater(flee.Priority, hunger.Priority,
                "중립 주민은 배고파도 먼저 도망친다");

            var brave = new[] { V(TraitId.Caution, -100) };
            Assert.Less(flee.Priority + flee.TraitBoost(brave), hunger.Priority,
                "겁이 최저인 주민(무모한자)은 배고프면 위험을 무시하고 밥부터 — M10 '용맹은 늦게 피신 " +
                "= 결정적 부상' 설계가 감지 반경만이 아니라 행동으로도 살아난다");

            var timid = new[] { V(TraitId.Caution, 100) };
            Assert.Greater(flee.Priority + flee.TraitBoost(timid), flee.Priority,
                "겁이 높으면 더 확실히 도망 우선");
        }

        [Test]
        public void M12_T3_Willfulness_SplitsCommunalAndPersonalGoals()
        {
            // 결함 5의 처방(결정 16): 자존이 ③문턱에만 있으면 고집쟁이·새침이는 방치 시 평범하다.
            // ①에 자리를 만들어 "저 사람은 마을 일을 안 도와"가 눈에 보이게 한다.
            var communal = new[] { "Goal_GatherWood", "Goal_GatherStone", "Goal_BuildHouse",
                                   "Goal_RequestHouse", "Goal_TendInjured", "Goal_TreatInjured" };
            var personal = new[] { "Goal_BuildMyHouse", "Goal_SaveForHome", "Goal_StoreFood",
                                   "Goal_Plant", "Goal_HarvestCrop" };

            List<GoalSO> goals = LoadAllGoals();
            float WillfulnessOf(string n)
            {
                GoalSO g = goals.First(x => x.name == n);
                return g.TraitWeights.Where(w => w.Trait == TraitId.Willfulness).Sum(w => w.Weight);
            }

            foreach (string n in communal)
            {
                Assert.Less(WillfulnessOf(n), 0f, $"{n}: 공용 goal은 자존 음수여야 한다");
                // 파생 2 방어: 너무 크면 자존↑ 판에서 공용 건물이 아예 안 선다 (M10 '규모 8 정체' 재현)
                Assert.GreaterOrEqual(WillfulnessOf(n), -0.5f, $"{n}: 자존 음수 폭이 -0.5를 넘는다 (공용 건물 정체 위험)");
            }
            foreach (string n in personal)
                Assert.Greater(WillfulnessOf(n), 0f, $"{n}: 개인 goal은 자존 양수여야 한다");
        }
    }
}
