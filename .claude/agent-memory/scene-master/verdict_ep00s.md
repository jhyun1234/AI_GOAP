---
name: verdict-ep00s
description: ep00s 판정 결과(APPROVED, 누적 반려 1회)와 이 회차에서 확정된 예외 두 가지 — DRAFT 원문·엔진 변경 파급
metadata:
  type: project
---

**ep00s = APPROVED (2026-07-29). 누적 반려 1회** (1차 4건 → 수정 → 2차 통과 → 마스터 승인).
판정서 = `tools/scene-video/episodes/ep00s/notes/verdict-v1.md`. 91.3초 · 10샷 · 자막 27줄.

**Why (이 회차가 남긴 예외 두 가지):**

1. **원문이 `publish_status: DRAFT` 인데 0편 소재로 승인됐다.** 사용자가 명시 지정한 예외다.
   공개 URL 이 없어 `source.url = null` 이 **정상**이고, 영상 설명에 블로그 링크가 안 들어가는 것이 맞다.
   `publish.mjs` 가 `scene.source?.url || ''` 로 처리하므로 링크 대신 **빈 줄이 하나** 들어간다
   (줄을 건너뛰는 게 아니다). 정식 발행되면 `schedule.json.sources.ep00s.url` 과 함께 채운다.
2. **이 회차 때문에 엔진이 바뀌었다** — `bars.js` 도장 자동 축소 + `guideLabel` 하드코딩 해제,
   그에 맞춰 `ep01s.json` 에 `guideLabel` 추가. 작성팀이 이 변경을 보고하지 않아 검수팀이 `git status` 로 발견했다.

**How to apply:**
- **엔진(`engine/`)이 바뀐 회차는 다른 회차 `check.mjs` 도 직접 돌린다.** 씬 파일만 보고 판정하면 파급을 놓친다.
  ep00s 때는 `check.mjs ep01s` 를 재실행해 통과를 확인했다(⚠ 자막 2초 미만 4줄은 TTS 유래 기존 항목, 무관).
- 타임라인·정적 구간을 검증할 때 검수 보고의 표를 베끼지 말고 `build/<ep>.timed.json` 에서 직접 뽑는다.
  실측 dur/pause 가 들어 있어 "첫 10초 안에 수수께끼가 던져지는가"를 초 단위로 확정할 수 있다.
- 다음 회차 재료로 남긴 미해소 항목: `scene.hook` 이 `title.js` 의 `mood:'outro'` 분기에서 렌더되지 않아
  **죽은 필드**라는 것(ep00s·ep01s 공통), 말 속도 6.3자/초가 `voice.json` rule 8 목표(6.5~7.0) 미달.
- 렌더/업로드는 마스터가 하지 않는다 — `publish.mjs --prepare` 는 코디네이터, 유튜브 업로드는 사람.

관련: [[static-segment-exception]]
