/// <summary>
/// ActionDatabase.cs - GOAP Action 전체 데이터베이스 ScriptableObject
///
/// 역할(Role): GDD v0.4의 모든 ActionDefinition을 보관하는 런타임 데이터 저장소.
///             VillagerFSM의 TryReserveForAction(), ApplyActionEffect(), SimulatePlanResult()
///             세 더미 메서드가 이 에셋을 조회하여 실제 로직으로 교체된다.
///
/// 사용법(Usage):
///   1. 프로젝트 창 우클릭 → Create → AIVillage → Action Database 로 에셋 생성.
///   2. 에셋 선택 후 Inspector 우상단 점 3개 → "기본 Action 정의 생성" ContextMenu 실행.
///   3. GameManager Inspector의 ActionDatabase 슬롯에 이 에셋을 드래그한다.
///   4. VillagerFSM.InjectDependencies()에 이 에셋 참조를 전달한다.
///
/// 의존성(Dependencies): ActionDefinition.cs, VillagerBrain.cs, AuthoritativeWorldState.cs,
///                       ResourceRegistry.cs, VillagerEnums.cs (AgentRole)
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-26
/// </summary>

using System.Collections.Generic;
using UnityEngine;
using AIVillage.AI;
using AIVillage.Core.GOAP;

namespace AIVillage.Core
{
    /// <summary>
    /// GOAP Action 전체 데이터베이스 ScriptableObject.
    /// 에셋 1개가 전체 게임에서 공유된다 (싱글턴 에셋 패턴).
    /// </summary>
    [CreateAssetMenu(fileName = "ActionDatabase", menuName = "AIVillage/Action Database")]
    public class ActionDatabase : ScriptableObject
    {
        #region ── Serialized Fields (Inspector) ──

        [Tooltip("GDD v0.4 기준 모든 ActionDefinition 배열. Inspector 또는 CreateDefaultActions()로 채운다.")]
        [SerializeField] private ActionDefinition[] _actions = System.Array.Empty<ActionDefinition>();

        #endregion

        #region ── Private Fields ──

        // 런타임 조회용 딕셔너리 — OnEnable에서 _actions 배열로 초기화
        // string(ActionId) → ActionDefinition 매핑, O(1) 조회 보장
        private Dictionary<string, ActionDefinition> _actionMap;

        #endregion

        #region ── Unity 생명주기 ──

        /// <summary>
        /// ScriptableObject가 로드되거나 Inspector 값이 변경될 때 Unity가 호출한다.
        /// _actions 배열을 딕셔너리로 빌드하여 GetAction()의 O(1) 조회를 준비한다.
        /// </summary>
        private void OnEnable()
        {
            RebuildActionMap();
        }

        #endregion

        #region ── 공개 조회 메서드 ──

        /// <summary>
        /// ActionId로 ActionDefinition을 조회한다.
        /// 존재하지 않는 ID는 null을 반환한다 (예외 없음).
        /// </summary>
        /// <param name="actionId">조회할 Action ID 문자열 (예: "ChopWood")</param>
        /// <returns>찾은 ActionDefinition, 없으면 null</returns>
        public ActionDefinition GetAction(string actionId)
        {
            if (string.IsNullOrEmpty(actionId))
            {
                Debug.LogWarning("[ActionDatabase] GetAction: actionId가 null 또는 빈 문자열입니다.");
                return null;
            }

            // 딕셔너리가 아직 초기화되지 않았으면 재빌드 (에디터 핫 리로드 방어)
            if (_actionMap == null) RebuildActionMap();

            return _actionMap.TryGetValue(actionId, out ActionDefinition def) ? def : null;
        }

        /// <summary>
        /// ActionId로 ActionDefinition을 시도-조회 패턴으로 반환한다.
        /// TryReserveForAction()과 ApplyActionEffect()에서 사용한다.
        /// </summary>
        /// <param name="actionId">조회할 Action ID</param>
        /// <param name="def">찾은 ActionDefinition (없으면 null)</param>
        /// <returns>찾았으면 true, 없으면 false</returns>
        public bool TryGetAction(string actionId, out ActionDefinition def)
        {
            def = null;

            if (string.IsNullOrEmpty(actionId)) return false;
            if (_actionMap == null) RebuildActionMap();

            return _actionMap.TryGetValue(actionId, out def);
        }

        /// <summary>
        /// 역할별 비용 보정을 적용한 최종 GOAP Action 비용을 반환한다.
        ///
        /// 역할별 보정 규칙 (GDD v0.4):
        ///   Lumberjack: ChopWood -50%,             MineStone/Iron/Copper +30%
        ///   Miner:      MineStone/Iron/Copper -50%, ChopWood +30%
        ///   Builder:    Build* -40%,                Attack* +60%
        ///   Cook:       CookMeal -60%,              Attack* +80%
        ///   Warrior:    Attack* -40%,               CookMeal +60%
        ///   Medic:      SeekMedicalAid -50%,        Attack* +80%
        ///   None:       보정 없음 (BaseCost 그대로)
        /// </summary>
        /// <param name="actionId">비용을 조회할 Action ID</param>
        /// <param name="role">보정 기준 역할</param>
        /// <returns>역할 보정 후 최종 비용. ActionId가 없으면 float.MaxValue 반환 (플래너가 기피하도록)</returns>
        public float GetCostForRole(string actionId, AgentRole role)
        {
            if (!TryGetAction(actionId, out ActionDefinition def))
            {
                Debug.LogWarning($"[ActionDatabase] GetCostForRole: '{actionId}'를 찾지 못했습니다. float.MaxValue 반환.");
                return float.MaxValue;
            }

            float baseCost   = def.BaseCost;
            float multiplier = CalculateRoleMultiplier(actionId, role);

            // 비용이 0 미만이 되지 않도록 클램프 (최소 0.1f)
            return Mathf.Max(0.1f, baseCost * multiplier);
        }

        /// <summary>
        /// Goal ID에 해당하는 기본 Action 시퀀스를 Brain/WorldState 상태를 고려하여 반환한다.
        /// VillagerFSM의 SimulatePlanResult() 더미를 교체하는 핵심 메서드.
        ///
        /// 반환 규칙:
        ///   - 성공 가능한 시퀀스를 찾으면 string[] (길이 >= 1) 반환
        ///   - 해결책 없음 → System.Array.Empty string 반환 (Success = false 판정)
        ///   - 알 수 없는 goalId → null 반환 (NoSolutionFound 처리)
        ///
        /// GDD v0.4 Goal별 규칙은 각 분기 주석 참조.
        /// </summary>
        /// <param name="goalId">플래닝할 Goal ID</param>
        /// <param name="role">에이전트 역할 (AgentRole)</param>
        /// <param name="brain">에이전트 런타임 상태 (읽기 전용 사용)</param>
        /// <param name="worldState">전역 월드 상태 (읽기 전용 사용)</param>
        /// <param name="registry">자원 가용량 조회용 (읽기 전용 사용)</param>
        /// <returns>실행할 Action ID 배열. 빈 배열 또는 null이면 실패.</returns>
        public string[] GetDefaultActionSequence(
            string goalId,
            AgentRole role,
            VillagerBrain brain,
            AuthoritativeWorldState worldState,
            ResourceRegistry registry)
        {
            // 방어: 필수 파라미터 null 체크
            if (string.IsNullOrEmpty(goalId))
            {
                Debug.LogWarning("[ActionDatabase] GetDefaultActionSequence: goalId가 null 또는 비어있습니다.");
                return null;
            }

            if (brain == null)
            {
                Debug.LogWarning("[ActionDatabase] GetDefaultActionSequence: brain이 null입니다.");
                return null;
            }

            // worldState와 registry는 null이어도 분기 내에서 방어 처리

            switch (goalId)
            {
                case "SurviveHunger":
                    return Plan_SurviveHunger(brain, worldState, registry);

                case "SurviveInjury":
                    return Plan_SurviveInjury(brain);

                case "SurviveFatigue":
                    return Plan_SurviveFatigue(brain);

                case "GatherResources":
                case "GatherWood":
                case "GatherStone":
                case "GatherIron":
                case "GatherCopper":
                    // GatherXxx 계열 Goal은 역할 + 자원 상태 기반으로 통합 처리
                    return Plan_GatherResources(goalId, role, brain, worldState, registry);

                case "BuildStructure":
                    return Plan_BuildStructure(worldState, registry);

                case "Explore":
                    // 탐험: 단순 단일 Action
                    return new string[] { "Explore" };

                case "DefendVillage":
                case "AttackEnemy":
                    return Plan_DefendVillage(brain, worldState);

                case "CookMeal":
                    return Plan_CookMeal(brain, worldState, registry);

                case "MoveToBase":
                    return new string[] { "MoveToBase" };

                case "RestOnGround":
                    return new string[] { "RestOnGround" };

                case "MoveToTarget":
                    return new string[] { "MoveToTarget" };

                default:
                    Debug.LogWarning($"[ActionDatabase] GetDefaultActionSequence: 알 수 없는 goalId '{goalId}'.");
                    return null;
            }
        }

        /// <summary>
        /// BuildingQueue에 대기 중인 다음 건물을 건설할 수 있는지 여부를 반환한다.
        ///
        /// [PR Fix R-002]: VillagerFSM.HasResourcesForBuilding()이 하드코딩 수치(Wood>=10, Stone>=5)를
        /// 사용하여 ActionDatabase의 실제 건물 비용과 불일치하는 문제를 해결한다.
        /// FSM은 이 메서드를 통해 간접적으로 실제 자원 요구량을 조회한다.
        ///
        /// BuildingQueue에 대기 항목이 없거나, 자원이 부족하거나, registry가 null이면 false를 반환한다.
        /// </summary>
        /// <param name="registry">자원 가용량 조회용 ResourceRegistry</param>
        /// <param name="worldState">자원 폴백 조회용 WorldState (registry가 null일 때 사용)</param>
        /// <returns>다음 건물을 즉시 건설 가능하면 true, 아니면 false</returns>
        public bool CanBuildNextBuilding(ResourceRegistry registry, AuthoritativeWorldState worldState)
        {
            BuildingQueue queue = BuildingQueue.Instance;
            if (queue == null) return false;

            BuildingQueueEntry entry = queue.GetNextPendingBuilding();
            if (entry == null) return false;

            // 내부 HasResourcesForBuilding()에 실제 비용 테이블 조회를 위임한다
            return HasResourcesForBuilding(entry.BuildingId, registry, worldState);
        }

        #endregion

        #region ── 내부 플래닝 메서드 (Goal별 분기) ──

        /// <summary>
        /// [SurviveHunger] 배고픔 해소 Action 시퀀스를 결정한다.
        ///
        /// GDD v0.4 우선순위:
        ///   1. cookedFoodStock >= 1 AND atBase  → ["EatCookedFood"]
        ///   2. cookedFoodStock >= 1 AND !atBase → ["MoveToBase", "EatCookedFood"]
        ///   3. rawFoodStock >= 1                → ["EatRawFood"]
        ///   4. 자원 없고 자원 근처              → ["HarvestWildBerries"]
        ///   5. 모두 불가                         → [] (NoSolutionFound)
        /// </summary>
        private string[] Plan_SurviveHunger(
            VillagerBrain brain,
            AuthoritativeWorldState worldState,
            ResourceRegistry registry)
        {
            // 가용량 조회 — registry가 null이면 worldState 직접 참조
            float cookedAvailable = (registry != null)
                ? registry.GetAvailable(ResourceType.CookedFood)
                : (worldState?.CookedFoodStock ?? 0f);

            float rawAvailable = (registry != null)
                ? registry.GetAvailable(ResourceType.RawFood)
                : (worldState?.RawFoodStock ?? 0f);

            // 우선순위 1 & 2: 조리식품 있음
            if (cookedAvailable >= 1f)
            {
                // 기지에 있으면 즉시 섭취, 없으면 먼저 이동
                return brain.AtBase
                    ? new string[] { "EatCookedFood" }
                    : new string[] { "MoveToBase", "EatCookedFood" };
            }

            // 우선순위 3: 생 식량 있음
            if (rawAvailable >= 1f)
            {
                return new string[] { "EatRawFood" };
            }

            // 우선순위 4: 자원이 전혀 없으면 베리 수확 시도 (자원 노드 근처인 경우)
            if (brain.NearResource)
            {
                return new string[] { "HarvestWildBerries" };
            }

            // 해결책 없음 — 빈 배열로 NoSolutionFound 신호
            Debug.LogWarning($"[ActionDatabase] SurviveHunger: 사용 가능한 식량 없음. " +
                             $"cookedFood={cookedAvailable:F0}, rawFood={rawAvailable:F0}. NoSolutionFound.");
            return System.Array.Empty<string>();
        }

        /// <summary>
        /// [SurviveInjury] 부상 회복 Action 시퀀스를 결정한다.
        ///
        /// GDD v0.4 우선순위:
        ///   1. nearHealer              → ["SeekMedicalAid"]
        ///   2. !nearHealer AND !atBase → ["MoveToBase"] (기지에 치료사가 있다고 가정)
        ///   3. 최후 수단               → ["RestOnGround"]
        /// </summary>
        private string[] Plan_SurviveInjury(VillagerBrain brain)
        {
            if (brain.NearHealer)
            {
                return new string[] { "SeekMedicalAid" };
            }

            // 기지 밖이면 기지로 이동 (치료사가 기지에 있다고 가정)
            if (!brain.AtBase)
            {
                return new string[] { "MoveToBase" };
            }

            // 기지 안이지만 치료사가 없음 → 최후 수단: 땅에서 휴식
            return new string[] { "RestOnGround" };
        }

        /// <summary>
        /// [SurviveFatigue] 피로 회복 Action 시퀀스를 결정한다.
        ///
        /// GDD v0.4 우선순위:
        ///   1. nearBed AND !enemyNearby → ["Sleep"]
        ///   2. otherwise                → ["RestOnGround"]
        /// </summary>
        private string[] Plan_SurviveFatigue(VillagerBrain brain)
        {
            // 침대 있고 적 없을 때만 수면 가능
            if (brain.NearBed && !brain.NearEnemy)
            {
                return new string[] { "Sleep" };
            }

            // 적이 있거나 침대 없으면 땅에서 쉬기 (피로 20 회복)
            return new string[] { "RestOnGround" };
        }

        /// <summary>
        /// [GatherResources] 역할별 자원 수집 Action 시퀀스를 결정한다.
        ///
        /// GDD v0.4 우선순위:
        ///   도구 없음 AND 드롭 아이템 근처 → ["PickUpDroppedItem"] 선행
        ///   Lumberjack + nearResource + hasTool    → ["ChopWood"]
        ///   Miner + nearResource + hasTool         → ["MineCopper" > "MineIron" > "MineStone" 순]
        ///   Cook                                   → rawFood >= 3 AND nearFireplace → ["CookMeal"]
        ///                                            otherwise → ["HarvestWildBerries"]
        ///   특정 자원 Goal (GatherWood 등)         → 역할 내에서 해당 자원으로 강제 분기
        ///   None (역할 미정)                       → ["HarvestWildBerries"] (가장 안전한 fallback)
        /// </summary>
        private string[] Plan_GatherResources(
            string goalId,
            AgentRole role,
            VillagerBrain brain,
            AuthoritativeWorldState worldState,
            ResourceRegistry registry)
        {
            // ── 도구 없음 처리: 드롭 아이템이 있으면 먼저 줍기 ─────────────────
            if (!brain.HasTool && brain.NearDroppedItem)
            {
                return new string[] { "PickUpDroppedItem" };
            }

            // ── 특정 자원 강제 분기 (GatherWood, GatherStone 등 구체적 Goal) ────
            switch (goalId)
            {
                case "GatherWood":
                    // [PR Fix R-003]: NearResource → NearDiscoveredResource 로 교체.
                    // FoW에서 발견된 자원 노드만 플래닝에 포함한다.
                    if (brain.HasTool && brain.NearDiscoveredResource)
                        return new string[] { "ChopWood" };
                    // 도구 없거나 발견된 나무 없으면 베리 수확 폴백
                    return new string[] { "HarvestWildBerries" };

                case "GatherStone":
                    // [PR Fix R-003]: NearResource → NearDiscoveredResource 로 교체.
                    // [PR Fix R-006]: 폴백을 HarvestWildBerries → System.Array.Empty 로 변경.
                    // 돌 채집 Goal에서 조건 미충족 시 목표와 무관한 베리 수확 대신 NoSolutionFound를 반환한다.
                    if (brain.HasTool && brain.NearRock && brain.NearDiscoveredResource)
                        return new string[] { "MineStone" };
                    return System.Array.Empty<string>(); // 돌은 베리로 대체 불가 — NoSolutionFound 신호

                case "GatherIron":
                    // [PR Fix R-003]: NearResource → NearIronOre + NearDiscoveredResource 로 교체.
                    if (brain.HasTool && brain.NearIronOre && brain.NearDiscoveredResource)
                        return new string[] { "MineIron" };
                    return System.Array.Empty<string>(); // 철은 베리로 대체 불가

                case "GatherCopper":
                    // [PR Fix R-003]: NearResource → NearCopperOre + NearDiscoveredResource 로 교체.
                    bool forgeBuiltForCopper = worldState?.ForgeBuilt ?? false;
                    if (brain.HasTool && brain.NearCopperOre && brain.NearDiscoveredResource && forgeBuiltForCopper)
                        return new string[] { "MineCopper" };
                    return System.Array.Empty<string>(); // 구리는 Forge 없으면 NoSolution
            }

            // ── 역할별 기본 수집 분기 (GatherResources 통합 Goal) ────────────
            switch (role)
            {
                case AgentRole.Lumberjack:
                    // 나무꾼: ChopWood 특화 (-50% 비용)
                    // [PR Fix R-003]: NearResource → NearDiscoveredResource 로 교체.
                    if (brain.HasTool && brain.NearDiscoveredResource)
                        return new string[] { "ChopWood" };
                    // 발견된 나무 없으면 베리 수확 폴백
                    return new string[] { "HarvestWildBerries" };

                case AgentRole.Miner:
                    // [PR Fix R-001]: 세 분기가 모두 동일한 (HasTool && NearResource) 조건을 사용하여
                    // MineIron이 먼저 통과하면 MineStone에 절대 도달하지 못하는 Dead Code 버그를 수정한다.
                    // 각 자원별 전용 근접 플래그(NearCopperOre, NearIronOre, NearRock)로 조건을 분리하고
                    // [PR Fix R-003]: NearDiscoveredResource(FoW 필터)를 추가한다.
                    // 우선순위: 구리(ForgeBuilt 필요) → 철 → 돌 (희귀 자원 먼저)

                    bool forgeAvail = worldState?.ForgeBuilt ?? false;

                    // 구리: 대장간 건설 완료 + 구리 광석 근처 + 발견된 자원
                    if (brain.HasTool && brain.NearCopperOre && brain.NearDiscoveredResource && forgeAvail)
                        return new string[] { "MineCopper" };

                    // 철: 철 광석 근처 + 발견된 자원 (대장간 불필요)
                    if (brain.HasTool && brain.NearIronOre && brain.NearDiscoveredResource)
                        return new string[] { "MineIron" };

                    // 돌: 바위 근처 + 발견된 자원
                    if (brain.HasTool && brain.NearRock && brain.NearDiscoveredResource)
                        return new string[] { "MineStone" };

                    // 도구 없거나 근처 광석 없으면 베리 수확 폴백
                    return new string[] { "HarvestWildBerries" };

                case AgentRole.Cook:
                    // 요리사: 재료 충분 AND 화덕 있으면 요리, 아니면 수확
                    float rawAvail = (registry != null)
                        ? registry.GetAvailable(ResourceType.RawFood)
                        : (worldState?.RawFoodStock ?? 0f);

                    if (rawAvail >= 3f && brain.NearFireplace)
                        return new string[] { "CookMeal" };

                    // 재료 부족하면 수확 먼저
                    return new string[] { "HarvestWildBerries" };

                case AgentRole.Builder:
                case AgentRole.Warrior:
                case AgentRole.Medic:
                case AgentRole.None:
                default:
                    // 역할 미정: 가장 안전한 수집 방법 (HarvestWildBerries = 도구 불필요)
                    return new string[] { "HarvestWildBerries" };
            }
        }

        /// <summary>
        /// [BuildStructure] BuildingQueue에서 대기 중인 건물을 확인하고 건설 Action을 반환한다.
        ///
        /// GDD v0.4 규칙:
        ///   BuildingQueue에서 첫 번째 Pending/InProgress 건물 확인
        ///   자원 충족 → ["BuildXxx"]
        ///   자원 부족 → [] (NoSolutionFound — GatherResources로 재플래닝 유도)
        ///   대기 건물 없음 → [] (NoSolutionFound)
        /// </summary>
        private string[] Plan_BuildStructure(
            AuthoritativeWorldState worldState,
            ResourceRegistry registry)
        {
            // BuildingQueue 싱글턴 접근
            BuildingQueue queue = BuildingQueue.Instance;

            if (queue == null)
            {
                Debug.LogWarning("[ActionDatabase] Plan_BuildStructure: BuildingQueue.Instance가 null입니다.");
                return System.Array.Empty<string>();
            }

            BuildingQueueEntry entry = queue.GetNextPendingBuilding();

            if (entry == null)
            {
                // 대기 중인 건물 없음
                return System.Array.Empty<string>();
            }

            // 건물별 Action ID 매핑
            string buildActionId = GetBuildActionId(entry.BuildingId);

            if (string.IsNullOrEmpty(buildActionId))
            {
                Debug.LogWarning($"[ActionDatabase] Plan_BuildStructure: 알 수 없는 BuildingId '{entry.BuildingId}'.");
                return System.Array.Empty<string>();
            }

            // 자원 충족 여부 확인
            if (!HasResourcesForBuilding(entry.BuildingId, registry, worldState))
            {
                // 자원 부족 — NoSolutionFound로 GatherResources 재플래닝 유도
                Debug.Log($"[ActionDatabase] Plan_BuildStructure: '{entry.BuildingId}' 건설 자원 부족. " +
                          $"NoSolutionFound 반환 (GatherResources 재플래닝 유도).");
                return System.Array.Empty<string>();
            }

            return new string[] { buildActionId };
        }

        /// <summary>
        /// [DefendVillage] 방어/전투 Action 시퀀스를 결정한다.
        ///
        /// GDD v0.4 우선순위:
        ///   healthLevel < 40                 → ["FleeFromEnemy"] (도주 우선)
        ///   hasWeapon OR hasPrimitiveWeapon  → ["AttackEnemy"]
        ///   !hasWeapon AND forgeBuilt        → ["CraftWeapon", "AttackEnemy"]
        ///   최후 수단                         → ["AlertVillage"]
        /// </summary>
        private string[] Plan_DefendVillage(VillagerBrain brain, AuthoritativeWorldState worldState)
        {
            // 체력 위기: 먼저 도주
            if (brain.HealthLevel < 40f)
            {
                return new string[] { "FleeFromEnemy" };
            }

            // 무기 있으면 즉시 공격
            if (brain.HasWeapon || brain.HasPrimitiveWeapon)
            {
                return new string[] { "AttackEnemy" };
            }

            // 무기 없고 대장간 있으면 무기 제작 후 공격
            bool forgeExists = worldState?.ForgeBuilt ?? false;
            if (forgeExists)
            {
                return new string[] { "CraftWeapon", "AttackEnemy" };
            }

            // 최후 수단: 마을 경보 발령
            return new string[] { "AlertVillage" };
        }

        /// <summary>
        /// [CookMeal] 요리 Action 시퀀스를 결정한다.
        /// 재료 부족 시 수확 선행 시퀀스를 반환한다.
        /// </summary>
        private string[] Plan_CookMeal(
            VillagerBrain brain,
            AuthoritativeWorldState worldState,
            ResourceRegistry registry)
        {
            float rawAvail = (registry != null)
                ? registry.GetAvailable(ResourceType.RawFood)
                : (worldState?.RawFoodStock ?? 0f);

            // 화덕 근처 + 재료 3개 이상 → 즉시 요리
            if (brain.NearFireplace && rawAvail >= 3f)
            {
                return new string[] { "CookMeal" };
            }

            // 화덕은 있지만 재료 부족 → 수확 선행
            if (brain.NearFireplace && rawAvail < 3f)
            {
                return new string[] { "HarvestWildBerries", "CookMeal" };
            }

            // 화덕 없음 → 기지로 이동 (화덕이 기지에 있다고 가정)
            if (rawAvail >= 3f)
            {
                return new string[] { "MoveToBase", "CookMeal" };
            }

            // 화덕도 없고 재료도 부족 → 수확 후 기지 이동 후 요리
            return new string[] { "HarvestWildBerries", "MoveToBase", "CookMeal" };
        }

        #endregion

        #region ── 내부 헬퍼 메서드 ──

        /// <summary>
        /// _actions 배열로 _actionMap 딕셔너리를 재구성한다.
        /// OnEnable()과 Inspector 수정 후 자동 호출된다.
        /// </summary>
        private void RebuildActionMap()
        {
            _actionMap = new Dictionary<string, ActionDefinition>(
                _actions != null ? _actions.Length : 8);

            if (_actions == null) return;

            foreach (ActionDefinition def in _actions)
            {
                if (def == null || string.IsNullOrEmpty(def.ActionId))
                {
                    Debug.LogWarning("[ActionDatabase] RebuildActionMap: ActionId가 null/빈 ActionDefinition을 건너뜁니다.");
                    continue;
                }

                if (_actionMap.ContainsKey(def.ActionId))
                {
                    Debug.LogWarning($"[ActionDatabase] RebuildActionMap: 중복 ActionId '{def.ActionId}' 발견. " +
                                     $"첫 번째 항목만 사용합니다.");
                    continue;
                }

                _actionMap[def.ActionId] = def;
            }
        }

        /// <summary>
        /// AgentRole + ActionId 조합으로 비용 보정 배율을 계산한다.
        ///
        /// GDD v0.4 역할별 보정 테이블 (배율로 변환):
        ///   Lumberjack: ChopWood ×0.5 (-50%),      MineStone/Iron/Copper ×1.3 (+30%)
        ///   Miner:      Mine* ×0.5 (-50%),           ChopWood ×1.3 (+30%)
        ///   Builder:    Build* ×0.6 (-40%),           Attack* ×1.6 (+60%)
        ///   Cook:       CookMeal ×0.4 (-60%),         Attack* ×1.8 (+80%)
        ///   Warrior:    Attack* ×0.6 (-40%),          CookMeal ×1.6 (+60%)
        ///   Medic:      SeekMedicalAid ×0.5 (-50%),  Attack* ×1.8 (+80%)
        ///   None:       ×1.0 (보정 없음)
        /// </summary>
        private float CalculateRoleMultiplier(string actionId, AgentRole role)
        {
            switch (role)
            {
                case AgentRole.Lumberjack:
                    if (actionId == "ChopWood")                             return 0.5f; // -50%
                    if (actionId == "MineStone"
                     || actionId == "MineIron"
                     || actionId == "MineCopper")                           return 1.3f; // +30%
                    break;

                case AgentRole.Miner:
                    if (actionId == "MineStone"
                     || actionId == "MineIron"
                     || actionId == "MineCopper")                           return 0.5f; // -50%
                    if (actionId == "ChopWood")                             return 1.3f; // +30%
                    break;

                case AgentRole.Builder:
                    if (actionId.StartsWith("Build"))                       return 0.6f; // -40%
                    if (actionId == "AttackEnemy"
                     || actionId == "FleeFromEnemy"
                     || actionId == "AlertVillage"
                     || actionId == "CraftWeapon")                          return 1.6f; // +60%
                    break;

                case AgentRole.Cook:
                    if (actionId == "CookMeal")                             return 0.4f; // -60%
                    if (actionId == "AttackEnemy"
                     || actionId == "FleeFromEnemy"
                     || actionId == "AlertVillage"
                     || actionId == "CraftWeapon")                          return 1.8f; // +80%
                    break;

                case AgentRole.Warrior:
                    if (actionId == "AttackEnemy"
                     || actionId == "FleeFromEnemy"
                     || actionId == "AlertVillage"
                     || actionId == "CraftWeapon")                          return 0.6f; // -40%
                    if (actionId == "CookMeal")                             return 1.6f; // +60%
                    break;

                case AgentRole.Medic:
                    if (actionId == "SeekMedicalAid")                       return 0.5f; // -50%
                    if (actionId == "AttackEnemy"
                     || actionId == "FleeFromEnemy"
                     || actionId == "AlertVillage"
                     || actionId == "CraftWeapon")                          return 1.8f; // +80%
                    break;

                case AgentRole.None:
                default:
                    // 보정 없음
                    break;
            }

            return 1.0f; // 기본값: 보정 없음
        }

        /// <summary>
        /// BuildingId를 GOAP Action ID 문자열로 변환한다.
        /// </summary>
        private string GetBuildActionId(string buildingId)
        {
            switch (buildingId)
            {
                case "Campfire":   return "BuildCampfire";
                case "House":      return "BuildHouse";
                case "Storehouse": return "BuildStorehouse";
                case "TownHall":   return "BuildTownHall";
                case "Forge":      return "BuildForge";
                case "Watchtower": return "BuildWatchtower";
                default:
                    Debug.LogWarning($"[ActionDatabase] GetBuildActionId: 알 수 없는 BuildingId '{buildingId}'.");
                    return null;
            }
        }

        /// <summary>
        /// BuildingId에 따른 자원 충족 여부를 확인한다.
        /// GDD v0.4 건설 비용 테이블 기준.
        /// </summary>
        private bool HasResourcesForBuilding(
            string buildingId,
            ResourceRegistry registry,
            AuthoritativeWorldState worldState)
        {
            if (registry == null && worldState == null) return false;

            // 편의 로컬 함수: 가용 자원 조회
            float AvailOf(ResourceType t) =>
                registry != null
                    ? registry.GetAvailable(t)
                    : worldState.GetStock(t);

            // 기획서 수치: 건물별 자원 요구량 (GDD v0.4)
            switch (buildingId)
            {
                case "Campfire":
                    return AvailOf(ResourceType.Wood) >= 5f; // 기획서: Wood 5

                case "House":
                    return AvailOf(ResourceType.Wood)  >= 20f  // 기획서: Wood 20
                        && AvailOf(ResourceType.Stone) >= 10f; // 기획서: Stone 10

                case "Storehouse":
                    return AvailOf(ResourceType.Wood)  >= 15f  // 기획서: Wood 15
                        && AvailOf(ResourceType.Stone) >= 5f;  // 기획서: Stone 5

                case "TownHall":
                    return AvailOf(ResourceType.Wood)  >= 35f  // 기획서: Wood 35
                        && AvailOf(ResourceType.Stone) >= 30f  // 기획서: Stone 30
                        && AvailOf(ResourceType.Iron)  >= 6f;  // 기획서: Iron 6

                case "Forge":
                    return AvailOf(ResourceType.Wood)  >= 20f  // 기획서: Wood 20
                        && AvailOf(ResourceType.Stone) >= 20f  // 기획서: Stone 20
                        && AvailOf(ResourceType.Iron)  >= 15f; // 기획서: Iron 15

                case "Watchtower":
                    return AvailOf(ResourceType.Wood)  >= 10f  // 기획서: Wood 10
                        && AvailOf(ResourceType.Stone) >= 30f  // 기획서: Stone 30
                        && AvailOf(ResourceType.Iron)  >= 5f;  // 기획서: Iron 5

                default:
                    Debug.LogWarning($"[ActionDatabase] HasResourcesForBuilding: 알 수 없는 BuildingId '{buildingId}'.");
                    return false;
            }
        }

        #endregion

        #region ── Editor 전용: 기본 Action 정의 생성 ──

#if UNITY_EDITOR
        /// <summary>
        /// GDD v0.4 기준 모든 ActionDefinition을 이 ScriptableObject에 채운다.
        /// 에셋이 비어있을 때만 사용한다. 이미 데이터가 있으면 덮어쓰지 않는다.
        /// Inspector에서 에셋 선택 → 우상단 점 3개 메뉴 → "기본 Action 정의 생성" 클릭.
        ///
        /// 생성 후 AssetDatabase.SaveAssets()를 자동 호출하여 에셋에 즉시 저장한다.
        /// </summary>
        [ContextMenu("기본 Action 정의 생성 (에셋 비어있을 때만 사용)")]
        public void CreateDefaultActions()
        {
            if (_actions != null && _actions.Length > 0)
            {
                Debug.LogWarning("[ActionDatabase] 이미 Action 데이터가 있습니다. " +
                                 "덮어쓰려면 _actions 배열을 Inspector에서 비운 후 다시 실행하세요.");
                return;
            }

            _actions = BuildDefaultActionDefinitions();
            RebuildActionMap();

            // 에디터에서 에셋 더티 플래그 설정 및 즉시 저장
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();

            Debug.Log($"[ActionDatabase] 기본 Action 정의 {_actions.Length}개 생성 완료. 에셋에 저장됨.");
        }

        /// <summary>
        /// GDD v0.4 수치 기반으로 모든 ActionDefinition 인스턴스를 생성하여 배열로 반환한다.
        /// CreateDefaultActions()의 내부 구현 메서드.
        /// </summary>
        private ActionDefinition[] BuildDefaultActionDefinitions()
        {
            // ──────────────────────────────────────────────────────────────────
            // 로컬 헬퍼: 단순 효과 하나짜리 ActionEffect 생성
            // ──────────────────────────────────────────────────────────────────
            ActionEffect MakeEffect(ActionEffectType type, float amount = 0f,
                                    ResourceType resType = ResourceType.RawFood)
                => new ActionEffect { EffectType = type, Amount = amount, ResourceType = resType };

            ResourceCostEntry MakeCost(ResourceType type, float amount)
                => new ResourceCostEntry { ResourceType = type, Amount = amount };

            // ══════════════════════════════════════════════════════════════════
            // 생존 Action (GDD v0.4 기준)
            // ══════════════════════════════════════════════════════════════════

            // EatCookedFood: 조리 식품 섭취 — 배고픔 50 감소, 기분 5 향상
            var eatCooked = new ActionDefinition
            {
                ActionId       = "EatCookedFood",
                BaseCost       = 2f, // 기획서 수치: BaseCost 2
                RequiresAtBase = true,
                ResourceCosts  = new[] { MakeCost(ResourceType.CookedFood, 1f) },
                Effects = new[]
                {
                    MakeEffect(ActionEffectType.ConsumeResource, 1f, ResourceType.CookedFood),
                    MakeEffect(ActionEffectType.ReduceHunger,   GOAPActionRegistry.EAT_HUNGER_RELIEF), // ADR-7: GOAPActionRegistry 단일 출처
                    MakeEffect(ActionEffectType.GainMood,        5f)  // 기획서 수치: mood += 5
                }
            };

            // EatRawFood: 생 식품 섭취 — 배고픔 15 감소 (효율 낮음)
            var eatRaw = new ActionDefinition
            {
                ActionId      = "EatRawFood",
                BaseCost      = 4f, // 기획서 수치: BaseCost 4
                ResourceCosts = new[] { MakeCost(ResourceType.RawFood, 1f) },
                Effects = new[]
                {
                    MakeEffect(ActionEffectType.ConsumeResource, 1f, ResourceType.RawFood),
                    MakeEffect(ActionEffectType.ReduceHunger, GOAPActionRegistry.EAT_RAW_RELIEF) // ADR-7: GOAPActionRegistry 단일 출처
                }
            };

            // HarvestWildBerries: 야생 베리 수확 — rawFood += 5
            var harvestBerries = new ActionDefinition
            {
                ActionId              = "HarvestWildBerries",
                BaseCost              = 5f, // 기획서 수치: BaseCost 5
                RequiresNearBerryBush = true,
                RequiresIsDiscovered  = true,
                ResourceCosts         = System.Array.Empty<ResourceCostEntry>(),
                Effects = new[]
                {
                    MakeEffect(ActionEffectType.GainResource, 5f, ResourceType.RawFood) // 기획서 수치: rawFood += 5
                }
            };

            // CookMeal: 요리 — rawFood 3 소비 → cookedFood 2 생성
            var cookMeal = new ActionDefinition
            {
                ActionId              = "CookMeal",
                BaseCost              = 8f, // 기획서 수치: BaseCost 8
                RequiresNearFireplace = true,
                ResourceCosts         = new[] { MakeCost(ResourceType.RawFood, 3f) },
                Effects = new[]
                {
                    MakeEffect(ActionEffectType.ConsumeResource, 3f, ResourceType.RawFood),
                    MakeEffect(ActionEffectType.GainResource,    2f, ResourceType.CookedFood) // 기획서 수치: cookedFood += 2
                }
            };

            // Sleep: 수면 — 피로 90 감소, 침대 + 비전투 조건
            var sleep = new ActionDefinition
            {
                ActionId               = "Sleep",
                BaseCost               = 6f, // 기획서 수치: BaseCost 6
                RequiresNearBed        = true,
                RequiresEnemyNotNearby = true,
                ResourceCosts          = System.Array.Empty<ResourceCostEntry>(),
                Effects = new[]
                {
                    MakeEffect(ActionEffectType.RestoreFatigue, GOAPActionRegistry.SLEEP_FATIGUE_RELIEF) // ADR-7: GOAPActionRegistry 단일 출처
                }
            };

            // RestOnGround: 땅에서 쉬기 — 피로 20 감소 (선행 조건 없음)
            var restOnGround = new ActionDefinition
            {
                ActionId      = "RestOnGround",
                BaseCost      = 10f, // 기획서 수치: BaseCost 10
                ResourceCosts = System.Array.Empty<ResourceCostEntry>(),
                Effects = new[]
                {
                    MakeEffect(ActionEffectType.ReduceFatigue, GOAPActionRegistry.REST_FATIGUE_RELIEF) // ADR-7: GOAPActionRegistry 단일 출처
                }
            };

            // SeekMedicalAid: 의료 치료 — health += 40
            var seekMedical = new ActionDefinition
            {
                ActionId           = "SeekMedicalAid",
                BaseCost           = 3f, // 기획서 수치: BaseCost 3
                RequiresNearHealer = true,
                ResourceCosts      = System.Array.Empty<ResourceCostEntry>(),
                Effects = new[]
                {
                    MakeEffect(ActionEffectType.GainHealth, GOAPActionRegistry.MEDICAL_HEALTH_GAIN) // ADR-7: GOAPActionRegistry 단일 출처
                }
            };

            // MoveToBase: 기지로 이동 (선행 조건 없음, 비용 5)
            var moveToBase = new ActionDefinition
            {
                ActionId      = "MoveToBase",
                BaseCost      = 5f, // 기획서 수치: BaseCost 5
                ResourceCosts = System.Array.Empty<ResourceCostEntry>(),
                Effects = new[]
                {
                    MakeEffect(ActionEffectType.SetAtBase)
                }
            };

            // ══════════════════════════════════════════════════════════════════
            // 자원 수집 Action (GDD v0.4 기준)
            // ══════════════════════════════════════════════════════════════════

            // ChopWood: 나무 수집 — wood += 10, fatigue += 10
            var chopWood = new ActionDefinition
            {
                ActionId             = "ChopWood",
                BaseCost             = 4f,  // 기획서 수치: BaseCost 4
                RequiresNearTree     = true,
                RequiresIsDiscovered = true,
                RequiresHasTool      = true, // 도끼 필요
                ResourceCosts        = System.Array.Empty<ResourceCostEntry>(),
                Effects = new[]
                {
                    MakeEffect(ActionEffectType.GainResource,   10f, ResourceType.Wood), // 기획서 수치: wood += 10
                    MakeEffect(ActionEffectType.IncreaseFatigue, 10f)                    // 기획서 수치: fatigue += 10
                }
            };

            // MineStone: 돌 수집 — stone += 8, fatigue += 15
            var mineStone = new ActionDefinition
            {
                ActionId             = "MineStone",
                BaseCost             = 5f,  // 기획서 수치: BaseCost 5
                RequiresNearRock     = true,
                RequiresIsDiscovered = true,
                RequiresHasTool      = true, // 곡괭이 필요
                ResourceCosts        = System.Array.Empty<ResourceCostEntry>(),
                Effects = new[]
                {
                    MakeEffect(ActionEffectType.GainResource,    8f, ResourceType.Stone), // 기획서 수치: stone += 8
                    MakeEffect(ActionEffectType.IncreaseFatigue, 15f)                     // 기획서 수치: fatigue += 15
                }
            };

            // MineIron: 철 수집 — iron += 5, fatigue += 15
            var mineIron = new ActionDefinition
            {
                ActionId             = "MineIron",
                BaseCost             = 6f,  // 기획서 수치: BaseCost 6
                RequiresNearIronOre  = true,
                RequiresIsDiscovered = true,
                RequiresHasTool      = true,
                ResourceCosts        = System.Array.Empty<ResourceCostEntry>(),
                Effects = new[]
                {
                    MakeEffect(ActionEffectType.GainResource,    5f, ResourceType.Iron), // 기획서 수치: iron += 5
                    MakeEffect(ActionEffectType.IncreaseFatigue, 15f)                    // 기획서 수치: fatigue += 15
                }
            };

            // MineCopper: 구리 수집 — copper += 3, fatigue += 15 (대장간 건설 후)
            var mineCopper = new ActionDefinition
            {
                ActionId              = "MineCopper",
                BaseCost              = 9f,  // 기획서 수치: BaseCost 9
                RequiresNearCopperOre = true,
                RequiresIsDiscovered  = true,
                RequiresHasTool       = true,
                RequiresForgeBuilt    = true, // 기획서: forgeBuilt=true 선행 조건
                ResourceCosts         = System.Array.Empty<ResourceCostEntry>(),
                Effects = new[]
                {
                    MakeEffect(ActionEffectType.GainResource,    3f, ResourceType.Copper), // 기획서 수치: copper += 3
                    MakeEffect(ActionEffectType.IncreaseFatigue, 15f)                      // 기획서 수치: fatigue += 15
                }
            };

            // PickUpDroppedItem: 드롭 아이템 줍기 — HasTool = true
            var pickUpItem = new ActionDefinition
            {
                ActionId                = "PickUpDroppedItem",
                BaseCost                = 1f,  // 기획서 수치: BaseCost 1
                RequiresNearDroppedItem = true,
                ResourceCosts           = System.Array.Empty<ResourceCostEntry>(),
                Effects = new[]
                {
                    MakeEffect(ActionEffectType.SetHasTool) // 도구 획득 효과
                }
            };

            // Explore: 탐험 — fatigue += 5
            var explore = new ActionDefinition
            {
                ActionId      = "Explore",
                BaseCost      = 7f,  // 기획서 수치: BaseCost 7
                ResourceCosts = System.Array.Empty<ResourceCostEntry>(),
                Effects = new[]
                {
                    MakeEffect(ActionEffectType.IncreaseFatigue, 5f), // 기획서 수치: fatigue += 5
                    MakeEffect(ActionEffectType.DiscoverNearby)        // 주변 자원 노드 발견
                }
            };

            // ══════════════════════════════════════════════════════════════════
            // 건설 Action (GDD v0.4 기준)
            // ══════════════════════════════════════════════════════════════════

            // BuildCampfire: 모닥불 건설 — Wood 5
            var buildCampfire = new ActionDefinition
            {
                ActionId      = "BuildCampfire",
                BaseCost      = 3f,  // 기획서 수치: GOAP Cost 3
                ResourceCosts = new[]
                {
                    MakeCost(ResourceType.Wood, 5f) // 기획서 수치: Wood 5
                },
                Effects = new[]
                {
                    MakeEffect(ActionEffectType.ConsumeResource, 5f, ResourceType.Wood),
                    MakeEffect(ActionEffectType.SetCampfireBuilt)
                }
            };

            // BuildHouse: 집 건설 — Wood 20, Stone 10
            var buildHouse = new ActionDefinition
            {
                ActionId      = "BuildHouse",
                BaseCost      = 15f, // 기획서 수치: GOAP Cost 15
                ResourceCosts = new[]
                {
                    MakeCost(ResourceType.Wood,  20f), // 기획서 수치: Wood 20
                    MakeCost(ResourceType.Stone, 10f)  // 기획서 수치: Stone 10
                },
                Effects = new[]
                {
                    MakeEffect(ActionEffectType.ConsumeResource, 20f, ResourceType.Wood),
                    MakeEffect(ActionEffectType.ConsumeResource, 10f, ResourceType.Stone),
                    MakeEffect(ActionEffectType.SetHouseBuilt)
                }
            };

            // BuildStorehouse: 창고 건설 — Wood 15, Stone 5
            var buildStorehouse = new ActionDefinition
            {
                ActionId      = "BuildStorehouse",
                BaseCost      = 10f, // 기획서 수치: GOAP Cost 10
                ResourceCosts = new[]
                {
                    MakeCost(ResourceType.Wood,  15f), // 기획서 수치: Wood 15
                    MakeCost(ResourceType.Stone,  5f)  // 기획서 수치: Stone 5
                },
                Effects = new[]
                {
                    MakeEffect(ActionEffectType.ConsumeResource, 15f, ResourceType.Wood),
                    MakeEffect(ActionEffectType.ConsumeResource,  5f, ResourceType.Stone),
                    MakeEffect(ActionEffectType.SetStorehouseBuilt)
                }
            };

            // BuildTownHall: 타운홀 건설 — Wood 35, Stone 30, Iron 6
            var buildTownHall = new ActionDefinition
            {
                ActionId      = "BuildTownHall",
                BaseCost      = 25f, // 기획서 수치: GOAP Cost 25
                ResourceCosts = new[]
                {
                    MakeCost(ResourceType.Wood,  35f), // 기획서 수치: Wood 35
                    MakeCost(ResourceType.Stone, 30f), // 기획서 수치: Stone 30
                    MakeCost(ResourceType.Iron,   6f)  // 기획서 수치: Iron 6
                },
                Effects = new[]
                {
                    MakeEffect(ActionEffectType.ConsumeResource, 35f, ResourceType.Wood),
                    MakeEffect(ActionEffectType.ConsumeResource, 30f, ResourceType.Stone),
                    MakeEffect(ActionEffectType.ConsumeResource,  6f, ResourceType.Iron),
                    MakeEffect(ActionEffectType.SetTownHallBuilt)
                }
            };

            // BuildForge: 대장간 건설 — Wood 20, Stone 20, Iron 15
            var buildForge = new ActionDefinition
            {
                ActionId      = "BuildForge",
                BaseCost      = 20f, // 기획서 수치: GOAP Cost 20
                ResourceCosts = new[]
                {
                    MakeCost(ResourceType.Wood,  20f), // 기획서 수치: Wood 20
                    MakeCost(ResourceType.Stone, 20f), // 기획서 수치: Stone 20
                    MakeCost(ResourceType.Iron,  15f)  // 기획서 수치: Iron 15
                },
                Effects = new[]
                {
                    MakeEffect(ActionEffectType.ConsumeResource, 20f, ResourceType.Wood),
                    MakeEffect(ActionEffectType.ConsumeResource, 20f, ResourceType.Stone),
                    MakeEffect(ActionEffectType.ConsumeResource, 15f, ResourceType.Iron),
                    MakeEffect(ActionEffectType.SetForgeBuilt)
                }
            };

            // BuildWatchtower: 망루 건설 — Wood 10, Stone 30, Iron 5
            var buildWatchtower = new ActionDefinition
            {
                ActionId      = "BuildWatchtower",
                BaseCost      = 12f, // 기획서 수치: GOAP Cost 12
                ResourceCosts = new[]
                {
                    MakeCost(ResourceType.Wood,  10f), // 기획서 수치: Wood 10
                    MakeCost(ResourceType.Stone, 30f), // 기획서 수치: Stone 30
                    MakeCost(ResourceType.Iron,   5f)  // 기획서 수치: Iron 5
                },
                Effects = new[]
                {
                    MakeEffect(ActionEffectType.ConsumeResource, 10f, ResourceType.Wood),
                    MakeEffect(ActionEffectType.ConsumeResource, 30f, ResourceType.Stone),
                    MakeEffect(ActionEffectType.ConsumeResource,  5f, ResourceType.Iron),
                    MakeEffect(ActionEffectType.SetWatchtowerBuilt)
                }
            };

            // ══════════════════════════════════════════════════════════════════
            // 전투 Action (더미 — 실제 전투 시스템은 5단계에서 구현)
            // ══════════════════════════════════════════════════════════════════

            // AttackEnemy: 적 공격 — fatigue += 20
            var attackEnemy = new ActionDefinition
            {
                ActionId      = "AttackEnemy",
                BaseCost      = 8f,  // TODO: 기획팀 수치 확인 필요 — 현재 임시값
                ResourceCosts = System.Array.Empty<ResourceCostEntry>(),
                Effects = new[]
                {
                    MakeEffect(ActionEffectType.IncreaseFatigue, 20f)
                }
            };

            // CraftWeapon: 무기 제작 (대장간에서)
            var craftWeapon = new ActionDefinition
            {
                ActionId           = "CraftWeapon",
                BaseCost           = 10f, // TODO: 기획팀 수치 확인 필요 — 현재 임시값
                RequiresForgeBuilt = true,
                ResourceCosts      = System.Array.Empty<ResourceCostEntry>(),
                Effects = new[]
                {
                    MakeEffect(ActionEffectType.SetHasWeapon)
                }
            };

            // FleeFromEnemy: 도주 — 체력 위기 시 사용
            var fleeEnemy = new ActionDefinition
            {
                ActionId      = "FleeFromEnemy",
                BaseCost      = 3f,  // TODO: 기획팀 수치 확인 필요 — 현재 임시값
                ResourceCosts = System.Array.Empty<ResourceCostEntry>(),
                Effects       = System.Array.Empty<ActionEffect>()
            };

            // AlertVillage: 마을 경보 (최후 수단)
            var alertVillage = new ActionDefinition
            {
                ActionId      = "AlertVillage",
                BaseCost      = 1f,  // TODO: 기획팀 수치 확인 필요 — 현재 임시값
                ResourceCosts = System.Array.Empty<ResourceCostEntry>(),
                Effects       = System.Array.Empty<ActionEffect>()
            };

            // MoveToTarget: 특정 위치로 이동
            var moveToTarget = new ActionDefinition
            {
                ActionId      = "MoveToTarget",
                BaseCost      = 5f,
                ResourceCosts = System.Array.Empty<ResourceCostEntry>(),
                Effects       = System.Array.Empty<ActionEffect>()
            };

            // ──────────────────────────────────────────────────────────────────
            // 전체 배열 조합 — 생성 순서대로 Inspector에 표시됨
            // ──────────────────────────────────────────────────────────────────
            return new ActionDefinition[]
            {
                // 생존 Action
                eatCooked, eatRaw, harvestBerries, cookMeal, sleep, restOnGround, seekMedical, moveToBase,
                // 자원 수집 Action
                chopWood, mineStone, mineIron, mineCopper, pickUpItem, explore,
                // 건설 Action
                buildCampfire, buildHouse, buildStorehouse, buildTownHall, buildForge, buildWatchtower,
                // 전투 / 기타 Action
                attackEnemy, craftWeapon, fleeEnemy, alertVillage, moveToTarget
            };
        }
#endif

        #endregion
    }
}
