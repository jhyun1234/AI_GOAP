using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 구역 (M9-A) — 수량형 건물 배치 규칙의 유일한 결정자 (ADR-M9-1: BuildRunner 군집 휴리스틱을
    /// 대체한다. 밭만 구역·집만 군집 같은 이원화는 반려 — 반경은 BuildingSO.ZoneRadius 에셋 값이다).
    /// 앵커 = 해당 슬롯의 첫 완공 타일 (ADR-M9-2, 결정적 — 회의 연출·테두리는 이 확정을 표현할 뿐).
    /// 세이브 대상 (ADR-M0-10 각주 — 앵커는 완공 위치에서 재유도 가능하나, 재로드 시 첫 완공을
    /// 다시 잡으려면 완공 순서가 필요하므로 저장 후보로 선언).
    /// </summary>
    public sealed class ZoneService
    {
        private readonly Dictionary<SlotId, (Vector2Int anchor, int radius)> _zones = new();

        /// <summary>구역 확정 1회 알림 (countSlot, anchor, radius) — 회의 연출(M9-E)·테두리 뷰가 구독.</summary>
        public event Action<SlotId, Vector2Int, int> OnZoneEstablished;

        /// <summary>
        /// 완공 통지 — ConstructionService.OnCompleted 구독 배선이 호출한다.
        /// 수량형·반경>0·미확정 슬롯의 첫 완공만 앵커가 된다 (두 번째 완공부터는 무시).
        /// </summary>
        public void NotifyBuilt(BuildingSO b, int x, int y)
        {
            if (b == null || !b.IsCountable || b.ZoneRadius <= 0 || _zones.ContainsKey(b.CountSlot))
                return;
            var anchor = new Vector2Int(x, y);
            _zones[b.CountSlot] = (anchor, b.ZoneRadius);
            Debug.Log($"[Zone] {b.CountSlot} 구역 확정 @ ({x},{y}) r={b.ZoneRadius}");
            OnZoneEstablished?.Invoke(b.CountSlot, anchor, b.ZoneRadius);
        }

        // (M22-W3의 EstablishPlayerZone은 ADR-M22-4 개정으로 삭제 — 드래그 사각형이
        //  (앵커, 반경) 그릇에 안 맞아 방어 계획 소유가 DefenseService로 이동했다. 폐기=삭제.)

        /// <summary>확정된 구역의 앵커·반경. 미확정이면 false (호출처가 첫 완공 경로로 폴백).</summary>
        public bool TryGetZone(SlotId countSlot, out Vector2Int anchor, out int radius)
        {
            if (_zones.TryGetValue(countSlot, out (Vector2Int a, int r) z))
            {
                anchor = z.a;
                radius = z.r;
                return true;
            }
            anchor = default;
            radius = 0;
            return false;
        }

        /// <summary>
        /// 타일이 구역 안인가 — 체비쇼프 반경 판정. 미확정 구역은 true (첫 완공 전엔 어디든 자유,
        /// M9-A ⚠️③: 미확정에서 false를 주면 첫 건설 자체가 불가능해진다).
        /// </summary>
        public bool Contains(SlotId countSlot, int x, int y)
        {
            if (!_zones.TryGetValue(countSlot, out (Vector2Int a, int r) z)) return true;
            return Mathf.Max(Mathf.Abs(x - z.a.x), Mathf.Abs(y - z.a.y)) <= z.r;
        }
    }
}
