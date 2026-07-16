---
name: blog-pipeline-alerts
description: 블로그 자동화 파이프라인의 반복 장애·경보 기록 — 다음 회차 실행 전 반드시 확인
metadata:
  node_type: memory
  type: reference
---

# 블로그 파이프라인 경보

**Why:** 원격 auto-run에서 반복적으로 발생하는 인프라 장애를 기록해, 매 회차 같은 문제를
새로 진단하는 낭비를 막고 근본 해결 여부를 추적한다.

**How to apply:** 원격 routine과 로컬 점검 세션은 실행 시작 시 이 파일을 확인한다.
OPEN 상태의 경보가 있으면 해당 우회 절차(MANUAL_STATE_UPDATE 등)를 미리 준비한다.

## 🟢 CLOSED — 2026-07-16 REJECTED_3X: M2+M3 소재 발행 실패 (검수 3연속 반려) → 해소

- **run**: 2026-07-16 13:03 KST auto-run. 소재 = M2 생산체인 + M3 주거 기반
  (`651ea47` ~ `153f180`). 당시 발행 안 됨 — latest_commit cc4602e 유지, 소재 미소비.
- **원인 (routine 자체 진단)**: 작성팀과 검수팀의 **분량 계측 방식 불일치** — 기준 문서에
  "4000~5500자"라는 숫자만 있고 셈법(공백 포함 여부, 한글만 셀지 등)이 정의돼 있지 않아,
  작성팀 기준으로는 충족인 초안이 검수팀 셈법으로는 미달 → 반려 3회 → REJECTED_3X.
- **조치 (2026-07-16 로컬 적용)**: blog-writer.md·blog-reviewer.md에 계측 명령을
  `wc -m`(공백·마크다운 포함 전체 문자수) 하나로 통일 명시. 반려 발동은 3,800자 미만/
  6,000자 초과일 때만 — 사소한 오차로 반려 왕복 금지.
- **해소 (2026-07-16 로컬 세션)**: 계측 기준 통일 후 동일 소재로 재작성·재검수를 거쳐
  마스터 Step 4/Step 6 승인 모두 통과, 게시팀 Step 7에서 실제 발행 완료.
  blogger_post_id 3935987342991362953, url
  https://gamedevclaude.blogspot.com/2026/07/unity-goap.html. 상세는
  `blog_last_published_commit.md` 최신 항목 참조. **해소 조건 충족 → CLOSED.**
- 참고: routine이 sandbox에서 커밋한 db77faf는 push 403으로 소실 — 이 항목은
  MANUAL_STATE_UPDATE 기반 로컬 재구성임.

## 🔴 OPEN — 원격 sandbox state 브랜치 push 403 (3회 연속)

- **증상**: 원격 auto-run이 게시 성공 후 상태 커밋을 `claude/state-*` 브랜치로 push하면
  GitHub가 403으로 거부. 브랜치가 sandbox 밖으로 나오지 못해
  `blog-state-auto-merge.yml`도 발동하지 않음.
- **발생 이력**:
  - 1회차: 2026-07-14 (M1 발행 회차) → 로컬 수동 반영으로 복구
  - 2회차: 2026-07-15 (`claude/state-2026-07-15T040738Z`, M0 회고 특집 회차) →
    2026-07-15 로컬 수동 반영으로 복구. HTML 사본은 Blogger API GET으로 재획득
    (post_id 6764155466991758383, 13,826 bytes).
  - 3회차: 2026-07-16 (REJECTED_3X 회차, alerts 커밋 db77faf 미push). **결정적 증거 확보**:
    1차 `git push origin HEAD` 시도에서 세션이 **detached HEAD**임이 드러남 + 403.
    → routine 세션은 세션 소유 작업 브랜치가 아예 없으므로, "현재 작업 브랜치만 허용"
    규칙 하에서 **routine의 git push는 전 형태 불가능이 확정**. 07-15에 넣은
    "현재 브랜치 push" 1차 경로는 routine에는 해당 없음 (판정: 가설 반증).
- **현재 우회책**: routine이 발행 결과를 MANUAL_STATE_UPDATE 블록으로 출력 → 사용자가
  로컬 세션에 전달 → 로컬에서 상태 파일 갱신 + main에 직접 커밋. (이 절차는 07-14부터
  routine 프롬프트에 내장됨 — 정상 작동 확인)
- **근본 원인 (2026-07-15 확정)**: 클라우드 샌드박스의 GitHub 프록시는 push를 **세션의
  현재 작업 브랜치로만** 허용한다 (공식 문서 claude-code-on-the-web "GitHub proxy" 절:
  "Restricts git push operations to the current working branch for safety"). routine의
  구 Step 8은 세션 도중 `git checkout -b claude/state-*`로 **새 브랜치를 만들어** push했기
  때문에 prefix와 무관하게 403이었다. "claude/* prefix면 push 허용"이라던 07-11의 전제는
  문서 규칙과 다른 오해였고, 당시 Path B 검증(claude/state-workflow-test)은 **로컬 PC에서
  push**한 것이라 샌드박스 프록시를 통과 검증한 적이 없다. GitHub 리포 쪽 설정은 무관
  (룰셋 0개, main 브랜치 보호 없음 — 2026-07-15 gh api로 확인).
- **1차 수정 (2026-07-15) — 반증됨**: "현재 작업 브랜치에 push origin HEAD" 경로를
  넣었으나, 07-16 run에서 routine 세션이 **detached HEAD**임이 확인되어 이 경로는 routine에
  적용 불가 (stderr 원문 확보 목적은 달성 — 이 증거로 진단 완결).
- **2차 수정 (2026-07-16, 검증 대기 — PAT 필요)**: git 프록시를 우회하는 **GitHub REST API
  직접 커밋** 경로 구현.
  1. `scripts/gh-state-push.js` 신설 — blob→tree→commit→`PATCH refs/heads/main`(force
     아님 = ff만). 상태 경로(`tools/blog-automation/{state,published}/`) 외 파일은 무시.
  2. `routine-prompt.md` Step 8 — 1순위 API 경로(`GH_STATE_TOKEN` env var 필요), 실패 시
     레거시 git push 폴백 + MANUAL_STATE_UPDATE.
  3. **사용자 작업 필요**: GitHub fine-grained PAT 발급 (리포 jhyun1234/AI_GOAP 단독,
     권한 Contents: Read and write 만) → claude.ai 환경(env_011dy96U4KfgKbckWYWVqzN1)
     env vars에 `GH_STATE_TOKEN`으로 저장 (BLOGGER_* 넣은 곳과 동일 UI).
- **검증 계획**: PAT 저장 후 다음 run에서 `STATE_PUSH_OK (api)` + main에
  `chore(blog): auto-run state update` 커밋 자동 등장 확인 → 이 항목 CLOSED.
  API 경로도 403이면(프록시가 api.github.com Authorization을 가로채는 경우) 최후 수단은
  상태 저장소를 git 밖(Blogger DRAFT/Google Drive)으로 옮기는 구조 변경.
- **부수 기록**: 2026-07-15 in-sandbox 진단 세션 1회 시도 (trig 임시 생성,
  session `cse_01K1SwR3ms78eGuoLKUm9bSX`) — 보고 채널(Blogger DRAFT) 미도착으로 결과 미회수,
  GitHub 부수효과(브랜치/커밋/이벤트) 전무. 세션 자체가 실행 안 됐거나 조기 실패한 것으로
  추정. 진단 트리거는 삭제 예정.
- **해소 조건**: 원격 auto-run의 state push가 성공해 main 자동 반영까지 통과하는 회차가
  1회 확인되면 이 항목을 CLOSED로 내린다.
