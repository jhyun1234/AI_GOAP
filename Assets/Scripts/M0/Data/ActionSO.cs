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

        /// <summary>플래너·실행이 공유하는 효과 수집.</summary>
        public virtual void CollectEffects(List<SlotEffect> into)
        {
            if (Effects != null) into.AddRange(Effects);
        }
    }
}
