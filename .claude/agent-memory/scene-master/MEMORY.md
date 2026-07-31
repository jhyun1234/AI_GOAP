# Memory Index

## 판정 이력
- [ep00s = APPROVED (2026-07-29)](verdict_ep00s.md) — 누적 반려 1회. DRAFT 원문 url null 예외 + 엔진 변경 파급 검증 절차
- [ep01s 재제작 = APPROVED (2026-07-29)](verdict_ep01s.md) — 그림 10종 전면 교체. 돌려막기 판정법 + 다음 회차 금지 3건
- [ep02s = APPROVED (2026-07-30)](verdict_ep02s.md) — 무인 첫 완주. 렌더 계층 화이트리스트로 정직성 규칙. 미참조 kind 2건 잔존
- [ep02s 재작업 = APPROVED (2026-07-30)](verdict_ep02s_redo.md) — 무효 사유로 잘린 분량 복원. 화면 문자열 기계 전수 대조 + "규정 없음"은 통과 근거가 아니다
- [정적 구간 3초 초과 = 1회성 예외](project_static_segment_exception.md) — ep00s 에서 3건 넘김. 다음 회차가 선례로 인용하면 반려

## 검증 절차
- [그림 회차 소유 = 회귀 검증 4단계](project_kind_ownership_check.md) — kind 해석 경로 확인법 + ep01 의 기존 🔴 실패는 무관하다는 것
- [ep02s 부분 재작업 = APPROVED (2026-07-30)](verdict_ep02s_fix.md) — 렌더 후 국소 수정 판정 4단계 + "밀어내기→소멸"이 reads 를 깨는가
- [효과음 판정 4항목](verdict_ep02s_sfx.md) — ep02s 효과음 = APPROVED. check.mjs 에 sfx 검사가 0건 = 기계 게이트 없는 유일한 계층
- [delayMs 는 timed.json 종속](procedure_sfx_retiming_fragility.md) — tts 재실행이 효과음 6개를 통째로 무효화. `t` 기반 그림은 앞줄 길이 합에 매달린다
