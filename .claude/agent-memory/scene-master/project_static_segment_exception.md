---
name: static-segment-exception
description: ep00s 를 정적 구간 3초 초과 3건을 넘긴 채 승인했다 — 1회성 예외이며 다음 회차가 선례로 인용하면 반려한다
metadata:
  type: project
---

`check.mjs` 의 「정적 구간 3초 이하」 ⚠ 를 **ep00s(2026-07-29)에서 한 번 넘겼다** — S7 5.8s · S9 4.4s · S10 3.4s.
승인 조건은 "`check.mjs` **실패** 0"이고 이것은 ⚠ 라 실패가 아니므로 승인 자체는 규정대로다.
다만 **1회성 예외로 기록했고, 기준을 낮춘 것이 아니다.**

**Why:** 대본이 게으른 게 아니라 kind 가 이벤트를 못 준다는 것을 엔진 코드로 직접 확인했다.
`rule`(showCue·strikeCue, label 은 strikeCue 파생) · `metrics`(showCue·dimCue, caption 은 reveal 파생) ·
`title(mood:'outro')`(appear·cut) — **셋 다 cue 이벤트 2개가 상한**인데 자막은 3줄이라
한 줄은 구조적으로 이벤트가 없다. 자막을 더 깎으면 반대쪽 상한(2초 미만)에 걸린다.
즉 대본 수정으로는 못 푼다.

**How to apply:**
- 근본 해법은 엔진이다 — `rule` 3번째 이벤트 / `metrics` caption 독립 cue / `title(outro)` 취소선 이후 이벤트.
- **셋 중 하나라도 들어오기 전까지, 같은 사유의 초과를 다음 회차에서 또 만나면 반려한다.**
  "ep00s 도 넘겼다"는 인용은 근거로 받지 않는다. 예외는 누적되면 기준이 된다.
- 판정할 때 검수팀의 "kind 문제다"라는 주장은 **엔진 파일을 직접 열어 cue 개수를 세어** 확인할 것.
  ep00s 에서는 주장이 사실이었지만, 확인 없이 받으면 다음엔 대본 게으름을 같은 말로 덮는다.

관련: [[verdict-ep00s]]
