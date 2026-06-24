# AI Village 게임 기획서 v0.1
## GOAP 기반 자율 생존 마을 시뮬레이션

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

GOAP AI를 탑재한 주민(Villager)들이 자신의 필요(Need)를 충족하기 위해 최적의 행동 시퀀스를 자율 계획·실행한다.
플레이어는 "마을 지도자(Village Leader)" 역할로 큰 방향을 제시하지만, 주민들은 생존을 최우선으로 명령을 수행하거나 거부할 수 있다.

### RimWorld와의 핵심 차별점
| 요소 | RimWorld | AI Village (GOAP) |
|---|---|---|
| AI 방식 | 작업 우선순위 목록 선택 | Goal → 멀티스텝 Action 체인 자동 계획 |
| 행동 계획 | 단일 최우선 작업 선택 | A* 기반 동적 플래닝 (Re-planning 지원) |
| 명령 거부 | 없음 (항상 수행) | 생존 위협·충성도 조건 충족 시 거부 |
| 팩션 AI | 스크립트 기반 레이드 | 팩션도 GOAP로 구동 — 자원 부족 시 자율 침략 결정 |
| 적응성 | 상황 변화 시 재선택 | 실행 중 장애물 발생 시 즉시 Re-plan |

---

## 2. 핵심 게임플레이 루프

### 매크로 루프 (장기 목표)
```
마을 건설 → 자원 확보 → 주민 성장 → 위협 극복 → 영토 확장 → 적 팩션 제압 → 생존 달성
```

### 미드 루프 (중기 사이클, 수 분)
```
자원 고갈 감지 → 수집 명령/자율 수집 → 건물 건설 → 방어선 구축 → 위협 대응
```

### 마이크로 루프 (GOAP 핵심, 초~수십 초)
```
주민 상태 변화 → Goal 활성화 → GOAP 플래너 실행 → 최적 Action 시퀀스 도출 → 실행 → World State 반영
```

---

## 3. GOAP AI 시스템

### 3.1 World State 정의
GOAP 플래너가 참조하는 전역/개인 상태값. 모든 Action의 Precondition과 Effect는 이 변수들을 기반으로 한다.

```
// 주민 개인 상태 (Per-Agent)
isAlive          : bool
hungerLevel      : float   // 0~100, 80 이상 = SurviveHunger 발동
fatigueLevel     : float   // 0~100, 90 이상 = SurviveFatigue 발동
healthLevel      : float   // 0~100, 20 이하 = SurviveInjury 발동
loyaltyLevel     : float   // 0~100, 30 이하 = 명령 거부 빈도 증가
hasTool          : bool
hasWeapon        : bool
hasFood          : bool
atBase           : bool
nearResource     : bool
nearEnemy        : bool

// 마을 전역 상태 (Global)
foodStock        : float   // 0~100
woodStock        : float   // 0~100
stoneStock       : float   // 0~100
ironStock        : float   // 0~100
copperStock      : float   // 0~100
silverStock      : float   // 0~100
enemyNearby      : bool
buildingQueued   : bool
townHallBuilt    : bool
forgeBuilt       : bool
```

### 3.2 Goal 목록 및 우선순위

| 우선순위 | Goal | 발동 조건 |
|---|---|---|
| P0 (생존 절대 우선) | SurviveHunger | hungerLevel > 80 |
| P0 | SurviveFatigue | fatigueLevel > 90 |
| P0 | SurviveInjury | healthLevel < 20 |
| P1 | DefendVillage | enemyNearby == true |
| P2 | ExecutePlayerOrder | playerOrderPending == true AND loyaltyLevel > 30 |
| P3 | BuildStructure | buildingQueued == true AND 자원 충족 |
| P4 | GatherResources | anyStock < 30 |
| P5 | Explore | allStocks >= 50 |

### 3.3 Action 목록

#### 생존 Actions
| Action | Precondition | Effect | Cost |
|---|---|---|---|
| EatStoredFood | hasFood=true, atBase=true | hungerLevel-=50 | 2 |
| HarvestWildBerries | nearBerryBush=true | hasFood=true, hungerLevel-=20 | 5 |
| CookMeal | hasRawFood=true, nearFireplace=true | hungerLevel-=80 (고품질) | 8 |
| Sleep | nearBed=true, enemyNearby=false | fatigueLevel-=90 | 10 |
| RestOnGround | (없음) | fatigueLevel-=40 (비효율) | 3 |
| SeekMedicalAid | nearHealer=true | healthLevel+=40 | 5 |

#### 자원 수집 Actions
| Action | Precondition | Effect | Cost |
|---|---|---|---|
| ChopWood | nearTree=true, hasTool(Axe)=true | woodStock+=10 | 4 |
| MineStone | nearRock=true, hasTool(Pickaxe)=true | stoneStock+=8 | 5 |
| MineIron | nearIronOre=true, hasTool(Pickaxe)=true | ironStock+=5 | 6 |
| MineCopper | nearCopperOre=true, hasTool(Pickaxe)=true, forgeBuilt=true | copperStock+=3 | 9 |
| PickUpTool | nearStorage=true | hasTool=true | 1 |

#### 건설 Actions
| Action | Precondition | Effect | Cost |
|---|---|---|---|
| BuildHouse | wood>=20, stone>=10, atBuildSite=true | houseBuilt=true | 15 |
| BuildStorehouse | wood>=15, stone>=5 | storageCapacity×2 | 10 |
| BuildCampfire | wood>=5 | nearFireplace=true | 3 |
| BuildTownHall | wood>=50, stone>=30, iron>=10 | townHallBuilt=true | 30 |
| BuildForge | stone>=20, iron>=15, townHallBuilt=true | forgeBuilt=true | 20 |
| BuildWatchtower | stone>=30, iron>=5 | enemyDetectRange×2 | 12 |
| BuildCommTower | wood>=30, stone>=20, copper>=20 | diplomacyUnlocked=true | 25 |

#### 전투 Actions
| Action | Precondition | Effect | Cost |
|---|---|---|---|
| AttackEnemy | hasWeapon=true, nearEnemy=true, healthLevel>40 | enemyDefeated=true | 7 |
| FleeFromEnemy | nearEnemy=true, healthLevel<40 | nearEnemy=false, atBase=true | 3 |
| CraftWeapon | iron>=5, nearForge=true | hasWeapon=true | 8 |
| AlertVillage | nearWatchtower=true | allVillagersAlert=true | 2 |

### 3.4 GOAP 플래닝 예시

**시나리오:** 주민 'Arin' hungerLevel=78 → Goal: SurviveHunger 활성화

```
플래너 탐색 결과:

경로 A: EatStoredFood (Cost: 2)
  Precondition 체크: hasFood=true ✅, atBase=true ✅
  → 즉시 실행 가능, 최적 선택

경로 B: HarvestWildBerries (Cost: 5+이동비용)
  Precondition 체크: nearBerryBush=false ❌
  → MoveToForest(Cost+3) 선행 필요 = 총 Cost 8

경로 C: CookMeal (Cost: 8+선행비용)
  hasRawFood=false → HarvestWildBerries 먼저(Cost 5)
  nearFireplace=false → ReturnToBase(Cost 2)
  → 체인: HarvestWildBerries → ReturnToBase → CookMeal = 총 Cost 18

최적 선택 → 경로 A 즉시 실행
만약 foodStock=0이면 → 경로 B 자동 선택 (Re-plan)
```

---

## 4. 주민(Villager) 시스템

### 4.1 주민 속성
| 속성 | 범위 | 위험 임계값 | 설명 |
|---|---|---|---|
| 체력(Health) | 0~100 | 20 이하 | 전투·사고로 감소, 0 = 사망 |
| 허기(Hunger) | 0~100 | 80 이상 | 시간 경과로 +3/시간 |
| 피로(Fatigue) | 0~100 | 90 이상 | 활동으로 증가, 수면으로 감소 |
| 행복도(Mood) | 0~100 | 20 이하 = 반란 위험 | 환경·이벤트로 변동 |
| 충성도(Loyalty) | 0~100 | 30 이하 = 명령 거부 ↑ | 플레이어 명령 수행 의지 |

### 4.2 주민 역할(Role) — Cost 보정 시스템
역할은 특화 Action의 Cost를 줄여 GOAP 플래너가 해당 Action을 선호하게 만든다.

| 역할 | 특화 Action Cost 감소 | 비특화 Cost 증가 |
|---|---|---|
| 나무꾼(Lumberjack) | ChopWood -50% | MineStone +30% |
| 광부(Miner) | Mine* -50% | ChopWood +30% |
| 건설자(Builder) | Build* -40% | Combat +50% |
| 전사(Warrior) | Attack* -40% | Build* +60% |
| 의료사(Medic) | Heal* -50% | Mine* +40% |
| 요리사(Cook) | CookMeal -60% | Attack* +80% |

### 4.3 명령 거부 로직
| 거부 조건 | 거부 메시지 (UI 표시) | AI 대안 행동 |
|---|---|---|
| hungerLevel > 85 | "너무 배고파요. 먼저 먹어야 해요!" | SurviveHunger 즉시 처리 |
| healthLevel < 25 | "부상이 너무 심해요. 치료가 먼저예요." | SeekMedicalAid |
| fatigueLevel > 90 | "지쳐서 쓰러질 것 같아요..." | Sleep |
| loyaltyLevel < 30 | "왜 제가 그걸 해야 하죠?" | 자율 P4~P5 Goal 수행 |
| nearEnemy + !hasWeapon | "무장 없이는 너무 위험해요!" | Flee → CraftWeapon |

---

## 5. 자원 & 경제 시스템

### 5.1 자원 종류 및 희소성
| 자원 | 획득 방법 | 주 용도 | 희소성 |
|---|---|---|---|
| 나무(Wood) | ChopWood | 건물 기본재, 연료 | 낮음 (초반 풍족) |
| 돌(Stone) | MineStone | 건물 강화재 | 낮음 |
| 철광석(Iron) | MineIron | 도구·무기·고급 건물 | 보통 |
| 구리(Copper) | MineCopperOre (특수 지역) | 고급 기계·Communication Tower | 높음 |
| 은(Silver) | MineOre (위험 지역) | 최상위 건물·특수 아이템 | 매우 높음 |
| 식량(Food) | Harvest/Hunt/Cook | 주민 생존 | 소모성 (지속 필요) |

### 5.2 건물 건설 비용표
| 건물 | Wood | Stone | Iron | Copper | Silver | 기능 |
|---|---|---|---|---|---|---|
| House | 20 | 10 | 0 | 0 | 0 | 수면 효율 +60% |
| Storehouse | 15 | 5 | 0 | 0 | 0 | 자원 용량 ×2 |
| Campfire | 5 | 0 | 0 | 0 | 0 | 요리 가능, 난방 |
| Town Hall | 50 | 30 | 10 | 0 | 0 | 팩션 기능 해금, 주민 모집 |
| Forge | 20 | 20 | 15 | 0 | 0 | 무기·도구 제작 |
| Watchtower | 10 | 30 | 5 | 0 | 0 | 적 탐지 범위 ×2 |
| Communication Tower | 30 | 20 | 10 | 20 | 0 | 외교·교역 해금 |
| Alchemist Lab | 15 | 10 | 5 | 5 | 0 | 의약품 제작 |
| Silver Citadel | 60 | 50 | 30 | 20 | 15 | 최종 승리 건물 |

### 5.3 경제 수치 기준값

**주민 1인 일일 소비량:**
- 식량: 3 units / 일
- 연료(나무): 1 unit / 일 (난방 활성화 시)

**자원 생산량 (주민 1인 기준, 8시간 작업):**
| 자원 | 일반 주민 | 전문 주민 (역할 특화) |
|---|---|---|
| 나무 | 8 / 일 | 15 / 일 (나무꾼) |
| 돌 | 5 / 일 | 10 / 일 (광부) |
| 철광석 | 3 / 일 | 7 / 일 (광부) |
| 식량 | 4 / 일 (단순 채집) | 12 / 일 (요리사) |

**Town Hall 건설 목표 달성 시간 (주민 3인, 역할 미특화 기준):**
- Wood 50 달성: (50 / (8×3)) = 약 2.1일
- Stone 30 달성: (30 / (5×3)) = 약 2.0일
- Iron 10 달성: (10 / (3×3)) = 약 1.1일
- **예상 Town Hall 건설 가능 시점: 약 3일 차** (병렬 수집 기준)

---

## 6. 위협 & 전투 시스템

### 6.1 위협 종류
| 위협 | 위험도 | 출현 타이밍 | 특성 |
|---|---|---|---|
| 소형 동물 (늑대, 멧돼지) | 1 | Day 1~ | 전사 1인으로 대응 가능 |
| 중형 몬스터 | 2 | Day 5~ | 전사 2인 이상 필요 |
| 대형 몬스터 | 3 | Day 15~ | 팀 전투 + 전략 필요 |
| 적 팩션 레이드 | 2~4 | Town Hall 건설 후 | GOAP 팩션 AI가 자율 결정 |
| 자연재해 (폭풍, 화재) | 1~3 | 랜덤 이벤트 | 즉각 대응 필요 |

### 6.2 적 팩션 GOAP AI
RimWorld의 스크립트 기반 레이드와 달리, 적 팩션도 GOAP AI로 운영된다:

- **Goal: ExpandTerritory** — 구리/은 자원 부족 시 인접 영토 침략 자율 계획
- **Goal: SurviveThreat** — 플레이어 공격 시 방어 계획 수립
- **Goal: GatherResources** — 자원 확보를 위한 채집 행동

침략 결정 조건:
```
copperStock < 10 AND nearPlayerTerritory == true
→ Goal: ExpandTerritory 활성화
→ Action 체인: AssessPlayerStrength → RaidIfFavorable OR AllianceProposal
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

관계값 범위: 0(전면전) ~ 100(동맹)

### 7.2 관계 변화 트리거
| 이벤트 | 관계 변화 |
|---|---|
| 영토 침범 | -20 |
| 교역 성공 | +10 |
| 주민 구조/지원 | +30 |
| 무력 충돌 | -50 |
| 선물 (희귀 자원) | +15 |
| 외교 무시 | -10 |

---

## 8. 맵 & 환경

### 8.1 맵 구조
- 크기: 100 × 100 타일
- 타일 종류: 평지 / 숲 / 암석 / 수역 / 위험 지대
- 시야 시스템: Fog of War (탐험으로 해제)

### 8.2 자원 노드 총량
| 자원 | 전체 수 | 플레이어 영역 | 무주지 | 적 영역 |
|---|---|---|---|---|
| 나무 | 80 | 30 | 25 | 25 |
| 돌 | 40 | 15 | 12 | 13 |
| 철광석 | 20 | 6 | 7 | 7 |
| 구리 | 10 | 2 | 5 | 3 |
| 은 | 5 | 0 | 3 | 2 |

> 은·구리는 의도적으로 무주지/적 영토에 집중 배치하여 침략 동기를 부여한다.

### 8.3 계절 시스템
| 계절 | 식량 생산 보정 | 특이사항 |
|---|---|---|
| 봄 | +20% | 없음 |
| 여름 | +30% | 화재 위험 ↑ |
| 가을 | +10% | 수확 이벤트 (일회성 식량 +50) |
| 겨울 | -50% | 연료 소비 ×2, 피로 증가 속도 +30% |

---

## 9. 플레이어 인터페이스

### 9.1 플레이어 역할
| 제어 유형 | 내용 | 주민 반응 |
|---|---|---|
| 건설 명령 | 특정 위치에 건물 큐 추가 | GOAP가 자율로 수행자 배정 |
| 역할 지정 | 주민 Role 설정 | Action Cost 보정 즉시 반영 |
| 우선순위 조정 | 팩션 전략 방향 설정 | Goal 가중치 전역 조정 |
| 긴급 명령 | 특정 주민에게 즉시 행동 지시 | 거부 조건 해당 시 거부 가능 |

### 9.2 자동화 레벨 설정
- **완전 자율 (Full Auto)**: 주민이 모든 것을 스스로 판단
- **반자율 (Semi-Auto)**: 자율 행동하되 플레이어 명령 우선 처리
- **수동 (Manual)**: 플레이어 지시 기반, GOAP가 최적 실행 경로 제안

---

## 10. 승리 / 패배 조건

### 패배 조건
- 모든 주민 사망
- Town Hall 파괴 + 생존 주민 2명 이하

### 승리 조건 (단계별)
| 단계 | 조건 |
|---|---|
| 1단계 (생존) | Town Hall 건설 + 주민 5명 이상 30일 생존 |
| 2단계 (지배) | 모든 적 팩션 제압 또는 전 팩션 동맹 체결 |
| 3단계 (번영) | Silver Citadel 완성 |

---

## 11. 기술 구현 개요

### GOAP 구현 방향
- Action Graph를 A* 알고리즘으로 탐색하여 최적 행동 체인 도출
- World State: `Dictionary<string, object>` 또는 비트플래그로 관리
- 50개 AI 유닛 동시 실행, Tick 방식 (0.1초 간격) 분산 처리
- Unity NavMesh 또는 A* Pathfinding Project 활용

### 성능 목표
| 항목 | 목표 |
|---|---|
| 목표 FPS | 60fps (50 유닛 동시) |
| GOAP 플래닝 비용 | 유닛당 < 0.5ms |
| A* 경로탐색 | 비동기 처리, < 1.0ms |
| 맵 타일 수 | 10,000개 (100×100) |

---

*작성일: 2026-06-25 | 버전: v0.1 초안*
