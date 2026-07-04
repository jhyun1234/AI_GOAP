# Phase 1 보완 가이드 — 남은 20%를 마감하는 수정 설계서

> **목적**: Phase 1 구현(커밋 `c6d9604`) 검토에서 확인된 미완 구간을 수정하기 위한 실행 문서.
> 각 항목은 **문제 → 원인 → 수정 설계 → 검증 기준** 순으로 기술하며, 우선순위와 예상 작업량을 명시한다.
>
> **대상 커밋**: `c6d9604` "Implement Phase 1 AI expansion"
> **작성일**: 2026-07-05

---

## 0. 수정 항목 총괄표

| # | 항목 | 심각도 | 작업량 | 완료 시 Phase 1 기여도 |
|---|---|---|---|---|
| F1 | 사고 말풍선 스팸 방지 (스로틀) | 🔴 높음 — 실플레이 차단급 | 소 (~30분) | Level 3 안정화 |
| F2 | 말풍선을 월드스페이스로 전환 | 🟠 중간 — 체감 핵심 | 중 (~2시간) | Level 3 50→85% |
| F3 | 대사 랜덤 풀 (상황당 3종) | 🟡 낮음 | 소 (~30분) | Level 3 85→90% |
| F4 | Goal 전환 발화 추가 | 🟡 낮음 | 소 (~30분) | Level 3 90→95% |
| F5 | 부상 시 전투 비용 배율 | 🟠 중간 | 소 (~15분) | 컨텍스트 비용 80→90% |
| F6 | 액션 아이콘 카메라 빌보드 | 🟡 낮음 (카메라 구성에 따라) | 소 (~15분) | Level 1 95→100% |
| F7 (선택) | 미발견 자원 → Explore 유도 | 🟢 개선 | 소 (~30분) | 컨텍스트 비용 보강 |

**권장 작업 순서**: F1 → F5 → F2 → F3 → F4 → F6 → F7
(F1이 최우선인 이유: 현재 구조는 리플래닝 루프에 빠진 주민 1명이 토스트 채널 전체를 마비시킬 수 있다. F2보다 먼저 잡아야 F2 테스트가 가능하다.)

---

## F1. 사고 말풍선 스팸 방지 🔴

### 문제
`EnterReplanning()`이 호출될 때마다 `ShowThoughtBubble()`이 무조건 발화한다. 리플래닝 쿨다운은 0.3~0.5초이므로, 플래닝이 반복 실패하는 주민(자원 전멸, Deadlock 직전 등)은 **초당 2~3회 토스트를 발행**한다. 주민 70명 규모에서 토스트 큐(MAX_QUEUE_SIZE=5)는 즉시 포화되고, 정작 중요한 명령 거부 메시지가 밀려난다.

### 원인
발화 조건에 시간·상태 게이트가 없다. 설계서 7장의 의도는 "리플래닝의 *의미 있는 순간*을 대사로 반전"이지, 모든 리플래닝의 중계가 아니다.

### 수정 설계

`VillagerFSM`에 발화 게이트 3중 필터를 추가한다:

```csharp
// ── 필드 추가 ──
private const float THOUGHT_MIN_INTERVAL_SEC = 5.0f;  // 주민당 최소 발화 간격
private float _lastThoughtTime = -999f;

// ── ShowThoughtBubble 교체 ──
private void ShowThoughtBubble(string thought)
{
    // 게이트 1: 주민당 시간 스로틀
    if (Time.time - _lastThoughtTime < THOUGHT_MIN_INTERVAL_SEC) return;

    // 게이트 2: LOD 주민은 발화 생략 (화면 밖 원거리 — 정보 가치 없음)
    if (Brain.FSMState == VillagerState.LOD_FSM) return;

    // 게이트 3: 연속 동일 원인 억제 — 같은 FallbackCounter 사이클 내 재발화 금지
    //           (FallbackCounter가 0으로 리셋 = 새 사이클 = 발화 허용)
    if (Brain.FallbackCounter >= 2) return;  // 3회째 실패부터는 침묵 (Deadlock 처리에 위임)

    _lastThoughtTime = Time.time;
    AIVillage.UI.HUDManager.Instance?.ShowAIThought(gameObject.name, thought);
}
```

**설계 근거**
- 게이트 1(5초)은 기존 상수 스타일(`REFUSE_DISPLAY_SEC = 3.0f`)과 일관된 명명·배치를 따른다.
- 게이트 3은 "실패 루프의 소음"과 "적응의 서사"를 구분한다 — 첫 리플래닝은 *"나무가 없네, 다른 곳을 찾아볼게"* 라는 지능의 연출이지만, 3연속 실패의 중계는 무능의 광고다. 그 시점은 Deadlock → `NeedsHelp` 경로가 담당한다.

### 검증 기준
- [ ] 자원 노드를 전부 고갈시킨 테스트 씬에서 주민 1명의 분당 발화 ≤ 12회 → **≤ 2회**로 감소
- [ ] LOD 거리(30타일 초과) 주민의 발화 0회
- [ ] 명령 거부 메시지가 사고 대사에 밀리지 않고 정상 표시

---

## F2. 말풍선을 월드스페이스로 전환 🟠

### 문제
현재 Level 3은 `HUDManager.ShowToast("[이름] 대사")` — **화면 구석의 전역 토스트**다. 검토 결과 `RefusalBubble`도 사실은 Canvas 토스트 패널이지 머리 위 말풍선이 아니다. 즉 현재 프로젝트에는 월드스페이스 말풍선 컴포넌트가 **존재하지 않는다**.

전역 토스트의 구조적 한계:
1. **공간적 단절** — "누가" 말하는지 시선 이동이 필요하다. 플레이어는 대사와 주민을 연결하지 못하고, "AI의 속마음"이 아니라 "시스템 로그"로 인지한다.
2. **단일 채널 병목** — 동시에 1개만 표시. 마을 곳곳의 사고가 한 줄로 직렬화된다.
3. **거부 메시지와 채널 공유** — 게임플레이 정보(거부)와 연출 정보(사고)의 우선순위 충돌.

### 수정 설계 — 기존 자산의 올바른 재활용

새 인프라를 만들 필요가 없다. Phase 1에서 이미 만든 **`VillagerActionIcon`이 정확한 재활용 대상**이다: 월드스페이스 TMP, 주민 자식 자동 부착, 주기 갱신 — 말풍선에 필요한 전부를 이미 갖췄다.

**방안: `VillagerActionIcon`을 2줄 표시로 확장** (신규 클래스 없이)

```csharp
// VillagerActionIcon.cs 에 추가

private const float THOUGHT_DISPLAY_SEC = 3.0f;
private string _thoughtText;
private float  _thoughtUntil;

/// <summary>사고 대사를 이 주민 머리 위에 3초간 표시한다.</summary>
public void ShowThought(string thought)
{
    _thoughtText  = thought;
    _thoughtUntil = Time.time + THOUGHT_DISPLAY_SEC;
}

// Update() 내 텍스트 조립 변경:
private string ComposeText(VillagerBrain b)
{
    string icon = BuildIconText(b);                      // 기존 로직
    if (Time.time < _thoughtUntil && _thoughtText != null)
        return $"<size=60%>💬{_thoughtText}</size>\n{icon}";  // 말풍선 줄 + 아이콘 줄
    return icon;
}
```

**호출 경로 변경** (`VillagerFSM.ShowThoughtBubble` 내부):

```csharp
// 변경 전: HUDManager.Instance?.ShowAIThought(gameObject.name, thought);
// 변경 후:
GetComponentInChildren<AIVillage.UI.VillagerActionIcon>()?.ShowThought(thought);
```

- `GetComponentInChildren` 매 호출이 부담스러우면 FSM이 아이콘 참조를 캐시한다 (GameManager.AttachActionIcon에서 주입).
- `HUDManager.ShowAIThought()`는 **삭제하지 말고 유지** — 전멸 위기 등 마을 단위 중대 사건의 전역 공지 채널로 용도를 재정의한다 (채널 분리로 3번 문제 해소).

**선택 보강**: 대사 표시 중 텍스트가 길면 잘리므로, 15자 초과 시 두 줄 래핑 또는 폰트 60%→50% 축소.

### 왜 이 방안이 최적인가
- **신규 파일 0개, 수정 2파일** — Phase 1 커밋이 만든 자산 위에 그대로 얹힌다.
- 갱신 루프·부착 로직·생명주기를 재사용하므로 버그 표면적이 최소.
- 말풍선과 아이콘이 같은 앵커를 공유 → 시선이 자연스럽게 주민에 고정된다. **"쟤가 지금 저 생각을 하며 저 행동을 한다"가 한 프레임에 읽힌다** — Level 3의 존재 이유.

### 검증 기준
- [ ] 대사가 주민 머리 위에 3초 표시 후 자동 소멸, 아이콘은 유지
- [ ] 서로 다른 주민 3명이 동시 발화 시 각자 머리 위에 독립 표시
- [ ] 전역 토스트에는 사고 대사가 더 이상 출력되지 않음

---

## F3. 대사 랜덤 풀 🟡

### 문제
`GetReplanThought()`가 상황당 1종 고정 문자열을 반환한다. 같은 상황 반복 시 동일 대사가 반복되어 **3회 노출쯤부터 "스크립트"로 인지**된다 — 지능 연출의 자기 붕괴.

### 수정 설계

if-switch 로직은 유지하고, 반환을 문자열 → 문자열 배열 + 랜덤 선택으로 교체한다:

```csharp
private static readonly string[] THOUGHT_NO_WOOD = {
    "나무가 없네... 다른 곳을 찾아볼게.",
    "여긴 다 베었군. 이동해야겠어.",
    "벌목할 게 없어. 어디 다른 데 없나?"
};
private static readonly string[] THOUGHT_HUNGRY_REPLAN = {
    "배가 고픈데 계획이 안 잡히네...",
    "먹을 걸 먼저 찾아야 하나...",
    "허기져서 집중이 안 돼."
};
// ... 상황별 3종씩, static readonly로 GC 할당 0 유지

private static string Pick(string[] pool)
    => pool[UnityEngine.Random.Range(0, pool.Length)];

// GetReplanThought() 내: return "배가 고픈데..." → return Pick(THOUGHT_HUNGRY_REPLAN);
```

**연속 중복 방지(선택)**: 직전 인덱스를 필드에 저장하고 같은 값이 나오면 +1 순환 — 3종 풀에서 체감 반복률이 크게 떨어진다.

**구조 노트**: 향후 특성 시스템(설계서 6장) 도입 시 `(상황, 특성) → 풀` 2차원 테이블로 확장할 자리다. 지금은 상황 1차원으로 충분하며, 데이터가 코드에 박히는 것이 부담되면 `ScriptableObject` 대사 테이블로 승격한다 (Phase 3 과제로 미뤄도 무방).

### 검증 기준
- [ ] 동일 상황 10회 반복 시 최소 3종 대사가 모두 관측됨
- [ ] 연속 2회 동일 대사 미발생 (중복 방지 적용 시)

---

## F4. Goal 전환 발화 추가 🟡

### 문제
현재 발화 트리거가 **리플래닝 1곳**뿐이다. 설계서 7.1 Level 3의 3대 트리거 중 "Goal 전환"과 "위험 회피"가 빠져 있어, 주민의 *능동적 판단 순간*(가장 지능적으로 보이는 순간)이 무음이다.

### 수정 설계

`VillagerFSM.TransitionTo(VillagerState.Planning)` 직전, Goal이 **변경**되는 지점에 발화를 삽입한다. 삽입 위치는 `State_Idle()`의 Goal 확정 3곳 + P0 AnyState 전이 1곳:

```csharp
// State_Idle() 내 예시 — GatherResources 확정 직후:
if (_worldState != null && IsAnyStockLow())
{
    bool goalChanged = Brain.CurrentGoalId != "GatherResources";
    Brain.CurrentGoalId = "GatherResources";
    if (goalChanged) ShowThoughtBubble(Pick(THOUGHT_GOAL_GATHER));
    TransitionTo(VillagerState.Planning);
    return;
}
```

대사 풀 (F3 구조 재사용):

| 트리거 | Goal | 대사 예시 |
|---|---|---|
| P0 전이 | SurviveHunger | "안 되겠다, 뭐라도 먹어야 해." |
| P0 전이 | SurviveFatigue | "잠깐... 좀 쉬어야겠어." |
| Idle → 채집 | GatherResources | "창고가 비어가네. 일하러 가자." |
| Idle → 건설 | BuildStructure | "재료가 모였으니 짓기 시작하자." |
| Idle → 탐험 | Explore | "여유가 생겼으니 주변을 둘러볼까." |
| 적 감지 | Fighting 전이 | "적이다! 맞서 싸운다!" |

**중요**: `goalChanged` 체크가 핵심이다 — 같은 Goal의 재플래닝마다 발화하면 F1이 무력화된다. F1의 5초 스로틀이 2차 방어선으로 함께 작동한다.

### 검증 기준
- [ ] 배고픔 80 돌파 순간 1회 발화, 이후 동일 Goal 유지 중 재발화 없음
- [ ] Goal이 채집→탐험으로 바뀌는 순간에만 탐험 대사 발화

---

## F5. 부상 시 전투 비용 배율 🟠

### 문제
설계서 4.1의 ConditionModifier 중 피로(노동 페널티)만 구현되고 **부상(전투 페널티)이 누락**됐다. 결과: 체력 25의 주민이 만전 주민과 동일한 비용으로 `AttackEnemy`를 플랜에 넣는다. 거부 시스템이 명령은 걸러주지만, **자발적 플래닝**은 무방비 — "다친 주민이 제 발로 싸우러 가는" 어색함이 남아 있다.

### 수정 설계

`ComputeContextMultipliers()`의 피로 블록 바로 아래에 대칭 블록을 추가한다 (기존 상수 스타일 준수):

```csharp
// ── 상수 추가 ──
private const float INJURY_THRESHOLD    = 50f;  // 체력 이 값 미만부터 전투 페널티 발동
private const float INJURY_COMBAT_MAX   = 3f;   // 체력 0 근접 시 전투 비용 최대 배율

// ── ComputeContextMultipliers() 내 추가 ──
// 부상 상태: 전투 비용 상승 (체력 50 미만부터 선형, 임계 20 부근에서 ~2.5배)
if (brain.HealthLevel < INJURY_THRESHOLD)
{
    float ratio = (INJURY_THRESHOLD - brain.HealthLevel) / INJURY_THRESHOLD; // 0~1
    mult.AttackEnemy *= 1f + ratio * (INJURY_COMBAT_MAX - 1f);
}
```

**설계 판단 2가지**
1. **선형 배율 (임계 스위치가 아니라)** — 체력 49와 체력 21의 주저함이 달라야 한다. 설계서 5장(Utility 곡선 철학)의 선행 적용이다.
2. **위험 근접 배율(×0.7)과의 곱 순서는 무관** — 둘 다 곱셈이므로 교환법칙 성립. 다만 부상+위험 동시 상황에서 `2.5 × 0.7 ≈ 1.75`로 여전히 전투 비용이 오르는지 확인할 것 — 이것이 의도된 결과다 ("위험이 코앞이어도 다쳤으면 싸움을 꺼린다").

### 검증 기준
- [ ] 체력 25 주민: 적 근접 시 AttackEnemy 대신 Fleeing/회피 플랜 선택 빈도 유의미하게 증가
- [ ] 체력 100 주민: 기존과 동일 행동 (회귀 없음)

---

## F6. 액션 아이콘 카메라 빌보드 🟡

### 문제
`VillagerActionIcon`의 WorldSpace TMP가 월드 회전 고정이다. 카메라가 완전 탑다운 직교가 아니라면(현재 CameraController 구성상 기울기 존재 가능) 시야각에 따라 글자가 기울거나 안 보인다.

### 수정 설계

`Update()` 말미에 3줄 (F2와 같은 파일이므로 함께 커밋):

```csharp
// 카메라를 향해 정면 유지 (빌보드)
if (Camera.main != null)
    transform.rotation = Camera.main.transform.rotation;
```

- `Camera.main`은 매 프레임 태그 검색 비용이 있으므로, `Initialize()`에서 캐시:
  `private Camera _cam;` → `_cam = Camera.main;` → `if (_cam != null) transform.rotation = _cam.transform.rotation;`
- `LookAt`이 아니라 **회전 복사**를 쓴다 — 70개 아이콘이 전부 같은 회전값을 가지므로 시각적으로 균일하고, 벡터 연산도 없다.

### 검증 기준
- [ ] 카메라 회전/기울임 시 모든 아이콘이 항상 정면으로 판독 가능

---

## F7 (선택). 미발견 자원 → Explore 유도 🟢

### 문제
`GatherMultFromResult()`에서 해당 타입 노드가 **하나도 발견되지 않았으면 배율 1f(중립)**을 반환한다. 논리적 공백: 발견된 노드가 없으면 채집 액션은 실행 단계에서 어차피 실패한다. 중립 배율은 실패할 플랜을 플래너가 정상 비용으로 선택하게 놔두는 것이다.

### 수정 설계

두 줄 수정으로 "모르면 찾으러 간다" 행동을 만든다:

```csharp
private static float GatherMultFromResult(bool anyDiscovered, bool anyAvailable, float bestDist)
{
    if (!anyDiscovered) return FULL_NODE_PENALTY * 2f;  // 미발견: 포화보다 더 큰 페널티 (10f)
    if (!anyAvailable)  return FULL_NODE_PENALTY;
    return 1f + UnityEngine.Mathf.Min(bestDist / DISTANCE_SCALE, 1f) * DISTANCE_WEIGHT;
}
```

추가로 미발견 자원이 존재할 때 Explore 비용을 낮춘다 (`ComputeContextMultipliers` 말미):

```csharp
// 미발견 자원 타입이 하나라도 있으면 탐험 선호 (FoW 개척 유도)
bool anyUndiscovered = /* ComputeAllGatherMults에서 out 플래그로 전달 */;
if (anyUndiscovered && !brain.NearEnemy)
    mult.Explore *= 0.6f;
```

**효과**: 게임 초반 FoW가 짙을 때 주민들이 자연스럽게 탐험에 배분되고, 노드 발견과 함께 채집으로 전환하는 **개척기 리듬**이 창발한다. 설계서 4.2의 "FoW 미탐험 페널티"를 최소 비용으로 선반영하는 셈이다.

### 검증 기준
- [ ] 신규 게임 시작 직후(발견 노드 0) 주민 다수가 Explore 플랜 선택
- [ ] 첫 목재 노드 발견 후 ChopWood 플랜으로 자연 전환

---

## 마감 체크리스트 (F1~F7 완료 후)

| 영역 | 완료 전 | 완료 후 목표 |
|---|---|---|
| Level 1 아이콘 | 95% | 100% (F6) |
| Level 2 사고 체인 | 90% | 90% (Phase 2 수치형 GOAP 대기 — 수정 불요) |
| Level 3 말풍선 | 50% | **95%** (F1+F2+F3+F4) |
| 컨텍스트 비용 | 80% | **90%+** (F5, +F7 시 95%) |
| **Phase 1 종합** | **~78%** | **~93%** |

남는 미완(경로 기반 위험 배율)은 JPS 경로 질의 비용 검토가 필요하므로 **Phase 2와 함께** 설계하는 것이 옳다 — 수치형 GOAP 전환 시 스케줄러를 어차피 다시 열게 되기 때문이다.

## 커밋 분할 제안

```
commit 1: fix(ui): thought bubble throttle + LOD skip          (F1)
commit 2: feat(goap): injury combat cost multiplier            (F5)
commit 3: feat(ui): world-space thought bubbles on ActionIcon  (F2)
commit 4: feat(ui): thought dialogue pools + goal transition   (F3+F4)
commit 5: fix(ui): action icon camera billboard                (F6)
commit 6: feat(goap): undiscovered-resource explore incentive  (F7)
```

F1·F5를 선행 독립 커밋으로 분리하면, F2~F4에서 문제가 생겨도 안정성 수정(F1)과 AI 품질 수정(F5)은 이미 안전하게 반영된 상태가 된다.
