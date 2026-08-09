using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 시설 손상 표현 (M22-W7 → 2026-08-09 개정, 표현 전용) — DefenseService.OnDurabilityChanged 구독.
    ///
    /// 🔁 **개정 이유**: 舊 방식은 시설 위에 검붉은 반투명 원을 얹는 것이었다. 임시 색 마커
    /// 시절엔 그게 유일한 방법이었지만, 헌장 표현 조항(ADR-M20-1)이 실제로 쓴 말은
    /// *"색 바랜 울타리가 늘어나는 것"* 이다 — 덧붙인 점이 아니라 **물건 자체가 상하는 것**.
    /// 실그림이 들어온 지금은 그 말대로 할 수 있다: 깎일수록 울타리가 어둡고 바래진다.
    /// 화면에서 표식 하나가 사라지고, 목수 부재는 마을 색으로 읽힌다.
    ///
    /// 몸은 BuildingVisualizer가 소유하고 여기는 **색만 만진다** (ADR-M23-3 — 두 번째 스폰 경로
    /// 금지). 만복(수리·완공)·소멸이면 원래 색으로 되돌린다. 시뮬 쓰기 0.
    /// </summary>
    public sealed class DefenseDurabilityView
    {
        // 다 깎였을 때의 색 — 어둡고 채도가 빠진 쪽으로. 붉게 물들이지 않는다:
        // 팩 그림이 이미 나무색이라 붉은 색조를 얹으면 "불탄 것"으로 읽힌다.
        private static readonly Color Worn = new Color(0.45f, 0.40f, 0.38f, 1f);

        private readonly BuildingVisualizer _visualizer;

        public DefenseDurabilityView(BuildingVisualizer visualizer, DefenseService defense)
        {
            _visualizer = visualizer;
            if (defense != null) defense.OnDurabilityChanged += OnChanged;
        }

        private void OnChanged(SlotId slot, Vector2Int tile, float cur, float max)
        {
            if (_visualizer == null) return;
            if (!_visualizer.TryGetRenderer(slot, tile, out SpriteRenderer sr)) return;

            // max <= 0 = 소멸 신호. 몸이 곧 파괴되므로 색만 원복하고 끝낸다.
            float dmg = max > 0f ? Mathf.Clamp01(1f - cur / max) : 0f;
            sr.color = Color.Lerp(Color.white, Worn, dmg);
        }
    }
}
