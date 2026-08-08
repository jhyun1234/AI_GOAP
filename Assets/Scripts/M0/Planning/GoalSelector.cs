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
        /// bias: 직업 실효 우선순위 보정 (ADR-M5-1) — 순위에만 개입, 발동 판정(Passes)에는 불개입.
        ///       null이면 기존과 완전 동일 (중립 불변식, M5-S3). 어디에도 저장하지 않는다.
        /// routine: 직업 일과 goal (ADR-M5-2) — extra와 같은 개인 주입, 씬 _goals에 넣지 않는다.
        /// request: 수락한 주민 부탁 goal (ADR-M8-4) — 같은 개인 주입. extra보다 뒤에 평가되므로
        ///          동률이면 촌장 명령 우선 (초과만 갱신 규칙이 보장).
        ///
        /// 전수 평가 (goal ~15개, O(n)). 동률은 먼저 평가된 후보 우선(초과만 갱신) —
        /// _goals가 Priority 내림차순 정렬본이라 기존 순회와 동일한 동률 해석이다.
        /// </summary>
        public GoalSO Select(WorldSnapshot snap, System.Func<GoalSO, bool> skip = null,
                             GoalSO extra = null, System.Func<GoalSO, int> bias = null,
                             GoalSO routine = null, GoalSO request = null)
        {
            if (!snap.IsValid) return null;

            GoalSO best = null;
            int bestP = int.MinValue;
            void Consider(GoalSO g, bool ordered = false)
            {
                if (g == null || !Passes(g, snap, skip, ordered)) return;
                // 명령은 보정 없이 원래 Priority (리뷰① 2026-08-08 — "명령의 무게는 촌장이 정하지
                // 기질이 깎지 않는다"). 겁 많은 주민의 「싸워라」(105)가 성향 보정으로 81까지 깎여
                // 피로(90)·배고픔(100)에 밀리는 것이 Play에서 관측됐다 — 수락한 명령이 기질에
                // 밀리면 소극적 거부가 된다. 거부는 수락 시점의 JudgeOrder(욕구 2축)가 전담한다
                // (ADR-M1-2 — 성향은 자율 goal 순위에서만 말한다).
                int b = ordered ? 0 : (bias != null ? bias(g) : 0);
                // 경험 우회 (M20-W11): 조건 성립 시 음수 보정(기질 페널티)만 지운다 — 경험 > 기질.
                // 양수는 불변이라 실효 상한이 안 오르고(명령 대역 60 불침범), 빈 배열 = 현행(중립).
                if (b < 0 && g.ExperienceOverrideWhen != null && g.ExperienceOverrideWhen.Length > 0
                    && AllHold(g.ExperienceOverrideWhen, snap))
                    b = 0;
                int p = g.Priority + b;
                if (p > bestP) { bestP = p; best = g; }
            }
            foreach (GoalSO goal in _goals) Consider(goal);
            Consider(extra, ordered: true); // 촌장 명령 — 동률이면 씬 goal 우선 (기존 해석 유지)
            Consider(routine); // 직업 일과 (M5-C에서 전달 시작)
            Consider(request); // 주민 부탁 (M8-D) — 동률이면 명령·일과 우선
            return best;
        }

        /// <param name="ordered">촌장 명령으로 주입된 goal인가 (M21-W4 B1). true이고 goal이
        /// OrderedIgnoresTrigger면 발동 조건을 우회한다 — 원정은 플레이어 개입의 값이다.
        /// 자율 후보(_goals·routine·request)에는 항상 false가 들어가므로 기존 동작 불변.</param>
        private bool Passes(GoalSO goal, WorldSnapshot snap, System.Func<GoalSO, bool> skip,
                            bool ordered = false)
        {
            if (IsFull(goal)) return false;                                // 정원 초과 (ADR-M3-4)
            if (skip != null && skip(goal)) return false;                  // 쿨다운 등 제외
            // 명령 원정 특례 (M21-W4 B1) — 이 goal이 명시적으로 허락했을 때만, 명령일 때만.
            bool bypassTrigger = ordered && goal.OrderedIgnoresTrigger;
            if (!bypassTrigger && !AllHold(goal.TriggerConditions, snap)) return false; // 미발동
            // 이미 달성 → 스킵. 단 상대 goal(RelativeToCurrent)은 면제 (ADR-M9-12): 원본
            // GoalConditions는 증분값(+2)이라 AllHold가 절대값으로 오독하면 대부분 "이미 달성"
            // 스킵돼 펌프가 영영 안 돈다. 발동 판정은 트리거가 전담한다.
            if (!goal.RelativeToCurrent
                && goal.GoalConditions != null && goal.GoalConditions.Length > 0
                && AllHold(goal.GoalConditions, snap)) return false;
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
                // M18 — 우변 선택: 슬롯 비교(트리거 전용, ADR-M18-1)면 RightSlot 현재값, 아니면 상수.
                int rhs = c.CompareToSlot ? snap.Get(c.RightSlot) : c.Value;
                bool hold;
                switch (c.Op)
                {
                    case CompareOp.Equal:          hold = v == rhs; break;
                    case CompareOp.GreaterOrEqual: hold = v >= rhs; break;
                    case CompareOp.LessOrEqual:    hold = v <= rhs; break;
                    case CompareOp.Less:           hold = v <  rhs; break; // M18 — 경계 겹침 방지
                    case CompareOp.Greater:        hold = v >  rhs; break; // M18
                    default:                       hold = false; break;
                }
                if (!hold) return false;
            }
            return true;
        }
    }
}
