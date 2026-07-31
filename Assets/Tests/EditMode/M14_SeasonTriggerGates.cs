using AIVillage.M0;
using NUnit.Framework;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M14 계절 방아쇠 게이트 (명세 W1 — M14-T1). 이 파일이 M14 게이트의 집이다 —
    /// W1(DaysToFreeze 순수 코어·심기 창·슬롯 주입)을 시작으로 W3(지터)·W4(기록) 게이트가 뒤에 추가된다.
    /// 기준 사이클 = 실측 에셋 구성: 온화3 → 여름3(위기·봉쇄X) → 가을2 → 겨울4(위기·봉쇄O), 총 12일.
    /// </summary>
    public class M14_SeasonTriggerGates
    {
        private static SeasonSO MakeSeason(string name, float days, bool crisis, bool frozen = false,
                                           float growthMult = 1f)
        {
            var s = ScriptableObject.CreateInstance<SeasonSO>();
            s.name = name;
            s.DisplayName = name;
            s.DurationDays = days;
            s.IsCrisis = crisis;
            s.ForageFrozen = frozen;
            s.GrowthMult = growthMult;
            return s;
        }

        /// <summary>실측 구성 사이클 — 온화 [0,3) · 여름 [3,6) · 가을 [6,8) · 겨울 [8,12).</summary>
        private static SeasonSO[] MakeStandardCycle()
            => new[]
            {
                MakeSeason("온화", 3f, crisis: false),
                MakeSeason("여름", 3f, crisis: true),                       // 위기지만 봉쇄 아님 (재해 축)
                MakeSeason("가을", 2f, crisis: false),
                MakeSeason("겨울", 4f, crisis: true, frozen: true, growthMult: 0f),
            };

        private static void DestroyAll(SeasonSO[] cycle)
        {
            foreach (SeasonSO s in cycle) Object.DestroyImmediate(s);
        }

        // ── M14-T1-a: DaysToFreeze 순수 코어 — 술어는 봉쇄이지 위기가 아니다 (ADR-M14-2) ──

        [Test]
        public void M14_T1_DaysToFreeze_FreezePredicateNotCrisis()
        {
            SeasonSO[] cycle = MakeStandardCycle();

            // ① 온화 시작: 봉쇄까지 = 온화 3 + 여름 3 + 가을 2 = 8
            Assert.AreEqual(8f, SeasonService.ComputeDaysToFreeze(cycle, 0f), 1e-4f,
                "온화 시작 = 봉쇄까지 8일");

            // ② 여름 중(t=4): DaysToCrisis는 0(위기 진행 중)이지만 봉쇄까지는 4일 —
            //    이 차이가 DaysToFreeze 신설의 존재 이유다 (ADR-M14-2 "위기 ≠ 봉쇄")
            SeasonService.Compute(cycle, 4f, out _, out float crisisDays, out _);
            Assert.AreEqual(0f, crisisDays, 1e-4f, "여름 = 위기 진행 중 (DaysToCrisis 0)");
            Assert.AreEqual(4f, SeasonService.ComputeDaysToFreeze(cycle, 4f), 1e-4f,
                "여름 중에도 봉쇄 카운트다운은 살아 있다 (여름 잔여 2 + 가을 2)");

            // ③ 가을 시작(t=6): 봉쇄까지 2 ④ 겨울 중(t=9): 0
            Assert.AreEqual(2f, SeasonService.ComputeDaysToFreeze(cycle, 6f), 1e-4f, "가을 시작 = 2일");
            Assert.AreEqual(0f, SeasonService.ComputeDaysToFreeze(cycle, 9f), 1e-4f, "봉쇄 진행 중 = 0");

            // 사이클 순환 — 2바퀴째도 같은 위상 (t=12 = 온화 시작)
            Assert.AreEqual(8f, SeasonService.ComputeDaysToFreeze(cycle, 12f), 1e-4f,
                "사이클 2바퀴째 온화 시작 = 8일 (위상 순환)");

            DestroyAll(cycle);

            // ⑤ 봉쇄 없는 사이클(위기는 있어도) = NO_CRISIS — "≤N" 트리거 전부 불발
            SeasonSO mild = MakeSeason("온화", 3f, crisis: false);
            SeasonSO summer = MakeSeason("여름", 3f, crisis: true);
            Assert.AreEqual(SeasonService.NO_CRISIS,
                SeasonService.ComputeDaysToFreeze(new[] { mild, summer }, 1f),
                "봉쇄 계절이 없으면 NO_CRISIS (위기가 있어도)");
            Object.DestroyImmediate(mild);
            Object.DestroyImmediate(summer);
        }

        // ── M14-T1-b: 심기 창 순수 판정 — "지금 심으면 봉쇄 전에 익는다" ──

        [Test]
        public void M14_T1_PlantWindow_ClosesLateAutumnAndAllWinter()
        {
            const float growthDays = 1.5f; // WorldConfigSO.FarmGrowthDays 기본값 — 런타임 원본은 에셋

            Assert.IsTrue(WorldModel.ComputePlantWindow(1f, 2f, growthDays),
                "가을 첫날(봉쇄까지 2 ≥ 1.5) = 열림");
            Assert.IsFalse(WorldModel.ComputePlantWindow(1f, 0.8f, growthDays),
                "가을 끝(봉쇄까지 0.8 < 1.5) = 닫힘 — 헛수고 파종 방지");
            Assert.IsFalse(WorldModel.ComputePlantWindow(0f, 8f, growthDays),
                "겨울(GrowthMult 0) = 닫힘 — 다음 겨울까지 8일이어도 (명세 ⚠️W1-② 차단 항)");
            Assert.IsTrue(WorldModel.ComputePlantWindow(1f, 4f, growthDays),
                "여름(봉쇄까지 4, 성장 정상) = 열림 — 실질 파종 성수기");
        }

        // ── M14-T1-c: 슬롯 주입 — 스냅샷에 실려야 트리거가 성립한다 (M6 봉쇄 게이트 패턴) ──

        [Test]
        public void M14_T1_Slots_InjectedIntoSnapshot()
        {
            var cfg = ScriptableObject.CreateInstance<WorldConfigSO>(); // FarmGrowthDays 기본 1.5
            var d = new DiscoveryService();
            SeasonSO[] cycle = MakeStandardCycle();

            // 가을 시작 (t=6): DaysToFreeze 슬롯 = ceil(2) = 2, 창 열림
            var autumnSvc = new SeasonService(cycle); autumnSvc.Tick(6f);
            var autumnSnap = new WorldModel(d, cfg, null, autumnSvc).BuildSnapshot(50, 50);
            Assert.AreEqual(2, autumnSnap.Get(SlotId.DaysToFreeze), "가을 시작 = 봉쇄까지 2일 (올림)");
            Assert.AreEqual(1, autumnSnap.Get(SlotId.PlantWindowOpen), "가을 시작 = 심기 창 열림");

            // 겨울 (t=9): 봉쇄 중 0, 창 닫힘
            var winterSvc = new SeasonService(cycle); winterSvc.Tick(9f);
            var winterSnap = new WorldModel(d, cfg, null, winterSvc).BuildSnapshot(50, 50);
            Assert.AreEqual(0, winterSnap.Get(SlotId.DaysToFreeze), "봉쇄 중 = 0");
            Assert.AreEqual(0, winterSnap.Get(SlotId.PlantWindowOpen), "겨울 = 심기 창 닫힘");

            // 계절 미배선 = 중립: 99(트리거 불발) / 1(창 상시 열림 = 계절 없던 시절 동작)
            var plainSnap = new WorldModel(d, cfg).BuildSnapshot(50, 50);
            Assert.AreEqual((int)SeasonService.NO_CRISIS, plainSnap.Get(SlotId.DaysToFreeze),
                "미배선 = 99 — '≤N' 트리거 전부 불발 (중립 불변식)");
            Assert.AreEqual(1, plainSnap.Get(SlotId.PlantWindowOpen),
                "미배선 = 창 열림 — 계절 없는 테스트·舊 에셋의 기존 동작 유지 (중립 불변식)");

            DestroyAll(cycle);
            Object.DestroyImmediate(cfg);
        }
    }
}
