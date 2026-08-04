using System.Collections.Generic;
using System.IO;
using System.Linq;
using AIVillage.M0;
using NUnit.Framework;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M17 재정 게이트 — 촌장 금고 축의 보장.
    /// T5(W1): 회계 폐곡선(무에서 나온 총량 == 도는 돈 + 금고 + 소멸)과 금고 지급 판정.
    /// 흐름이 Mint 하나에서 넷으로 늘어난 것이 이 밀스톤의 가장 위험한 변경이다 —
    /// 다섯 번째 쓰기 경로가 생기면 폐곡선이 깨지고, 그것을 여기와 하루 경계 검산이 잡는다.
    /// </summary>
    public class M17_TreasuryGates
    {
        // ── T5-a: 폐곡선 항등식 (순수) ────────────────────────────────────────

        [Test]
        public void M17_T5_Ledger_BalancedOnEveryFlow()
        {
            // 판 시작: 아무 일도 없음
            Assert.IsTrue(WorldModel.IsLedgerBalanced(0, 0, 0, 0), "빈 장부");

            // ① 임금 순액 4동 (무 → 주민): 발행누적 +4, M +4
            Assert.IsTrue(WorldModel.IsLedgerBalanced(4, 4, 0, 0));

            // ② 원천징수 1동 (무 → 금고): 발행누적 +1, 금고 +1 — M은 그대로
            Assert.IsTrue(WorldModel.IsLedgerBalanced(5, 4, 1, 0));

            // ③ 웃돈 1동 (금고 → 주민): 좌변 불변, 금고 −1·M +1 — 우변도 불변
            Assert.IsTrue(WorldModel.IsLedgerBalanced(5, 5, 0, 0));

            // ④ 사망 소멸 5동 (주민 → 무): M −5, 소멸누적 +5
            Assert.IsTrue(WorldModel.IsLedgerBalanced(5, 0, 0, 5));

            // 직거래(주민 ↔ 주민)는 어느 항도 안 건드린다 — 위 상태 그대로 성립
            Assert.IsTrue(WorldModel.IsLedgerBalanced(5, 0, 0, 5), "M 불변 이전");
        }

        [Test]
        public void M17_T5_Ledger_DetectsLeak()
        {
            // 금고를 채우면서 발행 누적을 안 올린 경우 = ADR-M17-2 밖의 쓰기 경로
            Assert.IsFalse(WorldModel.IsLedgerBalanced(0, 0, 100, 0), "금고가 무에서 솟았다");
            // 소멸시키며 누적을 안 센 경우
            Assert.IsFalse(WorldModel.IsLedgerBalanced(10, 0, 0, 0), "돈이 흔적 없이 사라졌다");
            // 이중 계상 — PayFromTreasury가 발행 누적까지 올리는 버그를 가정한 상태.
            // 정상: 발행 100(→금고) 뒤 웃돈 100(금고→주민) = 누적 100 · M 100 · 금고 0.
            // 버그: 옮기기만 한 돈을 "무에서 나온 돈"으로도 세어 누적이 200이 된다.
            Assert.IsFalse(WorldModel.IsLedgerBalanced(200, 100, 0, 0), "웃돈을 발행으로도 셌다");
            Assert.IsTrue(WorldModel.IsLedgerBalanced(100, 100, 0, 0), "↑의 정상 대조군");
        }

        // ── T5-b: 금고 지급 판정 (순수) ──────────────────────────────────────

        [Test]
        public void M17_T5_CanPayFromTreasury_NoCreditForTheChief()
        {
            Assert.IsTrue(WorldModel.CanPayFromTreasury(50, 10), "잔고 충분");
            Assert.IsTrue(WorldModel.CanPayFromTreasury(10, 10), "딱 맞음 = 지급 가능");
            Assert.IsFalse(WorldModel.CanPayFromTreasury(9, 10), "촌장은 없는 돈을 약속하지 못한다");
            Assert.IsFalse(WorldModel.CanPayFromTreasury(0, 10), "빈 금고");
            Assert.IsFalse(WorldModel.CanPayFromTreasury(50, 0), "0동 지급은 흐름이 아니다");
            Assert.IsFalse(WorldModel.CanPayFromTreasury(50, -5), "음수 방어 (역방향 지급 금지)");
        }

        // ── T2: 세후 임금 (순수 — 확정 보완 3) ───────────────────────────────

        [Test]
        public void M17_T2_NetWage_KnownRates()
        {
            // 명세 §W2 DoD의 검산치 — 현행 임금(벌목 5·채광 6·관개수로 12)에 대한 실수령
            Assert.AreEqual(5, ActionSO.NetWage(5, 0),   "면세");
            Assert.AreEqual(4, ActionSO.NetWage(5, 15),  "보통 — 4.25 내림");
            Assert.AreEqual(3, ActionSO.NetWage(5, 30),  "중과 — 3.5 내림");
            Assert.AreEqual(5, ActionSO.NetWage(6, 15),  "채광 6동 → 5.1 내림");
            Assert.AreEqual(4, ActionSO.NetWage(6, 30),  "채광 6동 → 4.2 내림");
            Assert.AreEqual(10, ActionSO.NetWage(12, 15), "관개수로 12동 → 10.2 내림");
            Assert.AreEqual(0, ActionSO.NetWage(0, 30),  "임금 없는 액션은 0");
            Assert.AreEqual(0, ActionSO.NetWage(-3, 15), "음수 방어");
        }

        [Test]
        public void M17_T2_NetWage_StaysInsideGross_AndFallsWithRate()
        {
            // 확정 보완 3의 실질 조건 — 세액은 잔여(W − 순액)로 구하므로, 순액이 [0, W] 밖으로
            // 나가는 순간 세액이 음수가 되거나 총액을 넘어 회계 폐곡선(D2)이 깨진다.
            // ⚠️ "순액 + (총액 − 순액) == 총액"은 항등식이라 무엇을 구현해도 통과한다 —
            // 실패할 수 있는 명제만 여기 둔다.
            int prev = int.MaxValue; // 오름차순 세율을 훑으며 단조 감소를 확인 (루프 밖에 둔다)
            foreach (int rate in new[] { 0, 15, 30, 50, 90, 100 })
            {
                for (int wage = 1; wage <= 50; wage++)
                {
                    int net = ActionSO.NetWage(wage, rate);
                    Assert.IsTrue(net >= 0 && net <= wage, $"실수령 범위 — 임금 {wage} · 세율 {rate}%");
                }
                // 같은 임금에서 세율이 오르면 실수령은 절대 늘지 않는다 (손잡이의 방향성)
                int atThisRate = ActionSO.NetWage(20, rate);
                Assert.IsTrue(atThisRate <= prev, $"세율 {rate}% 실수령 {atThisRate} > 직전 {prev}");
                prev = atThisRate;
            }

            Assert.AreEqual(20, ActionSO.NetWage(20, 0),   "무세 = 전액");
            Assert.AreEqual(0,  ActionSO.NetWage(20, 100), "전액 과세 = 0 (OnValidate가 90으로 막는 지점)");
            Assert.IsTrue(ActionSO.NetWage(20, 15) > ActionSO.NetWage(20, 30), "세율이 높을수록 덜 받는다");
        }

        [Test]
        public void M17_T2_SmallWage_EffectiveRateSpikes_GuardIsMinimumWage()
        {
            // 확정 보완 3 ⚠️ — 작은 임금에서 실효세율이 튄다. 현재 최소 임금이 5동이라
            // 발현하지 않지만, 임금 에셋을 5동 미만으로 내리면 여기가 근거가 된다 (§8 D8).
            Assert.AreEqual(1, ActionSO.NetWage(2, 15), "임금 2동 · 세율 15% → 실수령 1 = 실효 50%");
            Assert.AreEqual(0, ActionSO.NetWage(1, 15), "임금 1동은 통째로 세금 = 실효 100%");
        }

        // ── T1: 물가 산식의 발행 항 + 원인 분해 (순수 — ADR-M17-3) ───────────

        [Test]
        public void M17_T1_ComputePricePct_MintDebtRaisesPrice()
        {
            // 분모 = Q 20개 × 기준가 10 = 200
            Assert.AreEqual(100, WorldModel.ComputePricePct(150, 20, 10, 400),
                            "M만으로는 75 → 하한 100 (M16 시절과 동일)");
            Assert.AreEqual(125, WorldModel.ComputePricePct(150, 20, 10, 400, 100, 1f),
                            "발행 100동이 붙으면 (150+100)/200 = 125% — 명세 §W3 검산치");
            Assert.AreEqual(400, WorldModel.ComputePricePct(900, 20, 10, 400, 200, 1f), "상한 클램프");
        }

        [Test]
        public void M17_T1_ComputePricePct_NeutralWhenNoMintDebt()
        {
            // 발행 축이 꺼진 판(부채 0 또는 k=0)은 M16과 **완전히** 같아야 한다.
            // 기존 게이트 M16-T5가 4인자 호출로 남아 있는 이유이기도 하다.
            foreach (int m in new[] { 0, 50, 150, 400, 5000 })
            {
                int baseline = WorldModel.ComputePricePct(m, 20, 10, 400);
                Assert.AreEqual(baseline, WorldModel.ComputePricePct(m, 20, 10, 400, 0, 1f), "부채 0");
                Assert.AreEqual(baseline, WorldModel.ComputePricePct(m, 20, 10, 400, 999, 0f), "k = 0 (마찰 없음)");
            }
        }

        [Test]
        public void M17_T1_SplitPrice_MintShareNeverNegative_AndMatchesDefinition()
        {
            // ⚠️ "money + mint == total"은 잔여로 구하는 구현에서 **항등식**이라 무엇을 짜도
            // 통과한다. 실패할 수 있는 명제만 여기 둔다:
            //   ① 발행 몫이 음수가 아니다 = 부채가 늘어 물가가 내려가는 일은 없다 (clamp 단조성)
            //   ② 도는 돈 몫의 정의 = 부채를 뺀 물가와 정확히 같다
            //   ③ 총합의 정의 = 부채를 넣은 물가와 정확히 같다
            foreach (int m in new[] { 0, 150, 300, 900, 5000 })
                foreach (int debt in new[] { 0, 50, 200, 1000 })
                    foreach (int q in new[] { 1, 20, 60 })
                    {
                        (int money, int mint) = WorldModel.SplitPrice(m, debt, 1f, q, 10, 400);
                        string at = $"M {m} · 부채 {debt} · Q {q}";
                        Assert.IsTrue(mint >= 0, $"발행 몫 음수 — {at}");
                        Assert.AreEqual(WorldModel.ComputePricePct(m, q, 10, 400), money, $"도는 돈 몫 정의 — {at}");
                        Assert.AreEqual(WorldModel.ComputePricePct(m, q, 10, 400, debt, 1f), money + mint,
                                        $"총합 정의 — {at}");
                    }

            // 부채가 늘면 물가는 절대 내려가지 않는다 (같은 M·Q에서 단조 증가)
            int prev = 0;
            foreach (int debt in new[] { 0, 50, 100, 200, 400 })
            {
                int p = WorldModel.ComputePricePct(300, 20, 10, 400, debt, 1f);
                Assert.IsTrue(p >= prev, $"부채 {debt}에서 물가가 내려갔다 ({p} < {prev})");
                prev = p;
            }

            // clamp 밖의 대표 케이스 — 두 몫이 실제로 갈리는 것을 눈으로 박제
            (int mo, int mi) = WorldModel.SplitPrice(300, 100, 1f, 20, 10, 400);
            Assert.AreEqual(150, mo, "도는 돈 몫");
            Assert.AreEqual(50, mi, "발행 여파 몫");
        }

        // ── T6: 화면 (순수 — M17-W5) ─────────────────────────────────────────

        [Test]
        public void M17_T6_PriceSuffix_SilentInPeacetime()
        {
            Assert.AreEqual(string.Empty, SeasonHud.ComposePriceSuffix(100, -1, 0, 0), "예보 없음·기준가");
            Assert.AreEqual(string.Empty, SeasonHud.ComposePriceSuffix(100, 100, 100, 0), "예보 == 확정");
        }

        [Test]
        public void M17_T6_PriceSuffix_ShowsBothDirections()
        {
            // ⚠️ 내려가는 예보(▼)를 빼면 세율의 효과가 화면에 영영 안 나온다 (ADR-M17-7).
            string up = SeasonHud.ComposePriceSuffix(120, 135, 120, 0);
            StringAssert.Contains("▲", up);
            StringAssert.Contains("내일 ×1.35", up);

            string down = SeasonHud.ComposePriceSuffix(120, 110, 120, 0);
            StringAssert.Contains("▼", down, "좋은 소식을 숨기면 손잡이가 장식이 된다");
            StringAssert.Contains("내일 ×1.1", down);
            StringAssert.DoesNotContain("▲", down);
        }

        [Test]
        public void M17_T6_PriceSuffix_SplitOnlyWhenMintDebtBites()
        {
            StringAssert.DoesNotContain("발행 여파", SeasonHud.ComposePriceSuffix(120, -1, 120, 0),
                                        "부채 0이면 괄호 없음 (평시 줄 길이 보호)");
            string split = SeasonHud.ComposePriceSuffix(145, -1, 125, 20);
            StringAssert.Contains("도는 돈 ×1.25", split);
            // 라벨이 인과를 말해야 한다 — "발행 여파"는 무엇 때문인지 안 알려 준다 (M17-R7)
            StringAssert.Contains("찍은 탓 ×0.2", split);
        }

        [Test]
        public void M17_T6_PriceSuffix_AtCap_ShowsDebtInsteadOfDistortedSplit()
        {
            // §8.5 발견 A·B: 물가가 상한에 닿으면 예보도 같은 값이 되어 화살표가 사라지고,
            // 더 찍어도 화면이 안 변한다. 게다가 그 구간의 분해는 두 몫이 모두 클램프에 걸려
            // 실제 기여도와 다르다. 부채는 클램프 밖이라 계속 움직인다 — 그게 남은 신호다.
            string capped = SeasonHud.ComposePriceSuffix(400, 400, 100, 300, mintDebt: 1200, capPct: 400,
                                                         debtDaysLeft: 9);
            StringAssert.Contains("(상한)", capped, "포화 사실 자체가 보여야 한다");
            StringAssert.Contains("찍은 여파 12은", capped, "클램프 밖의 값 = 유일하게 움직이는 신호");
            StringAssert.Contains("9일 뒤 사라짐", capped, "기다릴지 세금을 올릴지의 판단 재료");
            StringAssert.DoesNotContain("도는 돈", capped, "상한에서는 분해가 왜곡이라 내지 않는다");

            // 상한 아래에서는 기존대로 분해를 낸다 (부채가 있어도)
            string below = SeasonHud.ComposePriceSuffix(145, -1, 125, 20, mintDebt: 300, capPct: 400);
            StringAssert.DoesNotContain("(상한)", below);
            StringAssert.Contains("도는 돈 ×1.25", below);
            StringAssert.DoesNotContain("발행 부채", below);

            // capPct 미지정(기존 호출) = 상한 판정 없음 — 회귀 방지
            StringAssert.DoesNotContain("(상한)", SeasonHud.ComposePriceSuffix(400, -1, 100, 300));
        }

        [Test]
        public void M17_T6_Treasury_HiddenWhenUnset_ShownWithTaxStage()
        {
            Assert.AreEqual(string.Empty, SeasonHud.ComposeTreasury(-1, 0),
                            "미표기 = 기존 3인자 Compose 호출·게이트 불변");
            StringAssert.Contains("금고 3은 42동", SeasonHud.ComposeTreasury(342, 15));
            StringAssert.Contains("세율 보통 15%", SeasonHud.ComposeTreasury(342, 15));
            StringAssert.Contains("금고 0동", SeasonHud.ComposeTreasury(0, 0), "빈 금고도 정보다");
            StringAssert.Contains("세율 면세 0%", SeasonHud.ComposeTreasury(0, 0));
        }

        [Test]
        public void M17_T6_Compose_DefaultsUnchangedFromM16()
        {
            // 기존 게이트(M6-T·M13·M14)가 3인자 Compose에 문자열 동등을 걸고 있다 —
            // 새 인자의 기본값이 옛 출력을 그대로 내야 한다.
            Assert.AreEqual("Day 4", SeasonHud.Compose(4.2f, null, 3f));
            Assert.AreEqual("Day 4", SeasonHud.Compose(4.2f, null, 3f, 100),
                            "물가 기준가 = 접미사 없음 (M16 동작)");
        }

        [Test]
        public void M17_T6_TaxStageName_MatchesGrumbleThreshold()
        {
            // 이름과 불평 조건이 같은 상수를 봐야 화면과 말풍선이 어긋나지 않는다.
            Assert.AreEqual("면세", SeasonHud.TaxStageName(0));
            Assert.AreEqual("보통", SeasonHud.TaxStageName(15));
            Assert.AreEqual("중과", SeasonHud.TaxStageName(SeasonHud.HeavyTaxPct));
            Assert.AreEqual("중과", SeasonHud.TaxStageName(30));
            Assert.AreEqual("보통", SeasonHud.TaxStageName(SeasonHud.HeavyTaxPct - 1),
                            "문턱 직전은 아직 보통 — 불평도 안 나온다");
        }

        // ── T7: 연대기의 경제 한 줄 (순수 — M17-W6) ──────────────────────────

        [Test]
        public void M17_T7_RunEconomy_SilentOnPreCurrencyRuns()
        {
            // 구버전 아카이브(화폐 이전)는 세 값이 전부 0이다 — 줄이 생기면 안 된다 (M15 표기 규약).
            Assert.AreEqual(string.Empty, SeasonHud.ComposeRunEconomy(0, 0, 0), "화폐 이전 기록");
            Assert.AreEqual(string.Empty, SeasonHud.ComposeRunEconomy(100, 0, 0), "물가 무변동 판");
        }

        [Test]
        public void M17_T7_RunEconomy_SplitsTheStory()
        {
            // 세수와 발행의 비율이 곧 서사다 — 굴린 판과 찍은 판이 목록에서 갈려야 한다.
            string taxRun = SeasonHud.ComposeRunEconomy(240, 1240, 0);
            StringAssert.Contains("최고 물가 ×2.4", taxRun);
            StringAssert.Contains("세수 12은 40동", taxRun);
            StringAssert.DoesNotContain("발행", taxRun, "안 찍은 판에는 발행 항목이 없다");

            string mintRun = SeasonHud.ComposeRunEconomy(400, 0, 10000);
            StringAssert.Contains("발행 1금", mintRun);
            StringAssert.DoesNotContain("세수", mintRun, "면세 판에는 세수 항목이 없다");
        }

        [Test]
        public void M17_T7_RunEconomy_PeakPriceFinallyReachesScreen()
        {
            // 🔴 PeakPricePct는 M16-W6에서 기록되고도 화면에 나온 적이 없었다 (W6 실사에서 발견).
            // 이 게이트가 그 회귀를 막는다 — 기록만 하고 안 보여 주는 것은 M13이 진단한 실패다.
            StringAssert.Contains("최고 물가 ×2.4", SeasonHud.ComposeRunEconomy(240, 0, 0));
        }

        // ── T9: 선불 부탁의 짝 (M17-R4) ──────────────────────────────────────

        [Test]
        public void M17_T9_AlwaysUpfront_RequiresGuaranteedAbilityToPay()
        {
            // AlwaysUpfront에는 짝이 있다: **켰으면 지불 능력이 부탁 성립 조건으로 보장돼야 한다.**
            // 안 그러면 선불이 조용히 실패해 후불로 되돌아가고, 빚이 다시 생겨 켠 의미가 사라진다
            // (그 빚은 반경 3의 우연에 걸려 영영 안 갚아진다 — Play 관측 2026-08-01, 아사).
            var requests = UnityEditor.AssetDatabase.FindAssets("t:RequestSO", new[] { "Assets/M0Config/Requests" })
                .Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
                .Select(UnityEditor.AssetDatabase.LoadAssetAtPath<RequestSO>)
                .Where(r => r != null)
                .ToList();
            Assert.IsNotEmpty(requests, "부탁 에셋 로드");

            foreach (RequestSO r in requests)
            {
                if (!r.AlwaysUpfront || r.RewardCostAmount <= 0) continue;

                // M18 개정 (2026-08-04): 보장 형태가 둘이 됐다 — ①상수 'MyMoney ≥ 기준가'(舊)
                // ②슬롯 비교 'MyMoney ≥ HomePriceNow'(신). ②가 보장인 이유: 성립 판정의
                // HomePriceNow와 지불액 AcceptancePrice가 **같은 기준가 × 같은 하루 1회 물가**를
                // 읽는다 (ADR-M18-2·3) — 스캔과 지불이 같은 날이면 액수가 같을 수밖에 없다.
                bool guaranteed = r.RequesterConditions != null && r.RequesterConditions.Any(
                    c => c.Slot == r.RewardCostSlot
                         && c.Op == CompareOp.GreaterOrEqual
                         && (c.CompareToSlot
                                 ? c.RightSlot == SlotId.HomePriceNow
                                 : c.Value >= r.RewardCostAmount));
                Assert.IsTrue(guaranteed,
                    $"{r.name}: AlwaysUpfront인데 의뢰인 조건이 지불 능력을 보장하지 않는다 — " +
                    $"'{r.RewardCostSlot} ≥ {r.RewardCostAmount}(상수)' 또는 " +
                    "'≥ HomePriceNow(슬롯 비교)'가 RequesterConditions에 있어야 한다");
            }
        }

        // (M17_T9_HouseRequests_LeaveNoDebt는 M19-W1에서 삭제 — 집 부탁 선불(M17-R4)은
        //  화폐 빚 아사의 처방이었고, 실물 사례 복원(ADR-M19-2)으로 후불 원형이 정답이 됐다.)

        // ── T8: 빚 슬롯의 분류와 스냅샷 주입 (M17-W7) ────────────────────────

        [Test]
        public void M17_T8_MyDebt_IsNotAPossession()
        {
            // 빚은 소지품이 아니라 관계다. IsPersonalStock에 들어가면 TransferTo·CanPayReward가
            // 빚을 재화처럼 다뤄 "빚을 남에게 주는" 경로가 열린다.
            Assert.IsFalse(SlotIds.IsPersonalStock(SlotId.MyDebt), "이전 대상 아님");
            Assert.IsFalse(SlotIds.IsStock(SlotId.MyDebt), "전역 스톡 아님 (라우팅 오염 금지)");
            Assert.IsFalse(SlotIds.IsHomeStock(SlotId.MyDebt), "집에 저장되지 않는다");
            Assert.IsTrue(SlotIds.IsNumeric(SlotId.MyDebt), "수치형 — goal 트리거가 읽는다");
            Assert.AreEqual(38, (int)SlotId.MyDebt, "기존 인덱스 뒤 append (ADR-M0-9)");
        }

        [Test]
        public void M17_T8_MyDebt_ReachesThePlanner()
        {
            // 🔴 M16-W5에서 MyMoney를 스냅샷에 안 넣어 직거래 조건이 영원히 불성립할 뻔했다.
            // 주입이 없으면 트리거가 늘 0을 읽어 Goal_RepayDebt가 **절대** 안 뜨는데,
            // 컴파일도 다른 게이트도 그걸 못 잡는다. 여기서 잡는다.
            var world = new WorldModel(null, null);

            WorldSnapshot none = world.BuildSnapshot(50, 10);
            Assert.AreEqual(0, none.Slots[(int)SlotId.MyDebt], "기본 0 = 화폐 이전과 동일 (중립)");

            WorldSnapshot owing = world.BuildSnapshot(50, 10, myMoney: 12, myDebt: 50);
            Assert.AreEqual(50, owing.Slots[(int)SlotId.MyDebt], "빚이 플래너 입력에 도착한다");
            Assert.AreEqual(12, owing.Slots[(int)SlotId.MyMoney], "지갑 주입은 그대로 (M16-W5 회귀 방지)");
        }

        // ── T10: 발행 한도 = 물가 산식의 역함수 (M17-R6, §8.5 발견 A) ─────────

        [Test]
        public void M17_T10_MintHeadroom_StopsExactlyAtTheCap()
        {
            // Q 20 · 기준가 10 · 상한 400 → 분자 예산 = 400×20×10/100 = 800
            // M 150 · k 1 → 부채 한도 650. 100동씩이면 6회.
            int room = WorldModel.MintHeadroom(150, 0, 1f, 20, 10, 400);
            Assert.AreEqual(650, room, "여력 = (상한 예산 800 − M 150) ÷ k");

            // 여력을 다 쓰면 물가가 **정확히** 상한에 닿는다 = 한도가 산식과 어긋나지 않는다.
            // 그 너머는 클램프 구간(더 찍어도 물가가 안 움직임 = 공짜)이라 애초에 막는다.
            Assert.AreEqual(400, WorldModel.ComputePricePct(150, 20, 10, 400, room, 1f), "정확히 상한");
            Assert.AreEqual(399, WorldModel.ComputePricePct(150, 20, 10, 400, room - 2, 1f),
                            "한 뼘 덜 찍으면 상한 아래 — 경계가 1동 단위로 맞다");

            // 이미 진 부채는 여력에서 빠진다
            Assert.AreEqual(450, WorldModel.MintHeadroom(150, 200, 1f, 20, 10, 400));
            Assert.AreEqual(0, WorldModel.MintHeadroom(150, 650, 1f, 20, 10, 400), "한도 소진");
            Assert.AreEqual(0, WorldModel.MintHeadroom(150, 5000, 1f, 20, 10, 400), "초과해도 음수 아님");
        }

        [Test]
        public void M17_T10_MintHeadroom_RecoversAndScalesWithVillage()
        {
            // ①부채가 감쇠하면 여력이 돌아온다 = 회복 곡선이 곧 발행 여유
            int tight = WorldModel.MintHeadroom(150, 600, 1f, 20, 10, 400);
            int afterDecay = WorldModel.MintHeadroom(150, WorldModel.DecayMintDebt(600, 50), 1f, 20, 10, 400);
            Assert.Greater(afterDecay, tight, "부채가 반으로 줄면 여력이 늘어야 한다");

            // ②마을이 커지면(식량 Q↑) 더 찍을 수 있다 = 경제 규모에 비례한 발행 여력
            Assert.Greater(WorldModel.MintHeadroom(150, 0, 1f, 40, 10, 400),
                           WorldModel.MintHeadroom(150, 0, 1f, 20, 10, 400));

            // ③도는 돈이 많을수록 여력이 준다 (이미 물가를 밀고 있으므로)
            Assert.Less(WorldModel.MintHeadroom(600, 0, 1f, 20, 10, 400),
                        WorldModel.MintHeadroom(150, 0, 1f, 20, 10, 400));

            // k = 0 (마찰 없음)이면 한도가 없다 — 에셋의 명시적 선택
            Assert.AreEqual(int.MaxValue, WorldModel.MintHeadroom(150, 0, 0f, 20, 10, 400));

            // 전멸 직전(Q=0) 방어 — 음수 여력이 나오지 않는다
            Assert.GreaterOrEqual(WorldModel.MintHeadroom(9999, 0, 1f, 0, 10, 400), 0);
        }

        [Test]
        public void M17_T10_TreasuryLine_WarnsOnlyWhenRunningOut()
        {
            // 넉넉할 때의 큰 숫자는 잡음이다 — 얼마 안 남았을 때만 띄운다
            StringAssert.DoesNotContain("발행", SeasonHud.ComposeTreasury(500, 15, 99));
            StringAssert.DoesNotContain("발행", SeasonHud.ComposeTreasury(500, 15, -1), "미표기");
            StringAssert.Contains("발행 2회", SeasonHud.ComposeTreasury(500, 15, 2));
            StringAssert.Contains("발행 불가", SeasonHud.ComposeTreasury(500, 15, 0),
                                  "0회 = 세금 말고 길이 없다");
            StringAssert.Contains("발행", SeasonHud.ComposeTreasury(500, 15, SeasonHud.MintChargesWarnAt),
                                  "문턱 값은 포함");
        }

        // ── T4: 예보가 판정에 새지 않는다 (소스 스캔 — ADR-M17-3) ─────────────

        [Test]
        public void M17_T4_Forecast_NeverReadByGameLogic()
        {
            // 화면에 숫자가 둘이 되면 "판단 규칙 이원화"가 고전적으로 새는 지점이다 —
            // 누군가 편의상 예보로 판정하면 화면과 실제가 어긋난다. 사람이 아니라 게이트가 막는다.
            // 허용 = 선언·캐시가 있는 SimulationLoop, 그리고 표시 계층(UI/).
            string root = Path.Combine(Application.dataPath, "Scripts/M0");
            var hits = new List<string>();
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string norm = file.Replace('\\', '/');
                bool allowed = norm.EndsWith("/SimulationLoop.cs") || norm.Contains("/M0/UI/");
                if (allowed) continue;

                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                    if (lines[i].Contains("PriceForecastPct"))
                        hits.Add($"{norm}:{i + 1}: {lines[i].Trim()}");
            }
            Assert.IsEmpty(hits,
                "예보값이 판정 계층에 새어 들어갔다 (ADR-M17-3 위반 — 판정은 PricePct 하나만 읽는다):\n"
                + string.Join("\n", hits));
        }

        // ── T3: 발행 부채의 감쇠 곡선 (순수 — ADR-M17-4) ──────────────────────

        [Test]
        public void M17_T3_DecayMintDebt_Curve()
        {
            Assert.AreEqual(50, WorldModel.DecayMintDebt(100, 50), "제안치 50%/일");
            Assert.AreEqual(12, WorldModel.DecayMintDebt(25, 50),  "12.5 내림");
            Assert.AreEqual(0,  WorldModel.DecayMintDebt(6, 50),   "3 → 5 미만이라 떨군다");
            Assert.AreEqual(80, WorldModel.DecayMintDebt(100, 20), "다른 감쇠율도 곡선은 같다");
            Assert.AreEqual(0,  WorldModel.DecayMintDebt(0, 50));
            Assert.AreEqual(0,  WorldModel.DecayMintDebt(-10, 50), "음수 방어");
            Assert.AreEqual(100, WorldModel.DecayMintDebt(100, 0), "감쇠 0% = 영구 부채 (에셋의 선택)");
            Assert.AreEqual(0,  WorldModel.DecayMintDebt(100, 100), "전량 감쇠");
        }

        [Test]
        public void M17_T3_MintDebt_ReachesZero_NotStuckAtDust()
        {
            // 감쇠가 **곱셈**이라 체감이 직관과 어긋난다. 명세 초안은 "20%/일 → 5일이면 소멸"
            // 이라고 적었는데 실제로는 5일 뒤가 32이고 0까지 13일이 걸린다 — 겨울이 4일인
            // 이 게임에서는 사실상 영구 페널티다. 제안치를 50%/일로 바로잡은 근거가 이 계산이다.
            int slow = 100;
            for (int day = 1; day <= 5; day++) slow = WorldModel.DecayMintDebt(slow, 20);
            Assert.AreEqual(32, slow, "20%/일은 5일 뒤에도 32가 남는다 (100·0.8⁵ = 32.8)");

            // 50%/일 = 의도한 곡선: 100 → 50 → 25 → 12 → 6 → 0 (5 미만 절단이 꼬리를 끊는다)
            int fast = 100;
            int[] expected = { 50, 25, 12, 6, 0 };
            for (int day = 0; day < expected.Length; day++)
            {
                fast = WorldModel.DecayMintDebt(fast, 50);
                Assert.AreEqual(expected[day], fast, $"{day + 1}일째");
            }
        }

        [Test]
        public void M17_T3_IssueCurrency_FillsChestAndDebt_ButNotMoneySupply()
        {
            var world = new WorldModel(null, null);

            world.IssueCurrency(100, "게이트");

            Assert.AreEqual(100, world.Treasury,    "금고가 찬다");
            Assert.AreEqual(100, world.MintDebt,    "쓰기 전부터 여파가 붙는다");
            Assert.AreEqual(100, world.IssuedTotal, "찍은 몫만 따로 센다 (연대기 W6용)");
            Assert.AreEqual(100, world.MintedTotal, "무에서 나왔으므로 발행 누적에도 실린다");
            Assert.AreEqual(0,   world.MoneySupply, "ADR-M17-1 — 시중에는 아직 안 나갔다");
            Assert.IsTrue(WorldModel.IsLedgerBalanced(world.MintedTotal, world.MoneySupply,
                                                      world.Treasury, world.BurnedTotal),
                          "MintDebt는 실물 돈이 아니므로 폐곡선 밖이다");
        }

        [Test]
        public void M17_T11_DaysToClear_CountsWhatNobodyCanDoInTheirHead()
        {
            // 감쇠가 곱셈이라 "며칠 참으면 되는가"를 사람이 암산할 수 없다.
            // 100동 · 50%/일 = 100→50→25→12→6→0 이므로 5일 (M17-T3와 같은 곡선).
            Assert.AreEqual(5, WorldModel.MintDebtDaysToClear(100, 50));
            Assert.AreEqual(0, WorldModel.MintDebtDaysToClear(0, 50), "부채 없음 = 0일");
            Assert.AreEqual(0, WorldModel.MintDebtDaysToClear(-5, 50), "음수 방어");
            Assert.AreEqual(-1, WorldModel.MintDebtDaysToClear(100, 0), "감쇠 0% = 영구 (-1)");
            Assert.AreEqual(1, WorldModel.MintDebtDaysToClear(100, 100), "전량 감쇠 = 하루");

            // 20%/일이면 훨씬 오래 간다 — 제안치를 50으로 바로잡은 근거가 이 곡선이다
            Assert.Greater(WorldModel.MintDebtDaysToClear(100, 20),
                           WorldModel.MintDebtDaysToClear(100, 50));
        }

        [Test]
        public void M17_T3_MintDebt_NotReducedBySpending()
        {
            // ⚠️ ADR-M17-4의 핵심: 찍은 돈을 써도 부채는 그대로다. 차감하면 발행 마찰이
            // 사라져 기각한 "마찰 없음" 안으로 되돌아간다. 줄어드는 길은 감쇠뿐이다.
            var world = new WorldModel(null, null);
            world.IssueCurrency(100, "게이트");

            // 지급 실패 경로(대상 null)로도, 성공 경로로도 부채는 건드려지지 않는다
            world.PayFromTreasury(null, 50, "게이트");
            Assert.AreEqual(100, world.MintDebt, "지급 시도는 부채를 안 건드린다");

            world.TickMintDebtDecay(50);
            Assert.AreEqual(50, world.MintDebt, "줄어드는 유일한 길 = 하루 감쇠");
        }

        // ── T5-c: 금고 흐름의 실제 회계 (인스턴스 — 지갑을 안 건드리는 경로만) ──

        [Test]
        public void M17_T5_MintToTreasury_LeavesMoneySupplyUntouched()
        {
            var world = new WorldModel(null, null); // 회계만 쓰므로 서비스 주입 불필요

            world.MintToTreasury(100, "게이트");

            Assert.AreEqual(100, world.Treasury, "금고 적립");
            Assert.AreEqual(100, world.MintedTotal, "무에서 나왔으므로 발행 누적에 실린다");
            Assert.AreEqual(0, world.MoneySupply, "ADR-M17-1 — 잠긴 돈은 시중에 없는 돈");
            Assert.IsTrue(WorldModel.IsLedgerBalanced(world.MintedTotal, world.MoneySupply,
                                                      world.Treasury, world.BurnedTotal));
        }

        [Test]
        public void M17_T5_PayFromTreasury_FailsClosedOnEmptyChest()
        {
            var world = new WorldModel(null, null);
            world.MintToTreasury(5, "게이트");

            // 주민 없이(null) 호출 = 지급 대상 부재. 어떤 항도 움직이면 안 된다.
            Assert.IsFalse(world.PayFromTreasury(null, 10, "게이트"), "대상 없음");
            Assert.AreEqual(5, world.Treasury, "실패 시 금고 불변");
            Assert.AreEqual(0, world.MoneySupply, "실패 시 통화량 불변");
            Assert.AreEqual(5, world.MintedTotal, "실패 시 발행 누적 불변");
        }

        [Test]
        public void M17_T5_Burn_CountsTowardBurnedTotal()
        {
            var world = new WorldModel(null, null);
            world.MintToTreasury(30, "게이트"); // 금고에만 넣고 M은 0

            world.Burn(10, "게이트"); // M이 0이라 NextSupply가 0으로 클램프한다

            Assert.AreEqual(0, world.MoneySupply, "음수 클램프 (M16-T2 계승)");
            Assert.AreEqual(10, world.BurnedTotal, "소멸은 클램프와 무관하게 누적된다");
            // ⚠️ 이 상태는 폐곡선이 깨진 상태가 맞다 — 도는 돈이 없는데 태웠기 때문이다.
            // 실전에서는 지갑에 있던 돈만 태우므로(BurnWalletOnDeath) 발생하지 않는다.
            Assert.IsFalse(WorldModel.IsLedgerBalanced(world.MintedTotal, world.MoneySupply,
                                                       world.Treasury, world.BurnedTotal),
                           "없는 돈을 태우면 검산이 깨진다 = 탐지기가 살아 있다는 증거");
        }
    }
}
