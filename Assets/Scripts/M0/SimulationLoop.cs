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

        public static M0SimulationLoop Instance { get; private set; }

        public WorldModel World { get; private set; }
        public DiscoveryService Discovery { get; private set; }
        public ConstructionService Construction { get; private set; }
        public PlannerGateway Planner { get; private set; }
        public GoalSelector Goals { get; private set; }
        public AgentConfigSO AgentConfig => _agentConfig;
        public WorldConfigSO WorldConfig => _worldConfig;
        public AgentSpriteSetSO SpriteSet => _spriteSet;
        public TMPro.TMP_FontAsset BubbleFont => _bubbleFont;

        /// <summary>게임 시간 (게임일 단위). 1게임일 = 0.1초 × (1/GameTimeScale) = 100초 (배율 0.01 기준).</summary>
        public float GameTime { get; private set; }

        /// <summary>JPS용 통행 가능 배열 (배열 인덱스 기준, MapConfig 크기). M0는 장애물 없음 — 전부 true.</summary>
        public bool[,] Walkable { get; private set; }

        private BuildingVisualizer _visualizer;
        private int _lastLoggedDay = -1;
        private readonly List<VillagerAgent> _agents = new List<VillagerAgent>(8);

        /// <summary>등록된 주민 목록 (PlayerInputController 픽킹용, 읽기 전용).</summary>
        public IReadOnlyList<VillagerAgent> Agents => _agents;

        public void RegisterAgent(VillagerAgent agent)
        {
            if (agent != null && !_agents.Contains(agent)) _agents.Add(agent);
        }

        public void UnregisterAgent(VillagerAgent agent)
        {
            _agents.Remove(agent);
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
            World        = new WorldModel(Discovery, _worldConfig);
            Construction = new ConstructionService(World);
            Planner      = new PlannerGateway(_catalog);
            Goals        = new GoalSelector(_goals);

            _visualizer = new BuildingVisualizer(transform);
            Construction.OnCompleted += (b, x, y) => _visualizer.Spawn(b, x, y);

            // JPS 통행 배열 — Bootstrap(-95)이 MapConfig를 먼저 활성화한다
            int mapSize = MapConfig.Active != null ? MapConfig.Active.mapSize : 100;
            Walkable = new bool[mapSize, mapSize];
            for (int x = 0; x < mapSize; x++)
                for (int y = 0; y < mapSize; y++)
                    Walkable[x, y] = true;
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

            StartCoroutine(TickLoop());
        }

        private IEnumerator TickLoop()
        {
            var wait = new WaitForSeconds(TICK_INTERVAL_SEC);
            while (true)
            {
                yield return wait;

                float deltaGameDays = TICK_INTERVAL_SEC * _worldConfig.GameTimeScale;
                GameTime += deltaGameDays;

                Discovery.TickRegeneration(deltaGameDays);

                // 에이전트 틱 (W4) — 역순 순회: SimTick 중 파괴/해제로 리스트가 줄어도 안전
                for (int i = _agents.Count - 1; i >= 0; i--)
                    _agents[i].SimTick(TICK_INTERVAL_SEC, deltaGameDays);

                // 하루 경계 로그 — W3 관측용 (Play 검증 지표)
                int day = (int)GameTime;
                if (day > _lastLoggedDay)
                {
                    _lastLoggedDay = day;
                    Debug.Log($"[M0Sim] Day {day} — Wood {World.GetStock(SlotId.WoodStock)}, " +
                              $"Stone {World.GetStock(SlotId.StoneStock)}, " +
                              $"RawFood {World.GetStock(SlotId.RawFoodStock)}, " +
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
