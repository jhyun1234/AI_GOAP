# Memory Index

## 판정 이력
- [ep00s = APPROVED (2026-07-29)](verdict_ep00s.md) — 누적 반려 1회. DRAFT 원문 url null 예외 + 엔진 변경 파급 검증 절차
- [ep01s 재제작 = APPROVED (2026-07-29)](verdict_ep01s.md) — 그림 10종 전면 교체. 돌려막기 판정법 + 다음 회차 금지 3건
- [ep02s = APPROVED (2026-07-30)](verdict_ep02s.md) — 무인 첫 완주. 렌더 계층 화이트리스트로 정직성 규칙. 미참조 kind 2건 잔존
- [ep02s 재작업 = APPROVED (2026-07-30)](verdict_ep02s_redo.md) — 무효 사유로 잘린 분량 복원. 화면 문자열 기계 전수 대조 + "규정 없음"은 통과 근거가 아니다
- [정적 구간 3초 초과 = 1회성 예외](project_static_segment_exception.md) — ep00s 에서 3건 넘김. 다음 회차가 선례로 인용하면 반려
- 🔴 [사건 단위 재분할 여섯 편 = main 머지 (2026-08-04, PR #10)](../scene-reviewer/project_review_log.md) — ep04s-1/-2/-3 · ep05s-1/-2/-3. 2차 검수까지 완료. 여섯 편 모두 check.mjs 에서 환경 예외(timed.json 없음) 하나만 남음

## 🔴 2026-08-04 이후 달라진 규칙 (판정 전에 확인)
- **길이 정본은 W3 하나** — 상한 50초(fail) · 권장 30~45초(warn). 옛 "상한 없음"은 폐기.
  권장대역 **아래**는 반려 사유가 아니다(`ep04s-2` 26.7초 = 사용자 확정, 사유는 그 편 `notes.길이`).
- **훅은 캔버스에 안 그린다** — 아웃트로 카드의 `.oc-hook` 이 맡는다. `spec.hookCue` 폐기.
  kind 에 훅 블록이 남아 있으면 반려. 걷어낸 자리의 `fade`·주석·`reads` 도 같이 봐야 한다.
- **`latch` 에 고유 `seed` 필수** — 없으면 다른 회차와 파형 상관 0.99. `dur` 만 다른 건 안 갈린 것이다.
- **형제 라벨 = 그 편 `youtube.title` 에서 꼬리만 뗀 것** — `check.mjs` 가 검사한다.
- 🔴 **합성 `timed.json` 이 남아 있으면 지운다** — `"voice": "SYNTHETIC-NOT-REAL"`.
  남으면 그 회차 길이 게이트가 **지어낸 값으로 통과**한다. 검수가 만들고 안 지운 전례가 있다.

## 검증 절차
- [그림 회차 소유 = 회귀 검증 4단계](project_kind_ownership_check.md) — kind 해석 경로 확인법 + ep01 의 기존 🔴 실패는 무관하다는 것
- [ep02s 부분 재작업 = APPROVED (2026-07-30)](verdict_ep02s_fix.md) — 렌더 후 국소 수정 판정 4단계 + "밀어내기→소멸"이 reads 를 깨는가
- [효과음 판정 4항목](verdict_ep02s_sfx.md) — ep02s 효과음 = APPROVED. check.mjs 에 sfx 검사가 0건 = 기계 게이트 없는 유일한 계층
- [delayMs 는 timed.json 종속](procedure_sfx_retiming_fragility.md) — tts 재실행이 효과음 6개를 통째로 무효화. `t` 기반 그림은 앞줄 길이 합에 매달린다
