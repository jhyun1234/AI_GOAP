---
name: project-implementation-phase6a
description: 6A단계 구현 완료 — GameManager.cs (중앙 초기화 및 틱 관리자)
metadata:
  type: project
---

## 6A단계: GameManager.cs 구현 완료 (2026-06-27)

**Why:** 1~5B단계에서 완성된 모든 시스템(WorldState, Registry, SensorSystem, MessageBus, BuildingQueue, VillagerFSM, GOAP)을 씬에서 연결하고 0.1초 게임 틱 루프를 구동하는 중앙 관리자 필요.

### 파일 위치

`Assets/Scripts/Core/GameManager.cs`

### 클래스 설계

- `[DefaultExecutionOrder(-80)]` — MessageBus(-100), BuildingQueue(-90) 다음, VillagerFSM(0) 이전
- 네임스페이스: `AIVillage.Core`
- `sealed class GameManager : MonoBehaviour` — 싱글턴

### Awake() 처리 순서 (6단계)

1. 싱글턴 설정 (중복 감지 포함)
2. `new AuthoritativeWorldState()` 생성 + `InitializeWorldState()` (Inspector 수치 주입)
3. `AuthoritativeWorldState.SetInstance(_worldState)` 등록
4. `new ResourceRegistry(_worldState)` 생성 → `Registry` 공개 프로퍼티에 노출
5. `SensorSystem.Instance?.InjectWorldState(_worldState)` — null 안전
6. `CreateAndRegisterDefaultNodes()` — 코드로 7개 기본 노드 생성
7. `SensorSystem.Instance?.DiscoverArea(_baseTileX, _baseTileY, _baseDiscoverRadius)` — FoW 초기화

### Start() 처리 순서

1. `FindObjectsOfType<VillagerFSM>()` — 한 번만 호출 (Start에서만 허용)
2. 각 FSM에 `SensorSystem.RegisterVillager` + `InjectDependencies(registry, actionDatabase, BuildingQueue.Instance)`
3. `MessageBus.Subscribe(VillagerDied, OrderRefused)`
4. `StartCoroutine(GameTickCoroutine())` 시작

### GameTickCoroutine() 틱 순서 (0.1초 간격)

1. `deltaGameDays = 0.1f * _gameTimeScale` + `GameTime += deltaGameDays`
2. `SensorSystem.UpdateAllSensors()` + `TickResourceRegeneration(deltaGameDays)`
3. `MessageBus.ProcessTick()`
4. `TickVillagerGroup(_tickCounter % 6)` + `_tickCounter++`

### 기본 ResourceNode 배치 (기획서 수치)

- Wood 3개: (3,0), (0,3), (-3,0) — MaxAmount=200
- Stone 2개: (5,2), (-5,2) — MaxAmount=150 (맨해튼 거리 7, 반경 5 초과 → 미발견)
- Iron 1개: (8,5) — MaxAmount=80, 미발견
- Copper 1개: (-8,5) — MaxAmount=60, 미발견

Stone 노드 실제 거리: |(±5-0)| + |2-0| = 7 > _baseDiscoverRadius(5) → IsDiscovered=false

### SerializeField Inspector 수치 (GDD v0.4)

- `_initialWoodStock=50, Stone=50, RawFood=30, CookedFood=20, Iron=10, Copper=5`
- `_baseTileX=0, _baseTileY=0, _baseDiscoverRadius=5`
- `_gameTimeScale=0.01f` (100초 = 1게임일)

### 공개 API

- `GameManager.Instance` — 싱글턴
- `float GameTime` — 경과 게임일수 (읽기 전용 프로퍼티)
- `ResourceRegistry Registry` — 외부 시스템용 자원 예약 관리자
- `RegisterNewVillager(VillagerFSM)` — 런타임 동적 주민 추가

### OnDestroy()

1. `UnsubscribeFromMessageBus()` — VillagerDied, OrderRefused 구독 해제
2. `AuthoritativeWorldState.SetInstance(null)` — 싱글턴 해제
3. `Instance = null`

**How to apply:** Inspector 설정 — ActionDatabase ScriptableObject 드래그 필수. SensorSystem, MessageBus, BuildingQueue는 별도 GameObject에 배치.
