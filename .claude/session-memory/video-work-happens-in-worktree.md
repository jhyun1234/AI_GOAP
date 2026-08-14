---
name: video-work-happens-in-worktree
description: 유튜브 쇼츠/롱폼 영상 제작은 반드시 C:\Users\anjyo\AI_GOAP-video 워크트리에서 한다 (main 체크아웃은 다른 세션이 쓴다)
metadata: 
  node_type: memory
  type: project
  originSessionId: 5ee97eee-b32a-4439-84b4-332307208fa7
  modified: 2026-08-12T05:01:55.106Z
---

영상 제작(scene-video 파이프라인) 작업은 전부 `C:\Users\anjyo\AI_GOAP-video`
(브랜치 `exp/palette4`)에서 한다. 2026-08-12 사용자 지시.

`C:\Users\anjyo\AI_GOAP` (main) 은 **다른 세션이 동시에 쓰고 있다** — Unity/맵 작업
쪽에서 브랜치를 갈아타며 커밋한다.

**Why:** main 체크아웃에서 영상 파일을 고치면 그 세션의 `git checkout` / `reset` 에
통째로 원복된다. 2026-08-12 에 실제로 두 번 났다 — `engine/lib.js`·`engine.js`·
`check.mjs`·`kinds/*.js`·`scene.json` 이 전부 옛 내용으로 돌아갔고, git 이 추적하지
않는 `build/` 의 mp4 만 살아남아서 한동안 눈치채지 못했다. reflog 에
`checkout: moving from main to de91beab` → `reset: moving to HEAD` 가 찍혀 있었다.

**How to apply:** 영상 관련 편집·렌더·검사는 `cd C:/Users/anjyo/AI_GOAP-video` 에서
시작한다. 산출물 미러는 그대로 `D:\AI_GOAP-videos` 다(publish.mjs 가 robocopy).
main 쪽 같은 파일을 고쳤다면 그건 버리고 워크트리에서 다시 해라.
관련: [[scene-video-pipeline-layout]]
