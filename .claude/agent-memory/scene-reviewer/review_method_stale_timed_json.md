---
name: review-method-stale-timed-json
description: check.mjs 를 돌리기 전에 build/timed.json 의 샷 수를 씬의 샷 수와 대조하라 — 옛 더미 타임라인이 길이·말속도·자막2초 게이트를 대신 통과시킨다
metadata:
  type: feedback
---

`check.mjs` 를 돌린 뒤 **가장 먼저 「총 길이」 줄의 꼬리 값을 본다.**
꼬리 = `0.35 × timed.json 의 샷 수`이므로, 씬의 샷 수와 안 맞으면 **다른 회차/옛 더미의
타임라인으로 판정되고 있는 것**이다.

**Why:** lf01(2026-08-09) 첫 실행이 34샷 126줄짜리 회차를
`총 길이 10.6초 = 자막 9.6 + 꼬리 1.05` 로 **통과**시켰다. 꼬리 1.05 = 0.35×3 —
`episodes/lf01/build/timed.json` 이 W1 가로 프로필 시험용 **3샷 더미**
(`가로 프로필 시험, 첫 샷입니다.`)였다. 그 파일 때문에
**`자막 2초 미만 없음`·`말 속도`·`총 길이` 세 항목이 대본이 아니라 더미 3줄로 판정**됐고,
`실측 타임라인 존재`(red)와 `산정 길이 (참고)` 줄은 아예 안 찍혔다.
지시서가 경고한 「지어낸 값으로 길이 게이트 통과」가 **합성이 아니라 잔여물로** 일어난다.

**How to apply:**

- 검사: `node -e "const t=require('./episodes/<ep>/build/timed.json');console.log(t.shots.length)"`
  vs `scene.json` 의 `shots.length`. 다르면 오염이다.
- 조치: `build/` 를 **스크래치패드로 옮긴다**(삭제 아님 — mp4·wav 가 같이 들어 있다).
  옮긴 뒤 check 를 다시 돌리고, **옮겼다는 사실과 복구 경로를 리포트에 적는다.**
  TTS 를 새로 돌리기 전에는 되돌리지 말 것.
- 🔴 길이를 재려고 합성 `timed.json` 을 만들지 않는다. `timed.json` 이 없을 때 찍히는
  「산정 길이 (참고 · 판정 아님)」 줄이 정본이고, `notes.길이` 는 그 줄을 **인용**해야 한다
  (자체 계산식을 적었으면 정정 요구).
- 스틸 확인용으로 `render.mjs --still` 을 돌리면 `build/stills/*.png` 가 생긴다 —
  그건 `timed.json` 을 만들지 않으므로 안전하고, 리포트의 증거로 인용한다.

관련: [[review-method-wide-longform]] · [[feedback-no-length-rejection]]
