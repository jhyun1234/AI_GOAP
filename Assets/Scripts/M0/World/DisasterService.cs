using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 재해 (M9-C) — 계절마다 확정 발동 (ADR-M9-4, 확률 없음), 수확 전 파이프라인만 타격
    /// (ADR-M9-5 — 창고 스톡 불가침). 표현이 아닌 시뮬 서비스지만 파괴는 직접 하지 않고
    /// ADR-M9-3의 문(FarmService.BlightCrop / ConstructionService.RemoveCountableAt)을 호출한다.
    /// 소실 수 산식·희생 선택은 순수 함수 + StableHash 시드 (UnityEngine.Random 금지 — 결정적).
    /// 발동 상태(_lastStruckOrdinal)는 세이브 대상 아님 — 계절 위상에서 재유도 (ADR-M0-10 각주).
    /// Disasters가 비면 SimulationLoop이 서비스 자체를 null로 둔다 (중립 불변식, SeasonService 패턴).
    /// </summary>
    public sealed class DisasterService
    {
        private readonly DisasterSO[] _disasters;
        private readonly FarmService _farm;
        private readonly ConstructionService _construction;
        private readonly WorldModel _world;

        // 각 재해가 마지막으로 발동한 계절 서수 — 같은 계절 인스턴스 내 중복 발동 방지 (ADR-M9-4).
        private readonly Dictionary<DisasterSO, int> _lastStruckOrdinal = new Dictionary<DisasterSO, int>();

        // 재사용 버퍼 — GC 억제. 대상은 스냅샷(파괴 중 무효화 방지)이므로 반드시 복사본이다.
        private readonly List<FarmPlot> _targetBuf = new List<FarmPlot>(32);
        private readonly List<int> _victimBuf = new List<int>(32);

        /// <summary>발동 알림 (재해, 소실 수) — HUD Notify·근처 주민 반응 대사가 구독 (표현).</summary>
        public event Action<DisasterSO, int> OnStruck;

        public DisasterService(DisasterSO[] disasters, FarmService farm,
                               ConstructionService construction, WorldModel world)
        {
            _disasters = disasters ?? Array.Empty<DisasterSO>();
            _farm = farm;
            _construction = construction;
            _world = world;
        }

        /// <summary>
        /// SimulationLoop 틱 — 현재 계절과 위상을 받아 발동 조건을 검사한다. 조건:
        /// 재해 계절 == 현재 && 계절 경과일 ≥ OffsetDays && 이번 계절 인스턴스 미발동.
        /// seasonOrdinal은 사이클 누적 서수라 같은 계절이라도 사이클마다 재발동한다.
        /// </summary>
        public void Tick(SeasonSO current, float daysIntoSeason, int seasonOrdinal)
        {
            if (current == null) return;
            foreach (DisasterSO d in _disasters)
            {
                if (d == null || d.Season != current) continue;
                if (daysIntoSeason < d.OffsetDays) continue;
                if (_lastStruckOrdinal.TryGetValue(d, out int last) && last == seasonOrdinal) continue;
                _lastStruckOrdinal[d] = seasonOrdinal;
                Strike(d, seasonOrdinal);
            }
        }

        private void Strike(DisasterSO d, int seasonOrdinal)
        {
            // 대상 스냅샷 — TargetCrops면 자라는/익은 작물, 아니면 전체 밭 시설.
            // 반드시 복사본: 파괴가 _farm._plots를 건드려도(RemovePlot) 인덱스가 흔들리지 않는다.
            _targetBuf.Clear();
            foreach (FarmPlot p in _farm.Plots)
            {
                if (d.TargetCrops)
                {
                    if (p.State == FarmState.Growing || p.State == FarmState.Ripe) _targetBuf.Add(p);
                }
                else _targetBuf.Add(p);
            }

            int targetCount = _targetBuf.Count;
            bool resisted = d.UseResistance && _world.GetFlag(d.ResistanceSlot);
            int loss = LossCount(targetCount, d, resisted);

            // 결정적 시드 — 계절 서수 + 재해 이름 salt (같은 계절 다중 재해가 다른 희생을 고르게)
            uint seed = StableHash.Fnv1a(seasonOrdinal.ToString(), d.DisplayName);
            PickVictims(targetCount, loss, seed, _victimBuf);

            foreach (int idx in _victimBuf)
            {
                FarmPlot p = _targetBuf[idx];
                if (d.TargetCrops) _farm.BlightCrop(p);                                    // 작물 소실의 문
                else _construction.RemoveCountableAt(SlotId.FarmPlotCount, p.Tile.x, p.Tile.y); // 시설 소실의 문
            }

            Debug.Log($"[Disaster] {d.DisplayName} 발동 — {(d.TargetCrops ? "작물" : "밭")} " +
                      $"{loss}/{targetCount} 소실 (저항 {(resisted ? "O" : "X")})");
            OnStruck?.Invoke(d, loss);
        }

        /// <summary>
        /// 소실 수 (순수 — M9-T3): CeilToInt(대상수 × Clamp(Base + PerTarget×대상수, 0, Max)
        /// × (저항 ? ResistMult : 1)). 대상 0이면 0 (게이트 케이스 — 발동 로그만).
        /// </summary>
        public static int LossCount(int targets, DisasterSO d, bool resisted)
            => LossCount(targets, d.BaseLossPct, d.PerTargetPct, d.MaxLossPct,
                         resisted ? d.ResistMult : 1f);

        /// <summary>
        /// 곡선 원시형 (M10-C 위협이 재사용 — 산식 이원화 금지): CeilToInt(대상수 ×
        /// Clamp(Base + PerTarget×대상수, 0, Max) × resistMult). 재해 버전은 여기로 위임한다.
        /// </summary>
        public static int LossCount(int targets, float basePct, float perTargetPct,
                                    float maxLossPct, float resistMult = 1f)
        {
            if (targets <= 0) return 0;
            float pct = Mathf.Clamp(basePct + perTargetPct * targets, 0f, maxLossPct) * resistMult;
            return Mathf.Min(targets, Mathf.CeilToInt(targets * pct));
        }

        /// <summary>
        /// 희생 인덱스 선택 (순수·결정적 — M9-T3): 0..targetCount-1을 seed 기반 부분 Fisher-Yates로
        /// 섞어 앞 lossCount개를 result에 담는다. 같은 (수, 시드)면 같은 목록, 중복 없음.
        /// lossCount ≥ targetCount면 전원. UnityEngine.Random 금지 (ADR-M9-4 — 결정적 LCG).
        /// </summary>
        public static void PickVictims(int targetCount, int lossCount, uint seed, List<int> result)
        {
            result.Clear();
            if (targetCount <= 0 || lossCount <= 0) return;
            for (int i = 0; i < targetCount; i++) result.Add(i);
            if (lossCount >= targetCount) return; // 전원 — 셔플 불필요

            uint rng = seed == 0u ? 1u : seed; // 0 시드 방어 (LCG 고정점 회피)
            for (int i = 0; i < lossCount; i++)
            {
                rng = unchecked(rng * 1664525u + 1013904223u); // Numerical Recipes LCG (결정적)
                int j = i + (int)(rng % (uint)(targetCount - i));
                (result[i], result[j]) = (result[j], result[i]);
            }
            result.RemoveRange(lossCount, result.Count - lossCount);
        }
    }
}
