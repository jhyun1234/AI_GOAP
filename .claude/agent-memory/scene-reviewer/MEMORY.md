# Memory Index

## 검수 방법
- [기계가 못 보는 자리 넷](feedback_review_blind_spots.md) — checks 키 유효성·좌우 가장자리·막대 등장 순서·자막 복사 화면. check.mjs 통과는 통과의 근거가 아니다
- [돌려막기는 이름이 아니라 '장치'로 대조한다](review_method_recycling_check.md) — 앞 회차 kind 주석이 이 회차를 지목하는지 반드시 확인
- [스틸 시각은 timed.json 합만으로 계산하면 틀린다](review_method_shot_timing.md) — 엔진이 샷마다 SHOT_TAIL 0.35초를 더한다
- [사실 대조는 숫자만이 아니다 — 화면 문자열 전부 원문 대조](review_method_fact_check_strings.md) — spec 안 label·line·expects·note 열거 후 grep. 🔴 **「없다」는 개수가 아니라 finditer 문맥으로 판정**(ep11s 오판) · 태그 제거본과 원본 둘 다 · 어간으로 훑기
- [가림은 스틸 한 장이 아니라 수식으로 잰다](review_method_occlusion_measure.md) — 둥근 캡 반경 포함 + 불투명(α>0.7) 겹침만 세기. 반투명 꼬리 위의 주인공은 오히려 잘 보인다
- [2차 이후 "뭐가 바뀌었나"는 mtime 으로 가른다](review_method_what_changed.md) — 회차가 통째로 미커밋이라 git diff 는 답을 못 준다
- [다음 편 예고는 정본 두 곳으로만 대조한다](review_method_next_episode_teaser.md) — 원문의 "다음 편에서는…"은 근거가 아니다. 되받기 vs 비트 중복은 네 기준으로 가른다
- [자기보고는 근거가 아니다 — 전부 다시 재라](review_method_self_report_is_not_evidence.md) — 2차 검수 여섯 편에서 「고쳤다」의 절반이 사실이 아니었다. git diff 로 먼저 보고, 숫자 주장은 재현 명령을 돌린다. 내 1차 처방이 달성 불가였던 경우(latch)도 있으니 같이 검산할 것
- [효과음은 그림 축·말 축을 따로 검산한다](review_method_sfx_timing.md) — cue 역산식 + TTS 선행 무음 360~540ms 라 "자막 시작 ≠ 말 시작". check.mjs 에 효과음 항목 없음
- [동색 가림 · clip 이음매 글자 복제](review_method_same_color_and_clip_seam.md) — 빗금 위 같은 색 라벨과 clip+translate 이음매는 혼합 픽셀이 0개라 게이트가 구조적으로 못 본다. 확대 스틸 + 테두리 한 행 샘플

## 회차 기록
- [회차별 판정과 반려 횟수](project_review_log.md) — "반려 3회면 사람에게" 규칙을 세기 위한 표. ep00s·ep01s 각 1차 반려(2026-07-29), ep02s 반려 2회 후 3차 통과, 재작업 트랙은 1차 반려 → 2차 통과(2026-07-30). 🔴 **사건 단위 재분할 여섯 편(2026-08-04)** — 2차까지 돌려 PR #10 으로 main 머지. 파일 끝 절에 트랙 전체 요약
- [ep01s 그림 재제작 검수](project_ep01s_rework_review.md) — 1차 반려(spoke ↔ ep00s channels 장치 중복) → pins 교체로 2차 통과
- [가장자리 잘림은 재서 가른다](review_method_edge_clip_diagnosis.md) — measureText 산술 확정 + 접촉 y좌표로 범인 특정 + checks.edge 는 글자 잘림에 절대 안 쓴다
- [정적 구간 경고는 "작아서"일 수 있다](review_method_static_motion_floor.md) — 판정식 복제로 프레임간 m 을 직접 재라. 0.1% 미만 요소는 아무리 빨라도 정지로 잡힌다
- 🔴 [길이 정본 = 상한 28초 · 권장 22.6~28 (2026-08-07 사용자 판정)](feedback_no_length_rejection.md) — 옛 "상한 없음"도 "50초/30~45"도 폐기. 본문 20초↑·예고 3.0초↓. **상한은 목적이 아니라 한계** — 길이 맞추려 첫 3초나 예고를 깎으면 반려. 「산정 길이(참고)」 줄로 판정 금지 · 합성 timed.json 금지
