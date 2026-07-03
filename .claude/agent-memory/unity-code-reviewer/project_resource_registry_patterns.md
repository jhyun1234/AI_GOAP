---
name: project-resource-registry-patterns
description: AI Village ResourceRegistry 설계 결정 및 리뷰 시 주의해야 할 아키텍처 규칙
metadata:
  type: project
---

# AI Village — ResourceRegistry & WorldState 아키텍처 규칙

**Why:** TechSpec + 기획서에서 확정된 예약 시스템. Race condition 방지가 핵심 설계 목표.

**How to apply:** ResourceRegistry, AuthoritativeWorldState, WorldStateSnapshot 관련 PR 리뷰 시 아래 규칙을 기준으로 판단.

## 예약 시스템 핵심 규칙
- 가용 수량 = stock - reserved
- Reserve: 가용량 충분 시 reserved += amount, 부족 시 false 반환
- Commit: stock -= amount + reserved -= amount (Release 호출로 처리)
- Release: reserved만 -= amount (stock 유지)
- ReleaseAll: 해당 agentId의 모든 reserved 해제 (주민 사망 시)
- stock 음수 방지: Mathf.Max(0f, value) 클램프 필수

## 두 상태 저장소 동기화 문제 (반복 발생 위험)
- ResourceRegistry는 _totalReserved (내부 캐시)와 AuthoritativeWorldState.SetReserved (외부 상태) 두 곳에 동시 쓰기함
- 이 이중 쓰기 구조는 설계상 허용되지만, ValidateIntegrity가 _worldState.GetReserved()를 검증하지 않으면 불일치 탐지 불가
- 현재 ValidateIntegrity는 _totalReserved만 검증하고 _worldState.Reserved는 별도 검증 안 함

## WorldStateSnapshot 수명 주의
- Allocator.TempJob은 Unity에서 4프레임 이내에 Dispose 필수
- Job 완료 후 즉시 Dispose 해야 함. 장기 보관 금지.
- WorldStateSnapshot을 필드에 저장하고 재사용하는 패턴 금지

## Commit 반환 타입
- 설계 명세상 Commit은 실패 시 false 반환 명시 없음 (void 허용)
- 단, Reserve는 bool 반환 필수 (가용량 부족 시 false)

## 인터페이스 필수 구현 목록
Reserve / Release / ReleaseAll / Commit / ValidateIntegrity — 5개 모두 필수
