---
name: design-goap-communication
description: GOAP 멀티에이전트 통신 아키텍처 — MessageBus Pub-Sub 패턴, DangerRegistry 설계
metadata:
  type: feedback
---

# GOAP 통신 아키텍처 패턴

## 핵심 원칙: 직접 통신 금지

AI 간 직접 통신 금지. 오직 MessageBus 또는 공유 레지스트리(ResourceRegistry, DangerRegistry)를 통해서만 정보 교환.

**Why:** TechSpec RISK-003 (멀티 에이전트 동시 접근 → Race Condition).
**How to apply:** 새 AI 행동 설계 시 "이 정보를 어떻게 전달하는가?" 질문에 항상 MessageBus/Registry 답변.

## MessageBus 설계 규칙

1. 발행자는 구독자를 알지 못한다 (Pub-Sub 분리).
2. 우선순위 큐: High → Medium → Low 순서.
3. 같은 Tick 내 동일 타입 메시지는 1개로 병합 (EnemyDetected 폭주 방지).
4. 메시지는 처리 완료 후 즉시 큐에서 제거 (영속 저장 없음).
5. 복수 OrderIssued (같은 주민): 최신 1개만 처리, 이전 폐기.

## DangerRegistry 만료 규칙

- Enemy 유형: 적 이탈 시 즉시 만료 OR 등록 후 30초 경과
- NaturalDisaster: 이벤트 종료 시 만료
- TerritoryConflict: RaidDecision 취소 시 만료

## ResourceRegistry 핵심 규칙

- 가용량 = stock - reserved (항상 이 공식으로 계산)
- Reserve(): 플래닝 시 즉시 예약
- Commit(): Executing에서 Action 완료 시 stock 차감
- Release(): Replanning, Dead, LOD 복귀 시 예약 해제
- ReleaseAll(agentId): Dead 상태 진입 시 즉시 호출 (사망 처리 1순위)

## 검증된 메시지 충돌 처리

EnemyDetected(High) vs ResourceDiscovered(Medium) 동시 수신:
→ EnemyDetected 먼저 처리, ResourceDiscovered는 큐 대기

OrderIssued(Medium) vs P0 Goal 동시:
→ P0 Goal 우선, OrderIssued는 P0 완료 후 처리

**Why:** AI Village 설계에서 충돌 시나리오 5개를 명시적으로 정의하며 도출한 패턴.
**How to apply:** 새 메시지 타입 추가 시 기존 High 타입과의 충돌 시나리오를 반드시 명시.
