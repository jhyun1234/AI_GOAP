using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 건설 완료의 유일한 경로 (ADR-M0-3) — 舊 BC1(완료 경로 다중화) 재발의 구조적 차단.
    /// 비용 차감·완공 플래그·후처리(시각 스폰 등)가 전부 Complete() 하나를 거친다.
    /// 시각 스폰은 OnCompleted 구독자(BuildingVisualizer)가 수행한다.
    /// </summary>
    public sealed class ConstructionService
    {
        private readonly WorldModel _world;

        // 완공 위치 기록 — 여가 앵커(WanderRunner) 등 "건물이 어디 있나" 질의의 단일 출처 (M1-A)
        private readonly Dictionary<SlotId, Vector2Int> _builtTiles = new Dictionary<SlotId, Vector2Int>();

        /// <summary>완공 후처리 (building, tileX, tileY). M0SimulationLoop가 시각 스폰을 구독한다.</summary>
        public event Action<BuildingSO, int, int> OnCompleted;

        /// <summary>완공된 건물의 타일 위치. 미완공이면 false.</summary>
        public bool TryGetBuiltTile(SlotId flagSlot, out Vector2Int tile)
            => _builtTiles.TryGetValue(flagSlot, out tile);

        public ConstructionService(WorldModel world)
        {
            _world = world;
        }

        /// <summary>
        /// 건설 완료 처리. 원자성 보장: 비용 전 항목 선검사 후 일괄 차감 (부분 성공 없음, 舊 ADR-2 계승).
        /// 이미 완공된 건물이면 false (중복 완공 방지).
        /// </summary>
        public bool Complete(BuildingSO building, int tileX, int tileY)
        {
            if (building == null)
            {
                Debug.LogError("[ConstructionService] Complete: building이 null입니다.");
                return false;
            }
            if (_world.GetFlag(building.BuiltFlagSlot))
            {
                Debug.LogWarning($"[ConstructionService] {building.DisplayName}은(는) 이미 완공됨 — 중복 완공 거부.");
                return false;
            }

            // ── 1단계: 전 비용 선검사 (하나라도 부족하면 아무 것도 바꾸지 않는다) ──
            if (building.Costs != null)
            {
                foreach (ResourceCost c in building.Costs)
                {
                    if (_world.GetStock(c.StockSlot) < c.Amount)
                    {
                        Debug.LogWarning($"[ConstructionService] {building.DisplayName} 비용 부족: " +
                                         $"{c.StockSlot} {_world.GetStock(c.StockSlot)}/{c.Amount}. 완공 취소.");
                        return false;
                    }
                }

                // ── 2단계: 일괄 차감 (선검사 통과 → 전부 성공 보장) ──
                foreach (ResourceCost c in building.Costs)
                    _world.TrySpendStock(c.StockSlot, c.Amount);
            }

            // ── 3단계: 완공 플래그 + 위치 기록 + 후처리 ──
            _world.SetBuiltFlag(building.BuiltFlagSlot, true);
            _builtTiles[building.BuiltFlagSlot] = new Vector2Int(tileX, tileY);
            OnCompleted?.Invoke(building, tileX, tileY);

            Debug.Log($"[ConstructionService] {building.DisplayName} 완공 @ ({tileX}, {tileY})");
            return true;
        }
    }
}
