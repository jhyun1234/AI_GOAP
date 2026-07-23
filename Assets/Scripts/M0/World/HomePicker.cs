using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 택지 선정 (M11-F, ADR-M11-5) — 개인 집이 설 자리를 정하는 유일한 결정자.
    /// 舊 구역(ZoneService) 배치를 집에 대해 대체한다: 앵커는 마을 기지(WorldConfig.BaseTile)로
    /// 고정이고, 밀집은 기존 집과의 최소 간격(BuildingSO.MinSpacingTiles)이 막는다.
    /// 전부 순수 정적 — EditMode 게이트 M11-T5 대상. 랜덤 금지 (동률은 (x,y) 사전순).
    /// </summary>
    public static class HomePicker
    {
        /// <summary>
        /// 택지 선정 (순수 함수 — 게이트 대상).
        /// 후보 = 마을 앵커 체비쇼프 반경 villageRadius 내 × 맵 경계 내 × 비점유 ×
        /// 기존 집 전부와 체비쇼프 거리 ≥ minSpacing.
        /// 점수 = -(앵커 거리) + outskirtsBias × (앵커 거리) → bias 0이면 안쪽(가까움) 선호,
        /// bias > 1이면 바깥 선호로 반전한다 (성격·직업 에셋 값이 취향을 만든다 — 코드 0줄 확장).
        /// 동률은 (x, y) 사전순 = 순회 순서상 먼저 찾은 것 (결정성).
        /// 후보 0 = false (마을 만원 — 보이는 소프트 상한, 구역 만원의 계승).
        /// </summary>
        public static bool PickHomesite(Func<int, int, bool> occupied, IReadOnlyList<Vector2Int> houses,
            Vector2Int villageAnchor, int villageRadius, int minSpacing, float outskirtsBias,
            int minX, int maxX, int minY, int maxY, out Vector2Int tile)
        {
            tile = default;
            if (occupied == null || villageRadius <= 0) return false;

            bool found = false;
            float bestScore = 0f;

            // x → y 오름차순 순회 = 사전순. 동률에 strict '>'만 쓰므로 먼저 찾은 쪽이 이긴다.
            int x0 = Mathf.Max(minX, villageAnchor.x - villageRadius);
            int x1 = Mathf.Min(maxX, villageAnchor.x + villageRadius);
            int y0 = Mathf.Max(minY, villageAnchor.y - villageRadius);
            int y1 = Mathf.Min(maxY, villageAnchor.y + villageRadius);

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    if (occupied(x, y)) continue;
                    if (!KeepsSpacing(houses, x, y, minSpacing)) continue;

                    int dist = Chebyshev(x - villageAnchor.x, y - villageAnchor.y);
                    float score = -dist + outskirtsBias * dist;
                    if (found && score <= bestScore) continue;

                    bestScore = score;
                    tile = new Vector2Int(x, y);
                    found = true;
                }
            }
            return found;
        }

        /// <summary>기존 집 전부와 체비쇼프 간격 minSpacing 이상인가 (순수 — 게이트 대상).
        /// 집이 없거나 minSpacing ≤ 0이면 항상 true = 중립 (M11 이전 동작).</summary>
        public static bool KeepsSpacing(IReadOnlyList<Vector2Int> houses, int x, int y, int minSpacing)
        {
            if (minSpacing <= 0 || houses == null) return true;
            for (int i = 0; i < houses.Count; i++)
                if (Chebyshev(x - houses[i].x, y - houses[i].y) < minSpacing) return false;
            return true;
        }

        /// <summary>택지 선호 가산치 = 성격 + 직업 (순수 — 둘 다 null이면 0 = 중립).</summary>
        public static float OutskirtsBias(PersonalitySO p, JobSO j)
            => (p != null ? p.HomeOutskirtsBias : 0f) + (j != null ? j.HomeOutskirtsBias : 0f);

        private static int Chebyshev(int dx, int dy) => Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
    }
}
