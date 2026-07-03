---
name: project-implementation-phase8
description: 8단계 구현 완료 — 전투 기초 + 정신 상태 시스템 (Fighting/Fleeing 상태, CombatMentalState, RageKill 전파)
metadata:
  type: project
---

8단계 구현 완료: 전투 기초 + 정신 상태 시스템

**Why:** 적(EnemyFSM)이 주민(VillagerFSM)을 실제로 공격하고, 전투 결과에 따라 공포/분노 심리 상태가 전파되는 시스템 필요.

**변경된 파일 6개:**
- VillagerEnums.cs: VillagerState에 Fighting/Fleeing 추가, MessageType에 RageKill 추가, CombatMentalState/VillagerRole enum 신규 추가
- VillagerBrain.cs: 전투 정신 상태 region 추가 (CombatMentalState, AttackModifier, RageTimer, NearbyEnemyCount, RecentAllyDeathWitness, RecentRageKillWitness)
- MessageBus.cs: RageKillPayload struct 추가, DEFAULT_PRIORITY_MAP에 RageKill→High 추가
- GameManager.cs: Villagers/Enemies IReadOnlyList 프로퍼티 추가 (EnemyFSM/VillagerFSM이 상호 탐색에 사용)
- EnemyFSM.cs: BASE_DAMAGE_PER_TICK/ATTACK_RANGE_TILES 상수 추가, State_Attacking() 실제 피해 처리로 교체, FindNearestVillager() 추가
- VillagerFSM.cs: 전투 상수 9개 추가, Update()에 AnyState #3(HP<30 Fear→Fleeing) 추가, Tick() switch에 Fighting/Fleeing 케이스 추가, State_Idle()의 NearEnemy 처리 → Fighting 직접 전이로 교체, State_Fighting()/State_Fleeing()/EnterFighting()/EnterFleeing()/EvaluateCombatMentalState()/FindNearestEnemy()/PublishRageKillEvent() 신규 추가, HandleMessage()에 VillagerDied Fear전염 + RageKill 케이스 추가

**핵심 설계:**
- Fear > Rage 우선순위. HP 30% 미만 → 항상 Fear
- Normal→Fear/Rage 분기: HP 50% 기준 (50 미만이면 Fear, 이상이면 Rage)
- Rage 지속 8초, 틱 기반 감소 (0.6초/틱)
- Fear 전염 반경: 5타일 (아군 사망 목격)
- Rage 전염 반경: 6타일 (RageKill 목격)
- Fear→Rage 역전: 체력 60% 이상 + RageKill 목격
- EnemyFSM.FindNearestVillager()와 VillagerFSM.FindNearestEnemy() 모두 GameManager.Instance.Villagers/Enemies를 참조

**How to apply:** 9단계(경로탐색 다양성) 작업 시 Fighting/Fleeing 상태에서의 이동 처리도 함께 고려할 것.
