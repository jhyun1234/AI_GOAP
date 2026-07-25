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

            // 면제 대상은 반드시 비어 있어야 한다 (ADR-M12-4 ① 몸값 불가침)
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
                    "— 굶으면서 다른 일을 하러 간다 (ADR-M12-4 ②③의 수치적 보증)");
            }
        }

        /// <summary>
        /// P0 배고픔보다 높이 설 수 있는 goal의 사전 승인 목록 (ADR-M12-8).
        /// 여기 추가하려면 ADR-M12-4 ③의 3조건을 논증해야 한다 —
        /// ⓐ외부 세계 상태가 발동 ⓑ그 상태가 사라지면 자기 종료 ⓒ굶주림보다 빠른 치사.
        /// </summary>
        private static readonly string[] MayOutrankHunger = { "Goal_Flee" };

        [Test]
        public void M12_T3_OnlyApprovedGoalsMayOutrankHunger()
        {
            // ADR-M12-4 ③의 이빨. 이 게이트가 없으면 "일중독은 굶어도 일한다" 같은 축이
            // 조용히 P0 위로 올라와 성격 때문에 굶어 죽는 판이 만들어진다.
            List<GoalSO> goals = LoadAllGoals();
            GoalSO hunger = goals.First(g => g.name == "Goal_P0_Hunger");

            foreach (GoalSO g in goals)
            {
                if (g == hunger || MayOutrankHunger.Contains(g.name)) continue;
                Assert.LessOrEqual(g.Priority, hunger.Priority,
                    $"{g.name}: 우선순위 {g.Priority}이 배고픔({hunger.Priority})보다 높다 — " +
                    "P0를 초월하려면 ADR-M12-4 ③의 3조건을 논증하고 승인 목록에 올려야 한다");
            }
        }

        [Test]
        public void M12_T3_Flee_OutranksHungerExceptForTheReckless()
        {
            // ADR-M12-4 ② 급박성 사다리의 첫 사례 — 즉사급(위협)이 시간급(굶주림) 위에 선다.
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

        // ── M12-T5: ②비용 유도 (M12-C) ───────────────────────────────────────

        private static TraitRulesSO LoadRules()
        {
            var r = AssetDatabase.LoadAssetAtPath<TraitRulesSO>("Assets/M0Config/TraitRules.asset");
            Assert.IsNotNull(r, "TraitRules 에셋 로드");
            return r;
        }

        [Test]
        public void M12_T5_TraitCost_NeutralWhenUnwired()
        {
            var rules = LoadRules();
            var lazy = new[] { V(TraitId.Diligence, -80) };

            // 중립 불변식 3경로: 규칙표 미배선 / 가중치 빈 계열 / 벡터 없는 성격
            Assert.AreEqual(1f, rules.CostMult(lazy, null), 1e-5f, "가중치 없는 계열 = 중립");
            Assert.AreEqual(1f, rules.CostMult(lazy, new TraitWeight[0]), 1e-5f, "빈 가중치 = 중립");
            Assert.AreEqual(1f, rules.CostMult(null, rules.GatherWeights), 1e-5f, "벡터 없는 성격 = 중립");
        }

        [Test]
        public void M12_T5_TraitCost_DirectionAndClampNeverBinds()
        {
            var rules = LoadRules();
            var diligent = new[] { V(TraitId.Diligence, 100) };
            var lazy = new[] { V(TraitId.Diligence, -100) };

            // 방향: 근면↑는 노동이 싸지고, 태만은 비싸진다
            Assert.Less(rules.CostMult(diligent, rules.BuildWeights), 1f, "근면 +100 → 건설이 싸다");
            Assert.Greater(rules.CostMult(lazy, rules.BuildWeights), 1f, "근면 -100 → 건설이 비싸다");

            // 근면 가중치 1.0인 계열(건설)은 정확히 1 ∓ CostScale
            Assert.AreEqual(1f - rules.CostScale, rules.CostMult(diligent, rules.BuildWeights), 1e-5f);
            Assert.AreEqual(1f + rules.CostScale, rules.CostMult(lazy, rules.BuildWeights), 1e-5f);

            // 모험은 채집·탐험을 싸게, 농사를 비싸게 (§3 정의표)
            var nomad = new[] { V(TraitId.Wanderlust, 100) };
            Assert.Less(rules.CostMult(nomad, rules.ExploreWeights), 1f, "모험 +100 → 탐험이 싸다");
            Assert.Less(rules.CostMult(nomad, rules.GatherWeights), 1f, "모험 +100 → 야외채집이 싸다");
            Assert.Greater(rules.CostMult(nomad, rules.FarmWeights), 1f, "모험 +100 → 농사가 비싸다");

            // 🔑 클램프는 안전망이지 일상 경로가 아니다 — CostScale ≤ 0.5면 |bias| ≤ 1이라 절대 안 닿는다.
            // 닿기 시작하면 서로 다른 성격이 같은 배율로 뭉개져 차별화가 죽는다.
            Assert.LessOrEqual(rules.CostScale, 0.5f, "CostScale이 0.5를 넘으면 클램프가 성격을 뭉갠다");
            foreach (var w in new[] { rules.GatherWeights, rules.FarmWeights, rules.BuildWeights, rules.ExploreWeights })
                foreach (int sign in new[] { 1, -1 })
                {
                    float m = rules.CostMult(Extreme(sign), w);
                    Assert.Greater(m, 0.5f + 1e-6f, "하한 클램프에 닿았다");
                    Assert.Less(m, 1.5f - 1e-6f, "상한 클램프에 닿았다");
                }
        }

        [Test]
        public void M12_T5_TraitCost_SurvivalActionsStayNeutral()
        {
            // ADR-M12-4 ① 몸값 불가침 — 소비·휴식·배회는 성향과 무관하게 항상 1.
            var catalog = AssetDatabase.LoadAssetAtPath<ActionCatalog>("Assets/M0Config/ActionCatalog.asset");
            Assert.IsNotNull(catalog, "카탈로그 로드");

            var extreme = ScriptableObject.CreateInstance<PersonalitySO>();
            extreme.Traits = Extreme(+1);
            try
            {
                float[] mult = PersonalityCost.Build(catalog, extreme, null, null, LoadRules());
                Assert.IsNotNull(mult, "성격만으로도 배열 생성");

                for (int i = 0; i < catalog.Actions.Length; i++)
                {
                    ActionSO a = catalog.Actions[i];
                    bool labor = a is GatherActionSO || a is FarmActionSO
                              || a is BuildActionSO || a is ExploreActionSO;
                    if (labor) continue;
                    Assert.AreEqual(1f, mult[i], 1e-5f,
                        $"{a.name}: 노동 계열이 아닌데 성향이 비용을 바꿨다 (ADR-M12-4 ① 몸값 불가침)");
                }
            }
            finally { Object.DestroyImmediate(extreme); }
        }

        // ── M12-T6: ③문턱 소비처 환산 (M12-D) ────────────────────────────────

        private static AgentConfigSO LoadCfg()
        {
            var c = AssetDatabase.LoadAssetAtPath<AgentConfigSO>("Assets/M0Config/AgentConfig.asset");
            Assert.IsNotNull(c, "AgentConfig 에셋 로드");
            return c;
        }

        [Test]
        public void M12_T6_RefuseThresholds_WillfulRefusesBothWays()
        {
            // 실사(2026-07-26)에서 확인한 성질: 두 거부 필드는 **부호 규약이 반대**일 뿐
            // 6성격 전부 방향이 일관된다 — 자존 1축으로 설명된다.
            // 그래서 같은 가중치를 쓰고 Sensitivity의 부호만 소비처가 뒤집는다.
            AgentConfigSO cfg = LoadCfg();
            var willful = new[] { V(TraitId.Willfulness, 100) };
            var meek = new[] { V(TraitId.Willfulness, -100) };

            float satW = TraitVector.Threshold(willful, cfg.RefuseSatietyBias, cfg.OrderRefuseSatiety);
            float fatW = TraitVector.Threshold(willful, cfg.RefuseFatigueBias, cfg.OrderRefuseFatigue);
            Assert.Greater(satW, cfg.OrderRefuseSatiety, "자존↑ → 배고픔 문턱 상승 = 더 쉽게 거부");
            Assert.Less(fatW, cfg.OrderRefuseFatigue, "자존↑ → 피로 문턱 하강 = 더 쉽게 거부 (부호 규약 반대)");

            float satM = TraitVector.Threshold(meek, cfg.RefuseSatietyBias, cfg.OrderRefuseSatiety);
            float fatM = TraitVector.Threshold(meek, cfg.RefuseFatigueBias, cfg.OrderRefuseFatigue);
            Assert.Less(satM, cfg.OrderRefuseSatiety, "자존↓ → 배고파도 참는다");
            Assert.Greater(fatM, cfg.OrderRefuseFatigue, "자존↓ → 피곤해도 참는다");

            // 중립 불변식: 벡터 없는 성격은 현행 기준값 그대로
            Assert.AreEqual(cfg.OrderRefuseSatiety,
                TraitVector.Threshold(null, cfg.RefuseSatietyBias, cfg.OrderRefuseSatiety), 1e-5f);
            Assert.AreEqual(cfg.OrderRefuseFatigue,
                TraitVector.Threshold(null, cfg.RefuseFatigueBias, cfg.OrderRefuseFatigue), 1e-5f);
        }

        [Test]
        public void M12_T6_FleeRadius_TimidNoticesEarlier()
        {
            AgentConfigSO cfg = LoadCfg();
            float timid = TraitVector.Threshold(new[] { V(TraitId.Caution, 100) }, cfg.FleeRadiusBias, 1f);
            float brave = TraitVector.Threshold(new[] { V(TraitId.Caution, -100) }, cfg.FleeRadiusBias, 1f);

            Assert.Greater(timid, 1f, "겁↑ → 감지 반경 확대 (먼저 알아챈다)");
            Assert.Less(brave, 1f, "겁↓ → 늦게 알아챈다 (밭을 지키다 물리는 서사)");
            Assert.Greater(brave, 0f, "배율이 0 이하로 내려가면 감지가 아예 죽는다");
            Assert.AreEqual(1f, TraitVector.Threshold(null, cfg.FleeRadiusBias, 1f), 1e-5f, "중립 = 1배");
        }

        [Test]
        public void M12_T6_DemandsUpfront_DiscretizedDeterministically()
        {
            // 불린 통로의 이산화 — 랜덤 금지(ADR-M1-2), 경계는 '이상'(>=).
            AgentConfigSO cfg = LoadCfg();
            var p = ScriptableObject.CreateInstance<PersonalitySO>();
            try
            {
                Assert.IsFalse(cfg.DemandsUpfront(null), "성격 없음 = 요구 안 함");

                p.Traits = new[] { V(TraitId.Willfulness, 0) };
                Assert.IsFalse(cfg.DemandsUpfront(p), "중립 자존 = 후불 수용");

                // 경계: 문턱 × 100이 정확히 임계값 (가중치 1.0 기준)
                int edge = Mathf.RoundToInt(cfg.UpfrontBiasThreshold * 100f);
                p.Traits = new[] { V(TraitId.Willfulness, edge) };
                Assert.IsTrue(cfg.DemandsUpfront(p), $"자존 {edge} = 경계값은 '이상'이므로 요구");
                p.Traits = new[] { V(TraitId.Willfulness, edge - 1) };
                Assert.IsFalse(cfg.DemandsUpfront(p), $"자존 {edge - 1} = 경계 바로 아래는 요구 안 함");

                // 舊 개별 필드와의 OR (M12-F까지 병존)
                p.Traits = null;
                p.DemandsRewardUpfront = true;
                Assert.IsTrue(cfg.DemandsUpfront(p), "벡터가 없어도 舊 필드가 켜져 있으면 요구 (병존)");
            }
            finally { Object.DestroyImmediate(p); }
        }

        // ── M12-T7: ④대상 — 택지 거리 (M12-E) ────────────────────────────────

        [Test]
        public void M12_T7_HomeDistance_NomadFarFriendlyNear()
        {
            TraitRulesSO rules = LoadRules();
            var nomad = ScriptableObject.CreateInstance<PersonalitySO>();
            var social = ScriptableObject.CreateInstance<PersonalitySO>();
            var blank = ScriptableObject.CreateInstance<PersonalitySO>();
            try
            {
                nomad.Traits = new[] { V(TraitId.Wanderlust, 100) };
                social.Traits = new[] { V(TraitId.Sociability, 100) };

                float far = HomePicker.PreferredDist(nomad, null, rules);
                float near = HomePicker.PreferredDist(social, null, rules);

                Assert.GreaterOrEqual(far, 0.9f, "모험 +100 → 외딴집 (비율 0.9 이상)");
                Assert.LessOrEqual(near, 0.2f, "사교 +100 → 이웃 곁 (비율 0.2 이하)");

                // M11-K 대역 규약: 비율은 0~0.95를 벗어나지 않는다 (맵-비례 변환의 전제)
                foreach (int sign in new[] { 1, -1 })
                {
                    var extreme = ScriptableObject.CreateInstance<PersonalitySO>();
                    extreme.Traits = Extreme(sign);
                    float f = HomePicker.PreferredDist(extreme, null, rules);
                    Assert.GreaterOrEqual(f, 0f, "비율 하한");
                    Assert.LessOrEqual(f, 0.95f, "비율 상한 (M11-K 대역)");
                    Object.DestroyImmediate(extreme);
                }

                // 중립 불변식 2경로: 규칙표 미배선 / 벡터 없는 성격 → 舊 필드 값 그대로
                blank.HomePreferredDist = 0.5f;
                Assert.AreEqual(0.5f, HomePicker.PreferredDist(blank, null), 1e-5f, "규칙표 없음 = 舊 경로");
                Assert.AreEqual(0.5f, HomePicker.PreferredDist(blank, null, rules), 1e-5f,
                    "벡터 없는 성격은 편향 0 → 舊 필드 그대로 (병존)");
                Assert.AreEqual(0f, HomePicker.PreferredDist(null, null, rules), 1e-5f, "성격 없음 = 최근접");
            }
            finally
            {
                Object.DestroyImmediate(nomad);
                Object.DestroyImmediate(social);
                Object.DestroyImmediate(blank);
            }
        }

        [Test]
        public void M12_T7_HomeDistance_DeterministicForSameVector()
        {
            // ④대상은 랜덤이 아니다 (M11-F 규약 계승) — 같은 벡터면 언제나 같은 값.
            TraitRulesSO rules = LoadRules();
            var a = ScriptableObject.CreateInstance<PersonalitySO>();
            var b = ScriptableObject.CreateInstance<PersonalitySO>();
            try
            {
                a.Traits = new[] { V(TraitId.Wanderlust, 40), V(TraitId.Sociability, -20) };
                b.Traits = new[] { V(TraitId.Sociability, -20), V(TraitId.Wanderlust, 40) }; // 순서만 다름
                Assert.AreEqual(HomePicker.PreferredDist(a, null, rules),
                                HomePicker.PreferredDist(b, null, rules), 1e-6f,
                    "벡터 항목 순서가 달라도 같은 결과 (가중합은 순서 무관)");
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }

        // ── M12-T8: 6성격 이식 + 축별 대사 풀 (M12-F) ────────────────────────

        private const string PERSONALITY_DIR = "Assets/M0Config/Personalities";

        private static List<PersonalitySO> LoadAllPersonalities()
        {
            var list = AssetDatabase.FindAssets("t:PersonalitySO", new[] { PERSONALITY_DIR })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<PersonalitySO>)
                .Where(p => p != null).ToList();
            Assert.IsNotEmpty(list, "성격 에셋 로드");
            return list;
        }

        [Test]
        public void M12_T8_AllPersonalitiesMigratedAndLegacyFieldsNeutral()
        {
            foreach (PersonalitySO p in LoadAllPersonalities())
            {
                Assert.IsTrue(p.Traits != null && p.Traits.Length > 0,
                    $"{p.name}: 성향 벡터가 비었다 — 이 성격은 아무 기질도 없다");

                // 舊 개별 필드는 전부 중립이어야 한다. 아니면 벡터와 이중 적용된다.
                Assert.AreEqual(1f, p.GatherCostMult,  1e-5f, $"{p.name}: 舊 채집 배율 잔존");
                Assert.AreEqual(1f, p.FarmCostMult,    1e-5f, $"{p.name}: 舊 농사 배율 잔존");
                Assert.AreEqual(1f, p.BuildCostMult,   1e-5f, $"{p.name}: 舊 건설 배율 잔존");
                Assert.AreEqual(1f, p.ExploreCostMult, 1e-5f, $"{p.name}: 舊 탐험 배율 잔존");
                Assert.AreEqual(0f, p.RefuseSatietyOffset, 1e-5f, $"{p.name}: 舊 배고픔 오프셋 잔존");
                Assert.AreEqual(0f, p.RefuseFatigueOffset, 1e-5f, $"{p.name}: 舊 피로 오프셋 잔존");
                Assert.AreEqual(1f, p.FleeRadiusMult, 1e-5f, $"{p.name}: 舊 감지 배율 잔존");
                Assert.AreEqual(0f, p.HomePreferredDist, 1e-5f, $"{p.name}: 舊 택지 거리 잔존");
                Assert.AreEqual(-100, p.SkipRewardBelowAffinity, $"{p.name}: 舊 떼먹기 문턱 잔존");
                Assert.IsFalse(p.DemandsRewardUpfront, $"{p.name}: 舊 선불 플래그 잔존");
                Assert.IsTrue(p.GoalBoosts == null || p.GoalBoosts.Length == 0,
                    $"{p.name}: 舊 GoalBoosts 잔존 — 벡터와 이중 적용된다");

                // 축 값은 정의 범위 안 (에셋 손편집 사고 방지)
                foreach (TraitValue t in p.Traits)
                    Assert.That(t.Value, Is.InRange(-100, 100), $"{p.name}: {t.Trait} 값이 범위 밖");
            }
        }

        [Test]
        public void M12_T8_MoodPool_DeterministicAndFallsBackToSilenceNotCrash()
        {
            TraitRulesSO rules = LoadRules();

            // 전 축 0 / 벡터 없음 → null (호출자가 성격 전용 대사로 폴백, 침묵은 마지막)
            Assert.IsNull(rules.MoodPoolFor(null), "벡터 없음 = 풀 없음");
            Assert.IsNull(rules.MoodPoolFor(new[] { V(TraitId.Caution, 0) }), "전 축 0 = 풀 없음");

            // 최고 |값| 축이 선택된다 — 부호까지 맞는 풀
            string[] timid = rules.MoodPoolFor(new[] { V(TraitId.Caution, 90), V(TraitId.Diligence, 20) });
            Assert.IsNotNull(timid, "겁 +90 → 겁쟁이 풀");
            string[] brave = rules.MoodPoolFor(new[] { V(TraitId.Caution, -90) });
            Assert.IsNotNull(brave, "겁 -90 → 무모한자 풀");
            Assert.AreNotSame(timid, brave, "같은 축이라도 부호가 다르면 다른 풀");

            // 결정성: 벡터 항목 순서가 달라도 같은 풀
            Assert.AreSame(timid,
                rules.MoodPoolFor(new[] { V(TraitId.Diligence, 20), V(TraitId.Caution, 90) }),
                "항목 순서 무관 (결정적)");

            // 동률이면 TraitId 작은 쪽 — 근면(0) vs 겁(5)
            string[] tie = rules.MoodPoolFor(new[] { V(TraitId.Caution, 80), V(TraitId.Diligence, 80) });
            Assert.AreSame(rules.MoodPoolFor(new[] { V(TraitId.Diligence, 80) }), tie,
                "동률은 TraitId 순 → 근면 풀");
        }

        // ── M12-T9: 조리 goal의 트리거 ↔ 목표 정합 (2026-07-26 Play 관측 회귀 방지) ──

        [Test]
        public void M12_T9_CookGoals_TriggerGuaranteesEnoughRawForTarget()
        {
            // Play 관측: Goal_WinterPrep이 겨울에 NoSolutionFound(노드 4096/4096) — 탐색 폭발.
            // 원인: 트리거는 생식 5를 보장하는데 목표가 조리 +2였고, 겨울 레시피(CookMealScarce)는
            // 생식 5 -> 조리 1이라 +2에는 생식 10이 필요했다. 몸 소지 상한이 8이라 집 저장분이
            // 없으면 **구조적으로 도달 불가** -> A*가 전 공간을 뒤진다.
            // 겨울 채집 봉쇄(ForageFrozen)가 이 goal들을 도달 불가로 만들었는데 ADR-M6-2 예외는
            // 식량 goal 3개에만 달려 있었다 = 조리·비축 goal 누락.
            var scarce = AssetDatabase.LoadAssetAtPath<ActionSO>("Assets/M0Config/Actions/CookMealScarce.asset");
            Assert.IsNotNull(scarce, "위기 레시피 로드");

            int rawPerCook = 0, cookedPerBatch = 0;
            foreach (SlotEffect e in scarce.Effects)
            {
                if (e.Slot == SlotId.MyRawFood && e.Op == EffectOp.SubClamp0) rawPerCook = e.Value;
                if (e.Slot == SlotId.MyCookedFood && e.Op == EffectOp.Add) cookedPerBatch = e.Value;
            }
            Assert.Greater(rawPerCook, 0, "위기 레시피의 생식 소모");
            Assert.Greater(cookedPerBatch, 0, "위기 레시피의 조리 산출");

            foreach (string name in new[] { "Goal_WinterPrep", "Goal_CookAhead", "Goal_CookExtra" })
            {
                var g = AssetDatabase.LoadAssetAtPath<GoalSO>($"Assets/M0Config/Goals/{name}.asset");
                Assert.IsNotNull(g, $"{name} 로드");

                int need = 0;
                foreach (SlotCondition c in g.GoalConditions)
                    if (c.Slot == SlotId.MyCookedFood) need = c.Value;
                int guaranteed = 0;
                foreach (SlotCondition c in g.TriggerConditions)
                    if (c.Slot == SlotId.MyRawFood && c.Op == CompareOp.GreaterOrEqual) guaranteed = c.Value;

                Assert.Greater(need, 0, $"{name}: 조리 목표량");
                Assert.Greater(guaranteed, 0, $"{name}: 트리거가 생식 보유를 보장해야 한다");

                int batches = Mathf.CeilToInt(need / (float)cookedPerBatch);
                Assert.LessOrEqual(batches * rawPerCook, guaranteed,
                    $"{name}: 목표 조리 {need}개에 생식 {batches * rawPerCook}이 필요한데 트리거는 " +
                    $"{guaranteed}만 보장한다 — 위기철에 도달 불가가 되어 탐색이 폭발한다 " +
                    "(발동했으면 달성 가능해야 한다, ADR-M0-7의 정신)");
            }
        }

        // ── M12-T4: 제한 플래그의 문언 ↔ 실사용 대조 (ADR 낡음 탐지기, 2026-07-26) ────

        /// <summary>
        /// "제한적으로만 쓰라"는 플래그의 **실제 사용 목록**. ADR 문언과 어긋나면 실패한다.
        ///
        /// 전수 감사(2026-07-26)에서 발견한 것: 금지형 ADR을 **허용 목록**으로 쓰면 콘텐츠가
        /// 늘 때마다 조용히 낡는다 — ADR-M2-5는 "P0 2종에만"인데 실제로 3개였고(피신),
        /// DirectActionPool 툴팁은 "여가 전용"인데 4개였다. 문서가 거짓말을 하면 다음 세션이
        /// 그걸 믿고 잘못 판단한다.
        ///
        /// 이 게이트가 실패하면 선택지는 둘뿐이다:
        ///   ① 그 goal에서 플래그를 끈다  ② ADR 문언을 개정하고 이 목록을 함께 고친다.
        /// **목록만 조용히 고치는 것은 감사를 무력화한다** — 반드시 ADR 본문도 함께 본다.
        /// </summary>
        private static readonly Dictionary<string, string[]> RestrictedFlagUsage = new Dictionary<string, string[]>
        {
            // ADR-M2-5 (쿨다운 면제 = 물러설 수 없는 goal의 자격)
            ["SkipFailureCooldown"] = new[] { "Goal_P0_Hunger", "Goal_P0_Fatigue", "Goal_Flee" },
            // GoalSO.DirectActionPool 툴팁의 자격 3조건
            ["DirectActionPool"] = new[] { "Goal_Leisure", "Goal_ReportDone", "Goal_Routine_Explorer", "Goal_Routine_Farmer" },
            // ADR-M5-4 (폴백 불변식) — "세 번째 용도 = 규칙 재검토 신호"라는 자폭 트리거 내장
            ["RequiredJob"] = new[] { "Goal_TreatInjured", "Goal_BuildMyHouse" },
            // ADR-M6-2 (게임건강 예외) — 식량 goal 3 + 명령 goal 3
            ["MayHaveNoSolution"] = new[] { "Goal_P0_Hunger", "Goal_Snack", "Goal_GatherFood",
                                            "Order_ChopWood", "Order_HarvestBerries", "Order_MineStone" },
        };

        [Test]
        public void M12_T4_RestrictedFlags_UsageMatchesADR()
        {
            List<GoalSO> goals = LoadAllGoals();

            bool Uses(GoalSO g, string flag)
            {
                switch (flag)
                {
                    case "SkipFailureCooldown": return g.SkipFailureCooldown;
                    case "DirectActionPool":    return g.DirectActionPool != null && g.DirectActionPool.Length > 0;
                    case "RequiredJob":         return g.RequiredJob != null;
                    case "MayHaveNoSolution":   return g.MayHaveNoSolution;
                    default: throw new System.ArgumentException($"미등록 플래그 {flag}");
                }
            }

            foreach (var kv in RestrictedFlagUsage)
            {
                var actual = goals.Where(g => Uses(g, kv.Key)).Select(g => g.name).OrderBy(n => n).ToArray();
                var expected = kv.Value.OrderBy(n => n).ToArray();

                CollectionAssert.AreEqual(expected, actual,
                    $"'{kv.Key}' 사용처가 ADR 문언과 어긋난다.\n" +
                    $"  문언: [{string.Join(", ", expected)}]\n" +
                    $"  실제: [{string.Join(", ", actual)}]\n" +
                    "→ 플래그를 끄거나, ADR 본문을 개정하고 이 목록을 함께 고칠 것. " +
                    "목록만 고치면 감사가 무력화된다.");
            }
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
