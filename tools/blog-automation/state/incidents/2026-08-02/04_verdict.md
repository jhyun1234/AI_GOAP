# Step 4 판정서 (1차 검수 — 2차 시도) — 2026-08-02 (M12 성격축/성향벡터 회차)

verify_at: c1d87e2 (브리프 명시). 모든 재확인은 `git show c1d87e2:<path>` 기준으로 수행했다.
HEAD(15c7cd6)로 대조하지 않았다 — HEAD와 다르다는 것 자체는 반려 사유가 아니다.

```
step: 4
verdict: REJECTED
```

---

## self_checked_evidence (마스터가 검수팀과 별개로 직접 실행한 재확인)

- claim: **[R1 해소 여부]** "goal 30개 중 성격 가중치가 붙은 것은 24개, 면제 6개는
  배고픔·휴식·간식 + 명령 3종" (초안 52·54행)
  method: `git ls-tree -r --name-only c1d87e2 -- Assets/M0Config/Goals`로 30개 에셋을
    열거하고, 각 파일을 `git show c1d87e2:<path>`로 열어 `TraitWeights:` 블록에
    실제 `Trait:` 항목이 있는지로 전수 분류(검수팀 스크립트를 쓰지 않고 직접 실행).
  result: **확인됨** — 30개 중 가중치 있음 24 / 없음 6. 없는 6개의 `DisplayName`은
    `Goal_P0_Hunger`="배고픔 해결", `Goal_P0_Fatigue`="휴식", `Goal_Snack`="허기 달래기",
    `Order_ChopWood`/`Order_HarvestBerries`/`Order_MineStone`="명령: 목재/식량/석재".
    **1차 반려 R1(휴식 누락)은 해소됐다.** 초안 54행의 "몸을 챙기는 계열 셋 —
    배고픔과 휴식, 그리고 간식"이 에셋 정본과 1:1 대응한다.

- claim: **[R2 해소 여부]** "계절 넷이 한 바퀴를 돌고, 한 바퀴 전체 길이가 7로
  나누어떨어지지 않는다" (초안 171행)
  method: `git show c1d87e2:Assets/M0Config/Seasons/Season_{Mild,Summer,Autumn,Winter}.asset`
    의 `DisplayName`·`DurationDays` 직접 확인 + `Assets/Scripts/M0/World/SeasonService.cs:68`
    (`foreach (SeasonSO s in _cycle) _totalDays += s.DurationDays;`) +
    `Assets/Scripts/M0/World/WorldConfigSO.cs:93` (`ProfilerIntervalDays = 7f`).
  result: **확인됨** — 온화 3 · 여름 3 · 가을 2 · 겨울 4 = 한 바퀴 12일, 프로파일러 주기 7일.
    12 ÷ 7은 나누어떨어지지 않는다. **1차 반려 R2("계절 길이 8일" 오기)는 해소됐다.**
    허위 수치 "8일"과 그에 딸린 반사실 추론이 초안에서 제거된 것을 확인했다.

- claim: **[R3 — 이번 반려 사유]** "겁이 최고인 주민은 **125**가 되어 무조건 먼저 도망가고,
  중립은 105로 도망을 택하고, 겁이 최저인 무모한 주민은 **85**가 되어 …" (초안 74행)
  method: 검수팀 계산을 믿지 않고 유도 경로를 처음부터 직접 재구성했다.
    1. `git show c1d87e2:Assets/M0Config/Goals/Goal_Flee.asset`
       → L16 `Priority: 105`, L28-29 `Trait: 5 / Weight: 0.8`, L30 `PriorityScale: 20`
    2. `git show c1d87e2:Assets/Scripts/M0/Planning/TraitVector.cs`
       → `Bias() = Mathf.Clamp(Σ(ValueOf(...) / 100f × Weight), -1f, 1f)`
    3. `git show c1d87e2:Assets/Scripts/M0/Data/GoalSO.cs`
       → `TraitBoost(traits) => Mathf.RoundToInt(TraitVector.Bias(traits, TraitWeights) * PriorityScale)`
    4. 런타임 합산 경로에 추가 배율·클램프가 없음을 확인:
       `Assets/Scripts/M0/Agent/VillagerAgent.cs` `BuildGoalBias()` =
       `job.BoostFor(g) + personality.BoostFor(g) + g.TraitBoost(personality.Traits)`,
       `EffectivePriority(g) => g.Priority + _goalBias(g)`,
       `Assets/Scripts/M0/Planning/GoalSelector.cs:60` `int p = g.Priority + bias(g);`
       (6성격 전부 舊 `GoalBoosts: []`라 `personality.BoostFor` 기여는 0)
    5. `git show 976fae8 -- Assets/M0Config/Goals/Goal_Flee.asset` diff 확인 →
       이 커밋이 바꾼 것은 `Priority 92→105`와 `PriorityScale 6→20` **둘뿐이고
       `Weight: 0.8` 줄은 손대지 않았다.**
  result: **불일치 발견** — 겁 +100 → 105 + round(0.8×20) = 105+16 = **121**,
    겁 -100 → 105-16 = **89**. 초안의 125/85가 나오려면 `Weight`가 1.0이어야 하는데
    c1d87e2 시점 정본은 0.8이다.

- claim: **[R3 보강]** 게이트가 125/85를 못 박고 있어서 초안이 옳을 가능성
  method: `git show c1d87e2:Assets/Tests/EditMode/M12_TraitGates.cs`의
    `M12_T3_Flee_OutranksHungerExceptForTheReckless`(L243~) 본문 직접 확인.
  result: **확인됨(초안 편이 아님)** — 게이트는 `Assert.Greater(flee.Priority, hunger.Priority)`,
    `Assert.Less(flee.Priority + flee.TraitBoost(brave), hunger.Priority)`,
    `Assert.Greater(flee.Priority + flee.TraitBoost(timid), flee.Priority)`로
    **부등식만** 검사한다. 121/89에서도 green이므로 게이트는 125/85를 지지하지 않는다.
    즉 이 그린 상태는 초안 수치의 근거가 될 수 없다.

- claim: **[표 검증]** 성향 벡터 표 36칸 (초안 126~133행)
  method: `git show c1d87e2:Assets/M0Config/Personalities/Personality_{Docile,Farmer,Lazy,
    Prickly,Stubborn,Wanderer}.asset`의 `Traits:` 블록을 축 인덱스(0~5) 순으로 직접 출력해
    표와 한 칸씩 대조.
  result: **확인됨** — 36칸 전부 일치(순둥이 20/10/80/-20/-70/10, 농사꾼 60/80/20/-60/0/-20,
    게으름뱅이 -80/-70/0/20/10/0, 새침이 20/0/-70/0/60/30, 고집쟁이 40/-30/-40/0/90/-60,
    떠돌이 0/-10/-20/90/20/0). 추정으로 채운 칸 없음.

- claim: **[내부 링크 실재]** 225행 내부 링크가 실제 발행글을 가리키는가
  method: `tools/blog-automation/published/2026-07-17-unity-goap-m4-personality.md`
    front matter 직접 확인(L2 title, L9 published_url).
  result: **확인됨** — 제목·URL 모두 초안과 일치. 허위 참조 아님.

---

## reviewer_report_accepted

**조건부 신뢰 — 다만 이번 판정의 근거로 삼지는 않았다.**

- 신뢰하는 이유: 검수팀이 직전 반려 지적을 수용해 evidence를 커밋 본문에서 `Assets/`·`.cs`
  원본으로 전면 재작성했고, 내가 독립 재확인한 4개 항목(R1 30개 전수 분류, R2 계절 4종
  길이, 벡터 표 36칸, 내부 링크)이 리포트와 전부 일치했다. 특히 **자기에게 불리한 발견을
  스스로 §2 말미에 올려 마스터 판단을 요청한 점**(74행 121/89 의심)은 러버스탬핑의 반대
  방향이며, 리포트의 성실성을 지지하는 근거다.
- 그럼에도 신뢰만으로 닫지 않은 이유: 74행 수치는 검수팀이 "반려하지 않음 + 마스터 판단
  요청"으로 넘긴 항목이다. 검수팀 판단을 그대로 채택하면(=반려 안 함) 그것이 곧
  러버스탬핑이 된다. 그래서 유도식 4단계와 976fae8 diff를 **처음부터 다시** 계산해
  독립적으로 121/89에 도달한 뒤에 판정했다.
- 리포트가 스스로 밝힌 한계 3건(`.staging/` 비추적으로 바이트 diff 불가 / "열두 밀스톤"
  대조 불가 / 실행 로그·테스트 개수 계열은 커밋 본문이 유일 1차 자료)은 성질상 불가피한
  한계로 인정하며, 이번 반려 사유에 포함하지 않는다.

---

## verdict_reason

### 해소 확인 (반려 사유 아님)

R1(면제 6개 구성)·R2(계절 길이)는 **둘 다 해소됐다.** 위 self_checked_evidence 1·2번
항목대로 마스터가 에셋 정본으로 직접 재확인했다. 수정된 두 문장에 검증되지 않은 새 숫자가
유입되지 않은 것도 확인했다(171행에 남은 숫자는 "넷"과 "7"뿐, 둘 다 정본 확인 완료).

### R3 — 초안 74행 "125 / 85"가 정본(에셋) 계산값과 불일치 [신규 반려]

**정본은 에셋·코드이지 커밋 메시지가 아니다.** 74행의 125/85는 커밋 `976fae8` 본문의
계산("겁 +100 -> 125 … 겁 -100 -> 85")에서 온 값이고, 그 커밋 본문은 `PriorityScale`을
6→20으로 올리면서 **`Weight: 0.8`을 곱하는 것을 빠뜨린 산술 오류**다. 같은 오류가
`GoalSO.cs`의 `PriorityScale` 툴팁 주석("피신(105, 진폭 20)은 겁이 최저인 주민을 85로 내려")
에도 복제돼 있으나, **툴팁은 동작을 설명하는 문서 문자열이지 동작을 결정하는 값이 아니다.**
동작을 결정하는 값은 에셋의 `Weight`·`PriorityScale`과 `TraitBoost()` 식이다.

이것은 이번 사이클에서 내가 이미 R2로 반려한 것과 **동일한 실패 유형**(커밋 본문 수치가
에셋 정본과 어긋남)이다. R2를 반려하고 R3를 통과시키면 같은 사이클 안에서 기준이
달라진다.

**교정 지시 (근거 포함):**

- 74행 `125` → **`121`**
- 74행 `85` → **`89`**

  근거(전부 `git show c1d87e2:<path>` 기준):
  - `Assets/M0Config/Goals/Goal_Flee.asset:16` `Priority: 105`
  - `Assets/M0Config/Goals/Goal_Flee.asset:28-29` `Trait: 5` / `Weight: 0.8`
  - `Assets/M0Config/Goals/Goal_Flee.asset:30` `PriorityScale: 20`
  - `Assets/Scripts/M0/Planning/TraitVector.cs` `Bias()` = `Mathf.Clamp(Σ(v/100f × w), -1f, 1f)`
  - `Assets/Scripts/M0/Data/GoalSO.cs` `TraitBoost()` = `Mathf.RoundToInt(Bias × PriorityScale)`
  - ⇒ 겁 +100: `105 + round(0.8×20)` = `105 + 16` = **121**
  - ⇒ 겁 -100: `105 - 16` = **89**
  - 중립 105는 **그대로 옳다**(가중치 기여 0). 이 숫자는 고치지 말 것.

**대안 (이쪽을 권장한다):** 숫자를 빼고 관계만 서술한다. 예 —
"겁이 최고인 주민은 배고픔보다 확실히 위로 올라가 무조건 먼저 도망가고, 중립은 105로
도망을 택하고, 겁이 최저인 무모한 주민은 배고픔 아래로 내려가 밥부터 찾다가 물립니다."
이유: 121/89를 그대로 쓰면 블로그가 프로젝트 자체 주석(85)과 어긋나 보이고, 아래 R3-b의
문제도 함께 정리된다.

### R3-b — "겁이 최고/최저인 주민"이 실제로는 존재하지 않는다 (같은 문장, 함께 고칠 것)

74행은 121/89(또는 125/85)를 **실제 주민에게 일어나는 일**처럼 서술한다. 그러나 ±100은
축의 이론적 극단이고, 실제 6성격 중 그 값을 가진 주민은 없다.

정본 확인(`git show c1d87e2:Assets/M0Config/Personalities/*.asset`의 `Traits:` `Trait: 5`):
- 겁이 가장 높은 성격 = **새침이 +30** → `105 + round(0.3×0.8×20)` = `105 + 5` = **110**
- 겁이 가장 낮은 성격 = **고집쟁이 -60** → `105 + round(-0.6×0.8×20)` = `105 - 10` = **95**

즉 실제 판에서 "배고픔(100) 아래로 내려가 물리는 무모한 자"는 **고집쟁이(95)**이며,
이는 초안 135행이 이미 쓴 "고집쟁이는 자존 +90에 겁 -60 — 밭을 지키다 늑대에게 물리는
서사가 숫자로 적혀 있습니다"와 정확히 맞물린다. 극단값 대신 고집쟁이를 쓰면 서사가
오히려 강해지고 수치도 정본과 일치한다. 작성팀 판단에 맡기되, **±100 극단을 실제 주민인
것처럼 쓰는 서술은 반드시 정리할 것.**

### 반려로 삼지 않은 것 (작성팀은 고치지 않아도 된다)

- 171행 "계절마다 길이가 다르고": 정본은 3/3/2/4라 온화와 여름이 같다. 다만 이 문장이
  지탱하는 결론("한 바퀴 길이가 7로 나누어떨어지지 않는다")은 참이고, "길이가 제각각"이라는
  통상적 독해 범위 안이다. 편집팀이 다듬는다면 "계절마다 길이가 제각각이고" 정도가 더
  정확하다 — 반려 사유 아님.
- "열두 밀스톤" 표현: 원문에 대응 문구가 없어 대조 불가. 수사적 표현으로 보며 반려 아님
  (직전 판정과 동일).
- 19행·70행 인용구 2건: 코드 의인화이며 본문이 스스로 화면 대사가 아님을 구분한다.
  검수팀 지적대로 **편집팀은 이 둘을 말풍선/캐릭터 대사 서식으로 꾸미지 말 것.**

### 방향/정체성 판정 (참고 — 이 축은 통과)

- 블로그 정체성 적합: **적합.** 1인칭 개발일지, 비개발자 대상 경어체, GOAP·성향 벡터를
  전부 일상어로 풀어 설명한다. "성공 기준은 통과했는데 회상 테스트는 실패했다"는 반전
  구조가 AI_GOAP 개발일지의 정체성과 잘 맞는다.
- 애드센스 리스크(5장): **잔여 없음.** 과장 없음(제목부터 자기 실패를 앞세움), 수익·트래픽
  주장 0건, 저품질 공회전 문단 없음(각 절이 커밋 1~2건에 대응), 민감정보(실명·이메일·키·
  매출·경로) 0건. 등장 수치는 전부 공개 게임 파라미터.
- **방향 자체는 승인 가능한 수준이다. 이번 반려는 오직 74행 수치 정확도 1건이다.**
