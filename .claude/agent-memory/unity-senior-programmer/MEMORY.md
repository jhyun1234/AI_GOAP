# Memory Index

## 프로젝트 구현 상태
- [1단계 구현 완료 — Core 레이어](project_implementation_phase1.md) — 네임스페이스, 파일별 인터페이스, GameManager 초기화 순서
- [2단계 구현 완료 — AI 레이어](project_implementation_phase2.md) — VillagerFSM, Brain, Enums, ConflictScoreCalculator, 더미 항목 목록
- [3단계 구현 완료 — MessageBus](project_implementation_phase3.md) — MessageBus 신규 생성, VillagerFSM 3개 구간 수정, GameManager 연동 방법
- [4단계 구현 완료 — ActionDatabase + BuildingQueue](project_implementation_phase4.md) — 신규 3파일, VillagerFSM 더미 5개 교체, InjectDependencies 시그니처 변경
- [5A단계 구현 완료 — SensorSystem](project_implementation_phase5a.md) — ResourceNode, SensorSystem 신규, VillagerFSM Brain 프로퍼티 추가, 환경 플래그 실제 갱신
- [5B단계 구현 완료 — GOAPPlannerJob](project_implementation_phase5b.md) — 신규 4파일(GOAP/), VillagerFSM SimulatePlanResult 제거, Burst A* 플래너 연동
- [6A단계 구현 완료 — GameManager](project_implementation_phase6a.md) — 중앙 초기화/틱 관리자, Awake 6단계, 기본 노드 7개, 틱 코루틴, MessageBus 핸들러
- [6B단계 구현 완료 — FactionAI](project_implementation_phase6b.md) — EnemyBrain/EnemyFSM/FactionAI 신규 3파일, GameManager CalculatePlayerStrength 추가, WatchtowerBuilt 추가
- [8단계 구현 완료 — 전투+정신상태](project_implementation_phase8.md) — Fighting/Fleeing 상태, CombatMentalState, RageKill 전파, EnemyFSM 실제 피해 처리, GameManager Villagers/Enemies 프로퍼티
- [VillageAdvisor 구현 완료](project_implementation_village_advisor.md) — 자율 건물 결정 시스템, 6우선순위 규칙, ExecutionOrder -65, 코루틴 GC 최적화
- [9+10단계 구현 완료 — 플레이어 입력+UI](project_implementation_phase9.md) — UI 8파일 신규, VillagerFSM SphereCollider 추가, GameManager OnOrderRefusedEvent, 빌드 비용 테이블
- [11단계 구현 완료 — Win/Lose 조건 시스템](project_implementation_phase11.md) — GameResultType enum, OnGameResultEvent, CheckWinLoseConditions, GameResultPanel 신규, WorldState 필드 2개 추가
- [13단계 구현 완료 — 타일 맵 렌더링+FoW](project_implementation_phase13.md) — MapConfig/FowManager/MapChunkRenderer 신규 3파일, JPSPathfinder/FlowFieldManager/VillagerFSM/GameManager 수정 4파일
- [15단계 구현 완료 — 주민 모집 시스템](project_implementation_phase15.md) — VillagerRecruitData/RecruitmentSystem/RecruitmentPanel 신규 3파일, VillagerBrain/GameManager/HUDManager 수정 3파일
