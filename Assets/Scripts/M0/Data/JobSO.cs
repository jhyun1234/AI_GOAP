using System;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>goal 실효 우선순위 보정 항목 (ADR-M5-1 — 에셋 Priority 원본 불변).</summary>
    [Serializable]
    public struct GoalBoost
    {
        public GoalSO Goal;
        public int Boost;
    }

    /// <summary>
    /// 주민 직업 (M5). 성격(PersonalitySO)과 별개 축 — goal 선점 보정·노동 배율·일과의 단일 출처.
    /// 직업 추가/삭제 = 이 에셋 1개 + 스폰 풀 등록 (코드 0줄, M5-S5).
    /// null 또는 전 필드 중립이면 M4와 goal 선택이 완전히 동일해야 한다 (중립 불변식, M5-S3).
    /// 직업은 강한 선호 + 공용 폴백 — 어떤 goal도 직업 전용으로 잠그지 않는다 (ADR-M5-4).
    /// 소비·휴식 계열 배율 필드는 의도적으로 없다 (ADR-M5-3 — ADR-M4-3 계승).
    /// 대사(MoodLines)는 성격 담당 — 직업에 넣지 않는다 (축 분리).
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Job", fileName = "Job")]
    public sealed class JobSO : ScriptableObject
    {
        [Tooltip("표시명 (예: 농부) — 스폰 로그·추후 UI용")]
        public string DisplayName;

        [Tooltip("이 goal들의 실효 우선순위 보정 (+선점) — 에셋 Priority 원본 불변 (ADR-M5-1)")]
        public GoalBoost[] GoalBoosts;

        [Header("노동 계열 배율 (성격 배율에 곱 결합 — ADR-M5-3. 생존 계열 필드 없음)")]
        public float GatherCostMult = 1f;
        public float FarmCostMult = 1f;
        public float BuildCostMult = 1f;
        public float ExploreCostMult = 1f;

        [Tooltip("할 일 없을 때의 일과 goal (개인 사다리 주입, ADR-M5-2). 비면 일과 없음")]
        public GoalSO RoutineGoal;

        [Tooltip("간호 시 부상 회복 배율 (M10-B). 1 = 일반 주민 (중립 — 간호는 직업 전용이 아니다, " +
                 "결정 11 붕괴 스위치 금지). 치료사만 3 (제안치) — 잃으면 마을이 '느려진다'.")]
        public float TendRecoveryMult = 1f;

        [Tooltip("택지 점수 가산 (M11-F, HomePicker). 0 = 중립(마을 안쪽), 1 초과 = 바깥 선호. " +
                 "성격 값과 합산 — 나무꾼·광부처럼 자원 곁에 살고 싶은 직업의 축.")]
        public float HomeOutskirtsBias;

        /// <summary>이 직업의 goal 보정치. 참조 동일성 비교만 — 이름 문자열 비교 금지 (ADR-M0-1 정신).</summary>
        public int BoostFor(GoalSO goal)
        {
            if (GoalBoosts != null)
                foreach (GoalBoost b in GoalBoosts)
                    if (b.Goal == goal) return b.Boost;
            return 0;
        }
    }
}
