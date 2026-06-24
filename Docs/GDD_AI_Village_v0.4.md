# AI Village 게임 기획서 v0.4
## GOAP 기반 자율 생존 마을 시뮬레이션

---

## 변경 이력

| 버전 | 날짜 | 주요 변경 사항 |
|---|---|---|
| v0.1 | 2026-06-25 | 최초 초안 작성 |
| v0.2 | 2026-06-25 | 경제 밸런스 수정, GOAP Dead-end 해소, 성능 유닛 상한 확정, 충성도 시스템 재설계, P0 서브 우선순위 확정 |
| v0.3 | 2026-06-25 | 2단계 식량 시스템, 자원 노드 재생, FoW 탐험 연동, 주민 사망 처리(DroppedItem) |
| v0.4 | 2026-06-25 | 주민 모집 시스템(Q05), 팩션 초기 상태·활성화(Q06), AssessPlayerStrength(Q11), 겨울 연료 처리(Q08), 건설 다중 주민(Q14), 게임 시작 주민 수 확정 |

---

## 1. 게임 개요 (Executive Summary)

| 항목 | 내용 |
|---|---|
| 게임 제목 | AI Village |
| 장르 | 콜로니 시뮬레이션 / 생존 전략 |
| 플랫폼 | PC (Windows) |
| 엔진 | Unity (URP) |
| 타겟 유저 | 전략·시뮬레이션 팬, RimWorld 류 게임 팬 |
| 핵심 기술 | GOAP (Goal-Oriented Action Planning) AI |
| 참고 게임 | RimWorld (Ludeon Studios) |

### 핵심 컨셉
> "플레이어가 지시하지 않아도 주민들이 스스로 목표를 세우고 행동 계획을 짜서 마을을 만들어 생존한다."

GOAP AI를 탑재한 주민(Villager)들이 자신의 필요(Need)를 충족하기 위해 최적의 행동 시퀀스를 자율 계획·실행한다. 플레이어는 "마을 지도자(Village Leader)" 역할로 큰 방향을 제시하지만, 주민들은 생존을 최우선으로 명령을 수행하거나 거부할 수 있다.

### RimWorld와의 핵심 차별점
| 요소 | RimWorld | AI Village (GOAP) |
|---|---|---|
| AI 방식 | 작업 우선순위 목록 선택 | Goal → 멀티스텝 Action 체인 자동 계획 |
| 행동 계획 | 단일 최우선 작업 선택 | A* 기반 동적 플래닝 + Re-planning |
| 명령 거부 | 없음 (항상 수행) | 필요 충돌 점수 + 충성도 기반 자율 거부 |
| 팩션 AI | 스크립트 기반 레이드 | 팩션도 GOAP로 구동 — 자원 부족 시 자율 침략 결정 |

---

## 2. 핵심 게임플레이 루프

### 매크로 루프 (장기 목표)
```
마을 건설 → 자원 확보 → 주민 성장 → 위협 극복 → 영토 확장 → 적 팩션 제압 → 생존 달성
```

### 미드 루프 (중기 사이클)
```
자원 고갈 감지 → 수집 명령/자율 수집 → 건물 건설 → 방어선 구축 → 위협 대응
```

### 마이크로 루프 (GOAP 핵심)
```
주민 상태 변화 → Goal 활성화 → GOAP 플래너 실행 → 최적 Action 시퀀스 도출 → 실행 → World State 반영
```

---

## 3. GOAP AI 시스템

### 3.1 World State 정의

```
// 주민 개인 상태 (Per-Agent)
isAlive           : bool
hungerLevel       : float   // 0~100, 80 이상 = SurviveHunger 발동
fatigueLevel      : float   // 0~100, 90 이상 = SurviveFatigue 발동
healthLevel       : float   // 0~100, 20 이하 = SurviveInjury 발동
loyaltyLevel      : float   // 0~100
hasTool           : bool
hasWeapon         : bool
hasPrimitiveWeapon: bool
atBase            : bool
nearResource      : bool
nearEnemy         : bool
nearDroppedItem   : bool

// 마을 전역 상태 (Global)
rawFoodStock      : float   // 채집/사냥으로 얻은 재료 창고
cookedFoodStock   : float   // 요리사가 완성한 식량 창고
woodStock         : float
stoneStock        : float
ironStock         : float
copperStock       : float
silverStock       : float

// 자원 예약
rawFoodReserved   : float
cookedFoodReserved: float
woodReserved      : float
stoneReserved     : float
ironReserved      : float
copperReserved    : float
silverReserved    : float

enemyNearby       : bool
buildingQueued    : bool
townHallBuilt     : bool
forgeBuilt        : bool
storehouseBuilt   : bool
droppedItems      : List<DroppedItem>
```

### 3.2 Goal 목록 및 우선순위

| 우선순위 | Goal | 발동 조건 |
|---|---|---|
| **P0-1** | SurviveInjury | healthLevel < 20 |
| **P0-2** | SurviveHunger | hungerLevel > 80 |
| **P0-3** | SurviveFatigue | fatigueLevel > 90 |
| P1 | DefendVillage | enemyNearby == true |
| P2 | ExecutePlayerOrder | ConflictScore < threshold AND loyaltyLevel 조건 충족 |
| P3 | BuildStructure | buildingQueued == true AND 자원 충족 |
| P4 | GatherResources | anyAvailableStock < 30 (rawFood + cookedFood 합산 포함) |
| P5 | Explore | allAvailableStocks >= 50 AND unexploredTilesNearby == true |

### 3.3 Action 목록

#### 생존 Actions
| Action | Precondition | Effect | Cost |
|---|---|---|---|
| EatCookedFood | cookedFoodStock>=1, atBase=true | cookedFoodStock-=1, hungerLevel-=50 | 2 |
| EatRawFood | rawFoodStock>=1 | rawFoodStock-=1, hungerLevel-=15 | 4 |
| HarvestWildBerries | nearBerryBush=true, berryBush.isDiscovered=true | rawFoodStock+=5 | 5 |
| CookMeal | rawFoodStock>=3, nearFireplace=true | rawFoodStock-=3, cookedFoodStock+=2 | 8 |
| Sleep | nearBed=true, enemyNearby=false | fatigueLevel-=90 | 10 |
| RestOnGround | (없음) | fatigueLevel-=20 (비상용) | 12 |
| SeekMedicalAid | nearHealer=true | healthLevel+=40 | 5 |

#### 자원 수집 Actions
| Action | Precondition | Effect | Cost |
|---|---|---|---|
| ChopWood | nearTree=true, tree.isDiscovered=true, hasTool(Axe)=true | woodStock+=10 | 4 |
| MineStone | nearRock=true, rock.isDiscovered=true, hasTool(Pickaxe)=true | stoneStock+=8 | 5 |
| MineIron | nearIronOre=true, ironOre.isDiscovered=true, hasTool(Pickaxe)=true | ironStock+=5 | 6 |
| MineCopper | nearCopperOre=true, copperOre.isDiscovered=true, hasTool(Pickaxe)=true, forgeBuilt=true | copperStock+=3 | 9 |
| PickUpTool | nearStorage=true | hasTool=true | 1 |
| PickUpDroppedItem | nearDroppedItem=true | hasItem=true (아이템 타입별) | 1 |

#### 건설 Actions
| Action | Precondition | Effect | Cost |
|---|---|---|---|
| BuildHouse | wood>=20, stone>=10, atBuildSite=true | houseBuilt=true | 15 |
| BuildStorehouse | wood>=15, stone>=5 | storageCapacity×2 | 10 |
| BuildCampfire | wood>=5 | nearFireplace=true | 3 |
| BuildTownHall | wood>=35, stone>=30, iron>=6 | townHallBuilt=true | 28 |
| BuildForge | stone>=20, iron>=15, townHallBuilt=true | forgeBuilt=true | 20 |
| BuildWatchtower | stone>=30, iron>=5 | enemyDetectRange×2 | 12 |
| BuildCommTower | wood>=30, stone>=20, copper>=20 | diplomacyUnlocked=true | 25 |
| BuildAlchemistLab | wood>=15, stone>=10, iron>=5, copper>=5 | alchemistLabBuilt=true | 18 |
| BuildSilverCitadel | wood>=60, stone>=50, iron>=30, copper>=20, silver>=15 | victory=true | 50 |

#### 전투 Actions
| Action | Precondition | Effect | Cost |
|---|---|---|---|
| AttackEnemy | hasWeapon=true, nearEnemy=true, healthLevel>40 | enemyDefeated=true | 7 |
| AttackEnemyWeak | hasPrimitiveWeapon=true, nearEnemy=true, enemyTier=1, healthLevel>40 | enemyDefeated=true | 10 |
| FleeFromEnemy | nearEnemy=true, healthLevel<40 | nearEnemy=false, atBase=true | 3 |
| CraftPrimitiveWeapon | wood>=3, stone>=2 | hasPrimitiveWeapon=true | 6 |
| CraftWeapon | iron>=5, nearForge=true | hasWeapon=true | 8 |
| AlertVillage | nearWatchtower=true | allVillagersAlert=true | 2 |

#### 탐험 Actions
| Action | Precondition | Effect | Cost |
|---|---|---|---|
| Explore | unexploredTilesNearby=true | 주변 15타일 내 자원 노드 isDiscovered=true | 6 |
| MoveToUnexplored | unexploredTilesNearby=true | 미탐험 타일로 이동 | 이동 거리 비례 |

### 3.4 GOAP 플래닝 예시 (주요 시나리오)

**시나리오 A:** Day 1, hungerLevel=82, cookedFoodStock=0, rawFoodStock=5
```
경로 A (Cost 4):  EatRawFood — 즉시 실행 (hungerLevel-=15)
경로 B (Cook 있을 시, Cost 10): CookMeal → EatCookedFood (hungerLevel-=50)
→ 요리사 주민은 B 선택 (효율 우선). 비요리사는 A 선택 (즉시 해결).
```

**시나리오 B:** 탐험된 나무 전부 고갈
```
ChopWood Precondition: tree.isDiscovered=true ❌ → Action 제외
→ GOAP 자동 Explore Goal 선택 → 미탐험 이동 → isDiscovered=true
→ Re-plan: ChopWood 실행 가능 ✅
```

**시나리오 C:** 주민 사망 후 자원 대응
```
전사 Bran 사망 → DroppedItem(Axe, tileX=45, tileY=32) 생성
                → wood 예약 5 즉시 해제 → 다른 주민 건설 진행 가능

나무꾼 Ceri (nearDroppedItem=true, hasTool=false):
  PickUpDroppedItem(Axe) → hasTool=true → ChopWood 가능 ✅
```

### 3.5 Re-planning 규칙

```
Re-plan 발동 조건:
  - 현재 Action의 Precondition이 무효화된 경우
  - P0 Goal 새로 활성화 (즉시 처리)
  - 탐험으로 isDiscovered=true 노드 생성 → GatherResources Re-plan

제한:
  - 쿨다운: 0.3~0.5초
  - 탐색 깊이 상한: Depth 6
  - 동일 Goal 30초 내 10회 초과 → 다음 우선순위 Goal 전환

Fallback:
  - NoSolutionFound → RestOnGround 또는 MoveToBase
  - 3회 연속 Deadlock → "도움 필요" UI 아이콘
```

---

## 4. 주민(Villager) 시스템

### 4.1 게임 시작 초기 주민 구성 (v0.4 확정)

```
시작 주민: 3명 (고정)
  ├── 나무꾼(Lumberjack) 1명  — 초반 Wood 수집 전담
  ├── 요리사(Cook) 1명        — Day 1부터 cookedFood 생산 (식량 위기 방지)
  └── 건설자(Builder) 1명     — Town Hall 건설 속도 확보

설계 의도:
  요리사 없이 시작하면 rawFood 효율(허기 -15)로 Day 3 이전에 식량 위기 발생 위험
  → 초반 메커니즘 학습 기회 없이 패배 가능성 높음
  → 요리사를 고정 지급하여 2단계 식량 시스템을 자연스럽게 체험
```

### 4.2 주민 속성
| 속성 | 범위 | 위험 임계값 | 설명 |
|---|---|---|---|
| 체력(Health) | 0~100 | 20 이하 → P0-1 | 전투·사고로 감소, 0 = 사망 |
| 허기(Hunger) | 0~100 | 80 이상 → P0-2 | +3/시간 자연 증가 |
| 피로(Fatigue) | 0~100 | 90 이상 → P0-3 | 활동으로 증가, 수면으로 감소 |
| 행복도(Mood) | 0~100 | 20 이하 = 반란 위험 | 환경·이벤트로 변동 |
| 충성도(Loyalty) | 0~100 | 구간별 효과 (§4.4) | 명령 수행 의지 |

### 4.3 주민 역할(Role) — Cost 보정
| 역할 | 특화 Action Cost 감소 | 비특화 Cost 증가 |
|---|---|---|
| 나무꾼(Lumberjack) | ChopWood -50% | MineStone +30% |
| 광부(Miner) | Mine* -50% | ChopWood +30% |
| 건설자(Builder) | Build* -40% | Combat +50% |
| 전사(Warrior) | Attack* -40% | Build* +60% |
| 의료사(Medic) | SeekMedicalAid -50% | Mine* +40% |
| 요리사(Cook) | CookMeal -60% | Attack* +80% |

### 4.4 충성도(Loyalty) 시스템 — Hybrid C+D

#### 레이어 1: GOAP Cost Modifier
| 충성도 구간 | Cost 배율 |
|---|---|
| 70~100 | ×0.7 |
| 50~69 | ×1.0 |
| 30~49 | ×2.5 |
| 0~29 | ×6.0 |

#### 레이어 2: Need-Conflict Score
```
ConflictScore = Σ (Need_urgency[i] × Order_impact[i])
실효 거부 임계값 = 2.5 × (loyaltyLevel / 50)
ConflictScore ≥ 임계값 → 명시적 거부 + UI 메시지
```

#### 충성도 변화 이벤트
| 이벤트 | 변화 |
|---|---|
| 식량/물자 선물 | +10 |
| 주거 환경 개선 | +5/일 |
| 성공적 전투 생환 | +15 |
| 명령 강제 이행 | -20 |
| 주민 사망 목격 | -10 |
| 전투 패배 도주 | -5 |

#### 거부 메시지 테이블
| 거부 조건 | UI 메시지 | AI 대안 행동 |
|---|---|---|
| ConflictScore ≥ 임계값 (배고픔) | "너무 배고파요. 밥 먹고 바로 할게요!" | SurviveHunger |
| ConflictScore ≥ 임계값 (부상) | "부상이 심해요. 치료 후 하겠습니다." | SeekMedicalAid |
| ConflictScore ≥ 임계값 (피로) | "지쳐서 쓰러질 것 같아요..." | Sleep |
| loyalty < 30 | "왜 제가 그걸 해야 하죠?" | P4~P5 자율 수행 |
| nearEnemy + !hasWeapon | "무장 없이는 너무 위험해요!" | CraftPrimitiveWeapon → Flee |

### 4.5 주민 사망 처리

#### 사망 즉시 처리 순서
```
1. isAlive = false
2. 예약 자원 즉시 해제: reserved -= amount (전 자원 타입)
3. 인벤토리 전체 → DroppedItem 엔티티 (사망 위치)
4. WorldState.droppedItems에 추가
5. 주변 15타일 주민 nearDroppedItem 플래그 갱신
6. 주변 주민 Loyalty -10, Mood -15 (목격 페널티)
```

#### DroppedItem 수거 판단
| 드롭 아이템 | 수거 조건 (GOAP 판단) |
|---|---|
| Axe | hasTool=false AND 현재 Goal에 ChopWood 포함 |
| Pickaxe | hasTool=false AND 현재 Goal에 Mine* 포함 |
| Weapon | hasWeapon=false AND (DefendVillage OR Warrior 역할) |
| PrimitiveWeapon | hasPrimitiveWeapon=false AND nearEnemy=true |

### 4.6 주민 모집 시스템 (v0.4 신규, Q05 확정)

#### 모집 해금 조건
- Town Hall 완성 시 모집 UI 해금 (§10 마일스톤 이벤트와 연동)

#### 모집 비용표
| 역할 | cookedFood | 추가 자원 | 비고 |
|---|---|---|---|
| 미지정(None) | 15 | — | 입문용, 가장 유연 |
| 나무꾼/광부/건설자/요리사 | 20 | — | 기본 생산직 |
| 의료사(Medic) | 20 | Copper 1 | 의약품 지참 |
| 전사(Warrior) | 25 | Iron 3 | 무기 지급 |

#### 초기 속성 범위 (ScriptableObject 기반)

구현 원칙: `VillagerRecruitData` ScriptableObject 1개/역할로 분리 → 코드 수정 없이 밸런스 조정 가능.

```
Base 범위 (역할 공통):
  Health:  Random(70, 90)
  Hunger:  Random(20, 40)  — 배고프지 않은 상태로 부임
  Fatigue: Random(10, 30)  — 활력 있음
  Mood:    Random(60, 80)  — 새 마을에 기대감
  Loyalty: Random(40, 70)  — 중립~우호 (함께 살며 변화)

역할별 Modifier (Base에 덧붙임):
  Warrior → Health  + Random(5, 10)
             Loyalty + Random(-5, 0)   — 독립적 성향
  Medic   → Mood    + Random(5, 10)   — 봉사 정신
  Cook    → Hunger  - Random(10, 15)  — 자기 음식 잘 챙김
  Miner   → Fatigue - Random(3, 5)    — 강인한 체력
```

---

## 5. 자원 & 경제 시스템

### 5.1 자원 종류
| 자원 | 획득 방법 | 주 용도 | 재생 여부 |
|---|---|---|---|
| 나무(Wood) | ChopWood | 건물 기본재, 연료 | ✅ 재생 |
| 돌(Stone) | MineStone | 건물 강화재 | ✅ 재생 |
| 철광석(Iron) | MineIron | 도구·무기·고급 건물 | ✅ 재생 |
| 구리(Copper) | MineCopper (Forge 필요) | 고급 건물 | ✅ 재생 |
| 은(Silver) | MineOre (위험 지역) | 최상위 건물 | ✅ 재생 |
| 재료 식량(Raw Food) | HarvestWildBerries | CookMeal 재료 | ❌ (요리사 생산) |
| 완성 식량(Cooked Food) | CookMeal | 주민 생존 (고효율) | ❌ |

### 5.2 자원 노드 재생 수치 (임시 — 밸런스 조정 예정)
| 자원 | 재생량 | 주기 | 최대 용량 |
|---|---|---|---|
| 나무 | 5 units | 1 game day | 노드당 50 |
| 돌 | 3 units | 1 game day | 노드당 40 |
| 철광석 | 1.5 units | 1 game day | 노드당 25 |
| 구리 | 0.5 units | 1 game day | 노드당 15 |
| 은 | 0.2 units | 1 game day | 노드당 10 |

### 5.3 건물 건설 비용표
| 건물 | Wood | Stone | Iron | Copper | Silver | 기능 |
|---|---|---|---|---|---|---|
| House | 20 | 10 | 0 | 0 | 0 | 수면 효율 +60%, Loyalty +5/일 |
| Storehouse | 15 | 5 | 0 | 0 | 0 | 자원 용량 100 → 200 |
| Campfire | 5 | 0 | 0 | 0 | 0 | 요리 가능, 난방 (겨울 연료 -20%) |
| Town Hall | 35 | 30 | 6 | 0 | 0 | 모집 해금, 팩션 기능 해금 |
| Forge | 20 | 20 | 15 | 0 | 0 | 무기·도구 제작 |
| Watchtower | 10 | 30 | 5 | 0 | 0 | 적 탐지 범위 ×2 |
| Communication Tower | 30 | 20 | 10 | 20 | 0 | 외교·교역 해금 |
| Alchemist Lab | 15 | 10 | 5 | 5 | 0 | cookedFood 소비 -20% (겨울) |
| Silver Citadel | 60 | 50 | 30 | 20 | 15 | 최종 승리 건물 |

### 5.4 건설 다중 주민 배치 (v0.4 확정, Q14)

```
배치 인원 → 건설 속도 배율
  1명: 100%  |  2명: ×1.5  |  3명: ×1.8  |  4명+: ×2.0 (상한 고정)

BuildingQueue 필드 추가:
  assignedVillagerIds: List<string>  — 최대 3명 슬롯
  currentSpeedMultiplier: float      — 배치 인원 수에 따라 자동 계산

GOAP 처리:
  BuildStructure Goal을 여러 주민이 동시에 선택 가능
  단, 동일 큐 아이템에 3명 초과 시 나머지 주민은 다른 큐 아이템으로 Re-plan
```

### 5.5 경제 수치

**게임 시작 스타팅 보너스:**
```
rawFoodStock:   30 units (Day 1~3 요리 재료 버퍼)
woodStock:      10 units
stoneStock:     5 units
cookedFoodStock: 0      (Day 1에 요리사가 CookMeal로 즉시 생산 시작)
```

**주민 1인 일일 식량 소비:**
```
EatCookedFood: cookedFood 3 units / 일 (표준)
EatRawFood:    rawFood    9 units / 일 (요리사 부재 비상 시 — 3배 소모)
```

**식량 생산 (요리사 1명 기준):**
```
CookMeal Cost × 0.4 (Cook -60%) = 3.2
rawFood 3 → cookedFood 2 (주민 2/3일 분량)
일 8시간 작업 시: cookedFood 약 8 units/일 → 주민 2.7명 분량 공급
```

### 5.6 겨울 위기 대응 시스템

```
겨울 경보 임계값:
  cookedFoodStock < (주민 수 × 3 × 30) → "겨울 대비 경보" UI
  예: 주민 5인 → 경보 기준: cookedFoodStock < 450

겨울 연료 소비 (v0.4 확정, Q08):
  매 game day: woodStock -= 1.0 × 주민 수  (난방 소비)
  Campfire 또는 House 건설 시: ×0.8 (20% 절약)
  woodStock = 0 → 피로 증가율 +20%/일, Mood -5/일 추가 패널티

계절별 식량 생산 보정:
  봄 +20% | 여름 +30% | 가을 +10% + rawFood 수집 가중치 ×3 자동 적용
  겨울 -50% + 연료 소비 발생

Alchemist Lab 보존 기술: cookedFoodStock 소비 -20% (겨울 한정)
```

---

## 6. 위협 & 전투 시스템

### 6.1 위협 종류
| 위협 | 위험도 | 출현 타이밍 | 대응 무기 |
|---|---|---|---|
| 소형 동물 (늑대, 멧돼지) | 1등급 | Day 1~ | 원시 무기 이상 |
| 중형 몬스터 | 2등급 | Day 5~ | 일반 무기, 전사 2인 이상 |
| 대형 몬스터 | 3등급 | Day 15~ | 일반 무기, 팀 전투 필요 |
| 적 팩션 레이드 | 2~4등급 | Town Hall 이후 | 일반 무기 + 전략 |
| 자연재해 | 1~3등급 | 랜덤 이벤트 | 즉각 대응 |

### 6.2 적 팩션 GOAP AI — 침략 트리거

```
침략 결정 조건:
  (copperStock < 10 OR silverStock < 5)
  AND nearPlayerTerritory == true
  AND playerStrength < factionStrength × 0.8
→ RaidDecision = true → ExpandTerritory Goal 활성화
→ 전력 열세 시: AllianceProposal 또는 교역 제안 우선
```

### 6.3 AssessPlayerStrength 공식 (v0.4 확정, Q11)

```
playerStrength
  = (주민 수 × 10)
  + (전사 역할 주민 수 × 15)
  + (무기 보유 주민 수 × 8)
  + (Watchtower 완성 여부 × 20)
  + (Forge 완성 여부 × 15)

factionStrength (각 팩션 고정값 + 유닛 보정):
  숲의 부족:  (팩션 유닛 수 × 10) + 25
  철의 도시:  (팩션 유닛 수 × 12) + 35  ← 가장 강함
  상인 연합:  (팩션 유닛 수 ×  8) + 20
```

### 6.4 팩션 초기 상태 (v0.4 확정, Q06)

#### 초기 자원 (플레이어 대비 1.5~2배 여유)

| 팩션 | Wood | Stone | Iron | Copper | rawFood | cookedFood | 유닛 수 |
|---|---|---|---|---|---|---|---|
| **플레이어 (참고)** | 10 | 5 | 0 | 0 | 30 | 0 | 3 |
| 숲의 부족 | 30 | 8 | 5 | 0 | 60 | 30 | 5 |
| 철의 도시 | 20 | 30 | 25 | 0 | 20 | 20 | 5 |
| 상인 연합 | 25 | 15 | 5 | 10 | 30 | 40 | 4 |

#### 기지 위치 및 활성화 타이밍

| 팩션 | 기지 위치 | 성향 | 정찰 시작 | 레이드 가능 |
|---|---|---|---|---|
| 숲의 부족 | 북서쪽 (숲 밀집) | 방어적, 식량 부족 시 침략 | Day 5 | Day 10+ |
| 철의 도시 | 동쪽 (광물 지역) | 공격적, 철광석 최우선 | Day 7 | Day 12+ |
| 상인 연합 | 남쪽 (맵 중앙) | 교역 우선, 전투 최후 수단 | Day 10 | Day 15+ |

```
활성화 단계:
  Day 1~4:    팩션 존재, 플레이어 탐험 불가 영역 — 완전 비활성
  정찰 시작:  팩션 정찰 유닛 파견 → playerStrength 체크 시작
  레이드 가능: 침략 트리거 조건 충족 시 첫 레이드 발동
```

### 6.5 유닛 상한선 및 LOD AI

```
총 유닛 상한선: 100
  플레이어: 최대 50명 | 적 팩션 합산: 최대 45명 | 중립/야생: 최대 5명

LOD AI:
  30타일 초과 + 비전투 → Full GOAP에서 간소화 FSM으로 전환
  전투 참여 또는 30타일 이내 → Full GOAP 복귀
```

---

## 7. 팩션 & 외교 시스템

### 7.1 등장 팩션
| 팩션명 | 초기 관계 | AI 성향 | 특화 자원 |
|---|---|---|---|
| 플레이어 마을 | — | 플레이어 제어 + GOAP 자율 | 균형형 |
| 숲의 부족 | 중립 (50) | 방어적, 자원 부족 시 침략 | 나무, 식량 |
| 철의 도시 | 적대 (20) | 공격적, 철광석 최우선 | 철광석 |
| 상인 연합 | 우호 (70) | 교역 지향, 전투 회피 | 구리 |

### 7.2 관계 변화 트리거
| 이벤트 | 관계 변화 |
|---|---|
| 영토 침범 | -20 |
| 교역 성공 | +10 |
| 주민 구조/지원 | +30 |
| 무력 충돌 | -50 |
| 선물 (희귀 자원) | +15 |

---

## 8. 맵 & 환경

### 8.1 맵 구조
- 크기: 100 × 100 타일
- 타일 종류: 평지 / 숲 / 암석 / 수역 / 위험 지대
- 시야 시스템: Fog of War (탐험으로 해제)

### 8.2 Fog of War + GOAP 연동

```
탐험 상태:
  isDiscovered = false  → GOAP 플래닝 완전 제외
  isDiscovered = true   → GOAP 플래닝 포함, 수집 Action 실행 가능

초기 탐험 영역: 플레이어 시작 위치 주변 20타일 isDiscovered=true

탐험 방법:
  Explore Action → 주민 주변 15타일 자원 노드 isDiscovered=true

탐험-GOAP 연결:
  탐험된 자원 노드 전부 고갈 → Explore Goal 자동 활성화
  → 주민이 자율적으로 영토 확장
```

### 8.3 자원 노드 분포

| 자원 | 전체 | 플레이어 시작 (탐험됨) | 무주지 (미탐험) | 적 영역 (미탐험) |
|---|---|---|---|---|
| 나무 | 80 | 30 | 25 | 25 |
| 돌 | 40 | 15 | 12 | 13 |
| 철광석 | 20 | 6 | 7 | 7 |
| 구리 | 10 | 2 | 5 | 3 |
| 은 | 5 | 0 | 3 | 2 |
| 베리 (Raw Food) | 15 | 6 | 6 | 3 |

> 구리·은은 의도적으로 미탐험/적 영토 집중 → 탐험 및 침략 동기 부여.

### 8.4 팩션 기지 위치 (맵 배치)

```
[북서] 숲의 부족 기지 — 숲 타일 밀집, 나무·식량 노드 풍부
[동쪽] 철의 도시 기지 — 암석 지형, 철광석 노드 집중
[남쪽] 상인 연합 기지 — 평지, 맵 중앙 접근 용이 (교역로)
[중앙] 플레이어 시작 위치 — 균형 잡힌 자원 분포

팩션 간 영토: 각 팩션은 기지 중심 반경 25타일 내를 초기 지배 영역으로 설정
무주지: 영토 사이 공백 — 자원 분쟁 발생 지역
```

### 8.5 계절 시스템
| 계절 | rawFood 채집 보정 | 특이사항 |
|---|---|---|
| 봄 | +20% | 없음 |
| 여름 | +30% | 화재 위험 ↑ |
| 가을 | +10% | 수확 이벤트 (+30 rawFood), rawFood 수집 가중치 ×3 자동 적용 |
| 겨울 | -50% | 연료 소비 발동 (woodStock -1/인/일), 피로 +30%/일 |

---

## 9. 마일스톤 이벤트 시스템

| 트리거 | 이벤트 | 보상 |
|---|---|---|
| Forge 완성 | "대장장이 나그네" | 전사 역할 주민 1명 자동 합류 |
| Day 7 생존 | "호기심 많은 탐험가" | 탐험가(Explore 특화) 주민 1명 합류 |
| Town Hall 완성 | "마을의 소문" | 주민 모집 UI 해금 |
| 첫 번째 적 격퇴 | "영웅의 귀환" | 전 주민 Loyalty +15, Mood +20 |
| Communication Tower 완성 | "외교의 시작" | 상인 연합 관계 +20, 교역 메뉴 해금 |

---

## 10. 플레이어 인터페이스

### 10.1 플레이어 역할
| 제어 유형 | 내용 | 주민 반응 |
|---|---|---|
| 건설 명령 | 특정 위치에 건물 큐 추가 | GOAP가 수행자 자율 배정 (다중 주민 자동 할당) |
| 역할 지정 | 주민 Role 설정 | Action Cost 보정 즉시 반영 |
| 모집 명령 | Town Hall 해금 후 역할 선택 + 비용 지불 | VillagerRecruitData 기반 주민 생성 |
| 우선순위 조정 | 팩션 전략 방향 설정 | Goal 가중치 전역 조정 |
| 긴급 명령 | 특정 주민에게 즉시 행동 지시 | ConflictScore 초과 시 거부 가능 |

### 10.2 자동화 레벨
- **완전 자율 (Full Auto)**: 주민이 모든 것을 스스로 판단
- **반자율 (Semi-Auto)**: 자율 행동하되 플레이어 명령 우선 처리
- **수동 (Manual)**: 플레이어 지시 기반, GOAP가 최적 실행 경로 제안

---

## 11. 승리 / 패배 조건

### 패배
- 모든 주민 사망
- Town Hall 파괴 + 생존 주민 2명 이하

### 승리 (단계별)
| 단계 | 조건 |
|---|---|
| 1단계 (생존) | Town Hall 건설 + 주민 5명 이상 30일 생존 |
| 2단계 (지배) | 모든 적 팩션 제압 또는 전 팩션 동맹 체결 |
| 3단계 (번영) | Silver Citadel 완성 |

---

## 12. 기술 구현

### 12.1 GOAP 구현 방향
- Action Graph A* 탐색으로 최적 행동 체인 도출
- World State 2레이어 구조 (플래닝용 스냅샷 + 실행용 Authoritative State)
- 자원 예약 시스템: `가용량 = stock - reserved`
- C# Job System + Burst Compiler로 GOAP 연산 병렬 처리
- Tick 분산: 0.1초 간격, 6그룹으로 분산

### 12.2 데이터 설계 원칙 (v0.4 추가)

```
ScriptableObject 분리 대상 (코드 수정 없이 밸런스 조정):
  VillagerRecruitData  — 역할별 모집 비용 + 초기 속성 범위
  ResourceNodeData     — 노드별 재생량, 주기, 최대 용량
  FactionInitialState  — 팩션 초기 자원, 유닛 수, 활성화 타이밍
  BuildingData         — 건설 비용, 효과, 다중 주민 슬롯 수
  SeasonData           — 계절별 보정 수치

→ 기획 변경 시 ScriptableObject 에셋만 수정, 재배포 없이 패치 가능
```

### 12.3 성능 목표
| 항목 | 목표 |
|---|---|
| 목표 FPS | 60fps |
| 총 유닛 상한 | 100 (Full GOAP 실질 60~70) |
| GOAP 플래닝 비용 | 프레임당 < 2ms |
| A* 경로탐색 | 비동기, < 1.0ms |
| Re-plan 쿨다운 | 0.3~0.5초 |
| 탐색 깊이 상한 | Depth 6 |

### 12.4 미확정 사항 (개발 진행 중 결정)

| 번호 | 질문 | 결정 시점 |
|---|---|---|
| Q09 | Full Auto 모드에서 플레이어 가능 행동 범위 (순수 관전 vs 개입 가능) | UI 프로토타입 완성 후 |
| Q10 | Silver Citadel 완성 후 엔딩 방식 (메뉴 복귀 vs 샌드박스 전환) | 후반 콘텐츠 구현 단계 |

---

## 13. 개발 준비 완료 체크리스트 (v0.4 기준)

| 항목 | 상태 |
|---|---|
| GOAP 핵심 아키텍처 (World State, Goal, Action, Re-plan) | ✅ 확정 |
| 2단계 식량 시스템 (rawFood / cookedFood) | ✅ 확정 |
| 주민 사망 처리 (DroppedItem, 예약 즉시 해제) | ✅ 확정 |
| FoW + GOAP 연동 (isDiscovered 필터링) | ✅ 확정 |
| 자원 노드 재생 (임시 수치) | ✅ 확정 |
| 주민 모집 시스템 (비용표, 속성 범위, ScriptableObject 구조) | ✅ 확정 |
| 팩션 초기 상태 (자원, 기지 위치, 활성화 타이밍) | ✅ 확정 |
| AssessPlayerStrength 공식 | ✅ 확정 |
| 겨울 연료 처리 | ✅ 확정 |
| 건설 다중 주민 | ✅ 확정 |
| 게임 시작 초기 주민 3명 구성 | ✅ 확정 |
| 성능 목표 + LOD AI 기준 | ✅ 확정 |
| Q09 (Full Auto 범위) | ⏳ 개발 중 결정 |
| Q10 (엔딩 방식) | ⏳ 개발 중 결정 |

---

*작성일: 2026-06-25 | 버전: v0.4*
*다음 단계: unity-ai-behavior-architect 에이전트로 GOAP 아키텍처 설계 → unity-senior-programmer로 코어 구현 시작*
