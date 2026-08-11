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
        // ⚠️ readonly 아님 (M17-W2): 세율이 바뀌면 Recompile이 넷을 통째로 갈아 끼운다.
        // 안전한 이유는 디스패치가 _defs를 NativeArray로 **복사**하기 때문이다 (BeginPlan) —
        // 진행 중인 플랜은 자기 사본으로 끝까지 돈다 (배율은 사본에만, ADR-M4-1).
        private GOAPActionDef[] _defs;              // 컴파일 원본 — 공유 (플랜은 사본을 받는다)
        private float[] _maxGain;
        private float[] _maxDrop;
        private float _minBaseCost;                 // 배율 적용 후 실효 비용의 하한 (휴리스틱 바닥 보호)
        private readonly int _bodyCap;              // 재컴파일 입력 보관 (M17-W2 — 舊: 생성자 지역값)
        private readonly int _homeCap;

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
            _bodyCap = agentCfg != null ? agentCfg.BodyCarryCap : 0;
            _homeCap = agentCfg != null ? agentCfg.HomeStorageCap : 0;
            // 컴파일 1회 (M19 — 세율 재컴파일 축은 화폐와 함께 철거. _defs가 readonly가 아닌
            // 이력은 M17-W2 주석 참조 — 재컴파일 창구가 다시 필요하면 그 안전성 논증부터 볼 것)
            _defs = ActionCompiler.CompileManaged(_catalog, _bodyCap, _homeCap);
            ActionCompiler.ComputeMaxGainDrop(_defs, PlanningConfig.TotalSlots, out _maxGain, out _maxDrop);
            _minBaseCost = MinBaseCost(_defs);
        }

        /// <summary>카탈로그 최소 BaseCost (순수 — 게이트 대상). 빈 배열이면 0 = 하한 없음.</summary>
        public static float MinBaseCost(GOAPActionDef[] defs)
        {
            if (defs == null || defs.Length == 0) return 0f;
            float min = float.MaxValue;
            for (int i = 0; i < defs.Length; i++)
                if (defs[i].BaseCost < min) min = defs[i].BaseCost;
            return min;
        }

        /// <summary>
        /// 배율 적용 후 실효 비용 (순수 — 게이트 대상, 2026-07-26).
        /// 잡의 휴리스틱은 h = 스텝 추정 × min(BaseCost)라, 배율이 최소 비용을 끌어내리면
        /// 휴리스틱이 수축해 노드가 폭발한다 (MAX_NODES 소진 → NoSolutionFound 오탐 —
        /// 2026-07-22 석재 goal 사고와 같은 실패 모드). 성격×직업×편차가 곱셈 누적이라
        /// 오늘도 농사꾼 성격 0.7 × 농부 직업 0.6 × 편차 0.9 = 0.378까지 내려간다.
        /// 하한을 카탈로그 min으로 잡으면 min이 절대 내려가지 않아 휴리스틱이 불변이고,
        /// 배율은 min보다 비싼 액션들 사이에서 정상 작동한다 (차별화 보존).
        /// </summary>
        public static float EffectiveCost(float baseCost, float mult, float catalogMin)
            => Mathf.Max(catalogMin, baseCost * Mathf.Max(0.1f, mult));

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
                    // 성격 배율을 per-request 사본에 굽는다 (ADR-M4-1). 실효 비용의 하한은
                    // 카탈로그 min — 배율이 휴리스틱 바닥을 뚫지 못하게 한다 (EffectiveCost 주석).
                    for (int i = 0; i < _defs.Length && i < costMult.Length; i++)
                    {
                        if (Mathf.Approximately(costMult[i], 1f)) continue;
                        GOAPActionDef d = p.Actions[i];
                        d.BaseCost = EffectiveCost(_defs[i].BaseCost, costMult[i], _minBaseCost);
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

        // NativeArray는 내부 포인터를 든 구조체라 값 복사본을 Dispose해도 같은 버퍼가 해제된다.
        // 재사용은 Alive=false가 막는다 (Cancel/Complete 진입부의 Alive 검사).
        private static void D<T>(NativeArray<T> a) where T : struct { if (a.IsCreated) a.Dispose(); }

        private static void DisposeAll(PendingPlan p)
        {
            D(p.CurrentState); D(p.GoalState); D(p.GoalMask); D(p.GoalOps); D(p.Actions);
            D(p.ResultActions); D(p.ResultLength); D(p.NodesExpanded);
            D(p.NodeStates); D(p.NodeCosts); D(p.NodeGCosts); D(p.NodeDepths);
            D(p.NodeParents); D(p.NodeActions); D(p.OpenQueue); D(p.QueueSize);
            D(p.VisitedHashes); D(p.VisitedNodeIdx); D(p.VisitedGCosts);
            D(p.MaxGain); D(p.MaxDrop);
            p.Alive = false;
        }
    }
}
