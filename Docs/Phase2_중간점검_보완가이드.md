# Phase 2 중간점검 보완 가이드 (W1~W3 리뷰)

> **대상 커밋**: `6fd6e08` "Implement Phase 2 W1-W3"
> **판정 요약**: W1 ✅ 100% · W2 ✅ 100% · W3 ⚠️ 80% — 구조는 정확하나 **게임 진행 차단 버그 1건(P1)**과 수치 정합 위반 3건(P2) 존재.
> W3 완료 직후 중간 리뷰를 권장했던 이유가 그대로 적중했다 — 오류는 전부 데이터 계층에 있다.
>
> **작성일**: 2026-07-05

---

## 0. 리뷰 총평

### 잘 된 것 (수정 불필요 — 그대로 유지)

| 항목 | 확인 내용 |
|---|---|
| W1 슬롯 52 | 기존 인덱스 무변경, 가용량(총량−예약량) 미러 — ⚠️ 오해 위험까지 정확히 회피 |
| W2 연산자 | PrecHolds/ApplyEff가 Job의 unroll **과** 구조체 헬퍼 CheckPreconditions **양쪽** 모두 적용 (명세의 "두 군데 다" 준수) |
| W2 GoalOp | 시그니처 확장, Scheduler 배선, Dispose 3경로 전부 포함 — leak 없음 |
| ADR-8 토글 | `UseNumericGoals` 정적 플래그, 롤백 경로 확보 |
| W3 건설 비용 | BuildingCosts.cs와 전 항목 일치 (35/30/6, 20/20/15, 15/5, 5) — ADR-7 모범 사례 |
| 검증 툴링 | F1/F5/F7 검증용 ContextMenu — 명세에 없던 자발적 개선. **P1~P4 수정 후에도 이 패턴으로 검증 메뉴를 추가할 것을 권장** |

### 수정 항목 총괄

| # | 항목 | 심각도 | 작업량 |
|---|---|---|---|
| P1 | GatherResources 수치 Goal의 -1 무한 루프 | 🔴 **게임 진행 차단** | 중 (~1시간) |
| P2 | 플래너-런타임 수치 불일치 3건 (ADR-7 위반) | 🔴 높음 | 소 (~20분) |
| P3 | MAX_NODES 8192 임시 조치의 관리 | 🟠 중간 (성능 리스크) | 소 (문서화+측정) |
| P4 | 창고 건설의 이중 게이트 (기존 문제 노출) | 🟡 낮음 | 소 (~15분) |
| P5 | SeekMedicalAid 회복량 대조 미확인 | 🟡 확인 필요 | 소 (~10분) |

**권장 순서**: P2 → P1 → P5 → P4 → P3(측정). P2가 P1보다 먼저인 이유: P1 수정 후 테스트 플레이를 하게 되는데, 그 전에 수치가 맞아 있어야 테스트 결과를 신뢰할 수 있다.

---

## P1. GatherResources 수치 Goal — "-1 무한 루프" 🔴

### 문제 (게임이 실제로 멈추는 시나리오)

현재 상태의 조합:
1. `State_Idle()`은 **어떤 자원이든** 30 미만이면 `CurrentGoalId = "GatherResources"` (범용 Goal, 기존 코드 유지됨)
2. `BuildGoalState`의 수치형 `GatherResources`는 **WoodStock ≥ 30 하나로 하드코딩** (주석: "GoalArbiter(W8)가 분기 결정" — 그러나 W8은 아직 없다)

**재현**: 목재 50 / 석재 5인 마을.
- Idle → 석재 부족 감지 → Goal = GatherResources
- 플래너: "WoodStock ≥ 30? 이미 50이네" → **ResultLength = -1 (이미 달성)**
- FSM: 달성 처리 → Idle 복귀 → 석재 여전히 부족 → Goal = GatherResources → -1 → …

주민들이 **석재를 영원히 캐지 않으면서 바쁘게 아무것도 안 하는** 상태가 된다. 석재가 필요한 건설이 전부 정체 → 30일 생존 진행 불가. 초반에 목재만 먼저 차는 자연스러운 흐름에서 **거의 확정적으로 발생**한다.

### 원인

명세 W3-3의 "GatherResources는 **가장 부족한 자원 1종으로 위임** — Scheduler가 치환" 항목이 미구현된 채, 임시 하드코딩(Wood)이 들어갔다. W8을 기다리는 판단은 이해되지만, W8 이전에도 State_Idle이 이 Goal을 발동하므로 공백이 생겼다.

### 수정 설계 — FSM에서 구체 Goal을 선택 (W8 이전의 올바른 위치)

Scheduler 치환보다 FSM 선택이 낫다: 발화(F4) 대사도 자원별로 정확해지고, W8 GoalArbiter로 자연스럽게 교체될 자리이기 때문이다.

```csharp
// VillagerFSM.cs — State_Idle()의 P2b 블록 교체

// 변경 전:
//   if (_worldState != null && IsAnyStockLow()) { CurrentGoalId = "GatherResources"; ... }

// 변경 후:
if (_worldState != null)
{
    string gatherGoal = SelectGatherGoalId();   // 가장 부족한 자원의 구체 Goal
    if (gatherGoal != null)
    {
        bool goalChanged = Brain.CurrentGoalId != gatherGoal;
        Brain.CurrentGoalId = gatherGoal;
        if (goalChanged) ShowThoughtBubble(Pick(THOUGHT_GOAL_GATHER));
        TransitionTo(VillagerState.Planning);
        return;
    }
}
```

```csharp
/// <summary>[P1] 임계값(30) 미만 자원 중 가장 부족한 것의 구체 Goal ID를 반환한다.
/// 전부 충분하면 null. W8 GoalArbiter 도입 시 이 함수가 Arbiter 호출로 교체된다.</summary>
private string SelectGatherGoalId()
{
    const float T = 30f;   // 기존 GATHER 임계값 상수가 있으면 그것을 사용할 것
    string best = null; float worstRatio = 1f;

    void Consider(float stock, float threshold, string goalId)
    {
        if (stock >= threshold) return;
        float ratio = stock / threshold;            // 0에 가까울수록 급함
        if (ratio < worstRatio) { worstRatio = ratio; best = goalId; }
    }

    Consider(_worldState.WoodStock,     T,   "GatherWood");
    Consider(_worldState.StoneStock,    T,   "GatherStone");
    Consider(_worldState.IronStock,     15f, "GatherIron");    // 수치 Goal 목표치(15)와 일치시킬 것
    Consider(_worldState.CopperStock,   15f, "GatherCopper");
    Consider(_worldState.RawFoodStock,  T,   "GatherFood");    // ← 신규 Goal, 아래 참조
    return best;
}
```

**⚠️ 함께 반드시 할 것 — "GatherFood" Goal 신설**: 수치형 매핑에 식량 채집 Goal이 없다 (Wood/Stone/Iron/Copper뿐). 생식량이 부족해도 캘 Goal이 없으면 요리 체인(T3 시나리오)의 앞단이 끊긴다. `BuildGoalState`에 케이스 추가:

```csharp
case "GatherFood":
    if (useNumericGoals)
    {
        goalMask[GOAPPlanningSlots.RawFoodStock]  = 1;
        goalState[GOAPPlanningSlots.RawFoodStock] = 30;
        goalOps[GOAPPlanningSlots.RawFoodStock]   = 1; // GreaterEq
    }
    else
    {
        goalMask[GOAPPlanningSlots.ResourcesGathered]  = 1;
        goalState[GOAPPlanningSlots.ResourcesGathered] = 1;
    }
    break;
```

레거시 케이스의 `GatherResources`는 후방 호환용으로 남겨두되(명령 시스템 등 다른 호출처 존재 가능), **수치형 분기에서는 "가장 부족한 자원" 폴백**으로 바꿔 이중 안전망을 친다 — FSM을 우회해 이 goalId가 들어와도 -1 루프가 불가능하도록:

```csharp
case "GatherResources":
    if (useNumericGoals)
    {
        // [P1 안전망] 하드코딩(Wood) 금지 — 호출 시점의 최저 재고 자원으로 위임
        int slot = LowestStockSlot(/* currentState 접근이 없으므로 아래 참고 */);
        ...
    }
```

> 구현 노트: `BuildGoalState`는 현재 월드 상태를 모르는 순수 함수다. 안전망을 여기 넣으려면 시그니처에 재고 파라미터를 추가해야 하므로, **간단히 가려면 Scheduler.Schedule() 서두에서 goalId == "GatherResources" && UseNumericGoals일 때 구체 goalId로 치환하는 3줄**이 가장 침습이 적다. 둘 중 하나만 하면 된다 — FSM(주 방어)과 Scheduler(안전망) 중 최소 FSM은 필수.

### Unity 검증 절차 (Play Mode)
1. GameManager ContextMenu에 검증 메뉴 추가: "석재만 5로 설정 + 전체 리플래닝"
2. 기대: 주민들이 Goal=GatherStone으로 전환, MineStone 체인 실행, 석재가 30까지 상승
3. 회귀: 목재도 함께 부족하게 만들면 더 부족한 쪽부터 처리하는지 확인
4. **10분 방치 테스트**: 자원 4종이 모두 30 이상으로 수렴하고 Explore로 전환되는지 — 이것이 "게임이 잘 진행된다"의 조작적 정의다

---

## P2. 플래너-런타임 수치 불일치 (ADR-7 위반 3건) 🔴

### 문제

W3 상수가 명세서의 **기본 제안치**를 그대로 채택했는데, 명세의 지시는 반대였다: *"기존 런타임 값이 확인되는 경우 그것을 따를 것 — 플래너 수치를 새로 정하는 게 아니라 런타임에 이미 존재하는 수치를 플래너에 알려주는 작업."* 대조 결과:

| 상수 | 플래너 (신규) | 런타임 (기존) | 근거 위치 |
|---|---|---|---|
| EAT_HUNGER_RELIEF | 60 | **50** | ActionDatabase `ReduceHunger 50f` (기획서 수치 주석) |
| EAT_RAW_RELIEF | 35 | **15** | ActionDatabase `ReduceHunger 15f` |
| SLEEP_FATIGUE_RELIEF | 70 | **90** | VillagerFSM `SLEEP_FATIGUE_RECOVERY = 90f` |
| REST_FATIGUE_RELIEF | 20 | 20 ✅ | 일치 — 문제없음 |

### 실제 게임 영향 (왜 그냥 두면 안 되는가)

- **EatRawFood가 최악**: 배고픔 90 주민 — 플래너는 "생식 2회(−35×2)면 20, 달성"이라 계획하고 생식량 2를 소모 예정에 넣지만, 런타임은 90−15−15=**60**. 배는 여전히 고픈데 식량은 계획대로 2개가 사라졌다. 마을 식량이 계획 대비 빠르게 증발하고, 주민은 곧 다시 P0에 진입 — **식량 경제가 조용히 무너진다.**
- Sleep은 반대 방향(과소 계획): 플래너가 2회 수면을 계획할 상황에서 실제론 1회면 충분 — 시간 낭비 수준이라 덜 치명적이지만 여전히 괴리다.

### 수정 (5분)

상수 값을 런타임 실측치로 교체한다. **런타임 코드는 건드리지 않는다** — 그건 W7(런타임이 이 상수를 *참조*하게 만드는 작업)의 몫이고, 지금은 값만 일치시킨다.

```csharp
public const int EAT_HUNGER_RELIEF    = 50;  // ActionDatabase ReduceHunger와 일치 (기획서 수치)
public const int EAT_RAW_RELIEF       = 15;  // 〃
public const int SLEEP_FATIGUE_RELIEF = 90;  // VillagerFSM.SLEEP_FATIGUE_RECOVERY와 일치
```

**⚠️ 부수 확인**: EAT_RAW_RELIEF=15로 내리면, 배고픔 90→목표 30은 생식만으로 4회가 필요해진다(90−60=30). MAX_DEPTH=6 내에서 `HarvestBerries ×n + EatRaw ×4` 체인은 깊이를 초과할 수 있다 → 이 경우 플래너는 자연스럽게 **조리 경로(2 생식량 → 1 조리 → −50)를 선호**하게 되는데, 이것이 오히려 의도된 게임플레이("생식은 비상용")다. 다만 **모닥불이 없는 극초반**에 생식 체인마저 깊이 초과로 실패하지 않는지 Play Mode에서 확인할 것 — 실패한다면 W6(깊이 12)을 앞당기는 근거가 된다.

### Unity 검증 절차
1. ContextMenu "주민 1번 배고픔 90 + 조리식량 3 지급" 추가
2. 기대 플랜: `MoveToFireplace? → EatCookedFood ×2` (90−50−50=0 ≤ 30... 1회로는 40>30이므로 정확히 2회)
3. 실행 후 실측 HungerLevel이 플랜 종료 시점 예상과 일치하는지 로그 대조

---

## P3. MAX_NODES 8192 — 임시 조치의 관리 🟠

### 상황 판단

2048→8192 인상은 ADR("2048 유지")과 다르지만, **커밋의 진단은 정확하다**: 수치형 Goal + 기존 휴리스틱(h∈{0, 2.9} — 사실상 Dijkstra) 조합에서는 멀티스텝 경로를 찾기 전에 노드 예산이 소진된다. W4(새 휴리스틱)가 없는 현 시점에서는 필요악이 맞다. **되돌리지 말 것.** 다만 관리가 필요하다:

1. **메모리·시간 영향 인지**: NodeStates가 8192×52×4B ≈ 1.7MB/플래닝. 틱 그룹 분산으로 동시 플래닝 ~12건이면 순간 ~20MB + 워커 시간 최악 4배. 지금 규모에선 감당되지만 예산(1.8ms) 초과 여부를 **실측**해야 한다.
2. **측정 훅 추가** (W4/W6의 DoD 계측과 동일한 것이 지금 필요하다):

```csharp
// GOAPPlannerJob.cs — 출력 필드 추가
/// <summary>[P3 계측] 이번 탐색에서 확장한 노드 수. NodesExpanded[0]에 기록.</summary>
public NativeArray<int> NodesExpanded;

// Execute() 루프 내 nodeCount 증가부에서 함께 기록, 종료 시 NodesExpanded[0] = nodeCount;
// Scheduler Complete 경로에서 Debug.Log 조건부 출력 (#if GOAP_PERF_LOG)
```

3. **W4+W5 완료 직후 2048로 내려 재측정** — 새 휴리스틱과 Closed Set이 정상이라면 2048로 충분해야 하며, 충분하지 않다면 그 자체가 W4/W5 버그의 신호다 (명세 W6 성능 게이트 논리). 이 재측정을 W5 DoD에 항목으로 추가한다:
   - [ ] MAX_NODES를 2048로 임시 하향 후 T3/T5 시나리오 성공 확인 → 성공 시 2048 복원 커밋

---

## P4. 창고(Storehouse) 이중 게이트 — 기존 문제가 수치화로 노출됨 🟡

### 문제

BuildStorehouse의 Precondition이 현재 **불리언 + 수치 이중**으로 걸려 있다:
- 불리언(기존 유지): `HasWoodForBuilding == 1` — 이 파생 플래그의 임계값은 **35**
- 수치(신규): `WoodStock ≥ 15` — 창고의 실제 비용

결과: 창고는 목재 15면 지을 수 있는데 **불리언 게이트가 35를 요구**해서, 목재 15~34 구간에서 플래너가 창고 건설을 부당하게 배제한다. 이는 원래부터 있던 문제(모든 건물이 동일 플래그 공유)지만, 수치 Prec이 생긴 지금은 **정확한 게이트가 존재하므로 부정확한 게이트를 제거할 수 있다.**

### 수정

Storehouse와 Campfire(목재 5)의 **불리언 자원 플래그 Prec만 제거**하고 수치 Prec을 유지한다. TownHall(35)과 Forge는 불리언 임계값과 실비용이 근접하므로 Phase 2에서는 그대로 둔다 (ADR-1 병행 원칙 — 최소 침습).

```csharp
// BuildStorehouse: PrecCount 6 → 4
//   제거: HasWoodForBuilding, HasStoneForBuilding
//   유지: BuildingQueued==1, WoodStock≥15, StoneStock≥5, (+기존 기타)
// BuildCampfire: 동일 패턴 — HasWoodForBuilding 제거, WoodStock≥5 유지
```

**게임플레이 이득**: 초반 목재 15~34 구간에서 창고를 지을 수 있게 되어 저장 한도 병목이 풀린다 — "게임이 잘 진행되어야 한다"에 직접 기여하는 수정이다.

### 검증
- [ ] 목재 20 / 석재 10 상태에서 창고 건설 명령 → 플랜 성공
- [ ] 목재 20 상태에서 TownHall 명령 → 여전히 배제 (회귀 확인)

---

## P5. SeekMedicalAid 회복량 대조 🟡

`MEDICAL_HEALTH_GAIN = 50`의 런타임 대응치를 이번 리뷰에서 확정하지 못했다 (ActionDatabase에서 RestoreHealth 계열 상수 미발견 — 치료가 Healer 건물/틱 회복 방식일 가능성). **직접 확인 절차**:

1. `grep -n "HealthLevel +=\|HealthLevel =" Assets/Scripts` 로 체력 회복 지점 전수 조사
2. SeekMedicalAid 완료 시 1회성 +N인지, 치료소 체류 중 틱당 회복인지 판별
3. **1회성이면** 그 값으로 상수 교체. **틱 회복이면** 플래너 모델과 근본이 다르므로: `MyHealth Add`를 "치료 1세션의 기대 회복량"으로 정의하고 주석에 명시 (예: 초당 5 × 평균 체류 10초 = 50) — 근사 모델임을 기록해두면 W7에서 정합성 테스트(T12)의 허용 오차 설계에 쓰인다.

---

## 마감 체크리스트 (P1~P5 반영 후)

- [ ] **10분 무개입 방치 테스트**: 자원 4종 30+ 수렴 → 건설 진행 → Explore 전환의 사이클이 끊기지 않음 (P1 검증의 최종 형태)
- [ ] 석재/식량 단독 부족 시나리오에서 -1 루프 미발생 (Console에 "이미 달성" 반복 로그 없음)
- [ ] 배고픔 90 주민의 식사 플랜 스텝 수 == 실측 결과 일치 (P2)
- [ ] 목재 20으로 창고 건설 성공 (P4)
- [ ] `UseNumericGoals = false` 롤백 시 전부 기존 동작 (ADR-8 회귀)
- [ ] Profiler: 스트레스 상황 Job 합산 시간 기록 — W4/W5 완료 후 비교할 기준선(baseline) 확보

이후 W4(새 휴리스틱) → W5(Closed Set) 순서로 진행하면 된다. **P1~P2를 건너뛰고 W4로 가지 말 것** — 휴리스틱은 탐색 속도를 바꿀 뿐, 잘못된 Goal 정의와 수치는 그대로 증폭한다.
