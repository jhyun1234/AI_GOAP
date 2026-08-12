using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 지형 지도 (M26-1차 W2) — 타일 지형은 **좌표의 함수**다 (ADR-T-5): `f(판 시드, x, y)`.
    ///
    /// 🔑 **배열을 저장하지 않는다.** 10,000칸을 직렬화하면 세이브/로드에서 어긋날 자리가 생기고,
    /// 순수 함수라야 게이트가 전수로 검사할 수 있다. 캐시는 **파생**이고 원본은 함수다.
    /// 🔑 **시드가 같으면 지도가 같다** — `ADR-M10R-2`(같은 시드 = 같은 판)가 **맵까지** 덮는 지점.
    ///
    /// 생성 규칙: 값 노이즈 2장(습도·고도)의 조합. 노이즈는 좌표 해시를 격자에서 보간한 것으로,
    /// **`PickVariantIndex`가 이미 쓰는 좌표 해시 기법의 확장**이다 (나무 3종이 그렇게 갈린다) —
    /// 새 수학을 들여오지 않았다.
    /// </summary>
    public sealed class TerrainService
    {
        // 노이즈 격자 크기 (타일) — 클수록 지형 덩어리가 커진다.
        // 알고리즘 상수이지 게임 수치가 아니다 (지형의 성질은 전부 에셋이 정한다).
        private const int PLAIN_CELL = 18;   // 습도 — 호수·늪의 크기
        private const int RELIEF_CELL = 26;  // 고도 — 절벽 능선의 크기
        // 바이옴 — 지역의 크기 (M29 W1). 패치가 아니라 지방(地方).
        // 🔑 64 → 48 (2026-08-12 실측): 200 맵에 64는 축당 3칸뿐이라 **가장 좁은 바이옴이
        //    판마다 사라졌다** (200시드 중 심층산악 없음 1건 — 그 판엔 은·다이아가 전무).
        //    48이면 축당 4칸이 넘어 200시드 결손 0. 지역 크기는 여전히 고도 패치(26)의 두 배다.
        private const int BIOME_CELL = 48;
        /// <summary>벽 줄 수 (M31) — 고지대 남쪽 가장자리에서 이 줄까지가 벽면이다.
        /// 🔑 에셋 값이 아니다: 벽 줄 수는 **팩 아트가 정한 그림 사실**(벽 블록의 높이)이지
        /// 플레이어가 밸런스 패치로 돌릴 수가 아니다 (ADR-M0-2의 "알고리즘 상수" 쪽).
        /// 🔴 2 → 4 (2026-08-12 사용자 Play 지적 *"낮아보여"*): 팩의 벽 블록은 두 종류인데
        /// 낮은 쪽(둥근 바위 2줄)을 골랐더니 높이가 안 읽혔다. 4줄짜리 층암 블록으로 바꿨다.
        /// 이 수를 바꾸면 **에셋의 `WallSprites` 장수(줄×3)도 같이** 바뀐다 (OnValidate가 감시).</summary>
        public const int WALL_ROWS = 4;

        private readonly uint _seed;
        private readonly BiomeSO[] _biomes;            // 미배선이면 길이 0 (= 舊 단일 팔레트 경로)
        private readonly int[] _biomeOrder;            // Priority 내림차순 인덱스 (배열 순서는 안 흔든다 — 0번 = 온대)
        private readonly TerrainTypeSO[][] _palettes;  // 바이옴별 Priority 내림차순 정렬본 (길이 ≥ 1)
        private readonly TerrainTypeSO _fallback;      // 안전지대·미배선용 (= 온대 Priority 최저)
        private readonly Dictionary<int, TerrainTypeSO> _cache = new Dictionary<int, TerrainTypeSO>(4096);
        private readonly int _safeRadius;
        private readonly Vector2Int _center;
        private readonly float _mapHalf;

        /// <summary>팔레트가 비었는가 — 미배선 판정 (전 타일 평지 = 오늘과 같은 판).</summary>
        public bool IsEmpty => _fallback == null;

        /// <summary>이 지도에 깔린 지형들의 최대 `NodeDensityMult` — 밀도를 상대값으로 읽는 분모.
        /// 🔑 바이옴 팔레트의 **합집합**을 본다: 분모가 온대만 보면 정글 2.0이 "항상 채택"으로 뭉갠다.</summary>
        public float MaxNodeDensityMult
        {
            get
            {
                float max = 1f;
                foreach (TerrainTypeSO[] p in _palettes)
                    foreach (TerrainTypeSO t in p)
                        max = Mathf.Max(max, t.NodeDensityMult);
                return max;
            }
        }

        /// <param name="seed">판 시드 (ADR-T-4) — 지형과 노드 배치가 **같은 시드**에서 나온다.</param>
        /// <param name="palette">지형 에셋들. **순서는 상관없다** — `Priority`로 정렬한다.
        /// 🔑 지형을 늘리는 것 = 이 배열에 에셋 하나 추가 · **코드 0줄** (게이트 `M26_T9`).</param>
        /// <param name="safeRadius">마을 중심 둘레 이 반경(체비쇼프)은 **반드시 폴백 지형**.
        /// 태어나자마자 물에 갇히는 판을 막는다 — 0이면 안전지대 없음.</param>
        public TerrainService(uint seed, Vector2Int villageCenter, int safeRadius, TerrainTypeSO[] palette)
            : this(seed, villageCenter, safeRadius, palette, null, 0f) { }

        /// <param name="biomes">바이옴 목록 (M29 W1). **비우면 `palette` 한 벌만 쓰는 舊 경로**
        /// — 지형 결과가 개조 전과 비트 동일하다 (`ADR-M29-3` 중립 불변식).
        /// 배선하면 `palette`는 안 쓰이고 각 바이옴이 자기 팔레트를 가진다. 0번 = 온대(폴백).</param>
        /// <param name="mapHalf">맵 반폭(타일) — 기지 거리비의 분모. 0이면 바이옴 문턱이 전부
        /// 충족된 것으로 본다 (거리비를 못 재는 판에서 지역을 지우지는 않는다).</param>
        public TerrainService(uint seed, Vector2Int villageCenter, int safeRadius,
                              TerrainTypeSO[] palette, BiomeSO[] biomes, float mapHalf)
        {
            _seed = seed;
            _center = villageCenter;
            _safeRadius = Mathf.Max(0, safeRadius);
            _mapHalf = mapHalf;

            var validBiomes = new List<BiomeSO>();
            if (biomes != null)
                foreach (BiomeSO b in biomes)
                    if (b != null && b.Palette != null && b.Palette.Length > 0) validBiomes.Add(b);
            _biomes = validBiomes.ToArray();

            if (_biomes.Length > 0)
            {
                // 후보 순회 순서만 Priority 로 정한다 — **배열 순서는 안 흔든다**: 0번 = 온대가
                // 폴백·안전지대·접기 대상이라 인덱스가 밀리면 그 셋이 딴 바이옴으로 바뀐다.
                _biomeOrder = new int[_biomes.Length];
                for (int i = 0; i < _biomes.Length; i++) _biomeOrder[i] = i;
                System.Array.Sort(_biomeOrder,
                    (a, b) => _biomes[b].Priority.CompareTo(_biomes[a].Priority));

                _palettes = new TerrainTypeSO[_biomes.Length][];
                for (int i = 0; i < _biomes.Length; i++)
                {
                    _palettes[i] = Sorted(_biomes[i].Palette);
                    // 온대(0번)는 언제나 설 수 있어야 폴백이 성립한다 — 0번 아닌 문턱 0은
                    // "안전지대가 둘"이라는 뜻이라 온대 보장이 흐려진다 (§W1 OnValidate 규약).
                    if (i > 0 && _biomes[i].MinBaseDistFrac <= 0f)
                        Debug.LogWarning($"[TerrainService] 바이옴 {_biomes[i].name}: MinBaseDistFrac 0 — " +
                                         "마을 옆에도 설 수 있습니다 (온대 보장이 0번에만 있습니다).", _biomes[i]);
                    // 0번(온대)이 밴드를 가지면 폴백이 폴백이 아니게 된다 — 아무도 안 걸리는
                    // 노이즈 값에서 후보가 0개가 되어 접기의 목적지가 사라진다.
                    if (i == 0 && _biomes[0].AppearsAbove > 0f)
                        Debug.LogWarning($"[TerrainService] 바이옴 {_biomes[0].name}(0번)의 AppearsAbove가 " +
                                         $"{_biomes[0].AppearsAbove} — 0이어야 폴백으로 삽니다.", _biomes[0]);
                }
            }
            else
            {
                _palettes = new[] { Sorted(palette) };
            }

            // 폴백 = 온대 팔레트의 우선순위 최저 (= 평지). 없으면 null 이고, 그때는 전부 중립으로 답한다.
            _fallback = _palettes[0].Length > 0 ? _palettes[0][_palettes[0].Length - 1] : null;
        }

        private static TerrainTypeSO[] Sorted(TerrainTypeSO[] palette)
        {
            var list = new List<TerrainTypeSO>();
            if (palette != null) foreach (TerrainTypeSO t in palette) if (t != null) list.Add(t);
            list.Sort((a, b) => b.Priority.CompareTo(a.Priority));   // 높은 Priority 가 먼저
            return list.ToArray();
        }

        /// <summary>이 타일의 바이옴 (M29 W1). 미배선이면 null — 게이트·로그용 조회 창구다.
        /// 🔴 지형 판정은 `At`이 유일한 출처다 (ADR-M29-1: 바이옴은 팔레트 출처일 뿐).</summary>
        public BiomeSO BiomeAt(int tileX, int tileY)
            => _biomes.Length == 0 ? null : _biomes[BiomeIndex(tileX, tileY)];

        /// <summary>이 타일의 지형. 팔레트가 비면 null (미배선 = 오늘과 같은 판, 중립).</summary>
        public TerrainTypeSO At(int tileX, int tileY)
        {
            if (_fallback == null) return null;
            int key = (tileX << 16) ^ (tileY & 0xFFFF);
            if (_cache.TryGetValue(key, out TerrainTypeSO hit)) return hit;
            TerrainTypeSO t = Compute(tileX, tileY);
            _cache[key] = t;
            return t;
        }

        /// <summary>이 칸이 절벽 '벽면'인가 (M31 W1) — 남쪽(y 감소) `WALL_ROWS` 안에 더 낮은 땅이
        /// 있으면 그렇다.
        ///
        /// 🔑 **통행 불가는 지형이 아니라 가장자리가 만든다** (ADR-M31-2). 물은 어디서나 못 지나고
        /// (`Walkable false`), 절벽은 위를 걸을 수 있되 **남쪽 두 줄만** 벽이다 — 그래서 북·동·서로
        /// 올라가는 길이 남아 고지대가 섬이 되지 않는다.
        /// 🔑 얇은 띠(세로 런이 `WALL_ROWS` 미만)는 통째로 벽이 된다 — 그게 맞다 (ADR-M31-4):
        /// 예외를 하나 만들면 벽 두께가 좌표마다 달라져 렌더가 두 규칙을 갖는다.</summary>
        public bool IsWallRow(int tileX, int tileY)
        {
            TerrainTypeSO t = At(tileX, tileY);
            if (t == null || t.HeightLevel <= 0) return false;
            for (int k = 1; k <= WALL_ROWS; k++)
            {
                TerrainTypeSO below = At(tileX, tileY - k);   // 남쪽 = y 감소 (ADR-M0-9)
                if (below == null || below.HeightLevel < t.HeightLevel) return true;
            }
            return false;
        }

        public bool IsWalkable(int tileX, int tileY)
        {
            TerrainTypeSO t = At(tileX, tileY);
            return (t == null || t.Walkable) && !IsWallRow(tileX, tileY);
        }

        /// <summary>진입 비용 배수 (A*가 읽는다). 미배선이면 1 = 균일 = JPS와 같은 답.</summary>
        public float EnterCost(int tileX, int tileY)
        {
            TerrainTypeSO t = At(tileX, tileY);
            return t != null ? Mathf.Max(1f, t.EnterCost) : 1f;
        }

        /// <summary>이동 속도 배수 (주민·위협이 **같이** 읽는다 — ADR-T-3).</summary>
        public float SpeedMult(int tileX, int tileY)
        {
            TerrainTypeSO t = At(tileX, tileY);
            return t != null ? Mathf.Clamp(t.MoveSpeedMult, 0.1f, 1f) : 1f;
        }

        /// <summary>바닥 색 (W5). 미배선이면 null — 렌더러가 舊 풀색으로 떨어진다 (중립).</summary>
        public Color? GroundColor(int tileX, int tileY)
        {
            TerrainTypeSO t = At(tileX, tileY);
            return t != null ? t.GroundColor : (Color?)null;
        }

        // ── 생성 ──────────────────────────────────────────────────────────────

        private TerrainTypeSO Compute(int x, int y)
        {
            // 마을 둘레는 무조건 폴백(평지) — "태어나 보니 갇혀 있다"를 구조적으로 막는다.
            if (_safeRadius > 0
                && Mathf.Max(Mathf.Abs(x - _center.x), Mathf.Abs(y - _center.y)) <= _safeRadius)
                return _fallback;

            float wet    = Noise(x, y, PLAIN_CELL,  0x9E3779B9u);
            float relief = Noise(x, y, RELIEF_CELL, 0x85EBCA6Bu);

            // 🔑 M29 W1: **바뀐 곳은 여기 한 줄** — 어느 팔레트를 훑을지 (ADR-M29-1).
            //    습도·고도 밴드는 그대로 산다: 바이옴은 지형을 바꾸는 층이 아니라 갈아끼우는 층이다.
            TerrainTypeSO[] palette = _palettes[BiomeIndex(x, y)];

            // Priority 내림차순으로 훑어 **처음 걸리는 것**이 이긴다.
            // 🔑 코드에 지형 이름이 하나도 없다 — 밴드도 순서도 에셋이 갖는다 (게이트 M26_T9).
            foreach (TerrainTypeSO t in palette)
            {
                float v = t.Field == TerrainNoiseField.Relief ? relief : wet;
                if (v >= t.AppearsAbove) return t;
            }
            return palette.Length > 0 ? palette[palette.Length - 1] : _fallback;
        }

        /// <summary>이 타일이 어느 바이옴에 속하는가 (§2). 바이옴이 0~1개면 언제나 0 = 舊 경로.</summary>
        private int BiomeIndex(int x, int y)
        {
            if (_biomes.Length <= 1) return 0;

            // 🔴 **개정 2026-08-12 (사용자 Play 지적)**: 舊 `floor(v × 바이옴수)`는 노이즈가
            //    균등하다고 가정했다. 실측은 가운데로 쏠려 있어(0.1 미만 2.3% · 0.9 초과 2.8%)
            //    **양 끝 칸이 굶었다** — 온대 27%(숲은 5%)로 밀렸고 심층산악은 시드에 따라
            //    아예 없었다. 지형이 이미 쓰는 밴드 문법(AppearsAbove + Priority)으로 바꾼다:
            //    넓이가 코드의 나눗셈이 아니라 **에셋의 문턱**이 되어, 바이옴을 늘려도 남의
            //    넓이가 저절로 줄지 않는다.
            float v = Noise(x, y, BIOME_CELL, 0x27D4EB2Fu);
            int idx = 0;
            foreach (int i in _biomeOrder)
                if (v >= _biomes[i].AppearsAbove) { idx = i; break; }

            // 🔑 혼합 배치가 이 한 줄이다 (사용자 결정): 가까이는 **비적격 후보가 온대로 접히고**,
            //    바깥은 노이즈가 고른 대로 선다. 적격 목록을 다시 뽑지 않으므로 문턱 반지름에서
            //    지역이 통째로 재배열되는 링 아티팩트가 없다 (§W1 오해 위험 ①).
            float distFrac = _mapHalf > 0f
                ? Mathf.Max(Mathf.Abs(x - _center.x), Mathf.Abs(y - _center.y)) / _mapHalf
                : 1f;
            return distFrac >= _biomes[idx].MinBaseDistFrac ? idx : 0;
        }

        /// <summary>값 노이즈 — 격자 모서리의 해시를 부드럽게 섞는다. 0~1.
        /// 같은 (시드, 좌표)면 언제나 같은 값이다 (ADR-T-5·ADR-M10R-2).</summary>
        private float Noise(int x, int y, int cell, uint salt)
        {
            int gx = Mathf.FloorToInt((float)x / cell), gy = Mathf.FloorToInt((float)y / cell);
            float fx = (float)(x - gx * cell) / cell, fy = (float)(y - gy * cell) / cell;
            float sx = fx * fx * (3f - 2f * fx), sy = fy * fy * (3f - 2f * fy); // smoothstep
            float a = Hash01(gx, gy, salt),         b = Hash01(gx + 1, gy, salt);
            float c = Hash01(gx, gy + 1, salt),     d = Hash01(gx + 1, gy + 1, salt);
            return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
        }

        private float Hash01(int gx, int gy, uint salt)
        {
            unchecked
            {
                uint h = _seed ^ salt;
                h ^= (uint)gx * 0x9E3779B9u; h = (h ^ (h >> 15)) * 0x2545F491u;
                h ^= (uint)gy * 0x85EBCA6Bu; h = (h ^ (h >> 13)) * 0xC2B2AE35u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }
    }
}
