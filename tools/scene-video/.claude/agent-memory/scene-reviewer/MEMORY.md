# Memory Index

## 검수 방법
- [cue/sfx 물림 실측 계산법](feedback_cue_timing_audit.md) — rel dur 은 pauseAfter 포함, ease 역함수는 이분탐색(0.9→0.8065, 작성팀이 쓰는 0.729 는 틀림)
- [sfx 피크 천장은 인자마다 다르다](feedback_sfx_gain_ceiling.md) — "kind 별 상한" 표를 믿지 말고 synth() 를 실제 인자로 호출해 경고 확인
- [스틸 뽑아 눈으로 보기](feedback_frame_capture.md) — check.mjs 가 못 보는 도형 겹침·라벨 가림·자막 옮겨적기용 openEngine/shot 재사용법

## 반복되는 것
- [작성팀 결함 3종](project_recurring_defects.md) — 화면이 자막 옮겨적기 · notes 자기보고 오류 · 앞 회차 sfx 파라미터 답습
