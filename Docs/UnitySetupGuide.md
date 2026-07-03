

# AI Village — Unity 씬 설정 가이드

**기준 버전:** VillageAdvisor 단계 완료 (2026-06-28)
**Unity 버전:** 2021.3 LTS, URP

> 이 문서는 코딩 단계가 추가될 때마다 업데이트된다.
> 새 스크립트가 생길 때마다 "어떻게 씬에 올리는지"를 이 문서에 추가할 것.

---

## 0. 현재 구현된 스크립트 목록

| 파일 | 역할 | 씬 배치 방식 |
|---|---|---|
| `ResourceType.cs` | enum 정의만 | 배치 불필요 |
| `WorldStateIndices.cs` | 상수 정의만 | 배치 불필요 |
| `AuthoritativeWorldState.cs` | 자원 재고 싱글턴 (순수 C#) | GameManager가 코드로 생성 |
| `WorldStateSnapshot.cs` | Job용 읽기 전용 복사본 | 배치 불필요 (코드 생성) |
| `ResourceRegistry.cs` | 자원 예약 관리자 (순수 C#) | GameManager가 코드로 생성 |
| `MessageBus.cs` | AI 메시지 브로커 | **씬에 컴포넌트로 배치 (ExecutionOrder -100)** |
| `ActionDefinition.cs` | Action 데이터 구조 | 배치 불필요 |
| `ActionDatabase.cs` | Action 정의 ScriptableObject | **에셋 생성 후 GameManager에 연결** |
| `BuildingQueue.cs` | 건설 대기열 관리자 | **씬에 컴포넌트로 배치 (ExecutionOrder -90)** |
| `SensorSystem.cs` | 환경 감지 MonoBehaviour | **씬에 컴포넌트로 배치** |
| `ResourceNode.cs` | 타일 좌표계 자원 노드 | GameManager가 코드로 생성 |
| `GameManager.cs` | 중앙 초기화·틱 관리자 | **씬에 컴포넌트로 배치 (ExecutionOrder -80)** |
| `IAutonomousAgent.cs` | 인터페이스 | 배치 불필요 |
| `VillagerEnums.cs` | enum/struct 정의만 | 배치 불필요 |
| `VillagerBrain.cs` | 주민 런타임 데이터 | VillagerFSM 내부에서 생성 |
| `ConflictScoreCalculator.cs` | ConflictScore 계산 유틸 | 배치 불필요 |
| `VillagerFSM.cs` | 주민 AI FSM | **주민 GameObject에 부착** |
| `FactionAI.cs` | 팩션 침략 AI (5초 틱) | **팩션 GameObject에 부착 (ExecutionOrder -70)** |
| `EnemyBrain.cs` / `EnemyFSM.cs` | 적 유닛 AI | **적 GameObject에 부착** |
| `VillageAdvisor.cs` | 자율 건물 결정 (5초 틱) | **`_Managers` 하위 GameObject에 부착 (ExecutionOrder -65)** |
| `GOAPPlanningSlots.cs` 외 GOAP 3개 | GOAP A* 플래너 | 배치 불필요 (순수 코드) |

---

## 1. 씬 Hierarchy 구조

Unity에서 아래 구조로 GameObject를 만든다.

```
[씬 루트]
├── _Managers                  ← 빈 GameObject (관리 컴포넌트 모음)
│   ├── MessageBus             ← MessageBus 컴포넌트 부착 (ExecutionOrder -100)
│   ├── BuildingQueue          ← BuildingQueue 컴포넌트 부착 (ExecutionOrder -90)
│   ├── GameManager            ← GameManager 컴포넌트 부착 (ExecutionOrder -80)
│   ├── SensorSystem           ← SensorSystem 컴포넌트 부착 (ExecutionOrder -85)
│   └── VillageAdvisor         ← VillageAdvisor 컴포넌트 부착 (ExecutionOrder -65, 코드 자동)
│
├── Factions                   ← 빈 GameObject (팩션 묶음)
│   ├── Faction_Forest         ← FactionAI 부착 (factionId=0, ExecutionOrder -70)
│   ├── Faction_Iron           ← FactionAI 부착 (factionId=1)
│   └── Faction_Merchant       ← FactionAI 부착 (factionId=2)
│
└── Villagers                  ← 빈 GameObject (주민 묶음)
    ├── Villager_01            ← VillagerFSM 부착 (TickGroupIndex=0)
    ├── Villager_02            ← VillagerFSM 부착 (TickGroupIndex=1)
    └── ...
```

> **VillagerBrain은 더 이상 Inspector에서 직접 부착하지 않는다.**
> VillagerFSM.Awake()가 내부적으로 new VillagerBrain()을 생성하므로
> VillagerFSM 하나만 부착하면 된다.

### 만드는 순서

1. **Hierarchy 창 우클릭 → Create Empty** → 이름 `_Managers`
2. `_Managers` 선택 → 우클릭 → Create Empty × 4: `MessageBus`, `BuildingQueue`, `GameManager`, `SensorSystem`
3. 각 오브젝트 선택 → Inspector → Add Component → 동일 이름 스크립트 추가
4. Hierarchy 우클릭 → Create Empty → 이름 `Villagers`
5. `Villagers` 우클릭 → Create Empty → 이름 `Villager_01`
6. `Villager_01` 선택 → Add Component → `VillagerFSM` 추가
7. 주민이 여러 명이면 TickGroupIndex를 0~5 순으로 다르게 설정

---

## 2. Script Execution Order 설정

여러 싱글턴이 Awake 순서에 의존하기 때문에 실행 순서를 명시적으로 지정해야 한다.

**설정 위치:** Edit → Project Settings → Script Execution Order

| 스크립트 | 실행 순서 값 | 이유 |
|---|---|---|
| `MessageBus` | **-100** | 가장 먼저 초기화 — VillagerFSM이 Publish할 수 있어야 함 |
| `BuildingQueue` | **-90** | MessageBus 다음 — ActionDatabase가 Instance 조회 |
| `GameManager` | **-80** | AuthoritativeWorldState·ResourceRegistry 생성 및 SensorSystem 초기화 |
| `SensorSystem` | 0 (기본값) | GameManager.Awake 이전에 Awake가 완료되도록 권장: -85 설정 |
| `VillagerFSM` | 0 (기본값) | GameManager.Start()에서 InjectDependencies를 받은 뒤 동작 |

### 설정 방법
1. Edit 메뉴 → Project Settings
2. 왼쪽 목록에서 **Script Execution Order** 클릭
3. 우측 하단 **+** 버튼 → `MessageBus` 검색 → 선택
4. 값 필드에 `-100` 입력
5. `BuildingQueue`도 동일하게 `-90` 설정
6. **Apply** 클릭

---

## 3. ActionDatabase 에셋 생성

ActionDatabase는 ScriptableObject다. Unity 씬에 배치하는 것이 아니라 Project 창에 파일로 만든다.

### 생성 방법
1. **Project 창** (Assets 폴더가 보이는 창) 에서 원하는 폴더 선택
   - 추천 경로: `Assets/Data/` (없으면 새로 만들기)
2. 해당 폴더에서 **우클릭 → Create → AIVillage → Action Database**
3. 파일 이름: `ActionDatabase` (기본값 유지)
4. 생성된 `ActionDatabase` 에셋 선택
5. Inspector 우측 상단 **점 세 개(⋮) 메뉴** 클릭 → **"기본 Action 정의 생성 (에셋 비어있을 때만 사용)"** 클릭
6. Console 창에 "기본 Action 정의 N개 생성 완료" 메시지 확인

> **주의:** 이 에셋은 게임 플레이 전에 반드시 생성해야 한다.
> GameManager에서 [SerializeField]로 연결할 예정이다.

---

## 4. VillagerBrain Inspector 설정

`Villager_01` 오브젝트에 VillagerBrain이 붙어 있으면 Inspector에서 설정할 수 있다.

| Inspector 필드 | 설정값 (예시) | 설명 |
|---|---|---|
| Villager Id | `villager_01` | 주민 고유 ID (겹치지 않게) |
| Role | `Lumberjack` | 역할 (None/Lumberjack/Miner/Builder/Warrior/Medic/Cook) |
| Health Level | `100` | 초기 체력 (0~100) |
| Hunger Level | `0` | 초기 허기 (0~100, 100이 되면 위험) |
| Fatigue Level | `0` | 초기 피로 (0~100) |
| Loyalty Level | `70` | 초기 충성도 (0~100) |
| Tile X / Tile Y | `0` / `0` | 초기 위치 (타일 좌표) |

---

## 5. VillagerFSM Inspector 설정

같은 `Villager_01` 오브젝트의 VillagerFSM Inspector.

| Inspector 필드 | 설정값 | 설명 |
|---|---|---|
| Tick Group Index | `0` ~ `5` | 주민마다 다르게 설정 (Tick 분산) |
| Base Tile X | `0` | 기지 위치 X |
| Base Tile Y | `0` | 기지 위치 Y |

> **Tick Group Index:** 주민이 6명이면 각각 0, 1, 2, 3, 4, 5로 설정.
> 이렇게 하면 60fps 기준 프레임당 1/6 주민만 AI를 계산해서 성능 절약.

---

## 6. GameManager 설정 (6A단계 완료)

GameManager가 구현되었다. Inspector에서 아래 항목만 연결하면 된다.

### Inspector 연결 (필수)

| Inspector 필드 | 연결 대상 | 설명 |
|---|---|---|
| `_actionDatabase` | ActionDatabase ScriptableObject | 에셋 생성 후 드래그 (§3 참조) |

### Inspector 조정 가능 항목 (선택)

| 필드 | 기본값 (GDD v0.4) | 설명 |
|---|---|---|
| Initial Wood Stock | 10 | 시작 나무 재고 |
| Initial Stone Stock | 5 | 시작 돌 재고 |
| Initial Raw Food Stock | 30 | 시작 생 식량 재고 |
| Initial Cooked Food Stock | 0 | 시작 조리 식량 재고 |
| Initial Iron Stock | 0 | 시작 철 재고 |
| Initial Copper Stock | 0 | 시작 구리 재고 |
| Base Tile X / Y | 0, 0 | 기지 타일 좌표 |
| Base Discover Radius | 5 | 게임 시작 시 탐험된 반경 |
| Game Time Scale | 0.01 | 1실제초 = 0.01 게임일 |

### 자동 처리 (코드)

GameManager.Awake/Start에서 자동으로 처리하는 것들:
- AuthoritativeWorldState, ResourceRegistry 생성
- ResourceNode 7개 생성 및 SensorSystem 등록 (Wood×3, Stone×2, Iron×1, Copper×1)
- 기지 주변 반경 IsDiscovered=true 설정
- 씬의 모든 VillagerFSM 자동 수집 및 의존성 주입
- MessageBus VillagerDied / OrderRefused 이벤트 구독
- 0.1초 게임 틱 코루틴 시작

### 디버그

Play Mode 중 GameManager Inspector 우클릭 → **"DEBUG: 시스템 상태 출력"** 클릭 시
현재 자원 재고·등록 주민 수·GameTime 등을 Console에 출력한다.

---

## 7. MessageBus Tick 호출 (GameManager 구현 시)

MessageBus는 자동으로 Update()를 사용하지 않는다.
GameManager의 Tick 코루틴(0.1초 간격)에서 직접 호출해야 한다.

```csharp
// GameManager의 Tick 코루틴 예시
IEnumerator TickCoroutine()
{
    while (true)
    {
        yield return new WaitForSeconds(0.1f);

        // 1. MessageBus 먼저 처리 (High → Medium → Low 순)
        MessageBus.Instance?.ProcessTick();

        // 2. VillagerFSM Tick (그룹 분산)
        int currentGroup = Time.frameCount % 6;
        foreach (var fsm in _villagerFSMs)
        {
            if (fsm.TickGroupIndex == currentGroup)
                fsm.Tick();
        }
    }
}
```

---

## 8. 지금 당장 테스트할 수 있는 최소 씬 구성

GameManager 없이도 아래 구성으로 기본 동작을 확인할 수 있다.

### 최소 구성 단계

1. **위 1~5번 순서대로** MessageBus, BuildingQueue, Villager_01 GameObject 생성
2. `Villager_01`의 VillagerFSM을 볼 수 있는 임시 GameManager 스크립트 생성:

```csharp
// Assets/Scripts/TempGameManager.cs (임시 테스트용)
using UnityEngine;
using AIVillage.Core;
using AIVillage.AI;

public class TempGameManager : MonoBehaviour
{
    [SerializeField] private ActionDatabase _actionDatabase;
    [SerializeField] private VillagerFSM[]  _villagers;

    private ResourceRegistry _registry;

    private void Awake()
    {
        // WorldState 초기화
        AuthoritativeWorldState.SetInstance(new AuthoritativeWorldState());
        _registry = new ResourceRegistry(AuthoritativeWorldState.Instance);

        // VillagerFSM 의존성 주입
        foreach (var fsm in _villagers)
            fsm.InjectDependencies(_registry, _actionDatabase, BuildingQueue.Instance);
    }

    private void Update()
    {
        // 임시: 0.1초마다 MessageBus Tick 호출
        _tickTimer += Time.deltaTime;
        if (_tickTimer >= 0.1f)
        {
            _tickTimer = 0f;
            MessageBus.Instance?.ProcessTick();

            // 모든 Villager Tick (임시 — 성능 분산 없음)
            foreach (var fsm in _villagers)
                fsm.Tick();
        }
    }
    private float _tickTimer;
}
```

3. `_Managers` → Create Empty → 이름 `TempGameManager`
4. TempGameManager 컴포넌트 부착
5. Inspector에서:
   - `_actionDatabase` → ActionDatabase 에셋 드래그
   - `_villagers` → Villager_01의 VillagerFSM 드래그

6. **Play** → Console 창에서 FSM 상태 전이 로그 확인

---

## 변경 이력

## 9. VillageAdvisor 씬 배치 (VillageAdvisor 단계)

### 배치 방법
1. `_Managers` → 우클릭 → Create Empty → 이름 `VillageAdvisor`
2. `VillageAdvisor` 선택 → Add Component → `VillageAdvisor` 검색 후 추가

### Inspector 설정
| 필드 | 기본값 | 설명 |
|---|---|---|
| `_evaluationInterval` | `5` | 건물 필요도 평가 주기(초). 보통 변경 불필요. |

### 자동 처리
- `[DefaultExecutionOrder(-65)]` 코드 어트리뷰트로 실행 순서 자동 적용 (Project Settings 설정 불필요)
- `GameManager.Start()`에서 `FindObjectOfType<VillageAdvisor>()`로 자동 수집됨
- 5초마다 6개 우선순위 규칙 평가 → `BuildingQueue.EnqueueBuilding()` 호출 → `WorldState.BuildingQueued = true`

### 우선순위 규칙 요약
| 우선순위 | 건물 | 조건 |
|---|---|---|
| 1 | Campfire | 주민 ≥ 1 && !CampfireBuilt |
| 2 | House | 주민 ≥ 3 && !HouseBuilt |
| 3 | Storehouse | 자원(Wood+Stone+Iron+Copper+Silver) 합계 ≥ 80 && !StorehouseBuilt |
| 4 | Forge | GameDay ≥ 5 && IronStock > 0 && !ForgeBuilt |
| 5 | Watchtower | 침략 횟수 ≥ 1 && !WatchtowerBuilt |
| 6 | TownHall | GameDay ≥ 15 && !TownHallBuilt |

> 건물 위치는 기지 타일 (0,0)으로 고정. 9단계 UI에서 플레이어 위치 선택 추가 예정.

---

## 10. 버그 9 수정 — BuildingQueueItemView 분리 파일 (2026-06-28)

`BuildingQueuePanel.cs` 내부 중첩 클래스였던 `BuildingQueueItemView`가 별도 파일로 분리됨.

**Unity 후속 작업 (아직 안 했다면):**
1. Unity가 재컴파일 완료 대기
2. Hierarchy 우클릭 → Create Empty → 이름 `BuildingQueueItemPrefab_TEMP`
3. Inspector → Add Component → `BuildingQueueItemView` 검색 후 추가 (이제 목록에 표시됨)
4. `_label` (TMP_Text), `_progressSlider` (Slider) 연결
5. Project 창으로 드래그하여 Prefab 저장 → `Assets/Prefab/` 권장
6. `BuildingQueuePanel` Inspector의 `_queueItemPrefab` 슬롯에 이 Prefab 연결
7. Hierarchy에서 `BuildingQueueItemPrefab_TEMP` GameObject 삭제

---

## 11. Win/Lose 결과 화면 (GameResultPanel) — 11단계

### 새 스크립트
| 파일 | 역할 | 씬 배치 방식 |
|---|---|---|
| `GameResultPanel.cs` | 승리/패배 결과 화면 | **Canvas 하위에 배치** |

### Hierarchy 추가 구조

기존 Canvas 하위에 아래 계층 추가:

```
Canvas
└── GameResultPanel         ← GameResultPanel 컴포넌트 부착
    └── ResultPanelRoot     ← _panelRoot 연결 대상 (평소 SetActive=false)
        ├── TitleText       ← TMP_Text 컴포넌트 (제목: "번영 달성!" 등)
        ├── DescriptionText ← TMP_Text 컴포넌트 (설명문)
        ├── RetryButton     ← Button 컴포넌트 (_retryButton)
        └── MainMenuButton  ← Button 컴포넌트 (_mainMenuButton)
```

### 배치 방법
1. `Canvas` 선택 → 우클릭 → Create Empty → 이름 `GameResultPanel`
2. `GameResultPanel` 선택 → Add Component → `GameResultPanel` 추가
3. `GameResultPanel` 하위에 빈 Panel GameObject (`ResultPanelRoot`) 생성
4. `ResultPanelRoot` 하위에 TMP_Text 2개 (`TitleText`, `DescriptionText`), Button 2개 생성
5. Button 내부에 TMP_Text를 넣어 버튼 레이블 설정 (예: "다시 하기", "메인 메뉴")
6. `GameResultPanel` Inspector에서 필드 5개 연결:
   - `_panelRoot` → `ResultPanelRoot`
   - `_titleText` → `TitleText`
   - `_descriptionText` → `DescriptionText`
   - `_retryButton` → `RetryButton`
   - `_mainMenuButton` → `MainMenuButton`
7. **`ResultPanelRoot` SetActive = false** (Inspector 체크박스 해제) — 게임 시작 시 숨김

### 동작 원리
- `GameManager`가 승리/패배 조건 달성 시 `OnGameResultEvent` 발행 → 틱 루프 정지
- `GameResultPanel.HandleGameResult(result)` 호출 → 텍스트 설정 → `ResultPanelRoot` 활성화
- 재시도/메인 메뉴 버튼 클릭 → 동일 씬 재로드

### Win/Lose 조건 요약 (GDD §11)
| 결과 | 조건 | 화면 제목 |
|---|---|---|
| 번영 달성! | Silver Citadel 완성 + 생존 주민 ≥ 1 | "번영 달성!" |
| 생존 달성! | TownHall 건설 + 주민 5명 이상 + 30일 생존 | "생존 달성!" |
| 전멸 | 생존 주민 = 0 | "전멸..." |
| 패배 | TownHall 파괴 + 생존 주민 ≤ 2 | "패배" |

---

---

## §12 — 12단계: FlowFieldManager 씬 배치 (JPS+FlowField 경로이동)

### 배치할 항목 (1개)

| 역할 | GameObject 이름 | 컴포넌트 |
|---|---|---|
| 적 FlowField 방향 맵 | `_FlowFieldManager` | `FlowFieldManager` |

### 단계별 배치 방법

1. **Hierarchy**에서 `_Managers` 아래에 빈 GameObject 생성 → 이름: `_FlowFieldManager`
2. Inspector에서 **Add Component** → `FlowFieldManager` 검색 후 추가
3. **Script Execution Order 수동 설정 불필요** — `[DefaultExecutionOrder(-75)]`가 코드에 선언되어 있음

### 동작 원리
- `Awake()` 시 BFS로 (0,0) 목표 기준 전체 맵 방향 배열 1회 계산
- `EnemyFSM.State_Moving()`이 매 Tick `GetDirection(Brain.TileX, Brain.TileY)` 호출 → O(1) 방향 조회
- 주민(VillagerFSM)은 JPS 경로탐색 사용 — FlowFieldManager 불필요
- VillagerFSM의 `_walkableGrid`는 첫 `StartPathTo()` 호출 시 자동 초기화 (전체 `true`, 별도 배치 불필요)

### Hierarchy 최종 구조 (추가 항목만)
```
_Managers/
  _FlowFieldManager      ← NEW (FlowFieldManager 컴포넌트)
```

---

---

## §13 — 13단계: 타일 맵 렌더링 + Fog of War (MapConfig·FowManager·MapChunkRenderer)

### 새 스크립트 3개

| 파일 | 역할 | 씬 배치 방식 |
|---|---|---|
| `MapConfig.cs` | 맵 크기·FoW·색상 설정 ScriptableObject | **에셋 생성 후 GameManager에 연결** |
| `FowManager.cs` | FoW 상태 배열 + Shadowcasting LOS | **`_Managers` 하위 GameObject에 부착** |
| `MapChunkRenderer.cs` | Texture2D 픽셀 렌더러 | **`_Managers` 하위 GameObject에 부착** |

### Script Execution Order (13단계 추가분)

| 컴포넌트 | 순서 | 방식 |
|---|---|---|
| MapChunkRenderer | **-60** | 코드 `[DefaultExecutionOrder]` 자동 적용 |
| FowManager | **-55** | 코드 `[DefaultExecutionOrder]` 자동 적용 |

---

### Step 1: MapConfig 에셋 생성

1. **Project 창** 우클릭 → **Create → AI Village → MapConfig**
2. 파일 이름: `MapConfig` (기본값 유지)
3. 생성된 `MapConfig.asset`을 선택하여 Inspector에서 설정 확인:
   - `mapSize` = 100, `mapOffset` = 50
   - `initialRevealRadius` = 15 (기지 주변 초기 공개 반경)
   - `villagerSightRadius` = 10 (주민 시야 반경)
   - `grassColor` = (100, 180, 80, 255), `forestColor` = (40, 110, 40, 255)
   - `fowUnexplored` = (0, 0, 0, 255), `fowExplored` = (80, 80, 80, 180)

---

### Step 2: FowManager 배치

1. `_Managers` 선택 → 우클릭 → **Create Empty** → 이름: `_FowManager`
2. `_FowManager` 선택 → **Add Component** → `FowManager` 검색 후 추가
3. `[DefaultExecutionOrder(-55)]` 코드에 선언되어 있으므로 Project Settings 설정 불필요

---

### Step 3: MapChunkRenderer 및 MapQuad 배치

#### 3-A: MapChunkRenderer 오브젝트 생성
1. `_Managers` 선택 → 우클릭 → **Create Empty** → 이름: `_MapChunkRenderer`
2. `_MapChunkRenderer` 선택 → **Add Component** → `MapChunkRenderer` 검색 후 추가
3. `[DefaultExecutionOrder(-60)]` 코드에 선언되어 있으므로 Project Settings 설정 불필요

#### 3-B: MapQuad 생성 (타일 맵을 덮을 Quad)
1. **Hierarchy 루트**에서 우클릭 → **3D Object → Quad** → 이름: `MapQuad`
2. `MapQuad` Transform 설정:
   - **Position:** (0, 0, 1) — 씬의 타일 평면보다 Z=1 앞에 배치하여 타일 위에 오버레이
   - **Rotation:** (0, 0, 0)
   - **Scale:** (100, 100, 1) — mapSize=100 타일 맵 전체를 덮음

#### 3-C: Unlit 머티리얼 생성
1. **Project 창** → `Assets/Materials/` 폴더 생성 (없으면 생성)
2. `Materials` 폴더 우클릭 → **Create → Material** → 이름: `MapMaterial`
3. `MapMaterial` 선택 → Inspector에서 **Shader** 드롭다운 → **Unlit → Texture** 선택
4. `MapQuad` 선택 → Inspector의 **MeshRenderer** 컴포넌트 → **Materials** → `MapMaterial` 드래그

#### 3-D: MapChunkRenderer Inspector 연결
1. `_MapChunkRenderer` 선택
2. `MapChunkRenderer` 컴포넌트의 **_quad** 슬롯에 `MapQuad`의 **MeshRenderer** 컴포넌트 드래그

---

### Step 4: GameManager Inspector 연결

`_GameManager` (또는 GameManager 컴포넌트가 부착된 오브젝트) 선택:

| Inspector 슬롯 | 연결할 에셋/오브젝트 |
|---|---|
| `_Map Config` | `MapConfig.asset` |

> 기존에 있던 `_actionDatabase`, `_villagerPrefab` 등 다른 슬롯은 그대로 유지한다.

---

### Step 5: 동작 확인

**Play** 후 다음을 확인한다:

1. Console 로그:
   - `[GameManager] MapConfig 활성화 완료. mapSize=100, initialRevealRadius=15`
   - `[FowManager] Awake 완료. 맵=100×100, offset=50.`
   - `[MapChunkRenderer] Start 완료. Texture2D=100×100 (RGBA32, Point).`
   - `[FowManager] 초기 시야 공개 완료. 기지=(0,0), 반경=15.`

2. Scene View에서:
   - MapQuad가 타일 맵 전체를 덮고 있어야 함
   - 기지(0,0) 주변 반경 15타일이 밝게 표시됨 (초록색 Grass)
   - 나머지 영역은 검정 (미탐험)

3. 주민 이동 시:
   - 주민이 이동하는 경로를 따라 FoW가 열림
   - 주민이 떠난 자리는 반투명 회색(탐험됨)으로 변함

---

### Hierarchy 최종 구조 (13단계 추가분)

```
[씬 루트]
├── _Managers
│   ├── ...기존 항목...
│   ├── _MapChunkRenderer    ← NEW (MapChunkRenderer 컴포넌트, ExecutionOrder -60)
│   └── _FowManager          ← NEW (FowManager 컴포넌트, ExecutionOrder -55)
│
└── MapQuad                  ← NEW (MeshFilter+MeshRenderer, Scale 100×100×1, Position Z=1)
```

---

### 자주 발생하는 문제

| 문제 | 원인 | 해결 방법 |
|---|---|---|
| `MapConfig.Active가 null` 오류 | GameManager의 `_mapConfig` 슬롯 미연결 | MapConfig.asset을 슬롯에 드래그 |
| 맵이 검정으로만 표시됨 | MapQuad의 MeshRenderer가 `_quad`에 연결 안 됨 | 3-D 단계 재확인 |
| 머티리얼이 핑크색 | Shader가 잘못 설정됨 | 머티리얼 → Shader → Unlit/Texture 선택 |
| FoW가 갱신되지 않음 | `_mapConfig` 슬롯이 null → MapConfig.SetActive 미호출 | Step 4 재확인 |
| Camera 이동에도 갱신 안 됨 | `_cam`이 null (MainCamera 태그 없음) | 카메라에 MainCamera 태그 확인 |

---

---

## §15 — 15단계: 주민 모집 시스템 (VillagerRecruitData·RecruitmentSystem·RecruitmentPanel)

### 새 파일 3개

| 파일 | 역할 | 씬 배치 방식 |
|---|---|---|
| `VillagerRecruitData.cs` | 모집 항목 1개 정의 ScriptableObject | **에셋 생성 후 RecruitmentPanel에 연결** |
| `RecruitmentSystem.cs` | 모집 처리 싱글턴 (-60 실행순서) | **`_Managers` 하위 GameObject에 부착** |
| `RecruitmentPanel.cs` | 모집 UI 패널 (0.5초 폴링) | **Canvas 하위 GameObject에 부착 (초기 비활성)** |

---

### Step 1 — Villager Prefab 생성 (선행 필수)

RecruitmentSystem이 `Instantiate(villagerPrefab, ...)` 을 호출하므로 Prefab이 반드시 있어야 한다.

1. Hierarchy에서 기존 `Villager_01` 오브젝트 선택
2. **Project 창의 `Assets/Prefab/` 폴더** 로 드래그 → Prefab 파일 생성 (`VillagerPrefab.prefab`)
3. 씬의 원본 `Villager_01`은 그대로 유지한다 (기존 테스트 주민)

> Prefab에 VillagerFSM 컴포넌트가 있어야 한다. 없으면 RecruitmentSystem이 실패 로그를 출력하고 자원을 차감하지 않는다.

---

### Step 2 — VillagerRecruitData ScriptableObject 에셋 생성

Project 창 우클릭 → **Create → AIVillage → VillagerRecruitData** 로 역할별 에셋을 생성한다.

**최소 권장 에셋 목록:**

| 파일명 | recruitId | displayName | role | unlockRequirement | 비용 |
|---|---|---|---|---|---|
| `Recruit_Lumberjack_T1` | `lumberjack_t1` | 나무꾼 | Lumberjack | TownHall | 식량 15 |
| `Recruit_Miner_T1` | `miner_t1` | 광부 | Miner | TownHall | 식량 15 |
| `Recruit_Cook_T1` | `cook_t1` | 요리사 | Cook | TownHall | 식량 20 |
| `Recruit_Builder_T1` | `builder_t1` | 건설자 | Builder | TownHall | 식량 15, 철 2 |
| `Recruit_Explorer_T1` | `explorer_t1` | 탐험가 | Explorer | TownHall | 식량 20 |
| `Recruit_Warrior_T1` | `warrior_t1` | 전사 | Warrior | Forge | 식량 25, 철 5 |
| `Recruit_Warrior_T2` | `warrior_t2` | 숙련 전사 | Warrior | Forge | 식량 30, 철 8, 구리 3 |

**공통 설정 값 (대부분 에셋에 동일 적용):**

| 필드 | 기본값 | 설명 |
|---|---|---|
| healthMin / Max | 70 / 90 | 체력 스폰 범위 |
| hungerMin / Max | 10 / 30 | 배고픔 스폰 범위 |
| fatigueMin / Max | 5 / 25 | 피로 스폰 범위 |
| moodMin / Max | 60 / 80 | 기분 스폰 범위 |
| loyaltyMin / Max | 40 / 70 | 충성도 스폰 범위 |
| startWithTool | true | 나무꾼·광부·건설자·탐험가 |
| startWithWeapon | true | 전사 계열만 |
| attackModifier | 1.0 (T1), 1.35 (T2 전사) | 공격력 배율 |

---

### Step 3 — _RecruitmentSystem GameObject 배치

1. Hierarchy에서 `_Managers` 선택 → 우클릭 → **Create Empty** → 이름: `_RecruitmentSystem`
2. `_RecruitmentSystem` 선택 → **Add Component** → `RecruitmentSystem` 검색 후 추가
3. Inspector 설정:

| 필드 | 값 | 설명 |
|---|---|---|
| `_spawnOffset` | (0, 0) | 기지 타일 중심 스폰 |
| `_spawnJitterRadius` | 0.4 | 동시 스폰 시 겹침 방지 |

> `[DefaultExecutionOrder(-60)]` 이 코드에 선언되어 있으므로 Project Settings 설정 불필요.

---

### Step 4 — RecruitmentPanel UI 생성

#### 4-A: Hierarchy 구조 생성

```
Canvas
└── RecruitmentPanel             ← RecruitmentPanel 컴포넌트, 초기 SetActive = false
    ├── Button_Slot0             ← Button 컴포넌트
    │   ├── NameText_0           ← TMP_Text (역할명 표시)
    │   └── CostText_0           ← TMP_Text (비용 표시, 예: "식량 15 | 철 2")
    ├── Button_Slot1
    │   ├── NameText_1
    │   └── CostText_1
    ... (에셋 수에 맞게, 최대 8개)
```

#### 4-B: 각 Button 생성 방법

1. `RecruitmentPanel` 하위 → 우클릭 → **UI → Button - TextMeshPro** → 이름: `Button_Slot0`
2. `Button_Slot0` 기본 Text를 삭제하고 TMP_Text 2개(`NameText_0`, `CostText_0`)를 수동 추가
3. 에셋 수만큼 반복 (예: 7개면 Button_Slot0 ~ Slot6)

#### 4-C: RecruitmentPanel Inspector 연결

| 슬롯 | 연결 대상 |
|---|---|
| `_recruitOptions` (배열 크기 = 에셋 수) | Step 2에서 만든 VillagerRecruitData 에셋 |
| `_villagerPrefab` | Step 1 VillagerPrefab |
| `_recruitButtons` (배열) | Button_Slot0 ~ SlotN |
| `_nameTexts` (배열) | NameText_0 ~ N |
| `_costTexts` (배열) | CostText_0 ~ N |
| `_refreshInterval` | 0.5 (기본값 유지) |

#### 4-D: 패널 초기 비활성화

- `RecruitmentPanel` GameObject 선택 → Inspector 상단 체크박스 해제 (**SetActive = false**)
- TownHall 완성 전까지 패널이 숨겨진 상태로 있어야 한다.

---

### Step 5 — HUDManager Inspector 슬롯 연결

`_UIManager` 오브젝트 선택 → HUDManager 컴포넌트에서:

| 슬롯 | 연결 대상 |
|---|---|
| `_recruitmentPanel` | `RecruitmentPanel` GameObject |

---

### Step 6 — 동작 테스트 (Debug ContextMenu 활용)

**TownHall 완성 시뮬레이션:**

1. Play 진입
2. Hierarchy에서 GameManager 오브젝트 선택
3. Inspector 우클릭 → **"DEBUG: 15단계 테스트 — TownHall 완성 강제 설정"** 클릭
4. 확인 사항:
   - Console: `[HUDManager] TownHall 완성 감지 — RecruitmentPanel 활성화.`
   - Scene: RecruitmentPanel이 화면에 나타남
   - 모든 버튼이 `interactable = false` (자원 부족 상태)

**모집 자원 보충 및 버튼 활성화 확인:**

5. Inspector 우클릭 → **"DEBUG: 15단계 테스트 — 모집 자원 보충 (식량/철/구리/은)"** 클릭
6. 0.5초 내 TownHall 해금 버튼(나무꾼·광부·요리사 등)이 `interactable = true` 로 바뀜

**Forge / Watchtower 해금 확인:**

7. Inspector 우클릭 → **"DEBUG: 15단계 테스트 — Forge 완성 강제 설정"** 클릭
8. Forge 해금 모집 항목(전사 T1·T2) 버튼 활성화 확인

**실제 모집 실행:**

9. 활성화된 버튼 클릭
10. Console: `[RecruitmentSystem] 모집 완료. recruitId=..., 역할=..., 이름=...`
11. Hierarchy: 새 Villager GameObject 스폰 확인
12. 자원 HUD에서 조리식량·철 감소 확인

---

### Hierarchy 최종 구조 (15단계 추가분)

```
[씬 루트]
├── _Managers
│   ├── ...기존 항목...
│   └── _RecruitmentSystem       ← NEW (RecruitmentSystem 컴포넌트, ExecutionOrder -60)
│
└── Canvas
    ├── ...기존 UI 패널...
    └── RecruitmentPanel         ← NEW (RecruitmentPanel 컴포넌트, 초기 비활성)
        ├── Button_Slot0
        │   ├── NameText_0
        │   └── CostText_0
        ... (에셋 수만큼)
```

---

### 자주 발생하는 문제

| 문제 | 원인 | 해결 방법 |
|---|---|---|
| `_villagerPrefab이 null` 오류 | RecruitmentPanel Inspector 미연결 | Step 1 Prefab을 _villagerPrefab 슬롯에 드래그 |
| 버튼이 전혀 안 보임 | _recruitOptions 배열 미설정 | Step 2 에셋을 _recruitOptions 배열에 드래그 |
| 버튼 클릭해도 아무 일 없음 | _recruitButtons 배열 미연결 | Button_Slot0~N을 _recruitButtons에 연결 |
| RecruitmentPanel이 Play 후에도 안 보임 | HUDManager._recruitmentPanel 슬롯 미연결 | Step 5 다시 확인 |
| 모집 후 주민이 씬에 안 보임 | Prefab에 VillagerFSM 없음 | VillagerPrefab에 VillagerFSM 컴포넌트 부착 확인 |

---

## 변경 이력

| 날짜 | 단계 | 추가 내용 |
|---|---|---|
| 2026-06-26 | 1~4단계 | 최초 문서 작성 |
| 2026-06-27 | 5A~6A단계 | SensorSystem·GameManager 추가, Hierarchy 구조 업데이트, Script Execution Order 확정 |
| 2026-06-28 | VillageAdvisor 단계 | VillageAdvisor 씬 배치 가이드 추가, Hierarchy에 Factions·VillageAdvisor 추가 |
| 2026-06-28 | 버그9 수정, 11단계 | BuildingQueueItemView 분리 + GameResultPanel Win/Lose 결과 화면 추가 |
| 2026-06-29 | 12단계 | FlowFieldManager 씬 배치 가이드 추가 |
| 2026-06-29 | 13단계 | MapConfig·FowManager·MapChunkRenderer 추가, JPSPathfinder/FlowFieldManager/VillagerFSM 동적 맵 크기 대응 |
| 2026-07-02 | 15단계 | VillagerRecruitData·RecruitmentSystem·RecruitmentPanel 씬 배치 가이드 추가. GameManager에 TownHall/Forge/Watchtower 완성 강제 설정 DEBUG ContextMenu 4개 추가 |
