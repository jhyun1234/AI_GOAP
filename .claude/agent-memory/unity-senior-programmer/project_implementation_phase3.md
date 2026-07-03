---
name: project-implementation-phase3
description: 3단계 구현 완료 — MessageBus Pub-Sub 브로커 신규 생성 및 VillagerFSM 연결
metadata:
  type: project
---

3단계 구현 완료 (2026-06-26). `AIVillage.Core` 네임스페이스에 `MessageBus.cs` 신규 생성 및 `VillagerFSM.cs` 수정.

**Why:** AI 유닛 간 이벤트 통신을 중앙화된 Pub-Sub 브로커로 처리하기 위해. 2단계의 static event 및 Debug.Log 더미를 실제 MessageBus 연결로 교체.

**How to apply:** 4단계 작업 시 아래 TODO 항목 및 GameManager 연동 방법을 참조할 것.

## 생성/수정된 파일
- `Assets/Scripts/Core/MessageBus.cs` — 신규 생성: Pub-Sub 싱글턴 브로커
- `Assets/Scripts/AI/VillagerFSM.cs` — 3개 구간 수정

## MessageBus 아키텍처
- Singleton MonoBehaviour, DontDestroyOnLoad 적용
- `SortedList<int, List<AIMessage>>` 우선순위 큐 (key=0 High→1 Medium→2 Low)
- 같은 Tick 내 EnemyDetected 동일 팩션 ID 중복 폐기 (Dedup HashSet)
- GameManager가 매 Tick(0.1초) `ProcessTick()` 직접 호출 — Update() 미사용
- 예외 격리: 한 구독자 콜백의 예외가 다른 구독자 전달을 막지 않음

## Payload 구조체 (MessageBus.cs 내부 nested)
7개 정의: VillagerDiedPayload, DroppedItemInfo, EnemyDetectedPayload,
ResourceDiscoveredPayload, DiscoveredNodeInfo, ResourceDepletedPayload,
OrderIssuedPayload, OrderRefusedPayload, RaidDecisionPayload

## VillagerFSM.cs 변경사항
1. `public static event Action<string, int, int> OnVillagerDied` **제거**
   → `EnterDead()`에서 `MessageBus.Instance.Publish(VillagerDiedPayload)` 로 교체
2. `EnterRefusingOrder()` — Debug.Log 더미 → `MessageBus.Instance.Publish(OrderRefusedPayload)` 교체
   - `BuildRefusalMessage()` private 헬퍼 메서드 신규 추가 (한국어 거부 이유 텍스트)
3. `_messageQueue` 타입 변경:
   - 2단계: `Queue<AIMessage>` → 3단계: `SortedList<int, List<AIMessage>>`
   - `ReceiveMessage()` 우선순위 버킷 분류로 수정
   - `ProcessMessageQueue()` → 배치 스냅샷 패턴 + 우선순위 순 처리로 재작성
   - `HandleMessage()` private 메서드 신규 분리 (단일 메시지 처리 로직)
4. `using System.Collections.Generic;` 추가 (List<T> 사용)

## GameManager 연동 추가 사항 (3단계 신규)
```csharp
// GameManager.Awake()에서 MessageBus 구독 등록:
MessageBus.Instance.Subscribe(MessageType.VillagerDied, OnVillagerDiedMessage);

// 핸들러 예시:
private void OnVillagerDiedMessage(AIMessage msg)
{
    if (msg.Payload is MessageBus.VillagerDiedPayload payload)
    {
        // 주변 주민 패널티 전파 (15타일 이내)
        foreach (var fsm in _allVillagers)
        {
            if (GetTileDistance(fsm, payload.DeathTileX, payload.DeathTileY) <= 15f)
                fsm.ReceiveMessage(msg); // VillagerDied 핸들러가 mood/loyalty 감소 처리
        }
    }
}

// GameManager의 0.1초 Tick 코루틴에서 ProcessTick() 호출:
MessageBus.Instance.ProcessTick();
// 이후 VillagerFSM 그룹별 Tick() 호출
```

## 남은 더미(Stub) 항목 (4단계 이후)
- `SimulatePlanResult()` — 실제 GOAPPlannerJob으로 교체 (미변경)
- `OrderRefusedPayload.ConflictScore/Threshold` — 현재 0f. Brain에 마지막 ConflictScoreData 캐싱 필요
- 거부 메시지 `BuildRefusalMessage()` — 로컬라이제이션 키 체계 도입 시 교체 필요

## 우선순위 매핑 (확정)
| MessageType       | Priority |
|-------------------|----------|
| VillagerDied      | High     |
| EnemyDetected     | High     |
| RaidDecision      | High     |
| ResourceDiscovered| Medium   |
| ResourceDepleted  | Medium   |
| OrderIssued       | Medium   |
| OrderRefused      | Low      |
