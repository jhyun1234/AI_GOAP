---
name: project-14-season-system
description: 14단계 계절 시스템 아키텍처 설계 — SeasonManager 신규 파일, GOAP 가중치 패치, MessageBus 확장
metadata:
  type: project
---

## 확정 결정 사항

### 파일 구성
- SeasonManager.cs: 신규 MonoBehaviour, ExecutionOrder -75 (GameManager -80과 FactionAI -70 사이)
- ResourceNode.cs 수정: BaseRegenerationRate 필드 추가
- VillagerEnums.cs 수정: MessageType에 SeasonChanged, WinterCrisis 추가
- MessageBus.cs 수정: Payload 2개 추가, DEFAULT_PRIORITY_MAP 2개 추가
- GOAPActionRegistry.cs 수정: AutumnGatherModifier 상수 + BuildActionDefs에 계절 파라미터

### ExecutionOrder
MessageBus(-100) → BuildingQueue(-90) → SensorSystem(-85) → GameManager(-80) → SeasonManager(-75) → FactionAI(-70) → VillageAdvisor(-65) → MapChunkRenderer(-60) → FowManager(-55)

### 틱 순서 (GameManager GameTickCoroutine)
1. GameTime += deltaGameDays
2. UpdateAllSensors
3. TickResourceRegeneration (deltaGameDays) — SeasonManager.OnTick 이후로 변경
   실제 순서: 1.GameTime → 2.SeasonManager.OnTick → 3.UpdateAllSensors → 4.TickResourceRegeneration → 5.MessageBus.ProcessTick → 6.TickVillagerGroup → 7.TickEnemyGroup → 8.FowManager.OnTick → 9.CheckMilestoneEvents → 10.CheckWinLoseConditions

### GOAP 가중치 적용 방법
GOAPActionRegistry.BuildActionDefs(role, allocator)에 seasonModifier float 파라미터 추가.
GOAPPlannerScheduler.Schedule()에서 AuthoritativeWorldState.Instance.Season을 읽어 배율 계산 후 전달.
Burst Job 내부에서 WorldState 참조 금지 — 메인 스레드에서 배율 계산 완료 후 Job에 주입.

### WinterCrisis 임계값 공식
threshold = villagerCount * 3 * 30
(villagerCount는 GameManager._villagerFSMs.Count를 SeasonManager가 참조)

**Why:** 30일치 비상식량 = 겨울 1주기(10일) × 3배 버퍼
**How to apply:** SeasonManager.OnTick에서 매 틱 체크, 1회만 발행 (_winterCrisisPublished 플래그)
