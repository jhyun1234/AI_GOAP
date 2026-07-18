# 블로그 발행 커버리지 대조표 + 주기 점검 절차

*목적: "블로그 글이 개발 순서에 맞게 잘 정리되어 나가고 있는가"를 어느 세션에서든
즉시 점검하기 위한 허브. 사용자가 "블로그 순서/품질 점검해줘"라고 하면 이 파일부터 읽는다.*

**마지막 사전점검**: 2026-07-18 (자동 스케줄, 13:03 발행 1시간 전 실행). 이번 점검
시작 시점에 이미 M5 글이 오늘 새벽(04:29 KST, 로컬 세션에서) 발행 완료되어 있었음
(`state/blog_last_published_commit.md` latest_commit 갱신 커밋 4c4dca0,
`git pull`로 이번 점검 세션에 반영됨) — **오늘 13:03 원격 routine 발행 이전의 회차**로
`blog_next_material_priority.md`가 이미 `STATUS: CONSUMED`. #7(M5 직업과 일과) 글
사실관계 스팟 체크 완료 — JobSO/실효 우선순위 방향 전환/농부·탐험가 일과/직업 5종/목수
.cs 0/JPS 대각 버그(같은 날 07-16 02:41 오전 수정→14:32 오후 재발견, 커밋 c03d461→eac0907
시각 대조로 확인) 전부 세션 로그·커밋과 일치, 오류 없음.

**오늘 13:03 소재 예측 및 선제 교정**: `blog-planner.md`가 어제(07-18) 밤 커밋 세션에서
**날짜 커트라인 → 커밋 해시(`latest_commit` 이후) 기준**으로 근본 수정됨(`36d57c2`,
`b53d52d`) — 이번이 그 새 로직의 **첫 실 발행 사이클**이라 검증 이력이 없음. 직접 계산한
결과: latest_commit(`87d7d7f`, M5) 이후 미발행 밀스톤은 시간순으로 **M6(예고된 겨울+보상
재설득, `aa795e6`~`80cac26`, 07-17 01:28~15:17, A~F 전 항목 완료 확인)** → M7(상호대화,
완료) → M8(관계·소유·부탁, 완료) → M9(공간·재해, 명세만·미구현) 순. "한 회차=밀스톤 하나,
가장 오래된 것부터" 규칙대로면 새 로직도 M6을 골라야 정상이지만, 첫 실사용이라 오탐
가능성을 배제할 수 없어 **안전망으로 `blog_next_material_priority.md`를 M6 소재로
ACTIVE 지정**(과거 3회 순서 역전 사고와 동일 카테고리 리스크 예방 차원). M7/M8은
`deferred_milestones`로 다음다음 회차 후보 관찰.

파이프라인 경보: 403/REJECTED_3X 둘 다 CLOSED 유지, 재발 없음 — 건강.

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
| 8 | (차기 예약) | **M6 예고된 겨울 + 보상 재설득** — 계절 선반·굶주림 이탈·보상 재설득·가을 리허설(.cs 0)·개체 편차 붕괴 수정, A~F 전 항목 완료 | aa795e6~80cac26 | 7번(M5) 다음 소재 — 07-17 15:17 전 항목 완료 | PRIORITY ACTIVE — 2026-07-18 사전점검 지정 (커밋해시 스코핑 첫 실사용 안전망, `blog_next_material_priority.md` 참조) |
| — | 미발행 구간 (완료, 대기) | **M7 상호대화와 가시성** — 이름표·정보줄·대사 확장·주민 반응 대화 | a451435~08f7f9b | 8번(M6) 다음 소재 | 대기 (M6 발행 후 다음 회차 후보) |
| — | 미발행 구간 (완료, 대기) | **M8 관계·소유·부탁** — 관계 축·소유 축(내 집)·부탁 선반·보상 태도(떼먹기/선불) | 68c6ef8~66f690b | M7 다음 소재 | 대기 |

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
