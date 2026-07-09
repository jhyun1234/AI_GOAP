# F-A 성격 특성 6종 — 실행명세서 (재미 로드맵 P0)

> **작성일**: 2026-07-09
> **선행 조건**: 방향 ③(문맥배율 무해 봉합, ADR-10, T17 게이트) 완료·푸시(4df41cc)
> **후행 안건**: F-B 침략 예고 시스템, F-C 보상 선포
> **참조**: `Docs/게임이해_및_재미설계.md` §3(F-A 절), `Docs/CLAUDE.md` ADR-1·10·커밋 전 체크 ⑥⑧, [[project-fun-roadmap]]

---

## 0. 이 작업이 완료됐을 때 화면에서 새로 보게 되는 것 (재미 검증 절)

같은 침략 이벤트에서 주민 A는 창을 들고 앞으로 뛰쳐나가고, 주민 B는 반대 방향으로 도망친다. 플레이어가 "쟤 왜 저래?" 하고 두 주민을 각각 클릭하면 정보창 최상단에 **[겁쟁이]**, **[용맹]** 라벨과 색상 아이콘이 뜬다. 곧이어 겁쟁이 주민 머리 위에 말풍선 "무서워… 못 싸워!"가 뜨고, 용맹 주민 위엔 "덤벼!"가 뜬다. 이 3초가 창발 서사의 씨앗이다 — 같은 시스템에서 이름·성격·역할이 겹쳐 "우리 겁쟁이 요리사"라는 캐릭터가 태어난다.

**답할 수 없으면 이 명세는 착수하지 않는다.** 성격 6종이 화면에 티가 안 나면(대사도 없고, 라벨도 없고, 행동 차이도 관찰 불가) F-A의 재미 근거는 0이므로 순서를 다시 논의한다.

---

## 1. 측정 가능한 성공 기준

| # | 지표 | 목표 값 | 측정 방법 |
|---|---|---|---|
| S1 | 정보창 성격 라벨 표시 | 선택 주민 100%에 [성격명] 라벨 1줄 표시 | Unity 실행 후 임의 주민 5명 클릭, VillagerOverviewPanel 헤더 확인 |
| S2 | 성격별 대사 발화 | 각 성격 최소 1회 이상 말풍선 발화 관찰 | 방치 5분간 THOUGHT_PERSONALITY_* 카운터 ≥ 1/성격 |
| S3 | 행동 비용 배율 반영 | 겁쟁이 AttackEnemy 비용 ≥ 용맹 AttackEnemy 비용 × 2 | GOAPPlannerJob 로그에서 두 주민의 동일 goal 비용 비교 |
| S4 | 배율 폭발 무회귀 | **T17 3케이스 pass 유지 + T18 3케이스 신규 pass** | Unity Test Runner EditMode 전체 green |
| S5 | 10분 방치 무개입 테스트 | Console `NoSolutionFound` 0건, `Deadlock` 0건 | GameManager 실행 → 10분 방치 → 로그 검사 |
| S6 | 이야기 회상 테스트 | 세션 후 사용자가 "이 겁쟁이 주민이…"로 시작하는 문장 1개 이상 서술 가능 | 사용자 자체 회고 (재미설계 §3 원칙) |

**S3~S5는 컴파일·EditMode·플레이 3층 게이트다. 하나라도 실패하면 커밋 금지.**

---

## 2. 성격 6종 정의

| Personality | 라벨 색상 | 배율 대상 액션 | 배율 값 (제안) | 대사 콘셉트 (제안) |
|---|---|---|---|---|
| Coward (겁쟁이) | `#88DDFF` 하늘 | AttackEnemy | ×2.0 (기피) | "무서워…", "못 싸워!" |
| Brave (용맹) | `#FF6666` 빨강 | AttackEnemy | ×0.7 (선호) | "덤벼!", "지지 않아" |
| Diligent (부지런) | `#FFDD44` 노랑 | ChopWood, MineStone, MineIron, MineCopper, HarvestBerries | ×0.85 (선호) | "쉬는 게 오히려 답답해" |
| Lazy (게으름) | `#AAAAAA` 회색 | ChopWood, MineStone, MineIron, MineCopper, HarvestBerries | ×1.3 (기피) | "잠깐만 쉬었다 하자…" |
| Glutton (대식가) | `#FF88AA` 분홍 | (배율 없음, 대신 SatietyLevel 임계값 +10) | 임계값 조정 | "배고파!", "먹을 거 어딨어?" |
| Curious (호기심) | `#AAFF88` 연두 | Explore | ×0.75 (선호) | "저 너머엔 뭐가 있을까?" |

**⚠️ 제안치임을 명시.** 기존 값이 있으면(현재는 없다) 그것을 우선 사용. 이 값들은 §6 ADR-P2에 따라 **관성 실험 1회 이상 이후에만** 조정 허용.

**대식가 임계값 조정 상세**: `VillagerBrain.HasActiveP0Condition()`의 `SatietyLevel < 20f` 판정을 성격이 Glutton이면 `< 30f`로 올린다 (더 일찍 배고픔을 느낀다). 별도 배율 없이 P0 SurviveHunger가 더 자주 활성화되게 한다. `ActionDatabase`/`GOAPActionRegistry` 상수는 건드리지 않는다 (ADR-1: 수치 단일 출처).
> **세만틱 반전 노트 (2026-07-10)**: `HungerLevel`(높음=배고픔) → `SatietyLevel`(높음=배부름)으로 필드 리네임. 임계값 방향과 오프셋 부호도 함께 반전됨. `SurviveHunger` goal 이름·`ReduceHunger` effect 이름·`GLUTTON_HUNGER_THRESHOLD_OFFSET` 상수 이름은 유지 (필드만 리네임 방침).

---

## 3. 시스템 통합 구조 (개괄)

```
[VillagerRecruitData] — personality 필드 신규 (또는 Random 배분)
        │
        ▼
[VillagerBrain.Personality] — 신규 프로퍼티 (VillagerEnums.Personality)
        │
        ├─▶ [GOAPPlannerScheduler.Schedule()] 
        │       → BuildActionDefs(role, alloc, seasonMod, contextMult, **personalityMult**)
        │       → ContextCostMultipliers × PersonalityCostMultipliers 합성
        │
        ├─▶ [VillagerFSM.ShowThoughtBubble()] 
        │       → THOUGHT_PERSONALITY_* 신규 배열, Idle→Executing 진입 시 확률 발화
        │
        └─▶ [VillagerOverviewPanel.AppendSelectedDetail()] 
                → 헤더 아래 "[성격] 색상라벨" 1줄
```

---

## 4. 작업 항목 (커밋 분할)

각 항목 = 1 커밋. 커밋 순서는 의존 관계에 따른다.

### FA-1 · Personality enum + PersonalityData 상수 테이블 정의  〔중요도: 상, 작업량: 소, 의존: 없음〕

**대상 파일**:
- `Assets/Scripts/AI/VillagerEnums.cs` — enum 추가
- `Assets/Scripts/AI/PersonalityData.cs` — **신규**

**코드 스케치**:
```csharp
// VillagerEnums.cs 하단에 추가
namespace AIVillage.AI
{
    /// <summary>
    /// F-A: 주민 성격 특성 6종. VillagerBrain.Personality가 보관하며,
    /// GOAPActionRegistry.BuildActionDefs에서 액션 비용 배율로,
    /// VillagerFSM.ShowThoughtBubble에서 대사 풀 선택자로,
    /// VillagerOverviewPanel에서 라벨/색상으로 사용한다.
    /// None = 성격 미지정 (레거시 세이브/테스트용 안전값 — 배율 Identity).
    /// </summary>
    public enum Personality { None, Coward, Brave, Diligent, Lazy, Glutton, Curious }
}

// PersonalityData.cs 신규 파일
namespace AIVillage.AI
{
    /// <summary>F-A: 성격별 상수 테이블. 배율·라벨·색상·대사 풀의 단일 출처.</summary>
    public static class PersonalityData
    {
        // 배율 범위 하한/상한 — ADR-P1(배율 폭발 방지) 게이트
        public const float MULT_MIN = 0.5f;
        public const float MULT_MAX = 2.0f;

        // 대식가 P0 임계값 하향 폭
        public const float GLUTTON_HUNGER_THRESHOLD_OFFSET = 10f;

        public static string KoreanLabel(Personality p) { /* switch */ }
        public static string HexColor(Personality p)    { /* switch — 표 §2 값 */ }
    }
}
```

**DoD 체크리스트**:
- [ ] `Personality` enum 7개 값 정의 (None + 6종). `None`이 첫 항목(default 안전).
- [ ] `PersonalityData.KoreanLabel(Personality)`가 6종 각각에 대해 §2 표 라벨 반환.
- [ ] `PersonalityData.HexColor(Personality)`가 6종 각각에 대해 §2 표 색상 반환.
- [ ] `MULT_MIN=0.5f`, `MULT_MAX=2.0f`, `GLUTTON_HUNGER_THRESHOLD_OFFSET=10f` 상수 노출.
- [ ] `None`에 대해 KoreanLabel="없음", HexColor="#FFFFFF" 반환 (테스트 안전).
- [ ] 컴파일 green.

**⚠️ 오해 위험**:
- `enum Personality { Coward, Brave, ... }`로 None을 빼면 default(Personality)=Coward가 되어 세이브/테스트에서 의도치 않은 성격 부여. **None을 반드시 첫 값으로.**
- 배율 값을 이 파일에 하드코딩하지 않는다. 배율은 FA-3의 `PersonalityCostMultipliers`에서 관리 — 라벨/색상 상수와 배율 상수를 한 파일에 섞으면 ADR-1 "수치 단일 출처" 위반.

---

### FA-2 · VillagerBrain.Personality 프로퍼티 추가  〔중요도: 상, 작업량: 소, 의존: FA-1〕

**대상 파일**:
- `Assets/Scripts/AI/VillagerBrain.cs`
- `Assets/Scripts/Core/VillagerRecruitData.cs` — 필드 1개 추가

**코드 스케치**:
```csharp
// VillagerBrain.cs — #region 식별 및 팩션 절 마지막에 추가
/// <summary>F-A: 주민 성격. GOAP 배율·대사·정보창 라벨의 단일 출처.</summary>
public Personality Personality { get; set; } = Personality.None;

// InitFromRecruitData() 마지막 라인에 추가
Personality = data.personality;

// VillagerRecruitData.cs — SerializeField로 신규 필드 (기획팀 편집 가능)
[Header("F-A 성격")]
[Tooltip("None이면 RecruitmentSystem이 6종 중 랜덤 배분.")]
public Personality personality = Personality.None;
```

**RecruitmentSystem 랜덤 배분**: `TryRecruit()`에서 `brain.Personality == None`이면 `(Personality)Random.Range(1, 7)`로 6종 중 하나 배분. (0=None 제외)

**DoD 체크리스트**:
- [ ] `VillagerBrain.Personality` 프로퍼티 존재, 기본값 `Personality.None`.
- [ ] `VillagerRecruitData.personality` SerializeField 존재.
- [ ] `InitFromRecruitData()` 마지막에 `Personality = data.personality;` 있음.
- [ ] `RecruitmentSystem.TryRecruit()` (경로: `Assets/Scripts/Core/RecruitmentSystem.cs`)에서 배분 후 `brain.Personality == None`이면 랜덤 배분 코드 존재.
- [ ] Editor Play → 신규 모집 주민 5명 클릭 로그로 각각 다른 Personality 확인.

**⚠️ 오해 위험**:
- **모집 데이터(ScriptableObject)에 배율 값을 넣지 않는다.** ScriptableObject에는 `Personality` 열거값만 저장. 배율은 FA-3에서 코드 상수로만 결정. (기획팀이 슬라이더로 배율을 조작하면 T18 게이트가 사후 검증만 가능하고 사전 방어 불가.)
- 기존 `.asset` 파일(VillagerRecruitData 인스턴스)은 필드 추가 후 `Personality.None`으로 자동 초기화된다 — RecruitmentSystem 랜덤 배분으로 자연 폴백. 마이그레이션 불필요.

---

### FA-3 · PersonalityCostMultipliers 구조체 + BuildActionDefs 6인자 확장  〔중요도: 상, 작업량: 중, 의존: FA-1〕

**대상 파일**:
- `Assets/Scripts/Core/GOAP/GOAPActionRegistry.cs` — 구조체 신규 + BuildActionDefs 시그니처 확장
- `Assets/Scripts/Core/GOAP/GOAPPlannerScheduler.cs` — Schedule에서 personalityMult 계산·주입
- `Assets/Tests/EditMode/T15_ActionRoundTrip.cs` · `T16_MaxNodesBudget.cs` · `GOAPPlannerTests.cs` — 시그니처 변경 반영

**코드 스케치**:
```csharp
// GOAPActionRegistry.cs — ContextCostMultipliers 아래에 추가
/// <summary>F-A: 성격별 액션 비용 배율. Identity(1f)가 성격 None 케이스.</summary>
public struct PersonalityCostMultipliers
{
    public float ChopWood;
    public float MineStone;
    public float MineIron;
    public float MineCopper;
    public float HarvestBerries;
    public float Explore;
    public float AttackEnemy;
    public float RestOnGround;

    public static PersonalityCostMultipliers Identity => new PersonalityCostMultipliers
    {
        ChopWood=1f, MineStone=1f, MineIron=1f, MineCopper=1f,
        HarvestBerries=1f, Explore=1f, AttackEnemy=1f, RestOnGround=1f
    };

    /// <summary>Personality → 배율 테이블. 모든 결과는 [MULT_MIN, MULT_MAX] 클램프됨.</summary>
    public static PersonalityCostMultipliers From(Personality p)
    {
        var m = Identity;
        switch (p)
        {
            case Personality.Coward:   m.AttackEnemy = 2.0f; break;
            case Personality.Brave:    m.AttackEnemy = 0.7f; break;
            case Personality.Diligent:
                m.ChopWood = m.MineStone = m.MineIron = m.MineCopper = m.HarvestBerries = 0.85f; break;
            case Personality.Lazy:
                m.ChopWood = m.MineStone = m.MineIron = m.MineCopper = m.HarvestBerries = 1.3f; break;
            case Personality.Curious:  m.Explore = 0.75f; break;
            // Coward/Brave의 반대 축(Explore)이나 Glutton은 배율 없음 (§2 표대로)
        }
        Clamp(ref m);
        return m;
    }

    private static void Clamp(ref PersonalityCostMultipliers m)
    {
        m.ChopWood       = UnityEngine.Mathf.Clamp(m.ChopWood,       PersonalityData.MULT_MIN, PersonalityData.MULT_MAX);
        m.MineStone      = UnityEngine.Mathf.Clamp(m.MineStone,      PersonalityData.MULT_MIN, PersonalityData.MULT_MAX);
        m.MineIron       = UnityEngine.Mathf.Clamp(m.MineIron,       PersonalityData.MULT_MIN, PersonalityData.MULT_MAX);
        m.MineCopper     = UnityEngine.Mathf.Clamp(m.MineCopper,     PersonalityData.MULT_MIN, PersonalityData.MULT_MAX);
        m.HarvestBerries = UnityEngine.Mathf.Clamp(m.HarvestBerries, PersonalityData.MULT_MIN, PersonalityData.MULT_MAX);
        m.Explore        = UnityEngine.Mathf.Clamp(m.Explore,        PersonalityData.MULT_MIN, PersonalityData.MULT_MAX);
        m.AttackEnemy    = UnityEngine.Mathf.Clamp(m.AttackEnemy,    PersonalityData.MULT_MIN, PersonalityData.MULT_MAX);
        m.RestOnGround   = UnityEngine.Mathf.Clamp(m.RestOnGround,   PersonalityData.MULT_MIN, PersonalityData.MULT_MAX);
    }
}

// BuildActionDefs 시그니처 확장 (5인자 → 6인자)
public static NativeArray<GOAPActionDef> BuildActionDefs(
    AgentRole                 role,
    Allocator                 alloc,
    float                     seasonGatherMod   = 1f,
    ContextCostMultipliers    contextMult       = default,
    PersonalityCostMultipliers personalityMult  = default)
{
    // ... 각 액션 비용 계산 시:
    // finalCost = baseCost * seasonMod * contextMult.X * personalityMult.X
    // Identity 처리: default(PersonalityCostMultipliers)의 모든 float=0f → Identity로 치환하는 기존 ContextMult와 동일 패턴 사용
}

// GOAPPlannerScheduler.cs — Schedule() 내부
PersonalityCostMultipliers persMult = PersonalityCostMultipliers.From(brain.Personality);
actions = GOAPActionRegistry.BuildActionDefs(role, alloc, seasonGatherMod, contextMult, persMult);
```

**DoD 체크리스트**:
- [ ] `PersonalityCostMultipliers` 구조체 존재, `Identity` 정적 프로퍼티 + `From(Personality)` 정적 팩토리 존재.
- [ ] `From()`이 모든 배율 값을 `PersonalityData.MULT_MIN`~`MULT_MAX`로 클램프.
- [ ] `BuildActionDefs` 6인자 시그니처 (기존 5인자 호출부는 컴파일 실패 → 전부 갱신).
- [ ] Grep: `grep -n "GOAPActionRegistry\.BuildActionDefs(" Assets/Scripts Assets/Tests` 결과가 5인자 0건, 6인자 형태만.
- [ ] `GOAPPlannerScheduler.Schedule()`에서 `PersonalityCostMultipliers.From(brain.Personality)` 호출 후 BuildActionDefs에 전달.
- [ ] T15/T16/GOAPPlannerTests 시그니처 갱신 후 EditMode green.
- [ ] 컴파일 green.

**⚠️ 오해 위험**:
- **배율 합성 순서**: `finalCost = base × seasonMod × contextMult × personalityMult`. **곱셈이므로 순서 무관**하지만, 방향 ③ 명세에서 확립된 컨텍스트 배율 계산 지점을 건드리면 안 된다. 성격 배율은 컨텍스트 배율 옆에 나란히 추가만 한다.
- `default(PersonalityCostMultipliers)`는 모든 float=0f이다. `From(Personality.None)`이 Identity(1f)를 반환해야 하고, BuildActionDefs 내부에서 default 감지 시 Identity로 치환하는 기존 컨텍스트 패턴을 그대로 따른다. **0f를 그대로 곱하면 모든 액션 비용이 0이 되어 플래너가 무한 반복.**
- 클램프는 `From()` 안에서만 한다. 나중에 배율 튜닝하면서 상수 값이 MIN/MAX를 넘어도 자동으로 방어. 그러나 `From()`의 상수 자체가 MIN 미만/MAX 초과이면 클램프로 조용히 잘림 → **커밋 메시지에 배율 값과 클램프 범위를 명시** (T18이 사후 검출).

---

### FA-4 · GameManager 팬아웃 — 대식가 P0 임계값 하향  〔중요도: 중, 작업량: 소, 의존: FA-2〕

**대상 파일**:
- `Assets/Scripts/AI/VillagerBrain.cs` — `HasActiveP0Condition()`, `GetHighestPriorityGoalId()` 수정

**코드 스케치**:
```csharp
// HasActiveP0Condition() — 2026-07-10 세만틱 반전 후 최종형
public bool HasActiveP0Condition()
{
    float satietyThreshold = 20f + (Personality == Personality.Glutton
        ? PersonalityData.GLUTTON_HUNGER_THRESHOLD_OFFSET : 0f);
    return !IsAlive
        || HealthLevel < 20f
        || SatietyLevel < satietyThreshold
        || FatigueLevel > 90f;
}

// GetHighestPriorityGoalId()의 배고픔 판정도 동일 임계값
```

**DoD 체크리스트**:
- [ ] Glutton 성격 주민은 SatietyLevel < 30f에서 SurviveHunger 활성화 (더 일찍 배고픔).
- [ ] 그 외 성격 주민은 SatietyLevel < 20f에서 활성화.
- [ ] ADR-7 규칙(Goal 목표치와 발동 임계값 방향성) — SurviveHunger Goal 목표치가 70(GreaterEq)이므로 임계값 30 < 70 유지, 안전.
- [ ] EditMode: 신규 테스트 `T18_PersonalityGates.cs`의 Case1 pass.
- [ ] 컴파일 green.

**⚠️ 오해 위험**:
- **임계값과 Goal 목표치는 항상 함께 확인 (ADR-7).** 이 커밋은 임계값 하향만 하는데, `SurviveHunger`의 GoalState 목표치(현재 30 이하로 알고 있음)는 그대로다. 목표치가 만약 70~80 사이라면 이 커밋은 무한 루프 유발 — 착수 전 `GOAPStateUtil.BuildGoalState("SurviveHunger", ...)` 목표치를 직접 확인.

---

### FA-5 · VillagerFSM 성격별 대사 발화  〔중요도: 중, 작업량: 중, 의존: FA-2〕

**대상 파일**:
- `Assets/Scripts/AI/VillagerFSM.cs` — THOUGHT_PERSONALITY_* 배열 추가, ShowThoughtBubble 호출 지점에 성격별 확률 발화 추가

**코드 스케치**:
```csharp
// VillagerFSM.cs 상수 절
private static readonly string[] THOUGHT_PERS_COWARD_IDLE   = { "무서워…", "괜찮겠지…?" };
private static readonly string[] THOUGHT_PERS_COWARD_COMBAT = { "못 싸워!", "도망칠래!" };
private static readonly string[] THOUGHT_PERS_BRAVE_COMBAT  = { "덤벼!", "지지 않아" };
private static readonly string[] THOUGHT_PERS_DILIGENT_WORK = { "일이 최고야", "쉬는 게 답답해" };
private static readonly string[] THOUGHT_PERS_LAZY_WORK     = { "잠깐만 쉬자…", "귀찮아…" };
private static readonly string[] THOUGHT_PERS_GLUTTON_ANY   = { "배고파!", "먹을 거 어딨어?" };
private static readonly string[] THOUGHT_PERS_CURIOUS_EXPLR = { "저 너머엔 뭐가?", "가보자!" };

// 성격별 발화 진입점: State_Idle의 goalChanged 분기 또는 Executing 진입 시
// 기존 THOUGHT_GOAL_* 발화 이후에 성격 확률(0.5f) 발화. 성격 라인은 goal 라인을 대체하지 않고 뒤이어 뜨는 게 아니라
// **THOUGHT_MIN_INTERVAL_SEC 스로틀을 공유한다.** 즉 성격 라인이 goal 라인을 스킵할 수 있다.
```

발화 규칙:
- Idle→Executing 진입 시(현재 `THOUGHT_GOAL_*` 발화 지점): 성격이 None이 아니고, 성격/goal 조합이 매치되면 **50% 확률로 성격 라인**, 나머지 50%는 기존 THOUGHT_GOAL_* 라인.
- Fighting 상태 진입 시: Coward/Brave는 항상 전용 라인 발화 (goal 라인 대신).
- 스로틀은 기존 `THOUGHT_MIN_INTERVAL_SEC(5.0f)`를 공유. 별도 카운터 추가 금지.

**DoD 체크리스트**:
- [ ] `THOUGHT_PERS_*` 배열 6종 이상 존재.
- [ ] Idle→Executing 성격별 확률 발화 코드 존재, 확률=0.5f 하드코딩 아닌 상수화 (`PERSONALITY_LINE_CHANCE = 0.5f`).
- [ ] Fighting 진입 시 Coward/Brave 전용 발화 존재.
- [ ] 방치 5분 재현: 6개 성격 각각 최소 1회 이상 대사 발화 관찰(S2).
- [ ] 기존 `_thoughtBubbleCount` 스로틀 게이트를 반드시 통과 (성격 라인이 스팸 유발 금지).
- [ ] 컴파일 green.

**⚠️ 오해 위험**:
- **스로틀 우회 금지.** 성격 라인만 별도 스로틀을 두면 리플래닝 루프에 빠진 겁쟁이가 채널을 마비시킨다. 기존 `THOUGHT_MIN_INTERVAL_SEC(5.0f)` 그대로 사용.
- 대사를 새 파일로 분리하지 말고 VillagerFSM.cs 상수로 둔다 — Phase 1에서 THOUGHT_*가 그렇게 관리되고 있으므로 관성 유지. (로컬라이제이션 리팩터는 별도 안건.)

---

### FA-6 · VillagerOverviewPanel 성격 라벨 표시  〔중요도: 상, 작업량: 소, 의존: FA-1, FA-2〕

**대상 파일**:
- `Assets/Scripts/UI/VillagerOverviewPanel.cs` — AppendSelectedDetail 헤더 아래에 라벨 1줄

**코드 스케치**:
```csharp
// AppendSelectedDetail() — "역할" 라인 위에 추가
_sb.Append("<color=");
_sb.Append(PersonalityData.HexColor(b.Personality));
_sb.Append(">[");
_sb.Append(PersonalityData.KoreanLabel(b.Personality));
_sb.Append("]</color>  ");
// 그 뒤에 기존 "역할: ..." 계속
```

**DoD 체크리스트**:
- [ ] 선택 주민 5명 클릭, 각각 성격 라벨이 정확한 색상으로 헤더에 표시.
- [ ] Personality.None 주민 선택 시 "[없음]" 흰색 라벨.
- [ ] 성격 라벨이 헤더 이름 라인과 시각적으로 구분됨 (`  ` 스페이스 2개로 여백).
- [ ] 컴파일 green.

**⚠️ 오해 위험**:
- 라벨을 세로로 새 줄에 두지 말고 **헤더 옆(가로)** 배치 — 정보창 공간 절약. `▶ 이름 [겁쟁이]  역할: ...` 형태.

---

### FA-7 · T18 성격 배율 회귀 게이트 신규  〔중요도: 상, 작업량: 중, 의존: FA-3, FA-4〕

**대상 파일**:
- `Assets/Tests/EditMode/T18_PersonalityGates.cs` — **신규**

**코드 스케치** (T17 패턴 준용):
```csharp
namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// T18 (F-A ADR-P1 게이트): 성격 배율이 컨텍스트 배율과 합성되어도
    /// admissible 휴리스틱과 MAX_NODES 예산을 초과하지 않음을 정적 회귀로 감지.
    /// </summary>
    public class T18_PersonalityGates
    {
        [Test] public void Case1_Glutton_HungerThreshold_Down10()   { /* Brain.Personality=Glutton, HungerLevel=71 → HasActiveP0Condition==true */ }
        [Test] public void Case2_Lazy_GatherMult_LessThan_MULT_MAX() { /* 게으름의 채집 5종 배율 값 검사 */ }
        [Test] public void Case3_Coward_Danger_Attack_Combined_LessThan_MAX_NODES() 
        { 
            // 겁쟁이 × DangerCtx(DANGER_ATTACK_MULT=0.7f) × personality.AttackEnemy=2.0f 곱셈 시나리오
            // AttackEnemy Goal 플래닝이 MAX_NODES 4096 이내에서 성공하는지 확인
        }
    }
}
```

**DoD 체크리스트**:
- [ ] T18 3케이스 pass.
- [ ] T17(문맥배율 무해 회귀 3케이스)이 T18 추가 후에도 여전히 pass — 성격 배율이 T17 케이스를 깨뜨리지 않았음.
- [ ] T15/T16 pass 유지.
- [ ] Case3에서 NodesExpanded 계측 로그 값이 4096 미만이며, 커밋 메시지에 이 값을 인용(CLAUDE.md 커밋 전 체크 ⑥).

**⚠️ 오해 위험**:
- Case3의 핵심은 "곱셈 폭발이 여전히 안전한가"이지 "겁쟁이가 실제 게임에서 공격을 안 하는가"가 아니다. **NodesExpanded 상한**이 유일한 판정 지표.
- T18을 T17 확장으로 만들지 말고 **별도 파일**로 분리 — 게이트 회귀 분석 시 성격 축을 독립 관찰해야 함.

---

### FA-8 · CLAUDE.md ADR-P1 추가 + 커밋 전 체크 ⑨  〔중요도: 상, 작업량: 소, 의존: FA-3, FA-7〕

**대상 파일**:
- `Docs/CLAUDE.md`

**추가할 내용** (핵심 문구):
```
11. **성격 배율 폭발 방지 (ADR-P1)**: PersonalityCostMultipliers.From()이 반환하는
    모든 float 값은 [PersonalityData.MULT_MIN, MULT_MAX] = [0.5, 2.0]으로 클램프한다.
    성격 배율은 컨텍스트 배율(FULL_NODE_PENALTY×2=10)과 곱해지므로 상한 초과 시
    admissible 휴리스틱이 A*를 안내하지 못하고 MAX_NODES 4096 소진 → NoSolutionFound.
    검증: EditMode 게이트 T18.

⑨ 성격 배율·PersonalityData 상수 변경 커밋이면 T17·T18이 초록불인지 명시 확인,
   그리고 grep -n "GOAPActionRegistry\.BuildActionDefs(" Assets 결과가 전부
   6인자 형태인지 확인 (ADR-P1).
```

**DoD 체크리스트**:
- [ ] CLAUDE.md에 ADR-11 (ADR-P1) 절 추가.
- [ ] 커밋 전 체크 ⑨ 추가.
- [ ] 다른 ADR/체크 절 번호 재부여 없음 (부여하면 diff 폭발).

**⚠️ 오해 위험**:
- ADR 번호 규칙은 **1~10 고정 + 11부터 F-A 스코프**. 방향 ③에서 ADR-10이 마지막이었으므로 F-A는 ADR-11부터. "P1"은 명세 표기용 별칭(planner-safety 1).

---

## 5. 커밋 순서 (권장)

FA-1 → FA-2 → FA-3 → FA-4 → FA-5 → FA-6 → FA-7 → FA-8

각 커밋 메시지 예:
- `feat(ai): FA-1 Personality enum + PersonalityData 상수 테이블`
- `feat(ai): FA-2 VillagerBrain.Personality + RecruitData 필드`
- `feat(goap): FA-3 PersonalityCostMultipliers + BuildActionDefs 6인자 확장 (ADR-P1)`
- `feat(ai): FA-4 대식가 P0 임계값 -10 (ADR-7 정합 확인)`
- `feat(ui): FA-5 성격별 대사 발화 (스로틀 공유)`
- `feat(ui): FA-6 VillagerOverviewPanel 성격 라벨`
- `test(goap): FA-7 T18 성격 배율 회귀 게이트 3케이스`
- `docs(goap): FA-8 CLAUDE.md ADR-11 + 커밋 전 체크 ⑨`

---

## 6. ADR — 미리 결정하는 판단

| ID | 결정 | 사유 | 변경 시 |
|---|---|---|---|
| **ADR-P1** | 성격 배율 하드 클램프 [0.5, 2.0] | 컨텍스트 배율(최대 10)과 곱해져 admissible 파괴 위험. 방향 ③ 봉합의 재발 방지. | 사유를 커밋 메시지에 명시하고 T18 재실행 결과 인용 |
| **ADR-P2** | 배율 튜닝은 관성 실험 후 | §2 배율은 제안치. 붙이자마자 재조정하면 재미 관측 신호와 배율 실험 신호가 섞임. | 최소 1회 방치 세션 후 조정 |
| **ADR-P3** | 성격은 GOAP 상태 슬롯이 아니다 | 성격은 배율에만 영향, GoalState 슬롯을 추가하지 않음. 플래너 코어 동결(CLAUDE.md 규칙) 준수. | 플래너 코어 재개 논의 필요 |
| **ADR-P4** | 대식가는 배율 대신 임계값 조정 | 배고픔 배율을 곱하면 SurviveHunger Goal의 목표치 슬롯과 즉시 충돌(ADR-7). 임계값 축은 이런 리스크 없음. | ADR-7 정합성 재검증 |
| **ADR-P5** | 성격 표시는 라벨+색상만 (아이콘 X) | 아이콘 스프라이트는 자산 준비가 필요. F-A는 데이터·UI·이벤트 수준 유지. | 자산 준비 완료 후 F-A 확장 커밋 |
| **ADR-P6** | 성격 배분은 RecruitmentSystem에서 랜덤 | 향후 F-E 방랑자 이벤트에서 성격 커스텀 배분 필요 — RecruitmentSystem이 단일 배분 지점이면 확장 용이. | F-E 진입 시 재검토 |

---

## 7. 스코프 가드 — 이번에 하지 않는 것

- 성격 조합 상호작용(겁쟁이×용맹 커플 등) — F-D 이후 안건.
- 성격에 따른 대화 유도(플레이어→주민 회유 등) — F-C 보상 선포 이후 안건.
- 성격 아이콘 스프라이트 — ADR-P5.
- 성격 조작(플레이어가 훈련으로 겁쟁이→용맹 전환) — 로드맵 F 이후.
- 로컬라이제이션 시스템 — 대사는 한국어 하드코딩.
- 성격을 GOAP 슬롯화 — ADR-P3.
- **배율 값 튜닝** — 관성 실험 전 금지 (ADR-P2).
- 세이브/로드 필드 확장 — 별도 세이브 시스템 부재. 현재는 씬 로드마다 랜덤 재배분.
- **플래너 코어 확장** — CLAUDE.md 금지 목록 유지. FA-3의 BuildActionDefs 시그니처 확장은 파사드 계층만.

---

## 8. 검증 순서 (커밋 전 체크 요약)

각 커밋 직전:
1. 컴파일 green.
2. Unity Test Runner EditMode 전체 pass (특히 FA-3 이후 T15/T16/T17, FA-7 이후 T18).
3. `grep -rn "GainResource,\s*[0-9]\|ReduceHunger,\s*[0-9]\|ReduceFatigue,\s*[0-9]\|GainHealth,\s*[0-9]" Assets/Scripts` → 0건.
4. FA-3 이후: `grep -n "GOAPActionRegistry\.BuildActionDefs(" Assets` → 6인자 형태만.
5. Editor 종료 시 NativeArray leak 경고 0건.
6. `Docs/CLAUDE.md` 커밋 전 체크 ⑥(컨텍스트 배율 커밋 시 T16 결과 인용) — FA-3, FA-4 커밋 메시지에 NodesExpanded 값 인용.

전체 완료 후:
7. Editor Play → 10분 방치 → Console 정적 관찰 (S5).
8. 이야기 회상 테스트 (S6) — 답이 "없음"이면 §2 배율 값 재조정 후보 목록화.

---

## 9. 참고: 파일 위치 지도

| 새 파일 | 경로 |
|---|---|
| PersonalityData | `Assets/Scripts/AI/PersonalityData.cs` |
| T18 게이트 | `Assets/Tests/EditMode/T18_PersonalityGates.cs` |

| 수정 파일 | 경로 |
|---|---|
| Personality enum | `Assets/Scripts/AI/VillagerEnums.cs` |
| Personality 프로퍼티 | `Assets/Scripts/AI/VillagerBrain.cs` |
| RecruitData 필드 | `Assets/Scripts/Core/VillagerRecruitData.cs` |
| 랜덤 배분 | `Assets/Scripts/Core/RecruitmentSystem.cs` |
| 성격 배율 구조체 | `Assets/Scripts/Core/GOAP/GOAPActionRegistry.cs` |
| Schedule 주입 | `Assets/Scripts/Core/GOAP/GOAPPlannerScheduler.cs` |
| 대사 발화 | `Assets/Scripts/AI/VillagerFSM.cs` |
| 정보창 라벨 | `Assets/Scripts/UI/VillagerOverviewPanel.cs` |
| ADR-P1 + 체크 ⑨ | `Docs/CLAUDE.md` |
| 테스트 시그니처 갱신 | `Assets/Tests/EditMode/T15_ActionRoundTrip.cs`, `T16_MaxNodesBudget.cs`, `GOAPPlannerTests.cs` |

---

## 10. 다음 스텝

이 명세서를 사용자가 승인하면:
1. `spec-implement` 스킬로 진입 → FA-1부터 순차 커밋.
2. FA-3 완료 후 반드시 T15/T16/T17 재실행 → 커밋 메시지에 인용.
3. FA-7·FA-8까지 완주 후 10분 방치 테스트 → 이야기 회상 테스트 → 관성 실험 세션 별도 잡음.

**진입 조건 재확인**: T17 3케이스가 현재 pass인지 사용자에게 확인 요청 (2026-07-09 시점 미검증). pass가 아니면 F-A 착수 전 T17 복구가 선행.
