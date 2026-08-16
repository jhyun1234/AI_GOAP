using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

using static AIVillage.Tests.EditMode.GateHelpers;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M14 계절 방아쇠 게이트 (명세 W1 — M14-T1). 이 파일이 M14 게이트의 집이다 —
    /// W1(DaysToFreeze 순수 코어·심기 창·슬롯 주입)을 시작으로 W3(지터)·W4(기록) 게이트가 뒤에 추가된다.
    /// 기준 사이클 = 실측 에셋 구성: 온화3 → 여름3(위기·봉쇄X) → 가을2 → 겨울4(위기·봉쇄O), 총 12일.
    /// </summary>
    public class M14_SeasonTriggerGates
    {
        // MakeSeason·DestroyAll → GateHelpers (2026-08-11 2차 감사 통합 — SeasonSO[] 통째는
        // params Object[]가 배열 공변으로 그대로 받는다)

        /// <summary>실측 구성 사이클 — 온화 [0,3) · 여름 [3,6) · 가을 [6,8) · 겨울 [8,12).</summary>
        private static SeasonSO[] MakeStandardCycle()
            => new[]
            {
                MakeSeason("온화", 3f, crisis: false),
                MakeSeason("여름", 3f, crisis: true),                       // 위기지만 봉쇄 아님 (재해 축)
                MakeSeason("가을", 2f, crisis: false),
                MakeSeason("겨울", 4f, crisis: true, frozen: true, growthMult: 0f),
            };

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

        // ── M14-T2: 개체 성향 지터 (W3) — 정체성이지 주사위가 아니다 (ADR-M14-1) ──

        [Test]
        public void M14_T2a_Jitter_NeutralInvariant()
        {
            var src = new[] { new TraitValue { Trait = TraitId.Diligence, Value = -80 } };

            // amp 0 = 원본 그대로 (참조 동일 허용 — 사본 의무 없음). 전 기존 게이트 green의 근거.
            Assert.AreSame(src, TraitVector.Jitter(src, "아무개", 0), "amp 0 = 원본 그대로");
            Assert.IsNull(TraitVector.Jitter(null, "아무개", 25), "성격 없음(null) = 중립 유지");
        }

        [Test]
        public void M14_T2b_Jitter_DeterministicPerIdentity()
        {
            var src = new[]
            {
                new TraitValue { Trait = TraitId.Diligence, Value = -80 },
                new TraitValue { Trait = TraitId.Foresight, Value = -70 },
            };

            TraitValue[] a1 = TraitVector.Jitter(src, "응이", 25);
            TraitValue[] a2 = TraitVector.Jitter(src, "응이", 25);
            for (int i = 0; i < a1.Length; i++)
            {
                Assert.AreEqual(a1[i].Trait, a2[i].Trait, "같은 신원 = 같은 축 순서");
                Assert.AreEqual(a1[i].Value, a2[i].Value, "같은 신원 = 같은 벡터 (재계산 = 세이브 불필요)");
                Assert.GreaterOrEqual(a1[i].Value, -100, "클램프 하한");
                Assert.LessOrEqual(a1[i].Value, 100, "클램프 상한");
            }

            // 다른 신원 = 어딘가는 다른 벡터 (표본 5쌍 중 1쌍 이상 — FNV 충돌 방어적 완화)
            bool anyDiff = false;
            string[] ids = { "응이", "콩이", "돌이", "밤이", "새벽이" };
            TraitValue[] baseline = TraitVector.Jitter(src, ids[0], 25);
            for (int n = 1; n < ids.Length && !anyDiff; n++)
            {
                TraitValue[] other = TraitVector.Jitter(src, ids[n], 25);
                for (int i = 0; i < baseline.Length; i++)
                    if (baseline[i].Value != other[i].Value) { anyDiff = true; break; }
            }
            Assert.IsTrue(anyDiff, "다른 신원인데 전원 같은 벡터 — 시드가 신원을 안 탄다");
        }

        // ── M14-T3: 최소 기록 (W4) — 경주의 자 ──

        [Test]
        public void M14_T3_WinterAlert_PreventionOnlyLine()
        {
            // 중립 불변식 — 새 인자 기본값(-1, null)은 기존 4인자 출력과 완전 동일
            Assert.AreEqual("", SeasonHud.ComposeStatus(null, 0, -1, null), "무경보 = 빈 문자열 유지");

            var unprepared = new[] { ("응이", 2), ("콩이", 1) };
            string s = SeasonHud.ComposeStatus(null, 0, -1, null, 3, unprepared);
            StringAssert.Contains("겨울까지 3일", s);
            StringAssert.Contains("응이(2일)", s);
            StringAssert.Contains("콩이(1일)", s);

            // 예방 전용 — 봉쇄 중(0)·창 밖(-1)·전원 대비 완료(빈 목록)면 줄 자체가 없다
            Assert.AreEqual("", SeasonHud.ComposeStatus(null, 0, -1, null, 0, unprepared),
                "봉쇄 진행 중(0) = 경보 없음 (심판 중 — 굶는 주민 줄의 몫)");
            Assert.AreEqual("", SeasonHud.ComposeStatus(null, 0, -1, null, 3, new (string, int)[0]),
                "전원 대비 완료 = 경보 없음");
        }

        [Test]
        public void M14_T3_CalendarShowsYear()
        {
            SeasonSO[] cycle = MakeStandardCycle();
            var svc = new SeasonService(cycle);

            svc.Tick(1f); // 1년째 온화
            StringAssert.Contains("1년째 온화", SeasonHud.Compose(1f, svc, 3f),
                "연차 병기 — 경주의 자는 살아 있는 동안 보여야 한다 (ADR-M13-1의 정신)");
            svc.Tick(13f); // 사이클 12일 → 2년째 온화
            StringAssert.Contains("2년째", SeasonHud.Compose(13f, svc, 3f), "사이클 2바퀴째 = 2년째");

            DestroyAll(cycle);
        }
    }
}
