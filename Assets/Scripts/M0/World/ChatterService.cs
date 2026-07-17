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

        private float _nextScanAt;
        // 개인 쿨다운 (화자·상대 공용) — ID 문자열 결합 (후반 확장 인지 규칙: 에이전트=주민 전제 금지)
        private readonly Dictionary<string, float> _cooldownUntil = new Dictionary<string, float>(16);
        private readonly List<VillagerAgent> _scratch = new List<VillagerAgent>(16); // 셔플 순회 버퍼

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
            speaker.ShowTransient(Pick(c.SpeakLines));
            target.ShowTransientDelayed(Pick(c.RepliesFor(target.Personality)), _agentCfg.ReplyDelaySec);
            // 멈춰서 마주보기 (2026-07-17 사용자 결정) — 표현 계층만, ChatPauseSec 0이면 무효
            speaker.FaceForChat(target.transform.position, _agentCfg.ChatPauseSec);
            target.FaceForChat(speaker.transform.position, _agentCfg.ChatPauseSec);
            RecordChat(speaker.AgentId, target.AgentId, nowSec);
            Debug.Log($"[Chatter] {speaker.AgentId}→{target.AgentId}: {c.DisplayName}"); // S3 카운트 근거
            OnChatted?.Invoke(c, speaker, target); // M8-A — 관계 축이 구독 (ADR-M8-1)
        }

        private static string Pick(string[] lines)
            => lines == null || lines.Length == 0 ? null : lines[Random.Range(0, lines.Length)];
    }
}
