---
name: project-implementation-phase4
description: 4단계 구현 완료 — ActionDatabase(ScriptableObject), BuildingQueue, VillagerFSM 더미 교체
metadata:
  type: project
---

4단계 구현 완료 (2026-06-26). ActionDefinition, ActionDatabase, BuildingQueue 신규 생성 + VillagerFSM 3개 더미 메서드 교체.

**Why:** 2~3단계의 하드코딩 더미 예약/효과/플래닝 로직을 데이터 기반으로 전환하여 기획팀이 Inspector에서 수치를 편집할 수 있게 한다.

**How to apply:** 5단계(GOAP Job System) 착수 전에 이 구조를 기준으로 코드를 읽어야 한다.

## 신규 파일

### `Assets/Scripts/Core/ActionDefinition.cs`
- `namespace AIVillage.Core`
- `ActionEffectType` 열거형: GainResource, ConsumeResource, ReduceHunger, ReduceFatigue, RestoreFatigue, GainHealth, IncreaseFatigue, GainMood, SetAtBase, SetNearFireplace, SetCampfireBuilt, SetHouseBuilt, SetStorehouseBuilt, SetTownHallBuilt, SetForgeBuilt, SetWatchtowerBuilt, SetHasTool, SetHasPrimitiveWeapon, SetHasWeapon, DiscoverNearby
- `ResourceCostEntry` struct: (ResourceType, float Amount)
- `ActionEffect` struct: (ActionEffectType, ResourceType, float Amount)
- `ActionDefinition` class: ActionId, BaseCost, Requires* 15개 bool, ResourceCosts[], Effects[]

### `Assets/Scripts/Core/ActionDatabase.cs`
- `namespace AIVillage.Core`
- `[CreateAssetMenu(fileName = "ActionDatabase", menuName = "AIVillage/Action Database")]`
- 핵심 공개 API:
  - `GetAction(actionId)` → ActionDefinition (null 가능)
  - `TryGetAction(actionId, out def)` → bool
  - `GetCostForRole(actionId, role)` → float (역할 보정 적용)
  - `GetDefaultActionSequence(goalId, role, brain, worldState, registry)` → string[]
- #if UNITY_EDITOR ContextMenu: `"기본 Action 정의 생성 (에셋 비어있을 때만 사용)"`
  - GDD v0.4 전체 25개 ActionDefinition 생성 후 에셋 저장
- 내부 플래닝 메서드: Plan_SurviveHunger, Plan_SurviveInjury, Plan_SurviveFatigue, Plan_GatherResources, Plan_BuildStructure, Plan_DefendVillage, Plan_CookMeal

### `Assets/Scripts/Core/BuildingQueue.cs`
- `namespace AIVillage.Core`
- MonoBehaviour 싱글턴 (`Instance` 프로퍼티)
- `BuildingStatus` enum: Pending, InProgress, Completed
- `BuildingQueueEntry` class: QueueId(GUID), BuildingId, TargetTileX/Y, AssignedVillagerIds(max 3), ProgressPercent, Status
- 핵심 공개 API:
  - `EnqueueBuilding(buildingId, x, y)` → bool
  - `GetNextPendingBuilding()` → BuildingQueueEntry (null 가능)
  - `TryAssignVillager(queueId, villagerId)` → bool (MAX_WORKERS=3 초과 시 false)
  - `UnassignVillager(queueId, villagerId)`
  - `GetSpeedMultiplier(queueId)` → float
  - `CompleteBuilding(queueId, worldState)`
  - `GetAllEntries()` → IReadOnlyList
  - `PurgeCompletedEntries()`
- 속도 배율: 0명=0f, 1명=1.0f, 2명=1.5f, 3명=1.8f, 4명+=2.0f

## 수정 파일: `Assets/Scripts/AI/VillagerFSM.cs`

### 신규 private 필드
```csharp
private ResourceCostEntry[] _pendingResourceCosts; // 다중 자원 예약 추적
private ActionDatabase _actionDatabase;
private BuildingQueue  _buildingQueue;
```

### InjectDependencies() 시그니처 변경
```csharp
// 이전
public void InjectDependencies(ResourceRegistry registry)
// 이후 (actionDatabase, buildingQueue는 null 허용 — null이면 더미 폴백)
public void InjectDependencies(ResourceRegistry registry, ActionDatabase actionDatabase = null, BuildingQueue buildingQueue = null)
```

### 교체된 메서드 3개
1. `TryReserveForAction(actionId)`: ActionDatabase.TryGetAction() 조회 → 다중 자원 예약 + 롤백. null이면 기존 단일 더미 switch 폴백.
2. `OnActionCompleted(actionId)`: _pendingResourceCosts 있으면 다중 Commit, 없으면 단일 Commit.
3. `ApplyActionEffect(actionId)`: ActionDatabase.TryGetAction() → Effects[] 순회 처리. null이면 기존 더미 switch 폴백.
4. `ReleaseCurrentReservation()`: _pendingResourceCosts 있으면 다중 Release.
5. `SimulatePlanResult(goalId)`: ActionDatabase 있으면 GetDefaultActionSequence() 위임, 없으면 기존 더미 폴백.
6. 신규 `CalculateTotalCost(sequence, role)`: 시퀀스 전체 비용 합산.

## GameManager 연동 변경
```csharp
// 이전 (3단계)
villagerFSM.InjectDependencies(_registry);
// 이후 (4단계)
villagerFSM.InjectDependencies(_registry, _actionDatabase, _buildingQueue);
// _actionDatabase: Inspector에서 ActionDatabase 에셋 참조
// _buildingQueue:  BuildingQueue.Instance 또는 Inspector 참조
```

## Inspector 셋업 순서
1. Assets/ScriptableObjects/ 폴더에 ActionDatabase 에셋 생성 (Create > AIVillage > Action Database)
2. 에셋 선택 → ContextMenu "기본 Action 정의 생성" 실행
3. Scene에 빈 GameObject "BuildingQueue" 생성 → BuildingQueue 컴포넌트 부착
4. GameManager Inspector: _actionDatabase 슬롯에 에셋 드래그
5. VillagerFSM.InjectDependencies() 호출 시 두 참조 함께 전달

## 미완료 항목 (5단계 대기)
- FoW/TileMap 미구현으로 DiscoverNearby 효과는 Debug.Log만 출력
- WatchtowerBuilt 플래그 WorldState에 없음 — 미래 확장 시 추가 필요
- AttackEnemy, CraftWeapon, FleeFromEnemy, AlertVillage BaseCost는 TODO 임시값
- Brain.NearBerryBush, NearTree, NearRock, NearIronOre, NearCopperOre 플래그 없음
  (현재 NearResource 단일 bool로 통합 — 5단계 SensorSystem 구현 시 세분화 필요)
