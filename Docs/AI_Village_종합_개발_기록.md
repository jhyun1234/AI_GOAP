# AI Village — 종합 개발 기록

*이 문서는 AI Village 프로젝트의 장르, 설계, 시스템 아키텍처, 알고리즘, 개발 진행 상황, 앞으로의 방향까지 하나로 정리한 종합 문서다. 마지막 업데이트 2026-07-13.*

**저장소**: https://github.com/jhyun1234/AI_GOAP.git
**엔진**: Unity 2D (URP), Windows 11
**언어**: C# (`.NET`), Job System, Burst Compiler
**개발자**: 최종현 (jhyun1234)

---

## 목차

1. [게임 개요](#1-게임-개요)
2. [핵심 컨셉과 재미 설계](#2-핵심-컨셉과-재미-설계)
3. [게임 시스템 총람](#3-게임-시스템-총람)
4. [아키텍처: 계획층·실행층·감각층](#4-아키텍처-계획층실행층감각층)
5. [알고리즘 상세](#5-알고리즘-상세)
6. [건물·경제·전투 데이터](#6-건물경제전투-데이터)
7. [실행 순서와 성능 예산](#7-실행-순서와-성능-예산)
8. [디렉토리 구조와 어셈블리](#8-디렉토리-구조와-어셈블리)
9. [개발 진행 이력 (단계별)](#9-개발-진행-이력-단계별)
10. [주요 결함과 학습](#10-주요-결함과-학습)
11. [개발 방식 — 명세, 리뷰, 재미 우선](#11-개발-방식--명세-리뷰-재미-우선)
12. [현재 상태와 다음 방향](#12-현재-상태와-다음-방향)
13. [부록: ADR 요약](#13-부록-adr-요약)

---

## 1. 게임 개요

### 장르
Unity URP 기반 **PC 콜로니 시뮬레이션 · 생존 전략 게임**. 탑다운 2D (X-Y 평면).

### 핵심 기술
**GOAP (Goal-Oriented Action Planning)** AI가 게임의 뼈대. 주민 50명 각각이 독립된 GOAP 플래너로 자율 행동한다. 적 팩션 3개도 GOAP 기반으로 침략 결정을 스스로 내린다.

### 참고 게임
**RimWorld** (Ludeon Studios). 다만 다음 지점에서 다르다:
- **멀티스텝 동적 플래닝** — RimWorld의 우선순위 큐가 아닌 A* 기반 목표-액션 체인 계획
- **명령 거부 시스템** — 플레이어가 "지도자"이지만 주민이 상태 기반으로 거부 가능 (게임의 정체성)
- **창발적 행동** — 성격/관계/기억 시스템으로 통제되지 않은 이야기 생성

### 목표 규모
- 플레이어 마을: 최대 50명
- 적 팩션 3개 합산: 최대 45명
- 중립/야생: 최대 5명
- 실질 Full GOAP 동시 유닛: 60~70명 (LOD로 나머지 완충)
- 맵: 100×100 타일 (원점 (0,0) 기준 오프셋 50)

---

## 2. 핵심 컨셉과 재미 설계

### 4가지 재미 엔진 (Docs/게임이해_및_재미설계.md, 2026-07-07 확정)

| # | 이름 | 설명 | 현주소 |
|---|---|---|---|
| ① | 창발 서사 | 시스템 충돌로 우연히 생긴 이야기. 플레이어가 남에게 말하고 싶어짐 | 무대만 있고 재료(성격/관계) 없음 |
| ② | 애착과 상실 | 주민을 "유닛"이 아닌 "우리 애"로 느끼게 함 | 이름만 있고 역사/성장 없음 |
| ③ | 긴장-해소 리듬 | 위협 예고→준비→절정→안도의 박동 | 겨울/침략 있지만 예고 약함 |
| ④ | 협상 (게임 정체성) | 거부하는 주민과 교섭 — "관리"가 아닌 "관계" | 거부는 구현됨, 설득 수단 없음 |

### F 로드맵 (재미 우선 개발 순서)

| 코드 | 기능 | 재미 엔진 | 난이도 | 상태 |
|---|---|---|---|---|
| **F-A** | 성격 특성 6종 (겁쟁이/용맹/부지런/게으름/대식가/호기심) | ①② | 하 | ✅ 명세+구현 |
| **F-B** | 침략 예고 시스템 (2~3일 전 신호) | ③ | 하 | ✅ 명세+구현, 검증 대기 |
| **F-C** | 보상 선포 (촌장 포상 걸기) | ④ | 하 | ⬜ 명세 미작성 |
| **F-D** | 숙련도 + 호칭 (견습→장인) | ② | 중하 | ⬜ 백로그 |
| **F-E** | 방랑자 이벤트 (랜덤 성격·숙련) | ①③ | 중하 | ⬜ 백로그 |
| **F-F** | 무역 제안 수락/거절 UI | ③④ | 중 | ⬜ 백로그 |

### 개발 원칙 (핵심 선언)
> **플래너 엔진은 동결됨. 모든 작업은 "플레이어가 화면에서 새로 보게 되는 것"으로 정의한다.**

- 새 기능 논의 시 항상 "어떤 재미 엔진에 기여하는가"와 "플레이어가 화면에서 보게 되는 것"을 먼저 질문
- 이야기 회상 테스트: "방금 판에서 남에게 말하고 싶은 순간이 있었나?"
- F 하나 끝날 때마다 1판 플레이 → "재밌어졌나?"만 확인

---

## 3. 게임 시스템 총람

### 자원 6종
- **Wood, Stone, Iron, Copper, Silver** (건자재/광물)
- **RawFood** (야생 채집), **CookedFood** (요리사 생산)

### 건물 9종 (완결도 우선순위 기획서 수치)
1. **Campfire** — 생존자 ≥ 1명 시. Wood 5
2. **House** — 생존자 ≥ 3명. Wood 20 + Stone 10
3. **Storehouse** — 총 자원 ≥ 80. 용량 100→200
4. **Forge** — GameTime ≥ 5일, Iron > 0. 마일스톤: 전사 합류
5. **Watchtower** — 침략 ≥ 1회. Wood 10 + Stone 30 + Iron 5
6. **TownHall** — GameTime ≥ 15일. Wood 35 + Stone 30 + Iron 6
7. **Alchemist Lab** — 겨울 대비 식량 보존 (미구현)
8. **Silver Citadel** — 최종 승리. Silver 50~60 (거의 전 맵 Silver 수거 필요)
9. (예비 확장 슬롯)

### 팩션 4개
- **플레이어** — 주민 마을
- **숲의 부족** — 식량 < 30 시 즉시 침략
- **철의 도시** — 철 < 15 시 즉시 침략
- **상인 연합** — TradeProposal 우선, 관계도 40+ 시 레이드 불가 (미구현 TODO)

### 계절 시스템 (14단계 완료)
- 4계절: Spring(0~9일) → Summer(10~19) → Autumn(20~29) → Winter(30~39) 반복
- 계절 결정 공식: `(Season)(Floor(gameTime / _seasonLengthDays) % 4)`
- 채집 비용 배율 (SeasonManager) 적용 대상: ChopWood/MineStone/MineIron/MineCopper
- 가을 시작 시 식량 GatherResources 가중치 ×3 자동 적용
- 겨울 경보: foodStock < (주민 수 × 3 × 30)

### Fog of War (13단계 완료)
- 3단계 상태: 0=미탐험(검정), 1=탐험됨(반투명 회색), 2=가시(원본)
- **Symmetric Shadowcasting 8-옥탄트** LOS (Adam Milazzo 알고리즘)
- 데이터: `byte[,] _fowState` (10KB) + `bool[,] _dirtyMask`
- 주민 시야 공유: 전역 배열 1개
- 매 웨이포인트 도착 시 `FowManager.RevealArea()` + `SensorSystem.DiscoverArea()` 동시 호출

### 주민 속성 (5개 + 3개 심리)
- **물리**: HealthLevel, HungerLevel, FatigueLevel, HappinessLevel
- **사회**: LoyaltyLevel (Hybrid C+D: GOAP Cost Modifier + Need-Conflict Score)
- **전투 정신**: Normal / Fear / Rage (별도 클래스 없이 FSM 상태로)

### 정신 상태 시스템 (8단계 완료)
- 3레이어 분리: 물리적 욕구(GOAP) / 심리적 상태(modifier) / 역할(액션 풀)
- 전투 정신 3상태의 전환 테이블:
  - `Normal → Fear`: HP < 30 && NearEnemy, 또는 아군 사망 목격(반경 5)
  - `Fear → Normal`: HP >= 40 && NearbyEnemyCount == 0
  - `Fear → Rage`: HP >= 60 && Rage 처치 목격(반경 6)
  - `Rage → Normal`: 8초 후 자동 (실효 7.4~8.0초)
- Rage 공격력 배율 1.5
- 메시지 전파: **VillagerFSM은 MessageBus 직접 구독 금지 → GameManager 팬아웃** (Fear 전염/Rage 전염 성능 안전)

### 성격 특성 6종 (F-A 완료)
- 겁쟁이, 용맹, 부지런, 게으름, 대식가, 호기심
- 각 특성이 GOAP 액션 비용 배율 또는 임계값 편차로 이어져 관찰 가능한 행동 차이 생성
- Docs/F-A_성격특성_명세서.md

### 침략 예고 시스템 (F-B 완료)
- 2~3일 전 신호(스테이지 머신 + UI 배너)
- 성격별 반응 편차 (겁쟁이는 미리 안전소 이동 등)
- Docs/F-B_침략예고_명세서.md
- 검증 대기 (Play 검증 미완, origin/main 미푸시)

---

## 4. 아키텍처: 계획층·실행층·감각층

### 3층 구조 총론

```
┌──────────────────────────────────────────────────────────┐
│  계획층 (Planning Layer) — 매 replan 시점                │
│  • GOAP A* Planner (Job System + Burst)                 │
│  • Action Registry (22개 Action)                        │
│  • Planning Slots (수치형 슬롯 12개 + 논리형 20개)      │
│  • Goal Arbiter (미구현, 미래 확장)                     │
└──────────────────────────────────────────────────────────┘
                          ↓ (Plan = [Action1, Action2, ...])
┌──────────────────────────────────────────────────────────┐
│  실행층 (Execution Layer) — Update / 0.1초 Tick          │
│  • VillagerFSM (10 상태) + EnemyFSM (5 상태)            │
│  • TileReservationRegistry (TR-1 이동 예약)             │
│  • BuildingQueue + BuildingSpawner (2026-07-13 신설)    │
│  • MessageBus (Pub-Sub)                                 │
└──────────────────────────────────────────────────────────┘
                          ↓ (관찰: Brain 플래그 갱신)
┌──────────────────────────────────────────────────────────┐
│  감각층 (Sensing Layer) — 0.1초 UpdateAllSensors        │
│  • SensorSystem (NearResource, NearEnemy, NearFireplace)│
│  • FowManager (LOS + 3단계 FoW)                         │
│  • ResourceNode Registry                                │
└──────────────────────────────────────────────────────────┘
```

### 계획층 상세 — GOAP A*

**World State 2레이어 구조 (필수)**
- **플래닝용 스냅샷**: 플래닝 시작 시점의 read-only 복사본 (`GOAPPlanningSlots.BuildCurrentState`)
- **실행용 Authoritative State**: 메인 스레드에서만 쓰기 (`AuthoritativeWorldState`)
- Dictionary는 멀티스레드 비안전 → Job System에서 **NativeArray<int> 슬롯 배열** 사용

**GOAPPlannerJob** (`Assets/Scripts/Core/GOAP/GOAPPlannerJob.cs`)
- **A* 알고리즘** + Closed Set (FNV-1a 해시 + 선형 프로빙 오픈 어드레싱)
- `MAX_NODES = 8192`, `MAX_DEPTH = 6`, `HEURISTIC_WEIGHT = 2.9f` (admissible)
- `ResultLength[0]` 특수값: `-1` = 이미 달성, `0` = NoSolutionFound, `>0` = 플랜 길이
- `Allocator.Persistent` (TempJob은 4프레임 제한으로 타임아웃 위험)
- **NodeGCosts와 NodeCosts 분리 저장** — 역산 버그 방지

**Closed Set 없으면 GOAP 폭발** (2026-07-06 긴급복구 S1):
- `MoveToBase → MoveToBase → …`처럼 동일 상태 재확장이 노드 8192개를 순식간에 소진
- 수치형 GOAP 활성화(UseNumericGoals=true) 시 특히 심각
- FNV-1a 해시로 방문 상태 O(1) 중복 체크로 해결

**전제조건 없는 Action의 함정**:
- MoveToBase, CraftPrimitiveWeapon 같은 무전제 Action에는 반드시 "이미 달성" 방어 조건 필요:
  - `MoveToBase` → `AtBase=0` 전제조건 추가
  - `CraftPrimitiveWeapon` → `HasPrimitiveWeapon=0` 전제조건 추가
- 그러지 않으면 A* 탐색 중 같은 상태에 무한 재적용 → MAX_NODES 소진

**액션 22개** (2026-07-06 기준 확정)
```
채집: ChopWood, MineStone, MineIron, MineCopper, HarvestWildBerries
소비: EatCookedFood, EatRawFood, CookMeal
휴식/치료: Sleep, RestOnGround, SeekMedicalAid
이동/탐험: MoveToBase, Explore
전투: CraftPrimitiveWeapon, CraftWeapon, AttackEnemy, FleeFromEnemy, AlertVillage
건설: BuildCampfire, BuildHouse, BuildStorehouse, BuildTownHall, BuildForge, BuildWatchtower
초기화용: SetTownHallBuilt
```

### 실행층 상세 — VillagerFSM 10상태

```
Idle → Planning → Executing → (Planning|Idle)
              ↓
         Replanning ← (플랜 실패, 자원 예약 실패, 이동 실패, 사전조건 위반)
              ↓
         Fighting (전투 진입 8단계)
              ↓
         Fleeing (Fear 상태 도주)
              ↓
         RefusingOrder (명령 거부, 9+10단계 UI)
              ↓
         Dead
              ↓
         LOD_FSM (30타일 초과 비전투, 5상태 간소화)
```

**LOD AI** (6A단계 확정)
- 30타일 초과 + 비전투 → LOD_FSM 진입 (0.5초 간격 처리)
- 전투 참여 또는 30타일 이내 진입 시 Full GOAP 복귀
- **주민 실효 탐색 공간 = 60×60 = 3,600 노드 (맵 크기 무관)**

**타일 예약 시스템** (TR-1, 2026-07-11)
- `TileReservationRegistry` 정적 클래스 — 유닛은 "현재 타일 + 다음 타일" 두 셀만 소유
- 다음 타일 예약 확보 순간 걷기 시작, 도착 순간 이전 타일 릴리즈
- 같은 타일 동시 소유 원천 불가 → 원운동/겹침/프리즈 3대 문제 해소
- `WAIT_MAX_FRAMES` 초과 실패 시 `AbortReason.PathBlocked`로 replanning

**메시지 전파**
- **VillagerFSM은 MessageBus를 직접 구독하지 않는다**
- GameManager가 구독 후 개별 FSM에 팬아웃
- 이유: 개별 FSM이 구독하면 Rage 전염/Fear 전염 이벤트에서 이터레이션 안전성 붕괴 위험
- RageKill: 반경 6타일(RAGE_FAN_RADIUS)만 팬아웃

**건물 완공 사이클** (2026-07-13 저녁 세션 신설)
- `CompleteBuildOrFallback(buildingId)` 단일 완료 처리 지점
- 큐 경로 우선: `BuildingQueue.CompleteBuildingByBuildingId` → CompleteBuilding → WorldState 플래그 + BuildingSpawner 스폰
- 폴백: WorldState 플래그만 직접 세팅 + 주민 현재 타일에 스폰
- `IsInPlaceBuildAction` — Build* 8종은 이동 스킵 (다들 (0,0) 몰림 방지)

### 감각층 상세 — SensorSystem

**갱신 대상 (Brain 플래그)**
- `AtBase, NearStorage, NearFireplace, NearBed, NearHealer, NearWatchtower`
- `NearResource, NearDiscoveredResource, NearEnemy, NearDroppedItem`

**감지 반경 (맨해튼 거리, 기획서 수치)**
- 자원 노드: 5타일
- 적 유닛: 8타일
- 건물: 3타일
- 드롭 아이템: 4타일

**런타임 건물 등록** (2026-07-13 신설)
- 기존 설계는 씬 배치 Transform 배열만 가정
- `RegisterBuildingAtTile(BuildingSensorKind, tileX, tileY)` API 추가
- Transform이 아닌 tile 좌표 직접 인자 — 서브시스템별 좌표 규칙 차이(X-Y vs X-Z) 회피

---

## 5. 알고리즘 상세

### 5.1 GOAP A* (계획)

**입력**: 현재 WorldState 슬롯 배열, 목표 슬롯 조건
**출력**: Action 시퀀스 (최대 6개)

**노드 확장 순서**:
1. 시작 상태를 Closed Set에 등록
2. 오픈 리스트에서 최소 f-cost 노드 팝
3. 목표 상태 도달 시 반환
4. 22개 Action 순회하며 Preconditions 만족하는 것 필터
5. Effects 적용한 새 상태 생성 → HashState → Closed Set 등록 시도
6. 이미 있으면 skip, 없으면 g-cost + h-cost 계산 → 오픈 리스트 푸시

**휴리스틱**: 목표까지의 남은 조건 수 × 2.9 (weight 2.9로 최단해 우선)

**Context Cost Multipliers** (Phase 1 확장)
- ChopWood/MineStone 등 채집 액션에 대해 노드 포화도 기반 배율
- `FULL_NODE_PENALTY = 5f` — 점유된 자원 노드는 5배 비용
- 결과: 여러 주민이 같은 노드에 몰리지 않음

**슬롯 시스템** (`GOAPPlanningSlots`)
- 수치형 슬롯 12개: WoodStock, StoneStock, IronStock, CopperStock, RawFoodStock, CookedFoodStock, MyHunger, MyFatigue, MyHealth 등
- 논리형 슬롯 20개: HasTool, HasWeapon, AtBase, NearFireplace, BuildingQueued, CampfireBuilt 등
- 음수 클램프 필수: `Mathf.Max(0, (int)avail)` — 예약 초과로 음수 나오면 GOAP 심도 초과

### 5.2 JPS (Jump Point Search) — 주민 경로탐색

**Why**: A* 대비 균일 비용 그리드에서 탐색 노드 최대 10배 감소.

**Implementation**: `Assets/Scripts/Core/JPSPathfinder.cs`
- 8방향 이동 (대각선 포함)
- `bool[,] walkable` (100×100, 초기값 전체 true)
- 좌표 오프셋: `arrayIdx = tileCoord + 50`

**PathResult 계약** (M17 게이트로 강제)
```csharp
enum PathResultKind {
    PathFound,      // Waypoints 유효
    AlreadyThere,   // 목표 = 현재 위치
    Unreachable     // 경로 없음
}
```

**결함 C 해소** (2026-07-05, ADR-M4):
- 이전: Unreachable 시 논리 좌표를 목표로 강제 스냅 → GOAP에 "성공"으로 위장 전달
- 신규: 이동 실패를 호출자에게 그대로 전달 → GOAP 재계획

### 5.3 Flow Field BFS — 적 팩션 경로탐색

**Why**: 팩션 15유닛이 기지로 동시 이동 시 A* 15회 → Flow Field 1회로 대체.

**Implementation**: `Assets/Scripts/Core/FlowFieldManager.cs`
- BFS 4방향 (대각선 미포함, 성능)
- 목표 = 플레이어 기지 (0, 0) 고정
- Awake 1회 빌드 → 팩션 전 유닛 O(1) 샘플링

### 5.4 Symmetric Shadowcasting — LOS (시야)

**Implementation**: `FowManager.cs`의 8-옥탄트 캐스팅
- Adam Milazzo 알고리즘
- **가시성이 대칭적**: A가 B를 볼 수 있으면 B도 A를 볼 수 있음 (게임 공정성)
- 현재 장애물 없음(`_blocking` 전부 false) → 원형 시야와 동일
- 건물 완성 시 `SetBlocking(x, y, true)`로 차단 추가 가능

### 5.5 TileReservationRegistry — 이동 예약 (TR-1)

**Implementation**: 정적 클래스, `Dictionary<Vector2Int, string>` 형태
- `IsOwnedBy(tile, agentId)` — 예약 조회
- `TryReserve(tile, agentId)` — 예약 시도, 실패면 false
- `Release(tile, agentId)` — 자기 소유일 때만 해제
- `ReleaseAllBy(agentId)` — 사망/AbortCurrentPath 시 leak 방지

**소유 규칙 (ADR-T3)**
- 유닛은 항상 정확히 두 셀 소유: 현재 타일 + 다음 타일
- 다음 타일 확보 → 걷기 시작
- 도착 → 이전 타일 릴리즈
- 결과: 같은 타일 동시 소유 원천 불가

### 5.6 GOAP 자원 발견 체인 (2026-07-03 확정)

**문제 3겹**:
1. ChopWood 전제조건이 `NearResource` (감지 반경 5) → Wood는 최소 10타일 → 영구 실패
2. `MoveTileForAction("Explore")`가 기지로 이동 (사실상 귀환)
3. `FowManager.RevealArea()`가 FoW 시각만 갱신, `IsDiscovered`는 미변경

**해결**:
- 채집 5개 전제조건 → `NearDiscoveredResource`
- Explore Effect: `NearDiscoveredResource=1` → 역추론 체인 가능
- `FindExplorationTarget()`이 미발견 노드 방향(1순위) 또는 랜덤 15타일(2순위)로 이동
- 매 웨이포인트 도착 시 `FowManager.RevealArea()` + `SensorSystem.DiscoverArea()` 동시 호출

**결과 플로우**:
```
초반: NearDiscoveredResource=false → GatherResources 목표
플래너: Explore(Effect:→1) → ChopWood(Prec:=1, HasTool=1) → 플랜 = [Explore, ChopWood]
실행: 미탐험 방향 이동 → DiscoverArea() 호출 → Wood 노드 발견
이후: MoveTileForAction("ChopWood") → FindNearestDiscoveredNode(Wood) → 채집
```

---

## 6. 건물·경제·전투 데이터

### 초기 자원 (GDD v0.4)
- 플레이어: Wood=10, Stone=5, RawFood=30, CookedFood=0, Iron=0, Copper=0
- 적 팩션: Wood=20, Stone=15, RawFood=50 (플레이어보다 강하게)
- 적 활성화 시점: `enemyActivationDay` (Inspector, 기본 10일)

### 자원 채취량 (ADR-7, YIELD_*)
| 액션 | YIELD | 소요 자원 |
|---|---|---|
| ChopWood | 10 | — |
| MineStone | 8 | HasTool 필요 |
| MineIron | 5 | HasTool 필요 |
| MineCopper | 3 | HasTool 필요 |
| HarvestWildBerries | 5 (RawFood) | — |
| CookMeal | 2 (CookedFood) | RawFood 3 소비 |
| EatCookedFood | -50 hunger | CookedFood 1 소비 |
| EatRawFood | -15 hunger | RawFood 1 소비 |
| Sleep | -90 fatigue | — |
| RestOnGround | -20 fatigue | — |
| SeekMedicalAid | +40 health | — |

### 자원 노드 재생 (기획서, 단위/game day)
- Wood: 5, Stone: 3, Iron: 1.5, Copper: 0.5, Silver: 0.2
- RawFood: **재생 없음** (요리사가 CookMeal로 생산)

### 전투 수치 (8단계)
```
EnemyFSM.BASE_DAMAGE_PER_TICK  = 10f
EnemyFSM.ATTACK_RANGE_TILES    = 2f (맨해튼)
VillagerFSM.BASE_VILLAGER_DAMAGE = 8f
VillagerFSM.ATTACK_RANGE_TILES  = 2f

FEAR_HEALTH_OVERRIDE     = 30f   (Fear 진입)
FEAR_RECOVERY_HEALTH     = 40f   (Fear 탈출)
FEAR_RAGE_REVERSE_HEALTH = 60f   (Rage 역전)
FEAR_CONTAGION_RADIUS    = 5f
RAGE_CONTAGION_RADIUS    = 6f
RAGE_DURATION_SEC        = 8f
RAGE_ATTACK_MODIFIER     = 1.5f
```

### playerStrength / factionStrength 공식
```
playerStrength = 주민수×10 + 전사×15 + 무기×8 + Watchtower×20 + Forge×15
숲의 부족 = 생존×10 + 25
철의 도시 = 생존×12 + 35
상인 연합 = 생존×8 + 20
```

### 침략 트리거 (GDD v0.4)
```
공통: (copperStock < 10 OR silverStock < 5)
      AND nearPlayerTerritory(25타일)
      AND playerStrength < factionStrength × 0.8
숲의 부족 추가: rawFoodStock < 30 → 즉시 침략
철의 도시 추가: ironStock < 15 → 즉시 침략
상인 연합:     TradeProposal 우선 (미구현 TODO)
```

### 충성도 시스템 — Hybrid C+D
**레이어 1 (GOAP Cost Modifier)**
```
Loyalty 70~100 → ExecutePlayerOrder Cost ×0.7
Loyalty 50~69  → ×1.0
Loyalty 30~49  → ×2.5
Loyalty  0~29  → ×6.0 (사실상 명령 무시)
```

**레이어 2 (Need-Conflict Score)**
```
ConflictScore = Σ(Need_urgency × Order_impact)
실효 거부 임계값 = 2.5 × (Loyalty / 50)
ConflictScore ≥ 임계값 → 명시적 거부 + UI 메시지
```

**핵심 원칙**: 거부는 랜덤이 아닌 현재 상태 기반 합리적 판단. 이유가 항상 설명 가능.

---

## 7. 실행 순서와 성능 예산

### Script Execution Order (2026-07-13 기준 최종)
```
MessageBus             -100  (수동)
BuildingQueue           -90  (수동)
SensorSystem            -85  (수동)
GameManager             -80  ([DefaultExecutionOrder(-80)])
SeasonManager           -75  ([DefaultExecutionOrder(-75)])
FactionAI               -70  ([DefaultExecutionOrder(-70)])
VillageAdvisor          -65  ([DefaultExecutionOrder(-65)])
MapChunkRenderer        -60  ([DefaultExecutionOrder(-60)])
FowManager              -55  ([DefaultExecutionOrder(-55)])
PlayerInputController   -20  ([DefaultExecutionOrder(-20)])
CameraController        -15  ([DefaultExecutionOrder(-15)])
HUDManager              -10  ([DefaultExecutionOrder(-10)])
MinimapController        -5  ([DefaultExecutionOrder(-5)])
VillagerFSM               0  (기본값)
EnemyFSM                  0  (기본값)
```

### 틱 실행 순서 (0.1초 코루틴, GameManager)
```
1. GameTime += 0.1 × _gameTimeScale
2. SensorSystem.UpdateAllSensors()
3. SensorSystem.TickResourceRegeneration(deltaGameDays)
4. SeasonManager.OnTick()
5. MessageBus.ProcessTick()          ← 이 시점에 VillagerDied 등 콜백 실행
6. TickVillagerGroup(_tickCounter % 6)  ← ProcessTick() 이후 필수
7. _tickCounter++
```

**주민 실효 틱 간격**: `0.1초 × 6그룹 = 0.6초`
**PLANNING_TIMEOUT_SEC = 3.0f** (실효 틱보다 충분히 커야 함 — 버그 3 교훈)

### 성능 예산 (60fps = 16.67ms/frame)
| 시스템 | 예산 |
|---|---|
| 렌더링 | ~6ms |
| 물리/NavMesh | ~2ms |
| UI + 기타 | ~1ms |
| AI 전체 | ~4ms |
| GOAP 플래닝 | ~1.8ms (Burst 적용) |
| 경로탐색 (비동기) | ~0.5ms |

### 대상 규모
- 60~70명 동시 Full GOAP + 30~40명 LOD FSM
- 100×100 타일 (확장 시 500×500까지 검토)

---

## 8. 디렉토리 구조와 어셈블리

### 프로젝트 루트
```
C:\Users\anjyo\AI_GOAP\
├── Assets/
│   ├── Scripts/
│   │   ├── AIVillage.asmdef       ← 게임 코드
│   │   ├── AI/
│   │   │   ├── VillagerFSM.cs     (실행층, 10 상태)
│   │   │   ├── VillagerBrain.cs   (POCO)
│   │   │   ├── VillagerEnums.cs
│   │   │   ├── EnemyFSM.cs
│   │   │   ├── EnemyBrain.cs
│   │   │   ├── PersonalityData.cs (F-A)
│   │   │   └── TileReservationRegistry.cs (TR-1)
│   │   ├── Core/
│   │   │   ├── GameManager.cs     (중앙 초기화·틱, -80)
│   │   │   ├── AuthoritativeWorldState.cs
│   │   │   ├── ResourceRegistry.cs (예약 시스템)
│   │   │   ├── ResourceNode.cs
│   │   │   ├── ResourceNodeSpawner.cs
│   │   │   ├── ResourceNodeSpawnConfig.cs (SO)
│   │   │   ├── ResourceNodeView.cs
│   │   │   ├── ActionDatabase.cs  (Action 정의)
│   │   │   ├── ActionDefinition.cs
│   │   │   ├── BuildingQueue.cs   (완공 API 포함)
│   │   │   ├── BuildingSpawner.cs (2026-07-13 신설)
│   │   │   ├── VillageAdvisor.cs  (자율 건물 결정)
│   │   │   ├── SensorSystem.cs    (감각층)
│   │   │   ├── MessageBus.cs      (Pub-Sub, -100)
│   │   │   ├── FactionAI.cs
│   │   │   ├── SeasonManager.cs
│   │   │   ├── FowManager.cs      (Symmetric Shadowcasting)
│   │   │   ├── MapChunkRenderer.cs
│   │   │   ├── MapConfig.cs       (SO)
│   │   │   ├── FlowFieldManager.cs
│   │   │   ├── JPSPathfinder.cs   (static)
│   │   │   ├── WorldStateIndices.cs
│   │   │   ├── WorldStateSnapshot.cs
│   │   │   ├── InvasionWarningIndicator.cs (F-B)
│   │   │   └── GOAP/
│   │   │       ├── GOAPPlannerJob.cs       (Burst, A*)
│   │   │       ├── GOAPPlannerScheduler.cs (컨텍스트 비용)
│   │   │       ├── GOAPActionRegistry.cs   (22 액션)
│   │   │       └── GOAPPlanningSlots.cs    (슬롯 인덱싱)
│   │   └── UI/
│   │       ├── PlayerInputController.cs (-20)
│   │       ├── HUDManager.cs            (-10)
│   │       ├── CameraController.cs      (-15)
│   │       ├── MinimapController.cs     (-5)
│   │       ├── ResourceHUD.cs
│   │       ├── VillagerStatusPanel.cs
│   │       ├── VillagerOverviewPanel.cs
│   │       ├── VillagerActionIcon.cs
│   │       ├── BuildingOrderPanel.cs
│   │       ├── BuildingQueuePanel.cs
│   │       ├── BuildingQueueItemView.cs
│   │       ├── BuildingCosts.cs (static 상수)
│   │       ├── RefusalBubble.cs
│   │       ├── GOAPDebugOverlay.cs
│   │       ├── RecruitmentPanel.cs
│   │       └── InvasionWarningIndicator.cs (F-B)
│   ├── Tests/
│   │   └── EditMode/
│   │       ├── AIVillage.Tests.EditMode.asmdef
│   │       ├── GOAPPlannerTests.cs
│   │       ├── M17_PathResultContract.cs
│   │       ├── T18_PersonalityGates.cs
│   │       └── T19_InvasionWarningGates.cs
│   ├── Prefab/
│   │   ├── Villagers_01.prefab
│   │   └── BuildingQueueItemPrefab_TEMP.prefab
│   ├── Scenes/SampleScene.unity
│   ├── ResourceNodeSpawnConfig/*.asset
│   ├── Settings/ (URP)
│   ├── Kenmi/ (Cute Fantasy RPG 픽셀 아트)
│   └── TextMesh Pro/Fonts/NeoDunggeunmoPro-Regular SDF
├── Docs/
│   ├── GDD_AI_Village_v0.1~v0.4.md
│   ├── TechSpec_AI_Village_v1.0.md
│   ├── AIBehaviorArchitecture_v1.0.md
│   ├── UnitySetupGuide.md
│   ├── AI_GOAP_확장설계_분석서.md
│   ├── Phase1_보완가이드.md
│   ├── Phase2_실행명세서.md
│   ├── Phase2_중간점검_보완가이드.md
│   ├── 이슈해결_가이드.md
│   ├── 게임이해_및_재미설계.md
│   ├── T13-T16_정합성테스트_명세서.md
│   ├── 방향2_이동실패_승격_명세서.md
│   ├── 방향3_무해_문맥배율_명세서.md
│   ├── F-A_성격특성_명세서.md
│   ├── F-B_침략예고_명세서.md
│   ├── 이동시스템_타일예약_명세서.md
│   ├── devlog-workflow.md
│   ├── CLAUDE.md (ADR 9종)
│   ├── 블로그_자동화_수익화_기획서.md
│   └── AI_Village_종합_개발_기록.md  ← 이 문서
├── ProjectSettings/
├── devlog/sessions/2026-06-*.md ~ 2026-07-13.md
└── tools/blog-automation/
```

### 어셈블리 정의 (2026-07-07 확정)
- **게임 코드**: `Assets/Scripts/AIVillage.asmdef` — `autoReferenced: true`
  - references: `Unity.TextMeshPro`, `Unity.Collections`, `Unity.Jobs`, `Unity.Burst`
- **EditMode 테스트**: `Assets/Tests/EditMode/AIVillage.Tests.EditMode.asmdef`
  - references: `UnityEngine.TestRunner`, `UnityEditor.TestRunner`, `AIVillage`, `Unity.Collections`, `Unity.Jobs`

### 렌더링 규칙 (2026-06-27 확정)
- **Unity 2D 프로젝트** — 카메라가 X-Y 평면(Z=0)을 봄
- 타일 좌표 → Transform 매핑: `new Vector3(TileX, TileY, 0f)`
- Gizmo 법선: `Vector3.forward` (3D의 `Vector3.up` 사용 금지)
- 모든 스프라이트는 Z=0, 깊이 정렬은 Sorting Layer로

---

## 9. 개발 진행 이력 (단계별)

### Phase 0 — 기획 (2026-06 초순)
- GDD v0.1 → v0.4 (Q&A 및 gdd-economy-auditor로 결정 확정)
- TechSpec v1.0
- AIBehaviorArchitecture v1.0

### Phase 1 — 코어 구축 (2026-06 중순 ~ 하순)
| 단계 | 완료일 | 리뷰 | 산출물 |
|---|---|---|---|
| 1: ResourceRegistry + WorldState 2레이어 | 2026-06 | APPROVED | 자원 예약 시스템 |
| 2: VillagerFSM 8상태 + LOD FSM 4상태 | 2026-06 | APPROVED | Villager 실행 층 |
| 3: MessageBus Pub-Sub | 2026-06 | APPROVED | 메시지 브로커 |
| 4: ActionDatabase + BuildingQueue | 2026-06 | APPROVED | Action 정의 + 건설 대기열 |
| 5A: SensorSystem + ResourceNode | 2026-06 | APPROVED | 감각 층 |
| 5B: GOAPPlannerJob A* + Scheduler | 2026-06 | APPROVED (2회) | Burst 컴파일 GOAP |
| 6A: GameManager 중앙 초기화·틱 | 2026-06 | APPROVED | 틱 시스템 |
| 6B: FactionAI + EnemyBrain/FSM | 2026-06 | APPROVED | 적 팩션 |
| 7: Transform 위치 연동 | 2026-06 | — | 타일 → 씬 동기화 |
| 8: 전투 기초 + 정신 상태 | 2026-06 | APPROVED (3회) | Fighting/Fleeing FSM, Fear/Rage 전염 |

### Phase 2 — 시스템 확장 (2026-06 하순 ~ 2026-07 초순)
| 단계 | 완료일 | 산출물 |
|---|---|---|
| VillageAdvisor | 2026-06-28 | 자율 건물 결정 (6 우선순위 규칙) |
| 9+10: 플레이어 입력 + UI 기초 | 2026-06-28 | 자원HUD, 주민패널, 건설버튼, 거부말풍선, GOAPDebug |
| 11: Win/Lose 조건 | 2026-06 | Silver Citadel 엔딩, 전멸 패배 |
| FactionAI 확장 | 2026-06-29 | 쿨다운·약탈·정찰 |
| 마일스톤 이벤트 | 2026-06-29 | Forge→전사 합류, Day7→탐험가 합류 |
| 경로 분산 | 2026-06-29 | 노드 포화도 + ID 기반 선호 |
| 12: JPS + FlowField | 2026-06-29 | 실제 경로이동 |
| 13: 타일 맵 + FoW | 2026-06-30 | MapConfig, MapChunkRenderer, FowManager (Symmetric Shadowcasting) |
| 14: 계절 시스템 | 2026-06-30 | SeasonManager, WinterCrisis |
| 15: 주민 모집 | 2026-07 | VillagerRecruitData SO, RecruitmentPanel |

### Phase 3 — 확장 명세 (2026-07 중순)
| 명세 | 완료일 | 산출물 |
|---|---|---|
| Phase 1 확장설계 | 2026-07-05 | 문맥 비용, 자원 발견 체인, 커스텀 GOAP |
| Phase 2 W1~W3 + N2~N3 | 2026-07-06 | 긴급복구 S0~S4, 이슈#1, #2, N3a 테스트, N3b 플랜 수선 |
| 방향 ① T13~T16 정합성 | 2026-07-06 | 실행명세서 작성 |
| 방향 ② 이동 실패 승격 | 2026-07-07 | ADR-M2~M6, PathResult 계약, M17 게이트 |
| 방향 ③ 무해 문맥 배율 | 2026-07-08 | 명세서 작성 |
| F-A 성격 특성 6종 | 2026-07-10 | PersonalityData SO, ADR-P1~P4 |
| F-B 침략 예고 | 2026-07-11 | InvasionWarningIndicator, T19 게이트, ADR-B1 |
| 이동시스템 타일 예약 (TR-1~3) | 2026-07-11~13 | TileReservationRegistry, VillagerFSM/EnemyFSM Rigidbody 제거 |

### Phase 4 — 현재 (2026-07-13)
**P0: 마을 시스템 점검·완성** (사용자 신규 지시)
- 건물 완공 사이클 5개 결함 해소 (BC1~BC5)
- BuildingSpawner 신설 (프리팹 + fallback 스프라이트)
- SensorSystem 런타임 등록 API
- Build 액션 제자리 실행
- 다음 안건: BuildStructure 목표 NoSolutionFound 루프

**적/침략 트랙 무기한 보류** (2026-07-13 사용자 결정)

---

## 10. 주요 결함과 학습

### 근본 진단 5개 층 (Docs/CLAUDE.md · project_planning_deadlock_diagnosis)
Planning/Deadlock 원인의 층위:
1. **레이어 1 — GOAP Precondition**: 전제조건이 논리적으로 만족 불가 (예: HasTool=1 but 도구 부여 액션 없음)
2. **레이어 2 — Numeric Goal 심도**: `(MAX_DEPTH - 1) × YIELD` 초과 불가
3. **레이어 3 — Registry ≠ 런타임 상수**: YIELD 불일치, Cost 불일치
4. **레이어 4 — Sensor 게이트**: NearResource vs NearDiscoveredResource
5. **레이어 5 — 이동 계약**: PathResult 3가지가 정확히 처리되어야 함

### 3대 근본 결함 (Docs/CLAUDE.md 등재)
- **결함 A**: Registry-런타임 상수 불일치 → 이슈 #1 해결 (2026-07-06)
- **결함 B**: Boolean 게이트가 수치 Prec을 가려버림 → BuildCampfire 등 정정 (2026-06-28)
- **결함 C**: 이동 실패가 GOAP에 "성공"으로 위장 전달 → ADR-M2~M6, PathResult 계약 (2026-07-07)

### 주요 버그 아카이브 (project_bugs_fixed 요약)
**초기 단계** (2026-06):
- 버그1: HasTool 기본값 false → GOAP 타임아웃 → Deadlock
- 버그3: PLANNING_TIMEOUT < 실효 틱 → 첫 폴링에서 즉시 실패
- 버그5: Gizmo `Vector3.up` → 2D에서 안 보임 → `Vector3.forward`

**GOAP 폭발 방지** (2026-07-05~07):
- B23~B25: 수치형 Goal MAX_DEPTH 초과 + Deadlock 재시도 루프
- B26: BuildCurrentState 슬롯 음수 클램프 부재
- 긴급복구 S1: Closed Set 부재 → FNV-1a 해시로 해결
- 긴급복구 S3: 도구 없는 주민 GatherWood 선택 → SelectGatherGoalId 가드
- 버그21: MoveToBase에 AtBase=0 Prec 추가, MAX_NODES 2048→8192

**Sprite/UI**:
- 버그11: SetActive→StartCoroutine 실패 → OnEnable 패턴
- 버그13: VillagerStatusPanel 위치 추적 → 뷰포트 앵커 방식
- 버그18: ResourceNodeSpawnConfig 색상 알파 0 → 자원 노드 전체 투명
- 버그19: RefusalBubble ToastPanel 코루틴 실패 → CanvasGroup alpha 방식
- 버그C7: WorldSpace TextMeshPro sizeDelta(0,0) → 오버플로우 렌더 → 정중앙 거대 표시

**전투/이동 시스템**:
- Awake sync fix (2026-07-13): 씬 배치 유닛 Brain.TileX/Y=0 몰림 → Awake에서 `transform.position → Brain.TileX/Y` 정합
- TR-1 완결: 원운동/겹침/프리즈 3대 문제 해소

**2026-07-13 저녁 세션 (BC1~BC5)**:
- BC1: BuildingQueue.CompleteBuilding() 호출부 0건 + FSM BuildCampfire case 누락 → CompleteBuildOrFallback로 단일 지점 통일
- BC2: 실제 GameObject 스폰 시스템 부재 → BuildingSpawner 신설
- BC3: SensorSystem이 런타임 건물 감지 못함 → RegisterBuildingAtTile API
- BC4: Build 액션이 default fallback으로 (0,0) 이동 → 타일 예약 충돌 → IsInPlaceBuildAction 조기 return
- BC5: BuildingSpawner FallbackColor 알파 0 + SortingOrder 음수 함정 → 자동 보정 + 기본값 상향

### 핵심 학습 요약

**GOAP 관련**
1. Closed Set 없으면 무전제 Action이 상태 재확장 → 노드 폭발
2. YIELD/Cost/MAX_DEPTH 관계식: `(MAX_DEPTH - 1) × YIELD >= 목표치` 필수
3. Explore 선행 1액션을 반드시 여유로 잡아둘 것
4. 슬롯 음수 클램프 필수 (`Mathf.Max(0, avail)`)
5. 무전제 Action에는 "이미 달성" 방어 조건 필수

**아키텍처 관련**
1. 씬 배치 assumption vs 런타임 확장의 균열 → 항상 `RegisterX` 런타임 API 설계
2. 단일 완료 처리 지점(single source of truth) 원칙 — 여러 경로 산재 시 하나만 고쳐도 다른 경로 실패
3. 좌표 규칙 서브시스템별 통일 강제 (X-Y vs X-Z, TileX/Y vs pos.x/z)
4. Inspector 신규 슬롯 필드 초기값의 직렬화 quirk — 코드 방어 필수

**UI 관련**
1. UI 패널이 자신의 코루틴을 가지면 SetActive로 자기 자신 비활성화 금지 → CanvasGroup alpha 방식
2. WorldSpace TextMeshPro는 sizeDelta 반드시 명시 (0,0이면 오버플로우 렌더)
3. UI 월드 추적은 뷰포트 앵커 방식이 Canvas Scaler 무관하게 가장 안전
4. TMP에서 이모지 사용 금지 (NeoDunggeunmoPro 등 한국어 폰트는 U+1F000 미지원)

---

## 11. 개발 방식 — 명세, 리뷰, 재미 우선

### 멀티 에이전트 파이프라인

**설계 → 구현 → 리뷰 4단계**:
1. `unity-ai-behavior-architect` — AI 행동 아키텍처 설계 (코딩 전)
2. `unity-senior-programmer` — 명세서 → C# 코드
3. `unity-code-reviewer` — 구조적 리뷰 (JSON 리포트)
4. `unity-pr-revision-coder` — 리뷰 피드백 적용

**보조**:
- `game-td-spec-analyzer` — GDD → 기술 명세 분해
- `game-qa-exploiter` — 익스플로잇/데드락 QA
- `gdd-economy-auditor` — 경제 밸런스 감사
- `unity-level-designer` — 맵/영토 설계
- `unity-performance-optimizer` — 60fps 병목 분석

**APPROVED 규칙**: 코드 리뷰가 APPROVED로 승인될 때까지 리뷰-수정 반복

### spec-write / spec-implement / spec-review 스킬

**spec-write**: 새 Phase/기능 실행명세서. Docs/에 마크다운으로 저장.
- W/F/P/N 항목 강제 형식
- "플레이어가 보는 것" 한 문단 필수 (재미 우선 원칙)

**spec-implement**: Docs/의 실행명세서를 구현.
- 명세 문항 대조 후 커밋

**spec-review**: 구현이 명세와 일치하는지 대조.

### session-log-append 규칙
- `devlog/sessions/YYYY-MM-DD.md`에 이어붙임
- 요약, 결정, 변경 파일, 다음 안건 순서

### CLAUDE.md ADR 9종 (2026-07 확정, 절대 불변)
- ADR-1~9: 플래너 코어 동결, 이동 실패 first-class, 성격 시스템, 침략 예고, 타일 예약 등

### 명세 예시 파일
- `Docs/Phase1_보완가이드.md` — 문맥 비용, 자원 발견 체인
- `Docs/Phase2_실행명세서.md` — W1~W3, N2~N3
- `Docs/방향2_이동실패_승격_명세서.md` — ADR-M2~M6, M17 게이트
- `Docs/F-A_성격특성_명세서.md` — 6종 특성, ADR-P1~P4
- `Docs/F-B_침략예고_명세서.md` — 스테이지 머신, ADR-B1
- `Docs/이동시스템_타일예약_명세서.md` — TR-1~6, ADR-T3~T6

### 자동 리뷰 파이프라인 (feedback_auto_review_pipeline)
- 코드 생성 → 리뷰 → 수정을 APPROVED까지 자동 반복
- 에이전트가 파일 미기입한 항목 검증 필수
- 사용자가 씬 배치 모르므로 코드 직접 분석 후 정확한 단계별 가이드 제공

### 블로그 자동화 (완결, 참조만)
- 개발 세션 로그를 소재로 개발 블로그 자동 발행 (RemoteTrigger + Blogger API)
- 검수-편집-마스터 5개 에이전트 파이프라인
- Cron: 월/수/금 13:03 KST

---

## 12. 현재 상태와 다음 방향

### 이번 세션 종료 시점 (2026-07-13 저녁)

**해결 완료**
- ✅ 마을 건물 완공 사이클 통합 (BC1~BC5 5개 결함 해소)
- ✅ Campfire 자동 큐잉 → 완공 → GameObject 스폰 → SensorSystem 등록 → 다음 우선순위(House) 진입까지 관측 확인
- ✅ 이동 리팩터(TR-1/2/3) 완결 검증 (원운동/겹침/프리즈 3대 문제)
- ✅ 초기 타일 sync fix (씬 배치본 Brain.TileX/Y=0 몰림)

**진행 중 / 다음 진입 안건**
- 🔴 **BuildStructure 목표 NoSolutionFound 루프** — 주민 B가 Campfire 완공 후에도 BuildStructure를 계속 잡고 replan 반복. Goal 선택 로직에서 이미 만족된 목표를 스킵하는 조건이 필요.
- 🟡 **6종 나머지 건물 매핑** — Campfire 안정 확인 후 House/Storehouse/TownHall/Forge/Watchtower/SilverCitadel의 Inspector 매핑
- 🟡 **Tech Tree 확장 시스템 명세** — 사용자 요청: 선행 건물 완공 축으로 잠금 해제

**보류**
- ⏸️ 적/침략 트랙 (사용자 지시로 무기한 보류)
- ⏸️ F-B 검증 (T19 EditMode + 씬 배치 + 10분 방치)
- ⏸️ TR-3 침략 시나리오 검증
- ⏸️ F-C 보상 선포 명세
- ⏸️ TR-4~6: 씬 컴포넌트 정리, EditMode 게이트, ADR-13 등재

### 로드맵 종합

**단기 (1~2 세션 내)**:
1. BuildStructure NoSolutionFound 루프 진단·해소
2. 6종 나머지 건물 프리팹/스프라이트 매핑
3. Tech Tree 확장 시스템 명세 (spec-write)

**중기 (2~4 세션)**:
1. Tech Tree 구현
2. 마을 사이클 완숙 (Gather → Deposit → Build → Rest → Reset이 자연스럽게 굴러가는지)
3. F-A 성격 특성 편차 관찰 튜닝 (관성 실험 후 튜닝)

**장기 (사용자 재개 결정 후)**:
1. F-B 침략 예고 검증 완료
2. F-C 보상 선포
3. F-D 숙련도+호칭
4. F-E 방랑자 이벤트
5. F-F 무역 UI

### 승리/패배 조건 (11단계 완료)
- **Win1**: Silver Citadel 완공 (Silver 50~60 축적)
- **Win2**: 주민 25명 도달
- **Win3**: (미확정, Prosperity)
- **Lose1**: 주민 전멸
- **Lose2**: TownHall 파괴 후 미재건 (TownHallEverBuilt 플래그로 오탐 방지)

---

## 13. 부록: ADR 요약

**ADR (Architecture Decision Records)** — Docs/CLAUDE.md에 총 13개 등재.

| ADR | 주제 | 규칙 |
|---|---|---|
| ADR-1 | 플래너 코어 동결 | GOAPPlannerJob/GOAPActionRegistry 이상 확장 금지 |
| ADR-2 | 자원 예약 원자성 | ReserveAll → Commit or Release, 부분 성공 금지 |
| ADR-3 | LOD 경계 | 30타일, 전투 진입/이탈 시 즉시 전환 |
| ADR-4 | 좌표계 통일 | 2D X-Y 평면, `new Vector3(x, y, 0f)` |
| ADR-5 | 스냅 grep 규칙 | `transform.position = ...Brain` 리터럴 금지 |
| ADR-6 | 메시지 팬아웃 | VillagerFSM은 MessageBus 직접 구독 금지 |
| ADR-7 | Registry-런타임 상수 단일 출처 | YIELD_*는 GOAPActionRegistry 상수만 참조 |
| ADR-8 | 이동 실패 first-class | PathResult 3분류, 강제 스냅 금지 |
| ADR-9 | GOAP Boolean 게이트 최소화 | 수치 Prec이 정확한 게이트, 임계값 불리언 지양 |
| ADR-M2~M6 | 방향 ② 이동 실패 승격 | 5개 결함, PathResult 계약, M17 게이트 |
| ADR-P1~P4 | F-A 성격 특성 | 6종 특성, 배율 정의, 관측 지표 |
| ADR-B1 | F-B 침략 예고 | 스테이지 머신, 성격 반응, T19 게이트 |
| ADR-T3~T6 | TR 타일 예약 | 두 셀 소유, 예약 실패 replanning, leak 방지 |

### 커밋 전 체크 7종
1. 실행 순서 확인 (Script Execution Order)
2. GOAP 상수-런타임 정합 (grep 리터럴)
3. asmdef 패키지 참조 완비
4. 씬 배치 가이드 검증
5. UI 코루틴은 OnEnable에서 시작
6. 알파/SortingOrder 명시 확인
7. 스냅 grep — `transform.position = ...Brain` 리터럴 0건

---

*문서 끝. 최종 업데이트 2026-07-13 저녁 세션 종료 시점.*
