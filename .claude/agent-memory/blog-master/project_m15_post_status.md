---
name: project-m15-post-status
description: M15 "연대기 아카이브" 블로그 글(2026-08-08 로컬 수동 사이클) 파이프라인 상태 — Step 4·6 모두 APPROVED(반려 0), 게시팀 위임까지 완료. 스냅샷 경로·blob 해시
metadata:
  type: project
---

소재: M15 "연대기 아카이브" (`1580dcf`~`ece7029`, 2026-07-31 세션). 2026-08-08 로컬 수동 사이클.

- **`verify_at` = `ece7029`** — 이 글의 모든 화면 문자열·수치는 이 시점 기준이지 HEAD 기준이
  아니다. HEAD에는 M17~M21이 들어와 있다.
- Step 4: **APPROVED (1회, 반려 0)**. 판정서 = `tools/blog-automation/.staging/04_verdict.md`
- **Step 4 승인 스냅샷** = `.claude/agent-memory/blog-master/snapshots/step4_2026-08-08_m15-chronicle-archive.md`
  (blob `7152762ca7967aee303d2148de68f162a968cd18`, 11,670자). **Step 6 대조는 이 스냅샷 기준**
  — `.staging/02_draft.md`가 아니다 ([[feedback-persist-approved-snapshots]]).
- Step 6: **APPROVED (1회, 반려 0)**. 판정서 = `.staging/06_verdict.md`(직전 회차 잔재를 덮어씀),
  최종본 스냅샷 = `snapshots/step6_2026-08-08_m15-chronicle-archive.md`
  (blob `6213a2abb32f64cc3255b4d84098ce6109b70301`, 13,427자). 게시팀에 **공개 발행** 위임
  (blogId `6014451945015572125` — 지시받은 값을 믿지 않고 `credentials/blog_config.json:2`에서
  직접 읽어 대조). Step 4→6 기계 diff 결과 **차이는 H1→`title` 이동 1건뿐, 본문 82줄 문자 일치**.
- 🔑 **Step 4에서 내가 쓴 주의 문구가 부정확했던 사례 1건** — "`{N}`을 코드 하이라이트로 감싸지
  말 것"이라고 적었으나 승인 스냅샷 80~81행이 **이미 백틱 인라인 코드**였다. 편집팀의 `<code>`
  변환은 승인 서식의 표준 변환이라 위반이 아니었다. 다음에 편집팀 주의사항을 쓸 때는
  **스냅샷의 실제 마크업을 먼저 보고** 금지어를 적는다 — 안 그러면 내가 만든 지시가 정확한
  결과물을 반려하게 만든다([[feedback-correction-value-sourcing]]의 같은 실패 유형).

**Why:** Step 6은 별도 대화에서 실행되므로 스냅샷 경로와 blob 해시가 여기 없으면
"승인분 대비 무엇이 달라졌는가"를 기계적으로 diff할 수 없다.

**How to apply:**
- Step 6에서 편집팀 HTML을 받으면 위 스냅샷과 먼저 diff한다. 특히 화면 문자열 6종
  (`SeasonHud.cs:700/703/788/544/712/741` @ `ece7029`)과 수치 2종(불투명도 `0.85` = `:809`,
  `44f` = `:652`)이 포맷팅 중 변형됐는지 본다. `▶`(U+25B6)·`†`(U+2020)·가운뎃점 `·`는
  코드 리터럴 그대로여야 한다.
- 초안 80~81행 인용 블록의 `{N}`·`{종료일}`은 C# 문자열 보간 자리다. 편집팀이 실제 숫자로
  치환했으면 그건 의미 변경이다.
- 사후에 "값이 틀렸다"는 지적이 들어오면 HEAD가 아니라 `git show ece7029:<경로>`로 대조.
- 곁가지(미처리, 보고만 함): `Docs/M15_연대기_실행명세서.md:33`이 설계를 바꾼 전제를
  "②·③"으로 적었으나 같은 파일 §1 표·세션 로그 520~522행·`1580dcf` 커밋 본문은 모두 ①·②다.
  명세서 헤더 쪽이 오기 → [[review-method-source-doc-conflict]]
