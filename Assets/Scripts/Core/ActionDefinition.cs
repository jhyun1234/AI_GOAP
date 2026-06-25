/// <summary>
/// ActionDefinition.cs - 단일 GOAP Action의 정적 데이터 정의
///
/// 역할(Role): GDD v0.4에 정의된 각 Action의 선행 조건(Precondition), 자원 소비(ResourceCost),
///             효과(Effect)를 직렬화 가능한 데이터 클래스로 표현한다.
///             ScriptableObject인 ActionDatabase의 내부 배열 원소로 사용된다.
///
/// 사용법(Usage): ActionDatabase ScriptableObject에서 배열로 관리한다.
///               직접 GameObject에 부착하지 않는다.
///               Inspector 또는 ActionDatabase.CreateDefaultActions()로 데이터를 채운다.
///
/// 의존성(Dependencies): ResourceType.cs (ResourceType 열거형)
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-26
/// </summary>

using UnityEngine;

namespace AIVillage.Core
{
    // ══════════════════════════════════════════════════════════════════════════
    // ActionEffect 효과 타입 열거형
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// GOAP Action 완료 후 세계 또는 에이전트 Brain에 적용되는 효과의 종류.
    /// ApplyActionEffect()의 switch 분기 기준이 된다.
    /// </summary>
    public enum ActionEffectType
    {
        // ── 자원 관련 ─────────────────────────────────────────────────────────
        GainResource,           // worldState.SetStock(type, stock + amount) — 수집 Action 결과
        ConsumeResource,        // Commit 후 실제 차감 — 소비 Action 결과 (EatCookedFood 등)

        // ── Brain 생존 수치 관련 ──────────────────────────────────────────────
        ReduceHunger,           // brain.HungerLevel -= amount (EatCookedFood, EatRawFood)
        ReduceFatigue,          // brain.FatigueLevel -= amount (RestOnGround)
        RestoreFatigue,         // brain.FatigueLevel -= amount (Sleep — 수면, 더 큰 회복량)
        GainHealth,             // brain.HealthLevel += amount (SeekMedicalAid)
        IncreaseFatigue,        // brain.FatigueLevel += amount (채집/전투 작업 부작용)
        GainMood,               // brain.MoodLevel += amount (음식 섭취 후 기분 향상)

        // ── Brain 위치/환경 플래그 관련 ───────────────────────────────────────
        SetAtBase,              // brain.AtBase = true (MoveToBase 완료)
        SetNearFireplace,       // brain.NearFireplace = true + worldState.BuildingQueued 관련

        // ── WorldState 건물 완료 플래그 관련 ─────────────────────────────────
        SetCampfireBuilt,       // worldState.BuildingQueued에서 제거, brain.NearFireplace = true
        SetHouseBuilt,          // worldState.HouseBuilt = true (미래 확장용)
        SetStorehouseBuilt,     // worldState.StorehouseBuilt = true
        SetTownHallBuilt,       // worldState.TownHallBuilt = true
        SetForgeBuilt,          // worldState.ForgeBuilt = true
        SetWatchtowerBuilt,     // worldState.WatchtowerBuilt 미래 확장용 (현재 WorldState에 없음)

        // ── Brain 인벤토리 플래그 관련 ───────────────────────────────────────
        SetHasTool,             // brain.HasTool = true (PickUpDroppedItem으로 도구 획득 등)
        SetHasPrimitiveWeapon,  // brain.HasPrimitiveWeapon = true
        SetHasWeapon,           // brain.HasWeapon = true (제련 무기 획득)

        // ── 탐험 관련 ────────────────────────────────────────────────────────
        DiscoverNearby          // 주변 자원 노드의 isDiscovered = true (Explore Action 효과, 미래 구현)
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 보조 구조체
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Action이 소비 또는 예약해야 하는 자원 항목 하나를 표현한다.
    /// TryReserveForAction()에서 ResourceRegistry.Reserve()를 호출할 때 사용한다.
    /// </summary>
    [System.Serializable]
    public struct ResourceCostEntry
    {
        [Tooltip("소비 또는 예약할 자원의 종류. ResourceType 열거형 값 중 하나.")]
        public ResourceType ResourceType;

        [Tooltip("소비 또는 예약할 수량. 양수여야 한다.")]
        public float Amount;
    }

    /// <summary>
    /// Action 완료 시 에이전트 Brain 또는 WorldState에 적용되는 단일 효과.
    /// ActionDefinition.Effects[] 배열의 원소로 사용되며 ApplyActionEffect()가 순회 처리한다.
    /// </summary>
    [System.Serializable]
    public struct ActionEffect
    {
        [Tooltip("적용할 효과 종류. ActionEffectType 열거형 값 참조.")]
        public ActionEffectType EffectType;

        [Tooltip("GainResource / ConsumeResource 효과에서 영향받는 자원 종류. 다른 효과에서는 무시된다.")]
        public ResourceType ResourceType;

        [Tooltip("효과의 크기. 회복량, 피로 증가량, 자원 수량 등에 사용된다.")]
        public float Amount;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ActionDefinition 메인 클래스
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 단일 GOAP Action의 완전한 정적 데이터.
    /// ActionDatabase ScriptableObject에 배열로 저장되며, 런타임에는 읽기 전용으로만 사용한다.
    ///
    /// 필드 규칙:
    ///   - Requires* 필드: Precondition. 이 조건이 충족되지 않으면 Action을 선택하지 않는다.
    ///   - ResourceCosts: 예약 및 소비할 자원 목록. TryReserveForAction()에서 사용한다.
    ///   - Effects: 완료 후 적용할 효과 목록. ApplyActionEffect()에서 순회 처리한다.
    ///   - BaseCost: GOAP 플래너의 탐색 비용. GetCostForRole()이 역할 보정 후 최종값을 반환한다.
    /// </summary>
    [System.Serializable]
    public class ActionDefinition
    {
        // ── 식별 ──────────────────────────────────────────────────────────────

        [Tooltip("Action 고유 ID. 코드와 Inspector에서 동일하게 사용한다. 예: \"ChopWood\", \"EatCookedFood\"")]
        public string ActionId;

        [Tooltip("GOAP 플래너 탐색 비용 기준값. 낮을수록 플래너가 이 Action을 선호한다.")]
        public float BaseCost;

        // ── 선행 조건 (Preconditions) ─────────────────────────────────────────
        // true인 항목만 Precondition으로 적용된다 (false = "이 조건 무시").
        // GetDefaultActionSequence()에서 Brain/WorldState 플래그와 대조하여 사용 가능 여부를 판단한다.

        [Header("선행 조건 (Preconditions)")]

        [Tooltip("기지에 있어야 함. brain.AtBase == true 필요.")]
        public bool RequiresAtBase;

        [Tooltip("모닥불/화덕 근처에 있어야 함. brain.NearFireplace == true 필요.")]
        public bool RequiresNearFireplace;

        [Tooltip("베리 덤불 근처에 있어야 하고 발견된 상태여야 함.")]
        public bool RequiresNearBerryBush;

        [Tooltip("나무 근처에 있어야 하고 발견된 상태여야 함 (ChopWood 전용).")]
        public bool RequiresNearTree;

        [Tooltip("바위 근처에 있어야 하고 발견된 상태여야 함 (MineStone 전용).")]
        public bool RequiresNearRock;

        [Tooltip("철광석 근처에 있어야 하고 발견된 상태여야 함 (MineIron 전용).")]
        public bool RequiresNearIronOre;

        [Tooltip("구리 광석 근처에 있어야 하고 발견된 상태여야 함 (MineCopper 전용).")]
        public bool RequiresNearCopperOre;

        [Tooltip("침대 근처에 있어야 함 (Sleep 전용).")]
        public bool RequiresNearBed;

        [Tooltip("도구(Axe 또는 Pickaxe)를 소지해야 함. brain.HasTool == true 필요.")]
        public bool RequiresHasTool;

        [Tooltip("대장간이 건설되어 있어야 함. worldState.ForgeBuilt == true 필요 (MineCopper 전용).")]
        public bool RequiresForgeBuilt;

        [Tooltip("타운홀이 건설되어 있어야 함. worldState.TownHallBuilt == true 필요.")]
        public bool RequiresTownHallBuilt;

        [Tooltip("주변에 적이 없어야 함. brain.NearEnemy == false 필요 (Sleep 전용).")]
        public bool RequiresEnemyNotNearby;

        [Tooltip("치료사 근처에 있어야 함. brain.NearHealer == true 필요 (SeekMedicalAid 전용).")]
        public bool RequiresNearHealer;

        [Tooltip("드롭된 아이템 근처에 있어야 함. brain.NearDroppedItem == true 필요.")]
        public bool RequiresNearDroppedItem;

        [Tooltip("대상 자원 노드가 탐험(Fog of War)으로 발견된 상태여야 함. isDiscovered == true 필요.")]
        public bool RequiresIsDiscovered;

        // ── 자원 소비 비용 ────────────────────────────────────────────────────

        [Header("자원 소비 비용 (Reserve 대상)")]
        [Tooltip("이 Action이 예약 및 소비할 자원 목록. 빈 배열이면 자원 소비 없음.")]
        public ResourceCostEntry[] ResourceCosts;

        // ── 완료 효과 (Effects) ───────────────────────────────────────────────

        [Header("완료 효과 (Effects)")]
        [Tooltip("Action 완료 시 Brain 또는 WorldState에 적용할 효과 목록.")]
        public ActionEffect[] Effects;
    }
}
