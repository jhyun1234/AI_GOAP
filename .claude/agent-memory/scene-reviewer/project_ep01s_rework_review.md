---
name: ep01s-rework-review
description: ep01s 그림 재제작 검수 종료 — 1차 반려(spoke↔ep00s channels 장치 중복) 후 pins 교체로 2차 통과
metadata:
  type: project
---

# ep01s 재제작 검수 — 종료 (2026-07-29) · 2차 통과

**누적 반려 1회(상한 3회).** 산출 =
`episodes/ep01s/notes/review.md`(2차 통과) · `review-v1.md`(1차 반려) ·
`tools/scene-video/.staging/03_review_report.md`(마스터 인계).

**1차 반려 사유(유일)**: `spoke`(S1B)가 `ep00s/kinds/channels.js` 와 장치가 같았다 —
한 출발점→네 갈래→하나만 채워지고 셋은 빈 채 + 값 3을 움직이는 표식 3개 +
하단 가운데 mono 로그 캡션. 배치(평행 관 vs 방사 십자)만 달랐다.
가중 사유 = `writer.md` 가 *"그림은 하나도 가져오지 않았다"* 를 **대조 없이 단언**.

**2차 해소**: `pins` 신규 교체. 배치가 아니라 **"네 갈래" 구조 자체를 폐기** —
`pins.js` 에 네 개짜리 그릇이 없고 `lanes`(3, S1 계승) + `unused`(3, 안 쓰인 이름표 더미)
구성이다. 값 3은 배열 원소가 아니라 *이름표가 세 장이라는 사실*(원문 `FallbackCounter=3`
= 주민 셋). 내가 열거한 겹침 8항목 중 6개 구조 해소, 2개(인용 로그 문자열·숫자 자체)는
원문 강제라 바꾸면 오히려 수치 정직성 위반이 된다 — 그래서 더 요구하지 않았다.

**부수 효과**: S1→S1B 되받기가 새로 생겼다(같은 레인 y·벽 x=209·주기·`LEAD=1.9`).
이제 rebound 골격이 S1·S1B·S9 세 샷에 쓰이는데 **통과로 봤다** — 사용자 판정 대상은
회차 *간* 돌려막기이지 회차 안 모티프가 아니고, 벽이 열리는 건 S9 뿐이라 payoff 유지.

**Why:** 사용자 판정 *"비슷한 애니메이션을 돌려막기하는 건 내가 원하는 방향이 아니야"* 가
이 재제작의 존재 이유. 인접 회차 간 장치 중복이 그 목적을 정면으로 거스른다.

**How to apply:** ep02s 이후 검수 때 **ep01s 의 10종(rebound pins budget nosolve sprawl
shortfall shelf latch teardown erasure)도 대조 대상**에 넣는다. 회차가 쌓일수록 대조표가
길어지므로 [[review-method-recycling-check]] 의 절차를 그대로 쓴다.

관련 = [[review-method-recycling-check]] · [[review-method-shot-timing]]
