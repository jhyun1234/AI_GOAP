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

        // ── M13-T5: 이유·문턱 줄 (D) ─────────────────────────────────────────

        [Test]
        public void M13_T5_ComposeReason_MarginsMatchJudgeOrder()
        {
            // 여유 표기와 실제 거부 판정이 같은 산식(RefuseSatiety/FatigueLimit)을 쓴다 —
            // 화면이 "여유 있음"이라는데 명령이 거부되면 협상 학습이 무너진다 (ADR-M1-2).
            var cfg = UnityEngine.ScriptableObject.CreateInstance<AgentConfigSO>();
            cfg.OrderRefuseSatiety = 35f;
            cfg.OrderRefuseFatigue = 75f;

            string ok = SeasonHud.ComposeReason(null, null, 0, 45f, 60f,
                                                InjurySeverity.None, cfg, null, 99);
            StringAssert.StartsWith("지금: 쉬는 중", ok, "goal 없음 = 쉬는 중 (중립)");
            StringAssert.Contains("명령 여유 포만 10·피로 15", ok);
            StringAssert.DoesNotContain("식량", ok, "99(중립) = 식량 표기 없음");
            Assert.AreEqual(VillagerAgent.OrderResult.Accepted,
                VillagerAgent.JudgeOrder(45f, 60f, cfg), "여유 양수 = 수락과 일치");

            string hungry = SeasonHud.ComposeReason(null, null, 0, 30f, 60f,
                                                    InjurySeverity.None, cfg, null, 4);
            StringAssert.Contains("명령 거부될 것 — 배고픔", hungry);
            StringAssert.Contains("식량 4일치", hungry);
            Assert.AreEqual(VillagerAgent.OrderResult.RefusedHungry,
                VillagerAgent.JudgeOrder(30f, 60f, cfg), "거부 예측 = 실제 판정");

            string tired = SeasonHud.ComposeReason(null, null, 0, 45f, 80f,
                                                   InjurySeverity.None, cfg, null, 99);
            StringAssert.Contains("명령 거부될 것 — 피로", tired);
            Assert.AreEqual(VillagerAgent.OrderResult.RefusedTired,
                VillagerAgent.JudgeOrder(45f, 80f, cfg));

            StringAssert.Contains("명령 불가 — 부상", SeasonHud.ComposeReason(
                null, null, 0, 45f, 60f, InjurySeverity.Light, cfg, null, 99));
        }

        [Test]
        public void M13_T5_ComposeReason_GoalWithoutPlanShowsNameOnly()
        {
            var goal = UnityEngine.ScriptableObject.CreateInstance<GoalSO>();
            goal.DisplayName = "겨울 비축";
            string s = SeasonHud.ComposeReason(goal, null, 0, 45f, 60f,
                                               InjurySeverity.None, null, null, 99);
            StringAssert.StartsWith("지금: 겨울 비축", s);
            StringAssert.DoesNotContain("→", s, "계획 없음 = 사슬 없음 (중립)");
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
