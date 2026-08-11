using System;
using System.Collections.Generic;
using AIVillage.Core;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 야생 위협 (M10-C) — 스케줄(예고→출몰)·타격 판정의 시뮬 서비스. 파괴·피해는 직접 하지 않고
    /// 문(Construction.RemoveCountableAt / VillagerAgent.TakeDamage)을 호출한다 (ADR-M9-3 사상).
    /// **수명 개정 (M21-W2)**: 도착은 퇴장이 아니라 **체류의 시작**이다 — NotifyArrived(첫 타격) 뒤
    /// NotifyStrikeTick이 주기마다 다시 치고, 퇴장은 BeginExit 하나로만 열린다(도주·상한·대상 소멸).
    /// **배회·계절 개정 (M21-W2R)**: 체류는 정지가 아니라 배회다(PickWanderTile) — 목적지 선정은
    /// 통행 배열을 아는 이쪽이 하고 개체는 걷기만 한다. 계절이 **타깃 확률과 체류 상한 둘**을
    /// 바꾼다(ADR-M21-9, 판정은 ForageFrozen 하나 — IsCrisis 아님). 밭 타깃의 "칠 대상 소멸" 퇴장이
    /// 여기서 신설됐다: 주민형에만 있던 경로라 밭형은 `밭 0/0 소실`을 상한까지 반복했다.
    /// 확정 발동·확정 착탄 (ADR-M10-1): 스케줄·희생 선정에 확률 없음 — 주민 희생은 거리순
    /// (도망 행동과의 인과), 밭 희생은 StableHash 시드 셔플 (재해와 동일).
    /// 활성 밴드 = 게임일 UnlockDay 충족 중 최신 1개 (ADR-M10R-1 시간 래칫 — 사망해도 강등 없음).
    /// 타깃 종류(밭/주민)는 밴드 고정이 아니라 출몰별 시드 롤 (ADR-M10R-3 — 상위 티어도 밭을 칠 수 있다).
    /// Threats가 비면 SimulationLoop이 서비스 자체를 null로 둔다 (중립 불변식, DisasterService 패턴).
    /// 세이브 대상 = _lastStrikeDay·_strikeOrdinal·**_reliefStacks·_delayDays**(M21-W7) (ADR-M10-10).
    /// 진행 중 개체·예고 상태는 저장 안 함 (로드 후 다음 스케줄에서 재출몰).
    /// </summary>
    public sealed class ThreatService
    {
        private readonly ThreatSO[] _threats;
        // 계절 (M21-W2R) — 舊 ZoneService 슬롯의 교체. 구역 참조는 b51c631(FarmPlot.ZoneRadius=0)
        // 이후 죽은 코드였고, 밭 타깃은 실제 밭 타일을 직접 본다 — 빼는 것이 실상을 코드에 맞추는 것이다.
        // null = 계절 없는 판 (중립: 야수는 굶지 않는다).
        private readonly SeasonService _season;
        private readonly ConstructionService _construction;
        private readonly IReadOnlyList<VillagerAgent> _agents;
        private readonly WorldConfigSO _config;
        private readonly Func<IPathfinder> _pathfinder; // Awake 조립 순서 무관하게 지연 조회
        private readonly Func<int, int, bool> _isWalkable; // 배회 목적지 필터 (VillagerAgent.IsWalkable 패턴)
        private readonly Transform _parent;

        private float _lastStrikeDay;   // 마지막 발동 시각 (게임일). 세이브 대상 (ADR-M10-10)
        private int _strikeOrdinal;     // 발동 누적 서수 — 진입점·밭 희생 시드의 유일 키. 세이브 대상

        // 종족별 조우 누적 (M24-1차 W3) — 키 = ThreatSO 에셋 이름(안정), 값 = 출몰 횟수.
        // **세이브 대상** (ADR-M0-10 선언): 유효압력의 한 항이라 판을 이어 받으면 함께 와야 한다.
        // 🔑 조우 = **출몰**이다 (ADR-M24-5). 격퇴 성공만 세면 지는 판에서 압력이 멈춰
        // "약한 마을은 영영 안 배우는 적을 만난다"가 되고, 무엇보다 단조가 깨질 여지가 생긴다.
        private readonly Dictionary<string, int> _encounters = new Dictionary<string, int>(4);
        private readonly HashSet<string> _seenBuf = new HashSet<string>();  // 편성 내 종족 중복 제거용
        private readonly Func<int> _peakPopulation;                          // 역대 최고 인구 (예산의 성장 항)
        private readonly List<Vector2Int> _towerBuf = new List<Vector2Int>(4); // 완공 망루 타일 (출몰 1회용)

        /// <summary>마을 중심 (W7R 우회 판정의 기준축) — 원천은 `WorldConfigSO.BaseTileX/Y` 하나다
        /// (집 배치·디버그 둘레와 같은 자, ADR-M0-2). 미배선 = 원점 = 舊 기본 기지 좌표(중립).</summary>
        private Vector2Int VillageCenter
            => _config != null ? new Vector2Int(_config.BaseTileX, _config.BaseTileY) : Vector2Int.zero;
        // 함정을 밟아 본 종족 (M24-1차 W5, TrapLearning) — 키 = 에셋 이름. 세이브 대상 (ADR-M0-10).
        // 🔑 "학습"을 확률이 아니라 **경험**으로 둔 이유: 결정성(ADR-M10R-2)을 지키면서
        // 플레이어가 인과를 짚을 수 있게 — "한 번 걸렸더니 그 뒤로 피한다".
        private readonly HashSet<string> _trapped = new HashSet<string>();
        // 예고~발동 사이 고정 (ADR-M24-3: 편성은 예고 시점에 확정된다 — 예고한 그 편성이 온다).
        // 발동 시점에 다시 계산하면 "3일 뒤 오크"라고 알려 놓고 고블린이 오는 일이 생긴다.
        private List<WaveEntry> _pendingWave;
        private ThreatSO _pendingPrimary;  // 편성의 주력 (예산 최다) — 화면 문구·술렁임의 화자
        private bool _pendingSolo;         // 악마 단독 웨이브인가
        private float _lastSoloDay = -1f;  // 마지막 단독 웨이브 게임일. 세이브 대상 (ADR-M0-10)
        private float _gameTime;        // 최근 틱의 게임일 (M21-W2) — 체류·재타격 판정의 시계.
                                        // 개체는 Update(실시간)로 돌지만 주기는 게임일이라 여기서 받아 쓴다

        private readonly List<ThreatAgent> _active = new List<ThreatAgent>(2);
        // 출몰 무리 등록부 (M21-W6) — 키 = 출몰 서수, 값 = 실제 생성 마릿수 (경로 없어 생략된
        // 개체는 안 센다 — 태어난 적 없는 개체가 분모에 남으면 무리 도주선이 영영 안 열린다).
        // 무리 전원 소멸 시 NotifyDespawn이 지운다. 세이브 대상 아님 (진행 중 개체와 운명 공유).
        private readonly Dictionary<int, int> _groupSpawned = new Dictionary<int, int>(2);
        private readonly List<ThreatAgent> _routBuf = new List<ThreatAgent>(4);
        // 무리별 전투 퇴장 수 (M21-W7) — 격퇴 성공("무리 전체가 전투로 물러남") 판정의 분자.
        private readonly Dictionary<int, int> _groupCombatExits = new Dictionary<int, int>(2);
        // 무리별 참여 타격자 (M21-W9) — 격퇴 성공 시 연대기에 남을 "전원"의 명단.
        // 원천 = CombatService.NoteAttacker (착탄한 타격만 — 헛손질은 참여가 아니다).
        private readonly Dictionary<int, List<string>> _groupAttackers = new Dictionary<int, List<string>>(2);

        /// <summary>적습 격퇴 성립 (M21-W9) — 무리 전원이 전투로 물러난 순간 1회 (위협, 무리 규모,
        /// **참여 타격자 전원**). 연대기(참여 전원 기록)·격퇴 카운터 구독 지점. W7 완충과 같은
        /// 판정 지점에서 나간다 — 판정이 두 벌이면 "완충은 됐는데 기록이 없다"가 생긴다.
        /// ⚠️ 명단 리스트는 직후 재사용될 수 있다 — 구독자는 동기 소비만 (OnStruck 규약과 동일).</summary>
        public event Action<ThreatSO, int, IReadOnlyList<string>> OnRaidRepelled;

        /// <summary>참여 타격자 등록 (CombatService 전용, M21-W9) — 착탄마다 호출, 중복은 1회만.</summary>
        public void NoteAttacker(int groupKey, string attackerId)
        {
            if (string.IsNullOrEmpty(attackerId)) return;
            if (!_groupAttackers.TryGetValue(groupKey, out List<string> list))
                _groupAttackers[groupKey] = list = new List<string>(2);
            if (!list.Contains(attackerId)) list.Add(attackerId);
        }

        // ── 래칫 양방향 완충 (M21-W7, ADR-M21-4) — 티어는 불가침, 완충은 규모·간격만 ──
        // 🔴 M10R가 막은 루프의 재발이 아니다: 옛 루프는 *마을 규모* 축이 사망으로 줄며 티어가
        // 강등되는 **영구** 하향이었고, 이것은 1회 소모·클램프·티어 불변의 **한시** 완충이다.
        private int _reliefStacks;   // 다음 출몰 마릿수 감산 (상한 = 에셋). 세이브 대상 (ADR-M0-10)
        private float _delayDays;    // 다음 발동 지연 (게임일, 사망만 쌓는다). 세이브 대상

        /// <summary>현재 완충 스택 (읽기 전용 — 게이트·HUD). 쓰기는 사망·격퇴 성공·스폰 소모 3곳뿐.</summary>
        public int ReliefStacks => _reliefStacks;

        /// <summary>현재 발동 지연 (읽기 전용 — 게이트·HUD).</summary>
        public float DelayDays => _delayDays;

        private int ReliefStackMax => _config != null ? Mathf.Max(0, _config.ThreatReliefStackMax) : 3;
        private float ReliefDelayPerDeath => _config != null ? Mathf.Max(0f, _config.ThreatReliefDelayDays) : 2f;
        private readonly List<VillagerAgent> _victimAgentBuf = new List<VillagerAgent>(8);
        private readonly List<(string id, int x, int y)> _victimKeyBuf = new List<(string, int, int)>(8);
        private readonly List<Vector2Int> _plotBuf = new List<Vector2Int>(16);
        private readonly List<Vector2Int> _approachBuf = new List<Vector2Int>(4);
        private readonly List<int> _victimIdxBuf = new List<int>(16);

        /// <summary>예고 알림 (1회/발동) — HUD 경보 구독 (표현).</summary>
        public event Action<ThreatSO> OnForecast;

        /// <summary>타격 알림 (위협, 주민타격 여부, 피해 수, 타격 타일, **실제 피격자**) — HUD·반응 대사 구독.
        /// victims = 이번 타격으로 체력이 깎인 주민 (밭 타격이면 빈 목록). 대사 화자를 "근처 아무나"가 아니라
        /// 실제 피격자로 못박기 위한 것 — 상태와 표현이 어긋나면 "물렸다는데 아무도 안 다침"이 된다
        /// (2026-07-24 Play 관측). 버퍼 재사용이므로 구독자는 동기 소비만 할 것.</summary>
        public event Action<ThreatSO, bool, int, Vector2Int, IReadOnlyList<VillagerAgent>> OnStruck;

        private static readonly VillagerAgent[] EmptyVictims = Array.Empty<VillagerAgent>();
        private readonly List<VillagerAgent> _struckBuf = new List<VillagerAgent>(8);

        /// <summary>예고 구간의 **주력 종족** (주민 술렁임·HUD 판독용 — Season.NextCrisis 패턴).
        /// null = 평시. 편성이 여럿이어도 화면 문구는 하나를 골라야 하므로 예산을 가장 많이 쓴
        /// 종족을 내보낸다 (M24-1차 W4 — 舊 단일 밴드 자리의 계승).</summary>
        public ThreatSO Forecasting => _pendingPrimary;

        /// <summary>예고 구간의 **전체 편성** (M24-1차 W4) — 예고 UI(W8)가 읽는다. null = 평시.</summary>
        public IReadOnlyList<WaveEntry> ForecastingWave => _pendingWave;

        /// <summary>이번 예고가 악마 단독 웨이브인가 (M24-1차 W4).</summary>
        public bool ForecastingSolo => _pendingSolo;

        /// <summary>발동까지 남은 게임일 (M13-B) — 예고 중이 아니면 음수. 표시 전용 파생값:
        /// 쓰기 없음, 예고·발동 판정은 Tick이 그대로 소유한다 (ADR-M0-3 상태 쓰기 단일 지점).
        /// 주기는 이제 **전역 고정**이다 (M24-1차 W4 — 밴드가 사라져 종족별 PeriodDays는 휴면).</summary>
        public float DaysToStrike(float gameTime)
            => _pendingWave == null ? -1f
                                    : _lastStrikeDay + WavePeriod + _delayDays - gameTime; // 지연 포함 (M21-W7)

        /// <summary>전역 웨이브 주기 — 미배선이면 5 (에셋 기본값과 같은 수, 중립).</summary>
        private float WavePeriod => _config != null && _config.WavePeriodDays > 0f
                                  ? _config.WavePeriodDays : 5f;

        public ThreatService(ThreatSO[] threats, SeasonService season,
                             ConstructionService construction, IReadOnlyList<VillagerAgent> agents,
                             WorldConfigSO config, Func<IPathfinder> pathfinder,
                             Func<int, int, bool> isWalkable, Transform parent,
                             DefenseService defense = null,
                             Func<int> peakPopulation = null)
        {
            // (M24-1차 W7R) 舊 towerRange 인자는 여기서 사라졌다 — 우회 판정이 사거리를 안 본다.
            // 사거리는 여전히 탑승 요격(ManTowerRunner)의 것이고, 그쪽 배선은 그대로다.
            // 역대 최고 인구 (M24-1차 W3·W4) — 예산의 성장 항. 미배선이면 0 = 시간 항만 (중립).
            // 🔴 현재 인구를 넘기면 ADR-M24-1 위반이다 (게이트 M24-T2가 그 계약을 지킨다).
            _peakPopulation = peakPopulation ?? (() => 0);
            _threats = threats ?? Array.Empty<ThreatSO>();
            _season = season;
            _construction = construction;
            _agents = agents;
            _config = config;
            _pathfinder = pathfinder;
            _isWalkable = isWalkable;
            _parent = parent;
            _defense = defense;
        }

        // ── 공성 (M22-W5, ADR-M22-2 — 막힌 위협은 출몰을 접지 않고 시설을 부순다) ──

        // null = 방어 축 없는 판 (중립: 막히면 기존 출몰 생략 그대로).
        private readonly DefenseService _defense;

        /// <summary>무리별 공성 상태 — 파괴 순간 해제되고 무리 전원이 원래 타깃으로 재진격한다.
        /// 공성은 **타깃 교체**일 뿐이다: 수명 주기(체류 상한·재타격 주기·퇴장 3경로)는 기존 그대로
        /// (명세 ⚠️① — 새 수명 규칙을 만들면 W2R가 고친 것을 다시 부순다).</summary>
        private struct SiegeInfo
        {
            public SlotId Slot;
            public Vector2Int Tile;               // 두드리는 시설
            public bool OrigTargetsVillagers;     // 원래 롤 (돌파 후 복귀할 타깃 종류)
            public Vector2Int OrigTarget;         // 원래 목표 (밭형 — 주민형은 돌파 시점 재선정)
        }
        private readonly Dictionary<int, SiegeInfo> _groupSiege = new Dictionary<int, SiegeInfo>(2);
        private readonly List<ThreatAgent> _resumeBuf = new List<ThreatAgent>(4);

        /// <summary>공성 타격 알림 (위협, 시설 슬롯, 타일, 남은 내구도, 최대) — HUD·대사 구독 (표현).</summary>
        public event Action<ThreatSO, SlotId, Vector2Int, float, float> OnStructureStruck;

        /// <summary>시설 파괴 알림 — HUD 구독. 제거·통행 복구는 RemoveCountableAt → OnRemoved 배선 몫.</summary>
        public event Action<ThreatSO, SlotId, Vector2Int> OnStructureDestroyed;

        /// <summary>이 무리가 공성 중인가 (ThreatAgent 판독점 — 공성 중엔 추격을 멈추고 시설로 간다).</summary>
        public bool IsGroupSieging(int groupKey) => _groupSiege.ContainsKey(groupKey);

        // ── 함정 발동 (M22-3차 W4, ADR-M22-13) ────────────────────────────────

        // 판정(피해·도주선·사냥)의 집. 생성 순서상 CombatService가 뒤라 늦게 물린다 —
        // null = 전투 축 없는 조립(테스트 등)이면 함정도 조용히 꺼진다 (중립 불변식).
        private CombatService _combat;

        /// <summary>전투 판정 결합 (SimulationLoop 전용, M22-3차 W4) — 생성 순환(Combat이
        /// Threats를 받는다) 때문에 생성자로 못 받는다. 호출은 조립 1회뿐.</summary>
        public void AttachCombat(CombatService combat) => _combat = combat;

        /// <summary>함정 발동 알림 (위협, 타일, 피해) — HUD 정보줄·대사 구독 (표현 전용).</summary>
        public event Action<ThreatSO, Vector2Int, float> OnTrapTriggered;

        /// <summary>
        /// 위협이 타일에 들어섰다 (ThreatAgent 전용, M22-3차 W4) — **발동 판정의 유일한 지점**이다
        /// (ADR-M22-13: 두 번째 발동 경로 금지). 순서가 곧 규칙:
        ///   ① 등록부 조회 + 소멸 준비(TryTriggerTrapAt = Demolish 동형 — 계획 복귀 무장해제)
        ///   → ② 제거의 유일한 문(RemoveCountableAt) → ③ 판정은 CombatService 한 벌(TrapHit).
        /// 주민·방랑자는 여기 오지 않는다 — 함정이 아군을 해치지 않는 것은 분기가 아니라 구조다.
        /// </summary>
        public void NoteThreatEnteredTile(ThreatAgent agent, Vector2Int tile)
        {
            if (agent == null || agent.So == null || agent.IsExiting) return;
            // 함정 학습 (M24-1차 W5) — 이 종족이 예전에 밟아 봤으면 이번엔 피한다.
            // 함정은 그대로 남는다(소모되지 않는다): 안 밟았으니 발동한 적이 없다.
            // → 플레이어의 답은 "함정을 더 까는 것"이 아니라 **재배치**다.
            if (agent.HasTrait(InvaderTraitId.TrapLearning) && _trapped.Contains(agent.So.name)) return;
            if (_defense == null || !_defense.TryTriggerTrapAt(tile, out float damage)) return;
            _trapped.Add(agent.So.name); // 밟았다 = 배웠다 (다음 출몰부터 유효)

            _construction.RemoveCountableAt(SlotId.TrapCount, tile.x, tile.y); // 제거의 유일한 문 (ADR-M0-3)
            Debug.Log($"[Threat] 함정 발동 — ({tile.x},{tile.y}) {agent.So.DisplayName}에게 {damage:0.#} 피해");
            OnTrapTriggered?.Invoke(agent.So, tile, damage);
            _combat?.TrapHit(damage, agent); // 도주선·사냥·무리 판정까지 한 벌 (⚠️ ApplyHit 직접 호출 금지)
        }

        private enum SiegeTick { NotSiege, Struck, Skipped, Resumed }

        /// <summary>공성 틱 — 무리가 공성 중이면 이번 타격을 시설이 가져간다.
        /// 사거리 밖이면 이번 틱만 거른다 (배회가 다시 데려온다 — 밭형 '대상 소멸' 퇴장에 떨어지면 안 된다).</summary>
        private SiegeTick TickSiegeStrike(ThreatAgent agent, Vector2Int here)
        {
            if (!_groupSiege.TryGetValue(agent.GroupKey, out SiegeInfo s)) return SiegeTick.NotSiege;
            // 다른 개체가 이미 부쉈다 — 이 개체도 원래 타깃으로 (해제는 파괴 순간에 이미 됐다)
            if (_defense == null || !_defense.HasStructureAt(s.Slot, s.Tile))
            {
                ResumeAdvance(agent, s);
                return SiegeTick.Resumed;
            }
            if (Mathf.Abs(here.x - s.Tile.x) + Mathf.Abs(here.y - s.Tile.y) > agent.So.StrikeRadiusTiles)
                return SiegeTick.Skipped;

            float remain = _defense.ApplyDamage(s.Slot, s.Tile, agent.So.StructureDamage);
            agent.PlayAttack(s.Tile); // 울타리를 **치는 그림** (2026-08-10 — 공성만 몸짓이 빠져 있었다)
            _defense.TryGetDurability(s.Slot, s.Tile, out _, out float max);
            Debug.Log($"[Threat] 공성 — {agent.So.DisplayName}: ({s.Tile.x},{s.Tile.y}) 내구 {remain:0.#}/{max:0.#}");
            OnStructureStruck?.Invoke(agent.So, s.Slot, s.Tile, remain, max);
            if (remain <= 0f)
            {
                // 파괴 = 제거 + 통행 복구의 원자 (ADR-M22-6) — 제거의 유일한 문을 지난다 (ADR-M0-3).
                // OnRemoved 구독(SimulationLoop)이 통행 두 배열 복구 + 내구도 정리 + 계획 복귀를 잇는다.
                _construction.RemoveCountableAt(s.Slot, s.Tile.x, s.Tile.y);
                OnStructureDestroyed?.Invoke(agent.So, s.Slot, s.Tile);
                _groupSiege.Remove(agent.GroupKey);
                ResumeGroupAdvance(agent.GroupKey, s); // 뚫었다 — 무리 전원 원래 타깃으로
            }
            return SiegeTick.Struck;
        }

        /// <summary>돌파 후 무리 전원 재진격 (퇴장 중 개체 제외).</summary>
        private void ResumeGroupAdvance(int groupKey, SiegeInfo s)
        {
            _resumeBuf.Clear();
            foreach (ThreatAgent t in _active)
                if (t != null && t.GroupKey == groupKey && !t.IsExiting) _resumeBuf.Add(t);
            foreach (ThreatAgent t in _resumeBuf) ResumeAdvance(t, s);
        }

        /// <summary>개체 하나를 원래 타깃으로 되돌린다. 아직도 막혀 있으면(겹 방어) 다음 시설로
        /// 공성을 재개하고, 그마저 못 가면 퇴장 — 남은 체류가 시간의 값을 정한다 (ADR-M22-2).</summary>
        private void ResumeAdvance(ThreatAgent agent, SiegeInfo s)
        {
            if (agent == null || agent.IsExiting) return;
            Vector2Int target = s.OrigTarget;
            if (s.OrigTargetsVillagers && TryPickChaseTile(agent.TileX, agent.TileY, out Vector2Int chase))
                target = chase; // 주민형은 돌파 시점의 최근접 생존 주민 (스폰 때 좌표는 낡았다)

            PathResult p = _pathfinder().FindPath(agent.TileX, agent.TileY, target.x, target.y);
            if (p.Kind == PathResultKind.PathFound) { agent.ResumeAdvance(target, p.Waypoints); return; }
            if (p.Kind == PathResultKind.AlreadyThere) { agent.ResumeAdvance(target, null); return; }

            // 겹 방어 — 가장 가까운 다음 시설로 공성 재등록 (무리 공용 — 개체별 시설 분산은 2차+)
            if (_defense != null && agent.So.StructureDamage > 0f
                && _defense.TryGetNearestStructure(new Vector2Int(agent.TileX, agent.TileY),
                                                   out SlotId nslot, out Vector2Int ntile))
            {
                Vector2Int approach = ApproachTileOf(ntile, new Vector2Int(agent.TileX, agent.TileY));
                PathResult sp = _pathfinder().FindPath(agent.TileX, agent.TileY, approach.x, approach.y);
                if (sp.Kind != PathResultKind.Unreachable)
                {
                    _groupSiege[agent.GroupKey] = new SiegeInfo
                    {
                        Slot = nslot, Tile = ntile,
                        OrigTargetsVillagers = s.OrigTargetsVillagers, OrigTarget = s.OrigTarget,
                    };
                    agent.ResumeAdvance(approach,
                        sp.Kind == PathResultKind.PathFound ? sp.Waypoints : null);
                    return;
                }
            }
            BeginExit(agent, "닿지 못했다");
        }

        // ── 순수 판정 (게이트 M10-T3) ─────────────────────────────────────────

        /// <summary>마을 규모 (순수): 주민 + 밭 + 집 — 성장의 3축만 (모닥불 등 필수 건물 제외).</summary>
        public static int VillageScale(int aliveCount, int farmPlots, int houses)
            => aliveCount + farmPlots + houses;

        // ── 압력 (M24-1차 W3 — ADR-M24-1: 압력은 단조다) ──────────────────────
        //
        // 이 축이 존재하는 이유는 게임일 밴드(PickTier)를 대체하면서 **두 가지 실패를 동시에
        // 피하는 것**이다. 둘은 정반대이고 둘 다 실제로 관측된 적이 있다:
        //
        //   ① 사망 → 마을 축소 → 위협 강등 → 영구 안정   (ADR-M10R-1이 고친 음성 피드백 루프)
        //   ② 사망 → 압력 유지 → 회복 불가 → 남은 판이 소화 시간  (①의 거울상)
        //
        // ①은 **역대 최고 인구**를 읽어 막는다(현재 인구가 아니다). ②는 압력을 낮춰서가 아니라
        // 자비 장치(W9 — 방랑자 유입 증가)로 푼다. **압력은 어떤 경로로도 내려가지 않는다.**

        /// <summary>전역압력 (순수 — 게이트 M24-T2). 예산·스탯 곡선의 단일 눈금.
        /// 두 항 모두 단조 증가라 절대 줄지 않는다: 게임일은 시간이고, 인구는 **역대 최고**다.
        /// 🔴 <paramref name="peakPopulation"/>에 현재 인구를 넘기면 `ADR-M24-1` 정면 위반이다 —
        /// 그 순간 "주민이 죽으면 게임이 쉬워진다"가 되어 M10R 버그가 그대로 돌아온다.</summary>
        public static int GlobalPressure(float gameDay, int peakPopulation,
                                         float daysPerPoint, int popPerPoint)
        {
            int byTime = Mathf.FloorToInt(Mathf.Max(0f, gameDay) / Mathf.Max(1e-4f, daysPerPoint));
            int byGrowth = Mathf.Max(0, peakPopulation) / Mathf.Max(1, popPerPoint);
            return byTime + byGrowth;
        }

        /// <summary>종족 유효압력 (순수 — 게이트 M24-T2). 그 종족의 **스탯 배율과 전략 해금**이
        /// 읽는 값. 전역압력이 바닥을 깔고, 많이 만난 종족만 그만큼 앞서 나간다.
        /// k=0이면 전역압력과 같다 (중립 — 적응하지 않는 종족).</summary>
        public static int EffectivePressure(int globalPressure, int encounters, float k)
            => globalPressure + Mathf.FloorToInt(Mathf.Max(0, encounters) * Mathf.Max(0f, k));

        /// <summary>이 종족을 몇 번 만났는가 (출몰 기준 — ADR-M24-5).</summary>
        public int EncountersOf(ThreatSO so)
            => so != null && _encounters.TryGetValue(so.name, out int n) ? n : 0;

        /// <summary>종족별 조우 중 최대 — 파생 슬롯 `ThreatEncounterMax`의 유일한 원천.</summary>
        public int MaxEncounters()
        {
            int max = 0;
            foreach (int n in _encounters.Values) if (n > max) max = n;
            return max;
        }

        /// <summary>지금의 전역압력 (설정 계수 적용). 원천은 위 순수 함수 하나뿐이다.</summary>
        public int CurrentGlobalPressure(int peakPopulation)
            => _config == null ? 0
             : GlobalPressure(_gameTime, peakPopulation,
                              _config.PressureDaysPerPoint, _config.PressurePopPerPoint)
#if UNITY_EDITOR
               + _debugPressureBonus
#endif
             ;

#if UNITY_EDITOR
        // 디버그 압력 부스트 (2026-08-10 신설) — S8("종족별로 다른 대비를 하게 되는가")을
        // 재는 도구다. 특성 해금이 압력 6~22에 걸려 있는데 초반 판의 압력은 5 안팎이라,
        // 부스트 없이는 **네 종족이 전부 정면으로 오는 구간만** 관측된다 (2026-08-10 Play).
        private int _debugPressureBonus;
#endif

#if UNITY_EDITOR
        /// <summary>압력을 올린다 (에디터 전용). 새 값을 돌려준다.
        /// 🔴 **되돌릴 수 없다** — `ADR-M24-1`(압력은 어떤 경로로도 안 내려간다)을 디버그에서도
        /// 지킨다. 내리는 키를 만들면 "압력이 내려가는 경로"가 코드에 생기고, 그 순간 게이트가
        /// 지키는 계약이 거짓말이 된다. 낮은 압력을 다시 보려면 **새 판을 시작한다.**
        /// 🔑 이 값은 세이브 대상이 아니다 — 디버그 상태는 판을 이어 받지 않는다.</summary>
        public int DebugBoostPressure(int amount)
        {
            _debugPressureBonus += Mathf.Max(0, amount);
            return CurrentGlobalPressure(_peakPopulation());
        }
#endif

        // ── 편성 구매 (M24-1차 W4 — 舊 PickTier의 자리) ───────────────────────
        //
        // 舊 구조는 "활성 밴드 1개 = 이번에 오는 것"이었다(PickTier). 밴드는 게임일 래칫으로
        // 한 번 열리면 안 닫혔고, 그래서 **후반엔 초반 종족을 다시 못 봤다**.
        // 예산제는 그 자리를 바꾼다: 압력이 점수를 주고, 스케줄러가 그 점수로 개체를 산다.
        // 후반 고블린은 사라지는 대신 **여럿이 되어** 돌아온다.

        /// <summary>이번 웨이브의 한 칸 — 어느 종족의 몇 등급이 몇 마리인가.</summary>
        public readonly struct WaveEntry
        {
            public readonly ThreatSO Race;
            public readonly int Grade;   // Race.Grades 인덱스 (Grades가 비면 0 = 기본)
            public readonly int Count;
            public WaveEntry(ThreatSO race, int grade, int count) { Race = race; Grade = grade; Count = count; }
        }

        /// <summary>등급 비용 (순수) — Grades 미배선이면 1 (중립: 예산 = 마릿수).</summary>
        public static int GradeCost(ThreatSO so, int grade)
            => so?.Grades == null || so.Grades.Length == 0 ? 1
             : Mathf.Max(1, so.Grades[Mathf.Clamp(grade, 0, so.Grades.Length - 1)].PointCost);

        /// <summary>등급 배율 (순수) — Grades 미배선이면 1 (중립: 종족 기본 스탯 그대로).</summary>
        public static float GradeMult(ThreatSO so, int grade)
            => so?.Grades == null || so.Grades.Length == 0 ? 1f
             : Mathf.Max(0.01f, so.Grades[Mathf.Clamp(grade, 0, so.Grades.Length - 1)].StatMult);

        /// <summary>해금된 종족만 (순수) — UnlockDay ≤ day. 배열 순서 보존(결정성).</summary>
        public static List<ThreatSO> UnlockedRaces(ThreatSO[] races, float day, ThreatSO exclude = null)
        {
            var list = new List<ThreatSO>(4);
            if (races == null) return list;
            foreach (ThreatSO r in races)
                if (r != null && r != exclude && r.UnlockDay <= day) list.Add(r);
            return list;
        }

        /// <summary>
        /// 이번 웨이브 편성 (순수·결정적 — 게이트 M24-T5). 시드는 출몰 서수 (ADR-M10R-2 계승:
        /// 같은 판 같은 서수 = 같은 편성).
        ///
        /// 절차: ①종족 수를 뽑는다(압력이 클수록 여럿) ②예산을 나눈다
        ///       ③각 종족은 **살 수 있는 가장 비싼 등급 1(지휘관) + 남은 예산을 최하 등급으로 채움**
        ///
        /// 🔴 "비싼 것부터 계속 산다"로 짜면 **항상 최고 등급 1마리**만 나와 편성이 하나로 굳는다
        /// (예산제의 고전적 함정 — 게이트 M24-T5가 감시). 지휘관을 하나만 사는 이유가 그것이고,
        /// 남은 예산을 버리지 않고 최하 등급으로 채우는 이유는 압력이 올라도 마릿수가 안 늘면
        /// 곡선이 계단으로 끊기기 때문이다.
        /// </summary>
        public static List<WaveEntry> BuyWave(IReadOnlyList<ThreatSO> races, int budget, int ordinal)
        {
            var wave = new List<WaveEntry>(3);
            if (races == null || races.Count == 0 || budget <= 0) return wave;

            // ① 종족 수 — 예산이 커질수록 섞인다. 상한은 해금된 종족 수.
            int kinds = Mathf.Clamp(1 + budget / 8, 1, races.Count);

            // 시작 종족을 서수로 회전시킨다 — 같은 예산이라도 서수가 다르면 다른 조합이 나온다.
            uint h = StableHash.Fnv1a(ordinal.ToString(), "wave");
            int start = races.Count == 0 ? 0 : (int)(h % (uint)races.Count);

            int left = budget;
            for (int i = 0; i < kinds && left > 0; i++)
            {
                ThreatSO race = races[(start + i) % races.Count];
                // ② 남은 종족 수로 예산을 나눈다 (마지막 종족이 잔액을 전부 받는다)
                int share = i == kinds - 1 ? left : Mathf.Max(1, left / (kinds - i));

                // ③ 지휘관 1 — share 로 살 수 있는 가장 비싼 등급
                int top = -1, topCost = 0;
                int gradeCount = race.Grades == null || race.Grades.Length == 0 ? 1 : race.Grades.Length;
                for (int g = 0; g < gradeCount; g++)
                {
                    int c = GradeCost(race, g);
                    if (c <= share && c >= topCost) { top = g; topCost = c; }
                }
                if (top < 0) continue;      // 최하 등급조차 못 산다 — 이 종족은 이번엔 안 온다
                left -= topCost;
                share -= topCost;

                // 졸개 — 최하 등급(0)으로 남은 몫을 채운다
                int baseCost = GradeCost(race, 0);
                int minions = top == 0 ? 0 : share / Mathf.Max(1, baseCost);
                left -= minions * baseCost;

                if (top != 0) wave.Add(new WaveEntry(race, top, 1));
                if (minions > 0) wave.Add(new WaveEntry(race, 0, minions));
                if (top == 0) wave.Add(new WaveEntry(race, 0, 1));
            }
            return wave;
        }

        // ── 침입 특성 (M24-1차 W5 — ADR-M24-4) ───────────────────────────────

        /// <summary>이 압력에서 이 종족이 쓰는 수 (순수 — 게이트 M24-T4).
        /// Traits 미배선이면 빈 배열 = 배선 전과 동일한 동작 (중립 불변식).</summary>
        public static InvaderTraitId[] ActiveTraits(ThreatSO so, int effectivePressure)
        {
            if (so?.Traits == null || so.Traits.Length == 0) return Array.Empty<InvaderTraitId>();
            int n = 0;
            foreach (InvaderTrait t in so.Traits)
                if (t.Id != InvaderTraitId.None && t.UnlockPressure <= effectivePressure) n++;
            if (n == 0) return Array.Empty<InvaderTraitId>();
            var arr = new InvaderTraitId[n];
            int i = 0;
            foreach (InvaderTrait t in so.Traits)
                if (t.Id != InvaderTraitId.None && t.UnlockPressure <= effectivePressure) arr[i++] = t.Id;
            return arr;
        }

        /// <summary>특성 목록에 이 수가 있는가 (순수).</summary>
        public static bool Has(InvaderTraitId[] traits, InvaderTraitId id)
        {
            if (traits == null) return false;
            foreach (InvaderTraitId t in traits) if (t == id) return true;
            return false;
        }

        /// <summary>이 종족의 지금 유효압력 — 전역압력 + 조우 가산 (W3의 두 함수를 잇는 자리).</summary>
        public int PressureOf(ThreatSO so)
            => so == null ? 0
             : EffectivePressure(CurrentGlobalPressure(_peakPopulation()),
                                 EncountersOf(so), so.EncounterPressureK);

        /// <summary>이 종족이 함정을 밟아 본 적이 있는가 — `TrapLearning`의 유일한 원천.
        /// 세이브 대상 (ADR-M0-10): "배웠다"는 판을 이어 받아도 유지돼야 한다.</summary>
        public bool HasTastedTrap(ThreatSO so) => so != null && _trapped.Contains(so.name);

        /// <summary>단독 웨이브 종족 (순수) — `SoloWave` 표시가 붙은 첫 종족. 없으면 null.
        /// 코드에 "악마"를 박지 않기 위한 조회다 (ADR-M24-4).</summary>
        public static ThreatSO SoloRace(ThreatSO[] races)
        {
            if (races == null) return null;
            foreach (ThreatSO r in races) if (r != null && r.SoloWave) return r;
            return null;
        }

        /// <summary>편성의 주력 (순수) — 점수를 가장 많이 쓴 종족. 동률은 앞선 칸 (결정적).</summary>
        public static ThreatSO PrimaryOf(IReadOnlyList<WaveEntry> wave)
        {
            if (wave == null) return null;
            ThreatSO best = null; int bestSpend = -1;
            foreach (WaveEntry e in wave)
            {
                int total = 0;                      // 같은 종족이 여러 칸(지휘관+졸개)이라 합산한다
                foreach (WaveEntry f in wave)
                    if (f.Race == e.Race) total += GradeCost(f.Race, f.Grade) * f.Count;
                if (total > bestSpend) { bestSpend = total; best = e.Race; }
            }
            return best;
        }

        /// <summary>편성 한 줄 요약 (로그·예고용).</summary>
        public static string Describe(IReadOnlyList<WaveEntry> wave)
        {
            if (wave == null || wave.Count == 0) return "(빈 편성)";
            var sb = new System.Text.StringBuilder(48);
            for (int i = 0; i < wave.Count; i++)
            {
                WaveEntry e = wave[i];
                string grade = e.Race.Grades != null && e.Race.Grades.Length > e.Grade
                             && !string.IsNullOrEmpty(e.Race.Grades[e.Grade].DisplayName)
                             ? " " + e.Race.Grades[e.Grade].DisplayName : "";
                if (i > 0) sb.Append(" · ");
                sb.Append(e.Race.DisplayName).Append(grade);
                if (e.Count > 1) sb.Append(" ×").Append(e.Count);
            }
            return sb.ToString();
        }

        /// <summary>예고 문구 (순수 — 게이트 M24-T11, W8). `Describe`의 화면판:
        /// **종족 이름만** 공개하고 등급도 마릿수도 감춘다.
        ///
        /// 🔴 마릿수는 **어떤 조건에서도 공개하지 않는다** (2026-08-10 사용자 결정 — 명세의
        /// "망루 탑승 중이면 마릿수까지"는 폐기). 실사해 보니 탑승 트리거가 `ThreatNear`라
        /// 예고 구간에는 탑승 주민이 **구조적으로 0명**이었다(예고 1일 전 · 체류 0.75일 ·
        /// 주기 5일 → 예고 시점의 활성 위협은 언제나 0). 조건을 다른 것으로 갈아 끼우는 대신
        /// **기능을 접었다**: 망루의 값은 정찰 정보가 아니라 **요격**이다.
        ///
        /// 🔑 한 종족이 지휘관·졸개 두 칸으로 나뉘어 있으므로 **종족 단위로 묶는다**
        /// (`Spawn`의 조우 카운트와 같은 규약 — 칸이 아니라 종족이 단위다).
        /// 순서는 편성 순서 그대로 = 예산을 많이 쓴 주력이 앞에 온다.</summary>
        public static string DescribeForecast(IReadOnlyList<WaveEntry> wave)
        {
            if (wave == null || wave.Count == 0) return "";
            var sb = new System.Text.StringBuilder(32);
            for (int i = 0; i < wave.Count; i++)
            {
                ThreatSO race = wave[i].Race;
                if (race == null) continue;
                bool first = true;
                for (int j = 0; j < i; j++)
                    if (wave[j].Race == race) { first = false; break; } // 앞에 있었다 = 이미 적었다
                if (!first) continue;
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(race.DisplayName);
            }
            return sb.ToString();
        }

        /// <summary>S8 판정 한 줄 (2026-08-10 신설) — **로그만 보고 "종족이 다르게 구는가"를
        /// 판정할 수 있게** 한다. 화면 관측은 사람의 시간이고 재현이 안 되지만, 이 줄은 판을
        /// 이어 붙여 표로 만들 수 있다.
        ///
        /// 🔑 이 로그가 필요해진 이유: S8 1차 Play(2026-08-10)가 **관측 불가**로 끝났는데,
        /// 원인이 "차이가 없다"가 아니라 **"압력이 낮아 특성이 하나도 안 열려 있었다"**였다.
        /// 눈으로는 그 둘이 똑같아 보인다 — 열린 수를 찍어야 구분된다.
        /// grep 태그는 `[S8]`.</summary>
        private void LogVerdictLine(string phase, ThreatSO so, IReadOnlyList<WaveEntry> wave,
                                    int entryCount, string target)
        {
            int global = CurrentGlobalPressure(_peakPopulation());
            int eff = PressureOf(so);
            InvaderTraitId[] traits = ActiveTraits(so, eff);
            var sb = new System.Text.StringBuilder(160);
            sb.Append("[S8] ").Append(phase)
              .Append(" 서수=").Append(_strikeOrdinal)
              .Append(" 종족=").Append(so.DisplayName)
              .Append(" 전역압력=").Append(global)
              .Append(" 유효=").Append(eff)
              .Append(" 조우=").Append(EncountersOf(so))
              .Append(" 수=[");
            for (int i = 0; i < traits.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(traits[i]);
            }
            sb.Append(']');
            if (traits.Length == 0) sb.Append("  ← 열린 수 0 (이 구간은 전 종족이 똑같이 군다)");
            sb.Append(" 편성=").Append(Describe(wave));
            if (entryCount > 0) sb.Append(" 진입=").Append(entryCount).Append("곳");
            if (!string.IsNullOrEmpty(target)) sb.Append(" 타깃=").Append(target);
            Debug.Log(sb.ToString());
        }

        /// <summary>단독 웨이브 간격 — 미배선이면 25 (에셋 기본값과 같은 수, 중립).</summary>
        private float SoloEvery => _config != null && _config.DemonWaveEveryDays > 0f
                                 ? _config.DemonWaveEveryDays : 25f;

        /// <summary>악마 웨이브 마릿수 (순수 — 게이트 M24-T5). 게임일 함수라 단조다.</summary>
        public static int DemonCount(float gameDay, float firstDay, float growEveryDays, int max)
            => growEveryDays <= 0f
             ? Mathf.Clamp(1, 1, Mathf.Max(1, max))
             : Mathf.Clamp(1 + Mathf.FloorToInt(Mathf.Max(0f, gameDay - firstDay) / growEveryDays),
                           1, Mathf.Max(1, max));

        /// <summary>이번 출몰이 주민을 노리는가 (순수·결정적, ADR-M10R-2·3): 출몰 서수 시드로 [0,1) 분수를
        /// 만들어 chance와 비교. 진입점 시드와 다른 솔트("|tgt")로 상관 차단 (동쪽=항상 주민 같은 편향 방지).
        /// 0=항상 밭, 1=항상 주민. 매 출몰 1회만 롤한다 (ADR-M10R-4 — 재타겟은 종류를 안 바꾼다).
        /// 에셋 기저 확률판 — 계절 보정 없는 호출자·게이트용.</summary>
        public static bool RollTargetsVillagers(ThreatSO so, int ordinal)
            => RollTargetsVillagers(so, ordinal, so.VillagerTargetChance);

        /// <summary>계절 보정 확률로 롤하는 판 (M21-W2R). **시드는 그대로다** — 계절이 바꾸는 것은
        /// 확률값뿐이고 난수원이 아니다 (ADR-M10R-2 결정성 불변: 같은 판 같은 서수 = 같은 결과).
        /// ADR-M21-10 경계의 판정 쪽: "무엇을 노리는가"는 끝까지 결정적이다.</summary>
        public static bool RollTargetsVillagers(ThreatSO so, int ordinal, float chance)
        {
            if (chance <= 0f) return false;
            if (chance >= 1f) return true;
            uint h = StableHash.Fnv1a(ordinal.ToString(), so.DisplayName + "|tgt");
            float r = (h & 0xFFFFFFu) / (float)0x1000000; // [0,1)
            return r < chance;
        }

        // ── 계절 연동 (M21-W2R — ADR-M21-9) ──────────────────────────────────

        /// <summary>이 계절에 침입자가 궁해지는가 (순수 — 게이트 M21-T7). 들에서 걷을 것이 없는
        /// 계절이면 그들도 굶고, 그래서 마을 쪽으로 기운다.
        /// 🔴 IsCrisis가 **아니다**: Season_Summer도 IsCrisis:1 이라 그걸 쓰면 여름 위협이 사나워진다.
        /// 겨울만 참인 것은 ForageFrozen이고, 의미도 정확히 겹치므로 새 필드를 만들지 않는다 (기존 통로 우선).
        /// ⚠️ 갈라지는 조건: "채집은 막히는데 침입자는 안 궁한" 계절이 생기면 그때 전용 필드로 분리한다.
        /// 📌 M24-1차 이월: 메서드 이름 `IsPredatorHungry`("포식자")는 종족 전환 뒤 부정확하다 —
        /// 게이트가 참조하는 공개 API라 개명은 별도 항목으로 남긴다.</summary>
        public static bool IsPredatorHungry(SeasonSO s) => s != null && s.ForageFrozen;

        /// <summary>계절 반영 주민 타깃 확률 (순수 — 게이트 M21-T7). 클램프 [0,1].
        /// 배율 1 미만은 무시한다 — 배고픈 계절이 오히려 순해지는 방향은 이 축의 뜻이 아니다.</summary>
        public static float EffectiveVillagerChance(float baseChance, bool hungry, float mult)
            => Mathf.Clamp01(hungry ? baseChance * Mathf.Max(1f, mult) : baseChance);

        /// <summary>계절 반영 체류 상한 (순수 — 게이트 M21-T7). 배율 1 미만은 무시 (위와 같은 이유).</summary>
        public static float EffectiveStayDays(float baseDays, bool hungry, float mult)
            => hungry ? baseDays * Mathf.Max(1f, mult) : baseDays;

        /// <summary>지금이 배고픈 계절인가 — 인스턴스 창구. 계절 서비스가 없는 판은 false (중립).</summary>
        private bool PredatorHungryNow => IsPredatorHungry(_season != null ? _season.Current : null);

        // ── 정찰 연장 (M21-W8) ────────────────────────────────────────────────

        /// <summary>정찰 예고 연장 (순수 — 게이트 M21-T13): 직업들의 WarnBonusDays **최댓값**.
        /// ⚠️ 합산 금지 (인원 비례 = 정찰꾼 몰빵 메타 — 명세 W8 ⚠️①). 1명이면 족하다.</summary>
        public static float MaxWarnBonus(IEnumerable<JobSO> jobs)
        {
            float best = 0f;
            if (jobs != null)
                foreach (JobSO j in jobs)
                    if (j != null && j.WarnBonusDays > best) best = j.WarnBonusDays;
            return best;
        }

        /// <summary>생존 주민 기준 정찰 연장 — 미배선(테스트 등)이면 0 (중립: 기존 예고 그대로).</summary>
        private float ScoutWarnBonus()
        {
            if (_agents == null) return 0f;
            float best = 0f;
            foreach (VillagerAgent a in _agents)
            {
                if (a == null || a.State == AgentState.Dead || a.Job == null) continue;
                if (a.Job.WarnBonusDays > best) best = a.Job.WarnBonusDays;
            }
            return best;
        }

        /// <summary>스폰 마릿수 산식 (순수 — 게이트 M21-T11, §4): 기본 + 밴드가 열린 뒤 흐른
        /// 시간의 성장 − 완충, [1, Max] 클램프. 성장 주기 0 이하 = 성장 없음 (에셋 사고 방어).
        /// mitigation 은 W7 래칫 완충의 자리 — W6 은 항상 0 을 넣는다 (자리만 먼저 판다:
        /// W7 이 인자만 갈아 끼우면 되게. FightRunner 의 배율 인자와 같은 수법).</summary>
        public static int SpawnCount(int baseCount, float unlockDay, float day,
                                     float growthEveryDays, int maxCount, int mitigation)
        {
            int grown = growthEveryDays > 0f
                ? Mathf.FloorToInt(Mathf.Max(0f, day - unlockDay) / growthEveryDays) : 0;
            return Mathf.Clamp(baseCount + grown - mitigation, 1, Mathf.Max(1, maxCount));
        }

        /// <summary>가장자리 진입점 (순수·결정적): 시드로 4변 중 택1, 변 중앙. 같은 시드 = 같은 지점.</summary>
        public static Vector2Int EntryPoint(uint seed, int minX, int maxX, int minY, int maxY)
        {
            int midX = (minX + maxX) / 2, midY = (minY + maxY) / 2;
            switch (seed % 4u)
            {
                case 0u: return new Vector2Int(minX, midY); // 서
                case 1u: return new Vector2Int(maxX, midY); // 동
                case 2u: return new Vector2Int(midX, minY); // 남
                default: return new Vector2Int(midX, maxY); // 북
            }
        }

        // ── 진입점 규칙 (M24-1차 W7 — 우회·다지점) ────────────────────────────

        /// <summary>다지점 진입 1곳이 맡는 마릿수 (M24-1차 W7). 6마리면 3곳, 2마리면 2곳.
        /// 에셋 값이 아닌 이유: 이건 종족의 성격이 아니라 **"나눈다"의 정의**다 (특성 자체가
        /// 종족별 데이터이므로, 나누는 잘기까지 종족마다 다르게 둘 이유가 없다).</summary>
        private const int MultiPointPerEntry = 2;

        /// <summary>다지점 진입의 최소 갈래 — 나눈다면 최소 둘이다 (하나면 안 나눈 것).</summary>
        private const int MultiPointMinEntries = 2;

        /// <summary>맵 네 변의 수 (진입점 후보 수 = 변 인덱스의 개수). `EntryPoint`의 시드 규약과 짝.</summary>
        private const int EdgeCount = 4;

        /// <summary>
        /// 망루가 지키는 변을 표시한다 (순수 — W7R). 인덱스는 `EntryPoint`의 시드 규약과 **같은 자**:
        /// 0 서 · 1 동 · 2 남 · 3 북. 판정은 **마을 중심에서 본 방향**이다 —
        /// |dx| &gt; |dy| 면 동·서, |dy| &gt; |dx| 면 남·북, 같으면 대각이라 **두 변을 함께** 지킨다.
        /// 중심에 정확히 선 망루(dx=dy=0)는 방향이 없어 어느 변도 안 지킨다.
        ///
        /// 🔴 **왜 사거리가 아닌가** (2026-08-10 S8 2차 판정, 안 A): 진입점은 맵 네 변 중앙(±50)이고
        /// 망루는 마을 둘레(±8)라 실측 거리가 43~56타일이었다 — 망루 사거리 6으로는 진입점이
        /// 감시 안에 들어올 수가 없어 이 특성은 **구조적으로 발동 불가**였다. 플레이어가 마을에서
        /// 50타일 떨어진 맵 끝에 망루를 지을 이유도 없다. 그래서 축을 거리에서 **방향**으로 옮긴다:
        /// 북쪽에 망루를 세우면 적이 남쪽으로 돈다 — 사거리와 무관하게 읽히는 인과다.
        /// (기각한 안 B = 진입점을 마을 둘레로 옮기기: 근본적이나 M10부터의 진입점 규약을 바꾼다.)
        /// </summary>
        private static bool[] WatchedEdges(IReadOnlyList<Vector2Int> towers, Vector2Int villageCenter)
        {
            var watched = new bool[EdgeCount];
            if (towers == null) return watched;
            for (int i = 0; i < towers.Count; i++)
            {
                int dx = towers[i].x - villageCenter.x, dy = towers[i].y - villageCenter.y;
                int ax = Mathf.Abs(dx), ay = Mathf.Abs(dy);
                if (ax == 0 && ay == 0) continue;          // 중심 = 방향 없음 (어느 변도 안 지킨다)
                if (ax >= ay) watched[dx > 0 ? 1 : 0] = true;   // 동 : 서
                if (ay >= ax) watched[dy > 0 ? 3 : 2] = true;   // 북 : 남  (대각이면 위와 함께 참)
            }
            return watched;
        }

        /// <summary>
        /// 진입점들 (순수·결정적 — 게이트 M24-T10). 특성이 **후보를 거를 뿐** 새 계산을 하지 않는다:
        ///   `ApproachFlank`      → 망루가 지키는 **변**의 후보를 뒤로 미룬다 (배제가 아니다)
        ///   `ApproachMultiPoint` → 마릿수를 진입점 여럿에 나눈다
        /// 특성이 없으면 길이 1 배열이고 그 한 칸은 `EntryPoint`와 **같은 좌표**다 (중립 불변식).
        ///
        /// 🔴 우회는 배제가 아니다 — 전부 피하면 망루가 무용지물이 된다. 네 변이 모두 감시되면
        /// 순서가 그대로라 기존 동작으로 돌아가고, 그때 망루는 제 값을 한다 (명세 W7 ⚠️).
        /// </summary>
        /// <param name="towers">완공 망루 타일. **탑승 여부를 묻지 않는다** — 탑승은 위협이 나타난
        /// 뒤에 일어나므로(ManTower 트리거 = ThreatNear) 출몰 시점엔 언제나 0기다. 탑승을 조건으로
        /// 걸면 이 수는 영영 안 발동한다. 그래서 여기서 감시 = **서 있는 망루**이고, 답도 그것과
        /// 짝을 이룬다: 답은 "사람을 더 태워라"가 아니라 **"망루를 옮겨라"**다.</param>
        /// <param name="villageCenter">마을 중심 타일 (원천 = `WorldConfigSO.BaseTileX/Y`, 집·둘레의
        /// 기준과 같은 자). 망루의 **방향**이 여기서 나온다 — W7R 이 사거리를 버리고 잡은 축이다.</param>
        /// <param name="count">이번 편성의 총 마릿수 (다지점 갈래 수의 상한 — 1마리는 못 나눈다).</param>
        public static Vector2Int[] EntryPoints(uint seed, int minX, int maxX, int minY, int maxY,
                                               InvaderTraitId[] traits, IReadOnlyList<Vector2Int> towers,
                                               Vector2Int villageCenter, int count)
        {
            // 후보 4변 — [0]은 舊 EntryPoint 와 같은 좌표(시드 그대로), 뒤는 변 순서로 회전.
            // 산식을 복제하지 않고 그 함수를 네 번 부른다 (진입점의 정의는 한 곳에 있다).
            // edge[k] = 그 후보가 선 변 (좌표에서 되짚지 않는다 — 시드 규약이 곧 변이다).
            var cand = new Vector2Int[EdgeCount];
            var edge = new int[EdgeCount];
            for (int k = 0; k < EdgeCount; k++)
            {
                cand[k] = EntryPoint(seed + (uint)k, minX, maxX, minY, maxY);
                edge[k] = (int)((seed + (uint)k) % (uint)EdgeCount);
            }

            if (Has(traits, InvaderTraitId.ApproachFlank))
            {
                // 지켜지지 않는 변을 앞으로, 지켜지는 변을 뒤로 — **순서만** 바꾼다 (안정 분할:
                // 같은 편 안에서는 시드 순서 보존). 전부 감시거나 전부 밖이면 순서 그대로 = 기존 동작.
                bool[] watched = WatchedEdges(towers, villageCenter);
                var sorted = new Vector2Int[EdgeCount];
                int w = 0;
                for (int k = 0; k < EdgeCount; k++) if (!watched[edge[k]]) sorted[w++] = cand[k];
                for (int k = 0; k < EdgeCount; k++) if (watched[edge[k]]) sorted[w++] = cand[k];
                cand = sorted;
            }

            int n = 1;
            if (Has(traits, InvaderTraitId.ApproachMultiPoint))
                n = Mathf.Clamp(count / MultiPointPerEntry, MultiPointMinEntries, cand.Length);
            n = Mathf.Clamp(n, 1, Mathf.Max(1, count)); // 마릿수보다 많은 갈래는 빈 진입점이 된다

            if (n >= cand.Length) return cand;
            var result = new Vector2Int[n];
            Array.Copy(cand, result, n);
            return result;
        }

        /// <summary>
        /// 주민 희생 선정 (순수·결정적): 거리 제곱 오름차순 앞 count개, 동률은 id 사전순(ordinal).
        /// 재해의 시드 셔플과 달리 거리순인 이유 — 부상은 "도망치지 않아서"의 결과여야 서사가
        /// 성립한다 (명세 M10-C ⚠️①). 후보 제외(기존 부상자·Dead)는 호출자가 수집 시 수행.
        /// </summary>
        public static void PickNearestVictims(int fromX, int fromY,
                                              IReadOnlyList<(string id, int x, int y)> candidates,
                                              int count, List<int> result)
        {
            result.Clear();
            if (count <= 0) return;
            for (int i = 0; i < candidates.Count; i++) result.Add(i);
            result.Sort((a, b) =>
            {
                long da = Dist2(candidates[a], fromX, fromY), db = Dist2(candidates[b], fromX, fromY);
                if (da != db) return da.CompareTo(db);
                return string.CompareOrdinal(candidates[a].id, candidates[b].id);
            });
            if (result.Count > count) result.RemoveRange(count, result.Count - count);
        }

        private static long Dist2((string id, int x, int y) c, int fromX, int fromY)
        {
            long dx = c.x - fromX, dy = c.y - fromY;
            return dx * dx + dy * dy;
        }

        // ── 스케줄 (SimulationLoop 틱) ────────────────────────────────────────

        public void Tick(float gameTime)
        {
            _gameTime = gameTime; // 체류 개체의 시계 (M21-W2) — 아래 조기 반환보다 먼저 갱신할 것

            // 상주 예약 (M26-2차 W5R) — 🔴 웨이브 스케줄의 조기 반환(종족 미해금 등)보다 **먼저**:
            // 상주는 웨이브가 아니라서 그 정지에 같이 멎으면 안 된다.
            TickResidentReservations();

            // 예고 진입 — 밴드는 예고 시점 게임일로 확정하고 발동까지 고정 (예고한 그놈이 온다).
            // 지연(_delayDays, M21-W7)은 예고·발동 **둘 다**에 얹는다 — 예고만 제때 나가고 발동이
            // 늦으면 "1일 후"가 거짓말이 된다 (예고 문구와 실제 간격은 한 값이어야 한다).
            if (_pendingWave == null)
            {
                // 해금 판정만 게임일 래칫을 그대로 쓴다 (ADR-M10R-1) — 열린 종족은 안 닫힌다.
                ThreatSO solo = SoloRace(_threats);
                List<ThreatSO> pool = UnlockedRaces(_threats, gameTime, solo);
                // 단독 웨이브: 해금되면 첫 회, 이후 DemonWaveEveryDays 마다 그 회차를 통째로 가져간다.
                bool soloDue = solo != null && gameTime >= solo.UnlockDay
                             && (_lastSoloDay < 0f || gameTime >= _lastSoloDay + SoloEvery);
                if (!soloDue && pool.Count == 0) return; // 아직 아무 종족도 안 열렸다 (스케줄 정지 — 중립)

                // 정찰 연장 (M21-W8) — 생존 정찰꾼이 있으면 예고가 이르다. 발동 시각은 불변 —
                // 정찰이 바꾸는 것은 "언제 오는가"가 아니라 **"언제 아는가"**다.
                ThreatSO warnSrc = soloDue ? solo : pool[0];
                float warn = warnSrc.WarnDays + ScoutWarnBonus();
                // 야습 (M24-1차 W5, ShortWarning) — 예고가 짧아진다. **발동 시각은 그대로다**:
                // 이 수가 바꾸는 것은 "언제 오는가"가 아니라 정찰의 거울상, "언제 아는가"다.
                if (Has(ActiveTraits(warnSrc, PressureOf(warnSrc)), InvaderTraitId.ShortWarning))
                    warn = Mathf.Max(0f, warn * 0.4f);
                if (gameTime < _lastStrikeDay + WavePeriod + _delayDays - warn) return;

                // 편성 확정 (ADR-M24-3). 완충(M21-W7)은 **예산에서** 깎는다 — 舊 구조는 마릿수를
                // 깎았는데, 예산제에서는 편성 자체가 예고 시점에 굳으므로 그 전에 반영해야
                // 예고가 진실을 말한다 ("완충으로 작아진 편성"을 예고가 보여준다).
                int relief = _reliefStacks;
                _reliefStacks = 0;
                if (soloDue)
                {
                    int n = DemonCount(gameTime, solo.UnlockDay,
                                       _config.DemonCountGrowthDays, _config.DemonCountMax);
                    _pendingWave = new List<WaveEntry> { new WaveEntry(solo, 0, Mathf.Max(1, n - relief)) };
                    _pendingPrimary = solo;
                    _pendingSolo = true;
                }
                else
                {
                    int budget = Mathf.Max(1, CurrentGlobalPressure(_peakPopulation()) - relief);
                    _pendingWave = BuyWave(pool, budget, _strikeOrdinal + 1);
                    if (_pendingWave.Count == 0) { _pendingWave = null; return; } // 아무것도 못 샀다
                    _pendingPrimary = PrimaryOf(_pendingWave);
                    _pendingSolo = false;
                }

                float lead = _lastStrikeDay + WavePeriod + _delayDays - gameTime;
                Debug.Log($"[Threat] 예고 — {Describe(_pendingWave)} ({lead:0.#}일 후" +
                          $"{(warn > warnSrc.WarnDays ? " · 정찰 연장" : "")}" +
                          $"{(relief > 0 ? $" · 완충 −{relief}" : "")})");
                LogVerdictLine("예고", _pendingPrimary, _pendingWave, 0, null);
                OnForecast?.Invoke(_pendingPrimary);
                return;
            }

            // 발동
            if (gameTime >= _lastStrikeDay + WavePeriod + _delayDays)
            {
                _lastStrikeDay = gameTime;
                List<WaveEntry> striking = _pendingWave;
                ThreatSO primary = _pendingPrimary;
                if (_pendingSolo) _lastSoloDay = gameTime;
                _pendingWave = null; _pendingPrimary = null; _pendingSolo = false;
                Spawn(primary, striking);
            }
        }

#if UNITY_EDITOR
        // ── 디버그 소환 (2026-08-10, 에디터 전용) ──────────────────────────────
        //
        // 계기: 습격은 게임일이 쌓여야 오는데(첫 웨이브까지 5일, 오크 12일, 타락천사 30일),
        // 그림·전투 표현을 고치려면 **지금** 와야 한다. 배속으로 기다리는 것은 관측이 아니라 대기다.
        //
        // 🔑 새 스폰 경로를 만들지 않는다 — **시계를 당기거나 후보를 좁힐 뿐**이고, 편성·등급·
        // 특성·진입점·타깃 롤은 전부 정규 통로(BuyWave → Spawn)가 그대로 정한다. 병렬 경로를
        // 파면 테스트로 본 것이 실전과 다른 물건이 되고, 그때부터 관측은 거짓말이 된다.

        /// <summary>다음 습격을 지금 부른다 — 예고 중이면 즉시 발동, 예고 전이면 다음 틱에
        /// 예고가 서고 그 다음 틱(0.1초)에 발동한다. 해금·예산·완충이 전부 실전 그대로다.
        /// 발동 후 `_lastStrikeDay`는 정규 경로가 현재 시각으로 갱신하므로 주기는 안 망가진다.</summary>
        public void DebugStrikeNow() => _lastStrikeDay = _gameTime - (WavePeriod + _delayDays);

        /// <summary>이 종족만으로 한 무리를 즉시 출몰시킨다 — **해금일과 주기를 무시한다**
        /// (1일차에 타락천사를 보기 위한 문). 마릿수·등급은 정규 구매기가 지금 압력으로 정하고,
        /// 예산이 0이면 최소 한 마리는 나온다 (빈 소환은 도구로서 쓸모가 없다).</summary>
        public ThreatSO DebugSummonRace(ThreatSO race)
        {
            if (race == null) return null;
            int budget = Mathf.Max(1, CurrentGlobalPressure(_peakPopulation()));
            List<WaveEntry> wave = BuyWave(new[] { race }, budget, _strikeOrdinal + 1);
            if (wave.Count == 0) wave.Add(new WaveEntry(race, 0, 1));
            Spawn(race, wave);
            return race;
        }

        /// <summary>배포된 종족 목록 (디버그 순환 소환용) — 순서는 에셋 배열 그대로.
        /// 입력 쪽이 이름으로 고르지 않게 하는 창구다 (ADR-M24-4: 종족 차이는 데이터다).</summary>
        public IReadOnlyList<ThreatSO> Races => _threats;
#endif

        // ── 출몰·타격 (개체는 표현+이동, 판정은 여기 — 명세 M10-C ⚠️③) ─────────

        /// <param name="so">주력 종족 — 타깃 종류·진입점·공성 판정이 이 하나를 따른다
        /// (한 웨이브는 한 곳을 노린다. 다지점 진입은 W7 몫).</param>
        /// <param name="wave">편성 — (종족, 등급, 마릿수) 칸들. 이 목록이 곧 태어날 개체다.</param>
        /// <summary>이 개체가 **주민을 치는가** (순수 — 게이트 `M26B_T8b`).
        ///
        /// 🔴 `ADR-T2-6` 개정의 자리다. 명세는 *"상주 = 퇴장 없는 배회"* 라 했지만 그것만으로는
        /// 성립하지 않는다: 밭형은 제자리에 도착해 배회하되 **주민을 안 치고**(들판엔 밭이 없어
        /// 아무 일도 안 일어난다), 주민형은 주민을 치되 **마을까지 걸어간다**(상주가 아니게 된다).
        /// 그래서 상주는 **밭형으로 태어나 주민을 치는** 제3의 조합이다 — 축이 둘로 갈린다.
        /// (그래도 새 러너·새 상태는 0이다. 갈린 것은 판정 한 줄이다.)
        /// </summary>
        public static bool StrikesVillagers(bool targetsVillagers, bool isResident)
            => targetsVillagers || isResident;

        // ── 들판 상주 = 예약제 (M26-2차 W5R — 2026-08-11 사용자 Play 관측으로 개정) ──────
        //
        // 🔴 W5(즉시 배치)가 Play 첫 판에 세 결함을 드러냈다: ①Day 0부터 "습격 중" 배너
        //    ②F 명령이 **미발견** 고블린을 정확히 찾아감("주민이 맵을 밝힌다" 위반) ③개체가
        //    판 시작부터 전부 살아 있음. 사용자 설계 = **좌표만 예약하고, 주민이 다가오면 부화**.
        // 🔑 덤: 활성 개체 수가 맵 면적이 아니라 **주민 활동 면적**에 비례하게 된다 —
        //    맵 대형화(트리거 B)에서 예약은 좌표 몇 개일 뿐이다.
        //
        // 수명 주기: 무장 --주민 접근(부화 반경)--> 부화 --전멸--> 쿨다운 --경과--> 무장
        //                                          └--주민 이탈(회수 반경) + 무손실--> 무장(즉시)
        // 🔴 부화 < 회수 반경 (히스테리시스) — 같으면 경계를 걷는 주민 앞에서 깜빡인다.
        // 🔴 다친 무리는 회수하지 않는다 — "때리고 물러났다 오면 체력이 찬다" 방지.
        // 🔴 쿨다운은 재무장 = 전리품(MeatDrop)의 재생 주기다. 1회 부화면 고기 공급원이 마른다
        //    (사용자 판단). ⚠️ 값 3일은 제안치 — **시간 밸런스 세션**(별도 명세)에서 29개
        //    일 단위 필드와 함께 재조정한다 (1년 = 12일인 지금 눈금에서 3일 = 분기다).

        private enum ResState { Armed, Hatched, Cooldown }

        /// <summary>상주 무리 예약 1건. **세이브 대상** (ADR-M0-10 선언): State·CooldownUntil이
        /// 없으면 로드 직후 전멸했던 무리가 즉시 부화해 쿨다운이 무력화된다. 구현은 세이브 축에서.</summary>
        private sealed class ResidentReservation
        {
            public ThreatSO Race;
            public Vector2Int Anchor;
            public int Size;          // 시드 롤로 고정 — 재무장해도 같은 무리 (지도를 외운다)
            public uint Roll;         // 산개 자리 롤 (부화마다 같은 배치 = 결정적)
            public int GroupKey;      // 음수 고유 — _strikeOrdinal(양수)·_groupSpawned와 절대 안 겹친다
            public ResState State;
            public float CooldownUntil;
            public int HatchedCount;  // 부화 시점 마릿수 — 전멸/무손실 판정의 분모
            public bool Recalling;    // 회수 중 — NotifyDespawn이 쿨다운으로 오해하지 않게
            public readonly List<ThreatAgent> Live = new List<ThreatAgent>(6);
        }

        private readonly List<ResidentReservation> _residentReservations = new List<ResidentReservation>(8);
        private int _residentHatchRadius;    // 주민 시야 × 배수 — 조립 시 확정
        private int _residentRecallRadius;
        private float _residentCooldownDays;

        /// <summary>
        /// 들판 상주 예약 (M26-2차 W5R) — **판 시작 1회, 개체는 만들지 않는다.**
        ///
        /// 🔴 압력(`ADR-M24-1`)을 건드리지 않는다: `_strikeOrdinal`·`_encounters`·`_groupSpawned`
        ///    어느 것도 쓰지 않는다. 🔑 자리·크기는 **시드 롤**이다 (`ADR-M10R-2` — 같은 판이면
        ///    같은 자리·같은 무리). 앵커가 고정이라 "저 골짜기엔 고블린이 산다"를 배울 수 있다.
        /// </summary>
        /// <param name="hatchRadius">부화 반경 (주민 시야 × 배수 — 주민은 아직 적을 못 봤지만
        /// 적은 주민이 온 것을 안다).</param>
        /// <param name="recallRadius">회수 반경 — **부화보다 커야 한다** (히스테리시스).</param>
        public int ReserveResidents(IReadOnlyList<ThreatSO> races, uint runSeed,
                                    System.Func<int, int, TerrainTypeSO> terrainAt, int safeRadius,
                                    int hatchRadius, int recallRadius, float cooldownDays)
        {
            if (races == null || _isWalkable == null) return 0;
            _residentHatchRadius = Mathf.Max(1, hatchRadius);
            _residentRecallRadius = Mathf.Max(_residentHatchRadius + 1, recallRadius);
            _residentCooldownDays = Mathf.Max(0f, cooldownDays);

            MapBounds.Get(out int minX, out int maxX, out int minY, out int maxY);
            long tiles = (long)(maxX - minX + 1) * (maxY - minY + 1);

            foreach (ThreatSO so in races)
            {
                if (so == null || !so.IsResident || so.ResidentBandsPer10kTiles <= 0f) continue;

                int bands = BandCount(so.ResidentBandsPer10kTiles, tiles);
                var anchors = new System.Text.StringBuilder();

                for (int b = 0; b < bands; b++)
                {
                    uint roll = StableHash.Fnv1a($"{so.name}_{b}", "residentband") ^ runSeed;
                    if (!TryPickHabitatTile(so, roll, terrainAt, safeRadius,
                                            minX, maxX, minY, maxY, out Vector2Int anchor))
                    {
                        Debug.LogWarning($"[Threat] 상주 예약 실패 — {so.DisplayName} {b + 1}/{bands} " +
                                         "(서식지 조건을 만족하는 칸을 못 찾았다)");
                        continue;
                    }

                    int lo = Mathf.Max(1, Mathf.Min(so.ResidentBandMin, so.ResidentBandMax));
                    int hi = Mathf.Max(lo, so.ResidentBandMax);
                    _residentReservations.Add(new ResidentReservation
                    {
                        Race = so,
                        Anchor = anchor,
                        Size = lo + (int)(StableHash.Fnv1a($"{so.name}_{b}", "bandsize") % (uint)(hi - lo + 1)),
                        Roll = roll,
                        GroupKey = -(_residentReservations.Count + 1),
                        State = ResState.Armed,
                    });
                    anchors.Append($" ({anchor.x},{anchor.y})");
                }
                // 무리별 앵커 로그 (W5R ⑤) — "한 곳에 다 모였다" 관측을 다음 판에 확정하는 근거.
                if (anchors.Length > 0)
                    Debug.Log($"[Threat] 들판 상주 예약 — {so.DisplayName} {bands}무리, 앵커:{anchors} " +
                              $"(부화 {_residentHatchRadius}칸 · 회수 {_residentRecallRadius}칸 · " +
                              $"재무장 {_residentCooldownDays:0.##}일)");
            }
            return _residentReservations.Count;
        }

        /// <summary>예약 틱 (Tick 초입) — 부화·회수·재무장. 예약 ~수 개 × 주민 ~수십이라 싸다.</summary>
        private void TickResidentReservations()
        {
            for (int i = 0; i < _residentReservations.Count; i++)
            {
                ResidentReservation r = _residentReservations[i];
                switch (r.State)
                {
                    case ResState.Armed:
                        if (AnyVillagerWithin(r.Anchor, _residentHatchRadius)) HatchBand(r);
                        break;

                    case ResState.Hatched:
                        // 회수 = 전원 생존 + 전원 무손상 + 주민이 회수 반경 밖.
                        // 🔴 손상·손실이 있으면 남는다 — 물러났다 와도 체력이 차 있으면 안 된다.
                        if (r.Live.Count == r.HatchedCount && AllUnharmed(r)
                            && !AnyVillagerWithin(r.Anchor, _residentRecallRadius))
                            RecallBand(r);
                        break;

                    case ResState.Cooldown:
                        if (_gameTime >= r.CooldownUntil)
                        {
                            r.State = ResState.Armed;
                            Debug.Log($"[Threat] 상주 재무장 — {r.Race.DisplayName} @ ({r.Anchor.x},{r.Anchor.y})");
                        }
                        break;
                }
            }
        }

        private bool AnyVillagerWithin(Vector2Int anchor, int radius)
        {
            if (_agents == null) return false;
            foreach (VillagerAgent a in _agents)
            {
                if (a == null || a.State == AgentState.Dead) continue;   // 시신은 부화를 부르지 않는다
                if (Mathf.Abs(a.TileX - anchor.x) + Mathf.Abs(a.TileY - anchor.y) <= radius) return true;
            }
            return false;
        }

        private static bool AllUnharmed(ResidentReservation r)
        {
            foreach (ThreatAgent t in r.Live)
                if (t == null || t.Hp < t.So.MaxHp * t.GradeMult) return false;
            return true;
        }

        /// <summary>부화 — 예약이 개체가 된다. 산개 롤이 예약에 고정돼 있어 매번 같은 배치다.</summary>
        private void HatchBand(ResidentReservation r)
        {
            ThreatSO so = r.Race;
            int spawned = 0;
            for (int m = 0; m < r.Size; m++)
            {
                Vector2Int at = r.Anchor;
                if (m > 0)
                {
                    uint mr = r.Roll + (uint)(m * 40503);
                    int span = so.ResidentBandRadius * 2 + 1;
                    at = new Vector2Int(r.Anchor.x + (int)(mr % (uint)span) - so.ResidentBandRadius,
                                        r.Anchor.y + (int)((mr / (uint)span) % (uint)span) - so.ResidentBandRadius);
                    if (!_isWalkable(at.x, at.y)) continue;   // 못 서는 칸 — 그 마리만 거른다
                }
                ThreatAgent agent = CreateAgent($"Resident_{so.name}_{at.x}_{at.y}", so, at, at,
                                                null, false, r.GroupKey, 1f, resident: true);
                r.Live.Add(agent);
                spawned++;
            }
            r.HatchedCount = spawned;
            r.State = ResState.Hatched;
            Debug.Log($"[Threat] 들판 조우 — {so.DisplayName} {spawned}마리 @ ({r.Anchor.x},{r.Anchor.y}) " +
                      "(주민 접근으로 부화)");
        }

        /// <summary>회수 — 주민이 물러났고 아무도 안 다쳤다. 조용히 걷어 즉시 재무장한다.</summary>
        private void RecallBand(ResidentReservation r)
        {
            r.Recalling = true;
            for (int i = r.Live.Count - 1; i >= 0; i--) r.Live[i]?.DespawnNow();
            r.Recalling = false;
            r.Live.Clear();
            r.HatchedCount = 0;
            r.State = ResState.Armed;
            Debug.Log($"[Threat] 상주 회수 — {r.Race.DisplayName} @ ({r.Anchor.x},{r.Anchor.y}) " +
                      "(주민이 물러나 다시 잠복)");
        }

        /// <summary>상주 소멸 처리 (`NotifyDespawn` 전용) — 전멸이면 쿨다운을 건다.</summary>
        private void OnResidentDespawn(ThreatAgent agent)
        {
            foreach (ResidentReservation r in _residentReservations)
            {
                if (r.GroupKey != agent.GroupKey) continue;
                r.Live.Remove(agent);
                if (r.Live.Count == 0)
                {
                    _groupAttackers.Remove(r.GroupKey);   // 교전 명단 정리 (키가 고정이라 다음 부화에 새로 쌓인다)
                    if (!r.Recalling && r.State == ResState.Hatched)
                    {
                        r.State = ResState.Cooldown;
                        r.CooldownUntil = _gameTime + _residentCooldownDays;
                        r.HatchedCount = 0;
                        Debug.Log($"[Threat] 상주 무리 소진 — {r.Race.DisplayName} @ ({r.Anchor.x},{r.Anchor.y}) " +
                                  $"· 재무장까지 {_residentCooldownDays:0.##}일");
                    }
                }
                return;
            }
        }

        /// <summary>맵 넓이에서 무리 수 (순수 — 게이트 `M26B_T8c`).
        /// 🔑 **밀도로 적는 이유**: 절대 수로 박으면 맵이 9배가 되어도 무리는 그대로라
        /// **넓어진 맵이 더 안전해진다.** 밀도면 맵을 넓혀도 코드 0줄이다 (2026-08-11 사용자 판단).
        /// 밀도가 0보다 크면 최소 1무리는 선다 — 작은 맵에서 조용히 0이 되지 않게.</summary>
        public static int BandCount(float bandsPer10kTiles, long mapTiles)
        {
            if (bandsPer10kTiles <= 0f || mapTiles <= 0) return 0;
            return Mathf.Max(1, Mathf.RoundToInt(bandsPer10kTiles * (mapTiles / 10000f)));
        }

        /// <summary>서식지 조건·안전반경·통행을 만족하는 칸 찾기 (시드 롤, 결정적).</summary>
        private bool TryPickHabitatTile(ThreatSO so, uint roll,
                                        System.Func<int, int, TerrainTypeSO> terrainAt, int safeRadius,
                                        int minX, int maxX, int minY, int maxY, out Vector2Int spot)
        {
            spot = default;
            for (int attempt = 0; attempt < 400; attempt++)
            {
                uint r = roll + (uint)attempt * 2654435761u;
                int x = minX + (int)(r % (uint)(maxX - minX + 1));
                int y = minY + (int)((r / 7919u) % (uint)(maxY - minY + 1));

                if (Mathf.Max(Mathf.Abs(x - VillageCenter.x),
                              Mathf.Abs(y - VillageCenter.y)) <= safeRadius) continue;
                if (!_isWalkable(x, y)) continue;
                if (terrainAt != null && so.HabitatTerrain != null && so.HabitatTerrain.Length > 0
                    && System.Array.IndexOf(so.HabitatTerrain, terrainAt(x, y)) < 0) continue;

                spot = new Vector2Int(x, y);
                return true;
            }
            return false;
        }


        /// <summary>
        /// 위협 개체가 태어나는 **유일한 문** (게이트 `M24_T7`이 전수로 감시한다).
        ///
        /// 🔴 문이 둘이면 테스트에서 본 적과 실전에서 오는 적이 **다른 물건**이 되고, 그 순간
        /// 관측이 거짓말을 시작한다. 특성 부여·무리 키·경로탐색 주입이 여기 한 곳에 모여 있어야
        /// 어느 경로로 태어나든 같은 계약을 진다 (M26-2차 W5에서 상주 통로가 생기며 이 문으로 합쳤다).
        /// </summary>
        private ThreatAgent CreateAgent(string name, ThreatSO so, Vector2Int at, Vector2Int target,
                                        List<Vector2Int> waypoints, bool targetsVillagers,
                                        int groupKey, float gradeMult, bool resident = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_parent, worldPositionStays: false);
            go.transform.position = new Vector3(at.x, at.y, 0f); // ADR-M0-9 — X-Y 평면
            ThreatAgent agent = go.AddComponent<ThreatAgent>();
            agent.Init(so, this, at, target, waypoints, _pathfinder(), targetsVillagers,
                       groupKey, gradeMult,
                       ActiveTraits(so, PressureOf(so))); // 쓰는 수 (M24-1차 W5, 스폰 시 고정)
            // 🔴 상주는 **개체 속성**이다 — 같은 종족이 들판에도 살고 웨이브로도 오므로
            //    SO 에서 읽으면 쳐들어온 개체까지 영영 안 나간다 (M26-2차 W5).
            if (resident) agent.MarkResident();
            _active.Add(agent);
            return agent;
        }

        private void Spawn(ThreatSO so, List<WaveEntry> wave)
        {
            _strikeOrdinal++;
            // 조우 누적 (M24-1차 W3) — 편성에 든 **종족마다 1회씩**. 이겨도 져도 배운다.
            // ⚠️ 한 종족이 지휘관·졸개 두 칸으로 나뉘어 있으므로 칸이 아니라 **종족** 단위로 센다.
            _seenBuf.Clear();
            foreach (WaveEntry e in wave)
                if (e.Race != null && _seenBuf.Add(e.Race.name))
                {
                    _encounters.TryGetValue(e.Race.name, out int prev);
                    _encounters[e.Race.name] = prev + 1;
                }
            // 타깃 종류는 출몰 시 1회 확정 (ADR-M10R-4). 확률만 계절 보정을 받는다 (M21-W2R) —
            // 겨울엔 위협이 굶어 주민 쪽으로 기운다. 시드는 그대로라 결정성은 유지된다.
            bool hungry = PredatorHungryNow;
            float chance = EffectiveVillagerChance(so.VillagerTargetChance, hungry, so.HungrySeasonChanceMult);
            bool targetsVillagers = RollTargetsVillagers(so, _strikeOrdinal, chance);
            MapBounds.Get(out int minX, out int maxX, out int minY, out int maxY);
            uint seed = StableHash.Fnv1a(_strikeOrdinal.ToString(), so.DisplayName);
            InvaderTraitId[] primaryTraits = ActiveTraits(so, PressureOf(so));

            // 진입점 (M24-1차 W7) — 우회·다지점. 특성이 없으면 후보 1개이고 그 칸은 舊 EntryPoint
            // 와 같은 좌표라 아래 전부가 배선 전과 동일하게 돈다 (중립 불변식, DoD ①).
            // 🔑 타깃·공성 판정은 **주 진입점(entries[0]) 하나**를 따른다 — 한 웨이브는 한 곳을
            //    노리고, 다지점이 바꾸는 것은 "어디로 들어오나"뿐이다 (목표를 쪼개면 무리가 아니다).
            _towerBuf.Clear();
            _defense?.CollectBuiltTiles(SlotId.WatchtowerCount, _towerBuf);
            int waveCount = 0;
            foreach (WaveEntry e in wave) waveCount += e.Count;
            Vector2Int[] entries = EntryPoints(seed, minX, maxX, minY, maxY,
                                               primaryTraits, _towerBuf, VillageCenter, waveCount);
            Vector2Int entry = entries[0];
            // 관측 로그 — 이 두 수는 화면에서 "왜 저기로 들어왔지"로만 보이므로 근거를 남긴다.
            if (entry != EntryPoint(seed, minX, maxX, minY, maxY))
                Debug.Log($"[Threat] 우회 진입 — {so.DisplayName}이(가) 망루가 선 쪽을 돌아 ({entry.x},{entry.y})로 들어온다");
            if (entries.Length > 1)
                Debug.Log($"[Threat] 다지점 진입 — {so.DisplayName} {waveCount}마리가 {entries.Length}곳에서 들어온다");
            LogVerdictLine("발동", so, wave, entries.Length,
                           targetsVillagers ? "주민" : "밭");
            Vector2Int target = PickTargetTile(so, targetsVillagers, entry);

            PathResult path = _pathfinder().FindPath(entry.x, entry.y, target.x, target.y);
            bool siege = false;

            // 시설 우선 (M24-1차 W5, TargetDefense) — 길이 막히지 않아도 **먼저** 방어 시설을 노린다.
            // 舊 공성(M22-W5)은 "막혔을 때의 대안"이었는데, 이 수를 쓰는 종족에게는 그게 목적이다.
            // 🔑 그래서 답이 갈린다: 늑대 시절 답은 "울타리를 두껍게"였지만, 여기 답은 **분산·이중화**다
            //    (한 곳에 몰아 지으면 그 한 곳이 통째로 목표가 된다).
            if (Has(primaryTraits, InvaderTraitId.TargetDefense)
                && _defense != null && _defense.HasStructures && so.StructureDamage > 0f
                && _defense.TryGetNearestStructure(entry, out SlotId dslot, out Vector2Int dtile))
            {
                Vector2Int approach = ApproachTileOf(dtile, entry);
                PathResult dpath = _pathfinder().FindPath(entry.x, entry.y, approach.x, approach.y);
                if (dpath.Kind != PathResultKind.Unreachable)
                {
                    _groupSiege[_strikeOrdinal] = new SiegeInfo
                    {
                        Slot = dslot, Tile = dtile,
                        OrigTargetsVillagers = targetsVillagers, OrigTarget = target,
                    };
                    target = approach;
                    path = dpath;
                    siege = true;
                    Debug.Log($"[Threat] 시설 우선 — {so.DisplayName}이(가) ({dtile.x},{dtile.y})을(를) 먼저 노린다");
                }
            }
            if (path.Kind == PathResultKind.Unreachable)
            {
                // 공성 전환 (M22-W5, ADR-M22-2) — 방어 시설이 길을 막았으면 출몰을 접지 않고
                // 최근접 시설을 두드린다. 시설이 파는 것은 시간뿐이다 ("완성되면 안전" 금지).
                // ApproachTileOf 폴백이 시설 타일 자체를 돌려줘도 안전하다: 시설은 통행 불가라
                // 그 경로도 Unreachable → 아래 생략으로 떨어진다 (전제 확인 ⑦의 해소 지점).
                if (_defense != null && _defense.HasStructures && so.StructureDamage > 0f
                    && _defense.TryGetNearestStructure(entry, out SlotId sslot, out Vector2Int stile))
                {
                    Vector2Int approach = ApproachTileOf(stile, entry);
                    PathResult siegePath = _pathfinder().FindPath(entry.x, entry.y, approach.x, approach.y);
                    if (siegePath.Kind != PathResultKind.Unreachable)
                    {
                        _groupSiege[_strikeOrdinal] = new SiegeInfo
                        {
                            Slot = sslot, Tile = stile,
                            OrigTargetsVillagers = targetsVillagers, OrigTarget = target,
                        };
                        target = approach;
                        path = siegePath;
                        siege = true;
                        Debug.Log($"[Threat] 공성 전환 — {so.DisplayName}: 길이 막혀 " +
                                  $"({stile.x},{stile.y}) 시설을 노린다 (ADR-M22-2)");
                    }
                }
                if (!siege)
                {
                    // 이번 발동은 건너뛴다 (무한 재시도 금지 — 명세 ⚠️④). 다음 주기에 재출몰.
                    // 방어 시설 0개(자연 지형 막힘)의 기존 동작 — 회귀 아님 (ADR-M22-2 단서).
                    Debug.LogWarning($"[Threat] {so.DisplayName}: 진입 경로 없음 ({entry.x},{entry.y})→({target.x},{target.y}) — 이번 출몰 생략");
                    return;
                }
            }

            // 완충(M21-W7)은 이제 **예고 시점에 예산에서** 깎였다 (ADR-M24-3 — 편성이 거기서 굳는다).
            // 남은 것은 지연 소모뿐. 마릿수는 편성이 이미 정했다 (ADR-M24-2 — SpawnCount 미사용).
            _delayDays = 0f;
            int spawned = 0;
            int i = -1;
            foreach (WaveEntry entryDef in wave)
            for (int n = 0; n < entryDef.Count; n++)
            {
                i++;
                ThreatSO race = entryDef.Race;
                float mult = GradeMult(race, entryDef.Grade);
                // 진입점 배분 (M24-1차 W7) — 마릿수를 진입점들에 돌아가며 나눈다. 후보가 1개면
                // 언제나 entry 라 아래 두 줄이 舊 코드와 완전히 같아진다 (중립 불변식, DoD ①·③).
                Vector2Int myEntry = entries[i % entries.Length];
                // 갈래마다 첫 마리 = 그 진입점 그대로. 나머지 = 곁 산개 (겹침 방지 오프셋 —
                // 표현이므로 난수 허용, ADR-M21-10).
                Vector2Int at = i < entries.Length || _isWalkable == null
                    ? myEntry : MapBounds.PickWalkableNear(_isWalkable, myEntry.x, myEntry.y, 2);
                PathResult p = at == entry
                    ? path : _pathfinder().FindPath(at.x, at.y, target.x, target.y);
                if (p.Kind == PathResultKind.Unreachable && myEntry != entry)
                {
                    // 갈래 진입점이 막혔다 — 주 진입점으로 되돌린다(생략하지 않는다). 마릿수는
                    // 예산이 정한 값이고(ADR-M24-2) 다지점은 "어디로"의 문제지 "몇이"의 문제가 아니다.
                    at = entry;
                    p = path;
                }
                if (p.Kind == PathResultKind.Unreachable)
                {
                    // 이 마리만 생략 (⚠️③ — Unreachable 생략 규약을 마리 단위로). 분모에도 안 센다.
                    Debug.LogWarning($"[Threat] {race.DisplayName}: 산개 지점 ({at.x},{at.y}) 경로 없음 — 개체 1 생략");
                    continue;
                }

                CreateAgent($"Threat_{race.name}_{_strikeOrdinal}_{i}", race, at, target,
                            p.Kind == PathResultKind.PathFound ? p.Waypoints : null,
                            targetsVillagers, // 추격 재경로용 + 이번 출몰 타깃 종류 (M10R)
                            _strikeOrdinal,   // 무리 키 (M21-W6)
                            mult);            // 등급 배율 (M24-1차 W4)
                spawned++;
            }
            if (spawned == 0) { _groupSiege.Remove(_strikeOrdinal); return; } // 전 개체 생략 — 공성 등록도 회수
            _groupSpawned[_strikeOrdinal] = spawned; // 무리 도주선의 분모 = 실제 태어난 수
            Debug.Log($"[Threat] 출몰 — {Describe(wave)} (총 {spawned}) " +
                      $"@ ({entry.x},{entry.y}) → ({target.x},{target.y}) " +
                      $"[{(targetsVillagers ? "주민" : "밭")} 타깃]{(hungry ? " · 배고픈 계절" : "")}");
        }

        /// <summary>목표 타일: 주민 타격 = 진입점 최근접 생존 주민, 밭 타격 = 진입점 최근접 **실제 밭 타일**.
        /// 폴백(주민 0·밭 0)은 기지 — 위협은 항상 마을 심장부로 향한다.
        ///
        /// 밭 분기 개정 (2026-08-06 Play "진입 경로 없음" 반복 관측): 舊 분기는 밭 구역 앵커
        /// (TryGetZone)였는데, M11-E(c1a097c)가 FarmPlot.ZoneRadius를 0으로 내린 뒤로 밭 구역은
        /// 영영 등록되지 않아 **죽은 분기**였다 — 항상 기지 (0,0) 폴백으로 떨어졌고, 공용 집이
        /// 기지 앵커(BuildRunner)에 서서 그 타일을 막으면(BlocksMovement) JPS가 목표 unwalkable
        /// 즉시 Unreachable → 밭 타깃 출몰 전부 영구 생략. 실제 밭 타일은 통행 차단이 아니므로
        /// 항상 도달 가능하고, 파괴 판정(ExecuteStrike)과 같은 원천(BuiltTilesOf)이라 어긋나지 않는다.</summary>
        private Vector2Int PickTargetTile(ThreatSO so, bool targetsVillagers, Vector2Int entry)
        {
            if (targetsVillagers)
            {
                CollectVictimCandidates(entry, int.MaxValue, excludeInjured: false);
                if (_victimKeyBuf.Count > 0)
                {
                    int idx = M0SimulationLoop.PickNearestIndex(entry.x, entry.y, _victimKeyBuf);
                    if (idx >= 0) return new Vector2Int(_victimKeyBuf[idx].x, _victimKeyBuf[idx].y);
                }
            }
            else
            {
                _plotBuf.Clear();
                _plotBuf.AddRange(_construction.BuiltTilesOf(SlotId.FarmPlotCount));
                int idx = PickNearestTileIndex(entry.x, entry.y, _plotBuf);
                if (idx >= 0) return ApproachTileOf(_plotBuf[idx], entry);
            }
            return new Vector2Int(_config.BaseTileX, _config.BaseTileY);
        }

        /// <summary>밭 곁 접근 타일 — 사방이 다 막혔으면 밭 자체 (밭은 통행 가능하므로 출몰이
        /// 생략되는 일은 없다. 2026-08-06 b51c631이 고친 실패 모드를 다시 열지 않기 위한 보루).</summary>
        private Vector2Int ApproachTileOf(Vector2Int plot, Vector2Int entry)
        {
            MapBounds.Get(out int minX, out int maxX, out int minY, out int maxY);
            ApproachCandidates(plot, entry, _approachBuf);
            foreach (Vector2Int c in _approachBuf)
            {
                if (c.x < minX || c.x > maxX || c.y < minY || c.y > maxY) continue; // 맵 밖은 조회 안 함(경고 소음 방지)
                if (_pathfinder().FindPath(entry.x, entry.y, c.x, c.y).Kind != PathResultKind.Unreachable)
                    return c;
            }
            return plot;
        }

        /// <summary>
        /// 접근 타일 후보 (순수·결정적 — 게이트 M10-T6): 밭의 4방 이웃을 진입점에 가까운 순으로.
        /// 동률은 좌표순 — 같은 판이면 같은 자리에 선다 (ADR-M10-1).
        ///
        /// 왜 밭 위가 아니라 곁인가 (2026-08-06 Play): 밭 타일을 목적지로 삼으니 개체 마커가
        /// 밭 스프라이트를 덮어(sortingOrder 11) **밭이 안 보였다**. 타격 반경이 3~4타일이라
        /// 밭 위에 설 이유도 없다. 진입점 쪽 이웃부터 보는 것은 돌아 들어가지 않게 하려는 것.
        /// </summary>
        public static void ApproachCandidates(Vector2Int plot, Vector2Int entry, List<Vector2Int> result)
        {
            result.Clear();
            result.Add(new Vector2Int(plot.x - 1, plot.y));
            result.Add(new Vector2Int(plot.x + 1, plot.y));
            result.Add(new Vector2Int(plot.x, plot.y - 1));
            result.Add(new Vector2Int(plot.x, plot.y + 1));
            result.Sort((a, b) =>
            {
                long da = Dist2Tile(a, entry), db = Dist2Tile(b, entry);
                if (da != db) return da.CompareTo(db);
                if (a.x != b.x) return a.x.CompareTo(b.x);
                return a.y.CompareTo(b.y);
            });
        }

        private static long Dist2Tile(Vector2Int t, Vector2Int from)
        {
            long dx = t.x - from.x, dy = t.y - from.y;
            return dx * dx + dy * dy;
        }

        /// <summary>최근접 타일 선정 (순수·결정적 — 게이트 M10-T5). 동률은 목록 앞(= 완공 등록 순)이
        /// 이긴다: 같은 판이면 같은 밭이 찍혀야 출몰이 확률이 아니다 (ADR-M10-1).
        /// 목록이 비면 -1 — 호출처가 기지 폴백으로 넘어간다.</summary>
        public static int PickNearestTileIndex(int fromX, int fromY, IReadOnlyList<Vector2Int> tiles)
        {
            int best = -1;
            long bestD = long.MaxValue;
            for (int i = 0; i < (tiles?.Count ?? 0); i++)
            {
                long dx = tiles[i].x - fromX, dy = tiles[i].y - fromY;
                long d = dx * dx + dy * dy;
                if (d >= bestD) continue; // >= 이므로 동률은 먼저 온 것이 남는다
                bestD = d;
                best = i;
            }
            return best;
        }

        /// <summary>
        /// 추격 대상 (M10-C 개정 2026-07-22: 고정 타깃 → 추격 — 고정 타깃은 도망이 구조적으로
        /// 100% 이겨 부상이 착탄 불가, Day40+ 관측) — 최근접 생존 주민. 없으면 false (퇴장).
        /// **부상자 제외 폐지 (M21-W2 재검사 A1)**: 舊 규칙은 "중복 부상은 무시되니 쫓아봐야
        /// 빈 타격"이라는 1회 타격 시대의 판단이었다. 이제 타격은 체력을 깎으므로 부상자야말로
        /// 죽는다 — 제외하면 위협이 누구도 직접 죽이지 못한다. 절뚝이는 사람이 먼저 잡히는 것이
        /// 치사율의 실체이고, 플레이어의 개입 창이 열리는 자리다.
        /// </summary>
        public bool TryPickChaseTile(int fromX, int fromY, out Vector2Int tile)
        {
            CollectVictimCandidates(new Vector2Int(fromX, fromY), int.MaxValue, excludeInjured: false);
            int idx = M0SimulationLoop.PickNearestIndex(fromX, fromY, _victimKeyBuf);
            if (idx >= 0)
            {
                tile = new Vector2Int(_victimKeyBuf[idx].x, _victimKeyBuf[idx].y);
                return true;
            }
            tile = default;
            return false;
        }

        /// <summary>타격 사거리 판정 (추격형 전용) — 개체 현 위치 기준 StrikeRadius 내 생존 주민
        /// 존재 여부. 판정은 서비스, 개체는 묻기만 한다 (명세 ⚠️③ 유지).
        /// 부상자도 후보다 (M21-W2 A1 — TryPickChaseTile 주석 참조).</summary>
        public bool IsInStrikeRange(ThreatAgent agent)
        {
            CollectVictimCandidates(new Vector2Int(agent.TileX, agent.TileY),
                                    agent.So.StrikeRadiusTiles, excludeInjured: false);
            return _victimKeyBuf.Count > 0;
        }

        // ── 체류 (M21-W2 — 舊 "도착=타격=퇴장"의 개정) ────────────────────────

        /// <summary>재타격 시점 판정 (순수 — 게이트 M21-T4): 마지막 타격에서 주기가 지났는가.
        /// 주기가 0 이하면 매 프레임 타격이 되므로 아예 치지 않는다 (에셋 사고 방어).</summary>
        public static bool ShouldRepeatStrike(float lastStrikeDay, float now, float periodDays)
            => periodDays > 0f && now - lastStrikeDay >= periodDays;

        /// <summary>체류 상한 판정 (순수 — 게이트 M21-T5): 자비가 아니라 지형 보루다.
        /// 닿을 수 없는 자리에 눌러앉은 개체가 영영 남으면 다음 출몰 스케줄이 그 위에 쌓인다.</summary>
        public static bool ShouldGiveUpStay(float arrivedDay, float now, float maxStayDays)
            => maxStayDays > 0f && now - arrivedDay >= maxStayDays;

        /// <summary>이번 개체의 실효 체류 상한 — 배고픈 계절이면 배율이 걸린다 (M21-W2R).
        /// 로그·판정이 같은 값을 읽도록 창구를 하나로 둔다 (겨울에 "상한 0.8일"이라 찍고
        /// 실제로는 1.5일 머무르면 관측이 거짓말이 된다).</summary>
        private float StayLimitOf(ThreatAgent agent)
            => EffectiveStayDays(agent.So.MaxStayDays, PredatorHungryNow, agent.So.HungrySeasonStayMult);

        /// <summary>체류 시작 통지 (ThreatAgent 전용) — 도착 지점을 배회 앵커로 삼고 첫 타격을 실행한다.
        /// M21-W2R: 더는 "눌러앉지" 않는다 — 여기서 시작되는 것은 정지가 아니라 배회다 (퇴장은 BeginExit).
        /// 밭 타깃인데 사거리 안에 밭이 하나도 없으면 치지 않고 곧바로 물러난다 (W2 결함 Y).</summary>
        public void NotifyArrived(ThreatAgent agent)
        {
            agent.MarkArrived(_gameTime);
            var here = new Vector2Int(agent.TileX, agent.TileY);

            // 공성이 먼저다 (M22-W5) — 밭형 "칠 대상이 없다" 판정에 떨어지면 시설 앞에서
            // 한 대도 못 치고 물러난다 (울타리 곁에 밭이 없는 것이 정상이므로).
            SiegeTick st = TickSiegeStrike(agent, here);
            if (st != SiegeTick.NotSiege)
            {
                if (st == SiegeTick.Struck) agent.MarkStruck(_gameTime);
                float slimit = StayLimitOf(agent);
                Debug.Log($"[Threat] 공성 체류 시작 — {agent.So.DisplayName} @ ({here.x},{here.y}) " +
                          $"[상한 {slimit:0.##}일 — 시설이 파는 것은 시간이다 (ADR-M22-2)]");
                return;
            }

            if (!agent.TargetsVillagers && !HasFarmTargetsNear(agent.So, here))
            {
                BeginExit(agent, "칠 대상이 없다");
                return;
            }
            ExecuteStrike(agent.So, agent.TargetsVillagers, here, out Vector2 focus);
            agent.PlayAttack(focus); // 공격 몸짓 (M22-3차 W5c — 표현 전용, 판정과 무관)
            agent.MarkStruck(_gameTime);
            float limit = StayLimitOf(agent);
            Debug.Log($"[Threat] 체류 시작 — {agent.So.DisplayName} @ ({here.x},{here.y}) " +
                      $"[재타격 {agent.So.RepeatStrikePeriodDays:0.##}일 · 상한 {limit:0.##}일 · " +
                      $"배회 반경 {agent.So.WanderRadiusTiles}]");
        }

        /// <summary>체류 틱 (ThreatAgent 전용) — 상한 도달이면 퇴장, 아니면 주기 충족 시 재타격.
        /// 순서가 중요하다: 상한을 먼저 봐야 마지막 프레임에 한 대 더 치고 나가지 않는다.
        ///
        /// 대상 소멸 판정 (M21-W2R — W2 결함 Y): **밭형에만** 건다. 밭이 다 사라져도 상한까지
        /// `밭 0/0 소실`을 주기마다 찍으며 남아 있던 것이 결함이었다 (시설은 도망가지 않으므로
        /// "사거리에 없다" = "없다"가 성립한다).
        ///
        /// 🔴 주민형에 같은 판정을 걸면 안 된다 (2026-08-07 Play에서 실측으로 잡혔다 — 걸었더니
        /// 상한 전에 전부 퇴장했다): 주민은 **도망친다**. 사거리 밖에 있는 것은 대상이 없다는 뜻이
        /// 아니라 아직 못 잡았다는 뜻이고, 그래서 추격이 있다. 주민형의 "칠 대상이 없다"는
        /// ThreatAgent.TickChase가 소유한다 — 조건은 사거리가 아니라 **생존 주민 0명**이다.
        /// 대신 사거리에 아무도 없는 순간의 타격은 그냥 거른다 (빈 타격 로그·알림 방지).
        /// 시각을 기록하지 않으므로 주민이 다시 들어오면 곧바로 문다.</summary>
        public void NotifyStrikeTick(ThreatAgent agent)
        {
            if (agent.IsExiting) return;
            // 상주형에는 체류 상한이 없다 (M26-2차 W5). 🔴 **여기서 걸러야 한다** — BeginExit만
            // 막으면 상한 판정이 매 틱 참이 되어 아래 `return`에 걸려 **영영 타격하지 않는다.**
            if (!agent.IsResident)
            {
                float limit = StayLimitOf(agent);
                if (ShouldGiveUpStay(agent.ArrivedDay, _gameTime, limit))
                {
                    BeginExit(agent, $"체류 상한 {limit:0.##}일");
                    return;
                }
            }
            if (!ShouldRepeatStrike(agent.LastStrikeDay, _gameTime, agent.So.RepeatStrikePeriodDays)) return;

            var here = new Vector2Int(agent.TileX, agent.TileY);

            // 공성이 먼저다 (M22-W5) — 무리가 공성 중이면 이번 타격은 시설 몫이고,
            // 사거리 밖(배회로 멀어짐)이면 이번 틱만 거른다 (밭형 퇴장 판정에 떨어지지 않게).
            SiegeTick st = TickSiegeStrike(agent, here);
            if (st == SiegeTick.Struck) { agent.MarkStruck(_gameTime); return; }
            if (st != SiegeTick.NotSiege) return;

            bool strikesVillagers = StrikesVillagers(agent.TargetsVillagers, agent.IsResident);

            if (!strikesVillagers)
            {
                if (!HasFarmTargetsNear(agent.So, here))
                {
                    BeginExit(agent, "칠 대상이 없다");
                    return;
                }
            }
            else if (!HasVillagerTargetsNear(agent.So, here)) return; // 사거리 밖 = 아직 못 잡았다 (추격이 잇는다)

            ExecuteStrike(agent.So, strikesVillagers, here, out Vector2 focus);
            agent.PlayAttack(focus); // 공격 몸짓 (M22-3차 W5c — 표현 전용, 판정과 무관)
            agent.MarkStruck(_gameTime);

            // 치고 빠지기 (M24-1차 W5, ExitOnGoal) — 한 대 치면 목적을 이룬 것이라 곧장 물러난다.
            // 🔑 이 수가 만드는 질문은 "어떻게 버티나"가 아니라 **"어떻게 잡나"**다: 체류하지
            // 않으므로 주민이 모여서 때릴 시간이 없고, 답은 추격이거나 길목의 망루다.
            if (agent.HasTrait(InvaderTraitId.ExitOnGoal)) BeginExit(agent, "목적을 이뤘다");
        }

        /// <summary>사거리 안에 밭이 있는가 (M21-W2R). 반경 규칙은 ExecuteStrike의 후보 수집과
        /// **같은 맨해튼 반경**이다: 여기서 "있다"고 했는데 저기서 0개를 고르면 로그가 서로를 부정한다.</summary>
        private bool HasFarmTargetsNear(ThreatSO so, Vector2Int tile)
        {
            foreach (Vector2Int t in _construction.BuiltTilesOf(SlotId.FarmPlotCount))
                if (Mathf.Abs(t.x - tile.x) + Mathf.Abs(t.y - tile.y) <= so.StrikeRadiusTiles)
                    return true;
            return false;
        }

        /// <summary>사거리 안에 생존 주민이 있는가 — 빈 타격 거르기 전용 (퇴장 판정 아님).</summary>
        private bool HasVillagerTargetsNear(ThreatSO so, Vector2Int tile)
        {
            CollectVictimCandidates(tile, so.StrikeRadiusTiles, excludeInjured: false);
            return _victimKeyBuf.Count > 0;
        }

        /// <summary>다음 배회 목적지 (ThreatAgent 전용, M21-W2R) — 앵커 반경 내 통행 가능 타일.
        /// 🔴 여기가 ADR-M21-10의 확률 쪽이다: **동선은 표현이라 난수를 허용한다**
        /// (같은 판에서 위협이 매번 같은 길로 걸으면 그게 더 이상하다). 타깃 종류·출몰 스케줄·
        /// 희생 선정은 여전히 시드 롤이다 — 그쪽에 난수를 넣으면 ADR-M10-1 위반이다.
        /// isWalkable 미배선이면 앵커 그대로 (배회 없음 = 舊 정지 동작, 중립 폴백).</summary>
        public Vector2Int PickWanderTile(ThreatAgent agent)
        {
            Vector2Int anchor = agent.WanderAnchor;
            if (_isWalkable == null || agent.So.WanderRadiusTiles <= 0) return anchor;
            return MapBounds.PickWalkableNear(_isWalkable, anchor.x, anchor.y, agent.So.WanderRadiusTiles);
        }

        /// <summary>퇴장 개시 (ThreatAgent·CombatService 공용) — 진입점으로 되돌아간다.
        /// 퇴장 경로가 없으면 즉시 소멸. reason은 로그 문구 — 도주(격퇴)와 지형 보루를 구분한다.</summary>
        public void BeginExit(ThreatAgent agent, string reason)
        {
            if (agent == null || agent.IsExiting) return;
            // 상주형은 나가지 않는다 (M26-2차 W5) — 들판이 그들의 집이다. 퇴장 경로가
            // **이 문 하나뿐**이라(M21-W2 규약) 여기 한 줄로 도주·상한·대상소멸이 전부 막힌다.
            if (agent.IsResident) return;
            Debug.Log($"[Threat] 퇴장 개시 — {agent.So.DisplayName}: {reason}");
            PathResult exit = _pathfinder().FindPath(agent.TileX, agent.TileY,
                                                     agent.EntryTile.x, agent.EntryTile.y);
            if (exit.Kind == PathResultKind.PathFound) agent.SetExitPath(exit.Waypoints);
            else agent.DespawnNow(); // 퇴장 경로 없음·이미 가장자리 — 즉시 소멸
        }

        /// <summary>소멸 통지 (ThreatAgent 전용) — 활성 목록 정리 + 퇴장 로그.
        /// 무리의 마지막 개체가 사라지면 등록부도 지운다 (M21-W6 — 등록부 무한 성장 방지).
        ///
        /// 격퇴 성공 판정 (M21-W7)이 여기 있는 이유: 전투 퇴장의 두 얼굴(도주 = IsFleeing,
        /// 사냥 = 체력 0)이 전부 이 문을 지나므로 CombatService에 별도 통지 통로를 팔 필요가
        /// 없다 (명세 대상의 "CombatService 격퇴 통지"를 소멸 문 하나로 갈음 — 문이 적을수록
        /// 어긋날 자리도 적다). 성공 = **무리 전원이 전투로 물러남** — 한 마리라도 체류 상한·
        /// 대상 소멸로 나갔으면 이긴 것이 아니다.</summary>
        public void NotifyDespawn(ThreatAgent agent)
        {
            _active.Remove(agent);
            // 상주 (M26-2차 W5R) — 예약 장부만 갱신하고 끝. 아래 무리 도주선·격퇴 판정은 웨이브
            // 장부(_groupSpawned, 양수 키)를 보므로 음수 키인 상주는 어차피 안 걸리지만,
            // 뜻을 분명히 하기 위해 여기서 갈라 나간다 (상주의 소멸은 격퇴가 아니다).
            if (agent.IsResident) { OnResidentDespawn(agent); return; }
            bool combatExit = agent.IsFleeing || agent.Hp <= 0f; // 도주 전환·사냥 — 둘 다 전투의 결과
            if (combatExit)
            {
                _groupCombatExits.TryGetValue(agent.GroupKey, out int exits);
                _groupCombatExits[agent.GroupKey] = exits + 1;
            }

            bool groupRemains = false;
            foreach (ThreatAgent t in _active)
                if (t != null && t.GroupKey == agent.GroupKey) { groupRemains = true; break; }
            if (!groupRemains)
            {
                if (_groupSpawned.TryGetValue(agent.GroupKey, out int spawnedCount)
                    && _groupCombatExits.TryGetValue(agent.GroupKey, out int combatExits)
                    && combatExits >= spawnedCount)
                {
                    RegisterRepelRelief(agent.So);
                    // 격퇴 사건 발신 (M21-W9) — 완충과 같은 판정, 참여 전원 명단과 함께
                    if (_groupAttackers.TryGetValue(agent.GroupKey, out List<string> attackers)
                        && attackers.Count > 0)
                        OnRaidRepelled?.Invoke(agent.So, spawnedCount, attackers);
                }
                _groupSpawned.Remove(agent.GroupKey);
                _groupCombatExits.Remove(agent.GroupKey);
                _groupAttackers.Remove(agent.GroupKey);
                _groupSiege.Remove(agent.GroupKey); // 공성 등록부도 무리와 운명 공유 (M22-W5)
            }
            Debug.Log($"[Threat] 퇴장 — {agent.So.DisplayName}");
        }

        /// <summary>격퇴 성공의 완충 (M21-W7) — 이김의 보상: 다음 출몰 마릿수만 깎는다
        /// (지연 없음 — 이김에는 숨돌릴 틈이 필요 없다, §4).</summary>
        private void RegisterRepelRelief(ThreatSO so)
        {
            if (_reliefStacks >= ReliefStackMax) return; // 상한 — 누적이 위협을 0으로 만들 수는 없다
            _reliefStacks++;
            Debug.Log($"[Threat] 완충 — 격퇴의 보상: 다음 출몰 −{_reliefStacks}마리 " +
                      $"(스택 {_reliefStacks}/{ReliefStackMax}) — {so.DisplayName} 무리를 물리쳤다");
        }

        /// <summary>주민 사망의 완충 (M21-W7) — 자비: 손실이 마모가 아니라 리듬이 되게
        /// (림월드 adaptation의 이중 축). 원인 무관 — 아사도 마을의 상실이다.
        /// 호출처는 SimulationLoop.RecordDeath 하나 (사망의 공통 문 — Die/StarveToDeath 합류점).</summary>
        public void NotifyVillagerDeath()
        {
            if (_reliefStacks >= ReliefStackMax) return; // 지연도 같은 상한에 묶는다 — 무한 지연 금지
            _reliefStacks++;
            _delayDays += ReliefDelayPerDeath;
            Debug.Log($"[Threat] 완충 — 상실의 자비: 다음 출몰 −{_reliefStacks}마리 · " +
                      $"발동 +{ReliefDelayPerDeath:0.#}일 (스택 {_reliefStacks}/{ReliefStackMax} · " +
                      $"지연 누적 {_delayDays:0.#}일)");
        }

        // ── 무리 도주선 (M21-W6 — 판정은 CombatService, 여기는 등록부·집계·집행) ──

        /// <summary>이 무리가 처음 몇 마리로 태어났는가 (CombatService 전용). 미등록이면 false —
        /// 판정 불가 = 무리 도주 없음 (ShouldRout 의 "스폰 0 = 무리가 아니다"와 같은 방향).</summary>
        public bool TryGetGroupSpawned(int groupKey, out int spawned)
            => _groupSpawned.TryGetValue(groupKey, out spawned);

        /// <summary>아직 싸우는 개체 수 (CombatService 전용) — 도주·퇴장·사망(체력 0) 제외.</summary>
        public int CountFighting(int groupKey)
        {
            int n = 0;
            foreach (ThreatAgent t in _active)
                if (t != null && t.GroupKey == groupKey
                    && !t.IsFleeing && !t.IsExiting && t.Hp > 0f) n++;
            return n;
        }

        /// <summary>무리 붕괴 집행 (CombatService 전용) — 잔여 전원 도주 전환. 판정은 호출자가
        /// 이미 끝냈다 (ShouldRout — ADR-M21-3: 판정은 서비스 한 곳). 스냅샷 순회인 이유:
        /// BeginFlee → BeginExit 가 퇴장 경로 없는 개체를 즉시 소멸시켜 _active 를 수정한다.</summary>
        public void RoutGroup(int groupKey)
        {
            _routBuf.Clear();
            foreach (ThreatAgent t in _active)
                if (t != null && t.GroupKey == groupKey
                    && !t.IsFleeing && !t.IsExiting && t.Hp > 0f) _routBuf.Add(t);
            foreach (ThreatAgent t in _routBuf) t.BeginFlee();
        }

        /// <param name="focus">이번 타격이 실제로 맞힌 첫 대상의 위치 (표현 전용 — 개체가
        /// 그쪽을 보고 휘두른다). 아무도 못 맞혔으면 제자리 = 방향을 억지로 틀지 않는다.</param>
        private void ExecuteStrike(ThreatSO so, bool targetsVillagers, Vector2Int tile, out Vector2 focus)
        {
            focus = tile;
            int hit = 0;
            if (targetsVillagers)
            {
                // 후보 = 타격 반경 내 생존 주민. 부상자도 포함한다 (M21-W2 A1) — 체류형에서
                // 제외를 유지하면 다친 사람이 무적이 되어 위협이 누구도 못 죽인다.
                CollectVictimCandidates(tile, so.StrikeRadiusTiles, excludeInjured: false);
                int loss = DisasterService.LossCount(_victimKeyBuf.Count,
                    so.BaseLossPct, so.PerTargetPct, so.MaxLossPct);
                PickNearestVictims(tile.x, tile.y, _victimKeyBuf, loss, _victimIdxBuf);
                _struckBuf.Clear();
                foreach (int idx in _victimIdxBuf)
                {
                    // 피해의 유일한 문 (ADR-M21-2). 부상 상태는 여기서 파생된다 —
                    // 이미 부상 중이면 상태는 그대로 Light, 체력만 깎인다 (⚠️④).
                    _victimAgentBuf[idx].TakeDamage(so.StrikeDamage, DamageCause.Combat);
                    _struckBuf.Add(_victimAgentBuf[idx]);              // 대사 화자 = 실제 피격자
                    hit++;
                }
                if (_struckBuf.Count > 0) focus = _struckBuf[0].transform.position;
                Debug.Log($"[Threat] 타격 — {so.DisplayName}: {hit}/{_victimKeyBuf.Count}명 피격 " +
                          $"(1인 {so.StrikeDamage:0.#}) @ ({tile.x},{tile.y})");
            }
            else
            {
                // 대상 스냅샷 (재해 Strike 패턴 — 파괴 중 무효화 방지 복사본).
                // **타격 반경 안만** (2026-08-06 Play): 舊 코드는 맵 전체 밭에서 시드 셔플로 골랐다.
                // 홍수라면 옳지만(재해에서 물려받은 산식), 특정 밭까지 걸어와 눌러앉은 위협에게는
                // "여기 서 있는데 저쪽 밭이 사라진다"가 된다. W2 체류가 이 어긋남을 처음 보이게 했다 —
                // 예전엔 개체가 스치듯 지나가 아무도 대조할 수 없었다. 주민 타격과 같은 규칙
                // (CollectVictimCandidates의 맨해튼 반경)으로 맞춘다.
                _plotBuf.Clear();
                foreach (Vector2Int t in _construction.BuiltTilesOf(SlotId.FarmPlotCount))
                    if (Mathf.Abs(t.x - tile.x) + Mathf.Abs(t.y - tile.y) <= so.StrikeRadiusTiles)
                        _plotBuf.Add(t);
                int loss = DisasterService.LossCount(_plotBuf.Count,
                    so.BaseLossPct, so.PerTargetPct, so.MaxLossPct);
                uint seed = StableHash.Fnv1a(_strikeOrdinal.ToString(), so.DisplayName);
                DisasterService.PickVictims(_plotBuf.Count, loss, seed, _victimIdxBuf);
                foreach (int idx in _victimIdxBuf)
                    if (_construction.RemoveCountableAt(SlotId.FarmPlotCount,
                                                        _plotBuf[idx].x, _plotBuf[idx].y))
                    {
                        if (hit == 0) focus = _plotBuf[idx]; // 첫 밭 = 바라볼 곳
                        hit++;
                    }
                Debug.Log($"[Threat] 타격 — {so.DisplayName}: 밭 {hit}/{_plotBuf.Count} 소실");
            }
            OnStruck?.Invoke(so, targetsVillagers, hit, tile,
                             targetsVillagers ? (IReadOnlyList<VillagerAgent>)_struckBuf : EmptyVictims);
        }

        /// <summary>타격 후보 수집 — 생존 주민 중 기준점 radius(맨해튼) 이내. excludeInjured면
        /// 기존 부상자 제외. **M21-W2 이후 호출처는 전부 false다** (A1 — 부상자 재타격 허용).
        /// 인자는 남겨 둔다: 부상자를 빼야 하는 새 판정(예: 구조 대상 선정)이 오면 그 자리다.</summary>
        private void CollectVictimCandidates(Vector2Int from, int radius, bool excludeInjured)
        {
            _victimAgentBuf.Clear();
            _victimKeyBuf.Clear();
            foreach (VillagerAgent a in _agents)
            {
                if (a == null || a.State == AgentState.Dead) continue;
                if (a.IsOnTower) continue; // 망루 위는 닿지 않는다 (높이 — M22-3차 W3, ADR-M22-11)
                if (excludeInjured && a.Injury != InjurySeverity.None) continue;
                if (Mathf.Abs(a.TileX - from.x) + Mathf.Abs(a.TileY - from.y) > radius) continue;
                _victimAgentBuf.Add(a);
                _victimKeyBuf.Add((a.AgentId, a.TileX, a.TileY));
            }
        }

        /// <summary>감지 판정 (순수 — 게이트 M10-T4): 맨해튼 거리 ≤ ⌈감지 반경 × 개인 배율⌉.
        /// 배율 <1 = 반경 축소 = 늦게 알아챔 (고집쟁이 0.6 → 6타일이 4타일로).</summary>
        public static bool WithinDanger(int manhattanDist, int dangerRadiusTiles, float personalRadiusMult)
            => manhattanDist <= Mathf.CeilToInt(dangerRadiusTiles * personalRadiusMult);

        /// <summary>최근접 활성 위협의 좌표 (M11-G 노숙 도피의 방향 기준) — 맨해튼 최근접.
        /// 활성 위협이 없으면 false (호출처는 제자리 완료 — 기지 폴백 부활 금지, ⚠️③).</summary>
        public bool TryGetNearestThreatPos(int x, int y, out Vector2Int pos)
        {
            pos = default;
            int best = int.MaxValue;
            foreach (ThreatAgent t in _active)
            {
                if (t == null) continue;
                int d = Mathf.Abs(x - t.TileX) + Mathf.Abs(y - t.TileY);
                if (d >= best) continue;
                best = d;
                pos = new Vector2Int(t.TileX, t.TileY);
            }
            return best != int.MaxValue;
        }

        /// <summary>최근접 **교전 가능** 위협 개체 (M21-W4 — FightRunner 전용). 맨해튼 최근접.
        /// 🔴 도주·퇴장 중인 개체는 제외한다: 이미 물러나는 것을 새 교전 대상으로 삼으면 러너가
        /// 마을 밖까지 따라간다 (§W4 DoD ④ "무한 추격 0"). 격퇴는 그 자체로 이김이라
        /// 쫓아갈 이유가 없다 — 도망가는 짐승을 잡는 것은 사냥꾼(W8)의 몫이다.
        /// TryGetNearestThreatPos(도피 방향용)와 목록은 같지만 **필터가 다르다**: 도망칠 때는
        /// 물러나는 위협도 무섭다.</summary>
        /// <param name="senseMult">개인 감지 배율 (`FleeRadius` — ThreatNear와 같은 자).
        /// 🔴 **상주에만 건다** (M26-2차 W5R): F 명령이 **미발견** 상주를 맵 끝까지 찾아가
        /// "주민이 맵을 밝힌다"를 깼다 (2026-08-11 Play 관측). 웨이브는 예고·도착이 HUD로
        /// 방송되는 공동 지식이라 기존 동작 그대로다 (중립 불변식 — 습격 대응 무회귀).</param>
        public bool TryGetNearestFightable(int x, int y, float senseMult, out ThreatAgent threat)
        {
            threat = null;
            int best = int.MaxValue;
            foreach (ThreatAgent t in _active)
            {
                if (t == null || t.IsFleeing || t.IsExiting) continue;
                int d = Mathf.Abs(x - t.TileX) + Mathf.Abs(y - t.TileY);
                // 상주는 감지 반경 안이어야 "안다" — 아무도 그 존재를 방송하지 않았다.
                if (t.IsResident && !WithinDanger(d, t.So.DangerRadiusTiles, senseMult)) continue;
                if (d >= best) continue;
                best = d;
                threat = t;
            }
            return threat != null;
        }

        /// <summary>진행 중 습격 요약 (M21-W8 — HUD 상시 프롬프트 전용, ADR-M21-7 "명령을 쓸
        /// 시점은 화면이 알려준다"). 체류 중(도착~퇴장 전) 개체가 있으면 true — 접근 중·퇴장 중은
        /// 습격이 아니다 (프롬프트가 일찍 뜨면 "습격 중"이 거짓말이 된다). 대표 SO = 첫 체류 개체.</summary>
        public bool TryGetActiveRaid(out ThreatSO so, out int stayingCount)
        {
            so = null;
            stayingCount = 0;
            foreach (ThreatAgent t in _active)
            {
                if (t == null || !t.Staying) continue;
                // 상주 제외 (M26-2차 W5R) — 상주는 태어나자마자 도착해 **영원히 체류**라, 세면
                // Day 0부터 "마을 습격 중"이 뜬다 (2026-08-11 Play 관측). 들판에 사는 것은
                // 습격이 아니다 — 이 배너는 마을로 온 웨이브의 것이다.
                if (t.IsResident) continue;
                if (so == null) so = t.So;
                if (t.So == so) stayingCount++;
            }
            return so != null;
        }

        /// <summary>내 근처에 활성 위협이 있는가 (M10-D ThreatNear 슬롯의 유일한 원천).
        /// personalRadiusMult = 성격 감지 배율 (고집쟁이 0.6 = 늦게 알아챈다).</summary>
        public bool IsNearThreat(int x, int y, float personalRadiusMult)
        {
            foreach (ThreatAgent t in _active)
            {
                if (t == null) continue;
                int d = Mathf.Abs(x - t.TileX) + Mathf.Abs(y - t.TileY);
                if (WithinDanger(d, t.So.DangerRadiusTiles, personalRadiusMult)) return true;
            }
            return false;
        }
    }
}
