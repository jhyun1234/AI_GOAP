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
