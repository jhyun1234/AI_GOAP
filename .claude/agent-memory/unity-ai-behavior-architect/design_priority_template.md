---
name: design-priority-template
description: AI 의사결정 우선순위 계층 템플릿 — P0 서브 우선순위, ConflictScore, Cost Modifier
metadata:
  type: reference
---

# 우선순위 계층 템플릿 (AI Village 검증 버전)

## P0 서브 우선순위 패턴

P0 Goal이 여러 개 동시 발동될 때의 처리 순서:
1. SurviveInjury (healthLevel < 20) — 즉사 위험, 최우선
2. SurviveHunger (hungerLevel > 80) — 이차 사망 위험
3. SurviveFatigue (fatigueLevel > 90) — 기절/기능 불능

**핵심:** P0는 Re-plan 쿨다운 무시, Depth 3 이하 제한 (빠른 탐색).

## ConflictScore 계산 공식

```
ConflictScore = Σ(Need_urgency[i] × Order_impact[i])
임계값 = 2.5 × (loyaltyLevel / 50)
거부: ConflictScore >= 임계값
```

Need_urgency 계산:
- hungerLevel > 80  → (hungerLevel - 80) / 20
- healthLevel < 20  → (20 - healthLevel) / 20
- fatigueLevel > 90 → (fatigueLevel - 90) / 10
- nearEnemy == true → 0.8 고정

loyalty < 30 시 임계값 = 2.5 × (30/50) = 1.5 → 매우 낮음 → 거부 빈발.

## Cost Modifier 4구간 (loyalty 기반)

| loyalty 구간 | Cost 배율 | 설명 |
|---|---|---|
| 70~100 | ×0.7 | 적극 수행 |
| 50~69 | ×1.0 | 보통 |
| 30~49 | ×2.5 | 소극적 |
| 0~29 | ×6.0 | 거의 수행 불가 |

## 명령 거부 7가지 케이스 (우선 적용 순서)

1. REFUSE_INJURY (healthLevel < 20)
2. REFUSE_HUNGER (hungerLevel > 80)
3. REFUSE_FATIGUE (fatigueLevel > 90)
4. REFUSE_LOYALTY (loyaltyLevel < 30)
5. REFUSE_DANGER (nearEnemy + !hasWeapon)
6. REFUSE_NO_TOOL (hasTool == false + 획득 경로 없음)
7. REFUSE_INSUFFICIENT_RESOURCES (자원 가용량 부족)

**Why:** AI Village GDD v0.4 기반 확정 수치. 이후 밸런스 조정은 ScriptableObject로 처리.
**How to apply:** 다른 프로젝트의 AI 명령 거부 설계 시 이 구조를 기준점으로 활용.
