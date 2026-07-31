using AIVillage.M0;
using NUnit.Framework;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M16 화폐 게이트 — 회계 축의 3대 보장:
    /// 지갑 슬롯 분류(T1 — IsStock에 새면 EffectApplier가 전역 라우팅해 돈이 증발한다),
    /// 상한 예외(T1 — 돈에 BodyCarryCap이 걸리면 임금이 8에서 막힌다, 명세 실사 ④),
    /// 통화량 계단(T2 — 소멸이 발행을 넘어도 음수 금지).
    /// </summary>
    public class M16_CurrencyGates
    {
        [Test]
        public void M16_T1_MyMoney_SlotClassification()
        {
            Assert.IsTrue(SlotIds.IsPersonalStock(SlotId.MyMoney), "이전·지급 판정 공유 (TransferTo)");
            Assert.IsFalse(SlotIds.IsStock(SlotId.MyMoney), "전역 스톡 아님 — W1 ⚠️ (라우팅 오염 금지)");
            Assert.IsFalse(SlotIds.IsHomeStock(SlotId.MyMoney), "집 저장 없음 — 돈은 몸만");
            Assert.IsTrue(SlotIds.IsNumeric(SlotId.MyMoney));
        }

        [Test]
        public void M16_T1_PersonalCap_MoneyUnlimited_FoodStillCapped()
        {
            // 돈은 부피가 없다 — 상한 예외는 PersonalCapOf 한 곳 (BodyCarryCap 인상 금지)
            Assert.AreEqual(int.MaxValue, VillagerAgent.PersonalCapOf(SlotId.MyMoney, 8));
            Assert.AreEqual(8, VillagerAgent.PersonalCapOf(SlotId.MyRawFood, 8), "식량은 여전히 상한 8");

            // 큰 임금 적립이 상한에 막히지 않는다 / 식량은 기존 그대로 막힌다
            (bool okMoney, int nextMoney) = VillagerAgent.NextPersonalStock(
                1000, EffectOp.Add, 999999, VillagerAgent.PersonalCapOf(SlotId.MyMoney, 8));
            Assert.IsTrue(okMoney);
            Assert.AreEqual(1000999, nextMoney);
            (bool okFood, _) = VillagerAgent.NextPersonalStock(
                7, EffectOp.Add, 2, VillagerAgent.PersonalCapOf(SlotId.MyRawFood, 8));
            Assert.IsFalse(okFood, "식량 상한 초과는 기존대로 실패 (클램프 아님)");
        }

        [Test]
        public void M16_T2_NextSupply_ClampsAtZero()
        {
            Assert.AreEqual(5, WorldModel.NextSupply(0, 5), "발행");
            Assert.AreEqual(7, WorldModel.NextSupply(5, 2), "누적");
            Assert.AreEqual(2, WorldModel.NextSupply(5, -3), "소멸");
            Assert.AreEqual(0, WorldModel.NextSupply(3, -10), "소멸이 발행을 넘으면 0 (음수 금지)");
        }
    }
}
