using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>공용 런타임 스프라이트 (주민 마커·건물 폴백이 공유). W5에서 실제 아트로 교체 대상.</summary>
    public static class M0Sprites
    {
        private static Sprite _circle;

        /// <summary>32×32 안티앨리어싱 원형 (흰색 — SpriteRenderer.color로 착색).</summary>
        public static Sprite Circle
        {
            get
            {
                if (_circle == null) _circle = CreateCircleSprite();
                return _circle;
            }
        }

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
