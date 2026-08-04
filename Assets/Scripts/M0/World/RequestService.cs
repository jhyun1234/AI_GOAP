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
        // 즉시 교환(M16-W5) 배선 — 정적 Instance 참조 금지 (명세 ⚠️), 생성자 주입 (Chatter 자리 패턴).
        // null 허용 = 교환 부탁이 없는 구성에서 중립 (기존 게이트·구형 생성 호환).
        private readonly ChronicleService _chronicle;   // Traded 사건 기록
        private readonly System.Func<float> _gameDay;   // 연대기 사건의 게임일 (nowSec은 실시간이라 부적합)

        private float _nextScanAt;
        // 의뢰인 개인 쿨다운 — 거절당하면 한동안 다시 조르지 않는다 (수락 시에도 기록 — 중복 방지 이중화)
        private readonly Dictionary<string, float> _requesterCooldownUntil = new Dictionary<string, float>(16);
        // 진행 중 부탁 (수락~완수): 수락자 ID → (부탁, 의뢰인 ID, 선불 완료 여부).
        // (M19-W5: M18의 실가격 필드는 화폐와 함께 철거 — 실물은 RewardCostAmount 액면이 진실)
        // 세이브 대상 (ADR-M0-10)
        private readonly Dictionary<string, (RequestSO so, string requesterId, bool prepaid)> _inFlight =
            new Dictionary<string, (RequestSO, string, bool)>(4);
        // 보상 미정산 빚 (조각 Y, 2026-07-18): 수행자 ID → (부탁, 의뢰인 ID, 선불 여부).
        // 일 완수 순간 기록되고, 수행자와 의뢰인이 자연스럽게 마주치면(TickRewardSettlement) 정산·소멸한다.
        // 쫓아가지 않으므로 타임아웃 소실 없음 — 정리는 이탈(ReleaseBy)뿐.
        // 세이브 대상으로 승격 (ADR-M11-10, M11-H): 빚은 연출이 아니라 실제 식량이다 —
        // 로드 후 소멸시키면 지급이 소실된다.
        private readonly Dictionary<string, (RequestSO so, string requesterId, bool prepaid)> _pendingReports =
            new Dictionary<string, (RequestSO, string, bool)>(4);
        private readonly List<VillagerAgent> _scratch = new List<VillagerAgent>(16);

        public RequestService(WorldConfigSO world, AgentConfigSO agentCfg, RelationshipService relationship,
                              OwnershipService ownership, ConstructionService construction,
                              IReadOnlyList<VillagerAgent> agents, ChatterService chatter = null,
                              ChronicleService chronicle = null,
                              System.Func<float> gameDay = null)
        {
            _world = world;
            _agentCfg = agentCfg;
            _relationship = relationship;
            _ownership = ownership;
            _construction = construction;
            _agents = agents;
            _chatter = chatter;
            _chronicle = chronicle;
            _gameDay = gameDay;
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
        /// 진행 중 부탁의 의뢰인 본인 (M11-F 택지) — 수행자가 "이 집의 주인이 될 사람"을 찾는 창구.
        /// 부탁 중이 아니거나 의뢰인이 이미 이탈했으면 false (호출처는 본인으로 폴백).
        /// </summary>
        /// <summary>
        /// 자가 소유 배정 가능 여부 (순수 — 게이트 M12-T12, 2026-07-26 회귀 수정).
        /// 판정 기준은 **"누가 무엇을 하는 중인가"가 아니라 "지금 짓는 이 건물이 그 부탁의
        /// 소유 배정 대상인가"**다.
        ///
        /// 舊 판정은 "부탁 수행 중이면 자가 배정 안 함"이라 **부탁이 진행되는 동안 짓는 모든**
        /// 소유 건물이 주인을 잃었다. 집 부탁을 수락한 목수가 자기 모닥불을 지으면 소유가
        /// 배정되지 않아 MyHasCampfire가 0으로 남고, Goal_BuildCampfire(50)가 부탁받은 집(36)을
        /// 계속 이겨 **집 주변이 만원이 될 때까지 모닥불을 반복 건축**했다 (2026-07-26 Play 관측).
        /// </summary>
        public static bool ShouldSelfAssign(RequestSO inFlight, SlotId countSlot)
            => inFlight == null || !inFlight.GrantOwnership || inFlight.OwnershipSlot != countSlot;

        /// <summary>이 주민이 지금 짓는 건물을 자기 것으로 가져도 되는가 (위 순수 판정의 창구).</summary>
        public bool ShouldSelfAssignFor(string workerId, SlotId countSlot)
            => ShouldSelfAssign(
                _inFlight.TryGetValue(workerId, out (RequestSO so, string requesterId, bool prepaid) rec)
                    ? rec.so : null,
                countSlot);

        public bool TryGetRequester(string workerId, out VillagerAgent requester)
        {
            requester = null;
            if (!_inFlight.TryGetValue(workerId, out (RequestSO so, string requesterId, bool prepaid) rec))
                return false;
            requester = FindAgent(rec.requesterId);
            return requester != null;
        }

        /// <summary>
        /// 떼먹기 판정 (순수 — 게이트 대상, ADR-보상1): 의뢰인 성격의 친밀 문턱 미만이면 떼먹음.
        /// 랜덤 금지 — 관계 표기로 예측 가능. p null·기본값 -100 = 판정 성립 불가 (절대 안 떼먹음).
        /// </summary>
        public static bool ShouldStiffReward(PersonalitySO p, int affinityTowardBuilder)
            => p != null && affinityTowardBuilder < p.SkipRewardBelowAffinity;

        /// <summary>
        /// 의뢰인 기질 문턱 (M12-G, 순수 — 게이트 M12-T7). 스톡 조건(RequesterConditions)을 이미
        /// 통과한 의뢰인에게 "그럴 사람인가"를 한 번 더 묻는다.
        ///
        ///   성립 = 성향 조건 충족 **OR** 경험 우회 조건 충족
        ///
        /// 유도는 TraitVector.Meets 한 곳에서만 한다 (ADR-M12-5 — 여기서 Traits를 직접 순회하면
        /// 두 번째 유도 경로다). 성향 조건이 비면 항상 true = 현행 동작(중립 불변식).
        /// 성격 미배정(p == null)도 중립 — 벡터가 전 축 0인 것과 같다.
        /// </summary>
        /// <summary>호환 진입점 (게이트·구형 호출 전용 — 벡터 = 성격 원본, 편차 없음. ⚠️W3-⑤).</summary>
        public static bool RequesterQualifies(RequestSO r, PersonalitySO p, in WorldSnapshot snap)
            => RequesterQualifies(r, p, p != null ? p.Traits : null, snap);

        public static bool RequesterQualifies(RequestSO r, PersonalitySO p, TraitValue[] traits,
                                              in WorldSnapshot snap)
        {
            if (r == null) return false;
            // 벡터는 인자(M14-W3 개체 편차 포함) — "기질이 되는가"도 개체마다 조금씩 다르다
            if (TraitVector.Meets(traits, r.RequesterTraits)) return true;
            // 기질이 모자라도 경험이 있으면 성립 — "죽다 살아난 자가 집을 원한다".
            // 우회 조건이 비면 AllHold가 true를 돌려주므로, 여기서 무조건 성립하지 않도록
            // 빈 배열을 "우회 없음"으로 먼저 거른다.
            return r.TraitBypassConditions != null && r.TraitBypassConditions.Length > 0
                && GoalSelector.AllHold(r.TraitBypassConditions, snap);
        }

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
                    if (!RequesterQualifies(r, requester.Personality, requester.MyTraits, snap)) continue; // M12-G 기질 문턱

                    foreach (VillagerAgent target in _scratch)
                    {
                        if (target == requester) continue;
                        if (_chatter != null && _chatter.IsCoolingDown(target.AgentId, nowSec)) continue;
                        if (r.TargetJob != null && target.Job != r.TargetJob) continue; // 참조 매핑 (ADR-M0-1)
                        // 대상 상태 조건 (M16-W5 — 예: 판매자 식량 여유). 비면 무조건 (중립)
                        if (r.TargetConditions != null && r.TargetConditions.Length > 0
                            && !GoalSelector.AllHold(r.TargetConditions, target.BuildSnapshot())) continue;
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
            // 대사는 에셋 원문 그대로 (M19-W1) — 실물 보상은 액수가 대사에 이미 있다
            // ("곡식 다섯 알"). 호가 병기(M18-W4)는 화폐와 함께 철거됐다.
            requester.ShowTransient(Pick(r.AskLines));
            requester.FaceForChat(target.transform.position, _agentCfg.ChatPauseSec);
            target.FaceForChat(requester.transform.position, _agentCfg.ChatPauseSec);

            // 즉시 나눔 (M19-W3, 舊 즉시 교환 M16-W5 계승) — 수락 순간 실물 일방 이전.
            // 완수·빚·정산을 타지 않는다 (주는 쪽이 지금 실물을 갖고 있으므로 떼먹기·연기가
            // 구조적으로 없다).
            if (r.TradeGiveAmount > 0)
            {
                AskShare(r, requester, target, nowSec);
                return;
            }

            // 선불 가용성 판정도 개인 잔고로 (M11-H) — 전역 스톡 조회는 폐지됐다.
            // 가난한 의뢰인은 선불 성격 수행자에게 RefusedNoReward (기존 대사 재사용).
            // ⚠️ 수령 공간을 여기 넣지 말 것 (2026-07-23 중간 리뷰 ② 관측으로 제거): 선불 성격의
            // 요구는 "저 사람이 값을 치를 능력이 있는가"이지 "내 손이 지금 비어 있는가"가 아니다.
            // 공간을 조건에 넣으면 평상시 생식 4를 채우는(Goal_GatherFood 목표) 수행자가 상한 8에서
            // 5를 못 받아 **구조적으로 항상 거절**했다. 공간이 없으면 후불로 수락하고 연기 정산이
            // 처리한다 (아래 prepaid는 실제 이전 성공 여부만 본다).
            bool canPayNow = UpfrontAvailable(r.RewardCostAmount,
                                             requester.CanPayReward(r.RewardCostSlot, r.RewardCostAmount));
            VillagerAgent.RequestResult verdict = target.TryGiveRequest(r, requester.AgentId, canPayNow);
            target.ShowTransientDelayed(Pick(ReplyLinesFor(r, target, verdict)), _agentCfg.ReplyDelaySec);

            _requesterCooldownUntil[requester.AgentId] = nowSec + _world.RequestCooldownSec;
            // 부탁 장면도 대화다 — 참여자 둘을 대화 쿨다운에 등록해 직후의 잔소리 연쇄를 막는다
            // (2026-07-17 Play 피드백: 부탁 거절 직후 같은 목수에게 '왜 일 안 해' 발화 → 장면 겹침)
            _chatter?.RecordChat(requester.AgentId, target.AgentId, nowSec);

            if (verdict == VillagerAgent.RequestResult.Accepted)
            {
                _relationship.AddAffinity(requester.AgentId, target.AgentId, r.AcceptDelta,
                                          $"{r.DisplayName} 수락");
                // 선불 (ADR-보상2): 수락 즉시 지급 — 판정(가용성 검사)과 같은 틱이라 안전.
                // 선불 완료 부탁은 보고 장면에서 지급·떼먹기 판정 없음 (prepaid — 이중 지급 차단).
                // 두 경로: ①부탁 자체가 선불(M17-R4, AlwaysUpfront — 집처럼 지불 능력이 성립
                // 조건으로 보장된 거래) ②수행자 성격이 선불을 요구(기존). 실패하면 조용히 후불로.
                bool wantsUpfront = r.AlwaysUpfront
                                    || (_agentCfg != null
                                        && _agentCfg.DemandsUpfront(target.Personality, target.MyTraits));
                bool prepaid = wantsUpfront
                               && canPayNow
                               && requester.TransferTo(target, r.RewardCostSlot, r.RewardCostAmount);
                if (prepaid)
                    Debug.Log($"[Request] 선불 — {requester.AgentId}→{target.AgentId}: " +
                              $"{r.RewardCostSlot} {r.RewardCostAmount}개 이전");
                // (M19-W5: HomePaid 연대기 기록 지점 철거 — 화폐 지불 사건 소멸, enum은 휴면)
                _inFlight[target.AgentId] = (r, requester.AgentId, prepaid);
            }
            else
            {
                _relationship.AddAffinity(requester.AgentId, target.AgentId, r.RefusedDelta,
                                          $"{r.DisplayName} 거절");
            }
            Debug.Log($"[Request] {requester.AgentId}→{target.AgentId}: {r.DisplayName} — {Kr(verdict)}");
        }

        /// <summary>
        /// 식량 나눔 장면 (M19-W3 — 舊 즉시 교환 M16-W5의 실물 계승). **나눔은 거래가 아니다**
        /// (ADR-M19-4): 실물 일방 이전 + 관계 델타뿐 — 반대급부·교환비를 붙이는 순간 화폐의
        /// 재발명이다. 거부 판정(부상·바쁨·배고픔·원한)은 기존 재사용 — 거절도 장면이다
        /// (ADR-M8-5). 무한 구걸은 의뢰인 쿨다운 + 배고픔 성립 조건이 이미 막는다 (명세 ⚠️W3).
        /// </summary>
        private void AskShare(RequestSO r, VillagerAgent requester, VillagerAgent target, float nowSec)
        {
            _requesterCooldownUntil[requester.AgentId] = nowSec + _world.RequestCooldownSec;
            _chatter?.RecordChat(requester.AgentId, target.AgentId, nowSec);

            // 나눠줄 사람의 거부 판정 재사용 (부상·바쁨·배고픔·원한)
            VillagerAgent.RequestResult verdict = target.TryGiveRequest(r, requester.AgentId,
                upfrontAvailable: true, instantTrade: true);
            target.ShowTransientDelayed(
                verdict == VillagerAgent.RequestResult.Accepted
                    ? Pick(r.AcceptLines)
                    : Pick(ReplyLinesFor(r, target, verdict)),
                _agentCfg.ReplyDelaySec);
            if (verdict != VillagerAgent.RequestResult.Accepted)
            {
                _relationship.AddAffinity(requester.AgentId, target.AgentId, r.RefusedDelta,
                                          $"{r.DisplayName} 거절");
                Debug.Log($"[Share] {requester.AgentId}→{target.AgentId}: {r.DisplayName} — {Kr(verdict)}");
                return;
            }

            // 원자성 (ADR-M0-8) — 재고·공간 선검사 후 실물 일방 이전
            if (!target.CanPayReward(r.TradeGiveSlot, r.TradeGiveAmount)
                || !requester.HasRoomFor(r.TradeGiveSlot, r.TradeGiveAmount)
                || !target.TransferTo(requester, r.TradeGiveSlot, r.TradeGiveAmount))
            {
                Debug.Log($"[Share] 불발 — 재고/공간 부족 ({target.AgentId}→{requester.AgentId}, " +
                          $"{r.TradeGiveSlot} {r.TradeGiveAmount}개)");
                return;
            }

            // 은혜는 받은 쪽이 기억한다 — 관계 델타가 보답의 자리다 (ADR-M19-4)
            _relationship.AddAffinity(requester.AgentId, target.AgentId, r.AcceptDelta,
                                      $"{r.DisplayName} 나눔");
            _chronicle?.RecordEvent(requester.AgentId, EventId.FoodShared,
                                    _gameDay != null ? _gameDay() : 0f, target.AgentId, r.TradeGiveAmount);
            Debug.Log($"[Share] {target.AgentId}→{requester.AgentId}: {r.TradeGiveSlot} " +
                      $"{r.TradeGiveAmount}개 나눔");
        }

        // (M19-W4: 실가격 산식 TradePrice·AcceptancePrice는 물가와 함께 철거 — ADR-M19-1)

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
            // (M19-W5: 화폐 빚 표식(M17-W7 SetDebt)은 지갑과 함께 철거 — 실물 미정산은
            //  _pendingReports 기록 자체가 빚이고, 마주침 정산(조각 Y)이 갚음이다)
        }

        /// <summary>
        /// 선불 가용성 (순수 — 게이트 M11-T7, ADR-보상2). **인자가 둘뿐인 것이 계약이다**:
        /// 수행자의 수령 공간은 여기 들어오지 않는다 (2026-07-23 중간 리뷰 ② 관측 — 공간을
        /// 조건에 넣었더니 평상시 생식을 지닌 수행자가 상한에 걸려 구조적으로 항상 거절했다).
        /// 선불 성격이 묻는 것은 의뢰인의 지급 능력뿐이다.
        /// </summary>
        public static bool UpfrontAvailable(int rewardAmount, bool requesterCanPay)
            => rewardAmount > 0 && requesterCanPay;

        /// <summary>정산 결과 (M11-H) — Defer는 빚을 남기고 장면 자체를 열지 않는다.</summary>
        public enum Settlement { Thanks, Stiff, Pay, Defer }

        /// <summary>
        /// 정산 분기 (순수 — 게이트 M11-T7, ADR-M11-4). 판정 순서가 곧 설계다:
        /// 보상 없음/선불 완료 → 감사 / **떼먹기(성격 경로, 잔고 무관 — 파산 ≠ 떼먹기)** /
        /// 낼 수 있고 받을 공간 있으면 지급 / 아니면 연기 (빚 유지).
        /// </summary>
        public static Settlement ResolveSettlement(bool noReward, bool prepaid, bool stiff,
                                                   bool canPay, bool hasRoom)
        {
            if (noReward || prepaid) return Settlement.Thanks;
            if (stiff) return Settlement.Stiff;
            return canPay && hasRoom ? Settlement.Pay : Settlement.Defer;
        }

        /// <summary>장면 전 분기 판정 — 순수 규칙에 현재 잔고·관계를 먹인다 (M11-H 원형).</summary>
        private Settlement Resolve(RequestSO so, bool prepaid, VillagerAgent requester, VillagerAgent builder)
            => ResolveSettlement(
                so.RewardCostAmount <= 0, prepaid,
                ShouldStiffReward(requester.Personality,
                                  _relationship.AffinityOf(requester.AgentId, builder.AgentId)),
                requester.CanPayReward(so.RewardCostSlot, so.RewardCostAmount),
                builder.HasRoomFor(so.RewardCostSlot, so.RewardCostAmount));

        // 연기 로그 1회용 (같은 빚이 매 틱 로그를 도배하지 않도록) — 정산·이탈 시 해제
        private readonly HashSet<string> _deferLogged = new HashSet<string>();

        /// <summary>
        /// 보상 정산 장면 (조각 Y) — TickRewardSettlement가 목수·의뢰인이 마주쳤을 때 호출.
        /// 수행자 완수 대사 → 의뢰인 보상/감사 대사 (지연 응수). 보상 = 의뢰인 잔고(몸+집)에서
        /// 수행자 몸으로의 **실제 식량 이전** (M11-H, 결정 10 — 포만 직접 지급은 폐지).
        /// 잔고·공간이 모자라면 장면 없이 연기하고 빚을 남긴다 (소실 없음 — 조각 Y 계승).
        /// </summary>
        public void PlayReport(VillagerAgent builder)
        {
            if (builder == null) return;
            if (!_pendingReports.TryGetValue(builder.AgentId,
                    out (RequestSO so, string requesterId, bool prepaid) rec))
                return; // 정산할 빚 없음

            VillagerAgent payer = FindAgent(rec.requesterId);
            Settlement how = payer != null ? Resolve(rec.so, rec.prepaid, payer, builder)
                                           : Settlement.Thanks; // 의뢰인 부재 = 장면만 정리
            if (how == Settlement.Defer)
            {
                // 첫 마주침에는 장면을 연다 (M16-B, Play 관측 2026-08-01: "집을 지어줬는데
                // 아무 말도 없다"). 舊 설계는 전면 침묵이었는데, 보상이 식량 5개에서 50동이
                // 되며 연기가 예외에서 상시 경로가 됐다 — 목수가 빈손으로 돌아서는 장면이
                // 화면에 있어야 플레이어가 "저 집은 빚으로 지어졌다"를 안다.
                // 두 번째부터는 침묵 (장면 스팸 방지 — 舊 설계의 정신은 유지).
                if (_deferLogged.Add(builder.AgentId))
                {
                    Debug.Log($"[Request] 정산 연기 — {rec.requesterId} 잔고/공간 부족 " +
                              $"({rec.so.RewardCostSlot} {rec.so.RewardCostAmount}). 빚 유지");
                    builder.ShowTransientDelayed(Pick(rec.so.FulfillLines), 0f);
                    if (payer != null)
                    {
                        builder.FaceForChat(payer.transform.position, _agentCfg.ChatPauseSec);
                        payer.FaceForChat(builder.transform.position, _agentCfg.ChatPauseSec);
                        payer.ShowTransientDelayed(Pick(FirstNonEmpty(rec.so.DeferLines, _agentCfg.DeferRewardLines)),
                                                   _agentCfg.ReplyDelaySec);
                        _chatter?.RecordChat(builder.AgentId, payer.AgentId, Time.time);
                    }
                }
                return; // 빚 유지 (다음 마주침엔 조용히 재시도)
            }
            _pendingReports.Remove(builder.AgentId);
            _deferLogged.Remove(builder.AgentId);
            // 이 장면으로 채무 관계는 끝난다 — 떼먹힌 원한은 관계 축이 따로 기억한다 (M11-H).

            // 목수 완수 대사도 지연 경로로 (2026-07-18 버그 수정): 즉시 ShowTransient는 정산
            // 순간(마주침=이동 중단)의 AbortPlan.Clear에 지워져 목수만 침묵하고 의뢰인 감사만
            // 남았다. 의뢰인 응수와 동일하게 clear-후-표시라 그 Clear를 비껴간다. 0f = 곧바로
            // (의뢰인 ReplyDelaySec보다 먼저 = "목수 먼저, 의뢰인 응수" 순서 보존).
            builder.ShowTransientDelayed(Pick(rec.so.FulfillLines), 0f);
            VillagerAgent requester = payer; // 분기 판정에 쓴 그 사람 — 재조회하면 판정과 어긋날 수 있다
            if (requester != null)
            {
                builder.FaceForChat(requester.transform.position, _agentCfg.ChatPauseSec);
                requester.FaceForChat(builder.transform.position, _agentCfg.ChatPauseSec);

                // 응수 분기 (M11-H) — 판정은 Resolve가 이미 했다 (장면 전 결정, 연기는 여기 안 온다)
                if (how == Settlement.Stiff)
                {
                    requester.ShowTransientDelayed(
                        Pick(FirstNonEmpty(requester.Personality.StiffRewardLines, _agentCfg.StiffRewardLines)),
                        _agentCfg.ReplyDelaySec);
                    _relationship.AddAffinity(builder.AgentId, rec.requesterId, rec.so.StiffedDelta,
                                              $"{rec.so.DisplayName} 보상 떼먹음");
                    Debug.Log($"[Request] 보상 떼먹음 — {rec.requesterId} " +
                              $"(수행자 {builder.AgentId} 관계 {rec.so.StiffedDelta})");
                }
                else if (how == Settlement.Pay
                         && requester.TransferTo(builder, rec.so.RewardCostSlot, rec.so.RewardCostAmount))
                {
                    requester.ShowTransientDelayed(Pick(FirstNonEmpty(rec.so.RewardLines, rec.so.ThanksLines)),
                                                   _agentCfg.ReplyDelaySec);
                    Debug.Log($"[Request] 보상 — {rec.requesterId}→{builder.AgentId}: " +
                              $"{rec.so.RewardCostSlot} {rec.so.RewardCostAmount}개 이전");
                }
                else
                {
                    // 보상 없는 부탁·선불 완료 = 감사만 (Resolve가 Thanks로 판정한 경로)
                    requester.ShowTransientDelayed(Pick(rec.so.ThanksLines), _agentCfg.ReplyDelaySec);
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
            {
                // (M19-W5: 화폐 빚 표식 SetDebt 청산은 지갑과 함께 철거 — 기록 제거가 곧 소멸)
                _pendingReports.Remove(key); // 목수든 의뢰인이든 이탈 → 정산 불가, 빚 소멸
                _deferLogged.Remove(key);    // 연기 로그 표식도 함께 (M11-H)
            }
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
