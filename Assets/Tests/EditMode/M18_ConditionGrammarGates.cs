using System.Reflection;
using System.Text.RegularExpressions;
using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M18 조건 문법 게이트 — 슬롯 대 슬롯 비교(CompareToSlot)와 새 연산자(Less/Greater)의
    /// **트리거 전용** 보장 (ADR-M18-1).
    ///
    /// 왜 이 게이트가 밀스톤의 안전핀인가: GoalConditions·Preconditions는 플래너 잡이
    /// (int)Op 그대로 삼킨다 (ActionCompiler). 새 문법이 거기 들어가면 컴파일도 통과하고
    /// 게임도 돌지만 — 잡은 상수 Value로 목표를 세우고 관리 판정은 새 필드를 읽어
    /// **플래너 목표와 완수 판정이 갈린다** (판단 규칙 이원화). 증상은 몇 판 뒤에야 보인다.
    /// </summary>
    public class M18_ConditionGrammarGates
    {
        // ── T1-탐지기: 위반을 실제로 잡는가 (실패 가능성 증명 — M17 "실패 불가능한 테스트" 교훈).
        //    아래 에셋 스캔(T1a·T1b)이 전부 통과하는 것은 "위반이 없어서"임을 이 테스트가 보증한다.

        [Test]
        public void M18_T1_Detector_FlagsEveryViolationKind()
        {
            // 위반 ①: 슬롯 비교
            Assert.IsTrue(SlotCondition.UsesTriggerOnlyGrammar(new[]
            {
                new SlotCondition { Slot = SlotId.MyMoney, Op = CompareOp.GreaterOrEqual, CompareToSlot = true, RightSlot = SlotId.MyDebt },
            }), "CompareToSlot 미탐지");

            // 위반 ②·③: 새 연산자 (상수라도 금지 칸에선 불가 — 잡이 모르는 값 3·4)
            Assert.IsTrue(SlotCondition.UsesTriggerOnlyGrammar(new[]
            {
                new SlotCondition { Slot = SlotId.MyMoney, Op = CompareOp.Less, Value = 50 },
            }), "Less 미탐지");
            Assert.IsTrue(SlotCondition.UsesTriggerOnlyGrammar(new[]
            {
                new SlotCondition { Slot = SlotId.MyMoney, Op = CompareOp.Greater, Value = 50 },
            }), "Greater 미탐지");

            // 무혐의: 기존 3연산 상수 조건·빈 배열·null — 오탐이 있으면 기존 에셋 전체가 red가 된다
            Assert.IsFalse(SlotCondition.UsesTriggerOnlyGrammar(new[]
            {
                new SlotCondition { Slot = SlotId.MyMoney, Op = CompareOp.LessOrEqual, Value = 49 },
            }), "기존 문법 오탐");
            Assert.IsFalse(SlotCondition.UsesTriggerOnlyGrammar(new SlotCondition[0]), "빈 배열 오탐");
            Assert.IsFalse(SlotCondition.UsesTriggerOnlyGrammar(null), "null 오탐");
        }

        // ── T1a·T1b: 금지 2칸 에셋 전수 스캔 (경계 지도의 🔴 두 칸이 이 테스트의 명세다) ──

        [Test]
        public void M18_T1a_GoalAssets_GoalConditionsStayConstantOnly()
        {
            int scanned = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:GoalSO", new[] { "Assets/M0Config" }))
            {
                var goal = AssetDatabase.LoadAssetAtPath<GoalSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (goal == null) continue;
                scanned++;
                Assert.IsFalse(SlotCondition.UsesTriggerOnlyGrammar(goal.GoalConditions),
                    $"{goal.name}: GoalConditions에 트리거 전용 문법 — 잡과 완수 판정이 갈립니다 (ADR-M18-1)");
            }
            Assert.Greater(scanned, 0, "GoalSO 에셋을 하나도 못 찾음 — 스캔 경로가 낡았다");
        }

        [Test]
        public void M18_T1b_ActionAssets_PreconditionsStayConstantOnly()
        {
            int scanned = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ActionSO", new[] { "Assets/M0Config" }))
            {
                var action = AssetDatabase.LoadAssetAtPath<ActionSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (action == null) continue;
                scanned++;
                Assert.IsFalse(SlotCondition.UsesTriggerOnlyGrammar(action.Preconditions),
                    $"{action.name}: Preconditions에 트리거 전용 문법 — 잡은 상수 3연산만 압니다 (ADR-M18-1)");
            }
            Assert.Greater(scanned, 0, "ActionSO 에셋을 하나도 못 찾음 — 스캔 경로가 낡았다");
        }

        // ── T1c: AllHold 의미론 (순수) — 경계에서 미만/초과가 상수·슬롯 양쪽 모두 옳은가 ──

        private static WorldSnapshot Snap(int myMoney, int myDebt)
        {
            int[] slots = new int[PlanningConfig.TotalSlots];
            slots[(int)SlotId.MyMoney] = myMoney;
            slots[(int)SlotId.MyDebt]  = myDebt;
            return new WorldSnapshot(slots);
        }

        private static SlotCondition MoneyVsDebt(CompareOp op) => new SlotCondition
        { Slot = SlotId.MyMoney, Op = op, CompareToSlot = true, RightSlot = SlotId.MyDebt };

        [Test]
        public void M18_T1c_AllHold_SlotCompare_BoundaryExact()
        {
            // 경계(같은 값): 미만 false · 이상 true — "그만 모으기 vs 사러 가기" 동시 발동 방지의 핵심
            WorldSnapshot eq = Snap(70, 70);
            Assert.IsFalse(GoalSelector.AllHold(new[] { MoneyVsDebt(CompareOp.Less) }, eq), "70 < 70");
            Assert.IsTrue(GoalSelector.AllHold(new[] { MoneyVsDebt(CompareOp.GreaterOrEqual) }, eq), "70 ≥ 70");
            Assert.IsFalse(GoalSelector.AllHold(new[] { MoneyVsDebt(CompareOp.Greater) }, eq), "70 > 70");

            // 한 칸 아래: 미만 true (빚 축 — "돈이 빚보다 적으면 벌어라")
            Assert.IsTrue(GoalSelector.AllHold(new[] { MoneyVsDebt(CompareOp.Less) }, Snap(69, 70)), "69 < 70");
            // 한 칸 위: 초과 true (대칭 확인)
            Assert.IsTrue(GoalSelector.AllHold(new[] { MoneyVsDebt(CompareOp.Greater) }, Snap(71, 70)), "71 > 70");
        }

        [Test]
        public void M18_T1c_AllHold_ConstantPathUnchanged()
        {
            // CompareToSlot=false면 RightSlot은 완전히 무시된다 — 기존 에셋(역직렬화 기본값
            // RightSlot=0=WoodStock)의 동작 불변 증명. WoodStock=0인 스냅샷에서 상수 49와 비교.
            WorldSnapshot snap = Snap(49, 0);
            var legacy = new SlotCondition
            { Slot = SlotId.MyMoney, Op = CompareOp.LessOrEqual, Value = 49, RightSlot = SlotId.WoodStock };
            Assert.IsTrue(GoalSelector.AllHold(new[] { legacy }, snap), "상수 경로가 RightSlot(0)을 읽으면 49 ≤ 0으로 깨진다");

            // 새 연산자 + 상수 (허용 칸에서 유효한 조합)
            var lessConst = new SlotCondition { Slot = SlotId.MyMoney, Op = CompareOp.Less, Value = 50 };
            Assert.IsTrue(GoalSelector.AllHold(new[] { lessConst }, snap), "49 < 50");
            Assert.IsFalse(GoalSelector.AllHold(new[] { lessConst }, Snap(50, 0)), "50 < 50");
        }

        // ── T1d: 자기 비교 실수 스캔 — CompareToSlot=true && RightSlot == Slot 은 상수 참/거짓 ──

        [Test]
        public void M18_T1d_NoSelfCompareInAnyConditionArray()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:GoalSO", new[] { "Assets/M0Config" }))
            {
                var goal = AssetDatabase.LoadAssetAtPath<GoalSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (goal != null) AssertNoSelfCompare(goal.name, goal.TriggerConditions);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:RequestSO", new[] { "Assets/M0Config" }))
            {
                var r = AssetDatabase.LoadAssetAtPath<RequestSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (r == null) continue;
                AssertNoSelfCompare(r.name, r.RequesterConditions);
                AssertNoSelfCompare(r.name, r.TargetConditions);
                AssertNoSelfCompare(r.name, r.TraitBypassConditions);
            }
        }

        private static void AssertNoSelfCompare(string owner, SlotCondition[] conditions)
        {
            if (conditions == null) return;
            foreach (SlotCondition c in conditions)
                Assert.IsFalse(c.CompareToSlot && c.RightSlot == c.Slot,
                    $"{owner}: 슬롯 {c.Slot}을 자기 자신과 비교 — 상수 참/거짓이 됩니다 (RightSlot 설정 실수)");
        }

        // ── T2: HomePriceNow — 집값의 단일 출처 (ADR-M18-3) ──────────────────

        [Test]
        public void M18_T2_HomeBasePrice_SingleSourceAcrossAssets()
        {
            // 집 소유권 Request 에셋 전수 — 기준가가 갈리면 HomePriceNow가 "어느 집값"인지
            // 애매해진다 (런타임은 첫 항을 읽으므로 두 번째 값은 조용히 무시된다 — red가 정답)
            var prices = new System.Collections.Generic.List<int>();
            foreach (string guid in AssetDatabase.FindAssets("t:RequestSO", new[] { "Assets/M0Config" }))
            {
                var r = AssetDatabase.LoadAssetAtPath<RequestSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (r != null && r.GrantOwnership && r.OwnershipSlot == SlotId.HouseCount
                    && r.RewardCostSlot == SlotId.MyMoney && r.RewardCostAmount > 0)
                    prices.Add(r.RewardCostAmount);
            }
            Assert.Greater(prices.Count, 0, "집 소유권 Request 에셋이 없다 — 스캔 조건이 낡았다");
            foreach (int p in prices)
                Assert.AreEqual(prices[0], p, "집 부탁들의 기준가가 갈린다 — ADR-M18-3 위반");
        }

        [Test]
        public void M18_T2_DeriveHomeBasePrice_FiltersCorrectly()
        {
            // 집 소유권 + 화폐 보상만 기준가다 — 실물 보상·소유권 없는 부탁은 집값이 아니다
            var house = ScriptableObject.CreateInstance<RequestSO>();
            house.GrantOwnership = true; house.OwnershipSlot = SlotId.HouseCount;
            house.RewardCostSlot = SlotId.MyMoney; house.RewardCostAmount = 50;
            var cook = ScriptableObject.CreateInstance<RequestSO>();
            cook.RewardCostSlot = SlotId.MyRawFood; cook.RewardCostAmount = 5;

            Assert.AreEqual(50, WorldModel.DeriveHomeBasePrice(new[] { cook, house }), "집 부탁을 못 찾음");
            Assert.AreEqual(0, WorldModel.DeriveHomeBasePrice(new[] { cook }), "실물 부탁을 집값으로 오인");
            Assert.AreEqual(0, WorldModel.DeriveHomeBasePrice(null), "null 구성은 0 (중립)");

            Object.DestroyImmediate(house);
            Object.DestroyImmediate(cook);
        }

        [Test]
        public void M18_T2_HomePriceNow_TracksConfirmedPrice()
        {
            // 산식 = TradePrice 하나 (복제 금지 — M16-T6의 그 함수): 올림·100% 하한
            Assert.AreEqual(50, RequestService.TradePrice(50, 100), "중립 물가 = 기준가 그대로");
            Assert.AreEqual(70, RequestService.TradePrice(50, 140), "물가 140% → 70동");
            Assert.AreEqual(200, RequestService.TradePrice(50, 400), "캡 400% → 200동");

            // 스냅샷 통합 — 물가 140%를 주입한 WorldModel이 실가격 70을 내놓는가
            var cfg = ScriptableObject.CreateInstance<WorldConfigSO>();
            var house = ScriptableObject.CreateInstance<RequestSO>();
            house.GrantOwnership = true; house.OwnershipSlot = SlotId.HouseCount;
            house.RewardCostSlot = SlotId.MyMoney; house.RewardCostAmount = 50;
            cfg.Requests = new[] { house };

            var world = new WorldModel(null, cfg, pricePct: () => 140);
            Assert.AreEqual(70, world.BuildSnapshot(50, 50).Get(SlotId.HomePriceNow), "물가 140% 실가격");

            // 미주입 = 물가 100% (기존 상수 50과 동치 — 동작 불변의 다리)
            var unbound = new WorldModel(null, cfg);
            Assert.AreEqual(50, unbound.BuildSnapshot(50, 50).Get(SlotId.HomePriceNow), "미주입 = 기준가");

            // 집 부탁 없는 구성 = 0 (중립 — 참조 goal도 없는 구성이라는 뜻)
            var empty = new WorldModel(null, ScriptableObject.CreateInstance<WorldConfigSO>());
            Assert.AreEqual(0, empty.BuildSnapshot(50, 50).Get(SlotId.HomePriceNow), "부탁 없음 = 0");

            Object.DestroyImmediate(cfg);
            Object.DestroyImmediate(house);
        }

        // ── T3: 수락 시점 실가격 (ADR-M18-2) ─────────────────────────────────

        [Test]
        public void M18_T3_AcceptancePrice_MoneyOnly()
        {
            // 화폐 보상만 물가를 탄다
            Assert.AreEqual(70, RequestService.AcceptancePrice(SlotId.MyMoney, 50, 140), "집 부탁 140%");
            Assert.AreEqual(50, RequestService.AcceptancePrice(SlotId.MyMoney, 50, 100), "중립 물가");
            // 실물 보상은 액면 그대로 — 곱하면 요리 부탁 식량 5개가 7개가 된다 (ADR-M16-4)
            Assert.AreEqual(5, RequestService.AcceptancePrice(SlotId.MyRawFood, 5, 140), "실물 가드");
            Assert.AreEqual(5, RequestService.AcceptancePrice(SlotId.MyCookedFood, 5, 400), "실물 가드(캡)");
        }

        [Test]
        public void M18_T3_Settlement_JudgesAndTransfersSameAmount()
        {
            // 판정≡이전 동치 — 정산 분기(ResolveSettlement)의 canPay와 실제 이전이 같은
            // price 하나를 먹는 한, "판정은 통과했는데 이전이 실패"하는 액수 어긋남이 없다.
            // 여기서는 순수 분기가 price 기반 입력만으로 결정됨을 고정한다 (재계산 입력 금지의 형식화).
            int price = RequestService.AcceptancePrice(SlotId.MyMoney, 50, 140); // 70
            bool canPay = 70 >= price;   // 잔고 70 = 수락 시점 가격과 같은 기준
            Assert.AreEqual(RequestService.Settlement.Pay,
                RequestService.ResolveSettlement(price <= 0, false, false, canPay, true),
                "잔고 70·가격 70 → 지급");
            // 물가가 정산 시점에 더 올라도(재계산 금지) 판정 입력은 수락가 그대로 → 여전히 지급
            Assert.AreEqual(RequestService.Settlement.Pay,
                RequestService.ResolveSettlement(price <= 0, false, false, 70 >= price, true),
                "정산 시점 물가 무관 — 수락가 하나만 읽는다");
        }

        // ── T4: 에셋 실배선 (W4) — 집 사슬·빚이 실제로 파생 슬롯을 보는가 ──────

        private static WorldSnapshot HouseSnap(int myMoney, int homePrice)
        {
            int[] slots = new int[PlanningConfig.TotalSlots];
            slots[(int)SlotId.MyMoney]        = myMoney;
            slots[(int)SlotId.HomePriceNow]   = homePrice;
            slots[(int)SlotId.MyFoodDaysLeft] = 5;   // 저축 트리거의 "배는 부르다" 전제
            slots[(int)SlotId.DaysToFreeze]   = 30;  // 겨울 직전 저축 중단 전제 회피
            return new WorldSnapshot(slots);         // MyHasHome(17) = 0 = 무주택
        }

        [Test]
        public void M18_T4_SaveAndSeek_NoOverlapAtBoundary()
        {
            var save = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_SaveForHome.asset");
            var seek = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_SeekCarpenter.asset");
            Assert.IsNotNull(save); Assert.IsNotNull(seek);

            // 물가 140% 세계 (실가격 70): 69동 = 아직 모은다, 사러 가지 않는다
            Assert.IsTrue(GoalSelector.AllHold(save.TriggerConditions, HouseSnap(69, 70)), "69 < 70 저축");
            Assert.IsFalse(GoalSelector.AllHold(seek.TriggerConditions, HouseSnap(69, 70)), "69로 구매 불가");
            // 정확히 70동 = 저축은 꺼지고 구매만 켜진다 — Less 도입의 존재 이유 (경계 동시 발동 0)
            Assert.IsFalse(GoalSelector.AllHold(save.TriggerConditions, HouseSnap(70, 70)), "70 도달 — 저축 종료");
            Assert.IsTrue(GoalSelector.AllHold(seek.TriggerConditions, HouseSnap(70, 70)), "70 도달 — 사러 간다");
            // 물가 100% 세계 (실가격 50): 기존 상수 49/50과 동일 경계 — 동작 불변의 다리
            Assert.IsTrue(GoalSelector.AllHold(save.TriggerConditions, HouseSnap(49, 50)), "49 < 50 저축(기존 동치)");
            Assert.IsTrue(GoalSelector.AllHold(seek.TriggerConditions, HouseSnap(50, 50)), "50 도달(기존 동치)");
        }

        [Test]
        public void M18_T4_RepayDebt_ComparesDebtNotHomePrice()
        {
            var repay = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_RepayDebt.asset");
            Assert.IsNotNull(repay);

            int[] slots = new int[PlanningConfig.TotalSlots];
            slots[(int)SlotId.MyDebt]         = 70;  // 수락 시점 실가격으로 진 빚 (W3)
            slots[(int)SlotId.MyMoney]        = 60;
            slots[(int)SlotId.MyFoodDaysLeft] = 5;
            // ⚠️ HomePriceNow를 0으로 둔 채 발동해야 한다 — 우변이 집값(39)으로 잘못 배선됐다면
            // "60 < 0"이 되어 여기서 false가 난다 (명세 ⚠️W4-① 기계적 교체 실수의 탐지기)
            Assert.IsTrue(GoalSelector.AllHold(repay.TriggerConditions, new WorldSnapshot(slots)), "빚 70·소지 60 발동");

            slots[(int)SlotId.MyMoney] = 70; // 갚을 만큼 모임 — 벌이 goal 종료
            Assert.IsFalse(GoalSelector.AllHold(repay.TriggerConditions, new WorldSnapshot(slots)), "빚만큼 모이면 미발동");

            slots[(int)SlotId.MyDebt] = 0; slots[(int)SlotId.MyMoney] = 0; // 빚 없음 — 첫 조건이 막는다
            Assert.IsFalse(GoalSelector.AllHold(repay.TriggerConditions, new WorldSnapshot(slots)), "무채무 미발동");
        }

        // ── T5: 연대기 HomePaid (W5) ─────────────────────────────────────────

        [Test]
        public void M18_T5_HomePaid_AppendOnlyAndVisible()
        {
            // append-only (ADR-M13-2) — 기존 값이 밀렸다면 저장된 연대기가 다른 이야기가 된다
            Assert.AreEqual(6, (int)EventId.Traded, "기존 사건 정수 불변");
            Assert.AreEqual(7, (int)EventId.HomePaid, "신규는 뒤에만");

            // 표시 — 기록만 하고 안 보여 주는 것은 M13이 진단한 실패 (M17-T7 선례)
            string shown = SeasonHud.KrEvent(new ChronicleEvent { Kind = EventId.HomePaid, Value = 70 });
            StringAssert.Contains("집값 지불", shown);
            StringAssert.Contains("70", shown, "판마다 다른 액수가 화면 언어에 실려야 한다");
        }

        // ── T1e: OnValidate 배선 — 에디터에서 저장 순간 ADR-M18-1 에러가 실제로 뜨는가 ──

        [Test]
        public void M18_T1e_OnValidate_RejectsForbiddenCells()
        {
            // GoalSO — private OnValidate를 리플렉션 호출 (에디터 저장 경로 재현)
            var goal = ScriptableObject.CreateInstance<GoalSO>();
            goal.GoalConditions = new[] { MoneyVsDebt(CompareOp.GreaterOrEqual) };
            LogAssert.Expect(LogType.Error, new Regex("ADR-M18-1"));
            typeof(GoalSO).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic)
                          .Invoke(goal, null);
            Object.DestroyImmediate(goal);

            // ActionSO — protected virtual OnValidate (자기 OnValidate가 없는 RestActionSO로)
            var action = ScriptableObject.CreateInstance<RestActionSO>();
            action.Preconditions = new[] { new SlotCondition { Slot = SlotId.MyMoney, Op = CompareOp.Less, Value = 50 } };
            LogAssert.Expect(LogType.Error, new Regex("ADR-M18-1"));
            typeof(ActionSO).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(action, null);
            Object.DestroyImmediate(action);
        }
    }
}
