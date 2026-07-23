using AIVillage.M0;
using NUnit.Framework;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M11-I 간호 2단계 게이트 (M11-T8) — 부상 계단 v2와 직업 게이트.
    /// 핵심 불변식 = **일반 간호는 1회성이다** (교대 간호로 영원히 살리면 crowding 재발 +
    /// Medic 무가치, ⚠️①). 치료(Medic)만 회복을 올린다 (결정 15).
    /// </summary>
    public class M11_CareGates
    {
        private const float GRACE = 1f;   // StabilizeGraceDays 제안치
        private const float DT = 0.1f;

        private static (float rec, float neg, bool stab, float grace) Step(
            float rec, float neg, bool stab, float grace, bool healer, bool helper, float mult = 1f)
            => VillagerAgent.NextInjuryStateV2(rec, neg, stab, grace, healer, helper, mult, GRACE, DT);

        // ── 치료(healer) = 회복 진행 ─────────────────────────────────────────────

        [Test]
        public void M11_T8_Healer_ProgressesRecovery()
        {
            var s = Step(0.2f, 0.4f, false, 0f, healer: true, helper: false, mult: 3f);
            Assert.AreEqual(0.5f, s.rec, 1e-4f, "회복 = rec + dt*mult (3배)");
            Assert.AreEqual(0.4f, s.neg, 1e-4f, "치료 중 방치 정지");
            Assert.IsFalse(s.stab, "치료는 안정화 플래그를 세우지 않는다 (별도 축)");
        }

        // ── 일반 간호(helper) = 1회성 안정화 ─────────────────────────────────────

        [Test]
        public void M11_T8_Helper_StabilizesOnceThenNoEffect()
        {
            // 최초 1회: 미안정 → 안정화 + 유예 부여 (회복 없음)
            var s = Step(0f, 0.3f, false, 0f, healer: false, helper: true);
            Assert.IsTrue(s.stab, "최초 일반 간호 = 안정화");
            Assert.AreEqual(GRACE, s.grace, 1e-4f, "유예 = graceDays 부여");
            Assert.AreEqual(0f, s.rec, "안정화는 회복을 올리지 않는다");
            Assert.AreEqual(0.3f, s.neg, 1e-4f, "방치 누적은 안정화 순간엔 정지");

            // 두 번째 일반 간호: 이미 안정화 → 무효과 (교대 간호로 살리지 못한다 = crowding 해소)
            var s2 = Step(0f, 0.3f, true, GRACE, healer: false, helper: true);
            Assert.AreEqual(GRACE - DT, s2.grace, 1e-4f, "재간호는 유예를 갱신하지 않고 유예가 소모된다");
            Assert.AreEqual(0f, s2.rec, "일반 재간호는 회복 없음");
        }

        // ── 유예 소모 → 소진 후 악화 재개 ────────────────────────────────────────

        [Test]
        public void M11_T8_Grace_DrainsThenNeglectResumes()
        {
            // 유예가 남아 있으면 방치 정지 + 유예만 감소
            var s = Step(0f, 0.5f, true, 0.05f, healer: false, helper: false);
            Assert.AreEqual(0.5f, s.neg, 1e-4f, "유예 중 방치 정지");
            Assert.AreEqual(0f, s.grace, 1e-4f, "유예 소진(0.05 - 0.1 → 음수는 다음 분기가 처리)");

            // 유예 0 + 간호 없음 = 악화 재개 (기존 방치와 동일 누적)
            var s2 = Step(0f, 0.5f, true, 0f, healer: false, helper: false);
            Assert.AreEqual(0.5f + DT, s2.neg, 1e-4f, "유예 소진 후 방치 누적 재개");
            Assert.IsTrue(s2.stab, "안정화 플래그는 유지 (다시 안정화되지 않는다)");
        }

        [Test]
        public void M11_T8_Unstabilized_NoTend_AccumulatesLikeM10()
        {
            // 안정화 전 + 간호 없음 = 즉시 방치 누적 (M10 동작과 동일 — 초기 사망 경로 보존)
            var s = Step(0f, 0f, false, 0f, healer: false, helper: false);
            Assert.AreEqual(DT, s.neg, 1e-4f, "미안정 부상자는 간호 없으면 곧바로 악화");
        }

        [Test]
        public void M11_T8_DeathTimeline_StabilizedNoHealerStillDies()
        {
            // 안정화됐지만 치료사가 안 오면: 유예(1.0) 소진 후 방치가 사망 문턱(1.5)까지 → 결국 사망
            var cfg = ScriptableObject.CreateInstance<AgentConfigSO>();
            float rec = 0f, neg = 0f, grace = GRACE; bool stab = true;
            int steps = 0;
            while (!VillagerAgent.ShouldDie(neg, cfg) && steps < 1000)
            {
                (rec, neg, stab, grace) = Step(rec, neg, stab, grace, healer: false, helper: false);
                steps++;
            }
            Assert.Less(steps, 1000, "치료사 없으면 반드시 사망한다 (무한 생존 불가 — S3 착탄)");
            // 유예 1.0 + 사망 문턱 1.5 = 약 2.5게임일 = 25스텝(dt 0.1)
            Assert.AreEqual(25, steps, 1, "≈2.5일 (유예 1.0 + 문턱 1.5)");
        }

        // ── 직업 게이트 (BlockedByJob) ───────────────────────────────────────────

        [Test]
        public void M11_T8_BlockedByJob_NullRequiredIsNeutral()
        {
            var goal = ScriptableObject.CreateInstance<GoalSO>(); // RequiredJob null
            var medic = ScriptableObject.CreateInstance<JobSO>();
            Assert.IsFalse(VillagerAgent.BlockedByJob(goal, medic), "RequiredJob 없음 = 누구나 통과 (중립)");
            Assert.IsFalse(VillagerAgent.BlockedByJob(goal, null), "무직도 통과");
        }

        [Test]
        public void M11_T8_BlockedByJob_GatesToMatchingJob()
        {
            var medic = ScriptableObject.CreateInstance<JobSO>();
            var farmer = ScriptableObject.CreateInstance<JobSO>();
            var treatGoal = ScriptableObject.CreateInstance<GoalSO>();
            treatGoal.RequiredJob = medic;

            Assert.IsFalse(VillagerAgent.BlockedByJob(treatGoal, medic), "치료사는 치료 goal 통과");
            Assert.IsTrue(VillagerAgent.BlockedByJob(treatGoal, farmer), "농부는 치료 goal 제외");
            Assert.IsTrue(VillagerAgent.BlockedByJob(treatGoal, null), "무직도 제외 (자가 건축·치료 게이트)");
        }
    }
}
