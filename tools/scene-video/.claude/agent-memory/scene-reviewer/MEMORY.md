# Memory Index

## 검수 방법
- [cue/sfx 물림 실측 계산법](feedback_cue_timing_audit.md) — rel dur = wav dur + pauseAfter(양쪽이 반복해 틀림), ease 역함수는 이분탐색(0.9→0.8065, 0.729 는 틀림)
- [sfx 피크 천장은 인자마다 다르다](feedback_sfx_gain_ceiling.md) — "kind 별 상한" 표를 믿지 말고 synth() 를 실제 인자로 호출해 경고 확인
- [스틸 뽑아 눈으로 보기](feedback_frame_capture.md) — check.mjs 가 못 보는 도형 겹침·라벨 가림·자막 옮겨적기용 openEngine/shot 재사용법
- [마지막 샷 vs 아웃트로 카드](feedback_last_shot_outro_collision.md) — 카드는 TOTAL−2600 부터 .vis 를 덮는다. 마지막 자막 3초 미만이면 페이오프가 통째로 안 보인다
- [t 로 도는 값 · 도형 겹침은 스윕해서 확인](feedback_t_driven_value_sweep.md) — 문턱 통과 프레임 0 / 넘어도 발화 구간 밖 / 떠다니는 도형 포갬 — 셋 다 스틸로 안 잡힌다

## 반복되는 것
- [작성팀 결함 5종](project_recurring_defects.md) — 자막 옮겨적기 · notes 자기보고 오류 · sfx 답습 · 형제 편 대조 누락 · **원문이 안 붙인 분류를 화면이 주장(게임 에셋으로 대조)**
