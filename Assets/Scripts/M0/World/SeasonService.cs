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
        private readonly float _prologueDays; // 첫 사이클 유예 (0 = 중립 = 기존 동작)
        private int _lastIndex = -1;

        /// <summary>계절 서수 (M9-C) — 사이클 누적 인덱스 (cyclesElapsed × 계절수 + 현재 인덱스).
        /// 같은 계절이라도 사이클마다 값이 달라 재해 "계절당 1회 발동" 판정·희생 시드의 유일 키가 된다.</summary>
        public int SeasonOrdinal { get; private set; }

        /// <summary>현재 계절. 첫 Tick 전이나 비활성 서비스면 null — 소비처는 null = 중립(배율 1) 처리.</summary>
        public SeasonSO Current { get; private set; }

        /// <summary>다음 위기 계절 시작까지 남은 게임일. 위기 진행 중 0, 위기 없으면 NO_CRISIS.</summary>
        public float DaysToCrisis { get; private set; } = NO_CRISIS;

        /// <summary>다음 채집 봉쇄(ForageFrozen) 계절 시작까지 남은 게임일 (M14 — ADR-M14-2).
        /// DaysToCrisis와 다르다: 비봉쇄 위기(여름) 앞·중에도 0이 되지 않는다.
        /// 봉쇄 진행 중 0, 사이클에 봉쇄가 없으면 NO_CRISIS.</summary>
        public float DaysToFreeze { get; private set; } = NO_CRISIS;

        /// <summary>다가오는(봉쇄 중이면 현재) 봉쇄 계절 — NextCrisis의 봉쇄판 (M14).
        /// 겨울 경보가 "겨울 길이"를 이 에셋의 DurationDays에서 읽는다 (상수 하드코딩 금지).</summary>
        public SeasonSO NextFreeze { get; private set; }

        /// <summary>현재 계절의 잔여 게임일 — HUD "겨울 (남은 N일)" 표시용 (M6-C).</summary>
        public float DaysLeftInSeason { get; private set; }

        /// <summary>연차 (M14-W4) — 사이클 순환 수 + 1. 달력 "N년째" 표기의 출처 (기록 경주의
        /// 자를 살아 있는 동안에도 보이게 — 전멸 화면에만 있으면 아무도 못 본다).</summary>
        public int Year => _cycle.Length > 0 ? SeasonOrdinal / _cycle.Length + 1 : 1;

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

        /// <param name="prologueDays">첫 겨울까지의 유예 (게임일). 계절 시계 자체를 이만큼 늦춰
        /// **첫 사이클의 준비 기간만** 늘린다 — 이후 사이클 간격은 그대로다. **기본 0 = 미사용.**
        /// 겨울은 목수가 있으면 집 곳간(15)으로 넘기고 없으면 무너지는 것이 의도된 구조다
        /// (2026-07-24 사용자 확인 — 목수 없는 판의 전멸은 결함이 아니라 시나리오). 이 손잡이는
        /// 계절 길이·인원 등 구조가 바뀔 때만 검토하고, 죽음을 막는 용도로 켜지 않는다.</param>
        public SeasonService(SeasonSO[] cycle, float prologueDays = 0f)
        {
            _prologueDays = Mathf.Max(0f, prologueDays);
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

            // 첫 사이클 유예 — 계절 시계를 통째로 늦춘다. 유예 중엔 시계가 0에 머물러 첫 계절이
            // 그만큼 길어지고, 이후 사이클 간격은 영향 없다 (Compute는 순수 유지 — 게이트 M6-T1).
            float seasonTime = Mathf.Max(0f, gameTime - _prologueDays);
            float graceLeft = Mathf.Max(0f, _prologueDays - gameTime); // 유예 잔여 (예고·HUD 보정)

            Compute(_cycle, seasonTime, out int index, out float days, out float left);
            // 유예분을 더해 카운트다운을 정직하게 — 유예 중 "위기까지"가 멈춰 보이면 안 된다.
            DaysToCrisis = days >= NO_CRISIS ? NO_CRISIS : days + graceLeft;
            DaysLeftInSeason = left + graceLeft;
            // 봉쇄 카운트다운 (M14) — 유예 보정은 DaysToCrisis와 동일 규약.
            float freezeDays = ComputeDaysToFreeze(_cycle, seasonTime);
            DaysToFreeze = freezeDays >= NO_CRISIS ? NO_CRISIS : freezeDays + graceLeft;
            // 서수 = 누적 사이클 × 계절수 + 현재 인덱스 (M9-C). gameTime 단조 증가라 단조 증가.
            int cyclesElapsed = _totalDays > 0f ? Mathf.FloorToInt(seasonTime / _totalDays) : 0;
            SeasonOrdinal = cyclesElapsed * _cycle.Length + index;
            if (index != _lastIndex)
            {
                _lastIndex = index;
                Current = _cycle[index];
                // 다가오는 위기 계절 (현재 포함 순방향 첫 IsCrisis) — 예고 대사·HUD 이름의 출처
                NextCrisis = null;
                for (int step = 0; step < _cycle.Length; step++)
                {
                    SeasonSO s = _cycle[(index + step) % _cycle.Length];
                    if (s.IsCrisis) { NextCrisis = s; break; }
                }
                // 다가오는 봉쇄 계절 (현재 포함 순방향 첫 ForageFrozen) — 겨울 경보 길이의 출처 (M14)
                NextFreeze = null;
                for (int step = 0; step < _cycle.Length; step++)
                {
                    SeasonSO s = _cycle[(index + step) % _cycle.Length];
                    if (s.ForageFrozen) { NextFreeze = s; break; }
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

        /// <summary>
        /// 순수 코어 (M14 — 게이트 M14-T1): 다음 채집 봉쇄 계절 시작까지 일수.
        /// Compute의 위상 계산을 그대로 쓰고 판정 술어만 IsCrisis → ForageFrozen이다
        /// (ADR-M14-2 "위기 ≠ 봉쇄" — 여름은 위기지만 봉쇄가 아니라서 DaysToCrisis로는
        /// 가을 준비 창을 표현할 수 없다). 봉쇄 진행 중 0, 사이클에 봉쇄가 없으면 NO_CRISIS.
        /// </summary>
        public static float ComputeDaysToFreeze(SeasonSO[] cycle, float gameTime)
        {
            if (cycle == null || cycle.Length == 0) return NO_CRISIS;

            Compute(cycle, gameTime, out int seasonIndex, out _, out float daysLeftInSeason);
            if (cycle[seasonIndex].ForageFrozen) return 0f;

            float dist = daysLeftInSeason;
            for (int step = 1; step <= cycle.Length; step++)
            {
                SeasonSO next = cycle[(seasonIndex + step) % cycle.Length];
                if (next.ForageFrozen) return dist;
                dist += next.DurationDays;
            }
            return NO_CRISIS;
        }
    }
}
