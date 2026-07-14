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
    }
}
