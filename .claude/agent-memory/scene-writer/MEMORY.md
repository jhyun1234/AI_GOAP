# Memory Index

## 프로젝트
- [회차 사이 연결 장치 · 엔딩 비트 소유권](project_series_handoffs.md) — "지워집니다" 예고는 ep01s 것, ByReason 로그는 ep00s/ep01s 가 강조 칸을 나눠 씀, Claude 마크는 ep01s S8
- [효과음 delayMs 는 draw 를 풀어서 낸다 · gain 상한은 kind+옵션이 정한다](project_sfx_timing.md) — 실측 나오면 재계산 필수(같은 회차 안에서도 오차 3배). 🔴 **돌려막기는 형제 편의 kind 「쌍」으로도 난다**
- [25초대 편 예산 산수](project_video_25s_budget.md) — 🔴 **총 = 자막합 + 0.35×샷수** · 상한 **28초** · 예고 3.0초 · 초안 자수는 **구성이 같은 직전 편 실측**에서(비 모형보다 정확) · 뺄 순서 pauseAfter → 조이기
- [제목의 문장 모양은 반복 금지 — 숫자 훅은 반복 가능](project_title_shape_reuse.md) — 「N명이 똑같이 움직였다」를 두 번 썼다. 갈라낼 재료는 ①방아쇠 ②잃은 것. 수치가 없으면 warn 을 받고, **이미 두 번 쓴 수치는 화면 라벨로 강등**한다
- [분할 회차 규약 — id·outro.source·형제 라벨·blurb·앞 편 약속](project_split_episode_conventions.md) — 형제 label 은 그 편 youtube.title 에서 꼬리만 뗀 것. 🔴 **`outro.siblings` 와 한국어 blurb 형제 줄은 둘 다 필요**(카드가 면제 아님 · ep12s-1 반려). 앞 편 `outro.next` 인계는 게이트가 못 본다
- [latch 는 dur 로 안 갈린다 — seed 를 줘라](project_sfx_latch_seed.md) — 몸통이 340/680Hz 고정. seed 를 안 준 세 회차가 상관 0.99 로 같은 소리였다. 0.68~0.79 가 하한이니 거기서 멈춰라. 전수는 인용 시점에 다시 세라
- [표를 제거한 브리프는 오염이 아니라 손실로 실패한다](project_table_stripped_brief.md) — 빈 줄 앞뒤를 읽어 뭐가 사라졌는지 추정. 부분만 남으면 남은 것만 그리고, 전무하면 통째로 버리고 excluded 에 사유

## 피드백
- [승인된 대본은 얼려 둔다 — 사유가 무효가 아닌 한](feedback_approved_script_is_frozen.md) — 그림 반려면 자막은 안 건드린다. 단 반려 사유가 무효면(ep02s "60초 상한") 되살리는 게 일이다. `excluded` 는 늘 다시 본다
- [기본 도형을 먼저 정한다](feedback_base_shape_first.md) — 샷별로 그림을 고르면 남의 회차 어휘가 섞인다. 축 한 문장 → 열 개 파생. 회차 안 되받기는 뒤집을 때만 허용
- [화면 문자열은 원문 grep 뒤에만 통과 — 축이 셋이다](feedback_string_fact_check.md) — 숫자 · 문자열 · 🔴 **분류**(그 낱말이 그 라벨에 속한다는 근거는 원문이 아니라 리포 에셋/ADR 에 있다). 근거 없으면 행을 통째로 가린다
- [훅은 캔버스에 안 그린다 — 아웃트로 카드가 덮는다](feedback_hook_card_not_canvas.md) — spec.hookCue 폐기. 훅으로 정적을 막던 설계가 통째로 무효(안 보이는 그림은 정적도 못 막는다). 블록만 지우고 fade·주석·reads 를 남기면 그 자리가 더 나빠진다
- [정적 구간은 면적 · 겹침은 범위 상수](feedback_static_segment_is_area.md) — 이동 거리를 늘려도 `m` 은 포화. 반대로 움직이는 것끼리의 겹침은 좌표 대조가 못 잡으니 범위를 안 겹치게 묶는다(둘이 상충하니 함께 본다)
- [영어 자막은 앞줄과 붙여 읽는다](feedback_en_subtitle_referent.md) — `That spread` 가 앞줄이 명명한 것을 가리켜 뜻이 뒤집혔다. 회상 관형형(~던)은 영어로 자동으로 안 넘어간다. 긴 blurb 는 맞히고 두 줄 자막에서 틀린다
- [흰색과 강조색을 같은 픽셀에 겹치지 마라 — 양방향 다 뚫린다](feedback_translucent_accent_zorder.md) — 게이트는 혼합을 hue 150~152 로 읽어 **어느 순서든** 통과시킨다. 문턱으로 색 바꾸기도 같은 자리에서 잡힌다
- [캔버스 글자는 폭을 재서 안쪽에 가둔다](feedback_canvas_edge_text.md) — 폭 제한은 박스가 아니라 글자에. 아래 가장자리는 **한 프레임만 스쳐도 red**이고 보고값은 거리가 아니라 **픽셀 개수**(÷dpr = 범인의 CSS 폭). 🔴 **움직이는 요소는 「가장 밖으로 나가는 순간」으로 여백을 잰다**
