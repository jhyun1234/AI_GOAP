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
        Dead,     // M0에서 진입 경로 없음 (전투 부재) — 상태만 예약
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
        public FarmService Farm => _sim.Farm;
        public WorldConfigSO WorldConfig => _sim.WorldConfig;

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
            Personality = _sim.PickRandomPersonality();
            // 직업 할당 (M5-A) — 성격과 별개 축, 스폰 1회 고정 (세이브 대상, ADR-M5-5)
            Job = _sim.PickRandomJob();
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

            // 임시 문구(거부 대사) 만료 처리 — 실행 중이면 다음 ShowPlan이 자연 복원
            if (_transientTextUntil > 0f && Time.time >= _transientTextUntil)
            {
                _transientTextUntil = 0f;
                if (State == AgentState.Idle) _bubble?.Clear();
                else _bubble?.ShowPlan(_plan, _planIndex, OrderPrefix());
            }

            // 지연 응수 발화 (M7-C) — 표현 전용, 걸음은 멈추지 않는다
            if (_delayedShowAt > 0f && Time.time >= _delayedShowAt)
            {
                _delayedShowAt = 0f;
                ShowTransient(_delayedLine);
                _delayedLine = null;
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
            _tickCounter++;

            // 실행 중 상위 goal 전환 (데이터 주도 — 임계값은 GoalSO 에셋에만 존재)
            if ((State == AgentState.Moving || State == AgentState.Acting)
                && _tickCounter % GOAL_RECHECK_EVERY_TICKS == 0)
            {
                // 쿨다운 필터를 Idle 선택과 동일하게 적용 — 방금 실패한 goal이 전환 대상으로
                // 재등장해 중단↔재시작 폭주(0.5초 주기)를 일으키는 것을 방지
                GoalSO now = _sim.Goals.Select(BuildSnapshot(), IsGoalCoolingDown, _order, _goalBias, _routine);
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

        private WorldSnapshot BuildSnapshot()
            => World.BuildSnapshot(Mathf.RoundToInt(Satiety), Mathf.RoundToInt(Fatigue));

        /// <summary>포만 감쇠 산식의 유일한 지점 (M6-B) — 순수 함수라 게이트(M6-T2b)가 직접 검증한다.</summary>
        public static float SatietyDecay(float perGameDay, float seasonMult, float deltaGameDays)
            => perGameDay * seasonMult * deltaGameDays;

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
            _sim.Hud?.Notify($"{AgentId}이(가) 마을을 떠났습니다");
            ShowTransient(Pick(_cfg.DepartLines));
            State = AgentState.Dead;      // SimTick 차단 — 새 상태 추가 금지 (ADR-M6-3)
            Destroy(gameObject, 2.5f);    // 마지막 대사 노출 후 소멸 (연출 상수 — ShowTransient와 동일)
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

            _goal = _sim.Goals.Select(snap, IsGoalCoolingDown, _order, _goalBias, _routine);
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

            _pending = _sim.Planner.RequestPlan(snap, _goal, _costMult);
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

            if (!_sim.Planner.TryGetResult(_pending, out PlanStatus status, out ActionSO[] plan, out _))
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

                default: // NoSolution — ADR-3: 버그 증상. MAX_NODES 인상 금지, 로그로 노출
                    Debug.LogWarning($"[VillagerAgent] {AgentId}: NoSolutionFound (goal={_goal.name}) — ADR-3 진단 필요");
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

        private void StartNextAction()
        {
            _bubble?.ShowPlan(_plan, _planIndex, OrderPrefix()); // 갱신 단일 지점: 적재·전환·완료 모두 여기 경유

            if (_planIndex >= _plan.Count)
            {
                if (!_directGoal) Debug.Log($"[VillagerAgent] {AgentId}: {_goal.DisplayName} 플랜 완료");
                ToIdle(0f); // 여가는 로그 없이 조용히 반복 (스팸 방지)
                return;
            }

            ActionSO action = _plan[_planIndex];

            // 성격 혼잣말 (M4-D) — 표현 전용, 확률·문구 전부 에셋 값. 비면 표시 없음 (중립 경로).
            // ShowTransient가 잠시 덮고 다음 갱신에서 플랜 문구로 복귀 — 거부 대사와 같은 통로.
            if (Personality != null && Personality.MoodLines != null && Personality.MoodLines.Length > 0
                && Random.value < Personality.MoodLineChance)
                ShowTransient(Pick(Personality.MoodLines));

            // 위기 예고 술렁임 (M6-C) — 혼잣말과 같은 통로. 예고 구간(위기 전)에만, 위기 중은 제외.
            // 대사·확률 전부 에셋 값 (SeasonSO.ForecastLines / AgentConfig.ForecastMoodChance).
            if (_sim.Season != null && _sim.Season.NextCrisis != null
                && _sim.Season.DaysToCrisis > 0f
                && _sim.Season.DaysToCrisis <= _sim.WorldConfig.ForecastDays
                && Random.value < _cfg.ForecastMoodChance)
                ShowTransient(Pick(_sim.Season.NextCrisis.ForecastLines));

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

            PathResult path = JPSPathfinder.FindPathResult(TileX, TileY, target.Value.x, target.Value.y, _sim.Walkable);
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
            float speed = _motion.Tick(dt, nearDest);
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

        public enum OrderResult { Accepted, RefusedHungry, RefusedTired, FailedNoStock }

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
            if (order.RelativeToCurrent && order.GoalConditions != null)
            {
                WorldSnapshot snap = BuildSnapshot();
                GoalSO resolved = Instantiate(order);
                resolved.name = order.name; // 로그·비교 가독성 (에셋은 무변경)
                resolved.RelativeToCurrent = false;
                for (int i = 0; i < resolved.GoalConditions.Length; i++)
                    resolved.GoalConditions[i].Value = snap.Get(resolved.GoalConditions[i].Slot)
                                                       + order.GoalConditions[i].Value;
                _order = resolved;
                _orderIsRuntimeClone = true;
            }
            else
            {
                _order = order;
                _orderIsRuntimeClone = false;
            }
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

        /// <summary>명령 취소 (주민 우클릭). 수행 중이었다면 즉시 자율 복귀.</summary>
        public void CancelOrder()
        {
            if (_order == null) return;
            GoalSO cancelled = _order;
            ClearOrderInstance();
            if (_goal == cancelled && (State == AgentState.Moving || State == AgentState.Acting))
                AbortPlan("명령 취소 (자율 복귀)", warn: false, cooldown: false);
        }

        /// <summary>임시 대사 표시의 유일한 통로 — ChatterService(M7)도 여기를 쓴다 (ADR-M7-4).</summary>
        public void ShowTransient(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            _bubble?.ShowText(line);
            _transientTextUntil = Time.time + 2.5f; // 대사 노출 시간 (연출 상수)
        }

        // 지연 응수 (M7-C) — 발화 말풍선 뒤 ReplyDelaySec 간격으로 "대화처럼" (ADR-M7-4)
        private string _delayedLine;
        private float _delayedShowAt;

        /// <summary>지연 표시 — ChatterService의 응수 전용. 새 요청이 이전 대기분을 덮는다.</summary>
        public void ShowTransientDelayed(string line, float delaySec)
        {
            if (string.IsNullOrEmpty(line)) return;
            _delayedLine = line;
            _delayedShowAt = Time.time + delaySec;
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
            State = AgentState.Idle;
            _idleCooldownSec = cooldownSec;
        }

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
