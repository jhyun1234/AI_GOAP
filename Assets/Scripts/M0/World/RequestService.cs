using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 주민→주민 부탁 (M8-D) — 짝 찾기는 이 서비스 하나가 주기 틱으로 수행하고, 한 주기 최대
    /// 1건 (ADR-M8-7, ChatterService 패턴 계승). 확률 게이트 없음 — 필요(의뢰인 조건)가 동기다.
    /// 희소성은 반경·의뢰인 쿨다운·조건이 만든다. 판정은 VillagerAgent.JudgeRequest (결정적,
    /// ADR-M8-2), 모든 거절은 사유 말풍선+로그 (ADR-M8-5). 연출은 대화 통로 재사용 (말풍선·
    /// 지연 응수·마주보기). 관계 델타는 RelationshipService 경유 (ADR-M8-1의 원천 ②).
    /// </summary>
    public sealed class RequestService
    {
        private readonly WorldConfigSO _world;
        private readonly AgentConfigSO _agentCfg;
        private readonly RelationshipService _relationship;
        private readonly OwnershipService _ownership;
        private readonly ConstructionService _construction;
        private readonly ChatterService _chatter; // 대화 쿨다운 공유 — 장면 연쇄 방지 (2026-07-17 피드백)
        private readonly WorldModel _worldModel;  // 주민 간 보상의 스톡 차감 (M8 후속 — TrySpendStock만)
        private readonly IReadOnlyList<VillagerAgent> _agents; // SimulationLoop 소유 리스트 (살아있는 참조)

        private float _nextScanAt;
        // 의뢰인 개인 쿨다운 — 거절당하면 한동안 다시 조르지 않는다 (수락 시에도 기록 — 중복 방지 이중화)
        private readonly Dictionary<string, float> _requesterCooldownUntil = new Dictionary<string, float>(16);
        // 진행 중 부탁 (수락~완수): 수락자 ID → (부탁, 의뢰인 ID, 선불 완료 여부). 세이브 대상 (ADR-M0-10)
        private readonly Dictionary<string, (RequestSO so, string requesterId, bool prepaid)> _inFlight =
            new Dictionary<string, (RequestSO, string, bool)>(4);
        // 보상 미정산 빚 (조각 Y, 2026-07-18): 수행자 ID → (부탁, 의뢰인 ID, 선불 여부).
        // 일 완수 순간 기록되고, 수행자와 의뢰인이 자연스럽게 마주치면(TickRewardSettlement) 정산·소멸한다.
        // 쫓아가지 않으므로 타임아웃 소실 없음 — 정리는 이탈(ReleaseBy)뿐. 저장 불필요(연출·로드 후 소멸).
        private readonly Dictionary<string, (RequestSO so, string requesterId, bool prepaid)> _pendingReports =
            new Dictionary<string, (RequestSO, string, bool)>(4);
        private readonly List<VillagerAgent> _scratch = new List<VillagerAgent>(16);

        public RequestService(WorldConfigSO world, AgentConfigSO agentCfg, RelationshipService relationship,
                              OwnershipService ownership, ConstructionService construction,
                              IReadOnlyList<VillagerAgent> agents, ChatterService chatter = null,
                              WorldModel worldModel = null)
        {
            _world = world;
            _agentCfg = agentCfg;
            _relationship = relationship;
            _ownership = ownership;
            _construction = construction;
            _agents = agents;
            _chatter = chatter;
            _worldModel = worldModel;
        }

        /// <summary>정보줄용 (M8 후속) — agentId가 수락해 진행 중인 부탁의 (의뢰인, 할 일 라벨).</summary>
        public bool TryGetAssignment(string agentId, out string requesterId, out string taskLabel)
        {
            if (_inFlight.TryGetValue(agentId, out (RequestSO so, string requesterId, bool prepaid) rec))
            {
                requesterId = rec.requesterId;
                taskLabel = rec.so.TaskLabelOrDefault;
                return true;
            }
            requesterId = null;
            taskLabel = null;
            return false;
        }

        /// <summary>
        /// 떼먹기 판정 (순수 — 게이트 대상, ADR-보상1): 의뢰인 성격의 친밀 문턱 미만이면 떼먹음.
        /// 랜덤 금지 — 관계 표기로 예측 가능. p null·기본값 -100 = 판정 성립 불가 (절대 안 떼먹음).
        /// </summary>
        public static bool ShouldStiffReward(PersonalitySO p, int affinityTowardBuilder)
            => p != null && affinityTowardBuilder < p.SkipRewardBelowAffinity;

        /// <summary>agentId가 의뢰인으로 걸어 둔 진행 중 부탁이 있는가 — 중복 부탁 방지.</summary>
        public bool HasInFlightFrom(string requesterId)
        {
            foreach (KeyValuePair<string, (RequestSO so, string requesterId, bool prepaid)> kv in _inFlight)
                if (kv.Value.requesterId == requesterId) return true;
            return false;
        }

        /// <summary>
        /// 주기 스캔 (SimulationLoop 틱에서 호출) — 셔플 순회로 성립 (의뢰인, 대상) 1건만.
        /// 의뢰인 조건은 스냅샷 대역 판정 (GoalSelector.AllHold 재사용 — goal 조건과 같은 언어).
        /// </summary>
        public void Tick(float nowSec, IReadOnlyList<VillagerAgent> agents)
        {
            TickRewardSettlement(nowSec); // 보상 빚 정산 — 목수·의뢰인이 마주치면 지급 (조각 Y)

            if (_world.Requests == null || _world.Requests.Length == 0) return; // 중립 — 부탁 없음
            if (nowSec < _nextScanAt) return;
            _nextScanAt = nowSec + _world.RequestIntervalSec;

            _scratch.Clear();
            for (int i = 0; i < agents.Count; i++)
            {
                VillagerAgent a = agents[i];
                if (a == null || a.State == AgentState.Dead) continue;
                _scratch.Add(a);
            }
            for (int i = _scratch.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (_scratch[i], _scratch[j]) = (_scratch[j], _scratch[i]);
            }

            foreach (VillagerAgent requester in _scratch)
            {
                if (IsRequesterCoolingDown(requester.AgentId, nowSec)) continue;
                if (HasInFlightFrom(requester.AgentId)) continue; // 이미 걸어 둔 부탁 완수 대기
                // 대화 직후 주민은 부탁 장면도 쉼 — 장면이 연달아 붙으면 읽을 수 없다 (공용 쿨다운)
                if (_chatter != null && _chatter.IsCoolingDown(requester.AgentId, nowSec)) continue;

                WorldSnapshot snap = requester.BuildSnapshot();
                foreach (RequestSO r in _world.Requests)
                {
                    if (r == null || r.InjectGoal == null) continue;
                    if (!GoalSelector.AllHold(r.RequesterConditions, snap)) continue;

                    foreach (VillagerAgent target in _scratch)
                    {
                        if (target == requester) continue;
                        if (_chatter != null && _chatter.IsCoolingDown(target.AgentId, nowSec)) continue;
                        if (r.TargetJob != null && target.Job != r.TargetJob) continue; // 참조 매핑 (ADR-M0-1)
                        int dist = Mathf.Abs(requester.TileX - target.TileX)
                                 + Mathf.Abs(requester.TileY - target.TileY);
                        if (dist > r.RadiusTiles) continue;

                        Ask(r, requester, target, nowSec);
                        return; // 한 주기 최대 1건 (ADR-M8-7)
                    }
                }
            }
        }

        /// <summary>의뢰인 쿨다운 판정 (게이트 대상 아님 — ChatterService.IsCoolingDown과 동일 구조).</summary>
        public bool IsRequesterCoolingDown(string requesterId, float nowSec)
            => _requesterCooldownUntil.TryGetValue(requesterId, out float until) && nowSec < until;

        /// <summary>
        /// 부탁 장면 1건 — 연출(말풍선·마주보기·지연 응수)은 대화 통로 재사용, 판정은 대상의
        /// TryGiveRequest. 결과별 관계 델타 + 사유 로그 (ADR-M8-5: 무언 거절 0).
        /// </summary>
        private void Ask(RequestSO r, VillagerAgent requester, VillagerAgent target, float nowSec)
        {
            requester.ShowTransient(Pick(r.AskLines));
            requester.FaceForChat(target.transform.position, _agentCfg.ChatPauseSec);
            target.FaceForChat(requester.transform.position, _agentCfg.ChatPauseSec);

            VillagerAgent.RequestResult verdict = target.TryGiveRequest(r, requester.AgentId);
            target.ShowTransientDelayed(Pick(ReplyLinesFor(r, target, verdict)), _agentCfg.ReplyDelaySec);

            _requesterCooldownUntil[requester.AgentId] = nowSec + _world.RequestCooldownSec;
            // 부탁 장면도 대화다 — 참여자 둘을 대화 쿨다운에 등록해 직후의 잔소리 연쇄를 막는다
            // (2026-07-17 Play 피드백: 부탁 거절 직후 같은 목수에게 '왜 일 안 해' 발화 → 장면 겹침)
            _chatter?.RecordChat(requester.AgentId, target.AgentId, nowSec);

            if (verdict == VillagerAgent.RequestResult.Accepted)
            {
                _relationship.AddAffinity(requester.AgentId, target.AgentId, r.AcceptDelta,
                                          $"{r.DisplayName} 수락");
                // 선불 성격 (ADR-보상2): 수락 즉시 지급 — 판정(가용성 검사)과 같은 틱이라 안전.
                // 선불 완료 부탁은 보고 장면에서 지급·떼먹기 판정 없음 (prepaid — 이중 지급 차단)
                bool prepaid = target.Personality != null && target.Personality.DemandsRewardUpfront
                               && r.RewardCostAmount > 0 && _worldModel != null
                               && _worldModel.TrySpendStock(r.RewardCostSlot, r.RewardCostAmount);
                if (prepaid)
                {
                    target.ApplyNeedEffect(SlotId.MySatiety, EffectOp.Add, r.RewardSatietyGain);
                    Debug.Log($"[Request] 선불 — {requester.AgentId}→{target.AgentId}: " +
                              $"{r.RewardCostSlot} -{r.RewardCostAmount}, 포만 +{r.RewardSatietyGain}");
                }
                _inFlight[target.AgentId] = (r, requester.AgentId, prepaid);
            }
            else
            {
                _relationship.AddAffinity(requester.AgentId, target.AgentId, r.RefusedDelta,
                                          $"{r.DisplayName} 거절");
            }
            Debug.Log($"[Request] {requester.AgentId}→{target.AgentId}: {r.DisplayName} — {Kr(verdict)}");
        }

        /// <summary>결과별 응수 대사 — 배고픔·피로는 성격 거부 대사 재사용 (이중 기입 금지, 명세 §4).</summary>
        private string[] ReplyLinesFor(RequestSO r, VillagerAgent target, VillagerAgent.RequestResult verdict)
        {
            switch (verdict)
            {
                case VillagerAgent.RequestResult.Accepted:           return r.AcceptLines;
                case VillagerAgent.RequestResult.RefusedBusy:        return r.RefuseBusyLines;
                case VillagerAgent.RequestResult.RefusedLowAffinity: return r.RefuseLowAffinityLines;
                case VillagerAgent.RequestResult.RefusedNoReward:    return r.RefuseNoRewardLines;
                case VillagerAgent.RequestResult.RefusedInjured:     return _agentCfg.InjuredLines; // M10-A — 부상 대사 재사용 (이중 기입 금지)
                case VillagerAgent.RequestResult.RefusedHungry:
                    return FirstNonEmpty(target.Personality != null ? target.Personality.RefuseHungryLines : null,
                                         _agentCfg.RefuseHungryLines);
                default:
                    return FirstNonEmpty(target.Personality != null ? target.Personality.RefuseTiredLines : null,
                                         _agentCfg.RefuseTiredLines);
            }
        }

        private static string Kr(VillagerAgent.RequestResult v)
        {
            switch (v)
            {
                case VillagerAgent.RequestResult.Accepted:           return "수락";
                case VillagerAgent.RequestResult.RefusedBusy:        return "거절(바쁨)";
                case VillagerAgent.RequestResult.RefusedHungry:      return "거절(배고픔)";
                case VillagerAgent.RequestResult.RefusedTired:       return "거절(피로)";
                case VillagerAgent.RequestResult.RefusedNoReward:    return "거절(선불)";
                case VillagerAgent.RequestResult.RefusedInjured:     return "거절(부상)"; // M10-A
                default:                                             return "거절(원한)";
            }
        }

        /// <summary>
        /// 부탁 완수 통지 — 수락자의 _request 완수 지점(VillagerAgent)이 호출. 쌍방 신뢰 델타 +
        /// 소유 배정(GrantOwnership이면 부탁자에게 최근접 무주 건물)은 즉시, 완수 대사·보상은
        /// 보고 심부름(의뢰인 곁까지 걸어가 알림 — 2026-07-17 사용자 요청)으로 미룬다.
        /// </summary>
        public void NotifyFulfilled(string builderId)
        {
            if (!_inFlight.TryGetValue(builderId, out (RequestSO so, string requesterId, bool prepaid) rec)) return;
            _inFlight.Remove(builderId);

            _relationship.AddAffinity(rec.requesterId, builderId, rec.so.FulfillDelta, $"{rec.so.DisplayName} 완수");
            _relationship.AddAffinity(builderId, rec.requesterId, rec.so.FulfillDelta, $"{rec.so.DisplayName} 완수");

            if (rec.so.GrantOwnership)
            {
                VillagerAgent owner = FindAgent(rec.requesterId);
                int fx = owner != null ? owner.TileX : 0;
                int fy = owner != null ? owner.TileY : 0;
                int best = int.MaxValue;
                Vector2Int bestTile = default;
                bool found = false;
                foreach (Vector2Int t in _construction.BuiltTilesOf(rec.so.OwnershipSlot))
                {
                    if (_ownership.IsOwned(t)) continue;
                    int dx = t.x - fx, dy = t.y - fy;
                    int d = dx * dx + dy * dy;
                    if (d < best) { best = d; bestTile = t; found = true; }
                }
                if (found) _ownership.Assign(bestTile, rec.so.OwnershipSlot, rec.requesterId, "부탁 완수");
                else Debug.LogWarning($"[Request] {rec.so.DisplayName} 완수 — 무주 건물이 없어 배정 생략");
            }
            Debug.Log($"[Request] {builderId}: {rec.so.DisplayName} 완수 — 의뢰인 {rec.requesterId}");

            // 보상 빚 기록 (조각 Y) — 목수는 쫓아가지 않는다. 의뢰인과 자연스럽게 마주치는 순간
            // TickRewardSettlement가 정산 장면(PlayReport)을 재생한다. 타임아웃 소실 없음.
            // 의뢰인이 이미 없으면(이탈) 정산 상대가 없으니 완수 혼잣말만 하고 빚은 기록하지 않는다.
            VillagerAgent builder = FindAgent(builderId);
            VillagerAgent requester = FindAgent(rec.requesterId);
            if (builder == null || requester == null)
            {
                builder?.ShowTransient(Pick(rec.so.FulfillLines));
                return;
            }
            _pendingReports[builderId] = (rec.so, rec.requesterId, rec.prepaid);
        }

        /// <summary>
        /// 보상 정산 장면 (조각 Y) — TickRewardSettlement가 목수·의뢰인이 마주쳤을 때 호출.
        /// 수행자 완수 대사 → 의뢰인 보상/감사 대사 (지연 응수). 보상 = 공용 스톡 차감 + 수행자
        /// 포만 (밥 대접 — 개인 인벤토리 없음). 재고 부족이면 지급 생략 + 감사 대사 (로그로 구분).
        /// </summary>
        public void PlayReport(VillagerAgent builder)
        {
            if (builder == null) return;
            if (!_pendingReports.TryGetValue(builder.AgentId,
                    out (RequestSO so, string requesterId, bool prepaid) rec))
                return; // 정산할 빚 없음
            _pendingReports.Remove(builder.AgentId);

            // 목수 완수 대사도 지연 경로로 (2026-07-18 버그 수정): 즉시 ShowTransient는 정산
            // 순간(마주침=이동 중단)의 AbortPlan.Clear에 지워져 목수만 침묵하고 의뢰인 감사만
            // 남았다. 의뢰인 응수와 동일하게 clear-후-표시라 그 Clear를 비껴간다. 0f = 곧바로
            // (의뢰인 ReplyDelaySec보다 먼저 = "목수 먼저, 의뢰인 응수" 순서 보존).
            builder.ShowTransientDelayed(Pick(rec.so.FulfillLines), 0f);
            VillagerAgent requester = FindAgent(rec.requesterId);
            if (requester != null)
            {
                builder.FaceForChat(requester.transform.position, _agentCfg.ChatPauseSec);
                requester.FaceForChat(builder.transform.position, _agentCfg.ChatPauseSec);

                // 응수 분기: 보상 없음/선불 완료 → 감사 / 떼먹는 성격+낮은 친밀 → 떼먹기 (ADR-보상1)
                // / 그 외 → 지급 시도 (재고 부족이면 감사)
                if (rec.so.RewardCostAmount <= 0 || rec.prepaid)
                {
                    requester.ShowTransientDelayed(Pick(rec.so.ThanksLines), _agentCfg.ReplyDelaySec);
                }
                else if (ShouldStiffReward(requester.Personality,
                                           _relationship.AffinityOf(rec.requesterId, builder.AgentId)))
                {
                    requester.ShowTransientDelayed(
                        Pick(FirstNonEmpty(requester.Personality.StiffRewardLines, _agentCfg.StiffRewardLines)),
                        _agentCfg.ReplyDelaySec);
                    _relationship.AddAffinity(builder.AgentId, rec.requesterId, rec.so.StiffedDelta,
                                              $"{rec.so.DisplayName} 보상 떼먹음");
                    Debug.Log($"[Request] 보상 떼먹음 — {rec.requesterId} " +
                              $"(수행자 {builder.AgentId} 관계 {rec.so.StiffedDelta})");
                }
                else if (_worldModel != null
                         && _worldModel.TrySpendStock(rec.so.RewardCostSlot, rec.so.RewardCostAmount))
                {
                    builder.ApplyNeedEffect(SlotId.MySatiety, EffectOp.Add, rec.so.RewardSatietyGain);
                    requester.ShowTransientDelayed(Pick(FirstNonEmpty(rec.so.RewardLines, rec.so.ThanksLines)),
                                                   _agentCfg.ReplyDelaySec);
                    Debug.Log($"[Request] 보상 — {rec.requesterId}→{builder.AgentId}: " +
                              $"{rec.so.RewardCostSlot} -{rec.so.RewardCostAmount}, 포만 +{rec.so.RewardSatietyGain}");
                }
                else
                {
                    requester.ShowTransientDelayed(Pick(rec.so.ThanksLines), _agentCfg.ReplyDelaySec);
                    Debug.Log($"[Request] 보상 생략 — {rec.so.RewardCostSlot} 재고 부족 (감사 대사만)");
                }
                _chatter?.RecordChat(builder.AgentId, requester.AgentId, Time.time); // 보고 장면도 대화
            }
            Debug.Log($"[Request] {builder.AgentId}: {rec.so.DisplayName} 보상 정산 → {rec.requesterId}");
        }

        /// <summary>
        /// 보상 빚 정산 (조각 Y) — 목수와 의뢰인이 정산 반경 안에서 마주치면 그 자리에서 지급.
        /// 쫓아가지 않으므로 추격 수렴 문제가 없고, 못 만나도 빚은 남는다(소실 없음). 한 틱 1건
        /// (장면 겹침 방지, 매칭 규약과 동일). 대화 쿨다운 중인 쌍은 다음 기회로 미룬다.
        /// </summary>
        private void TickRewardSettlement(float nowSec)
        {
            if (_pendingReports.Count == 0) return;
            foreach (KeyValuePair<string, (RequestSO so, string requesterId, bool prepaid)> kv in _pendingReports)
            {
                VillagerAgent builder   = FindAgent(kv.Key);
                VillagerAgent requester = FindAgent(kv.Value.requesterId);
                if (builder == null || requester == null) continue;        // 이탈은 ReleaseBy가 정리
                if (builder.State == AgentState.Dead || requester.State == AgentState.Dead) continue;
                // 장면 연쇄 방지 — 둘 중 하나라도 대화 쿨다운이면 다음 기회에 정산
                if (_chatter != null && (_chatter.IsCoolingDown(kv.Key, nowSec)
                                         || _chatter.IsCoolingDown(requester.AgentId, nowSec))) continue;

                int dist = Mathf.Abs(builder.TileX - requester.TileX)
                         + Mathf.Abs(builder.TileY - requester.TileY);
                if (dist > _world.RewardSettleRadiusTiles) continue;

                PlayReport(builder); // 정산 장면 + 지급 — _pendingReports에서 제거됨
                return;              // 한 틱 1건 (직후 return이라 딕셔너리 수정 후 순회 없음 → 안전)
            }
        }

        /// <summary>이탈 정리 — 그 주민이 수락자든 의뢰인이든 진행·보고 기록 제거 (유령 유예 방지).</summary>
        public void ReleaseBy(string agentId)
        {
            _keysToRemove.Clear();
            foreach (KeyValuePair<string, (RequestSO so, string requesterId, bool prepaid)> kv in _inFlight)
                if (kv.Key == agentId || kv.Value.requesterId == agentId)
                    _keysToRemove.Add(kv.Key);
            foreach (string key in _keysToRemove)
                _inFlight.Remove(key);

            _keysToRemove.Clear();
            foreach (KeyValuePair<string, (RequestSO so, string requesterId, bool prepaid)> kv in _pendingReports)
                if (kv.Key == agentId || kv.Value.requesterId == agentId)
                    _keysToRemove.Add(kv.Key);
            foreach (string key in _keysToRemove)
                _pendingReports.Remove(key); // 목수든 의뢰인이든 이탈 → 정산 불가, 빚 소멸
        }

        private VillagerAgent FindAgent(string agentId)
        {
            for (int i = 0; i < _agents.Count; i++)
            {
                VillagerAgent a = _agents[i];
                if (a != null && a.AgentId == agentId) return a;
            }
            return null;
        }

        private static string[] FirstNonEmpty(string[] preferred, string[] fallback)
            => preferred != null && preferred.Length > 0 ? preferred : fallback;

        private static string Pick(string[] lines)
            => lines == null || lines.Length == 0 ? null : lines[Random.Range(0, lines.Length)];

        private readonly List<string> _keysToRemove = new List<string>(4);
    }
}
