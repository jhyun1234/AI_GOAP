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

        [Tooltip("이 조건 전부 만족 시 goal 후보로 발동. 비우면 항상 후보.")]
        public SlotCondition[] TriggerConditions;

        [Tooltip("플래너에 넘길 목표 조건. 이미 전부 만족이면 GoalSelector가 스킵한다. " +
                 "비워 두면 '항상 미달성'으로 취급 (여가 등 달성 개념이 없는 goal — DirectActionPool과 함께 사용).")]
        public SlotCondition[] GoalConditions;

        [Tooltip("설정 시 플래너를 생략하고 이 풀에서 랜덤 1개를 즉시 실행 (M1-A ADR-M1-3: 여가 전용 특례 — " +
                 "다른 goal에 남용 금지, 플래너 우회 뒷문이 된다).")]
        public ActionSO[] DirectActionPool;

        [Tooltip("true면 GoalConditions.Value를 '수신 시점 현재값 + Value(증분)'로 해석 (명령 goal 전용, M1-C). " +
                 "'창고를 30까지'가 아니라 '지금보다 10 더'가 플레이어의 의도 — 수신 시 절대값 사본으로 고정된다.")]
        public bool RelativeToCurrent;

        [Tooltip("P0 생존 goal 전용 (ADR-M2-5): 플랜 실패 후 재시도 쿨다운을 면제한다 — " +
                 "굶는데 여가를 가는 상황 원천 차단. 다른 goal에 켜면 공회전 방지가 무력화된다.")]
        public bool SkipFailureCooldown;

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
            if (TriggerConditions == null || GoalConditions == null) return;

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
            switch (cond.Op)
            {
                case CompareOp.Equal:          return value == cond.Value;
                case CompareOp.GreaterOrEqual: return value >= cond.Value;
                case CompareOp.LessOrEqual:    return value <= cond.Value;
                default:                       return false;
            }
        }
    }
}
