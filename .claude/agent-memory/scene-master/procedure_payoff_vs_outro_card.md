---
name: procedure-payoff-vs-outro-card
description: 마지막 샷의 페이오프를 아웃트로 카드가 덮는지 마스터가 직접 검산하는 4줄 산수 (ep10s-2 에서 확립)
metadata:
  type: reference
---

# 아웃트로 카드가 페이오프를 덮는가 — 마스터 직접 검산

**Why:** ep08s-3 이 바로 이 자리에서 반려됐다(판정 알파 0.078 · 완성 0.54초 지각).
마지막 샷은 「자막에 물렸다」만으로 부족하고 **그 자막이 끝나기 전에 카드가 뜨는지**를 봐야 한다.
검수 보고를 인용하지 말고 kind 파일을 직접 열어 검산한다.

**How to apply:** `build/timed.json` 의 `rel` 과 kind 의 `cue(i, lead, span)` 만 있으면 된다.

```
rel        = dur + pauseAfter            ← engine.js:163. ⚠️ wav 길이가 아니다
샷 길이     = Σ rel + SHOT_TAIL(350ms)
카드 시각   = TOTAL − 2600ms             ← 샷 시작 기준으로 환산할 것
W          = lead + rel × span           ← 그 자막의 cue 창
진행도 p 인 시각 = p × W − lead          ← 자막 시작으로부터
```

페이오프의 `p` 는 kind 코드의 문턱식을 **1 로 놓고** 역산한다. 예(`secondcard.js`):
`vk = ease(clamp((c0 − snapAt) / (1 − snapAt)))` = 1 → `c0 = 1.0`.

## 🔴 여유의 손잡이는 `span` 하나뿐 — `lead` 를 바꾸라고 요구하지 말 것

완성 시각 = `자막 시작 + rel × span` 이므로 **`lead` 는 시작만 당길 뿐 완성을 못 바꾼다.**
그래서 예고 자막의 `shelfCue`(류) `lead`·`span` 은 **아웃트로 여유의 유일한 출처**다.
라벨이 안 읽힌다는 지적이 나와도 **문턱(`sk > x`)만 내리게 하고 `lead`·`span` 은 건드리지 않는다.**
ep10s-2 가 그렇게 처리했다(`sk > 0.45` → `0.20`, 타이밍 영향 0).

## 통과 형태 (ep10s-2 실측)

페이오프는 **전부 cue 0(앞 자막) 안에서 끝내고**, 예고 자막(cue 1)에는 **짧고 큰 변화 하나**만 건다.
`CODE 0` 완성 +2,369ms → 카드까지 **1,174ms** · 선반 판 완성 +3,131ms → **412ms**.

관련: [[verdict-ep10s-2]] · [[procedure-sfx-retiming-fragility]]
