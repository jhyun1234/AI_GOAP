using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 조리 중인 모닥불 갈아입히기 (M22-3차 W5d, 표현 전용) — 주민이 그 모닥불에서 끓이는 동안
    /// BuildingSO.AltFrames(솥 걸린 그림)로 바뀌고, 끝나면 평상 불꽃으로 돌아온다.
    /// 몸(GameObject)은 BuildingVisualizer가 스폰하고 여기는 **옷만 바꾼다** (ADR-M23-3 —
    /// 두 번째 스폰 경로 금지, DefenseFenceView와 같은 규약). 시뮬 쓰기 0.
    ///
    /// 판정 원천은 VillagerAgent.CookingAt 하나뿐이다 — 위치·거리로 되짚지 않는다.
    /// 모닥불 곁엔 밥 먹는 사람도 앉아 있어서, 위치로 재면 아무도 안 끓여도 솥이 걸린다.
    /// </summary>
    public sealed class CampfireCookView
    {
        private readonly Dictionary<Vector2Int, BuildingFlipbook> _fires
            = new Dictionary<Vector2Int, BuildingFlipbook>();
        private readonly HashSet<Vector2Int> _cooking = new HashSet<Vector2Int>(); // 프레임 재사용 — 할당 0

        /// <summary>완공 등록 — 모닥불이 아니거나 애니 미배선이면 무시 (중립).</summary>
        public void Register(BuildingSO b, GameObject body, Vector2Int tile)
        {
            if (b == null || body == null || b.CountSlot != SlotId.CampfireCount) return;
            var fb = body.GetComponentInChildren<BuildingFlipbook>();
            if (fb != null) _fires[tile] = fb;
        }

        public void NotifyRemoved(SlotId slot, Vector2Int tile)
        {
            if (slot == SlotId.CampfireCount) _fires.Remove(tile);
        }

        /// <summary>매 틱 갱신 — 등록된 모닥불이 없으면 즉시 빠진다 (배선 전 판과 완전 동일).</summary>
        public void Tick(IReadOnlyList<VillagerAgent> agents)
        {
            if (_fires.Count == 0 || agents == null) return;
            _cooking.Clear();
            for (int i = 0; i < agents.Count; i++)
            {
                Vector2Int? at = agents[i] != null ? agents[i].CookingAt : null;
                if (at.HasValue) _cooking.Add(at.Value);
            }
            foreach (KeyValuePair<Vector2Int, BuildingFlipbook> kv in _fires)
                if (kv.Value != null) kv.Value.SetAlt(_cooking.Contains(kv.Key));
        }
    }
}
