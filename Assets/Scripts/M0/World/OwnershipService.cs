using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 건물 소유 (M8-C) — 완공 타일 = 건물 신원 (ADR-M8-3: 인스턴스 ID 신설 금지,
    /// ConstructionService의 완공 위치 규약과 동일). 쓰기는 Assign/ReleaseBy만 — 두 번째
    /// 경로 금지. 1인 1채: 같은 슬롯의 두 번째 배정은 무시 (첫 배정 유지).
    /// 세이브 대상 (ADR-M0-10): _ownerByTile 사전이 저장 상태의 전부다.
    /// 에이전트=주민 전제 없음 — ID 문자열·SlotId로만 결합 (후반 확장 인지 규칙).
    /// </summary>
    public sealed class OwnershipService
    {
        // 타일 → (건물 슬롯, 소유자). 소유 가능 슬롯의 지식은 여기 없다 —
        // 무엇이 소유되는가는 호출자(부탁 에셋 OwnershipSlot·클레임 패스 인자)가 데이터로 결정.
        private readonly Dictionary<Vector2Int, (SlotId slot, string owner)> _ownerByTile =
            new Dictionary<Vector2Int, (SlotId, string)>(16);

        /// <summary>agentId가 소유한 slot 건물의 타일. 없으면 false.</summary>
        public bool TryGetOwned(string agentId, SlotId slot, out Vector2Int tile)
        {
            foreach (KeyValuePair<Vector2Int, (SlotId slot, string owner)> kv in _ownerByTile)
                if (kv.Value.slot == slot && kv.Value.owner == agentId)
                {
                    tile = kv.Key;
                    return true;
                }
            tile = default;
            return false;
        }

        /// <summary>해당 타일에 소유자가 있는가 — 클레임 패스의 무주 판정.</summary>
        public bool IsOwned(Vector2Int tile) => _ownerByTile.ContainsKey(tile);

        /// <summary>
        /// 소유 배정의 유일한 쓰기 지점 (ADR-M8-3). 이미 주인이 있는 건물·이미 같은 슬롯을
        /// 소유한 주민은 무시 (1인 1채 — 첫 배정 유지, 경고 로그).
        /// </summary>
        public void Assign(Vector2Int tile, SlotId slot, string agentId, string why)
        {
            if (string.IsNullOrEmpty(agentId)) return;
            if (_ownerByTile.TryGetValue(tile, out (SlotId slot, string owner) cur))
            {
                Debug.LogWarning($"[Ownership] ({tile.x},{tile.y})은 이미 {cur.owner} 소유 — 배정 무시 ({why})");
                return;
            }
            if (TryGetOwned(agentId, slot, out Vector2Int owned))
            {
                Debug.LogWarning($"[Ownership] {agentId}은(는) 이미 ({owned.x},{owned.y}) 소유 — " +
                                 $"1인 1채, 배정 무시 ({why})");
                return;
            }
            _ownerByTile[tile] = (slot, agentId);
            Debug.Log($"[Ownership] {agentId} ← {slot} ({tile.x},{tile.y}) 배정 ({why})");
        }

        /// <summary>이탈 주민 정리 — 소유가 풀려 빈집이 된다 (다음 클레임 패스의 재배정 대상).</summary>
        public void ReleaseBy(string agentId)
        {
            _tilesToRemove.Clear();
            foreach (KeyValuePair<Vector2Int, (SlotId slot, string owner)> kv in _ownerByTile)
                if (kv.Value.owner == agentId)
                    _tilesToRemove.Add(kv.Key);
            foreach (Vector2Int tile in _tilesToRemove)
            {
                _ownerByTile.Remove(tile);
                Debug.Log($"[Ownership] ({tile.x},{tile.y}) 소유 해제 — {agentId} 이탈 (빈집)");
            }
        }

        /// <summary>
        /// 주기 클레임 패스 (M8-C) — 무주 완공 건물 ↔ 무소유 주민을 최근접 배정 (순수 입력 —
        /// 게이트 M8-T3). requestInFlight면 그 회차 전체 유예 — 부탁으로 짓는 중인 건물을
        /// 다른 주민이 가로채지 않는다 (부탁자 우선권, 명세 ⚠️②).
        /// candidates = 그 슬롯을 소유하지 않은 주민 (호출자가 필터) — 순서대로 탐욕 배정.
        /// </summary>
        public void ClaimPass(IReadOnlyList<(string id, Vector2Int pos)> candidates,
                              IReadOnlyList<Vector2Int> builtTiles, SlotId slot, bool requestInFlight)
        {
            if (requestInFlight || candidates == null || builtTiles == null) return;

            foreach ((string id, Vector2Int pos) c in candidates)
            {
                if (TryGetOwned(c.id, slot, out _)) continue; // 방어 — 호출자 필터 누락 대비
                int best = int.MaxValue;
                Vector2Int bestTile = default;
                bool found = false;
                foreach (Vector2Int t in builtTiles)
                {
                    if (IsOwned(t)) continue;
                    int dx = t.x - c.pos.x, dy = t.y - c.pos.y;
                    int d = dx * dx + dy * dy;
                    if (d < best) { best = d; bestTile = t; found = true; }
                }
                if (found) Assign(bestTile, slot, c.id, "무주택 클레임");
            }
        }

        private readonly List<Vector2Int> _tilesToRemove = new List<Vector2Int>(4);
    }
}
