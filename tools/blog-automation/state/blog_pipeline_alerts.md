---
name: blog-pipeline-alerts
description: 블로그 자동화 파이프라인의 반복 장애·경보 기록 — 다음 회차 실행 전 반드시 확인
metadata:
  node_type: memory
  type: reference
---

# 블로그 파이프라인 경보

**Why:** 원격 auto-run에서 반복적으로 발생하는 인프라 장애를 기록해, 매 회차 같은 문제를
새로 진단하는 낭비를 막고 근본 해결 여부를 추적한다.

**How to apply:** 원격 routine과 로컬 점검 세션은 실행 시작 시 이 파일을 확인한다.
OPEN 상태의 경보가 있으면 해당 우회 절차(MANUAL_STATE_UPDATE 등)를 미리 준비한다.

## 🔴 OPEN — 발행은 됐는데 상태 커밋이 유실됐다 (2026-08-10) · **08-11 "무발동" 경보는 오진이었다**

- **관측(2026-08-12 사전점검, 라이브 블로그 직접 열람)**: 08-10 회차는 **정상 발동했고
  정식 공개 발행까지 성공했다.** 블로그 아카이브에 2026-08-10 자로 M19 글이 게시돼 있다 —
  https://gamedevclaude.blogspot.com/2026/08/45-2260-unity-goap-claude-code-ai.html
  ("완성한 날 전부 지웠습니다: 마을 화폐 경제 45개 파일 2,260줄 철거기"). 본문 도입부가
  M19 서사(45파일 2,260줄 철거 · GOAP 마을 · 재정 정책이 개입 수단이었다는 진단)와 일치.
- **실제 고장난 곳은 마지막 한 칸이다**: Blogger 발행(Step 7)은 성공했으나 **상태 파일
  갱신·커밋·push(Step 8)가 남지 않았다.** 그래서 `blog_last_published_commit.md`는
  M15(`ece7029`)에서, `blog_next_material_priority.md`는 M19 `STATUS: ACTIVE`(미소비)에서
  멈춰 있었다. 두 파일 모두 이번 점검이 소급 복원했다.
- 🔴 **이 오진이 만들 뻔한 사고**: 상태 파일이 "M19 미발행"이라고 믿고 있었으므로,
  **오늘(08-12) 13:03 run 은 기준 D(ACTIVE)를 통과해 M19를 두 번째로 발행할 참이었다.**
  같은 밀스톤 중복 발행 = 이 블로그가 겪은 적 없는 신종 사고. 이번 점검의 교정이 막았다.
- 🔑 **왜 08-11 점검이 틀렸는가 — 교훈**: 08-11 점검은 `git log --all --grep="chore(blog)"`
  전수 확인으로 "커밋 0건 → run 자체가 없었다"고 단정했다. **커밋만 보고 결과물(블로그)을
  보지 않은 것이 오진의 원인이다.** 파이프라인의 성공 증거는 커밋이 아니라 **발행물**이다.
  → **앞으로 무발행이 의심되면 커밋 로그가 아니라 블로그 아카이브를 먼저 열 것.**
  (08-11 점검이 근거로 삼은 "08-08 DRAFTED 회차도 커밋을 남겼으니 커밋이 없으면 안 돈
  것"이라는 추론은, 커밋 단계 자체가 고장날 수 있다는 경우를 빠뜨린 것이었다.)
- **강제 push 로 지워진 것이 아니다**: `git merge-base --is-ancestor dfd6d6bf origin/main`
  통과 — 08-10 당시 origin/main 팁이 현재 이력에 그대로 살아 있다. 로컬 117커밋 push 는
  fast-forward 였으므로 남의 커밋을 덮어쓰지 않았다. **애초에 push 된 적이 없는 것이다.**
- **미상으로 남는 것**: `blogger_post_id`, 승인 경로·반려 횟수, `.staging/` 산출물,
  `published/` 로컬 사본 — 전부 상태 커밋과 함께 유실. 발행물 자체는 무사하다.
- 🔴 **사람 몫 / 재발 방지**: ⑴ 게시팀 Step 8이 상태 커밋의 **`git push` 성공까지 확인**
  하도록 `blog-publisher.md` 보강 필요(이 점검의 범위 밖 — 다음 로컬 세션 몫).
  ⑵ 08-08 M19 초안(post_id `9034137679451863013`, DRAFT)이 아직 살아 있다면 **이제는
  같은 소재의 공개본이 실제로 존재하므로** 삭제가 전보다 시급하다.
- ✅ **재발 없음 — 08-12 회차가 Step 8까지 완주했다 (2026-08-13 사전점검 관측)**:
  `chore(blog): auto-run state update (2026-08-12) — M20 발행 + M21 ACTIVE 지정`
  (`cd76b9d2`)이 **origin/main 에 정상 push 됐다.** 상태 파일 3종 + 로컬 사본
  (`published/2026-08-12-unity-goap-m20-carpenter-rule.html`)이 전부 남았고, 게시팀이 POST
  응답 content 를 md5 로 대조해 byte-identical 을 확인한 기록까지 있다. **08-10 유실은
  반복 고장이 아니라 1회성이었다.**
- **상태**: OPEN (2026-08-12 사전점검이 08-11 경보를 대체·정정. 08-13 점검이 재발 없음을
  확인했으나 **닫지 않는다** — 이 경보가 스스로 정한 CLOSE 조건은 관측 1회가 아니라
  `blog-publisher.md` Step 8 의 `git push` 성공 확인 보강이며, 그 코드는 아직 미이행이다).
  보강이 들어가면 CLOSE.

## ~~🔴 OPEN — 짝수일 회차 무발동 (2026-08-10) · 원격 routine 생존 여부 미확인~~ → 🔵 CLOSED (오진, 2026-08-12 정정)

> ⚠️ **아래 경보는 사실이 아니었다.** 08-10 회차는 발동했고 발행에도 성공했다 —
> 유실된 것은 상태 커밋뿐이다(위 항목 참조). 기록 보존용으로 남기되, **"08-10 무발동"·
> "원격 routine 이 멈췄다"·"짝수일 규칙이 깨졌다"는 서술을 근거로 인용하지 마라.**
> 짝수일 발동 규칙은 **깨지지 않았다** — 07-25 이후 지금까지 유효하다.

- **관측(2026-08-11 사전점검)**: `git log --all --grep="chore(blog)"` 전수 확인 결과
  파이프라인 자기 커밋의 최신은 **`dc527850`(2026-08-08, DRAFTED)**이며, 그 뒤
  **08-10(짝수일)에 `chore(blog)` 커밋이 하나도 없다.** 08-09·08-11은 홀수일이라 정상
  무발행이지만 **08-10은 발동했어야 하는 날**이다.
- **게이트는 문제가 아니었다**: 08-10 시점 origin/main 기준 `game_commits=161` `big=22`
  `days=9` 로 기준 A·B·C 통과, `blog_next_material_priority.md` 최상단이 `STATUS: ACTIVE`
  라 기준 D도 통과, `publish_status`도 `PUBLISHED`(무결성 가드 해당 없음). 즉
  **"게이트 미달 SKIP"이 아니다** — SKIP이었다면 그것도 실행 흔적을 남긴다.
- **"돌았는데 실패"보다 "아예 안 돌았다"에 가깝다**: 08-08 회차는 반려 3회로 DRAFTED가
  됐을 때조차 `chore(blog): auto-run state update (2026-08-08) — DRAFTED` 커밋을 남겼다.
  완주하든 반려하든 커밋이 남는 구조인데 그 흔적이 0이므로, **cron 미발동 또는 에이전트
  호출 이전 단계에서의 중단**이 유력하다. 403 흔적도 없다(요청 자체가 없었으므로).
- **왜 지금 경보인가**: 2026-07-25 이후 "짝수일 발동 / 홀수일 무발행"이 8회차 연속
  적중해 이 파일과 `BLOG_COVERAGE.md` 전체가 그 규칙을 전제로 예측을 내려 왔다.
  **08-10이 그 규칙이 깨진 첫 사례**다. 규칙이 무너진 채로 두면 이후 점검이 계속
  "짝수일이니 나갔을 것"이라 잘못 예측한다.
- **소재는 소비되지 않았다**: `blog_last_published_commit.md`(`latest_commit: ece7029`,
  `publish_status: PUBLISHED`)·`blog_next_material_priority.md`(M19 `STATUS: ACTIVE`)
  모두 08-08 이후 변동 없음 — 상태 파일은 건강하다. **M19 소재는 그대로 유효하다.**
- 🔴 **다음 관측점 = 2026-08-12(짝수일).** 거기서도 무발행이면 원격 routine 자체가
  멈춘 것이다 → **사람이 claude.ai routines 등록 상태를 직접 확인해야 한다**(이 점검
  세션은 로컬 리포만 볼 수 있어 원격 등록 상태를 조회할 수단이 없다).
- **상태**: OPEN (2026-08-11 사전점검이 신설). 08-12 회차 결과로 CLOSE 또는 격상 판정.

## 🟡 반려 3회 도달 (2026-08-08 auto-run) — draft 강등 진행 중

- **run**: 2026-08-08 스케줄 auto-run. 소재 = M19 "화폐 전면 철거와 실물 마을"(M16~M18을
  배경으로 압축 서술, `verify_at c4c3431`). `state/blog_next_material_priority.md`의
  ACTIVE 지정(M16)을 기획팀이 브레인스토밍 게이트에서 재판단해 M19 단일 완결 밀스톤 +
  M16~M18 배경 서사로 재구성한 소재.
- **반려 3회 각각의 사유·시각(UTC)**:
  1. Step 3(검수팀) 1라운드 FAIL (~2026-08-08 08:05 UTC 추정) — 내부 링크 404(브리프
     `internal_link_hint` 오타), 시각 오차("27분"이라 썼으나 실제 1시간 44분), H2 "여섯
     단계" ↔ 본문 1~7단계 불일치, "직전 밀스톤" 귀속 오류(M17을 가리켜야 하는데 미명시).
  2. Step 3(검수팀) 2라운드 FAIL (~2026-08-08 08:16 UTC 추정) — 재검사(`cfe9c96`)와
     명세서(`6eec765`)의 선후·인과를 초안이 반대로 서술(재검사가 철거를 "확정"한 것처럼).
     **근본 원인은 기획팀 브리프**(`01_planner_brief.md:57~58`)가 두 커밋 시각을 둘 다
     "08-05 02:12"로 잘못 적은 것 — `6eec765`의 실제 시각은 00:55:18.
  3. Step 4(마스터 1차) REJECTED (~2026-08-08 08:33 UTC) — 검수팀이 advisory(반려 아님)로
     내렸던 2건을 마스터가 재검증 후 반려 사유로 격상: (a) 141행이 `Docs/CLAUDE.md:97`
     규칙을 따옴표로 축자 인용하듯 제시했으나 원문과 문구가 다름(뜻은 동일), (b) 109~113행
     "마지막 커밋"에 실제로는 다른 커밋(`5acc98e` W2, `3ef7457` W4) 소속 성과 2건이
     잘못 귀속됨 — 마지막 커밋 `c3f8eba` 본문이 스스로 이 소속을 명시.
- **판정**: 3회 누적(Step 3 ×2 + Step 4 ×1) — 오케스트레이터가 재작성 루프 중단 결정.
  글은 버리지 않는다. `.staging/02_draft.md`(반려 사유 ⑤까지 반영된 최신본, 11,586자)를
  그대로 blog-publisher에 draft 모드로 위임한다. `blog-editor`(Step 5)를 거치지 않았으므로
  **SEO 메타·라벨 없이 최소 HTML 변환만 적용한 초안**이다.
- **소재 미소비**: `blog_last_published_commit.md`·`blog_next_material_priority.md`는
  이 회차에서 갱신하지 않는다 (draft는 소재 소비가 아님 — blog-publisher.md Step 7 규정).
- **원인 스냅샷**: `.staging/*.md` 전체를 `state/incidents/2026-08-08/`에 보존함(아래
  참조) — 반려 3건의 판정서 원문(`03_review.md`, `04_verdict.md`)과 각 라운드 초안
  경과를 사후 추적 가능하게 함.
- **다음 기획팀에게**: 브리프 작성 시 커밋 시각은 반드시 `git log -1 --format=%ai <hash>`로
  직접 읽을 것 — 이번 회차 반려 2회차 원인이 브리프의 시각 오기였다.
- **상태 (경과, 2026-08-08 게시팀 Step 7)**: DRAFTED로 파이프라인 자체는 정상 종료.
  게시팀 Step 7이 실제로 Blogger 초안을 생성 완료함 — blogger_post_id
  `9034137679451863013` (blog `6014451945015572125`, gamedevclaude.blogspot.com),
  status: DRAFT. 초안 상태라 퍼머링크는 없음(`url` 필드가 블로그 홈으로 나옴), 관리자
  화면: https://www.blogger.com/blog/posts/6014451945015572125?hl=ko. 입력은
  `blog-editor`(Step 5)를 거치지 않은 `.staging/02_draft.md`(반려 3건 사유가 전부
  반영된 최신본, `blog-writer`가 재작성한 상태) 그대로이며, **blog-editor
  미경유(SEO 메타·라벨·이미지·광고 위치 없음)**. 문단 `<p>` · 소제목 `<h2>`/`<h3>` ·
  인용 `<blockquote>` · 강조 `<b>` 네 가지 변환만 적용했다. 본문에 있던 마크다운 표
  2개(밀스톤 요약표, "남긴 것" 표)는 변환기 범위 밖이라 파이프 문자 그대로 `<p>`에
  남아 있다 — 2026-08-02 항목에서 이미 관측된 동일 한계이며 이번에도 손대지 않았다
  (편집팀을 대행하지 않는다는 원칙 준수). `--labels` 인자에 빈 문자열을 넘기면
  `blogger-client.js`의 `parseArgs`가 이를 불리언 플래그로 오인해 `labels: ["true"]`가
  붙는 결함을 발견함 — 최초 게시(post_id `3121106430114146540`)에서 이 결함이 실제로
  발생해 즉시 DELETE로 제거하고, `--labels` 인자 자체를 생략한 재게시(post_id
  `9034137679451863013`, 현재 살아있는 초안)로 대체했다. 로컬 사본:
  `tools/blog-automation/published/2026-08-08-unity-goap-m19-currency-teardown-DRAFT-NOT-PUBLISHED.html`.
  **지시서 원칙에 따라 `blog_last_published_commit.md`·`blog_next_material_priority.md`는
  갱신하지 않았다** — 소재를 소비한 것이 아니므로 M19(및 배경 M16~M18)는 다음 회차에도
  ACTIVE 소재로 그대로 남아 있다(재검토 대상).
- **다음 로컬 세션에게 (부수 결함, 블로그와 무관)**: `scripts/blogger-client.js`의
  `parseArgs`가 `--flag ""`(빈 문자열 인자)를 값이 아니라 불리언 `true`로 해석해
  `String(true).split(',')` → `["true"]`라는 잘못된 라벨이 실제로 게시될 뻔했다.
  `--labels` 값이 빈 문자열일 수 있는 모든 호출부는 인자 자체를 생략하는 방식으로
  당장은 우회했지만, 근본 수정(빈 문자열과 플래그 부재를 구분)은 아직 안 됐다.


## 🟢 CLOSED — 2026-08-02 반려 3회 도달 (M12 "성격 축 — 성향 벡터") → draft 강등 → 정식 발행으로 해소

**해소 (2026-08-02 로컬 세션):** 해소 조건 (a)·(b) 둘 다 충족.

- **(a) `GoalSO.cs` 주석 정합성** — 정정 완료. 두 선택지("주석을 89로" / "`Weight`를
  1.0으로") 중 **주석 쪽을 고쳤다.** `Weight`를 올리면 게임 행동이 바뀌는데(고집쟁이
  95→93 등) 그건 블로그 회차가 결정할 사안이 아니기 때문이다. 덧붙여 재발 방지 문장을
  같은 툴팁에 넣었다 — "진폭은 boost의 상한이지 boost 자체가 아니다. 실제 boost =
  round(bias × PriorityScale)이고 bias에는 Weight가 곱해져 있다." 그리고 극단값(±100)
  예시를 **실재하는 성격**(고집쟁이 겁 -60 → 95)으로 교체했다. 원래 주석이 틀린 뿌리가
  "실제 주민에게 없는 ±100을 예시로 든 것"이었기 때문이다.
- **(b) M12 소재 정식 발행** — 2026-08-02 05:29 UTC 공개 발행 완료.
  https://gamedevclaude.blogspot.com/2026/08/unity-goap-claude-code-ai.html
  (`blogger_post_id` 4080593902932668278 — auto-run이 만든 DRAFT를 **그대로 공개
  전환**했다. 새 글로 다시 올리지 않아 고아 초안이 남지 않았다.)

**교정 내용 (마스터 2차 판정서 R3·R3-b 이행):** 초안 74행의 "겁 최고 125 / 최저 85"를
숫자 치환(121/89)이 아니라 **판정서의 권장안대로 실재 성격으로 다시 썼다** — 새침이
(겁 +30) 110 · 겁 0인 게으름뱅이/떠돌이 105 · 고집쟁이(겁 -60) 95. 121/89는 산술은
맞지만 ±100인 주민이 실재하지 않아 R3-b가 남는다. 로컬 세션이 에셋·코드로 유도식을
독립 재계산해 확인했다(`Goal_Flee.asset` Priority 105 / Trait 5 Weight 0.8 /
PriorityScale 20, `TraitVector.Bias`, `GoalSO.TraitBoost`, `AXIS_MAX = 100`).
부수로 171행 "계절마다 길이가 다르고"를 "제각각이고"로 다듬었다(온화·여름이 둘 다 3일).

**부수 수정 2건:**
1. `scripts/blogger-client.js`에 **`publish` 서브커맨드 신설.** `update`는 발행 상태를
   보존하므로, draft로 강등된 회차를 나중에 정식 발행할 경로가 아예 없었다. 이번에
   그 구멍 때문에 "새 글로 올리고 초안은 버린다"가 유일한 선택지가 될 뻔했다.
2. 마크다운 표 2개를 실제 `<table>`로 변환. auto-run의 최소 변환기(Step 5 미경유
   폴백)는 표를 파이프 문자 그대로 `<p>`에 흘려보낸다(`| 성격 | 근면 | …`). **DRAFTED
   폴백 경로를 쓸 때마다 재발하는 결함이므로, 다음에 이 경로를 탈 때 확인할 것.**

### 🟡 남은 관찰 (이번 반려의 구조적 원인 — 미해소)

**게이트가 부등식만 검사해 수치 오류를 못 잡는다.** `M12_T3_Flee_OutranksHungerExceptForTheReckless`는
121/89에서도 85/125에서도 green이다. 이번엔 마스터가 손으로 재계산해 잡았지만 자동으로는
안 잡힌다. 같은 종류(커밋 본문·주석의 수치가 에셋 정본과 어긋남)가 다른 goal에도 남아
있을 수 있다 — **전수 점검은 하지 않았다.**

### 이력 — 해소 전 원문 (2026-08-02 auto-run이 OPEN 상태로 작성. 이미 해소됐다)

- **run**: 2026-08-02 원격 routine auto-run (기록 시각 2026-08-02 05:10 UTC).
  소재 = M12 성격 축/성향 벡터, 커밋 범위 `2e2ce34`~`c1d87e2`, `verify_at: c1d87e2`.
- **반려 3건 (이번 사이클 합산 카운트)**:
  1. **미상** — 오케스트레이터가 보고한 반려 카운터 2/3 중 마스터 Step 4 1차를 뺀 나머지
     1건. `.staging/03_review.md`가 "재검수 #2"로 덮어써져 원문을 확인할 수 없었다.
     마스터가 근거 없이 내용을 추정해 적지 않는다. (재발 방지 메모: 검수 리포트는
     회차별로 보존하거나 append해야 3회 도달 시 사유를 재구성할 수 있다.)
  2. **Step 4 (마스터, 1차): REJECTED 2건** — 검수팀 PASS를 마스터가 에셋 정본으로
     재검증하다 발견.
     - R1: 초안 54행 "먹는 행동 계열 셋"이 실제 면제 3종(`Goal_P0_Hunger` "배고픔 해결",
       `Goal_P0_Fatigue` "**휴식**", `Goal_Snack` "허기 달래기") 중 **휴식을 누락**.
     - R2: 초안 171행 "계절 길이 8일"이 정본과 불일치. 정본은
       `Assets/M0Config/Seasons/Season_{Mild,Summer,Autumn,Winter}.asset:16 DurationDays`
       = 3/3/2/4 (한 바퀴 12일), `WorldConfigSO.cs:93 ProfilerIntervalDays = 7f`.
     → 작성팀이 두 곳 수정, 검수팀 재검증 PASS. **R1·R2는 마스터 2차 판정에서 해소 확인됨.**
  3. **Step 4 (마스터, 2차): REJECTED 1건 (R3)** — 초안 74행 피신 우선순위 수치
     "겁 최고 **125** / 겁 최저 **85**"가 에셋 정본 계산값과 불일치.
     - 정본(`git show c1d87e2:...`): `Goal_Flee.asset:16 Priority: 105`,
       `:28-29 Trait: 5 / Weight: 0.8`, `:30 PriorityScale: 20`;
       `TraitVector.cs Bias() = Clamp(Σ(v/100×w), -1, 1)`;
       `GoalSO.cs TraitBoost() = RoundToInt(Bias × PriorityScale)`.
     - ⇒ 겁 +100 → 105 + round(0.8×20) = **121**, 겁 -100 → **89**. 중립 105는 옳음.
     - 125/85의 출처는 커밋 `976fae8` **본문**이며, 그 커밋이 `PriorityScale 6→20`만 바꾸고
       `Weight: 0.8`은 그대로 둔 채 0.8을 곱하지 않은 **산술 오류**다. 같은 오류가
       `Assets/Scripts/M0/Data/GoalSO.cs`의 `PriorityScale` 툴팁 주석에 복제돼 있다.
     - 게이트 `M12_T3_Flee_OutranksHungerExceptForTheReckless`는 **부등식만** 검사하므로
       121/89에서도 green — 그린 상태가 125/85의 근거가 되지 못한다.
     - 부수(R3-b): ±100은 축의 이론적 극단이고 실제 6성격 중 해당 값이 없다. 실제 최댓값은
       새침이 겁 +30(→110), 최솟값은 고집쟁이 겁 -60(→95).

### ✅ 리포로 되돌려야 할 실제 결함 (블로그와 무관 — 게임 코드 쪽) — 2026-08-02 정정 완료

이번 반려는 블로그 초안의 오기이지만, **뿌리는 리포지토리 자체에 있다.**
`Assets/Scripts/M0/Data/GoalSO.cs`의 `PriorityScale` 툴팁이
"피신(105, 진폭 20)은 겁이 최저인 주민을 **85**로 내려"라고 적고 있으나 실제 계산값은
**89**다(`Goal_Flee.asset`의 `Weight: 0.8` 미반영). 주석과 커밋 본문이 서로를 인용하며
틀린 값을 재생산하고 있고, 게이트가 부등식만 보므로 앞으로도 자동으로는 안 잡힌다.
**다음 개발 세션에서 주석을 89로 정정하거나, 의도가 85였다면 `Weight`를 1.0으로 올릴 것.**
(이 항목이 정리되기 전까지 같은 지점이 블로그 회차마다 반복 반려될 수 있다.)

- **조치**: 재작성 루프 중단. 게시팀(blog-publisher)에 **draft 모드** Step 7 위임 —
  마지막 초안(`.staging/02_draft.md`, R1·R2 수정 반영본)을 Blogger **초안** 상태로 올린다.
  사람의 승인을 기다리지 않는다. draft 게시도 실패하면 조용히 종료.
- **해소 조건**: (a) 위 `GoalSO.cs` 주석 정합성 정리, (b) M12 소재가 다음 사이클에서
  정식 발행되면 이 항목을 CLOSED로 내린다.
- **판정서 원본**: `tools/blog-automation/.staging/04_verdict.md` (사이클 중 덮어써짐 주의).

- **상태 (경과, 2026-08-02 게시팀 Step 7)**: DRAFTED로 파이프라인 자체는 정상 종료.
  게시팀 Step 7이 실제로 Blogger 초안을 생성 완료함 — blogger_post_id
  `4080593902932668278` (blog `6014451945015572125`, gamedevclaude.blogspot.com),
  status: DRAFT. 초안 상태라 퍼머링크는 없음(`url` 필드가 블로그 홈으로 나옴), 관리자
  화면: https://www.blogger.com/blog/posts/6014451945015572125?hl=ko. 게시된 본문은
  blog-editor(Step 5)를 거치지 않은 `.staging/02_draft.md`(R1·R2 반영본)를 최소 HTML
  변환(문단 `<p>` · 소제목 `<h2>`/`<h3>` · 인용 `<blockquote>` · 강조 `<b>` 네 가지만)만
  적용한 것이며, **blog-editor 미경유(SEO 메타·라벨·이미지·광고 위치 없음)**. 위 R3(피신
  우선순위 겁 최고/최저 125/85 수치) 반려 사유도 아직 반영되지 않았다 — 초안 그대로 올린
  것이므로 사람이 검토할 재료이지 발행물이 아니다. 게시 직후 `view=ADMIN` GET으로 재조회해
  title·content가 로컬 제출본과 바이트 단위(문자열 완전 일치, 18,635자, U+FFFD 없음)로
  동일함을 확인함. 로컬 사본:
  `tools/blog-automation/published/2026-08-02-unity-goap-m12-trait-vector-DRAFT-NOT-PUBLISHED.html`
  (파일명·내용 모두에 DRAFT 표시). **지시서 원칙에 따라 `blog_last_published_commit.md`·
  `blog_next_material_priority.md`는 갱신하지 않았다** — 소재를 소비한 것이 아니므로
  M12는 다음 회차에도 ACTIVE 소재로 그대로 남아 있다(재집필 대상).

## 🟢 CLOSED — 2026-07-28 반려 3회 도달 (M10 "야생 위협과 방랑자") → draft 강등 → 정식 발행으로 해소

- **run**: 2026-07-28 원격 routine auto-run. 소재 = M10 "야생 위협과 방랑자"
  (`db82ffd`~`caa23b0`), `state/blog_next_material_priority.md` STATUS: ACTIVE로
  지정된 소재.
- **반려 3건 (이번 사이클 합산 카운트, 3번/4번/6번 반려 대상)**:
  1. Step 3 (검수팀, 1차): FAIL — 인트로 첫 문단이 최근 3편(특히 2026-07-26 M9)과
     문장 1·2·4가 자구까지 동일(4편 연속 동일 골격). 부수로 41행 "치료사 직업이
     코드 없이 에셋 하나로 생겼다"가 `b255063`의 `JobSO.cs +4`(TendRecoveryMult
     신설) diff와 어긋남(과장) 지적.
  2. Step 4 (마스터, 1차): REJECTED — 검수팀 PASS를 마스터가 직접 재검증하며
     화면 인용문 2건이 실제 코드와 불일치 발견: (D1) 방랑자 프롬프트 인용이
     `WandererService.cs`의 실제 조립 문자열과 다름(성격/직업 순서 역전, HUD
     알림과 프롬프트 두 문자열을 한 줄로 합침, 수락 시한 누락), (D2) 부상자 거절
     대사 "지금은 몸이…"가 게임에 없는 문자열(명세서 코드 스케치 주석 속
     플레이스홀더를 실제 대사로 오인 인용, 실제는 `AgentConfigSO.cs`
     `InjuredLines`).
  3. Step 3 (검수팀, 2차 재검수): FAIL — D1 수정이 절반만 반영됨. 성격 표시명이
     실제 에셋 값 `"새침이"`(Personality_Prickly.asset DisplayName)가 아니라
     `"새침"`으로 남았고, 수락 시한이 실제 값 `0.7일`이 아니라 `"(…일 내)"`
     placeholder로 남음. 참고: 마스터 판정서(`04_verdict.md`)가 이 지점에서
     `"새침"`으로 오기해, 작성팀이 판정서를 따라 고치며 잔여 불일치가 생김 —
     **원인은 작성팀이 아니라 판정서 오기**였음을 기록.
- **판단**: 3건 모두 "존재하지 않는 자구/화면 문자열을 실제인 것처럼 인용"하는
  동일 계열의 정확성 결함이며, 마지막 미해결분(D1 잔여)은 코드 에셋 값 2개
  치환뿐인 사소한 수준. 그러나 반려 카운터가 정확히 3에 도달했으므로 지시서 규정대로
  재작성 루프를 여기서 중단하고, 글을 버리지 않는 방식(게시팀 Step 7 `--draft` 위임)으로
  전환한다. 사람의 승인은 기다리지 않는다(지시서 원칙).
- **다음 조치 제안 (사람이 원할 경우)**: `tools/blog-automation/.staging/02_draft.md`
  105·111행의 `"새침"` → `"새침이"`, 105행의 `"(…일 내)"` → `"(0.7일 내)"`로 수기
  교정 후 Blogger 초안을 직접 공개 전환하면 별도 사이클 없이 발행 가능. 근거 값은
  `Assets/M0Config/Personalities/Personality_Prickly.asset`(DisplayName) +
  `Assets/M0Config/WorldConfig.asset`(WandererWaitDays: 0.7).
- **상태 (경과)**: DRAFTED로 파이프라인 자체는 정상 종료. 게시팀 Step 7이 실제로 Blogger
  초안을 생성 완료함 — blogger_post_id `4321240890939765189` (blog
  `6014451945015572125`, gamedevclaude.blogspot.com), status: DRAFT. 초안 상태라
  퍼머링크는 없음, 관리자 화면: https://www.blogger.com/blog/posts/6014451945015572125?hl=ko.
  게시된 본문은 blog-editor(Step 5)를 거치지 않은 `.staging/02_draft.md`를 최소 HTML
  변환(문단 `<p>`, 소제목 `<h2>`, 인용 `<blockquote>`)만 적용한 것이며, 위에 적힌 잔여
  결함 2곳도 아직 반영되지 않았다. `blog_last_published_commit.md`·
  `blog_next_material_priority.md`도 당시 DRAFT 상태로 갱신함.
- **해소 (2026-07-28 로컬 세션)**: 사용자가 위 Blogger 초안(`4321240890939765189`)을
  Blogger에서 직접 삭제함. 파이프라인 지시서(검수팀·마스터·기획팀 브리프 등) 6개 커밋을
  잔여 결함 원인(방랑자 프롬프트 자구 오류·명세서 코드 스케치 오인용) 재발 방지 방향으로
  수정한 뒤, 같은 소재(M10, `db82ffd`~`caa23b0`)로 blog-editor(Step 5)를 정식으로 거쳐
  재집필했다. 재검수·재승인 결과 반려 0회로 Step 3(검수팀)·Step 4(마스터 1차)·Step 6
  (마스터 2차) 전부 APPROVED 통과, 게시팀 Step 7에서 정식 공개 발행 완료
  (blogger_post_id `1047866260477949128`, status: LIVE,
  https://gamedevclaude.blogspot.com/2026/07/unity-goap-claude-code-ai.html). 로컬 사본
  `tools/blog-automation/published/2026-07-28-unity-goap-m10-wolf-injury-wanderer.html`은
  아침 draft 강등분 내용을 이번 정식 발행분으로 덮어씀. `blog_last_published_commit.md`
  (`latest_commit: caa23b0`, `publish_status: PUBLISHED`)·`blog_next_material_priority.md`
  (`STATUS: CONSUMED`)도 정식 발행 결과로 갱신함. **해소 조건 충족 → CLOSED.**

## 🟢 CLOSED — 2026-07-16 REJECTED_3X: M2+M3 소재 발행 실패 (검수 3연속 반려) → 해소

- **run**: 2026-07-16 13:03 KST auto-run. 소재 = M2 생산체인 + M3 주거 기반
  (`651ea47` ~ `153f180`). 당시 발행 안 됨 — latest_commit cc4602e 유지, 소재 미소비.
- **원인 (routine 자체 진단)**: 작성팀과 검수팀의 **분량 계측 방식 불일치** — 기준 문서에
  "4000~5500자"라는 숫자만 있고 셈법(공백 포함 여부, 한글만 셀지 등)이 정의돼 있지 않아,
  작성팀 기준으로는 충족인 초안이 검수팀 셈법으로는 미달 → 반려 3회 → REJECTED_3X.
- **조치 (2026-07-16 로컬 적용)**: blog-writer.md·blog-reviewer.md에 계측 명령을
  `wc -m`(공백·마크다운 포함 전체 문자수) 하나로 통일 명시. 반려 발동은 3,800자 미만/
  6,000자 초과일 때만 — 사소한 오차로 반려 왕복 금지.
- **해소 (2026-07-16 로컬 세션)**: 계측 기준 통일 후 동일 소재로 재작성·재검수를 거쳐
  마스터 Step 4/Step 6 승인 모두 통과, 게시팀 Step 7에서 실제 발행 완료.
  blogger_post_id 3935987342991362953, url
  https://gamedevclaude.blogspot.com/2026/07/unity-goap.html. 상세는
  `blog_last_published_commit.md` 최신 항목 참조. **해소 조건 충족 → CLOSED.**
- 참고: routine이 sandbox에서 커밋한 db77faf는 push 403으로 소실 — 이 항목은
  MANUAL_STATE_UPDATE 기반 로컬 재구성임.

## 🟢 CLOSED — 원격 sandbox state push 403 → GitHub API 직접 커밋으로 해소 (2026-07-16)

- **해소 확인**: 2026-07-16 16:33 KST 수동 run(session `cse_01GuTZ7uGzNG8prQ1zcCb4rB`)에서
  `GH_STATE_TOKEN` env var + `scripts/gh-state-push.js` API 경로로 상태 커밋 `48ea8b8`
  (`chore(blog): auto-run state update`)이 main에 자동 반영됨 — 해소 조건 충족.
  같은 run에서 M2+M3 글 발행도 성공 (post_id 3935987342991362953).
- **최종 원인 요약** (아래 이력 참조): 샌드박스 GitHub 프록시는 push를 "세션의 현재 작업
  브랜치"로만 허용하는데, routine 세션은 detached HEAD라 세션 소유 브랜치가 없음 →
  routine에서 git push는 전 형태 불가. 해법 = git 프록시를 우회하는 REST API 직접 커밋
  (fine-grained PAT, AI_GOAP 단독·Contents R/W).
- **운영 주의**: PAT 만료(발급일로부터 설정 기간) 시 API 경로가 죽고 MANUAL_STATE_UPDATE
  폴백으로 되돌아간다 — 만료 임박 알림을 받으면 재발급 후 env var 교체.
- **관찰 (🟡 무해)**: 07-16 run에서 routine이 state 파일 외에 devlog 커밋 2건도 같은
  토큰으로 API 커밋함 (df0dcf3, 4e03140). gh-state-push.js의 경로 제한은 스크립트 안의
  가드일 뿐 토큰 권한은 리포 전체 Contents R/W이므로 모델이 자체 API 호출로 우회 가능.
  devlog는 파이프라인의 정규 기록 대상이라 문제없으나, 이상 커밋이 보이면 이 지점을 의심.

### 이력 (해소 전 기록)

- **증상**: 원격 auto-run이 게시 성공 후 상태 커밋을 `claude/state-*` 브랜치로 push하면
  GitHub가 403으로 거부. 브랜치가 sandbox 밖으로 나오지 못해
  `blog-state-auto-merge.yml`도 발동하지 않음.
- **발생 이력**:
  - 1회차: 2026-07-14 (M1 발행 회차) → 로컬 수동 반영으로 복구
  - 2회차: 2026-07-15 (`claude/state-2026-07-15T040738Z`, M0 회고 특집 회차) →
    2026-07-15 로컬 수동 반영으로 복구. HTML 사본은 Blogger API GET으로 재획득
    (post_id 6764155466991758383, 13,826 bytes).
  - 3회차: 2026-07-16 (REJECTED_3X 회차, alerts 커밋 db77faf 미push). **결정적 증거 확보**:
    1차 `git push origin HEAD` 시도에서 세션이 **detached HEAD**임이 드러남 + 403.
    → routine 세션은 세션 소유 작업 브랜치가 아예 없으므로, "현재 작업 브랜치만 허용"
    규칙 하에서 **routine의 git push는 전 형태 불가능이 확정**. 07-15에 넣은
    "현재 브랜치 push" 1차 경로는 routine에는 해당 없음 (판정: 가설 반증).
- **현재 우회책**: routine이 발행 결과를 MANUAL_STATE_UPDATE 블록으로 출력 → 사용자가
  로컬 세션에 전달 → 로컬에서 상태 파일 갱신 + main에 직접 커밋. (이 절차는 07-14부터
  routine 프롬프트에 내장됨 — 정상 작동 확인)
- **근본 원인 (2026-07-15 확정)**: 클라우드 샌드박스의 GitHub 프록시는 push를 **세션의
  현재 작업 브랜치로만** 허용한다 (공식 문서 claude-code-on-the-web "GitHub proxy" 절:
  "Restricts git push operations to the current working branch for safety"). routine의
  구 Step 8은 세션 도중 `git checkout -b claude/state-*`로 **새 브랜치를 만들어** push했기
  때문에 prefix와 무관하게 403이었다. "claude/* prefix면 push 허용"이라던 07-11의 전제는
  문서 규칙과 다른 오해였고, 당시 Path B 검증(claude/state-workflow-test)은 **로컬 PC에서
  push**한 것이라 샌드박스 프록시를 통과 검증한 적이 없다. GitHub 리포 쪽 설정은 무관
  (룰셋 0개, main 브랜치 보호 없음 — 2026-07-15 gh api로 확인).
- **1차 수정 (2026-07-15) — 반증됨**: "현재 작업 브랜치에 push origin HEAD" 경로를
  넣었으나, 07-16 run에서 routine 세션이 **detached HEAD**임이 확인되어 이 경로는 routine에
  적용 불가 (stderr 원문 확보 목적은 달성 — 이 증거로 진단 완결).
- **2차 수정 (2026-07-16, 검증 대기 — PAT 필요)**: git 프록시를 우회하는 **GitHub REST API
  직접 커밋** 경로 구현.
  1. `scripts/gh-state-push.js` 신설 — blob→tree→commit→`PATCH refs/heads/main`(force
     아님 = ff만). 상태 경로(`tools/blog-automation/{state,published}/`) 외 파일은 무시.
  2. `routine-prompt.md` Step 8 — 1순위 API 경로(`GH_STATE_TOKEN` env var 필요), 실패 시
     레거시 git push 폴백 + MANUAL_STATE_UPDATE.
  3. **사용자 작업 필요**: GitHub fine-grained PAT 발급 (리포 jhyun1234/AI_GOAP 단독,
     권한 Contents: Read and write 만) → claude.ai 환경(env_011dy96U4KfgKbckWYWVqzN1)
     env vars에 `GH_STATE_TOKEN`으로 저장 (BLOGGER_* 넣은 곳과 동일 UI).
- **검증 계획**: PAT 저장 후 다음 run에서 `STATE_PUSH_OK (api)` + main에
  `chore(blog): auto-run state update` 커밋 자동 등장 확인 → 이 항목 CLOSED.
  API 경로도 403이면(프록시가 api.github.com Authorization을 가로채는 경우) 최후 수단은
  상태 저장소를 git 밖(Blogger DRAFT/Google Drive)으로 옮기는 구조 변경.
- **부수 기록**: 2026-07-15 in-sandbox 진단 세션 1회 시도 (trig 임시 생성,
  session `cse_01K1SwR3ms78eGuoLKUm9bSX`) — 보고 채널(Blogger DRAFT) 미도착으로 결과 미회수,
  GitHub 부수효과(브랜치/커밋/이벤트) 전무. 세션 자체가 실행 안 됐거나 조기 실패한 것으로
  추정. 진단 트리거는 삭제 예정.
- **해소 조건**: 원격 auto-run의 state push가 성공해 main 자동 반영까지 통과하는 회차가
  1회 확인되면 이 항목을 CLOSED로 내린다.
