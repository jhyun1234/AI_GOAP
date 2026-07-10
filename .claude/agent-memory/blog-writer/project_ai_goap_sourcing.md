---
name: project-ai-goap-sourcing
description: AI_GOAP 프로젝트(Unity GOAP 마을 시뮬레이션) 개발일지를 쓸 때 사실을 어디서 확인하는지 — 이 리포는 Bash/grep 없이 Read만으로 취재해야 할 때가 많다.
metadata:
  type: project
---

## 이 프로젝트의 사실 확인 경로

- 프로젝트 규칙/ADR 원문: `C:\Users\anjyo\AI_GOAP\Docs\CLAUDE.md` (주의: 리포 루트가 아니라
  Docs 하위에 있음). ADR 번호, 커밋 전 체크리스트 문구를 여기서 그대로 인용하면 안전하다.
- 구조적 진단/서사 배경: `C:\Users\anjyo\.claude\projects\C--Users-anjyo-AI-GOAP\memory\`
  아래 project_*.md 파일들 — 이건 blog-writer 자체 메모리가 아니라 **사용자의 전체
  세션 메모리(자동 로드되는 MEMORY.md가 가리키는 파일들)**다. 특히
  `project_planning_deadlock_diagnosis.md`에 "결함 A/B/C/D" 같은 프로젝트 내부 용어와
  실전 검증 로그 사례가 정리되어 있어 스토리텔링에 좋은 소재.
- 테스트로 계약 검증: `Assets/Tests/EditMode/M17_PathResultContract.cs` 같은 파일을 직접
  읽으면 커밋 메시지만으로는 안 보이는 정확한 enum 값(Unreachable/AlreadyThere/PathFound)과
  계약 조건을 확인할 수 있다.
- 이 환경의 blog-writer 서브에이전트는 Bash/Grep 도구가 없고 Read/Write/Edit만 가진다.
  grep이 필요한 사실(예: 특정 코드 패턴이 실제로 존재하는지)은 CLAUDE.md나 커밋 메시지에
  이미 인용되어 있는 경우가 많으니 그걸 우선 활용하고, 안 되면 관련 파일을 통째로 Read.

**Why:** 첫 취재 시 Bash 도구가 없다는 걸 모르고 grep을 시도하다 실패 — Read 기반
취재 경로를 미리 알아두면 시간 절약.

**How to apply:** 새 개발일지 요청이 오면 먼저 Docs/CLAUDE.md와 관련 project_*.md
메모리를 Read로 훑어서 소재의 "왜"와 프로젝트 내부 용어(결함 C 같은)를 확보한 뒤 쓴다.
[[feedback-structure-v2]]의 "의외의 디테일" 섹션 재료가 대개 여기서 나온다.
