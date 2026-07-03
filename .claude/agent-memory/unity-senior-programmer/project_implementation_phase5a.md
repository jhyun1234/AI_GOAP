---
name: project-implementation-phase5a
description: 5A단계 구현 완료 — SensorSystem, ResourceNode, VillagerFSM Brain 프로퍼티 추가
metadata:
  type: project
---

5A단계 구현 완료 (2026-06-26). ResourceNode 신규, SensorSystem 신규, VillagerFSM Brain 프로퍼티 추가로 환경 감지 플래그 실제 갱신 실현.

**Why:** VillagerBrain의 NearRock/NearIronOre/NearCopperOre 등 환경 플래그가 전부 false 고정이어서 Miner 역할이 MineStone/MineIron/MineCopper를 절대 실행하지 못하는 버그 해결.

**How to apply:** GameManager에서 SensorSystem 컴포넌트를 참조하여 RegisterVillager/AddResourceNode를 호출하고, 0.1초 Tick 코루틴에서 UpdateAllSensors()를 호출해야 한다.

## 신규 파일

### `Assets/Scripts/Core/ResourceNode.cs`
- `namespace AIVillage.Core`
- 순수 C# 클래스 (MonoBehaviour 아님)
- 주요 프로퍼티: NodeId(string), ResourceType, TileX, TileY, CurrentAmount, MaxAmount, RegenerationRate, IsDiscovered, IsBeingHarvested, HarvestingVillagerId
- 편의 생성자: ResourceNode(nodeId, type, tileX, tileY, maxAmount, isDiscovered)
  - RegenerationRate는 GetDefaultRegenRate(type) 자동 적용
- GDD v0.4 재생률 상수: Wood=5f, Stone=3f, Iron=1.5f, Copper=0.5f, Silver=0.2f, RawFood/CookedFood=0f
- 공개 헬퍼: IsAvailableForHarvest(), TryOccupy(villagerId), Release()
- 정적 헬퍼: GetDefaultRegenRate(ResourceType) → float

### `Assets/Scripts/Core/SensorSystem.cs`
- `namespace AIVillage.Core`
- IEnemyAgent 인터페이스도 같은 파일 내 정의 (TileX, TileY, IsAlive)
- `[DisallowMultipleComponent]` MonoBehaviour
- 감지 반경 SerializeField: _resourceSensingRadius=5, _enemyDetectionRadius=8, _buildingSensingRadius=3, _droppedItemRadius=4
- 건물 Transform SerializeField: _baseTransform, _storageTransforms[], _firehouseTransforms[], _bedTransforms[], _healerTransforms[], _watchtowerTransforms[]
- 성능 패턴: Dictionary + 캐시 List + dirty 플래그 조합으로 GC 알로케이션 없이 동작
  - _villagerMap(Dictionary) + _villagerList(캐시) + _villagerListDirty
  - _nodeMap(Dictionary) + _nodeList(캐시) + _nodeListDirty
  - 건물 Transform[] → int[] 타일 캐시 (Awake에서 한 번만 변환)
- 공개 API:
  - RegisterVillager(VillagerFSM) / UnregisterVillager(villagerId)
  - RegisterEnemy(IEnemyAgent) / UnregisterEnemy(IEnemyAgent)
  - AddResourceNode(node) / RemoveResourceNode(nodeId) / GetResourceNode(nodeId) → ResourceNode
  - GetAllNodes() → IReadOnlyList<ResourceNode>
  - UpdateAllSensors() — GameManager 0.1초 Tick에서 호출
  - TickResourceRegeneration(deltaGameTime) — GameTimeManager Tick에서 호출
  - DiscoverArea(centerX, centerY, radius) — Explore Action 완료 시 호출
  - InjectWorldState(worldState) — 초기화 순서 보정용
  - RefreshBuildingCache() — 런타임 건물 이동 시
- UpdateAllSensors() 내부 순서:
  1. UpdateResourceFlags — NearRock/NearIronOre/NearCopperOre/NearResource/NearDiscoveredResource
  2. UpdateEnemyFlag — NearEnemy
  3. UpdateDroppedItemFlag — NearDroppedItem (DroppedItem struct, 인덱스 for)
  4. UpdateBuildingFlags — AtBase/NearStorage/NearFireplace/NearBed/NearHealer/NearWatchtower

## 수정 파일: `Assets/Scripts/AI/VillagerFSM.cs`

### 변경 사항: `private VillagerBrain _brain` → `public VillagerBrain Brain { get; private set; }`
- 위치: `#region ── Private Fields ──` 섹션 상단
- private set: Awake()에서만 할당, 외부에서는 읽기 전용
- SensorSystem.RegisterVillager()가 fsm.Brain.VillagerId를 읽고,
  UpdateAllSensors()가 fsm.Brain 을 통해 환경 플래그를 갱신한다.
- 파일 전체 `_brain` → `Brain` 치환 완료 (replace_all)

## GameManager 연동 추가 필요 사항
```csharp
// SensorSystem 컴포넌트 참조 (GameManager Inspector에 할당)
[SerializeField] private SensorSystem _sensorSystem;

// Start() 또는 주민 스폰 시
_sensorSystem.RegisterVillager(villagerFSM);

// 맵 로드 시 ResourceNode 등록
var stoneNode = new ResourceNode(null, ResourceType.Stone, tileX:10, tileY:5, maxAmount:200f, isDiscovered:true);
_sensorSystem.AddResourceNode(stoneNode);

// 0.1초 Tick 코루틴 내
_sensorSystem.UpdateAllSensors();

// 게임 하루 경과 시 (GameTimeManager)
_sensorSystem.TickResourceRegeneration(1.0f);

// 주민 사망 시 (MessageBus VillagerDied 핸들러)
_sensorSystem.UnregisterVillager(villagerId);
```

## Inspector 셋업 순서
1. GameManager GameObject에 SensorSystem 컴포넌트 추가
2. _baseTransform에 기지(Base) GameObject의 Transform 드래그
3. _storageTransforms, _firehouseTransforms 등 각 건물 타입별로 할당
4. GameManager의 _sensorSystem 슬롯에 SensorSystem 컴포넌트 드래그
5. Script Execution Order: GameManager > SensorSystem > VillagerFSM 권장

## 미완료 항목 (5B단계 이후)
- FoW 초기화: 기지 주변 노드 IsDiscovered=true 자동 설정 로직
- Explore Action의 ApplyActionEffect에서 SensorSystem.DiscoverArea() 실제 호출 연결
- NearSilver, NearRawFood 세부 플래그 추가 여부 기획팀 확인 필요
- TODO: 기획팀 — 동시 채집 허용 여부 (현재 TryOccupy로 1명만 허용)
