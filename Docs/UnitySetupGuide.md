# AI Village — Unity 씬 설정 가이드

**기준 버전:** 구현 4단계 완료 (2026-06-26)
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
| `MessageBus.cs` | AI 메시지 브로커 | **씬에 컴포넌트로 배치** |
| `ActionDefinition.cs` | Action 데이터 구조 | 배치 불필요 |
| `ActionDatabase.cs` | Action 정의 ScriptableObject | **에셋 생성 후 GameManager에 연결** |
| `BuildingQueue.cs` | 건설 대기열 관리자 | **씬에 컴포넌트로 배치** |
| `IAutonomousAgent.cs` | 인터페이스 | 배치 불필요 |
| `VillagerEnums.cs` | enum/struct 정의만 | 배치 불필요 |
| `VillagerBrain.cs` | 주민 런타임 데이터 | **주민 GameObject에 부착** |
| `ConflictScoreCalculator.cs` | ConflictScore 계산 유틸 | 배치 불필요 (VillagerFSM이 사용) |
| `VillagerFSM.cs` | 주민 AI FSM | **주민 GameObject에 부착** |

---

## 1. 씬 Hierarchy 구조

Unity에서 아래 구조로 GameObject를 만든다.

```
[씬 루트]
├── _Managers              ← 빈 GameObject (관리 컴포넌트 모음)
│   ├── MessageBus         ← MessageBus 컴포넌트 부착
│   ├── BuildingQueue      ← BuildingQueue 컴포넌트 부착
│   └── GameManager        ← (미구현, 나중에 추가)
│
└── Villagers              ← 빈 GameObject (주민 묶음)
    ├── Villager_01        ← VillagerBrain + VillagerFSM 부착
    ├── Villager_02
    └── ...
```

### 만드는 순서

1. **Hierarchy 창 우클릭 → Create Empty** → 이름 `_Managers`
2. `_Managers` 선택 → 우클릭 → Create Empty → 이름 `MessageBus`
3. `MessageBus` 오브젝트 선택 → Inspector → **Add Component** → `MessageBus` 입력 후 추가
4. `_Managers` 선택 → 우클릭 → Create Empty → 이름 `BuildingQueue`
5. `BuildingQueue` 오브젝트 선택 → Inspector → **Add Component** → `BuildingQueue` 추가
6. Hierarchy 우클릭 → Create Empty → 이름 `Villagers`
7. `Villagers` 오브젝트 우클릭 → Create Empty → 이름 `Villager_01`
8. `Villager_01` 선택 → Add Component → `VillagerBrain` 추가
9. 같은 오브젝트에 Add Component → `VillagerFSM` 추가

---

## 2. Script Execution Order 설정

여러 싱글턴이 Awake 순서에 의존하기 때문에 실행 순서를 명시적으로 지정해야 한다.

**설정 위치:** Edit → Project Settings → Script Execution Order

| 스크립트 | 실행 순서 값 | 이유 |
|---|---|---|
| `MessageBus` | **-100** | 가장 먼저 초기화 — VillagerFSM이 Publish할 수 있어야 함 |
| `BuildingQueue` | **-90** | MessageBus 다음 — ActionDatabase가 Instance 조회 |
| *(GameManager)* | **-80** | 나중에 추가 예정 — AuthoritativeWorldState 생성 |
| `VillagerFSM` | 0 (기본값) | GameManager 이후 초기화되어야 함 |

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

## 6. GameManager 설정 (미구현 — 다음 단계에서 추가)

GameManager는 아직 구현되지 않았다. 아래는 구현될 때 필요한 연결 사항 미리보기다.

```csharp
// GameManager가 Awake에서 해야 할 일:

// 1. AuthoritativeWorldState 생성
AuthoritativeWorldState.SetInstance(new AuthoritativeWorldState());

// 2. ResourceRegistry 생성
var registry = new ResourceRegistry(AuthoritativeWorldState.Instance);

// 3. 씬의 모든 VillagerFSM에 의존성 주입
foreach (var fsm in FindObjectsOfType<VillagerFSM>())
{
    fsm.InjectDependencies(
        registry,
        _actionDatabase,   // Inspector에서 드래그 연결
        BuildingQueue.Instance
    );
}

// 4. MessageBus.Instance.Subscribe로 VillagerDied 구독
MessageBus.Instance.Subscribe(MessageType.VillagerDied, OnVillagerDied);
```

GameManager Inspector에서 연결해야 할 SerializeField:
- `_actionDatabase` → 3번에서 만든 ActionDatabase 에셋을 드래그

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

| 날짜 | 단계 | 추가 내용 |
|---|---|---|
| 2026-06-26 | 1~4단계 | 최초 문서 작성 |

---

> **다음 코딩 단계가 완료되면 이 문서에 추가될 내용:**
> - GameManager 본격 구현 시 → 6번 섹션 업데이트
> - SensorSystem 구현 시 → VillagerBrain의 NearXxx 플래그 갱신 방법 추가
> - GOAPPlannerJob 구현 시 → Job System 씬 설정 추가
