using System.Collections;
using System.Collections.Generic;
using AIVillage.Core;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// M0 시뮬레이션 루프 — 舊 GameManager의 틱·초기화 중 M0 필요분만.
    /// 서비스(WorldModel/Discovery/Construction/Planner/GoalSelector)를 조립·소유하고
    /// 0.1초 틱으로 게임 시간과 자원 재생을 진행시킨다. 주민 틱은 W4에서 합류한다.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    [DisallowMultipleComponent]
    public sealed class M0SimulationLoop : MonoBehaviour
    {
        private const float TICK_INTERVAL_SEC = 0.1f; // 알고리즘 상수 (舊 GameTickCoroutine 동일)

        [Tooltip("초기 자원·시간 배율·기지 설정. Assets/M0Config/WorldConfig.asset")]
        [SerializeField] private WorldConfigSO _worldConfig;

        [Tooltip("주민 공통 설정. Assets/M0Config/AgentConfig.asset")]
        [SerializeField] private AgentConfigSO _agentConfig;

        [Tooltip("액션 카탈로그. Assets/M0Config/ActionCatalog.asset")]
        [SerializeField] private ActionCatalog _catalog;

        [Tooltip("Goal 에셋 목록 (우선순위는 각 에셋의 Priority 필드)")]
        [SerializeField] private GoalSO[] _goals;

        [Tooltip("씬의 ResourceNodeSpawner (재사용 컴포넌트)")]
        [SerializeField] private ResourceNodeSpawner _nodeSpawner;

        [Tooltip("주민 스프라이트 세트 (W5). 비우면 원형 마커 폴백.")]
        [SerializeField] private AgentSpriteSetSO _spriteSet;

        [Tooltip("말풍선 한국어 폰트 (W6). NeoDunggeunmoPro SDF. 비우면 TMP 기본 폰트(한글 미표시 위험).")]
        [SerializeField] private TMPro.TMP_FontAsset _bubbleFont;

        [Tooltip("밭 작물 성장 스프라이트 (M2-D). 비우면 색 폴백 (연두 새싹→노랑 결실).")]
        [SerializeField] private CropSpriteSetSO _cropSprites;

        [Tooltip("성격 아키타입 풀 (M4-A) — 스폰 시 랜덤 할당. 비우면 전원 성격 없음(중립, M3 동작).")]
        [SerializeField] private PersonalitySO[] _personalityPool;

        [Tooltip("직업 풀 (M5-A) — 스폰 시 랜덤 할당. 비우면 전원 무직(중립, M4 동작).")]
        [SerializeField] private JobSO[] _jobPool;

        public static M0SimulationLoop Instance { get; private set; }

        public WorldModel World { get; private set; }
        public DiscoveryService Discovery { get; private set; }
        public ConstructionService Construction { get; private set; }

        /// <summary>구역 배치 결정자 (M9-A) — 수량형 건물의 앵커·반경. 첫 완공이 앵커 (ADR-M9-2).</summary>
        public ZoneService Zones { get; private set; }
        public FarmService Farm { get; private set; }

        /// <summary>계절 시계 (M6-A). WorldConfig.SeasonCycle이 비면 null = 계절 없음 (M5 동작).</summary>
        public SeasonService Season { get; private set; }

        /// <summary>좌상단 달력·알림 HUD (M6-C). 표시 전용 — 이탈 알림(M6-D)도 여기로.</summary>
        public SeasonHud Hud { get; private set; }

        /// <summary>주민 상호대화 (M7-C). 표현 전용 — Chatters가 비면 스스로 중립 (M6 동작).</summary>
        public ChatterService Chatter { get; private set; }

        /// <summary>주민 관계 (M8-A). 쓰기 원천은 대화 이벤트·부탁 결과뿐 (ADR-M8-1).</summary>
        public RelationshipService Relationship { get; private set; }

        /// <summary>건물 소유 (M8-C). 쓰기는 Assign/ReleaseBy만 (ADR-M8-3). 세이브 대상.</summary>
        public OwnershipService Ownership { get; private set; }

        /// <summary>주민 부탁 (M8-D). Requests가 비면 스스로 중립 (M7 동작).</summary>
        public RequestService Requests { get; private set; }
        public PlannerGateway Planner { get; private set; }
        public GoalSelector Goals { get; private set; }
        public AgentConfigSO AgentConfig => _agentConfig;
        public ActionCatalog Catalog => _catalog;
        public WorldConfigSO WorldConfig => _worldConfig;
        public AgentSpriteSetSO SpriteSet => _spriteSet;
        public TMPro.TMP_FontAsset BubbleFont => _bubbleFont;

        /// <summary>게임 시간 (게임일 단위). 1게임일 = 0.1초 × (1/GameTimeScale) = 100초 (배율 0.01 기준).</summary>
        public float GameTime { get; private set; }

        /// <summary>JPS용 통행 가능 배열 (배열 인덱스 기준, MapConfig 크기). M0는 장애물 없음 — 전부 true.</summary>
        public bool[,] Walkable { get; private set; }

        /// <summary>경로 탐색 창구 (2026-07-18) — 이동 계층은 이 인터페이스로만 경로를 구한다.
        /// 알고리즘 교체(A*/HPA*)·가중치 지형은 이 뒤에서 흡수한다 (Docs/ADR_경로탐색_확장경계.md).</summary>
        public IPathfinder Pathfinder { get; private set; }

        private BuildingVisualizer _visualizer;
        private ZoneBorderView _zoneBorderView;
        private FarmPlotView _farmView;
        private int _lastLoggedDay = -1;
        private readonly List<VillagerAgent> _agents = new List<VillagerAgent>(8);

        /// <summary>등록된 주민 목록 (PlayerInputController 픽킹용, 읽기 전용).</summary>
        public IReadOnlyList<VillagerAgent> Agents => _agents;

        /// <summary>스폰 시 성격 랜덤 할당 (M4-A). 풀이 비면 null = 중립 (ADR-M4-2 불변식 경로).</summary>
        public PersonalitySO PickRandomPersonality()
            => _personalityPool != null && _personalityPool.Length > 0
                ? _personalityPool[Random.Range(0, _personalityPool.Length)]
                : null;

        /// <summary>스폰 시 직업 랜덤 할당 (M5-A). 풀이 비면 null = 무직(중립, M5-S3 불변식 경로).</summary>
        public JobSO PickRandomJob()
            => _jobPool != null && _jobPool.Length > 0
                ? _jobPool[Random.Range(0, _jobPool.Length)]
                : null;

        public void RegisterAgent(VillagerAgent agent)
        {
            if (agent != null && !_agents.Contains(agent)) _agents.Add(agent);
        }

        public void UnregisterAgent(VillagerAgent agent)
        {
            if (agent == null) return;
            _agents.Remove(agent);
            Relationship?.ReleaseBy(agent.AgentId); // 이탈 시 관계 기록 정리 (M8-A)
            Ownership?.ReleaseBy(agent.AgentId);    // 이탈 시 소유 해제 — 빈집 (M8-C)
            Requests?.ReleaseBy(agent.AgentId);     // 이탈 시 진행 부탁 정리 (M8-D — 유령 유예 방지)
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[M0SimulationLoop] 중복 인스턴스 — 이 오브젝트를 비활성화합니다.");
                gameObject.SetActive(false);
                return;
            }
            Instance = this;

            if (_worldConfig == null || _agentConfig == null || _catalog == null || _nodeSpawner == null)
            {
                Debug.LogError("[M0SimulationLoop] Inspector 슬롯 미연결 (WorldConfig/AgentConfig/Catalog/NodeSpawner 확인).");
                enabled = false;
                return;
            }

            Discovery    = new DiscoveryService();
            Farm         = new FarmService(_worldConfig.FarmGrowthDays);
            // 계절 시계 (M6-A) — 사이클이 비면 서비스 자체를 null로 (중립 불변식, M6-T1b)
            var season = new SeasonService(_worldConfig.SeasonCycle);
            if (season.IsActive)
            {
                Season = season;
                Season.OnSeasonChanged += s =>
                {
                    Debug.Log($"[M0Sim] 계절 전환 — {s.DisplayName} (Day {(int)GameTime}, 위기={s.IsCrisis})");
                    Hud?.Notify($"계절이 바뀌었습니다 — {s.DisplayName}");
                };
            }
            else
            {
                Debug.LogWarning("[M0SimulationLoop] WorldConfig.SeasonCycle 비어 있음 — 계절 없이 진행 (M5 동작).");
            }
            World        = new WorldModel(Discovery, _worldConfig, Farm, Season);
            Construction = new ConstructionService(World);
            Zones        = new ZoneService(); // M9-A — 배치 결정자 (군집 휴리스틱 대체, ADR-M9-1)
            Planner      = new PlannerGateway(_catalog);
            Goals        = new GoalSelector(_goals);
            Chatter      = new ChatterService(_worldConfig, _agentConfig); // M7-C — 표현 전용 (ADR-M7-1)
            Relationship = new RelationshipService();
            Ownership    = new OwnershipService(); // M8-C — 소유 축
            Requests     = new RequestService(_worldConfig, _agentConfig, Relationship,
                                              Ownership, Construction, _agents,
                                              Chatter, World); // M8-D — 부탁 선반 (대화 쿨다운·보상 스톡)
            // 대화 → 관계 축적의 유일한 배선 (M8-A, ADR-M8-1) — 본체는 ApplyChat (게이트 대상)
            Chatter.OnChatted += (c, speaker, target) => Relationship.ApplyChat(c, speaker.AgentId, target.AgentId);

            _visualizer = new BuildingVisualizer(transform);
            Construction.OnCompleted += (b, x, y) => _visualizer.Spawn(b, x, y);
            Construction.OnRemoved += (slot, x, y) => _visualizer.Remove(slot, x, y); // M9-B 시각 파괴
            // 밭 시설 소실 → FarmService.RemovePlot (RemovePlot의 유일한 호출 경로, ADR-M9-3 대칭)
            Construction.OnRemoved += (slot, x, y) =>
            {
                if (slot == SlotId.FarmPlotCount) Farm.RemovePlot(x, y);
            };
            // 구역 확정 = 첫 완공 (M9-A, ADR-M9-2) — NotifyBuilt가 첫 완공만 앵커로 잡는다
            Construction.OnCompleted += (b, x, y) => Zones.NotifyBuilt(b, x, y);
            // 구역 테두리 (표현 전용) — 확정 순간 앵커 둘레에 외곽선
            _zoneBorderView = new ZoneBorderView(transform);
            Zones.OnZoneEstablished += (slot, anchor, radius) => _zoneBorderView.Draw(slot, anchor, radius);
            // 밭 완공 → FarmService 등록 (RegisterPlot의 유일한 호출 경로, ADR-M2-4)
            Construction.OnCompleted += (b, x, y) =>
            {
                if (b.IsCountable && b.CountSlot == SlotId.FarmPlotCount)
                    Farm.RegisterPlot(x, y);
            };
            _farmView = new FarmPlotView(transform, _cropSprites, Farm); // M2-D 성장 표현 (이벤트 구독)
            // 통행 차단 건물 → Walkable 갱신의 유일한 지점 (ADR-M3-3) — JPS는 이 배열만 보고 우회한다
            Construction.OnCompleted += (b, x, y) =>
            {
                if (b.BlocksMovement && MapBounds.ToArrayIndex(x, y, out int ax, out int ay))
                    Walkable[ax, ay] = false;
            };

            // JPS 통행 배열 — Bootstrap(-95)이 MapConfig를 먼저 활성화한다
            int mapSize = MapConfig.Active != null ? MapConfig.Active.mapSize : 100;
            Walkable = new bool[mapSize, mapSize];
            for (int x = 0; x < mapSize; x++)
                for (int y = 0; y < mapSize; y++)
                    Walkable[x, y] = true;

            // 경로 탐색 창구 — Walkable을 지연 조회하는 JPS 어댑터로 초기화한다.
            // 후반 A*/HPA* 교체는 이 한 줄만 바꾼다 (Docs/ADR_경로탐색_확장경계.md).
            Pathfinder = new JpsPathfinder(() => Walkable);
        }

        private void Start()
        {
            // 노드 스폰 — M0 경로: SensorSystem 대신 DiscoveryService가 등록처 (W0 이월분 해소)
            bool ok = _nodeSpawner.SpawnAll(
                _worldConfig.BaseTileX, _worldConfig.BaseTileY, _worldConfig.BaseDiscoverRadius, Discovery);
            if (!ok)
            {
                Debug.LogError("[M0SimulationLoop] 자원 노드 스폰 실패 — 루프를 시작하지 않습니다.");
                return;
            }
            Debug.Log($"[M0Sim] 시작 — 노드 {Discovery.Nodes.Count}개, " +
                      $"Wood {World.GetStock(SlotId.WoodStock)}, RawFood {World.GetStock(SlotId.RawFoodStock)}");

            // 씬 배선 없음 — BuildingVisualizer 패턴 (M6-C). 관계·소유·부탁 참조는 표기 전용 (M8-B/C/후속)
            Hud = new SeasonHud(transform, _bubbleFont, Relationship, _worldConfig, Ownership, Requests);

            StartCoroutine(TickLoop());
        }

        private IEnumerator TickLoop()
        {
            var wait = new WaitForSeconds(TICK_INTERVAL_SEC);
            while (true)
            {
                yield return wait;

                float deltaGameDays = TICK_INTERVAL_SEC * _worldConfig.GameTimeScale;
                GameTime += deltaGameDays; // 계절 배율 금지 — 겨울이 시간을 늦추면 안 된다 (ADR-M6-1)

                Season?.Tick(GameTime);

                // 계절 배율 (M6-B) — 서비스 시그니처 무변경, 시간 입력만 스케일 (겨울 0 = 재생·성장 정지)
                float regenMult  = Season != null ? Season.RegenMult  : 1f;
                float growthMult = Season != null ? Season.GrowthMult : 1f;
                Discovery.TickRegeneration(deltaGameDays * regenMult);
                Farm.TickGrowth(deltaGameDays * growthMult);

                Hud?.Tick(GameTime, Season, _worldConfig.ForecastDays);

                // 에이전트 틱 (W4) — 역순 순회: SimTick 중 파괴/해제로 리스트가 줄어도 안전
                for (int i = _agents.Count - 1; i >= 0; i--)
                    _agents[i].SimTick(TICK_INTERVAL_SEC, deltaGameDays);

                Chatter.Tick(Time.time, _agents); // M7-C — 주기·쿨다운은 실시간 초 기준
                Requests.Tick(Time.time, _agents); // M8-D — 부탁 스캔 (한 주기 1건)
                // 소유 배정은 부탁 완수(RequestService.NotifyFulfilled)가 유일한 경로 —
                // 자동 클레임 패스는 폐기 (2026-07-18 사용자 결정: 부탁 없이 집이 생기면 안 된다)

                // 하루 경계 로그 — W3 관측용 (Play 검증 지표)
                int day = (int)GameTime;
                if (day > _lastLoggedDay)
                {
                    _lastLoggedDay = day;
                    string seasonStr = Season?.Current != null
                        ? $"{Season.Current.DisplayName}(위기까지 {Mathf.CeilToInt(Season.DaysToCrisis)}일)" : "-";
                    Debug.Log($"[M0Sim] Day {day} [{seasonStr}] — Wood {World.GetStock(SlotId.WoodStock)}, " +
                              $"Stone {World.GetStock(SlotId.StoneStock)}, " +
                              $"RawFood {World.GetStock(SlotId.RawFoodStock)}, " +
                              $"Cooked {World.GetStock(SlotId.CookedFoodStock)}, " +
                              $"Farm {World.GetStock(SlotId.FarmPlotCount)}, " +
                              $"발견 W/S/F={Discovery.HasDiscovered(ResourceType.Wood)}/" +
                              $"{Discovery.HasDiscovered(ResourceType.Stone)}/" +
                              $"{Discovery.HasDiscovered(ResourceType.RawFood)}");
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
