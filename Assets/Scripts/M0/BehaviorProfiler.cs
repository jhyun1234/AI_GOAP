using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 성격별 행동 프로파일 계측 (M12-J, 새 선반 ③).
    ///
    /// 왜 필요한가: 성격 6 × 직업 7 = 42조합인데 한 판의 주민은 4~10명이고 매판 랜덤이라,
    /// 특정 조합(예: 떠돌이 목수)을 눈으로 관측하려면 여러 판을 돌려야 한다
    /// (2026-07-26 사용자: "매번 달라지니깐 찾기가 힘들어"). 이 계측기는 **한 번의 방치로
    /// 그 판에 나온 성격 전부를 동시에** 숫자로 보여준다.
    ///
    /// 성공 기준 S4("성격별 프로파일이 갈린다")의 판정기이기도 하다 — 상위 3 goal 구성이
    /// 서로 다른 성격 쌍의 수를 함께 찍는다.
    ///
    /// ⚠️ **읽기 전용이다.** 시뮬레이션 상태를 쓰지 않으며 세이브 대상이 아니다
    /// (ADR-M0-10 — 로드 후 재계산되는 관측값). 계측이 게임을 바꾸면 관측이 아니다.
    /// </summary>
    public sealed class BehaviorProfiler
    {
        /// <summary>성격 하나의 누적 기록. goal 선택은 이름 키로 센다(에셋 참조 수명과 무관하게).</summary>
        private sealed class Entry
        {
            public readonly Dictionary<string, int> GoalPicks = new Dictionary<string, int>(16);
            public int TotalPicks;
            public int LaborPicks;   // 노동 계열 goal 선택 수 (근면 축의 관측 신호)
            public int Refusals;     // 명령·부탁 거부 수 (자존 축의 관측 신호)
            public int Members;      // 이 성격을 가진 주민 수 (스냅샷마다 갱신)
            public int Alive;
            public int WithHome;
        }

        private readonly Dictionary<string, Entry> _byPersonality = new Dictionary<string, Entry>(8);
        private float _nextLogDay;

        /// <summary>성격 미배선 주민도 한 칸으로 모은다 — 빠뜨리면 합계가 안 맞아 해석이 틀어진다.</summary>
        private const string NO_PERSONALITY = "(성격없음)";

        private static string KeyOf(PersonalitySO p)
            => p == null ? NO_PERSONALITY
             : (string.IsNullOrEmpty(p.DisplayName) ? p.name : p.DisplayName);

        private Entry EntryOf(PersonalitySO p)
        {
            string key = KeyOf(p);
            if (!_byPersonality.TryGetValue(key, out Entry e))
            {
                e = new Entry();
                _byPersonality[key] = e;
            }
            return e;
        }

        /// <summary>goal 선택 1회 기록 (VillagerAgent가 실제로 착수한 goal).</summary>
        public void RecordGoalPick(PersonalitySO p, GoalSO goal, bool isLabor)
        {
            if (goal == null) return;
            Entry e = EntryOf(p);
            e.GoalPicks.TryGetValue(goal.name, out int n);
            e.GoalPicks[goal.name] = n + 1;
            e.TotalPicks++;
            if (isLabor) e.LaborPicks++;
        }

        /// <summary>명령·부탁 거부 1회 기록.</summary>
        public void RecordRefusal(PersonalitySO p) => EntryOf(p).Refusals++;

        /// <summary>
        /// 주기 판정 (순수 — 게이트 대상). 간격 ≤ 0이면 계측 끄기(중립).
        /// 계절 경계(보통 8일 배수)와 어긋나게 잡아야 특정 계절만 찍히는 편향이 안 생긴다.
        /// </summary>
        public static bool ShouldLog(float gameDay, float nextLogDay, float intervalDays)
            => intervalDays > 0f && gameDay >= nextLogDay;

        /// <summary>다음 로그 시점 (순수 — 밀린 경우에도 현재 시점 기준으로 다시 잡는다).</summary>
        public static float AdvanceLogDay(float gameDay, float intervalDays)
            => gameDay + intervalDays;

        /// <summary>
        /// S4 판정 (순수 — 게이트 대상): 상위 N개 goal 구성이 **서로 다른** 성격 쌍의 수.
        /// 순서가 아니라 집합을 비교한다 — "무엇을 주로 하는가"가 갈리면 성격이 보이는 것이지
        /// 1·2위가 뒤바뀐 정도는 노이즈다.
        /// </summary>
        public static int DivergentPairs(IReadOnlyList<HashSet<string>> topGoalsPerPersonality)
        {
            if (topGoalsPerPersonality == null || topGoalsPerPersonality.Count < 2) return 0;
            int pairs = 0;
            for (int i = 0; i < topGoalsPerPersonality.Count; i++)
                for (int j = i + 1; j < topGoalsPerPersonality.Count; j++)
                    if (!topGoalsPerPersonality[i].SetEquals(topGoalsPerPersonality[j])) pairs++;
            return pairs;
        }

        /// <summary>상위 N개 goal 이름 집합 (순수 — 동률은 이름 순으로 결정적 절단).</summary>
        public static HashSet<string> TopGoals(Dictionary<string, int> picks, int n)
        {
            var result = new HashSet<string>();
            if (picks == null || picks.Count == 0) return result;

            var names = new List<string>(picks.Keys);
            names.Sort((a, b) =>
            {
                int c = picks[b].CompareTo(picks[a]);          // 많이 고른 순
                return c != 0 ? c : string.CompareOrdinal(a, b); // 동률은 이름 순 (결정적)
            });
            for (int i = 0; i < names.Count && i < n; i++) result.Add(names[i]);
            return result;
        }

        /// <summary>
        /// 계측 틱 — 주기가 되면 성격별 프로파일 1회 출력. 살아있는 주민 상태는 여기서만 읽는다.
        /// </summary>
        public void Tick(float gameDay, float intervalDays, IReadOnlyList<VillagerAgent> agents)
        {
            if (!ShouldLog(gameDay, _nextLogDay, intervalDays)) return;
            _nextLogDay = AdvanceLogDay(gameDay, intervalDays);

            foreach (KeyValuePair<string, Entry> kv in _byPersonality)
            {
                kv.Value.Members = 0; kv.Value.Alive = 0; kv.Value.WithHome = 0;
            }
            if (agents != null)
                foreach (VillagerAgent a in agents)
                {
                    if (a == null) continue;
                    Entry e = EntryOf(a.Personality);
                    e.Members++;
                    if (a.State != AgentState.Dead) e.Alive++;
                            if (a.TryGetHomeTile(out _)) e.WithHome++;
                }

            var tops = new List<HashSet<string>>();
            var sb = new System.Text.StringBuilder();
            sb.Append($"[Profiler] Day {Mathf.FloorToInt(gameDay)} — 성격별 행동 프로파일\n");
            foreach (KeyValuePair<string, Entry> kv in _byPersonality)
            {
                Entry e = kv.Value;
                HashSet<string> top = TopGoals(e.GoalPicks, 3);
                if (e.TotalPicks > 0) tops.Add(top);

                float laborPct = e.TotalPicks > 0 ? 100f * e.LaborPicks / e.TotalPicks : 0f;
                sb.Append($"  {kv.Key}: 생존 {e.Alive}/{e.Members} · 집 {e.WithHome} · " +
                          $"노동비율 {laborPct:F0}% · 거부 {e.Refusals} · " +
                          $"상위goal [{string.Join(", ", top)}]\n");
            }
            sb.Append($"  → S4 분화: 상위3 구성이 다른 성격 쌍 {DivergentPairs(tops)}개 " +
                      $"(성격 {tops.Count}종 중)");
            Debug.Log(sb.ToString());
        }
    }
}
