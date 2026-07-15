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

## 🔴 OPEN — 원격 sandbox state 브랜치 push 403 (2회 연속)

- **증상**: 원격 auto-run이 게시 성공 후 상태 커밋을 `claude/state-*` 브랜치로 push하면
  GitHub가 403으로 거부. 브랜치가 sandbox 밖으로 나오지 못해
  `blog-state-auto-merge.yml`도 발동하지 않음.
- **발생 이력**:
  - 1회차: 2026-07-14 (M1 발행 회차) → 로컬 수동 반영으로 복구
  - 2회차: 2026-07-15 (`claude/state-2026-07-15T040738Z`, M0 회고 특집 회차) →
    2026-07-15 로컬 수동 반영으로 복구. HTML 사본은 Blogger API GET으로 재획득
    (post_id 6764155466991758383, 13,826 bytes).
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
- **적용한 수정 (2026-07-15, 검증 대기)**:
  1. `routine-prompt.md` Step 8 재설계 — 새 브랜치 생성 금지, **현재 작업 브랜치에 커밋 후
     `git push origin HEAD`** (1차). 실패 시 구방식 claude/state-* push (2차 폴백). 둘 다
     실패 시 MANUAL_STATE_UPDATE에 **push stderr 원문 + 시도 브랜치명 포함** (다음 진단 증거).
  2. `blog-state-auto-merge.yml` — 트리거를 `claude/state-*` → `claude/**`로 확대 (세션
     작업 브랜치 이름을 미리 알 수 없으므로). 상태 파일 외 경로를 건드린 브랜치는 조용히
     스킵, 브랜치 삭제는 claude/state-* 이름일 때만.
- **검증 계획**: 다음 스케줄 run (2026-07-16 13:03 KST)에서 (a) `STATE_PUSH_OK` + main에
  상태 커밋 자동 반영 확인 → 이 항목 CLOSED. (b) 재실패 시 MANUAL_STATE_UPDATE의 stderr
  원문으로 2차 진단 — 그 경우 남은 후보는 GitHub API 직접 쓰기(fine-grained PAT를 env var로
  주입) 또는 상태 저장소를 git 밖(Blogger DRAFT/Google Drive)으로 옮기는 구조 변경.
- **부수 기록**: 2026-07-15 in-sandbox 진단 세션 1회 시도 (trig 임시 생성,
  session `cse_01K1SwR3ms78eGuoLKUm9bSX`) — 보고 채널(Blogger DRAFT) 미도착으로 결과 미회수,
  GitHub 부수효과(브랜치/커밋/이벤트) 전무. 세션 자체가 실행 안 됐거나 조기 실패한 것으로
  추정. 진단 트리거는 삭제 예정.
- **해소 조건**: 원격 auto-run의 state push가 성공해 main 자동 반영까지 통과하는 회차가
  1회 확인되면 이 항목을 CLOSED로 내린다.
