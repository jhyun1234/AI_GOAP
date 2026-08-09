using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 화면 좌상단 달력·계절 경보·알림 HUD (M6-C — 첫 screen-space UI).
    /// 표시 전용: 시뮬레이션 상태를 읽기만 하고 아무것도 쓰지 않는다.
    /// BuildingVisualizer 패턴 — 씬 배선 없이 SimulationLoop가 코드로 생성·틱한다
    /// (사용자 Editor 배치 불필요 — 명세 스케치의 MonoBehaviour 싱글턴에서 변경, 커밋 사유 참조).
    /// </summary>
    public sealed class SeasonHud
    {
        private const float NOTIFY_SEC = 6f; // 알림 노출 시간 (연출 상수)

        /// <summary>식량 경보 문턱 (일치, 이하) — 상태 알림(M13-B)의 굶는 주민 판정 기준.
        /// SimulationLoop의 열거 필터가 같은 값을 읽는다 (public — 판정 기준의 단일 출처).
        /// 舊 달력 접미사(FoodSuffix)는 2026-07-30 개정으로 삭제 — 아래 Compose 주석 참조. 연출 상수.</summary>
        public const int FOOD_ALERT_DAYS = 2;

        private readonly TMP_Text _calendar;
        private readonly TMP_Text _resources; // 자원 줄 (M22-2차 W4) — 목록·라벨은 WorldConfigSO.HudResources
        private readonly TMP_Text _notice;
        private readonly TMP_Text _selectedInfo;
        private readonly TMP_Text _prompt; // 결정 프롬프트 (M10-E) — 방랑자 Y/N 등 상시 유지 줄
        private readonly TMP_Text _status; // 상태 알림 (M13-B) — 해소될 때까지 유지 (_prompt 패턴의 일반화)
        private readonly TMP_Text _modeInfo; // 모드 정보줄 (M22-W3R3) — 입력 모드 상시 안내 (프롬프트와 분리)
        private float _noticeUntil;
        private string _lastCalendar;
        private string _lastStatus;
        private VillagerAgent _selected;
        private string _lastSelectedLine;

        // 관계·소유·부탁 표기 (M8-B/C/후속) — 읽기 전용 참조. null이면 미표기 (중립 — M7 표시와 동일)
        private readonly RelationshipService _relationship;
        private readonly WorldConfigSO _worldCfg;
        private readonly OwnershipService _ownership;
        private readonly RequestService _requests;
        private readonly HomeStorageService _homeStorage; // 집 저장 표기 (M11-A)
        private readonly ChronicleService _chronicle;     // 최근 사건 표기 (M13-C2)

        public SeasonHud(Transform parent, TMP_FontAsset font,
                         RelationshipService relationship = null, WorldConfigSO worldCfg = null,
                         OwnershipService ownership = null, RequestService requests = null,
                         HomeStorageService homeStorage = null, ChronicleService chronicle = null)
        {
            _relationship = relationship;
            _worldCfg = worldCfg;
            _ownership = ownership;
            _requests = requests;
            _homeStorage = homeStorage;
            _chronicle = chronicle;
            var root = new GameObject("SeasonHud");
            root.transform.SetParent(parent, false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // 월드 위 최상단

            // 블록 위치는 전부 Reflow가 정한다 (2026-07-31 수직 스택 개정) — MakeText 오프셋은
            // 첫 프레임 임시값일 뿐이다. 아래 주석의 舊 고정 오프셋(-10/-48/-86/-162/-200)은 폐기:
            // 정보줄이 길어지면 상태줄을 덮었다 (사용자 Play 피드백 — 스크린샷 관측).
            _calendar = MakeText(root.transform, "Calendar", font, new Vector2(12f, -10f), 30f);
            // 자원 줄 (M22-2차 W4, 사용자 Play 피드백 "보유 자원이 안 보인다") — 달력 바로 아래.
            // 무엇을 몇 줄 보여줄지는 에셋(WorldConfigSO.HudResources)이 정한다 — 새 자원 = 행 추가.
            _resources = MakeText(root.transform, "Resources", font, new Vector2(12f, -48f), 24f);
            _resources.color = new Color(0.95f, 0.88f, 0.62f); // 옅은 볏짚색 — 달력·알림과 층 구분
            _resources.text = "";
            _notice   = MakeText(root.transform, "Notice",   font, new Vector2(12f, -48f), 24f);
            _notice.text = "";
            // 정보줄 — M13-D부터 2줄 (1줄 = 신상, 2줄 = 이유·문턱). 높이는 Reflow가 실측한다.
            _selectedInfo = MakeText(root.transform, "SelectedInfo", font, new Vector2(12f, -86f), 24f);
            _selectedInfo.text = "";
            // 결정 프롬프트 (M10-E) — 알림과 달리 상시 유지 (해소될 때까지). 노랑 강조.
            _prompt = MakeText(root.transform, "Prompt", font, new Vector2(12f, -162f), 26f);
            _prompt.color = new Color(1f, 0.85f, 0.4f);
            _prompt.text = "";
            // 상태 알림 (M13-B) — 순간 사건(Notify)과 달리 조건이 해소될 때까지 남는다
            // ("식량 부족"은 지나가는 소식이 아니라 지금 손쓸 수 있는 상태다 — 예고 휘발성 교훈의 일반화).
            // 폰트는 에셋 값 (WorldConfigSO.HudStatusFontSize — "작다" Play 피드백으로 승격).
            float statusSize = worldCfg != null && worldCfg.HudStatusFontSize > 0f
                ? worldCfg.HudStatusFontSize : 24f;
            _status = MakeText(root.transform, "Status", font, new Vector2(12f, -200f), statusSize);
            _status.text = "";
            // 모드 정보줄 (M22-W3R3) — 울타리 그리기 등 입력 모드의 상시 안내. 프롬프트(적습·방랑자
            // 소유)와 분리 — 한 줄을 나눠 쓰면 모드 종료가 적습 프롬프트를 지우는 사고가 난다.
            _modeInfo = MakeText(root.transform, "ModeInfo", font, new Vector2(12f, -238f), 24f);
            _modeInfo.color = new Color(0.6f, 0.95f, 0.6f);
            _modeInfo.text = "";

            // 수직 스택 순서 (위 → 아래) — 달력 → 알림 → 정보줄 → 프롬프트 → 상태 → 모드. 순서 불변이
            // 클릭 매핑의 전제는 아니지만(픽킹은 실제 렌더 좌표 기준), 시선 습관의 전제다.
            _stack = new[] { _calendar, _resources, _notice, _selectedInfo, _prompt, _status, _modeInfo };
        }

        // ── 수직 스택 리플로우 (M14 후속 2026-07-31 — 겹침 해소) ─────────────────
        // 비지 않은 블록만 위에서부터 실측 높이로 쌓는다: 블록이 길어지면 아래가 밀려 내려가고,
        // 비면(주민 선택 해제·알림 소멸) 아래가 다시 올라온다. 표시 전용 규약 유지 (상태 쓰기 없음).
        // 픽킹(PickStatusLine 등)은 TMP가 실제 렌더 좌표로 판독하므로 위치 이동과 무관하게 성립.

        private const float STACK_X = 12f;    // 좌측 여백 (舊 고정 오프셋의 X 계승)
        private const float STACK_TOP = -10f; // 첫 블록 Y (舊 달력 위치 계승)
        private const float STACK_GAP = 8f;   // 블록 간 간격 (연출 상수)
        private readonly TMP_Text[] _stack;

        private void Reflow()
        {
            float y = STACK_TOP;
            for (int i = 0; i < _stack.Length; i++)
            {
                TMP_Text t = _stack[i];
                if (string.IsNullOrEmpty(t.text)) continue; // 빈 블록 = 접힘 — 아래가 올라온다
                RectTransform rt = t.rectTransform;
                // 폭 고정(줄바꿈 유지) 상태의 선호 높이 실측 — wrap된 줄 수만큼 자리를 차지한다
                float h = t.GetPreferredValues(t.text, rt.sizeDelta.x, 0f).y;
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);
                rt.anchoredPosition = new Vector2(STACK_X, y);
                y -= h + STACK_GAP;
            }
        }


        private static TMP_Text MakeText(Transform parent, string name, TMP_FontAsset font,
                                         Vector2 offset, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font; // 말풍선 폰트 공유 — 비면 TMP 기본 (한글 미표시 위험, W6 동일)
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false; // 상호작용 없음 — EventSystem 불필요
            RectTransform rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); // 좌상단 고정
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(760f, 40f);
            return text;
        }

        /// <summary>SimulationLoop 틱마다 호출 — 문자열은 값이 바뀔 때만 재조립 (GC 절약).
        /// 舊 foodDaysLeft 인자(M9-I·M11-D 마을 최솟값)는 2026-07-30 개정으로 삭제 —
        /// 식량 표기는 상태 알림 줄(M13-B)의 개인 열거가 전담한다.</summary>
        public void Tick(float gameTime, SeasonService season, float forecastDays, int pressure = -1)
        {
            // (M19-W4: 물가·금고·세율·발행 인자 9종은 화폐와 함께 철거 — 달력 줄은 계절만)
            // pressure는 M24-1차 W3 — 미배선(-1)이면 문구가 안 바뀐다 (중립).
            string line = Compose(gameTime, season, forecastDays, pressure);
            if (line != _lastCalendar)
            {
                _lastCalendar = line;
                _calendar.text = line;
            }

            if (_noticeUntil > 0f && Time.time >= _noticeUntil)
            {
                _noticeUntil = 0f;
                _notice.text = "";
            }

            TickSelected();
            // 리플로우는 틱 끝 1회 — 이 틱에서 바뀐 모든 블록(달력·알림·정보줄)을 한 번에 쌓는다.
            // 프롬프트·상태줄 변경은 다음 틱(0.1초)에 따라온다 — 지각 불가 지연 (연출 허용치).
            Reflow();
        }

        /// <summary>주민 선택/해제 (M7-A) — PlayerInputController가 호출 (null = 해제).
        /// 선택 변경은 무덤 조사도 함께 덮는다 (빈 땅 클릭 = 정보줄 완전 소거).</summary>
        public void SetSelected(VillagerAgent agent)
        {
            _selected = agent;
            _graveLine = null;
            TickSelected(); // 다음 틱을 기다리지 않고 즉시 반영 (선택 반응성)
            Reflow();       // 선택/해제는 정보줄 높이가 크게 출렁이는 지점 — 즉시 재쌓기
        }

        private string _graveLine; // 무덤 조사 (M13) — 산 주민 선택이 없을 때만 정보줄에 표시

        /// <summary>무덤 조사 표시 (M13) — 죽은 주민의 기록을 정보줄에. 산 주민 선택과
        /// 같은 자리를 쓴다 (죽은 자의 정보줄 = 산 자의 정보줄과 같은 창구).</summary>
        public void SetGraveInfo(string line)
        {
            _graveLine = line;
            TickSelected();
        }

        /// <summary>
        /// 선택 정보줄 폴링 (M7-A) — 파괴(이탈)된 주민은 자동 해제. 문자열은 값이 바뀔 때만
        /// 재할당 (달력과 동일 패턴). 표기는 플레이어 언어만 — 내부값(EffectivePriority 등) 노출 금지.
        /// </summary>
        private void TickSelected()
        {
            if (_selected == null) // Unity 파괴 비교 포함 — 이탈한 주민 자동 소거
            {
                _selected = null;
                string idle = _graveLine ?? ""; // 무덤 조사 중이면 그 기록 유지 (M13)
                if (_lastSelectedLine != idle)
                {
                    _lastSelectedLine = idle;
                    _selectedInfo.text = idle;
                }
                return;
            }

            string line = ComposeSelected(_selected, _relationship, _worldCfg, _ownership, _requests,
                                          _homeStorage, _chronicle);
            if (line != _lastSelectedLine)
            {
                _lastSelectedLine = line;
                _selectedInfo.text = line;
            }
        }

        /// <summary>
        /// 정보줄 문구 조립 — 성격·직업을 구분 표기해 이름 혼동을 해소 (ADR-M7-5의 짝).
        /// 예: "A — 성격 고집쟁이 · 직업 농부 · 포만 45 · 피로 60 · 지금: 겨울 비축 · 원한 C"
        /// 관계(M8-B)는 극단 1명씩만 — 정보줄은 한 줄이다 (전체 목록 UI 금지, 명세 §7).
        /// 문턱 미만/이상이 없으면 표기 없음 (M7 표시와 동일 — 중립).
        /// </summary>
        public static string ComposeSelected(VillagerAgent a, RelationshipService rel = null,
                                             WorldConfigSO cfg = null, OwnershipService own = null,
                                             RequestService requests = null,
                                             HomeStorageService homeStorage = null,
                                             ChronicleService chronicle = null)
        {
            string line =
                $"{a.ShortName} — 성격 {(a.Personality != null ? a.Personality.DisplayName : "없음")}" +
                $" · 직업 {(a.Job != null ? a.Job.DisplayName : "무직")}" +
                $" · 포만 {Mathf.RoundToInt(a.Satiety)} · 피로 {Mathf.RoundToInt(a.Fatigue)}" +
                // 체력 (M21-W8 — 헌장 표현 조항: 판정이 읽는 값은 화면에도 자리가 있어야 한다)
                $" · 체력 {Mathf.RoundToInt(a.Hp)}/{Mathf.RoundToInt(a.AgentConfig.MaxHp)}" +
                // 소지 식량 표기 (M11-A 관측 — 보상 차감·저장 이동을 콘솔 없이 화면에서 확인)
                $" · 소지 생{a.MyRaw}·조{a.MyCooked}" + // (M19-W4: 지갑 표기 철거)
                // 부상 표기 (M10-A) — 붉은 강조. None이면 표기 없음 (중립 — M9 표시와 동일)
                (a.Injury != InjurySeverity.None ? " · <color=#FF6B6B>부상</color>" : "");
                // "지금: goal"은 M13-D에서 2번째 줄(ComposeReason)로 이사 — 사슬과 한 몸이 됐다

            // 수락한 부탁 표기 (M8 후속) — "부탁: A의 집 지어주기". 진행 중(수락~완수)에만
            if (requests != null && requests.TryGetAssignment(a.AgentId, out string reqId, out string task))
                line += $" · <color=#7EC8FF>부탁: {ToShortName(reqId)}의 {task}</color>";

            // 집 표기 (M8-C) — 표시 정책: 집(HouseCount)만. 다른 소유 슬롯이 생기면 여기 확장
            // 집 저장 식량 (M11-A) — 서비스 미배선이면 좌표만 (중립)
            if (own != null)
            {
                if (own.TryGetOwned(a.AgentId, SlotId.HouseCount, out Vector2Int home))
                {
                    line += $" · 집 ({home.x},{home.y})";
                    if (homeStorage != null)
                    {
                        (int raw, int cooked) = homeStorage.Get(home);
                        line += $" 저장 생{raw}·조{cooked}";
                    }
                }
                else line += " · 집 없음";
            }

            if (rel != null && cfg != null)
            {
                if (rel.TryGetExtreme(a.AgentId, buddy: true, cfg.BuddyThreshold, out string buddy))
                    line += $" · 단짝 {ToShortName(buddy)}";
                if (rel.TryGetExtreme(a.AgentId, buddy: false, cfg.GrudgeThreshold, out string grudge))
                    line += $" · <color=#FF8A65>원한 {ToShortName(grudge)}</color>";
            }

            // 최근 사건 (M13-C2) — 사건 0건이면 표기 없음 (중립). 정보줄은 한 줄 규율이라
            // 명세 초안의 "3줄"을 "한 줄에 최근 3건"으로 바꿨다 (M8-B 관계 극단 1명 전례와 동일 규율).
            if (chronicle != null && chronicle.TryGetRecord(a.AgentId, out VillagerRecord rec))
            {
                string recent = ComposeRecentEvents(rec, RECENT_EVENTS_MAX);
                if (recent.Length > 0) line += $" · <color=#B8B8B8>{recent}</color>";
            }

            // 2번째 줄 = 이유·문턱 (M13-D) — "왜 지금 저걸 하는가"(계획 사슬)와
            // "언제까지 손쓸 수 있는가"(명령 여유·식량)를 붙인다. 우리 시그니처 —
            // RimWorld는 계획이 없어(깊이 1) 이 줄을 만들 수 없다.
            line += "\n" + ComposeReason(a.CurrentGoal, a.CurrentPlan, a.CurrentPlanIndex,
                                         a.Satiety, a.Fatigue, a.Injury, a.AgentConfig,
                                         a.Personality, a.MyTraits, a.EstimateMyFoodDays(),
                                         a.CurrentActionDurationMult);
            return line;
        }

        /// <summary>
        /// 이유·문턱 줄 (M13-D, 순수 — 게이트 M13-T5). 세 부분:
        /// ①계획 사슬 — "지금: 겨울 비축 — 채집 → [운반] → 조리" (현재 칸 노랑, 문구는 전부
        ///   에셋 DisplayName — ADR-M0-1). 계획 없으면 goal명만, goal도 없으면 "쉬는 중" (중립).
        /// ②명령 여유 — 실효 거부 문턱(JudgeOrder와 같은 산식 — RefuseSatiety/FatigueLimit)까지
        ///   남은 양. 문턱을 넘었으면 거부 **예측**을 그대로 적는다 — 판정이 결정적(ADR-M1-2)이라
        ///   예측이 정확하고, 그래서 학습·협상이 성립한다. 값이 아니라 여유를 보여준다 (분석 §6 ②).
        /// ③식량 일수 — 99(중립)면 생략, 문턱은 상태줄과 같은 FOOD_ALERT_DAYS.
        /// 내부값(EffectivePriority 등) 노출 금지 — 표기는 플레이어 언어만 (기존 규율).
        /// </summary>
        /// <summary>호환 진입점 (게이트·구형 호출 전용 — 벡터 = 성격 원본, 편차 없음).
        /// 런타임(ComposeInfoLine)은 traits 인자판(MyTraits)을 쓴다 (⚠️W3-⑤).</summary>
        public static string ComposeReason(GoalSO goal, IReadOnlyList<ActionSO> plan, int planIndex,
                                           float satiety, float fatigue, InjurySeverity injury,
                                           AgentConfigSO cfg, PersonalitySO p, int foodDays)
            => ComposeReason(goal, plan, planIndex, satiety, fatigue, injury, cfg, p,
                             p != null ? p.Traits : null, foodDays);

        public static string ComposeReason(GoalSO goal, IReadOnlyList<ActionSO> plan, int planIndex,
                                           float satiety, float fatigue, InjurySeverity injury,
                                           AgentConfigSO cfg, PersonalitySO p, TraitValue[] traits,
                                           int foodDays, float durationMult = 1f)
        {
            var sb = new System.Text.StringBuilder(96);
            sb.Append(goal != null ? $"지금: {goal.DisplayName}" : "지금: 쉬는 중");

            if (goal != null && plan != null && plan.Count > 0)
            {
                sb.Append(" — ");
                for (int i = 0; i < plan.Count; i++)
                {
                    if (i > 0) sb.Append(" → ");
                    if (i == planIndex) sb.Append($"<color=#FFD966>{plan[i].DisplayName}</color>");
                    else sb.Append(plan[i].DisplayName);
                }
            }

            // 솜씨 (M20-W10) — 직업 효율은 실재하는데 **화면에 없어서** 판별이 불가능했다
            // (2026-08-05 Play: "목수 있는 판과 없는 판의 완공 속도차를 판별하기 힘들다").
            // 판을 나눠 비교할 수는 없으니, 그 자리에서 읽히게 한다. 배율이 아니라 배수로 —
            // 내부값 노출 금지 규율(표기는 플레이어 언어만)을 지킨다.
            string skill = ComposeSkill(durationMult);
            if (skill != null) sb.Append($" · <color=#8FD694>{skill}</color>");

            if (cfg != null)
            {
                if (injury != InjurySeverity.None)
                    sb.Append(" · <color=#FF6B6B>명령 불가 — 부상</color>"); // TryGiveOrder 조기 거절과 동일
                else
                {
                    // 문턱은 개체 편차 포함 벡터로 (M14-W3) — 화면의 여유와 실제 거부가 어긋나면 안 된다
                    float satMargin = satiety - VillagerAgent.RefuseSatietyLimit(cfg, p, traits, null);
                    float fatMargin = VillagerAgent.RefuseFatigueLimit(cfg, p, traits, null) - fatigue;
                    if (satMargin <= 0f)      // 판정 순서도 JudgeOrder와 동일 (배고픔 먼저)
                        sb.Append(" · <color=#FF6B6B>명령 거부될 것 — 배고픔</color>");
                    else if (fatMargin <= 0f)
                        sb.Append(" · <color=#FF6B6B>명령 거부될 것 — 피로</color>");
                    else
                        sb.Append($" · 명령 여유 포만 {satMargin:0}·피로 {fatMargin:0}");
                }
            }

            if (foodDays < WorldModel.NO_ESTIMATE)
                sb.Append(foodDays <= FOOD_ALERT_DAYS
                    ? $" · <color=#FF6B6B>식량 {foodDays}일치</color>"
                    : $" · 식량 {foodDays}일치");
            return sb.ToString();
        }

        /// <summary>
        /// 솜씨 표기 (M20-W10, 순수 — 게이트 M20-T7). 중립(1)이면 null = 아무것도 안 붙인다:
        /// 대다수 주민에게 늘 붙는 라벨은 정보가 아니라 소음이다.
        /// 배수는 소요 시간의 역수 — 0.5배 시간 = "두 배 빨리". 정수배는 한글로 읽고
        /// (두 배·세 배) 나머지는 소수 한 자리로. 느린 쪽(>1)도 표기한다 — 지금은 그런 직업이
        /// 없지만(중립 불변식), 생기면 화면이 먼저 알려야 한다.
        /// </summary>
        public static string ComposeSkill(float durationMult)
        {
            if (durationMult <= 0f || Mathf.Approximately(durationMult, 1f)) return null;

            if (durationMult < 1f)
            {
                float times = 1f / durationMult;
                return $"솜씨 {Times(times)} 빨리";
            }
            return $"솜씨 {Times(durationMult)} 느리게";
        }

        private static string Times(float t)
        {
            if (Mathf.Approximately(t, 2f)) return "두 배";
            if (Mathf.Approximately(t, 3f)) return "세 배";
            if (Mathf.Approximately(t, 4f)) return "네 배";
            return $"{t:0.#}배";
        }

        /// <summary>정보줄 최근 사건 표시 수 (제안치 3 — 명세 §12-1, 유일하게 발명한 수치). 연출 상수.</summary>
        private const int RECENT_EVENTS_MAX = 3;

        /// <summary>동일 사건 판정 (압축 단위) — 종류+부가값+대상이 같아야 한 묶음.
        /// 대상 비교가 없으면 "밭 완공 ×11"이 "집 완공"을 삼킨다.</summary>
        private static bool SameEvent(in ChronicleEvent a, in ChronicleEvent b)
            => a.Kind == b.Kind && a.Value == b.Value && a.OtherId == b.OtherId;

        /// <summary>사건 묶음 — 같은 사건의 전체 반복 (발생 순서 무관, 2026-07-30 개정).
        /// 舊 "연속" 압축은 사이에 다른 사건이 끼면 묶음이 끊겨 "밭 완공 · 모닥불 완공 ·
        /// 밭 완공 ×2"처럼 같은 날 같은 일이 나뉘었다 (Play 피드백). 연대기는 감사 로그가
        /// 아니라 전기(傳記)다 — 세밀한 순서보다 "무엇을 몇 번 했는가"가 읽기의 단위.</summary>
        private struct EventGroup
        {
            public ChronicleEvent Rep; // 대표 (첫 발생)
            public int N;
            public int FirstDay;
            public int LastDay;
        }

        /// <summary>사건을 종류별 묶음으로 집계 — 목록 순서 = 첫 발생 순 (결정적).</summary>
        private static List<EventGroup> GroupEvents(List<ChronicleEvent> events)
        {
            var groups = new List<EventGroup>(8);
            foreach (ChronicleEvent e in events)
            {
                int day = (int)e.Day;
                int found = -1;
                for (int i = 0; i < groups.Count; i++)
                    if (SameEvent(groups[i].Rep, e)) { found = i; break; }
                if (found >= 0)
                {
                    EventGroup g = groups[found];
                    g.N++;
                    g.LastDay = day;
                    groups[found] = g;
                }
                else groups.Add(new EventGroup { Rep = e, N = 1, FirstDay = day, LastDay = day });
            }
            return groups;
        }

        /// <summary>묶음의 연대기 문구 — 하루면 "D1 밭 완공 ×3", 여러 날이면 "D5~34 명령 거부(피로) ×30".</summary>
        private static string KrGroup(in EventGroup g)
        {
            string days = g.FirstDay == g.LastDay ? $"D{g.FirstDay}" : $"D{g.FirstDay}~{g.LastDay}";
            return g.N > 1 ? $"{days} {KrEvent(g.Rep)} ×{g.N}" : $"{days} {KrEvent(g.Rep)}";
        }

        /// <summary>최근 사건 요약 (M13-C2, 순수 — 게이트 M13-T4). 마지막 발생일이 늦은
        /// 묶음부터 max개, "치료받음 D9 · 명령 거부(피로) ×3 D7". 사건 0건 = 빈 문자열 (중립).</summary>
        public static string ComposeRecentEvents(VillagerRecord r, int max)
        {
            if (r == null || r.Events.Count == 0 || max <= 0) return "";
            List<EventGroup> groups = GroupEvents(r.Events);
            // 최신순 — 마지막 발생일 내림차순, 동률은 나중에 시작된 묶음 먼저 (결정적)
            groups.Sort((a, b) => a.LastDay != b.LastDay
                ? b.LastDay.CompareTo(a.LastDay)
                : b.FirstDay.CompareTo(a.FirstDay));

            var sb = new System.Text.StringBuilder(48);
            sb.Append("최근: ");
            for (int i = 0; i < groups.Count && i < max; i++)
            {
                if (i > 0) sb.Append(" · ");
                EventGroup g = groups[i];
                sb.Append(KrEvent(g.Rep));
                if (g.N > 1) sb.Append($" ×{g.N}");
                sb.Append($" D{g.LastDay}");
            }
            return sb.ToString();
        }

        /// <summary>개인 연대기 (M13-C2, 순수 — 무덤 조사·회고 드릴다운용). 첫 발생 순 묶음,
        /// "D1 집 마련 · D1 밭 완공 ×3 · D5~34 명령 거부(피로) ×30".
        /// 사건 0건 = 빈 문자열 (그 줄 자체가 없다).</summary>
        public static string ComposeLifeEvents(VillagerRecord r)
        {
            if (r == null || r.Events.Count == 0) return "";
            List<EventGroup> groups = GroupEvents(r.Events);
            var sb = new System.Text.StringBuilder(64);
            for (int i = 0; i < groups.Count; i++)
            {
                if (i > 0) sb.Append(" · ");
                sb.Append(KrGroup(groups[i]));
            }
            return sb.ToString();
        }

        /// <summary>무덤·명부 조사 문구 (M13, 순수) — 죽은 주민의 정보줄. 산 주민
        /// ComposeSelected의 짝. 기록·관계 없으면 생애 구간만 (중립 — 표기 없음).</summary>
        public static string ComposeGraveInfo(VillagerRecord r)
        {
            if (r == null) return "";
            string line = $"† {r.ShortName} — {r.PersonalityName}, {r.JobName}. {KrLifeSpan(r)}";
            // 퇴장 관계 (M13-C3) — 죽음이 지우지 못한 것. 회상 테스트가 찾던 종류의 줄
            // ("영희의 단짝이었다"). 산 주민 정보줄과 같은 문턱 판정의 스냅샷이다.
            if (!string.IsNullOrEmpty(r.BuddyIdAtExit))
                line += $" · {ToShortName(r.BuddyIdAtExit)}의 단짝이었다";
            if (!string.IsNullOrEmpty(r.GrudgeIdAtExit))
                line += $" · <color=#FF8A65>{ToShortName(r.GrudgeIdAtExit)}에게 원한이 있었다</color>";
            string life = ComposeLifeEvents(r);
            return life.Length > 0 ? $"{line} · <color=#B8B8B8>{life}</color>" : line;
        }

        /// <summary>사건의 플레이어 언어 (M13-C2) — KrCause와 같은 자리 (표시 정책 단일 지점).</summary>
        public static string KrEvent(ChronicleEvent e)
        {
            switch (e.Kind)
            {
                case EventId.Injured:      return "다침";
                case EventId.Healed:       return "치료받음";
                case EventId.GotHome:      return "집 마련";
                case EventId.OrderTaken:   return "명령 수락";
                case EventId.OrderRefused: return "명령 거부" + KrRefuse(e.Value);
                // 건물명은 기록 시점의 에셋 DisplayName (OtherId) — "완공"만으로는 집인지
                // 밭인지 모른다 (2026-07-30 Play 피드백). 舊 기록(대상 없음)은 "완공" 그대로.
                case EventId.Built:
                    return string.IsNullOrEmpty(e.OtherId) ? "완공" : $"{e.OtherId} 완공";
                // 거래 (M16-W5) — 생존 서사의 핵심 사건: "굶다가 사 먹고 살았다"가 연대기에 남는다
                case EventId.Traded:
                    return $"식량 구입({ComposeMoney(e.Value)})";
                // 집값 지불 (M18-W5 → M19 휴면) — 새 기록은 없다. 옛 판 연대기 표시 호환용
                case EventId.HomePaid:
                    return $"집값 지불({ComposeMoney(e.Value)})";
                // 식량 나눔 (M19-W3) — 생존 서사의 계승: "굶다가 얻어먹고 살았다"
                case EventId.FoodShared:
                    return $"식량 얻어먹음({e.Value}개)";
                // 격퇴 (M21-W9) — ADR-M21-1 검증 문장의 재료: "침입 무리 3마리 격퇴"
                case EventId.Repelled:
                    return string.IsNullOrEmpty(e.OtherId)
                        ? "적습 격퇴"
                        : e.Value > 1 ? $"{e.OtherId} {e.Value}마리 격퇴" : $"{e.OtherId} 격퇴";
                // 사냥 (M21-W9) — 잡은 본인의 공적. Value = 드랍 고기
                case EventId.Hunted:
                    return (string.IsNullOrEmpty(e.OtherId) ? "사냥" : $"{e.OtherId} 사냥")
                         + (e.Value > 0 ? $"(고기 {e.Value})" : "");
                default:                   return e.Kind.ToString(); // 미등록 신규 — 이름 그대로 (침묵 금지)
            }
        }

        private static string KrRefuse(int v)
        {
            switch (v)
            {
                case ChronicleEvent.REFUSE_HUNGRY:  return "(배고픔)";
                case ChronicleEvent.REFUSE_TIRED:   return "(피로)";
                case ChronicleEvent.REFUSE_INJURED: return "(부상)";
                default:                            return "";
            }
        }

        /// <summary>AgentId → 표시명 ("M0_Villager_A" → "A") — VillagerAgent.ShortName과 동일 규칙.
        /// public (M15-W2) — 아카이브 스냅샷(SimulationLoop)이 같은 규칙을 쓴다 (표시 정책 단일 지점).</summary>
        public static string ToShortName(string agentId)
        {
            int sep = agentId.LastIndexOf('_');
            return sep >= 0 && sep < agentId.Length - 1 ? agentId.Substring(sep + 1) : agentId;
        }

        /// <summary>
        /// 달력 문구 조립 — 표시 정책의 단일 지점, 순수 함수 (EditMode 게이트 대상).
        /// 평시 "Day N · 계절" / 예고 "…겨울까지 N일"(주황) / 위기 "…겨울 (남은 N일)"(하늘).
        /// 舊 "식량 최소 N일치" 접미사(M9-I·M11-D)는 2026-07-30 사용자 Play 피드백으로 삭제 —
        /// 마을 최솟값 요약이 상태 알림 줄(M13-B)의 개인 열거와 겹쳐 "마을 전체가 N일치"로
        /// 오독됐다. 식량 표기는 개인 열거 한 곳만 남긴다 (같은 정보 두 형태 = 오해 소지).
        /// </summary>
        /// <param name="pressure">전역압력 (M24-1차 W3). **음수면 표기 없음** — 기본값이 -1이라
        /// 기존 호출자·게이트는 문구가 한 글자도 안 바뀐다 (중립 불변식).
        /// 상시 노출하는 이유는 연차 병기와 같다: 예산·스탯·전략 해금이 전부 이 숫자를 읽는데
        /// 화면에 없으면 플레이어는 무엇이 세지고 있는지 모른 채 세진다.</param>
        public static string Compose(float gameTime, SeasonService season, float forecastDays,
                                     int pressure = -1)
        {
            // (M19-W4: 재정 접미사 — 금고·세율·물가·예보·발행 — 는 화폐와 함께 철거)
            int day = (int)gameTime;
            string pres = pressure >= 0 ? $" · 압력 {pressure}" : "";
            if (season == null || season.Current == null) return $"Day {day}{pres}";

            // 연차 병기 (M14-W4) — "N번째 겨울까지"라는 경주의 자를 상시 노출 (ADR-M13-1의 정신:
            // 기록 카운터가 전멸 화면에만 있으면 살아 있는 동안 아무도 못 본다).
            SeasonSO cur = season.Current;
            string yearSeason = $"{season.Year}년째 {cur.DisplayName}";
            if (cur.IsCrisis)
                return $"Day {day} · <color=#7EC8FF>{yearSeason}</color> " +
                       $"(남은 {Mathf.CeilToInt(season.DaysLeftInSeason)}일){pres}";
            if (season.NextCrisis != null && season.DaysToCrisis <= forecastDays)
                return $"Day {day} · {yearSeason} · <color=#FF8A65>" +
                       $"{season.NextCrisis.DisplayName}까지 {Mathf.CeilToInt(season.DaysToCrisis)}일</color>{pres}";
            return $"Day {day} · {yearSeason}{pres}";
        }

        /// <summary>화폐 표시 (M16-W4, M17-W5 진법 개편, 순수 — 게이트 M16-T8.
        /// ADR-M16-6: 표시 변환은 이 함수뿐).
        /// 동 정수 → "N금 N은 N동" (**1은 = 100동, 1금 = 100은 = 10,000동** — 표시 계층일 뿐
        /// 실물 아님). 舊 진법은 1은 = 10동이었다; 현실 화폐감에 맞춰 100진법으로 바꿨다.
        /// 지금 규모(임금 5~6동·집 50동)에서는 은이 거의 안 나오는데 그게 의도다 —
        /// **은이 보이기 시작하는 것 자체가 "마을이 커졌다"의 신호**다.
        /// ⚠️ 이 함수와 게이트 M16-T8은 짝이다 (방법론 M17). 한쪽만 고치지 않는다.
        /// 0인 단위는 생략, 전부 0이면 "0동". 지갑·대사·결산·경보가 전부 여기를 지난다.</summary>
        public static string ComposeMoney(int coins)
        {
            if (coins <= 0) return "0동";
            int gold = coins / 10000, silver = coins % 10000 / 100, copper = coins % 100;
            var sb = new System.Text.StringBuilder(12);
            if (gold > 0) sb.Append(gold).Append('금');
            if (silver > 0) { if (sb.Length > 0) sb.Append(' '); sb.Append(silver).Append('은'); }
            if (copper > 0) { if (sb.Length > 0) sb.Append(' '); sb.Append(copper).Append('동'); }
            return sb.ToString();
        }

        /// <summary>
        /// 전멸 종료 요약 문구 (순수 — 게이트 M10-T6). 마을의 마지막 날 기록 —
        /// 사망·이탈·정착이 각자 집계된다 (결말 이원화는 기록에서도 유지, ADR-M10-3).
        /// </summary>
        /// 이탈 0이면 항목을 감춘다 — 굶주림이 아사로 바뀐 뒤(ADR-M10-3 개정) 이탈은 휴면이라
        /// "이탈 0"이 매번 뜨면 잡음이다. 다른 이탈 사유가 생기면 자동으로 다시 표시된다.
        public static string ComposeGameOver(int day, int deaths, int departs, int settles)
            => $"마을의 마지막 날 — Day {day}\n사망 {deaths}"
             + (departs > 0 ? $" · 이탈 {departs}" : string.Empty)
             + $" · 정착 {settles}\n\n아무도 남지 않았다.";

        /// <summary>
        /// 전멸 회고 — 통계 대신 **명부** (M13-C1, 순수 — 게이트 M13-T3). 한 줄 = 한 사람:
        /// "A — 게으름뱅이, 무직. Day 3~34, 굶어 죽음". 舊 4인자 오버로드는 게이트
        /// M10-T6 보존을 위해 유지된다 (명세 §3 — 시그니처 변경 금지, 호출처는 명부 우선).
        /// </summary>
        public static string ComposeGameOver(int day, int settles, IReadOnlyList<VillagerRecord> roster)
        {
            // 명부 = 목차 (2026-07-30 개정 — 연대기 서브라인 제거). Day가 쌓이면 사건이 명부를
            // 덮어 읽을 수 없었다 (인스펙터 원칙 ③ "탭으로 깊이를 접는다"). 깊이는 클릭 드릴다운
            // (TryPickGameOverRosterIndex → ShowGameOverDetail)이 맡는다.
            var sb = new System.Text.StringBuilder(256);
            sb.Append($"마을의 마지막 날 — Day {day}\n\n");
            foreach (VillagerRecord r in roster)
                sb.Append($"{r.ShortName} — {r.PersonalityName}, {r.JobName}. {KrLifeSpan(r)}\n");
            sb.Append($"\n정착 {settles} · 아무도 남지 않았다.");
            return sb.ToString();
        }

        /// <summary>
        /// 전멸 회고 + 경주 기록 (M14-W4, 순수 — 게이트 M14-T3). 명부 오버로드 위에 기록 2줄:
        /// 이번 판(넘긴 겨울·생존일·최대 인구)과 역대 최고. best는 **저장 후** 값이라 이번 판이
        /// 최고면 자기 자신이 찍힌다 — newRecord가 그 사실을 문구로 밝힌다.
        /// "N번째 겨울에 쓰러졌다"가 아니라 "겨울 N번을 넘기고"다 — 여름 전멸에서도 어색하지 않은
        /// 전천후 문형 (명세 🟡 문구 조정의 이행).
        /// </summary>
        public static string ComposeGameOver(int day, int settles, IReadOnlyList<VillagerRecord> roster,
                                             int winters, int peakPop,
                                             RunRecordStore.RunRecord best, bool newRecord,
                                             int repels = 0)
        {
            // 격퇴 줄 (M21-W9) — 0회면 생략 (이탈 0 감춤과 같은 규율: 없는 축은 잡음이다)
            string run = $"겨울 {winters}번을 넘기고 Day {day}에 쓰러졌다 · 최대 {peakPop}명"
                       + (repels > 0 ? $" · 격퇴 {repels}회" : string.Empty);
            string record = best == null || (best.BestWinters == 0 && best.BestDay == 0)
                ? "첫 기록이다."
                : (newRecord ? "<color=#FFD966>신기록!</color> " : "역대 최고: ")
                  + $"겨울 {best.BestWinters}번 · Day {best.BestDay} · 최대 {best.BestPeakPop}명"
                  + (best.BestRepels > 0 ? $" · 격퇴 {best.BestRepels}회" : string.Empty);
            // 힌트 줄 (M15-W3, 확정 보완 1) — 전멸 화면은 이미 현재 판 명부라 아카이브 패널을
            // 자동으로 겹치지 않는다 (같은 정보 두 형태 = 오독). 열람 통로는 C 토글 하나.
            return ComposeGameOver(day, settles, roster) + $"\n\n{run}\n{record}"
                 + "\n<color=#B8B8B8>C — 역대 연대기</color>";
        }

        /// <summary>생애 구간 문구 — 생존 중(-1 센티넬)은 열린 구간으로 (Day 0 사망과 구별, 명세 §5.3).</summary>
        private static string KrLifeSpan(VillagerRecord r)
            => r.Cause == ExitCause.Alive
                ? $"Day {(int)r.BornDay}~, 생존"
                : $"Day {(int)r.BornDay}~{(int)r.LeftDay}, {KrCause(r.Cause)}";

        /// <summary>퇴장 사유의 플레이어 언어 — 결말 이원화(ADR-M10-3)가 기록에서도 보인다.</summary>
        private static string KrCause(ExitCause c)
        {
            switch (c)
            {
                case ExitCause.Starvation: return "굶어 죽음";
                case ExitCause.Injury:     return "부상으로 죽음"; // 옛 판 기록 호환 (M21-W9 이후 새 기록 없음)
                case ExitCause.Combat:     return "짐승에게 물려 죽음"; // M21-W9 — "부상"이 아니라 가해자가 이야기다
                case ExitCause.Unknown:    return "행방불명";
                default:                   return "생존";
            }
        }

        private GameObject _gameOver; // 전멸 오버레이 (1회 생성 — 재건은 M11)
        private TextMeshProUGUI _gameOverText;   // 명부 본문 — 드릴다운 클릭 판독 대상 (M13)
        private TextMeshProUGUI _gameOverDetail; // 드릴다운 상세 (하단) — 클릭한 주민의 연대기

        /// <summary>회고 화면 표시 중인가 — PlayerInputController의 클릭 분기용 (M13).</summary>
        public bool GameOverShown => _gameOver != null;

        /// <summary>
        /// 회고 명부 클릭 판독 (M13 — 드릴다운). 화면 좌표 → 명부 몇 번째 사람인가.
        /// 렌더 줄이 아니라 **원문 줄**로 계산한다 — lineInfo의 첫 문자 → 원문 인덱스 →
        /// 개행 수. 오토사이즈로 줄바꿈(wrap)돼도 매핑이 밀리지 않는다 (상태줄 클릭의
        /// 🟡줄바꿈 밀림을 여기선 구조적으로 차단).
        /// 헤더 2줄(제목·빈 줄) 뒤가 명부다. 명부 밖 = false.
        /// </summary>
        public bool TryPickGameOverRosterIndex(Vector2 screenPos, out int rosterIndex)
        {
            rosterIndex = -1;
            if (_gameOverText == null || string.IsNullOrEmpty(_gameOverText.text)) return false;
            int line = TMP_TextUtilities.FindIntersectingLine(_gameOverText, screenPos, null);
            if (line < 0) return false;

            TMP_TextInfo info = _gameOverText.textInfo;
            if (line >= info.lineCount) return false;
            int chArr = info.lineInfo[line].firstCharacterIndex;
            if (chArr < 0 || chArr >= info.characterCount) return false;
            int src = info.characterInfo[chArr].index; // 원문 문자 위치

            string t = _gameOverText.text;
            int composedLine = 0;
            for (int i = 0; i < src && i < t.Length; i++)
                if (t[i] == '\n') composedLine++;
            rosterIndex = composedLine - 2; // 제목 + 빈 줄
            return rosterIndex >= 0;
        }

        /// <summary>드릴다운 상세 표시 (M13) — 회고 하단에 클릭한 주민의 연대기 한 줄.
        /// 다른 주민 클릭 시 교체 (한 명씩 — 깊이는 접어서 보여준다).</summary>
        public void ShowGameOverDetail(string line)
        {
            if (_gameOver == null) return;
            if (_gameOverDetail == null)
            {
                var go = new GameObject("GameOverDetail");
                go.transform.SetParent(_gameOver.transform, false);
                var txt = go.AddComponent<TextMeshProUGUI>();
                if (_calendar.font != null) txt.font = _calendar.font;
                txt.fontSize = 26f;
                txt.enableAutoSizing = true; // 긴 연대기 — 박스에 맞춰 축소 (명부와 동일 방침)
                txt.fontSizeMin = 16f;
                txt.fontSizeMax = 26f;
                txt.alignment = TextAlignmentOptions.Center;
                txt.color = new Color(0.92f, 0.9f, 0.78f);
                txt.raycastTarget = false;
                RectTransform rt = txt.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f); // 하단 중앙
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 40f);
                rt.sizeDelta = new Vector2(1100f, 90f);
                _gameOverDetail = txt;
            }
            _gameOverDetail.text = line ?? "";
        }

        /// <summary>
        /// 전멸 종료 화면 (M10-F) — 반투명 검정 오버레이 + 중앙 요약. 닫기·재건 버튼 없음
        /// (명세 §7 — 관찰은 계속되고, 새 시작은 에디터 재실행). 두 번째 호출은 무시 (래치).
        /// </summary>
        public void ShowGameOver(string text)
        {
            if (_gameOver != null) return;
            Transform canvas = _calendar.rectTransform.parent; // ctor의 SeasonHud 캔버스 재사용

            _gameOver = new GameObject("GameOver");
            _gameOver.transform.SetParent(canvas, false);
            var bg = _gameOver.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0f, 0f, 0f, 0.75f);
            bg.raycastTarget = false; // 상호작용 없음 — 주민 선택 등 입력은 그대로 통과
            RectTransform bgRt = bg.rectTransform;
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = bgRt.offsetMax = Vector2.zero; // 풀스크린

            var txtGo = new GameObject("GameOverText");
            txtGo.transform.SetParent(_gameOver.transform, false);
            var txt = txtGo.AddComponent<TextMeshProUGUI>();
            _gameOverText = txt; // 드릴다운 클릭 판독 대상 (M13)
            if (_calendar.font != null) txt.font = _calendar.font; // 한글 폰트 공유 (W6 패턴)
            txt.fontSize = 44f;
            // 오토사이즈 (M13-C1 — 명세 §12-2 제안 채택): 명부는 주민 수만큼 길어지므로
            // 통계 3줄 기준 고정 44pt로는 넘친다. 스크롤 UI 없이 박스에 맞춰 축소.
            txt.enableAutoSizing = true;
            txt.fontSizeMin = 18f;
            txt.fontSizeMax = 44f;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = new Color(1f, 0.92f, 0.85f);
            txt.raycastTarget = false;
            txt.text = text ?? "";
            RectTransform txtRt = txt.rectTransform;
            txtRt.anchorMin = txtRt.anchorMax = new Vector2(0.5f, 0.5f);
            txtRt.pivot = new Vector2(0.5f, 0.5f);
            txtRt.anchoredPosition = Vector2.zero;
            txtRt.sizeDelta = new Vector2(900f, 700f); // 명부 수용 — 8명 + 여백 (M13-C1)
        }

        // ── 연대기 패널 (M15-W3) — 판 목록 + 판 상세. 한 패널을 두 지점(게임 중 C 토글 ·
        // 전멸 화면 힌트)에서 연다. 표현 전용 — 시뮬레이션 상태를 쓰지 않는다 (ADR-M13-4 정신). ──
        private GameObject _chroniclePanel; // _chronicle(ChronicleService)과 구별 — 이쪽은 오버레이
        private TextMeshProUGUI _chronicleList;
        private TextMeshProUGUI _chronicleDetail;

        /// <summary>연대기 패널 표시 중인가 — PlayerInputController의 키·클릭 분기용.</summary>
        public bool ChronicleShown => _chroniclePanel != null && _chroniclePanel.activeSelf;

        /// <summary>표시 행 구성 (순수) — 진행 중 판(있으면)이 맨 위, 그 뒤 저장된 판 최신순.
        /// 클릭 인덱스 매핑의 단일 출처 — ComposeChronicleList와 호출자가 같은 목록을 쓴다.
        /// skipIndex = 현재 판의 저장 자리 (첫 겨울 이후 존재) — 라이브 행과 같은 판이 두 줄로
        /// 겹치지 않게 제외한다 (Play 검증에서 발견된 중복, 2026-07-31). -1 = 제외 없음.</summary>
        public static List<ChronicleArchive.RunEntry> BuildChronicleRows(
            IReadOnlyList<ChronicleArchive.RunEntry> archived, ChronicleArchive.RunEntry current,
            int skipIndex = -1)
        {
            var rows = new List<ChronicleArchive.RunEntry>((archived?.Count ?? 0) + 1);
            if (current != null) rows.Add(current);
            if (archived != null)
                for (int i = archived.Count - 1; i >= 0; i--) // 최신이 위
                    if (i != skipIndex) rows.Add(archived[i]);
            return rows;
        }

        /// <summary>판 목록 문구 (M15-W3, 순수 — 게이트 M15-T4). 헤더 2줄(제목·빈 줄) 뒤가 목록 —
        /// TryPickChronicleRunIndex의 오프셋과 한 몸. 빈 목록 = 안내 한 줄.</summary>
        public static string ComposeChronicleList(IReadOnlyList<ChronicleArchive.RunEntry> rows,
                                                  bool firstIsCurrent)
        {
            var sb = new System.Text.StringBuilder(256);
            sb.Append("연대기 — 지난 마을들\n\n");
            if (rows == null || rows.Count == 0)
            {
                sb.Append("아직 기록된 마을이 없다.");
                return sb.ToString();
            }
            for (int i = 0; i < rows.Count; i++)
            {
                ChronicleArchive.RunEntry r = rows[i];
                if (i > 0) sb.Append('\n');
                if (i == 0 && firstIsCurrent)
                    // 진행 중 판 — 번호는 store만 알므로 안 붙인다 ("이번 판"이면 충분)
                    sb.Append($"▶ 이번 판 — 겨울 {r.Winters} · Day {r.LastDay} · 최대 {r.PeakPop}명");
                else
                    sb.Append($"판 {r.RunNumber} — 겨울 {r.Winters} · Day {r.LastDay} · 최대 {r.PeakPop}명" +
                              $" · {(r.Ended ? "전멸" : "중단")} · {r.EndedAt}");
            }
            return sb.ToString();
        }

        /// <summary>판 상세 = 명부 (M15-W3, 순수 — 게이트 M15-T4). 한 줄 = 한 사람,
        /// ComposeGraveInfo와 같은 문형 (VillagerEntry판 — 아카이브·현재 판 공용).</summary>
        public static string ComposeRunDetail(ChronicleArchive.RunEntry run)
        {
            if (run == null) return "";
            var sb = new System.Text.StringBuilder(256);
            sb.Append(run.RunNumber > 0 ? $"판 {run.RunNumber}" : "이번 판");
            sb.Append($" — 겨울 {run.Winters} · Day {run.LastDay} · 최대 {run.PeakPop}명 · {(run.Ended ? "전멸" : "진행 중")}");
            sb.Append(ComposeRunEconomy(run.PeakPricePct, run.TaxTotal, run.MintTotal));
            foreach (ChronicleArchive.VillagerEntry v in run.Roster)
                sb.Append('\n').Append(ComposeArchiveGrave(v));
            return sb.ToString();
        }

        /// <summary>판의 경제 한 줄 (M17-W6, 순수 — 게이트 M17-T7).
        ///
        /// **세수와 발행의 비율이 곧 서사다**: 세금으로 굴린 판인가, 찍어서 버틴 판인가.
        /// 북극성("성장하며 버티는 기록 경주")에서 이 판이 어떤 방식으로 버텼는지가 여기 남는다.
        ///
        /// 🔴 `PeakPricePct`는 M16-W6에서 RunEntry에 기록되고도 **화면에 나온 적이 없었다**
        /// (2026-08-01 W6 실사에서 발견 — SeasonHud 전체에 참조 0건). 기록해 놓고 아무도 못
        /// 읽는 것은 M13이 "이야기가 로그에만 있었다"로 진단한 실패와 같은 종류다. 여기서 낸다.
        ///
        /// 세 항목 모두 0이면 줄 자체가 없다 — 화폐 이전 기록·무변동 판과의 호환 (M15 표기 규약).</summary>
        public static string ComposeRunEconomy(int peakPricePct, int taxTotal, int mintTotal)
        {
            bool anyPrice = peakPricePct > 100;
            if (!anyPrice && taxTotal <= 0 && mintTotal <= 0) return string.Empty;

            var sb = new System.Text.StringBuilder(64);
            if (anyPrice) sb.Append($" · 최고 물가 ×{peakPricePct / 100f:0.0#}");
            if (taxTotal > 0)  sb.Append($" · 세수 {ComposeMoney(taxTotal)}");
            if (mintTotal > 0) sb.Append($" · 발행 {ComposeMoney(mintTotal)}");
            return sb.ToString();
        }

        /// <summary>아카이브 명부 한 줄 (순수) — ComposeGraveInfo의 VillagerEntry판.
        /// 생존 중(현재 판)은 † 없이 열린 구간으로 (죽지 않은 사람에게 비석을 세우지 않는다).</summary>
        public static string ComposeArchiveGrave(ChronicleArchive.VillagerEntry v)
        {
            bool alive = v.Cause == (int)ExitCause.Alive;
            string span = alive
                ? $"Day {v.BornDay}~, 생존"
                : $"Day {v.BornDay}~{v.LeftDay}, {KrCause((ExitCause)v.Cause)}";
            string line = $"{(alive ? "" : "† ")}{v.ShortName} — {v.Personality}, {v.Job}. {span}";
            if (!string.IsNullOrEmpty(v.BuddyShort))
                line += $" · {v.BuddyShort}의 단짝이었다";
            if (!string.IsNullOrEmpty(v.GrudgeShort))
                line += $" · <color=#FF8A65>{v.GrudgeShort}에게 원한이 있었다</color>";
            if (!string.IsNullOrEmpty(v.LifeEvents))
                line += $" · <color=#B8B8B8>{v.LifeEvents}</color>";
            return line;
        }

        /// <summary>판 목록 클릭 판독 — TryPickGameOverRosterIndex와 같은 기법 (원문 줄 계산 —
        /// wrap 밀림 차단). 헤더 2줄 뒤가 목록. 목록 밖 = false.</summary>
        public bool TryPickChronicleRunIndex(Vector2 screenPos, out int runIndex)
        {
            runIndex = -1;
            if (_chronicleList == null || string.IsNullOrEmpty(_chronicleList.text)) return false;
            int line = TMP_TextUtilities.FindIntersectingLine(_chronicleList, screenPos, null);
            if (line < 0) return false;

            TMP_TextInfo info = _chronicleList.textInfo;
            if (line >= info.lineCount) return false;
            int chArr = info.lineInfo[line].firstCharacterIndex;
            if (chArr < 0 || chArr >= info.characterCount) return false;
            int src = info.characterInfo[chArr].index;

            string t = _chronicleList.text;
            int composedLine = 0;
            for (int i = 0; i < src && i < t.Length; i++)
                if (t[i] == '\n') composedLine++;
            runIndex = composedLine - 2; // 제목 + 빈 줄
            return runIndex >= 0;
        }

        /// <summary>연대기 패널 토글 — 열 때 목록 텍스트를 받고 상세는 비운다. 닫기 = SetActive
        /// (재생성 없음). 열 때 SetAsLastSibling — 전멸 오버레이가 나중에 생겨도 그 위로 뜬다.</summary>
        public void ToggleChronicle(string listText)
        {
            if (ChronicleShown)
            {
                _chroniclePanel.SetActive(false);
                // 전멸 화면 복원 — 열 때 숨겼던 것 (겹침 수정, 2026-07-31). GameOverShown은
                // null 검사라 래치·클릭 분기에 영향 없다.
                if (_gameOver != null) _gameOver.SetActive(true);
                return;
            }
            if (_chroniclePanel == null) BuildChroniclePanel();
            _chronicleList.text = listText ?? "";
            _chronicleDetail.text = "판을 클릭하면 그 마을의 명부가 여기 펼쳐진다.";
            // 전멸 화면과 겹치면 두 겹의 밝은 글자가 서로를 뚫고 읽힌다 (Play 검증 스크린샷) —
            // 반투명 배경으로는 못 가리므로 여는 동안 숨긴다.
            if (_gameOver != null) _gameOver.SetActive(false);
            _chroniclePanel.transform.SetAsLastSibling();
            _chroniclePanel.SetActive(true);
        }

        /// <summary>판 상세 표시 — 클릭한 판의 명부를 하단에 (다른 판 클릭 시 교체).</summary>
        public void ShowChronicleDetail(string detailText)
        {
            if (_chronicleDetail != null) _chronicleDetail.text = detailText ?? "";
        }

        private void BuildChroniclePanel()
        {
            Transform canvas = _calendar.rectTransform.parent; // ShowGameOver와 같은 캔버스 재사용

            _chroniclePanel = new GameObject("Chronicle");
            _chroniclePanel.transform.SetParent(canvas, false);
            var bg = _chroniclePanel.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0f, 0f, 0f, 0.85f); // 전멸 오버레이보다 살짝 진하게 — 겹쳐도 구분
            bg.raycastTarget = false; // 클릭 소비는 PlayerInputController가 담당 (기존 판독 순서 규약)
            RectTransform bgRt = bg.rectTransform;
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

            var listGo = new GameObject("ChronicleList");
            listGo.transform.SetParent(_chroniclePanel.transform, false);
            _chronicleList = listGo.AddComponent<TextMeshProUGUI>();
            if (_calendar.font != null) _chronicleList.font = _calendar.font;
            _chronicleList.enableAutoSizing = true; // 판이 쌓이면 길어진다 — 회고 명부와 동일 방침
            _chronicleList.fontSizeMin = 16f;
            _chronicleList.fontSizeMax = 36f;
            _chronicleList.alignment = TextAlignmentOptions.Center;
            _chronicleList.color = new Color(1f, 0.92f, 0.85f);
            _chronicleList.raycastTarget = false;
            RectTransform listRt = _chronicleList.rectTransform;
            listRt.anchorMin = listRt.anchorMax = new Vector2(0.5f, 0.5f);
            listRt.pivot = new Vector2(0.5f, 0f);
            listRt.anchoredPosition = new Vector2(0f, -20f); // 화면 상반부 = 목록
            listRt.sizeDelta = new Vector2(900f, 340f);

            var detailGo = new GameObject("ChronicleDetail");
            detailGo.transform.SetParent(_chroniclePanel.transform, false);
            _chronicleDetail = detailGo.AddComponent<TextMeshProUGUI>();
            if (_calendar.font != null) _chronicleDetail.font = _calendar.font;
            _chronicleDetail.enableAutoSizing = true;
            _chronicleDetail.fontSizeMin = 14f;
            _chronicleDetail.fontSizeMax = 26f;
            _chronicleDetail.alignment = TextAlignmentOptions.Center;
            _chronicleDetail.color = new Color(0.92f, 0.9f, 0.78f); // 드릴다운과 같은 톤
            _chronicleDetail.raycastTarget = false;
            RectTransform detailRt = _chronicleDetail.rectTransform;
            detailRt.anchorMin = detailRt.anchorMax = new Vector2(0.5f, 0.5f);
            detailRt.pivot = new Vector2(0.5f, 1f);
            detailRt.anchoredPosition = new Vector2(0f, -60f); // 하반부 = 명부 상세
            detailRt.sizeDelta = new Vector2(1100f, 300f);
        }

        /// <summary>
        /// 상태 알림 조립 (M13-B, 순수 — 게이트 M13-T2). **해소되면 빈 문자열** = 줄이 사라진다.
        /// 조건이 하나도 없으면 "" — 평온한 마을에 경보가 떠 있으면 잡음이지 알림이 아니다 (중립 불변식).
        /// 굶는 주민은 **개인 단위로 한 줄씩** (2026-07-30 사용자 피드백 — "누구인지 모르는 정보"는
        /// 개입을 못 만든다. N명이면 N줄). 위협색은 달력 예고와 같은 주황 (#FF8A65).
        ///
        /// 🔴 2026-08-07 개정 — 굶주림 줄의 **판정 기준이 저장 식량에서 몸 상태로** 옮겨졌다
        /// (VillagerAgent.JudgeHunger). 舊 기준(식량 ≤ FOOD_ALERT_DAYS)은 비축이 0인 Day 0에
        /// 전원 참이라 화면이 빨간 줄 8개로 시작했다 — 항상 켜진 경보는 배경이지 경보가 아니다.
        /// 저장 식량 일수는 판정에서 빠지고 **참고 수치로만** 남는다 (개입 판단에 여전히 쓸모 있다:
        /// 포만이 낮은데 식량도 0이면 명령이 필요하고, 식량이 있으면 곧 알아서 먹는다).
        /// 두 등급(배고픔 주황 / 굶주림 빨강)은 "절벽이 아니라 계단"(StarvingBelowSatiety 규약)의 표현.
        ///
        /// ⚠️ 확장 규칙: 새 상태 종류(추위·수면 부족 등)는 인자 + Append 블록을 **굶는 주민 줄
        /// 뒤에** 추가한다. 클릭 매핑(SimulationLoop.FindStarvingVillagerAt)이 "굶는 줄 =
        /// 맨 앞 0..N-1"을 전제하므로, 앞에 끼우면 클릭이 엉뚱한 주민을 집는다.
        /// </summary>
        public static string ComposeStatus(
            IReadOnlyList<(string name, int satiety, int foodDays, bool critical)> starving,
            int untendedInjured, int threatDaysLeft, string threatName,
            int freezeDaysLeft = -1,
            IReadOnlyList<(string name, int days)> unprepared = null)
        {
            bool threat = threatDaysLeft >= 0 && !string.IsNullOrEmpty(threatName);
            bool anyStarving = starving != null && starving.Count > 0;
            // 겨울 경보 (M14-W4) — 예방 전용: 봉쇄 중(0)·창 밖(-1)·전원 대비 완료면 줄 자체가 없다.
            bool winterAlert = freezeDaysLeft > 0 && unprepared != null && unprepared.Count > 0;
            if (!anyStarving && untendedInjured <= 0 && !threat && !winterAlert)
                return ""; // 평시 조기 반환 — 틱마다 불리므로 무경보 시 할당 0 (달력 캐시와 같은 GC 배려)

            var sb = new System.Text.StringBuilder(96);
            if (anyStarving)
                for (int i = 0; i < starving.Count; i++)
                {
                    // (M19-W4: 지갑 병기 철거 — 굶주림 구제는 나눔 부탁이 맡는다)
                    (string name, int satiety, int foodDays, bool critical) s = starving[i];
                    // 식량 일수는 참고 수치 — 99(중립·미배선)면 생략한다 (ComposeReason과 같은 규약).
                    string food = s.foodDays < WorldModel.NO_ESTIMATE ? $" · 식량 {s.foodDays}일치" : "";
                    sb.Append(s.critical
                        ? $"<color=#FF6B6B>■ 굶주리는 주민 {s.name} — 포만 {s.satiety}, 체력이 깎이는 중{food}</color>\n"
                        : $"<color=#FFB74D>■ 배고픈 주민 {s.name} — 포만 {s.satiety}{food}</color>\n");
                }
            if (untendedInjured > 0)
                sb.Append($"<color=#FF6B6B>■ 치료가 필요한 부상자 {untendedInjured}명</color>\n");
            if (threat)
                sb.Append($"<color=#FF8A65>■ {threatName} — {threatDaysLeft}일 뒤</color>\n");
            // 확장 규칙 준수 — 굶는 줄 뒤에만 추가 (클릭 매핑 "굶는 줄 = 맨 앞" 전제 보존)
            if (winterAlert)
            {
                sb.Append($"<color=#7EC8FF>■ 겨울까지 {freezeDaysLeft}일 — 준비 부족: ");
                for (int i = 0; i < unprepared.Count; i++)
                {
                    if (i > 0) sb.Append(" · ");
                    sb.Append($"{unprepared[i].name}({unprepared[i].days}일)");
                }
                sb.Append("</color>\n");
            }
            return sb.ToString().TrimEnd('\n');
        }

        /// <summary>겨울 경보 창 (게임일) — WinterPrep 트리거 창(DaysToFreeze ≤ 5)과 같은 제안치 (명세 W4).</summary>
        public const int WINTER_ALERT_DAYS = 5;

        /// <summary>상태 알림 갱신 (M13-B) — SimulationLoop 틱마다 호출. 값이 바뀔 때만 재할당
        /// (달력과 동일 패턴). 빈 문자열 = 줄 소거.</summary>
        /// <summary>자원 줄 갱신 (M22-2차 W4) — 표시 전용, 캐시로 재대입 방지. 목록·라벨의
        /// 원천 = WorldConfigSO.HudResources (비면 줄 없음 = 중립). 값 = WorldModel 전역 스톡.</summary>
        public void TickResources(WorldModel world)
        {
            if (world == null || _worldCfg == null || _worldCfg.HudResources == null
                || _worldCfg.HudResources.Length == 0) return;
            _resBuf.Length = 0;
            foreach (WorldConfigSO.ResourceHudEntry e in _worldCfg.HudResources)
            {
                if (string.IsNullOrEmpty(e.Label)) continue; // 빈 라벨 행 = 무시 (배선 실수 중립)
                if (_resBuf.Length > 0) _resBuf.Append("  ·  ");
                _resBuf.Append(e.Label).Append(' ').Append(world.GetStock(e.Slot));
            }
            string line = _resBuf.ToString();
            if (line == _lastResources) return;
            _lastResources = line;
            _resources.text = line;
            Reflow(); // 첫 표시·자릿수 변화로 높이가 바뀔 수 있다 — 즉시 재쌓기
        }

        private string _lastResources;
        private readonly System.Text.StringBuilder _resBuf = new System.Text.StringBuilder(64);

        public void TickStatus(string line)
        {
            if (line == _lastStatus) return;
            _lastStatus = line;
            _status.text = line ?? "";
        }

        /// <summary>상태 알림 클릭 판독 (M13-B 후속) — 화면 좌표가 상태줄의 몇 번째 줄인가.
        /// 텍스트 밖·빈 상태 = -1. EventSystem 불필요 — PlayerInputController의 수동 픽킹 패턴.
        /// ScreenSpaceOverlay 캔버스라 camera 인자는 null (TMP 규약).</summary>
        public int PickStatusLine(Vector2 screenPos)
        {
            if (string.IsNullOrEmpty(_status.text)) return -1;
            return TMP_TextUtilities.FindIntersectingLine(_status, screenPos, null);
        }

        /// <summary>결정 프롬프트 표시 (M10-E) — 알림(Notify)과 달리 ClearPrompt까지 상시 유지.
        /// 플레이어 입력을 기다리는 줄이라 자동 소거가 없다 (놓침 방지 — 예고 휘발성 교훈).</summary>
        /// <summary>모드 정보줄 (M22-W3R3) — 입력 모드(울타리 그리기 등)의 상시 안내.
        /// 프롬프트(적습·방랑자)와 별줄 — 모드 종료가 결정 프롬프트를 지우면 안 된다.</summary>
        public void SetModeInfo(string line) => _modeInfo.text = line ?? "";

        public void ClearModeInfo() => _modeInfo.text = "";

        public void SetPrompt(string line) => _prompt.text = line ?? "";

        /// <summary>결정 프롬프트 소거 — 해소(수락·거절·시간 초과)의 표현 짝.</summary>
        public void ClearPrompt() => _prompt.text = "";

        /// <summary>이벤트 알림 1줄 (계절 전환·주민 이탈 등) — 최신 1건만, NOTIFY_SEC 후 소거.</summary>
        public void Notify(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            _notice.text = line;
            _noticeUntil = Time.time + NOTIFY_SEC;
        }
    }
}
