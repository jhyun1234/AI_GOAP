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
        private readonly int[] _slots = new int[SlotIds.Count];
        private readonly DiscoveryService _discovery;
        private readonly FarmService _farm;
        private readonly SeasonService _season;

        public WorldModel(DiscoveryService discovery, WorldConfigSO config, FarmService farm = null,
                          SeasonService season = null)
        {
            _discovery = discovery;
            _farm = farm;
            _season = season;
            if (config != null)
            {
                _slots[(int)SlotId.WoodStock]    = config.InitialWoodStock;
                _slots[(int)SlotId.RawFoodStock] = config.InitialRawFoodStock;
                _slots[(int)SlotId.StoneStock]   = config.InitialStoneStock;
            }
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
            return new WorldSnapshot(slots);
        }

        private int Discovered(ResourceType type)
            => _discovery != null && _discovery.HasDiscovered(type) ? 1 : 0;
    }
}
