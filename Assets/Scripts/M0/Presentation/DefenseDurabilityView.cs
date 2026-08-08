using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 시설 손상 오버레이 (M22-W7, 표현 전용) — DefenseService.OnDurabilityChanged 구독.
    /// 손상된(0 < 내구도 < 최대) 시설 위에 검붉은 반투명 마커를 얹는다 — 깎일수록 짙어진다.
    /// 헌장 표현 조항(ADR-M20-1)의 이행 지점: "색 바랜 울타리가 늘어나는 것"이 곧 목수 부재의
    /// 화면이다. 만복(수리·완공)·소멸(파괴, max=0 신호)이면 마커를 걷는다. 시뮬 쓰기 0.
    /// </summary>
    public sealed class DefenseDurabilityView
    {
        private readonly Transform _parent;
        private readonly Dictionary<(SlotId slot, Vector2Int tile), SpriteRenderer> _overlays
            = new Dictionary<(SlotId, Vector2Int), SpriteRenderer>();

        public DefenseDurabilityView(Transform parent, DefenseService defense)
        {
            _parent = parent;
            if (defense != null) defense.OnDurabilityChanged += OnChanged;
        }

        private void OnChanged(SlotId slot, Vector2Int tile, float cur, float max)
        {
            var key = (slot, tile);
            bool damaged = max > 0f && cur < max;
            if (!damaged)
            {
                if (_overlays.TryGetValue(key, out SpriteRenderer gone))
                {
                    if (gone != null) Object.Destroy(gone.gameObject);
                    _overlays.Remove(key);
                }
                return;
            }

            if (!_overlays.TryGetValue(key, out SpriteRenderer sr) || sr == null)
            {
                var go = new GameObject($"DefenseDamage_{tile.x}_{tile.y}");
                go.transform.SetParent(_parent, worldPositionStays: false);
                go.transform.position = new Vector3(tile.x, tile.y, 0f); // ADR-M0-9 — X-Y 평면
                go.transform.localScale = Vector3.one * 0.55f;
                sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = M0Sprites.Circle;
                sr.sortingOrder = 6; // 건물 마커(5) 위, 위협(11) 아래
                _overlays[key] = sr;
            }
            float dmg = 1f - cur / max; // 0(멀쩡)~1(파괴 직전)
            sr.color = new Color(0.6f, 0.08f, 0.05f, 0.2f + 0.55f * dmg); // 깎일수록 짙은 검붉음
        }
    }
}
