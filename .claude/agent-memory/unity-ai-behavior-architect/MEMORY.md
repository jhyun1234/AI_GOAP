# Memory Index

## 프로젝트
- [14단계 계절 시스템 설계](project_14_season_system.md) — SeasonManager(-75), GOAP seasonModifier 파라미터, WinterCrisis 임계값 공식
- [AI Village 아키텍처 설계 결정](project_architecture_decisions.md) — FSM 8상태 래퍼 구조, LOD 임계값, 메시지 우선순위
- [6B FactionAI 설계 확정](project_6b_faction_ai.md) — EnemyBrain/FSM/FactionAI 구조, Execution Order, GameManager 연동 수정 목록
- [9+10단계 UI+플레이어 입력 설계](project_9_10_ui_player_input.md) — Execution Order, 갱신 전략, SphereCollider 자동 추가, 건설 위치 자동 배치, RefusalBubble 토스트 방식
- [12단계 경로탐색 시스템 설계](project_12_pathfinding.md) — JPS(주민)+FlowField(적) 하이브리드, 이동속도, 신규파일 2개, 리스크 5개
- [13단계 타일맵+FoW 설계](project_13_tilemap_fow.md) — MapConfig ScriptableObject 중앙화, ChunkRenderer(-60), FowManager(-55), 결정 항목 A/D 대기

## 설계 패턴
- [FSM 안티패턴 및 검증 규칙](design_fsm_antipatterns.md) — DeadState 탐지, AnyState 폴백, P0 즉시 전이
- [GOAP 통신 아키텍처 패턴](design_goap_communication.md) — MessageBus Pub-Sub, DangerRegistry 만료 규칙, 직접 통신 금지
- [우선순위 계층 템플릿](design_priority_template.md) — P0 서브 우선순위, ConflictScore 공식, Cost Modifier 구간
