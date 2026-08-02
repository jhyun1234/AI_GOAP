---
name: cue-timing-audit-method
description: cue/sfx 물림을 실측 검증하는 계산법 — rel dur 은 pauseAfter 포함, smoothstep 역함수는 ease(k)=0.9 → k=0.8065 (0.729 아님)
metadata:
  type: feedback
---

`cue` 물림은 눈으로 인덱스만 보지 말고 **초로 환산해 검증한다.** 계산에 필요한 사실 둘이
코드에 흩어져 있어 매번 다시 찾게 된다.

**Why:** 지금까지 두 번 사고가 났다(ep02s `tripwire` = `frac(t/5.2)` 로 도는 조건에 sfx 를 물림,
ep00i `runmark` = 다른 cue 의 진행도로 만든 문턱이 1.64초 먼저 터짐). 인덱스만 보면 둘 다 멀쩡해 보인다.

**How to apply:**

1. `engine/engine.js` `buildTimeline` — `rel[i].dur` 은 **TTS dur + `pauseAfter`** 다.
   `build/timed.json` 의 dur 만 쓰면 창이 짧게 나온다.
2. 창 `W = lead + rel[i].dur × span`, 진행도 p 인 시각 = `p × W − lead` (자막 시작 기준 초).
3. `ease` 는 smoothstep `3k²−2k³` 다. **역함수를 손으로 넣지 말고 이분탐색으로 풀 것** —
   실측 `ease(k)=0.9 → k=0.8065` · `0.95 → 0.8646` · `0.72 → 0.6513` · `0.5 → 0.5`.
   🔴 작성팀이 여러 회차에서 `ease(k)=0.9 → k=0.729` 로 적어 왔는데 **틀린 값**이다
   (0.729 = 0.9³). span 이 큰 샷(0.9)에서는 이 오차만으로 0.5초가 어긋난다.
4. `render.mjs` 의 sfx 배치는 `line.t + delayMs` — **자막 시작 기준**이 맞다.
   `sweep` 은 `sin(PI·k)` 포락선이라 **에너지 정점이 dur/2 뒤**다. 시작 시각이 아니라 정점을 맞춰야 한다.
5. 판정선 = ±20프레임(0.667초). 그 안이면 통과지만, 10프레임 넘게 어긋나면 사유를 적어 올린다.

`t` 로 도는 것(`frac(t/N)`)은 전부 장식 루프인지 확인한다 — 거기에 sfx 나 나레이션이 물려 있으면 반려.
`grep -n "frac(t\|Math.sin(t" kinds/*.js` 로 한 번에 뽑힌다.

관련 = [[sfx-gain-ceiling-is-per-parameter]] · [[frame-capture-for-review]]
