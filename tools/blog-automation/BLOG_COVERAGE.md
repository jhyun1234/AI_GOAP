# 블로그 발행 커버리지 대조표 + 주기 점검 절차

*목적: "블로그 글이 개발 순서에 맞게 잘 정리되어 나가고 있는가"를 어느 세션에서든
즉시 점검하기 위한 허브. 사용자가 "블로그 순서/품질 점검해줘"라고 하면 이 파일부터 읽는다.*

## 발행 커버리지 대조표 (발행마다 갱신 — 점검 세션의 1차 화면)

| # | 발행일 | 글 (주제) | 다룬 개발 구간 | 개발 순서상 위치 | 상태 |
|---|---|---|---|---|---|
| 1 | ~2026-07-09 | 방향③ GatherIron 무해 봉합 | ~4df41cc | 舊 아키텍처 후반 | PUBLISHED |
| 2 | 2026-07-11 | F-A 성격배분 + 허기→포만감 반전 | 549444d~b930365 | 舊 아키텍처 말기 | PUBLISHED ([링크](https://gamedevclaude.blogspot.com/2026/07/goap-ai-ai.html)) |
| 3 | 2026-07-14 | M1 개발기 (여가·명령/거부) | M1 트랙 ~cc4602e | **⚠️ 순서 역전** — M0보다 먼저 발행됨 | PUBLISHED ([링크](https://gamedevclaude.blogspot.com/2026/07/unity-goap-ai-ai-m1.html)) |
| 4 | (차기 예약) | **M0 재설계 회고** (24,800→5,977줄) | 8ee845b~b175ddf +04f0975 | 3번의 앞 이야기 (회고 특집으로 복구) | PRIORITY ACTIVE |
| — | 미발행 구간 | 옵시디언 도입·블로그 게이트 전환 등 운영 개선 | f71c0a8, acd0e13 등 | 소재 가치 낮음 — 스킵 허용 | 소비됨 |

## 주기 점검 절차 (점검 세션용 체크리스트)

1. **커버리지 갱신**: `state/blog_last_published_commit.md` 이력과 위 표를 대조 —
   새 발행분을 표에 추가하고, 다룬 커밋 구간을 git log로 확인해 기입.
2. **미발행 구간 스캔**: `devlog/INDEX.md`의 밀스톤 타임라인 대비, 표에 없는 굵직한
   개발 구간(명세 확정·밀스톤 완결·대형 리팩터)이 있는가? 있으면 소재 가치 판단 후
   `state/blog_next_material_priority.md`에 STATUS: ACTIVE로 지정 (게이트 기준 D가 받아줌).
3. **순서 점검**: 발행 순서가 개발 서사 순서와 어긋났는가? (사례: 3번 M1이 M0보다 먼저 —
   회고 특집으로 복구). 역전 발견 시 같은 방법으로 복구 지정.
4. **품질 스팟 체크**: 최신 글 1편을 열어(URL) 사실관계를 실제 커밋·devlog와 대조 —
   과장·오류·미검증 주장 여부. 문제 시 정정 소재로 지정하거나 사용자에게 보고.
5. **파이프라인 건강**: `state/blog_pipeline_alerts.md` 확인 + 직전 run의 PIPELINE_RESULT에
   403/실패 흔적이 있으면 상태 수동 반영 여부 점검 (routine-prompt.md의 MANUAL_STATE_UPDATE 참조).

## 관련 파일 지도

- 발행 이력(기계용 상태): `state/blog_last_published_commit.md`
- 차기 우선 소재 지정: `state/blog_next_material_priority.md` (ACTIVE면 게이트 무조건 통과)
- 파이프라인 정의: `routine-prompt.md` (Step 0 소재 게이트 포함)
- 개발 타임라인 허브: `devlog/INDEX.md` → `devlog/sessions/*.md` (1차 소스)
- 발행 사본: `published/` (원격 push 성공 시에만 존재)
