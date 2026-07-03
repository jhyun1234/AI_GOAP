/// <summary>
/// ResourceHUD.cs - 자원 재고 표시 HUD 패널
///
/// 역할(Role): AuthoritativeWorldState에서 6개 자원 재고를 읽어
///             TMP_Text 컴포넌트에 표시한다.
///             값이 바뀐 경우에만 SetText를 호출하여 TextMeshPro의
///             메시 재생성 비용을 절감한다.
///
/// 사용법(Usage):
///   1. Canvas 아래 ResourceHUD Panel GameObject에 부착한다.
///   2. Inspector에서 TMP_Text 6개를 연결한다.
///   3. HUDManager.Start()에서 RefreshCoroutine()을 StartCoroutine으로 시작한다.
///
/// 의존성(Dependencies): AuthoritativeWorldState.cs, TextMeshPro
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-28
/// </summary>

using System.Collections;
using UnityEngine;
using TMPro;
using AIVillage.Core;

namespace AIVillage.UI
{
    /// <summary>
    /// 자원 재고 6종을 화면에 표시하는 HUD 패널.
    /// 변경 시에만 텍스트를 갱신하여 TMP 메시 재생성 비용을 줄인다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResourceHUD : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════════
        // 상수
        // ══════════════════════════════════════════════════════════════════════

        #region ── 상수 ──

        // TODO: 기획팀 확인 필요 — 현재 0.5초 폴링, 너무 빠르거나 느리면 조정 필요
        private const float REFRESH_INTERVAL = 0.5f; // 자원 HUD 갱신 주기 (초)

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Serialized Fields (Inspector)
        // ══════════════════════════════════════════════════════════════════════

        #region ── Serialized Fields ──

        [Tooltip("나무(Wood) 재고 표시 텍스트.")]
        [SerializeField] private TMP_Text _woodText;

        [Tooltip("돌(Stone) 재고 표시 텍스트.")]
        [SerializeField] private TMP_Text _stoneText;

        [Tooltip("생 식량(RawFood) 재고 표시 텍스트.")]
        [SerializeField] private TMP_Text _rawFoodText;

        [Tooltip("조리 식량(CookedFood) 재고 표시 텍스트.")]
        [SerializeField] private TMP_Text _cookedFoodText;

        [Tooltip("철(Iron) 재고 표시 텍스트.")]
        [SerializeField] private TMP_Text _ironText;

        [Tooltip("구리(Copper) 재고 표시 텍스트.")]
        [SerializeField] private TMP_Text _copperText;

        [Tooltip("은(Silver) 재고 표시 텍스트.")]
        [SerializeField] private TMP_Text _silverText;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private Fields — 이전 값 캐시 (변경 감지용)
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private Fields ──

        // ── 이전 프레임 자원 값 캐시 ─────────────────────────────────────────
        // 자원 값이 실제로 변경되었을 때만 TMP SetText를 호출한다.
        // -1: 초기화되지 않은 상태 (첫 프레임 강제 갱신용)
        private float _prevWood       = -1f;
        private float _prevStone      = -1f;
        private float _prevRawFood    = -1f;
        private float _prevCookedFood = -1f;
        private float _prevIron       = -1f;
        private float _prevCopper     = -1f;
        private float _prevSilver     = -1f;

        // WaitForSeconds 객체 미리 할당 — 코루틴 루프마다 GC 할당을 방지한다
        // 코루틴 수명 관리는 HUDManager가 담당한다 (StartCoroutine/StopCoroutine).
        private WaitForSeconds _waitForSeconds;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Unity 생명주기
        // ══════════════════════════════════════════════════════════════════════

        #region ── Unity 생명주기 ──

        /// <summary>
        /// WaitForSeconds 객체를 미리 생성하여 GC 할당을 방지한다.
        /// </summary>
        private void Awake()
        {
            // GC 최적화: WaitForSeconds를 매 코루틴 루프마다 new로 생성하지 않고
            // Awake에서 한 번만 생성하여 재사용한다.
            _waitForSeconds = new WaitForSeconds(REFRESH_INTERVAL);
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // 공개 메서드
        // ══════════════════════════════════════════════════════════════════════

        #region ── 공개 메서드 ──

        /// <summary>
        /// 0.5초마다 AuthoritativeWorldState를 폴링하여 자원 재고를 갱신하는 코루틴.
        /// HUDManager.Start()에서 StartCoroutine(resourceHUD.RefreshCoroutine())으로 시작한다.
        ///
        /// 갱신 최적화:
        ///   이전 값과 현재 값을 비교하여 변경된 자원만 TMP SetText를 호출한다.
        /// </summary>
        public IEnumerator RefreshCoroutine()
        {
            while (true)
            {
                yield return _waitForSeconds;

                AuthoritativeWorldState ws = AuthoritativeWorldState.Instance;
                if (ws == null) continue; // 초기화 전 → 건너뜀

                // ── 변경 감지 후 갱신 (변경된 항목만 SetText 호출) ────────────
                UpdateTextIfChanged(ref _prevWood,       ws.WoodStock,       _woodText,       "나무");
                UpdateTextIfChanged(ref _prevStone,      ws.StoneStock,      _stoneText,      "돌");
                UpdateTextIfChanged(ref _prevRawFood,    ws.RawFoodStock,    _rawFoodText,    "생식량");
                UpdateTextIfChanged(ref _prevCookedFood, ws.CookedFoodStock, _cookedFoodText, "조리식량");
                UpdateTextIfChanged(ref _prevIron,       ws.IronStock,       _ironText,       "철");
                UpdateTextIfChanged(ref _prevCopper,     ws.CopperStock,     _copperText,     "구리");
                UpdateTextIfChanged(ref _prevSilver,     ws.SilverStock,     _silverText,     "은");
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private 헬퍼 메서드
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private 헬퍼 메서드 ──

        /// <summary>
        /// 이전 값과 현재 값을 비교하여 변경된 경우에만 TMP_Text를 갱신한다.
        /// </summary>
        private void UpdateTextIfChanged(
            ref float prevValue,
            float currentValue,
            TMP_Text textComponent,
            string label)
        {
            if (textComponent == null) return;

            // 부동소수점 비교: 0.01 미만 차이는 무시 (텍스트 깜빡임 방지)
            if (Mathf.Abs(currentValue - prevValue) < 0.01f) return;

            prevValue = currentValue;

            // 표시 형식: "나무: 42" (소수점 없이 정수 표시)
            textComponent.SetText($"{label}: {currentValue:F0}");
        }

        #endregion
    }
}
