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
    /// 타깃 종류(밭/주민)는 밴드 고정이 아니라 출몰별 시드 롤 (ADR-M10R-3 — 곰도 밭을 칠 수 있다).
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
        private ThreatSO _pending;      // 예고~발동 사이 고정 — 예고한 그 위협이 온다
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

        /// <summary>예고 구간 진행 중인 위협 (주민 술렁임 판독용 — Season.NextCrisis 패턴). null = 평시.</summary>
        public ThreatSO Forecasting => _pending;

        /// <summary>발동까지 남은 게임일 (M13-B) — 예고 중이 아니면 음수. 표시 전용 파생값:
        /// 쓰기 없음, 예고·발동 판정은 Tick이 그대로 소유한다 (ADR-M0-3 상태 쓰기 단일 지점).
        /// 기존 예고 알림(WarnDays 상수 1회)과 달리 매일 줄어드는 카운트다운의 원천.</summary>
        public float DaysToStrike(float gameTime)
            => _pending == null ? -1f
                                : _lastStrikeDay + _pending.PeriodDays + _delayDays - gameTime; // 지연 포함 (M21-W7) — 판정(Tick)과 같은 시계

        public ThreatService(ThreatSO[] threats, SeasonService season,
                             ConstructionService construction, IReadOnlyList<VillagerAgent> agents,
                             WorldConfigSO config, Func<IPathfinder> pathfinder,
                             Func<int, int, bool> isWalkable, Transform parent)
        {
            _threats = threats ?? Array.Empty<ThreatSO>();
            _season = season;
            _construction = construction;
            _agents = agents;
            _config = config;
            _pathfinder = pathfinder;
            _isWalkable = isWalkable;
            _parent = parent;
        }

        // ── 순수 판정 (게이트 M10-T3) ─────────────────────────────────────────

        /// <summary>마을 규모 (순수): 주민 + 밭 + 집 — 성장의 3축만 (모닥불 등 필수 건물 제외).</summary>
        public static int VillageScale(int aliveCount, int farmPlots, int houses)
            => aliveCount + farmPlots + houses;

        /// <summary>활성 밴드 (순수, ADR-M10R-1): UnlockDay ≤ day 중 최신(최대 UnlockDay). 동률은 배열 앞.
        /// 충족 밴드가 없으면 null. 게임일은 단조 증가 → 한 번 열린 밴드는 닫히지 않는다(래칫 — 사망해도
        /// 위협이 강등되지 않는다, §0 음성 피드백 루프 해소).</summary>
        public static ThreatSO PickTier(ThreatSO[] threats, float day)
        {
            ThreatSO best = null;
            if (threats != null)
                foreach (ThreatSO t in threats)
                {
                    if (t == null || t.UnlockDay > day) continue;
                    if (best == null || t.UnlockDay > best.UnlockDay) best = t;
                }
            return best;
        }

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

        /// <summary>이 계절에 야수가 굶는가 (순수 — 게이트 M21-T7). 야생 먹이가 어는 계절이면 늑대도 굶는다.
        /// 🔴 IsCrisis가 **아니다**: Season_Summer도 IsCrisis:1 이라 그걸 쓰면 여름 늑대가 사나워진다.
        /// 겨울만 참인 것은 ForageFrozen이고, 의미도 정확히 겹치므로 새 필드를 만들지 않는다 (기존 통로 우선).
        /// ⚠️ 갈라지는 조건: "채집은 막히는데 야수는 안 굶는" 계절이 생기면 그때 전용 필드로 분리한다.</summary>
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

            // 예고 진입 — 밴드는 예고 시점 게임일로 확정하고 발동까지 고정 (예고한 그놈이 온다).
            // 지연(_delayDays, M21-W7)은 예고·발동 **둘 다**에 얹는다 — 예고만 제때 나가고 발동이
            // 늦으면 "1일 후"가 거짓말이 된다 (예고 문구와 실제 간격은 한 값이어야 한다).
            if (_pending == null)
            {
                ThreatSO tier = PickTier(_threats, gameTime); // 규모 아님 — 시간 래칫 (ADR-M10R-1)
                if (tier == null) return; // 전 밴드 미달 — 위협 없음 (스케줄도 흐르지 않는다)
                if (gameTime >= _lastStrikeDay + tier.PeriodDays + _delayDays - tier.WarnDays)
                {
                    _pending = tier;
                    Debug.Log($"[Threat] 예고 — {tier.DisplayName} ({tier.WarnDays:0.#}일 후)");
                    OnForecast?.Invoke(tier);
                }
                return;
            }

            // 발동
            if (gameTime >= _lastStrikeDay + _pending.PeriodDays + _delayDays)
            {
                _lastStrikeDay = gameTime;
                ThreatSO striking = _pending;
                _pending = null; // 예고 해제 — 개체가 맵에 있는 동안 술렁임은 도망(M10-D)이 대신한다
                Spawn(striking);
            }
        }

        // ── 출몰·타격 (개체는 표현+이동, 판정은 여기 — 명세 M10-C ⚠️③) ─────────

        private void Spawn(ThreatSO so)
        {
            _strikeOrdinal++;
            // 타깃 종류는 출몰 시 1회 확정 (ADR-M10R-4). 확률만 계절 보정을 받는다 (M21-W2R) —
            // 겨울엔 늑대가 굶어 주민 쪽으로 기운다. 시드는 그대로라 결정성은 유지된다.
            bool hungry = PredatorHungryNow;
            float chance = EffectiveVillagerChance(so.VillagerTargetChance, hungry, so.HungrySeasonChanceMult);
            bool targetsVillagers = RollTargetsVillagers(so, _strikeOrdinal, chance);
            MapBounds.Get(out int minX, out int maxX, out int minY, out int maxY);
            uint seed = StableHash.Fnv1a(_strikeOrdinal.ToString(), so.DisplayName);
            Vector2Int entry = EntryPoint(seed, minX, maxX, minY, maxY);
            Vector2Int target = PickTargetTile(so, targetsVillagers, entry);

            PathResult path = _pathfinder().FindPath(entry.x, entry.y, target.x, target.y);
            if (path.Kind == PathResultKind.Unreachable)
            {
                // 이번 발동은 건너뛴다 (무한 재시도 금지 — 명세 ⚠️④). 다음 주기에 재출몰.
                // 대표 경로(진입점→목표)가 없으면 인접 산개 지점도 못 간다고 보고 무리 전체를 접는다.
                Debug.LogWarning($"[Threat] {so.DisplayName}: 진입 경로 없음 ({entry.x},{entry.y})→({target.x},{target.y}) — 이번 출몰 생략");
                return;
            }

            // 마릿수 (M21-W6, ADR-M21-6) — 완충(M21-W7)은 여기서 전부 소모된다 (1회성):
            // 스택은 마릿수 감산으로, 지연은 이미 발동 시각에 반영됐다. 예고는 마릿수를 말하지
            // 않았다 (DoD ④ — 정찰 재탄생의 여지): 몇 마리인지는 여기 도착해서야 드러난다.
            int relief = _reliefStacks;
            _reliefStacks = 0;
            _delayDays = 0f;
            int count = SpawnCount(so.SpawnCountBase, so.UnlockDay, _gameTime,
                                   so.CountGrowthEveryDays, so.SpawnCountMax, relief);
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                // 첫 마리 = 진입점 그대로 (count=1 이면 기존과 완전 동일 — 중립 불변식, DoD ③).
                // 나머지 = 진입점 곁 산개 (겹침 방지 오프셋 — 표현이므로 난수 허용, ADR-M21-10).
                Vector2Int at = i == 0 || _isWalkable == null
                    ? entry : MapBounds.PickWalkableNear(_isWalkable, entry.x, entry.y, 2);
                PathResult p = at == entry
                    ? path : _pathfinder().FindPath(at.x, at.y, target.x, target.y);
                if (p.Kind == PathResultKind.Unreachable)
                {
                    // 이 마리만 생략 (⚠️③ — Unreachable 생략 규약을 마리 단위로). 분모에도 안 센다.
                    Debug.LogWarning($"[Threat] {so.DisplayName}: 산개 지점 ({at.x},{at.y}) 경로 없음 — 개체 1 생략");
                    continue;
                }

                var go = new GameObject($"Threat_{so.name}_{_strikeOrdinal}_{i}");
                go.transform.SetParent(_parent, worldPositionStays: false);
                go.transform.position = new Vector3(at.x, at.y, 0f); // ADR-M0-9 — X-Y 평면
                ThreatAgent agent = go.AddComponent<ThreatAgent>();
                agent.Init(so, this, at, target,
                           p.Kind == PathResultKind.PathFound ? p.Waypoints : null,
                           _pathfinder(), targetsVillagers, // 추격 재경로용 + 이번 출몰 타깃 종류 (M10R)
                           _strikeOrdinal);                 // 무리 키 (M21-W6)
                _active.Add(agent);
                spawned++;
            }
            if (spawned == 0) return; // 전 개체 생략 — 위의 개별 경고가 이미 말했다
            _groupSpawned[_strikeOrdinal] = spawned; // 무리 도주선의 분모 = 실제 태어난 수
            Debug.Log($"[Threat] 출몰 — {so.DisplayName}{(spawned > 1 ? $" ×{spawned}" : "")} " +
                      $"@ ({entry.x},{entry.y}) → ({target.x},{target.y}) " +
                      $"[{(targetsVillagers ? "주민" : "밭")} 타깃]{(hungry ? " · 배고픈 계절" : "")}" +
                      $"{(relief > 0 ? $" · 완충 −{relief}" : "")}");
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
            if (!agent.TargetsVillagers && !HasFarmTargetsNear(agent.So, here))
            {
                BeginExit(agent, "칠 대상이 없다");
                return;
            }
            ExecuteStrike(agent.So, agent.TargetsVillagers, here);
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
            float limit = StayLimitOf(agent);
            if (ShouldGiveUpStay(agent.ArrivedDay, _gameTime, limit))
            {
                BeginExit(agent, $"체류 상한 {limit:0.##}일");
                return;
            }
            if (!ShouldRepeatStrike(agent.LastStrikeDay, _gameTime, agent.So.RepeatStrikePeriodDays)) return;

            var here = new Vector2Int(agent.TileX, agent.TileY);
            if (!agent.TargetsVillagers)
            {
                if (!HasFarmTargetsNear(agent.So, here))
                {
                    BeginExit(agent, "칠 대상이 없다");
                    return;
                }
            }
            else if (!HasVillagerTargetsNear(agent.So, here)) return; // 사거리 밖 = 아직 못 잡았다 (추격이 잇는다)

            ExecuteStrike(agent.So, agent.TargetsVillagers, here);
            agent.MarkStruck(_gameTime);
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
        /// (같은 판에서 늑대가 매번 같은 길로 걸으면 그게 더 이상하다). 타깃 종류·출몰 스케줄·
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
                    RegisterRepelRelief(agent.So);
                _groupSpawned.Remove(agent.GroupKey);
                _groupCombatExits.Remove(agent.GroupKey);
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

        private void ExecuteStrike(ThreatSO so, bool targetsVillagers, Vector2Int tile)
        {
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
                Debug.Log($"[Threat] 타격 — {so.DisplayName}: {hit}/{_victimKeyBuf.Count}명 피격 " +
                          $"(1인 {so.StrikeDamage:0.#}) @ ({tile.x},{tile.y})");
            }
            else
            {
                // 대상 스냅샷 (재해 Strike 패턴 — 파괴 중 무효화 방지 복사본).
                // **타격 반경 안만** (2026-08-06 Play): 舊 코드는 맵 전체 밭에서 시드 셔플로 골랐다.
                // 홍수라면 옳지만(재해에서 물려받은 산식), 특정 밭까지 걸어와 눌러앉은 늑대에게는
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
                                                        _plotBuf[idx].x, _plotBuf[idx].y)) hit++;
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
        /// 물러나는 늑대도 무섭다.</summary>
        public bool TryGetNearestFightable(int x, int y, out ThreatAgent threat)
        {
            threat = null;
            int best = int.MaxValue;
            foreach (ThreatAgent t in _active)
            {
                if (t == null || t.IsFleeing || t.IsExiting) continue;
                int d = Mathf.Abs(x - t.TileX) + Mathf.Abs(y - t.TileY);
                if (d >= best) continue;
                best = d;
                threat = t;
            }
            return threat != null;
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
