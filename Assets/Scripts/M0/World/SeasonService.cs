using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 계절 시계 (M6-A) — WorldConfig.SeasonCycle을 순환하며 현재 계절과 다음 위기까지의
    /// 일수를 캐시한다. 코어 계산은 순수 static(Compute) — EditMode 게이트(M6-T1)가 직접 검증.
    /// GameTime 자체에는 개입하지 않는다 (ADR-M6-1 — 겨울이 시간을 늦추면 안 된다).
    /// 게임 개입은 배율 3종을 소비처(SimulationLoop 재생·성장, VillagerAgent 포만 감쇠)가
    /// 읽어 곱하는 방식뿐 — 이 클래스는 아무것도 쓰지 않는다.
    /// </summary>
    public sealed class SeasonService
    {
        /// <summary>사이클에 위기 계절이 없을 때의 DaysToCrisis 관례값 — 슬롯 트리거 "≤N"이 전부 불발된다.</summary>
        public const float NO_CRISIS = 99f;

        private readonly SeasonSO[] _cycle; // null 제거 복사본
        private int _lastIndex = -1;

        /// <summary>현재 계절. 첫 Tick 전이나 비활성 서비스면 null — 소비처는 null = 중립(배율 1) 처리.</summary>
        public SeasonSO Current { get; private set; }

        /// <summary>다음 위기 계절 시작까지 남은 게임일. 위기 진행 중 0, 위기 없으면 NO_CRISIS.</summary>
        public float DaysToCrisis { get; private set; } = NO_CRISIS;

        /// <summary>사이클이 비어 있으면 false — SimulationLoop가 서비스 자체를 null로 둔다.</summary>
        public bool IsActive => _cycle.Length > 0;

        // 소비처용 null-안전 배율 (M6-B) — 첫 Tick 전이나 비활성이면 중립 1.
        // 곱 지점은 호출부(SimulationLoop 재생·성장, VillagerAgent 포만 감쇠)다 —
        // 서비스(Discovery/Farm)는 계절을 몰라야 한다 (명세 M6-B ⚠️①).
        public float RegenMult        => Current != null ? Current.RegenMult        : 1f;
        public float GrowthMult       => Current != null ? Current.GrowthMult       : 1f;
        public float SatietyDecayMult => Current != null ? Current.SatietyDecayMult : 1f;

        /// <summary>계절 전환 시 1회 (첫 Tick 포함) — HUD·전환 로그 구독용.</summary>
        public event System.Action<SeasonSO> OnSeasonChanged;

        public SeasonService(SeasonSO[] cycle)
        {
            var list = new System.Collections.Generic.List<SeasonSO>();
            if (cycle != null)
                foreach (SeasonSO s in cycle)
                    if (s != null) list.Add(s);
            _cycle = list.ToArray();
        }

        /// <summary>SimulationLoop 틱마다 호출 — 결과 캐시, 계절이 바뀌면 이벤트 1회.</summary>
        public void Tick(float gameTime)
        {
            if (_cycle.Length == 0) return; // 비활성 — 방어 (정상 경로는 서비스 미생성)

            Compute(_cycle, gameTime, out int index, out float days);
            DaysToCrisis = days;
            if (index != _lastIndex)
            {
                _lastIndex = index;
                Current = _cycle[index];
                OnSeasonChanged?.Invoke(Current);
            }
        }

        /// <summary>
        /// 순수 코어: 사이클 위상 → (현재 계절 인덱스, 다음 위기 시작까지 일수).
        /// 위기 계절 진행 중이면 0 (예고 goal 트리거가 겨울 내내 유지 — 명세 M6-A ⚠️②),
        /// 사이클에 위기가 없으면 NO_CRISIS.
        /// </summary>
        public static void Compute(SeasonSO[] cycle, float gameTime,
                                   out int seasonIndex, out float daysToCrisis)
        {
            float total = 0f;
            for (int i = 0; i < cycle.Length; i++) total += cycle[i].DurationDays;

            float phase = Mathf.Repeat(gameTime, total);

            // 현재 계절 탐색 — 부동소수 끝단(phase==total 근접)은 마지막 계절로 폴백
            seasonIndex = cycle.Length - 1;
            float seasonStart = total - cycle[cycle.Length - 1].DurationDays;
            float acc = 0f;
            for (int i = 0; i < cycle.Length; i++)
            {
                if (phase < acc + cycle[i].DurationDays)
                {
                    seasonIndex = i;
                    seasonStart = acc;
                    break;
                }
                acc += cycle[i].DurationDays;
            }

            if (cycle[seasonIndex].IsCrisis)
            {
                daysToCrisis = 0f;
                return;
            }

            // 현재 계절 잔여 + 이후 비위기 계절들 (사이클 1바퀴 순방향 탐색)
            float dist = seasonStart + cycle[seasonIndex].DurationDays - phase;
            for (int step = 1; step <= cycle.Length; step++)
            {
                SeasonSO next = cycle[(seasonIndex + step) % cycle.Length];
                if (next.IsCrisis)
                {
                    daysToCrisis = dist;
                    return;
                }
                dist += next.DurationDays;
            }
            daysToCrisis = NO_CRISIS;
        }
    }
}
