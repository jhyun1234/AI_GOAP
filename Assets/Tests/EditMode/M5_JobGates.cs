using AIVillage.M0;
using NUnit.Framework;
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
    }
}
