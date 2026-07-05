# Phase 2 실행 명세서 — 수치형 GOAP + Utility Goal 중재기

> **목적**: 확장설계 분석서의 Phase 2(3장·5장·3.6절)를 **첫 구현에 100% 도달**시키기 위한 실행 명세.
> Phase 1 1차 구현이 78%에 그친 원인을 회고하고, 그 원인을 문서 구조 자체로 차단한다.
>
> **대상 리포지토리**: `jhyun1234/AI_GOAP` @ `4210db3` 기준
> **작성일**: 2026-07-05

---

## 0. Phase 1 회고 — 왜 78%였고, 이 문서는 무엇이 다른가

| Phase 1 미달 원인 | 실제 사례 | Phase 2 문서의 대응 |
|---|---|---|
| **R1. "무엇"만 있고 "어디에 어떻게"가 없었다** | 설계서 7장이 "월드스페이스 말풍선"이라고만 써서 → 전역 토스트로 구현됨 | 모든 작업 항목(W)에 **대상 파일 경로 + 코드 스케치 + 시그니처**를 명시 |
| **R2. 완료 판정 기준이 없었다** | "컨텍스트 비용" 중 부상 배율은 공식 목록에만 있어 누락됨 | 항목마다 **DoD 체크리스트**(테스트 가능한 문장). 하나라도 미충족이면 그 항목은 미완 |
| **R3. 큰 덩어리를 원자 단위로 쪼개지 않았다** | "Level 3" 하나에 4개 하위 요구가 숨어 있었음 | Phase 2를 **9개 독립 작업(W1~W9)**으로 분해. 각각이 컴파일·플레이 가능한 커밋 단위 |
| **R4. 오해 가능 지점을 예고하지 않았다** | "RefusalBubble 재활용" 지시 자체가 잘못된 전제였음 | 항목마다 **⚠️ 오해 위험** 절을 두어 흔한 잘못된 해석을 사전 차단 |

> 참고: 보완가이드(F1~F7)는 코드 스케치 + 검증 기준 형식이었고 반영률 95%였다. **이 문서 전체가 그 형식이다.**

### Phase 2 성공 기준 (측정 가능)

- [ ] 평균 플랜 길이: 현재 1~3스텝 → **5스텝 이상** (배고픔+식량0 시나리오에서 8스텝 플랜 관측)
- [ ] "ChopWood ×N" 반복 체인을 **플래너가 스스로** 산출 (리플래닝 루프 아님) — 사고 체인 UI에서 확인
- [ ] Goal 전환이 임계값 스위치가 아닌 점수 경쟁으로 결정 (로그로 상위 3개 Goal 점수 출력)
- [ ] 70명 동시 플래닝 ≤ 1.8ms 유지 (Profiler 캡처 첨부)
- [ ] EditMode 테스트 12케이스 전부 green

---

## 1. 아키텍처 결정 사항 (ADR) — 구현 전 반드시 읽을 것

구현 중 판단이 갈릴 수 있는 지점을 미리 결정해둔다. **여기 있는 결정을 임의로 바꾸지 말고, 바꿔야 한다면 사유를 커밋 메시지에 남길 것.**

**ADR-1. 슬롯은 43 → 52로 확장하고, 기존 불리언 슬롯은 유지한다 (병행 마이그레이션)**
기존 파생 플래그(WoodLow, HasCookedFood 등)를 지우지 않는다. 지우면 19개 액션·Goal 정의·FSM 분기가 한꺼번에 깨져 회귀 지옥이 된다. Phase 2에서는 수치 슬롯을 *추가*만 하고, 불리언 제거는 Phase 3 과제로 미룬다.

**ADR-2. 수치는 양자화 없이 원시 단위 int를 그대로 쓴다**
설계서 3.1의 "5단위 양자화"는 채택하지 않는다. 슬롯이 이미 int이므로 목재 37은 37로 저장한다. 양자화는 버그 표면만 늘린다.

**ADR-3. Goal 판정에 연산자(GoalOp)가 반드시 필요하다** ⚠️ 설계서에 없던 항목
현재 목표 판정은 `state[s] != GoalState[s]` **등호 비교**다. "목재 ≥ 30" 같은 수치 목표는 등호로 판정 불가능하다. Prec/Eff 연산자만 추가하고 GoalOp를 빠뜨리면 **수치형 GOAP 전체가 작동하지 않는다.** W2에 포함.

**ADR-4. 깊이 12 확장에는 중복 상태 차단(Closed Set)이 선행돼야 한다** ⚠️ 설계서에 없던 항목
현재 Job에는 방문 상태 중복 검사가 없다. 불리언+깊이 6에서는 견딜 수 있었지만, 수치형+깊이 12에서는 `ChopWood→MineStone`과 `MineStone→ChopWood`가 같은 상태를 두 번 만들며 노드 2048이 조합 폭발로 즉사한다. W5에 포함. **W5 없이 W6(깊이 12)을 켜지 말 것.**

**ADR-5. 휴리스틱의 최소 액션 비용은 하드코딩(2.9f)이 아니라 Job 시작 시 실측한다**
Phase 1의 컨텍스트 배율이 비용을 동적으로 바꾼다: `RestOnGround ×0.4`, `AttackEnemy ×0.7`이면 실효 최소 비용이 2.9f 아래로 내려가 **기존 휴리스틱조차 admissible이 깨질 수 있다** (현재 코드의 잠재 버그). 새 휴리스틱은 `Actions` 배열에서 min(BaseCost)를 스캔해 스케일로 쓴다.

**ADR-6. Effect 적용 시 음수 클램프는 Job 안에서 한다**
`MyHunger -60`이 음수가 되면 이후 Precondition 비교가 왜곡된다. `Sub` 연산은 `max(0, ...)` 고정.

**ADR-7. 플래너 Effect와 런타임 Commit의 수치는 단일 출처에서 나와야 한다**
플래너가 "ChopWood = +5"로 계획했는데 런타임(`OnActionCompleted`/ActionDatabase)이 +3을 주면 플랜-현실 괴리가 재발한다. 수치 상수를 `GOAPActionRegistry` 안의 `public const`로 승격하고 런타임이 같은 상수를 참조한다. W7 + 정합성 테스트(W9)로 강제.

**ADR-8. 전환 스위치**
`GOAPPlannerScheduler`에 `public static bool UseNumericGoals = true;` 플래그를 둔다. 문제가 생기면 런타임에 레거시 Goal 정의로 즉시 롤백 가능해야 한다. Phase 2 안정화 후 제거.

---

## 2. 작업 분해 총괄표

| # | 작업 | 파일 | 의존 | 작업량 |
|---|---|---|---|---|
| W1 | 슬롯 52 확장 + 수치 스냅샷 채우기 | GOAPPlanningSlots.cs | — | 소 |
| W2 | Prec/Eff/Goal 연산자 확장 (구조체+Job) | GOAPActionRegistry.cs, GOAPPlannerJob.cs | W1 | 중 |
| W3 | 액션 20개 수치 재정의 (데이터 마이그레이션) | GOAPActionRegistry.cs | W2 | 중 |
| W4 | 새 휴리스틱 (부족량/최대증가량) | GOAPPlannerJob.cs, Registry | W2 | 중 |
| W5 | Closed Set (중복 상태 차단) | GOAPPlannerJob.cs, Scheduler | W2 | 중 |
| W6 | 깊이 12 + 성능 게이트 | GOAPPlannerJob.cs | W4, W5 | 소 |
| W7 | 런타임 정합화 (Commit 상수 통일) | VillagerFSM.cs, ActionDatabase.cs | W3 | 중 |
| W8 | GoalArbiter (Utility 중재기) | **신규** GoalArbiter.cs, VillagerBrain.cs, VillagerFSM.cs | — (병행 가능) | 대 |
| W9 | EditMode 테스트 12케이스 + 플랜 수선 | **신규** Tests/, VillagerFSM.cs | 전체 | 중 |

**커밋 순서 = W 번호 순서.** 각 W는 독립 커밋이며, 커밋 후 게임이 반드시 실행돼야 한다. W2가 완료된 시점에는 모든 액션이 기본값(Equal/Set)으로 동작하므로 **행동 변화가 0이어야 정상**이다 (회귀 스모크 포인트).

---

## W1. 슬롯 52 확장 + 수치 스냅샷

**파일**: `GOAPPlanningSlots.cs`

### 슬롯 추가 (기존 43개 뒤에 그대로 이어붙인다 — 기존 인덱스 절대 변경 금지)

```csharp
// ── [Phase 2] 수치 슬롯 (43~51) ── 값은 0/1이 아니라 원시 단위 정수다.
public const int WoodStock       = 43;  // 마을 목재 보유량 (AuthoritativeWorldState 미러)
public const int StoneStock      = 44;
public const int IronStock       = 45;
public const int CopperStock     = 46;
public const int RawFoodStock    = 47;
public const int CookedFoodStock = 48;
public const int MyHunger        = 49;  // 이 주민의 배고픔 0~100 (Brain 미러)
public const int MyFatigue       = 50;  // 이 주민의 피로도 0~100
public const int MyHealth        = 51;  // 이 주민의 체력 0~100

public const int TOTAL_SLOTS = 52;      // 43 → 52
```

### BuildCurrentState() 말미에 수치 채우기 추가

```csharp
// [Phase 2] 수치 슬롯 — 마을 자원과 주민 스탯을 그대로 미러링
state[GOAPPlanningSlots.WoodStock]       = worldState.WoodAvailable;   // ※ 실제 프로퍼티명 확인
state[GOAPPlanningSlots.StoneStock]      = worldState.StoneAvailable;
state[GOAPPlanningSlots.IronStock]       = worldState.IronAvailable;
state[GOAPPlanningSlots.CopperStock]     = worldState.CopperAvailable;
state[GOAPPlanningSlots.RawFoodStock]    = worldState.RawFoodAvailable;
state[GOAPPlanningSlots.CookedFoodStock] = worldState.CookedFoodAvailable;
state[GOAPPlanningSlots.MyHunger]        = (int)brain.HungerLevel;
state[GOAPPlanningSlots.MyFatigue]       = (int)brain.FatigueLevel;
state[GOAPPlanningSlots.MyHealth]        = (int)brain.HealthLevel;
```

**⚠️ 오해 위험**: `AuthoritativeWorldState`의 실제 자원 프로퍼티명을 확인해 맞출 것(위 이름은 추정). 프로퍼티가 없고 예약 시스템 경유라면 "가용량(총량−예약량)"을 쓴다 — 플래너가 남이 예약한 자원 위에 계획을 세우면 안 되기 때문이다.

### DoD
- [ ] TOTAL_SLOTS 52로 컴파일, 기존 씬 실행 시 행동 변화 없음 (수치 슬롯은 아직 아무도 안 읽음)
- [ ] 디버그 로그로 플래닝 직전 state[43..51]에 실제 값이 들어오는 것 확인

---

## W2. 연산자 확장 — Prec / Eff / **Goal** 3종 세트

**파일**: `GOAPActionRegistry.cs` (구조체), `GOAPPlannerJob.cs` (판정·적용), `GOAPPlanningSlots.cs` (BuildGoalState 시그니처)

### 2-1. enum과 구조체 필드 추가

```csharp
// GOAPActionRegistry.cs — 파일 상단
public enum PrecOp : int { Equal = 0, GreaterEq = 1, LessEq = 2 }   // int: Burst 구조체 정렬 단순화
public enum EffOp  : int { Set = 0, Add = 1, Sub = 2 }

// GOAPActionDef 구조체 — 기존 S/V 쌍 옆에 Op 필드 8개씩 추가
public int Prec0Op; public int Prec1Op; /* ... Prec7Op */
public int Eff0Op;  public int Eff1Op;  /* ... Eff7Op  */
// 기본값 0 = Equal / Set → 기존 19개 액션 정의는 한 글자도 안 바꿔도 동일하게 동작한다.
```

### 2-2. Job 판정 함수 — switch-unroll을 유지하면서 op 분기 삽입

기존 unroll의 각 항을 인라인 헬퍼 호출로 치환한다:

```csharp
// GOAPPlannerJob.cs
private static bool PrecHolds(int stateVal, int op, int reqVal)
{
    // Burst: 상수 분기 3개 — 예측 잘 되는 branch, 벡터화 방해 없음
    if (op == 1) return stateVal >= reqVal;   // GreaterEq
    if (op == 2) return stateVal <= reqVal;   // LessEq
    return stateVal == reqVal;                 // Equal
}

// CheckPreconditionsAtNode의 각 case 항 치환 예 (case 2):
case 2: return PrecHolds(NodeStates[o + action.Prec0S], action.Prec0Op, action.Prec0V)
            && PrecHolds(NodeStates[o + action.Prec1S], action.Prec1Op, action.Prec1V);
```

```csharp
private static int ApplyEff(int stateVal, int op, int v)
{
    if (op == 1) return stateVal + v;                              // Add
    if (op == 2) { int r = stateVal - v; return r < 0 ? 0 : r; }   // Sub + 클램프 (ADR-6)
    return v;                                                       // Set
}
// ApplyEffectsToNode의 각 대입 치환 예:
// NodeStates[o + action.Eff0S] = ApplyEff(NodeStates[o + action.Eff0S], action.Eff0Op, action.Eff0V);
```

`GOAPActionDef.CheckPreconditions()` (구조체 내 헬퍼)도 동일하게 치환한다 — Job 밖 사전검증 경로가 있으므로 **두 군데 다** 고쳐야 한다.

### 2-3. GoalOp — 목표 판정 연산자 (ADR-3)

```csharp
// BuildGoalState 시그니처 확장 (호출부는 Scheduler 1곳뿐):
public static void BuildGoalState(string goalId,
    out NativeArray<int> goalState, out NativeArray<int> goalMask,
    out NativeArray<int> goalOps,          // ← 신규: 슬롯별 판정 연산자 (PrecOp 재사용)
    Allocator allocator = Allocator.Persistent)

// Job 입력 필드 추가:
[ReadOnly] public NativeArray<int> GoalOps;

// IsGoalSatisfied / IsGoalSatisfiedAtNode 판정 치환:
if (GoalMask[s] == 1 && !PrecHolds(state[s], GoalOps[s], GoalState[s])) return false;
```

**⚠️ 오해 위험 3가지**
1. Prec/Eff만 하고 GoalOp를 빼먹는 것 — 그러면 "WoodStock ≥ 30" Goal을 표현할 수 없어 W3 전체가 막힌다. **W2의 DoD에 GoalOp가 포함된 이유.**
2. Sub 클램프를 잊는 것 — MyHunger가 음수가 되면 LessEq 판정이 영구 참이 되어 플랜이 오염된다.
3. Scheduler의 Dispose 경로 — `goalOps` NativeArray를 기존 goalState/goalMask와 같은 위치(성공·실패·조기종료 3경로 모두)에서 Dispose할 것. Phase 1 스타일상 누락 시 에디터에서 leak 경고가 뜬다.

### DoD
- [ ] 컴파일 후 기존 씬 실행: 모든 주민 행동이 W2 이전과 **완전히 동일** (기본값 = 레거시 동작)
- [ ] 임시 테스트: WoodStock=10 상태에서 `Prec: WoodStock GreaterEq 30` 액션이 플랜에서 배제되는 것 확인
- [ ] Editor 플레이 종료 시 NativeArray leak 경고 0건

---

## W3. 액션 데이터 마이그레이션 — 20개 전체 명세

**파일**: `GOAPActionRegistry.cs`

### 3-1. 산출·소모 상수 승격 (ADR-7)

```csharp
// GOAPActionRegistry.cs — 플래너와 런타임이 공유하는 단일 출처
public const int YIELD_CHOP_WOOD    = 5;
public const int YIELD_MINE_STONE   = 5;
public const int YIELD_MINE_IRON    = 3;
public const int YIELD_MINE_COPPER  = 3;
public const int YIELD_HARVEST_BERRIES = 3;
public const int COOK_RAW_CONSUME   = 2;
public const int COOK_YIELD         = 1;
public const int EAT_HUNGER_RELIEF  = 60;   // 조리 식사
public const int EAT_RAW_RELIEF     = 35;   // 생식 (조리보다 비효율 — 요리 체인 유도)
public const int SLEEP_FATIGUE_RELIEF = 70;
public const int REST_FATIGUE_RELIEF  = 20; // 기존 REST_ON_GROUND_FATIGUE_RECOVERY와 값 일치시킬 것
```

**⚠️ 오해 위험**: 위 수치 중 채집 산출량·회복량은 **기존 ActionDatabase/FSM 상수와 반드시 대조**해서 이미 있는 값을 따를 것 (예: FSM의 `REST_ON_GROUND_FATIGUE_RECOVERY = 20f`). 이 표의 값은 기존 값이 확인 안 되는 경우의 기본 제안치다. **플래너 수치를 새로 정하는 게 아니라, 런타임에 이미 존재하는 수치를 플래너에 알려주는 작업이다.**

### 3-2. 마이그레이션 표 — 각 액션에 *추가*할 수치 Prec/Eff

기존 불리언 Prec/Eff는 유지하고(ADR-1) 아래를 **추가**한다. 8쌍 한도 내에서 전부 수용 가능함을 확인했다.

| 액션 | 추가 Precondition | 추가 Effect |
|---|---|---|
| ChopWood | — | `WoodStock Add +YIELD_CHOP_WOOD` |
| MineStone | — | `StoneStock Add +YIELD_MINE_STONE` |
| MineIron | — | `IronStock Add +YIELD_MINE_IRON` |
| MineCopper | — | `CopperStock Add +YIELD_MINE_COPPER` |
| HarvestWildBerries | — | `RawFoodStock Add +YIELD_HARVEST_BERRIES` |
| CookMeal | `RawFoodStock GreaterEq COOK_RAW_CONSUME` | `RawFoodStock Sub COOK_RAW_CONSUME`, `CookedFoodStock Add COOK_YIELD` |
| EatCookedFood | `CookedFoodStock GreaterEq 1` | `CookedFoodStock Sub 1`, `MyHunger Sub EAT_HUNGER_RELIEF` |
| EatRawFood | `RawFoodStock GreaterEq 1` | `RawFoodStock Sub 1`, `MyHunger Sub EAT_RAW_RELIEF` |
| Sleep | — | `MyFatigue Sub SLEEP_FATIGUE_RELIEF` |
| RestOnGround | — | `MyFatigue Sub REST_FATIGUE_RELIEF` |
| BuildTownHall | `WoodStock GreaterEq 35`, `StoneStock GreaterEq 20` ※ | `WoodStock Sub 35`, `StoneStock Sub 20` ※ |
| BuildForge / BuildStorehouse / BuildCampfire | 위와 동일 패턴 — **건물별 실제 비용을 ActionDatabase/BuildingQueue에서 찾아 그 값을 쓸 것** ※ | 동일 ※ |
| CraftWeapon | `IronStock GreaterEq 실제비용` ※ | `IronStock Sub 실제비용` ※ |
| SeekMedicalAid | — | `MyHealth Add 50` (상한 100 클램프는 런타임 몫 — 플래너는 초과 무해) |
| AttackEnemy / CraftPrimitiveWeapon / MoveToBase / Explore | 변경 없음 (Phase 2 범위 외) | 변경 없음 |

※ 표시 항목: **기존 코드/데이터에서 실제 수치를 발굴해 채워 넣는 것까지가 이 작업이다.** 임의 수치를 넣으면 W7 정합성 테스트에서 걸리게 설계돼 있다.

> 주의: 기존 Prec 쌍이 이미 3개인 건설 액션에 2쌍을 추가하면 5쌍 — 한도(8) 내. Eff도 기존 3+2=5로 한도 내. 초과하는 액션이 나오면 불리언 쌍 중 수치로 대체 가능한 것(HasWoodForBuilding ↔ WoodStock GreaterEq)을 **교체**하되, 교체 사실을 커밋 메시지에 기록.

### 3-3. Goal 수치 재정의 (BuildGoalState, `UseNumericGoals == true`일 때)

| goalId | 레거시 (유지) | 수치형 (신규) |
|---|---|---|
| SurviveHunger | HungerSolved == 1 | `MyHunger LessEq 30` |
| SurviveFatigue | FatigueSolved == 1 | `MyFatigue LessEq 30` |
| SurviveInjury | InjurySolved == 1 | `MyHealth GreaterEq 60` |
| GatherWood | ResourcesGathered == 1 | `WoodStock GreaterEq 30` |
| GatherStone / Iron / Copper | 〃 | 각 Stock `GreaterEq 30` (Iron/Copper는 15) |
| GatherResources | 〃 | **가장 부족한 자원 1종으로 위임** — Scheduler가 최저 재고 타입을 골라 GatherWood 등으로 치환 |
| CookMeal | MealCooked == 1 | `CookedFoodStock GreaterEq 3` |
| BuildStructure / Defend / Explore / MoveToBase | 변경 없음 | 변경 없음 |

**여기서 멀티스텝이 태어난다**: `SurviveHunger` = "MyHunger ≤ 30"이고 CookedFood가 0, RawFood가 0이면, 플래너는 `HarvestWildBerries → HarvestWildBerries → CookMeal → EatCookedFood`를 **스스로 도출**한다. 성공 기준 1번이 이 시나리오다.

### DoD
- [ ] 배고픔 90 / 전 식량 0 상태에서 위 4~5스텝 플랜이 산출되고 사고 체인 UI에 표시됨
- [ ] `UseNumericGoals = false`로 내리면 기존 행동으로 복귀
- [ ] 목재 5 상태의 GatherWood에서 `ChopWood ×5` 체인 산출 (깊이 6 한도 내 최대치 — W6 전까지는 5회로 잘리는 게 정상)

---

## W4. 새 휴리스틱 — 부족량 / 최대증가량

**파일**: `GOAPPlannerJob.cs`, `GOAPActionRegistry.cs`, `GOAPPlannerScheduler.cs`

### 설계

```
h(n) = Σ_{s: GoalMask[s]=1, 미충족} StepsNeeded(s) × MinActionCost
StepsNeeded(s):
  GoalOp == GreaterEq:  ceil( (GoalState[s] − state[s]) / MaxGain[s] )
  GoalOp == LessEq:     ceil( (state[s] − GoalState[s]) / MaxDrop[s] )
  GoalOp == Equal:      1   (기존 불리언 슬롯 — 종전과 동일)
```

- `MaxGain[s]` / `MaxDrop[s]`: 슬롯 s를 가장 크게 올리는/내리는 액션 Effect의 절대값. **Registry가 BuildActionDefs 직후 액션 배열을 1회 스캔해 산출**하고, Scheduler가 `NativeArray<int>` 2개로 Job에 주입한다. 해당 슬롯을 바꾸는 액션이 없으면 1로 채운다(0 나눗셈 방지 + admissible 유지).
- `MinActionCost`: Job의 `Execute()` 서두에서 `Actions` 배열 min(BaseCost) 실측 (ADR-5). `HEURISTIC_WEIGHT` 상수는 삭제하고, `minCost * 0.99f`를 스케일로 사용 — 근소하게 낮춰 admissibility에 여유를 둔다.

### admissibility 증명 메모 (주석으로 코드에 남길 것)
목표 슬롯 하나를 충족하려면 최소 StepsNeeded번의 액션이 필요하고(최대 증가량 가정), 각 액션은 최소 minCost 이상이므로 h ≤ 실제 잔여 비용. 슬롯 여러 개의 합산은 한 액션이 두 슬롯을 동시에 진전시키는 경우 과대평가가 될 수 있으나, **현 액션 데이터(W3 표)에서 서로 다른 Goal 슬롯 2개를 동시에 올리는 액션은 없음**을 확인했다. 향후 그런 액션 추가 시 h를 max()로 바꿀 것 — 이 경고를 Registry 주석에 남긴다.

### DoD
- [ ] 계측 로그: "배고픔+식량0" 시나리오에서 확장 노드 수가 W4 이전 대비 **50% 이상 감소**
- [ ] 동일 시나리오에서 산출 플랜이 W4 이전과 동일 (휴리스틱은 속도만 바꾸고 답은 못 바꿔야 한다 — 최적성 회귀 검증)

---

## W5. Closed Set — 중복 상태 차단 (ADR-4)

**파일**: `GOAPPlannerJob.cs`, `GOAPPlannerScheduler.cs`

### 설계 — 개방 주소 해시 테이블 (NativeArray 기반, Burst 호환)

```csharp
// Scheduler가 할당해 주입:
public NativeArray<int>   VisitedHashes;   // 크기 4096 (MAX_NODES × 2), 0 = 빈 슬롯
public NativeArray<int>   VisitedNodeIdx;  // 해시 일치 시 전수 비교할 대표 노드
public NativeArray<float> VisitedGCosts;   // 대표 노드의 g-cost

// Job 내부:
private int HashState(int nodeIdx, int totalSlots)   // FNV-1a, 결과 0이면 1로 치환(0은 빈 슬롯 표식)
private bool TryRegisterState(int newNode, float g, int totalSlots)
// 절차: 해시 → 선형 프로빙 → 빈 슬롯이면 등록(true)
//       해시 일치 슬롯이면 상태 전수 비교(52 int) →
//         동일 상태 && 기존 g <= 새 g  → false (확장 포기)
//         동일 상태 && 기존 g >  새 g  → g 갱신 후 true (더 싼 경로로 재개방)
//         다른 상태(해시 충돌)         → 프로빙 계속
```

자식 노드 생성 직후, HeapPush **전에** `TryRegisterState`가 false면 nodeCount를 되돌리고 skip한다.

**⚠️ 오해 위험**: "해시만 비교하고 전수 비교 생략" 최적화 금지 — 충돌 시 도달 가능한 플랜을 잘못 버려 **플래닝이 이유 없이 실패**하는, 재현 어려운 버그가 된다. 52 int 비교는 충돌 시에만 발생하므로 비용이 아니다.

### DoD
- [ ] 동일 시나리오에서 확장 노드 수 추가 감소 (계측 로그 비교)
- [ ] 산출 플랜 불변 (최적성 회귀)
- [ ] Scheduler의 3개 신규 배열이 모든 경로에서 Dispose됨 (leak 0)

---

## W6. 깊이 12 확장

**파일**: `GOAPPlannerJob.cs`

```csharp
public const int MAX_DEPTH    = 12;   // 6 → 12
public const int MAX_PLAN_LEN = 12;
// MAX_NODES = 2048 유지 — W4+W5가 선행됐으므로 증설 불필요 (프로파일 후 재검토)
```

`ResultActions` 할당 크기·`VillagerBrain.CurrentPlanFull` 표시가 12스텝을 수용하는지 확인 (사고 체인 UI가 길어진 플랜을 스크롤 없이 그리는지 — 잘리면 UI에 "…" 처리 1줄 추가).

### DoD
- [ ] 목재 0 → "WoodStock ≥ 30" Goal에서 `ChopWood ×6` 체인 산출 (깊이 6이던 시절 불가능하던 플랜)
- [ ] **성능 게이트**: 70명 강제 동시 플래닝 스트레스 씬에서 Job 합산 ≤ 1.8ms. 초과 시 이 커밋을 되돌리지 말고 MAX_NODES를 1024로 내려 재측정 — W4/W5가 정상이라면 노드 수가 병목일 수 없기 때문에, 초과는 W4/W5의 버그 신호다.

---

## W7. 런타임 정합화

**파일**: `VillagerFSM.cs` (`OnActionCompleted`/`ApplyActionEffect`), `ActionDatabase.cs`

- 채집·요리·식사·수면의 런타임 수치 반영부가 W3-1의 `GOAPActionRegistry.YIELD_*` / `*_RELIEF` / `*_CONSUME` 상수를 참조하도록 치환한다. 하드코딩 숫자가 남아 있으면 안 된다 — `grep -n "[^A-Z_]5f\|+= 5\|-= 2" `로 소탕.
- 자원 예약 시스템의 Commit 단계와 이중 반영되지 않는지 확인: **자원 증감의 반영 지점은 정확히 1곳**이어야 한다. 현재 주석("ConsumeResource 효과는 Commit 단계에서 이미 처리")대로면 플래너 상수만 맞추면 된다.

### DoD
- [ ] W9의 정합성 테스트 통과: 20개 액션 각각에 대해 "플래너 Effect 시뮬레이션 결과 == 런타임 실행 후 실측 delta"

---

## W8. GoalArbiter — Utility Goal 중재기

**파일**: 신규 `Assets/Scripts/AI/GoalArbiter.cs` (ConflictScoreCalculator와 같은 순수 계산 클래스 스타일), `VillagerFSM.cs`, `VillagerBrain.cs`

### 8-1. 구조와 계약

```csharp
/// 순수 정적 계산 — MonoBehaviour 아님, 상태 없음 (테스트 가능성 확보)
public static class GoalArbiter
{
    public struct GoalScore { public string GoalId; public float Score; }

    /// 후보 Goal 전체를 점수화해 내림차순 buffer에 채우고 개수를 반환.
    /// P0 Goal은 여기 없다 — P0는 기존 AnyState 하드 오버라이드가 계속 담당한다(설계서 5.2).
    public static int Evaluate(VillagerBrain brain, AuthoritativeWorldState world,
                               GoalScore[] buffer);
}
```

### 8-2. 점수 공식 (초기 상수 — 밸런싱 대상임을 주석에 명시)

```csharp
// 모든 곡선은 0~1 정규화. Response curve는 제곱 곡선으로 시작한다 (완만→급등).
score(SurviveHungerSoft) = Sq01((hunger - 50f) / 50f) * 1.2f    // P0(80) 전 연착륙용 소프트 식사
score(SurviveFatigueSoft)= Sq01((fatigue - 55f) / 45f) * 1.1f
score(GatherWood)   = Sq01((30f - woodStock) / 30f) * seasonBonus        // stone/iron/copper 동형
score(BuildStructure)= (queueLength > 0 && hasResources) ? 0.55f : 0f
score(DefendVillage) = brain.NearEnemy ? (brain.HasAnyWeapon ? 0.9f : 0.3f) : 0f
score(Explore)       = 0.15f + 0.35f * unexploredRatio                    // FoW 매니저에서 읽기
// Sq01(x) = Mathf.Clamp01(x)²
```

**히스테리시스 (설계서 5.2 — 반드시 포함)**:
```csharp
if (goalId == brain.CurrentGoalId) score *= 1.15f;               // 관성 보너스
// FSM 측: 마지막 Goal 전환 후 5초 이내에는 최고점이 현재 Goal의 1.3배를 넘지 않는 한 유지
```

### 8-3. FSM 통합 — State_Idle()의 하드코딩 if 3개를 교체

```csharp
// State_Idle() 교체부:
int n = GoalArbiter.Evaluate(Brain, _worldState, _goalScoreBuffer);   // 버퍼는 FSM 필드로 재사용(GC 0)
if (n > 0 && _goalScoreBuffer[0].Score > GOAL_SCORE_MIN)              // MIN = 0.1f
{
    string next = _goalScoreBuffer[0].GoalId;
    bool changed = Brain.CurrentGoalId != next;
    Brain.CurrentGoalId = next;
    if (changed) ShowThoughtBubble(Pick(ThoughtPoolFor(next)));       // F4 발화 계승
    TransitionTo(VillagerState.Planning);
    return;
}
// 전부 저점수면 진짜 Idle (서성임) — 기존 fallback 유지
```

**⚠️ 오해 위험 3가지**
1. **P0를 Arbiter에 흡수하지 말 것.** 생존 인터럽트는 AnyState 체크(Update 서두)에 남는다. Arbiter는 Idle에서만 호출된다. 소프트 식사/휴식 Goal(임계 50/55부터 점수화)은 P0의 *예방* 계층이지 대체가 아니다 — goalId는 기존 SurviveHunger를 재사용해 BuildGoalState 수정을 피한다.
2. `GetHighestPriorityGoalId()`의 P0 부분은 남기고, **P2 이하를 결정하던 FSM if 3개만** 제거한다. Brain 쪽 함수에서 P2 로직을 찾으려 하지 말 것(원래 없다 — 그게 L4였다).
3. 점수 로그: `GOAL_ARBITER_LOG` 조건부 컴파일로 상위 3개 Goal 점수를 주민 선택 시 사고 패널에 노출 — 성공 기준 3번의 검증 수단이자 밸런싱 도구다. 로그 없이 커밋하면 밸런싱이 불가능해진다.

### DoD
- [ ] 배고픔 60~75 주민이 하던 채집을 마치고(관성) 식사 Goal로 전환하는 장면 관측
- [ ] 5분 방치 시 Goal 전환 횟수가 주민당 분당 4회 이하 (우왕좌왕 없음 — 히스테리시스 검증)
- [ ] 주민 패널에 상위 3개 Goal 점수 표시

---

## W9. 검증 체계 — EditMode 테스트 12케이스 + 플랜 수선

**파일**: 신규 `Assets/Tests/EditMode/GOAPPlannerTests.cs` (+ asmdef), `VillagerFSM.cs`

### 9-1. 테스트 인프라

Job은 `job.Run()`으로 메인 스레드 동기 실행이 가능하다 — 씬 없이 순수 로직 테스트가 된다. 헬퍼 하나로 전 케이스를 커버한다:

```csharp
private static int[] Plan(Dictionary<int,int> state, string goalId, AgentRole role = AgentRole.None)
// state 딕셔너리로 CurrentState 구성 → BuildGoalState → BuildActionDefs → job.Run() → ResultActions 반환
```

### 9-2. 케이스 목록 (전부 구현 — 하나라도 빠지면 W9 미완)

| # | 시나리오 (초기 상태) | Goal | 기대 결과 |
|---|---|---|---|
| T1 | 레거시 회귀: CookedFood=1, NearFireplace=1 | SurviveHunger(레거시 플래그) | 기존과 동일 플랜 (연산자 기본값 검증) |
| T2 | MyHunger=90, CookedFoodStock=2 | SurviveHunger(수치) | `[EatCookedFood]` 1스텝 |
| T3 | MyHunger=90, Cooked=0, Raw=0 | SurviveHunger | `[HarvestBerries, CookMeal, EatCookedFood]` 포함 4스텝± — **멀티스텝 핵심 케이스** |
| T4 | WoodStock=5 | GatherWood(≥30) | `ChopWood ×5` |
| T5 | WoodStock=0, MAX_DEPTH=12 | GatherWood(≥30) | `ChopWood ×6` (깊이 검증) |
| T6 | Raw=1 (CookMeal 요구=2) | CookMeal(수치) | 플랜에 HarvestBerries 선행 — GreaterEq Prec 검증 |
| T7 | MyHunger=90, Cooked=1, Raw=0, 그리고 Cooked를 이미 남이 소진 가정(0) | SurviveHunger | Sub 클램프·재고 0 분기 — HarvestBerries 경로 선택 |
| T8 | 목표 이미 달성 (WoodStock=50) | GatherWood | ResultLength == -1 (특수값 회귀) |
| T9 | 알 수 없는 goalId | "Nonsense" | Schedule 조기 중단 (기존 방어 회귀) |
| T10 | T3 상태에서 W4 켬/끔 | SurviveHunger | 동일 플랜 + 노드 수 감소 (휴리스틱 최적성) |
| T11 | T5 상태에서 W5 켬/끔 | GatherWood | 동일 플랜 + 노드 수 감소 (Closed Set 최적성) |
| T12 | **정합성**: 20개 액션 각각 | — | 플래너 Effect 시뮬 결과 == 런타임 상수 기반 기대 delta (ADR-7 강제) |

### 9-3. 플랜 수선 (설계서 3.6 축소판 — 이번 Phase에서는 2개만)

1. **실행 전 사전검증**: `State_Executing`에서 다음 액션 시작 직전, 최신 스냅샷으로 해당 액션 Prec만 재검사. 불충족 시 EnterReplanning — 단 **CurrentGoalId를 유지**하고 Idle을 경유하지 않는다 (완료 진척 보존).
2. **수선 실패 한도**: 같은 Goal에서 수선 리플래닝 2연속 실패 시에만 Goal 재선택(Idle 복귀). 기존 FallbackCounter와 별도 카운터로 관리 — 의미가 다르다(수선 = 세계 변화 대응, Fallback = 플래닝 자체 실패).

기회주의적 스텝 삽입(3.6의 3번)은 **Phase 3로 명시 이월** — 범위에 넣지 말 것.

### DoD
- [ ] 12케이스 green, CI 없이도 `Window > General > Test Runner`로 원클릭 재현
- [ ] 수선 경로: 요리 완성 직전 다른 주민이 재료를 소진하는 연출 씬에서, Goal 유지한 채 재플래닝하고 말풍선("어라, 재료가 없어졌네") 발화

---

## 3. 최종 마감 체크리스트

- [ ] W1~W9 각 DoD 전부 체크 (부분 완료는 완료가 아니다 — R2 재발 방지)
- [ ] 성공 기준 5항목 전부 충족 (0장)
- [ ] `UseNumericGoals` 토글 양방향 정상 (ADR-8)
- [ ] Profiler 캡처: 스트레스 씬 1.8ms 이내 스크린샷을 커밋에 첨부
- [ ] 커밋 9개, 각 커밋 단독으로 컴파일·플레이 가능
- [ ] 리뷰 요청 시점: **W3 완료 직후 중간 리뷰 1회 권장** — 데이터 마이그레이션이 이 Phase의 오류 밀집 지대이므로, 전체 완성 후보다 절반 시점 점검이 총비용이 싸다

---

## 부록 — Phase 2에서 절대 하지 않는 것 (스코프 가드)

- 불리언 슬롯 제거·정리 (Phase 3)
- 성격/기분/관계 (Phase 3)
- 수요 게시판·협동 (Phase 4)
- 기회주의적 플랜 삽입 (Phase 3)
- 경로 기반 위험 비용 (Phase 2.5 — 수치형 안정화 후 별도 검토)

스코프를 넘는 좋은 아이디어가 떠오르면 코드가 아니라 이 문서 하단에 메모로 남길 것. Phase 1의 교훈 중 하나는 **범위 준수가 완성도의 전제**라는 것이다.
