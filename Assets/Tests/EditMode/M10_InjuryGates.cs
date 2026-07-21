using AIVillage.M0;
using NUnit.Framework;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M10-A 부상 축 게이트 (M10-T1) — 최초의 사망 축.
    /// 판정은 순수 함수 4종(계단·문턱·진입 가드·goal 필터)뿐 — M6-T3 이탈 게이트와 동일 사상.
    /// 이탈=굶주림·사망=부상의 결말 이원화(ADR-M10-3)는 각 계단이 자기 원인만 읽는 것으로 보장된다.
    /// </summary>
    public class M10_InjuryGates
    {
        [Test]
        public void M10_T1_NextInjuryState_TendHoldsNeglectAndRecovers()
        {
            // 간호 중 = 회복 진행 + 방치 정지 (홀드)
            (float rec, float neg) = VillagerAgent.NextInjuryState(0.2f, 0.4f, tended: true, tendMult: 1f, deltaGameDays: 0.1f);
            Assert.AreEqual(0.3f, rec, 1e-5f, "간호 중 회복 누적");
            Assert.AreEqual(0.4f, neg, 1e-5f, "간호 중 방치 정지 (홀드 — 리셋 아님)");

            // Medic 배율 — 회복만 가속 (방치와 무관)
            (rec, neg) = VillagerAgent.NextInjuryState(0f, 0f, tended: true, tendMult: 3f, deltaGameDays: 0.1f);
            Assert.AreEqual(0.3f, rec, 1e-5f, "치료사 배율 3 = 3배 회복");

            // 방치 = 방치 누적 + 회복 정지 (자연 회복 없음 — 결정 11)
            (rec, neg) = VillagerAgent.NextInjuryState(0.2f, 0.4f, tended: false, tendMult: 3f, deltaGameDays: 0.1f);
            Assert.AreEqual(0.2f, rec, 1e-5f, "방치 중 회복 정지 — 자연 회복 없음");
            Assert.AreEqual(0.5f, neg, 1e-5f, "방치 누적");
        }

        [Test]
        public void M10_T1_NextInjuryState_TendedNeglectIsHoldNotReset()
        {
            // 스치는 간호가 사망 시계를 초기화하면 사망이 사실상 불가능해진다 (명세 M10-A ⚠️②)
            (_, float neg) = VillagerAgent.NextInjuryState(0f, 1.4f, tended: true, tendMult: 1f, deltaGameDays: 0.1f);
            Assert.AreEqual(1.4f, neg, 1e-5f, "간호는 홀드다 — 누적 1.4일이 0으로 돌아가지 않는다");
        }

        [Test]
        public void M10_T1_ShouldDie_ThresholdBoundary()
        {
            var cfg = ScriptableObject.CreateInstance<AgentConfigSO>();
            cfg.InjuryDeathAfterDays = 1.5f;

            Assert.IsFalse(VillagerAgent.ShouldDie(0f, cfg), "방치 없음");
            Assert.IsFalse(VillagerAgent.ShouldDie(1.49f, cfg), "문턱 직전");
            Assert.IsTrue(VillagerAgent.ShouldDie(1.5f, cfg), "문턱 도달 = 사망");
            Assert.IsTrue(VillagerAgent.ShouldDie(3f, cfg), "초과분도 사망");

            Object.DestroyImmediate(cfg);
        }

        [Test]
        public void M10_T1_CanInjure_DuplicateAndDeadIgnored()
        {
            Assert.IsTrue(VillagerAgent.CanInjure(AgentState.Idle, InjurySeverity.None), "정상 진입");
            Assert.IsTrue(VillagerAgent.CanInjure(AgentState.Acting, InjurySeverity.None), "작업 중도 진입 (플랜 중단은 Injure가 수행)");
            Assert.IsFalse(VillagerAgent.CanInjure(AgentState.Idle, InjurySeverity.Light), "중복 부상 무시 (M10 단일 심각도)");
            Assert.IsFalse(VillagerAgent.CanInjure(AgentState.Dead, InjurySeverity.None), "Dead 무시");
        }

        [Test]
        public void M10_T1_BlockedByInjury_FilterAndNeutralInvariant()
        {
            GoalSO labor = ScriptableObject.CreateInstance<GoalSO>();    // AllowedWhenInjured 기본 false
            GoalSO survival = ScriptableObject.CreateInstance<GoalSO>();
            survival.AllowedWhenInjured = true;

            // 부상 중 — 노동 차단, 생존 잔존
            Assert.IsTrue(VillagerAgent.BlockedByInjury(labor, InjurySeverity.Light), "부상 중 노동 goal 제외");
            Assert.IsFalse(VillagerAgent.BlockedByInjury(survival, InjurySeverity.Light), "생존 goal 잔존 (절뚝이며 밥 먹는 게 설계)");

            // 중립 불변식 — 부상 None이면 어떤 goal도 막지 않는다 (기존 Select와 완전 동일)
            Assert.IsFalse(VillagerAgent.BlockedByInjury(labor, InjurySeverity.None), "무부상 = 필터 불개입");
            Assert.IsFalse(VillagerAgent.BlockedByInjury(survival, InjurySeverity.None), "무부상 = 필터 불개입");

            Object.DestroyImmediate(labor);
            Object.DestroyImmediate(survival);
        }
    }
}
