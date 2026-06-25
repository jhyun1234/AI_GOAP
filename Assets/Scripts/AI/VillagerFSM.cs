/// <summary>
/// VillagerFSM.cs - 주민 AI 유한 상태 머신 (FSM) 생명주기 관리자
///
/// 역할(Role): GOAP 플래너의 생명주기를 관리하는 FSM.
///             플래너 자체가 아니라 "언제 플래닝을 시작/중단할지"를 결정한다.
///             주민 1명당 1개의 VillagerFSM 컴포넌트를 부착한다.
/// 사용법(Usage):
///   1. Villager GameObject에 이 컴포넌트를 추가한다.
///   2. Inspector에서 tickGroupIndex(0~5)를 주민마다 다르게 설정하여 Tick 분산.
///   3. GameManager.Awake()에서 InjectDependencies()를 호출하여 ResourceRegistry를 주입한다.
///   4. GameManager의 코루틴 또는 FixedUpdate에서 0.1초마다 Tick()을 그룹별로 호출한다.
/// 의존성(Dependencies):
///   - VillagerBrain.cs, VillagerEnums.cs, IAutonomousAgent.cs
///   - ConflictScoreCalculator.cs
///   - AIVillage.Core: ResourceRegistry, AuthoritativeWorldState, WorldStateSnapshot
///
/// Tick 그룹 분산 (성능 예산):
///   60fps 기준 매 프레임 모든 주민을 업데이트하면 과부하가 발생한다.
///   tickGroupIndex(0~5) × 6그룹으로 나누어 프레임당 1/6씩만 Tick을 실행한다.
///   GameManager가 (frameCount % 6 == tickGroupIndex)인 FSM만 Tick()을 호출한다.
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-25
/// </summary>

using System;
using System.Collections;
using UnityEngine;
using AIVillage.Core;

namespace AIVillage.AI
{
    /// <summary>
    /// 주민 AI의 핵심 FSM. GOAP 플래너 생명주기를 관리하고 IAutonomousAgent 계약을 이행한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VillagerFSM : MonoBehaviour, IAutonomousAgent
    {
        #region ── 상수 ──

        // 기획서 수치 — 아래 값들은 기획팀과 협의한 확정값
        private const float PLANNING_TIMEOUT_SEC   = 0.5f;  // 이 시간 초과 시 Replanning
        private const float ACTION_SIMULATE_SEC    = 2.0f;  // 2단계 더미: Action 1개 수행 시뮬레이션 시간
        private const float REFUSE_DISPLAY_SEC     = 3.0f;  // 거부 메시지 표시 시간
        private const float LOD_TICK_INTERVAL_SEC  = 0.5f;  // LOD 내부 틱 간격
        private const float LOD_GATHER_DURATION    = 3.0f;  // LOD 자원 수집 시뮬레이션 시간
        private const float LOD_MOVE_DURATION      = 2.0f;  // LOD 기지 이동 시뮬레이션 시간
        private const float LOD_DISTANCE_THRESHOLD = 30f;   // 이 타일 거리 초과 시 LOD 모드 진입
        private const float REPLAN_COOLDOWN_MIN    = 0.3f;  // Replanning 쿨다운 최소값
        private const float REPLAN_COOLDOWN_MAX    = 0.5f;  // Replanning 쿨다운 최대값
        private const int   DEADLOCK_THRESHOLD     = 3;     // 이 횟수 이상 Fallback → Deadlock
        private const float LOD_RESOURCE_STOCK_ADD = 5f;    // LOD 수집 완료 시 더미 추가 자원량

        // 기획서 수치: GatherResources Goal 발동 임계값
        private const float GATHER_STOCK_LOW_THRESHOLD  = 30f;  // 이 미만이면 수집 Goal
        private const float GATHER_STOCK_HIGH_THRESHOLD = 50f;  // 이 이상이면 수집 불필요

        // 사망 후 GameObject 비활성화까지 대기 시간 (연출용)
        private const float DEATH_DEACTIVATE_DELAY = 5.0f;

        // [PR Fix]: F-005 — RestOnGround와 Sleep의 피로 회복량을 명명된 상수로 분리.
        // 두 Action의 회복량이 달라 switch case를 분리하여 관리한다.
        private const float REST_ON_GROUND_FATIGUE_RECOVERY = 20f; // 땅에서 쉬기: 회복량 낮음
        private const float SLEEP_FATIGUE_RECOVERY          = 90f; // 수면: 회복량 높음

        #endregion

        #region ── Serialized Fields (Inspector) ──

        [Tooltip("Tick 그룹 인덱스 (0~5). 주민마다 다르게 설정하여 0.1초 Tick 부하를 6프레임에 분산한다.")]
        [SerializeField] private int _tickGroupIndex = 0;

        [Tooltip("초기 역할. GameManager 또는 기획 데이터에서 Awake 후 주입할 수도 있다.")]
        [SerializeField] private AgentRole _initialRole = AgentRole.None;

        [Tooltip("LOD 거리 계산 기준이 되는 기지(Base) 타일 X 좌표.")]
        [SerializeField] private int _baseTileX = 0;

        [Tooltip("LOD 거리 계산 기준이 되는 기지(Base) 타일 Y 좌표.")]
        [SerializeField] private int _baseTileY = 0;

        #endregion

        #region ── Private Fields ──

        // 주민 상태 데이터 (Awake에서 초기화)
        private VillagerBrain _brain;

        // Core 레이어 의존성 (외부에서 InjectDependencies()로 주입)
        private ResourceRegistry _registry;

        // AuthoritativeWorldState는 싱글턴으로 접근 (캐시)
        private AuthoritativeWorldState _worldState;

        // 현재 처리 중인 자원 예약 정보 (Executing 진입 시 기록, Replanning 시 해제)
        private ResourceType _reservedResourceType;
        private float        _reservedAmount;
        private bool         _hasActiveReservation = false;

        // 수신된 AI 메시지 임시 저장 (우선순위 정렬 없이 단순 큐 — 2단계)
        // TODO: 3단계에서 우선순위 큐(SortedList)로 교체
        private readonly System.Collections.Generic.Queue<AIMessage> _messageQueue
            = new System.Collections.Generic.Queue<AIMessage>();

        // [PR Fix]: F-007 — DeactivateAfterDelay 코루틴 참조를 필드에 저장하여
        // OnDestroy()에서 명시적으로 정지할 수 있도록 한다. 씬 전환이나 Destroy 시 고아 코루틴 방지.
        private Coroutine _deactivateCoroutine;

        // [PR Fix]: F-004 — 2단계에서 MessageBus가 없으므로 C# static event로 사망 이벤트를 발행한다.
        // GameManager나 UI 시스템이 이 이벤트를 구독하여 사망 처리를 수행한다.
        // 3단계에서 MessageBus 구현 시 이 이벤트를 래핑하거나 교체할 수 있다.
        /// <summary>
        /// 주민 사망 시 발행되는 이벤트.
        /// 파라미터: (agentId, tileX, tileY)
        /// GameManager, UI 등 외부 시스템이 Subscribe하여 사망 후처리를 수행한다.
        /// </summary>
        public static event System.Action<string, int, int> OnVillagerDied;

        #endregion

        #region ── IAutonomousAgent 프로퍼티 구현 ──

        /// <summary>에이전트 고유 ID. Awake()에서 GUID로 자동 생성된다.</summary>
        public string AgentId => _brain?.VillagerId ?? string.Empty;

        /// <summary>현재 FSM 최상위 상태.</summary>
        public VillagerState CurrentState => _brain?.FSMState ?? VillagerState.Dead;

        /// <summary>충성도 (0~100).</summary>
        public float LoyaltyLevel => _brain?.LoyaltyLevel ?? 0f;

        /// <summary>원래 소속 팩션 ID.</summary>
        public int OriginalFactionId => _brain?.OriginalFactionId ?? 0;

        /// <summary>현재 체력 (0~100).</summary>
        public float HealthLevel => _brain?.HealthLevel ?? 0f;

        /// <summary>현재 배고픔 (0~100).</summary>
        public float HungerLevel => _brain?.HungerLevel ?? 0f;

        /// <summary>현재 피로도 (0~100).</summary>
        public float FatigueLevel => _brain?.FatigueLevel ?? 0f;

        /// <summary>생존 여부.</summary>
        public bool IsAlive => _brain?.IsAlive ?? false;

        #endregion

        #region ── Unity 생명주기 메서드 ──

        /// <summary>
        /// Unity가 GameObject 활성화 시 가장 먼저 호출한다.
        /// VillagerBrain을 초기화하고 WorldState 싱글턴 참조를 캐시한다.
        /// </summary>
        private void Awake()
        {
            // VillagerBrain 초기화 — 모든 상태 데이터의 컨테이너
            _brain = new VillagerBrain
            {
                // 런타임 GUID 생성: 에디터에서 복사본을 만들어도 ID가 충돌하지 않는다
                VillagerId       = Guid.NewGuid().ToString(),
                Role             = _initialRole,
                OriginalFactionId = 0, // TODO: 기획팀 — 팩션 ID 할당 방식 확인 필요
                FSMState         = VillagerState.Idle,
                LODState         = LODState.LOD_Idle,
            };

            // AuthoritativeWorldState 싱글턴 캐시
            // Update()에서 직접 접근하지 않기 위해 Awake에서 저장
            _worldState = AuthoritativeWorldState.Instance;

            if (_worldState == null)
            {
                // GameManager보다 VillagerFSM이 먼저 Awake될 경우 발생할 수 있다.
                // InjectDependencies()에서 재할당하므로 경고만 출력한다.
                Debug.LogWarning($"[VillagerFSM] Awake: AuthoritativeWorldState.Instance가 null입니다. " +
                                 $"GameManager의 Script Execution Order를 VillagerFSM보다 높게 설정하거나 " +
                                 $"InjectDependencies()를 호출하세요. AgentId={_brain.VillagerId}");
            }
        }

        /// <summary>
        /// Unity가 매 프레임 호출한다.
        /// AnyState 전이(Dead, P0)를 감지하고, LOD 모드에서는 틱 누적을 처리한다.
        /// 실제 상태 로직은 외부에서 Tick()을 호출할 때 실행한다.
        /// </summary>
        private void Update()
        {
            if (_brain == null) return;

            // [PR Fix]: F-009 — P0(생존 위기)와 Dead 전이는 Tick 분산 없이 매 프레임 즉시 반응이
            // 필요하므로 Update에서 처리한다. 일반 상태 로직은 외부 GameManager가 Tick()으로
            // 그룹별 호출한다. GameManager 코루틴(0.1초 간격)을 통하면 최대 0.1초 지연이 생기고
            // P0 상황(HP<20, 허기>80, 피로>90)은 그 지연조차 치명적일 수 있기 때문이다.

            // ── AnyState 전이 #1: 사망 감지 ──────────────────────────────────
            // isAlive가 false가 되는 순간 어떤 상태에서도 즉시 Dead로 전이한다.
            // 이 체크는 Update에서 수행하여 Tick 간격(0.1초) 지연 없이 즉시 반응한다.
            if (!_brain.IsAlive && _brain.FSMState != VillagerState.Dead)
            {
                TransitionTo(VillagerState.Dead);
                return;
            }

            // ── AnyState 전이 #2: P0 Goal 긴급 감지 ─────────────────────────
            // 사망 외 P0(생존 위기) 조건이 발동되면 현재 상태에 관계없이 Planning으로 이동.
            // 이미 Planning/Dead 상태이면 중복 전이를 방지한다.
            if (_brain.IsAlive
                && _brain.FSMState != VillagerState.Planning
                && _brain.FSMState != VillagerState.Dead
                && IsP0GoalActive())
            {
                string p0GoalId = GetP0GoalId();

                // 현재 추구 중인 Goal이 이미 P0 Goal과 동일하면 재전이 불필요
                if (_brain.CurrentGoalId != p0GoalId)
                {
                    Debug.Log($"[VillagerFSM] AnyState → Planning (P0 Goal: {p0GoalId}). AgentId={AgentId}");
                    _brain.CurrentGoalId = p0GoalId;
                    TransitionTo(VillagerState.Planning);
                    return;
                }
            }

            // ── LOD 틱 누적 (LOD_FSM 상태일 때만) ───────────────────────────
            // LOD 모드의 내부 상태는 매 프레임이 아닌 0.5초 간격으로만 실행한다.
            if (_brain.FSMState == VillagerState.LOD_FSM)
            {
                _brain.LODTickAccumulator += Time.deltaTime;
            }

            // Replanning 쿨다운 감소
            if (_brain.ReplanCooldown > 0f)
            {
                _brain.ReplanCooldown -= Time.deltaTime;
                if (_brain.ReplanCooldown < 0f) _brain.ReplanCooldown = 0f;
            }

            // RefusingOrder 타이머 감소
            if (_brain.FSMState == VillagerState.RefusingOrder && _brain.RefuseMessageTimer > 0f)
            {
                _brain.RefuseMessageTimer -= Time.deltaTime;
            }
        }

        #endregion

        #region ── IAutonomousAgent 메서드 구현 ──

        /// <summary>
        /// 외부 시스템이 이 에이전트에 메시지를 전달한다.
        /// 메시지는 내부 큐에 쌓이며 다음 Tick()에서 처리된다.
        /// </summary>
        public void ReceiveMessage(AIMessage message)
        {
            _messageQueue.Enqueue(message);
        }

        /// <summary>
        /// 플레이어 명령 수락을 시도한다.
        /// Executing 또는 Planning 중이면 CommandConflict 상태로 전이하여 ConflictScore를 계산한다.
        /// Idle 상태이면 즉시 Planning으로 전이한다.
        /// </summary>
        /// <returns>명령 처리 시작됨 = true, 즉시 거부(사망/null 상태) = false</returns>
        public bool TryExecuteOrder(PlayerOrder order)
        {
            if (_brain == null || !_brain.IsAlive)
            {
                Debug.LogWarning($"[VillagerFSM] TryExecuteOrder: 사망하거나 초기화되지 않은 에이전트. AgentId={AgentId}");
                return false;
            }

            // 명령을 Brain에 저장한 뒤 CommandConflict 상태에서 처리
            _brain.PendingOrder    = order;
            _brain.HasPendingOrder = true;

            // Idle 상태이면 바로 CommandConflict로 진입하여 점수 계산
            // Executing/Planning 중이면 마찬가지로 CommandConflict — 기존 행동과 충돌 평가
            TransitionTo(VillagerState.CommandConflict);
            return true;
        }

        /// <summary>
        /// FSM 상태를 외부에서 강제 전이시킨다. P0 발동이나 Dead 처리에만 사용한다.
        /// </summary>
        public void ForceTransitionTo(VillagerState state)
        {
            if (_brain == null)
            {
                Debug.LogWarning($"[VillagerFSM] ForceTransitionTo({state}): brain이 null입니다.");
                return;
            }

            TransitionTo(state);
        }

        /// <summary>
        /// GameManager에서 ResourceRegistry와 WorldState를 주입한다.
        /// Script Execution Order 문제로 Awake 시점에 싱글턴이 없을 수 있으므로
        /// 이 메서드를 GameManager.Start() 또는 GameManager.Awake() 마지막에 호출한다.
        /// </summary>
        public void InjectDependencies(ResourceRegistry registry)
        {
            if (registry == null)
            {
                Debug.LogError($"[VillagerFSM] InjectDependencies: registry가 null입니다. AgentId={AgentId}");
                return;
            }

            _registry   = registry;
            _worldState = AuthoritativeWorldState.Instance;

            if (_worldState == null)
            {
                Debug.LogError($"[VillagerFSM] InjectDependencies: AuthoritativeWorldState.Instance가 여전히 null입니다. " +
                               $"GameManager가 먼저 초기화되었는지 확인하세요. AgentId={AgentId}");
            }
        }

        #endregion

        #region ── 외부 호출 Tick ──

        /// <summary>
        /// GameManager가 0.1초 간격으로 그룹별로 호출하는 메인 Tick 메서드.
        /// 현재 FSM 상태에 해당하는 상태 핸들러를 실행하고 수신 메시지를 처리한다.
        ///
        /// 호출 예시 (GameManager):
        ///   if (Time.frameCount % 6 == villagerFSM.TickGroupIndex)
        ///       villagerFSM.Tick();
        /// </summary>
        public void Tick()
        {
            if (_brain == null) return;

            // 수신된 메시지를 먼저 처리한다 (상태 전이를 유발할 수 있음)
            ProcessMessageQueue();

            // 현재 상태 핸들러 실행
            switch (_brain.FSMState)
            {
                case VillagerState.Idle:            State_Idle();            break;
                case VillagerState.Planning:         State_Planning();        break;
                case VillagerState.Executing:        State_Executing();       break;
                case VillagerState.Replanning:       State_Replanning();      break;
                case VillagerState.CommandConflict:  State_CommandConflict(); break;
                case VillagerState.RefusingOrder:    State_RefusingOrder();   break;
                case VillagerState.Dead:             State_Dead();            break;
                case VillagerState.LOD_FSM:          State_LOD_FSM();         break;
                default:
                    Debug.LogWarning($"[VillagerFSM] Tick: 처리되지 않은 상태 '{_brain.FSMState}'. AgentId={AgentId}");
                    break;
            }
        }

        /// <summary>Tick 그룹 인덱스. GameManager가 읽어서 호출 여부를 판단한다.</summary>
        public int TickGroupIndex => _tickGroupIndex;

        #endregion

        #region ── 상태 핸들러 메서드 ──

        /// <summary>
        /// [Idle] 다음 Goal을 탐색하는 대기 상태.
        /// 우선순위 순으로 탈출 조건을 확인하여 Planning 또는 LOD_FSM으로 전이한다.
        /// P0 탈출 조건은 Update()의 AnyState 체크가 처리하므로 여기서는 P1 이하만 확인한다.
        /// </summary>
        private void State_Idle()
        {
            // 쿨다운이 남아있으면 P0 외 모든 전이를 차단한다.
            // (P0는 Update()의 AnyState 체크에서 쿨다운 무시하고 처리됨)
            bool cooldownActive = _brain.ReplanCooldown > 0f;

            if (!cooldownActive)
            {
                // 우선순위 P1: 적 위협
                if (_brain.NearEnemy)
                {
                    _brain.CurrentGoalId = "DefendVillage";
                    TransitionTo(VillagerState.Planning);
                    return;
                }

                // 우선순위 P2a: 건설 대기 + 자원 충족 여부
                // 실제 자원 충족 확인은 WorldState를 통해 수행
                if (_worldState != null && _worldState.BuildingQueued && HasResourcesForBuilding())
                {
                    _brain.CurrentGoalId = "BuildStructure";
                    TransitionTo(VillagerState.Planning);
                    return;
                }

                // 우선순위 P2b: 자원 부족 (어떤 자원이든 30 미만)
                if (_worldState != null && IsAnyStockLow())
                {
                    _brain.CurrentGoalId = "GatherResources";
                    TransitionTo(VillagerState.Planning);
                    return;
                }

                // 우선순위 P3: 탐험 (모든 자원이 50 이상이고 미탐험 타일이 있을 때)
                // TODO: 기획팀 — unexploredTilesNearby 플래그를 Brain에 추가할지 별도 시스템으로 관리할지 결정 필요
                // 현재는 WorldState 자원이 모두 50 이상이면 Explore Goal로 전환 (더미)
                if (_worldState != null && AreAllStocksAboveThreshold(GATHER_STOCK_HIGH_THRESHOLD))
                {
                    _brain.CurrentGoalId = "Explore";
                    TransitionTo(VillagerState.Planning);
                    return;
                }
            }

            // 우선순위 P4: LOD 모드 진입 (30타일 초과 + 비전투)
            // 쿨다운과 무관하게 거리 조건만으로 LOD 진입 가능
            if (!_brain.NearEnemy && GetDistanceToBase() > LOD_DISTANCE_THRESHOLD)
            {
                TransitionTo(VillagerState.LOD_FSM);
                return;
            }

            // 위 조건이 모두 해당 없으면 계속 Idle 유지
        }

        /// <summary>
        /// [Planning] GOAP 플래너가 Action 시퀀스를 탐색하는 상태.
        /// 2단계에서는 SimulatePlanResult()로 더미 플랜을 즉시 반환한다.
        /// 3단계에서 실제 Job System 연동으로 교체된다.
        /// </summary>
        private void State_Planning()
        {
            // 타임아웃 체크: 플래닝이 0.5초를 초과하면 Replanning
            if (Time.time - _brain.PlanningStartTime > PLANNING_TIMEOUT_SEC)
            {
                Debug.LogWarning($"[VillagerFSM] Planning 타임아웃 ({PLANNING_TIMEOUT_SEC}초 초과). Replanning으로 전이. AgentId={AgentId}, GoalId={_brain.CurrentGoalId}");
                TransitionTo(VillagerState.Replanning);
                return;
            }

            // 2단계: 더미 플랜 즉시 반환 (Job System은 3단계에서 연동)
            // TODO: 3단계에서 GOAPPlannerJob 스케줄링으로 교체
            GOAPPlanResult planResult = SimulatePlanResult(_brain.CurrentGoalId);

            if (planResult.Success)
            {
                // 유효한 플랜 수신 → Brain의 CurrentPlan 큐에 적재
                _brain.CurrentPlan.Clear();
                if (planResult.ActionSequence != null)
                {
                    foreach (string actionId in planResult.ActionSequence)
                    {
                        _brain.CurrentPlan.Enqueue(actionId);
                    }
                }

                // 재플래닝 횟수 초기화 (성공했으므로)
                _brain.FallbackCounter = 0;

                Debug.Log($"[VillagerFSM] Planning 성공. 플랜: [{string.Join(" → ", planResult.ActionSequence ?? new System.Collections.Generic.List<string>())}]. AgentId={AgentId}");

                TransitionTo(VillagerState.Executing);
            }
            else
            {
                // 해결책을 찾지 못함 → Replanning
                Debug.LogWarning($"[VillagerFSM] Planning 실패 ({planResult.ResultType}). Goal={_brain.CurrentGoalId}. AgentId={AgentId}");
                TransitionTo(VillagerState.Replanning);
            }
        }

        /// <summary>
        /// [Executing] 플래닝된 Action을 순차적으로 실행하는 상태.
        /// 2단계에서는 ACTION_SIMULATE_SEC(2초) 후 Action이 완료된 것으로 시뮬레이션한다.
        /// </summary>
        private void State_Executing()
        {
            // 현재 실행 중인 Action이 완료 시간에 도달했는지 확인
            if (Time.time - _brain.ActionStartTime >= ACTION_SIMULATE_SEC)
            {
                // Action 완료 처리
                OnActionCompleted(_brain.CurrentActionId);

                // 큐에 남은 Action이 있으면 다음 Action 시작
                if (_brain.CurrentPlan.Count > 0)
                {
                    StartNextAction();
                }
                else
                {
                    // 모든 Action 완료 → Goal 달성, Idle로 복귀
                    Debug.Log($"[VillagerFSM] 플랜 완료. Goal={_brain.CurrentGoalId}. AgentId={AgentId}");
                    _brain.CurrentGoalId  = null;
                    _brain.CurrentActionId = null;
                    TransitionTo(VillagerState.Idle);
                }
            }
        }

        /// <summary>
        /// [Replanning] 플래닝 실패 또는 Action 취소 후 쿨다운 대기 상태.
        /// 쿨다운 경과 후 다시 Planning으로 이동한다.
        /// fallbackCounter >= DEADLOCK_THRESHOLD이면 Deadlock 처리를 수행한다.
        /// </summary>
        private void State_Replanning()
        {
            // Deadlock 판정: 너무 많이 실패했으면 강제 Fallback Goal
            if (_brain.FallbackCounter >= DEADLOCK_THRESHOLD)
            {
                HandleDeadlock();
                return;
            }

            // 쿨다운이 끝나면 다시 Planning 시도
            if (_brain.ReplanCooldown <= 0f)
            {
                // P0 Goal이 활성화되어 있으면 해당 Goal로 다시 Planning
                if (IsP0GoalActive())
                {
                    _brain.CurrentGoalId = GetP0GoalId();
                }

                TransitionTo(VillagerState.Planning);
            }

            // 쿨다운이 남아있으면 대기 (Update()에서 매 프레임 감소 처리됨)
        }

        /// <summary>
        /// [CommandConflict] 플레이어 명령과 현재 상태의 충돌 점수를 계산하는 상태.
        /// ConflictScoreCalculator.Calculate()를 호출하여 수락/거부를 즉시 결정한다.
        /// 이 상태는 매 Tick에서 즉시 탈출한다 (대기 상태가 아님).
        /// </summary>
        private void State_CommandConflict()
        {
            if (!_brain.HasPendingOrder)
            {
                // 처리할 명령이 없으면 Idle로 복귀
                Debug.LogWarning($"[VillagerFSM] CommandConflict: HasPendingOrder가 false입니다. Idle로 복귀. AgentId={AgentId}");
                TransitionTo(VillagerState.Idle);
                return;
            }

            // [PR Fix]: F-002 — HasPendingOrder = false를 설정하기 전에 PendingOrder를 로컬 변수에
            // 복사한다. 플래그를 먼저 false로 만든 뒤 _brain.PendingOrder를 다시 읽으면
            // 다른 경로에서 PendingOrder가 덮어쓰일 경우 잘못된 명령을 참조하는 버그가 발생한다.
            PlayerOrder localOrder = _brain.PendingOrder;

            // ConflictScore 계산 (복사된 로컬 명령 데이터 사용)
            ConflictScoreData scoreData = ConflictScoreCalculator.Calculate(_brain, localOrder);

            Debug.Log($"[VillagerFSM] ConflictScore={scoreData.ConflictScore:F2}, Threshold={scoreData.Threshold:F2}, " +
                      $"ShouldRefuse={scoreData.ShouldRefuse}. AgentId={AgentId}");

            // 플래그 소비: PendingOrder 복사 완료 후에 false로 설정한다
            _brain.HasPendingOrder = false;

            if (scoreData.ShouldRefuse)
            {
                // 거부 → RefusingOrder 상태
                TransitionTo(VillagerState.RefusingOrder);
            }
            else
            {
                // 수락 → 로컬 복사본으로 Goal로 변환하여 Planning
                // (HasPendingOrder = false 이후에도 localOrder는 안전하게 참조 가능)
                _brain.CurrentGoalId = ConvertOrderToGoalId(localOrder.OrderType);
                TransitionTo(VillagerState.Planning);
            }
        }

        /// <summary>
        /// [RefusingOrder] 명령 거부를 3초간 유지하고 Idle로 복귀하는 상태.
        /// 실제 MessageBus 구현 전까지 Debug.Log로 거부 사유를 출력한다.
        /// </summary>
        private void State_RefusingOrder()
        {
            // 타이머 경과 → Idle 복귀
            if (_brain.RefuseMessageTimer <= 0f)
            {
                Debug.Log($"[VillagerFSM] 거부 메시지 표시 완료. Idle로 복귀. AgentId={AgentId}");
                TransitionTo(VillagerState.Idle);
            }

            // 타이머 감소는 Update()에서 처리됨
        }

        /// <summary>
        /// [Dead] 사망 처리 상태. 진입 시 한 번만 실행되며 이후 호출은 무시된다.
        /// 자원 예약 해제 → 드롭 아이템 생성 → 메시지 발행 → 5초 후 비활성화 순으로 처리한다.
        /// </summary>
        private void State_Dead()
        {
            // Dead 상태 핸들러는 TransitionTo(Dead) 시 진입 처리에서 모두 수행하므로
            // 이후 반복 Tick에서는 아무 작업도 하지 않는다.
            // GameObject 비활성화는 코루틴으로 지연 처리됨.
        }

        /// <summary>
        /// [LOD_FSM] 원거리 주민을 경량 시뮬레이션으로 처리하는 상태.
        /// 0.5초 간격으로 내부 LOD 상태를 실행한다.
        /// 적 탐지 또는 30타일 이내 복귀 시 Full GOAP(Idle)로 즉시 전환한다.
        /// </summary>
        private void State_LOD_FSM()
        {
            // LOD 탈출 조건: 위협 또는 기지 복귀
            if (_brain.NearEnemy || GetDistanceToBase() <= LOD_DISTANCE_THRESHOLD)
            {
                Debug.Log($"[VillagerFSM] LOD → Full GOAP 복귀 (nearEnemy={_brain.NearEnemy}, dist={GetDistanceToBase():F0}). AgentId={AgentId}");
                _brain.IsLODMode           = false;
                _brain.LODTickAccumulator  = 0f;
                TransitionTo(VillagerState.Idle);
                return;
            }

            // 0.5초 간격 틱
            if (_brain.LODTickAccumulator < LOD_TICK_INTERVAL_SEC) return;
            _brain.LODTickAccumulator -= LOD_TICK_INTERVAL_SEC;

            // 내부 LOD 상태 실행
            switch (_brain.LODState)
            {
                case LODState.LOD_Idle:            LOD_State_Idle();            break;
                case LODState.LOD_GatheringResource: LOD_State_GatheringResource(); break;
                case LODState.LOD_MovingToBase:    LOD_State_MovingToBase();    break;
                case LODState.LOD_Alert:           LOD_State_Alert();           break;
                default:
                    Debug.LogWarning($"[VillagerFSM] LOD_FSM: 처리되지 않은 LOD 상태 '{_brain.LODState}'. AgentId={AgentId}");
                    break;
            }
        }

        #endregion

        #region ── LOD 내부 상태 핸들러 ──

        /// <summary>
        /// [LOD_Idle] 자원 부족 여부를 확인하고 수집 또는 대기를 결정한다.
        /// </summary>
        private void LOD_State_Idle()
        {
            // 자원이 부족하고 주변에 자원 노드가 있으면 수집 시작
            if (_worldState != null && IsAnyStockLow() && _brain.NearResource)
            {
                TransitionToLOD(LODState.LOD_GatheringResource);
            }
            // 그 외에는 대기 유지 (다음 0.5초 틱까지)
        }

        /// <summary>
        /// [LOD_GatheringResource] 자원 수집을 3초간 시뮬레이션하고 MovingToBase로 전이한다.
        /// </summary>
        private void LOD_State_GatheringResource()
        {
            if (Time.time - _brain.LODActionStartTime >= LOD_GATHER_DURATION)
            {
                Debug.Log($"[VillagerFSM] LOD 수집 완료. MovingToBase로 전이. AgentId={AgentId}");
                TransitionToLOD(LODState.LOD_MovingToBase);
            }
        }

        /// <summary>
        /// [LOD_MovingToBase] 기지 이동을 2초간 시뮬레이션하고 자원을 더미 추가한 뒤 LOD_Idle로 전이한다.
        /// </summary>
        private void LOD_State_MovingToBase()
        {
            if (Time.time - _brain.LODActionStartTime >= LOD_MOVE_DURATION)
            {
                // 기지 도달: 수집한 자원을 WorldState에 더미 추가
                ApplyLODResourceGain();

                Debug.Log($"[VillagerFSM] LOD 기지 도달. 자원 납부 완료. LOD_Idle로 전이. AgentId={AgentId}");
                TransitionToLOD(LODState.LOD_Idle);
            }
        }

        /// <summary>
        /// [LOD_Alert] 위협 감지. 즉시 Full GOAP(VillagerFSM.Idle)로 복귀한다.
        /// </summary>
        private void LOD_State_Alert()
        {
            Debug.Log($"[VillagerFSM] LOD_Alert → Full GOAP 복귀. AgentId={AgentId}");
            _brain.IsLODMode           = false;
            _brain.LODTickAccumulator  = 0f;
            TransitionTo(VillagerState.Idle);
        }

        #endregion

        #region ── 상태 전이 메서드 ──

        /// <summary>
        /// FSM 최상위 상태를 전이한다.
        /// 이전 상태의 탈출 정리(Exit) → 새 상태의 진입 초기화(Enter) 순으로 실행한다.
        /// 같은 상태로의 재전이도 허용한다 (재진입을 통한 리셋이 필요한 경우가 있음).
        /// </summary>
        private void TransitionTo(VillagerState newState)
        {
            if (_brain == null) return;

            VillagerState prevState = _brain.FSMState;

            // ── Exit 처리: 이전 상태 정리 ─────────────────────────────────────
            OnStateExit(prevState, newState);

            // 상태 변경
            _brain.FSMState = newState;

            // ── Enter 처리: 새 상태 초기화 ────────────────────────────────────
            OnStateEnter(newState, prevState);
        }

        /// <summary>
        /// LOD 내부 상태를 전이한다. LODActionStartTime을 갱신한다.
        /// </summary>
        private void TransitionToLOD(LODState newLODState)
        {
            if (_brain == null) return;

            _brain.LODState         = newLODState;
            _brain.LODActionStartTime = Time.time;
        }

        /// <summary>
        /// 상태 Exit 처리. 상태별 정리 작업을 수행한다.
        /// 이 메서드는 TransitionTo() 내에서만 호출된다.
        /// </summary>
        private void OnStateExit(VillagerState exitingState, VillagerState enteringState)
        {
            switch (exitingState)
            {
                case VillagerState.Executing:
                    // [PR Fix]: F-003 — 'enteringState != VillagerState.Idle' 조건을 제거한다.
                    // 기존 조건은 Executing → Idle 전이(정상 완료) 시 예약을 해제하지 않아 자원이 누수되었다.
                    // 정상 완료(Goal 달성 후 Idle)는 OnActionCompleted()에서 Commit을 호출하며
                    // Commit이 완료되면 _hasActiveReservation = false로 설정된다.
                    // 따라서 _hasActiveReservation이 true인 채로 이 Exit 코드에 도달하는 경우는
                    // 항상 '완료되지 않은 중단'이므로 조건 없이 무조건 해제한다.
                    if (_hasActiveReservation)
                    {
                        ReleaseCurrentReservation();
                    }
                    _brain.IsExecutingPlan = false;
                    break;

                case VillagerState.LOD_FSM:
                    _brain.IsLODMode          = false;
                    _brain.LODTickAccumulator = 0f;
                    break;
            }
        }

        /// <summary>
        /// 상태 Enter 처리. 새 상태 진입 시 초기화 작업을 수행한다.
        /// 이 메서드는 TransitionTo() 내에서만 호출된다.
        /// </summary>
        private void OnStateEnter(VillagerState enteringState, VillagerState fromState)
        {
            switch (enteringState)
            {
                case VillagerState.Planning:
                    EnterPlanning();
                    break;

                case VillagerState.Executing:
                    EnterExecuting();
                    break;

                case VillagerState.Replanning:
                    EnterReplanning();
                    break;

                case VillagerState.CommandConflict:
                    // 즉시 ConflictScore 계산은 State_CommandConflict()에서 수행
                    break;

                case VillagerState.RefusingOrder:
                    EnterRefusingOrder();
                    break;

                case VillagerState.Dead:
                    EnterDead();
                    break;

                case VillagerState.LOD_FSM:
                    _brain.IsLODMode          = true;
                    _brain.LODState           = LODState.LOD_Idle;
                    _brain.LODTickAccumulator = 0f;
                    _brain.LODActionStartTime = Time.time;
                    break;
            }
        }

        // ── Enter 헬퍼 메서드 ──────────────────────────────────────────────────

        /// <summary>Planning 상태 진입 초기화.</summary>
        private void EnterPlanning()
        {
            // 이전에 실행 중이던 플랜 큐를 비운다
            _brain.CurrentPlan.Clear();
            _brain.IsExecutingPlan  = false;
            _brain.PlanningStartTime = Time.time;

            // 2단계에서는 즉시 WorldStateSnapshot을 생성하고 더미 결과를 반환한다.
            // 3단계에서 Job System 스케줄링으로 교체한다.
            // TODO: 3단계 — WorldStateSnapshot.CreateFrom() 후 GOAPPlannerJob 스케줄링
            if (_worldState != null)
            {
                // 스냅샷은 Planning Tick에서 더미 플래닝 시 사용 (현재는 생성 후 즉시 Dispose)
                // 실제 Job 연결 전까지 스냅샷 생성 테스트용으로만 호출
                using var snapshot = WorldStateSnapshot.CreateFrom(_worldState);
                // snapshot은 2단계에서는 SimulatePlanResult가 Brain을 직접 읽으므로 사용하지 않음
                _ = snapshot; // 컴파일러 경고 억제
            }
        }

        /// <summary>Executing 상태 진입 초기화. 첫 Action을 큐에서 꺼내 실행 시작.</summary>
        private void EnterExecuting()
        {
            _brain.IsExecutingPlan = true;

            if (_brain.CurrentPlan.Count == 0)
            {
                Debug.LogWarning($"[VillagerFSM] EnterExecuting: CurrentPlan이 비어있습니다. Idle로 복귀. AgentId={AgentId}");
                TransitionTo(VillagerState.Idle);
                return;
            }

            StartNextAction();
        }

        /// <summary>다음 Action을 큐에서 꺼내 자원 예약 후 실행 시작.</summary>
        private void StartNextAction()
        {
            _brain.CurrentActionId = _brain.CurrentPlan.Dequeue();
            _brain.ActionStartTime = Time.time;

            // 자원 예약 시도 (Action에 따라 필요한 자원이 다름)
            // 2단계: 간소화된 자원 예약 — 실제 Action별 자원 요구량은 3단계에서 정밀화
            if (!TryReserveForAction(_brain.CurrentActionId))
            {
                // 자원 예약 실패 → Replanning
                Debug.LogWarning($"[VillagerFSM] Action '{_brain.CurrentActionId}' 자원 예약 실패. Replanning으로 전이. AgentId={AgentId}");
                TransitionTo(VillagerState.Replanning);
                return;
            }

            Debug.Log($"[VillagerFSM] Action 시작: '{_brain.CurrentActionId}'. 완료까지 {ACTION_SIMULATE_SEC}초. AgentId={AgentId}");
        }

        /// <summary>Replanning 상태 진입 초기화. 자원 해제 및 쿨다운 설정.</summary>
        private void EnterReplanning()
        {
            // 현재 예약 해제
            ReleaseCurrentReservation();

            // 현재 플랜 초기화
            _brain.CurrentPlan.Clear();
            _brain.IsExecutingPlan = false;

            // 쿨다운 설정 (P0 Goal은 이 쿨다운을 무시하고 Update()에서 강제 전이)
            _brain.ReplanCooldown       = UnityEngine.Random.Range(REPLAN_COOLDOWN_MIN, REPLAN_COOLDOWN_MAX);
            _brain.LastReplanTimestamp  = Time.time;
            _brain.FallbackCounter++;

            // ReplanCount 딕셔너리 갱신
            if (_brain.CurrentGoalId != null)
            {
                if (!_brain.ReplanCount.ContainsKey(_brain.CurrentGoalId))
                    _brain.ReplanCount[_brain.CurrentGoalId] = 0;
                _brain.ReplanCount[_brain.CurrentGoalId]++;
            }

            Debug.Log($"[VillagerFSM] Replanning 진입. FallbackCounter={_brain.FallbackCounter}, Cooldown={_brain.ReplanCooldown:F2}초. AgentId={AgentId}");
        }

        /// <summary>RefusingOrder 상태 진입 초기화. 거부 이유 결정 및 메시지 발행.</summary>
        private void EnterRefusingOrder()
        {
            RefusalReasonCode reason = ConflictScoreCalculator.DetermineReason(_brain);
            _brain.RefuseMessageTimer = REFUSE_DISPLAY_SEC;

            // 2단계: MessageBus 미구현 → Debug.Log로 대체
            // TODO: 3단계 — MessageBus.Publish(new AIMessage { Type = MessageType.OrderRefused, ... })
            Debug.Log($"[VillagerFSM] 명령 거부. 이유: {reason}. AgentId={AgentId}");

            // 거부 후 수행할 대안 Goal 결정
            _brain.AlternativeGoalId = DetermineAlternativeGoal(reason);

            // 거부 메시지 발행 (2단계: 로그로 대체)
            AIMessage refuseMsg = new AIMessage
            {
                Type      = MessageType.OrderRefused,
                Priority  = MessagePriority.Medium,
                SenderId  = AgentId,
                Payload   = reason,
                IssuedAt  = Time.time
            };
            // TODO: MessageBus.Instance.Publish(refuseMsg);
            Debug.Log($"[VillagerFSM] OrderRefused 메시지 발행 (더미). 이유코드={reason}. AgentId={AgentId}");
        }

        /// <summary>
        /// Dead 상태 진입 처리 (순서 엄수):
        /// 1. IsAlive = false
        /// 2. 플랜 실행 중지
        /// 3. 모든 자원 예약 해제
        /// 4. 드롭 아이템 생성
        /// 5. VillagerDied 메시지 발행
        /// 6. 주변 주민 loyalty/mood 패널티 기록
        /// 7. 5초 후 GameObject 비활성화
        /// </summary>
        private void EnterDead()
        {
            // 1. 생존 플래그 false 확정
            _brain.IsAlive         = false;
            // 2. 플랜 실행 중지
            _brain.IsExecutingPlan = false;
            _brain.CurrentPlan.Clear();

            // 3. 모든 자원 예약 해제 — 다른 주민이 이 자원을 즉시 사용할 수 있도록
            if (_registry != null)
            {
                _registry.ReleaseAll(AgentId);
            }
            else
            {
                Debug.LogWarning($"[VillagerFSM] EnterDead: _registry가 null이어서 ReleaseAll을 건너뜁니다. AgentId={AgentId}");
            }
            _hasActiveReservation = false;

            // 4. 인벤토리 아이템 드롭
            DropInventoryItems();

            // 5. VillagerDied 이벤트 발행
            // [PR Fix]: F-004 — MessageBus가 없으므로 2단계에서는 C# static event를 통해 발행한다.
            // GameManager나 UI 시스템이 VillagerFSM.OnVillagerDied를 Subscribe하여 처리한다.
            // 3단계에서 MessageBus 연결 시 이 이벤트를 래핑하거나 OnVillagerDied 구독자를 교체한다.
            // Debug.Log 더미는 디버깅 목적으로 유지한다.
            Debug.Log($"[VillagerFSM] VillagerDied 이벤트 발행. AgentId={AgentId}, Tile=({_brain.TileX},{_brain.TileY})");
            OnVillagerDied?.Invoke(AgentId, _brain.TileX, _brain.TileY);

            // 6. 주변 주민 loyalty/mood 패널티
            // 2단계: 저장만 수행, MessageBus 구현 후 전파
            // TODO: 3단계 — 주변 주민에게 mood -= 10, loyalty -= 5 전파
            Debug.Log($"[VillagerFSM] 주변 주민 loyalty/mood 패널티 기록 (2단계 더미). AgentId={AgentId}");

            // 7. 5초 후 GameObject 비활성화 (연출 시간 확보)
            // [PR Fix]: F-007 — StartCoroutine 반환값을 _deactivateCoroutine 필드에 저장한다.
            // 씬 전환이나 Destroy 시 OnDestroy()에서 StopCoroutine을 호출하여 고아 코루틴을 방지한다.
            _deactivateCoroutine = StartCoroutine(DeactivateAfterDelay(DEATH_DEACTIVATE_DELAY));
        }

        #endregion

        #region ── 유틸리티 메서드 ──

        /// <summary>
        /// P0(생존 위기) 조건이 현재 활성화되어 있는지 반환한다.
        /// Update()와 State_Replanning()에서 호출된다.
        /// </summary>
        private bool IsP0GoalActive()
        {
            if (_brain == null) return false;
            return _brain.HealthLevel  < 20f
                || _brain.HungerLevel  > 80f
                || _brain.FatigueLevel > 90f;
        }

        /// <summary>
        /// 현재 활성화된 P0 Goal ID를 반환한다.
        /// 우선순위: SurviveInjury > SurviveHunger > SurviveFatigue
        /// </summary>
        private string GetP0GoalId()
        {
            if (_brain.HealthLevel  < 20f) return "SurviveInjury";
            if (_brain.HungerLevel  > 80f) return "SurviveHunger";
            if (_brain.FatigueLevel > 90f) return "SurviveFatigue";
            return null;
        }

        /// <summary>
        /// 어떤 자원이든 30 미만으로 부족한지 확인한다.
        /// 실제 확인은 WorldState의 가용량 기준 (예약 포함).
        /// </summary>
        private bool IsAnyStockLow()
        {
            if (_worldState == null || _registry == null) return false;

            // 주요 자원(Food, Wood, Stone)만 부족 감지 — 희귀 자원은 별도 Goal로 처리
            return _registry.GetAvailable(ResourceType.RawFood)    < GATHER_STOCK_LOW_THRESHOLD
                || _registry.GetAvailable(ResourceType.CookedFood) < GATHER_STOCK_LOW_THRESHOLD
                || _registry.GetAvailable(ResourceType.Wood)       < GATHER_STOCK_LOW_THRESHOLD
                || _registry.GetAvailable(ResourceType.Stone)      < GATHER_STOCK_LOW_THRESHOLD;
        }

        /// <summary>
        /// 모든 주요 자원이 threshold 이상인지 확인한다.
        /// 탐험 Goal 발동 조건에 사용한다.
        /// </summary>
        private bool AreAllStocksAboveThreshold(float threshold)
        {
            if (_worldState == null || _registry == null) return false;

            return _registry.GetAvailable(ResourceType.RawFood)    >= threshold
                && _registry.GetAvailable(ResourceType.CookedFood) >= threshold
                && _registry.GetAvailable(ResourceType.Wood)       >= threshold
                && _registry.GetAvailable(ResourceType.Stone)      >= threshold;
        }

        /// <summary>
        /// 건물 건설에 필요한 자원이 충족되어 있는지 확인한다.
        /// 2단계: 더미 체크 (Wood >= 10, Stone >= 5).
        /// TODO: 3단계 — BuildingTypeId별 실제 자원 요구량 테이블로 교체
        /// </summary>
        private bool HasResourcesForBuilding()
        {
            if (_registry == null) return false;
            // TODO: 기획팀 — 건물별 자원 요구량 테이블 필요
            return _registry.GetAvailable(ResourceType.Wood)  >= 10f
                && _registry.GetAvailable(ResourceType.Stone) >= 5f;
        }

        /// <summary>
        /// 기지까지의 타일 거리를 맨해튼 거리로 계산한다.
        /// LOD 진입/탈출 조건에 사용한다.
        /// TODO: 3단계 — 실제 Pathfinding 거리로 교체 (맨해튼은 장애물을 고려하지 않음)
        /// </summary>
        private float GetDistanceToBase()
        {
            return Mathf.Abs(_brain.TileX - _baseTileX) + Mathf.Abs(_brain.TileY - _baseTileY);
        }

        /// <summary>
        /// Action ID를 기반으로 자원 예약을 시도한다.
        /// 2단계: 간소화된 더미 예약 (Action별 정확한 소비량은 3단계에서 정의).
        /// </summary>
        /// <returns>예약 성공 또는 예약 불필요(true), 예약 실패(false)</returns>
        private bool TryReserveForAction(string actionId)
        {
            if (_registry == null)
            {
                Debug.LogWarning($"[VillagerFSM] TryReserveForAction: _registry가 null입니다. AgentId={AgentId}");
                // Registry 없으면 예약 없이 진행 (더미 모드)
                return true;
            }

            // 2단계 더미 예약 로직
            // TODO: 3단계 — ActionDatabase에서 actionId별 ResourceCost 테이블 조회
            ResourceType reserveType;
            float        reserveAmount;

            switch (actionId)
            {
                case "EatCookedFood":
                    reserveType   = ResourceType.CookedFood;
                    reserveAmount = 1f;
                    break;
                case "EatRawFood":
                    reserveType   = ResourceType.RawFood;
                    reserveAmount = 1f;
                    break;
                case "CookMeal":
                    // 요리: RawFood 3 소비 → CookedFood 2 생성 (소비만 예약)
                    reserveType   = ResourceType.RawFood;
                    reserveAmount = 3f;
                    break;
                case "BuildTownHall":
                    reserveType   = ResourceType.Wood;
                    reserveAmount = 10f;
                    break;
                default:
                    // 자원 소비 없는 Action (이동, 수집 자체, 전투 등)
                    return true;
            }

            bool success = _registry.Reserve(AgentId, reserveType, reserveAmount);
            if (success)
            {
                _reservedResourceType  = reserveType;
                _reservedAmount        = reserveAmount;
                _hasActiveReservation  = true;
            }

            return success;
        }

        /// <summary>
        /// Action 완료 시 자원 Commit과 Brain 효과(Effect) 적용을 수행한다.
        /// </summary>
        private void OnActionCompleted(string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return;

            // 자원 Commit (예약된 자원을 실제로 차감)
            if (_hasActiveReservation && _registry != null)
            {
                bool committed = _registry.Commit(AgentId, _reservedResourceType, _reservedAmount);
                if (!committed)
                {
                    Debug.LogWarning($"[VillagerFSM] Action '{actionId}' Commit 실패. AgentId={AgentId}");
                }
                _hasActiveReservation = false;
            }

            // Brain에 Action 효과 적용
            ApplyActionEffect(actionId);

            Debug.Log($"[VillagerFSM] Action '{actionId}' 완료. AgentId={AgentId}");
        }

        /// <summary>
        /// Action 완료 시 Brain의 수치에 효과를 적용한다.
        /// 2단계: 더미 효과값 사용. 3단계에서 ActionDatabase로 교체.
        /// TODO: 기획팀 — 각 Action의 정확한 수치 효과 확인 필요
        /// </summary>
        private void ApplyActionEffect(string actionId)
        {
            switch (actionId)
            {
                case "EatCookedFood":
                    _brain.HungerLevel  = Mathf.Max(0f, _brain.HungerLevel  - 40f);
                    _brain.MoodLevel    = Mathf.Min(100f, _brain.MoodLevel   + 5f);
                    break;
                case "EatRawFood":
                    // 생 식량은 효율이 낮음 (기획서: CookedFood의 절반 효과)
                    _brain.HungerLevel  = Mathf.Max(0f, _brain.HungerLevel  - 20f);
                    break;
                // [PR Fix]: F-005 — Sleep과 RestOnGround를 동일 case에서 처리하던 구조를 분리한다.
                // 기존 코드는 두 Action을 같은 case에 묶은 뒤 if(actionId == "RestOnGround")로 분기하여
                // -50 후 +20 = 순 -30 만 적용되는 dead code가 존재했다.
                // 각 Action의 회복량을 상수로 명시하고 단일 수식으로 정리한다.
                case "Sleep":
                    // 침대에서 수면: 충분한 회복 (기획서: 피로 90 감소)
                    _brain.FatigueLevel = Mathf.Max(0f, _brain.FatigueLevel - SLEEP_FATIGUE_RECOVERY);
                    break;
                case "RestOnGround":
                    // 땅에서 쉬기: 수면보다 회복 효율 낮음 (기획서: 피로 20 감소)
                    _brain.FatigueLevel = Mathf.Max(0f, _brain.FatigueLevel - REST_ON_GROUND_FATIGUE_RECOVERY);
                    break;
                case "SeekMedicalAid":
                    _brain.HealthLevel  = Mathf.Min(100f, _brain.HealthLevel + 40f);
                    break;
                case "ChopWood":
                    // 나무 수집: WorldState에 Wood 추가
                    if (_worldState != null)
                        _worldState.WoodStock += 5f; // TODO: 기획팀 — 수집량 확인
                    _brain.FatigueLevel = Mathf.Min(100f, _brain.FatigueLevel + 10f);
                    _brain.HasTool      = true; // 도끼를 사용했다는 전제
                    break;
                case "MineStone":
                    if (_worldState != null)
                        _worldState.StoneStock += 3f; // TODO: 기획팀 — 수집량 확인
                    _brain.FatigueLevel = Mathf.Min(100f, _brain.FatigueLevel + 15f);
                    break;
                case "CookMeal":
                    // 요리: RawFood 3 → CookedFood 2 (Commit은 RawFood 3을 처리, 여기서 CookedFood 추가)
                    if (_worldState != null)
                        _worldState.CookedFoodStock += 2f;
                    break;
                case "AttackEnemy":
                    _brain.FatigueLevel = Mathf.Min(100f, _brain.FatigueLevel + 20f);
                    break;
                case "CraftPrimitiveWeapon":
                    _brain.HasPrimitiveWeapon = true;
                    break;
                case "Explore":
                    _brain.FatigueLevel = Mathf.Min(100f, _brain.FatigueLevel + 5f);
                    break;
                // BuildTownHall, MoveToBase, 기타 Action은 효과 없음 (2단계 더미)
            }
        }

        /// <summary>
        /// 현재 활성화된 자원 예약을 해제한다.
        /// Replanning 진입 또는 비정상 Executing 탈출 시 호출한다.
        /// </summary>
        private void ReleaseCurrentReservation()
        {
            if (!_hasActiveReservation) return;

            if (_registry != null)
            {
                _registry.Release(AgentId, _reservedResourceType, _reservedAmount);
            }
            else
            {
                Debug.LogWarning($"[VillagerFSM] ReleaseCurrentReservation: _registry가 null입니다. AgentId={AgentId}");
            }

            _hasActiveReservation = false;
        }

        /// <summary>
        /// 사망 시 인벤토리에 있는 아이템을 AuthoritativeWorldState.DroppedItems에 추가한다.
        /// </summary>
        private void DropInventoryItems()
        {
            if (_worldState == null)
            {
                Debug.LogWarning($"[VillagerFSM] DropInventoryItems: _worldState가 null입니다. AgentId={AgentId}");
                return;
            }

            // 도구 드롭
            if (_brain.HasTool)
            {
                var droppedTool = new DroppedItem
                {
                    ItemId                  = Guid.NewGuid().ToString(),
                    ItemType                = DetermineToolType(_brain.Role),
                    TileX                   = _brain.TileX,
                    TileY                   = _brain.TileY,
                    OriginalOwnerVillagerId = AgentId
                };
                _worldState.DroppedItems.Add(droppedTool);
                Debug.Log($"[VillagerFSM] 도구 드롭: {droppedTool.ItemType} at ({_brain.TileX},{_brain.TileY}). AgentId={AgentId}");
            }

            // 무기 드롭
            if (_brain.HasWeapon)
            {
                var droppedWeapon = new DroppedItem
                {
                    ItemId                  = Guid.NewGuid().ToString(),
                    ItemType                = ItemType.Weapon,
                    TileX                   = _brain.TileX,
                    TileY                   = _brain.TileY,
                    OriginalOwnerVillagerId = AgentId
                };
                _worldState.DroppedItems.Add(droppedWeapon);
            }

            // 원시 무기 드롭
            if (_brain.HasPrimitiveWeapon)
            {
                var droppedPrimWeapon = new DroppedItem
                {
                    ItemId                  = Guid.NewGuid().ToString(),
                    ItemType                = ItemType.PrimitiveWeapon,
                    TileX                   = _brain.TileX,
                    TileY                   = _brain.TileY,
                    OriginalOwnerVillagerId = AgentId
                };
                _worldState.DroppedItems.Add(droppedPrimWeapon);
            }

            // 식량 드롭
            if (_brain.HasFood)
            {
                var droppedFood = new DroppedItem
                {
                    ItemId                  = Guid.NewGuid().ToString(),
                    ItemType                = ItemType.Food,
                    TileX                   = _brain.TileX,
                    TileY                   = _brain.TileY,
                    OriginalOwnerVillagerId = AgentId
                };
                _worldState.DroppedItems.Add(droppedFood);
            }
        }

        /// <summary>역할에 따른 도구 아이템 타입을 반환한다.</summary>
        private ItemType DetermineToolType(AgentRole role)
        {
            switch (role)
            {
                case AgentRole.Lumberjack: return ItemType.Axe;
                case AgentRole.Miner:      return ItemType.Pickaxe;
                default:                   return ItemType.Axe; // 폴백: 도끼
            }
        }

        /// <summary>
        /// LOD 모드에서 기지 도착 시 수집한 자원을 WorldState에 더미 추가한다.
        /// 역할에 따라 다른 자원 타입을 추가한다.
        /// </summary>
        private void ApplyLODResourceGain()
        {
            if (_worldState == null) return;

            switch (_brain.Role)
            {
                case AgentRole.Lumberjack:
                    _worldState.WoodStock  += LOD_RESOURCE_STOCK_ADD;
                    break;
                case AgentRole.Miner:
                    _worldState.StoneStock += LOD_RESOURCE_STOCK_ADD;
                    break;
                case AgentRole.Cook:
                    _worldState.CookedFoodStock += LOD_RESOURCE_STOCK_ADD * 0.5f;
                    break;
                default:
                    // 역할 미정이면 기본 자원(RawFood) 추가
                    _worldState.RawFoodStock += LOD_RESOURCE_STOCK_ADD;
                    break;
            }
        }

        /// <summary>
        /// Deadlock 상태 처리: needsHelp 플래그 설정 및 강제 Fallback Goal 실행.
        /// fallbackCounter >= DEADLOCK_THRESHOLD일 때 Replanning 핸들러에서 호출된다.
        /// </summary>
        private void HandleDeadlock()
        {
            _brain.NeedsHelp = true;

            // Fallback Goal 결정: 배고프면 RestOnGround, 그 외 MoveToBase
            string fallbackGoal = _brain.HungerLevel < 50f ? "RestOnGround" : "MoveToBase";

            Debug.LogWarning($"[VillagerFSM] Deadlock 감지! FallbackCounter={_brain.FallbackCounter}. " +
                             $"NeedsHelp=true. FallbackGoal={fallbackGoal}. AgentId={AgentId}");

            // FallbackCounter 초기화 (무한 루프 방지)
            _brain.FallbackCounter  = 0;
            _brain.CurrentGoalId    = fallbackGoal;
            _brain.ReplanCooldown   = 0f;

            TransitionTo(VillagerState.Planning);
        }

        /// <summary>
        /// PlayerOrder의 OrderType을 GOAP Goal ID로 변환한다.
        /// CommandConflict에서 명령 수락 시 사용한다.
        /// </summary>
        private string ConvertOrderToGoalId(OrderType orderType)
        {
            switch (orderType)
            {
                case OrderType.GatherWood:    return "GatherWood";
                case OrderType.GatherStone:   return "GatherStone";
                case OrderType.GatherIron:    return "GatherIron";
                case OrderType.GatherCopper:  return "GatherCopper";
                case OrderType.BuildStructure: return "BuildStructure";
                case OrderType.Attack:        return "AttackEnemy";
                case OrderType.Move:          return "MoveToTarget";
                case OrderType.Cook:          return "CookMeal";
                case OrderType.Explore:       return "Explore";
                default:
                    Debug.LogWarning($"[VillagerFSM] ConvertOrderToGoalId: 알 수 없는 OrderType '{orderType}'. 'Idle'을 반환합니다.");
                    return "Idle";
            }
        }

        /// <summary>
        /// 거부 이유 코드를 기반으로 대안 Goal ID를 결정한다.
        /// 거부 후 어떤 행동을 대신 수행할지 설정한다.
        /// </summary>
        private string DetermineAlternativeGoal(RefusalReasonCode reason)
        {
            switch (reason)
            {
                case RefusalReasonCode.REFUSE_HUNGER:  return "SurviveHunger";
                case RefusalReasonCode.REFUSE_INJURY:  return "SurviveInjury";
                case RefusalReasonCode.REFUSE_FATIGUE: return "SurviveFatigue";
                case RefusalReasonCode.REFUSE_DANGER:  return "DefendVillage";
                default:                               return null; // 대안 없으면 Idle로 복귀
            }
        }

        /// <summary>
        /// 수신된 AI 메시지 큐를 처리한다. Tick() 시작 시 호출된다.
        /// EnemyDetected → LOD_Alert 전이, OrderIssued → CommandConflict 전이 등을 처리한다.
        /// </summary>
        private void ProcessMessageQueue()
        {
            // [PR Fix]: F-006 — 큐 스냅샷(snapshot) 패턴 적용.
            // _messageQueue 필드는 readonly이므로 참조 교체가 불가능하다.
            // 대신 처리 시작 시점의 메시지 개수를 캡처(messageCountThisBatch)하여
            // 그 수만큼만 Dequeue한다.
            // 처리 중(예: TryExecuteOrder 내부)에 새 메시지가 추가되면 큐 뒤에 쌓이지만
            // 현재 for 루프에서는 처리하지 않고 다음 Tick에서 처리된다.
            // 이를 통해 큐가 무한히 자라나는 무한루프 위험을 방지한다.
            int messageCountThisBatch = _messageQueue.Count;

            for (int i = 0; i < messageCountThisBatch; i++)
            {
                if (_messageQueue.Count == 0) break; // 안전 장치: 예상치 못한 외부 Dequeue 대비

                AIMessage msg = _messageQueue.Dequeue();

                switch (msg.Type)
                {
                    case MessageType.EnemyDetected:
                        // LOD 모드에서 적 탐지 시 LOD_Alert로 전이
                        if (_brain.FSMState == VillagerState.LOD_FSM)
                        {
                            TransitionToLOD(LODState.LOD_Alert);
                        }
                        // 일반 모드에서는 NearEnemy 플래그 갱신으로 처리 (SensorSystem이 Brain을 직접 갱신)
                        break;

                    case MessageType.OrderIssued:
                        // 외부에서 메시지로 명령을 전달하는 경우 (TryExecuteOrder 경유 없이)
                        // payload가 PlayerOrder인지 확인
                        if (msg.Payload is PlayerOrder orderFromMsg)
                        {
                            TryExecuteOrder(orderFromMsg);
                        }
                        break;

                    case MessageType.ResourceDepleted:
                        // 현재 수집 중인 자원이 고갈되면 재플래닝
                        if (_brain.FSMState == VillagerState.Executing
                            && (_brain.CurrentActionId == "ChopWood"
                                || _brain.CurrentActionId == "MineStone"))
                        {
                            Debug.Log($"[VillagerFSM] 자원 고갈 메시지 수신. Replanning. AgentId={AgentId}");
                            TransitionTo(VillagerState.Replanning);
                        }
                        break;

                    case MessageType.VillagerDied:
                        // 동료 사망 → mood와 loyalty 소폭 감소
                        // 2단계: Brain에만 저장, 실제 전파는 3단계 MessageBus 구현 후
                        _brain.MoodLevel    = Mathf.Max(0f, _brain.MoodLevel    - 5f);
                        _brain.LoyaltyLevel = Mathf.Max(0f, _brain.LoyaltyLevel - 2f);
                        break;
                }
            }
        }

        /// <summary>
        /// Goal ID와 Brain 상태를 기반으로 더미 플랜 결과를 반환한다.
        /// 3단계에서 실제 GOAP Job System으로 교체될 예정.
        /// </summary>
        private GOAPPlanResult SimulatePlanResult(string goalId)
        {
            if (string.IsNullOrEmpty(goalId))
            {
                return new GOAPPlanResult
                {
                    AgentId    = AgentId,
                    Success    = false,
                    ResultType = PlanResultType.NoSolutionFound,
                    ActionSequence = null
                };
            }

            var actions = new System.Collections.Generic.List<string>();
            bool success = true;

            switch (goalId)
            {
                case "SurviveHunger":
                    // 조리된 식량 우선, 없으면 생 식량
                    if (_worldState != null && _worldState.CookedFoodStock >= 1f)
                        actions.Add("EatCookedFood");
                    else
                        actions.Add("EatRawFood");
                    break;

                case "SurviveInjury":
                    // 치료사 근처이면 의료 치료, 없으면 땅에서 휴식
                    if (_brain.NearHealer)
                        actions.Add("SeekMedicalAid");
                    else
                        actions.Add("RestOnGround");
                    break;

                case "SurviveFatigue":
                    // 침대 근처이면 수면, 없으면 땅에서 휴식
                    if (_brain.NearBed)
                        actions.Add("Sleep");
                    else
                        actions.Add("RestOnGround");
                    break;

                case "GatherResources":
                case "GatherWood":
                    // 역할에 따른 수집 Action
                    actions.Add(GetGatherActionByRole());
                    break;

                case "GatherStone":
                    actions.Add("MineStone");
                    break;

                case "GatherIron":
                    actions.Add("MineIron"); // TODO: 기획팀 — Iron 수집 Action 확인
                    break;

                case "GatherCopper":
                    actions.Add("MineCopper"); // TODO: 기획팀 — Copper 수집 Action 확인
                    break;

                case "DefendVillage":
                    // 무기 있으면 공격, 없으면 원시 무기 제작
                    if (_brain.HasWeapon || _brain.HasPrimitiveWeapon)
                        actions.Add("AttackEnemy");
                    else
                        actions.Add("CraftPrimitiveWeapon");
                    break;

                case "BuildStructure":
                case "BuildTownHall":
                    actions.Add("BuildTownHall"); // TODO: 기획팀 — 건물 타입별 분기 필요
                    break;

                case "Explore":
                    actions.Add("Explore");
                    break;

                case "CookMeal":
                    actions.Add("CookMeal");
                    break;

                case "AttackEnemy":
                    actions.Add("AttackEnemy");
                    break;

                case "MoveToBase":
                    actions.Add("MoveToBase");
                    break;

                case "RestOnGround":
                    actions.Add("RestOnGround");
                    break;

                case "MoveToTarget":
                    actions.Add("MoveToTarget");
                    break;

                default:
                    success = false;
                    break;
            }

            return new GOAPPlanResult
            {
                AgentId         = AgentId,
                Success         = success && actions.Count > 0,
                ResultType      = (success && actions.Count > 0)
                                    ? PlanResultType.Success
                                    : PlanResultType.NoSolutionFound,
                TotalEstimatedCost = actions.Count * _brain.GetLoyaltyCostModifier(),
                SearchDepth     = 1, // 2단계: 항상 깊이 1 (더미)
                ActionSequence  = success ? actions : null
            };
        }

        /// <summary>
        /// 주민 역할에 따라 기본 수집 Action ID를 반환한다.
        /// </summary>
        private string GetGatherActionByRole()
        {
            switch (_brain.Role)
            {
                case AgentRole.Lumberjack: return "ChopWood";
                case AgentRole.Miner:      return "MineStone";
                case AgentRole.Cook:       return "CookMeal";
                default:
                    // 역할 미정이면 가장 부족한 자원 수집
                    if (_registry != null
                        && _registry.GetAvailable(ResourceType.Wood) < _registry.GetAvailable(ResourceType.Stone))
                        return "ChopWood";
                    return "MineStone";
            }
        }

        #endregion

        #region ── 코루틴 및 OnDestroy ──

        /// <summary>
        /// 사망 연출 후 GameObject를 비활성화한다.
        /// _deactivateCoroutine 필드에 참조가 저장되므로 OnDestroy()에서 안전하게 중지할 수 있다.
        /// </summary>
        private IEnumerator DeactivateAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            // [PR Fix]: F-007 — 'this != null && gameObject != null' 조건 제거.
            // MonoBehaviour 코루틴은 GameObject가 Destroy되면 자동으로 정지된다.
            // 그러므로 이 코루틴이 yield 재개 지점에 도달했다면 this와 gameObject는
            // 항상 유효하다. 해당 조건은 도달 불가(dead code)였으며 혼란을 야기한다.
            gameObject.SetActive(false);
            Debug.Log($"[VillagerFSM] GameObject 비활성화 완료. AgentId={AgentId}");
        }

        /// <summary>
        /// Unity가 이 컴포넌트를 Destroy할 때 호출한다.
        /// [PR Fix]: F-007 — 진행 중인 DeactivateAfterDelay 코루틴을 명시적으로 중지하여
        /// 씬 전환이나 오브젝트 파괴 시 고아 코루틴(orphaned coroutine)이 발생하지 않도록 한다.
        /// </summary>
        private void OnDestroy()
        {
            if (_deactivateCoroutine != null)
            {
                StopCoroutine(_deactivateCoroutine);
                _deactivateCoroutine = null;
            }
        }

        #endregion

        #region ── Unity Editor 디버그 헬퍼 ──

#if UNITY_EDITOR
        /// <summary>
        /// 씬 뷰에서 주민의 현재 상태, 기지까지의 거리, LOD 경계를 시각화한다.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (_brain == null) return;

            // LOD 경계 원 (보라색)
            UnityEditor.Handles.color = new Color(0.5f, 0f, 1f, 0.3f);
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, LOD_DISTANCE_THRESHOLD);

            // 현재 상태 레이블
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2f,
                $"[{_brain.FSMState}]\nGoal: {_brain.CurrentGoalId ?? "none"}\n" +
                $"HP:{_brain.HealthLevel:F0} HG:{_brain.HungerLevel:F0} FT:{_brain.FatigueLevel:F0} LY:{_brain.LoyaltyLevel:F0}"
            );
        }
#endif

        #endregion
    }
}
