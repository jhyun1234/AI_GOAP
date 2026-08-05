using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M19 실물 마을 게이트 — 화폐 철거 후의 보장.
    /// W2: 직업 독점 부재(ADR-M19-3)와 효율 배율의 중립 불변식.
    /// (W6에서 슬롯 37~39 잔존 감시·실물 사슬·나눔 무상 검사가 추가된다.)
    /// </summary>
    public class M19_RealVillageGates
    {
        // ── T3: 독점 부재 (ADR-M19-3 — ADR-M5-4 원상 복구) ───────────────────

        [Test]
        public void M19_T3_RequiredJob_OnlyTreatmentRemains()
        {
            // "직업은 강한 선호 + 공용 폴백 — 어떤 goal도 직업 전용으로 잠그지 않는다"(ADR-M5-4).
            // 남은 예외는 치료 goal 하나 (보완① — 사망 착탄과 한 몸이라 M20에서 재설계).
            // 두 번째 예외가 다시 생기면 red — 독점의 재발 방지선.
            int locked = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:GoalSO", new[] { "Assets/M0Config" }))
            {
                var goal = AssetDatabase.LoadAssetAtPath<GoalSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (goal == null || goal.RequiredJob == null) continue;
                locked++;
                Assert.AreEqual("Goal_TreatInjured", goal.name,
                    $"{goal.name}: RequiredJob 독점은 치료 goal만 허용된다 (ADR-M19-3)");
            }
            Assert.AreEqual(1, locked, "RequiredJob 사용 goal은 정확히 1곳(치료)이어야 한다");
        }

        // ── T1: 화폐 슬롯 잔존 감시 (W6) — 휴면 3칸(37~39)을 아무도 다시 만지지 않는다 ──

        /// <summary>휴면 화폐 슬롯 여부 — 이 게이트의 판정 단일 출처.</summary>
        private static bool IsDormantMoneySlot(SlotId slot)
            => slot == SlotId.MyMoney || slot == SlotId.MyDebt || slot == SlotId.HomePriceNow;

        private static bool TouchesDormantSlot(SlotCondition[] conditions)
        {
            if (conditions == null) return false;
            foreach (SlotCondition c in conditions)
                if (IsDormantMoneySlot(c.Slot) || (c.CompareToSlot && IsDormantMoneySlot(c.RightSlot)))
                    return true;
            return false;
        }

        private static bool TouchesDormantSlot(SlotEffect[] effects)
        {
            if (effects == null) return false;
            foreach (SlotEffect e in effects)
                if (IsDormantMoneySlot(e.Slot)) return true;
            return false;
        }

        [Test]
        public void M19_T1_Detector_FlagsDormantSlotUse()
        {
            // 위반 실증 (M17 "실패 불가능한 테스트" 교훈) — 탐지기가 실제로 잡는가
            Assert.IsTrue(TouchesDormantSlot(new[] { new SlotCondition { Slot = SlotId.MyMoney } }), "좌변");
            Assert.IsTrue(TouchesDormantSlot(new[] { new SlotCondition
                { Slot = SlotId.MySatiety, CompareToSlot = true, RightSlot = SlotId.HomePriceNow } }), "우변");
            Assert.IsTrue(TouchesDormantSlot(new[] { new SlotEffect { Slot = SlotId.MyDebt } }), "효과");
            Assert.IsFalse(TouchesDormantSlot(new[] { new SlotCondition { Slot = SlotId.MyRawFood } }), "오탐");
        }

        [Test]
        public void M19_T1_NoAssetTouchesDormantMoneySlots()
        {
            // 전체 에셋 스캔 — 조건·효과·보상·나눔 슬롯 어디에도 37~39가 없다 (S1의 에셋판).
            // 휴면 슬롯은 스냅샷이 항상 0이라, 참조가 생기면 "영구 불발 또는 항상 발동"으로
            // 조용히 썩는다 — 컴파일도 못 잡으므로 이 스캔이 유일한 방어선이다.
            foreach (string guid in AssetDatabase.FindAssets("t:GoalSO", new[] { "Assets/M0Config" }))
            {
                var g = AssetDatabase.LoadAssetAtPath<GoalSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (g == null) continue;
                Assert.IsFalse(TouchesDormantSlot(g.TriggerConditions), $"{g.name}: 트리거가 휴면 슬롯 참조");
                Assert.IsFalse(TouchesDormantSlot(g.GoalConditions), $"{g.name}: 목표가 휴면 슬롯 참조");
            }
            foreach (string guid in AssetDatabase.FindAssets("t:ActionSO", new[] { "Assets/M0Config" }))
            {
                var a = AssetDatabase.LoadAssetAtPath<ActionSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (a == null) continue;
                Assert.IsFalse(TouchesDormantSlot(a.Preconditions), $"{a.name}: 전제가 휴면 슬롯 참조");
                Assert.IsFalse(TouchesDormantSlot(a.Effects), $"{a.name}: 효과가 휴면 슬롯 참조");
            }
            foreach (string guid in AssetDatabase.FindAssets("t:RequestSO", new[] { "Assets/M0Config" }))
            {
                var r = AssetDatabase.LoadAssetAtPath<RequestSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (r == null) continue;
                Assert.IsFalse(TouchesDormantSlot(r.RequesterConditions), $"{r.name}: 의뢰인 조건");
                Assert.IsFalse(TouchesDormantSlot(r.TargetConditions), $"{r.name}: 대상 조건");
                Assert.IsFalse(TouchesDormantSlot(r.TraitBypassConditions), $"{r.name}: 우회 조건");
                Assert.IsFalse(IsDormantMoneySlot(r.RewardCostSlot), $"{r.name}: 보상 슬롯이 돈");
                Assert.IsFalse(r.TradeGiveAmount > 0 && IsDormantMoneySlot(r.TradeGiveSlot),
                               $"{r.name}: 나눔 슬롯이 돈");
            }
            foreach (string guid in AssetDatabase.FindAssets("t:RewardSO", new[] { "Assets/M0Config" }))
            {
                var w = AssetDatabase.LoadAssetAtPath<RewardSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (w != null)
                    Assert.IsFalse(IsDormantMoneySlot(w.CostSlot), $"{w.name}: 보상 비용이 돈");
            }
        }

        // ── 舊 T2: 실물 집 사슬 (W6 — ADR-M19-2) — M20-W12에서 삭제 ─────────────
        // 감시 대상(집 소유를 배정하는 부탁)이 설계상 소멸했다: ADR-M20-7 "집은 개인이
        // 짓는다"로 집 부탁 사슬이 폐기됐고, M20_T5가 정반대("집 소유 배정 부탁 금지")를
        // 같은 값-기반 스캔으로 감시한다. ADR-M19-2의 정신(대가는 실물)은 화폐 슬롯 잔존
        // 감시(T1)가 계속 지킨다.

        // ── T4: 나눔 무상 (W6) — 나눔은 거래가 아니다 (ADR-M19-4) ──

        [Test]
        public void M19_T4_ShareRequests_AreFree()
        {
            int shares = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:RequestSO", new[] { "Assets/M0Config" }))
            {
                var r = AssetDatabase.LoadAssetAtPath<RequestSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (r == null || r.TradeGiveAmount <= 0) continue;
                shares++;
                Assert.AreEqual(0, r.RewardCostAmount,
                    $"{r.name}: 나눔에 반대급부가 붙었다 — 화폐의 재발명 (ADR-M19-4)");
            }
            Assert.Greater(shares, 0, "나눔 부탁 에셋이 없다 — 굶주림 구제 통로 소실");
        }

        // ── T5: 옛 판 연대기 표시 호환 (W4 게이트 정리에서 이관 — 2분류 표의 "이관 필요" 4건) ──
        // 화폐는 철거됐지만 **옛 판의 기록은 표시돼야 한다** (ADR-M14-3 append-only의 정신).
        // ComposeMoney·ComposeRunEconomy는 그래서 생존 — 이 게이트들이 그 계약을 지킨다.

        [Test]
        public void M19_T5_ComposeMoney_TieredDisplay_ForOldChronicles()
        {
            // (M16-T8 이관) 1은 = 100동, 1금 = 100은. 옛 연대기의 "집값 지불(70동)"이 읽힌다.
            Assert.AreEqual("0동", SeasonHud.ComposeMoney(0));
            Assert.AreEqual("50동", SeasonHud.ComposeMoney(50));
            Assert.AreEqual("1은", SeasonHud.ComposeMoney(100), "0 단위 생략");
            Assert.AreEqual("2은 47동", SeasonHud.ComposeMoney(247));
            Assert.AreEqual("1금 2은 47동", SeasonHud.ComposeMoney(10247));
            Assert.AreEqual("1금 47동", SeasonHud.ComposeMoney(10047), "가운데 단위만 0이면 건너뛴다");
            Assert.AreEqual("0동", SeasonHud.ComposeMoney(-5), "음수 방어");
        }

        [Test]
        public void M19_T5_RunEconomy_SilentOnNewRuns_ShownOnOldRuns()
        {
            // (M17-T7 3종 이관) 새 판(M19 이후)은 100/0/0 기록 = 경제 줄 자체가 없다.
            Assert.AreEqual(string.Empty, SeasonHud.ComposeRunEconomy(0, 0, 0), "빈 기록");
            Assert.AreEqual(string.Empty, SeasonHud.ComposeRunEconomy(100, 0, 0), "M19 이후 새 판");
            // 옛 판(화폐 시대 아카이브)은 그대로 읽힌다 — 기록은 다른 이야기가 되면 안 된다.
            string taxRun = SeasonHud.ComposeRunEconomy(240, 1240, 0);
            StringAssert.Contains("최고 물가 ×2.4", taxRun);
            StringAssert.Contains("세수 12은 40동", taxRun);
            StringAssert.DoesNotContain("발행", taxRun, "안 찍은 판에는 발행 항목이 없다");
            string mintRun = SeasonHud.ComposeRunEconomy(400, 0, 10000);
            StringAssert.Contains("발행 1금", mintRun);
            StringAssert.DoesNotContain("세수", mintRun, "면세 판에는 세수 항목이 없다");
        }

        // ── T3b: 효율 배율의 중립 불변식 (M5-S3) ─────────────────────────────

        [Test]
        public void M19_T3b_BuildDurationMult_NeutralByDefault()
        {
            // 새 JobSO의 기본값 = 1 (배율을 모르는 직업·기존 에셋 전부 현행 속도 그대로)
            var fresh = ScriptableObject.CreateInstance<JobSO>();
            Assert.AreEqual(1f, fresh.BuildDurationMult, "신규 JobSO 기본 = 1 (중립)");
            Object.DestroyImmediate(fresh);

            // 에셋 전수 — 목수만 보너스(0.5), 나머지는 전부 1 (일반이 느려지는 페널티 방향 금지:
            // BuildRunner는 모닥불·밭도 타므로 페널티는 기존 건설 전체를 느리게 한다 — 명세 W2)
            int carpenters = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:JobSO", new[] { "Assets/M0Config/Jobs" }))
            {
                var job = AssetDatabase.LoadAssetAtPath<JobSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (job == null) continue;
                if (job.name == "Job_Carpenter")
                {
                    carpenters++;
                    Assert.AreEqual(0.5f, job.BuildDurationMult, "목수 = 0.5 (제안치, 명세 보완③)");
                }
                else
                {
                    Assert.AreEqual(1f, job.BuildDurationMult,
                        $"{job.name}: 목수 외 배율은 1 — 페널티 방향은 중립 불변식 위반");
                }
            }
            Assert.AreEqual(1, carpenters, "Job_Carpenter 에셋 존재");
        }
    }
}
