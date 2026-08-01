using AIVillage.M0;
using NUnit.Framework;

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
