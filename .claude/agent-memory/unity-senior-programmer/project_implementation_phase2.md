---
name: project-implementation-phase2
description: 2단계 구현 완료 — AI 레이어 VillagerFSM, Brain, Enums, ConflictScoreCalculator
metadata:
  type: project
---

2단계 구현 완료 (2026-06-25). `AIVillage.AI` 네임스페이스 5개 파일을 `Assets/Scripts/AI/`에 생성.

**Why:** Core 레이어([[project-implementation-phase1]]) 위에 주민 FSM 생명주기 관리자 구현. GOAP 플래너 자체는 3단계에서 Job System과 함께 구현.

**How to apply:** 3단계 작업 시 아래 TODO 항목들을 처리할 것.

## 생성된 파일
- `IAutonomousAgent.cs` — 에이전트 공통 인터페이스
- `VillagerEnums.cs` — FSM 상태, 메시지, 명령, 결과 타입 열거형 및 구조체
- `VillagerBrain.cs` — 주민 런타임 상태 데이터 클래스 (순수 C#)
- `VillagerFSM.cs` — 핵심 FSM MonoBehaviour, IAutonomousAgent 구현
- `ConflictScoreCalculator.cs` — ConflictScore 계산 정적 유틸리티

## 아키텍처 결정사항
- VillagerFSM.Tick()은 외부(GameManager)에서 0.1초 간격으로 그룹별 호출 (tickGroupIndex 0~5)
- AnyState 전이(Dead, P0)는 Update()에서 매 프레임 체크 — Tick 지연 없이 즉시 반응
- LOD 모드: 30타일 초과 + 비전투 시 0.5초 간격 경량 시뮬레이션
- ResourceRegistry는 InjectDependencies()로 GameManager가 주입 — Script Execution Order 문제 회피
- ConflictScore 공식: Σ(urgency × orderImpact) >= Threshold(2.5 × loyalty/50) → 거부

## 2단계 더미(Stub) 항목 (3단계에서 교체)
- `SimulatePlanResult()` — 실제 GOAPPlannerJob으로 교체
- `TryReserveForAction()` — ActionDatabase 자원 소비 테이블로 교체
- `ApplyActionEffect()` — ActionDatabase 효과 테이블로 교체
- `GetDistanceToBase()` — 맨해튼 거리 → 실제 Pathfinding 거리로 교체
- `WorldStateSnapshot.CreateFrom()` 호출 — Planning 진입 시 호출하지만 Job 미연결
- MessageBus — 모든 발행 지점에 Debug.Log 대체 주석 `// TODO: MessageBus.Publish(...)` 표기

## TODO (기획팀 확인 필요)
- 주민 초기 생존 수치 (현재: HP=80, HG=30, FT=20, Mood=70, Loyalty=55)
- 각 Action의 정확한 자원 소비량 및 수치 효과
- 건물별 자원 요구량 테이블
- GatherIron, GatherCopper Action ID 확정
- unexploredTilesNearby 플래그 관리 주체 (Brain vs 별도 SensorSystem)
- 팩션 ID 할당 방식

## GameManager 연동 시 필요한 호출
```csharp
// GameManager.Awake() 또는 Start()에서:
foreach (var fsm in FindObjectsOfType<VillagerFSM>())
    fsm.InjectDependencies(resourceRegistry);

// GameManager.Update() 또는 0.1초 코루틴에서:
int group = Time.frameCount % 6;
foreach (var fsm in _allVillagers)
    if (fsm.TickGroupIndex == group) fsm.Tick();
```
