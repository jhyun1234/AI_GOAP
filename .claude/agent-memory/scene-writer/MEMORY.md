# Memory Index

## 프로젝트
- [회차 사이 연결 장치 · 엔딩 비트 소유권](project_series_handoffs.md) — "지워집니다" 예고는 ep01s 것, ByReason 로그는 ep00s/ep01s 가 강조 칸을 나눠 씀, Claude 마크는 ep01s S8
- [효과음 delayMs 는 draw 를 풀어서 낸다](project_sfx_timing.md) — 사건이 cue 가 아니라 t 로 도는 kind 가 있다(tripwire). 가지가 갈리는 지점을 찾고, 램프만 하는 샷은 조용히 둔다
- [25초 편 예산 산수](project_video_25s_budget.md) — 🔴 산정치가 실측보다 3.4초 짧다(상한의 14%) → 산정 22초를 겨냥할 것. 음성 예고를 달면 창이 0.5초라 outro.next 가 사실상 강제
- [분할 회차 규약 — id·outro.source·형제 라벨·길이](project_split_episode_conventions.md) — 형제 label 은 그 편 youtube.title 에서 꼬리만 뗀 것. 여섯 편이 서로를 부르는 이름 18개 중 하나도 안 맞았고 둘은 사실관계까지 어긋났다. notes.길이 는 check.mjs 출력을 인용
- [latch 는 dur 로 안 갈린다 — seed 를 줘라](project_sfx_latch_seed.md) — 몸통이 340/680Hz 고정. seed 를 안 준 세 회차가 상관 0.99 로 같은 소리였다. 0.68~0.79 가 하한이니 거기서 멈춰라. 전수는 인용 시점에 다시 세라
- [표를 제거한 브리프는 오염이 아니라 손실로 실패한다](project_table_stripped_brief.md) — 빈 줄 앞뒤를 읽어 뭐가 사라졌는지 추정. 부분만 남으면 남은 것만 그리고, 전무하면 통째로 버리고 excluded 에 사유

## 피드백
- [승인된 대본은 얼려 둔다 — 사유가 무효가 아닌 한](feedback_approved_script_is_frozen.md) — 그림 반려면 자막은 안 건드린다. 단 반려 사유가 무효면(ep02s "60초 상한") 되살리는 게 일이다. `excluded` 는 늘 다시 본다
- [기본 도형을 먼저 정한다](feedback_base_shape_first.md) — 샷별로 그림을 고르면 남의 회차 어휘가 섞인다. 축 한 문장 → 열 개 파생. 회차 안 되받기는 뒤집을 때만 허용
- [화면 문자열은 원문 grep 뒤에만 통과](feedback_string_fact_check.md) — 숫자 대조와 문자열 대조는 별개 절차. 배열을 통째로 그리는 kind(options.forEach 등)는 원소 전부가 원문 근거를 가져야 한다. 없으면 익명 자리(···)
- [훅은 캔버스에 안 그린다 — 아웃트로 카드가 덮는다](feedback_hook_card_not_canvas.md) — spec.hookCue 폐기. 훅으로 정적을 막던 설계가 통째로 무효(안 보이는 그림은 정적도 못 막는다). 블록만 지우고 fade·주석·reads 를 남기면 그 자리가 더 나빠진다
- [정적 구간은 속도가 아니라 면적이다](feedback_static_segment_is_area.md) — 이동 거리를 늘려도 `m` 은 이미 포화. 선 위 같은 색 점은 거의 안 먹힘(선보다 굵게 깔 것). duration 늘어난 기존 샷도 점검 대상
- [캔버스 글자는 폭을 재서 안쪽에 가둔다](feedback_canvas_edge_text.md) — 폭 제한은 박스가 아니라 글자에. 문자열을 잘라 도망가지 않는다. 한글에 mono 금지, 퇴장은 가장자리 전에 소멸
