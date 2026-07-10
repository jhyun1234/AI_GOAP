---
name: feedback-persist-approved-snapshots
description: Step 4/Step 6 승인 시 승인한 원문(intro, html_content 등)을 반드시 파일로 스냅샷 저장할 것 — 재검수 때 대조할 아티팩트가 없으면 fail-closed로 반려해야 했던 사건 (2026-07-09)
metadata:
  type: feedback
---

## 무슨 일이 있었나

GatherIron 소재 블로그 글(방향 ③, 5커밋)의 Step 6 2차 재검수에서, 오케스트레이터가
"이전 REJECTED 지적(인트로 누락) 이후 편집팀이 인트로 2문단만 추가했고 나머지는
무변경"이라고 서술했다. 이걸 확인해달라는 요청을 받았지만, `.claude/agent-memory/
blog-master/`(이 폴더 자체)가 완전히 비어 있었고, blog-writer/editor/reviewer/publisher
메모리와 devlog/, Docs/ 어디에도 Step 4에서 승인됐던 인트로 원문이나 Step 6 1차에서
"인트로만 빠졌다"고 지적했던 html_content 사본이 남아있지 않았다. 결과적으로 오케스트
레이터의 서술 외에는 대조할 아티팩트가 전혀 없어, 기술적 사실 검증(git show로 5개 커밋
전부 확인, 전부 일치)은 통과했음에도 "무변경" 핵심 주장 자체는 검증 불가로 REJECTED
처리했다.

**Why:** 이 파이프라인은 서브에이전트 세션이 끊기면 이전 대화를 기억 못 하는 구조적
결함이 있고(편집팀이 그래서 원본을 잃고 본문을 통째로 재구성한 사건이 바로 이 반려의
직접 원인), 마스터 자신도 승인 스냅샷을 남기지 않으면 다음 재검수 라운드에서 "서술만
믿고 승인"하는 러버스탬핑을 강요당하는 구조가 된다. fail-closed 원칙(근거를 스스로
채우지 못하면 반려)을 지키려면 애초에 근거가 만들어지도록 승인 시점에 저장해둬야 한다.

**How to apply:**
1. Step 4에서 APPROVED를 낼 때, 승인한 초안 텍스트(최소 인트로 등 핵심 문단, 가능하면
   전문)를 `.claude/agent-memory/blog-master/snapshots/step4_<날짜>_<소재요약>.md`
   같은 파일로 저장한다.
2. Step 6에서 APPROVED를 낼 때도 최종 title/meta_description/labels/html_content
   전문을 `.claude/agent-memory/blog-master/snapshots/step6_<날짜>_<소재요약>.md`로
   저장한다 (게시팀 위임 직전).
3. 재검수 요청이 "이전 버전 대비 이 부분만 바뀌었다"는 형태로 들어오면, 서술을 믿지 말고
   위 스냅샷 파일과 실제로 `diff`를 떠서 육안 확인한다. 스냅샷이 없으면 그 자체가 반려
   사유다 — "아티팩트 없이는 검증 불가 → REJECTED"를 판정 사유에 명시한다.
4. 반려된 사이클이 재시도되어 다시 내 앞에 올 때, 이번 메모리를 먼저 확인해서 스냅샷
   디렉토리가 쌓이고 있는지 점검한다.
