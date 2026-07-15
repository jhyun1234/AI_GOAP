using AIVillage.Core;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 맵 경계의 유일한 출처 (M3-F) — 러너 4곳(Explore/Consume/Wander/Build)에 복사돼 있던
    /// 클램프 규칙 통합. MapConfig 미활성(테스트 등)이면 舊 기본값(-50~49).
    /// 배열 변환은 JPSPathfinder와 동일 규칙 (tile + mapOffset).
    /// </summary>
    public static class MapBounds
    {
        // MapConfig 부재 시 폴백 — 舊 러너 4벌의 기본값과 동일 (알고리즘 상수)
        private const int FALLBACK_MIN = -50;
        private const int FALLBACK_MAX = 49;

        public static void Get(out int minX, out int maxX, out int minY, out int maxY)
        {
            MapConfig map = MapConfig.Active;
            if (map != null)
            {
                minX = -map.mapOffset;
                maxX = map.mapSize - map.mapOffset - 1;
                minY = -map.mapOffset;
                maxY = map.mapSize - map.mapOffset - 1;
            }
            else
            {
                minX = FALLBACK_MIN; maxX = FALLBACK_MAX;
                minY = FALLBACK_MIN; maxY = FALLBACK_MAX;
            }
        }

        /// <summary>경계 클램프 — 러너의 목표 타일 산출용.</summary>
        public static Vector2Int Clamp(int x, int y)
        {
            Get(out int minX, out int maxX, out int minY, out int maxY);
            return new Vector2Int(Mathf.Clamp(x, minX, maxX), Mathf.Clamp(y, minY, maxY));
        }

        /// <summary>타일 좌표 → Walkable 배열 인덱스 (JPS와 동일 규칙). 맵 밖이면 false — M3-C 통행 갱신용.</summary>
        public static bool ToArrayIndex(int tileX, int tileY, out int ax, out int ay)
        {
            Get(out int minX, out int maxX, out int minY, out int maxY);
            ax = tileX - minX;
            ay = tileY - minY;
            return tileX >= minX && tileX <= maxX && tileY >= minY && tileY <= maxY;
        }

        /// <summary>
        /// 반경 내 랜덤 목표 타일 (경계 클램프 + 통행 필터, M4-E — 집 타일을 목표로 뽑는 소음 제거).
        /// ① 랜덤 attempts회 → ② 실패 시 결정적 링 순회(반경 내 walkable이 있으면 반드시 찾음)
        /// → ③ 전부 막혀 있으면 중심 클램프 (결정적 종료 — 이후는 이동 실패 first-class가 처리).
        /// walkable=null이면 필터 없음 (기존 동작).
        /// </summary>
        public static Vector2Int PickWalkableNear(System.Func<int, int, bool> walkable,
            int cx, int cy, int radius, int attempts = 4)
        {
            int r = Mathf.Max(1, radius);

            if (walkable == null)
                return Clamp(cx + Random.Range(-r, r + 1), cy + Random.Range(-r, r + 1));

            for (int i = 0; i < attempts; i++)
            {
                Vector2Int t = Clamp(cx + Random.Range(-r, r + 1), cy + Random.Range(-r, r + 1));
                if (walkable(t.x, t.y)) return t;
            }

            // 링 폴백 — 랜덤이 불운해도 반경 내 통행 가능 타일이 있으면 반드시 반환
            Get(out int minX, out int maxX, out int minY, out int maxY);
            for (int ring = 1; ring <= r; ring++)
            {
                for (int dy = -ring; dy <= ring; dy++)
                {
                    for (int dx = -ring; dx <= ring; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != ring) continue; // 테두리만
                        int x = cx + dx, y = cy + dy;
                        if (x < minX || x > maxX || y < minY || y > maxY) continue;
                        if (walkable(x, y)) return new Vector2Int(x, y);
                    }
                }
            }
            return Clamp(cx, cy);
        }
    }
}
