---
name: project-6b-faction-ai
description: 6B단계 FactionAI 설계 확정 — EnemyBrain/EnemyFSM/FactionAI 구조, Script Execution Order, GameManager 연동
metadata:
  type: project
---

# 6B단계 FactionAI 시스템 설계 확정 (2026-06-27)

## 파일 구조
- `Assets/Scripts/AI/EnemyBrain.cs` — 순수 C# 데이터 클래스, EnemyFSMState 열거형 포함
- `Assets/Scripts/AI/EnemyFSM.cs`  — MonoBehaviour, IEnemyAgent 구현 (SensorSystem.cs에 이미 정의)
- `Assets/Scripts/Core/FactionAI.cs` — MonoBehaviour, FactionState 열거형 포함

## Script Execution Order 확정
MessageBus(-100) → BuildingQueue(-90) → SensorSystem(-85) → GameManager(-80) → FactionAI(-70) → VillagerFSM(0) → EnemyFSM(10)

## EnemyFSM 5개 상태
Idle → Marching → Attacking → Retreating → Dead (흡수)
AnyState P0: IsAlive==false → Dead (Update()에서 매 프레임)
AnyState P1: Attacking 중 HealthLevel<20f → Retreating

## FactionAI 4개 상태
Dormant → Scouting → Raiding → Retreating(팩션) → Scouting

## 팩션 초기 자원 (플레이어보다 강함)
Wood=20, Stone=15, RawFood=50 (플레이어: Wood=10, Stone=5, RawFood=30)

## 침략 트리거 공식 (GDD v0.4 확정)
(CopperStock<10 OR SilverStock<5) AND nearPlayer AND playerStrength < factionStrength×0.8

## 팩션별 특수 규칙
- 숲의 부족(0): Day10+, RawFood<30이면 nearPlayer 조건 면제
- 철의 도시(1): Day12+, IronStock<15이면 자원 조건 없이 즉시 트리거
- 상인 연합(2): Day10+, TradeRelation>=40이면 영구 레이드 불가

## GameManager 수정 사항 (기존 파일 수정)
- BaseTileX, BaseTileY 공개 프로퍼티 추가
- CalculatePlayerStrength() 추가 (공식: 주민×10 + 전사×15 + 무장×8 + 망루×20 + 대장간×15)
- GenerateEnemyFactionBases()를 Start()에서 호출 (Awake 타이밍 문제 방지)
- _enemyFSMs 목록 + TickEnemyGroup() + GameTickCoroutine에 TickEnemyGroup 추가

## MessageBus 추가 페이로드
RaidDecisionPayload (신규): FactionId, FactionName, BaseTileX, BaseTileY, UnitCount, EstimatedStrength, RaidTriggerReason

## AuthoritativeWorldState 수정 필요
WatchtowerBuilt bool, ForgeBuilt bool 추가 (CalculatePlayerStrength 연동)

## 핵심 설계 결정
1. EnemyFSM 직접 통신 금지: EnemyDetected → MessageBus, NearEnemy → SensorSystem
2. EnemyDetected 폭주 방지: FactionAI에서 팩션 단위 1회만 발행 (5초 쿨다운)
3. playerStrength 캐싱: GameManager.CachedPlayerStrength로 중복 계산 방지 권장
4. EnemyBrain에 LoyaltyLevel 없음 (적 유닛은 팩션 완전 종속 — 반란 불가)

**Why:** 기존 VillagerFSM 파이프라인과 완전 연동하면서 팩션 단위 전략 AI를 추가.
**How to apply:** 차기 설계 시 FactionAI Tick 간격(5초)과 GameManager Tick(0.1초)의 분리 패턴을 참고.
