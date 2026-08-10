# Memory Index

## 검수 방법
- [기계가 못 보는 자리 다섯](feedback_review_blind_spots.md) — checks 키·좌우 가장자리·막대 순서·자막 복사 화면 + 🔴 **캔버스 글자 겹침**(게이트는 잘림만 본다 · 캔버스 kind 전부를 검수가 뜬다 · kind 당 3시점 · 세로 이동 띠는 바닥을 넘는다)
- [돌려막기는 이름이 아니라 '장치'로 대조한다](review_method_recycling_check.md) — 앞 회차 kind 주석이 이 회차를 지목하는지 반드시 확인
- [스틸 시각은 timed.json 합만으로 계산하면 틀린다](review_method_shot_timing.md) — 엔진이 샷마다 SHOT_TAIL 0.35초를 더한다
- [사실 대조는 숫자만이 아니다 — 화면 문자열 전부 원문 대조](review_method_fact_check_strings.md) — spec 안 label·line·expects·note 열거 후 grep. 🔴 **「없다」는 개수가 아니라 finditer 문맥으로 판정**(ep11s 오판) · 🔴 **값↔라벨 결합을 3번째 절차로 따로 본다**(ep13s-2 「최소 비용 8」)
- [가림은 스틸 한 장이 아니라 수식으로 잰다](review_method_occlusion_measure.md) — 둥근 캡 반경 + 불투명(α>0.7) 겹침만 세기. 🔴 **색 군집 세기로 「없다」를 증명하지 마라** — 가려지면 거짓 0(lf01 3차). 🔴 **작성팀 「여유 N px」는 늘 낙관적** — `lineWidth/2` 누락 + 원/상자 근사로 최악 자리 오지목. **다각형 최소거리 + 시간 결합**으로 다시 풀 것(ep14s-4)
- [2차 이후 "뭐가 바뀌었나"는 mtime 으로 가른다](review_method_what_changed.md) — git diff 는 답을 못 준다. 🔑 **check 출력 델타(산정 길이·정적·프레임)를 그 한 줄로 설명해 보는 3중 검산**
- [다음 편 예고는 정본 두 곳으로만 대조한다](review_method_next_episode_teaser.md) — 원문의 "다음 편에서는…"은 근거가 아니다. 되받기 vs 비트 중복은 네 기준으로 가른다
- [자기보고는 근거가 아니다 — 전부 다시 재라](review_method_self_report_is_not_evidence.md) — 2차 검수 여섯 편에서 「고쳤다」의 절반이 사실이 아니었다. git diff 로 먼저 보고, 숫자 주장은 재현 명령을 돌린다. 내 1차 처방이 달성 불가였던 경우(latch)도 있으니 같이 검산할 것
- [효과음은 그림 축·말 축을 따로 검산한다](review_method_sfx_timing.md) — cue 역산식 + TTS 선행 무음 360~540ms 라 "자막 시작 ≠ 말 시작". check.mjs 에 효과음 항목 없음
- 🔴 [효과음 겹침 = synth 파형 상관 + 통제군](review_method_sfx_waveform_correlation.md) — 절대 표본 정렬(리샘플 금지) · **tick·drop·thud·riser·pad 는 seed 를 안 읽는다** · latch 바닥 0.74~0.83(**승인 253쌍 분포 중앙 0.809 를 같이 내라**) · **길이 다른 kind 간 r≈0.3 은 패딩 인공물** · 진짜 피크는 `peakCeil:99`
- [동색 가림 · clip 이음매 글자 복제](review_method_same_color_and_clip_seam.md) — 빗금 위 같은 색 라벨과 clip+translate 이음매는 혼합 픽셀이 0개라 게이트가 구조적으로 못 본다. 확대 스틸 + 테두리 한 행 샘플
- 🔴 [롱폼(wide) 검수 절차](review_method_wide_longform.md) — 정본은 롱폼_대본_문법.md · en 0줄은 정상(비목표) · 클립은 카탈로그 detail 이 아니라 스틸로 판정 · 게임 HUD 겹침은 render.mjs --still 로만 보인다
- 🔴 [사라지는 라벨의 알파도 cue 산수로 재라](review_method_vanishing_label_alpha.md) — `1 - ease(…)` 요소 × 샷 진입 페이드(ENTER 0.26 · 0.12→1.0) × `k = lead/W` 시작값. ep08s-3 의 거울상(ep14s-2 반려) · 말 축(TTS 선행 무음 0.31~0.48)이 가장 센 근거
- 🔴 [check 전에 build/timed.json 샷 수를 대조하라](review_method_stale_timed_json.md) — 옛 더미 타임라인이 길이·말속도·자막2초 게이트를 대신 통과시킨다(lf01: 34샷을 「10.6초」로). 꼬리 ÷0.35 = 샷 수 · **양성 확인 = tts 재실행의 「새로 만듦 0 · 재사용 N」**

## 회차 기록
- [회차별 판정과 반려 횟수](project_review_log.md) — "반려 3회면 사람에게" 규칙을 세기 위한 표. ep00s·ep01s 각 1차 반려(2026-07-29), ep02s 반려 2회 후 3차 통과, 재작업 트랙은 1차 반려 → 2차 통과(2026-07-30). 🔴 **사건 단위 재분할 여섯 편(2026-08-04)** — 2차까지 돌려 PR #10 으로 main 머지. **롱폼 lf01·lf02 둘 다 반려 2회 후 3차 통과**. 🔴 **ep13s 트리오(2026-08-10)** — 13-2 **1차 반려 → 2차 통과**(「최소 비용 8」 값↔라벨 결합 오류를 슬롯 쪼개기 + 폭 30 으로 해소) · **13-3 1차 통과**(4막 피날레 · 페이오프를 마지막 샷 **밖**에 둬서 아웃트로 카드를 구조로 피한 첫 사례 · `since()` 기반 delayMs 는 실측 후 오차 1.5ms). 🔴 **ep14s(4편 분할)** — **14-2 1차 반려 → 2차 통과**(사라지는 카드 글자 알파 0.145→1.000 · **내 처방이 불완전**해 상자까지 함께 밀어야 했다 · latch 0.794 는 정상 범위) · **14-1 1차 통과 + 정정 6건** · **14-3 1차 통과 + 정정 2건**(어려운 낱말 **0개** — 기획이 사건을 걸러서 · 「원문 어절 그대로」 주장은 `count()` 로 깨진다 · 낡은 `notes` 3회 연속) · 🔴 **14-4 1차 반려 2건 → 2차 통과**(①화덕이 「마을 경계」 밖 — 원인은 좌표가 아니라 「집 최대거리」와 「경계」를 한 상수로 쓴 것 ②`--fps 30` 정적 5.2→**1.1초**. **여섯 지적 전부 자막 0자로 닫힘**). 파일 끝 절에 트랙 전체 요약
- [ep01s 그림 재제작 검수](project_ep01s_rework_review.md) — 1차 반려(spoke ↔ ep00s channels 장치 중복) → pins 교체로 2차 통과
- [가장자리 잘림은 재서 가른다](review_method_edge_clip_diagnosis.md) — measureText 산술 확정 + 접촉 y좌표로 범인 특정 + checks.edge 는 글자 잘림에 절대 안 쓴다
- 🔴 [정적은 `--fps 30`(= 렌더 눈금) + 승인 회차 통제군으로 잰다](review_method_static_motion_floor.md) — 기본 5fps 는 느린 요소를 통과시킨다(ep14s-4: 0.6s → 3.2/5.2s, 통제군 1.1~1.7s). 판정식 복제로 m 직접 측정 · 0.1% 미만 요소는 아무리 빨라도 정지
- 🔴 [길이 정본 = 38초 fail · **권장 33~37**(2026-08-10 개정 · 옛 30~35 는 아래쪽이 도달 불가였다)](feedback_no_length_rejection.md) — 옛 28초·50초 눈금 전부 폐기. 본문 18~22 · 예고 5.0↓ · 인트로 5.5↓. **상한은 목적이 아니라 한계** — 길이 맞추려 인트로·훅·예고·소개를 깎으면 반려. 🔴 **본문이 하한 18.0 에 붙은 회차는 자막 다듬기 권고 전에 여유부터 재라**. 「산정 길이(참고)」 줄로 판정 금지 · 합성 timed.json 금지
