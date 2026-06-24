---
name: project-architecture-decisions
description: AI Village v1.0 아키텍처 확정 결정 사항 — FSM 구조, LOD 기준, MessageBus 설계
metadata:
  type: project
---

# AI Village 아키텍처 설계 확정 결정 사항

**결정일:** 2026-06-25
**기반 문서:** GDD v0.4, TechSpec v1.0
**출력 파일:** C:\Users\anjyo\AI_GOAP\Docs\AIBehaviorArchitecture_v1.0.md

## FSM 구조 결정

FSM은 GOAP 플래너의 "생명주기 관리자"로 설계.
GOAP A* 탐색은 Job System 스레드, FSM은 메인 스레드에서 결과 수신·실행.

확정된 8개 상태:
- Idle, Planning, Executing, Replanning, CommandConflict, RefusingOrder, Dead, LOD_FSM

LOD 내부 4개 상태:
- LOD_Idle, LOD_GatheringResource, LOD_MovingToBase, LOD_Alert

## LOD 전환 기준
- 진입: 거리 > 30타일 AND nearEnemy == false
- 탈출: 거리 <= 30타일 OR nearEnemy == true OR EnemyDetected 수신
- LOD Tick 빈도: 0.5초 (Full GOAP 0.1초 대비 5배 절약)

## MessageBus 7개 메시지 타입
VillagerDied(High), EnemyDetected(High), RaidDecision(High)
ResourceDiscovered(Medium), ResourceDepleted(Medium), OrderIssued(Medium)
OrderRefused(Low)

## P0 서브 우선순위 (GDD v0.4 확정)
SurviveInjury(healthLevel<20) > SurviveHunger(hungerLevel>80) > SurviveFatigue(fatigueLevel>90)

## ConflictScore 공식
ConflictScore = Σ(Need_urgency[i] × Order_impact[i])
임계값 = 2.5 × (loyaltyLevel / 50)
거부: ConflictScore >= 임계값

## 명령 거부 7가지 케이스
REFUSE_HUNGER, REFUSE_INJURY, REFUSE_FATIGUE, REFUSE_LOYALTY,
REFUSE_DANGER, REFUSE_NO_TOOL, REFUSE_INSUFFICIENT_RESOURCES

**Why:** GDD v0.4 + TechSpec v1.0의 모든 확정 사항을 반영한 최초 완전 아키텍처 설계.
**How to apply:** 다음 설계 요청 시 이 결정 사항을 기준으로 델타만 반영.
