using System;

namespace AIVillage.M0
{
    /// <summary>
    /// M0 플래닝 슬롯. enum 값 = 슬롯 배열 인덱스 (W2 ActionCompiler가 직접 사용).
    /// 수치형(0~3)과 논리형(4~7, 값 0/1)을 구분해 배치했다. 슬롯 추가 시 SlotIds.Count 갱신 필수.
    /// </summary>
    public enum SlotId
    {
        // ── 수치형 ────────────────────────────────────────────────────────
        WoodStock    = 0,
        RawFoodStock = 1,
        MySatiety    = 2,  // 포만감 (높을수록 배부름) — 舊 2026-07-10 세만틱 반전 계승
        MyFatigue    = 3,

        // ── 논리형 (0/1) ─────────────────────────────────────────────────
        NearDiscoveredWood = 4,
        NearDiscoveredFood = 5,
        CampfireBuilt      = 6,
        AtBuildSite        = 7,
    }

    public static class SlotIds
    {
        public const int Count = 8;
    }

    /// <summary>전제조건 비교 연산. 값은 舊 goalOps 규약(1=GreaterEq)과 정렬 — W2 컴파일 단순화.</summary>
    public enum CompareOp
    {
        Equal          = 0,
        GreaterOrEqual = 1,
        LessOrEqual    = 2,
    }

    /// <summary>효과 연산. 값은 舊 effect op 규약(0=Set, 1=Add, 2=Sub)과 정렬. Sub는 항상 0 클램프.</summary>
    public enum EffectOp
    {
        Set      = 0,
        Add      = 1,
        SubClamp0 = 2,
    }

    [Serializable]
    public struct SlotCondition
    {
        public SlotId Slot;
        public CompareOp Op;
        public int Value;
    }

    [Serializable]
    public struct SlotEffect
    {
        public SlotId Slot;
        public EffectOp Op;
        public int Value;
    }
}
