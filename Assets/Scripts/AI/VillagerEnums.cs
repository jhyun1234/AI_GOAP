/// <summary>
/// VillagerEnums.cs - FSM 상태, 메시지, 명령, 계산 결과 타입 정의
///
/// 역할(Role): AIVillage.AI 레이어 전반에서 사용되는 열거형과 struct를 한 곳에 모아
///             타입 안전성을 보장하고 매직 넘버 사용을 방지한다.
/// 사용법(Usage): 별도 컴포넌트 불필요. 다른 AI 스크립트에서 직접 참조한다.
/// 의존성(Dependencies): System.Collections.Generic (List, Queue, Dictionary)
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-25
/// </summary>

using System.Collections.Generic;

namespace AIVillage.AI
{
    // ══════════════════════════════════════════════════════════════════════════
    // FSM 상태 열거형
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// VillagerFSM의 최상위 상태 목록.
    /// AnyState 전이: isAlive == false 시 Dead, P0 Goal 발동 시 Planning으로 즉시 이동.
    /// LOD_FSM은 30타일 초과 + 비전투 시 Full GOAP를 경량 시뮬레이션으로 대체한다.
    /// </summary>
    public enum VillagerState
    {
        Idle,            // 다음 Goal을 대기 중. 탈출 조건을 매 Tick 검사한다.
        Planning,        // GOAP 플래너가 Action 시퀀스를 탐색 중.
        Executing,       // 플래닝된 Action을 순차 실행 중.
        Replanning,      // 실패/취소 후 쿨다운 → 재플래닝 대기 중.
        CommandConflict, // 플레이어 명령과 현재 Goal 충돌 계산 중.
        RefusingOrder,   // 명령 거부. 3초 후 Idle로 복귀.
        Dead,            // 사망. 자원 해제 및 드롭 아이템 생성 후 GameObject 비활성화.
        LOD_FSM          // 원거리 경량 시뮬레이션 모드.
    }

    /// <summary>
    /// LOD_FSM 내부 상태.
    /// 30타일 초과 거리의 주민을 경량 틱(0.5초 간격)으로 시뮬레이션한다.
    /// </summary>
    public enum LODState
    {
        LOD_Idle,              // 자원 부족 여부 확인 → GatheringResource 전환
        LOD_GatheringResource, // 자원 수집 시뮬레이션 (3초)
        LOD_MovingToBase,      // 기지 귀환 시뮬레이션 (2초)
        LOD_Alert              // 위협 감지 → Full GOAP 즉시 복귀
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 역할 / 우선순위 열거형
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 주민의 직업 역할. GOAP SimulatePlanResult에서 역할별 기본 Action을 결정한다.
    /// </summary>
    public enum AgentRole
    {
        None,       // 미배정 (초기 상태)
        Lumberjack, // 나무꾼 → ChopWood
        Miner,      // 광부   → MineStone
        Builder,    // 건설자 → BuildTownHall
        Warrior,    // 전사   → AttackEnemy
        Medic,      // 치료사 → SeekMedicalAid 지원
        Cook        // 요리사 → CookMeal
    }

    /// <summary>
    /// AI 메시지 우선순위. 우선순위 큐 정렬에 사용한다.
    /// 숫자가 낮을수록 높은 우선순위 (High = 0이 가장 먼저 처리됨).
    /// </summary>
    public enum MessagePriority
    {
        High   = 0, // 즉시 처리 필요 (사망, 적 감지)
        Medium = 1, // 일반 이벤트 (자원 발견/고갈)
        Low    = 2  // 정보성 알림 (정찰 결과 등)
    }

    /// <summary>
    /// AI 메시지 종류. ReceiveMessage()로 수신되며 FSM 상태 전이를 유발할 수 있다.
    /// </summary>
    public enum MessageType
    {
        VillagerDied,       // 동료 사망 → Loyalty/Mood 패널티 전파
        EnemyDetected,      // 적 탐지 → LOD_Alert 또는 DefendVillage Goal
        ResourceDiscovered, // 새 자원 노드 발견 → GatherResources Goal 재평가
        ResourceDepleted,   // 자원 고갈 → 재플래닝 유발
        OrderIssued,        // 플레이어 명령 수신 → CommandConflict 상태 진입
        OrderRefused,       // 이 에이전트가 거부 메시지를 발행할 때 사용
        RaidDecision        // 팩션 침략 결정 공지
    }

    /// <summary>
    /// 플레이어가 주민에게 내릴 수 있는 명령 종류.
    /// ConflictScore 계산의 OrderImpact 항목에서 참조한다.
    /// </summary>
    public enum OrderType
    {
        GatherWood,      // 나무 수집 (도구 필요, 체력 소모)
        GatherStone,     // 돌 수집  (도구 필요, 체력 소모)
        GatherIron,      // 철 수집  (도구 필요, 체력 소모)
        GatherCopper,    // 구리 수집 (도구 필요, 체력 소모)
        BuildStructure,  // 건설 (기지 이동 필요, 자원 소모)
        Attack,          // 전투 (무기 필요, 고위험)
        Move,            // 이동 (저충돌)
        Cook,            // 요리 (기지 이동 필요)
        Explore          // 탐험 (원거리 이동)
    }

    /// <summary>
    /// 명령 거부 이유 코드. RefusingOrder 상태에서 메시지와 함께 발행된다.
    /// UI 및 디버그 출력에 사용한다.
    /// </summary>
    public enum RefusalReasonCode
    {
        REFUSE_HUNGER,                // hungerLevel > 80 — 먹지 않으면 행동 불가
        REFUSE_INJURY,                // healthLevel < 20 — 부상으로 수행 불가
        REFUSE_FATIGUE,               // fatigueLevel > 90 — 탈진 상태
        REFUSE_LOYALTY,               // loyaltyLevel < 30 — 충성심 부족
        REFUSE_DANGER,                // 근처 적 + 무기 없음
        REFUSE_NO_TOOL,               // 도구 없이 채집 명령
        REFUSE_INSUFFICIENT_RESOURCES // 필요 자원 부족 (건설 등)
    }

    /// <summary>
    /// GOAP 플래닝 결과 타입. SimulatePlanResult()가 반환하며 상태 전이를 결정한다.
    /// </summary>
    public enum PlanResultType
    {
        Success,         // 유효한 Action 시퀀스를 찾음
        NoSolutionFound, // Goal을 달성할 Action 조합 없음 → Replanning
        Timeout,         // 0.5초 제한 초과 → Replanning
        Deadlock         // fallbackCounter >= 3 → needsHelp = true
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 값 타입 구조체 (struct)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 에이전트 간 또는 시스템 → 에이전트로 전달되는 AI 메시지.
    /// ReceiveMessage()로 수신하며 내부 우선순위 큐에서 처리된다.
    /// </summary>
    public struct AIMessage
    {
        /// <summary>메시지 종류. FSM이 어떤 반응을 할지 결정한다.</summary>
        public MessageType Type;

        /// <summary>처리 우선순위. High(0)가 가장 먼저 소비된다.</summary>
        public MessagePriority Priority;

        /// <summary>메시지를 발행한 에이전트의 AgentId. 빈 문자열이면 시스템 발행.</summary>
        public string SenderId;

        /// <summary>
        /// 추가 데이터 페이로드. EnemyDetected → 적의 TileX/Y 등을 담을 수 있다.
        /// 2단계에서는 string 또는 int 값 위주로 사용. 박싱 주의.
        /// </summary>
        public object Payload;

        /// <summary>메시지 발행 시각 (Time.time). 유효기간 판별에 사용한다.</summary>
        public float IssuedAt;
    }

    /// <summary>
    /// 플레이어가 특정 주민에게 내리는 명령 데이터.
    /// TryExecuteOrder()의 파라미터로 전달된다.
    /// </summary>
    public struct PlayerOrder
    {
        /// <summary>명령 대상 주민의 AgentId. 빈 문자열이면 전체 명령.</summary>
        public string TargetVillagerId;

        /// <summary>명령 종류. ConflictScore의 OrderImpact 계산에 사용한다.</summary>
        public OrderType OrderType;

        /// <summary>명령의 목표 타일 좌표 X.</summary>
        public int TargetTileX;

        /// <summary>명령의 목표 타일 좌표 Y.</summary>
        public int TargetTileY;

        /// <summary>건설 명령 시 건물 타입 ID. OrderType.BuildStructure일 때만 유효.</summary>
        public string BuildingTypeId;

        /// <summary>명령 발행 시각 (Time.time). 오래된 명령 필터링에 사용한다.</summary>
        public float IssuedAt;
    }

    /// <summary>
    /// ConflictScore 계산 결과 및 중간값을 보관하는 구조체.
    /// 디버그 및 UI 표시를 위해 중간 계산 항목을 모두 보존한다.
    /// </summary>
    public struct ConflictScoreData
    {
        // ── 긴급도(Urgency) 항목 ─────────────────────────────────────────────

        /// <summary>배고픔 긴급도. (hungerLevel - 80) / 20, hungerLevel > 80일 때만 양수.</summary>
        public float HungerUrgency;

        /// <summary>부상 긴급도. (20 - healthLevel) / 20, healthLevel < 20일 때만 양수.</summary>
        public float HealthUrgency;

        /// <summary>피로 긴급도. (fatigueLevel - 90) / 10, fatigueLevel > 90일 때만 양수.</summary>
        public float FatigueUrgency;

        /// <summary>안전 긴급도. nearEnemy == true 시 0.8f 고정.</summary>
        public float SafetyUrgency;

        // ── 명령 충격(Impact) 항목 ───────────────────────────────────────────

        /// <summary>
        /// 명령별 충돌 임팩트 합산값.
        /// 기지 이동 필요(+0.5), 저체력 소모 명령(+0.7), 무기 없이 전투(+1.0), 도구 없이 채집(+0.3).
        /// </summary>
        public float OrderImpact;

        // ── 최종 판정 ────────────────────────────────────────────────────────

        /// <summary>ConflictScore = Σ(urgency × impact). 이 값과 Threshold를 비교한다.</summary>
        public float ConflictScore;

        /// <summary>Threshold = 2.5f × (loyaltyLevel / 50f). 충성도가 낮을수록 임계값도 낮아진다.</summary>
        public float Threshold;

        /// <summary>ConflictScore >= Threshold 이면 true → RefusingOrder 상태로 전이한다.</summary>
        public bool ShouldRefuse;
    }

    /// <summary>
    /// GOAP 플래닝 결과. SimulatePlanResult()가 반환하며 VillagerFSM이 소비한다.
    /// 3단계 Job System 연동 후에는 실제 플래너 결과로 교체될 예정.
    /// </summary>
    public struct GOAPPlanResult
    {
        /// <summary>플래닝을 요청한 에이전트의 AgentId.</summary>
        public string AgentId;

        /// <summary>플래닝 성공 여부. false이면 ResultType으로 실패 원인을 확인한다.</summary>
        public bool Success;

        /// <summary>실패/성공 구분 타입. NoSolutionFound 또는 Timeout이면 Replanning으로 전이한다.</summary>
        public PlanResultType ResultType;

        /// <summary>계획된 전체 Action의 예상 누적 비용.</summary>
        public float TotalEstimatedCost;

        /// <summary>탐색 깊이. 디버그 및 Deadlock 감지에 사용한다.</summary>
        public int SearchDepth;

        /// <summary>
        /// 실행할 Action ID 시퀀스. VillagerBrain.CurrentPlan Queue에 복사된다.
        /// 3단계에서 NativeArray&lt;FixedString64Bytes&gt;로 교체될 예정.
        /// </summary>
        public List<string> ActionSequence;
    }
}
