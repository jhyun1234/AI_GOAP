# AI_GOAP — Claude 작업 규칙

Unity 탑다운 마을 생존 시뮬레이션. GOAP(Burst Job A*) 기반 주민 AI 70명이 핵심.
게임 정체성: "자기 생각이 있는 AI와 협상하는 게임" — 주민은 명령을 거부할 수 있다.
**최우선 가치: 게임이 재밌어야 한다. 기술적 완성도는 재미의 수단이지 목적이 아니다.**

## 핵심 파일 지도
- 플래너: `Assets/Scripts/Core/GOAP/` (PlannerJob=탐색, Registry=액션데이터, PlanningSlots=상태/Goal, Scheduler=배선)
- 에이전트: `Assets/Scripts/AI/VillagerFSM.cs`(실행), `VillagerBrain.cs`(상태)
- 런타임 수치: `Core/ActionDatabase.cs`, `UI/BuildingCosts.cs`
- 명세 문서: `Docs/` — 작업 전 해당 Phase 명세서를 반드시 읽는다

## 불변 규칙 — ADR (위반 금지, 변경하려면 사유를 커밋 메시지에)

1. **수치의 단일 출처**: 기획서 수치의 원본은 런타임(ActionDatabase/BuildingCosts/FSM 상수)이다.
   플래너(GOAPActionRegistry 상수)가 런타임을 따라간다. **절대 그 반대가 아니다.**
   새 수치가 필요하면 발명하지 말고 기존 코드에서 찾아라. 없으면 멈추고 질문하라.
2. **GoalOp 3종 세트**: 수치 Goal 추가 시 goalState/goalMask/goalOps 3종을 항상 함께 추가한다.
3. **NoSolutionFound는 버그 증상**: MAX_NODES 인상으로 해결 금지.
   진단 순서: ① NodesExpanded 계측 로그 확인 → ② Profiler에서 Planner Job 시간 대조
   → ③ 중복 상태/휴리스틱/무해(無解) Goal 여부 판단.
4. **Sub Effect 0 클램프 필수**: 수치 슬롯은 원시 단위 int (양자화 금지).
5. **불리언 파생 플래그 유지**: WoodLow 등 Phase 3 전까지 제거 금지 (병행 마이그레이션 중).
6. **UseNumericGoals 토글 유지**: 문제 시 즉시 false로 롤백 가능해야 한다.
7. **Goal 목표치와 FSM 발동 임계값 세트 수정**: 목표치 < 임계값이면
   alreadySatisfied(-1) 무한 루프. 둘은 항상 함께 수정한다.
8. **액션 추가 3종 세트**: ActionDef + names[] 배열(해시 역매핑) + Debug.Assert 개수.
   하나라도 빠지면 미완성 커밋이다.

## 작업 프로토콜
- 명세서의 작업 항목(W/F/P/N 번호) 1개 = 커밋 1개. 합쳐 커밋 금지.
- 각 항목의 DoD 체크리스트(`- [ ]`) 전 항목을 `- [x]`로 표시하기 전 "완료"라고 말하지 않는다.
- 명세의 ⚠️(오해 위험) 절은 코드보다 먼저 읽는다.
- 명세에 없는 판단이 필요해지면: 구현하지 말고, 선택지와 추천을 정리해 질문하거나 GitHub 이슈로 남긴다.
- **설명 의무**: 커밋 보고 시, 이번 변경이 게임에서 어떻게 보이는지를 비개발자도 이해할 수 있는
  한 문단으로 요약한다. 소유자가 이해하지 못하는 코드는 완성된 코드가 아니다.
- 커밋 전 체크: ① 컴파일 ② Unity Test Runner EditMode 전체 green ③ 하드코딩 grep 0건
  ④ Editor 종료 시 NativeArray leak 경고 0건
  ⑤ Registry 상수·발동 임계값·액션 추가 커밋이면 T13/T14/T15가 초록불인지 명시 확인
  ⑥ 컨텍스트 비용 배율·MAX_NODES·액션 배율 변경 커밋이면 T16을 실행하고 결과를 커밋 메시지에 인용

## 검증 명령 (커밋 전 실행)
```
grep -rn "GainResource,\s*[0-9]\|ReduceHunger,\s*[0-9]\|ReduceFatigue,\s*[0-9]\|GainHealth,\s*[0-9]" Assets/Scripts
```
→ 결과 0건이어야 커밋 가능.

게임 건강 기준: "10분 무개입 방치 테스트" — 자원 수렴→건설→탐험 사이클 유지,
Console에 NoSolutionFound/Deadlock 0건.

## 금지 목록
- 스코프 밖 기능 추가 금지 (좋은 아이디어는 코드가 아니라 로드맵 문서 하단에 메모)
- 명세 없는 밸런스 수치 변경 금지
- 해시 비교만으로 상태 동일 판정 금지 (Closed Set은 반드시 전수 비교 동반)
- Job 구조 변경 금지 — 동적 요소는 스케줄 시점(메인 스레드)에서 주입
- **플래너 코어(PlannerJob/Slots/Registry 구조) 신규 확장 금지 — 엔진 동결 중.**
  코어 변경이 필요해 보이면 먼저 "정말 재미에 필요한가"를 질문으로 올린다.

## 같은 오해 2회 반복 시 처리
리뷰나 수정에서 동일 오해가 2회 발생하면 → ADR 신규 항목 추가를 사용자에게 능동적으로 제안한다.
T13~T16이 잡지 못한 오해는 새 게이트 추가 안건으로 승격한다.
