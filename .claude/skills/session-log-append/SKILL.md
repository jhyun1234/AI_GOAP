---
name: session-log-append
description: devlog/sessions/YYYY-MM-DD.md에 세션 로그를 수동으로 append할 때 사용.
  기본 흐름은 PostToolUse 훅(tools/devlog/append-session-log.sh)이 자동 처리한다 —
  이 스킬은 훅 실패 복구, 소급 정리, 하루 마감 시 왜/막힘/다음 절 보강 등에만 발동한다.
  중복 판정 기준: 60분 이내 + 태그 완전 일치 → 병합.
---

# 세션 로그 append 절차

## 1. 오늘 파일 상태 확인
- `devlog/sessions/YYYY-MM-DD.md` 없으면 새로 생성한다.
- 있으면 파일의 마지막 `## [HH:MM]` 블록을 읽는다.

## 2. 중복 판정 (3개 모두 성립 시 병합)
- **시간 간격 60분 이내** (마지막 블록의 HH:MM과 현재 시각 비교)
- **태그 조합 완전 일치**
- **요약 주제 동일** (같은 파일군 · 같은 이슈 번호 · 같은 서브시스템 다룸)

애매하면 신규 처리. 과병합보다 과분리가 안전하다.

## 3a. 병합 처리
- "무엇을 했나:"에 새 bullet 추가
- "변경 파일:" 목록에 새 파일 append (중복 제거)
- "다음에 할 일:"은 새 정보로 갱신 (덮어쓰기)
- 헤더의 시간·요약은 유지 (첫 시작 시간)

## 3b. 신규 append (devlog-workflow.md 템플릿)
- 태그는 대상 파일 경로로 추정:
  - `Assets/Scripts/Core/GOAP/` → `#planner`
  - `ActionDatabase.cs` / `Registry` → `#action-system`
  - `WorldState`, `PlanningSlots` → `#world-state`
  - `GoalArbiter`, `GoalSelector` → `#goal-selection`
  - `Sensor`, `Perception` → `#sensor`
  - `Assets/Tests/` → `#debug-viz`
  - `PlannerJob`, 캐싱/JPS → `#performance`
  - `MessageBus`, `Faction` → `#multi-agent`
  - 위 어디에도 안 걸리면 `#misc`
- **무엇을 했나:** 커밋의 실제 변경 요지 (사실만)
- **왜 이렇게 했나:** 판단이 필요했을 때만 1~2줄, 아니면 생략
- **막혔던 문제 / 해결 방법:** 있을 때만
- **다음에 할 일:** 자연스러운 후속 작업 1줄
- **변경 파일:** `git diff --name-only HEAD~1..HEAD` 결과

## 원칙
- **1 커밋 = 1 append 시도.** 병합으로 처리하더라도 시도 자체는 반드시 있어야 한다.
  세션 로그가 비면 blog-planner가 굶어 파이프라인이 스킵된다.
- 사실만 기록. 감상·평가·SEO 가공은 blog-writer/blog-editor 담당.
- 개인 식별 정보 · 상업적으로 민감한 수치는 로그에도 넣지 않는다 (기획서 3장 필터).
