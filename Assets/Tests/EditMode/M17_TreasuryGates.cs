using System.Collections.Generic;
using System.IO;
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

        [Test]
        public void M17_T2_TaxStageName_ShowsFeelingNotNumber()
        {
            Assert.AreEqual("면세", M0SimulationLoop.TaxStageName(0));
            Assert.AreEqual("보통", M0SimulationLoop.TaxStageName(15));
            Assert.AreEqual("중과", M0SimulationLoop.TaxStageName(30));
            Assert.AreEqual("중과", M0SimulationLoop.TaxStageName(90), "상한 근처도 중과");
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
