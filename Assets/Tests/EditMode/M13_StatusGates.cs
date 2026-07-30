using System.Collections.Generic;
using AIVillage.M0;
using NUnit.Framework;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M13-B 상태 알림 게이트 (M13-T2) — 상태 줄의 3대 보장:
    /// 조건이 없으면 완전히 빈다(중립 — 평온한 마을에 경보 금지),
    /// 굶는 주민은 이름과 함께 개인 단위 N줄(2026-07-30 개정 — "누구인지 모르는 정보"는
    /// 개입을 못 만든다), 식량 문턱은 FOOD_ALERT_DAYS 단일 출처.
    /// </summary>
    public class M13_StatusGates
    {
        private static List<(string, int)> Starving(params (string, int)[] s)
            => new List<(string, int)>(s);

        [Test]
        public void M13_T2_ComposeStatus_EmptyWhenAllClear()
        {
            // 평시(중립값) — 줄 자체가 없다. null 목록과 빈 목록 둘 다 (미배선 방어)
            Assert.AreEqual("", SeasonHud.ComposeStatus(null, 0, -1, null), "무경보 = 빈 문자열");
            Assert.AreEqual("", SeasonHud.ComposeStatus(Starving(), 0, -1, null), "빈 목록 = 빈 문자열");
            // 예고 일수만 있고 이름이 없으면 위협 줄 없음 (미배선 방어)
            Assert.AreEqual("", SeasonHud.ComposeStatus(null, 0, 3, null), "이름 없는 위협 = 표기 없음");
            // 이름만 있고 일수가 음수면 예고 중 아님
            Assert.AreEqual("", SeasonHud.ComposeStatus(null, 0, -1, "늑대"), "예고 전 위협 = 표기 없음");
        }

        [Test]
        public void M13_T2_ComposeStatus_StarvingListedByName_OnePerLine()
        {
            // 2026-07-30 개정의 핵심 — N명이면 N줄, 각 줄에 이름
            string s = SeasonHud.ComposeStatus(Starving(("A", 0), ("C", 2)), 0, -1, null);
            StringAssert.Contains("굶는 주민 A — 식량 0일치", s);
            StringAssert.Contains("굶는 주민 C — 식량 2일치", s);
            Assert.AreEqual(2, s.Split('\n').Length, "2명 = 2줄 (끝 개행 없음)");
        }

        [Test]
        public void M13_T2_ComposeStatus_ThreeConditionsStack()
        {
            string s = SeasonHud.ComposeStatus(Starving(("B", 1)), 2, 3, "늑대");
            StringAssert.Contains("굶는 주민 B — 식량 1일치", s);
            StringAssert.Contains("부상자 2명", s);
            StringAssert.Contains("늑대 — 3일 뒤", s);
            Assert.AreEqual(3, s.Split('\n').Length, "3조건 = 3줄");
        }

        [Test]
        public void M13_T2_ComposeStatus_ThreatCountdownReachesZero()
        {
            // 발동 직전 틱 — 0일 뒤도 유효한 예고다 (음수만 소거)
            StringAssert.Contains("늑대 — 0일 뒤", SeasonHud.ComposeStatus(null, 0, 0, "늑대"));
        }

        [Test]
        public void M13_T2_Calendar_HasNoFoodSuffix()
        {
            // 2026-07-30 개정 회귀 방지 — 달력의 "식량 최소 N일치" 요약(M9-I·M11-D)은 삭제됐다.
            // 마을 최솟값 요약이 상태줄 개인 열거와 겹쳐 "마을 전체가 N일치"로 오독됨 (사용자 피드백).
            // 식량 표기는 상태 알림 줄 한 곳뿐이다 — 달력에 다시 넣으면 이 게이트가 잡는다.
            StringAssert.DoesNotContain("식량", SeasonHud.Compose(4.2f, null, 3f));
        }
    }
}
