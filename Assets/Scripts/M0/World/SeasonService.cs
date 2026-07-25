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
        private readonly float _totalDays;  // 사이클 총 게임일 (서수 계산용 캐시)
        private int _lastIndex = -1;

        /// <summary>계절 서수 (M9-C) — 사이클 누적 인덱스 (cyclesElapsed × 계절수 + 현재 인덱스).
        /// 같은 계절이라도 사이클마다 값이 달라 재해 "계절당 1회 발동" 판정·희생 시드의 유일 키가 된다.</summary>
        public int SeasonOrdinal { get; private set; }

        /// <summary>현재 계절. 첫 Tick 전이나 비활성 서비스면 null — 소비처는 null = 중립(배율 1) 처리.</summary>
        public SeasonSO Current { get; private set; }

        /// <summary>다음 위기 계절 시작까지 남은 게임일. 위기 진행 중 0, 위기 없으면 NO_CRISIS.</summary>
        public float DaysToCrisis { get; private set; } = NO_CRISIS;

        /// <summary>현재 계절의 잔여 게임일 — HUD "겨울 (남은 N일)" 표시용 (M6-C).</summary>
        public float DaysLeftInSeason { get; private set; }

        /// <summary>다가오는(위기 중이면 현재) 위기 계절 — 예고 대사·HUD 이름의 출처. 사이클에 없으면 null.</summary>
        public SeasonSO NextCrisis { get; private set; }

        /// <summary>사이클이 비어 있으면 false — SimulationLoop가 서비스 자체를 null로 둔다.</summary>
        public bool IsActive => _cycle.Length > 0;

        // 소비처용 null-안전 배율 (M6-B) — 첫 Tick 전이나 비활성이면 중립 1.
        // 곱 지점은 호출부(SimulationLoop 재생·성장, VillagerAgent 포만 감쇠)다 —
        // 서비스(Discovery/Farm)는 계절을 몰라야 한다 (명세 M6-B ⚠️①).
        public float RegenMult        => Current != null ? Current.RegenMult        : 1f;
        public float GrowthMult       => Current != null ? Current.GrowthMult       : 1f;
        public float SatietyDecayMult => Current != null ? Current.SatietyDecayMult : 1f;

        /// <summary>이 계절 야생 채집 봉쇄 여부 (M6 겨울 위기 — ADR-M6-1 개정). Current 없으면 false(중립).
        /// WorldModel.BuildSnapshot이 이걸 읽어 NearDiscoveredFood를 0으로 만든다.</summary>
        public bool ForageFrozen      => Current != null && Current.ForageFrozen;

        /// <summary>계절 전환 시 1회 (첫 Tick 포함) — HUD·전환 로그 구독용.</summary>
        public event System.Action<SeasonSO> OnSeasonChanged;

        public SeasonService(SeasonSO[] cycle)
        {
            var list = new System.Collections.Generic.List<SeasonSO>();
            if (cycle != null)
                foreach (SeasonSO s in cycle)
                    if (s != null) list.Add(s);
            _cycle = list.ToArray();
            foreach (SeasonSO s in _cycle) _totalDays += s.DurationDays;
        }

        /// <summary>SimulationLoop 틱마다 호출 — 결과 캐시, 계절이 바뀌면 이벤트 1회.</summary>
        public void Tick(float gameTime)
        {
            if (_cycle.Length == 0) return; // 비활성 — 방어 (정상 경로는 서비스 미생성)

            Compute(_cycle, gameTime, out int index, out float days, out float left);
            DaysToCrisis = days;
            DaysLeftInSeason = left;
            // 서수 = 누적 사이클 × 계절수 + 현재 인덱스 (M9-C). gameTime 단조 증가라 단조 증가.
            int cyclesElapsed = _totalDays > 0f ? Mathf.FloorToInt(gameTime / _totalDays) : 0;
            SeasonOrdinal = cyclesElapsed * _cycle.Length + index;
            if (index != _lastIndex)
            {
                _lastIndex = index;
                Current = _cycle[index];
                // 다가오는 위기 계절 (현재 포함 순방향 첫 IsCrisis) — 예고 대사의 출처
                NextCrisis = null;
                for (int step = 0; step < _cycle.Length; step++)
                {
                    SeasonSO s = _cycle[(index + step) % _cycle.Length];
                    if (s.IsCrisis) { NextCrisis = s; break; }
                }
                OnSeasonChanged?.Invoke(Current);
            }
        }

        /// <summary>
        /// 순수 코어: 사이클 위상 → (현재 계절 인덱스, 다음 위기 시작까지 일수).
        /// 위기 계절 진행 중이면 0 (예고 goal 트리거가 겨울 내내 유지 — 명세 M6-A ⚠️②),
        /// 사이클에 위기가 없으면 NO_CRISIS.
        /// </summary>
        public static void Compute(SeasonSO[] cycle, float gameTime,
                                   out int seasonIndex, out float daysToCrisis,
                                   out float daysLeftInSeason)
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

            daysLeftInSeason = seasonStart + cycle[seasonIndex].DurationDays - phase;

            if (cycle[seasonIndex].IsCrisis)
            {
                daysToCrisis = 0f;
                return;
            }

            // 현재 계절 잔여 + 이후 비위기 계절들 (사이클 1바퀴 순방향 탐색)
            float dist = daysLeftInSeason;
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
