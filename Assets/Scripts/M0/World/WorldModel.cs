using System.Collections.Generic;
using AIVillage.Core;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 전역 수치 상태(스톡·완공 플래그)의 유일한 쓰기 지점 (ADR-M0-3).
    /// My*(포만감/피로)는 에이전트 개인 소유라 여기 없다 — 스냅샷 생성 시 인자로 합쳐진다.
    /// 완공 플래그 쓰기는 ConstructionService.Complete()만 호출한다 (그 외 호출 금지).
    /// </summary>
    public sealed class WorldModel
    {
        /// <summary>식량 일수 중립·상한 관례값 (M9-G) — SeasonService.NO_CRISIS와 같은 99.
        /// 인원 0·미배선이면 이 값 = "≤N" 트리거 전부 불발 (기존 동작).</summary>
        public const int NO_ESTIMATE = 99;

        private readonly int[] _slots = new int[SlotIds.Count];
        private readonly DiscoveryService _discovery;
        private readonly FarmService _farm;
        private readonly SeasonService _season;

        // 식량 수지 (M9-G) — 가치표는 생성 시 1회 파생·캐시 (틱마다 액션 스캔 금지).
        private readonly (SlotId slot, int gain)[] _foodValues;
        private readonly System.Func<int> _aliveCount; // 원천 = SimulationLoop._agents.Count (Dead 제외)
        private readonly float _decayPerDay;            // AgentConfig.SatietyDecayPerGameDay

        public WorldModel(DiscoveryService discovery, WorldConfigSO config, FarmService farm = null,
                          SeasonService season = null,
                          System.Func<int> aliveCount = null, AgentConfigSO agentCfg = null)
        {
            _discovery = discovery;
            _farm = farm;
            _season = season;
            _aliveCount = aliveCount;
            _decayPerDay = agentCfg != null ? agentCfg.SatietyDecayPerGameDay : 0f;
            if (config != null)
            {
                _slots[(int)SlotId.WoodStock]    = config.InitialWoodStock;
                _slots[(int)SlotId.RawFoodStock] = config.InitialRawFoodStock;
                _slots[(int)SlotId.StoneStock]   = config.InitialStoneStock;
                _foodValues = DeriveFoodValues(config.FoodSources); // ADR-M9-10 — 액션 에셋에서 파생
            }
        }

        /// <summary>FoodSources → (스톡 슬롯, 1개당 포만) 가치표 (M9-G). 식량 아닌 항목은 건너뛴다.</summary>
        private static (SlotId slot, int gain)[] DeriveFoodValues(ConsumeActionSO[] sources)
        {
            if (sources == null || sources.Length == 0) return null;
            var list = new List<(SlotId, int)>(sources.Length);
            foreach (ConsumeActionSO a in sources)
                if (TryGetFoodValue(a, out SlotId slot, out int gain)) list.Add((slot, gain));
            return list.Count > 0 ? list.ToArray() : null;
        }

        public int GetStock(SlotId slot) => _slots[(int)slot];

        /// <summary>스톡 증감. 음수 결과는 0 클램프 + 경고 (舊 B26 계승).</summary>
        public void AddStock(SlotId slot, int amount)
        {
            int result = _slots[(int)slot] + amount;
            if (result < 0)
            {
                Debug.LogWarning($"[WorldModel] {slot} 증감 결과 음수({result}) — 0으로 클램프. 호출 경로 점검 필요.");
                result = 0;
            }
            _slots[(int)slot] = result;
        }

        /// <summary>차감 시도. 부족하면 아무 것도 바꾸지 않고 false (원자성, 舊 ADR-2 계승).</summary>
        public bool TrySpendStock(SlotId slot, int amount)
        {
            if (_slots[(int)slot] < amount) return false;
            _slots[(int)slot] -= amount;
            return true;
        }

        public bool GetFlag(SlotId slot) => _slots[(int)slot] != 0;

        /// <summary>완공 플래그 세팅 — ConstructionService.Complete() 전용 (단일 완료 지점).</summary>
        internal void SetBuiltFlag(SlotId slot, bool value) => _slots[(int)slot] = value ? 1 : 0;

        /// <summary>
        /// 소비 액션 → (스톡 슬롯, 1개당 포만) 파생 (순수 — M9-T5, ADR-M9-10). 식량 가치의 유일한
        /// 출처는 액션 에셋이다: 효과에서 스톡 SubClamp0 1개 + MySatiety Add를 찾는다. 둘 중 하나라도
        /// 없으면 false (식량 아님 — FoodSources 오등록·Rest류 방어). 50·15 리터럴은 여기 없다.
        /// </summary>
        public static bool TryGetFoodValue(ActionSO a, out SlotId stockSlot, out int satietyGain)
        {
            stockSlot = default;
            satietyGain = 0;
            if (a == null) return false;

            var effects = new List<SlotEffect>();
            a.CollectEffects(effects);

            bool foundStock = false, foundGain = false;
            foreach (SlotEffect e in effects)
            {
                if (!foundStock && e.Op == EffectOp.SubClamp0 && SlotIds.IsStock(e.Slot) && e.Value == 1)
                {
                    stockSlot = e.Slot;
                    foundStock = true;
                }
                else if (!foundGain && e.Op == EffectOp.Add && e.Slot == SlotId.MySatiety)
                {
                    satietyGain = e.Value;
                    foundGain = true;
                }
            }
            return foundStock && foundGain;
        }

        /// <summary>
        /// 남은 식량 일수 (순수 — M9-T5): floor(Σ(스톡×1개당 포만) ÷ (인원×감쇠×계절배율)).
        /// 인원 ≤ 0·미배선·일소요 ≤ 0이면 NO_ESTIMATE(99). NO_ESTIMATE 상한 클램프 (트리거 언어 통일).
        /// </summary>
        public static int ComputeFoodDaysLeft((SlotId slot, int gain)[] foodValues, int[] slots,
                                              int aliveCount, float decayPerDay, float seasonMult)
        {
            if (foodValues == null || foodValues.Length == 0 || aliveCount <= 0) return NO_ESTIMATE;
            long totalSatiety = 0;
            foreach ((SlotId slot, int gain) fv in foodValues)
                totalSatiety += (long)slots[(int)fv.slot] * fv.gain;

            float dailyNeed = aliveCount * decayPerDay * seasonMult;
            if (dailyNeed <= 0f) return NO_ESTIMATE;
            return Mathf.Min(NO_ESTIMATE, Mathf.FloorToInt(totalSatiety / dailyNeed));
        }

        private float SeasonDecayMult() => _season != null ? _season.SatietyDecayMult : 1f;

        /// <summary>HUD 폴링용 공개 진입 (M9-I) — 스냅샷과 같은 산식·같은 캐시 (판정 이원화 금지).</summary>
        public int EstimateFoodDaysLeft()
            => _aliveCount != null && _foodValues != null
                ? ComputeFoodDaysLeft(_foodValues, _slots, _aliveCount(), _decayPerDay, SeasonDecayMult())
                : NO_ESTIMATE;

        /// <summary>
        /// 플래닝용 read-only 스냅샷 생성. 발견 플래그는 "잔량 있는 발견 노드 존재 여부"다
        /// (근접이 아님 — 노드까지의 이동은 W4 러너 담당).
        /// </summary>
        public WorldSnapshot BuildSnapshot(int satiety, int fatigue, bool hasHome = false)
        {
            var slots = new int[PlanningConfig.TotalSlots];

            // 월드 소유 슬롯은 전부 복사 — "슬롯 추가 시 복사 목록 누락" 함정의 구조적 제거
            // (2026-07-14 HouseCount 누락 사고: goal이 달성을 영영 몰라 집 초과 건설 + NoSolution)
            for (int i = 0; i < SlotIds.Count; i++)
                slots[i] = _slots[i];

            // 파생 슬롯만 덮어쓰기 — 각 원천이 유일한 출처
            slots[(int)SlotId.MySatiety] = satiety; // 에이전트 개인 소유
            slots[(int)SlotId.MyFatigue] = fatigue;
            slots[(int)SlotId.MyHasHome] = hasHome ? 1 : 0; // 원천 = OwnershipService (M8-C)
            slots[(int)SlotId.NearDiscoveredWood]  = Discovered(ResourceType.Wood);   // DiscoveryService
            slots[(int)SlotId.NearDiscoveredFood]  = Discovered(ResourceType.RawFood);
            slots[(int)SlotId.NearDiscoveredStone] = Discovered(ResourceType.Stone);
            slots[(int)SlotId.AtBuildSite] = 0; // W6에서 사용
            // Empty/Ripe의 유일한 원천은 FarmService (ADR-M2-4) — 미배선(테스트 등)이면 0.
            // 값은 개수 (ADR-M3-2) — 익은 밭 N개면 한 플랜에 수확 N회가 담긴다.
            slots[(int)SlotId.EmptyFarmPlot]     = _farm != null ? _farm.CountEmpty() : 0;
            slots[(int)SlotId.RipeCropAvailable] = _farm != null ? _farm.CountRipe()  : 0;
            // 계절의 유일한 원천은 SeasonService (M6-A) — 미배선이면 "위기 없음" 중립 (99/0 = M5 동일 판정)
            slots[(int)SlotId.DaysToCrisis] = _season != null
                ? Mathf.CeilToInt(_season.DaysToCrisis) : (int)SeasonService.NO_CRISIS;
            slots[(int)SlotId.CrisisActive] = _season != null && _season.Current != null
                                              && _season.Current.IsCrisis ? 1 : 0;
            // 남은 식량 일수 (M9-G) — DaysToCrisis와 같은 파생 슬롯 패턴. 미배선이면 99 (중립).
            slots[(int)SlotId.FoodDaysLeft] = _aliveCount != null && _foodValues != null
                ? ComputeFoodDaysLeft(_foodValues, slots, _aliveCount(), _decayPerDay, SeasonDecayMult())
                : NO_ESTIMATE;
            return new WorldSnapshot(slots);
        }

        private int Discovered(ResourceType type)
            => _discovery != null && _discovery.HasDiscovered(type) ? 1 : 0;
    }
}
