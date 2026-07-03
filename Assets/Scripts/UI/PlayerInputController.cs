/// <summary>
/// PlayerInputController.cs - 플레이어 입력 처리 및 주민 선택/건설 명령 관리자
///
/// 역할(Role): 마우스 레이캐스트로 Villager 레이어 GameObject를 감지하고,
///             선택된 VillagerFSM을 HUDManager에 이벤트로 알린다.
///             건설 명령(IssueBuildingOrder)은 자원 충족 여부를 확인한 뒤
///             BuildingQueue.EnqueueBuilding()을 호출한다.
///
/// 사용법(Usage):
///   1. 씬에 빈 GameObject("_InputController")를 생성하고 이 컴포넌트를 부착한다.
///   2. Inspector의 _camera 슬롯에 Main Camera를 드래그한다.
///   3. Unity Tag Manager에 "Villager" 레이어가 등록되어 있어야 한다.
///
/// 의존성(Dependencies):
///   - VillagerFSM.cs (AI 레이어) — 선택 대상
///   - BuildingQueue.cs (Core 레이어) — 건설 명령 전달
///   - AuthoritativeWorldState.cs (Core 레이어) — 자원 충족 확인
///   - HUDManager.cs (UI 레이어) — 토스트 표시 위임
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-28
/// </summary>

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using AIVillage.AI;
using AIVillage.Core;

namespace AIVillage.UI
{
    /// <summary>
    /// 플레이어 입력 전담 컨트롤러. 주민 선택과 건설 명령 두 가지 역할을 담당한다.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    [DisallowMultipleComponent]
    public sealed class PlayerInputController : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════════
        // 상수
        // ══════════════════════════════════════════════════════════════════════

        #region ── 상수 ──

        // ── 레이캐스트 최대 거리 ──────────────────────────────────────────────────
        // 건물 비용 상수는 BuildingCosts.cs에서 중앙 관리한다.
        private const float RAYCAST_DISTANCE = 100f;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Serialized Fields (Inspector)
        // ══════════════════════════════════════════════════════════════════════

        #region ── Serialized Fields ──

        [Tooltip("레이캐스트에 사용할 카메라. null이면 Camera.main을 Awake에서 자동 할당한다.")]
        [SerializeField] private Camera _camera;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // 이벤트 (HUDManager가 구독)
        // ══════════════════════════════════════════════════════════════════════

        #region ── 이벤트 ──

        /// <summary>
        /// 주민이 선택되었을 때 발행된다.
        /// HUDManager가 구독하여 VillagerStatusPanel을 활성화한다.
        /// </summary>
        public event Action<VillagerFSM> OnVillagerSelected;

        /// <summary>
        /// 주민 선택이 해제되었을 때 발행된다.
        /// HUDManager가 구독하여 VillagerStatusPanel을 비활성화한다.
        /// </summary>
        public event Action OnVillagerDeselected;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private Fields
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private Fields ──

        // Awake에서 캐시한 Villager 레이어 마스크
        private int _villagerLayerMask;

        // 현재 선택된 주민 (null이면 선택 없음)
        private VillagerFSM _selectedVillager;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // 공개 프로퍼티
        // ══════════════════════════════════════════════════════════════════════

        #region ── 공개 프로퍼티 ──

        /// <summary>현재 선택된 주민. null이면 선택 없음.</summary>
        public VillagerFSM SelectedVillager => _selectedVillager;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Unity 생명주기
        // ══════════════════════════════════════════════════════════════════════

        #region ── Unity 생명주기 ──

        /// <summary>
        /// 컴포넌트 초기화. 카메라 캐시 및 레이어 마스크 설정.
        /// Update()에서 GetComponent/Find 호출을 방지하기 위해 여기서 모두 캐시한다.
        /// </summary>
        private void Awake()
        {
            // 카메라 캐시: Inspector에서 미설정 시 Camera.main으로 폴백
            if (_camera == null)
            {
                _camera = Camera.main;

                if (_camera == null)
                {
                    Debug.LogError("[PlayerInputController] Awake: Camera.main이 null입니다. " +
                                   "씬에 MainCamera 태그가 설정된 카메라가 있는지 확인하세요.");
                }
            }

            // ── Villager 레이어 마스크 계산 ────────────────────────────────────
            // 레이캐스트가 Villager 레이어만 감지하도록 비트마스크를 미리 계산한다.
            // LayerMask.NameToLayer()는 문자열 룩업이므로 Awake에서 한 번만 호출한다.
            int layerIndex = LayerMask.NameToLayer("Villager");

            if (layerIndex < 0)
            {
                Debug.LogError("[PlayerInputController] Awake: 'Villager' 레이어가 " +
                               "Project Settings > Tags and Layers에 등록되지 않았습니다. " +
                               "레이어를 추가한 뒤 실행하세요.");
                _villagerLayerMask = 0;
            }
            else
            {
                // "1 << layerIndex": layerIndex번 비트를 1로 설정하여 해당 레이어만 검출
                _villagerLayerMask = 1 << layerIndex;
            }
        }

        /// <summary>
        /// 매 프레임 마우스 왼쪽 버튼 클릭을 감지하여 주민 선택/해제를 처리한다.
        /// UI 위에서 클릭하면 레이캐스트를 건너뛰어 버튼 등 UI 상호작용이 정상 동작하도록 한다.
        /// </summary>
        private void Update()
        {
            // ── 마우스 왼쪽 버튼 클릭 감지 ────────────────────────────────────
            if (!Input.GetMouseButtonDown(0)) return;

            // ── UI 위 클릭 방지 ──────────────────────────────────────────────
            // EventSystem.current가 UI 요소 위에 있으면 3D 레이캐스트를 건너뛴다.
            // 이렇게 해야 건설 버튼 등 UI 버튼 클릭이 레이캐스트와 충돌하지 않는다.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (_camera == null) return;

            // ── 마우스 위치 → 월드 레이 변환 ─────────────────────────────────
            // 씬 좌표계: Vector3(x, y, 0) — 카메라는 (0, 0, -10)에서 +z 방향
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);

            // ── Physics Raycast: Villager 레이어만 감지 ──────────────────────
            if (Physics.Raycast(ray, out RaycastHit hit, RAYCAST_DISTANCE, _villagerLayerMask))
            {
                // 충돌한 오브젝트에서 VillagerFSM 컴포넌트를 가져온다.
                // 레이캐스트 히트 시에만 호출되므로 Update 안이지만 성능 부담 최소화.
                VillagerFSM clickedFSM = hit.collider.GetComponent<VillagerFSM>();

                if (clickedFSM != null && clickedFSM.Brain != null && clickedFSM.Brain.IsAlive)
                {
                    SelectVillager(clickedFSM);
                }
            }
            else
            {
                // 빈 공간 클릭 → 선택 해제
                if (_selectedVillager != null)
                {
                    DeselectVillager();
                }
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // 공개 메서드
        // ══════════════════════════════════════════════════════════════════════

        #region ── 공개 메서드 ──

        /// <summary>
        /// 주민을 선택하고 OnVillagerSelected 이벤트를 발행한다.
        /// </summary>
        /// <param name="fsm">선택할 VillagerFSM. null이면 경고 후 무시.</param>
        public void SelectVillager(VillagerFSM fsm)
        {
            if (fsm == null)
            {
                Debug.LogWarning("[PlayerInputController] SelectVillager: fsm이 null입니다.");
                return;
            }

            _selectedVillager = fsm;

            Debug.Log($"[PlayerInputController] 주민 선택: {fsm.Brain?.VillagerId ?? "Unknown"}");

            // 이벤트 발행 — HUDManager가 구독하여 VillagerStatusPanel 패널을 활성화한다
            OnVillagerSelected?.Invoke(fsm);
        }

        /// <summary>
        /// 현재 선택된 주민의 선택을 해제하고 OnVillagerDeselected 이벤트를 발행한다.
        /// 이미 선택된 주민이 없으면 아무 동작도 하지 않는다.
        /// </summary>
        public void DeselectVillager()
        {
            if (_selectedVillager == null) return;

            Debug.Log($"[PlayerInputController] 주민 선택 해제: {_selectedVillager.Brain?.VillagerId ?? "Unknown"}");

            _selectedVillager = null;

            // 이벤트 발행 — HUDManager가 구독하여 VillagerStatusPanel 패널을 비활성화한다
            OnVillagerDeselected?.Invoke();
        }

        /// <summary>
        /// 건설 명령을 처리한다.
        ///
        /// 처리 순서:
        ///   1. buildingId 유효성 검사
        ///   2. 자원 충족 여부 확인 (AuthoritativeWorldState 조회, 차감은 하지 않음)
        ///   3. BuildingQueue.Instance.EnqueueBuilding() 호출
        ///   4. HUDManager.ShowToast()로 결과 피드백
        ///
        /// 주의: 자원 차감은 이 메서드에서 하지 않는다.
        ///       ResourceRegistry.Reserve/Commit은 VillagerFSM의 Planning 단계에서 수행한다.
        ///       BuildingOrderPanel 버튼의 onClick 리스너에서 이 메서드를 호출한다.
        /// </summary>
        /// <param name="buildingId">건설할 건물 ID (예: "Campfire", "TownHall")</param>
        public void IssueBuildingOrder(string buildingId)
        {
            // ── 유효성 검사 ──────────────────────────────────────────────────
            if (string.IsNullOrEmpty(buildingId))
            {
                Debug.LogWarning("[PlayerInputController] IssueBuildingOrder: buildingId가 null/빈 문자열입니다.");
                return;
            }

            // ── 자원 충족 여부 확인 (조회만, 차감 없음) ─────────────────────
            if (!HasEnoughResourcesForBuilding(buildingId, out string shortageMessage))
            {
                string toastMessage = $"{buildingId} 건설 불가: {shortageMessage}";
                Debug.Log($"[PlayerInputController] 자원 부족 — {toastMessage}");
                HUDManager.Instance?.ShowToast(toastMessage);
                return;
            }

            // ── BuildingQueue에 건설 명령 등록 ───────────────────────────────
            if (BuildingQueue.Instance == null)
            {
                Debug.LogError("[PlayerInputController] IssueBuildingOrder: BuildingQueue.Instance가 null입니다.");
                HUDManager.Instance?.ShowToast("건설 시스템 오류 — BuildingQueue를 확인하세요.");
                return;
            }

            // 건설 타일 좌표: 기지 중앙(0, 0)을 기본값으로 사용
            // TODO: 기획팀 확인 필요 — 플레이어가 건설 위치를 직접 선택하는 기능 추가 시 수정 필요
            bool success = BuildingQueue.Instance.EnqueueBuilding(buildingId, 0, 0);

            if (success)
            {
                string successMessage = $"{buildingId} 건설이 대기열에 추가되었습니다.";
                Debug.Log($"[PlayerInputController] 건설 명령 성공 — {buildingId}");
                HUDManager.Instance?.ShowToast(successMessage);
            }
            else
            {
                // EnqueueBuilding이 false를 반환 = 이미 동일 건물이 대기 중
                string failMessage = $"{buildingId}가 이미 건설 대기열에 있습니다.";
                Debug.Log($"[PlayerInputController] 건설 명령 중복 — {buildingId}");
                HUDManager.Instance?.ShowToast(failMessage);
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private 헬퍼 메서드
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private 헬퍼 메서드 ──

        /// <summary>
        /// 지정한 건물의 건설에 필요한 자원이 현재 WorldState에 충족되어 있는지 확인한다.
        /// 이 메서드는 자원을 차감하지 않는다 — 조회(읽기)만 한다.
        ///
        /// GDD v0.4 건물 비용 테이블 (기획서 수치):
        ///   Campfire:   Wood 5
        ///   House:      Wood 20, Stone 10
        ///   Storehouse: Wood 15, Stone 5
        ///   TownHall:   Wood 35, Stone 30, Iron 6
        ///   Forge:      Wood 20, Stone 20, Iron 15
        ///   Watchtower: Wood 10, Stone 30, Iron 5
        /// </summary>
        /// <param name="buildingId">확인할 건물 ID</param>
        /// <param name="shortageMessage">자원 부족 시 사유 메시지 (충족 시 null)</param>
        /// <returns>자원 충족 여부</returns>
        private bool HasEnoughResourcesForBuilding(string buildingId, out string shortageMessage)
        {
            shortageMessage = null;

            AuthoritativeWorldState ws = AuthoritativeWorldState.Instance;
            if (ws == null)
            {
                shortageMessage = "WorldState 미초기화";
                return false;
            }

            switch (buildingId)
            {
                case "Campfire":
                    if (ws.WoodStock < BuildingCosts.CAMPFIRE_WOOD)
                    {
                        shortageMessage = $"나무 부족 (필요: {BuildingCosts.CAMPFIRE_WOOD}, 보유: {ws.WoodStock:F0})";
                        return false;
                    }
                    break;

                case "House":
                    if (ws.WoodStock < BuildingCosts.HOUSE_WOOD)
                    {
                        shortageMessage = $"나무 부족 (필요: {BuildingCosts.HOUSE_WOOD}, 보유: {ws.WoodStock:F0})";
                        return false;
                    }
                    if (ws.StoneStock < BuildingCosts.HOUSE_STONE)
                    {
                        shortageMessage = $"돌 부족 (필요: {BuildingCosts.HOUSE_STONE}, 보유: {ws.StoneStock:F0})";
                        return false;
                    }
                    break;

                case "Storehouse":
                    if (ws.WoodStock < BuildingCosts.STORE_WOOD)
                    {
                        shortageMessage = $"나무 부족 (필요: {BuildingCosts.STORE_WOOD}, 보유: {ws.WoodStock:F0})";
                        return false;
                    }
                    if (ws.StoneStock < BuildingCosts.STORE_STONE)
                    {
                        shortageMessage = $"돌 부족 (필요: {BuildingCosts.STORE_STONE}, 보유: {ws.StoneStock:F0})";
                        return false;
                    }
                    break;

                case "TownHall":
                    if (ws.WoodStock < BuildingCosts.TOWNHALL_WOOD)
                    {
                        shortageMessage = $"나무 부족 (필요: {BuildingCosts.TOWNHALL_WOOD}, 보유: {ws.WoodStock:F0})";
                        return false;
                    }
                    if (ws.StoneStock < BuildingCosts.TOWNHALL_STONE)
                    {
                        shortageMessage = $"돌 부족 (필요: {BuildingCosts.TOWNHALL_STONE}, 보유: {ws.StoneStock:F0})";
                        return false;
                    }
                    if (ws.IronStock < BuildingCosts.TOWNHALL_IRON)
                    {
                        shortageMessage = $"철 부족 (필요: {BuildingCosts.TOWNHALL_IRON}, 보유: {ws.IronStock:F0})";
                        return false;
                    }
                    break;

                case "Forge":
                    if (ws.WoodStock < BuildingCosts.FORGE_WOOD)
                    {
                        shortageMessage = $"나무 부족 (필요: {BuildingCosts.FORGE_WOOD}, 보유: {ws.WoodStock:F0})";
                        return false;
                    }
                    if (ws.StoneStock < BuildingCosts.FORGE_STONE)
                    {
                        shortageMessage = $"돌 부족 (필요: {BuildingCosts.FORGE_STONE}, 보유: {ws.StoneStock:F0})";
                        return false;
                    }
                    if (ws.IronStock < BuildingCosts.FORGE_IRON)
                    {
                        shortageMessage = $"철 부족 (필요: {BuildingCosts.FORGE_IRON}, 보유: {ws.IronStock:F0})";
                        return false;
                    }
                    break;

                case "Watchtower":
                    if (ws.WoodStock < BuildingCosts.WATCHTOWER_WOOD)
                    {
                        shortageMessage = $"나무 부족 (필요: {BuildingCosts.WATCHTOWER_WOOD}, 보유: {ws.WoodStock:F0})";
                        return false;
                    }
                    if (ws.StoneStock < BuildingCosts.WATCHTOWER_STONE)
                    {
                        shortageMessage = $"돌 부족 (필요: {BuildingCosts.WATCHTOWER_STONE}, 보유: {ws.StoneStock:F0})";
                        return false;
                    }
                    if (ws.IronStock < BuildingCosts.WATCHTOWER_IRON)
                    {
                        shortageMessage = $"철 부족 (필요: {BuildingCosts.WATCHTOWER_IRON}, 보유: {ws.IronStock:F0})";
                        return false;
                    }
                    break;

                default:
                    Debug.LogWarning($"[PlayerInputController] HasEnoughResourcesForBuilding: " +
                                     $"알 수 없는 buildingId '{buildingId}'. 자원 검사 없이 진행합니다.");
                    break;
            }

            return true;
        }

        #endregion
    }
}
