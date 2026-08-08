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

        [Tooltip("집들이 연출 (M11-F) — 새 집 소유 배정 순간 집주인+이웃 릴레이. 비우면 집들이 없음(중립). " +
                 "舊 농부 회의(M9-E)의 자리 — 개인 택지 시대엔 마을의 장면이 집들이다.")]
        [SerializeField] private ChatterSO _housewarmingChatter;

        [Tooltip("방랑자 도착 술렁임 (M10-E) — 어귀 도착 순간 최근접 주민 릴레이. 비우면 술렁임 없음(중립).")]
        [SerializeField] private ChatterSO _wandererChatter;

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

        /// <summary>집 저장 식량 (M11-A). 쓰기는 TryAdd/TrySpend만 (ADR-M11-1). 세이브 대상.</summary>
        public HomeStorageService HomeStorage { get; private set; }

        /// <summary>주민 부탁 (M8-D). Requests가 비면 스스로 중립 (M7 동작).</summary>
        public RequestService Requests { get; private set; }

        /// <summary>재해 (M9-C). Disasters가 비면 서비스 null = 재해 없음 (중립 불변식, M8 동작).</summary>
        public DisasterService Disaster { get; private set; }

        /// <summary>야생 위협 (M10-C). Threats가 비면 서비스 null = 위협 없음 (중립 불변식, M9 동작).</summary>
        public ThreatService Threats { get; private set; }

        /// <summary>전투 판정 (M21-W3, ADR-M21-3). **Threats와 생사를 같이 한다** — 때릴 위협이
        /// 없는 판에 전투 판정만 살아 있으면 그게 더 이상하다 (중립 불변식).</summary>
        public CombatService Combat { get; private set; }

        /// <summary>방랑자 (M10-E). WandererIntervalDays ≤ 0이면 서비스 null = 방랑자 없음 (중립 불변식).</summary>
        public WandererService Wanderers { get; private set; }
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

        /// <summary>위협용 통행 배열 (ADR-M22-1: 개체별 통행 = 배열 선택). 문(BlocksThreatMovement)
        /// 타일만 여기서 추가로 false — 문 없는 세계에서는 Walkable과 항상 동일하다.</summary>
        public bool[,] ThreatWalkable { get; private set; }

        /// <summary>경로 탐색 창구 (2026-07-18) — 이동 계층은 이 인터페이스로만 경로를 구한다.
        /// 알고리즘 교체(A*/HPA*)·가중치 지형은 이 뒤에서 흡수한다 (Docs/ADR_경로탐색_확장경계.md).</summary>
        public IPathfinder Pathfinder { get; private set; }

        /// <summary>위협 전용 경로 창구 (ADR-M22-1) — ThreatWalkable을 보는 두 번째 JPS 인스턴스.
        /// "누가 묻는가"는 시그니처가 아니라 인스턴스 선택이 흡수한다 (트리거 C, 확장경계 ADR).</summary>
        public IPathfinder ThreatPathfinder { get; private set; }

        /// <summary>이 건물이 주민 통행을 막는가 (ADR-M22-1 순수 규칙 — 게이트가 씬 없이 검산).</summary>
        public static bool BlocksVillagerPassage(BuildingSO b) => b.BlocksMovement;

        /// <summary>이 건물이 위협 통행을 막는가. 문(BlocksThreatMovement)은 여기만 참이 된다 —
        /// 주민 쪽에 문 예외 분기를 만들면 ADR-M22-1 위반이다.</summary>
        public static bool BlocksThreatPassage(BuildingSO b) => b.BlocksMovement || b.BlocksThreatMovement;

        private BuildingVisualizer _visualizer;
        private ZoneBorderView _zoneBorderView;
        private FarmPlotView _farmView;
        private int _lastLoggedDay = -1;
        private readonly List<VillagerAgent> _agents = new List<VillagerAgent>(8);

        /// <summary>등록된 주민 목록 (PlayerInputController 픽킹용, 읽기 전용).</summary>
        public IReadOnlyList<VillagerAgent> Agents => _agents;

        /// <summary>성격별 행동 계측 (M12-J) — 읽기 전용 관측, 세이브 대상 아님.</summary>
        public BehaviorProfiler Profiler { get; } = new BehaviorProfiler();

        /// <summary>주민 생애 기록 (M13-C1, 새 선반 ②) — 개인 단위, 죽어도 남는다.
        /// 세이브 대상 (ADR-M0-10). 쓰기 지점은 ADR-M13-3 참조 (Die·StarveToDeath + 등장·보루).</summary>
        public ChronicleService Chronicle { get; } = new ChronicleService();

        /// <summary>스폰 시 성격 랜덤 할당 (M4-A). 풀이 비면 null = 중립 (ADR-M4-2 불변식 경로).</summary>
        public PersonalitySO PickRandomPersonality()
            => _personalityPool != null && _personalityPool.Length > 0
                ? _personalityPool[Random.Range(0, _personalityPool.Length)]
                : null;

        // 舊 PickRandomJob(M5-A 균등 랜덤)은 M12-H 이후 호출처 0으로 확인되어 삭제 (ADR-M0-4:
        // 폐기=삭제, git이 히스토리다). 배정의 유일한 창구는 아래 PickJobFor(성향 편향)뿐.

        /// <summary>가중치 하한 (알고리즘 상수 — 밸런스 아님). 0을 허용하면 편향이 결정론이 되어
        /// "게으른데 손재주는 있는 목수"가 구조적으로 불가능해진다 (M12-H ⚠️).</summary>
        private const float MIN_JOB_WEIGHT = 0.05f;

        /// <summary>
        /// 성향 편향 직업 추첨 (M12-H, 순수 — 게이트 M12-T14). roll01을 주입받아 결정적이다.
        /// 반환은 pool 인덱스, **-1 = 무직**.
        ///
        /// 가중치 = max(하한, 1 + Bias(traits, job.PreferWeights) × JobBiasStrength) — ④대상과
        /// 같은 유도식(후보 점수화)이고 유도는 TraitVector 한 곳에서만 한다 (ADR-M12-5).
        /// 전 직업 PreferWeights가 비면 전부 가중치 1 = 균등 = 현행 독립 랜덤 (중립 불변식).
        ///
        /// 무직은 근면이 NoJobBelowDiligence 미만일 때만 후보로 **추가**된다 — 게으름뱅이가 보통
        /// 놀러 다니게 하되(M11의 "게으름 = 대비만" 정의 개정), 규칙이 아니라 확률로 둔다.
        /// </summary>
        public static int PickJobIndex(TraitValue[] traits, JobSO[] pool, TraitRulesSO rules, float roll01)
        {
            if (pool == null || pool.Length == 0) return -1;

            float strength = rules != null ? rules.JobBiasStrength : 0f;
            float total = 0f;
            for (int i = 0; i < pool.Length; i++)
                total += JobWeight(traits, pool[i], strength);

            // 무직 후보 — 문턱에 걸린 사람에게만, 그것도 가중치일 뿐이다.
            float noJob = 0f;
            if (rules != null && rules.NoJobWeight > 0f
                && TraitVector.ValueOf(traits, TraitId.Diligence) < rules.NoJobBelowDiligence)
                noJob = rules.NoJobWeight;

            if (total + noJob <= 0f) return -1; // 있을 수 없음(하한 > 0) — 방어
            float pick = Mathf.Clamp01(roll01) * (total + noJob);

            float acc = 0f;
            for (int i = 0; i < pool.Length; i++)
            {
                acc += JobWeight(traits, pool[i], strength);
                if (pick < acc) return i;
            }
            return noJob > 0f ? -1 : pool.Length - 1; // 부동소수 꼬리는 마지막 후보로
        }

        private static float JobWeight(TraitValue[] traits, JobSO job, float strength)
            => job == null ? 0f
             : Mathf.Max(MIN_JOB_WEIGHT, 1f + TraitVector.Bias(traits, job.PreferWeights) * strength);

        /// <summary>
        /// 스폰 시 직업 배정 (M12-H) — 성격·직업 독립 랜덤을 폐기하고 성향으로 편향시킨다.
        /// 여기가 배정의 유일한 창구다.
        ///
        /// M20-W5: 목수 최소 보장을 삭제했다. 보장의 근거는 "집은 목수 부탁 전용이라 목수 없는
        /// 판은 집이 아예 안 선다"였는데, M19 독점 해제로 누구나 짓게 되면서 소멸했다.
        /// 이제 **어떤 직업도 보장하지 않는다** — "목수 없는 판"이 존재할 수 있어야 직업이
        /// "부재가 느껴지는 장치"가 된다 (ADR-M20-1).
        /// </summary>
        public JobSO PickJobFor(TraitValue[] traits) // M14-W3: 개체 편차 포함 벡터를 직접 받는다
        {
            if (_jobPool == null || _jobPool.Length == 0) return null; // 중립 — 전원 무직 (M5-S3)

            int idx = PickJobIndex(traits, _jobPool,
                                   _worldConfig != null ? _worldConfig.TraitRules : null, Random.value);
            return idx >= 0 ? _jobPool[idx] : null;
        }

        public void RegisterAgent(VillagerAgent agent)
        {
            if (agent != null && !_agents.Contains(agent))
            {
                _agents.Add(agent);
                _everHadAgents = true; // 전멸 판정용 — 시작 전 빈 목록과 전멸을 구분 (M10-F)
                PeakPopulation = Mathf.Max(PeakPopulation, _agents.Count); // 최대 인구 (M14-W4 — 쓰기 이 한 곳)
                // 명부 등장 기록 (M13-C1) — 등록의 유일한 문에 얹는다. 성격·직업은 이 시점에
                // 이미 확정 (VillagerAgent.Start가 할당 후 등록). 문자열 규약 = ComposeSelected와 동일.
                Chronicle.RecordBirth(agent.AgentId, agent.ShortName,
                    agent.Personality != null ? agent.Personality.DisplayName : "없음",
                    agent.Job != null ? agent.Job.DisplayName : "무직", GameTime);
            }
        }

        /// <summary>상태줄 line번째 굶는 주민 (M13-B 후속 — 클릭 점프용). 굶는 줄 범위 밖이면 null.
        /// _starvingBuf가 표시(ComposeStatus)와 같은 원천이라 줄 순서가 화면과 일치한다 —
        /// 클릭과 갱신이 한 틱(0.1초) 어긋날 수 있으나 무해 (다른 굶는 주민을 집을 뿐).
        /// ⚠️ "굶는 줄 = 상태줄 맨 앞"이 전제 — 새 상태 종류는 반드시 굶는 줄 **뒤에** 추가
        /// (ComposeStatus의 확장 규칙 주석과 짝).</summary>
        public VillagerAgent FindStarvingVillagerAt(int line)
        {
            if (line < 0 || line >= _starvingBuf.Count) return null;
            string name = _starvingBuf[line].name;
            foreach (VillagerAgent a in _agents)
                if (a != null && a.State != AgentState.Dead && a.ShortName == name) return a;
            return null;
        }

        // 굶는 주민 열거 버퍼 (M13-B) — 프레임 재사용, 할당 0 (부상자 버퍼 패턴).
        // 정렬 비교자는 정적 캐시 — 틱마다 람다 할당 방지. 급한 순, 동률은 이름 순 (결정적).
        // (M19-W4: M16의 지갑 병기 3튜플·병렬 리스트는 화폐와 함께 철거 — 2튜플 원형 복귀)
        // (2026-08-07: 판정 기준이 저장 식량 → 몸 상태로 바뀌며 포만·등급이 튜플에 들어왔다.
        //  식량 일수는 판정에서 빠지고 표시용 참고 수치로만 남는다 — ComposeStatus 주석 참조.)
        private readonly List<(string name, int satiety, int foodDays, bool critical)> _starvingBuf =
            new List<(string, int, int, bool)>(8);
        // 적습 상시 프롬프트의 현재 문구 (M21-W8) — null = 미표시. 방랑자 제안에 슬롯을 양보한다.
        private string _raidPrompt;
        // 급한 순 = 포만 낮은 순 (굶주림 등급이 곧 포만 순서라 별도 키가 필요 없다).
        private static readonly System.Comparison<(string name, int satiety, int foodDays, bool critical)>
            ByHungerUrgency = (a, b) => a.satiety != b.satiety
                ? a.satiety.CompareTo(b.satiety) : string.CompareOrdinal(a.name, b.name);

        // 겨울 미대비 버퍼 전용 (M14-W4) — 이쪽은 여전히 **저장 식량**이 판정 기준이다.
        // 겨울 대비는 몸 상태가 아니라 비축의 문제이므로 기준이 갈리는 것이 옳다 (판정 이원화 아님).
        private static readonly System.Comparison<(string name, int days)> ByFoodUrgency =
            (a, b) => a.days != b.days ? a.days.CompareTo(b.days) : string.CompareOrdinal(a.name, b.name);

        // (M19-W4: 물가·예보·발행·세율의 재정 하위계는 화폐와 함께 철거됐다 — ADR-M19-1.
        //  이력은 git — M16-W4 물가 캐시 · M17-W2 세율 · M17-W3 발행 · M17-W4 예보 · M17-R6 한도.)

        // 겨울 미대비 열거 버퍼 (M14-W4) — _starvingBuf와 동일 패턴 (재사용·정렬·급한 순)
        private readonly List<(string name, int days)> _unpreparedBuf = new List<(string, int)>(8);

        // 부상자 탐색 버퍼 (M10-B) — 프레임 재사용, 할당 0 (농부 회의 버퍼 패턴)
        private readonly List<VillagerAgent> _injuredBuf = new List<VillagerAgent>(8);
        private readonly List<(string id, int x, int y)> _injuredKeyBuf = new List<(string, int, int)>(8);

        /// <summary>
        /// 최근접 부상자 (M10-B) — TendRunner 전용 창구. 미간호 우선(2패스), 거리순, 동률은
        /// AgentId 사전순 (결정적 — ADR-M10-1). 미간호가 없으면 간호 중인 부상자도 반환 —
        /// 두 번째 간호자는 무해(표시 갱신뿐)하고, 첫 간호자가 P0로 떠날 때 끊김 없이 인계된다.
        /// </summary>
        public VillagerAgent FindNearestInjured(VillagerAgent tender, bool healerMode)
        {
            // 일반 간호자(응급조치)는 미안정 부상자만 대상 — 안정화 완료자를 계속 붙잡지 않는다
            // (crowding 해소, M11-I). 치료사(healer)는 전 부상자 대상 (안정화 여부 무관).
            VillagerAgent found = FindInjuredPass(tender, untendedOnly: true, healerMode);
            return found != null ? found : FindInjuredPass(tender, untendedOnly: false, healerMode);
        }

        private VillagerAgent FindInjuredPass(VillagerAgent tender, bool untendedOnly, bool healerMode)
        {
            _injuredBuf.Clear();
            _injuredKeyBuf.Clear();
            foreach (VillagerAgent a in _agents)
            {
                if (a == null || a == tender || a.State == AgentState.Dead
                    || a.Injury == InjurySeverity.None) continue;
                if (!healerMode && a.IsStabilized) continue; // 일반 간호자는 안정화 완료자 제외
                if (untendedOnly && a.IsTended) continue;
                _injuredBuf.Add(a);
                _injuredKeyBuf.Add((a.AgentId, a.TileX, a.TileY));
            }
            int idx = PickNearestIndex(tender.TileX, tender.TileY, _injuredKeyBuf);
            return idx >= 0 ? _injuredBuf[idx] : null;
        }

        /// <summary>최근접 인덱스 선택 (순수 — 게이트 M10-T2): 거리 제곱 오름차순, 동률은 id
        /// 사전순(ordinal — 지역 무관 결정성). 빈 목록 -1.</summary>
        public static int PickNearestIndex(int fromX, int fromY,
                                           IReadOnlyList<(string id, int x, int y)> candidates)
        {
            int best = -1;
            long bestD = long.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                long dx = candidates[i].x - fromX, dy = candidates[i].y - fromY;
                long d = dx * dx + dy * dy;
                if (d < bestD
                    || (d == bestD && best >= 0
                        && string.CompareOrdinal(candidates[i].id, candidates[best].id) < 0))
                {
                    bestD = d;
                    best = i;
                }
            }
            return best;
        }

        // ── 런타임 인구 문 (M10-E — 새 선반 ③의 심장. 시작 드래프트·M11 재건이 재사용 예정) ──

        // 사용 이력 이름 집합 — 사망·이탈로 빠져도 이름은 예약 유지 (같은 이름 재등장 = 서사 혼동).
        // 첫 사용 시 현 주민으로 시드. 세이브 대상 (ADR-M0-10 — 카운터와 함께).
        private readonly HashSet<string> _usedNames = new HashSet<string>();

        /// <summary>누적 정착 수 (M10-E) — 쓰기는 SpawnVillager뿐. 세이브 대상 (ADR-M10-10).</summary>
        public int SettleCount { get; private set; }

        /// <summary>
        /// 런타임 주민 스폰의 유일한 문 (M10-E). 이름 발급 → GameObject 생성 → Preset 주입 →
        /// VillagerAgent.Start가 기존 스폰 파이프라인(등록·편차·뷰·타일 예약)을 전부 수행한다.
        /// 호출처는 WandererService.Resolve(수락)뿐 — 두 번째 인구 유입 경로가 생기면 반려.
        /// </summary>
        public VillagerAgent SpawnVillager(Vector2Int tile, PersonalitySO p, JobSO j)
        {
            if (_usedNames.Count == 0)
                foreach (VillagerAgent a in _agents)
                    if (a != null) _usedNames.Add(a.AgentId);

            string name = NextSpawnName(_usedNames);
            _usedNames.Add(name);
            var go = new GameObject(name); // Awake가 go.name을 AgentId로 읽는다 — 생성 시점 확정
            go.transform.position = new Vector3(tile.x, tile.y, 0f); // ADR-M0-9 — X-Y 평면
            VillagerAgent agent = go.AddComponent<VillagerAgent>();
            agent.Preset(p, j); // Start 전 주입 보장 (AddComponent는 Awake만 즉시 실행)
            SettleCount++;
            Debug.Log($"[Wanderer] 정착 — {name} ({(p != null ? p.DisplayName : "중립")}·{(j != null ? j.DisplayName : "무직")})");
            return agent;
        }

        /// <summary>
        /// 스폰 이름 발급 (순수 — 게이트 M10-T5): A~Z, 그다음 AA·AB… 순으로 미사용 첫 이름.
        /// 결정적 — 같은 사용 집합이면 같은 이름 (StableHash 개체 편차의 시드가 이름이라 중요).
        /// </summary>
        public static string NextSpawnName(ICollection<string> usedNames)
        {
            for (int i = 0; ; i++)
            {
                string name = "M0_Villager_" + ToLetters(i);
                if (!usedNames.Contains(name)) return name;
            }
        }

        private static string ToLetters(int index)
        {
            string s = "";
            index++;
            while (index > 0)
            {
                index--;
                s = (char)('A' + index % 26) + s;
                index /= 26;
            }
            return s;
        }

        // 방랑자 술렁임 청중 버퍼 (M10-E) — 농부 회의 버퍼와 분리 (같은 프레임 겹침 방어)
        private readonly List<VillagerAgent> _wandererListeners = new List<VillagerAgent>(8);

        /// <summary>방랑자 도착 술렁임 (M10-E, 표현 전용) — 어귀 최근접 주민이 화자, 반경 내 릴레이
        /// (ShowHousewarming과 같은 패턴 — FireScene 이벤트 전용 대화).</summary>
        private void ShowWandererMurmur(Vector2Int arriveTile)
        {
            if (_wandererChatter == null || _agents.Count == 0) return;

            VillagerAgent speaker = null;
            int best = int.MaxValue;
            foreach (VillagerAgent a in _agents)
            {
                if (a == null || a.State == AgentState.Dead) continue;
                int dx = a.TileX - arriveTile.x, dy = a.TileY - arriveTile.y;
                int d = dx * dx + dy * dy;
                if (d < best) { best = d; speaker = a; }
            }
            if (speaker == null) return;

            _wandererListeners.Clear();
            foreach (VillagerAgent a in _agents)
            {
                if (_wandererListeners.Count >= _wandererChatter.MaxExtraListeners) break;
                if (a == null || a == speaker || a.State == AgentState.Dead) continue;
                if (Mathf.Abs(a.TileX - arriveTile.x) > _wandererChatter.RadiusTiles
                    || Mathf.Abs(a.TileY - arriveTile.y) > _wandererChatter.RadiusTiles) continue;
                _wandererListeners.Add(a);
            }
            if (_wandererListeners.Count == 0) return; // 혼잣말 방지 (회의 연출과 동일)
            Chatter.FireScene(_wandererChatter, speaker, _wandererListeners, Time.time);
        }

        /// <summary>부상 주민 수 집계 (M10-A) — InjuredCount 파생 슬롯의 유일한 원천 (트리거 전용).</summary>
        public int CountInjured()
        {
            int n = 0;
            foreach (VillagerAgent a in _agents)
                if (a != null && a.State != AgentState.Dead && a.Injury != InjurySeverity.None) n++;
            return n;
        }

        /// <summary>미안정 부상자 수 집계 (M11-I) — UntendedInjuredCount 파생 슬롯의 유일한 원천.
        /// 응급조치(안정화)를 받으면 여기서 빠져 Goal_TendInjured 트리거가 꺼진다 → 군중 해산.</summary>
        public int CountUntendedInjured()
        {
            int n = 0;
            foreach (VillagerAgent a in _agents)
                if (a != null && a.State != AgentState.Dead
                    && a.Injury != InjurySeverity.None && !a.IsStabilized) n++;
            return n;
        }

        /// <summary>누적 사망 수 (M10-A) — 쓰기는 RecordDeath뿐. 세이브 대상 (ADR-M0-10).</summary>
        public int DeathCount { get; private set; }

        /// <summary>넘긴 봉쇄 계절(겨울) 수 (M14-W4) — 쓰기는 계절 전환 핸들러 1곳 ("이전이 봉쇄,
        /// 지금은 아님"일 때 +1 — 봉쇄 술어라 여름 위기를 세지 않는다, ADR-M14-2). 세이브 대상.</summary>
        public int WintersSurvived { get; private set; }

        /// <summary>최대 동시 생존 인구 (M14-W4) — 쓰기는 RegisterAgent뿐. 세이브 대상.</summary>
        public int PeakPopulation { get; private set; }

        /// <summary>이번 판 적습 격퇴 수 (M21-W9) — 쓰기는 OnRaidRepelled 구독 1곳
        /// (무리 전원이 전투로 물러난 사건 단위 — 마리 단위 아님). 세이브 대상 (ADR-M0-10).</summary>
        public int RepelCount { get; private set; }

        /// <summary>현재 판 스냅샷 (M15-W2 — 저장과 열람이 같은 함수: 직렬화 경로가 평소
        /// 열람으로도 검증된다, 명세 확정 보완 3). World+UI를 둘 다 아는 조립자는 여기뿐
        /// (ComposeGameOver 호출부 선례 — World→UI 역참조 금지). RunNumber는 안 채운다
        /// (ChronicleArchive.Apply가 부여). EndedAt = 벽시계 — 게임 밖 이력이 목적.</summary>
        public ChronicleArchive.RunEntry SnapshotCurrentRun(bool ended)
        {
            var entry = new ChronicleArchive.RunEntry
            {
                EndedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Winters = WintersSurvived,
                LastDay = (int)GameTime,
                PeakPop = PeakPopulation,
                Settles = SettleCount,
                Ended   = ended,
                // M19-W4: 화폐 철거 — 세 필드는 append-only 규약(ADR-M14-3)으로 잔류하되
                // 기록은 중립값 (옛 판 아카이브는 그대로 표시된다. ComposeRunEconomy는
                // 100/0/0이면 항목을 감춘다 — 새 판에는 경제 줄이 아예 안 나온다)
                PeakPricePct = 100,
                TaxTotal     = 0,
                MintTotal    = 0,
            };
            foreach (VillagerRecord r in Chronicle.RosterByBirth())
                entry.Roster.Add(new ChronicleArchive.VillagerEntry
                {
                    ShortName   = r.ShortName,
                    Personality = r.PersonalityName,
                    Job         = r.JobName,
                    BornDay     = (int)r.BornDay,
                    LeftDay     = (int)r.LeftDay, // -1 센티넬 그대로 (생존 중)
                    Cause       = (int)r.Cause,
                    BuddyShort  = string.IsNullOrEmpty(r.BuddyIdAtExit) ? "" : SeasonHud.ToShortName(r.BuddyIdAtExit),
                    GrudgeShort = string.IsNullOrEmpty(r.GrudgeIdAtExit) ? "" : SeasonHud.ToShortName(r.GrudgeIdAtExit),
                    LifeEvents  = SeasonHud.ComposeLifeEvents(r), // 묶음 요약 재사용 (실사 ③, ADR-M15-3)
                });
            return entry;
        }

        private bool _prevSeasonFrozen; // 겨울 결산 판정용 — 직전 계절의 봉쇄 여부

        // 舊 DepartCount/RecordDepart(M10-F 이탈 계수)는 ADR-M10-3 개정(2026-07-24 — 굶주림이
        // 이탈에서 아사로)으로 호출처 0이 확인되어 삭제 (ADR-M0-4: 폐기=삭제). 새 이탈 사유가
        // 생기면 그때 명세와 함께 재도입한다 — ComposeGameOver 4인자(departs)는 게이트
        // M10-T6이 지키는 순수 함수라 남아 있다.

        // 전멸 종료 (M10-F) — 화면만 덮고 틱은 계속 돈다 (관찰 샌드박스 유지, 재건은 M11).
        private bool _everHadAgents;
        private bool _gameOverShown;
        // 이번 판의 아카이브 자리 (M15-W2). -1 = 아직 안 씀. 세이브 대상 아님 — 판 = 프로세스
        // 수명 (M10-F "새 시작은 재실행"). static 금지 — Enter Play Mode(도메인 리로드 꺼짐)에서 오염.
        private int _archiveRunIndex = -1;

        /// <summary>이번 판의 아카이브 인덱스 (M15 — 패널이 현재 판의 저장분을 목록에서 제외할 때
        /// 쓴다: 첫 겨울 이후엔 라이브 행과 저장 행이 같은 판이라 겹쳐 보인다). -1 = 아직 저장 없음.</summary>
        public int ArchiveRunIndex => _archiveRunIndex;

        /// <summary>전멸 래치 판정 (순수 — 게이트 M10-T6): 주민이 있었던 마을이 0명이 된 첫 순간만.</summary>
        public static bool ShouldShowGameOver(bool alreadyShown, bool everHadAgents, int aliveCount)
            => !alreadyShown && everHadAgents && aliveCount == 0;

        /// <summary>
        /// 사망 기록 (M10-A → M13-A 이름) — 호출처는 VillagerAgent.Die()·StarveToDeath() 2곳뿐
        /// (이탈 이원화 — ADR-M10-3: Depart는 여기 오지 않는다). 카운터 +1 + 사망 타일에 무덤 +
        /// **누구의 흔적인지** (이름·사망일 이름표 — M13-A).
        /// 이름·날짜는 죽는 순간에 인자로 받는다 — OnDestroy 시점엔 지연 파괴·서비스 기록 정리와
        /// 순서가 얽힌다 (M13 명세 §6-A ⚠️).
        /// </summary>
        public void RecordDeath(int tileX, int tileY, string shortName, int day, string agentId)
        {
            DeathCount++;
            // 래칫 완충 (M21-W7) — 사망의 공통 문(Die·StarveToDeath 합류점)이라 여기 한 줄이면
            // 원인 무관 전 사망을 덮는다. 위협 없는 판은 null이라 자연히 무효 (중립 불변식).
            Threats?.NotifyVillagerDeath();
            _graves.Add((new Vector2Int(tileX, tileY), agentId)); // 무덤 조사 등록부 (M13 — 클릭 → 생전 기록)
            var grave = new GameObject($"Grave_{shortName}_{tileX}_{tileY}");
            grave.transform.SetParent(transform, worldPositionStays: false);
            grave.transform.position = new Vector3(tileX, tileY, 0f); // ADR-M0-9 — X-Y 평면

            // 비석 마커 — 루트가 아니라 자식을 축소한다 (M13-A): 舊 코드처럼 루트를 0.5배 하면
            // 이름표까지 반토막 난다. 주민 이름표(스케일 1 루트)와 크기·오프셋을 통일한다.
            var marker = new GameObject("Marker");
            marker.transform.SetParent(grave.transform, worldPositionStays: false);
            marker.transform.localScale = Vector3.one * 0.5f;
            var sr = marker.AddComponent<SpriteRenderer>();
            sr.sprite = M0Sprites.Circle;
            sr.color = new Color(0.45f, 0.45f, 0.5f, 0.9f); // 회색 비석 마커 (아트 교체는 후속 에셋)
            sr.sortingOrder = 5;                             // 주민(10) 아래, 바닥 위

            // 이름표 (M13-A) — 주민과 같은 부품(NameTag) 공유, 크기만 별도 에셋 값
            // (Play 피드백 "작아서 확대해야 보임" — GraveTagFontSize로 Inspector 조절).
            if (_agentConfig != null)
                new NameTag(grave.transform, _bubbleFont, _agentConfig, $"{shortName} · Day {day}",
                            _agentConfig.GraveTagFontSize);
        }

        // 무덤 등록부 (M13) — 타일 + 주인 AgentId. 세이브 대상 (ADR-M0-10 — 무덤 오브젝트와 함께).
        private readonly List<(Vector2Int tile, string agentId)> _graves =
            new List<(Vector2Int, string)>(8);

        /// <summary>무덤 픽킹 (M13 — 클릭 조사). 반경 내 최근접 무덤 주인의 생애 기록.
        /// PickVillager와 같은 거리 픽킹 — 산 주민이 우선이므로 호출자는 주민 픽킹 실패 후에 부른다.</summary>
        public bool TryPickGraveRecord(Vector2 world, float radius, out VillagerRecord record)
        {
            record = null;
            string bestId = null;
            float best = radius;
            foreach ((Vector2Int tile, string agentId) g in _graves)
            {
                float d = Vector2.Distance(world, new Vector2(g.tile.x, g.tile.y));
                if (d < best) { best = d; bestId = g.agentId; }
            }
            return bestId != null && Chronicle.TryGetRecord(bestId, out record);
        }

        public void UnregisterAgent(VillagerAgent agent)
        {
            if (agent == null) return;
            _agents.Remove(agent);
            // 명부 마감 보루 (M13-C1, ADR-M13-3) — 사망 경로(Die·StarveToDeath)를 안 거친
            // 소멸만 Unknown으로 닫는다. 사유가 있는 레코드는 건드리지 않는다 (멱등).
            // ⚠️ 퇴장 기록 본체를 여기 두면 안 된다 — 아래 ReleaseBy와 한 몸이라 C3의 관계
            // 스냅샷 시점과 얽힌다 (명세 §5.4 — 증분이 무너지는 유일한 배치).
            Chronicle.CloseIfOpen(agent.AgentId, GameTime);
            Relationship?.ReleaseBy(agent.AgentId); // 이탈 시 관계 기록 정리 (M8-A)
            Ownership?.ReleaseBy(agent.AgentId);    // 이탈 시 소유 해제 — 빈집 (M8-C)
            Requests?.ReleaseBy(agent.AgentId);     // 이탈 시 진행 부탁 정리 (M8-D — 유령 유예 방지)
        }

        /// <summary>재해 반응 대사 (M9-C, 표현 전용) — 밭 구역 앵커 최근접 최대 2명이 StrikeLines를
        /// 내뱉는다. 릴레이 아님(마주보기·응수 없음). 결정성 불요라 Random 허용 (소실은 결정적, 대사는 표현).</summary>
        private void ShowStrikeLines(DisasterSO d)
        {
            if (d.StrikeLines == null || d.StrikeLines.Length == 0 || _agents.Count == 0) return;
            if (!Zones.TryGetZone(SlotId.FarmPlotCount, out Vector2Int anchor, out _)) return;

            VillagerAgent a1 = null, a2 = null;
            int d1 = int.MaxValue, d2 = int.MaxValue;
            foreach (VillagerAgent a in _agents)
            {
                if (a == null || a.State == AgentState.Dead) continue;
                int dx = a.TileX - anchor.x, dy = a.TileY - anchor.y;
                int dist = dx * dx + dy * dy;
                if (dist < d1) { d2 = d1; a2 = a1; d1 = dist; a1 = a; }
                else if (dist < d2) { d2 = dist; a2 = a; }
            }
            a1?.ShowTransient(d.StrikeLines[Random.Range(0, d.StrikeLines.Length)]);
            a2?.ShowTransient(d.StrikeLines[Random.Range(0, d.StrikeLines.Length)]);
        }

        // 집들이 청중 버퍼 (M11-F, 舊 농부 회의 버퍼) — 프레임 재사용, 할당 0
        private readonly List<VillagerAgent> _meetingListeners = new List<VillagerAgent>(8);

        /// <summary>집들이 연출 (M11-F, 표현 전용 — 舊 ShowFarmMeeting의 자리) — 새 집 소유 배정
        /// 순간 **집주인 본인**이 외치고(⚠️③ — 최근접이 아니다, 주인공이 있는 장면이다) 집 반경
        /// 내 이웃이 릴레이 응수한다. 배정을 중계할 뿐 소유를 정하지 않는다.</summary>
        private void ShowHousewarming(Vector2Int tile, SlotId slot, string ownerId)
        {
            if (slot != SlotId.HouseCount || _housewarmingChatter == null || _agents.Count == 0) return;

            // 화자 = 새 집주인 (주인공 — 근사 금지)
            VillagerAgent speaker = null;
            foreach (VillagerAgent a in _agents)
                if (a != null && a.State != AgentState.Dead && a.AgentId == ownerId) { speaker = a; break; }
            if (speaker == null) return;

            // 청중 = 집 반경 RadiusTiles+2 내 이웃 (화자 제외), MaxExtraListeners 상한
            _meetingListeners.Clear();
            int rr = _housewarmingChatter.RadiusTiles + 2;
            foreach (VillagerAgent a in _agents)
            {
                if (_meetingListeners.Count >= _housewarmingChatter.MaxExtraListeners) break;
                if (a == null || a == speaker || a.State == AgentState.Dead) continue;
                if (Mathf.Abs(a.TileX - tile.x) > rr || Mathf.Abs(a.TileY - tile.y) > rr) continue;
                _meetingListeners.Add(a);
            }
            if (_meetingListeners.Count == 0) return; // 근처에 아무도 없으면 집들이 없음 (혼잣말 방지)
            Chatter.FireScene(_housewarmingChatter, speaker, _meetingListeners, Time.time);
            // 1회성 장면이라 화면 밖에서 벌어져도 놓치지 않도록 알림 (재해 Notify 패턴)
            Hud?.Notify($"{speaker.ShortName}의 새 집 — 집들이 ({_meetingListeners.Count + 1}명)");
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
            var season = new SeasonService(_worldConfig.SeasonCycle, _worldConfig.SeasonPrologueDays);
            if (season.IsActive)
            {
                Season = season;
                Season.OnSeasonChanged += s =>
                {
                    // 채집 봉쇄 상태를 계절 전환마다 노출 — "봉쇄가 실제로 켜졌는가"를 로그 한 줄로
                    // 판정할 수 있어야 한다 (M13 탐지기: 겨울인데 봉쇄=꺼짐이면 에셋 미기입 즉시 발각).
                    Debug.Log($"[M0Sim] 계절 전환 — {s.DisplayName} (Day {(int)GameTime}, 위기={s.IsCrisis}, " +
                              $"야생채집={(s.ForageFrozen ? "봉쇄(열매가 언다)" : "가능")})");
                    Hud?.Notify(s.ForageFrozen
                        ? $"계절이 바뀌었습니다 — {s.DisplayName} · 열매가 얼어 채집할 수 없습니다"
                        : $"계절이 바뀌었습니다 — {s.DisplayName}");
                    if (s.ForageFrozen) LogWinterReadiness(s);
                    // 겨울 결산 (M14-W4) — 봉쇄 계절을 **넘긴** 전환에서 +1 (봉쇄 술어라 여름 위기를
                    // 세지 않는다, ADR-M14-2). 기록 저장 지점 ① (⚠️W4-③ — 나머지는 전멸 래치뿐).
                    // ⚠️ 전멸 후 차단 (자가 재검토 2026-07-31): 전멸 후에도 틱·계절은 계속 돈다
                    // (M10-F 관찰 샌드박스) — 게이트 없으면 빈 마을이 겨울을 무한히 "넘겨" 기록이 오염된다.
                    if (_prevSeasonFrozen && !s.ForageFrozen && !_gameOverShown)
                    {
                        WintersSurvived++;
                        bool newBest = RunRecordStore.SaveIfBetter(WintersSurvived, (int)GameTime, PeakPopulation, RepelCount);
                        // 연대기 갱신 (M15-W2) — 쓰기 지점 ① (ADR-M15-2). 같은 가드 안이라
                        // 전멸 후 빈 마을의 겨울은 아카이브도 오염 못 한다 (M14 버그 수정 공유).
                        _archiveRunIndex = ChronicleArchive.SaveRun(_archiveRunIndex, SnapshotCurrentRun(ended: false));
                        Debug.Log($"[M0Sim] 겨울 {WintersSurvived}번째를 넘겼다 — Day {(int)GameTime}, " +
                                  $"생존 {_agents.Count}명{(newBest ? " · 역대 최고 갱신" : "")} · 연대기 기록");
                    }
                    _prevSeasonFrozen = s.ForageFrozen;
                };
            }
            else
            {
                Debug.LogWarning("[M0SimulationLoop] WorldConfig.SeasonCycle 비어 있음 — 계절 없이 진행 (M5 동작).");
            }
            // 식량 수지 (M9-G, M11-D 개인화) — 가치표는 config.FoodSources에서 파생, 인원 입력은
            // 제거됨 (식량은 개인 단위). 부상 수(M10-A)는 provider 패턴 — 파생 슬롯의 원천은 집계 하나뿐
            World        = new WorldModel(Discovery, _worldConfig, Farm, Season,
                                         _agentConfig, CountInjured, CountUntendedInjured);
            Construction = new ConstructionService(World);
            Zones        = new ZoneService(); // M9-A — 배치 결정자 (군집 휴리스틱 대체, ADR-M9-1)
            Planner      = new PlannerGateway(_catalog, _agentConfig); // M11-A — 개인 상한 전제 주입 (ADR-M11-3)
            Goals        = new GoalSelector(_goals);
            Chatter      = new ChatterService(_worldConfig, _agentConfig); // M7-C — 표현 전용 (ADR-M7-1)
            Relationship = new RelationshipService();
            Ownership    = new OwnershipService(); // M8-C — 소유 축
            HomeStorage  = new HomeStorageService(); // M11-A — 집 저장 (타일 키, ADR-M11-1)
            Requests     = new RequestService(_worldConfig, _agentConfig, Relationship,
                                              Ownership, Construction, _agents,
                                              Chatter, // M8-D — 부탁 선반 (보상은 실물, M19)
                                              // 연대기 게임일은 델리게이트 (정적 Instance 참조 금지)
                                              Chronicle, () => GameTime);
            // 대화 → 관계 축적의 유일한 배선 (M8-A, ADR-M8-1) — 본체는 ApplyChat (게이트 대상)
            Chatter.OnChatted += (c, speaker, target) => Relationship.ApplyChat(c, speaker.AgentId, target.AgentId);

            // 재해 (M9-C) — Disasters가 비면 서비스 null (중립 불변식, SeasonService 패턴).
            // 파괴는 ADR-M9-3의 문(Farm/Construction)을 지난다 — 서비스가 직접 State를 쓰지 않는다.
            if (_worldConfig.Disasters != null && _worldConfig.Disasters.Length > 0)
            {
                Disaster = new DisasterService(_worldConfig.Disasters, Farm, Construction, World);
                Disaster.OnStruck += (d, n) =>
                {
                    Hud?.Notify($"{d.DisplayName}! {n}개 소실");
                    ShowStrikeLines(d); // 근처 주민 반응 대사 (표현 전용, 릴레이 아님)
                };
            }

            _visualizer = new BuildingVisualizer(transform);
            Construction.OnCompleted += (b, x, y, _) => _visualizer.Spawn(b, x, y);
            Construction.OnRemoved += (slot, x, y) => _visualizer.Remove(slot, x, y); // M9-B 시각 파괴
            // 밭 시설 소실 → FarmService.RemovePlot (RemovePlot의 유일한 호출 경로, ADR-M9-3 대칭)
            Construction.OnRemoved += (slot, x, y) =>
            {
                if (slot == SlotId.FarmPlotCount) Farm.RemovePlot(x, y);
            };
            // 집 소실 → 저장 식량 정리 (M11-A, 소멸 경로 단일성 — M12 집 타격-상실 로그가 이 지점에 연결)
            Construction.OnRemoved += (slot, x, y) =>
            {
                if (slot == SlotId.HouseCount) HomeStorage.ReleaseTile(new Vector2Int(x, y));
            };
            // 구역 확정 = 첫 완공 (M9-A, ADR-M9-2) — NotifyBuilt가 첫 완공만 앵커로 잡는다
            Construction.OnCompleted += (b, x, y, _) => Zones.NotifyBuilt(b, x, y);
            // 구역 테두리 (표현 전용) — 확정 순간 앵커 둘레에 외곽선
            _zoneBorderView = new ZoneBorderView(transform);
            Zones.OnZoneEstablished += (slot, anchor, radius) => _zoneBorderView.Draw(slot, anchor, radius);
            // 舊 농부 회의 배선(OnZoneEstablished → ShowFarmMeeting)은 M11-F에서 제거됐다.
            // 개인 택지 시대의 장면은 집들이다 — 소유 배정 이벤트로 옮겨졌다(아래 Ownership.OnAssigned).
            // ZoneService는 휴면 보존 (테두리 뷰·재해 대사 앵커가 여전히 읽는다, ⚠️②).
            Ownership.OnAssigned += (tile, slot, ownerId) => ShowHousewarming(tile, slot, ownerId);
            // 연대기 구독 (M13-C2) — 원본 서비스 무수정 (§4 ① — 이벤트 통로 재사용).
            // 집 획득만 기록 (모닥불 등 다른 소유 슬롯은 1차 등록 6종 밖 — EventId append-only).
            Ownership.OnAssigned += (tile, slot, ownerId) =>
            {
                if (slot == SlotId.HouseCount)
                    Chronicle.RecordEvent(ownerId, EventId.GotHome, GameTime);
            };
            Construction.OnCompleted += (b, x, y, builderId) =>
            {
                if (!string.IsNullOrEmpty(builderId))
                    // 완공 — 지은 사람의 사건. OtherId = 건물명 (에셋 DisplayName — "완공"만으로는
                    // 집인지 밭인지 모른다는 2026-07-30 Play 피드백. 문구는 에셋 출처 유지, ADR-M0-1)
                    Chronicle.RecordEvent(builderId, EventId.Built, GameTime, otherId: b.DisplayName);
            };
            // 밭 완공 → FarmService 등록 (RegisterPlot의 유일한 호출 경로, ADR-M2-4)
            Construction.OnCompleted += (b, x, y, builderId) =>
            {
                if (b.IsCountable && b.CountSlot == SlotId.FarmPlotCount)
                    Farm.RegisterPlot(x, y, builderId); // 소유자 = 지은 사람 (M11-E)
            };
            _farmView = new FarmPlotView(transform, _cropSprites, Farm); // M2-D 성장 표현 (이벤트 구독)
            // 통행 차단 건물 → 통행 배열 갱신의 유일한 지점 (ADR-M3-3) — JPS는 이 배열들만 보고 우회한다.
            // 두 배열 규칙은 순수 함수(BlocksVillagerPassage/BlocksThreatPassage)가 정한다 (ADR-M22-1).
            Construction.OnCompleted += (b, x, y, _) =>
            {
                if (!MapBounds.ToArrayIndex(x, y, out int ax, out int ay)) return;
                if (BlocksVillagerPassage(b)) Walkable[ax, ay] = false;
                if (BlocksThreatPassage(b)) ThreatWalkable[ax, ay] = false;
            };

            // JPS 통행 배열 (주민/위협 2벌, ADR-M22-1) — Bootstrap(-95)이 MapConfig를 먼저 활성화한다
            int mapSize = MapConfig.Active != null ? MapConfig.Active.mapSize : 100;
            Walkable = new bool[mapSize, mapSize];
            ThreatWalkable = new bool[mapSize, mapSize];
            for (int x = 0; x < mapSize; x++)
                for (int y = 0; y < mapSize; y++)
                {
                    Walkable[x, y] = true;
                    ThreatWalkable[x, y] = true;
                }

            // 경로 탐색 창구 — 통행 배열을 지연 조회하는 JPS 어댑터로 초기화한다.
            // 후반 A*/HPA* 교체는 이 두 줄만 바꾼다 (Docs/ADR_경로탐색_확장경계.md).
            Pathfinder = new JpsPathfinder(() => Walkable);
            ThreatPathfinder = new JpsPathfinder(() => ThreatWalkable);

            // 야생 위협 (M10-C) — Threats가 비면 서비스 null (중립 불변식, DisasterService 패턴).
            // 부상·파괴는 문(Injure/RemoveCountableAt)을 지난다 — 서비스가 상태를 직접 쓰지 않는다.
            if (_worldConfig.Threats != null && _worldConfig.Threats.Length > 0)
            {
                // M21-W2R: 인자 2번이 Zones → Season으로 교체됐다. 구역 참조는 b51c631 이후
                // 죽은 코드였고(FarmPlot.ZoneRadius=0이라 등록 자체가 안 된다), 그 자리에 계절이
                // 들어와 "겨울엔 늑대가 굶는다"를 만든다. isWalkable은 배회 목적지 필터.
                // M22-W1: 위협의 경로·통행 원천은 위협용 배열이다 (ADR-M22-1 — 창구는 그대로,
                // 배선의 원천만 교체. 문이 생기기 전엔 두 배열이 동일해 행동 변화 0).
                Threats = new ThreatService(_worldConfig.Threats, Season, Construction,
                                            _agents, _worldConfig, () => ThreatPathfinder,
                                            (x, y) => MapBounds.ToArrayIndex(x, y, out int ax, out int ay)
                                                      && ThreatWalkable[ax, ay],
                                            transform);
                Threats.OnForecast += t =>
                    // 잔여 일수는 실제 스케줄에서 읽는다 (M21-W8) — 정찰 연장 시 WarnDays 상수로
                    // 표기하면 "1일 뒤"가 거짓말이 된다 (판정과 표시는 한 시계).
                    Hud?.Notify($"{t.DisplayName}이(가) 다가옵니다 — {Threats.DaysToStrike(GameTime):0.#}일 뒤");
                Threats.OnStruck += (t, struckVillagers, n, tile, victims) =>
                {
                    // 빈 타격(0명·0개)은 "아무 일도 없었다" — 알림도 대사도 내지 않는다.
                    // 추격 실패 후 퇴장이 명시적 설계 경로라 0 타격이 흔하다 (ThreatAgent.TickChase).
                    if (n <= 0)
                    {
                        Hud?.Notify($"{t.DisplayName}이(가) 마을을 훑고 지나갔습니다 — 피해 없음");
                        return;
                    }
                    Hud?.Notify(struckVillagers ? $"{t.DisplayName} 습격 — 부상 {n}명"
                                                : $"{t.DisplayName} 습격 — 밭 {n}개 소실");
                    if (struckVillagers) ShowVictimStrikeLines(t, victims); // 화자 = 실제 부상자
                    else                 ShowFarmStrikeLines(t, tile);      // 화자 = 근처 주민
                };

                // 전투 판정 (M21-W3) — Threats 분기 **안**에 둔다: 때릴 위협이 없는 판에
                // 전투 판정만 살아 있으면 중립 불변식이 깨진다. 표현 배선은 여기, 판정은 서비스.
                // Threats 를 넘기는 것은 무리 도주선(M21-W6) — 판정은 Combat, 등록부·집행은 Threats.
                Combat = new CombatService(World, () => GameTime, Threats);
                Combat.OnRepelled += (t, attackerId, day) =>
                    Hud?.Notify($"{t.DisplayName}을(를) 물리쳤습니다");
                Combat.OnHunted += (t, attackerId, drop, day) =>
                {
                    Hud?.Notify(drop > 0 ? $"{t.DisplayName} 사냥 성공 — 고기 {drop} 확보"
                                         : $"{t.DisplayName} 사냥 성공");
                    // 연대기 (M21-W9) — 사냥은 잡은 본인의 공적. Value = 드랍 (무리 규모는 격퇴 사건 몫)
                    Chronicle.RecordEvent(attackerId, EventId.Hunted, day, t.DisplayName, drop);
                };
                Combat.OnRouted += (t, attackerId, day) =>
                    Hud?.Notify($"{t.DisplayName} 적습을 물리쳤습니다 — 무리가 달아납니다");
                // 적습 격퇴 (M21-W9) — 무리 전원이 전투로 물러난 사건. 참여 전원의 연대기에 남고
                // (ADR-M21-1 검증 문장: "늑대 다섯 적습을 사냥꾼이 앞장서 물리쳤다"의 재료),
                // 격퇴 카운터가 오른다 (신기록 BestRepels의 원천).
                Threats.OnRaidRepelled += (t, spawnedCount, attackers) =>
                {
                    RepelCount++;
                    foreach (string id in attackers)
                        Chronicle.RecordEvent(id, EventId.Repelled, GameTime, t.DisplayName, spawnedCount);
                };
            }

            // 방랑자 (M10-E) — 주기 ≤ 0이면 서비스 null (중립 불변식). 표현 배선은 전부 여기 —
            // 서비스는 이벤트만 쏘고 HUD·술렁임을 모른다.
            if (_worldConfig.WandererIntervalDays > 0f)
            {
                Wanderers = new WandererService(_worldConfig, this);
                Wanderers.OnOffered += (prompt, tile) =>
                {
                    Hud?.SetPrompt(prompt);
                    Hud?.Notify("방랑자가 마을 어귀에 왔습니다");
                    ShowWandererMurmur(tile);
                };
                Wanderers.OnResolved += accept =>
                {
                    Hud?.ClearPrompt();
                    Hud?.Notify(accept ? "방랑자가 마을에 합류했습니다" : "방랑자가 떠났습니다");
                };
            }
        }

        /// <summary>부상 대사 (표현 전용) — **다친 본인**이 내뱉는다. 화자를 "근처 아무나"로 두면
        /// 상태와 표현이 어긋나 "물렸다는데 아무도 안 다침"이 된다 (2026-07-24 Play 관측).</summary>
        private void ShowVictimStrikeLines(ThreatSO t, System.Collections.Generic.IReadOnlyList<VillagerAgent> victims)
        {
            if (t.StrikeLinesVillager == null || t.StrikeLinesVillager.Length == 0 || victims == null) return;
            foreach (VillagerAgent v in victims)
                v?.ShowTransient(t.StrikeLinesVillager[Random.Range(0, t.StrikeLinesVillager.Length)]);
        }

        /// <summary>밭 소실 반응 대사 (M10-C, 표현 전용) — 다친 사람이 없으므로 타격 지점 최근접
        /// 생존 주민 최대 2명이 내뱉는다 (재해 ShowStrikeLines 패턴 — 릴레이 아님, 대사만 Random 허용).</summary>
        private void ShowFarmStrikeLines(ThreatSO t, Vector2Int tile)
        {
            if (t.StrikeLinesFarm == null || t.StrikeLinesFarm.Length == 0 || _agents.Count == 0) return;
            VillagerAgent a1 = null, a2 = null;
            int d1 = int.MaxValue, d2 = int.MaxValue;
            foreach (VillagerAgent a in _agents)
            {
                if (a == null || a.State == AgentState.Dead) continue;
                int dx = a.TileX - tile.x, dy = a.TileY - tile.y;
                int dist = dx * dx + dy * dy;
                if (dist < d1) { d2 = d1; a2 = a1; d1 = dist; a1 = a; }
                else if (dist < d2) { d2 = dist; a2 = a; }
            }
            a1?.ShowTransient(t.StrikeLinesFarm[Random.Range(0, t.StrikeLinesFarm.Length)]);
            a2?.ShowTransient(t.StrikeLinesFarm[Random.Range(0, t.StrikeLinesFarm.Length)]);
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
            Hud = new SeasonHud(transform, _bubbleFont, Relationship, _worldConfig, Ownership, Requests,
                                HomeStorage, Chronicle); // 집 저장 표기 (M11-A) + 연대기 (M13-C2)

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

                // 재해 발동 검사 (M9-C) — 계절 위상(경과일·서수)을 넘겨준다. 서비스가 null이면 무동작.
                if (Disaster != null && Season?.Current != null)
                {
                    float daysIntoSeason = Season.Current.DurationDays - Season.DaysLeftInSeason;
                    Disaster.Tick(Season.Current, daysIntoSeason, Season.SeasonOrdinal);
                }

                // 야생 위협 스케줄 (M10-C) — 계절과 별개 축 (변인 분리). 서비스 null이면 무동작.
                Threats?.Tick(GameTime);

                // 방랑자 도착·응답 시한 (M10-E) — 서비스 null이면 무동작.
                // 전멸 후 정지 (M10-F): 수락 주체(플레이어의 마을)가 없다 — 재건과 함께 M11에서 개정.
                if (!_gameOverShown) Wanderers?.Tick(GameTime);

                // 전멸 검사 (M10-F) — 1회 래치. 화면만 덮고 시뮬·위협 틱은 지속 (빈 마을의 늑대도 풍경).
                if (ShouldShowGameOver(_gameOverShown, _everHadAgents, _agents.Count))
                {
                    _gameOverShown = true;
                    Wanderers?.Resolve(false); // 진행 중 제안 정리 — 프롬프트 소거·후보 퇴장 (이중 호출 안전)
                    // 회고 = 통계 대신 명부 (M13-C1). 명부가 비면 舊 통계 문구로 폴백 (반례 ③ⓐ —
                    // 이론상 도달 불가하지만, 빈 회고 화면보다 숫자가 낫다).
                    IReadOnlyList<VillagerRecord> roster = Chronicle.RosterByBirth();
                    // 기록 마감 (M14-W4) — 저장 지점 ② (마지막 생존일·최대 인구 확정, ⚠️W4-③).
                    bool newRecord = RunRecordStore.SaveIfBetter(WintersSurvived, (int)GameTime, PeakPopulation, RepelCount);
                    // 연대기 마감 (M15-W2) — 쓰기 지점 ② (ADR-M15-2). 겨울 0번 판도 여기서 처음 남는다.
                    _archiveRunIndex = ChronicleArchive.SaveRun(_archiveRunIndex, SnapshotCurrentRun(ended: true));
                    RunRecordStore.RunRecord best = RunRecordStore.Load();
                    Hud?.ShowGameOver(roster.Count > 0
                        ? SeasonHud.ComposeGameOver((int)GameTime, SettleCount, roster,
                                                    WintersSurvived, PeakPopulation, best, newRecord,
                                                    RepelCount) // 격퇴 줄 (M21-W9)
                        : SeasonHud.ComposeGameOver((int)GameTime, DeathCount, 0, SettleCount)); // 이탈 축 휴면 — 0 (항목 자동 감춤)
                    Debug.Log($"[M0Sim] 전멸 — Day {(int)GameTime} (사망 {DeathCount} · 정착 {SettleCount} · " +
                              $"겨울 {WintersSurvived}번 · 최대 {PeakPopulation}명{(newRecord ? " · 역대 최고 갱신" : "")})");
                }

                // 계절 줄 (M19-W4: 재정 인자 9종은 화폐와 함께 철거 — 계절·예보만 남는다)
                Hud?.Tick(GameTime, Season, _worldConfig.ForecastDays);

                // 상태 알림 줄 (M13-B, 2026-07-30 개정 — 舊 M11-D 마을 최솟값 요약을 개인 열거로).
                // 관측 대상은 마을 평균이 아니라 낙오자 — 그 정신의 완성형은 "낙오자의 이름"이다.
                // "누구인지 모르는 정보"는 개입을 못 만든다 (사용자 Play 피드백). N명이면 N줄.
                // 🔴 2026-08-07: 판정 기준을 **저장 식량 → 실제 굶주림(포만)** 으로 옮겼다.
                // 舊 기준(EstimateMyFoodDays ≤ FOOD_ALERT_DAYS)은 비축이 0인 Day 0에 전원 참이라
                // 시작 화면이 빨간 줄 8개였다 — 항상 켜진 경보는 개입 신호가 아니라 배경이다.
                // 식량 일수는 판정에서 빠지고 표시용 참고 수치로만 따라간다.
                _starvingBuf.Clear();
                for (int i = 0; i < _agents.Count; i++)
                {
                    VillagerAgent a = _agents[i];
                    if (a == null || a.State == AgentState.Dead) continue;
                    VillagerAgent.HungerLevel lv = a.MyHunger;
                    if (lv == VillagerAgent.HungerLevel.None) continue;
                    _starvingBuf.Add((a.ShortName, Mathf.RoundToInt(a.Satiety), a.EstimateMyFoodDays(),
                                      lv == VillagerAgent.HungerLevel.Starving));
                }
                _starvingBuf.Sort(ByHungerUrgency); // 급한 순 — 순서가 곧 분류(triage)

                int threatDaysLeft = -1;
                string threatName = null;
                if (Threats != null && Threats.Forecasting != null)
                {
                    threatDaysLeft = Mathf.CeilToInt(Threats.DaysToStrike(GameTime));
                    threatName = Threats.Forecasting.DisplayName;
                }

                // 겨울 경보 (M14-W4) — "누가 겨울을 못 넘기는가"의 예방 열거. 봉쇄 중(0)·창 밖은
                // 침묵 (경보는 예방 전용 — 심판 중엔 굶는 주민 줄이 맡는다). 문턱 = 다음 봉쇄 계절의
                // 실제 길이 (에셋에서 읽는다 — 상수 4 금지, 명세 W4).
                int freezeDaysLeft = -1;
                _unpreparedBuf.Clear();
                if (Season != null && Season.NextFreeze != null
                    && Season.DaysToFreeze > 0f && Season.DaysToFreeze <= SeasonHud.WINTER_ALERT_DAYS)
                {
                    freezeDaysLeft = Mathf.CeilToInt(Season.DaysToFreeze);
                    int winterLen = Mathf.CeilToInt(Season.NextFreeze.DurationDays);
                    for (int i = 0; i < _agents.Count; i++)
                    {
                        VillagerAgent a = _agents[i];
                        if (a == null || a.State == AgentState.Dead) continue;
                        int d = a.EstimateMyFoodDays();
                        if (d < winterLen) _unpreparedBuf.Add((a.ShortName, d));
                    }
                    _unpreparedBuf.Sort(ByFoodUrgency);
                }
                Hud?.TickStatus(SeasonHud.ComposeStatus(_starvingBuf, CountUntendedInjured(),
                                                        threatDaysLeft, threatName,
                                                        freezeDaysLeft, _unpreparedBuf));

                // 적습 상시 프롬프트 (M21-W8, ADR-M21-7 — 개입 시점은 화면이 알려준다).
                // 방랑자 제안이 떠 있으면 양보한다 (프롬프트 슬롯은 하나 — 결정 요구가 관측보다
                // 급하다). 제안이 해소되면 다음 틱에 자동 복원 (_raidPrompt 비교가 재설정을 연다).
                bool wandererPending = Wanderers != null && Wanderers.HasPendingOffer;
                ThreatSO raidSo = null;
                int raidCount = 0;
                bool raidActive = Threats != null && Threats.TryGetActiveRaid(out raidSo, out raidCount);
                if (raidActive && !wandererPending)
                {
                    string p = $"⚔ {raidSo.DisplayName}{(raidCount > 1 ? $" {raidCount}마리" : "")} — " +
                               "마을 습격 중 (주민 선택 후 F = 맞서라)";
                    if (p != _raidPrompt)
                    {
                        _raidPrompt = p;
                        Hud?.SetPrompt(p);
                    }
                }
                else if (_raidPrompt != null)
                {
                    _raidPrompt = null;
                    if (!wandererPending) Hud?.ClearPrompt(); // 방랑자가 슬롯을 차지했으면 놔둔다
                }

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
                    // (M19-W4: 하루 경계의 물가 확정·회계 검산·세수 결산·부채 감쇠는 화폐와
                    //  함께 철거 — ADR-M19-1. 이력은 git: M16-W4/W6 · M17-W1/W2/W3/W5)
                    string seasonStr = Season?.Current != null
                        ? $"{Season.Current.DisplayName}(위기까지 {Mathf.CeilToInt(Season.DaysToCrisis)}일)" : "-";
                    // 위협 경주 게이지 (M10R 관측용) — 마을 크기(정보)와 게임일 래칫 활성밴드를 매일 노출.
                    // 활성밴드는 게임일 기준이라 사망해도 강등되지 않는다 (ADR-M10R-1 실측 지표).
                    string threatStr = "-";
                    if (Threats != null)
                    {
                        int alive = 0;
                        foreach (VillagerAgent a in _agents)
                            if (a != null && a.State != AgentState.Dead) alive++;
                        int farms = World.GetStock(SlotId.FarmPlotCount);
                        int houses = World.GetStock(SlotId.HouseCount);
                        int scale = ThreatService.VillageScale(alive, farms, houses); // 정보용 — 게이팅 아님
                        ThreatSO tier = ThreatService.PickTier(_worldConfig.Threats, GameTime); // 시간 래칫
                        threatStr = $"마을{scale}(주민{alive}+밭{farms}+집{houses}) 활성위협={(tier != null ? tier.DisplayName : "없음")}(Day{day})";
                    }
                    // 성격별 행동 프로파일 (M12-J) — 하루 경계에 얹되 자체 주기로 스스로 솎는다.
                    Profiler.Tick(GameTime, _worldConfig.ProfilerIntervalDays, _agents);
                    Debug.Log($"[M0Sim] Day {day} [{seasonStr}] — Wood {World.GetStock(SlotId.WoodStock)}, " +
                              $"Stone {World.GetStock(SlotId.StoneStock)}, " +
                              $"RawFood {World.GetStock(SlotId.RawFoodStock)}, " +
                              $"Cooked {World.GetStock(SlotId.CookedFoodStock)}, " +
                              $"Farm {World.GetStock(SlotId.FarmPlotCount)}, " +
                              $"발견 W/S/F={Discovery.HasDiscovered(ResourceType.Wood)}/" +
                              $"{Discovery.HasDiscovered(ResourceType.Stone)}/" +
                              $"{Discovery.HasDiscovered(ResourceType.RawFood)} — {threatStr}");
                }
            }
        }

        /// <summary>
        /// 겨울 진입 대비 점검 로그 (M13 탐지기 — 2026-07-24 Day 20 전멸 진단용). "왜 다 죽었나"를
        /// 다음 관측 1회로 판정하기 위해, 봉쇄 계절이 시작되는 순간 주민별 비축을 남긴다.
        /// 집·모닥불이 없으면 몸 소지 상한(BodyCarryCap)이 곧 한계라 겨울을 물리적으로 못 난다 —
        /// 그 판정을 로그만 보고 내릴 수 있어야 한다. 값은 전부 공개 스냅샷에서 읽는다(판정 이원화 금지).
        /// </summary>
        private void LogWinterReadiness(SeasonSO s)
        {
            float need = s.DurationDays * _agentConfig.SatietyDecayPerGameDay * s.SatietyDecayMult;
            Debug.Log($"[M0Sim] 겨울 대비 점검 — {s.DisplayName} {s.DurationDays:0.#}일 · " +
                      $"1인 수요 ≈ {need:F0} 포만 (생식 {Mathf.CeilToInt(need / 15f)}개 상당) · " +
                      $"몸 상한 {_agentConfig.BodyCarryCap} · 집 곳간 상한 {_agentConfig.HomeStorageCap}");
            foreach (VillagerAgent a in _agents)
            {
                if (a == null || a.State == AgentState.Dead) continue;
                WorldSnapshot snap = a.BuildSnapshot();
                // 성격 병기 (M14-W4) — 성공 기준 2("성격별 대비 격차")의 유일한 탐지기.
                Debug.Log($"[M0Sim]   {a.AgentId}" +
                          $"({(a.Personality != null ? a.Personality.DisplayName : "중립")})" +
                          $": 집={(snap.Get(SlotId.MyHasHome) == 1 ? "O" : "X")} " +
                          $"모닥불={(snap.Get(SlotId.MyHasCampfire) == 1 ? "O" : "X")} · " +
                          $"몸 생식{snap.Get(SlotId.MyRawFood)}/조리{snap.Get(SlotId.MyCookedFood)} · " +
                          $"집 생식{snap.Get(SlotId.MyHomeRawFood)}/조리{snap.Get(SlotId.MyHomeCookedFood)} · " +
                          $"식량일수={snap.Get(SlotId.MyFoodDaysLeft)}"); // (M19-W4: 지갑 병기 철거)
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
