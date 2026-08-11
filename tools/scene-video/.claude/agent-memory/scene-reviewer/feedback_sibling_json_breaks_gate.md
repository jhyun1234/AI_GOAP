---
name: sibling-scene-json-breaks-check
description: check.mjs 의 「형제 라벨」 게이트가 형제 scene.json 을 직접 파싱하므로, 형제 한 편의 JSON 문법 오류가 분할 회차 전부의 check 실행을 막는다. 원인 1순위는 notes 안 인용부호 미이스케이프
metadata:
  type: feedback
---

**분할 회차에서 `node check.mjs <ep>` 가 `SyntaxError: … JSON at position N` 으로 죽으면,
그 회차 파일이 아니라 **형제 편 `scene.json`** 을 먼저 의심하라.**

**Why:** `check.mjs:63` 근처의 「형제 라벨 = 그 편의 `youtube.title`」 게이트가
`outro.siblings` 의 편 번호(`16-5편`)를 `ep16s-5` 로 바꿔 **그 편의 `scene.json` 을 직접
`JSON.parse` 한다.** 그래서 **형제 다섯 중 하나만 깨져도 다섯 편 전부** 게이트를 못 돈다.
실측(2026-08-11): `ep16s-4` 2차 검수 때 `ep16s-5/scene.json` 의 `notes.정적 구간 대책` 안
`검수 총평이 *"작성팀이 …"* 였다` 의 **`"` 가 이스케이프 없이** 들어가 JSON 문자열이 조기 종료됐고,
`check.mjs ep16s-4` 가 크래시했다. 스택 트레이스는 **깨진 형제 편 이름을 안 찍고** 그 편 `notes`
본문만 토해내서, 트레이스만 보면 어느 파일인지 헷갈린다.

🔑 **원인 1순위는 `notes` 다.** 검수 판정서 문장을 `notes` 에 인용해 넣을 때 `*"…"*` 형태가
그대로 들어간다 — 리포트를 인용하는 습관이 생긴 뒤 나타난 결함이다.

**How to apply:**
1. 범인 특정 한 줄:
   ```
   node -e "const fs=require('fs');for(const e of fs.readdirSync('episodes').filter(x=>/^ep16s-/.test(x))){
    try{JSON.parse(fs.readFileSync('episodes/'+e+'/scene.json','utf8'));console.log(e,'OK')}
    catch(err){console.log(e,'❌',err.message)}}"
   ```
   그다음 `position N` 앞 180자를 `JSON.stringify(s.slice(p-180,p+40))` 로 떠서 눈으로 본다.
2. 🔴 **형제 파일을 내가 고치지 마라** — 다른 편이 동시에 편집 중일 수 있다(lost update).
   **렌더 차단 항목으로 코디네이터에게 넘기고**, 그동안 막힌 게이트는 **손으로 재현**한다.
3. 손 재현법(형제 라벨 게이트) — `sibLabel()` 을 그대로 옮기고, 깨진 편의 `title` 만 관용 추출:
   ```js
   const m = /"title"\s*:\s*"((?:[^"\]|\.)*)"/.exec(raw.slice(raw.indexOf('"youtube"')));
   const title = JSON.parse('"' + m[1] + '"');
   const label = /^(.*) · 유니티 GOAP 개발일지 (\d+-\d+편) #shorts$/.exec(title);  // → `${label[2]} · ${label[1]}`
   ```
4. 나머지 게이트는 그 편 자기 파일만 읽으므로, **발화 필드(`say`·`pauseAfter`·`sfx` 인자)가
   무변경임을 `timed.json` 대조로 실증**하면 직전 실행 결과를 그대로 인용해도 된다.
   🔴 **그래도 형제를 고친 뒤 한 번 더 돌리라고 리포트에 못 박아라** — 기계 출력 없이 렌더 금지.

관련: [[recurring-script-defects]] · [[self-report-is-not-evidence]]
