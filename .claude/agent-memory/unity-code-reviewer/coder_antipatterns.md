---
name: coder-antipatterns
description: AI Village Coder 에이전트가 반복적으로 저지르는 실수 — 리뷰 속도 향상을 위한 체크리스트
metadata:
  type: project
---

# Coder 에이전트 반복 실수 목록

**Why:** PR #1 (ResourceRegistry + WorldState), PR #2 (VillagerFSM + LOD FSM) 리뷰에서 발견된 패턴. 향후 PR에서 우선적으로 확인.

**How to apply:** 새 PR 리뷰 시 아래 항목을 먼저 grep/체크한 후 본격 리뷰 진행.

## 확인된 반복 실수 패턴

### 1. 이중 상태 저장 + 부분 검증 (PR #1 발견)
- ResourceRegistry._totalReserved와 AuthoritativeWorldState._reserved를 모두 유지하면서
  ValidateIntegrity가 _totalReserved만 검증하고 _worldState.GetReserved()는 검증하지 않음
- 결과: _worldState.Reserved가 _totalReserved와 달라져도 탐지 불가

### 2. Reserved 상한선 미설정 (PR #1 발견)
- AuthoritativeWorldState의 Reserved setter에 stock 상한선이 없음
- Reserved > Stock 상태가 되면 GetAvailable()이 음수를 반환할 수 있음
  (Mathf.Max(0f, ...) 로 방어하지만 데이터 자체가 오염됨)

### 3. NativeArray Allocator.TempJob 수명 문서화 부재 (PR #1 발견)
- WorldStateSnapshot이 Allocator.TempJob을 사용하지만
  호출자에게 "4프레임 이내 Dispose 필수" 제약을 주석/XML 문서로 명시하지 않음
- 사용자가 snapshot을 필드에 저장하면 Unity가 크래시

### 4. Singleton SetInstance null 할당 미지원 (PR #1 발견)
- AuthoritativeWorldState.SetInstance(null) 호출 시 기존 Instance가 null이 됨
- 씬 언로드/테스트 해체 시 null 할당이 정상 패턴인데 현재 null 검사 로직이 경고만 출력

### 5. ValidateIntegrity의 Debug.Log가 프로덕션 빌드에 포함 (PR #1 발견)
- 정합성 통과 시 Debug.Log 호출 — ValidateIntegrity는 매 틱 또는 자주 호출될 수 있어
  프로덕션에서 로그 스팸이 될 수 있음

### 6. 더미(stub) 코드를 실제 구현으로 제출 (PR #2 발견)
- EnterDead()에서 VillagerDied 메시지 발행이 Debug.Log 더미로만 구현됨
- DroppedItem 생성 → WorldState.DroppedItems 추가 코드 없음
- 설계 명세에 명시된 필수 동작을 "더미" 주석으로 처리하고 PR을 제출하는 패턴 — 반드시 차단

### 7. 복합 수식을 단순화하다 명세와 다른 공식 사용 (PR #2 발견)
- ConflictScore 명세: Σ(urgency_i × impact_i) — 각 (긴급도, 충격) 쌍의 개별 곱 합산
- 구현: totalUrgency × totalImpact — 합산 후 단일 곱
- 수학적으로 동치가 아님. 단순화가 명세 위반으로 이어지는 패턴

### 8. 조건 변수 소비 후 같은 소스 재접근 (PR #2 발견)
- State_CommandConflict에서 _brain.HasPendingOrder = false 이후
  _brain.PendingOrder.OrderType에 재접근
- 플래그를 먼저 끄고 값을 읽는 순서 문제 — 값을 로컬 변수에 복사한 뒤 플래그를 끄는 패턴이 필요

### 9. OnStateExit 예약 해제 조건 누락 (PR #2 발견)
- Executing → Idle 전이 시 예약이 해제되지 않음 (enteringState != Idle 조건 때문)
- 설계 명세: 비정상 탈출 포함 모든 경우에서 Release 보장 필요

### 10. switch-case 내 중복 조건 체크로 의도치 않은 이중 적용 (PR #2 발견)
- ApplyActionEffect "RestOnGround" case 내에서 FatigueLevel을 먼저 조정 후
  동일 case 안에서 if (actionId == "RestOnGround") 로 다시 보정
- switch case 진입 시 actionId == "RestOnGround"는 항상 참이므로 조건 없이 두 번 실행됨

### 11. ProcessTick()에서 foreach 내부 List 직접 순회 (PR #3 발견, BLOCKER 수준 아님)
- MessageBus.ProcessTick()이 SortedList를 foreach로 순회하면서 내부 List를 Clear() 처리함
- 이 자체는 안전하나, DispatchToSubscribers() 내 역순 for 루프는 콜백에서 Unsubscribe 호출 시
  앞 인덱스 항목은 스킵될 수 있음 — 역순 제거가 완전히 안전하지 않은 케이스가 존재함 (WARNING)

### 12. Payload가 struct일 때 object 박싱 비용 (PR #3 발견, 설계 트레이드오프 허용)
- AIMessage.Payload가 object이므로 struct Payload를 담을 때마다 GC 할당 발생
- 메시지 발행 빈도가 Tick당 수 건 수준이므로 현재 허용 범위. 향후 ECS 전환 시 재검토 필요

### 13. MessageBus.Publish()에서 _pendingDedupSet 초기화 타이밍 버그 (PR #3 발견, MAJOR)
- ProcessTick() 시작 시 _debugLastTickDedupCount를 0으로 초기화하지만
  Publish()에서 이미 _debugLastTickDedupCount++를 증가시킴
- ProcessTick()에서 0으로 초기화하면 이전 Tick의 Dedup 카운트가 사라짐 (통계 오염)
- 비기능 버그이나 디버그 신뢰성 훼손

### 14. List<DroppedItemInfo> 임시 할당 in EnterDead() (PR #3 발견, WARNING)
- EnterDead()에서 var droppedItemList = new List<MessageBus.DroppedItemInfo>() 를 매번 생성
- 사망 이벤트는 드물어 허용 가능 수준이나, 사전 할당 배열로 대체 가능

### 15. 동일 조건 분기 중복으로 Dead Code 생성 (PR #4 발견, MAJOR)
- Plan_GatherResources Miner 분기에서 MineIron과 MineStone이 동일한 조건(brain.HasTool && brain.NearResource)을 사용
- MineStone 반환 경로는 절대 실행되지 않음 (Dead Code)
- 자원 종류를 구분하는 별도 Brain 플래그(NearRock, NearIronOre 등)를 조건에 사용해야 함

### 16. FSM Goal 발동 조건이 ActionDatabase와 비동기화 (PR #4 발견, WARNING)
- VillagerFSM.State_Idle()의 HasResourcesForBuilding() 더미 메서드(Wood>=10, Stone>=5)가
  4단계 ActionDatabase.HasResourcesForBuilding()와 연동되지 않음
- Goal 발동 조건과 실제 플래닝 조건이 불일치 → 자원이 부족해도 BuildStructure Goal 진입 가능

### 17. ScriptableObject 내부 배열을 _pendingResourceCosts에 직접 참조 저장 (PR #4 발견, NOTE)
- TryReserveForAction()에서 _pendingResourceCosts = def.ResourceCosts (참조 공유)
- 에디터 핫 리로드 시 SO가 재로드되면 _pendingResourceCosts가 무효 배열을 가리킬 수 있음
- 방어적 복사(Clone 또는 ToArray())가 필요

### 18. Singleton 패턴 불완전 구현 — Instance 프로퍼티 누락 (PR #5 발견, MAJOR)
- SensorSystem이 MonoBehaviour Singleton이어야 하는데 Instance 정적 프로퍼티가 없음
- 코드 내 XML 주석에서 SensorSystem.Instance.DiscoverArea() 사용법을 문서화하지만
  실제 클래스에 Instance 프로퍼티가 없어 해당 사용 패턴이 컴파일 에러를 유발한다
- GameManager가 씬에서 SensorSystem 참조를 직접 보유하면 회피 가능하나 명시적 설계 결정이 필요

### 19. RegisterEnemy에서 List.Contains() 선형 탐색 (PR #5 발견, WARNING)
- RegisterEnemy()에서 _enemies.Contains(enemy)로 중복 체크 수행
- 적 목록이 클 경우(100명 이상 전투 시나리오) O(n) 탐색이 등록 시마다 발생
- HashSet<IEnemyAgent>로 교체하면 O(1) 중복 체크 가능 (순회는 별도 List 유지)

### 20. NearDiscoveredResource 논리 버그 — 루프 내 오염 (PR #5 발견, CRITICAL)
- UpdateResourceFlags()의 FoW 통합 조건: if (brain.NearResource && node.IsDiscovered)
- brain.NearResource가 루프를 돌면서 이전 반복에서 이미 true가 되면
  현재 노드가 IsDiscovered == false인 미발견 노드인데도 이전 노드 때문에 NearResource가 true라서
  NearDiscoveredResource = true로 잘못 설정될 수 있음
- 올바른 조건: 현재 노드가 NearResource에 기여했는지 여부를 별도 로컬 변수로 추적해야 함

### 21. GOAP A* f-cost 역산 시 루트 노드 초기화 버그 (PR #6 발견, CRITICAL)
- 루트 노드 NodeCosts[0]=0f로 초기화하고 힙에 삽입한 후
  자식 확장 시 parentGCost = NodeCosts[current] - CalculateHeuristic(current, totalSlots)로 g를 역산한다.
- 루트 노드에서: parentGCost = 0 - h(root) → 음수 g-cost → 자식 f = 음수 + baseCost → 잘못된 f-cost
- 올바른 초기화: NodeCosts[0] = CalculateHeuristic(0, totalSlots) (h(root))로 설정해야 한다.

### 22. Allocator.TempJob을 장수명 컨텍스트에 사용 (PR #6 발견, CRITICAL)
- PLANNING_TIMEOUT_SEC = 0.5초 = ~30프레임으로 TempJob 4프레임 제한을 초과한다.
- 코드 내 TODO 주석에서 스스로 인정하고 있으나 "기능적으로 안전하다"는 잘못된 판단.
- Allocator.Persistent로 교체하거나 Temp 제한 이내로 타임아웃을 줄여야 한다.

### 23. HashToActionId()에서 Animator.StringToHash() 매 호출마다 반복 계산 (PR #6 발견, MAJOR)
- 19개의 if문마다 Animator.StringToHash("ChopWood") 등을 호출한다.
- static 딕셔너리로 초기화하여 O(1) 조회로 교체해야 한다.

### 24. BuildGoalState() unknown goalId → 빈 마스크 → Scheduler 폴백 미구현으로 Replanning 루프 (PR #6 발견, MAJOR)
- "MoveToTarget" 등 미매핑 goalId는 빈 마스크를 반환하고 IsGoalSatisfied가 즉시 true → 빈 플랜
- Scheduler에 폴백 로직이 없어 VillagerFSM이 Replanning 무한 루프에 빠진다.
- unknown goalId는 빈 마스크 대신 에러 처리(IsScheduled=false 반환)하거나 Explore 폴백이 필요하다.
- PR #7(수정본)에서 "MoveToTarget"을 AtBase 목표로 매핑했으나, 의미론적으로 부정확한 해결책임 (N-002).

### 25. GetAvailable()을 동일 메서드에서 동일 자원에 두 번 계산 (PR #7 발견, WARNING)
- BuildCurrentState()에서 상단에 availWood/availStone/availRawFood/availCooked/availIron/availCopper 계산 후
  하단에 동일 소스로 cookedAvail/rawAvail/woodAvail/stoneAvail/ironAvail 재계산함.
- registry.GetAvailable()이 5회 중복 호출됨. 상단 변수를 재사용하는 것이 올바른 수정.

### 26. 현재 상태가 이미 목표 달성 시 ResultLength[0]=0 반환 → VillagerFSM Replanning 루프 (PR #7 발견, MAJOR)
- GOAPPlannerJob.Execute()에서 IsGoalSatisfied()가 true면 ResultLength[0]=0을 반환함.
- VillagerFSM.ReadResult()가 null을 받으면 Replanning으로 전이 — 이미 달성된 목표라도 루프 발생.
- 이미 달성된 경우 ResultLength[0]에 특별 값(예: -1)을 넣거나, Scheduler 레벨에서 처리해야 함.
- PR #8(5B 최종 수정)에서 ResultLength[0]=-1 특수값 + ReadResult(out bool alreadySatisfied)로 완전 해결.

### 27. alreadySatisfied=true → TransitionTo(Executing) 시 빈 CurrentPlan 문제 (PR #8 수정 반영, RESOLVED)
- VillagerFSM.EnterExecuting()은 CurrentPlan.Count==0이면 "Idle로 복귀" 경고를 출력하고 Idle로 전이.
- PR #9(5B 최종본)에서 alreadySatisfied 분기를 TransitionTo(VillagerState.Idle) 직접 전이로 수정. APPROVED.
- 수정 내용: CurrentGoalId=null, CurrentActionId=null 리셋 후 TransitionTo(Idle) — 올바른 구현.
- _planningContext.Dispose()가 TransitionTo(Idle) 전에 호출되어 OnStateExit(Planning, Idle) 경로 완전히 안전.

### 28. 코루틴 Coroutine 참조 미보관 — StopCoroutine 불가 (PR #10 / GameManager 발견, WARNING)
- GameManager.Start()에서 StartCoroutine(GameTickCoroutine())의 반환값(Coroutine 객체)을 저장하지 않음
- OnDestroy에서 해당 코루틴을 StopCoroutine으로 멈출 수 없어 MonoBehaviour 파괴 후에도
  일시적으로 콜백이 실행될 위험이 있음 (Unity 내부적으로 GameObject 파괴 시 정지되나
  명시적 관리가 모범 사례)
- 패턴: private Coroutine _tickCoroutine; → _tickCoroutine = StartCoroutine(...); → OnDestroy에서 StopCoroutine(_tickCoroutine);

### 29. [SerializeField] 초기값이 기획서 수치와 불일치 (PR #10 / GameManager 발견, WARNING)
- AuthoritativeWorldState 생성자 기본값: Wood=10, Stone=5, RawFood=30, Cooked=0, Iron=0, Copper=0
- GameManager SerializeField 기본값: Wood=50, Stone=50, RawFood=30, Cooked=20, Iron=10, Copper=5
- InitializeWorldState()가 Inspector 값으로 WorldState를 덮어써서 코드 기본값은 무효화됨
- Inspector 기본값이 기획서와 다르면 에디터 설정을 잊었을 때 잘못된 수치로 게임이 시작됨
- 기획서 수치와 맞추거나, AuthoritativeWorldState 생성자 기본값을 이 Inspector 기본값으로 변경 필요

### 30. GameManager [DefaultExecutionOrder(-80)]와 의존 MonoBehaviour 실행 순서 미명시 (PR #10 / GameManager 발견, WARNING)
- GameManager는 [DefaultExecutionOrder(-80)]으로 설정되어 있으나
  SensorSystem, MessageBus, BuildingQueue 등 의존 MonoBehaviour에는 실행 순서 어트리뷰트가 없음
- Awake()에서 SensorSystem.Instance, BuildingQueue.Instance를 null-guard 후 Start()에서 재시도하는
  방어 코드가 존재하지만, 실제 null이 될 경우 CreateAndRegisterDefaultNodes()가 조기 종료됨
  (DiscoverArea 미호출 → 모든 노드가 미발견 상태로 시작)
- SensorSystem/MessageBus/BuildingQueue에 [DefaultExecutionOrder(-90)] 이하를 명시하거나
  문서에 필수 설정 순서를 기재해야 함

### 31. SerializeField Inspector 값 수정 후 내부 헬퍼 메서드의 인라인 주석을 갱신하지 않음 (PR #11 / GameManager 최종본 발견, WARNING)
- G-002에서 Inspector 기본값(Wood, Stone, CookedFood, Iron, Copper)을 GDD v0.4 수치로 수정했으나
  InitializeWorldState() 내부의 인라인 주석 "기획서 수치: 50" 등이 구(旧) 기획서 수치를 그대로 표기
- 런타임 동작에는 영향 없으나, 주석이 거짓 정보를 전달하여 유지보수 혼란 초래
- Inspector 기본값과 헬퍼 메서드 주석을 동시에 갱신하는 습관이 필요

### 32. 폴백 경로에서 InjectWorldState 무조건 중복 호출 (PR #11 / GameManager 최종본 발견, WARNING)
- Start()의 SensorSystem 폴백 블록이 `if (SensorSystem.Instance != null && _worldState != null)` 조건으로만 판단
- Awake()에서 SensorSystem이 정상 초기화된 경우에도 Start()에서 InjectWorldState가 재호출됨
- !_nodesRegistered 가드는 노드 중복 등록만 막고 InjectWorldState는 막지 않음
- 올바른 수정: InjectWorldState 호출도 `if (!_nodesRegistered)` 블록 내부로 이동하거나
  별도 `_worldStateInjected` 플래그로 관리해야 함

### 33. 팩션 공통 자원 압박 초기값이 0으로 고정 — 공통 침략 트리거 영구 불발 (PR #12 / 6B FactionAI 발견, CRITICAL)
- FactionAI._copperStock = 0f, _silverStock = 0f 기본값으로 시작
- 공통 침략 트리거: copperStock < 10 OR silverStock < 5 → 0 < 10이므로 처음부터 조건 만족
- 그런데 nearPlayerTerritory 조건이 기지에서 멀면 false → 공통 트리거가 불발
- 팩션 특수 트리거(숲=식량, 철=철광석)는 즉시 발동하므로 단기적으로 무관하나, 공통 트리거 의미가 퇴색
- Inspector에서 초기 copper/silver를 설정 가능하게 [SerializeField]로 노출하거나 초기값 로직 명확화 필요

### 34. FactionAI.Start()에서 MessageBus 없이 코루틴 시작 — 침략 결정 발행 시 silent fail (PR #12 / 6B FactionAI 발견, WARNING)
- FactionAI는 MessageBus.Instance를 직접 Publish() 시 null 체크하므로 크래시는 없음
- 그러나 InjectDependencies 패턴 없이 싱글턴에 의존하므로 테스트 가능성 낮음
- EnemyFSM은 InjectDependencies(messageBus)로 받는 반면 FactionAI는 MessageBus.Instance 직접 참조 — 일관성 부재

### 35. Retreating HP 회복이 Update()에서만 처리 — LastHealTime 필드가 실제로 미사용 (PR #12 / 6B EnemyFSM 발견, NOTE)
- EnemyBrain.LastHealTime이 OnStateEnter(Retreating)에서 설정되지만
  HP 회복 로직이 deltaTime 기반(Update)으로 구현되어 LastHealTime을 참조하지 않음
- 의도적이면 LastHealTime 필드가 dead weight — 제거하거나 주석으로 "향후 누적 방식 전환 시 사용" 명시 필요

### 36. 상인 연합 관계도 40+ 시 레이드 불가 규칙 미구현 — 기획서 누락 (PR #12 / 6B FactionAI 발견, WARNING)
- 기획서: 상인 연합은 TradeProposal 먼저 발행, 관계도 40 이상이면 레이드 불가
- 구현: TradeProposal 발행 후 _tradeProposalSent = true → 다음 Tick에 EvaluateRaidDecision 진입
- 관계도 변수 자체가 FactionAI에 존재하지 않음. TODO 주석으로 명시는 되어 있으나 완성도 점검 필요

### 37. RageKill MessageType이 DEFAULT_PRIORITY_MAP에 미등록 (PR #13 / 8단계 발견, WARNING)
- MessageBus.DEFAULT_PRIORITY_MAP에 MessageType.RageKill 항목이 없음
- Publish() 시 테이블 미등록 타입은 발행자 설정값(MessagePriority.High)을 그대로 사용하므로
  현재는 우연히 올바르게 동작하지만, 향후 발행자가 Priority를 바꾸면 조용히 오동작할 수 있음
- 새 MessageType 추가 시 DEFAULT_PRIORITY_MAP도 반드시 갱신해야 하는 규칙이 있으나 Coder가 누락

### 38. VillagerRole enum과 AgentRole enum 중복 정의 (PR #13 / 8단계 발견, WARNING)
- VillagerEnums.cs에 AgentRole(Lumberjack, Miner, Builder, Warrior, Medic, Cook) 기존 존재
- 8단계에서 VillagerRole(Gatherer, Builder, Cook, Warrior)을 추가 정의
- Brain.Role은 AgentRole 타입, VillagerRole은 참조하는 코드가 없음 → Dead Code
- 새 enum 추가 대신 기존 AgentRole에 통합해야 함

### 39. Fighting 상태에서 EnterFighting() 예약 해제 미처리 (PR #13 / 8단계 발견, WARNING)
- Fighting 진입 시 EnterFighting()은 CurrentPlan.Clear()와 IsExecutingPlan=false만 수행
- 직전이 Executing 상태이던 경우 OnStateExit(Executing)이 호출되어 ReleaseCurrentReservation이 수행되지만
  직전이 Idle이나 Planning이던 경우 예약 관련 처리 누락 가능성 확인 필요 (설계 확인 권고)

### 40. Rage 타이머 감소에 하드코딩 상수 RAGE_TIMER_TICK_DELTA=0.6f 사용 (PR #13 / 8단계 발견, WARNING)
- RAGE_TIMER_TICK_DELTA = 0.6f = 그룹 실효 틱 주기 (0.1s × 6그룹)로 가정
- 실제 Tick 간격(0.1s)과 그룹 수(6)가 바뀌면 RAGE_TIMER_TICK_DELTA를 수동으로 맞춰야 함
- Time.deltaTime 기반 업데이트로 전환하거나 상수 계산식으로 명시하는 것이 더 안전

### 41. EnemyFSM.State_Attacking()에서 VillagerDied 이벤트 미발행 (PR #13 / 8단계 발견, PR #14 재리뷰에서 간접 해결 확인)
- EnemyFSM이 IsAlive=false 설정만 하고 VillagerDied를 직접 발행하지 않음
- 수정본(PR #14)에서 State_Attacking()에 "IsAlive=false → VillagerFSM.Update()의 AnyState 체크가 다음 프레임에
  TransitionTo(Dead) → EnterDead() → VillagerDied 메시지 발행을 처리한다" 주석 추가됨 (간접 해결로 허용)
- 직접 발행이 아니므로 처치 타이밍과 목록 정리 타이밍 간에 최대 1프레임 지연 존재 (허용 범위)

### 42. MessageBus.Subscribe() 미호출로 RageKill 메시지가 VillagerFSM에 도달 불가 (PR #14 / 8단계 재리뷰 발견, CRITICAL)
- VillagerFSM.HandleMessage()에 RageKill 케이스가 구현되어 있으나
  MessageBus.Instance.Subscribe(MessageType.RageKill, ...) 호출이 VillagerFSM 어디에도 없음
- HandleMessage()는 ReceiveMessage()에서 쌓인 _messageQueue를 처리하는 경로인데
  _messageQueue에 메시지가 쌓이려면 누군가 ReceiveMessage()를 호출해야 함
- MessageBus는 브로드캐스트(Subscribe된 콜백에게만 전달)이므로 Subscribe 없이는 RageKill이 아무에게도 전달되지 않음
- 결과: Rage 전염 시스템이 완전히 동작하지 않음 (핵심 8단계 기능 불발)

### 43. EnterFighting() XML 주석 이중 선언 버그 (PR #14 / 8단계 재리뷰 발견, WARNING)
- VillagerFSM.cs 라인 1235~1246에서 <summary> 태그가 두 번 연속 선언됨
- 첫 번째 `/// <summary>` (라인 1235)는 닫히지 않고 두 번째가 시작되어 컴파일러 경고 유발 가능

### 44. HandleDeadlock() 주석 시작 태그 누락 (PR #14 / 8단계 재리뷰 발견, RESOLVED in PR #15)
- VillagerFSM.cs: `/// Deadlock 상태 처리...` 로 `<summary>` 없이 주석 시작
- PR #15(2차 수정)에서 `<summary>` 태그 복원 완료. RESOLVED.

### 45. EnterFighting() 이중 `<summary>` 태그 — PR #14에서 발견, PR #15에서 미수정 (WARNING, PERSISTENT)
- VillagerFSM.cs 라인 1235~1236: `/// <summary>` 가 연속 두 번 선언됨 (`/// <summary>\n/// <summary>`)
- 1차 리뷰(PR #14)에서 발견되었으나 2차 수정(PR #15)에서도 그대로 남아 있음
- XML 문서 파서가 중첩 태그를 오류 처리하여 IntelliSense / 자동 문서 생성이 깨질 수 있음
- `OnVillagerDied` 팬아웃 루프에서 `_villagerFSMs`를 `foreach`로 순회하는데, 이 콜백은
  `MessageBus.ProcessTick()` 내부에서 호출된다. GameManager 틱 순서상 ProcessTick → TickVillagerGroup 순서이므로
  이 시점에 _villagerFSMs가 이미 RemoveVillagerFromList로 정리된 직후라서 안전하다. (순서 확인 완료)

### 46. HandleMessage(RageKill) 거리 중복 체크 — GameManager와 VillagerFSM 양쪽에서 수행 (PR #15 발견, NOTE)
- GameManager.OnRageKill()에서 6타일 필터링 후 fsm.ReceiveMessage(msg) 호출
- VillagerFSM.HandleMessage(RageKill)에서도 `dist <= RAGE_CONTAGION_RADIUS` 재확인
- 거리 필터가 이중으로 적용되어 동작은 정확하나, 불필요한 중복 연산 발생
- GameManager가 이미 필터링했으므로 HandleMessage 내 거리 체크를 제거하거나
  주석으로 "GameManager에서 이미 필터링됨" 명시가 필요 (현재는 무해한 중복)

### 47. `/// <summary>` 닫기 없이 중첩 선언하는 XML 주석 패턴 (PR #14~#15 반복, WARNING)
- EnterFighting()의 이중 summary 태그가 2차 수정에서도 수정되지 않음
- Coder가 XML 주석 수정 시 기존 태그를 삭제하지 않고 새 태그를 추가하는 패턴
- 향후 PR에서 XML 주석 영역을 집중 점검할 것

### 48. VillageAdvisor PR (VillageAdvisor.cs) — APPROVED (결함 0건, 2026-06-28)
- 모든 명세 항목 완벽 준수: 6개 우선순위 규칙, 즉시 return, 상수 분리, LINQ 없음
- WaitForSeconds 1회 생성 및 재사용, Coroutine 핸들 보관 + OnDestroy StopCoroutine — 교과서적 구현
- 모든 싱글턴(AuthoritativeWorldState, GameManager, BuildingQueue, Brain) null 방어 완비
- GameManager.GameTime 올바르게 참조 (AuthoritativeWorldState.GameDay 미사용)
- CountAliveVillagers()에서 인덱스 for 루프 + villagers.Count 사전 캐시 — 모범 패턴
- GetTotalResourceStock(): 5종 자원(Wood/Stone/Iron/Copper/Silver) 합산, 식량 제외 — 명세 일치
- TryEnqueue(): EnqueueBuilding() 반환값(bool) 확인 후 조건부 BuildingQueued 설정 — 명세 일치
- [DefaultExecutionOrder(-65)], [DisallowMultipleComponent], sealed, namespace AIVillage.Core 전부 명시
- 이 PR은 Coder 에이전트가 지금까지 제출한 PR 중 최초 무결함 APPROVED 사례

### 49. 9+10단계 UI PR — APPROVED (결함 0건, 2026-06-28)

- PlayerInputController: Update() 내 GetComponent 허용(레이캐스트 히트 시에만 호출, 프레임당 1회 이하), layerIndex<0시 LogError 명세 준수, EventSystem null체크+IsPointerOverGameObject 올바른 순서
- HUDManager: GameManager.OnOrderRefusedEvent C# event 패턴으로 구독 (UI 타입 직접 참조 없음), Start()에서 구독 + OnDestroy에서 해제 완비, _resourceHUDCoroutine/_buildingQueueCoroutine 핸들 보관 + StopCoroutine 완비
- ResourceHUD: WaitForSeconds Awake 1회 생성, RefreshCoroutine 핸들 _refreshCoroutine 보관 + OnDestroy StopCoroutine 완비, UpdateTextIfChanged 변경 시에만 SetText 호출 최적화
- VillagerStatusPanel: Brain 읽기 전용 접근만 수행(쓰기 없음), 사망 감지 후 SetTarget(null) 올바른 코루틴 자기 종료, WaitForSeconds 재사용
- BuildingOrderPanel: Awake에서 onClick 람다 등록(string buildingId 캡처 올바름), Start에서 코루틴 시작 + OnDestroy StopCoroutine, 건물 완성 시 버튼 비활성화 조건 명세 일치
- BuildingQueuePanel: 항목 수 변경 시만 Instantiate/Destroy(GC 최소화), RefreshCoroutine null 안전 guards 완비
- RefusalBubble: 토스트 큐(MAX_QUEUE_SIZE=5) 큐 오버플로 방어, ShowToastQueueCoroutine에서 _isShowingToast 중복 시작 방지, OnDestroy StopCoroutine 완비
- GOAPDebugOverlay: #if UNITY_EDITOR || DEVELOPMENT_BUILD 전체 파일 감싸기 — 릴리즈 빌드 완전 제외, 0.1초 WaitForSeconds 재사용
- VillagerFSM Awake 수정: SphereCollider isTrigger=true, Villager 레이어 미등록 시 LogError 명세 준수
- GameManager: OnOrderRefusedEvent Action<OrderRefusedPayload> C# event 선언, OnOrderRefused 핸들러에서 OnOrderRefusedEvent?.Invoke(payload) — UI 타입 직접 참조 없음
- EnqueueBuilding 시그니처 일치: bool EnqueueBuilding(string buildingId, int targetTileX, int targetTileY) — PlayerInputController 호출 완벽 일치
- OrderRefusedPayload 필드명 일치: VillagerId, RefusalReasonCode, RefusalMessage, ConflictScore, Threshold, AlternativeGoalId, LoyaltyLevel — HUDManager/RefusalBubble 접근 필드 모두 정확
- AuthoritativeWorldState 프로퍼티 이름 일치: WoodStock, StoneStock, IronStock, RawFoodStock, CookedFoodStock, CopperStock — ResourceHUD/PlayerInputController 접근 모두 정확
- RefusalReasonCode enum 값 일치: REFUSE_HUNGER, REFUSE_INJURY, REFUSE_FATIGUE, REFUSE_LOYALTY, REFUSE_DANGER, REFUSE_NO_TOOL, REFUSE_INSUFFICIENT_RESOURCES — RefusalBubble 딕셔너리 7개 키 완벽 일치
- 건물 자원 비용 수치: PlayerInputController와 BuildingOrderPanel이 동일 상수 정의 (중복이나 일치함)
- BuildingOrderPanel의 ResourceCost 상수가 PlayerInputController와 중복 정의됨 — 리팩토링 여지 있으나 현재는 값이 동일하므로 기능상 무해

### 50. 11단계 Win/Lose PR — APPROVED with 2 Warnings + 3 Suggestions (2026-06-28)
- CheckWinLoseConditions() 우선순위 순서: Win3(최우선) → Win1 → Lose1(전멸) → Lose2(TownHall파괴) — 명세 일치
- _gameEnded 중복 발화 방지 플래그: CheckWinLoseConditions와 TriggerGameResult 양쪽 모두 재진입 guard 완비
- TriggerGameResult에서 StopCoroutine(_tickCoroutine) + _tickCoroutine=null 처리 — coroutine 내부에서 self-stop 시 다음 yield까지 실행됨(안전)
- OnDestroy에서 _tickCoroutine null 체크로 이중 StopCoroutine 방지 — 올바른 패턴
- [WARNING] Win3_Prosperity가 aliveCount==0일 때도 발동 — GDD "최우선" 문구로 스펙상 맞으나 모든 주민이 사망한 채 번영 승리하는 시나리오 발생 가능. 기획 확인 필요.
- [WARNING] GameResultPanel.Start()에서 GameManager.Instance null 시 이벤트 구독 실패 → 결과 화면이 영구적으로 표시되지 않음. null일 때 Awake로 재시도하거나 에러 수준 로그로 격상 필요.
- 이 PR에서 확인된 Coder 패턴: 스펙의 "최우선" 표현을 조건 없이 구현 (guard 없는 단순 if 순서) — 엣지케이스 처리는 기획팀 결정이 필요한 경우 WARNING으로 플래그해야 함.

### 52. #if UNITY_EDITOR 블록 내부에 프로덕션 코드 경로가 호출하는 메서드 정의 (FactionAI 확장 PR / CRITICAL)
- CountAliveUnits()가 `#if UNITY_EDITOR` 전처리기 블록 안에 정의되어 있으나
  EvaluateAndExecuteGoal() (프로덕션 코루틴 경로)에서 직접 호출됨
- 릴리즈 빌드에서 CountAliveUnits() 심볼이 존재하지 않아 컴파일 에러 발생
- 패턴: DebugPrintFactionStatus()와 같은 에디터 전용 메서드 근처에 함께 배치하면서
  실제 필요한 런타임 헬퍼까지 같은 #if 블록에 넣어버리는 실수
- 수정: CountAliveUnits()를 #if UNITY_EDITOR 블록 밖으로 이동

### 53. Scouting 상태 타이머를 OnStateEnter가 아닌 SetScoutTarget에서 설정 (FactionAI 확장 PR / WARNING)
- Moving 상태는 OnStateEnter(Moving)에서 Brain.MoveStartTime = Time.time을 설정
- Scouting 상태는 OnStateEnter에 case 없이 SetScoutTarget()에서 직접 설정
- 일관성 부재: 향후 다른 코드가 TransitionTo(Scouting)을 직접 호출하면 MoveStartTime 미갱신으로 잘못된 타이머 사용
- OnStateEnter에 case EnemyState.Scouting: Brain.MoveStartTime = Time.time; 추가가 올바른 패턴

### 54. _prevTickGameDay 첫 틱 deltaGameDays 과다 산출 가능성 (FactionAI 확장 PR / WARNING → RESOLVED in PR #16 2차 리뷰)
- _prevTickGameDay 초기값 = 0f. 게임이 Day 10에 활성화되면 첫 틱의 deltaGameDays = 10
- PR #16 수정: Start()에서 `_prevTickGameDay = GameManager.Instance != null ? GameManager.Instance.GameTime : 0f;` 로 초기화 완료

### 70. ExecutionOrder 주석의 순서 설명이 실제 숫자와 반대로 기술됨 (PR #13 / FowManager 발견, WARNING)
- FowManager 클래스 주석: "[DefaultExecutionOrder(-55)] // MapChunkRenderer(-60) 이후, VillagerFSM(0) 이전"
- 숫자가 작을수록 먼저 실행되므로 -60 < -55 → MapChunkRenderer가 FowManager보다 먼저 실행됨
- 주석의 "이후"라는 표현이 혼란을 초래함. "MapChunkRenderer(-60)보다 나중에 실행됨"이 올바른 표현
- Coder가 Execution Order 숫자 방향을 반복적으로 혼동하는 패턴 — 주석 작성 시 "숫자가 작을수록 먼저"를 명시할 것

### 71. 렌더러가 카메라 이동에만 반응 — FoW 틱 변경이 화면에 즉시 반영되지 않음 (PR #13 / MapChunkRenderer 발견, Critical)
- MapChunkRenderer.Update()는 카메라가 chunkSize*0.5(기본 8타일) 이상 이동해야만 RefreshDirtyChunks() 호출
- FowManager.OnTick()이 0.1초마다 FoW 상태를 변경하고 _dirtyMask를 세우지만
  카메라가 정지 상태이면 Update()에서 moveDist < threshold → 텍스처 갱신이 영구적으로 스킵됨
- 결과: 주민이 이동하고 시야가 열려도 카메라를 움직이지 않으면 맵에 아무 변화가 없어 보임
- 올바른 수정: FowManager.OnTick() 후 anyDirtyExists 플래그를 공개 프로퍼티로 노출하거나
  MapChunkRenderer가 FowManager의 전역 dirtyCount를 체크하여 카메라 이동과 독립적으로 갱신 트리거

### 73. OnTick() VISIBLE→EXPLORED 다운그레이드 경로에서 HasAnyDirty 미설정 (PR #13 2차 리뷰 / FowManager 발견, Critical → RESOLVED in PR #13 3차 리뷰)
- OnTick() Step1 라인 291: `_dirtyMask[ax, ay] = true` 를 세우지만 `HasAnyDirty = true`를 세우지 않음
- SetVisible()만 HasAnyDirty를 갱신하는 구조이므로, 주민이 이동하지 않아 RevealArea()가 호출되지 않는 틱에서는
  다운그레이드 타일이 MapChunkRenderer에 신호를 보내지 못함 — 카메라가 정지하면 EXPLORED 상태가 화면에 미반영
- Critical-01 수정 의도(HasAnyDirty 추가)가 OnTick의 dirty 경로를 빠트린 부분 수정 실패
- PR #13 3차 수정: `MarkDirty(int ax, int ay)` 헬퍼 도입으로 완전 해결
  - MarkDirty: `_dirtyMask[ax,ay]=true` + `HasAnyDirty=true` 를 단일 메서드로 캡슐화
  - OnTick Step1, SetVisible, MarkAllDirty 세 경로 모두 MarkDirty()로 통일
  - 파일 내 `_dirtyMask[ax, ay] = true` 직접 할당은 MarkDirty 내부(라인 575) 1개만 존재 — 누락 없음
- RESOLVED. APPROVED.

### 72. _quad.material 인스턴스 생성 후 OnDestroy에서 해제하지 않음 (PR #13 / MapChunkRenderer 발견, Warning)
- Start()에서 _quad.material.mainTexture = _tileTexture 호출 시 Unity가 sharedMaterial의 인스턴스 복사본을 생성함
- OnDestroy에서 _tileTexture는 Destroy하지만 생성된 Material 인스턴스는 해제하지 않음
- 씬 전환 시 Material 인스턴스가 메모리에 잔류하는 메모리 누수 발생

### 55. State_Retreating()이 Tick마다 동일 좌표를 반복 재설정 (PR #16 2차 리뷰 / Suggestion)
- State_Retreating()에서 Brain.TileX = Brain.BaseTileX; Brain.TileY = Brain.BaseTileY; 를 매 Tick 실행
- 이미 기지에 있으므로 동일 값 재설정 — 기능상 무해하나 불필요한 연산
- 진입 시 1회만 실행(OnStateEnter에서 처리)하거나 "순간이동" 완료 플래그로 차단하는 패턴이 더 명확

### 56. State_Plundering()에서 탈취 자원이 팩션 자원에 추가되지 않음 (PR #16 2차 리뷰 / Warning)
- `taken` 변수를 계산 후 Debug.Log에만 사용
- 플레이어 WoodStock은 감소하지만 FactionAI의 _copperStock/_silverStock 등 팩션 자원에 추가 없음
- 기획서에 약탈 자원의 행선지가 명시되어 있지 않아 TODO로 플래그됨 (기획 확인 필요)

### 57. 동적 Instantiate 후 TickGroupIndex 미설정 (마일스톤 이벤트 PR / Warning)
- SpawnVillager()에서 VillagerFSM을 Prefab으로 Instantiate할 때 _tickGroupIndex를 설정하지 않음
- Prefab 기본값(_tickGroupIndex=0)을 그대로 사용하면 동적 스폰 주민이 전원 그룹 0에 집중됨
- 그룹 0이 혼자 6배 틱 부하를 받아 성능 분산 의도가 무너짐
- SpawnVillager에서 `_villagerFSMs.Count % 6` 등으로 TickGroupIndex를 할당하거나
  Prefab Inspector 기본값을 기존 주민과 다르게 설정하는 규칙이 필요

### 58. _gameEnded=true 이후에도 CheckMilestoneEvents() 호출 지속 (마일스톤 이벤트 PR / Warning → RESOLVED in PR #17 2차 리뷰)
- TriggerGameResult()가 StopCoroutine(_tickCoroutine)으로 틱을 정지시키나,
  정지 명령은 현재 yield 이후 틱부터 적용됨 — 동일 while(true) 이터레이션 내에서
  CheckMilestoneEvents → CheckWinLoseConditions 순으로 호출될 때 문제없음
- 단, CheckMilestoneEvents()에 _gameEnded 가드가 없어 다른 경로에서 메서드가 직접 호출될 경우
  게임 종료 후에도 스폰이 발생할 수 있음 (현재 직접 호출 경로 없으므로 Warning 수준)
- PR #17 수정: CheckMilestoneEvents() 최상단에 `if (_gameEnded) return;` 추가. RESOLVED.

### 59. 동적 Instantiate 후 TickGroupIndex 미설정 — M-001 (마일스톤 이벤트 PR / Warning → RESOLVED in PR #17 2차 리뷰)
- SpawnVillager()에서 Instantiate 직후, RegisterNewVillager 호출 이전에 `fsm.SetTickGroupIndex(_villagerFSMs.Count % 6)` 추가.
- VillagerFSM에 `SetTickGroupIndex(int groupIndex)` 공개 메서드 추가 (Mathf.Clamp(groupIndex, 0, 5)).
- 호출 시점의 _villagerFSMs.Count는 RegisterNewVillager 이전이므로 기존 주민 수를 기준으로 모듈러 연산 → 올바른 분산.
- RESOLVED.

### 61. 경로 분산 PR (옵션A+B) — 이중 Release 경로 존재 (2026-06-29 발견, WARNING)
- ReleaseCurrentReservation() 내부에서 이미 ReleaseGatherNode()를 호출함
- EnterReplanning()은 ReleaseCurrentReservation() 후 추가로 ReleaseGatherNode()를 명시적 호출
- 결과: Replanning 진입 시 ReleaseGatherNode가 최대 2회 호출됨
- 1차 호출: ReleaseCurrentReservation → ReleaseGatherNode (내부) → _currentGatherNode=null
- 2차 호출: EnterReplanning → ReleaseGatherNode → _currentGatherNode가 이미 null이므로 early return
- 현재 ReleaseGatherNode가 null 가드(_currentGatherNode==null → return)를 갖추고 있어 중복 실행은 안전하지만,
  설계 의도(어느 경로에서 해제 책임을 지는지)가 불명확하여 향후 코드 수정 시 혼란 초래 가능

### 62. 경로 분산 PR (옵션A+B) — TryOccupy 실패 시 노드 점유 없이 _currentGatherNode에 null 저장 (2026-06-29, SUGGESTION)
- MoveTileForAction()에서 TryOccupy 결과를 확인하지 않고 무조건 _currentGatherNode = nearest로 저장
- TryOccupy가 false를 반환(이미 MaxGatherers 포화)해도 _currentGatherNode가 nearest로 설정됨
- ReleaseGatherNode()에서 Release()를 호출하면 CurrentGatherers를 실제 점유하지 않았는데 감소시킴
- 단, FindNearestDiscoveredNode()가 CurrentGatherers < MaxGatherers 노드만 후보로 걸러내므로
  TryOccupy가 false를 반환하는 경우는 이론상 발생하지 않음 (경쟁 조건 없는 단일 스레드)
- 그러나 방어 코드로 TryOccupy 반환값 확인 후 _currentGatherNode 설정하는 것이 더 안전

### 63. 경로 분산 PR (옵션A+B) — APPROVED (결함 0건, 2026-06-29)
- ResourceNode.cs: CurrentGatherers private setter, MaxGatherers=2 기본값, IsAvailableForHarvest() / TryOccupy() / Release() 모두 정확하게 구현
- FindNearestDiscoveredNode(): 포화도 필터(A) + ID 해시 선호(B) 조합 올바름, PREFERENCE_BONUS=3 상수 분리
- ReleaseGatherNode(): null 가드 완비, _currentGatherNode = null 초기화 올바름
- 이중 Release 경로(EnterReplanning + ReleaseCurrentReservation 내부)는 null 가드로 안전하게 처리됨

### 65. 12단계 경로이동 PR — ActionStartTime 타이머 충돌 버그 (2026-06-29, CRITICAL)
- StartNextAction()에서 Brain.ActionStartTime = Time.time으로 양수 설정 후 MoveTileForAction() 호출
- State_Executing()의 이동 완료 감지 조건 `if (Brain.ActionStartTime <= 0f)` 가 절대 true가 되지 않음
- 이동 중에도 타이머가 이미 흐르고 있어, 이동 시간이 ACTION_SIMULATE_SEC(2초)보다 길면 도착 즉시 완료 처리
- 수정: StartNextAction()에서 타이머를 0f로 초기화하고(MoveTileForAction 이전), State_Executing()에서 이동 완료 후 Time.time으로 시작

### 66. 12단계 경로이동 PR — JPS 대각선 비용 계산 오류 (2026-06-29, WARNING)
- FindPath()에서 점프 포인트 간 이동 비용을 `Mathf.Max(|adx|, |ady|) * DIAGONAL_COST`로 계산
- 올바른 Chebyshev 비용: `min(|adx|,|ady|)*DIAGONAL_COST + (max-min)*STRAIGHT_COST`
- 현재 공식은 직선 이동 성분을 대각선 비용으로 과다 계산하여 A* 비용 추정이 왜곡됨 (admissible은 유지되나 suboptimal 경로 선택 가능)

### 67. 12단계 경로이동 PR — EnemyFSM Retreating 중 Brain 논리 좌표 미갱신 (2026-06-29, WARNING)
- OnStateEnter(Retreating)에서 _moveTarget을 기지 위치로 설정하고 _isMoving=true
- Update()에서 기지 도착 시에만 Brain.TileX/Y = BaseTileX/Y 갱신
- 이동 중에는 Brain 논리 좌표가 출발 위치에 고정 → SensorSystem NearEnemy 판정이 낡은 좌표로 동작
- VillagerFSM은 웨이포인트마다 Brain 좌표를 갱신하는 반면 EnemyFSM Retreating은 도착 후 1회만 갱신 (비대칭)

### 68. ActionStartTime=0f 센티넬 패턴 — Time.time==0f 게임 시작 직후 모호성 (12단계 2차 리뷰 / Suggestion)
- StartNextAction()에서 ActionStartTime=0f를 "타이머 미시작" 센티넬로 사용
- State_Executing() 조건: `if (Brain.ActionStartTime <= 0f)` → Time.time으로 타이머 시작
- 게임 시작 직후 Time.time==0f인 첫 틱에 타이머를 0f로 설정하면 "센티넬"과 "타이머 시작됨"이 동일값이 됨
- 다음 Tick에서 `Time.time(0.016) - 0f < ACTION_SIMULATE_SEC(2.0)` → wait, 기능은 정상 동작
- 실질 버그 없음. 그러나 센티넬 값과 실제 시작 시각이 충돌할 수 있는 설계 모호성 존재
- 완전한 해결: 별도 bool _actionTimerActive 플래그 또는 -1f 센티넬 사용이 더 명확함 (Suggestion 수준)

### 69. 12단계 경로이동 PR 2차 리뷰 — APPROVED (결함 0건, 2026-06-29)
- Fix-1 확인: StartNextAction() 라인 1243 → `Brain.ActionStartTime = 0f;` 올바르게 수정됨
- Fix-2 확인: FindPath() 라인 161~163 → diagSteps*DIAGONAL_COST + straightSteps*STRAIGHT_COST 올바른 비용 계산
- Fix-3 확인: GetSuccessors() 라인 232~233 → System.Math.Abs(cx-px) 정수 나눗셈 올바르게 수정됨
- Fix-4 확인: EnemyFSM Update() 라인 225~226 → Mathf.RoundToInt(_moveTarget.x/y) 모든 상태에서 통일 적용
- 신규 Suggestion #68: ActionStartTime=0f 센티넬 모호성 — 기능 버그 없음, 설계 명확성 개선 여지

### 64. 경로 분산 PR 2차 리뷰 (W-001, S-001 수정) — APPROVED (결함 0건, 2026-06-29)
- W-001 수정 완료: ReleaseCurrentReservation() 내부의 ReleaseGatherNode() 제거됨
  - 확인: 라인 1897~1924 ReleaseCurrentReservation() 본문에 ReleaseGatherNode 호출 없음
  - OnStateExit(Executing) 라인 1010: ReleaseGatherNode() 무조건 호출 — 중단 케이스 커버 확인
  - EnterReplanning() 라인 1164: ReleaseGatherNode() 명시적 호출 확인
  - ReleaseGatherNode() 자체: null 가드(_currentGatherNode==null → return) 완비 — 이중 호출 시 안전
- S-001 수정 완료: MoveTileForAction() 라인 2457
  - if (nearest != null && nearest.TryOccupy(AgentId)) 조건으로 반환값 확인 후 _currentGatherNode 저장
  - TryOccupy 실패 시 _currentGatherNode가 null 유지됨 — 올바른 방어 구현
- 최종 해제 경로 4가지 모두 코드상 확인됨:
  1. 정상 완료: OnActionCompleted() 라인 1683 → ReleaseGatherNode()
  2. Executing 중단(모든 케이스): OnStateExit(Executing) 라인 1010 → ReleaseGatherNode()
  3. Replanning: EnterReplanning() 라인 1164 → ReleaseGatherNode()
  4. 액션 전환: MoveTileForAction() 라인 2451 → ReleaseGatherNode()

### 60. 마일스톤 이벤트 PR 2차 리뷰 — APPROVED (결함 0건, 2026-06-29)
- M-001 (SetTickGroupIndex 미설정): VillagerFSM에 SetTickGroupIndex 추가 + SpawnVillager에서 호출 확인.
- M-002 (Awake 타이밍 주석): SpawnVillager에 Instantiate 동기 보장 주석 및 Brain null 불가 근거 명시 확인.
- M-003 (_gameEnded 가드 누락): CheckMilestoneEvents() 최상단 if (_gameEnded) return; 추가 확인.
- SetTickGroupIndex 호출 위치: RegisterNewVillager 이전 → Count가 등록 전 값이므로 그룹 분산 올바름.

### 51. 11단계 Win/Lose PR 2차 리뷰 — APPROVED (결함 0건, 2026-06-28)
- 1차 Warning/Suggestion 4건 모두 완전히 수정됨
  - W-01: GameResultPanel에 _subscribed/_gameManagerRef 필드 + TrySubscribe() 패턴 도입 (Awake+Start 이중 시도, 중복 구독 방지, OnDestroy 안전 해제)
  - W-02: Win3_Prosperity에 aliveCount > 0 가드 추가 (주민 0명인 채 번영 승리 방지)
  - S-01: HandleGameResult default 케이스 LogWarning → LogError 격상
  - S-02: Win1_Survival description에 "주민 5명 이상" 조건 문구 명시
- BuildingQueue: SilverCitadel case 완비 (worldState.SilverCitadelBuilt = true 설정), TownHallEverBuilt 영구 기록 로직 완비
- CheckWinLoseConditions 우선순위 체인: 패배 1(전멸 0명)이 패배 2(1~2명) 앞에서 return되므로 경계값(0명) 중복 발동 없음 — 올바른 순서
- TriggerGameResult를 코루틴 내부에서 self-stop 시 _tickCounter++ (L459)가 추가 실행되지만 _gameEnded=true이므로 다음 틱에 즉시 return — 안전
- 이 PR은 Coder 에이전트의 2차 수정 리뷰 첫 완전 APPROVED 사례 (모든 지적 사항 수정 완료)

### 76. 15단계 주민 모집 PR — 검토 결과 (2026-07-02)
- VillagerRecruitData, RecruitmentSystem, VillagerBrain.InitFromRecruitData: 설계 명세 완벽 준수, 결함 없음
- RecruitmentPanel: OnEnable/OnDisable 코루틴 패턴 올바름, 람다 클로저 idx 캡처 올바름, OnDestroy RemoveListener 완비
- HUDManager.WatchTownHallCoroutine: yield break 자가종료 올바름, _townHallWatchCoroutine=null 직접 설정 후 yield break — 기능상 정상이나 불필요한 패턴
- GameManager Step 7-a: FindObjectOfType를 Start()에서 1회만 호출 (올바른 패턴)
- AuthoritativeWorldState setter 존재 여부: CookedFoodStock, IronStock, CopperStock, SilverStock 모두 확인됨, 컴파일 오류 없음
- RegisterNewVillager(VillagerFSM fsm) 시그니처: RecruitmentSystem의 호출과 완벽 일치
- ExecutionOrder 체계: GameManager(-80) → RecruitmentSystem(-60) → VillagerFSM(0) — 올바른 순서
- [MAJOR] RecruitmentSystem.TryRecruit()에서 Instantiate 후 VillagerFSM GetComponent 실패 시 이미 차감된 자원을 복구하지 않음 — 경고 수준 이슈
- [WARNING] BuildCostString()에서 new StringBuilder()를 Awake에서 1회만 생성하지 않고 매 호출마다 할당 — 초기화 시 1회 호출이므로 런타임 영향 없음
- APPROVED 판정 (Critical 없음)

### 75. 14단계 SeasonManager PR — APPROVED (결함 0건, 2026-06-30)
- SeasonManager: _accumulatedTime 누적 방식으로 계절 공식 구현. _seasonLengthDays=0 DivideByZero 위험에 대한 방어 없음 (Warning 수준)
- RISK-2~5 모두 올바르게 구현. 오발행 없음, 배율 누적 없음, 두번째 겨울 경보 재발행 정상.
- WinterCrisis 임계값 공식: villagerCount × _winterFoodCostPerDay × _winterCrisisBufferDays — 설계 명세 일치
- DEFAULT_PRIORITY_MAP에 SeasonChanged(Medium), WinterCrisis(High) 정상 추가 확인
- BuildActionDefs 3번째 파라미터 seasonGatherModifier=1.0f 추가 + GOAPPlannerScheduler.Schedule() 내부에서 null-safe 읽기 — 시그니처 변경 호환성 완전
- GameManager 틱 순서: 2-b에 SeasonManager.Instance?.OnTick() 삽입 위치 정확 (TickResourceRegeneration 직전)
- GameManager.Villagers는 사망 시 RemoveVillagerFromList()로 제거되는 List이므로 Dead 상태 포함 가능성 없음
- SeasonManager DontDestroyOnLoad 없음 — 씬 단위 관리 의도적으로 올바름 (MessageBus와 달리 씬 의존성 O)
- Warning: _seasonLengthDays=0일 때 Mathf.FloorToInt(x/0) → NaN/Infinity → Season 캐스팅 오류 가능. Inspector 최솟값 보호 또는 OnTick() 내 방어 코드 권장
- Suggestion: DebugForceWinterCrisis()에서 deltaGameDays=0f → TickWinterEffects(0f) → 식량 소모=0, 임계값 체크만 수행 — 의도적 설계이므로 안전

### 74. 13단계 FowManager PR 3차 리뷰 — APPROVED (결함 0건, 2026-06-29)
- Critical 수정 확인 (항목 73): MarkDirty(int ax, int ay) 헬퍼(라인 573~577) 올바르게 도입
  - `_dirtyMask[ax,ay] = true` + `HasAnyDirty = true` 두 쓰기를 단일 메서드로 캡슐화
  - OnTick Step1 (라인 291): MarkDirty(ax, ay) 호출 확인
  - SetVisible (라인 556): MarkDirty(ax, ay) 호출 확인
  - MarkAllDirty (라인 568): MarkDirty(ax, ay) 호출 확인
  - 파일 전체 grep: `_dirtyMask[ax, ay] = true` 직접 할당이 MarkDirty 내부 1곳(라인 575)만 존재 — 누락 없음
- Shadowcasting 알고리즘(CastLight, TransformX/Y, IsBlocking, SetVisible 내부 로직) 변경 없음 확인
- HasAnyDirty 프로퍼티, ClearAllDirtyFlag() 등 1·2차 수정 결과물 원형 유지 확인
- MarkDirty 헬퍼 도입 패턴 평가: 두 상태가 항상 함께 설정되어야 하는 불변식을 메서드로 캡슐화 — 교과서적 구현
