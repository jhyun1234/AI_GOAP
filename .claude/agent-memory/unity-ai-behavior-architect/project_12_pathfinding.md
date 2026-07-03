---
name: project-12-pathfinding
description: 12단계 경로탐색 시스템 설계 확정 — JPS(주민)+FlowField(적) 하이브리드, 이동 속도, 신규 파일 목록
metadata:
  type: project
---

# 12단계 경로탐색 시스템 설계 확정 (2026-06-29)

**Why:** 순간이동 제거 + 실제 타일 이동 구현. 주민 JPS, 적 Flow Field 하이브리드.
**How to apply:** 구현 지시 또는 코드리뷰 시 이 결정을 기준으로 판단.

## 핵심 결정 사항

### JPS 그리드
- bool[,] walkable 100×100, 전체 true 초기화 (장애물 없음)
- 타일 좌표 변환: arrayX = tileX + 50 (OFFSET = MAP_SIZE/2)
- static class JPSPathfinder — MonoBehaviour 아님
- FindPath(start, goal, maxRadius=60) → null=경로없음, 빈List=이동불필요(start==goal)
- Debug.Log 금지 (핫 경로)

### Flow Field
- Vector2Int[,] _flowField 100×100 (방향 벡터 per 타일)
- FlowFieldManager.cs — MonoBehaviour 싱글턴, ExecutionOrder -75
- Dijkstra BFS, 목표 타일(0,0)부터 역방향 전파
- 게임 시작 1회 빌드 → 레이드 재사용 (목표 불변이므로)
- RebuildIfGoalChanged(): 목표 변경 시에만 재빌드
- Sample(): O(1), _isBuilt=false 시 zero + 경고 1회

### VillagerFSM 추가 필드
- List<Vector2Int> _currentPath, int _pathIndex, bool _isMoving
- VILLAGER_MOVE_SPEED = 2.0f 타일/초
- FLEE_SPEED_MULTIPLIER = 1.5f (Fleeing 배율 → 3.0 타일/초)
- TILE_ARRIVE_THRESHOLD = 0.05f (도착 판정 거리)

### 이동 실행 방식
- Update()에서 MoveAlongPath() → Vector3.MoveTowards() (방식 A 채택)
- 코루틴 방식 불채택 (FSM 상태 중단 처리 복잡도 높음)
- 타일 도착 시 Brain.TileX/Y 즉시 갱신 (SensorSystem 정확도 유지)

### 경로 중단 필수 호출처
- OnStateExit(Executing) → AbortCurrentPath()
- OnStateExit(Fleeing) → AbortCurrentPath()
- OnStateEnter(Fighting) → AbortCurrentPath() [간접: EnterFighting 내]
- OnStateEnter(Dead) → AbortCurrentPath()

### EnemyFSM 수정
- _isMoving, _nextTileX/Y 필드 추가
- ENEMY_MOVE_SPEED = 1.5f 타일/초
- State_Moving() 교체: 더미 3초 → FlowField Sample() + _isMoving = true
- Retreating OnStateEnter: Brain.TileX/Y 기지 즉시 스냅 유지 (경로 없음)

### Script Execution Order 추가
FlowFieldManager: -75 (GameManager -80 이후, FactionAI -70 이전)

### 신규 파일 2개
- Assets/Scripts/Core/JPSPathfinder.cs (static class)
- Assets/Scripts/Core/FlowFieldManager.cs (MonoBehaviour 싱글턴)

### 이동 속도 확정값
- 주민 일반: 2.0 타일/초
- 주민 Fleeing: 3.0 타일/초 (1.5× 배율)
- 적 유닛: 1.5 타일/초
- LOD 주민: 더미 시뮬레이션 유지 (변경 없음)

## 주요 리스크 (코드리뷰 체크 포인트)
1. _isMoving 중단이 모든 AnyState 전이에서 호출되는가
2. Brain.TileX/Y가 타일 도착 즉시 갱신되는가
3. JPSPathfinder와 FlowFieldManager의 OFFSET 값이 동일(50)한가
4. State_Fleeing()에서 경로 재계산 루프 방지: _currentPath==null 조건 체크
5. ResourceNode TryOccupy()가 DetermineTargetTile()에서 즉시 호출됨을 확인
