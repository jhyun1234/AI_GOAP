using System.Collections.Generic;
using AIVillage.M0;
using NUnit.Framework;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M11-F/J 택지 게이트 (M11-T5) — HomePicker의 배치 (ADR-M11-5).
    /// 성격 취향 = **선호 거리 대역**(M11-J 개선): 서로 다른 성격은 서로 다른 동심원,
    /// 같은 대역 안에서는 주민 신원(seed)으로 흩어진다. 결정성(같은 입력 = 같은 출력)은 유지 —
    /// seed가 고정이면 타일도 고정이다(랜덤 아님). 간격·만원·경계는 舊 규약 그대로.
    /// </summary>
    public class M11_HomesiteGates
    {
        private static readonly Vector2Int Anchor = new Vector2Int(0, 0);

        private static System.Func<int, int, bool> Empty => (x, y) => false;

        private static bool Pick(System.Func<int, int, bool> occupied, IReadOnlyList<Vector2Int> houses,
            int radius, int minSpacing, float preferred, int seed, out Vector2Int tile)
            => HomePicker.PickHomesite(occupied, houses, Anchor, radius, minSpacing, preferred, seed,
                                       -20, 20, -20, 20, out tile);

        private static int Dist(Vector2Int t) => Mathf.Max(Mathf.Abs(t.x), Mathf.Abs(t.y));

        // ── 간격 ──────────────────────────────────────────────────────────────

        [Test]
        public void M11_T5_PickHomesite_RejectsTilesWithinMinSpacing()
        {
            var houses = new List<Vector2Int> { new Vector2Int(0, 0) };
            Assert.IsTrue(Pick(Empty, houses, 5, 3, 0f, 0, out Vector2Int tile));
            Assert.GreaterOrEqual(Dist(tile), 3, $"기존 집 (0,0)과 체비쇼프 3 이상 — 실제 {tile}");
        }

        [Test]
        public void M11_T5_KeepsSpacing_ZeroSpacingIsNeutral()
        {
            var houses = new List<Vector2Int> { new Vector2Int(0, 0) };
            Assert.IsTrue(HomePicker.KeepsSpacing(houses, 0, 0, 0), "간격 0 = 규칙 없음");
            Assert.IsTrue(HomePicker.KeepsSpacing(null, 0, 0, 3), "집 목록 없음 = 항상 통과");
            Assert.IsFalse(HomePicker.KeepsSpacing(houses, 2, 0, 3), "간격 2 < 3 = 배제");
            Assert.IsTrue(HomePicker.KeepsSpacing(houses, 3, 0, 3), "간격 3 = 경계 포함");
        }

        // ── 중립(선호 0) = 최근접 사전순 (舊 동작 보존) ──────────────────────────

        [Test]
        public void M11_T5_Neutral_PicksNearestLexicographic()
        {
            // 앵커 점유 + 반경 1 → 남은 8개 후보 전부 거리 1 = 동률. 사전순 최소 (-1,-1).
            Assert.IsTrue(Pick((x, y) => x == 0 && y == 0, new List<Vector2Int>(), 1, 0, 0f, 0, out Vector2Int t));
            Assert.AreEqual(new Vector2Int(-1, -1), t, "선호 0 = 최근접 + 동률 사전순");
        }

        // ── 성격 = 선호 거리 대역 (핵심 개선) ────────────────────────────────────

        [Test]
        public void M11_T5_PreferredDistance_LandsInBand()
        {
            // 선호 5 → 거리 4~6 대역 안에 앉는다 (BAND ±1). 여러 seed 전부 대역 안이어야.
            for (int seed = 0; seed < 20; seed++)
            {
                Assert.IsTrue(Pick(Empty, new List<Vector2Int>(), 10, 0, 5f, seed, out Vector2Int t));
                int d = Dist(t);
                Assert.IsTrue(d >= 4 && d <= 6, $"선호 5의 ±1 대역(4~6) 밖 — seed {seed} → {t} (거리 {d})");
            }
        }

        [Test]
        public void M11_T5_PreferredDistance_DifferentPersonalitiesDifferentRings()
        {
            // 舊 결함(6종이 2종으로 뭉갬)의 해소 증명: 온순 1·농사꾼 3·방랑벽 9가 서로 다른 링.
            Assert.IsTrue(Pick(Empty, new List<Vector2Int>(), 12, 0, 1f, 7, out Vector2Int docile));
            Assert.IsTrue(Pick(Empty, new List<Vector2Int>(), 12, 0, 3f, 7, out Vector2Int farmer));
            Assert.IsTrue(Pick(Empty, new List<Vector2Int>(), 12, 0, 9f, 7, out Vector2Int wanderer));
            Assert.LessOrEqual(Dist(docile), 2, "온순은 앵커 곁");
            Assert.IsTrue(Dist(farmer) >= 2 && Dist(farmer) <= 4, "농사꾼은 중간 링");
            Assert.GreaterOrEqual(Dist(wanderer), 8, "방랑벽은 외곽");
            Assert.AreNotEqual(docile, wanderer, "성격이 다르면 자리가 다르다 (舊 뭉갬 해소)");
        }

        [Test]
        public void M11_T5_Scatter_DifferentSeedsSpread()
        {
            // 같은 선호 거리라도 주민(seed)이 다르면 자리가 흩어진다 (일렬 정렬 해소).
            var seen = new HashSet<Vector2Int>();
            for (int seed = 0; seed < 12; seed++)
            {
                Assert.IsTrue(Pick(Empty, new List<Vector2Int>(), 10, 0, 6f, seed, out Vector2Int t));
                seen.Add(t);
            }
            Assert.Greater(seen.Count, 1, "seed가 다르면 최소한 두 자리 이상으로 흩어져야 (산개)");
        }

        [Test]
        public void M11_T5_Deterministic_SameSeedSameTile()
        {
            var houses = new List<Vector2Int> { new Vector2Int(2, 2), new Vector2Int(-3, 1) };
            Assert.IsTrue(Pick(Empty, houses, 8, 3, 5f, 12345, out Vector2Int a));
            Assert.IsTrue(Pick(Empty, houses, 8, 3, 5f, 12345, out Vector2Int b));
            Assert.AreEqual(a, b, "같은 seed 2회 = 같은 출력 (랜덤 아님 — ADR-M11-5 재현성)");
        }

        [Test]
        public void M11_T5_StableSeed_IsProcessIndependent()
        {
            // FNV-1a 고정값 — string.GetHashCode(프로세스마다 다름) 대신 써야 세이브/로드 재현.
            Assert.AreEqual(HomePicker.StableSeed("M0_Villager_A"), HomePicker.StableSeed("M0_Villager_A"));
            Assert.AreNotEqual(HomePicker.StableSeed("M0_Villager_A"), HomePicker.StableSeed("M0_Villager_B"));
            Assert.AreEqual(0, HomePicker.StableSeed(null), "빈 id = 0");
            Assert.GreaterOrEqual(HomePicker.StableSeed("x"), 0, "음수 아님 (mod 방어)");
        }

        [Test]
        public void M11_T5_PreferredDist_NullsAreNeutral()
            => Assert.AreEqual(0f, HomePicker.PreferredDist(null, null), "성격·직업 없음 = 중립 0");

        // ── 대역 만원 → 선호 최근접 폴백 ─────────────────────────────────────────

        [Test]
        public void M11_T5_BandFull_FallsBackToNearestPreferred()
        {
            // 선호 5인데 거리 4~6 전부 점유 → 실패 아님, 선호에 가장 가까운(거리 3 or 7) 자리로 폴백.
            System.Func<int, int, bool> band456 = (x, y) =>
            {
                int d = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                return d >= 4 && d <= 6;
            };
            Assert.IsTrue(Pick(band456, new List<Vector2Int>(), 10, 0, 5f, 0, out Vector2Int t),
                          "대역 만원이어도 폴백으로 성립");
            int d = Dist(t);
            Assert.IsTrue(d == 3 || d == 7, $"선호 5 최근접 폴백 = 거리 3 또는 7 — 실제 {t}");
        }

        // ── 만원·경계 ────────────────────────────────────────────────────────────

        [Test]
        public void M11_T5_FullVillageFails()
        {
            Assert.IsFalse(Pick((x, y) => true, new List<Vector2Int>(), 2, 3, 0f, 0, out Vector2Int _),
                           "전 영역 점유 = false");
            Assert.IsFalse(Pick(Empty, new List<Vector2Int> { new Vector2Int(0, 0) }, 2, 5, 0f, 0, out Vector2Int _),
                           "간격 요구가 반경을 덮으면 만원");
        }

        [Test]
        public void M11_T5_ZeroRadiusFails()
            => Assert.IsFalse(Pick(Empty, new List<Vector2Int>(), 0, 3, 0f, 0, out Vector2Int _),
                              "반경 0 = 후보 없음");

        [Test]
        public void M11_T5_ClampsToMapBounds()
        {
            // 앵커 모서리 + 선호 큰 값 → 후보는 경계 안에서만
            Assert.IsTrue(HomePicker.PickHomesite(Empty, new List<Vector2Int>(), new Vector2Int(0, 0),
                                                  10, 0, 8f, 3, 0, 4, 0, 4, out Vector2Int t));
            Assert.IsTrue(t.x >= 0 && t.x <= 4 && t.y >= 0 && t.y <= 4, $"경계 밖 금지 — 실제 {t}");
        }
    }
}
