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
    public sealed class TerrainSurfaceRenderer : MonoBehaviour
    {
        // 한 화면에 허용하는 장식 수 상한 — 알고리즘 상수 (밀도는 에셋이 정한다).
        // 🔑 1600 → 4000 (2026-08-12 실측): 상한에 걸리면 **화면 아래쪽이 통째로 민둥산이 된다**
        //    (위에서부터 채우므로). 1600개에서 505fps였으니 예산이 아니라 상한이 문제였다.
        private const int MAX_DECOR = 4000;
        // 이 이상 넓게 보면 장식을 끈다 — 한 칸이 몇 픽셀이라 장식이 그림이 아니라 노이즈가 되고,
        // 그 화면에서 지역을 가르는 것은 어차피 **바닥색**이다 (전체 지도 보기).
        private const int MAX_VISIBLE_TILES = 10000;
        // 표면 조각 상한 — 경계·벽면·기단이 함께 쓴다. 경계는 장식보다 흔하다 (한 타일이 최대 넷).
        private const int MAX_EDGE = 6000;
        // 카메라가 이만큼(타일) 움직이면 다시 깐다. MapChunkRenderer의 청크 갱신과 같은 규약.
        private const float REBUILD_MOVE_TILES = 3f;
        private const float MARGIN_TILES = 4f;   // 화면 가장자리에서 툭 튀어나오지 않게

        private TerrainService _terrain;
        private System.Func<Vector2Int, bool> _tileTaken;   // 노드·건물이 선 칸
        private uint _seed;

        private readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _edgePool = new List<SpriteRenderer>();
        private static readonly Vector2Int[] DIRS =    // EdgeTiles 순서와 같다: 위·오른쪽·아래·왼쪽
            { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0) };
        private Camera _cam;
        private Vector3 _lastCamPos = Vector3.positiveInfinity;
        private float _lastCamSize = -1f;
        private bool _lastRevealAll;

        /// <summary>씬 배선 없음 — 코드가 만든다 (BuildingVisualizer 패턴 M6-C).</summary>
        public static TerrainSurfaceRenderer Create(Transform parent, TerrainService terrain, uint seed,
                                                  System.Func<Vector2Int, bool> tileTaken)
        {
            var go = new GameObject("TerrainDecor");
            go.transform.SetParent(parent, worldPositionStays: false);
            var r = go.AddComponent<TerrainSurfaceRenderer>();
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

            int used = 0, edges = 0;
            if ((long)(x1 - x0 + 1) * (y1 - y0 + 1) > MAX_VISIBLE_TILES)
            {
                foreach (SpriteRenderer sr in _pool) sr.enabled = false;
                foreach (SpriteRenderer sr in _edgePool) sr.enabled = false;
                return;
            }

            for (int y = y1; y >= y0 && used < MAX_DECOR; y--)
                for (int x = x0; x <= x1 && used < MAX_DECOR; x++)
                {
                    TerrainTypeSO t = _terrain.At(x, y);
                    if (t == null) continue;

                    byte fow = FowManager.Instance != null ? FowManager.Instance.GetFowState(x, y)
                                                           : FowManager.FOW_VISIBLE;
                    if (fow == 0) continue;   // 안 밝힌 땅에 풀이 떠 있으면 안개가 뚫린다

                    // ── 벽면 (M31-W2) — 고지대 남쪽 두 줄은 **서 있는 바위 벽**이다 ──
                    // 🔑 제 그림을 **자기 칸에** 그린다: 아래 칸에 그리면 그 칸은 걸을 수 있는
                    //    땅이라 "벽인데 지나간다"가 된다 (아래 칸으로 내려가는 것은 기단뿐).
                    if (_terrain.IsWallRow(x, y))
                    {
                        if (t.WallSprites != null && t.WallSprites.Length >= 6)
                        {
                            int row  = _terrain.IsWallRow(x, y - 1) ? 0 : 1;   // 아래도 벽 = 내가 윗줄
                            int side = _terrain.IsWallRow(x - 1, y)
                                     ? (_terrain.IsWallRow(x + 1, y) ? 1 : 2) : 0;
                            // 🔑 **Y 정렬이다** (Decal 아님): 벽은 바닥 데칼이 아니라 서 있는 물건이라,
                            //    남쪽에 선 주민은 벽 앞에·고지대 위에 선 주민은 벽 뒤에 그려져야 한다.
                            //    bias는 Node(0) — 벽 칸엔 노드가 못 서므로 같은 칸에서 다툴 상대가 없다.
                            DrawSurface(x, y, t.WallSprites[row * 3 + side], t.EdgeTint, fow, dim,
                                        WorldSort.Order(y, WorldSort.Node), ref edges);
                        }
                    }
                    // ── 기단 잡석 — 벽 **아래** 첫 저지대 칸. 굴러떨어진 돌이라 판정이 없다 ──
                    else if (_terrain.IsWallRow(x, y + 1))
                    {
                        TerrainTypeSO wall = _terrain.At(x, y + 1);
                        if (wall != null && wall.BaseSprites != null && wall.BaseSprites.Length >= 3)
                        {
                            int side = _terrain.IsWallRow(x - 1, y + 1)
                                     ? (_terrain.IsWallRow(x + 1, y + 1) ? 1 : 2) : 0;
                            DrawSurface(x, y, wall.BaseSprites[side], wall.EdgeTint, fow, dim,
                                        WorldSort.Decal, ref edges);
                        }
                    }

                    // ── 경계 그림 (M29-4차) — **낮은 쪽 타일에 이웃의 술을 얹는다** ──
                    // 색면 두 개가 계단으로 만나던 자리에 유기적 가장자리가 생긴다.
                    for (int d = 0; d < DIRS.Length && edges < MAX_EDGE; d++)
                    {
                        TerrainTypeSO nb = _terrain.At(x + DIRS[d].x, y + DIRS[d].y);
                        if (nb == null || nb == t) continue;

                        // 둘 중 **누구의 조각을 이 타일에 그리는가**: 물가는 자기 몸에(EdgeOnSelf),
                        // 잔디는 낮은 이웃 몸에. 규약이 하나였을 때 호수가 잔디 위로 번졌다.
                        // 🔴 자기 림(EdgeOnSelf)은 **우선순위를 안 본다**: 심층암반은 팔레트
                        //    폴백이라 Priority가 제일 낮은데, 그 이유로 제 테두리를 못 그리면
                        //    가장 깊은 곳만 계단으로 남는다 (2026-08-12 실측 — 두 규칙이 부딪힌 자리).
                        TerrainTypeSO owner = t.EdgeOnSelf ? t
                                            : (!nb.EdgeOnSelf && nb.Priority > t.Priority ? nb : null);
                        if (owner == null) continue;
                        if (owner.EdgeTiles == null || owner.EdgeTiles.Length < 4) continue;

                        DrawSurface(x, y, owner.EdgeTiles[d], owner.EdgeTint, fow, dim,
                                    WorldSort.Decal - 1, ref edges);   // 바닥 바로 위·장식 아래
                    }

                    if (t.DecorSprites == null || t.DecorSprites.Length == 0 || t.DecorPerTile <= 0f) continue;

                    // 🔴 **군집** (2026-08-12 재설계): 균일 난수로 뿌리면 장식이 아니라 **색종이**가
                    //    된다 (사용자 지적). 레퍼런스 맵은 빈 땅이 대부분이고 풀·바위가 **덩어리로**
                    //    모여 있다 — 그래서 밀도를 지형 값 그대로 쓰지 않고 **패치 밭**으로 깎는다.
                    //    밭이 옅은 곳은 0이 되어 맨땅이 남는다 (그 맨땅이 덩어리를 덩어리로 보이게 한다).
                    float field = PatchField(x, y);
                    float p = t.DecorPerTile * Mathf.Clamp01((field - 0.42f) / 0.58f);
                    if (p <= 0f) continue;

                    uint h = Hash(x, y);
                    if ((h & 0xFFFFu) / 65535f >= p) continue;
                    if (_tileTaken != null && _tileTaken(new Vector2Int(x, y))) continue;

                    // 한 덩어리는 **같은 것들**로 이뤄진다 (꽃밭·돌무더기) — 매 칸 다른 그림을
                    // 고르면 종류가 섞여 다시 색종이가 된다. 넷 중 셋은 그 패치의 대표 그림.
                    uint patchH = Hash(Mathf.FloorToInt(x / (float)PATCH_CELL) * 7919,
                                       Mathf.FloorToInt(y / (float)PATCH_CELL) * 6271);
                    uint pick = ((h >> 28) & 3u) == 0u ? (h >> 16) : (patchH >> 16);
                    Sprite sprite = t.DecorSprites[(int)(pick % (uint)t.DecorSprites.Length)];
                    if (sprite == null) continue;

                    SpriteRenderer sr = Take(_pool, used++);
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

            for (int i = used;  i < _pool.Count;     i++) _pool[i].enabled = false;
            for (int i = edges; i < _edgePool.Count; i++) _edgePool[i].enabled = false;
        }

        /// <summary>1칸짜리 표면 조각 하나 — 경계·벽면·기단이 같은 풀을 쓴다 (그림 크기가 같고
        /// `sortingOrder`만 다르다). 안개 속(fow 1)에서는 바닥과 **같은 배율**로 어두워진다.</summary>
        private void DrawSurface(int x, int y, Sprite sprite, Color tint, byte fow, float dim,
                                 int sortingOrder, ref int count)
        {
            if (sprite == null) return;
            SpriteRenderer sr = Take(_edgePool, count++);
            sr.transform.position = new Vector3(x, y, 0f);
            sr.transform.localScale = Vector3.one;
            sr.sprite = sprite;
            sr.color = fow == FowManager.FOW_VISIBLE
                     ? tint : new Color(tint.r * dim, tint.g * dim, tint.b * dim, tint.a);
            sr.sortingOrder = sortingOrder;
            sr.enabled = true;
        }

        private SpriteRenderer Take(List<SpriteRenderer> pool, int i)
        {
            while (pool.Count <= i)
            {
                var go = new GameObject("surface");
                go.transform.SetParent(transform, worldPositionStays: false);
                pool.Add(go.AddComponent<SpriteRenderer>());
            }
            return pool[i];
        }

        // 장식 덩어리의 크기 (타일) — 알고리즘 상수. 지형 노이즈(18·26)보다 작아야 한 지형 안에
        // 여러 덩어리가 들어간다. 7이면 화면에 대여섯 덩어리 = 레퍼런스의 리듬.
        private const int PATCH_CELL = 7;

        /// <summary>장식 덩어리 밭 (0~1) — 지형 노이즈와 **같은 값 노이즈**를 작은 격자로 뜬 것.
        /// 이 값이 낮은 곳은 맨땅으로 남는다: 빈 곳이 있어야 모인 곳이 모여 보인다.</summary>
        private float PatchField(int x, int y)
        {
            int gx = Mathf.FloorToInt((float)x / PATCH_CELL), gy = Mathf.FloorToInt((float)y / PATCH_CELL);
            float fx = (float)(x - gx * PATCH_CELL) / PATCH_CELL, fy = (float)(y - gy * PATCH_CELL) / PATCH_CELL;
            float sx = fx * fx * (3f - 2f * fx), sy = fy * fy * (3f - 2f * fy);
            float a = Corner(gx, gy),     b = Corner(gx + 1, gy);
            float c = Corner(gx, gy + 1), d = Corner(gx + 1, gy + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
        }

        private float Corner(int gx, int gy) => (Hash(gx * 31, gy * 17) & 0xFFFFu) / 65535f;

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
