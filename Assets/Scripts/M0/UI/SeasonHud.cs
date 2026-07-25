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

        private readonly TMP_Text _calendar;
        private readonly TMP_Text _notice;
        private readonly TMP_Text _selectedInfo;
        private readonly TMP_Text _prompt; // 결정 프롬프트 (M10-E) — 방랑자 Y/N 등 상시 유지 줄
        private float _noticeUntil;
        private string _lastCalendar;
        private VillagerAgent _selected;
        private string _lastSelectedLine;

        // 관계·소유·부탁 표기 (M8-B/C/후속) — 읽기 전용 참조. null이면 미표기 (중립 — M7 표시와 동일)
        private readonly RelationshipService _relationship;
        private readonly WorldConfigSO _worldCfg;
        private readonly OwnershipService _ownership;
        private readonly RequestService _requests;
        private readonly HomeStorageService _homeStorage; // 집 저장 표기 (M11-A)

        public SeasonHud(Transform parent, TMP_FontAsset font,
                         RelationshipService relationship = null, WorldConfigSO worldCfg = null,
                         OwnershipService ownership = null, RequestService requests = null,
                         HomeStorageService homeStorage = null)
        {
            _relationship = relationship;
            _worldCfg = worldCfg;
            _ownership = ownership;
            _requests = requests;
            _homeStorage = homeStorage;
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
        /// foodDaysLeft는 전 주민 EstimatePersonalFoodDays 최솟값 (M11-D, SimulationLoop 집계 —
        /// HUD 자체 재계산 금지는 M9-I 그대로).</summary>
        public void Tick(float gameTime, SeasonService season, float forecastDays,
                         int foodDaysLeft = WorldModel.NO_ESTIMATE)
        {
            string line = Compose(gameTime, season, forecastDays, foodDaysLeft);
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
                                          _homeStorage);
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
                                             HomeStorageService homeStorage = null)
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
            return line;
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
        /// 뒤에 "식량 N일치"(M9-I) — 99(중립)면 생략, 2일치 이하는 붉은 강조.
        /// </summary>
        public static string Compose(float gameTime, SeasonService season, float forecastDays,
                                     int foodDaysLeft = WorldModel.NO_ESTIMATE)
        {
            int day = (int)gameTime;
            string food = FoodSuffix(foodDaysLeft);
            if (season == null || season.Current == null) return $"Day {day}{food}";

            SeasonSO cur = season.Current;
            if (cur.IsCrisis)
                return $"Day {day} · <color=#7EC8FF>{cur.DisplayName}</color> " +
                       $"(남은 {Mathf.CeilToInt(season.DaysLeftInSeason)}일){food}";
            if (season.NextCrisis != null && season.DaysToCrisis <= forecastDays)
                return $"Day {day} · {cur.DisplayName} · <color=#FF8A65>" +
                       $"{season.NextCrisis.DisplayName}까지 {Mathf.CeilToInt(season.DaysToCrisis)}일</color>{food}";
            return $"Day {day} · {cur.DisplayName}{food}";
        }

        /// <summary>식량 일수 접미사 (M9-I, M11-D 개인화 — 값 = 전 주민 최솟값 '가장 위험한 주민')
        /// — 중립(99)이면 빈 문자열, ≤2일치는 붉은 강조.</summary>
        private static string FoodSuffix(int foodDaysLeft)
        {
            if (foodDaysLeft >= WorldModel.NO_ESTIMATE) return ""; // 미배선·풍족 = 표기 없음 (중립)
            return foodDaysLeft <= 2
                ? $" · <color=#FF6B6B>식량 최소 {foodDaysLeft}일치</color>"
                : $" · 식량 최소 {foodDaysLeft}일치";
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
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = new Color(1f, 0.92f, 0.85f);
            txt.raycastTarget = false;
            txt.text = text ?? "";
            RectTransform txtRt = txt.rectTransform;
            txtRt.anchorMin = txtRt.anchorMax = new Vector2(0.5f, 0.5f);
            txtRt.pivot = new Vector2(0.5f, 0.5f);
            txtRt.anchoredPosition = Vector2.zero;
            txtRt.sizeDelta = new Vector2(900f, 400f);
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
