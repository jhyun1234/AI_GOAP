---
name: review-method-loop-events-and-path-math
description: t-루프 사건은 「몇 번 도는가」가 아니라 「어느 자막 위에 앉는가」로 세고, 둘레·경로 좌표 함수는 시뮬레이션으로 범위를 재라 (ep15s-1 반려 2건의 방법)
metadata:
  type: feedback
---

`cue`/`since` 밖에서 도는 것 둘은 **각각 다른 방법으로** 재야 잡힌다. 둘 다 `check.mjs`
전 항목이 통과한 상태에서 나왔다(ep15s-1 1차 반려 두 건).

**Why:** ①「루프가 몇 바퀴 돈다」는 작성팀 서술은 **횟수만 세고 위치를 안 센다** — ep15s-1 은
「온전한 등장이 두 번, 놓쳐도 다시 온다」고 이점으로 적었는데 그 둘 중 하나가 **정반대의 말을
하는 자막 위**에 있었다. ②경로 좌표 함수(둘레 걷기·궤적)는 **눈으로 읽으면 맞아 보인다** —
ep15s-1 의 `perim()` 은 네 번째 변 앞에서 `p -= wd` 대신 `p -= ht` 를 써서 마디를 칸 밖으로
11.5px 내보냈고, 팔레트·겹침·가장자리 게이트가 전부 통과시켰다.

**How to apply:**

**① t-루프 사건 — 「전 프레임 열거 후 자막 라벨 붙이기」**
`frac(t/N)` 위에 사건이 있으면(갈라짐·깜빡임·표식 등장) 샷 전 구간을 30fps 로 돌면서
**알파/진행도와 함께 「그 시각이 몇 번 자막인가」를 같이 찍는다.**

```js
for (let ts = 0; ts <= shotDur; ts += 1/30) {
  const u = frac(ts / loopSec);
  const a = enter(ts) * st(ts) * fan(u);
  if (a > 0.05) console.log(ts, a, 'line', ts < rel[1].t ? 0 : 1);
}
```
- 🔴 **다른 자막 위에 앉은 발생이 하나라도 있으면 반려**다. 특히 그 자막이 반대말을 하면
  (「똑같다」 위에서 갈라짐) 그것만으로 결론이 난다.
- 말 축도 같이 본다 — 라벨이 부르는 낱말의 **발화 개시 = 자막 시작 + wav 선행 무음**
  (실측 0.29~0.51초, `build/audio/*.wav` 를 `|v|>0.02` 로 스캔). 한도 ±20프레임(0.67초).
- 처방은 **`t` → `since(그 자막)`** 로 옮기는 것이고, 길이·TTS·`delayMs` 를 안 건드린다.

**② 경로 좌표 함수 — 「범위를 시뮬레이션해서 상자와 비교」**
`perim`·`lerp` 사슬·수동 다각형 순회를 보면 **읽지 말고 돌려라.**

```js
let minY=1e9,maxY=-1e9,minX=1e9,maxX=-1e9;
for (let p=0; p<P; p+=0.5) { const [x,y]=perim(0,0,wd,ht,p); /* min/max 갱신 */ }
// 상자(0..wd / 0..ht)와 대조 — 벗어나면 그 자체가 결함
```
- 벗어난 값이 나오면 **이웃 요소까지 계산한다**: 이탈량 + 마디 반크기 + 숨 + 글로우 blur 를
  더해 격자 간격(`GX`·`GY`)과 비교. ep15s-1 은 14.3px 이탈 vs 세로 간격 16px = 여백 1.7px 이라
  **흰 칸에 강조색 글로우가 닿았다** — 색 규약 주석의 「9px 여유」가 거짓이 된다.
- 🔑 확인 사살은 **확대 스틸**이다. `render.mjs --still <abs>` → `ffmpeg -vf crop=…,scale=…:flags=neighbor`
  로 2배 확대(리포에 PIL 없음). 전체 스틸에서는 「칸이 좀 이상하다」로만 보이고 확대해야 원인이 보인다.

🔑 두 검사 다 **작성팀 노트가 「했다」고 적은 자리**에서 나왔다 — 노트가 센 것과 내가 세야 할
것이 다르다는 신호를 [[review_method_self_report_is_not_evidence]] 와 같이 볼 것.
관련 = [[feedback_review_blind_spots]] · [[review_method_occlusion_measure]] ·
[[review_method_vanishing_label_alpha]].
