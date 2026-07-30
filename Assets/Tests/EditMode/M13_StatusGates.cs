using AIVillage.M0;
using NUnit.Framework;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M13-B 상태 알림 게이트 (M13-T2) — 상태 줄의 2대 보장:
    /// 조건이 없으면 완전히 빈다(중립 — 평온한 마을에 경보 금지),
    /// 식량 문턱은 달력 FoodSuffix의 붉은 강조(≤2)와 같은 값(판단 규칙 이원화 금지).
    /// </summary>
    public class M13_StatusGates
    {
        [Test]
        public void M13_T2_ComposeStatus_EmptyWhenAllClear()
        {
            // 평시(중립값) — 줄 자체가 없다
            Assert.AreEqual("", SeasonHud.ComposeStatus(99, 0, -1, null), "무경보 = 빈 문자열");
            // 식량 3일치 = 문턱(≤2) 밖 — FoodSuffix 붉은 강조와 같은 경계
            Assert.AreEqual("", SeasonHud.ComposeStatus(3, 0, -1, null), "식량 3일치는 경보 아님");
            // 예고 일수만 있고 이름이 없으면 위협 줄 없음 (미배선 방어)
            Assert.AreEqual("", SeasonHud.ComposeStatus(99, 0, 3, null), "이름 없는 위협 = 표기 없음");
            // 이름만 있고 일수가 음수면 예고 중 아님
            Assert.AreEqual("", SeasonHud.ComposeStatus(99, 0, -1, "늑대"), "예고 전 위협 = 표기 없음");
        }

        [Test]
        public void M13_T2_ComposeStatus_ThreeConditionsThreeLines()
        {
            string s = SeasonHud.ComposeStatus(1, 2, 3, "늑대");
            StringAssert.Contains("식량 1일치", s);
            StringAssert.Contains("부상자 2명", s);
            StringAssert.Contains("늑대 — 3일 뒤", s);
            Assert.AreEqual(3, s.Split('\n').Length, "3조건 = 3줄 (끝 개행 없음)");
        }

        [Test]
        public void M13_T2_ComposeStatus_FoodThresholdBoundary()
        {
            // 문턱 경계 — ≤2 포함, 0도 경보 (아사 직전이 가장 위험)
            StringAssert.Contains("식량 2일치", SeasonHud.ComposeStatus(2, 0, -1, null));
            StringAssert.Contains("식량 0일치", SeasonHud.ComposeStatus(0, 0, -1, null));
            // 단일 조건 = 단일 줄 (다른 조건 줄이 섞이지 않는다)
            Assert.AreEqual(1, SeasonHud.ComposeStatus(2, 0, -1, null).Split('\n').Length);
        }

        [Test]
        public void M13_T2_ComposeStatus_ThreatCountdownReachesZero()
        {
            // 발동 직전 틱 — 0일 뒤도 유효한 예고다 (음수만 소거)
            StringAssert.Contains("늑대 — 0일 뒤", SeasonHud.ComposeStatus(99, 0, 0, "늑대"));
        }
    }
}
