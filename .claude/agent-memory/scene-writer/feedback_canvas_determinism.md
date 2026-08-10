---
name: feedback-canvas-determinism
description: draw 가 순수해도 래스터화 경로가 순수하지 않을 수 있다 — 긴 축정렬 setLineDash 가 30fps 3패스에서 갈렸다. 「난수를 안 썼다」는 필요조건일 뿐이고, check.mjs 는 기본 FPS 5 라 30 으로 따로 돌려야 보인다
metadata:
  type: feedback
---

**긴 가로 점선을 `setLineDash` 로 그리지 마라. 조각을 `fillRect` 로 직접 채워라.**
그리고 `lineDashOffset` 은 `((v % P) + P) % P` 로 **양수 정규화**해라(JS 의 `%` 는 음수를 남긴다).

**Why:** ep13s-1 이 이걸로 반려됐다(2026-08-10). `draw` 는 완전히 순수했다 —
`Math.random()`·`Date.now()`·이전 프레임 기억이 하나도 없고 흔들림은 전부 `Math.sin(t·…)` 였다.
그런데 **30fps 결정성 3패스에서 44/1062 프레임이 갈렸다.** 어긋난 2,653px 이 **전부
`chase.js` 의 가로 점선 세 줄**(`tone('track')` 알파 **21↔19** = 안티에일리어싱)이었고
다른 요소는 한 픽셀도 안 어긋났다. 대조군 네 편(ep12s-1·ep12s-3·ep13s-2·ep13s-3)은 전부 0.
🔑 그 파일만 `dashRule` 을 **세 번** 불렀다(나머지 넷은 두 번). 긴 축정렬 점선은 Skia 의
dash 경로 효과를 타고, 그 경로가 패스 순서에 따라 다르게 래스터화됐다.

**How to apply:**
- 🔴 **「난수·시계·상태를 안 썼다」와 「결정적이다」는 다른 주장이다.** 노트에
  *"같은 `t` 면 같은 그림이다"* 라고 단언했다가 실측에 반증됐다. **앞의 것까지만 적어라.**
- 대체 구현은 짧다(모양이 동일하고 좌표 감사도 안 바뀐다):
  ```js
  const P = 14, DASH = 7;
  const off = (((t * speed) % P) + P) % P;
  ctx.fillStyle = tone('track');
  for (let x = x0 - P + off; x < x1; x += P) {
    const a = Math.max(x0, x), b = Math.min(x1, x + DASH);
    if (b > a) ctx.fillRect(a, y - 1.5, b - a, 3);   // lineWidth 3 과 같다
  }
  ```
- 🟢 **작은 도형 위의 점선(24px 둥근 사각 · 원호 · 짧은 폴리라인)은 통과했다.** 전부 갈아
  치우지 말고 **긴 축정렬 직선만** 바꿔라. 남은 것들은 위상만 양수 정규화하면 된다
  (mod P 로 같은 값이라 **그림이 한 픽셀도 안 바뀐다**).
- 🔴 **`check.mjs` 는 기본 FPS 5 라 이걸 못 잡는다.** `render.mjs` 의 실제 FPS 는 **30** 이고
  30 판독이 정본에 가깝다(mp4 의 모든 프레임을 본다). 제출 전에
  `node tools/scene-video/check.mjs <ep> --fps 30` 을 **따로 돌려라.**
  ⚠️ 단 「정적 구간」만은 fps 5 값으로 판정한다(문턱이 fps 5 에서 잡힌 상수다).

관련: [[feedback-static-segment-is-area]] · [[feedback-canvas-edge-text]]
