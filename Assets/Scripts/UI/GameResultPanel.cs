/// <summary>
/// GameResultPanel.cs - Win/Lose 게임 결과 화면 표시 패널
///
/// 역할(Role): GameManager.OnGameResultEvent를 구독하여 승리/패배 결과를 화면에 표시한다.
///             결과 종류에 따라 제목/설명 텍스트를 설정하고 _panelRoot를 활성화한다.
///             재시도 버튼(동일 씬 재로드)과 메인 메뉴 버튼을 처리한다.
///
/// 사용법(Usage):
///   1. Canvas 아래에 결과 화면용 Panel GameObject를 만들고 이 컴포넌트를 부착한다.
///   2. Inspector에서 _panelRoot, _titleText, _descriptionText, _retryButton, _mainMenuButton을 연결한다.
///   3. _panelRoot는 평소 비활성(SetActive(false)) 상태로 둔다 — HandleGameResult 호출 시 활성화된다.
///   4. GameManager는 DefaultExecutionOrder(-80)이므로 Start 시점에 Instance가 보장되지만,
///      Awake에서도 TrySubscribe()를 시도하여 가능한 빨리 구독을 완료한다.
///
/// 의존성(Dependencies):
///   - GameManager.cs (OnGameResultEvent 구독)
///   - TMPro (TMP_Text)
///   - UnityEngine.UI (Button)
///   - UnityEngine.SceneManagement (씬 재로드)
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-28
/// </summary>

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using AIVillage.Core;

namespace AIVillage.UI
{
    /// <summary>
    /// Win/Lose 결과가 확정됐을 때 결과 패널을 표시하는 UI 컴포넌트.
    /// GameManager.OnGameResultEvent를 구독한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameResultPanel : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════════
        // Serialized Fields (Inspector)
        // ══════════════════════════════════════════════════════════════════════

        #region ── Serialized Fields ──

        [Tooltip("결과 화면 전체를 감싸는 루트 Panel GameObject. " +
                 "평소 비활성 상태로 두고, 게임 결과 시 SetActive(true)가 호출된다.")]
        [SerializeField] private GameObject _panelRoot;

        [Tooltip("결과 화면 제목 텍스트 (TMP_Text). " +
                 "예: '번영 달성!', '생존 달성!', '전멸...', '패배'")]
        [SerializeField] private TMP_Text _titleText;

        [Tooltip("결과 화면 설명 텍스트 (TMP_Text). " +
                 "결과 종류에 따라 상세 설명 문구가 설정된다.")]
        [SerializeField] private TMP_Text _descriptionText;

        [Tooltip("재시도 버튼. 클릭 시 현재 활성 씬을 재로드한다.")]
        [SerializeField] private Button _retryButton;

        [Tooltip("메인 메뉴 버튼. 현재는 메인 메뉴 씬 미구현으로 동일 씬을 재로드한다. " +
                 "TODO: 메인 메뉴 씬 구현 후 SceneManager.LoadScene(\"MainMenu\")로 교체 필요.")]
        [SerializeField] private Button _mainMenuButton;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private Fields
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private Fields ──

        // [PR Fix]: W-01 — 이벤트 구독 안전성 개선: 중복 구독 방지 플래그 추가
        // TrySubscribe()가 Awake와 Start 두 곳에서 호출되므로, 이미 구독했으면 건너뛴다.
        private bool _subscribed = false;

        // [PR Fix]: W-01 — OnDestroy 시 GameManager.Instance가 null이어도 안전하게
        // 구독 해제할 수 있도록 구독 당시 참조를 직접 저장한다.
        // (씬 전환 도중 Instance가 먼저 null이 되는 경합 상황을 방지)
        private GameManager _gameManagerRef;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Unity 생명주기
        // ══════════════════════════════════════════════════════════════════════

        #region ── Unity 생명주기 ──

        /// <summary>
        /// 이 컴포넌트가 활성화될 때 Unity가 가장 먼저 호출한다.
        /// GameManager가 ExecutionOrder(-80)으로 이 컴포넌트보다 먼저 Awake를 수행하므로,
        /// Awake 시점에 Instance가 이미 존재할 가능성이 높다. 가능하면 여기서 구독을 완료한다.
        /// </summary>
        // [PR Fix]: W-01 — Awake에서 TrySubscribe() 첫 번째 시도 (이중 시도 패턴)
        private void Awake()
        {
            TrySubscribe();
        }

        /// <summary>
        /// Awake 이후, 첫 프레임 Update 직전에 Unity가 호출한다.
        /// Awake에서 구독에 실패했을 경우 재시도한다. 이미 구독됐으면 TrySubscribe() 내부에서 즉시 반환한다.
        /// 버튼 리스너 등록은 이 시점에 수행한다.
        /// </summary>
        private void Start()
        {
            // [PR Fix]: W-01 — Start에서 TrySubscribe() 두 번째 시도
            // Awake 시점에 GameManager가 아직 초기화되지 않았던 경우를 대비한 안전망이다.
            TrySubscribe();

            // ── 버튼 리스너 등록 ──────────────────────────────────────────────
            // Inspector 연결 누락 시 기능 없이 경고만 출력하여 NullReferenceException을 방지한다.
            if (_retryButton != null)
            {
                _retryButton.onClick.AddListener(OnRetryClicked);
            }
            else
            {
                Debug.LogWarning("[GameResultPanel] Start: _retryButton이 null입니다. " +
                                 "Inspector에서 Retry Button을 연결하세요.");
            }

            if (_mainMenuButton != null)
            {
                _mainMenuButton.onClick.AddListener(OnMainMenuClicked);
            }
            else
            {
                Debug.LogWarning("[GameResultPanel] Start: _mainMenuButton이 null입니다. " +
                                 "Inspector에서 Main Menu Button을 연결하세요.");
            }

            // ── 패널 초기 비활성화 확인 ──────────────────────────────────────
            // Start 시점에 패널이 활성화되어 있으면 경고 후 비활성화한다.
            // (씬 저장 시 실수로 활성화된 채로 저장되는 경우를 방지)
            if (_panelRoot != null && _panelRoot.activeSelf)
            {
                Debug.LogWarning("[GameResultPanel] Start: _panelRoot가 활성화된 상태로 시작합니다. " +
                                 "씬 저장 전 비활성화(SetActive(false))하는 것을 권장합니다. 자동으로 비활성화합니다.");
                _panelRoot.SetActive(false);
            }
        }

        /// <summary>
        /// 이벤트 구독 해제 및 버튼 리스너 제거.
        /// 씬 전환이나 GameObject Destroy 시 메모리 누수를 방지한다.
        /// </summary>
        private void OnDestroy()
        {
            // [PR Fix]: W-01 — Instance가 아닌 _gameManagerRef로 구독 해제
            // 씬 전환 도중 GameManager.Instance가 먼저 null이 되어도 안전하게 구독 해제가 가능하다.
            // _subscribed가 false이면 구독 자체가 없으므로 건너뛴다.
            if (_subscribed && _gameManagerRef != null)
            {
                _gameManagerRef.OnGameResultEvent -= HandleGameResult;
                _subscribed = false;
                _gameManagerRef = null;
            }

            // ── 버튼 리스너 제거 ──────────────────────────────────────────────
            if (_retryButton != null)
            {
                _retryButton.onClick.RemoveListener(OnRetryClicked);
            }

            if (_mainMenuButton != null)
            {
                _mainMenuButton.onClick.RemoveListener(OnMainMenuClicked);
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private 헬퍼
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private 헬퍼 ──

        /// <summary>
        /// GameManager.OnGameResultEvent 구독을 시도한다.
        /// 이미 구독됐으면 (_subscribed == true) 즉시 반환하여 중복 구독을 방지한다.
        /// GameManager.Instance가 null이면 LogError를 출력하고 반환한다.
        /// Awake()와 Start() 모두에서 호출되는 이중 시도 패턴을 지원한다.
        /// </summary>
        // [PR Fix]: W-01 — TrySubscribe() 메서드 신설 (Awake+Start 이중 시도, _subscribed 플래그로 중복 방지)
        private void TrySubscribe()
        {
            // 이미 구독 완료된 경우 재진입 방지
            if (_subscribed) return;

            // GameManager 참조를 필드에 저장 (OnDestroy에서 안전 해제에 사용)
            _gameManagerRef = GameManager.Instance;

            if (_gameManagerRef != null)
            {
                _gameManagerRef.OnGameResultEvent += HandleGameResult;
                _subscribed = true;
            }
            else
            {
                // [PR Fix]: W-01 — null 시 LogWarning → LogError 격상 (Start에서 null은 씬 구성 오류)
                Debug.LogError("[GameResultPanel] TrySubscribe: GameManager.Instance가 null입니다. " +
                               "결과 화면이 표시되지 않습니다. 씬에 GameManager 컴포넌트가 있는지 확인하세요.");
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // 이벤트 핸들러
        // ══════════════════════════════════════════════════════════════════════

        #region ── 이벤트 핸들러 ──

        /// <summary>
        /// GameManager.OnGameResultEvent 핸들러.
        /// 결과 종류에 따라 제목/설명 텍스트를 설정하고 패널을 활성화한다.
        /// </summary>
        /// <param name="result">GameManager에서 확정된 게임 결과 타입.</param>
        private void HandleGameResult(GameManager.GameResultType result)
        {
            // ── 텍스트 결정 ───────────────────────────────────────────────────
            string title       = string.Empty;
            string description = string.Empty;

            switch (result)
            {
                case GameManager.GameResultType.Win3_Prosperity:
                    // 최종 승리: Silver Citadel 완성
                    title       = "번영 달성!";
                    description = "Silver Citadel이 완성됐습니다. 마을이 번영했습니다!";
                    break;

                case GameManager.GameResultType.Win1_Survival:
                    // 기본 승리: Town Hall + 주민 5명 + 30일 생존
                    title       = "생존 달성!";
                    // [PR Fix]: S-02 — description 텍스트에 "주민 5명 이상" 조건 명시
                    description = "Town Hall을 건설하고 주민 5명 이상으로 30일을 버텼습니다!";
                    break;

                case GameManager.GameResultType.Lose_Annihilated:
                    // 패배: 모든 주민 사망
                    title       = "전멸...";
                    description = "모든 주민이 사망했습니다.";
                    break;

                case GameManager.GameResultType.Lose_TownHallFallen:
                    // 패배: Town Hall 파괴 + 소수 생존
                    title       = "패배";
                    description = "Town Hall이 무너지고 마을이 소멸했습니다.";
                    break;

                default:
                    // [PR Fix]: S-01 — None 또는 알 수 없는 결과 — LogWarning → LogError 격상
                    // GameResultType에 새 값이 추가됐는데 이 switch가 갱신되지 않은 경우 코드 버그이므로 Error로 처리한다.
                    Debug.LogError($"[GameResultPanel] HandleGameResult: 처리되지 않은 결과 타입 '{result}'. " +
                                   $"GameResultType에 새 값이 추가된 경우 이 switch를 업데이트하세요.");
                    return;
            }

            // ── 텍스트 적용 ───────────────────────────────────────────────────
            if (_titleText != null)
            {
                _titleText.text = title;
            }
            else
            {
                Debug.LogWarning("[GameResultPanel] HandleGameResult: _titleText가 null입니다. " +
                                 "Inspector에서 Title Text(TMP_Text)를 연결하세요.");
            }

            if (_descriptionText != null)
            {
                _descriptionText.text = description;
            }
            else
            {
                Debug.LogWarning("[GameResultPanel] HandleGameResult: _descriptionText가 null입니다. " +
                                 "Inspector에서 Description Text(TMP_Text)를 연결하세요.");
            }

            // ── 패널 활성화 ───────────────────────────────────────────────────
            if (_panelRoot != null)
            {
                _panelRoot.SetActive(true);
            }
            else
            {
                Debug.LogError("[GameResultPanel] HandleGameResult: _panelRoot가 null입니다. " +
                               "Inspector에서 Panel Root를 연결하세요. 결과 화면을 표시할 수 없습니다.");
            }

            Debug.Log($"[GameResultPanel] 결과 화면 표시 — 제목: '{title}', 설명: '{description}'");
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // 버튼 핸들러
        // ══════════════════════════════════════════════════════════════════════

        #region ── 버튼 핸들러 ──

        /// <summary>
        /// 재시도 버튼 클릭 핸들러.
        /// 현재 활성 씬을 재로드하여 게임을 처음부터 시작한다.
        /// </summary>
        private void OnRetryClicked()
        {
            // 현재 씬의 이름을 가져와 동일 씬을 재로드한다.
            // SceneManager.GetActiveScene().name은 씬 이름을 반환한다 (경로 없이).
            string currentSceneName = SceneManager.GetActiveScene().name;
            Debug.Log($"[GameResultPanel] 재시도 버튼 클릭 — 씬 '{currentSceneName}' 재로드.");
            SceneManager.LoadScene(currentSceneName);
        }

        /// <summary>
        /// 메인 메뉴 버튼 클릭 핸들러.
        ///
        /// TODO: 기획팀 확인 필요 — 메인 메뉴 씬 구현 후 아래 코드를 교체한다.
        ///   현재: 동일 씬 재로드 (임시 구현)
        ///   목표: SceneManager.LoadScene("MainMenu") — 메인 메뉴 씬 이름 확정 필요
        /// </summary>
        private void OnMainMenuClicked()
        {
            // TODO: 기획팀 확인 필요 — 메인 메뉴 씬 이름 미확정. 현재 임시로 동일 씬 재로드.
            string currentSceneName = SceneManager.GetActiveScene().name;
            Debug.Log($"[GameResultPanel] 메인 메뉴 버튼 클릭 — " +
                      $"메인 메뉴 씬 미구현으로 씬 '{currentSceneName}'을 재로드합니다.");
            SceneManager.LoadScene(currentSceneName);
        }

        #endregion
    }
}
