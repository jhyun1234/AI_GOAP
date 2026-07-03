---
name: project-implementation-phase6b
description: 6B단계 FactionAI 시스템 구현 완료 — EnemyBrain, EnemyFSM, FactionAI 신규 파일, GameManager/AuthoritativeWorldState 수정
metadata:
  type: project
---

# 6B단계 구현 완료 — FactionAI 시스템

**구현 완료 일자**: 2026-06-27

## 신규 생성 파일

| 파일 | 네임스페이스 | 역할 |
|---|---|---|
| `Assets/Scripts/AI/EnemyBrain.cs` | `AIVillage.AI` | 적 유닛 런타임 상태 데이터 클래스. EnemyState enum 포함. |
| `Assets/Scripts/AI/EnemyFSM.cs` | `AIVillage.AI` | 적 유닛 5상태 FSM. IEnemyAgent(SensorSystem) 구현. |
| `Assets/Scripts/Core/FactionAI.cs` | `AIVillage.Core` | 팩션 전략 AI. 5초 코루틴, 3개 팩션 특수 규칙. |

## 기존 파일 수정

### AuthoritativeWorldState.cs
- `WatchtowerBuilt` bool 프로퍼티 추가 (ForgeBuilt는 이미 존재했음)
- playerStrength 계산용: WatchtowerBuilt=+20, ForgeBuilt=+15

### GameManager.cs
- `using System.Linq;` 추가
- Private Fields: `_factionAIs`, `_enemyFSMs` 필드 추가
- 공개 프로퍼티: `BaseTileX`, `BaseTileY` 추가 (FactionAI.InitializeBase용)
- Start() Step 4,5 추가: `CollectAndSetupFactionAI()`, `CollectAndSetupEnemies()`
- 틱 루프: `TickEnemyGroup()` 추가 (VillagerFSM과 동일한 그룹 분산)
- MessageBus: `OnRaidDecision` 핸들러 추가, Subscribe/Unsubscribe에 등록
- 공개 메서드: `CalculatePlayerStrength()` — 플레이어 전력 계산 공식 구현
- 헬퍼: `CollectAndSetupFactionAI()`, `CollectAndSetupEnemies()`, `TickEnemyGroup()`

## 핵심 아키텍처 결정

### IEnemyAgent 위치
- SensorSystem.cs에 이미 `IEnemyAgent` (TileX, TileY, IsAlive)가 정의되어 있었음
- EnemyFSM이 `AIVillage.Core.IEnemyAgent`를 구현
- FactionAI 추가 멤버(EnemyId, FactionId, HealthLevel, TickGroupIndex, SetTargetTile)는 EnemyFSM 공개 프로퍼티로 직접 노출

### EnemyState enum 위치
- `EnemyBrain.cs` 파일 상단에 정의 (별도 파일 없이 동일 파일에 배치)
- 5상태: Idle, Moving, Attacking, Retreating, Dead

### FactionAI 소속 유닛 수집
- Start()에서 `FindObjectsOfType<EnemyFSM>().Where(u => u.FactionId == _factionId)` 패턴
- LINQ 사용 (Start 1회 한정 — Update/Tick 내 금지)

### EnemyDetected 발행 전략
- FactionAI.PublishRaidDecision()에서 1회만 발행 (개별 EnemyFSM 발행 금지)
- MessageBus Dedup (EnemyFactionId 기반)이 추가 보호층 역할

### CalculatePlayerStrength 공식
```
(주민 수 × 10) + (전사 수 × 15) + (무기 보유 수 × 8)
+ (WatchtowerBuilt × 20) + (ForgeBuilt × 15)
```
- 전사: AgentRole.Warrior인 생존 주민
- 무기 보유: Brain.HasWeapon OR Brain.HasPrimitiveWeapon

## Script Execution Order
- GameManager: -80 (기존 유지)
- FactionAI: -70 (GameManager 이후, EnemyFSM 이전)
- SensorSystem: -85 (가장 먼저)
- EnemyFSM: 0 (기본값)

## 팩션별 특수 규칙 구현 위치
- 숲의 부족 즉시 트리거: FactionAI.EvaluateRaidDecision() → `_rawFoodStock < 30`
- 철의 도시 즉시 트리거: FactionAI.EvaluateRaidDecision() → `_ironStock < 15`
- 상인 연합 TradeProposal: FactionAI.EvaluateAndExecuteGoal() → `_tradeProposalSent` 플래그

## 다음 단계 TODO
- FactionAI.ModifyFactionResource() 연동: 팩션 자원이 실제 수집/소비 시스템에 연동되어야 함
- EnemyFSM 이동 더미 → 실제 PathFinding 교체
- 주민에 대한 전투 데미지 적용 시스템 (CombatSystem) 미구현
- TradeProposal 수락/거절 처리 시스템 미구현

**Why:** 6B단계는 팩션 AI 시스템의 골격을 완성하는 단계. 실제 PathFinding과 전투 로직은 7단계 이후 추가 예정.
**How to apply:** 씬에 FactionAI GameObject를 팩션당 1개씩 배치하고 Inspector에서 factionId, activationDay, 기지 위치 설정 필요.
