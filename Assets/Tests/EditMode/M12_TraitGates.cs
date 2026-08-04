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

        // ── M12-T10: 서비스 직업의 접근성 (2026-07-26 Play 관측 회귀 방지) ──────

        [Test]
        public void M12_T10_RequestTargetJobs_LiveNearVillageCenter()
        {
            // Play 관측: 떠돌이 성격의 목수가 기지에서 38타일 밖(마을 반경 40 × 비율 0.95)에 집을 지었고,
            // 주민들이 각자 자기 집 근처에서만 생활하는 탈중심 마을(M11-K)이라 아무도 6타일 안에
            // 들어오지 못해 **집 부탁이 영구히 성립하지 않았다** -> 집 없는 주민 아사.
            //
            // 반경을 늘리는 것은 해법이 아니다 — 부탁 연출이 FaceForChat(양쪽이 멈추고 마주봄)이라
            // 멀어지면 맵 반대편끼리 허공에 대고 말하는 장면이 된다. 거리는 **배치**로 푼다.
            //
            // 규칙을 목록이 아니라 구조로 건다: 부탁 대상이 되는 직업은 마을 중심 쪽에 산다.
            // 새 부탁 종류(RequestSO)가 생기면 그 대상 직업도 자동으로 이 검사에 걸린다.
            var requests = AssetDatabase.FindAssets("t:RequestSO", new[] { "Assets/M0Config" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<RequestSO>)
                .Where(r => r != null).ToList();
            Assert.IsNotEmpty(requests, "부탁 에셋 로드");

            var serviceJobs = requests.Where(r => r.TargetJob != null).Select(r => r.TargetJob).Distinct().ToList();
            Assert.IsNotEmpty(serviceJobs, "대상 직업이 지정된 부탁이 하나는 있어야 한다");

            TraitRulesSO rules = LoadRules();
            var nomad = ScriptableObject.CreateInstance<PersonalitySO>();
            try
            {
                nomad.Traits = new[] { V(TraitId.Wanderlust, 100) }; // 가장 멀리 살려는 기질
                foreach (JobSO job in serviceJobs)
                {
                    Assert.Less(job.HomePreferredDist, 0f,
                        $"{job.name}: 부탁 대상 직업인데 택지 가산이 중심 쪽이 아니다 — " +
                        "외딴 곳에 살면 부탁이 물리적으로 닿지 않아 그 서비스가 마을에서 사라진다");

                    float frac = HomePicker.PreferredDist(nomad, job, rules);
                    Assert.LessOrEqual(frac, 0.25f,
                        $"{job.name}: 가장 방랑벽 강한 성격과 겹쳐도 마을 반경의 25% 안에 살아야 한다 " +
                        $"(실효 비율 {frac:F2}) — 성격이 서비스 접근성을 끊으면 안 된다");
                }
            }
            finally { Object.DestroyImmediate(nomad); }
        }

        // ── M12-T11: 행동 프로파일 계측 (M12-J) ──────────────────────────────

        [Test]
        public void M12_T11_Profiler_PureHelpersAreDeterministic()
        {
            // 계측 주기: 간격 0 = 끄기(중립). 관측 장치가 켜져 있다는 이유로 게임이 달라지면 안 된다.
            Assert.IsFalse(BehaviorProfiler.ShouldLog(100f, 0f, 0f), "간격 0 = 계측 끄기");
            Assert.IsFalse(BehaviorProfiler.ShouldLog(6.9f, 7f, 7f), "주기 전");
            Assert.IsTrue(BehaviorProfiler.ShouldLog(7f, 7f, 7f), "경계는 '이상'");
            Assert.AreEqual(14f, BehaviorProfiler.AdvanceLogDay(7f, 7f), 1e-5f, "다음 시점");
            Assert.AreEqual(27f, BehaviorProfiler.AdvanceLogDay(20f, 7f), 1e-5f,
                "밀렸어도 현재 시점 기준 — 지난 주기를 몰아서 찍지 않는다");

            // 상위 goal 절단: 동률은 이름 순으로 **결정적**이어야 재현 가능한 관측이 된다
            var picks = new Dictionary<string, int>
            {
                ["Goal_Plant"] = 5, ["Goal_Leisure"] = 5, ["Goal_GatherWood"] = 5, ["Goal_Snack"] = 1,
            };
            HashSet<string> top3 = BehaviorProfiler.TopGoals(picks, 3);
            Assert.AreEqual(3, top3.Count);
            Assert.IsFalse(top3.Contains("Goal_Snack"), "최하위는 잘린다");
            CollectionAssert.AreEquivalent(top3, BehaviorProfiler.TopGoals(picks, 3), "같은 입력 = 같은 결과");

            Assert.IsEmpty(BehaviorProfiler.TopGoals(null, 3), "기록 없음 = 빈 집합");
        }

        [Test]
        public void M12_T11_Profiler_DivergentPairsMeasuresS4()
        {
            // S4 판정기: 상위 goal '구성'이 다른 성격 쌍의 수. 순서가 아니라 집합을 본다 —
            // 1·2위가 뒤바뀐 정도는 노이즈고, 무엇을 주로 하는가가 갈려야 성격이 보이는 것이다.
            var a = new HashSet<string> { "Goal_Plant", "Goal_HarvestCrop" };
            var b = new HashSet<string> { "Goal_HarvestCrop", "Goal_Plant" }; // 순서만 다름 = 같은 구성
            var c = new HashSet<string> { "Goal_Leisure", "Goal_Snack" };

            Assert.AreEqual(0, BehaviorProfiler.DivergentPairs(new[] { a, b }), "같은 구성 = 분화 0");
            Assert.AreEqual(1, BehaviorProfiler.DivergentPairs(new[] { a, c }), "다른 구성 = 1쌍");
            Assert.AreEqual(2, BehaviorProfiler.DivergentPairs(new[] { a, b, c }),
                "3종 중 c만 달라 a-c, b-c 두 쌍");
            Assert.AreEqual(0, BehaviorProfiler.DivergentPairs(new[] { a }), "1종이면 비교 불가");
            Assert.AreEqual(0, BehaviorProfiler.DivergentPairs(null), "입력 없음");
        }

        // ── M12-T12: 자가 소유 배정 가드 (2026-07-26 Play 관측 회귀 방지) ──────

        [Test]
        public void M12_T12_SelfAssign_JudgesTheBuildingNotTheBusyness()
        {
            // Play 관측: 집 부탁을 수락한 목수가 자기 모닥불을 짓자 소유가 배정되지 않아
            // MyHasCampfire가 0으로 남았고, Goal_BuildCampfire(50)가 부탁받은 집(36)을 계속 이겨
            // **집 주변 반경 2가 만원이 될 때까지 모닥불을 반복 건축**했다.
            // 원인은 판정 기준이 "부탁 수행 중인가"(누구)였다는 것 — "이 건물이 그 부탁의 소유
            // 대상인가"(무엇)로 바꿔야 한다.
            var houseReq = AssetDatabase.LoadAssetAtPath<RequestSO>(
                "Assets/M0Config/Requests/Request_BuildMyHouse.asset");
            var cookReq = AssetDatabase.LoadAssetAtPath<RequestSO>(
                "Assets/M0Config/Requests/Request_CookForMe.asset");
            Assert.IsNotNull(houseReq, "집 부탁 로드");
            Assert.IsNotNull(cookReq, "요리 부탁 로드");

            // 부탁 없음 = 지은 사람 것
            Assert.IsTrue(RequestService.ShouldSelfAssign(null, SlotId.CampfireCount));
            Assert.IsTrue(RequestService.ShouldSelfAssign(null, SlotId.HouseCount));

            // 집 부탁 수행 중이라도 **모닥불은 내 것** — 이것이 회귀의 핵심
            Assert.IsTrue(RequestService.ShouldSelfAssign(houseReq, SlotId.CampfireCount),
                "집 부탁 중에 지은 모닥불은 지은 사람 것이어야 한다 (반복 건축의 원인)");

            // 그 부탁의 대상인 집만 의뢰인 몫으로 남긴다
            Assert.IsFalse(RequestService.ShouldSelfAssign(houseReq, SlotId.HouseCount),
                "부탁받아 지은 집은 의뢰인 것 (RequestService.NotifyFulfilled가 배정)");

            // 소유를 안 넘기는 부탁(요리)은 어떤 건물도 막지 않는다
            Assert.IsTrue(RequestService.ShouldSelfAssign(cookReq, SlotId.HouseCount));
            Assert.IsTrue(RequestService.ShouldSelfAssign(cookReq, SlotId.CampfireCount));
        }

        [Test]
        public void M12_T12_OwnedBuildingGoals_TerminateOnOwnership()
        {
            // 반복 건축의 구조적 조건: '소유 건물' goal의 목표가 **소유 플래그**여야 한다.
            // 목표가 수량(Count)이면 소유 배정이 실패해도 goal이 끝나 조용히 넘어가지만,
            // 소유 플래그면 배정 누락이 즉시 무한 건축으로 드러난다 — 지금이 그 경우였다.
            var campfire = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/Campfire.asset");
            var house = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/House.asset");
            Assert.IsTrue(campfire.OwnedBuilding && campfire.IsCountable, "모닥불 = 개인 소유 수량형");
            Assert.IsTrue(house.OwnedBuilding && house.IsCountable, "집 = 개인 소유 수량형");

            var goal = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_BuildCampfire.asset");
            bool targetsOwnership = false;
            foreach (SlotCondition c in goal.GoalConditions)
                if (c.Slot == SlotId.MyHasCampfire) targetsOwnership = true;
            Assert.IsTrue(targetsOwnership,
                "Goal_BuildCampfire의 목표는 MyHasCampfire(소유)여야 한다 — " +
                "수량으로 바꾸면 소유 배정 누락이 조용히 숨는다");
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
            // (M17-R3 추가: Goal_SeekCarpenter — ⓐ의 두 번째 종류 "끝이 슬롯 밖에 있는 것".
            //  '목수 곁에 도착'은 위치라 GoalConditions로 쓸 수 없다. ⓑ 풀 1개 ⓒ 이동만·효과 없음)
            ["DirectActionPool"] = new[] { "Goal_Leisure", "Goal_ReportDone", "Goal_Routine_Explorer",
                                           "Goal_Routine_Farmer", "Goal_SeekCarpenter" },
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
            // Goal_SaveForHome은 M19-W1에서 화폐와 함께 삭제 (저축 goal 소멸 — 목록 동기화)
            var personal = new[] { "Goal_BuildMyHouse", "Goal_StoreFood",
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

        // ── M12-T13: 집 부탁의 기질 문턱 + 경험 우회 (M12-G) ──────────────────
        // 명세는 이 게이트를 M12-T7로 불렀으나 T7은 이미 ④대상(택지 거리)이 쓰고 있어
        // 실제 파일의 연번을 따른다.

        private static WorldSnapshot SnapWith(SlotId slot, int value)
        {
            var slots = new int[PlanningConfig.TotalSlots];
            slots[(int)slot] = value;
            return new WorldSnapshot(slots);
        }

        [Test]
        public void M12_T13_HouseRequest_TraitGateAndStarvedBypass()
        {
            var r = AssetDatabase.LoadAssetAtPath<RequestSO>(
                "Assets/M0Config/Requests/Request_BuildMyHouse.asset");
            Assert.IsNotNull(r, "Request_BuildMyHouse 로드");
            Assert.IsNotEmpty(r.RequesterTraits, "집 부탁에 성향 문턱이 걸려 있어야 한다 (M12-G의 핵심)");

            WorldSnapshot none = SnapWith(SlotId.MyWasStarved, 0);
            WorldSnapshot starved = SnapWith(SlotId.MyWasStarved, 1);

            // 게으름뱅이(대비 -70)는 스톡 조건을 이미 통과했어도 집을 부탁하지 않는다.
            // 이것이 성격 페널티 우회 구조(2026-07-24 관측)의 차단 지점이다.
            PersonalitySO lazy = LoadAllPersonalities().First(p => p.name == "Personality_Lazy");
            Assert.IsFalse(RequestService.RequesterQualifies(r, lazy, none),
                "게으름뱅이는 여력이 있어도 집 부탁이 성립하면 안 된다");

            // 그러나 굶어 죽을 뻔한 경험이 있으면 기질을 넘어 성립한다 (경험 > 기질).
            Assert.IsTrue(RequestService.RequesterQualifies(r, lazy, starved),
                "MyWasStarved면 대비 문턱을 우회해 성립해야 한다 (영원히 못 하는 사람 방지)");

            // 대비가 문턱 이상인 성격은 경험 없이도 성립 — 문턱이 마을 전체를 막지 않는다.
            List<PersonalitySO> qualifying = LoadAllPersonalities()
                .Where(p => RequestService.RequesterQualifies(r, p, none)).ToList();
            Assert.IsNotEmpty(qualifying, "경험 없이 집을 부탁할 수 있는 성격이 최소 1종은 있어야 한다");
            // M10 '규모 8 정체' 재현 방어 — 집이 안 서면 위협 티어 진행까지 막힌다.
            Assert.GreaterOrEqual(qualifying.Count, LoadAllPersonalities().Count / 2,
                "성향 문턱이 과반을 막으면 집이 안 서서 마을 규모가 정체한다");

            // 중립 불변식 — 성향 조건이 비면 성격과 무관하게 현행 동작.
            var neutral = ScriptableObject.CreateInstance<RequestSO>();
            Assert.IsTrue(RequestService.RequesterQualifies(neutral, lazy, none),
                "RequesterTraits가 비면 성향 무관 = 현행 동작(중립 불변식)");
            // 성격 미배정도 중립 — 전 축 0인 벡터와 같아야 한다.
            Assert.IsTrue(RequestService.RequesterQualifies(neutral, null, none),
                "성격 null도 중립 경로");
            Object.DestroyImmediate(neutral);
        }

        [Test]
        public void M12_T13_NearStarvation_IsRarerThanHungerAndSurvivable()
        {
            var c = AssetDatabase.LoadAssetAtPath<AgentConfigSO>("Assets/M0Config/AgentConfig.asset");
            Assert.IsNotNull(c, "AgentConfig 로드");

            // 희소성 — 굶주림 시작 즉시가 아니어야 한다 (겨울 봉쇄로 굶주림은 흔하다).
            Assert.Greater(c.NearStarvationRatio, 0f,
                "비율 0이면 굶주림 시작 즉시 참이 되어 전원이 기록된다 (성향 문턱 무력화 = 우회 재발)");
            // 생존 가능성 — 아사 문턱 이상이면 그 틱에 죽으므로 아무도 기록되지 못한다.
            Assert.Less(c.NearStarvationRatio, 1f,
                "비율 1이면 기록 시점 = 사망 시점이라 살아남은 자가 존재할 수 없다");

            float death = c.DepartAfterStarvingDays;
            Assert.IsFalse(VillagerAgent.IsNearStarvation(0f, c), "굶주림 0일에는 기록 없음");
            Assert.IsFalse(VillagerAgent.IsNearStarvation(death * c.NearStarvationRatio * 0.5f, c),
                "문턱 이전에는 기록 없음");
            Assert.IsTrue(VillagerAgent.IsNearStarvation(death * c.NearStarvationRatio, c),
                "문턱에 닿으면 기록");
            // 기록 지점이 사망 지점보다 반드시 앞 — 이 순서가 깨지면 슬롯이 영구히 0이다.
            Assert.IsFalse(VillagerAgent.ShouldStarveToDeath(death * c.NearStarvationRatio, c),
                "기록 시점에는 아직 죽지 않아야 한다 (살아남아야 표시가 쓸모 있다)");
        }

        // ── M12-T14: 직업 배정 성향 편향 + 목수 최소 보장 (M12-H) ──────────────
        // 명세는 M12_T8로 불렀으나 T8은 이미 6성격 이식이 점유 → 실제 파일 연번을 따른다.

        private const string JOBS_DIR = "Assets/M0Config/Jobs";

        private static JobSO[] LoadAllJobs()
        {
            JobSO[] jobs = AssetDatabase.FindAssets("t:JobSO", new[] { JOBS_DIR })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<JobSO>)
                .Where(j => j != null)
                .OrderBy(j => j.name).ToArray();
            Assert.IsNotEmpty(jobs, "직업 에셋 로드");
            return jobs;
        }

        /// <summary>roll01을 촘촘히 훑어 각 직업이 뽑히는 비율을 실측한다 (확률 편향의 유일한 검증법).</summary>
        private static Dictionary<string, int> SampleJobs(TraitValue[] traits, JobSO[] pool,
                                                          TraitRulesSO rules, int samples = 1000)
        {
            var hist = new Dictionary<string, int>();
            for (int i = 0; i < samples; i++)
            {
                int idx = M0SimulationLoop.PickJobIndex(traits, pool, rules, (i + 0.5f) / samples);
                string key = idx >= 0 ? pool[idx].name : "(무직)";
                hist[key] = hist.TryGetValue(key, out int n) ? n + 1 : 1;
            }
            return hist;
        }

        [Test]
        public void M12_T14_JobPick_NeutralWhenUnwired()
        {
            JobSO[] pool = LoadAllJobs();
            var diligent = new[] { V(TraitId.Diligence, 100) };

            // rules가 null이면 강도 0 → 전 직업 가중치 1 → 균등 = 현행 독립 랜덤 (중립 불변식).
            Dictionary<string, int> hist = SampleJobs(diligent, pool, null, pool.Length * 100);
            Assert.AreEqual(pool.Length, hist.Count, "미배선이면 전 직업이 고르게 나와야 한다");
            foreach (KeyValuePair<string, int> kv in hist)
                Assert.AreEqual(100, kv.Value, 1, $"{kv.Key}: 미배선은 균등 추첨이어야 한다");
            Assert.IsFalse(hist.ContainsKey("(무직)"), "미배선이면 무직 후보가 없다 (현행 동작)");

            // 성향이 전 축 0이어도 균등 — 벡터 없는 주민(성격 null)의 경로.
            var wired = ScriptableObject.CreateInstance<TraitRulesSO>();
            wired.JobBiasStrength = 1f;
            Dictionary<string, int> flat = SampleJobs(null, pool, wired, pool.Length * 100);
            foreach (KeyValuePair<string, int> kv in flat)
                Assert.AreEqual(100, kv.Value, 1, $"{kv.Key}: 벡터가 없으면 편향 0 = 균등");
            Object.DestroyImmediate(wired);
        }

        [Test]
        public void M12_T14_JobPick_BiasedButNeverDeterministic()
        {
            JobSO[] pool = LoadAllJobs();
            var rules = AssetDatabase.LoadAssetAtPath<TraitRulesSO>("Assets/M0Config/TraitRules.asset");
            Assert.IsNotNull(rules, "TraitRules 로드");
            Assert.Greater(rules.JobBiasStrength, 0f, "편향 강도가 0이면 M12-H가 통째로 휴면이다");

            // 전 직업이 성향을 읽어야 편향이 의미를 갖는다 (한 직업이라도 비면 그 직업만 무색).
            foreach (JobSO j in pool)
                Assert.IsNotEmpty(j.PreferWeights, $"{j.name}: PreferWeights 미기입 — 성향과 무관해진다");

            // 모험가는 탐험가를, 사교가는 치료사를 더 자주 고른다 (축이 실제로 갈리는지).
            var nomad = new[] { V(TraitId.Wanderlust, 100) };
            var social = new[] { V(TraitId.Sociability, 100) };
            Dictionary<string, int> nomadHist = SampleJobs(nomad, pool, rules);
            Dictionary<string, int> socialHist = SampleJobs(social, pool, rules);

            Assert.Greater(nomadHist.GetValueOrDefault("Job_Explorer"),
                           socialHist.GetValueOrDefault("Job_Explorer"),
                           "모험 축이 높으면 탐험가가 더 자주 나와야 한다");
            Assert.Greater(socialHist.GetValueOrDefault("Job_Medic"),
                           nomadHist.GetValueOrDefault("Job_Medic"),
                           "사교 축이 높으면 치료사가 더 자주 나와야 한다");

            // ⚠️ 편향이지 결정론이 아니다 — 어떤 축이든 모든 직업의 확률이 0이 되면 안 된다
            // ("게으른데 손재주는 있는 목수"가 나올 수 있어야 사람이 입체적이다).
            foreach (TraitId axis in System.Enum.GetValues(typeof(TraitId)))
                foreach (int sign in new[] { -100, 100 })
                {
                    Dictionary<string, int> h = SampleJobs(new[] { V(axis, sign) }, pool, rules, 4000);
                    foreach (JobSO j in pool)
                        Assert.Greater(h.GetValueOrDefault(j.name), 0,
                            $"{axis}={sign}에서 {j.name}이 확률 0 — 편향이 결정론이 됐다");
                }
        }

        [Test]
        public void M12_T14_JobPick_IdlenessIsAWeightNotARule()
        {
            JobSO[] pool = LoadAllJobs();
            var rules = AssetDatabase.LoadAssetAtPath<TraitRulesSO>("Assets/M0Config/TraitRules.asset");
            PersonalitySO lazy = LoadAllPersonalities().First(p => p.name == "Personality_Lazy");

            Dictionary<string, int> lazyHist = SampleJobs(lazy.Traits, pool, rules, 4000);
            Assert.Greater(lazyHist.GetValueOrDefault("(무직)"), 0,
                "게으름뱅이(근면 -80)는 무직이 후보로 올라와야 한다 (M11 '게으름 = 대비만' 정의 개정)");
            // 규칙이 아니라 확률 — 게으름뱅이도 직업을 가질 수 있다.
            Assert.Less(lazyHist.GetValueOrDefault("(무직)"), 4000,
                "무직이 100%면 확률이 아니라 규칙이다 (M12-H ⚠️)");

            // 문턱 위의 성격에겐 무직 후보가 아예 없다 — 문턱이 실제로 갈라야 한다.
            foreach (PersonalitySO p in LoadAllPersonalities())
            {
                if (TraitVector.ValueOf(p.Traits, TraitId.Diligence) >= rules.NoJobBelowDiligence)
                    Assert.AreEqual(0, SampleJobs(p.Traits, pool, rules, 2000).GetValueOrDefault("(무직)"),
                        $"{p.name}: 근면이 문턱 이상인데 무직 후보가 생겼다");
            }
        }

        [Test]
        public void M12_T14_CarpenterGuarantee_FiresOnlyForTheLastStarter()
        {
            // 마지막 한 명에게만, 그리고 아직 없을 때만 발동한다.
            Assert.IsTrue(M0SimulationLoop.MustForceGuaranteedJob(3, 4, false), "정원 4의 마지막(4번째)에서 발동");
            Assert.IsFalse(M0SimulationLoop.MustForceGuaranteedJob(3, 4, true), "이미 목수가 있으면 발동 안 함");
            Assert.IsFalse(M0SimulationLoop.MustForceGuaranteedJob(0, 4, false), "첫 주민을 강제하면 편향이 죽는다");
            Assert.IsFalse(M0SimulationLoop.MustForceGuaranteedJob(2, 4, false), "중간 주민에게는 발동 안 함");
            // 방랑자(정원 밖)는 보장 대상이 아니다 — 카운터가 이미 정원을 넘었다.
            Assert.IsFalse(M0SimulationLoop.MustForceGuaranteedJob(9, 4, false), "정원 밖(방랑자)에는 발동 안 함");
            Assert.IsFalse(M0SimulationLoop.MustForceGuaranteedJob(0, 0, false), "주민이 없으면 발동 안 함");

            // 씬 배선 — 보장 직업이 실제로 목수여야 한다 (집이 목수 부탁 전용이라 이게 방어선).
            JobSO carpenter = LoadAllJobs().First(j => j.name == "Job_Carpenter");
            var request = AssetDatabase.LoadAssetAtPath<RequestSO>(
                "Assets/M0Config/Requests/Request_BuildMyHouse.asset");
            Assert.AreEqual(carpenter, request.TargetJob,
                "집 부탁의 대상 직업이 곧 최소 보장 대상이어야 한다 (보장 대상이 어긋나면 방어선이 헛돈다)");
        }
    }
}
