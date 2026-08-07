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

        [Header("추적 설정 (M13-B 후속)")]
        [Tooltip("선택 주민 추적 속도 (지수 감쇠 계수). 클수록 빨리 따라붙음. 기본 5.")]
        [SerializeField] private float _followLerpSpeed = 5f;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private Fields
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private Fields ──

        // 맵 경계 반크기 (MapConfig.mapOffset — 기본 50)
        // 타일 좌표계: -_mapHalf ~ +_mapHalf (예: -50 ~ +50)
        private float _mapHalf = 50f;

        // 추적 대상 (M13-B 후속) — null = 자유 카메라. 대상 파괴(주민 사망) 시
        // Unity null 비교로 자동 해제된다 (HandleFollow의 첫 검사).
        private Transform _followTarget;

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
            HandleFollow(); // 이동(수동 취소 판정) 뒤, 줌 앞 — 같은 프레임 취소가 즉시 반영
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

            // 추적 해제 (M13-B 후속) — 의도적 입력(키보드)만 추적을 푼다. 수동 조작 우선.
            // 엣지 패닝은 해제 조건이 아니다: 상태줄이 좌상단이라 클릭 직후 마우스가
            // 가장자리에 남기 마련인데, 그걸로 풀리면 추적이 시작되자마자 끝난다.
            if (_followTarget != null && (inputX != 0f || inputY != 0f))
                _followTarget = null;

            Vector2 moveDir = new Vector2(inputX, inputY);

            // ── 마우스 엣지 패닝 ──────────────────────────────────────────── (추적 중엔 잠금)
            if (_edgePanEnabled && _followTarget == null)
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

            // unscaledDeltaTime (2026-08-07 일시정지 도입) — 카메라는 시뮬레이션이 아니라
            // 플레이어의 눈이다. Time.deltaTime이면 timeScale 0에서 화면이 얼어붙어,
            // "멈추고 상황을 살펴본다"는 일시정지의 목적 자체가 성립하지 않는다.
            Vector3 pos = _camera.transform.position;
            pos.x += moveDir.x * speed * Time.unscaledDeltaTime;
            pos.y += moveDir.y * speed * Time.unscaledDeltaTime;

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
        /// 지정 월드 좌표로 즉시 점프 (M13-B 후속 — 상태 알림 줄 클릭 → 굶는 주민 포커스).
        /// 이동·줌과 같은 맵 경계 클램핑을 적용한다 — 클램프 산식이 이 파일에만 있으므로
        /// 점프도 여기서 제공한다 (밖에서 복제하면 판단 규칙 이원화).
        /// 재사용 라이브러리 최소 절제 원칙: 기존 메서드는 무수정, public 진입점 1개만 추가.
        /// </summary>
        public void FocusOn(Vector2 worldPos)
        {
            Vector3 pos = _camera.transform.position;
            pos.x = worldPos.x;
            pos.y = worldPos.y;
            _camera.transform.position = ClampToMap(pos);
        }

        /// <summary>주민 추적 시작 (M13-B 후속 — 선택 = 추적, 몰입 카메라). 해제 경로 3개:
        /// StopFollow(선택 해제) · 키보드 카메라 입력(수동 우선) · 대상 파괴(사망).</summary>
        public void Follow(Transform target) => _followTarget = target;

        /// <summary>추적 해제 — 선택 해제의 표현 짝. 카메라는 그 자리에 남는다.</summary>
        public void StopFollow() => _followTarget = null;

        /// <summary>
        /// 선택 주민 추적 틱 (M13-B 후속) — 지수 감쇠로 부드럽게 따라붙는다
        /// (프레임률 무관 — Lerp(1-e^-kt)). 클램프는 이동·줌·점프와 동일 산식.
        /// </summary>
        private void HandleFollow()
        {
            if (_followTarget == null) return; // Unity 파괴 비교 포함 — 사망 주민 자동 해제

            // unscaledDeltaTime — 이동과 같은 이유 (일시정지 중에도 추적이 대상에 정착해야 한다).
            float t = 1f - Mathf.Exp(-_followLerpSpeed * Time.unscaledDeltaTime);
            Vector3 pos = _camera.transform.position;
            pos.x = Mathf.Lerp(pos.x, _followTarget.position.x, t);
            pos.y = Mathf.Lerp(pos.y, _followTarget.position.y, t);
            _camera.transform.position = ClampToMap(pos);
        }

        /// <summary>맵 경계 클램프 (M13-B 후속 신설분 공용 — FocusOn·HandleFollow).
        /// HandleMovement·HandleZoom의 인라인 산식과 동일 (기존 메서드는 최소 절제로 무수정).</summary>
        private Vector3 ClampToMap(Vector3 pos)
        {
            float halfH = _camera.orthographicSize;
            float halfW = halfH * _camera.aspect;
            float clampX = Mathf.Max(0f, _mapHalf - halfW);
            float clampY = Mathf.Max(0f, _mapHalf - halfH);
            pos.x = Mathf.Clamp(pos.x, -clampX, clampX);
            pos.y = Mathf.Clamp(pos.y, -clampY, clampY);
            return pos;
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
