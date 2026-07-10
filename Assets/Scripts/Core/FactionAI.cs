/// <summary>
/// FactionAI.cs - 팩션 AI 의사결정 시스템
///
/// 역할(Role): 팩션(적 세력) 전체의 전략적 의사결정을 담당한다.
///             5초마다 자신의 Goal을 평가하고 소속 EnemyFSM 유닛들에게 SetTargetTile()로 지시를 내린다.
///             GDD v0.4 기준 3개 팩션(숲의 부족/철의 도시/상인 연합) 각각에 1개씩 부착한다.
///
/// 사용법(Usage):
///   1. 각 팩션의 빈 GameObject (예: "Faction_Forest")에 이 컴포넌트를 추가한다.
///   2. Inspector에서 _factionId, _enemyActivationDay, 초기 자원량, 기지 위치를 설정한다.
///   3. GameManager.Start()에서 InjectDependencies()와 InitializeBase()를 호출한다.
///   4. FactionTick 코루틴이 자동으로 5초마다 의사결정을 수행한다.
///
/// 팩션별 특수 규칙:
///   0 (숲의 부족): activationDay=10, 식량 < 30이면 추가 침략 트리거
///   1 (철의 도시):  activationDay=12, ironStock < 15이면 즉시 침략 트리거
///   2 (상인 연합):  activationDay=10, RaidDecision 전 TradeProposal 1회 먼저 발행
///
/// 의존성(Dependencies):
///   - EnemyFSM.cs, EnemyBrain.cs (AIVillage.AI)
///   - MessageBus.cs, AuthoritativeWorldState.cs (AIVillage.Core)
///   - VillagerEnums.cs (MessageType, MessagePriority, AIMessage 등)
///
/// Script Execution Order: [DefaultExecutionOrder(-70)]
///   GameManager(-80)가 먼저 초기화 → FactionAI(-70) → EnemyFSM(0)
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-27
/// </summary>

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AIVillage.AI;

namespace AIVillage.Core
{
    /// <summary>
    /// 팩션 전체의 전략 AI. 5초 간격 코루틴으로 침략/방어/정찰 여부를 결정하고
    /// 소속 EnemyFSM 유닛에게 이동 명령(SetTargetTile)을 내린다.
    /// </summary>
    [DefaultExecutionOrder(-70)] // GameManager(-80) 이후, EnemyFSM(0) 이전
    [DisallowMultipleComponent]
    public sealed class FactionAI : MonoBehaviour
    {
        #region ── 상수 ──

        // ── FactionTick 간격 (기획서 수치) ────────────────────────────────────
        private const float FACTION_TICK_INTERVAL = 5.0f; // 기획서 수치: 5초마다 의사결정

        // ── 팩션 ID 상수 (매직 넘버 방지) ────────────────────────────────────
        private const int FACTION_FOREST  = 0; // 숲의 부족
        private const int FACTION_IRON    = 1; // 철의 도시
        private const int FACTION_MERCHANT = 2; // 상인 연합

        // ── factionStrength 공식 상수 (기획서 수치) ───────────────────────────
        // 숲의 부족:  (유닛 수 × 10) + 25
        // 철의 도시:  (유닛 수 × 12) + 35
        // 상인 연합:  (유닛 수 × 8) + 20
        private const float FOREST_UNIT_WEIGHT   = 10f;
        private const float FOREST_BASE_STRENGTH = 25f;
        private const float IRON_UNIT_WEIGHT     = 12f;
        private const float IRON_BASE_STRENGTH   = 35f;
        private const float MERCHANT_UNIT_WEIGHT = 8f;
        private const float MERCHANT_BASE_STRENGTH = 20f;

        // ── 침략 트리거 임계값 (기획서 확정값) ───────────────────────────────
        // (copperStock < 10 OR silverStock < 5)
        // AND nearPlayerTerritory == true  (팩션 유닛이 플레이어 영역 25타일 이내)
        // AND playerStrength < factionStrength × 0.8
        private const float COPPER_RAID_THRESHOLD    = 10f; // 기획서 수치
        private const float SILVER_RAID_THRESHOLD    = 5f;  // 기획서 수치
        private const float PLAYER_TERRITORY_DIST    = 25f; // 기획서 수치: 25타일 이내
        private const float STRENGTH_RATIO_THRESHOLD = 0.8f; // 기획서 수치

        // ── 팩션별 특수 트리거 임계값 (기획서 수치) ─────────────────────────
        private const float FOREST_FOOD_RAID_THRESHOLD = 30f; // 숲의 부족: 식량 < 30 추가 트리거
        private const float IRON_IRON_RAID_THRESHOLD   = 15f; // 철의 도시: iron < 15 즉시 트리거

        // ── Goal 우선순위 관련 임계값 (기획서 수치) ─────────────────────────
        // F-P0: SurviveFaction  — factionStrength <= 초기값 × 0.3
        private const float SURVIVE_FACTION_RATIO = 0.3f;
        // F-P1: DefendBase      — playerStrength > factionStrength × 1.2
        private const float DEFEND_BASE_RATIO = 1.2f;

        // ── EnemyDetected 위협 등급 (기획서 수치) ────────────────────────────
        // 팩션 레이드 = 위협 4등급 (가장 높은 위협)
        private const int RAID_THREAT_TIER = 4;

        // ── 약탈 전술 발동 조건 (FactionAI 확장) ─────────────────────────────
        // playerStrength >= factionStrength × 0.8 (전면 공격 불리)
        // AND (copperStock < 5 OR silverStock < 3) (자원 긴급)
        private const float PLUNDER_STRENGTH_RATIO = 0.8f; // 기획서 수치: playerStrength 비율 임계값
        private const float PLUNDER_COPPER_MIN     = 5f;   // 기획서 수치: 약탈 트리거 copper 하한
        private const float PLUNDER_SILVER_MIN     = 3f;   // 기획서 수치: 약탈 트리거 silver 하한

        // ── 침략 예고 스테이지 (F-B, ADR-B1/B2) ──────────────────────────────
        // 제안치: 관성 실험 1회 이상 이후에만 조정 (F-B 명세 §6 ADR-B2)
        private const float WARNING_LEAD_DAYS_RUMOR     = 3f; // Rumor 진입 시점 (D-3)
        private const float WARNING_LEAD_DAYS_CONFIRMED = 1f; // Confirmed 전환 임계 (D-1)

        // 예고 스테이지 상수 — nested enum 대신 int 상수(확장 예정 낮음)
        private const int WSTAGE_NONE      = 0;
        private const int WSTAGE_RUMOR     = 1;
        private const int WSTAGE_CONFIRMED = 2;

        // ── 플레이어 기지 위치 (더미 기본값) ─────────────────────────────────
        // GameManager가 InitializeBase()를 통해 실제 값을 주입한다.
        // TODO: 기획팀 확인 필요 — 플레이어 기지의 기본 타일 좌표
        private const int DEFAULT_PLAYER_BASE_X = 0;
        private const int DEFAULT_PLAYER_BASE_Y = 0;

        #endregion

        #region ── Serialized Fields (Inspector) ──

        // ── 팩션 식별 ─────────────────────────────────────────────────────────

        [Tooltip("팩션 ID. 0=숲의 부족, 1=철의 도시, 2=상인 연합. 씬에 배치된 EnemyFSM의 _factionId와 일치해야 한다.")]
        [SerializeField] private int _factionId = 0;

        // ── 활성화 시점 (기획서 확정값) ─────────────────────────────────────

        [Tooltip("팩션 활성화 게임 일수. 이 일수 미만이면 FactionTick이 의사결정을 건너뛴다. " +
                 "숲의 부족=10, 철의 도시=12, 상인 연합=10 (기획서 수치)")]
        [SerializeField] private float _enemyActivationDay = 10f;

        // ── 초기 팩션 자원 (기획서 수치) ─────────────────────────────────────

        [Tooltip("팩션 초기 나무 재고. 기획서 수치: 20")]
        [SerializeField] private int _initialWood = 20;

        [Tooltip("팩션 초기 돌 재고. 기획서 수치: 15")]
        [SerializeField] private int _initialStone = 15;

        [Tooltip("팩션 초기 식량 재고. 기획서 수치: 50")]
        [SerializeField] private int _initialRawFood = 50;

        [Tooltip("팩션 초기 구리 재고. 0이면 공통 침략 트리거(copper < 10)가 게임 시작부터 즉시 활성화됨. 기획서 미확정.")]
        [SerializeField] private float _initialCopperStock = 0f;

        [Tooltip("팩션 초기 은 재고. 0이면 공통 침략 트리거(silver < 5)가 게임 시작부터 즉시 활성화됨. 기획서 미확정.")]
        [SerializeField] private float _initialSilverStock = 0f;

        // ── 기지 위치 ─────────────────────────────────────────────────────────

        [Tooltip("이 팩션 기지의 타일 X 좌표. GameManager가 InitializeBase()로 갱신할 수 있다.")]
        [SerializeField] private int _baseTileX = 20;

        [Tooltip("이 팩션 기지의 타일 Y 좌표. GameManager가 InitializeBase()로 갱신할 수 있다.")]
        [SerializeField] private int _baseTileY = 20;

        // ── 침략 쿨다운 (FactionAI 확장) ─────────────────────────────────────

        [Tooltip("레이드 격퇴 후 재침략까지의 쿨다운 (게임일). 기본값: 5일")]
        [SerializeField] private float _raidCooldownDays = 5f;

        // ── 정찰 전술 설정 (FactionAI 확장) ──────────────────────────────────

        [Tooltip("정찰을 시작하는 최소 게임 일수. 숲의 부족=5, 철의 도시=7, 상인 연합=10")]
        [SerializeField] private float _scoutStartDay = 5f;

        [Tooltip("정찰 완료 후 다음 정찰까지의 간격 (게임일). 기본값: 3일")]
        [SerializeField] private float _scoutIntervalDays = 3f;

        #endregion

        #region ── Private Fields (런타임 상태) ──

        // ── 소속 유닛 목록 ────────────────────────────────────────────────────
        // Start()에서 FindObjectsOfType<EnemyFSM>()으로 수집하고 LINQ로 FactionId 필터링한다.
        // FindObjectsOfType 사용은 Start()에서 1회만 허용된다.
        private List<EnemyFSM> _units = new List<EnemyFSM>();

        // ── 팩션 전력 ─────────────────────────────────────────────────────────
        // FactionTick마다 CalculateFactionStrength()로 재계산한다.
        private float _factionStrength;

        /// <summary>게임 시작 시점의 초기 팩션 전력. F-P0 SurviveFaction 판정 기준.</summary>
        private float _initialFactionStrength;

        // ── 팩션 자원 재고 (침략 트리거용) ──────────────────────────────────
        // 기획서에서 팩션 자체 자원 관리는 간소화하여 필드로만 추적한다.
        // TODO: 기획팀 확인 필요 — 팩션 자원이 실제 수집 시스템에 연동될 경우 갱신 방식 논의 필요
        private float _copperStock;   // 기획서: 침략 트리거 (copper < 10). 초기값 = _initialCopperStock
        private float _silverStock;   // 기획서: 침략 트리거 (silver < 5).  초기값 = _initialSilverStock
        private float _ironStock   = 0f;   // 철의 도시 전용 트리거
        private float _rawFoodStock;       // 숲의 부족 전용 트리거 (초기값 = _initialRawFood)

        // ── 특수 상태 플래그 ──────────────────────────────────────────────────
        /// <summary>상인 연합 전용: RaidDecision 전 TradeProposal을 이미 발행했는지 추적.</summary>
        private bool _tradeProposalSent = false;

        /// <summary>팩션이 활성화되었는지 여부. activationDay 도달 시 true로 설정된다.</summary>
        private bool _isActivated = false;

        /// <summary>현재 레이드(침략)를 진행 중인지 여부.</summary>
        private bool _isRaiding = false;

        // ── 플레이어 기지 위치 캐시 ─────────────────────────────────────────
        // GameManager가 InitializeBase()를 통해 주입한다.
        private int _playerBaseTileX = DEFAULT_PLAYER_BASE_X;
        private int _playerBaseTileY = DEFAULT_PLAYER_BASE_Y;

        // ── 외부 의존성 참조 캐시 ────────────────────────────────────────────
        // Update/Tick 내에서 GetComponent/Find 호출 금지 — Start에서 캐시한다.
        private AuthoritativeWorldState _worldState;

        // ── FactionTick 코루틴 참조 (OnDestroy에서 StopCoroutine용) ─────────
        private Coroutine _factionTickCoroutine;

        // ── 침략 쿨다운 런타임 (FactionAI 확장) ──────────────────────────────
        /// <summary>레이드 격퇴 후 남은 쿨다운 (게임일 단위). 0이면 평가 재개.</summary>
        private float _raidCooldownRemaining = 0f;

        // ── 정찰 런타임 (FactionAI 확장) ─────────────────────────────────────
        /// <summary>정찰로 playerStrength를 파악했는지 여부. false이면 F-P3 Scout 발동.</summary>
        private bool _playerStrengthKnown = false;

        /// <summary>마지막 정찰 완료 게임 일수. 재정찰 주기(_scoutIntervalDays) 계산에 사용.</summary>
        private float _lastScoutCompletedDay = -999f;

        /// <summary>현재 정찰 명령이 발령 중인지 여부. 중복 발령 방지에 사용.</summary>
        private bool _isScoutActive = false;

        /// <summary>직전 FactionTick의 GameTime. deltaGameDays 계산에 사용.</summary>
        private float _prevTickGameDay = 0f;

        // ── 침략 예고 상태 (F-B, ADR-B1) ─────────────────────────────────────
        /// <summary>F-B: 현재 예고 스테이지. WSTAGE_NONE/RUMOR/CONFIRMED.</summary>
        private int   _warningStage       = WSTAGE_NONE;
        /// <summary>F-B: 예고 트리거된 시점의 예상 침략 게임일. Rumor 발행 시 확정 후 불변.</summary>
        private float _expectedRaidDay    = -1f;
        /// <summary>F-B: Rumor 스테이지 발행 여부(디버그·통계용).</summary>
        private bool  _rumorPublished     = false;
        /// <summary>F-B: Confirmed 스테이지 발행 여부(디버그·통계용).</summary>
        private bool  _confirmedPublished = false;

        #endregion

        #region ── 공개 프로퍼티 ──

        /// <summary>팩션 기지 타일 X 좌표. GameManager나 EnemyFSM이 기지 위치를 참조할 때 사용.</summary>
        public int BaseTileX => _baseTileX;

        /// <summary>팩션 기지 타일 Y 좌표.</summary>
        public int BaseTileY => _baseTileY;

        /// <summary>팩션 ID. GameManager나 외부 시스템이 팩션을 구분할 때 사용.</summary>
        public int FactionId => _factionId;

        /// <summary>현재 팩션 전력 수치. GameManager의 CalculatePlayerStrength와 비교에 사용.</summary>
        public float FactionStrength => _factionStrength;

        /// <summary>현재 레이드 상태 여부. 디버그 및 UI 표시용.</summary>
        public bool IsRaiding => _isRaiding;

        #endregion

        #region ── Unity 생명주기 메서드 ──

        /// <summary>
        /// Unity가 GameObject 활성화 시 가장 먼저 호출한다.
        /// 팩션 자원과 WorldState 참조를 초기화한다.
        /// </summary>
        private void Awake()
        {
            // ── 팩션 자원 초기화 ─────────────────────────────────────────────────
            _rawFoodStock  = _initialRawFood;
            _copperStock   = _initialCopperStock;
            _silverStock   = _initialSilverStock;

            // WorldState 싱글턴 캐시 (GameManager보다 늦게 초기화되므로 null일 수 있음)
            _worldState = AuthoritativeWorldState.Instance;
            if (_worldState == null)
            {
                Debug.Log($"[FactionAI] Awake: WorldState 미주입 상태. " +
                          $"Start()에서 재획득합니다. FactionId={_factionId}");
            }
        }

        /// <summary>
        /// Awake 이후, 첫 프레임 Update 직전에 Unity가 호출한다.
        /// 소속 EnemyFSM을 수집하고 초기 팩션 전력을 계산한 뒤 FactionTick 코루틴을 시작한다.
        ///
        /// FindObjectsOfType 사용 이유: Start() 시점에 씬의 모든 EnemyFSM이 초기화 완료된 상태.
        /// Update/Tick 내에서는 절대 Find 계열 호출 금지.
        /// </summary>
        private void Start()
        {
            // ── WorldState 재획득 (Awake에서 실패한 경우) ──────────────────────
            if (_worldState == null)
            {
                _worldState = AuthoritativeWorldState.Instance;
                if (_worldState == null)
                {
                    Debug.LogWarning($"[FactionAI] Start: AuthoritativeWorldState.Instance가 여전히 null입니다. " +
                                     $"침략 트리거 판정이 제한됩니다. FactionId={_factionId}");
                }
            }

            // ── 소속 EnemyFSM 수집 ─────────────────────────────────────────────
            // Start() 시점에 한 번만 허용되는 FindObjectsOfType 호출.
            // LINQ Where로 FactionId가 일치하는 유닛만 필터링한다.
            EnemyFSM[] allEnemies = FindObjectsOfType<EnemyFSM>();
            _units = allEnemies != null
                ? new List<EnemyFSM>(allEnemies.Where(u => u != null && u.FactionId == _factionId))
                : new List<EnemyFSM>();

            Debug.Log($"[FactionAI] Start: FactionId={_factionId}, 소속 유닛 {_units.Count}명 수집 완료.");

            // ── 초기 팩션 전력 계산 ──────────────────────────────────────────────
            // CalculateFactionStrength()는 _units 리스트를 기반으로 계산하므로
            // 유닛 수집 완료 후에 호출한다.
            _factionStrength        = CalculateFactionStrength();
            _initialFactionStrength = _factionStrength;

            Debug.Log($"[FactionAI] 초기 팩션 전력={_factionStrength:F1}, " +
                      $"활성화 일수={_enemyActivationDay}. FactionId={_factionId}");

            // ── FactionTick 코루틴 시작 ─────────────────────────────────────────
            // 첫 틱의 deltaGameDays 과다 산출 방지: 현재 GameTime으로 초기화한다.
            _prevTickGameDay = GameManager.Instance != null ? GameManager.Instance.GameTime : 0f;
            _factionTickCoroutine = StartCoroutine(FactionTickCoroutine());
        }

        /// <summary>
        /// 이 GameObject가 파괴될 때 호출된다.
        /// FactionTick 코루틴을 명시적으로 정지한다.
        /// </summary>
        private void OnDestroy()
        {
            // 고아 코루틴 방지: OnDestroy에서 명시적 StopCoroutine 호출
            if (_factionTickCoroutine != null)
            {
                StopCoroutine(_factionTickCoroutine);
                _factionTickCoroutine = null;
            }
        }

        #endregion

        #region ── 공개 API ──

        /// <summary>
        /// GameManager가 Start()에서 호출하여 플레이어 기지 위치를 주입한다.
        /// 이 메서드 호출 이전에는 DEFAULT_PLAYER_BASE_X/Y가 사용된다.
        /// </summary>
        /// <param name="tileX">플레이어 기지 타일 X 좌표.</param>
        /// <param name="tileY">플레이어 기지 타일 Y 좌표.</param>
        public void InitializeBase(int tileX, int tileY)
        {
            _playerBaseTileX = tileX;
            _playerBaseTileY = tileY;

            Debug.Log($"[FactionAI] InitializeBase: 플레이어 기지 위치=({tileX},{tileY}) 설정 완료. " +
                      $"FactionId={_factionId}");
        }

        #endregion

        #region ── FactionTick 코루틴 ──

        /// <summary>
        /// 5초 간격으로 팩션 의사결정을 수행하는 메인 코루틴.
        ///
        /// 실행 순서:
        ///   1. 게임 일수 확인 → activationDay 미달 시 건너뜀
        ///   2. 팩션 전력 재계산
        ///   3. 침략 결정 평가 (팩션별 특수 규칙 포함)
        ///   4. 결정에 따른 유닛 지시 발행
        /// </summary>
        private IEnumerator FactionTickCoroutine()
        {
            // 5초 WaitForSeconds 객체를 미리 생성하여 매 틱 GC를 방지한다.
            WaitForSeconds wait = new WaitForSeconds(FACTION_TICK_INTERVAL);

            while (true)
            {
                yield return wait;

                float currentGameDay = GameManager.Instance != null ? GameManager.Instance.GameTime : 0f;
                float deltaGameDays  = currentGameDay - _prevTickGameDay;
                _prevTickGameDay     = currentGameDay;

                // ── Step 1: 침략 쿨다운 차감 ──────────────────────────────────────
                // 레이드 격퇴 후 _raidCooldownDays 게임일 동안 의사결정을 완전히 건너뛴다.
                if (_raidCooldownRemaining > 0f)
                {
                    _raidCooldownRemaining = Mathf.Max(0f, _raidCooldownRemaining - deltaGameDays);
                    if (_raidCooldownRemaining <= 0f)
                    {
                        Debug.Log($"[FactionAI] 침략 쿨다운 종료. 다음 Tick부터 침략 평가 재개. FactionId={_factionId}");
                    }
                    continue; // 쿨다운 중 — 평가 건너뜀
                }

                // ── Step 2: 활성화 일수 체크 ──────────────────────────────────────
                if (currentGameDay < _enemyActivationDay) continue;

                // 최초 활성화 로그 (한 번만 출력)
                if (!_isActivated)
                {
                    _isActivated = true;
                    Debug.Log($"[FactionAI] 팩션 활성화! Day={currentGameDay:F2}, FactionId={_factionId}");
                }

                // ── Step 3: 팩션 전력 재계산 ──────────────────────────────────────
                _factionStrength = CalculateFactionStrength();

                // ── Step 4: 플레이어 전력 계산 ────────────────────────────────────
                float playerStrength = GameManager.Instance != null
                    ? GameManager.Instance.CalculatePlayerStrength()
                    : 0f;

                // ── Step 5: 팩션 Goal 우선순위 평가 ───────────────────────────────
                EvaluateAndExecuteGoal(playerStrength, currentGameDay);
            }
        }

        /// <summary>
        /// 팩션 Goal 우선순위를 평가하고 해당하는 행동을 실행한다.
        ///
        /// Goal 우선순위:
        ///   [사전] 레이드 격퇴 감지 — 레이드 중 전 유닛 사망 → 쿨다운 시작
        ///   F-P0:  SurviveFaction  — factionStrength <= 초기값 × 0.3
        ///   F-P1:  DefendBase      — playerStrength > factionStrength × 1.2
        ///   F-P3:  Scout           — !_playerStrengthKnown (F-P2보다 먼저 체크하여 초회 정찰 전 침략 차단)
        ///   F-P2:  ExpandTerritory — RaidDecision = true (Attack 또는 Plunder 모드 자동 선택)
        ///   F-P4:  GatherFaction   — 현재 더미 (Idle 유지)
        /// </summary>
        private void EvaluateAndExecuteGoal(float playerStrength, float currentGameDay)
        {
            // ── 레이드 격퇴 감지: 레이드 중인데 전 유닛 사망 → 쿨다운 시작 ────────
            if (_isRaiding && CountAliveUnits() == 0)
            {
                _isRaiding             = false;
                _raidCooldownRemaining = _raidCooldownDays;
                ResetWarningState("레이드 격퇴 → 쿨다운 진입"); // F-B ADR-B1: 쿨다운 진입 시 예고 상태 초기화
                Debug.Log($"[FactionAI] 레이드 격퇴 감지 → 쿨다운 {_raidCooldownDays:F1}일 시작. FactionId={_factionId}");
                return;
            }

            // ── F-P0: SurviveFaction (생존 최우선) ────────────────────────────────
            if (_factionStrength <= _initialFactionStrength * SURVIVE_FACTION_RATIO)
            {
                Debug.Log($"[FactionAI] F-P0 SurviveFaction 발동. " +
                          $"현재 전력={_factionStrength:F1}, 초기×30%={_initialFactionStrength * SURVIVE_FACTION_RATIO:F1}. " +
                          $"FactionId={_factionId}");
                _isRaiding = false;
                ResetWarningState("SurviveFaction 진입 → 침략 계획 소멸"); // F-B ADR-B1
                // TODO: 기획팀 확인 필요 — SurviveFaction 시 방어 유닛 배치 방식
                return;
            }

            // ── F-P1: DefendBase (기지 방어) ──────────────────────────────────────
            if (playerStrength > _factionStrength * DEFEND_BASE_RATIO)
            {
                Debug.Log($"[FactionAI] F-P1 DefendBase 발동. " +
                          $"플레이어 전력({playerStrength:F1}) > 팩션×1.2({_factionStrength * DEFEND_BASE_RATIO:F1}). " +
                          $"FactionId={_factionId}");
                _isRaiding = false;
                ResetWarningState("DefendBase 진입 → 침략 계획 소멸"); // F-B ADR-B1
                // TODO: 기획팀 확인 필요 — 방어 유닛 배치 로직
                return;
            }

            // ── 정찰 정보 만료 체크 ────────────────────────────────────────────────
            // _scoutIntervalDays 게임일 경과 시 playerStrength 캐시를 무효화하여 재정찰을 예약한다.
            if (_playerStrengthKnown
                && currentGameDay >= _scoutStartDay
                && currentGameDay - _lastScoutCompletedDay >= _scoutIntervalDays)
            {
                _playerStrengthKnown = false;
                _isScoutActive       = false;
                Debug.Log($"[FactionAI] 정찰 정보 만료 → 재정찰 예약. FactionId={_factionId}");
            }

            // ── F-P3: Scout (정찰) — F-P2보다 먼저 체크하여 초회 정찰 전 침략 차단 ─
            if (currentGameDay >= _scoutStartDay && !_playerStrengthKnown)
            {
                // 이미 정찰 명령이 발령됐으면 완료 여부를 체크한다.
                if (_isScoutActive)
                {
                    int unitCount = _units.Count;
                    for (int i = 0; i < unitCount; i++)
                    {
                        EnemyFSM unit = _units[i];
                        if (unit == null || !unit.IsAlive || unit.Brain == null) continue;
                        if (!unit.Brain.ScoutCompleted) continue;

                        // 정찰 완료 처리
                        unit.Brain.ScoutCompleted = false;
                        _playerStrengthKnown      = true;
                        _lastScoutCompletedDay    = currentGameDay;
                        _isScoutActive            = false;
                        Debug.Log($"[FactionAI] F-P3 정찰 완료 — playerStrength 파악됨. " +
                                  $"Day={currentGameDay:F2}. FactionId={_factionId}");
                        break;
                    }
                }

                // 정찰 완료 전까지 침략 차단
                if (!_playerStrengthKnown)
                {
                    if (!_isScoutActive)
                        IssueScoutOrder();
                    return;
                }
            }

            // ── F-P2: ExpandTerritory (침략 예고 스테이지 머신, F-B ADR-B1) ─────
            // 조건 충족 시 즉시 침략이 아니라 Rumor(D-3) → Confirmed(D-1) → Raid(D0) 순서로 진행.
            // 같은 Tick 이중 전이 금지 — 각 전이는 반드시 다음 Tick에서만 발생.
            if (EvaluateRaidDecision(playerStrength))
            {
                // 상인 연합 특수 규칙: RaidDecision 전 TradeProposal 1회 발행 (ADR-B5: 예고 이전 단계)
                if (_factionId == FACTION_MERCHANT && !_tradeProposalSent)
                {
                    PublishTradeProposal();
                    _tradeProposalSent = true;
                    Debug.Log($"[FactionAI] 상인 연합 TradeProposal 발행 완료. 다음 Tick에 RaidDecision 평가. " +
                              $"FactionId={_factionId}");
                    return;
                }

                // Stage 진입: None → Rumor
                if (_warningStage == WSTAGE_NONE)
                {
                    _warningStage    = WSTAGE_RUMOR;
                    _expectedRaidDay = currentGameDay + WARNING_LEAD_DAYS_RUMOR;
                    PublishInvasionWarning(MessageBus.INVASION_STAGE_RUMOR, currentGameDay);
                    _rumorPublished  = true;
                    return;
                }

                float leadRemaining = _expectedRaidDay - currentGameDay;

                // Stage 전이: Rumor → Confirmed (남은 lead가 CONFIRMED 임계 이하로 진입)
                if (_warningStage == WSTAGE_RUMOR && leadRemaining <= WARNING_LEAD_DAYS_CONFIRMED)
                {
                    _warningStage = WSTAGE_CONFIRMED;
                    PublishInvasionWarning(MessageBus.INVASION_STAGE_CONFIRMED, currentGameDay);
                    _confirmedPublished = true;
                    return;
                }

                // Stage 종료: Confirmed → 실제 침략 개시 (D0 도달)
                if (_warningStage == WSTAGE_CONFIRMED && leadRemaining <= 0f)
                {
                    if (!_isRaiding)
                    {
                        _isRaiding = true;
                        RaidMode mode = DetermineRaidMode(playerStrength);
                        PublishRaidDecision(playerStrength);
                        IssueRaidOrders(mode);
                        // 다음 침략 사이클 대비 초기화 (쿨다운 진입 없어도 초기화 필요)
                        ResetWarningState("실제 침략 개시 → 스테이지 머신 초기화");
                    }
                    return;
                }

                // 스테이지 유지(예고 진행 중) — 이번 Tick은 아무 것도 안 함
                return;
            }

            // ── 예고 중 조건 소멸: 침략 조건이 도중에 깨졌으면 예고 취소 (ADR-B1) ───
            if (_warningStage != WSTAGE_NONE)
            {
                ResetWarningState("EvaluateRaidDecision=false → 침략 조건 소멸");
            }

            // ── F-P4: GatherFaction (더미) — 레이드 조건 미충족 시 Idle 유지 ──────
            if (_isRaiding)
            {
                _isRaiding = false;
                Debug.Log($"[FactionAI] 레이드 조건 미충족 → 레이드 중단. FactionId={_factionId}");
            }
        }

        /// <summary>
        /// F-B ADR-B1: 예고 상태 머신을 초기 상태(WSTAGE_NONE)로 리셋한다.
        /// 호출 지점: 쿨다운 진입, SurviveFaction/DefendBase 진입, 조건 소멸, 실제 침략 개시.
        /// </summary>
        private void ResetWarningState(string reason)
        {
            if (_warningStage == WSTAGE_NONE) return; // 이미 초기 상태면 로그 스팸 방지

            Debug.Log($"[FactionAI] 예고 상태 리셋. 이유='{reason}', " +
                      $"이전 Stage={_warningStage}, ExpectedRaidDay={_expectedRaidDay:F1}, FactionId={_factionId}");
            _warningStage       = WSTAGE_NONE;
            _expectedRaidDay    = -1f;
            _rumorPublished     = false;
            _confirmedPublished = false;
        }

        /// <summary>
        /// F-B: 침략 예고 메시지 발행. Rumor(Medium Priority) 또는 Confirmed(High Priority)
        /// 스테이지에 따라 우선순위가 달라지므로 MessageBus.DEFAULT_PRIORITY_MAP에 등록하지 않고
        /// 이 메서드에서 명시 설정한다(FB-1 map 주석 [F-B 예외] 참조).
        /// UI 배너(InvasionWarningIndicator)와 주민 대사(VillagerFSM.OnInvasionWarning)가
        /// 이 메시지를 구독한다.
        /// </summary>
        private void PublishInvasionWarning(string stageId, float currentGameDay)
        {
            float leadRemain = Mathf.Max(0f, _expectedRaidDay - currentGameDay);

            if (MessageBus.Instance == null)
            {
                Debug.LogWarning($"[FactionAI] PublishInvasionWarning: MessageBus.Instance가 null입니다. " +
                                 $"메시지를 발행할 수 없습니다. Stage={stageId}, FactionId={_factionId}");
                return;
            }

            var payload = new MessageBus.InvasionWarningPayload
            {
                FactionId          = _factionId.ToString(),
                StageId            = stageId,
                ExpectedRaidDay    = Mathf.RoundToInt(_expectedRaidDay),
                LeadDaysRemaining  = leadRemain,
                FactionNameKnown   = _playerStrengthKnown  // ADR-B4: 정찰 완료 시에만 팩션명 노출
            };

            MessageBus.Instance.Publish(new AIMessage
            {
                Type     = MessageType.InvasionWarning,
                Priority = (stageId == MessageBus.INVASION_STAGE_CONFIRMED)
                           ? MessagePriority.High
                           : MessagePriority.Medium, // Rumor는 EnemyDetected 큐와 섞이지 않도록 Medium
                SenderId = $"faction_{_factionId}",
                Payload  = payload,
                IssuedAt = Time.time
            });

            Debug.Log($"[FactionAI] InvasionWarning 발행. Stage={stageId}, " +
                      $"ExpectedDay={_expectedRaidDay:F1}, LeadRemain={leadRemain:F1}일, " +
                      $"NameKnown={_playerStrengthKnown}, FactionId={_factionId}");
        }

        /// <summary>
        /// 전면 공격(Attack)과 약탈(Plunder) 중 어떤 전술을 선택할지 결정한다.
        ///
        /// Plunder 선택 조건:
        ///   playerStrength >= factionStrength × PLUNDER_STRENGTH_RATIO (전면 공격 불리)
        ///   AND (copperStock &lt; PLUNDER_COPPER_MIN OR silverStock &lt; PLUNDER_SILVER_MIN) (자원 긴급)
        /// </summary>
        private RaidMode DetermineRaidMode(float playerStrength)
        {
            bool playerTooStrong = playerStrength >= _factionStrength * PLUNDER_STRENGTH_RATIO;
            bool resourceUrgent  = (_copperStock < PLUNDER_COPPER_MIN) || (_silverStock < PLUNDER_SILVER_MIN);

            if (playerTooStrong && resourceUrgent)
            {
                Debug.Log($"[FactionAI] RaidMode.Plunder 선택. " +
                          $"playerStrength={playerStrength:F1} >= factionStrength×{PLUNDER_STRENGTH_RATIO}={_factionStrength * PLUNDER_STRENGTH_RATIO:F1}, " +
                          $"copper={_copperStock:F1}, silver={_silverStock:F1}. FactionId={_factionId}");
                return RaidMode.Plunder;
            }

            return RaidMode.Attack;
        }

        #endregion

        #region ── 팩션 전력 계산 ──

        /// <summary>
        /// 팩션별 공식으로 현재 factionStrength를 계산한다.
        /// 죽은 유닛(IsAlive=false)은 계산에서 제외한다.
        ///
        /// 공식:
        ///   숲의 부족: (생존 유닛 수 × 10) + 25
        ///   철의 도시: (생존 유닛 수 × 12) + 35
        ///   상인 연합: (생존 유닛 수 × 8) + 20
        /// </summary>
        private float CalculateFactionStrength()
        {
            // 생존 유닛 수 계산 (null 방어 + IsAlive 체크)
            int aliveCount = 0;
            int unitCount = _units.Count;
            for (int i = 0; i < unitCount; i++)
            {
                EnemyFSM unit = _units[i];
                if (unit != null && unit.IsAlive)
                {
                    aliveCount++;
                }
            }

            // 팩션별 공식 적용 (기획서 수치)
            switch (_factionId)
            {
                case FACTION_FOREST:
                    // 숲의 부족: (유닛 수 × 10) + 25
                    return (aliveCount * FOREST_UNIT_WEIGHT) + FOREST_BASE_STRENGTH;

                case FACTION_IRON:
                    // 철의 도시: (유닛 수 × 12) + 35
                    return (aliveCount * IRON_UNIT_WEIGHT) + IRON_BASE_STRENGTH;

                case FACTION_MERCHANT:
                    // 상인 연합: (유닛 수 × 8) + 20
                    return (aliveCount * MERCHANT_UNIT_WEIGHT) + MERCHANT_BASE_STRENGTH;

                default:
                    // ⚠️ 알 수 없는 팩션 ID — 기획팀 확인 후 수정 필요
                    Debug.LogWarning($"[FactionAI] CalculateFactionStrength: 알 수 없는 FactionId={_factionId}. " +
                                     "기본 공식(×10+25)을 사용합니다.");
                    return (aliveCount * FOREST_UNIT_WEIGHT) + FOREST_BASE_STRENGTH;
            }
        }

        #endregion

        #region ── 침략 결정 평가 ──

        /// <summary>
        /// 침략 트리거 조건을 평가하여 레이드 여부를 반환한다.
        ///
        /// 공통 트리거 (GDD v0.4 확정값):
        ///   (copperStock &lt; 10 OR silverStock &lt; 5)
        ///   AND nearPlayerTerritory == true  (팩션 유닛이 플레이어 영역 25타일 이내)
        ///   AND playerStrength &lt; factionStrength × 0.8
        ///
        /// 팩션별 추가 트리거:
        ///   숲의 부족 (0): rawFoodStock &lt; 30이면 공통 조건 무시하고 추가 침략
        ///   철의 도시 (1): ironStock &lt; 15이면 공통 조건 무시하고 즉시 침략
        ///   상인 연합 (2): TradeProposal 발행 전에는 RaidDecision 반환 안 함
        /// </summary>
        private bool EvaluateRaidDecision(float playerStrength)
        {
            // ── 팩션별 즉시 트리거 (공통 조건 무시) ─────────────────────────────

            if (_factionId == FACTION_FOREST)
            {
                // 숲의 부족 특수: 식량 < 30이면 즉시 침략
                if (_rawFoodStock < FOREST_FOOD_RAID_THRESHOLD)
                {
                    Debug.Log($"[FactionAI] 숲의 부족 즉시 트리거: rawFood={_rawFoodStock:F1} < {FOREST_FOOD_RAID_THRESHOLD}. " +
                              $"FactionId={_factionId}");
                    return true;
                }
            }
            else if (_factionId == FACTION_IRON)
            {
                // 철의 도시 특수: iron < 15이면 즉시 침략
                if (_ironStock < IRON_IRON_RAID_THRESHOLD)
                {
                    Debug.Log($"[FactionAI] 철의 도시 즉시 트리거: iron={_ironStock:F1} < {IRON_IRON_RAID_THRESHOLD}. " +
                              $"FactionId={_factionId}");
                    return true;
                }
            }
            // 상인 연합(FACTION_MERCHANT)은 TradeProposal 발행 전에는 이 분기까지 오지 않는다.
            // EvaluateAndExecuteGoal()에서 TradeProposal 체크 후 다음 Tick에 이 메서드가 호출된다.
            // TODO: [기획서 확정 필요] 상인 연합 관계도(RelationScore) 시스템 미구현
            // 기획서 수치: RelationScore >= 40이면 레이드 불가
            // 구현 시: private float _relationScore = 0f; 필드 추가 후 여기서 체크.
            // if (_factionId == FACTION_MERCHANT && _relationScore >= 40f) return false;

            // ── 공통 침략 트리거 ──────────────────────────────────────────────────

            // 조건 1: copperStock < 10 OR silverStock < 5
            bool resourcePressure = (_copperStock < COPPER_RAID_THRESHOLD)
                                 || (_silverStock < SILVER_RAID_THRESHOLD);

            if (!resourcePressure) return false;

            // 조건 2: nearPlayerTerritory — 팩션 유닛이 플레이어 영역 25타일 이내
            // 생존 유닛 중 하나라도 플레이어 기지 25타일 이내이면 true
            bool nearPlayerTerritory = IsAnyUnitNearPlayerTerritory();

            if (!nearPlayerTerritory) return false;

            // 조건 3: playerStrength < factionStrength × 0.8
            // 플레이어가 충분히 약할 때만 침략 (우세한 상황에서만 공격)
            bool playerWeak = playerStrength < _factionStrength * STRENGTH_RATIO_THRESHOLD;

            if (!playerWeak) return false;

            Debug.Log($"[FactionAI] 공통 침략 트리거 충족. " +
                      $"copper={_copperStock:F1}, silver={_silverStock:F1}, " +
                      $"playerStrength={playerStrength:F1} < factionStrength×0.8={_factionStrength * STRENGTH_RATIO_THRESHOLD:F1}. " +
                      $"FactionId={_factionId}");
            return true;
        }

        /// <summary>
        /// 소속 유닛 중 하나라도 플레이어 기지 PLAYER_TERRITORY_DIST(25타일) 이내에 있는지 확인한다.
        /// </summary>
        private bool IsAnyUnitNearPlayerTerritory()
        {
            int unitCount = _units.Count;
            for (int i = 0; i < unitCount; i++)
            {
                EnemyFSM unit = _units[i];
                if (unit == null || !unit.IsAlive) continue;

                // 맨해튼 거리 계산
                int dist = Mathf.Abs(unit.TileX - _playerBaseTileX)
                         + Mathf.Abs(unit.TileY - _playerBaseTileY);

                if (dist <= PLAYER_TERRITORY_DIST) return true;
            }
            return false;
        }

        #endregion

        #region ── MessageBus 발행 메서드 ──

        /// <summary>
        /// 침략 결정을 MessageBus를 통해 발행한다.
        /// EnemyDetected (위협 알림)와 RaidDecision (전략 정보) 두 메시지를 모두 발행한다.
        ///
        /// 설계 결정: EnemyFSM 개별이 아닌 FactionAI.IssueRaidOrders()에서 1회만 발행.
        /// 이로써 같은 Tick에 여러 유닛이 중복으로 EnemyDetected를 발행하는 폭주를 방지한다.
        /// (MessageBus의 Dedup이 EnemyFactionId 기반으로 중복을 1회로 합치지만 발행 자체를 줄인다)
        /// </summary>
        private void PublishRaidDecision(float playerStrength)
        {
            if (MessageBus.Instance == null)
            {
                Debug.LogWarning($"[FactionAI] PublishRaidDecision: MessageBus.Instance가 null입니다. " +
                                 $"메시지를 발행할 수 없습니다. FactionId={_factionId}");
                return;
            }

            float currentGameDay = GameManager.Instance != null ? GameManager.Instance.GameTime : 0f;

            // ── 참여 유닛 ID 목록 구성 ──────────────────────────────────────────
            // 생존 유닛의 EnemyId 배열을 만들어 페이로드에 포함한다.
            var participatingIds = new List<string>(_units.Count);
            for (int i = 0; i < _units.Count; i++)
            {
                EnemyFSM unit = _units[i];
                if (unit != null && unit.IsAlive)
                {
                    participatingIds.Add(unit.EnemyId);
                }
            }

            // ── RaidDecision 메시지 발행 ─────────────────────────────────────────
            var raidPayload = new MessageBus.RaidDecisionPayload
            {
                FactionId             = _factionId.ToString(),
                TargetFactionId       = "player",          // 플레이어 마을이 항상 타깃
                RaidStartTileX        = _baseTileX,        // 집결 위치 = 팩션 기지
                RaidStartTileY        = _baseTileY,
                ParticipatingUnitIds  = participatingIds.ToArray(),
                EstimatedStrength     = _factionStrength,
                RaidTriggerReason     = DetermineRaidTriggerReason(),
                ActivationDay         = Mathf.RoundToInt(currentGameDay)
            };

            MessageBus.Instance.Publish(new AIMessage
            {
                Type     = MessageType.RaidDecision,
                Priority = MessagePriority.High,
                SenderId = $"faction_{_factionId}",
                Payload  = raidPayload,
                IssuedAt = Time.time
            });

            // ── EnemyDetected 메시지 발행 ────────────────────────────────────────
            // 주민의 NearEnemy → LOD_Alert 전이를 유발하기 위해 발행한다.
            // FactionAI에서 1회만 발행하여 MessageBus Dedup이 처리하기 전에 중복을 차단한다.
            var enemyPayload = new MessageBus.EnemyDetectedPayload
            {
                EnemyTileX           = _baseTileX,         // 팩션 기지 = 집결 위치
                EnemyTileY           = _baseTileY,
                EnemyFactionId       = _factionId.ToString(),
                ThreatTier           = RAID_THREAT_TIER,    // 기획서 수치: 팩션 레이드 = 4등급
                DetectedByVillagerId = $"faction_{_factionId}",
                DetectedAt           = currentGameDay
            };

            MessageBus.Instance.Publish(new AIMessage
            {
                Type     = MessageType.EnemyDetected,
                Priority = MessagePriority.High,
                SenderId = $"faction_{_factionId}",
                Payload  = enemyPayload,
                IssuedAt = Time.time
            });

            Debug.Log($"[FactionAI] RaidDecision + EnemyDetected 발행 완료. " +
                      $"FactionId={_factionId}, 참여 유닛={participatingIds.Count}명, " +
                      $"전력={_factionStrength:F1}");
        }

        /// <summary>
        /// 상인 연합 전용: RaidDecision 전 TradeProposal을 MessageBus로 발행한다.
        /// RaidDecision 대신 외교적 제안을 먼저 시도한다.
        /// TODO: 기획팀 확인 필요 — TradeProposal 수락/거절 처리 시스템 미구현
        /// </summary>
        private void PublishTradeProposal()
        {
            if (MessageBus.Instance == null)
            {
                Debug.LogWarning($"[FactionAI] PublishTradeProposal: MessageBus.Instance가 null입니다. " +
                                 $"FactionId={_factionId}");
                return;
            }

            // RaidDecision 타입으로 발행하되 RaidTriggerReason에 "TradeProposal"로 구분한다.
            // TODO: 기획팀 확인 필요 — TradeProposal 전용 MessageType 추가 여부
            float currentGameDay = GameManager.Instance != null ? GameManager.Instance.GameTime : 0f;

            var tradePayload = new MessageBus.RaidDecisionPayload
            {
                FactionId             = _factionId.ToString(),
                TargetFactionId       = "player",
                RaidStartTileX        = _baseTileX,
                RaidStartTileY        = _baseTileY,
                ParticipatingUnitIds  = System.Array.Empty<string>(), // 무력 시위 없음
                EstimatedStrength     = _factionStrength,
                RaidTriggerReason     = "TradeProposal",  // 침략이 아님을 명시
                ActivationDay         = Mathf.RoundToInt(currentGameDay)
            };

            MessageBus.Instance.Publish(new AIMessage
            {
                Type     = MessageType.RaidDecision,
                Priority = MessagePriority.High,
                SenderId = $"faction_{_factionId}",
                Payload  = tradePayload,
                IssuedAt = Time.time
            });

            Debug.Log($"[FactionAI] 상인 연합 TradeProposal 발행. FactionId={_factionId}");
        }

        /// <summary>
        /// 침략 트리거 원인을 문자열로 반환한다. RaidDecisionPayload.RaidTriggerReason에 사용.
        /// </summary>
        private string DetermineRaidTriggerReason()
        {
            if (_factionId == FACTION_FOREST && _rawFoodStock < FOREST_FOOD_RAID_THRESHOLD)
                return "FoodShortage";

            if (_factionId == FACTION_IRON && _ironStock < IRON_IRON_RAID_THRESHOLD)
                return "IronShortage";

            if (_copperStock < COPPER_RAID_THRESHOLD)
                return "CopperShortage";

            if (_silverStock < SILVER_RAID_THRESHOLD)
                return "SilverShortage";

            return "ResourcePressure";
        }

        #endregion

        #region ── 유닛 지시 메서드 ──

        /// <summary>
        /// 생존 중인 모든 소속 유닛에게 레이드 이동 명령을 내린다.
        /// mode에 따라 Attacking(전면) 또는 Plundering(약탈) 전술로 분기한다.
        /// </summary>
        private void IssueRaidOrders(RaidMode mode = RaidMode.Attack)
        {
            int issued = 0;
            int unitCount = _units.Count;

            for (int i = 0; i < unitCount; i++)
            {
                EnemyFSM unit = _units[i];

                if (unit == null)
                {
                    Debug.LogWarning($"[FactionAI] IssueRaidOrders: units[{i}]가 null입니다. FactionId={_factionId}");
                    continue;
                }

                if (!unit.IsAlive) continue;

                unit.SetTargetTile(_playerBaseTileX, _playerBaseTileY, mode);
                issued++;
            }

            Debug.Log($"[FactionAI] 레이드 명령 발행 완료. mode={mode}, 명령 유닛={issued}명, " +
                      $"목표=({_playerBaseTileX},{_playerBaseTileY}). FactionId={_factionId}");
        }

        /// <summary>
        /// 유휴 유닛 1명을 선발하여 플레이어 기지 방향으로 정찰 명령을 내린다.
        /// 정찰 유닛은 Scouting 상태로 전이하며, 도달 후 Brain.ScoutCompleted=true를 설정한다.
        /// FactionAI가 다음 Tick에 ScoutCompleted를 감지하여 _playerStrengthKnown=true 처리한다.
        /// </summary>
        private void IssueScoutOrder()
        {
            EnemyFSM scout = null;
            int unitCount = _units.Count;

            for (int i = 0; i < unitCount; i++)
            {
                EnemyFSM unit = _units[i];
                if (unit == null || !unit.IsAlive || unit.Brain == null) continue;
                if (unit.Brain.FSMState != EnemyState.Idle) continue;

                scout = unit;
                break;
            }

            if (scout == null)
            {
                Debug.Log($"[FactionAI] 정찰 발령 실패: 유휴 유닛 없음. FactionId={_factionId}");
                return;
            }

            scout.SetScoutTarget(_playerBaseTileX, _playerBaseTileY);
            _isScoutActive = true;
            Debug.Log($"[FactionAI] F-P3 정찰 발령. EnemyId={scout.EnemyId}. FactionId={_factionId}");
        }

        #endregion

        #region ── 공개 자원 수정 API ──

        /// <summary>
        /// 팩션 자원 재고를 외부에서 수정할 때 사용한다.
        /// 이벤트 기반 자원 감소 (예: 주민이 팩션 자원을 약탈) 시 호출된다.
        /// TODO: 기획팀 확인 필요 — 팩션 자원 자동 감소 방식 및 수급 시스템 결정 필요
        /// </summary>
        public void ModifyFactionResource(string resourceName, float delta)
        {
            switch (resourceName)
            {
                case "Copper":   _copperStock   = Mathf.Max(0f, _copperStock   + delta); break;
                case "Silver":   _silverStock   = Mathf.Max(0f, _silverStock   + delta); break;
                case "Iron":     _ironStock     = Mathf.Max(0f, _ironStock     + delta); break;
                case "RawFood":  _rawFoodStock  = Mathf.Max(0f, _rawFoodStock  + delta); break;
                default:
                    Debug.LogWarning($"[FactionAI] ModifyFactionResource: 알 수 없는 자원 '{resourceName}'. " +
                                     $"FactionId={_factionId}");
                    break;
            }
        }

        #endregion

        #region ── 런타임 유닛 헬퍼 ──

        /// <summary>소속 유닛 중 생존한(IsAlive=true) 유닛의 수를 반환한다. 프로덕션 코드 경로에서 사용.</summary>
        private int CountAliveUnits()
        {
            int count = 0;
            for (int i = 0; i < _units.Count; i++)
            {
                if (_units[i] != null && _units[i].IsAlive) count++;
            }
            return count;
        }

        #endregion

#if UNITY_EDITOR
        #region ── Unity Editor 디버그 헬퍼 ──

        /// <summary>
        /// Inspector 우클릭 → "DEBUG: 팩션 상태 출력" 으로 현재 상태를 Console에 출력한다.
        /// </summary>
        [ContextMenu("DEBUG: 팩션 상태 출력 (Print Faction Status)")]
        private void DebugPrintFactionStatus()
        {
            float currentGameDay = GameManager.Instance != null ? GameManager.Instance.GameTime : 0f;

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"[FactionAI] 팩션 상태 ─────────────────────────────────────");
            sb.AppendLine($"  FactionId       : {_factionId} ({GetFactionName()})");
            sb.AppendLine($"  IsActivated     : {_isActivated}");
            sb.AppendLine($"  IsRaiding       : {_isRaiding}");
            sb.AppendLine($"  CurrentGameDay  : {currentGameDay:F2}");
            sb.AppendLine($"  ActivationDay   : {_enemyActivationDay}");
            sb.AppendLine($"  FactionStrength : {_factionStrength:F1} (초기={_initialFactionStrength:F1})");
            sb.AppendLine($"  Base            : ({_baseTileX},{_baseTileY})");
            sb.AppendLine($"  PlayerBase      : ({_playerBaseTileX},{_playerBaseTileY})");
            sb.AppendLine($"  Units           : {_units.Count}명 (생존={CountAliveUnits()}명)");
            sb.AppendLine($"  Resources        : Copper={_copperStock:F1}, Silver={_silverStock:F1}, " +
                          $"Iron={_ironStock:F1}, Food={_rawFoodStock:F1}");
            sb.AppendLine($"  RaidCooldown     : {_raidCooldownRemaining:F2}일 남음");
            sb.AppendLine($"  ScoutKnown       : {_playerStrengthKnown} (마지막={_lastScoutCompletedDay:F1}일, 간격={_scoutIntervalDays}일)");
            sb.AppendLine($"  ScoutActive      : {_isScoutActive} (시작일={_scoutStartDay})");
            if (_factionId == FACTION_MERCHANT)
                sb.AppendLine($"  TradeProposalSent: {_tradeProposalSent}");
            sb.AppendLine($"──────────────────────────────────────────────────────────────");

            Debug.Log(sb.ToString());
        }

        private string GetFactionName()
        {
            switch (_factionId)
            {
                case FACTION_FOREST:   return "숲의 부족";
                case FACTION_IRON:     return "철의 도시";
                case FACTION_MERCHANT: return "상인 연합";
                default:               return "알 수 없는 팩션";
            }
        }

        #endregion
#endif
    }
}
