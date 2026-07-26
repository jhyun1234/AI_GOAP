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

- `latest_commit`: `a351437` (fix(goal) 조리 goal 재료 조건 5 상향 — 위기 레시피와 정합
  (NoSolution 방어) — 기획팀 브리프(`01_planner_brief.md`) commit_refs 중 타임스탬프 기준
  가장 최신 커밋)
- `selected_commits_range`: `afdbc22`(feat(zone) M9-A 구역 선반, ZoneService) ~ `a351437`
  (fix(goal) 조리 goal 재료 조건 5 상향) — M9-A~J 구현 22커밋 전체:
  `afdbc22`(M9-A 구역 선반) · `ebab28a`(M9-B 파괴 문) · `db6e00c`(M9-C 재해 선반
  DisasterSO+DisasterService) · `fe05bfd`(M9-D 에셋 배선 여름·폭염·한파·관개수로) ·
  `8e27deb`(M9-G FoodDaysLeft 파생 슬롯) · `52e093a`(M9-H RelativeToCurrent 씬 goal 확장) ·
  `eccf35f`(M9-I HUD 식량 N일치 표기) · `99a61ab`(M9-J 식량 goal 트리거 개편, .cs 0) ·
  `188bbd4`(fix(food) 밭 재파종 FoodDaysLeft 게이트) · `79f1167`(fix(food) Plant 게이트
  6→4 하향, 중간 리뷰 ①) · `7863f9d`(test(agent) 이탈 진단 로그 임시) · `c504d4d`
  (fix(food) 조리식 포만 50→35) · `1082c2e`(refactor(agent) 이탈 진단 로그 제거, 조사
  종결·실제 이탈 0건) · `a16593a`(chore(scene) 관개수로 goal 파킹) · `5f3de87`
  (feat(chatter) M9-E N명 대화 릴레이+농부 회의) · `724fafe`(feat(chatter) 회의 장면 연출
  배속+HUD 알림, 사용자 Play 피드백 대응) · `9240e43`(feat(disaster) M9-F 홍수 리허설,
  에셋 1개·코드 0줄) · `33dd94b`(fix(disaster) 홍수를 밭 시설 파괴로 전환, 중간 리뷰 ②
  근본 진단) · `bc18e15`(balance(season) 가을 자원 재생 1.5→1.0) · `07a8bec`
  (balance(cook) 조리 레시피 계절 분화) · `a351437`(fix(goal) 조리 goal 재료 조건 5 상향).
  기획팀 브리프(`01_planner_brief.md`) commit_refs 전체 반영 — 명세 2건(`db4424a`·
  `9980876`)은 #11(2026-07-22 발행)에서 이미 다뤄 배경 참조로만 인용, 이번 글 본문에서는
  재해설하지 않음.
- `cycle_date`: 2026-07-26 (마스터 1차·2차 승인 후 게시팀 Step 7 발행, 격일 auto-run —
  완전 무인 실행)
- `publish_status`: PUBLISHED (Blogger published: 2026-07-26 04:39 UTC / 2026-07-25 21:39
  PDT, status: LIVE)
- `blogger_post_id`: 2543183737716951825
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/unity-goap-ai-claude-code-ai.html
- `title`: Unity GOAP AI 인디게임 개발일지: 마을에 "구역"과 "재해"를 심었더니 홍수가 밭을
  한 개도 못 태운 이유 (Claude Code 게임 개발 AI 페어 프로그래밍 후기)
- `labels`: Unity GOAP AI, Claude Code 게임 개발, AI 페어 프로그래밍, 인디게임 개발일지
- `local_archive`: tools/blog-automation/published/2026-07-26-unity-goap-m9-zone-disaster-flood.html
  (POST 응답 직후 별도 GET 요청을 `Buffer.concat` 후 `toString('utf8')`로 재조회해 저장 —
  재조회한 content가 원본 05_final.md의 html_content(마크다운 서식 제거본)와 문자열 완전
  일치(byte-identical, md5 동일, 25,019 bytes)함을 직접 확인했고 U+FFFD 없음도 확인.)
- `비고`: M9 "공간과 재해" 소재(afdbc22~a351437). 마을에 처음으로 구역(ZoneService)과
  재해(DisasterSO/DisasterService)가 생기고, 식량을 개수가 아니라 FoodDaysLeft("며칠
  버티나")로 말하기 시작한 밀스톤. 핵심 재미 포인트는 홍수가 "작물 0개 소실"만 때리던
  근본 진단 — 파종 goal의 전제("식량 넉넉하면 파종 중단")와 재해 설계의 전제("잉여를
  깎는 지렛대")가 서로 반대 방향을 보고 있었던 것. 이번 사이클은 blog_next_material_
  priority.md가 4회 연속(07-22·07-24·07-25·07-26) "devlog 서술 미비" 게이트로 보류하던
  것을 사전점검이 직접 해제하고 ACTIVE 지정한 첫 회차다 — 1차 소스를 devlog 세션 로그
  (M9-A~J 서사 0줄 확인됨, 훅 중복 버그로 고유 헤더 4개뿐)에서 `Docs/M9_공간축과재해_
  실행명세서`·`Docs/M9_식량수지_보완가이드.md` + 커밋 메시지 원문으로 전환한 것이 이번
  회차의 핵심 방법론 판단이었다(#12 방법론 명세서 회차와 동일 경로). 회상 테스트 판정과
  M9 종료·M10 전환 선언은 리포에서 검증 불가능해 본문에서 제외(밸런스 4커밋 적용까지로
  닫음). M10(야생 위협과 방랑자)·M11(개인화 경제)·M12(성격 축)는 말미 예고 한 문단만
  허용하고 본문에서 다루지 않음.

### 이전 회차 이력 (2026-07-24, 개발 방법론 명세서)

- `latest_commit`: `ee165a5` (docs(method) 방법론 개념 노트 성좌 — 3개 커밋 중 타임스탬프
  기준 가장 최신, 2026-07-18 04:21)
- `selected_commits_range`: `1ab4f9c`(docs(method) 개발 방법론 명세서, 04:00) ~ `ee165a5`
  (docs(method) 방법론 개념 노트 성좌, 04:21) — 3커밋 전부: `1ab4f9c`(docs(method) 개발
  방법론 명세서, 04:00) · `ae9d393`(docs(method) 방법론 다이어그램, 04:07) · `ee165a5`
  (docs(method) 방법론 개념 노트 성좌, 04:21). 기획팀 브리프(`01_planner_brief.md`)
  commit_refs 전체 반영.
  ⚠️ **git 계보 caveat**: 이 3커밋은 유효한 커밋 객체로 존재하고 내용도 현재 저장소
  파일과 일치하지만, `git merge-base --is-ancestor <hash> HEAD` 검사는 실패한다 — main이
  스쿼시/재작성되며 `bc18e15`(balance(season), 678개 파일·377,003줄 대형 스쿼시 커밋)를
  통해 원본 3커밋의 개별 정체성 없이 내용만 main 계보에 합류했다. 산출물 파일
  (Docs/개발_방법론_명세서.md, Docs/개발_방법론.canvas, Docs/방법론/*.md 12개)은
  origin/main HEAD에 실제로 존재함을 직접 확인했고, 이를 근거로 소재 진위·존재를
  판단했다 — 상세는 `01_planner_brief.md`의 `git_lineage_caveat` 참조.
- `cycle_date`: 2026-07-24 (마스터 1차·2차 승인 후 게시팀 Step 7 발행, 격일 auto-run)
- `publish_status`: PUBLISHED (Blogger published: 2026-07-24 04:24 UTC / 2026-07-23 21:24
  PDT, status: LIVE)
- `blogger_post_id`: 6860975822353124027
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/claude-code-ai-unity-goap-ai-ai.html
- `title`: Claude Code 게임 개발 AI 페어 프로그래밍 후기: Unity GOAP AI 인디게임 개발일지 —
  AI에게 "나처럼 판단해줘"를 문서 한 장으로 남기기까지
- `labels`: Claude Code 게임 개발, AI 페어 프로그래밍 후기, Unity GOAP AI, 인디게임 개발일지
- `local_archive`: tools/blog-automation/published/2026-07-24-unity-goap-methodology-spec-constellation.html
  (POST 응답 직후 별도 GET 요청을 `Buffer.concat` 후 `toString('utf8')`로 재조회해 저장 —
  재조회한 content가 원본 05_final.md의 html_content(마크다운 서식 제거본)와 문자열 완전
  일치(byte-identical, md5 동일, 11,564 bytes)함을 직접 확인했고 U+FFFD 없음도 확인.)
- `비고`: 개발 방법론 명세서 소재(1ab4f9c~ee165a5). 코드가 아니라 "AI가 이 프로젝트에서
  어떻게 판단하는가"를 방법론 명세서 1건 + mermaid 다이어그램 2종 + 옵시디언 Canvas 1개 +
  위키링크 개념 노트 12개로 박제한 메타 회차. M9/M10/M11(공간·재해/야생 위협/개인화 경제)은
  이번 글에서 다루지 않음 — 전부 devlog 서술 미비로 승격 보류 상태(아래 다음 회차 소재
  판단 참조).

### 이전 회차 이력 (2026-07-22, M8 후속 보완 + M9 명세)

- `latest_commit`: `897605c` (fix(social) 완공 보고 "쫓아가기"→"마주치면 정산"(조각 Y) +
  IPathfinder 경계 — 4개 커밋 중 타임스탬프 기준 가장 최신, 2026-07-18 14:48)
- `selected_commits_range`: `db4424a`(spec(m9) 공간 축과 재해 실행명세서, 02:27) ~
  `897605c`(fix(social) 완공 보고 "쫓아가기"→"마주치면 정산" + IPathfinder 경계, 14:48) —
  비연속 4커밋: `897605c`(fix(social), 14:48) · `f907fc2`(feat(bubble) 말풍선 침묵 여운 3초,
  03:05) · `db4424a`(spec(m9) 공간 축과 재해 실행명세서, 02:27) · `9980876`(spec(m9) 식량
  수지 보완가이드, 02:57). 기획팀 브리프(`01_planner_brief.md`) commit_refs 전체 반영 —
  같은 시간대에 섞여 있던 서사 무관 부수 커밋(`958fbfd`·`b53d52d`·`3170129`·`8b828ad`
  devlog/파이프라인 정책 커밋, `1ab4f9c`~`ee165a5` 개발 방법론 명세서, `dda3a69` 사전점검
  기록 등)은 제외.
- `cycle_date`: 2026-07-22 (마스터 1차·2차 승인 후 게시팀 Step 7 발행, 격일 auto-run)
- `publish_status`: PUBLISHED (Blogger published: 2026-07-22 04:39 UTC / 2026-07-21 21:39
  PDT, status: LIVE)
- `blogger_post_id`: 4764036419422193885
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/claude-code-ai-unity-goap-ai.html
- `title`: Claude Code 게임 개발 AI 페어 프로그래밍 후기: Unity GOAP AI 인디게임 개발일지 —
  쫓아가도 못 잡는 버그를 "빚으로 적어두기"로 뒤집은 길찾기 확장 설계
- `labels`: Claude Code 게임 개발, AI 페어 프로그래밍 후기, Unity GOAP AI, 인디게임 개발일지
- `local_archive`: tools/blog-automation/published/2026-07-22-unity-goap-debt-settlement-pathfinder-boundary.html
  (POST 응답의 `labels` 필드에서 `data += c` 스트림 이어붙이기 버그로 멀티바이트 문자가
  깨진 것을 직접 확인했다(U+FFFD, "인디게임"이 "인���게임"으로 표시됨) — 그대로 저장하지
  않고 별도 GET 요청을 `Buffer.concat` 후 `toString('utf8')`로 재조회해 저장. 재조회한
  content가 원본 `05_final.md`의 html_content와 문자열 완전 일치(byte-identical, md5 동일,
  13,581 bytes)함을 직접 확인했고 U+FFFD 없음도 확인.)
- `비고`: M8 후속 보완(완공 보고 "쫓아가서 건네기" → "빚+정산" 재진단, IPathfinder 경로탐색
  창구 단일화) + M9 명세 단계(공간·재해 실행명세서, 식량 수지 보완가이드) 묶음 소재.
  M9 실제 구현(M9-A~J, `afdbc22`~`a351437`)은 이번 글에서 다루지 않음 — 기획팀 브리프의
  명시 지시(밀스톤 미완결, 재관측 대기)를 그대로 따름, 말미 예고 한 문단만 허용.

### 이전 회차 이력 (2026-07-21, M8 관계·소유·부탁)

- `latest_commit`: `66f690b` (docs(devlog) 2026-07-18 M8 후속 세션 — 범위 종료점)
- `selected_commits_range`: `68c6ef8`(spec(m8) 사회 축 실행명세서) ~ `66f690b`(docs(devlog)
  2026-07-18 M8 후속 세션) — 18커밋(M8-A~F 6개 세부 항목 + CLAUDE.md 비용표 갱신·DoD
  코드 검증·세션 로그 + 후속 보완 4건(대화 호흡 상향+공용 쿨다운 8e53abf, 부탁 가시화+
  완공 보고 심부름+주민 간 보상 5ce720e, 성격별 보상 태도 떼먹기·선불 요구
  efcb0d5/8d72438, 보고 도착 재검사+씬 주민 10→4명+장식성 발화 억제 2d24b0f/9be5239,
  집=목수 부탁 전용화 5956af2) 포함, 기획팀 브리프 `01_planner_brief.md` commit_refs
  전체 반영 — 서사 무관 부수 커밋 0369bda·987e9bb는 제외)
- `cycle_date`: 2026-07-21 (마스터 1차·2차 승인 후 게시팀 Step 7 발행)
- `publish_status`: PUBLISHED (Blogger published: 2026-07-21 04:26 UTC / 2026-07-20 21:26 PDT, status: LIVE)
- `blogger_post_id`: 2940839193792714370
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/claude-code-ai-goap-ai-m8.html
- `title`: Claude Code 게임 개발 AI 페어 프로그래밍 후기: 유니티 GOAP AI 인디게임 개발일지 — 주민들이 드디어 서로를 "기억"하기 시작했다 (M8 관계·소유·부탁)
- `labels`: Claude Code 게임 개발, AI 페어 프로그래밍 후기, 유니티 GOAP AI, 인디게임 개발일지
- `local_archive`: tools/blog-automation/published/2026-07-21-unity-goap-m8-relationship-ownership-request.html
  (POST 응답의 content 필드는 `data += chunk` 스트림 이어붙이기 버그로 멀티바이트 문자가
  깨져 있어(U+FFFD 확인됨) 그대로 저장하지 않고, GET으로 재조회하며 `Buffer.concat` 후
  `toString('utf8')`로 재획득해 저장 — 원본 05_final.md html_content와 바이트 단위로
  완전 동일함을 직접 확인, 14,874 bytes)
- `비고`: M8 관계·소유·부탁 소재(68c6ef8~66f690b). 대화 이벤트 구독형 관계 축적(M8-A,
  RelationshipService)·정보줄 단짝·원한 표기(M8-B)·소유 축(M8-C, OwnershipService+
  MyHasHome)·부탁 선반(M8-D, RequestSO/RequestService/JudgeRequest)·부탁 에셋 1호+
  요리 부탁 리허설(M8-E/F, 데이터 주도 설계 다섯 번째 재현)까지 A~F 전 항목 포함.
  완료 선언 직후 실측에서 발견된 후속 보완 4건 — 성격별 보상 태도(떼먹기·선불 요구)·
  집은 목수 부탁으로만(자율 건설·자동 배정 폐기, 트레이드오프)·대화 흐름 보호(장식성
  발화 억제)·부탁 가시화+완공 보고 심부름+주민 간 보상 — 도 다룸. 이 글로 M8 미발행
  구간 해소.


### 이전 회차 이력 (2026-07-20, M7 상호대화와 가시성)

- `latest_commit`: `598e51e` (feat(content) 대사 풀 2차 확장 — .cs 0, 사용자 요청)
- `selected_commits_range`: `a451435`(spec(m7) M7 실행명세서) ~ `598e51e`(feat(content) 대사 풀 2차 확장) — 11커밋(M7-A~E 5개 세부 항목 + M7-T3 에셋 정책 게이트 + M7 완료 직후 실측에서 발견된 부수 버그 수정 3건(모닥불 몰림 8e1ac6b·대화 연출 94b892e·말풍선 우선순위 799842f) + 대사 풀 2차 확장 598e51e 포함, 기획팀 브리프 `01_planner_brief.md` commit_refs 전체 반영)
- `cycle_date`: 2026-07-20 (마스터 1차·2차 승인 후 게시팀 Step 7 발행)
- `publish_status`: PUBLISHED (Blogger published: 2026-07-20 04:43 UTC / 2026-07-19 21:43 PDT, status: LIVE)
- `blogger_post_id`: 267235146240614055
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/claude-code-ai-goap-ai-m7.html
- `title`: Claude Code AI 페어 프로그래밍 후기: 유니티 GOAP AI 인디게임 개발일지 — 주민들이 드디어 서로에게 말을 걸기 시작했다 (M7 상호대화와 가시성)
- `labels`: Claude Code 게임 개발, AI 페어 프로그래밍 후기, Unity GOAP AI, 인디게임 개발일지
- `local_archive`: tools/blog-automation/published/2026-07-20-unity-goap-m7-chatter-visibility.html
  (Blogger API 응답의 content 필드를 그대로 저장 — 실제 게시본과 동일, 9,101 bytes)
- `비고`: M7 상호대화와 가시성 소재(a451435~598e51e). 이름표+선택 정보줄(M7-A)로 성격(M4)·
  직업(M5) 분화가 처음으로 화면에서 보이게 됨·대사 풀 확장(.cs 0, M7-B)·ChatterSO/
  ChatterService 상호대화 선반(M7-C, ADR: 표현 전용·관계 미축적·이름 분기 금지·주기당
  최대 1쌍)·대화 에셋 2종(M7-D)+에셋 정책 게이트(M7-T3)·직교성 리허설(M7-E, 에셋
  1개·코드 0)까지 전 항목 포함. 완료 직후 실측에서 발견된 부수 버그 3건(모닥불 몰림
  해소·대화 연출 멈춰서 마주보기+반경 4→2타일·말풍선 우선순위 정리)도 다룸. 이 글로
  M7 미발행 구간 해소.

### 이전 회차 이력 (2026-07-19, M6 예고된 겨울과 설득)

- `latest_commit`: `7c0dd7f` (feat(personality) 성격이 위기 대응을 가른다 — GoalBoosts 축 + 이탈 계단화)
- `selected_commits_range`: `aa795e6`(spec(m6) M6 실행명세서) ~ `7c0dd7f`(feat(personality) 성격이 위기 대응을 가른다) — 12커밋(M6-A~F 8개 세부 항목 + 위기감 재조정 + 개체 편차 붕괴 수정 포함, 기획팀 브리프 `01_planner_brief.md`에서 지정 문서 범위(`aa795e6`~`80cac26`)를 `a0e318b`·`7c0dd7f`(같은 세션의 M6 결말부)까지 보정)
- `cycle_date`: 2026-07-19 (마스터 1차·2차 승인 후 게시팀 Step 7 발행)
- `publish_status`: PUBLISHED (Blogger published: 2026-07-19 04:30 UTC / 2026-07-18 21:30 PDT, status: LIVE)
- `blogger_post_id`: 4301273701134180743
- `blog_url`: https://gamedevclaude.blogspot.com/2026/07/claude-code-unity-0.html
- `title`: 인디게임 개발일지: Claude Code와 만든 Unity 계절 시스템 구현 — 기술은 만점인데 재미는 0점이었다
- `labels`: AI 페어 프로그래밍 후기, Claude Code 게임 개발, Unity 계절 시스템 구현, Unity GOAP 게임 밸런싱
- `local_archive`: tools/blog-automation/published/2026-07-19-unity-goap-m6-season-winter-crisis.html
  (Blogger API 응답의 content 필드를 Buffer.concat 후 toString('utf8')로 재획득해 저장 —
  게시 직후 별도 GET으로 재조회한 값과 원본 제출본을 diff/md5 대조해 완전 동일함을 확인,
  13,856 bytes)
- `비고`: M6 예고된 겨울과 설득 소재(aa795e6~7c0dd7f). 계절 선반(SeasonSO/SeasonService)·
  겨울 효과(배율 곱 지점 2곳)·달력 HUD 예고·굶주림 이탈(이 게임 최초의 실패 상태)·보상
  재설득(에스크로 방식, 이 게임 최초의 협상 수단)·가을 리허설(.cs 0)까지 M6-A~F 전 항목+
  완료 직후 실측에서 "겨울 수지 2배 잉여"를 발견해 즉시 재조정(겨울 3→4일, 감쇠 1.75배,
  주민 5→10명)한 위기감 재조정(a0e318b)·성격별 위기 대응 분화(GoalBoosts, 7c0dd7f)까지
  포함. 같은 세션에서 발견된 "개체 편차 붕괴" 버그(주민 5명의 초기 포만이 문자열 해시
  충돌로 81.6~81.7에 뭉치던 것을 FNV-1a로 근본 수정, 44b7538)도 다룸. 이 글로 M6 미발행
  구간 해소.

### 이전 회차 이력 (2026-07-18, M5 직업과 일과)

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
