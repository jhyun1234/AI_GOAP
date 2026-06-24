# AI Village — GOAP AI 행동 아키텍처 설계 명세서 v1.0
## unity-senior-programmer 구현 전달용 설계 문서

---

**기반 문서:** GDD v0.4, TechSpec v1.0
**작성일:** 2026-06-25
**설계 에이전트:** unity-ai-behavior-architect
**대상 구현 에이전트:** unity-senior-programmer

---

## 0. 설계 대상 요약

### 설계할 시스템명과 범위

| 시스템 | 범위 |
|---|---|
| 주민(Villager) GOAP FSM 래퍼 | Full GOAP 플래너의 생명주기 관리 (8개 상태) |
| LOD AI 간소화 FSM | 원거리/비전투 유닛 경량 상태 기계 (4개 상태) |
| MessageBus 통신 설계 | 7개 메시지 타입 + 충돌 처리 규칙 |
| 팩션 GOAP AI 의사결정 구조 | 3개 팩션 침략 결정 시스템 |

### 이 시스템이 달성하는 게임플레이 목적

- 주민 50명이 각자 독립된 GOAP 플래너로 생존 목표를 자율 추구한다.
- 플레이어 명령은 "제안"이며, 주민은 충성도·생존 필요도 계산 후 수행 또는 거부한다.
- 적 팩션 3개도 동일한 GOAP 원칙으로 자원 부족 시 자율 침략을 결정한다.
- 60fps 프레임 예산(16.67ms) 내에서 최대 100유닛이 결정론적으로 동작한다.

### 연관 시스템 목록

- ResourceRegistry (자원 예약 시스템) — 가용량 = stock - reserved
- DangerRegistry (위험 정보 공유 레지스트리)
- NavMesh (이동 비용 휴리스틱 제공)
- MessageBus (모든 AI 간 통신 중개)
- FoW (isDiscovered 필터로 GOAP 플래닝 노드 제한)
- ScriptableObject 계층 (VillagerRecruitData, FactionInitialState 등)

---

## 1. 주민(Villager) GOAP FSM 래퍼 — 상태 전이 명세표

### 설계 원칙

FSM은 GOAP 플래너 자체가 아니다. FSM은 플래너의 실행/중단/재시작을 관리하는 "생명주기 관리자"다.
GOAP 플래너(A* 탐색)는 항상 Job System 스레드에서 실행되며, FSM은 메인 스레드에서 결과를 수신·실행한다.

```
[FSM 상태 전이 개요]

              ┌──────────────────────────────────────────┐
              │         AnyState 폴백 규칙               │
              │  P0 Goal 활성화 시 → Planning(P0 우선)   │
              │  isAlive=false 시 → Dead                 │
              └──────────────────────────────────────────┘

    ┌──────┐    플랜 요청     ┌──────────┐   플랜 완성    ┌───────────┐
    │ Idle │ ──────────────→ │ Planning │ ────────────→ │ Executing │
    └──────┘                 └──────────┘               └───────────┘
       ↑                          │                          │
       │     NoSolutionFound      │                          │ WorldState 무효화
       │◄─────────────────────────┘                          ↓
       │                                              ┌────────────┐
       │          재플랜 완성                          │ Replanning │
       │◄─────────────────────────────────────────────│            │
       │                                              └────────────┘
       │
       │  플레이어 명령 수신
       ↓
┌──────────────────┐    ConflictScore < 임계값    ┌───────────┐
│  CommandConflict │ ─────────────────────────→  │ Executing │
└──────────────────┘                              └───────────┘
       │
       │ ConflictScore >= 임계값
       ↓
┌───────────────┐    거부 완료(3초)   ┌──────┐
│ RefusingOrder │ ──────────────────→ │ Idle │
└───────────────┘                     └──────┘

     거리>30 + 비전투                 거리<=30 or 전투
┌──────┐ ─────────────────→ ┌──────────┐ ──────────────→ ┌──────┐
│ Idle │                    │ LOD_FSM  │                  │ Idle │
└──────┘                    └──────────┘                  └──────┘

  isAlive=false
모든 상태 ────────────────────────────────────────────────→ ┌──────┐
                                                             │ Dead │
                                                             └──────┘
```

---

### 상태 1: Idle

```
[Idle]: GOAP 플래너 대기 상태. 새 플랜이 필요한지 조건을 점검한다.

  진입 조건:
    - 게임 시작 초기
    - Executing 상태에서 플랜 내 모든 Action이 완료됨
    - RefusingOrder 상태에서 거부 메시지 표시 완료(3초 경과)
    - Replanning 상태에서 NoSolutionFound + Fallback 행동 완료
    - Dead 상태 외 모든 상태에서 Fallback 폴백 전이

  유지 조건:
    - 활성화된 Goal 없음 (모든 Goal 조건 미충족)
    - 재플랜 쿨다운(0.3~0.5초) 미경과

  탈출 조건 목록 (우선순위 순):
    1순위: isAlive == false → Dead
           (이유: 사망은 모든 로직보다 우선. AnyState 전이로도 처리)
    2순위: healthLevel < 20 → Planning (목표: SurviveInjury P0-1)
           (이유: 체력 0 = 즉사. 허기/피로보다 즉각적 위협)
    3순위: hungerLevel > 80 → Planning (목표: SurviveHunger P0-2)
           (이유: 허기 100 도달 시 체력 감소 루프 → 이차 사망 위험)
    4순위: fatigueLevel > 90 → Planning (목표: SurviveFatigue P0-3)
           (이유: 피로 100 = 기절, 전투/작업 불능. 허기보다 후순위)
    5순위: enemyNearby == true → Planning (목표: DefendVillage P1)
    6순위: OrderIssued 메시지 수신 → CommandConflict
    7순위: buildingQueued == true AND 자원 충족 → Planning (목표: BuildStructure P3)
    8순위: anyAvailableStock < 30 → Planning (목표: GatherResources P4)
    9순위: 거리 > 30타일 AND nearEnemy == false → LOD_FSM
    10순위: allAvailableStocks >= 50 AND unexploredTilesNearby == true
            → Planning (목표: Explore P5)

  진입 시 실행:
    - currentGoalId = null, currentActionId = null
    - replanCooldown 타이머 시작 (0.3~0.5초)
    - "도움 필요" 아이콘이 표시 중이면 deadlockCounter 유지 (해소 전까지)

  매 Tick 실행 (0.1초 간격, 그룹 분산):
    - 탈출 조건 목록을 1순위부터 순서대로 평가
    - 조건 충족 시 즉시 해당 상태로 전환, 이후 조건 평가 중단
    - 모든 조건 미충족 시 Idle 유지

  탈출 시 실행:
    - (없음 — 진입하는 상태에서 필요한 초기화 수행)

  엣지케이스:
    - P0 Goal 3개 동시 활성화: 서브 우선순위 고정 적용
      SurviveInjury > SurviveHunger > SurviveFatigue
      → Planning 진입 시 currentGoalId에 최상위 Goal 1개만 지정
    - replanCooldown 중 P0 발동: 쿨다운 무시하고 즉시 Planning 전환
      (P0는 쿨다운 예외 — TechSpec EX-002 §5)
```

---

### 상태 2: Planning

```
[Planning]: GOAP A* 플래너가 최적 Action 시퀀스를 탐색하는 상태.
           플래닝 연산은 Job System 스레드에서 실행, FSM은 결과 대기.

  진입 조건:
    - Idle 상태에서 Goal 활성화 조건 충족
    - Replanning 상태에서 재플랜 트리거 발생
    - AnyState에서 P0 Goal 즉시 발동 (쿨다운 무시)

  유지 조건:
    - Job System 스레드에서 플래닝 연산 진행 중
    - 결과 수신 대기 중

  탈출 조건 목록 (우선순위 순):
    1순위: isAlive == false → Dead
    2순위: Job System에서 유효한 플랜 수신 → Executing
           (유효 플랜 = Action 시퀀스 길이 >= 1 AND Depth <= 6)
    3순위: Job System에서 NoSolutionFound 수신 → Replanning
           (Fallback 처리를 Replanning 상태에서 수행)
    4순위: Planning 상태 진입 후 최대 0.5초 초과 → Replanning
           (타임아웃 — 무한 대기 방지)

  진입 시 실행:
    - WorldState 플래닝용 스냅샷 생성 (읽기 전용 복사본)
    - Job System에 플래닝 작업 제출
      파라미터: { goalId, worldStateSnapshot, maxDepth=6, agentRole }
    - planningStartTime = Time.time 기록
    - isExecutingPlan = false

  매 Tick 실행 (0.1초 간격):
    - Job System 결과 큐 폴링
    - 타임아웃 체크: Time.time - planningStartTime > 0.5초 → Replanning
    - P0 Goal 새로 발동 감지:
      현재 플래닝 중인 Goal이 P0보다 낮은 우선순위이면
      → Job System 작업 취소 → P0 Goal로 즉시 재진입

  탈출 시 실행:
    - 수신한 플랜을 currentPlan 큐에 저장
    - lastReplanTimestamp = Time.time
    - replanCooldown 타이머 리셋

  엣지케이스:
    - P0 플래닝 중 추가 P0 Goal 발동:
      서브 우선순위로 더 높은 P0가 있으면 취소 후 재플래닝
      같은 우선순위 P0면 기존 플래닝 유지
    - WorldState 스냅샷 생성 시점과 Job System 실행 시점 사이에
      Authoritative State 변화 발생 가능 → 이는 Executing 진입 시
      Precondition 재검증으로 처리 (EX-005 대응)
    - FoW 필터: 플래닝 스냅샷에 포함되는 자원 노드는
      isDiscovered == true인 것만 포함
```

---

### 상태 3: Executing

```
[Executing]: currentPlan 큐의 Action을 순서대로 실행하는 상태.
            Action 1개 완료 → 다음 Action 시작, 큐 소진 → Idle.

  진입 조건:
    - Planning 상태에서 유효한 플랜 수신
    - CommandConflict 상태에서 ConflictScore < 임계값 판정 + 명령 플랜 수신

  유지 조건:
    - currentPlan 큐에 실행 대기 Action이 존재
    - 현재 실행 중인 Action의 Precondition이 유효

  탈출 조건 목록 (우선순위 순):
    1순위: isAlive == false → Dead
    2순위: P0 Goal 새로 활성화 (현재 Goal이 P0보다 낮은 경우) → Planning
           (이유: 생존 위협은 어떤 작업도 중단시킨다)
    3순위: 현재 Action의 Precondition 무효화 → Replanning
           (이유: 월드 상태 변화로 계획이 실행 불가능해짐)
    4순위: OrderIssued 메시지 수신 → CommandConflict
           (이유: 플레이어 명령은 반드시 ConflictScore 계산 후 처리)
    5순위: currentPlan 큐 소진 (모든 Action 완료) → Idle
    6순위: 거리 > 30타일 AND nearEnemy == false AND 현재 Action이 이동 계열 →
           LOD_FSM (단, 현재 Action 완료 후 전환)

  진입 시 실행:
    - isExecutingPlan = true
    - currentPlan 큐에서 첫 번째 Action 꺼내기
    - 해당 Action의 Precondition 재검증 (Authoritative State 기준)
      재검증 실패 시 → Replanning 즉시 전환
    - Action이 자원 소모를 요구하면 ResourceRegistry.Reserve() 호출
      가용량 부족 시 → Replanning 즉시 전환

  매 Tick 실행 (0.1초 간격):
    - Action 실행 진행도 업데이트
    - Action 완료 감지:
      → Authoritative State에 Effect 적용 (메인 스레드 전용)
      → ResourceRegistry.Commit() 호출 (reserved -= amount, stock -= amount)
      → currentPlan 큐에서 다음 Action 꺼내기 + Precondition 재검증
    - 현재 Action의 Precondition 매 Tick 모니터링
      (자원 고갈, 다른 주민이 선점 등 외부 변화 감지)
    - replanCount[currentGoalId] 30초 윈도우 내 10회 초과 여부 체크

  탈출 시 실행:
    - isExecutingPlan = false
    - 미완료 Action이 있는 경우 ResourceRegistry.Release() 호출
      (reserved -= 예약됐으나 소모되지 않은 수량)
    - 사망/거부/재플랜 등 비정상 종료 시 currentPlan = null

  엣지케이스:
    - 건설 Action(BuildStructure)에 다중 주민 배정:
      BuildingQueue.assignedVillagerIds에 자신의 ID 추가
      슬롯 3개 초과 시 → 다른 BuildingQueue 항목으로 Re-plan
    - DroppedItem 수거 (PickUpDroppedItem):
      nearDroppedItem == true AND 아이템 타입이 현재 Goal에 유효한 경우
      GOAP 플래너가 이를 선행 Action으로 자동 삽입
    - Action 실행 중 적이 근접(nearEnemy = true 갱신):
      P1 DefendVillage Goal 활성화 → Replanning 트리거
```

---

### 상태 4: Replanning

```
[Replanning]: WorldState 변화 또는 플랜 실패로 재플래닝이 필요한 상태.
             쿨다운 관리와 Fallback 처리를 담당한다.

  진입 조건:
    - Executing 상태에서 현재 Action의 Precondition 무효화
    - Planning 상태에서 NoSolutionFound 수신
    - Planning 상태에서 타임아웃(0.5초) 발생
    - 탐험으로 isDiscovered=true 노드 신규 생성 → GatherResources 재평가

  유지 조건:
    - replanCooldown 타이머 미경과 (0.3~0.5초)
    - P0 Goal 미발동 (P0 발동 시 쿨다운 무시하고 즉시 Planning 전환)

  탈출 조건 목록 (우선순위 순):
    1순위: isAlive == false → Dead
    2순위: P0 Goal 활성화 → Planning (쿨다운 무시)
    3순위: replanCooldown 경과 → Planning (재플래닝 시도)
    4순위: 연속 Fallback 3회 완료 → Idle
           ("도움 필요" 플래그 설정 + UI 알림 발생)

  진입 시 실행:
    - 현재 실행 중이던 Action 중단
    - ResourceRegistry.Release() — 예약된 자원 전량 해제
    - replanCooldown = Random(0.3f, 0.5f) 시작
    - replanReason 기록 (GOAPActionLog에 기록)
    - fallbackCounter 체크:
      이전 플랜과 동일한 Goal에서 NoSolutionFound면 fallbackCounter++
      fallbackCounter >= 3 → Deadlock 처리 시작

  매 Tick 실행 (0.1초 간격):
    - replanCooldown 타이머 카운트다운
    - P0 Goal 즉시 감지 (쿨다운 무시 탈출 조건)
    - Deadlock 처리 (fallbackCounter >= 3일 때):
      a. 현재 Goal 우선순위보다 한 단계 낮은 Goal로 전환 준비
         (단, P0 Goal은 낮추지 않음 — TechSpec EX-001)
      b. hungerLevel < 50 → Fallback Action: RestOnGround
         그 외 → Fallback Action: MoveToBase
      c. "도움 필요" UI 플래그 = true

  탈출 시 실행:
    - fallbackCounter 리셋 (재플래닝 성공 시에만)
    - 다음 상태가 Planning이면: GOAPActionLog에 replanReason 기록

  엣지케이스:
    - 동일 Goal에 30초 내 10회 이상 재플래닝:
      → 강제로 다음 우선순위 Goal로 전환 (replanCount 카운터 리셋)
    - 이전 플랜 == 새 플랜: 재플랜 결과를 폐기하고 현재 플랜 유지
      (TechSpec EX-002: 불필요한 재플랜 방지)
    - ResourceDiscovered 메시지 수신 중 Replanning 상태:
      → 쿨다운 경과 즉시 Planning으로 전환하여 새 노드 포함 재플래닝
```

---

### 상태 5: CommandConflict

```
[CommandConflict]: 플레이어 명령 수신 후 ConflictScore를 계산하는 상태.
                   수행 가능 여부를 결정하고, 가능하면 플랜으로 전환한다.

  진입 조건:
    - OrderIssued 메시지 수신 (Idle, Executing 상태 모두에서 진입 가능)
    - Executing 상태에서 진입 시 현재 Action을 일시 중단

  유지 조건:
    - ConflictScore 계산 진행 중 (최대 0.1초 이내 완료 — 동기 계산)

  탈출 조건 목록 (우선순위 순):
    1순위: isAlive == false → Dead
    2순위: P0 Goal 활성화 감지 (계산 중에라도) → Replanning
           (이유: P0는 명령 처리보다 우선)
    3순위: ConflictScore >= 임계값 → RefusingOrder
           임계값 = 2.5 × (loyaltyLevel / 50)
    4순위: ConflictScore < 임계값 → Planning (명령 플랜 생성)
           (ExecutePlayerOrder Goal로 플래닝 시작)

  진입 시 실행:
    - 수신된 OrderIssued 메시지 파싱 (명령 종류, 파라미터 추출)
    - Executing 상태에서 진입 시: 현재 Action 일시 중단 플래그 설정
      (실행 중이던 자원 예약은 유지)
    - ConflictScore 계산 시작:
      ConflictScore = Σ (Need_urgency[i] × Order_impact[i])

      Need_urgency 계산:
        hungerLevel > 80  → Need_urgency[hunger]  = (hungerLevel - 80) / 20  (0~1)
        healthLevel < 20  → Need_urgency[health]  = (20 - healthLevel) / 20   (0~1)
        fatigueLevel > 90 → Need_urgency[fatigue] = (fatigueLevel - 90) / 10  (0~1)
        nearEnemy == true → Need_urgency[safety]  = 0.8 (고정)
        (조건 미충족 시 해당 urgency = 0)

      Order_impact 계산:
        명령이 AtBase를 요구하는데 현재 위치가 멀면 → impact += 0.5
        명령 실행에 체력 소모가 있는데 healthLevel < 40이면 → impact += 0.7
        명령이 전투 관련이고 hasWeapon == false이면 → impact += 1.0
        명령이 자원 수집인데 hasTool == false이면 → impact += 0.3

  매 Tick 실행:
    - (계산이 동기적이므로 통상 1 Tick 이내 완료)
    - loyalty 구간별 Cost Modifier는 Planning 단계에서 적용
      (CommandConflict에서는 거부 임계값 계산에만 loyalty 사용)

  탈출 시 실행:
    - ConflictScore, 임계값, 거부 여부를 GOAPActionLog에 기록
    - 수행 결정 시: Executing에서 중단했던 예약 자원 해제 후 새 플랜으로 교체

  엣지케이스:
    - loyalty < 30인 상태에서 명령 수신:
      임계값 = 2.5 × (30 / 50) = 1.5 (매우 낮음)
      → 사소한 필요 충돌에도 거부 발생 용이
      거부 메시지: "왜 제가 그걸 해야 하죠?"
    - 복수의 OrderIssued 메시지가 동시 수신된 경우:
      MessageBus 우선순위 High > Medium > Low 순서로 1개만 처리
      나머지는 큐에서 폐기 (주민은 한 번에 하나의 명령만 평가)
    - Executing 중 명령 수신 시:
      현재 P0 Action을 중단하지 않음
      P0 Action 완료 후 다음 Tick에 CommandConflict 진입
```

---

### 상태 6: RefusingOrder

```
[RefusingOrder]: 명령 거부를 결정하고 UI 메시지를 표시하는 상태.
                3초간 거부 메시지를 표시한 후 자율 행동으로 복귀한다.

  진입 조건:
    - CommandConflict 상태에서 ConflictScore >= 임계값 판정

  유지 조건:
    - 거부 메시지 표시 타이머(3초) 진행 중

  탈출 조건 목록 (우선순위 순):
    1순위: isAlive == false → Dead
    2순위: P0 Goal 활성화 → Replanning (생존 우선, 즉시 전환)
    3순위: 3초 경과 → Idle (자율 행동 재개)

  진입 시 실행:
    - OrderRefused 메시지 발행 (MessageBus — UI 시스템이 구독)
      페이로드: { villagerId, refusalReasonCode, refusalMessage }
    - 거부 이유 코드 결정 (아래 표 참조):

      거부 이유 코드 → UI 메시지 → AI 대안 행동 Goal:
      REFUSE_HUNGER   → "너무 배고파요. 밥 먹고 바로 할게요!"   → SurviveHunger
      REFUSE_INJURY   → "부상이 심해요. 치료 후 하겠습니다."     → SurviveInjury
      REFUSE_FATIGUE  → "지쳐서 쓰러질 것 같아요..."            → SurviveFatigue
      REFUSE_LOYALTY  → "왜 제가 그걸 해야 하죠?"               → P4~P5 자율 수행
      REFUSE_DANGER   → "무장 없이는 너무 위험해요!"             → CraftPrimitiveWeapon

      우선 적용 기준:
        healthLevel < 20  → REFUSE_INJURY
        hungerLevel > 80  → REFUSE_HUNGER
        fatigueLevel > 90 → REFUSE_FATIGUE
        loyaltyLevel < 30 → REFUSE_LOYALTY
        nearEnemy + !hasWeapon → REFUSE_DANGER

    - alternativeGoal 설정 (거부 이유에 맞는 대안 행동)
    - refuseMessageTimer = 3.0f 시작
    - loyalty -= 0 (거부 자체는 페널티 없음 — 플레이어의 명령 강제 이행 시 -20)

  매 Tick 실행 (0.1초 간격):
    - refuseMessageTimer 카운트다운
    - P0 Goal 즉시 감지

  탈출 시 실행:
    - refuseMessageTimer 리셋
    - Idle 진입 시: alternativeGoal을 Idle의 첫 번째 평가 힌트로 설정
      (Idle에서 다음 Tick에 해당 Goal로 Planning 전환 유도)

  엣지케이스:
    - 거부 표시 중 플레이어가 같은 명령을 다시 발행:
      두 번째 명령도 동일하게 CommandConflict 처리
      (거부 상태 타이머 리셋 없음 — 현재 거부 완료 후 재평가)
    - 거부 직후 상황 개선으로 재수행 가능:
      Idle 복귀 후 자연스럽게 Goal 재평가로 처리
      (별도의 "재수락" 메커니즘 없음 — 명령은 소멸하고 자율 행동으로 대체)
```

---

### 상태 7: Dead

```
[Dead]: 주민 사망 처리를 완료하고 엔티티를 비활성화하는 최종 상태.
       이 상태에서는 탈출 경로가 없다. (Dead는 최종 상태)

  진입 조건:
    - isAlive == false가 감지된 모든 상태 (AnyState 전이)
    - healthLevel == 0 (전투 피해, 굶주림 등)

  유지 조건:
    - 항상 (Dead는 최종 상태, 탈출 없음)

  탈출 조건:
    - 없음 (Dead는 FSM의 최종 흡수 상태)

  진입 시 실행 (순서 엄수):
    1. isAlive = false 확인 (이미 설정됨)
    2. isExecutingPlan = false
    3. 모든 자원 예약 즉시 해제:
       ResourceRegistry.ReleaseAll(villagerId)
       → rawFoodReserved, cookedFoodReserved, woodReserved,
         stoneReserved, ironReserved, copperReserved, silverReserved 전부 0
    4. 인벤토리 → DroppedItem 엔티티 생성:
       hasTool == true → DroppedItem(toolType, tileX, tileY) 생성
       hasWeapon == true → DroppedItem(Weapon, tileX, tileY) 생성
       hasPrimitiveWeapon == true → DroppedItem(PrimitiveWeapon, tileX, tileY) 생성
       hasFood == true → DroppedItem(Food, tileX, tileY) 생성
    5. WorldState.droppedItems 리스트에 생성된 DroppedItem 추가
    6. VillagerDied 메시지 발행 (MessageBus):
       페이로드: { villagerId, deathPosition, droppedItemIds[], freedReservations{} }
    7. 주변 15타일 내 모든 주민의 nearDroppedItem 플래그 갱신
       (DangerRegistry 아닌 MessageBus VillagerDied 구독자가 처리)
    8. 주변 15타일 내 주민: loyalty -= 10, mood -= 15 (목격 페널티)
    9. BuildingQueue에서 자신이 담당하던 항목 제거 (assignedVillagerIds에서 ID 제거)
    10. GOAPActionLog에 사망 기록 (planResult = "Interrupted", replanReason = "Death")
    11. 게임 오브젝트 비활성화 또는 사망 애니메이션 재생 후 비활성화

  매 Tick 실행:
    - 없음 (Dead 상태에서 Tick 처리 없음)

  탈출 시 실행:
    - 없음

  엣지케이스:
    - 동시에 2명 이상 사망 (전투 등):
      VillagerDied 메시지가 같은 Tick에 복수 발행 가능
      MessageBus는 큐 방식으로 순차 처리 (동시 처리 없음)
      loyalty/mood 페널티가 중복 적용될 수 있음 → 정상 동작 (누적 페널티)
    - 사망 시 건설 중인 건물이 있었던 경우:
      BuildingQueue.assignedVillagerIds에서 ID 제거 후
      currentSpeedMultiplier 자동 재계산 (배치 인원 -1)
    - 모든 주민 사망 → 패배 조건 체크:
      VillagerDied 메시지를 GameManager가 구독하여 생존 주민 수 확인
      생존 주민 0 → 게임 오버 처리
```

---

### 상태 8: LOD_FSM

```
[LOD_FSM]: 30타일 초과 거리 + 비전투 유닛이 사용하는 경량 상태.
           Full GOAP 대신 간소화된 행동 루프를 실행한다.
           (LOD AI 세부 상태는 섹션 2에서 별도 정의)

  진입 조건:
    - 카메라/플레이어 기지로부터 거리 > 30타일
    AND nearEnemy == false
    AND 현재 상태가 Idle 또는 Executing(이동 계열 Action 완료 후)

  유지 조건:
    - 거리 > 30타일
    AND nearEnemy == false
    AND 팩션 전투 이벤트 미발생

  탈출 조건 목록 (우선순위 순):
    1순위: isAlive == false → Dead
    2순위: nearEnemy == true → Idle (Full GOAP 즉시 재활성화)
           (이유: 전투 상황에서는 반드시 Full GOAP 정밀 판단 필요)
    3순위: 거리 <= 30타일 → Idle (Full GOAP 재활성화)
    4순위: EnemyDetected 메시지 수신 → Idle (Full GOAP 재활성화)
    5순위: VillagerDied 메시지 수신 (주변) → Idle (Full GOAP 재활성화)
           (이유: 위협 상황 변화, Full GOAP로 정확한 판단 필요)

  진입 시 실행:
    - GOAP 플래너 Job System 작업 취소 (진행 중이면)
    - currentPlan 큐 클리어
    - ResourceRegistry.Release() — 미사용 예약 자원 해제
    - LOD 내부 상태 머신 시작 (LOD_Idle로 초기화)
    - isLODMode = true 플래그 설정

  매 Tick 실행 (0.1초 간격, 단 LOD는 0.5초 간격으로 Tick 빈도 감소 가능):
    - 탈출 조건 점검
    - LOD 내부 상태 머신 Tick 실행

  탈출 시 실행:
    - isLODMode = false
    - LOD 내부 상태 초기화
    - Idle로 전환 후 즉시 GOAP 재평가 (Full GOAP Planning 재시작)

  엣지케이스:
    - LOD 상태에서 자원 수집 중 DroppedItem 발생:
      VillagerDied 메시지 구독 → 거리 15타일 이내면 LOD에서도 감지 가능
      단, nearDroppedItem 갱신은 Full GOAP 복귀 후 처리
    - LOD 중 P0 Goal 발동:
      LOD Tick에서 P0 조건 체크 포함 필요
      P0 발동 → 즉시 Dead(isAlive=false) or Idle(Full GOAP P0 처리)
```

---

## 2. LOD AI 간소화 FSM — 상태 전이 명세표

### 설계 원칙

LOD FSM은 VillagerFSM의 LOD_FSM 상태 내에서 동작하는 중첩 FSM이다.
Full GOAP의 A* 탐색 없이 간단한 조건 분기만으로 행동을 결정한다.
Tick 빈도: 0.5초 간격 (Full GOAP의 5배 절약).

---

### LOD 상태 1: LOD_Idle

```
[LOD_Idle]: LOD 모드의 대기 및 목표 결정 상태.

  진입 조건:
    - LOD_FSM 진입 시 초기 상태
    - LOD_GatheringResource 완료 후
    - LOD_MovingToBase 완료 후

  유지 조건:
    - 활성 목표 없음

  탈출 조건 (우선순위 순):
    1순위: nearEnemy == true OR EnemyDetected 수신 → LOD_Alert
    2순위: anyAvailableStock < 30 AND 주변 자원 노드 존재 → LOD_GatheringResource
    3순위: 인벤토리에 수집 자원 있음 → LOD_MovingToBase
    4순위: (모든 조건 미충족 시) LOD_Idle 유지 (대기)

  진입 시 실행:
    - 주변 자원 노드 간단 스캔 (isDiscovered == true인 노드만)
    - 목표 자원 타입 결정 (가장 부족한 stock 타입)

  매 Tick 실행 (0.5초 간격):
    - 탈출 조건 점검

  엣지케이스:
    - 주변에 isDiscovered 자원 노드 없음:
      LOD_Idle 유지 (탐험 불가 — LOD는 Explore 불수행)
      → Full GOAP 복귀 후 Explore Goal로 처리
```

---

### LOD 상태 2: LOD_GatheringResource

```
[LOD_GatheringResource]: 간소화된 자원 수집 실행 상태.
                        경로 계산 없이 목표 노드 방향으로 이동 후 수집.

  진입 조건:
    - LOD_Idle에서 자원 부족 + 주변 노드 존재 확인

  유지 조건:
    - 목표 자원 노드 도달 전 또는 수집 완료 전

  탈출 조건 (우선순위 순):
    1순위: nearEnemy == true OR EnemyDetected 수신 → LOD_Alert
    2순위: 자원 수집 완료 → LOD_MovingToBase
    3순위: 목표 노드 고갈(ResourceDepleted 수신) → LOD_Idle (재목표 설정)
    4순위: 인벤토리 가득 참 → LOD_MovingToBase

  진입 시 실행:
    - 목표 노드 타일 좌표 설정
    - LOD 이동 시작 (NavMesh 비동기 요청, 낮은 우선순위)
    - ResourceRegistry.Reserve() 호출 (수집 예정 자원 예약)

  매 Tick 실행 (0.5초 간격):
    - 이동 진행 (간소화: 직선 이동 시뮬레이션, 장애물 무시 가능)
    - 목표 노드 도달 시 수집 처리 (AnimatorController 없이 수치만 갱신)

  엣지케이스:
    - hasTool == false인데 ChopWood/MineStone 시도:
      LOD는 도구 없이도 수집 허용하되 효율 50% 적용
      (LOD 정확도보다 성능 우선)
```

---

### LOD 상태 3: LOD_MovingToBase

```
[LOD_MovingToBase]: 수집한 자원을 기지로 가져가는 상태.

  진입 조건:
    - LOD_GatheringResource에서 수집 완료
    - 인벤토리 자원 보유 중

  유지 조건:
    - 기지 도달 전

  탈출 조건 (우선순위 순):
    1순위: nearEnemy == true → LOD_Alert
    2순위: 기지 도달 → LOD_Idle (자원 stock에 추가)

  진입 시 실행:
    - 기지 타일 좌표로 이동 목표 설정

  매 Tick 실행 (0.5초 간격):
    - 기지 방향 이동 처리

  탈출 시 실행:
    - 기지 도달 시: stock += 수집량, ResourceRegistry.Commit() 호출
    - 인벤토리 클리어
```

---

### LOD 상태 4: LOD_Alert

```
[LOD_Alert]: 전투 감지 시 Full GOAP 복귀를 준비하는 상태.
            LOD에서 직접 전투하지 않는다.

  진입 조건:
    - nearEnemy == true 감지
    - EnemyDetected 메시지 수신

  유지 조건:
    - 없음 (진입 즉시 Full GOAP 복귀 준비)

  탈출 조건:
    1순위: 항상 즉시 → VillagerFSM.Idle (Full GOAP 재활성화)
           (LOD_Alert는 경유 상태, 즉시 탈출)

  진입 시 실행:
    - ResourceRegistry.Release() — 예약 자원 해제
    - VillagerFSM.LOD_FSM 탈출 트리거

  엣지케이스:
    - Full GOAP 재활성화 후 DefendVillage Goal이 즉시 활성화됨
```

---

## 3. MessageBus 통신 명세

### 설계 원칙

- 모든 AI 간 직접 통신 금지. 오직 MessageBus 또는 공유 레지스트리를 통해서만 정보 교환.
- MessageBus는 발행-구독(Pub-Sub) 패턴. 발행자는 구독자를 알지 못한다.
- 메시지는 우선순위 큐로 처리: High → Medium → Low 순서.
- 같은 Tick에 복수 메시지 수신 시 우선순위 높은 것부터 처리.
- 메시지는 처리 완료 후 즉시 큐에서 제거 (영속 저장 없음).

---

### 메시지 타입 1: VillagerDied

```
[VillagerDied]
  발행자: VillagerFSM (Dead 상태 진입 시)
  구독자:
    - 모든 주변 VillagerFSM (15타일 이내) — nearDroppedItem 갱신, loyalty/mood 페널티
    - ResourceRegistry — 예약 해제 확인 (이중 안전장치)
    - BuildingQueue 관리자 — assignedVillagerIds에서 제거
    - GameManager — 생존 주민 수 체크 (패배 조건 평가)
    - UIManager — 사망 알림 토스트

  발행 조건: isAlive가 true에서 false로 변경되는 순간 (Dead 상태 진입 시 1회)

  데이터 페이로드:
    {
      villagerId: string,
      deathTileX: int,
      deathTileY: int,
      droppedItems: [
        { itemType: enum(Axe|Pickaxe|Weapon|PrimitiveWeapon|Food), tileX: int, tileY: int }
      ],
      freedReservations: {
        rawFood: float, cookedFood: float, wood: float,
        stone: float, iron: float, copper: float, silver: float
      },
      nearbyVillagerIds: [string]  // 15타일 이내 주민 ID (발행 전 계산)
    }

  수신 후 처리:
    VillagerFSM (15타일 이내):
      → nearDroppedItem = true (드롭 아이템 타입이 현재 Goal에 유효한 경우)
      → loyalty -= 10, mood -= 15
      → Executing 상태라면 Replanning 유도 (DroppedItem 수거 기회 인식)

    ResourceRegistry:
      → 해당 villagerId의 모든 예약 확인 및 해제 (Dead 상태에서 이미 해제했지만 이중 확인)

    BuildingQueue 관리자:
      → 해당 villagerId가 포함된 모든 큐 항목에서 ID 제거
      → currentSpeedMultiplier 재계산

    GameManager:
      → 생존 주민 수 -= 1
      → 생존 주민 수 == 0 → 패배 이벤트 발생
      → Town Hall 파괴 AND 생존 주민 <= 2 → 패배 이벤트 발생

  유효 시간: 즉시 처리 (수신 즉시 처리, 이후 큐에서 제거)
  우선순위: High
```

---

### 메시지 타입 2: EnemyDetected

```
[EnemyDetected]
  발행자:
    - VillagerFSM (nearEnemy = true 갱신 감지 시)
    - Watchtower 감지 시스템 (감지 범위 내 적 유닛 진입 시)

  구독자:
    - 모든 주민 VillagerFSM
    - 팩션 관리자 (AssessPlayerStrength 재계산 트리거)
    - UIManager (레이드 경보 알림)

  발행 조건:
    - 주민 주변 타일에 적 유닛 출현
    - Watchtower 존재 시: 탐지 범위 ×2 적용

  데이터 페이로드:
    {
      enemyTileX: int,
      enemyTileY: int,
      enemyFactionId: string,
      threatTier: int,         // 1(소형동물) ~ 4(팩션 레이드)
      detectedByVillagerId: string,
      detectedAt: float        // 게임 시간
    }

  수신 후 처리:
    VillagerFSM (전체):
      → enemyNearby = true (WorldState 전역 변수 갱신)
      → P1 DefendVillage Goal 활성화 평가
      → LOD_FSM 상태라면 LOD_Alert → Idle (Full GOAP 즉시 복귀)
      → Idle/Executing 상태라면 Replanning 또는 즉시 Planning(DefendVillage)

    팩션 관리자:
      → allVillagersAlert = true
      → AssessPlayerStrength 재계산 (방어 상태 반영)

  유효 시간: 즉시 처리. enemyNearby는 적 유닛 사라질 때까지 유지.
  우선순위: High
```

---

### 메시지 타입 3: ResourceDiscovered

```
[ResourceDiscovered]
  발행자: VillagerFSM (Explore Action 실행 중 isDiscovered=true 노드 생성 시)

  구독자:
    - 모든 주민 VillagerFSM
    - FactionGOAP (팩션 AI 자원 평가 갱신)

  발행 조건:
    - Explore Action 실행 완료 시 (주변 15타일 내 노드 isDiscovered=true 전환)
    - 발견된 노드가 1개 이상일 때 발행

  데이터 페이로드:
    {
      discoveredNodes: [
        { nodeId: string, resourceType: enum, tileX: int, tileY: int, currentAmount: float }
      ],
      explorerVillagerId: string
    }

  수신 후 처리:
    VillagerFSM:
      → GatherResources Goal 재평가 (새 노드가 현재 Goal보다 유리한지 확인)
      → Replanning 또는 Executing 중이면 재플래닝 큐에 추가
      → Idle 상태면 즉시 GatherResources Planning 전환 고려

    FactionGOAP:
      → 발견 노드가 팩션 영역에 있으면 factionStrength 보정 재계산

  유효 시간: 즉시 처리 (노드 정보는 ResourceRegistry에 영속 저장)
  우선순위: Medium
```

---

### 메시지 타입 4: ResourceDepleted

```
[ResourceDepleted]
  발행자: ResourceNode (currentAmount == 0 도달 시)

  구독자:
    - 해당 노드를 목표로 Executing 중인 VillagerFSM
    - ResourceRegistry

  발행 조건: 자원 노드의 currentAmount가 0이 되는 순간

  데이터 페이로드:
    {
      nodeId: string,
      resourceType: enum,
      tileX: int,
      tileY: int,
      regenerationRate: float,   // 재생 속도 (0이면 영구 고갈)
      estimatedRecoveryTime: float
    }

  수신 후 처리:
    VillagerFSM (해당 노드 대상 Action 실행 중):
      → Replanning 즉시 트리거 (현재 Action의 Precondition 무효화)
      → 동일 자원 타입의 다른 노드로 재플래닝

    ResourceRegistry:
      → 해당 노드 상태 isDepleted = true 갱신
      → 재생 타이머 시작 (regenerationRate > 0인 경우)

  유효 시간: 즉시 처리
  우선순위: Medium
```

---

### 메시지 타입 5: OrderIssued

```
[OrderIssued]
  발행자: PlayerInputManager (플레이어가 명령 입력 시)

  구독자:
    - 지정된 VillagerFSM (1명 또는 복수 지정)

  발행 조건: 플레이어가 UI를 통해 특정 주민에게 명령 입력 시

  데이터 페이로드:
    {
      targetVillagerId: string,          // 명령 대상 주민 ID
      orderType: enum,                   // GatherWood | BuildStructure | Attack | Move | ...
      orderParameters: { ... },          // 명령별 파라미터 (목표 타일, 건물 타입 등)
      playerAutomationLevel: enum,       // FullAuto | SemiAuto | Manual
      issuedAt: float
    }

  수신 후 처리:
    VillagerFSM:
      → 현재 상태에 관계없이 CommandConflict 상태로 전환
      → (단, Dead 상태와 P0 Action 실행 중은 예외 — P0 완료 후 처리)
      → ConflictScore 계산 시작

  유효 시간: 즉시 처리. CommandConflict 전환 후 메시지 소멸.
  우선순위: Medium
  (P0 Goal과 동시 수신 시 P0가 High이므로 먼저 처리됨)
```

---

### 메시지 타입 6: OrderRefused

```
[OrderRefused]
  발행자: VillagerFSM (RefusingOrder 상태 진입 시)

  구독자:
    - UIManager (말풍선 메시지 표시)
    - PlayerInputManager (명령 거부 이벤트 기록)
    - GOAPActionLog (기록용)

  발행 조건: CommandConflict 상태에서 ConflictScore >= 임계값 판정 시

  데이터 페이로드:
    {
      villagerId: string,
      refusalReasonCode: enum,          // REFUSE_HUNGER | REFUSE_INJURY | ...
      refusalMessage: string,           // UI 표시용 텍스트
      conflictScore: float,
      threshold: float,
      alternativeGoalId: string,        // 대신 수행할 Goal ID
      loyaltyLevel: float               // 현재 충성도 (디버그용)
    }

  수신 후 처리:
    UIManager:
      → 해당 주민 위치에 말풍선 표시 (3초)
      → 거부 이유 아이콘 표시 (빨간색 = 위험, 노란색 = 필요 충족 부재)

    PlayerInputManager:
      → 거부 이벤트 기록 (연속 거부 횟수 카운팅)
      → 연속 3회 이상 거부 시 "자율화 레벨 상향 추천" UI 힌트 표시

  유효 시간: UIManager에서 3초 표시 후 자동 소멸
  우선순위: Low
```

---

### 메시지 타입 7: RaidDecision

```
[RaidDecision]
  발행자: FactionGOAP (침략 결정 Goal 활성화 시)

  구독자:
    - 해당 팩션의 모든 유닛 GOAP 플래너
    - DangerRegistry (위협 정보 등록)
    - UIManager (레이드 경보)
    - GameManager (레이드 추적)

  발행 조건:
    팩션 GOAP에서 RaidDecision = true 판정 시
    (copperStock < 10 OR silverStock < 5)
    AND nearPlayerTerritory == true
    AND playerStrength < factionStrength × 0.8

  데이터 페이로드:
    {
      factionId: string,
      targetFactionId: string,          // 항상 "player"
      raidStartTile: { x: int, y: int },
      participatingUnitIds: [string],
      estimatedStrength: float,
      raidTriggerReason: string,        // "ResourceDeficit_Copper" 등
      activationDay: int
    }

  수신 후 처리:
    팩션 유닛 GOAP:
      → ExpandTerritory Goal 활성화
      → 집결 타일로 이동 후 레이드 시작

    DangerRegistry:
      → 플레이어 마을 방향 위협 정보 등록
      → 모든 주민 VillagerFSM에 EnemyDetected 메시지 연쇄 발행 트리거

    UIManager:
      → "레이드 경보" 팝업 + 팩션 아이콘 표시

  유효 시간: 레이드 종료 또는 팩션 괴멸 시까지 유효
  우선순위: High
```

---

### 메시지 충돌 규칙

```
충돌 시나리오 1: EnemyDetected + ResourceDiscovered 동시 수신
  처리 순서: EnemyDetected(High) 먼저 처리
  EnemyDetected → P1 DefendVillage 활성화 → ResourceDiscovered는 큐에 대기
  DefendVillage Goal 해소 후 Idle 복귀 시 ResourceDiscovered 처리

충돌 시나리오 2: OrderIssued + P0 Goal 동시 활성화
  처리: P0 Goal이 항상 우선
  OrderIssued는 P0 Action 완료 후 CommandConflict에서 처리
  단, P0 Action 완료 전에 OrderIssued가 만료되지 않도록 큐에 보존

충돌 시나리오 3: VillagerDied + ResourceDepleted 동시 수신
  처리 순서: VillagerDied(High) 먼저 처리
  VillagerDied로 nearDroppedItem 갱신 후 ResourceDepleted로 Replanning 처리

충돌 시나리오 4: 복수의 OrderIssued (같은 주민 대상)
  처리: 가장 최근 메시지 1개만 처리, 이전 메시지 폐기
  (플레이어가 명령을 빠르게 변경한 경우 최신 명령 우선)

충돌 시나리오 5: LOD_FSM 중 EnemyDetected 수신
  처리: LOD_Alert 즉시 진입 → VillagerFSM.Idle로 Full GOAP 복귀
  (LOD는 EnemyDetected를 구독, 지연 없이 즉시 처리)
```

---

## 4. AI 의사결정 우선순위 규칙

```
우선순위 계층 (높을수록 먼저 처리):

P0-절대 (Absolute Override):
  - healthLevel < 20 → SurviveInjury 즉시 활성화
    → 어떤 명령도, 어떤 상태도 이를 막을 수 없음
    → Re-plan 쿨다운 무시, 즉시 Planning 전환
    → Depth 3 이하 제한 (빠른 탐색)

P0-1 (P0 서브 1위):
  - SurviveInjury: healthLevel < 20
    대응 Action 체인: SeekMedicalAid (nearHealer=true 필요)
                     → 없으면 MoveToBase (치료사 찾기)
                     → RestOnGround (최후 수단)

P0-2 (P0 서브 2위):
  - SurviveHunger: hungerLevel > 80
    P0-1과 동시 발동 시: P0-1 우선, P0-2는 P0-1 해소 후 처리
    대응 Action: EatCookedFood (최우선) → EatRawFood → HarvestWildBerries → CookMeal

P0-3 (P0 서브 3위):
  - SurviveFatigue: fatigueLevel > 90
    P0-1, P0-2와 동시 발동 시 최후 순위
    단, hasFood == true → EatStoredFood 후 Sleep 조합으로 처리 (TechSpec EX-003)
    대응 Action: Sleep (nearBed=true) → RestOnGround

P1 (매우 높음):
  - DefendVillage: enemyNearby == true
    - hasWeapon == true AND healthLevel > 40 → AttackEnemy
    - hasPrimitiveWeapon == true AND enemyTier == 1 → AttackEnemyWeak
    - healthLevel < 40 → FleeFromEnemy
    - hasWeapon == false → CraftPrimitiveWeapon → (재평가)
    - AlertVillage (Watchtower 근처일 때 우선 실행)

P2 (높음):
  - ExecutePlayerOrder:
    ConflictScore < 임계값 AND loyaltyLevel 조건 충족
    loyalty 구간별 Cost Modifier 적용:
      70~100 → ×0.7 (적극 수행)
      50~69  → ×1.0 (보통)
      30~49  → ×2.5 (소극적)
      0~29   → ×6.0 (거의 수행 불가 수준)

P3 (보통):
  - BuildStructure:
    buildingQueued == true
    AND 자원 가용량(stock - reserved) >= 건설 비용
    AND hasTool == true (또는 PickUpTool 선행 가능)
    다중 주민: BuildingQueue 슬롯 < 3명이면 참여

P4 (낮음):
  - GatherResources:
    anyAvailableStock < 30 (rawFood + cookedFood 합산 포함)
    자원 예약 후 즉시 수집 시작
    역할별 특화 자원 우선 (Lumberjack → Wood, Miner → Stone/Iron/Copper)

P5 (기본):
  - Explore:
    allAvailableStocks >= 50 AND unexploredTilesNearby == true
    → Explore Action → ResourceDiscovered 메시지 발행
    Explore 완료 후 isDiscovered 노드 증가 → GatherResources P4 재평가

Fallback (최하위):
  - NoSolutionFound 3회 연속:
    RestOnGround 또는 MoveToBase
    "도움 필요" UI 아이콘 표시
```

---

### 충돌 해소 규칙 (동일 조건 충족 시)

```
규칙 1: P0 3개 동시 발동
  → 서브 우선순위 고정 적용: SurviveInjury > SurviveHunger > SurviveFatigue
  → 단, 상위 P0 해소 후 하위 P0 즉시 평가 (연속 처리)

규칙 2: P3 BuildStructure vs P4 GatherResources 동시 충족
  → P3 우선. 단, 자원이 건설 + 생존 모두에 부족하면 P4 우선
  (생존 임계값: rawFood + cookedFood < 주민 수 × 3)

규칙 3: 같은 우선순위 Goal이 여러 주민에게 동시 활성화
  → ResourceRegistry를 통한 선착순 예약으로 자동 조율
  → 예약 실패한 주민은 다음 우선순위 Goal로 자동 전환

규칙 4: 계절 보정과 Goal 우선순위
  → 겨울: GatherResources P4에 rawFood 수집 가중치 자동 적용 없음
          (가중치는 Action Cost에 반영, Goal 우선순위 자체는 변경 없음)
  → 가을: rawFood 수집 가중치 ×3 → GatherResources Action Cost 감소
          → GOAP 플래너가 자연스럽게 rawFood 수집 선택
```

---

## 5. 명령 거부 로직 명세

### 거부 케이스 1: 생존 필요 충돌 (배고픔)

```
[REFUSE_HUNGER]
  명령 종류: 모든 플레이어 명령 (특히 자원 수집, 건설, 전투)
  거부 조건:
    hungerLevel > 80
    AND ConflictScore >= (2.5 × loyaltyLevel / 50)
    AND Order_impact[hunger] >= 0.5
  거부 이유 코드: REFUSE_HUNGER
  UI 메시지: "너무 배고파요. 밥 먹고 바로 할게요!"
  대안 행동: SurviveHunger Goal 활성화
    → EatCookedFood (cookedFoodStock >= 1, atBase)
    → EatRawFood (rawFoodStock >= 1)
    → HarvestWildBerries → CookMeal (요리사인 경우)
  재수행 조건: hungerLevel <= 60 (안정권 복귀 후)
```

### 거부 케이스 2: 생존 필요 충돌 (부상)

```
[REFUSE_INJURY]
  명령 종류: 모든 플레이어 명령 (특히 전투, 이동, 건설)
  거부 조건:
    healthLevel < 20
    AND ConflictScore >= 임계값
  거부 이유 코드: REFUSE_INJURY
  UI 메시지: "부상이 심해요. 치료 후 하겠습니다."
  대안 행동: SurviveInjury Goal 활성화
    → SeekMedicalAid (nearHealer=true)
    → MoveToBase (치료사 탐색)
    → RestOnGround (최후 수단)
  재수행 조건: healthLevel >= 40 (전투 가능 체력 복귀)
```

### 거부 케이스 3: 생존 필요 충돌 (피로)

```
[REFUSE_FATIGUE]
  명령 종류: 고강도 작업 명령 (건설, 채광, 전투)
  거부 조건:
    fatigueLevel > 90
    AND ConflictScore >= 임계값
  거부 이유 코드: REFUSE_FATIGUE
  UI 메시지: "지쳐서 쓰러질 것 같아요..."
  대안 행동: SurviveFatigue Goal 활성화
    → Sleep (nearBed=true, enemyNearby=false)
    → RestOnGround (긴급)
  재수행 조건: fatigueLevel <= 50
```

### 거부 케이스 4: 낮은 충성도

```
[REFUSE_LOYALTY]
  명령 종류: 모든 플레이어 명령
  거부 조건:
    loyaltyLevel < 30
    AND ConflictScore >= (2.5 × loyaltyLevel / 50)
    (임계값이 매우 낮아 사소한 충돌에도 거부)
  거부 이유 코드: REFUSE_LOYALTY
  UI 메시지: "왜 제가 그걸 해야 하죠?"
  대안 행동: P4~P5 자율 수행 (GatherResources 또는 Explore)
  재수행 조건: loyaltyLevel >= 50 (식량 선물, 주거 개선 등으로 회복 후)
```

### 거부 케이스 5: 무장 없이 위험 지역

```
[REFUSE_DANGER]
  명령 종류: 전투 명령, 적 영역 이동 명령
  거부 조건:
    nearEnemy == true OR 명령 목표 타일이 적 영역
    AND hasWeapon == false AND hasPrimitiveWeapon == false
  거부 이유 코드: REFUSE_DANGER
  UI 메시지: "무장 없이는 너무 위험해요!"
  대안 행동:
    wood >= 3 AND stone >= 2 → CraftPrimitiveWeapon
    자원 부족 → FleeFromEnemy (또는 MoveToBase)
  재수행 조건: hasWeapon == true OR hasPrimitiveWeapon == true
```

### 거부 케이스 6: 정비 미완료 (도구 없음)

```
[REFUSE_NO_TOOL]
  명령 종류: ChopWood, MineStone, MineIron, MineCopper 명령
  거부 조건:
    hasTool == false
    AND nearStorage == false (도구 획득 경로 없음)
    AND nearDroppedItem == false (주운 도구 없음)
  거부 이유 코드: REFUSE_NO_TOOL
  UI 메시지: "도구가 없어요. 먼저 도구를 구해야 해요."
  대안 행동:
    nearStorage == true → PickUpTool → 명령 재수행
    nearDroppedItem == true (적합한 도구) → PickUpDroppedItem → 명령 재수행
    둘 다 없으면 → Idle (자율 판단)
  재수행 조건: hasTool == true
```

### 거부 케이스 7: 건설 자원 미충족

```
[REFUSE_INSUFFICIENT_RESOURCES]
  명령 종류: BuildStructure 명령
  거부 조건:
    가용량(stock - reserved) < 건설 요구 비용 (1종 이상)
  거부 이유 코드: REFUSE_INSUFFICIENT_RESOURCES
  UI 메시지: "자원이 부족해요. [부족 자원명]이 더 필요해요."
  대안 행동: GatherResources Goal 활성화 (부족 자원 수집)
  재수행 조건: 모든 건설 자원의 가용량 >= 요구 비용
```

---

## 6. 팩션 GOAP AI 의사결정 구조

### 팩션 GOAP 설계 원칙

팩션 AI는 주민 GOAP와 동일한 Goal-Action-WorldState 구조를 공유하되,
개별 유닛보다 "팩션 단위"로 Goal을 결정하고 유닛에 분배하는 2레이어 구조다.

```
[팩션 레이어 (Faction-Level GOAP)]:
  팩션 전체 목표 결정 (RaidDecision, TradeProposal, Scouting 등)
  → 팩션 유닛들에게 개별 Goal 할당

[유닛 레이어 (Unit-Level GOAP)]:
  할당된 Goal에 따라 개별 A* 플래닝 및 실행
  (주민 GOAP FSM 래퍼와 동일한 구조 사용)
```

---

### 팩션 공통 Goal 목록

| 우선순위 | Goal | 발동 조건 |
|---|---|---|
| F-P0 | SurviveFaction | factionStrength <= 자신의 초기 factionStrength × 0.3 |
| F-P1 | DefendBase | playerStrength > factionStrength × 1.2 AND nearPlayerTerritory |
| F-P2 | ExpandTerritory (레이드) | RaidDecision = true |
| F-P3 | Scout | 정찰 시작일 도달 AND playerStrength 미파악 |
| F-P4 | GatherFactionResources | 자원 임계값 미달 AND 레이드 미결정 |
| F-P5 | TradeProposal | 상인 연합 전용, 외교 관계 >= 40 |

---

### 팩션별 침략 결정 로직

#### 공통 침략 트리거

```
[침략 결정 조건 — 매 팩션 Tick (5초 간격) 평가]:

  Step 1: 자원 결핍 체크
    copperStock < 10 OR silverStock < 5
    → 자원 결핍 플래그 = true

  Step 2: 지리적 조건 체크
    nearPlayerTerritory == true
    (팩션 유닛이 플레이어 영역 25타일 이내에 있음)

  Step 3: 전력 비교
    playerStrength = (주민 수 × 10) + (전사 역할 주민 수 × 15)
                   + (무기 보유 주민 수 × 8)
                   + (Watchtower 완성 × 20)
                   + (Forge 완성 × 15)

    factionStrength:
      숲의 부족: (팩션 유닛 수 × 10) + 25
      철의 도시: (팩션 유닛 수 × 12) + 35
      상인 연합: (팩션 유닛 수 ×  8) + 20

    playerStrength < factionStrength × 0.8 → 전력 우세 플래그 = true

  Step 4: 레이드 결정
    자원 결핍 플래그 AND 지리적 조건 AND 전력 우세 플래그
    → RaidDecision = true
    → ExpandTerritory Goal 활성화
    → RaidDecision 메시지 발행

  Step 5: 전력 열세 시 대안
    playerStrength >= factionStrength × 0.8
    → AllianceProposal (동맹 제안) 또는 TradeProposal (교역 제안) 우선 시도
    → 관계 < 30이면 대기 (재자원 수집 후 재시도)
```

---

### AssessPlayerStrength 메커니즘

```
[팩션이 playerStrength를 파악하는 방법]:

  Day 0 ~ 정찰 시작 전: 추정값만 사용 (초기값 30 고정)
  정찰 단계 이후: 정찰 유닛이 플레이어 영역 방문 시 실제 값 갱신

  정찰 유닛 동작:
    1. 플레이어 영역 25타일 이내 이동
    2. 가시 범위 내 주민 수, 건물 종류, 무기 보유 유닛 수 카운팅
    3. playerStrength 공식 적용 → 팩션 GOAP WorldState 갱신
    4. 정찰 완료 메시지 팩션 레이어로 전달 (MessageBus를 통해)

  정찰 주기:
    정찰 시작일 이후: 5 game day마다 재정찰
    레이드 결정 후: 정찰 중단 (전투 모드)
```

---

### 팩션별 특수 규칙

```
[숲의 부족]:
  성향: 방어적, 식량 부족 시 침략
  활성화: Day 5 정찰 시작, Day 10+ 레이드 가능
  특수 규칙:
    - 식량(rawFood + cookedFood) 합산 < 30 → 자원 결핍 트리거 추가 발동
    - 관계 50 이상이면 레이드 대신 rawFood 요청 이벤트 우선
    - 숲 타일 이동 비용 -50% (특화 지형)

[철의 도시]:
  성향: 공격적, 철광석 최우선
  활성화: Day 7 정찰 시작, Day 12+ 레이드 가능
  특수 규칙:
    - ironStock < 15 → 자원 결핍 즉시 트리거 (구리/은 조건 외 추가)
    - 항상 다른 팩션보다 먼저 공격 결정 (같은 조건이면 철의 도시 우선)
    - 전사 유닛 Cost -40%, 건설 유닛 부재

[상인 연합]:
  성향: 교역 우선, 전투 최후 수단
  활성화: Day 10 교역 제안 우선, Day 15+ 레이드 가능
  특수 규칙:
    - RaidDecision 이전에 반드시 TradeProposal 1회 이상 시도
    - TradeProposal 거절 3회 → 레이드 결정 가능
    - 관계 40 이상이면 레이드 결정 불가 (교역으로만 해결)
    - commTowerBuilt == true → 플레이어와 교역 협상 이벤트 트리거
```

---

### 팩션 활성화 타임라인

```
Day 1~4:    전 팩션 완전 비활성 (플레이어 영역 접근 불가)
            내부: 팩션 초기 자원으로 GatherFactionResources 실행

Day 5:      숲의 부족 — 정찰 유닛 파견 (Scout Goal 활성화)
Day 7:      철의 도시 — 정찰 유닛 파견
Day 10:     상인 연합 — 정찰 유닛 파견 + TradeProposal 이벤트 (최초 1회)
Day 10+:    숲의 부족 — 침략 트리거 조건 충족 시 첫 레이드 가능
Day 12+:    철의 도시 — 침략 트리거 조건 충족 시 첫 레이드 가능
Day 15+:    상인 연합 — TradeProposal 3회 거절 후 레이드 가능
```

---

## 7. 인터페이스 및 데이터 구조 명세

### 핵심 인터페이스

```csharp
// 자율 에이전트 공통 인터페이스
interface IAutonomousAgent
{
    string AgentId { get; }
    VillagerState CurrentState { get; }     // FSM 현재 상태
    float LoyaltyLevel { get; }             // 충성도 (명령 거부 계산에 사용)
    int OriginalFactionId { get; }          // 출신 팩션 기록 (향후 반란 시스템 대비)
    float HealthLevel { get; }
    float HungerLevel { get; }
    float FatigueLevel { get; }
    bool IsAlive { get; }

    void ReceiveMessage(AIMessage message);
    bool TryExecuteOrder(PlayerOrder order);  // false = 거부
    void ForceTransitionTo(VillagerState state);  // AnyState 전이용 (P0, Dead)
}

// 주민 FSM 상태 열거형
enum VillagerState
{
    Idle,
    Planning,
    Executing,
    Replanning,
    CommandConflict,
    RefusingOrder,
    Dead,
    LOD_FSM
}

// LOD 내부 상태 열거형
enum LODState
{
    LOD_Idle,
    LOD_GatheringResource,
    LOD_MovingToBase,
    LOD_Alert
}

// 플레이어 명령 구조체
struct PlayerOrder
{
    public string TargetVillagerId;
    public OrderType OrderType;
    public int TargetTileX;
    public int TargetTileY;
    public string BuildingTypeId;
    public float IssuedAt;
}

// AI 메시지 구조체 (MessageBus 통신용)
struct AIMessage
{
    public MessageType Type;           // VillagerDied | EnemyDetected | ...
    public MessagePriority Priority;   // High | Medium | Low
    public string SenderId;
    public object Payload;             // 타입별 페이로드 (boxing 최소화 설계 권장)
    public float IssuedAt;
}

enum MessagePriority { High = 0, Medium = 1, Low = 2 }
enum MessageType
{
    VillagerDied,
    EnemyDetected,
    ResourceDiscovered,
    ResourceDepleted,
    OrderIssued,
    OrderRefused,
    RaidDecision
}
```

---

### 핵심 데이터 구조

```csharp
// GOAP 플래너 핵심 데이터
struct GOAPPlanRequest
{
    public string AgentId;
    public string GoalId;
    public int MaxDepth;               // 상한: 6
    public NativeArray<int> WorldStateSnapshot;  // Blittable (Job System 호환)
    public AgentRole AgentRole;
    public float PlanningStartTime;
}

struct GOAPPlanResult
{
    public string AgentId;
    public bool Success;
    public NativeList<ActionId> ActionSequence;
    public float TotalEstimatedCost;
    public int SearchDepth;
    public PlanResultType ResultType;  // Success | NoSolutionFound | Timeout
}

enum PlanResultType { Success, NoSolutionFound, Timeout, Deadlock }

// 자원 예약 레지스트리 (메인 스레드 전용)
class ResourceRegistry
{
    // 가용량 = stock - reserved
    public float GetAvailable(ResourceType type);
    public bool Reserve(string agentId, ResourceType type, float amount);   // false = 가용량 부족
    public void Release(string agentId, ResourceType type, float amount);
    public void ReleaseAll(string agentId);  // 사망 시 사용
    public void Commit(string agentId, ResourceType type, float amount);    // stock 실제 차감
}

// ConflictScore 계산 데이터
struct ConflictScoreData
{
    public float HungerUrgency;    // (hungerLevel - 80) / 20, hungerLevel > 80일 때
    public float HealthUrgency;    // (20 - healthLevel) / 20, healthLevel < 20일 때
    public float FatigueUrgency;   // (fatigueLevel - 90) / 10, fatigueLevel > 90일 때
    public float SafetyUrgency;    // 0.8 고정, nearEnemy == true일 때
    public float OrderImpact;      // 명령별 계산
    public float ConflictScore;    // Σ(urgency × impact)
    public float Threshold;        // 2.5 × (loyaltyLevel / 50)
    public bool ShouldRefuse;      // ConflictScore >= Threshold
}

// 팩션 GOAP WorldState
struct FactionWorldState
{
    public string FactionId;
    public float CopperStock;
    public float SilverStock;
    public float IronStock;
    public float RawFoodStock;
    public float CookedFoodStock;
    public int UnitCount;
    public float PlayerStrengthEstimate;  // 정찰로 업데이트
    public bool NearPlayerTerritory;
    public bool RaidDecision;
    public int DayCount;
    public FactionRelationState RelationWithPlayer;
    public int TradeProposalRejectedCount;  // 상인 연합 전용
}
```

---

### DangerRegistry 구조

```csharp
// 위험 정보 공유 레지스트리 (MessageBus 보완, 지속 정보 저장)
class DangerRegistry
{
    // 위험 정보 등록
    public void RegisterDanger(DangerInfo info);

    // 특정 타일 반경 내 위험 정보 조회
    public List<DangerInfo> QueryDangers(int centerTileX, int centerTileY, int radius);

    // 만료된 위험 정보 제거 (매 게임 틱 자동 실행)
    public void PurgeExpired(float currentGameTime);
}

struct DangerInfo
{
    public string DangerId;
    public DangerType Type;          // Enemy | NaturalDisaster | TerritoryConflict
    public int TileX;
    public int TileY;
    public int ThreatTier;           // 1~4
    public string SourceFactionId;
    public float RegisteredAt;       // 게임 시간
    public float ExpiresAt;          // 적 이탈 시 또는 고정 만료 시간
}

// 위험 만료 규칙:
// Enemy 유형: 적 유닛이 영역 이탈 시 즉시 만료 OR 등록 후 30초 경과
// NaturalDisaster: 이벤트 종료 시 만료
// TerritoryConflict: RaidDecision 취소 시 만료
```

---

## 8. 설계 리스크 및 엣지케이스 경고

### 리스크 1: P0 Goal Thrashing (교착 상태)

```
[P0_GOAL_THRASHING]
  시나리오:
    hungerLevel = 81 AND healthLevel = 19 AND fatigueLevel = 91
    → P0-1, P0-2, P0-3 동시 발동
    → 해결 자원 없음 (cookedFoodStock = 0, nearHealer = false, nearBed = false)
    → 매 Tick SurviveInjury 시도 → NoSolutionFound → SurviveHunger 시도 → NoSolutionFound → ...

  영향: AI가 아무 행동도 실행하지 못하고 Replanning 루프에 빠짐. 주민 사망 가속.

  방어 설계:
    - P0 복합 위기 시 서브 우선순위 고정 (P0-1 우선)
    - NoSolutionFound 시 EX-001 Fallback 즉시 적용:
      hunger < 50 → RestOnGround, 그 외 → MoveToBase (안전 지대 이동)
    - 복합 P0 상태를 UI에 다색 아이콘으로 표시 (플레이어 개입 유도)
    - 3회 연속 Deadlock → "도움 필요" 플래그 (플레이어가 자원 투입 가능)
```

### 리스크 2: 자원 예약 누수 (사망/중단 시)

```
[RESERVATION_LEAK]
  시나리오:
    주민 A가 woodReserved += 20 후 건설 시작
    주민 A 전투 중 사망 → Dead 상태 진입
    Dead 상태에서 ReleaseAll() 미호출 → woodReserved 영구 잠금

  영향: 가용 자원이 실제보다 적게 표시 → 건설/수집 목표 달성 불가.

  방어 설계:
    - Dead 상태 진입 시 ReleaseAll(villagerId) 반드시 즉시 호출 (순서 1순위)
    - VillagerDied 메시지를 ResourceRegistry가 구독 → 이중 안전장치
    - 매 30초마다 ResourceRegistry 전체 무결성 검증:
      Σ(reserved by living agents) == Σ(all reserved) 불일치 시 경고 로그
```

### 리스크 3: 건설 다중 주민 슬롯 경쟁

```
[BUILD_SLOT_RACE_CONDITION]
  시나리오:
    주민 A, B, C, D 4명이 동시에 같은 BuildingQueue 항목에 참여 시도
    → assignedVillagerIds에 4명 모두 추가되면 슬롯(3명) 초과

  영향: 건설 속도 계산 오류 (×2.0 상한 초과 등).

  방어 설계:
    - BuildStructure Goal Planning 시: assignedVillagerIds.Count >= 3이면
      즉시 다른 BuildingQueue 항목으로 Re-plan (Executing 전 확인)
    - 슬롯 점유는 Executing 진입 시 메인 스레드에서 원자적 처리
    - 슬롯 초과 시 추가 주민은 대기 없이 즉시 다른 Goal로 전환
```

### 리스크 4: LOD → Full GOAP 복귀 시 상태 불일치

```
[LOD_STATE_DESYNC]
  시나리오:
    LOD 상태에서 5초 간격으로 자원 수집 시뮬레이션
    LOD_GatheringResource 완료 직전 Full GOAP 복귀
    → LOD가 stock += X 처리했는지 여부가 불분명
    → Full GOAP 플래닝 시 잘못된 stock 수치 기준으로 플래닝

  영향: 자원이 실제보다 많다고 판단 → 건설 시도 → Executing 중 자원 부족으로 Replanning 반복.

  방어 설계:
    - LOD 자원 수집은 LOD_MovingToBase 도달 후에만 stock 갱신 (중간 처리 없음)
    - LOD → Idle 전환 시 ResourceRegistry.Release() 전체 호출 후 상태 초기화
    - Executing 진입 시 Precondition 재검증이 이 문제를 최종 방어
```

### 리스크 5: 팩션 PlayerStrength 정보 지연

```
[FACTION_STRENGTH_INFO_DELAY]
  시나리오:
    Day 12: 철의 도시가 Day 7 정찰 정보 기준으로 playerStrength = 40 추정
    Day 11에 플레이어가 Watchtower, Forge 완성 → 실제 playerStrength = 75
    Day 12 레이드 결정: 40 < factionStrength × 0.8 → RaidDecision = true
    실제 전투: 플레이어 우세 → 팩션 패배

  영향: 팩션 AI가 전력 과소평가로 무모한 레이드 → 팩션 빠른 괴멸 → 긴장감 저하.

  방어 설계:
    - 정찰 주기: 5 game day마다 재정찰 (최신 정보 갱신)
    - Watchtower/Forge 완성 이벤트를 팩션이 정찰 시 감지 가능하도록 설계
    - 재정찰 없이 30일 이상 경과 시 playerStrength 불확실성 보정 × 1.3 적용
      (과소평가 방지 — 보수적 판단)
```

### 리스크 6: MessageBus High 우선순위 메시지 폭주

```
[MESSAGE_BUS_HIGH_FLOOD]
  시나리오:
    레이드 발생 → 10명 주민 모두 EnemyDetected 발행 (모두 High 우선순위)
    같은 Tick에 10개 High 메시지 처리 → 10개 Replanning 동시 발생

  영향: 한 Tick에 과도한 처리 부하 → 프레임 드롭.

  방어 설계:
    - EnemyDetected 중복 발행 방지: enemyNearby == true이면 이미 발행된 것으로 간주
    - MessageBus에서 같은 타입의 중복 메시지는 1개로 병합 (같은 Tick 내)
    - Replanning은 replanCooldown(0.3~0.5초)이 적용되어 동시 폭주 자동 분산
```

---

## 9. unity-senior-programmer 전달 체크리스트

```
이 설계서로 코딩을 시작하기 전 확인:

FSM 완전성:
  [v] 모든 FSM 상태의 진입/탈출 조건이 정의됨 (8개 상태, 4개 LOD 상태)
  [v] 모든 상태에 탈출 경로 존재 (Dead만 최종 흡수 상태로 의도적 예외)
  [v] AnyState → Dead 폴백 전이 정의됨 (isAlive == false)
  [v] AnyState → Planning P0 즉시 전이 정의됨

MessageBus 완전성:
  [v] 모든 메시지 타입의 발행자/구독자가 명시됨 (7개 타입)
  [v] 메시지 충돌 규칙 5가지 정의됨
  [v] MessageBus 직접 통신 금지 규칙 명시됨

의사결정 완전성:
  [v] 의사결정 우선순위 계층 완전히 정의됨 (P0-절대 ~ Fallback)
  [v] P0 서브 우선순위 확정: SurviveInjury > SurviveHunger > SurviveFatigue
  [v] 동시 충족 충돌 해소 규칙 4가지 정의됨

명령 거부 완전성:
  [v] 명령 거부 케이스 7가지 모두 열거됨
  [v] "정비 미완료(도구 없음)" 케이스 포함 (REFUSE_NO_TOOL)
  [v] "체력 미충족" 케이스 포함 (REFUSE_INJURY)
  [v] ConflictScore 계산 공식 상세 정의됨

인터페이스 완전성:
  [v] IAutonomousAgent에 loyalty, originalFactionId 필드 포함됨
  [v] ResourceRegistry 인터페이스 (Reserve/Release/Commit/ReleaseAll) 정의됨
  [v] DangerRegistry 인터페이스 및 만료 규칙 정의됨
  [v] GOAPPlanRequest/Result 구조체 정의됨 (Job System NativeArray 포함)

팩션 AI 완전성:
  [v] 3개 팩션의 침략 결정 로직 완전 정의됨
  [v] AssessPlayerStrength 공식 및 정찰 메커니즘 정의됨
  [v] 팩션별 활성화 타임라인 (Day별) 정의됨
  [v] 팩션 특수 규칙 (숲의 부족/철의 도시/상인 연합) 정의됨

LOD AI 완전성:
  [v] LOD 진입 조건 (30타일 + 비전투) 정의됨
  [v] LOD → Full GOAP 복귀 조건 (전투 or 30타일 이내) 정의됨
  [v] LOD Tick 빈도 감소 (0.5초) 정의됨

성능 설계:
  [v] GOAP 연산: Job System 오프로드 (NativeArray 기반)
  [v] P0: 즉시 처리, 나머지: 다음 Tick 큐잉
  [v] Tick 분산: 6그룹, 0.1초 간격
  [v] Re-plan 쿨다운: 0.3~0.5초
  [v] 탐색 깊이 상한: Depth 6
  [v] LOD 전환으로 실질 Full GOAP 유닛 60~70 이하 유지

데이터 일관성:
  [v] WorldState 2레이어 (플래닝 스냅샷 + Authoritative State) 명시
  [v] 자원 예약 시스템: 가용량 = stock - reserved
  [v] Authoritative State 쓰기는 메인 스레드 전용
  [v] stock 음수 방지: Mathf.Max(0, stock) 하드 클램프

ScriptableObject 분리:
  [v] VillagerRecruitData (역할별 모집 비용 + 속성 범위)
  [v] FactionInitialState (팩션 초기 자원, 유닛 수, 활성화 타이밍)
  [v] ResourceNodeData (재생량, 주기, 최대 용량)
  [v] SeasonData (계절별 보정 수치)
  [v] BuildingData (건설 비용, 다중 주민 슬롯 수)
```

---

## 자기 검증 체크리스트 완료 확인

```
[v] FSM에 DeadState(막힌 상태)가 없는가?
    → Dead만 최종 흡수 상태 (의도적). 나머지 모든 상태에 탈출 경로 존재.

[v] 두 AI가 동시에 같은 행동을 하면 충돌이 발생하는 케이스를 다루었는가?
    → ResourceRegistry 선착순 예약으로 자원 경쟁 해소 (EX-005)
    → BuildingQueue 슬롯 3명 상한 + Executing 진입 시 원자적 처리
    → 동일 자원 노드 경합: harvestingVillagerId 점유권 시스템 (EX-004)

[v] 생존 최우선 원칙이 모든 P0 우선순위에 반영되었는가?
    → P0-절대: 쿨다운 무시, AnyState 즉시 전환
    → P0-1(SurviveInjury) > P0-2(SurviveHunger) > P0-3(SurviveFatigue) 고정

[v] 플레이어 명령 거부 조건이 "정비 미완료"와 "체력 미충족" 두 가지를 모두 포함하는가?
    → REFUSE_NO_TOOL (정비 미완료 — 도구 없음)
    → REFUSE_INJURY (체력 미충족 — healthLevel < 20)
    → 총 7가지 거부 케이스 정의

[v] loyalty와 originalFactionId가 설계 어딘가에 포함되었는가?
    → IAutonomousAgent 인터페이스에 LoyaltyLevel, OriginalFactionId 포함
    → ConflictScore 임계값 계산에 loyaltyLevel 사용
    → Cost Modifier 4구간 정의 (70~100, 50~69, 30~49, 0~29)

[v] unity-senior-programmer가 이 문서만 보고 구현을 시작할 수 있는가?
    → 모든 상태 8개 + LOD 4개 완전 정의
    → 모든 메시지 타입 7개 + 충돌 규칙 5가지 정의
    → C# 인터페이스 및 데이터 구조 의사 코드 제공
    → 리스크 6개 + 방어 설계 제공
    → 구현 전 체크리스트 제공
```

---

## 참고: 겨울 연료 처리와 GOAP 연동

```
겨울 시즌 자동 적용 (별도 명령 없음):
  매 game day: woodStock -= 1.0 × 주민 수 (메인 스레드 자동 처리)
  Campfire/House 완성 시: 소비량 × 0.8

GOAP 연동:
  woodStock 감소 → GatherResources P4 발동 조건(anyAvailableStock < 30) 도달 가능
  → 주민 자율적으로 ChopWood 우선 수행
  woodStock = 0 → fatigueLevel 증가율 +20%/일 → SurviveFatigue P0-3 더 빠르게 발동
  → 연료 부족이 P0 압력으로 자동 연결

계절별 Action Cost 보정 (SeasonData ScriptableObject):
  봄:  HarvestWildBerries Cost × 0.83  (+20% 효율)
  여름: HarvestWildBerries Cost × 0.77  (+30% 효율)
  가을: HarvestWildBerries Cost × 0.91  (+10% 효율), rawFood 수집 가중치 ×3
        (가을 가중치 ×3은 rawFood 관련 Action Cost를 1/3로 감소시키는 방식으로 구현)
  겨울: HarvestWildBerries Cost × 2.0   (-50% 효율)
```

---

## 참고: 주민 모집과 GOAP 즉시 연동

```
Town Hall 완성 → townHallBuilt = true → 모집 UI 해금 이벤트

신규 주민 생성 시 즉시:
  1. VillagerRecruitData ScriptableObject에서 초기 속성 랜덤 생성
  2. isAlive = true, VillagerFSM 초기 상태 = Idle
  3. Tick 그룹 배정 (6그룹 중 가장 유닛 수 적은 그룹에 배정)
  4. 다음 Tick에 Idle → Planning 전환 (P4 GatherResources 또는 역할별 Goal)
  5. ResourceRegistry에 새 에이전트 ID 등록

모집 비용 차감 순서:
  cookedFoodStock -= 모집 비용 (메인 스레드)
  추가 자원 차감 (Iron, Copper)
  → ResourceRegistry.Commit() 호출 (예약 없이 직접 차감 — 플레이어 액션)
```

---

*설계 명세서 v1.0 완성 — 2026-06-25*
*설계 에이전트: unity-ai-behavior-architect*
*기반 문서: GDD v0.4, TechSpec v1.0*

---

다음 단계: unity-senior-programmer에게 전달 권장

추가 검토 권장:
  - unity-performance-optimizer: LOD 전환 임계값(30타일) 및 Tick 분산 그룹 설정 성능 검증
  - game-qa-exploiter: ConflictScore 계산식 어뷰징 가능성 점검 (특히 loyalty < 30 구간)
  - unity-code-reviewer: 구현 완료 후 IAutonomousAgent 인터페이스 준수 여부 검증
