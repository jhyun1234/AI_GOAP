---
name: sfx-gain-ceiling-is-per-parameter
description: sfx 피크 천장(0.46)은 kind 별 상한이 아니라 kind+dur+freq 조합마다 다르다 — 반드시 synth() 를 실제로 호출해 경고를 확인할 것
metadata:
  type: feedback
---

효과음 gain 판정은 **표에 적힌 "kind 별 상한"을 믿지 말고 `synth()` 를 그 sfx 의 실제 인자로
호출해서 경고가 뜨는지 본다.**

**Why:** 가이드에 적힌 상한(`tick` 0.065 · `latch` 0.071)은 **기본 인자**(tick: dur 0.06 · freq 1400)로
잰 값이다. 같은 kind 라도 더 짧고 더 높으면 피크/RMS 비가 올라가 천장에 먼저 걸린다.
eptest-m11 S9 에서 실측 — `tick(dur 0.05, freq 1660)` 은 무경고 상한이 **0.0590** 이라
기본 gain 0.060 만으로도 `⚠️ sfx tick: 피크 천장(0.46)에 걸려` 가 뜬다.
작성팀은 "tick 은 0.065 가 상한이라 기본 0.060 그대로 뒀다" 고 근거를 적었는데, 그 0.065 는
다른 인자의 값이었다. 표를 믿었으면 그냥 통과시켰을 것이다.

**How to apply:** 검수 때 씬의 모든 `shot.sfx` 를 이 한 줄로 훑는다(경고가 stderr 로 나온다).

```
node -e "import('./sfx.mjs').then(m=>{const fs=require('fs');
  const sc=JSON.parse(fs.readFileSync('episodes/<ep>/scene.json','utf8'));
  for(const sh of sc.shots) for(const s of sh.sfx||[]) { console.log(sh.id,s.kind); m.synth(s.kind,s,44100); }})"
```

무경고 상한을 알고 싶으면 `console.warn` 을 가로채고 gain 을 이분탐색한다. 참고 실측값:
`tick` 기본 0.0657 · `tick(0.09s/880Hz)` 0.0793 · `tick(0.05s/1660Hz)` **0.0590** ·
`tick(0.052s/1180Hz)` **0.05979** · `sweep(0.58s/seed 43)` 0.18435.

🔴 **작성팀이 반복해 쓰는 거꾸로 된 사유** (ep09s-1 · ep10s-1 두 번 실측):
> 「`dur` 이 기본보다 **짧아** 50ms 창에 담기는 에너지가 적으므로 천장까지 **여유가 더 있다**」

**정반대다.** `synth()` 는 `g = gain / loud`(loud = 50ms 창 최대 RMS)로 이득을 정하므로
창의 에너지가 **적을수록 `g` 가 커지고 `peak × g` 가 천장에 더 빨리 걸린다.**
짧을수록 여유가 **준다.** 이 문장이 보이면 그 자리는 거의 확실히 걸려 있다 — 바로 호출해 보라.

🔑 그리고 **인자를 앞 회차에서 복사해 오면 그때 내렸던 gain 만 원복되는 사고**가 난다.
ep09s-1 S3 이 `tick(0.052/1180)` 의 gain 을 0.060 → **0.058** 로 내리고 그 사유까지 `why` 에
적어 뒀는데, ep10s-1 S2 가 같은 인자를 쓰면서 gain 만 0.060 으로 되돌려 경고가 다시 떴다.
**같은 인자 조합이 앞 회차에 있으면 그 회차의 `why` 를 먼저 읽어라** — 이미 푼 문제다
(그리고 그 자체가 [[recurring-script-defects]] ③ 돌려막기의 방증이다).

관련 = [[cue-timing-audit-method]]
