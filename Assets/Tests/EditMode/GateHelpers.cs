using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// 게이트 공용 헬퍼 (2026-08-11 2차 감사 — 파일마다 복붙되던 6종을 한곳으로).
    /// 판정 로직은 없다 — 스냅샷 생성·에셋 로드·SO 생성/정리의 손잡이만 모은다.
    /// 파일 고유 변형(M4 RunPlan(costMult)·M9 Config(wood)·M12 SnapWith·M21 SnapThreat·
    /// M1 MakeGoal(4인자)·M18 Snap(left,right))은 의미가 달라 각자 남는다 — 여기 넣지 말 것.
    /// 사용법: 파일 상단에 `using static AIVillage.Tests.EditMode.GateHelpers;` 한 줄.
    /// </summary>
    public static class GateHelpers
    {
        /// <summary>지정 슬롯만 세팅한 스냅샷 생성. 나머지는 0.</summary>
        public static WorldSnapshot Snap(params (SlotId slot, int value)[] pairs)
        {
            var slots = new int[PlanningConfig.TotalSlots];
            foreach ((SlotId slot, int value) in pairs) slots[(int)slot] = value;
            return new WorldSnapshot(slots);
        }

        /// <summary>초기 스톡만 지정한 임시 WorldConfig (테스트 수명 — 정리는 호출자 몫).</summary>
        public static WorldConfigSO Config(int wood, int raw)
        {
            var c = ScriptableObject.CreateInstance<WorldConfigSO>();
            c.InitialWoodStock = wood;
            c.InitialRawFoodStock = raw;
            return c;
        }

        /// <summary>배포 goal 에셋 로드 — 없으면 즉시 실패 (에셋 이름 오기입을 게이트가 잡는다).</summary>
        public static GoalSO LoadGoal(string name)
        {
            var goal = AssetDatabase.LoadAssetAtPath<GoalSO>($"Assets/M0Config/Goals/{name}.asset");
            Assert.IsNotNull(goal, $"Goal 에셋 없음: {name}");
            return goal;
        }

        /// <summary>플랜 요청→동기 완료→결과 회수 (게이트 표준 경로 — CompleteNow는 게이트 전용 훅).</summary>
        public static (PlanStatus status, ActionSO[] plan) RunPlan(
            PlannerGateway gw, WorldSnapshot snap, GoalSO goal)
        {
            PlannerGateway.PendingPlan pending = gw.RequestPlan(snap, goal);
            Assert.IsNotNull(pending, "RequestPlan이 null을 반환했습니다.");
            gw.CompleteNow(pending);
            bool done = gw.TryGetResult(pending, out PlanStatus status, out ActionSO[] plan, out _);
            Assert.IsTrue(done, "CompleteNow 이후에도 TryGetResult가 false입니다.");
            return (status, plan);
        }

        /// <summary>테스트가 만든 SO/GameObject 일괄 파괴 (SeasonSO[] 등 배열 통째도 받는다 — 공변).</summary>
        public static void DestroyAll(params Object[] objs)
        {
            foreach (Object o in objs) Object.DestroyImmediate(o);
        }

        /// <summary>이름·우선순위만 있는 임시 goal (M7/M8 대화·부탁 판정용).</summary>
        public static GoalSO MakeGoal(string name, int priority)
        {
            var g = ScriptableObject.CreateInstance<GoalSO>();
            g.name = name;
            g.DisplayName = name;
            g.Priority = priority;
            return g;
        }

        /// <summary>임시 계절 SO — 기본값(frozen=false, growthMult=1)은 SeasonSO 에셋 기본값과 동일.</summary>
        public static SeasonSO MakeSeason(string name, float days, bool crisis, bool frozen = false,
                                          float growthMult = 1f)
        {
            var s = ScriptableObject.CreateInstance<SeasonSO>();
            s.name = name;
            s.DisplayName = name;
            s.DurationDays = days;
            s.IsCrisis = crisis;
            s.ForageFrozen = frozen;
            s.GrowthMult = growthMult;
            return s;
        }
    }
}
