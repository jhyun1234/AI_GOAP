# Memory Index

## 프로젝트
- [AI Village 아키텍처 설계 결정](project_architecture_decisions.md) — FSM 8상태 래퍼 구조, LOD 임계값, 메시지 우선순위

## 설계 패턴
- [FSM 안티패턴 및 검증 규칙](design_fsm_antipatterns.md) — DeadState 탐지, AnyState 폴백, P0 즉시 전이
- [GOAP 통신 아키텍처 패턴](design_goap_communication.md) — MessageBus Pub-Sub, DangerRegistry 만료 규칙, 직접 통신 금지
- [우선순위 계층 템플릿](design_priority_template.md) — P0 서브 우선순위, ConflictScore 공식, Cost Modifier 구간
