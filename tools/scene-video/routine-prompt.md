# AI_GOAP 영상 대본 파이프라인 (루틴 프롬프트)

블로그 파이프라인(`tools/blog-automation/routine-prompt.md`)과 같은 모양이다. 다른 점은
**산출물이 글이 아니라 씬 JSON 이고, 마지막에 사람이 유튜브에 올린다**는 것이다.

## 이 파이프라인이 하는 일과 안 하는 일

- **한다**: 다음 회차 선정 → 대본(씬 JSON) 작성 → 검수 → 승인 → 음성·렌더·점검 →
  붙여넣을 메타데이터 생성
- **안 한다**: 유튜브 업로드. 감사를 통과하지 않은 API 프로젝트로 올린 영상은 YouTube 가
  비공개로 **잠그고** 스튜디오에서도 못 푼다. 사람이 스튜디오에 끌어다 놓는다.

## 🔴 그림은 회차가 소유한다

`episodes/<ep>/kinds/` 는 그 회차의 소유물이고, 작성팀이 **그 글만을 위해 새로 설계**한다.
공용 그림 라이브러리(`engine/kinds/`)는 **일부러 없앴다** — 있을 때 두 회차가 같은
그림으로 채워졌고, 사용자 판정이 "돌려막기는 내가 원하는 방향이 아니다" 였다.
공유하는 것은 `engine/lib.js`(팔레트·이징·캔버스 헬퍼)뿐 — 그림이 아니라 붓이다.

## 4개 부서 (Agent 도구로 이 순서대로)

| | 에이전트 | 모델 | 산출 |
|---|---|---|---|
| 1 | `scene-planner` | sonnet | `episodes/<ep>/notes/planner.md` |
| 2 | `scene-writer` | opus | `episodes/<ep>/scene.json` + `kinds/*.js` + `notes/writer.md` |
| 3 | `scene-reviewer` | opus | `episodes/<ep>/notes/review.md` (또는 작성팀에 반려) |
| 4 | `scene-master` | opus | `episodes/<ep>/notes/verdict.md` |

기획팀만 sonnet 이다 — 순서표 대조와 본문 추출이라 판단이 들어갈 자리가 없다.
나머지 셋은 대본의 질을 결정하므로 opus.

## 스테이징 파일 규칙

각 서브에이전트는 **별도 무상태 세션**이다. 앞 단계의 결과를 대화로 물려받을 수 없으므로
반드시 `tools/scene-video/episodes/<ep>/notes/` 에 파일로 남기고 다음 단계에 경로를 알려준다.
(블로그 파이프라인이 이 규칙을 어겨 사고가 났던 자리와 같다.)

## 승인 이후 — 기계가 하는 부분

```
node tools/scene-video/publish.mjs <ep> --prepare
```
음성(캐시 재사용) → mp4 렌더 → `check.mjs` 점검 → `episodes/<ep>/build/upload.txt` 생성.
**점검이 실패하면 여기서 멈춘다.** 그 경우 검수팀 단계로 되돌아간다.

## 멈춰야 하는 조건

- 기획팀이 `SKIP` 을 냈다 (원본 글 미발행 / 만들 회차 없음)
- 검수 반려 3회
- 마스터 반려 3회
- `check.mjs` 실패가 두 번 연속 같은 항목

멈출 때는 `episodes/<ep>/notes/` 에 사유를 남긴다. **애매하면 통과시키지 말고 멈춘다** —
영상은 글과 달리 되돌리는 비용이 크다.

## 사람이 이어받는 지점

1. `episodes/<ep>/build/video.mp4` 를 스튜디오(<https://studio.youtube.com>)에 끌어다 놓는다
2. `episodes/<ep>/build/upload.txt` 의 제목·설명·태그를 붙여넣는다
3. 공개로 전환한다

## 주기

로컬 작업 스케줄러 `AI_GOAP scene-video` 가 **격일 09:00**(다음 2026-07-31)에 돈다.
달력이 아니라 "마지막 제작으로부터 며칠"로 게이트하므로 한 번 밀려도 따라잡는다.
블로그(짝수일 13:03)와 겹치지 않는다.
