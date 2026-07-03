---
name: project-implementation-phase13
description: 13단계 타일 맵 렌더링 + Fog of War 구현 완료 — MapConfig/FowManager/MapChunkRenderer 신규 3파일, 기존 4파일 수정
metadata:
  type: project
---

13단계 구현 완료 (2026-06-29): 타일 맵 렌더링 + Fog of War 시스템

**신규 파일 3개:**
- `Assets/Scripts/Core/MapConfig.cs` — ScriptableObject. mapSize/mapOffset/FoW수치/색상 담당. MapConfig.Active 정적 프로퍼티.
- `Assets/Scripts/Core/FowManager.cs` — ExecutionOrder -55. byte[,] _fowState(0미탐험/1탐험됨/2가시). Symmetric Shadowcasting 8옥탄트. OCTANT_TRANSFORMS 정적 테이블. OnTick()에서 가시→탐험됨 다운그레이드 후 전체 주민 RevealArea 재실행.
- `Assets/Scripts/Core/MapChunkRenderer.cs` — ExecutionOrder -60. Texture2D(mapSize×mapSize, RGBA32, FilterMode.Point). Color32[] 픽셀버퍼 캐시. 카메라 이동 chunkSize×0.5 이상 시 RefreshDirtyChunks(). FoW 상태별 색상: 0=검정, 1=grassColor×fowExplored.a/255, 2=grassColor 100%.

**수정 파일 4개:**
- `GameManager.cs` — Awake() 첫 줄에 MapConfig.SetActive(_mapConfig) 추가. Start() Step7에 FowManager.Instance.InitialReveal() 추가. GameTickCoroutine() Step6에 FowManager.Instance?.OnTick() 추가. Inspector 슬롯 [SerializeField] private MapConfig _mapConfig 추가.
- `JPSPathfinder.cs` — MAP_SIZE/OFFSET 상수 제거. FindPath() 시작 시 mapSize/offset 로컬 변수로 MapConfig.Active에서 동적 읽기. 폴백값 FALLBACK_MAP_SIZE=100/FALLBACK_OFFSET=50. GetSuccessors/Jump/ReconstructAndExpand 시그니처에 mapSize/offset/maxJumpSteps/maxPathNodes 파라미터 추가. IsInBounds(ax, ay, mapSize) 오버로드 추가.
- `FlowFieldManager.cs` — MAP_SIZE/OFFSET 상수 제거. _flowDir 필드 선언에서 고정 크기 초기화 제거. Awake()에서 MapConfig.Active 읽어 _mapSize/_offset 캐시 후 new Vector2Int[_mapSize, _mapSize] 동적 생성. BuildFlowField()/GetDirection()/OnDrawGizmosSelected() 내부 상수→필드 교체.
- `VillagerFSM.cs` — 웨이포인트 도착 시(Update 내 WAYPOINT_ARRIVE_DIST 분기) + State_Executing() 첫 틱(ActionStartTime < 0 분기)에 FowManager.Instance?.RevealArea(Brain.TileX, Brain.TileY, MapConfig.Active.villagerSightRadius) 추가. StartPathTo()의 new bool[100,100] → MapConfig.Active.mapSize 동적 읽기.

**Why:** 하드코딩 100×100을 제거하여 MapConfig 한 곳에서 맵 크기를 변경할 수 있도록 중앙화. FoW는 전역 byte[,] 1개(주민 시야 공유, 설계 결정 E). Shadowcasting으로 LOS 차단 확장 가능(설계 결정 A).

**설계 결정 적용:**
- A(LOS): Symmetric Shadowcasting 8옥탄트 완전 구현. 현재 _blocking 전부 false → 원형 시야와 동일. 건물 완성 시 FowManager.SetBlocking()으로 LOS 차단 추가 가능.
- B(렌더링): Texture2D Overlay Quad + Unlit/Texture Shader (씬 배치 가이드에 포함)
- C(초기 반경): 15타일 (MapConfig.initialRevealRadius Inspector에서 조정 가능)
- D(타일 팔레트): grassColor/forestColor 2타입. MapChunkRenderer.GetBaseTileColor() 확장 포인트로 분리.
- E(시야 공유): 전역 byte[,] _fowState 1개. OnTick()에서 GameManager.Villagers 순회하여 전체 재계산.

**씬 배치 필수 항목:**
1. MapConfig.asset 생성 (Create → AI Village → MapConfig)
2. _FowManager GameObject + FowManager 컴포넌트
3. _MapChunkRenderer GameObject + MapChunkRenderer 컴포넌트
4. MapQuad (Quad 메시, Scale 100×100×1, Position Z=1, Unlit/Texture 머티리얼)
5. MapChunkRenderer._quad 슬롯에 MapQuad MeshRenderer 연결
6. GameManager._mapConfig 슬롯에 MapConfig.asset 연결
