using AIVillage.Core;
using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M20 직업 게이트 — "부재가 느껴지는 장치"(ADR-M20-1) 헌장의 자동 감시.
    ///
    /// T1 중립 불변식: 아래 예외표에 없는 직업은 새 효율 배율이 전부 1이어야 한다.
    ///                 명시 없이 생긴 배율은 오염이다 (M5-S3·M19-W2 교훈).
    /// T2 전문가 값: 승격 4종이 실제로 남보다 빠른가 (부재 시나리오의 근거).
    /// T3 식사 불가침: 조리 플래그가 켜진 액션은 조리 2종뿐 (ADR-M5-3 몸값 불가침).
    /// T4 배율 조회: 자원별 배열의 중립·분기 동작.
    /// </summary>
    public class M20_JobGates
    {
        /// <summary>효율 배율이 허용된 직업과 그 값 — 이 게이트의 판정 단일 출처.
        /// 승격을 추가하려면 명세 개정 사유와 함께 여기부터 고친다 (조용한 추가 차단).</summary>
        private struct JobExpect
        {
            public string Name;
            public float Farm;
            public float Cook;
            public ResourceType GatherRes;
            public float GatherMult;   // 0 = 채집 배율 없음

            public JobExpect(string name, float farm, float cook, ResourceType res, float gather)
            {
                Name = name; Farm = farm; Cook = cook; GatherRes = res; GatherMult = gather;
            }
        }

        /// <summary>승격 4종 (M20-W3). 값은 전부 제안치 0.5 — 유일 선례인 목수
        /// BuildDurationMult 0.5(M19)의 복제이지 발명이 아니다 (명세 §4).</summary>
        private static readonly JobExpect[] Expected =
        {
            new JobExpect("Job_Farmer",     0.5f, 1f,   ResourceType.RawFood, 0f),
            new JobExpect("Job_Cook",       1f,   0.5f, ResourceType.RawFood, 0f),
            new JobExpect("Job_Lumberjack", 1f,   1f,   ResourceType.Wood,    0.5f),
            new JobExpect("Job_Miner",      1f,   1f,   ResourceType.Stone,   0.5f),
        };

        private static bool TryExpect(string jobName, out JobExpect expect)
        {
            foreach (JobExpect e in Expected)
                if (e.Name == jobName) { expect = e; return true; }
            expect = default;
            return false;
        }

        // ── T1: 중립 불변식 — 예외표 밖은 전부 1 ────────────────────────────

        [Test]
        public void M20_T1_DurationMults_NeutralExceptListed()
        {
            var fresh = ScriptableObject.CreateInstance<JobSO>();
            Assert.AreEqual(1f, fresh.FarmDurationMult, "신규 JobSO 밭일 기본 = 1 (중립)");
            Assert.AreEqual(1f, fresh.CookDurationMult, "신규 JobSO 조리 기본 = 1 (중립)");
            Object.DestroyImmediate(fresh);

            foreach (string guid in AssetDatabase.FindAssets("t:JobSO", new[] { "Assets/M0Config/Jobs" }))
            {
                var job = AssetDatabase.LoadAssetAtPath<JobSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (job == null) continue;
                if (TryExpect(job.name, out _)) continue; // 값 검사는 T2 담당

                Assert.AreEqual(1f, job.FarmDurationMult,
                    $"{job.name}: 예외표에 없는 직업의 밭일 배율은 1 (명시 없는 배율 = 오염)");
                Assert.AreEqual(1f, job.CookDurationMult,
                    $"{job.name}: 예외표에 없는 직업의 조리 배율은 1");
                Assert.IsTrue(job.GatherDurationMults == null || job.GatherDurationMults.Length == 0,
                    $"{job.name}: 예외표에 없는 직업은 자원별 채집 배율을 갖지 않는다");
            }
        }

        // ── T2: 전문가 값 — 승격 직업이 실제로 빠른가 ────────────────────────

        [Test]
        public void M20_T2_SpecialistsAreFaster()
        {
            foreach (JobExpect e in Expected)
            {
                var job = AssetDatabase.LoadAssetAtPath<JobSO>($"Assets/M0Config/Jobs/{e.Name}.asset");
                Assert.IsNotNull(job, $"{e.Name} 에셋 존재");
                Assert.AreEqual(e.Farm, job.FarmDurationMult, $"{e.Name} 밭일 배율");
                Assert.AreEqual(e.Cook, job.CookDurationMult, $"{e.Name} 조리 배율");

                if (e.GatherMult <= 0f)
                {
                    Assert.IsTrue(job.GatherDurationMults == null || job.GatherDurationMults.Length == 0,
                        $"{e.Name}: 채집 전문이 아니므로 자원별 배율을 갖지 않는다");
                    continue;
                }
                Assert.AreEqual(e.GatherMult, job.GatherDurationMultFor(e.GatherRes),
                    $"{e.Name}: {e.GatherRes} 채집 보너스");

                // 자원별 구분이 두 직업(나무꾼·광부)을 가르는 유일한 선 — 남의 자원은 중립이어야 한다
                ResourceType other = e.GatherRes == ResourceType.Wood ? ResourceType.Stone : ResourceType.Wood;
                Assert.AreEqual(1f, job.GatherDurationMultFor(other),
                    $"{e.Name}: {other}는 전문 밖이므로 1 — 한 직업이 모든 채집을 잘하면 구분이 사라진다");
            }
        }

        // ── T3: 식사 불가침 — 조리 플래그는 조리 액션에만 (ADR-M5-3·ADR-M20-3) ─────

        [Test]
        public void M20_T3_CookingFlag_OnlyOnCookActions()
        {
            // 이 단언이 실패하는 구현: Eat* 액션에 IsCookingWork가 켜지면 식사 속도가 직업에
            // 따라 달라진다 = 몸값 불가침 위반. 그 순간 red가 되는 것이 이 게이트의 존재 이유다.
            int flagged = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ConsumeActionSO", new[] { "Assets/M0Config" }))
            {
                var action = AssetDatabase.LoadAssetAtPath<ConsumeActionSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (action == null || !action.IsCookingWork) continue;
                flagged++;
                Assert.IsTrue(action.name == "CookMeal" || action.name == "CookMealScarce",
                    $"{action.name}: 조리 노동 플래그는 조리 액션에만 허용된다 (ADR-M20-3)");
            }
            Assert.AreEqual(2, flagged, "IsCookingWork가 켜진 액션은 정확히 2곳(CookMeal·CookMealScarce)");
        }

        // ── T5: 집 사슬 순서 — 부탁이 자가 건축보다 위 (ADR-M20-6) ────────────

        [Test]
        public void M20_T5_HouseChain_AskOutranksSelfBuild()
        {
            var self = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_BuildMyHouse.asset");
            var ask  = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_SeekCarpenter.asset");
            Assert.IsNotNull(self, "Goal_BuildMyHouse 에셋 존재");
            Assert.IsNotNull(ask, "Goal_SeekCarpenter 에셋 존재");

            // 이 단언이 실패하는 구현 = M19가 만든 바로 그 상태(자가 34 > 부탁 17)다.
            // 독점이 곧 필터였던 시절엔 무해했지만, 누구나 자가 건축을 할 수 있게 된 뒤로는
            // 부탁 경로에 순번이 영영 오지 않는다 (2026-08-05 Play 관측). 회귀 감시선.
            Assert.Less(self.Priority, ask.Priority,
                "자가 건축은 부탁하러 가기보다 낮아야 한다 — 부탁이 기본, 자가가 폴백 (ADR-M20-6)");

            // 다만 결정론이 아니라 편향이다: 성향 보정(±PriorityScale)이 개인을 가를 수 있도록
            // 같은 대역에 둔다. 격차가 보정 진폭을 넘으면 성격 분화가 다시 덮인다.
            Assert.LessOrEqual(ask.Priority - self.Priority, Mathf.RoundToInt(self.PriorityScale),
                "격차가 성향 보정 진폭을 넘으면 '고집쟁이는 혼자, 순한 사람은 부탁'이 사라진다");
        }

        // ── T6: 지불 능력 정합 — 유상 부탁은 성립 조건에 대가를 건다 ──────────

        [Test]
        public void M20_T6_PaidRequest_RequiresMeansUpfront()
        {
            var req = AssetDatabase.LoadAssetAtPath<RequestSO>(
                "Assets/M0Config/Requests/Request_BuildMyHouse.asset");
            Assert.IsNotNull(req, "Request_BuildMyHouse 에셋 존재");
            Assert.Greater(req.RewardCostAmount, 0, "집 부탁은 유상이다");

            // 선불 요구 성격(AgentConfigSO.DemandsUpfront)이 존재하는 한, 지불 능력이 성립
            // 조건에 없는 유상 부탁은 구조적 모순이다 — 빈손 의뢰인이 부탁을 걸고 거절당한다
            // (2026-08-05 Play 관측: "선불 없이는 안 해"). M19가 돈 조건을 지우며 생긴 구멍.
            Assert.IsTrue(HasMeansCondition(req.RequesterConditions, req),
                $"의뢰인 성립 조건에 {req.RewardCostSlot} ≥ {req.RewardCostAmount}가 있어야 한다");

            // 수단 없이 걸어가지도 않는다 — 같은 조건이 부탁하러 가기 트리거에도 있어야 한다
            var seek = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_SeekCarpenter.asset");
            Assert.IsTrue(HasMeansCondition(seek.TriggerConditions, req),
                "부탁하러 가는 트리거에도 같은 지불 능력 조건이 있어야 한다 (헛걸음 방지)");
        }

        /// <summary>대가 슬롯을 대가 수량 이상으로 요구하는 조건이 있는가 — 값 불일치는 red
        /// (대사 "곡식 다섯 알"·RewardCostAmount·조건 Value 세 곳이 갈리면 말과 규칙이 어긋난다).</summary>
        private static bool HasMeansCondition(SlotCondition[] conditions, RequestSO r)
        {
            if (conditions == null) return false;
            foreach (SlotCondition c in conditions)
                if (c.Slot == r.RewardCostSlot && c.Op == CompareOp.GreaterOrEqual
                    && !c.CompareToSlot && c.Value == r.RewardCostAmount)
                    return true;
            return false;
        }

        // ── T4: 자원별 배율 조회 ─────────────────────────────────────────────

        [Test]
        public void M20_T4_GatherMultLookup_NeutralAndBranching()
        {
            var job = ScriptableObject.CreateInstance<JobSO>();

            // 빈 배열 = 전 자원 중립
            Assert.AreEqual(1f, job.GatherDurationMultFor(ResourceType.Wood), "미정의 자원 = 1 (중립)");

            job.GatherDurationMults = new[]
            {
                new GatherDurationMult { Resource = ResourceType.Wood, Mult = 0.5f }
            };
            Assert.AreEqual(0.5f, job.GatherDurationMultFor(ResourceType.Wood), "정의된 자원 = 그 값");
            Assert.AreEqual(1f, job.GatherDurationMultFor(ResourceType.Stone), "미정의 자원은 여전히 1");
            Assert.AreEqual(1f, job.GatherDurationMultFor(ResourceType.Iron),
                "새 자원(격퇴 축 재료)이 생겨도 기존 직업은 자동 중립");

            Object.DestroyImmediate(job);
        }
    }
}
