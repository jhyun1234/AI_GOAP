---
name: review-method-sfx-timing
description: 효과음 delayMs 검산법 — cue 역산식과 "자막 시작 ≠ 말 시작"(TTS 선행 무음 360~540ms)
metadata:
  type: feedback
---

효과음 `delayMs` 는 **두 축으로 따로 검산한다.** 하나만 보면 틀린다.

**Why:** ep02s 효과음 검수(2026-07-31)에서 여섯 개 전부를 다시 풀어 보니, 그림 정렬은
`cue` 역산으로 나오지만 *"나레이션이 이때 무슨 말을 하는가"* 는 그 식으로 안 나온다.
`timed.json` 의 `dur` 에는 TTS wav 의 **선행 무음 360~540ms · 후행 무음 500~700ms** 가
통째로 들어 있다. 즉 `line.t` 는 자막이 뜨는 시각이지 말이 시작하는 시각이 아니다.
이걸 안 재면 "이 소리가 말을 먹는가"도 "이때 그 단어를 발음하는가"도 판정할 수 없다.

**How to apply:**

① **그림 축** — `cue(i, lead, span)` = `clamp((ts−a)/(b−a))`, `a = lᵢ.t−lead`,
`b = lᵢ.t + lᵢ.dur×span` (`engine/engine.js:240-245`). `lᵢ.dur` 는 발화+pauseAfter
(`engine.js:156`). 효과음은 `line.t + delayMs` 에 놓인다(`render.mjs:111`) — 그림과 **같은 기준선**.
```
c = (delayMs/1000 + lead) / (lead + dur×span)
delayMs = (c × 창 − lead) × 1000
```
`ease` 는 smoothstep 이라 `ease(c)=0.5 ⇔ c=0.5`. **이중 이징**(`lerp(a,b,ease(ak))` 에
`ak` 가 이미 `ease(cue(...))`)을 흘리지 말 것 — nextnote·wardline 이 그렇다.
🔴 `cue` 를 안 쓰고 `frac(t/CYCLE)` 로 도는 그림이 있다(tripwire). 그런 샷은 **샷 안 절대시각**
으로 풀고 자막 시작 시각과 대조해야 한다. 자막 인덱스만 보면 절대 못 잡는다.

② **말 축** — 줄별 wav(`timed.json` 의 `file`)를 20ms 창 RMS 로 훑어 말 시작·끝을 재고,
소리 시각의 단기 RMS 를 직접 잰다. 판정 기준은 나레이션 **평균** RMS(ep02s 0.051)가 아니라
**그 순간의 값**이다 — 유성 구간은 0.11~0.15 라 평균과 비교하면 소리에 부당하게 불리하다.
말이 쉬는 골이나 말 시작 직전 무음에 떨어진 소리는 마스킹이 0 이고, 그게 가장 좋은 자리다.

③ `sfx.mjs` 의 `synth(kind, opt)` 를 씬의 실제 인자로 **직접 돌려** 길이·피크·RMS·영교차
주파수를 잰다. 같은 kind 가 두 번 나올 때 "성격이 갈렸나"는 이 숫자로만 판정할 수 있다.

`check.mjs` 에는 효과음 항목이 **없다** — 기계가 아무 증거도 안 준다. 위 셋이 증거 전부다.
관련: [[review_method_shot_timing]] · [[feedback_review_blind_spots]]
