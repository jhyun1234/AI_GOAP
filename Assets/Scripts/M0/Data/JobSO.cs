using System;
using AIVillage.Core;
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
    /// 자원별 채집 실행 시간 배율 항목 (M20 — ADR-M20-2. GoalBoost와 같은 {대상, 값} 패턴).
    /// 자원별로 갈라야 나무꾼(나무)과 광부(돌)가 구분된다 — 단일 배율이면 벌목 전문가가
    /// 채석도 빨라져 두 직업이 같은 사람이 된다.
    /// </summary>
    [Serializable]
    public struct GatherDurationMult
    {
        public ResourceType Resource;
        public float Mult;
    }

    /// <summary>
    /// 주민 직업 (M5). 성격(PersonalitySO)과 별개 축 — goal 선점 보정·노동 배율·일과의 단일 출처.
    /// 직업 추가/삭제 = 이 에셋 1개 + 스폰 풀 등록 (코드 0줄, M5-S5).
    /// null 또는 전 필드 중립이면 M4와 goal 선택이 완전히 동일해야 한다 (중립 불변식, M5-S3).
    /// 직업은 강한 선호 + 공용 폴백 — 어떤 goal도 직업 전용으로 잠그지 않는다 (ADR-M5-4).
    /// 소비·휴식 계열 배율 필드는 의도적으로 없다 (ADR-M5-3 — 몸값 불가침 ADR-M12-4 ① 계승).
    /// 대사(MoodLines)는 성격 담당 — 직업에 넣지 않는다 (축 분리).
    ///
    /// 🧭 **헌장 (ADR-M20-1): 직업은 "부재가 느껴지는 장치"다.**
    /// 새 직업은 **"○○ 없는 판은 ~한 판이다"라는 부재 시나리오를 한 줄로 쓸 수 있어야** 만든다.
    /// 못 쓰면 만들지 않는다 — 결과를 못 바꾸는 층은 개념만 어지럽힌다(화폐 철거 M19의 교훈).
    /// 답하는 방식은 둘: ①효율(남보다 빠르다 = Duration 배율) ②라우팅(무엇의 부탁 대상인가).
    /// 탐험가가 휴면인 이유가 이것이다 — 발견은 이동 중 누구나 하므로 부재가 느껴지지 않는다.
    /// 🔴 **표현 조항 (2026-08-05 Play가 드러낸 빠진 절반)**: 부재가 "느껴지려면" 상태가
    /// 달라지는 것만으로 부족하고 **화면에 도달해야** 한다. 효율 배율은 판을 나눠 비교할 수
    /// 없어 실재해도 감지 불가였다 — 새 효율 축을 만들 때는 **읽는 자리도 함께** 만든다
    /// (현재 = 주민 선택 정보줄의 "솜씨 두 배 빨리", SeasonHud.ComposeSkill).
    ///
    /// ⚠️ 효율 배율의 사정거리를 좁게 보지 말 것: BuildDurationMult는 BuildRunner를 타는
    /// **건물 5종 전부**(모닥불·밭·집·내 집·관개수로)에 걸린다. 목수의 부재 시나리오는
    /// "집이 느린 판"이 아니라 **"마을 전체 건설이 두 배 느린 판"**이다 — 모닥불은 조리의,
    /// 밭은 식량의 전제라 파급이 훨씬 넓다 (2026-08-05 실사 정정).
    ///
    /// 📦 **격퇴 계열(사냥꾼·경비)의 선반 자리** — M21+ 전투 시스템과 **한 몸으로만** 신설한다
    /// (전투 판정 없이 직업만 만들면 빈 껍데기다):
    ///   · 전투 효율 → 새 필드 그룹(예: CombatDurationMult) — 여기에 append
    ///   · 사냥·채광 자원 → GatherDurationMults가 이미 수용 (Iron 등 미정의 자원은 자동 1)
    ///   · 격퇴 전용 권한이 필요하면 → 먼저 ADR-M19-3(독점은 치료 1곳뿐)의 개정 사유부터 쓴다
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

        [Tooltip("건설 실행 시간 배율 (M19 — 효율 전문화, ADR-M19-3). 1 = 현행(일반·무직 전부 " +
                 "불변 — 중립 불변식 M5-S3). 목수만 <1 = 솜씨 보너스 (Job_Carpenter 0.5, 제안치).\n" +
                 "⚠️ BuildCostMult(플래너 비용 = 계획 선호)와 다른 축 — 이쪽은 실행층 시간이다. " +
                 "적용 지점은 BuildRunner 하나 — 채집·농사에 곱지 않는다.")]
        public float BuildDurationMult = 1f;

        [Tooltip("밭일(심기·수확) 실행 시간 배율 (M20 — ADR-M20-2). 1 = 현행(무직·일반 불변). " +
                 "농부만 <1 = 솜씨 보너스 (Job_Farmer 0.5, 제안치).\n" +
                 "적용 지점은 FarmRunner 하나 — Routine_TendFields(다른 SO)는 영향권 밖이다.")]
        public float FarmDurationMult = 1f;

        [Tooltip("조리 실행 시간 배율 (M20 — ADR-M20-3). 1 = 현행. 요리사만 <1 (Job_Cook 0.5, 제안치).\n" +
                 "⚠️ 조리는 러너가 소비 계열(ConsumeRunner)이지만 성질은 노동이다. 그래서 " +
                 "ConsumeActionSO.IsCookingWork가 켜진 액션에만 곱한다 — **식사·휴식은 전 직업 " +
                 "영원히 중립**(ADR-M5-3 몸값 불가침). 게이트 M20-T3이 감시.")]
        public float CookDurationMult = 1f;

        [Tooltip("자원별 채집 실행 시간 배율 (M20 — ADR-M20-2). 비면 전 자원 1(중립). " +
                 "나무꾼 = Wood 0.5, 광부 = Stone 0.5 (제안치).\n" +
                 "미정의 자원은 1 — Iron 등 새 자원이 생겨도 기존 직업은 자동으로 중립이다 " +
                 "(격퇴 축 M21+의 사냥·채광 확장이 이 배열에 얹힌다).")]
        public GatherDurationMult[] GatherDurationMults;

        [Tooltip("할 일 없을 때의 일과 goal (개인 사다리 주입, ADR-M5-2). 비면 일과 없음")]
        public GoalSO RoutineGoal;

        [Tooltip("간호 시 부상 회복 배율 (M10-B). 1 = 일반 주민 (중립). 치료사만 3 (제안치).")]
        public float TendRecoveryMult = 1f;

        [Tooltip("완치가 가능한 직업인가 (M11-I 간호 2단계). false = 일반 주민(응급조치=안정화만). " +
                 "true = 치료사 — 완전 회복의 유일한 경로. 없으면 안정화만 받은 부상자는 결국 사망 " +
                 "(결정 15 — crowding 해소 + 사망 착탄). Job_Medic만 true.")]
        public bool CanTreatInjury;

        [Tooltip("택지 선호 거리 가산 (M11-F → M11-J, HomePicker). 0 = 중립. 성격 값과 합산. " +
                 "현재 전 직업 0(보류) — 앵커가 기지 고정이라 '숲 근처' 같은 방향은 표현 불가.")]
        public float HomePreferredDist;

        [Header("성향 선호 (M12-H — 이 직업을 어떤 사람이 원하는가)")]
        [Tooltip("배정 추첨의 가중치 축 (④대상과 같은 유도식 — 후보 점수화). 비면 이 직업은 " +
                 "성향과 무관 = 균등 추첨(중립 불변식).\n" +
                 "⚠️ 편향이지 결정론이 아니다 — 가중치가 아무리 낮아도 0이 되지는 않는다. " +
                 "'게으른데 손재주는 있는 목수'가 나올 수 있어야 사람이 입체적이다.")]
        public TraitWeight[] PreferWeights;

        /// <summary>이 직업의 goal 보정치. 참조 동일성 비교만 — 이름 문자열 비교 금지 (ADR-M0-1 정신).</summary>
        public int BoostFor(GoalSO goal)
        {
            if (GoalBoosts != null)
                foreach (GoalBoost b in GoalBoosts)
                    if (b.Goal == goal) return b.Boost;
            return 0;
        }

        /// <summary>이 직업의 자원별 채집 배율. 미정의 = 1 (중립, M5-S3).
        /// ResourceType은 값 신원이므로 enum 비교다 — 이름 문자열 비교가 아니다.</summary>
        public float GatherDurationMultFor(ResourceType resource)
        {
            if (GatherDurationMults != null)
                foreach (GatherDurationMult g in GatherDurationMults)
                    if (g.Resource == resource) return g.Mult;
            return 1f;
        }
    }
}
