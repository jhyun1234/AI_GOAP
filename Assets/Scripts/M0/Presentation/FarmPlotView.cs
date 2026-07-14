using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// FarmService.OnPlotStateChanged 구독자 — 밭 위 작물 오버레이를 상태별로 갱신한다 (M2-D).
    /// 이벤트 구동만 — 성장 진행도 매 프레임 폴링 금지 (M0 표현 원칙).
    /// 흙 바닥은 BuildingVisualizer의 밭 마커가 담당하고, 이 클래스는 그 위의 작물만 소유한다.
    /// 스프라이트 미지정 시 색 폴백: 빈 흙(오버레이 없음) → 연두 새싹 → 노랑 결실.
    /// </summary>
    public sealed class FarmPlotView
    {
        // 폴백 색·크기 — 표현 전용 상수 (게임 밸런스 수치 아님)
        private static readonly Color GrowingColor = new Color(0.45f, 0.85f, 0.35f, 1f);
        private static readonly Color RipeColor    = new Color(1f, 0.85f, 0.2f, 1f);
        private const float GrowingFallbackScale = 0.45f;
        private const float RipeFallbackScale    = 0.8f;

        private readonly Transform _parent;
        private readonly CropSpriteSetSO _sprites;
        private readonly Dictionary<FarmPlot, SpriteRenderer> _overlays =
            new Dictionary<FarmPlot, SpriteRenderer>();

        public FarmPlotView(Transform parent, CropSpriteSetSO sprites, FarmService farm)
        {
            _parent = parent;
            _sprites = sprites;
            farm.OnPlotStateChanged += Refresh;
        }

        private void Refresh(FarmPlot plot)
        {
            if (!_overlays.TryGetValue(plot, out SpriteRenderer sr))
            {
                var go = new GameObject($"Crop_{plot.Tile.x}_{plot.Tile.y}");
                go.transform.SetParent(_parent, worldPositionStays: false);
                go.transform.position = new Vector3(plot.Tile.x, plot.Tile.y, 0f); // ADR-9: 2D X-Y, Z=0
                sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = _sprites != null ? _sprites.SortingOrder : 3;
                _overlays[plot] = sr;
            }

            switch (plot.State)
            {
                case FarmState.Empty:
                    sr.enabled = false; // 빈 흙 — 밭 마커만 보인다 (수확 후 복귀 표현)
                    break;
                case FarmState.Growing:
                    Show(sr, _sprites != null ? _sprites.Growing : null, GrowingColor, GrowingFallbackScale);
                    break;
                case FarmState.Ripe:
                    Show(sr, _sprites != null ? _sprites.Ripe : null, RipeColor, RipeFallbackScale);
                    break;
            }
        }

        private void Show(SpriteRenderer sr, Sprite sprite, Color fallbackColor, float fallbackScale)
        {
            sr.enabled = true;
            if (sprite != null)
            {
                sr.sprite = sprite;
                sr.color = Color.white;
                sr.transform.localScale = Vector3.one * Mathf.Max(0.1f, _sprites.Scale);
            }
            else
            {
                sr.sprite = M0Sprites.Circle;
                sr.color = fallbackColor;
                sr.transform.localScale = Vector3.one * fallbackScale;
            }
        }
    }
}
