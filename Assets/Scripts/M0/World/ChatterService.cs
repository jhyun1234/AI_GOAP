using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 주민 상호대화 (M7-C) — 표현 전용. 시뮬레이션 상태에 쓰기 0:
    /// WorldModel·FarmService 등 시뮬 참조 필드를 갖지 않는다 (ADR-M7-1 — 리뷰 항목 M7-S5).
    /// 짝 찾기는 이 서비스 하나가 주기 틱으로 수행하고, 한 주기 최대 1쌍만 발화시킨다
    /// (ADR-M7-3 — 에이전트 개별 스캔 금지, 대화는 희소해야 장면이 된다).
    /// 표시는 기존 말풍선 통로(ShowTransient) 재사용 (ADR-M7-4).
    /// </summary>
    public sealed class ChatterService
    {
        /// <summary>발화 알림 (상황, 화자, 상대) — M8-A. 이 서비스는 구독자를 모른다 (ADR-M8-1:
        /// 관계 축적은 RelationshipService가 구독으로 수행 — 여기에 시뮬 참조가 생기면 반려).</summary>
        public event System.Action<ChatterSO, VillagerAgent, VillagerAgent> OnChatted;

        private readonly WorldConfigSO _world;
        private readonly AgentConfigSO _agentCfg;

        /// <summary>N명 대화 장면당 FaceForChat 정지 최대 인원 (ADR-M9-7 — 화자 포함). 나머지 청중은
        /// 걸으며 듣는다: 길목 통째 점유(PathBlocked)를 막는 알고리즘 상수.</summary>
        public const int MAX_CHAT_PAUSERS = 3;

        private float _nextScanAt;
        // 개인 쿨다운 (화자·상대 공용) — ID 문자열 결합 (후반 확장 인지 규칙: 에이전트=주민 전제 금지)
        private readonly Dictionary<string, float> _cooldownUntil = new Dictionary<string, float>(16);
        private readonly List<VillagerAgent> _scratch = new List<VillagerAgent>(16); // 셔플 순회 버퍼
        private readonly List<int> _listenerIdx = new List<int>(8);        // 추가 청중 인덱스 버퍼 (M9-E)
        private readonly List<VillagerAgent> _responders = new List<VillagerAgent>(8); // 상대+청중 (M9-E)

        public ChatterService(WorldConfigSO world, AgentConfigSO agentCfg)
        {
            _world = world;
            _agentCfg = agentCfg;
        }

        /// <summary>
        /// 상황 성립 판정 (순수 — 게이트 M7-T1). 판정 대역은 goal 에셋의 원본 Priority만 —
        /// EffectivePriority(개인 bias)는 남의 속마음이라 쓰지 않는다 (ADR-M7-2).
        /// 화자는 goal이 있어야(일하는 중), 상대는 goal 없음(Idle)도 '노는 중'에 포함.
        /// </summary>
        public static bool Matches(ChatterSO c, GoalSO speakerGoal, GoalSO targetGoal)
        {
            if (c == null || speakerGoal == null) return false;
            if (c.SpeakerGoal != null && speakerGoal != c.SpeakerGoal) return false;
            if (speakerGoal.Priority < c.MinSpeakerGoalPriority) return false;
            if (targetGoal != null && targetGoal.Priority > c.MaxTargetGoalPriority) return false;
            return true;
        }

        /// <summary>개인 쿨다운 판정 (게이트 M7-T2) — 발화 직후 같은 화자/상대 재선정 불가.</summary>
        public bool IsCoolingDown(string agentId, float nowSec)
            => _cooldownUntil.TryGetValue(agentId, out float until) && nowSec < until;

        /// <summary>발화 기록 — 화자·상대 모두 공용 쿨다운 (게이트 M7-T2).</summary>
        public void RecordChat(string speakerId, string targetId, float nowSec)
        {
            _cooldownUntil[speakerId] = nowSec + _world.ChatterCooldownSec;
            _cooldownUntil[targetId] = nowSec + _world.ChatterCooldownSec;
        }

        /// <summary>
        /// 주기 스캔 (SimulationLoop 틱에서 호출). IntervalSec마다 성립 쌍을 찾아 최대 1쌍 발화 —
        /// 상대는 걸음을 멈추지 않는다 (표현 전용 — 지나가며 말해도 된다).
        /// </summary>
        public void Tick(float nowSec, IReadOnlyList<VillagerAgent> agents)
        {
            if (_world.Chatters == null || _world.Chatters.Length == 0) return; // 중립 — 대화 없음
            if (nowSec < _nextScanAt) return;
            _nextScanAt = nowSec + _world.ChatterIntervalSec;

            // 셔플 순회 — 항상 같은 주민이 화자가 되는 편향 방지
            _scratch.Clear();
            for (int i = 0; i < agents.Count; i++)
            {
                VillagerAgent a = agents[i];
                if (a == null || a.State == AgentState.Dead) continue; // 이탈 직전 주민 제외 (⚠️④)
                // 명령 수행 중 제외 (리뷰① 2026-08-08 — 촌장이 시킨 일이 잡담보다 먼저다).
                // 화자·대상 양쪽 다: 대상으로 뽑혀도 대화 정지가 걸려 행군이 실제로 멈춘다
                // (실측: 「싸워라」 받고 위협에게 가던 주민이 요리사와 "밥 좀 해달라" 장면으로 정지).
                if (a.CurrentOrder != null) continue;
                _scratch.Add(a);
            }
            for (int i = _scratch.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (_scratch[i], _scratch[j]) = (_scratch[j], _scratch[i]);
            }

            foreach (VillagerAgent speaker in _scratch)
            {
                if (IsCoolingDown(speaker.AgentId, nowSec)) continue;
                foreach (VillagerAgent target in _scratch)
                {
                    if (target == speaker || IsCoolingDown(target.AgentId, nowSec)) continue;
                    int dist = Mathf.Abs(speaker.TileX - target.TileX) + Mathf.Abs(speaker.TileY - target.TileY);
                    foreach (ChatterSO c in _world.Chatters)
                    {
                        if (c == null || dist > c.RadiusTiles) continue;
                        if (!Matches(c, speaker.CurrentGoal, target.CurrentGoal)) continue;

                        // 성립 쌍 존재 — 주기당 확률 통과 시에만 발화 (희소성 노브)
                        if (Random.value >= _world.ChatterChance) return;
                        Fire(c, speaker, target, nowSec);
                        return; // 한 주기 최대 1쌍 (ADR-M7-3)
                    }
                }
            }
        }

        private void Fire(ChatterSO c, VillagerAgent speaker, VillagerAgent target, float nowSec)
        {
            // 응답자 = 상대(1×) + 반경 내 추가 청중 (M9-E). MaxExtraListeners 0이면 상대뿐 = 기존 1:1.
            _responders.Clear();
            _responders.Add(target);
            if (c.MaxExtraListeners > 0)
            {
                // 자격 = 상대와 동일 대역(Matches) + 반경 내 + 쿨다운·화자·상대 제외 (_scratch는 Dead 제외됨)
                CollectEligible(_scratch.Count, c.MaxExtraListeners, i =>
                {
                    VillagerAgent cand = _scratch[i];
                    if (cand == speaker || cand == target || IsCoolingDown(cand.AgentId, nowSec)) return false;
                    int d = Mathf.Abs(speaker.TileX - cand.TileX) + Mathf.Abs(speaker.TileY - cand.TileY);
                    if (d > c.RadiusTiles) return false;
                    return Matches(c, speaker.CurrentGoal, cand.CurrentGoal);
                }, _listenerIdx);
                for (int k = 0; k < _listenerIdx.Count; k++) _responders.Add(_scratch[_listenerIdx[k]]);
            }
            Deliver(c, speaker, _responders, nowSec);
        }

        /// <summary>
        /// 이벤트 구동 장면 공용 진입 (M9-E 회의 연출) — Matches 검사 없음: 성립은 호출자가 보장한다
        /// (⚠️①: 회의는 goal 대역이 성립하지 않는 완공 직후에 발화한다. 남용 방어는 "호출처=이벤트
        /// 구독 배선뿐" 리뷰로). 발화·릴레이·정지·쿨다운은 Fire와 동일 경로(Deliver). listeners에서
        /// Dead·화자·중복은 제외한다.
        /// </summary>
        public void FireScene(ChatterSO c, VillagerAgent speaker,
                              IReadOnlyList<VillagerAgent> listeners, float nowSec)
        {
            if (c == null || speaker == null || listeners == null) return;
            _responders.Clear();
            for (int i = 0; i < listeners.Count; i++)
            {
                VillagerAgent l = listeners[i];
                if (l == null || l == speaker || l.State == AgentState.Dead || _responders.Contains(l)) continue;
                _responders.Add(l);
            }
            if (_responders.Count == 0) return; // 청중 없으면 장면 없음 (혼잣말 방지)
            Deliver(c, speaker, _responders, nowSec);
        }

        /// <summary>
        /// 발화·릴레이·정지·쿨다운의 단일 경로 (Fire·FireScene 공용). 화자가 외치고 응답자가 한 명씩
        /// 릴레이 지연(1×·2×·3×…)으로 응수한다. 마주보기 정지는 앞에서부터 MAX_CHAT_PAUSERS명뿐
        /// (ADR-M9-7), 정지 시간은 장면 길이(응답자 수)에 비례한다. 관계·쿨다운은 참여자 전원.
        /// </summary>
        private void Deliver(ChatterSO c, VillagerAgent speaker, List<VillagerAgent> responders, float nowSec)
        {
            // 연출 배속 (M9-E 후속) — 1이면 아래 전부 기존 값 그대로 (중립 불변식)
            float tempo = c.SceneTempoMult > 0f ? c.SceneTempoMult : 1f;
            float gapSec = _agentCfg.ReplyDelaySec * tempo;   // 릴레이 간격
            float holdSec = _agentCfg.TransientLineSec * tempo; // 말풍선 노출

            speaker.ShowTransient(Pick(c.SpeakLines), holdSec);
            int pausers = PauserCount(responders.Count);                 // 화자 포함 총 정지 인원 (≤3)
            float pauseSec = _agentCfg.ChatPauseSec + gapSec * (responders.Count - 1);
            // 화자는 첫 응답자를 바라보며 정지 (응답자 0은 두 진입점 모두에서 이미 배제)
            speaker.FaceForChat(responders[0].transform.position, pauseSec);
            for (int i = 0; i < responders.Count; i++)
            {
                VillagerAgent r = responders[i];
                r.ShowTransientDelayed(Pick(c.RepliesFor(r.Personality)), RelayDelay(gapSec, i), holdSec);
                if (i < pausers - 1) r.FaceForChat(speaker.transform.position, pauseSec); // 앞 청중만 정지
                RecordChat(speaker.AgentId, r.AgentId, nowSec); // 전원 쿨다운 (화자 반복 기록은 idempotent)
                OnChatted?.Invoke(c, speaker, r); // M8-A — 청중별 관계 축적 (1:1이면 1회 = M8 동작)
            }
            string tail = responders.Count == 1 ? responders[0].AgentId : responders.Count + "인";
            Debug.Log($"[Chatter] {speaker.AgentId}→{tail}: {c.DisplayName}"); // S3·S5 카운트 근거
        }

        /// <summary>릴레이 응수 지연 (순수 — 게이트 M9-T4): i번째 응답자 = ReplyDelaySec × (1+i).
        /// 상대(i=0)=1×, 청중(i=1,2…)=2×·3× — 한 명씩 이어져 대화처럼 보인다.</summary>
        public static float RelayDelay(float replyDelaySec, int index) => replyDelaySec * (1 + index);

        /// <summary>마주보기 정지 인원 (순수 — 게이트 M9-T4): 화자 1 + 앞 응답자, MAX_CHAT_PAUSERS 상한.
        /// 응답자 0이면 대화가 아니므로 0. 1:1(응답자 1)이면 2 = 기존 동작(중립 불변식).</summary>
        public static int PauserCount(int responderCount)
            => responderCount <= 0 ? 0 : 1 + Mathf.Min(responderCount, MAX_CHAT_PAUSERS - 1);

        /// <summary>자격 통과 후보를 앞에서부터 max개 수집 (순수 — 게이트 M9-T4). max 0이면 빈 목록
        /// (중립 불변식 — MaxExtraListeners 0 = 청중 미수집). 자격 판정은 호출자가 델리게이트로 캡슐화.</summary>
        public static void CollectEligible(int candidateCount, int max, System.Func<int, bool> eligible, List<int> outIdx)
        {
            outIdx.Clear();
            if (max <= 0 || eligible == null) return;
            for (int i = 0; i < candidateCount && outIdx.Count < max; i++)
                if (eligible(i)) outIdx.Add(i);
        }

        private static string Pick(string[] lines)
            => lines == null || lines.Length == 0 ? null : lines[Random.Range(0, lines.Length)];
    }
}
