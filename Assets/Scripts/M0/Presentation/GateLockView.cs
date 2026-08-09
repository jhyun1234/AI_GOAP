using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 문 잠금 표현 (M22-2차 W1 → 2026-08-09 개정, 표현 전용) — DefenseService.OnGateLockChanged 구독.
    ///
    /// 🔁 **개정 이유**: 舊 방식은 문 위에 노란 원 표식을 얹는 것이었다. 그때는 문에 제 그림이
    /// 없어(울타리 조각 + 금빛 색조) 상태를 말할 방법이 표식뿐이었다. 구매 팩에 여닫는 문 그림
    /// (Palisade_Gate_Anim)이 들어오면서 **상태를 물건 자체가 말할 수 있게** 됐다 —
    /// 잠그면 닫히고 풀면 열린다. 화면에서 표식 하나가 사라지고 뜻은 더 분명해진다.
    ///
    /// 그림 교체는 BuildingFlipbook이 하고(평상=열린 문 / 대체=닫힌 문), 여기는 **어느 쪽인지만**
    /// 알려준다 — CampfireCookView와 같은 규약이다. 문 에셋에 AltFrames가 없으면 아무 일도
    /// 일어나지 않는다 (중립: 그림 미배선 판은 배선 전과 동일).
    ///
    /// 내구도 이벤트도 구독한다 — 잠금 중 신규 문 완공·파괴를 따라가야 새 문도 닫힌다. 시뮬 쓰기 0.
    /// </summary>
    public sealed class GateLockView
    {
        private readonly DefenseService _defense;
        // 문 몸체의 재생기 — 등록은 완공 배선(SimulationLoop)이, 해제는 소멸 통지가 한다.
        private readonly Dictionary<Vector2Int, BuildingFlipbook> _gates
            = new Dictionary<Vector2Int, BuildingFlipbook>();

        public GateLockView(DefenseService defense)
        {
            _defense = defense;
            if (defense == null) return;
            defense.OnGateLockChanged += _ => Apply();
            // 내구도 변화 = 문 생사의 유일한 신호 (등록·소멸이 같은 이벤트로 나간다).
            defense.OnDurabilityChanged += (slot, tile, cur, max) =>
            {
                if (slot == SlotId.GateCount) Apply();
            };
        }

        /// <summary>완공 등록 — 문이 아니거나 그림 미배선이면 무시 (중립).</summary>
        public void Register(BuildingSO b, GameObject body, Vector2Int tile)
        {
            if (b == null || body == null || b.CountSlot != SlotId.GateCount) return;
            var fb = body.GetComponentInChildren<BuildingFlipbook>();
            if (fb == null) return;
            _gates[tile] = fb;
            fb.SetAlt(_defense != null && _defense.GatesLocked); // 잠금 중에 세워진 문도 닫혀서 선다
        }

        public void NotifyRemoved(SlotId slot, Vector2Int tile)
        {
            if (slot == SlotId.GateCount) _gates.Remove(tile);
        }

        private void Apply()
        {
            bool locked = _defense.GatesLocked;
            foreach (KeyValuePair<Vector2Int, BuildingFlipbook> kv in _gates)
                if (kv.Value != null) kv.Value.SetAlt(locked);
        }
    }
}
