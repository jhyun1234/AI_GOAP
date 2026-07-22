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
        EmptyFarmPlot     = 12, // 수치형(개수, ADR-M3-2 승격) — FarmService가 유일한 원천 (ADR-M2-4)
        RipeCropAvailable = 13, // 수치형(개수, ADR-M3-2 승격) — FarmService가 유일한 원천 (ADR-M2-4)

        // ── M3 확장 (기존 인덱스 뒤에만 추가 — 에셋 호환 유지) ─────────────
        HouseCount        = 14, // 수치형 — 집 수량 (M3-D)

        // ── M6 확장 (기존 인덱스 뒤에만 추가 — 에셋 호환 유지) ─────────────
        DaysToCrisis      = 15, // 수치형 — 다음 위기 계절까지 일수(올림). 위기 중 0, 계절/위기 없으면 99 (SeasonService.NO_CRISIS)
        CrisisActive      = 16, // 논리형 — 위기 계절 진행 중

        // ── M8 확장 (기존 인덱스 뒤에만 추가 — 에셋 호환 유지) ─────────────
        MyHasHome         = 17, // 논리형 — 내 소유 집 존재 (OwnershipService가 유일한 원천, M8-C)

        // ── M9 확장 (기존 인덱스 뒤에만 추가 — 에셋 호환 유지) ─────────────
        IrrigationBuilt   = 18, // 논리형 — 관개수로 완공 (재해 저항 슬롯, M9-D). 예산 52칸 중 19 사용.
        MyFoodDaysLeft    = 19, // 수치형 — [M11 개정] 舊 FoodDaysLeft. 내(몸+집) 남은 식량 일수 파생.
                                // ⚠️ M11-D 전까지는 산식이 아직 마을 합산 — 이름만 선행 개명 (직렬화 int라 에셋 호환).

        // ── M10 확장 (기존 인덱스 뒤에만 추가 — 에셋 호환 유지) ────────────
        InjuredCount      = 20, // 수치형 — 부상 주민 수. 파생 슬롯(SimulationLoop 집계, 트리거 전용 ADR-M9-9 패턴). 예산 52칸 중 21.
        ThreatNear        = 21, // 논리형 — 내 감지 반경 내 활성 위협 존재. 개인 파생 슬롯(ThreatService.IsNearThreat, M10-D 배선). 예산 52칸 중 22.

        // ── M11 확장 (기존 인덱스 뒤에만 추가 — 에셋 호환 유지) ────────────
        MyRawFood         = 22, // 수치형 — 몸 소지 생식. 개인 스톡 (원천 = VillagerAgent.ApplyPersonalStock, ADR-M11-1)
        MyCookedFood      = 23, // 수치형 — 몸 소지 조리식
        MyHomeRawFood     = 24, // 수치형 — 내 집 저장 생식 (원천 = HomeStorageService — 타일 키, M12 집 타격-상실 대비)
        MyHomeCookedFood  = 25, // 수치형 — 내 집 저장 조리식
        MyWasAttacked     = 26, // 논리형 — 피격 경험 (Injure 1회로 영구 true. 집 동기 축, M11-G 배선. 세이브 대상)
        MyFarmPlotCount   = 27, // 수치형 — 내 소유 밭 수 (원천 = FarmService 소유 필터, M11-E 배선)
        MyEmptyPlot       = 28, // 수치형 — 내 빈 밭 수
        MyRipeCrop        = 29, // 수치형 — 내 익은 밭 수
        UntendedInjuredCount = 30, // 수치형 — 미안정 부상자 수 (안정화 goal 트리거 — crowding 해소 축, M11-I 배선). 예산 52칸 중 31.
    }

    public static class SlotIds
    {
        public const int Count = 31;

        /// <summary>전역 스톡 슬롯 여부 — EffectApplier/러너가 공유하는 유일한 판정.
        /// ⚠️ 개인 스톡(MyRawFood 등)을 여기 넣으면 안 된다 (명세 M11-A ⚠️①) —
        /// EffectApplier 선검사가 WorldModel을 읽어 개인 식사가 전부 실패한다. IsPersonalStock이 별도 판정.</summary>
        public static bool IsStock(SlotId slot)
            => slot == SlotId.WoodStock || slot == SlotId.RawFoodStock || slot == SlotId.StoneStock
            || slot == SlotId.CookedFoodStock;

        /// <summary>몸 소지 개인 스톡 여부 (M11-A) — 원천 = VillagerAgent (ADR-M11-1).</summary>
        public static bool IsPersonalStock(SlotId slot)
            => slot == SlotId.MyRawFood || slot == SlotId.MyCookedFood;

        /// <summary>집 저장 스톡 여부 (M11-A) — 원천 = HomeStorageService (ADR-M11-1). 무주택이면 효과 실패.</summary>
        public static bool IsHomeStock(SlotId slot)
            => slot == SlotId.MyHomeRawFood || slot == SlotId.MyHomeCookedFood;

        /// <summary>
        /// 수치형 슬롯 여부 — "수치 슬롯만 허용" 검증(BuildingSO.CountSlot 등 OnValidate)의
        /// 유일한 판정. 범위 상수 하드코딩 대신 여기만 갱신한다 (슬롯 추가 시 낡은 범위 방지).
        /// </summary>
        public static bool IsNumeric(SlotId slot)
            => slot == SlotId.WoodStock || slot == SlotId.RawFoodStock
            || slot == SlotId.MySatiety || slot == SlotId.MyFatigue
            || slot == SlotId.StoneStock
            || slot == SlotId.CookedFoodStock || slot == SlotId.FarmPlotCount
            || slot == SlotId.HouseCount || slot == SlotId.DaysToCrisis
            || slot == SlotId.MyFoodDaysLeft || slot == SlotId.InjuredCount
            || slot == SlotId.MyRawFood || slot == SlotId.MyCookedFood
            || slot == SlotId.MyHomeRawFood || slot == SlotId.MyHomeCookedFood
            || slot == SlotId.MyFarmPlotCount || slot == SlotId.MyEmptyPlot
            || slot == SlotId.MyRipeCrop || slot == SlotId.UntendedInjuredCount;

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
