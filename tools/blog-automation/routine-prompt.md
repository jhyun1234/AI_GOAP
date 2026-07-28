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

0. **소재 게이트 (오케스트레이터 직접 — 에이전트 호출 전, 2026-07-14 도입)**
   이 routine은 격일(짝수 날짜, 2026-07-22 사용자 지시 — 개발 속도보다 발행이 빨라
   소재 고갈 방지. 주기는 이후에도 조정될 수 있음)로 트리거되지만 **발행은 소재가
   충분할 때만** 한다. 게이트는
   git 명령 몇 개로만 판단해 미달 시 에이전트를 하나도 부르지 않는다 (토큰 절약).
   ```bash
   # 상태 파일 형식: - `latest_commit`: `b930365` (...) — 첫 항목이 최신
   LAST=$(grep -oP '`latest_commit`:\s*`\K[0-9a-f]+' tools/blog-automation/state/blog_last_published_commit.md | head -1)
   # 소재 후보: 마지막 소비 커밋 이후, 파이프라인 자기 커밋(chore(blog)) 제외
   COMMITS=$(git log --oneline "$LAST"..HEAD | grep -cv "chore(blog)")
   BIG=$(git log --oneline "$LAST"..HEAD | grep -cE "^\w+ (spec|refactor)\(")
   DAYS_SINCE=$(( ( $(date +%s) - $(git log -1 --format=%ct "$LAST") ) / 86400 ))
   # 무결성 가드 (2026-07-28 추가): 최신 항목이 DRAFT면 소재는 소비되지 않은 것이다
   DRAFTED_STATE=$(grep -oP '`publish_status`:\s*\**\K[A-Z]+' tools/blog-automation/state/blog_last_published_commit.md | head -1)
   ```
   **⚠️ `DRAFTED_STATE`가 `DRAFT`면 상태 파일이 깨진 것이다.** draft 게시는 소재를 소비하지
   않으므로 게시팀이 `latest_commit`을 갱신했어야 할 이유가 없다(`blog-publisher.md` Step 7).
   이 경우 **게이트를 무조건 통과시키고**, `state/blog_pipeline_alerts.md`에 "draft 회차가
   latest_commit을 갱신함 — 지시서 위반" 경보를 기록한 뒤, 기획팀에게 **직전 회차 소재는
   미소비이므로 재집필 후보로 계속 유효하다**고 전달한다. 조용히 건너뛰면 그 소재는 영구히
   사라진다 (2026-07-28 실제 발생).
   **통과 기준 (하나라도 충족 시 Step 1 진행):**
   - A. 미소비 커밋 ≥ 10 (활발한 개발 — 하루치 대형 세션)
   - B. 대형 이벤트(spec/refactor 커밋) ≥ 1 이고 미소비 커밋 ≥ 5 (명세 확정·재설계 등 이야깃거리)
   - C. 마지막 소비 후 5일 이상 경과 이고 미소비 커밋 ≥ 3 (블로그 공백 방지 백스톱)
   - D. `state/blog_next_material_priority.md`의 **최상단 항목**이 **STATUS: ACTIVE**
     (사용자 지정 소재 대기 중 — 커밋 수와 무관하게 무조건 통과). 판정 대상은 최상단
     항목뿐이다 — 파일 아래쪽 이력 섹션의 옛 마커는 무시한다. 또한 **정의된 상태값은
     `ACTIVE` / `CONSUMED` / 이력 표기 셋뿐이다.** 그 밖의 값(예: 07-28 run이 임의로 쓴
     `DRAFT_PENDING`)을 발견하면 새 상태값을 발명하지 말고, 소재가 실제로 공개 발행됐는지
     기준으로 `ACTIVE`(미발행) 또는 `CONSUMED`(발행 완료) 중 하나로 판정한 뒤 진행한다.

   미달이면 즉시 종료: `PIPELINE_RESULT: SKIPPED (게이트 미달 — 커밋 N개, 대형 M개, 경과 D일)`
   발행 상한은 cron이 일 1회이므로 자동으로 하루 최대 1편이다.

1. **blog-planner** — 이번 회차 소재 선정. `skip: true`면 이번 사이클 조용히 종료.
   (게이트는 "양" 판단, 기획팀은 "글이 되는가" 판단 — 이중 필터는 의도된 설계다.)
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
   - **"정상 경로는 공개 발행, 마스터가 draft 위임 시에만 --draft"**
   - 대상 블로그: gamedevclaude.blogspot.com (blogId `6014451945015572125`)
   - 성공 시 `tools/blog-automation/state/blog_last_published_commit.md`를 이번 커밋
     해시로 `publish_status: PUBLISHED` 갱신, `tools/blog-automation/published/`에 로컬
     사본 저장.
   - 이번에 소비한 `state/blog_next_material_priority.md`가 있으면 "소비 완료 YYYY-MM-DD"
     마커로 초기화(다음 사이클 중복 방지).
   - 🚫 **draft 위임(아래 반려 카운터 경로)일 때는 위 두 줄을 수행하지 않는다.**
     `blog_last_published_commit.md`를 **어떤 형태로도 갱신하지 않고**(`publish_status:
     DRAFT`를 적는 것도 갱신이다), `blog_next_material_priority.md`도 `ACTIVE`로 남긴다 —
     draft는 소재를 소비한 것이 아니다. 기록은 `blog_pipeline_alerts.md`에만 남긴다.
     (`blog-publisher.md` Step 7과 동일 규정. 2026-07-28에 이 규정이 지켜지지 않아
     M10 소재가 소실될 뻔했다.)
   - **기획팀 브리프에 `deferred_milestones`가 있으면** (미발행 밀스톤이 더 남아 있다는
     뜻 — 한 회차 = 한 밀스톤 규칙), 그중 **첫 번째 밀스톤을
     `state/blog_next_material_priority.md`에 STATUS: ACTIVE로 새로 지정**한다 (커밋 범위와
     1차 소스 세션 로그 명시). 다음 회차가 순서대로 이어 쓰게 하는 장치다.

8. **상태 커밋 + push (orchestrator가 직접 실행, 서브에이전트 아님)** — publisher 성공 후
   상태 파일 변경을 리포에 반영한다.

   **⚠️ 샌드박스 push 규칙 (2026-07-16 최종 확정)**: routine 세션은 **detached HEAD**로
   체크아웃되고, 샌드박스 GitHub 프록시는 push를 "세션의 현재 작업 브랜치"로만 허용하므로
   routine에서는 **어떤 형태의 git push도 403이다** (2026-07-14/15/16 3회 실증 — 새
   claude/* 브랜치·HEAD push 모두 실패). 따라서 **1순위는 git 프록시를 우회하는 GitHub
   REST API 직접 커밋**이다 (env var `GH_STATE_TOKEN` 필요 — fine-grained PAT, 이 리포
   단독, Contents Read/Write).
   ```bash
   git add tools/blog-automation/state/ tools/blog-automation/published/
   # 1차: GitHub REST API로 main에 직접 커밋 (git 프록시 우회, GH_STATE_TOKEN 필요)
   if node tools/blog-automation/scripts/gh-state-push.js \
        "chore(blog): auto-run state update ($(date -u +%Y-%m-%d))" 2> /tmp/push-err-1.txt; then
     echo "STATE_PUSH_OK (api)"
   else
     cat /tmp/push-err-1.txt
     # 2차(레거시 — 프록시 정책이 바뀐 경우에만 성공 가능): git push 시도
     git config user.email "blog-automation@aigoap.local"
     git config user.name "aigoap-blog-automation"
     git commit -m "chore(blog): auto-run state update ($(date -u +%Y-%m-%d))" || echo "no local commit"
     if git push origin HEAD 2> /tmp/push-err-2.txt; then
       echo "STATE_PUSH_OK (git HEAD)"
     else
       cat /tmp/push-err-2.txt
       echo "STATE_PUSH_FAILED"
     fi
   fi
   ```
   두 시도 모두 실패 시 `state/blog_pipeline_alerts.md`에 기록하되 파이프라인은
   PUBLISHED로 종료(Blogger 발행 자체는 이미 성공했으므로). 상태 파일 반영 실패는
   다음 auto-run이 중복 소재를 볼 위험만 있고 이번 사이클 자체는 성공이다.
   **단, push 실패 시 최종 PIPELINE_RESULT 줄 다음에 수동 반영용 블록을 반드시 출력한다**
   (2026-07-14 403 사고에서 URL만 출력되어 소비 커밋을 추정해야 했음). **push 실패 원문
   (stderr 전체, /tmp/push-err-*.txt 내용)과 시도한 브랜치명을 반드시 포함한다** — 로컬
   세션이 근본 원인을 추적할 유일한 증거다:
   ```
   MANUAL_STATE_UPDATE:
   latest_commit: <이번에 소비한 마지막 커밋 해시와 제목>
   blog_url / title / labels / blogger_post_id
   next_material_priority 소비 여부
   push_attempts: <브랜치명 → stderr 원문 (1차/2차 각각)>
   ```

   **API_FAILED / REJECTED_3X / DRAFTED 경로에서도** 동일한 push 절차(현재 브랜치 우선,
   실패 시 claude/state-* 폴백)를 따른다 (main이 현재 브랜치가 아닐 때 main 직접 push 시도
   금지). **DRAFTED 경로에서 반영 대상은 `state/blog_pipeline_alerts.md`와
   `state/incidents/` 스냅샷뿐이다** — `blog_last_published_commit.md`·
   `blog_next_material_priority.md`는 애초에 수정되지 않아야 하므로, 이 둘이 변경된 채
   나타나면 위 7번의 draft 금지 규정을 어긴 것이니 되돌린 뒤 push한다.

## 반려 카운터 & 안전장치

이번 실행(하나의 스케줄 사이클) 안에서 3번/4번/6번의 반려를 합산 카운트.
**연속 3회 반려되면 재작성 루프를 중단**한다. 단 글을 버리지 않는다 —
`tools/blog-automation/state/blog_pipeline_alerts.md`(없으면 새로 생성)에 반려된 소재,
각 반려 사유, 시각을 기록한 뒤, **7번 blog-publisher를 `--draft` 모드로 호출해**
마지막 초안을 Blogger 초안 상태로 올리고 `PIPELINE_RESULT: DRAFTED`로 종료한다.
draft 게시까지 실패하면 그때만 `REJECTED_3X`로 종료. 사람의 승인은 기다리지 않는다.

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
PIPELINE_RESULT: <PUBLISHED|SKIPPED|DRAFTED|REJECTED_3X|API_FAILED>  (블로그 URL 또는 실패 사유)
```

각 값의 뜻:

```
PUBLISHED    정상 승인 경로 완주 → 공개 발행
SKIPPED      소재 게이트 미달 또는 기획팀 skip:true (정상 종료, 소재 미소비)
DRAFTED      반려 3회 → 마지막 초안을 Blogger 초안으로 게시 (소재 미소비, 재집필 대상)
REJECTED_3X  반려 3회 + draft 게시까지 실패 (소재 미소비)
API_FAILED   Blogger API 게시 실패 (쿼터·인증 등)
```
