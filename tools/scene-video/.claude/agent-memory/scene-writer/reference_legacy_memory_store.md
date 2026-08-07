---
name: legacy-memory-store
description: scene-writer 의 기존 메모 대부분은 이 폴더가 아니라 리포 루트 .claude/agent-memory/scene-writer/ 에 있다 — 새 회차를 쓰기 전에 그쪽을 먼저 훑어라
metadata:
  type: reference
---

이 폴더(`tools/scene-video/.claude/agent-memory/scene-writer/`)는 비어 있었고, **실제로
쌓여 있는 메모는 리포 루트의 `C:\Users\anjyo\AI_GOAP\.claude\agent-memory\scene-writer\`**
에 있다(2026-08-07 ep10s-1 작업 중 확인).

거기서 회차 착수 전에 읽을 것:
- `project_video_25s_budget.md` — 25/28초 예산 산수, `SHOT_TAIL`, 예고 상한 3.0초,
  `rel = 말하는 시간 + pauseAfter`, 실측으로 통과한 그릇 표
- `project_split_episode_conventions.md` — 분할 회차 id·`outro.source`·형제 라벨 규약,
  **첫 편은 `siblings` 를 비운다**, 앞 편 `outro.next` 는 지켜야 할 약속
- `feedback_base_shape_first.md` — 「이 글의 기본 도형은 무엇인가」를 먼저 답하고 파생시킨다,
  「안 겹친다」는 항목 대조 뒤에만 적는다
- `feedback_static_segment_is_area.md` — 정적 구간은 거리가 아니라 **면적**, 그리고 `t` 주기는
  등장 게이트의 위상까지 보고 고른다
- `feedback_string_fact_check.md` — 숫자 · 문자열 · **분류** 세 축으로 따로 검사
- `feedback_canvas_edge_text.md` — 캔버스 글자는 `measureText` 로 재서 기준점을 clamp

⚠️ 두 곳에 같은 내용을 복사하지 마라. 새 메모는 이 폴더에 쓰되, 위 파일들을 갱신해야 하면
**루트 쪽 원본을 고친다.**
