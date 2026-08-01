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
