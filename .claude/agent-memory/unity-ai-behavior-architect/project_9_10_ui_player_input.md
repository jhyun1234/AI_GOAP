---
name: project-9-10-ui-player-input
description: 9+10단계 플레이어 입력 + UI 기초 시스템 설계 확정 (2026-06-28)
metadata:
  type: project
---

# 9+10단계 UI + 플레이어 입력 설계 확정 (2026-06-28)

## Script Execution Order 최종
MessageBus(-100) → BuildingQueue(-90) → SensorSystem(-85) → GameManager(-80)
→ FactionAI(-70) → VillageAdvisor(-65) → PlayerInputController(-20) → HUDManager(-10) → VillagerFSM(0)

## 신규 파일 (Assets/Scripts/UI/)
PlayerInputController, HUDManager, ResourceHUD, VillagerStatusPanel,
BuildingOrderPanel, BuildingQueuePanel, RefusalBubble, GOAPDebugOverlay

## 핵심 설계 결정
1. HUD 갱신 전략: 이벤트 구독(MessageBus) + 폴링(0.5초) 하이브리드
   - ResourceHUD: 0.5초 폴링 (AuthoritativeWorldState 직접 읽기)
   - VillagerStatusPanel: 선택된 VillagerFSM.Brain 직접 읽기 0.2초 폴링
   - BuildingQueuePanel: BuildingQueue.GetAllEntries() 0.3초 폴링
2. SphereCollider: Awake()에서 코드로 자동 추가 (씬 배치 실수 방지)
   - radius=0.5f, isTrigger=true, 레이어 "Villager"
3. 건설 위치: 자동 배치 (플레이어 클릭 위치 지정 없음) — 기지 타일 (0,0) 고정
4. 자원 차감 시점: EnqueueBuilding 즉시 ResourceRegistry.Reserve() — 이중 차감 방지
5. RefusalBubble: Screen Space Overlay Canvas + 화면 좌하단 토스트 큐 방식
6. GameManager.OnOrderRefused → 이벤트(C# event)로 UI에 중계

## 레이어 설계
Layer "Villager" (LayerMask 8번 권장) — VillagerFSM.Awake()에서 자동 할당

## BuildingOrderPanel 자원 선제 체크
EnqueueBuilding 호출 전 AuthoritativeWorldState에서 자원 충분 여부 확인 후 UI 피드백
부족 시 "자원 부족" 토스트 + 버튼 비활성화 (비활성화 조건: WorldState Stock < 건물 비용 상수)

## GOAPDebugOverlay
#if UNITY_EDITOR 또는 Debug 빌드에서만 활성화
선택된 VillagerFSM.Brain의 CurrentGoalId, CurrentActionId, CombatMentalState, FSMState 표시
