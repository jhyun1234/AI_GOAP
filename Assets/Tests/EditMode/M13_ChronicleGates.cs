using System.Collections.Generic;
using AIVillage.M0;
using NUnit.Framework;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M13-C1 명부 게이트 (M13-T3) — 기록 축의 3대 보장:
    /// 회고 순서 결정성(같은 판 = 같은 순서), 퇴장 사유 불변(멱등 — 보루가 사유를 덮으면
    /// 이야기가 사라진다, ADR-M13-3), LeftDay -1 센티넬(Day 0 사망과 생존 중의 구별).
    /// </summary>
    public class M13_ChronicleGates
    {
        private static ChronicleService Service(params (string id, float born)[] births)
        {
            var c = new ChronicleService();
            foreach ((string id, float born) b in births)
                c.RecordBirth(b.id, b.id, "중립", "무직", b.born);
            return c;
        }

        [Test]
        public void M13_T3_RosterByBirth_DeterministicOrder()
        {
            // 등록 순서를 섞어도 등장일 순 + 동률은 Id 사전순 (PickNearestIndex 규약)
            ChronicleService c = Service(("C", 5f), ("A", 0f), ("B", 5f));
            IReadOnlyList<VillagerRecord> roster = c.RosterByBirth();
            Assert.AreEqual(3, roster.Count);
            Assert.AreEqual("A", roster[0].Id, "등장일 최소 우선");
            Assert.AreEqual("B", roster[1].Id, "동률(Day 5)은 Id 사전순");
            Assert.AreEqual("C", roster[2].Id);
        }

        [Test]
        public void M13_T3_RecordExit_IdempotentAndCausePreserved()
        {
            ChronicleService c = Service(("A", 0f));
            c.RecordExit("A", 34f, ExitCause.Starvation);
            // 두 번째 퇴장·보루가 첫 사유를 덮지 않는다 (사유를 덮으면 이야기가 사라진다)
            c.RecordExit("A", 40f, ExitCause.Injury);
            c.CloseIfOpen("A", 41f);
            VillagerRecord r = c.RosterByBirth()[0];
            Assert.AreEqual(ExitCause.Starvation, r.Cause, "첫 사유 보존");
            Assert.AreEqual(34f, r.LeftDay, "첫 퇴장일 보존");
        }

        [Test]
        public void M13_T3_CloseIfOpen_OnlyClosesAlive()
        {
            ChronicleService c = Service(("A", 0f), ("B", 1f));
            c.RecordExit("A", 10f, ExitCause.Injury);
            c.CloseIfOpen("A", 12f); // 이미 마감 — 무시
            c.CloseIfOpen("B", 12f); // 사망 경로를 안 거친 소멸 — 보루 발동
            IReadOnlyList<VillagerRecord> roster = c.RosterByBirth();
            Assert.AreEqual(ExitCause.Injury, roster[0].Cause);
            Assert.AreEqual(ExitCause.Unknown, roster[1].Cause, "보루 = Unknown");
            Assert.AreEqual(12f, roster[1].LeftDay);
        }

        [Test]
        public void M13_T3_RecordBirth_IdempotentOnReRegister()
        {
            ChronicleService c = Service(("A", 3f));
            c.RecordBirth("A", "A", "고집쟁이", "농부", 9f); // 재등록 — 무시 (첫 기록이 진실)
            VillagerRecord r = c.RosterByBirth()[0];
            Assert.AreEqual(3f, r.BornDay);
            Assert.AreEqual("중립", r.PersonalityName);
        }

        [Test]
        public void M13_T3_LeftDaySentinel_AliveVsDayZeroDeath()
        {
            // -1 센티넬 — Day 0 사망과 생존 중이 구별된다 (명세 §5.3)
            ChronicleService c = Service(("A", 0f), ("B", 0f));
            c.RecordExit("A", 0f, ExitCause.Starvation); // Day 0 아사
            IReadOnlyList<VillagerRecord> roster = c.RosterByBirth();
            Assert.AreEqual(0f, roster[0].LeftDay, "Day 0 사망 = 0");
            Assert.AreEqual(-1f, roster[1].LeftDay, "생존 중 = -1 (0과 구별)");
            Assert.AreEqual(ExitCause.Alive, roster[1].Cause);
        }

        // ── M13-T4: 사건 로그 (C2) ───────────────────────────────────────────

        [Test]
        public void M13_T4_EventId_AppendOnlyIntegersFrozen()
        {
            // append-only 회귀 탐지 (ADR-M13-2) — 값이 바뀌면 저장된 연대기가 다른 이야기가 된다.
            // 새 사건은 6 이후로만 추가하고 이 게이트에 한 줄 늘린다.
            Assert.AreEqual(0, (int)EventId.Injured);
            Assert.AreEqual(1, (int)EventId.Healed);
            Assert.AreEqual(2, (int)EventId.GotHome);
            Assert.AreEqual(3, (int)EventId.OrderTaken);
            Assert.AreEqual(4, (int)EventId.OrderRefused);
            Assert.AreEqual(5, (int)EventId.Built);
            Assert.AreEqual(0, (int)ExitCause.Alive);
            Assert.AreEqual(3, (int)ExitCause.Unknown);
        }

        [Test]
        public void M13_T4_RecordEvent_AppendsInOrder_IgnoresUnknownSubject()
        {
            ChronicleService c = Service(("A", 0f));
            c.RecordEvent("A", EventId.Injured, 3f);
            c.RecordEvent("A", EventId.Healed, 9f);
            c.RecordEvent("GHOST", EventId.Built, 5f); // 미등장 주체 — 무시 (방어)
            VillagerRecord r = c.RosterByBirth()[0];
            Assert.AreEqual(2, r.Events.Count);
            Assert.AreEqual(EventId.Injured, r.Events[0].Kind, "시간순 append");
            Assert.AreEqual(EventId.Healed, r.Events[1].Kind);
        }

        [Test]
        public void M13_T4_ComposeRecentEvents_NewestFirstCappedNeutralWhenEmpty()
        {
            ChronicleService c = Service(("A", 0f));
            Assert.AreEqual("", SeasonHud.ComposeRecentEvents(c.RosterByBirth()[0], 3),
                "사건 0건 = 빈 문자열 (중립 — 줄 없음)");

            c.RecordEvent("A", EventId.Injured, 3f);
            c.RecordEvent("A", EventId.Healed, 9f);
            c.RecordEvent("A", EventId.GotHome, 12f);
            c.RecordEvent("A", EventId.OrderTaken, 15f);
            string s = SeasonHud.ComposeRecentEvents(c.RosterByBirth()[0], 3);
            StringAssert.StartsWith("최근: 명령 수락 D15", s, "최신이 앞");
            StringAssert.Contains("집 마련 D12", s);
            StringAssert.Contains("치료받음 D9", s);
            StringAssert.DoesNotContain("다침", s, "최대 3건 — 가장 오래된 사건 절단");
        }

        [Test]
        public void M13_T4_ComposeLifeEvents_ChronologicalWithRefuseReason()
        {
            ChronicleService c = Service(("A", 0f));
            c.RecordEvent("A", EventId.OrderRefused, 5f, value: ChronicleEvent.REFUSE_HUNGRY);
            c.RecordEvent("A", EventId.Built, 8f);
            string s = SeasonHud.ComposeLifeEvents(c.RosterByBirth()[0]);
            Assert.AreEqual("D5 명령 거부(배고픔) · D8 완공", s, "시간순 + 거부 사유");
        }

        [Test]
        public void M13_T4_CompressRuns_SameEventRepeatsCollapse()
        {
            // 2026-07-30 Play 스크린샷의 B 줄 — "명령 거부(피로)" 9연발은 스팸이지 이야기가 아니다
            ChronicleService c = Service(("A", 0f));
            for (int day = 5; day < 8; day++)
                c.RecordEvent("A", EventId.OrderRefused, day, value: ChronicleEvent.REFUSE_TIRED);
            c.RecordEvent("A", EventId.Built, 8f);
            VillagerRecord r = c.RosterByBirth()[0];

            // 연대기(시간순): 묶음 날짜 = 첫 발생일
            Assert.AreEqual("D5 명령 거부(피로) ×3 · D8 완공", SeasonHud.ComposeLifeEvents(r));
            // 최근(역순): 묶음도 1건으로 센다, 날짜 = 최신 발생일
            Assert.AreEqual("최근: 완공 D8 · 명령 거부(피로) ×3 D7",
                            SeasonHud.ComposeRecentEvents(r, 2));
            // 사유가 다르면 다른 묶음 (Kind+Value 동일해야 압축)
            c.RecordEvent("A", EventId.OrderRefused, 9f, value: ChronicleEvent.REFUSE_HUNGRY);
            StringAssert.Contains("명령 거부(배고픔) D9",
                SeasonHud.ComposeRecentEvents(c.RosterByBirth()[0], 1));
        }

        [Test]
        public void M13_T4_BuiltCarriesBuildingName_SeparateRunsPerBuilding()
        {
            // "완공"만으로는 집인지 밭인지 모른다 (2026-07-30 Play 피드백) — 건물명 병기.
            ChronicleService c = Service(("A", 0f));
            c.RecordEvent("A", EventId.Built, 10f, otherId: "밭");
            c.RecordEvent("A", EventId.Built, 11f, otherId: "밭");
            c.RecordEvent("A", EventId.Built, 12f, otherId: "집");
            VillagerRecord r = c.RosterByBirth()[0];

            // 건물명이 다르면 다른 묶음 — "밭 완공 ×2"가 "집 완공"을 삼키면 안 된다
            Assert.AreEqual("D10 밭 완공 ×2 · D12 집 완공", SeasonHud.ComposeLifeEvents(r));
            Assert.AreEqual("최근: 집 완공 D12 · 밭 완공 ×2 D11", SeasonHud.ComposeRecentEvents(r, 2));
        }

        [Test]
        public void M13_T4_GameOverIsIndexOnly_DetailViaGraveInfo()
        {
            // 2026-07-30 개정 — 명부 = 목차 (연대기 서브라인 제거, 깊이는 클릭 드릴다운).
            ChronicleService c = Service(("A", 0f));
            c.RecordEvent("A", EventId.Injured, 3f);
            c.RecordExit("A", 10f, ExitCause.Injury);
            VillagerRecord r = c.RosterByBirth()[0];

            string roster = SeasonHud.ComposeGameOver(10, settles: 0, roster: c.RosterByBirth());
            StringAssert.DoesNotContain("다침", roster, "명부에는 사건이 없다 (목차)");
            StringAssert.Contains("A — 중립, 무직. Day 0~10, 부상으로 죽음", roster);

            string detail = SeasonHud.ComposeGraveInfo(r);
            StringAssert.StartsWith("† A — 중립, 무직. Day 0~10, 부상으로 죽음", detail);
            StringAssert.Contains("D3 다침", detail, "깊이는 조사 문구에 (드릴다운·무덤 클릭 공용)");

            // 기록 없는 주민 — 생애 구간만 (중립)
            ChronicleService c2 = Service(("B", 1f));
            c2.RecordExit("B", 5f, ExitCause.Starvation);
            string bare = SeasonHud.ComposeGraveInfo(c2.RosterByBirth()[0]);
            Assert.AreEqual("† B — 중립, 무직. Day 1~5, 굶어 죽음", bare);
        }

        [Test]
        public void M13_T3_ComposeGameOver_RosterReplacesStatistics()
        {
            ChronicleService c = Service(("A", 0f));
            c.RecordBirth("B", "B", "게으름뱅이", "무직", 3f);
            c.RecordExit("B", 34f, ExitCause.Starvation);
            c.RecordExit("A", 41f, ExitCause.Injury);

            string s = SeasonHud.ComposeGameOver(41, settles: 2, roster: c.RosterByBirth());
            StringAssert.Contains("마을의 마지막 날 — Day 41", s);
            StringAssert.Contains("A — 중립, 무직. Day 0~41, 부상으로 죽음", s);
            StringAssert.Contains("B — 게으름뱅이, 무직. Day 3~34, 굶어 죽음", s);
            StringAssert.Contains("정착 2", s);
            // 사람 수만큼 줄이 나온다 (헤더 2 + 명부 2 + 빈 줄 1 + 푸터 1)
            Assert.AreEqual(6, s.Split('\n').Length);
        }
    }
}
