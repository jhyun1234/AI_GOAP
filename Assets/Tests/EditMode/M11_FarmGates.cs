using AIVillage.M0;
using NUnit.Framework;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M11-E 개인 밭 게이트 (M11-T4) — 소유 필터. 핵심 불변식 = **남의 밭은 후보가 아니다**
    /// (개간·파종·수확 전부 집주인 본인의 일, 결정 8). 전역 집계(CountEmpty/FarmPlotCount)는
    /// 휴면 보존 — 위협 사다리 규모의 분모라 개인화하지 않는다 (⚠️①).
    /// </summary>
    public class M11_FarmGates
    {
        private static FarmService Farm() => new FarmService(1.5f);

        // ── 소유 집계 ────────────────────────────────────────────────────────────

        [Test]
        public void M11_T4_CountPlotsOf_FiltersByOwner()
        {
            FarmService f = Farm();
            f.RegisterPlot(1, 1, "A");
            f.RegisterPlot(2, 2, "A");
            f.RegisterPlot(3, 3, "B");

            Assert.AreEqual(2, f.CountPlotsOf("A"), "A의 밭 2");
            Assert.AreEqual(1, f.CountPlotsOf("B"), "B의 밭 1");
            Assert.AreEqual(0, f.CountPlotsOf("C"), "밭 없는 주민 0");
            Assert.AreEqual(3, f.Plots.Count, "전역 밭 수는 소유와 무관하게 3 (위협 규모 분모 — ⚠️①)");
        }

        [Test]
        public void M11_T4_CountEmptyRipeOf_FiltersByOwner()
        {
            FarmService f = Farm();
            f.RegisterPlot(1, 1, "A");
            f.RegisterPlot(5, 5, "B");

            Assert.AreEqual(1, f.CountEmptyOf("A"));
            Assert.AreEqual(1, f.CountEmptyOf("B"));

            // A만 심는다 → A는 빈 밭 0, B는 그대로
            Assert.IsTrue(f.TryPlant(f.NearestEmptyOf("A", 0, 0)));
            Assert.AreEqual(0, f.CountEmptyOf("A"), "심으면 내 빈 밭 감소");
            Assert.AreEqual(1, f.CountEmptyOf("B"), "남의 밭은 영향 없음");
            Assert.AreEqual(1, f.CountEmpty(), "전역 집계는 B 몫이 남아 1 (휴면 경로 보존)");

            f.TickGrowth(2f); // 성장 완료 → Ripe
            Assert.AreEqual(1, f.CountRipeOf("A"), "익은 건 A의 밭");
            Assert.AreEqual(0, f.CountRipeOf("B"));
        }

        [Test]
        public void M11_T4_CountOf_NullOwnerIsZero()
        {
            FarmService f = Farm();
            f.RegisterPlot(1, 1, "A");
            Assert.AreEqual(0, f.CountPlotsOf(null), "소유자 미상 조회 = 0 (전역으로 새지 않는다)");
            Assert.AreEqual(0, f.CountPlotsOf(""), "빈 문자열도 0");
        }

        // ── 최근접 조회의 타인 배제 ──────────────────────────────────────────────

        [Test]
        public void M11_T4_NearestEmptyOf_ExcludesOthersPlots()
        {
            FarmService f = Farm();
            f.RegisterPlot(1, 1, "B");   // 더 가깝지만 남의 밭
            f.RegisterPlot(9, 9, "A");   // 멀지만 내 밭

            FarmPlot p = f.NearestEmptyOf("A", 0, 0);
            Assert.IsNotNull(p);
            Assert.AreEqual(new Vector2Int(9, 9), p.Tile, "가까운 남의 밭이 아니라 먼 내 밭");

            Assert.IsNull(f.NearestEmptyOf("C", 0, 0), "밭 없는 주민은 후보 없음 (전역 폴백 금지)");
        }

        [Test]
        public void M11_T4_NearestRipeOf_ExcludesOthersPlots()
        {
            FarmService f = Farm();
            f.RegisterPlot(1, 1, "B");
            f.RegisterPlot(9, 9, "A");
            f.TryPlant(f.NearestEmptyOf("B", 0, 0));
            f.TickGrowth(2f);

            Assert.IsNull(f.NearestRipeOf("A", 0, 0), "B의 익은 밭을 A가 수확하러 가지 않는다");
            Assert.IsNotNull(f.NearestRipeOf("B", 0, 0), "B 본인은 수확 가능");
        }

        [Test]
        public void M11_T4_NearestOf_StillRespectsOccupancy()
        {
            FarmService f = Farm();
            f.RegisterPlot(1, 1, "A");
            f.RegisterPlot(5, 5, "A");

            FarmPlot near = f.NearestEmptyOf("A", 0, 0);
            Assert.IsTrue(near.TryClaim("A"));
            Assert.AreEqual(new Vector2Int(5, 5), f.NearestEmptyOf("A", 0, 0).Tile,
                            "점유 중인 내 밭은 다음 후보로 (기존 점유 규약 유지)");
        }

        // ── 소유는 등록 시 1회, 파괴는 소유 무관 (⚠️④) ──────────────────────────

        [Test]
        public void M11_T4_OwnerIsRecordedOnRegister()
        {
            FarmService f = Farm();
            f.RegisterPlot(4, 4, "A");
            Assert.AreEqual("A", f.Plots[0].OwnerId, "등록 시 소유자 기입");
        }

        [Test]
        public void M11_T4_RemovePlot_IgnoresOwnership()
        {
            FarmService f = Farm();
            f.RegisterPlot(4, 4, "A");
            // 재해·위협의 파괴는 소유와 무관하게 기존 문 그대로 (⚠️④ — 소유자 분기 금지)
            Assert.IsTrue(f.RemovePlot(4, 4), "파괴는 소유를 묻지 않는다");
            Assert.AreEqual(0, f.CountPlotsOf("A"), "소유 집계에서도 사라진다");
        }
    }
}
