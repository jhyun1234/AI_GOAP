/// <summary>
/// EnemyBrain.cs - 적 유닛 1명의 모든 런타임 상태를 보관하는 데이터 클래스
///
/// 역할(Role): EnemyFSM(MonoBehaviour)이 읽고 쓰는 상태 저장소.
///             VillagerBrain과 동일한 패턴으로 FSM 로직과 데이터를 분리하여
///             유닛 테스트와 ECS 마이그레이션이 쉬운 구조를 유지한다.
/// 사용법(Usage): EnemyFSM.Awake()에서 new EnemyBrain()으로 생성한다.
///               직접 씬이나 Inspector에 노출되지 않는다.
/// 의존성(Dependencies): 없음 (순수 C# 데이터 클래스)
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-27
/// </summary>

namespace AIVillage.AI
{
    // ══════════════════════════════════════════════════════════════════════════
    // EnemyFSM 상태 열거형
    // EnemyBrain.cs 파일에 같이 정의하여 파일 수를 최소화한다.
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// EnemyFSM의 5가지 상태.
    /// AnyState 전이:
    ///   - HP 30% 이하 → Retreating (Update에서 즉시 감지)
    ///   - IsAlive == false → Dead
    /// </summary>
    public enum EnemyState
    {
        Idle,       // 활성화 대기 (FactionAI가 SetTargetTile 호출할 때까지 대기)
        Moving,     // FactionAI에서 받은 목표 타일로 이동 시뮬레이션 (3초)
        Attacking,  // 플레이어 마을 공격 시뮬레이션 (2초 후 Idle 복귀)
        Scouting,   // [FactionAI 확장] 정찰 전술 — 목표 타일 도달 후 ScoutCompleted=true → Idle
        Plundering, // [FactionAI 확장] 약탈 전술 — 자원 탈취 후 Retreating으로 귀환
        Retreating, // 체력 30% 이하 시 기지 방향으로 후퇴 + HP 회복 (0.5f/초)
        Dead        // 모든 자원 해제, 틱 비활성화
    }

    /// <summary>
    /// 팩션 레이드 전술 모드.
    /// FactionAI가 결정하고 EnemyFSM이 Moving 완료 후 분기에 사용한다.
    /// </summary>
    public enum RaidMode
    {
        Attack,  // 전면 공격 (기본값) — Moving 완료 후 Attacking 상태 진입
        Plunder  // 약탈 후 철수 — Moving 완료 후 Plundering 상태 진입
    }

    /// <summary>
    /// 적 AI의 모든 런타임 상태를 보관하는 순수 C# 데이터 클래스.
    /// 로직은 EnemyFSM에 있다. 이 클래스는 데이터만 갖는다.
    /// </summary>
    public class EnemyBrain
    {
        #region ── 식별 ──

        /// <summary>런타임 고유 ID. EnemyFSM.Awake()에서 System.Guid.NewGuid()로 자동 생성.</summary>
        public string EnemyId { get; set; }

        /// <summary>
        /// 소속 팩션 ID.
        ///   0 = 숲의 부족 (Forest Tribe)
        ///   1 = 철의 도시 (Iron City)
        ///   2 = 상인 연합 (Merchant Alliance)
        /// FactionAI가 동일 FactionId를 가진 EnemyFSM만 지휘한다.
        /// </summary>
        public int FactionId { get; set; }

        #endregion

        #region ── 위치 ──

        /// <summary>현재 타일 X 좌표. SensorSystem이 NearEnemy 판정에 사용한다.</summary>
        public int TileX { get; set; } = 0;

        /// <summary>현재 타일 Y 좌표. SensorSystem이 NearEnemy 판정에 사용한다.</summary>
        public int TileY { get; set; } = 0;

        /// <summary>FactionAI가 SetTargetTile()로 설정한 이동 목표 타일 X 좌표.</summary>
        public int TargetTileX { get; set; } = 0;

        /// <summary>FactionAI가 SetTargetTile()로 설정한 이동 목표 타일 Y 좌표.</summary>
        public int TargetTileY { get; set; } = 0;

        /// <summary>소속 팩션 기지 타일 X 좌표. Retreating 시 복귀 목적지.</summary>
        public int BaseTileX { get; set; } = 0;

        /// <summary>소속 팩션 기지 타일 Y 좌표. Retreating 시 복귀 목적지.</summary>
        public int BaseTileY { get; set; } = 0;

        #endregion

        #region ── 생존 상태 ──

        /// <summary>
        /// 체력 (0~100). 30 미만 → AnyState에서 Retreating으로 즉시 전이.
        /// Retreating 상태에서 0.5f/초 회복. 0 이하 → Dead 전이.
        /// </summary>
        public float HealthLevel { get; set; } = 100f;

        /// <summary>생존 여부. false → FSM이 다음 Update에서 Dead로 강제 전이.</summary>
        public bool IsAlive { get; set; } = true;

        /// <summary>
        /// 적은 항상 무장 상태로 시작한다.
        /// 장래에 무장해제 시스템 도입 시 이 필드를 사용한다.
        /// </summary>
        public bool HasWeapon { get; set; } = true;

        #endregion

        #region ── FSM 런타임 상태 ──

        /// <summary>현재 FSM 상태. EnemyFSM.TransitionTo()에서만 변경한다.</summary>
        public EnemyState FSMState { get; set; } = EnemyState.Idle;

        // ── 전술 모드 (FactionAI 확장) ──────────────────────────────────────────
        /// <summary>
        /// 현재 레이드 전술 모드. FactionAI가 SetTargetTile 호출 시 지정한다.
        /// Plunder이면 Moving 완료 후 Plundering 상태로 분기한다.
        /// </summary>
        public RaidMode CurrentRaidMode { get; set; } = RaidMode.Attack;

        /// <summary>
        /// 정찰 완료 플래그. State_Scouting에서 목표 타일 도달 시 true가 된다.
        /// FactionAI가 다음 Tick에 이 플래그를 감지하여 _playerStrengthKnown=true 처리 후 false로 리셋한다.
        /// </summary>
        public bool ScoutCompleted { get; set; } = false;

        // ── 상태별 타이머 ──────────────────────────────────────────────────────
        // 각 상태에서 Time.time 기준 진입 시각을 기록하여 더미 완료 판정에 사용한다.

        /// <summary>Moving 상태 진입 시각 (Time.time). 3초 이동 시뮬레이션 기준.</summary>
        public float MoveStartTime { get; set; } = 0f;

        /// <summary>Attacking 상태 진입 시각 (Time.time). 2초 공격 시뮬레이션 기준.</summary>
        public float AttackStartTime { get; set; } = 0f;

        /// <summary>Retreating 상태 진입 시각 (Time.time). HP 회복 계산 기준.</summary>
        public float RetreatStartTime { get; set; } = 0f;

        /// <summary>
        /// HP 회복 마지막 처리 시각 (Time.time).
        /// 현재 구현(EnemyFSM.Update)은 deltaTime 기반이므로 이 필드는 미사용.
        /// 향후 틱 기반 누적 회복 방식으로 전환 시 사용 예정.
        /// </summary>
        public float LastHealTime { get; set; } = 0f;

        #endregion

        #region ── 틱 분산 ──

        /// <summary>
        /// Tick 그룹 인덱스 (0~5).
        /// GameManager 틱 루프에서 그룹별로 나누어 호출하여 프레임당 부하를 분산한다.
        /// VillagerFSM과 동일한 방식으로 동작한다.
        /// </summary>
        public int TickGroupIndex { get; set; } = 0;

        /// <summary>
        /// Dead 상태 처리를 이미 실행했는지 추적하는 플래그.
        /// EnterDead()는 최초 1회만 실행되어야 하므로 이 플래그로 중복 실행을 방지한다.
        /// </summary>
        public bool DeadProcessed { get; set; } = false;

        #endregion
    }
}
