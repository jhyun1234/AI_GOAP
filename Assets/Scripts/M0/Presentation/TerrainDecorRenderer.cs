using System.Collections.Generic;
using AIVillage.Core;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 지형 장식 산포 (M29-3차) — **색만 다른 타일은 지역이 아니다** (사용자 Play 지적 2026-08-12:
    /// *"주민이 정글인지 늪인지 광산인지 분별을 할 수 있겠어?"*). 정글은 덤불로 빽빽하고, 산은
    /// 바위가 흩어져 있고, 심층은 종유석이 솟아 있어야 **그 땅이 무엇인지 화면이 말한다.**
    ///
    /// 🔑 **판정이 없다.** 장식은 통행·자원·경로 어디에도 안 걸린다 — 순수 표현이라 서비스가
    /// 이것을 모른다. 그래서 지형 에셋에 그림만 꽂으면 늘어난다 (코드 0줄).
    /// 🔑 **좌표의 함수다** (ADR-T-5와 같은 계약): `f(판 시드, x, y)`라 같은 자리는 언제나 같은
    /// 풀포기다. 화면 밖으로 나갔다 돌아와도 안 바뀌고, 저장할 것도 없다.
    /// 🔑 **자원 노드 자리는 비운다** — 바위 장식이 광석을 가리면 "저기 있는데 왜 안 캐지"가 된다.
    ///
    /// 렌더 예산: 카메라에 보이는 칸만 만든다 (풀에서 꺼내 쓴다). 200² 전체를 만들면 4만 칸이라
    /// 판이 시작도 못 한다 — 화면에 없는 풀포기는 존재할 이유가 없다.
    /// </summary>
    public sealed class TerrainDecorRenderer : MonoBehaviour
    {
        // 한 화면에 허용하는 장식 수 상한 — 알고리즘 상수 (밀도는 에셋이 정한다).
        // 🔑 1600 → 4000 (2026-08-12 실측): 상한에 걸리면 **화면 아래쪽이 통째로 민둥산이 된다**
        //    (위에서부터 채우므로). 1600개에서 505fps였으니 예산이 아니라 상한이 문제였다.
        private const int MAX_DECOR = 4000;
        // 이 이상 넓게 보면 장식을 끈다 — 한 칸이 몇 픽셀이라 장식이 그림이 아니라 노이즈가 되고,
        // 그 화면에서 지역을 가르는 것은 어차피 **바닥색**이다 (전체 지도 보기).
        private const int MAX_VISIBLE_TILES = 10000;
        // 카메라가 이만큼(타일) 움직이면 다시 깐다. MapChunkRenderer의 청크 갱신과 같은 규약.
        private const float REBUILD_MOVE_TILES = 3f;
        private const float MARGIN_TILES = 4f;   // 화면 가장자리에서 툭 튀어나오지 않게

        private TerrainService _terrain;
        private System.Func<Vector2Int, bool> _tileTaken;   // 노드·건물이 선 칸
        private uint _seed;

        private readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        private Camera _cam;
        private Vector3 _lastCamPos = Vector3.positiveInfinity;
        private float _lastCamSize = -1f;
        private bool _lastRevealAll;

        /// <summary>씬 배선 없음 — 코드가 만든다 (BuildingVisualizer 패턴 M6-C).</summary>
        public static TerrainDecorRenderer Create(Transform parent, TerrainService terrain, uint seed,
                                                  System.Func<Vector2Int, bool> tileTaken)
        {
            var go = new GameObject("TerrainDecor");
            go.transform.SetParent(parent, worldPositionStays: false);
            var r = go.AddComponent<TerrainDecorRenderer>();
            r._terrain = terrain;
            r._seed = seed;
            r._tileTaken = tileTaken;
            return r;
        }

        private void LateUpdate()
        {
            if (_terrain == null || _terrain.IsEmpty) return;
            if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }

            bool revealAll = FowManager.Instance != null && FowManager.Instance.RevealAll;
            Vector3 pos = _cam.transform.position;
            if (revealAll == _lastRevealAll
                && Mathf.Approximately(_cam.orthographicSize, _lastCamSize)
                && Vector3.Distance(pos, _lastCamPos) < REBUILD_MOVE_TILES) return;

            _lastCamPos = pos;
            _lastCamSize = _cam.orthographicSize;
            _lastRevealAll = revealAll;
            Rebuild();
        }

        private void Rebuild()
        {
            float halfH = _cam.orthographicSize + MARGIN_TILES;
            float halfW = halfH * _cam.aspect;
            Vector3 c = _cam.transform.position;
            MapBounds.Get(out int minX, out int maxX, out int minY, out int maxY);
            int x0 = Mathf.Max(minX, Mathf.FloorToInt(c.x - halfW));
            int x1 = Mathf.Min(maxX, Mathf.CeilToInt(c.x + halfW));
            int y0 = Mathf.Max(minY, Mathf.FloorToInt(c.y - halfH));
            int y1 = Mathf.Min(maxY, Mathf.CeilToInt(c.y + halfH));

            // 기억(1) 상태는 바닥이 어두워지므로 장식도 같이 어두워야 한다 — 안 그러면
            // 안개 속에서 풀만 환하게 뜬다 (바닥 렌더러의 fowExplored 알파와 같은 자).
            float dim = MapConfig.Active != null ? MapConfig.Active.fowExplored.a / 255f : 0.7f;
            var dimColor = new Color(dim, dim, dim, 1f);

            int used = 0;
            if ((long)(x1 - x0 + 1) * (y1 - y0 + 1) > MAX_VISIBLE_TILES)
            {
                foreach (SpriteRenderer sr in _pool) sr.enabled = false;
                return;
            }

            for (int y = y1; y >= y0 && used < MAX_DECOR; y--)
                for (int x = x0; x <= x1 && used < MAX_DECOR; x++)
                {
                    TerrainTypeSO t = _terrain.At(x, y);
                    if (t == null || t.DecorSprites == null || t.DecorSprites.Length == 0
                        || t.DecorPerTile <= 0f) continue;

                    byte fow = FowManager.Instance != null ? FowManager.Instance.GetFowState(x, y)
                                                           : FowManager.FOW_VISIBLE;
                    if (fow == 0) continue;   // 안 밝힌 땅에 풀이 떠 있으면 안개가 뚫린다

                    uint h = Hash(x, y);
                    if ((h & 0xFFFFu) / 65535f >= t.DecorPerTile) continue;
                    if (_tileTaken != null && _tileTaken(new Vector2Int(x, y))) continue;

                    Sprite sprite = t.DecorSprites[(int)((h >> 16) % (uint)t.DecorSprites.Length)];
                    if (sprite == null) continue;

                    SpriteRenderer sr = Take(used++);
                    // 칸 안에서 살짝 흩어 놓는다 — 격자에 딱 맞으면 심어 놓은 티가 난다.
                    float ox = ((h >> 20) & 0xFFu) / 255f - 0.5f;
                    float oy = ((h >> 12) & 0xFFu) / 255f - 0.5f;
                    sr.transform.position = new Vector3(x + ox * 0.7f, y + oy * 0.7f, 0f);
                    sr.sprite = sprite;
                    sr.color = fow == FowManager.FOW_VISIBLE ? Color.white : dimColor;
                    // 바닥 데칼 — 자원·주민·건물 **아래**다. 장식이 광석을 가리면 표현이 아니라 방해다.
                    sr.sortingOrder = WorldSort.Decal;
                    float scale = t.DecorSize * (0.85f + ((h >> 8) & 0xFu) / 15f * 0.3f);
                    sr.transform.localScale = new Vector3(scale, scale, 1f);
                    sr.enabled = true;
                }

            for (int i = used; i < _pool.Count; i++) _pool[i].enabled = false;
        }

        private SpriteRenderer Take(int i)
        {
            while (_pool.Count <= i)
            {
                var go = new GameObject("decor");
                go.transform.SetParent(transform, worldPositionStays: false);
                _pool.Add(go.AddComponent<SpriteRenderer>());
            }
            return _pool[i];
        }

        /// <summary>좌표 해시 — `TerrainService.Hash01`과 같은 기법(`PickVariantIndex` 계보).
        /// 같은 판·같은 칸이면 언제나 같은 장식이다.</summary>
        private uint Hash(int x, int y)
        {
            unchecked
            {
                uint h = _seed ^ 0xB5297A4Du;
                h ^= (uint)x * 0x9E3779B9u; h = (h ^ (h >> 15)) * 0x2545F491u;
                h ^= (uint)y * 0x85EBCA6Bu; h = (h ^ (h >> 13)) * 0xC2B2AE35u;
                return h ^ (h >> 16);
            }
        }
    }
}
