/// <summary>
/// GameManager.cs - AI Village 게임의 중앙 초기화 및 틱 관리자
///
/// 역할(Role): 씬에 배치된 모든 AI 시스템(SensorSystem, MessageBus, BuildingQueue)과
///             코드로 생성하는 순수 C# 시스템(AuthoritativeWorldState, ResourceRegistry)을
///             연결하고, 0.1초 간격의 게임 틱 루프를 구동한다.
///
/// 사용법(Usage):
///   1. 씬에 빈 GameObject ("_GameManager" 등)를 만들고 이 컴포넌트를 부착한다.
///   2. Inspector의 _actionDatabase 슬롯에 ActionDatabase ScriptableObject를 드래그한다.
///   3. SensorSystem, MessageBus, BuildingQueue는 별도 GameObject에 부착하여 씬에 배치한다.
///   4. 씬에 VillagerFSM 컴포넌트를 가진 주민 GameObject를 배치한다.
///
/// 의존성(Dependencies):
///   - AuthoritativeWorldState.cs, ResourceRegistry.cs (순수 C# — 코드로 생성)
///   - SensorSystem.cs, MessageBus.cs, BuildingQueue.cs (MonoBehaviour — 씬에 배치)
///   - VillagerFSM.cs (AI 레이어 — 씬에 배치)
///   - ActionDatabase.cs (ScriptableObject — Inspector 드래그 연결)
///   - ResourceNode.cs (순수 C# — Awake에서 코드로 생성)
///
/// 초기화 순서 (Script Execution Order):
///   MessageBus(-100) → BuildingQueue(-90) → GameManager(-80) → VillagerFSM(0)
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-29 (마일스톤 이벤트: Forge→전사 합류, Day7→탐험가 합류)
/// </summary>

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using AIVillage.Core;
using AIVillage.AI;
using AIVillage.UI;
using System.Linq; // FactionAI 수집 시 Where 사용

namespace AIVillage.Core
{
    /// <summary>
    /// 모든 AI 시스템의 중앙 초기화 및 틱 관리자.
    /// MessageBus(-100), BuildingQueue(-90) 이후, VillagerFSM(0) 이전에 초기화된다.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════════
        // 싱글턴
        // ══════════════════════════════════════════════════════════════════════

        #region ── 싱글턴 ──

        /// <summary>
        /// GameManager의 씬 내 유일한 인스턴스.
        /// 외부 시스템이 Registry나 GameTime에 접근할 때 사용한다.
        /// </summary>
        public static GameManager Instance { get; private set; }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Serialized Fields (Inspector)
        // ══════════════════════════════════════════════════════════════════════

        #region ── Serialized Fields ──

        // ── 의존성 주입 ────────────────────────────────────────────────────────

        // ── [13단계] MapConfig 에셋 참조 ──────────────────────────────────────────
        // Awake() 첫 줄에서 MapConfig.SetActive()를 호출하기 위해 필요하다.
        // 모든 시스템 중 가장 먼저 처리되어야 하므로 필드 선언 순서도 첫 번째로 배치한다.
        [Tooltip("맵 크기·FoW 수치·타일 색상을 담은 ScriptableObject. " +
                 "Project 창에서 Create → AI Village → MapConfig로 생성 후 이 슬롯에 드래그한다. " +
                 "연결하지 않으면 FowManager와 MapChunkRenderer가 동작하지 않는다.")]
        [SerializeField] private MapConfig _mapConfig;

        [Tooltip("GDD v0.4 Action 정의 전체를 담고 있는 ScriptableObject 에셋. " +
                 "프로젝트 창 우클릭 → Create → AIVillage → Action Database로 생성 후 드래그한다.")]
        [SerializeField] private ActionDatabase _actionDatabase;

        [Tooltip("자원 노드 랜덤 스폰 시스템. 씬의 _ResourceNodeSpawner GameObject를 연결한다. " +
                 "연결하지 않으면 노드 없이 시작하므로 반드시 연결할 것.")]
        [SerializeField] private ResourceNodeSpawner _resourceNodeSpawner;

        [Tooltip("마일스톤 이벤트(Forge 완성, Day7)에서 동적으로 생성할 주민 Prefab. " +
                 "씬의 Villager GameObject를 Assets/Prefab/Villager.prefab으로 저장 후 연결한다.")]
        [SerializeField] private GameObject _villagerPrefab;

        [Tooltip("주민 머리 위 행동 아이콘에 사용할 한국어 폰트. " +
                 "Assets/TextMesh Pro/Fonts/NeoDunggeunmoPro-Regular SDF 에셋을 드래그해 연결한다.")]
        [SerializeField] private TMP_FontAsset _koreanFont;

        // ── 초기 자원 재고 (GDD v0.4 기획서 수치) ────────────────────────────

        // [PR Fix]: G-002 — Inspector 초기값을 GDD v0.4 기획서 수치로 교정 (기존 값: Wood=50, Stone=50, CookedFood=20, Iron=10, Copper=5)
        [Tooltip("게임 시작 시 나무 재고량. 기획서 수치: 10")]
        [SerializeField] private float _initialWoodStock = 10f;        // GDD v0.4 수치

        [Tooltip("게임 시작 시 돌 재고량. 기획서 수치: 5")]
        [SerializeField] private float _initialStoneStock = 5f;        // GDD v0.4 수치

        [Tooltip("게임 시작 시 생 식량 재고량. 기획서 수치: 30")]
        [SerializeField] private float _initialRawFoodStock = 30f;     // GDD v0.4 수치 (유지)

        [Tooltip("게임 시작 시 조리된 식량 재고량. 기획서 수치: 0")]
        [SerializeField] private float _initialCookedFoodStock = 0f;   // GDD v0.4 수치

        [Tooltip("게임 시작 시 철 재고량. 기획서 수치: 0")]
        [SerializeField] private float _initialIronStock = 0f;         // GDD v0.4 수치

        [Tooltip("게임 시작 시 구리 재고량. 기획서 수치: 0")]
        [SerializeField] private float _initialCopperStock = 0f;       // GDD v0.4 수치

        // ── 기지 위치 및 FoW 초기 탐험 범위 ─────────────────────────────────

        [Tooltip("기지 타일 X 좌표. 맵 중심(원점)에 위치한다.")]
        [SerializeField] private int _baseTileX = 0;

        [Tooltip("기지 타일 Y 좌표. 맵 중심(원점)에 위치한다.")]
        [SerializeField] private int _baseTileY = 0;

        [Tooltip("게임 시작 시 기지 주변 초기 탐험 반경 (타일 맨해튼 거리). " +
                 "이 반경 이내의 자원 노드는 IsDiscovered=true로 시작한다. " +
                 "MapConfig.initialRevealRadius(기본 15)와 일치시킬 것. " +
                 "SpawnConfig.minDistanceFromBase보다 작으면 초기 발견 노드가 0개가 된다. " +
                 "전맵 공개 테스트 시 60 이상으로 설정한다.")]
        [SerializeField] private int _baseDiscoverRadius = 15;

        // ── 게임 시간 배율 ─────────────────────────────────────────────────────

        [Tooltip("1 실제 초 = 몇 게임 일. 기획서 수치: 0.01f (100초 = 1게임일). " +
                 "값을 크게 할수록 게임 시간이 빠르게 흐른다.")]
        [SerializeField] private float _gameTimeScale = 0.01f;         // 기획서 수치

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private Fields
        // ══════════════════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════════════════
        // 상수 [11단계 Win/Lose 기획서 확정 수치]
        // ══════════════════════════════════════════════════════════════════════

        #region ── 상수 ──

        /// <summary>승리 1 조건: 최소 생존 일수. 기획서 확정 수치: 30일.</summary>
        private const float WIN1_SURVIVAL_DAYS = 30f;  // 기획서 수치: 30일 생존

        /// <summary>승리 1 조건: 최소 생존 주민 수. 기획서 확정 수치: 5명.</summary>
        private const int WIN1_MIN_VILLAGERS = 5;      // 기획서 수치: 주민 5명 이상

        /// <summary>패배 2 조건: 최대 허용 생존 주민 수. 기획서 확정 수치: 2명 이하.</summary>
        private const int LOSE_TOWNHALL_MAX_VILLAGERS = 2; // 기획서 수치: 주민 2명 이하

        #endregion

        #region ── Private Fields ──

        // ── 순수 C# 시스템 (코드로 생성) ──────────────────────────────────────
        private AuthoritativeWorldState _worldState;
        private ResourceRegistry        _registry;

        // ── 씬에 배치된 주민 목록 ──────────────────────────────────────────────
        // Start()에서 FindObjectsOfType으로 초기 수집, 런타임 추가/제거 지원
        private List<VillagerFSM> _villagerFSMs = new List<VillagerFSM>();

        // ── 6B단계 추가: 씬에 배치된 FactionAI 목록 ────────────────────────────
        // Start()에서 FindObjectsOfType으로 초기 수집한다.
        // FactionAI는 런타임 추가/제거가 없으므로 배열로 캐시한다.
        private FactionAI[] _factionAIs;

        // ── VillageAdvisor: 자율 건물 결정 시스템 ───────────────────────────────
        private VillageAdvisor _villageAdvisor;

        // ── [15단계] 주민 모집 시스템 ────────────────────────────────────────────
        // Start() Step 7-a에서 FindObjectOfType으로 수집한다.
        // RecruitmentSystem은 자체 싱글턴(Instance)을 갖고 있으므로 직접 호출 없이 참조만 보관한다.
        private RecruitmentSystem _recruitmentSystem;

        // ── 6B단계 추가: 씬에 배치된 EnemyFSM 목록 ─────────────────────────────
        // GameManager의 틱 루프에서 VillagerFSM과 동일하게 그룹별 Tick()을 호출한다.
        private List<EnemyFSM> _enemyFSMs = new List<EnemyFSM>();

        // ── [8단계] 전투 시스템용 공개 읽기 전용 접근자 ─────────────────────────
        /// <summary>EnemyFSM이 공격 대상 탐색에 사용하는 주민 목록 (읽기 전용).</summary>
        public IReadOnlyList<VillagerFSM> Villagers => _villagerFSMs;

        /// <summary>VillagerFSM이 공격 대상 탐색에 사용하는 적 목록 (읽기 전용).</summary>
        public IReadOnlyList<EnemyFSM> Enemies => _enemyFSMs;

        // [PR Fix]: G-001 — StartCoroutine 반환값을 보관하여 OnDestroy에서 명시적 StopCoroutine 가능하게 함
        // 씬 전환/Destroy 시 고아 코루틴이 계속 실행되는 버그를 방지한다.
        private Coroutine _tickCoroutine;

        // ── [11단계] Win/Lose 중복 발화 방지 플래그 ──────────────────────────
        // TriggerGameResult()가 호출된 이후 true가 되며, 이후 CheckWinLoseConditions()는
        // 즉시 return하여 결과 이벤트가 중복 발행되는 것을 막는다.
        private bool _gameEnded = false;

        // ── [마일스톤] 이벤트 중복 발화 방지 플래그 ────────────────────────
        private bool _forgeEventFired = false;  // Forge 완성 → 전사 합류 (1회만)
        private bool _day7EventFired  = false;  // Day 7 → 탐험가 합류 (1회만)

        // ── 틱 카운터 (그룹 분산용) ────────────────────────────────────────────
        // 매 틱마다 1씩 증가하여 (_tickCounter % 6)으로 현재 실행 그룹을 결정한다.
        private int _tickCounter = 0;

        // Start()에서 SensorSystem 폴백 초기화 중복 방지용 플래그
        // ResourceNodeSpawner.SpawnAll()이 성공적으로 완료됐을 때만 true로 설정된다.
        private bool _nodesRegistered = false;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // 공개 프로퍼티
        // ══════════════════════════════════════════════════════════════════════

        #region ── 공개 프로퍼티 ──

        /// <summary>
        /// 게임 시작 이후 경과한 게임 일수.
        /// UI, 계절 시스템, 이벤트 트리거 등에서 참조한다.
        /// </summary>
        public float GameTime { get; private set; } = 0f;

        /// <summary>
        /// 자원 예약 관리자. 외부 시스템(GOAP Action 등)이 자원을 예약할 때 사용한다.
        /// null 체크 필수 — Awake 이전에 접근하면 null이다.
        /// </summary>
        public ResourceRegistry Registry { get; private set; }

        // ── 6B단계 추가: 기지 위치 공개 프로퍼티 ───────────────────────────────
        // EnemyFSM LOD 거리 계산 및 FactionAI 플레이어 기지 참조에 사용한다.
        // VillagerFSM의 _baseTileX/Y Inspector 설정과 동일한 값이어야 한다.

        // ── [9단계] UI 레이어 이벤트 ────────────────────────────────────────────

        /// <summary>
        /// 주민이 플레이어 명령을 거부했을 때 발행되는 이벤트.
        /// HUDManager가 Start()에서 구독하여 RefusalBubble에 거부 내용을 전달한다.
        ///
        /// 발행 시점: MessageBus → GameManager.OnOrderRefused() → 이 이벤트
        /// 구독 방법: GameManager.Instance.OnOrderRefusedEvent += yourHandler;
        /// </summary>
        public event Action<MessageBus.OrderRefusedPayload> OnOrderRefusedEvent;

        // ── [11단계] Win/Lose 결과 시스템 ──────────────────────────────────────

        /// <summary>
        /// 게임 결과 종류. CheckWinLoseConditions()에서 판정하며 TriggerGameResult()가 발행한다.
        ///
        ///   None              : 아직 결과 없음 (게임 진행 중)
        ///   Win1_Survival     : 승리 1 — TownHall + 주민 5명 + 30일 생존
        ///   Win3_Prosperity   : 승리 3 (최우선) — Silver Citadel 완성
        ///   Lose_Annihilated  : 패배 1 — 전체 주민 전멸
        ///   Lose_TownHallFallen : 패배 2 — Town Hall 파괴 + 주민 2명 이하
        ///
        /// Win2(팩션 제압/동맹)는 이번 단계 범위 밖 — 향후 추가 예정.
        /// </summary>
        public enum GameResultType
        {
            None,
            Win1_Survival,
            Win3_Prosperity,
            Lose_Annihilated,
            Lose_TownHallFallen
        }

        /// <summary>
        /// Win/Lose 결과가 확정됐을 때 한 번만 발행되는 이벤트.
        /// GameResultPanel이 Start()에서 구독하여 결과 UI를 표시한다.
        ///
        /// 발행 시점: TriggerGameResult() → 이 이벤트
        /// 구독 방법: GameManager.Instance.OnGameResultEvent += yourHandler;
        /// 보장 사항: 게임 진행 중 단 한 번만 발행된다 (_gameEnded 플래그로 중복 방지).
        /// </summary>
        public event Action<GameResultType> OnGameResultEvent;

        /// <summary>
        /// 플레이어 기지 타일 X 좌표.
        /// FactionAI.InitializeBase()와 VillagerFSM LOD 거리 계산에서 사용한다.
        /// </summary>
        public int BaseTileX => _baseTileX;

        /// <summary>
        /// 플레이어 기지 타일 Y 좌표.
        /// FactionAI.InitializeBase()와 VillagerFSM LOD 거리 계산에서 사용한다.
        /// </summary>
        public int BaseTileY => _baseTileY;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Unity 생명주기
        // ══════════════════════════════════════════════════════════════════════

        #region ── Unity 생명주기 ──

        /// <summary>
        /// Unity가 씬 로드 후 가장 먼저 호출한다 (ExecutionOrder -80).
        /// 순수 C# 시스템을 생성하고 씬 싱글턴들에 WorldState를 주입한다.
        /// VillagerFSM 수집은 Start()에서 수행 — Awake 시점에 FSM이 아직 초기화 중일 수 있다.
        /// </summary>
        private void Awake()
        {
            // ── Step -1: TileReservationRegistry 초기화 (TR-1, ADR-T2) ─────────
            // 도메인 리로드 비활성 환경에서 이전 세션 좀비 예약이 남지 않도록 씬 시작 즉시 비운다.
            TileReservationRegistry.ResetAll();

            // ── Step 0: MapConfig 활성화 ─────────────────────────────────────────
            // 반드시 모든 초기화보다 먼저 실행해야 한다.
            // FowManager(-55), MapChunkRenderer(-60)은 Awake에서 MapConfig.Active를 참조하는데,
            // ExecutionOrder 상 GameManager(-80)이 먼저 실행되므로 이 시점에 SetActive()가 보장된다.
            if (_mapConfig == null)
            {
                Debug.LogError("[GameManager] Awake: _mapConfig가 Inspector에 연결되지 않았습니다. " +
                               "FowManager와 MapChunkRenderer가 동작하지 않습니다. " +
                               "Project 창에서 MapConfig 에셋을 생성하고 Inspector에 연결하세요.");
            }
            else
            {
                MapConfig.SetActive(_mapConfig);
                Debug.Log($"[GameManager] MapConfig 활성화 완료. " +
                          $"mapSize={_mapConfig.mapSize}, initialRevealRadius={_mapConfig.initialRevealRadius}");
            }

            // ── Step 1: 싱글턴 설정 ─────────────────────────────────────────────
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameManager] 중복 인스턴스 감지. 이 GameObject를 Destroy합니다. " +
                                 "씬에 GameManager가 하나만 존재하는지 확인하세요.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // ── Step 2: AuthoritativeWorldState 생성 + 초기화 ──────────────────
            _worldState = new AuthoritativeWorldState();
            InitializeWorldState(_worldState);
            AuthoritativeWorldState.SetInstance(_worldState);

            // ── Step 3: ResourceRegistry 생성 ──────────────────────────────────
            _registry = new ResourceRegistry(_worldState);
            Registry  = _registry; // 공개 프로퍼티에도 노출

            // ── Step 4: SensorSystem에 WorldState 주입 ─────────────────────────
            if (SensorSystem.Instance != null)
            {
                SensorSystem.Instance.InjectWorldState(_worldState);
            }
            else
            {
                Debug.LogWarning("[GameManager] Awake: SensorSystem.Instance가 null입니다. " +
                                 "씬에 SensorSystem 컴포넌트가 있는지 확인하세요. " +
                                 "Start()에서 재시도합니다.");
            }

            // ── Step 5: ResourceNodeSpawner — 클러스터 랜덤 노드 스폰 ───────────
            // SpawnAll 내부에서 discoveryRadius 기준 IsDiscovered 설정까지 처리하므로
            // 별도 SensorSystem.DiscoverArea() 호출 불필요
            if (_resourceNodeSpawner != null)
            {
                _nodesRegistered = _resourceNodeSpawner.SpawnAll(_baseTileX, _baseTileY, _baseDiscoverRadius);
            }
            else
            {
                Debug.LogWarning("[GameManager] _resourceNodeSpawner가 연결되지 않았습니다. " +
                                 "씬의 _ResourceNodeSpawner GameObject를 Inspector에 드래그하세요.");
            }

            Debug.Log("[GameManager] Awake 완료 — WorldState, Registry, 기본 노드 초기화.");
        }

        /// <summary>
        /// Awake 이후, 첫 프레임 Update 직전에 Unity가 호출한다.
        /// 이 시점에는 씬의 모든 Awake가 완료되어 VillagerFSM이 안전하게 수집 가능하다.
        /// </summary>
        private void Start()
        {
            // ── Step 1: 씬의 모든 VillagerFSM 수집 및 시스템 등록 ───────────────
            CollectAndSetupVillagers();

            // ── Step 2: SensorSystem null 안전 재주입 (Awake에서 실패한 경우 전체 복구) ─
            // [PR Fix]: G-003 — Awake에서 SensorSystem이 null이었던 경우 Start에서 전체 초기화 복구
            // InjectWorldState도 _nodesRegistered 블록 안으로 이동하여 중복 호출 방지
            if (SensorSystem.Instance != null && _worldState != null && !_nodesRegistered
                && _resourceNodeSpawner != null)
            {
                SensorSystem.Instance.InjectWorldState(_worldState);
                _nodesRegistered = _resourceNodeSpawner.SpawnAll(_baseTileX, _baseTileY, _baseDiscoverRadius);
            }

            // ── Step 3: MessageBus 이벤트 구독 ─────────────────────────────────
            SubscribeToMessageBus();

            // ── Step 4: FactionAI 수집 및 초기화 ────────────────────────────────
            // FindObjectsOfType은 Start() 시점에 한 번만 허용된다.
            CollectAndSetupFactionAI();

            // ── Step 5: EnemyFSM 수집 및 SensorSystem 등록 ──────────────────────
            CollectAndSetupEnemies();

            // ── Step 6: VillageAdvisor 수집 ─────────────────────────────────────
            CollectAndSetupVillageAdvisor();

            // ── Step 7-a: [15단계] RecruitmentSystem 수집 ───────────────────────
            // RecruitmentSystem은 자체 싱글턴을 갖고 있어 직접 의존성 주입이 불필요하다.
            // 여기서는 씬에 존재하는지 확인하고 참조를 보관하는 역할만 한다.
            _recruitmentSystem = FindObjectOfType<RecruitmentSystem>();
            if (_recruitmentSystem == null)
            {
                Debug.Log("[GameManager] Start: 씬에 RecruitmentSystem이 없습니다. " +
                          "주민 모집 기능을 사용하려면 씬에 RecruitmentSystem 컴포넌트를 배치하세요.");
            }
            else
            {
                Debug.Log("[GameManager] RecruitmentSystem 수집 완료.");
            }

            // ── Step 7: [13단계] FoW 초기 시야 공개 ─────────────────────────────
            // FactionAI, VillageAdvisor 초기화 완료 후 FoW 초기 시야를 공개한다.
            // 기지(0, 0) 주변 initialRevealRadius(기본 15타일) 반경을 원형으로 공개한다.
            // FowManager(-55)는 GameManager(-80) Awake보다 늦게 실행되지만
            // Start()는 모든 Awake 완료 후 실행되므로 이 시점에 Instance가 채워져 있다.
            if (FowManager.Instance != null && MapConfig.Active != null)
            {
                int revealRadius = MapConfig.Active.initialRevealRadius;
                FowManager.Instance.InitialReveal(_baseTileX, _baseTileY, revealRadius);

                // FoW 공개 범위와 ResourceNode.IsDiscovered를 동기화한다.
                // SpawnAll()의 _baseDiscoverRadius(12)와 initialRevealRadius(15)가 다를 수 있고,
                // SpawnConfig.minDistanceFromBase가 _baseDiscoverRadius를 초과하면
                // 초기 발견 노드가 0개가 되어 모든 ResourceNodeView가 숨겨진다.
                // Start()에서 한 번 더 DiscoverArea()를 호출해 두 시스템을 맞춘다.
                SensorSystem.Instance?.DiscoverArea(_baseTileX, _baseTileY, revealRadius);

                Debug.Log($"[GameManager] FoW 초기 시야 공개 + ResourceNode 발견 동기화 완료. " +
                          $"반경={revealRadius}타일.");
            }
            else
            {
                Debug.LogWarning("[GameManager] Start: FowManager 또는 MapConfig가 null입니다. " +
                                 "FoW 초기 시야 공개를 건너뜁니다. 씬에 FowManager가 배치됐는지 확인하세요.");
            }

            // ── Step 8: 게임 틱 코루틴 시작 ────────────────────────────────────
            // [PR Fix]: G-001 — 반환된 Coroutine을 _tickCoroutine에 저장하여 OnDestroy에서 StopCoroutine 가능하게 함
            _tickCoroutine = StartCoroutine(GameTickCoroutine());

            Debug.Log("[GameManager] Start 완료 — 틱 루프 시작. " +
                      $"등록된 주민 수: {_villagerFSMs.Count}명, " +
                      $"FactionAI 수: {(_factionAIs?.Length ?? 0)}개, " +
                      $"EnemyFSM 수: {_enemyFSMs.Count}명, " +
                      $"VillageAdvisor: {(_villageAdvisor != null ? "등록됨" : "없음")}.");
        }

        /// <summary>
        /// 이 GameObject가 파괴될 때 호출된다.
        /// MessageBus 구독 해제 및 WorldState 싱글턴 참조를 정리한다.
        /// </summary>
        private void OnDestroy()
        {
            // [PR Fix]: G-001 — OnDestroy에서 _tickCoroutine을 명시적으로 StopCoroutine하여 고아 코루틴 방지
            if (_tickCoroutine != null)
            {
                StopCoroutine(_tickCoroutine);
                _tickCoroutine = null;
            }

            // MessageBus 구독 해제 (구독 상태인 경우에만)
            UnsubscribeFromMessageBus();

            // AuthoritativeWorldState 싱글턴 참조 해제
            AuthoritativeWorldState.SetInstance(null);

            // 싱글턴 자기 참조 해제
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // 게임 틱 코루틴
        // ══════════════════════════════════════════════════════════════════════

        #region ── 게임 틱 코루틴 ──

        /// <summary>
        /// 0.1초 간격으로 게임 틱을 실행하는 메인 루프.
        /// 모든 시스템 갱신 순서가 이 코루틴에서 결정된다.
        ///
        /// 실행 순서 (순서 변경 금지):
        ///   1. 게임 시간 진행
        ///   2. UpdateAllSensors          — 환경 갱신
        ///   2b. SeasonManager.OnTick     — [14단계] 계절 배율 변경 (Regen 이전 적용)
        ///   3. TickResourceRegeneration  — 자원 재생 (배율 반영)
        ///   4. MessageBus.ProcessTick()  — SeasonChanged/WinterCrisis 구독자에게 전달
        ///   5. TickVillagerGroup         — 반드시 ProcessTick() 이후 실행 (루프 중 리스트 수정 방지)
        ///   6. TickEnemyGroup
        ///   7. FowManager.OnTick
        ///   8. CheckMilestoneEvents
        ///   9. CheckWinLoseConditions
        /// </summary>
        private IEnumerator GameTickCoroutine()
        {
            // 0.1초 WaitForSeconds 객체를 미리 할당하여 매 틱 GC를 방지한다
            WaitForSeconds wait = new WaitForSeconds(0.1f);

            while (true)
            {
                yield return wait;

                // ── 1. 게임 시간 진행 ─────────────────────────────────────────
                // 공식: deltaGameDays = 실제 경과 시간(0.1초) × 배율
                // 예: _gameTimeScale=0.01 → 0.1초당 0.001 게임일 → 100초 = 1게임일
                float deltaGameDays = 0.1f * _gameTimeScale; // 기획서 수치: 0.01f
                GameTime += deltaGameDays;

                // [PR Fix]: G-004 — 틱 실행 순서 주석을 아래와 같이 명시 (순서 변경 금지)
                // 틱 실행 순서 (순서 변경 금지):
                // 1. UpdateAllSensors      — 환경 갱신
                // 2. SeasonManager.OnTick  — [14단계] 계절 배율 변경 (Regen 이전 적용)
                // 3. TickResourceRegeneration — 자원 재생 (배율 반영)
                // 4. MessageBus.ProcessTick() — SeasonChanged/WinterCrisis 전달
                // 5. TickVillagerGroup     — 반드시 ProcessTick() 이후 실행 (루프 중 리스트 수정 방지)

                // ── 2. SensorSystem 갱신 ──────────────────────────────────────
                // 모든 주민의 VillagerBrain 환경 플래그를 현재 맵 상태 기준으로 갱신한다
                SensorSystem.Instance?.UpdateAllSensors();

                // ── 2-b. SeasonManager 틱 [14단계] ──────────────────────────────
                // 재생률 배율 변경이 TickResourceRegeneration보다 먼저 적용되도록 한다.
                SeasonManager.Instance?.OnTick(deltaGameDays);

                // 자원 노드 시간 경과 재생 (CurrentAmount 회복)
                SensorSystem.Instance?.TickResourceRegeneration(deltaGameDays);

                // ── 3. MessageBus 틱 ──────────────────────────────────────────
                // 이번 틱에 발행된 메시지를 우선순위 순서대로 구독자에게 전달한다
                MessageBus.Instance?.ProcessTick();

                // ── 4. VillagerFSM 그룹별 틱 ─────────────────────────────────
                // 6개 그룹 중 이번 틱에 해당하는 그룹만 실행한다 (성능 분산).
                // 그룹 0 : tickCounter 0, 6, 12, ...
                // 그룹 1 : tickCounter 1, 7, 13, ...
                // ...
                // 그룹 5 : tickCounter 5, 11, 17, ...
                TickVillagerGroup(_tickCounter % 6);

                // ── 5. EnemyFSM 그룹별 틱 ──────────────────────────────────────
                // VillagerFSM과 동일한 그룹 분산 방식으로 EnemyFSM을 틱한다.
                // FactionTick(5초 간격)은 EnemyFSM 코루틴에서 독립적으로 실행된다.
                TickEnemyGroup(_tickCounter % 6);

                // ── 6. [13단계] FoW 틱 갱신 ────────────────────────────────────────
                // 가시(2) 타일을 탐험됨(1)으로 다운그레이드하고 주민 전체 시야를 재계산한다.
                // EnemyFSM 틱 이후에 실행하여 이번 틱의 전투/이동이 모두 반영된 상태에서 갱신한다.
                FowManager.Instance?.OnTick();

                // ── 7. 마일스톤 이벤트 체크 ─────────────────────────────────────
                // Win/Lose 체크 전에 주민 수를 늘릴 수 있도록 먼저 실행한다.
                CheckMilestoneEvents();

                // ── 8. Win/Lose 조건 체크 [11단계] ─────────────────────────────
                // 모든 시스템 갱신(주민 사망 처리 포함) 이후에 체크하여 같은 틱 내
                // 사망한 주민이 반영된 정확한 생존자 수로 판정한다.
                // _gameEnded가 true가 되면 TriggerGameResult()가 틱 코루틴을 정지시키므로
                // 이 줄 이후의 코드는 더 이상 실행되지 않는다.
                CheckWinLoseConditions();

                _tickCounter++;
            }
        }

        /// <summary>
        /// 특정 그룹 인덱스에 속하는 EnemyFSM만 Tick()을 호출한다.
        /// VillagerFSM과 동일한 그룹 분산 방식을 사용한다.
        /// null이거나 비활성화된 FSM은 건너뛴다.
        /// </summary>
        /// <param name="groupIndex">이번 틱에서 처리할 그룹 인덱스 (0~5).</param>
        private void TickEnemyGroup(int groupIndex)
        {
            int count = _enemyFSMs.Count;
            for (int i = 0; i < count; i++)
            {
                EnemyFSM fsm = _enemyFSMs[i];

                // null 방어: 씬 전환이나 사망으로 Destroy된 참조 무시
                if (fsm == null) continue;

                // 비활성화된 유닛은 틱 처리 제외
                if (!fsm.isActiveAndEnabled) continue;

                // 이 FSM의 그룹 인덱스가 현재 그룹과 일치할 때만 실행
                if (fsm.TickGroupIndex == groupIndex)
                {
                    fsm.Tick();
                }
            }
        }

        /// <summary>
        /// 특정 그룹 인덱스에 속하는 VillagerFSM만 Tick()을 호출한다.
        /// null이거나 비활성화된 FSM은 건너뛴다.
        /// </summary>
        /// <param name="groupIndex">이번 틱에서 처리할 그룹 인덱스 (0~5).</param>
        private void TickVillagerGroup(int groupIndex)
        {
            int count = _villagerFSMs.Count;
            for (int i = 0; i < count; i++)
            {
                VillagerFSM fsm = _villagerFSMs[i];

                // null 방어: 씬 전환이나 사망으로 Destroy된 주민 참조 무시
                if (fsm == null) continue;

                // 비활성화된 주민은 틱 처리 제외 (사망 연출 중 등)
                if (!fsm.isActiveAndEnabled) continue;

                // 이 FSM의 그룹 인덱스가 현재 그룹과 일치할 때만 실행
                if (fsm.TickGroupIndex == groupIndex)
                {
                    fsm.Tick();
                }
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // MessageBus 이벤트 핸들러
        // ══════════════════════════════════════════════════════════════════════

        #region ── MessageBus 이벤트 핸들러 ──

        /// <summary>
        /// VillagerDied 메시지를 수신하여 사망 주민을 처리한다.
        ///
        /// 처리 순서:
        ///   1. Payload 언박싱
        ///   2. 콘솔 로그 출력
        ///   3. 드롭 아이템을 WorldState.DroppedItems에 추가
        ///   4. _villagerFSMs 목록에서 사망 주민 제거
        /// </summary>
        /// <param name="msg">VillagerDied AIMessage. Payload가 VillagerDiedPayload여야 한다.</param>
        private void OnVillagerDied(AIMessage msg)
        {
            // ── Payload 언박싱 ─────────────────────────────────────────────────
            if (!(msg.Payload is MessageBus.VillagerDiedPayload payload))
            {
                Debug.LogWarning("[GameManager] OnVillagerDied: Payload를 VillagerDiedPayload로 캐스팅할 수 없습니다. " +
                                 $"실제 타입: {msg.Payload?.GetType().Name ?? "null"}");
                return;
            }

            // ── 콘솔 로그 ─────────────────────────────────────────────────────
            Debug.Log($"[GameManager] 주민 사망 — VillagerId={payload.VillagerId}, " +
                      $"위치=({payload.DeathTileX}, {payload.DeathTileY}), " +
                      $"드롭 아이템={payload.DroppedItems?.Length ?? 0}개.");

            // ── 드롭 아이템 WorldState 등록 ──────────────────────────────────
            // 사망 주민이 소지하던 아이템을 타일에 드롭 — 다른 주민이 나중에 픽업 가능
            if (payload.DroppedItems != null && _worldState != null)
            {
                foreach (MessageBus.DroppedItemInfo dropInfo in payload.DroppedItems)
                {
                    var droppedItem = new DroppedItem
                    {
                        ItemId                  = System.Guid.NewGuid().ToString(),
                        ItemType                = dropInfo.ItemType,
                        TileX                   = dropInfo.TileX,
                        TileY                   = dropInfo.TileY,
                        OriginalOwnerVillagerId = payload.VillagerId
                    };
                    _worldState.DroppedItems.Add(droppedItem);
                }
            }

            // ── _villagerFSMs 목록에서 사망 주민 제거 ────────────────────────
            // SensorSystem.UnregisterVillager()는 VillagerFSM.OnDestroy()에서 처리되므로 여기서는 호출하지 않는다.
            RemoveVillagerFromList(payload.VillagerId);

            // ── [8단계] 팬아웃: 생존 주민 전체에 VillagerDied 메시지 전달 ─────
            // 각 VillagerFSM이 HandleMessage(VillagerDied)에서 mood/loyalty 패널티 및
            // Fear 전염(5타일 이내)을 처리한다.
            foreach (VillagerFSM fsm in _villagerFSMs)
            {
                if (fsm == null || fsm.Brain == null || !fsm.Brain.IsAlive) continue;
                if (fsm.AgentId == payload.VillagerId) continue; // 사망 당사자 제외
                fsm.ReceiveMessage(msg);
            }
        }

        /// <summary>
        /// RageKill 메시지 수신: Rage 처치 반경(6타일) 이내 생존 주민에게 메시지를 팬아웃한다. [8단계]
        /// 각 VillagerFSM의 HandleMessage()에서 RecentRageKillWitness를 증가시켜 Rage 전염을 유발한다.
        /// </summary>
        private void OnRageKill(AIMessage msg)
        {
            if (!(msg.Payload is MessageBus.RageKillPayload payload)) return;

            const float RAGE_FAN_RADIUS = 6f; // VillagerFSM.RAGE_CONTAGION_RADIUS와 일치
            foreach (VillagerFSM fsm in _villagerFSMs)
            {
                if (fsm == null || fsm.Brain == null || !fsm.Brain.IsAlive) continue;
                if (fsm.AgentId == payload.KillerVillagerId) continue; // 처치자 본인 제외
                float dist = Mathf.Abs(fsm.Brain.TileX - payload.KillTileX)
                           + Mathf.Abs(fsm.Brain.TileY - payload.KillTileY);
                if (dist <= RAGE_FAN_RADIUS)
                    fsm.ReceiveMessage(msg);
            }
        }

        /// <summary>
        /// RaidDecision 메시지를 수신하여 팩션 침략 정보를 처리한다.
        /// GameManager는 이 메시지를 수신하여 로그를 남기고,
        /// 향후 UI 시스템이 침략 경고를 플레이어에게 표시할 수 있도록 데이터를 보존한다.
        /// </summary>
        /// <param name="msg">RaidDecision AIMessage. Payload가 RaidDecisionPayload여야 한다.</param>
        private void OnRaidDecision(AIMessage msg)
        {
            if (!(msg.Payload is MessageBus.RaidDecisionPayload payload))
            {
                Debug.LogWarning("[GameManager] OnRaidDecision: Payload를 RaidDecisionPayload로 캐스팅할 수 없습니다. " +
                                 $"실제 타입: {msg.Payload?.GetType().Name ?? "null"}");
                return;
            }

            // TradeProposal(상인 연합 외교 제안)과 실제 RaidDecision을 구분하여 로깅한다.
            bool isTradeProposal = payload.RaidTriggerReason == "TradeProposal";

            if (isTradeProposal)
            {
                Debug.Log($"[GameManager] 상인 연합 무역 제안 수신 — " +
                          $"FactionId={payload.FactionId}, 전력={payload.EstimatedStrength:F1}");
            }
            else
            {
                // 실제 침략 발생 시 RaidCount 증가 — VillageAdvisor Watchtower 조건 판단에 사용
                if (_worldState != null)
                {
                    _worldState.RaidCount++;
                }

                Debug.Log($"[GameManager] 팩션 침략 결정 수신 — " +
                          $"FactionId={payload.FactionId}, 대상={payload.TargetFactionId}, " +
                          $"유닛 수={payload.ParticipatingUnitIds?.Length ?? 0}명, " +
                          $"전력={payload.EstimatedStrength:F1}, " +
                          $"이유={payload.RaidTriggerReason}, " +
                          $"누적 침략 횟수={_worldState?.RaidCount ?? 0}회");
            }
        }

        /// <summary>
        /// OrderRefused 메시지를 수신하여 거부 정보를 기록한다.
        /// 향후 UI 시스템이 이 정보를 화면에 표시한다.
        /// </summary>
        /// <param name="msg">OrderRefused AIMessage. Payload가 OrderRefusedPayload여야 한다.</param>
        private void OnOrderRefused(AIMessage msg)
        {
            // ── Payload 언박싱 ─────────────────────────────────────────────────
            if (!(msg.Payload is MessageBus.OrderRefusedPayload payload))
            {
                Debug.LogWarning("[GameManager] OnOrderRefused: Payload를 OrderRefusedPayload로 캐스팅할 수 없습니다. " +
                                 $"실제 타입: {msg.Payload?.GetType().Name ?? "null"}");
                return;
            }

            // ── 콘솔 로그 ─────────────────────────────────────────────────────
            Debug.Log($"[GameManager] 명령 거부 — VillagerId={payload.VillagerId}, " +
                      $"거부 이유={payload.RefusalReasonCode}, " +
                      $"메시지={payload.RefusalMessage}");

            // ── [9단계] UI 이벤트 발행 ──────────────────────────────────────
            // HUDManager가 이 이벤트를 구독하여 RefusalBubble.HandleRefusal()을 호출한다.
            // 구독자가 없으면 null 조건 연산자(?.)가 아무 동작도 하지 않는다.
            OnOrderRefusedEvent?.Invoke(payload);
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private 초기화 헬퍼
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private 초기화 헬퍼 ──

        /// <summary>
        /// Inspector 수치로 AuthoritativeWorldState의 초기 자원 재고를 설정한다.
        /// AuthoritativeWorldState 생성자는 기본값으로 초기화하므로,
        /// 이 메서드에서 기획서 수치로 덮어쓴다.
        /// </summary>
        /// <param name="worldState">초기화할 WorldState 인스턴스.</param>
        private void InitializeWorldState(AuthoritativeWorldState worldState)
        {
            // 기획서 수치 (GDD v0.4) — Inspector에서 변경 가능
            worldState.WoodStock       = _initialWoodStock;        // GDD v0.4 수치: 10
            worldState.StoneStock      = _initialStoneStock;       // GDD v0.4 수치: 5
            worldState.RawFoodStock    = _initialRawFoodStock;     // GDD v0.4 수치: 30
            worldState.CookedFoodStock = _initialCookedFoodStock;  // GDD v0.4 수치: 0
            worldState.IronStock       = _initialIronStock;        // GDD v0.4 수치: 0
            worldState.CopperStock     = _initialCopperStock;      // GDD v0.4 수치: 0

            Debug.Log($"[GameManager] WorldState 초기화 완료 — " +
                      $"Wood={worldState.WoodStock}, Stone={worldState.StoneStock}, " +
                      $"RawFood={worldState.RawFoodStock}, CookedFood={worldState.CookedFoodStock}, " +
                      $"Iron={worldState.IronStock}, Copper={worldState.CopperStock}");
        }

        /// <summary>
        /// 씬의 모든 VillagerFSM을 수집하고 각 시스템에 등록한다.
        ///
        /// FindObjectsOfType 호출 이유: Start() 시점에 한 번만 호출하고 이후 캐시를 사용한다.
        /// (성능 규칙: Update/Tick 내부에서 Find 계열 호출 금지 — 여기서는 Start에서만 호출하므로 허용)
        /// </summary>
        private void CollectAndSetupVillagers()
        {
            // 씬에서 VillagerFSM을 모두 찾는다 (Start 시점에 한 번만 허용되는 Find 호출)
            VillagerFSM[] found = FindObjectsOfType<VillagerFSM>();

            if (found == null || found.Length == 0)
            {
                Debug.LogWarning("[GameManager] CollectAndSetupVillagers: 씬에 VillagerFSM이 없습니다. " +
                                 "주민 GameObject에 VillagerFSM 컴포넌트가 부착되어 있는지 확인하세요.");
                return;
            }

            foreach (VillagerFSM fsm in found)
            {
                if (fsm == null) continue;

                // SensorSystem에 주민 등록 (환경 플래그 갱신 대상으로 추가)
                SensorSystem.Instance?.RegisterVillager(fsm);

                // 의존성 주입 — registry, actionDatabase, BuildingQueue.Instance 전달
                // _actionDatabase가 null이면 VillagerFSM이 내부 더미 로직으로 폴백한다
                fsm.InjectDependencies(_registry, _actionDatabase, BuildingQueue.Instance);

                // [Phase 1] 주민 머리 위 행동 아이콘 자동 부착 (7장 Level 1)
                AttachActionIcon(fsm);

                _villagerFSMs.Add(fsm);
            }

            Debug.Log($"[GameManager] 주민 {_villagerFSMs.Count}명 수집 및 의존성 주입 완료.");
        }

        /// <summary>
        /// 주민 GameObject에 VillagerActionIcon 자식을 자동 생성·부착한다.
        /// 이미 자식에 아이콘이 있으면 중복 생성하지 않는다.
        /// </summary>
        private void AttachActionIcon(VillagerFSM fsm)
        {
            if (fsm == null) return;
            if (fsm.GetComponentInChildren<VillagerActionIcon>() != null) return;

            var iconGo = new GameObject("_ActionIcon");
            iconGo.transform.SetParent(fsm.transform, false);
            iconGo.transform.localPosition = new Vector3(0f, 1.3f, 0f);

            var icon = iconGo.AddComponent<VillagerActionIcon>();
            icon.Initialize(fsm, _koreanFont);
        }

        /// <summary>
        /// MessageBus의 이벤트를 구독한다. Start()에서 호출한다.
        /// </summary>
        private void SubscribeToMessageBus()
        {
            if (MessageBus.Instance == null)
            {
                Debug.LogWarning("[GameManager] SubscribeToMessageBus: MessageBus.Instance가 null입니다. " +
                                 "씬에 MessageBus 컴포넌트가 있는지 확인하세요.");
                return;
            }

            MessageBus.Instance.Subscribe(MessageType.VillagerDied,  OnVillagerDied);
            MessageBus.Instance.Subscribe(MessageType.OrderRefused,  OnOrderRefused);
            MessageBus.Instance.Subscribe(MessageType.RaidDecision,  OnRaidDecision); // 6B단계 추가
            MessageBus.Instance.Subscribe(MessageType.RageKill,      OnRageKill);     // [8단계] Rage 전염 팬아웃
        }

        /// <summary>
        /// MessageBus 구독을 해제한다. OnDestroy()에서 호출한다.
        /// MessageBus가 null이면 이미 파괴된 것으로 간주하고 조용히 종료한다.
        /// </summary>
        private void UnsubscribeFromMessageBus()
        {
            if (MessageBus.Instance == null) return;

            MessageBus.Instance.Unsubscribe(MessageType.VillagerDied, OnVillagerDied);
            MessageBus.Instance.Unsubscribe(MessageType.OrderRefused,  OnOrderRefused);
            MessageBus.Instance.Unsubscribe(MessageType.RaidDecision,  OnRaidDecision); // 6B단계 추가
            MessageBus.Instance.Unsubscribe(MessageType.RageKill,      OnRageKill);     // [8단계]
        }

        /// <summary>
        /// 씬의 모든 FactionAI를 수집하고 플레이어 기지 위치를 주입한다.
        ///
        /// FindObjectsOfType 호출 이유: Start() 시점에 한 번만 호출하고 이후 캐시를 사용한다.
        /// (성능 규칙: Update/Tick 내부에서 Find 계열 호출 금지 — Start에서만 허용)
        /// </summary>
        private void CollectAndSetupFactionAI()
        {
            _factionAIs = FindObjectsOfType<FactionAI>();

            if (_factionAIs == null || _factionAIs.Length == 0)
            {
                Debug.Log("[GameManager] CollectAndSetupFactionAI: 씬에 FactionAI가 없습니다. " +
                          "팩션 AI 없이 진행합니다.");
                _factionAIs = System.Array.Empty<FactionAI>();
                return;
            }

            // 각 FactionAI에 플레이어 기지 위치를 주입한다.
            foreach (FactionAI faction in _factionAIs)
            {
                if (faction == null) continue;

                // _baseTileX/Y는 GameManager Inspector 필드 (플레이어 기지 위치)
                faction.InitializeBase(_baseTileX, _baseTileY);

                Debug.Log($"[GameManager] FactionAI 초기화 완료. FactionId={faction.FactionId}");
            }

            Debug.Log($"[GameManager] FactionAI {_factionAIs.Length}개 수집 및 기지 위치 주입 완료.");
        }

        /// <summary>
        /// 씬의 모든 EnemyFSM을 수집하고 SensorSystem에 등록한다.
        /// MessageBus 주입도 이 메서드에서 수행한다.
        ///
        /// FindObjectsOfType 호출 이유: Start() 시점에 한 번만 허용된다.
        /// </summary>
        private void CollectAndSetupEnemies()
        {
            EnemyFSM[] found = FindObjectsOfType<EnemyFSM>();

            if (found == null || found.Length == 0)
            {
                Debug.Log("[GameManager] CollectAndSetupEnemies: 씬에 EnemyFSM이 없습니다. " +
                          "적 AI 없이 진행합니다.");
                return;
            }

            foreach (EnemyFSM fsm in found)
            {
                if (fsm == null) continue;

                // SensorSystem에 적 등록 (주민의 NearEnemy 감지 대상으로 추가)
                SensorSystem.Instance?.RegisterEnemy(fsm);

                // MessageBus 의존성 주입
                fsm.InjectDependencies(MessageBus.Instance);

                _enemyFSMs.Add(fsm);
            }

            Debug.Log($"[GameManager] EnemyFSM {_enemyFSMs.Count}개 수집, SensorSystem 등록, " +
                      $"MessageBus 주입 완료.");
        }

        /// <summary>
        /// 씬의 VillageAdvisor를 수집한다.
        /// VillageAdvisor는 씬에 1개만 존재해야 하며, 없어도 경고만 출력하고 진행한다.
        /// </summary>
        private void CollectAndSetupVillageAdvisor()
        {
            _villageAdvisor = FindObjectOfType<VillageAdvisor>();

            if (_villageAdvisor == null)
            {
                Debug.Log("[GameManager] CollectAndSetupVillageAdvisor: 씬에 VillageAdvisor가 없습니다. " +
                          "자율 건물 결정 없이 진행합니다. 씬 배치 가이드를 참고하세요.");
                return;
            }

            Debug.Log("[GameManager] VillageAdvisor 수집 완료.");
        }

        /// <summary>
        /// VillagerId를 기준으로 _villagerFSMs 목록에서 해당 주민을 제거한다.
        /// 사망 이벤트 처리 시 호출된다.
        /// </summary>
        /// <param name="villagerId">제거할 주민의 고유 ID.</param>
        private void RemoveVillagerFromList(string villagerId)
        {
            if (string.IsNullOrEmpty(villagerId))
            {
                Debug.LogWarning("[GameManager] RemoveVillagerFromList: villagerId가 null/empty입니다.");
                return;
            }

            // 역순 순회 — RemoveAt 시 인덱스 오염 없이 안전하게 제거
            for (int i = _villagerFSMs.Count - 1; i >= 0; i--)
            {
                VillagerFSM fsm = _villagerFSMs[i];

                // 이미 Destroy된 FSM(null)이거나, Brain이 null인 경우 동시에 목록에서 제거한다
                if (fsm == null || fsm.Brain == null)
                {
                    _villagerFSMs.RemoveAt(i);
                    continue;
                }

                if (fsm.Brain.VillagerId == villagerId)
                {
                    _villagerFSMs.RemoveAt(i);
                    Debug.Log($"[GameManager] 주민 목록에서 제거 완료. VillagerId={villagerId}, " +
                              $"남은 주민={_villagerFSMs.Count}명.");
                    break; // ID는 고유하므로 첫 발견 즉시 종료
                }
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // 마일스톤 이벤트
        // ══════════════════════════════════════════════════════════════════════

        #region ── 마일스톤 이벤트 ──

        /// <summary>
        /// Forge 완성 및 Day 7 마일스톤 이벤트를 체크하여 신규 주민을 스폰한다.
        /// GameTickCoroutine에서 CheckWinLoseConditions 직전에 매 틱 호출된다.
        /// 각 이벤트는 1회만 발동한다 (_forgeEventFired / _day7EventFired 플래그).
        /// </summary>
        private void CheckMilestoneEvents()
        {
            if (_gameEnded) return;
            if (_worldState == null || _villagerPrefab == null) return;

            // ── 이벤트 1: Forge 완성 → 전사(Warrior) 합류 ──────────────────────
            if (!_forgeEventFired && _worldState.ForgeBuilt)
            {
                _forgeEventFired = true;
                SpawnVillager(AgentRole.Warrior);
                Debug.Log("[GameManager] 마일스톤: Forge 완성 — 전사 합류.");
            }

            // ── 이벤트 2: Day 7 → 탐험가(Explorer) 합류 ───────────────────────
            if (!_day7EventFired && GameTime >= 7f)
            {
                _day7EventFired = true;
                SpawnVillager(AgentRole.Explorer);
                Debug.Log("[GameManager] 마일스톤: Day 7 — 탐험가 합류.");
            }
        }

        /// <summary>
        /// 지정된 역할의 주민을 기지 위치에 스폰하고 모든 게임 시스템에 등록한다.
        /// </summary>
        /// <param name="role">새 주민의 AgentRole.</param>
        private void SpawnVillager(AgentRole role)
        {
            GameObject go = Instantiate(_villagerPrefab,
                                        new Vector3(_baseTileX, _baseTileY, 0f),
                                        Quaternion.identity);
            go.name = $"Villager_{role}_{_villagerFSMs.Count}";

            VillagerFSM fsm = go.GetComponent<VillagerFSM>();
            if (fsm == null)
            {
                Debug.LogError("[GameManager] SpawnVillager: Prefab에 VillagerFSM이 없습니다. " +
                               "Villager Prefab을 올바르게 설정했는지 확인하세요. " +
                               "이 마일스톤 이벤트는 1회성이므로 게임을 재시작해야 합니다.");
                Destroy(go);
                return;
            }

            // Instantiate()는 메인 스레드 동기 호출이므로 반환 시점에 VillagerFSM.Awake()가 완료됨.
            // Brain은 Awake에서 이미 생성되었으므로 null 불가능.
            fsm.Brain.Role  = role;
            fsm.Brain.TileX = _baseTileX;
            fsm.Brain.TileY = _baseTileY;

            if (role == AgentRole.Warrior)
            {
                fsm.Brain.HasWeapon = true;
            }

            // 6-그룹 틱 분산 유지: Prefab 기본값(0)이 아닌 현재 주민 수 기반 그룹 배정
            fsm.SetTickGroupIndex(_villagerFSMs.Count % 6);

            RegisterNewVillager(fsm);
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Win/Lose 조건 체크 [11단계]
        // ══════════════════════════════════════════════════════════════════════

        #region ── Win/Lose 조건 체크 ──

        /// <summary>
        /// 매 틱 GameTickCoroutine()에서 호출되어 승리/패배 조건을 판정한다.
        ///
        /// 판정 우선순위 (위에서 아래 순서로 체크):
        ///   1. Win3_Prosperity  — SilverCitadel 완성 (최우선. 다른 모든 조건보다 먼저 확인)
        ///   2. Win1_Survival    — TownHall + 주민 5명 이상 + 30일 생존
        ///   3. Lose_Annihilated — 생존 주민 0명 전멸
        ///   4. Lose_TownHallFallen — Town Hall이 한번 건설됐다가 파괴 + 주민 2명 이하
        ///
        /// _gameEnded가 true이면 즉시 return하여 중복 발화를 막는다.
        /// _worldState가 null이면(비정상 상태) 경고 후 return한다.
        /// </summary>
        private void CheckWinLoseConditions()
        {
            // ── 중복 발화 방지 ───────────────────────────────────────────────
            if (_gameEnded) return;

            // ── WorldState null 방어 ─────────────────────────────────────────
            if (_worldState == null)
            {
                Debug.LogWarning("[GameManager] CheckWinLoseConditions: _worldState가 null입니다. 판정을 건너뜁니다.");
                return;
            }

            // 현재 생존 주민 수 (사망 처리 후 이미 갱신된 값)
            int aliveCount = _villagerFSMs.Count;

            // ── 승리 3 (최우선): Silver Citadel 완성 ──────────────────────────
            // 기획서: 번영 승리는 어떤 패배 조건보다 우선하여 판정한다.
            // [PR Fix]: W-02 — Silver Citadel 완성과 전멸이 동일 틱에 동시 발생하는 엣지 케이스 방어
            // GDD §11에 명시 없음 → 안전하게 aliveCount > 0 가드 추가 (주민이 없으면 번영 불가)
            if (_worldState.SilverCitadelBuilt && aliveCount > 0)
            {
                TriggerGameResult(GameResultType.Win3_Prosperity);
                return;
            }

            // ── 승리 1: Town Hall + 주민 5명 이상 + 30일 ────────────────────
            // 기획서 확정 수치: WIN1_MIN_VILLAGERS=5, WIN1_SURVIVAL_DAYS=30f
            if (_worldState.TownHallBuilt
                && aliveCount >= WIN1_MIN_VILLAGERS
                && GameTime >= WIN1_SURVIVAL_DAYS)
            {
                TriggerGameResult(GameResultType.Win1_Survival);
                return;
            }

            // ── 패배 1: 전멸 ─────────────────────────────────────────────────
            // 주민이 한 명도 없으면 즉시 패배. TownHall 존재 여부와 무관하다.
            if (aliveCount <= 0)
            {
                TriggerGameResult(GameResultType.Lose_Annihilated);
                return;
            }

            // ── 패배 2: Town Hall 파괴 + 주민 2명 이하 ──────────────────────
            // TownHallEverBuilt == true  : TownHall이 한번 건설됐음 (오탐 방지)
            // TownHallBuilt     == false : 현재 TownHall이 없음 (파괴됨)
            // aliveCount <= LOSE_TOWNHALL_MAX_VILLAGERS : 소수 생존으로 마을 재건 불가
            if (_worldState.TownHallEverBuilt
                && !_worldState.TownHallBuilt
                && aliveCount <= LOSE_TOWNHALL_MAX_VILLAGERS)
            {
                TriggerGameResult(GameResultType.Lose_TownHallFallen);
            }
        }

        /// <summary>
        /// 게임 결과를 확정하고 관련 시스템을 정리한 뒤 OnGameResultEvent를 발행한다.
        ///
        /// 처리 순서:
        ///   1. _gameEnded = true 설정 (이후 CheckWinLoseConditions 무시)
        ///   2. 틱 코루틴 정지 (더 이상 틱이 실행되지 않음)
        ///   3. 결과 로그 출력
        ///   4. OnGameResultEvent 발행 (GameResultPanel이 수신)
        /// </summary>
        /// <param name="result">확정된 게임 결과 타입.</param>
        private void TriggerGameResult(GameResultType result)
        {
            // 재진입 방지 — 동일 틱에서 중복 호출이 발생하더라도 안전하게 무시
            if (_gameEnded) return;

            _gameEnded = true;

            // 틱 루프 정지 — 게임이 끝났으므로 더 이상 시뮬레이션이 진행되지 않아야 한다.
            if (_tickCoroutine != null)
            {
                StopCoroutine(_tickCoroutine);
                _tickCoroutine = null;
            }

            Debug.Log($"[GameManager] 게임 결과 확정: {result}. " +
                      $"GameTime={GameTime:F1}일, 생존 주민={_villagerFSMs.Count}명.");

            // GameResultPanel(또는 다른 구독자)에게 결과를 전달한다.
            // 구독자가 없어도 null 조건 연산자(?.)가 예외 없이 처리한다.
            OnGameResultEvent?.Invoke(result);
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // 공개 런타임 API
        // ══════════════════════════════════════════════════════════════════════

        #region ── 공개 런타임 API ──

        /// <summary>
        /// 현재 플레이어 마을의 전력을 계산하여 반환한다.
        /// FactionAI.EvaluateRaidDecision()에서 침략 결정 비교에 사용한다.
        ///
        /// 공식 (GDD v0.4 확정값):
        ///   (주민 수 × 10) + (전사 수 × 15) + (무기 보유 수 × 8)
        ///   + (Watchtower 완성 × 20) + (Forge 완성 × 15)
        ///
        /// TODO: 기획팀 확인 필요 — '전사 수'와 '무기 보유 수' 집계 방식.
        ///       현재는 AgentRole.Warrior인 주민을 전사로 간주하고,
        ///       Brain.HasWeapon이 true인 주민을 무기 보유자로 집계한다.
        /// </summary>
        public float CalculatePlayerStrength()
        {
            // ── 기본 전력: 전체 주민 수 × 10 ────────────────────────────────────
            int totalVillagers = _villagerFSMs.Count;
            float strength = totalVillagers * 10f;

            // ── 전사 보너스: 전사(Warrior) × 15 ─────────────────────────────────
            // AgentRole.Warrior인 생존 주민을 집계한다.
            int warriorCount = 0;
            int weaponCount  = 0;

            for (int i = 0; i < totalVillagers; i++)
            {
                VillagerFSM fsm = _villagerFSMs[i];
                if (fsm == null || fsm.Brain == null || !fsm.Brain.IsAlive) continue;

                if (fsm.Brain.Role == AgentRole.Warrior)
                {
                    warriorCount++;
                }

                // 무기 보유 보너스: HasWeapon 또는 HasPrimitiveWeapon이 true인 주민
                if (fsm.Brain.HasWeapon || fsm.Brain.HasPrimitiveWeapon)
                {
                    weaponCount++;
                }
            }

            strength += warriorCount * 15f;  // 기획서 수치: 전사 × 15
            strength += weaponCount  * 8f;   // 기획서 수치: 무기 보유 × 8

            // ── 건물 보너스 ──────────────────────────────────────────────────────
            if (_worldState != null)
            {
                // 망루 완성 시 +20 (기획서 수치)
                if (_worldState.WatchtowerBuilt)
                {
                    strength += 20f;
                }

                // 대장간 완성 시 +15 (기획서 수치)
                if (_worldState.ForgeBuilt)
                {
                    strength += 15f;
                }
            }

            return strength;
        }

        /// <summary>
        /// 런타임에 동적으로 생성된 주민을 게임 시스템에 등록한다.
        /// 스폰 시스템이나 이벤트 기반 주민 추가 시 사용한다.
        /// </summary>
        /// <param name="fsm">등록할 VillagerFSM. null이면 경고 후 무시.</param>
        public void RegisterNewVillager(VillagerFSM fsm)
        {
            if (fsm == null)
            {
                Debug.LogWarning("[GameManager] RegisterNewVillager: fsm이 null입니다.");
                return;
            }

            if (_villagerFSMs.Contains(fsm))
            {
                Debug.LogWarning($"[GameManager] RegisterNewVillager: 이미 등록된 FSM입니다. " +
                                 $"VillagerId={fsm.Brain?.VillagerId}");
                return;
            }

            SensorSystem.Instance?.RegisterVillager(fsm);
            fsm.InjectDependencies(_registry, _actionDatabase, BuildingQueue.Instance);
            AttachActionIcon(fsm); // 런타임 모집 주민도 아이콘 부착 (C2 버그 수정)
            _villagerFSMs.Add(fsm);

            Debug.Log($"[GameManager] 신규 주민 등록 완료. VillagerId={fsm.Brain?.VillagerId}, " +
                      $"총 주민={_villagerFSMs.Count}명.");
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Unity Editor 디버그 헬퍼
        // ══════════════════════════════════════════════════════════════════════

        #region ── Editor 디버그 헬퍼 ──

#if UNITY_EDITOR
        /// <summary>
        /// Inspector 우클릭 → "DEBUG: 시스템 상태 출력" 으로 현재 상태를 Console에 출력한다.
        /// Play Mode에서만 의미 있는 정보를 출력한다.
        /// </summary>
        [ContextMenu("DEBUG: 시스템 상태 출력 (Print System Status)")]
        private void DebugPrintSystemStatus()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("[GameManager] 시스템 상태 ──────────────────────────────────");

            sb.AppendLine($"  GameTime       : {GameTime:F4} 게임일 경과");
            sb.AppendLine($"  TickCounter    : {_tickCounter}");
            sb.AppendLine($"  Villagers      : {_villagerFSMs.Count}명 등록됨");
            sb.AppendLine($"  GameTimeScale  : {_gameTimeScale}");

            if (_worldState != null)
            {
                sb.AppendLine($"  -- WorldState --");
                sb.AppendLine($"  Wood       : {_worldState.WoodStock:F1}");
                sb.AppendLine($"  Stone      : {_worldState.StoneStock:F1}");
                sb.AppendLine($"  RawFood    : {_worldState.RawFoodStock:F1}");
                sb.AppendLine($"  CookedFood : {_worldState.CookedFoodStock:F1}");
                sb.AppendLine($"  Iron       : {_worldState.IronStock:F1}");
                sb.AppendLine($"  Copper     : {_worldState.CopperStock:F1}");
                sb.AppendLine($"  DroppedItems: {_worldState.DroppedItems?.Count ?? 0}개");
                // [PR Fix]: G-005 — EnemyNearby는 AuthoritativeWorldState.cs에 실제로 존재함이 확인되어 불확실 주석 제거
                sb.AppendLine($"  EnemyNearby: {_worldState.EnemyNearby}");
            }

            sb.AppendLine($"  ActionDatabase : {(_actionDatabase != null ? _actionDatabase.name : "null (미연결)")}");
            sb.AppendLine($"  SensorSystem   : {(SensorSystem.Instance != null ? "OK" : "null")}");
            sb.AppendLine($"  MessageBus     : {(MessageBus.Instance != null ? "OK" : "null")}");
            sb.AppendLine($"  BuildingQueue  : {(BuildingQueue.Instance != null ? "OK" : "null")}");

            // 6B단계 추가: 팩션 AI 상태
            sb.AppendLine($"  -- 6B FactionAI --");
            sb.AppendLine($"  FactionAI 수   : {(_factionAIs?.Length ?? 0)}개");
            sb.AppendLine($"  EnemyFSM 수    : {_enemyFSMs.Count}개");
            sb.AppendLine($"  PlayerStrength : {CalculatePlayerStrength():F1}");
            if (_worldState != null)
            {
                sb.AppendLine($"  WatchtowerBuilt: {_worldState.WatchtowerBuilt}");
                sb.AppendLine($"  ForgeBuilt     : {_worldState.ForgeBuilt}");
            }
            sb.AppendLine("──────────────────────────────────────────────────────────────");

            Debug.Log(sb.ToString());
        }

        [ContextMenu("DEBUG: 마일스톤 — 전사 합류 강제 발동 (Forge Event)")]
        private void DebugTriggerForgeEvent()
        {
            if (_villagerPrefab == null)
            {
                Debug.LogWarning("[GameManager] DEBUG: _villagerPrefab이 null입니다. Inspector에서 Prefab을 연결하세요.");
                return;
            }
            _forgeEventFired = false; // 재발동 허용
            if (_worldState != null) _worldState.ForgeBuilt = true;
            CheckMilestoneEvents();
        }

        [ContextMenu("DEBUG: 마일스톤 — 탐험가 합류 강제 발동 (Day7 Event)")]
        private void DebugTriggerDay7Event()
        {
            if (_villagerPrefab == null)
            {
                Debug.LogWarning("[GameManager] DEBUG: _villagerPrefab이 null입니다. Inspector에서 Prefab을 연결하세요.");
                return;
            }
            _day7EventFired = false; // 재발동 허용
            GameTime = 7f;
            CheckMilestoneEvents();
        }

        [ContextMenu("DEBUG: 테스트 — 전멸 패배 (Lose_Annihilated)")]
        private void DebugTriggerLoseAnnihilated()
        {
            _gameEnded = false; // 재트리거 허용 (테스트용)
            TriggerGameResult(GameResultType.Lose_Annihilated);
        }

        [ContextMenu("DEBUG: 테스트 — Town Hall 패배 (Lose_TownHallFallen)")]
        private void DebugTriggerLoseTownHall()
        {
            _gameEnded = false;
            TriggerGameResult(GameResultType.Lose_TownHallFallen);
        }

        [ContextMenu("DEBUG: 테스트 — 생존 승리 (Win1_Survival)")]
        private void DebugTriggerWin1()
        {
            _gameEnded = false;
            TriggerGameResult(GameResultType.Win1_Survival);
        }

        [ContextMenu("DEBUG: 테스트 — 번영 승리 (Win3_Prosperity)")]
        private void DebugTriggerWin3()
        {
            _gameEnded = false;
            TriggerGameResult(GameResultType.Win3_Prosperity);
        }

        /// <summary>
        /// [15단계 테스트] TownHall 완성 플래그를 강제로 true로 설정한다.
        /// RecruitmentPanel이 HUDManager에 의해 자동 활성화되는지 확인할 때 사용한다.
        /// </summary>
        [ContextMenu("DEBUG: 15단계 테스트 — TownHall 완성 강제 설정")]
        private void DebugForceTownHallBuilt()
        {
            if (_worldState == null)
            {
                Debug.LogWarning("[GameManager] DEBUG: WorldState가 null입니다. Play Mode에서 실행하세요.");
                return;
            }

            _worldState.TownHallBuilt    = true;
            _worldState.TownHallEverBuilt = true;
            Debug.Log("[GameManager] DEBUG: TownHallBuilt = true 강제 설정 완료. " +
                      "HUDManager가 0.5초 이내에 RecruitmentPanel을 활성화해야 합니다.");
        }

        /// <summary>
        /// [15단계 테스트] Forge 완성 플래그를 강제로 true로 설정한다.
        /// Tier2 Warrior 모집 버튼이 활성화되는지 확인할 때 사용한다.
        /// </summary>
        [ContextMenu("DEBUG: 15단계 테스트 — Forge 완성 강제 설정")]
        private void DebugForceForgeBuilt()
        {
            if (_worldState == null)
            {
                Debug.LogWarning("[GameManager] DEBUG: WorldState가 null입니다. Play Mode에서 실행하세요.");
                return;
            }

            _worldState.ForgeBuilt = true;
            Debug.Log("[GameManager] DEBUG: ForgeBuilt = true 강제 설정 완료. " +
                      "Forge 해금 모집 항목 버튼이 활성화되어야 합니다.");
        }

        /// <summary>
        /// [15단계 테스트] Watchtower 완성 플래그를 강제로 true로 설정한다.
        /// Tier3 Explorer/Warrior 모집 버튼이 활성화되는지 확인할 때 사용한다.
        /// </summary>
        [ContextMenu("DEBUG: 15단계 테스트 — Watchtower 완성 강제 설정")]
        private void DebugForceWatchtowerBuilt()
        {
            if (_worldState == null)
            {
                Debug.LogWarning("[GameManager] DEBUG: WorldState가 null입니다. Play Mode에서 실행하세요.");
                return;
            }

            _worldState.WatchtowerBuilt = true;
            Debug.Log("[GameManager] DEBUG: WatchtowerBuilt = true 강제 설정 완료. " +
                      "Watchtower 해금 모집 항목 버튼이 활성화되어야 합니다.");
        }

        /// <summary>
        /// [15단계 테스트] 모집 자원을 충분히 보충한다. (조리 식량 100, 철 50, 구리 30, 은 20)
        /// </summary>
        [ContextMenu("DEBUG: 15단계 테스트 — 모집 자원 보충 (식량/철/구리/은)")]
        private void DebugAddRecruitResources()
        {
            if (_worldState == null)
            {
                Debug.LogWarning("[GameManager] DEBUG: WorldState가 null입니다. Play Mode에서 실행하세요.");
                return;
            }

            _worldState.CookedFoodStock += 100f;
            _worldState.IronStock       += 50f;
            _worldState.CopperStock     += 30f;
            _worldState.SilverStock     += 20f;
            Debug.Log($"[GameManager] DEBUG: 모집 자원 보충 완료. " +
                      $"조리식량={_worldState.CookedFoodStock:F0}, 철={_worldState.IronStock:F0}, " +
                      $"구리={_worldState.CopperStock:F0}, 은={_worldState.SilverStock:F0}");
        }

        // ── Phase 1 검증용 ContextMenu ────────────────────────────────────────

        /// <summary>[Phase 1 F1] 주민당 말풍선 발화 횟수를 출력한다. 분당 2회 이하가 목표.</summary>
        [ContextMenu("DEBUG: Phase1 F1 — 말풍선 발화 카운터 출력")]
        private void DebugPhase1PrintThoughtCounts()
        {
            float elapsed = Mathf.Max(Time.time, 1f);
            foreach (var fsm in _villagerFSMs)
            {
                if (fsm == null || fsm.Brain == null) continue;
                float perMin = fsm.ThoughtBubbleCount / (elapsed / 60f);
                string id    = fsm.AgentId.Length >= 8 ? fsm.AgentId.Substring(0, 8) : fsm.AgentId;
                string judge = perMin <= 2f ? "✅" : "⚠ 초과";
                Debug.Log($"[Phase1-F1] {id}..: 총 {fsm.ThoughtBubbleCount}회 | {perMin:F1}/분 {judge} (기준 ≤2/분)");
            }
        }

        /// <summary>[Phase 1 F5] 주민 1번 체력을 25로 설정하고 리플래닝을 유도한다.
        /// 다음 플래닝에서 AttackEnemy 비용이 ×2 이상이 되어야 한다 (VillagerOverviewPanel 플랜 체인 확인).</summary>
        [ContextMenu("DEBUG: Phase1 F5 — 주민 1번 체력 25 설정 (부상 전투 비용 테스트)")]
        private void DebugPhase1SetVillagerHealth25()
        {
            if (_villagerFSMs == null || _villagerFSMs.Count == 0)
            {
                Debug.LogWarning("[Phase1-F5] 등록된 주민이 없습니다. Play Mode에서 실행하세요.");
                return;
            }
            var fsm = _villagerFSMs[0];
            if (fsm == null || fsm.Brain == null) return;
            fsm.Brain.HealthLevel = 25f;
            fsm.ForceReplan();
            // 기대값: ratio=(50-25)/50=0.5, mult=1+0.5*(3-1)=2.0 → AttackEnemy 비용 ×2.0
            string id = fsm.AgentId.Length >= 8 ? fsm.AgentId.Substring(0, 8) : fsm.AgentId;
            Debug.Log($"[Phase1-F5] {id}..: HealthLevel=25 설정 + 리플래닝 유도. " +
                      $"AttackEnemy 비용 ×2.0 예상. VillagerOverviewPanel 플랜 체인에서 확인하세요.");
        }

        /// <summary>[Phase 1 F7] 모든 자원 노드를 미발견 상태로 초기화하고 전체 주민 리플래닝을 유도한다.
        /// 미발견 자원이 있으면 탐험 비용이 ×0.6, 수집 비용은 ×10이 되어 Explore 플랜이 선택되어야 한다.</summary>
        [ContextMenu("DEBUG: Phase1 F7 — 자원 노드 전체 미발견 초기화 (탐험 유도 테스트)")]
        private void DebugPhase1ResetNodesToUndiscovered()
        {
            var nodes = SensorSystem.Instance?.GetAllNodes();
            if (nodes == null || nodes.Count == 0)
            {
                Debug.LogWarning("[Phase1-F7] SensorSystem 또는 자원 노드가 없습니다.");
                return;
            }
            foreach (var node in nodes)
                node.IsDiscovered = false;
            foreach (var fsm in _villagerFSMs)
                fsm?.ForceReplan();
            Debug.Log($"[Phase1-F7] {nodes.Count}개 노드 미발견 초기화 + {_villagerFSMs.Count}명 리플래닝 유도. " +
                      $"주민들이 Explore 플랜을 선택하는지 'Phase1 F7 — 전체 주민 플랜 출력'으로 확인하세요.");
        }

        /// <summary>[Phase 1 F7] 전체 주민의 현재 Goal과 플랜 체인을 출력한다.</summary>
        [ContextMenu("DEBUG: Phase1 F7 — 전체 주민 플랜 현황 출력")]
        private void DebugPhase1PrintAllPlans()
        {
            foreach (var fsm in _villagerFSMs)
            {
                if (fsm?.Brain == null) continue;
                string plan = (fsm.Brain.CurrentPlanFull != null && fsm.Brain.CurrentPlanFull.Count > 0)
                    ? string.Join("→", fsm.Brain.CurrentPlanFull)
                    : "(없음)";
                string id = fsm.AgentId.Length >= 8 ? fsm.AgentId.Substring(0, 8) : fsm.AgentId;
                Debug.Log($"[Phase1-F7] {id}..: Goal={fsm.Brain.CurrentGoalId ?? "-"} | {plan}");
            }
        }
#endif

        #endregion
    }
}
