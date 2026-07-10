/// <summary>
/// InvasionWarningIndicator.cs - F-B 침략 예고 배너 UI
///
/// 역할(Role): MessageBus.InvasionWarning을 구독하여 화면에 예고 배너를 띄운다.
///             Rumor(회색) → Confirmed(빨강) 스테이지별 배경색을 갱신하고,
///             매 프레임 잔여 게임 일수를 CeilToInt로 카운트다운한다.
///             D0(_expectedRaidDay 도달) 시 자동 소멸.
///             팩션명 미상(정찰 미완) + Rumor 조합에서만 텍스트 알파를 PingPong으로
///             깜빡여 정보 부족을 시각화한다(ADR-B4).
///
/// 사용법(Usage):
///   1. Canvas 하위 임의 위치에 GameObject를 배치한다(위치·크기·앵커 자유 — 스크립트 미조작).
///   2. 자식 TextMeshProUGUI 1개와 Image(배경) 1개를 두고 Inspector에 연결한다.
///   3. 활성 상태로 두면 OnEnable에서 자동 Subscribe. 시작 시 SetActive(false).
///
/// 의존성(Dependencies):
///   - MessageBus.cs (InvasionWarning, InvasionWarningPayload, INVASION_STAGE_*)
///   - GameManager.cs (GameTime — 카운트다운 계산)
///   - TextMeshPro
///
/// Author: F-B 실행명세서 FB-4
/// Last Updated: 2026-07-11
/// </summary>

using UnityEngine;
using AIVillage.Core;
using AIVillage.AI;

namespace AIVillage.UI
{
    /// <summary>
    /// 침략 예고 배너. Rumor/Confirmed 스테이지별 색상과 문구를 표시하고,
    /// 팩션명 미상 시 텍스트 깜빡임 효과로 불확실성을 시각화한다.
    /// 위치·크기는 사용자가 씬에서 자유롭게 조절한다(스크립트 Transform 미조작).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InvasionWarningIndicator : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════════
        // Serialized Fields
        // ══════════════════════════════════════════════════════════════════════

        #region ── Inspector ──

        [Tooltip("배너 텍스트. 스테이지·팩션명·잔여 일수를 표시한다.")]
        [SerializeField] private TMPro.TextMeshProUGUI _label;

        [Tooltip("배너 배경 이미지. Rumor=회색, Confirmed=빨강.")]
        [SerializeField] private UnityEngine.UI.Image _background;

        [Tooltip("미상의 세력 Rumor 표기일 때 텍스트 깜빡임 주기(초). 사용자 조절.")]
        [SerializeField] private float _blinkPeriodSec = 0.8f;

        [Tooltip("깜빡임 시 텍스트 최소 알파(0~1). 0=완전 사라짐, 1=깜빡임 없음.")]
        [SerializeField, Range(0f, 1f)] private float _blinkMinAlpha = 0.25f;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // 상수 — 배경 색상
        // ══════════════════════════════════════════════════════════════════════

        #region ── 상수 ──

        // Rumor: 흐린 회색 (약한 조짐)
        private static readonly Color COLOR_RUMOR     = new Color(0.53f, 0.53f, 0.53f, 0.85f);
        // Confirmed: 강한 빨강 (임박)
        private static readonly Color COLOR_CONFIRMED = new Color(0.87f, 0.20f, 0.20f, 0.90f);

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private 런타임 상태
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private Fields ──

        /// <summary>현재 배너가 활성(구독 이후 D0 이전) 상태인지 여부.</summary>
        private bool   _active              = false;
        /// <summary>예상 침략 게임일. Payload에서 캐시하며 D0 도달 판정 기준.</summary>
        private float  _expectedRaidDay     = -1f;
        /// <summary>현재 스테이지 ID(INVASION_STAGE_RUMOR / CONFIRMED).</summary>
        private string _stageId             = null;
        /// <summary>UI에 표시할 팩션 이름(정찰 미완이면 "미상의 세력").</summary>
        private string _factionDisplayName  = null;
        /// <summary>ADR-B4: 팩션명 미상 여부. true + Rumor 조합일 때만 텍스트 깜빡임 활성.</summary>
        private bool   _factionNameUnknown  = false;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Unity 생명주기
        // ══════════════════════════════════════════════════════════════════════

        #region ── Unity 생명주기 ──

        /// <summary>
        /// GameObject 활성화 시 MessageBus에 InvasionWarning 구독을 등록하고
        /// 배너 자신은 숨긴다(Warning 수신 시에만 표시).
        /// </summary>
        private void OnEnable()
        {
            if (MessageBus.Instance != null)
            {
                MessageBus.Instance.Subscribe(MessageType.InvasionWarning, OnInvasionWarning);
            }
            else
            {
                Debug.LogWarning("[InvasionWarningIndicator] OnEnable: MessageBus.Instance가 null입니다. " +
                                 "MessageBus Awake 이후에 이 컴포넌트가 활성화되도록 씬 배치를 확인하세요.");
            }
            SetVisible(false);
        }

        /// <summary>
        /// 씬 이탈·비활성화 시 구독을 반드시 해제하고 텍스트 알파를 복구한다.
        /// (다음 활성화 시 이전 깜빡임 상태가 남지 않도록.)
        /// </summary>
        private void OnDisable()
        {
            if (MessageBus.Instance != null)
            {
                MessageBus.Instance.Unsubscribe(MessageType.InvasionWarning, OnInvasionWarning);
            }
            RestoreLabelAlpha();
        }

        /// <summary>
        /// 매 프레임 잔여 일수 카운트다운을 갱신하고, 미상+Rumor 조건일 때 깜빡임을 적용한다.
        /// D0에 도달하면 자동으로 비활성화한다(FactionAI가 이 시점에 실제 침략을 개시).
        /// </summary>
        private void Update()
        {
            if (!_active) return;

            float now = (GameManager.Instance != null) ? GameManager.Instance.GameTime : 0f;
            float remain = _expectedRaidDay - now;

            if (remain <= 0f)
            {
                SetVisible(false);
                _active = false;
                RestoreLabelAlpha();
                return;
            }

            if (_label != null) _label.text = FormatLabel(remain);
            ApplyBlinkIfUnknown();
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // MessageBus 콜백
        // ══════════════════════════════════════════════════════════════════════

        #region ── 이벤트 핸들러 ──

        /// <summary>
        /// InvasionWarning 페이로드를 수신하여 스테이지 색상·표기명·타이머를 갱신한다.
        /// Rumor → Confirmed 전이 시에도 이 콜백이 두 번 호출되므로 상태를 매번 덮어쓴다.
        /// </summary>
        private void OnInvasionWarning(AIMessage msg)
        {
            if (!(msg.Payload is MessageBus.InvasionWarningPayload p))
            {
                Debug.LogWarning($"[InvasionWarningIndicator] Payload 언박싱 실패. Type={msg.Type}");
                return;
            }

            _stageId             = p.StageId;
            _expectedRaidDay     = p.ExpectedRaidDay;
            _factionNameUnknown  = !p.FactionNameKnown;
            _factionDisplayName  = p.FactionNameKnown ? FactionNameFromId(p.FactionId) : "미상의 세력";

            if (_background != null)
            {
                _background.color = (p.StageId == MessageBus.INVASION_STAGE_CONFIRMED)
                                    ? COLOR_CONFIRMED
                                    : COLOR_RUMOR;
            }

            _active = true;
            SetVisible(true);

            // Confirmed 진입 시 Rumor 상태에서 남은 잔여 알파를 즉시 1로 복구
            // (팩션명이 뒤늦게 확정된 케이스 대비, 임박 배너가 반투명으로 뜨는 것 방지)
            if (_stageId == MessageBus.INVASION_STAGE_CONFIRMED)
            {
                RestoreLabelAlpha();
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private 헬퍼
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private 헬퍼 ──

        /// <summary>
        /// 미상 + Rumor 조합에서만 텍스트 알파를 [_blinkMinAlpha, 1] 사이 PingPong으로 깜빡인다.
        /// 배경 알파는 절대 건드리지 않는다(사용자가 UI 위치를 놓치지 않도록).
        /// Time.unscaledTime을 사용해 게임 일시정지 상태에서도 깜빡임이 유지된다.
        /// </summary>
        private void ApplyBlinkIfUnknown()
        {
            if (_label == null) return;

            // Confirmed거나 팩션명 확정이면 알파 1 유지
            if (!_factionNameUnknown || _stageId == MessageBus.INVASION_STAGE_CONFIRMED)
            {
                RestoreLabelAlpha();
                return;
            }

            float period = Mathf.Max(0.01f, _blinkPeriodSec);
            float t = Mathf.PingPong(Time.unscaledTime / period, 1f);
            float a = Mathf.Lerp(_blinkMinAlpha, 1f, t);
            Color c = _label.color;
            c.a = a;
            _label.color = c;
        }

        /// <summary>텍스트 알파를 1로 복구(깜빡임 중단).</summary>
        private void RestoreLabelAlpha()
        {
            if (_label == null) return;
            Color c = _label.color;
            c.a = 1f;
            _label.color = c;
        }

        /// <summary>
        /// 스테이지별 배너 문구 조립.
        /// Rumor: "[팩션명] · 이상한 낌새 · 약 N일 후"
        /// Confirmed: "[팩션명] · 침략 임박 · 오늘"
        /// </summary>
        private string FormatLabel(float remainDays)
        {
            if (_stageId == MessageBus.INVASION_STAGE_CONFIRMED)
            {
                return $"[{_factionDisplayName}] · 침략 임박 · 오늘";
            }
            return $"[{_factionDisplayName}] · 이상한 낌새 · 약 {Mathf.CeilToInt(remainDays)}일 후";
        }

        /// <summary>GameObject 활성/비활성 전환 헬퍼.</summary>
        private void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// FactionAI에서 발행한 FactionId(문자열)를 사용자 표기용 한국어명으로 변환.
        /// FactionAI 상수 FACTION_FOREST/IRON/MERCHANT의 정수값과 정렬됨.
        /// </summary>
        private static string FactionNameFromId(string factionId)
        {
            switch (factionId)
            {
                case "0": return "숲의 부족";
                case "1": return "철의 도시";
                case "2": return "상인 연합";
                default:  return "알 수 없는 세력";
            }
        }

        #endregion
    }
}
