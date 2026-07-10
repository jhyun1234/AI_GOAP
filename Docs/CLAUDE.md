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
9. **이동 실패 first-class**: `JPSPathfinder.FindPathResult()`가 `Unreachable`을 반환하면
   호출자는 **좌표 스냅 금지**. 반드시 `Brain.NearDiscoveredResource = false` 강제 후
   `RequestReplanning(AbortReason.PathUnreachable, ...)` 호출. GOAP에 이동 실패를
   "성공"으로 위장 전달하지 않는다 (방향 ② M2 명세, EditMode 게이트 M17/M18).
10. **문맥 배율 무해 방지**: 발견되지 않은 자원 타입의 Gather Goal은 상위 계층
    (`GatherGoalSelector`/`SelectGatherGoalId`)에서 **후보 제외**한다. 미발견 자원의
    컨텍스트 배율(`FULL_NODE_PENALTY × 2 = 10`)이 액션 비용을 폭발시켜 admissible
    휴리스틱이 A*를 안내하지 못하고 MAX_NODES 4096을 소진하며 NoSolutionFound가
    발생한다 (방향 ③ 명세, P1-A 진단 `Docs/이슈_GatherIron_초반_무해.md`).
    검증: EditMode 게이트 T17.
11. **성격 배율 폭발 방지 (ADR-P1)**: `PersonalityCostMultipliers.From()`이 반환하는
    모든 float 값은 `[PersonalityData.MULT_MIN, MULT_MAX] = [0.5, 2.0]`으로 클램프한다.
    성격 배율은 컨텍스트 배율(`FULL_NODE_PENALTY × 2 = 10`)과 곱해지므로 상한 초과 시
    admissible 휴리스틱이 A*를 안내하지 못하고 MAX_NODES 4096을 소진 → NoSolutionFound.
    Glutton은 배율 축이 아니라 `HasActiveP0Condition` 포만감 임계값 축(+10)으로 표현하며
    ADR-7 정합(goal target 70 > 임계값 30, Satiety 세만틱)을 유지한다 (F-A 명세, ADR-P4).
    검증: EditMode 게이트 T18.
12. **침략 예고 스테이지 순서 (ADR-B1)**: `FactionAI`가 `EvaluateRaidDecision`=true를
    받아도 즉시 `IssueRaidOrders`를 부르지 않는다. `_warningStage`는 반드시
    `None → Rumor(D-3) → Confirmed(D-1) → Raid(D0)` 순서로만 전이하며, 각 전이는
    별도 Tick에서만 발생한다(같은 Tick 이중 전이 금지). 예고 조건 소멸(EvaluateRaidDecision
    false로 뒤집힘), F-P0/F-P1 진입, 쿨다운 진입, 실제 침략 개시 모두 `ResetWarningState`
    호출로 초기화. Rumor의 팩션명 노출은 정찰 완료 여부에 종속(ADR-B4: 미상 시
    "미상의 세력" + 텍스트 깜빡임). `PublishInvasionWarning`은 스테이지별 Priority를
    명시 설정하므로 `MessageBus.DEFAULT_PRIORITY_MAP`에 InvasionWarning을 넣지 않는다.
    검증: EditMode 게이트 T19 (F-B 명세, Docs/F-B_침략예고_명세서.md §6 ADR-B1).

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
  ⑦ 이동/경로 관련 커밋이면 `grep -n "Brain\.TileX\s*=\s*targetX" Assets/Scripts` 결과 0건 확인 (ADR-9, 결함 C 부활 감시)
  ⑧ Gather Goal 후보 확장 커밋이면 T17이 초록인지 명시 확인, 그리고 `grep -n "GatherGoalSelector\.Select(" Assets/Scripts` 결과가 1건이고 인자 11개인지 확인 (ADR-10)
  ⑨ 성격 배율·`PersonalityData` 상수·`PersonalityCostMultipliers.From()` 변경 커밋이면 T17·T18이 초록인지 명시 확인,
     그리고 `grep -n "GOAPActionRegistry\.BuildActionDefs(" Assets` 결과 호출부가 성격 배율 축(5인자 이상)을
     인지한 형태인지 확인. 3인자 이하 호출부는 default 폴백(Identity) 의도 명시 (ADR-11/ADR-P1)
  ⑩ 침략 예고 관련 커밋(`FactionAI._warningStage`, `PublishInvasionWarning`, `InvasionWarningIndicator`,
     `MessageType.InvasionWarning` payload 변경)이면 T19가 초록인지 명시 확인, 그리고
     `grep -n "IssueRaidOrders\s*(" Assets/Scripts` 결과가 예고 D0(WSTAGE_CONFIRMED + leadRemaining<=0)
     조건 없이 직접 호출되는 경로가 없는지 확인 (ADR-12/ADR-B1)

## 자동 커밋 + 개발 일지 (devlog-workflow.md 실행)

**모든 의미 있는 개발 단위 완료 시 즉시 커밋한다.** 대상:
- 버그 수정 1건 / 새 기능·시스템 1개 / 아키텍처 변경 1건 / 테스트 케이스 1개 추가
- 명세 항목(W/F/P/N) **진척 단위마다 부분 커밋 허용** (예: W5의 FSM 부분 → 커밋, W5의 테스트 부분 → 별도 커밋, 마지막 "W5 완료" 마무리 커밋 별도).
  단 각 부분 커밋도 상단 "커밋 전 체크 7종"의 ①②를 통과해야 한다(빌드 깨진 채 커밋 금지).

여러 단위를 한 커밋에 합치지 않는다. 배치 지연 금지.

커밋 메시지: `<type>(<scope>): <요약>` — type ∈ {fix, feat, refactor, test, docs, spec}.
명세 항목이면 요약에 W/F/P/N 번호 포함(예: `feat(goap): F1 GatherGoalSelector 5인자 확장 중간 스냅`).

**세션 로그는 훅이 자동 처리한다.** `.claude/settings.json`의 `PostToolUse` Bash 훅이
매 Bash 툴 호출 후 `tools/devlog/append-session-log.sh`를 실행한다. HEAD가 이동했을 때만
동작하며 `devlog/sessions/YYYY-MM-DD.md`에 append/merge(60분 이내 + 같은 태그 = 병합).
로컬 상태는 `tools/devlog/.last-processed-commit`, 오류는 `tools/devlog/.hook-errors.log`.
훅이 무언가로 실패한 경우 `session-log-append` 스킬로 수동 append 가능(폴백).

## 응답 규율 (컨텍스트/토큰 절감)
- 종료 요약은 1~2문장. 무엇을 바꿨고 다음이 뭔지만. 이미 diff나 파일 링크로 보이는 것을 다시 산문으로 풀어쓰지 않는다.
- 툴 호출 사이 진행 나레이션 금지. 방향 전환·발견·차단이 실제로 있을 때만 1문장.
- 단순 질문/짧은 확인에는 헤더·불릿·볼드 쓰지 않는다. 리뷰 판정표·커밋 보고 등 스캔이 필요한 응답에만 구조를 넣는다.
- 커밋 보고와 spec-review 보고는 정해진 형식이 있다. 서문("이제 커밋하겠습니다")·후기("도움이 되었길") 없이 곧장 형식으로 시작한다.
- 코드에 주석은 기본적으로 달지 않는다. **왜**가 자명하지 않을 때(숨은 제약, 특정 버그 회피, 놀라울 동작)만 짧게 남긴다. 파일명·라인번호·"현재 태스크"·"이전 호출자" 언급은 커밋 메시지에 남기고 코드엔 남기지 않는다.
- 명세서에 없는 개선 아이디어가 떠오르면 코드에 넣지 말고 커밋 메시지 하단이나 로드맵에 한 줄로 기록. ADR-금지 규칙과 동일한 이유(스코프 팽창 방지).
- 파일을 새로 만들기 전에 기존 파일 확장 여부를 먼저 검토. 문서(*.md)는 사용자가 요청했을 때만 생성한다.

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
