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

        // 작업 클레임 카운트 (ADR-M3-4) — 유일한 소유자는 이 클래스. 에이전트는 Claim/Release만 호출.
        // 에이전트 틱이 단일 스레드 순차라 Select→Claim 사이 경쟁이 없다.
        private readonly Dictionary<GoalSO, int> _claims = new Dictionary<GoalSO, int>();

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

        private bool Passes(GoalSO goal, WorldSnapshot snap, System.Func<GoalSO, bool> skip)
        {
            if (IsFull(goal)) return false;                                // 정원 초과 (ADR-M3-4)
            if (skip != null && skip(goal)) return false;                  // 쿨다운 등 제외
            if (!AllHold(goal.TriggerConditions, snap)) return false;      // 미발동
            if (goal.GoalConditions != null && goal.GoalConditions.Length > 0
                && AllHold(goal.GoalConditions, snap)) return false;       // 이미 달성 → 스킵
            return true;
        }

        /// <summary>goal 착수 선언 — 선택 직후 호출 (Planning 대기도 클레임 상태, ADR-M3-4).</summary>
        public void Claim(GoalSO goal)
        {
            if (goal == null) return;
            _claims.TryGetValue(goal, out int n);
            _claims[goal] = n + 1;
        }

        /// <summary>goal 내려놓기 — 완료·중단·전환 공통. 이중 해제는 0 클램프 (음수 잠김 방지).</summary>
        public void Release(GoalSO goal)
        {
            if (goal == null) return;
            if (_claims.TryGetValue(goal, out int n))
                _claims[goal] = Mathf.Max(0, n - 1);
        }

        /// <summary>동시 인원 가득 여부 — MaxWorkers 0은 무제한.</summary>
        public bool IsFull(GoalSO goal)
            => goal.MaxWorkers > 0 && _claims.TryGetValue(goal, out int n) && n >= goal.MaxWorkers;

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
