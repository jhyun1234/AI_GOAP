using System.Collections.Generic;
using AIVillage.M0;
using NUnit.Framework;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M11-F 택지 게이트 (M11-T5) — HomePicker의 결정적 배치 (ADR-M11-5).
    /// 간격 위반 배제·bias 부호별 원근 반전·동률 사전순·만원 false·결정성이 전부다.
    /// 랜덤이 섞이면 여기서 즉시 깨진다 (같은 입력 2회 = 같은 출력).
    /// </summary>
    public class M11_HomesiteGates
    {
        private static readonly Vector2Int Anchor = new Vector2Int(0, 0);

        /// <summary>아무것도 점유되지 않은 세계 (경계 밖은 호출자가 이미 자름).</summary>
        private static System.Func<int, int, bool> Empty => (x, y) => false;

        private static bool Pick(System.Func<int, int, bool> occupied, IReadOnlyList<Vector2Int> houses,
            int radius, int minSpacing, float bias, out Vector2Int tile)
            => HomePicker.PickHomesite(occupied, houses, Anchor, radius, minSpacing, bias,
                                       -20, 20, -20, 20, out tile);

        // ── 간격 ──────────────────────────────────────────────────────────────

        [Test]
        public void M11_T5_PickHomesite_RejectsTilesWithinMinSpacing()
        {
            var houses = new List<Vector2Int> { new Vector2Int(0, 0) };
            Assert.IsTrue(Pick(Empty, houses, 5, 3, 0f, out Vector2Int tile));
            int d = Mathf.Max(Mathf.Abs(tile.x), Mathf.Abs(tile.y));
            Assert.GreaterOrEqual(d, 3, $"기존 집 (0,0)과 체비쇼프 3 이상이어야 한다 — 실제 {tile}");
        }

        [Test]
        public void M11_T5_KeepsSpacing_ZeroSpacingIsNeutral()
        {
            var houses = new List<Vector2Int> { new Vector2Int(0, 0) };
            Assert.IsTrue(HomePicker.KeepsSpacing(houses, 0, 0, 0), "간격 0 = 규칙 없음 (M11 이전 동작)");
            Assert.IsTrue(HomePicker.KeepsSpacing(null, 0, 0, 3), "집 목록 없음 = 항상 통과");
            Assert.IsFalse(HomePicker.KeepsSpacing(houses, 2, 0, 3), "간격 2 < 3 = 배제");
            Assert.IsTrue(HomePicker.KeepsSpacing(houses, 3, 0, 3), "간격 3 = 경계 포함(이상)");
        }

        // ── bias 원근 반전 ─────────────────────────────────────────────────────

        [Test]
        public void M11_T5_PickHomesite_BiasFlipsNearFar()
        {
            var houses = new List<Vector2Int>();

            Assert.IsTrue(Pick(Empty, houses, 4, 0, 0f, out Vector2Int near));
            int dNear = Mathf.Max(Mathf.Abs(near.x), Mathf.Abs(near.y));
            Assert.AreEqual(0, dNear, "bias 0 = 앵커에 가장 가까운 자리 (빈 마을이면 앵커 자신)");

            Assert.IsTrue(Pick(Empty, houses, 4, 0, 2f, out Vector2Int far));
            int dFar = Mathf.Max(Mathf.Abs(far.x), Mathf.Abs(far.y));
            Assert.AreEqual(4, dFar, "bias 2 = 점수 부호 반전 → 마을 바깥 링(반경 4)");
        }

        [Test]
        public void M11_T5_OutskirtsBias_NullsAreNeutral()
            => Assert.AreEqual(0f, HomePicker.OutskirtsBias(null, null), "성격·직업 없음 = 중립 0");

        // ── 동률 사전순 ────────────────────────────────────────────────────────

        [Test]
        public void M11_T5_PickHomesite_TieBreaksLexicographic()
        {
            // 앵커 점유 + 반경 1, bias 0 → 남은 8개 후보 전부 거리 1 = 완전 동률. 사전순 최소는 (-1,-1).
            Assert.IsTrue(Pick((x, y) => x == 0 && y == 0, new List<Vector2Int>(), 1, 0, 0f, out Vector2Int tile));
            Assert.AreEqual(new Vector2Int(-1, -1), tile, "동률은 (x,y) 사전순 최소");
        }

        [Test]
        public void M11_T5_PickHomesite_Deterministic()
        {
            var houses = new List<Vector2Int> { new Vector2Int(2, 2), new Vector2Int(-3, 1) };
            Assert.IsTrue(Pick(Empty, houses, 6, 3, 0.5f, out Vector2Int a));
            Assert.IsTrue(Pick(Empty, houses, 6, 3, 0.5f, out Vector2Int b));
            Assert.AreEqual(a, b, "같은 입력 2회 = 같은 출력 (랜덤 금지 — ADR-M11-5)");
        }

        // ── 만원 ──────────────────────────────────────────────────────────────

        [Test]
        public void M11_T5_PickHomesite_FullVillageFails()
        {
            // 반경 2 전 영역이 점유 → 후보 0 = 보이는 소프트 상한
            Assert.IsFalse(Pick((x, y) => true, new List<Vector2Int>(), 2, 3, 0f, out Vector2Int _),
                           "만원이면 false — 구역 밖 폴백 없음");

            // 간격만으로도 만원이 된다: 반경 2 안에서 (0,0) 집 하나가 간격 5를 요구
            Assert.IsFalse(Pick(Empty, new List<Vector2Int> { new Vector2Int(0, 0) }, 2, 5, 0f, out Vector2Int _),
                           "간격 요구가 반경을 덮으면 만원");
        }

        [Test]
        public void M11_T5_PickHomesite_ZeroRadiusFails()
            => Assert.IsFalse(Pick(Empty, new List<Vector2Int>(), 0, 3, 0f, out Vector2Int _),
                              "마을 반경 0 = 후보 없음 (설정 실수 방어)");

        // ── 경계 클램프 ────────────────────────────────────────────────────────

        [Test]
        public void M11_T5_PickHomesite_ClampsToMapBounds()
        {
            // 앵커가 맵 경계(0..4)의 모서리면 후보도 경계 안에서만 나온다
            Assert.IsTrue(HomePicker.PickHomesite(Empty, new List<Vector2Int>(), new Vector2Int(0, 0),
                                                  10, 0, 2f, 0, 4, 0, 4, out Vector2Int tile));
            Assert.IsTrue(tile.x >= 0 && tile.x <= 4 && tile.y >= 0 && tile.y <= 4,
                          $"맵 경계 밖 택지 금지 — 실제 {tile}");
            // 거리 4 링(경계 테두리)이 전부 동률이므로 사전순 최소가 이긴다 — (4,4)가 아니라 (0,4)
            Assert.AreEqual(new Vector2Int(0, 4), tile, "bias 2 = 경계 안 최원 링 + 동률 사전순");
        }
    }
}
