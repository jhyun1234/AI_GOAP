# AI_GOAP — Claude 작업 규칙 (M0 개정판, 2026-07-14)

Unity 탑다운 마을 생존 시뮬레이션. GOAP(Burst Job A*) 기반 주민 AI가 핵심.
게임 정체성: "자기 생각이 있는 AI와 협상하는 게임" — 주민은 명령을 거부할 수 있다 (M1 예정).
**최우선 가치: 게임이 재밌어야 한다. 기술적 완성도는 재미의 수단이지 목적이 아니다.**

> 이 문서는 M0 재설계(2026-07-13, `Docs/M0_재설계_실행명세서.md`) 이후 아키텍처 기준이다.
> 舊 아키텍처(VillagerFSM/ActionDatabase/Registry 등 24,800줄)는 W8 커밋 `b175ddf`에서
> 폐기됨 — git 히스토리에만 존재한다. 舊 규칙을 이 문서에서 발견하면 개정 누락이므로 수정할 것.

## 핵심 파일 지도

- **수치·행동의 유일한 집 = 에셋**: `Assets/M0Config/`
  (Actions/·Goals/·Buildings/·ActionCatalog·AgentConfig·WorldConfig·VillagerSprites)
- 데이터 스키마: `Assets/Scripts/M0/Data/` (ActionSO 계열, GoalSO, BuildingSO, SlotId)
- 계획층: `Assets/Scripts/M0/Planning/` (ActionCompiler·PlannerGateway·GoalSelector)
  + `Core/GOAP/GOAPPlannerJob.cs`(**동결된 A* 코어**), `Core/GOAP/GOAPDefs.cs`(브릿지)
- 실행층: `Assets/Scripts/M0/Agent/` (VillagerAgent 5상태 ≤600줄, Runners/, EffectApplier)
- 월드: `Assets/Scripts/M0/World/` (WorldModel·DiscoveryService·ConstructionService)
- 표현: `Assets/Scripts/M0/Presentation/` (AgentAnimator·MoveMotion·PlanBubble·BuildingVisualizer)
- 재사용 라이브러리(검증 완료, 함부로 수정 금지): JPSPathfinder, TileReservationRegistry,
  FowManager, MapChunkRenderer/MapConfig, ResourceNode 3종, CameraController
- 씬: `Assets/Scenes/M0Scene.unity` (유일한 씬)

## 불변 규칙 — ADR-M0 (위반 금지, 변경하려면 사유를 커밋 메시지에)

1. **액션 단일 응집**: 액션 1개의 계획 데이터(전제/효과/비용)·실행 파라미터·말풍선 문구는
   ActionSO 에셋 하나에만 존재한다. **액션 이름으로 분기하는 switch/if를 쓰는 순간 반려** —
   실행은 IActionRunner 다형 디스패치뿐이다. (게이트: M0-T3)
2. **수치 단일 출처 = SO 에셋**: 게임 밸런스 수치는 에셋에만. 코드 상수는 알고리즘 상수만
   허용. 판정 기준: "플레이어가 밸런스 패치로 바꿀 값인가?" — 예이면 SO. 새 수치는
   발명하지 말고 舊 값을 git 히스토리에서 찾거나, 제안치임을 명시하고 승인받는다.
3. **상태 쓰기 단일 지점**: 완공은 ConstructionService.Complete()만, 스톡은 WorldModel만,
   효과 해석은 EffectApplier만(BuildRunner 예외는 AppliesOwnEffects로 명시). 두 번째
   경로가 필요해 보이면 설계 오류 — 멈추고 질문. (게이트: M0-T3 SetBuiltFlag 1건)
4. **플래너 잡 동결**: GOAPPlannerJob의 A* 알고리즘(Closed Set·FNV-1a·MAX_NODES 4096·
   MAX_DEPTH 12) 수정 금지. 입력 형식이 안 맞으면 ActionCompiler가 잡에 맞춘다.
   NoSolutionFound는 버그 증상 — MAX_NODES 인상으로 해결 금지, 진단 순서:
   ① goal/에셋 정합 ② Explore 발견 체인 ③ GoalSelector 후보 제외 여부.
5. **플랜 인덱스 신원**: 플랜 결과 = ActionCatalog 배열 인덱스. 이름 해시 역매핑 금지.
6. **Goal 목표치·발동 임계값 동일 에셋**: GoalSO의 두 필드 + OnValidate 정합 검사.
   목표 달성 상태가 발동 조건을 만족하면 무한 루프다.
7. **이동 실패 first-class** (舊 ADR-8/9 계승): Unreachable/PathBlocked → 좌표 스냅 없이
   AbortPlan → 재계획. `TileX = target...` 강제 대입 금지. (게이트: M0-T3)
8. **타일 두 셀 소유** (舊 ADR-T3~T6 계승): 현재+다음 타일만 예약, 실패·파괴 시
   ReleaseAllBy. 원자성(舊 ADR-2): 자원 차감은 선검사 후 일괄 — 부분 성공 금지.
9. **좌표계**: 2D X-Y 평면, `new Vector3(x, y, 0f)`. 슬롯 확장은 SlotId enum 뒤에만
   append (기존 인덱스 불변 — 에셋 호환).

## 콘텐츠 추가 비용표 (이 표보다 비싸게 작업하고 있다면 설계를 잘못 쓰는 것)

| 작업 | 비용 |
|---|---|
| 기존 계열 액션 추가 (채집/소비/휴식/건설/탐험) | 에셋 1개 + 카탈로그 등록. **코드 0줄** (W7 리허설 증명) |
| Goal 추가 | GoalSO 에셋 1개 + 씬 _goals 등록. 코드 0줄 |
| 건물 추가 | BuildingSO + BuildActionSO 에셋. 코드 0줄 |
| 새 자원 타입 | SlotId.cs 1파일 (enum 2줄 + 매핑 2줄 + Count) + 에셋 |
| 새 실행 계열 | ActionSO 서브클래스 + Runner 1개 (다른 파일 수정 없음 — abstract가 강제) |
| 밸런스·문구 수정 | 에셋 필드만. 코드 diff가 생기면 반려 |

## 작업 프로토콜

- 명세 기반 작업은 spec-write / spec-implement / spec-review 스킬 절차를 따른다.
  명세 항목(W/F/P/N) 1개 = 커밋 1개 (부분 커밋 허용, 빌드 깨진 커밋 금지).
- 명세에 없는 판단이 필요하면 구현하지 말고 선택지+추천을 정리해 질문.
- **설명 의무**: 커밋 보고 시 "게임에서 어떻게 보이는지"를 비개발자용 한 문단으로.
- 씬·에셋 파일은 YAML 직접 작성 가능 (GUID는 신규 발급, .meta 동반 필수).
  사용자는 Editor 배치에 익숙하지 않으므로 확인 절차는 단계별로 정확히 안내한다.

## 커밋 전 체크 (M0 기준 4종)

1. 컴파일: `dotnet build AIVillage.csproj` + `AIVillage.Tests.EditMode.csproj` 오류 0
   (신규 .cs는 csproj에 수동 추가 후 검증 — Unity가 다음 임포트에서 재생성)
2. **EditMode M0-T 게이트 전체 green** — M0-T3가 하드코딩·단일경로·스냅 검사를
   자동화하므로 수동 grep 불필요. 게이트가 못 잡는 위반을 발견하면 게이트 확장이 먼저다.
3. 에셋 커밋이면: 참조 GUID 실재 확인, OnValidate 에러 0
4. Editor 종료 시 NativeArray leak 경고 0 (PlannerGateway Cancel 경로 확인)

## 게임 건강 기준

**10분 무개입 방치 테스트**: 탐험→채집→건설→식사→휴식 순환 유지,
Console에 NoSolutionFound·에러 0건 (PathBlocked 경고는 자기 회복 로그로 허용).

## 자동 커밋 + 개발 일지

모든 의미 있는 단위(버그 1건·기능 1개·테스트 1건) 완료 시 즉시 커밋. 배치 지연 금지.
커밋 메시지: `<type>(<scope>): <요약>` — type ∈ {fix, feat, refactor, test, docs, spec}.
세션 로그는 PostToolUse 훅이 자동 처리 (`devlog/sessions/`, 실패 시 session-log-append 스킬).
문서 허브: `devlog/INDEX.md` (옵시디언 볼트 루트 = 저장소. 과거 기록은 원본보다 INDEX 먼저).

## 응답 규율 (컨텍스트/토큰 절감)

- 종료 요약 1~2문장 + 설명 의무 문단. diff로 보이는 것을 산문으로 반복하지 않는다.
- 툴 호출 사이 나레이션 금지. 방향 전환·발견·차단 시에만 1문장.
- 코드 주석은 "왜"가 자명하지 않을 때만. 파일명·라인·태스크 언급은 커밋 메시지에.
- 명세 밖 아이디어는 코드에 넣지 말고 M1 백로그(메모리/로드맵)에 한 줄.

## 금지 목록

- 플래너 잡(GOAPPlannerJob) 수정 — 동결. 필요해 보이면 "정말 재미에 필요한가"를 질문으로.
- 스코프 밖 기능 추가, 명세 없는 밸런스 수치 변경
- 액션 이름 문자열 분기, 코드 내 게임 수치, 완료 처리 경로 추가 (M0-T3가 감시)
- 재사용 라이브러리(JPS·타일예약·FoW 등) 수정 — 필연적이면 최소 절제 + 커밋에 사유
- 문서(*.md) 신규 생성은 사용자 요청 시에만

## 같은 오해 2회 반복 시

동일 오해 2회 → ADR-M0 신규 항목 제안. M0-T 게이트가 못 잡은 위반 → 게이트 확장 승격.

## M1 백로그 (다음 명세 후보 — 코드에 선반영 금지)

자율 노동 시스템(할 일 없으면 정지) / 식량 생산 체인(열매 재생 없음 → 고갈 위기) /
말풍선 대사 변주 테이블 / 명령·거부(게임 정체성) / 성격 재편입 / Tech Tree·건물 확장 /
PathBlocked 몰림 완화 / 운반(Deposit) 실체화
