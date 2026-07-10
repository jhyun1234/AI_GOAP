# 블로그 자동화 파이프라인 상태 파일

이 디렉토리는 블로그 자동화 파이프라인이 사이클 사이에 유지해야 하는 상태를 담는다.
**단일 진리(source of truth)** — 이전에는 사용자의 auto-memory에 있었으나 원격 routine에서
접근 불가하므로 리포 안으로 이관했다(2026-07-10).

## 파일

- `blog_last_published_commit.md` — 마지막으로 소재로 사용한 커밋 해시 + 게시 상태.
  기획팀(`blog-planner`)이 새 사이클 시작 시 이 파일을 읽어 중복 소재를 방지하고,
  게시팀(`blog-publisher`)이 게시 성공 시 이 파일을 갱신한다.
- `blog_next_material_priority.md` — 사용자가 명시적으로 지정한 다음 회차 우선 소재.
  기획팀이 새 사이클 시작 시 이 파일이 있으면 우선 후보로 고려하고, 소비 후 이 파일을
  삭제한다(빈 파일로 두거나 "소비 완료" 마킹).
- `blog_pipeline_alerts.md` — (필요 시 생성) 연속 3회 반려 또는 게시 실패 로그.
  자동 재개되지 않으므로 사람이 열어서 확인하는 용도.

## 갱신 주체

| 파일 | 읽는 에이전트 | 쓰는 에이전트 |
|---|---|---|
| `blog_last_published_commit.md` | blog-planner | blog-publisher |
| `blog_next_material_priority.md` | blog-planner | 사람(수동) |
| `blog_pipeline_alerts.md` | 사람(수동 확인) | blog-master, blog-publisher |

## 원격/로컬 공존

로컬 auto-memory MEMORY.md에는 이 디렉토리로 향하는 pointer만 두고, 실제 데이터는
여기서만 관리한다. 원격 routine은 리포를 체크아웃해 이 경로를 그대로 읽고 쓴다.
