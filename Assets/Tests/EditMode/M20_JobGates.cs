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

        // ── T5: 집은 개인이 짓는다 (ADR-M20-7) ───────────────────────────────

        [Test]
        public void M20_T5_NoRequestGrantsHouseOwnership()
        {
            var world = AssetDatabase.LoadAssetAtPath<WorldConfigSO>("Assets/M0Config/WorldConfig.asset");
            Assert.IsNotNull(world, "WorldConfig 에셋 존재");
            if (world.Requests == null) return;

            // 집은 개인이 짓는다 (M19 독점 해제의 완결) — 부탁으로 남의 집을 지어 주는 경로는
            // 없다. 이 단언이 실패하는 구현 = 집 부탁을 다시 만든 상태. 되살리려면 ADR-M20-7의
            // 개정 사유부터 쓴다 (두 번 패치하고도 맴돌기·경고 폭풍이 났던 사슬이다 — §9~10).
            // 판정은 이름이 아니라 소유 슬롯(값 신원)으로 — 새 이름으로 만들어도 잡힌다.
            foreach (RequestSO r in world.Requests)
            {
                if (r == null) continue;
                Assert.IsFalse(r.GrantOwnership && r.OwnershipSlot == SlotId.HouseCount,
                    $"{r.name}: 집 소유를 배정하는 부탁은 금지다 (ADR-M20-7 — 집은 개인이 짓는다)");
            }
        }

        // ── T6: 지불 능력 정합 — 활성 유상 부탁은 성립 조건에 대가를 건다 ─────

        [Test]
        public void M20_T6_ActivePaidRequests_RequireMeansUpfront()
        {
            var world = AssetDatabase.LoadAssetAtPath<WorldConfigSO>("Assets/M0Config/WorldConfig.asset");
            Assert.IsNotNull(world, "WorldConfig 에셋 존재");
            if (world.Requests == null) return;

            // 선불 요구 성격(AgentConfigSO.DemandsUpfront)이 존재하는 한, 지불 능력이 성립
            // 조건에 없는 **유상** 부탁은 구조적 모순이다 — 빈손 의뢰인이 부탁을 걸고 거절당한다
            // (2026-08-05 Play 관측: "선불 없이는 안 해". M19가 돈 조건을 지우며 생긴 구멍).
            // 현재 활성 부탁은 전부 무상이라 대상이 0건 = 통과 — 이건 **미래 방어선**이다.
            foreach (RequestSO r in world.Requests)
            {
                if (r == null || r.RewardCostAmount <= 0) continue;
                Assert.IsTrue(HasMeansCondition(r.RequesterConditions, r),
                    $"{r.name}: 유상 부탁은 의뢰인 성립 조건에 " +
                    $"{r.RewardCostSlot} ≥ {r.RewardCostAmount}를 걸어야 한다");
            }
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

        // ── T7: 솜씨 표기 — 효율이 화면에 도달하는가 (ADR-M20-1 표현 조항) ────

        [Test]
        public void M20_T7_SkillLabel_ShowsOnlyWhenItMatters()
        {
            // 중립은 침묵한다 — 전원에게 늘 붙는 라벨은 정보가 아니라 소음이다.
            Assert.IsNull(SeasonHud.ComposeSkill(1f), "무직·비전문은 표기 없음");
            Assert.IsNull(SeasonHud.ComposeSkill(0f), "0 이하는 방어적으로 표기 없음");

            // 전문가는 배수로 읽힌다 (배율 0.5 = 소요 시간 절반 = 두 배 빨리).
            // ⚠️ 배율을 그대로 노출하면 안 된다 — 내부값 노출 금지 규율.
            string fast = SeasonHud.ComposeSkill(0.5f);
            StringAssert.Contains("두 배", fast);
            StringAssert.Contains("빨리", fast);
            StringAssert.DoesNotContain("0.5", fast, "배율(내부값)은 화면에 나오지 않는다");

            // 느린 쪽도 말한다 — 지금은 그런 직업이 없지만(중립 불변식) 생기면 화면이 먼저 안다.
            StringAssert.Contains("느리게", SeasonHud.ComposeSkill(2f));
        }

        // ── T8: 경험 우회 — 경험이 기질 페널티를 지운다 (M20-W11, M12-G 이전) ──

        [Test]
        public void M20_T8_ExperienceOverride_ErasesOnlyNegativeBias()
        {
            // "굶어 죽을 뻔했으면 기질을 넘어 집을 원한다" — 舊 집 부탁의 TraitBypassConditions
            // (M12_T13에서 검증하던 것)를 자가 건축의 우선순위 세계로 이전한 것.
            var wants = ScriptableObject.CreateInstance<GoalSO>(); // 집 (기질이 말리는 goal)
            wants.name = "T8_Wants";
            wants.Priority = 30;
            wants.ExperienceOverrideWhen = new[]
            {
                new SlotCondition { Slot = SlotId.MyWasStarved, Op = CompareOp.GreaterOrEqual, Value = 1 }
            };
            var rival = ScriptableObject.CreateInstance<GoalSO>(); // 경쟁 goal (중립)
            rival.name = "T8_Rival";
            rival.Priority = 25;

            var selector = new GoalSelector(new[] { wants, rival });
            int Bias(GoalSO g) => g == wants ? -10 : 0; // 게으름 페널티 (진폭 안 값)

            var slots = new int[PlanningConfig.TotalSlots];
            var normal = new WorldSnapshot(slots);
            Assert.AreSame(rival, selector.Select(normal, bias: Bias),
                "경험 없음 = 페널티 그대로 (30-10=20 < 25) — 기질이 이긴다");

            slots[(int)SlotId.MyWasStarved] = 1;
            var starved = new WorldSnapshot(slots);
            Assert.AreSame(wants, selector.Select(starved, bias: Bias),
                "굶어 죽을 뻔한 경험 = 페널티 소거 (30 > 25) — 경험이 기질을 넘는다");

            // 양수 보너스는 건드리지 않는다 — 우회는 발목을 지우는 것이지 열정을 더하는 게 아니다
            // (실효 상한 불변 = 명령 대역 60 불침범).
            int Bonus(GoalSO g) => g == wants ? 10 : 0;
            Assert.AreSame(wants, selector.Select(starved, bias: Bonus), "양수는 그대로 40");

            // 조건이 빈 goal은 페널티가 그대로다 (중립 불변식 — 우회는 명시한 goal에만)
            wants.ExperienceOverrideWhen = null;
            Assert.AreSame(rival, selector.Select(starved, bias: Bias),
                "우회 미명시 goal은 경험이 있어도 현행과 동일");

            Object.DestroyImmediate(wants);
            Object.DestroyImmediate(rival);
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
