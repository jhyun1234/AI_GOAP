# Memory Index

## 프로젝트
- [회차 사이 연결 장치 · 엔딩 비트 소유권](project_series_handoffs.md) — "지워집니다" 예고는 ep01s 것, ByReason 로그는 ep00s/ep01s 가 강조 칸을 나눠 씀, Claude 마크는 ep01s S8
- [효과음 delayMs 는 draw 를 풀어서 낸다](project_sfx_timing.md) — 사건이 cue 가 아니라 t 로 도는 kind 가 있다(tripwire). 가지가 갈리는 지점을 찾고, 램프만 하는 샷은 조용히 둔다

## 피드백
- [승인된 대본은 얼려 둔다 — 사유가 무효가 아닌 한](feedback_approved_script_is_frozen.md) — 그림 반려면 자막은 안 건드린다. 단 반려 사유가 무효면(ep02s "60초 상한") 되살리는 게 일이다. `excluded` 는 늘 다시 본다
- [기본 도형을 먼저 정한다](feedback_base_shape_first.md) — 샷별로 그림을 고르면 남의 회차 어휘가 섞인다. 축 한 문장 → 열 개 파생. 회차 안 되받기는 뒤집을 때만 허용
- [화면 문자열은 원문 grep 뒤에만 통과](feedback_string_fact_check.md) — 숫자 대조와 문자열 대조는 별개 절차. 배열을 통째로 그리는 kind(options.forEach 등)는 원소 전부가 원문 근거를 가져야 한다. 없으면 익명 자리(···)
- [정적 구간은 속도가 아니라 면적이다](feedback_static_segment_is_area.md) — 이동 거리를 늘려도 `m` 은 이미 포화. 선 위 같은 색 점은 거의 안 먹힘(선보다 굵게 깔 것). duration 늘어난 기존 샷도 점검 대상
- [캔버스 글자는 폭을 재서 안쪽에 가둔다](feedback_canvas_edge_text.md) — 폭 제한은 박스가 아니라 글자에. 문자열을 잘라 도망가지 않는다. 한글에 mono 금지, 퇴장은 가장자리 전에 소멸
