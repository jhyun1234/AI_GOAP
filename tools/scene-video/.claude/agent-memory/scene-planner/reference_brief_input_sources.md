---
name: reference-brief-input-sources
description: 브리프에 필요한 값(출처 커밋 범위·글 제목·발행 상태·다음 편 막)이 실제로 어느 파일에 있는지
metadata:
  type: reference
---

브리프 §1 표를 채울 때 뒤져야 할 곳 — 매번 찾아 헤매지 말 것.

- **출처 커밋 범위 · 글 제목 · 발행 상태(LIVE/DRAFT) · blogger post id**
  → `tools/blog-automation/state/blog_last_published_commit.md` 안에 **회차별 이력 블록**이 있다
  (`### 이전 회차 이력 (2026-07-21, M8 관계·소유·부탁)` 같은 제목으로 날짜별). `selected_commits_range` 필드가 정본.
  ⚠️ 이 파일의 커밋 수와 `git log` 실측이 다를 수 있다 — 발행 기록이 **서사 무관 커밋을 일부러 제외**하기 때문.
  브리프에는 둘 다 적어라(ep10s: 기록 18커밋 / git 20커밋).
- **밀스톤 한 줄 요약 · 발행 링크** → `tools/blog-automation/BLOG_COVERAGE.md` 하단의 번호 표.
- **막(act) · 다음 편 주제** → `Docs/영상_시리즈_구성안.md` 「한눈에 보기」 표. **URL 로 대조**(번호로 세지 말 것).
  막 설명 문단은 그 아래 「막마다, 왜 이 자리인가」에 있고, 4막 = *"만드는 이야기 → 잃는 이야기"*.
- **규격 실측치**(길이 상한·본문 하한·예고 상한) → `Docs/영상_25초_전환_실행명세서.md` §2-B 와
  `tools/scene-video/check.mjs` 의 실제 임계값. **정의서보다 이 둘이 최신이다.**
- **직전 편이 시청자에게 한 약속** → `episodes/<직전id>/scene.json` 의 `outro.next` (+ `outro.siblings`).
  승인 여부는 `episodes/<id>/notes/verdict.md`.

관련: [[project_hook_and_number_ledger]]
