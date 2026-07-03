---
name: feedback-reviewer-preferences
description: AI Village Tech Lead 리뷰어가 PR에서 반복 강조하는 코딩 규칙 및 선호 패턴
metadata:
  type: feedback
---

## 규칙 목록

### 1. 하드코딩 오프셋 금지
인덱스 오프셋을 매직 넘버로 사용하지 말고 명명된 상수(const)로 분리한다.
예: `return (int)type + 7;` → `private const int ReservedOffset = RawFoodReserved - RawFoodStock; return (int)type + ReservedOffset;`
**Why:** 인덱스 변경 시 조용한 버그 발생.

### 2. 성능 로그는 UNITY_EDITOR로 감싸기
ValidateIntegrity 같은 주기적 호출 메서드의 통과 로그는 `#if UNITY_EDITOR` 블록으로 감싸거나 완전히 제거한다.
**Why:** 주기적 호출 시 콘솔 오염 및 성능 비용.

### 3. NativeArray TempJob 수명 제약 문서화
TempJob 할당이 있는 팩토리 메서드에는 XML 주석에 "4프레임 이내 Dispose 필수, 클래스 필드 장기 보관 금지"를 명시한다.

### 4. IsCreated 방어 확인
NativeArray 접근 메서드에서 `_isDisposed` 단독 확인이 아닌 `_isDisposed || !Data.IsCreated` 조건을 사용한다.
**Why:** Dispose 후 상태 불일치 가능.

### 5. 중복 예약 방어 경고
Reserve() 에서 agentDict에 해당 type 키가 이미 존재하고 값 > 0이면 Debug.LogWarning으로 중복 예약 경고를 출력한다.
**Why:** GOAP Action 실수로 두 번 호출 시 과다 예약 발생.

### 6. 공개 메서드 bool 반환 일관성
설계 명세에서 '실패는 false 반환'을 요구하는 메서드는 반드시 bool을 반환해야 한다. void는 호출자가 오류를 조용히 무시하게 만든다.

### 7. 플래그 소비 순서 — 로컬 복사 우선
Brain의 Pending 데이터를 읽은 뒤 플래그(HasPendingOrder 등)를 false로 설정하는 패턴에서는 반드시 로컬 변수에 데이터를 복사한 뒤 플래그를 해제한다. 해제 이후에는 Brain 원본이 아닌 로컬 변수만 참조한다.
**Why:** 플래그를 먼저 false로 만든 뒤 Brain을 다시 읽으면 다른 경로에서 값이 덮어쓰이는 race condition 버그 발생 (F-002).

### 8. 코루틴 참조 저장 및 OnDestroy 해제
StartCoroutine 반환값을 Coroutine 타입 필드에 저장하고, OnDestroy()에서 StopCoroutine을 호출한다. 코루틴 내부의 'this != null' 조건은 MonoBehaviour 코루틴에서 도달 불가(dead code)이므로 제거한다.
**Why:** 씬 전환이나 Destroy 시 고아 코루틴이 계속 실행되는 버그 방지 (F-007).

### 9. 큐 처리 시 스냅샷 패턴
메시지 큐를 처리하는 루프에서 처리 중에 큐에 새 항목이 추가될 수 있다면, 루프 시작 시 Count를 캡처하여 그 수만큼만 처리한다(스냅샷 패턴). while(Count > 0) 패턴은 무한루프 위험.
**Why:** TryExecuteOrder 같은 내부 호출이 큐에 새 메시지를 추가하면 루프가 무한히 실행됨 (F-006).

### 10. 예약 해제 조건에 대상 상태 조건 불필요
OnStateExit Executing case에서 자원 예약 해제 조건에 '다음 상태가 Idle이 아닐 때'를 추가하지 않는다. 정상 완료는 OnActionCompleted에서 Commit 후 _hasActiveReservation=false가 되므로 Exit 시점에 true이면 항상 중단이다.
**Why:** Idle 예외 조건이 있으면 Executing → Idle 전이(정상 완료) 시 예약이 누수됨 (F-003).

### 11. 이벤트 디스패치 스냅샷 패턴
구독자 콜백을 순회할 때 역순 for 루프는 중간 Unsubscribe 안전성을 완전히 보장하지 않는다.
재사용 클래스 필드(List<T>)에 Clear+AddRange 스냅샷을 만들고, 각 콜백 실행 전 원본 목록에 Contains 체크로 이미 해제된 콜백을 건너뜀.
**Why:** [A,B,C] 순회 중 C가 A를 Unsubscribe하면 역순 루프는 C를 재실행하는 버그 발생 (R-001).

### 12. 디버그 통계 초기화 타이밍
Publish()가 ProcessTick() 이전에도 호출될 수 있는 카운터는 ProcessTick() 시작에서 초기화하지 않는다.
해당 Tick의 통계가 확정되는 ProcessTick() 끝에서 초기화한다.
**Why:** ProcessTick() 시작에서 초기화하면 Tick 전 누적된 카운트가 사라져 Inspector 통계가 오염됨 (R-002).

### 13. 매핑 테이블은 실제 강제 적용
설계 의도 테이블(우선순위 맵, 비용 테이블 등)이 코드에 존재하면 반드시 로직에서 참조하여 강제 적용한다.
문서 역할만 하는 테이블은 발행자 실수를 막지 못하므로 존재 이유가 없다.
**Why:** DEFAULT_PRIORITY_MAP이 미사용이면 발행자가 Priority를 잘못 설정해도 시스템이 모른다 (R-007).

### 14. 플래닝 분기 조건은 자원별 전용 플래그로 분리
여러 자원 타입을 처리하는 플래닝 분기에서 동일한 boolean 조건을 반복 사용하면 Dead Code가 발생한다.
자원 종류별 전용 Brain 플래그(NearRock, NearIronOre, NearCopperOre)를 만들어 각 분기를 분리한다.
**Why:** Miner가 MineIron/MineStone을 모두 HasTool && NearResource 조건으로 쓰면 MineStone에 절대 도달하지 못함 (R-001).

### 15. ScriptableObject 내부 배열은 Clone()으로 독립 사본 캐싱
ScriptableObject의 배열 필드를 클래스 필드에 직접 대입하지 않는다. Clone()으로 사본을 만들어야 에디터 핫 리로드 시 구 버전 참조를 방지한다.
**Why:** 에디터가 ScriptableObject를 재로드하면 원본 배열 객체가 교체되지만 직접 참조 필드는 구 버전을 가리킨다 (R-005).

### 16. Null 체크 조건을 복합 AND로 묶지 않기
ActionDatabase 경로 진입 조건에 _worldState null 체크를 함께 묶으면 worldState만 null일 때 조용히 폴백되어 버그 추적이 어렵다. 독립적인 null 체크로 분리하고 각각 LogError/LogWarning으로 원인을 명시한다.
**Why:** 복합 조건 실패는 어떤 값이 null인지 알 수 없어 디버깅 시간이 증가한다 (R-004).

### 17. Fallback Goal은 목표 달성 실패 맥락에 맞게 선택
Deadlock/Fallback Goal은 현재 실패한 Goal의 맥락을 고려해야 한다. P0 위기 실패 시 HungerLevel 기반 RestOnGround 같은 무관한 행동 대신 P0 Goal 재시도 또는 MoveToBase를 선택한다.
**Why:** Deadlock에서 목표와 무관한 행동을 실행하면 주민이 Goal 없이 표류하는 상태로 빠진다 (R-007).

### 18. 목표 이미 달성 vs 플래닝 실패 구분 (특수 반환값 패턴)
Job/플래너가 "이미 목표 달성" 상태일 때 실패 코드(0)와 동일한 값을 쓰지 않는다.
특수값(-1)을 사용하여 세 가지 결과(이미 달성=-1, 실패=0, 성공=양수)를 명확히 구분한다.
FSM 호출부는 out bool 파라미터 등으로 세 가지 경우를 각각 처리해야 한다.
**Why:** 0(이미 달성)과 0(실패)이 동일하면 FSM이 Replanning으로 전이하여 무한루프 발생 (N-001).
**How to apply:** GOAPPlannerJob → ReadResult() → VillagerFSM.State_Planning() 파이프라인에서 이미 적용됨.

### 19. 중복 계산 변수 제거 (상단 변수 재사용)
같은 메서드 내에서 동일한 registry.GetAvailable() 호출 결과를 다른 이름의 변수로 재선언하지 않는다.
상단에서 계산된 변수(availWood, availCooked 등)를 하단에서 그대로 재사용한다.
**Why:** 중복 계산은 불필요한 성능 비용 + 두 변수가 다른 값이 되는 유지보수 버그 위험 (N-002).

### 20. 이벤트 구독 안전성 — 이중 시도 + 참조 저장 패턴 (W-01, 2026-06-28)
UI 컴포넌트가 GameManager 이벤트를 구독할 때는 세 가지를 모두 준수한다:
  (1) Awake + Start 두 곳에서 TrySubscribe() 이중 시도 (실행 순서 차이 대비)
  (2) _subscribed 플래그로 중복 구독 방지
  (3) _gameManagerRef 필드에 구독 당시 참조를 저장하고, OnDestroy에서 Instance 대신 이 참조로 해제
**Why:** 씬 전환 도중 Instance가 먼저 null이 되는 경합 상황에서 구독 해제가 실패하여 메모리 누수 발생 가능.
**How to apply:** 이벤트 구독 UI 컴포넌트(GameResultPanel 등) 신규 작성 시 이 패턴을 템플릿으로 사용한다.

### 21. 엣지 케이스 동시 발생 처리 — GDD 명시 없을 때 보수적 가드 (W-02, 2026-06-28)
승리/패배 조건이 동일 틱에 동시 발생할 수 있는 경우(예: Silver Citadel 완성 + 전멸 동시 발생),
GDD에 명시가 없으면 안전한 방향으로 추가 가드를 넣는다(예: `aliveCount > 0`).
**Why:** 명시 없는 엣지 케이스를 방치하면 논리적으로 불가능한 "주민 없이 번영 승리" 상태가 될 수 있다.
**How to apply:** Win 조건 판정 코드 작성 시 패배 조건과 동시 성립 가능성을 항상 점검한다.

### 22. LogWarning → LogError 격상 기준 (S-01, 2026-06-28)
코드 버그(switch에 새 enum 값 미처리)나 씬 구성 오류(GameManager 미배치)처럼
개발자가 반드시 수정해야 하는 상황은 LogWarning이 아닌 LogError로 출력한다.
LogWarning은 "처리됐지만 비정상 상황", LogError는 "코드/씬 구성이 잘못된 상황"으로 구분한다.
**Why:** Warning은 콘솔에서 묻혀 무시되기 쉬우나 Error는 빨간색으로 즉시 주의를 끈다.
**How to apply:** default case, null GameManager 참조, enum 미처리 등은 LogError. 런타임 데이터 이상은 LogWarning.

**How to apply:** 위 규칙들은 코어 시스템 클래스(Registry, WorldState 계열)와 FSM 클래스, UI 컴포넌트 작성 시 항상 사전 점검한다.
