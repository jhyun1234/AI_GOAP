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
///   - JPSPathfinder.cs (12단계: JPS 경로 탐색)
///
/// 12단계 변경 (경로이동 시스템):
///   - Update()의 순간이동을 MoveTowards 기반 실제 이동으로 교체
///   - MoveTileForAction()에서 Brain.TileX/Y 직접 설정을 StartPathTo()로 교체
///   - State_Executing()에서 이동 완료 후 액션 타이머 시작으로 변경
///   - State_Fleeing()에서 VILLAGER_FLEE_SPEED 적용 (EnterFleeing에서 경로 시작)
///   - AbortCurrentPath() 헬퍼 추가 (전투/사망/재플래닝 시 경로 즉시 중단)
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
using AIVillage.Core.GOAP; // GOAPPlannerScheduler, GOAPPlannerJob

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
        // 주민 1명의 실효 틱 주기 = 0.1s × 6그룹 = 0.6s. 최초 폴링이 0.6s 후에 도달하므로
        // 타임아웃은 반드시 0.6s보다 충분히 커야 한다 (3.0s = 5 틱 여유).
        private const float PLANNING_TIMEOUT_SEC   = 3.0f;  // 이 시간 초과 시 Replanning
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

        // ── [C2 T14] P0 생존 Goal 발동 임계값 (기획서 원본, ADR-T5로 승격) ────
        // IsP0GoalActive/GetP0GoalId의 리터럴이었던 값을 클래스 상수로 승격한다.
        // T14 정합성 테스트가 이 상수와 BuildGoalState 목표치의 방향성을 검증한다.
        // - Injury: 발동 HealthLevel < 20, 목표 MyHealth GreaterEq 60 (역방향, 20 < 60 정상)
        // - Hunger: 발동 SatietyLevel < 20, 목표 MySatiety GreaterEq 70 (역방향, 19 < 70 정상)
        // - Fatigue: 발동 FatigueLevel > 90, 목표 MyFatigue LessEq 30 (역방향, 91 > 30 정상)
        internal const float P0_INJURY_TRIGGER_HEALTH   = 20f;
        internal const float P0_HUNGER_TRIGGER_SATIETY  = 20f; // SatietyLevel이 이 값 미만이면 P0 발동
        internal const float P0_FATIGUE_TRIGGER_FATIGUE = 90f;

        // 사망 후 GameObject 비활성화까지 대기 시간 (연출용)
        private const float DEATH_DEACTIVATE_DELAY = 5.0f;

        // ── [Phase 1 F1] 사고 말풍선 스로틀 상수 ────────────────────────────
        // 주민당 최소 발화 간격. 리플래닝 루프에 빠진 주민이 토스트 채널을 마비시키는 것을 방지한다.
        private const float THOUGHT_MIN_INTERVAL_SEC = 5.0f;

        // [PR Fix]: F-005 — RestOnGround와 Sleep의 피로 회복량을 명명된 상수로 분리.
        // 두 Action의 회복량이 달라 switch case를 분리하여 관리한다.
        private const float REST_ON_GROUND_FATIGUE_RECOVERY = GOAPActionRegistry.REST_FATIGUE_RELIEF;  // ADR-7: 단일 출처
        private const float SLEEP_FATIGUE_RECOVERY          = GOAPActionRegistry.SLEEP_FATIGUE_RELIEF; // ADR-7: 단일 출처

        // ── [8단계] 전투 / 정신 상태 수치 ────────────────────────────────────────
        private const float BASE_VILLAGER_DAMAGE     = 8f;   // 틱당 주민이 적에게 입히는 기본 피해
        private const float ATTACK_RANGE_TILES       = 2f;   // 공격 유효 타일 거리 (맨해튼)
        private const float CHASE_MAX_RANGE_TILES    = 8f;   // Fighting 중 공격 범위 밖 적을 추격할 최대 거리 (SensorSystem._enemyDetectionRadius와 일치)
        private const int   CHASE_REPATH_DRIFT_TILES = 3;    // 추격 중 적이 이만큼 이동하면 경로 재계산 (성능/반응성 균형)
        private const float FEAR_CONTAGION_RADIUS    = 5f;   // 아군 사망 목격 Fear 전염 반경
        private const float RAGE_CONTAGION_RADIUS    = 6f;   // Rage 처치 목격 전염 반경
        private const float RAGE_DURATION_SEC        = 8f;   // Rage 지속 시간(초)
        private const float RAGE_ATTACK_MODIFIER     = 1.5f; // Rage 공격 배율
        private const float FEAR_HEALTH_OVERRIDE     = 30f;  // 이 HP 미만 → 무조건 Fear 오버라이드
        private const float FEAR_RECOVERY_HEALTH     = 40f;  // Fear → Normal 회복 기준 체력
        private const float FEAR_RAGE_REVERSE_HEALTH = 60f;  // Fear → Rage 역전 최소 체력
        // 틱당 Rage 타이머 감소량 = GameManager 코루틴 주기(0.1s) × 그룹 수(6)
        // GameManager의 틱 간격 또는 그룹 수 변경 시 이 값도 반드시 동기화해야 한다.
        private const float GAME_TICK_INTERVAL_SEC   = 0.1f; // GameManager 코루틴과 동기화 필요
        private const int   TICK_GROUP_COUNT         = 6;    // GameManager 그룹 수와 동기화 필요
        private const float RAGE_TIMER_TICK_DELTA    = GAME_TICK_INTERVAL_SEC * TICK_GROUP_COUNT; // 0.6f

        // ── [12단계] 경로 이동 속도 상수 ─────────────────────────────────────────
        // 기획서 수치 확정값
        private const float VILLAGER_MOVE_SPEED  = 2.0f;  // 기획서 수치: 일반 이동 속도 (타일/초)
        private const float VILLAGER_FLEE_SPEED  = 3.0f;  // 기획서 수치: Fleeing 상태 이동 속도 (타일/초)
        private const float WAYPOINT_ARRIVE_DIST     = 0.05f; // 중간 웨이포인트 도착 반경 (정확한 경로 추종)
        private const float TERMINAL_ARRIVE_DIST     = 0.5f;  // 최종 웨이포인트 도착 반경 — 콜라이더 밀림에도 진동 없이 안착
        private const float STATIONARY_SNAP_THRESHOLD = 1.0f; // 정지 상태에서 이 거리(1타일) 초과로 이탈 시에만 논리 좌표로 복귀

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

        [Tooltip("F-A 초기 성격. None이면 Awake에서 6종 중 랜덤 배분 (RecruitmentSystem과 동일 규칙). " +
                 "특정 성격을 지정하면 그 값을 그대로 사용 — 디버그/시연용.")]
        [SerializeField] private Personality _initialPersonality = Personality.None;

        #endregion

        #region ── Private Fields ──

        // ── 5단계 신규: SensorSystem이 Brain에 접근하기 위한 공개 프로퍼티 ──────────
        // SensorSystem.RegisterVillager()에서 Brain.VillagerId를 읽고,
        // UpdateAllSensors()에서 Brain을 통해 환경 플래그를 직접 갱신한다.
        // private set: VillagerFSM.Awake()에서만 할당하고 외부에서는 읽기만 허용한다.
        // 기존 private Brain 필드를 이 auto-property로 교체 — 의미는 동일하다.
        public VillagerBrain Brain { get; private set; }

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

        // 현재 점유 중인 자원 노드. 채집 완료/중단 시 Release() 호출을 위해 추적한다.
        private ResourceNode _currentGatherNode;

        // ── 4단계 신규: ActionDatabase / BuildingQueue 의존성 ────────────────
        // GameManager.InjectDependencies() 호출 시 주입된다.
        // Update() 및 Tick() 내부에서는 절대 GetComponent/FindObjectOfType 호출 금지.
        private ActionDatabase _actionDatabase;
        private BuildingQueue  _buildingQueue;

        // ── 5B단계 신규: GOAPPlannerJob 스케줄링 컨텍스트 ──────────────────
        // Planning 상태 진입 시 Schedule()로 생성, 완료 또는 타임아웃 시 Dispose()된다.
        // IsScheduled 플래그로 유효성을 확인한다.
        private GOAPPlannerScheduler.PlanningContext _planningContext;

        // ── [N3b] 플랜 수선 카운터 (FallbackCounter와 별도 — 의미가 다르다) ──
        // 수선: 실행 중 세계 변화로 Prec 불충족 → Goal 유지 채 재플래닝
        // FallbackCounter: 플랜 자체 실패 횟수 (NoSolutionFound)
        private int    _planRepairCount;
        private bool   _replanTriggeredByRepair;
        private string _lastPlanningGoalId;

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

        // ── [12단계] 경로 이동 추적 필드 ──────────────────────────────────────────
        // JPS 경로 탐색 결과를 저장하고 웨이포인트 단위로 이동을 관리한다.

        /// <summary>
        /// 현재 이동 중인 경로. StartPathTo()에서 JPS 결과로 채워진다.
        /// 중간 타일까지 포함한 완전한 타일 단위 경로 (보간 포함).
        /// </summary>
        private List<Vector2Int> _currentPath = new List<Vector2Int>();

        /// <summary>_currentPath에서 현재 향하고 있는 웨이포인트의 인덱스.</summary>
        private int _pathIndex = 0;

        /// <summary>
        /// MoveTowards로 이동 중인지 여부.
        /// false이면 Update()에서 transform을 Brain 논리 위치로 부드럽게 스냅한다.
        /// </summary>
        private bool _isMoving = false;

        /// <summary>현재 이동 중인 웨이포인트의 월드 좌표. Vector3.MoveTowards의 목표값.</summary>
        private Vector3 _waypointTarget = Vector3.zero;

        // ── 물리 컴포넌트 캐시 ──────────────────────────────────────────────────
        // Awake에서 부착 후 캐시. Update에서 rb.MovePosition으로 이동시켜 유닛 간 자동 분리 + 발사체 히트 지원.
        private Rigidbody _rigidbody;
        private SphereCollider _sphereCollider;

        // ── [Phase 1 F1] 사고 말풍선 마지막 발화 시간 ───────────────────────────
        // -999f로 초기화하여 첫 발화는 시간 게이트를 통과한다.
        private float _lastThoughtTime = -999f;

        // ── [Phase 1 검증용] 사고 말풍선 발화 횟수 카운터 ──────────────────────
        // GameManager ContextMenu "Phase1 F1 — 말풍선 발화 카운터 출력"에서 분당 발화율 계산에 사용.
        private int _thoughtBubbleCount = 0;

        // ── [12단계] 공유 walkable 그리드 (static — 모든 주민 공유) ─────────────
        // 현재 맵은 100×100 전체 통행 가능. 장애물 추가 시 이 배열을 수정한다.
        // null이면 첫 번째 StartPathTo() 호출 시 초기화된다.
        private static bool[,] _walkableGrid = null;

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
        public string AgentId => Brain?.VillagerId ?? string.Empty;

        /// <summary>현재 FSM 최상위 상태.</summary>
        public VillagerState CurrentState => Brain?.FSMState ?? VillagerState.Dead;

        /// <summary>충성도 (0~100).</summary>
        public float LoyaltyLevel => Brain?.LoyaltyLevel ?? 0f;

        /// <summary>원래 소속 팩션 ID.</summary>
        public int OriginalFactionId => Brain?.OriginalFactionId ?? 0;

        /// <summary>현재 체력 (0~100).</summary>
        public float HealthLevel => Brain?.HealthLevel ?? 0f;

        /// <summary>현재 포만감 (0~100). 높을수록 배부름.</summary>
        public float SatietyLevel => Brain?.SatietyLevel ?? 0f;

        /// <summary>현재 피로도 (0~100).</summary>
        public float FatigueLevel => Brain?.FatigueLevel ?? 0f;

        /// <summary>생존 여부.</summary>
        public bool IsAlive => Brain?.IsAlive ?? false;

        /// <summary>현재 목표 타일로 이동 중이면 true. 채취/건설 전 이동과 실제 작업을 구분하는 데 사용.</summary>
        public bool IsMoving => _isMoving;

        /// <summary>[Phase 1 F1 검증] 말풍선 발화 총 횟수 (게임 시작부터 누적). GameManager ContextMenu에서 분당 발화율 계산에 사용.</summary>
        public int ThoughtBubbleCount => _thoughtBubbleCount;

        #endregion

        #region ── 디버그 전용 메서드 ──

        /// <summary>
        /// [Phase 1 디버그] 현재 플랜을 즉시 무효화하고 Idle 상태로 전이하여 리플래닝을 유도한다.
        /// GameManager ContextMenu에서 F5(부상 전투 비용) / F7(탐험 유도) 테스트 시 사용한다.
        /// </summary>
        public void ForceReplan()
        {
            if (Brain == null || !Brain.IsAlive) return;
            Brain.ReplanCooldown = 0f;
            Brain.CurrentGoalId  = string.Empty;
            TransitionTo(VillagerState.Idle);
        }

        #endregion

        #region ── Unity 생명주기 메서드 ──

        /// <summary>
        /// Unity가 GameObject 활성화 시 가장 먼저 호출한다.
        /// VillagerBrain을 초기화하고 WorldState 싱글턴 참조를 캐시한다.
        /// </summary>
        private void Awake()
        {
            // VillagerBrain 초기화 — 모든 상태 데이터의 컨테이너
            Brain = new VillagerBrain
            {
                // 런타임 GUID 생성: 에디터에서 복사본을 만들어도 ID가 충돌하지 않는다
                VillagerId       = Guid.NewGuid().ToString(),
                Role             = _initialRole,
                OriginalFactionId = 0, // TODO: 기획팀 — 팩션 ID 할당 방식 확인 필요
                FSMState         = VillagerState.Idle,
                LODState         = LODState.LOD_Idle,
            };

            // F-A: 씬 배치본 성격 배분. Inspector가 None이면 6종 중 랜덤(모집 로직과 동일 규칙),
            // 지정값이면 그 값을 그대로 사용한다. RecruitmentSystem 경로는 InitFromRecruitData가
            // Awake 이후에 재할당하므로 여기 배분값을 덮어써도 무해하다.
            Brain.Personality = _initialPersonality == Personality.None
                ? (Personality)UnityEngine.Random.Range(1, 7)
                : _initialPersonality;

            // AuthoritativeWorldState 싱글턴 캐시
            // Update()에서 직접 접근하지 않기 위해 Awake에서 저장
            _worldState = AuthoritativeWorldState.Instance;

            if (_worldState == null)
            {
                // GameManager보다 VillagerFSM이 먼저 Awake될 경우 발생할 수 있다.
                // InjectDependencies()에서 재할당하므로 경고만 출력한다.
                Debug.LogWarning($"[VillagerFSM] Awake: AuthoritativeWorldState.Instance가 null입니다. " +
                                 $"GameManager의 Script Execution Order를 VillagerFSM보다 높게 설정하거나 " +
                                 $"InjectDependencies()를 호출하세요. AgentId={Brain.VillagerId}");
            }

            // ── SphereCollider + Rigidbody 자동 부착 ─────────────────────────
            // 두 용도를 하나로 통합:
            //   1) PlayerInputController.Physics.Raycast 클릭 감지 (트리거·논트리거 무관하게 히트)
            //   2) 유닛 간 물리 분리 + 나중에 발사체 히트 판정용 (비트리거 콜라이더 필요)
            // 프리팹에 이미 붙어 있으면 재사용, 없으면 런타임에 AddComponent.
            _sphereCollider = GetComponent<SphereCollider>();
            if (_sphereCollider == null)
            {
                _sphereCollider = gameObject.AddComponent<SphereCollider>();
            }
            _sphereCollider.radius    = 0.3f;   // 유닛 중심 반경 — 같은 타일 겹침 시 분리 부담 최소화
            _sphereCollider.isTrigger = false;  // 물리 분리 활성 (Physics.Raycast는 논트리거도 히트하므로 클릭 감지도 유지됨)

            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody == null)
            {
                _rigidbody = gameObject.AddComponent<Rigidbody>();
            }
            _rigidbody.useGravity     = false;
            // linearDamping 낮게 유지 — 5.0은 depenetration 임펄스를 즉시 소멸시켜 서로 겹친 채 정지하는 버그 유발.
            // 1.0이면 물리 엔진의 분리 힘이 실제 이동으로 반영되어 마주 겹친 유닛이 옆으로 밀려난다.
            _rigidbody.linearDamping  = 1f;
            _rigidbody.angularDamping = 1f;
            _rigidbody.interpolation  = RigidbodyInterpolation.Interpolate;
            // Continuous는 Kinematic-vs-Dynamic용. Dynamic-vs-Dynamic에는 Discrete가 안정적이고 저비용이다.
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            // Z 위치 잠금 + 모든 회전 잠금 (2D-in-3D 씬에서 평면 유지)
            _rigidbody.constraints =
                RigidbodyConstraints.FreezePositionZ |
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationY |
                RigidbodyConstraints.FreezeRotationZ;

            // ── Villager 레이어 설정 ─────────────────────────────────────────
            // PlayerInputController의 레이어 마스크가 "Villager" 레이어만 감지하므로
            // 이 GameObject가 "Villager" 레이어에 있어야 한다.
            int villagerLayer = LayerMask.NameToLayer("Villager");
            if (villagerLayer >= 0)
            {
                gameObject.layer = villagerLayer;
            }
            else
            {
                Debug.LogError("[VillagerFSM] 'Villager' 레이어가 Project Settings > Tags and Layers에 " +
                               "등록되지 않았습니다. 레이어를 추가한 뒤 실행하세요.");
            }
        }

        /// <summary>
        /// Unity가 매 프레임 호출한다.
        /// AnyState 전이(Dead, P0)를 감지하고, LOD 모드에서는 틱 누적을 처리한다.
        /// 실제 상태 로직은 외부에서 Tick()을 호출할 때 실행한다.
        /// </summary>
        private void Update()
        {
            if (Brain == null) return;

            // [PR Fix]: F-009 — P0(생존 위기)와 Dead 전이는 Tick 분산 없이 매 프레임 즉시 반응이
            // 필요하므로 Update에서 처리한다. 일반 상태 로직은 외부 GameManager가 Tick()으로
            // 그룹별 호출한다. GameManager 코루틴(0.1초 간격)을 통하면 최대 0.1초 지연이 생기고
            // P0 상황(HP<20, 허기>80, 피로>90)은 그 지연조차 치명적일 수 있기 때문이다.

            // ── AnyState 전이 #1: 사망 감지 ──────────────────────────────────
            // isAlive가 false가 되는 순간 어떤 상태에서도 즉시 Dead로 전이한다.
            // 이 체크는 Update에서 수행하여 Tick 간격(0.1초) 지연 없이 즉시 반응한다.
            if (!Brain.IsAlive && Brain.FSMState != VillagerState.Dead)
            {
                TransitionTo(VillagerState.Dead);
                return;
            }

            // ── AnyState 전이 #2: P0 Goal 긴급 감지 ─────────────────────────
            // 사망 외 P0(생존 위기) 조건이 발동되면 현재 상태에 관계없이 Planning으로 이동.
            // 이미 Planning/Dead 상태이면 중복 전이를 방지한다.
            if (Brain.IsAlive
                && Brain.FSMState != VillagerState.Planning
                && Brain.FSMState != VillagerState.Dead
                && IsP0GoalActive())
            {
                string p0GoalId = GetP0GoalId();

                // 현재 추구 중인 Goal이 이미 P0 Goal과 동일하면 재전이 불필요
                if (Brain.CurrentGoalId != p0GoalId)
                {
                    Debug.Log($"[VillagerFSM] AnyState → Planning (P0 Goal: {p0GoalId}). AgentId={AgentId}");
                    // F4: P0 전환 발화 — Goal이 바뀌는 순간에만 1회 표시
                    // [F-A] Glutton 성격이면 50% 확률로 성격 라인 대체 (SurviveHunger 한정)
                    if (p0GoalId == "SurviveHunger")
                    {
                        string persLine = TryPickPersonalityLine("p0_hunger");
                        ShowThoughtBubble(persLine ?? Pick(THOUGHT_P0_HUNGER));
                    }
                    else if (p0GoalId == "SurviveFatigue") ShowThoughtBubble(Pick(THOUGHT_P0_FATIGUE));
                    Brain.CurrentGoalId = p0GoalId;
                    TransitionTo(VillagerState.Planning);
                    return;
                }
            }

            // ── AnyState 전이 #3: HP 30% 미만 + 전투 중 → Fear → Fleeing [8단계] ─
            // 아무리 분노 상태여도 체력이 임계값 미만이면 즉시 도주.
            if (Brain.IsAlive
                && Brain.NearEnemy
                && Brain.FSMState != VillagerState.Dead
                && Brain.FSMState != VillagerState.Fleeing
                && Brain.HealthLevel < FEAR_HEALTH_OVERRIDE)
            {
                Brain.CombatMentalState = CombatMentalState.Fear;
                Brain.AttackModifier    = 1.0f;
                TransitionTo(VillagerState.Fleeing);
                return;
            }

            // ── AnyState 전이 #4: NearEnemy 진입 시 자원 채취/이동 중단하고 Fighting 진입 ─
            // State_Idle만 NearEnemy를 감지하던 기존 로직으로는 Executing/Planning 중 적이
            // 접근해도 액션 타이머가 끝날 때까지 반응하지 못했다. AnyState에서 감지해 즉시
            // Fighting으로 전이시켜 전투 상태로 진입하게 한다.
            //   - Fear 조건은 위 #3에서 이미 처리됨(HP<30 → Fleeing).
            //   - LOD_FSM은 State_LOD_FSM()에서 자체적으로 Idle 복귀 처리하므로 여기서 건너뛴다.
            //   - CommandConflict는 1틱 내 자체 해소되는 전이 상태이므로 건너뛴다.
            //   - EnterFighting()이 CurrentPlan/CurrentGoalId를 정리하고 AbortCurrentPath()로
            //     이동 경로를 중단하며, OnStateExit(Executing)이 자원 예약을 해제한다.
            if (Brain.IsAlive
                && Brain.NearEnemy
                && Brain.FSMState != VillagerState.Fighting
                && Brain.FSMState != VillagerState.Fleeing
                && Brain.FSMState != VillagerState.Dead
                && Brain.FSMState != VillagerState.LOD_FSM
                && Brain.FSMState != VillagerState.CommandConflict)
            {
                Debug.Log($"[VillagerFSM] AnyState → Fighting (NearEnemy 감지, prev={Brain.FSMState}). AgentId={AgentId}");
                TransitionTo(VillagerState.Fighting);
                return;
            }

            // ── LOD 틱 누적 (LOD_FSM 상태일 때만) ───────────────────────────
            // LOD 모드의 내부 상태는 매 프레임이 아닌 0.5초 간격으로만 실행한다.
            if (Brain.FSMState == VillagerState.LOD_FSM)
            {
                Brain.LODTickAccumulator += Time.deltaTime;
            }

            // Replanning 쿨다운 감소
            if (Brain.ReplanCooldown > 0f)
            {
                Brain.ReplanCooldown -= Time.deltaTime;
                if (Brain.ReplanCooldown < 0f) Brain.ReplanCooldown = 0f;
            }

            // RefusingOrder 타이머 감소
            if (Brain.FSMState == VillagerState.RefusingOrder && Brain.RefuseMessageTimer > 0f)
            {
                Brain.RefuseMessageTimer -= Time.deltaTime;
            }

            // ── [12단계] 경로 이동 처리 (velocity 기반) ─────────────────────────
            // linearVelocity로 이동하고 도착 시 0으로 초기화한다.
            // MovePosition은 매 프레임 위치를 강제해 물리 depenetration을 무효화하는 문제로
            // 반대 방향 두 유닛이 겹쳐 정지하는 버그를 유발했다. velocity 방식은 물리 엔진에
            // 이동과 충돌 해소를 위임하므로 겹침 시 자연스럽게 밀려나며 각자 목표로 진행한다.
            if (_isMoving)
            {
                float speed = (Brain.FSMState == VillagerState.Fleeing)
                              ? VILLAGER_FLEE_SPEED
                              : VILLAGER_MOVE_SPEED;

                Vector3 toTarget = _waypointTarget - _rigidbody.position;
                float distToTarget = toTarget.magnitude;

                bool isTerminal = (_pathIndex >= _currentPath.Count - 1);
                float arriveDist = isTerminal ? TERMINAL_ARRIVE_DIST : WAYPOINT_ARRIVE_DIST;

                if (distToTarget < arriveDist)
                {
                    // 도착: 관성 방지를 위해 velocity 0
                    _rigidbody.linearVelocity = Vector3.zero;

                    // Brain 논리 좌표 갱신 (SensorSystem 감지 정확성)
                    Brain.TileX = _currentPath[_pathIndex].x;
                    Brain.TileY = _currentPath[_pathIndex].y;

                    // ── [13단계] FoW 시야 + 자원 노드 발견 갱신 ────────────────
                    if (MapConfig.Active != null)
                    {
                        int sight = MapConfig.Active.villagerSightRadius;
                        FowManager.Instance?.RevealArea(Brain.TileX, Brain.TileY, sight);
                        SensorSystem.Instance?.DiscoverArea(Brain.TileX, Brain.TileY, sight);
                    }

                    _pathIndex++;

                    if (_pathIndex >= _currentPath.Count)
                    {
                        _isMoving = false;
                    }
                    else
                    {
                        _waypointTarget = new Vector3(
                            _currentPath[_pathIndex].x,
                            _currentPath[_pathIndex].y,
                            0f);
                    }
                }
                else
                {
                    // 이동: 목표 방향 * 속도를 velocity로 지정. 물리 엔진이 콜라이더 겹침을 자동 해소한다.
                    // 도달 직전(< speed*dt)에는 딱 도착할 만큼만 이동시켜 overshoot 방지.
                    float stepThisFrame = speed * Time.deltaTime;
                    float velMag = distToTarget < stepThisFrame ? distToTarget / Time.deltaTime : speed;
                    _rigidbody.linearVelocity = (toTarget / distToTarget) * velMag;
                }
            }
            else
            {
                // 정지 상태: 관성 제거. 콜라이더가 유닛끼리 자연스럽게 분리하도록 두되,
                // 논리 위치에서 심하게(> 1타일) 이탈하면 부드럽게 복귀 (텔레포트/튕김 복원용).
                _rigidbody.linearVelocity = Vector3.zero;

                Vector3 logicalPos = new Vector3(Brain.TileX, Brain.TileY, 0f);
                if (Vector3.Distance(_rigidbody.position, logicalPos) > STATIONARY_SNAP_THRESHOLD)
                {
                    Vector3 snapPos = Vector3.MoveTowards(
                        _rigidbody.position,
                        logicalPos,
                        VILLAGER_MOVE_SPEED * Time.deltaTime);
                    _rigidbody.MovePosition(snapPos);
                }
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
            if (Brain == null || !Brain.IsAlive)
            {
                Debug.LogWarning($"[VillagerFSM] TryExecuteOrder: 사망하거나 초기화되지 않은 에이전트. AgentId={AgentId}");
                return false;
            }

            // 명령을 Brain에 저장한 뒤 CommandConflict 상태에서 처리
            Brain.PendingOrder    = order;
            Brain.HasPendingOrder = true;

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
            if (Brain == null)
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
            if (Brain == null) return;

            // 수신된 메시지를 먼저 처리한다 (상태 전이를 유발할 수 있음)
            ProcessMessageQueue();

            // 현재 상태 핸들러 실행
            switch (Brain.FSMState)
            {
                case VillagerState.Idle:            State_Idle();            break;
                case VillagerState.Planning:         State_Planning();        break;
                case VillagerState.Executing:        State_Executing();       break;
                case VillagerState.Replanning:       State_Replanning();      break;
                case VillagerState.CommandConflict:  State_CommandConflict(); break;
                case VillagerState.RefusingOrder:    State_RefusingOrder();   break;
                case VillagerState.Dead:             State_Dead();            break;
                case VillagerState.LOD_FSM:          State_LOD_FSM();         break;
                case VillagerState.Fighting:         State_Fighting();        break;
                case VillagerState.Fleeing:          State_Fleeing();         break;
                default:
                    Debug.LogWarning($"[VillagerFSM] Tick: 처리되지 않은 상태 '{Brain.FSMState}'. AgentId={AgentId}");
                    break;
            }
        }

        /// <summary>Tick 그룹 인덱스. GameManager가 읽어서 호출 여부를 판단한다.</summary>
        public int TickGroupIndex => _tickGroupIndex;

        /// <summary>
        /// 동적 스폰 주민의 틱 그룹을 배정한다. GameManager.SpawnVillager()에서 호출.
        /// Inspector 직렬화 기본값(0)이 아닌 분산된 그룹 번호를 주입하여 6-그룹 부하 분산을 유지한다.
        /// </summary>
        public void SetTickGroupIndex(int groupIndex) => _tickGroupIndex = Mathf.Clamp(groupIndex, 0, 5);

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
            bool cooldownActive = Brain.ReplanCooldown > 0f;

            if (!cooldownActive)
            {
                // 우선순위 P1: 적 위협 → [8단계] Fighting 상태 직접 진입
                // 진입 시 사고 말풍선은 EnterFighting()이 담당한다 (AnyState 진입 경로와 일관성 유지).
                if (Brain.NearEnemy)
                {
                    TransitionTo(VillagerState.Fighting);
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
                    bool goalChanged = Brain.CurrentGoalId != "BuildStructure";
                    Brain.CurrentGoalId = "BuildStructure";
                    if (goalChanged) ShowThoughtBubble(Pick(THOUGHT_GOAL_BUILD));
                    TransitionTo(VillagerState.Planning);
                    return;
                }

                // 우선순위 P2b: 자원 부족 — 가장 부족한 자원의 구체 Goal 선택 [P1 무한루프 수정]
                // 범용 "GatherResources"를 직접 사용하면 수치형 플래너가 WoodStock만 확인해 -1 루프 발생.
                // SelectGatherGoalId()가 실제 부족 자원을 확인하여 GatherWood/Stone/Iron/Copper/Food 중 하나를 반환한다.
                if (_worldState != null)
                {
                    string gatherGoal = SelectGatherGoalId();
                    if (gatherGoal != null)
                    {
                        bool goalChanged = Brain.CurrentGoalId != gatherGoal;
                        Brain.CurrentGoalId = gatherGoal;
                        if (goalChanged)
                        {
                            // [F-A] Diligent/Lazy 성격이면 50% 확률로 성격 라인 대체
                            string persLine = TryPickPersonalityLine("gather");
                            ShowThoughtBubble(persLine ?? Pick(THOUGHT_GOAL_GATHER));
                        }
                        TransitionTo(VillagerState.Planning);
                        return;
                    }
                }

                // 우선순위 P3: 탐험 (모든 자원이 50 이상이고 미탐험 타일이 있을 때)
                // TODO: 기획팀 — unexploredTilesNearby 플래그를 Brain에 추가할지 별도 시스템으로 관리할지 결정 필요
                // 현재는 WorldState 자원이 모두 50 이상이면 Explore Goal로 전환 (더미)
                if (_worldState != null && AreAllStocksAboveThreshold(GATHER_STOCK_HIGH_THRESHOLD))
                {
                    bool goalChanged = Brain.CurrentGoalId != "Explore";
                    Brain.CurrentGoalId = "Explore";
                    if (goalChanged)
                    {
                        // [F-A] Curious 성격이면 50% 확률로 성격 라인 대체
                        string persLine = TryPickPersonalityLine("explore");
                        ShowThoughtBubble(persLine ?? Pick(THOUGHT_GOAL_EXPLORE));
                    }
                    TransitionTo(VillagerState.Planning);
                    return;
                }
            }

            // 우선순위 P4: LOD 모드 진입 (30타일 초과 + 비전투)
            // 쿨다운과 무관하게 거리 조건만으로 LOD 진입 가능
            if (!Brain.NearEnemy && GetDistanceToBase() > LOD_DISTANCE_THRESHOLD)
            {
                TransitionTo(VillagerState.LOD_FSM);
                return;
            }

            // 위 조건이 모두 해당 없으면 계속 Idle 유지
        }

        /// <summary>
        /// [Planning] GOAPPlannerJob의 완료를 폴링하고 결과를 처리하는 상태.
        ///
        /// 5B단계: SimulatePlanResult() 더미를 Job System 기반 실제 A* GOAP로 교체.
        /// EnterPlanning()에서 Job을 스케줄하고, 이 핸들러에서 완료를 폴링한다.
        /// Job이 완료되면 결과를 읽어 Brain.CurrentPlan에 적재하고 Executing으로 전이한다.
        /// </summary>
        private void State_Planning()
        {
            // ── 타임아웃 체크 ─────────────────────────────────────────────────
            // 0.5초 이내에 Job이 완료되지 않으면 강제 종료 후 Replanning으로 전이한다.
            if (Time.time - Brain.PlanningStartTime > PLANNING_TIMEOUT_SEC)
            {
                Debug.LogWarning(
                    $"[VillagerFSM] Planning 타임아웃 ({PLANNING_TIMEOUT_SEC}초 초과). " +
                    $"Replanning으로 전이. AgentId={AgentId}, GoalId={Brain.CurrentGoalId}"
                );

                // 진행 중인 Job이 있으면 강제 완료 후 NativeArray 해제
                if (_planningContext.IsScheduled)
                {
                    _planningContext.JobHandle.Complete();
                    _planningContext.Dispose();
                }

                RequestReplanning(AbortReason.PlanFailed,
                    $"Planning 타임아웃 {PLANNING_TIMEOUT_SEC}s 초과, Goal={Brain.CurrentGoalId}");
                return;
            }

            // ── Job 스케줄 실패 체크 ──────────────────────────────────────────
            // EnterPlanning()에서 Schedule()이 실패한 경우 (null 입력 등)
            if (!_planningContext.IsScheduled)
            {
                Debug.LogWarning(
                    $"[VillagerFSM] Planning: _planningContext가 스케줄되지 않았습니다. " +
                    $"Replanning으로 전이. AgentId={AgentId}"
                );
                RequestReplanning(AbortReason.PlanFailed, "Job 스케줄 실패");
                return;
            }

            // ── Job 완료 대기 (비블로킹 폴링) ────────────────────────────────
            // IsCompleted가 false이면 이번 Tick에서는 아무것도 하지 않는다.
            // 다음 Tick(0.1초 후)에 다시 확인한다.
            if (!_planningContext.JobHandle.IsCompleted) return;

            // ── Job 완료 → 결과 읽기 ─────────────────────────────────────────
            // Complete()를 명시적으로 호출해야 JobHandle이 정리되고 NativeArray 접근이 안전해진다.
            _planningContext.JobHandle.Complete();

            // [PR Fix]: N-001 — ReadResult() 호출부를 새 시그니처로 변경
            // alreadySatisfied=true이면 현재 상태가 이미 목표를 달성한 것이므로
            // 빈 플랜으로 바로 Executing 전이 (Replanning 무한루프 방지)
            List<string> actionSequence = _planningContext.ReadResult(out bool alreadySatisfied);
            _planningContext.Dispose(); // NativeArray 즉시 해제

            // [PR Fix]: N-001 수정 — 이미 목표 달성 상태에서 Idle 직접 전이
            // 기존: TransitionTo(Executing) → EnterExecuting()이 빈 CurrentPlan 감지 → 경고 로그 → Idle
            //       정상 달성 상황에서 "CurrentPlan이 비어있습니다" 오탐 경고가 매번 출력됨
            // 수정: 불필요한 Executing 경유 없이 Idle로 직접 전이하여 경고 제거 및 상태 전이 간소화
            if (alreadySatisfied)
            {
                Debug.Log(
                    $"[VillagerFSM] 현재 상태가 이미 목표를 달성했습니다. 직접 Idle로 복귀. " +
                    $"AgentId={AgentId}"
                );
                Brain.CurrentGoalId   = null;
                Brain.CurrentActionId = null;
                TransitionTo(VillagerState.Idle);
                return;
            }

            bool planSuccess = actionSequence != null && actionSequence.Count > 0;

            if (planSuccess)
            {
                // 플랜 성공 → Brain.CurrentPlan 큐에 적재
                Brain.CurrentPlan.Clear();
                Brain.CurrentPlanFull.Clear();
                foreach (string actionId in actionSequence)
                {
                    Brain.CurrentPlan.Enqueue(actionId);
                    Brain.CurrentPlanFull.Add(actionId);
                }

                Brain.ResetFallback();
                _planRepairCount           = 0; // [N3b] 수선 성공 → 카운터 리셋
                _replanTriggeredByRepair   = false;

                Debug.Log(
                    $"[VillagerFSM] Planning 성공 (GOAPPlannerJob). " +
                    $"플랜: [{string.Join(" → ", actionSequence)}]. AgentId={AgentId}"
                );

                TransitionTo(VillagerState.Executing);
            }
            else
            {
                // [N3b] 수선 트리거 재플래닝이 실패한 경우 수선 카운터를 증가시킨다.
                if (_replanTriggeredByRepair)
                {
                    _replanTriggeredByRepair = false;
                    _planRepairCount++;
                    if (_planRepairCount >= 2)
                    {
                        // 2연속 수선 실패 → Goal 재선택 (Idle 복귀)
                        Debug.LogWarning(
                            $"[VillagerFSM] 플랜 수선 2연속 실패. Goal 재선택. AgentId={AgentId}");
                        _planRepairCount    = 0;
                        Brain.CurrentGoalId = string.Empty;
                        TransitionTo(VillagerState.Idle);
                        return;
                    }
                }

                // 해결책 없음 → Replanning
                Debug.LogWarning(
                    $"[VillagerFSM] Planning 실패 (NoSolutionFound). " +
                    $"Goal={Brain.CurrentGoalId}. AgentId={AgentId}"
                );
                RequestReplanning(AbortReason.PlanFailed,
                    $"NoSolutionFound, Goal={Brain.CurrentGoalId}");
            }
        }

        /// <summary>
        /// [Executing] 플래닝된 Action을 순차적으로 실행하는 상태.
        ///
        /// [12단계 수정]: 이동 완료 후 액션 타이머를 시작한다.
        ///   1. _isMoving == true이면 아직 목표 타일에 이동 중 → 타이머 보류
        ///   2. Brain.ActionStartTime == 0f이면 이동 완료 직후 최초 1회 → 타이머 시작
        ///   3. 타이머 경과(ACTION_SIMULATE_SEC) 후 OnActionCompleted() 호출
        /// </summary>
        private void State_Executing()
        {
            // ── [12단계] 이동 중이면 액션 타이머 보류 ────────────────────────────
            // 목표 타일에 도착해야 액션을 수행할 수 있다.
            if (_isMoving) return;

            // ── 도착 후 최초 1회: 액션 타이머 시작 ──────────────────────────────
            // StartNextAction()에서 ActionStartTime = -1f(센티넬)로 초기화했다.
            // 이동이 완료된 첫 틱에 이 조건이 true → 타이머를 Time.time으로 설정한다.
            if (Brain.ActionStartTime < 0f)
            {
                Brain.ActionStartTime = Time.time;

                // ── [13단계] 액션 목적지 도착 시 FoW 시야 + 자원 노드 발견 갱신 ────
                // 이동이 완료되어 목적지 타일에 실제로 서있을 때 주변 시야를 공개한다.
                // 웨이포인트 단위 갱신에 추가로 목적지에서 한 번 더 갱신하여
                // 채집 장소나 기지 주변 시야가 확실히 열리도록 한다.
                if (FowManager.Instance != null && MapConfig.Active != null)
                {
                    int sight = MapConfig.Active.villagerSightRadius;
                    FowManager.Instance.RevealArea(Brain.TileX, Brain.TileY, sight);
                    SensorSystem.Instance?.DiscoverArea(Brain.TileX, Brain.TileY, sight);
                }

                Debug.Log($"[VillagerFSM] 목표 도착 후 액션 타이머 시작: '{Brain.CurrentActionId}'. " +
                          $"완료까지 {ACTION_SIMULATE_SEC}초. AgentId={AgentId}");
                return; // 다음 Tick에서 타이머 체크
            }

            // ── 액션 완료 체크 ────────────────────────────────────────────────
            if (Time.time - Brain.ActionStartTime < ACTION_SIMULATE_SEC) return;

            // Action 완료 처리
            OnActionCompleted(Brain.CurrentActionId);

            // 큐에 남은 Action이 있으면 다음 Action 시작
            if (Brain.CurrentPlan.Count > 0)
            {
                // [N3b] 사전 검증: 다음 액션 Prec을 최신 스냅샷으로 재검사한다.
                // Prec 불충족 시 Goal을 유지한 채 Replanning (완료 진척 보존).
                string nextAction = Brain.CurrentPlan.Peek();
                if (!CheckNextActionPrec(nextAction))
                {
                    _replanTriggeredByRepair = true;
                    ShowThoughtBubble(Pick(THOUGHT_PLAN_REPAIR));
                    Debug.Log(
                        $"[VillagerFSM] 플랜 수선: '{nextAction}' Prec 불충족 → " +
                        $"Goal='{Brain.CurrentGoalId}' 유지 채 Replanning. AgentId={AgentId}");
                    TransitionTo(VillagerState.Replanning);
                    return;
                }
                StartNextAction();
            }
            else
            {
                // 모든 Action 완료 → Goal 달성, Idle로 복귀
                Debug.Log($"[VillagerFSM] 플랜 완료. Goal={Brain.CurrentGoalId}. AgentId={AgentId}");
                _planRepairCount      = 0;
                Brain.CurrentGoalId   = null;
                Brain.CurrentActionId = null;
                TransitionTo(VillagerState.Idle);
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
            if (Brain.FallbackCounter >= DEADLOCK_THRESHOLD)
            {
                HandleDeadlock();
                return;
            }

            // 쿨다운이 끝나면 다시 Planning 시도
            if (Brain.ReplanCooldown <= 0f)
            {
                // P0 Goal이 활성화되어 있으면 해당 Goal로 다시 Planning
                if (IsP0GoalActive())
                {
                    Brain.CurrentGoalId = GetP0GoalId();
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
            if (!Brain.HasPendingOrder)
            {
                // 처리할 명령이 없으면 Idle로 복귀
                Debug.LogWarning($"[VillagerFSM] CommandConflict: HasPendingOrder가 false입니다. Idle로 복귀. AgentId={AgentId}");
                TransitionTo(VillagerState.Idle);
                return;
            }

            // [PR Fix]: F-002 — HasPendingOrder = false를 설정하기 전에 PendingOrder를 로컬 변수에
            // 복사한다. 플래그를 먼저 false로 만든 뒤 Brain.PendingOrder를 다시 읽으면
            // 다른 경로에서 PendingOrder가 덮어쓰일 경우 잘못된 명령을 참조하는 버그가 발생한다.
            PlayerOrder localOrder = Brain.PendingOrder;

            // ConflictScore 계산 (복사된 로컬 명령 데이터 사용)
            ConflictScoreData scoreData = ConflictScoreCalculator.Calculate(Brain, localOrder);

            Debug.Log($"[VillagerFSM] ConflictScore={scoreData.ConflictScore:F2}, Threshold={scoreData.Threshold:F2}, " +
                      $"ShouldRefuse={scoreData.ShouldRefuse}. AgentId={AgentId}");

            // 플래그 소비: PendingOrder 복사 완료 후에 false로 설정한다
            Brain.HasPendingOrder = false;

            if (scoreData.ShouldRefuse)
            {
                // 거부 → RefusingOrder 상태
                TransitionTo(VillagerState.RefusingOrder);
            }
            else
            {
                // 수락 → 로컬 복사본으로 Goal로 변환하여 Planning
                // (HasPendingOrder = false 이후에도 localOrder는 안전하게 참조 가능)
                Brain.CurrentGoalId = ConvertOrderToGoalId(localOrder.OrderType);
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
            if (Brain.RefuseMessageTimer <= 0f)
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
            if (Brain.NearEnemy || GetDistanceToBase() <= LOD_DISTANCE_THRESHOLD)
            {
                Debug.Log($"[VillagerFSM] LOD → Full GOAP 복귀 (nearEnemy={Brain.NearEnemy}, dist={GetDistanceToBase():F0}). AgentId={AgentId}");
                Brain.IsLODMode           = false;
                Brain.LODTickAccumulator  = 0f;
                TransitionTo(VillagerState.Idle);
                return;
            }

            // 0.5초 간격 틱
            if (Brain.LODTickAccumulator < LOD_TICK_INTERVAL_SEC) return;
            Brain.LODTickAccumulator -= LOD_TICK_INTERVAL_SEC;

            // 내부 LOD 상태 실행
            switch (Brain.LODState)
            {
                case LODState.LOD_Idle:            LOD_State_Idle();            break;
                case LODState.LOD_GatheringResource: LOD_State_GatheringResource(); break;
                case LODState.LOD_MovingToBase:    LOD_State_MovingToBase();    break;
                case LODState.LOD_Alert:           LOD_State_Alert();           break;
                default:
                    Debug.LogWarning($"[VillagerFSM] LOD_FSM: 처리되지 않은 LOD 상태 '{Brain.LODState}'. AgentId={AgentId}");
                    break;
            }
        }

        /// <summary>
        /// [Fighting] 적과 근접 전투 상태. [8단계]
        /// 매 틱 공격 범위 내 가장 가까운 적에게 피해를 입히고 심리 상태를 평가한다.
        /// </summary>
        private void State_Fighting()
        {
            EvaluateCombatMentalState();

            if (Brain.CombatMentalState == CombatMentalState.Fear)
            {
                TransitionTo(VillagerState.Fleeing);
                return;
            }

            if (!Brain.NearEnemy)
            {
                Brain.CombatMentalState = CombatMentalState.Normal;
                Brain.AttackModifier    = 1.0f;
                TransitionTo(VillagerState.Idle);
                return;
            }

            EnemyFSM target = FindNearestEnemy(ATTACK_RANGE_TILES);
            if (target != null && target.Brain != null && target.Brain.IsAlive)
            {
                // 사거리 안 — 이동 중이던 추격 경로를 중단하고 그 자리에서 공격한다.
                if (_isMoving) AbortCurrentPath();

                float damage = BASE_VILLAGER_DAMAGE * Brain.AttackModifier;
                target.Brain.HealthLevel = Mathf.Max(0f, target.Brain.HealthLevel - damage);
                Debug.Log($"[VillagerFSM] Fighting: Enemy={target.Brain.EnemyId} HP={target.Brain.HealthLevel:F0} " +
                          $"(dmg={damage:F1}, {Brain.CombatMentalState}). AgentId={AgentId}");

                if (target.Brain.HealthLevel <= 0f && target.Brain.IsAlive)
                {
                    target.Brain.IsAlive = false;
                    Debug.Log($"[VillagerFSM] 적 처치! Enemy={target.Brain.EnemyId}. AgentId={AgentId}");
                    if (Brain.CombatMentalState == CombatMentalState.Rage)
                        PublishRageKillEvent();
                }
                return;
            }

            // ── 공격 범위 밖 적 추격 ────────────────────────────────────────────
            // NearEnemy=true(감지 8타일)지만 공격 범위(2타일) 안에 없는 경우.
            // 기존 로직은 여기서 아무 것도 안 해 "전투 진입만 하고 서있는" 상태였다.
            // 이제 감지 범위 내 가장 가까운 적을 향해 JPS 경로로 추격한다.
            EnemyFSM chaseTarget = FindNearestEnemy(CHASE_MAX_RANGE_TILES);
            if (chaseTarget == null || chaseTarget.Brain == null || !chaseTarget.Brain.IsAlive)
            {
                // 감지 범위 내 살아있는 적이 없다 — 다음 틱에 NearEnemy가 false가 되면 자연 종료.
                return;
            }

            // 이미 이동 중이고 적이 크게 이동하지 않았다면 경로 유지 (재산출 비용 절약).
            bool needRepath = !_isMoving;
            if (!needRepath && _currentPath != null && _currentPath.Count > 0)
            {
                var lastWaypoint = _currentPath[_currentPath.Count - 1];
                int drift = Mathf.Abs(lastWaypoint.x - chaseTarget.Brain.TileX)
                          + Mathf.Abs(lastWaypoint.y - chaseTarget.Brain.TileY);
                if (drift >= CHASE_REPATH_DRIFT_TILES) needRepath = true;
            }

            if (needRepath)
            {
                StartPathTo(chaseTarget.Brain.TileX, chaseTarget.Brain.TileY);
            }
        }

        /// <summary>
        /// [Fleeing] Fear 상태 도주. [8단계]
        ///
        /// [12단계 수정]: Brain.TileX/Y 직접 조작을 제거한다.
        ///   이동은 EnterFleeing()에서 StartPathTo(_baseTileX, _baseTileY)로 시작된 JPS 경로가
        ///   Update()의 MoveTowards 루프에서 VILLAGER_FLEE_SPEED(3.0)로 처리한다.
        ///   이 핸들러는 심리 상태 전이 판정만 담당한다.
        /// </summary>
        private void State_Fleeing()
        {
            EvaluateCombatMentalState();

            if (Brain.CombatMentalState == CombatMentalState.Rage)
            {
                // 의도적 역전: Fear→Rage 조건 충족 시 같은 틱에 Fighting으로 복귀.
                Debug.Log($"[VillagerFSM] Fleeing → Rage 역전 → Fighting. AgentId={AgentId}");
                TransitionTo(VillagerState.Fighting);
                return;
            }

            if (Brain.CombatMentalState == CombatMentalState.Normal && !Brain.NearEnemy)
            {
                Debug.Log($"[VillagerFSM] Fleeing → 회복 완료 → Idle. AgentId={AgentId}");
                TransitionTo(VillagerState.Idle);
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
            if (_worldState != null && IsAnyStockLow() && Brain.NearResource)
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
            if (Time.time - Brain.LODActionStartTime >= LOD_GATHER_DURATION)
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
            if (Time.time - Brain.LODActionStartTime >= LOD_MOVE_DURATION)
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
            Brain.IsLODMode           = false;
            Brain.LODTickAccumulator  = 0f;
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
            if (Brain == null) return;

            VillagerState prevState = Brain.FSMState;

            // ── Exit 처리: 이전 상태 정리 ─────────────────────────────────────
            OnStateExit(prevState, newState);

            // 상태 변경
            Brain.FSMState = newState;

            // ── Enter 처리: 새 상태 초기화 ────────────────────────────────────
            OnStateEnter(newState, prevState);
        }

        /// <summary>
        /// LOD 내부 상태를 전이한다. LODActionStartTime을 갱신한다.
        /// </summary>
        private void TransitionToLOD(LODState newLODState)
        {
            if (Brain == null) return;

            Brain.LODState         = newLODState;
            Brain.LODActionStartTime = Time.time;
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
                    // [PR Fix]: F-003 — 조건 없이 무조건 해제
                    if (_hasActiveReservation)
                        ReleaseCurrentReservation();
                    ReleaseGatherNode();
                    Brain.IsExecutingPlan = false;
                    break;

                case VillagerState.LOD_FSM:
                    Brain.IsLODMode          = false;
                    Brain.LODTickAccumulator = 0f;
                    break;

                // ── [12단계] Fleeing 종료 시 경로 즉시 중단 ─────────────────────
                // Rage 역전(→Fighting) 또는 회복(→Idle) 시 도주 경로를 중단하고
                // transform을 Brain 논리 위치로 스냅한다.
                case VillagerState.Fleeing:
                    AbortCurrentPath();
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
                    Brain.IsLODMode          = true;
                    Brain.LODState           = LODState.LOD_Idle;
                    Brain.LODTickAccumulator = 0f;
                    Brain.LODActionStartTime = Time.time;
                    break;

                case VillagerState.Fighting:
                    EnterFighting();
                    break;

                case VillagerState.Fleeing:
                    EnterFleeing();
                    break;
            }
        }

        // ── Enter 헬퍼 메서드 ──────────────────────────────────────────────────

        /// <summary>
        /// Planning 상태 진입 초기화.
        ///
        /// 5B단계: GOAPPlannerJob을 스케줄링한다.
        /// Brain.CurrentGoalId, _registry, _worldState가 모두 유효한 경우에만 스케줄링된다.
        /// 하나라도 null이면 _planningContext.IsScheduled = false로 설정되고,
        /// State_Planning()이 다음 Tick에서 Replanning으로 전이한다.
        /// </summary>
        private void EnterPlanning()
        {
            // [N3b] Goal이 바뀌면 수선 카운터 리셋 (다른 Goal의 수선 실패 누적 방지)
            if (_lastPlanningGoalId != Brain.CurrentGoalId)
            {
                _planRepairCount         = 0;
                _replanTriggeredByRepair = false;
            }
            _lastPlanningGoalId = Brain.CurrentGoalId;

            // 이전 플랜 큐 초기화
            Brain.CurrentPlan.Clear();
            Brain.IsExecutingPlan   = false;
            Brain.PlanningStartTime = Time.time;

            // ── 5B단계: GOAPPlannerJob 스케줄링 ──────────────────────────────
            // Brain.CurrentGoalId와 의존성이 모두 유효한 경우에만 스케줄링한다.
            if (Brain.CurrentGoalId != null && _worldState != null && _registry != null)
            {
                _planningContext = GOAPPlannerScheduler.Schedule(
                    Brain.CurrentGoalId,
                    Brain.Role,
                    Brain,
                    _worldState,
                    _registry);

                if (!_planningContext.IsScheduled)
                {
                    // Schedule 내부에서 실패 (null 입력 등) — 이미 LogWarning이 출력됨
                    Debug.LogWarning(
                        $"[VillagerFSM] EnterPlanning: GOAPPlannerScheduler.Schedule() 실패. " +
                        $"다음 Tick에서 Replanning으로 전이됩니다. AgentId={AgentId}"
                    );
                }
            }
            else
            {
                // 의존성 부족: IsScheduled = false인 default 컨텍스트
                _planningContext = default;

                Debug.LogWarning(
                    $"[VillagerFSM] EnterPlanning: 스케줄링 조건 미충족 " +
                    $"(GoalId={Brain.CurrentGoalId ?? "null"}, " +
                    $"worldState={(_worldState != null ? "ok" : "null")}, " +
                    $"registry={(_registry != null ? "ok" : "null")}). " +
                    $"AgentId={AgentId}"
                );
            }
        }

        /// <summary>
        /// [N3b] 다음 액션의 Precondition이 현재 월드 스테이트에서 충족되는지 검사한다.
        /// 충족되면 true, 불충족이면 false (수선 Replanning 트리거용).
        /// 방어 코드: _worldState/_registry가 null이거나 액션 정의를 못 찾으면 true 반환(통과).
        /// </summary>
        private bool CheckNextActionPrec(string nextActionId)
        {
            if (_worldState == null || Brain == null) return true;

            int actionHash = Animator.StringToHash(nextActionId);
            var state   = GOAPStateUtil.BuildCurrentState(_worldState, _registry, Brain, Unity.Collections.Allocator.Temp);
            var defs    = GOAPActionRegistry.BuildActionDefs(Brain.Role, Unity.Collections.Allocator.Temp, 1.0f);

            bool precHolds = true;
            for (int i = 0; i < defs.Length; i++)
            {
                if (defs[i].ActionStringHash == actionHash)
                {
                    precHolds = defs[i].CheckPreconditions(state);
                    break;
                }
            }

            state.Dispose();
            defs.Dispose();
            return precHolds;
        }

        /// <summary>Executing 상태 진입 초기화. 첫 Action을 큐에서 꺼내 실행 시작.</summary>
        private void EnterExecuting()
        {
            Brain.IsExecutingPlan = true;

            if (Brain.CurrentPlan.Count == 0)
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
            Brain.CurrentActionId = Brain.CurrentPlan.Dequeue();
            Brain.ActionStartTime = -1f; // -1f 센티넬: 이동 완료 후 State_Executing()에서 타이머 시작

            // 7단계: Action에 따라 목표 타일로 Brain 위치 이동
            MoveTileForAction(Brain.CurrentActionId);

            // 자원 예약 시도 (Action에 따라 필요한 자원이 다름)
            // 2단계: 간소화된 자원 예약 — 실제 Action별 자원 요구량은 3단계에서 정밀화
            if (!TryReserveForAction(Brain.CurrentActionId))
            {
                // 자원 예약 실패 → Replanning
                Debug.LogWarning($"[VillagerFSM] Action '{Brain.CurrentActionId}' 자원 예약 실패. Replanning으로 전이. AgentId={AgentId}");
                RequestReplanning(AbortReason.ResourceReservationFailed,
                    $"Action={Brain.CurrentActionId} 자원 예약 실패");
                return;
            }

            Debug.Log($"[VillagerFSM] Action 시작: '{Brain.CurrentActionId}'. 완료까지 {ACTION_SIMULATE_SEC}초. AgentId={AgentId}");
        }

        /// <summary>
        /// 방향 ② M3: Fallback 원인을 분류하여 Replanning 상태 전이를 요청한다.
        /// 모든 Fallback 성격의 재계획(수선 제외)은 이 헬퍼를 통해서만 진입한다.
        /// FallbackCounter 직접 증분은 금지 (Brain.FallbackByReason과 sum 불일치 방지).
        /// </summary>
        /// <param name="reason">Fallback 원인 분류. HandleDeadlock 관측 로그에 사용된다.</param>
        /// <param name="detail">디버그 로그용 부가 설명 (null 허용).</param>
        private void RequestReplanning(AbortReason reason, string detail = null)
        {
            Brain.IncrementFallback(reason);
            Debug.Log(
                $"[VillagerFSM] Replanning 요청. reason={reason}, detail={detail ?? "-"}, " +
                $"FallbackCounter={Brain.FallbackCounter}, ByReason=" +
                $"[Plan={Brain.FallbackByReason[0]}," +
                $"Res={Brain.FallbackByReason[1]}," +
                $"Path={Brain.FallbackByReason[2]}," +
                $"Pre={Brain.FallbackByReason[3]}]. AgentId={AgentId}");
            TransitionTo(VillagerState.Replanning);
        }

        /// <summary>Replanning 상태 진입 초기화. 자원 해제 및 쿨다운 설정.</summary>
        private void EnterReplanning()
        {
            AbortCurrentPath();
            // 현재 예약 해제 + 채집 노드 점유 해제 (예약 실패 케이스도 커버)
            ReleaseCurrentReservation();
            ReleaseGatherNode();

            // 현재 플랜 초기화 (CurrentPlanFull도 함께 클리어 — UI 사고 체인 잔류 방지)
            Brain.CurrentPlan.Clear();
            Brain.CurrentPlanFull.Clear();
            Brain.IsExecutingPlan = false;

            // 쿨다운 설정 (P0 Goal은 이 쿨다운을 무시하고 Update()에서 강제 전이)
            Brain.ReplanCooldown      = UnityEngine.Random.Range(REPLAN_COOLDOWN_MIN, REPLAN_COOLDOWN_MAX);
            Brain.LastReplanTimestamp = Time.time;
            // 방향 ② M3: FallbackCounter 증분은 RequestReplanning으로 이관되었다.
            // [N3b] 수선 트리거 리플래닝(_replanTriggeredByRepair)은 어차피 RequestReplanning을 거치지 않으므로
            // 여기서 별도 예외 처리도 불필요.

            // ReplanCount 딕셔너리 갱신
            if (Brain.CurrentGoalId != null)
            {
                if (!Brain.ReplanCount.ContainsKey(Brain.CurrentGoalId))
                    Brain.ReplanCount[Brain.CurrentGoalId] = 0;
                Brain.ReplanCount[Brain.CurrentGoalId]++;
            }

            Debug.Log($"[VillagerFSM] Replanning 진입. FallbackCounter={Brain.FallbackCounter}, Cooldown={Brain.ReplanCooldown:F2}초. AgentId={AgentId}");

            // 사고 말풍선: 리플래닝 원인에 맞는 대사 (7장 Level 3)
            ShowThoughtBubble(GetReplanThought());
        }

        /// <summary>RefusingOrder 상태 진입 초기화. 거부 이유 결정 및 MessageBus를 통해 메시지 발행.</summary>
        private void EnterRefusingOrder()
        {
            // ConflictScoreCalculator에서 현재 Brain 상태 기반으로 거부 이유 코드 결정
            RefusalReasonCode refusalCode = ConflictScoreCalculator.DetermineReason(Brain);
            Brain.RefuseMessageTimer = REFUSE_DISPLAY_SEC;

            // 거부 후 수행할 대안 Goal 결정 (먹기, 치료, 피하기 등)
            Brain.AlternativeGoalId = DetermineAlternativeGoal(refusalCode);

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
                AlternativeGoalId = Brain.AlternativeGoalId,
                LoyaltyLevel      = Brain.LoyaltyLevel
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
                $"[VillagerFSM] 명령 거부. 이유={refusalCode}, 대안Goal={Brain.AlternativeGoalId ?? "없음"}. " +
                $"AgentId={AgentId}"
            );
        }

        /// <summary>
        /// RefusalReasonCode를 사람이 읽을 수 있는 한국어 메시지로 변환한다.
        /// UI 시스템이 OrderRefusedPayload.RefusalMessage를 화면에 직접 표시한다.
        /// TODO: 기획팀 — 로컬라이제이션 키 체계 도입 시 이 메서드를 LocalizationManager로 교체
        /// </summary>
        // ── [Phase 1 F3/F4] 사고 말풍선 대사 풀 (7장 Level 3) ────────────────
        // static readonly: 힙 할당 없이 재사용. GC 부담 0.
        // 리플래닝 대사 (F3)
        private static readonly string[] THOUGHT_HUNGRY_REPLAN  = { "배가 고픈데 계획이 안 잡히네...", "먹을 걸 먼저 찾아야 하나...", "허기져서 집중이 안 돼." };
        private static readonly string[] THOUGHT_TIRED_REPLAN   = { "너무 지쳤어, 다시 생각해야겠어.", "몸이 말을 안 듣는군...", "잠깐 쉬어야 할 것 같아." };
        private static readonly string[] THOUGHT_DANGER_REPLAN  = { "위험해! 다른 방법을 찾아야겠어.", "여기선 안 되겠어. 피해야 해.", "적이 너무 가까워. 계획 변경!" };
        private static readonly string[] THOUGHT_NO_WOOD        = { "나무가 없네... 다른 곳을 찾아볼게.", "여긴 다 베었군. 이동해야겠어.", "벌목할 게 없어. 어디 다른 데 없나?" };
        private static readonly string[] THOUGHT_NO_STONE       = { "돌이 다 떨어졌어. 다시 계획.", "이 채석장은 고갈됐군.", "돌이 없어. 다른 곳을 봐야겠어." };
        private static readonly string[] THOUGHT_NO_FOOD        = { "열매가 없어... 다른 걸 해야겠어.", "여긴 채집할 게 없네.", "식량이 없어. 다른 방법을 찾자." };
        private static readonly string[] THOUGHT_NO_EAT        = { "먹을 게 없다... 어떡하지?", "식사를 못 하겠어. 다른 방법은?", "밥을 구할 수가 없네." };
        private static readonly string[] THOUGHT_REPLAN_DEFAULT = { "계획을 다시 세워야겠어.", "이 방법은 안 되겠어. 다시 생각해보자.", "뭔가 잘못됐어. 처음부터 다시." };
        private static readonly string[] THOUGHT_PLAN_REPAIR    = { "어라, 상황이 바뀌었네. 다시 생각해보자.", "누가 먼저 썼나... 다시 계획해야겠어.", "조건이 달라졌어. 재계획!" };

        // Goal 전환 대사 (F4)
        private static readonly string[] THOUGHT_GOAL_GATHER  = { "창고가 비어가네. 일하러 가자.", "자원이 부족해. 채집에 나서야겠어.", "이대로면 큰일나. 일단 자원부터." };
        private static readonly string[] THOUGHT_GOAL_BUILD   = { "재료가 모였으니 짓기 시작하자.", "이제 건설을 시작할 때야.", "준비가 됐어. 짓자!" };
        private static readonly string[] THOUGHT_GOAL_EXPLORE = { "여유가 생겼으니 주변을 둘러볼까.", "미지의 지역이 있어. 탐험해보자.", "지도에 빈 곳이 많아. 가봐야겠어." };
        private static readonly string[] THOUGHT_P0_HUNGER    = { "안 되겠다, 뭐라도 먹어야 해.", "배가 너무 고파. 쓰러지기 전에!", "먹는 게 최우선이야. 지금 당장!" };
        private static readonly string[] THOUGHT_P0_FATIGUE   = { "잠깐... 좀 쉬어야겠어.", "탈진하기 전에 쉬어야 해.", "몸이 한계야. 지금 쉬지 않으면 안 돼." };

        // ── [F-A] 성격별 대사 (재미 로드맵 P0) ────────────────────────────────
        // 사용 규칙: Goal 전환 지점에서 PERSONALITY_LINE_CHANCE 확률로 goal 라인 대신 발화.
        // Fighting 진입은 확률 무시 (Coward/Brave 성격이면 항상 발화).
        // 스로틀은 기존 THOUGHT_MIN_INTERVAL_SEC(5s)를 공유 — 별도 카운터 추가 금지 (⚠️ 명세 §4 FA-5).
        private const float PERSONALITY_LINE_CHANCE = 0.5f;
        private static readonly string[] THOUGHT_PERS_COWARD_COMBAT = { "무서워… 못 싸워!", "도망칠래!", "이런 건 나 못 해!" };
        private static readonly string[] THOUGHT_PERS_BRAVE_COMBAT  = { "덤벼!", "지지 않아!", "겁먹은 놈은 내가 처리한다." };
        private static readonly string[] THOUGHT_PERS_DILIGENT_WORK = { "일이 최고야.", "쉬는 게 오히려 답답해.", "부지런히 움직이자!" };
        private static readonly string[] THOUGHT_PERS_LAZY_WORK     = { "잠깐만 쉬었다 하자…", "귀찮아…", "왜 나만 시키지…" };
        private static readonly string[] THOUGHT_PERS_GLUTTON_HUNGRY= { "배고파! 먹을 거 없어?", "밥! 밥!", "이 배고픔은 못 참지." };
        private static readonly string[] THOUGHT_PERS_CURIOUS_EXPLR = { "저 너머엔 뭐가 있을까?", "가보자!", "새로운 게 보고 싶어." };

        // ── [F-B] 침략 예고 반응 대사 (ADR-B6: Coward/Brave만 전용, 나머지 Default) ─
        // 사용 규칙: MessageBus.InvasionWarning 수신 시 스테이지·성격 조합으로 풀 선택 → ShowThoughtBubble.
        // 스로틀·LOD·FallbackCounter 게이트를 기존 ShowThoughtBubble이 공유(⚠️ 명세 §4 FB-5).
        private static readonly string[] THOUGHT_INVASION_RUMOR_DEFAULT = { "…뭔가 이상해.", "공기가 무거워졌어.", "왜 이리 조용하지?" };
        private static readonly string[] THOUGHT_INVASION_RUMOR_COWARD  = { "뭐가… 온다…", "숨을 데 없나…", "무서워…" };
        private static readonly string[] THOUGHT_INVASION_RUMOR_BRAVE   = { "덤빌 테면 덤벼봐라!", "무기 챙겨두자.", "예감이 안 좋군." };
        private static readonly string[] THOUGHT_INVASION_CONF_DEFAULT  = { "온다!", "각오해야 해.", "드디어 오는군!" };
        private static readonly string[] THOUGHT_INVASION_CONF_COWARD   = { "안 돼… 안 돼!", "도망갈래!", "숨을 곳! 어디!" };
        private static readonly string[] THOUGHT_INVASION_CONF_BRAVE    = { "드디어 오는군!", "지지 않는다!", "각오는 됐다!" };

        /// <summary>
        /// F-A: Goal/상태 전환 지점에서 발화할 성격 대사를 확률적으로 선택한다.
        /// 성격이 매치되지 않거나 확률 실패 시 null 반환 → 호출자는 기존 goal 라인으로 폴백.
        /// alwaysSpeak=true는 Fighting/Fleeing 진입 등 성격 표현이 최우선인 경우.
        /// </summary>
        private string TryPickPersonalityLine(string ctx, bool alwaysSpeak = false)
        {
            if (Brain.Personality == Personality.None) return null;
            if (!alwaysSpeak && UnityEngine.Random.value >= PERSONALITY_LINE_CHANCE) return null;

            switch (ctx)
            {
                case "gather":
                    if (Brain.Personality == Personality.Diligent) return Pick(THOUGHT_PERS_DILIGENT_WORK);
                    if (Brain.Personality == Personality.Lazy)     return Pick(THOUGHT_PERS_LAZY_WORK);
                    return null;
                case "explore":
                    if (Brain.Personality == Personality.Curious)  return Pick(THOUGHT_PERS_CURIOUS_EXPLR);
                    return null;
                case "p0_hunger":
                    if (Brain.Personality == Personality.Glutton)  return Pick(THOUGHT_PERS_GLUTTON_HUNGRY);
                    return null;
                case "fighting":
                    if (Brain.Personality == Personality.Brave)    return Pick(THOUGHT_PERS_BRAVE_COMBAT);
                    if (Brain.Personality == Personality.Coward)   return Pick(THOUGHT_PERS_COWARD_COMBAT);
                    return null;
                default:
                    return null;
            }
        }

        /// <summary>풀에서 무작위로 대사 하나를 선택한다. 연속 중복을 방지하기 위해 마지막 인덱스를 추적한다.</summary>
        private int _lastPickIndex = -1;
        private string Pick(string[] pool)
        {
            if (pool.Length == 1) return pool[0];
            int idx;
            do { idx = UnityEngine.Random.Range(0, pool.Length); }
            while (idx == _lastPickIndex && pool.Length > 1);
            _lastPickIndex = idx;
            return pool[idx];
        }

        /// <summary>리플래닝 상황에 맞는 대사를 랜덤 풀에서 반환한다.</summary>
        private string GetReplanThought()
        {
            if (Brain.SatietyLevel < 25f) return Pick(THOUGHT_HUNGRY_REPLAN);
            if (Brain.FatigueLevel > 80f) return Pick(THOUGHT_TIRED_REPLAN);
            if (Brain.NearEnemy) return Pick(THOUGHT_DANGER_REPLAN);

            switch (Brain.CurrentActionId)
            {
                case "ChopWood":           return Pick(THOUGHT_NO_WOOD);
                case "MineStone":          return Pick(THOUGHT_NO_STONE);
                case "HarvestWildBerries": return Pick(THOUGHT_NO_FOOD);
                case "EatCookedFood":
                case "EatRawFood":         return Pick(THOUGHT_NO_EAT);
                default:                   return Pick(THOUGHT_REPLAN_DEFAULT);
            }
        }

        /// <summary>주민 머리 위 WorldSpace 아이콘에 사고 텍스트를 2.5초 표시한다.
        /// 3중 게이트(시간 스로틀·LOD 생략·FallbackCounter 억제)로 스팸을 방지한다.</summary>
        private void ShowThoughtBubble(string thought)
        {
            // 게이트 1: 주민당 시간 스로틀 (5초 미만 재발화 차단)
            if (Time.time - _lastThoughtTime < THOUGHT_MIN_INTERVAL_SEC) return;

            // 게이트 2: LOD 주민은 발화 생략 (화면 밖 원거리 — 정보 가치 없음)
            if (Brain.FSMState == VillagerState.LOD_FSM) return;

            // 게이트 3: 연속 실패 억제 — FallbackCounter 2 이상이면 Deadlock 핸들러에 위임
            if (Brain.FallbackCounter >= 2) return;

            _lastThoughtTime = Time.time;
            _thoughtBubbleCount++;
            var icon = GetComponentInChildren<AIVillage.UI.VillagerActionIcon>();
            if (icon != null)
                icon.ShowThought(thought, 2.5f);
        }

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
        /// Fighting 상태 진입 초기화. 현재 플랜을 중단하고 전투 준비 상태로 전환한다.
        /// CombatMentalState는 진입 전 이미 설정된 값(Normal/Rage)을 유지한다.
        /// </summary>
        private void EnterFighting()
        {
            AbortCurrentPath();
            Brain.CurrentGoalId   = null;
            Brain.CurrentPlan.Clear();
            Brain.IsExecutingPlan = false;

            // 사고 말풍선: 성격 라인 우선, 없으면 위기 대사로 폴백. AnyState/Idle 어느 경로에서 진입해도 표시된다.
            string persLine = TryPickPersonalityLine("fighting", alwaysSpeak: true);
            ShowThoughtBubble(persLine ?? Pick(THOUGHT_DANGER_REPLAN));

            Debug.Log($"[VillagerFSM] → Fighting ({Brain.CombatMentalState}). AgentId={AgentId}");
        }

        /// <summary>
        /// Fleeing 상태 진입 초기화. 현재 플랜을 중단하고 기지 방향 도주를 시작한다.
        /// CombatMentalState는 Fear로 이미 설정된 상태에서 진입한다.
        /// </summary>
        private void EnterFleeing()
        {
            Brain.CurrentGoalId   = null;
            Brain.CurrentPlan.Clear();
            Brain.IsExecutingPlan = false;
            // 방향 ② M2: 기지 unreachable 시 좌표 스냅 없이 로그만 남긴다.
            // 기지가 도달 불가하면 도망 자체가 불가 — 별도 분기 처리는 스코프 밖.
            if (StartPathTo(_baseTileX, _baseTileY) == StartPathOutcome.Unreachable)
            {
                Debug.LogWarning(
                    $"[VillagerFSM] Fleeing 진입 실패: 기지 unreachable. " +
                    $"AgentId={AgentId}, base=({_baseTileX},{_baseTileY})");
            }
            Debug.Log($"[VillagerFSM] → Fleeing (HP={Brain.HealthLevel:F0}). AgentId={AgentId}");
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
            AbortCurrentPath();
            // 1. 생존 플래그 false 확정
            Brain.IsAlive         = false;
            // 2. 플랜 실행 중지
            Brain.IsExecutingPlan = false;
            Brain.CurrentPlan.Clear();

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
            // Brain 인벤토리 플래그를 읽어 실제 드롭된 아이템을 기록한다.
            // DropInventoryItems()가 이미 _worldState.DroppedItems에 추가했으므로
            // 여기서는 메시지 페이로드 전달용으로만 사용한다.

            // [PR Fix R-003]: 초기 용량을 4로 지정하여 내부 배열 재할당을 방지한다.
            // 드롭 가능한 아이템 종류는 HasTool, HasWeapon, HasPrimitiveWeapon, HasFood 4종으로 고정이다.
            // 용량 힌트를 주지 않으면 기본 용량(4)에서 Add 시 8 → 16 식으로 재할당이 발생할 수 있다.
            var droppedItemList = new List<MessageBus.DroppedItemInfo>(4);

            if (Brain.HasTool)
            {
                // 역할에 따라 도끼 또는 곡괭이
                ItemType toolType = DetermineToolType(Brain.Role);
                droppedItemList.Add(new MessageBus.DroppedItemInfo
                {
                    ItemType = toolType,
                    TileX    = Brain.TileX,
                    TileY    = Brain.TileY
                });
            }

            if (Brain.HasWeapon)
            {
                droppedItemList.Add(new MessageBus.DroppedItemInfo
                {
                    ItemType = ItemType.Weapon,
                    TileX    = Brain.TileX,
                    TileY    = Brain.TileY
                });
            }

            if (Brain.HasPrimitiveWeapon)
            {
                droppedItemList.Add(new MessageBus.DroppedItemInfo
                {
                    ItemType = ItemType.PrimitiveWeapon,
                    TileX    = Brain.TileX,
                    TileY    = Brain.TileY
                });
            }

            if (Brain.HasFood)
            {
                droppedItemList.Add(new MessageBus.DroppedItemInfo
                {
                    ItemType = ItemType.Food,
                    TileX    = Brain.TileX,
                    TileY    = Brain.TileY
                });
            }

            // ── VillagerDiedPayload 구성 ──────────────────────────────────────
            var diedPayload = new MessageBus.VillagerDiedPayload
            {
                VillagerId        = AgentId,
                DeathTileX        = Brain.TileX,
                DeathTileY        = Brain.TileY,
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
                    $"AgentId={AgentId}, Tile=({Brain.TileX},{Brain.TileY}), " +
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
            if (Brain == null) return false;
            return Brain.HealthLevel  < P0_INJURY_TRIGGER_HEALTH
                || Brain.SatietyLevel < P0_HUNGER_TRIGGER_SATIETY
                || Brain.FatigueLevel > P0_FATIGUE_TRIGGER_FATIGUE;
        }

        /// <summary>
        /// 현재 활성화된 P0 Goal ID를 반환한다.
        /// 우선순위: SurviveInjury > SurviveHunger > SurviveFatigue
        /// </summary>
        private string GetP0GoalId()
        {
            if (Brain.HealthLevel  < P0_INJURY_TRIGGER_HEALTH)   return "SurviveInjury";
            if (Brain.SatietyLevel < P0_HUNGER_TRIGGER_SATIETY)  return "SurviveHunger";
            if (Brain.FatigueLevel > P0_FATIGUE_TRIGGER_FATIGUE) return "SurviveFatigue";
            return null;
        }

        /// <summary>
        /// 어떤 자원이든 30 미만으로 부족한지 확인한다.
        /// 실제 확인은 WorldState의 가용량 기준 (예약 포함).
        /// </summary>
        private bool IsAnyStockLow()
        {
            if (_worldState == null || _registry == null) return false;

            // CookedFood는 Campfire가 없으면 0으로 고정 → 영구 true 방지를 위해 제외
            // 조리 식량 부족은 별도 CookFood Goal에서 처리 (Campfire 건설 후)
            return _registry.GetAvailable(ResourceType.RawFood) < GATHER_STOCK_LOW_THRESHOLD
                || _registry.GetAvailable(ResourceType.Wood)    < GATHER_STOCK_LOW_THRESHOLD
                || _registry.GetAvailable(ResourceType.Stone)   < GATHER_STOCK_LOW_THRESHOLD;
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
        /// [방향 ③ P1] 임계값 미만 자원 중 가장 부족한 것의 구체 Goal ID를 반환한다.
        /// 미발견 자원은 후보에서 제외되어 무해 Goal이 플래너로 넘어가지 않는다.
        /// 발견 판정: IsDiscovered && CurrentAmount>0 (GOAPPlannerScheduler.cs:610과 동일 계약, ADR-③-3).
        /// SensorSystem 없으면 5개 discovered 전부 false로 안전 폴백.
        /// </summary>
        private string SelectGatherGoalId()
        {
            if (_registry == null) return null;

            bool woodDisc = false, stoneDisc = false, ironDisc = false,
                 copperDisc = false, foodDisc = false;
            var nodes = SensorSystem.Instance?.GetAllNodes();
            if (nodes != null)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    var n = nodes[i];
                    if (!n.IsDiscovered || n.CurrentAmount <= 0f) continue;
                    switch (n.ResourceType)
                    {
                        case ResourceType.Wood:    woodDisc   = true; break;
                        case ResourceType.Stone:   stoneDisc  = true; break;
                        case ResourceType.Iron:    ironDisc   = true; break;
                        case ResourceType.Copper:  copperDisc = true; break;
                        case ResourceType.RawFood: foodDisc   = true; break;
                    }
                }
            }

            return GatherGoalSelector.Select(
                _registry.GetAvailable(ResourceType.Wood),
                _registry.GetAvailable(ResourceType.Stone),
                _registry.GetAvailable(ResourceType.Iron),
                _registry.GetAvailable(ResourceType.Copper),
                _registry.GetAvailable(ResourceType.RawFood),
                Brain != null && Brain.HasTool,
                woodDisc, stoneDisc, ironDisc, copperDisc, foodDisc);
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
            return Mathf.Abs(Brain.TileX - _baseTileX) + Mathf.Abs(Brain.TileY - _baseTileY);
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

            // 채집 노드 점유 해제 (완료 후 다른 주민이 사용 가능하도록)
            ReleaseGatherNode();

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

                        // Brain 수치: 포만감 증가 (상한 100 클램프)
                        // ReduceHunger effect의 Amount는 "배고픔 해소량" = "포만감 증가량"으로 재해석한다.
                        case ActionEffectType.ReduceHunger:
                            Brain.SatietyLevel = Mathf.Min(100f, Brain.SatietyLevel + effect.Amount);
                            break;

                        // Brain 수치: 피로 감소 — 땅에서 쉬기 (20 감소)
                        case ActionEffectType.ReduceFatigue:
                            Brain.FatigueLevel = Mathf.Max(0f, Brain.FatigueLevel - effect.Amount);
                            break;

                        // Brain 수치: 피로 회복 — 수면 (90 감소)
                        // ReduceFatigue와 수식은 같지만 GameDesign상 의미가 다르므로 분리
                        case ActionEffectType.RestoreFatigue:
                            Brain.FatigueLevel = Mathf.Max(0f, Brain.FatigueLevel - effect.Amount);
                            break;

                        // Brain 수치: 체력 회복 (상한 100 클램프)
                        case ActionEffectType.GainHealth:
                            Brain.HealthLevel = Mathf.Min(100f, Brain.HealthLevel + effect.Amount);
                            break;

                        // Brain 수치: 피로 증가 (수집/전투 부작용, 상한 100 클램프)
                        case ActionEffectType.IncreaseFatigue:
                            Brain.FatigueLevel = Mathf.Min(100f, Brain.FatigueLevel + effect.Amount);
                            break;

                        // Brain 수치: 기분 향상
                        case ActionEffectType.GainMood:
                            Brain.MoodLevel = Mathf.Min(100f, Brain.MoodLevel + effect.Amount);
                            break;

                        // Brain 위치 플래그: 기지 도착 완료
                        case ActionEffectType.SetAtBase:
                            Brain.AtBase = true;
                            break;

                        // Brain 환경 플래그: 모닥불 근처 (건설 완료 후)
                        case ActionEffectType.SetNearFireplace:
                            Brain.NearFireplace = true;
                            break;

                        // WorldState 건물 완료 플래그
                        case ActionEffectType.SetCampfireBuilt:
                            // 모닥불 완료: NearFireplace는 SensorSystem이 갱신
                            Brain.NearFireplace = true;
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
                            Brain.HasTool = true;
                            break;

                        case ActionEffectType.SetHasPrimitiveWeapon:
                            Brain.HasPrimitiveWeapon = true;
                            break;

                        case ActionEffectType.SetHasWeapon:
                            Brain.HasWeapon = true;
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
                    Brain.SatietyLevel = Mathf.Min(100f, Brain.SatietyLevel + GOAPActionRegistry.EAT_HUNGER_RELIEF); // ADR-7
                    Brain.MoodLevel    = Mathf.Min(100f, Brain.MoodLevel    + 5f);
                    break;
                case "EatRawFood":
                    Brain.SatietyLevel = Mathf.Min(100f, Brain.SatietyLevel + GOAPActionRegistry.EAT_RAW_RELIEF); // ADR-7
                    break;
                case "Sleep":
                    Brain.FatigueLevel = Mathf.Max(0f, Brain.FatigueLevel - SLEEP_FATIGUE_RECOVERY);
                    break;
                case "RestOnGround":
                    Brain.FatigueLevel = Mathf.Max(0f, Brain.FatigueLevel - REST_ON_GROUND_FATIGUE_RECOVERY);
                    break;
                case "SeekMedicalAid":
                    Brain.HealthLevel  = Mathf.Min(100f, Brain.HealthLevel + GOAPActionRegistry.MEDICAL_HEALTH_GAIN); // ADR-7
                    break;
                case "ChopWood":
                    if (_worldState != null) _worldState.WoodStock  += GOAPActionRegistry.YIELD_CHOP_WOOD; // ADR-7
                    Brain.FatigueLevel = Mathf.Min(100f, Brain.FatigueLevel + 10f);
                    break;
                case "MineStone":
                    if (_worldState != null) _worldState.StoneStock += GOAPActionRegistry.YIELD_MINE_STONE; // ADR-7
                    Brain.FatigueLevel = Mathf.Min(100f, Brain.FatigueLevel + 15f);
                    break;
                case "MineIron":
                    if (_worldState != null) _worldState.IronStock  += GOAPActionRegistry.YIELD_MINE_IRON; // ADR-7
                    Brain.FatigueLevel = Mathf.Min(100f, Brain.FatigueLevel + 15f);
                    break;
                case "CookMeal":
                    if (_worldState != null) _worldState.CookedFoodStock += GOAPActionRegistry.COOK_YIELD; // ADR-7
                    break;
                case "AttackEnemy":
                    Brain.FatigueLevel = Mathf.Min(100f, Brain.FatigueLevel + 20f);
                    break;
                case "CraftPrimitiveWeapon":
                case "CraftWeapon":
                    Brain.HasWeapon = true;
                    break;
                case "Explore":
                    Brain.FatigueLevel = Mathf.Min(100f, Brain.FatigueLevel + 5f); // 기획서 수치: fatigue += 5
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
                case "BuildHouse":
                    if (_worldState != null) _worldState.HouseBuilt = true;
                    break;
                case "BuildWatchtower":
                    if (_worldState != null) _worldState.WatchtowerBuilt = true;
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
            if (Brain.HasTool)
            {
                var droppedTool = new DroppedItem
                {
                    ItemId                  = Guid.NewGuid().ToString(),
                    ItemType                = DetermineToolType(Brain.Role),
                    TileX                   = Brain.TileX,
                    TileY                   = Brain.TileY,
                    OriginalOwnerVillagerId = AgentId
                };
                _worldState.DroppedItems.Add(droppedTool);
                Debug.Log($"[VillagerFSM] 도구 드롭: {droppedTool.ItemType} at ({Brain.TileX},{Brain.TileY}). AgentId={AgentId}");
            }

            // 무기 드롭
            if (Brain.HasWeapon)
            {
                var droppedWeapon = new DroppedItem
                {
                    ItemId                  = Guid.NewGuid().ToString(),
                    ItemType                = ItemType.Weapon,
                    TileX                   = Brain.TileX,
                    TileY                   = Brain.TileY,
                    OriginalOwnerVillagerId = AgentId
                };
                _worldState.DroppedItems.Add(droppedWeapon);
            }

            // 원시 무기 드롭
            if (Brain.HasPrimitiveWeapon)
            {
                var droppedPrimWeapon = new DroppedItem
                {
                    ItemId                  = Guid.NewGuid().ToString(),
                    ItemType                = ItemType.PrimitiveWeapon,
                    TileX                   = Brain.TileX,
                    TileY                   = Brain.TileY,
                    OriginalOwnerVillagerId = AgentId
                };
                _worldState.DroppedItems.Add(droppedPrimWeapon);
            }

            // 식량 드롭
            if (Brain.HasFood)
            {
                var droppedFood = new DroppedItem
                {
                    ItemId                  = Guid.NewGuid().ToString(),
                    ItemType                = ItemType.Food,
                    TileX                   = Brain.TileX,
                    TileY                   = Brain.TileY,
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

            switch (Brain.Role)
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
        // ── [8단계] 전투 헬퍼 메서드 ──────────────────────────────────────────

        /// <summary>
        /// 현재 틱의 목격 이벤트와 체력을 기반으로 전투 심리 상태를 갱신한다.
        /// 호출 후 RecentAllyDeathWitness, RecentRageKillWitness를 0으로 초기화한다.
        /// </summary>
        private void EvaluateCombatMentalState()
        {
            if (Brain.HealthLevel < FEAR_HEALTH_OVERRIDE)
            {
                Brain.CombatMentalState = CombatMentalState.Fear;
                Brain.AttackModifier    = 1.0f;
                Brain.RecentAllyDeathWitness = 0;
                Brain.RecentRageKillWitness  = 0;
                return;
            }

            switch (Brain.CombatMentalState)
            {
                case CombatMentalState.Normal:
                    if (Brain.RecentAllyDeathWitness > 0)
                    {
                        if (Brain.HealthLevel < 50f)
                        {
                            Brain.CombatMentalState = CombatMentalState.Fear;
                            Brain.AttackModifier    = 1.0f;
                            Debug.Log($"[VillagerFSM] Normal→Fear (HP={Brain.HealthLevel:F0}). AgentId={AgentId}");
                        }
                        else
                        {
                            Brain.CombatMentalState = CombatMentalState.Rage;
                            Brain.AttackModifier    = RAGE_ATTACK_MODIFIER;
                            Brain.RageTimer         = RAGE_DURATION_SEC;
                            Debug.Log($"[VillagerFSM] Normal→Rage (HP={Brain.HealthLevel:F0}). AgentId={AgentId}");
                        }
                    }
                    else if (Brain.RecentRageKillWitness > 0 && Brain.HealthLevel >= 40f)
                    {
                        Brain.CombatMentalState = CombatMentalState.Rage;
                        Brain.AttackModifier    = RAGE_ATTACK_MODIFIER;
                        Brain.RageTimer         = RAGE_DURATION_SEC;
                        Debug.Log($"[VillagerFSM] Normal→Rage 전염 (HP={Brain.HealthLevel:F0}). AgentId={AgentId}");
                    }
                    break;

                case CombatMentalState.Fear:
                    if (Brain.RecentRageKillWitness > 0 && Brain.HealthLevel >= FEAR_RAGE_REVERSE_HEALTH)
                    {
                        Brain.CombatMentalState = CombatMentalState.Rage;
                        Brain.AttackModifier    = RAGE_ATTACK_MODIFIER;
                        Brain.RageTimer         = RAGE_DURATION_SEC;
                        Debug.Log($"[VillagerFSM] Fear→Rage 역전 (HP={Brain.HealthLevel:F0}). AgentId={AgentId}");
                    }
                    else if (Brain.NearbyEnemyCount == 0 && Brain.HealthLevel >= FEAR_RECOVERY_HEALTH)
                    {
                        Brain.CombatMentalState = CombatMentalState.Normal;
                        Brain.AttackModifier    = 1.0f;
                        Debug.Log($"[VillagerFSM] Fear→Normal 회복 (HP={Brain.HealthLevel:F0}). AgentId={AgentId}");
                    }
                    break;

                case CombatMentalState.Rage:
                    // 주의: 진입 첫 틱에도 즉시 차감되므로 실효 Rage 지속 시간은
                    // RAGE_DURATION_SEC - RAGE_TIMER_TICK_DELTA(≈7.4초) ~ RAGE_DURATION_SEC(8초) 범위임.
                    Brain.RageTimer -= RAGE_TIMER_TICK_DELTA;
                    if (Brain.RageTimer <= 0f)
                    {
                        Brain.CombatMentalState = CombatMentalState.Normal;
                        Brain.AttackModifier    = 1.0f;
                        Debug.Log($"[VillagerFSM] Rage→Normal (타이머 만료). AgentId={AgentId}");
                    }
                    break;
            }

            Brain.RecentAllyDeathWitness = 0;
            Brain.RecentRageKillWitness  = 0;
        }

        private EnemyFSM FindNearestEnemy(float rangeTiles)
        {
            if (GameManager.Instance == null) return null;

            EnemyFSM nearest = null;
            float minDist = float.MaxValue;

            foreach (EnemyFSM e in GameManager.Instance.Enemies)
            {
                if (e == null || e.Brain == null || !e.Brain.IsAlive) continue;
                float dist = Mathf.Abs(e.Brain.TileX - Brain.TileX)
                           + Mathf.Abs(e.Brain.TileY - Brain.TileY);
                if (dist <= rangeTiles && dist < minDist)
                {
                    minDist = dist;
                    nearest = e;
                }
            }
            return nearest;
        }

        private void PublishRageKillEvent()
        {
            if (MessageBus.Instance == null) return;

            MessageBus.Instance.Publish(new AIMessage
            {
                Type     = MessageType.RageKill,
                Priority = MessagePriority.High,
                SenderId = AgentId,
                Payload  = new MessageBus.RageKillPayload
                {
                    KillerVillagerId = AgentId,
                    KillTileX        = Brain.TileX,
                    KillTileY        = Brain.TileY
                },
                IssuedAt = Time.time
            });
        }

        /// <summary>
        /// Deadlock 상태 처리: needsHelp 플래그 설정 및 강제 Fallback Goal 실행.
        /// fallbackCounter >= DEADLOCK_THRESHOLD일 때 Replanning 핸들러에서 호출된다.
        /// </summary>
        private void HandleDeadlock()
        {
            Brain.NeedsHelp = true;

            // [PR Fix R-007]: 기존 폴백 로직(SatietyLevel > 50 → RestOnGround)은 P0 생존 위기가
            // 이미 실패한 Deadlock 상황에서 부적절하다. 허기가 낮다고 RestOnGround로 가면
            // 오히려 Goal을 잃고 표류할 위험이 크다.
            // 기본 폴백을 MoveToBase로 설정하고, P0 Goal이 활성 상태이면 해당 Goal을 우선 사용한다.
            // P0 Goal이 이미 실패하여 Deadlock에 빠진 경우에도 기지 복귀가 가장 안전한 행동이다.
            // Deadlock = 동일 Goal이 DEADLOCK_THRESHOLD(3)회 연속 실패한 상태.
            // P0 Goal이 활성이어도 이미 3번 실패했으므로 재시도하면 동일 루프 반복.
            // MoveToBase는 전제조건이 AtBase=0→1 단 하나 (또는 alreadySatisfied)로
            // 어떤 상황에서도 풀이가 존재하는 가장 안전한 폴백이다.
            string fallbackGoal = "MoveToBase";

            Debug.LogWarning($"[VillagerFSM] Deadlock 감지! FallbackCounter={Brain.FallbackCounter}, " +
                             $"ByReason=[Plan={Brain.FallbackByReason[0]}," +
                             $"Res={Brain.FallbackByReason[1]}," +
                             $"Path={Brain.FallbackByReason[2]}," +
                             $"Pre={Brain.FallbackByReason[3]}]. " +
                             $"NeedsHelp=true. FallbackGoal={fallbackGoal}. AgentId={AgentId}");

            // FallbackCounter + FallbackByReason 함께 초기화 (무한 루프 방지)
            Brain.ResetFallback();
            Brain.CurrentGoalId    = fallbackGoal;
            Brain.ReplanCooldown   = 0f;

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
                    if (Brain.FSMState == VillagerState.LOD_FSM)
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
                    if (Brain.FSMState == VillagerState.Executing
                        && (Brain.CurrentActionId == "ChopWood"
                            || Brain.CurrentActionId == "MineStone"))
                    {
                        UnityEngine.Debug.Log(
                            $"[VillagerFSM] 자원 고갈 메시지 수신 → Replanning. " +
                            $"Action={Brain.CurrentActionId}, AgentId={AgentId}"
                        );
                        RequestReplanning(AbortReason.ActionPreconditionInvalidated,
                            $"자원 고갈, Action={Brain.CurrentActionId}");
                    }
                    break;

                case MessageType.VillagerDied:
                    Brain.MoodLevel    = Mathf.Max(0f, Brain.MoodLevel    - 5f);
                    Brain.LoyaltyLevel = Mathf.Max(0f, Brain.LoyaltyLevel - 2f);
                    // [8단계] Fear 전염: 5타일 이내 아군 사망 목격
                    if (msg.Payload is MessageBus.VillagerDiedPayload diedPayload)
                    {
                        float dist = Mathf.Abs(Brain.TileX - diedPayload.DeathTileX)
                                   + Mathf.Abs(Brain.TileY - diedPayload.DeathTileY);
                        if (dist <= FEAR_CONTAGION_RADIUS)
                            Brain.RecentAllyDeathWitness = Mathf.Min(1, Brain.RecentAllyDeathWitness + 1);
                    }
                    break;

                case MessageType.RageKill:
                    // [8단계] GameManager.OnRageKill()이 이미 RAGE_CONTAGION_RADIUS 이내 주민만
                    // ReceiveMessage()로 전달했으므로 거리 재확인 없이 카운터만 증가시킨다.
                    if (Brain != null && Brain.IsAlive)
                        Brain.RecentRageKillWitness = Mathf.Min(1, Brain.RecentRageKillWitness + 1);
                    break;

                default:
                    break;
            }
        }

        // SimulatePlanResult()는 5B단계에서 GOAPPlannerScheduler + GOAPPlannerJob으로 교체됨.
        // EnterPlanning()이 Job을 스케줄하고 State_Planning()이 완료를 폴링한다.

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
            switch (Brain.Role)
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
        /// F-B: GameObject 활성화 시 MessageBus.InvasionWarning 구독.
        /// LOD SetActive 사이클에서 자동 재구독되도록 Awake가 아닌 OnEnable에 둔다.
        /// MessageBus.Subscribe는 자체 중복 방지가 있어 재구독 안전.
        /// </summary>
        private void OnEnable()
        {
            if (MessageBus.Instance != null)
            {
                MessageBus.Instance.Subscribe(MessageType.InvasionWarning, OnInvasionWarning);
            }
        }

        /// <summary>
        /// F-B: 대칭 Unsubscribe. OnDestroy가 아니라 OnDisable에서 해제하는 이유는
        /// LOD 비활성 상태에서 예고 알림에 반응하지 않도록 하기 위함.
        /// </summary>
        private void OnDisable()
        {
            if (MessageBus.Instance != null)
            {
                MessageBus.Instance.Unsubscribe(MessageType.InvasionWarning, OnInvasionWarning);
            }
        }

        /// <summary>
        /// F-B: 침략 예고 수신 콜백. 스테이지·성격 조합으로 대사 풀 선택 후 ShowThoughtBubble.
        /// ShowThoughtBubble이 THOUGHT_MIN_INTERVAL_SEC(5s) 스로틀·LOD 게이트·FallbackCounter
        /// 억제를 담당하므로 여기서는 별도 스로틀 추가 금지 (⚠️ 명세 §4 FB-5 오해 위험).
        /// </summary>
        private void OnInvasionWarning(AIMessage msg)
        {
            if (!(msg.Payload is MessageBus.InvasionWarningPayload p)) return;
            if (Brain == null) return;

            string[] pool = SelectInvasionThoughtPool(p.StageId, Brain.Personality);
            if (pool == null || pool.Length == 0) return;

            ShowThoughtBubble(Pick(pool));
        }

        /// <summary>
        /// F-B ADR-B6: 침략 예고 스테이지·성격 조합으로 대사 풀 선택.
        /// Coward/Brave만 전용 풀, 그 외 성격(Diligent/Lazy/Glutton/Curious/None)은 Default 풀 공유.
        /// 6종 모두 전용 대사는 스코프 팽창(관성 실험 신호 흐림) — F-D 이후 확장.
        /// </summary>
        private static string[] SelectInvasionThoughtPool(string stageId, Personality p)
        {
            bool confirmed = (stageId == MessageBus.INVASION_STAGE_CONFIRMED);
            switch (p)
            {
                case Personality.Coward:
                    return confirmed ? THOUGHT_INVASION_CONF_COWARD  : THOUGHT_INVASION_RUMOR_COWARD;
                case Personality.Brave:
                    return confirmed ? THOUGHT_INVASION_CONF_BRAVE   : THOUGHT_INVASION_RUMOR_BRAVE;
                default:
                    return confirmed ? THOUGHT_INVASION_CONF_DEFAULT : THOUGHT_INVASION_RUMOR_DEFAULT;
            }
        }

        /// <summary>
        /// Unity가 이 컴포넌트를 Destroy할 때 호출한다.
        ///
        /// [PR Fix F-007]: 진행 중인 DeactivateAfterDelay 코루틴을 명시적으로 중지하여
        /// 씬 전환이나 오브젝트 파괴 시 고아 코루틴(orphaned coroutine)이 발생하지 않도록 한다.
        ///
        /// 5B단계 추가: 진행 중인 GOAPPlannerJob이 있으면 Complete() 후 NativeArray를 해제한다.
        /// Job을 Complete() 없이 Destroy하면 Unity가 NativeArray 누수 오류를 출력한다.
        /// </summary>
        private void OnDestroy()
        {
            // ── 5B단계: 진행 중인 Planning Job 안전 종료 ──────────────────────
            // IsScheduled가 true이면 NativeArray가 살아있으므로 반드시 Complete+Dispose한다.
            if (_planningContext.IsScheduled)
            {
                _planningContext.JobHandle.Complete();
                _planningContext.Dispose();
            }

            // ── 고아 코루틴 방지 ──────────────────────────────────────────────
            if (_deactivateCoroutine != null)
            {
                StopCoroutine(_deactivateCoroutine);
                _deactivateCoroutine = null;
            }
        }

        #endregion

        #region ── [12단계] 경로 이동 헬퍼 ──

        // ── [방향 ② M2] 이동 실패 first-class 승격 ──────────────────────────
        // StartPathTo의 반환값. 호출자는 세 케이스를 명시 분기해야 한다.
        // - Started       : Waypoint 이동 개시
        // - AlreadyThere  : 시작 == 목표. 조용히 성공 취급 (ADR-M3)
        // - Unreachable   : 경로 없음. 호출자가 Replanning으로 위임 (좌표 스냅 금지, ADR-M4)
        private enum StartPathOutcome
        {
            Started      = 0,
            AlreadyThere = 1,
            Unreachable  = 2
        }

        /// <summary>
        /// 목표 타일까지 JPS 경로를 계산하고 이동을 시작한다.
        /// 방향 ② M2: PathResult 3분기(Unreachable/AlreadyThere/PathFound)를 명시 처리한다.
        /// Unreachable 시 좌표를 강제 스냅하지 않고 호출자에게 실패를 위임한다 (결함 C 해소).
        /// </summary>
        private StartPathOutcome StartPathTo(int targetX, int targetY)
        {
            // walkable 그리드 최초 1회 초기화 (전체 true — 장애물 없음)
            // [13단계] 하드코딩 100 → MapConfig.Active.mapSize 동적 읽기로 교체
            if (_walkableGrid == null)
            {
                int mapSize = (MapConfig.Active != null) ? MapConfig.Active.mapSize : 100;
                _walkableGrid = new bool[mapSize, mapSize];
                for (int x = 0; x < mapSize; x++)
                    for (int y = 0; y < mapSize; y++)
                        _walkableGrid[x, y] = true;
            }

            // 이전 경로 초기화
            _currentPath.Clear();
            _pathIndex = 0;
            _isMoving  = false;

            // 이미 목표에 있으면 이동 불필요 — 조용히 성공 반환 (ADR-M3)
            if (Brain.TileX == targetX && Brain.TileY == targetY)
                return StartPathOutcome.AlreadyThere;

            PathResult result = JPSPathfinder.FindPathResult(
                Brain.TileX, Brain.TileY,
                targetX, targetY,
                _walkableGrid);

            switch (result.Kind)
            {
                case PathResultKind.AlreadyThere:
                    return StartPathOutcome.AlreadyThere;

                case PathResultKind.Unreachable:
                    // ⚠️ 결함 C 해소: 좌표 강제 스냅 금지 (ADR-M4).
                    // 기존에는 논리 좌표를 목표로 즉시 설정해 GOAP에 성공으로 위장 전달했다.
                    // 새 코드 : 호출자에게 이동 실패를 전달하고 GOAP가 재계획하도록 한다.
                    Debug.LogWarning(
                        $"[VillagerFSM] Unreachable 수신, Replanning 요청. " +
                        $"AgentId={AgentId}, from=({Brain.TileX},{Brain.TileY}), to=({targetX},{targetY})");
                    return StartPathOutcome.Unreachable;

                case PathResultKind.PathFound:
                    _currentPath    = result.Waypoints;
                    _pathIndex      = 0;
                    _isMoving       = true;
                    _waypointTarget = new Vector3(_currentPath[0].x, _currentPath[0].y, 0f);
                    return StartPathOutcome.Started;

                default:
                    // ADR-M2에 따르면 도달 불가. 방어 로그 후 Unreachable 취급.
                    Debug.LogError($"[VillagerFSM] PathResult.Kind 미지원 값: {result.Kind}. AgentId={AgentId}");
                    return StartPathOutcome.Unreachable;
            }
        }

        /// <summary>
        /// 이동 불가 발생 시 결함 C 해소 조치 (방향 ② M2/M3):
        /// 1) NearDiscoveredResource=false 강제 → GOAP가 자원 근접 전제조건을 재검사하도록 (ADR-M4)
        /// 2) RequestReplanning(AbortReason.PathUnreachable)로 원인 분류 저장 후 재계획 요청
        /// </summary>
        private void OnMoveUnreachable(string actionId)
        {
            Brain.NearDiscoveredResource = false;
            RequestReplanning(AbortReason.PathUnreachable, $"actionId={actionId}");
        }

        /// <summary>
        /// 현재 이동 경로를 즉시 중단하고 transform을 Brain 논리 위치로 스냅한다.
        /// 전투 진입/사망/재플래닝 시 호출하여 이동을 강제 종료한다.
        /// </summary>
        private void AbortCurrentPath()
        {
            if (!_isMoving) return;
            _isMoving = false;
            _currentPath.Clear();
            _pathIndex = 0;
            // Rigidbody 사용 중 — 텔레포트에도 물리 상태를 맞춘다.
            if (_rigidbody != null)
            {
                _rigidbody.position = new Vector3(Brain.TileX, Brain.TileY, 0f);
                _rigidbody.linearVelocity = Vector3.zero;
            }
            else
            {
                transform.position = new Vector3(Brain.TileX, Brain.TileY, 0f);
            }
        }

        #endregion

        #region ── 7단계: 타일-Transform 동기화 헬퍼 ──

        /// <summary>
        /// Action ID에 따라 JPS 경로 이동을 시작한다.
        /// 자원 수집 Action → 가장 가까운 발견된 ResourceNode 타일.
        /// 기타 Action → 기지 타일.
        /// </summary>
        private void MoveTileForAction(string actionId)
        {
            if (actionId == null) return;

            // 이전 액션의 노드 점유 해제 (액션 전환 시 이중 점유 방지)
            ReleaseGatherNode();

            // ── Explore: 미탐험 방향의 새 영역으로 이동 ──────────────────────────
            // GOAP 플래너가 [Explore → ChopWood] 체인을 생성하면 이 분기에서
            // 실제로 주민이 이동한다. 이동 중 웨이포인트마다 SensorSystem.DiscoverArea()가
            // 호출되어 새 자원 노드가 발견되고 NearDiscoveredResource=true가 된다.
            if (actionId == "Explore")
            {
                Vector2Int exploreTile = FindExplorationTarget();
                if (StartPathTo(exploreTile.x, exploreTile.y) == StartPathOutcome.Unreachable)
                    OnMoveUnreachable(actionId);
                return;
            }

            ResourceType? targetType = GetResourceTypeForAction(actionId);
            if (targetType.HasValue && SensorSystem.Instance != null)
            {
                ResourceNode nearest = FindNearestDiscoveredNode(targetType.Value);
                if (nearest != null && nearest.TryOccupy(AgentId))
                {
                    _currentGatherNode = nearest;
                    if (StartPathTo(nearest.TileX, nearest.TileY) == StartPathOutcome.Unreachable)
                    {
                        ReleaseGatherNode();
                        OnMoveUnreachable(actionId);
                    }
                    return;
                }
            }

            // 기지 관련 Action 또는 해당 자원 노드 미발견 → 기지 타일
            if (StartPathTo(_baseTileX, _baseTileY) == StartPathOutcome.Unreachable)
                OnMoveUnreachable(actionId);
        }

        /// <summary>
        /// 탐험 목적지 타일을 선택한다.
        /// 미발견 자원 노드가 있으면 그중 가장 가까운 노드 방향으로 이동한다.
        /// 없으면 현재 위치에서 랜덤 방향으로 약 15타일 이동한다.
        /// </summary>
        private Vector2Int FindExplorationTarget()
        {
            // 1순위: 등록된 자원 노드 중 아직 미발견(IsDiscovered=false)인 것의 방향으로 이동
            if (SensorSystem.Instance != null)
            {
                IReadOnlyList<ResourceNode> allNodes = SensorSystem.Instance.GetAllNodes();
                ResourceNode bestUndiscovered = null;
                int bestDist = int.MaxValue;
                for (int idx = 0; idx < allNodes.Count; idx++)
                {
                    ResourceNode n = allNodes[idx];
                    if (n.IsDiscovered || n.CurrentAmount <= 0f) continue;
                    int d = Mathf.Abs(n.TileX - Brain.TileX) + Mathf.Abs(n.TileY - Brain.TileY);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestUndiscovered = n;
                    }
                }
                if (bestUndiscovered != null)
                    return new Vector2Int(bestUndiscovered.TileX, bestUndiscovered.TileY);
            }

            // 2순위: 미발견 노드 정보 없음 → 현재 위치에서 임의 방향으로 15타일
            int mapOffset = MapConfig.Active != null ? MapConfig.Active.mapOffset : 50;
            int mapSize   = MapConfig.Active != null ? MapConfig.Active.mapSize   : 100;
            int mapMin    = -mapOffset;
            int mapMax    =  mapSize - mapOffset - 1;

            float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            int tx = Mathf.Clamp(Brain.TileX + Mathf.RoundToInt(Mathf.Cos(angle) * 15f), mapMin, mapMax);
            int ty = Mathf.Clamp(Brain.TileY + Mathf.RoundToInt(Mathf.Sin(angle) * 15f), mapMin, mapMax);
            return new Vector2Int(tx, ty);
        }

        /// <summary>Action ID → 대상 ResourceType 매핑. null이면 기지 관련 Action.</summary>
        private static ResourceType? GetResourceTypeForAction(string actionId)
        {
            switch (actionId)
            {
                case "ChopWood":   return ResourceType.Wood;
                case "MineStone":  return ResourceType.Stone;
                case "MineIron":   return ResourceType.Iron;
                case "MineCopper": return ResourceType.Copper;
                default:           return null;
            }
        }

        /// <summary>
        /// 옵션 A(포화도) + 옵션 B(ID 기반 선호) 조합으로 채집 노드를 선택한다.
        /// A: CurrentGatherers < MaxGatherers인 노드만 후보로 수집.
        /// B: AgentId 해시로 선호 인덱스를 결정하고 선호 노드에 거리 보너스(-3)를 부여하여
        ///    주민마다 다른 노드를 자연스럽게 선택하도록 유도한다.
        /// </summary>
        private ResourceNode FindNearestDiscoveredNode(ResourceType type)
        {
            IReadOnlyList<ResourceNode> nodes = SensorSystem.Instance.GetAllNodes();

            // 옵션 A: 포화도 조건 포함한 후보 수집
            var candidates = new System.Collections.Generic.List<ResourceNode>();
            for (int i = 0; i < nodes.Count; i++)
            {
                ResourceNode node = nodes[i];
                if (node.ResourceType == type && node.IsDiscovered
                    && node.CurrentAmount > 0f && node.CurrentGatherers < node.MaxGatherers)
                {
                    candidates.Add(node);
                }
            }
            if (candidates.Count == 0) return null;

            // 옵션 B: VillagerId 해시 기반 선호 인덱스 (주민마다 다른 노드 선호)
            int preferredIdx = Mathf.Abs(AgentId.GetHashCode()) % candidates.Count;
            const int PREFERENCE_BONUS = 3; // 선호 노드에 부여할 가상 거리 단축값

            ResourceNode best = null;
            int bestScore = int.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                ResourceNode node = candidates[i];
                int dist = Mathf.Abs(node.TileX - Brain.TileX) + Mathf.Abs(node.TileY - Brain.TileY);
                int score = (i == preferredIdx) ? Mathf.Max(0, dist - PREFERENCE_BONUS) : dist;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = node;
                }
            }
            return best;
        }

        /// <summary>현재 점유 중인 채집 노드를 해제한다. 점유 중이 아니면 아무것도 하지 않는다.</summary>
        private void ReleaseGatherNode()
        {
            if (_currentGatherNode == null) return;
            _currentGatherNode.Release();
            _currentGatherNode = null;
        }

        #endregion

        #region ── Unity Editor 디버그 헬퍼 ──

#if UNITY_EDITOR
        /// <summary>
        /// 씬 뷰에서 주민의 현재 상태, 기지까지의 거리, LOD 경계를 시각화한다.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (Brain == null) return;

            // LOD 경계 원 (보라색) — 2D XY 평면: 법선은 Z축(forward)
            UnityEditor.Handles.color = new Color(0.5f, 0f, 1f, 0.3f);
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.forward, LOD_DISTANCE_THRESHOLD);

            // 현재 상태 레이블
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2f,
                $"[{Brain.FSMState}]\nGoal: {Brain.CurrentGoalId ?? "none"}\n" +
                $"HP:{Brain.HealthLevel:F0} SA:{Brain.SatietyLevel:F0} FT:{Brain.FatigueLevel:F0} LY:{Brain.LoyaltyLevel:F0}"
            );
        }
#endif

        #endregion
    }
}
