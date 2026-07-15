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
- **근본 해결 후보** (다음 점검 세션 안건):
  1. routine 환경의 git 자격증명이 push 권한을 갖는지 확인 (403 = 인증은 되나 권한 없음)
  2. GitHub fine-grained token의 Contents write 권한 / 브랜치 보호 규칙 점검
  3. push 대신 GitHub API(gh api)로 커밋 생성하는 대안 검토
- **해소 조건**: 원격 auto-run의 state push가 성공해 auto-merge까지 통과하는 회차가
  1회 확인되면 이 항목을 CLOSED로 내린다.
