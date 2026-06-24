---
name: design-fsm-antipatterns
description: GOAP FSM 설계 시 발견된 안티패턴과 검증 규칙 — AI Village 설계 경험 기반
metadata:
  type: feedback
---

# FSM 안티패턴 및 검증 규칙

## 안티패턴 1: DeadState (막힌 상태)

**문제:** 탈출 경로가 없는 상태 → AI가 영구 정체.
**검증:** 모든 상태에 탈출 조건 목록이 존재해야 함. 단, 최종 흡수 상태(Dead)는 의도적 예외.
**AI Village 적용:** Dead 상태만 탈출 경로 없음. 나머지 7개 상태 모두 탈출 경로 보유.

## 안티패턴 2: AnyState 전이 누락

**문제:** P0 Goal 또는 사망 처리가 특정 상태에서만 발동되면 AI가 위험 상황에서 응답 못함.
**검증 규칙:**
  - AnyState → Dead (isAlive == false)
  - AnyState → Planning P0 (healthLevel < 20 등, 쿨다운 무시)
**AI Village 적용:** 모든 상태 탈출 조건 1순위에 isAlive == false → Dead 포함.

## 안티패턴 3: P0 Goal Thrashing

**문제:** P0 3개 동시 발동 + 자원 없음 → NoSolutionFound 루프.
**방어 설계:**
  - 서브 우선순위 고정: SurviveInjury > SurviveHunger > SurviveFatigue
  - Fallback 즉시 적용: hunger < 50 → RestOnGround, 그 외 → MoveToBase
  - 3회 Deadlock → "도움 필요" UI 플래그

## 안티패턴 4: LOD 상태에서 자원 처리 미완료 복귀

**문제:** LOD_GatheringResource 중 Full GOAP 복귀 시 stock 갱신 불일치.
**방어 설계:** LOD 자원 수집은 LOD_MovingToBase 도달 후에만 stock 갱신. 복귀 시 ReleaseAll().

## 검증 체크리스트 (설계 완료 시 반드시 확인)
- [ ] 모든 상태에 탈출 경로 존재 (Dead 제외)
- [ ] AnyState → Dead 폴백 전이 존재
- [ ] AnyState → P0 즉시 전이 존재
- [ ] P0 서브 우선순위 고정
- [ ] Fallback 3회 후 UI 플래그 설정

**Why:** AI Village 설계 중 발견한 실제 엣지케이스. P0 Thrashing은 TechSpec RISK-006과 직결.
**How to apply:** 새 FSM 설계 시 자기 검증 체크리스트에 위 항목 포함.
