---
name: blog-last-published-commit
description: 블로그 파이프라인이 마지막으로 소재로 사용한 최신 커밋 해시 및 게시 상태 — 기획팀의 중복 소재 방지용
metadata: 
  node_type: memory
  type: reference
  originSessionId: 67127d74-590e-4800-92a2-33f811dd3e8e
---

# 블로그 파이프라인 최근 사용 커밋

**Why:** 기획팀이 다음 사이클에서 이미 다룬 커밋을 다시 소재로 고르지 않도록, 가장 최근에 파이프라인이 "사용 완료"로 처리한 커밋 해시를 기록해 둔다.

**How to apply:** 기획팀은 새 사이클 시작 시 이 파일의 `latest_commit`을 확인하고, 그 이전(포함) 커밋들은 이미 소재로 소비된 것으로 간주한다. 단, 아래 상태가 `DRAFT`인 경우 실제 공개 게시는 아니었으므로, 같은 소재를 이어서 다룰지/새 소재로 넘어갈지는 그때 기획팀이 판단한다.

## 최신 사용 커밋 (2026-08-10, M19 화폐 전면 철거 — 🔴 2026-08-12 사전점검이 소급 기입)

🔴 **이 블록은 파이프라인이 스스로 쓰지 못한 것을 사후 복원한 것이다.** 2026-08-10
원격 run 이 M19 소재로 **Blogger 정식 공개 발행까지 성공했으나 `chore(blog)` 상태 커밋을
남기지 못한 채 종료**했다. 그래서 08-11까지 이 파일의 최신 항목은 M15(`ece7029`)로 멈춰
있었고, `blog_next_material_priority.md`도 M19를 `ACTIVE`(미소비)로 잘못 유지하고 있었다.
2026-08-12 사전점검이 **라이브 블로그를 직접 열어** 발행 사실을 실측 확인하고 복원했다.

- `latest_commit`: `c4c3431` (전체 해시 `c4c34310`, 2026-08-05 09:13, `docs(goap): M19
  P1 판정 기입 — 사용자 Play 확인(HUD 소멸·집 사슬), 밀스톤 종료` — 2026-08-08 기획팀
  브리프(`state/incidents/2026-08-08/01_planner_brief.md`)의 `verify_at`과 동일하며,
  08-09~08-11 세 차례 사전점검이 일관되게 이 회차의 소재 소비 커밋으로 지목해 온 값이다.)
- `selected_commits_range`: M19 "화폐 전면 철거와 실물 마을"(`a9d8337`~`c4c3431`,
  45파일 −2,260줄 — 지갑·세금·물가·발행 전부 게임에서 제거). M16~M18(화폐 축)은 별도
  회차가 아니라 **이 글의 배경으로 압축 서술**됐다 — 08-08 기획팀이 브레인스토밍
  게이트에서 내린 판정이며 그 판정대로 발행됐다.
- `cycle_date`: 2026-08-10 (원격 auto-run)
- `publish_status`: PUBLISHED (라이브 실측 확인 2026-08-12 — 블로그 아카이브에 2026-08-10
  자로 게시돼 있음)
- `blogger_post_id`: **미상** — 발행 응답 로그가 상태 커밋과 함께 유실됐다. 필요 시
  Blogger 관리 화면에서 조회할 것.
- `blog_url`: https://gamedevclaude.blogspot.com/2026/08/45-2260-unity-goap-claude-code-ai.html
- `title`: 완성한 날 전부 지웠습니다: 마을 화폐 경제 45개 파일 2,260줄 철거기 —
  Unity GOAP 인디게임 개발일지 (Claude Code 게임 개발 AI 페어 프로그래밍 후기)
- `content_note`: ⚠️ **이 회차는 승인 경로·반려 횟수·`local_archive` 사본이 전부
  확인되지 않는다** — `.staging/` 산출물과 실행 로그가 상태 커밋 유실과 함께 사라졌기
  때문이다. 발행물 자체는 라이브에 정상 존재하며 본문 도입부가 M19 서사(45파일 2,260줄
  철거·GOAP 마을·재정 정책이 개입 수단이었다는 진단)와 일치함을 실측 확인했다.
  🔴 **재발 방지: 게시팀 Step 8은 상태 커밋을 `git push` 성공까지 확인해야 한다.**

## 이전 사용 커밋 (2026-08-08, M15 연대기)

- `latest_commit`: `ece7029` (전체 해시 확인 안 됨 — 짧은 해시는 기획팀 브리프
  (`.staging/01_planner_brief.md`)·마스터 Step 6 판정서(`06_verdict.md`) `verify_at`과
  동일, `docs(devlog): 2026-07-31 M15 세션 — 브레인스토밍→명세→W1~W3→Play 검증 완주 기록`
  (21:09) — commit_refs 중 타임스탬프 기준 가장 최신 커밋이며 `verify_at`과도 일치해
  별도 치환이 필요 없었다.)
- `selected_commits_range`: `1580dcf`(spec(chronicle) M15 연대기 실행명세서) ~ `ece7029`
  (docs(devlog) 2026-07-31 M15 세션) — M15 "연대기" 전 항목(W1~W3) 완결 + 결함 수정 +
  세션 로그까지 6커밋: `1580dcf`(spec(chronicle) M15 연대기 실행명세서 — 판별 아카이브·
  C 토글 패널·쓰기 2곳, 16:52) · `b2b1a56`(feat(chronicle) M15-W1 ChronicleArchive —
  판별 연대기 저장소 + 게이트 T1~T3, 17:31) · `68a3f49`(feat(chronicle) M15-W2 아카이브
  배선 — 겨울 전환·전멸 래치 2곳 스냅샷, 17:32) · `9a8bdc9`(feat(chronicle) M15-W3
  연대기 패널 — C 토글 + 판 목록/상세 + 게이트 T4, 17:37) · `3b6cfeb`(fix(chronicle)
  연대기 패널 결함 2건 — 현재 판 중복 행·전멸 화면 겹침, 20:52) · `ece7029`(docs(devlog)
  2026-07-31 M15 세션 — 브레인스토밍→명세→W1~W3→Play 검증 완주 기록, 21:09,
  **latest_commit**). 기획팀 브리프(`.staging/01_planner_brief.md`) commit_refs 전체 반영.
- `cycle_date`: 2026-08-08 (로컬 수동 사이클 — 마스터 Step 6 2차 APPROVED, 반려 0회로
  Step 3/4/6 정상 승인 경로 완주. 게시팀 Step 7이 `.staging/05_final.md`의
  title/meta_description/labels/html_content를 수정 없이 그대로 정식 공개 발행.)
- `publish_status`: PUBLISHED (Blogger published: 2026-08-08T00:19:34-07:00, status: LIVE)
- `blogger_post_id`: 1028397470425467506
- `blog_url`: https://gamedevclaude.blogspot.com/2026/08/unity-goap-2-claude-code-ai.html
- `title`: Unity GOAP 연대기 아카이브 인디게임 개발일지: "통과"라고 적어둔 화면을 다시
  봤더니 결함이 2건 숨어 있었다 (Claude Code 게임 개발 AI 페어 프로그래밍 후기)
- `labels`: Unity GOAP 연대기 아카이브, Claude Code 게임 개발, AI 페어 프로그래밍 후기,
  인디게임 개발일지
- `local_archive`: tools/blog-automation/published/2026-08-08-unity-goap-m15-chronicle-archive.html
  (게시 직후 Blogger POST 응답 JSON의 `content` 필드를 게시 직전 임시 파일
  `_tmp_content.html`과 Node `===` 문자열 완전 비교 및 md5 대조해 완전 일치함을 직접
  확인(byte-identical, md5 `429ea058f78e263aa3c4d7631d51290a` 동일, 12,876자, U+FFFD
  0건). `title`·`labels`(4개, 집합 일치) 필드도 일치 확인. 화면 문자열 6종(▶·†·중간점·
  em dash·`{N}`·`{종료일}`) 라이브 본문에서 개별 카운트 재확인 — `▶`1·`†`1·`·`15·`—`21·
  `{N}`7·`{종료일}`1로 전부 승인 스냅샷과 동수.)
- `content_note`: 이 회차는 blog-editor(Step 5)·blog-master(Step 6)를 정상 경유했다
  (반려 0회). 마스터 Step 6 2차 판정서(`06_verdict.md`)가 Step 4 승인 스냅샷과
  `05_final.md`를 기계 diff로 대조해 의미 변경이 H1→title 이동 1건뿐임을 확인했고,
  게시팀은 그 승인분을 수정 없이 그대로 게시했다. 게시팀 자체 검증(POST 응답 대조)도
  독립적으로 완전 일치를 확인했다.

## 이전 사용 커밋 (2026-08-06, M14 가을과 겨울)

- `latest_commit`: `c4bc93e` (전체 해시 `c4bc93edfbf5f921de035a45993f0856c52c31bf`,
  2026-07-31 16:07:44 +0900, `docs(devlog): M14 방치 완주 1판 관측 — 죽음의 축 이동,
  치료사 부재 적재` — 기획팀 브리프(`.staging/01_planner_brief.md`)의 `verify_at`이자
  마스터 Step 6 2차 판정서(`06_verdict.md`)가 재확인한 commit_refs 중 타임스탬프 기준
  가장 최신 커밋. M13 회차와 달리 이번엔 `verify_at`과 최고위험 인용(회상 테스트 문장)의
  근거 커밋이 동일해 별도 치환이 필요 없었다.)
- `selected_commits_range`: `9c352cc`(docs(spec) M14 "가을과 겨울" 실행명세서) ~
  `c4bc93e`(docs(devlog) M14 방치 완주 1판 관측) — M14 "가을과 겨울" 전 항목(W1~W4)
  완결 + 세션 로그 + 실전 방치 관측까지 8커밋: `9c352cc`(spec(m14) "가을과 겨울"
  실행명세서 — 계절 대비 축 + 최소 기록, 07-31 12:59) · `92e5479`(feat(goap) M14-W1
  계절 방아쇠 기반 — DaysToFreeze 파생값 + 슬롯 2종 + 게이트) · `2e22378`(feat(goap)
  M14-W2 방아쇠 재배선 — 가을이 기회를 열고 성격이 판정한다) · `fae3c57`(feat(goap)
  M14-W3 개인 성향 지터 — 확정사의 확률화, 개인의 탄생) · `3a6d753`(feat(goap) M14-W4
  최소 기록 — 경주의 자(연차·겨울 결산·역대 최고 파일·겨울 경보)) · `9100281`
  (docs(devlog) 2026-07-31 M14 세션 — 브레인스토밍(북극성 확정)→명세→W1~W4 구현 완주) ·
  `67d57e5`(fix(hud) 좌상단 HUD 수직 스택 리플로우 — M14 검증 Play 중 발견) ·
  `c4bc93e`(docs(devlog) M14 방치 완주 1판 관측 — 죽음의 축 이동, 치료사 부재 적재,
  **latest_commit**). 기획팀 브리프(`.staging/01_planner_brief.md`) commit_refs 전체
  반영 — 같은 구간에 interleave된 `tools/scene-video/` 커밋 4개(`1696186`·`900eeca`·
  `78c3853`·`14ded6c`)는 게임 개발 소재 범위 밖이라 기획팀이 의도적으로 제외했다.
- `cycle_date`: 2026-08-06 (마스터 Step 6 2차 APPROVED, 반려 0회 — 정상 승인 경로 완주.
  게시팀 Step 7이 `.staging/05_final.md`의 title/meta_description/labels/html_content를
  수정 없이 그대로 정식 공개 발행.)
- `publish_status`: PUBLISHED (Blogger published: 2026-08-05T22:19:41-07:00, status: LIVE)
- `blogger_post_id`: 8259513125902654061
- `blog_url`: https://gamedevclaude.blogspot.com/2026/08/unity-goap-claude-code-ai-3-day-44.html
- `title`: Unity GOAP 인디게임 개발일지 (Claude Code 게임 개발 AI 페어 프로그래밍 후기) —
  겨울 3번을 넘기고 Day 44에 쓰러졌다, 계절이 기회를 열고 성격이 판정하게 만들다
- `labels`: Unity GOAP, Claude Code 게임 개발, AI 페어 프로그래밍, 인디게임 개발일지
- `local_archive`: tools/blog-automation/published/2026-08-06-unity-goap-m14-autumn-winter.html
  (게시 직후 Blogger POST 응답 JSON의 `content` 필드를 게시 직전 임시 파일
  `_tmp_content.html`과 파이썬 `==` 문자열 비교 및 md5 대조해 완전 일치함을 직접
  확인(byte-identical, md5 `5b604f852c028b257af6b51777c49119` 동일, 12,343자, U+FFFD
  0건). `title`·`labels`(4개, 집합 일치) 필드도 일치 확인.)
- `content_note`: 이 회차는 blog-editor(Step 5)·blog-master(Step 6)를 정상 경유했다
  (반려 0회). 마스터 Step 6 2차 판정서(`06_verdict.md`)가 Step 4 승인 스냅샷과
  `05_final.md`를 기계 diff로 대조해 의미 변경이 없음을 확인했고(표 설명 문장 1건 지시
  이행, H1→title 이동, 회상 인용 따옴표 제거 등은 전부 무해 판정), 게시팀은 그 승인분을
  수정 없이 그대로 게시했다.

## 이전 사용 커밋 (2026-08-04, M13 사건과 흔적)

- `latest_commit`: `b105360` (전체 해시 `b105360ddc0f0029479cab3657d5029129d0d383`,
  2026-07-30 23:54:24 +0900, `docs(devlog): S9 회상 테스트 결과 — 첫 통과 신호,
  준비된 자의 아사 수수께끼` — 마스터 Step 6 2차 판정서(`06_verdict.md`) handoff_notes
  ①이 명시한 소재 소비 커밋. 기획팀 브리프의 `verify_at`은 `458e882`(M13 결산 커밋)였으나,
  글의 결론을 떠받치는 최고위험 회상 인용의 실제 근거는 `b105360`이며, `458e882`는
  `b105360`의 조상이라 그대로 기록하면 다음 회차 기획팀이 `b105360`을 미발행 소재로
  재선정할 위험이 있어 이 값을 채택했다.)
- `selected_commits_range`: `dc797d0`(docs(devlog) M13 목적 재정의) ~ `b105360`(docs(devlog)
  S9 회상 테스트 결과) — M13 "사건과 흔적" 전 항목(A~D+C3, 6개) 완결 + 결산 + 회상 테스트
  실측까지 20커밋: `dc797d0`(M13 목적 재정의, 07-26) · `a5f362c`(M13 브레인스토밍 —
  GOAP 의도 판정·RimWorld 대조·코드 실사, 07-27) · `d6b63c4`(M13 = '사건과 흔적' 확정 —
  항목 6개·순서, spec-write 실사 1, 07-27) · `d6a84bf`(spec(m13) 사건과 흔적 실행명세서 —
  실사 2~5 완료, 07-27) · `ea042a0`(feat(m13) A 비석 이름) · `2192563`(A 후속 — 비석
  이름표 크기 에셋화) · `9a6acad`(feat(m13) B 상태 알림 줄) · `0b2a8cc`(B 개정 — 굶는
  주민을 이름으로, 달력 식량 요약 삭제) · `8b68ff9`(B 후속 — 굶는 주민 줄 클릭 = 카메라
  점프 + 선택) · `74cc183`(B 후속 — 선택 = 추적 카메라) · `f5c24a6`(B 후속 — 상태줄
  폰트 에셋화 + 확장 규칙 주석) · `d613d0e`(feat(m13) C1 명부+회고) · `e7003b5`(디버그
  전멸 Ctrl+F9) · `9262660`(feat(m13) C2 사건+정보줄) · `92c8d8b`(C2 개정 — 명부는 목차,
  깊이는 클릭) · `3f4906f`(C2 후속 — 완공 사건에 건물명 병기) · `00d743c`(C2 후속 — 압축을
  연속에서 전체 묶음으로) · `4d2c265`(feat(m13) D 문턱·이유) · `9a0a94d`(feat(m13) C3
  관계 보존) · `458e882`(docs(devlog) M13 결산 — 항목 6개 완료·증분 실증·S9 회상 테스트
  인계, 07-30 17:18) · `b105360`(docs(devlog) S9 회상 테스트 결과 — 첫 통과 신호,
  준비된 자의 아사 수수께끼, 07-30 23:54, **latest_commit**). 기획팀 브리프(`.staging/
  01_planner_brief.md`) commit_refs 전체 반영.
- `cycle_date`: 2026-08-04 (마스터 Step 6 2차 APPROVED, 반려 0회 — 정상 승인 경로 완주.
  게시팀 Step 7이 `.staging/05_final.md`의 title/meta_description/labels/html_content를
  수정 없이 그대로 정식 공개 발행.)
- `publish_status`: PUBLISHED (Blogger published: 2026-08-03T22:49:02-07:00, status: LIVE)
- `blogger_post_id`: 2763756011570552738
- `blog_url`: https://gamedevclaude.blogspot.com/2026/08/claude-code-ai-unity-goap.html
- `title`: Claude Code 게임 개발 AI 페어 프로그래밍 후기 — 주민이 죽어도 "그냥 죽었어"로
  끝났다: Unity GOAP 마을에 사건과 흔적을 심은 인디게임 개발일지
- `labels`: Unity GOAP, Claude Code 게임 개발, 인디게임 개발일지, AI 페어 프로그래밍
- `local_archive`: tools/blog-automation/published/2026-08-04-unity-goap-m13-events-and-traces.html
  (게시 직후 Blogger POST 응답 JSON의 `content` 필드를 게시 직전 임시 파일
  `_tmp_content.html`과 문자열 비교(파이썬 `==`) 및 md5 대조해 완전 일치함을 직접
  확인(byte-identical, md5 `cdf6d74339e81ff40364e1ce2982c1b9` 동일, 16,065자, U+FFFD
  0건). `title`·`labels`(4개, 순서만 다름) 필드도 일치 확인.)
- `content_note`: 이 회차는 blog-editor(Step 5)를 정상 경유했다(반려 0회). 마스터 Step 6
  2차 판정서(`06_verdict.md`)가 승인 스냅샷과 `.staging/02_draft.md`를 sha256까지 대조해
  사이클 중 원고 변조가 없음을 확인했고, 편집팀의 실질 변경은 목록 기호(`①②③④`→
  `1)2)3)4)`) 1건 + M12 링크 삽입 1건뿐이었다.

## 이전 사용 커밋 (2026-08-02, M12 성격 축/성향 벡터)
- `latest_commit`: `c1d87e2` (docs(devlog) M12 종료 — S4 15/15 분화, 회상 테스트 실패,
  M13 = 표현층 — 기획팀 브리프(`incidents/2026-08-02/01_planner_brief.md`)의 `verify_at`
  이자 commit_refs 중 가장 최신 커밋)
- `selected_commits_range`: `2e2ce34`(spec(m12) 성격 축 — 성향(Trait) 벡터 4작용형식
  실행명세서) ~ `c1d87e2`(docs(devlog) M12 종료) — M12-A~J 구현 18커밋 전체:
  `2e2ce34`(spec) · `d54b000`(M12-A 성향 스키마 + 유도기) · `c8a023c`(M12-B 성향 ①우선순위
  — goal 30개 중 24개에 기질 가중치) · `976fae8`(fix 굶주린 주민이 늑대 앞에 굳어 서서
  물리던 결함) · `4e3090a`(ADR-M12-4 전면 개정 — 굶주림 앞에 성격 없음 3조항) ·
  `2183311`(ADR 전수 감사 재조치 — 번호 단일화 + 자격조건화) · `14ba4b8`(M12-C ②비용) ·
  `1312806`(M12-D ③문턱) · `4b83d78`(M12-E ④대상) · `48f8a95`(M12-F 6성격 벡터 이식 —
  여기서부터 행동이 갈린다) · `3129670`(fix 겨울 조리 goal 탐색 폭발) · `c4d1566`(fix
  서비스 직업 외딴 거주로 부탁 영구 단절) · `fbe6dc1`(M12-J 성격별 행동 계측) ·
  `2c940b1`(fix 모닥불 소유 누락 무한 건축) · `789e577`(fix 프로파일러 생존 0/0 + 자존
  미노출) · `30ea98c`(M12-G 집 동기) · `80cfe22`(M12-H 성향→직업 배정 편향) ·
  `eed392b`(scene 관측 표본 확대 — 주민 8명)
- `cycle_date`: 2026-08-02 (⚠️ **정상 승인 경로 아님** — 원격 auto-run이 반려 3회에
  도달해 DRAFTED로 종료했고, 같은 날 로컬 세션이 마스터 2차 판정서의 교정 지시를
  이행한 뒤 수동 발행했다. 상세 경위는 `blog_pipeline_alerts.md` 2026-08-02 항목)
- `publish_status`: PUBLISHED (Blogger published: 2026-08-02 05:29 UTC, status: LIVE)
- `blogger_post_id`: 4080593902932668278 (auto-run이 만든 DRAFT를 새 글로 다시 올리지
  않고 `blogger-client.js publish`로 공개 전환 — 고아 초안 없음)
- `blog_url`: https://gamedevclaude.blogspot.com/2026/08/unity-goap-claude-code-ai.html
- `title`: 성격 여섯이 전부 다르게 살기 시작했는데, 정작 볼 게 없었다 — Unity GOAP 성향
  벡터 인디게임 개발일지 (Claude Code 게임 개발 AI 페어 프로그래밍 후기)
- `labels`: Unity GOAP, 인디게임 개발일지, Claude Code 게임 개발, AI 페어 프로그래밍 후기
- `local_archive`: tools/blog-automation/published/2026-08-02-unity-goap-m12-trait-vector.html
  (게시 후 Blogger GET으로 되받은 `content` 필드와 md5 대조해 완전 일치
  (byte-identical, 45,988 bytes, U+FFFD 0건)함을 직접 확인.)
- `content_note`: 이 회차는 blog-editor(Step 5)를 거치지 않았다. 대신 로컬 세션이
  마크다운 표 2개를 실제 `<table>`로 변환했다 — auto-run의 최소 변환기는 표를 파이프
  문자 그대로 `<p>`에 흘려보냈다(`| 성격 | 근면 | …`). 이 블로그의 첫 HTML 표 사용 회차다.

## 이전 사용 커밋 (2026-07-30 M11 회차)

- `latest_commit`: `7cd9e46` (docs(spec) M11-K 명세 마무리 — 앵커 전수 교체 노트 + DoD
  체크 — 기획팀 브리프(`01_planner_brief.md`) commit_refs 중 타임스탬프 기준 가장 최신 커밋)
- `selected_commits_range`: `6a0486e`(spec(goap) M11 개인화 경제 실행명세서) ~ `7cd9e46`
  (docs(spec) M11-K 명세 마무리) — M11-A~K 구현 22커밋 전체: `6a0486e`(spec M11 개인화
  경제 실행명세서 — 브레인스토밍 16항+보완 4건) · `1ebdff3`(M11-A 개인 인벤토리 선반 —
  슬롯 9종·몸 소지·집 저장·상한 컴파일러 주입) · `3912f4c`(M11-B 먹거리 개인화 — 소비·
  채집·조리·수확 에셋 슬롯 교체) · `bbcae48`(M11-D 식량일수 개인화 — 슬롯 개인 산식·HUD
  최솟값) · `dc11a61`(fix 조리 goal 3종 MyCookedFood<=6 트리거 제거) · `2807386`(test 구
  식사·수확 게이트 3종을 개인 경제 의미로 개정) · `939b27f`(M11-C 집 저장 — 넣기/꺼내기
  액션 4종 + Goal_StoreFood, .cs 0) · `0d622ed`(M11-F 개인 택지 — HomePicker·집들이) ·
  `78f0791`(M11-G 집 동기 — 피격 플래그·저축 goal·노숙 방향 도피) · `900ae7c`(M11-H 보상
  실이전 — 개인 잔고 정산·연기, 전역 스톡 통로 제거) · `51278e2`(fix 선불 판정에서 수령
  공간 제거 — 선불 성격의 구조적 거절 진단) · `3601ae0`(M11-I 간호 2단계 + 목수 자가
  건축 — crowding 해소·상실 착탄) · `c1a097c`(M11-E 개인 밭 — 소유 등록·집 곁 배치·
  goal 개인화) · `1d80710`(fix 밭 goal 목표 슬롯 도달 불가로 인한 Burst 0-나눗셈 크래시) ·
  `669e795`(fix 조리식 집 저장 구조적 불발 진단·설계 확정) · `e53d08a`(M11-J① 전역 식량
  0 + 명령 보상 휴면) · `b4e79bd`(M11-J② 성격 5종 택지 취향 값 배정) · `1b38c2b`(M11-J③
  게으름뱅이 성격 신설 — 리허설, .cs 0 증명) · `c31590d`(fix 택지 취향을 선호 거리 대역
  으로 개선) · `b7519d5`(refactor 순수 판정 함수에서 로그 제거 — 게이트 콘솔 오염 해소) ·
  `3d504f5`(M11-K 탈중심 마을 — 개인 모닥불·제자리 식사·맵-비례 확산) · `7cd9e46`(docs
  M11-K 명세 마무리). 기획팀 브리프(`01_planner_brief.md`) commit_refs 전체 반영.
- `cycle_date`: 2026-07-30 (마스터 Step 6 2차 APPROVED 후 게시팀 Step 7 발행, 반려
  카운터 0회로 Step 3/4/6 정상 승인 경로 완주 — draft 위임 아님)
- `publish_status`: PUBLISHED (Blogger published: 2026-07-30 06:20 UTC, status: LIVE)
- `blogger_post_id`: 6558847625680705861
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/unity-goap-claude-code-ai_0280147432.html
- `title`: 공용 창고를 지웠더니 마을이 뿔뿔이 흩어졌다 — Unity GOAP 개인화 경제 인디게임
  개발일지 (Claude Code 게임 개발 AI 페어 프로그래밍 후기)
- `labels`: Unity GOAP, 인디게임 개발일지, Claude Code 게임 개발, AI 페어 프로그래밍 후기
- `local_archive`: tools/blog-automation/published/2026-07-30-unity-goap-m11-personalized-economy.html
  (POST 응답 JSON의 content 필드(fetch/undici로 획득, `data += c` 스트림 이어붙이기
  버그 미해당 경로)와 게시 직전 임시 파일 `_tmp_content.html`을 md5 대조해 완전 일치
  (byte-identical, 15,629자, U+FFFD 없음)함을 직접 확인.)
- `비고`: M11 "개인화 경제" 소재(6a0486e~7cd9e46). 마을 전역 창고 하나를 지우고 식량·집·
  밭·모닥불을 전부 개인 소유로 쪼갠 밀스톤 — "각자 먹고살되, 마을은 함께 짓는다"(식량만
  개인화, 목재·석재 등 자재는 공용 유지). 핵심 재미 포인트는 Burst Job에서 스택 추적 없이
  터진 0-나눗셈 크래시를 "밭 goal에 목표 슬롯을 올리는 액션이 카탈로그에 없다"는 데이터
  결함으로 역추적한 진단(`1d80710`), 보상 경제가 전역 창고 제거로 조용히 죽어 있었던 것과
  선불 목수가 "수령 공간" 오조건으로 구조적으로 항상 거절하던 버그(`51278e2`), 목수가
  마을에서 유일하게 집이 없던 오래된 구멍을 목수 전용 자가 건축 goal로 닫은 것(`3601ae0`).
  마지막 조각(M11-K)에서 공용 모닥불을 개인 시설로 내려 마을이 성격별 선호 거리(순둥이
  0.1~떠돌이 0.95, 맵 반경 비율)로 흩어진 홈스테드 마을로 시각적으로 바뀜. M11 자체의
  종료 판정(24일 방치 관측·회상 테스트)은 origin/main에서 검증 불가능해 본문 제외 —
  구현 완료(M11-K)까지만 다룸. M12(성격 축)·M13(사건과 흔적)·scene-video 트랙은 말미
  예고 한 문단만 허용하고 본문에서 다루지 않음.

### 이전 회차 이력 (2026-07-28, M10 야생 위협과 방랑자)

- `latest_commit`: `caa23b0` (feat(goap) M10-G 리허설 — 티어3 큰 곰 (.cs 0 증명) + 비용표
  갱신 — 기획팀 브리프(`01_planner_brief.md`) commit_refs 중 타임스탬프 기준 가장 최신 커밋)
- `selected_commits_range`: `db82ffd`(spec(m10) 야생 위협과 방랑자 실행명세서) ~ `caa23b0`
  (feat(goap) M10-G 리허설 — 티어3 큰 곰) — M10-A~G 구현 14커밋 전체:
  `db82ffd`(spec(m10) 실행명세서 — 상실의 순환) · `8cf2151`(M10-A 부상 축 코어 — 최초의
  사망 축) · `b255063`(M10-B 간호 — TendActionSO/TendRunner·치료사 직업 신설) · `1e0e612`
  (M10-C 위협 선반 — ThreatSO/ThreatService/ThreatAgent + 늑대 티어 1·2) · `6a497be`
  (fix TendInjured BaseCost 1→8 — 카탈로그 최소 비용 붕괴로 인한 휴리스틱 수축·탐색 폭발
  수정) · `0085fc2`(test 카탈로그 최소 BaseCost 게이트) · `868b0fd`(M10-D 도망 —
  FleeToSafety·Goal_Flee·성격 감지 반경) · `074c400`(M10-E 방랑자 — 런타임 인구 문·Y/N
  수락·술렁임) · `9ff7fdc`(M10-F 전멸 종료 화면 — 마을의 마지막 날) · `ace7804`(fix 티어2
  임계 12→10) · `b6a6b00`(fix 주민 타격형 위협 추격 도입 — 고정 타깃은 도망이 100% 이겨
  부상 착탄 불가, 명세 M10-C 개정) · `4dd4c2a`(feat 위협 경주 게이지) · `40e1135`(fix 티어2
  임계 10→8) · `86ccc4b`(feat 배속 기능, 관측 도구) · `caa23b0`(M10-G 리허설 — 티어3 큰 곰,
  .cs 0). 기획팀 브리프(`01_planner_brief.md`) commit_refs 전체 반영.
- `cycle_date`: 2026-07-28 (로컬 수동 사이클 — 마스터 Step 6 2차 APPROVED 후 게시팀 Step 7
  발행. **경위**: 같은 날 오전 원격 auto-run이 이 소재로 먼저 집필했으나 검수팀 2차·마스터
  1차 재검증에서 연속 3회 반려되어 draft로 강등됐고(`blogger_post_id` `4321240890939765189`,
  사용자가 Blogger에서 직접 삭제함), 오후 로컬 세션에서 파이프라인 지시서 6개 커밋 수정 후
  같은 소재로 재집필하여 반려 0회로 Step 3/4/6을 전부 통과, 이번에 정식 공개 발행함.)
- `publish_status`: PUBLISHED (Blogger published: 2026-07-28 09:41 UTC / 2026-07-28 02:41
  PDT, status: LIVE)
- `blogger_post_id`: 1047866260477949128
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/unity-goap-claude-code-ai.html
- `title`: Unity GOAP 구현 인디게임 개발일지: 마을에 처음으로 무덤이 생겼다 — 야생 위협·
  부상·방랑자를 심은 이야기 (Claude Code 게임 개발 AI 페어 프로그래밍 후기)
- `labels`: Unity GOAP 구현, Claude Code 게임 개발, AI 페어 프로그래밍 후기, 인디게임 개발일지
- `local_archive`: tools/blog-automation/published/2026-07-28-unity-goap-m10-wolf-injury-wanderer.html
  (POST 응답 직후 별도 GET 요청을 `Buffer.concat` 후 `toString('utf8')`로 재조회해 저장 —
  POST 응답을 그대로 stdout에 출력하면 `data += c` 스트림 이어붙이기로 U+FFFD 깨짐이
  실제로 관측됐으나(예: "안전"→"안���"), 별도 GET 재조회본은 원본 임시 파일의 html_content와
  문자열 완전 일치(byte-identical, md5 `6f7e6951cfdd6d28566fe5a2342a9164` 동일, 10,928자,
  U+FFFD 없음)함을 직접 확인함. 아침 draft 강등분 사본(동일 파일명)을 이번 정식 발행분으로
  덮어씀.)
- `비고`: M10 "야생 위협과 방랑자" 소재(db82ffd~caa23b0). 마을에 처음으로 부상·간호·사망
  (무덤)이라는 상실 축과, 그 상실을 회복하는 방랑자 합류 축이 함께 생긴 밀스톤. M9 회상
  테스트의 "이탈이 없어 재미 미흡" 판정에 대한 정면 응답. 핵심 재미 포인트는 늑대가 고정된
  스폰 지점만 때리던 실측 버그(도망이 100% 이겨 부상이 원천적으로 착탄 불가) — 추격
  메커니즘 도입으로 명세를 현장에서 개정해 해소. 성격별 위협 감지 반경(FleeRadiusMult)으로
  "성격이 생사를 가른다"를 구현. M11(개인화 경제)·M12(성격 축)는 말미 예고 한 문단만
  허용하고 본문에서 다루지 않음.

### 이전 회차 이력 (2026-07-26, M9 공간과 재해)

- `latest_commit`: `a351437` (fix(goal) 조리 goal 재료 조건 5 상향 — 위기 레시피와 정합
  (NoSolution 방어) — 기획팀 브리프(`01_planner_brief.md`) commit_refs 중 타임스탬프 기준
  가장 최신 커밋)
- `selected_commits_range`: `afdbc22`(feat(zone) M9-A 구역 선반, ZoneService) ~ `a351437`
  (fix(goal) 조리 goal 재료 조건 5 상향) — M9-A~J 구현 22커밋 전체:
  `afdbc22`(M9-A 구역 선반) · `ebab28a`(M9-B 파괴 문) · `db6e00c`(M9-C 재해 선반
  DisasterSO+DisasterService) · `fe05bfd`(M9-D 에셋 배선 여름·폭염·한파·관개수로) ·
  `8e27deb`(M9-G FoodDaysLeft 파생 슬롯) · `52e093a`(M9-H RelativeToCurrent 씬 goal 확장) ·
  `eccf35f`(M9-I HUD 식량 N일치 표기) · `99a61ab`(M9-J 식량 goal 트리거 개편, .cs 0) ·
  `188bbd4`(fix(food) 밭 재파종 FoodDaysLeft 게이트) · `79f1167`(fix(food) Plant 게이트
  6→4 하향, 중간 리뷰 ①) · `7863f9d`(test(agent) 이탈 진단 로그 임시) · `c504d4d`
  (fix(food) 조리식 포만 50→35) · `1082c2e`(refactor(agent) 이탈 진단 로그 제거, 조사
  종결·실제 이탈 0건) · `a16593a`(chore(scene) 관개수로 goal 파킹) · `5f3de87`
  (feat(chatter) M9-E N명 대화 릴레이+농부 회의) · `724fafe`(feat(chatter) 회의 장면 연출
  배속+HUD 알림, 사용자 Play 피드백 대응) · `9240e43`(feat(disaster) M9-F 홍수 리허설,
  에셋 1개·코드 0줄) · `33dd94b`(fix(disaster) 홍수를 밭 시설 파괴로 전환, 중간 리뷰 ②
  근본 진단) · `bc18e15`(balance(season) 가을 자원 재생 1.5→1.0) · `07a8bec`
  (balance(cook) 조리 레시피 계절 분화) · `a351437`(fix(goal) 조리 goal 재료 조건 5 상향).
  기획팀 브리프(`01_planner_brief.md`) commit_refs 전체 반영 — 명세 2건(`db4424a`·
  `9980876`)은 #11(2026-07-22 발행)에서 이미 다뤄 배경 참조로만 인용, 이번 글 본문에서는
  재해설하지 않음.
- `cycle_date`: 2026-07-26 (마스터 1차·2차 승인 후 게시팀 Step 7 발행, 격일 auto-run —
  완전 무인 실행)
- `publish_status`: PUBLISHED (Blogger published: 2026-07-26 04:39 UTC / 2026-07-25 21:39
  PDT, status: LIVE)
- `blogger_post_id`: 2543183737716951825
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/unity-goap-ai-claude-code-ai.html
- `title`: Unity GOAP AI 인디게임 개발일지: 마을에 "구역"과 "재해"를 심었더니 홍수가 밭을
  한 개도 못 태운 이유 (Claude Code 게임 개발 AI 페어 프로그래밍 후기)
- `labels`: Unity GOAP AI, Claude Code 게임 개발, AI 페어 프로그래밍, 인디게임 개발일지
- `local_archive`: tools/blog-automation/published/2026-07-26-unity-goap-m9-zone-disaster-flood.html
  (POST 응답 직후 별도 GET 요청을 `Buffer.concat` 후 `toString('utf8')`로 재조회해 저장 —
  재조회한 content가 원본 05_final.md의 html_content(마크다운 서식 제거본)와 문자열 완전
  일치(byte-identical, md5 동일, 25,019 bytes)함을 직접 확인했고 U+FFFD 없음도 확인.)
- `비고`: M9 "공간과 재해" 소재(afdbc22~a351437). 마을에 처음으로 구역(ZoneService)과
  재해(DisasterSO/DisasterService)가 생기고, 식량을 개수가 아니라 FoodDaysLeft("며칠
  버티나")로 말하기 시작한 밀스톤. 핵심 재미 포인트는 홍수가 "작물 0개 소실"만 때리던
  근본 진단 — 파종 goal의 전제("식량 넉넉하면 파종 중단")와 재해 설계의 전제("잉여를
  깎는 지렛대")가 서로 반대 방향을 보고 있었던 것. 이번 사이클은 blog_next_material_
  priority.md가 4회 연속(07-22·07-24·07-25·07-26) "devlog 서술 미비" 게이트로 보류하던
  것을 사전점검이 직접 해제하고 ACTIVE 지정한 첫 회차다 — 1차 소스를 devlog 세션 로그
  (M9-A~J 서사 0줄 확인됨, 훅 중복 버그로 고유 헤더 4개뿐)에서 `Docs/M9_공간축과재해_
  실행명세서`·`Docs/M9_식량수지_보완가이드.md` + 커밋 메시지 원문으로 전환한 것이 이번
  회차의 핵심 방법론 판단이었다(#12 방법론 명세서 회차와 동일 경로). 회상 테스트 판정과
  M9 종료·M10 전환 선언은 리포에서 검증 불가능해 본문에서 제외(밸런스 4커밋 적용까지로
  닫음). M10(야생 위협과 방랑자)·M11(개인화 경제)·M12(성격 축)는 말미 예고 한 문단만
  허용하고 본문에서 다루지 않음.

### 이전 회차 이력 (2026-07-24, 개발 방법론 명세서)

- `latest_commit`: `ee165a5` (docs(method) 방법론 개념 노트 성좌 — 3개 커밋 중 타임스탬프
  기준 가장 최신, 2026-07-18 04:21)
- `selected_commits_range`: `1ab4f9c`(docs(method) 개발 방법론 명세서, 04:00) ~ `ee165a5`
  (docs(method) 방법론 개념 노트 성좌, 04:21) — 3커밋 전부: `1ab4f9c`(docs(method) 개발
  방법론 명세서, 04:00) · `ae9d393`(docs(method) 방법론 다이어그램, 04:07) · `ee165a5`
  (docs(method) 방법론 개념 노트 성좌, 04:21). 기획팀 브리프(`01_planner_brief.md`)
  commit_refs 전체 반영.
  ⚠️ **git 계보 caveat**: 이 3커밋은 유효한 커밋 객체로 존재하고 내용도 현재 저장소
  파일과 일치하지만, `git merge-base --is-ancestor <hash> HEAD` 검사는 실패한다 — main이
  스쿼시/재작성되며 `bc18e15`(balance(season), 678개 파일·377,003줄 대형 스쿼시 커밋)를
  통해 원본 3커밋의 개별 정체성 없이 내용만 main 계보에 합류했다. 산출물 파일
  (Docs/개발_방법론_명세서.md, Docs/개발_방법론.canvas, Docs/방법론/*.md 12개)은
  origin/main HEAD에 실제로 존재함을 직접 확인했고, 이를 근거로 소재 진위·존재를
  판단했다 — 상세는 `01_planner_brief.md`의 `git_lineage_caveat` 참조.
- `cycle_date`: 2026-07-24 (마스터 1차·2차 승인 후 게시팀 Step 7 발행, 격일 auto-run)
- `publish_status`: PUBLISHED (Blogger published: 2026-07-24 04:24 UTC / 2026-07-23 21:24
  PDT, status: LIVE)
- `blogger_post_id`: 6860975822353124027
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/claude-code-ai-unity-goap-ai-ai.html
- `title`: Claude Code 게임 개발 AI 페어 프로그래밍 후기: Unity GOAP AI 인디게임 개발일지 —
  AI에게 "나처럼 판단해줘"를 문서 한 장으로 남기기까지
- `labels`: Claude Code 게임 개발, AI 페어 프로그래밍 후기, Unity GOAP AI, 인디게임 개발일지
- `local_archive`: tools/blog-automation/published/2026-07-24-unity-goap-methodology-spec-constellation.html
  (POST 응답 직후 별도 GET 요청을 `Buffer.concat` 후 `toString('utf8')`로 재조회해 저장 —
  재조회한 content가 원본 05_final.md의 html_content(마크다운 서식 제거본)와 문자열 완전
  일치(byte-identical, md5 동일, 11,564 bytes)함을 직접 확인했고 U+FFFD 없음도 확인.)
- `비고`: 개발 방법론 명세서 소재(1ab4f9c~ee165a5). 코드가 아니라 "AI가 이 프로젝트에서
  어떻게 판단하는가"를 방법론 명세서 1건 + mermaid 다이어그램 2종 + 옵시디언 Canvas 1개 +
  위키링크 개념 노트 12개로 박제한 메타 회차. M9/M10/M11(공간·재해/야생 위협/개인화 경제)은
  이번 글에서 다루지 않음 — 전부 devlog 서술 미비로 승격 보류 상태(아래 다음 회차 소재
  판단 참조).

### 이전 회차 이력 (2026-07-22, M8 후속 보완 + M9 명세)

- `latest_commit`: `897605c` (fix(social) 완공 보고 "쫓아가기"→"마주치면 정산"(조각 Y) +
  IPathfinder 경계 — 4개 커밋 중 타임스탬프 기준 가장 최신, 2026-07-18 14:48)
- `selected_commits_range`: `db4424a`(spec(m9) 공간 축과 재해 실행명세서, 02:27) ~
  `897605c`(fix(social) 완공 보고 "쫓아가기"→"마주치면 정산" + IPathfinder 경계, 14:48) —
  비연속 4커밋: `897605c`(fix(social), 14:48) · `f907fc2`(feat(bubble) 말풍선 침묵 여운 3초,
  03:05) · `db4424a`(spec(m9) 공간 축과 재해 실행명세서, 02:27) · `9980876`(spec(m9) 식량
  수지 보완가이드, 02:57). 기획팀 브리프(`01_planner_brief.md`) commit_refs 전체 반영 —
  같은 시간대에 섞여 있던 서사 무관 부수 커밋(`958fbfd`·`b53d52d`·`3170129`·`8b828ad`
  devlog/파이프라인 정책 커밋, `1ab4f9c`~`ee165a5` 개발 방법론 명세서, `dda3a69` 사전점검
  기록 등)은 제외.
- `cycle_date`: 2026-07-22 (마스터 1차·2차 승인 후 게시팀 Step 7 발행, 격일 auto-run)
- `publish_status`: PUBLISHED (Blogger published: 2026-07-22 04:39 UTC / 2026-07-21 21:39
  PDT, status: LIVE)
- `blogger_post_id`: 4764036419422193885
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/claude-code-ai-unity-goap-ai.html
- `title`: Claude Code 게임 개발 AI 페어 프로그래밍 후기: Unity GOAP AI 인디게임 개발일지 —
  쫓아가도 못 잡는 버그를 "빚으로 적어두기"로 뒤집은 길찾기 확장 설계
- `labels`: Claude Code 게임 개발, AI 페어 프로그래밍 후기, Unity GOAP AI, 인디게임 개발일지
- `local_archive`: tools/blog-automation/published/2026-07-22-unity-goap-debt-settlement-pathfinder-boundary.html
  (POST 응답의 `labels` 필드에서 `data += c` 스트림 이어붙이기 버그로 멀티바이트 문자가
  깨진 것을 직접 확인했다(U+FFFD, "인디게임"이 "인���게임"으로 표시됨) — 그대로 저장하지
  않고 별도 GET 요청을 `Buffer.concat` 후 `toString('utf8')`로 재조회해 저장. 재조회한
  content가 원본 `05_final.md`의 html_content와 문자열 완전 일치(byte-identical, md5 동일,
  13,581 bytes)함을 직접 확인했고 U+FFFD 없음도 확인.)
- `비고`: M8 후속 보완(완공 보고 "쫓아가서 건네기" → "빚+정산" 재진단, IPathfinder 경로탐색
  창구 단일화) + M9 명세 단계(공간·재해 실행명세서, 식량 수지 보완가이드) 묶음 소재.
  M9 실제 구현(M9-A~J, `afdbc22`~`a351437`)은 이번 글에서 다루지 않음 — 기획팀 브리프의
  명시 지시(밀스톤 미완결, 재관측 대기)를 그대로 따름, 말미 예고 한 문단만 허용.

### 이전 회차 이력 (2026-07-21, M8 관계·소유·부탁)

- `latest_commit`: `66f690b` (docs(devlog) 2026-07-18 M8 후속 세션 — 범위 종료점)
- `selected_commits_range`: `68c6ef8`(spec(m8) 사회 축 실행명세서) ~ `66f690b`(docs(devlog)
  2026-07-18 M8 후속 세션) — 18커밋(M8-A~F 6개 세부 항목 + CLAUDE.md 비용표 갱신·DoD
  코드 검증·세션 로그 + 후속 보완 4건(대화 호흡 상향+공용 쿨다운 8e53abf, 부탁 가시화+
  완공 보고 심부름+주민 간 보상 5ce720e, 성격별 보상 태도 떼먹기·선불 요구
  efcb0d5/8d72438, 보고 도착 재검사+씬 주민 10→4명+장식성 발화 억제 2d24b0f/9be5239,
  집=목수 부탁 전용화 5956af2) 포함, 기획팀 브리프 `01_planner_brief.md` commit_refs
  전체 반영 — 서사 무관 부수 커밋 0369bda·987e9bb는 제외)
- `cycle_date`: 2026-07-21 (마스터 1차·2차 승인 후 게시팀 Step 7 발행)
- `publish_status`: PUBLISHED (Blogger published: 2026-07-21 04:26 UTC / 2026-07-20 21:26 PDT, status: LIVE)
- `blogger_post_id`: 2940839193792714370
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/claude-code-ai-goap-ai-m8.html
- `title`: Claude Code 게임 개발 AI 페어 프로그래밍 후기: 유니티 GOAP AI 인디게임 개발일지 — 주민들이 드디어 서로를 "기억"하기 시작했다 (M8 관계·소유·부탁)
- `labels`: Claude Code 게임 개발, AI 페어 프로그래밍 후기, 유니티 GOAP AI, 인디게임 개발일지
- `local_archive`: tools/blog-automation/published/2026-07-21-unity-goap-m8-relationship-ownership-request.html
  (POST 응답의 content 필드는 `data += chunk` 스트림 이어붙이기 버그로 멀티바이트 문자가
  깨져 있어(U+FFFD 확인됨) 그대로 저장하지 않고, GET으로 재조회하며 `Buffer.concat` 후
  `toString('utf8')`로 재획득해 저장 — 원본 05_final.md html_content와 바이트 단위로
  완전 동일함을 직접 확인, 14,874 bytes)
- `비고`: M8 관계·소유·부탁 소재(68c6ef8~66f690b). 대화 이벤트 구독형 관계 축적(M8-A,
  RelationshipService)·정보줄 단짝·원한 표기(M8-B)·소유 축(M8-C, OwnershipService+
  MyHasHome)·부탁 선반(M8-D, RequestSO/RequestService/JudgeRequest)·부탁 에셋 1호+
  요리 부탁 리허설(M8-E/F, 데이터 주도 설계 다섯 번째 재현)까지 A~F 전 항목 포함.
  완료 선언 직후 실측에서 발견된 후속 보완 4건 — 성격별 보상 태도(떼먹기·선불 요구)·
  집은 목수 부탁으로만(자율 건설·자동 배정 폐기, 트레이드오프)·대화 흐름 보호(장식성
  발화 억제)·부탁 가시화+완공 보고 심부름+주민 간 보상 — 도 다룸. 이 글로 M8 미발행
  구간 해소.


### 이전 회차 이력 (2026-07-20, M7 상호대화와 가시성)

- `latest_commit`: `598e51e` (feat(content) 대사 풀 2차 확장 — .cs 0, 사용자 요청)
- `selected_commits_range`: `a451435`(spec(m7) M7 실행명세서) ~ `598e51e`(feat(content) 대사 풀 2차 확장) — 11커밋(M7-A~E 5개 세부 항목 + M7-T3 에셋 정책 게이트 + M7 완료 직후 실측에서 발견된 부수 버그 수정 3건(모닥불 몰림 8e1ac6b·대화 연출 94b892e·말풍선 우선순위 799842f) + 대사 풀 2차 확장 598e51e 포함, 기획팀 브리프 `01_planner_brief.md` commit_refs 전체 반영)
- `cycle_date`: 2026-07-20 (마스터 1차·2차 승인 후 게시팀 Step 7 발행)
- `publish_status`: PUBLISHED (Blogger published: 2026-07-20 04:43 UTC / 2026-07-19 21:43 PDT, status: LIVE)
- `blogger_post_id`: 267235146240614055
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/claude-code-ai-goap-ai-m7.html
- `title`: Claude Code AI 페어 프로그래밍 후기: 유니티 GOAP AI 인디게임 개발일지 — 주민들이 드디어 서로에게 말을 걸기 시작했다 (M7 상호대화와 가시성)
- `labels`: Claude Code 게임 개발, AI 페어 프로그래밍 후기, Unity GOAP AI, 인디게임 개발일지
- `local_archive`: tools/blog-automation/published/2026-07-20-unity-goap-m7-chatter-visibility.html
  (Blogger API 응답의 content 필드를 그대로 저장 — 실제 게시본과 동일, 9,101 bytes)
- `비고`: M7 상호대화와 가시성 소재(a451435~598e51e). 이름표+선택 정보줄(M7-A)로 성격(M4)·
  직업(M5) 분화가 처음으로 화면에서 보이게 됨·대사 풀 확장(.cs 0, M7-B)·ChatterSO/
  ChatterService 상호대화 선반(M7-C, ADR: 표현 전용·관계 미축적·이름 분기 금지·주기당
  최대 1쌍)·대화 에셋 2종(M7-D)+에셋 정책 게이트(M7-T3)·직교성 리허설(M7-E, 에셋
  1개·코드 0)까지 전 항목 포함. 완료 직후 실측에서 발견된 부수 버그 3건(모닥불 몰림
  해소·대화 연출 멈춰서 마주보기+반경 4→2타일·말풍선 우선순위 정리)도 다룸. 이 글로
  M7 미발행 구간 해소.

### 이전 회차 이력 (2026-07-19, M6 예고된 겨울과 설득)

- `latest_commit`: `7c0dd7f` (feat(personality) 성격이 위기 대응을 가른다 — GoalBoosts 축 + 이탈 계단화)
- `selected_commits_range`: `aa795e6`(spec(m6) M6 실행명세서) ~ `7c0dd7f`(feat(personality) 성격이 위기 대응을 가른다) — 12커밋(M6-A~F 8개 세부 항목 + 위기감 재조정 + 개체 편차 붕괴 수정 포함, 기획팀 브리프 `01_planner_brief.md`에서 지정 문서 범위(`aa795e6`~`80cac26`)를 `a0e318b`·`7c0dd7f`(같은 세션의 M6 결말부)까지 보정)
- `cycle_date`: 2026-07-19 (마스터 1차·2차 승인 후 게시팀 Step 7 발행)
- `publish_status`: PUBLISHED (Blogger published: 2026-07-19 04:30 UTC / 2026-07-18 21:30 PDT, status: LIVE)
- `blogger_post_id`: 4301273701134180743
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/claude-code-unity-0.html
- `title`: 인디게임 개발일지: Claude Code와 만든 Unity 계절 시스템 구현 — 기술은 만점인데 재미는 0점이었다
- `labels`: AI 페어 프로그래밍 후기, Claude Code 게임 개발, Unity 계절 시스템 구현, Unity GOAP 게임 밸런싱
- `local_archive`: tools/blog-automation/published/2026-07-19-unity-goap-m6-season-winter-crisis.html
  (Blogger API 응답의 content 필드를 Buffer.concat 후 toString('utf8')로 재획득해 저장 —
  게시 직후 별도 GET으로 재조회한 값과 원본 제출본을 diff/md5 대조해 완전 동일함을 확인,
  13,856 bytes)
- `비고`: M6 예고된 겨울과 설득 소재(aa795e6~7c0dd7f). 계절 선반(SeasonSO/SeasonService)·
  겨울 효과(배율 곱 지점 2곳)·달력 HUD 예고·굶주림 이탈(이 게임 최초의 실패 상태)·보상
  재설득(에스크로 방식, 이 게임 최초의 협상 수단)·가을 리허설(.cs 0)까지 M6-A~F 전 항목+
  완료 직후 실측에서 "겨울 수지 2배 잉여"를 발견해 즉시 재조정(겨울 3→4일, 감쇠 1.75배,
  주민 5→10명)한 위기감 재조정(a0e318b)·성격별 위기 대응 분화(GoalBoosts, 7c0dd7f)까지
  포함. 같은 세션에서 발견된 "개체 편차 붕괴" 버그(주민 5명의 초기 포만이 문자열 해시
  충돌로 81.6~81.7에 뭉치던 것을 FNV-1a로 근본 수정, 44b7538)도 다룸. 이 글로 M6 미발행
  구간 해소.

### 이전 회차 이력 (2026-07-18, M5 직업과 일과)

- `latest_commit`: `87d7d7f` (feat(m5) M5-E 목수 리허설 — 직업 추가 = 에셋 1개 + 씬 풀 등록, .cs 0개)
- `selected_commits_range`: `fd6b1db`(feat(m5) M5-A JobSO 스키마·스폰 할당) ~ `87d7d7f`(feat(m5) M5-E 목수 리허설)
- `cycle_date`: 2026-07-18 (마스터 1차·2차 승인 후 게시팀 Step 7 발행)
- `publish_status`: PUBLISHED (Blogger published: 2026-07-18 KST, status: LIVE)
- `blogger_post_id`: 2221417024528752034
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/unity-goap-ai.html
- `title`: 인디게임 개발일지: Unity GOAP AI 직업 시스템 — 농부는 밭 곁을 떠나지 않고, 탐험가는 지도 밖으로 사라진다
- `labels`: Unity GOAP AI, 인디게임 개발일지, 게임 AI 프로그래밍, GOAP 직업 시스템
- `local_archive`: tools/blog-automation/published/2026-07-18-unity-goap-m5-job-schedule.html
  (Blogger API 응답의 content 필드를 그대로 저장 — 실제 게시본과 바이트 단위 동일, 14,466 bytes)
- `비고`: M5 직업과 일과 소재(fd6b1db~87d7d7f, 5커밋+JPS 대각 강제 이웃 수정 eac0907 포함).
  JobSO 스키마·실효 우선순위 결합(ADR-M5-6, 사용자 승인 하 방향 전환)·일과 주입(농부 밭
  배회·탐험가 지도 밝히기)·직업 에셋 5종·6번째 직업(목수) 코드 0줄 리허설 + 같은 세션에서
  발견된 JPS 대각선 길찾기 버그(M4 수정 c03d461의 반쪽 봉합을 뒤늦게 발견)까지 포함.
  게시 중 게시팀 운영 실수로 동일 글이 한 번 더 게시(post_id 4412536271596111356,
  url .../unity-goap-ai_01703797100.html)됐다가 Blogger API DELETE(HTTP 204)로 즉시
  삭제·정리됨 — 최종 라이브 글은 위 post_id 하나만 남음. 이 글로 M5 미발행 구간 해소.

### 이전 회차 이력 (2026-07-17, M4 성격 시스템)

- `latest_commit`: `862de3b` (docs(m4) M4 완료 선언 — 성공 기준 S1~S7 충족)
- `selected_commits_range`: `b982090`(spec(m4) M4 주민의 성격 실행명세서) ~ `862de3b`(docs(m4) M4 완료 선언)
- `cycle_date`: 2026-07-17 (마스터 2차 승인 후 게시팀 Step 7 발행)
- `publish_status`: PUBLISHED (Blogger published: 2026-07-17 KST, status: LIVE)
- `blogger_post_id`: 6809679934191538669
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/unity-goap_0950458123.html
- `title`: 주민이 처음으로 "다른 사람"으로 보인 날 — Unity GOAP 성격 시스템 인디게임 개발일지
- `labels`: Unity GOAP, 인디게임 개발일지, GOAP 성격 시스템
- `local_archive`: tools/blog-automation/published/2026-07-17-unity-goap-m4-personality.md
- `비고`: M4 성격 시스템 소재(b982090~862de3b, 11커밋). PersonalitySO 스키마·비용 배율·명령 거부·혼잣말·5번째 아키타입 + JPS 강제 이웃 버그 수정 포함. 이 글로 M4 미발행 구간 해소.

### 이전 회차 이력 (2026-07-16, M2+M3 생산체인+주거 기반)

- `latest_commit`: `153f180` (M3 완료 선언 — M2 생산체인 + M3 주거 기반 소재의 마지막 커밋)
- `selected_commits_range`: `651ea47`(M2 스펙) ~ `153f180`(M3 완료 선언)
- `cycle_date`: 2026-07-16 (마스터 2차 승인 후 게시팀 Step 7 발행 — 전날 REJECTED_3X였던 동일
  소재가 계측 기준 통일 후 재작성/재검수를 거쳐 이번 회차에 승인·발행됨)
- `publish_status`: PUBLISHED (Blogger published: 2026-07-16 16:07 KST, status: LIVE)
- `blogger_post_id`: 3935987342991362953
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/unity-goap.html
- `title`: 밭에서 부엌으로, 그리고 처음 생긴 집 한 채 — Unity GOAP 생산체인과 마을 시뮬레이션 인디게임 개발일지
- `labels`: 유니티, 인디게임, AI개발, GOAP
- `local_archive`: tools/blog-automation/published/2026-07-16-unity-goap-m2-m3-production-housing.html
  (Blogger API 응답의 content 필드를 그대로 저장 — 실제 게시본과 바이트 단위 동일)
- `비고`: 2026-07-16 13:03 KST auto-run에서는 작성팀·검수팀 분량 계측 기준 불일치로 검수
  3연속 반려(REJECTED_3X, `blog_pipeline_alerts.md` 참조)되어 미발행이었으나, 계측 기준을
  `wc -m` 하나로 통일한 뒤 동일 소재로 재작성·재검수를 거쳐 마스터 1차·2차 승인을 모두
  통과했고 이번 로컬 세션에서 게시팀(Step 7)이 실제 발행을 완료했다. 이 글로 M2(생산체인)·
  M3(주거 기반) 미발행 구간이 해소됨 (BLOG_COVERAGE.md 참조).

### 이전 회차 이력 (2026-07-15, M0 회고 특집)

- `latest_commit`: `cc4602e` (유지 — 이번 회차는 회고 특집 예외, M0 커밋 구간은 별도 트랙이므로 갱신하지 않음)
- `selected_commits_range`: `8ee845b` ~ `b175ddf` + `04f0975` (M0 GOAP 정합 재설계 대서사 — 회고 특집)
- `cycle_date`: 2026-07-15 (원격 auto-run 13:03 KST — `blog_next_material_priority.md` ACTIVE 게이트 무조건 통과)
- `publish_status`: PUBLISHED (Blogger published: 2026-07-15 13:40 KST)
- `blogger_post_id`: 6764155466991758383
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/unity-goap-24800-5977-m0.html
- `title`: Unity GOAP 재설계: 24,800줄을 하루 만에 5,977줄로 줄인 M0 재건 개발일지
- `labels`: 유니티, 인디게임, AI개발, GOAP
- `local_archive`: tools/blog-automation/published/2026-07-15-unity-goap-m0-redesign.html
  (원격 sandbox push 403 실패 → 2026-07-15 로컬에서 Blogger API GET으로 재획득해 복구.
  검증: Buffer.concat 후 toString('utf8'), 13,826 bytes)
- `비고`: 원격 sandbox의 `claude/state-2026-07-15T040738Z` push가 07-14에 이어 **2회 연속
  403 실패** → MANUAL_STATE_UPDATE 경로로 로컬 수동 반영 (2026-07-15). 상세는
  `blog_pipeline_alerts.md` 참조. 이 글로 M0 미발행 구간이 해소됨 — 남은 미발행 구간은
  M2(생산체인)·M3(주거 기반) (BLOG_COVERAGE.md 참조). → 이 M2/M3 구간은 위 최신 항목
  (2026-07-16)에서 발행 완료됨.

### 이전 회차 이력 (2026-07-14, M1 밀스톤 완결)

- `latest_commit`: `cc4602e` (M1 밀스톤 완결 시점 — M1 개발 소재의 마지막 커밋. 이후 M2 명세 커밋들은 미소비)
- `selected_commits_range`: M1 트랙 (여가·식량순환·명령/거부·대사변주, 2026-07-14)
- `cycle_date`: 2026-07-14 (원격 auto-run 13:03 KST — 일일 게이트 전환 후 첫 발행)
- `publish_status`: PUBLISHED
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/unity-goap-ai-ai-m1.html
- `비고`: 원격 sandbox의 claude/state-* push가 403으로 실패 → 이 항목은 로컬에서 수동
  반영함 (2026-07-14). M0 재설계 대서사(24,800→5,977줄)는 이 글에서 다뤄지지 않음 →
  2026-07-15 회고 특집으로 발행 완료.

### 이전 회차 이력 (2026-07-11, F-A 성격배분 + Satiety 반전)

- `latest_commit`: `b930365` (refactor(core): 허기 → 포만감 세만틱 반전 — F-A 성격배분+Satiety 반전 묶음 소재의 마지막 커밋)
- `selected_commits_range`: 549444d ~ b930365 (549444d F-A 성격배분 + b930365 허기→포만감 반전, 2커밋 묶음)
- `cycle_date`: 2026-07-11 (원격 auto-run, session `cse_01U1p6C8aDWswa4xhkCzhXSR`)
- `publish_status`: PUBLISHED (실제 공개 발행 완료, Blogger status: LIVE)
- `blogger_post_id`: 118729701893909598
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/goap-ai-ai.html
- `local_archive`: (원격 sandbox에서 생성, git push 실패로 리포에 없음 — 필요 시 blogger-client.js get으로 재획득)
- `title`: 유니티 GOAP AI 개발일지: 인디게임 AI 리팩터링 — 성격 시스템 완성 후 발견한 씬 배치 버그와 허기→포만감 반전
- `labels`: 유니티, 인디게임, AI개발, GOAP

### 이전 회차 이력 (2026-07-09, 방향 ③ GatherIron 무해 봉합)

- `latest_commit`: `4df41cc` (docs(goap): N2 CLAUDE.md ADR-10 + 커밋 전 체크 ⑧ 추가 — 방향 ③ GatherIron 초반 무해 봉합 소재의 마지막 커밋)
- `selected_commits_range`: 87ac93c ~ 4df41cc (87ac93c, 5448e68, 45d546c, 81d8c76, 4df41cc — 방향 ③ GatherIron 초반 무해 봉합 5커밋)
- `cycle_date`: 2026-07-09
- `publish_status`: PUBLISHED
- `blogger_post_id`: 3490326739510391777
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/unity-goap-ai-4096.html
- `local_archive`: tools/blog-automation/published/2026-07-09-unity-goap-ai-4096.html

### 이전 DRAFT 이력 (참고용, 별개 소재)

- `latest_commit`: `f05ae3c` (docs(goap): CLAUDE.md에 이동 실패 first-class 계약(ADR-9 + 커밋 전 체크 ⑦) 명시)
- `selected_commits_range`: eb8aaed ~ f05ae3c (M1~M6, ADR-9 CLAUDE.md 반영까지 포함) — v1/v2 동일, 이 커밋 구간은 변경 없음
- `publish_status`: DRAFT (비공개 초안 — 여전히 공개 발행 아님)
- `blogger_post_id`: 7396645330542050308
- `blog_url`: https://gamedevclaude.blogspot.com/ (초안 상태라 퍼머링크 미발급. 관리자 화면: https://www.blogger.com/blog/posts/6014451945015572125?hl=ko)
- `local_archive`: tools/blog-automation/published/2026-07-09-goap-pathfinding-honest-failure.html

## v2 교체 기록 (2026-07-09)

마스터 에이전트가 Step 6에서 v2 최종 HTML을 승인한 뒤, 신규 게시가 아니라 **기존 DRAFT
교체**를 지시했다. 동일 소재(커밋 구간 eb8aaed~f05ae3c)에 대해 사용자 요청으로 분량 확대
+ "의외의 디테일" 섹션 등 재미 요소를 추가한 v2 본문으로 교체한 것이며, 새로운 소재를
다룬 것이 아니다.

- 사용한 명령: `node tools/blog-automation/scripts/blogger-client.js update --post-id 7396645330542050308 --title-file <tmp> --content-file <tmp> --labels <4개>` (신규 `post` 명령이 아니라 `update` 명령)
- 결과: HTTP 성공, `status: "DRAFT"` 유지 확인. 제목/본문/라벨 4개 모두 반영됨.
- 검증: update 직후 GET으로 실제 저장된 콘텐츠를 다시 가져와 로컬 원본 파일과 바이트 단위(`Buffer.concat` 후 `toString('utf8')` 비교, `data += chunk` 방식이 아님)로 비교해 완전히 동일함을 확인. (주의: `res.on('data', c => data += chunk)` 식으로 스트림을 이어붙이면 UTF-8 멀티바이트 문자가 TCP 청크 경계에서 잘려 `U+FFFD`로 깨져 보일 수 있다 — 실제 데이터 손상이 아니라 검증 스크립트 쪽의 버그였다. 다음에 콘텐츠 무결성을 확인할 때는 반드시 `Buffer.concat(chunks)` 후 한 번에 `toString('utf8')` 할 것.)
- 커밋 해시는 v1과 동일(`f05ae3c`)하며, 바뀐 것은 본문 내용뿐이다. `latest_commit`/`selected_commits_range` 필드는 갱신할 필요 없음.

## 기록 판단 근거 (draft인데도 이력에 남긴 이유)

이번 회차는 파이프라인 전체의 첫 드라이런이라 사용자가 명시적으로 "비공개 초안으로만 올려달라"고 요청했다. Blogger API 호출 자체는 성공했고(`status: DRAFT`로 정상 생성됨), 다만 공개 발행(publish)은 아니다.

- 커밋 해시를 기록한 이유: 이 소재(경로탐색 실패 시 좌표 스냅 제거, M1~M6)는 이미 한 편의 글로 소비되었다. 기록하지 않으면 다음 기획 사이클이 같은 커밋 구간을 다시 골라 중복 글을 기획할 위험이 있다.
- 다만 `publish_status: DRAFT`로 명시해 "공개 게시 완료"와는 구분했다. 다음 사이클의 기획팀이 이 필드를 보고 "이미 초안까지는 나갔다 → 그대로 공개 게시만 진행할지, 소재를 폐기하고 새로 갈지"를 판단할 수 있게 했다.
- 즉, 이 메모는 "공개 게시 완료 이력"이 아니라 "소재 소비 + 파이프라인 처리 이력"으로 남긴 것이다.

## 경로 정정 기록 (2026-07-09)

드라이런 중 blog-publisher 에이전트가 이 파일을 잘못된 위치(`C:\Users\anjyo\AI_GOAP\memory\`,
git 저장소 내부에 새로 생성)에 썼다가 여기(auto-memory 디렉토리)로 옮겼다. 원인: 에이전트
정의(blog-publisher.md)와 오케스트레이션 프롬프트에 "memory/"라고만 적혀 있고 절대경로가
명시되지 않아 에이전트가 저장소 상대경로로 오해함. blog-publisher.md와
tools/blog-automation/README.md에 절대경로를 명시해 재발을 막았다 — Phase 3 자동화에서
동일 실수가 나면 이 메모를 먼저 참조할 것.
