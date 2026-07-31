# Memory Index

## 검수 방법
- [기계가 못 보는 자리 넷](feedback_review_blind_spots.md) — checks 키 유효성·좌우 가장자리·막대 등장 순서·자막 복사 화면. check.mjs 통과는 통과의 근거가 아니다
- [돌려막기는 이름이 아니라 '장치'로 대조한다](review_method_recycling_check.md) — 앞 회차 kind 주석이 이 회차를 지목하는지 반드시 확인
- [스틸 시각은 timed.json 합만으로 계산하면 틀린다](review_method_shot_timing.md) — 엔진이 샷마다 SHOT_TAIL 0.35초를 더한다
- [사실 대조는 숫자만이 아니다 — 화면 문자열 전부 원문 대조](review_method_fact_check_strings.md) — spec 안 label·line·expects·note 열거 후 grep. ep02s 1차 반려의 사실관계 위반이 계기
- [가림은 스틸 한 장이 아니라 수식으로 잰다](review_method_occlusion_measure.md) — 둥근 캡 반경 포함 + 불투명(α>0.7) 겹침만 세기. 반투명 꼬리 위의 주인공은 오히려 잘 보인다
- [2차 이후 "뭐가 바뀌었나"는 mtime 으로 가른다](review_method_what_changed.md) — 회차가 통째로 미커밋이라 git diff 는 답을 못 준다
- [다음 편 예고는 정본 두 곳으로만 대조한다](review_method_next_episode_teaser.md) — 원문의 "다음 편에서는…"은 근거가 아니다. 되받기 vs 비트 중복은 네 기준으로 가른다
- [효과음은 그림 축·말 축을 따로 검산한다](review_method_sfx_timing.md) — cue 역산식 + TTS 선행 무음 360~540ms 라 "자막 시작 ≠ 말 시작". check.mjs 에 효과음 항목 없음

## 회차 기록
- [회차별 판정과 반려 횟수](project_review_log.md) — "반려 3회면 사람에게" 규칙을 세기 위한 표. ep00s·ep01s 각 1차 반려(2026-07-29), ep02s 반려 2회 후 3차 통과, 재작업 트랙은 1차 반려 → 2차 통과(2026-07-30)
- [ep01s 그림 재제작 검수](project_ep01s_rework_review.md) — 1차 반려(spoke ↔ ep00s channels 장치 중복) → pins 교체로 2차 통과
- [가장자리 잘림은 재서 가른다](review_method_edge_clip_diagnosis.md) — measureText 산술 확정 + 접촉 y좌표로 범인 특정 + checks.edge 는 글자 잘림에 절대 안 쓴다
- [정적 구간 경고는 "작아서"일 수 있다](review_method_static_motion_floor.md) — 판정식 복제로 프레임간 m 을 직접 재라. 0.1% 미만 요소는 아무리 빨라도 정지로 잡힌다
- [길이는 절대 반려 사유가 아니다](feedback_no_length_rejection.md) — ep02s 1차가 리포에 없는 "60초 상한"으로 36초를 잘라냈다. 근거는 리포 문서에서만
