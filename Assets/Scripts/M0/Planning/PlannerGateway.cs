using System;
using System.Collections.Generic;
using AIVillage.Core.GOAP;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace AIVillage.M0
{
    public enum PlanStatus
    {
        Pending,          // 잡 미완료 (계속 폴링)
        Success,          // 유효 플랜 반환
        AlreadySatisfied, // 목표 이미 달성 (ResultLength -1)
        NoSolution,       // 해 없음 (ResultLength 0) — ADR-3: 버그 증상으로 취급
    }

    /// <summary>
    /// GOAPPlannerJob 스케줄/폴링 게이트웨이 — 舊 GOAPPlannerScheduler(656줄)의 M0 대체 (~200줄).
    /// 잡 자체는 무변경 (ADR-M0-5). 성격 배율(M4-B)은 RequestPlan의 per-request 사본에만 굽는다.
    ///
    /// 사용 계약:
    ///   1. RequestPlan() → PendingPlan (null이면 요청 실패)
    ///   2. 매 틱 TryGetResult() 폴링 — true 반환 시 버퍼는 이미 해제됨
    ///   3. 타임아웃/사망 등 중단 시 반드시 Cancel() — NativeArray leak 방지
    /// </summary>
    public sealed class PlannerGateway
    {
        private readonly ActionCatalog _catalog;
        private readonly GOAPActionDef[] _defs;     // 시작 시 1회 컴파일 — 공유 원본 (배율은 사본에만, ADR-M4-1)
        private readonly float[] _maxGain;
        private readonly float[] _maxDrop;

        public sealed class PendingPlan
        {
            internal JobHandle Handle;
            internal NativeArray<int> CurrentState, GoalState, GoalMask, GoalOps;
            internal NativeArray<GOAPActionDef> Actions;
            internal NativeArray<int> ResultActions, ResultLength, NodesExpanded;
            internal NativeArray<int> NodeStates, NodeDepths, NodeParents, NodeActions, OpenQueue, QueueSize;
            internal NativeArray<float> NodeCosts, NodeGCosts;
            internal NativeArray<int> VisitedHashes, VisitedNodeIdx;
            internal NativeArray<float> VisitedGCosts;
            internal NativeArray<float> MaxGain, MaxDrop;
            internal bool Alive;

            /// <summary>요청 시점 (Time.realtimeSinceStartup). 호출자의 타임아웃 판정용.</summary>
            public float RequestedAt { get; internal set; }
        }

        /// <summary>agentCfg = 개인/집 스톡 상한의 단일 출처 (M11-A, ADR-M11-3 — 컴파일 시 상한
        /// 전제 자동 주입). null이면 주입 없음 = 기존 동작 (중립 — 테스트·미배선 경로 불변).</summary>
        public PlannerGateway(ActionCatalog catalog, AgentConfigSO agentCfg = null)
        {
            _catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            _defs = ActionCompiler.CompileManaged(catalog,
                agentCfg != null ? agentCfg.BodyCarryCap : 0,
                agentCfg != null ? agentCfg.HomeStorageCap : 0);
            ActionCompiler.ComputeMaxGainDrop(_defs, PlanningConfig.TotalSlots, out _maxGain, out _maxDrop);
        }

        /// <summary>
        /// 목표 슬롯 도달 가능성 검사 (순수 — 게이트 대상). GreaterEq 목표는 그 슬롯을 올리는
        /// 액션이, LessEq 목표는 내리는 액션이 카탈로그에 있어야 한다. 없으면 잡의 휴리스틱이
        /// 0으로 나눈다 (Burst 예외 = 진단 불가능한 크래시). Equal 목표는 나눗셈을 하지 않아 제외.
        /// false를 반환할 상황은 전부 에셋 결함이다 — 로그가 어느 goal·슬롯인지 지목한다.
        /// </summary>
        public static bool HasReachableGoalSlots(GoalSO goal, float[] maxGain, float[] maxDrop,
                                                 List<string> reasons = null)
        {
            bool ok = true;
            foreach (SlotCondition c in goal.GoalConditions)
            {
                int s = (int)c.Slot;
                if (c.Op == CompareOp.GreaterOrEqual && (maxGain == null || s >= maxGain.Length || maxGain[s] <= 0f))
                {
                    reasons?.Add($"{c.Slot} 올리는 액션 없음");
                    ok = false;
                }
                else if (c.Op == CompareOp.LessOrEqual && (maxDrop == null || s >= maxDrop.Length || maxDrop[s] <= 0f))
                {
                    reasons?.Add($"{c.Slot} 내리는 액션 없음");
                    ok = false;
                }
            }
            return ok;
        }

        // 도달 불가 사유 버퍼 (RequestPlan 전용) — 순수 판정 함수는 로그하지 않는다(부작용 0),
        // 로그는 여기서 낸다 (게이트 테스트가 콘솔을 더럽히지 않게 — 2026-07-23).
        private readonly List<string> _reachReasons = new List<string>(4);

        /// <summary>
        /// 플랜 요청. 실패(잘못된 입력/OOM) 시 null. 메인 스레드 전용.
        /// costMult = 카탈로그 인덱스별 성격 비용 배율 (M4-B, null=중립) — 잡에 넘길 사본에만
        /// 구워지는 것이 유일한 주입 지점 (ADR-M4-1). 공유 _defs 원본은 무변경.
        /// </summary>
        public PendingPlan RequestPlan(WorldSnapshot snap, GoalSO goal, float[] costMult = null)
        {
            if (!snap.IsValid || goal == null || goal.GoalConditions == null || goal.GoalConditions.Length == 0)
            {
                Debug.LogWarning($"[PlannerGateway] RequestPlan: 입력이 유효하지 않습니다. goal={(goal != null ? goal.name : "null")}");
                return null;
            }
            // 휴리스틱 0-나눗셈 방어 (ADR-M0-4: 잡은 동결 — 입력이 안 맞으면 여기서 막는다).
            // 목표 슬롯을 개선하는 액션이 카탈로그에 하나도 없으면 CalculateHeuristic이
            // 부족량 ÷ 0을 수행해 Burst 잡에서 DivideByZeroException으로 터진다.
            // 원인은 항상 에셋 결함(목표 슬롯에 대응하는 효과 누락)이므로 에러로 드러낸다.
            _reachReasons.Clear();
            if (!HasReachableGoalSlots(goal, _maxGain, _maxDrop, _reachReasons))
            {
                Debug.LogError($"[PlannerGateway] {goal.name}: 목표 슬롯을 움직일 액션이 카탈로그에 없습니다 " +
                               $"({string.Join(", ", _reachReasons)}) — 플래너 픽션 효과 누락 (에셋 결함).", goal);
                return null;
            }

            const Allocator alloc = Allocator.Persistent;
            int totalSlots = PlanningConfig.TotalSlots;
            var p = new PendingPlan();

            try
            {
                p.CurrentState = new NativeArray<int>(totalSlots, alloc);
                for (int s = 0; s < SlotIds.Count; s++) p.CurrentState[s] = snap.Slots[s];

                p.GoalState = new NativeArray<int>(totalSlots, alloc);
                p.GoalMask  = new NativeArray<int>(totalSlots, alloc);
                p.GoalOps   = new NativeArray<int>(totalSlots, alloc);
                foreach (SlotCondition c in goal.GoalConditions)
                {
                    int s = (int)c.Slot;
                    p.GoalState[s] = c.Value;
                    p.GoalMask[s]  = 1;
                    p.GoalOps[s]   = (int)c.Op; // CompareOp 값 = 잡 GoalOps 규약 (0/1/2)
                }

                p.Actions = new NativeArray<GOAPActionDef>(_defs, alloc);
                if (costMult != null)
                {
                    // 성격 배율을 per-request 사본에 굽는다 (ADR-M4-1). 잡이 min(BaseCost)를
                    // 실측하므로 휴리스틱 정합 자동 유지. 0 이하 방지 클램프.
                    for (int i = 0; i < _defs.Length && i < costMult.Length; i++)
                    {
                        if (Mathf.Approximately(costMult[i], 1f)) continue;
                        GOAPActionDef d = p.Actions[i];
                        d.BaseCost = _defs[i].BaseCost * Mathf.Max(0.1f, costMult[i]);
                        p.Actions[i] = d;
                    }
                }
                p.MaxGain = new NativeArray<float>(_maxGain, alloc);
                p.MaxDrop = new NativeArray<float>(_maxDrop, alloc);

                p.ResultActions = new NativeArray<int>(PlanningConfig.MaxPlanLen, alloc);
                p.ResultLength  = new NativeArray<int>(1, alloc);
                p.NodesExpanded = new NativeArray<int>(1, alloc);

                int maxNodes = PlanningConfig.MaxNodes;
                p.NodeStates  = new NativeArray<int>(maxNodes * totalSlots, alloc);
                p.NodeCosts   = new NativeArray<float>(maxNodes, alloc);
                p.NodeGCosts  = new NativeArray<float>(maxNodes, alloc);
                p.NodeDepths  = new NativeArray<int>(maxNodes, alloc);
                p.NodeParents = new NativeArray<int>(maxNodes, alloc);
                p.NodeActions = new NativeArray<int>(maxNodes, alloc);
                p.OpenQueue   = new NativeArray<int>(maxNodes, alloc);
                p.QueueSize   = new NativeArray<int>(1, alloc);
                p.VisitedHashes  = new NativeArray<int>(maxNodes * 2, alloc);
                p.VisitedNodeIdx = new NativeArray<int>(maxNodes * 2, alloc);
                p.VisitedGCosts  = new NativeArray<float>(maxNodes * 2, alloc);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlannerGateway] 버퍼 할당 실패: {e.Message}");
                DisposeAll(p);
                return null;
            }

            var job = new GOAPPlannerJob
            {
                CurrentState = p.CurrentState,
                GoalState    = p.GoalState,
                GoalMask     = p.GoalMask,
                GoalOps      = p.GoalOps,
                Actions      = p.Actions,
                ResultActions = p.ResultActions,
                ResultLength  = p.ResultLength,
                NodesExpanded = p.NodesExpanded,
                NodeStates  = p.NodeStates,
                NodeCosts   = p.NodeCosts,
                NodeGCosts  = p.NodeGCosts,
                NodeDepths  = p.NodeDepths,
                NodeParents = p.NodeParents,
                NodeActions = p.NodeActions,
                OpenQueue   = p.OpenQueue,
                QueueSize   = p.QueueSize,
                VisitedHashes  = p.VisitedHashes,
                VisitedNodeIdx = p.VisitedNodeIdx,
                VisitedGCosts  = p.VisitedGCosts,
                MaxGain = p.MaxGain,
                MaxDrop = p.MaxDrop,
            };

            p.Handle      = job.Schedule();
            p.Alive       = true;
            p.RequestedAt = Time.realtimeSinceStartup;
            return p;
        }

        /// <summary>
        /// 완료 폴링. false = 아직 실행 중 (status Pending).
        /// true 반환 시 plan/nodesExpanded가 유효하며 버퍼는 해제 완료 상태다.
        /// </summary>
        public bool TryGetResult(PendingPlan p, out PlanStatus status, out ActionSO[] plan, out int nodesExpanded)
        {
            plan = null;
            nodesExpanded = 0;
            status = PlanStatus.Pending;

            if (p == null || !p.Alive)
            {
                Debug.LogWarning("[PlannerGateway] TryGetResult: 유효하지 않은 PendingPlan.");
                status = PlanStatus.NoSolution;
                return true;
            }
            if (!p.Handle.IsCompleted) return false;

            p.Handle.Complete();
            int len = p.ResultLength[0];
            nodesExpanded = p.NodesExpanded[0];

            if (len == -1)
            {
                status = PlanStatus.AlreadySatisfied;
                plan = Array.Empty<ActionSO>();
            }
            else if (len == 0)
            {
                status = PlanStatus.NoSolution;
            }
            else
            {
                plan = new ActionSO[len];
                for (int i = 0; i < len; i++)
                {
                    int idx = p.ResultActions[i]; // ADR-M0-6: 값 = 카탈로그 인덱스
                    if (idx < 0 || idx >= _catalog.Actions.Length)
                    {
                        Debug.LogError($"[PlannerGateway] 결과 인덱스 {idx} 범위 초과 — NoSolution으로 강등.");
                        DisposeAll(p);
                        status = PlanStatus.NoSolution;
                        plan = null;
                        return true;
                    }
                    plan[i] = _catalog.Actions[idx];
                }
                status = PlanStatus.Success;
            }

            DisposeAll(p);
            return true;
        }

        /// <summary>잡을 동기 완료시킨다 (EditMode 게이트·강제 완료용). 이후 TryGetResult가 즉시 true.</summary>
        public void CompleteNow(PendingPlan p)
        {
            if (p != null && p.Alive) p.Handle.Complete();
        }

        /// <summary>타임아웃/사망 등으로 요청을 중단한다. Complete 후 해제 (leak 방지, ADR-T6 계승).</summary>
        public void Cancel(PendingPlan p)
        {
            if (p == null || !p.Alive) return;
            p.Handle.Complete();
            DisposeAll(p);
        }

        private static void DisposeAll(PendingPlan p)
        {
            if (p.CurrentState.IsCreated)  p.CurrentState.Dispose();
            if (p.GoalState.IsCreated)     p.GoalState.Dispose();
            if (p.GoalMask.IsCreated)      p.GoalMask.Dispose();
            if (p.GoalOps.IsCreated)       p.GoalOps.Dispose();
            if (p.Actions.IsCreated)       p.Actions.Dispose();
            if (p.ResultActions.IsCreated) p.ResultActions.Dispose();
            if (p.ResultLength.IsCreated)  p.ResultLength.Dispose();
            if (p.NodesExpanded.IsCreated) p.NodesExpanded.Dispose();
            if (p.NodeStates.IsCreated)    p.NodeStates.Dispose();
            if (p.NodeCosts.IsCreated)     p.NodeCosts.Dispose();
            if (p.NodeGCosts.IsCreated)    p.NodeGCosts.Dispose();
            if (p.NodeDepths.IsCreated)    p.NodeDepths.Dispose();
            if (p.NodeParents.IsCreated)   p.NodeParents.Dispose();
            if (p.NodeActions.IsCreated)   p.NodeActions.Dispose();
            if (p.OpenQueue.IsCreated)     p.OpenQueue.Dispose();
            if (p.QueueSize.IsCreated)     p.QueueSize.Dispose();
            if (p.VisitedHashes.IsCreated)  p.VisitedHashes.Dispose();
            if (p.VisitedNodeIdx.IsCreated) p.VisitedNodeIdx.Dispose();
            if (p.VisitedGCosts.IsCreated)  p.VisitedGCosts.Dispose();
            if (p.MaxGain.IsCreated)        p.MaxGain.Dispose();
            if (p.MaxDrop.IsCreated)        p.MaxDrop.Dispose();
            p.Alive = false;
        }
    }
}
