# Memory Index

## 판정 원칙
- [반려 근거의 등급](feedback_verdict_evidence_grades.md) — 미검증 의견·폐기된 옛 규격으로는 반려하지 않는다. 승인은 조건 넷으로만 가른다
- [사실 대조의 함정 2종](project_factcheck_gotchas.md) — 인용문 grep 0 은 태그 분절일 수 있다 · 「안 그렸다」는 값은 사유 문장 말고 렌더 경로로 판정
- [게이트의 사각지대](project_gate_metric_gaps.md) — 「총 길이」 꼬리 누락은 해결됨. 카드에 먹힌 그림·이른 효과음·거짓 `reads`·구도 중복은 여전히 못 본다
- [직접 검산하는 두 공식](project_timing_formulas.md) — 마지막 샷 여유 `0.92×L6 − 2.25`(L5 소거) · `sfx.delayMs` 는 실측 `timed.json` 으로 다시 푼다
- [z 겹침과 위상 상수](project_zorder_and_phase.md) — 겹침은 최종 자세 말고 **자세가 변하는 전 구간 × 모든 획**으로 잰다 · `PHASE` 는 그 샷 `rel.dur` 에 묶인다

## 절차
- [예고 사실 대조 5단계](procedure_teaser_fact_check.md) — 게이트가 길이만 보는 자리 · order 를 기억 말고 파일로 · 수치는 **다음 편의 글**에서 · 바꿨으면 다음 편 첫 줄도 본다

## 규격
- [4단 고정 구조 (ADR-V25-8~11)](project_four_act_structure.md) — 콜드 오픈은 「훅의 수수께끼」로 대체 · 카드 = TOTAL−**3000** · 「인트로를 깎으면 반려」의 예외 범위 둘

## 회차
- [ep10s 3편 — 10-2 만 4단으로 재제작·재승인](project_ep10s_split.md) — 형제 셋의 구조가 갈렸다(사람 결정) · 재제작 회차는 `build/` 타임스탬프를 봐라
- [ep08s 3편 분할 — 종료](project_ep08s_split.md) — 8-1·8-2·8-3 전부 APPROVED. 원문 수치 전수·형제 누출 없음 확인 · 남은 정정 목록
- [ep12s 3편 — 전부 4단 APPROVED](project_ep12s_split.md) — 🔴 이월: **ep13s 분할되면 12-3 예고를 다시 본다** · 원문이 안 센 수는 화면도 안 센다
- [ep09s 2편 분할 — 9-1·9-2 APPROVED](project_ep09s_split.md) — 재검산 일치. 🔴 남은 정정: 9-1 자기검산 3문장 · 9-2 는 z 겹침/`delayMs` 거짓 증명 2건. 둘은 같이 확정
