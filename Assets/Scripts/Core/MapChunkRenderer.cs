/// <summary>
/// MapChunkRenderer.cs - 타일 맵 FoW 텍스처 렌더러
///
/// 역할(Role): mapSize×mapSize 크기의 Texture2D를 생성하여 Quad MeshRenderer에 적용한다.
///             FoW 상태(미탐험/탐험됨/가시)에 따라 각 픽셀의 색상을 결정하고
///             FowManager의 더티 마스크를 기반으로 변경된 타일만 효율적으로 갱신한다.
///             카메라가 chunkSize×0.5 타일 이상 이동했거나, FowManager에 더티 타일이
///             존재할 때 RefreshDirtyChunks()를 실행한다.
///
/// 사용법(Usage):
///   1. _Managers 하위에 빈 GameObject "_MapChunkRenderer"를 만들고 이 컴포넌트를 부착한다.
///   2. 별도 GameObject "MapQuad"에 MeshFilter(내장 Quad 메시) + MeshRenderer를 부착하고
///      스케일을 (100, 100, 1)로, 위치를 (0, 0, 1)로 설정한다. Unlit/Texture 머티리얼을 적용한다.
///   3. Inspector의 _quad 슬롯에 MapQuad의 MeshRenderer 컴포넌트를 드래그한다.
///
/// 의존성(Dependencies):
///   - MapConfig.cs (맵 크기·색상 설정)
///   - FowManager.cs (FoW 상태·더티 마스크 조회)
///
/// 렌더링 좌표계:
///   타일 좌표 (tileX, tileY) = Texture2D 픽셀 인덱스 (ax, ay)
///   픽셀 인덱스 ↔ 배열 인덱스: ax = tileX + offset, ay = tileY + offset
///   1D 픽셀 버퍼 인덱스: ay * _size + ax (Texture2D의 x-major 레이아웃)
///
/// Script Execution Order: [DefaultExecutionOrder(-60)]
///   FowManager(-55)의 Awake보다 먼저 실행되어 Texture2D를 생성한다.
///   실제 FoW 상태는 Start()에서 읽으므로 FowManager 초기화와 순서 충돌 없음.
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-29
/// </summary>

using UnityEngine;

namespace AIVillage.Core
{
    /// <summary>
    /// 맵 타일을 Texture2D Overlay Quad로 렌더링하는 컴포넌트.
    /// FoW 상태에 따른 픽셀 색상을 관리하고 더티 타일만 효율적으로 갱신한다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-60)] // FowManager(-55) 이전. Texture2D 생성 후 FowManager가 Start에서 참조 가능.
    public sealed class MapChunkRenderer : MonoBehaviour
    {
        #region ── Serialized Fields (Inspector) ──

        [Tooltip("타일 맵을 표시할 Quad의 MeshRenderer. " +
                 "MapQuad GameObject에 MeshFilter(Quad) + MeshRenderer를 부착하고 " +
                 "스케일 (100, 100, 1), 위치 (0, 0, 1)로 설정한 뒤 이 슬롯에 드래그한다. " +
                 "Unlit/Texture 머티리얼을 사용해야 FoW 색상이 정확히 표시된다.")]
        [SerializeField] private MeshRenderer _quad;   // Inspector 연결 필수

        #endregion

        #region ── Private Fields ──

        // ── Texture2D (GPU 업로드용) ────────────────────────────────────────────
        // mapSize×mapSize 픽셀 크기. FilterMode.Point로 픽셀아트 스타일을 유지한다.
        // 100×100 = RGBA32 포맷 기준 40,000 bytes ≈ 40 KB.
        private Texture2D _tileTexture;

        // [PR Fix]: Material 인스턴스 메모리 누수 수정 — _materialInstance 필드 추가.
        // _quad.material 접근 시 Unity가 Material 인스턴스를 생성하므로
        // 이 참조를 유지했다가 OnDestroy()에서 명시적으로 Destroy()해야 누수가 없다.
        /// <summary>
        /// Start()에서 _quad.material로 생성된 Material 인스턴스.
        /// OnDestroy()에서 Destroy()로 해제하여 메모리 누수를 방지한다.
        /// </summary>
        private Material _materialInstance;

        // ── 픽셀 버퍼 (CPU측 색상 배열) ────────────────────────────────────────
        // SetPixels32() 호출 1회로 전체 배열을 GPU에 업로드하기 위해 캐시한다.
        // 매 프레임 new Color32[]를 생성하지 않아 GC를 방지한다.
        private Color32[] _pixelBuffer;

        // ── 카메라 이동 추적 ────────────────────────────────────────────────────
        // 마지막으로 RefreshDirtyChunks()를 실행한 시점의 카메라 위치.
        // Vector3.positiveInfinity로 초기화하여 Start() 직후 첫 갱신을 강제한다.
        private Vector3 _lastCameraPos = Vector3.positiveInfinity;

        // ── 카메라 참조 캐시 ────────────────────────────────────────────────────
        // Update()에서 Camera.main을 매 프레임 조회하지 않도록 Start()에서 캐시한다.
        // Camera.main은 내부적으로 FindObjectsWithTag("MainCamera")를 호출하여 비용이 있다.
        private Camera _cam;

        // ── 맵 크기 캐시 ────────────────────────────────────────────────────────
        // Start()에서 MapConfig.Active에서 읽어 저장한다.
        private int _size;    // MapConfig.Active.mapSize (예: 100)
        private int _offset;  // MapConfig.Active.mapOffset (예: 50)

        #endregion

        #region ── Unity 생명주기 ──

        /// <summary>
        /// Start()에서 Texture2D를 생성하고 초기 전체 렌더링을 1회 실행한다.
        /// Awake()가 아닌 Start()를 사용하는 이유:
        ///   FowManager(-55)의 Awake()가 MapChunkRenderer(-60)의 Awake()보다 나중에 실행되지만,
        ///   Start()는 모든 Awake() 완료 후 실행되므로 FowManager.Instance가 보장된다.
        /// </summary>
        private void Start()
        {
            // ── MapConfig 유효성 체크 ──────────────────────────────────────────
            if (MapConfig.Active == null)
            {
                Debug.LogError("[MapChunkRenderer] Start: MapConfig.Active가 null입니다. " +
                               "GameManager.Awake()에서 MapConfig.SetActive()가 호출됐는지 확인하세요. " +
                               "GameManager의 _mapConfig 슬롯에 MapConfig 에셋이 연결됐는지 확인하세요.");
                enabled = false; // 이 컴포넌트 비활성화하여 Update()가 실행되지 않도록 방지
                return;
            }

            // ── _quad 유효성 체크 ──────────────────────────────────────────────
            if (_quad == null)
            {
                Debug.LogError("[MapChunkRenderer] Start: _quad가 Inspector에 연결되지 않았습니다. " +
                               "MapQuad GameObject의 MeshRenderer를 _quad 슬롯에 드래그하세요.");
                enabled = false;
                return;
            }

            // 지면·안개 판은 **맨 아래** (2026-08-09). Y 정렬은 위쪽 물건에 음수 order를 주는데
            // 舊 MapQuad는 0이라, 배선하지 않으면 y > 0인 집·주민이 통째로 지면 뒤로 숨는다.
            // 씬 설정이 아니라 코드가 정한다 — 씬은 사람이 실수로 되돌릴 수 있다.
            _quad.sortingOrder = AIVillage.M0.WorldSort.Ground;

            // ── 맵 크기 캐시 ──────────────────────────────────────────────────
            _size   = MapConfig.Active.mapSize;
            _offset = MapConfig.Active.mapOffset;

            // ── 카메라 캐시 ────────────────────────────────────────────────────
            // Camera.main은 Start()에서 한 번만 캐시하여 Update()의 매 프레임 호출을 방지한다.
            _cam = Camera.main;
            if (_cam == null)
            {
                Debug.LogWarning("[MapChunkRenderer] Start: Camera.main이 null입니다. " +
                                 "씬에 'MainCamera' 태그를 가진 카메라가 있는지 확인하세요. " +
                                 "카메라 이동 기반 갱신이 작동하지 않을 수 있습니다.");
            }

            // ── Texture2D 생성 ─────────────────────────────────────────────────
            // TextureFormat.RGBA32: 픽셀당 R·G·B·A 각 1바이트 = 4바이트.
            // 100×100×4 = 40,000 바이트 ≈ 40 KB.
            // mipChain=false: 맵 텍스처는 1:1 픽셀 대응이 목적이므로 밉맵 불필요.
            _tileTexture            = new Texture2D(_size, _size, TextureFormat.RGBA32, mipChain: false);
            _tileTexture.filterMode = FilterMode.Point; // 픽셀아트 스타일: 보간 없이 선명한 경계
            _tileTexture.wrapMode   = TextureWrapMode.Clamp; // 가장자리를 늘리지 않음

            // ── 픽셀 버퍼 초기화 ──────────────────────────────────────────────
            // _size*_size개의 Color32를 미리 할당. 이후 재할당 없이 내용만 교체한다.
            _pixelBuffer = new Color32[_size * _size];

            // [PR Fix]: Material 인스턴스 메모리 누수 수정 — _quad.material을 _materialInstance에 저장.
            // _quad.material은 호출 시 새 Material 인스턴스를 생성하므로 반드시 참조를 보관해야
            // OnDestroy()에서 Destroy()로 해제할 수 있다.
            // sharedMaterial 대신 material을 사용하여 다른 오브젝트에 영향이 없도록 한다.
            _materialInstance             = _quad.material;
            _materialInstance.mainTexture = _tileTexture;

            // ── 초기 전체 렌더링 1회 ──────────────────────────────────────────
            // Start() 시점에는 FowManager.Instance가 초기화됐으므로 안전하게 참조 가능.
            // 초기 상태: 기지 주변은 가시(2), 나머지는 미탐험(0).
            RenderAll();

            Debug.Log($"[MapChunkRenderer] Start 완료. Texture2D={_size}×{_size} (RGBA32, Point). " +
                      "초기 전체 렌더링 완료.");
        }

        /// <summary>
        /// 매 프레임 호출된다.
        /// 카메라가 chunkSize×0.5 타일 이상 이동했거나, FowManager에 더티 타일이 있을 때
        /// RefreshDirtyChunks()를 실행한다.
        ///
        /// 성능 참고:
        ///   FowManager.OnTick()이 0.1초 간격으로 더티 마스크를 갱신한다.
        ///   MapChunkRenderer는 카메라 이동 또는 FoW 상태 변화가 감지될 때만 텍스처를 갱신하여
        ///   불필요한 SetPixels32 + Apply() 호출을 최소화한다.
        /// </summary>
        private void Update()
        {
            // FowManager 또는 MapConfig가 없으면 업데이트 스킵
            if (FowManager.Instance == null) return;
            if (MapConfig.Active == null) return;
            if (_cam == null) return;

            // [PR Fix]: 카메라 정지 시 FoW 갱신 누락 버그 수정 — HasAnyDirty 조건 추가.
            // 기존: 카메라 이동(chunkSize*0.5 이상)에만 갱신을 의존하여
            //       카메라 고정 상태에서는 FoW 변화가 화면에 전혀 반영되지 않았음.
            // 수정: cameraMovedEnough OR hasDirtyTiles 중 하나라도 참이면 갱신 실행.
            Vector3 camPos  = _cam.transform.position;
            float moveDist  = Vector3.Distance(camPos, _lastCameraPos);
            float threshold = MapConfig.Active.chunkSize * 0.5f;

            bool cameraMovedEnough = moveDist >= threshold;
            bool hasDirtyTiles     = FowManager.Instance.HasAnyDirty;

            if (cameraMovedEnough || hasDirtyTiles)
            {
                _lastCameraPos = camPos;
                RefreshDirtyChunks();
            }
        }

        /// <summary>
        /// 이 컴포넌트가 파괴될 때 Texture2D와 Material 인스턴스를 명시적으로 해제한다.
        /// Unity는 MonoBehaviour Destroy 시 비관리 Unity 오브젝트를 자동 해제하지 않으므로 직접 해제한다.
        /// 메모리 누수 방지를 위해 Destroy()를 호출한다.
        /// </summary>
        private void OnDestroy()
        {
            if (_tileTexture != null)
            {
                Destroy(_tileTexture);
                _tileTexture = null;
            }

            // [PR Fix]: Material 인스턴스 메모리 누수 수정 — OnDestroy()에서 _materialInstance 해제.
            // Start()에서 _quad.material로 생성된 인스턴스는 씬 종료 시 자동 해제되지 않으므로
            // 명시적으로 Destroy()를 호출해야 GPU 메모리 누수가 발생하지 않는다.
            if (_materialInstance != null)
            {
                Destroy(_materialInstance);
                _materialInstance = null;
            }
        }

        #endregion

        #region ── 렌더링 메서드 ──

        /// <summary>
        /// 전체 맵의 모든 픽셀을 한 번에 계산하여 텍스처에 적용한다.
        /// Start() 초기화 시 1회 실행된다.
        ///
        /// 성능 참고:
        ///   100×100 = 10,000 픽셀 순회.
        ///   이후 RefreshDirtyChunks()로 변경 타일만 부분 갱신하여 성능을 최적화한다.
        /// </summary>
        private void RenderAll()
        {
            if (_tileTexture == null || _pixelBuffer == null) return;

            // 전체 픽셀 버퍼를 FoW 상태에 따라 채운다.
            for (int ax = 0; ax < _size; ax++)
            {
                for (int ay = 0; ay < _size; ay++)
                {
                    // 배열 인덱스 → 타일 좌표 변환 (ax - offset = tileX)
                    int tileX = ax - _offset;
                    int tileY = ay - _offset;

                    // 1D 픽셀 버퍼 인덱스: Texture2D는 y*width+x 순서로 저장된다
                    // (Unity Texture2D: (0,0)이 좌하단)
                    int pixelIdx           = ay * _size + ax;
                    _pixelBuffer[pixelIdx] = GetTileColor(tileX, tileY);
                }
            }

            // SetPixels32: 전체 버퍼를 한 번에 GPU에 업로드.
            // 개별 SetPixel()보다 훨씬 빠르다 (API 호출 횟수: 10,000 → 1).
            _tileTexture.SetPixels32(_pixelBuffer);

            // Apply(updateMipmaps=false): 밉맵 갱신 없이 텍스처를 즉시 GPU에 반영한다.
            // mipChain=false로 생성했으므로 false가 안전하다.
            _tileTexture.Apply(false);
        }

        /// <summary>
        /// FowManager의 더티 마스크를 확인하여 변경된 타일의 픽셀만 갱신한다.
        /// Update()에서 카메라 이동 또는 FoW 더티 타일이 감지될 때 호출된다.
        ///
        /// 성능 최적화:
        ///   - 더티 타일이 0개이면 SetPixels32 + Apply() 자체를 스킵한다.
        ///   - FowManager.ClearDirty()를 호출하여 처리된 타일의 플래그를 초기화한다.
        ///   - 완료 후 FowManager.ClearAllDirtyFlag()를 호출하여 HasAnyDirty를 false로 초기화한다.
        /// </summary>
        private void RefreshDirtyChunks()
        {
            if (_tileTexture == null || _pixelBuffer == null) return;
            if (FowManager.Instance == null) return;

            // 이번 갱신 주기에서 실제로 변경된 타일이 있는지 추적
            bool anyDirty = false;

            for (int ax = 0; ax < _size; ax++)
            {
                for (int ay = 0; ay < _size; ay++)
                {
                    // 배열 인덱스 → 타일 좌표 변환
                    int tileX = ax - _offset;
                    int tileY = ay - _offset;

                    // 더티 마스크 체크: 변경되지 않은 타일은 건너뜀
                    if (!FowManager.Instance.IsDirty(tileX, tileY)) continue;

                    // 픽셀 버퍼 갱신
                    int pixelIdx           = ay * _size + ax;
                    _pixelBuffer[pixelIdx] = GetTileColor(tileX, tileY);
                    anyDirty               = true;

                    // 이 타일의 더티 플래그 초기화 (다음 갱신 주기에서 재처리 방지)
                    FowManager.Instance.ClearDirty(tileX, tileY);
                }
            }

            // 실제 변경이 있을 때만 GPU 업로드 수행 (불필요한 Apply() 방지)
            if (anyDirty)
            {
                _tileTexture.SetPixels32(_pixelBuffer);
                _tileTexture.Apply(false);
            }

            // [PR Fix]: 카메라 정지 시 FoW 갱신 누락 버그 수정 — RefreshDirtyChunks() 완료 후 HasAnyDirty 초기화.
            // 모든 더티 타일을 처리했으므로 HasAnyDirty를 false로 리셋한다.
            // 다음 SetVisible() 호출 시 HasAnyDirty가 다시 true로 설정되어 다음 갱신을 트리거한다.
            FowManager.Instance.ClearAllDirtyFlag();
        }

        /// <summary>
        /// 지정 타일의 렌더링 색상을 FoW 상태에 따라 결정한다.
        ///
        /// FoW 상태별 색상 결정 로직:
        ///   0 (미탐험) : fowUnexplored 색상 (완전 검정 — 아무것도 보이지 않음)
        ///   1 (탐험됨) : 타일 본래 색 × fowExplored 알파 블렌드 (어두운 기억 버전)
        ///   2 (가시)   : 타일 본래 색 100% (현재 보이는 상태)
        ///
        /// 알파 블렌드 공식 (state 1):
        ///   result.r = baseTileColor.r * fowExplored.a / 255
        ///   (fowExplored.a=180이면 약 70% 밝기로 어둡게 표시)
        /// </summary>
        private Color32 GetTileColor(int tileX, int tileY)
        {
            // MapConfig가 없으면 검정 반환 (안전 기본값)
            if (MapConfig.Active == null) return new Color32(0, 0, 0, 255);

            // FoW 상태 조회 (FowManager.Instance가 null이면 전부 미탐험으로 처리)
            byte fowState = (FowManager.Instance != null)
                            ? FowManager.Instance.GetFowState(tileX, tileY)
                            : (byte)0;

            switch (fowState)
            {
                // ── 미탐험: 완전 검정 ───────────────────────────────────────────
                case 0:
                    return MapConfig.Active.fowUnexplored;

                // ── 탐험됨: 타일 색 × fowExplored 알파 블렌드 ──────────────────
                // 이전에 본 곳이지만 현재 보이지 않는 "기억" 상태.
                // fowExplored.a (기본 180)를 곱하여 원본 색의 ~70% 밝기로 표시한다.
                case 1:
                    Color32 baseTile = GetBaseTileColor(tileX, tileY);
                    Color32 fowE     = MapConfig.Active.fowExplored;

                    // 공식: result.channel = baseTile.channel * fowE.a / 255
                    // 정수 연산으로 GC 없이 처리한다.
                    return new Color32(
                        r: (byte)(baseTile.r * fowE.a / 255),
                        g: (byte)(baseTile.g * fowE.a / 255),
                        b: (byte)(baseTile.b * fowE.a / 255),
                        a: 255 // 완전 불투명 (Quad 자체가 맵을 덮으므로 알파 투명 불필요)
                    );

                // ── 가시: 타일 본래 색 100% ──────────────────────────────────────
                case 2:
                    return GetBaseTileColor(tileX, tileY);

                // ── 예상치 못한 상태: 마젠타 ────────────────────────────────────
                // 마젠타는 디버그 시 즉시 식별 가능한 색상이다.
                // 이 분기에 진입하면 FowManager의 상태 값에 버그가 있는 것이다.
                default:
                    return new Color32(255, 0, 255, 255);
            }
        }

        /// <summary>
        /// 타일의 기본(FoW 적용 전) 색상을 반환한다.
        /// 현재는 전부 grassColor를 반환한다 (지형 타입 시스템 없음).
        ///
        /// 확장 포인트:
        ///   나중에 지형 타입 맵(Forest 영역 등)을 추가할 때 이 메서드만 수정한다.
        ///   예시:
        ///     if (TerrainMap.Instance.GetType(tileX, tileY) == TileType.Forest)
        ///         return MapConfig.Active.forestColor;
        ///
        /// TODO: 지형 타입 시스템 추가 시 이 메서드를 확장한다.
        /// </summary>
        private static Color32 GetBaseTileColor(int tileX, int tileY)
        {
            // 현재: 전부 Grass (설계 결정 D: 2타입 색상 — Forest는 향후 확장)
            // tileX, tileY 파라미터: 향후 지형 타입 조회 시 사용할 좌표 (현재 미사용)
            if (MapConfig.Active == null) return new Color32(100, 180, 80, 255); // 안전 기본값
            return MapConfig.Active.grassColor;
        }

        #endregion
    }
}
