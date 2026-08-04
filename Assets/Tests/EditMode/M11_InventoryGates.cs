using System.Collections.Generic;
using AIVillage.M0;
using NUnit.Framework;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M11-A 개인 인벤토리 선반 게이트 (M11-T1) — 몸 소지 계단·집 저장 원자성·컴파일러 상한
    /// 전제 주입. 판정은 순수 함수/서비스 직접 검증 (M10-T1과 동일 사상).
    /// 상한은 슬롯별이다 (명세 §4.2 '합산'에서 개정 — 플래너 전제 언어가 단일 슬롯 비교라
    /// 합산은 표현 불가, 판정 이원화 금지. 사유 = M11-A 커밋).
    /// </summary>
    public class M11_InventoryGates
    {
        // ── 몸 소지 계단 (VillagerAgent.NextPersonalStock — 순수) ──────────────

        [Test]
        public void M11_T1_NextPersonalStock_SubInsufficientFailsUnchanged()
        {
            (bool ok, int next) = VillagerAgent.NextPersonalStock(2, EffectOp.SubClamp0, 3, 8);
            Assert.IsFalse(ok, "부족 차감 = 실패");
            Assert.AreEqual(2, next, "실패 시 무변경 (원자성)");

            (ok, next) = VillagerAgent.NextPersonalStock(3, EffectOp.SubClamp0, 3, 8);
            Assert.IsTrue(ok, "정확히 잔량만큼은 성공");
            Assert.AreEqual(0, next);
        }

        [Test]
        public void M11_T1_NextPersonalStock_AddOverCapFailsNoSilentLoss()
        {
            // 상한 초과 Add = 실패 반환 (클램프로 초과분을 조용히 버리지 않는다 — 명세 M11-A DoD)
            (bool ok, int next) = VillagerAgent.NextPersonalStock(8, EffectOp.Add, 1, 8);
            Assert.IsFalse(ok, "가득 찬 몸에 Add = 실패");
            Assert.AreEqual(8, next, "실패 시 무변경 — 소실 없음");

            (ok, next) = VillagerAgent.NextPersonalStock(7, EffectOp.Add, 1, 8);
            Assert.IsTrue(ok, "상한 정확히 도달은 성공");
            Assert.AreEqual(8, next);

            (ok, _) = VillagerAgent.NextPersonalStock(0, EffectOp.Set, 5, 8);
            Assert.IsFalse(ok, "Set 미지원 (전역 스톡과 동일)");
        }

        // ── 집 저장 (HomeStorageService — 원자성·타일 키) ──────────────────────

        [Test]
        public void M11_T1_HomeStorage_AddCapAndSpendAtomicity()
        {
            var storage = new HomeStorageService();
            var tile = new Vector2Int(3, 4);

            Assert.IsTrue(storage.TryAdd(tile, SlotId.MyHomeRawFood, 10, 15), "입고");
            Assert.IsFalse(storage.TryAdd(tile, SlotId.MyHomeRawFood, 6, 15), "상한(15) 초과 = 실패");
            Assert.AreEqual(10, storage.GetStock(tile, SlotId.MyHomeRawFood), "실패 시 무변경");

            Assert.IsTrue(storage.TryAdd(tile, SlotId.MyHomeCookedFood, 15, 15),
                "상한은 슬롯별 — 생식 10이어도 조리식은 독립 상한");

            Assert.IsFalse(storage.TrySpend(tile, SlotId.MyHomeRawFood, 11), "부족 출고 = 실패");
            Assert.AreEqual(10, storage.GetStock(tile, SlotId.MyHomeRawFood), "실패 시 무변경");
            Assert.IsTrue(storage.TrySpend(tile, SlotId.MyHomeRawFood, 10), "전량 출고");
            Assert.AreEqual(0, storage.GetStock(tile, SlotId.MyHomeRawFood));

            Assert.AreEqual(0, storage.GetStock(new Vector2Int(9, 9), SlotId.MyHomeRawFood),
                "미등록 타일 = 0 (중립)");

            Assert.IsFalse(storage.TryAdd(tile, SlotId.MyRawFood, 1, 15),
                "몸 소지 슬롯은 거부 — 집 저장 문은 IsHomeStock 전용 (라우팅 오염 방지)");
        }

        // ── 컴파일러 상한 전제 주입 (ActionCompiler.InjectCapPreconditions — 순수) ──

        [Test]
        public void M11_T1_InjectCap_PersonalAddGetsPrecondition()
        {
            var precs = new List<SlotCondition>();
            var effs = new List<SlotEffect>
            {
                new SlotEffect { Slot = SlotId.MyRawFood, Op = EffectOp.Add, Value = 8 },
            };
            ActionCompiler.InjectCapPreconditions(precs, effs, bodyCap: 8, homeCap: 15);

            Assert.AreEqual(1, precs.Count, "개인 스톡 Add → 전제 1개 주입");
            Assert.AreEqual(SlotId.MyRawFood, precs[0].Slot);
            Assert.AreEqual(CompareOp.LessOrEqual, precs[0].Op);
            Assert.AreEqual(0, precs[0].Value, "Add 8 + cap 8 → '≤ 0' (명세 M11-A DoD)");
        }

        [Test]
        public void M11_T1_InjectCap_HomeAddUsesHomeCap_SubIgnored()
        {
            var precs = new List<SlotCondition>();
            var effs = new List<SlotEffect>
            {
                new SlotEffect { Slot = SlotId.MyHomeRawFood, Op = EffectOp.Add, Value = 3 },
                new SlotEffect { Slot = SlotId.MyRawFood, Op = EffectOp.SubClamp0, Value = 3 }, // Sub는 주입 없음
            };
            ActionCompiler.InjectCapPreconditions(precs, effs, bodyCap: 8, homeCap: 15);

            Assert.AreEqual(1, precs.Count, "집 Add 1건만 주입 (Sub는 대상 아님)");
            Assert.AreEqual(SlotId.MyHomeRawFood, precs[0].Slot);
            Assert.AreEqual(12, precs[0].Value, "Add 3 + homeCap 15 → '≤ 12'");
        }

        [Test]
        public void M11_T1_InjectCap_NeutralInvariants()
        {
            // 개인 스톡 효과 없는 액션 → 입력 그대로 (중립 불변식, 명세 M11-A DoD)
            var precs = new List<SlotCondition>
            {
                new SlotCondition { Slot = SlotId.WoodStock, Op = CompareOp.GreaterOrEqual, Value = 5 },
            };
            var effs = new List<SlotEffect>
            {
                new SlotEffect { Slot = SlotId.WoodStock, Op = EffectOp.Add, Value = 10 },
                new SlotEffect { Slot = SlotId.MySatiety, Op = EffectOp.Add, Value = 15 },
            };
            ActionCompiler.InjectCapPreconditions(precs, effs, bodyCap: 8, homeCap: 15);
            Assert.AreEqual(1, precs.Count, "전역 스톡·욕구 Add는 주입 대상 아님 — 기존 액션 불변");

            // cap 0 = 주입 없음 (미배선 게이트웨이 = 기존 동작)
            var precs2 = new List<SlotCondition>();
            var effs2 = new List<SlotEffect>
            {
                new SlotEffect { Slot = SlotId.MyRawFood, Op = EffectOp.Add, Value = 1 },
            };
            ActionCompiler.InjectCapPreconditions(precs2, effs2, bodyCap: 0, homeCap: 0);
            Assert.AreEqual(0, precs2.Count, "cap 0 = 중립 (주입 없음)");
        }

        // ── 식량 가치 파생 (M11-B, M11-T2 — 개인 슬롯 인식) ────────────────────

        [Test]
        public void M11_T2_TryGetFoodValue_RecognizesPersonalStock()
        {
            // 개인화된 소비 액션 (EatRawFood 개편형): MyRawFood Sub 1 + MySatiety Add 15
            var eat = ScriptableObject.CreateInstance<ConsumeActionSO>();
            eat.Preconditions = new[]
            {
                new SlotCondition { Slot = SlotId.MyRawFood, Op = CompareOp.GreaterOrEqual, Value = 1 },
            };
            eat.Effects = new[]
            {
                new SlotEffect { Slot = SlotId.MyRawFood, Op = EffectOp.SubClamp0, Value = 1 },
                new SlotEffect { Slot = SlotId.MySatiety, Op = EffectOp.Add, Value = 15 },
            };

            Assert.IsTrue(WorldModel.TryGetFoodValue(eat, out SlotId slot, out int gain),
                "개인 스톡 소비도 식량으로 파생 (M11-B — FoodDaysLeft 산식의 입력 유지)");
            Assert.AreEqual(SlotId.MyRawFood, slot);
            Assert.AreEqual(15, gain);

            Object.DestroyImmediate(eat);
        }

        // ── 슬롯 판정 경계 (명세 M11-A ⚠️① — IsStock 오염 금지) ─────────────────

        [Test]
        public void M11_T1_SlotJudgments_PersonalNotGlobalStock()
        {
            Assert.IsFalse(SlotIds.IsStock(SlotId.MyRawFood),
                "개인 스톡은 IsStock이 아니다 — 선검사가 WorldModel을 읽으면 개인 식사 전부 실패 (⚠️①)");
            Assert.IsFalse(SlotIds.IsStock(SlotId.MyHomeRawFood));
            Assert.IsTrue(SlotIds.IsPersonalStock(SlotId.MyRawFood));
            Assert.IsTrue(SlotIds.IsPersonalStock(SlotId.MyCookedFood));
            Assert.IsFalse(SlotIds.IsPersonalStock(SlotId.MyHomeRawFood), "집 저장은 별도 판정 (ADR-M11-2)");
            Assert.IsTrue(SlotIds.IsHomeStock(SlotId.MyHomeRawFood));
            Assert.IsTrue(SlotIds.IsHomeStock(SlotId.MyHomeCookedFood));
            Assert.IsFalse(SlotIds.IsPersonalStock(SlotId.MyDebt),
                "빚은 소지품이 아니다 (M17-W7) — 넣으면 TransferTo가 '빚을 남에게 주는' 경로를 연다");
            Assert.AreEqual(40, SlotIds.Count,
                "슬롯 예산 40/52 (M18-W2 HomePriceNow 추가 — 2026-08-04)");
        }
    }
}
