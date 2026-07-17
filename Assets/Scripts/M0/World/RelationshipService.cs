using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 주민 쌍의 방향성 친밀도 (M8-A) — 관계 쓰기의 유일한 지점 (ADR-M8-1).
    /// 원천 2개뿐: ①ChatterService 발화 이벤트 구독 ②부탁 결과(M8-D). 다른 코드의 직접 쓰기 금지.
    /// 원한은 방향이 있다 (잔소리 들은 쪽이 더 미워한다) — 방향 사전, 단짝만 상호 판정.
    /// 세이브 대상 (ADR-M0-10): _affinity 사전이 저장 상태의 전부다.
    /// 에이전트=주민 전제 없음 — ID 문자열로만 결합 (후반 확장 인지 규칙).
    /// </summary>
    public sealed class RelationshipService
    {
        private const int CLAMP = 100; // 알고리즘 상수 (±100) — 문턱·델타는 전부 에셋 (ADR-M8-6)

        private readonly Dictionary<(string from, string to), int> _affinity =
            new Dictionary<(string, string), int>(32);

        /// <summary>from이 to를 보는 친밀도. 미기록 0 (중립).</summary>
        public int AffinityOf(string from, string to)
            => _affinity.TryGetValue((from, to), out int v) ? v : 0;

        /// <summary>
        /// 친밀도 변화의 유일한 쓰기 지점 (ADR-M8-1). delta 0은 기록 자체를 만들지 않는다
        /// (중립 불변식 — 델타 없는 대화는 M7과 완전 동일, 사전 크기 불변).
        /// </summary>
        public void AddAffinity(string from, string to, int delta, string reason)
        {
            if (delta == 0 || string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to) || from == to)
                return;
            int next = Mathf.Clamp(AffinityOf(from, to) + delta, -CLAMP, CLAMP);
            _affinity[(from, to)] = next;
            Debug.Log($"[Relation] {from}→{to} {(delta > 0 ? "+" : "")}{delta} = {next} ({reason})");
        }

        /// <summary>
        /// 대화 1건의 관계 반영 (M8-A) — OnChatted 구독 핸들러의 본체 (게이트 M8-T1 대상).
        /// 배선(SimulationLoop)은 이 메서드 호출 1줄이 전부다.
        /// </summary>
        public void ApplyChat(ChatterSO c, string speakerId, string targetId)
        {
            if (c == null) return;
            AddAffinity(speakerId, targetId, c.SpeakerToTargetDelta, c.DisplayName);
            AddAffinity(targetId, speakerId, c.TargetToSpeakerDelta, c.DisplayName);
        }

        /// <summary>원한 판정 — from이 to를 문턱 미만으로 본다 (방향성).</summary>
        public bool IsGrudge(string from, string to, int grudgeThreshold)
            => AffinityOf(from, to) < grudgeThreshold;

        /// <summary>단짝 판정 — 쌍방 모두 문턱 이상 (상호성. 한쪽 짝사랑은 단짝이 아니다).</summary>
        public bool IsBuddy(string a, string b, int buddyThreshold)
            => AffinityOf(a, b) >= buddyThreshold && AffinityOf(b, a) >= buddyThreshold;

        /// <summary>
        /// 정보줄용 극단 1명 (M8-B) — buddy=true면 self 기준 최고 친밀 상대(문턱 이상+상호),
        /// false면 최저(문턱 미만인 원한 상대). 없으면 false. 동률은 먼저 기록된 쪽.
        /// </summary>
        public bool TryGetExtreme(string self, bool buddy, int threshold, out string otherId)
        {
            otherId = null;
            int best = 0;
            bool found = false;
            foreach (KeyValuePair<(string from, string to), int> kv in _affinity)
            {
                if (kv.Key.from != self) continue;
                if (buddy)
                {
                    if (!IsBuddy(self, kv.Key.to, threshold)) continue;
                    if (!found || kv.Value > best) { best = kv.Value; otherId = kv.Key.to; found = true; }
                }
                else
                {
                    if (kv.Value >= threshold) continue; // 문턱 미만만 원한
                    if (!found || kv.Value < best) { best = kv.Value; otherId = kv.Key.to; found = true; }
                }
            }
            return found;
        }

        /// <summary>이탈 주민 정리 — 그 주민이 얽힌 기록 전부 제거 (양방향).</summary>
        public void ReleaseBy(string agentId)
        {
            _keysToRemove.Clear();
            foreach ((string from, string to) key in _affinity.Keys)
                if (key.from == agentId || key.to == agentId)
                    _keysToRemove.Add(key);
            foreach ((string, string) key in _keysToRemove)
                _affinity.Remove(key);
        }

        private readonly List<(string, string)> _keysToRemove = new List<(string, string)>(8);
    }
}
