---
name: project-implementation-phase9
description: 9+10단계 플레이어 입력+UI 기초 구현 완료 — 신규 8파일, 기존 2파일 수정
metadata:
  type: project
---

9+10단계 (플레이어 입력 + UI 기초) 구현 완료 (2026-06-28).

**Why:** 플레이어가 주민을 클릭 선택하고 건설 명령을 내릴 수 있는 최초 UI 레이어 구축.

**How to apply:** 다음 단계(11단계 이후)에서 UI 확장 시 이 구조를 기준으로 패널 추가.

## 신규 파일 (Assets/Scripts/UI/)
- PlayerInputController.cs — DefaultExecutionOrder(-20), 레이캐스트 주민 선택, IssueBuildingOrder
- HUDManager.cs — DefaultExecutionOrder(-10), 싱글턴, 이벤트 중계, 코루틴 시작
- ResourceHUD.cs — 0.5초 폴링, 변경 시만 SetText (GC 절감)
- VillagerStatusPanel.cs — 0.2초 폴링, Slider 5개, 사망 감지 후 자동 닫기
- BuildingOrderPanel.cs — 버튼 6개, 0.5초마다 interactable 갱신
- BuildingQueuePanel.cs — 0.3초 폴링, 항목수 변경 시만 재생성, 내부클래스 BuildingQueueItemView
- RefusalBubble.cs — Queue<string> 토스트, 2.5초 표시, HandleRefusal + EnqueueToast
- GOAPDebugOverlay.cs — #if UNITY_EDITOR || DEVELOPMENT_BUILD, 0.1초 폴링

## 기존 파일 수정
- VillagerFSM.cs Awake(): SphereCollider(radius=0.5, isTrigger=true) 자동 추가 + "Villager" 레이어 설정
- GameManager.cs: `using System;` 추가, `OnOrderRefusedEvent` 이벤트 선언, OnOrderRefused() 말미에 `OnOrderRefusedEvent?.Invoke(payload)` 추가

## 설계 계약 (준수됨)
- UI 레이어: Brain 읽기 전용 (쓰기 금지)
- 자원 차감 없음: IssueBuildingOrder는 WorldState 조회만
- GameManager → UI 직접 참조 없음: C# event 패턴 사용
- PlayerInputController만 BuildingQueue.EnqueueBuilding() 호출

## 씬 설정 필수 요건
- Project Settings > Tags and Layers에 "Villager" 레이어 추가 필요
- Canvas 아래 각 패널 GameObject 생성 및 컴포넌트 연결 필요
- BuildingQueuePanel의 _queueItemPrefab: BuildingQueueItemView 컴포넌트 포함 프리팹 필요

## 빌드 비용 테이블 (GDD v0.4 기획서 수치)
- Campfire: Wood 5
- House: Wood 20, Stone 10
- Storehouse: Wood 15, Stone 5
- TownHall: Wood 35, Stone 30, Iron 6
- Forge: Wood 20, Stone 20, Iron 15 (TownHall 필요)
- Watchtower: Wood 10, Stone 30, Iron 5 (TownHall 필요)

관련 메모리: [[project-implementation-phase8]], [[project-implementation-phase6a]]
