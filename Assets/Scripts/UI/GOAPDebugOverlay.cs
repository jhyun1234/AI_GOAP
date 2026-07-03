/// <summary>
/// GOAPDebugOverlay.cs - GOAP AI 상태 디버그 오버레이
///
/// 역할(Role): 선택된 주민의 Goal/Action/FSMState/CombatMentalState를
///             0.1초마다 TMP_Text에 표시한다.
///             UNITY_EDITOR 또는 DEVELOPMENT_BUILD에서만 컴파일된다.
///
/// 사용법(Usage):
///   1. Canvas 아래 GOAPDebugOverlay Panel GameObject에 부착한다.
///   2. Inspector에서 TMP_Text 4개를 연결한다.
///   3. HUDManager.HandleVillagerSelected()에서 SetTarget(fsm)을 호출한다.
///   4. 릴리즈 빌드에서는 자동으로 비활성화된다.
///
/// 의존성(Dependencies): VillagerFSM.cs, TextMeshPro
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-28
/// </summary>

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections;
using UnityEngine;
using TMPro;
using AIVillage.AI;

namespace AIVillage.UI
{
    /// <summary>
    /// GOAP AI 상태 디버그 정보를 화면에 표시하는 오버레이.
    /// 에디터 및 개발 빌드에서만 활성화된다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GOAPDebugOverlay : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════════
        // 상수
        // ══════════════════════════════════════════════════════════════════════

        #region ── 상수 ──

        private const float REFRESH_INTERVAL = 0.1f; // 디버그 오버레이 갱신 주기 (초)

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Serialized Fields (Inspector)
        // ══════════════════════════════════════════════════════════════════════

        #region ── Serialized Fields ──

        [Tooltip("현재 Goal ID를 표시하는 TMP_Text.")]
        [SerializeField] private TMP_Text _goalText;

        [Tooltip("현재 Action ID를 표시하는 TMP_Text.")]
        [SerializeField] private TMP_Text _actionText;

        [Tooltip("현재 FSM 상태(VillagerState)를 표시하는 TMP_Text.")]
        [SerializeField] private TMP_Text _fsmStateText;

        [Tooltip("현재 전투 정신 상태(CombatMentalState)를 표시하는 TMP_Text.")]
        [SerializeField] private TMP_Text _mentalStateText;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private Fields
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private Fields ──

        private VillagerFSM _targetFSM;
        private WaitForSeconds _waitForSeconds;
        private Coroutine _refreshCoroutine;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Unity 생명주기
        // ══════════════════════════════════════════════════════════════════════

        #region ── Unity 생명주기 ──

        private void Awake()
        {
            _waitForSeconds = new WaitForSeconds(REFRESH_INTERVAL);
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            StopRefreshCoroutine();
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // 공개 메서드
        // ══════════════════════════════════════════════════════════════════════

        #region ── 공개 메서드 ──

        /// <summary>
        /// 디버그 표시 대상 VillagerFSM을 설정한다.
        /// null을 전달하면 오버레이를 비활성화한다.
        /// </summary>
        public void SetTarget(VillagerFSM fsm)
        {
            StopRefreshCoroutine();

            _targetFSM = fsm;

            if (_targetFSM == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            // StartCoroutine은 OnEnable에서 처리 — 부모가 inactive인 경우 대비
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════════════════════

        #region ── Unity 라이프사이클 ──

        private void OnEnable()
        {
            if (_targetFSM != null && _refreshCoroutine == null)
                _refreshCoroutine = StartCoroutine(RefreshCoroutine());
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // 코루틴
        // ══════════════════════════════════════════════════════════════════════

        #region ── 코루틴 ──

        /// <summary>
        /// 0.1초마다 _targetFSM의 Brain에서 디버그 정보를 읽어 TMP_Text를 갱신한다.
        /// </summary>
        private IEnumerator RefreshCoroutine()
        {
            while (_targetFSM != null)
            {
                yield return _waitForSeconds;

                if (_targetFSM == null) break;

                VillagerBrain brain = _targetFSM.Brain;
                if (brain == null) break;

                SetTextSafe(_goalText,        $"Goal: {brain.CurrentGoalId ?? "없음"}");
                SetTextSafe(_actionText,      $"Action: {brain.CurrentActionId ?? "없음"}");
                SetTextSafe(_fsmStateText,    $"FSM: {brain.FSMState}");
                SetTextSafe(_mentalStateText, $"정신: {brain.CombatMentalState}");
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private 헬퍼
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private 헬퍼 ──

        private void StopRefreshCoroutine()
        {
            if (_refreshCoroutine != null)
            {
                StopCoroutine(_refreshCoroutine);
                _refreshCoroutine = null;
            }
        }

        private static void SetTextSafe(TMP_Text text, string value)
        {
            if (text == null) return;
            text.SetText(value);
        }

        #endregion
    }
}

#endif
