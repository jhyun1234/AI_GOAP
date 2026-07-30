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
        private readonly TMP_Text _notice;
        private readonly TMP_Text _selectedInfo;
        private readonly TMP_Text _prompt; // 결정 프롬프트 (M10-E) — 방랑자 Y/N 등 상시 유지 줄
        private readonly TMP_Text _status; // 상태 알림 (M13-B) — 해소될 때까지 유지 (_prompt 패턴의 일반화)
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

            _calendar = MakeText(root.transform, "Calendar", font, new Vector2(12f, -10f), 30f);
            _notice   = MakeText(root.transform, "Notice",   font, new Vector2(12f, -48f), 24f);
            _notice.text = "";
            _selectedInfo = MakeText(root.transform, "SelectedInfo", font, new Vector2(12f, -86f), 24f);
            _selectedInfo.text = "";
            // 결정 프롬프트 (M10-E) — 알림과 달리 상시 유지 (해소될 때까지). 노랑 강조.
            _prompt = MakeText(root.transform, "Prompt", font, new Vector2(12f, -124f), 26f);
            _prompt.color = new Color(1f, 0.85f, 0.4f);
            _prompt.text = "";
            // 상태 알림 (M13-B) — 순간 사건(Notify)과 달리 조건이 해소될 때까지 남는다
            // ("식량 부족"은 지나가는 소식이 아니라 지금 손쓸 수 있는 상태다 — 예고 휘발성 교훈의 일반화).
            // ⚠️ 다줄이라 이 인스턴스만 높이를 키운다 — MakeText 공유값(760,40)은 불변 (명세 §6-B ⚠️).
            // 폰트는 에셋 값 (WorldConfigSO.HudStatusFontSize — "작다" Play 피드백으로 승격),
            // 높이는 폰트 비례(×16 ≈ 12줄) — 굶는 주민 개인 열거 + 부상·위협 줄 수용.
            float statusSize = worldCfg != null && worldCfg.HudStatusFontSize > 0f
                ? worldCfg.HudStatusFontSize : 24f;
            _status = MakeText(root.transform, "Status", font, new Vector2(12f, -162f), statusSize);
            _status.rectTransform.sizeDelta = new Vector2(760f, statusSize * 16f);
            _status.text = "";
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
        public void Tick(float gameTime, SeasonService season, float forecastDays)
        {
            string line = Compose(gameTime, season, forecastDays);
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
        }

        /// <summary>주민 선택/해제 (M7-A) — PlayerInputController가 호출 (null = 해제).</summary>
        public void SetSelected(VillagerAgent agent)
        {
            _selected = agent;
            TickSelected(); // 다음 틱을 기다리지 않고 즉시 반영 (선택 반응성)
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
                if (_lastSelectedLine != "")
                {
                    _lastSelectedLine = "";
                    _selectedInfo.text = "";
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
                // 소지 식량 표기 (M11-A 관측 — 보상 차감·저장 이동을 콘솔 없이 화면에서 확인)
                $" · 소지 생{a.MyRaw}·조{a.MyCooked}" +
                // 부상 표기 (M10-A) — 붉은 강조. None이면 표기 없음 (중립 — M9 표시와 동일)
                (a.Injury != InjurySeverity.None ? " · <color=#FF6B6B>부상</color>" : "") +
                $" · 지금: {(a.CurrentGoal != null ? a.CurrentGoal.DisplayName : "쉬는 중")}";

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
            // 명세 초안의 "3줄"을 "한 줄에 최근 3건"으로 바꿨다 — 정보줄 아래(-124~)에
            // 프롬프트·상태줄이 살고 있어 다줄 확장은 겹친다 (M8-B 관계 극단 1명 전례와 동일 규율).
            if (chronicle != null && chronicle.TryGetRecord(a.AgentId, out VillagerRecord rec))
            {
                string recent = ComposeRecentEvents(rec, RECENT_EVENTS_MAX);
                if (recent.Length > 0) line += $" · <color=#B8B8B8>{recent}</color>";
            }
            return line;
        }

        /// <summary>정보줄 최근 사건 표시 수 (제안치 3 — 명세 §12-1, 유일하게 발명한 수치). 연출 상수.</summary>
        private const int RECENT_EVENTS_MAX = 3;

        /// <summary>최근 사건 요약 (M13-C2, 순수 — 게이트 M13-T4). 최신부터 max건,
        /// "최근: 치료받음 D9 · 집 마련 D7". 사건 0건 = 빈 문자열 (중립).</summary>
        public static string ComposeRecentEvents(VillagerRecord r, int max)
        {
            if (r == null || r.Events.Count == 0 || max <= 0) return "";
            var sb = new System.Text.StringBuilder(48);
            sb.Append("최근: ");
            int shown = 0;
            for (int i = r.Events.Count - 1; i >= 0 && shown < max; i--, shown++)
            {
                if (shown > 0) sb.Append(" · ");
                sb.Append($"{KrEvent(r.Events[i])} D{(int)r.Events[i].Day}");
            }
            return sb.ToString();
        }

        /// <summary>개인 연대기 (M13-C2, 순수 — 회고용). 시간순 전체,
        /// "D3 다침 · D9 치료받음". 사건 0건 = 빈 문자열 (그 줄 자체가 없다).</summary>
        public static string ComposeLifeEvents(VillagerRecord r)
        {
            if (r == null || r.Events.Count == 0) return "";
            var sb = new System.Text.StringBuilder(64);
            for (int i = 0; i < r.Events.Count; i++)
            {
                if (i > 0) sb.Append(" · ");
                sb.Append($"D{(int)r.Events[i].Day} {KrEvent(r.Events[i])}");
            }
            return sb.ToString();
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
                case EventId.Built:        return "완공";
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

        /// <summary>AgentId → 표시명 ("M0_Villager_A" → "A") — VillagerAgent.ShortName과 동일 규칙.</summary>
        private static string ToShortName(string agentId)
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
        public static string Compose(float gameTime, SeasonService season, float forecastDays)
        {
            int day = (int)gameTime;
            if (season == null || season.Current == null) return $"Day {day}";

            SeasonSO cur = season.Current;
            if (cur.IsCrisis)
                return $"Day {day} · <color=#7EC8FF>{cur.DisplayName}</color> " +
                       $"(남은 {Mathf.CeilToInt(season.DaysLeftInSeason)}일)";
            if (season.NextCrisis != null && season.DaysToCrisis <= forecastDays)
                return $"Day {day} · {cur.DisplayName} · <color=#FF8A65>" +
                       $"{season.NextCrisis.DisplayName}까지 {Mathf.CeilToInt(season.DaysToCrisis)}일</color>";
            return $"Day {day} · {cur.DisplayName}";
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
            var sb = new System.Text.StringBuilder(256);
            sb.Append($"마을의 마지막 날 — Day {day}\n\n");
            foreach (VillagerRecord r in roster)
            {
                sb.Append($"{r.ShortName} — {r.PersonalityName}, {r.JobName}. {KrLifeSpan(r)}\n");
                // 개인 연대기 (M13-C2) — 그 사람에게 있었던 일. 사건 0건이면 줄 자체가 없다.
                string life = ComposeLifeEvents(r);
                if (life.Length > 0) sb.Append($"<size=70%><color=#C8C4B0>{life}</color></size>\n");
            }
            sb.Append($"\n정착 {settles} · 아무도 남지 않았다.");
            return sb.ToString();
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
                case ExitCause.Injury:     return "부상으로 죽음";
                case ExitCause.Unknown:    return "행방불명";
                default:                   return "생존";
            }
        }

        private GameObject _gameOver; // 전멸 오버레이 (1회 생성 — 재건은 M11)

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

        /// <summary>
        /// 상태 알림 조립 (M13-B, 순수 — 게이트 M13-T2). **해소되면 빈 문자열** = 줄이 사라진다.
        /// 조건이 하나도 없으면 "" — 평온한 마을에 경보가 떠 있으면 잡음이지 알림이 아니다 (중립 불변식).
        /// 굶는 주민은 **개인 단위로 한 줄씩** (2026-07-30 사용자 피드백 — "누구인지 모르는 정보"는
        /// 개입을 못 만든다. N명이면 N줄). 목록은 호출자가 FOOD_ALERT_DAYS로 걸러 넘긴다 —
        /// 여기는 표시만 한다. 위협색은 달력 예고와 같은 주황 (#FF8A65).
        ///
        /// ⚠️ 확장 규칙: 새 상태 종류(추위·수면 부족 등)는 인자 + Append 블록을 **굶는 주민 줄
        /// 뒤에** 추가한다. 클릭 매핑(SimulationLoop.FindStarvingVillagerAt)이 "굶는 줄 =
        /// 맨 앞 0..N-1"을 전제하므로, 앞에 끼우면 클릭이 엉뚱한 주민을 집는다.
        /// </summary>
        public static string ComposeStatus(IReadOnlyList<(string name, int days)> starving,
                                           int untendedInjured, int threatDaysLeft, string threatName)
        {
            bool threat = threatDaysLeft >= 0 && !string.IsNullOrEmpty(threatName);
            bool anyStarving = starving != null && starving.Count > 0;
            if (!anyStarving && untendedInjured <= 0 && !threat)
                return ""; // 평시 조기 반환 — 틱마다 불리므로 무경보 시 할당 0 (달력 캐시와 같은 GC 배려)

            var sb = new System.Text.StringBuilder(96);
            if (anyStarving)
                foreach ((string name, int days) s in starving)
                    sb.Append($"<color=#FF6B6B>■ 굶는 주민 {s.name} — 식량 {s.days}일치</color>\n");
            if (untendedInjured > 0)
                sb.Append($"<color=#FF6B6B>■ 치료가 필요한 부상자 {untendedInjured}명</color>\n");
            if (threat)
                sb.Append($"<color=#FF8A65>■ {threatName} — {threatDaysLeft}일 뒤</color>\n");
            return sb.ToString().TrimEnd('\n');
        }

        /// <summary>상태 알림 갱신 (M13-B) — SimulationLoop 틱마다 호출. 값이 바뀔 때만 재할당
        /// (달력과 동일 패턴). 빈 문자열 = 줄 소거.</summary>
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
