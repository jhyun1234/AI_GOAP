---
name: blog-next-material-priority
description: 사용자가 명시적으로 지정한 다음 회차 블로그 소재 우선순위 커밋 — blog-planner는 이 목록을 먼저 소재로 고려한다
metadata: 
  node_type: memory
  type: project
  originSessionId: 5aefab25-46fa-45ca-9ad3-dd7933130087
---

# 블로그 파이프라인 다음 회차 우선 소재

**STATUS: CONSUMED (2026-07-11)** — 아래 지정된 2건은 원격 auto-run에서 한 편의 글로 묶여 실제 공개 발행됨 (post id 118729701893909598, https://gamedevclaude.blogspot.com/2026/07/goap-ai-ai.html). blog-planner는 이 파일을 무시해야 하며, 다음 사이클에서는 [[blog-last-published-commit]]의 `latest_commit: b930365` 이후 새 커밋만 후보로 삼는다.

---

**Fact (소비 완료된 원본 지시):** 사용자(2026-07-10)가 "허기 → 포만감 세만틱 반전" 및 "씬 배치본 자동 성격 배분" 두 커밋을 **반드시 블로그 소재로 사용해달라**고 명시적으로 요청했다.

**Why:** 사용자 관점에서 특히 서사 가치가 있는 변경이라 판단(플레이어 UX 관점에서 필드 방향을 뒤집는 결정, 게임 UX ↔ 코드 정합성 트레이드오프). 자동 스케줄이 없는 상태에서 이 커밋들이 다음 회차 blog-planner 실행 시 반드시 후보에 포함되도록 명시적 지시.

**How to apply:**
- blog-planner 다음 실행 시, 아래 커밋 2건을 최우선 소재 후보로 고려한다.
- 두 커밋을 **한 편의 글로 묶어도 좋음** (성격 배분 = 도입부/컨텍스트, Satiety 반전 = 본론). 실제 blog-planner 판단.
- 사용된 후에는 `latest_commit`을 [[blog-last-published-commit]]에 갱신하고 이 파일을 삭제(또는 "소비 완료" 마킹).

## 대상 커밋

- `549444d` — feat(ai): F-A 씬 배치본 자동 성격 배분 (VillagerFSM._initialPersonality) — #misc
- `b930365` — refactor(core): 허기 → 포만감 세만틱 반전 (HungerLevel → SatietyLevel) — #planner
  - 34 files, +707 / -312
  - 세만틱 반전에 딸린 연쇄 수정(P0 임계값·GOAP 목표 연산자·Effect Op·15개 RecruitData asset 값·테스트 4종·문서 2종)
  - 서사 포인트: "왜 게임 UX 관점에서 필드 이름과 방향을 뒤집었는가", "이름-의미 정합성 결정 (필드만 리네임, goal/effect 이름 유지)", "테스트가 방향을 반대로 강제하는 리팩터의 안전망"

## 관련 컨텍스트

- 이번 회차 세션 로그: `devlog/sessions/2026-07-10.md` (자동 append 훅으로 이미 기록됨)
- 이전 회차 게시 이력: [[blog-last-published-commit]] (latest_commit=4df41cc, GatherIron 무해 봉합)
- 사용자 검증: Play 모드에서 게으름 성격 대사 "왜 나만 시키지.." 출력 확인 완료 → 성격 배분+대사 발화 파이프라인이 실제로 동작한다는 살아있는 증거
