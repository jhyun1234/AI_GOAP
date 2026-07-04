/// <summary>
/// HUDManager.cs - UI HUD 중앙 관리자
///
/// 역할(Role): PlayerInputController의 이벤트를 구독하여 각 UI 패널을 켜고 끈다.
///             ResourceHUD, VillagerStatusPanel, BuildingQueuePanel의 코루틴을 시작/정지한다.
///             ShowToast()를 외부에서 호출하여 RefusalBubble에 위임한다.
///             GameManager.OnOrderRefusedEvent를 구독하여 거부 메시지를 표시한다.
///
/// 사용법(Usage):
///   1. 씬 Canvas 아래 빈 GameObject("HUDManager")에 부착한다.
///   2. Inspector에서 7개 컴포넌트와 PlayerInputController를 연결한다.
///
/// 의존성(Dependencies):
///   - PlayerInputController.cs (이벤트 구독)
///   - GameManager.cs (OnOrderRefusedEvent 구독)
///   - ResourceHUD, VillagerStatusPanel, BuildingOrderPanel,
///     BuildingQueuePanel, RefusalBubble, GOAPDebugOverlay
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-28
/// </summary>

using System;
using UnityEngine;
using AIVillage.AI;
using AIVillage.Core;

namespace AIVillage.UI
{
    /// <summary>
    /// 씬 내 모든 HUD 패널을 중앙에서 제어하는 싱글턴.
    /// PlayerInputController 이벤트와 GameManager 이벤트를 연결한다.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    [DisallowMultipleComponent]
    public sealed class HUDManager : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════════
        // 싱글턴
        // ══════════════════════════════════════════════════════════════════════

        #region ── 싱글턴 ──

        /// <summary>
        /// 씬 내 유일한 HUDManager 인스턴스.
        /// PlayerInputController, RefusalBubble 등이 ShowToast()를 호출할 때 사용한다.
        /// </summary>
        public static HUDManager Instance { get; private set; }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Serialized Fields (Inspector)
        // ══════════════════════════════════════════════════════════════════════

        #region ── Serialized Fields ──

        [Tooltip("자원 재고(Wood/Stone/Food/Iron/Copper)를 표시하는 HUD 패널 컴포넌트.")]
        [SerializeField] private ResourceHUD _resourceHUD;

        [Tooltip("[미사용] VillagerStatusPanel은 VillagerOverviewPanel로 통합됨. 씬에서 비활성화 가능.")]
        [SerializeField] private VillagerStatusPanel _villagerStatusPanel;

        [Tooltip("건설 버튼 6개를 보유한 패널.")]
        [SerializeField] private BuildingOrderPanel _buildingOrderPanel;

        [Tooltip("건설 대기열 진행 상황을 표시하는 패널.")]
        [SerializeField] private BuildingQueuePanel _buildingQueuePanel;

        [Tooltip("주민 거부/시스템 토스트 메시지를 표시하는 컴포넌트.")]
        [SerializeField] private RefusalBubble _refusalBubble;

        [Tooltip("GOAP 디버그 정보를 표시하는 오버레이 (에디터/개발 빌드 전용).")]
        [SerializeField] private GOAPDebugOverlay _debugOverlay;

        [Tooltip("게임 일수·계절·생존 주민 수를 상시 표시하는 HUD 패널.")]
        [SerializeField] private GameInfoHUD _gameInfoHUD;

        [Tooltip("모든 주민의 FSM 상태를 한눈에 표시하는 디버그 패널 (에디터/개발 빌드 전용).")]
        [SerializeField] private VillagerOverviewPanel _villagerOverviewPanel;

        [Tooltip("주민 선택 이벤트를 발행하는 입력 컨트롤러.")]
        [SerializeField] private PlayerInputController _inputController;

        [Tooltip("Town Hall 해금 후 표시되는 주민 모집 패널. " +
                 "씬에서 초기에 비활성화한 상태로 배치한다. " +
                 "TownHallBuilt 감지 시 HUDManager가 gameObject.SetActive(true)를 호출한다.")]
        [SerializeField] private RecruitmentPanel _recruitmentPanel;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private Fields
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private Fields ──

        // ResourceHUD 코루틴 핸들 (OnDestroy에서 StopCoroutine용)
        private Coroutine _resourceHUDCoroutine;

        // [15단계] TownHall 완성 감지 폴링 코루틴 핸들
        private Coroutine _townHallWatchCoroutine;

        // BuildingQueuePanel 코루틴 핸들
        private Coroutine _buildingQueueCoroutine;

        // GameInfoHUD 코루틴 핸들
        private Coroutine _gameInfoCoroutine;

        // VillagerOverviewPanel 코루틴 핸들
        private Coroutine _villagerOverviewCoroutine;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Unity 생명주기
        // ══════════════════════════════════════════════════════════════════════

        #region ── Unity 생명주기 ──

        /// <summary>
        /// 싱글턴 설정. Awake에서 중복 인스턴스를 Destroy한다.
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[HUDManager] 중복 인스턴스 감지. 이 인스턴스를 Destroy합니다.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        /// <summary>
        /// 이벤트 구독 및 UI 코루틴 시작.
        /// Start()에서 수행하는 이유: Awake 시점에 PlayerInputController/GameManager가
        /// 아직 초기화 중일 수 있으므로 Start로 연기한다.
        /// </summary>
        private void Start()
        {
            // ── PlayerInputController 이벤트 구독 ──────────────────────────────
            if (_inputController != null)
            {
                _inputController.OnVillagerSelected   += HandleVillagerSelected;
                _inputController.OnVillagerDeselected += HandleVillagerDeselected;
            }
            else
            {
                Debug.LogWarning("[HUDManager] Start: _inputController가 null입니다. " +
                                 "Inspector에서 PlayerInputController를 연결하세요.");
            }

            // ── GameManager.OnOrderRefusedEvent 구독 ──────────────────────────
            // GameManager는 DefaultExecutionOrder(-80)이므로 Start 시점에 Instance가 보장된다.
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnOrderRefusedEvent += HandleOrderRefused;
            }
            else
            {
                Debug.LogWarning("[HUDManager] Start: GameManager.Instance가 null입니다. " +
                                 "씬에 GameManager가 있는지 확인하세요.");
            }

            // ── 항상 표시되는 패널 코루틴 시작 ──────────────────────────────────
            StartAlwaysOnCoroutines();

            // ── [15단계] RecruitmentPanel TownHall 감지 폴링 시작 ─────────────────
            // TownHallBuilt가 true가 되는 순간 패널을 활성화하고 코루틴을 종료한다.
            if (_recruitmentPanel != null)
            {
                _recruitmentPanel.gameObject.SetActive(false); // 초기 숨김 보장
                _townHallWatchCoroutine = StartCoroutine(WatchTownHallCoroutine());
            }

            // ── GOAPDebugOverlay 초기 대상 없음 ──────────────────────────────────
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_debugOverlay != null)
            {
                _debugOverlay.SetTarget(null);
            }
#endif
        }

        /// <summary>
        /// 이벤트 구독 해제 및 코루틴 정리.
        /// 씬 전환이나 GameObject Destroy 시 메모리 누수를 방지한다.
        /// </summary>
        private void OnDestroy()
        {
            if (_inputController != null)
            {
                _inputController.OnVillagerSelected   -= HandleVillagerSelected;
                _inputController.OnVillagerDeselected -= HandleVillagerDeselected;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnOrderRefusedEvent -= HandleOrderRefused;
            }

            // 코루틴 정리 — 고아 코루틴이 씬 전환 후에도 실행되는 것을 방지
            if (_resourceHUDCoroutine != null)
            {
                StopCoroutine(_resourceHUDCoroutine);
                _resourceHUDCoroutine = null;
            }

            if (_buildingQueueCoroutine != null)
            {
                StopCoroutine(_buildingQueueCoroutine);
                _buildingQueueCoroutine = null;
            }

            if (_gameInfoCoroutine != null)
            {
                StopCoroutine(_gameInfoCoroutine);
                _gameInfoCoroutine = null;
            }

            if (_villagerOverviewCoroutine != null)
            {
                StopCoroutine(_villagerOverviewCoroutine);
                _villagerOverviewCoroutine = null;
            }

            if (_townHallWatchCoroutine != null)
            {
                StopCoroutine(_townHallWatchCoroutine);
                _townHallWatchCoroutine = null;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // 공개 메서드
        // ══════════════════════════════════════════════════════════════════════

        #region ── 공개 메서드 ──

        /// <summary>
        /// 토스트 메시지를 화면에 표시한다.
        /// 내부적으로 RefusalBubble.EnqueueToast()에 위임한다.
        /// RefusalBubble이 null이면 Debug.Log로만 출력한다.
        /// </summary>
        /// <param name="message">표시할 메시지 문자열.</param>
        public void ShowToast(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            if (_refusalBubble != null)
            {
                _refusalBubble.EnqueueToast(message);
            }
            else
            {
                // RefusalBubble 미연결 시 콘솔로 폴백
                Debug.Log($"[HUDManager] Toast (RefusalBubble 없음): {message}");
            }
        }

        /// <summary>
        /// AI 주민의 사고 말풍선을 토스트로 표시한다 (7장 Level 3 플랜 가시화).
        /// 거부 메시지와 동일한 토스트 채널을 공유하며, 주민 이름 접두사로 구분한다.
        /// </summary>
        public void ShowAIThought(string villagerName, string thought)
        {
            ShowToast($"[{villagerName}] {thought}");
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // 이벤트 핸들러
        // ══════════════════════════════════════════════════════════════════════

        #region ── 이벤트 핸들러 ──

        /// <summary>
        /// PlayerInputController.OnVillagerSelected 핸들러.
        /// VillagerOverviewPanel 상단 상세 블록에 선택된 FSM을 설정한다.
        /// </summary>
        private void HandleVillagerSelected(VillagerFSM fsm)
        {
            if (fsm == null) return;

            if (_villagerOverviewPanel != null)
                _villagerOverviewPanel.SetSelectedVillager(fsm);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_debugOverlay != null)
                _debugOverlay.SetTarget(fsm);
#endif
        }

        /// <summary>
        /// PlayerInputController.OnVillagerDeselected 핸들러.
        /// </summary>
        private void HandleVillagerDeselected()
        {
            if (_villagerOverviewPanel != null)
                _villagerOverviewPanel.SetSelectedVillager(null);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_debugOverlay != null)
                _debugOverlay.SetTarget(null);
#endif
        }

        /// <summary>
        /// GameManager.OnOrderRefusedEvent 핸들러.
        /// 거부 메시지를 RefusalBubble.HandleRefusal()에 위임한다.
        /// </summary>
        private void HandleOrderRefused(MessageBus.OrderRefusedPayload payload)
        {
            if (_refusalBubble != null)
            {
                _refusalBubble.HandleRefusal(payload);
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private 헬퍼 메서드
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private 헬퍼 메서드 ──

        /// <summary>
        /// 항상 활성화되어야 하는 패널의 코루틴을 시작한다.
        /// ResourceHUD(0.5초)와 BuildingQueuePanel(0.3초)이 해당된다.
        /// </summary>
        private void StartAlwaysOnCoroutines()
        {
            if (_resourceHUD != null)
            {
                _resourceHUDCoroutine = StartCoroutine(_resourceHUD.RefreshCoroutine());
            }
            else
            {
                Debug.LogWarning("[HUDManager] StartAlwaysOnCoroutines: _resourceHUD가 null입니다. " +
                                 "Inspector에서 ResourceHUD를 연결하세요.");
            }

            if (_buildingQueuePanel != null)
            {
                _buildingQueueCoroutine = StartCoroutine(_buildingQueuePanel.RefreshCoroutine());
            }
            else
            {
                Debug.LogWarning("[HUDManager] StartAlwaysOnCoroutines: _buildingQueuePanel이 null입니다.");
            }

            if (_gameInfoHUD != null)
            {
                _gameInfoCoroutine = StartCoroutine(_gameInfoHUD.RefreshCoroutine());
            }

            // VillagerOverviewPanel 항상 활성 — 선택 주민 상세 + 전체 목록 표시
            if (_villagerOverviewPanel != null)
            {
                _villagerOverviewPanel.gameObject.SetActive(true);
                _villagerOverviewCoroutine = StartCoroutine(_villagerOverviewPanel.RefreshCoroutine());
            }
        }

        /// <summary>
        /// [15단계] 0.5초마다 AuthoritativeWorldState.TownHallBuilt를 확인한다.
        /// true가 되면 RecruitmentPanel을 활성화하고 코루틴을 스스로 종료한다.
        /// 한 번 활성화하면 이후 다시 비활성화하지 않으므로 yield break로 루프를 탈출한다.
        /// </summary>
        private System.Collections.IEnumerator WatchTownHallCoroutine()
        {
            WaitForSeconds wait = new WaitForSeconds(0.5f);

            while (true)
            {
                yield return wait;

                AIVillage.Core.AuthoritativeWorldState ws = AIVillage.Core.AuthoritativeWorldState.Instance;
                if (ws == null) continue;

                if (ws.TownHallBuilt)
                {
                    if (_recruitmentPanel != null)
                    {
                        _recruitmentPanel.gameObject.SetActive(true);
                        Debug.Log("[HUDManager] TownHall 완성 감지 — RecruitmentPanel 활성화.");
                    }

                    _townHallWatchCoroutine = null;
                    yield break; // 한 번 활성화하면 폴링 불필요
                }
            }
        }

        #endregion
    }
}
