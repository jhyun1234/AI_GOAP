---
name: procedure-cue-window-math
description: cue 창·split·delayMs·아웃트로 여유를 마스터가 직접 검산하는 식 — 분모는 rel, 어절은 dur, 카드는 TOTAL-OUTRO_MS(현행 3000)
metadata:
  type: reference
---

# cue 타이밍 직접 검산 (검수 산수를 믿지 않고 다시 푸는 법)

**Why:** 작성팀이 `split` 을 감으로 잡아 0.42 로 냈다가 자진 정정한 일이 있었고(ep10s-3),
그 값은 실측이 없어도 틀린 값이었다. 분모를 `dur` 로 잘못 잡으면 같은 자막에서 0.55 대신
0.58 이 나와 그림이 다른 낱말을 먹는다. **산수는 검수 보고가 아니라 엔진 코드에서 다시 푼다.**

**How to apply:** 아래 다섯 줄이 정본이다(경로 = `tools/scene-video/engine/engine.js`).

🔴 **행 번호는 자주 밀린다 — 인용하기 전에 `grep` 으로 현재 위치를 확인할 것.**
아래 괄호는 2026-08-10(ep13s-2 판정) 시점의 실측 위치다.

- (`:199`) `dur = durOf(si,li,say) + (l.pauseAfter ?? 0)` — 자막 레코드의 dur 은 **wav + pause**
- (`:203`) 샷 dur 에 `SHOT_TAIL * 1000`(0.35초) 이 **샷마다** 더해진다
- (`:205`) `rel = ls.map(l => ({ t: (l.t − ls[0].t)/1000, dur: l.dur/1000 }))`
  → `rel.dur` 도 **wav + pause** 이고, `t` 는 **샷 상대 좌표**다
  🔑 그래서 **앞 샷의 자막을 고쳐도 뒤 샷의 `rel` 은 안 움직인다.** `tts.mjs:64 keyOf` 가
  `voiceName|speed|steps|say` 해시로 wav 를 캐시하므로 안 바뀐 줄은 바이트가 같다.
  → 「자막 한 줄 고치면 `delayMs` 가 다 흔들린다」는 **거짓 사유**다(ep13s-2 에서 반박됨).
- (`:396`) `cue(i, lead, span)` = `a = l.t − lead`, `b = l.t + l.dur*span`, `(ts − a)/(b − a)` 로 0~1 클램프
- 🔴 (`:219`) **`OUTRO_MS = 3000`** · 아웃트로 카드는 `t ≥ TOTAL − 3000`
  (옛 메모의 `2600` 은 **폐기** — 2026-08-10 실사에서 3000 이었다)

## 검산 세 가지

**① `split` / cue 진행률 → 창 분모는 `rel`**
`W = lead + rel × span`, `c = (경계시각 + lead) / W`.

**② 경계 시각 / 어절 배분 → `dur`(말하는 시간)로 잰다.**
pause 는 말이 끝난 뒤의 침묵이라 어절 안에 안 들어간다.
`경계 = (경계 앞 글자수 / spoken 글자수) × dur`. spoken = `say` 에서 `[.,?!…·「」"'\s]` 제거.
🔑 **한 식 안에서 rel 과 dur 을 섞으면 pauseAfter(130~250ms)만큼 통째로 밀린다.**

**③ 아웃트로 여유** — 마지막 샷에서만 쓴다.
`카드(샷 기준) = (마지막 두 자막 rel 합 + 350) − OUTRO_MS`(현행 **3000**).
페이오프 완성 시각 = `그림의 완성 진행률 × W − lead`. 둘의 차가 여유다.
ep10s-3 = 페이오프 +1,975ms vs 카드 +4,249ms → 2,274ms. 예고 그림은 span 0.06 으로 조여 242ms.
🔴 ep08s-3 사고 = 마지막 자막이 짧아 페이오프가 카드 뒤로 밀린 것. **판정 그림은 예고 자막이
아니라 그 앞 자막에 걸려 있어야 한다.**

## 곁들여 확인할 것
- `timed.json` 이 실물인가 — `voice` 가 `SYNTHETIC-NOT-REAL` 이 아니고 `build/audio/*.wav` 가 줄 수만큼
- smoothstep 역산이 필요하면 `3x² − 2x³ = y` 를 이분탐색으로 푼다(ease 가 smoothstep)

관련: [[procedure_sfx_retiming_fragility]] · [[verdict_ep10s3]]
