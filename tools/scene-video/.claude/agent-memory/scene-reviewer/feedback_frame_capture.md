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

관련 = [[cue-timing-audit-method]]
