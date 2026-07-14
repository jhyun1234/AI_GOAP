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

        public WorldModel(DiscoveryService discovery, WorldConfigSO config, FarmService farm = null)
        {
            _discovery = discovery;
            _farm = farm;
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
        public WorldSnapshot BuildSnapshot(int satiety, int fatigue)
        {
            var slots = new int[PlanningConfig.TotalSlots];
            slots[(int)SlotId.WoodStock]    = _slots[(int)SlotId.WoodStock];
            slots[(int)SlotId.RawFoodStock] = _slots[(int)SlotId.RawFoodStock];
            slots[(int)SlotId.StoneStock]   = _slots[(int)SlotId.StoneStock];
            slots[(int)SlotId.MySatiety]    = satiety;
            slots[(int)SlotId.MyFatigue]    = fatigue;
            slots[(int)SlotId.NearDiscoveredWood]  = Discovered(ResourceType.Wood);
            slots[(int)SlotId.NearDiscoveredFood]  = Discovered(ResourceType.RawFood);
            slots[(int)SlotId.NearDiscoveredStone] = Discovered(ResourceType.Stone);
            slots[(int)SlotId.CampfireBuilt] = _slots[(int)SlotId.CampfireBuilt];
            slots[(int)SlotId.AtBuildSite]   = 0; // W6에서 사용
            slots[(int)SlotId.CookedFoodStock] = _slots[(int)SlotId.CookedFoodStock];
            slots[(int)SlotId.FarmPlotCount]   = _slots[(int)SlotId.FarmPlotCount];
            // Empty/Ripe의 유일한 원천은 FarmService (ADR-M2-4) — 미배선(테스트 등)이면 0
            slots[(int)SlotId.EmptyFarmPlot]     = _farm != null && _farm.HasEmpty ? 1 : 0;
            slots[(int)SlotId.RipeCropAvailable] = _farm != null && _farm.HasRipe  ? 1 : 0;
            return new WorldSnapshot(slots);
        }

        private int Discovered(ResourceType type)
            => _discovery != null && _discovery.HasDiscovered(type) ? 1 : 0;
    }
}
