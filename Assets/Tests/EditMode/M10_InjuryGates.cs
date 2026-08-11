using AIVillage.M0;
using NUnit.Framework;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M10-A 부상 축 게이트 (M10-T1) — 최초의 사망 축.
    /// 판정은 순수 함수 4종(계단·문턱·진입 가드·goal 필터)뿐 — M6-T3 이탈 게이트와 동일 사상.
    /// 이탈=굶주림·사망=부상의 결말 이원화(ADR-M10-3)는 각 계단이 자기 원인만 읽는 것으로 보장된다.
    /// </summary>
    public class M10_InjuryGates
    {
        // 🔴 게이트 개정 (2026-08-11 2차 감사): 舊 M10_T1_NextInjuryState 2판(v1 계단) 삭제 —
        // 관측: W7R 간호 대개정으로 부상 상태 기계의 정본이 V2("회복은 치료사만·안정화 유예")가
        // 됐고 v1은 프로덕션 호출 0. v1이 지키던 성질(홀드는 리셋이 아니다·자연 회복 없음)은
        // V2 게이트(M11_CareGates·M26B_T10)가 현행 설계 위에서 검증한다.

        /// <summary>M21-W1 재보정 — 부상 사망 판정이 방치 일수 문턱에서 **체력 0**으로 옮겨졌다.
        /// 여기서 지키는 것은 "부상 방치가 에셋의 사망 문턱 일수 만에 끝나는가"다
        /// (舊 ShouldDie는 삭제 — 아무도 안 쓰는 함수를 지키지 않는다).</summary>
        [Test]
        public void M10_T1_InjuryDeath_TimingPreserved()
        {
            var cfg = ScriptableObject.CreateInstance<AgentConfigSO>();
            cfg.InjuryDeathAfterDays = 1.5f;
            cfg.MaxHp = 100f;
            cfg.InjuredBelowHp = 67f;
            cfg.BleedHpLossPerDay = 0f; // 0 = 자동 환산

            Assert.AreEqual(1.5f, VillagerAgent.DaysToBleedDeath(cfg), 1e-4f,
                "부상 진입 체력이 출혈만으로 정확히 사망 문턱 일수 만에 0이 되어야 한다");

            // M21-W2 재보정: "진입 피해"라는 것은 더는 없다 — 체력을 깎는 것은 때린 쪽이고
            // 부상은 그 결과로 파생된다. 여기서 지킬 것은 부상선의 성질뿐이다.
            Assert.IsFalse(VillagerAgent.IsDead(cfg.InjuredBelowHp), "부상은 즉사가 아니다");
            Assert.Less(cfg.InjuredBelowHp, cfg.MaxHp, "부상선이 만복 이상이면 태어나자마자 부상이다");

            cfg.BleedHpLossPerDay = 67f;
            Assert.AreEqual(1f, VillagerAgent.DaysToBleedDeath(cfg), 1e-4f, "명시값 우선");

            Object.DestroyImmediate(cfg);
        }

        [Test]
        public void M10_T1_CanInjure_DuplicateAndDeadIgnored()
        {
            Assert.IsTrue(VillagerAgent.CanInjure(AgentState.Idle, InjurySeverity.None), "정상 진입");
            Assert.IsTrue(VillagerAgent.CanInjure(AgentState.Acting, InjurySeverity.None), "작업 중도 진입 (플랜 중단은 Injure가 수행)");
            Assert.IsFalse(VillagerAgent.CanInjure(AgentState.Idle, InjurySeverity.Light), "중복 부상 무시 (M10 단일 심각도)");
            Assert.IsFalse(VillagerAgent.CanInjure(AgentState.Dead, InjurySeverity.None), "Dead 무시");
        }

        [Test]
        public void M10_T2_Catalog_MinBaseCost_HeuristicFloor()
        {
            // 2026-07-22 석재 goal 탐색 폭발의 재발 방지 게이트: 플래너 잡 휴리스틱이
            // h = 스텝 추정 × min(카탈로그 BaseCost)라, 카탈로그에 저비용 액션이 들어오면
            // 전 goal의 휴리스틱이 일괄 수축해 A*가 무유도로 퍼진다 (MAX_NODES 폭발 → NoSolution).
            // 5 = 현 카탈로그 최소선(EatCookedFood) — 이 아래로 내리려면 대부족량 goal
            // (Goal_GatherStone 등)의 탐색 예산을 함께 검증하고 이 게이트를 의도적으로 개정할 것.
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<ActionCatalog>(
                "Assets/M0Config/ActionCatalog.asset");
            Assert.IsNotNull(catalog, "실제 카탈로그 에셋 로드");

            float min = float.MaxValue;
            string minName = "?";
            foreach (ActionSO a in catalog.Actions)
            {
                Assert.IsNotNull(a, "카탈로그 빈 슬롯 없음");
                if (a.BaseCost < min) { min = a.BaseCost; minName = a.name; }
            }
            Assert.GreaterOrEqual(min, 5f,
                $"카탈로그 최소 BaseCost 붕괴 — '{minName}'({min})이 휴리스틱 바닥을 뚫는다 (본 게이트 주석 참조)");
        }

        [Test]
        public void M10_T2_EffectiveCost_NeverBreaksHeuristicFloor()
        {
            // 위 게이트의 사각지대 보강 (2026-07-26): 에셋 BaseCost가 5 이상이어도 성격×직업×편차의
            // 곱셈 누적이 실효 비용을 그 아래로 끌어내린다 — 오늘도 농사꾼 성격 0.7 × 농부 직업 0.6
            // × 편차 0.9 = 0.378이라 BaseCost 5 액션이 실효 1.89다. 하한 = 카탈로그 min.
            const float catalogMin = 5f;

            Assert.AreEqual(catalogMin, PlannerGateway.EffectiveCost(5f, 0.378f, catalogMin), 0.001f,
                "최저 배율에서도 실효 비용이 카탈로그 min 아래로 못 내려간다");
            Assert.AreEqual(catalogMin, PlannerGateway.EffectiveCost(5f, 0.26f, catalogMin), 0.001f,
                "성향 축이 곱을 하나 더 얹어도(0.378×0.7) 바닥 불변");
            Assert.AreEqual(7f, PlannerGateway.EffectiveCost(10f, 0.7f, catalogMin), 0.001f,
                "min보다 비싼 액션은 배율이 그대로 작동한다 (차별화 보존)");
            Assert.AreEqual(15f, PlannerGateway.EffectiveCost(10f, 1.5f, catalogMin), 0.001f,
                "비용 증가 배율은 하한과 무관");
            Assert.AreEqual(10f, PlannerGateway.EffectiveCost(10f, 1f, 0f), 0.001f,
                "카탈로그 min 0(미배선) = 하한 없음 — 중립 불변식");
        }

        // ── M10-T2 간호 (M10-B) ───────────────────────────────────────────────

        [Test]
        public void M10_T2_PickNearestIndex_DistanceOrderAndDeterministicTie()
        {
            var candidates = new System.Collections.Generic.List<(string id, int x, int y)>
            {
                ("M0_Villager_C", 5, 5), // 거리² 50
                ("M0_Villager_A", 3, 0), // 거리² 9 — 최근접
                ("M0_Villager_B", 0, 4), // 거리² 16
            };
            Assert.AreEqual(1, M0SimulationLoop.PickNearestIndex(0, 0, candidates), "거리 오름차순 — 최근접 선택");

            // 동률 — AgentId 사전순 (ordinal, 결정적 — ADR-M10-1: 희생·대상 선정에 랜덤 금지)
            var tie = new System.Collections.Generic.List<(string id, int x, int y)>
            {
                ("M0_Villager_B", 3, 0),
                ("M0_Villager_A", 0, 3), // 같은 거리² 9 — id 사전순 앞
            };
            Assert.AreEqual(1, M0SimulationLoop.PickNearestIndex(0, 0, tie), "거리 동률 = id 사전순");

            // 빈 목록 — 대상 없음 (TendRunner Prepare 실패 경로)
            var empty = new System.Collections.Generic.List<(string id, int x, int y)>();
            Assert.AreEqual(-1, M0SimulationLoop.PickNearestIndex(0, 0, empty), "부상자 0 = -1");
        }

        [Test]
        public void M10_T2_TendRecoveryMult_DefaultNeutral()
        {
            // 직업 미설정 = 배율 1 (중립 불변식 — 간호는 직업 전용이 아니다, 결정 11).
            // TendRunner의 "Job null → 1f" 경로와 함께 이 기본값이 일반 주민 간호를 보장한다.
            var job = ScriptableObject.CreateInstance<JobSO>();
            Assert.AreEqual(1f, job.TendRecoveryMult, 1e-5f, "신규 직업 기본 배율 = 1 (가속은 에셋 값으로만)");
            Object.DestroyImmediate(job);
        }

        [Test]
        public void M10_T2_Goal_TendInjured_TriggerGoalConsistency()
        {
            // ADR-M0-7 정합의 논리 재현: 목표(InjuredCount ≤ 0) 달성값 0은 트리거(≥ 1)를
            // 만족하지 않는다 — 달성 즉시 재발동 무한 루프 없음 (에셋 OnValidate는 Unity 임포트가 검증)
            var goal = ScriptableObject.CreateInstance<GoalSO>();
            goal.TriggerConditions = new[] { new SlotCondition { Slot = SlotId.InjuredCount, Op = CompareOp.GreaterOrEqual, Value = 1 } };
            goal.GoalConditions = new[] { new SlotCondition { Slot = SlotId.InjuredCount, Op = CompareOp.LessOrEqual, Value = 0 } };
            Assert.IsFalse(goal.GoalConditions[0].Value >= goal.TriggerConditions[0].Value,
                "목표값 0 < 트리거 문턱 1 — 무한 루프 없음");
            Object.DestroyImmediate(goal);
        }

        [Test]
        public void M10_T1_BlockedByInjury_FilterAndNeutralInvariant()
        {
            GoalSO labor = ScriptableObject.CreateInstance<GoalSO>();    // AllowedWhenInjured 기본 false
            GoalSO survival = ScriptableObject.CreateInstance<GoalSO>();
            survival.AllowedWhenInjured = true;

            // 부상 중 — 노동 차단, 생존 잔존
            Assert.IsTrue(VillagerAgent.BlockedByInjury(labor, InjurySeverity.Light), "부상 중 노동 goal 제외");
            Assert.IsFalse(VillagerAgent.BlockedByInjury(survival, InjurySeverity.Light), "생존 goal 잔존 (절뚝이며 밥 먹는 게 설계)");

            // 중립 불변식 — 부상 None이면 어떤 goal도 막지 않는다 (기존 Select와 완전 동일)
            Assert.IsFalse(VillagerAgent.BlockedByInjury(labor, InjurySeverity.None), "무부상 = 필터 불개입");
            Assert.IsFalse(VillagerAgent.BlockedByInjury(survival, InjurySeverity.None), "무부상 = 필터 불개입");

            Object.DestroyImmediate(labor);
            Object.DestroyImmediate(survival);
        }
    }
}
