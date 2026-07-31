---
name: sfx-timing-from-draw
description: 효과음 delayMs 는 자막 cue 가 아니라 kind 의 draw 를 풀어서 낸다 — 사건이 t 로 도는 kind 가 있어서 cue 인덱스만 믿으면 빈 화면에 소리가 난다
metadata:
  type: project
---

효과음 `at`·`delayMs` 는 **자막 내용이 아니라 그 샷 kind 의 `draw` 를 풀어서** 정한다.
`cue(i, lead, span)` 은 `a = tᵢ − lead` ~ `b = tᵢ + durᵢ × span` 의 0~1 이고
`durᵢ` 는 **발화 + pauseAfter** 다(`engine/engine.js:156, 240-245`). 창 = `lead + durᵢ × span`.

🔴 **함정 — 사건이 `cue` 가 아니라 `t` 로 도는 kind 가 있다.**
ep02s `tripwire.js` 의 걸림은 `u = frac(t / 5.2)` 로 돌고 `trapCue` 는 라벨만 붙인다.
자막 인덱스만 보고 `trapCue` 자리(at2)에 `latch` 를 놓으면 그 순간 실선은 곧고
화살표는 정방향이라 **아무 일도 안 일어나는 화면 위에서 소리가 난다.**
실제 걸림은 at1+324ms 였다. `cue` 이름과 사건 시각이 일치한다고 가정하지 말 것.

**Why:** ep02s 효과음 재작업(2026-07-31)에서 사용자 참고 배치 6개 중 3개가 draw 와
어긋나 있었다 — S8(위 사례) · S5(회전은 `flipCue`=at0 이 몰고 at1 은 밑줄만 자란다) ·
S3(wardline 은 알파·위치가 전부 연속 램프라 물 이산 사건이 아예 없다).
자막만 읽으면 셋 다 그럴듯해 보인다.

**How to apply:** 소리를 달기 전에 그 kind 에서 **가지가 갈리는 지점**을 찾는다 —
`if (k > 0.4)` 로 색이 바뀌거나(`ink`→`accent`), `absScale < 0.06` 처럼 다른 그림이 켜지거나,
`inK` 가 0→1 로 글자를 통째로 여는 자리. 알파가 `clamp(k × 1.4)` 로 램프하기만 하는 곳은
소리를 물 자리가 아니다. 그런 샷은 **조용히 두는 게 맞다.**
`riser`·`sweep` 처럼 긴 소리는 시작점이 아니라 **에너지 정점**(sweep 은 `sin(PI·k)` 라
한가운데)을 사건에 맞춘다.

관련: [[feedback-restraint-is-design]] · `episodes/ep02s/notes/writer-sfx.md` 에 6항목 전부의 계산이 있다.
