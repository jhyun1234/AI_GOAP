/// <summary>
/// BuildingOrderPanel.cs - 건설 명령 버튼 패널
///
/// 역할(Role): 건설 가능한 건물 6개의 버튼을 보유하며,
///             0.5초마다 자원 충족 여부와 건물 완성 여부를 확인하여
///             버튼의 interactable 상태를 갱신한다.
///             버튼 클릭 시 PlayerInputController.IssueBuildingOrder()를 호출한다.
///
/// 사용법(Usage):
///   1. Canvas 아래 BuildingOrderPanel GameObject에 부착한다.
///   2. Inspector에서 Button 6개와 PlayerInputController를 연결한다.
///
/// 의존성(Dependencies):
///   - PlayerInputController.cs (건설 명령 전달)
///   - AuthoritativeWorldState.cs (자원 충족 확인)
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-28
/// </summary>

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using AIVillage.Core;

namespace AIVillage.UI
{
    /// <summary>
    /// 건물 6개의 건설 버튼 패널.
    /// 자원/건물 조건에 따라 버튼 interactable을 자동 갱신한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingOrderPanel : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════════
        // 상수 — 건물별 자원 비용 (기획서 GDD v0.4 수치)
        // ══════════════════════════════════════════════════════════════════════

        #region ── 상수 ──

        private const float REFRESH_INTERVAL = 0.5f;
        // 건물 비용 상수는 BuildingCosts.cs에서 중앙 관리한다.

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Serialized Fields (Inspector)
        // ══════════════════════════════════════════════════════════════════════

        #region ── Serialized Fields ──

        [Tooltip("Campfire 건설 버튼. 비용: Wood 5")]
        [SerializeField] private Button _campfireButton;

        [Tooltip("House 건설 버튼. 비용: Wood 20, Stone 10")]
        [SerializeField] private Button _houseButton;

        [Tooltip("Storehouse 건설 버튼. 비용: Wood 15, Stone 5")]
        [SerializeField] private Button _storehouseButton;

        [Tooltip("TownHall 건설 버튼. 비용: Wood 35, Stone 30, Iron 6")]
        [SerializeField] private Button _townHallButton;

        [Tooltip("Forge 건설 버튼. 비용: Wood 20, Stone 20, Iron 15. TownHall 완성 후 활성화.")]
        [SerializeField] private Button _forgeButton;

        [Tooltip("Watchtower 건설 버튼. 비용: Wood 10, Stone 30, Iron 5. TownHall 완성 후 활성화.")]
        [SerializeField] private Button _watchtowerButton;

        [Tooltip("건설 명령을 전달할 PlayerInputController. Inspector에서 연결한다.")]
        [SerializeField] private PlayerInputController _inputController;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private Fields
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private Fields ──

        private WaitForSeconds _waitForSeconds;
        private Coroutine _refreshCoroutine;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Unity 생명주기
        // ══════════════════════════════════════════════════════════════════════

        #region ── Unity 생명주기 ──

        /// <summary>
        /// Awake에서 버튼 onClick 리스너를 등록하고 WaitForSeconds를 캐시한다.
        /// 람다를 사용하여 각 버튼에 buildingId를 직접 바인딩한다.
        /// </summary>
        private void Awake()
        {
            _waitForSeconds = new WaitForSeconds(REFRESH_INTERVAL);

            RegisterButton(_campfireButton,   "Campfire");
            RegisterButton(_houseButton,      "House");
            RegisterButton(_storehouseButton, "Storehouse");
            RegisterButton(_townHallButton,   "TownHall");
            RegisterButton(_forgeButton,      "Forge");
            RegisterButton(_watchtowerButton, "Watchtower");
        }

        private void Start()
        {
            _refreshCoroutine = StartCoroutine(RefreshButtonStatesCoroutine());
        }

        private void OnDestroy()
        {
            if (_refreshCoroutine != null)
            {
                StopCoroutine(_refreshCoroutine);
                _refreshCoroutine = null;
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // 코루틴
        // ══════════════════════════════════════════════════════════════════════

        #region ── 코루틴 ──

        /// <summary>
        /// 0.5초마다 각 버튼의 interactable 상태를 갱신하는 코루틴.
        ///
        /// 버튼 비활성화 조건:
        ///   - 필요 자원이 부족한 경우
        ///   - 건물이 이미 완성된 경우
        ///   - TownHall/Forge/Watchtower: TownHallBuilt == false인 경우 추가 비활성화
        /// </summary>
        private IEnumerator RefreshButtonStatesCoroutine()
        {
            while (true)
            {
                yield return _waitForSeconds;

                AuthoritativeWorldState ws = AuthoritativeWorldState.Instance;
                if (ws == null) continue;

                float wood  = ws.WoodStock;
                float stone = ws.StoneStock;
                float iron  = ws.IronStock;

                // Campfire: 이미 완성되면 비활성화
                SetButtonInteractable(_campfireButton,
                    !ws.CampfireBuilt && wood >= BuildingCosts.CAMPFIRE_WOOD);

                // House: 이미 완성되면 비활성화
                SetButtonInteractable(_houseButton,
                    !ws.HouseBuilt && wood >= BuildingCosts.HOUSE_WOOD && stone >= BuildingCosts.HOUSE_STONE);

                // Storehouse: 이미 완성되면 비활성화
                SetButtonInteractable(_storehouseButton,
                    !ws.StorehouseBuilt && wood >= BuildingCosts.STORE_WOOD && stone >= BuildingCosts.STORE_STONE);

                // TownHall: 이미 완성되면 비활성화
                SetButtonInteractable(_townHallButton,
                    !ws.TownHallBuilt
                    && wood  >= BuildingCosts.TOWNHALL_WOOD
                    && stone >= BuildingCosts.TOWNHALL_STONE
                    && iron  >= BuildingCosts.TOWNHALL_IRON);

                // Forge: TownHall 완성 후 활성화, 이미 완성되면 비활성화
                SetButtonInteractable(_forgeButton,
                    ws.TownHallBuilt
                    && !ws.ForgeBuilt
                    && wood  >= BuildingCosts.FORGE_WOOD
                    && stone >= BuildingCosts.FORGE_STONE
                    && iron  >= BuildingCosts.FORGE_IRON);

                // Watchtower: TownHall 완성 후 활성화, 이미 완성되면 비활성화
                SetButtonInteractable(_watchtowerButton,
                    ws.TownHallBuilt
                    && !ws.WatchtowerBuilt
                    && wood  >= BuildingCosts.WATCHTOWER_WOOD
                    && stone >= BuildingCosts.WATCHTOWER_STONE
                    && iron  >= BuildingCosts.WATCHTOWER_IRON);
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private 헬퍼 메서드
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private 헬퍼 메서드 ──

        /// <summary>
        /// 버튼에 onClick 리스너를 등록한다.
        /// 클릭 시 PlayerInputController.IssueBuildingOrder(buildingId)를 호출한다.
        /// </summary>
        private void RegisterButton(Button button, string buildingId)
        {
            if (button == null)
            {
                Debug.LogWarning($"[BuildingOrderPanel] RegisterButton: '{buildingId}' 버튼이 null입니다. " +
                                 "Inspector에서 버튼을 연결하세요.");
                return;
            }

            button.onClick.AddListener(() =>
            {
                if (_inputController == null)
                {
                    Debug.LogWarning($"[BuildingOrderPanel] 버튼 클릭: _inputController가 null입니다. " +
                                     $"BuildingId={buildingId}");
                    return;
                }

                _inputController.IssueBuildingOrder(buildingId);
            });
        }

        private static void SetButtonInteractable(Button button, bool interactable)
        {
            if (button == null) return;
            button.interactable = interactable;
        }

        #endregion
    }
}
