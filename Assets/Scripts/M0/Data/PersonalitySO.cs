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

        [Header("위협 감지 (M10-D — 도망 반응의 성격 축. 기본값 1 = 중립)")]
        [Tooltip("위협 감지 반경 배율 — ThreatSO.DangerRadiusTiles에 곱해진다. <1 = 늦게 알아채고 " +
                 "늦게 도망 (고집쟁이 0.6 — 밭을 지키다 물리는 서사), >1 = 예민 (새침 1.2). " +
                 "도망 속도가 아니라 감지 반경이다 (명세 M10-D ⚠️② — 속도는 전원 동일, 부상만 감속).")]
        public float FleeRadiusMult = 1f;

        [Header("택지 취향 (M11-F/J/K — 내 집이 설 자리. 기본 0 = 중립·최근접)")]
        [Tooltip("마을 앵커(기지)로부터의 **선호 거리 비율**(0~1, M11-K). 0 = 중립(최근접). " +
                 "실효 거리 = 이 비율 × 실효 마을 반경 → 맵이 커지면 함께 멀어진다(절대값 아님). " +
                 "온순 0.1(이웃 곁)·방랑벽 0.95(외딴집)처럼 성격마다 다른 동심원. 대역(±1) 안에서 " +
                 "주민 신원 기반으로 흩어져 앉는다(결정적, 랜덤 아님). 직업 값과 합산.")]
        public float HomePreferredDist;

        [Header("보상 태도 (M8 보완 — 주민 간 보상. 기본값 = 중립: 안 떼먹고 후불 수용)")]
        [Tooltip("의뢰인일 때: 수행자에 대한 친밀도가 이 미만이면 보상을 떼먹는다 (ADR-보상1 — " +
                 "결정적, 랜덤 금지). 기본 -100 = 친밀도 하한이라 판정 성립 불가 (절대 안 떼먹음). " +
                 "주의: 수락 +5·완수 +15 델타가 판정보다 먼저 쌓인다 — 거래 자체가 +20이므로 " +
                 "문턱은 그 위에 잡을 것. 제안치: 고집쟁이 25(초면 떼먹음), 새침이 30(꽤 친해야 지급).")]
        public int SkipRewardBelowAffinity = -100;

        [Tooltip("수행자일 때: 보상 선불을 요구한다 (ADR-보상2 — 수락 시 즉시 지급, 재고 없으면 " +
                 "거절). 보상 없는 부탁은 항상 거절 (ADR-보상4). 선불 = 확실하지만 깐깐.")]
        public bool DemandsRewardUpfront;

        [Header("대사 (비면 기본 대사 사용 — 중립 경로)")]
        [Tooltip("보상 떼먹기 대사 오버라이드 (비면 AgentConfig 기본) — 예: 보상?! 하.. 내가 생각 해볼게~")]
        public string[] StiffRewardLines;

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
