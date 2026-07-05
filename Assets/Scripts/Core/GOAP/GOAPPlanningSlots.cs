/// <summary>
/// GOAPPlanningSlots.cs - GOAP 플래닝 전용 월드 스테이트 슬롯 인덱스 상수 정의
///
/// 역할(Role): Job System NativeArray<int>(43 슬롯)의 각 인덱스가 무엇을 의미하는지
///             상수로 선언한다. 마법 숫자(Magic Number) 사용을 완전히 제거한다.
///             GOAPStateUtil이 AuthoritativeWorldState + VillagerBrain → NativeArray 변환을 담당한다.
/// 사용법(Usage): 정적 클래스 직접 참조.
///               예: NativeArray<int>[GOAPPlanningSlots.WoodLow] = 1;
/// 의존성(Dependencies):
///   GOAPStateUtil: AIVillage.Core (AuthoritativeWorldState, ResourceRegistry, ResourceType)
///                  AIVillage.AI (VillagerBrain)
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-26
/// </summary>

using Unity.Collections;
using AIVillage.Core;
using AIVillage.AI;

namespace AIVillage.Core.GOAP
{
    /// <summary>
    /// GOAP 플래닝에서 사용하는 월드 스테이트 NativeArray&lt;int&gt;의 슬롯 인덱스 상수.
    /// 슬롯 값 규칙: 0 = false/부족/미달성, 1 = true/충분/달성
    /// </summary>
    public static class GOAPPlanningSlots
    {
        // ── 자원 부족 플래그 ────────────────────────────────────────────────────
        // 0 = 충분, 1 = 부족 (직관과 반대로 느낄 수 있으나 "문제 있음 = 1" 패턴 통일)

        /// <summary>목재 부족 여부. woodAvailable &lt; 30 → 1, else 0</summary>
        public const int WoodLow        = 0;

        /// <summary>석재 부족 여부. stoneAvailable &lt; 30 → 1, else 0</summary>
        public const int StoneLow       = 1;

        /// <summary>생 식량 부족 여부. rawFoodAvailable &lt; 30 → 1, else 0</summary>
        public const int RawFoodLow     = 2;

        /// <summary>조리 식량 부족 여부. cookedFoodAvailable &lt; 30 → 1, else 0</summary>
        public const int CookedFoodLow  = 3;

        /// <summary>철 부족 여부. ironAvailable &lt; 1 → 1, else 0</summary>
        public const int IronLow        = 4;

        /// <summary>구리 부족 여부. copperAvailable &lt; 1 → 1, else 0</summary>
        public const int CopperLow      = 5;

        // ── 건물 상태 플래그 ─────────────────────────────────────────────────────
        // AuthoritativeWorldState의 bool 필드를 int로 변환한다.

        /// <summary>Town Hall 건설 완료 여부.</summary>
        public const int TownHallBuilt   = 6;

        /// <summary>Forge(대장간) 건설 완료 여부.</summary>
        public const int ForgeBuilt      = 7;

        /// <summary>Storehouse(창고) 건설 완료 여부.</summary>
        public const int StorehouseBuilt = 8;

        /// <summary>건설 큐에 대기 중인 건물이 있는지 여부.</summary>
        public const int BuildingQueued  = 9;

        // ── 자원 가용 여부 (특정 수량 이상 있는지) ─────────────────────────────
        // 위의 "부족 플래그"와 별개로, 특정 Action의 Precondition에서 직접 사용한다.

        /// <summary>조리된 식량이 1개 이상 있는지. cookedFoodAvailable &gt;= 1f → 1</summary>
        public const int HasCookedFood       = 10;

        /// <summary>생 식량이 1개 이상 있는지. rawFoodAvailable &gt;= 1f → 1</summary>
        public const int HasRawFood          = 11;

        /// <summary>목재 건설 충분 여부. woodAvailable &gt;= 35f → 1 (기획서 수치: TownHall 비용)</summary>
        public const int HasWoodForBuilding  = 12;

        /// <summary>석재 건설 충분 여부. stoneAvailable &gt;= 30f → 1</summary>
        public const int HasStoneForBuilding = 13;

        /// <summary>철 건설 충분 여부. ironAvailable &gt;= 6f → 1 (기획서 수치: CraftWeapon 비용)</summary>
        public const int HasIronForBuilding  = 14;

        // ── 생존 위기 플래그 ─────────────────────────────────────────────────────
        // P0 Goal 발동 기준과 동일한 수치를 사용한다.

        /// <summary>배고픔 위기. hungerLevel &gt; 80f → 1 (기획서 수치)</summary>
        public const int HungerCritical  = 15;

        /// <summary>부상 위기. healthLevel &lt; 20f → 1 (기획서 수치)</summary>
        public const int InjuryCritical  = 16;

        /// <summary>탈진 위기. fatigueLevel &gt; 90f → 1 (기획서 수치)</summary>
        public const int FatigueCritical = 17;

        // ── 에이전트 공간 플래그 (VillagerBrain 환경 플래그와 1:1 대응) ─────────

        /// <summary>기지 위치에 있는지 여부.</summary>
        public const int AtBase               = 18;

        /// <summary>수집 가능한 자원 노드 근처.</summary>
        public const int NearResource         = 19;

        /// <summary>채석 가능한 바위 근처.</summary>
        public const int NearRock             = 20;

        /// <summary>철 광석 노드 근처.</summary>
        public const int NearIronOre          = 21;

        /// <summary>구리 광석 노드 근처.</summary>
        public const int NearCopperOre        = 22;

        /// <summary>FoW에서 발견된 자원 노드 근처 (NearResource AND isDiscovered).</summary>
        public const int NearDiscoveredResource = 23;

        /// <summary>인식 범위 내 적 존재.</summary>
        public const int NearEnemy            = 24;

        /// <summary>주변에 드롭 아이템 존재.</summary>
        public const int NearDroppedItem      = 25;

        /// <summary>침대 근처 (수면 회복 가능).</summary>
        public const int NearBed              = 26;

        /// <summary>모닥불 근처 (조리 가능).</summary>
        public const int NearFireplace        = 27;

        /// <summary>창고 근처 (자원 납부 가능).</summary>
        public const int NearStorage          = 28;

        /// <summary>치료사 근처 (의료 치료 가능).</summary>
        public const int NearHealer           = 29;

        /// <summary>망루 근처 (방어 지원 가능).</summary>
        public const int NearWatchtower       = 30;

        // ── 에이전트 인벤토리 플래그 ─────────────────────────────────────────────

        /// <summary>도구(도끼/곡괭이) 소지 여부.</summary>
        public const int HasTool            = 31;

        /// <summary>제련 무기 소지 여부.</summary>
        public const int HasWeapon          = 32;

        /// <summary>원시 무기 소지 여부 (Wood 3 + Stone 2, Forge 불필요).</summary>
        public const int HasPrimitiveWeapon = 33;

        /// <summary>인벤토리에 식량 보유 여부.</summary>
        public const int HasFood            = 34;

        // ── 목표 달성 플래그 (Goal의 target state) ───────────────────────────────
        // 플래닝 시작 시 항상 0, 액션 효과(Effect)로 1이 된다.

        /// <summary>배고픔 해결 완료 플래그.</summary>
        public const int HungerSolved      = 35;

        /// <summary>부상 해결 완료 플래그.</summary>
        public const int InjurySolved      = 36;

        /// <summary>피로 해결 완료 플래그.</summary>
        public const int FatigueSolved     = 37;

        /// <summary>자원 수집 완료 플래그.</summary>
        public const int ResourcesGathered = 38;

        /// <summary>건물 건설 완료 플래그.</summary>
        public const int StructureBuilt    = 39;

        /// <summary>적 격퇴 완료 플래그.</summary>
        public const int EnemyDefeated     = 40;

        /// <summary>탐험 완료 플래그.</summary>
        public const int AreaExplored      = 41;

        /// <summary>식사 조리 완료 플래그.</summary>
        public const int MealCooked        = 42;

        // ── [Phase 2] 수치 슬롯 (43~51) ── 값은 0/1이 아니라 원시 단위 정수다. ──
        // AuthoritativeWorldState 가용량(총량-예약량)과 VillagerBrain 스탯을 그대로 미러링한다.

        /// <summary>마을 목재 가용량 (정수). registry.GetAvailable(Wood) 미러.</summary>
        public const int WoodStock       = 43;

        /// <summary>마을 석재 가용량 (정수).</summary>
        public const int StoneStock      = 44;

        /// <summary>마을 철 가용량 (정수).</summary>
        public const int IronStock       = 45;

        /// <summary>마을 구리 가용량 (정수).</summary>
        public const int CopperStock     = 46;

        /// <summary>마을 생 식량 가용량 (정수).</summary>
        public const int RawFoodStock    = 47;

        /// <summary>마을 조리 식량 가용량 (정수).</summary>
        public const int CookedFoodStock = 48;

        /// <summary>이 주민의 배고픔 0~100 (VillagerBrain.HungerLevel 미러).</summary>
        public const int MyHunger        = 49;

        /// <summary>이 주민의 피로도 0~100 (VillagerBrain.FatigueLevel 미러).</summary>
        public const int MyFatigue       = 50;

        /// <summary>이 주민의 체력 0~100 (VillagerBrain.HealthLevel 미러).</summary>
        public const int MyHealth        = 51;

        /// <summary>NativeArray 총 슬롯 수. 할당 시 반드시 이 값을 사용한다.</summary>
        public const int TOTAL_SLOTS = 52;
    }

    // ────────────────────────────────────────────────────────────────────────────
    // GOAPStateUtil — 런타임 상태를 NativeArray로 변환하는 정적 유틸리티
    // ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// AuthoritativeWorldState + VillagerBrain을 GOAP Job이 읽을 수 있는
    /// NativeArray&lt;int&gt; (43슬롯)으로 변환하는 정적 유틸리티.
    ///
    /// 스레드 안전성:
    ///   BuildCurrentState()는 메인 스레드에서 호출하여 NativeArray를 생성한다.
    ///   생성된 NativeArray는 Job System에 ReadOnly로 전달되므로 안전하다.
    /// </summary>
    public static class GOAPStateUtil
    {
        // 자원 부족 판정 임계값 (기획서 수치)
        // TODO: 기획팀 수치 확인 필요 — 현재 설계서 기준 30 사용
        private const float LOW_RESOURCE_THRESHOLD = 30f; // 이 미만이면 "부족" 판정
        private const float LOW_RARE_THRESHOLD     = 1f;  // Iron/Copper: 1 미만이면 "부족"

        // 건설 가용 자원 판정 임계값 (기획서 수치)
        private const float WOOD_FOR_BUILDING  = 5f;  // Campfire 최소 목재 비용 (TownHall 등은 HasStoneForBuilding으로 추가 보호)
        private const float STONE_FOR_BUILDING = 30f;
        private const float IRON_FOR_BUILDING  = 6f;

        /// <summary>
        /// AuthoritativeWorldState, ResourceRegistry, VillagerBrain을 읽어
        /// GOAP 플래닝용 NativeArray&lt;int&gt;(43 슬롯)을 생성하고 반환한다.
        ///
        /// 호출자는 반드시 반환된 배열을 Dispose해야 한다.
        /// GOAPPlannerScheduler.Schedule()이 이 배열을 생성 후 Job에 전달하고
        /// PlanningContext.Dispose()에서 해제한다.
        /// </summary>
        /// <param name="worldState">전역 자원/건물 상태.</param>
        /// <param name="registry">가용 자원량 계산에 사용하는 예약 관리자.</param>
        /// <param name="brain">에이전트의 현재 위치/인벤토리/생존 수치 상태.</param>
        /// <param name="allocator">NativeArray 할당자. 기본값 Persistent.</param>
        /// <returns>43 슬롯짜리 GOAP 월드 스테이트 NativeArray.</returns>
        public static NativeArray<int> BuildCurrentState(
            AuthoritativeWorldState worldState,
            ResourceRegistry        registry,
            VillagerBrain           brain,
            Allocator               allocator = Allocator.Persistent)
        {
            // ── 방어 코드: null 입력 체크 ───────────────────────────────────────
            // worldState나 brain이 null이면 0으로 가득 찬 배열을 반환한다.
            // 이 경우 GOAP 플래너는 "목표 달성 불가" 판정을 내려 Replanning으로 전이한다.
            if (worldState == null || brain == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[GOAPStateUtil] BuildCurrentState: worldState 또는 brain이 null입니다. " +
                    "0으로 초기화된 스테이트를 반환합니다."
                );
                // NativeArray는 생성 시 자동으로 0으로 초기화된다.
                return new NativeArray<int>(GOAPPlanningSlots.TOTAL_SLOTS, allocator);
            }

            var state = new NativeArray<int>(GOAPPlanningSlots.TOTAL_SLOTS, allocator);

            // ── 자원 부족 플래그 ──────────────────────────────────────────────
            // registry가 null이면 WorldState 재고를 직접 읽는다 (예약량 무시).
            float availWood    = registry != null
                ? registry.GetAvailable(ResourceType.Wood)       : worldState.WoodStock;
            float availStone   = registry != null
                ? registry.GetAvailable(ResourceType.Stone)      : worldState.StoneStock;
            float availRawFood = registry != null
                ? registry.GetAvailable(ResourceType.RawFood)    : worldState.RawFoodStock;
            float availCooked  = registry != null
                ? registry.GetAvailable(ResourceType.CookedFood) : worldState.CookedFoodStock;
            float availIron    = registry != null
                ? registry.GetAvailable(ResourceType.Iron)       : worldState.IronStock;
            float availCopper  = registry != null
                ? registry.GetAvailable(ResourceType.Copper)     : worldState.CopperStock;

            // 기획서 수치: 30 미만이면 부족 판정
            state[GOAPPlanningSlots.WoodLow]       = availWood    < LOW_RESOURCE_THRESHOLD ? 1 : 0;
            state[GOAPPlanningSlots.StoneLow]      = availStone   < LOW_RESOURCE_THRESHOLD ? 1 : 0;
            state[GOAPPlanningSlots.RawFoodLow]    = availRawFood < LOW_RESOURCE_THRESHOLD ? 1 : 0;
            state[GOAPPlanningSlots.CookedFoodLow] = availCooked  < LOW_RESOURCE_THRESHOLD ? 1 : 0;
            // Iron/Copper: 기획서 수치 1 미만이면 부족 판정
            state[GOAPPlanningSlots.IronLow]       = availIron   < LOW_RARE_THRESHOLD ? 1 : 0;
            state[GOAPPlanningSlots.CopperLow]     = availCopper < LOW_RARE_THRESHOLD ? 1 : 0;

            // ── 건물 상태 플래그 ──────────────────────────────────────────────
            state[GOAPPlanningSlots.TownHallBuilt]   = worldState.TownHallBuilt   ? 1 : 0;
            state[GOAPPlanningSlots.ForgeBuilt]      = worldState.ForgeBuilt      ? 1 : 0;
            state[GOAPPlanningSlots.StorehouseBuilt] = worldState.StorehouseBuilt ? 1 : 0;
            state[GOAPPlanningSlots.BuildingQueued]  = worldState.BuildingQueued  ? 1 : 0;

            // ── 자원 가용 여부 플래그 ─────────────────────────────────────────
            // [PR Fix]: N-002 — 기존 중복 변수 제거 — 상단의 availCooked 등 재사용
            // 기존: cookedAvail, rawAvail, woodAvail, stoneAvail, ironAvail을 이 블록에서 재선언
            //       → registry.GetAvailable()을 두 번 호출하는 중복 계산 + 유지보수 위험
            // 수정: 상단에서 이미 계산된 availCooked / availRawFood / availWood / availStone / availIron을
            //       직접 재사용하여 중복 계산을 완전히 제거한다.

            // 기획서 수치: cookedFood >= 1 → 먹을 수 있음
            state[GOAPPlanningSlots.HasCookedFood]       = availCooked  >= 1f                 ? 1 : 0;
            state[GOAPPlanningSlots.HasRawFood]          = availRawFood >= 1f                 ? 1 : 0;
            // 기획서 수치: wood >= 35, stone >= 30, iron >= 6 → 건설 가능
            state[GOAPPlanningSlots.HasWoodForBuilding]  = availWood    >= WOOD_FOR_BUILDING  ? 1 : 0;
            state[GOAPPlanningSlots.HasStoneForBuilding] = availStone   >= STONE_FOR_BUILDING ? 1 : 0;
            state[GOAPPlanningSlots.HasIronForBuilding]  = availIron    >= IRON_FOR_BUILDING  ? 1 : 0;

            // ── 생존 위기 플래그 (기획서 수치: P0 Goal 발동 기준과 동일) ────────
            state[GOAPPlanningSlots.HungerCritical]  = brain.HungerLevel  > 80f ? 1 : 0;
            state[GOAPPlanningSlots.InjuryCritical]  = brain.HealthLevel  < 20f ? 1 : 0;
            state[GOAPPlanningSlots.FatigueCritical] = brain.FatigueLevel > 90f ? 1 : 0;

            // ── 에이전트 공간 플래그 (VillagerBrain 환경 플래그 직접 매핑) ──────
            state[GOAPPlanningSlots.AtBase]                 = brain.AtBase               ? 1 : 0;
            state[GOAPPlanningSlots.NearResource]           = brain.NearResource         ? 1 : 0;
            state[GOAPPlanningSlots.NearRock]               = brain.NearRock             ? 1 : 0;
            state[GOAPPlanningSlots.NearIronOre]            = brain.NearIronOre          ? 1 : 0;
            state[GOAPPlanningSlots.NearCopperOre]          = brain.NearCopperOre        ? 1 : 0;
            state[GOAPPlanningSlots.NearDiscoveredResource] = brain.NearDiscoveredResource ? 1 : 0;
            state[GOAPPlanningSlots.NearEnemy]              = brain.NearEnemy            ? 1 : 0;
            state[GOAPPlanningSlots.NearDroppedItem]        = brain.NearDroppedItem      ? 1 : 0;
            state[GOAPPlanningSlots.NearBed]                = brain.NearBed              ? 1 : 0;
            state[GOAPPlanningSlots.NearFireplace]          = brain.NearFireplace        ? 1 : 0;
            state[GOAPPlanningSlots.NearStorage]            = brain.NearStorage          ? 1 : 0;
            state[GOAPPlanningSlots.NearHealer]             = brain.NearHealer           ? 1 : 0;
            state[GOAPPlanningSlots.NearWatchtower]         = brain.NearWatchtower       ? 1 : 0;

            // ── 인벤토리 플래그 ───────────────────────────────────────────────
            state[GOAPPlanningSlots.HasTool]            = brain.HasTool            ? 1 : 0;
            state[GOAPPlanningSlots.HasWeapon]          = brain.HasWeapon          ? 1 : 0;
            state[GOAPPlanningSlots.HasPrimitiveWeapon] = brain.HasPrimitiveWeapon ? 1 : 0;
            state[GOAPPlanningSlots.HasFood]            = brain.HasFood            ? 1 : 0;

            // ── 목표 달성 플래그: 플래닝 시작 시 항상 0 ──────────────────────
            // 슬롯 35~42는 NativeArray 생성 시 이미 0으로 초기화되므로 명시적 설정 불필요.

            // ── [Phase 2] 수치 슬롯 (43~51) — 가용량과 스탯을 그대로 미러링 ──
            // 플래너가 예약된 자원 위에 계획을 세우지 않도록 가용량(총량-예약량)을 사용한다.
            state[GOAPPlanningSlots.WoodStock]       = (int)availWood;
            state[GOAPPlanningSlots.StoneStock]      = (int)availStone;
            state[GOAPPlanningSlots.IronStock]       = (int)availIron;
            state[GOAPPlanningSlots.CopperStock]     = (int)availCopper;
            state[GOAPPlanningSlots.RawFoodStock]    = (int)availRawFood;
            state[GOAPPlanningSlots.CookedFoodStock] = (int)availCooked;
            state[GOAPPlanningSlots.MyHunger]        = (int)brain.HungerLevel;
            state[GOAPPlanningSlots.MyFatigue]       = (int)brain.FatigueLevel;
            state[GOAPPlanningSlots.MyHealth]        = (int)brain.HealthLevel;

            return state;
        }

        /// <summary>
        /// goalId를 받아 GOAP 탐색이 달성해야 하는 목표 상태(goalState), 마스크(goalMask),
        /// 슬롯별 판정 연산자(goalOps)를 생성한다.
        ///
        /// goalMask[i]=1인 슬롯에 대해 PrecHolds(state[i], goalOps[i], goalState[i])이면 목표 달성.
        /// goalOps[i]: 0=Equal(기존 불리언), 1=GreaterEq, 2=LessEq
        /// useNumericGoals=false(기본)이면 goalOps 전부 0 → 기존 불리언 동작과 동일.
        ///
        /// 호출자는 반환된 세 배열을 모두 Dispose해야 한다.
        /// </summary>
        /// <param name="goalId">목표 식별자 문자열 (예: "SurviveHunger", "GatherResources").</param>
        /// <param name="goalState">목표 달성 시 각 슬롯의 목표 값 (out).</param>
        /// <param name="goalMask">목표 판정에 포함되는 슬롯 마스크 (out). 1=포함, 0=무시.</param>
        /// <param name="goalOps">슬롯별 판정 연산자 (out). 0=Equal, 1=GreaterEq, 2=LessEq.</param>
        /// <param name="useNumericGoals">true이면 수치형 슬롯으로 목표를 정의한다 (ADR-8).</param>
        /// <param name="allocator">NativeArray 할당자.</param>
        public static void BuildGoalState(
            string               goalId,
            out NativeArray<int> goalState,
            out NativeArray<int> goalMask,
            out NativeArray<int> goalOps,
            bool                 useNumericGoals = false,
            Allocator            allocator = Allocator.Persistent)
        {
            goalState = new NativeArray<int>(GOAPPlanningSlots.TOTAL_SLOTS, allocator);
            goalMask  = new NativeArray<int>(GOAPPlanningSlots.TOTAL_SLOTS, allocator);
            goalOps   = new NativeArray<int>(GOAPPlanningSlots.TOTAL_SLOTS, allocator);
            // 세 배열 모두 생성 시 자동으로 0으로 초기화된다 (0 = Equal / 레거시 동작).

            if (string.IsNullOrEmpty(goalId))
            {
                UnityEngine.Debug.LogWarning(
                    "[GOAPStateUtil] BuildGoalState: goalId가 null 또는 빈 문자열입니다. " +
                    "빈 마스크(즉시 성공)를 반환합니다."
                );
                return;
            }

            // ── goalId별 목표 슬롯 설정 ──────────────────────────────────────
            switch (goalId)
            {
                // P0: 생존 위기 해결
                case "SurviveHunger":
                    if (useNumericGoals)
                    {
                        // MyHunger LessEq 30 (배고픔을 30 이하로 낮추면 달성)
                        goalMask[GOAPPlanningSlots.MyHunger]  = 1;
                        goalState[GOAPPlanningSlots.MyHunger] = 30;
                        goalOps[GOAPPlanningSlots.MyHunger]   = 2; // LessEq
                    }
                    else
                    {
                        goalMask[GOAPPlanningSlots.HungerSolved]  = 1;
                        goalState[GOAPPlanningSlots.HungerSolved] = 1;
                    }
                    break;

                case "SurviveInjury":
                    if (useNumericGoals)
                    {
                        // MyHealth GreaterEq 60
                        goalMask[GOAPPlanningSlots.MyHealth]  = 1;
                        goalState[GOAPPlanningSlots.MyHealth] = 60;
                        goalOps[GOAPPlanningSlots.MyHealth]   = 1; // GreaterEq
                    }
                    else
                    {
                        goalMask[GOAPPlanningSlots.InjurySolved]  = 1;
                        goalState[GOAPPlanningSlots.InjurySolved] = 1;
                    }
                    break;

                case "SurviveFatigue":
                    if (useNumericGoals)
                    {
                        // MyFatigue LessEq 30
                        goalMask[GOAPPlanningSlots.MyFatigue]  = 1;
                        goalState[GOAPPlanningSlots.MyFatigue] = 30;
                        goalOps[GOAPPlanningSlots.MyFatigue]   = 2; // LessEq
                    }
                    else
                    {
                        goalMask[GOAPPlanningSlots.FatigueSolved]  = 1;
                        goalState[GOAPPlanningSlots.FatigueSolved] = 1;
                    }
                    break;

                // P2: 자원 수집 (수치형: 각 자원 30 이상 보유)
                case "GatherResources":
                    if (useNumericGoals)
                    {
                        // 기본: 목재 30 이상 확보 (GoalArbiter(W8)가 자원별 분기 결정)
                        goalMask[GOAPPlanningSlots.WoodStock]  = 1;
                        goalState[GOAPPlanningSlots.WoodStock] = 30;
                        goalOps[GOAPPlanningSlots.WoodStock]   = 1; // GreaterEq
                    }
                    else
                    {
                        goalMask[GOAPPlanningSlots.ResourcesGathered]  = 1;
                        goalState[GOAPPlanningSlots.ResourcesGathered] = 1;
                    }
                    break;

                case "GatherWood":
                    if (useNumericGoals)
                    {
                        goalMask[GOAPPlanningSlots.WoodStock]  = 1;
                        goalState[GOAPPlanningSlots.WoodStock] = 30;
                        goalOps[GOAPPlanningSlots.WoodStock]   = 1; // GreaterEq
                    }
                    else
                    {
                        goalMask[GOAPPlanningSlots.ResourcesGathered]  = 1;
                        goalState[GOAPPlanningSlots.ResourcesGathered] = 1;
                    }
                    break;

                case "GatherStone":
                    if (useNumericGoals)
                    {
                        goalMask[GOAPPlanningSlots.StoneStock]  = 1;
                        goalState[GOAPPlanningSlots.StoneStock] = 30;
                        goalOps[GOAPPlanningSlots.StoneStock]   = 1; // GreaterEq
                    }
                    else
                    {
                        goalMask[GOAPPlanningSlots.ResourcesGathered]  = 1;
                        goalState[GOAPPlanningSlots.ResourcesGathered] = 1;
                    }
                    break;

                case "GatherIron":
                    if (useNumericGoals)
                    {
                        goalMask[GOAPPlanningSlots.IronStock]  = 1;
                        goalState[GOAPPlanningSlots.IronStock] = 15;
                        goalOps[GOAPPlanningSlots.IronStock]   = 1; // GreaterEq
                    }
                    else
                    {
                        goalMask[GOAPPlanningSlots.ResourcesGathered]  = 1;
                        goalState[GOAPPlanningSlots.ResourcesGathered] = 1;
                    }
                    break;

                case "GatherCopper":
                    if (useNumericGoals)
                    {
                        goalMask[GOAPPlanningSlots.CopperStock]  = 1;
                        goalState[GOAPPlanningSlots.CopperStock] = 15;
                        goalOps[GOAPPlanningSlots.CopperStock]   = 1; // GreaterEq
                    }
                    else
                    {
                        goalMask[GOAPPlanningSlots.ResourcesGathered]  = 1;
                        goalState[GOAPPlanningSlots.ResourcesGathered] = 1;
                    }
                    break;

                // P2: 건설
                case "BuildStructure":
                    goalMask[GOAPPlanningSlots.StructureBuilt]  = 1;
                    goalState[GOAPPlanningSlots.StructureBuilt] = 1;
                    break;

                // P1: 전투/방어
                case "DefendVillage":
                case "AttackEnemy":
                    goalMask[GOAPPlanningSlots.EnemyDefeated]  = 1;
                    goalState[GOAPPlanningSlots.EnemyDefeated] = 1;
                    break;

                // P3: 탐험
                case "Explore":
                    goalMask[GOAPPlanningSlots.AreaExplored]  = 1;
                    goalState[GOAPPlanningSlots.AreaExplored] = 1;
                    break;

                // 요리
                case "CookMeal":
                    if (useNumericGoals)
                    {
                        // CookedFoodStock GreaterEq 3
                        goalMask[GOAPPlanningSlots.CookedFoodStock]  = 1;
                        goalState[GOAPPlanningSlots.CookedFoodStock] = 3;
                        goalOps[GOAPPlanningSlots.CookedFoodStock]   = 1; // GreaterEq
                    }
                    else
                    {
                        goalMask[GOAPPlanningSlots.MealCooked]  = 1;
                        goalState[GOAPPlanningSlots.MealCooked] = 1;
                    }
                    break;

                // 기지 복귀 (Deadlock 폴백 Goal)
                case "MoveToBase":
                    goalMask[GOAPPlanningSlots.AtBase]  = 1;
                    goalState[GOAPPlanningSlots.AtBase] = 1;
                    break;

                // MoveToTarget은 AtBase 달성을 목표로 하는 이동 Goal로 처리한다.
                case "MoveToTarget":
                    goalMask[GOAPPlanningSlots.AtBase]  = 1;
                    goalState[GOAPPlanningSlots.AtBase] = 1;
                    break;

                default:
                    // 인식되지 않은 goalId: 마스크를 모두 0으로 유지
                    // GOAPPlannerScheduler.Schedule()이 goalMask all-zero 방어 코드로 처리한다.
                    UnityEngine.Debug.LogWarning(
                        $"[GOAPStateUtil] BuildGoalState: 알 수 없는 goalId '{goalId}'. " +
                        $"빈 마스크를 반환합니다. GOAPPlannerScheduler가 Schedule을 중단합니다."
                    );
                    break;
            }
        }
    }
}
