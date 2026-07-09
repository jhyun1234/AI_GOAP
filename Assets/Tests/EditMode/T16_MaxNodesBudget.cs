using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using AIVillage.AI;
using AIVillage.Core.GOAP;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// T16: 대표 시나리오 5개에서 각 P0/P1/P2 Goal의 실제 NodesExpanded가
    ///      MAX_NODES × 0.7 이하인지 검증한다.
    ///
    /// 초과 시 = 컨텍스트 비용 배율(계절·거리·위험·피로)로 목표 경로 f-cost가
    /// 예산을 소진해 NoSolutionFound → Deadlock 폴백으로 이어질 위험 (버그 21 재발 방지).
    ///
    /// 스코프 (ADR-T3): 씬 로드·Prefab·SO 의존 금지. 시나리오는 파일 내 상수로 하드코딩.
    /// 실행 방식 (ADR-T1): EditMode에서 Job.Run() 동기 실행. Burst 최적화는 확인 대상 아님.
    /// 임계값 (ADR-T4): BUDGET = (int)(GOAPPlannerJob.MAX_NODES × 0.7f) 동적 계산.
    ///                  MAX_NODES 승격 시 예산이 자동 반영된다.
    ///
    /// SensorSystem 미의존:
    ///   ContextCostMultipliers는 원래 GOAPPlannerScheduler.ComputeContextMultipliers()가
    ///   SensorSystem.Instance.GetAllNodes()를 참조해 계산하지만, EditMode에서는 SensorSystem이
    ///   null이라 실제 게임 상황보다 낙관적이다. 본 테스트는 각 시나리오에 필요한 배율을
    ///   직접 주입해(Danger/Distance 등) 계약을 강제한다.
    /// </summary>
    public class T16_MaxNodesBudget
    {
        // ADR-T4: MAX_NODES × 0.7 동적 계산. MAX_NODES가 2048 → 4096으로 승격되면 예산도 자동 확대.
        private static readonly int BUDGET = (int)(GOAPPlannerJob.MAX_NODES * 0.7f);

        // ── 시나리오 정의 ────────────────────────────────────────────────────
        //
        // 5개 시나리오는 파일 상수로 하드코딩 (ADR-T3). 각 시나리오는:
        //   Name          — 테스트 리포트에 표시되는 짧은 식별자 (ToString() 오버라이드)
        //   StateOverrides — CurrentState NativeArray 초기값 (미지정 슬롯은 0)
        //   Role          — 역할 보정 (BuildActionDefs.BaseCost 배율)
        //   SeasonMod     — SeasonManager.GetCurrentGatherCostModifier() 대체값
        //                   Autumn=0.333f(수집 3배 선호), 그 외=1.0f
        //   Ctx           — ContextCostMultipliers (거리·포화·위험·피로 배율)
        //
        // 공통 BaseState: 모든 goal이 최소 1개의 진행 경로를 가지도록 필수 인프라만 설정.
        // - HasTool=1, NearDiscoveredResource=1: ChopWood/MineStone 등 채집 액션 활성
        // - NearFireplace=1: CookMeal 활성
        // - NearEnemy=1 + HasPrimitiveWeapon=1: AttackEnemy 1스텝 활성
        // - BuildingQueued=1: 건설 액션 활성
        // - MySatiety=30: SurviveHunger 미달성(발동 조건, 목표=70)
        // - MyHealth=100, MyFatigue=0: SurviveInjury/SurviveFatigue 이미 달성(방해 없음)
        // - RawFood/CookedFood/Wood/Stone/Iron Stock=0: 채집 유도

        public class Scenario
        {
            public string Name;
            public Dictionary<int, int> StateOverrides;
            public AgentRole Role;
            public float SeasonMod;
            public ContextCostMultipliers Ctx;

            /// <summary>
            /// 예정된 NoSolution 시나리오 여부 (S4_NoTool).
            /// true + planLength==0 이면 예산(MAX_NODES × 0.7) 검증 대신 상한(MAX_NODES) 이하만 확인한다.
            /// Plan이 반환된 경우(planLength > 0)에는 예산 검증을 그대로 유지 → 성공 경로 회귀 방지.
            /// </summary>
            public bool AllowMaxNodesForNoSolution;

            // NUnit이 TestCaseSource 결과 명명에 사용한다.
            public override string ToString() => Name;
        }

        private static Dictionary<int, int> BaseState()
        {
            return new Dictionary<int, int>
            {
                [GOAPPlanningSlots.HasTool]                = 1,
                [GOAPPlanningSlots.NearDiscoveredResource] = 1,
                [GOAPPlanningSlots.NearFireplace]          = 1,
                [GOAPPlanningSlots.NearEnemy]              = 1,
                [GOAPPlanningSlots.HasPrimitiveWeapon]     = 1,
                [GOAPPlanningSlots.BuildingQueued]         = 1,
                [GOAPPlanningSlots.MySatiety]              = 30,
                [GOAPPlanningSlots.MyHealth]               = 100,
                [GOAPPlanningSlots.MyFatigue]              = 0,
            };
        }

        // 위험 근접: GOAPPlannerScheduler의 DANGER_GATHER_MULT/DANGER_ATTACK_MULT 상수값 재현.
        private static ContextCostMultipliers DangerCtx()
        {
            var c = ContextCostMultipliers.Identity;
            c.ChopWood       *= 2.5f;  // DANGER_GATHER_MULT
            c.MineStone      *= 2.5f;
            c.MineIron       *= 2.5f;
            c.MineCopper     *= 2.5f;
            c.HarvestBerries *= 2.5f;
            c.Explore        *= 2.5f;
            c.AttackEnemy    *= 0.7f;  // DANGER_ATTACK_MULT
            return c;
        }

        // 자원 원거리: DISTANCE_SCALE(50) 상한 도달 시 배율.
        // 공식: 1 + min(dist/50, 1) × DISTANCE_WEIGHT(1.5) = 최대 2.5.
        private static ContextCostMultipliers DistanceCtx()
        {
            return new ContextCostMultipliers
            {
                ChopWood       = 2.5f,
                MineStone      = 2.5f,
                MineIron       = 2.5f,
                MineCopper     = 2.5f,
                HarvestBerries = 2.5f,
                Explore        = 1f,
                AttackEnemy    = 1f,
                RestOnGround   = 1f,
            };
        }

        private static IEnumerable<Scenario> Scenarios()
        {
            // S1: 빈 마을 초기 상태 — 배율 모두 1, 자원 스톡 0
            yield return new Scenario
            {
                Name           = "S1_EmptyVillage",
                StateOverrides = BaseState(),
                Role           = AgentRole.None,
                SeasonMod      = 1.0f,
                Ctx            = ContextCostMultipliers.Identity,
            };

            // S2: Autumn 계절 배율 — 수집 액션 비용 1/3 (선호도 3배)
            //     SeasonManager.GetCurrentGatherCostModifier() 실측 대체값.
            yield return new Scenario
            {
                Name           = "S2_AutumnGatherDiscount",
                StateOverrides = BaseState(),
                Role           = AgentRole.None,
                SeasonMod      = 0.333f,
                Ctx            = ContextCostMultipliers.Identity,
            };

            // S3: 적 근접 — DANGER 배율 (수집 ×2.5, 전투 ×0.7)
            yield return new Scenario
            {
                Name           = "S3_DangerNearby",
                StateOverrides = BaseState(),
                Role           = AgentRole.None,
                SeasonMod      = 1.0f,
                Ctx            = DangerCtx(),
            };

            // S4: 도구 없음 — HasTool=0.
            //     ChopWood/MineStone/MineIron/MineCopper 전부 Prec 불충족.
            //     HarvestWildBerries/AttackEnemy는 HasTool 요구 없음 → 여전히 가능.
            //     GatherWood/BuildStructure는 예정된 NoSolution → 예산 대신 상한(MAX_NODES) 검증만.
            //     Plan이 반환되는 goal(GatherFood/SurviveHunger/DefendVillage)에는 예산 검증 유지.
            {
                var state = BaseState();
                state[GOAPPlanningSlots.HasTool] = 0;
                yield return new Scenario
                {
                    Name                        = "S4_NoTool",
                    StateOverrides              = state,
                    Role                        = AgentRole.None,
                    SeasonMod                   = 1.0f,
                    Ctx                         = ContextCostMultipliers.Identity,
                    AllowMaxNodesForNoSolution  = true,
                };
            }

            // S5: 자원 원거리 — DISTANCE_SCALE 상한 배율 (2.5)
            yield return new Scenario
            {
                Name           = "S5_DistantResources",
                StateOverrides = BaseState(),
                Role           = AgentRole.None,
                SeasonMod      = 1.0f,
                Ctx            = DistanceCtx(),
            };
        }

        // ────────────────────────────────────────────────────────────────────
        // 플래너 동기 실행 헬퍼
        //   GOAPPlannerTests.cs의 PlanRun 패턴을 재사용하되, 시나리오 컨텍스트를 명시 주입한다.
        //   반환: (planLength, nodesExpanded)
        //     planLength = -1  → AlreadySatisfied (탐색 없이 조기 종료 가능)
        //     planLength = 0   → NoSolution
        //     planLength > 0   → 정상 플랜 길이
        // ────────────────────────────────────────────────────────────────────

        private static (int planLength, int nodesExpanded) RunScenario(string goalId, Scenario s)
        {
            int  totalSlots = GOAPPlanningSlots.TOTAL_SLOTS;
            int  maxNodes   = GOAPPlannerJob.MAX_NODES;
            int  maxPlanLen = GOAPPlannerJob.MAX_PLAN_LEN;
            var  alloc      = Allocator.Persistent;

            var currentState = new NativeArray<int>(totalSlots, alloc);
            foreach (var kv in s.StateOverrides)
                currentState[kv.Key] = kv.Value;

            GOAPStateUtil.BuildGoalState(
                goalId,
                out NativeArray<int> goalState,
                out NativeArray<int> goalMask,
                out NativeArray<int> goalOps,
                useNumericGoals: true,
                alloc);

            // [F-A] 성격 배율 축은 T16 시나리오에 없다 — Identity로 무해 폴백.
            var actions = GOAPActionRegistry.BuildActionDefs(s.Role, alloc, s.SeasonMod, s.Ctx, PersonalityCostMultipliers.Identity);
            GOAPActionRegistry.BuildMaxGainDrop(actions, totalSlots, alloc,
                out NativeArray<float> maxGain, out NativeArray<float> maxDrop);

            var resultActions   = new NativeArray<int>(maxPlanLen, alloc);
            var resultLength    = new NativeArray<int>(1, alloc);
            var nodesExp        = new NativeArray<int>(1, alloc);
            var nodeStates      = new NativeArray<int>(maxNodes * totalSlots, alloc);
            var nodeCosts       = new NativeArray<float>(maxNodes, alloc);
            var nodeGCosts      = new NativeArray<float>(maxNodes, alloc);
            var nodeDepths      = new NativeArray<int>(maxNodes, alloc);
            var nodeParents     = new NativeArray<int>(maxNodes, alloc);
            var nodeActions     = new NativeArray<int>(maxNodes, alloc);
            var openQueue       = new NativeArray<int>(maxNodes, alloc);
            var queueSize       = new NativeArray<int>(1, alloc);
            int visitedCap      = maxNodes * 2;
            var visitedHashes   = new NativeArray<int>(visitedCap, alloc);
            var visitedNodeIdx  = new NativeArray<int>(visitedCap, alloc);
            var visitedGCosts   = new NativeArray<float>(visitedCap, alloc);

            var job = new GOAPPlannerJob
            {
                CurrentState   = currentState,
                GoalState      = goalState,
                GoalMask       = goalMask,
                GoalOps        = goalOps,
                Actions        = actions,
                ResultActions  = resultActions,
                ResultLength   = resultLength,
                NodesExpanded  = nodesExp,
                NodeStates     = nodeStates,
                NodeCosts      = nodeCosts,
                NodeGCosts     = nodeGCosts,
                NodeDepths     = nodeDepths,
                NodeParents    = nodeParents,
                NodeActions    = nodeActions,
                OpenQueue      = openQueue,
                QueueSize      = queueSize,
                VisitedHashes  = visitedHashes,
                VisitedNodeIdx = visitedNodeIdx,
                VisitedGCosts  = visitedGCosts,
                MaxGain        = maxGain,
                MaxDrop        = maxDrop,
            };
            job.Run();

            int planLen = resultLength[0];
            int nodes   = nodesExp[0];

            currentState.Dispose(); goalState.Dispose(); goalMask.Dispose(); goalOps.Dispose();
            actions.Dispose(); maxGain.Dispose(); maxDrop.Dispose();
            resultActions.Dispose(); resultLength.Dispose(); nodesExp.Dispose();
            nodeStates.Dispose(); nodeCosts.Dispose(); nodeGCosts.Dispose();
            nodeDepths.Dispose(); nodeParents.Dispose(); nodeActions.Dispose();
            openQueue.Dispose(); queueSize.Dispose();
            visitedHashes.Dispose(); visitedNodeIdx.Dispose(); visitedGCosts.Dispose();

            return (planLen, nodes);
        }

        // ────────────────────────────────────────────────────────────────────
        // 예산 어설션
        //   두 가지 실패 신호:
        //   1) nodes > BUDGET  → 배율 곱셈이 예산을 소진, MAX_NODES 승격 또는 배율 감소 필요
        //   2) planLength != -1 && nodes == 0 → 탐색이 시작조차 못했다면 상태 구성 결함 신호
        //      (AlreadySatisfied 경로는 goalMask all-zero/즉시 달성으로 nodes=0 가능하므로 예외)
        // ────────────────────────────────────────────────────────────────────

        private static void AssertBudget(string goalId, Scenario s)
        {
            var (planLen, nodes) = RunScenario(goalId, s);
            string outcome = planLen == -1 ? "AlreadySatisfied"
                           : planLen ==  0 ? "NoSolution"
                                           : $"Plan(len={planLen})";

            // 예정된 NoSolution 시나리오(S4_NoTool + Gather/Build)는 예산 대신 상한만 검증한다.
            // NoSolution 자체는 시나리오가 의도한 결과이므로 A*가 MAX_NODES까지 탐색해도 정상.
            // Plan(len>0)이 반환된 경우에는 예산 검증을 유지해 회귀를 감지한다.
            int effectiveBudget = (s.AllowMaxNodesForNoSolution && planLen == 0)
                ? GOAPPlannerJob.MAX_NODES
                : BUDGET;

            string budgetLabel = effectiveBudget == BUDGET
                ? $"BUDGET={BUDGET} (= MAX_NODES({GOAPPlannerJob.MAX_NODES}) × 0.7)"
                : $"MAX_NODES={GOAPPlannerJob.MAX_NODES} (예정된 NoSolution)";

            Assert.LessOrEqual(
                nodes, effectiveBudget,
                $"[T16 {s.Name}/{goalId}] NodesExpanded={nodes} > {budgetLabel}. Outcome={outcome}. " +
                "컨텍스트 배율 감소, 상태 재조정, 또는 MAX_NODES 승격이 필요합니다.");

            if (planLen != -1)
            {
                Assert.Greater(
                    nodes, 0,
                    $"[T16 {s.Name}/{goalId}] NodesExpanded=0 인데 outcome={outcome}. " +
                    "AlreadySatisfied가 아닌데 탐색이 시작조차 못했다면 시나리오 상태 구성 결함.");
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // 25 케이스 = 5 Goal × 5 Scenario
        //   NUnit이 TestCaseSource(Scenarios)로 자동 전개한다.
        //   Test Runner에는 "GatherWood_...(S1_EmptyVillage)" 형태로 표시된다.
        // ────────────────────────────────────────────────────────────────────

        [Test, TestCaseSource(nameof(Scenarios))]
        public void GatherWood_NodesExpanded_within_budget(Scenario s)
            => AssertBudget("GatherWood", s);

        [Test, TestCaseSource(nameof(Scenarios))]
        public void GatherFood_NodesExpanded_within_budget(Scenario s)
            => AssertBudget("GatherFood", s);

        [Test, TestCaseSource(nameof(Scenarios))]
        public void SurviveHunger_NodesExpanded_within_budget(Scenario s)
            => AssertBudget("SurviveHunger", s);

        [Test, TestCaseSource(nameof(Scenarios))]
        public void BuildStructure_NodesExpanded_within_budget(Scenario s)
            => AssertBudget("BuildStructure", s);

        [Test, TestCaseSource(nameof(Scenarios))]
        public void DefendVillage_NodesExpanded_within_budget(Scenario s)
            => AssertBudget("DefendVillage", s);
    }
}
