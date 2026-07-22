using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 집 저장 식량 (M11-A) — 개인 인벤토리 선반의 "큰 비축" 절반. 쓰기는 TryAdd/TrySpend만
    /// (ADR-M11-1 — 두 번째 경로 금지). 키 = 집 타일 (ADR-M8-3 완공 타일 신원 규약과 동일 —
    /// agentId 키 금지: M12 위협의 집 타격 → 저장 상실을 연결할 곳이 타일이다, 명세 M11-A ⚠️③).
    /// 상한은 호출자(EffectApplier)가 AgentConfig에서 넘긴다 — 수치 단일 출처 (ADR-M11-3).
    /// 세이브 대상 (ADR-M11-10): _byTile 사전이 저장 상태의 전부다.
    /// </summary>
    public sealed class HomeStorageService
    {
        // 타일 → (생식, 조리식). 어느 집이 누구 것인가는 여기 없다 — 소유는 OwnershipService의 몫.
        private readonly Dictionary<Vector2Int, (int raw, int cooked)> _byTile =
            new Dictionary<Vector2Int, (int, int)>(16);

        public (int raw, int cooked) Get(Vector2Int homeTile)
            => _byTile.TryGetValue(homeTile, out (int raw, int cooked) s) ? s : (0, 0);

        /// <summary>슬롯별 잔량 — EffectApplier 선검사·스냅샷 주입 공용 (판정 단일).</summary>
        public int GetStock(Vector2Int homeTile, SlotId slot)
        {
            (int raw, int cooked) = Get(homeTile);
            return slot == SlotId.MyHomeRawFood ? raw
                 : slot == SlotId.MyHomeCookedFood ? cooked : 0;
        }

        /// <summary>입고 — 상한 초과면 아무 것도 바꾸지 않고 false (원자성, 舊 ADR-2 계승).
        /// 상한은 슬롯별 (몸 소지 NextPersonalStock과 동일 개정 — 컴파일러 주입 전제와 판정 단일).</summary>
        public bool TryAdd(Vector2Int homeTile, SlotId slot, int amount, int cap)
        {
            if (!SlotIds.IsHomeStock(slot) || amount <= 0) return false;
            (int raw, int cooked) = Get(homeTile);
            int cur = slot == SlotId.MyHomeRawFood ? raw : cooked;
            if (cur + amount > cap) return false;
            _byTile[homeTile] = slot == SlotId.MyHomeRawFood ? (raw + amount, cooked)
                                                             : (raw, cooked + amount);
            return true;
        }

        /// <summary>출고 — 부족하면 아무 것도 바꾸지 않고 false (원자성).</summary>
        public bool TrySpend(Vector2Int homeTile, SlotId slot, int amount)
        {
            if (!SlotIds.IsHomeStock(slot) || amount <= 0) return false;
            (int raw, int cooked) = Get(homeTile);
            int cur = slot == SlotId.MyHomeRawFood ? raw : cooked;
            if (cur < amount) return false;
            _byTile[homeTile] = slot == SlotId.MyHomeRawFood ? (raw - amount, cooked)
                                                             : (raw, cooked - amount);
            return true;
        }

        /// <summary>집 소멸 정리 (건물 파괴 시 — M12에서 "저장 식량 상실" 로그가 이 지점에 연결된다).</summary>
        public void ReleaseTile(Vector2Int tile)
        {
            if (_byTile.Remove(tile))
                Debug.Log($"[Inventory] ({tile.x},{tile.y}) 집 저장 소멸");
        }
    }
}
