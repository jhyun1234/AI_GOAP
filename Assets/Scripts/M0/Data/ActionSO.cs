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

        [Tooltip("임금 (M16-W2 — 공용 기여 노동만 유급, 0 = 무급 중립). 이 액션 완료 시 촌장이 " +
                 "자동 지급하는 동(銅). 명령·자율 무관 — 지불은 액션이 결정한다 (ADR-M16-2).\n" +
                 "유급 = 공용 스톡 적재·마을 시설 건설만. 자기 채집·자기 밭·자기 집은 0으로 둘 것.\n" +
                 "⚠️ 임금을 Effects(MyMoney Add)로 넣지 않는다 — 효과는 플래너 시야에 들어가 " +
                 "'돈 벌려고 벌목' 플랜이 생긴다 (ADR-M16-5). 임금은 플랜 밖의 부산물이다.")]
        public int WagePay = 0;

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
        /// 플래너 전용 효과 수집 (M16-B, ADR-M16-5 개정) = 에셋 효과 + **임금**(WagePay > 0이면
        /// MyMoney Add). 플래너가 "이 일을 하면 돈이 생긴다"를 알아야 저축 goal이 성립한다.
        ///
        /// 왜 Data 계층인가: SlotEffect 생성은 데이터 계층 전용이다 (게이트 M0-T3 —
        /// "실행 계층에서 효과를 만들면 수치 이원화의 시작"). 컴파일러가 만들면 액션의 계획
        /// 데이터가 에셋 밖에 생겨 ADR-M0-1(단일 응집)도 흔들린다. 수치의 출처는 WagePay 필드
        /// 하나 그대로다 — 이 메서드는 그것을 플래너 언어로 옮길 뿐이다.
        ///
        /// M17-W2: 세율이 붙으면 플래너가 보는 임금이 **세후 실수령**으로 줄어든다. 별도
        /// 페널티 로직 없이 임금노동의 매력이 자급 대비 떨어진다 — 대가는 산식에서 나온다.
        /// 기본값 0(무세)이라 기존 1인자 호출·게이트는 그대로 컴파일된다.
        /// </summary>
        public void CollectPlannerEffects(List<SlotEffect> into, int taxRatePct = 0)
        {
            CollectEffects(into);
            if (WagePay <= 0) return;
            int net = NetWage(WagePay, taxRatePct);
            if (net > 0)
                into.Add(new SlotEffect { Slot = SlotId.MyMoney, Op = EffectOp.Add, Value = net });
        }

        /// <summary>세후 임금 (M17-W2, 순수 — 게이트 M17-T2): 순액 = floor(W × (100−r) ÷ 100).
        /// ⚠️ **세액은 반드시 잔여(W − 순액)로 구한다.** 세액을 따로 반올림하면
        /// 순액 + 세액 ≠ W가 되어 회계 폐곡선(§8 D2)이 깨진다 (명세 확정 보완 3).
        /// 실수령이 0이 되는 세율(100%)은 임금을 플래너 시야에서 지워 저축 goal을
        /// NoSolution으로 만든다 — WorldConfigSO.OnValidate가 상한 90으로 막는다.</summary>
        public static int NetWage(int wage, int taxRatePct)
        {
            if (wage <= 0) return 0;
            return Mathf.FloorToInt(wage * (100 - Mathf.Clamp(taxRatePct, 0, 100)) / 100f);
        }

        /// <summary>
        /// 실행자 생성 — 실행 1회마다 새 인스턴스 (W4).
        /// abstract이므로 새 계열 SO는 컴파일러가 구현을 강제한다. 문자열 분기 금지의 핵심.
        /// </summary>
        public abstract IActionRunner CreateRunner(VillagerAgent agent);
    }
}
