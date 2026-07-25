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
            if (agent != null && !_agents.Contains(agent))
            {
                _agents.Add(agent);
                _everHadAgents = true; // 전멸 판정용 — 시작 전 빈 목록과 전멸을 구분 (M10-F)
            }
        }

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

        /// <summary>누적 이탈 수 (M10-F) — 쓰기는 RecordDepart뿐. 세이브 대상 (ADR-M10-10).</summary>
        public int DepartCount { get; private set; }

        /// <summary>이탈 기록 — 호출처는 VillagerAgent.Depart()뿐. 사망과 기록도 이원화 (ADR-M10-3).</summary>
        public void RecordDepart() => DepartCount++;

        // 전멸 종료 (M10-F) — 화면만 덮고 틱은 계속 돈다 (관찰 샌드박스 유지, 재건은 M11).
        private bool _everHadAgents;
        private bool _gameOverShown;

        /// <summary>전멸 래치 판정 (순수 — 게이트 M10-T6): 주민이 있었던 마을이 0명이 된 첫 순간만.</summary>
        public static bool ShouldShowGameOver(bool alreadyShown, bool everHadAgents, int aliveCount)
            => !alreadyShown && everHadAgents && aliveCount == 0;

        /// <summary>
        /// 사망 기록 (M10-A) — 호출처는 VillagerAgent.Die()뿐 (이탈 이원화 — ADR-M10-3:
        /// Depart는 여기 오지 않는다). 카운터 +1 + 사망 타일에 무덤 오브젝트 (표현 전용, 영구 흔적).
        /// </summary>
        public void RecordDeath(int tileX, int tileY)
        {
            DeathCount++;
            var grave = new GameObject($"Grave_{tileX}_{tileY}");
            grave.transform.SetParent(transform, worldPositionStays: false);
            grave.transform.position = new Vector3(tileX, tileY, 0f); // ADR-M0-9 — X-Y 평면
            var sr = grave.AddComponent<SpriteRenderer>();
            sr.sprite = M0Sprites.Circle;
            sr.color = new Color(0.45f, 0.45f, 0.5f, 0.9f); // 회색 비석 마커 (아트 교체는 후속 에셋)
            sr.sortingOrder = 5;                             // 주민(10) 아래, 바닥 위
            grave.transform.localScale = Vector3.one * 0.5f;
        }

        public void UnregisterAgent(VillagerAgent agent)
        {
            if (agent == null) return;
            _agents.Remove(agent);
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
            var season = new SeasonService(_worldConfig.SeasonCycle);
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
                                              Chatter); // M8-D — 부탁 선반 (보상은 개인 잔고, M11-H)
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
            // 밭 완공 → FarmService 등록 (RegisterPlot의 유일한 호출 경로, ADR-M2-4)
            Construction.OnCompleted += (b, x, y, builderId) =>
            {
                if (b.IsCountable && b.CountSlot == SlotId.FarmPlotCount)
                    Farm.RegisterPlot(x, y, builderId); // 소유자 = 지은 사람 (M11-E)
            };
            _farmView = new FarmPlotView(transform, _cropSprites, Farm); // M2-D 성장 표현 (이벤트 구독)
            // 통행 차단 건물 → Walkable 갱신의 유일한 지점 (ADR-M3-3) — JPS는 이 배열만 보고 우회한다
            Construction.OnCompleted += (b, x, y, _) =>
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

            // 야생 위협 (M10-C) — Threats가 비면 서비스 null (중립 불변식, DisasterService 패턴).
            // 부상·파괴는 문(Injure/RemoveCountableAt)을 지난다 — 서비스가 상태를 직접 쓰지 않는다.
            if (_worldConfig.Threats != null && _worldConfig.Threats.Length > 0)
            {
                Threats = new ThreatService(_worldConfig.Threats, World, Zones, Construction,
                                            _agents, _worldConfig, () => Pathfinder, transform);
                Threats.OnForecast += t =>
                    Hud?.Notify($"{t.DisplayName}이(가) 다가옵니다 — {t.WarnDays:0.#}일 뒤");
                Threats.OnStruck += (t, n, tile) =>
                {
                    Hud?.Notify(t.TargetVillagers ? $"{t.DisplayName} 습격 — 부상 {n}명"
                                                  : $"{t.DisplayName} 습격 — 밭 {n}개 소실");
                    ShowThreatStrikeLines(t, tile);
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

        /// <summary>위협 타격 반응 대사 (M10-C, 표현 전용) — 타격 지점 최근접 생존 주민 최대 2명이
        /// StrikeLines를 내뱉는다 (재해 ShowStrikeLines 패턴 — 릴레이 아님, 대사만 Random 허용).</summary>
        private void ShowThreatStrikeLines(ThreatSO t, Vector2Int tile)
        {
            if (t.StrikeLines == null || t.StrikeLines.Length == 0 || _agents.Count == 0) return;
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
            a1?.ShowTransient(t.StrikeLines[Random.Range(0, t.StrikeLines.Length)]);
            a2?.ShowTransient(t.StrikeLines[Random.Range(0, t.StrikeLines.Length)]);
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
                                HomeStorage); // 집 저장 표기 (M11-A)

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
                    Hud?.ShowGameOver(SeasonHud.ComposeGameOver((int)GameTime, DeathCount, DepartCount, SettleCount));
                    Debug.Log($"[M0Sim] 전멸 — Day {(int)GameTime} (사망 {DeathCount} · 이탈 {DepartCount} · 정착 {SettleCount})");
                }

                // HUD 식량 일수 = 전 주민 최솟값 (M11-D) — 관측 대상은 마을 평균이 아니라 낙오자다
                int minFoodDays = WorldModel.NO_ESTIMATE;
                for (int i = 0; i < _agents.Count; i++)
                {
                    VillagerAgent a = _agents[i];
                    if (a == null || a.State == AgentState.Dead) continue;
                    int d = a.EstimateMyFoodDays();
                    if (d < minFoodDays) minFoodDays = d;
                }
                Hud?.Tick(GameTime, Season, _worldConfig.ForecastDays, minFoodDays);

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
                    // 위협 경주 게이지 (M10 관측용) — 규모 산식·활성 티어를 매일 노출.
                    // "티어2가 왜 안 오나"류 진단이 로그 한 줄로 끝나야 한다 (2026-07-22 관측 사이클 교훈).
                    string threatStr = "-";
                    if (Threats != null)
                    {
                        int alive = 0;
                        foreach (VillagerAgent a in _agents)
                            if (a != null && a.State != AgentState.Dead) alive++;
                        int farms = World.GetStock(SlotId.FarmPlotCount);
                        int houses = World.GetStock(SlotId.HouseCount);
                        int scale = ThreatService.VillageScale(alive, farms, houses);
                        ThreatSO tier = ThreatService.PickTier(_worldConfig.Threats, scale);
                        threatStr = $"규모 {scale}(주민{alive}+밭{farms}+집{houses}) 티어={(tier != null ? tier.DisplayName : "없음")}";
                    }
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

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
