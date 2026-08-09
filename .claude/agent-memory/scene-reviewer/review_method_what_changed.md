---
name: review-method-what-changed
description: 재검수에서 "무엇이 바뀌었나"는 git diff 로 못 잰다 — 회차 전체가 미커밋이라 파일 수정 시각으로 가른다
metadata:
  type: feedback
---

2차 이후 검수에서 **"작성팀이 정말 그 파일 하나만 고쳤는가"** 는 `git diff` 로 답할 수 없다.
`.claude/agent-memory/scene-reviewer/project_review_log.md` 의 회차들은 **작업 내내 미커밋**이라
diff 가 HEAD 기준이고, 1차에서 이미 바뀐 파일이 2차에도 똑같이 "M" 으로 남는다.

**대신 파일 수정 시각(mtime)을 내 앞선 보고서의 mtime 과 비교한다.**
내 보고서보다 **뒤**인 파일만이 이번 패스에서 바뀐 것이다.

```
node -e "const fs=require('fs'),p=require('path');const d='<episode dir>';
for(const s of ['kinds','notes','']){const q=s?p.join(d,s):d;
for(const f of fs.readdirSync(q)){const fp=p.join(q,f),st=fs.statSync(fp);
if(st.isFile())console.log(st.mtime.toISOString(),(s?s+'/':'')+f);}}"
```

**Why:** ep02s 2차(2026-07-30). 작성팀이 "`tripwire.js` 하나만 고쳤고 `scene.json` 은
무변경"이라고 보고했는데 `git diff --stat` 에는 `scene.json` 이 151줄 변경으로 떴다.
그건 **1차 재작업분**이었다. mtime 이 `scene.json 01:14:49` < `내 1차 보고서 01:31:23` <
`tripwire.js 01:42:26` 이라 무변경이 확정됐다.

**How to apply:**

- 2차 이후 패스는 **가장 먼저** 이걸 돌린다. 무엇을 재검증하고 무엇을 인용할지가 여기서 갈린다.
- mtime 만으로 못 미더우면 내용 지표로 교차 확인한다(샷 수 · `lines` 총합 · `excluded` 개수 ·
  고친 kind 를 쓰는 샷 목록). ep02s 는 12샷 · 28줄 · 7건 · `tripwire`=S8 단독으로 재확인했다.
- 고친 kind 를 **쓰는 샷이 하나뿐인지** 반드시 확인한다. 여러 샷이 공유하면 파급이 있다.

### 🔑 세 번째 증거 — check 출력의 미세 차이를 「그 한 줄」로 설명해 보라

mtime 은 *어느 파일*이 바뀌었는지만 말한다. **파일 안에서 한 줄만 바뀌었는지**는
`check.mjs` 출력의 앞 패스 대비 델타로 검산할 수 있다. 산수가 딱 맞으면 다른 자막을
건드리지 않았다는 강한 증거다(맞지 않으면 숨은 변경이 있다).

- 「산정 길이」는 **구두점·공백을 뺀** 글자 수 기반(CPS_REF 6.7)이라, 그 수가 같으면
  소수점까지 동일하게 나온다.
- 「정적 구간」·「결정성 프레임」은 임시 타이밍 기반이고 그건 **`say` 원문 길이**를 쓴다
  (`engine/engine.js` `PROVISIONAL_CPS = 5.6`, `max(1700, (say.length/5.6 + LINE_TAIL)*1000)`).
  즉 **구두점을 포함한** 글자 수다.
- 실측(lf01 3차): 자막 한 줄이 구두점 제외 17 → 17자(변화 0), `say` 원문 25 → 23자.
  결과가 산정 길이 485.0 → **485.0 그대로**, S23 정적 7.0 → **6.6**(= 2 ÷ 5.6 = 0.357초),
  결정성 4197 → **4196프레임**. 세 값이 전부 그 한 줄로 설명돼 무변경이 확정됐다.

[[review-method-recycling-check]] [[project-review-log]] [[review-method-stale-timed-json]]
