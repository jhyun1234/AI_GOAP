# 블로그 발행 커버리지 대조표 + 주기 점검 절차

*목적: "블로그 글이 개발 순서에 맞게 잘 정리되어 나가고 있는가"를 어느 세션에서든
즉시 점검하기 위한 허브. 사용자가 "블로그 순서/품질 점검해줘"라고 하면 이 파일부터 읽는다.*

**마지막 사전점검**: 2026-07-20 (자동 스케줄, 13:03 발행 1시간 전 정상 실행). 07-19
사전점검 이후 새로 발행된 글 없음 — `blog_last_published_commit.md` latest_commit은
여전히 `7c0dd7f`(M6). `blog_next_material_priority.md`가 `STATUS: ACTIVE`로 **M7(상호대화와
가시성, `a451435`~`08f7f9b`)**을 유지 중이며, 소재 게이트 기준 D(사용자 지정 소재
ACTIVE)가 커밋 수와 무관하게 무조건 통과시키므로 오늘 13:03 회차는 이 M7 소재로 발행될
전망 — 이번 점검에서 추가 교정 불필요. 순서 점검: M6(발행 완료) → M7(다음, ACTIVE) →
M8(완료, 대기) → M9(진행 중, 미완결) — 역전 없음. 사실관계 스팟 체크: 최신 발행 글(M6)은
07-19 사전점검에서 이미 검증 완료(과장·오류 없음, 아래 참조)이고 그 이후 새 발행이 없어
재점검 대상 없음. 파이프라인 경보 2건 모두 CLOSED 유지, 재발 없음 — 건강.

참고로 로컬 작업 브랜치에는 M9 트랙 커밋 다수(M9-A~J, 최신 `5f3de87` M9-E 대화 릴레이)가
아직 origin/main에 push되지 않은 상태다 — 원격 파이프라인은 origin/main만 읽으므로 이
구간은 push 전까지 소재 후보에 잡히지 않는다(정상, 교정 불필요. M9는 위 표에서도 이미
"진행 중·미완결"로 대기 표시되어 있음).

### 이전 사전점검 이력 (2026-07-19, 기록 보존용)

**마지막 사전점검**: 2026-07-19 (자동 스케줄, 13:03 발행 1시간 전 실행 예정이었으나 세션
지연으로 발행 이후 실행됨). 점검 시작 시점에 이미 **M6 글이 오늘 13:03 회차에서 발행
완료**되어 있었음(`blog_last_published_commit.md` latest_commit이 `7c0dd7f`로 갱신,
`blogger_post_id` 4301273701134180743, `blog_next_material_priority.md`가 이미
`STATUS: CONSUMED`) — 07-18 사전점검이 안전망으로 지정한 M6 ACTIVE가 정확히 적중해 순서
역전 없이 발행됨. 게시팀 Step 8이 이미 다음 소재를 **M7(상호대화와 가시성)**으로
`STATUS: ACTIVE` 지정해 둔 상태라 이번 점검에서 추가 교정 불필요.

**#8(M6 예고된 겨울과 설득) 글 사실관계 스팟 체크 완료**(로컬 아카이브
`published/2026-07-19-unity-goap-m6-season-winter-crisis.html` vs
`devlog/sessions/2026-07-17.md` 대조) — 겨울 수지 562(필요) vs 1,110(보유) 2배 잉여,
개체 편차 붕괴 포만 81.6~81.7→FNV-1a 수정 후 62.6~82.8, 겨울 3→4일·감쇠 1.5→1.75×·
주민 5→10명 재조정, GoalBoosts 성격별 위기 대응 분화(농사꾼45~고집쟁이5) 전부 세션
로그·커밋(a0e318b, 44b7538, 7c0dd7f)과 일치, 과장·오류 없음. 실제 소비 커밋 범위는
`aa795e6`~`7c0dd7f`로, 기존 예측 범위(`~80cac26`)보다 위기감 재조정·성격 분화 2커밋
더 포함됨(같은 세션의 M6 결말부로 기획팀이 보정).

**다음 회차(M7) 예측**: `blog_next_material_priority.md`가 이미 M7(상호대화와 가시성,
`a451435`~`08f7f9b`, 1차 소스 `devlog/sessions/2026-07-17.md` `[04:40]` 이후)을 ACTIVE로
지정해 두어 다음 13:03 회차는 이 소재가 나갈 전망. M8(관계·소유·부탁, 완료)·M9(공간·재해,
A~J 다수 항목 진행 중·미완결)은 그 다음 후보로 관찰 계속.

파이프라인 경보: 403/REJECTED_3X 둘 다 CLOSED 유지, 재발 없음 — 건강. 이번 회차 발행
과정에서도 중복 게시 등 이상 없음(M5 회차 때 있었던 중복 게시 사고 재발 없음).

## 발행 커버리지 대조표 (발행마다 갱신 — 점검 세션의 1차 화면)

| # | 발행일 | 글 (주제) | 다룬 개발 구간 | 개발 순서상 위치 | 상태 |
|---|---|---|---|---|---|
| 1 | ~2026-07-09 | 방향③ GatherIron 무해 봉합 | ~4df41cc | 舊 아키텍처 후반 | PUBLISHED |
| 2 | 2026-07-11 | F-A 성격배분 + 허기→포만감 반전 | 549444d~b930365 | 舊 아키텍처 말기 | PUBLISHED ([링크](https://gamedevclaude.blogspot.com/2026/07/goap-ai-ai.html)) |
| 3 | 2026-07-14 | M1 개발기 (여가·명령/거부) | M1 트랙 ~cc4602e | **⚠️ 순서 역전** — M0보다 먼저 발행됨 | PUBLISHED ([링크](https://gamedevclaude.blogspot.com/2026/07/unity-goap-ai-ai-m1.html)) |
| 4 | 2026-07-15 | **M0 재설계 회고** (24,800→5,977줄) | 8ee845b~b175ddf +04f0975 | 3번의 앞 이야기 (회고 특집으로 복구) | PUBLISHED ([링크](https://gamedevclaude.blogspot.com/2026/07/unity-goap-24800-5977-m0.html)) |
| — | 미발행 구간 | 옵시디언 도입·블로그 게이트 전환 등 운영 개선 | f71c0a8, acd0e13 등 | 소재 가치 낮음 — 스킵 허용 | 소비됨 |
| 5 | 2026-07-16 | **M2 생산체인(농사·요리) + M3 집과 기틀** — 밭·부엌·앵커 승격 체인, 집/통행차단/작업클레임 | 651ea47~153f180 | 4번(M0) 다음 소재 — 두 밀스톤 다 spec+feat+test 완결, 이야깃거리 충분 | PUBLISHED ([링크](https://gamedevclaude.blogspot.com/2026/07/unity-goap.html)) |
| 6 | 2026-07-17 | **M4 주민의 성격(아키타입 재편입)** — PersonalitySO·비용 배율·명령 거부·혼잣말·5번째 아키타입 + JPS 버그 발견 | b982090~862de3b | 5번(M2+M3) 다음 소재 | PUBLISHED ([링크](https://gamedevclaude.blogspot.com/2026/07/unity-goap_0950458123.html)) |
| 7 | 2026-07-18 | **M5 직업과 일과** — JobSO·실효 우선순위·일과 주입(농부/탐험가)·직업 에셋 5종·목수 리허설(.cs 0) + JPS 대각 버그 | fd6b1db~87d7d7f | 6번(M4) 다음 소재 | PUBLISHED ([링크](https://gamedevclaude.blogspot.com/2026/07/unity-goap-ai.html)) |
| 8 | 2026-07-19 | **M6 예고된 겨울 + 보상 재설득** — 계절 선반·굶주림 이탈·보상 재설득·가을 리허설(.cs 0)·개체 편차 붕괴 수정·위기감 재조정·성격별 위기 대응 분화(GoalBoosts) | aa795e6~7c0dd7f | 7번(M5) 다음 소재 | PUBLISHED ([링크](https://gamedevclaude.blogspot.com/2026/07/claude-code-unity-0.html)) |
| — | (차기 예약) | **M7 상호대화와 가시성** — 이름표·정보줄·대사 확장·주민 반응 대화 | a451435~08f7f9b | 8번(M6) 다음 소재 | PRIORITY ACTIVE — 2026-07-19 게시팀 Step 8 지정 (`blog_next_material_priority.md` 참조) |
| — | 미발행 구간 (완료, 대기) | **M8 관계·소유·부탁** — 관계 축·소유 축(내 집)·부탁 선반·보상 태도(떼먹기/선불) | 68c6ef8~66f690b | M7 다음 소재 | 대기 |
| — | 미발행 구간 (진행 중) | **M9 공간·재해** — 구역 선반·파괴 문·재해 선반·FoodDaysLeft·식량 goal 개편 등 A~J 다수 항목 | (진행 중, 미완결) | M8 다음 소재 후보 — 완결 후 관찰 | 대기 (미완결) |

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
