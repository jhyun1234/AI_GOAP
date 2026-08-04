using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// M0 액션의 단일 응집 정의 (ADR-M0-1).
    /// 계획 데이터(전제/효과/비용)·실행 파라미터·말풍선 문구가 이 에셋 하나에만 존재한다.
    /// 플래너(W2 ActionCompiler)와 실행(W4 Runner)이 같은 Effects를 읽으므로
    /// Registry-런타임 수치 이원화(舊 결함 A)가 구조적으로 불가능하다.
    /// 실행 로직과 런타임 상태는 SO에 두지 않는다 — W4 IActionRunner 담당.
    /// </summary>
    public abstract class ActionSO : ScriptableObject
    {
        [Tooltip("말풍선/로그용 한국어 문구 (예: 나무를 베자)")]
        public string DisplayName;

        [Tooltip("말풍선 변주 문구 (M1-D, ADR-M1-5) — 현재 실행 중인 액션에만 적용, 비면 DisplayName. " +
                 "같은 액션 반복 시 문구가 단조로워지는 것을 막는다.")]
        public string[] BubbleLines;

        /// <summary>표시 시점 랜덤 선택 — 표현 전용, 로직·플래너와 무관 (ADR-M1-5).</summary>
        public string PickBubbleLine()
            => BubbleLines == null || BubbleLines.Length == 0
                ? DisplayName
                : BubbleLines[UnityEngine.Random.Range(0, BubbleLines.Length)];

        [Tooltip("플래너 기본 비용. 舊 GOAPActionRegistry BaseCost 이관값.")]
        public float BaseCost = 1f;

        [Tooltip("실행 소요 시간(초). 舊 코드는 도착 즉시 완료였으므로 기본 0 — 표현 연출용 여유 필드.")]
        public float DurationSec = 0f;

        public SlotCondition[] Preconditions;
        public SlotEffect[] Effects;

        /// <summary>플래너에 노출할 전제조건 수집. 서브클래스가 파생 조건을 덧붙일 수 있다 (BuildActionSO).</summary>
        public virtual void CollectPreconditions(List<SlotCondition> into)
        {
            if (Preconditions != null) into.AddRange(Preconditions);
        }

        /// <summary>플래너·실행이 공유하는 효과 수집. **실행이 읽는 것은 이쪽뿐**이다 —
        /// 임금은 여기 들어오지 않는다 (지급은 Mint 한 곳, 이중 지급 차단).</summary>
        public virtual void CollectEffects(List<SlotEffect> into)
        {
            if (Effects != null) into.AddRange(Effects);
        }

        /// <summary>
        /// 실행자 생성 — 실행 1회마다 새 인스턴스 (W4).
        /// abstract이므로 새 계열 SO는 컴파일러가 구현을 강제한다. 문자열 분기 금지의 핵심.
        /// </summary>
        public abstract IActionRunner CreateRunner(VillagerAgent agent);

        /// <summary>ADR-M18-1: Preconditions는 잡이 (int)Op 그대로 삼킨다 — 슬롯 비교·새
        /// 연산자가 들어가면 잡이 모르는 연산이 된다 (조용한 오동작). virtual인 이유:
        /// 서브클래스(BuildActionSO)가 자기 OnValidate로 이 검사를 가리면 안 된다.</summary>
        protected virtual void OnValidate()
        {
            if (SlotCondition.UsesTriggerOnlyGrammar(Preconditions))
                Debug.LogError($"[ActionSO] {name}: Preconditions에 슬롯 비교/새 연산자 금지 — " +
                               "플래너 잡은 상수 3연산만 압니다 (ADR-M18-1).", this);
        }
    }
}
