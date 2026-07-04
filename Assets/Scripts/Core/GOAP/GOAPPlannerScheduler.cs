/// <summary>
/// GOAPPlannerScheduler.cs - GOAPPlannerJob 스케줄링 단일 진입점
///
/// 역할(Role): VillagerFSM이 GOAP 플래닝을 요청할 때 사용하는 파사드(Facade) 클래스.
///             NativeArray 할당, Job 구성, 스케줄링, 결과 읽기, Dispose를 모두 캡슐화한다.
///             VillagerFSM은 PlanningContext 구조체만 보관하면 된다.
///
/// 사용법(Usage):
///   // EnterPlanning()에서 Job 스케줄
///   _planningContext = GOAPPlannerScheduler.Schedule(goalId, role, brain, worldState, registry);
///
///   // State_Planning()에서 완료 폴링 → 결과 읽기
///   if (_planningContext.JobHandle.IsCompleted)
///   {
///       _planningContext.JobHandle.Complete();
///       List<string> plan = _planningContext.ReadResult();
///       _planningContext.Dispose();
///   }
///
/// 의존성(Dependencies):
///   GOAPPlanningSlots, GOAPActionRegistry, GOAPPlannerJob
///   AIVillage.AI (VillagerBrain, AgentRole)
///   AIVillage.Core (AuthoritativeWorldState, ResourceRegistry)
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-26
/// </summary>

using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using AIVillage.AI;
using AIVillage.Core;

namespace AIVillage.Core.GOAP
{
    /// <summary>
    /// GOAPPlannerJob의 스케줄링 및 결과 관리를 담당하는 정적 파사드 클래스.
    /// </summary>
    public static class GOAPPlannerScheduler
    {
        // ── 컨텍스트 비용 배율 상수 ──────────────────────────────────────────────
        private const float DISTANCE_SCALE      = 50f;   // 50타일 거리에서 최대 배율 도달
        private const float DISTANCE_WEIGHT     = 1.5f;  // 거리 최대 추가 배율
        private const float FULL_NODE_PENALTY   = 5f;    // 해당 자원 노드 전부 포화 시 페널티
        private const float DANGER_GATHER_MULT  = 2.5f;  // 위험 근접 시 수집 액션 비용 증가
        private const float DANGER_ATTACK_MULT  = 0.7f;  // 위험 근접 시 공격 액션 비용 감소
        private const float FATIGUE_THRESHOLD   = 70f;   // 피로 페널티 발동 임계값
        private const float FATIGUE_MAX_EXTRA   = 0.5f;  // 피로 최대 추가 배율 (100일 때 +0.5)
        private const float INJURY_THRESHOLD    = 50f;   // 부상 페널티 발동 임계값 (체력 이 값 미만)
        private const float INJURY_COMBAT_MAX   = 3f;    // 체력 0 근접 시 전투 비용 최대 배율
        private const float EXPLORE_UNDISCOVERED_MULT = 0.6f; // 미발견 자원 타입 존재 시 탐험 비용 감소
        // ────────────────────────────────────────────────────────────────────
        // PlanningContext: VillagerFSM이 Planning 상태 동안 보관하는 구조체
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// VillagerFSM이 Planning 상태 동안 보관하는 플래닝 컨텍스트.
        /// Job 핸들, 입출력 NativeArray, 내부 버퍼를 모두 포함한다.
        ///
        /// Dispose 규칙 (NativeArray 누수 방지):
        ///   - JobHandle.Complete() 후 ReadResult() 호출 완료 시 반드시 Dispose() 호출
        ///   - 타임아웃으로 Job을 강제 취소할 때: Complete() 후 Dispose()
        ///   - VillagerFSM.OnDestroy()에서 IsScheduled==true이면 Complete() 후 Dispose()
        /// </summary>
        public struct PlanningContext
        {
            // ── Job 핸들 및 상태 플래그 ─────────────────────────────────────

            /// <summary>스케줄된 Job의 핸들. IsCompleted 폴링과 Complete() 호출에 사용한다.</summary>
            public JobHandle JobHandle;

            /// <summary>
            /// Job이 스케줄되었는지 여부.
            /// false이면 NativeArray가 할당되지 않았으므로 Dispose 불필요.
            /// </summary>
            public bool IsScheduled;

            // ── Job 입력 NativeArray ─────────────────────────────────────────

            /// <summary>플래닝 시작 시점의 GOAP 월드 스테이트 (43 슬롯).</summary>
            public NativeArray<int> CurrentState;

            /// <summary>달성해야 하는 목표 슬롯 값 (43 슬롯).</summary>
            public NativeArray<int> GoalState;

            /// <summary>목표 판정 마스크 (43 슬롯). 1=포함, 0=무시.</summary>
            public NativeArray<int> GoalMask;

            /// <summary>역할 보정이 반영된 Action 정의 목록 (19개).</summary>
            public NativeArray<GOAPActionDef> Actions;

            // ── Job 출력 NativeArray ─────────────────────────────────────────

            /// <summary>결과 Action 해시 배열 (최대 GOAPPlannerJob.MAX_PLAN_LEN).</summary>
            public NativeArray<int> ResultActions;

            /// <summary>ResultActions의 실제 유효 길이. [0] = 0이면 플래닝 실패.</summary>
            public NativeArray<int> ResultLength;

            // ── 내부 작업 버퍼 (Job 실행 중에만 유효) ───────────────────────

            /// <summary>노드별 월드 스테이트 버퍼 (MAX_NODES x TOTAL_SLOTS).</summary>
            public NativeArray<int>   NodeStates;

            /// <summary>노드별 f-cost 버퍼 (MAX_NODES).</summary>
            public NativeArray<float> NodeCosts;

            // [PR Fix]: Critical-1 — NodeGCosts 필드 추가 (g-cost 별도 추적)
            /// <summary>노드별 g-cost 버퍼 (MAX_NODES). 루트에서 해당 노드까지의 실제 비용.</summary>
            public NativeArray<float> NodeGCosts;

            /// <summary>노드별 깊이 버퍼 (MAX_NODES).</summary>
            public NativeArray<int>   NodeDepths;

            /// <summary>노드별 부모 인덱스 버퍼 (MAX_NODES).</summary>
            public NativeArray<int>   NodeParents;

            /// <summary>노드별 진입 Action 인덱스 버퍼 (MAX_NODES).</summary>
            public NativeArray<int>   NodeActions;

            /// <summary>최소 힙 인덱스 버퍼 (MAX_NODES).</summary>
            public NativeArray<int>   OpenQueue;

            /// <summary>힙 현재 크기 버퍼 (크기 1). QueueSize[0]에 실제 힙 크기 저장.</summary>
            public NativeArray<int>   QueueSize;

            // ────────────────────────────────────────────────────────────────
            // 공개 메서드
            // ────────────────────────────────────────────────────────────────

            /// <summary>
            /// Job 완료 후 ResultActions를 List&lt;string&gt; ActionId 목록으로 변환하여 반환한다.
            ///
            /// 전제 조건: 이 메서드를 호출하기 전에 반드시 JobHandle.Complete()를 호출해야 한다.
            ///            Complete() 없이 호출하면 경쟁 조건이 발생한다.
            ///
            /// [PR Fix]: N-001 — 시그니처 변경: out bool alreadySatisfied 파라미터 추가
            /// ResultLength[0] = -1(이미 달성) / 0(실패) / 양수(성공) 세 가지 경우를 명확히 분기한다.
            ///
            /// 반환값:
            ///   - alreadySatisfied=true : new List&lt;string&gt;() (빈 리스트, 액션 불필요)
            ///   - 실패 (planLength &lt;= 0) : alreadySatisfied=false, null 반환
            ///   - 성공 (planLength &gt; 0)  : alreadySatisfied=false, ActionId 목록 반환
            /// </summary>
            public List<string> ReadResult(out bool alreadySatisfied)
            {
                // 기본값: 아직 달성되지 않은 것으로 초기화
                alreadySatisfied = false;

                if (!IsScheduled)
                {
                    Debug.LogWarning(
                        "[GOAPPlannerScheduler] ReadResult: IsScheduled가 false입니다. " +
                        "Schedule()이 성공적으로 호출되지 않았습니다."
                    );
                    return null;
                }

                int planLength = ResultLength[0];

                // [PR Fix]: N-001 — planLength == -1: 이미 목표 달성 특수값 처리
                // GOAPPlannerJob.Execute()에서 현재 상태가 이미 목표를 만족할 때 -1을 설정한다.
                // 이 경우 빈 플랜(액션 없이 Executing으로 전이)을 의미한다.
                if (planLength == -1)
                {
                    alreadySatisfied = true;
                    return new List<string>(); // 빈 리스트 = 실행할 액션이 없음 (이미 달성)
                }

                // planLength == 0: 플래닝 실패 (해결책 없음 또는 탐색 공간 소진)
                if (planLength <= 0) return null;

                // planLength > 0: 정상 플랜 — 해시 → ActionId 문자열 변환
                var result = new List<string>(planLength);
                for (int i = 0; i < planLength; i++)
                {
                    string actionId = GOAPActionRegistry.HashToActionId(ResultActions[i]);
                    result.Add(actionId);
                }

                return result;
            }

            /// <summary>
            /// 이 컨텍스트가 보유한 모든 NativeArray를 안전하게 해제한다.
            ///
            /// 안전 처리:
            ///   - IsScheduled가 false이면 아무 작업도 하지 않는다.
            ///   - IsCreated 체크로 이미 해제된 배열의 중복 Dispose를 방지한다.
            ///   - Dispose 후 IsScheduled = false로 설정하여 재진입을 차단한다.
            ///
            /// 주의: Dispose 전에 반드시 JobHandle.Complete()를 호출해야 한다.
            ///       진행 중인 Job의 NativeArray를 Dispose하면 크래시가 발생한다.
            /// </summary>
            public void Dispose()
            {
                if (!IsScheduled) return;

                // 각 NativeArray IsCreated 체크 후 해제
                if (CurrentState.IsCreated)  CurrentState.Dispose();
                if (GoalState.IsCreated)     GoalState.Dispose();
                if (GoalMask.IsCreated)      GoalMask.Dispose();
                if (Actions.IsCreated)       Actions.Dispose();
                if (ResultActions.IsCreated) ResultActions.Dispose();
                if (ResultLength.IsCreated)  ResultLength.Dispose();
                if (NodeStates.IsCreated)    NodeStates.Dispose();
                if (NodeCosts.IsCreated)     NodeCosts.Dispose();
                // [PR Fix]: Critical-1 — NodeGCosts Dispose 추가
                if (NodeGCosts.IsCreated)    NodeGCosts.Dispose();
                if (NodeDepths.IsCreated)    NodeDepths.Dispose();
                if (NodeParents.IsCreated)   NodeParents.Dispose();
                if (NodeActions.IsCreated)   NodeActions.Dispose();
                if (OpenQueue.IsCreated)     OpenQueue.Dispose();
                if (QueueSize.IsCreated)     QueueSize.Dispose();

                IsScheduled = false;
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Schedule: 플래닝 Job을 생성하고 스케줄링한다
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// GOAP 플래닝 Job을 구성하고 Job System에 스케줄링한 뒤 PlanningContext를 반환한다.
        ///
        /// 이 메서드는 메인 스레드에서만 호출해야 한다.
        /// 반환된 PlanningContext.JobHandle.IsCompleted로 완료 여부를 폴링하고,
        /// Complete() 후 ReadResult()로 결과를 읽고, Dispose()로 메모리를 해제한다.
        ///
        /// 실패 케이스 (IsScheduled=false 반환):
        ///   - worldState 또는 brain이 null
        ///   - goalId가 null 또는 빈 문자열
        ///   - 알 수 없는 goalId (goalMask가 모두 0)
        ///   - NativeArray 할당 실패 (OutOfMemoryException 등)
        ///   → VillagerFSM.State_Planning()이 IsScheduled=false를 감지하여 Replanning으로 전이
        /// </summary>
        /// <param name="goalId">달성할 Goal ID 문자열.</param>
        /// <param name="role">에이전트 역할 (비용 보정에 사용).</param>
        /// <param name="brain">에이전트 현재 상태 (환경 플래그, 인벤토리 등).</param>
        /// <param name="worldState">전역 자원/건물 상태.</param>
        /// <param name="registry">자원 가용량 계산용 예약 관리자. null 허용 (WorldState 직접 읽기로 폴백).</param>
        /// <returns>스케줄된 플래닝 컨텍스트. 실패 시 IsScheduled=false인 default 반환.</returns>
        public static PlanningContext Schedule(
            string                  goalId,
            AgentRole               role,
            VillagerBrain           brain,
            AuthoritativeWorldState worldState,
            ResourceRegistry        registry)
        {
            // ── 방어 코드 ────────────────────────────────────────────────────
            if (worldState == null || brain == null)
            {
                Debug.LogWarning(
                    $"[GOAPPlannerScheduler] Schedule: worldState 또는 brain이 null입니다. " +
                    $"IsScheduled=false를 반환합니다. goalId={goalId}"
                );
                return default; // IsScheduled = false
            }

            if (string.IsNullOrEmpty(goalId))
            {
                Debug.LogWarning(
                    "[GOAPPlannerScheduler] Schedule: goalId가 null 또는 빈 문자열입니다. " +
                    "IsScheduled=false를 반환합니다."
                );
                return default;
            }

            // ── NativeArray 할당 ─────────────────────────────────────────────
            // [PR Fix]: Critical-2 — Allocator.TempJob → Allocator.Persistent 변경
            // TempJob은 4프레임(~66ms) 이내 Dispose가 강제됨.
            // PLANNING_TIMEOUT_SEC(0.5초) = ~30프레임이므로 TempJob 제한을 초과.
            // Persistent는 수명 제한이 없으므로 VillagerFSM Dispose 타이밍에 맞출 수 있다.
            // 주의: Persistent 배열은 반드시 Dispose()를 호출해야 한다 (자동 해제 없음).
            const Allocator alloc = Allocator.Persistent;

            int totalSlots = GOAPPlanningSlots.TOTAL_SLOTS;
            int maxNodes   = GOAPPlannerJob.MAX_NODES;
            int maxPlanLen = GOAPPlannerJob.MAX_PLAN_LEN;

            // ── NativeArray 할당 (OOM 방어 try-catch) ───────────────────────
            // [PR Fix]: Warning-1 — NativeArray 할당 전체를 try-catch로 감싸 OOM 시 안전 해제
            // 할당 전에 default로 초기화하여 catch 블록에서 IsCreated 체크가 가능하게 한다.
            NativeArray<int>           currentState  = default;
            NativeArray<int>           goalState     = default;
            NativeArray<int>           goalMask      = default;
            NativeArray<GOAPActionDef> actions       = default;
            NativeArray<int>           resultActions = default;
            NativeArray<int>           resultLength  = default;
            NativeArray<int>           nodeStates    = default;
            NativeArray<float>         nodeCosts     = default;
            NativeArray<float>         nodeGCosts    = default;
            NativeArray<int>           nodeDepths    = default;
            NativeArray<int>           nodeParents   = default;
            NativeArray<int>           nodeActions   = default;
            NativeArray<int>           openQueue     = default;
            NativeArray<int>           queueSize     = default;

            try
            {
                // ── 입력 배열 생성 ───────────────────────────────────────────
                currentState = GOAPStateUtil.BuildCurrentState(worldState, registry, brain, alloc);
                GOAPStateUtil.BuildGoalState(goalId, out goalState, out goalMask, alloc);
                // [14단계] Autumn 계절 배율: SeasonManager에서 메인 스레드 읽기 후 Burst Job 외부에서 주입
                float seasonGatherMod = SeasonManager.Instance != null
                    ? SeasonManager.Instance.GetCurrentGatherCostModifier()
                    : 1.0f;
                // [Phase 1 확장] 컨텍스트 비용: 거리·포화·위험·피로 배율 계산 (메인 스레드에서만)
                ContextCostMultipliers contextMult = ComputeContextMultipliers(brain);
                actions = GOAPActionRegistry.BuildActionDefs(role, alloc, seasonGatherMod, contextMult);

                // [PR Fix]: Major-2 — goalMask all-zero 체크: 알 수 없는 goalId 방어
                // goalMask가 모두 0이면 GoalState를 설정한 case가 없다는 뜻이다.
                // 이 상태로 Job을 스케줄하면 즉시 "목표 달성"으로 판정되어 빈 플랜이 반환된다.
                // 조기 종료하여 불필요한 배열 할당과 잘못된 플래닝을 방지한다.
                bool hasMask = false;
                for (int s = 0; s < GOAPPlanningSlots.TOTAL_SLOTS; s++)
                {
                    if (goalMask[s] != 0) { hasMask = true; break; }
                }
                if (!hasMask)
                {
                    // 이미 할당된 배열 안전 해제 후 early return
                    if (currentState.IsCreated) currentState.Dispose();
                    if (goalState.IsCreated)    goalState.Dispose();
                    if (goalMask.IsCreated)     goalMask.Dispose();
                    if (actions.IsCreated)      actions.Dispose();
                    Debug.LogWarning(
                        $"[GOAPPlannerScheduler] 알 수 없는 goalId '{goalId}'. " +
                        $"GoalMask가 모두 0입니다. Schedule 중단."
                    );
                    return default;
                }

                // ── 출력 배열 생성 ───────────────────────────────────────────
                resultActions = new NativeArray<int>(maxPlanLen, alloc);
                resultLength  = new NativeArray<int>(1, alloc);

                // ── 내부 버퍼 생성 ───────────────────────────────────────────
                nodeStates  = new NativeArray<int>  (maxNodes * totalSlots, alloc);
                nodeCosts   = new NativeArray<float>(maxNodes, alloc);
                // [PR Fix]: Critical-1 — nodeGCosts 배열 할당 (g-cost 별도 관리)
                nodeGCosts  = new NativeArray<float>(maxNodes, alloc);
                nodeDepths  = new NativeArray<int>  (maxNodes, alloc);
                nodeParents = new NativeArray<int>  (maxNodes, alloc);
                nodeActions = new NativeArray<int>  (maxNodes, alloc);
                openQueue   = new NativeArray<int>  (maxNodes, alloc);
                queueSize   = new NativeArray<int>  (1, alloc);
            }
            catch (System.Exception e)
            {
                // [PR Fix]: Warning-1 — OOM 등 할당 실패 시 IsCreated인 배열 모두 해제
                Debug.LogError($"[GOAPPlannerScheduler] NativeArray 할당 실패: {e.Message}");
                if (currentState.IsCreated)  currentState.Dispose();
                if (goalState.IsCreated)     goalState.Dispose();
                if (goalMask.IsCreated)      goalMask.Dispose();
                if (actions.IsCreated)       actions.Dispose();
                if (resultActions.IsCreated) resultActions.Dispose();
                if (resultLength.IsCreated)  resultLength.Dispose();
                if (nodeStates.IsCreated)    nodeStates.Dispose();
                if (nodeCosts.IsCreated)     nodeCosts.Dispose();
                if (nodeGCosts.IsCreated)    nodeGCosts.Dispose();
                if (nodeDepths.IsCreated)    nodeDepths.Dispose();
                if (nodeParents.IsCreated)   nodeParents.Dispose();
                if (nodeActions.IsCreated)   nodeActions.Dispose();
                if (openQueue.IsCreated)     openQueue.Dispose();
                if (queueSize.IsCreated)     queueSize.Dispose();
                return default;
            }

            // ── Job 구성 ─────────────────────────────────────────────────────
            var job = new GOAPPlannerJob
            {
                CurrentState  = currentState,
                GoalState     = goalState,
                GoalMask      = goalMask,
                Actions       = actions,
                ResultActions = resultActions,
                ResultLength  = resultLength,
                NodeStates    = nodeStates,
                NodeCosts     = nodeCosts,
                // [PR Fix]: Critical-1 — job.NodeGCosts 할당
                NodeGCosts    = nodeGCosts,
                NodeDepths    = nodeDepths,
                NodeParents   = nodeParents,
                NodeActions   = nodeActions,
                OpenQueue     = openQueue,
                QueueSize     = queueSize
            };

            // ── Job 스케줄링 ─────────────────────────────────────────────────
            // Schedule()은 즉시 반환된다. 워커 스레드에서 비동기 실행.
            // 의존성 없음(default) — 다른 Job과 독립적으로 스케줄된다.
            JobHandle handle = job.Schedule();

            // ── PlanningContext 구성 및 반환 ─────────────────────────────────
            return new PlanningContext
            {
                JobHandle     = handle,
                IsScheduled   = true,
                CurrentState  = currentState,
                GoalState     = goalState,
                GoalMask      = goalMask,
                Actions       = actions,
                ResultActions = resultActions,
                ResultLength  = resultLength,
                NodeStates    = nodeStates,
                NodeCosts     = nodeCosts,
                // [PR Fix]: Critical-1 — PlanningContext 반환 시 NodeGCosts 포함
                NodeGCosts    = nodeGCosts,
                NodeDepths    = nodeDepths,
                NodeParents   = nodeParents,
                NodeActions   = nodeActions,
                OpenQueue     = openQueue,
                QueueSize     = queueSize
            };
        }

        // ────────────────────────────────────────────────────────────────────
        // 컨텍스트 비용 계산 (메인 스레드 전용)
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 에이전트의 현재 위치·상태·주변 환경을 분석하여 액션별 비용 배율을 계산한다.
        /// Job 스케줄 직전 메인 스레드에서 호출된다. Job 내부는 무수정.
        /// </summary>
        private static ContextCostMultipliers ComputeContextMultipliers(VillagerBrain brain)
        {
            var mult = ContextCostMultipliers.Identity;

            System.Collections.Generic.IReadOnlyList<ResourceNode> nodes =
                SensorSystem.Instance?.GetAllNodes();
            if (nodes == null || nodes.Count == 0) return mult;

            float agentX = brain.TileX;
            float agentY = brain.TileY;

            // 자원 타입별 거리 + 포화 배율 — 노드 리스트를 1회 패스로 전체 처리 (C6 개선)
            ComputeAllGatherMults(agentX, agentY, nodes, ref mult, out bool anyUndiscovered);

            // 위험 근접: 수집·탐험 비용 상승, 공격 비용 하락
            if (brain.NearEnemy)
            {
                mult.ChopWood       *= DANGER_GATHER_MULT;
                mult.MineStone      *= DANGER_GATHER_MULT;
                mult.MineIron       *= DANGER_GATHER_MULT;
                mult.MineCopper     *= DANGER_GATHER_MULT;
                mult.HarvestBerries *= DANGER_GATHER_MULT;
                mult.Explore        *= DANGER_GATHER_MULT;
                mult.AttackEnemy    *= DANGER_ATTACK_MULT;
            }

            // 피로 상태: 노동 비용 상승, 휴식 비용 하락
            if (brain.FatigueLevel > FATIGUE_THRESHOLD)
            {
                float ratio        = (brain.FatigueLevel - FATIGUE_THRESHOLD) / (100f - FATIGUE_THRESHOLD);
                float laborPenalty = 1f + ratio * FATIGUE_MAX_EXTRA;
                mult.ChopWood       *= laborPenalty;
                mult.MineStone      *= laborPenalty;
                mult.MineIron       *= laborPenalty;
                mult.MineCopper     *= laborPenalty;
                mult.HarvestBerries *= laborPenalty;
                // 휴식은 피로할수록 선호 (비용 감소)
                mult.RestOnGround   = UnityEngine.Mathf.Max(0.4f, 1f / laborPenalty);
            }

            // F5: 부상 상태 — 전투 비용 선형 상승 (체력 50 미만부터, 0 근접 시 최대 ×3)
            if (brain.HealthLevel < INJURY_THRESHOLD)
            {
                float ratio = (INJURY_THRESHOLD - brain.HealthLevel) / INJURY_THRESHOLD;
                mult.AttackEnemy *= 1f + ratio * (INJURY_COMBAT_MAX - 1f);
            }

            // F7: 미발견 자원 타입이 하나라도 있으면 탐험 선호 (FoW 개척 유도)
            if (anyUndiscovered && !brain.NearEnemy)
                mult.Explore *= EXPLORE_UNDISCOVERED_MULT;

            return mult;
        }

        /// <summary>
        /// 노드 리스트를 단일 패스로 순회하여 5개 자원 타입 전체의 거리·포화 배율을 계산한다.
        /// 기존 5회 패스(ComputeGatherMult × 5)를 1회 패스로 대체. (C6 최적화)
        /// anyUndiscovered: 발견되지 않은 자원 타입이 하나라도 있으면 true — F7 탐험 유도에 사용.
        /// </summary>
        private static void ComputeAllGatherMults(
            float agentX, float agentY,
            System.Collections.Generic.IReadOnlyList<ResourceNode> nodes,
            ref ContextCostMultipliers mult,
            out bool anyUndiscovered)
        {
            // per-type: bestAvailDist, anyDiscovered, anyAvailable
            float woodDist = float.MaxValue, stoneDist = float.MaxValue,
                  ironDist = float.MaxValue, copperDist = float.MaxValue, foodDist = float.MaxValue;
            bool woodFound = false, stoneFound = false, ironFound = false,
                 copperFound = false, foodFound = false;
            bool woodAvail = false, stoneAvail = false, ironAvail = false,
                 copperAvail = false, foodAvail = false;

            for (int i = 0; i < nodes.Count; i++)
            {
                ResourceNode node = nodes[i];
                if (!node.IsDiscovered || node.CurrentAmount <= 0f) continue;

                float dist = UnityEngine.Mathf.Abs(node.TileX - agentX)
                           + UnityEngine.Mathf.Abs(node.TileY - agentY);
                bool avail = node.IsAvailableForHarvest();

                switch (node.ResourceType)
                {
                    case ResourceType.Wood:
                        woodFound = true;
                        if (avail && dist < woodDist) { woodDist = dist; woodAvail = true; } break;
                    case ResourceType.Stone:
                        stoneFound = true;
                        if (avail && dist < stoneDist) { stoneDist = dist; stoneAvail = true; } break;
                    case ResourceType.Iron:
                        ironFound = true;
                        if (avail && dist < ironDist) { ironDist = dist; ironAvail = true; } break;
                    case ResourceType.Copper:
                        copperFound = true;
                        if (avail && dist < copperDist) { copperDist = dist; copperAvail = true; } break;
                    case ResourceType.RawFood:
                        foodFound = true;
                        if (avail && dist < foodDist) { foodDist = dist; foodAvail = true; } break;
                }
            }

            mult.ChopWood       = GatherMultFromResult(woodFound,   woodAvail,   woodDist);
            mult.MineStone      = GatherMultFromResult(stoneFound,  stoneAvail,  stoneDist);
            mult.MineIron       = GatherMultFromResult(ironFound,   ironAvail,   ironDist);
            mult.MineCopper     = GatherMultFromResult(copperFound, copperAvail, copperDist);
            mult.HarvestBerries = GatherMultFromResult(foodFound,   foodAvail,   foodDist);

            // F7: 발견되지 않은 자원 타입이 하나라도 있으면 탐험을 우선한다
            anyUndiscovered = !woodFound || !stoneFound || !ironFound || !copperFound || !foodFound;
        }

        private static float GatherMultFromResult(bool anyDiscovered, bool anyAvailable, float bestDist)
        {
            // F7: 미발견 자원은 포화 페널티의 2배 — 플래너가 실패할 플랜을 정상 비용으로 선택하는 것을 방지
            if (!anyDiscovered) return FULL_NODE_PENALTY * 2f;
            if (!anyAvailable)  return FULL_NODE_PENALTY;
            return 1f + UnityEngine.Mathf.Min(bestDist / DISTANCE_SCALE, 1f) * DISTANCE_WEIGHT;
        }
    }
}
