/// <summary>
/// IAutonomousAgent.cs - 자율 에이전트(주민)의 공통 계약 인터페이스
///
/// 역할(Role): 모든 자율 에이전트(주민)가 반드시 구현해야 하는 메서드와 프로퍼티를 정의한다.
///             VillagerFSM이 이 인터페이스를 구현하며, 외부 시스템(GameManager, MessageBus 등)은
///             구체 타입이 아닌 이 인터페이스를 통해 주민과 통신한다.
/// 사용법(Usage): VillagerFSM 컴포넌트가 자동으로 구현한다. 직접 씬에 붙이지 않는다.
///               IAutonomousAgent agent = villagerFSMComponent;
///               agent.ReceiveMessage(message);
/// 의존성(Dependencies): VillagerEnums.cs (VillagerState, AIMessage, PlayerOrder)
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-25
/// </summary>

namespace AIVillage.AI
{
    /// <summary>
    /// 자율 에이전트(주민 AI)의 공통 계약.
    /// 외부 시스템이 VillagerFSM의 내부 구현에 의존하지 않도록 추상화한다.
    /// </summary>
    public interface IAutonomousAgent
    {
        // ── 식별 / 소속 ───────────────────────────────────────────────────────

        /// <summary>에이전트의 런타임 고유 ID. Awake()에서 GUID로 자동 생성된다.</summary>
        string AgentId { get; }

        /// <summary>에이전트가 태어난 원래 팩션 ID. 충성도 판정에 사용한다.</summary>
        int OriginalFactionId { get; }

        // ── 현재 FSM 상태 ─────────────────────────────────────────────────────

        /// <summary>현재 FSM 최상위 상태. UI 및 디버그용으로 읽기만 허용한다.</summary>
        VillagerState CurrentState { get; }

        // ── 생존 수치 (0~100) ─────────────────────────────────────────────────

        /// <summary>체력. 0이 되면 Dead 전이가 즉시 발생한다.</summary>
        float HealthLevel { get; }

        /// <summary>배고픔. 100에 가까울수록 긴급 목표가 발동된다.</summary>
        float HungerLevel { get; }

        /// <summary>피로도. 높을수록 행동 비용이 증가하고 거부 확률이 높아진다.</summary>
        float FatigueLevel { get; }

        /// <summary>
        /// 충성도 (0~100). 팩션 귀속 및 명령 거부 임계값 계산에 사용한다.
        /// Hybrid C+D 모델: 70 이상 = 충성, 50~69 = 중립, 30~49 = 의심, 30 미만 = 반란 가능성.
        /// </summary>
        float LoyaltyLevel { get; }

        /// <summary>생존 여부. false가 되면 Dead 상태로 강제 전이된다.</summary>
        bool IsAlive { get; }

        // ── 외부 통신 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 외부 시스템(MessageBus, GameManager)이 이 에이전트에 메시지를 전달한다.
        /// 내부적으로 우선순위 큐에 적재하며 Tick()에서 소비한다.
        /// </summary>
        /// <param name="message">전달할 AI 메시지</param>
        void ReceiveMessage(AIMessage message);

        /// <summary>
        /// 플레이어의 명령을 처리 시도한다.
        /// ConflictScore 계산 후 거부 임계값을 초과하면 false를 반환하고
        /// RefusingOrder 상태로 전이한다.
        /// </summary>
        /// <param name="order">플레이어가 내린 명령</param>
        /// <returns>명령 수락 = true, 거부 = false</returns>
        bool TryExecuteOrder(PlayerOrder order);

        /// <summary>
        /// FSM 상태를 외부에서 강제 전이시킨다.
        /// P0 Goal 발동이나 Dead 처리 등 AnyState 전이에만 사용한다.
        /// 일반적인 명령 전달은 TryExecuteOrder()를 사용할 것.
        /// </summary>
        /// <param name="state">강제 전이할 목표 상태</param>
        void ForceTransitionTo(VillagerState state);
    }
}
