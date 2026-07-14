using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    public enum FarmState { Empty, Growing, Ripe }

    /// <summary>밭 1칸 상태. 전이는 FarmService의 세 문(TryPlant/TickGrowth/TryHarvest)만 수행한다.</summary>
    public sealed class FarmPlot
    {
        public Vector2Int Tile { get; internal set; }
        public FarmState State { get; internal set; }
        public float Growth { get; internal set; } // Growing 누적 게임일

        /// <summary>점유 주민 ID (1인 점유, ResourceNode.TryOccupy 미러). null = 비점유.</summary>
        public string OccupantId { get; private set; }

        public bool TryClaim(string agentId)
        {
            if (OccupantId != null && OccupantId != agentId) return false;
            OccupantId = agentId;
            return true;
        }

        public void Release() => OccupantId = null;
    }

    /// <summary>
    /// 밭 상태의 유일한 원천 (ADR-M2-4) — 스냅샷의 EmptyFarmPlot/RipeCropAvailable은
    /// 여기서만 파생된다 (DiscoveryService의 NearDiscovered*와 동일 패턴).
    /// 심기 goal과 수확 goal 사이는 플랜이 아니라 게임일 성장 틱이 잇는다 (ADR-M2-1).
    /// </summary>
    public sealed class FarmService
    {
        private readonly List<FarmPlot> _plots = new List<FarmPlot>();
        private readonly float _growthDays;

        /// <summary>상태 전이 알림 — M2-D FarmPlotView가 구독한다 (진행도 폴링 금지).</summary>
        public event Action<FarmPlot> OnPlotStateChanged;

        public IReadOnlyList<FarmPlot> Plots => _plots;

        public FarmService(float growthDays)
        {
            _growthDays = Mathf.Max(0.01f, growthDays);
        }

        /// <summary>밭 완공 등록. 유일한 호출처 = ConstructionService.OnCompleted 구독 (SimulationLoop 조립).</summary>
        public void RegisterPlot(int tileX, int tileY)
        {
            var plot = new FarmPlot { Tile = new Vector2Int(tileX, tileY), State = FarmState.Empty };
            _plots.Add(plot);
            OnPlotStateChanged?.Invoke(plot);
        }

        /// <summary>게임일 성장 틱. Growing 밭이 성장 기간(WorldConfig.FarmGrowthDays)을 채우면 Ripe.</summary>
        public void TickGrowth(float deltaGameDays)
        {
            foreach (FarmPlot p in _plots)
            {
                if (p.State != FarmState.Growing) continue;
                p.Growth += deltaGameDays;
                if (p.Growth >= _growthDays)
                {
                    p.State = FarmState.Ripe;
                    OnPlotStateChanged?.Invoke(p);
                }
            }
        }

        public bool HasEmpty
        {
            get
            {
                foreach (FarmPlot p in _plots)
                    if (p.State == FarmState.Empty) return true;
                return false;
            }
        }

        public bool HasRipe
        {
            get
            {
                foreach (FarmPlot p in _plots)
                    if (p.State == FarmState.Ripe) return true;
                return false;
            }
        }

        /// <summary>최근접 빈 밭. 타인 점유 밭은 제외 — 같은 밭에 두 주민이 몰리는 헛걸음 완화.</summary>
        public FarmPlot NearestEmpty(int fromX, int fromY) => Nearest(FarmState.Empty, fromX, fromY);

        /// <summary>최근접 익은 밭. 타인 점유 밭은 제외.</summary>
        public FarmPlot NearestRipe(int fromX, int fromY) => Nearest(FarmState.Ripe, fromX, fromY);

        private FarmPlot Nearest(FarmState state, int fromX, int fromY)
        {
            FarmPlot best = null;
            int bestD = int.MaxValue;
            foreach (FarmPlot p in _plots)
            {
                if (p.State != state || p.OccupantId != null) continue;
                int dx = p.Tile.x - fromX, dy = p.Tile.y - fromY;
                int d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        /// <summary>심기 — Empty에서만 성공. 상태 전이의 유일한 문 (러너가 직접 State를 쓰지 않는다).</summary>
        public bool TryPlant(FarmPlot p)
        {
            if (p == null || p.State != FarmState.Empty) return false;
            p.State = FarmState.Growing;
            p.Growth = 0f;
            OnPlotStateChanged?.Invoke(p);
            return true;
        }

        /// <summary>수확 — Ripe에서만 성공. 밭은 빈 상태로 복귀. 스톡 증가는 EffectApplier 몫 (수치 단일 출처).</summary>
        public bool TryHarvest(FarmPlot p)
        {
            if (p == null || p.State != FarmState.Ripe) return false;
            p.State = FarmState.Empty;
            p.Growth = 0f;
            OnPlotStateChanged?.Invoke(p);
            return true;
        }
    }
}
