using System;
using AIVillage.Core;

namespace AIVillage.M0
{
    /// <summary>
    /// M0 플래닝 슬롯. enum 값 = 슬롯 배열 인덱스 (W2 ActionCompiler가 직접 사용).
    /// 수치형과 논리형(값 0/1)이 확장 구획별로 섞여 있다 — 판정은 SlotIds.IsNumeric이 유일한 출처.
    /// 슬롯 추가 시 SlotIds.Count·IsNumeric 갱신 필수 (기존 인덱스 뒤 append만 — 에셋 호환).
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

        // ── W7 확장 (기존 인덱스 뒤에만 추가 — 에셋 호환 유지) ─────────────
        StoneStock          = 8,  // 수치형
        NearDiscoveredStone = 9,  // 논리형

        // ── M2 확장 (기존 인덱스 뒤에만 추가 — 에셋 호환 유지) ─────────────
        CookedFoodStock   = 10, // 수치형
        FarmPlotCount     = 11, // 수치형 — 수량형 건물 카운트 (ADR-M2-3)
        EmptyFarmPlot     = 12, // 논리형 — FarmService가 유일한 원천 (ADR-M2-4)
        RipeCropAvailable = 13, // 논리형 — FarmService가 유일한 원천 (ADR-M2-4)
    }

    public static class SlotIds
    {
        public const int Count = 14;

        /// <summary>전역 스톡 슬롯 여부 — EffectApplier/러너가 공유하는 유일한 판정.</summary>
        public static bool IsStock(SlotId slot)
            => slot == SlotId.WoodStock || slot == SlotId.RawFoodStock || slot == SlotId.StoneStock
            || slot == SlotId.CookedFoodStock;

        /// <summary>
        /// 수치형 슬롯 여부 — "수치 슬롯만 허용" 검증(BuildingSO.CountSlot 등 OnValidate)의
        /// 유일한 판정. 범위 상수 하드코딩 대신 여기만 갱신한다 (슬롯 추가 시 낡은 범위 방지).
        /// </summary>
        public static bool IsNumeric(SlotId slot)
            => slot == SlotId.WoodStock || slot == SlotId.RawFoodStock
            || slot == SlotId.MySatiety || slot == SlotId.MyFatigue
            || slot == SlotId.StoneStock
            || slot == SlotId.CookedFoodStock || slot == SlotId.FarmPlotCount;

        /// <summary>자원 타입 → 스톡 슬롯. M0 미지원 타입이면 null (Iron/Copper/Silver는 M1).</summary>
        public static SlotId? StockOf(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Wood:       return SlotId.WoodStock;
                case ResourceType.RawFood:    return SlotId.RawFoodStock;
                case ResourceType.Stone:      return SlotId.StoneStock;
                case ResourceType.CookedFood: return SlotId.CookedFoodStock;
                default:                      return null;
            }
        }

        /// <summary>자원 타입 → 발견 플래그 슬롯. 새 자원 추가 시 여기와 enum만 확장하면 된다.</summary>
        public static SlotId? DiscoveredOf(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Wood:    return SlotId.NearDiscoveredWood;
                case ResourceType.RawFood: return SlotId.NearDiscoveredFood;
                case ResourceType.Stone:   return SlotId.NearDiscoveredStone;
                default:                   return null;
            }
        }
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
