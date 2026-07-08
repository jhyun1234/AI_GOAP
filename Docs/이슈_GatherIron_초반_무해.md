# 이슈: 초반 GatherIron Goal이 3연속 NoSolutionFound로 Deadlock

**발견일**: 2026-07-09
**발견 방법**: 방향 ② M3 커밋(6cec017) 직후 사용자 방치 테스트
**심각도**: 🟠 (초반 몇 초 동안 3명 주민이 Deadlock → MoveToBase 폴백. 게임 붕괴는 아님)
**상태**: **P1-A 진단 완료 (2026-07-09)** — 근본원인 확정, 방향 ③ 명세 대기

---

## 관측 로그 (원본)

```
[VillagerFSM] Deadlock 감지! FallbackCounter=3, ByReason=[Plan=3,Res=0,Path=0,Pre=0].
NeedsHelp=true. FallbackGoal=MoveToBase. AgentId=458ce707-...
[VillagerFSM] Deadlock 감지! FallbackCounter=3, ByReason=[Plan=3,Res=0,Path=0,Pre=0].
NeedsHelp=true. FallbackGoal=MoveToBase. AgentId=80848c28-...
[VillagerFSM] Deadlock 감지! FallbackCounter=3, ByReason=[Plan=3,Res=0,Path=0,Pre=0].
NeedsHelp=true. FallbackGoal=MoveToBase. AgentId=3a3419e1-...
[VillagerFSM] Planning 실패 (NoSolutionFound). Goal=GatherIron. AgentId=458ce707-...
[VillagerFSM] Planning 실패 (NoSolutionFound). Goal=GatherIron. AgentId=80848c28-...
[VillagerFSM] Planning 실패 (NoSolutionFound). Goal=GatherIron. AgentId=3a3419e1-...
```

## 확정 사실

1. **원인 분포**: `ByReason=[Plan=3, Res=0, Path=0, Pre=0]` — 3회 실패 전부 `PlanFailed`.
   즉 이동 실패도, 자원 예약 실패도, 액션 전제 무효화도 아니고 **GOAP 자체가 해결 불가** 판정.
2. **원인 Goal 특정**: 세 주민 모두 `Goal=GatherIron`.
3. **방향 ② 결함 C 무관**: `Path=0` — JPS Unreachable은 이 시나리오에서 발생 안 함.
   방향 ② M1~M3의 이동 실패 승격 계약과 별개의 문제.

## 진단 가설 (검증 필요)

### 가설 A — IronOre 자원 노드 초기 미발견
- 초반 몇 초 동안 SensorSystem.DiscoverArea가 IronOre 노드를 아직 발견 못 함.
- `NearIronOre=false` + `NearDiscoveredResource=false` 상태.
- GatherIron 액션 체인에 "Explore → MineIron" 경로가 없거나 Explore의 Effect가 `NearIronOre=true`를 만들지 못함.
- 결과: Precondition 만족 액션이 하나도 없어 무해.

### 가설 B — GoalArbiter가 초반에 GatherIron을 잘못 선택
- Phase 2에서 GoalArbiter 도입 예정이었지만 아직 미구현 (Memory: N4 GoalArbiter 대기).
- 현재는 P0/P1/P2/P3 순차 평가 방식.
- 초반 자원 재고 상태에서 GatherIron이 P2로 자동 선택되는 조건 확인 필요.
- Iron 재고가 0인 게임 초기에는 GatherIron이 selected → 무해 → Deadlock 반복.

### 가설 C — 액션 등록 누락
- MineIron 액션의 Effect가 실제로 Iron 재고를 증가시키는지, 그리고 GatherIron Goal의 goalState가 그 Effect를 소비하는지 상수 정합성 확인 필요.
- ADR-1(수치 단일 출처) 위반 여부 점검.

## 진단 순서 (방향 ② 완료 후)

1. **CLAUDE.md ADR-3 진단 순서 그대로 적용**:
   - ① NodesExpanded 계측 로그 확인 (GatherIron Planning 시)
   - ② Profiler Planner Job 시간 대조
   - ③ 무해 Goal 여부 판단
2. **GatherIron 액션 체인 손으로 추적**: 초기 상태에서 goalState까지 도달 가능한 액션 시퀀스가 존재하는가?
3. **GoalArbiter 도입 시점 재검토**: N4 GoalArbiter가 이런 무해 Goal을 사전에 걸러내야 하는지 확정.

## 지금 안 하는 것 (스코프 가드)

- ❌ GatherIron Goal 우선순위 임의 조정 (증상 감춤)
- ❌ MAX_NODES 인상 (ADR-3 위반)
- ❌ NoSolutionFound 무시 (버그 은닉)
- ❌ 방향 ② M4~M6 중단 (이 이슈는 이동 실패 승격과 병렬)

## 재미 관점 영향

플레이어 관점에서 이 경고 자체는 화면에 안 뜨지만, 초반 3명이 몇 초간 GatherIron 시도 → Deadlock → MoveToBase 폴백 → 다시 시도 사이클이 반복되면 "마을이 초반에 굼뜨다"는 인상을 줄 수 있음. 근본 해결 우선순위는 방향 ② 완료 후 F-A 진입 전이 안전.

## 관련 문서

- `Docs/방향2_이동실패_승격_명세서.md` (M3에서 이 로그를 관측 가능하게 만듦)
- `project_planning_deadlock_diagnosis.md` (memory) — 5개 층 이중 진리 근본 진단
- CLAUDE.md ADR-3 (NoSolutionFound 진단 순서)

---

## 🔬 P1-A 진단 결과 (2026-07-09)

### 관측 로그 (GOAP_PERF_LOG 활성 재현)

```
[GOAP Perf] Agent=a5c9af49-... Goal=GatherIron Nodes=4096 Result=NoSolution
[VillagerFSM] Planning 실패 (NoSolutionFound). Goal=GatherIron.
```

**`Nodes=4096`** — MAX_NODES 완전 소진. 이론상 최적 플랜 `Explore + MineIron × 3` = 4스텝인데 4096 노드를 다 써도 도달 못함.

### 정적 가설 3종 판정

| 가설 | 결과 |
|---|---|
| **C** MineIron 등록(ADR-8) | 위반 없음 — Def+names[2]+Debug.Assert 3종 정합 |
| **B** 임계값(ADR-7) T_RARE vs GatherIron_IronStock | 위반 없음 — 15==15 |
| **A** Explore→MineIron 체인 | 존재 — 이론상 4스텝 solvable |

**→ 정적 정합성 위반 없음. 새 원인 카테고리.**

### 근본 원인 확정 (LAYER: 컨텍스트 배율 × 휴리스틱 상호작용)

1. **컨텍스트 배율 폭발** (`GOAPPlannerScheduler.cs:649`):
   - IronOre 미발견 시 `mult.MineIron = FULL_NODE_PENALTY × 2 = 10`
   - MineIron 실효비용 = `15 × 10 = 150`
   - 최적 플랜 총비용 = Explore(9) + MineIron×3(450) = **459**

2. **휴리스틱 과소평가** (`GOAPPlannerJob.CalculateHeuristic()`):
   - `h(root) = 3 스텝 × minActionCost(3) × 0.99 = 8.91`
   - 실제 459 대비 **약 50× 과소평가** → admissible이지만 정보량 부족.

3. **A* 실패 메커니즘**:
   - 낮은 h로 인해 값싼 무관 액션(`MoveToBase=3, RestOnGround≈2.4, CraftPrimitiveWeapon=6`)이 우선순위 큐 상단 독점.
   - MineIron 확장하는 순간 f-cost가 150+ 로 뛰어 큐 밑바닥으로 밀림.
   - 22 액션 × 유효 브랜치 팩터 ~10 × 깊이 4 = 이론상 ~10,000 상태 → 4096 예산 초과.

### 왜 GatherWood는 성공?

- ChopWood 실효비용 = 15×(1~5) = 15~75 → MineIron 대비 최대 10× 저렴.
- A*가 몇 노드 만에 도달.

### 대응 방향 (사용자 확정 2026-07-09)

**A안 + D안 병행** (권장 선택):
- **A**: `GatherGoalSelector`에 discovered 인자 추가 → 미발견 자원은 후보 제외
- **D**: T17 EditMode 게이트 신설 → "무해 by context penalty" 회귀 자동 방지

**대응 위치**: 방향 ③ 명세서 (`Docs/방향3_무해_문맥배율_명세서.md`) W/F/P/N 분할 예정.

### 잔여 리스크

- 배율 폭발은 Copper·미래 자원 추가 시에도 반복될 수 있음 → GatherGoalSelector 확장은 자원 타입 무관 인터페이스로 설계 필요.
- 휴리스틱 정보량 부족(원인 2)은 이 이슈 범위 밖 — 플래너 코어 동결 상태이므로 손대지 않음. 배율 상한 우회로 대응.
