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

        // ── M11-K 확장 (탈중심 마을 — 개인 모닥불) ──
        CampfireCount     = 31, // 수치형 — 개인 모닥불 카운트 (소유 키. 舊 CampfireBuilt 단일 플래그 대체)
        MyHasCampfire     = 32, // 논리형 — 내 모닥불 소유 (원천 = OwnershipService, MyHasHome 패턴). 조리 전제

        // ── 겨울 채집 봉쇄 (ADR-M6-1 개정 2026-07-24) ──
        ForageFrozen      = 33, // 논리형 — 이 계절 야생 채집 봉쇄 (원천 = SeasonService.ForageFrozen).
                                // ⚠️ **어떤 액션도 이 슬롯에 효과를 가지면 안 된다** — 플래너가 위조할 수
                                // 없어야 봉쇄가 성립한다. NearDiscoveredFood(5)로 막던 舊 시도는 Explore가
                                // 그 슬롯을 Set 1 하는 바람에 "Explore→채집" 우회 플랜이 나와 실패했다.

        // ── M12-G 확장 (집 동기 — 기질 옆의 경험 축) ──
        MyWasStarved      = 34, // 논리형 — 굶어 죽을 뻔한 경험 (영구 true. MyWasAttacked 동형,
                                // 원천 = VillagerAgent 굶주림 계단 1곳. 세이브 대상).
                                // ⚠️ "포만 0을 한 번 찍음"이 아니라 **죽음 문턱 근처까지 갔다 살아남음** —
                                // 겨울 봉쇄로 굶주림이 흔해진 지금 전자로 두면 전원 참이 되어 성향 문턱이
                                // 다시 무력해진다(M12 결정 11의 희소성).

        // ── M14 확장 (가을과 겨울 — 계절 방아쇠, ADR-M14-2 "위기 ≠ 봉쇄") ──
        DaysToFreeze      = 35, // 수치형 파생 — 다음 채집 봉쇄 계절 시작까지 일수(올림). 봉쇄 중 0,
                                // 사이클에 봉쇄 없으면 99. DaysToCrisis(15)와 다르다: 여름(위기·봉쇄X)
                                // 앞·중에도 0이 되지 않는다 (원천 = SeasonService.DaysToFreeze). 예산 52칸 중 36.
        PlantWindowOpen   = 36, // 논리형 파생 — "지금 심으면 봉쇄 전에 익는다"
                                // (GrowthMult > 0 && DaysToFreeze ≥ FarmGrowthDays — WorldModel.ComputePlantWindow).
                                // 미배선(계절 없음) = 1 = 기존 동작(창 상시 열림).
                                // ⚠️ 액션 효과 금지 — ForageFrozen(33) 규약 동형 (위조 가능하면 겨울 파종이 뚫린다).
                                // 예산 52칸 중 37.

        // ── M16 확장 (화폐 — 개인 지갑) ──
        MyMoney           = 37, // 수치형 — 몸 소지 돈(동). 개인 스톡 (원천 = VillagerAgent.ApplyPersonalStock,
                                // ADR-M11-1). ⚠️ 집 저장 없음·소지 상한 없음 (돈은 부피가 없다 — PersonalCapOf).
                                // 플래너 전제·효과 금지 (ADR-M16-5 — 스냅샷 값은 부탁 스캔 조건 전용).
                                // 예산 52칸 중 38.

        // ── M17 확장 (재정 — 빚) ──
        MyDebt            = 38, // 수치형 — 내가 남에게 갚아야 할 돈(동). 원천 = RequestService
                                // (미정산 보상 = 조각 Y의 빚 사전). ⚠️ **IsPersonalStock 아님** —
                                // 빚은 소지품이 아니다. 넣으면 TransferTo가 "빚을 남에게 주는"
                                // 경로를 열어 준다. 액션 효과 금지 (원천이 서비스 한 곳).
                                // 예산 52칸 중 39.
    }

    public static class SlotIds
    {
        public const int Count = 39;

        /// <summary>전역 스톡 슬롯 여부 — EffectApplier/러너가 공유하는 유일한 판정.
        /// ⚠️ 개인 스톡(MyRawFood 등)을 여기 넣으면 안 된다 (명세 M11-A ⚠️①) —
        /// EffectApplier 선검사가 WorldModel을 읽어 개인 식사가 전부 실패한다. IsPersonalStock이 별도 판정.</summary>
        public static bool IsStock(SlotId slot)
            => slot == SlotId.WoodStock || slot == SlotId.RawFoodStock || slot == SlotId.StoneStock
            || slot == SlotId.CookedFoodStock;

        /// <summary>몸 소지 개인 스톡 여부 (M11-A) — 원천 = VillagerAgent (ADR-M11-1).
        /// MyMoney(M16)도 여기다 — 이전·지급 판정(TransferTo·CanPayReward)이 이 판정 하나를
        /// 공유한다. ⚠️ IsStock에는 절대 넣지 않는다 (전역 스톡 라우팅 — 명세 W1 ⚠️).</summary>
        public static bool IsPersonalStock(SlotId slot)
            => slot == SlotId.MyRawFood || slot == SlotId.MyCookedFood || slot == SlotId.MyMoney;

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
            || slot == SlotId.MyRipeCrop || slot == SlotId.UntendedInjuredCount
            || slot == SlotId.CampfireCount // MyHasCampfire(32)는 논리형이라 제외
            || slot == SlotId.DaysToFreeze  // PlantWindowOpen(36)은 논리형이라 제외
            || slot == SlotId.MyMoney       // M16 — 지갑(동)
            || slot == SlotId.MyDebt;       // M17 — 빚(동)

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

    /// <summary>전제조건 비교 연산. 값은 舊 goalOps 규약(1=GreaterEq)과 정렬 — W2 컴파일 단순화.
    /// ⚠️ Less/Greater(M18)는 **트리거 전용**이다 (ADR-M18-1) — 플래너 잡은 0~2만 안다.
    /// ActionCompiler가 (int)Op를 그대로 잡에 먹이므로 Preconditions·GoalConditions에 새 값이
    /// 들어가면 잡의 스위치가 모르는 연산이 된다. 게이트 M18-T1이 에셋 전수를 감시한다.</summary>
    public enum CompareOp
    {
        Equal          = 0,
        GreaterOrEqual = 1,
        LessOrEqual    = 2,
        Less           = 3, // M18 — 슬롯 비교의 경계 겹침 방지 (상수 시절의 "≤ N-1" 수법 대체)
        Greater        = 4, // M18 — 〃 (대칭)
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

        // ── M18 — 슬롯 대 슬롯 비교 (ADR-M18-1: 트리거 전용) ──
        // 켜면 Value 대신 RightSlot의 현재값이 우변이 된다 (AllHold 한 곳만 해석).
        // 기존 에셋은 필드 부재 → false/0 역직렬화 = 동작 완전 불변.
        // ⚠️ GoalConditions·Preconditions에서 켜면 잡은 이 필드를 몰라 상수 Value로 목표를
        // 세운다 — 플래너 목표와 완수 판정이 갈린다. OnValidate 2곳 + 게이트 M18-T1이 막는다.
        public bool   CompareToSlot;
        public SlotId RightSlot;

        /// <summary>트리거 전용 문법(슬롯 비교·Less·Greater) 사용 여부 — 금지 칸 판정의
        /// **유일한 출처** (ADR-M18-1). GoalSO/ActionSO OnValidate와 게이트 M18-T1이 이것
        /// 하나를 읽는다 — 판정을 각자 들고 있으면 게이트와 에디터가 갈린다.</summary>
        public static bool UsesTriggerOnlyGrammar(SlotCondition[] conditions)
        {
            if (conditions == null) return false;
            foreach (SlotCondition c in conditions)
                if (c.CompareToSlot || c.Op >= CompareOp.Less) return true;
            return false;
        }
    }

    [Serializable]
    public struct SlotEffect
    {
        public SlotId Slot;
        public EffectOp Op;
        public int Value;
    }
}
