/// <summary>
/// FowManager.cs - Fog of War 상태 관리자
///
/// 역할(Role): 전역 byte[,] FoW 상태 배열(_fowState)을 관리한다.
///             0 = 미탐험(검정), 1 = 탐험됨(반투명 회색), 2 = 가시(원본 색상).
///             RevealArea()가 유클리드 원형으로 가시 타일을 갱신한다.
///             (Symmetric Shadowcasting LOS는 2026-08-11 철거 — 차단 지형(SetBlocking)의
///             호출자가 3개월간 0건이라 순수 원형과 동일하게 퇴화해 있었다. 경계 포함
///             6,481개 중심 × 반경 3종 전수 대조로 동치 실측 후 삭제. 건물 LOS 차단이
///             생기면 git 히스토리에서 복원한다.)
///             OnTick()에서 가시(2) → 탐험됨(1) 다운그레이드 후 주민 전체 시야 재계산.
///             MapChunkRenderer가 _dirtyMask를 읽어 변경 타일만 텍스처를 갱신한다.
///
/// 사용법(Usage):
///   1. _Managers 하위에 빈 GameObject "_FowManager"를 만들고 이 컴포넌트를 부착한다.
///   2. GameManager.Start()에서 FowManager.Instance.InitialReveal()을 호출한다.
///   3. GameManager.GameTickCoroutine()에서 FowManager.Instance?.OnTick()을 호출한다.
///   4. VillagerFSM 웨이포인트 도착 시 FowManager.Instance?.RevealArea()를 호출한다.
///
/// 의존성(Dependencies):
///   - MapConfig.cs (Active 정적 프로퍼티로 맵 크기·오프셋 읽기)
///   - GameManager.cs (Villagers 프로퍼티로 생존 주민 목록 접근)
///   - VillagerFSM.cs (Brain.TileX/Y로 주민 좌표 읽기)
///
/// Script Execution Order: [DefaultExecutionOrder(-55)]
///   GameManager(-80)이 MapConfig.SetActive()를 먼저 실행하므로
///   FowManager Awake 시점에 MapConfig.Active가 반드시 채워진다.
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-29
/// </summary>

using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.Core
{
    /// <summary>
    /// Fog of War 상태 배열을 관리하는 싱글턴 MonoBehaviour.
    /// </summary>
    [DisallowMultipleComponent]
    // [PR Fix]: ExecutionOrder 주석이 실행 순서를 반대로 기술하고 있었음. 올바른 체인으로 교체.
    // 실행 순서 체인: GameManager(-80)→FlowFieldManager(-75)→FactionAI(-70)→VillageAdvisor(-65)
    // →MapChunkRenderer(-60)→FowManager(-55)→VillagerFSM(0)
    [DefaultExecutionOrder(-55)]
    public sealed class FowManager : MonoBehaviour
    {
        #region ── 싱글턴 ──

        /// <summary>
        /// FowManager의 씬 내 유일한 인스턴스.
        /// GameManager.OnTick()과 VillagerFSM에서 이 프로퍼티로 접근한다.
        /// Awake 이전에 접근하면 null이다.
        /// </summary>
        public static FowManager Instance { get; private set; }

        #endregion

        #region ── 상수 ──

        // FoW 상태 값 (byte 타입)
        // 가독성을 위해 명명된 상수를 사용한다.
        // FOW_VISIBLE 만 public (M26-2차 A2, 2026-08-11) — 동적 개체(위협)가 "현재 시야에서만
        // 렌더"를 판정할 때 GetFowState 반환값과 비교할 이름이 필요하다. 리터럴 2를 밖에
        // 흩뿌리는 것보다 상수 노출이 싸다 (재사용 라이브러리 최소 절제).
        private const byte FOW_UNEXPLORED = 0; // 미탐험: 완전 검정
        private const byte FOW_EXPLORED   = 1; // 탐험됨(기억): 반투명 회색
        public  const byte FOW_VISIBLE    = 2; // 가시: 원본 타일 색상 100%

        #endregion

        #region ── Private Fields ──

        // ── FoW 상태 배열 (배열 인덱스 기준, 0~mapSize-1) ─────────────────────
        // _fowState[ax, ay]: 타일 (ax-offset, ay-offset)의 FoW 상태 (0·1·2)
        // byte[,] → 100×100 = 10,000 bytes = 약 10 KB (메모리 효율적)
        private byte[,] _fowState;

        // ── 더티 마스크 (MapChunkRenderer가 읽음) ──────────────────────────────
        // _dirtyMask[ax, ay] = true이면 이 타일의 픽셀이 갱신되어야 함.
        // MapChunkRenderer.RefreshDirtyChunks()에서 읽고 ClearDirty()로 초기화한다.
        private bool[,] _dirtyMask;

        // ── 맵 크기 캐시 ────────────────────────────────────────────────────────
        // MapConfig에서 한 번만 읽어 저장한다. OnTick() 내부에서 반복 접근을 피한다.
        private int _size;    // mapSize (예: 100)
        private int _offset;  // mapOffset (예: 50)

        #endregion

        #region ── 공개 프로퍼티 ──

        // [PR Fix]: 카메라 정지 시 FoW 갱신 누락 버그 수정 — HasAnyDirty 프로퍼티 추가.
        // MapChunkRenderer.Update()가 카메라 이동 여부와 무관하게 더티 타일이 있으면
        // RefreshDirtyChunks()를 호출할 수 있도록 신호를 제공한다.
        /// <summary>
        /// 갱신이 필요한 더티 타일이 하나라도 존재하는지 여부.
        /// SetVisible()에서 true로 설정되고, MapChunkRenderer가 ClearAllDirtyFlag()를
        /// 호출한 후 false로 초기화된다.
        /// MapChunkRenderer.Update()에서 이 프로퍼티를 폴링하여 카메라 정지 중에도
        /// FoW 변화를 화면에 즉시 반영할 수 있다.
        /// </summary>
        public bool HasAnyDirty { get; private set; }

        #endregion

        #region ── Unity 생명주기 ──

        /// <summary>
        /// Unity가 씬 로드 시 호출한다 (ExecutionOrder -55 보장).
        /// 싱글턴 설정과 두 배열(_fowState, _dirtyMask)을 동적 생성한다.
        /// MapConfig.Active가 반드시 먼저 설정되어 있어야 한다 (GameManager가 -80에서 SetActive).
        /// </summary>
        private void Awake()
        {
            // ── 싱글턴 중복 방지 ──────────────────────────────────────────────
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[FowManager] 중복 인스턴스 감지. 이 GameObject를 비활성화합니다.");
                gameObject.SetActive(false);
                return;
            }
            Instance = this;

            // ── MapConfig 유효성 체크 ──────────────────────────────────────────
            // GameManager(-80)이 먼저 실행되어 MapConfig.SetActive()를 호출했어야 한다.
            if (MapConfig.Active == null)
            {
                Debug.LogError("[FowManager] Awake: MapConfig.Active가 null입니다. " +
                               "GameManager.Awake()에서 MapConfig.SetActive()를 먼저 호출했는지 확인하세요. " +
                               "GameManager의 _mapConfig 슬롯에 MapConfig 에셋이 연결됐는지 확인하세요.");
                return;
            }

            // ── 맵 크기 캐시 ──────────────────────────────────────────────────
            _size   = MapConfig.Active.mapSize;
            _offset = MapConfig.Active.mapOffset;

            // ── 두 배열 동적 생성 ──────────────────────────────────────────────
            // mapSize×mapSize 크기로 생성. 기본값: byte=0(FOW_UNEXPLORED), bool=false.
            _fowState  = new byte[_size, _size];
            _dirtyMask = new bool[_size, _size];

            // 전체 맵을 더티로 초기화: 첫 번째 MapChunkRenderer.RenderAll()이 모든 픽셀을 그린다.
            MarkAllDirty();

            Debug.Log($"[FowManager] Awake 완료. 맵={_size}×{_size}, offset={_offset}. " +
                      "FoW 배열 초기화 완료 (전체 미탐험).");
        }

        /// <summary>
        /// 씬 전환 또는 GameObject 파괴 시 싱글턴 참조를 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region ── 공개 API ──

        /// <summary>
        /// 게임 시작 시 기지 주변 초기 시야를 1회 원형으로 공개한다 (RevealArea와 같은 붓 + 로그).
        /// GameManager.Start()에서 FactionAI, VillageAdvisor 초기화 이후 호출한다.
        /// </summary>
        /// <param name="baseTileX">기지 타일 X 좌표 (타일 좌표계).</param>
        /// <param name="baseTileY">기지 타일 Y 좌표 (타일 좌표계).</param>
        /// <param name="radius">초기 공개 반경 (타일 단위). MapConfig.Active.initialRevealRadius 사용.</param>
        public void InitialReveal(int baseTileX, int baseTileY, int radius)
        {
            RevealArea(baseTileX, baseTileY, radius);
            Debug.Log($"[FowManager] 초기 시야 공개 완료. 기지=({baseTileX},{baseTileY}), 반경={radius}.");
        }

        /// <summary>
        /// 중심 타일에서 반경 내 가시 타일을 유클리드 원형으로 갱신한다 (InitialReveal과 같은 붓).
        /// 에이전트 타일 이동마다 호출된다.
        /// (Shadowcasting은 2026-08-11 철거 — 파일 헤더 참조. 차단 지형이 없는 배포 상태에서
        /// 원형과 동치임을 경계 포함 전수 대조로 실측했다.)
        /// </summary>
        /// <param name="centerTileX">시야 중심 타일 X (타일 좌표계).</param>
        /// <param name="centerTileY">시야 중심 타일 Y (타일 좌표계).</param>
        /// <param name="radius">시야 반경 (타일 단위). MapConfig.Active.villagerSightRadius 사용.</param>
        public void RevealArea(int centerTileX, int centerTileY, int radius)
        {
            if (_fowState == null)
            {
                Debug.LogWarning("[FowManager] RevealArea: 배열이 초기화되지 않았습니다. " +
                                 "FowManager.Awake()가 실행됐는지 확인하세요.");
                return;
            }

            // 원형 반경 내 타일 전부 가시 — 유클리드 거리 제곱 비교 (sqrt 불필요),
            // 맵 밖 좌표는 SetVisible의 범위 검사가 거른다.
            for (int dx = -radius; dx <= radius; dx++)
                for (int dy = -radius; dy <= radius; dy++)
                    if (dx * dx + dy * dy <= radius * radius)
                        SetVisible(centerTileX + dx, centerTileY + dy);
        }

        /// <summary>
        /// GameManager의 틱 코루틴(0.1초 간격)에서 호출된다.
        /// 가시(2) 상태의 타일을 탐험됨(1)으로 다운그레이드하고,
        /// 모든 살아있는 주민의 RevealArea()를 재실행하여 현재 시야를 반영한다.
        ///
        /// 호출 시점: GameTickCoroutine()의 EnemyFSM 틱 이후, 마일스톤 체크 이전.
        /// </summary>
        public void OnTick()
        {
            if (_fowState == null) return;

            // ── Step 1: 전체 가시 타일을 탐험됨으로 다운그레이드 ─────────────
            // 이 틱 이전까지 보이던 타일은 이제 "기억된(explored)" 상태가 된다.
            // 주민이 이동한 뒤 RevealArea()가 다시 가시(2)로 올려준다.
            for (int ax = 0; ax < _size; ax++)
            {
                for (int ay = 0; ay < _size; ay++)
                {
                    if (_fowState[ax, ay] == FOW_VISIBLE)
                    {
                        _fowState[ax, ay] = FOW_EXPLORED;
                        MarkDirty(ax, ay);
                    }
                }
            }

            // ── Step 2 (W8 폐기): 舊 GameManager.Villagers 기반 시야 재계산 제거 ──
            // M0에서는 에이전트가 타일 이동마다 RevealArea()를 직접 호출한다.
            // OnTick을 다시 쓰려면 다운그레이드 후 호출자가 각 에이전트 위치에서
            // RevealArea()를 재호출할 것 (현재 M0는 미호출 — 밝힌 지역은 가시 유지).
        }

        /// <summary>
        /// 지정 타일의 FoW 상태를 반환한다.
        /// MapChunkRenderer.GetTileColor()가 픽셀 색상 결정에 사용한다.
        /// </summary>
        /// <param name="tileX">타일 X 좌표 (타일 좌표계).</param>
        /// <param name="tileY">타일 Y 좌표 (타일 좌표계).</param>
        /// <returns>0=미탐험, 1=탐험됨, 2=가시. 맵 범위 밖이면 0(미탐험) 반환.</returns>
        public byte GetFowState(int tileX, int tileY)
        {
            int ax = tileX + _offset;
            int ay = tileY + _offset;
            if (ax < 0 || ax >= _size || ay < 0 || ay >= _size) return FOW_UNEXPLORED;
            return _fowState[ax, ay];
        }

        /// <summary>
        /// 지정 타일의 더티 여부를 반환한다.
        /// MapChunkRenderer.RefreshDirtyChunks()에서 갱신이 필요한 타일을 판별할 때 사용한다.
        /// </summary>
        public bool IsDirty(int tileX, int tileY)
        {
            int ax = tileX + _offset;
            int ay = tileY + _offset;
            if (ax < 0 || ax >= _size || ay < 0 || ay >= _size) return false;
            return _dirtyMask[ax, ay];
        }

        /// <summary>
        /// 지정 타일의 더티 플래그를 초기화한다.
        /// MapChunkRenderer가 픽셀을 갱신한 직후 호출한다.
        /// </summary>
        public void ClearDirty(int tileX, int tileY)
        {
            int ax = tileX + _offset;
            int ay = tileY + _offset;
            if (ax < 0 || ax >= _size || ay < 0 || ay >= _size) return;
            _dirtyMask[ax, ay] = false;
        }

        // [PR Fix]: 카메라 정지 시 FoW 갱신 누락 버그 수정 — ClearAllDirtyFlag() 메서드 추가.
        // MapChunkRenderer.RefreshDirtyChunks() 완료 후 이 메서드를 호출하여
        // HasAnyDirty를 false로 초기화한다. 다음 SetVisible() 호출 시 다시 true가 된다.
        /// <summary>
        /// HasAnyDirty 플래그를 false로 초기화한다.
        /// MapChunkRenderer가 RefreshDirtyChunks() 완료 후 반드시 이 메서드를 호출해야 한다.
        /// 호출하지 않으면 더티 타일이 없어도 매 프레임 RefreshDirtyChunks()가 실행된다.
        /// </summary>
        public void ClearAllDirtyFlag()
        {
            HasAnyDirty = false;
        }

        #endregion

        #region ── 배열 인덱스 헬퍼 ──

        /// <summary>
        /// 지정 타일을 가시(2) 상태로 설정하고 더티 마스크를 표시한다.
        /// 이미 가시 상태이면 더티 마스크를 다시 표시하지 않는다 (불필요한 텍스처 갱신 방지).
        /// </summary>
        private void SetVisible(int tileX, int tileY)
        {
            int ax = tileX + _offset;
            int ay = tileY + _offset;

            // 배열 범위 밖이면 조용히 무시 (경계 타일 처리)
            if (ax < 0 || ax >= _size || ay < 0 || ay >= _size) return;

            // 상태가 변경될 때만 더티 마스크 설정 (이미 가시이면 갱신 불필요)
            if (_fowState[ax, ay] != FOW_VISIBLE)
                MarkDirty(ax, ay);
            _fowState[ax, ay] = FOW_VISIBLE;
        }

        /// <summary>
        /// 전체 맵의 더티 마스크를 true로 초기화한다.
        /// Awake 시 MapChunkRenderer의 첫 RenderAll()이 전체를 그리도록 유도한다.
        /// </summary>
        private void MarkAllDirty()
        {
            for (int ax = 0; ax < _size; ax++)
                for (int ay = 0; ay < _size; ay++)
                    MarkDirty(ax, ay);
        }

        // _dirtyMask와 HasAnyDirty를 항상 함께 설정하는 헬퍼.
        // OnTick Step1, SetVisible, MarkAllDirty 세 경로 모두 이 메서드를 통해 더티 플래그를 세운다.
        private void MarkDirty(int ax, int ay)
        {
            _dirtyMask[ax, ay] = true;
            HasAnyDirty        = true;
        }

        #endregion

        #region ── Unity Editor 디버그 헬퍼 ──

#if UNITY_EDITOR
        /// <summary>
        /// 씬 뷰에서 FoW 상태를 색상 기즈모로 시각화한다.
        /// FowManager GameObject를 선택했을 때만 표시된다.
        /// 5타일 간격으로 색상 점을 찍어 에디터 성능을 유지한다.
        ///
        /// 색상 범례:
        ///   검정 (0,0,0,1)     : 미탐험
        ///   회색 (0.3,0.3,0.3) : 탐험됨
        ///   초록 (0.2,0.8,0.2) : 가시
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (_fowState == null) return;

            const int step = 5; // 5타일 단위로 시각화 (성능 유지)
            for (int ax = 0; ax < _size; ax += step)
            {
                for (int ay = 0; ay < _size; ay += step)
                {
                    // FoW 상태에 따라 기즈모 색상 결정
                    switch (_fowState[ax, ay])
                    {
                        case FOW_UNEXPLORED:
                            Gizmos.color = Color.black;
                            break;
                        case FOW_EXPLORED:
                            Gizmos.color = new Color(0.3f, 0.3f, 0.3f, 0.7f);
                            break;
                        case FOW_VISIBLE:
                            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.5f);
                            break;
                        default:
                            Gizmos.color = Color.magenta; // 예상치 못한 상태: 즉시 식별 가능
                            break;
                    }

                    // 배열 인덱스 → 타일 좌표 → 씬 위치 변환
                    float wx = ax - _offset;
                    float wy = ay - _offset;
                    Gizmos.DrawCube(new Vector3(wx, wy, 0f), Vector3.one * 0.4f);
                }
            }
        }
#endif

        #endregion
    }
}
