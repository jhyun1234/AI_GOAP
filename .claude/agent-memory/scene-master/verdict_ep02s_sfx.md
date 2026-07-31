---
name: verdict-ep02s-sfx
description: ep02s 효과음 라운드 = APPROVED (2026-07-31). 파이프라인 첫 효과음 회차 — sfx 판정 4항목과 check.mjs 사각지대
metadata:
  type: project
---

# ep02s 효과음 = APPROVED (2026-07-31, 반려 0회)

승인·렌더까지 끝난 회차에 **효과음만** 나중에 얹은 재작업. 6샷 6개
(S3B tick · S5 drop · S7 sweep · S8 latch · S9B riser · S10 tick) / 95.539초.
판정서 = `tools/scene-video/episodes/ep02s/notes/verdict-sfx.md`.

**Why:** 효과음이 2026-07-30 에 파이프라인에 들어온 첫 회차라, 여기서 세운 판정 절차가
다음 회차의 기준이 된다. 그리고 **`check.mjs` 에 효과음 검사가 한 항목도 없다**(grep 0건) —
`at` 이 틀려도 `render.mjs:109` 가 경고 한 줄 찍고 조용히 렌더된다. 기계 게이트가 없는
유일한 계층이라 마스터가 손으로 푸는 것 말고 방법이 없다.

**How to apply:** 효과음 판정은 이 넷을 직접 재계산한다. 검수 리포트는 근거가 아니다.
1. **`at` 해석 경로** — `render.mjs:103-111` 의 `base + s.at` 이 `engine.js:340-342`
   → `buildTimeline`(`:151-159`)의 평탄화 배열과 같은 순서인지. `at` = **샷 안 인덱스**가 정답.
   6개를 평탄 인덱스로 풀어 **그 줄의 `say` 문자열까지** 찍어 대조할 것.
2. **draw 사건과의 물림** — `cue`(`engine.js:240-245`) + `ease=smoothstep`(`lib.js:7`)을
   그대로 구현해 kind 식을 역산. 🔴 `cue` 를 안 쓰고 `t`(샷 로컬 초, `engine.js:221`)로 도는
   그림이 있다 — 자막 인덱스만 보면 절대 못 잡는다.
3. **꼬리 vs 다음 자막** — `delayMs + dur` 을 그 줄의 `dur`(발화+pauseAfter, `engine.js:156`)과 대조.
4. **음량** — `sfx.mjs` 의 `synth` 를 직접 불러 합성. `gain` 은 피크가 아니라 **목표 단기 RMS**.
   피크 천장 0.34(`sfx.mjs:145`), 합계 리미터 0.98(`render.mjs:123`).

🔑 **`delayMs` 가 200~400ms 경험칙 밖이어도 반려 사유가 아니다.** 구속력 있는 규칙은
`scene-writer.md:123-124` 의 *"그림이 그 사건을 그리는 순간에 맞춘다"* 이고 200~400 은 "대개"다.
이 회차는 넷(440·730·1130·1150)이 밖이었고 전부 draw 역산의 결과였다.

관련: [[verdict_ep02s_redo]] · [[procedure_sfx_retiming_fragility]] · [[feedback_repo_only_grounds]]
