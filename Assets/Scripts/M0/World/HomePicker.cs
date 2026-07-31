using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 택지 선정 (M11-F → M11-J 개선, ADR-M11-5) — 개인 집이 설 자리를 정하는 유일한 결정자.
    /// 舊 구역(ZoneService) 배치를 집에 대해 대체한다: 앵커는 마을 기지(WorldConfig.BaseTile)로
    /// 고정이고, 밀집은 기존 집과의 최소 간격(BuildingSO.MinSpacingTiles)이 막는다.
    ///
    /// 성격 취향 = **선호 거리 대역**이다 (M11-J 개선). 舊 "점수 배율" 방식은 argmax가 극단
    /// (중심 or 구석)만 골라 6종 성격이 2종으로 뭉개지고 사전순 동률이 집을 일렬로 세웠다
    /// (2026-07-23 중간 리뷰 ② 관측). 이제 성격은 앵커로부터의 선호 거리를 정하고(온순 1·방랑벽 9
    /// 등 서로 다른 동심원), 그 대역 안에서 **주민 신원 기반 결정적 흩뿌리기**로 자리를 고른다 —
    /// 겉보기엔 산개하나 같은 주민은 같은 자리(세이브/로드·게이트 재현성 유지, 랜덤 아님).
    /// 전부 순수 정적 — EditMode 게이트 M11-T5 대상.
    /// </summary>
    public static class HomePicker
    {
        // 선호 거리 대역 폭 (알고리즘 상수) — 선호 거리 ±BAND 링을 후보로 삼는다.
        private const int BAND = 1;

        /// <summary>
        /// 택지 선정 (순수 함수 — 게이트 대상). 후보 = 마을 앵커 체비쇼프 반경 villageRadius 내 ×
        /// 맵 경계 내 × 비점유 × 기존 집 전부와 체비쇼프 거리 ≥ minSpacing.
        ///
        /// preferredDist ≤ 0: 중립 — 앵커 최근접(舊 동작), 동률은 (x,y) 사전순 (성격 없는 주민).
        /// preferredDist > 0: 그 거리 ±BAND 대역의 후보 중 seed로 하나를 고른다(대역 산개).
        ///   대역이 비면(만원·간격) 선호 거리에 가장 가까운 자리로 폴백 (선호는 지키되 만원 회피).
        /// 후보 0 = false (마을 만원 — 보이는 소프트 상한, 구역 만원의 계승).
        /// </summary>
        public static bool PickHomesite(Func<int, int, bool> occupied, IReadOnlyList<Vector2Int> houses,
            Vector2Int villageAnchor, int villageRadius, int minSpacing, float preferredDist, int seed,
            int minX, int maxX, int minY, int maxY, out Vector2Int tile)
        {
            tile = default;
            if (occupied == null || villageRadius <= 0) return false;

            int x0 = Mathf.Max(minX, villageAnchor.x - villageRadius);
            int x1 = Mathf.Min(maxX, villageAnchor.x + villageRadius);
            int y0 = Mathf.Max(minY, villageAnchor.y - villageRadius);
            int y1 = Mathf.Min(maxY, villageAnchor.y + villageRadius);

            // 중립(선호 없음): 앵커 최근접 사전순 — 舊 동작 보존 (성격 미주입 주민).
            if (preferredDist <= 0f)
            {
                bool foundNear = false;
                int bestDist = int.MaxValue;
                for (int x = x0; x <= x1; x++)
                    for (int y = y0; y <= y1; y++)
                    {
                        if (occupied(x, y) || !KeepsSpacing(houses, x, y, minSpacing)) continue;
                        int d = Chebyshev(x - villageAnchor.x, y - villageAnchor.y);
                        if (foundNear && d >= bestDist) continue; // strict '<' = 사전순 동률
                        bestDist = d; tile = new Vector2Int(x, y); foundNear = true;
                    }
                return foundNear;
            }

            // 선호 거리 대역: |거리 - 선호| ≤ BAND 인 후보를 사전순으로 모아 seed로 선택.
            int pref = Mathf.RoundToInt(preferredDist);
            var band = new List<Vector2Int>(32);
            int fallbackGap = int.MaxValue;
            Vector2Int fallback = default;
            bool hasFallback = false;

            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                {
                    if (occupied(x, y) || !KeepsSpacing(houses, x, y, minSpacing)) continue;
                    int d = Chebyshev(x - villageAnchor.x, y - villageAnchor.y);
                    int gap = Mathf.Abs(d - pref);
                    if (gap <= BAND) band.Add(new Vector2Int(x, y));
                    if (gap < fallbackGap) { fallbackGap = gap; fallback = new Vector2Int(x, y); hasFallback = true; }
                }

            if (band.Count > 0)
            {
                // seed로 대역 안에서 결정적 선택 — 후보 목록이 사전순이라 재현 가능. 음수 seed 방어.
                int idx = (int)((uint)seed % (uint)band.Count);
                tile = band[idx];
                return true;
            }
            if (hasFallback) { tile = fallback; return true; } // 대역 만원 → 선호 최근접
            return false;
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

        /// <summary>
        /// 선호 거리 **비율**(0~1, M11-K) = 성격 + 직업 + 성향 편향 (순수 — 게이트 M12-T7).
        /// 전부 null이면 0 = 중립·최근접 (기존 동작).
        ///
        /// ④대상의 첫 소비처 (M12-E) — 모험은 외딴집, 사교는 이웃 곁으로 민다.
        /// 상한 0.95는 M11-K 대역 규약(비율 0.1~0.95)의 계승이고, 하한 0은
        /// "최근접"이라는 기존 의미 그대로다 — 사교가 아주 높으면 자연히 여기로 수렴한다.
        /// ⚠️ 절대 거리로 바꾸는 것은 WorldConfig.PreferredHomeDist의 몫 (맵-비례, M11-K).
        /// </summary>
        /// <summary>호환 진입점 (게이트·구형 호출 전용 — 벡터 = 성격 원본, 편차 없음).</summary>
        public static float PreferredDist(PersonalitySO p, JobSO j, TraitRulesSO rules = null)
            => PreferredDist(p, j, p != null ? p.Traits : null, rules);

        public static float PreferredDist(PersonalitySO p, JobSO j, TraitValue[] traits,
                                          TraitRulesSO rules = null)
        {
            float baseFrac = (p != null ? p.HomePreferredDist : 0f) + (j != null ? j.HomePreferredDist : 0f);
            if (rules == null || p == null) return baseFrac; // 미배선 = 기존 경로 (중립 불변식)

            // 벡터는 인자(M14-W3 개체 편차 포함 MyTraits) — 같은 성격도 조금씩 다른 자리에 앉는다
            return Mathf.Clamp(TraitVector.Threshold(traits, rules.HomeDistanceBias, baseFrac), 0f, 0.95f);
        }

        /// <summary>주민 신원 → 안정적 seed (순수 — .NET string.GetHashCode는 프로세스마다 달라
        /// 재현 불가라 FNV-1a로 직접 계산한다). 같은 주민 = 같은 seed = 같은 흩뿌리기 결과.</summary>
        public static int StableSeed(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            uint h = 2166136261u;
            for (int i = 0; i < id.Length; i++) { h ^= id[i]; h *= 16777619u; }
            return (int)(h & 0x7fffffff);
        }

        private static int Chebyshev(int dx, int dy) => Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
    }
}
