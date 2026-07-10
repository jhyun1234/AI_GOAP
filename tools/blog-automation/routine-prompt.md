# AI_GOAP 블로그 자동화 파이프라인 (원격 routine 프롬프트)

이 프롬프트는 claude.ai routines가 정기적으로 트리거하는 완전 무인 실행이다. 사람의 승인
이나 확인을 기다리지 않는다. blog-master 에이전트가 이 파이프라인의 유일한 품질 게이트다.

## 환경 전제

- **작업 디렉토리**: 리포 `jhyun1234/AI_GOAP` 루트 (routine이 체크아웃해준 것)
- **자격증명**: env vars `BLOGGER_CLIENT_SECRET`, `BLOGGER_TOKEN`, `BLOG_CONFIG`가 설정되어
  있다. `tools/blog-automation/scripts/blogger-client.js`가 자동으로 이걸 읽는다 — 별도
  파일 생성 불필요.
- **auto-memory 없음**: 사용자 로컬 auto-memory에는 접근할 수 없다. 모든 상태는 리포
  `tools/blog-automation/state/`에서만 읽고 쓴다.

## 핵심 문서 (반드시 먼저 읽을 것)

1. `tools/blog-automation/README.md` — 부서별 역할, 파일 구조, **스테이징 파일 규칙**
2. `tools/blog-automation/state/README.md` — 상태 파일 3종 목적과 read/write 주체
3. `Docs/devlog-workflow.md` "5. SEO 체크리스트" — 각 서브에이전트가 자체 참조하므로
   오케스트레이터는 문서 존재만 각 단계에 전달

## 6개 부서 에이전트

`.claude/agents/blog-*.md`의 6개 (blog-planner, blog-writer, blog-reviewer, blog-editor,
blog-master, blog-publisher). Agent 도구로 이 순서대로 호출.

## 스테이징 파일 (사고 재발 방지 — 반드시 준수)

각 서브에이전트는 별도 무상태 세션이다. 각 단계 결과물을 `tools/blog-automation/.staging/`에
파일로 저장하도록 지시하라(01_planner_brief.md ~ 06_verdict.md — README.md의 정확한 파일명).
**반려로 Step 2나 Step 5를 재호출할 때는 "처음부터 다시 써라"가 아니라 "`.staging/`의 기존
파일을 읽고 반려 사유만 반영해 그 파일을 덮어써라, 지적받지 않은 부분은 한 글자도 바꾸지
마라"고 명시할 것.** 이걸 지키지 않으면 편집 단계에서 전체 내용이 검증 안 된 채로 재구성
되는 사고가 난다(실제로 한 번 발생함).

## 실행 순서

1. **blog-planner** — 이번 회차 소재 선정. `skip: true`면 이번 사이클 조용히 종료.
2. **blog-writer** — 기획팀 브리프로 초안 작성.
3. **blog-reviewer** — 초안 검증.
   - FAIL이면 반려 사유를 blog-writer에게 넘기고 2번 재시도. (반려 카운터 +1)
4. **blog-master** (Step 4) — 1차 검수, 방향/내용 승인.
   - REJECTED면 반려 사유를 blog-writer에게 넘기고 2번 재시도. (반려 카운터 +1)
5. **blog-editor** — SEO 메타 + Blogger HTML 포맷 변환.
6. **blog-master** (Step 6) — 2차 검수, 최종 HTML 승인.
   - REJECTED면 순수 포맷팅 결함이면 blog-editor(5번)만 재호출 가능. 원본 내용이
     바뀌지 않았다면 blog-writer(2번)까지 돌 필요 없음. 스테이징 파일 규칙 준수.
     (반려 카운터 +1)
7. **blog-publisher** — 실제 게시.
   - **`--draft` 플래그 없이 실제 공개 발행.**
   - 대상 블로그: gamedevclaude.blogspot.com (blogId `6014451945015572125`)
   - 성공 시 `tools/blog-automation/state/blog_last_published_commit.md`를 이번 커밋
     해시로 `publish_status: PUBLISHED` 갱신, `tools/blog-automation/published/`에 로컬
     사본 저장.
   - 이번에 소비한 `state/blog_next_material_priority.md`가 있으면 "소비 완료 YYYY-MM-DD"
     마커로 초기화(다음 사이클 중복 방지).

## 반려 카운터 & 안전장치

이번 실행(하나의 스케줄 사이클) 안에서 3번/4번/6번의 반려를 합산 카운트.
**연속 3회 반려되면 즉시 중단**한다. 게시하지 않고, `tools/blog-automation/state/blog_pipeline_alerts.md`
(없으면 새로 생성)에 반려된 소재, 각 반려 사유, 시각을 기록하고 종료. 사람의 승인을
기다리지 않는다 — 다음 사이클에 기획팀이 새 소재로 다시 시작.

Blogger API 게시 자체 실패(쿼터, 인증 오류 등)도 동일하게 `blog_pipeline_alerts.md`에
기록하고 조용히 종료.

## 절대 하지 말 것

- `Assets/`, `ProjectSettings/` 등 게임 코드/에셋 디렉토리 건드리지 않는다. 이 파이프라인은
  `tools/blog-automation/`, `.claude/agents/blog-*.md`, `state/`, `.staging/`, `devlog/`만
  다룬다.
- 자격증명(env vars `BLOGGER_*`, `BLOG_CONFIG`) 값을 로그, 커밋 메시지, 스테이징 파일,
  상태 파일에 그대로 옮겨 적지 않는다.
- 사람에게 승인·확인을 요청하지 않는다 — 완전 무인 설계다.

## 실행 완료 후

성공/실패/스킵 어느 경우든, 이 실행 turn의 마지막에 다음 한 줄을 출력하고 종료한다
(routine 로그 확인용):

```
PIPELINE_RESULT: <PUBLISHED|SKIPPED|REJECTED_3X|API_FAILED>  (블로그 URL 또는 실패 사유)
```
