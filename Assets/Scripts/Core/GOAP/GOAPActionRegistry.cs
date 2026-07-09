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
    // ── [Phase 2] Precondition / Effect / Goal 판정 연산자 ─────────────────────
    // int로 선언하여 Burst 구조체 정렬을 단순화한다. 기본값 0 = 기존 Equal/Set 동작.

    /// <summary>Precondition 및 Goal 판정 연산자. 기본값 0=Equal → 기존 불리언 동작과 동일.</summary>
    public enum PrecOp : int { Equal = 0, GreaterEq = 1, LessEq = 2 }

    /// <summary>Effect 적용 연산자. 기본값 0=Set → 기존 대입 동작과 동일.</summary>
    public enum EffOp  : int { Set = 0, Add = 1, Sub = 2 }

    /// <summary>
    /// BuildActionDefs() 호출 시 전달하는 컨텍스트 기반 액션 비용 배율.
    /// GOAPPlannerScheduler.ComputeContextMultipliers()가 메인 스레드에서 계산한다.
    /// 모든 필드 기본값 0 → Identity(1f)로 자동 치환된다.
    /// </summary>
    public struct ContextCostMultipliers
    {
        public float ChopWood;
        public float MineStone;
        public float MineIron;
        public float MineCopper;
        public float HarvestBerries;
        public float Explore;
        public float AttackEnemy;
        public float RestOnGround;

        public static ContextCostMultipliers Identity => new ContextCostMultipliers
        {
            ChopWood      = 1f,
            MineStone     = 1f,
            MineIron      = 1f,
            MineCopper    = 1f,
            HarvestBerries = 1f,
            Explore        = 1f,
            AttackEnemy    = 1f,
            RestOnGround   = 1f
        };
    }

    /// <summary>
    /// F-A: 성격별 액션 비용 배율. ContextCostMultipliers와 곱해져 최종 액션 비용에 반영된다.
    /// GOAPPlannerScheduler.Schedule()에서 PersonalityCostMultipliers.From(brain.Personality)로 생성해 주입.
    /// 모든 필드 기본값 0 → BuildActionDefs 내부에서 Identity(1f)로 자동 치환된다
    /// (default(PersonalityCostMultipliers)를 그대로 곱하면 비용 0 폭발 방지, ADR-P1).
    /// 반환 값은 항상 [PersonalityData.MULT_MIN, MULT_MAX] = [0.5, 2.0]으로 클램프됨.
    /// </summary>
    public struct PersonalityCostMultipliers
    {
        public float ChopWood;
        public float MineStone;
        public float MineIron;
        public float MineCopper;
        public float HarvestBerries;
        public float Explore;
        public float AttackEnemy;
        public float RestOnGround;

        public static PersonalityCostMultipliers Identity => new PersonalityCostMultipliers
        {
            ChopWood      = 1f,
            MineStone     = 1f,
            MineIron      = 1f,
            MineCopper    = 1f,
            HarvestBerries = 1f,
            Explore        = 1f,
            AttackEnemy    = 1f,
            RestOnGround   = 1f
        };

        /// <summary>
        /// Personality → 배율 테이블. §2 표 값 하드코딩. 모든 결과 필드는 [MULT_MIN, MULT_MAX] 클램프.
        /// Personality.None/Glutton은 Identity 반환 (Glutton은 배율 축이 아니라 임계값 축, ADR-P4).
        /// </summary>
        public static PersonalityCostMultipliers From(Personality p)
        {
            var m = Identity;
            switch (p)
            {
                case Personality.Coward:   m.AttackEnemy = 2.0f; break;
                case Personality.Brave:    m.AttackEnemy = 0.7f; break;
                case Personality.Diligent:
                    m.ChopWood       = 0.85f;
                    m.MineStone      = 0.85f;
                    m.MineIron       = 0.85f;
                    m.MineCopper     = 0.85f;
                    m.HarvestBerries = 0.85f;
                    break;
                case Personality.Lazy:
                    m.ChopWood       = 1.3f;
                    m.MineStone      = 1.3f;
                    m.MineIron       = 1.3f;
                    m.MineCopper     = 1.3f;
                    m.HarvestBerries = 1.3f;
                    break;
                case Personality.Curious:  m.Explore = 0.75f; break;
                // Personality.None / Personality.Glutton: Identity 유지
            }
            Clamp(ref m);
            return m;
        }

        /// <summary>모든 배율 필드를 [PersonalityData.MULT_MIN, MULT_MAX]로 클램프 (ADR-P1).</summary>
        private static void Clamp(ref PersonalityCostMultipliers m)
        {
            m.ChopWood       = UnityEngine.Mathf.Clamp(m.ChopWood,       PersonalityData.MULT_MIN, PersonalityData.MULT_MAX);
            m.MineStone      = UnityEngine.Mathf.Clamp(m.MineStone,      PersonalityData.MULT_MIN, PersonalityData.MULT_MAX);
            m.MineIron       = UnityEngine.Mathf.Clamp(m.MineIron,       PersonalityData.MULT_MIN, PersonalityData.MULT_MAX);
            m.MineCopper     = UnityEngine.Mathf.Clamp(m.MineCopper,     PersonalityData.MULT_MIN, PersonalityData.MULT_MAX);
            m.HarvestBerries = UnityEngine.Mathf.Clamp(m.HarvestBerries, PersonalityData.MULT_MIN, PersonalityData.MULT_MAX);
            m.Explore        = UnityEngine.Mathf.Clamp(m.Explore,        PersonalityData.MULT_MIN, PersonalityData.MULT_MAX);
            m.AttackEnemy    = UnityEngine.Mathf.Clamp(m.AttackEnemy,    PersonalityData.MULT_MIN, PersonalityData.MULT_MAX);
            m.RestOnGround   = UnityEngine.Mathf.Clamp(m.RestOnGround,   PersonalityData.MULT_MIN, PersonalityData.MULT_MAX);
        }
    }

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

        // 각 필드: S = Slot 인덱스, V = 요구 값, Op = 판정 연산자(0=Equal, 1=GreaterEq, 2=LessEq)
        // Op 기본값 0 → 기존 불리언 Prec와 동일하게 동작하므로 기존 액션 정의는 무수정.
        public int Prec0S; public int Prec0V; public int Prec0Op;
        public int Prec1S; public int Prec1V; public int Prec1Op;
        public int Prec2S; public int Prec2V; public int Prec2Op;
        public int Prec3S; public int Prec3V; public int Prec3Op;
        public int Prec4S; public int Prec4V; public int Prec4Op;
        public int Prec5S; public int Prec5V; public int Prec5Op;
        public int Prec6S; public int Prec6V; public int Prec6Op;
        public int Prec7S; public int Prec7V; public int Prec7Op;

        // ── Effect 목록 ──────────────────────────────────────────────────────
        // 최대 8개 (슬롯 인덱스, 값, 연산자) 3튜플.
        // EffectCount = 0이면 Effect 없음 (순수 이동 액션 등).
        // Op 기본값 0=Set → 기존 대입 동작과 동일.

        /// <summary>유효한 Effect 항목 수. 0~8.</summary>
        public int EffectCount;

        public int Eff0S; public int Eff0V; public int Eff0Op;
        public int Eff1S; public int Eff1V; public int Eff1Op;
        public int Eff2S; public int Eff2V; public int Eff2Op;
        public int Eff3S; public int Eff3V; public int Eff3Op;
        public int Eff4S; public int Eff4V; public int Eff4Op;
        public int Eff5S; public int Eff5V; public int Eff5Op;
        public int Eff6S; public int Eff6V; public int Eff6Op;
        public int Eff7S; public int Eff7V; public int Eff7Op;

        // ────────────────────────────────────────────────────────────────────
        // 헬퍼 메서드: Burst에서 호출 가능한 인스턴스 메서드 + 정적 연산 헬퍼
        // ────────────────────────────────────────────────────────────────────

        /// <summary>Precondition/Goal 판정 헬퍼. op=0:Equal, 1:GreaterEq, 2:LessEq.</summary>
        public static bool PrecHolds(int stateVal, int op, int reqVal)
        {
            if (op == 1) return stateVal >= reqVal; // GreaterEq
            if (op == 2) return stateVal <= reqVal; // LessEq
            return stateVal == reqVal;               // Equal (기본)
        }

        /// <summary>Effect 적용 헬퍼. op=0:Set, 1:Add, 2:Sub(음수 클램프 ADR-6).</summary>
        public static int ApplyEff(int stateVal, int op, int v)
        {
            if (op == 1) return stateVal + v;
            if (op == 2) { int r = stateVal - v; return r < 0 ? 0 : r; } // Sub + 클램프
            return v; // Set
        }

        /// <summary>
        /// 현재 월드 스테이트가 이 Action의 모든 Precondition을 충족하는지 확인한다.
        /// PrecCount가 0이면 항상 true를 반환한다.
        /// Op=0(기본)이면 기존 Equal 비교와 동일하게 동작한다 (레거시 호환).
        /// </summary>
        public bool CheckPreconditions(NativeArray<int> state)
        {
            switch (PrecCount)
            {
                case 0: return true;
                case 1: return PrecHolds(state[Prec0S], Prec0Op, Prec0V);
                case 2: return PrecHolds(state[Prec0S], Prec0Op, Prec0V)
                            && PrecHolds(state[Prec1S], Prec1Op, Prec1V);
                case 3: return PrecHolds(state[Prec0S], Prec0Op, Prec0V)
                            && PrecHolds(state[Prec1S], Prec1Op, Prec1V)
                            && PrecHolds(state[Prec2S], Prec2Op, Prec2V);
                case 4: return PrecHolds(state[Prec0S], Prec0Op, Prec0V)
                            && PrecHolds(state[Prec1S], Prec1Op, Prec1V)
                            && PrecHolds(state[Prec2S], Prec2Op, Prec2V)
                            && PrecHolds(state[Prec3S], Prec3Op, Prec3V);
                case 5: return PrecHolds(state[Prec0S], Prec0Op, Prec0V)
                            && PrecHolds(state[Prec1S], Prec1Op, Prec1V)
                            && PrecHolds(state[Prec2S], Prec2Op, Prec2V)
                            && PrecHolds(state[Prec3S], Prec3Op, Prec3V)
                            && PrecHolds(state[Prec4S], Prec4Op, Prec4V);
                case 6: return PrecHolds(state[Prec0S], Prec0Op, Prec0V)
                            && PrecHolds(state[Prec1S], Prec1Op, Prec1V)
                            && PrecHolds(state[Prec2S], Prec2Op, Prec2V)
                            && PrecHolds(state[Prec3S], Prec3Op, Prec3V)
                            && PrecHolds(state[Prec4S], Prec4Op, Prec4V)
                            && PrecHolds(state[Prec5S], Prec5Op, Prec5V);
                case 7: return PrecHolds(state[Prec0S], Prec0Op, Prec0V)
                            && PrecHolds(state[Prec1S], Prec1Op, Prec1V)
                            && PrecHolds(state[Prec2S], Prec2Op, Prec2V)
                            && PrecHolds(state[Prec3S], Prec3Op, Prec3V)
                            && PrecHolds(state[Prec4S], Prec4Op, Prec4V)
                            && PrecHolds(state[Prec5S], Prec5Op, Prec5V)
                            && PrecHolds(state[Prec6S], Prec6Op, Prec6V);
                case 8: return PrecHolds(state[Prec0S], Prec0Op, Prec0V)
                            && PrecHolds(state[Prec1S], Prec1Op, Prec1V)
                            && PrecHolds(state[Prec2S], Prec2Op, Prec2V)
                            && PrecHolds(state[Prec3S], Prec3Op, Prec3V)
                            && PrecHolds(state[Prec4S], Prec4Op, Prec4V)
                            && PrecHolds(state[Prec5S], Prec5Op, Prec5V)
                            && PrecHolds(state[Prec6S], Prec6Op, Prec6V)
                            && PrecHolds(state[Prec7S], Prec7Op, Prec7V);
                default:
                    return false;
            }
        }

        /// <summary>
        /// 이 Action의 Effect를 월드 스테이트에 적용한다.
        /// Op=0(Set, 기본)이면 기존 대입 동작과 동일. Op=1(Add), Op=2(Sub, 음수 클램프).
        /// </summary>
        public void ApplyEffects(NativeArray<int> state)
        {
            switch (EffectCount)
            {
                case 0: return;
                case 1:
                    state[Eff0S] = ApplyEff(state[Eff0S], Eff0Op, Eff0V);
                    return;
                case 2:
                    state[Eff0S] = ApplyEff(state[Eff0S], Eff0Op, Eff0V);
                    state[Eff1S] = ApplyEff(state[Eff1S], Eff1Op, Eff1V);
                    return;
                case 3:
                    state[Eff0S] = ApplyEff(state[Eff0S], Eff0Op, Eff0V);
                    state[Eff1S] = ApplyEff(state[Eff1S], Eff1Op, Eff1V);
                    state[Eff2S] = ApplyEff(state[Eff2S], Eff2Op, Eff2V);
                    return;
                case 4:
                    state[Eff0S] = ApplyEff(state[Eff0S], Eff0Op, Eff0V);
                    state[Eff1S] = ApplyEff(state[Eff1S], Eff1Op, Eff1V);
                    state[Eff2S] = ApplyEff(state[Eff2S], Eff2Op, Eff2V);
                    state[Eff3S] = ApplyEff(state[Eff3S], Eff3Op, Eff3V);
                    return;
                case 5:
                    state[Eff0S] = ApplyEff(state[Eff0S], Eff0Op, Eff0V);
                    state[Eff1S] = ApplyEff(state[Eff1S], Eff1Op, Eff1V);
                    state[Eff2S] = ApplyEff(state[Eff2S], Eff2Op, Eff2V);
                    state[Eff3S] = ApplyEff(state[Eff3S], Eff3Op, Eff3V);
                    state[Eff4S] = ApplyEff(state[Eff4S], Eff4Op, Eff4V);
                    return;
                case 6:
                    state[Eff0S] = ApplyEff(state[Eff0S], Eff0Op, Eff0V);
                    state[Eff1S] = ApplyEff(state[Eff1S], Eff1Op, Eff1V);
                    state[Eff2S] = ApplyEff(state[Eff2S], Eff2Op, Eff2V);
                    state[Eff3S] = ApplyEff(state[Eff3S], Eff3Op, Eff3V);
                    state[Eff4S] = ApplyEff(state[Eff4S], Eff4Op, Eff4V);
                    state[Eff5S] = ApplyEff(state[Eff5S], Eff5Op, Eff5V);
                    return;
                case 7:
                    state[Eff0S] = ApplyEff(state[Eff0S], Eff0Op, Eff0V);
                    state[Eff1S] = ApplyEff(state[Eff1S], Eff1Op, Eff1V);
                    state[Eff2S] = ApplyEff(state[Eff2S], Eff2Op, Eff2V);
                    state[Eff3S] = ApplyEff(state[Eff3S], Eff3Op, Eff3V);
                    state[Eff4S] = ApplyEff(state[Eff4S], Eff4Op, Eff4V);
                    state[Eff5S] = ApplyEff(state[Eff5S], Eff5Op, Eff5V);
                    state[Eff6S] = ApplyEff(state[Eff6S], Eff6Op, Eff6V);
                    return;
                case 8:
                    state[Eff0S] = ApplyEff(state[Eff0S], Eff0Op, Eff0V);
                    state[Eff1S] = ApplyEff(state[Eff1S], Eff1Op, Eff1V);
                    state[Eff2S] = ApplyEff(state[Eff2S], Eff2Op, Eff2V);
                    state[Eff3S] = ApplyEff(state[Eff3S], Eff3Op, Eff3V);
                    state[Eff4S] = ApplyEff(state[Eff4S], Eff4Op, Eff4V);
                    state[Eff5S] = ApplyEff(state[Eff5S], Eff5Op, Eff5V);
                    state[Eff6S] = ApplyEff(state[Eff6S], Eff6Op, Eff6V);
                    state[Eff7S] = ApplyEff(state[Eff7S], Eff7Op, Eff7V);
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
        // ── [Phase 2] 플래너-런타임 공유 단일 출처 상수 (ADR-7) ──────────────
        // 플래너 Effect와 런타임 Commit이 동일 수치를 참조한다.
        // 런타임 적용부(VillagerFSM.OnActionCompleted)가 이 상수를 import하여 정합성을 보장한다.

        public const int YIELD_CHOP_WOOD       = 10;  // ChopWood 1회 수확량 (기획서 수치 — VillagerFSM WoodStock += 10f)
        public const int YIELD_MINE_STONE      = 8;   // MineStone 1회 수확량 (기획서 수치 — VillagerFSM StoneStock += 8f)
        public const int YIELD_MINE_IRON       = 5;   // MineIron 1회 수확량 (기획서 수치 — VillagerFSM IronStock += 5f)
        public const int YIELD_MINE_COPPER     = 3;   // MineCopper 1회 수확량
        public const int YIELD_HARVEST_BERRIES = 5;   // HarvestWildBerries 1회 수확량 (기획서 수치 — ActionDatabase rawFood += 5)
        public const int COOK_RAW_CONSUME      = 2;   // CookMeal 소비 생 식량
        public const int COOK_YIELD            = 2;   // CookMeal 산출 조리 식량 (기획서 수치 — VillagerFSM cookedFood += 2f)
        public const int EAT_HUNGER_RELIEF     = 50;  // EatCookedFood 포만감 증가량 (배고픔 해소량 = SatietyLevel gain)
        public const int EAT_RAW_RELIEF        = 15;  // EatRawFood 포만감 증가량
        public const int SLEEP_FATIGUE_RELIEF  = 90;  // Sleep 피로 회복량 (VillagerFSM.SLEEP_FATIGUE_RECOVERY 90f와 일치)
        public const int REST_FATIGUE_RELIEF   = 20;  // RestOnGround 피로 회복량 (FSM REST_ON_GROUND_FATIGUE_RECOVERY와 일치)
        public const int MEDICAL_HEALTH_GAIN   = 40;  // SeekMedicalAid 체력 회복량 (ActionDatabase GainHealth 40f와 일치)

        // ── 건물 건설 비용 (BuildingCosts.cs 수치와 동기화 — 단일 출처 ADR-7) ─
        public const int BUILD_CAMPFIRE_WOOD    = 5;
        public const int BUILD_HOUSE_WOOD       = 20;  // BuildingCosts.HOUSE_WOOD
        public const int BUILD_HOUSE_STONE      = 10;  // BuildingCosts.HOUSE_STONE
        public const int BUILD_WATCHTOWER_WOOD  = 10;  // BuildingCosts.WATCHTOWER_WOOD
        public const int BUILD_WATCHTOWER_STONE = 30;  // BuildingCosts.WATCHTOWER_STONE
        public const int BUILD_WATCHTOWER_IRON  = 5;   // BuildingCosts.WATCHTOWER_IRON
        public const int BUILD_STOREHOUSE_WOOD  = 15;
        public const int BUILD_STOREHOUSE_STONE = 5;
        public const int BUILD_TOWNHALL_WOOD   = 35;
        public const int BUILD_TOWNHALL_STONE  = 30;
        public const int BUILD_TOWNHALL_IRON   = 6;
        public const int BUILD_FORGE_WOOD      = 20;
        public const int BUILD_FORGE_STONE     = 20;
        public const int BUILD_FORGE_IRON      = 15;
        public const int CRAFT_WEAPON_IRON     = 6;   // CraftWeapon 철 소비량 (HasIronForBuilding 임계값과 일치)

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

            // [Phase 2] 수치 슬롯 alias
            public const int WoodStock       = GOAPPlanningSlots.WoodStock;
            public const int StoneStock      = GOAPPlanningSlots.StoneStock;
            public const int IronStock       = GOAPPlanningSlots.IronStock;
            public const int CopperStock     = GOAPPlanningSlots.CopperStock;
            public const int RawFoodStock    = GOAPPlanningSlots.RawFoodStock;
            public const int CookedFoodStock = GOAPPlanningSlots.CookedFoodStock;
            public const int MySatiety       = GOAPPlanningSlots.MySatiety;
            public const int MyFatigue       = GOAPPlanningSlots.MyFatigue;
            public const int MyHealth        = GOAPPlanningSlots.MyHealth;
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
        /// 22개 Action 이름 배열. BuildActionDefs의 액션 목록과 반드시 동기화된다.
        /// [C3 T15] 액션 추가 3종 세트(ADR-8) 정합성 검증의 단일 출처.
        /// </summary>
        private static readonly string[] _actionNames = new string[]
        {
            "ChopWood", "MineStone", "MineIron", "MineCopper",
            "EatCookedFood", "EatRawFood", "CookMeal", "Sleep",
            "RestOnGround", "SeekMedicalAid", "MoveToBase",
            "CraftPrimitiveWeapon", "AttackEnemy", "CraftWeapon",
            "BuildTownHall", "BuildForge", "BuildStorehouse",
            "Explore", "HarvestWildBerries", "BuildCampfire",
            "BuildHouse", "BuildWatchtower"
        };

        /// <summary>
        /// [C3 T15] EditMode 테스트 전용: _actionNames 배열의 읽기 전용 접근자.
        /// 배열 원본은 캡슐화 유지를 위해 사본을 반환한다.
        /// </summary>
        internal static string[] GetActionNamesForTest()
        {
            var copy = new string[_actionNames.Length];
            System.Array.Copy(_actionNames, copy, _actionNames.Length);
            return copy;
        }

        /// <summary>
        /// 정적 생성자: 클래스가 처음 참조될 때 한 번만 실행된다.
        /// 22개 Action 이름을 해시로 변환하여 딕셔너리에 등록한다.
        /// 해시 충돌이 감지되면 즉시 LogError로 보고한다.
        /// </summary>
        static GOAPActionRegistry()
        {
            _hashToId = new Dictionary<int, string>(_actionNames.Length);
            foreach (var name in _actionNames)
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
            float seasonGatherModifier = 1.0f,
            ContextCostMultipliers contextMult = default,
            PersonalityCostMultipliers personalityMult = default)
        {
            // 각 필드가 0이면(default 미설정) 1f로 치환 — 부분 설정 struct도 안전하게 처리
            if (contextMult.ChopWood       <= 0f) contextMult.ChopWood       = 1f;
            if (contextMult.MineStone      <= 0f) contextMult.MineStone      = 1f;
            if (contextMult.MineIron       <= 0f) contextMult.MineIron       = 1f;
            if (contextMult.MineCopper     <= 0f) contextMult.MineCopper     = 1f;
            if (contextMult.HarvestBerries <= 0f) contextMult.HarvestBerries = 1f;
            if (contextMult.Explore        <= 0f) contextMult.Explore        = 1f;
            if (contextMult.AttackEnemy    <= 0f) contextMult.AttackEnemy    = 1f;
            if (contextMult.RestOnGround   <= 0f) contextMult.RestOnGround   = 1f;

            // F-A: 성격 배율도 default 감지 시 Identity 치환 (ADR-P1 오해 위험 방어).
            // default(PersonalityCostMultipliers)의 모든 float=0f를 그대로 곱하면 비용이 0이 되어
            // 플래너 무한 확장.
            if (personalityMult.ChopWood       <= 0f) personalityMult.ChopWood       = 1f;
            if (personalityMult.MineStone      <= 0f) personalityMult.MineStone      = 1f;
            if (personalityMult.MineIron       <= 0f) personalityMult.MineIron       = 1f;
            if (personalityMult.MineCopper     <= 0f) personalityMult.MineCopper     = 1f;
            if (personalityMult.HarvestBerries <= 0f) personalityMult.HarvestBerries = 1f;
            if (personalityMult.Explore        <= 0f) personalityMult.Explore        = 1f;
            if (personalityMult.AttackEnemy    <= 0f) personalityMult.AttackEnemy    = 1f;
            if (personalityMult.RestOnGround   <= 0f) personalityMult.RestOnGround   = 1f;

            // 기획서 수치 — 22개 Action 기본 비용 (GDD v0.4 기준 + BuildHouse/BuildWatchtower)
            var defs = new NativeArray<GOAPActionDef>(22, allocator);
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
            chopCost *= contextMult.ChopWood;
            chopCost *= personalityMult.ChopWood;
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("ChopWood"),
                BaseCost = chopCost,
                PrecCount = 2, Prec0S = S.NearDiscoveredResource, Prec0V = 1, Prec1S = S.HasTool, Prec1V = 1,
                EffectCount = 3, Eff0S = S.ResourcesGathered, Eff0V = 1, Eff1S = S.WoodLow, Eff1V = 0,
                Eff2S = S.WoodStock, Eff2Op = 1, Eff2V = YIELD_CHOP_WOOD // Add
            };

            // ── MineStone (돌 채광) ───────────────────────────────────────────
            // 기획서 수치: BaseCost=12, Miner x0.5, Lumberjack x1.3
            // Preconditions: NearDiscoveredResource=1, HasTool=1
            // Effects: ResourcesGathered=1, StoneLow=0
            float mineStoneCost = 12f;
            if (role == AgentRole.Miner)           mineStoneCost *= MINER_MINE_MODIFIER;
            else if (role == AgentRole.Lumberjack) mineStoneCost *= LUMBERJACK_MINE_MODIFIER;
            mineStoneCost *= seasonGatherModifier;
            mineStoneCost *= contextMult.MineStone;
            mineStoneCost *= personalityMult.MineStone;
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("MineStone"),
                BaseCost = mineStoneCost,
                PrecCount = 2, Prec0S = S.NearDiscoveredResource, Prec0V = 1, Prec1S = S.HasTool, Prec1V = 1,
                EffectCount = 3, Eff0S = S.ResourcesGathered, Eff0V = 1, Eff1S = S.StoneLow, Eff1V = 0,
                Eff2S = S.StoneStock, Eff2Op = 1, Eff2V = YIELD_MINE_STONE // Add
            };

            // ── MineIron (철 채광) ────────────────────────────────────────────
            // 기획서 수치: BaseCost=15, Miner x0.5
            // Preconditions: NearDiscoveredResource=1, HasTool=1
            // Effects: ResourcesGathered=1, IronLow=0
            float mineIronCost = 15f;
            if (role == AgentRole.Miner) mineIronCost *= MINER_MINE_MODIFIER;
            mineIronCost *= seasonGatherModifier;
            mineIronCost *= contextMult.MineIron;
            mineIronCost *= personalityMult.MineIron;
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("MineIron"),
                BaseCost = mineIronCost,
                PrecCount = 2, Prec0S = S.NearDiscoveredResource, Prec0V = 1, Prec1S = S.HasTool, Prec1V = 1,
                EffectCount = 3, Eff0S = S.ResourcesGathered, Eff0V = 1, Eff1S = S.IronLow, Eff1V = 0,
                Eff2S = S.IronStock, Eff2Op = 1, Eff2V = YIELD_MINE_IRON // Add
            };

            // ── MineCopper (구리 채광) ────────────────────────────────────────
            // 기획서 수치: BaseCost=16, Miner x0.5
            // Preconditions: NearDiscoveredResource=1, HasTool=1
            // Effects: ResourcesGathered=1, CopperLow=0
            float mineCopperCost = 16f;
            if (role == AgentRole.Miner) mineCopperCost *= MINER_MINE_MODIFIER;
            mineCopperCost *= seasonGatherModifier;
            mineCopperCost *= contextMult.MineCopper;
            mineCopperCost *= personalityMult.MineCopper;
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("MineCopper"),
                BaseCost = mineCopperCost,
                PrecCount = 2, Prec0S = S.NearDiscoveredResource, Prec0V = 1, Prec1S = S.HasTool, Prec1V = 1,
                EffectCount = 3, Eff0S = S.ResourcesGathered, Eff0V = 1, Eff1S = S.CopperLow, Eff1V = 0,
                Eff2S = S.CopperStock, Eff2Op = 1, Eff2V = YIELD_MINE_COPPER // Add
            };

            // ── EatCookedFood (조리된 음식 섭취) ─────────────────────────────
            // Preconditions: HasCookedFood=1, [P2] CookedFoodStock GreaterEq 1
            // Effects: HungerSolved=1, HungerCritical=0, [P2] CookedFoodStock Sub 1, MySatiety Add EAT_HUNGER_RELIEF
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("EatCookedFood"),
                BaseCost = 5f,
                PrecCount = 2,
                Prec0S = S.HasCookedFood, Prec0V = 1,
                Prec1S = S.CookedFoodStock, Prec1V = 1, Prec1Op = 1, // GreaterEq
                EffectCount = 4,
                Eff0S = S.HungerSolved, Eff0V = 1,
                Eff1S = S.HungerCritical, Eff1V = 0,
                Eff2S = S.CookedFoodStock, Eff2Op = 2, Eff2V = 1, // Sub 1
                Eff3S = S.MySatiety, Eff3Op = 1, Eff3V = EAT_HUNGER_RELIEF // Add
            };

            // ── EatRawFood (생 음식 섭취) ─────────────────────────────────────
            // Preconditions: HasRawFood=1, [P2] RawFoodStock GreaterEq 1
            // Effects: HungerSolved=1, HungerCritical=0, [P2] RawFoodStock Sub 1, MySatiety Add EAT_RAW_RELIEF
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("EatRawFood"),
                BaseCost = 8f,
                PrecCount = 2,
                Prec0S = S.HasRawFood, Prec0V = 1,
                Prec1S = S.RawFoodStock, Prec1V = 1, Prec1Op = 1, // GreaterEq
                EffectCount = 4,
                Eff0S = S.HungerSolved, Eff0V = 1,
                Eff1S = S.HungerCritical, Eff1V = 0,
                Eff2S = S.RawFoodStock, Eff2Op = 2, Eff2V = 1, // Sub 1
                Eff3S = S.MySatiety, Eff3Op = 1, Eff3V = EAT_RAW_RELIEF // Add
            };

            // ── CookMeal (음식 조리) ──────────────────────────────────────────
            // Preconditions: HasRawFood=1, NearFireplace=1, [P2] RawFoodStock GreaterEq COOK_RAW_CONSUME
            // Effects: HasCookedFood=1, MealCooked=1, [P2] RawFoodStock Sub COOK_RAW_CONSUME, CookedFoodStock Add COOK_YIELD
            float cookCost = 6f;
            if (role == AgentRole.Cook) cookCost *= COOK_COOK_MODIFIER;
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("CookMeal"),
                BaseCost = cookCost,
                PrecCount = 3,
                Prec0S = S.HasRawFood, Prec0V = 1,
                Prec1S = S.NearFireplace, Prec1V = 1,
                Prec2S = S.RawFoodStock, Prec2V = COOK_RAW_CONSUME, Prec2Op = 1, // GreaterEq
                EffectCount = 4,
                Eff0S = S.HasCookedFood, Eff0V = 1,
                Eff1S = S.MealCooked, Eff1V = 1,
                Eff2S = S.RawFoodStock, Eff2Op = 2, Eff2V = COOK_RAW_CONSUME, // Sub
                Eff3S = S.CookedFoodStock, Eff3Op = 1, Eff3V = COOK_YIELD // Add
            };

            // ── Sleep (수면 회복) ─────────────────────────────────────────────
            // Preconditions: NearBed=1
            // Effects: FatigueSolved=1, FatigueCritical=0, [P2] MyFatigue Sub SLEEP_FATIGUE_RELIEF
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("Sleep"),
                BaseCost = 5f,
                PrecCount = 1, Prec0S = S.NearBed, Prec0V = 1,
                EffectCount = 3,
                Eff0S = S.FatigueSolved, Eff0V = 1,
                Eff1S = S.FatigueCritical, Eff1V = 0,
                Eff2S = S.MyFatigue, Eff2Op = 2, Eff2V = SLEEP_FATIGUE_RELIEF // Sub
            };

            // ── RestOnGround (땅에서 쉬기) ────────────────────────────────────
            // Preconditions: 없음 (폴백 액션)
            // Effects: FatigueSolved=1, FatigueCritical=0, [P2] MyFatigue Sub REST_FATIGUE_RELIEF
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("RestOnGround"),
                BaseCost = 12f * contextMult.RestOnGround * personalityMult.RestOnGround,
                PrecCount = 0,
                EffectCount = 3,
                Eff0S = S.FatigueSolved, Eff0V = 1,
                Eff1S = S.FatigueCritical, Eff1V = 0,
                Eff2S = S.MyFatigue, Eff2Op = 2, Eff2V = REST_FATIGUE_RELIEF // Sub
            };

            // ── SeekMedicalAid (의료 치료) ────────────────────────────────────
            // Preconditions: NearHealer=1
            // Effects: InjurySolved=1, InjuryCritical=0, [P2] MyHealth Add MEDICAL_HEALTH_GAIN
            float healCost = 4f;
            if (role == AgentRole.Medic) healCost *= MEDIC_HEAL_MODIFIER;
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("SeekMedicalAid"),
                BaseCost = healCost,
                PrecCount = 1, Prec0S = S.NearHealer, Prec0V = 1,
                EffectCount = 3,
                Eff0S = S.InjurySolved, Eff0V = 1,
                Eff1S = S.InjuryCritical, Eff1V = 0,
                Eff2S = S.MyHealth, Eff2Op = 1, Eff2V = MEDICAL_HEALTH_GAIN // Add (상한 100 클램프는 런타임 몫)
            };

            // ── MoveToBase (기지 귀환) ────────────────────────────────────────
            // 기획서 수치: BaseCost=3, 역할 보정 없음
            // Preconditions: AtBase=0 — 이미 기지에 있으면 적용 불가 (A* 무한 체인 방지)
            // Effects: AtBase=1, NearStorage=1
            // [Fix] Phase 1 컨텍스트 비용 배율로 목표 경로 비용이 올라간 상태에서
            //       MoveToBase가 전제조건 없이 반복 확장되어 MAX_NODES를 소진하는 버그 수정.
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("MoveToBase"),
                BaseCost = 3f,
                PrecCount = 1, Prec0S = S.AtBase, Prec0V = 0,
                EffectCount = 2, Eff0S = S.AtBase, Eff0V = 1, Eff1S = S.NearStorage, Eff1V = 1
            };

            // ── CraftPrimitiveWeapon (원시 무기 제작) ─────────────────────────
            // 기획서 수치: BaseCost=6, 역할 보정 없음
            // Preconditions: HasPrimitiveWeapon=0 — 이미 제작됐으면 불필요 (A* 무한 체인 방지)
            // Effects: HasPrimitiveWeapon=1
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("CraftPrimitiveWeapon"),
                BaseCost = 6f,
                PrecCount = 1, Prec0S = S.HasPrimitiveWeapon, Prec0V = 0,
                EffectCount = 1, Eff0S = S.HasPrimitiveWeapon, Eff0V = 1
            };

            // ── AttackEnemy (적 공격) ─────────────────────────────────────────
            // 기획서 수치: BaseCost=10, Warrior x0.6, Cook x1.8
            // Preconditions: NearEnemy=1, HasPrimitiveWeapon=1
            // Effects: EnemyDefeated=1, NearEnemy=0
            float attackCost = 10f;
            if (role == AgentRole.Warrior)      attackCost *= WARRIOR_ATTACK_MODIFIER;
            else if (role == AgentRole.Cook)    attackCost *= COOK_ATTACK_MODIFIER;
            attackCost *= contextMult.AttackEnemy;
            attackCost *= personalityMult.AttackEnemy;
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("AttackEnemy"),
                BaseCost = attackCost,
                PrecCount = 2, Prec0S = S.NearEnemy, Prec0V = 1, Prec1S = S.HasPrimitiveWeapon, Prec1V = 1,
                EffectCount = 2, Eff0S = S.EnemyDefeated, Eff0V = 1, Eff1S = S.NearEnemy, Eff1V = 0
            };

            // ── CraftWeapon (제련 무기 제작) ──────────────────────────────────
            // Preconditions: ForgeBuilt=1, HasIronForBuilding=1, [P2] IronStock GreaterEq CRAFT_WEAPON_IRON
            // Effects: HasWeapon=1, [P2] IronStock Sub CRAFT_WEAPON_IRON
            float craftWeaponCost = 15f;
            if (role == AgentRole.Warrior) craftWeaponCost *= WARRIOR_CRAFT_MODIFIER;
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("CraftWeapon"),
                BaseCost = craftWeaponCost,
                PrecCount = 3,
                Prec0S = S.ForgeBuilt, Prec0V = 1,
                Prec1S = S.HasIronForBuilding, Prec1V = 1,
                Prec2S = S.IronStock, Prec2V = CRAFT_WEAPON_IRON, Prec2Op = 1, // GreaterEq
                EffectCount = 2,
                Eff0S = S.HasWeapon, Eff0V = 1,
                Eff1S = S.IronStock, Eff1Op = 2, Eff1V = CRAFT_WEAPON_IRON // Sub
            };

            // ── BuildTownHall (Town Hall 건설) ────────────────────────────────
            // Preconditions: BuildingQueued=1, HasWoodForBuilding=1, HasStoneForBuilding=1,
            //                [P2] WoodStock GreaterEq 35, StoneStock GreaterEq 30, IronStock GreaterEq 6
            // Effects: StructureBuilt=1, TownHallBuilt=1, BuildingQueued=0,
            //          [P2] WoodStock Sub 35, StoneStock Sub 30, IronStock Sub 6
            float buildTHCost = 50f;
            if (role == AgentRole.Builder) buildTHCost *= BUILDER_BUILD_MODIFIER;
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("BuildTownHall"),
                BaseCost = buildTHCost,
                PrecCount = 6,
                Prec0S = S.BuildingQueued,      Prec0V = 1,
                Prec1S = S.HasWoodForBuilding,  Prec1V = 1,
                Prec2S = S.HasStoneForBuilding, Prec2V = 1,
                Prec3S = S.WoodStock,  Prec3V = BUILD_TOWNHALL_WOOD,  Prec3Op = 1, // GreaterEq
                Prec4S = S.StoneStock, Prec4V = BUILD_TOWNHALL_STONE, Prec4Op = 1, // GreaterEq
                Prec5S = S.IronStock,  Prec5V = BUILD_TOWNHALL_IRON,  Prec5Op = 1, // GreaterEq
                EffectCount = 6,
                Eff0S = S.StructureBuilt,                Eff0V = 1,
                Eff1S = GOAPPlanningSlots.TownHallBuilt, Eff1V = 1,
                Eff2S = S.BuildingQueued,                Eff2V = 0,
                Eff3S = S.WoodStock,  Eff3Op = 2, Eff3V = BUILD_TOWNHALL_WOOD,  // Sub
                Eff4S = S.StoneStock, Eff4Op = 2, Eff4V = BUILD_TOWNHALL_STONE, // Sub
                Eff5S = S.IronStock,  Eff5Op = 2, Eff5V = BUILD_TOWNHALL_IRON   // Sub
            };

            // ── BuildForge (Forge 건설) ───────────────────────────────────────
            // Preconditions: BuildingQueued=1, HasWoodForBuilding=1, HasStoneForBuilding=1,
            //                [P2] WoodStock GreaterEq 20, StoneStock GreaterEq 20, IronStock GreaterEq 15
            // Effects: StructureBuilt=1, ForgeBuilt=1, BuildingQueued=0,
            //          [P2] WoodStock Sub 20, StoneStock Sub 20, IronStock Sub 15
            float buildForgeCost = 40f;
            if (role == AgentRole.Builder) buildForgeCost *= BUILDER_BUILD_MODIFIER;
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("BuildForge"),
                BaseCost = buildForgeCost,
                PrecCount = 6,
                Prec0S = S.BuildingQueued,      Prec0V = 1,
                Prec1S = S.HasWoodForBuilding,  Prec1V = 1,
                Prec2S = S.HasStoneForBuilding, Prec2V = 1,
                Prec3S = S.WoodStock,  Prec3V = BUILD_FORGE_WOOD,  Prec3Op = 1, // GreaterEq
                Prec4S = S.StoneStock, Prec4V = BUILD_FORGE_STONE, Prec4Op = 1, // GreaterEq
                Prec5S = S.IronStock,  Prec5V = BUILD_FORGE_IRON,  Prec5Op = 1, // GreaterEq
                EffectCount = 6,
                Eff0S = S.StructureBuilt, Eff0V = 1,
                Eff1S = S.ForgeBuilt,     Eff1V = 1,
                Eff2S = S.BuildingQueued, Eff2V = 0,
                Eff3S = S.WoodStock,  Eff3Op = 2, Eff3V = BUILD_FORGE_WOOD,  // Sub
                Eff4S = S.StoneStock, Eff4Op = 2, Eff4V = BUILD_FORGE_STONE, // Sub
                Eff5S = S.IronStock,  Eff5Op = 2, Eff5V = BUILD_FORGE_IRON   // Sub
            };

            // ── BuildStorehouse (Storehouse 건설) ─────────────────────────────
            // Preconditions: BuildingQueued=1, [P2] WoodStock GreaterEq 15, StoneStock GreaterEq 5
            // [P4] HasWoodForBuilding(임계값 35)·HasStoneForBuilding(30) 불리언 게이트 제거 —
            //      수치 Prec(15/5)이 정확한 게이트이므로 과도한 불리언 게이트가 불필요하게 차단했음.
            // Effects: StructureBuilt=1, StorehouseBuilt=1, BuildingQueued=0,
            //          [P2] WoodStock Sub 15, StoneStock Sub 5
            float buildStoreCost = 35f;
            if (role == AgentRole.Builder) buildStoreCost *= BUILDER_BUILD_MODIFIER;
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("BuildStorehouse"),
                BaseCost = buildStoreCost,
                PrecCount = 3,
                Prec0S = S.BuildingQueued, Prec0V = 1,
                Prec1S = S.WoodStock,  Prec1V = BUILD_STOREHOUSE_WOOD,  Prec1Op = 1, // GreaterEq
                Prec2S = S.StoneStock, Prec2V = BUILD_STOREHOUSE_STONE, Prec2Op = 1, // GreaterEq
                EffectCount = 5,
                Eff0S = S.StructureBuilt,                  Eff0V = 1,
                Eff1S = GOAPPlanningSlots.StorehouseBuilt, Eff1V = 1,
                Eff2S = S.BuildingQueued,                  Eff2V = 0,
                Eff3S = S.WoodStock,  Eff3Op = 2, Eff3V = BUILD_STOREHOUSE_WOOD,  // Sub
                Eff4S = S.StoneStock, Eff4Op = 2, Eff4V = BUILD_STOREHOUSE_STONE  // Sub
            };

            // ── Explore (탐험) ────────────────────────────────────────────────
            // 기획서 수치: BaseCost=15, 역할 보정 없음
            // Preconditions: 없음
            // Effects: AreaExplored=1, NearDiscoveredResource=1
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("Explore"),
                BaseCost = 15f * contextMult.Explore * personalityMult.Explore,
                PrecCount = 0,
                EffectCount = 2, Eff0S = S.AreaExplored, Eff0V = 1, Eff1S = S.NearDiscoveredResource, Eff1V = 1
            };

            // ── HarvestWildBerries (야생 열매 채집) ───────────────────────────
            // Preconditions: NearDiscoveredResource=1
            // Effects: ResourcesGathered=1, HasRawFood=1, RawFoodLow=0, [P2] RawFoodStock Add YIELD_HARVEST_BERRIES
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("HarvestWildBerries"),
                BaseCost = 10f * contextMult.HarvestBerries * personalityMult.HarvestBerries,
                PrecCount = 1, Prec0S = S.NearDiscoveredResource, Prec0V = 1,
                EffectCount = 4,
                Eff0S = S.ResourcesGathered,          Eff0V = 1,
                Eff1S = S.HasRawFood,                 Eff1V = 1,
                Eff2S = GOAPPlanningSlots.RawFoodLow, Eff2V = 0,
                Eff3S = S.RawFoodStock, Eff3Op = 1, Eff3V = YIELD_HARVEST_BERRIES // Add
            };

            // ── BuildCampfire (모닥불 건설) ───────────────────────────────────
            // Preconditions: BuildingQueued=1, [P2] WoodStock GreaterEq 5
            // [P4] HasWoodForBuilding(임계값 35) 불리언 게이트 제거 — 수치 Prec(5)이 정확한 게이트.
            // Effects: StructureBuilt=1, BuildingQueued=0, [P2] WoodStock Sub 5
            float buildCampfireCost = 20f;
            if (role == AgentRole.Builder) buildCampfireCost *= BUILDER_BUILD_MODIFIER;
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("BuildCampfire"),
                BaseCost = buildCampfireCost,
                PrecCount = 2,
                Prec0S = S.BuildingQueued, Prec0V = 1,
                Prec1S = S.WoodStock, Prec1V = BUILD_CAMPFIRE_WOOD, Prec1Op = 1, // GreaterEq
                EffectCount = 3,
                Eff0S = S.StructureBuilt, Eff0V = 1,
                Eff1S = S.BuildingQueued, Eff1V = 0,
                Eff2S = S.WoodStock, Eff2Op = 2, Eff2V = BUILD_CAMPFIRE_WOOD // Sub
            };

            // ── BuildHouse (집 건설) ──────────────────────────────────────────
            // Preconditions: BuildingQueued=1, WoodStock GreaterEq 20, StoneStock GreaterEq 10
            // [P4] 불리언 게이트(HasWoodForBuilding 임계값 35) 미사용 — 수치 Prec이 정확한 게이트.
            // Effects: StructureBuilt=1, BuildingQueued=0, WoodStock Sub 20, StoneStock Sub 10
            float buildHouseCost = 30f;
            if (role == AgentRole.Builder) buildHouseCost *= BUILDER_BUILD_MODIFIER;
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("BuildHouse"),
                BaseCost = buildHouseCost,
                PrecCount = 3,
                Prec0S = S.BuildingQueued, Prec0V = 1,
                Prec1S = S.WoodStock,  Prec1V = BUILD_HOUSE_WOOD,  Prec1Op = 1, // GreaterEq
                Prec2S = S.StoneStock, Prec2V = BUILD_HOUSE_STONE, Prec2Op = 1,
                EffectCount = 4,
                Eff0S = S.StructureBuilt, Eff0V = 1,
                Eff1S = S.BuildingQueued, Eff1V = 0,
                Eff2S = S.WoodStock,  Eff2Op = 2, Eff2V = BUILD_HOUSE_WOOD,  // Sub
                Eff3S = S.StoneStock, Eff3Op = 2, Eff3V = BUILD_HOUSE_STONE  // Sub
            };

            // ── BuildWatchtower (망루 건설) ───────────────────────────────────
            // Preconditions: BuildingQueued=1, WoodStock GreaterEq 10, StoneStock GreaterEq 30, IronStock GreaterEq 5
            // [철 Prec 필수] BuildingCosts: 목재 10 / 석재 30 / 철 5
            // Effects: StructureBuilt=1, BuildingQueued=0, WoodStock Sub 10, StoneStock Sub 30, IronStock Sub 5
            float buildWatchtowerCost = 35f;
            if (role == AgentRole.Builder) buildWatchtowerCost *= BUILDER_BUILD_MODIFIER;
            defs[i++] = new GOAPActionDef
            {
                ActionStringHash = Animator.StringToHash("BuildWatchtower"),
                BaseCost = buildWatchtowerCost,
                PrecCount = 4,
                Prec0S = S.BuildingQueued, Prec0V = 1,
                Prec1S = S.WoodStock,  Prec1V = BUILD_WATCHTOWER_WOOD,  Prec1Op = 1, // GreaterEq
                Prec2S = S.StoneStock, Prec2V = BUILD_WATCHTOWER_STONE, Prec2Op = 1,
                Prec3S = S.IronStock,  Prec3V = BUILD_WATCHTOWER_IRON,  Prec3Op = 1,
                EffectCount = 5,
                Eff0S = S.StructureBuilt, Eff0V = 1,
                Eff1S = S.BuildingQueued, Eff1V = 0,
                Eff2S = S.WoodStock,  Eff2Op = 2, Eff2V = BUILD_WATCHTOWER_WOOD,  // Sub
                Eff3S = S.StoneStock, Eff3Op = 2, Eff3V = BUILD_WATCHTOWER_STONE, // Sub
                Eff4S = S.IronStock,  Eff4Op = 2, Eff4V = BUILD_WATCHTOWER_IRON   // Sub
            };

            // 배열 크기와 실제 정의 수 검증 (개발 중 Action 추가/삭제 감지용)
            Debug.Assert(
                i == 22,
                $"[GOAPActionRegistry] Action 정의 수 불일치: 예상 22, 실제 {i}."
            );

            return defs;
        }

        /// <summary>
        /// [S2] BuildActionDefs() 결과 배열을 1회 스캔해 슬롯별 최대 Add/Sub 효과량을 산출한다.
        ///
        /// MaxGain[s]: 슬롯 s를 가장 크게 올리는 Add(op=1) 효과 절대값. 없으면 1.
        /// MaxDrop[s]: 슬롯 s를 가장 크게 내리는 Sub(op=2) 효과 절대값. 없으면 1.
        /// Set(op=0) 효과는 불리언 슬롯(Equal Goal)에만 쓰이며, Equal은 h=1로 고정이므로 제외한다.
        ///
        /// 반환 배열은 호출자(GOAPPlannerScheduler)가 Dispose해야 한다.
        /// </summary>
        public static void BuildMaxGainDrop(
            NativeArray<GOAPActionDef> defs,
            int totalSlots,
            Allocator alloc,
            out NativeArray<float> maxGain,
            out NativeArray<float> maxDrop)
        {
            maxGain = new NativeArray<float>(totalSlots, alloc);
            maxDrop = new NativeArray<float>(totalSlots, alloc);

            for (int a = 0; a < defs.Length; a++)
            {
                GOAPActionDef def = defs[a];
                for (int e = 0; e < def.EffectCount; e++)
                {
                    GetEffect(def, e, out int slot, out int op, out int val);
                    if (op == 1 && (float)val > maxGain[slot]) maxGain[slot] = (float)val; // Add
                    if (op == 2 && (float)val > maxDrop[slot]) maxDrop[slot] = (float)val; // Sub
                }
            }

            // 0 나눗셈 방지: 어떤 액션도 해당 슬롯을 올리거나 내리지 못하면 1 (하한)로 대체
            for (int s = 0; s < totalSlots; s++)
            {
                if (maxGain[s] < 1f) maxGain[s] = 1f;
                if (maxDrop[s] < 1f) maxDrop[s] = 1f;
            }
        }

        /// <summary>index 번째 Effect의 (slot, op, val)을 반환한다. BuildMaxGainDrop 전용 헬퍼.</summary>
        private static void GetEffect(GOAPActionDef def, int index, out int slot, out int op, out int val)
        {
            switch (index)
            {
                case 0: slot = def.Eff0S; op = def.Eff0Op; val = def.Eff0V; return;
                case 1: slot = def.Eff1S; op = def.Eff1Op; val = def.Eff1V; return;
                case 2: slot = def.Eff2S; op = def.Eff2Op; val = def.Eff2V; return;
                case 3: slot = def.Eff3S; op = def.Eff3Op; val = def.Eff3V; return;
                case 4: slot = def.Eff4S; op = def.Eff4Op; val = def.Eff4V; return;
                case 5: slot = def.Eff5S; op = def.Eff5Op; val = def.Eff5V; return;
                case 6: slot = def.Eff6S; op = def.Eff6Op; val = def.Eff6V; return;
                case 7: slot = def.Eff7S; op = def.Eff7Op; val = def.Eff7V; return;
                default: slot = 0; op = 0; val = 0; return;
            }
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
