using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// ConstructionService.OnCompleted 구독자 — 완공 건물의 시각 표현을 스폰한다.
    /// BuildingSO.Prefab이 있으면 그것을, 없으면 원형 마커를 코드 생성한다.
    /// 舊 BC5 함정 방어: 알파 0 자동 보정 + SortingOrder 명시.
    /// </summary>
    public sealed class BuildingVisualizer
    {
        private readonly Transform _parent;
        private Sprite _fallbackSprite; // 지연 1회 생성

        public BuildingVisualizer(Transform parent)
        {
            _parent = parent;
        }

        public GameObject Spawn(BuildingSO building, int tileX, int tileY)
        {
            var pos = new Vector3(tileX, tileY, 0f); // ADR-4: 2D X-Y 평면, Z=0

            if (building.Prefab != null)
                return Object.Instantiate(building.Prefab, pos, Quaternion.identity, _parent);

            // ── 폴백 마커 생성 ──
            var go = new GameObject($"Building_{building.name}");
            go.transform.SetParent(_parent, worldPositionStays: false);
            go.transform.position   = pos;
            go.transform.localScale = Vector3.one * Mathf.Max(0.1f, building.FallbackSize);

            var sr = go.AddComponent<SpriteRenderer>();
            if (_fallbackSprite == null) _fallbackSprite = CreateCircleSprite();
            sr.sprite = _fallbackSprite;

            Color c = building.FallbackColor;
            if (c.a <= 0f) c.a = 1f; // BC5: 알파 0 에셋 함정 방어
            sr.color = c;
            sr.sortingOrder = building.SortingOrder;
            return go;
        }

        /// <summary>32×32 안티앨리어싱 원형 스프라이트 (舊 ResourceNodeSpawner 폴백과 동일 패턴).</summary>
        private static Sprite CreateCircleSprite()
        {
            const int N = 32;
            const float C = N * 0.5f;
            const float R = C - 1.5f;

            var tex = new Texture2D(N, N, TextureFormat.RGBA32, mipChain: false);
            var pixels = new Color[N * N];
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float dist = Mathf.Sqrt((x + 0.5f - C) * (x + 0.5f - C) + (y + 0.5f - C) * (y + 0.5f - C));
                    float alpha = Mathf.Clamp01(R - dist + 0.5f);
                    pixels[y * N + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
        }
    }
}
