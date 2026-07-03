/// <summary>
/// MapConfig.cs - 맵 크기·FoW·색상 설정을 담는 ScriptableObject
///
/// 역할(Role): 맵 크기(mapSize, mapOffset), FoW 초기 탐험 반경, 주민 시야 반경,
///             이동 속도, 타일 색상 2종을 한 곳에서 관리한다.
///             MapConfig.Active 정적 프로퍼티를 통해 모든 시스템이 런타임에 접근한다.
///
/// 사용법(Usage):
///   1. Project 창 우클릭 → Create → AI Village → MapConfig
///   2. 생성된 MapConfig.asset을 GameManager Inspector의 _mapConfig 슬롯에 드래그한다.
///   3. GameManager.Awake() 첫 줄에서 MapConfig.SetActive()를 호출하면 Active가 채워진다.
///   4. 이후 어느 코드에서든 MapConfig.Active.mapSize 형식으로 접근한다.
///
/// 의존성(Dependencies): 없음 (UnityEngine만 사용)
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-29
/// </summary>

using UnityEngine;

namespace AIVillage.Core
{
    /// <summary>
    /// 맵 전역 설정을 담는 ScriptableObject.
    /// 런타임에 MapConfig.Active로 접근하여 맵 크기·FoW 수치·색상을 읽는다.
    /// </summary>
    [CreateAssetMenu(menuName = "AI Village/MapConfig", fileName = "MapConfig")]
    public class MapConfig : ScriptableObject
    {
        #region ── 정적 싱글턴 ──

        /// <summary>
        /// 현재 활성 MapConfig 에셋.
        /// GameManager.Awake()에서 SetActive()로 설정된 후 모든 시스템이 이 프로퍼티를 참조한다.
        /// null이면 SetActive()가 아직 호출되지 않은 것이므로 GameManager 초기화 순서를 확인한다.
        /// </summary>
        public static MapConfig Active { get; private set; }

        /// <summary>
        /// MapConfig.Active를 설정한다.
        /// GameManager.Awake() 첫 번째 줄에서 반드시 호출해야 한다.
        /// </summary>
        /// <param name="cfg">활성화할 MapConfig 에셋. null이면 오류 출력.</param>
        public static void SetActive(MapConfig cfg)
        {
            if (cfg == null)
            {
                Debug.LogError("[MapConfig] SetActive: null이 전달됐습니다. " +
                               "GameManager Inspector의 _mapConfig 슬롯에 에셋을 연결했는지 확인하세요.");
                return;
            }
            Active = cfg;
        }

        #endregion

        #region ── 맵 크기 ──

        [Header("맵 크기")]
        [Tooltip("타일 맵의 한 변 크기 (정사각형). 기본값 100 = 100×100 타일 맵. " +
                 "변경 시 JPSPathfinder, FlowFieldManager, FowManager의 배열 크기가 함께 변경된다.")]
        public int mapSize = 100;

        [Tooltip("타일 좌표(-50~+49)를 배열 인덱스(0~99)로 변환하는 오프셋. " +
                 "공식: arrayIdx = tileCoord + mapOffset. mapSize=100일 때 반드시 50이어야 한다.")]
        public int mapOffset = 50;   // arrayIdx = tileCoord + mapOffset

        #endregion

        #region ── 청크 렌더링 ──

        [Header("청크 렌더링")]
        [Tooltip("카메라 이동 시 갱신 판단 기준 청크 크기 (타일 단위). " +
                 "카메라가 chunkSize×0.5 타일 이상 이동하면 MapChunkRenderer가 더티 청크를 갱신한다.")]
        public int chunkSize = 16;

        #endregion

        #region ── FoW 설정 ──

        [Header("FoW 설정")]
        [Tooltip("게임 시작 시 기지(0,0) 주변 초기 공개 반경 (타일 단위). " +
                 "확정된 설계 결정 C: 15타일. Inspector에서 조정 가능.")]
        public int initialRevealRadius = 15;   // 기획서 확정 수치: 15타일

        [Tooltip("주민 한 명의 시야 반경 (Shadowcasting 기준 타일 거리). " +
                 "RevealArea() 호출 시 이 값이 radius 파라미터로 전달된다.")]
        public int villagerSightRadius = 10;   // TODO: 기획팀 수치 확인 필요 — 현재 임시값

        #endregion

        #region ── 이동 속도 ──

        [Header("이동 속도")]
        [Tooltip("주민의 기본 이동 속도 (타일/초). 기획서 확정 수치: 2.0f. " +
                 "현재 VillagerFSM.VILLAGER_MOVE_SPEED 상수와 동기화되어야 한다.")]
        public float villagerMoveSpeed = 2.0f;  // 기획서 수치: 주민 이동속도 2.0f

        [Tooltip("적 유닛의 기본 이동 속도 (타일/초). 기획서 확정 수치: 1.5f.")]
        public float enemyMoveSpeed = 1.5f;     // 기획서 수치: 적 이동속도 1.5f

        #endregion

        #region ── 타일 색상 (설계 결정 D: 2타입 색상) ──

        [Header("타일 색상 (D: 2타입 색상)")]
        [Tooltip("초원(Grass) 타일의 RGBA 색상. 기본: (100, 180, 80, 255) — 밝은 초록.")]
        public Color32 grassColor    = new Color32(100, 180,  80, 255);

        [Tooltip("숲(Forest) 타일의 RGBA 색상. 기본: (40, 110, 40, 255) — 짙은 초록. " +
                 "현재 지형 타입 시스템 없음 — MapChunkRenderer.GetBaseTileColor() 확장 시 사용됨.")]
        public Color32 forestColor   = new Color32( 40, 110,  40, 255);

        [Tooltip("FoW 미탐험(Unexplored) 색상. 완전 검정으로 가림. (0, 0, 0, 255)")]
        public Color32 fowUnexplored = new Color32(  0,   0,   0, 255);

        [Tooltip("FoW 탐험됨(Explored·기억) 색상. 반투명 회색으로 어둡게 표시. (80, 80, 80, 180). " +
                 "Alpha값(180)이 어둠의 강도를 결정한다 — 값이 클수록 원본 색에 가까워진다.")]
        public Color32 fowExplored   = new Color32( 80,  80,  80, 180);

        #endregion
    }
}
