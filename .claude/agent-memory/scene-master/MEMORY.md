# Memory Index

## 판정 이력
- [ep00s = APPROVED (2026-07-29)](verdict_ep00s.md) — 누적 반려 1회. DRAFT 원문 url null 예외 + 엔진 변경 파급 검증 절차
- [ep01s 재제작 = APPROVED (2026-07-29)](verdict_ep01s.md) — 그림 10종 전면 교체. 돌려막기 판정법 + 다음 회차 금지 3건
- [ep02s = APPROVED (2026-07-30)](verdict_ep02s.md) — 무인 첫 완주. 렌더 계층 화이트리스트로 정직성 규칙. 미참조 kind 2건 잔존
- [ep02s 재작업 = APPROVED (2026-07-30)](verdict_ep02s_redo.md) — 무효 사유로 잘린 분량 복원. 화면 문자열 기계 전수 대조 + "규정 없음"은 통과 근거가 아니다
- [정적 구간 3초 초과 = 1회성 예외](project_static_segment_exception.md) — ep00s 에서 3건 넘김. 다음 회차가 선례로 인용하면 반려
- [ep10s-1 = 1차 반려 → 2차 APPROVED (2026-08-08, 누적 2회에서 정지)](verdict_ep10s_1.md) — seed 한 값으로 해소. 척도 확정 + 🔴 되돌림 지시 「일부 이행」은 다음부터 반려
- 🔴 [사건 단위 재분할 여섯 편 = main 머지 (2026-08-04, PR #10)](../scene-reviewer/project_review_log.md) — ep04s-1/-2/-3 · ep05s-1/-2/-3. 2차 검수까지 완료. 여섯 편 모두 check.mjs 에서 환경 예외(timed.json 없음) 하나만 남음
- [ep10s-3 = APPROVED (2026-08-08)](verdict_ep10s3.md) — 막 전환 예고 URL 대조법 + sfx 파형 실측(sweep 0.02 · latch 0.77) + 「다시 세었다」 명단 4번째 오류 + 선례 인용 불가 3건
- [cue 타이밍 직접 검산 4줄](procedure_cue_window_math.md) — 창 분모는 `rel`, 어절은 `dur`, 카드는 `TOTAL−2600`. 둘을 섞으면 pauseAfter 만큼 밀린다
- [ep10s-2 = APPROVED (2026-08-08)](verdict_ep10s_2.md) — 반려 0회. sfx 대조표가 두 라운드 연속 틀렸는데 값은 살아남음. freq 전수는 손으로 세지 말 것 + 선례 금지 2건
- 🔴 [ep11s = APPROVED (2026-08-08, 누적 2회에서 정지)](verdict_ep11s.md) — 4막 개막. 예고는 「다음 편이 **버린 것**」과도 대조 + 글자 복제는 런 개수로 증명 + 선례 금지 5건
- 🔴 [ep12s-1 = APPROVED (2026-08-08, 마스터 반려 0)](verdict_ep12s_1.md) — 앞 편 예고와 사건이 어긋날 때의 인계 3조건 + **검수 상관 수치가 재현 안 된 첫 사례**(다음부터 반려) + 선례 금지 5건
- 🔴 [ep12s-2 = APPROVED (2026-08-08, 누적 반려 1회)](verdict_ep12s_2.md) — 「규칙을 낳은 회차가 그 규칙을 어겼다」로 파이프라인 문제 판정 + 정적 구간은 fps 불변이 아니다 + 선례 금지 4건
- 🔴 [ep12s-3 = APPROVED (2026-08-08, 마스터 반려 0)](verdict_ep12s3.md) — 어미 3연속을 **파이프라인 문제로 승격**(9편 중 5편·게이트 0건) + 예고 연출 규칙 문구 교정 + kind 쌍 중복은 비둘기집

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
- 🔴 [효과음 돌려막기 = **절대 표본 정렬** 파형 상관으로 판정](procedure_sfx_waveform_correlation.md) — 「정규화 상관」이라 쓰지 말 것(두 뜻으로 읽혀 사고남). 정규화 위치 리샘플은 이 결함을 못 잡는다
- [delayMs 는 timed.json 종속](procedure_sfx_retiming_fragility.md) — tts 재실행이 효과음 6개를 통째로 무효화. `t` 기반 그림은 앞줄 길이 합에 매달린다
- 🔴 [다음 편 예고 = 3단계 대조](procedure_next_preview_fact_check.md) — 원문에 있는가 · 그 편이 **버린 것**이 아닌가 · 쪼개져도 참인가. ep02s·ep11s 두 번 틀린 자리
- 🔴 [예고 구간 연출 = 카드 시각 안에서 **완결**](verdict_ep12s3.md#) — 금지는 「끝까지 자라는 것」이 아니라 「끝까지 못 자라는 것」. 상한 `TOTAL−2600`
- 🔴 [정적 구간 잣대는 **fps 불변이 아니다** — fps 30 값으로 반려 금지](verdict_ep12s_2.md) — 문턱 0.0008 이 FPS 미정규화. ep12s-2 0.2→3.4s · 발행된 ep11s 0.0→2.5s. 정본은 기본 fps 5
- [아웃트로 카드 vs 페이오프 = 4줄 산수](procedure_payoff_vs_outro_card.md) — ep08s-3 반려 지점. 여유의 손잡이는 `span` 하나뿐 — `lead` 를 고치라고 요구하지 말 것
