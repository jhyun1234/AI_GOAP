# F-B 침략 예고 시스템 — 실행명세서 (재미 로드맵 P0)

> **작성일**: 2026-07-11
> **선행 조건**: F-A 성격 특성 6종 완료·푸시(`ab28a25`, `549444d`, `b930365`), 블로그 자동화 무인 사이클 확정(`0edb3a9`, `a035fdd`)
> **후행 안건**: F-C 보상 선포, F-D 숙련도+호칭
> **참조**: `Docs/게임이해_및_재미설계.md` §재미엔진③, `Docs/CLAUDE.md` ADR-3·10·11, [[project-fun-roadmap]], [[project-next-session-priorities]]

---

## 0. 이 작업이 완료됐을 때 화면에서 새로 보게 되는 것 (재미 검증 절)

Day 12의 어느 아침, 아무 예고 없이 조용했던 마을에 회색 배너가 켜지고 **[미상의 세력 · 이상한 낌새 · 약 3일 후]** 문구가 깜빡깜빡 뜬다(정찰이 아직 안 됐기 때문). 정찰이 완료되면 [미상의 세력]은 [숲의 부족]으로 바뀌고 깜빡임이 멈춘다. 곧이어 겁쟁이 성격의 요리사 머리 위에 "뭐가… 온다…" 말풍선이 뜨고, 용맹 성격의 벌목꾼은 "덤빌 테면 덤벼봐라" 하고 도끼를 꺼내 든다(대사만, 실제 무장은 F-D). 이틀 뒤 배너가 빨갛게 물들며 **[침략 임박 · 오늘 밤]**으로 바뀌고, 겁쟁이는 Fear 상태 진입 확률이 급등해 집 쪽으로 뛴다. 그날 밤 진짜로 팩션 유닛이 마을 경계에 도착한다.

같은 침략이 예전엔 갑자기 얻어맞는 사건이었는데, 이제는 "온다… 이틀 남았다"의 리듬을 만든다. 이 3~5일이 F-A 성격을 무대에 올려 창발 서사와 결합시키는 지점이다.

**답할 수 없으면 이 명세는 착수하지 않는다.** 예고가 화면에 티가 안 나면(배너도 없고, 주민 대사도 없고, 실제 침략과 시각·행동 차이 없음) F-B의 재미 근거는 0이므로 순서를 다시 논의한다.

---

## 1. 측정 가능한 성공 기준

| # | 지표 | 목표 값 | 측정 방법 |
|---|---|---|---|
| S1 | 예고 → 실제 침략 지연 | 최소 3 게임일(≒ 제안치) 이상 지연 | Console 로그 `[FactionAI] InvasionWarning 발행 Day=X` → `[FactionAI] IssueRaidOrders Day=Y`, `Y - X >= 3` |
| S2 | 예고 배너 UI 표시 | Warning 진입 시 화면 상단 인디케이터 100% 표시, 남은 일수 카운트 매 실시간 초 갱신 | Editor Play → FactionAI DEBUG 강제 침략 → 인디케이터 관찰 |
| S3 | 주민 예고 반응 대사 | Warning/Confirmed 각 단계 최초 발행 후 30초 이내 THOUGHT_INVASION_* 발화 카운트 ≥ 1 | Console `[VillagerFSM] Thought:` 로그 |
| S4 | 예고 → 실제 스테이지 전환 무결함 | Rumor(약한 조짐) → Confirmed(확실 신호) → Raid(실제 명령) 순서 위반 0건 | T19 EditMode 3케이스 pass |
| S5 | T17·T18 게이트 무회귀 | 전 케이스 유지 pass | Unity Test Runner EditMode 전체 green |
| S6 | 10분 방치 무개입 테스트 | Console `NoSolutionFound` 0건, `Deadlock` 0건, MessageBus 누락 경고 0건 | GameManager 실행 → 방치 → 로그 검사 |
| S7 | 이야기 회상 테스트 | 세션 후 사용자가 "예고가 뜨자 우리 겁쟁이 요리사가…"로 시작하는 문장 1개 이상 서술 가능 | 사용자 자체 회고 |

**S4·S5·S6은 컴파일·EditMode·플레이 3층 게이트다. 하나라도 실패하면 커밋 금지.**

---

## 2. 예고 스테이지 정의

| Stage | 게임일 (침략 D0 기준) | 배너 색상 | 표기 문구 (제안) | 발행 메시지 |
|---|---|---|---|---|
| **None** | — | (표시 없음) | — | — |
| **Rumor (약한 조짐)** | D − 3 | `#888888` 회색 | "[팩션명] · 이상한 낌새 · 약 3일 후" (미상 시 텍스트 깜빡임) | `InvasionWarning`, StageId=Rumor |
| **Confirmed (확실 신호)** | D − 1 | `#DD3333` 빨강 | "[팩션명] · 침략 임박 · 오늘 밤" | `InvasionWarning`, StageId=Confirmed |
| **Raid (실제 발발)** | D 0 | (기존 EnemyDetected 흐름으로 이관) | — | `RaidDecision`, `EnemyDetected` (기존 유지) |

**⚠️ 제안치**: `WARNING_LEAD_DAYS_RUMOR = 3f`, `WARNING_LEAD_DAYS_CONFIRMED = 1f`는 §5 ADR-B2 관성 실험 1회 이상 이후에만 조정 허용. 기존 값이 있으면(현재는 없다) 그것을 우선.

**팩션명 노출 규칙 (ADR-B4)**: Rumor 단계는 `_playerStrengthKnown == false`(정찰 이전)일 때 "미상의 세력"으로 대체 표기. Confirmed 단계는 정찰 여부와 무관하게 실제 팩션명 노출. — 정찰(F-P3)이 이미 있는데 예고가 완전 정보라면 정찰의 역할이 사라지므로 정보 축을 나눈다.

---

## 3. 시스템 통합 구조 (개괄)

```
[FactionAI.EvaluateRaidDecision()] — 기존 true 반환 지점
        │  변경: true → 즉시 침략 실행이 아니라 예고 진입 판정
        ▼
[FactionAI._pendingRaidWarning]   ── 신규 상태 머신
    None → Rumor(D-3) → Confirmed(D-1) → Raid(D0) → None
        │
        ├─▶ MessageBus.Publish(InvasionWarning, StageId)
        │       ├─▶ [InvasionWarningIndicator] UI 배너 갱신
        │       └─▶ [VillagerFSM.OnInvasionWarning()] 성격별 대사 발화
        │
        └─(D0에서)▶ 기존 PublishRaidDecision + IssueRaidOrders
```

**핵심 원칙**: 예고 계층은 FactionAI에 얹기만 한다. 플래너 코어(GOAP)는 건드리지 않는다 (CLAUDE.md 금지 목록 · ADR-P3 준용). VillagerBrain에 예고를 GOAP 슬롯으로 넣지 않는다 — 반응은 대사와 확률적 감정 진입뿐.

---

## 4. 작업 항목 (커밋 분할)

각 항목 = 1 커밋. 커밋 순서는 의존 관계에 따른다.

### FB-1 · InvasionWarning MessageType + Payload  〔중요도: 상, 작업량: 소, 의존: 없음〕

**대상 파일**:
- `Assets/Scripts/AI/VillagerEnums.cs` — MessageType enum 확장
- `Assets/Scripts/Core/MessageBus.cs` — Payload 구조체 신규

**코드 스케치**:
```csharp
// VillagerEnums.cs — MessageType enum 하단에 추가
InvasionWarning,   // [F-B] 팩션 침략 예고 (Rumor/Confirmed 2단계)

// MessageBus.cs 하단에 Payload 추가
public struct InvasionWarningPayload
{
    public string FactionId;
    public string StageId;              // "Rumor" | "Confirmed"
    public int    ExpectedRaidDay;      // 예상 침략 게임일
    public float  LeadDaysRemaining;    // D일 - 현재 게임일
    public bool   FactionNameKnown;     // 정찰 완료 여부 (ADR-B4)
}
```

**DoD 체크리스트**:
- [x] `MessageType.InvasionWarning` 존재 (마지막 값으로 추가하여 기존 정수 매핑 유지).
- [x] `MessageBus.InvasionWarningPayload` 구조체 존재, 필드 5개 정확히 위 스케치대로.
- [x] `StageId` 상수화: `MessageBus.INVASION_STAGE_RUMOR = "Rumor"`, `INVASION_STAGE_CONFIRMED = "Confirmed"` 정적 필드로 노출.
- [x] 컴파일 green (mcp__ide__getDiagnostics 0건, 2026-07-11).

**명세 대비 변경**: 스케치는 `MessageBus.INVASION_STAGE_RUMOR`를 "정적 필드"로 표현했으나 실제 코드는 `public const string`으로 구현. const는 컴파일 타임 상수가 되어 switch/expression 매칭이 가능해지고 스위치 문에서도 사용 가능(readonly static은 불가). 참조 방식(`MessageBus.INVASION_STAGE_RUMOR`)은 동일하므로 하위 항목 스케치와 호환. **DEFAULT_PRIORITY_MAP에 InvasionWarning을 넣지 않음** — 스테이지별 Priority 차이(Rumor=Medium, Confirmed=High)를 살리기 위한 의도적 예외. Map 주석에 [F-B 예외] 절 추가.

**⚠️ 오해 위험**:
- MessageType enum 값 **순서 변경 금지**. 새 값은 반드시 마지막 위치에 추가. 순서 뒤바꾸면 SeasonChanged=8, WinterCrisis=9 등 하드코딩된 정수 캐스팅과 어긋난다.
- Payload에 `System.DateTime` 넣지 않는다. 게임 시간 축은 `float GameTime` 하나뿐(ADR-3 준용). 실제 시각은 세션 로그가 처리.

---

### FB-2 · FactionAI 예고 상태 머신 필드 + EvaluateRaidDecision 분기  〔중요도: 상, 작업량: 중, 의존: FB-1〕

**대상 파일**:
- `Assets/Scripts/Core/FactionAI.cs`

**코드 스케치**:
```csharp
// #region 상수 절에 추가
private const float WARNING_LEAD_DAYS_RUMOR     = 3f; // ⚠️ 제안치 (ADR-B2)
private const float WARNING_LEAD_DAYS_CONFIRMED = 1f; // ⚠️ 제안치 (ADR-B2)

// 상태 enum (파일 내부 nested type 또는 int 상수 3개 중 택1 — 후자 권장, 확장성 낮으므로)
private const int WSTAGE_NONE      = 0;
private const int WSTAGE_RUMOR     = 1;
private const int WSTAGE_CONFIRMED = 2;

// #region Private Fields 절에 추가
/// <summary>F-B: 현재 예고 스테이지. 0=None, 1=Rumor, 2=Confirmed.</summary>
private int   _warningStage       = WSTAGE_NONE;
/// <summary>F-B: 예고 트리거된 시점의 예상 침략 게임일. Rumor 발행 시 확정 후 불변.</summary>
private float _expectedRaidDay    = -1f;
/// <summary>F-B: Confirmed 스테이지 진입까지 이미 발행했는지. Rumor→Confirmed 중복 방지.</summary>
private bool  _rumorPublished     = false;
private bool  _confirmedPublished = false;

// EvaluateAndExecuteGoal() 안의 F-P2 분기 개조:
// 기존: if (EvaluateRaidDecision(playerStrength)) { ... PublishRaidDecision(); IssueRaidOrders(mode); }
// 신규:
if (EvaluateRaidDecision(playerStrength))
{
    // 상인 연합 TradeProposal 흐름은 기존 그대로 유지 (ADR-B5 준용).
    if (_factionId == FACTION_MERCHANT && !_tradeProposalSent) { ... 기존 그대로 ... return; }

    // 예고 스테이지 관리 — 실제 침략은 D0에서만 실행
    if (_warningStage == WSTAGE_NONE)
    {
        _warningStage    = WSTAGE_RUMOR;
        _expectedRaidDay = currentGameDay + WARNING_LEAD_DAYS_RUMOR;
        PublishInvasionWarning(MessageBus.INVASION_STAGE_RUMOR, currentGameDay);
        _rumorPublished = true;
        return;
    }

    float leadRemaining = _expectedRaidDay - currentGameDay;

    if (_warningStage == WSTAGE_RUMOR && leadRemaining <= WARNING_LEAD_DAYS_CONFIRMED)
    {
        _warningStage = WSTAGE_CONFIRMED;
        PublishInvasionWarning(MessageBus.INVASION_STAGE_CONFIRMED, currentGameDay);
        _confirmedPublished = true;
        return;
    }

    if (_warningStage == WSTAGE_CONFIRMED && leadRemaining <= 0f)
    {
        if (!_isRaiding)
        {
            _isRaiding = true;
            RaidMode mode = DetermineRaidMode(playerStrength);
            PublishRaidDecision(playerStrength);
            IssueRaidOrders(mode);
            _warningStage       = WSTAGE_NONE;   // 다음 침략 사이클 대비 초기화
            _rumorPublished     = false;
            _confirmedPublished = false;
            _expectedRaidDay    = -1f;
        }
        return;
    }

    // 예고 진행 중(스테이지 유지) — 이번 Tick은 아무 것도 안 함
    return;
}

// 조건이 도중에 깨진 경우(예: 정찰로 playerStrength 재파악 → playerWeak false 전환) 예고 취소
if (_warningStage != WSTAGE_NONE)
{
    Debug.Log($"[FactionAI] 예고 취소 — 침략 조건 소멸. Stage={_warningStage}, FactionId={_factionId}");
    _warningStage       = WSTAGE_NONE;
    _rumorPublished     = false;
    _confirmedPublished = false;
    _expectedRaidDay    = -1f;
}
```

**DoD 체크리스트**:
- [ ] 4개 신규 필드 + 3개 상수(WSTAGE_*) + 2개 lead 상수 존재.
- [ ] Rumor → Confirmed 순서 위반 코드 경로 없음(반드시 leadRemaining 비교 단계별로만 전환).
- [ ] Confirmed 진입 후 leadRemaining <= 0에서만 실제 `PublishRaidDecision` + `IssueRaidOrders` 호출.
- [ ] 조건 소멸 시(정찰 등으로 playerWeak false 전환) 예고 취소 및 상태 초기화.
- [ ] 침략 쿨다운 진입 시 (`_raidCooldownRemaining` 설정 지점)에도 상태 초기화 코드 존재.
- [ ] 컴파일 green.

**⚠️ 오해 위험**:
- **`EvaluateRaidDecision` 자체를 개조하지 않는다.** 트리거 판정 로직은 그대로. F-B는 판정 결과 사용법만 바꾼다. — 판정 로직에 예고 상태를 섞으면 T19가 판정 원인과 예고 상태를 분리 관찰 불가.
- 상인 연합의 `TradeProposal` 흐름은 예고 스테이지 진입 **이전** 단계다. TradeProposal 발행 후 다음 Tick에 다시 EvaluateRaidDecision가 true면 그때 Rumor 진입 (ADR-B5).
- 예고 스테이지 진입 후에도 `_raidCooldownRemaining > 0`이면 continue로 건너뛴다 — 기존 쿨다운 게이트를 통과할 수 없다. **쿨다운 진입 시 예고 상태도 함께 초기화** 코드를 반드시 넣을 것 (예고 유령 상태 방지).

---

### FB-3 · PublishInvasionWarning 메서드  〔중요도: 상, 작업량: 소, 의존: FB-1·FB-2〕

**대상 파일**:
- `Assets/Scripts/Core/FactionAI.cs`

**코드 스케치**:
```csharp
private void PublishInvasionWarning(string stageId, float currentGameDay)
{
    if (MessageBus.Instance == null)
    {
        Debug.LogWarning($"[FactionAI] PublishInvasionWarning: MessageBus null. Stage={stageId}, FactionId={_factionId}");
        return;
    }

    var payload = new MessageBus.InvasionWarningPayload
    {
        FactionId          = _factionId.ToString(),
        StageId            = stageId,
        ExpectedRaidDay    = Mathf.RoundToInt(_expectedRaidDay),
        LeadDaysRemaining  = Mathf.Max(0f, _expectedRaidDay - currentGameDay),
        FactionNameKnown   = _playerStrengthKnown  // ADR-B4: 정찰 여부 노출 제어
    };

    MessageBus.Instance.Publish(new AIMessage
    {
        Type     = MessageType.InvasionWarning,
        Priority = stageId == MessageBus.INVASION_STAGE_CONFIRMED ? MessagePriority.High : MessagePriority.Normal,
        SenderId = $"faction_{_factionId}",
        Payload  = payload,
        IssuedAt = Time.time
    });

    Debug.Log($"[FactionAI] InvasionWarning 발행. Stage={stageId}, ExpectedDay={_expectedRaidDay:F1}, " +
              $"LeadRemain={payload.LeadDaysRemaining:F1}일, FactionId={_factionId}");
}
```

**DoD 체크리스트**:
- [ ] Rumor 발행 시 Priority=Normal, Confirmed 발행 시 Priority=High.
- [ ] `LeadDaysRemaining` 음수 방지 (Mathf.Max).
- [ ] Console 로그에 Stage/ExpectedDay/LeadRemain 3개 값 포함 (T19가 이 로그로 검증).
- [ ] 컴파일 green.

**⚠️ 오해 위험**:
- **Rumor에도 High Priority를 주지 않는다.** MessageBus의 High 큐는 EnemyDetected(RAID_THREAT_TIER 4)와 공유된다. Rumor를 High로 올리면 임박 신호와 조짐 신호가 같은 큐에서 섞여 주민 반응이 마비된다.

---

### FB-4 · InvasionWarningIndicator UI 컴포넌트  〔중요도: 상, 작업량: 중, 의존: FB-1〕

**대상 파일**:
- `Assets/Scripts/UI/InvasionWarningIndicator.cs` — **신규**

**배치 규칙**: **위치·앵커·크기는 사용자가 씬에서 자유롭게 조절한다.** 스크립트는 위치 정보를 하드코딩하지 않는다 (`RectTransform.anchoredPosition`/`anchors` 자동 세팅 금지). 프리팹 또는 씬의 Canvas 하위 어디에 붙여도 동작.

**코드 스케치**:
```csharp
namespace AIVillage.UI
{
    /// <summary>
    /// F-B: 침략 예고 배너.
    /// MessageBus.InvasionWarning 구독 → 스테이지별 색상/문구 갱신 → D0 도달 시 소멸.
    /// 씬 배치 위치·크기는 사용자가 자유롭게 조절 (스크립트는 Transform 건드리지 않음).
    /// 미상의 세력 Rumor 상태에서는 텍스트 깜빡임 효과로 정보 부족을 시각화한다.
    /// </summary>
    public sealed class InvasionWarningIndicator : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────
        [SerializeField] private TMPro.TextMeshProUGUI _label;
        [SerializeField] private UnityEngine.UI.Image  _background;

        // ── 깜빡임 파라미터 (제안치, ADR-B2 준용) ────────────────────────────
        [Tooltip("미상의 세력 Rumor 표기일 때 텍스트 깜빡임 주기(초). 사용자 조절.")]
        [SerializeField] private float _blinkPeriodSec = 0.8f;
        [Tooltip("깜빡임 시 텍스트 최소 알파(0~1). 0=완전 사라짐, 1=깜빡임 없음.")]
        [SerializeField, Range(0f, 1f)] private float _blinkMinAlpha = 0.25f;

        private static readonly Color COLOR_RUMOR     = new Color(0.53f, 0.53f, 0.53f, 0.85f);
        private static readonly Color COLOR_CONFIRMED = new Color(0.87f, 0.20f, 0.20f, 0.90f);

        private bool   _active              = false;
        private float  _expectedRaidDay     = -1f;
        private string _stageId             = null;
        private string _factionDisplayName  = null;
        /// <summary>ADR-B4: 팩션명 미상 상태 여부. true일 때만 텍스트 깜빡임 활성.</summary>
        private bool   _factionNameUnknown  = false;

        private void OnEnable()
        {
            if (MessageBus.Instance != null)
                MessageBus.Instance.Subscribe(MessageType.InvasionWarning, OnWarning);
            SetVisible(false);
        }

        private void OnDisable()
        {
            if (MessageBus.Instance != null)
                MessageBus.Instance.Unsubscribe(MessageType.InvasionWarning, OnWarning);
            // 씬 이탈 시 텍스트 알파를 원상 복구 (다음 활성화 시 초기 상태 보장)
            RestoreLabelAlpha();
        }

        private void Update()
        {
            if (!_active) return;
            float remain = _expectedRaidDay - (GameManager.Instance != null ? GameManager.Instance.GameTime : 0f);
            if (remain <= 0f) { SetVisible(false); _active = false; RestoreLabelAlpha(); return; }
            _label.text = FormatLabel(remain);
            ApplyBlinkIfUnknown();
        }

        private void OnWarning(AIMessage msg)
        {
            var p = (MessageBus.InvasionWarningPayload)msg.Payload;
            _stageId             = p.StageId;
            _expectedRaidDay     = p.ExpectedRaidDay;
            _factionNameUnknown  = !p.FactionNameKnown;
            _factionDisplayName  = p.FactionNameKnown ? FactionNameFromId(p.FactionId) : "미상의 세력";
            _background.color    = (p.StageId == MessageBus.INVASION_STAGE_CONFIRMED) ? COLOR_CONFIRMED : COLOR_RUMOR;
            _active = true;
            SetVisible(true);
            // Confirmed 진입 시 깜빡임 강제 종료 (팩션명이 뒤늦게 확정된 케이스 대비)
            if (_stageId == MessageBus.INVASION_STAGE_CONFIRMED) RestoreLabelAlpha();
        }

        private void ApplyBlinkIfUnknown()
        {
            // Rumor + 미상 조합에서만 깜빡임. Confirmed거나 팩션명 확정이면 알파 1 유지.
            if (!_factionNameUnknown || _stageId == MessageBus.INVASION_STAGE_CONFIRMED)
            {
                RestoreLabelAlpha();
                return;
            }
            float t = Mathf.PingPong(Time.unscaledTime / Mathf.Max(0.01f, _blinkPeriodSec), 1f);
            float a = Mathf.Lerp(_blinkMinAlpha, 1f, t);
            Color c = _label.color; c.a = a; _label.color = c;
        }

        private void RestoreLabelAlpha()
        {
            if (_label == null) return;
            Color c = _label.color; c.a = 1f; _label.color = c;
        }

        private string FormatLabel(float remain)
        {
            if (_stageId == MessageBus.INVASION_STAGE_CONFIRMED)
                return $"[{_factionDisplayName}] · 침략 임박 · 오늘";
            return $"[{_factionDisplayName}] · 이상한 낌새 · 약 {Mathf.CeilToInt(remain)}일 후";
        }

        private void SetVisible(bool v) { gameObject.SetActive(v); }

        private static string FactionNameFromId(string id) =>
            id switch { "0" => "숲의 부족", "1" => "철의 도시", "2" => "상인 연합", _ => "알 수 없는 세력" };
    }
}
```

**DoD 체크리스트**:
- [ ] `Assets/Scripts/UI/InvasionWarningIndicator.cs` 신규 파일 존재.
- [ ] `OnEnable`/`OnDisable`에서 MessageBus Subscribe/Unsubscribe 쌍 대칭. (누락 시 씬 리로드에서 leak)
- [ ] Rumor Payload 수신 시 회색, Confirmed 수신 시 빨강 배경.
- [ ] `Update()`에서 잔여 일수 CeilToInt로 카운트다운, D0 도달 시 비활성화.
- [ ] `FactionNameKnown == false`이면 "미상의 세력" 표기.
- [ ] **미상의 세력 + Rumor 조합**에서만 텍스트 알파가 `_blinkMinAlpha`↔`1f` 사이 PingPong. Confirmed 진입 즉시 알파 1로 복구.
- [ ] Inspector에 `_blinkPeriodSec`, `_blinkMinAlpha` 노출 (사용자 튜닝 가능).
- [ ] 스크립트가 `RectTransform.anchoredPosition`·`sizeDelta`·`anchors`를 프로그램적으로 세팅하지 않음 (사용자 배치 자유).
- [ ] 컴파일 green. **씬 배치는 사용자가 수동 확인.**

**⚠️ 오해 위험**:
- **씬 배치·위치·크기는 이 커밋의 DoD가 아니다.** 스크립트는 위치 정보를 절대 건드리지 않는다. — `SampleScene.unity` 자동 수정은 다른 modified 파일 상태와 충돌 위험. (`gitStatus`상 이미 씬 modified 상태)
- 깜빡임을 코루틴으로 구현하지 말고 `Update()` 안에서 `Mathf.PingPong` — 스테이지 전환·비활성화 시 코루틴 leak 방지.
- 깜빡임은 **텍스트 알파만** 조작한다. 배경 알파(`_background.color.a`)나 GameObject 자체를 껐다 켜지 않는다. — 배경까지 깜빡이면 사용자가 UI 위치 놓친다.
- `Time.unscaledTime` 사용: 게임 일시정지 상태에서도 깜빡임이 멈추면 "고장난 UI"로 오인.
- Confirmed 스테이지 진입 시 즉시 `RestoreLabelAlpha()` 호출 — Rumor 상태의 잔여 알파 값이 남으면 임박 배너가 반투명으로 뜬다.
- `Update()`에서 GameManager.Instance null 체크 필수. 씬 시작 시 GameManager Awake 순서보다 UI가 먼저 활성화될 수 있음.
- FactionId 문자열 → 이름 매핑을 UI 안에 두는 이유: FactionAI가 이름 문자열을 payload에 넣으면 로컬라이제이션 시점에 UI와 로직이 함께 고침 필요. F-B에서는 UI가 표기 책임.

---

### FB-5 · VillagerFSM 성격별 예고 반응 대사  〔중요도: 상, 작업량: 중, 의존: FB-1〕

**대상 파일**:
- `Assets/Scripts/AI/VillagerFSM.cs` — 대사 배열 신규 + MessageBus 구독

**코드 스케치**:
```csharp
// 상수 절 추가
private static readonly string[] THOUGHT_INVASION_RUMOR_DEFAULT = { "…뭔가 이상해", "공기가 무거워졌어" };
private static readonly string[] THOUGHT_INVASION_RUMOR_COWARD  = { "뭐가… 온다…", "숨을 데 없나…" };
private static readonly string[] THOUGHT_INVASION_RUMOR_BRAVE   = { "덤빌 테면 덤벼봐라", "무기 챙겨두자" };
private static readonly string[] THOUGHT_INVASION_CONF_DEFAULT  = { "온다!", "각오해야 해" };
private static readonly string[] THOUGHT_INVASION_CONF_COWARD   = { "안 돼… 안 돼!", "도망갈래!" };
private static readonly string[] THOUGHT_INVASION_CONF_BRAVE    = { "드디어 오는군", "지지 않는다" };

// OnEnable — MessageBus 구독 (기존 EnemyDetected 등과 나란히)
if (MessageBus.Instance != null)
    MessageBus.Instance.Subscribe(MessageType.InvasionWarning, OnInvasionWarning);

// OnDisable — 대칭 Unsubscribe

private void OnInvasionWarning(AIMessage msg)
{
    var p = (MessageBus.InvasionWarningPayload)msg.Payload;
    string[] pool = SelectInvasionThoughtPool(p.StageId, _brain != null ? _brain.Personality : Personality.None);
    if (pool == null || pool.Length == 0) return;
    // 기존 THOUGHT_MIN_INTERVAL_SEC 스로틀을 반드시 통과 (F-A ADR-P5 준용)
    TryShowThoughtBubble(pool[UnityEngine.Random.Range(0, pool.Length)]);
}

private static string[] SelectInvasionThoughtPool(string stageId, Personality p)
{
    bool confirmed = (stageId == MessageBus.INVASION_STAGE_CONFIRMED);
    switch (p)
    {
        case Personality.Coward: return confirmed ? THOUGHT_INVASION_CONF_COWARD : THOUGHT_INVASION_RUMOR_COWARD;
        case Personality.Brave:  return confirmed ? THOUGHT_INVASION_CONF_BRAVE  : THOUGHT_INVASION_RUMOR_BRAVE;
        default:                 return confirmed ? THOUGHT_INVASION_CONF_DEFAULT: THOUGHT_INVASION_RUMOR_DEFAULT;
    }
}
```

**DoD 체크리스트**:
- [ ] `THOUGHT_INVASION_*` 배열 6종 존재 (Rumor/Confirmed × Default/Coward/Brave).
- [ ] `OnEnable`/`OnDisable`에 InvasionWarning 구독/해제 쌍 대칭.
- [ ] 발화가 기존 `THOUGHT_MIN_INTERVAL_SEC` 스로틀을 통과 (F-A와 채널·카운터 공유).
- [ ] 방치 검증: FactionAI DEBUG 강제 트리거 → 주민 5명 이상에서 각 스테이지 대사 최소 1회 관찰(S3).
- [ ] 컴파일 green.

**⚠️ 오해 위험**:
- **성격 6종 모두에 개별 배열을 만들지 않는다.** F-B 스코프는 Coward/Brave만 전용 대사. Diligent/Lazy/Glutton/Curious는 Default 풀 사용. — 대사 폭발 방지 (ADR-B6). 정말 필요하면 F-D 이후 안건.
- 스로틀 우회 금지. `TryShowThoughtBubble`이 기존 스로틀을 반드시 존중해야 함. Warning 발행 순간과 겁쟁이 자체 대사 발화가 겹쳐도 하나만 뜬다.

---

### FB-6 · T19 예고 스테이지 게이트 신규  〔중요도: 상, 작업량: 중, 의존: FB-2·FB-3〕

**대상 파일**:
- `Assets/Tests/EditMode/T19_InvasionWarningGates.cs` — **신규**

**코드 스케치**:
```csharp
namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// T19 (F-B ADR-B1 게이트): 예고 스테이지 순서·타이밍·취소 회귀 감지.
    /// </summary>
    public class T19_InvasionWarningGates
    {
        [Test] public void Case1_Rumor_Then_Confirmed_Then_Raid_Order_Preserved()
        {
            // FactionAI 인스턴스에 EvaluateRaidDecision 조건 강제 충족 스텁 주입
            // Day 10: EvaluateAndExecuteGoal 호출 → InvasionWarning(Rumor) 발행, ExpectedRaidDay=13
            // Day 12: 호출 → InvasionWarning(Confirmed) 발행
            // Day 13: 호출 → PublishRaidDecision + IssueRaidOrders 호출
            // Rumor 발행 시각 < Confirmed 발행 시각 < RaidDecision 발행 시각 assert.
        }

        [Test] public void Case2_Condition_Cancels_Warning_Before_Raid()
        {
            // Day 10 Rumor 발행 후, Day 11에 playerStrength가 factionStrength×1.0으로 급등해서
            // EvaluateRaidDecision가 false 반환하면 _warningStage == WSTAGE_NONE로 초기화됨을 assert.
        }

        [Test] public void Case3_Cooldown_Resets_Warning_State()
        {
            // Confirmed 스테이지 도중 유닛 전멸 → 쿨다운 진입 시 _warningStage 초기화 assert.
        }
    }
}
```

**DoD 체크리스트**:
- [ ] T19 3케이스 pass.
- [ ] T17·T18이 T19 추가 후에도 유지 pass.
- [ ] Case1이 `Assert.Less(rumorTime, confirmedTime)` + `Assert.Less(confirmedTime, raidTime)` 명시.
- [ ] Case1에서 실제 침략까지의 게임일 간격이 `WARNING_LEAD_DAYS_RUMOR` 이상임을 assert.

**⚠️ 오해 위험**:
- `FactionAI` 인스턴스를 EditMode에서 그대로 실행하기 어렵다면(코루틴·MonoBehaviour 의존), **판정 로직만 순수 함수로 분리하지 말고** — 대신 스텁 MessageBus + FactionAI 리플렉션 필드 조작으로 처리. 판정 순수 함수 분리는 스코프 밖 (ADR-P3 준용).
- 대안: `[UnityTest]`로 코루틴 회전. 이 경우 시간 배속은 `GameManager.Instance.GameTime` 필드 직접 조작.

---

### FB-7 · CLAUDE.md ADR-B1 추가 + 커밋 전 체크 ⑩  〔중요도: 상, 작업량: 소, 의존: FB-2·FB-6〕

**대상 파일**:
- `Docs/CLAUDE.md`

**추가할 내용** (핵심 문구):
```
12. **침략 예고 스테이지 순서 (ADR-B1)**: FactionAI가 EvaluateRaidDecision=true를
    받아도 즉시 IssueRaidOrders를 부르지 않는다. `_warningStage`는 반드시
    None → Rumor(D-3) → Confirmed(D-1) → Raid(D0) 순서로만 전이하며, 각 전이는
    별도 Tick에서만 발생한다(같은 Tick 이중 전이 금지). 예고 조건 소멸 및 쿨다운
    진입 시 상태 초기화. 검증: EditMode 게이트 T19.

⑩ 침략 예고 관련 커밋이면 T19가 초록인지 명시 확인, 그리고
   grep -n "IssueRaidOrders\s*(" Assets/Scripts 결과가 예고 D0 조건 없이 직접
   호출되는 경로가 없는지 확인 (ADR-B1).
```

**DoD 체크리스트**:
- [ ] CLAUDE.md에 ADR-12(ADR-B1) 절 추가.
- [ ] 커밋 전 체크 ⑩ 추가.
- [ ] 다른 ADR/체크 번호 재부여 없음.

**⚠️ 오해 위험**:
- ADR 번호는 11 다음이 12. "B1"은 명세 표기용 별칭(behavior-warning 1).

---

## 5. 커밋 순서 (권장)

FB-1 → FB-2 → FB-3 → FB-4 → FB-5 → FB-6 → FB-7

각 커밋 메시지 예:
- `feat(msg): FB-1 InvasionWarning MessageType + Payload`
- `feat(faction): FB-2 예고 스테이지 머신 (ADR-B1)`
- `feat(faction): FB-3 PublishInvasionWarning 메서드`
- `feat(ui): FB-4 InvasionWarningIndicator 배너 컴포넌트`
- `feat(ai): FB-5 성격별 예고 반응 대사 (Coward/Brave)`
- `test(faction): FB-6 T19 예고 스테이지 게이트 3케이스`
- `docs(faction): FB-7 CLAUDE.md ADR-12 + 커밋 전 체크 ⑩`

**모든 커밋은 개별로 블로그 자동화 소재가 된다.** (사용자 요청 반영 — 각 항목 완료 시 즉시 개별 커밋)

---

## 6. ADR — 미리 결정하는 판단

| ID | 결정 | 사유 | 변경 시 |
|---|---|---|---|
| **ADR-B1** | 예고 스테이지 순서 하드 강제 | 같은 Tick 이중 전이하면 UI 배너가 회색→빨강 없이 튀거나 예고 없이 침략 개시. | 사유를 커밋 메시지에 명시하고 T19 재실행 결과 인용 |
| **ADR-B2** | Lead 일수(3일/1일)는 제안치 | 재미 관측 전에 튜닝하면 관측 신호와 배율 신호가 섞임. | 최소 1회 방치 세션 후 조정 |
| **ADR-B3** | 예고를 GOAP 슬롯화하지 않음 | 플래너 코어 동결(CLAUDE.md). 예고 반응은 대사 + 확률적 감정 상태 진입뿐. | 플래너 코어 재개 논의 필요 |
| **ADR-B4** | Rumor는 정찰 미완료 시 "미상의 세력" 표기 + 텍스트 깜빡임, Confirmed는 무조건 팩션명 노출 + 알파 복구 | 정찰(F-P3)의 역할을 완전히 무력화하지 않음 — 정보 축(팩션명/타이밍)을 나누고, 미상 상태의 불확실성을 시각으로 강조. | 정찰 시스템 리팩터 시 함께 |
| **ADR-B5** | 상인 연합 TradeProposal은 예고 이전 단계 | Trade → Rumor → Confirmed → Raid의 4단계로 확장. 기존 TradeProposal 로직은 그대로. | 상인 연합 관계도 시스템 구현 시 재검토 |
| **ADR-B6** | Coward/Brave만 전용 대사, 나머지 성격은 Default 풀 | 6성격 × 2스테이지 = 12풀은 관성 실험 신호를 흐림. | F-D 이후 확장 |
| **ADR-B7** | 예고 신호는 배너 UI + 대사 2채널만 | 씬 배치·자산 없는 상태에서 하늘 색 변화·새들 도망 등은 스코프 팽창. | F-E 이후 시각 자산 확보 후 |

---

## 7. 스코프 가드 — 이번에 하지 않는 것

- 하늘 색 변화, 새들 도망, 마을 종 소리 등 환경 이펙트 (ADR-B7).
- **배너 위치 자동 배치 · 화면 상단 고정 · 자동 앵커** — 사용자가 씬에서 자유 배치.
- 예고 시점에 주민의 실제 도피·무장 행동 (F-D 이후 대사 → 행동 승격).
- 겨울 위기 예고와 UI 통일 — 이번엔 침략 전용 배너만. `WinterCrisis` UI는 별도 안건.
- 예고를 GOAP 슬롯화 (ADR-B3).
- 성격 6종 모두 전용 대사 (ADR-B6).
- 팩션별 예고 이펙트 차별화 (숲=바람 소리, 철=쇠 소리 등).
- **Lead 일수 튜닝** — 관성 실험 전 금지 (ADR-B2).
- 세이브/로드로 예고 진행 상태 유지 — 세이브 시스템 부재 상태.
- **플래너 코어 확장 · GOAP 슬롯 추가** — CLAUDE.md 금지 목록 유지.
- 씬 배치 (`SampleScene.unity`) 자동 수정 — 다른 modified 파일과 충돌 방지, 사용자 수동 배치.

---

## 8. 검증 순서 (커밋 전 체크 요약)

각 커밋 직전:
1. 컴파일 green.
2. Unity Test Runner EditMode 전체 pass (특히 FB-2 이후 T17/T18, FB-6 이후 T19).
3. `grep -rn "GainResource,\s*[0-9]\|ReduceHunger,\s*[0-9]\|ReduceFatigue,\s*[0-9]\|GainHealth,\s*[0-9]" Assets/Scripts` → 0건.
4. FB-2 이후: `grep -n "IssueRaidOrders\s*(" Assets/Scripts` 결과가 예고 D0 조건 없이 직접 호출되는 경로 없음(ADR-B1).
5. Editor 종료 시 NativeArray leak 경고 0건.
6. FB-4·FB-5는 MessageBus Subscribe/Unsubscribe 대칭 grep 확인:
   `grep -n "MessageType\.InvasionWarning" Assets/Scripts` 결과에서 Subscribe/Unsubscribe 쌍 매칭.

전체 완료 후:
7. FactionAI ContextMenu에 **DEBUG: 강제 침략 예고 트리거** 추가하여 Editor Play 재현 (선택). 없다면 GameTime 배속.
8. 10분 방치 → Console 정적 관찰 (S6).
9. 이야기 회상 테스트 (S7).

---

## 9. 파일 위치 지도

| 새 파일 | 경로 |
|---|---|
| InvasionWarningIndicator | `Assets/Scripts/UI/InvasionWarningIndicator.cs` |
| T19 게이트 | `Assets/Tests/EditMode/T19_InvasionWarningGates.cs` |

| 수정 파일 | 경로 |
|---|---|
| MessageType enum | `Assets/Scripts/AI/VillagerEnums.cs` |
| InvasionWarningPayload | `Assets/Scripts/Core/MessageBus.cs` |
| 예고 상태 머신 | `Assets/Scripts/Core/FactionAI.cs` |
| 성격별 대사 | `Assets/Scripts/AI/VillagerFSM.cs` |
| ADR-B1 + 체크 ⑩ | `Docs/CLAUDE.md` |

---

## 10. 다음 스텝

이 명세서를 사용자가 승인하면:
1. `spec-implement` 스킬로 진입 → FB-1부터 순차 커밋.
2. FB-2 완료 후 반드시 T17·T18 재실행 → 커밋 메시지에 인용.
3. FB-4·FB-5까지 완주 후 씬 배치 안내 (Canvas 슬롯 위치).
4. FB-6·FB-7 완료 후 10분 방치 → 이야기 회상 테스트.
5. **각 커밋은 즉시 push하지 않아도 되지만 로컬 커밋은 항목마다 즉시 남긴다** (사용자 요청: 블로그 자동화 소재).

**사용자 확인 완료 (2026-07-11)**:
- ✅ Lead 일수 제안치(Rumor 3일 / Confirmed 1일) 승인.
- ✅ 배너 위치는 자동 고정하지 않고 사용자가 씬에서 자유 조절 (스크립트 Transform 조작 금지).
- ✅ Rumor 정찰 미완 시 "미상의 세력" 표기 승인 (ADR-B4).
- ✅ **추가**: "미상의 세력" 표기일 때 텍스트 깜빡임 효과 (Confirmed 진입 시 즉시 복구, 배경은 깜빡이지 않음). FB-4 DoD에 반영됨.
