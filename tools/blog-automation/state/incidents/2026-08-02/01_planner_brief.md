skip: false
verify_at: c1d87e2

deep_dive_posts:
  - tag: "#trait-vector (M12 성격 축 — 성향 벡터)"
    session_refs:
      - devlog/sessions/2026-07-26.md#[02:00] spec(m12) 성격 축 — 성향(Trait) 벡터 4작용형식 실행명세서 (브레인스토밍 배경 포함)
      - devlog/sessions/2026-07-26.md#[02:19] feat(trait) M12-A 성향 스키마 + 유도기
      - devlog/sessions/2026-07-26.md#(+02:31) feat(trait) M12-B 성향 1우선순위 — goal 30개 중 24개에 기질 가중치 (S1)
      - devlog/sessions/2026-07-26.md#(+02:41) fix(threat) 굶주린 주민이 늑대 앞에 굳어 서서 물리던 결함 (ADR-M12-8)
      - devlog/sessions/2026-07-26.md#(+02:51) docs(adr) ADR-M12-4 전면 개정 — "굶주림 앞에 성격 없음" 3조항
      - devlog/sessions/2026-07-26.md#[03:41] docs(adr) ADR 전수 감사 재조치 — 번호 단일화 + 낡음 탐지 게이트
      - devlog/sessions/2026-07-26.md#(+03:50) feat(trait) M12-C 성향 2비용 — 노동 4계열 배율을 벡터에서 유도
      - devlog/sessions/2026-07-26.md#(+03:59) feat(trait) M12-D 성향 3문턱 — 거부·감지반경·선불을 소비처 민감도로 환산
      - devlog/sessions/2026-07-26.md#(+04:05) feat(trait) M12-E 성향 4대상 — 택지 거리를 벡터에서 유도
      - devlog/sessions/2026-07-26.md#(+04:14) refactor(trait) M12-F 6성격 벡터 이식 — "중간 리뷰 1", 여기서부터 실제로 행동이 갈린다
      - devlog/sessions/2026-07-26.md#[04:44] fix(cook) 겨울 조리·비축 goal의 탐색 폭발
      - devlog/sessions/2026-07-26.md#[12:35] fix(job) 서비스 직업이 외딴 곳에 살아 부탁이 영구 단절되던 결함
      - devlog/sessions/2026-07-26.md#(+12:55) feat(profiler) M12-J 성격별 행동 계측 — "떠돌이 목수가 나오는 경우를 찾기가 힘들어" 사용자 재관측이 명세 순서(G→H→I→J)를 바꿈
      - devlog/sessions/2026-07-26.md#[13:48] fix(ownership) 부탁 수행 중 지은 모닥불이 주인을 잃어 무한 건축되던 결함
      - devlog/sessions/2026-07-26.md#[14:09] fix(profiler) 사망 관측 불가 + 자존 축 미노출
      - devlog/sessions/2026-07-26.md#(+14:11) docs(devlog) 2026-07-26 세션 로그 — 세션 총괄 요약 (굵은 줄기 5가지, "블로그 파이프라인 1차 소재"라고 스스로 명시)
      - devlog/sessions/2026-07-26.md#[14:39] feat(goap) M12-G 집 동기 — 성향 문턱 + MyWasStarved 경험 우회
      - devlog/sessions/2026-07-26.md#(+15:30) feat(goap) M12-H 성향 → 직업 배정 편향 + 목수 최소 보장
      - devlog/sessions/2026-07-26.md#[15:51] feat(scene) 관측 표본 확대 — 주민 8명 + 성격 씬 지정 필드
      - devlog/sessions/2026-07-26.md#(+16:19) docs(devlog) M12 종료 — S4 15/15 분화, 회상 테스트 실패, M13 = 표현층
    commit_refs:
      - 2e2ce34: spec(m12) 성격 축 — 성향(Trait) 벡터 4작용형식 실행명세서
      - d54b000: feat(trait) M12-A 성향 스키마 + 유도기 — 4작용형식의 공통 토대
      - c8a023c: feat(trait) M12-B 성향 1우선순위 — goal 30개 중 24개에 기질 가중치 (S1)
      - 976fae8: fix(threat) 굶주린 주민이 늑대 앞에 굳어 서서 물리던 결함 (ADR-M12-8)
      - 4e3090a: docs(adr) ADR-M12-4 전면 개정 — 굶주림 앞에 성격 없음을 3조항으로
      - 2183311: docs(adr) ADR 전수 감사 재조치 — 번호 단일화 + 자격조건화 + 낡음 탐지 게이트
      - 14ba4b8: feat(trait) M12-C 성향 2비용 — 노동 4계열 배율을 벡터에서 유도 (TraitRules 에셋 신설)
      - 1312806: feat(trait) M12-D 성향 3문턱 — 거부·감지반경·선불을 소비처 민감도로 환산
      - 4b83d78: feat(trait) M12-E 성향 4대상 — 택지 거리를 벡터에서 유도
      - 48f8a95: refactor(trait) M12-F 6성격 벡터 이식 — 중간 리뷰 1, 여기서부터 행동이 갈린다
      - 3129670: fix(cook) 겨울 조리·비축 goal의 탐색 폭발
      - c4d1566: fix(job) 서비스 직업이 외딴 곳에 살아 부탁이 영구 단절되던 결함
      - fbe6dc1: feat(profiler) M12-J 성격별 행동 계측
      - 2c940b1: fix(ownership) 부탁 수행 중 지은 모닥불이 주인을 잃어 무한 건축되던 결함
      - 789e577: fix(profiler) 사망 관측 불가(생존 0/0) + 자존 축 미노출
      - 30ea98c: feat(goap) M12-G 집 동기 — 성향 문턱 + MyWasStarved 경험 우회
      - 80cfe22: feat(goap) M12-H 성향 → 직업 배정 편향 + 목수 최소 보장
      - eed392b: feat(scene) 관측 표본 확대 — 주민 8명 + 성격 씬 지정 필드
      - c1d87e2: docs(devlog) M12 종료 — S4 15/15 분화, 회상 테스트 실패, M13 = 표현층
    checklist_hits:
      - "서브시스템이 데모 가능한 상태에 도달했다 — BehaviorProfiler 로그로 성격 6종 15쌍 전부 다른 상위3 goal 구성 확인(S4 15/15)"
      - "까다로운 문제를 해결했고 과정이 설명할 가치가 있다 — 성격×goal O(180칸) 수작업 배율표를 성향 벡터 O(성격+goal)로 재설계, 4작용형식(우선순위·비용·문턱·대상) 법칙화"
      - "트레이드오프가 있는 설계 결정을 내렸다 — ADR-M12-1~8 다수(특히 ADR-M12-4 '굶주림 앞에 성격 없음' 3조항, ADR-M12-2 'goal 발동에는 불개입'), 舊 12필드를 삭제 대신 중립값으로 병존시킨 마이그레이션 안전장치"
      - "눈에 보이는 마일스톤을 찍었다 — 종료 선언 커밋(c1d87e2)으로 명세 성공기준 8개 중 핵심 S4 달성, 그러나 '회상 테스트'는 실패해 다음 밀스톤(M13 표현층) 착수로 이어지는 자연스러운 반전 서사"
    narrative_angle: >
      "성격 6종이 15쌍 전부 다르게 행동하도록 벡터 하나로 재설계했는데, 정작 사람이 보기엔
      '게으름뱅이 둘 다 그냥 죽었어, 별 이야기가 없었다'였다" — 시스템은 완성됐지만 화면에
      드러나지 않아 다음 밀스톤(표현층)으로 이어지는, AI와 함께 설계 원칙(4작용형식)을 세우고
      스트레스 테스트한 뒤 실제 관측에서 뒤통수를 맞은 하루의 기록.
    seo_layer: C
    seo_keywords: ["Claude Code 게임 개발", "AI 페어 프로그래밍 후기"]
    internal_link_hint: "2026-07-17 발행 'M4 주민의 성격(아키타입 재편입)' — https://gamedevclaude.blogspot.com/2026/07/unity-goap_0950458123.html (같은 성격/성향 시스템 토픽 클러스터, M4가 최초 도입한 PersonalitySO를 M12가 확장하는 후속편 관계)"

weekly_summary: null

memory_refs: []

missing_from_sessions: []

excluded_notes: >
  ① M12-I(절망 시 구걸 행동)·M12-K(신규 성격 6종)는 M12 자체 내에서 "표현층(M13) 이후로"
  명시 이월됐다(c1d87e2 커밋 본문) — 미구현 상태이므로 이번 소재에서 다루지 않음, 억지로
  "예정" 서술 금지.
  ② c1d87e2 이후의 M13 관련 후속 커밋들("M13 목적 재정의", "INDEX 활성 트랙 갱신" 등,
  16:25~16:35)은 verify_at(c1d87e2) 이후 시점이라 커밋 범위 밖 — 다음 회차(M13) 소재이므로
  이번 브리프에 포함하지 않음. M12의 "회상 테스트 실패" 진단 자체(S4는 성공, 서사 전달은 실패)는
  c1d87e2 안에 있으므로 이번 글의 자연스러운 마무리로 포함 가능하나, M13이 무엇을 할지의
  구체 계획까지는 다루지 않는다(다음 회차 소재 선점 방지).
  ③ M13(사건과 흔적)·scene-video 트랙은 사용자 지시대로 이번 회차에서 다루지 않음.
  ④ 상업적으로 민감한 수치나 개인 식별 정보 없음.

quotable_strings_verified_at_c1d87e2:
  - "[Profiler] Day {day} — 성격별 행동 프로파일" (Assets/Scripts/M0/BehaviorProfiler.cs:141, sb.Append 헤더 포맷 문자열)
  - "{성격명}: 생존 {N}/{M} · 집 {W} · 노동 {P}% · 공용 {C}% · 거부 {R} · 상위goal [{목록}]" (Assets/Scripts/M0/BehaviorProfiler.cs:150-152, 성격별 1줄 로그 포맷)
  - "→ S4 분화: 상위3 구성이 다른 성격 쌍 {K}개 (성격 {N}종 중)" (Assets/Scripts/M0/BehaviorProfiler.cs:154-155)
  - 순둥이 DisplayName "순둥이" (Assets/M0Config/Personalities/Personality_Docile.asset:15), MoodLines 예: "다들 힘내세요!" / "오늘도 평화롭네요" (Personality_Docile.asset:24-)
  - 게으름뱅이 DisplayName "게으름뱅이" (Assets/M0Config/Personalities/Personality_Lazy.asset:15), MoodLines: "귀찮은데... 좀 있다 하지 뭐" / "오늘은 좀 쉬엄쉬엄" / "겨울? 아직 멀었잖아" / "누가 대신 안 해주나" / "배부르면 그만이지" / "천천히 해도 안 죽어" / "내일의 내가 하겠지" / "아, 눕고 싶다" (Personality_Lazy.asset:24-31)
  - 농사꾼 DisplayName "농사꾼" (Assets/M0Config/Personalities/Personality_Farmer.asset:15), MoodLines 예: "흙냄새가 최고야" / "작물은 정직하지" (Personality_Farmer.asset:24-)
  - 새침이 DisplayName "새침이" (Assets/M0Config/Personalities/Personality_Prickly.asset:15)
  - 고집쟁이 DisplayName "고집쟁이" (Assets/M0Config/Personalities/Personality_Stubborn.asset:15)
  - 떠돌이 DisplayName "떠돌이" (Assets/M0Config/Personalities/Personality_Wanderer.asset:15)
  - 성격 6종 성향 벡터 표(근면·대비·사교·모험·자존·겁, M12-F 커밋 48f8a95 본문 인용, Assets/M0Config/Personalities/*.asset 6개 파일에 Traits 필드로 반영됨 — 순둥이 +20/+10/+80/-20/-70/+10, 농사꾼 +60/+80/+20/-60/0/-20, 게으름뱅이 -80/-70/0/+20/+10/0, 새침이 +20/0/-70/0/+60/+30, 고집쟁이 +40/-30/-40/0/+90/-60, 떠돌이 0/-10/-20/+90/+20/0)
  - 사용자 관측 인용 (c1d87e2 커밋 본문, devlog 2026-07-26.md#(+16:19)): "게으름뱅이 둘 다 그냥 죽었어, 별 이야기가 없었다"
  - 사용자 관측 인용 (devlog 2026-07-26.md#(+12:55), fbe6dc1 커밋 본문): "떠돌이 목수가 나오는 경우를 찾기가 힘들어 매번 달라지니깐"
