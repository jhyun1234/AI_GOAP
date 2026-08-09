---
name: review-method-wide-longform
description: 롱폼(format:"wide") 검수 절차 — 정본은 Docs/롱폼_대본_문법.md, 클립 샷은 카탈로그가 아니라 스틸로 판정, 게임 HUD 겹침은 엔진 스틸로만 보인다
metadata:
  type: feedback
---

`scene.json` 에 `format:"wide"` 가 있으면 판정 정본은 **`Docs/롱폼_대본_문법.md`** 다.
쇼츠 규격(4단 구조·30~35초·예고 사슬·제목 이행)으로 반려하지 마라 — `check.mjs` 도
그 항목들을 「wide — 쇼츠 규격 비대상」으로 통과시킨다.

**Why:** lf01(2026-08-09) 검수에서 확립. 쇼츠 잣대를 대면 멀쩡한 회차가 무더기로 걸린다.

**How to apply:**

1. **영어 자막은 대상이 아니다.** 「en 0줄이면 검수가 반려」 규칙은 쇼츠 것이다 —
   `Docs/롱폼_영상_트랙_실행명세서.md` 37행이 *"영어판(`--lang en`) 롱폼 — 한국어 파일럿
   먼저"* 를 **비목표**로 못 박았다. wide 회차의 `en` 0줄은 정상이다.

2. **길이는 마디별로 잰다.** §1 = 콜드오픈 30~60초 · 챕터 4~6개 각 1~2분 · 마무리 ~1분 ·
   총 300~600초. 산정은 `check.mjs` 의 「산정 길이 (참고)」 식(CPS_REF 6.7 · SHOT_TAIL 0.35 ·
   구두점 제외, 오차 ±2.0초)을 **그대로 복제해 샷별로** 돌린다. 총계가 그 줄과 일치하면
   마디 합산도 믿을 수 있다. 클립 샷은 자막 2줄로 묶여 4~6초라 **콜드오픈이 쉽게 미달한다.**

3. 🔴 **클립 채택은 카탈로그 `detail` 로 판정하지 마라 — 스틸을 떠라.**
   `node tools/scene-video/clips.mjs stills <id>` → `D:\AI_GOAP-videos\clips\library\stills\`
   에 `-a`(0s)·`-b`(durSec/2)·`-c`(durSec−0.2) 3장. lf01 에서 `forecast-…-58` 은
   `detail = Threat_Tier1_Wolf` 인데 **어느 프레임에도 늑대 예고줄이 없었고**,
   `gothome-…-12` 는 입주가 아니라 밭일 장면이었다. `detail` 은 사건 이름이지 프레임 안에
   그것이 있다는 보증이 아니다. camDist 가 크면 앞 몇 초가 빈 들판이다.

4. 🔴 **게임 HUD 겹침(§4, 반려 사유)은 클립 스틸만으로는 못 본다 — 엔진 스틸을 떠라.**
   `node tools/scene-video/render.mjs <ep> --still 4,227,362,538` →
   `episodes/<ep>/build/stills/<sec>s.png`. 시각은 임시 타이밍
   (`PROVISIONAL_CPS 5.6` · `LINE_TAIL 0.5` · `SHOT_TAIL 0.35`, engine.js 14~15행)으로 역산한다.
   `wide.css` 의 `.hud{top:3%}` 띠(제목 + aiHook + 진행바)가 게임 좌상단 알림줄
   (최대 4~5줄)과 같은 높이라 **제목을 짧게 잡아도 안 풀린다 — 문제는 폭이 아니라 y 좌표다.**
   특히 "저 줄을 새로 얹었습니다"(M13 상태줄)·"늑대 무리 1일 뒤" 처럼 **말이 그 줄을
   가리키는 샷**에서 치명적이다.

5. **시점 규칙(§6)** — 과거는 완결형, 미래형은 「지금」·「북극성」에만.
   「전말은 시리즈에서」 유도는 **궤도**만 보면 되지만(발행 글 또는
   `tools/blog-automation/BLOG_COVERAGE.md` 대기열), **시제**는 따로 본다.
   「쇼츠 시리즈에 있습니다」는 아직 안 만든 회차엔 거짓이다 —
   `tools/scene-video/state/schedule.json` 의 `order` + `episodes/<id>` 폴더 실재로 확인.

6. 🔴 **TTS 전에 나오는 클립 정적 경고는 대부분 가짜다.** `timed.json` 이 없으면 엔진이
   `PROVISIONAL_CPS 5.6` 으로 길이를 잡는데, 그게 실측(≈6.7)보다 훨씬 길어 **클립 창
   (`out−in`)을 넘기고 꼬리가 언다.** 판정 전에 샷마다 `out−in` vs 산정 길이(6.7)를
   대조하라 — 창이 더 크면 TTS 뒤 사라질 경고다. lf01 2차 실측: 7건 중 6건이
   「임시 길이 − 창」과 오차 0.4초 안에서 일치했다. 예외는 게임 화면 자체가 멈춘 컷
   (`broll` 계열 — 0.2초 간격 `m = 0.000005`)인데 그건 그 샷의 설계이기도 하다.

7. 🔴 **화면 오브젝트의 개수·방향을 말하는 자막은 색으로 세서 검산하라.** 낱말이 맞아도
   수와 방향이 틀리는 것이 이 트랙의 반복 결함이다(ep11s · lf01 R6). 무덤 =
   `Assets/Scripts/M0/SimulationLoop.cs:668` `Color(0.45,0.45,0.5,0.9)` 이고 게임 전체에서
   그 회색을 쓰는 것은 무덤뿐이다(`grep "sr.color" Assets/Scripts/M0/` 전수). 스틸을
   480×270 으로 줄여 색 범위로 군집을 세면 개수와 x 좌표가 한 번에 나온다.
   🔑 **셀 때 쓰는 스틸은 그 자막이 떠 있는 시각의 엔진 스틸**이어야 한다 —
   `clips.mjs stills` 의 `-a/-b/-c` 는 `durSec/2` 기준이라 샷이 실제로 쓰는
   `in`~`out` 창 밖일 수 있다(`forecast-99` 는 `in:6` 이라 `-a`·`-b` 가 창 밖).

관련: [[review-method-stale-timed-json]] · [[feedback-no-length-rejection]] ·
[[review-method-recycling-check]] · [[review-method-self-report-is-not-evidence]]
