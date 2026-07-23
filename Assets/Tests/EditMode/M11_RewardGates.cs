using AIVillage.M0;
using NUnit.Framework;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M11-H 보상 실이전 게이트 (M11-T7) — 지급 능력 판정과 정산 분기.
    /// 핵심 불변식 = **떼먹기(성격)와 파산(잔고)은 다른 축이다** (ADR-M11-4):
    /// 가난한 온순 성격은 연기되고, 부유한 고집쟁이는 떼먹는다.
    /// </summary>
    public class M11_RewardGates
    {
        // ── 지급 능력 (VillagerAgent.CanPay — 순수) ─────────────────────────────

        [Test]
        public void M11_T7_CanPay_BodyOnly()
        {
            Assert.IsTrue(VillagerAgent.CanPay(5, 0, 5), "몸만으로 정확히 충당");
            Assert.IsTrue(VillagerAgent.CanPay(6, 0, 5));
        }

        [Test]
        public void M11_T7_CanPay_HomeOnly()
            => Assert.IsTrue(VillagerAgent.CanPay(0, 5, 5), "집 저장만으로도 지급 가능 (몸+집이 잔고)");

        [Test]
        public void M11_T7_CanPay_Combined()
        {
            Assert.IsTrue(VillagerAgent.CanPay(2, 3, 5), "몸 우선 + 부족분 집 = 합산 판정");
            Assert.IsFalse(VillagerAgent.CanPay(2, 2, 5), "합산이 모자라면 불가");
        }

        [Test]
        public void M11_T7_CanPay_NoRewardIsAlwaysPayable()
        {
            Assert.IsTrue(VillagerAgent.CanPay(0, 0, 0), "보상 0 = 낼 것이 없다 (항상 true)");
            Assert.IsTrue(VillagerAgent.CanPay(0, 0, -1), "음수 방어");
        }

        // ── 정산 분기 (RequestService.ResolveSettlement — 순수) ──────────────────

        [Test]
        public void M11_T7_Resolve_PaysWhenAbleAndRoom()
            => Assert.AreEqual(RequestService.Settlement.Pay,
                RequestService.ResolveSettlement(false, false, false, true, true));

        [Test]
        public void M11_T7_Resolve_DefersOnEmptyBalance()
            => Assert.AreEqual(RequestService.Settlement.Defer,
                RequestService.ResolveSettlement(false, false, false, canPay: false, hasRoom: true),
                "잔고 부족 = 연기 (빚 유지 — 감사 대사로 소멸시키지 않는다)");

        [Test]
        public void M11_T7_Resolve_DefersWhenReceiverIsFull()
            => Assert.AreEqual(RequestService.Settlement.Defer,
                RequestService.ResolveSettlement(false, false, false, canPay: true, hasRoom: false),
                "수령 공간 부족도 연기 — 수행자가 먹어서 자리가 나면 자연 정산");

        [Test]
        public void M11_T7_Resolve_StiffIsIndependentOfBalance()
        {
            // 부유한 떼먹기 성격 → 떼먹기 (잔고가 있어도)
            Assert.AreEqual(RequestService.Settlement.Stiff,
                RequestService.ResolveSettlement(false, false, stiff: true, canPay: true, hasRoom: true));
            // 가난한 떼먹기 성격 → 여전히 떼먹기 (파산이 떼먹기로 둔갑하지 않게 판정 순서가 앞)
            Assert.AreEqual(RequestService.Settlement.Stiff,
                RequestService.ResolveSettlement(false, false, stiff: true, canPay: false, hasRoom: true));
            // 가난한 중립 성격 → 떼먹기가 아니라 연기 (관계 델타 없음 — 축 분리의 핵심)
            Assert.AreEqual(RequestService.Settlement.Defer,
                RequestService.ResolveSettlement(false, false, stiff: false, canPay: false, hasRoom: true));
        }

        [Test]
        public void M11_T7_Resolve_ThanksShortCircuits()
        {
            Assert.AreEqual(RequestService.Settlement.Thanks,
                RequestService.ResolveSettlement(noReward: true, false, true, false, false),
                "보상 없는 부탁은 떼먹기·연기 판정을 타지 않는다");
            Assert.AreEqual(RequestService.Settlement.Thanks,
                RequestService.ResolveSettlement(false, prepaid: true, true, false, false),
                "선불 완료도 감사만 — 이중 지급 차단");
        }

        [Test]
        public void M11_T7_StiffReward_NeutralPersonalityNeverStiffs()
        {
            var neutral = ScriptableObject.CreateInstance<PersonalitySO>(); // 기본 -100
            Assert.IsFalse(RequestService.ShouldStiffReward(neutral, -50), "기본값은 판정 성립 불가");
            Assert.IsFalse(RequestService.ShouldStiffReward(null, -100), "성격 없음 = 안 떼먹음");
        }

        // ── 보상 슬롯 규약 (에셋 실수 방어) ──────────────────────────────────────

        [Test]
        public void M11_T7_RewardSlot_MustBePersonalStock()
        {
            Assert.IsTrue(SlotIds.IsPersonalStock(SlotId.MyRawFood), "보상 슬롯은 개인 스톡이어야");
            Assert.IsFalse(SlotIds.IsPersonalStock(SlotId.CookedFoodStock),
                           "전역 식량은 휴면 — 보상 경로가 다시 전역을 읽으면 S1 위반");
        }
    }
}
