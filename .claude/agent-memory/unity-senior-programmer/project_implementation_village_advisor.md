---
name: project_implementation_village_advisor
description: VillageAdvisor.cs 구현 완료 — 자율 건물 결정 시스템, 6우선순위 규칙, ExecutionOrder -65
metadata:
  type: project
---

## VillageAdvisor 구현 완료 (2026-06-28)

**파일**: `Assets/Scripts/Core/VillageAdvisor.cs`
**네임스페이스**: `AIVillage.Core`
**실행 순서**: `[DefaultExecutionOrder(-65)]`

### 핵심 설계 결정

- MonoBehaviour 코루틴 기반 주기 평가 (`_evaluationInterval = 5f`)
- `WaitForSeconds`를 `Start()`에서 1회 생성 후 재사용 (GC 할당 방지)
- LINQ 사용 금지 — `CountAliveVillagers()`는 인덱스 기반 `for` 루프 사용
- 한 번의 평가 사이클에 최대 1개 건물만 큐잉 후 즉시 `return`

### 6단계 우선순위 규칙 (기획서 수치)

| 순위 | 건물 | 조건 |
|---|---|---|
| 1 | Campfire | !CampfireBuilt && aliveVillagers >= 1 |
| 2 | House | !HouseBuilt && aliveVillagers >= 3 |
| 3 | Storehouse | !StorehouseBuilt && 총자원 >= 80f |
| 4 | Forge | !ForgeBuilt && GameTime >= 5f && IronStock > 0 |
| 5 | Watchtower | !WatchtowerBuilt && RaidCount >= 1 |
| 6 | TownHall | !TownHallBuilt && GameTime >= 15f |

### GetTotalResourceStock 집계 대상

Wood + Stone + Iron + Copper + Silver (식량 제외 — 소비재이므로)

### 씬 배치

- `_Managers` 하위 빈 GameObject "VillageAdvisor"에 컴포넌트 부착
- Inspector 연결 불필요 — GameManager가 FindObjectOfType으로 자동 수집
- BuildingQueue.Instance null 방어 포함 (BuildingQueue 미배치 시 경고만 출력)

**Why:** FactionAI(-70) 패턴을 따르는 자율 마을 관리 시스템으로 플레이어 입력 없이 마을이 성장하도록 설계.
**How to apply:** 향후 우선순위 규칙 추가 시 `EvaluateBuildingNeeds()` 내 if 블록 추가 후 `return` 패턴 유지.
