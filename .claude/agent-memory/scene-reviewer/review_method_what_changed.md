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

[[review-method-recycling-check]] [[project-review-log]]
