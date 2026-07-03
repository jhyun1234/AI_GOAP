---
name: project-implementation-phase1
description: "AI Village 1단계 구현 완료 상태 — Core 레이어 5개 파일, 네임스페이스, 아키텍처 패턴"
metadata:
  type: project
---

# AI Village 1단계 구현 완료 (2026-06-25)

Core 레이어 5개 파일이 `Assets/Scripts/Core/` 에 생성됨.

**Why:** GOAP Planner, VillagerFSM, Action 구현의 기반이 되는 월드 스테이트 + 자원 예약 시스템.

**How to apply:** 2단계(GOAPPlanner, Action) 구현 시 이 파일들의 인터페이스를 그대로 사용한다.

## 네임스페이스
전체 프로젝트: `AIVillage.Core` (모든 Core 파일에 적용됨)

## 생성된 파일 및 핵심 인터페이스

### ResourceType.cs
- `ResourceType` 열거형: RawFood=0, CookedFood=1, Wood=2, Stone=3, Iron=4, Copper=5, Silver=6
- `ItemType` 열거형: Axe, Pickaxe, Weapon, PrimitiveWeapon, Food
- `Season` 열거형: Spring=0, Summer=1, Autumn=2, Winter=3

### WorldStateIndices.cs
- NativeArray<int> 슬롯 인덱스 상수 (TOTAL_COUNT = 21)
- `StockIndexOf(ResourceType)` 헬퍼 (ResourceType int값 = Stock 인덱스)
- `ReservedIndexOf(ResourceType)` 헬퍼 (Stock인덱스 + 7)

### AuthoritativeWorldState.cs
- 싱글턴: `AuthoritativeWorldState.Instance` / `SetInstance()`
- 순수 C# 클래스 (MonoBehaviour 아님) — GameManager가 new로 생성
- 모든 stock 프로퍼티에 `Mathf.Max(0f, value)` 클램프 내장
- GDD v0.4 초기값: rawFoodStock=30, woodStock=10, stoneStock=5, 나머지=0
- `GetStock(ResourceType)` / `SetStock(ResourceType, float)` 범용 접근자
- `GetReserved(ResourceType)` / `SetReserved(ResourceType, float)` — ResourceRegistry 전용
- `List<DroppedItem> DroppedItems` (사망 시 드롭 아이템 목록)

### WorldStateSnapshot.cs
- `WorldStateSnapshot.CreateFrom(AuthoritativeWorldState)` — 메인 스레드에서만 호출
- `NativeArray<int> Data` — Allocator.TempJob, Job 완료 후 Dispose() 필수
- float 직렬화: `BitConverter.SingleToInt32Bits` / `Int32BitsToSingle`
- `GetAvailableStock(ResourceType)` 편의 메서드
- IDisposable 구현 → using 구문 권장

### ResourceRegistry.cs
- 순수 C# 클래스 — `new ResourceRegistry(AuthoritativeWorldState)` 로 생성
- `Reserve(agentId, type, amount) → bool` — 실패 시 false (예외 throw 없음)
- `Release(agentId, type, amount)` — Action 취소 시
- `ReleaseAll(agentId)` — 주민 사망 시 즉시 호출 (Q04)
- `Commit(agentId, type, amount)` — Action 성공 완료 시
- `ValidateIntegrity()` — 30초마다 호출 권장, 불일치 시 자동 복구
- 내부: `_agentReservations: Dictionary<string, Dictionary<ResourceType, float>>`
- 내부: `_totalReserved: Dictionary<ResourceType, float>` (O(1) 조회 캐시)

## GameManager에서의 초기화 순서 (중요)
```csharp
// Awake() 에서:
var worldState = new AuthoritativeWorldState();
AuthoritativeWorldState.SetInstance(worldState);
var resourceRegistry = new ResourceRegistry(worldState);
```

## 아키텍처 결정 사항
- ResourceType int값(0~6)과 WorldStateIndices Stock 슬롯(0~6)이 1:1 대응 — 변경 금지
- Reserved 슬롯은 Stock 슬롯 + 7 오프셋 — 변경 금지
- AuthoritativeWorldState.SetReserved()는 ResourceRegistry만 호출해야 함
