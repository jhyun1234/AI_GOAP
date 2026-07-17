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
        private float _noticeUntil;
        private string _lastCalendar;
        private VillagerAgent _selected;
        private string _lastSelectedLine;

        public SeasonHud(Transform parent, TMP_FontAsset font)
        {
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

        /// <summary>SimulationLoop 틱마다 호출 — 문자열은 값이 바뀔 때만 재조립 (GC 절약).</summary>
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

            string line = ComposeSelected(_selected);
            if (line != _lastSelectedLine)
            {
                _lastSelectedLine = line;
                _selectedInfo.text = line;
            }
        }

        /// <summary>
        /// 정보줄 문구 조립 — 성격·직업을 구분 표기해 이름 혼동을 해소 (ADR-M7-5의 짝).
        /// 예: "A — 성격 고집쟁이 · 직업 농부 · 포만 45 · 피로 60 · 지금: 겨울 비축"
        /// </summary>
        public static string ComposeSelected(VillagerAgent a)
            => $"{a.ShortName} — 성격 {(a.Personality != null ? a.Personality.DisplayName : "없음")}" +
               $" · 직업 {(a.Job != null ? a.Job.DisplayName : "무직")}" +
               $" · 포만 {Mathf.RoundToInt(a.Satiety)} · 피로 {Mathf.RoundToInt(a.Fatigue)}" +
               $" · 지금: {(a.CurrentGoal != null ? a.CurrentGoal.DisplayName : "쉬는 중")}";

        /// <summary>
        /// 달력 문구 조립 — 표시 정책의 단일 지점, 순수 함수 (EditMode 게이트 대상).
        /// 평시 "Day N · 계절" / 예고 "…겨울까지 N일"(주황) / 위기 "…겨울 (남은 N일)"(하늘).
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

        /// <summary>이벤트 알림 1줄 (계절 전환·주민 이탈 등) — 최신 1건만, NOTIFY_SEC 후 소거.</summary>
        public void Notify(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            _notice.text = line;
            _noticeUntil = Time.time + NOTIFY_SEC;
        }
    }
}
