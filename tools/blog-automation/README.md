# AI_GOAP 블로그 자동화 파이프라인 — 실행 가이드

전체 기획은 [Docs/블로그_자동화_수익화_기획서.md](../../Docs/블로그_자동화_수익화_기획서.md)
참조. 이 문서는 실제 실행 순서(오케스트레이션)를 정리한다 — Phase 3에서 `scheduled-tasks`가
호출할 최상위 프롬프트가 이 순서를 그대로 따른다.

## 부서 에이전트

`.claude/agents/blog-*.md` 6개:

| 순서 | 에이전트 | 담당 |
|---|---|---|
| 1 | `blog-planner` | 소재 선정, 부족하면 스킵 판단 |
| 2 | `blog-writer` | 초안 작성 (톤 고정됨, 2026-07-09 승인) |
| 3 | `blog-reviewer` | 사실/정책/분량 검증, 통과 못하면 2로 직접 반려 |
| 4 | `blog-master` (Step 4) | 1차 승인 — 방향/내용, 검수팀 근거 재확인 |
| 5 | `blog-editor` | SEO/HTML 포맷팅 (마스터 1차 승인 후에만) |
| 6 | `blog-master` (Step 6) | 2차 승인 — 최종 HTML |
| 7 | `blog-publisher` | 실제 게시 + 이력 기록 |

## 스테이징 파일 (2026-07-09 추가 — 반려 루프 실전 검증 중 발견한 문제의 수정)

각 서브에이전트는 별도의 무상태(stateless) 세션으로 호출된다 — 이전 단계의 대화 맥락을
기억하지 못한다. 드라이런에서 이 때문에 실제 사고가 났다: Step 6이 REJECTED를 내고
편집팀(Step 5)을 재호출했는데, 재호출된 편집팀 세션이 원래 승인됐던 원고를 기억하지
못해 소스 문서를 처음부터 다시 읽고 내용을 통째로 재구성해버렸다 — 검증되지 않은 새
문장(성격 특성 언급 등)까지 섞여 들어갔다.

**그래서 각 단계는 결과물을 대화 응답으로만 주지 않고, 반드시 아래 경로에도 파일로
저장한다.** 재시도 시에는 오케스트레이터가 프롬프트에 전체 텍스트를 다시 붙여넣는 대신,
해당 서브에이전트에게 "이 스테이징 파일을 읽고 그 내용만 수정하라"고 지시한다.

```
tools/blog-automation/.staging/    (gitignore 처리, 사이클마다 내용 덮어씀)
  ├── 01_planner_brief.md   # Step 1 출력
  ├── 02_draft.md           # Step 2 출력 (마크다운 초안)
  ├── 03_review.md          # Step 3 검수 리포트
  ├── 04_verdict.md         # Step 4 마스터 판정
  ├── 05_final.md           # Step 5 편집팀 출력 (title/meta/labels/html_content)
  └── 06_verdict.md         # Step 6 마스터 판정
```

반려로 Step 2(또는 Step 5)를 다시 부를 때는 "`02_draft.md`(또는 `05_final.md`)를 읽고,
`04_verdict.md`(또는 `06_verdict.md`)의 반려 사유만 반영해서 그 파일을 덮어써라 — 지적받지
않은 부분은 그대로 유지하라"고 명시적으로 지시할 것. Step 6도 byte-diff가 필요하면
`02_draft.md`/`05_final.md`를 직접 Read해서 대조할 수 있다.

## 실행 순서 (스케줄 트리거 시 이 순서로 서브에이전트를 호출한다)

```
1. blog-planner 호출
   → skip: true면 여기서 종료 (정상 종료, alerts 기록 안 함)
   → skip: false면 브리프를 들고 2로

2. blog-writer 호출 (기획팀 브리프 전달)
   → 초안 생성

3. blog-reviewer 호출 (초안 + 기획팀 브리프 전달)
   → FAIL이면 반려 사유를 들고 2로 돌아가 재작성 (반려 카운터 +1)
   → PASS면 4로

4. blog-master 호출 (Step 4 — 초안 + 검수 리포트 전달)
   → REJECTED면 반려 사유를 들고 2로 돌아가 재작성 (반려 카운터 +1)
   → APPROVED면 5로

   [반려 카운터가 3에 도달하면: blog-master가
    tools/blog-automation/state/blog_pipeline_alerts.md에 기록하고,
    게시팀에 draft 모드로 Step 7을 위임한다 — 마지막 초안을 Blogger 초안 상태로
    올린 뒤 종료]

5. blog-editor 호출 (승인된 초안 전달)
   → 최종 HTML 패키지 생성

6. blog-master 호출 (Step 6 — 최종 HTML 패키지 전달)
   → REJECTED면 반려 사유를 들고 2로 돌아가 재작성 (반려 카운터 +1, 5도 다시 거침)
   → APPROVED면 7로

7. blog-publisher 호출 (최종 HTML 패키지 전달)
   → 게시 성공: state/blog_last_published_commit.md 갱신, published/에 사본 저장
   → 게시 실패: state/blog_pipeline_alerts.md에 기록, 조용히 종료
   → draft 위임(반려 3회): state/blog_pipeline_alerts.md에만 기록.
     blog_last_published_commit.md는 갱신하지 않는다 — 소재 미소비
```

**반려 카운터는 이번 스케줄 사이클(하나의 파이프라인 실행) 안에서만 유효하다.** 다음
스케줄에는 0부터 다시 시작한다.

## 관련 파일 (리포 내부 상태 — 원격 routine 호환)

디렉토리: `tools/blog-automation/state/` (리포 상대경로)

- `blog_last_published_commit.md` — 마지막으로 소재로 쓴 커밋 해시. 없으면(최초 실행)
  blog-planner가 최근 10개 커밋을 후보로 삼는다.
- `blog_next_material_priority.md` — 사용자가 명시적으로 지정한 우선 소재. 있으면
  blog-planner가 최우선 후보로 고려하고, 소비 후 blog-publisher가 소비 완료 마킹한다.
- `blog_pipeline_alerts.md` — 연속 3회 반려 또는 게시 실패 로그. 사람이 승인해야
  재개되는 게 아니라 기록용이다 — 원하는 시점에 사용자가 열어서 확인할 수 있다.

> **경로 이관 기록 (2026-07-10)**: 이 파일들은 이전에는 사용자 auto-memory 디렉토리
> (`C:\Users\anjyo\.claude\projects\...\memory\`)에 있었으나, 원격(클라우드 routines)
> 실행에서 접근 불가하므로 리포 안으로 이관했다. 로컬 auto-memory에서는 이 경로를
> 가리키는 pointer만 남긴다.

## 로컬 파일

- `credentials/` — OAuth 클라이언트/토큰/블로그 설정. **`.gitignore` 처리됨, 절대 커밋 금지.**
- `scripts/blogger-auth.js` — 최초 1회(또는 재인증 필요 시) 실행하는 OAuth 인증 스크립트.
  사람이 브라우저에서 로그인+허용을 직접 해야 한다.
- `scripts/blogger-client.js` — Blogger API 클라이언트 (`token` / `get-blog` / `post` 명령).
  blog-publisher가 호출한다.
- `published/` — 게시 완료된 글의 로컬 사본 (자격증명이 아니므로 git 커밋 가능).

## Phase 3 스케줄 등록 (아직 안 함)

`schedule` 스킬 또는 `scheduled-tasks` MCP로 위 "실행 순서"를 프롬프트로 등록한다.
주기는 5장 권장대로 초기엔 낮게(주 1회) 잡는다.
