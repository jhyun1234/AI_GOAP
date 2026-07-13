using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// GoalSO 우선순위 선택기.
    ///
    /// 핵심 규칙 두 가지:
    ///   1. TriggerConditions 전부 만족해야 후보 (비어 있으면 항상 후보)
    ///   2. GoalConditions가 이미 전부 만족이면 스킵 —
    ///      舊 "BuildStructure NoSolutionFound 루프"(완료된 목표 재선택)의 구조적 방지
    /// </summary>
    public sealed class GoalSelector
    {
        private readonly GoalSO[] _goals; // Priority 내림차순 정렬본

        public GoalSelector(IEnumerable<GoalSO> goals)
        {
            var list = new List<GoalSO>();
            if (goals != null)
                foreach (GoalSO g in goals)
                    if (g != null) list.Add(g);

            list.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            _goals = list.ToArray();

            if (_goals.Length == 0)
                Debug.LogWarning("[GoalSelector] 등록된 goal이 없습니다 — 주민이 항상 Idle 상태가 됩니다.");
        }

        /// <summary>
        /// 현재 스냅샷에서 수행할 goal을 반환한다. 할 일이 없으면 null (정상 Idle).
        /// skip: 후보 제외 판정 (에이전트의 실패 쿨다운 등) — 상위가 막히면 하위로 내려간다.
        /// extra: 사다리에 합류하는 개인 goal (촌장 명령, ADR-M1-1) — Priority 위치에 끼워 평가.
        /// </summary>
        public GoalSO Select(WorldSnapshot snap, System.Func<GoalSO, bool> skip = null, GoalSO extra = null)
        {
            if (!snap.IsValid) return null;

            foreach (GoalSO goal in _goals)
            {
                if (extra != null && extra.Priority > goal.Priority)
                {
                    if (Passes(extra, snap, skip)) return extra;
                    extra = null; // 탈락한 extra는 재평가하지 않음
                }
                if (Passes(goal, snap, skip)) return goal;
            }
            return extra != null && Passes(extra, snap, skip) ? extra : null;
        }

        private static bool Passes(GoalSO goal, WorldSnapshot snap, System.Func<GoalSO, bool> skip)
        {
            if (skip != null && skip(goal)) return false;                  // 쿨다운 등 제외
            if (!AllHold(goal.TriggerConditions, snap)) return false;      // 미발동
            if (goal.GoalConditions != null && goal.GoalConditions.Length > 0
                && AllHold(goal.GoalConditions, snap)) return false;       // 이미 달성 → 스킵
            return true;
        }

        /// <summary>조건 배열 전체 만족 여부. null/빈 배열은 true (조건 없음 = 항상 성립).</summary>
        public static bool AllHold(SlotCondition[] conditions, WorldSnapshot snap)
        {
            if (conditions == null) return true;
            foreach (SlotCondition c in conditions)
            {
                int v = snap.Get(c.Slot);
                bool hold;
                switch (c.Op)
                {
                    case CompareOp.Equal:          hold = v == c.Value; break;
                    case CompareOp.GreaterOrEqual: hold = v >= c.Value; break;
                    case CompareOp.LessOrEqual:    hold = v <= c.Value; break;
                    default:                       hold = false; break;
                }
                if (!hold) return false;
            }
            return true;
        }
    }
}
