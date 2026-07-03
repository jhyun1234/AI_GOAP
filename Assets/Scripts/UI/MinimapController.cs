/// <summary>
/// MinimapController.cs — 우측 하단 미니맵 컨트롤러
///
/// 역할(Role): FowManager의 FoW 상태 배열을 읽어 타일맵 전체 미니맵 텍스처를 생성하고,
///             주민(초록)·적(빨강)·기지(파랑) 위치를 점으로 그려 RawImage에 표시한다.
///             미탐험 타일은 검정, 탐험됨 타일은 어두운 색, 가시 타일은 원본 색으로 표시.
///
/// 사용법(Usage):
///   1. Canvas 아래 Panel("MinimapPanel")을 생성하고 앵커를 우측 하단으로 설정한다.
///   2. MinimapPanel 아래 RawImage("MinimapImage")를 배치한다.
///   3. 씬에 빈 GameObject "_MinimapController"를 생성하고 이 컴포넌트를 부착한다.
///   4. Inspector의 _minimapImage 슬롯에 MinimapImage(RawImage)를 연결한다.
///
/// 의존성(Dependencies):
///   - FowManager.cs (FoW 상태 조회 — GetFowState())
///   - MapConfig.cs (맵 크기·색상 설정)
///   - GameManager.cs (Villagers·Enemies 목록)
///
/// Script Execution Order: [DefaultExecutionOrder(-5)]
///   HUDManager(-10) 이후 실행.
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-07-03
/// </summary>

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using AIVillage.Core;
using AIVillage.AI;

namespace AIVillage.UI
{
    /// <summary>
    /// 타일맵 전체를 FoW 상태에 따라 렌더링하고 유닛 위치를 점으로 표시하는 미니맵.
    /// </summary>
    [DefaultExecutionOrder(-5)]
    [DisallowMultipleComponent]
    public sealed class MinimapController : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════════
        // 상수
        // ══════════════════════════════════════════════════════════════════════

        #region ── 상수 ──

        // 유닛 도트 반경 (픽셀 단위, 1 = 3×3 도트, 0 = 1×1 도트)
        private const int DOT_RADIUS = 1;

        // 갱신 주기 (초)
        private const float REFRESH_INTERVAL = 0.3f;

        // 유닛 색상
        private static readonly Color32 COLOR_VILLAGER = new Color32(  0, 230,  80, 255); // 초록
        private static readonly Color32 COLOR_ENEMY    = new Color32(230,  30,  30, 255); // 빨강
        private static readonly Color32 COLOR_BASE     = new Color32( 80, 160, 255, 255); // 파랑
        private static readonly Color32 COLOR_BLACK    = new Color32(  0,   0,   0, 255); // 검정 (미탐험)

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Serialized Fields (Inspector)
        // ══════════════════════════════════════════════════════════════════════

        #region ── Serialized Fields ──

        [Tooltip("미니맵을 표시할 UI RawImage. Canvas > MinimapPanel > MinimapImage에 연결.")]
        [SerializeField] private RawImage _minimapImage;

        [Tooltip("미니맵 배경 패널 (선택 사항). 초기화 중 비활성화 방지용.")]
        [SerializeField] private GameObject _minimapPanel;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private Fields
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private Fields ──

        // 미니맵 텍스처 (mapSize × mapSize, 1픽셀 = 1타일)
        private Texture2D _mapTex;

        // 픽셀 버퍼 (CPU side) — 매 갱신마다 재할당 없이 내용만 교체
        private Color32[] _pixelBuffer;

        // 맵 크기 캐시
        private int _size;    // MapConfig.Active.mapSize (예: 100)
        private int _offset;  // MapConfig.Active.mapOffset (예: 50)

        // 탐험됨(state=1) 타일의 어두운 색 — 실제 색의 약 45% 밝기
        // MapConfig의 grassColor에서 계산하여 캐시
        private Color32 _exploredGrass;
        private Color32 _exploredForest;

        // 코루틴 핸들
        private Coroutine _refreshCoroutine;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Unity 생명주기
        // ══════════════════════════════════════════════════════════════════════

        #region ── Unity 생명주기 ──

        private void Start()
        {
            // ── MapConfig 유효성 체크 ──────────────────────────────────────────
            // GameManager(-80)이 MapConfig.SetActive()를 먼저 호출하므로 Start에서 안전하게 접근 가능
            if (MapConfig.Active == null)
            {
                Debug.LogError("[MinimapController] Start: MapConfig.Active가 null입니다. " +
                               "GameManager Inspector의 _mapConfig 슬롯에 에셋이 연결됐는지 확인하세요.");
                enabled = false;
                return;
            }

            if (_minimapImage == null)
            {
                Debug.LogError("[MinimapController] Start: _minimapImage가 Inspector에 연결되지 않았습니다.");
                enabled = false;
                return;
            }

            _size   = MapConfig.Active.mapSize;
            _offset = MapConfig.Active.mapOffset;

            // ── 탐험됨 타일 색상 계산 (grassColor의 약 45% 밝기) ──────────────
            Color32 g = MapConfig.Active.grassColor;
            Color32 f = MapConfig.Active.forestColor;
            byte darkAlpha = 115; // 약 45%
            _exploredGrass  = new Color32((byte)(g.r * darkAlpha / 255),
                                          (byte)(g.g * darkAlpha / 255),
                                          (byte)(g.b * darkAlpha / 255), 255);
            _exploredForest = new Color32((byte)(f.r * darkAlpha / 255),
                                          (byte)(f.g * darkAlpha / 255),
                                          (byte)(f.b * darkAlpha / 255), 255);

            // ── Texture2D 생성 ─────────────────────────────────────────────────
            _mapTex            = new Texture2D(_size, _size, TextureFormat.RGBA32, mipChain: false);
            _mapTex.filterMode = FilterMode.Point; // 픽셀아트 스타일
            _mapTex.wrapMode   = TextureWrapMode.Clamp;

            _pixelBuffer = new Color32[_size * _size];

            _minimapImage.texture = _mapTex;

            // ── 초기 렌더링 후 코루틴 시작 ─────────────────────────────────────
            RefreshMinimap();
            _refreshCoroutine = StartCoroutine(RefreshCoroutine());

            Debug.Log($"[MinimapController] Start 완료. 텍스처={_size}×{_size}.");
        }

        private void OnDestroy()
        {
            if (_refreshCoroutine != null)
            {
                StopCoroutine(_refreshCoroutine);
                _refreshCoroutine = null;
            }

            if (_mapTex != null)
            {
                Destroy(_mapTex);
                _mapTex = null;
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // 코루틴
        // ══════════════════════════════════════════════════════════════════════

        #region ── 코루틴 ──

        /// <summary>
        /// 0.3초마다 미니맵 텍스처를 갱신하는 코루틴.
        /// </summary>
        public IEnumerator RefreshCoroutine()
        {
            WaitForSeconds wait = new WaitForSeconds(REFRESH_INTERVAL);
            while (true)
            {
                yield return wait;
                RefreshMinimap();
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // 렌더링 메서드
        // ══════════════════════════════════════════════════════════════════════

        #region ── 렌더링 메서드 ──

        /// <summary>
        /// 미니맵 텍스처 전체를 갱신한다.
        /// Step1: FoW 상태에 따라 모든 타일 픽셀을 채운다.
        /// Step2: 기지·주민·적 위치에 색상 도트를 그린다.
        /// Step3: SetPixels32 + Apply로 GPU에 업로드한다.
        /// </summary>
        private void RefreshMinimap()
        {
            if (_mapTex == null || _pixelBuffer == null) return;

            // ── Step 1: 타일 FoW 색상 ──────────────────────────────────────────
            for (int ax = 0; ax < _size; ax++)
            {
                for (int ay = 0; ay < _size; ay++)
                {
                    int tileX = ax - _offset;
                    int tileY = ay - _offset;

                    // Texture2D 픽셀 인덱스: ay * _size + ax (x-major, (0,0)=좌하단)
                    int idx = ay * _size + ax;
                    _pixelBuffer[idx] = GetTileColor(tileX, tileY);
                }
            }

            // ── Step 2: 기지 도트 (파랑 3×3) ──────────────────────────────────
            DrawDot(0, 0, COLOR_BASE);

            // ── Step 3: 주민 도트 (초록) ───────────────────────────────────────
            if (GameManager.Instance != null)
            {
                var villagers = GameManager.Instance.Villagers;
                if (villagers != null)
                {
                    for (int i = 0; i < villagers.Count; i++)
                    {
                        VillagerFSM fsm = villagers[i];
                        if (fsm == null || fsm.Brain == null || !fsm.Brain.IsAlive) continue;
                        DrawDot(fsm.Brain.TileX, fsm.Brain.TileY, COLOR_VILLAGER);
                    }
                }

                // ── Step 4: 적 도트 (빨강) ──────────────────────────────────────
                // 적은 탐험된 영역에서만 표시 (미탐험 영역의 적은 안 보임)
                var enemies = GameManager.Instance.Enemies;
                if (enemies != null)
                {
                    for (int i = 0; i < enemies.Count; i++)
                    {
                        EnemyFSM fsm = enemies[i];
                        if (fsm == null || fsm.Brain == null || !fsm.Brain.IsAlive) continue;

                        int ex = fsm.Brain.TileX;
                        int ey = fsm.Brain.TileY;

                        // FoW 확인: 탐험됨(1) 또는 가시(2)인 타일에서만 적 표시
                        byte fowState = FowManager.Instance != null
                                        ? FowManager.Instance.GetFowState(ex, ey)
                                        : (byte)0;

                        if (fowState > 0)
                            DrawDot(ex, ey, COLOR_ENEMY);
                    }
                }
            }

            // ── Step 5: GPU 업로드 ──────────────────────────────────────────────
            _mapTex.SetPixels32(_pixelBuffer);
            _mapTex.Apply(false);
        }

        /// <summary>
        /// 타일 좌표 (tileX, tileY)에 FoW 상태에 따른 색상을 반환한다.
        ///   0 = 미탐험: 검정
        ///   1 = 탐험됨: 어두운 잔디 색
        ///   2 = 가시: 잔디 색 (원본)
        /// FowManager.Instance가 null이면 전부 검정 반환.
        /// </summary>
        private Color32 GetTileColor(int tileX, int tileY)
        {
            if (FowManager.Instance == null || MapConfig.Active == null)
                return COLOR_BLACK;

            byte state = FowManager.Instance.GetFowState(tileX, tileY);

            switch (state)
            {
                case 0: return COLOR_BLACK;
                case 1: return _exploredGrass;
                case 2: return MapConfig.Active.grassColor;
                default: return COLOR_BLACK;
            }
        }

        /// <summary>
        /// 타일 좌표 (tileX, tileY)에 DOT_RADIUS 반경의 원형 도트를 그린다.
        /// 맵 범위를 벗어나면 클립처리한다.
        /// </summary>
        private void DrawDot(int tileX, int tileY, Color32 color)
        {
            // 타일 좌표 → 배열 인덱스
            int cx = tileX + _offset;
            int cy = tileY + _offset;

            for (int dx = -DOT_RADIUS; dx <= DOT_RADIUS; dx++)
            {
                for (int dy = -DOT_RADIUS; dy <= DOT_RADIUS; dy++)
                {
                    int ax = cx + dx;
                    int ay = cy + dy;

                    // 맵 범위 체크
                    if (ax < 0 || ax >= _size || ay < 0 || ay >= _size) continue;

                    _pixelBuffer[ay * _size + ax] = color;
                }
            }
        }

        #endregion
    }
}
