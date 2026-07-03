/// <summary>
/// GOAPActionRegistry.cs - GOAP Action 정의 레지스트리
///
/// 역할(Role): 20개 GOAP Action의 Preconditions, Effects, BaseCost를
///             Burst Compiler 호환 GOAPActionDef struct NativeArray로 관리한다.
///             역할(AgentRole)별 비용 보정을 BuildActionDefs() 호출 시 BaseCost에 즉시 적용한다.
/// 사용법(Usage): GOAPPlannerScheduler.Schedule() 내부에서만 호출한다.
///               var defs = GOAPActionRegistry.BuildActionDefs(AgentRole.Lumberjack, Allocator.Persistent);
/// 의존성(Dependencies): GOAPPlanningSlots.cs, Unity.Collections, AIVillage.AI (AgentRole)
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-26
/// </summary>

using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using AIVillage.AI;

namespace AIVillage.Core.GOAP
{
    /// <summary>
    /// Burst Compiler 호환 GOAP Action 정의 구조체.
    ///
    /// Burst 제약 사항:
    ///   - 관리형 타입(string, List, class) 사용 불가
    ///   - NativeContainer 내부 포함 불가
    ///   - unsafe 없이 고정 크기 배열 사용 불가 (명시적 필드로 대체)
    ///
    /// 설계 결정: Precondition/Effect를 최대 8개 (slot, value) 쌍으로 제한.
    ///            8개 초과가 필요한 경우 Action을 분리한다.
    /// </summary>
    public struct GOAPActionDef
    {
        /// <summary>
        /// Action 식별자의 해시 값 (Animator.StringToHash와 동일한 알고리즘).
        /// Job 내에서 string 비교를 피하기 위해 int 해시를 사용한다.
        /// HashToActionId()로 역매핑 가능.
        /// </summary>
        public int ActionStringHash;

        /// <summary>
        /// 역할 보정이 반영된 실제 Action 비용.
        /// BuildActionDefs(role) 호출 시 GDD 기획서 기준 보정 배율이 적용된다.
        /// </summary>
        public float BaseCost;

        // ── Precondition 목록 ─────────────────────────────────────────────────
        // 최대 8개 (슬롯 인덱스, 요구 값) 쌍. Burst 호환을 위해 명시적 필드로 선언.
        // PrecCount = 0이면 Precondition 없음 (언제든 실행 가능).

        /// <summary>유효한 Precondition 항목 수. 0~8.</summary>
        public int PrecCount;

        // 각 필드: S = Slot 인덱스, V = 요구 값 (0 또는 1)
        public int Prec0S; public int Prec0V;
        public int Prec1S; public int Prec1V;
        public int Prec2S; public int Prec2V;
        public int Prec3S; public int Prec3V;
        public int Prec4S; public int Prec4V;
        public int Prec5S; public int Prec5V;
        public int Prec6S; public int Prec6V;
        public int Prec7S; public int Prec7V;

        // ── Effect 목록 ──────────────────────────────────────────────────────
        // 최대 8개 (슬롯 인덱스, 설정 값) 쌍.
        // EffectCount = 0이면 Effect 없음 (순수 이동 액션 등).

        /// <summary>유효한 Effect 항목 수. 0~8.</summary>
        public int EffectCount;

        public int Eff0S; public int Eff0V;
        public int Eff1S; public int Eff1V;
        public int Eff2S; public int Eff2V;
        public int Eff3S; public int Eff3V;
        public int Eff4S; public int Eff4V;
        public int Eff5S; public int Eff5V;
        public int Eff6S; public int Eff6V;
        public int Eff7S; public int Eff7V;

        // ────────────────────────────────────────────────────────────────────
        // 헬퍼 메서드: Burst에서 호출 가능한 인스턴스 메서드
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 현재 월드 스테이트가 이 Action의 모든 Precondition을 충족하는지 확인한다.
        /// PrecCount가 0이면 항상 true를 반환한다.
        /// switch-case unroll로 루프 오버헤드를 제거하여 Burst 최적화에 유리하다.
        /// </summary>
        /// <param name="state">현재 GOAP 월드 스테이트 (43 슬롯).</param>
        /// <returns>모든 Precondition 충족 시 true, 하나라도 불충족 시 false.</returns>
        public bool CheckPreconditions(NativeArray<int> state)
        {
            switch (PrecCount)
            {
                case 0: return true;
                case 1: return state[Prec0S] == Prec0V;
                case 2: return state[Prec0S] == Prec0V
                            && state[Prec1S] == Prec1V;
                case 3: return state[Prec0S] == Prec0V
                            && state[Prec1S] == Prec1V
                            && state[Prec2S] == Prec2V;
                case 4: return state[Prec0S] == Prec0V
                            && state[Prec1S] == Prec1V
                            && state[Prec2S] == Prec2V
                            && state[Prec3S] == Prec3V;
                case 5: return state[Prec0S] == Prec0V
                            && state[Prec1S] == Prec1V
                            && state[Prec2S] == Prec2V
                            && state[Prec3S] == Prec3V
                            && state[Prec4S] == Prec4V;
                case 6: return state[Prec0S] == Prec0V
                            && state[Prec1S] == Prec1V
                            && state[Prec2S] == Prec2V
                            && state[Prec3S] == Prec3V
                            && state[Prec4S] == Prec4V
                            && state[Prec5S] == Prec5V;
                case 7: return state[Prec0S] == Prec0V
                            && state[Prec1S] == Prec1V
                            && state[Prec2S] == Prec2V
                            && state[Prec3S] == Prec3V
                            && state[Prec4S] == Prec4V
                            && state[Prec5S] == Prec5V
                            && state[Prec6S] == Prec6V;
                case 8: return state[Prec0S] == Prec0V
                            && state[Prec1S] == Prec1V
                            && state[Prec2S] == Prec2V
                            && state[Prec3S] == Prec3V
                            && state[Prec4S] == Prec4V
                            && state[Prec5S] == Prec5V
                            && state[Prec6S] == Prec6V
                            && state[Prec7S] == Prec7V;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 이 Action의 Effect를 월드 스테이트에 적용한다.
        /// 주의: 전달된 NativeArray를 직접 수정한다.
        ///       원본 상태 보존이 필요하면 사전에 복사해야 한다.
        /// </summary>
        /// <param name="state">Effect를 적용할 GOAP 월드 스테이트 (수정됨).</param>
        public void ApplyEffects(NativeArray<int> state)
        {
            switch (EffectCount)
            {
                case 0: return;
                case 1:
                    state[Eff0S] = Eff0V;
                    return;
                case 2:
                    state[Eff0S] = Eff0V;
                    state[Eff1S] = Eff1V;
                    return;
                case 3:
                    state[Eff0S] = Eff0V;
                    state[Eff1S] = Eff1V;
                    state[Eff2S] = Eff2V;
                    return;
                case 4:
                    state[Eff0S] = Eff0V;
                    state[Eff1S] = Eff1V;
                    state[Eff2S] = Eff2V;
                    state[Eff3S] = Eff3V;
                    return;
                case 5:
                    state[Eff0S] = Eff0V;
                    state[Eff1S] = Eff1V;
                    state[Eff2S] = Eff2V;
                    state[Eff3S] = Eff3V;
                    state[Eff4S] = Eff4V;
                    return;
                case 6:
                    state[Eff0S] = Eff0V;
                    state[Eff1S] = Eff1V;
                    state[Eff2S] = Eff2V;
                    state[Eff3S] = Eff3V;
                    state[Eff4S] = Eff4V;
                    state[Eff5S] = Eff5V;
                    return;
                case 7:
                    state[Eff0S] = Eff0V;
                    state[Eff1S] = Eff1V;
                    state[Eff2S] = Eff2V;
                    state[Eff3S] = Eff3V;
                    state[Eff4S] = Eff4V;
                    state[Eff5S] = Eff5V;
                    state[Eff6S] = Eff6V;
                    return;
                case 8:
                    state[Eff0S] = Eff0V;
                    state[Eff1S] = Eff1V;
                    state[Eff2S] = Eff2V;
                    state[Eff3S] = Eff3V;
                    state[Eff4S] = Eff4V;
                    state[Eff5S] = Eff5V;
                    state[Eff6S] = Eff6V;
                    state[Eff7S] = Eff7V;
                    return;
            }
        }
    }

    // ────────────────────────────────────────────────────────────────────────────
    // GOAPActionRegistry — Action 정의 목록을 NativeArray로 생성하는 팩토리
    // ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// GDD v0.4 기준 20개 GOAP Action의 정의를 NativeArray로 생성하는 정적 팩토리 클래스.
    ///
    /// 역할별 비용 보정 (BuildActionDefs 호출 시 BaseCost에 즉시 적용):
    ///   Lumberjack: ChopWood x0.5, MineStone x1.3
    ///   Miner:      MineStone/MineIron/MineCopper x0.5, ChopWood x1.3
    ///   Builder:    Build* x0.6
    ///   Warrior:    AttackEnemy x0.6, CraftWeapon x0.7
    ///   Cook:       CookMeal x0.4, AttackEnemy x1.8
    ///   Medic:      SeekMedicalAid x0.5
    /// </summary>
    public static class GOAPActionRegistry
    {
        // ── 비용 보정 배율 상수 (기획서 수치) ─────────────────────────────────
        private const float LUMBERJACK_CHOP_MODIFIER = 0.5f;
        private const float LUMBERJACK_MINE_MODIFIER = 1.3f;
        private const float MINER_MINE_MODIFIER      = 0.5f;
        private const float MINER_CHOP_MODIFIER      = 1.3f;
        private const float BUILDER_BUILD_MODIFIER   = 0.6f;
        private const float WARRIOR_ATTACK_MODIFIER  = 0.6f;
        private const float WARRIOR_CRAFT_MODIFIER   = 0.7f;
        private const float COOK_COOK_MODIFIER       = 0.4f;
        private const float COOK_ATTACK_MODIFIER     = 1.8f;
        private const float MEDIC_HEAL_MODIFIER      = 0.5f;

        // ── 슬롯 단축 참조 (코드 가독성용 로컬 alias) ──────────────────────
        private static class S
        {
            public const int WoodLow         = GOAPPlanningSlots.WoodLow;
            public const int StoneLow        = GOAPPlanningSlots.StoneLow;
            public const int IronLow         = GOAPPlanningSlots.IronLow;
            public const int CopperLow       = GOAPPlanningSlots.CopperLow;
            public const int ForgeBuilt      = GOAPPlanningSlots.ForgeBuilt;
            public const int BuildingQueued  = GOAPPlanningSlots.BuildingQueued;
            public const int HasCookedFood       = GOAPPlanningSlots.HasCookedFood;
            public const int HasRawFood          = GOAPPlanningSlots.HasRawFood;
            public const int HasWoodForBuilding  = GOAPPlanningSlots.HasWoodForBuilding;
            public const int HasStoneForBuilding = GOAPPlanningSlots.HasStoneForBuilding;
            public const int HasIronForBuilding  = GOAPPlanningSlots.HasIronForBuilding;
            public const int HungerCritical  = GOAPPlanningSlots.HungerCritical;
            // [PR Fix]: N-003 — InjuryCritical을 S 클래스에 추가
            // 기존: SeekMedicalAid 정의에서 GOAPPlanningSlots.InjuryCritical을 직접 참조
            //       → S 클래스를 통한 단일 참조 지점 원칙 위반 (일관성 결여)
            // 수정: S.InjuryCritical을 추가하여 SeekMedicalAid 정의에서 S.InjuryCritical로 참조
            public const int InjuryCritical  = GOAPPlanningSlots.InjuryCritical;
            public const int FatigueCritical = GOAPPlanningSlots.FatigueCritical;
            public const int AtBase          = GOAPPlanningSlots.AtBase;
            public const int NearResource    = GOAPPlanningSlots.NearResource;
            public const int NearRock        = GOAPPlanningSlots.NearRock;
            public const int NearIronOre     = GOAPPlanningSlots.NearIronOre;
            public const int NearCopperOre   = GOAPPlanningSlots.NearCopperOre;
            public const int NearEnemy       = GOAPPlanningSlots.NearEnemy;
            public const int NearBed         = GOAPPlanningSlots.NearBed;
            public const int NearFireplace   = GOAPPlanningSlots.NearFireplace;
            public const int NearStorage     = GOAPPlanningSlots.NearStorage;
            public const int NearHealer      = GOAPPlanningSlots.NearHealer;
            public const int NearDiscoveredResource = GOAPPlanningSlots.NearDiscoveredResource;
            public const int HasTool            = GOAPPlanningSlots.HasTool;
            public const int HasWeapon          = GOAPPlanningSlots.HasWeapon;
            public const int HasPrimitiveWeapon = GOAPPlanningSlots.HasPrimitiveWeapon;
            public const int HungerSolved      = GOAPPlanningSlots.HungerSolved;
            public const int InjurySolved      = GOAPPlanningSlots.InjurySolved;
            public const int FatigueSolved     = GOAPPlanningSlots.FatigueSolved;
            public const int ResourcesGathered = GOAPPlanningSlots.ResourcesGathered;
            public const int StructureBuilt    = GOAPPlanningSlots.StructureBuilt;
            public const int EnemyDefeated     = GOAPPlanningSlots.EnemyDefeated;
            public const int AreaExplored      = GOAPPlanningSlots.AreaExplored;
            public const int MealCooked        = GOAPPlanningSlots.MealCooked;
        }

        // ────────────────────────────────────────────────────────────────────────────
        // [PR Fix]: Major-1 — HashToActionId를 정적 딕셔너리 방식으로 교체
        // 기존: if (hash == Animator.StringToHash("ChopWood")) return "ChopWood"; ... (19개 if 체인)
        //       → 매 호출마다 Animator.StringToHash()를 최대 19번 호출 = 성능 낭비
        // 수정: 정적 생성자에서 Dictionary를 한 번만 빌드하고 TryGetValue로 O(1) 조회
        //       해시 충돌 감지 로직도 포함한다.
        // ────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Action 이름 → 해시 역매핑 딕셔너리.
        /// 정적 생성자에서 한 번만 초기화된다. 런타임에서는 읽기 전용으로 사용한다.
        /// </summary>
        private static readonly Dictionary<int, string> _hashToId;

        /// <summary>
        /// 정적 생성자: 클래스가 처음 참조될 때 한 번만 실행된다.
        /// 20개 Action 이름을 해시로 변환하여 딕셔너리에 등록한다.
        /// 해시 충돌이 감지되면 즉시 LogError로 보고한다.
        /// </summary>
        static GOAPActionRegistry()
        {
            // 20개 Action 이름 배열 (BuildActionDefs의 Action 목록과 동기화 유지)
            var names = new string[]
            {
                "ChopWood", "MineStone", "MineIron", "MineCopper",
                "EatCookedFood", "EatRawFood", "CookMeal", "Sleep",
                "RestOnGround", "SeekMedicalAid", "MoveToBase",
                "CraftPrimitiveWeapon", "AttackEnemy", "CraftWeapon",
                "BuildTownHall", "BuildForge", "BuildStorehouse",
                "Explore", "HarvestWildBerries", "BuildCampfire"
            };

            _hashToId = new Dictionary<int, string>(names.Length);
            foreach (var name in names)
            {
                int h = Animator.StringToHash(name);
                if (_hashToId.ContainsKey(h))
                {
                    // 해시 충돌은 Action 이름이 매우 유사하거나 알고리즘 문제일 때 발생
                    Debug.LogError(
                        $"[GOAPActionRegistry] 해시 충돌 감지: '{name}'과 '{_hashToId[h]}'의 해시가 동일합니다 ({h}). " +
                        $"Action 이름을 변경하거나 기획팀에 보고하세요."
                    );
                }
                else
                {
                    _hashToId[h] = name;
                }
            }
        }

        /// <summary>
        /// GDD v0.4 기준 20개 Action 정의를 NativeArray로 빌드하여 반환한다.
        /// 역할(role)에 따른 비용 보정이 BaseCost에 즉시 반영된다.
        /// 호출자는 반환된 배열을 반드시 Dispose해야 한다.
        /// </summary>
        /// <param name="role">비용 보정 기준 역할. AgentRole.None이면 보정 없음.</param>
        /// <param name="allocator">NativeArray 할당자.</param>
        /// <returns>20개 GOAPActionDef가 담긴 NativeArray.</returns>
        /// <param name="seasonGatherModifier">
        /// [14단계] 계절별 수집 비용 배율. Autumn=0.333f(선호도 3배), 나머지=1.0f.
        /// SeasonManager.Instance?.GetCurrentGatherCostModifier() ?? 1.0f로 메인 스레드에서 주입한다.
        /// </param>
        public static NativeArray<GOAPActionDef> BuildActionDefs(
            AgentRole role,
            Allocator allocator = Allocator.Persistent,
            float seasonGatherModifier = 1.0f)
        {
            // 기획서 수치 — 20개 Action 기본 비용 (GDD v0.4 기준)
            var defs = new NativeArray<GOAPActionDef>(20, allocator);
            int i = 0;

            // ── ChopWood (나무 채집) ──────────────────────────────────────────
            // 기획서 수치: BaseCost=10, Lumberjack x0.5, Miner x1.3
            // [14단계] Autumn: seasonGatherModifier=0.333f → 비용 1/3 → 수집 선호도 3배
            // Preconditions: NearDiscoveredResource=1, HasTool=1
            // Effects: ResourcesGathered=1, WoodLow=0
            // NearResource → NearDiscoveredResource 변경 이유:
            //   자원 노드는 기지에서 8~10타일 거리에 스폰되지만 감지 반경은 5타일이다.
            //   Explore(Effect: NearDiscoveredResource=1)가 선행 Action이 되어야
            //   GOAP 플래너가 [Explore → ChopWood] 체인을 역추론할 수 있다.
            //   NearRock/NearIronOre/NearCopperOre 역시 동일한 이유로 NearDiscoveredResource로 통일한다.
            //   실제 이동 목적지 결정은 FSM의 MoveTileForAction()이 자원 타입별로 처리한다.
            float chopCost = 10f;
            if (role == AgentRole.Lumberjack)      chopCost *= LUMBERJACK_CHOP_MODIFIER;
            else if (role == AgentRole.Miner)      chopCost *= MINER_CHOP_MODIFIER;
            chopCost *= seasonGatherModifier;
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("ChopWood"),
                BaseCost = chopCost,
                PrecCount = 2, Prec0S = S.NearDiscoveredResource, Prec0V = 1, Prec1S = S.HasTool, Prec1V = 1,
                EffectCount = 2, Eff0S = S.ResourcesGathered, Eff0V = 1, Eff1S = S.WoodLow, Eff1V = 0
            };

            // ── MineStone (돌 채광) ───────────────────────────────────────────
            // 기획서 수치: BaseCost=12, Miner x0.5, Lumberjack x1.3
            // Preconditions: NearDiscoveredResource=1, HasTool=1
            // Effects: ResourcesGathered=1, StoneLow=0
            float mineStoneCost = 12f;
            if (role == AgentRole.Miner)           mineStoneCost *= MINER_MINE_MODIFIER;
            else if (role == AgentRole.Lumberjack) mineStoneCost *= LUMBERJACK_MINE_MODIFIER;
            mineStoneCost *= seasonGatherModifier;
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("MineStone"),
                BaseCost = mineStoneCost,
                PrecCount = 2, Prec0S = S.NearDiscoveredResource, Prec0V = 1, Prec1S = S.HasTool, Prec1V = 1,
                EffectCount = 2, Eff0S = S.ResourcesGathered, Eff0V = 1, Eff1S = S.StoneLow, Eff1V = 0
            };

            // ── MineIron (철 채광) ────────────────────────────────────────────
            // 기획서 수치: BaseCost=15, Miner x0.5
            // Preconditions: NearDiscoveredResource=1, HasTool=1
            // Effects: ResourcesGathered=1, IronLow=0
            float mineIronCost = 15f;
            if (role == AgentRole.Miner) mineIronCost *= MINER_MINE_MODIFIER;
            mineIronCost *= seasonGatherModifier;
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("MineIron"),
                BaseCost = mineIronCost,
                PrecCount = 2, Prec0S = S.NearDiscoveredResource, Prec0V = 1, Prec1S = S.HasTool, Prec1V = 1,
                EffectCount = 2, Eff0S = S.ResourcesGathered, Eff0V = 1, Eff1S = S.IronLow, Eff1V = 0
            };

            // ── MineCopper (구리 채광) ────────────────────────────────────────
            // 기획서 수치: BaseCost=16, Miner x0.5
            // Preconditions: NearDiscoveredResource=1, HasTool=1
            // Effects: ResourcesGathered=1, CopperLow=0
            float mineCopperCost = 16f;
            if (role == AgentRole.Miner) mineCopperCost *= MINER_MINE_MODIFIER;
            mineCopperCost *= seasonGatherModifier;
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("MineCopper"),
                BaseCost = mineCopperCost,
                PrecCount = 2, Prec0S = S.NearDiscoveredResource, Prec0V = 1, Prec1S = S.HasTool, Prec1V = 1,
                EffectCount = 2, Eff0S = S.ResourcesGathered, Eff0V = 1, Eff1S = S.CopperLow, Eff1V = 0
            };

            // ── EatCookedFood (조리된 음식 섭취) ─────────────────────────────
            // 기획서 수치: BaseCost=5, 역할 보정 없음
            // Preconditions: HasCookedFood=1
            // Effects: HungerSolved=1, HungerCritical=0
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("EatCookedFood"),
                BaseCost = 5f,
                PrecCount = 1, Prec0S = S.HasCookedFood, Prec0V = 1,
                EffectCount = 2, Eff0S = S.HungerSolved, Eff0V = 1, Eff1S = S.HungerCritical, Eff1V = 0
            };

            // ── EatRawFood (생 음식 섭취) ─────────────────────────────────────
            // 기획서 수치: BaseCost=8, 역할 보정 없음
            // Preconditions: HasRawFood=1
            // Effects: HungerSolved=1, HungerCritical=0
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("EatRawFood"),
                BaseCost = 8f,
                PrecCount = 1, Prec0S = S.HasRawFood, Prec0V = 1,
                EffectCount = 2, Eff0S = S.HungerSolved, Eff0V = 1, Eff1S = S.HungerCritical, Eff1V = 0
            };

            // ── CookMeal (음식 조리) ──────────────────────────────────────────
            // 기획서 수치: BaseCost=6, Cook x0.4
            // Preconditions: HasRawFood=1, NearFireplace=1
            // Effects: HasCookedFood=1, MealCooked=1
            float cookCost = 6f;
            if (role == AgentRole.Cook) cookCost *= COOK_COOK_MODIFIER;
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("CookMeal"),
                BaseCost = cookCost,
                PrecCount = 2, Prec0S = S.HasRawFood, Prec0V = 1, Prec1S = S.NearFireplace, Prec1V = 1,
                EffectCount = 2, Eff0S = S.HasCookedFood, Eff0V = 1, Eff1S = S.MealCooked, Eff1V = 1
            };

            // ── Sleep (수면 회복) ─────────────────────────────────────────────
            // 기획서 수치: BaseCost=5, 역할 보정 없음
            // Preconditions: NearBed=1
            // Effects: FatigueSolved=1, FatigueCritical=0
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("Sleep"),
                BaseCost = 5f,
                PrecCount = 1, Prec0S = S.NearBed, Prec0V = 1,
                EffectCount = 2, Eff0S = S.FatigueSolved, Eff0V = 1, Eff1S = S.FatigueCritical, Eff1V = 0
            };

            // ── RestOnGround (땅에서 쉬기) ────────────────────────────────────
            // 기획서 수치: BaseCost=12, 역할 보정 없음 (Precondition 없는 폴백 액션)
            // Preconditions: 없음
            // Effects: FatigueSolved=1, FatigueCritical=0
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("RestOnGround"),
                BaseCost = 12f,
                PrecCount = 0,
                EffectCount = 2, Eff0S = S.FatigueSolved, Eff0V = 1, Eff1S = S.FatigueCritical, Eff1V = 0
            };

            // ── SeekMedicalAid (의료 치료) ────────────────────────────────────
            // 기획서 수치: BaseCost=4, Medic x0.5
            // Preconditions: NearHealer=1
            // Effects: InjurySolved=1, InjuryCritical=0
            float healCost = 4f;
            if (role == AgentRole.Medic) healCost *= MEDIC_HEAL_MODIFIER;
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("SeekMedicalAid"),
                BaseCost = healCost,
                PrecCount = 1, Prec0S = S.NearHealer, Prec0V = 1,
                EffectCount = 2,
                Eff0S = S.InjurySolved,  Eff0V = 1,
                // [PR Fix]: N-003 — GOAPPlanningSlots.InjuryCritical → S.InjuryCritical로 변경
                Eff1S = S.InjuryCritical, Eff1V = 0
            };

            // ── MoveToBase (기지 귀환) ────────────────────────────────────────
            // 기획서 수치: BaseCost=3, 역할 보정 없음
            // Preconditions: 없음
            // Effects: AtBase=1, NearStorage=1
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("MoveToBase"),
                BaseCost = 3f,
                PrecCount = 0,
                EffectCount = 2, Eff0S = S.AtBase, Eff0V = 1, Eff1S = S.NearStorage, Eff1V = 1
            };

            // ── CraftPrimitiveWeapon (원시 무기 제작) ─────────────────────────
            // 기획서 수치: BaseCost=6, 역할 보정 없음
            // Preconditions: 없음
            // Effects: HasPrimitiveWeapon=1
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("CraftPrimitiveWeapon"),
                BaseCost = 6f,
                PrecCount = 0,
                EffectCount = 1, Eff0S = S.HasPrimitiveWeapon, Eff0V = 1
            };

            // ── AttackEnemy (적 공격) ─────────────────────────────────────────
            // 기획서 수치: BaseCost=10, Warrior x0.6, Cook x1.8
            // Preconditions: NearEnemy=1, HasPrimitiveWeapon=1
            // Effects: EnemyDefeated=1, NearEnemy=0
            float attackCost = 10f;
            if (role == AgentRole.Warrior)      attackCost *= WARRIOR_ATTACK_MODIFIER;
            else if (role == AgentRole.Cook)    attackCost *= COOK_ATTACK_MODIFIER;
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("AttackEnemy"),
                BaseCost = attackCost,
                PrecCount = 2, Prec0S = S.NearEnemy, Prec0V = 1, Prec1S = S.HasPrimitiveWeapon, Prec1V = 1,
                EffectCount = 2, Eff0S = S.EnemyDefeated, Eff0V = 1, Eff1S = S.NearEnemy, Eff1V = 0
            };

            // ── CraftWeapon (제련 무기 제작) ──────────────────────────────────
            // 기획서 수치: BaseCost=15, Warrior x0.7
            // Preconditions: ForgeBuilt=1, HasIronForBuilding=1
            // Effects: HasWeapon=1
            float craftWeaponCost = 15f;
            if (role == AgentRole.Warrior) craftWeaponCost *= WARRIOR_CRAFT_MODIFIER;
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("CraftWeapon"),
                BaseCost = craftWeaponCost,
                PrecCount = 2, Prec0S = S.ForgeBuilt, Prec0V = 1, Prec1S = S.HasIronForBuilding, Prec1V = 1,
                EffectCount = 1, Eff0S = S.HasWeapon, Eff0V = 1
            };

            // ── BuildTownHall (Town Hall 건설) ────────────────────────────────
            // 기획서 수치: BaseCost=50, Builder x0.6
            // Preconditions: BuildingQueued=1, HasWoodForBuilding=1, HasStoneForBuilding=1
            // Effects: StructureBuilt=1, TownHallBuilt=1, BuildingQueued=0
            float buildTHCost = 50f;
            if (role == AgentRole.Builder) buildTHCost *= BUILDER_BUILD_MODIFIER;
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("BuildTownHall"),
                BaseCost = buildTHCost,
                PrecCount = 3,
                Prec0S = S.BuildingQueued,      Prec0V = 1,
                Prec1S = S.HasWoodForBuilding,  Prec1V = 1,
                Prec2S = S.HasStoneForBuilding, Prec2V = 1,
                EffectCount = 3,
                Eff0S = S.StructureBuilt,                Eff0V = 1,
                Eff1S = GOAPPlanningSlots.TownHallBuilt, Eff1V = 1,
                Eff2S = S.BuildingQueued,                Eff2V = 0
            };

            // ── BuildForge (Forge 건설) ───────────────────────────────────────
            // 기획서 수치: BaseCost=40, Builder x0.6
            // [PR Fix]: Major-3 — BuildForge Precondition을 1→3으로 확장
            // 기존: BuildingQueued=1 만 요구 → 자원 없이도 건설 시도 → Replanning 루프 발생
            // 수정: HasWoodForBuilding=1, HasStoneForBuilding=1 조건 추가하여 자원 가용 시에만 건설 가능
            // Preconditions: BuildingQueued=1, HasWoodForBuilding=1, HasStoneForBuilding=1
            // Effects: StructureBuilt=1, ForgeBuilt=1, BuildingQueued=0
            float buildForgeCost = 40f;
            if (role == AgentRole.Builder) buildForgeCost *= BUILDER_BUILD_MODIFIER;
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("BuildForge"),
                BaseCost = buildForgeCost,
                PrecCount = 3,
                Prec0S = S.BuildingQueued,      Prec0V = 1,
                Prec1S = S.HasWoodForBuilding,  Prec1V = 1,
                Prec2S = S.HasStoneForBuilding, Prec2V = 1,
                EffectCount = 3,
                Eff0S = S.StructureBuilt, Eff0V = 1,
                Eff1S = S.ForgeBuilt,     Eff1V = 1,
                Eff2S = S.BuildingQueued, Eff2V = 0
            };

            // ── BuildStorehouse (Storehouse 건설) ─────────────────────────────
            // 기획서 수치: BaseCost=35, Builder x0.6
            // [PR Fix]: Major-3 — BuildStorehouse Precondition을 1→3으로 확장
            // 기존: BuildingQueued=1 만 요구 → 자원 없이도 건설 시도 → Replanning 루프 발생
            // 수정: HasWoodForBuilding=1, HasStoneForBuilding=1 조건 추가하여 자원 가용 시에만 건설 가능
            // Preconditions: BuildingQueued=1, HasWoodForBuilding=1, HasStoneForBuilding=1
            // Effects: StructureBuilt=1, StorehouseBuilt=1, BuildingQueued=0
            float buildStoreCost = 35f;
            if (role == AgentRole.Builder) buildStoreCost *= BUILDER_BUILD_MODIFIER;
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("BuildStorehouse"),
                BaseCost = buildStoreCost,
                PrecCount = 3,
                Prec0S = S.BuildingQueued,      Prec0V = 1,
                Prec1S = S.HasWoodForBuilding,  Prec1V = 1,
                Prec2S = S.HasStoneForBuilding, Prec2V = 1,
                EffectCount = 3,
                Eff0S = S.StructureBuilt,                  Eff0V = 1,
                Eff1S = GOAPPlanningSlots.StorehouseBuilt, Eff1V = 1,
                Eff2S = S.BuildingQueued,                  Eff2V = 0
            };

            // ── Explore (탐험) ────────────────────────────────────────────────
            // 기획서 수치: BaseCost=15, 역할 보정 없음
            // Preconditions: 없음
            // Effects: AreaExplored=1, NearDiscoveredResource=1
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("Explore"),
                BaseCost = 15f,
                PrecCount = 0,
                EffectCount = 2, Eff0S = S.AreaExplored, Eff0V = 1, Eff1S = S.NearDiscoveredResource, Eff1V = 1
            };

            // ── HarvestWildBerries (야생 열매 채집) ───────────────────────────
            // 기획서 수치: BaseCost=10, 역할 보정 없음 (도구 불필요)
            // Preconditions: NearDiscoveredResource=1
            // Effects: ResourcesGathered=1, HasRawFood=1, RawFoodLow=0
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("HarvestWildBerries"),
                BaseCost = 10f,
                PrecCount = 1, Prec0S = S.NearDiscoveredResource, Prec0V = 1,
                EffectCount = 3,
                Eff0S = S.ResourcesGathered,          Eff0V = 1,
                Eff1S = S.HasRawFood,                 Eff1V = 1,
                Eff2S = GOAPPlanningSlots.RawFoodLow, Eff2V = 0
            };

            // ── BuildCampfire (모닥불 건설) ───────────────────────────────────
            // 기획서 수치: BaseCost=20, Builder x0.6
            // Preconditions: BuildingQueued=1, HasWoodForBuilding=1  (Stone 불필요)
            // Effects: StructureBuilt=1, BuildingQueued=0
            float buildCampfireCost = 20f;
            if (role == AgentRole.Builder) buildCampfireCost *= BUILDER_BUILD_MODIFIER;
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("BuildCampfire"),
                BaseCost = buildCampfireCost,
                PrecCount = 2,
                Prec0S = S.BuildingQueued,     Prec0V = 1,
                Prec1S = S.HasWoodForBuilding, Prec1V = 1,
                EffectCount = 2,
                Eff0S = S.StructureBuilt,  Eff0V = 1,
                Eff1S = S.BuildingQueued,  Eff1V = 0
            };

            // 배열 크기와 실제 정의 수 검증 (개발 중 Action 추가/삭제 감지용)
            Debug.Assert(
                i == 20,
                $"[GOAPActionRegistry] Action 정의 수 불일치: 예상 20, 실제 {i}."
            );

            return defs;
        }

        /// <summary>
        /// Action 해시 값 → Action ID 문자열 역매핑.
        /// [PR Fix]: Major-1 — 정적 Dictionary _hashToId를 사용한 O(1) 조회로 교체
        /// 기존: if-체인으로 Animator.StringToHash()를 최대 19번 반복 호출 (성능 낭비)
        /// 수정: 정적 생성자에서 한 번 빌드된 Dictionary를 TryGetValue로 즉시 조회
        /// Job에서 해시로 저장된 결과를 메인 스레드에서 문자열 ActionId로 복원할 때 사용한다.
        /// 알 수 없는 해시는 "Unknown_[hash]" 형식으로 반환한다.
        /// </summary>
        public static string HashToActionId(int hash)
        {
            if (_hashToId.TryGetValue(hash, out string id)) return id;
            Debug.LogWarning($"[GOAPActionRegistry] 알 수 없는 해시: {hash}");
            return $"Unknown_{hash}";
        }
    }
}
