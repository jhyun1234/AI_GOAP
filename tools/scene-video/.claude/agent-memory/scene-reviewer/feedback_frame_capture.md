---
name: frame-capture-for-review
description: check.mjs 가 못 보는 것(도형 겹침·라벨 가림·자막 옮겨적기)은 PNG 스틸을 뽑아야 보인다 — lib-node.mjs 의 openEngine/shot 재사용법
metadata:
  type: feedback
---

`check.mjs` 14종을 전부 통과해도 **도형끼리 겹쳐 라벨을 덮는 결함은 안 잡힌다.**
검수 때 샷마다 스틸을 뽑아 눈으로 본다.

**Why:** eptest-m11 은 14종 전부 OK 였는데, 스틸을 보니 S4 에서 흰 토큰이 `주머니 상한 8`
라벨 위에 앉아 '상한' 두 글자를 덮고 있었다. 픽셀 점검은 팔레트·잘림·정적 구간만 보고
"무엇이 무엇을 가렸는가"는 보지 않는다. 자막을 화면이 옮겨 적었는지도 스틸이 제일 빠르다.

**How to apply:** `tools/scene-video/` 에 임시 mjs 를 만들어 돌리고 지운다.
(`lib-node.mjs` 를 절대경로로 import 하면 `ERR_UNSUPPORTED_ESM_URL_SCHEME` 이 난다 —
스크립트를 `tools/scene-video/` 안에 두고 `./lib-node.mjs` 로 상대 import 할 것.)

```js
import fs from 'fs';
import { openEngine, shot } from './lib-node.mjs';
const { cdp, close } = await openEngine('<ep>', { quiet: true });
for (const ms of process.argv.slice(2).map(Number))
  fs.writeFileSync(`<outdir>/${ms}.png`, await shot(cdp, ms));
await close();
```

찍을 시각은 `build/timed.json` 으로 만든다 — 샷 시작을 누적할 때 **샷마다 `SHOT_TAIL` 0.35초**를
더해야 실제 타임라인과 맞는다(그래서 `timed.json` 의 totalMs 보다 영상이 0.35×샷수 만큼 길다).
🔴 이산 분기 시각을 정확히 찍으면 `>` 비교라 **직전 프레임**이 나온다. +300ms 쯤 뒤도 같이 찍을 것.

## 🔴 흰색×강조색 겹침은 팔레트 게이트가 **구조적으로** 못 본다 (증명, ep08s-2 검수)

`#00FF88` 을 흰색(또는 흰 위의 회색) 위에 알파 a 로 얹으면 결과는
`r=255(1-a) · g=255 · b=136a+255(1-a)` 이고, hue = `60×((b−r)/c + 2)` 에서
`(b−r)/c = 136a/255a = 0.533` 으로 **a 가 약분돼 사라진다** → 어떤 알파에서도 hue 는 정확히
**152도**, `check.mjs` 의 accent 대역(138~168) 한복판이다.
🔑 그래서 "팔레트 통과 = 안 섞였다"는 **절대 성립하지 않는다.** 작성팀이 z 순서를 짚어
자기신고했더라도 반드시 스틸로 확인한다. 실제로 볼 것은 색이 아니라 **흰 글자·숫자가
강조색에 훑히는가**(8-1 반려 형태)이고, 그건 라벨의 y 대역과 강조색 요소의 y 대역을
코드에서 뽑아 겹치는지 보면 스틸 없이도 1차로 걸러진다.

관련 = [[cue-timing-audit-method]]
