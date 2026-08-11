using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

using static AIVillage.Tests.EditMode.GateHelpers;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M7 상호대화 게이트 (명세 M7-C). 이 파일이 M7-T1~T3의 집이다 —
    /// T1(성립 판정 순수 함수)·T2(쿨다운)를 시작으로 T3(에셋 정책, M7-D)이 뒤에 추가된다.
    /// </summary>
    public class M7_ChatterGates
    {
        // MakeGoal·DestroyAll → GateHelpers (2026-08-11 2차 감사 통합)

        private static ChatterSO MakeChatter(GoalSO speakerGoal = null,
                                             int minSpeaker = 8, int maxTarget = 2)
        {
            var c = ScriptableObject.CreateInstance<ChatterSO>();
            c.DisplayName = "테스트 상황";
            c.SpeakerGoal = speakerGoal;
            c.MinSpeakerGoalPriority = minSpeaker;
            c.MaxTargetGoalPriority = maxTarget;
            return c;
        }

        // ── M7-T1: Matches 순수 판정 (ADR-M7-2 — 원본 Priority 대역만) ─────────

        [Test]
        public void M7_T1_Matches_SpeakerPriorityBoundary()
        {
            ChatterSO c = MakeChatter();
            GoalSO work8 = MakeGoal("공용 노동", 8);
            GoalSO leisure7 = MakeGoal("경계 아래", 7);

            Assert.IsTrue(Matches(c, work8, null), "화자 Priority 8 = 하한 경계 — '일하는 중' 성립");
            Assert.IsFalse(Matches(c, leisure7, null), "화자 Priority 7 = 하한 미달 — 불성립");
            Assert.IsFalse(Matches(c, null, null), "화자 goal 없음 — '일하는 중'이 아니므로 불성립");

            DestroyAll(c, work8, leisure7);
        }

        [Test]
        public void M7_T1_Matches_TargetPriorityBoundary()
        {
            ChatterSO c = MakeChatter();
            GoalSO work8 = MakeGoal("공용 노동", 8);
            GoalSO idle2 = MakeGoal("여가", 2);
            GoalSO routine3 = MakeGoal("경계 위", 3);
            GoalSO p0 = MakeGoal("배고픔 해결", 90);

            Assert.IsTrue(Matches(c, work8, idle2), "상대 Priority 2 = 상한 경계 — '노는 중' 성립");
            Assert.IsTrue(Matches(c, work8, null), "상대 goal 없음(Idle) — '노는 중'에 포함");
            Assert.IsFalse(Matches(c, work8, routine3), "상대 Priority 3 = 상한 초과 — 불성립");
            Assert.IsFalse(Matches(c, work8, p0), "P0(90) 수행 중인 상대는 '노는 중'이 아니다");

            DestroyAll(c, work8, idle2, routine3, p0);
        }

        [Test]
        public void M7_T1_Matches_SpeakerGoalFilter()
        {
            GoalSO prep = MakeGoal("겨울 비축", 10);
            GoalSO build = MakeGoal("건설", 10);
            ChatterSO filtered = MakeChatter(speakerGoal: prep);

            Assert.IsTrue(Matches(filtered, prep, null), "SpeakerGoal 필터 일치 — 성립");
            Assert.IsFalse(Matches(filtered, build, null), "SpeakerGoal 필터 불일치 — 대역을 넘어도 불성립");

            DestroyAll(filtered, prep, build);
        }

        private static bool Matches(ChatterSO c, GoalSO speaker, GoalSO target)
            => ChatterService.Matches(c, speaker, target);

        // ── M7-T2: 개인 쿨다운 (스팸 0의 근거 — 화자·상대 공용) ────────────────

        [Test]
        public void M7_T2_Cooldown_BlocksBothThenExpires()
        {
            var world = ScriptableObject.CreateInstance<WorldConfigSO>();
            world.ChatterCooldownSec = 30f;
            var agentCfg = ScriptableObject.CreateInstance<AgentConfigSO>();
            var svc = new ChatterService(world, agentCfg);

            Assert.IsFalse(svc.IsCoolingDown("A", 100f), "발화 전 — 쿨다운 없음");

            svc.RecordChat("A", "B", 100f);
            Assert.IsTrue(svc.IsCoolingDown("A", 100f), "발화 직후 화자 재선정 불가");
            Assert.IsTrue(svc.IsCoolingDown("B", 100f), "발화 직후 상대도 재선정 불가 (공용 쿨다운)");
            Assert.IsTrue(svc.IsCoolingDown("A", 129.9f), "쿨다운 중 — 여전히 불가");
            Assert.IsFalse(svc.IsCoolingDown("C", 100f), "제3자는 영향 없음");

            Assert.IsFalse(svc.IsCoolingDown("A", 130f), "쿨다운 경과 — 재선정 가능");
            Assert.IsFalse(svc.IsCoolingDown("B", 130f), "상대도 경과 후 가능");

            DestroyAll(world, agentCfg);
        }

        // ── M7-T3: 에셋 정책 (M7-D — 대화 에셋 2종의 데이터 완결성) ────────────

        private static ChatterSO LoadChatter(string name)
        {
            var c = AssetDatabase.LoadAssetAtPath<ChatterSO>($"Assets/M0Config/Chatters/{name}.asset");
            Assert.IsNotNull(c, $"{name}.asset 로드 실패");
            return c;
        }

        [Test]
        public void M7_T3_AssetPolicy_BothChattersComplete()
        {
            foreach (string name in new[] { "Chatter_WorkVsIdle", "Chatter_PrepVsIdle" })
            {
                ChatterSO c = LoadChatter(name);
                Assert.GreaterOrEqual(c.SpeakLines.Length, 3, $"{name}: SpeakLines ≥ 3");
                Assert.GreaterOrEqual(c.DefaultReplyLines.Length, 1, $"{name}: 기본 응수 필수 (빈칸 폴백처)");

                int mapped = 0;
                foreach (ChatterSO.PersonalityReply r in c.PersonalityReplies)
                    if (r.Personality != null && r.Lines != null && r.Lines.Length > 0) mapped++;
                Assert.GreaterOrEqual(mapped, 3, $"{name}: 성격별 응수 ≥ 3성격 (M7-S4의 근거)");
            }
        }

        [Test]
        public void M7_T3_AssetPolicy_PrepVsIdleFiltersWinterPrep()
        {
            ChatterSO prep = LoadChatter("Chatter_PrepVsIdle");
            var winterPrep = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_WinterPrep.asset");
            Assert.IsNotNull(winterPrep, "Goal_WinterPrep.asset 로드 실패");
            Assert.AreSame(winterPrep, prep.SpeakerGoal, "비축 잔소리는 겨울 비축 goal 수행 중에만");
        }
    }
}
