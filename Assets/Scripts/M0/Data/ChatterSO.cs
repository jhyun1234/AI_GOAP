using System;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 대화 상황 정의 (M7-C) — 조건 대역·대사·성격별 응수가 전부 필드라 새 대화 상황 추가 =
    /// 에셋 1개 (.cs 0, M7-S6). 판정은 goal 에셋의 원본 Priority 대역만 쓴다 (ADR-M7-2 —
    /// 개인 bias는 그 주민의 속마음이라 남이 볼 수 없다). 성격·직업 이름 문자열 분기 금지.
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Chatter", fileName = "Chatter")]
    public sealed class ChatterSO : ScriptableObject
    {
        [Tooltip("상황 표시명 (로그용, 예: 노는 놈 잔소리)")]
        public string DisplayName;

        [Tooltip("화자가 이 goal 수행 중일 때만 성립 (비면 대역 조건만) — 예: 겨울 비축 잔소리")]
        public GoalSO SpeakerGoal;

        [Tooltip("화자 goal 원본 Priority 하한 — 8 = 공용 노동 이상 '일하는 중' (ADR-M7-2)")]
        public int MinSpeakerGoalPriority = 8;

        [Tooltip("상대 goal 원본 Priority 상한 — 2 = 여가·일과 이하 '노는 중'. goal 없음(Idle)도 포함")]
        public int MaxTargetGoalPriority = 2;

        [Tooltip("성립 반경 (타일, 맨해튼 거리)")]
        public int RadiusTiles = 4;

        [Tooltip("추가 청중 상한 (M9-E). 0 = 기존 1:1 (중립 불변식). 청중 성립 조건은 상대와 동일 대역.")]
        public int MaxExtraListeners;

        [Header("관계 영향 (M8-A — 대화가 관계에 남기는 흔적. 0 = 중립, M7 동작과 동일)")]
        [Tooltip("발화 시 화자→상대 친밀도 변화 (잔소리 -2 류). 적용은 RelationshipService 구독 — ADR-M8-1")]
        public int SpeakerToTargetDelta;

        [Tooltip("발화 시 상대→화자 친밀도 변화 (잔소리 들은 쪽이 더 미워한다 -3 류)")]
        public int TargetToSpeakerDelta;

        [Tooltip("발화 대사 (랜덤 선택)")]
        public string[] SpeakLines;

        [Tooltip("기본 응수 — 성격 매칭이 없을 때 (빈칸이 여기로 떨어지는 게 설계)")]
        public string[] DefaultReplyLines;

        [Serializable]
        public struct PersonalityReply
        {
            public PersonalitySO Personality;
            public string[] Lines;
        }

        [Tooltip("성격별 응수 — 매칭 없으면 Default (ADR-M7-2: 이름 분기 금지, 참조 매핑)")]
        public PersonalityReply[] PersonalityReplies;

        /// <summary>성격 응수 조회 — 참조 매칭, 없거나 빈 배열이면 기본 응수 (중립 경로).</summary>
        public string[] RepliesFor(PersonalitySO p)
        {
            if (p != null && PersonalityReplies != null)
                foreach (PersonalityReply r in PersonalityReplies)
                    if (r.Personality == p && r.Lines != null && r.Lines.Length > 0)
                        return r.Lines;
            return DefaultReplyLines;
        }
    }
}
