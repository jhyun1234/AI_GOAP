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

        /// <summary>밭 시설 소실 알림 (M9-B) — FarmPlotView가 오버레이 GameObject를 파괴한다.
        /// 상태 전이(OnPlotStateChanged)가 아니라 소멸이라 별도 이벤트 (사전 키 잔류 = 누수, ⚠️②).</summary>
        public event Action<FarmPlot> OnPlotRemoved;

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

        /// <summary>빈 밭 개수 — 스냅샷 EmptyFarmPlot 슬롯의 원천 (ADR-M3-2: 0/1 → 개수 승격).</summary>
        public int CountEmpty() => CountState(FarmState.Empty);

        /// <summary>익은 밭 개수 — 스냅샷 RipeCropAvailable 슬롯의 원천. 개수라서 한 플랜에 연속 수확이 담긴다.</summary>
        public int CountRipe() => CountState(FarmState.Ripe);

        public bool HasEmpty => CountEmpty() > 0;

        public bool HasRipe => CountRipe() > 0;

        private int CountState(FarmState state)
        {
            int n = 0;
            foreach (FarmPlot p in _plots)
                if (p.State == state) n++;
            return n;
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

        /// <summary>심기 — Empty에서만 성공. 상태 전이의 유일한 문 (러너가 직접 State를 쓰지 않는다).
        /// 제거된 플롯(_plots 밖 참조)은 거부 — 유령 심기 방지 (M9-B ⚠️①, ADR-M0-7 실패 first-class).</summary>
        public bool TryPlant(FarmPlot p)
        {
            if (p == null || p.State != FarmState.Empty || !_plots.Contains(p)) return false;
            p.State = FarmState.Growing;
            p.Growth = 0f;
            OnPlotStateChanged?.Invoke(p);
            return true;
        }

        /// <summary>수확 — Ripe에서만 성공. 밭은 빈 상태로 복귀. 스톡 증가는 EffectApplier 몫 (수치 단일 출처).
        /// 제거된 플롯은 거부 — 러너가 들고 있던 참조로 유령 수확 불가 (M9-B ⚠️①).</summary>
        public bool TryHarvest(FarmPlot p)
        {
            if (p == null || p.State != FarmState.Ripe || !_plots.Contains(p)) return false;
            p.State = FarmState.Empty;
            p.Growth = 0f;
            OnPlotStateChanged?.Invoke(p);
            return true;
        }

        /// <summary>
        /// 작물 소실 (M9-B, ADR-M9-3의 네 번째 문) — Growing·Ripe → Empty. 시설(밭)은 남아
        /// 재파종 가능. 점유도 해제 (소실된 작물을 붙잡고 있던 주민 놓아줌). 재해가 부르는 유일한
        /// 작물 파괴 경로 — DisasterService가 State를 직접 쓰지 않고 반드시 이 문을 지난다 (M9-C ⚠️④).
        /// </summary>
        public bool BlightCrop(FarmPlot p)
        {
            if (p == null || p.State == FarmState.Empty || !_plots.Contains(p)) return false;
            p.State = FarmState.Empty;
            p.Growth = 0f;
            p.Release();
            OnPlotStateChanged?.Invoke(p); // 뷰는 기존 이벤트로 오버레이 → 빈 흙 (공짜 갱신)
            return true;
        }

        /// <summary>
        /// 밭 시설 소실 (M9-B, ADR-M9-3의 다섯 번째 문) — 목록 제거 + 소멸 알림. 등록(RegisterPlot)과
        /// 대칭. 유일한 호출처 = ConstructionService.OnRemoved 구독 (SimulationLoop 배선) — 다른
        /// 경로가 _plots를 지우면 반려. 제거 후 그 FarmPlot 참조는 TryPlant/TryHarvest에서 전부 false.
        /// </summary>
        public bool RemovePlot(int tileX, int tileY)
        {
            for (int i = 0; i < _plots.Count; i++)
            {
                FarmPlot p = _plots[i];
                if (p.Tile.x != tileX || p.Tile.y != tileY) continue;
                _plots.RemoveAt(i);
                p.Release();
                OnPlotRemoved?.Invoke(p);
                return true;
            }
            return false;
        }
    }
}
