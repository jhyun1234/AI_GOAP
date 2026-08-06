using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// M0 목표 정의. 발동 임계값(TriggerConditions)과 플래너 목표(GoalConditions)를
    /// 한 에셋의 두 필드로 강제한다 (ADR-M0-7) —
    /// 舊 ADR-7 위반(목표치·임계값 역전 → alreadySatisfied 무한 루프)의 구조적 차단.
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Goal", fileName = "Goal")]
    public sealed class GoalSO : ScriptableObject
    {
        [Tooltip("한국어 표시명 (예: 배고픔 해결)")]
        public string DisplayName;

        [Tooltip("높을수록 먼저 평가된다.")]
        public int Priority;

        [Header("성향 보정 (M12-B — ①우선순위. 이 goal을 어느 기질이 얼마나 원하는가)")]
        [Tooltip("실효 우선순위 = Priority + round(bias × PriorityScale). 비면 성향 무관 = 중립.\n" +
                 "⚠️ 이것은 **순위**에만 개입한다 — TriggerConditions에는 절대 쓰지 않는다 (ADR-M12-2): " +
                 "성향이 발동 임계값을 옮기면 OnValidate의 정적 검사(아래)가 런타임 이동을 못 봐 " +
                 "'달성 즉시 재발동' 무한 루프가 된다.\n" +
                 "⚠️ 먹는 행동(P0 식사·간식)에는 비워 둔다 — 몸값 불가침 (ADR-M12-4 ①). 순서는 급박성이 정하므로 " +
                 "즉사급 위협(피신)만 P0 위에 설 수 있다 (ADR-M12-4 ③ · 승인 목록 ADR-M12-8).")]
        public TraitWeight[] TraitWeights;

        [Tooltip("이 goal의 성향 진폭. |boost| ≤ 30 (ADR-M12-7) — P0 생존 goal(우선순위 100)이 " +
                 "성향으로 밀리면 굶으면서 일하러 간다. 제안치 20.\n" +
                 "진폭이 P0 경계를 넘게 잡히는 것은 **의도된 설계일 때만** 허용한다 — 피신(105, 진폭 20, " +
                 "겁 가중치 0.8)은 겁이 최저인 고집쟁이(겁 -60)를 95로 내려 '무모한 자는 배고프면 위험을 " +
                 "무시한다'를 만든다 (ADR-M12-4 ②의 인접 칸 넘나들기).\n" +
                 "⚠️ 진폭은 boost의 상한이지 boost 자체가 아니다 — 실제 boost = round(bias × PriorityScale)이고 " +
                 "bias에는 TraitWeights의 Weight가 곱해져 있다. 축 극단(±100)에서도 Weight 0.8이면 ±16이지 " +
                 "±20이 아니다 (커밋 976fae8 본문과 이 주석이 Weight를 빠뜨려 한때 85/125로 잘못 적혀 있었다).")]
        [Range(0f, 30f)]
        public float PriorityScale = 20f;

        /// <summary>
        /// ①우선순위 유도 (M12-B, 순수 — 게이트 M12-T3). 가중치가 비면 0 = 중립.
        /// 반올림은 순위 비교가 정수라서다 (GoalSelector.Consider).
        /// </summary>
        public int TraitBoost(TraitValue[] traits)
            => TraitWeights == null || TraitWeights.Length == 0
                ? 0
                : Mathf.RoundToInt(TraitVector.Bias(traits, TraitWeights) * PriorityScale);

        [Tooltip("이 조건 전부 만족 시 goal 후보로 발동. 비우면 항상 후보.")]
        public SlotCondition[] TriggerConditions;

        [Tooltip("경험 우회 (M20-W11 — M12-G '경험 > 기질'의 자가 건축 이전). 조건 전부 성립 시 " +
                 "이 goal의 **음수 성향 보정(기질 페널티)을 지운다** (양수 보너스는 불변).\n" +
                 "예: 굶어 죽을 뻔한(MyWasStarved) 게으름뱅이는 집 goal의 게으름 페널티가 사라져 " +
                 "스스로 집을 원하게 된다 — '한 번 구해줬더니 사람이 됐다'.\n" +
                 "비우면 우회 없음 = 현행 (중립 불변식). 트리거 문법이므로 슬롯 비교 허용 " +
                 "(적용 지점 = GoalSelector.Consider 한 곳 — 플래너 잡은 이 필드를 모른다).")]
        public SlotCondition[] ExperienceOverrideWhen;

        [Tooltip("플래너에 넘길 목표 조건. 이미 전부 만족이면 GoalSelector가 스킵한다. " +
                 "비워 두면 '항상 미달성'으로 취급 (여가 등 달성 개념이 없는 goal — DirectActionPool과 함께 사용).")]
        public SlotCondition[] GoalConditions;

        [Tooltip("설정 시 플래너를 생략하고 이 풀에서 랜덤 1개를 즉시 실행한다 (M1-A).\n" +
                 "⚠️ 자격 (셋 다 충족해야 쓴다 — 2026-07-26 개정: 舊 문언 '여가 전용 특례'는 ADR-M1-3이 " +
                 "실제로 말하지 않는 주장이었고, 이미 여가 외 3개가 쓰고 있어 문언이 낡아 있었다):\n" +
                 "  ⓐ 달성 개념이 없다 — GoalConditions로 '끝'을 적을 수 없는 goal.\n" +
                 "     두 종류가 있다: ①끝이 아예 없는 것(여가·일과) ②끝이 **슬롯 밖에** 있는 것\n" +
                 "     (심부름·접근 — '누구 곁에 도착'은 위치라서 슬롯 조건으로 못 쓴다)\n" +
                 "  ⓑ 풀 안의 어느 것을 골라도 같은 뜻이다 — 플래너가 고를 이유가 없다\n" +
                 "  ⓒ 전역 스톡을 건드리지 않는다 — 플래너를 우회하므로 전제·효과 검증이 없다\n" +
                 "셋 중 하나라도 아니면 플래너에 맡긴다.\n" +
                 "현재 사용: 여가·완공보고·일과 2종·**접근 1종(목수 찾아가기, M17-R3)**.\n" +
                 "게이트 M12_T4가 실제 사용 개수를 세어 이 문언과 어긋나면 실패한다.")]
        public ActionSO[] DirectActionPool;

        [Tooltip("true면 GoalConditions.Value를 '수신 시점 현재값 + Value(증분)'로 해석 (M1-C 명령 → M9-H 씬 " +
                 "goal → 생존 goal로 확장). '창고를 30까지'가 아니라 '지금보다 10 더'가 의도일 때. " +
                 "⚠️ 생존 goal에 특히 중요: 절대 목표(예: 포만 70)는 식량이 모자라면 **도달 불가라 플래너가 " +
                 "아무 계획도 못 세워 식량을 쥐고도 굶는다**. 증분(+15=한 끼)은 1개만 있어도 항상 성립한다 " +
                 "(2026-07-24 Play 실측: 생식 2개 보유 중 아사).")]
        public bool RelativeToCurrent;

        [Tooltip("P0 생존 goal 전용 (ADR-M2-5): 플랜 실패 후 재시도 쿨다운을 면제한다 — " +
                 "굶는데 여가를 가는 상황 원천 차단. 다른 goal에 켜면 공회전 방지가 무력화된다.")]
        public bool SkipFailureCooldown;

        [Tooltip("이 goal은 해가 없어도 정상 (ADR-M6-2 게임건강 예외). true면 NoSolution을 경고 대신 " +
                 "정보로 로그한다. 기본 false = 버그 탐지(경고) 유지.\n" +
                 "① 식량 goal: 겨울 봉쇄 + 저장분 0 = 버그가 아니라 굶주림.\n" +
                 "② 명령 goal: 플레이어가 불가능한 걸 시킨 것 — 실패는 주민의 거부 대사·HUD로 이미 " +
                 "보이므로 경고가 중복이다.\n" +
                 "⚠️ 남용 금지 — 켜는 순간 그 goal의 진짜 결함도 조용해진다. '해가 없는 게 정상인 상황'을 " +
                 "설명할 수 있을 때만.")]
        public bool MayHaveNoSolution;

        [Tooltip("동시 수행 인원 제한 (0=무제한, ADR-M3-4). 건설·비축 goal의 초과 달성 방지용. " +
                 "P0 생존 goal·명령에 설정 금지 — 생존과 플레이어 의지는 인원 제한 대상이 아니다.")]
        public int MaxWorkers;

        [Tooltip("부상 중에도 후보로 남는 goal (M10-A — 생존 goal만 true: P0 식사·수면, 도망, 간식). " +
                 "false(기본)면 부상 주민의 후보에서 제외 — 절뚝이며 노동하러 가는 것을 막는다. " +
                 "부상 None이면 이 필드는 판정에 개입하지 않는다 (중립 불변식).")]
        public bool AllowedWhenInjured;

        [Tooltip("이 직업만 후보로 삼는 goal (M11-I, ADR-M11-6 개정 예외). null(기본)이면 직업 무관 " +
                 "(중립 불변식 — 기존 Select와 동일). **치료 goal + 목수 자가 건축 전용** — 그 밖의 " +
                 "goal에 설정하면 직업 붕괴 스위치가 되살아난다(ADR-M5-4). 세 번째 용도 = 규칙 재검토 신호.")]
        public JobSO RequiredJob;

        /// <summary>
        /// ADR-M0-7 정합 검사: goal 달성 상태가 trigger를 다시 발동시키면 무한 루프다.
        /// 같은 슬롯에 대해 "목표값이 발동 조건을 만족"하면 에셋 저장 시점에 에러를 띄운다.
        /// </summary>
        private void OnValidate()
        {
            // ADR-M12-7: ①유도값 상한 — 진폭이 크면 성향이 P0 생존 순서를 뒤집는다.
            if (PriorityScale > 30f)
                Debug.LogError($"[GoalSO] {name}: PriorityScale({PriorityScale})이 30을 넘습니다 — " +
                               "성향이 P0 생존 goal을 밀어낼 수 있습니다 (ADR-M12-7).", this);

            // ADR-M18-1: GoalConditions는 관리(달성 스킵·명령/부탁 완수)와 잡이 함께 읽는다 —
            // 슬롯 비교·새 연산자를 켜면 잡은 상수로 목표를 세워 완수 판정과 갈린다.
            // ⚠️ RelativeToCurrent 조기 반환보다 앞에 있어야 한다 (상대 goal도 금지 대상).
            if (SlotCondition.UsesTriggerOnlyGrammar(GoalConditions))
                Debug.LogError($"[GoalSO] {name}: GoalConditions에 슬롯 비교/새 연산자 금지 — " +
                               "플래너 목표와 완수 판정이 갈립니다 (ADR-M18-1). 트리거로 옮기세요.",
                               this);

            if (TriggerConditions == null || GoalConditions == null) return;
            // 상대 goal은 검사 제외 — GoalConditions.Value가 절대 목표가 아니라 증분이라
            // 발동 조건과 직접 비교하는 것이 범주 오류다 (해석은 ResolveRelativeGoal이 수행).
            // 루프가 안 서는 이유는 증분의 **부호가 발동 방향의 반대**이기 때문이다:
            //   +증분(배고픔) → 실효 목표가 현재보다 높다 → 달성하면 트리거(≤N)에서 멀어진다
            //   −증분(피로)   → 실효 목표가 현재보다 낮다 → 달성하면 트리거(≥N)에서 멀어진다
            // (舊 주석은 "항상 현재값보다 높아"라고 +증분만 상정했다 — 피로가 첫 −증분 goal이다)
            if (RelativeToCurrent) return;

            foreach (SlotCondition g in GoalConditions)
            {
                foreach (SlotCondition t in TriggerConditions)
                {
                    if (g.Slot != t.Slot) continue;
                    if (Satisfies(g.Value, t))
                    {
                        Debug.LogError($"[GoalSO] {name}: 슬롯 {g.Slot}의 목표값 {g.Value}이(가) " +
                                       $"발동 조건({t.Op} {t.Value})을 만족합니다 — 달성 즉시 재발동 무한 루프 (ADR-M0-7).",
                                       this);
                    }
                }
            }
        }

        private static bool Satisfies(int value, SlotCondition cond)
        {
            // M18 — 슬롯 비교 트리거는 우변이 런타임 값이라 정적 판정 불가. ADR-M0-7 루프
            // 검사에서 제외한다 (발동 판정은 트리거가 전담 — RelativeToCurrent 면제와 같은 논리).
            if (cond.CompareToSlot) return false;
            switch (cond.Op)
            {
                case CompareOp.Equal:          return value == cond.Value;
                case CompareOp.GreaterOrEqual: return value >= cond.Value;
                case CompareOp.LessOrEqual:    return value <= cond.Value;
                case CompareOp.Less:           return value <  cond.Value; // M18 — 상수 트리거도 루프 검사 유지
                case CompareOp.Greater:        return value >  cond.Value; // M18
                default:                       return false;
            }
        }
    }
}
