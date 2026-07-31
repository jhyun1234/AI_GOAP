using System.Collections.Generic;
using AIVillage.M0;
using NUnit.Framework;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M15 연대기 아카이브 게이트 — 저장 축의 3대 보장:
    /// JSON 왕복 무손실(T1 — 필드가 새면 과거 판의 이야기가 사라진다),
    /// Apply의 append/갱신 규약(T2 — RunNumber는 store만 부여, ADR-M15-1 append-only),
    /// 손상 파일 무해(T3 — 기록은 게임을 못 막는다, ADR-M15-4).
    /// 파일 IO는 게이트에서 안 건드린다 — 순수부(Apply·Parse)가 검증 대상 (RunRecordStore.IsBetter 선례).
    /// </summary>
    public class M15_ChronicleArchiveGates
    {
        private static ChronicleArchive.RunEntry Entry(int winters, int lastDay, bool ended,
                                                       params string[] names)
        {
            var e = new ChronicleArchive.RunEntry
            {
                EndedAt = "2026-07-31 21:04",
                Winters = winters, LastDay = lastDay, PeakPop = names.Length,
                Settles = names.Length, Ended = ended,
            };
            foreach (string n in names)
                e.Roster.Add(new ChronicleArchive.VillagerEntry
                {
                    ShortName = n, Personality = "게으름뱅이", Job = "무직",
                    BornDay = 3, LeftDay = ended ? 34 : -1,
                    Cause = (int)(ended ? ExitCause.Starvation : ExitCause.Alive),
                    BuddyShort = "B", GrudgeShort = "",
                    LifeEvents = "D5~34 명령 거부(피로) ×30",
                });
            return e;
        }

        [Test]
        public void M15_T1_JsonRoundtrip_PreservesAllFields()
        {
            var file = new ChronicleArchive.ArchiveFile();
            ChronicleArchive.RunEntry src = Entry(3, 44, true, "A", "B");
            src.PeakPricePct = 150; // M16-W6 append 필드 — 왕복 보존 (append-only 규약)
            ChronicleArchive.Apply(file, -1, src);

            ChronicleArchive.ArchiveFile back =
                ChronicleArchive.Parse(JsonUtility.ToJson(file, prettyPrint: true));

            Assert.IsNotNull(back);
            Assert.AreEqual(1, back.Runs.Count);
            ChronicleArchive.RunEntry r = back.Runs[0];
            Assert.AreEqual(1, r.RunNumber);
            Assert.AreEqual("2026-07-31 21:04", r.EndedAt);
            Assert.AreEqual(3, r.Winters);
            Assert.AreEqual(44, r.LastDay);
            Assert.AreEqual(2, r.PeakPop);
            Assert.IsTrue(r.Ended);
            Assert.AreEqual(150, r.PeakPricePct, "M16 append 필드도 왕복 보존");
            Assert.AreEqual(2, r.Roster.Count);
            ChronicleArchive.VillagerEntry v = r.Roster[0];
            Assert.AreEqual("A", v.ShortName);
            Assert.AreEqual("게으름뱅이", v.Personality);
            Assert.AreEqual("무직", v.Job);
            Assert.AreEqual(3, v.BornDay);
            Assert.AreEqual(34, v.LeftDay);
            Assert.AreEqual((int)ExitCause.Starvation, v.Cause);
            Assert.AreEqual("B", v.BuddyShort);
            Assert.AreEqual("", v.GrudgeShort, "빈 관계는 빈 문자열로 왕복 (null 아님)");
            Assert.AreEqual("D5~34 명령 거부(피로) ×30", v.LifeEvents);
        }

        [Test]
        public void M15_T2_Apply_AppendAssignsRunNumber()
        {
            var file = new ChronicleArchive.ArchiveFile();
            int i0 = ChronicleArchive.Apply(file, -1, Entry(1, 15, false, "A"));
            int i1 = ChronicleArchive.Apply(file, -1, Entry(0, 9, true, "B"));
            Assert.AreEqual(0, i0);
            Assert.AreEqual(1, i1);
            Assert.AreEqual(2, file.Runs.Count);
            Assert.AreEqual(1, file.Runs[0].RunNumber, "번호는 store가 부여 — 1부터");
            Assert.AreEqual(2, file.Runs[1].RunNumber);
        }

        [Test]
        public void M15_T2_Apply_UpdateKeepsCountAndNumber()
        {
            var file = new ChronicleArchive.ArchiveFile();
            int idx = ChronicleArchive.Apply(file, -1, Entry(1, 15, false, "A"));
            // 같은 판의 전멸 마감 — 자리 갱신 (겨울 갱신 → 전멸 마감의 실제 흐름)
            int again = ChronicleArchive.Apply(file, idx, Entry(3, 44, true, "A"));
            Assert.AreEqual(idx, again);
            Assert.AreEqual(1, file.Runs.Count, "갱신은 Count 불변");
            Assert.AreEqual(1, file.Runs[0].RunNumber, "번호는 처음 받은 그대로");
            Assert.AreEqual(3, file.Runs[0].Winters);
            Assert.IsTrue(file.Runs[0].Ended);
        }

        [Test]
        public void M15_T2_Apply_OutOfRangeDemotesToAppend()
        {
            var file = new ChronicleArchive.ArchiveFile();
            ChronicleArchive.Apply(file, -1, Entry(1, 15, false, "A"));
            // 범위 밖 인덱스 (다른 프로세스가 파일을 줄였거나 오염) — 기록 유실보다 중복이 낫다
            int idx = ChronicleArchive.Apply(file, 7, Entry(2, 30, true, "B"));
            Assert.AreEqual(1, idx, "append로 강등");
            Assert.AreEqual(2, file.Runs.Count);
            Assert.AreEqual(2, file.Runs[1].RunNumber);
        }

        [Test]
        public void M15_T3_Parse_CorruptOrEmptyIsHarmless()
        {
            Assert.IsNull(ChronicleArchive.Parse(null), "null = 빈 아카이브 처리");
            Assert.IsNull(ChronicleArchive.Parse(""), "빈 문자열");
            Assert.IsNull(ChronicleArchive.Parse("{ 깨진 json"), "손상 — 예외 전파 없이 null");
            Assert.IsNull(ChronicleArchive.Parse("3"), "루트가 객체가 아님");
        }

        // ── M15-T4 — 패널 조립 순수 함수 (표시 정책이 데이터를 배반하지 않는가) ──

        [Test]
        public void M15_T4_BuildChronicleRows_CurrentFirstThenLatest()
        {
            var file = new ChronicleArchive.ArchiveFile();
            ChronicleArchive.Apply(file, -1, Entry(1, 15, false, "A")); // 판 1 (옛날)
            ChronicleArchive.Apply(file, -1, Entry(3, 44, true, "B"));  // 판 2 (최신)
            ChronicleArchive.RunEntry current = Entry(0, 5, false, "C");

            List<ChronicleArchive.RunEntry> rows = SeasonHud.BuildChronicleRows(file.Runs, current);
            Assert.AreEqual(3, rows.Count);
            Assert.AreSame(current, rows[0], "진행 중 판이 맨 위");
            Assert.AreEqual(2, rows[1].RunNumber, "저장된 판은 최신순");
            Assert.AreEqual(1, rows[2].RunNumber);

            // 전멸 후(현재 판 없음) — 저장분만 최신순
            List<ChronicleArchive.RunEntry> noCurrent = SeasonHud.BuildChronicleRows(file.Runs, null);
            Assert.AreEqual(2, noCurrent.Count);
            Assert.AreEqual(2, noCurrent[0].RunNumber);

            // 첫 겨울 이후 — 현재 판의 저장분(skipIndex)은 제외한다: 라이브 행과 같은 판이
            // 두 줄로 겹치면 안 된다 (Play 검증에서 발견된 중복, 2026-07-31)
            List<ChronicleArchive.RunEntry> skip1 =
                SeasonHud.BuildChronicleRows(file.Runs, current, skipIndex: 1);
            Assert.AreEqual(2, skip1.Count, "저장분 2 중 1 제외 + 라이브 행");
            Assert.AreSame(current, skip1[0]);
            Assert.AreEqual(1, skip1[1].RunNumber, "제외된 건 index 1(판 2)뿐");
        }

        [Test]
        public void M15_T4_ComposeChronicleList_MarksCurrentAndOutcome()
        {
            Assert.That(SeasonHud.ComposeChronicleList(new List<ChronicleArchive.RunEntry>(), false),
                        Does.Contain("아직 기록된 마을이 없다"), "빈 아카이브 = 안내 문구");

            var file = new ChronicleArchive.ArchiveFile();
            ChronicleArchive.Apply(file, -1, Entry(1, 15, false, "A"));
            ChronicleArchive.Apply(file, -1, Entry(3, 44, true, "B"));
            List<ChronicleArchive.RunEntry> rows =
                SeasonHud.BuildChronicleRows(file.Runs, Entry(0, 5, false, "C"));

            string s = SeasonHud.ComposeChronicleList(rows, firstIsCurrent: true);
            StringAssert.Contains("▶ 이번 판 — 겨울 0 · Day 5 · 최대 1명", s);
            StringAssert.Contains("판 2 — 겨울 3 · Day 44 · 최대 1명 · 전멸 · 2026-07-31 21:04", s);
            StringAssert.Contains("판 1 — 겨울 1 · Day 15 · 최대 1명 · 중단", s, "Ended false = 중단");
            // 헤더 2줄 뒤가 목록 — 클릭 오프셋(TryPickChronicleRunIndex -2)과 한 몸
            string[] lines = s.Split('\n');
            StringAssert.StartsWith("▶ 이번 판", lines[2], "행 0 = 원문 3번째 줄 (헤더 2줄 규약)");
        }

        [Test]
        public void M15_T4_ComposeRunDetail_RosterLinesWithCauseAndRelations()
        {
            ChronicleArchive.RunEntry run = Entry(3, 44, true, "A"); // 사망자 (아사, 단짝 B)
            run.Roster.Add(new ChronicleArchive.VillagerEntry
            {
                ShortName = "C", Personality = "중립", Job = "농부",
                BornDay = 12, LeftDay = -1, Cause = (int)ExitCause.Alive,
                BuddyShort = "", GrudgeShort = "D", LifeEvents = "",
            });
            run.RunNumber = 3;

            string s = SeasonHud.ComposeRunDetail(run);
            StringAssert.Contains("판 3 — 겨울 3 · Day 44 · 최대 1명 · 전멸", s);
            StringAssert.Contains("† A — 게으름뱅이, 무직. Day 3~34, 굶어 죽음", s, "Cause 정수 → 문구");
            StringAssert.Contains("B의 단짝이었다", s);
            StringAssert.Contains("D5~34 명령 거부(피로) ×30", s, "사건 요약 문자열 그대로");
            StringAssert.Contains("C — 중립, 농부. Day 12~, 생존", s, "LeftDay -1 = 열린 구간");
            StringAssert.DoesNotContain("† C", s, "산 사람에게 비석을 세우지 않는다");
            StringAssert.Contains("D에게 원한이 있었다", s);
        }
    }
}
