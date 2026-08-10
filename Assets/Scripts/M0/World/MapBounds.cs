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

        /// <summary>
        /// 중심 **옆에** 서는 타일 — 8이웃 중 통행 가능하고 (fromX,fromY)에 가장 가까운 칸.
        ///
        /// 🔴 계기 (2026-08-10 사용자 Play): 벌목·채광하는 주민이 나무·바위 **위에 겹쳐** 서 있었다.
        /// 채집 러너가 목적지를 노드 타일 그 자체로 줬기 때문이다. 나무를 안고 도끼질하는 그림은
        /// 누가 봐도 이상하다 — 일하는 자리는 대상 **곁**이다.
        ///
        /// PickWalkableNear 를 못 쓰는 이유: 그쪽은 랜덤 시도가 (0,0)을 뽑을 수 있어 중심에
        /// 그대로 설 수 있고, 반경 안 아무 데나라 반대편으로 돌아가기도 한다. 여기서 필요한 것은
        /// **가장 가까운 옆칸** 하나다 (오는 길에 자연스럽게 멈춰야 한다).
        ///
        /// 전부 막혀 있으면 중심을 돌려준다 — 배선 전과 같은 동작(중립)이라 갇히지 않는다.
        /// </summary>
        public static Vector2Int PickWalkableAdjacent(System.Func<int, int, bool> walkable,
            int cx, int cy, int fromX, int fromY)
        {
            Get(out int minX, out int maxX, out int minY, out int maxY);
            var best = new Vector2Int(cx, cy);
            int bestDist = int.MaxValue;

            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;          // 중심은 후보가 아니다 (겹쳐 서기 금지)
                    int x = cx + dx, y = cy + dy;
                    if (x < minX || x > maxX || y < minY || y > maxY) continue;
                    if (walkable != null && !walkable(x, y)) continue;
                    // 맨해튼 + 대각 감점: 같은 거리면 직교 칸이 이긴다 (옆·앞이 대각보다 읽기 쉽다).
                    int d = Mathf.Abs(x - fromX) + Mathf.Abs(y - fromY);
                    if (dx != 0 && dy != 0) d = d * 2 + 1;
                    if (d >= bestDist) continue;
                    bestDist = d;
                    best = new Vector2Int(x, y);
                }
            return best;
        }
    }
}
