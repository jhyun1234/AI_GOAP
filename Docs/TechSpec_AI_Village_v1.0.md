# AI Village — 기술 명세서 v1.0
## (GDD v0.1 기반, Technical Director 검토본)

---

## 0. 기획서 요약 (Executive Summary)

GOAP(Goal-Oriented Action Planning) AI를 핵심 기술로 사용하는 PC 콜로니 시뮬레이션. 주민 최대 50명이 각자 독립된 GOAP 플래너를 실행하며, 적 팩션 유닛도 동일한 GOAP 시스템으로 구동된다. 플레이어는 명령을 내릴 수 있지만 주민은 생존 위협 시 거부할 수 있으며, 이 점이 RimWorld와의 핵심 차별점이다.

- **기술적 복잡도: 매우 높음**
- **예상 개발 기간:**
  - 프로토타입 (GOAP 단일 유닛 + 기본 루프): 6~8주
  - 알파 (50유닛 + 팩션 AI + 기본 UI): 5~7개월
  - 베타 (전체 콘텐츠 + 성능 최적화): 10~14개월
  - 솔로 개발자 기준: 위 수치의 1.5~2배

---

## 1. 기술적 실현 가능성 및 리스크 평가

```
[리스크 ID] RISK-001
[경고 수준] 🔴
[대상 기능] GOAP 플래닝 — 50유닛 동시, 유닛당 < 0.5ms
[리스크 유형] 클라이언트 퍼포먼스

[문제 설명]
50유닛 × 0.5ms = 25ms → 60fps 프레임 예산(16.67ms) 이미 초과.
현재 Action 수 18개 기준 탐색 깊이 4~6 시 최악 18^4 = 104,976 노드.
메인 스레드 동기 실행 시 즉각적 프레임 드롭 발생.

[권장 해결책]
1. C# Job System + Burst Compiler로 플래닝 연산 오프로드
   → World State를 NativeArray<int>로 Blittable 구조 변환 필요
2. Re-plan 트리거를 '즉시'가 아닌 '다음 Tick 예약'으로 큐잉
3. Action 그래프를 빌드 타임에 Precomputed Graph로 사전 계산
4. 탐색 깊이 상한선 설정 (예: 최대 Depth 6), 해 없으면 Fallback Goal 전환
```

```
[리스크 ID] RISK-002
[경고 수준] 🔴
[대상 기능] 적 팩션 GOAP AI — 총 동시 동작 유닛 수
[리스크 유형] 클라이언트 퍼포먼스 + 아키텍처

[문제 설명]
플레이어 50 + 적 팩션 3개 × 5~10명 = 최대 65~80 유닛 동시 동작 가능.
성능 목표 "50유닛"과 실제 최대 유닛 수 사이의 갭이 기획서에 미정의.

[권장 해결책]
적 팩션 유닛 수 상한선 명확히 정의 (예: 적 총합 최대 30유닛).
원거리/화면 밖 유닛은 GOAP 대신 단순 FSM으로 전환 (LOD AI).
```

```
[리스크 ID] RISK-003
[경고 수준] 🔴
[대상 기능] World State 동기화 — Dictionary<string, object> 멀티 에이전트 동시 접근
[리스크 유형] 아키텍처 + 데이터 일관성

[문제 설명]
Dictionary<string, object>는 스레드 안전하지 않음.
두 주민이 동시에 foodStock을 읽고 둘 다 EatStoredFood 실행 시
Race Condition 발생 → 한 명은 없는 식량을 먹는 행동 실행.
Job System에서 접근 시 크래시.

[권장 해결책]
World State를 두 레이어로 분리:
  (A) 플래닝용 스냅샷(읽기 전용 복사본)
  (B) 실행용 Authoritative State(메인 스레드 전용 쓰기)
자원 예약(Reservation) 시스템 도입:
  가용 수량 = stock - reserved
  Action 시작 시 예약 증가, 완료/취소 시 해제
```

```
[리스크 ID] RISK-004
[경고 수준] 🟡
[대상 기능] Re-planning 빈도와 CPU 부하

[문제 설명]
전투 상황 등 World State 고속 변화 시 초당 수십 번 Re-plan 발생 가능.
50유닛 동시 Re-plan 요청 시 0.5ms 예산 순간적으로 수십 배 초과.

[권장 해결책]
Re-plan 쿨다운: 같은 유닛은 최소 0.3~0.5초에 1회만 허용.
현재 실행 중 Action의 Precondition이 여전히 유효하면 Re-plan 건너뜀.
P0 Goal 변화만 즉시 처리, 나머지는 다음 Tick으로 지연.
```

```
[리스크 ID] RISK-005
[경고 수준] 🟡
[대상 기능] NavMesh + GOAP 이동 비용 통합

[문제 설명]
GOAP Action Cost에 이동 비용이 고정값으로 표기됨.
실제 NavMesh 경로 길이는 맵 상황에 따라 동적으로 변함
→ 플래너가 선택한 "최적 경로"가 실제로는 최적이 아닐 수 있음.

[권장 해결책]
이동 비용을 Heuristic 추정값 사용 (유클리드 거리 × 타일 비용 계수).
비동기 경로 요청 결과를 Action 실행 전 캐싱.
동일 목적지 이동은 여러 유닛이 경로 공유 캐시 사용.
```

```
[리스크 ID] RISK-006
[경고 수준] 🟡
[대상 기능] 명령 거부 시스템 — P0 Goal 동시 발동

[문제 설명]
P0 Goal 3개(SurviveHunger / SurviveFatigue / SurviveInjury) 동시 발동 시
어느 것이 우선인지 기획서에 미정의.
Goal Thrashing: 두 Goal 사이를 매 Tick 오가며 아무것도 실행 못하는 교착 발생 가능.

[권장 해결책]
P0 서브 우선순위 확정:
  SurviveInjury > SurviveHunger > SurviveFatigue
  (체력 0 = 즉시 사망 > 허기 100 = 수분 위험 > 피로 100 = 기절)
또는 P0를 "SurviveSelf" 하나로 통합하고 내부에서 가중치로 처리.
```

```
[리스크 ID] RISK-007
[경고 수준] 🟢
[대상 기능] Fog of War + 100×100 타일 렌더링

[권장 해결책]
Fog of War를 1비트(탐험) + 1비트(현재 시야)로 Texture2D 관리.
주민 이동 시 해당 주민 주변 타일만 갱신(Dirty 마킹).
GPU 기반 Fog of War 셰이더(URP Custom Pass) 적용 권장.
```

---

## 2. 데이터베이스 및 스키마 설계

### VillagerState (주민 개별 상태)
```json
{
  "villagerId": "string (UUID)",
  "name": "string",
  "role": "enum: Lumberjack | Miner | Builder | Warrior | Medic | Cook | None",
  "isAlive": "bool",
  "stats": {
    "health": "float (0~100)",
    "hunger": "float (0~100)",
    "fatigue": "float (0~100)",
    "mood": "float (0~100)",
    "loyalty": "float (0~100)"
  },
  "inventory": {
    "hasTool": "bool",
    "toolType": "enum: Axe | Pickaxe | None",
    "hasWeapon": "bool",
    "hasFood": "bool",
    "hasRawFood": "bool"
  },
  "position": { "tileX": "int", "tileY": "int" },
  "locationFlags": {
    "atBase": "bool", "nearResource": "bool", "nearEnemy": "bool",
    "nearBed": "bool", "nearHealer": "bool", "nearForge": "bool",
    "nearFireplace": "bool", "nearStorage": "bool", "nearTree": "bool",
    "nearRock": "bool", "nearIronOre": "bool", "nearCopperOre": "bool",
    "nearBerryBush": "bool", "atBuildSite": "bool", "nearWatchtower": "bool"
  },
  "goap": {
    "currentGoalId": "string",
    "currentActionId": "string",
    "lastReplanTimestamp": "float",
    "replanCooldownRemaining": "float",
    "isExecutingPlan": "bool"
  }
}
```

### WorldState (GOAP 전역 상태)
```json
{
  "gameTime": {
    "dayCount": "int",
    "hourOfDay": "float (0~24)",
    "season": "enum: Spring | Summer | Autumn | Winter"
  },
  "resourceStock": {
    "food": "float", "wood": "float", "stone": "float",
    "iron": "float", "copper": "float", "silver": "float"
  },
  "resourceReservations": {
    "food_reserved": "float", "wood_reserved": "float", "stone_reserved": "float",
    "iron_reserved": "float", "copper_reserved": "float", "silver_reserved": "float"
  },
  "buildings": {
    "campfireBuilt": "bool", "townHallBuilt": "bool", "forgeBuilt": "bool",
    "storehouseBuilt": "bool", "watchtowerBuilt": "bool", "commTowerBuilt": "bool",
    "alchemistLabBuilt": "bool", "silverCitadelBuilt": "bool", "houseCount": "int"
  },
  "threats": {
    "enemyNearby": "bool", "nearestEnemyFactionId": "string | null",
    "activeRaidCount": "int", "allVillagersAlert": "bool"
  },
  "buildingQueued": "bool",
  "playerAutomationLevel": "enum: FullAuto | SemiAuto | Manual"
}
```

### ResourceStock (자원 노드 — 맵 개별 노드)
```json
{
  "nodeId": "string (UUID)",
  "resourceType": "enum: Wood | Stone | Iron | Copper | Silver | Food | Berry",
  "tileX": "int", "tileY": "int",
  "currentAmount": "float", "maxAmount": "float",
  "depletionRate": "float",
  "regenerationRate": "float (0이면 재생 없음)",
  "isDepletable": "bool",
  "factionControlId": "string | null",
  "isBeingHarvested": "bool",
  "harvestingVillagerId": "string | null",
  "requiredTool": "enum: Axe | Pickaxe | None",
  "requiredBuilding": "string | null"
}
```

### BuildingQueue (건설 큐)
```json
{
  "queueId": "string (UUID)",
  "buildingType": "enum: House | Storehouse | Campfire | TownHall | ...",
  "targetTileX": "int", "targetTileY": "int",
  "requiredResources": { "wood": "float", "stone": "float", "iron": "float", "copper": "float", "silver": "float" },
  "status": "enum: Pending | ResourcesBeingGathered | InProgress | Completed | Cancelled",
  "assignedVillagerId": "string | null",
  "progressPercent": "float (0~100)",
  "priority": "enum: P0 | P1 | P2 | P3"
}
```

### FactionRelation (팩션 관계)
```json
{
  "factionAId": "string", "factionBId": "string",
  "relationScore": "float (0~100)",
  "relationState": "enum: FullWar | Hostile | Neutral | Friendly | Allied",
  "factionAGoapState": {
    "currentGoal": "string",
    "resourceDeficits": { "copper": "float", "silver": "float" },
    "isRaiding": "bool",
    "raidTargetFactionId": "string | null"
  },
  "events": [{ "eventType": "string", "scoreDelta": "float", "gameTime": "float" }]
}
```

### GOAPActionLog (행동 로그 — 디버그 전용)
```json
{
  "villagerId": "string",
  "goalId": "string",
  "planSnapshot": [{ "actionId": "string", "estimatedCost": "float", "actualCost": "float | null" }],
  "worldStateSnapshot": "object",
  "planResult": "enum: Success | NoSolutionFound | Deadlock | Interrupted | Replanned",
  "replanReason": "string | null",
  "executionStartTime": "float",
  "tickIndex": "int"
}
```

---

## 3. 어셋 자동 추출 및 분류

### UI/UX 어셋
| 어셋명 | 설명 | 우선순위 |
|---|---|---|
| 주민 상태 패널 | Health/Hunger/Fatigue/Mood/Loyalty 바 | P0 |
| 자원 현황 HUD | 6종 자원 수량 표시 | P0 |
| 건설 큐 패널 | 대기 건물 + 진행률 | P0 |
| 명령 거부 말풍선 | 5가지 거부 메시지 + 아이콘 | P0 |
| GOAP 디버그 오버레이 | Goal/Action/비용 실시간 표시 | P0 (개발용) |
| 팩션 관계 UI | 관계 수치 + 상태 아이콘 | P1 |
| 자동화 레벨 토글 | FullAuto/Semi/Manual 전환 | P1 |
| 미니맵 | FoW 반영, 자원/건물 아이콘 | P1 |
| 역할 지정 UI | 주민 역할 할당 드래그앤드롭 | P1 |
| 이벤트 알림 토스트 | 레이드 경고, 주민 위기 알림 | P1 |
| 승리/패배 화면 | 단계별 승리 연출 | P2 |

### 3D/2D 아트 어셋
| 어셋명 | 우선순위 |
|---|---|
| 주민 캐릭터 모델 (6종 역할) | P0 (임시 캡슐 가능) |
| 타일셋 (평지/숲/암석/수역/위험) | P0 |
| 자원 노드 프롭 (나무, 돌) | P0 |
| 건물 모델 (House, Campfire) | P0 |
| 나머지 자원 노드 (철/구리/은/베리) | P1 |
| 나머지 건물 모델 7종 | P1 |
| 적 팩션 유닛 모델 (3개 팩션) | P1 |
| 몬스터 모델 3종 | P1 |
| Fog of War 셰이더 (URP) | P1 |
| 자연재해 VFX | P2 |
| 계절 환경 변화 (눈/단풍/봄꽃) | P2 |

### 애니메이션
| 어셋명 | 우선순위 |
|---|---|
| 주민 이동 (Walk/Run) | P0 |
| 주민 자원 수집 (ChopWood/Mine) | P0 |
| 주민 수면/식사 | P1 |
| 주민 전투 (Attack/Flee) | P1 |
| 주민 건설 | P1 |
| 명령 거부 리액션 | P2 |

### 사운드
| 어셋명 | 우선순위 |
|---|---|
| 주민 작업 SFX (벌목/채광) | P0 |
| 레이드 경보 SFX | P1 |
| 전투 SFX | P1 |
| 배경 앰비언트 (계절별 4종) | P1 |
| UI 피드백음 | P1 |
| BGM (평화/긴장/전투 상태 전환형) | P2 |

---

## 4. 예외 처리 로직 요구사항

### EX-001: GOAP 플래너 Deadlock (해결 불가 상태)
```
발동: 최대 탐색 깊이 도달 또는 전체 경로 탐색 후 해 없음 판정
처리:
  1. GOAPActionLog에 "NoSolutionFound" 기록
  2. Fallback 체계:
     a. Goal 우선순위 한 단계 낮춰 재플래닝 (P0는 낮추지 않음)
     b. 안전 행동 실행: hunger < 50 → RestOnGround, 그 외 → MoveToBase
  3. 3회 연속 Deadlock → "도움 필요" 플래그 + UI 알림 아이콘
  4. 플레이어 개입(역할 변경/자원 투입) 시 Deadlock 해소 트리거
```

### EX-002: Re-planning 무한 루프 방지
```
처리:
  1. 유닛당 Re-plan 카운터: 동일 Goal에 Tick당 최대 1회
  2. Re-plan 쿨다운: 0.3~0.5초 대기
  3. 이전 플랜 == 새 플랜이면 Re-plan 폐기, 현재 플랜 유지
  4. 30초 이내 10회 초과 시: 현재 Goal 포기 → 다음 우선순위 Goal 전환
  5. P0 Goal은 쿨다운 예외 (즉시 처리, Depth 3 이하 제한)
```

### EX-003: P0 Goal 동시 발동 충돌
```
처리:
  서브 우선순위: SurviveInjury > SurviveHunger > SurviveFatigue
  hunger > 80 AND fatigue > 90 동시:
    hasFood == true → EatStoredFood 우선 (Cost 2, 빠름)
    hasFood == false → RestOnGround 후 식량 확보
  복합 위기 상태를 UI 다색 아이콘으로 표시
```

### EX-004: 적 팩션 + 플레이어 동시 자원 노드 경합
```
처리:
  자원 노드 점유권(Claim) 시스템:
    harvestingVillagerId로 1유닛만 수집 허용
  동시 도달 시 팩션 관계값 기준:
    관계 < 30 → 전투 발생
    관계 30~60 → Claim 선착순, 패자는 Re-plan
    관계 > 60 → 교대 수집 협상 이벤트 트리거
  동시 Claim: villagerId 해시값으로 결정적(Deterministic) 우선순위
```

### EX-005: World State 동기화 실패 (자원 중복 수집)
```
처리:
  예방(플래닝): 플래닝 시 resourceReservations에 필요 수량 예약
    가용 수량 = stock - reserved
  실행 직전 재검증:
    가용 수량 부족 시 Action 취소 + Re-plan 트리거
    "[WARN] Reservation miss" 경고 로그
  예약 해제 타이밍 (메인 스레드 전용):
    시작: reserved += amount
    성공: stock -= amount, reserved -= amount
    실패/취소: reserved -= amount
  stock이 음수가 되면 Mathf.Max(0, stock) 하드 클램프
```

---

## 5. 기획서 미비점 및 개발팀 질의사항

| 번호 | 질문 | 관련 기능 | 중요도 |
|---|---|---|---|
| Q01 | 최대 총 유닛 수는? 플레이어 50 + 적 팩션 합산인가, 플레이어만 50인가? 팩션별 레이드 유닛 수 상한선 정의 필요 | GOAP 성능 목표 | 매우 높음 |
| Q02 | P0 Goal 동시 발동 시 서브 우선순위 기준? SurviveInjury > SurviveHunger > SurviveFatigue 순서 확정 필요 | Goal 우선순위 | 매우 높음 |
| Q03 | loyaltyLevel < 30일 때 명령 거부 확률 공식? (예: loyalty=0 → 100%, loyalty=30 → 50%) | 명령 거부 시스템 | 높음 |
| Q04 | 주민 사망 처리: 인벤토리 드롭 여부? 예약 자원 해제 방식? 다른 주민 Re-plan 트리거? | 주민 시스템 | 높음 |
| Q05 | 주민 신규 채용 메커니즘: 모집 비용/초기 속성/역할 지정 방식 미정의 | 주민 시스템 | 높음 |
| Q06 | 적 팩션 AI 초기 자원 상태? Day 1부터 활성화? 팩션 기지 위치? | 적 팩션 AI | 높음 |
| Q07 | Fog of War에서 GOAP 플래닝: 미탐험 영역 자원 노드 제외? 예상 위치 제공? | FoW + GOAP | 높음 |
| Q08 | 계절 변화가 GOAP Action Cost에 미치는 영향? 겨울 연료 2배가 World State에 어떻게 반영? | 계절 + GOAP | 보통 |
| Q09 | Full Auto 모드에서 플레이어가 할 수 있는 행동은? 단순 관전인가, 언제든 개입 가능한가? | 자동화 레벨 | 보통 |
| Q10 | Silver Citadel 완성 후 게임 종료 방식? 엔딩 연출 후 메뉴, 또는 샌드박스 전환? | 승리 조건 | 보통 |
| Q11 | AssessPlayerStrength 구현 방법: 적이 플레이어 정보를 탐지하는 메커니즘 미정의 | 적 팩션 GOAP | 보통 |
| Q12 | 자원 노드 재생 여부: 나무는 시간 후 재생? 철광석은 영구 고갈? | 자원 시스템 | 보통 |
| Q13 | 주민 간 상호작용: 요리사가 다른 주민을 위해 요리하는 방식? SeekMedicalAid에서 Medic이 이동하는 주체? | 주민 시스템 | 낮음 |
| Q14 | 건설 다중 주민 허용? 다수 참여 시 속도 증가? 단일 주민 전용? | 건설 시스템 | 낮음 |

---

*기술 명세서 v1.0 — GDD v0.1 기반 / 작성일: 2026-06-25*
