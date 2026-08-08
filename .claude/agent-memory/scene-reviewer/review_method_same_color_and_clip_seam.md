---
name: review-method-same-color-and-clip-seam
description: 팔레트·잘림 게이트가 구조적으로 못 보는 두 형태 — 같은 색끼리의 가림(빗금 위 라벨)과 clip+translate 이음매의 글자 복제. 확대 스틸 + 한 행 픽셀 샘플로만 잡힌다
metadata:
  type: feedback
---

`check.mjs` 의 팔레트·가장자리 잘림·정적 구간은 **색과 면적**만 본다. 아래 두 형태는 그물을
그대로 통과하는데 화면에서는 바로 보인다. **확대 스틸을 뽑아라**(→ [[frame-capture-for-review]]).

## ① 같은 색끼리의 가림 — 강조색 빗금 **위에** 강조색 라벨

`hatch(...)` 로 도형 속을 채운 **뒤** 같은 `tone('accent')` 로 글자를 그리면 글자가 빗금에 잠긴다.
두 색이 **같으므로 혼합 픽셀이 0개**다 — 흰색×강조색 겹침을 세는 기존 방법
(→ [[review-method-occlusion-measure]])이 **아무것도 못 잡는다.**

**Why:** ep11s 반려. `chase.js` 의 `REWARD`(0~9.6초 내내) 와 `settle.js` 위 레인 `DEBT`(페이오프
물체)가 둘 다 안 읽혔다. 🔑 **같은 회차의 `ledger.js:20-21` 에 작성팀이 정답을 주석으로 적어 두었다** —
*"카드 안 빗금은 글자 아래에 깔지 않고 카드 바닥 띠에만 넣었다. 강조색 글자 위에 같은 강조색
빗금을 겹치면 읽히지 않는다."* 그리고 같은 회차의 카드 둘은 실제로 그렇게 그려져 선명했다.

**How to apply:** kind 코드에서 `hatch(` / `fillRect` 로 채운 사각형과 그 뒤의 `fillText` 가
**같은 `tone()`** 이고 좌표가 겹치면 확대 스틸을 뽑는다. 정답 형태가 같은 회차 안에 있는 일이 잦으니
「이 회차의 다른 카드는 어떻게 했나」를 먼저 본다 — 고치라고 요구할 대상이 바로 나온다.

## ② `clip` + `translate` 이음매 — 벌어지는 게 아니라 **한 조각이 복제된다**

「판이 좌우로 갈라진다」를 클립 둘 + 반대 `translate` 로 그리는 관용구가 있다.
클립 경계를 `[0, X)` / `[X, w)` 로 **맞닿게** 잡으면 **틈이 안 생긴다** — 원본
`x ∈ [X−off, X+off)` 인 **2×off 폭 한 조각이 양쪽에서 각각 그려져 두 번 나온다.**

**Why:** ep11s `premise.js`. `off` 최대 10px, 인용문이 `fit()` 로 21px 에 확정 → 한글 한 글자 폭이
정확히 20px 이라 **글자 하나가 통째로 복제**됐다. 화면이 「쫓아가서 **서** 건네준다」로 읽혔다.
게이트는 팔레트도 잘림도 통과. 🔴 그리고 **파생 결함이 따라온다** — 코드가 「검은 틈 안에서만
균열을 그린다」고 믿고 `crackA = clamp((off−3)/3.5)` 로 열지만 **틈이 없으므로 균열이 흰 글자 위에
직접 그어진다.** 강조색이 불투명하게 덮어 혼합 픽셀은 0개다.

**How to apply:**
- 코드 신호 = `ctx.clip()` 두 번 + `ctx.translate(±off, 0)` + 같은 draw 함수 두 번 호출.
  경계값과 두 클립 범위를 적어 놓고 **겹치는 원본 구간이 있는지 산수로 먼저 본다.**
- 픽셀 증거 = **판 테두리 한 행을 샘플한다.** 틈이 진짜면 그 행에 검정(α=0 또는 R=G=B=0) 구간이
  `2×off` 폭으로 나와야 한다. ep11s 는 그 행이 흰색 255 로 연속이었고 끊긴 3px 은 균열 자신이었다.
  ```js
  const d = ctx.getImageData(0, Math.round(SLAB_Y*dpr), cv.width, 1).data;  // R 채널만 훑는다
  ```
  🔑 `.vis` 캔버스는 여러 개고 **보이지 않는 것의 `getBoundingClientRect().width` 가 0** 이라
  `dpr = cv.width / rect.width` 가 `Infinity` 가 된다. `rect.width > 10` 인 것만 고를 것.
- 🔑 **글자를 지는 그림이면 반려로 올린다.** 특히 작성팀이 「자막에서 뺀 말을 화면이 진다」를
  근거로 자막을 줄였다면, 그 글자가 깨지는 순간 **줄이기의 근거 자체가 무너진다.**

관련: [[frame-capture-for-review]] · [[review-method-occlusion-measure]] · [[project-scene-video-review-log]]
