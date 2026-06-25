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
using System.Collections.Generic; // List<T> — VillagerDied 드롭 아이템 목록 구성
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
        // 4단계 이전 단일 자원 예약 필드 — ReleaseCurrentReservation 롤백 전용으로 남겨둠
        private ResourceType _reservedResourceType;
        private float        _reservedAmount;
        private bool         _hasActiveReservation = false;

        // ── 4단계 신규: 다중 자원 예약 추적 ─────────────────────────────────
        // TryReserveForAction()이 ActionDatabase를 조회하여 다중 자원을 예약한 경우
        // 이 배열에 소비한 ResourceCostEntry[] 를 캐싱한다.
        // ReleaseCurrentReservation()에서 항목별로 Registry.Release()를 호출한다.
        // OnActionCompleted()에서 Commit 시에도 이 배열을 기준으로 처리한다.
        private ResourceCostEntry[] _pendingResourceCosts;

        // ── 4단계 신규: ActionDatabase / BuildingQueue 의존성 ────────────────
        // GameManager.InjectDependencies() 호출 시 주입된다.
        // Update() 및 Tick() 내부에서는 절대 GetComponent/FindObjectOfType 호출 금지.
        private ActionDatabase _actionDatabase;
        private BuildingQueue  _buildingQueue;

        // ── 수신 메시지 내부 큐 (우선순위 정렬 SortedList — 3단계) ──────────────
        // MessageBus가 이 에이전트에게 전달한 메시지를 우선순위 순으로 처리한다.
        // key = (int)MessagePriority (0=High, 1=Medium, 2=Low) — 작은 값이 먼저 처리됨
        // 같은 우선순위 내에서는 ReceiveMessage() 호출 순서(FIFO)를 유지한다.
        // 2단계의 Queue<AIMessage>를 SortedList<int, List<AIMessage>>로 교체했다.
        private readonly SortedList<int, List<AIMessage>> _messageQueue
            = new SortedList<int, List<AIMessage>>
            {
                // 버킷을 미리 생성하여 ReceiveMessage()에서 null 체크를 생략한다.
                { (int)MessagePriority.High,   new List<AIMessage>() },
                { (int)MessagePriority.Medium, new List<AIMessage>() },
                { (int)MessagePriority.Low,    new List<AIMessage>() }
            };

        // [PR Fix]: F-007 — DeactivateAfterDelay 코루틴 참조를 필드에 저장하여
        // OnDestroy()에서 명시적으로 정지할 수 있도록 한다. 씬 전환이나 Destroy 시 고아 코루틴 방지.
        private Coroutine _deactivateCoroutine;

        // ── 3단계: static OnVillagerDied event 제거 완료 ─────────────────────────
        // 2단계의 'public static event Action<string, int, int> OnVillagerDied'는
        // 3단계에서 MessageBus.Publish()로 교체되었다.
        // 기존에 OnVillagerDied를 구독하던 GameManager는 아래와 같이 교체해야 한다:
        //
        //   [변경 전] VillagerFSM.OnVillagerDied += OnVillagerDiedHandler;
        //   [변경 후] MessageBus.Instance.Subscribe(MessageType.VillagerDied, OnVillagerDiedHandler);
        //             // 핸들러 시그니처: void OnVillagerDiedHandler(AIMessage msg)
        //             // 페이로드 접근: var payload = (MessageBus.VillagerDiedPayload)msg.Payload;
        //
        // ─────────────────────────────────────────────────────────────────────────

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
        /// 외부 시스템(MessageBus 구독 콜백)이 이 에이전트에 메시지를 전달한다.
        /// 메시지는 우선순위 버킷(_messageQueue)에 적재되며 다음 Tick()에서 처리된다.
        /// </summary>
        public void ReceiveMessage(AIMessage message)
        {
            int priorityKey = (int)message.Priority;

            // 버킷은 생성자에서 미리 초기화했으므로 키 존재 여부를 별도로 체크하지 않는다.
            // 예상치 못한 Priority 값(3 이상)에 대한 방어 처리
            if (!_messageQueue.ContainsKey(priorityKey))
            {
                UnityEngine.Debug.LogWarning(
                    $"[VillagerFSM] ReceiveMessage: 알 수 없는 Priority 키 {priorityKey}. " +
                    $"Low 버킷으로 강제 분류합니다. AgentId={AgentId}"
                );
                priorityKey = (int)MessagePriority.Low;
            }

            _messageQueue[priorityKey].Add(message);
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
        /// GameManager에서 ResourceRegistry, WorldState, ActionDatabase, BuildingQueue를 주입한다.
        /// Script Execution Order 문제로 Awake 시점에 싱글턴이 없을 수 있으므로
        /// 이 메서드를 GameManager.Start() 또는 GameManager.Awake() 마지막에 호출한다.
        ///
        /// 4단계 변경: actionDatabase, buildingQueue 파라미터 추가.
        ///   - actionDatabase: TryReserveForAction, ApplyActionEffect, SimulatePlanResult 교체용
        ///   - buildingQueue:  BuildStructure Goal의 다음 건물 조회용
        ///   두 파라미터는 null 허용 — null이면 기존 더미 로직으로 폴백한다.
        ///
        /// GameManager 호출 예시:
        ///   villagerFSM.InjectDependencies(registry, actionDatabase, buildingQueue);
        /// </summary>
        public void InjectDependencies(
            ResourceRegistry registry,
            ActionDatabase   actionDatabase = null,
            BuildingQueue    buildingQueue  = null)
        {
            if (registry == null)
            {
                Debug.LogError($"[VillagerFSM] InjectDependencies: registry가 null입니다. AgentId={AgentId}");
                return;
            }

            _registry       = registry;
            _worldState     = AuthoritativeWorldState.Instance;
            _actionDatabase = actionDatabase; // null이면 기존 더미 로직 사용 (폴백)
            _buildingQueue  = buildingQueue;  // null이면 BuildingQueue.Instance로 대체

            if (_worldState == null)
            {
                Debug.LogError($"[VillagerFSM] InjectDependencies: AuthoritativeWorldState.Instance가 여전히 null입니다. " +
                               $"GameManager가 먼저 초기화되었는지 확인하세요. AgentId={AgentId}");
            }

            if (_actionDatabase == null)
            {
                // 경고: 더미 플래닝 로직으로 폴백하지만 기능상 제한이 있음
                Debug.LogWarning($"[VillagerFSM] InjectDependencies: actionDatabase가 null입니다. " +
                                 $"SimulatePlanResult 더미 로직으로 폴백합니다. AgentId={AgentId}");
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
                // [PR Fix R-002]: HasResourcesForBuilding()의 하드코딩 수치(Wood>=10, Stone>=5)가
                // ActionDatabase 실제 건물 비용과 불일치하여 Planning→Replanning 루프를 유발한다.
                // ActionDatabase.CanBuildNextBuilding()을 통해 실제 비용 테이블로 간접 판단한다.
                if (_worldState != null && _worldState.BuildingQueued
                    && (_actionDatabase != null
                        ? _actionDatabase.CanBuildNextBuilding(_registry, _worldState)
                        : HasResourcesForBuilding()))
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
        /// MessageBus를 통해 OrderRefused 메시지를 발행하며, 타이머 종료 후 Idle로 복귀한다.
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

        /// <summary>RefusingOrder 상태 진입 초기화. 거부 이유 결정 및 MessageBus를 통해 메시지 발행.</summary>
        private void EnterRefusingOrder()
        {
            // ConflictScoreCalculator에서 현재 Brain 상태 기반으로 거부 이유 코드 결정
            RefusalReasonCode refusalCode = ConflictScoreCalculator.DetermineReason(_brain);
            _brain.RefuseMessageTimer = REFUSE_DISPLAY_SEC;

            // 거부 후 수행할 대안 Goal 결정 (먹기, 치료, 피하기 등)
            _brain.AlternativeGoalId = DetermineAlternativeGoal(refusalCode);

            // ── ConflictScoreData 재계산: 페이로드에 점수 정보를 포함시키기 위해 ──
            // HasPendingOrder는 CommandConflict 핸들러에서 이미 false로 설정되었다.
            // 여기서는 Brain에 저장된 마지막 PendingOrder로 점수를 재계산하기 어려우므로
            // ConflictScore와 Threshold를 Brain에서 직접 읽는 대신 0으로 기록한다.
            // TODO: 기획팀 — ConflictScoreData를 Brain에 캐싱하여 페이로드에 포함시킬지 확인 필요
            //              현재는 UI/디버그보다 이벤트 전달 자체가 우선이므로 0 처리 허용
            string refusalMessage = BuildRefusalMessage(refusalCode);

            // ── OrderRefused 페이로드 구성 ──────────────────────────────────────
            var refusedPayload = new MessageBus.OrderRefusedPayload
            {
                VillagerId        = AgentId,
                RefusalReasonCode = refusalCode,
                RefusalMessage    = refusalMessage,
                ConflictScore     = 0f,    // TODO: Brain에 마지막 ConflictScoreData를 캐싱하면 실값 전달 가능
                Threshold         = 0f,    // TODO: 동일
                AlternativeGoalId = _brain.AlternativeGoalId,
                LoyaltyLevel      = _brain.LoyaltyLevel
            };

            // ── MessageBus를 통해 OrderRefused 발행 ─────────────────────────────
            // UI 시스템이 이 메시지를 구독하여 화면에 거부 이유를 표시한다.
            // MessageBus가 null이면 씬 초기화 순서 문제 — 경고만 출력하고 계속 진행한다.
            if (MessageBus.Instance != null)
            {
                MessageBus.Instance.Publish(new AIMessage
                {
                    Type     = MessageType.OrderRefused,
                    Priority = MessagePriority.Low,
                    SenderId = AgentId,
                    Payload  = refusedPayload,
                    IssuedAt = Time.time
                });
            }
            else
            {
                UnityEngine.Debug.LogWarning(
                    $"[VillagerFSM] EnterRefusingOrder: MessageBus.Instance가 null입니다. " +
                    $"OrderRefused 메시지를 발행할 수 없습니다. AgentId={AgentId}"
                );
            }

            UnityEngine.Debug.Log(
                $"[VillagerFSM] 명령 거부. 이유={refusalCode}, 대안Goal={_brain.AlternativeGoalId ?? "없음"}. " +
                $"AgentId={AgentId}"
            );
        }

        /// <summary>
        /// RefusalReasonCode를 사람이 읽을 수 있는 한국어 메시지로 변환한다.
        /// UI 시스템이 OrderRefusedPayload.RefusalMessage를 화면에 직접 표시한다.
        /// TODO: 기획팀 — 로컬라이제이션 키 체계 도입 시 이 메서드를 LocalizationManager로 교체
        /// </summary>
        private string BuildRefusalMessage(RefusalReasonCode code)
        {
            switch (code)
            {
                case RefusalReasonCode.REFUSE_HUNGER:
                    return "너무 배가 고파서 움직일 수 없습니다. 먼저 식사가 필요합니다.";
                case RefusalReasonCode.REFUSE_INJURY:
                    return "부상이 심해서 명령을 수행할 수 없습니다. 치료가 먼저입니다.";
                case RefusalReasonCode.REFUSE_FATIGUE:
                    return "탈진 상태입니다. 쉬지 않으면 쓰러집니다.";
                case RefusalReasonCode.REFUSE_LOYALTY:
                    return "당신의 명령을 따를 이유를 모르겠습니다.";
                case RefusalReasonCode.REFUSE_DANGER:
                    return "적이 근처에 있습니다. 무기 없이 그 명령은 수행할 수 없습니다.";
                case RefusalReasonCode.REFUSE_NO_TOOL:
                    return "도구가 없어서 채집할 수 없습니다.";
                case RefusalReasonCode.REFUSE_INSUFFICIENT_RESOURCES:
                    return "자원이 부족하여 건설할 수 없습니다.";
                default:
                    return $"명령을 거부합니다. (코드: {code})";
            }
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

            // 5. VillagerDied 메시지 발행 (MessageBus 경유)
            // ──────────────────────────────────────────────────────────────────
            // 3단계: static OnVillagerDied event → MessageBus.Publish() 로 교체.
            // GameManager가 MessageBus.Subscribe(MessageType.VillagerDied, ...)로
            // 구독하여 사망 후처리(주변 주민 패널티, 드롭 아이템 스폰 등)를 수행한다.
            // ──────────────────────────────────────────────────────────────────

            // ── 드롭 아이템 목록 구성 ─────────────────────────────────────────
            // _brain 인벤토리 플래그를 읽어 실제 드롭된 아이템을 기록한다.
            // DropInventoryItems()가 이미 _worldState.DroppedItems에 추가했으므로
            // 여기서는 메시지 페이로드 전달용으로만 사용한다.

            // [PR Fix R-003]: 초기 용량을 4로 지정하여 내부 배열 재할당을 방지한다.
            // 드롭 가능한 아이템 종류는 HasTool, HasWeapon, HasPrimitiveWeapon, HasFood 4종으로 고정이다.
            // 용량 힌트를 주지 않으면 기본 용량(4)에서 Add 시 8 → 16 식으로 재할당이 발생할 수 있다.
            var droppedItemList = new List<MessageBus.DroppedItemInfo>(4);

            if (_brain.HasTool)
            {
                // 역할에 따라 도끼 또는 곡괭이
                ItemType toolType = DetermineToolType(_brain.Role);
                droppedItemList.Add(new MessageBus.DroppedItemInfo
                {
                    ItemType = toolType,
                    TileX    = _brain.TileX,
                    TileY    = _brain.TileY
                });
            }

            if (_brain.HasWeapon)
            {
                droppedItemList.Add(new MessageBus.DroppedItemInfo
                {
                    ItemType = ItemType.Weapon,
                    TileX    = _brain.TileX,
                    TileY    = _brain.TileY
                });
            }

            if (_brain.HasPrimitiveWeapon)
            {
                droppedItemList.Add(new MessageBus.DroppedItemInfo
                {
                    ItemType = ItemType.PrimitiveWeapon,
                    TileX    = _brain.TileX,
                    TileY    = _brain.TileY
                });
            }

            if (_brain.HasFood)
            {
                droppedItemList.Add(new MessageBus.DroppedItemInfo
                {
                    ItemType = ItemType.Food,
                    TileX    = _brain.TileX,
                    TileY    = _brain.TileY
                });
            }

            // ── VillagerDiedPayload 구성 ──────────────────────────────────────
            var diedPayload = new MessageBus.VillagerDiedPayload
            {
                VillagerId        = AgentId,
                DeathTileX        = _brain.TileX,
                DeathTileY        = _brain.TileY,
                DroppedItems      = droppedItemList.ToArray(),
                // NearbyVillagerIds: GameManager가 구독 콜백에서 공간 쿼리로 채워야 한다.
                // Publish 시점의 VillagerFSM은 다른 주민 목록을 알 수 없으므로 빈 배열로 초기화.
                NearbyVillagerIds = System.Array.Empty<string>()
            };

            // ── MessageBus로 VillagerDied 발행 ────────────────────────────────
            if (MessageBus.Instance != null)
            {
                MessageBus.Instance.Publish(new AIMessage
                {
                    Type     = MessageType.VillagerDied,
                    Priority = MessagePriority.High,
                    SenderId = AgentId,
                    Payload  = diedPayload,
                    IssuedAt = Time.time
                });

                UnityEngine.Debug.Log(
                    $"[VillagerFSM] VillagerDied 메시지 발행 완료. " +
                    $"AgentId={AgentId}, Tile=({_brain.TileX},{_brain.TileY}), " +
                    $"드롭아이템={droppedItemList.Count}개"
                );
            }
            else
            {
                // MessageBus가 초기화되지 않은 경우 경고 출력
                // 씬 전환 중이거나 초기화 순서 문제일 수 있다
                UnityEngine.Debug.LogWarning(
                    $"[VillagerFSM] EnterDead: MessageBus.Instance가 null입니다. " +
                    $"VillagerDied 메시지를 발행할 수 없습니다. AgentId={AgentId}"
                );
            }

            // 6. 주변 주민 loyalty/mood 패널티
            // GameManager가 VillagerDied 메시지를 구독하여 NearbyVillagerIds를 채우고
            // 각 주민에게 ReceiveMessage(VillagerDied)를 전달하면,
            // ProcessMessageQueue()의 VillagerDied 핸들러가 mood -= 5, loyalty -= 2를 적용한다.
            // (VillagerFSM.ProcessMessageQueue() 라인 참조)
            UnityEngine.Debug.Log(
                $"[VillagerFSM] 주변 주민 loyalty/mood 패널티는 GameManager의 VillagerDied 구독 콜백에서 처리됩니다. " +
                $"AgentId={AgentId}"
            );

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
        ///
        /// 4단계: ActionDatabase에서 actionId별 ResourceCosts 테이블을 조회하여
        ///        다중 자원 예약을 수행한다. 하나라도 실패하면 전체 롤백하고 false 반환.
        ///        ActionDatabase가 주입되지 않았으면 기존 더미 로직으로 폴백한다.
        ///
        /// 예약 성공 시 _pendingResourceCosts에 소비 목록을 캐싱하고
        /// _hasActiveReservation = true로 설정한다.
        /// </summary>
        /// <returns>예약 성공 또는 예약 불필요(true), 예약 실패(false)</returns>
        private bool TryReserveForAction(string actionId)
        {
            if (_registry == null)
            {
                Debug.LogWarning($"[VillagerFSM] TryReserveForAction: _registry가 null입니다. AgentId={AgentId}");
                return true; // Registry 없으면 예약 없이 진행 (더미 모드)
            }

            // ── 4단계: ActionDatabase 조회 ────────────────────────────────────
            if (_actionDatabase != null)
            {
                // ActionDatabase에 해당 Action 정의가 없거나 소비 자원이 없으면 예약 불필요
                if (!_actionDatabase.TryGetAction(actionId, out ActionDefinition def))
                {
                    // 등록되지 않은 Action은 자원 소비 없음으로 간주 (Explore, MoveToBase 등)
                    return true;
                }

                if (def.ResourceCosts == null || def.ResourceCosts.Length == 0)
                {
                    // 자원 소비 없는 Action (ChopWood, MineStone 등 수집 Action)
                    return true;
                }

                // ── 다중 자원 예약 시도 (전부 성공해야 진행) ─────────────────────
                // 예약 성공 목록을 로컬 리스트에 누적하다가 하나라도 실패 시 전체 롤백
                var successfulReservations = new System.Collections.Generic.List<(ResourceType, float)>(
                    def.ResourceCosts.Length);

                foreach (ResourceCostEntry cost in def.ResourceCosts)
                {
                    bool reserved = _registry.Reserve(AgentId, cost.ResourceType, cost.Amount);

                    if (reserved)
                    {
                        successfulReservations.Add((cost.ResourceType, cost.Amount));
                    }
                    else
                    {
                        // 하나라도 실패 → 이미 예약 성공한 자원 전부 롤백
                        foreach (var (rollbackType, rollbackAmount) in successfulReservations)
                        {
                            _registry.Release(AgentId, rollbackType, rollbackAmount);
                        }

                        Debug.Log($"[VillagerFSM] TryReserveForAction: '{actionId}' 다중 예약 실패 " +
                                  $"(실패 자원: {cost.ResourceType} {cost.Amount:F0}). 전체 롤백 완료. AgentId={AgentId}");
                        return false;
                    }
                }

                // 전체 예약 성공 — _pendingResourceCosts에 캐싱
                // [PR Fix R-005]: def.ResourceCosts를 직접 참조하면 에디터 핫 리로드 시
                // ScriptableObject가 재로드될 때 구 버전 배열을 가리키는 문제가 생긴다.
                // Clone()으로 독립적인 사본을 만들어 참조 안전성을 확보한다.
                _pendingResourceCosts = (ResourceCostEntry[])def.ResourceCosts.Clone();
                _hasActiveReservation = successfulReservations.Count > 0;

                return true;
            }

            // ── Fallback: ActionDatabase 없으면 기존 단일 자원 더미 로직 ──────
            // 2단계 더미 예약 로직 (ActionDatabase 주입 전 호환성 유지)
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
                    reserveAmount = 35f; // 기획서 수치: Wood 35
                    break;
                case "BuildForge":
                    reserveType   = ResourceType.Wood;
                    reserveAmount = 20f; // 기획서 수치: Wood 20 (다중 자원 중 대표값)
                    break;
                default:
                    // 자원 소비 없는 Action (이동, 수집 자체, 전투 등)
                    return true;
            }

            bool fallbackSuccess = _registry.Reserve(AgentId, reserveType, reserveAmount);
            if (fallbackSuccess)
            {
                // 단일 자원 더미 예약 — 기존 필드에 저장
                _reservedResourceType = reserveType;
                _reservedAmount       = reserveAmount;
                _hasActiveReservation = true;
                // _pendingResourceCosts는 null 유지 (Commit/Release에서 단일 필드 사용)
                _pendingResourceCosts = null;
            }

            return fallbackSuccess;
        }

        /// <summary>
        /// Action 완료 시 자원 Commit과 Brain 효과(Effect) 적용을 수행한다.
        ///
        /// 4단계 변경: _pendingResourceCosts가 있으면(ActionDatabase 경로) 다중 Commit 수행.
        ///             없으면(더미 폴백 경로) 기존 단일 Commit 수행.
        /// </summary>
        private void OnActionCompleted(string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return;

            // ── 자원 Commit: 예약된 자원을 실제로 차감 ───────────────────────
            if (_hasActiveReservation && _registry != null)
            {
                if (_pendingResourceCosts != null && _pendingResourceCosts.Length > 0)
                {
                    // 4단계: ActionDatabase 경로 — 다중 자원 Commit
                    foreach (ResourceCostEntry cost in _pendingResourceCosts)
                    {
                        bool committed = _registry.Commit(AgentId, cost.ResourceType, cost.Amount);
                        if (!committed)
                        {
                            Debug.LogWarning($"[VillagerFSM] Action '{actionId}' 다중 Commit 실패. " +
                                             $"자원: {cost.ResourceType} {cost.Amount:F0}. AgentId={AgentId}");
                        }
                    }
                    _pendingResourceCosts = null;
                }
                else
                {
                    // 더미 폴백 경로 — 단일 자원 Commit
                    bool committed = _registry.Commit(AgentId, _reservedResourceType, _reservedAmount);
                    if (!committed)
                    {
                        Debug.LogWarning($"[VillagerFSM] Action '{actionId}' Commit 실패. AgentId={AgentId}");
                    }
                }

                _hasActiveReservation = false;
            }

            // Brain에 Action 효과 적용
            ApplyActionEffect(actionId);

            Debug.Log($"[VillagerFSM] Action '{actionId}' 완료. AgentId={AgentId}");
        }

        /// <summary>
        /// Action 완료 시 Brain의 수치 또는 WorldState 플래그에 효과를 적용한다.
        ///
        /// 4단계: ActionDatabase가 주입되어 있으면 ActionDefinition.Effects[] 배열을 순회하여
        ///        ActionEffectType별로 처리한다.
        ///        ActionDatabase가 없으면 기존 더미 switch 로직으로 폴백한다.
        ///
        /// 참고: ConsumeResource 효과는 OnActionCompleted()의 Commit 단계에서 이미 처리되므로
        ///       여기서는 Brain 수치 변경과 WorldState 플래그 갱신만 수행한다.
        /// </summary>
        private void ApplyActionEffect(string actionId)
        {
            // ── 4단계: ActionDatabase 경로 ────────────────────────────────────
            if (_actionDatabase != null)
            {
                if (!_actionDatabase.TryGetAction(actionId, out ActionDefinition def))
                {
                    // 등록되지 않은 Action은 효과 없음으로 처리
                    return;
                }

                if (def.Effects == null || def.Effects.Length == 0) return;

                foreach (ActionEffect effect in def.Effects)
                {
                    // ── 효과 타입별 처리 ──────────────────────────────────────
                    switch (effect.EffectType)
                    {
                        // 자원 획득: WorldState 재고 증가
                        case ActionEffectType.GainResource:
                            if (_worldState != null)
                            {
                                float current = _worldState.GetStock(effect.ResourceType);
                                _worldState.SetStock(effect.ResourceType, current + effect.Amount);
                            }
                            break;

                        // 자원 소비: Commit 단계에서 처리됨 — 여기서는 건너뜀
                        case ActionEffectType.ConsumeResource:
                            // OnActionCompleted()의 Registry.Commit()이 이미 처리함
                            break;

                        // Brain 수치: 배고픔 감소 (하한 0 클램프)
                        case ActionEffectType.ReduceHunger:
                            _brain.HungerLevel = Mathf.Max(0f, _brain.HungerLevel - effect.Amount);
                            break;

                        // Brain 수치: 피로 감소 — 땅에서 쉬기 (20 감소)
                        case ActionEffectType.ReduceFatigue:
                            _brain.FatigueLevel = Mathf.Max(0f, _brain.FatigueLevel - effect.Amount);
                            break;

                        // Brain 수치: 피로 회복 — 수면 (90 감소)
                        // ReduceFatigue와 수식은 같지만 GameDesign상 의미가 다르므로 분리
                        case ActionEffectType.RestoreFatigue:
                            _brain.FatigueLevel = Mathf.Max(0f, _brain.FatigueLevel - effect.Amount);
                            break;

                        // Brain 수치: 체력 회복 (상한 100 클램프)
                        case ActionEffectType.GainHealth:
                            _brain.HealthLevel = Mathf.Min(100f, _brain.HealthLevel + effect.Amount);
                            break;

                        // Brain 수치: 피로 증가 (수집/전투 부작용, 상한 100 클램프)
                        case ActionEffectType.IncreaseFatigue:
                            _brain.FatigueLevel = Mathf.Min(100f, _brain.FatigueLevel + effect.Amount);
                            break;

                        // Brain 수치: 기분 향상
                        case ActionEffectType.GainMood:
                            _brain.MoodLevel = Mathf.Min(100f, _brain.MoodLevel + effect.Amount);
                            break;

                        // Brain 위치 플래그: 기지 도착 완료
                        case ActionEffectType.SetAtBase:
                            _brain.AtBase = true;
                            break;

                        // Brain 환경 플래그: 모닥불 근처 (건설 완료 후)
                        case ActionEffectType.SetNearFireplace:
                            _brain.NearFireplace = true;
                            break;

                        // WorldState 건물 완료 플래그
                        case ActionEffectType.SetCampfireBuilt:
                            // 모닥불 완료: NearFireplace는 SensorSystem이 갱신
                            _brain.NearFireplace = true;
                            break;

                        case ActionEffectType.SetHouseBuilt:
                            // WorldState에 HouseBuilt 필드 없음 — 미래 확장 시 추가
                            Debug.Log("[VillagerFSM] ApplyActionEffect: SetHouseBuilt — 미래 구현 예정.");
                            break;

                        case ActionEffectType.SetStorehouseBuilt:
                            if (_worldState != null) _worldState.StorehouseBuilt = true;
                            break;

                        case ActionEffectType.SetTownHallBuilt:
                            if (_worldState != null) _worldState.TownHallBuilt = true;
                            break;

                        case ActionEffectType.SetForgeBuilt:
                            if (_worldState != null) _worldState.ForgeBuilt = true;
                            break;

                        case ActionEffectType.SetWatchtowerBuilt:
                            // WorldState에 WatchtowerBuilt 필드 없음 — 미래 확장 시 추가
                            Debug.Log("[VillagerFSM] ApplyActionEffect: SetWatchtowerBuilt — 미래 구현 예정.");
                            break;

                        // Brain 인벤토리 플래그
                        case ActionEffectType.SetHasTool:
                            _brain.HasTool = true;
                            break;

                        case ActionEffectType.SetHasPrimitiveWeapon:
                            _brain.HasPrimitiveWeapon = true;
                            break;

                        case ActionEffectType.SetHasWeapon:
                            _brain.HasWeapon = true;
                            break;

                        // 탐험 효과: FoW isDiscovered 갱신 (미래 TileMap 시스템에서 구현)
                        case ActionEffectType.DiscoverNearby:
                            // TODO: TileMap/FoW 시스템 구현 후 주변 타일 isDiscovered = true 처리
                            Debug.Log("[VillagerFSM] ApplyActionEffect: DiscoverNearby — FoW 시스템 연동 예정.");
                            break;

                        default:
                            Debug.LogWarning($"[VillagerFSM] ApplyActionEffect: 처리되지 않은 ActionEffectType " +
                                             $"'{effect.EffectType}'. Action='{actionId}'. AgentId={AgentId}");
                            break;
                    }
                }

                return; // ActionDatabase 경로 처리 완료
            }

            // ── Fallback: ActionDatabase 없으면 기존 더미 switch 로직 ──────────
            // 2단계 하드코딩 효과 — ActionDatabase 주입 전 호환성 유지
            switch (actionId)
            {
                case "EatCookedFood":
                    _brain.HungerLevel = Mathf.Max(0f, _brain.HungerLevel  - 50f); // 기획서 수치
                    _brain.MoodLevel   = Mathf.Min(100f, _brain.MoodLevel   + 5f);
                    break;
                case "EatRawFood":
                    _brain.HungerLevel = Mathf.Max(0f, _brain.HungerLevel   - 15f); // 기획서 수치
                    break;
                case "Sleep":
                    _brain.FatigueLevel = Mathf.Max(0f, _brain.FatigueLevel - SLEEP_FATIGUE_RECOVERY);
                    break;
                case "RestOnGround":
                    _brain.FatigueLevel = Mathf.Max(0f, _brain.FatigueLevel - REST_ON_GROUND_FATIGUE_RECOVERY);
                    break;
                case "SeekMedicalAid":
                    _brain.HealthLevel  = Mathf.Min(100f, _brain.HealthLevel + 40f);
                    break;
                case "ChopWood":
                    if (_worldState != null) _worldState.WoodStock  += 10f; // 기획서 수치: wood += 10
                    _brain.FatigueLevel = Mathf.Min(100f, _brain.FatigueLevel + 10f);
                    break;
                case "MineStone":
                    if (_worldState != null) _worldState.StoneStock += 8f;  // 기획서 수치: stone += 8
                    _brain.FatigueLevel = Mathf.Min(100f, _brain.FatigueLevel + 15f);
                    break;
                case "MineIron":
                    if (_worldState != null) _worldState.IronStock  += 5f;  // 기획서 수치: iron += 5
                    _brain.FatigueLevel = Mathf.Min(100f, _brain.FatigueLevel + 15f);
                    break;
                case "CookMeal":
                    if (_worldState != null) _worldState.CookedFoodStock += 2f; // 기획서 수치: cookedFood += 2
                    break;
                case "AttackEnemy":
                    _brain.FatigueLevel = Mathf.Min(100f, _brain.FatigueLevel + 20f);
                    break;
                case "CraftPrimitiveWeapon":
                case "CraftWeapon":
                    _brain.HasWeapon = true;
                    break;
                case "Explore":
                    _brain.FatigueLevel = Mathf.Min(100f, _brain.FatigueLevel + 5f); // 기획서 수치: fatigue += 5
                    break;
                case "SetTownHallBuilt":
                case "BuildTownHall":
                    if (_worldState != null) _worldState.TownHallBuilt = true;
                    break;
                case "BuildForge":
                    if (_worldState != null) _worldState.ForgeBuilt = true;
                    break;
                case "BuildStorehouse":
                    if (_worldState != null) _worldState.StorehouseBuilt = true;
                    break;
                // MoveToBase, MoveToTarget, FleeFromEnemy, AlertVillage 등: 더미에서 효과 없음
            }
        }

        /// <summary>
        /// 현재 활성화된 자원 예약을 해제한다.
        /// Replanning 진입 또는 비정상 Executing 탈출 시 호출한다.
        ///
        /// 4단계 변경: _pendingResourceCosts가 있으면(ActionDatabase 경로) 다중 Release 수행.
        ///             없으면(더미 폴백 경로) 기존 단일 Release 수행.
        /// </summary>
        private void ReleaseCurrentReservation()
        {
            if (!_hasActiveReservation) return;

            if (_registry == null)
            {
                Debug.LogWarning($"[VillagerFSM] ReleaseCurrentReservation: _registry가 null입니다. AgentId={AgentId}");
                _hasActiveReservation = false;
                _pendingResourceCosts = null;
                return;
            }

            if (_pendingResourceCosts != null && _pendingResourceCosts.Length > 0)
            {
                // 4단계: ActionDatabase 경로 — 다중 자원 해제
                foreach (ResourceCostEntry cost in _pendingResourceCosts)
                {
                    _registry.Release(AgentId, cost.ResourceType, cost.Amount);
                }
                _pendingResourceCosts = null;
            }
            else
            {
                // 더미 폴백 경로 — 단일 자원 해제
                _registry.Release(AgentId, _reservedResourceType, _reservedAmount);
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

            // [PR Fix R-007]: 기존 폴백 로직(HungerLevel < 50 → RestOnGround)은 P0 생존 위기가
            // 이미 실패한 Deadlock 상황에서 부적절하다. 허기가 낮다고 RestOnGround로 가면
            // 오히려 Goal을 잃고 표류할 위험이 크다.
            // 기본 폴백을 MoveToBase로 설정하고, P0 Goal이 활성 상태이면 해당 Goal을 우선 사용한다.
            // P0 Goal이 이미 실패하여 Deadlock에 빠진 경우에도 기지 복귀가 가장 안전한 행동이다.
            string fallbackGoal;

            if (IsP0GoalActive())
            {
                // P0 Goal이 활성이면 해당 Goal ID를 우선 사용 (SurviveInjury/SurviveHunger/SurviveFatigue)
                fallbackGoal = GetP0GoalId() ?? "MoveToBase";
            }
            else
            {
                // P0 Goal이 없는 일반 Deadlock: 기지 복귀를 기본 폴백으로 설정
                fallbackGoal = "MoveToBase";
            }

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
        /// 수신된 AI 메시지 큐를 우선순위 순으로 처리한다. Tick() 시작 시 호출된다.
        /// High(0) 버킷부터 순서대로 처리하며, 각 버킷 내부는 FIFO 순서를 따른다.
        ///
        /// 안전 처리:
        ///   - 처리 시작 시점의 메시지 개수만큼만 처리한다 (배치 스냅샷 패턴).
        ///   - 처리 중 ReceiveMessage()로 새 메시지가 추가되면 다음 Tick에서 처리된다.
        ///   - 이를 통해 무한 루프(처리 → 새 메시지 추가 → 처리 → ...)를 방지한다.
        /// </summary>
        private void ProcessMessageQueue()
        {
            // ── 배치 스냅샷: 각 버킷의 현재 개수를 미리 캡처 ───────────────────
            // SortedList는 key 오름차순(0=High → 1=Medium → 2=Low)으로 순회된다.
            foreach (KeyValuePair<int, List<AIMessage>> bucket in _messageQueue)
            {
                List<AIMessage> messages = bucket.Value;
                if (messages.Count == 0) continue;

                // 처리 시작 시점의 메시지 수만큼만 처리 (배치 스냅샷 패턴)
                // 처리 중 추가된 메시지는 messages.Count가 증가하지만 for 조건에 포함되지 않는다.
                int countThisBatch = messages.Count;

                for (int i = 0; i < countThisBatch; i++)
                {
                    AIMessage msg = messages[i];
                    HandleMessage(msg);
                }

                // 이번 배치에서 처리한 메시지를 제거한다.
                // countThisBatch 이후에 추가된 메시지는 남긴다.
                if (messages.Count == countThisBatch)
                {
                    // 처리 중 추가된 메시지가 없는 일반 케이스: 전체 Clear로 빠르게 처리
                    messages.Clear();
                }
                else
                {
                    // 처리 중 ReceiveMessage()로 새 메시지가 추가된 케이스:
                    // 앞의 countThisBatch 개만 제거하고 뒤에 추가된 메시지는 남긴다.
                    messages.RemoveRange(0, countThisBatch);
                }
            }
        }

        /// <summary>
        /// 단일 AIMessage를 처리하여 FSM 상태 전이 또는 Brain 수치 갱신을 수행한다.
        /// ProcessMessageQueue()의 내부 루프에서 호출된다.
        /// </summary>
        private void HandleMessage(AIMessage msg)
        {
            switch (msg.Type)
            {
                case MessageType.EnemyDetected:
                    // LOD 모드에서 적 탐지 시 즉시 LOD_Alert로 전이하여 Full GOAP 복귀 준비
                    if (_brain.FSMState == VillagerState.LOD_FSM)
                    {
                        TransitionToLOD(LODState.LOD_Alert);
                    }
                    // 일반 모드에서는 SensorSystem이 Brain.NearEnemy 플래그를 직접 갱신한다.
                    // Update()의 AnyState P0 체크가 즉시 반응하므로 여기서는 별도 전이 불필요.
                    break;

                case MessageType.OrderIssued:
                    // MessageBus를 통해 외부에서 명령이 도착한 경우 (TryExecuteOrder 경유 없이)
                    // Payload가 OrderIssuedPayload인지 확인 후 PlayerOrder로 변환하여 처리
                    if (msg.Payload is MessageBus.OrderIssuedPayload orderPayload
                        && orderPayload.TargetVillagerId == AgentId)
                    {
                        TryExecuteOrder(new PlayerOrder
                        {
                            TargetVillagerId = orderPayload.TargetVillagerId,
                            OrderType        = orderPayload.OrderType,
                            TargetTileX      = orderPayload.TargetTileX,
                            TargetTileY      = orderPayload.TargetTileY,
                            BuildingTypeId   = orderPayload.BuildingTypeId,
                            IssuedAt         = orderPayload.IssuedAt
                        });
                    }
                    break;

                case MessageType.ResourceDepleted:
                    // 현재 수집 중인 자원이 고갈되면 즉시 재플래닝
                    // ChopWood와 MineStone 외의 Action은 영향을 받지 않는다.
                    if (_brain.FSMState == VillagerState.Executing
                        && (_brain.CurrentActionId == "ChopWood"
                            || _brain.CurrentActionId == "MineStone"))
                    {
                        UnityEngine.Debug.Log(
                            $"[VillagerFSM] 자원 고갈 메시지 수신 → Replanning. " +
                            $"Action={_brain.CurrentActionId}, AgentId={AgentId}"
                        );
                        TransitionTo(VillagerState.Replanning);
                    }
                    break;

                case MessageType.VillagerDied:
                    // 동료 사망 소식 수신 → 심리적 충격: mood와 loyalty 소폭 감소
                    // 기획서 수치: mood -= 5, loyalty -= 2 (TODO: 기획팀 최종 수치 확인)
                    _brain.MoodLevel    = Mathf.Max(0f, _brain.MoodLevel    - 5f);
                    _brain.LoyaltyLevel = Mathf.Max(0f, _brain.LoyaltyLevel - 2f);
                    break;

                // RaidDecision, ResourceDiscovered, OrderRefused:
                // 현재 VillagerFSM이 직접 처리할 로직 없음 → 무시 (GameManager가 처리)
                default:
                    break;
            }
        }

        /// <summary>
        /// Goal ID와 Brain 상태를 기반으로 플랜 결과를 반환한다.
        ///
        /// 4단계 변경: ActionDatabase가 주입되어 있으면 GetDefaultActionSequence()에 위임한다.
        ///             ActionDatabase가 없으면 기존 더미 switch 로직으로 폴백한다.
        ///
        /// 5단계에서 실제 GOAP Job System으로 교체될 예정.
        /// TODO: 5단계 — WorldStateSnapshot + GOAPPlannerJob 스케줄링으로 교체
        /// </summary>
        private GOAPPlanResult SimulatePlanResult(string goalId)
        {
            if (string.IsNullOrEmpty(goalId))
            {
                return new GOAPPlanResult
                {
                    AgentId        = AgentId,
                    Success        = false,
                    ResultType     = PlanResultType.NoSolutionFound,
                    ActionSequence = null
                };
            }

            // ── 4단계: ActionDatabase 경로 ────────────────────────────────────
            // [PR Fix R-004]: 기존 '_actionDatabase != null && _worldState != null' 복합 조건에서
            // _worldState null 체크를 분리한다. 이전 코드에서는 worldState가 null이면
            // ActionDatabase가 정상 주입되어도 조용히 더미 폴백으로 떨어져 디버깅이 어려웠다.
            // 이제 _worldState가 null인 경우 명시적으로 LogError를 출력하고 별도 폴백 처리한다.
            if (_actionDatabase != null && _brain != null && _registry != null)
            {
                if (_worldState == null)
                {
                    // worldState만 없는 경우: ActionDatabase는 있지만 월드 상태를 읽을 수 없음.
                    // 이 상황은 InjectDependencies()가 올바르게 호출되지 않은 것이므로
                    // 조용한 폴백 대신 명시적 에러로 디버깅을 돕는다.
                    Debug.LogError($"[VillagerFSM] SimulatePlanResult: _worldState가 null입니다. " +
                                   $"ActionDatabase 경로를 사용할 수 없으므로 더미 폴백으로 전환합니다. " +
                                   $"GameManager.InjectDependencies() 호출 순서를 확인하세요. AgentId={AgentId}");
                    // 아래 더미 폴백으로 fall-through
                }
                else
                {
                    string[] sequence = _actionDatabase.GetDefaultActionSequence(
                        goalId,
                        _brain.Role,
                        _brain,
                        _worldState,
                        _registry);

                    bool planSuccess = sequence != null && sequence.Length > 0;

                    return new GOAPPlanResult
                    {
                        AgentId            = AgentId,
                        Success            = planSuccess,
                        ResultType         = planSuccess
                                                ? PlanResultType.Success
                                                : PlanResultType.NoSolutionFound,
                        ActionSequence     = planSuccess
                                                ? new System.Collections.Generic.List<string>(sequence)
                                                : new System.Collections.Generic.List<string>(),
                        SearchDepth        = sequence?.Length ?? 0,
                        TotalEstimatedCost = CalculateTotalCost(sequence, _brain.Role)
                    };
                }
            }

            // ── Fallback: ActionDatabase 없으면 기존 더미 switch 로직 ──────────
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
                    if (_brain.NearHealer)
                        actions.Add("SeekMedicalAid");
                    else
                        actions.Add("RestOnGround");
                    break;

                case "SurviveFatigue":
                    if (_brain.NearBed)
                        actions.Add("Sleep");
                    else
                        actions.Add("RestOnGround");
                    break;

                case "GatherResources":
                case "GatherWood":
                    actions.Add(GetGatherActionByRole());
                    break;

                case "GatherStone":
                    actions.Add("MineStone");
                    break;

                case "GatherIron":
                    actions.Add("MineIron");
                    break;

                case "GatherCopper":
                    actions.Add("MineCopper");
                    break;

                case "DefendVillage":
                    if (_brain.HasWeapon || _brain.HasPrimitiveWeapon)
                        actions.Add("AttackEnemy");
                    else
                        actions.Add("CraftPrimitiveWeapon");
                    break;

                case "BuildStructure":
                case "BuildTownHall":
                    actions.Add("BuildTownHall");
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
                AgentId            = AgentId,
                Success            = success && actions.Count > 0,
                ResultType         = (success && actions.Count > 0)
                                        ? PlanResultType.Success
                                        : PlanResultType.NoSolutionFound,
                TotalEstimatedCost = actions.Count * _brain.GetLoyaltyCostModifier(),
                SearchDepth        = 1, // 더미: 항상 깊이 1
                ActionSequence     = success ? actions : null
            };
        }

        /// <summary>
        /// Action 시퀀스의 총 예상 비용을 계산한다.
        /// 역할 보정이 적용된 GetCostForRole()을 Action별로 합산한다.
        /// </summary>
        /// <param name="sequence">Action ID 배열. null이면 0f 반환.</param>
        /// <param name="role">비용 보정 기준 역할.</param>
        /// <returns>시퀀스 전체 예상 비용.</returns>
        private float CalculateTotalCost(string[] sequence, AgentRole role)
        {
            if (sequence == null || sequence.Length == 0 || _actionDatabase == null) return 0f;

            float total = 0f;
            foreach (string actionId in sequence)
            {
                total += _actionDatabase.GetCostForRole(actionId, role);
            }

            return total;
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
