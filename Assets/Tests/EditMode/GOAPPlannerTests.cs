using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using AIVillage.AI;
using AIVillage.Core;
using AIVillage.Core.GOAP;

/// <summary>
/// N3a: GOAP 플래너 EditMode 테스트 (T01~T13)
///
/// 선행 조건: 이슈 #1(Registry 상수 정정), 이슈 #2(BuildHouse/Watchtower 추가) 완료 상태.
/// 실행 방법: Unity Test Runner → EditMode 탭 → Run All
/// </summary>
[TestFixture]
public class GOAPPlannerTests
{
    // ────────────────────────────────────────────────────────────────────────
    // 헬퍼: Plan 실행
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// stateOverrides를 적용한 CurrentState + goalId로 플래너를 동기 실행한다.
    /// bypassHeuristic=true이면 MaxGain을 전부 1로 채워 휴리스틱 효과를 무력화한다 (T10).
    /// visitedHashesCap=2이면 Closed Set이 사실상 비활성화된다 (T12).
    /// </summary>
    private static (List<string> plan, int nodesExpanded) PlanRun(
        Dictionary<int, int> stateOverrides,
        string goalId,
        AgentRole role          = AgentRole.None,
        bool useNumericGoals    = true,
        bool bypassHeuristic    = false,
        int  visitedHashesCap   = -1)
    {
        int  totalSlots = GOAPPlanningSlots.TOTAL_SLOTS;
        int  maxNodes   = GOAPPlannerJob.MAX_NODES;
        int  maxPlanLen = GOAPPlannerJob.MAX_PLAN_LEN;
        int  visitedCap = visitedHashesCap > 0 ? visitedHashesCap : maxNodes * 2;
        var  alloc      = Allocator.Persistent;

        // ── 현재 상태 ─────────────────────────────────────────────────────
        var currentState = new NativeArray<int>(totalSlots, alloc);
        if (stateOverrides != null)
            foreach (var kv in stateOverrides)
                currentState[kv.Key] = kv.Value;

        // ── 목표 상태 ─────────────────────────────────────────────────────
        GOAPStateUtil.BuildGoalState(
            goalId,
            out NativeArray<int> goalState,
            out NativeArray<int> goalMask,
            out NativeArray<int> goalOps,
            useNumericGoals,
            alloc);

        // ── 액션 정의 ─────────────────────────────────────────────────────
        var actions = GOAPActionRegistry.BuildActionDefs(
            role, alloc, 1.0f, ContextCostMultipliers.Identity);

        // ── MaxGain / MaxDrop ─────────────────────────────────────────────
        GOAPActionRegistry.BuildMaxGainDrop(actions, totalSlots, alloc,
            out NativeArray<float> maxGain, out NativeArray<float> maxDrop);

        if (bypassHeuristic)
        {
            // 휴리스틱을 "무력화"하려면 h가 0에 가까워야 한다 (Dijkstra 유사).
            // h = ⌈deficit / MaxGain⌉ × minActionCost × 0.99 이므로
            // MaxGain이 크면 stepsEstimate → 0 → h → 0.
            // (반대로 MaxGain=1은 h를 과대추정시켜 A*를 greedy로 만든다 — 무력화가 아님)
            for (int s = 0; s < totalSlots; s++) maxGain[s] = 1_000_000f;
        }

        // ── 출력 버퍼 ─────────────────────────────────────────────────────
        var resultActions = new NativeArray<int>(maxPlanLen, alloc);
        var resultLength  = new NativeArray<int>(1, alloc);
        var nodesExp      = new NativeArray<int>(1, alloc);

        // ── 내부 버퍼 ─────────────────────────────────────────────────────
        var nodeStates   = new NativeArray<int>(maxNodes * totalSlots, alloc);
        var nodeCosts    = new NativeArray<float>(maxNodes, alloc);
        var nodeGCosts   = new NativeArray<float>(maxNodes, alloc);
        var nodeDepths   = new NativeArray<int>(maxNodes, alloc);
        var nodeParents  = new NativeArray<int>(maxNodes, alloc);
        var nodeActions  = new NativeArray<int>(maxNodes, alloc);
        var openQueue    = new NativeArray<int>(maxNodes, alloc);
        var queueSize    = new NativeArray<int>(1, alloc);
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

        job.Run(); // 동기 실행

        // ── 결과 읽기 ─────────────────────────────────────────────────────
        int planLen  = resultLength[0];
        int nodes    = nodesExp[0];
        List<string> plan = null;

        if (planLen == -1)
            plan = new List<string>(); // AlreadySatisfied
        else if (planLen > 0)
        {
            plan = new List<string>(planLen);
            for (int i = 0; i < planLen; i++)
                plan.Add(GOAPActionRegistry.HashToActionId(resultActions[i]));
        }

        // ── Dispose ───────────────────────────────────────────────────────
        currentState.Dispose(); goalState.Dispose(); goalMask.Dispose(); goalOps.Dispose();
        actions.Dispose(); maxGain.Dispose(); maxDrop.Dispose();
        resultActions.Dispose(); resultLength.Dispose(); nodesExp.Dispose();
        nodeStates.Dispose(); nodeCosts.Dispose(); nodeGCosts.Dispose();
        nodeDepths.Dispose(); nodeParents.Dispose(); nodeActions.Dispose();
        openQueue.Dispose(); queueSize.Dispose();
        visitedHashes.Dispose(); visitedNodeIdx.Dispose(); visitedGCosts.Dispose();

        return (plan, nodes);
    }

    /// <summary>T11: 목표를 직접 NativeArray로 구성하는 오버로드 (MAX_DEPTH 실패 검증용).</summary>
    private static (List<string> plan, int nodesExpanded) PlanRunDirect(
        Dictionary<int, int> stateOverrides,
        int goalSlot, int goalValue, int goalOp,
        AgentRole role = AgentRole.None)
    {
        int  totalSlots = GOAPPlanningSlots.TOTAL_SLOTS;
        int  maxNodes   = GOAPPlannerJob.MAX_NODES;
        int  maxPlanLen = GOAPPlannerJob.MAX_PLAN_LEN;
        var  alloc      = Allocator.Persistent;

        var currentState = new NativeArray<int>(totalSlots, alloc);
        if (stateOverrides != null)
            foreach (var kv in stateOverrides)
                currentState[kv.Key] = kv.Value;

        var goalState = new NativeArray<int>(totalSlots, alloc);
        var goalMask  = new NativeArray<int>(totalSlots, alloc);
        var goalOps   = new NativeArray<int>(totalSlots, alloc);
        goalState[goalSlot] = goalValue;
        goalMask[goalSlot]  = 1;
        goalOps[goalSlot]   = goalOp;

        var actions = GOAPActionRegistry.BuildActionDefs(role, alloc, 1.0f, ContextCostMultipliers.Identity);
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
            CurrentState = currentState, GoalState = goalState,
            GoalMask = goalMask, GoalOps = goalOps, Actions = actions,
            ResultActions = resultActions, ResultLength = resultLength,
            NodesExpanded = nodesExp, NodeStates = nodeStates,
            NodeCosts = nodeCosts, NodeGCosts = nodeGCosts,
            NodeDepths = nodeDepths, NodeParents = nodeParents,
            NodeActions = nodeActions, OpenQueue = openQueue,
            QueueSize = queueSize, VisitedHashes = visitedHashes,
            VisitedNodeIdx = visitedNodeIdx, VisitedGCosts = visitedGCosts,
            MaxGain = maxGain, MaxDrop = maxDrop,
        };
        job.Run();

        int planLen = resultLength[0];
        int nodes   = nodesExp[0];
        List<string> plan = null;
        if (planLen == -1)  plan = new List<string>();
        else if (planLen > 0)
        {
            plan = new List<string>(planLen);
            for (int i = 0; i < planLen; i++)
                plan.Add(GOAPActionRegistry.HashToActionId(resultActions[i]));
        }

        currentState.Dispose(); goalState.Dispose(); goalMask.Dispose(); goalOps.Dispose();
        actions.Dispose(); maxGain.Dispose(); maxDrop.Dispose();
        resultActions.Dispose(); resultLength.Dispose(); nodesExp.Dispose();
        nodeStates.Dispose(); nodeCosts.Dispose(); nodeGCosts.Dispose();
        nodeDepths.Dispose(); nodeParents.Dispose(); nodeActions.Dispose();
        openQueue.Dispose(); queueSize.Dispose();
        visitedHashes.Dispose(); visitedNodeIdx.Dispose(); visitedGCosts.Dispose();

        return (plan, nodes);
    }

    // ────────────────────────────────────────────────────────────────────────
    // T01 ~ T13
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>T01 — 레거시 불리언 Goal 회귀: useNumericGoals=false에서 기존 동작 유지.</summary>
    [Test]
    public void T01_LegacyBoolGoal_EatCooked1Step()
    {
        var state = new Dictionary<int, int>
        {
            [GOAPPlanningSlots.HasCookedFood]     = 1,
            [GOAPPlanningSlots.CookedFoodStock]   = 5,
            [GOAPPlanningSlots.MyHunger]          = 90,
        };
        var (plan, _) = PlanRun(state, "SurviveHunger", useNumericGoals: false);

        Assert.IsNotNull(plan, "레거시 불리언 Goal: 플랜을 찾아야 한다.");
        Assert.AreEqual(1, plan.Count, "EatCookedFood 1스텝이어야 한다.");
        Assert.AreEqual("EatCookedFood", plan[0]);
    }

    /// <summary>T02 — 수치형 SurviveHunger 1스텝: MyHunger=70 → EatCooked 1회 → 20 ≤ 30.</summary>
    [Test]
    public void T02_NumericSurviveHunger_EatCooked1Step()
    {
        int relief = GOAPActionRegistry.EAT_HUNGER_RELIEF; // 50
        var state = new Dictionary<int, int>
        {
            [GOAPPlanningSlots.MyHunger]        = 70,
            [GOAPPlanningSlots.HasCookedFood]   = 1,
            [GOAPPlanningSlots.CookedFoodStock] = 2,
        };
        var (plan, _) = PlanRun(state, "SurviveHunger");

        Assert.IsNotNull(plan);
        Assert.AreEqual(1, plan.Count);
        Assert.AreEqual("EatCookedFood", plan[0],
            $"MyHunger=70, Relief={relief} → 결과=20 ≤ 30이므로 1스텝이어야 한다.");
    }

    /// <summary>T03 — CookMeal → EatCooked 멀티스텝: 생 식량만 있을 때 요리 체인.</summary>
    [Test]
    public void T03_CookChain_2Steps()
    {
        int cookConsume = GOAPActionRegistry.COOK_RAW_CONSUME; // 2
        var state = new Dictionary<int, int>
        {
            [GOAPPlanningSlots.MyHunger]        = 70,
            [GOAPPlanningSlots.HasRawFood]      = 1,
            [GOAPPlanningSlots.RawFoodStock]    = cookConsume + 1, // 최소+여분
            [GOAPPlanningSlots.NearFireplace]   = 1,
            [GOAPPlanningSlots.CookedFoodStock] = 0,
            [GOAPPlanningSlots.HasCookedFood]   = 0,
        };
        var (plan, _) = PlanRun(state, "SurviveHunger");

        Assert.IsNotNull(plan);
        Assert.AreEqual(2, plan.Count, "CookMeal → EatCookedFood 2스텝이어야 한다.");
        Assert.AreEqual("CookMeal",     plan[0]);
        Assert.AreEqual("EatCookedFood", plan[1]);
    }

    /// <summary>T04 — GatherWood 직접 채취: 자원 노드 근처에서 YIELD×3 = 30.</summary>
    [Test]
    public void T04_GatherWood_3Chops()
    {
        int yield  = GOAPActionRegistry.YIELD_CHOP_WOOD; // 10
        int target = 30; // BuildGoalState("GatherWood") 목표치
        int steps  = target / yield;                     // 3

        var state = new Dictionary<int, int>
        {
            [GOAPPlanningSlots.WoodStock]              = 0,
            [GOAPPlanningSlots.HasTool]                = 1,
            [GOAPPlanningSlots.NearDiscoveredResource] = 1,
        };
        var (plan, _) = PlanRun(state, "GatherWood");

        Assert.IsNotNull(plan);
        Assert.AreEqual(steps, plan.Count, $"YIELD={yield}, target={target} → {steps}스텝이어야 한다.");
        foreach (var action in plan)
            Assert.AreEqual("ChopWood", action);
    }

    /// <summary>T05 — GatherWood: 자원 발견 안 된 상태에서 Explore 선행 후 채취.</summary>
    [Test]
    public void T05_GatherWood_ExploreThenChop()
    {
        int yield  = GOAPActionRegistry.YIELD_CHOP_WOOD;
        int target = 30;
        int chopSteps = target / yield; // 3

        var state = new Dictionary<int, int>
        {
            [GOAPPlanningSlots.WoodStock]              = 0,
            [GOAPPlanningSlots.HasTool]                = 1,
            [GOAPPlanningSlots.NearDiscoveredResource] = 0,
            [GOAPPlanningSlots.AreaExplored]           = 0,
        };
        var (plan, _) = PlanRun(state, "GatherWood");

        Assert.IsNotNull(plan);
        Assert.AreEqual(chopSteps + 1, plan.Count, $"Explore + {chopSteps}×ChopWood이어야 한다.");
        Assert.AreEqual("Explore", plan[0]);
        for (int i = 1; i < plan.Count; i++)
            Assert.AreEqual("ChopWood", plan[i]);
    }

    /// <summary>T06 — GreaterEq Prec 게이트: 목재 부족 시 BuildCampfire 직접 불가, ChopWood 선행.</summary>
    [Test]
    public void T06_GreaterEqPrecGate_BuildAfterGather()
    {
        var state = new Dictionary<int, int>
        {
            [GOAPPlanningSlots.BuildingQueued]         = 1,
            [GOAPPlanningSlots.WoodStock]              = 0,
            [GOAPPlanningSlots.HasTool]                = 1,
            [GOAPPlanningSlots.NearDiscoveredResource] = 1,
        };
        var (plan, _) = PlanRun(state, "BuildStructure");

        Assert.IsNotNull(plan);
        // BuildCampfire 비용(5) < YIELD_CHOP_WOOD(10) → ChopWood 1회 후 BuildCampfire
        Assert.AreEqual(2, plan.Count, "WoodStock=0이므로 ChopWood → BuildCampfire 2스텝이어야 한다.");
        Assert.AreEqual("ChopWood",     plan[0]);
        Assert.AreEqual("BuildCampfire", plan[1]);
    }

    /// <summary>T07 — Sub 클램프 회귀 (B26): 임계값 경계에서 Sub가 음수로 내려가지 않음.</summary>
    [Test]
    public void T07_SubClamp_BoundaryBuildCampfire()
    {
        // WoodStock=4 (BUILD_CAMPFIRE_WOOD=5 미만) → ChopWood(+10)→14 → BuildCampfire(-5)→9
        var state = new Dictionary<int, int>
        {
            [GOAPPlanningSlots.BuildingQueued]         = 1,
            [GOAPPlanningSlots.WoodStock]              = GOAPActionRegistry.BUILD_CAMPFIRE_WOOD - 1, // 4
            [GOAPPlanningSlots.HasTool]                = 1,
            [GOAPPlanningSlots.NearDiscoveredResource] = 1,
        };
        var (plan, _) = PlanRun(state, "BuildStructure");

        Assert.IsNotNull(plan, "WoodStock=4로도 플랜이 성공해야 한다 (Sub 클램프 동작 확인).");
        Assert.AreEqual(2, plan.Count);
        Assert.AreEqual("ChopWood",      plan[0]);
        Assert.AreEqual("BuildCampfire", plan[1]);
    }

    /// <summary>T08 — AlreadySatisfied: 이미 목표 달성 상태에서 ResultLength==-1.</summary>
    [Test]
    public void T08_AlreadySatisfied_EmptyPlan()
    {
        var state = new Dictionary<int, int>
        {
            [GOAPPlanningSlots.MyHunger] = 10, // 10 ≤ 30 → 이미 달성
        };
        var (plan, _) = PlanRun(state, "SurviveHunger");

        Assert.IsNotNull(plan, "AlreadySatisfied는 null이 아닌 빈 리스트를 반환해야 한다.");
        Assert.AreEqual(0, plan.Count, "이미 달성 상태이므로 플랜 길이 0이어야 한다.");
    }

    /// <summary>T09 — 알 수 없는 goalId: goalMask all-zero → AlreadySatisfied 반환.</summary>
    [Test]
    public void T09_UnknownGoalId_ZeroMask_AlreadySatisfied()
    {
        var (plan, _) = PlanRun(new Dictionary<int, int>(), "Nonsense_GoalId_XYZ");

        // 마스크가 전부 0 → 체크할 Goal이 없으므로 즉시 달성(-1)
        Assert.IsNotNull(plan);
        Assert.AreEqual(0, plan.Count, "미지 goalId는 all-zero 마스크 → AlreadySatisfied.");
    }

    /// <summary>T10 — 휴리스틱 우회: MaxGain=1로 무력화해도 동일 플랜, 단 탐색 노드 수 증가.</summary>
    [Test]
    public void T10_HeuristicBypass_SamePlanMoreNodes()
    {
        var state = new Dictionary<int, int>
        {
            [GOAPPlanningSlots.WoodStock]              = 0,
            [GOAPPlanningSlots.HasTool]                = 1,
            [GOAPPlanningSlots.NearDiscoveredResource] = 1,
        };

        var (planNormal, nodesNormal)  = PlanRun(state, "GatherWood", bypassHeuristic: false);
        var (planBypass, nodesBypass)  = PlanRun(state, "GatherWood", bypassHeuristic: true);

        Assert.IsNotNull(planNormal);
        Assert.IsNotNull(planBypass);
        Assert.AreEqual(planNormal.Count, planBypass.Count, "플랜 길이는 동일해야 한다.");
        for (int i = 0; i < planNormal.Count; i++)
            Assert.AreEqual(planNormal[i], planBypass[i], $"플랜[{i}]가 달라서는 안 된다.");
        Assert.LessOrEqual(nodesNormal, nodesBypass,
            "좋은 휴리스틱은 탐색 노드 수를 줄여야 한다 (nodesNormal <= nodesBypass).");
    }

    /// <summary>T11 — MAX_DEPTH 초과: target > YIELD×12 → NoSolution.</summary>
    [Test]
    public void T11_MaxDepthExceeded_NoSolution()
    {
        // MAX_DEPTH=12로 얻을 수 있는 최대 WoodStock: YIELD×12
        int maxAchievable = GOAPActionRegistry.YIELD_CHOP_WOOD * GOAPPlannerJob.MAX_DEPTH;
        int impossibleTarget = maxAchievable + 1; // 121

        var state = new Dictionary<int, int>
        {
            [GOAPPlanningSlots.WoodStock]              = 0,
            [GOAPPlanningSlots.HasTool]                = 1,
            [GOAPPlanningSlots.NearDiscoveredResource] = 1,
        };

        // PlanRunDirect: goalSlot=WoodStock, op=1(GreaterEq)
        var (plan, _) = PlanRunDirect(
            state,
            GOAPPlanningSlots.WoodStock, impossibleTarget, 1);

        Assert.IsNull(plan,
            $"YIELD={GOAPActionRegistry.YIELD_CHOP_WOOD}, MAX_DEPTH={GOAPPlannerJob.MAX_DEPTH} → " +
            $"target={impossibleTarget}은 달성 불가능하므로 null을 반환해야 한다.");
    }

    /// <summary>T12 — Closed Set 우회: visitedCap=2로 사실상 비활성화해도 동일 플랜, 단 노드 수 증가.</summary>
    [Test]
    public void T12_ClosedSetBypass_SamePlanMoreNodes()
    {
        var state = new Dictionary<int, int>
        {
            [GOAPPlanningSlots.WoodStock]              = 0,
            [GOAPPlanningSlots.HasTool]                = 1,
            [GOAPPlanningSlots.NearDiscoveredResource] = 0,
            [GOAPPlanningSlots.AreaExplored]           = 0,
        };

        var (planNormal, nodesNormal)   = PlanRun(state, "GatherWood", visitedHashesCap: -1);
        var (planBypass, nodesBypass)   = PlanRun(state, "GatherWood", visitedHashesCap: 2);

        Assert.IsNotNull(planNormal);
        Assert.IsNotNull(planBypass);
        Assert.AreEqual(planNormal.Count, planBypass.Count,
            "Closed Set 비활성화로 플랜 내용이 달라져서는 안 된다.");
        for (int i = 0; i < planNormal.Count; i++)
            Assert.AreEqual(planNormal[i], planBypass[i], $"플랜[{i}]가 달라서는 안 된다.");
        Assert.LessOrEqual(nodesNormal, nodesBypass,
            "Closed Set 활성화 시 탐색 노드 수가 더 적어야 한다 (nodesNormal <= nodesBypass).");
    }

    /// <summary>T13 — ADR-7 정합성: 22개 액션 각각의 Effect값이 Registry 상수와 일치.</summary>
    [Test]
    public void T13_ADR7_EffectConstants_MatchRegistry()
    {
        var actions = GOAPActionRegistry.BuildActionDefs(AgentRole.None, Allocator.Persistent);

        try
        {
            // 액션 해시 → def 딕셔너리 구성
            var defMap = new Dictionary<int, GOAPActionDef>(actions.Length);
            for (int i = 0; i < actions.Length; i++)
                defMap[actions[i].ActionStringHash] = actions[i];

            void AssertEffect(string actionName, int targetSlot, int targetOp, int expectedVal,
                              string constantName)
            {
                int hash = Animator.StringToHash(actionName);
                Assert.IsTrue(defMap.ContainsKey(hash), $"{actionName} 액션이 정의되지 않았다.");
                GOAPActionDef def = defMap[hash];
                int? found = null;
                for (int e = 0; e < def.EffectCount; e++)
                {
                    GetEffectAt(def, e, out int s, out int op, out int v);
                    if (s == targetSlot && op == targetOp) { found = v; break; }
                }
                Assert.IsTrue(found.HasValue,
                    $"{actionName}: slot={targetSlot} op={targetOp} Effect를 찾지 못했다.");
                Assert.AreEqual(expectedVal, found.Value,
                    $"{actionName} Effect({constantName}) 불일치: Registry={expectedVal}, Def={found.Value}");
            }

            int S = 1; // EffOp.Add
            int D = 2; // EffOp.Sub

            // ── 채집 YIELD ────────────────────────────────────────────────
            AssertEffect("ChopWood",          GOAPPlanningSlots.WoodStock,     S, GOAPActionRegistry.YIELD_CHOP_WOOD,       "YIELD_CHOP_WOOD");
            AssertEffect("MineStone",         GOAPPlanningSlots.StoneStock,    S, GOAPActionRegistry.YIELD_MINE_STONE,      "YIELD_MINE_STONE");
            AssertEffect("MineIron",          GOAPPlanningSlots.IronStock,     S, GOAPActionRegistry.YIELD_MINE_IRON,       "YIELD_MINE_IRON");
            AssertEffect("MineCopper",        GOAPPlanningSlots.CopperStock,   S, GOAPActionRegistry.YIELD_MINE_COPPER,     "YIELD_MINE_COPPER");
            AssertEffect("HarvestWildBerries",GOAPPlanningSlots.RawFoodStock,  S, GOAPActionRegistry.YIELD_HARVEST_BERRIES, "YIELD_HARVEST_BERRIES");

            // ── 조리 ─────────────────────────────────────────────────────
            AssertEffect("CookMeal", GOAPPlanningSlots.RawFoodStock,    D, GOAPActionRegistry.COOK_RAW_CONSUME, "COOK_RAW_CONSUME");
            AssertEffect("CookMeal", GOAPPlanningSlots.CookedFoodStock, S, GOAPActionRegistry.COOK_YIELD,       "COOK_YIELD");

            // ── 식사 / 피로 / 체력 ──────────────────────────────────────
            AssertEffect("EatCookedFood", GOAPPlanningSlots.MyHunger,  D, GOAPActionRegistry.EAT_HUNGER_RELIEF,    "EAT_HUNGER_RELIEF");
            AssertEffect("EatRawFood",    GOAPPlanningSlots.MyHunger,  D, GOAPActionRegistry.EAT_RAW_RELIEF,       "EAT_RAW_RELIEF");
            AssertEffect("Sleep",         GOAPPlanningSlots.MyFatigue, D, GOAPActionRegistry.SLEEP_FATIGUE_RELIEF, "SLEEP_FATIGUE_RELIEF");
            AssertEffect("RestOnGround",  GOAPPlanningSlots.MyFatigue, D, GOAPActionRegistry.REST_FATIGUE_RELIEF,  "REST_FATIGUE_RELIEF");
            AssertEffect("SeekMedicalAid",GOAPPlanningSlots.MyHealth,  S, GOAPActionRegistry.MEDICAL_HEALTH_GAIN,  "MEDICAL_HEALTH_GAIN");

            // ── 건설 비용 ─────────────────────────────────────────────────
            AssertEffect("BuildCampfire",   GOAPPlanningSlots.WoodStock,  D, GOAPActionRegistry.BUILD_CAMPFIRE_WOOD,    "BUILD_CAMPFIRE_WOOD");
            AssertEffect("BuildHouse",      GOAPPlanningSlots.WoodStock,  D, GOAPActionRegistry.BUILD_HOUSE_WOOD,       "BUILD_HOUSE_WOOD");
            AssertEffect("BuildHouse",      GOAPPlanningSlots.StoneStock, D, GOAPActionRegistry.BUILD_HOUSE_STONE,      "BUILD_HOUSE_STONE");
            AssertEffect("BuildWatchtower", GOAPPlanningSlots.WoodStock,  D, GOAPActionRegistry.BUILD_WATCHTOWER_WOOD,  "BUILD_WATCHTOWER_WOOD");
            AssertEffect("BuildWatchtower", GOAPPlanningSlots.StoneStock, D, GOAPActionRegistry.BUILD_WATCHTOWER_STONE, "BUILD_WATCHTOWER_STONE");
            AssertEffect("BuildWatchtower", GOAPPlanningSlots.IronStock,  D, GOAPActionRegistry.BUILD_WATCHTOWER_IRON,  "BUILD_WATCHTOWER_IRON");
        }
        finally
        {
            actions.Dispose();
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // ADR-7 헬퍼: GOAPActionDef의 e번째 Effect를 읽는다 (T13 전용).
    // ────────────────────────────────────────────────────────────────────────
    private static void GetEffectAt(GOAPActionDef def, int index,
                                    out int slot, out int op, out int val)
    {
        switch (index)
        {
            case 0: slot=def.Eff0S; op=def.Eff0Op; val=def.Eff0V; return;
            case 1: slot=def.Eff1S; op=def.Eff1Op; val=def.Eff1V; return;
            case 2: slot=def.Eff2S; op=def.Eff2Op; val=def.Eff2V; return;
            case 3: slot=def.Eff3S; op=def.Eff3Op; val=def.Eff3V; return;
            case 4: slot=def.Eff4S; op=def.Eff4Op; val=def.Eff4V; return;
            case 5: slot=def.Eff5S; op=def.Eff5Op; val=def.Eff5V; return;
            case 6: slot=def.Eff6S; op=def.Eff6Op; val=def.Eff6V; return;
            case 7: slot=def.Eff7S; op=def.Eff7Op; val=def.Eff7V; return;
            default: slot=0; op=0; val=0; return;
        }
    }
}
