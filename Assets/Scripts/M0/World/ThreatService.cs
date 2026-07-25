using System;
using System.Collections.Generic;
using AIVillage.Core;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 야생 위협 (M10-C) — 스케줄(예고→출몰)·타격 판정의 시뮬 서비스. 파괴·부상은 직접 하지 않고
    /// 문(Construction.RemoveCountableAt / VillagerAgent.Injure)을 호출한다 (ADR-M9-3 사상).
    /// 확정 발동·확정 착탄 (ADR-M10-1): 스케줄·희생 선정에 확률 없음 — 주민 희생은 거리순
    /// (도망 행동과의 인과), 밭 희생은 StableHash 시드 셔플 (재해와 동일).
    /// 활성 밴드 = 게임일 UnlockDay 충족 중 최신 1개 (ADR-M10R-1 시간 래칫 — 사망해도 강등 없음).
    /// 타깃 종류(밭/주민)는 밴드 고정이 아니라 출몰별 시드 롤 (ADR-M10R-3 — 곰도 밭을 칠 수 있다).
    /// Threats가 비면 SimulationLoop이 서비스 자체를 null로 둔다 (중립 불변식, DisasterService 패턴).
    /// 세이브 대상 = _lastStrikeDay·_strikeOrdinal (ADR-M10-10). 진행 중 개체·예고 상태는 저장 안 함
    /// (로드 후 다음 스케줄에서 재출몰).
    /// </summary>
    public sealed class ThreatService
    {
        private readonly ThreatSO[] _threats;
        private readonly ZoneService _zones;
        private readonly ConstructionService _construction;
        private readonly IReadOnlyList<VillagerAgent> _agents;
        private readonly WorldConfigSO _config;
        private readonly Func<IPathfinder> _pathfinder; // Awake 조립 순서 무관하게 지연 조회
        private readonly Transform _parent;

        private float _lastStrikeDay;   // 마지막 발동 시각 (게임일). 세이브 대상 (ADR-M10-10)
        private int _strikeOrdinal;     // 발동 누적 서수 — 진입점·밭 희생 시드의 유일 키. 세이브 대상
        private ThreatSO _pending;      // 예고~발동 사이 고정 — 예고한 그 위협이 온다

        private readonly List<ThreatAgent> _active = new List<ThreatAgent>(2);
        private readonly List<VillagerAgent> _victimAgentBuf = new List<VillagerAgent>(8);
        private readonly List<(string id, int x, int y)> _victimKeyBuf = new List<(string, int, int)>(8);
        private readonly List<Vector2Int> _plotBuf = new List<Vector2Int>(16);
        private readonly List<int> _victimIdxBuf = new List<int>(16);

        /// <summary>예고 알림 (1회/발동) — HUD 경보 구독 (표현).</summary>
        public event Action<ThreatSO> OnForecast;

        /// <summary>타격 알림 (위협, 주민타격 여부, 피해 수, 타격 타일, **실제 부상자**) — HUD·반응 대사 구독.
        /// victims = 이번 타격으로 Injure된 주민 (밭 타격이면 빈 목록). 대사 화자를 "근처 아무나"가 아니라
        /// 실제 부상자로 못박기 위한 것 — 상태와 표현이 어긋나면 "물렸다는데 아무도 안 다침"이 된다
        /// (2026-07-24 Play 관측). 버퍼 재사용이므로 구독자는 동기 소비만 할 것.</summary>
        public event Action<ThreatSO, bool, int, Vector2Int, IReadOnlyList<VillagerAgent>> OnStruck;

        private static readonly VillagerAgent[] EmptyVictims = Array.Empty<VillagerAgent>();
        private readonly List<VillagerAgent> _struckBuf = new List<VillagerAgent>(8);

        /// <summary>예고 구간 진행 중인 위협 (주민 술렁임 판독용 — Season.NextCrisis 패턴). null = 평시.</summary>
        public ThreatSO Forecasting => _pending;

        public ThreatService(ThreatSO[] threats, ZoneService zones,
                             ConstructionService construction, IReadOnlyList<VillagerAgent> agents,
                             WorldConfigSO config, Func<IPathfinder> pathfinder, Transform parent)
        {
            _threats = threats ?? Array.Empty<ThreatSO>();
            _zones = zones;
            _construction = construction;
            _agents = agents;
            _config = config;
            _pathfinder = pathfinder;
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
        /// 0=항상 밭, 1=항상 주민. 매 출몰 1회만 롤한다 (ADR-M10R-4 — 재타겟은 종류를 안 바꾼다).</summary>
        public static bool RollTargetsVillagers(ThreatSO so, int ordinal)
        {
            if (so.VillagerTargetChance <= 0f) return false;
            if (so.VillagerTargetChance >= 1f) return true;
            uint h = StableHash.Fnv1a(ordinal.ToString(), so.DisplayName + "|tgt");
            float r = (h & 0xFFFFFFu) / (float)0x1000000; // [0,1)
            return r < so.VillagerTargetChance;
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
            // 예고 진입 — 밴드는 예고 시점 게임일로 확정하고 발동까지 고정 (예고한 그놈이 온다)
            if (_pending == null)
            {
                ThreatSO tier = PickTier(_threats, gameTime); // 규모 아님 — 시간 래칫 (ADR-M10R-1)
                if (tier == null) return; // 전 밴드 미달 — 위협 없음 (스케줄도 흐르지 않는다)
                if (gameTime >= _lastStrikeDay + tier.PeriodDays - tier.WarnDays)
                {
                    _pending = tier;
                    Debug.Log($"[Threat] 예고 — {tier.DisplayName} ({tier.WarnDays:0.#}일 후)");
                    OnForecast?.Invoke(tier);
                }
                return;
            }

            // 발동
            if (gameTime >= _lastStrikeDay + _pending.PeriodDays)
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
            bool targetsVillagers = RollTargetsVillagers(so, _strikeOrdinal); // 출몰 시 1회 확정 (ADR-M10R-4)
            MapBounds.Get(out int minX, out int maxX, out int minY, out int maxY);
            uint seed = StableHash.Fnv1a(_strikeOrdinal.ToString(), so.DisplayName);
            Vector2Int entry = EntryPoint(seed, minX, maxX, minY, maxY);
            Vector2Int target = PickTargetTile(so, targetsVillagers, entry);

            PathResult path = _pathfinder().FindPath(entry.x, entry.y, target.x, target.y);
            if (path.Kind == PathResultKind.Unreachable)
            {
                // 이번 발동은 건너뛴다 (무한 재시도 금지 — 명세 ⚠️④). 다음 주기에 재출몰.
                Debug.LogWarning($"[Threat] {so.DisplayName}: 진입 경로 없음 ({entry.x},{entry.y})→({target.x},{target.y}) — 이번 출몰 생략");
                return;
            }

            var go = new GameObject($"Threat_{so.name}_{_strikeOrdinal}");
            go.transform.SetParent(_parent, worldPositionStays: false);
            go.transform.position = new Vector3(entry.x, entry.y, 0f); // ADR-M0-9 — X-Y 평면
            ThreatAgent agent = go.AddComponent<ThreatAgent>();
            agent.Init(so, this, entry, target,
                       path.Kind == PathResultKind.PathFound ? path.Waypoints : null,
                       _pathfinder(), targetsVillagers); // 추격 재경로용 + 이번 출몰 타깃 종류 (M10R)
            _active.Add(agent);
            Debug.Log($"[Threat] 출몰 — {so.DisplayName} @ ({entry.x},{entry.y}) → ({target.x},{target.y}) " +
                      $"[{(targetsVillagers ? "주민" : "밭")} 타깃]");
        }

        /// <summary>목표 타일: 주민 타격 = 진입점 최근접 생존 주민, 밭 타격 = 밭 구역 앵커.
        /// 폴백(주민 0·구역 미확정)은 기지 — 위협은 항상 마을 심장부로 향한다.</summary>
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
            else if (_zones.TryGetZone(SlotId.FarmPlotCount, out Vector2Int anchor, out _))
                return anchor;
            return new Vector2Int(_config.BaseTileX, _config.BaseTileY);
        }

        /// <summary>
        /// 추격 대상 (M10-C 개정 2026-07-22: 고정 타깃 → 추격 — 고정 타깃은 도망이 구조적으로
        /// 100% 이겨 부상이 착탄 불가, Day40+ 관측) — 최근접 비부상 생존 주민. 기부상자 제외:
        /// 중복 부상이 무시되므로(CanInjure) 쫓아봐야 빈 타격이다. 없으면 false (빈 타격 후 퇴장).
        /// </summary>
        public bool TryPickChaseTile(int fromX, int fromY, out Vector2Int tile)
        {
            CollectVictimCandidates(new Vector2Int(fromX, fromY), int.MaxValue, excludeInjured: true);
            int idx = M0SimulationLoop.PickNearestIndex(fromX, fromY, _victimKeyBuf);
            if (idx >= 0)
            {
                tile = new Vector2Int(_victimKeyBuf[idx].x, _victimKeyBuf[idx].y);
                return true;
            }
            tile = default;
            return false;
        }

        /// <summary>타격 사거리 판정 (추격형 전용) — 개체 현 위치 기준 StrikeRadius 내 부상 가능
        /// 주민 존재 여부. 판정은 서비스, 개체는 묻기만 한다 (명세 ⚠️③ 유지).</summary>
        public bool IsInStrikeRange(ThreatAgent agent)
        {
            CollectVictimCandidates(new Vector2Int(agent.TileX, agent.TileY),
                                    agent.So.StrikeRadiusTiles, excludeInjured: true);
            return _victimKeyBuf.Count > 0;
        }

        /// <summary>타격 통지 (ThreatAgent 전용) — 타격 지점 = 개체 현 위치 (추격형은 잡은 자리,
        /// 밭형은 도착 시 목표 타일과 동일). 타격 실행 후 퇴장 경로를 되돌려준다.</summary>
        public void NotifyArrived(ThreatAgent agent)
        {
            var strikeTile = new Vector2Int(agent.TileX, agent.TileY);
            ExecuteStrike(agent.So, agent.TargetsVillagers, strikeTile);
            PathResult exit = _pathfinder().FindPath(strikeTile.x, strikeTile.y,
                                                     agent.EntryTile.x, agent.EntryTile.y);
            if (exit.Kind == PathResultKind.PathFound) agent.SetExitPath(exit.Waypoints);
            else agent.DespawnNow(); // 퇴장 경로 없음·이미 가장자리 — 즉시 소멸
        }

        /// <summary>소멸 통지 (ThreatAgent 전용) — 활성 목록 정리 + 퇴장 로그.</summary>
        public void NotifyDespawn(ThreatAgent agent)
        {
            _active.Remove(agent);
            Debug.Log($"[Threat] 퇴장 — {agent.So.DisplayName}");
        }

        private void ExecuteStrike(ThreatSO so, bool targetsVillagers, Vector2Int tile)
        {
            int hit = 0;
            if (targetsVillagers)
            {
                // 후보 = 타격 반경 내 생존·비부상 주민 (기존 부상자 제외 — 중복 부상 방지, 게이트)
                CollectVictimCandidates(tile, so.StrikeRadiusTiles, excludeInjured: true);
                int loss = DisasterService.LossCount(_victimKeyBuf.Count,
                    so.BaseLossPct, so.PerTargetPct, so.MaxLossPct);
                PickNearestVictims(tile.x, tile.y, _victimKeyBuf, loss, _victimIdxBuf);
                _struckBuf.Clear();
                foreach (int idx in _victimIdxBuf)
                {
                    _victimAgentBuf[idx].Injure(InjurySeverity.Light); // 부상의 유일한 문 (ADR-M10-2)
                    _struckBuf.Add(_victimAgentBuf[idx]);              // 대사 화자 = 실제 부상자
                    hit++;
                }
                Debug.Log($"[Threat] 발동 — {so.DisplayName}: 부상 {hit}/{_victimKeyBuf.Count}명 @ ({tile.x},{tile.y})");
            }
            else
            {
                // 대상 스냅샷 (재해 Strike 패턴 — 파괴 중 무효화 방지 복사본)
                _plotBuf.Clear();
                _plotBuf.AddRange(_construction.BuiltTilesOf(SlotId.FarmPlotCount));
                int loss = DisasterService.LossCount(_plotBuf.Count,
                    so.BaseLossPct, so.PerTargetPct, so.MaxLossPct);
                uint seed = StableHash.Fnv1a(_strikeOrdinal.ToString(), so.DisplayName);
                DisasterService.PickVictims(_plotBuf.Count, loss, seed, _victimIdxBuf);
                foreach (int idx in _victimIdxBuf)
                    if (_construction.RemoveCountableAt(SlotId.FarmPlotCount,
                                                        _plotBuf[idx].x, _plotBuf[idx].y)) hit++;
                Debug.Log($"[Threat] 발동 — {so.DisplayName}: 밭 {hit}/{_plotBuf.Count} 소실");
            }
            OnStruck?.Invoke(so, targetsVillagers, hit, tile,
                             targetsVillagers ? (IReadOnlyList<VillagerAgent>)_struckBuf : EmptyVictims);
        }

        /// <summary>부상 후보 수집 — 생존 주민 중 기준점 radius(맨해튼) 이내. excludeInjured면
        /// 기존 부상자 제외 (Injure의 중복 무시와 이중 방어 — 게이트 M10-T3).</summary>
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
