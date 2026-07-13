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
        public WorldConfigSO WorldConfig => _sim.WorldConfig;

        // ── 플랜 상태 ──────────────────────────────────────────────────────
        private readonly List<ActionSO> _plan = new List<ActionSO>(PlanningConfig.MaxPlanLen);
        private int _planIndex;
        private GoalSO _goal;
        private bool _directGoal; // DirectActionPool goal 여부 (완료 로그 억제용)
        private PlannerGateway.PendingPlan _pending;
        private IActionRunner _runner;
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
        private Vector2 _lastDir = Vector2.down;

        // ─────────────────────────────────────────────────────────────────────
        // 라이프사이클
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            AgentId = name;
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
            Satiety = _cfg.InitialSatiety;
            Fatigue = _cfg.InitialFatigue;
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
            _runner?.Cleanup(this);
            if (_pending != null && _sim != null) _sim.Planner.Cancel(_pending);
            TileReservationRegistry.ReleaseAllBy(AgentId);
            if (_sim != null) _sim.UnregisterAgent(this);
        }

        private void Update()
        {
            if (State == AgentState.Moving) TickMoving(Time.deltaTime);
            _animator?.Tick(Time.deltaTime, State == AgentState.Moving && _hasNextReserved, _lastDir);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 시뮬 틱 (M0SimulationLoop가 0.1초마다 호출)
        // ─────────────────────────────────────────────────────────────────────

        public void SimTick(float dtSec, float deltaGameDays)
        {
            if (State == AgentState.Dead) return;

            Satiety = Mathf.Max(0f, Satiety - _cfg.SatietyDecayPerGameDay * deltaGameDays);
            _tickCounter++;

            // 실행 중 상위 goal 전환 (데이터 주도 — 임계값은 GoalSO 에셋에만 존재)
            if ((State == AgentState.Moving || State == AgentState.Acting)
                && _tickCounter % GOAL_RECHECK_EVERY_TICKS == 0)
            {
                GoalSO now = _sim.Goals.Select(BuildSnapshot());
                if (now != null && _goal != null && now != _goal && now.Priority > _goal.Priority)
                {
                    AbortPlan($"상위 목표 전환: {_goal.DisplayName} → {now.DisplayName}");
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

        // ─────────────────────────────────────────────────────────────────────
        // Idle → Planning
        // ─────────────────────────────────────────────────────────────────────

        private void TickIdle(float dt)
        {
            _idleCooldownSec -= dt;
            if (_idleCooldownSec > 0f) return;

            WorldSnapshot snap = BuildSnapshot();
            _goal = _sim.Goals.Select(snap);
            if (_goal == null)
            {
                _idleCooldownSec = 0.5f; // 할 일 없음 — 정상 Idle
                return;
            }

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

            _pending = _sim.Planner.RequestPlan(snap, _goal);
            if (_pending == null)
            {
                _idleCooldownSec = 1f;
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

        private void StartNextAction()
        {
            _bubble?.ShowPlan(_plan, _planIndex); // 갱신 단일 지점: 적재·전환·완료 모두 여기 경유

            if (_planIndex >= _plan.Count)
            {
                if (!_directGoal) Debug.Log($"[VillagerAgent] {AgentId}: {_goal.DisplayName} 플랜 완료");
                ToIdle(0f); // 여가는 로그 없이 조용히 반복 (스팸 방지)
                return;
            }

            ActionSO action = _plan[_planIndex];
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
                    AbortPlan($"경로 없음 → ({target.Value.x},{target.Value.y})");
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

        private void AbortPlan(string reason)
        {
            Debug.LogWarning($"[VillagerAgent] {AgentId}: 플랜 중단 — {reason} (goal={(_goal != null ? _goal.name : "?")})");
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
            float hue = Mathf.Abs(AgentId.GetHashCode() % 1000) / 1000f;
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
        }
    }
}
