---
name: project-13-tilemap-fow
description: 13단계 타일맵 렌더링 + FoW 아키텍처 설계 확정 — MapConfig 중앙화, ChunkRenderer, FowManager
metadata:
  type: project
---

# 13단계: 타일 맵 렌더링 + Fog of War 아키텍처

**설계 완료일:** 2026-06-29
**상태:** 설계 완료, 구현 전 사용자 결정 항목 5개 대기 중

## 신규 파일 (3개)
- `Assets/Scripts/Core/MapConfig.cs` — ScriptableObject, 맵 크기 상수 중앙화
- `Assets/Scripts/Core/MapChunkRenderer.cs` — MonoBehaviour, ExecutionOrder -60
- `Assets/Scripts/Core/FowManager.cs` — MonoBehaviour 싱글턴, ExecutionOrder -55

## 기존 파일 수정 (5개)
- `GameManager.cs` — MapConfig 슬롯 추가, 틱에 FowManager.OnTick() 추가
- `JPSPathfinder.cs` — MAP_SIZE/OFFSET const 제거, FindPath() 내 MapConfig 읽기
- `FlowFieldManager.cs` — _flowDir 동적 배열 생성, _mapSize/_offset 필드 저장
- `SensorSystem.cs` — 수정 없음 (DiscoverArea() 유지)
- `VillagerFSM.cs` — DiscoverArea() 직접 호출 → FowManager.RevealArea()로 교체

## 핵심 설계 결정
- MapConfig.Active 정적 프로퍼티로 런타임 주입 (GameManager.Awake 최상단)
- FoW 3단계: byte[,] 배열 (0=미탐험, 1=탐험됨, 2=가시)
- 업데이트: Visible→Explored 다운그레이드 후 주민 시야 재계산 (0.1초 틱)
- 렌더링: Texture2D Overlay Quad + Unlit Shader (URP 호환)
- 더티 마스크: bool[,] _dirtyMask로 변경 타일만 SetPixels32 + Apply(false)
- 청크 크기: 16×16 타일 (MapConfig._chunkSize)
- 청크 업데이트 임계값: ChunkSize × 0.5f 타일 (카메라 이동 감지)
- FowManager → SensorSystem 단방향 의존 (RevealArea 내부에서 DiscoverArea 호출)

## Execution Order (업데이트)
MessageBus -100, BuildingQueue -90, SensorSystem -85, GameManager -80,
FlowFieldManager -75, FactionAI -70, VillageAdvisor -65,
MapChunkRenderer -60 (신규), FowManager -55 (신규),
VillagerFSM/EnemyFSM 0

## 사용자 결정 대기 항목
- 결정 A: LOS(시야 차단) 구현 여부 — FowManager 알고리즘 차단
- 결정 B: FoW 렌더링 방식 — 권장: Texture2D Overlay Quad
- 결정 C: 게임 시작 초기 발견 범위 — 비차단
- 결정 D: 타일 팔레트 및 지형 타입 정의 — MapChunkRenderer 구현 차단
- 결정 E: 주민 시야 공유 방식 — 비차단

**Why:** 100×100→500×500 확장성을 위해 MapConfig ScriptableObject 중앙화 필수.
**How to apply:** 다음 설계 요청 시 결정 A/D가 완료됐는지 먼저 확인.
