---
name: video-script-numbers
description: "쇼츠 대본의 숫자 — 자막은 아라비아 숫자, TTS 는 100 이상 한자어(백팔십, 천백팔)"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 448da9ce-2a2e-4d21-ac68-ea6c5d519a19
  modified: 2026-08-13T00:55:56.733Z
---

2026-08-13 사용자 지시. `scene.json` 의 한 줄은 세 칸이고 칸마다 숫자 표기가 다르다.

- `text`(화면 자막) · `en`(영어 자막) → **아라비아 숫자**. 「서른」이 아니라 `30`,
  「백여든」이 아니라 `180`.
- `say`(TTS 가 읽는 말) → 한글. **100 이상은 전부 한자어**로 읽는다.
  180 = 백팔십 · 110 = 백십 · 120 = 백이십 · 1108 = 천백팔.
  고유어(여든·아흔·쉰)를 백 단위에 섞지 마라. 99 이하는 고유어가 자연스러우면 그대로.

**Why:** 자막에서 한글 수사는 읽는 데 시간이 더 걸리고 쇼츠에서는 숫자가 눈에 먼저 든다.
「백여든」은 읽는 사람이 한 박자 멈춘다.

**How to apply:** `say` 를 한 글자라도 고치면 `tts.mjs` 를 다시 돌려야 하고 **그 샷의
비트 길이가 바뀐다.** ep15s-1 에서 「백여든→백팔십」 하나로 S3 비트가 1.499 → 1.509 초가
되어 홀드 상한 1.5 를 넘겼고, 그 줄의 `pauseAfter` 를 100ms 줄여 되돌렸다.
대본을 고쳤으면 렌더 스크립트의 비트 단언을 다시 봐라.
정본은 `tools/scene-video/notes/2026-08-13-새-회차-runbook.md` §3-①.

관련: [[video-3d-pipeline-state]] · [[video-work-happens-in-worktree]]
