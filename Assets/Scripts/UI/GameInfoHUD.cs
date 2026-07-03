/// <summary>
/// GameInfoHUD.cs - 게임 일수·계절·생존 주민 수 상시 표시 HUD
///
/// 역할(Role): 게임 진행 상태(Day, Season, 주민 수)를 0.5초마다 읽어
///             TMP_Text 3개에 표시한다.
///             값이 바뀐 경우에만 SetText를 호출하여 TMP 메시 재생성 비용을 절감한다.
///
/// 사용법(Usage):
///   1. Canvas 아래 GameInfoHUD Panel GameObject에 부착한다.
///   2. Inspector에서 TMP_Text 3개(_dayText, _seasonText, _villagerCountText)를 연결한다.
///   3. HUDManager.Start()에서 RefreshCoroutine()을 StartCoroutine으로 시작한다.
///
/// 의존성(Dependencies):
///   - AuthoritativeWorldState.cs (GameDay)
///   - SeasonManager.cs (CurrentSeason)
///   - GameManager.cs (Villagers.Count)
///   - TextMeshPro
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-30
/// </summary>

using System.Collections;
using UnityEngine;
using TMPro;
using AIVillage.Core;

namespace AIVillage.UI
{
    /// <summary>
    /// 게임 일수·계절·생존 주민 수를 상시 표시하는 HUD 패널.
    /// ResourceHUD와 동일한 변경-감지 패턴으로 TMP 갱신 비용을 최소화한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameInfoHUD : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════════
        // 상수
        // ══════════════════════════════════════════════════════════════════════

        #region ── 상수 ──

        private const float REFRESH_INTERVAL = 0.5f;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Serialized Fields (Inspector)
        // ══════════════════════════════════════════════════════════════════════

        #region ── Serialized Fields ──

        [Tooltip("현재 게임 일수를 표시하는 TMP_Text. 예: 'Day 7'")]
        [SerializeField] private TMP_Text _dayText;

        [Tooltip("현재 계절을 표시하는 TMP_Text. 예: '계절: 여름'")]
        [SerializeField] private TMP_Text _seasonText;

        [Tooltip("생존 주민 수를 표시하는 TMP_Text. 예: '주민: 5명'")]
        [SerializeField] private TMP_Text _villagerCountText;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private Fields — 이전 값 캐시 (변경 감지용)
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private Fields ──

        private int    _prevDay     = -1;
        private Season _prevSeason  = (Season)(-1); // 유효하지 않은 초기값 → 첫 프레임 강제 갱신
        private int    _prevCount   = -1;

        private WaitForSeconds _wait;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Unity 생명주기
        // ══════════════════════════════════════════════════════════════════════

        #region ── Unity 생명주기 ──

        private void Awake()
        {
            _wait = new WaitForSeconds(REFRESH_INTERVAL);
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // 공개 메서드
        // ══════════════════════════════════════════════════════════════════════

        #region ── 공개 메서드 ──

        /// <summary>
        /// 0.5초마다 게임 상태를 폴링하여 TMP_Text를 갱신하는 코루틴.
        /// HUDManager.Start()에서 StartCoroutine으로 시작한다.
        /// </summary>
        public IEnumerator RefreshCoroutine()
        {
            while (true)
            {
                yield return _wait;

                RefreshDay();
                RefreshSeason();
                RefreshVillagerCount();
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private 헬퍼
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private 헬퍼 ──

        private void RefreshDay()
        {
            if (_dayText == null) return;

            int day = AuthoritativeWorldState.Instance?.GameDay ?? 1;
            if (day == _prevDay) return;

            _prevDay = day;
            _dayText.SetText($"Day {day}");
        }

        private void RefreshSeason()
        {
            if (_seasonText == null) return;

            Season season = SeasonManager.Instance != null
                ? SeasonManager.Instance.CurrentSeason
                : Season.Spring;

            if (season == _prevSeason) return;

            _prevSeason = season;
            _seasonText.SetText($"계절: {ToKorean(season)}");
        }

        private void RefreshVillagerCount()
        {
            if (_villagerCountText == null) return;

            int count = GameManager.Instance?.Villagers?.Count ?? 0;
            if (count == _prevCount) return;

            _prevCount = count;
            _villagerCountText.SetText($"주민: {count}명");
        }

        private static string ToKorean(Season season)
        {
            switch (season)
            {
                case Season.Spring: return "봄";
                case Season.Summer: return "여름";
                case Season.Autumn: return "가을";
                case Season.Winter: return "❄겨울";
                default:            return season.ToString();
            }
        }

        #endregion
    }
}
