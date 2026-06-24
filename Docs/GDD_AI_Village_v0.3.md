# AI Village 게임 기획서 v0.3
## GOAP 기반 자율 생존 마을 시뮬레이션

---

## 변경 이력

| 버전 | 날짜 | 주요 변경 사항 |
|---|---|---|
| v0.1 | 2026-06-25 | 최초 초안 작성 |
| v0.2 | 2026-06-25 | 경제 밸런스 수정, GOAP Dead-end 해소, 성능 유닛 상한 확정, 충성도 시스템 재설계, P0 서브 우선순위 확정 |
| v0.3 | 2026-06-25 | 2단계 식량 시스템 도입, 자원 노드 재생 확정, FoW 탐험 연동, 주민 사망 처리(DroppedItem) 설계 |

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
nearDroppedItem   : bool    // v0.3 추가 — 근처에 DroppedItem 존재 여부

// 마을 전역 상태 (Global)
// ▼ v0.3 변경: foodStock → rawFoodStock + cookedFoodStock 분리
rawFoodStock      : float   // 채집/사냥으로 얻은 재료 창고
cookedFoodStock   : float   // 요리사가 완성한 식량 창고
woodStock         : float
stoneStock        : float
ironStock         : float
copperStock       : float
silverStock       : float

// 자원 예약 (Resource Reservation System)
rawFoodReserved   : float   // v0.3 변경
cookedFoodReserved: float   // v0.3 변경
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

// v0.3 추가
droppedItems      : List<DroppedItem>  // 맵 위 낙하 아이템 목록
```

> **v0.3 변경:** `foodStock` → `rawFoodStock` + `cookedFoodStock` 분리. `hasFood`, `hasRawFood` 개인 인벤토리 플래그 제거 (식량은 공유 창고로 직행). `nearDroppedItem`, `droppedItems` 추가.

### 3.2 Goal 목록 및 우선순위

| 우선순위 | Goal | 발동 조건 |
|---|---|---|
| **P0-1** (최우선) | SurviveInjury | healthLevel < 20 |
| **P0-2** | SurviveHunger | hungerLevel > 80 |
| **P0-3** | SurviveFatigue | fatigueLevel > 90 |
| P1 | DefendVillage | enemyNearby == true |
| P2 | ExecutePlayerOrder | ConflictScore < threshold AND loyaltyLevel 조건 충족 |
| P3 | BuildStructure | buildingQueued == true AND 자원 충족 |
| P4 | GatherResources | anyAvailableStock < 30 (rawFood + cookedFood 합산 포함) |
| P5 | Explore | allAvailableStocks >= 50 AND unexploredTilesNearby == true |

> **v0.3 변경:** GatherResources 발동 조건에서 `anyAvailableStock`이 rawFoodStock + cookedFoodStock 합산 기준으로 평가. Explore Goal 발동 조건에 `unexploredTilesNearby` 추가 — 탐험할 공간이 없으면 발동하지 않음.

### 3.3 Action 목록

#### 생존 Actions
| Action | Precondition | Effect | Cost |
|---|---|---|---|
| **EatCookedFood** | cookedFoodStock>=1, atBase=true | cookedFoodStock-=1, hungerLevel-=50 | 2 |
| **EatRawFood** | rawFoodStock>=1 | rawFoodStock-=1, hungerLevel-=15 | 4 |
| **HarvestWildBerries** | nearBerryBush=true, berryBush.isDiscovered=true | rawFoodStock+=5 | 5 |
| CookMeal | rawFoodStock>=3, nearFireplace=true | rawFoodStock-=3, cookedFoodStock+=2 | 8 |
| Sleep | nearBed=true, enemyNearby=false | fatigueLevel-=90 | 10 |
| RestOnGround | (없음) | fatigueLevel-=20 (비상용) | 12 |
| SeekMedicalAid | nearHealer=true | healthLevel+=40 | 5 |

> **v0.3 변경:**
> - `EatStoredFood` → `EatCookedFood` (cookedFoodStock 소비, 고효율 허기 -50)
> - `EatRawFood` 신규 추가 (rawFoodStock 소비, 비상용 허기 -15)
> - `HarvestWildBerries` Effect: `hasFood=true, hungerLevel-=20` → `rawFoodStock+=5` (공유 재료 창고로 직행)
> - `CookMeal` Precondition: `hasRawFood=true` → `rawFoodStock>=3` / Effect: `rawFoodStock-=3, cookedFoodStock+=2`

**식량 Action 효율 비교표 (v0.3):**
| 시나리오 | 소비 재료 | 허기 감소 | 효율 |
|---|---|---|---|
| 요리사 있음 (CookMeal → EatCookedFood) | rawFood 1.5개 per 식사 | -50 | 고효율 |
| 요리사 없음 (EatRawFood 직접 소비) | rawFood 1개 per 식사 | -15 | 저효율 (3.3배 더 소모) |
| 비상 (베리 채집 후 EatRawFood) | 현장 해결 | -15 | 최저효율 |

#### 자원 수집 Actions
| Action | Precondition | Effect | Cost |
|---|---|---|---|
| ChopWood | nearTree=true, tree.isDiscovered=true, hasTool(Axe)=true | woodStock+=10 | 4 |
| MineStone | nearRock=true, rock.isDiscovered=true, hasTool(Pickaxe)=true | stoneStock+=8 | 5 |
| MineIron | nearIronOre=true, ironOre.isDiscovered=true, hasTool(Pickaxe)=true | ironStock+=5 | 6 |
| MineCopper | nearCopperOre=true, copperOre.isDiscovered=true, hasTool(Pickaxe)=true, forgeBuilt=true | copperStock+=3 | 9 |
| PickUpTool | nearStorage=true | hasTool=true | 1 |
| **PickUpDroppedItem** | nearDroppedItem=true | hasItem=true (아이템 타입별 적용) | 1 |

> **v0.3 추가:** 모든 자원 수집 Action에 `isDiscovered=true` 조건 추가 — 탐험되지 않은 노드는 GOAP 플래닝에서 제외됨.
> `PickUpDroppedItem` 신규 추가 — 주민 사망 시 드롭된 아이템 수거.

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
| Explore | atBase=false OR (atBase=true → MoveToUnexplored) | 주변 15타일 내 자원 노드 isDiscovered=true | 6 |
| MoveToUnexplored | unexploredTilesNearby=true | 미탐험 타일로 이동 | 이동 거리 비례 |

> **v0.3 추가:** Explore Action Effect에 자원 노드 `isDiscovered=true` 적용 명시.

### 3.4 GOAP 플래닝 예시

**시나리오 A:** Day 1, 주민 'Arin' hungerLevel=82 → Goal: SurviveHunger

```
[요리사가 없고 cookedFoodStock=0인 경우]
경로 A (Cost 4):  EatRawFood — rawFoodStock>=1 ✅ → 즉시 실행 (hungerLevel-=15)
경로 B (Cost 9):  HarvestWildBerries(5) → EatRawFood — nearBerryBush 탐험됨 필요
경로 C (Cost 10): [요리사 주민] CookMeal(rawFood 3개) → EatCookedFood (hungerLevel-=50)

[요리사가 있고 rawFoodStock>=3인 경우]
GOAP 플래너: CookMeal(Cost 8) + EatCookedFood(Cost 2) = 10
vs EatRawFood(Cost 4) + EatRawFood(Cost 4) + EatRawFood(Cost 4) = 12 (허기 -45)
→ 요리사는 CookMeal 우선. 비요리사는 EatRawFood로 즉시 해결.
```

**시나리오 B:** 탐험된 나무 고갈 → GatherResources Goal

```
[탐험된 나무 노드 전부 고갈]
ChopWood Precondition: nearTree=true, tree.isDiscovered=true ❌ → 해당 Action 제외
→ GOAP Fallback: Explore Goal 자동 활성화
→ 주민이 미탐험 영역으로 이동 → 새 나무 노드 isDiscovered=true
→ Re-plan: ChopWood 가능 ✅
```

**시나리오 C:** 주민 사망 → 다른 주민 DroppedItem 수거

```
전사 'Bran' 사망:
  - 인벤토리: Axe, wood×3 → DroppedItem 생성 (tileX=45, tileY=32)
  - 예약 wood 5 → 즉시 reserved -= 5 (다른 주민 건설에 활용 가능)

나무꾼 'Ceri' (nearDroppedItem=true):
  - GOAP 판단: hasTool=false → PickUpDroppedItem(Axe) 실행 (Cost 1)
  - hasTool=true → ChopWood 가능 ✅

광부 'Dain' (nearDroppedItem=true):
  - GOAP 판단: hasTool=true (Pickaxe) → Axe 불필요 → PickUpDroppedItem 건너뜀
```

### 3.5 Re-planning 규칙

```
Re-plan 발동 조건:
  - 현재 실행 중인 Action의 Precondition이 무효화된 경우
  - P0 Goal이 새로 활성화된 경우 (즉시 처리)
  - 탐험으로 새 자원 노드 isDiscovered=true → GatherResources Re-plan

Re-plan 제한:
  - 쿨다운: 마지막 Re-plan 이후 최소 0.3~0.5초
  - 탐색 깊이 상한: Depth 6
  - 동일 Goal에 대해 30초 내 10회 초과 시 → 다음 우선순위 Goal로 전환

Fallback:
  - NoSolutionFound → 안전 행동 (RestOnGround 또는 MoveToBase)
  - 3회 연속 Deadlock → "도움 필요" UI 아이콘 표시
```

---

## 4. 주민(Villager) 시스템

### 4.1 주민 속성
| 속성 | 범위 | 위험 임계값 | 설명 |
|---|---|---|---|
| 체력(Health) | 0~100 | 20 이하 → P0-1 | 전투·사고로 감소, 0 = 사망 |
| 허기(Hunger) | 0~100 | 80 이상 → P0-2 | +3/시간 자연 증가 |
| 피로(Fatigue) | 0~100 | 90 이상 → P0-3 | 활동으로 증가, 수면으로 감소 |
| 행복도(Mood) | 0~100 | 20 이하 = 반란 위험 | 환경·이벤트로 변동 |
| 충성도(Loyalty) | 0~100 | 구간별 효과 (§4.3 참조) | 명령 수행 의지 |

### 4.2 주민 역할(Role) — Cost 보정
| 역할 | 특화 Action Cost 감소 | 비특화 Cost 증가 |
|---|---|---|
| 나무꾼(Lumberjack) | ChopWood -50% | MineStone +30% |
| 광부(Miner) | Mine* -50% | ChopWood +30% |
| 건설자(Builder) | Build* -40% | Combat +50% |
| 전사(Warrior) | Attack* -40% | Build* +60% |
| 의료사(Medic) | SeekMedicalAid -50% (상대 Heal) | Mine* +40% |
| 요리사(Cook) | CookMeal -60% | Attack* +80% |

> **요리사(Cook) 역할의 전략적 가치 (v0.3):**
> - CookMeal Cost: 8 × 0.4 = **3.2** (60% 감소)
> - rawFood 3개 → cookedFood 2개 변환 효율 유지
> - 요리사 1명이 주민 5명 분량의 식량을 소화할 수 있음
> - 요리사 사망 시: 주민 전원이 EatRawFood(효율 1/3)로 전환 → 식량 위기 3배 가속

### 4.3 충성도(Loyalty) 시스템 — v0.2 Hybrid C+D 방식 (변경 없음)

#### 레이어 1: GOAP Cost Modifier
| 충성도 구간 | Cost 배율 | 동작 |
|---|---|---|
| 70~100 | ×0.7 | 명령 수행 적극적 |
| 50~69 | ×1.0 | 기본값 |
| 30~49 | ×2.5 | 거부 빈도 증가 |
| 0~29 | ×6.0 | 사실상 명령 무시, 마을 이탈 위험 |

#### 레이어 2: Need-Conflict Score
```
ConflictScore = Σ (Need_urgency[i] × Order_impact[i])
실효 거부 임계값 = 2.5 × (loyaltyLevel / 50)
ConflictScore ≥ 임계값 → 명시적 거부 + UI 메시지
```

#### 충성도 회복/감소 이벤트
| 이벤트 | 충성도 변화 |
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

### 4.4 주민 사망 처리 (v0.3 신규)

#### 사망 즉시 처리 순서
```
1. isAlive = false
2. 예약 자원 즉시 해제:
     reservedResources.ForEach(r => r.reserved -= r.amount)
     → 다른 주민이 해당 자원을 바로 사용 가능

3. 인벤토리 전체 드롭:
     인벤토리 아이템 → DroppedItem 엔티티 생성 (사망 위치)
     DroppedItem { itemType, amount, tileX, tileY }

4. WorldState.droppedItems에 추가

5. 주변 주민 nearDroppedItem 플래그 갱신 (15타일 이내)

6. 주변 주민 Loyalty -10, Mood -15 (목격 페널티)
```

#### DroppedItem 수거 GOAP 판단 기준
GOAP 플래너가 `nearDroppedItem=true`일 때 PickUpDroppedItem을 선택하는 조건:

| 드롭 아이템 | 수거 조건 (GOAP 판단) |
|---|---|
| Axe (도끼) | hasTool=false AND (현재 Goal에 ChopWood 포함) |
| Pickaxe (곡괭이) | hasTool=false AND (현재 Goal에 Mine* 포함) |
| Weapon (무기) | hasWeapon=false AND (DefendVillage 또는 Warrior 역할) |
| PrimitiveWeapon | hasPrimitiveWeapon=false AND nearEnemy=true |

> 필요하지 않은 아이템은 수거하지 않는다. GOAP 플래너가 현재 Goal 달성에 기여하지 않으면 Cost 대비 이득이 없어 자연히 건너뜀.

---

## 5. 자원 & 경제 시스템

### 5.1 자원 종류
| 자원 | 획득 방법 | 주 용도 | 희소성 | 재생 여부 |
|---|---|---|---|---|
| 나무(Wood) | ChopWood | 건물 기본재, 연료 | 낮음 | ✅ 재생 |
| 돌(Stone) | MineStone | 건물 강화재 | 낮음 | ✅ 재생 |
| 철광석(Iron) | MineIron | 도구·무기·고급 건물 | 보통 | ✅ 재생 |
| 구리(Copper) | MineCopper (Forge 필요) | 고급 건물 | 높음 | ✅ 재생 |
| 은(Silver) | MineOre (위험 지역) | 최상위 건물 | 매우 높음 | ✅ 재생 |
| 재료 식량(Raw Food) | HarvestWildBerries, Hunt | CookMeal 재료 | 소모성 | ❌ (요리사 생산) |
| 완성 식량(Cooked Food) | CookMeal (요리사 생산) | 주민 생존 | 소모성 | ❌ |

> **v0.3 변경:** 식량을 Raw/Cooked 분리. 광물 자원은 전부 재생 있음.

### 5.2 자원 노드 재생 수치 (v0.3 임시 — 밸런스 조정 예정)

| 자원 | 재생량 | 재생 주기 | 최대 용량 | 비고 |
|---|---|---|---|---|
| 나무 | 5 units | 1 game day | 노드당 50 | 숲 타일에만 존재 |
| 돌 | 3 units | 1 game day | 노드당 40 | 암석 타일 |
| 철광석 | 1.5 units | 1 game day | 노드당 25 | 암석 타일 (깊은 층) |
| 구리 | 0.5 units | 1 game day | 노드당 15 | 무주지/적 영역 집중 |
| 은 | 0.2 units | 1 game day | 노드당 10 | 위험 지역 |
| 베리(Raw Food 원천) | — | — | — | 재생 없음, 고갈 시 Explore 유도 |

> 모든 재생 수치는 밸런스 단계에서 조정. `regenerationRate = 0`이면 영구 고갈.

### 5.3 건물 건설 비용표
| 건물 | Wood | Stone | Iron | Copper | Silver | 기능 |
|---|---|---|---|---|---|---|
| House | 20 | 10 | 0 | 0 | 0 | 수면 효율 +60%, Loyalty +5/일 |
| Storehouse | 15 | 5 | 0 | 0 | 0 | 자원 용량 100 → 200 |
| Campfire | 5 | 0 | 0 | 0 | 0 | 요리 가능, 난방 |
| Town Hall | 35 | 30 | 6 | 0 | 0 | 팩션 기능 해금, 주민 모집 |
| Forge | 20 | 20 | 15 | 0 | 0 | 무기·도구 제작 — 마일스톤 이벤트 |
| Watchtower | 10 | 30 | 5 | 0 | 0 | 적 탐지 범위 ×2 |
| Communication Tower | 30 | 20 | 10 | 20 | 0 | 외교·교역 해금 |
| Alchemist Lab | 15 | 10 | 5 | 5 | 0 | 식량 보존 기술 (cookedFood 소비 -20%) |
| Silver Citadel | 60 | 50 | 30 | 20 | 15 | 최종 승리 건물 |

### 5.4 경제 수치

**게임 시작 스타팅 보너스:**
```
초기 지급: rawFoodStock 30 units  ← v0.3: foodStock → rawFoodStock으로 변경
초기 지급: 나무 10 units, 돌 5 units
목적: 첫 3일 간 SurviveHunger P0 남발 방지
(EatRawFood 효율이 낮으므로 가능한 빨리 요리사 지정 유도)
```

**주민 1인 일일 식량 소비:**
```
EatCookedFood 기준: cookedFood 3 units / 일 (hungerLevel 정상 유지)
EatRawFood 기준:    rawFood 9 units / 일 (동일 효과, 3배 소모)
→ 요리사 1명이 rawFood 9×N을 cookedFood 6×N으로 전환 가능 (6인 분량 커버)
```

**자원 생산량 (주민 1인 기준, 8시간 작업):**
| 자원 | 일반 주민 | 전문 주민 |
|---|---|---|
| 나무 | 8 / 일 | 15 / 일 (나무꾼) |
| 돌 | 5 / 일 | 10 / 일 (광부) |
| 철광석 | 3 / 일 | 7 / 일 (광부) |
| rawFood (채집) | 4 / 일 | — |
| cookedFood (요리) | — | 8 / 일 (요리사, CookMeal Cost -60%) |

### 5.5 겨울 위기 대응 시스템

```
겨울 경보 임계값 (v0.3 업데이트):
  cookedFoodStock < (주민 수 × 3 × 30일) → "겨울 대비 경보" UI 표시
  예: 주민 5인 → 경보 발동 기준: cookedFoodStock < 450

비상 상황: cookedFoodStock = 0이나 rawFoodStock 있음 → 주민 EatRawFood 자동 전환
  → 식량 소모 3배 가속 → 위기 체감 명확

Alchemist Lab 식량 보존 기술 (v0.3 업데이트):
  효과: 겨울 cookedFoodStock 소비량 -20% (rawFood는 적용 안 됨)
  → 요리사 + Alchemist Lab 조합의 전략적 가치 상승

계절별 긴급 자동 배치:
  가을 시작 시 GOAP GatherResources에 rawFood 수집 가중치 ×3 자동 적용
  → 주민들이 rawFood 비축 → 요리사가 겨울 전 cookedFood 대량 생산 유도
```

---

## 6. 위협 & 전투 시스템

### 6.1 위협 종류
| 위협 | 위험도 (등급) | 출현 타이밍 | 대응 무기 |
|---|---|---|---|
| 소형 동물 (늑대, 멧돼지) | 1 | Day 1~ | 원시 무기 또는 일반 무기 |
| 중형 몬스터 | 2 | Day 5~ | 일반 무기, 전사 2인 이상 |
| 대형 몬스터 | 3 | Day 15~ | 일반 무기, 팀 전투 필요 |
| 적 팩션 레이드 | 2~4 | Town Hall 건설 후 | 일반 무기 + 전략 |
| 자연재해 (폭풍, 화재) | 1~3 | 랜덤 이벤트 | 즉각 대응 필요 |

### 6.2 적 팩션 GOAP AI

침략 트리거 조건:
```
(copperStock < 10 OR silverStock < 5)
AND nearPlayerTerritory == true
AND playerStrength < factionStrength × 0.8
→ RaidDecision = true → ExpandTerritory Goal 활성화
→ 전력 열세 시: AllianceProposal 또는 교역 제안 우선
```

### 6.3 유닛 상한선 및 LOD AI

```
총 유닛 상한선: 100
  플레이어 마을 주민:    최대 50명
  적 팩션 3개 합산:      최대 45명 (팩션당 최대 15명)
  중립/야생 유닛:        최대 5명

LOD AI 규칙:
  조건: 플레이어 마을 기준 30타일 초과 + 전투 미참여
  전환: Full GOAP → 간소화 FSM
  복귀: 전투 참여 또는 30타일 이내 진입 시 Full GOAP 재활성화
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

### 8.2 Fog of War + GOAP 연동 (v0.3 신규)

```
탐험 상태 정의:
  isDiscovered = false  → GOAP 플래닝에서 완전 제외 (존재를 모름)
  isDiscovered = true   → GOAP 플래닝에 포함, 수집 Action 실행 가능

탐험 방법:
  Explore Action 실행 → 주민 주변 15타일 내 모든 자원 노드 isDiscovered=true
  시작 위치 주변 20타일은 게임 시작 시 isDiscovered=true (초기 탐험 영역)

GOAP와 탐험의 연결:
  모든 자원 수집 Action Precondition에 [resourceNode].isDiscovered=true 필수
  탐험된 노드가 전부 고갈 → GOAP 자동으로 Explore Goal 선택
  → 주민들이 자율적으로 영토를 확장하는 자연스러운 흐름 발생

전략적 의미:
  플레이어가 탐험을 명령하지 않아도 자원이 부족해지면 주민이 스스로 탐험
  구리·은은 먼 곳(미탐험 영역)에 집중 → 게임 후반 자연스러운 탐험 동기 부여
```

### 8.3 자원 노드 분포
| 자원 | 전체 | 플레이어 시작지 (탐험됨) | 무주지 (미탐험) | 적 영역 (미탐험) |
|---|---|---|---|---|
| 나무 | 80 | 30 | 25 | 25 |
| 돌 | 40 | 15 | 12 | 13 |
| 철광석 | 20 | 6 | 7 | 7 |
| 구리 | 10 | 2 | 5 | 3 |
| 은 | 5 | 0 | 3 | 2 |
| 베리 (Raw Food) | 15 | 6 | 6 | 3 |

> **v0.3 추가:** 베리 노드 수량 명시. 초기 탐험 영역 구분.
> 구리·은은 의도적으로 미탐험/적 영토 집중 → 탐험 및 침략 동기 부여.

### 8.4 계절 시스템
| 계절 | rawFood 채집 보정 | 특이사항 |
|---|---|---|
| 봄 | +20% | 없음 |
| 여름 | +30% | 화재 위험 ↑ |
| 가을 | +10% | 수확 이벤트 (+30 rawFood), **rawFood 수집 가중치 ×3 자동 적용** |
| 겨울 | -50% | 연료 소비 ×2, 피로 증가 +30%, **겨울 경보 시스템 작동** |

> **v0.3 변경:** 계절 보정이 `rawFood 채집량`에 적용 (cookedFood 생산량은 요리사 효율에 종속).

---

## 9. 마일스톤 이벤트 시스템

| 트리거 | 이벤트 | 보상 |
|---|---|---|
| Forge 완성 | "대장장이 나그네" | 전사 역할 주민 1명 자동 합류 |
| Day 7 생존 | "호기심 많은 탐험가" | 탐험가(Explore 특화) 주민 1명 합류 |
| Town Hall 완성 | "마을의 소문" | 일반 주민 1명 모집 가능 |
| 첫 번째 적 격퇴 | "영웅의 귀환" | 전 주민 Loyalty +15, Mood +20 |
| Communication Tower 완성 | "외교의 시작" | 상인 연합 관계 +20, 교역 메뉴 해금 |

---

## 10. 플레이어 인터페이스

### 10.1 플레이어 역할
| 제어 유형 | 내용 | 주민 반응 |
|---|---|---|
| 건설 명령 | 특정 위치에 건물 큐 추가 | GOAP가 수행자 자율 배정 |
| 역할 지정 | 주민 Role 설정 | Action Cost 보정 즉시 반영 |
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
- World State 2레이어 구조:
  - 플래닝용 스냅샷 (읽기 전용 복사본)
  - 실행용 Authoritative State (메인 스레드 전용 쓰기)
- 자원 예약 시스템: `가용량 = stock - reserved`
- C# Job System + Burst Compiler로 GOAP 연산 병렬 처리
- Tick 분산 처리: 0.1초 간격, 전체 유닛을 6그룹으로 분산

### 12.2 성능 목표
| 항목 | 목표 |
|---|---|
| 목표 FPS | 60fps |
| 총 유닛 상한 | 100 (Full GOAP 실질 60~70) |
| GOAP 플래닝 비용 | 프레임당 < 2ms (Job System 적용) |
| A* 경로탐색 | 비동기 처리, < 1.0ms |
| Re-plan 쿨다운 | 0.3~0.5초 |
| 탐색 깊이 상한 | Depth 6 |

### 12.3 미확정 사항 (개발팀 질의 필요)

| 번호 | 질문 | 중요도 |
|---|---|---|
| Q05 | 주민 신규 모집 비용 및 초기 속성 랜덤 범위 | 높음 |
| Q06 | 적 팩션 AI 초기 자원 상태, 기지 위치, 활성화 시점 (Day 몇?) | 높음 |
| Q08 | 겨울 연료 소비 ×2가 WorldState에 어떻게 반영? woodStock 직접 감소? | 보통 |
| Q09 | Full Auto 모드에서 플레이어 가능 행동 범위 (순수 관전 vs 개입 가능) | 보통 |
| Q10 | Silver Citadel 완성 후 엔딩 연출 방식 (메뉴 복귀 vs 샌드박스 전환) | 보통 |
| Q11 | 적이 플레이어 전력을 탐지하는 메커니즘 (정찰 유닛? 인접 타일 감지?) | 보통 |
| Q14 | 건설에 주민 다수 배치 가능 여부 및 속도 보정 공식 | 낮음 |

---

*작성일: 2026-06-25 | 버전: v0.3*
*다음 버전(v0.4) 목표: Q05·Q06 확정 반영, 주민 간 상호작용 설계(의료·요리 배달 흐름), 튜토리얼 온보딩 설계*
