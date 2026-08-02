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
`tick` 기본 0.0657 · `tick(0.09s/880Hz)` 0.0793 · `tick(0.05s/1660Hz)` **0.0590**.

관련 = [[cue-timing-audit-method]]
