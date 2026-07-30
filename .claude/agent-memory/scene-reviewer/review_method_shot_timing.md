---
name: review-method-shot-timing
description: 스틸 뽑을 샷 시각은 timed.json 합만으로 계산하면 틀린다 — 엔진이 샷마다 SHOT_TAIL 0.35초를 더한다
metadata:
  type: feedback
---

# 스틸 시각 계산 — 샷마다 0.35초가 더 붙는다

`render.mjs --still <초>` 로 프레임을 뽑을 때, `build/timed.json` 의
`dur + pauseAfter` 만 누적해서 샷 경계를 잡으면 **뒤로 갈수록 어긋난다.**
`engine/engine.js` 의 `buildTimeline()` 이 샷마다 `SHOT_TAIL = 0.35초` 여운을
추가하기 때문이다(`dur = 마지막줄끝 - 첫줄시작 + SHOT_TAIL*1000`).
12샷이면 마지막 샷에서 최대 4.2초 차이가 난다.

올바른 누적:
```
샷 시작 = 이전 샷 시작 + (그 샷 자막들의 dur+pause 합) + 350ms
```
`l.dur` 에는 이미 `pauseAfter` 가 포함돼 있는지도 확인할 것 — engine 은
`durOf(...) + (l.pauseAfter ?? 0)` 로 합쳐서 한 줄의 dur 로 쓴다.

**Why:** ep01s 1차 검수에서 이걸 틀려 S5 스틸을 샷 중간에서 뽑았고,
"예산 고리가 안 찬다 = 나레이션과 화면이 반대"라는 **없는 결함을 반려 사유로
올릴 뻔했다.** 실제 샷 끝(67.8s)에서 다시 뽑으니 고리는 100% 차 있고 `소진`
딱지도 정상이었다.

**How to apply:** 결함을 의심하면 **확정 전에 시각을 다시 유도하고 스틸을 재취득한다.**
특히 "cue 로 도달해야 할 상태에 도달하지 않았다" 류의 의심은 거의 항상 타이밍
계산 실수다. 그리고 오판했으면 review.md 에 그 사실을 적어 남긴다 — 증거 없이
판정하지 않는다는 원칙은 내 오판에도 적용된다([[ep01s-rework-review]] §4).
