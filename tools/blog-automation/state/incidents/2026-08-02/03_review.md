# Step 3 검수 리포트 (재검수 #2) — 2026-08-02 (M12 성격축/성향벡터 회차)

verify_at: c1d87e2 (브리프 명시) — 모든 대조는 `git show c1d87e2:<path>` 기준.
HEAD(168ce77)가 아니라 verify_at 시점 트리로 확인했다.

verdict: PASS

**이번 재검수의 방침 변경**: 직전 리포트(재검수 #1)는 상당수 evidence를 `커밋 본문`으로만
닫았고, 마스터가 그 틈에서 사실 오류 2건(R1·R2)을 잡아냈다. 그 지적을 수용해 이번에는
**수치·표시명·문자열 주장을 전부 `Assets/`·`.cs` 원본으로 다시 내려가 닫았다.**
그 결과 R1·R2 해소를 확인했고, **커밋 본문으로만 닫혔던 항목에서 새 불일치 1건을
추가 발견**했다(아래 §2 — 반려는 아니나 마스터 판단 요청).

---

## 1. R1 · R2 반려 사유 해소 확인 (에셋 정본 직접 대조)

### R1 — 면제 6개의 구성 (초안 54행) → **해소됨**

- 수정 확인: 54행 현재 문구
  > "빠진 여섯 개는 일부러 면제했습니다. **몸을 챙기는 계열 셋 — 배고픔과 휴식, 그리고 간식 —**
  > 그리고 플레이어 명령 계열 셋입니다. **먹고 자는 행동**은 뒤에서 따로 이야기할 원칙 때문에 …"
  "먹는 행동 계열 셋" → "몸을 챙기는 계열 셋(배고픔·휴식·간식)"으로 바뀌었고, 휴식(자는 행동)이
  명시적으로 들어갔다.
- checked_against: `git ls-tree -r c1d87e2 -- Assets/M0Config/Goals`의 **goal 에셋 30개 전수**를
  `TraitWeights` 유무로 분류(직접 실행). 결과:
  - 가중치 있음 **24개** / 없음 **6개**
  - 없는 6개 = `Goal_P0_Hunger`("배고픔 해결") · `Goal_P0_Fatigue`("**휴식**") ·
    `Goal_Snack`("허기 달래기") · `Order_ChopWood`("명령: 목재") ·
    `Order_HarvestBerries`("명령: 식량") · `Order_MineStone`("명령: 석재")
  - `Goal_P0_Fatigue` 표시명: `git show c1d87e2:Assets/M0Config/Goals/Goal_P0_Fatigue.asset` L15
    `DisplayName: "휴식"` — 초안의 "휴식"이 정본 표기와 자구 일치
  result: **일치 — 반려 사유 해소**
- 교차 확인(면제 사유의 정확성): `git show c1d87e2:Assets/Tests/EditMode/M12_TraitGates.cs:170`
  `"면제 대상(먹는 행동 3 + 명령 3)을 뺀 전부에 붙어야 한다 (S1)"`, L173 주석 `(ADR-M12-4 ① 몸값 불가침)`,
  L178 `"명령은 ③문턱이 담당"`. 초안 54행의 두 사유 서술(① 원칙 때문에 영구 중립 ② 명령은
  문턱이 담당해 이중 계산 방지)이 게이트 주석과 1:1 대응한다.
  ℹ️ 정본 게이트는 이 셋을 여전히 "먹는 행동 3"이라고 부르지만, 실제 구성에 휴식이 들어 있으므로
  초안의 "몸을 챙기는 계열"이 게이트 문언보다 오히려 정확하다. 초안 80행 ADR 1조항
  "먹기·**자기**·치료" 및 명세서 원문(`Docs/M12_…실행명세서.md:80-82`)과도 정합.
- "처음에 면제가 둘(배고픔과 휴식)인 줄 알았는데 셋이었다"
  checked_against: `git show -s c8a023c` "면제 대상이 P0 2개가 아니라 3개다 — Goal_Snack("허기 달래기"
  포만 35->70)도 먹는 행동이라 ADR-M12-4 대상이다"
  result: 일치 — 마스터 지적대로 '둘'이 P0 두 개(배고픔·휴식)로 바르게 특정됐다.

### R2 — 계절 길이 (초안 171행) → **해소됨**

- 수정 확인: 171행 현재 문구
  > "이 게임의 계절은 **온화·여름·가을·겨울 넷**이 한 바퀴를 도는데, **계절마다 길이가 다르고
  > 한 바퀴 전체 길이도 7로 나누어떨어지지 않습니다.** … 만약 **주기가 계절 한 바퀴와 딱
  > 맞아떨어졌다면** 늘 같은 계절 위치에서만 찍히게 되고, 그건 관측이 아니라 편향입니다."
  "8일"이라는 숫자와 "8로 잡았다면 영원히 같은 계절" 반사실 추론이 **둘 다 제거**됐다.
- checked_against (전부 c1d87e2 에셋·소스 직접):
  - `Assets/M0Config/WorldConfig.asset` L22-26 `SeasonCycle` = GUID 4개.
    GUID→파일 역추적(`git grep -l <guid> c1d87e2 -- Assets/M0Config/Seasons/*.meta`) 결과
    **순서대로 Season_Mild → Season_Summer → Season_Autumn → Season_Winter**.
    초안의 "온화·여름·가을·겨울 넷이 한 바퀴" — 개수·순서·표시명 전부 일치
    (각 에셋 L15 `DisplayName` = "온화"/"여름"/"가을"/"겨울").
  - `DurationDays` = Mild **3** · Summer **3** · Autumn **2** · Winter **4** → 합 **12**
  - `Assets/Scripts/M0/World/SeasonService.cs:68`
    `foreach (SeasonSO s in _cycle) _totalDays += s.DurationDays;` — 사이클 총 길이는 넷의 합
  - `Assets/Scripts/M0/World/WorldConfigSO.cs:93` `public float ProfilerIntervalDays = 7f;`
  - 검증: 12 ÷ 7 = 나누어떨어지지 않음 → 초안의 결론("7일마다 찍히는 프로파일은 매번 다른
    계절 위치에 걸린다")이 정본 수치로 성립한다.
  result: **일치 — 반려 사유 해소**
- ⚠️ 한 가지 단서를 그대로 적어 둔다: "**계절마다 길이가 다르고**"는 엄밀히는 3/3/2/4라
  **온화와 여름이 같다**(넷이 전부 다르지는 않다). 다만 (a) 문장이 지탱하는 결론은
  "한 바퀴 길이가 7로 나누어떨어지지 않는다"이고 이는 참이며, (b) 마스터 판정서 본문도
  정본을 "4계절 순환, 계절마다 길이 다름"으로 기술하고 있다. 반려 사유로 삼지 않되,
  편집팀이 다듬는다면 "계절마다 길이가 제각각이고" 정도가 더 정확하다.
- 새 문장에 들어간 숫자 재확인: 171행에 남은 숫자는 **"넷"과 "7"뿐**이며 둘 다 위에서
  에셋·소스로 닫았다. 검증되지 않은 새 숫자는 **없다**.

### 수정 범위 검증 (두 곳만 고쳤다는 보고의 독립 확인)

- `.staging/`는 `.gitignore`로 git 비추적이라 **바이트 단위 diff는 뜨지 못했다(확인 못함).**
- 간접 확인 2건:
  1. 문자수 17,658 → **17,766 (+108)**. R1 문장 확장(+약 20자)과 R2 문장 재작성(+약 88자)의
     합과 자릿수가 맞는다. 다른 절이 통째로 손대졌다면 이 폭에 들어오기 어렵다.
  2. 그와 별개로 **아래 §2 evidence 전부를 이번 회차에 다시 대조**했다(직전 리포트를 신뢰하고
     넘긴 항목 없음). 절 구조·제목·인트로·표·인용구를 전수 재확인했고 변동 없음.

---

## 2. evidence (사실관계 — 이번 회차는 에셋/소스 원본으로 재대조)

- claim: "goal 서른 개 중 성격 보정이 걸린 건 셋 / 24개로 늘렸다 / 면제 6개"
  checked_against: c1d87e2 goal 에셋 30개 전수 `TraitWeights` 분류(24/6) + `git show -s c8a023c`
  result: 일치

- claim: "그중 여섯 성격이 다 같이 값을 가진 건 겨울 비축 하나, 나머지 둘은 게으름뱅이 전용"
  checked_against: `git show 2e2ce34:Assets/M0Config/Personalities/Personality_*.asset`의 舊 `GoalBoosts`
  result: 일치 (WinterPrep 6성격 공유 / SaveForHome·StoreFood 게으름뱅이 전용)

- claim: 축 6개와 그 이름 "근면·대비·사교·모험·자존·겁", -100~+100, 유도 결과 -1~+1
  checked_against: `git show c1d87e2:Assets/Scripts/M0/Data/TraitId.cs`
    (Diligence=0 근면 / Foresight=1 대비 / Sociability=2 사교 / Wanderlust=3 모험 /
     Willfulness=4 자존 / Caution=5 겁, `[Range(-100,100)]` / `[Range(-1f,1f)]`)
    + `Assets/Scripts/M0/Planning/TraitVector.cs` `Bias() = Mathf.Clamp(Σ(v/100×w), -1, 1)`
  result: 일치

- claim: "{축 이름, 값} 쌍 목록 / 뒤에만 append / 미등록 = 0 = 중립 → 7번째 축 추가해도
    기존 에셋 0개 수정·행동 100% 불변 / 슬롯 규약 복제"
  checked_against: `TraitId.cs` L9-11 주석 및 `TraitVector.ValueOf()` (미발견 시 `return 0`)
  result: 일치 — 커밋이 아니라 코드로 닫았다

- claim: 성향 벡터 표 **36칸 전부**
  checked_against: `git show c1d87e2:Assets/M0Config/Personalities/Personality_{Docile,Farmer,
    Lazy,Prickly,Stubborn,Wanderer}.asset`의 `Traits:` 블록을 축 인덱스 순으로 추출해 한 칸씩 대조
    - 순둥이 20/10/80/-20/-70/10 · 농사꾼 60/80/20/-60/0/-20 · 게으름뱅이 -80/-70/0/20/10/0
    - 새침이 20/0/-70/0/60/30 · 고집쟁이 40/-30/-40/0/90/-60 · 떠돌이 0/-10/-20/90/20/0
  result: **일치 — 36칸 전부. 추정으로 채운 칸 없음**
- claim: 성격 표시명 6종
  checked_against: 위 6개 에셋 `DisplayName` = 순둥이·농사꾼·게으름뱅이·새침이·고집쟁이·떠돌이
  result: 일치

- claim: 비용 가중치 "채집=근면+모험 / 농사=근면+·모험− / 건설=근면 단독 / 탐험=모험 위주"
  checked_against: `git show c1d87e2:Assets/M0Config/TraitRules.asset`
    `GatherWeights` 근면0.6·모험0.4 / `FarmWeights` 근면0.6·모험**-0.4** /
    `BuildWeights` 근면1.0 **단독** / `ExploreWeights` 근면0.2·모험0.8
  result: 일치 (에셋 원본 확인)

- claim: "택지 거리를 벡터에서 — 모험이 높으면 바깥으로, 사교가 높으면 이웃 곁으로 /
    유도 지점은 택지 고르는 함수 안 한 곳"
  checked_against: `TraitRules.asset` `HomeDistanceBias` = 모험(3) +1 / 사교(2) **-1**, Sensitivity 0.95
    + `Assets/Scripts/M0/World/HomePicker.cs:110-113` `PreferredDist()` 단일 지점
  result: 일치

- claim: "서비스 직업(목수·요리사·치료사)에 택지 가산 음수 / 떠돌이 목수 0.95 + (-0.75) = 0.20"
  checked_against: `Job_Carpenter.asset` `HomePreferredDist: -0.75` · `Job_Cook.asset` -0.75 ·
    `Job_Medic.asset` -0.5 (셋 다 음수) + `HomePicker.cs:113` `Mathf.Clamp(..., 0f, 0.95f)`
  result: 일치 (0.95는 클램프 상한, 0.95-0.75=0.20 산술 성립)

- claim: "요리사 첫 값 -0.5는 떠돌이와 겹치면 실효 0.45가 되어 25% 선을 넘는다 → -0.75로 정정"
  checked_against: 최종값 `Job_Cook.asset` -0.75 + `Assets/Tests/EditMode/M12_TraitGates.cs:638`
    `Assert.LessOrEqual(frac, 0.25f)` + 0.95-0.5=0.45 산술
  result: 일치

- claim: "명령 거부 두 조건은 같은 자존 축, 민감도 부호만 반전"
  checked_against: `git show c1d87e2:Assets/M0Config/AgentConfig.asset` L50-64
    `RefuseSatietyBias`(Trait 4 / Sensitivity **+15**) · `RefuseFatigueBias`(Trait 4 / Sensitivity **-15**)
  result: 일치

- claim: "위협 감지 반경 보정을 위협 데이터가 아니라 주민 데이터에 뒀다"
  checked_against: `AgentConfig.asset` L60에 `FleeRadiusBias:` 존재(ThreatSO 아님)
  result: 일치

- claim: "겨울 레시피는 생식 5개로 조리식 1개 / 목표는 조리식 +2였는데 +1로 낮춤 / 소지 상한 8 /
    4096 노드"
  checked_against: `Assets/M0Config/Actions/CookMealScarce.asset` Preconditions 슬롯22 ≥5,
    Effects 슬롯22 **-5** / 슬롯23 **+1** · `Goal_CookAhead.asset` GoalConditions 슬롯23
    Value **1** + `RelativeToCurrent: 1` · `Assets/Scripts/M0/Agent/VillagerAgent.cs:822`
    "몸 소지 상한(**8**)" · `Assets/Scripts/Core/GOAP/GOAPPlannerJob.cs:13` `MAX_NODES=4096`
  result: 일치 — 네 수치 전부 소스/에셋으로 닫음

- claim: "부탁이 성립하려면 6타일 안에서 마주쳐야 한다 / 양쪽이 8초간 말풍선을 주고받는다"
  checked_against: `Assets/M0Config/Requests/Request_BuildMyHouse.asset:33` `RadiusTiles: 6`
    (`Request_CookForMe`·`Request_House_Urgent`도 6) + `AgentConfig.asset:27` `ChatPauseSec: 8`
    + `Assets/Scripts/M0/World/RequestService.cs:200-201` `FaceForChat(..., ChatPauseSec)` 양방향
  result: 일치

- claim: "모닥불 우선순위가 부탁받은 집보다 높다"
  checked_against: `Goal_BuildCampfire.asset` Priority **50** vs `Goal_RequestHouse.asset` Priority **36**
  result: 일치

- claim: "배고픔 목표의 우선순위는 100이고 피신 목표는 92였다 → 105로 올렸다"
  checked_against: `Goal_P0_Hunger.asset` Priority **100** · `Goal_Flee.asset` Priority **105**
    (`git show c8a023c:...Goal_Flee.asset` = 92, `git show 976fae8 -- ...Goal_Flee.asset` diff에서 92→105)
  result: 일치

- claim: "배고픔 목표는 계획이 실패해도 쿨다운 없이 다시 고를 수 있다는 특례를 갖는다"
  checked_against: `Goal_P0_Hunger.asset` `SkipFailureCooldown: 1`
  result: 일치

- claim: "승인 목록 밖 goal이 배고픔 위로 올라오면 자동 검사 실패"
  checked_against: `M12_TraitGates.cs:222` `MayOutrankHunger = { "Goal_Flee" }` +
    `M12_T3_OnlyApprovedGoalsMayOutrankHunger`
  result: 일치 (테스트 실물)

- claim: ADR 개정 3조항(몸값 불가침 / 순서는 급박성 / 배고픔 위에 설 자격 3조건)
  checked_against: `git show c1d87e2:"Docs/M12_성격축_성향벡터_실행명세서.md":80-92` 3조항 전문
  result: 일치 — 초안 80~82행이 원문의 각 조항과 1:1 대응
    ("초 단위 vs 반나절 단위" = 원문 "즉사급(초 단위) > 시간급(`DepartAfterStarvingDays 0.5`일)"의 환산)

- claim: "ADR이 아흔 개 남짓 / 번호가 두 문서에서 어긋남 / 코드는 한 줄도 안 고침 /
    플래너 상수 세 개가 낡음 / 제한 플래그 네 종류 검사 / 낡은 규칙은 예외 없이 허용 목록형"
  checked_against: `git show -s 2183311` ("M0~M12의 **90여 개** ADR", 발견 A "코드는 한 줄도 안
    고쳤다", 발견 B 3개 수치, `M12_T4_RestrictedFlags_UsageMatchesADR` 플래그 4종 열거,
    "낡은 ADR은 예외 없이 허용 목록형이었고")
    + 교차: `git grep -ho "ADR-M[0-9R]*-[0-9]*" c1d87e2 -- Docs | sort -u | wc -l` = **104**
  result: 일치 ("아흔 개 남짓"은 M12 신설분 이전 기준으로 원문 표현과 동일. 104는 M12 신설
    ADR 15개 포함 수치라 모순 아님)

- claim: "옛 성격 필드 열두 개를 삭제하지 않고 값만 중립으로 비웠다 / 새 검사가 옛 필드
    전부 중립인지 확인 / 기존 검사 네 개가 깨졌다"
  checked_against: `git show -s 48f8a95` ("舊 **12필드**는 값만 중립으로 비웠다(필드는 유지)",
    "새 게이트가 舊 필드 전부 중립을 검사하니 이중 적용은 구조적으로 불가능하다",
    "깨진 기존 게이트 **4개**(M4_A·M4_T2·M4_T3·M6_T5)")
    + 실물 교차: `Personality_Prickly.asset`의 `GoalBoosts: []` · `HomePreferredDist: 0` ·
    `GatherCostMult: 1` 등 舊 필드가 존재하되 중립값
  result: 일치

- claim: "확률 하한을 상수로 박았다"
  checked_against: `git show c1d87e2:Assets/Scripts/M0/SimulationLoop.cs:145`
    `private const float MIN_JOB_WEIGHT = 0.05f;`
  result: 일치

- claim: "성격 6종 × 직업 7종이면 42조합"
  checked_against: `git ls-tree -r c1d87e2 -- Assets/M0Config/Jobs` = **7개**
    (Carpenter·Cook·Explorer·Farmer·Lumberjack·Medic·Miner) × 성격 에셋 6개
  result: 일치

- claim: 프로파일러 콘솔 문자열 2종 — `{성격명}: 생존 {N}/{M} · 집 {W} · 노동 {P}% · 공용 {C}% ·
    거부 {R} · 상위goal [{목록}]` 및 `→ S4 분화: 상위3 구성이 다른 성격 쌍 {K}개 (성격 {N}종 중)`
  checked_against: `git show c1d87e2:Assets/Scripts/M0/BehaviorProfiler.cs` L150-156 실물 포맷 문자열
  result: 일치 — **문자열 실재 검사 통과**(구분자 `·`·`%`·`[]`·`→`까지 자구 일치)

- claim: "'노동' 판정을 목표 이름이 아니라 그 목표의 근면 축 가중치가 양수인지로 한다"
  checked_against: `Assets/Scripts/M0/Agent/VillagerAgent.cs:158-164` `GoalWants(goal, axis)` —
    "goal 이름이 아니라 **데이터가 분류를 결정**한다" 주석 + `TraitVector.Bias` 사용
  result: 일치

- claim: "계측기는 읽기 전용 / 세이브 대상 아님"
  checked_against: `BehaviorProfiler.cs` 전문 — 시뮬 상태 쓰기 호출 없음, `Debug.Log`만
  result: 일치

- claim: 사용자 인용 "떠돌이 목수가 나오는 경우를 찾기가 힘들어 매번 달라지니깐"
  checked_against: `git show -s fbe6dc1` 본문 3행
  result: 일치 (자구 동일)
- claim: 사용자 인용 "게으름뱅이 둘 다 그냥 죽었어, 별 이야기가 없었다"
  checked_against: `git show -s c1d87e2` 본문 3~4행
  result: 일치 (자구 동일)

- claim: "15쌍 전부 분화 / 자동 검사 218개 → 252개 / 2026-07-07 선언 / 일곱 밀스톤 /
    첫 로그 38일차 생존 0/0·거부 0 / 순둥이 노동 81% vs 게으름뱅이 2% / 평균 3.1종·8명 4.6종 /
    집 마련 문턱 30 → -50 / 결함 후보 9건 + 파생 3건"
  checked_against: `git show -s c1d87e2` · `2e2ce34`(218 베이스라인) · `eed392b`/`80cfe22`(252) ·
    `789e577` · `30ea98c` · `0a91c40`
  result: 일치 — ⚠️ 단, **이 묶음은 실행 로그·테스트 개수라 에셋으로 닫을 수 없다.**
    커밋 본문이 유일한 1차 자료임을 명시한다(성질상 확인 한계).

- claim: 인용구 2건 — 19행 "성격을 반영하고 싶다고? …" / 70행 "배고파. 밥. 밥. 밥. …"
  checked_against: `git grep "밥. 밥" c1d87e2 -- Assets/` → 히트 없음 / 19행 문구도 히트 없음
  result: **화면 문자열 아님(코드 의인화)으로 면제.** 근거: 19행 앞 문장이 "예전 구조의 속마음을
    사람 말로 옮기면", 70행 뒤 72행이 "화면에서 이건 … 장면으로 보입니다"로 실제 화면을 따로
    서술한다 — 본문이 스스로 UI 대사가 아님을 구분한다.
  ⚠️ 편집팀 참고: 이 게임은 실제 말풍선 대사(`PersonalitySO.MoodLines` 등, 예: 새침이
    "말 걸지 마세요")를 갖고 있으므로, 발행 시 이 두 인용구를 대사 서식(말풍선/캐릭터 대사)으로
    꾸미지 말 것. 독자가 게임 내 대사로 오인한다.

- claim: "열두 밀스톤을 지나오는 동안" / "열두 밀스톤 만에 처음으로"
  checked_against: Docs 밀스톤 명세 목록(M0~M12)
  result: **확인 못함**(수사적 표현. M12를 12번째로 세면 성립, M0 포함이면 13).
    원문에 대응 문구가 없어 대조 불가 — 직전 검수·마스터 판정과 동일하게 반려 사유로 삼지 않음.

### 🟡 마스터 판단 요청 — 커밋 본문으로만 닫혀 있던 수치 1건 (반려 아님)

R2와 **동일한 실패 유형**이 한 곳 더 남아 있어 적어 둔다. 이번 수정으로 생긴 것이 아니고,
직전 회차에 마스터가 "확인됨(최종 상태 일치)"으로 처리한 항목이라 **반려 사유로 삼지 않았지만**,
수치 정확도 기준을 엄격히 적용하면 정정 대상이다.

- claim (초안 74행): "겁이 최고인 주민은 **125**가 되어 무조건 먼저 도망가고, 중립은 105로
  도망을 택하고, 겁이 최저인 무모한 주민은 **85**가 되어 …"
- checked_against (전부 c1d87e2):
  - `Assets/M0Config/Goals/Goal_Flee.asset` — `Priority: 105`, `TraitWeights: [Trait 5(겁),
    Weight **0.8**]`, `PriorityScale: 20`
  - `Assets/Scripts/M0/Planning/TraitVector.cs` — `Bias = clamp(Σ(v/100 × w), -1, 1)`
  - `Assets/Scripts/M0/Data/GoalSO.cs:41-43` — `TraitBoost = RoundToInt(Bias × PriorityScale)`
  - `Assets/Scripts/M0/Agent/VillagerAgent.cs:139-146` — 舊 `GoalBoosts`와 합산이나 6성격 전부
    `GoalBoosts: []`(중립)이라 기여 0
  - ⇒ **계산값: 겁 +100 → 105 + round(0.8×20) = 105+16 = 121 / 겁 -100 → 105-16 = 89**
    (125·85가 되려면 Weight가 1.0이어야 하는데 `c8a023c`·`976fae8` 어느 시점에도 0.8이다 —
    `git show 976fae8 -- Assets/M0Config/Goals/Goal_Flee.asset` diff에서 Weight 줄은 무변경)
- result: **불일치(계산값 기준) / 일치(프로젝트 자체 문서 기준)**
  - 125·85는 `git show -s 976fae8` 본문("겁 +100 -> 125 … 겁 -100 -> 85")에 있고,
    **`Assets/Scripts/M0/Data/GoalSO.cs`의 `PriorityScale` 툴팁에도 "피신(105, 진폭 20)은 겁이
    최저인 주민을 85로 내려"라고 적혀 있다.** 즉 마스터가 요구한 "`.cs`로 닫는다" 기준은
    형식상 충족한다. 어긋난 것은 프로젝트 내부(주석·커밋 vs 실제 가중치 0.8)다.
  - 게이트 `M12_T3_Flee_OutranksHungerExceptForTheReckless`는 등식이 아니라 **부등식**만
    검사하므로(89 < 100 < 105 < 121) 121/89에서도 green이다 — 초안이 지탱하는 결론
    ("무모한 자는 배고프면 위험을 무시한다")은 어느 값에서도 그대로 성립한다.
- 권고: 마스터가 엄격 기준을 적용한다면 74행의 `125`→`121`, `85`→`89` 두 글자 수정으로 끝난다.
  다만 이 경우 블로그 수치가 프로젝트 자체 주석(85)과 어긋나게 되므로, 숫자를 빼고
  "겁이 최고면 배고픔보다 확실히 위, 최저면 배고픔 아래로 내려간다"로 쓰는 쪽도 선택지다.
  검수팀 단독 판단으로는 반려하지 않는다(이번 반려 사유 밖 + 직전 회차 마스터 확인 완료 항목).

---

## 3. 표 검증 (셀 → 산문 방향)

- 표 2개(4작용형식 3열×4행 / 성향 벡터 7열×6행), 표 행 총 14줄. 그 외 표 없음 — 수정 전과 동일.
- **셀 → 산문 대조 (4작용형식 표 12칸)**: 전부 뒤쪽 산문에 근거가 있다 —
  ① 52행 "첫 번째 형식인 우선순위 … 서른 개 중 스물네 개",
  ② 102행 "두 번째 형식인 비용입니다. 채집·농사·건설·탐험 네 계열",
  ③ 110행 "명령 거부는 포만감 퍼센트, 위협 감지는 타일 거리 배율, 보상 선불 요구는 참/거짓",
  ④ 118행 "네 번째 형식인 대상입니다 … 집을 짓고 싶어 하는 거리".
  근거 없는 셀 0개. `—`로 채운 빈 칸 0개.
- **성향 벡터 표 36칸**: §2에서 6개 성격 에셋으로 한 칸씩 대조 완료. 숫자를 보고 순위·위치를
  유추해 채운 칸(2026-08-02 규칙이 겨냥한 사고 유형) **없음**.
- **표를 지워도 글이 성립하는가**: 성립한다. 표 14줄을 걷어내도 **주장 단위**로 다음이 산문에 남는다 —
  4작용형식 네 이름과 각각이 정하는 것(23~38행 + 구현 1~6단계 절 제목), 벡터 표의 결론과 양 끝 값
  (135행이 6성격 전부의 대표 축과 값을 문장으로 다시 적는다: 순둥이 사교+80/자존-70,
  농사꾼 대비+80/모험-60, 게으름뱅이 근면-80/대비-70, 새침이 사교-70/자존+60,
  고집쟁이 자존+90/겁-60, 떠돌이 모험+90). 표만 있고 산문에 없는 주장은 없다.

## 4. 금지 시각 장치 점검

`<pre>`·`<img`·`![`·`<div`·`<table`·`<br`·`<figure` 전수 스캔 **0건**. 유니코드 So/Sk 카테고리
전수 스캔 결과 이모지 **0건**(검출된 것은 백틭 `` ` ``(Sk) 하나뿐 — 코드 인용 기호로 허용 범위.
①②③④는 원문자 No, →는 화살표). ASCII 도식·2단 카드·타임라인·표 밖 단독 막대·그림 파일 0건.
불릿은 201~205행 5줄 한 덩어리뿐이고 앞뒤(199·207행)에 서술형 맥락 문단이 붙어 있다.
수정 전 대비 변동 없음.

## 5. adsense_risk_notes

- 제목에 과장·낚시 없음. "정작 볼 게 없었다"로 자기 실패를 앞세우는 구조.
- 수익·트래픽·성과 주장 0건. 제휴·광고 유도 문구 0건.
- 외부 이미지·외부 인용 0건 → 저작권 표기 대상 없음. 표 2개는 자체 제작 마크다운이라 대상 아님.
- 민감정보 재확인(3장): 실명·이메일·API 키·매출/비용·비공개 계약 정보 없음. 등장 수치는 전부
  게임 내부 파라미터와 테스트 개수. 수정된 54·171행에도 새로 유입된 민감정보 없음
  (계절 이름·기간, goal 표시명은 전부 공개 게임 데이터).

## 6. length_check

`LC_ALL=C.UTF-8 wc -m < tools/blog-automation/.staging/02_draft.md` = **17,766자**
→ 목표 밴드 4,000~45,000자 안(하한 3,800·상한 46,000 어느 쪽도 아님). 분량 사유 반려 없음.
직전 계측 17,658 대비 +108(R1·R2 수정분). 분량 채우기용 공회전 문단 없음 — 각 절이 커밋
1~2건에 1:1 대응한다.

## 7. 톤 / 7단계 구조

후킹 인트로(3~7행) → 문제상황(9행) → 해결과정(23~157행: 설계·구현 1~6단계·ADR 감사·잠복 결함) →
AI와 함께 푼 장면(179행) → 의외의 디테일(191행) → 왜 중요한가(209행) → 다음 예고(227행).
7단계 전부 순서대로 존재. 이번 수정은 54행(해결과정 내부)·171행(관측 도구 절 내부)만 건드려
절 구성·H2 목록에 변동이 없다. 문체(1인칭 회고·경어체·비개발자 대상) 일치.

## 8. seo_checklist

  title_meta: PASS — (a) 제목에 "Unity GOAP · 성향 벡터 · 인디게임 개발일지 · Claude Code
    게임 개발 · AI 페어 프로그래밍 후기" 키워드가 자연스럽게 포함(브리프 seo_keywords 반영).
    (b) "개발일지 N편" 류 포괄 제목이 아니라 "성격 여섯이 전부 다르게 살기 시작했는데,
    정작 볼 게 없었다"는 구체 서술. (c) 인트로 블록(3~7행)에 훅(성격이 닿는 자리가 30개 중 3개)과
    결과물(15쌍 전부 분화, 그러나 "별 이야기가 없었다")이 압축돼 있어 150자 메타 추출 가능.
    제목·인트로는 이번 수정 대상이 아니어서 변동 없음.
  body_structure: PASS — (a) H2 11개 중 "Unity GOAP 성격 시스템", "GOAP 잠복 결함 세 건",
    "4작용형식", "성격별 행동 프로파일러" 등에 검색 키워드 분포. (b) 17,766자로 1,000자 요건
    충족, 문제(왜)/구현 단계(어떻게)/관측 결과(결과)가 절 단위로 분리. (c) 인용구 4개와 로그
    포맷 인용 모두 앞뒤에 맥락 문단 존재(19·70·163·167·215행 확인). (d) 불릿 5줄 1곳뿐,
    나머지 전부 서술형 문단. 수정된 54·171행 둘 다 서술형 문단 안이라 (d) 영향 없음.
  links_images: PASS — 내부 링크 1건이 실물과 일치:
    "주민이 처음으로 '다른 사람'으로 보인 날"
    (https://gamedevclaude.blogspot.com/2026/07/unity-goap_0950458123.html) ↔
    `tools/blog-automation/published/2026-07-17-unity-goap-m4-personality.md` front matter의
    title·published_url 완전 일치 — 허위 참조 아님. 225행 "저번 편의 개인화 경제"도
    `published/2026-07-30-unity-goap-m11-personalized-economy.html` 인트로("성격마다 다른 거리에
    흩어진 홈스테드로 바뀝니다")와 일치. 외부 인용 0건이라 출처 표기 대상 없음.
    이미지 미생성 회차이므로 alt 검증 건너뜀.

## 9. 인트로 중복 검사

`published/` 최근 3편 첫 문단 실물 대조:
- 2026-07-26 M9: "저는 지금 Unity로 마을 시뮬레이션 게임을 혼자 만들고 있습니다. 이 게임의
  주민들은 …" / 예시 명령 **"밭을 갈아라"**
- 2026-07-28 M10: "제가 만드는 마을에 이번 회차에서 처음으로 무덤이 생겼습니다 …" / 예시 명령 없음
- 2026-07-30 M11: "마을 한복판에 창고가 하나 있고, 주민 넷이 거기서 꺼내 먹습니다 …" /
  예시 명령 **"저녁을 지어라"**

이번 초안 첫 문단: "게임 속 캐릭터에게 '성격이 있다'는 말은, 코드 안에서는 정확히 무슨
뜻일까요?" — 질문형 훅으로, 3편 중 어느 것과도 문장 구성이 다르다.
예시 명령: **"겨울을 준비해라"** — `grep -l "겨울을 준비" published/*` 결과 **히트 0**,
즉 최근 3편은 물론 published 전체에서 처음 쓰이는 예시다.
→ 두 FAIL 조건 모두 미해당. **PASS** (인트로는 이번 수정 대상이 아니어서 직전 판정과 동일)

---

## reject_reasons

없음.

- 마스터 반려 사유 **R1·R2 둘 다 해소**됐고, 해소 여부를 커밋이 아니라
  **`Assets/M0Config/Goals/` 30개 전수 · `WorldConfig.asset` · `Season_*.asset` 4개 ·
  `SeasonService.cs:68` · `WorldConfigSO.cs:93`** 원본으로 직접 닫았다.
- 수정된 두 문장에 **검증되지 않은 새 주장·새 숫자는 들어가지 않았다**(171행에 남은 숫자는
  "넷"과 "7"뿐이고 둘 다 정본 확인 완료, 54행은 숫자 없이 표시명만 사용하며 그 표시명이
  에셋 `DisplayName`과 자구 일치).
- 직전에 PASS였던 나머지 항목(표 36칸·4작용형식 12칸, 화면/콘솔 문자열, 금지 시각 장치,
  분량, 톤·7단계, SEO 3항목, 인트로 중복, 애드센스·민감정보)을 **전부 재점검**했고 훼손 없음.

확인 못한 것을 그대로 적어 둔다:
1. `.staging/`가 git 비추적이라 **초안의 바이트 단위 diff는 뜨지 못했다.** "두 곳만 수정"은
   문자수 +108(수정 폭과 정합)과 전 항목 재대조로 간접 확인한 것이다.
2. "열두 밀스톤" 표현은 원문에 대응 문구가 없어 대조 불가(수사적 표현으로 판단, 반려 아님).
3. 실행 로그·테스트 개수 계열 수치(218→252, 노동 81%/2%, 38일차, 3.1종/4.6종, 15쌍)는
   성질상 에셋으로 닫을 수 없어 **커밋 본문이 유일한 1차 자료**다.
4. 🟡 §2 말미의 74행 "125 / 85"는 **에셋 가중치(0.8) 기준 계산값 121 / 89와 어긋난다.**
   프로젝트 자체 주석(`GoalSO.cs`)·커밋(`976fae8`)과는 일치하므로 검수팀 단독으로는 반려하지
   않았다. **마스터 판단을 요청한다** — 정정한다면 74행 두 숫자만 고치면 된다.

→ 마스터 에이전트(blog-master)에게 초안 + 이 리포트를 패키지로 올린다.
