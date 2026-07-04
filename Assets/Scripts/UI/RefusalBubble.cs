/// <summary>
/// RefusalBubble.cs - 명령 거부 및 토스트 알림 표시 컴포넌트
///
/// 역할(Role): GameManager.OnOrderRefusedEvent와 HUDManager.ShowToast()에서
///             호출된 메시지를 Queue에 적재하고, 2.5초씩 순서대로 표시한다.
///             동시에 여러 메시지가 와도 순서가 보장된다.
///
/// 사용법(Usage):
///   1. Canvas 아래 RefusalBubble Panel GameObject에 부착한다.
///   2. Inspector에서 _toastPanel과 _toastText를 연결한다.
///   3. HUDManager.Instance.ShowToast() 또는 HandleRefusal()로 외부에서 메시지를 요청한다.
///
/// 의존성(Dependencies): TextMeshPro, MessageBus.cs (OrderRefusedPayload)
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-28
/// </summary>

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using AIVillage.Core;
using AIVillage.AI;

namespace AIVillage.UI
{
    /// <summary>
    /// 명령 거부 및 시스템 알림을 토스트 방식으로 표시하는 컴포넌트.
    /// 최대 하나의 토스트만 동시 표시되며, 나머지는 Queue에서 대기한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RefusalBubble : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════════
        // 상수
        // ══════════════════════════════════════════════════════════════════════

        #region ── 상수 ──

        // TODO: 기획팀 확인 필요 — 현재 2.5초
        private const float TOAST_DISPLAY_DURATION = 2.5f;
        private const int   MAX_QUEUE_SIZE         = 5;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // 거부 메시지 매핑 딕셔너리
        // ══════════════════════════════════════════════════════════════════════

        #region ── 거부 메시지 딕셔너리 ──

        /// <summary>
        /// RefusalReasonCode를 UI 표시용 한국어 메시지로 변환한다.
        /// </summary>
        private static readonly Dictionary<RefusalReasonCode, string> REFUSAL_MESSAGES
            = new Dictionary<RefusalReasonCode, string>
            {
                { RefusalReasonCode.REFUSE_HUNGER,
                    "배가 너무 고파서 움직일 수 없습니다." },

                { RefusalReasonCode.REFUSE_INJURY,
                    "부상이 심해서 명령을 수행할 수 없습니다." },

                { RefusalReasonCode.REFUSE_FATIGUE,
                    "탈진 상태입니다. 먼저 휴식이 필요합니다." },

                { RefusalReasonCode.REFUSE_LOYALTY,
                    "당신의 명령을 따를 이유를 모르겠습니다." },

                { RefusalReasonCode.REFUSE_DANGER,
                    "적이 근처에 있습니다. 지금은 위험합니다." },

                { RefusalReasonCode.REFUSE_NO_TOOL,
                    "도구가 없어서 채집할 수 없습니다." },

                { RefusalReasonCode.REFUSE_INSUFFICIENT_RESOURCES,
                    "자원이 부족하여 건설할 수 없습니다." }
            };

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Serialized Fields (Inspector)
        // ══════════════════════════════════════════════════════════════════════

        #region ── Serialized Fields ──

        [Tooltip("토스트 메시지를 감싸는 패널 GameObject. 메시지 표시 시 SetActive(true).")]
        [SerializeField] private GameObject _toastPanel;

        [Tooltip("토스트 메시지 내용을 표시하는 TMP_Text.")]
        [SerializeField] private TMP_Text _toastText;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private Fields
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private Fields ──

        private readonly Queue<string> _toastQueue = new Queue<string>();
        private bool _isShowingToast = false;
        private Coroutine _toastCoroutine;
        private WaitForSeconds _waitForSeconds;
        private CanvasGroup _toastCanvasGroup;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Unity 생명주기
        // ══════════════════════════════════════════════════════════════════════

        #region ── Unity 생명주기 ──

        private void Awake()
        {
            _waitForSeconds = new WaitForSeconds(TOAST_DISPLAY_DURATION);

            if (_toastPanel != null)
            {
                // SetActive(false) 대신 CanvasGroup으로 숨김 처리.
                // SetActive는 RefusalBubble 자신의 GameObject가 ToastPanel일 때
                // 자기 자신을 비활성화해 이후 StartCoroutine을 막는 버그가 발생한다.
                _toastCanvasGroup = _toastPanel.GetComponent<CanvasGroup>();
                if (_toastCanvasGroup == null)
                    _toastCanvasGroup = _toastPanel.AddComponent<CanvasGroup>();
                HideToastPanel();
            }
        }

        private void OnDestroy()
        {
            if (_toastCoroutine != null)
            {
                StopCoroutine(_toastCoroutine);
                _toastCoroutine = null;
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // 공개 메서드
        // ══════════════════════════════════════════════════════════════════════

        #region ── 공개 메서드 ──

        /// <summary>
        /// OrderRefusedPayload를 받아 거부 이유 코드를 딕셔너리에서 조회한 뒤
        /// 토스트 큐에 메시지를 추가한다.
        ///
        /// 우선순위:
        ///   1. payload.RefusalMessage가 있으면 그대로 사용
        ///   2. REFUSAL_MESSAGES 딕셔너리에서 조회
        ///   3. 기본 메시지
        /// </summary>
        public void HandleRefusal(MessageBus.OrderRefusedPayload payload)
        {
            string message;

            if (!string.IsNullOrEmpty(payload.RefusalMessage))
            {
                message = payload.RefusalMessage;
            }
            else if (REFUSAL_MESSAGES.TryGetValue(payload.RefusalReasonCode, out string dictMessage))
            {
                message = dictMessage;
            }
            else
            {
                message = $"명령 거부 (코드: {payload.RefusalReasonCode})";
            }

            EnqueueToast(message);
        }

        /// <summary>
        /// 토스트 메시지를 큐에 추가한다.
        /// 큐가 MAX_QUEUE_SIZE를 초과하면 가장 오래된 메시지를 버린다.
        /// </summary>
        public void EnqueueToast(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            while (_toastQueue.Count >= MAX_QUEUE_SIZE)
            {
                _toastQueue.Dequeue();
            }

            _toastQueue.Enqueue(message);

            if (!_isShowingToast)
            {
                _toastCoroutine = StartCoroutine(ShowToastQueueCoroutine());
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // 코루틴
        // ══════════════════════════════════════════════════════════════════════

        #region ── 내부 헬퍼 ──

        private void ShowToastPanel()
        {
            if (_toastCanvasGroup == null) return;
            _toastCanvasGroup.alpha          = 1f;
            _toastCanvasGroup.interactable   = true;
            _toastCanvasGroup.blocksRaycasts = true;
        }

        private void HideToastPanel()
        {
            if (_toastCanvasGroup == null) return;
            _toastCanvasGroup.alpha          = 0f;
            _toastCanvasGroup.interactable   = false;
            _toastCanvasGroup.blocksRaycasts = false;
        }

        #endregion

        #region ── 코루틴 ──

        /// <summary>
        /// 큐에 있는 토스트 메시지를 2.5초씩 순서대로 표시하는 코루틴.
        /// </summary>
        private IEnumerator ShowToastQueueCoroutine()
        {
            _isShowingToast = true;

            while (_toastQueue.Count > 0)
            {
                string message = _toastQueue.Dequeue();

                if (_toastText != null)
                    _toastText.SetText(message);

                ShowToastPanel();

                yield return _waitForSeconds;
            }

            HideToastPanel();

            _isShowingToast = false;
            _toastCoroutine = null;
        }

        #endregion
    }
}
