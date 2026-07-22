using System.Collections.Generic;
using AIVillage.AI;
using AIVillage.Core;
using AIVillage.Core.GOAP;
using UnityEngine;

namespace AIVillage.M0
{
    public enum AgentState
    {
        Idle,     // goal 평가 대기
        Planning, // 플래너 잡 폴링
        Moving,   // 타일 예약 이동 (Update 프레임 보간)
        Acting,   // 러너 실행
        Dead,     // 시뮬 종료 상태 — 이탈(M6-D)·사망(M10-A)이 공유 (ADR-M6-3: 새 상태 추가 금지)
    }

    /// <summary>부상 심각도 (M10-A). append 예약: Heavy = 2 (M11+ 침상 고정·간병 배달 — 명세 §7).</summary>
    public enum InjurySeverity
    {
        None  = 0,
        Light = 1, // 경상 — 거동 가능 (감속 + 노동 goal 차단), 간호로 회복
    }

    /// <summary>
    /// M0 주민 에이전트 — 舊 VillagerFSM(3,476줄)의 대체. 5상태 고정 (성공 기준 S5: ≤600줄).
    ///
    /// 원칙:
    ///   - 액션 이름 분기 없음: 실행은 IActionRunner 다형 디스패치뿐 (ADR-M0-1)
    ///   - 효과 적용은 EffectApplier 단일 해석기 (BuildRunner만 ConstructionService 경유)
    ///   - 이동 실패 first-class: Unreachable/PathBlocked → 좌표 스냅 없이 AbortPlan (舊 ADR-8/9 계승)
    ///   - 타일 두 셀 소유 (현재+다음), 실패·사망 시 ReleaseAllBy (舊 ADR-T3~T6 계승)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VillagerAgent : MonoBehaviour
    {
        // ── 알고리즘 상수 (게임 수치는 전부 AgentConfigSO에) ──────────────────
        private const float NEXT_TILE_WAIT_MAX_SEC = 0.5f;  // 舊 TR-3 WAIT_MAX_TICKS(5×0.1s) 계승
        private const int   GOAL_RECHECK_EVERY_TICKS = 5;   // 실행 중 상위 goal 전환 검사 간격 (0.5초)
        private const float ARRIVE_EPSILON = 0.001f;

        public AgentState State { get; private set; } = AgentState.Idle;
        public string AgentId { get; private set; }

        /// <summary>표시용 짧은 이름 (M7-A) — "M0_Villager_A" → "A". 이름표·정보줄이 쓴다.</summary>
        public string ShortName { get; private set; }
        public int TileX { get; private set; }
        public int TileY { get; private set; }
        public float Satiety { get; private set; }
        public float Fatigue { get; private set; }

        /// <summary>현재 플랜 (읽기 전용 — W6 말풍선용). _planIndex 이전 항목은 완료분.</summary>
        public IReadOnlyList<ActionSO> CurrentPlan => _plan;
        public int CurrentPlanIndex => _planIndex;
        public GoalSO CurrentGoal => _goal;

        private M0SimulationLoop _sim;
        private AgentConfigSO _cfg;

        public WorldModel World => _sim.World;
        public DiscoveryService Discovery => _sim.Discovery;
        public ConstructionService Construction => _sim.Construction;
        public ZoneService Zones => _sim.Zones; // 구역 배치 결정자 (M9-A)
        public FarmService Farm => _sim.Farm;
        public HomeStorageService HomeStorage => _sim.HomeStorage; // 집 저장 (M11-A — EffectApplier 창구)
        public WorldConfigSO WorldConfig => _sim.WorldConfig;
        public AgentConfigSO AgentConfig => _cfg; // 러너용 읽기 창구 (M10-B — TendLines 등 대사 에셋)

        /// <summary>타일 통행 가능 여부 — 러너의 랜덤 목표 필터용 (M4-E). 맵 밖은 false.</summary>
        public bool IsWalkable(int x, int y)
            => MapBounds.ToArrayIndex(x, y, out int ax, out int ay) && _sim.Walkable[ax, ay];

        /// <summary>경로 실패 진단 전용 (2026-07-16) — 통행불가 타일 개수+좌표(최대 12개) 덤프.</summary>
        private string DumpBlockedTiles(int max = 12)
        {
            var sb = new System.Text.StringBuilder();
            int n = 0;
            MapBounds.Get(out int minX, out int maxX, out int minY, out int maxY);
            for (int x = minX; x <= maxX; x++)
                for (int y = minY; y <= maxY; y++)
                    if (!IsWalkable(x, y))
                    {
                        n++;
                        if (n <= max) sb.Append($"({x},{y})");
                    }
            return $"{n}개 {sb}";
        }

        // ── 런타임 스폰 주입 (M10-E) — Start 전에 Preset()으로 도착. 미주입 = 기존 랜덤 (중립) ──
        private PersonalitySO _presetPersonality;
        private JobSO _presetJob;
        private bool _hasPreset;

        /// <summary>
        /// 성격·직업 사전 주입 (SpawnVillager 전용, M10-E) — AddComponent 직후·Start 전에 호출.
        /// null도 유효한 주입이다 (중립 성격·무직 방랑자) — 판정은 _hasPreset 플래그.
        /// </summary>
        public void Preset(PersonalitySO p, JobSO j)
        {
            _presetPersonality = p;
            _presetJob = j;
            _hasPreset = true;
        }

        /// <summary>주입 우선 선택 (순수 — 게이트 M10-T5): 주입 시 그 값(null 포함), 아니면 랜덤 경로.</summary>
        public static T PresetOrRandom<T>(bool hasPreset, T preset, System.Func<T> random) where T : class
            => hasPreset ? preset : random();

        /// <summary>성격 아키타입 (M4-A). null = 중립 — M3와 동작 동일 (ADR-M4-2).</summary>
        public PersonalitySO Personality { get; private set; }

        /// <summary>직업 (M5-A). null = 무직 — M4와 goal 선택 동일 (M5-S3). 세이브 대상 (ADR-M5-5).</summary>
        public JobSO Job { get; private set; }

        // goal 실효 우선순위 보정 (M5-B 직업 + M6 후속 성격 합산, ADR-M5-1) — 스폰 1회 캐시.
        // 둘 다 null이면 null = Select가 기존과 완전 동일 경로 (중립 불변식).
        private System.Func<GoalSO, int> _goalBias;

        /// <summary>
        /// 직업+성격 goal 보정 결합의 유일한 지점 (순수 — 게이트 M6-T5). 둘 다 null = null (중립).
        /// 성격 보정이 위기 대응을 가른다: 같은 트리거를 봐도 실효 우선순위가 달라
        /// 돌입 시점·참여 여부가 성격따라 흩어진다 (2026-07-17 "동시 돌입 = AI티" 대응).
        /// </summary>
        public static System.Func<GoalSO, int> BuildGoalBias(JobSO job, PersonalitySO personality)
        {
            if (job == null && personality == null) return null;
            if (personality == null) return job.BoostFor;
            if (job == null) return personality.BoostFor;
            return g => job.BoostFor(g) + personality.BoostFor(g);
        }

        // 직업 일과 goal (M5-C, ADR-M5-2) — 개인 사다리 주입, 씬 _goals에 넣지 않는다.
        // 무직·일과 없는 직업이면 null = 기존 여가 동작.
        private GoalSO _routine;

        /// <summary>실효 우선순위 = 에셋 Priority + 직업 보정. 선택(Select)과 전환 비교가
        /// 같은 진리를 쓰기 위한 유일한 계산 지점 (ADR-M5-6).</summary>
        private int EffectivePriority(GoalSO g)
            => g.Priority + (_goalBias != null ? _goalBias(g) : 0);

        /// <summary>배율 개체 편차 [채집, 농사, 건설, 탐험] — 스폰 1회 고정, M4-B 비용 배열 계산에 사용.</summary>
        public float[] MultJitter => _multJitter;
        private float[] _multJitter;

        // 포만 감쇠율 개체 편차 (2026-07-17 웨이브 수정) — FNV-1a 결정적, 세이브 불필요 (재계산)
        private float _decayJitter = 1f;
        private float[] _costMult; // 카탈로그 인덱스별 성격 비용 배율 (스폰 1회 계산, null=중립)

        // ── 플랜 상태 ──────────────────────────────────────────────────────
        private readonly List<ActionSO> _plan = new List<ActionSO>(PlanningConfig.MaxPlanLen);
        private int _planIndex;
        private GoalSO _goal;
        // 상대 씬 goal(M9-H, ADR-M9-12)의 플래너 입력 전용 사본. _goal에는 원본을 유지한다 —
        // Claim/IsFull/_goalRetryAt이 _goal을 키로 쓰므로 사본을 넣으면 MaxWorkers 정원이 무력화된다.
        // 수명 = 플랜 1회 (ToIdle·OnDestroy에서 Destroy).
        private GoalSO _planGoalClone;
        private bool _directGoal; // DirectActionPool goal 여부 (완료 로그 억제용)
        private PlannerGateway.PendingPlan _pending;
        private IActionRunner _runner;
        // 실패 goal 재시도 쿨다운 — 공회전(실패→즉시 재선택) 방지, 그동안 하위 goal로
        private readonly Dictionary<GoalSO, float> _goalRetryAt = new Dictionary<GoalSO, float>();

        // ── 촌장 명령 (M1-C, ADR-M1-1: 상태머신이 아니라 사다리의 한 칸) ──
        private GoalSO _order;
        private bool _orderIsRuntimeClone; // 상대 목표 해석용 사본 여부 (수명 관리)
        private float _transientTextUntil;

        public GoalSO CurrentOrder => _order;

        /// <summary>촌장이 지목한 노드 ("저거 캐와"의 '저거'). GatherRunner가 최우선 대상으로 삼는다.</summary>
        public ResourceNode OrderTargetNode { get; private set; }

        /// <summary>보상 약속 (M6-E — 에스크로 차감 완료 상태). 세이브 대상 (ADR-M4-5 목록 추가 예정).</summary>
        public RewardSO PromisedReward => _promisedReward;
        private RewardSO _promisedReward;

        /// <summary>
        /// 명령 슬롯 정리 — 런타임 사본이면 파괴 (누수 방지). 소멸의 유일한 경로.
        /// 보상 약속의 소멸도 반드시 여기를 지난다 (ADR-M6-5): 미지급 약속은 에스크로 반환 —
        /// 완수 경로만 PayReward()가 먼저 지급·null 처리 후 진입한다 (반환과 배타).
        /// </summary>
        private void ClearOrderInstance()
        {
            if (_promisedReward != null)
            {
                if (_sim != null) World.AddStock(_promisedReward.CostSlot, _promisedReward.CostAmount);
                _promisedReward = null;
            }
            if (_order != null && _orderIsRuntimeClone) Destroy(_order);
            _order = null;
            _orderIsRuntimeClone = false;
            OrderTargetNode = null;
        }

        /// <summary>보상 지급 — 명령 완수 지점 전용 (ADR-M6-5). 지급 후 null 처리로 반환 경로 차단.</summary>
        private void PayReward()
        {
            if (_promisedReward == null) return;
            ApplyNeedEffect(SlotId.MySatiety, EffectOp.Add, _promisedReward.SatietyGain);
            ShowTransient(Pick(_promisedReward.PayLines));
            Debug.Log($"[VillagerAgent] {AgentId}: 보상 지급 — {_promisedReward.DisplayName} " +
                      $"(포만 +{_promisedReward.SatietyGain}, 비용은 수락 시 차감 완료)");
            _promisedReward = null;
        }
        private float _idleCooldownSec;
        private int _tickCounter;
        private readonly List<SlotEffect> _effectBuf = new List<SlotEffect>(8);

        // ── 이동 상태 ──────────────────────────────────────────────────────
        private List<Vector2Int> _waypoints;
        private int _wpIndex;
        private Vector2Int _fromTile, _nextTile;
        private bool _hasNextReserved;
        private float _blockedWaitSec;

        // ── 표현 (W5/W6) ──────────────────────────────────────────────────
        private MoveMotion _motion;
        private AgentAnimator _animator;
        private PlanBubble _bubble;
        private NameTag _nameTag;
        private Vector2 _lastDir = Vector2.down;

        // ─────────────────────────────────────────────────────────────────────
        // 라이프사이클
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            AgentId = name;
            int sep = AgentId.LastIndexOf('_');
            ShortName = sep >= 0 && sep < AgentId.Length - 1 ? AgentId.Substring(sep + 1) : AgentId;
            // 씬 배치 위치 → 논리 타일 정합 (舊 Awake sync fix 계승 — Brain=(0,0) 몰림 방지)
            TileX = Mathf.RoundToInt(transform.position.x);
            TileY = Mathf.RoundToInt(transform.position.y);
        }

        private void Start()
        {
            _sim = M0SimulationLoop.Instance;
            if (_sim == null || !_sim.enabled)
            {
                Debug.LogError($"[VillagerAgent] {AgentId}: M0SimulationLoop가 없습니다. 비활성화.");
                enabled = false;
                return;
            }
            _cfg = _sim.AgentConfig;
            // 개인 편차: 전원 동일 초기값 → 동시 배고픔 웨이브 방지 (FNV-1a 결정적 —
            // GetHashCode()%1000은 꼬리 1글자 차이 이름에서 붕괴, 2026-07-17 근본 수정)
            float spread = StableHash.Spread(AgentId, "satiety"); // [-1, 1]
            Satiety = Mathf.Clamp(_cfg.InitialSatiety + spread * _cfg.InitialSatietyVariance, 0f, 100f);
            Fatigue = _cfg.InitialFatigue;
            // 감쇠율 개체 편차 — 동기화(포만 0 클램프·같은 문턱 식사)가 생겨도 되돌리는 상시 힘.
            // 편차가 없으면 감쇠율이 전원 동일이라 한 번 뭉친 웨이브가 영구 지속된다.
            _decayJitter = 1f + StableHash.Spread(AgentId, "decay") * _cfg.SatietyDecayVariancePct;

            // 성격 할당 (M4-A) — 스폰 1회 고정. 배율 편차 ±10%도 이때 확정 (정체성 — 세이브 대상, ADR-M4-5)
            // 런타임 스폰(M10-E)은 Preset 주입값이 랜덤을 대체 — UI가 보여준 그 사람이 온다 (⚠️①).
            Personality = PresetOrRandom(_hasPreset, _presetPersonality, _sim.PickRandomPersonality);
            // 직업 할당 (M5-A) — 성격과 별개 축, 스폰 1회 고정 (세이브 대상, ADR-M5-5)
            Job = PresetOrRandom(_hasPreset, _presetJob, _sim.PickRandomJob);
            _multJitter = new[]
            {
                Random.Range(0.9f, 1.1f), Random.Range(0.9f, 1.1f),
                Random.Range(0.9f, 1.1f), Random.Range(0.9f, 1.1f),
            };
            Debug.Log($"[VillagerAgent] {AgentId}: 성격 = {(Personality != null ? Personality.DisplayName : "없음(중립)")}"
                      + $" / 직업 = {(Job != null ? Job.DisplayName : "무직(공용)")}");
            _goalBias = BuildGoalBias(Job, Personality); // 직업+성격 합산 (M6 후속, ADR-M5-6 동일 인자 유지)
            _routine = Job != null ? Job.RoutineGoal : null;
            // 배율 배열 1회 캐시 (M4-B) — 성격·직업 둘 다 null이면 null = 중립 (RequestPlan이 무시)
            _costMult = PersonalityCost.Build(_sim.Catalog, Personality, Job, _multJitter);
            _motion = new MoveMotion(_cfg, AgentId);
            SetupView();
            SetupBubble();

            // 두 셀 소유 규칙의 '현재' 셀 확보
            if (!TileReservationRegistry.TryReserve(new Vector2Int(TileX, TileY), AgentId))
                Debug.LogWarning($"[VillagerAgent] {AgentId}: 시작 타일 ({TileX},{TileY}) 예약 실패 — 겹침 배치 의심.");

            _sim.RegisterAgent(this);
            RevealAndDiscoverAt(TileX, TileY);
        }

        private void OnDestroy()
        {
            if (_goal != null && _sim != null) _sim.Goals.Release(_goal); // 파괴 시 클레임 잠김 방지
            ClearOrderInstance();
            ClearRequestInstance(); // 부탁 사본 정리 (M8-D) — 진행 기록은 UnregisterAgent가 정리
            ClearPlanGoalClone();   // 상대 씬 goal 플래너 사본 정리 (M9-H)
            _runner?.Cleanup(this);
            if (_pending != null && _sim != null) _sim.Planner.Cancel(_pending);
            TileReservationRegistry.ReleaseAllBy(AgentId);
            if (_sim != null) _sim.UnregisterAgent(this);
        }

        private void Update()
        {
            // 대화 중 정지·마주보기 (M7 후속) — 이동 보간과 걷기 애니메이션만 잠근다
            bool chatting = Time.time < _chatPauseUntil;
            if (chatting) _lastDir = _chatFaceDir;

            if (State == AgentState.Moving && !chatting) TickMoving(Time.deltaTime);
            _animator?.Tick(Time.deltaTime,
                State == AgentState.Moving && _hasNextReserved && !chatting, _lastDir);

            // 임시 문구(거부 대사) 만료 처리 — 실행 중이면 침묵 여운 뒤 플랜 복원
            // (2026-07-18 사용자 지시: 대사 직후 '다음 행동' 노란 문구가 바로 튀지 않게)
            if (_transientTextUntil > 0f && Time.time >= _transientTextUntil)
            {
                _transientTextUntil = 0f;
                if (State == AgentState.Idle) _bubble?.Clear();
                else if (_cfg.PlanResumeDelaySec > 0f)
                {
                    _bubble?.Clear();
                    _planResumeAt = Time.time + _cfg.PlanResumeDelaySec;
                }
                else _bubble?.ShowPlan(_plan, _planIndex, OrderPrefix()); // 0 = 기존 즉시 복원
            }

            // 예약된 플랜 복원 — 그 사이 새 대사가 시작됐으면 양보 (그 대사의 만료가 다시 예약한다)
            if (_planResumeAt > 0f && Time.time >= _planResumeAt)
            {
                _planResumeAt = 0f;
                if (!BubbleShowingLine && State != AgentState.Idle)
                    _bubble?.ShowPlan(_plan, _planIndex, OrderPrefix());
            }

            // 지연 응수 발화 (M7-C) — 표현 전용, 걸음은 멈추지 않는다
            if (_delayedShowAt > 0f && Time.time >= _delayedShowAt)
            {
                _delayedShowAt = 0f;
                ShowTransient(_delayedLine, _delayedHold);
                _delayedLine = null;
                _delayedHold = 0f;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 시뮬 틱 (M0SimulationLoop가 0.1초마다 호출)
        // ─────────────────────────────────────────────────────────────────────

        public void SimTick(float dtSec, float deltaGameDays)
        {
            if (State == AgentState.Dead) return;

            // 겨울은 더 빨리 배고프다 (M6-B) — 계절 없으면 배율 1 (중립). 개체 편차 곱 포함.
            float decayMult = (_sim.Season != null ? _sim.Season.SatietyDecayMult : 1f) * _decayJitter;
            Satiety = Mathf.Max(0f, Satiety - SatietyDecay(_cfg.SatietyDecayPerGameDay, decayMult, deltaGameDays));

            // 굶주림 이탈 (M6-D) — 계절 분기 없음: 굶주림은 계절 무관 사실 (⚠️③).
            // 대기 가드 금지 (M4 교훈 — 동상·데드락) — 판정은 누적 시간 하나뿐.
            _starvingDays = NextStarvingDays(_starvingDays, Satiety, _cfg.StarvingBelowSatiety, deltaGameDays);
            if (ShouldDepart(_starvingDays, _cfg))
            {
                Depart();
                return;
            }

            // 부상 계단 (M10-A) — 굶주림과 같은 패턴, 원인·결말은 분리 (ADR-M10-3).
            // 회복은 간호 중에만 진행 (자연 회복 없음 — 결정 11), 방치 누적이 문턱에 닿으면 사망.
            if (Injury != InjurySeverity.None)
            {
                bool tended = Time.time < _tendedUntil;
                (_injuryRecovery, _injuryNeglectDays) = NextInjuryState(
                    _injuryRecovery, _injuryNeglectDays, tended, _tendMult, deltaGameDays);
                if (_injuryRecovery >= _cfg.InjuryRecoverDays) HealInjury();
                else if (ShouldDie(_injuryNeglectDays, _cfg))
                {
                    Die();
                    return;
                }
            }
            _tickCounter++;

            // 실행 중 상위 goal 전환 (데이터 주도 — 임계값은 GoalSO 에셋에만 존재)
            if ((State == AgentState.Moving || State == AgentState.Acting)
                && _tickCounter % GOAL_RECHECK_EVERY_TICKS == 0)
            {
                // 쿨다운+부상 필터를 Idle 선택과 동일하게 적용 — 방금 실패한 goal이 전환 대상으로
                // 재등장해 중단↔재시작 폭주(0.5초 주기)를 일으키는 것을 방지 (M10-A: 합성 필터)
                GoalSO now = _sim.Goals.Select(BuildSnapshot(), IsGoalExcluded, _order, _goalBias, _routine, _request);
                if (now != null && _goal != null && now != _goal
                    && EffectivePriority(now) > EffectivePriority(_goal))
                {
                    // 정상 전환은 실패가 아니다 — 쿨다운 없이 중단해야 M1-C 명령 복귀가 즉시 성립
                    AbortPlan($"상위 목표 전환: {_goal.DisplayName} → {now.DisplayName}", warn: false, cooldown: false);
                    return;
                }
            }

            switch (State)
            {
                case AgentState.Idle:     TickIdle(dtSec);   break;
                case AgentState.Planning: TickPlanning();    break;
                case AgentState.Acting:   TickActing(dtSec); break;
                // Moving은 Update() 프레임 보간
            }
        }

        /// <summary>플래닝 스냅샷 — RequestService(M8-D)의 의뢰인 조건 판정도 이걸 쓴다 (같은 언어).
        /// ThreatNear는 개인 감지 배율(성격 FleeRadiusMult)로 여기서 계산 — 같은 위협을 봐도
        /// 고집쟁이는 늦게 알아챈다 (M10-D, 결정적 부상 선정의 성격 축).
        /// 몸 소지·집 저장(M11-A)도 개인 주입 — 집 저장은 무주택이면 0 (중립).</summary>
        public WorldSnapshot BuildSnapshot()
        {
            bool hasHome = TryGetHomeTile(out Vector2Int home);
            (int homeRaw, int homeCooked) = hasHome && HomeStorage != null
                ? HomeStorage.Get(home) : (0, 0);
            return World.BuildSnapshot(Mathf.RoundToInt(Satiety), Mathf.RoundToInt(Fatigue),
                hasHome, // MyHasHome (M8-C)
                _sim.Threats != null && _sim.Threats.IsNearThreat(TileX, TileY,
                    Personality != null ? Personality.FleeRadiusMult : 1f),    // ThreatNear (M10-D)
                MyRaw, MyCooked, homeRaw, homeCooked);                         // 개인 인벤토리 (M11-A)
        }

        /// <summary>
        /// 앵커 조회 단일 창구 (M8-C) — 러너는 이 메서드만 쓴다. 슬롯 순회마다 내 소유 건물이
        /// 있으면 그 타일 우선 ("내 집" — 남의 집 최근접보다 앞선다), 없으면 기존 완공 조회.
        /// 소유 기록이 없으면 M7 동작과 완전 동일 (중립 불변식).
        /// </summary>
        public bool ResolveAnchor(SlotId[] priority, out Vector2Int tile)
        {
            if (priority != null)
                foreach (SlotId slot in priority)
                {
                    if (_sim.Ownership.TryGetOwned(AgentId, slot, out tile)) return true;
                    if (Construction.TryGetAnchorTileForSlot(slot, TileX, TileY, out tile)) return true;
                }
            tile = default;
            return false;
        }

        /// <summary>포만 감쇠 산식의 유일한 지점 (M6-B) — 순수 함수라 게이트(M6-T2b)가 직접 검증한다.</summary>
        public static float SatietyDecay(float perGameDay, float seasonMult, float deltaGameDays)
            => perGameDay * seasonMult * deltaGameDays;

        // ── 부상·사망 (M10-A — 최초의 사망 축. ADR-M10-2: 쓰기는 Injure/HealInjury/SimTick뿐) ──

        /// <summary>부상 상태. 세이브 대상 (ADR-M0-10 — Severity·회복·방치 누적 3종 함께).</summary>
        public InjurySeverity Injury { get; private set; }

        private float _injuryRecovery;    // 간호 누적 (게임일) — InjuryRecoverDays 도달 시 완치. 세이브 대상
        private float _injuryNeglectDays; // 미간호 누적 (게임일) — InjuryDeathAfterDays 도달 시 사망. 세이브 대상
        private float _tendedUntil;       // 간호 유효 시각 (Time.time) — 세이브 안 함 (로드 후 간호 재개로 재유도)
        private float _tendMult = 1f;     // 현재 간호자 회복 배율 (Job.TendRecoveryMult)

        /// <summary>부상 진입 가능 판정 (순수 — 게이트 M10-T1): Dead·중복 부상 무시 (M10은 단일 심각도).</summary>
        public static bool CanInjure(AgentState state, InjurySeverity current)
            => state != AgentState.Dead && current == InjurySeverity.None;

        /// <summary>
        /// 부상 진입의 유일한 문 (ADR-M10-2) — 호출처는 ThreatService.ExecuteStrike뿐 (M10-C).
        /// 하던 일은 중단 (실패 아님 — 쿨다운 없음), 이후 goal 후보는 AllowedWhenInjured로 좁혀진다.
        /// </summary>
        public void Injure(InjurySeverity severity)
        {
            if (!CanInjure(State, Injury) || severity == InjurySeverity.None) return;
            Injury = severity;
            _injuryRecovery = 0f;
            _injuryNeglectDays = 0f;
            _tendedUntil = 0f;
            // Planning 중 부상: 대기 중인 노동 플랜이 부상 필터를 우회해 시작되는 구멍 차단.
            // Cancel 없이 상태만 바꾸면 _pending 누수(NativeArray leak — 커밋 전 체크 4) — 반드시 취소 후 중단.
            if (State == AgentState.Planning && _pending != null)
            {
                _sim.Planner.Cancel(_pending);
                _pending = null;
                AbortPlan("부상 — 계획 취소", warn: false, cooldown: false);
            }
            else if (State == AgentState.Moving || State == AgentState.Acting)
                AbortPlan("부상 — 하던 일 중단", warn: false, cooldown: false);
            ShowTransient(Pick(_cfg.InjuredLines));
            Debug.LogWarning($"[Injury] {AgentId}: 부상 ({severity})");
            _sim.Hud?.Notify($"{ShortName}이(가) 다쳤습니다");
        }

        /// <summary>간호 표시 (M10-B TendRunner 전용) — 유효 시간 동안 사망 계단 정지 + 회복 진행.
        /// 부상 상태 자체는 쓰지 않는다 (ADR-M10-2 — 회복 진행은 본인 SimTick만).</summary>
        public void MarkTended(float untilSec, float recoveryMult)
        {
            _tendedUntil = untilSec;
            _tendMult = Mathf.Max(1f, recoveryMult);
        }

        /// <summary>간호받는 중인가 — FindNearestInjured의 미간호 우선 판정용 (M10-B).</summary>
        public bool IsTended => Time.time < _tendedUntil;

        /// <summary>최근접 부상자 조회 (TendRunner 전용) — FindVisitTarget과 같은 러너 창구 패턴.</summary>
        public VillagerAgent FindNearestInjured() => _sim.FindNearestInjured(this);

        /// <summary>
        /// 부상 계단 갱신 (순수 — 게이트 M10-T1): 간호 중 = 회복 진행·방치 정지(홀드 — 리셋 아님,
        /// 스치는 간호로 사망 시계가 초기화되면 사망 불가능 — 명세 ⚠️②), 방치 = 방치 누적·회복 정지.
        /// </summary>
        public static (float recovery, float neglect) NextInjuryState(
            float recovery, float neglect, bool tended, float tendMult, float deltaGameDays)
            => tended ? (recovery + deltaGameDays * tendMult, neglect)
                      : (recovery, neglect + deltaGameDays);

        /// <summary>사망 판정 (순수 — 게이트 M10-T1) — 문턱은 에셋 값 (ADR-M0-2). ShouldDepart와 동일 형식.</summary>
        public static bool ShouldDie(float neglectDays, AgentConfigSO cfg)
            => neglectDays >= cfg.InjuryDeathAfterDays;

        /// <summary>완치 — 부상 소멸의 유일한 지점. 감속·goal 필터가 같은 틱에 해제된다.</summary>
        private void HealInjury()
        {
            Debug.Log($"[Injury] {AgentId}: 회복 — 간호 누적 {_injuryRecovery:F2}일");
            Injury = InjurySeverity.None;
            _injuryRecovery = 0f;
            _injuryNeglectDays = 0f;
            _tendedUntil = 0f;
            _tendMult = 1f;
        }

        /// <summary>
        /// 부상 사망 (M10-A) — 이탈(Depart)과 동일 경로 재사용 (ADR-M6-3): State=Dead + 지연 파괴,
        /// 클레임·타일·명령·보상 정리는 전부 OnDestroy 단일 경로. 무덤·카운터는 RecordDeath (표현+기록).
        /// 굶주림은 여기 오지 않는다 — 결말 이원화 (ADR-M10-3: 이탈=굶주림, 사망=부상).
        /// </summary>
        private void Die()
        {
            Debug.LogWarning($"[VillagerAgent] {AgentId}: 부상 사망 — 방치 {_injuryNeglectDays:F2}일 " +
                             $"(문턱 {_cfg.InjuryDeathAfterDays}일)");
            _sim.Hud?.Notify($"{ShortName}이(가) 숨을 거뒀습니다");
            _sim.RecordDeath(TileX, TileY);
            ShowTransient(Pick(_cfg.DieLines));
            State = AgentState.Dead;      // SimTick 차단 — Depart와 동일 (새 상태 추가 금지)
            Destroy(gameObject, _cfg.TransientLineSec);
        }

        /// <summary>부상 goal 필터 (순수 — 게이트 M10-T1): 부상 중엔 AllowedWhenInjured goal만 후보.
        /// None이면 항상 false = 기존 Select와 완전 동일 (중립 불변식).</summary>
        public static bool BlockedByInjury(GoalSO goal, InjurySeverity injury)
            => injury != InjurySeverity.None && goal != null && !goal.AllowedWhenInjured;

        /// <summary>쿨다운+부상 합성 필터 — Select의 skip 델리게이트 (두 호출부가 같은 진리를 쓴다).</summary>
        private bool IsGoalExcluded(GoalSO goal)
            => IsGoalCoolingDown(goal) || BlockedByInjury(goal, Injury);

        // ── 굶주림 이탈 (M6-D — 최초의 실패 상태) ─────────────────────────────

        private float _starvingDays; // 포만 0 지속 누적 (게임일). 세이브 대상 (ADR-M4-5 목록 추가 예정)

        /// <summary>
        /// 굶주림 누적 갱신 — 문턱 위로 회복되면 리셋 (순수, 게이트 M6-T3).
        /// 문턱은 0이 아니라 에셋 값(StarvingBelowSatiety) — "전멸 아니면 무사"의 절벽 구조를
        /// "경쟁에서 밀리는 개인부터"의 계단으로 (2026-07-17 관측 대응).
        /// </summary>
        public static float NextStarvingDays(float current, float satiety, float starvingBelow,
                                             float deltaGameDays)
            => satiety < starvingBelow ? current + deltaGameDays : 0f;

        /// <summary>이탈 판정 (순수, 게이트 M6-T3) — 문턱은 에셋 값 (ADR-M0-2).</summary>
        public static bool ShouldDepart(float starvingDays, AgentConfigSO cfg)
            => starvingDays >= cfg.DepartAfterStarvingDays;

        /// <summary>
        /// 마을 이탈 — 최초의 실패 상태 (M6-D). 상태만 Dead(시뮬 종료 의미 재사용, ADR-M6-3)로
        /// 바꾸고 지연 파괴한다. 클레임·타일·플래너·명령·보상 정리는 전부 OnDestroy 단일 경로 —
        /// 여기서 직접 해제 금지 (두 번째 정리 경로가 된다).
        /// </summary>
        private void Depart()
        {
            Debug.LogWarning($"[VillagerAgent] {AgentId}: 굶주림 이탈 — 포만 {Satiety:F0} " +
                             $"(< {_cfg.StarvingBelowSatiety}) 지속 {_starvingDays:F2}일 " +
                             $"(이탈 문턱 {_cfg.DepartAfterStarvingDays}일)");
            _sim.RecordDepart(); // 기록 카운터 (M10-F) — 사망(RecordDeath)과 이원화 (ADR-M10-3)
            _sim.Hud?.Notify($"{AgentId}이(가) 마을을 떠났습니다");
            ShowTransient(Pick(_cfg.DepartLines));
            State = AgentState.Dead;      // SimTick 차단 — 새 상태 추가 금지 (ADR-M6-3)
            Destroy(gameObject, _cfg.TransientLineSec); // 마지막 대사 노출 후 소멸 (ShowTransient와 동일 에셋 값)
        }

        // ─────────────────────────────────────────────────────────────────────
        // Idle → Planning
        // ─────────────────────────────────────────────────────────────────────

        private void TickIdle(float dt)
        {
            _idleCooldownSec -= dt;
            if (_idleCooldownSec > 0f) return;

            WorldSnapshot snap = BuildSnapshot();

            // 명령 완수 판정 — 목표 충족 시 자동 소멸 (ADR-M1-1)
            if (_order != null && _order.GoalConditions != null && _order.GoalConditions.Length > 0
                && GoalSelector.AllHold(_order.GoalConditions, snap))
            {
                Debug.Log($"[VillagerAgent] {AgentId}: 명령 완수 — {_order.DisplayName}");
                PayReward(); // 약속 이행 (M6-E) — ClearOrderInstance의 반환 경로와 배타 (ADR-M6-5)
                ClearOrderInstance();
            }

            // 부탁 완수 판정 (M8-D) — 명령과 같은 패턴. 반드시 정리 후 통지 —
            // NotifyFulfilled가 보고 심부름을 새 _request로 주입하므로 (역순이면 심부름이 파괴됨)
            if (_request != null && _request.GoalConditions != null && _request.GoalConditions.Length > 0
                && GoalSelector.AllHold(_request.GoalConditions, snap))
            {
                Debug.Log($"[VillagerAgent] {AgentId}: 부탁 완수 — {_request.DisplayName}");
                ClearRequestInstance();
                _sim.Requests?.NotifyFulfilled(AgentId);
            }

            _goal = _sim.Goals.Select(snap, IsGoalExcluded, _order, _goalBias, _routine, _request);
            if (_goal == null)
            {
                _idleCooldownSec = 0.5f; // 할 일 없음 — 정상 Idle
                return;
            }
            _sim.Goals.Claim(_goal); // 착수 선언 (ADR-M3-4) — 해제는 ToIdle 단일 지점

            // M1-A DirectActionPool 특례: 여가는 플래너를 태우지 않는다 (ADR-M1-3)
            if (_goal.DirectActionPool != null && _goal.DirectActionPool.Length > 0)
            {
                _directGoal = true;
                _plan.Clear();
                ActionSO pick = _goal.DirectActionPool[Random.Range(0, _goal.DirectActionPool.Length)];
                if (pick != null) _plan.Add(pick);
                _planIndex = 0;
                StartNextAction();
                return;
            }
            _directGoal = false;

            // 상대 씬 goal(M9-H): 플래너 입력은 선택 시점 절대값 사본, _goal은 원본 유지.
            // _order·_request는 수신 시점에 이미 해석돼 RelativeToCurrent=false이므로 여기 안 걸린다.
            GoalSO planGoal = _goal;
            if (_goal.RelativeToCurrent)
            {
                planGoal = ResolveRelativeGoal(_goal, out bool cloned);
                if (cloned) _planGoalClone = planGoal; // 수명 = 이 플랜 (ToIdle에서 Destroy)
            }

            _pending = _sim.Planner.RequestPlan(snap, planGoal, _costMult);
            if (_pending == null)
            {
                ToIdle(1f); // 클레임 해제 경유 (ADR-M3-4 누수 방지) — 舊 단순 쿨다운과 동작 동일
                return;
            }
            State = AgentState.Planning;
        }

        private void TickPlanning()
        {
            if (Time.realtimeSinceStartup - _pending.RequestedAt > _cfg.PlanningTimeoutSec)
            {
                Debug.LogWarning($"[VillagerAgent] {AgentId}: 플래닝 타임아웃 (goal={_goal.name})");
                _sim.Planner.Cancel(_pending);
                _pending = null;
                ToIdle(1f);
                return;
            }

            if (!_sim.Planner.TryGetResult(_pending, out PlanStatus status, out ActionSO[] plan, out int nodes))
                return;
            _pending = null;

            switch (status)
            {
                case PlanStatus.Success:
                    _plan.Clear();
                    _plan.AddRange(plan);
                    _planIndex = 0;
                    Debug.Log($"[VillagerAgent] {AgentId}: {_goal.DisplayName} 플랜 [{Join(plan)}]");
                    StartNextAction(); // 말풍선 갱신은 StartNextAction 단일 지점에서
                    break;

                case PlanStatus.AlreadySatisfied:
                    ToIdle(0.5f);
                    break;

                default: // NoSolution — ADR-3: 버그 증상. MAX_NODES 인상 금지, 로그로 노출.
                    // 노드 수 병기 (2026-07-22 석재 goal 폭발 진단의 교훈): 4096 = 탐색 폭발
                    // (휴리스틱 수축·대부족량 goal), 그 미만 = 진짜 해 없음 (에셋 정합·발견 체인 순).
                    Debug.LogWarning($"[VillagerAgent] {AgentId}: NoSolutionFound (goal={_goal.name}, " +
                                     $"노드 {nodes}/{PlanningConfig.MaxNodes}) — ADR-3 진단 필요");
                    ToIdle(2f);
                    break;
            }
        }

        private static string Join(ActionSO[] plan)
        {
            var names = new string[plan.Length];
            for (int i = 0; i < plan.Length; i++) names[i] = plan[i].DisplayName;
            return string.Join("→", names);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 액션 시작 / 실행
        // ─────────────────────────────────────────────────────────────────────

        private string OrderPrefix() => _goal != null && _goal == _order ? _cfg.OrderBubblePrefix : null;

        /// <summary>대사 노출·응수 대기 중인가 — 이 동안 플랜 갱신이 말풍선을 덮지 않는다
        /// (2026-07-17: 액션 전환이 대화 대사를 조기에 자르던 문제. 만료 시 Update가 플랜 문구 복원).</summary>
        private bool BubbleShowingLine => Time.time < _transientTextUntil || _delayedShowAt > 0f;

        /// <summary>
        /// 대화 장면 참여 중인가 — 마주보기 정지 중이거나 응수 대기 중 (M8 후속).
        /// 이 동안 장식성 발화(혼잣말·예고 술렁임)를 억제한다 — SimTick은 대화 중에도 돌므로
        /// 새 액션 시작이 대화 말풍선을 덮던 문제 (2026-07-18 Play 피드백: 보고 대화 중 혼잣말).
        /// 거부·이탈 등 사건성 대사는 억제하지 않는다 — 장식만 침묵.
        /// </summary>
        private bool InConversation => Time.time < _chatPauseUntil || _delayedShowAt > 0f;

        private void StartNextAction()
        {
            // 갱신 단일 지점: 적재·전환·완료 모두 여기 경유. 단 대사 노출 중·침묵 여운 중엔 유예 —
            // Update의 만료·예약 복원 경로가 이어받는다 (여운을 액션 전환이 자르면 지시 무효)
            if (!BubbleShowingLine && _planResumeAt <= 0f)
                _bubble?.ShowPlan(_plan, _planIndex, OrderPrefix());

            if (_planIndex >= _plan.Count)
            {
                if (!_directGoal) Debug.Log($"[VillagerAgent] {AgentId}: {_goal.DisplayName} 플랜 완료");
                ToIdle(0f); // 여가는 로그 없이 조용히 반복 (스팸 방지)
                return;
            }

            ActionSO action = _plan[_planIndex];

            // 성격 혼잣말 (M4-D) — 표현 전용, 확률·문구 전부 에셋 값. 비면 표시 없음 (중립 경로).
            // ShowTransient가 잠시 덮고 다음 갱신에서 플랜 문구로 복귀 — 거부 대사와 같은 통로.
            // 대화 장면 중엔 침묵 (InConversation — 대화 흐름 보호, 2026-07-18)
            if (!InConversation
                && Personality != null && Personality.MoodLines != null && Personality.MoodLines.Length > 0
                && Random.value < Personality.MoodLineChance)
                ShowTransient(Pick(Personality.MoodLines));

            // 위기 예고 술렁임 (M6-C) — 혼잣말과 같은 통로. 예고 구간(위기 전)에만, 위기 중은 제외.
            // 대사·확률 전부 에셋 값 (SeasonSO.ForecastLines / AgentConfig.ForecastMoodChance).
            if (!InConversation
                && _sim.Season != null && _sim.Season.NextCrisis != null
                && _sim.Season.DaysToCrisis > 0f
                && _sim.Season.DaysToCrisis <= _sim.WorldConfig.ForecastDays
                && Random.value < _cfg.ForecastMoodChance)
                ShowTransient(Pick(_sim.Season.NextCrisis.ForecastLines));

            // 위협 예고 술렁임 (M10-D) — 계절 예고와 같은 통로·같은 확률 (표현 전용).
            // 대사·예고 기간은 전부 에셋 값 (ThreatSO.ForecastLines / WarnDays).
            if (!InConversation
                && _sim.Threats != null && _sim.Threats.Forecasting != null
                && Random.value < _cfg.ForecastMoodChance)
                ShowTransient(Pick(_sim.Threats.Forecasting.ForecastLines));

            _runner = action.CreateRunner(this);

            if (!_runner.Prepare(this))
            {
                AbortPlan(_runner.FailReason);
                return;
            }

            Vector2Int? target = _runner.MoveTarget;
            if (target == null || (target.Value.x == TileX && target.Value.y == TileY))
            {
                State = AgentState.Acting; // 제자리 실행 (舊 BC4 계승)
                return;
            }

            // 경로 탐색 창구 경유 (2026-07-18) — 알고리즘·지형표현은 IPathfinder 뒤에서 흡수.
            PathResult path = _sim.Pathfinder.FindPath(TileX, TileY, target.Value.x, target.Value.y);
            switch (path.Kind)
            {
                case PathResultKind.AlreadyThere:
                    State = AgentState.Acting;
                    break;

                case PathResultKind.PathFound:
                    _waypoints = path.Waypoints;
                    _wpIndex = 0;
                    _hasNextReserved = false;
                    _blockedWaitSec = 0f;
                    _motion.ResetPath();
                    State = AgentState.Moving;
                    break;

                default: // Unreachable — ADR-8/9: 좌표 스냅 금지, 실패를 그대로 승격
                    // 진단 강화 (2026-07-16): 출발/목표 통행 상태 + 전체 통행불가 타일 덤프 —
                    // 둘 다 True인데 실패하면 유령 벽(불가 타일 과다)이나 JPS 문제로 좁혀진다
                    AbortPlan($"경로 없음 → ({target.Value.x},{target.Value.y}) " +
                              $"[출발 ({TileX},{TileY}) 통행={IsWalkable(TileX, TileY)}, " +
                              $"목표 통행={IsWalkable(target.Value.x, target.Value.y)}, " +
                              $"통행불가={DumpBlockedTiles()}]");
                    break;
            }
        }

        private void TickActing(float dt)
        {
            RunnerResult result = _runner.Tick(this, dt);
            if (result == RunnerResult.Running) return;

            if (result == RunnerResult.Failed)
            {
                AbortPlan(_runner.FailReason);
                return;
            }

            // Succeeded — 효과 적용 (BuildRunner는 ConstructionService가 이미 반영)
            if (!_runner.AppliesOwnEffects)
            {
                _effectBuf.Clear();
                _plan[_planIndex].CollectEffects(_effectBuf);
                if (!EffectApplier.TryApply(this, World, _effectBuf))
                {
                    AbortPlan($"{_plan[_planIndex].DisplayName}: 스톡 부족으로 효과 적용 실패");
                    return;
                }
            }

            _runner.Cleanup(this);
            _runner = null;
            _planIndex++;
            StartNextAction();
        }

        // ─────────────────────────────────────────────────────────────────────
        // 이동 (타일 예약, 舊 TR-1/2 계승)
        // ─────────────────────────────────────────────────────────────────────

        private void TickMoving(float dt)
        {
            if (_waypoints == null || _wpIndex >= _waypoints.Count)
            {
                State = AgentState.Acting;
                return;
            }

            if (!_hasNextReserved)
            {
                Vector2Int next = _waypoints[_wpIndex];
                if (TileReservationRegistry.TryReserve(next, AgentId))
                {
                    _fromTile = new Vector2Int(TileX, TileY);
                    _nextTile = next;
                    _hasNextReserved = true;
                    _blockedWaitSec = 0f;
                }
                else
                {
                    _blockedWaitSec += dt;
                    if (_blockedWaitSec > NEXT_TILE_WAIT_MAX_SEC)
                        AbortPlan($"PathBlocked — ({next.x},{next.y}) 예약 대기 초과");
                    return;
                }
            }

            var targetPos = new Vector3(_nextTile.x, _nextTile.y, 0f); // ADR-4: X-Y 평면

            // W5 표현: 개체 편차 + 출발 가속 + 최종 목적지 근접 감속 (논리 도착 판정과 분리)
            bool nearDest = _wpIndex >= _waypoints.Count - 1
                            && (transform.position - targetPos).magnitude < _cfg.DecelDistance;
            // 부상 감속 (M10-A) — 속도 계산의 유일한 지점에 배율 1곱 (절뚝임. None이면 1 = 중립)
            float speed = _motion.Tick(dt, nearDest)
                          * (Injury != InjurySeverity.None ? _cfg.InjuredMoveSpeedMult : 1f);
            _lastDir = (targetPos - transform.position).normalized;
            transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * dt);

            if ((transform.position - targetPos).sqrMagnitude <= ARRIVE_EPSILON)
            {
                TileX = _nextTile.x;
                TileY = _nextTile.y;
                TileReservationRegistry.Release(_fromTile, AgentId);
                _hasNextReserved = false;
                _wpIndex++;

                RevealAndDiscoverAt(TileX, TileY); // 탐사는 걷기의 부산물 (舊 발견 체인 계승)

                if (_wpIndex >= _waypoints.Count)
                {
                    _waypoints = null;
                    State = AgentState.Acting;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 중단 / 공용
        // ─────────────────────────────────────────────────────────────────────

        private bool IsGoalCoolingDown(GoalSO goal)
            => _goalRetryAt.TryGetValue(goal, out float until) && Time.time < until;

        /// <summary>
        /// 실패 쿨다운 기록 여부 (순수 판정 — EditMode 게이트 대상, M2-E).
        /// P0 생존 goal은 SkipFailureCooldown으로 면제된다 (ADR-M2-5) — 실패 직후에도 즉시 재선택.
        /// </summary>
        public static bool ShouldRecordFailureCooldown(GoalSO goal, bool cooldownRequested)
            => cooldownRequested && goal != null && !goal.SkipFailureCooldown;

        // ─────────────────────────────────────────────────────────────────────
        // 촌장 명령 (M1-C)
        // ─────────────────────────────────────────────────────────────────────

        public enum OrderResult { Accepted, RefusedHungry, RefusedTired, FailedNoStock, RefusedInjured }

        /// <summary>
        /// 거부 판정의 유일한 규칙 (ADR-M1-2: 욕구 2축, 랜덤 없음) — 순수 함수라 게이트(M1-T1)가 직접 검증한다.
        /// 배고픔이 피로보다 먼저 판정된다 (둘 다면 배고픔 사유).
        /// </summary>
        public static OrderResult JudgeOrder(float satiety, float fatigue, AgentConfigSO cfg)
            => JudgeOrder(satiety, fatigue, cfg, null, null);

        /// <summary>성격 오프셋 포함 거부 판정 (M4-C, 순수 — 게이트 M4-T3). p=null이면 기존과 완전 동일.</summary>
        public static OrderResult JudgeOrder(float satiety, float fatigue, AgentConfigSO cfg, PersonalitySO p)
            => JudgeOrder(satiety, fatigue, cfg, p, null);

        /// <summary>
        /// 보상 오프셋 포함 최종 판정 (M6-E, 순수 — 게이트 M6-T4). r=null이면 기존과 완전 동일.
        /// 성격·보상은 문턱을 옮길 뿐 판정은 결정적 (ADR-M1-2 계승 — 랜덤 금지,
        /// 플레이어가 학습 가능해야 협상이 성립). 보상은 판정에만 개입, 수행에는 불개입 (ADR-M6-4).
        /// </summary>
        public static OrderResult JudgeOrder(float satiety, float fatigue, AgentConfigSO cfg,
                                             PersonalitySO p, RewardSO r)
        {
            float satLimit = cfg.OrderRefuseSatiety + (p != null ? p.RefuseSatietyOffset : 0f)
                                                    + (r != null ? r.RefuseSatietyOffset : 0f);
            float fatLimit = cfg.OrderRefuseFatigue + (p != null ? p.RefuseFatigueOffset : 0f)
                                                    + (r != null ? r.RefuseFatigueOffset : 0f);
            if (satiety < satLimit) return OrderResult.RefusedHungry;
            if (fatigue > fatLimit) return OrderResult.RefusedTired;
            return OrderResult.Accepted;
        }

        /// <summary>성격 대사 우선, 비면 기본 대사 (중립 경로 — ADR-M4-2).</summary>
        private static string[] FirstNonEmpty(string[] preferred, string[] fallback)
            => preferred != null && preferred.Length > 0 ? preferred : fallback;

        /// <summary>
        /// 명령 수신 — 거부는 수신 시점 1회, 욕구 상태 기반 판정 (ADR-M1-2, 랜덤 아님).
        /// 수락 시 goal이 개인 사다리에 합류하며, P0 인터럽트 후에도 유지되어 자동 복귀한다.
        /// reward(M6-E): 거부 문턱 오프셋 + 수락 시 에스크로 차감. 재고 부족이면 판정 전에
        /// FailedNoStock — 없는 것을 약속할 수 없다 (선검사 일괄, ADR-M0-8 원자성).
        /// </summary>
        public OrderResult TryGiveOrder(GoalSO order, ResourceNode targetNode = null, RewardSO reward = null)
        {
            if (order == null) return OrderResult.Accepted;

            // 부상 거절 (M10-A) — 판정 앞단 조기 반환: JudgeOrder(순수·게이트 보유)는 무변경.
            // 노동 goal이 필터로 막힌 주민에게 명령만 통하면 이원화가 된다 (몸의 사정은 하나).
            if (Injury != InjurySeverity.None)
            {
                ShowTransient(Pick(_cfg.InjuredLines));
                Debug.Log($"[VillagerAgent] {AgentId}: 명령 거부 (부상)");
                return OrderResult.RefusedInjured;
            }

            if (reward != null && World.GetStock(reward.CostSlot) < reward.CostAmount)
                return OrderResult.FailedNoStock; // 말풍선 없음 — 주민이 아니라 촌장의 실수

            OrderResult verdict = JudgeOrder(Satiety, Fatigue, _cfg, Personality, reward);
            if (verdict == OrderResult.RefusedHungry)
            {
                ShowTransient(Pick(FirstNonEmpty(Personality != null ? Personality.RefuseHungryLines : null, _cfg.RefuseHungryLines)));
                Debug.Log($"[VillagerAgent] {AgentId}: 명령 거부 (배고픔 {Satiety:F0} < {_cfg.OrderRefuseSatiety})");
                return verdict;
            }
            if (verdict == OrderResult.RefusedTired)
            {
                ShowTransient(Pick(FirstNonEmpty(Personality != null ? Personality.RefuseTiredLines : null, _cfg.RefuseTiredLines)));
                Debug.Log($"[VillagerAgent] {AgentId}: 명령 거부 (피로 {Fatigue:F0} > {_cfg.OrderRefuseFatigue})");
                return verdict;
            }

            ClearOrderInstance(); // 기존 명령 교체 시 런타임 사본·미지급 약속 정리 (반환 경유)

            // 보상 에스크로 (M6-E): 수락 확정 시점에 즉시 차감 — 완수=지급, 그 외=반환 (ADR-M6-5)
            if (reward != null)
            {
                if (!World.TrySpendStock(reward.CostSlot, reward.CostAmount))
                    return OrderResult.FailedNoStock; // 선검사 통과 후 변동 — 방어 (원자성은 TrySpend가 보장)
                _promisedReward = reward;
                ShowTransient(Pick(reward.PromiseLines));
                Debug.Log($"[VillagerAgent] {AgentId}: 보상 약속 — {reward.DisplayName} " +
                          $"({reward.CostSlot} -{reward.CostAmount} 에스크로)");
            }

            // 상대 목표 해석: "지금보다 +N" — 수신 시점 절대값으로 고정한 런타임 사본 생성
            _order = ResolveRelativeGoal(order, out _orderIsRuntimeClone);
            OrderTargetNode = targetNode;
            _goalRetryAt.Remove(_order); // 새 명령은 과거 실패 쿨다운을 잊는다

            // 즉시 착수: 현재 일이 명령보다 낮으면 중단 (실패 아님 — 쿨다운 없음)
            if ((State == AgentState.Moving || State == AgentState.Acting)
                && _goal != null && _goal.Priority < order.Priority)
            {
                AbortPlan($"촌장 명령 수락: {order.DisplayName}", warn: false, cooldown: false);
                _idleCooldownSec = 0f;
            }
            else if (State == AgentState.Idle)
            {
                _idleCooldownSec = 0f;
            }
            Debug.Log($"[VillagerAgent] {AgentId}: 명령 수락 — {order.DisplayName}");
            return OrderResult.Accepted;
        }

        /// <summary>
        /// 상대 목표 해석 공용 헬퍼 (M1-C · M8-D) — RelativeToCurrent면 수신 시점 절대값으로
        /// 고정한 런타임 사본을 만든다 (에셋 무변경). isClone이면 소멸 시 Destroy 필수.
        /// </summary>
        private GoalSO ResolveRelativeGoal(GoalSO goal, out bool isClone)
        {
            if (goal.RelativeToCurrent && goal.GoalConditions != null)
            {
                WorldSnapshot snap = BuildSnapshot();
                GoalSO resolved = Instantiate(goal);
                resolved.name = goal.name; // 로그·비교 가독성 (에셋은 무변경)
                resolved.RelativeToCurrent = false;
                for (int i = 0; i < resolved.GoalConditions.Length; i++)
                    resolved.GoalConditions[i].Value = snap.Get(resolved.GoalConditions[i].Slot)
                                                       + goal.GoalConditions[i].Value;
                isClone = true;
                return resolved;
            }
            isClone = false;
            return goal;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 주민 부탁 (M8-D, ADR-M8-4: _order와 대칭 — 개인 사다리의 또 한 칸)
        // ─────────────────────────────────────────────────────────────────────

        public enum RequestResult { Accepted, RefusedBusy, RefusedHungry, RefusedTired, RefusedLowAffinity, RefusedNoReward, RefusedInjured }

        private GoalSO _request;
        private bool _requestIsRuntimeClone;

        /// <summary>수락한 부탁 goal (읽기 전용). null = 부탁 없음.</summary>
        public GoalSO CurrentRequest => _request;

        /// <summary>
        /// 부탁 판정의 유일한 규칙 (순수 — 게이트 M8-T2). 순서 = 바쁨→배고픔→피로→친밀→선불
        /// (몸 → 마음 → 조건 — ADR-M8-2·ADR-보상2). 배고픔·피로 문턱은 촌장 명령(JudgeOrder)과
        /// 동일 규칙 재사용 — 부탁이라고 몸의 사정이 달라지지 않는다.
        /// upfrontAvailable = 선불 지급 가능 여부 (보상 있음 + 재고 충분 — 호출자가 계산).
        /// 선불 성격은 불가 시 거절, 보상 0 부탁도 여기로 (ADR-보상4 — 공짜로는 안 한다).
        /// 랜덤 금지 — 플레이어가 학습 가능해야 협상이 성립 (ADR-M1-2 계승).
        /// </summary>
        public static RequestResult JudgeRequest(bool busy, float satiety, float fatigue,
                                                 int affinityTowardRequester, AgentConfigSO cfg,
                                                 PersonalitySO p, RequestSO r,
                                                 bool upfrontAvailable = false)
        {
            if (busy) return RequestResult.RefusedBusy;
            OrderResult body = JudgeOrder(satiety, fatigue, cfg, p, null);
            if (body == OrderResult.RefusedHungry) return RequestResult.RefusedHungry;
            if (body == OrderResult.RefusedTired) return RequestResult.RefusedTired;
            if (r != null && affinityTowardRequester < r.RefuseAffinityBelow)
                return RequestResult.RefusedLowAffinity;
            if (p != null && p.DemandsRewardUpfront && !upfrontAvailable)
                return RequestResult.RefusedNoReward;
            return RequestResult.Accepted;
        }

        /// <summary>
        /// 부탁 수신 (RequestService 전용) — 수락 시 InjectGoal이 개인 사다리에 합류한다
        /// (ADR-M8-4: 새 실행 경로 없음, Select의 request 칸). 말풍선·관계 델타·사유 로그는
        /// 호출자(RequestService)가 수행 (ADR-M8-5의 단일 지점).
        /// </summary>
        public RequestResult TryGiveRequest(RequestSO r, string requesterId)
        {
            if (r == null || r.InjectGoal == null) return RequestResult.RefusedBusy; // 방어 — 에셋 오류
            // 부상 거절 (M10-A) — 명령과 동일한 앞단 조기 반환 (JudgeRequest 순수 함수 무변경)
            if (Injury != InjurySeverity.None) return RequestResult.RefusedInjured;
            // 선불 가용성 (ADR-보상2) — 보상이 있고 지금 재고가 충분해야. 판정과 차감이 같은
            // 시뮬 틱이라 검사~지급 사이 경쟁 없음 (지급은 RequestService.Ask 수락 경로)
            bool upfrontAvailable = r.RewardCostAmount > 0
                                    && World.GetStock(r.RewardCostSlot) >= r.RewardCostAmount;
            RequestResult verdict = JudgeRequest(_request != null, Satiety, Fatigue,
                _sim.Relationship.AffinityOf(AgentId, requesterId), _cfg, Personality, r,
                upfrontAvailable);
            if (verdict != RequestResult.Accepted) return verdict;

            _request = ResolveRelativeGoal(r.InjectGoal, out _requestIsRuntimeClone);
            _goalRetryAt.Remove(_request); // 새 부탁은 과거 실패 쿨다운을 잊는다 (명령과 동일)
            if (State == AgentState.Idle) _idleCooldownSec = 0f; // 즉시 재평가 (반응성)
            return RequestResult.Accepted;
        }

        /// <summary>상대 씬 goal 플래너 사본 파괴 (M9-H) — 수명 = 플랜 1회. 소멸의 유일한 경로.</summary>
        private void ClearPlanGoalClone()
        {
            if (_planGoalClone != null) Destroy(_planGoalClone);
            _planGoalClone = null;
        }

        /// <summary>부탁 슬롯 정리 — 런타임 사본이면 파괴 (누수 방지). 소멸의 유일한 경로.</summary>
        private void ClearRequestInstance()
        {
            if (_request != null && _requestIsRuntimeClone) Destroy(_request);
            _request = null;
            _requestIsRuntimeClone = false;
            VisitTargetAgentId = null;
        }

        // ── [DEPRECATED 2026-07-18 — 조각 Y] 완공 보고 심부름 (M8 후속 — "알리러 가기") ────────
        // 보고가 "쫓아가기"에서 "마주치면 정산"(RequestService.TickRewardSettlement)으로 바뀌며 휴면.
        // GiveReportErrand는 더 이상 호출되지 않아 아래 전부 죽은 경로. 후속 정리 대상
        // (Docs/퀘스트보드_및_보고심부름정리_후속.md).

        /// <summary>방문 심부름의 대상 주민 ID (VisitRunner가 읽는다). null = 심부름 없음.</summary>
        public string VisitTargetAgentId { get; private set; }

        /// <summary>방문 대상 조회 — 이탈(Dead)·소멸이면 null (러너가 실패로 승격).</summary>
        public VillagerAgent FindVisitTarget()
        {
            if (string.IsNullOrEmpty(VisitTargetAgentId)) return null;
            foreach (VillagerAgent a in _sim.Agents)
                if (a != null && a.State != AgentState.Dead && a.AgentId == VisitTargetAgentId)
                    return a;
            return null;
        }

        /// <summary>
        /// 보고 심부름 부여 (RequestService 전용) — 부탁 슬롯 재사용 (ADR-M8-4: 새 실행 경로 없음).
        /// 심부름 goal은 GoalConditions가 비어 완수 판정을 타지 않는다 — 소멸은 PlayReport/타임아웃.
        /// </summary>
        public void GiveReportErrand(GoalSO errand, string targetAgentId)
        {
            if (errand == null || string.IsNullOrEmpty(targetAgentId)) return;
            ClearRequestInstance(); // 방어 — 기존 슬롯 정리 후 (완수 경로에선 이미 비어 있음)
            _request = errand;
            _requestIsRuntimeClone = false;
            VisitTargetAgentId = targetAgentId;
            _goalRetryAt.Remove(errand);
            if (State == AgentState.Idle) _idleCooldownSec = 0f;
        }

        /// <summary>방문 도착 통지 (VisitRunner 전용) — 장면·보상·심부름 정리는 RequestService.</summary>
        public void CompleteVisit() => _sim.Requests?.PlayReport(this);

        /// <summary>심부름 회수 (RequestService 전용 — 보고 완료·타임아웃·의뢰인 이탈).</summary>
        public void ClearRequestErrand() => ClearRequestInstance();

        /// <summary>명령 취소 (주민 우클릭). 수행 중이었다면 즉시 자율 복귀.</summary>
        public void CancelOrder()
        {
            if (_order == null) return;
            GoalSO cancelled = _order;
            ClearOrderInstance();
            if (_goal == cancelled && (State == AgentState.Moving || State == AgentState.Acting))
                AbortPlan("명령 취소 (자율 복귀)", warn: false, cooldown: false);
        }

        /// <summary>임시 대사 표시의 유일한 통로 — ChatterService(M7)도 여기를 쓴다 (ADR-M7-4).
        /// holdSec 0 = 에셋 기본 노출(TransientLineSec). 장면별 연출 배속은 호출자(ChatterService)가
        /// 계산해 넘긴다 — 1회성 회의처럼 천천히 읽혀야 하는 장면용 (M9-E 후속).</summary>
        public void ShowTransient(string line, float holdSec = 0f)
        {
            if (string.IsNullOrEmpty(line)) return;
            _bubble?.ShowText(line);
            _transientTextUntil = Time.time + (holdSec > 0f ? holdSec : _cfg.TransientLineSec);
        }

        // 지연 응수 (M7-C) — 발화 말풍선 뒤 ReplyDelaySec 간격으로 "대화처럼" (ADR-M7-4)
        private string _delayedLine;
        private float _delayedShowAt;
        private float _delayedHold; // 응수 노출 시간 (0 = 에셋 기본) — 장면 배속 전달용
        private float _planResumeAt; // 대사 만료 후 플랜 말풍선 복원 예약 시각 (0 = 예약 없음)

        /// <summary>
        /// 지연 표시 — ChatterService의 응수 전용. 새 요청이 이전 대기분을 덮는다.
        /// 대기 중엔 말풍선을 비운다 — 기존 혼잣말("따뜻하다...")이 즉답처럼 보이는 것 방지
        /// (2026-07-17 관측: 잔소리 직후 여가 문구가 남아 대화가 어색). 듣는 사이 = 침묵.
        /// </summary>
        public void ShowTransientDelayed(string line, float delaySec, float holdSec = 0f)
        {
            if (string.IsNullOrEmpty(line)) return;
            _delayedLine = line;
            _delayedShowAt = Time.time + delaySec;
            _delayedHold = holdSec;
            _bubble?.Clear();
        }

        // 대화 연출: 멈춰서 마주보기 (2026-07-17 사용자 결정 — ADR-M7 오해위험① '멈춤 없음' 개정).
        // 표현 계층만 잠근다: Update의 이동 보간·애니메이션만 정지, SimTick(욕구·러너·goal 전환)은
        // 그대로 — 시뮬 상태 쓰기 0은 유지 (ADR-M7-1의 본질).
        private float _chatPauseUntil;
        private Vector2 _chatFaceDir;

        /// <summary>대화 상대를 바라보며 잠시 멈춘다 — ChatterService 전용. pauseSec 0이면 멈춤 없음.</summary>
        public void FaceForChat(Vector2 towardWorldPos, float pauseSec)
        {
            Vector2 d = towardWorldPos - (Vector2)transform.position;
            if (d.sqrMagnitude > 1e-4f) _chatFaceDir = d.normalized;
            else _chatFaceDir = _lastDir;
            _chatPauseUntil = Time.time + pauseSec;
        }

        private static string Pick(string[] lines)
            => lines == null || lines.Length == 0 ? null : lines[Random.Range(0, lines.Length)];

        /// <summary>
        /// warn=false는 정상 흐름의 전환(상위 goal 인터럽트) — 경고가 아닌 정보 로그.
        /// cooldown=false는 실패가 아닌 중단 — 재시도 벌칙을 주지 않는다 (전환·복귀 즉시 성립).
        /// </summary>
        private void AbortPlan(string reason, bool warn = true, bool cooldown = true)
        {
            string msg = $"[VillagerAgent] {AgentId}: 플랜 중단 — {reason} (goal={(_goal != null ? _goal.name : "?")})";
            if (warn) Debug.LogWarning(msg); else Debug.Log(msg);
            if (ShouldRecordFailureCooldown(_goal, cooldown)) _goalRetryAt[_goal] = Time.time + _cfg.GoalRetryCooldownSec;
            _runner?.Cleanup(this);
            _runner = null;
            _plan.Clear();
            _bubble?.Clear();
            _waypoints = null;
            _hasNextReserved = false;

            // 예약 정리: 전부 해제 후 현재 타일만 재확보 (leak 방지, 舊 ADR-T6)
            TileReservationRegistry.ReleaseAllBy(AgentId);
            TileReservationRegistry.TryReserve(new Vector2Int(TileX, TileY), AgentId);

            ToIdle(1f);
        }

        private void ToIdle(float cooldownSec)
        {
            // goal 내려놓기의 유일한 지점 (ADR-M3-4) — 완료·중단·전환·타임아웃 전부 여기를 지난다
            if (_goal != null)
            {
                _sim.Goals.Release(_goal);
                _goal = null;
            }
            ClearPlanGoalClone(); // M9-H — 플래너 입력 사본 파괴 (수명 = 플랜 1회)
            State = AgentState.Idle;
            _idleCooldownSec = cooldownSec;
        }

        // ── 몸 소지 개인 스톡 (M11-A — 개인 인벤토리 선반의 절반. 세이브 대상 ADR-M11-10) ──

        /// <summary>몸 소지 생식. 쓰기는 ApplyPersonalStock만 (ADR-M11-1).</summary>
        public int MyRaw { get; private set; }

        /// <summary>몸 소지 조리식.</summary>
        public int MyCooked { get; private set; }

        /// <summary>슬롯별 잔량 — EffectApplier 선검사·스냅샷 주입 공용 (판정 단일).</summary>
        public int GetPersonalStock(SlotId slot)
            => slot == SlotId.MyRawFood ? MyRaw
             : slot == SlotId.MyCookedFood ? MyCooked : 0;

        /// <summary>
        /// 개인 스톡 계단 (순수 — 게이트 M11-T1): Sub 부족 = 실패(무변경), Add 상한 초과 = 실패
        /// (클램프로 초과분을 조용히 버리지 않는다 — 선검사 실패 → 러너 실패 → 재계획).
        /// ⚠️ 상한은 슬롯별이다 (명세 §4.2 "합산"에서 개정 — 플래너 전제 언어가 단일 슬롯 비교라
        /// 합산 상한은 표현 불가. 합산을 런타임에만 두면 컴파일러 주입 전제와 판정이 이원화되어
        /// ADR-M11-3 "클램프 발동 = 버그" 불변식이 깨진다. 사유는 M11-A 커밋 메시지).
        /// </summary>
        public static (bool ok, int next) NextPersonalStock(int current, EffectOp op, int value, int cap)
        {
            switch (op)
            {
                case EffectOp.Add:
                    return current + value > cap ? (false, current) : (true, current + value);
                case EffectOp.SubClamp0:
                    return current < value ? (false, current) : (true, current - value);
                default: // Set — 전역 스톡과 동일하게 미지원 (EffectApplier가 경고)
                    return (false, current);
            }
        }

        /// <summary>
        /// 개인 스톡 쓰기의 유일한 문 (ADR-M11-1) — 호출처는 EffectApplier와 시작 분배·보상 이전뿐.
        /// 실패 반환 = 정상 흐름 (원자성) — 다만 EffectApplier 선검사를 통과하고도 실패하면
        /// 컴파일러 상한 주입 누락 버그다 (ADR-M11-3 방어선, 경고는 EffectApplier가 낸다).
        /// </summary>
        public bool ApplyPersonalStock(SlotId slot, EffectOp op, int value)
        {
            if (!SlotIds.IsPersonalStock(slot)) return false;
            (bool ok, int next) = NextPersonalStock(GetPersonalStock(slot), op, value, _cfg.BodyCarryCap);
            if (!ok) return false;
            if (slot == SlotId.MyRawFood) MyRaw = next;
            else MyCooked = next;
            return true;
        }

        /// <summary>내 집 타일 (M11-A) — 집 저장 라우팅·피난 목적지 공용 창구. 원천 = OwnershipService.</summary>
        public bool TryGetHomeTile(out Vector2Int tile)
            => _sim.Ownership.TryGetOwned(AgentId, SlotId.HouseCount, out tile);

        /// <summary>EffectApplier 전용 — My* 슬롯 효과를 개인 욕구에 반영 (0~100 클램프).</summary>
        public void ApplyNeedEffect(SlotId slot, EffectOp op, int value)
        {
            float cur = slot == SlotId.MySatiety ? Satiety : Fatigue;
            switch (op)
            {
                case EffectOp.Add:       cur += value; break;
                case EffectOp.SubClamp0: cur -= value; break;
                case EffectOp.Set:       cur  = value; break;
            }
            cur = Mathf.Clamp(cur, 0f, 100f);
            if (slot == SlotId.MySatiety) Satiety = cur;
            else Fatigue = cur;
        }

        private void RevealAndDiscoverAt(int x, int y)
        {
            int radius = MapConfig.Active != null ? MapConfig.Active.villagerSightRadius : 10;
            FowManager.Instance?.RevealArea(x, y, radius);
            Discovery.DiscoverArea(x, y, radius);
        }

        /// <summary>
        /// 시각 표현 초기화 (W5): 스프라이트 세트가 있으면 Kenmi 걷기 애니메이션,
        /// 없으면 개체 색 원형 마커 폴백. 아트 교체 = AgentSpriteSetSO 에셋 교체.
        /// </summary>
        private void SetupView()
        {
            float hue = StableHash.Value01(AgentId, "hue"); // GetHashCode 붕괴 수정 (2026-07-17)
            Color agentColor = Color.HSVToRGB(hue, 0.7f, 1f);

            AgentSpriteSetSO set = _sim.SpriteSet;
            if (set == null)
            {
                if (GetComponent<SpriteRenderer>() != null) return;
                var marker = gameObject.AddComponent<SpriteRenderer>();
                marker.sprite = M0Sprites.Circle;
                marker.color = agentColor;
                marker.sortingOrder = 10;
                transform.localScale = Vector3.one * 0.8f;
                return;
            }

            // Kenmi 조각은 피벗이 좌하단 — 자식 오프셋으로 타일 중앙 정렬 (32px/16ppu = 2유닛)
            var view = new GameObject("View");
            view.transform.SetParent(transform, worldPositionStays: false);
            view.transform.localScale = Vector3.one * set.Scale;
            view.transform.localPosition = new Vector3(-set.Scale, -set.Scale, 0f);

            var sr = view.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 10;
            _animator = new AgentAnimator(sr, set, agentColor);
        }

        /// <summary>말풍선 초기화 (W6). SetupView와 분리 — 마커 폴백이어도 말풍선은 표시.</summary>
        private void SetupBubble()
        {
            _bubble = new PlanBubble(transform, _sim.BubbleFont, _cfg);
            // 상시 이름표 (M7-A) — 직업만 표기, 무직은 이름만 (ADR-M7-5)
            _nameTag = new NameTag(transform, _sim.BubbleFont, _cfg,
                Job != null ? $"{ShortName} · {Job.DisplayName}" : ShortName);
        }
    }
}
