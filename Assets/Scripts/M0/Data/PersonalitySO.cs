using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 주민 성격 아키타입 (M4). 거부 오프셋·작업 비용 배율·대사 톤의 단일 출처 —
    /// 성격 추가/삭제 = 이 에셋 1개 + 스폰 풀 등록 (코드 0줄, M4-S4).
    /// null 또는 전 필드 중립이면 M3와 동작이 완전히 동일해야 한다 (중립 불변식, ADR-M4-2).
    /// 소비·휴식 계열 배율 필드는 의도적으로 없다 — 굶주림 앞에 성격 없음 (ADR-M4-3).
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Personality", fileName = "Personality")]
    public sealed class PersonalitySO : ScriptableObject
    {
        [Tooltip("표시명 (예: 고집쟁이) — 스폰 로그·추후 UI용")]
        public string DisplayName;

        [Header("명령 거부 (AgentConfig 문턱에 더하는 오프셋 — ADR-M1-2 결정적 판정 유지)")]
        [Tooltip("+면 더 쉽게 배고픔 거부 (문턱 상향)")]
        public float RefuseSatietyOffset;

        [Tooltip("-면 더 쉽게 피로 거부 (문턱 하향)")]
        public float RefuseFatigueOffset;

        [Header("작업 비용 배율 (1=중립, <1=선호. 생존 계열 필드 없음 — ADR-M4-3)")]
        public float GatherCostMult = 1f;
        public float FarmCostMult = 1f;
        public float BuildCostMult = 1f;
        public float ExploreCostMult = 1f;

        [Header("goal 실효 우선순위 보정 (M6 후속 — 직업 GoalBoosts와 합산, ADR-M5-1 주입 패턴)")]
        [Tooltip("성격이 특정 goal을 더/덜 중요하게 여긴다 — 위기 대응이 성격따라 갈리는 축. " +
                 "예: 고집쟁이 겨울비축 -25(내 일이 우선), 농사꾼 +15(선제 비축). " +
                 "동시 돌입(전역 트리거 = 전원 같은 판단)을 흩는 장치이기도 하다.")]
        public GoalBoost[] GoalBoosts;

        /// <summary>이 성격의 goal 보정치 — JobSO.BoostFor와 동일 규약 (참조 동일성, 미등록 0).</summary>
        public int BoostFor(GoalSO goal)
        {
            if (GoalBoosts != null)
                for (int i = 0; i < GoalBoosts.Length; i++)
                    if (GoalBoosts[i].Goal == goal) return GoalBoosts[i].Boost;
            return 0;
        }

        [Header("대사 (비면 기본 대사 사용 — 중립 경로)")]
        [Tooltip("혼잣말 풀 — 액션 문구 대신 MoodLineChance 확률로 표시 (M4-D)")]
        public string[] MoodLines;

        [Tooltip("배고픔 거부 대사 오버라이드 (비면 AgentConfig 기본)")]
        public string[] RefuseHungryLines;

        [Tooltip("피로 거부 대사 오버라이드 (비면 AgentConfig 기본)")]
        public string[] RefuseTiredLines;

        [Range(0f, 1f)]
        [Tooltip("혼잣말 표시 확률 (액션 시작 시점)")]
        public float MoodLineChance = 0.2f;
    }
}
