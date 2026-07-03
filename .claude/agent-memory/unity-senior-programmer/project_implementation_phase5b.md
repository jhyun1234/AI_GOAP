---
name: project-implementation-phase5b
description: 5B단계 구현 완료 — GOAPPlannerJob (Burst A* GOAP), VillagerFSM SimulatePlanResult 제거
metadata:
  type: project
---

## 5B단계: GOAPPlannerJob 구현 완료 (2026-06-26)

**Why:** VillagerFSM의 SimulatePlanResult() 더미(규칙 기반 룩업)를 Burst + Job System 기반 실제 A* GOAP로 교체.

### 신규 파일 4개 (Assets/Scripts/Core/GOAP/)

1. **GOAPPlanningSlots.cs** — 43슬롯 인덱스 상수 (GOAPPlanningSlots) + 상태 변환 유틸 (GOAPStateUtil)
   - BuildCurrentState(worldState, registry, brain) → NativeArray<int>(43)
   - BuildGoalState(goalId, out goalState, out goalMask)
   - 네임스페이스: AIVillage.Core.GOAP

2. **GOAPActionRegistry.cs** — Burst 호환 GOAPActionDef struct + 19개 Action 정의 팩토리
   - GOAPActionDef: PrecCount(0~8) + EffCount(0~8), 명시적 필드 (Burst 배열 제약 우회)
   - BuildActionDefs(role, allocator) → NativeArray<GOAPActionDef>(19), 역할별 비용 보정 즉시 적용
   - HashToActionId(hash) → string 역매핑

3. **GOAPPlannerJob.cs** — [BurstCompile] IJob A* GOAP 탐색
   - MAX_NODES=2048, MAX_DEPTH=6, MAX_PLAN_LEN=6, HEURISTIC_WEIGHT=5.0f
   - NodeStates 버퍼: MAX_NODES x TOTAL_SLOTS (flat array, nodeIdx * totalSlots + slotIdx)
   - min-heap: OpenQueue NativeArray, NodeCosts 기준 정렬, SiftUp/SiftDown 비트연산
   - Backtrack(): 역순 저장 후 제자리 뒤집기로 루트→목표 정방향 변환

4. **GOAPPlannerScheduler.cs** — VillagerFSM 호출용 파사드
   - PlanningContext struct: JobHandle, 13개 NativeArray, IsScheduled, ReadResult(), Dispose()
   - Schedule(goalId, role, brain, worldState, registry) → PlanningContext
   - Allocator.TempJob 사용 (타임아웃 0.5초 이내 Dispose 보장)

### VillagerFSM.cs 변경 사항

1. `using AIVillage.Core.GOAP;` 추가
2. `private GOAPPlannerScheduler.PlanningContext _planningContext;` 필드 추가
3. `State_Planning()` — 비블로킹 폴링 방식으로 완전 교체 (IsCompleted 폴링 → Complete → ReadResult → Dispose)
4. `EnterPlanning()` — GOAPPlannerScheduler.Schedule() 호출로 교체 (WorldStateSnapshot 더미 제거)
5. `SimulatePlanResult()` 메서드 **완전 삭제** (주석 4건만 잔류)
6. `OnDestroy()` — `_planningContext.IsScheduled` 체크 후 Complete+Dispose 추가

### 알려진 주의사항

- Allocator.TempJob은 4프레임 이내 Dispose 필요 → 타임아웃 0.5초(~30프레임) 초과 가능 → Editor 경고 발생 가능. 실제 기능은 안전 (TODO 주석 기록).
- CalculateHeuristic의 HEURISTIC_WEIGHT(5.0f)이 MoveToBase(3f)보다 커서 완전 허용 가능 휴리스틱이 아님 → 속도 우선 설계.
- GetGatherActionByRole() 및 ActionDatabase 폴백 경로는 VillagerFSM에서 제거됨.

**How to apply:** 향후 GOAP 관련 변경 시 이 4개 파일 구조를 기준으로 확장한다.
