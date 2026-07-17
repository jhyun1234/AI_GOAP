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
        private readonly IReadOnlyList<VillagerAgent> _agents; // SimulationLoop 소유 리스트 (살아있는 참조)

        private float _nextScanAt;
        // 의뢰인 개인 쿨다운 — 거절당하면 한동안 다시 조르지 않는다 (수락 시에도 기록 — 중복 방지 이중화)
        private readonly Dictionary<string, float> _requesterCooldownUntil = new Dictionary<string, float>(16);
        // 진행 중 부탁 (수락~완수): 수락자 ID → (부탁, 의뢰인 ID). 세이브 대상 (ADR-M0-10)
        private readonly Dictionary<string, (RequestSO so, string requesterId)> _inFlight =
            new Dictionary<string, (RequestSO, string)>(4);
        private readonly List<VillagerAgent> _scratch = new List<VillagerAgent>(16);

        public RequestService(WorldConfigSO world, AgentConfigSO agentCfg, RelationshipService relationship,
                              OwnershipService ownership, ConstructionService construction,
                              IReadOnlyList<VillagerAgent> agents, ChatterService chatter = null)
        {
            _world = world;
            _agentCfg = agentCfg;
            _relationship = relationship;
            _ownership = ownership;
            _construction = construction;
            _agents = agents;
            _chatter = chatter;
        }

        /// <summary>이 슬롯을 배정하는 부탁이 진행 중인가 — 클레임 패스 유예 질의 (부탁자 우선권, M8-C ⚠️②).</summary>
        public bool AnyInFlightGranting(SlotId slot)
        {
            foreach (KeyValuePair<string, (RequestSO so, string requesterId)> kv in _inFlight)
                if (kv.Value.so.GrantOwnership && kv.Value.so.OwnershipSlot == slot) return true;
            return false;
        }

        /// <summary>agentId가 의뢰인으로 걸어 둔 진행 중 부탁이 있는가 — 중복 부탁 방지.</summary>
        public bool HasInFlightFrom(string requesterId)
        {
            foreach (KeyValuePair<string, (RequestSO so, string requesterId)> kv in _inFlight)
                if (kv.Value.requesterId == requesterId) return true;
            return false;
        }

        /// <summary>
        /// 주기 스캔 (SimulationLoop 틱에서 호출) — 셔플 순회로 성립 (의뢰인, 대상) 1건만.
        /// 의뢰인 조건은 스냅샷 대역 판정 (GoalSelector.AllHold 재사용 — goal 조건과 같은 언어).
        /// </summary>
        public void Tick(float nowSec, IReadOnlyList<VillagerAgent> agents)
        {
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
                _inFlight[target.AgentId] = (r, requester.AgentId);
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
                default:                                             return "거절(원한)";
            }
        }

        /// <summary>
        /// 부탁 완수 통지 — 수락자의 _request 완수 지점(VillagerAgent)이 호출. 쌍방 신뢰 델타 +
        /// 소유 배정(GrantOwnership이면 부탁자에게 최근접 무주 건물) + 완수 대사.
        /// </summary>
        public void NotifyFulfilled(string builderId)
        {
            if (!_inFlight.TryGetValue(builderId, out (RequestSO so, string requesterId) rec)) return;
            _inFlight.Remove(builderId);

            _relationship.AddAffinity(rec.requesterId, builderId, rec.so.FulfillDelta, $"{rec.so.DisplayName} 완수");
            _relationship.AddAffinity(builderId, rec.requesterId, rec.so.FulfillDelta, $"{rec.so.DisplayName} 완수");

            if (rec.so.GrantOwnership)
            {
                VillagerAgent requester = FindAgent(rec.requesterId);
                int fx = requester != null ? requester.TileX : 0;
                int fy = requester != null ? requester.TileY : 0;
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
                else Debug.LogWarning($"[Request] {rec.so.DisplayName} 완수 — 무주 건물이 없어 배정 생략 " +
                                      $"(클레임 패스가 선배정했는지 확인)");
            }

            FindAgent(builderId)?.ShowTransient(Pick(rec.so.FulfillLines));
            Debug.Log($"[Request] {builderId}: {rec.so.DisplayName} 완수 — 의뢰인 {rec.requesterId}");
        }

        /// <summary>이탈 정리 — 그 주민이 수락자든 의뢰인이든 진행 기록 제거 (유령 유예 방지).</summary>
        public void ReleaseBy(string agentId)
        {
            _keysToRemove.Clear();
            foreach (KeyValuePair<string, (RequestSO so, string requesterId)> kv in _inFlight)
                if (kv.Key == agentId || kv.Value.requesterId == agentId)
                    _keysToRemove.Add(kv.Key);
            foreach (string key in _keysToRemove)
                _inFlight.Remove(key);
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
