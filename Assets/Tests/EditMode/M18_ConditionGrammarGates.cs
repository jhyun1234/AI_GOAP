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

        // (T2 집값 기준가·T3 수락 시점 실가격·T4 에셋 실배선 게이트는 M19에서 삭제 —
        //  검사 대상(HomePriceNow 계산·TradePrice·AcceptancePrice·화폐 goal)이 화폐와 함께
        //  철거됐다. 슬롯 비교 문법의 의미론은 T1 계열이 계속 지킨다.)

        // ── T5: 연대기 사건의 append-only (W5 → M19 개정: 표시 검사는 M19-T5로 이관) ──

        [Test]
        public void M18_T5_EventIds_AppendOnly()
        {
            // append-only (ADR-M13-2) — 기존 값이 밀렸다면 저장된 연대기가 다른 이야기가 된다.
            // HomePaid(7)는 M19에서 휴면(기록 지점 없음)이지만 값은 영원히 7이다.
            Assert.AreEqual(6, (int)EventId.Traded, "기존 사건 정수 불변");
            Assert.AreEqual(7, (int)EventId.HomePaid, "휴면이어도 값 불변");

            // 옛 판 표시 호환 — 기록만 남고 표시가 죽으면 옛 연대기가 침묵한다
            string shown = SeasonHud.KrEvent(new ChronicleEvent { Kind = EventId.HomePaid, Value = 70 });
            StringAssert.Contains("집값 지불", shown);
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
