/// <summary>
/// EnemyFSM.cs - 적 유닛 AI 유한 상태 머신 (FSM)
///
/// 역할(Role): 팩션 소속 적 유닛 1개의 상태 전이와 행동을 관리한다.
///             FactionAI로부터 목표 타일을 받아 이동/공격/후퇴/대기 상태를 처리한다.
///             SensorSystem의 IEnemyAgent 인터페이스를 구현하여 주민의 NearEnemy 감지에 활용된다.
///
/// 사용법(Usage):
///   1. Enemy GameObject에 이 컴포넌트를 추가한다.
///   2. Inspector에서 _factionId, _tickGroupIndex, _baseTileX, _baseTileY를 설정한다.
///   3. GameManager.Start()에서 InjectDependencies(messageBus)를 호출한다.
///   4. SensorSystem에 이 인스턴스를 RegisterEnemy()로 등록한다.
///   5. FactionAI가 SetTargetTile()로 이동 명령을 내린다.
///
/// 의존성(Dependencies):
///   - EnemyBrain.cs (데이터 클래스)
///   - MessageBus.cs, VillagerEnums.cs (AIVillage.Core / AIVillage.AI)
///   - SensorSystem의 IEnemyAgent 인터페이스 (AIVillage.Core)
///
/// Script Execution Order: [DefaultExecutionOrder(0)] — 기본값 (FactionAI -70 이후)
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-27
/// </summary>

using System;
using UnityEngine;
using AIVillage.Core;

namespace AIVillage.AI
{
    /// <summary>
    /// 적 유닛 AI FSM. IEnemyAgent를 구현하여 SensorSystem의 적 감지 시스템에 통합된다.
    /// FactionAI가 SetTargetTile()로 지시를 내리고, 이 FSM이 실제 상태를 처리한다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(0)] // FactionAI(-70)가 먼저 초기화된 후 EnemyFSM이 초기화된다
    public sealed class EnemyFSM : MonoBehaviour, IEnemyAgent
    {
        #region ── 상수 ──

        // ── 상태별 더미 시뮬레이션 시간 (기획서 수치) ──────────────────────────
        private const float MOVE_SIMULATE_SEC    = 3.0f;  // 이동 시뮬레이션 완료 시간 (3초)
        private const float ATTACK_SIMULATE_SEC  = 2.0f;  // 공격 시뮬레이션 완료 시간 (2초)

        // ── 전투 수치 (기획서 수치) ────────────────────────────────────────────
        private const float HP_RETREAT_THRESHOLD  = 30f;   // HP가 이 값 이하면 Retreating 전이
        private const float HP_RETURN_THRESHOLD   = 80f;   // Retreating에서 HP 이 값 이상이면 Idle 복귀
        private const float HP_REGEN_PER_SEC      = 0.5f;  // 후퇴 중 HP 회복 속도 (0.5f HP/초)
        private const float BASE_DAMAGE_PER_TICK  = 10f;   // [8단계] 틱당 주민에게 입히는 기본 피해
        private const float ATTACK_RANGE_TILES    = 2f;    // [8단계] 공격 유효 타일 거리 (맨해튼)

        // ── 약탈 수치 (FactionAI 확장) ─────────────────────────────────────────
        private const float PLUNDER_WOOD_AMOUNT   = 5f;    // [FactionAI 확장] 약탈 시 빼앗는 나무 양

        // ── [12단계] 경로 이동 속도 ─────────────────────────────────────────────
        private const float ENEMY_MOVE_SPEED          = 1.5f;   // 적 유닛 이동 속도 (타일/초)
        private const float WAYPOINT_ARRIVE_DIST      = 0.05f;  // 중간 도착 판정 거리 (Unity 유닛)
        private const float TERMINAL_ARRIVE_DIST      = 0.5f;   // 최종 목표 도착 반경 — 몰림 시 진동 방지
        private const float STATIONARY_SNAP_THRESHOLD = 1.0f;   // 정지 상태에서 이 거리 초과로 이탈 시에만 논리 좌표 복귀

        #endregion

        #region ── Serialized Fields (Inspector) ──

        [Tooltip("Tick 그룹 인덱스 (0~5). 적마다 다르게 설정하여 Tick 부하를 분산한다.")]
        [SerializeField] private int _tickGroupIndex = 0;

        [Tooltip("소속 팩션 ID. 0=숲의 부족, 1=철의 도시, 2=상인 연합. FactionAI와 반드시 일치해야 한다.")]
        [SerializeField] private int _factionId = 0;

        [Tooltip("소속 팩션 기지 타일 X 좌표. Retreating 상태에서 복귀 목적지로 사용한다.")]
        [SerializeField] private int _baseTileX = 20;

        [Tooltip("소속 팩션 기지 타일 Y 좌표. Retreating 상태에서 복귀 목적지로 사용한다.")]
        [SerializeField] private int _baseTileY = 20;

        [Tooltip("EnemyFSM 초기 타일 X 좌표. 기지 위치와 동일하게 설정하거나 배치 위치를 반영한다.")]
        [SerializeField] private int _initialTileX = 20;

        [Tooltip("EnemyFSM 초기 타일 Y 좌표. 기지 위치와 동일하게 설정하거나 배치 위치를 반영한다.")]
        [SerializeField] private int _initialTileY = 20;

        #endregion

        #region ── Private Fields ──

        // ── EnemyBrain: 모든 런타임 상태 데이터 ────────────────────────────────
        // VillagerFSM의 Brain 패턴과 동일하게 MonoBehaviour와 데이터를 분리한다.
        // public으로 노출하여 FactionAI와 SensorSystem이 직접 읽을 수 있게 한다.
        public EnemyBrain Brain { get; private set; }

        // ── 외부 의존성 (InjectDependencies로 주입) ────────────────────────────
        // Update() 또는 Tick() 내에서 GetComponent/Find 절대 금지 — Awake에서 캐시한다.
        private MessageBus _messageBus;

        // ── 틱 활성화 플래그 ────────────────────────────────────────────────────
        // Dead 상태 진입 시 false로 설정하여 이후 Tick() 호출을 차단한다.
        private bool _isTickActive = true;

        // ── [12단계] FlowField 기반 이동 추적 ────────────────────────────────────
        private bool    _isMoving   = false;       // 타일 간 시각적 이동 진행 중
        private Vector3 _moveTarget = Vector3.zero; // 현재 이동 목표 월드 좌표

        // ── 물리 컴포넌트 캐시 ──────────────────────────────────────────────────
        // Awake에서 부착 후 캐시. 유닛 간 자동 분리 + 나중 발사체 히트 판정용.
        private Rigidbody     _rigidbody;
        private SphereCollider _sphereCollider;

        #endregion

        #region ── IEnemyAgent 프로퍼티 구현 ──

        // SensorSystem이 NearEnemy 판정에 사용하는 최소 인터페이스 구현.
        // SensorSystem.cs에 정의된 IEnemyAgent의 TileX, TileY, IsAlive를 구현한다.

        /// <summary>적의 현재 타일 X 좌표. SensorSystem의 NearEnemy 판정에 사용한다.</summary>
        public int TileX => Brain?.TileX ?? 0;

        /// <summary>적의 현재 타일 Y 좌표. SensorSystem의 NearEnemy 판정에 사용한다.</summary>
        public int TileY => Brain?.TileY ?? 0;

        /// <summary>생존 여부. false이면 SensorSystem이 NearEnemy 판정에서 제외한다.</summary>
        public bool IsAlive => Brain?.IsAlive ?? false;

        // ── 추가 공개 프로퍼티 (FactionAI가 읽는 정보) ────────────────────────

        /// <summary>런타임 고유 ID. FactionAI가 유닛 목록을 관리할 때 참조한다.</summary>
        public string EnemyId => Brain?.EnemyId ?? string.Empty;

        /// <summary>소속 팩션 ID. FactionAI가 자신의 유닛만 필터링할 때 사용한다.</summary>
        public int FactionId => _factionId;

        /// <summary>체력 (0~100).</summary>
        public float HealthLevel => Brain?.HealthLevel ?? 0f;

        /// <summary>현재 FSM 상태. FactionAI가 유닛 상태를 모니터링할 때 사용한다.</summary>
        public EnemyState CurrentState => Brain?.FSMState ?? EnemyState.Dead;

        /// <summary>
        /// Tick 그룹 인덱스. GameManager가 읽어서 그룹별 Tick 호출 여부를 판단한다.
        /// </summary>
        public int TickGroupIndex => _tickGroupIndex;

        #endregion

        #region ── Unity 생명주기 메서드 ──

        /// <summary>
        /// Unity가 GameObject 활성화 시 가장 먼저 호출한다.
        /// EnemyBrain을 초기화하고 GUID를 생성한다.
        /// </summary>
        private void Awake()
        {
            // ── EnemyBrain 초기화 ────────────────────────────────────────────────
            // 런타임 GUID 생성: 에디터에서 복사본을 만들어도 ID가 충돌하지 않는다.
            Brain = new EnemyBrain
            {
                EnemyId        = Guid.NewGuid().ToString(),
                FactionId      = _factionId,
                TileX          = _initialTileX,
                TileY          = _initialTileY,
                BaseTileX      = _baseTileX,
                BaseTileY      = _baseTileY,
                TargetTileX    = _baseTileX, // 초기 목표는 기지 위치
                TargetTileY    = _baseTileY,
                FSMState       = EnemyState.Idle,
                HealthLevel    = 100f,
                IsAlive        = true,
                HasWeapon      = true,
                TickGroupIndex = _tickGroupIndex,
                LastHealTime   = 0f
            };

            Debug.Log($"[EnemyFSM] 초기화 완료. EnemyId={Brain.EnemyId}, " +
                      $"FactionId={_factionId}, 기지=({_baseTileX},{_baseTileY})");

            // ── SphereCollider + Rigidbody 자동 부착 ─────────────────────────
            // 목적: 유닛 간 물리 분리 + 나중에 발사체 히트 판정. VillagerFSM과 동일 패턴.
            _sphereCollider = GetComponent<SphereCollider>();
            if (_sphereCollider == null)
            {
                _sphereCollider = gameObject.AddComponent<SphereCollider>();
            }
            _sphereCollider.radius    = 0.4f;
            _sphereCollider.isTrigger = false;

            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody == null)
            {
                _rigidbody = gameObject.AddComponent<Rigidbody>();
            }
            _rigidbody.useGravity     = false;
            _rigidbody.linearDamping  = 5f;
            _rigidbody.angularDamping = 5f;
            _rigidbody.interpolation  = RigidbodyInterpolation.Interpolate;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            _rigidbody.constraints =
                RigidbodyConstraints.FreezePositionZ |
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationY |
                RigidbodyConstraints.FreezeRotationZ;
        }

        /// <summary>
        /// Unity가 매 프레임 호출한다.
        /// AnyState 전이(Dead, Retreating)를 감지한다.
        /// 실제 상태 로직은 외부에서 Tick()을 호출할 때 실행된다.
        ///
        /// GetComponent 계열 호출 금지 — 모든 참조는 Awake에서 캐시되었다.
        /// </summary>
        private void Update()
        {
            if (Brain == null) return;

            // ── AnyState 전이 #1: 사망 감지 ──────────────────────────────────────
            // IsAlive가 false가 되는 순간 어떤 상태에서도 즉시 Dead로 전이한다.
            if (!Brain.IsAlive && Brain.FSMState != EnemyState.Dead)
            {
                TransitionTo(EnemyState.Dead);
                return;
            }

            // ── AnyState 전이 #2: HP 30% 이하 → Retreating ───────────────────────
            // Dead/Retreating 상태가 아닐 때만 체크하여 중복 전이를 방지한다.
            // 공식: HP 30% = HealthLevel <= HP_RETREAT_THRESHOLD (30f)
            if (Brain.IsAlive
                && Brain.FSMState != EnemyState.Retreating
                && Brain.FSMState != EnemyState.Dead
                && Brain.HealthLevel <= HP_RETREAT_THRESHOLD)
            {
                Debug.Log($"[EnemyFSM] AnyState → Retreating (HP={Brain.HealthLevel:F1} <= {HP_RETREAT_THRESHOLD}). " +
                          $"EnemyId={Brain.EnemyId}");
                TransitionTo(EnemyState.Retreating);
                return;
            }

            // ── Retreating 상태에서 HP 회복 처리 ─────────────────────────────────
            // HP 회복은 매 프레임 정밀하게 처리해야 하므로 Tick이 아닌 Update에서 수행한다.
            // 공식: HP += 0.5f HP/초 × deltaTime
            if (Brain.FSMState == EnemyState.Retreating && Brain.IsAlive)
            {
                Brain.HealthLevel = Mathf.Min(100f, Brain.HealthLevel + HP_REGEN_PER_SEC * Time.deltaTime);
            }

            // ── [12단계] 시각적 이동 처리 ────────────────────────────────────────
            // rb.MovePosition 사용: 유닛 간 물리 자동 분리 + 발사체 히트 시 AddForce 지원.
            if (_isMoving)
            {
                Vector3 nextPos = Vector3.MoveTowards(
                    _rigidbody.position,
                    _moveTarget,
                    ENEMY_MOVE_SPEED * Time.deltaTime);
                _rigidbody.MovePosition(nextPos);

                if (Vector3.Distance(nextPos, _moveTarget) < WAYPOINT_ARRIVE_DIST)
                {
                    _isMoving = false;
                    // 도착 시 논리 좌표를 목표 월드 위치 기준으로 갱신
                    Brain.TileX = Mathf.RoundToInt(_moveTarget.x);
                    Brain.TileY = Mathf.RoundToInt(_moveTarget.y);
                }
            }
            else
            {
                // 정지 상태(Attacking/Idle 등): 콜라이더가 유닛끼리 자연스럽게 분리하도록 두고,
                // 논리 위치에서 심하게(> 1타일) 이탈한 경우에만 복귀. 소량 오프셋은 진동 방지 위해 유지.
                Vector3 logicalPos = new Vector3(Brain.TileX, Brain.TileY, 0f);
                if (Vector3.Distance(_rigidbody.position, logicalPos) > STATIONARY_SNAP_THRESHOLD)
                {
                    Vector3 snapPos = Vector3.MoveTowards(
                        _rigidbody.position,
                        logicalPos,
                        ENEMY_MOVE_SPEED * Time.deltaTime);
                    _rigidbody.MovePosition(snapPos);
                }
            }
        }

        #endregion

        #region ── 외부 호출 메서드 ──

        /// <summary>
        /// GameManager 또는 FactionAI가 MessageBus 참조를 주입한다.
        /// InjectDependencies 패턴: Awake 이후 Start 이전에 호출하는 것을 권장한다.
        /// </summary>
        /// <param name="messageBus">MessageBus 인스턴스. null이면 메시지 발행 기능이 비활성화된다.</param>
        public void InjectDependencies(MessageBus messageBus)
        {
            // null도 허용한다 — null이면 메시지 발행 시 경고만 출력하고 계속 진행한다.
            _messageBus = messageBus;

            if (_messageBus == null)
            {
                Debug.LogWarning($"[EnemyFSM] InjectDependencies: messageBus가 null입니다. " +
                                 $"메시지 발행 기능이 비활성화됩니다. EnemyId={EnemyId}");
            }
        }

        /// <summary>
        /// FactionAI가 호출하여 이동 목표 타일을 설정하고 Moving 상태로 전이시킨다.
        /// </summary>
        /// <param name="x">목표 타일 X 좌표.</param>
        /// <param name="y">목표 타일 Y 좌표.</param>
        public void SetTargetTile(int x, int y, RaidMode raidMode = RaidMode.Attack)
        {
            if (Brain == null)
            {
                Debug.LogWarning($"[EnemyFSM] SetTargetTile: Brain이 null입니다. EnemyId={EnemyId}");
                return;
            }

            if (!Brain.IsAlive)
            {
                Debug.LogWarning($"[EnemyFSM] SetTargetTile: 사망한 유닛에게 명령을 내릴 수 없습니다. EnemyId={EnemyId}");
                return;
            }

            // 목표 타일과 전술 모드를 Brain에 기록하고 Moving 상태로 전이한다.
            Brain.TargetTileX    = x;
            Brain.TargetTileY    = y;
            Brain.CurrentRaidMode = raidMode;

            Debug.Log($"[EnemyFSM] SetTargetTile({x},{y}) mode={raidMode} → Moving 전이. EnemyId={Brain.EnemyId}");
            TransitionTo(EnemyState.Moving);
        }

        /// <summary>
        /// FactionAI가 호출하여 정찰 목표 타일을 설정하고 Scouting 상태로 전이시킨다.
        /// Scouting 완료 시 Brain.ScoutCompleted=true를 설정하고 Idle로 복귀한다.
        /// FactionAI가 다음 Tick에 ScoutCompleted 플래그를 감지하여 playerStrength 파악 완료 처리한다.
        /// </summary>
        public void SetScoutTarget(int x, int y)
        {
            if (Brain == null)
            {
                Debug.LogWarning($"[EnemyFSM] SetScoutTarget: Brain이 null입니다. EnemyId={EnemyId}");
                return;
            }

            if (!Brain.IsAlive)
            {
                Debug.LogWarning($"[EnemyFSM] SetScoutTarget: 사망한 유닛에게 명령을 내릴 수 없습니다. EnemyId={EnemyId}");
                return;
            }

            Brain.TargetTileX    = x;
            Brain.TargetTileY    = y;
            Brain.ScoutCompleted = false;
            // MoveStartTime은 TransitionTo(Scouting) → OnStateEnter에서 설정된다.

            Debug.Log($"[EnemyFSM] SetScoutTarget({x},{y}) → Scouting 전이. EnemyId={Brain.EnemyId}");
            TransitionTo(EnemyState.Scouting);
        }

        /// <summary>
        /// GameManager의 틱 루프에서 그룹별로 호출하는 메인 Tick 메서드.
        /// 현재 FSM 상태에 해당하는 상태 핸들러를 실행한다.
        ///
        /// 호출 예시 (GameManager):
        ///   if (Time.frameCount % 6 == enemyFSM.TickGroupIndex)
        ///       enemyFSM.Tick();
        /// </summary>
        public void Tick()
        {
            if (Brain == null) return;

            // Dead 상태 진입 후에는 틱을 차단하여 불필요한 처리를 방지한다.
            if (!_isTickActive) return;

            // 현재 상태 핸들러 실행
            switch (Brain.FSMState)
            {
                case EnemyState.Idle:       State_Idle();       break;
                case EnemyState.Moving:     State_Moving();     break;
                case EnemyState.Attacking:  State_Attacking();  break;
                case EnemyState.Scouting:   State_Scouting();   break;
                case EnemyState.Plundering: State_Plundering(); break;
                case EnemyState.Retreating: State_Retreating(); break;
                case EnemyState.Dead:       State_Dead();       break;
                default:
                    Debug.LogWarning($"[EnemyFSM] Tick: 처리되지 않은 상태 '{Brain.FSMState}'. EnemyId={EnemyId}");
                    break;
            }
        }

        #endregion

        #region ── 상태 핸들러 메서드 ──

        /// <summary>
        /// [Idle] FactionAI의 SetTargetTile() 호출을 대기하는 상태.
        /// 직접 탈출 조건 없음 — FactionAI가 SetTargetTile을 호출하면 Moving으로 전이한다.
        /// </summary>
        private void State_Idle()
        {
            // FactionAI가 SetTargetTile()을 호출할 때까지 대기한다.
            // AnyState 전이(Dead, Retreating)는 Update()에서 처리되므로 여기서는 대기만 한다.
        }

        /// <summary>
        /// [Moving] FlowField를 이용하여 목표 타일(0,0)로 1타일씩 이동하는 상태.
        /// Tick마다 1타일 진전 후 시각적 이동이 완료될 때까지 대기한다.
        /// Brain.TargetTileX/Y(0,0)에 도달하면 Attacking/Plundering으로 전이한다.
        /// </summary>
        private void State_Moving()
        {
            // 이전 타일 시각적 이동 완료 대기
            if (_isMoving) return;

            // 목표(0,0) 도달 확인
            if (Brain.TileX == Brain.TargetTileX && Brain.TileY == Brain.TargetTileY)
            {
                EnemyState nextState = Brain.CurrentRaidMode == RaidMode.Plunder
                    ? EnemyState.Plundering
                    : EnemyState.Attacking;
                Debug.Log($"[EnemyFSM] Moving 완료 → {nextState} 전이. " +
                          $"현재 타일=({Brain.TileX},{Brain.TileY}). EnemyId={Brain.EnemyId}");
                TransitionTo(nextState);
                return;
            }

            // FlowField에서 다음 이동 방향 샘플링
            if (FlowFieldManager.Instance == null)
            {
                Debug.LogWarning($"[EnemyFSM] FlowFieldManager.Instance가 null입니다. EnemyId={Brain.EnemyId}");
                return;
            }

            Vector2Int dir = FlowFieldManager.Instance.GetDirection(Brain.TileX, Brain.TileY);
            if (dir == Vector2Int.zero) return; // 이미 목표이거나 도달 불가

            // 논리 위치 즉시 갱신 → 시각적 이동 시작
            Brain.TileX += dir.x;
            Brain.TileY += dir.y;
            _moveTarget  = new Vector3(Brain.TileX, Brain.TileY, 0f);
            _isMoving    = true;
        }

        /// <summary>
        /// [Attacking] 주민에게 실제 피해를 입히는 전투 상태. [8단계]
        /// 매 틱 공격 범위(2타일) 내 가장 가까운 생존 주민에게 BASE_DAMAGE_PER_TICK을 적용한다.
        /// ATTACK_SIMULATE_SEC(2초) 사이클 후 Idle로 복귀하여 FactionAI의 다음 지시를 대기한다.
        /// </summary>
        private void State_Attacking()
        {
            VillagerFSM target = FindNearestVillager(ATTACK_RANGE_TILES);
            if (target != null && target.Brain != null && target.Brain.IsAlive)
            {
                target.Brain.HealthLevel = Mathf.Max(0f, target.Brain.HealthLevel - BASE_DAMAGE_PER_TICK);
                Debug.Log($"[EnemyFSM] 공격: Villager={target.AgentId} HP={target.Brain.HealthLevel:F0}. " +
                          $"EnemyId={Brain.EnemyId}");

                if (target.Brain.HealthLevel <= 0f)
                {
                    target.Brain.IsAlive = false;
                    Debug.Log($"[EnemyFSM] 주민 처치! Villager={target.AgentId}. EnemyId={Brain.EnemyId}");
                    // IsAlive=false → VillagerFSM.Update()의 AnyState 체크가 다음 프레임에
                    // TransitionTo(Dead) → EnterDead() → VillagerDied 메시지 발행을 처리한다.
                }
            }

            if (Time.time - Brain.AttackStartTime >= ATTACK_SIMULATE_SEC)
            {
                Debug.Log($"[EnemyFSM] Attacking 사이클 완료 → Idle 복귀. EnemyId={Brain.EnemyId}");
                TransitionTo(EnemyState.Idle);
            }
        }

        /// <summary>
        /// 지정 타일 거리(맨해튼) 이내에서 살아있는 주민 중 가장 가까운 VillagerFSM을 반환한다.
        /// GameManager.Instance가 없거나 범위 내 주민이 없으면 null을 반환한다.
        /// </summary>
        private VillagerFSM FindNearestVillager(float rangeTiles)
        {
            if (GameManager.Instance == null) return null;

            VillagerFSM nearest = null;
            float minDist = float.MaxValue;

            foreach (VillagerFSM v in GameManager.Instance.Villagers)
            {
                if (v == null || v.Brain == null || !v.Brain.IsAlive) continue;
                float dist = Mathf.Abs(v.Brain.TileX - Brain.TileX)
                           + Mathf.Abs(v.Brain.TileY - Brain.TileY);
                if (dist <= rangeTiles && dist < minDist)
                {
                    minDist = dist;
                    nearest = v;
                }
            }
            return nearest;
        }

        /// <summary>
        /// [Scouting] 정찰 전술 상태. MOVE_SIMULATE_SEC 경과 후 목표 타일 도달 간주.
        /// Brain.ScoutCompleted=true를 설정하고 Idle로 복귀한다.
        /// FactionAI가 다음 Tick에 ScoutCompleted를 감지하여 _playerStrengthKnown=true 처리한다.
        /// </summary>
        private void State_Scouting()
        {
            if (Time.time - Brain.MoveStartTime >= MOVE_SIMULATE_SEC)
            {
                Brain.TileX          = Brain.TargetTileX;
                Brain.TileY          = Brain.TargetTileY;
                Brain.ScoutCompleted = true;

                Debug.Log($"[EnemyFSM] 정찰 완료. 위치=({Brain.TileX},{Brain.TileY}). EnemyId={Brain.EnemyId}");
                TransitionTo(EnemyState.Idle);
            }
        }

        /// <summary>
        /// [Plundering] 약탈 전술 상태. 플레이어 기지 도착 후 WorldState.WoodStock을 즉시 차감하고
        /// Retreating 상태로 귀환한다. Retreating은 자체 HP 회복 후 Idle로 복귀한다.
        /// </summary>
        private void State_Plundering()
        {
            AuthoritativeWorldState worldState = AuthoritativeWorldState.Instance;
            if (worldState != null)
            {
                float taken = Mathf.Min(worldState.WoodStock, PLUNDER_WOOD_AMOUNT);
                worldState.WoodStock = Mathf.Max(0f, worldState.WoodStock - PLUNDER_WOOD_AMOUNT);
                Debug.Log($"[EnemyFSM] 약탈 완료. 나무 {taken:F1} 탈취. EnemyId={Brain.EnemyId}");

                // TODO: [기획 확인 필요] 탈취한 자원을 FactionAI 재고에 반영해야 하는지,
                // 반영 시 어떤 팩션 자원 항목(Copper/Silver 등)에 환산하여 넣는지 결정 필요.
                // 예: GameManager.Instance?.GetFactionAI(Brain.FactionId)?.ModifyFactionResource("Wood", taken);
            }

            Brain.CurrentRaidMode = RaidMode.Attack; // 다음 레이드 대비 초기화
            TransitionTo(EnemyState.Retreating);     // Retreating이 기지 복귀 + HP 회복을 담당
        }

        /// <summary>
        /// [Retreating] 기지로 후퇴하는 상태.
        /// HP 회복(0.5f/초)은 Update()에서 처리된다.
        /// HP가 HP_RETURN_THRESHOLD(80) 이상이 되면 Idle로 복귀한다.
        /// </summary>
        private void State_Retreating()
        {
            // [12단계] 기지에 완전히 도착한 후 HP가 충분하면 Idle로 복귀한다.
            // _isMoving이 true이면 아직 이동 중 — 도착 전 전이 방지.
            if (!_isMoving && Brain.HealthLevel >= HP_RETURN_THRESHOLD)
            {
                Debug.Log($"[EnemyFSM] Retreating 완료 (HP={Brain.HealthLevel:F1} >= {HP_RETURN_THRESHOLD}) → Idle 복귀. " +
                          $"EnemyId={Brain.EnemyId}");
                TransitionTo(EnemyState.Idle);
            }
        }

        /// <summary>
        /// [Dead] 사망 상태 핸들러.
        /// EnterDead()에서 모든 처리가 완료되므로 이후 호출은 아무 작업도 하지 않는다.
        /// </summary>
        private void State_Dead()
        {
            // Dead 상태 진입 시 EnterDead()에서 모든 정리를 수행했다.
            // _isTickActive = false이므로 일반적으로 이 핸들러에 도달하지 않는다.
        }

        #endregion

        #region ── 상태 전이 메서드 ──

        /// <summary>
        /// FSM 상태를 전이한다.
        /// 이전 상태 Exit → 새 상태 Enter 순으로 실행한다.
        /// </summary>
        private void TransitionTo(EnemyState newState)
        {
            if (Brain == null) return;

            EnemyState prevState = Brain.FSMState;

            // ── Enter 처리: 새 상태 진입 초기화 ──────────────────────────────────
            Brain.FSMState = newState;
            OnStateEnter(newState, prevState);
        }

        /// <summary>
        /// 새 상태 진입 시 초기화 작업을 수행한다.
        /// </summary>
        private void OnStateEnter(EnemyState enteringState, EnemyState fromState)
        {
            switch (enteringState)
            {
                case EnemyState.Moving:
                    Brain.MoveStartTime = Time.time;
                    break;

                case EnemyState.Scouting:
                    // Moving과 동일한 타이머 사용 — SetScoutTarget에서 중복 설정하지 않는다.
                    Brain.MoveStartTime = Time.time;
                    break;

                case EnemyState.Attacking:
                    // 공격 시작 시각 기록
                    Brain.AttackStartTime = Time.time;
                    break;

                case EnemyState.Retreating:
                    // [12단계] 즉시 텔레포트 대신 기지 방향으로 시각적 이동 시작.
                    // Brain.TileX/Y는 Update()에서 기지 도착 시 갱신된다.
                    _moveTarget            = new Vector3(Brain.BaseTileX, Brain.BaseTileY, 0f);
                    _isMoving              = true;
                    Brain.RetreatStartTime = Time.time;
                    Brain.LastHealTime     = Time.time;
                    break;

                case EnemyState.Dead:
                    EnterDead();
                    break;
            }
        }

        /// <summary>
        /// Dead 상태 진입 처리.
        /// 1회만 실행되어야 하므로 DeadProcessed 플래그로 중복 실행을 방지한다.
        ///
        /// 처리 순서:
        ///   1. IsAlive = false 확정
        ///   2. 틱 비활성화
        ///   3. 콘솔 기록
        ///   4. SensorSystem에서 자동으로 IsAlive=false 감지하여 NearEnemy 판정 제외됨
        /// </summary>
        private void EnterDead()
        {
            // 중복 실행 방지
            if (Brain.DeadProcessed) return;
            Brain.DeadProcessed = true;

            // 1. 생존 플래그 false 확정
            Brain.IsAlive = false;

            // 2. 틱 비활성화 — 이후 GameManager의 Tick() 호출을 차단한다.
            _isTickActive = false;

            // 3. 콘솔 기록
            Debug.Log($"[EnemyFSM] 사망 처리 완료. " +
                      $"EnemyId={Brain.EnemyId}, FactionId={Brain.FactionId}, " +
                      $"최후 위치=({Brain.TileX},{Brain.TileY})");

            // 4. SensorSystem은 UpdateEnemyFlag()에서 IsAlive=false를 감지하여
            //    자동으로 NearEnemy 판정에서 이 유닛을 제외한다.
            //    별도로 UnregisterEnemy()를 호출할 필요가 없다.

            // TODO: 기획팀 확인 필요 — 적 사망 시 드롭 아이템 또는 팩션 보상 처리 방식 결정 필요
        }

        #endregion

#if UNITY_EDITOR
        #region ── Unity Editor 디버그 헬퍼 ──

        /// <summary>
        /// 씬 뷰에서 적 유닛의 현재 타일 위치와 목표 타일을 시각화한다.
        /// EnemyFSM GameObject를 선택했을 때만 표시된다.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (Brain == null) return;

            // 현재 위치 (빨간 원 — 2D: XY 평면)
            UnityEditor.Handles.color = Color.red;
            UnityEditor.Handles.DrawWireDisc(
                new Vector3(Brain.TileX, Brain.TileY, 0f),
                Vector3.forward, 0.5f
            );
            UnityEditor.Handles.Label(
                new Vector3(Brain.TileX, Brain.TileY + 1f, 0f),
                $"Enemy[{Brain.FSMState}]\nHP={Brain.HealthLevel:F0}"
            );

            // 목표 타일 (노란 선)
            if (Brain.FSMState == EnemyState.Moving    || Brain.FSMState == EnemyState.Attacking
             || Brain.FSMState == EnemyState.Scouting  || Brain.FSMState == EnemyState.Plundering)
            {
                UnityEditor.Handles.color = Color.yellow;
                UnityEditor.Handles.DrawLine(
                    new Vector3(Brain.TileX, Brain.TileY, 0f),
                    new Vector3(Brain.TargetTileX, Brain.TargetTileY, 0f)
                );
            }

            // 기지 방향 (파란 선)
            if (Brain.FSMState == EnemyState.Retreating)
            {
                UnityEditor.Handles.color = Color.blue;
                UnityEditor.Handles.DrawLine(
                    new Vector3(Brain.TileX, Brain.TileY, 0f),
                    new Vector3(Brain.BaseTileX, Brain.BaseTileY, 0f)
                );
            }
        }

        #endregion
#endif
    }
}
