---
name: blog-last-published-commit
description: 블로그 파이프라인이 마지막으로 소재로 사용한 최신 커밋 해시 및 게시 상태 — 기획팀의 중복 소재 방지용
metadata: 
  node_type: memory
  type: reference
  originSessionId: 67127d74-590e-4800-92a2-33f811dd3e8e
---

# 블로그 파이프라인 최근 사용 커밋

**Why:** 기획팀이 다음 사이클에서 이미 다룬 커밋을 다시 소재로 고르지 않도록, 가장 최근에 파이프라인이 "사용 완료"로 처리한 커밋 해시를 기록해 둔다.

**How to apply:** 기획팀은 새 사이클 시작 시 이 파일의 `latest_commit`을 확인하고, 그 이전(포함) 커밋들은 이미 소재로 소비된 것으로 간주한다. 단, 아래 상태가 `DRAFT`인 경우 실제 공개 게시는 아니었으므로, 같은 소재를 이어서 다룰지/새 소재로 넘어갈지는 그때 기획팀이 판단한다.

## 최신 사용 커밋

- `latest_commit`: `87d7d7f` (feat(m5) M5-E 목수 리허설 — 직업 추가 = 에셋 1개 + 씬 풀 등록, .cs 0개)
- `selected_commits_range`: `fd6b1db`(feat(m5) M5-A JobSO 스키마·스폰 할당) ~ `87d7d7f`(feat(m5) M5-E 목수 리허설)
- `cycle_date`: 2026-07-18 (마스터 1차·2차 승인 후 게시팀 Step 7 발행)
- `publish_status`: PUBLISHED (Blogger published: 2026-07-18 KST, status: LIVE)
- `blogger_post_id`: 2221417024528752034
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/unity-goap-ai.html
- `title`: 인디게임 개발일지: Unity GOAP AI 직업 시스템 — 농부는 밭 곁을 떠나지 않고, 탐험가는 지도 밖으로 사라진다
- `labels`: Unity GOAP AI, 인디게임 개발일지, 게임 AI 프로그래밍, GOAP 직업 시스템
- `local_archive`: tools/blog-automation/published/2026-07-18-unity-goap-m5-job-schedule.html
  (Blogger API 응답의 content 필드를 그대로 저장 — 실제 게시본과 바이트 단위 동일, 14,466 bytes)
- `비고`: M5 직업과 일과 소재(fd6b1db~87d7d7f, 5커밋+JPS 대각 강제 이웃 수정 eac0907 포함).
  JobSO 스키마·실효 우선순위 결합(ADR-M5-6, 사용자 승인 하 방향 전환)·일과 주입(농부 밭
  배회·탐험가 지도 밝히기)·직업 에셋 5종·6번째 직업(목수) 코드 0줄 리허설 + 같은 세션에서
  발견된 JPS 대각선 길찾기 버그(M4 수정 c03d461의 반쪽 봉합을 뒤늦게 발견)까지 포함.
  게시 중 게시팀 운영 실수로 동일 글이 한 번 더 게시(post_id 4412536271596111356,
  url .../unity-goap-ai_01703797100.html)됐다가 Blogger API DELETE(HTTP 204)로 즉시
  삭제·정리됨 — 최종 라이브 글은 위 post_id 하나만 남음. 이 글로 M5 미발행 구간 해소.

### 이전 회차 이력 (2026-07-17, M4 성격 시스템)

- `latest_commit`: `862de3b` (docs(m4) M4 완료 선언 — 성공 기준 S1~S7 충족)
- `selected_commits_range`: `b982090`(spec(m4) M4 주민의 성격 실행명세서) ~ `862de3b`(docs(m4) M4 완료 선언)
- `cycle_date`: 2026-07-17 (마스터 2차 승인 후 게시팀 Step 7 발행)
- `publish_status`: PUBLISHED (Blogger published: 2026-07-17 KST, status: LIVE)
- `blogger_post_id`: 6809679934191538669
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/unity-goap_0950458123.html
- `title`: 주민이 처음으로 "다른 사람"으로 보인 날 — Unity GOAP 성격 시스템 인디게임 개발일지
- `labels`: Unity GOAP, 인디게임 개발일지, GOAP 성격 시스템
- `local_archive`: tools/blog-automation/published/2026-07-17-unity-goap-m4-personality.md
- `비고`: M4 성격 시스템 소재(b982090~862de3b, 11커밋). PersonalitySO 스키마·비용 배율·명령 거부·혼잣말·5번째 아키타입 + JPS 강제 이웃 버그 수정 포함. 이 글로 M4 미발행 구간 해소.

### 이전 회차 이력 (2026-07-16, M2+M3 생산체인+주거 기반)

- `latest_commit`: `153f180` (M3 완료 선언 — M2 생산체인 + M3 주거 기반 소재의 마지막 커밋)
- `selected_commits_range`: `651ea47`(M2 스펙) ~ `153f180`(M3 완료 선언)
- `cycle_date`: 2026-07-16 (마스터 2차 승인 후 게시팀 Step 7 발행 — 전날 REJECTED_3X였던 동일
  소재가 계측 기준 통일 후 재작성/재검수를 거쳐 이번 회차에 승인·발행됨)
- `publish_status`: PUBLISHED (Blogger published: 2026-07-16 16:07 KST, status: LIVE)
- `blogger_post_id`: 3935987342991362953
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/unity-goap.html
- `title`: 밭에서 부엌으로, 그리고 처음 생긴 집 한 채 — Unity GOAP 생산체인과 마을 시뮬레이션 인디게임 개발일지
- `labels`: 유니티, 인디게임, AI개발, GOAP
- `local_archive`: tools/blog-automation/published/2026-07-16-unity-goap-m2-m3-production-housing.html
  (Blogger API 응답의 content 필드를 그대로 저장 — 실제 게시본과 바이트 단위 동일)
- `비고`: 2026-07-16 13:03 KST auto-run에서는 작성팀·검수팀 분량 계측 기준 불일치로 검수
  3연속 반려(REJECTED_3X, `blog_pipeline_alerts.md` 참조)되어 미발행이었으나, 계측 기준을
  `wc -m` 하나로 통일한 뒤 동일 소재로 재작성·재검수를 거쳐 마스터 1차·2차 승인을 모두
  통과했고 이번 로컬 세션에서 게시팀(Step 7)이 실제 발행을 완료했다. 이 글로 M2(생산체인)·
  M3(주거 기반) 미발행 구간이 해소됨 (BLOG_COVERAGE.md 참조).

### 이전 회차 이력 (2026-07-15, M0 회고 특집)

- `latest_commit`: `cc4602e` (유지 — 이번 회차는 회고 특집 예외, M0 커밋 구간은 별도 트랙이므로 갱신하지 않음)
- `selected_commits_range`: `8ee845b` ~ `b175ddf` + `04f0975` (M0 GOAP 정합 재설계 대서사 — 회고 특집)
- `cycle_date`: 2026-07-15 (원격 auto-run 13:03 KST — `blog_next_material_priority.md` ACTIVE 게이트 무조건 통과)
- `publish_status`: PUBLISHED (Blogger published: 2026-07-15 13:40 KST)
- `blogger_post_id`: 6764155466991758383
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/unity-goap-24800-5977-m0.html
- `title`: Unity GOAP 재설계: 24,800줄을 하루 만에 5,977줄로 줄인 M0 재건 개발일지
- `labels`: 유니티, 인디게임, AI개발, GOAP
- `local_archive`: tools/blog-automation/published/2026-07-15-unity-goap-m0-redesign.html
  (원격 sandbox push 403 실패 → 2026-07-15 로컬에서 Blogger API GET으로 재획득해 복구.
  검증: Buffer.concat 후 toString('utf8'), 13,826 bytes)
- `비고`: 원격 sandbox의 `claude/state-2026-07-15T040738Z` push가 07-14에 이어 **2회 연속
  403 실패** → MANUAL_STATE_UPDATE 경로로 로컬 수동 반영 (2026-07-15). 상세는
  `blog_pipeline_alerts.md` 참조. 이 글로 M0 미발행 구간이 해소됨 — 남은 미발행 구간은
  M2(생산체인)·M3(주거 기반) (BLOG_COVERAGE.md 참조). → 이 M2/M3 구간은 위 최신 항목
  (2026-07-16)에서 발행 완료됨.

### 이전 회차 이력 (2026-07-14, M1 밀스톤 완결)

- `latest_commit`: `cc4602e` (M1 밀스톤 완결 시점 — M1 개발 소재의 마지막 커밋. 이후 M2 명세 커밋들은 미소비)
- `selected_commits_range`: M1 트랙 (여가·식량순환·명령/거부·대사변주, 2026-07-14)
- `cycle_date`: 2026-07-14 (원격 auto-run 13:03 KST — 일일 게이트 전환 후 첫 발행)
- `publish_status`: PUBLISHED
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/unity-goap-ai-ai-m1.html
- `비고`: 원격 sandbox의 claude/state-* push가 403으로 실패 → 이 항목은 로컬에서 수동
  반영함 (2026-07-14). M0 재설계 대서사(24,800→5,977줄)는 이 글에서 다뤄지지 않음 →
  2026-07-15 회고 특집으로 발행 완료.

### 이전 회차 이력 (2026-07-11, F-A 성격배분 + Satiety 반전)

- `latest_commit`: `b930365` (refactor(core): 허기 → 포만감 세만틱 반전 — F-A 성격배분+Satiety 반전 묶음 소재의 마지막 커밋)
- `selected_commits_range`: 549444d ~ b930365 (549444d F-A 성격배분 + b930365 허기→포만감 반전, 2커밋 묶음)
- `cycle_date`: 2026-07-11 (원격 auto-run, session `cse_01U1p6C8aDWswa4xhkCzhXSR`)
- `publish_status`: PUBLISHED (실제 공개 발행 완료, Blogger status: LIVE)
- `blogger_post_id`: 118729701893909598
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/goap-ai-ai.html
- `local_archive`: (원격 sandbox에서 생성, git push 실패로 리포에 없음 — 필요 시 blogger-client.js get으로 재획득)
- `title`: 유니티 GOAP AI 개발일지: 인디게임 AI 리팩터링 — 성격 시스템 완성 후 발견한 씬 배치 버그와 허기→포만감 반전
- `labels`: 유니티, 인디게임, AI개발, GOAP

### 이전 회차 이력 (2026-07-09, 방향 ③ GatherIron 무해 봉합)

- `latest_commit`: `4df41cc` (docs(goap): N2 CLAUDE.md ADR-10 + 커밋 전 체크 ⑧ 추가 — 방향 ③ GatherIron 초반 무해 봉합 소재의 마지막 커밋)
- `selected_commits_range`: 87ac93c ~ 4df41cc (87ac93c, 5448e68, 45d546c, 81d8c76, 4df41cc — 방향 ③ GatherIron 초반 무해 봉합 5커밋)
- `cycle_date`: 2026-07-09
- `publish_status`: PUBLISHED
- `blogger_post_id`: 3490326739510391777
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/unity-goap-ai-4096.html
- `local_archive`: tools/blog-automation/published/2026-07-09-unity-goap-ai-4096.html

### 이전 DRAFT 이력 (참고용, 별개 소재)

- `latest_commit`: `f05ae3c` (docs(goap): CLAUDE.md에 이동 실패 first-class 계약(ADR-9 + 커밋 전 체크 ⑦) 명시)
- `selected_commits_range`: eb8aaed ~ f05ae3c (M1~M6, ADR-9 CLAUDE.md 반영까지 포함) — v1/v2 동일, 이 커밋 구간은 변경 없음
- `publish_status`: DRAFT (비공개 초안 — 여전히 공개 발행 아님)
- `blogger_post_id`: 7396645330542050308
- `blog_url`: https://gamedevclaude.blogspot.com/ (초안 상태라 퍼머링크 미발급. 관리자 화면: https://www.blogger.com/blog/posts/6014451945015572125?hl=ko)
- `local_archive`: tools/blog-automation/published/2026-07-09-goap-pathfinding-honest-failure.html

## v2 교체 기록 (2026-07-09)

마스터 에이전트가 Step 6에서 v2 최종 HTML을 승인한 뒤, 신규 게시가 아니라 **기존 DRAFT
교체**를 지시했다. 동일 소재(커밋 구간 eb8aaed~f05ae3c)에 대해 사용자 요청으로 분량 확대
+ "의외의 디테일" 섹션 등 재미 요소를 추가한 v2 본문으로 교체한 것이며, 새로운 소재를
다룬 것이 아니다.

- 사용한 명령: `node tools/blog-automation/scripts/blogger-client.js update --post-id 7396645330542050308 --title-file <tmp> --content-file <tmp> --labels <4개>` (신규 `post` 명령이 아니라 `update` 명령)
- 결과: HTTP 성공, `status: "DRAFT"` 유지 확인. 제목/본문/라벨 4개 모두 반영됨.
- 검증: update 직후 GET으로 실제 저장된 콘텐츠를 다시 가져와 로컬 원본 파일과 바이트 단위(`Buffer.concat` 후 `toString('utf8')` 비교, `data += chunk` 방식이 아님)로 비교해 완전히 동일함을 확인. (주의: `res.on('data', c => data += chunk)` 식으로 스트림을 이어붙이면 UTF-8 멀티바이트 문자가 TCP 청크 경계에서 잘려 `U+FFFD`로 깨져 보일 수 있다 — 실제 데이터 손상이 아니라 검증 스크립트 쪽의 버그였다. 다음에 콘텐츠 무결성을 확인할 때는 반드시 `Buffer.concat(chunks)` 후 한 번에 `toString('utf8')` 할 것.)
- 커밋 해시는 v1과 동일(`f05ae3c`)하며, 바뀐 것은 본문 내용뿐이다. `latest_commit`/`selected_commits_range` 필드는 갱신할 필요 없음.

## 기록 판단 근거 (draft인데도 이력에 남긴 이유)

이번 회차는 파이프라인 전체의 첫 드라이런이라 사용자가 명시적으로 "비공개 초안으로만 올려달라"고 요청했다. Blogger API 호출 자체는 성공했고(`status: DRAFT`로 정상 생성됨), 다만 공개 발행(publish)은 아니다.

- 커밋 해시를 기록한 이유: 이 소재(경로탐색 실패 시 좌표 스냅 제거, M1~M6)는 이미 한 편의 글로 소비되었다. 기록하지 않으면 다음 기획 사이클이 같은 커밋 구간을 다시 골라 중복 글을 기획할 위험이 있다.
- 다만 `publish_status: DRAFT`로 명시해 "공개 게시 완료"와는 구분했다. 다음 사이클의 기획팀이 이 필드를 보고 "이미 초안까지는 나갔다 → 그대로 공개 게시만 진행할지, 소재를 폐기하고 새로 갈지"를 판단할 수 있게 했다.
- 즉, 이 메모는 "공개 게시 완료 이력"이 아니라 "소재 소비 + 파이프라인 처리 이력"으로 남긴 것이다.

## 경로 정정 기록 (2026-07-09)

드라이런 중 blog-publisher 에이전트가 이 파일을 잘못된 위치(`C:\Users\anjyo\AI_GOAP\memory\`,
git 저장소 내부에 새로 생성)에 썼다가 여기(auto-memory 디렉토리)로 옮겼다. 원인: 에이전트
정의(blog-publisher.md)와 오케스트레이션 프롬프트에 "memory/"라고만 적혀 있고 절대경로가
명시되지 않아 에이전트가 저장소 상대경로로 오해함. blog-publisher.md와
tools/blog-automation/README.md에 절대경로를 명시해 재발을 막았다 — Phase 3 자동화에서
동일 실수가 나면 이 메모를 먼저 참조할 것.
