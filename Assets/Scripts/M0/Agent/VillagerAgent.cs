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
    /// 피해 원인 (M21-W1, ADR-M21-2) — 체력이 하나로 합쳐졌어도 **왜 깎였는가**는 갈라야 한다.
    /// 이 값이 가르는 것 둘: ①사망 시 어느 문으로 나가는가(StarveToDeath / Die — 무덤 문구·
    /// 연대기 사유가 갈린다) ②MyWasStarved를 찍을 자격이 있는가(굶주림뿐 — 전투 피해로도 찍히면
    /// 경험 우회가 전투마다 발동해 M12-G가 지키려던 희소성이 사라진다).
    /// append-only: 새 원인은 뒤에만 (세이브·연대기 사유와 결합될 자리).
    /// </summary>
    public enum DamageCause
    {
        Combat     = 0, // 전투·야수 타격·출혈
        Starvation = 1, // 굶주림
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
        public DefenseService Defense => _sim.Defense; // 방어 계획 (M22-W4 — BuildRunner 배치 조회)
        // 액션 카탈로그 (M22-4차 W4 — ClearRunner 가 짝이 되는 채집 액션의 수확량·소요를 읽는다).
        // 이미 _sim 이 갖고 있는 것을 그대로 연다 (World·Discovery 와 같은 패턴, 새 배선 0).
        public ActionCatalog Catalog => _sim.Catalog;

        /// <summary>망루 탑승 중인가 (M22-3차 W3, ADR-M22-11) — 위협 희생 선정에서 제외되는
        /// 유일한 근거 (높이). 쓰기는 ManTowerRunner의 탑승/Cleanup 두 지점뿐 — 세이브 대상
        /// 아님 (러너 상태와 한 몸, 로드 후 재계획이 다시 올린다 — ADR-M0-10).</summary>
        public bool IsOnTower { get; private set; }
        public void SetOnTower(bool on) => IsOnTower = on;

        /// <summary>지금 조리 중인 모닥불 타일 (M22-3차 W5d) — null = 조리 안 함. **표현 전용**:
        /// 소비처는 CampfireCookView 하나뿐이고 플래너·시뮬은 이 값을 읽지 않는다.
        /// 쓰기는 ConsumeRunner의 실행/Cleanup 두 지점뿐 (IsOnTower와 같은 규약 — 러너 상태와
        /// 한 몸이라 세이브 대상 아님).</summary>
        public Vector2Int? CookingAt { get; private set; }
        public void SetCookingAt(Vector2Int? tile) => CookingAt = tile;

        // 몸 스프라이트 (마커 폴백이면 자신, 실그림이면 자식 View) — 깊이 갱신 대상.
        private SpriteRenderer _viewSr;
        private SpriteRenderer _shadowSr; // 발밑 그림자 (없을 수 있다)

        /// <summary>발밑 그림자 (2026-08-09) — 팩 건물·나무는 그림자를 달고 오는데 주민만 없어서
        /// 집 앞에 서면 경계가 사라졌다 (사용자 Play 지적). 앞뒤는 정렬이 정하고, **땅에 붙어
        /// 있다**는 이것이 정한다. 크기·진하기는 에셋의 몫 — 0이면 안 만든다 (중립).</summary>
        // 그림자 장착은 SpriteViewRig 공용 (2026-08-10) — 위협도 같은 규약으로 붙는다.
        // 여기 따로 두면 "발밑"의 정의가 두 벌이 되고, 그게 바로 정렬 상수가 어긋난 경로였다.
        private void SetupShadow(AgentSpriteSetSO set) => _shadowSr = SpriteViewRig.AttachShadow(transform, set);
        // 발밑 선택 링 (PlayerInputController 소유) — 주민과 같은 깊이 띠에 매달아 둔다.
        private SpriteRenderer _selectionRing;
        public void AttachSelectionRing(SpriteRenderer ring) => _selectionRing = ring;
        public void DetachSelectionRing(SpriteRenderer ring)
        {
            if (_selectionRing == ring) _selectionRing = null;
        }

        /// <summary>깊이 갱신 (2026-08-09) — 위에서 내려다보는 2D에서 앞뒤는 **아래에 있는 쪽이 앞**.
        /// 움직이니까 매 프레임 다시 잰다 (건물은 안 움직여서 스폰 때 한 번).
        /// ⚠️ 망루에 올라가면 몸은 3.2타일 위로 올라가지만 **서 있는 자리는 망루 칸**이다 —
        /// 올라간 높이로 재면 자기가 올라선 망루 뒤로 숨는다.</summary>
        private void TickDepth()
        {
            if (_viewSr == null) return;
            float y = transform.position.y;
            if (IsOnTower && _sim != null) y -= _sim.DefenseTowerMountOffset; // 발판이 아니라 망루 칸으로
            _viewSr.sortingOrder = WorldSort.Order(y, WorldSort.Agent);
            // 그림자는 제 몸 바로 아래 — 선택 링과 같은 칸에 겹치면 링이 위 (Select > Ghost)
            // 🔮 낮/밤 축이 들어오면 **여기서** 알파를 시간대로 곱한다 (사용자 방향 2026-08-09).
            //    밤엔 옅게·한낮엔 짙게 — 기준값은 에셋(ShadowAlpha), 시간 배율만 곱하면 된다.
            //    매 프레임 도는 지점이 이미 여기 하나라 배선은 한 줄이다.
            if (_shadowSr != null) _shadowSr.sortingOrder = WorldSort.Order(y, WorldSort.Ghost);
            if (_selectionRing != null)
                _selectionRing.sortingOrder = WorldSort.Order(y, WorldSort.Select);
        }
        public FarmService Farm => _sim.Farm;
        public HomeStorageService HomeStorage => _sim.HomeStorage; // 집 저장 (M11-A — EffectApplier 창구)
        public RequestService Requests => _sim.Requests; // 부탁 (M11-F — 택지의 의뢰인 조회)
        public ThreatService Threats => _sim.Threats;   // 위협 (M11-G — 노숙 도피 방향). null = 위협 없음
        public CombatService Combat => _sim.Combat;     // 전투 판정 (M21-W4 FightRunner). null = 위협 없는 판
        public OwnershipService Ownership => _sim.Ownership; // 소유 (M11-I — 목수 자가 건축 배정)
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

        // ── 씬 지정 (M12 관측 지원) — 시작 드래프트 UI(백로그)의 UI 없는 버전 ──
        [Header("씬 지정 (비우면 랜덤 — 관측용)")]
        [Tooltip("이 주민의 성격을 고정한다. **비우면 기존 랜덤**(중립 불변식).\n" +
                 "성격이 6종인데 주민이 그보다 적어 랜덤으로는 한 판에 절반밖에 안 나온다 — " +
                 "성격별 행동 차이를 보려면 관측을 설계할 수 있어야 한다.")]
        [SerializeField] private PersonalitySO _startPersonality;

        [Tooltip("이 주민의 직업을 고정한다. **비우면 성향 편향 추첨**(M12-H).\n" +
                 "⚠️ 성격만 지정하고 직업은 비워 두는 것이 기본 관측 자세다 — " +
                 "'성향이 직업을 얼마나 끌어당기는가'가 M12-H에서 볼 것이기 때문이다.")]
        [SerializeField] private JobSO _startJob;

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

        /// <summary>내 성향 벡터 (M14-W3) — 성격 원본 + 개체 편차, 스폰 1회 고정 (ADR-M14-1:
        /// FNV-1a 결정적 = 세이브 불필요·재계산). 성향 읽기는 전부 이것을 쓴다 —
        /// PersonalitySO.Traits 직접 읽기는 '반쪽 개체'를 만든다 (명세 ⚠️W3-⑤).</summary>
        public TraitValue[] MyTraits { get; private set; }

        /// <summary>직업 (M5-A). null = 무직 — M4와 goal 선택 동일 (M5-S3). 세이브 대상 (ADR-M5-5).</summary>
        public JobSO Job { get; private set; }

        /// <summary>건설 실행 시간 배율 (M19 — 효율 전문화, ADR-M19-3). 직업 없음 = 1(중립,
        /// M5-S3). 소비처는 BuildRunner뿐 — 여기가 유도의 유일한 지점이다.</summary>
        public float BuildDurationMultOfJob()
            => Job != null ? Job.BuildDurationMult : 1f;

        /// <summary>수리 실행 시간 배율 (M22-W6, ADR-M22-7 — 목수 0.2 = 1:5). 소비처는 RepairRunner뿐.</summary>
        public float RepairDurationMultOfJob()
            => Job != null ? Job.RepairDurationMult : 1f;

        /// <summary>지금 하는 일에 걸린 직업 효율 배율 (M20-W10 — 표기 전용).
        /// 러너가 없으면(계획 중·쉬는 중) 1. **HUD는 이 값만 읽는다** — 액션 타입으로 다시
        /// 유도하면 판정이 이원화된다.</summary>
        public float CurrentActionDurationMult => _runner != null ? _runner.DurationMult : 1f;

        /// <summary>밭일 실행 시간 배율 (M20). 소비처는 FarmRunner뿐.</summary>
        public float FarmDurationMultOfJob()
            => Job != null ? Job.FarmDurationMult : 1f;

        /// <summary>조리 실행 시간 배율 (M20). 소비처는 ConsumeRunner의 IsCookingWork 분기뿐 —
        /// 식사·휴식에는 절대 닿지 않는다 (ADR-M5-3·ADR-M20-3).</summary>
        public float CookDurationMultOfJob()
            => Job != null ? Job.CookDurationMult : 1f;

        /// <summary>자원별 채집 실행 시간 배율 (M20). 소비처는 GatherRunner뿐.</summary>
        public float GatherDurationMultOfJob(ResourceType resource)
            => Job != null ? Job.GatherDurationMultFor(resource) : 1f;

        /// <summary>전투 타격 간격 배율 (M21-W8). 소비처는 FightRunner.Interval뿐 —
        /// 곱 결합·클램프는 CombatService.HitInterval(순수)이 한다. 무직 = 1 (중립).</summary>
        public float CombatDurationMultOfJob()
            => Job != null ? Job.CombatDurationMult : 1f;

        // goal 실효 우선순위 보정 (M5-B 직업 + M6 후속 성격 합산, ADR-M5-1) — 스폰 1회 캐시.
        // 둘 다 null이면 null = Select가 기존과 완전 동일 경로 (중립 불변식).
        private System.Func<GoalSO, int> _goalBias;

        /// <summary>
        /// 직업+성격 goal 보정 결합의 유일한 지점 (순수 — 게이트 M6-T5). 둘 다 null = null (중립).
        /// 성격 보정이 위기 대응을 가른다: 같은 트리거를 봐도 실효 우선순위가 달라
        /// 돌입 시점·참여 여부가 성격따라 흩어진다 (2026-07-17 "동시 돌입 = AI티" 대응).
        /// </summary>
        /// <summary>호환 진입점 (게이트·구형 호출 전용 — 벡터 = 성격 원본, 편차 없음).
        /// 런타임은 반드시 3인자(MyTraits)를 쓴다 (⚠️W3-⑤ 반쪽 개체 방지).</summary>
        public static System.Func<GoalSO, int> BuildGoalBias(JobSO job, PersonalitySO personality)
            => BuildGoalBias(job, personality, personality != null ? personality.Traits : null);

        public static System.Func<GoalSO, int> BuildGoalBias(JobSO job, PersonalitySO personality,
                                                             TraitValue[] traits)
        {
            if (job == null && personality == null) return null;
            if (personality == null) return job.BoostFor;
            // 성격 = 舊 나열(GoalBoosts) + 新 벡터(TraitWeights). M12-F에서 나열을 비우면
            // 벡터만 남는다 — 합산 지점은 이 함수 하나뿐이다 (ADR-M12-5).
            // 벡터는 인자(M14-W3 개체 편차 포함 MyTraits)로 받는다 — SO 직접 읽기는 반쪽 개체 (⚠️W3-⑤).
            if (job == null) return g => personality.BoostFor(g) + g.TraitBoost(traits);
            return g => job.BoostFor(g) + personality.BoostFor(g) + g.TraitBoost(traits);
        }

        // 직업 일과 goal (M5-C, ADR-M5-2) — 개인 사다리 주입, 씬 _goals에 넣지 않는다.
        // 무직·일과 없는 직업이면 null = 기존 여가 동작.
        private GoalSO _routine;

        /// <summary>실효 우선순위 = 에셋 Priority + 직업 보정. 선택(Select)과 전환 비교가
        /// 같은 진리를 쓰기 위한 유일한 계산 지점 (ADR-M5-6).
        /// 명령은 보정 없음 (리뷰① 2026-08-08 — GoalSelector.Consider의 ordered 규약과 짝:
        /// 선택은 무보정인데 전환 비교만 보정이면, 뽑힌 명령이 다음 재선택 검사에서
        /// 즉시 하위 goal에 밀려나는 어긋남이 생긴다).</summary>
        private int EffectivePriority(GoalSO g)
            => g.Priority + (g == _order || _goalBias == null ? 0 : _goalBias(g));

        /// <summary>
        /// 이 goal을 특정 축이 얼마나 원하는가 (M12-J 계측 분류용, 순수).
        /// 축을 +100으로 세운 단위 벡터를 넣어 가중치의 부호·크기를 그대로 읽는다 —
        /// goal 이름이 아니라 **데이터가 분류를 결정**한다.
        /// </summary>
        private static float GoalWants(GoalSO goal, TraitId axis)
            => TraitVector.Bias(new[] { new TraitValue { Trait = axis, Value = 100 } }, goal.TraitWeights);

        /// <summary>
        /// 내 위협 감지 반경 배율 (M10-D 성격 필드 × M12-D 성향 편향 — M12-F까지 병존).
        /// 같은 위협을 봐도 겁 많은 주민이 먼저 알아챈다. 성향 가중치가 비면 Threshold가 1을
        /// 그대로 돌려주므로 현행 동작과 완전히 같다 (중립 불변식).
        /// </summary>
        private float FleeRadius()
            => (Personality != null ? Personality.FleeRadiusMult : 1f)
               * TraitVector.Threshold(MyTraits, _cfg.FleeRadiusBias, 1f); // 개체 편차 포함 (M14-W3)

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

        // 계측 전용 (M14-W2) — 직전 채택 goal. [GoalPick] 로그의 연속 중복 제거용, 판정에 안 쓴다.
        private GoalSO _lastPickedGoal;

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
                // 미지급 약속은 에스크로 반환 (ADR-M6-5 원형 — M19-W4에서 화폐 분기 철거)
                if (_sim != null)
                    World.AddStock(_promisedReward.CostSlot, _promisedReward.CostAmount);
                _promisedReward = null;
            }
            if (_order != null && _orderIsRuntimeClone) Destroy(_order);
            _order = null;
            _orderIsRuntimeClone = false;
            OrderTargetNode = null;
        }

        /// <summary>보상 지급 — 명령 완수 지점 전용 (ADR-M6-5). 지급 후 null 처리로 반환 경로 차단.
        /// M19-W4: 실물 원형 복원 — 화폐 웃돈(M16-W3 발행 → M17-W1 금고 지출)은 철거됐다.</summary>
        private void PayReward()
        {
            if (_promisedReward == null) return;
            ApplyNeedEffect(SlotId.MySatiety, EffectOp.Add, _promisedReward.SatietyGain);
            Debug.Log($"[VillagerAgent] {AgentId}: 보상 지급 — {_promisedReward.DisplayName} " +
                      $"(포만 +{_promisedReward.SatietyGain}, 비용은 수락 시 차감 완료)");
            ShowTransient(Pick(_promisedReward.PayLines));
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
            Hp = _cfg.MaxHp; // M21-W1 — 체력만 편차 없이 만복 시작 (몸값 불가침: 누구는 태생이 약한 설계 아님)
            // 감쇠율 개체 편차 — 동기화(포만 0 클램프·같은 문턱 식사)가 생겨도 되돌리는 상시 힘.
            // 편차가 없으면 감쇠율이 전원 동일이라 한 번 뭉친 웨이브가 영구 지속된다.
            _decayJitter = 1f + StableHash.Spread(AgentId, "decay") * _cfg.SatietyDecayVariancePct;

            // 성격 할당 (M4-A) — 스폰 1회 고정. 배율 편차 ±10%도 이때 확정 (정체성 — 세이브 대상, ADR-M4-5)
            // 런타임 스폰(M10-E)은 Preset 주입값이 랜덤을 대체 — UI가 보여준 그 사람이 온다 (⚠️①).
            // 우선순위: 스폰 주입(방랑자 — null도 값이다) > 씬 지정(인스펙터) > 랜덤.
            // 인스펙터 필드는 **비면 랜덤**이라 주입과 의미가 다르다 — 둘을 한 플래그로 묶지 말 것.
            Personality = PresetOrRandom(_hasPreset, _presetPersonality,
                () => _startPersonality != null ? _startPersonality : _sim.PickRandomPersonality());
            // 개체 편차 벡터 (M14-W3) — 성격 확정 직후·직업 픽 **이전** (직업 편향도 '이 사람'을
            // 따라야 한다). 진폭은 TraitRules 에셋의 몫 — 0 또는 미배선이면 원본 그대로 (중립 불변식).
            int traitAmp = _sim.WorldConfig != null && _sim.WorldConfig.TraitRules != null
                ? _sim.WorldConfig.TraitRules.TraitJitterAmp : 0;
            MyTraits = TraitVector.Jitter(Personality != null ? Personality.Traits : null,
                                          AgentId, traitAmp);
            // 직업 할당 (M5-A → M12-H) — 이제 성격과 독립이 아니라 성향이 편향시킨다.
            // 개체 편차가 위에서 먼저 확정되므로 그 벡터를 그대로 넘긴다. 스폰 1회 고정 (세이브 대상, ADR-M5-5)
            Job = PresetOrRandom(_hasPreset, _presetJob,
                () => _startJob != null ? _startJob : _sim.PickJobFor(MyTraits));
            _multJitter = new[]
            {
                Random.Range(0.9f, 1.1f), Random.Range(0.9f, 1.1f),
                Random.Range(0.9f, 1.1f), Random.Range(0.9f, 1.1f),
            };
            Debug.Log($"[VillagerAgent] {AgentId}: 성격 = {(Personality != null ? Personality.DisplayName : "없음(중립)")}"
                      + $" / 직업 = {(Job != null ? Job.DisplayName : "무직(공용)")}");
            // 편차 요약 계측 (ADR-M13-1 — 지터는 로그에 보여야 존재한다). amp 0이면 침묵 = 기존 로그 불변.
            if (traitAmp > 0 && Personality != null && MyTraits != null)
                Debug.Log($"[VillagerAgent] {AgentId}: 성향 편차 = {TraitDeltaSummary(Personality.Traits, MyTraits)}");
            _goalBias = BuildGoalBias(Job, Personality, MyTraits); // 직업+성격 합산 (M6 후속, ADR-M5-6 동일 인자 유지)
            _routine = Job != null ? Job.RoutineGoal : null;
            // 배율 배열 1회 캐시 (M4-B) — 성격·직업 둘 다 null이면 null = 중립 (RequestPlan이 무시)
            _costMult = PersonalityCost.Build(_sim.Catalog, Personality, MyTraits, Job, _multJitter,
                                             _sim.WorldConfig != null ? _sim.WorldConfig.TraitRules : null);
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
            // 행동 몸짓 (M23-W1) — 어느 몸짓인지는 실행 중 액션의 에셋 필드가 정한다 (ADR-M23-1).
            // 대화 중엔 끈다 (마주보며 도끼질하는 그림 방지).
            // M22-4차: 러너가 이번 실행의 몸짓을 지목했으면 그것이 이긴다 (개간 = 나무면 도끼·
            // 돌이면 곡괭이). 지목값의 출처도 **에셋**이다 — 코드는 어느 에셋을 볼지만 안다.
            AnimKind acting = State == AgentState.Acting && !chatting && !SuppressActionGesture
                && _plan != null && _planIndex >= 0 && _planIndex < _plan.Count
                ? (_runner != null && _runner.AnimOverride != AnimKind.None
                   ? _runner.AnimOverride : _plan[_planIndex].Anim)
                : AnimKind.None;
            _animator?.Tick(Time.deltaTime,
                State == AgentState.Moving && _hasNextReserved && !chatting, _lastDir, acting);
            TickDepth();
            TickFlash();

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

            // 아사 (M6-D → ADR-M10-3 → M21-W1 체력 환산) — 계절 분기 없음: 굶주림은 계절 무관 사실 (⚠️③).
            // 대기 가드 금지 (M4 교훈 — 동상·데드락) — 시계는 누적 시간 하나뿐.
            // 검증된 시계는 그대로 돌리고 **증분만** 피해로 환산한다: 시계를 지우고 새 판정을
            // 만들면 0.5일이라는 검증된 아사 소요가 조용히 어긋난다 (M17 수치의 짝).
            float prevStarvingDays = _starvingDays;
            _starvingDays = NextStarvingDays(_starvingDays, Satiety, _cfg.StarvingBelowSatiety, deltaGameDays);
            float starvingDelta = _starvingDays - prevStarvingDays;
            if (starvingDelta > 0f)
                TakeDamage(_cfg.StarveHpPerDay * starvingDelta, DamageCause.Starvation);
            else if (Injury == InjurySeverity.None)
                RecoverHp(_cfg.SatedHpRegen * deltaGameDays); // 먹고 지내면 되돌아온다 (부상 중엔 간호가 맡는다)
            if (State == AgentState.Dead) return;

            // 부상 계단 v2 (M11-I 간호 2단계) — 굶주림과 같은 패턴, 원인·결말은 분리 (ADR-M10-3).
            // 치료 간호(Medic)만 회복을 올린다. 일반 간호(응급조치)는 최초 1회 안정화(유예 부여)뿐 —
            // 유예가 지나면 악화 재개 → 치료사 없으면 사망 (crowding 자연 해소, 결정 15).
            if (Injury != InjurySeverity.None)
            {
                bool tended = Time.time < _tendedUntil;
                bool byHealer = tended && _tendedByHealer;
                bool byHelper = tended && !_tendedByHealer;
                float prevNeglectDays = _injuryNeglectDays;
                (_injuryRecovery, _injuryNeglectDays, _stabilized, _graceLeft) = NextInjuryStateV2(
                    _injuryRecovery, _injuryNeglectDays, _stabilized, _graceLeft,
                    byHealer, byHelper, _tendMult, _cfg.StabilizeGraceDays, deltaGameDays);
                // 출혈 = 방치 시계의 증분 (M21-W1). 간호 중·안정화 유예 중에는 방치가 멈추므로
                // 출혈도 함께 멎는다 — 그 우선순위 규칙(결정 15)을 여기에 복제하지 않는 이유다.
                float neglectDelta = _injuryNeglectDays - prevNeglectDays;
                if (neglectDelta > 0f)
                {
                    TakeDamage(_cfg.BleedHpPerDay * neglectDelta, DamageCause.Combat);
                    if (State == AgentState.Dead) return;
                }
                if (_injuryRecovery >= _cfg.InjuryRecoverDays) HealInjury();
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
            bool hasCampfire = _sim.Ownership.TryGetOwned(AgentId, SlotId.CampfireCount, out _); // M11-K
            return World.BuildSnapshot(Mathf.RoundToInt(Satiety), Mathf.RoundToInt(Fatigue),
                hasHome, // MyHasHome (M8-C)
                _sim.Threats != null && _sim.Threats.IsNearThreat(TileX, TileY,
                    FleeRadius()),                                             // ThreatNear (M10-D + M12-D)
                MyRaw, MyCooked, homeRaw, homeCooked,                          // 개인 인벤토리 (M11-A)
                MyWasAttacked,                                                 // 피격 경험 (M11-G)
                Farm != null ? Farm.CountPlotsOf(AgentId) : 0,                 // 개인 밭 (M11-E)
                Farm != null ? Farm.CountEmptyOf(AgentId) : 0,
                Farm != null ? Farm.CountRipeOf(AgentId) : 0,
                hasCampfire,                                                   // 내 모닥불 (M11-K)
                MyWasStarved,                                                  // 아사 직전 경험 (M12-G)
                MyHasWeapon);                                                  // 무기 소유 (M21-W5)
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

        // ── 체력 (M21-W1 — ADR-M21-2: 생존 단일 척도) ─────────────────────────
        //
        // 굶주림도 전투도 여기서 깎인다. 舊 구조는 사망 시계가 둘(_starvingDays·_injuryNeglectDays)
        // 이었고 서로를 몰랐다 — 굶주린 사람이 물려도 두 시계가 따로 돌아 "쇠약해진 사람이 먼저
        // 죽는다"가 표현되지 않았다. 이제 눈금 하나를 공유한다.
        //
        // ⚠️ 시계를 지운 게 아니라 **판정을 옮겼다**: NextStarvingDays·NextInjuryStateV2는 그대로
        // 돌고(게이트도 그대로), 그 **증분**이 피해로 환산된다. "언제 시계가 도는가"의 규칙을
        // 두 벌 두지 않기 위해서다 — 간호·안정화 유예의 우선순위(M11-I 결정 15)를 출혈 쪽에
        // 복제했다면 두 규칙이 언젠가 갈렸을 것이다.

        /// <summary>현재 체력. 세이브 대상 (ADR-M0-10). 쓰기는 TakeDamage/RecoverHp 둘뿐.</summary>
        public float Hp { get; private set; }

        /// <summary>사망 판정 (순수 — 게이트 M21-T1). 문턱이 0인 유일한 판정이다.</summary>
        public static bool IsDead(float hp) => hp <= 0f;

        /// <summary>
        /// "죽을 뻔했다" 판정 (순수 — 게이트 M21-T1. 舊 IsNearStarvation의 HP판).
        /// 舊 판정은 굶주림 **누적일**을 봤는데, 체력이 합쳐진 뒤로는 그 값이 죽음과의 거리를
        /// 더는 말해주지 못한다 — 이미 다쳐서 체력 30인 사람은 0.15일 만에 죽으므로 舊 문턱
        /// (0.4일)에 영영 닿지 못하고, **가장 아슬아슬했던 사람이 기록에서 빠진다.**
        /// 기준선은 기존 에셋 값을 그대로 쓴다: NearStarvationRatio 0.8 = 남은 체력 20% 지점
        /// (만복에서 굶기 시작하면 舊 0.4일 지점과 정확히 같다 — 환산이지 새 수치가 아니다).
        /// </summary>
        public static bool IsNearDeath(float hp, AgentConfigSO cfg)
            => hp <= cfg.MaxHp * (1f - cfg.NearStarvationRatio);

        /// <summary>MyWasStarved 를 찍을 자격 (순수 — 게이트 M21-T15, M12-G 희소성의 판정).
        /// 원인이 **굶주림**이고 죽음 문턱 근처일 때만 — 전투 피해로도 찍히면 경험 우회가
        /// 전투마다 발동해 "굶어 죽을 뻔한 기억"의 희소성이 사라진다 (DamageCause 주석 ②).</summary>
        public static bool ShouldMarkStarved(DamageCause cause, float hp, AgentConfigSO cfg)
            => cause == DamageCause.Starvation && IsNearDeath(hp, cfg);

        /// <summary>아사 소요 (순수 — 게이트 M21-T1): 만복에서 굶기만 할 때 죽기까지의 게임일.
        /// 이 값이 DepartAfterStarvingDays와 어긋나면 검증된 밸런스가 조용히 바뀐 것이다.</summary>
        public static float DaysToStarveDeath(AgentConfigSO cfg) => cfg.MaxHp / cfg.StarveHpPerDay;

        /// <summary>부상 방치 사망 소요 (순수 — 게이트 M21-T1): 부상 진입 체력에서 출혈만으로
        /// 죽기까지의 게임일. InjuryDeathAfterDays와 일치해야 한다.</summary>
        public static float DaysToBleedDeath(AgentConfigSO cfg)
            => cfg.InjuredBelowHp / cfg.BleedHpPerDay;

        /// <summary>
        /// 피해의 유일한 문 (ADR-M21-2) — 체력 차감·경험 기록·사망 라우팅이 전부 여기 모인다.
        /// 밖에서 Hp를 직접 만지지 않는다: 두 번째 쓰기 경로가 생기는 순간 "누가 죽였는가"를
        /// 잃는다 (ADR-M0-3 상태 쓰기 단일 지점).
        /// 원인(cause)이 사망 문을 가른다 — 무덤·연대기 사유·마지막 대사가 원인별로 다르다.
        /// </summary>
        public void TakeDamage(float amount, DamageCause cause)
        {
            if (State == AgentState.Dead || amount <= 0f) return;
            Hp = Mathf.Max(0f, Hp - amount);
            // 맞았다 (표현 전용, 2026-08-10). 전투 피해에만 — 굶주림은 서서히 닳는 것이라
            // 번쩍이면 "누가 때렸나"로 읽힌다.
            if (cause == DamageCause.Combat) PlayHurt();
            // 굶주림 원인일 때만 기록 (M12-G 희소성 — 판정은 순수 함수, 게이트 M21-T15).
            // 여기가 MyWasStarved의 유일한 쓰기 지점이다 (舊 SimTick에서 이전).
            if (ShouldMarkStarved(cause, Hp, _cfg)) MyWasStarved = true;
            if (!IsDead(Hp))
            {
                // 부상 파생 (M21-W2) — 전투 피해로 부상선 아래면 다친 것이다.
                // 舊 구조는 정반대였다: Injure가 진입 피해를 줬다. 그래서 위협이 "얼마나 세게
                // 쳤는가"를 말할 수 없었고 한 대든 열 대든 결과가 같았다 — 체류형에서는 그 구조가
                // 곧 무적이다. 중복 진입은 CanInjure가 거르므로 이미 다친 사람은 체력만 깎인다.
                if (cause == DamageCause.Combat && Hp < _cfg.InjuredBelowHp) Injure(InjurySeverity.Light);
                return;
            }
            if (cause == DamageCause.Starvation) StarveToDeath();
            else Die();
        }

        /// <summary>체력 회복 (M21-W1) — 굶주리지 않고 다치지도 않은 사람만. 부상 중 회복은
        /// 간호의 몫이라 여기로 오지 않는다 (전투 피해가 시간만으로 지워지면 위협이 다시
        /// 날씨가 된다 — ADR-M21-2).</summary>
        private void RecoverHp(float amount)
        {
            if (State == AgentState.Dead || amount <= 0f) return;
            Hp = Mathf.Min(_cfg.MaxHp, Hp + amount);
        }

        // ── 부상·사망 (M10-A — 최초의 사망 축. ADR-M10-2: 쓰기는 Injure/HealInjury/SimTick뿐) ──

        /// <summary>부상 상태. 세이브 대상 (ADR-M0-10 — Severity·회복·방치 누적 3종 함께).</summary>
        public InjurySeverity Injury { get; private set; }

        private float _injuryRecovery;    // 치료 간호 누적 (게임일) — InjuryRecoverDays 도달 시 완치. 세이브 대상
        private float _injuryNeglectDays; // 미간호 누적 (게임일) — InjuryDeathAfterDays 도달 시 사망. 세이브 대상
        private float _tendedUntil;       // 간호 유효 시각 (Time.time) — 세이브 안 함 (로드 후 간호 재개로 재유도)
        private float _tendMult = 1f;     // 현재 간호자 회복 배율 (Job.TendRecoveryMult)
        private bool  _tendedByHealer;    // 이번 간호가 치료사(완치 가능)인가 — MarkTended가 갱신
        private bool  _stabilized;        // 응급조치 1회 완료 (M11-I) — 세이브 대상. 유예를 부여받은 상태
        private float _graceLeft;         // 안정화 유예 잔량 (게임일) — 세이브 대상. 0이 되면 악화 재개

        /// <summary>부상 진입 가능 판정 (순수 — 게이트 M10-T1): Dead·중복 부상 무시 (M10은 단일 심각도).</summary>
        public static bool CanInjure(AgentState state, InjurySeverity current)
            => state != AgentState.Dead && current == InjurySeverity.None;

        /// <summary>
        /// 부상 진입의 유일한 문 (ADR-M10-2) — 호출처는 TakeDamage의 파생뿐 (M21-W2. 舊 M10-C에서는
        /// ThreatService.ExecuteStrike가 직접 불렀다: 이제 위협은 피해만 주고 부상은 체력이 정한다).
        /// 하던 일은 중단 (실패 아님 — 쿨다운 없음), 이후 goal 후보는 AllowedWhenInjured로 좁혀진다.
        /// </summary>
        public void Injure(InjurySeverity severity)
        {
            if (!CanInjure(State, Injury) || severity == InjurySeverity.None) return;
            Injury = severity;
            _injuryRecovery = 0f;
            _injuryNeglectDays = 0f;
            _tendedUntil = 0f;
            _tendedByHealer = false;
            _stabilized = false;
            _graceLeft = 0f;
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
            // 피격 경험은 영구다 (M11-G) — 회복해도 잊지 않는다. "노숙자가 위협에 물리고
            // 나서야 집을 원한다"는 서사의 실체 (결정 6). 세이브 대상 (ADR-M11-10).
            MyWasAttacked = true;
            _sim.Chronicle.RecordEvent(AgentId, EventId.Injured, _sim.GameTime); // 연대기 (M13-C2)
            ShowTransient(Pick(_cfg.InjuredLines));
            Debug.LogWarning($"[Injury] {AgentId}: 부상 ({severity})");
            _sim.Hud?.Notify($"{ShortName}이(가) 다쳤습니다");
            // 진입 피해 없음 (M21-W2 — W1의 임시 구조를 여기서 뒤집었다): 체력을 깎는 것은
            // 때린 쪽(ThreatSO.StrikeDamage)이고, 부상은 그 결과로 **파생**된다. 여기서 또 깎으면
            // 한 대에 두 번 맞는다.
        }

        /// <summary>간호 표시 (TendRunner 전용, M11-I) — 유효 시간 동안 간호 인정. isHealer가
        /// 치료(회복 진행)와 안정화(1회성 유예)를 가른다. 부상 상태 자체는 쓰지 않는다
        /// (ADR-M10-2 — 진행은 본인 SimTick만). 같은 틱 복수 간호 시 마지막 호출이 이긴다.</summary>
        public void MarkTended(float untilSec, float recoveryMult, bool isHealer)
        {
            _tendedUntil = untilSec;
            _tendMult = Mathf.Max(1f, recoveryMult);
            _tendedByHealer = isHealer;
        }

        /// <summary>간호받는 중인가 — FindNearestInjured의 미간호 우선 판정용 (M10-B).</summary>
        public bool IsTended => Time.time < _tendedUntil;

        /// <summary>응급조치를 받아 안정화됐는가 (M11-I) — 일반 간호자의 대상 제외 판정.
        /// 안정화된 부상자는 UntendedInjuredCount에서 빠져 군중이 해산한다 (crowding 해소).</summary>
        public bool IsStabilized => _stabilized;

        /// <summary>최근접 부상자 조회 (TendRunner 전용) — healer는 전 부상자, 일반은 미안정 부상자만
        /// (M11-I 이원화). 안정화 완료 대상에 일반 간호자가 계속 붙는 crowding을 여기서 끊는다.</summary>
        public VillagerAgent FindNearestInjured(bool healerMode) => _sim.FindNearestInjured(this, healerMode);

        /// <summary>
        /// 부상 계단 갱신 (순수 — 게이트 M10-T1): 간호 중 = 회복 진행·방치 정지(홀드 — 리셋 아님,
        /// 스치는 간호로 사망 시계가 초기화되면 사망 불가능 — 명세 ⚠️②), 방치 = 방치 누적·회복 정지.
        /// </summary>
        public static (float recovery, float neglect) NextInjuryState(
            float recovery, float neglect, bool tended, float tendMult, float deltaGameDays)
            => tended ? (recovery + deltaGameDays * tendMult, neglect)
                      : (recovery, neglect + deltaGameDays);

        /// <summary>
        /// 부상 계단 v2 (순수 — 게이트 M11-T8, 결정 15). 우선순위가 곧 설계다:
        /// ① 치료 간호 = 회복 진행 (완치의 유일한 경로 — Medic만).
        /// ② 일반 간호 + 미안정 = 최초 1회 안정화 (유예 부여, 회복 없음). 이미 안정화면 무효과
        ///    (교대 간호로 영원히 못 살린다 — crowding 재발·Medic 무가치 방지, ⚠️①).
        /// ③ 유예 잔량 > 0 = 유예 소모 (방치 정지).
        /// ④ 그 외 = 악화 누적 → 사망. (간호 없고 유예도 소진 = 기존 방치와 동일)
        /// </summary>
        public static (float recovery, float neglect, bool stabilized, float grace) NextInjuryStateV2(
            float recovery, float neglect, bool stabilized, float grace,
            bool tendedByHealer, bool tendedByHelper, float tendMult, float graceDays, float dt)
        {
            if (tendedByHealer) return (recovery + dt * tendMult, neglect, stabilized, grace);
            if (tendedByHelper && !stabilized) return (recovery, neglect, true, graceDays);
            // 유예는 0 아래로 내려가지 않는다 (불변식) — Max로 클램프. 마지막 부분 틱은 방치 정지지만
            // 다음 틱 grace==0이라 악화가 재개된다 (오버슈트는 최대 dt 1틱, 무해).
            if (grace > 0f) return (recovery, neglect, stabilized, Mathf.Max(0f, grace - dt));
            return (recovery, neglect + dt, stabilized, grace);
        }

        // (M21-W1: 舊 ShouldDie(방치 일수 문턱)는 삭제 — 사망 판정은 IsDead(Hp) 하나로 합쳐졌다.
        //  방치 시계 자체는 살아 있고 그 증분이 출혈이 된다. 폐기=삭제 ADR-M0-4)

        /// <summary>완치 — 부상 소멸의 유일한 지점. 감속·goal 필터가 같은 틱에 해제된다.
        /// 체력은 여기서 되돌리지 않는다 (M21-W1) — 나은 직후엔 아직 쇠약하고, 먹고 지내는
        /// 동안 RecoverHp가 채운다. 즉시 만복 회복은 "간호로 살아났다"를 공짜로 만든다.</summary>
        private void HealInjury()
        {
            // 연대기 (M13-C2) — 치료 완료는 알림 누락 5건 중 유일한 🔴였다: 개입해서
            // 살려내도 화면에 안 떠 개입에 보상이 없었다. 이제 기록으로 남는다.
            _sim.Chronicle.RecordEvent(AgentId, EventId.Healed, _sim.GameTime);
            Debug.Log($"[Injury] {AgentId}: 회복 — 간호 누적 {_injuryRecovery:F2}일");
            Injury = InjurySeverity.None;
            _injuryRecovery = 0f;
            _injuryNeglectDays = 0f;
            _tendedUntil = 0f;
            _tendMult = 1f;
            _tendedByHealer = false;
            _stabilized = false;
            _graceLeft = 0f;
        }

        /// <summary>
        /// 부상 사망 (M10-A) — 이탈(Depart)과 동일 경로 재사용 (ADR-M6-3): State=Dead + 지연 파괴,
        /// 클레임·타일·명령·보상 정리는 전부 OnDestroy 단일 경로. 무덤·카운터는 RecordDeath (표현+기록).
        /// 굶주림은 여기가 아니라 StarveToDeath로 — 문(RecordDeath)은 같고 원인·대사만 분리
        /// (ADR-M10-3 개정 2026-07-24: 굶주림의 결말은 이탈이 아니라 아사다. 舊 주석
        /// "이탈=굶주림"은 개정을 안 따라간 문언 불일치였다 — 2026-07-27 곁가지 적재분 정정).
        /// </summary>
        private void Die()
        {
            Debug.LogWarning($"[VillagerAgent] {AgentId}: 부상 사망 — 체력 0 " +
                             $"(방치 {_injuryNeglectDays:F2}일 / 출혈 {_cfg.BleedHpPerDay:F1}/일)");
            _sim.Hud?.Notify($"{ShortName}이(가) 숨을 거뒀습니다");
            _sim.RecordDeath(TileX, TileY, ShortName, (int)_sim.GameTime, AgentId);
            // 명부 퇴장 기록 (M13-C1) — 사유를 아는 곳에서만 쓴다 (ADR-M13-3:
            // 호출처는 여기와 StarveToDeath 둘뿐. UnregisterAgent로 옮기면 C3 증분이 무너진다).
            // M21-W9: Injury → Combat 개정 — 이 문은 전투 피해(즉사·출혈 방치)로만 열린다
            // (TakeDamage의 Combat 분기가 유일한 호출자). "부상으로"가 아니라 "짐승에게"가 이야기다.
            _sim.Chronicle.RecordExit(AgentId, _sim.GameTime, ExitCause.Combat);
            SnapshotRelationsForChronicle(); // C3 — ReleaseBy(OnDestroy)가 지우기 전에
            // (M19-W4: 지갑 소멸 회계(M16-W1)는 화폐와 함께 철거)
            ShowTransient(Pick(_cfg.DieLines));
            State = AgentState.Dead;      // SimTick 차단 — Depart와 동일 (새 상태 추가 금지)
            Destroy(gameObject, _cfg.TransientLineSec);
        }

        /// <summary>퇴장 관계 스냅샷 (M13-C3) — 죽는 순간의 단짝·원한을 명부에 남긴다.
        /// M13의 유일한 수술 대상이던 ReleaseBy는 결국 무수정 — 지우기 **전에** 찍는 것으로
        /// 충분했다 (산 자의 사전에서 죽은 자가 지워지는 것은 그대로 옳다).
        /// 판정은 정보줄과 같은 TryGetExtreme·같은 문턱 에셋 (판단 규칙 이원화 금지).</summary>
        private void SnapshotRelationsForChronicle()
        {
            RelationshipService rel = _sim.Relationship;
            WorldConfigSO cfg = _sim.WorldConfig;
            if (rel == null || cfg == null) return;
            rel.TryGetExtreme(AgentId, buddy: true, cfg.BuddyThreshold, out string buddy);
            rel.TryGetExtreme(AgentId, buddy: false, cfg.GrudgeThreshold, out string grudge);
            _sim.Chronicle.SnapshotRelations(AgentId, buddy, grudge);
        }

        /// <summary>부상 goal 필터 (순수 — 게이트 M10-T1): 부상 중엔 AllowedWhenInjured goal만 후보.
        /// None이면 항상 false = 기존 Select와 완전 동일 (중립 불변식).</summary>
        public static bool BlockedByInjury(GoalSO goal, InjurySeverity injury)
            => injury != InjurySeverity.None && goal != null && !goal.AllowedWhenInjured;

        /// <summary>쿨다운+부상 합성 필터 — Select의 skip 델리게이트 (두 호출부가 같은 진리를 쓴다).</summary>
        private bool IsGoalExcluded(GoalSO goal)
            => IsGoalCoolingDown(goal) || BlockedByInjury(goal, Injury) || BlockedByJob(goal, Job);

        /// <summary>직업 게이트 (순수 — 게이트 M11-T8, ADR-M11-6). RequiredJob null이면 항상 통과
        /// (중립 불변식 — 기존 Select와 동일). 설정되면 그 직업이 아닌 주민의 후보에서 제외한다
        /// (Prepare 실패가 아니라 후보 제외 — 실패 쿨다운 공회전 방지, 명세 ⚠️②).</summary>
        public static bool BlockedByJob(GoalSO goal, JobSO job)
            => goal != null && goal.RequiredJob != null && job != goal.RequiredJob;

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

        // (M21-W1: 舊 ShouldStarveToDeath·IsNearStarvation은 삭제 — 아사 판정은 IsDead(Hp),
        //  "죽을 뻔했다"는 IsNearDeath(Hp)로 이관됐다. 굶주림 누적일은 여전히 시계로 돌지만
        //  더는 죽음과의 거리를 말하지 못한다: 이미 다친 사람은 그 문턱에 닿기 전에 죽는다.
        //  에셋 값(DepartAfterStarvingDays·NearStarvationRatio)은 환산의 기준으로 그대로 쓰인다.)

        /// <summary>상태 경보 등급 (M13-B 개정 2026-08-07). 저장 식량이 아니라 **몸 상태**다.</summary>
        public enum HungerLevel
        {
            None = 0,     // 평시 — 경보 없음
            Hungry = 1,   // 배고픔 — 명령을 거부하기 시작하는 구간. 아직 손쓸 시간이 있다
            Starving = 2, // 굶주림 — _starvingDays 시계가 돌고 체력이 깎이는 중
        }

        /// <summary>
        /// 상태 경보 판정 (순수 — 게이트 M13-T6). 기준이 "저장 식량 0일치"에서 실제 굶주림으로
        /// 옮겨졌다 (2026-08-07 Play 관측): 시작 시점엔 아무도 비축이 없어 Day 0 화면이 빨간 줄
        /// 8개로 시작했다. 전원이 항상 빨간 경보는 경보가 아니라 배경이고, 배경은 개입을 못 만든다.
        ///
        /// 문턱은 둘 다 기존 에셋 값 그대로 재사용한다 (새 수치 발명 금지 — ADR-M0-2):
        ///   Hungry   = OrderRefuseSatiety   — 이 밑에선 명령이 거부된다. 촌장이 알아야 할 첫 지점
        ///   Starving = StarvingBelowSatiety — 이 밑에선 체력이 깎인다 (NextStarvingDays와 같은 문턱)
        /// ⚠️ Starving 판정을 _starvingDays > 0 으로 쓰지 않은 이유: 같은 조건의 두 번째 사본이 된다.
        /// 여기는 문턱 비교 하나뿐이고, 시계는 SimTick이 돌린다 (판정 이원화 금지).
        /// </summary>
        public static HungerLevel JudgeHunger(float satiety, AgentConfigSO cfg)
        {
            if (cfg == null) return HungerLevel.None; // 미배선 = 중립 (경보 없음)
            if (satiety < cfg.StarvingBelowSatiety) return HungerLevel.Starving;
            if (satiety < cfg.OrderRefuseSatiety) return HungerLevel.Hungry;
            return HungerLevel.None;
        }

        /// <summary>내 상태 경보 등급 — 위 순수 판정의 인스턴스 창구 (HUD 열거용).</summary>
        public HungerLevel MyHunger => JudgeHunger(Satiety, _cfg);

        /// <summary>
        /// 아사 (ADR-M10-3 개정 2026-07-24 — 舊 굶주림 이탈). 상태만 Dead(ADR-M6-3)로 바꾸고 지연
        /// 파괴한다. 클레임·타일·플래너·명령·보상 정리는 전부 OnDestroy 단일 경로 — 여기서 직접
        /// 해제 금지 (두 번째 정리 경로가 된다).
        ///
        /// 개정 사유(사용자 결정): "배고파서 마을을 이탈하는 게 아니라 사망(아사)으로 가는 게 서사상
        /// 좋다". 떠난 주민은 어딘가 살아 있다는 여지를 남겨 상실이 희석되지만, 아사는 무덤이라는
        /// 영구 흔적을 남긴다 — 부상 사망(Die)과 같은 문(RecordDeath)을 쓰되 원인·대사만 분리한다.
        /// </summary>
        private void StarveToDeath()
        {
            Debug.LogWarning($"[VillagerAgent] {AgentId}: 아사 — 체력 0, 포만 {Satiety:F0} " +
                             $"(< {_cfg.StarvingBelowSatiety}) 지속 {_starvingDays:F2}일 " +
                             $"(감쇠 {_cfg.StarveHpPerDay:F0}/일 · 만복 기준 {DaysToStarveDeath(_cfg):F2}일)");
            _sim.RecordDeath(TileX, TileY, ShortName, (int)_sim.GameTime, AgentId); // 부상 사망과 같은 문 — 무덤·카운터 (원인만 이원)
            _sim.Chronicle.RecordExit(AgentId, _sim.GameTime, ExitCause.Starvation); // 명부 — 사유 이원화 유지 (ADR-M13-3)
            SnapshotRelationsForChronicle(); // C3 — ReleaseBy(OnDestroy)가 지우기 전에
            // (M19-W4: 지갑 소멸 회계(M16-W1)는 화폐와 함께 철거)
            _sim.Hud?.Notify($"{ShortName}이(가) 굶어 숨을 거뒀습니다");
            ShowTransient(Pick(_cfg.StarveLines));
            State = AgentState.Dead;      // SimTick 차단 — 새 상태 추가 금지 (ADR-M6-3)
            Destroy(gameObject, _cfg.TransientLineSec); // 마지막 대사 노출 후 소멸 (ShowTransient와 동일 에셋 값)
        }

#if UNITY_EDITOR
        /// <summary>디버그 즉사 (M13 — 전멸 회고 관측 장치, 에디터 전용). 실제 아사 경로
        /// (StarveToDeath)를 그대로 쓴다 — 무덤·카운터·명부·OnDestroy 정리가 실전과 완전히
        /// 동일해야 관측이 의미 있다 (새 사망 경로 금지 — ADR-M6-3 소멸 경로 단일성).
        /// Dead 가드 = 중복 발동 시 무덤·카운터 이중 기록 방지.</summary>
        public void DebugKill()
        {
            if (State == AgentState.Dead) return;
            // M21-W1: 체력을 통째로 깎아 실제 아사 문(TakeDamage → StarveToDeath)을 그대로 탄다.
            TakeDamage(_cfg.MaxHp, DamageCause.Starvation);
        }
#endif

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
            // 계측 (M12-J, 읽기 전용) — 분류를 이름이 아니라 **성향 태그**로 한다
            // (ADR-M0-1 이름 분기 금지의 정신 — 새 goal도 태그만 달면 자동 분류된다):
            //   노동 = 근면 축이 원하는 goal / 공용 = 자존 축이 피하는 goal
            _sim.Profiler?.RecordGoalPick(AgentId, Personality, _goal,
                GoalWants(_goal, TraitId.Diligence) > 0f,
                GoalWants(_goal, TraitId.Willfulness) < 0f);
            // 계측 (M14-W2, 읽기 전용) — goal이 **바뀔 때만** 1줄. 성공 기준 1(자발 파종 =
            // "식량이 남았는데 심는다")을 콘솔에서 판독하는 유일한 탐지기다. 매 재선택 로그는 소음.
            if (_goal != _lastPickedGoal)
            {
                _lastPickedGoal = _goal;
                Debug.Log($"[GoalPick] {AgentId}: {_goal.DisplayName} (식량 {snap.Get(SlotId.MyFoodDaysLeft)}일)");
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
                    // 단 MayHaveNoSolution goal은 예외 (ADR-M6-2 게임건강 예외):
                    //   ① 식량 goal — 겨울 봉쇄 + 저장분 0 = 굶주림이지 버그가 아니다.
                    //   ② 명령 goal — 플레이어가 불가능한 걸 시킨 것이지 결함이 아니다(2026-07-24 확장).
                    //      실패는 주민의 거부 대사·HUD로 이미 플레이어에게 보이므로 경고가 중복이다.
                    //      예: "명령: 식량"은 +5를 요구하는데 채집 전제가 MyRawFood ≤ 3(상한 8 − 수확 5)이라
                    //      소지 4 이상이면 애초에 달성 불가이고, 겨울엔 채집 자체가 봉쇄다.
                    bool giveUpOrder = _goal == _order; // ToIdle이 _goal을 비우기 전에 포착
                    string failedName = _goal.DisplayName;
                    if (_goal.MayHaveNoSolution)
                        Debug.Log($"[VillagerAgent] {AgentId}: {failedName} — 지금은 달성할 방법이 없다 " +
                                  $"(정상 — 굶주림이거나, 불가능한 명령을 받았거나)");
                    else
                        // 노드 수 병기 (2026-07-22 석재 goal 폭발 진단의 교훈): 4096 = 탐색 폭발
                        // (휴리스틱 수축·대부족량 goal), 그 미만 = 진짜 해 없음 (에셋 정합·발견 체인 순).
                        Debug.LogWarning($"[VillagerAgent] {AgentId}: NoSolutionFound (goal={_goal.name}, " +
                                         $"노드 {nodes}/{PlanningConfig.MaxNodes}) — ADR-3 진단 필요");

                    // 실패 쿨다운을 여기서도 기록한다 (AbortPlan과 같은 판정 — 누락이던 것).
                    // 없으면 도달 불가 goal이 2초마다 4096노드 탐색을 반복해 경고·CPU를 갉는다.
                    // P0 생존 goal은 SkipFailureCooldown으로 면제라 즉시 재시도가 유지된다.
                    if (!giveUpOrder && ShouldRecordFailureCooldown(_goal, true))
                        _goalRetryAt[_goal] = Time.time + _cfg.GoalRetryCooldownSec;

                    ToIdle(2f);

                    // 달성 불가 명령은 붙들지 않는다 — 쿨다운만으론 무한 재시도가 끝나지 않는다.
                    // 구조적 불가가 실재한다: 겨울 채집 봉쇄, 또는 "지금보다 +5"가 몸 소지 상한(8)을
                    // 넘는 경우(채집 전제가 ≤3이라 소지 4 이상이면 애초에 달성 불가). 주민이 못 하겠다고
                    // 말하고 자율로 돌아간다 — "주민은 명령을 거부할 수 있다"는 게임 정체성과도 맞는다.
                    if (giveUpOrder)
                    {
                        Debug.Log($"[VillagerAgent] {AgentId}: 명령 포기 — {failedName} (달성 불가)");
                        _sim.Hud?.Notify($"{ShortName}: 그 일은 못 하겠어요");
                        ShowTransient(Pick(_cfg.OrderGiveUpLines));
                        CancelOrder();
                    }
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
                // 🔴 조건 없는 명령의 완수 지점 (M21-W4, 2026-08-07 Play에서 잡았다).
                // TickIdle의 완수 판정은 `_order.GoalConditions`가 **있을 때만** 돈다. 그런데
                // DirectActionPool goal은 규약상 그 배열이 비어 있어("항상 미달성" 특례), 명령으로
                // 주면 완수 판정이 통째로 건너뛰어져 **명령이 영영 안 비워진다** — 실측: 위협이 다
                // 물러난 뒤에도 D가 「싸워라」를 쥔 채 노동으로 돌아갔고, 쿨다운이 풀릴 때마다 다시
                // 교전 goal을 집었다. 이런 goal에서는 **플랜 완료가 곧 완수**다 (끝이 슬롯 밖에
                // 있으니 슬롯으로 판정할 수가 없다 — DirectActionPool 자격 ⓐ의 귀결).
                if (_goal == _order && (_order.GoalConditions == null || _order.GoalConditions.Length == 0))
                {
                    Debug.Log($"[VillagerAgent] {AgentId}: 명령 완수 — {_order.DisplayName}");
                    PayReward();          // 약속 이행 (M6-E) — 반환 경로와 배타 (ADR-M6-5)
                    ClearOrderInstance(); // 소멸의 유일한 경로
                }
                ToIdle(0f); // 여가는 로그 없이 조용히 반복 (스팸 방지)
                return;
            }

            ActionSO action = _plan[_planIndex];

            // 성격 혼잣말 (M4-D) — 표현 전용, 확률·문구 전부 에셋 값. 비면 표시 없음 (중립 경로).
            // ShowTransient가 잠시 덮고 다음 갱신에서 플랜 문구로 복귀 — 거부 대사와 같은 통로.
            // 대화 장면 중엔 침묵 (InConversation — 대화 흐름 보호, 2026-07-18)
            // M12-F: 성격 전용 대사가 우선, 없으면 **축별 풀**에서 뽑는다 — 새 성격이 대사 0줄로도
            // 말이 통하게 (에셋 1개로 성격 추가라는 약속의 마지막 조각). 둘 다 없으면 침묵(중립).
            if (!InConversation && Personality != null && Random.value < Personality.MoodLineChance)
            {
                string[] pool = Personality.MoodLines != null && Personality.MoodLines.Length > 0
                    ? Personality.MoodLines
                    : (_sim.WorldConfig != null && _sim.WorldConfig.TraitRules != null
                        ? _sim.WorldConfig.TraitRules.MoodPoolFor(MyTraits) : null); // 개체 편차 포함 (M14-W3)
                if (pool != null && pool.Length > 0) ShowTransient(Pick(pool));
            }

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
            SuppressActionGesture = false; // 몸짓 억제는 액션 하나짜리 상태 — 다음 행동으로 새지 않는다

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

            // (M19-W4: 임금 지급·원천징수·중과세 불평은 화폐와 함께 철거 — ADR-M19-1.
            //  노동의 대가는 마을이 커지는 것 그 자체다. 이력 = M16-W2 · M17-W2/W5)

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
            // 방향은 **의미 있는 거리가 남았을 때만** 갱신한다 (2026-08-10). 도착 직전엔 남은
            // 벡터가 0에 수렴하는데, 그걸 normalize 하면 잡음이 방향으로 증폭되고 (0,0)이 되면
            // 보던 쪽마저 잃는다 — 축 히스테리시스가 지켜 낸 방향을 마지막 프레임에 놓치게 된다.
            Vector3 toTarget = targetPos - transform.position;
            if (toTarget.sqrMagnitude > 1e-6f) _lastDir = toTarget.normalized;
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

        /// <summary>잔여 경로 교차 판정 (순수 — EditMode 게이트 검산, M22-2차 W1).
        /// startIndex 앞은 이미 밟았거나 예약한 칸 — 검사하지 않는다 (한 걸음 관용, ADR-M22-9).</summary>
        internal static bool PathCrossesAny(List<Vector2Int> waypoints, int startIndex, HashSet<Vector2Int> tiles)
        {
            if (waypoints == null || tiles == null || tiles.Count == 0) return false;
            for (int i = Mathf.Max(0, startIndex); i < waypoints.Count; i++)
                if (tiles.Contains(waypoints[i])) return true;
            return false;
        }

        /// <summary>문 잠금의 선별 재계획 (M22-2차 W1, ADR-M22-9) — 잔여 경로가 잠긴 문을 지나는
        /// 주민만 중단한다 (전원 Abort 금지 — 진행 중 노동을 초기화하면 잠금의 숨은 비용이 과대).
        /// 한 걸음 관용: 이미 예약한 다음 칸은 그대로 밟는다 — 예약 시스템과 싸우지 않는다.
        /// warn/cooldown 없음 — 실패가 아니라 세계가 바뀐 것이다 ("상위 목표 전환" 선례).</summary>
        public void AbortIfPathCrosses(HashSet<Vector2Int> tiles)
        {
            if (State != AgentState.Moving) return; // 경로는 Moving에만 있다 (그 외는 다음 FindPath가 새 배열을 본다)
            int start = _hasNextReserved ? _wpIndex + 1 : _wpIndex;
            if (PathCrossesAny(_waypoints, start, tiles))
                AbortPlan("문이 잠겼다 — 재계획", warn: false, cooldown: false);
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
            => JudgeOrder(satiety, fatigue, cfg, null, null, null);

        /// <summary>성격 오프셋 포함 거부 판정 (M4-C, 순수 — 게이트 M4-T3). p=null이면 기존과 완전 동일.
        /// 호환 진입점 — 벡터는 성격 원본 (편차 없음 = 게이트·구형 호출 전용, 런타임은 MyTraits를 넘길 것).</summary>
        public static OrderResult JudgeOrder(float satiety, float fatigue, AgentConfigSO cfg, PersonalitySO p)
            => JudgeOrder(satiety, fatigue, cfg, p, p != null ? p.Traits : null, null);

        /// <summary>
        /// 보상 오프셋 포함 최종 판정 (M6-E, 순수 — 게이트 M6-T4). r=null이면 기존과 완전 동일.
        /// 성격·보상은 문턱을 옮길 뿐 판정은 결정적 (ADR-M1-2 계승 — 랜덤 금지,
        /// 플레이어가 학습 가능해야 협상이 성립). 보상은 판정에만 개입, 수행에는 불개입 (ADR-M6-4).
        /// </summary>
        /// <summary>실효 포만 거부 문턱 (순수, M13-D 추출) — 기준값 + 성향 편향(M12-D) +
        /// 舊 개별 오프셋(M12-F까지 병존) + 보상 오프셋. JudgeOrder(판정)와 정보줄 여유
        /// 표기(SeasonHud.ComposeReason)가 **이 함수 하나**를 쓴다 — 문턱 산식이 두 곳이면
        /// 화면의 여유와 실제 거부가 어긋난다 (판단 규칙 이원화).</summary>
        public static float RefuseSatietyLimit(AgentConfigSO cfg, PersonalitySO p, TraitValue[] traits,
                                               RewardSO r)
            => TraitVector.Threshold(traits, cfg.RefuseSatietyBias, cfg.OrderRefuseSatiety)
             + (p != null ? p.RefuseSatietyOffset : 0f)
             + (r != null ? r.RefuseSatietyOffset : 0f);
             // (M19-W4: 물가 스케일 철거 — 실물 보상의 설득력은 돈 값과 무관하다)

        /// <summary>실효 피로 거부 문턱 (순수, M13-D 추출) — 위와 동일 사유.</summary>
        public static float RefuseFatigueLimit(AgentConfigSO cfg, PersonalitySO p, TraitValue[] traits,
                                               RewardSO r)
            => TraitVector.Threshold(traits, cfg.RefuseFatigueBias, cfg.OrderRefuseFatigue)
             + (p != null ? p.RefuseFatigueOffset : 0f)
             + (r != null ? r.RefuseFatigueOffset : 0f);

        /// <summary>호환 진입점 (게이트·구형 호출 전용 — 벡터 = 성격 원본, 편차 없음. ⚠️W3-⑤).</summary>
        public static OrderResult JudgeOrder(float satiety, float fatigue, AgentConfigSO cfg,
                                             PersonalitySO p, RewardSO r)
            => JudgeOrder(satiety, fatigue, cfg, p, p != null ? p.Traits : null, r);

        public static OrderResult JudgeOrder(float satiety, float fatigue, AgentConfigSO cfg,
                                             PersonalitySO p, TraitValue[] traits, RewardSO r)
        {
            // 성향 가중치가 비면 Threshold가 기준값을 그대로 돌려주므로 현행 판정과 완전히 같다.
            // 벡터(traits)는 호출자가 개체 편차 포함본(MyTraits)을 넘긴다 (M14-W3 — ⚠️W3-⑤).
            if (satiety < RefuseSatietyLimit(cfg, p, traits, r)) return OrderResult.RefusedHungry;
            if (fatigue > RefuseFatigueLimit(cfg, p, traits, r)) return OrderResult.RefusedTired;
            return OrderResult.Accepted;
        }

        /// <summary>개체 편차 요약 (계측 전용, 순수 — 스폰 로그 1줄). 편차 0인 축은 생략.</summary>
        public static string TraitDeltaSummary(TraitValue[] baseTraits, TraitValue[] jittered)
        {
            if (jittered == null) return "없음";
            var sb = new System.Text.StringBuilder(64);
            for (int i = 0; i < jittered.Length; i++)
            {
                int delta = jittered[i].Value - TraitVector.ValueOf(baseTraits, jittered[i].Trait);
                if (delta == 0) continue;
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(jittered[i].Trait).Append(delta > 0 ? " +" : " ").Append(delta);
            }
            return sb.Length > 0 ? sb.ToString() : "없음";
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
                _sim.Chronicle.RecordEvent(AgentId, EventId.OrderRefused, _sim.GameTime,
                                           value: ChronicleEvent.REFUSE_INJURED); // 연대기 (M13-C2)
                return OrderResult.RefusedInjured;
            }

            // 재고 선검사 (M6-E 원형 — M19-W4에서 화폐 분기 철거): 없는 것을 약속할 수 없다.
            if (reward != null && World.GetStock(reward.CostSlot) < reward.CostAmount)
                return OrderResult.FailedNoStock; // 말풍선 없음 — 주민이 아니라 촌장의 실수

            OrderResult verdict = JudgeOrder(Satiety, Fatigue, _cfg, Personality, MyTraits, reward);
            if (verdict == OrderResult.RefusedHungry || verdict == OrderResult.RefusedTired)
                _sim.Profiler?.RecordRefusal(Personality); // 계측 (M12-J) — 자존 축의 관측 신호
            if (verdict == OrderResult.RefusedHungry)
            {
                ShowTransient(Pick(FirstNonEmpty(Personality != null ? Personality.RefuseHungryLines : null, _cfg.RefuseHungryLines)));
                Debug.Log($"[VillagerAgent] {AgentId}: 명령 거부 (배고픔 {Satiety:F0} < {_cfg.OrderRefuseSatiety})");
                _sim.Chronicle.RecordEvent(AgentId, EventId.OrderRefused, _sim.GameTime,
                                           value: ChronicleEvent.REFUSE_HUNGRY); // 연대기 (M13-C2)
                return verdict;
            }
            if (verdict == OrderResult.RefusedTired)
            {
                ShowTransient(Pick(FirstNonEmpty(Personality != null ? Personality.RefuseTiredLines : null, _cfg.RefuseTiredLines)));
                Debug.Log($"[VillagerAgent] {AgentId}: 명령 거부 (피로 {Fatigue:F0} > {_cfg.OrderRefuseFatigue})");
                _sim.Chronicle.RecordEvent(AgentId, EventId.OrderRefused, _sim.GameTime,
                                           value: ChronicleEvent.REFUSE_TIRED); // 연대기 (M13-C2)
                return verdict;
            }

            ClearOrderInstance(); // 기존 명령 교체 시 런타임 사본·미지급 약속 정리 (반환 경유)

            // 보상 에스크로 (M6-E 원형): 수락 확정 시점에 즉시 차감 — 완수=지급, 그 외=반환 (ADR-M6-5).
            if (reward != null)
            {
                if (!World.TrySpendStock(reward.CostSlot, reward.CostAmount))
                    return OrderResult.FailedNoStock; // 선검사 통과 후 변동 — 방어 (원자성은 TrySpend가 보장)
                Debug.Log($"[VillagerAgent] {AgentId}: 보상 약속 — {reward.DisplayName} " +
                          $"({reward.CostSlot} -{reward.CostAmount} 에스크로)");
                _promisedReward = reward;
                ShowTransient(Pick(reward.PromiseLines));
            }

            // 연대기 (M13-C2) — 수락 확정 지점 (거부·재고 실패 반환을 모두 지난 뒤).
            // 기존엔 실패 경로만 화면에 있었다 (알림 누락 5건 중 하나 — 명세 §3).
            _sim.Chronicle.RecordEvent(AgentId, EventId.OrderTaken, _sim.GameTime);

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
        /// <summary>
        /// 상대 목표의 실효 목표치 (순수 — 게이트 대상, M1-C 개정 2026-07-26).
        /// "지금보다 +N"은 몸 소지 상한을 모르는 표현이라, 개인 스톡에서는 소지 4~7일 때
        /// 목표가 상한을 넘어 **여름에도 항상 달성 불가**였다 (채집의 상한 전제 ADR-M11-3과 충돌 —
        /// 상한 8, 수확 +5면 전제가 ≤3이라 소지 4부터 계획이 안 선다).
        /// 상한으로 클램프하되 **진전을 요구할 때만** — 이미 상한이면 원래 목표(도달 불가)를 남겨
        /// 기존 '달성 불가 → 포기' 경로가 처리한다 (완료 경로를 새로 만들지 않는다, ADR-M0-3).
        /// cap ≤ 0 = 상한 없음(전역 스톡·미배선) = 기존 동작 그대로 (중립 불변식).
        /// delta는 음수도 된다 — "지금보다 −20"(피로 goal, 2026-08-06). 낮추는 목표에는 상한이
        /// 무의미하고 실제로 MyFatigue는 개인 스톡이 아니라 cap=0으로 들어온다.
        /// </summary>
        public static int ResolveRelativeTarget(int current, int delta, int cap)
        {
            int want = current + delta;
            if (cap <= 0 || want <= cap) return want;
            return cap > current ? cap : want;
        }

        private GoalSO ResolveRelativeGoal(GoalSO goal, out bool isClone)
        {
            if (goal.RelativeToCurrent && goal.GoalConditions != null)
            {
                WorldSnapshot snap = BuildSnapshot();
                GoalSO resolved = Instantiate(goal);
                resolved.name = goal.name; // 로그·비교 가독성 (에셋은 무변경)
                resolved.RelativeToCurrent = false;
                for (int i = 0; i < resolved.GoalConditions.Length; i++)
                {
                    SlotId slot = resolved.GoalConditions[i].Slot;
                    resolved.GoalConditions[i].Value = ResolveRelativeTarget(
                        snap.Get(slot), goal.GoalConditions[i].Value,
                        SlotIds.IsPersonalStock(slot) ? _cfg.BodyCarryCap : 0);
                }
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
        /// <summary>호환 진입점 (게이트·구형 호출 전용 — 벡터 = 성격 원본, 편차 없음).
        /// 런타임은 traits 인자판(MyTraits)을 쓴다 (⚠️W3-⑤).</summary>
        public static RequestResult JudgeRequest(bool busy, float satiety, float fatigue,
                                                 int affinityTowardRequester, AgentConfigSO cfg,
                                                 PersonalitySO p, RequestSO r,
                                                 bool upfrontAvailable = false)
            => JudgeRequest(busy, satiety, fatigue, affinityTowardRequester, cfg, p,
                            p != null ? p.Traits : null, r, upfrontAvailable);

        public static RequestResult JudgeRequest(bool busy, float satiety, float fatigue,
                                                 int affinityTowardRequester, AgentConfigSO cfg,
                                                 PersonalitySO p, TraitValue[] traits, RequestSO r,
                                                 bool upfrontAvailable = false)
        {
            if (busy) return RequestResult.RefusedBusy;
            OrderResult body = JudgeOrder(satiety, fatigue, cfg, p, traits, null);
            if (body == OrderResult.RefusedHungry) return RequestResult.RefusedHungry;
            if (body == OrderResult.RefusedTired) return RequestResult.RefusedTired;
            if (r != null && affinityTowardRequester < r.RefuseAffinityBelow)
                return RequestResult.RefusedLowAffinity;
            if (cfg != null && cfg.DemandsUpfront(p, traits) && !upfrontAvailable)
                return RequestResult.RefusedNoReward;
            return RequestResult.Accepted;
        }

        /// <summary>
        /// 부탁 수신 (RequestService 전용) — 수락 시 InjectGoal이 개인 사다리에 합류한다
        /// (ADR-M8-4: 새 실행 경로 없음, Select의 request 칸). 말풍선·관계 델타·사유 로그는
        /// 호출자(RequestService)가 수행 (ADR-M8-5의 단일 지점).
        /// </summary>
        public RequestResult TryGiveRequest(RequestSO r, string requesterId, bool upfrontAvailable = false,
                                            bool instantTrade = false)
        {
            // 즉시 교환(M16-W5)은 InjectGoal이 없는 게 정상 — 일이 아니라 교환이다
            if (r == null || (!instantTrade && r.InjectGoal == null))
                return RequestResult.RefusedBusy; // 방어 — 에셋 오류
            // 부상 거절 (M10-A) — 명령과 동일한 앞단 조기 반환 (JudgeRequest 순수 함수 무변경)
            if (Injury != InjurySeverity.None) return RequestResult.RefusedInjured;
            // 선불 가용성 (ADR-보상2)은 호출자(RequestService)가 의뢰인 개인 잔고로 판정해 넘긴다
            // (M11-H — 전역 스톡 조회 폐지. 판정과 이전이 같은 시뮬 틱이라 검사~지급 경쟁 없음)
            RequestResult verdict = JudgeRequest(_request != null, Satiety, Fatigue,
                _sim.Relationship.AffinityOf(AgentId, requesterId), _cfg, Personality, MyTraits, r,
                upfrontAvailable);
            if (verdict != RequestResult.Accepted) return verdict;

            if (!instantTrade)
            {
                _request = ResolveRelativeGoal(r.InjectGoal, out _requestIsRuntimeClone);
                _goalRetryAt.Remove(_request); // 새 부탁은 과거 실패 쿨다운을 잊는다 (명령과 동일)
                if (State == AgentState.Idle) _idleCooldownSec = 0f; // 즉시 재평가 (반응성)
            }
            // 즉시 교환은 상태 무변경 — 이전(TransferTo)은 호출자(RequestService.Ask)가 같은 틱에 한다
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

        // FindNearestWithJob(M17-R3)은 M20-W12에서 SeekJob 사슬(유일한 호출처)과 함께 삭제
        // (ADR-M0-4 폐기=삭제 — 집 부탁 철거 ADR-M20-7의 귀결. 복원은 git 히스토리에서).

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

        /// <summary>이 지점을 바라본다 (표현 전용 — 러너가 대상 응시에 쓴다. 판정 없음).
        /// 이동 중이면 다음 프레임에 이동 방향이 덮는다: 응시는 **제자리에서 무언가를 하고 있을 때**의
        /// 그림이다. 계기 = 주민이 고블린에게 등을 돌린 채 도끼질하고 있었다 (2026-08-10 Play).</summary>
        public void FaceTowards(Vector2 towardWorldPos)
        {
            Vector2 d = towardWorldPos - (Vector2)transform.position;
            if (d.sqrMagnitude > 1e-4f) _lastDir = d.normalized;
        }

        // ── 피격 번쩍임 (2026-08-10) — 위협 쪽(ThreatAgent)과 같은 규약·같은 색.
        // 색만 건드린다: 체력·부상 판정은 TakeDamage의 것이고 여기는 그 결과를 보이게 할 뿐이다.
        private const float HURT_FLASH_SEC = 0.12f;
        private static readonly Color HurtColor = new Color(1f, 0.45f, 0.45f, 1f);
        private float _flashUntil;
        private Color _flashBaseColor;
        private bool _flashing;

        /// <summary>맞았다 (표현 전용).</summary>
        public void PlayHurt()
        {
            if (_viewSr == null) return;
            if (!_flashing) { _flashBaseColor = _viewSr.color; _flashing = true; } // 원래 색은 한 번만
            _flashUntil = Time.time + HURT_FLASH_SEC;
        }

        private void TickFlash()
        {
            if (!_flashing || _viewSr == null) return;
            if (Time.time < _flashUntil) { _viewSr.color = HurtColor; return; }
            _viewSr.color = _flashBaseColor;
            _flashing = false;
        }

        /// <summary>행동 몸짓 억제 (러너 전용, 표현). 액션은 Running 인데 **아직 실제로는 하고 있지
        /// 않은** 구간 — 교전의 매복 대기 같은 — 에서 켠다. 켜면 대기/걷기 그림으로 돌아간다.
        /// 🔑 이게 없으면 사거리 밖에서 기다리는 주민이 허공에 칼을 휘두르고, 화면만 보는
        /// 플레이어는 "싸우는 중"과 "기다리는 중"을 구분할 수 없다. 새 액션이 시작될 때
        /// 자동으로 꺼진다 (StartNextAction) — 러너가 끄는 것을 잊어도 다음 행동에 안 샌다.</summary>
        public bool SuppressActionGesture { get; set; }

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

        // (M19-W5: 지갑·빚·SetDebt·첫 임금 표식은 화폐와 함께 철거 — ADR-M19-1.
        //  M24-1차 W2: 그 슬롯 3칸은 종족 축이 재사용했고, 화폐 재발은 이제 게이트 M19-T1이
        //  번호가 아니라 **개념**으로 감시한다.)

        /// <summary>슬롯별 잔량 — EffectApplier 선검사·스냅샷 주입 공용 (판정 단일).</summary>
        public int GetPersonalStock(SlotId slot)
            => slot == SlotId.MyRawFood ? MyRaw
             : slot == SlotId.MyCookedFood ? MyCooked : 0;

        /// <summary>개인 스톡 슬롯별 상한 (M19-W5: 돈 예외 철거 — 전 슬롯 몸 상한 하나).</summary>
        public static int PersonalCapOf(SlotId slot, int bodyCarryCap)
            => bodyCarryCap;

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
            (bool ok, int next) = NextPersonalStock(GetPersonalStock(slot), op, value,
                                                    PersonalCapOf(slot, _cfg.BodyCarryCap));
            if (!ok) return false;
            if (slot == SlotId.MyRawFood) MyRaw = next;
            else if (slot == SlotId.MyCookedFood) MyCooked = next;
            else return false; // (M19-W5: 지갑 철거 — 개인 스톡은 식량 2종뿐)
            return true;
        }

        /// <summary>지급 능력 (순수 — 게이트 M11-T7): 몸 우선, 부족분은 집 저장에서.
        /// amount ≤ 0은 항상 true (보상 없는 부탁 — 지급할 것이 없다).</summary>
        public static bool CanPay(int bodyStock, int homeStock, int amount)
            => amount <= 0 || bodyStock + homeStock >= amount;

        /// <summary>내 잔고(몸+집)로 amount를 낼 수 있는가 (M11-H 보상 정산의 판정 창구).</summary>
        public bool CanPayReward(SlotId slot, int amount)
            => CanPay(GetPersonalStock(slot), HomeStockOf(slot), amount);

        /// <summary>수령 공간 — 상한은 슬롯별이다 (M11-A 개정). 몸에만 받는다 (집은 걸어가야 하므로).
        /// 돈은 상한 없음 (PersonalCapOf — M16 실사 ④).</summary>
        public bool HasRoomFor(SlotId slot, int amount)
            => amount <= 0 || PersonalCapOf(slot, _cfg.BodyCarryCap) - GetPersonalStock(slot) >= amount;

        /// <summary>내 집 저장 잔량 — 무주택·미배선이면 0.</summary>
        private int HomeStockOf(SlotId slot)
        {
            SlotId homeSlot = slot == SlotId.MyRawFood ? SlotId.MyHomeRawFood
                            : slot == SlotId.MyCookedFood ? SlotId.MyHomeCookedFood : slot;
            if (homeSlot == slot) return 0; // 개인 스톡이 아니면 집 몫 0
            return HomeStorage != null && TryGetHomeTile(out Vector2Int home)
                ? HomeStorage.GetStock(home, homeSlot) : 0;
        }

        /// <summary>
        /// 개인 재화 이전 (M11-H) — 보상의 실체. 새 쓰기 경로가 아니다 (ADR-M11-1): 차감은
        /// ApplyPersonalStock·HomeStorage.TrySpend, 지급은 ApplyPersonalStock을 그대로 지난다.
        /// 원자성: 선검사(지급 능력 + 수령 공간) 통과 후에만 손을 댄다 — 부분 이전 없음.
        /// 몸에서 먼저 빼고 부족분만 집에서 꺼낸다 (몸이 마르면 다시 채우는 리듬 유지).
        /// </summary>
        public bool TransferTo(VillagerAgent to, SlotId slot, int amount)
        {
            if (to == null || amount <= 0 || !SlotIds.IsPersonalStock(slot)) return false;
            if (!CanPayReward(slot, amount) || !to.HasRoomFor(slot, amount)) return false;

            int fromBody = Mathf.Min(GetPersonalStock(slot), amount);
            int fromHome = amount - fromBody;
            if (fromBody > 0 && !ApplyPersonalStock(slot, EffectOp.SubClamp0, fromBody)) return false;
            if (fromHome > 0)
            {
                SlotId homeSlot = slot == SlotId.MyRawFood ? SlotId.MyHomeRawFood : SlotId.MyHomeCookedFood;
                if (!TryGetHomeTile(out Vector2Int home)
                    || !HomeStorage.TrySpend(home, homeSlot, fromHome))
                {
                    // 선검사를 통과하고도 실패 = 잔고 판정과 저장 문의 이원화 (버그)
                    if (fromBody > 0) ApplyPersonalStock(slot, EffectOp.Add, fromBody); // 되돌림
                    Debug.LogWarning($"[Inventory] {AgentId}: 집 저장 차감 실패 — 이전 취소 (판정 이원화 의심)");
                    return false;
                }
            }
            if (!to.ApplyPersonalStock(slot, EffectOp.Add, amount))
            {
                // 선검사(HasRoomFor)를 통과하고도 실패 = 버그. 식량이 사라지는 쪽이 더 나쁘므로
                // 차감분을 전부 되돌린다 (몸→집 순, 집 복귀 실패분은 경고로 드러낸다).
                RefundSelf(slot, fromBody, fromHome);
                Debug.LogWarning($"[Inventory] {to.AgentId}: 수령 실패 — 공간 선검사 누락 의심 (이전 취소)");
                return false;
            }
            return true;
        }

        /// <summary>이전 취소 시 차감분 복원 (TransferTo 전용) — 뺀 순서의 역순.</summary>
        private void RefundSelf(SlotId slot, int fromBody, int fromHome)
        {
            if (fromHome > 0 && TryGetHomeTile(out Vector2Int home) && HomeStorage != null)
            {
                SlotId homeSlot = slot == SlotId.MyRawFood ? SlotId.MyHomeRawFood : SlotId.MyHomeCookedFood;
                if (!HomeStorage.TryAdd(home, homeSlot, fromHome, _cfg.HomeStorageCap))
                    Debug.LogWarning($"[Inventory] {AgentId}: 집 저장 복원 실패 {fromHome} — 소실");
            }
            if (fromBody > 0 && !ApplyPersonalStock(slot, EffectOp.Add, fromBody))
                Debug.LogWarning($"[Inventory] {AgentId}: 몸 소지 복원 실패 {fromBody} — 소실");
        }

        /// <summary>내 식량 총 개수 (M16-W4 — 물가 분모 Q의 재료: 몸+집, 생식+조리).
        /// EstimateMyFoodDays(일수 환산)와 다르다 — 물가는 재화 "개수" 대 돈의 비율이다.</summary>
        public int TotalFoodCount()
        {
            int home = 0;
            if (HomeStorage != null && TryGetHomeTile(out Vector2Int t))
            {
                (int raw, int cooked) = HomeStorage.Get(t);
                home = raw + cooked;
            }
            return MyRaw + MyCooked + home;
        }

        /// <summary>피격 경험 (M11-G, MyWasAttacked 슬롯의 유일한 원천) — 쓰기는 Injure뿐이고
        /// 되돌리는 경로는 없다 (영구). 회복·간호는 이 값을 건드리지 않는다.</summary>
        public bool MyWasAttacked { get; private set; }

        /// <summary>굶어 죽을 뻔한 경험 (M12-G, MyWasStarved 슬롯의 유일한 원천) — 쓰기는 SimTick의
        /// 굶주림 계단 1곳뿐이고 되돌리는 경로는 없다 (영구, MyWasAttacked 동형).
        /// 배부르게 회복해도 남는다 — 이것은 상태가 아니라 **기억**이고, 기질(성향)이 하지 않는
        /// 선택을 하게 만드는 근거다 (집 부탁의 성향 문턱 우회).</summary>
        public bool MyWasStarved { get; private set; }

        /// <summary>무기 소유 (M21-W5, MyHasWeapon 슬롯의 유일한 원천) — 쓰기는 CraftRunner 완료
        /// (AcquireWeapon)뿐이고 되돌리는 경로는 없다 (분실·내구도 없음 — ADR-M21-5 이번 범위).
        /// 소비처는 FightRunner의 타격 간격 배율 하나 — 게이트가 아니라 배율이다 (맨손도 싸운다).
        /// 세이브 대상 (ADR-M0-10).</summary>
        public bool MyHasWeapon { get; private set; }

        /// <summary>무기 획득의 유일한 문 (M21-W5) — CraftRunner가 재료 지불 가능성을 선검사한
        /// 직후에만 부른다 (Set MyHasWeapon 효과는 플래너 픽션 — EffectApplier는 논리형을 무시).</summary>
        public void AcquireWeapon() => MyHasWeapon = true;

        /// <summary>내 집 타일 (M11-A) — 집 저장 라우팅·피난 목적지 공용 창구. 원천 = OwnershipService.</summary>
        public bool TryGetHomeTile(out Vector2Int tile)
            => _sim.Ownership.TryGetOwned(AgentId, SlotId.HouseCount, out tile);

        /// <summary>내(몸+집) 식량 일수 (M11-D) — HUD 최솟값 집계용. 스냅샷 슬롯 19와 같은 산식.</summary>
        public int EstimateMyFoodDays()
        {
            bool hasHome = TryGetHomeTile(out Vector2Int home);
            (int homeRaw, int homeCooked) = hasHome && HomeStorage != null
                ? HomeStorage.Get(home) : (0, 0);
            return World.EstimatePersonalFoodDays(MyRaw, MyCooked, homeRaw, homeCooked);
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
                _viewSr = marker; // 깊이는 매 프레임 갱신 (움직이니까)
                transform.localScale = Vector3.one * 0.8f;
                return;
            }

            // 중앙 정렬과 규격 배율은 SpriteViewRig 하나가 소유한다 (위협과 공용, 2026-08-10).
            // 舊 코드의 `-set.Scale` 오프셋은 "팩 피벗이 좌하단"이라는 **틀린 전제**였다 —
            // 실측 피벗은 중앙이라(rect 32×32 → (16,16)) 그 오프셋만큼 주민이 반 칸 어긋나 있었다.
            SpriteRenderer sr = SpriteViewRig.Attach(transform, set);
            _viewSr = sr; // 깊이는 매 프레임 갱신 (움직이니까 — Update의 TickDepth)
            _animator = new AgentAnimator(sr, set, agentColor);
            SetupShadow(set);
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
