---
name: dnns-source-keeps-growing
description: (포인터) dNNs 원문이 쓰는 동안에도 자란다는 메모의 정본은 루트 저장소에 있다 — .claude/agent-memory/scene-writer/project_dnns_source_keeps_growing.md
metadata:
  type: reference
---

🔴 **이 메모의 정본은 루트 저장소다** —
`C:\Users\anjyo\AI_GOAP\.claude\agent-memory\scene-writer\project_dnns_source_keeps_growing.md`

여기 두었다가 저장소가 둘(루트 / `tools/scene-video/`)이라는 것을 뒤늦게 확인해 옮겼다.
**고칠 일이 생기면 루트 쪽을 고쳐라.** 요지만 남긴다:

- dNNs 의 원문(세션 로그)은 **회차를 쓰는 동안에도 append 된다.** 사건 커밋 범위 **밖**의
  뒤쪽 커밋이 같은 수치를 갈아 놓는다(`d01s` 실측: 시야 10→7 · 부화 15→10 · 회수 20→14 ·
  게이트 477→479). 수치 이름으로 **원문 전체를 grep** 하고, 형제 편에게 보고하라.
- dNNs 의 `outro.source` 는 **날짜**다(「2026-08-11 개발 로그」). 「M26-2차 W5R」류 내부 작업
  번호는 `ADR-V-10` 예외 밖이라 반려된다.
- 낱말 「전수」는 자막·캔버스로 끝나지 않는다 — **캔버스 밖 여섯 층**(`hud.title`·`hud.outro`·
  `scene.hook`·`outro.source`+`part`·`outro.next`·`hud.aiHook`)까지가 전수다.

관련: [[sfx-uniqueness-is-the-arg-tuple]] · [[length-grail-beats-estimate]]
