namespace AIVillage.Core.GOAP
{
    /// <summary>
    /// W8 브릿지 — 폐기된 GOAPActionRegistry/GOAPPlanningSlots에서 잔존 GOAPPlannerJob이
    /// 필요로 하는 최소 정의만 이전 (ADR-M0-5: 잡 알고리즘 무변경, 상수·구조체만 이동).
    /// 필드 구성·의미 무변경. 채움은 M0 ActionCompiler가 담당한다.
    /// </summary>
    public struct GOAPActionDef
    {
        /// <summary>액션 신원. M0에서는 해시가 아니라 ActionCatalog 배열 인덱스 (ADR-M0-6).</summary>
        public int ActionStringHash;

        /// <summary>플래너 비용.</summary>
        public float BaseCost;

        // ── Precondition 최대 8개: S=슬롯, V=값, Op(0=Equal, 1=GreaterEq, 2=LessEq) ──
        public int PrecCount;
        public int Prec0S; public int Prec0V; public int Prec0Op;
        public int Prec1S; public int Prec1V; public int Prec1Op;
        public int Prec2S; public int Prec2V; public int Prec2Op;
        public int Prec3S; public int Prec3V; public int Prec3Op;
        public int Prec4S; public int Prec4V; public int Prec4Op;
        public int Prec5S; public int Prec5V; public int Prec5Op;
        public int Prec6S; public int Prec6V; public int Prec6Op;
        public int Prec7S; public int Prec7V; public int Prec7Op;

        // ── Effect 최대 8개: S=슬롯, V=값, Op(0=Set, 1=Add, 2=Sub 0클램프) ──
        public int EffectCount;
        public int Eff0S; public int Eff0V; public int Eff0Op;
        public int Eff1S; public int Eff1V; public int Eff1Op;
        public int Eff2S; public int Eff2V; public int Eff2Op;
        public int Eff3S; public int Eff3V; public int Eff3Op;
        public int Eff4S; public int Eff4V; public int Eff4Op;
        public int Eff5S; public int Eff5V; public int Eff5Op;
        public int Eff6S; public int Eff6V; public int Eff6Op;
        public int Eff7S; public int Eff7V; public int Eff7Op;
    }

    /// <summary>
    /// 잡의 슬롯 배열 크기 상수 브릿지. M0는 앞쪽 SlotIds.Count(10)개만 사용하고
    /// 나머지는 0 유지 (GoalMask 0 → 판정 무시). 값 무변경 (舊 52).
    /// </summary>
    public static class GOAPPlanningSlots
    {
        public const int TOTAL_SLOTS = 52;
    }
}
