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

        private readonly uint _seed;
        private readonly TerrainTypeSO[] _palette;   // Priority 내림차순 정렬본
        private readonly TerrainTypeSO _fallback;    // 안전지대·미배선용 (= Priority 최저)
        private readonly Dictionary<int, TerrainTypeSO> _cache = new Dictionary<int, TerrainTypeSO>(4096);
        private readonly int _safeRadius;
        private readonly Vector2Int _center;

        /// <summary>팔레트가 비었는가 — 미배선 판정 (전 타일 평지 = 오늘과 같은 판).</summary>
        public bool IsEmpty => _palette == null || _palette.Length == 0;

        /// <param name="seed">판 시드 (ADR-T-4) — 지형과 노드 배치가 **같은 시드**에서 나온다.</param>
        /// <param name="palette">지형 에셋들. **순서는 상관없다** — `Priority`로 정렬한다.
        /// 🔑 지형을 늘리는 것 = 이 배열에 에셋 하나 추가 · **코드 0줄** (게이트 `M26_T9`).</param>
        /// <param name="safeRadius">마을 중심 둘레 이 반경(체비쇼프)은 **반드시 폴백 지형**.
        /// 태어나자마자 물에 갇히는 판을 막는다 — 0이면 안전지대 없음.</param>
        public TerrainService(uint seed, Vector2Int villageCenter, int safeRadius, TerrainTypeSO[] palette)
        {
            _seed = seed;
            _center = villageCenter;
            _safeRadius = Mathf.Max(0, safeRadius);

            var list = new List<TerrainTypeSO>();
            if (palette != null) foreach (TerrainTypeSO t in palette) if (t != null) list.Add(t);
            list.Sort((a, b) => b.Priority.CompareTo(a.Priority));   // 높은 Priority 가 먼저
            _palette = list.ToArray();
            // 폴백 = 우선순위 최저 (= 평지). 없으면 null 이고, 그때는 전부 중립으로 답한다.
            _fallback = _palette.Length > 0 ? _palette[_palette.Length - 1] : null;
        }

        /// <summary>이 타일의 지형. 팔레트가 비면 null (미배선 = 오늘과 같은 판, 중립).</summary>
        public TerrainTypeSO At(int tileX, int tileY)
        {
            if (_palette.Length == 0) return null;
            int key = (tileX << 16) ^ (tileY & 0xFFFF);
            if (_cache.TryGetValue(key, out TerrainTypeSO hit)) return hit;
            TerrainTypeSO t = Compute(tileX, tileY);
            _cache[key] = t;
            return t;
        }

        public bool IsWalkable(int tileX, int tileY)
        {
            TerrainTypeSO t = At(tileX, tileY);
            return t == null || t.Walkable;
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

            // Priority 내림차순으로 훑어 **처음 걸리는 것**이 이긴다.
            // 🔑 코드에 지형 이름이 하나도 없다 — 밴드도 순서도 에셋이 갖는다 (게이트 M26_T9).
            foreach (TerrainTypeSO t in _palette)
            {
                float v = t.Field == TerrainNoiseField.Relief ? relief : wet;
                if (v >= t.AppearsAbove) return t;
            }
            return _fallback;
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
