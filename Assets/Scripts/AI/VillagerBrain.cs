/// <summary>
/// VillagerBrain.cs - 주민 1명의 모든 런타임 상태를 보관하는 데이터 클래스
///
/// 역할(Role): VillagerFSM(MonoBehaviour)이 읽고 쓰는 상태 저장소.
///             FSM 로직과 데이터를 분리하여 유닛 테스트가 가능하고,
///             나중에 ECS 마이그레이션 시 컴포넌트로 추출하기 쉬운 구조를 유지한다.
/// 사용법(Usage): VillagerFSM.Awake()에서 new VillagerBrain()으로 생성한다.
///               직접 씬이나 Inspector에 노출되지 않는다.
/// 의존성(Dependencies): VillagerEnums.cs
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-25
/// </summary>

using System.Collections.Generic;

namespace AIVillage.AI
{
    /// <summary>
    /// 주민 AI의 모든 런타임 상태를 보관하는 순수 C# 데이터 클래스.
    /// 로직은 VillagerFSM과 ConflictScoreCalculator에 있다.
    /// 이 클래스는 데이터만 갖는다.
    /// </summary>
    public class VillagerBrain
    {
        #region ── 식별 및 팩션 ──

        /// <summary>주민의 런타임 고유 ID. VillagerFSM.Awake()에서 System.Guid.NewGuid()로 자동 생성.</summary>
        public string VillagerId { get; set; }

        /// <summary>주민 직업. GOAP SimulatePlanResult()에서 기본 Action 결정에 사용한다.</summary>
        public AgentRole Role { get; set; }

        /// <summary>
        /// 원래 소속 팩션 ID. 포획/배신 후에도 변하지 않는다.
        /// 충성도 판정(LoyaltyLevel)은 별도 필드로 관리한다.
        /// </summary>
        public int OriginalFactionId { get; set; } = 0;

        #endregion

        #region ── 생존 수치 (0~100) ──

        // 기획서 수치: 초기값 Health=80, Hunger=30, Fatigue=20, Mood=70, Loyalty=55
        // TODO: 기획팀 초기 수치 확인 필요 — 현재 GDD v0.4 기준 임시값 사용 중

        /// <summary>체력 (0~100). 20 미만 → P0 SurviveInjury Goal 발동. 0 → Dead 전이.</summary>
        public float HealthLevel  { get; set; } = 80f;

        /// <summary>배고픔 (0~100). 80 초과 → P0 SurviveHunger Goal 발동.</summary>
        public float HungerLevel  { get; set; } = 30f;

        /// <summary>피로도 (0~100). 90 초과 → P0 SurviveFatigue Goal 발동. ConflictScore에도 영향.</summary>
        public float FatigueLevel { get; set; } = 20f;

        /// <summary>기분 (0~100). 동료 사망 시 감소. 향후 생산성 보정에 사용 예정.</summary>
        public float MoodLevel    { get; set; } = 70f;

        /// <summary>
        /// 충성도 (0~100).
        /// Hybrid C+D 모델 기준:
        ///   70 이상 → CostModifier 0.7 (헌신적 행동)
        ///   50~69   → CostModifier 1.0 (일반)
        ///   30~49   → CostModifier 2.5 (소극적)
        ///   30 미만 → CostModifier 6.0 (반란 가능성)
        /// </summary>
        public float LoyaltyLevel { get; set; } = 55f;

        #endregion

        #region ── 생사 상태 ──

        /// <summary>생존 여부. false → FSM이 다음 Tick에서 Dead 상태로 강제 전이한다.</summary>
        public bool IsAlive { get; set; } = true;

        #endregion

        #region ── 위치 및 주변 환경 플래그 ──

        // 이 플래그들은 GameManager 또는 SensorSystem이 매 틱마다 갱신한다.
        // VillagerFSM은 읽기만 한다.

        /// <summary>기지에 있으면 true. 기지 도착이 필요한 Action의 Precondition.</summary>
        public bool AtBase          { get; set; } = true;

        /// <summary>주변 타일에 수집 가능한 자원 노드가 있으면 true.</summary>
        public bool NearResource    { get; set; } = false;

        // [PR Fix R-001]: Miner 역할의 자원별 플래닝 분기를 분리하기 위해
        // 자원 종류별 근접 플래그를 추가한다. 동일 조건 사용으로 MineStone에
        // 절대 도달하지 못하는 Dead Code 버그를 수정한다.

        /// <summary>주변 타일에 채석 가능한 바위(Rock)가 있으면 true. MineStone의 Precondition.</summary>
        public bool NearRock        { get; set; } = false;

        /// <summary>주변 타일에 철 광석(IronOre)이 있으면 true. MineIron의 Precondition.</summary>
        public bool NearIronOre     { get; set; } = false;

        /// <summary>주변 타일에 구리 광석(CopperOre)이 있으면 true. MineCopper의 Precondition.</summary>
        public bool NearCopperOre   { get; set; } = false;

        // [PR Fix R-003]: FoW(안개 전쟁) 필터를 플래닝에 적용하기 위해 복합 플래그를 추가한다.
        // SensorSystem이 (NearResource AND isDiscovered) 양쪽이 모두 true일 때만 이 플래그를 true로 설정한다.
        // Plan_GatherResources()는 brain.NearResource 대신 이 플래그를 참조하여
        // 발견되지 않은 자원 노드가 플래닝에 포함되는 것을 차단한다.

        /// <summary>
        /// 주변에 수집 가능한 자원 노드가 있으면서 FoW(안개전쟁)에서 이미 발견된 경우 true.
        /// NearResource AND isDiscovered 두 조건을 모두 충족해야 true가 된다.
        /// SensorSystem이 매 틱마다 갱신한다.
        /// </summary>
        public bool NearDiscoveredResource { get; set; } = false;

        /// <summary>적이 인식 범위 내에 있으면 true. ConflictScore SafetyUrgency 0.8f 적용.</summary>
        public bool NearEnemy       { get; set; } = false;

        /// <summary>주변에 드롭 아이템이 있으면 true. PickUpDroppedItem Action의 Precondition.</summary>
        public bool NearDroppedItem { get; set; } = false;

        /// <summary>침대 근처 = 수면 회복 가능. SurviveFatigue Action에서 Sleep 선택 조건.</summary>
        public bool NearBed         { get; set; } = false;

        /// <summary>모닥불 근처 = 음식 조리 가능.</summary>
        public bool NearFireplace   { get; set; } = false;

        /// <summary>창고 근처 = 자원 납부 가능.</summary>
        public bool NearStorage     { get; set; } = false;

        /// <summary>치료사 근처 = 의료 치료 가능. SurviveInjury에서 SeekMedicalAid 선택 조건.</summary>
        public bool NearHealer      { get; set; } = false;

        /// <summary>망루 근처 = 방어 지원 가능.</summary>
        public bool NearWatchtower  { get; set; } = false;

        /// <summary>현재 타일 X 좌표. LOD 거리 계산 기준.</summary>
        public int  TileX           { get; set; } = 0;

        /// <summary>현재 타일 Y 좌표. LOD 거리 계산 기준.</summary>
        public int  TileY           { get; set; } = 0;

        #endregion

        #region ── 인벤토리 플래그 ──

        // bool 플래그로 관리하는 이유: GOAP Precondition이 int 재고보다 bool 플래그를
        // 훨씬 자주 참조하므로 가독성과 성능을 위해 분리한다.

        /// <summary>도구(도끼/곡괭이) 소지 여부. 채집 Action의 Precondition.</summary>
        public bool HasTool            { get; set; } = false;

        /// <summary>제련 무기 소지 여부. Attack Action의 강한 Precondition.</summary>
        public bool HasWeapon          { get; set; } = false;

        /// <summary>원시 무기 소지 여부 (Wood 3 + Stone 2). Forge 없이 제작 가능.</summary>
        public bool HasPrimitiveWeapon { get; set; } = false;

        /// <summary>인벤토리에 식량 보유 여부. 이동 중 섭취 가능.</summary>
        public bool HasFood            { get; set; } = false;

        #endregion

        #region ── FSM 런타임 상태 ──

        /// <summary>현재 FSM 최상위 상태. TransitionTo()에서만 변경한다.</summary>
        public VillagerState FSMState    { get; set; } = VillagerState.Idle;

        /// <summary>LOD 모드 내부 상태. TransitionToLOD()에서만 변경한다.</summary>
        public LODState      LODState    { get; set; } = LODState.LOD_Idle;

        /// <summary>현재 LOD 모드 활성화 여부. FSMState == LOD_FSM일 때 true.</summary>
        public bool          IsLODMode   { get; set; } = false;

        /// <summary>플랜 실행 중 여부. Executing 진입 시 true, 큐 소진/Dead 시 false.</summary>
        public bool          IsExecutingPlan { get; set; } = false;

        #endregion

        #region ── 현재 Goal / Plan ──

        /// <summary>현재 추구 중인 Goal ID (예: "SurviveHunger", "GatherResources").</summary>
        public string CurrentGoalId   { get; set; } = null;

        /// <summary>현재 실행 중인 Action ID. Executing 상태에서 큐에서 꺼낸 값.</summary>
        public string CurrentActionId { get; set; } = null;

        /// <summary>
        /// 실행 대기 중인 Action ID 큐.
        /// Planning에서 채워지고 Executing에서 순서대로 소비된다.
        /// Replanning 진입 시 Clear()된다.
        /// </summary>
        public Queue<string> CurrentPlan { get; set; } = new Queue<string>();

        #endregion

        #region ── 재플래닝 제어 ──

        /// <summary>
        /// 재플래닝 쿨다운 (초). 이 값이 0보다 크면 P0 외 Planning 진입을 차단한다.
        /// Replanning 진입 시 Random(0.3f, 0.5f)로 설정된다.
        /// </summary>
        public float ReplanCooldown      { get; set; } = 0f;

        /// <summary>마지막 재플래닝 시작 Time.time. 연속 실패 간격 측정에 사용한다.</summary>
        public float LastReplanTimestamp { get; set; } = 0f;

        /// <summary>Planning 상태 진입 시 기록한 Time.time. 0.5초 타임아웃 계산 기준.</summary>
        public float PlanningStartTime   { get; set; } = 0f;

        /// <summary>
        /// 연속 Fallback 실패 횟수. 3회 이상이면 Deadlock 판정.
        /// NeedsHelp = true를 설정하고 강제 Fallback Goal로 진입한다.
        /// </summary>
        public int   FallbackCounter     { get; set; } = 0;

        /// <summary>도움 요청 플래그. Deadlock 발생 시 true. GameManager가 이 플래그를 감지한다.</summary>
        public bool  NeedsHelp           { get; set; } = false;

        /// <summary>
        /// Goal별 재플래닝 횟수 추적 딕셔너리.
        /// 키: GoalId / 값: 재플래닝 횟수
        /// 특정 Goal에서 반복 실패 감지에 사용한다.
        /// </summary>
        public Dictionary<string, int> ReplanCount { get; set; }
            = new Dictionary<string, int>();

        #endregion

        #region ── 명령 거부 관련 ──

        /// <summary>거부 메시지 표시 타이머 (초). 0이 되면 Idle로 복귀한다.</summary>
        public float  RefuseMessageTimer { get; set; } = 0f;

        /// <summary>거부 시 대신 수행할 대안 Goal ID. RefusingOrder 상태에서 설정된다.</summary>
        public string AlternativeGoalId  { get; set; } = null;

        /// <summary>현재 처리 대기 중인 플레이어 명령. CommandConflict 진입 시 저장된다.</summary>
        public PlayerOrder PendingOrder  { get; set; }

        /// <summary>대기 중인 명령이 있는지 여부.</summary>
        public bool HasPendingOrder      { get; set; } = false;

        #endregion

        #region ── LOD 제어 ──

        /// <summary>
        /// LOD Tick 누적 시간 (초). 0.5초마다 LOD 내부 상태 머신을 실행한다.
        /// Full GOAP는 0.1초 간격이지만 LOD는 성능 절약을 위해 0.5초 간격으로 제한한다.
        /// </summary>
        public float LODTickAccumulator { get; set; } = 0f;

        /// <summary>LOD GatheringResource 또는 MovingToBase 진입 시각. 시뮬레이션 완료 판정용.</summary>
        public float LODActionStartTime { get; set; } = 0f;

        #endregion

        #region ── Executing 상태 제어 ──

        /// <summary>현재 Action 실행 시작 시각 (Time.time). 2초 완료 시뮬레이션 기준.</summary>
        public float ActionStartTime { get; set; } = 0f;

        #endregion

        #region ── 공개 헬퍼 메서드 ──

        /// <summary>
        /// 충성도에 따른 GOAP Action 비용 배율을 반환한다.
        /// 충성도가 낮을수록 같은 행동을 하는 데 더 높은 비용이 소요된다고 간주한다.
        ///
        /// 기획서 수치 (Hybrid C+D 모델):
        ///   70 이상 → 0.7 (헌신적 — 비용 30% 절감)
        ///   50~69   → 1.0 (보통)
        ///   30~49   → 2.5 (소극적 — 비용 2.5배)
        ///   30 미만 → 6.0 (반란 가능성 — 비용 6배, 사실상 거부에 근접)
        /// </summary>
        public float GetLoyaltyCostModifier()
        {
            if (LoyaltyLevel >= 70f) return 0.7f;
            if (LoyaltyLevel >= 50f) return 1.0f;
            if (LoyaltyLevel >= 30f) return 2.5f;
            return 6.0f;
        }

        /// <summary>
        /// P0(생존) Goal이 현재 활성화되어야 하는지 여부를 반환한다.
        /// Idle, Replanning 등의 상태에서 Tick마다 호출하여 긴급 전이를 감지한다.
        /// </summary>
        public bool HasActiveP0Condition()
        {
            return !IsAlive
                || HealthLevel < 20f
                || HungerLevel > 80f
                || FatigueLevel > 90f;
        }

        /// <summary>
        /// 현재 활성화 조건을 기반으로 가장 높은 우선순위 Goal ID를 반환한다.
        /// P0 → 생존, P1 → 전투, P2 → 건설/채집, P3 → 탐험 순으로 평가한다.
        /// </summary>
        public string GetHighestPriorityGoalId()
        {
            // P0: 생존 긴급 목표
            if (!IsAlive)          return "Dead";
            if (HealthLevel  < 20f) return "SurviveInjury";
            if (HungerLevel  > 80f) return "SurviveHunger";
            if (FatigueLevel > 90f) return "SurviveFatigue";

            // P1: 전투
            if (NearEnemy) return "DefendVillage";

            // P2: 건설 / 채집 (실제 자원 재고 확인은 AuthoritativeWorldState에서 수행)
            // TODO: 기획팀 — 건물 건설 우선순위와 자원 채집 우선순위 순서 확인 필요

            return null; // 외부(VillagerFSM)에서 월드 스테이트 참조 후 결정
        }

        #endregion
    }
}
