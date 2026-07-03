/// <summary>
/// CameraController.cs — 2D 탑다운 카메라 이동 및 줌 컨트롤러
///
/// 역할(Role): WASD·방향키·마우스 엣지 패닝으로 카메라를 이동시키고,
///             마우스 스크롤 휠로 줌을 조절한다.
///             카메라 위치를 맵 경계(MapConfig.mapOffset) 내로 클램핑한다.
///
/// 사용법(Usage):
///   씬의 Main Camera GameObject에 이 컴포넌트를 부착한다.
///   별도 Inspector 연결 없이 Camera.main 자동 사용.
///
/// 의존성(Dependencies):
///   - MapConfig.cs (맵 경계 읽기 — Active.mapOffset)
///
/// Script Execution Order: [DefaultExecutionOrder(-15)]
///   PlayerInputController(-20) 이후, HUDManager(-10) 이전.
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-07-03
/// </summary>

using UnityEngine;
using AIVillage.Core;

namespace AIVillage.UI
{
    /// <summary>
    /// 2D 탑다운 카메라 이동·줌 컨트롤러.
    /// Main Camera에 직접 부착하거나 별도 GameObject에 부착 후 _camera 슬롯에 연결한다.
    /// </summary>
    [DefaultExecutionOrder(-15)]
    [DisallowMultipleComponent]
    public sealed class CameraController : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════════
        // Serialized Fields (Inspector)
        // ══════════════════════════════════════════════════════════════════════

        #region ── Serialized Fields ──

        [Tooltip("이동시킬 카메라. null이면 Awake에서 Camera.main으로 자동 할당.")]
        [SerializeField] private Camera _camera;

        [Header("이동 설정")]
        [Tooltip("WASD/방향키 이동 속도 (타일/초). 기본값 20.")]
        [SerializeField] private float _keyMoveSpeed = 20f;

        [Tooltip("마우스 엣지 패닝 속도 (타일/초). 기본값 15.")]
        [SerializeField] private float _edgeMoveSpeed = 15f;

        [Tooltip("마우스가 화면 가장자리에서 이 픽셀 이내일 때 엣지 패닝 발동.")]
        [SerializeField] private float _edgePanThreshold = 60f;

        [Tooltip("마우스 엣지 패닝 활성화 여부.")]
        [SerializeField] private bool _edgePanEnabled = true;

        [Header("줌 설정")]
        [Tooltip("마우스 스크롤 줌 감도. 값이 클수록 줌 속도가 빨라짐.")]
        [SerializeField] private float _zoomSpeed = 4f;

        [Tooltip("줌 아웃 최대값 (Orthographic Size). 값이 클수록 더 멀리 보임.")]
        [SerializeField] private float _maxZoom = 30f;

        [Tooltip("줌 인 최소값 (Orthographic Size). 값이 작을수록 더 가까이 보임.")]
        [SerializeField] private float _minZoom = 3f;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private Fields
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private Fields ──

        // 맵 경계 반크기 (MapConfig.mapOffset — 기본 50)
        // 타일 좌표계: -_mapHalf ~ +_mapHalf (예: -50 ~ +50)
        private float _mapHalf = 50f;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Unity 생명주기
        // ══════════════════════════════════════════════════════════════════════

        #region ── Unity 생명주기 ──

        private void Awake()
        {
            // 카메라 캐시: Inspector 미연결 시 이 컴포넌트가 붙은 카메라 또는 Camera.main 폴백
            if (_camera == null)
                _camera = GetComponent<Camera>() ?? Camera.main;

            if (_camera == null)
            {
                Debug.LogError("[CameraController] Awake: 카메라를 찾을 수 없습니다. " +
                               "Main Camera에 부착하거나 Inspector에서 카메라를 연결하세요.");
                enabled = false;
            }
        }

        private void Start()
        {
            // MapConfig에서 맵 경계 읽기 (GameManager.Awake(-80)에서 SetActive 완료 보장)
            if (MapConfig.Active != null)
                _mapHalf = MapConfig.Active.mapOffset; // 기본 50
        }

        private void Update()
        {
            HandleMovement();
            HandleZoom();
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private 메서드
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private 메서드 ──

        /// <summary>
        /// WASD·방향키·마우스 엣지 패닝으로 카메라 위치를 갱신한다.
        /// 맵 경계를 벗어나지 않도록 클램핑 처리한다.
        /// </summary>
        private void HandleMovement()
        {
            // ── 키보드 입력 (WASD + 방향키) ──────────────────────────────────
            // Input.GetAxisRaw: Unity의 "Horizontal"(A/D, ←/→)과 "Vertical"(W/S, ↑/↓) 축 사용
            float inputX = Input.GetAxisRaw("Horizontal");
            float inputY = Input.GetAxisRaw("Vertical");

            Vector2 moveDir = new Vector2(inputX, inputY);

            // ── 마우스 엣지 패닝 ────────────────────────────────────────────
            if (_edgePanEnabled)
            {
                // 마우스가 화면 밖이면 엣지 패닝 비활성 (Alt-Tab 등 방지)
                Vector3 mouse = Input.mousePosition;
                bool mouseInScreen = mouse.x >= 0 && mouse.x <= Screen.width &&
                                     mouse.y >= 0 && mouse.y <= Screen.height;

                if (mouseInScreen)
                {
                    // 화면 가장자리 _edgePanThreshold 픽셀 이내: 해당 방향으로 이동
                    if (mouse.x < _edgePanThreshold)            moveDir.x -= 1f;
                    else if (mouse.x > Screen.width - _edgePanThreshold)  moveDir.x += 1f;

                    if (mouse.y < _edgePanThreshold)            moveDir.y -= 1f;
                    else if (mouse.y > Screen.height - _edgePanThreshold) moveDir.y += 1f;
                }
            }

            if (moveDir.sqrMagnitude < 0.001f) return;

            // 대각선 이동 시 속도를 정규화하여 일정하게 유지
            // (키보드 입력과 엣지 패닝이 합산될 경우 크기 > 1)
            if (moveDir.magnitude > 1f) moveDir.Normalize();

            // 키보드: _keyMoveSpeed, 엣지 패닝 전용: _edgeMoveSpeed
            // 두 입력이 합산된 경우 키보드 속도 기준 (키보드 우선)
            float speed = (inputX != 0f || inputY != 0f) ? _keyMoveSpeed : _edgeMoveSpeed;

            Vector3 pos = _camera.transform.position;
            pos.x += moveDir.x * speed * Time.deltaTime;
            pos.y += moveDir.y * speed * Time.deltaTime;

            // ── 맵 경계 클램핑 ──────────────────────────────────────────────
            // 카메라 뷰포트가 맵 밖으로 나가지 않도록 orthographic half-size를 감안하여 클램핑한다.
            float halfH = _camera.orthographicSize;
            float halfW = halfH * _camera.aspect;

            // 뷰포트가 맵보다 크면 중앙에 고정 (클램핑 범위가 반전되지 않도록)
            float clampX = Mathf.Max(0f, _mapHalf - halfW);
            float clampY = Mathf.Max(0f, _mapHalf - halfH);

            pos.x = Mathf.Clamp(pos.x, -clampX, clampX);
            pos.y = Mathf.Clamp(pos.y, -clampY, clampY);

            _camera.transform.position = pos;
        }

        /// <summary>
        /// 마우스 스크롤 휠로 카메라 Orthographic Size를 조절한다.
        /// 줌 인/아웃은 _minZoom ~ _maxZoom 사이로 클램핑된다.
        /// </summary>
        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) < 0.001f) return;

            // 현재 크기에 비례하여 줌 — 멀리 있을수록 줌 변화가 크게 느껴짐
            float newSize = _camera.orthographicSize - scroll * _zoomSpeed * _camera.orthographicSize;
            _camera.orthographicSize = Mathf.Clamp(newSize, _minZoom, _maxZoom);

            // 줌 후 위치가 맵 경계를 벗어났을 수 있으므로 클램핑 재적용
            Vector3 pos = _camera.transform.position;
            float halfH = _camera.orthographicSize;
            float halfW = halfH * _camera.aspect;
            float clampX = Mathf.Max(0f, _mapHalf - halfW);
            float clampY = Mathf.Max(0f, _mapHalf - halfH);
            pos.x = Mathf.Clamp(pos.x, -clampX, clampX);
            pos.y = Mathf.Clamp(pos.y, -clampY, clampY);
            _camera.transform.position = pos;
        }

        #endregion
    }
}
