---
name: project-core-system-pr-patterns
description: AI Village 코어 시스템(AuthoritativeWorldState, ResourceRegistry 등)에서 리뷰어가 반복 지적한 설계 패턴과 버그 유형
metadata:
  type: project
---

## 핵심 파일 구조 (1단계 코어 시스템)
- AuthoritativeWorldState.cs — 싱글턴, Stock/Reserved 프로퍼티, GetStock/SetStock/GetReserved/SetReserved
- ResourceRegistry.cs — Reserve/Release/ReleaseAll/Commit/ValidateIntegrity
- WorldStateSnapshot.cs — NativeArray<int> 직렬화, IDisposable, TempJob
- WorldStateIndices.cs — NativeArray 슬롯 인덱스 상수, StockIndexOf/ReservedIndexOf

## 반복 발견된 버그 유형

### Reserved > Stock 데이터 오염 (CRITICAL)
Reserved setter에 Stock 상한선이 없으면 GOAP 스냅샷이 가용량 0으로 오염되어 모든 주민이 행동 불능에 빠진다.
수정 패턴: `Mathf.Max(0f, value)` → `Mathf.Clamp(value, 0f, _대응Stock필드)`

### ValidateIntegrity 이중 저장소 미검증 (CRITICAL)
_totalReserved 캐시만 검증하고 _worldState.GetReserved()를 검증하지 않으면 두 저장소가 불일치해도 '통과'가 출력된다.
수정 패턴: foreach 루프 안에 worldState.GetReserved(type) vs calculated 비교 블록 추가 + LogError + 복구

### Commit() void 반환 (WARNING)
설계 명세는 '실패는 false 반환'을 요구한다. void Commit → bool Commit.

### SetInstance null 처리 분리 미비 (WARNING)
null 입력과 교체 입력 처리가 혼재하면 의도가 불명확하다.
수정 패턴: null → 해제만(Log 후 null 할당) + return / non-null → 기존 유무 확인 후 경고

**Why:** 이 패턴들은 첫 번째 PR 리뷰(2026-06-25)에서 Critical/Warning으로 지적됨.
**How to apply:** 향후 유사 클래스 작성 시 Reserved setter, Commit 반환형, ValidateIntegrity 이중 검증, SetInstance 분기를 사전에 확인한다.

---

## VillagerFSM + ConflictScoreCalculator PR 패턴 (2단계, 2026-06-25)

### switch-case 내 dead code (CRITICAL)
같은 case에 두 Action을 묶은 뒤 if(actionId == X)로 분기하면 항상 true인 조건이 되어 dead code 발생.
수정 패턴: case를 분리하고 Action별 상수(const)로 수치 관리.

### 플래그 소비 순서 버그 (CRITICAL)
Brain.HasPendingOrder = false 설정 후 Brain.PendingOrder를 다시 읽으면 덮어쓰기 race condition.
수정 패턴: PlayerOrder localOrder = _brain.PendingOrder 복사 → HasPendingOrder = false → localOrder만 사용.

### 예약 해제 탈출 조건 오류 (CRITICAL)
OnStateExit Executing에서 enteringState != Idle 조건이 있으면 → Idle 전이 시 예약 누수.
수정 패턴: 조건 제거, _hasActiveReservation이 true면 항상 Release.

### MessageBus 없는 2단계 이벤트 패턴 (CRITICAL)
MessageBus 미구현 단계에서 사망 등 중요 이벤트는 public static event로 발행.
패턴: public static event System.Action<string, int, int> OnVillagerDied — 3단계에서 MessageBus 래핑으로 교체.

### DetermineReason fallback 탐지 (WARNING)
모든 임계값 미위반인데 거부 판정이 호출된 경우는 버그 가능성 — fallback 직전에 Debug.LogWarning.

### 공식 근사 주석 필수 (WARNING)
설계서 공식과 구현이 근사 관계일 때는 반드시 주석으로 수식적 동치 또는 근사 이유를 명시한다.
예: Σ(urgency_i × impact_i) → (Σurgency_i) × impact 근사 이유 = 1:1 매핑 불가 구조.

---

## MessageBus PR 패턴 (3단계, 2026-06-26)

### DispatchToSubscribers 역순 루프 안전성 허점 (MAJOR / R-001)
역순(i=Count-1→0) 순회는 '뒤에서부터 제거'할 때만 안전하다.
Unsubscribe()가 List.Remove()로 임의 인덱스를 제거하면 이미 지나간 인덱스가 콜백을 재실행한다.
수정 패턴: 클래스 필드 `_dispatchSnapshot(List<Action<AIMessage>>)`로 Clear+AddRange 스냅샷 후 순회.
실행 전 `callbackList.Contains(callback)`으로 Unsubscribe된 콜백 건너뜀. GC 절약을 위해 매번 new하지 않는다.

### 디버그 통계 초기화 타이밍 버그 (MAJOR / R-002)
ProcessTick() 시작에서 _debugLastTickDedupCount를 초기화하면, Tick 시작 전 이미 Publish()로 누적된
Dedup 카운트가 날아간다. 수정 패턴: ProcessTick() 끝 _pendingDedupSet.Clear()와 함께 초기화.

### DEFAULT_PRIORITY_MAP 미사용 (NOTE / R-007)
우선순위 매핑 테이블이 문서 역할만 하고 실제로 적용되지 않으면 발행자 실수를 막을 수 없다.
수정 패턴: Publish() 진입 직후 TryGetValue로 message.Priority를 테이블 값으로 강제 덮어씌운다.

### Awake 전 Publish 방어 코드 수준 (MINOR / R-005)
초기화 순서 위반은 설계 오류 수준이므로 LogWarning이 아닌 LogError를 사용한다.

### Enum.GetValues GC 할당 (MINOR / R-004)
Awake에서만 호출되므로 영향은 미미하지만 Enum.GetValues()는 매번 새 배열을 할당한다.
명시적 .Add((int)MessagePriority.High/Medium/Low, ...) 3줄이 더 안전하고 명확하다.

### List<T> 초기 용량 힌트 (MINOR / R-003)
드롭 아이템 수가 고정(4종)인 경우 new List<T>(4)로 초기 용량을 지정하면 재할당을 방지한다.
일반 규칙: 최대 원소 수를 예측할 수 있으면 초기 용량 힌트를 항상 제공한다.

---

## ActionDatabase + VillagerFSM PR 패턴 (4단계, 2026-06-26)

### Miner 분기 동일 조건 Dead Code (CRITICAL / R-001)
AgentRole.Miner 플래닝 분기에서 MineCopper/MineIron/MineStone 세 갈래가 모두 동일한 조건
(HasTool && NearResource)을 사용하면 첫 번째 조건이 항상 참이어서 나머지가 Dead Code가 된다.
수정 패턴: VillagerBrain에 NearRock/NearIronOre/NearCopperOre 자원별 전용 플래그 추가.
각 자원 분기에서 해당 전용 플래그를 참조해 조건을 분리한다.

### HasResourcesForBuilding 하드코딩 불일치 (WARNING / R-002)
VillagerFSM.HasResourcesForBuilding()의 더미 수치(Wood>=10, Stone>=5)가 ActionDatabase 실제
건물 비용 테이블과 다르면 Planning→Replanning 루프가 발생한다.
수정 패턴: ActionDatabase에 public bool CanBuildNextBuilding(registry, worldState) 추가.
FSM은 ActionDatabase가 주입된 경우 이 메서드에 위임하고 없을 때만 더미 로직 폴백.

### FoW isDiscovered 필터 누락 (WARNING / R-003)
NearResource 단독 체크는 발견되지 않은 자원 노드도 플래닝에 포함시킨다.
수정 패턴: VillagerBrain에 NearDiscoveredResource 복합 플래그(NearResource AND isDiscovered) 추가.
모든 자원 수집 분기에서 NearResource → NearDiscoveredResource로 교체.
SensorSystem이 매 틱마다 양쪽 조건을 확인해 이 플래그를 갱신한다.

### SimulatePlanResult 복합 null 조건 (WARNING / R-004)
_actionDatabase != null && _worldState != null을 한 조건으로 묶으면, worldState만 null일 때
ActionDatabase 경로를 조용히 포기하고 더미 폴백으로 떨어져 디버깅이 어렵다.
수정 패턴: _actionDatabase != null 체크와 _worldState null 체크를 분리한다.
worldState가 null이면 LogError 출력 후 명시적 폴백.

### ScriptableObject 내부 배열 직접 참조 저장 (SUGGESTION / R-005)
_pendingResourceCosts = def.ResourceCosts 는 ScriptableObject 내부 배열을 직접 가리킨다.
에디터 핫 리로드 시 ScriptableObject가 재로드되면 구 버전 배열 참조가 남는다.
수정 패턴: _pendingResourceCosts = (ResourceCostEntry[])def.ResourceCosts.Clone();

### 목표 무관 폴백 반환 (SUGGESTION / R-006)
GatherStone Goal에서 조건 미충족 시 HarvestWildBerries를 반환하는 것은 Goal 의미와 불일치.
수정 패턴: 돌/철/구리 같은 특수 자원 Goal의 폴백은 System.Array.Empty<string>() (NoSolutionFound).
베리 폴백은 GatherWood처럼 대체 가능한 경우에만 허용한다.

### Deadlock 폴백 Goal 비적절 (SUGGESTION / R-007)
P0 생존 위기가 이미 실패한 Deadlock 상황에서 HungerLevel < 50 → RestOnGround는 Goal을 잃고 표류할 위험.
수정 패턴: 기본 폴백을 MoveToBase로 설정. IsP0GoalActive()가 true이면 GetP0GoalId()를 우선 사용.
**Why:** P0 Goal이 활성이면 생존 Goal 재시도가 더 합리적이고, 아니면 기지 복귀가 가장 안전하다.

---

## SensorSystem PR 패턴 (5단계, 2026-06-26)

### switch-case 바깥 FoW 플래그 오염 (CRITICAL / Critical-1)
NearDiscoveredResource 판정을 switch 바깥의 별도 if(brain.NearResource && node.IsDiscovered)로 처리하면,
이전 노드가 NearResource=true로 설정한 상태에서 현재 노드의 IsDiscovered만 체크하여 노드 간 FoW 상태 오염이 발생.
수정 패턴: 각 case 내부에서 brain.NearResource = true와 함께 if(node.IsDiscovered) brain.NearDiscoveredResource = true를 설정.
switch 바깥의 별도 if 블록 삭제.

### 싱글턴 Instance 누락 (CRITICAL / Critical-2)
DiscoverArea() 주석에 SensorSystem.Instance 사용 예시가 있지만 Instance 정적 프로퍼티가 없으면 컴파일/런타임 오류.
수정 패턴: public static SensorSystem Instance { get; private set; } 추가 + Awake()에서 중복 인스턴스 체크/Destroy + OnDestroy()에서 null 해제.

### RebuildXxxList 더티 플래그 GC 할당 (WARNING / Warning-1)
Dictionary.Values를 foreach로 순회하는 RebuildVillagerList/RebuildNodeList 호출 시 Enumerator 힙 할당 발생.
수정 패턴: Dictionary + List 동기화 방식으로 변경. Register/Add 시 List.Add, Unregister/Remove 시 역순 for + RemoveAt.
_villagerListDirty, _nodeListDirty 필드 및 RebuildXxxList() 메서드 삭제.

### RegisterEnemy O(n) Contains/Remove (WARNING / Warning-2)
List<IEnemyAgent> 단독 사용 시 Contains()와 Remove()가 O(n).
수정 패턴: HashSet<IEnemyAgent> _enemySet + List<IEnemyAgent> _enemyList 병행. HashSet이 O(1) 중복체크/제거, List가 인덱스 for 순회 담당.

### Inspector 필수 필드 null에 LogWarning 부족 (WARNING / Warning-3)
_baseTransform null 시 AtBase 판정이 항상 false가 되는 치명적 설정 오류가 경고 없이 조용히 진행됨.
수정 패턴: Awake()에서 Debug.Assert(_baseTransform != null, ...) 추가.

### 파라미터 이름 단위 모호 (WARNING / Warning-5)
deltaGameTime은 '게임 내 시간'인지 '경과 일수'인지 불명확.
수정 패턴: deltaGameDays로 변경. Debug.Assert(deltaGameDays >= 0f && deltaGameDays <= 10f, ...) 추가.

---

## GameManager PR 패턴 (6단계, 2026-06-27)

### 코루틴 참조 미보관 (WARNING / G-001)
StartCoroutine(GameTickCoroutine()) 반환값을 보관하지 않으면 OnDestroy()에서 StopCoroutine 불가.
씬 전환이나 Destroy 시 고아 코루틴이 계속 실행되는 버그.
수정 패턴: private Coroutine _tickCoroutine 필드 추가 → Start에서 _tickCoroutine = StartCoroutine(...) →
OnDestroy 첫 줄에 if(_tickCoroutine != null) StopCoroutine(_tickCoroutine) 추가.

### Inspector 초기값 GDD 수치 불일치 (WARNING / G-002)
Inspector [SerializeField] 기본값이 AuthoritativeWorldState 생성자 기본값(GDD v0.4)과 다름.
수정 패턴: Wood=10, Stone=5, RawFood=30(유지), CookedFood=0, Iron=0, Copper=0으로 교정.
Tooltip의 "기획서 수치:" 숫자도 같이 수정한다.

### Start() SensorSystem 폴백 경로 불완전 (WARNING / G-003)
Awake에서 SensorSystem.Instance가 null이면 CreateAndRegisterDefaultNodes()와 DiscoverArea()가 건너뜀.
Start에서 InjectWorldState만 재시도하고 노드 등록은 재호출 안 함 → 노드 7개 전부 미등록.
수정 패턴:
  1. private bool _nodesRegistered = false 필드 추가
  2. CreateAndRegisterDefaultNodes() 성공 완료 직전에 _nodesRegistered = true 설정 (SensorSystem null early return 시 도달 안 됨)
  3. Start()의 SensorSystem 재주입 블록에서 !_nodesRegistered이면 CreateAndRegisterDefaultNodes() + DiscoverArea() 재호출

### 틱 실행 순서 주석 불명확 (SUGGESTION / G-004)
GameTickCoroutine 내부의 실행 순서 주석이 MessageBus.ProcessTick() 이후에 TickVillagerGroup을 실행해야 하는 이유를 명시하지 않음.
수정 패턴: "3. MessageBus.ProcessTick() — 이 시점에 OnVillagerDied 콜백으로 _villagerFSMs가 수정될 수 있음"
         "4. TickVillagerGroup — 반드시 ProcessTick() 이후 실행 (루프 중 리스트 수정 방지)" 주석 추가.

### EnemyNearby 불확실 주석 잔존 (SUGGESTION / G-005)
DebugPrintSystemStatus()에 "⚠️ 존재 여부 불확실" 주석이 있었지만 EnemyNearby는 AuthoritativeWorldState.cs에 실제로 존재.
수정 패턴: 불확실 주석 제거.

---

## FowManager + MapChunkRenderer PR 패턴 (13단계, 2026-06-29)

### 갱신 트리거 카메라 이동 단일 의존 (CRITICAL / Critical-01)
MapChunkRenderer.Update()가 카메라 이동(chunkSize*0.5 이상)에만 갱신을 의존하면,
카메라 고정 상태에서 FoW 상태가 변경되어도 화면에 전혀 반영되지 않는다.
수정 패턴:
  1. FowManager에 `HasAnyDirty { get; private set; }` 프로퍼티 추가
  2. SetVisible()에서 상태 변경 시 `HasAnyDirty = true` 설정 (MarkAllDirty()에서도 true 설정)
  3. FowManager에 `ClearAllDirtyFlag()` 메서드 추가 — RefreshDirtyChunks() 완료 후 false로 초기화
  4. Update()에서 `cameraMovedEnough || FowManager.Instance.HasAnyDirty` 복합 조건 사용

### Material 인스턴스 해제 누락 (WARNING / Warning-01)
`_quad.material`은 호출 시 새 Material 인스턴스를 생성하지만 OnDestroy()에서 해제하지 않으면
씬 종료 시 GPU 메모리 누수가 발생한다.
수정 패턴: `private Material _materialInstance` 필드 추가 → Start()에서 `_materialInstance = _quad.material` 저장 →
OnDestroy()에서 `Destroy(_materialInstance); _materialInstance = null;` 추가.

### ExecutionOrder 주석 실행 순서 역기술 (WARNING / Warning-02)
"-60 이후, -55 이전"처럼 숫자 의미를 혼동하여 체인을 반대로 기술하는 실수.
숫자가 작을수록 먼저 실행된다. 주석은 항상 체인 전체를 명시한다.
올바른 패턴: `// 실행 순서 체인: GameManager(-80)→FlowFieldManager(-75)→FactionAI(-70)→VillageAdvisor(-65)→MapChunkRenderer(-60)→FowManager(-55)→VillagerFSM(0)`

### for 루프 초기화 구문에 비루프 변수 혼재 (WARNING / Warning-03)
`for (int dx = -i, dy = -i; dx <= 0; dx++)` 처럼 루프에서 변하지 않는 변수를 for 초기화 구문에
함께 선언하면 유지보수자가 dy도 루프 변수로 오해할 수 있다.
수정 패턴: `int dy = -i;` 를 루프 바로 위 별도 줄로 분리 + 인라인 주석으로 고정값임을 명시.
