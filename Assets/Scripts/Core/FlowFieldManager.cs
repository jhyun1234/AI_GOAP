/// <summary>
/// FlowFieldManager.cs - BFS 기반 Flow Field 생성 및 샘플링 관리자
///
/// 역할(Role): 고정 목표(0,0) 기준으로 전체 맵의 방향 벡터 그리드를 Awake에서 1회 계산한다.
///             EnemyFSM이 매 틱 GetDirection()으로 O(1)에 다음 이동 방향을 조회한다.
///             팩션 3개 모두 같은 FlowField를 공유하여 메모리/계산 비용을 절감한다.
///
/// 사용법(Usage):
///   1. 씬에 빈 GameObject를 만들고 이 컴포넌트를 부착한다. (이름: "FlowFieldManager")
///   2. Awake에서 자동으로 BuildFlowField(0, 0)을 1회 실행한다.
///   3. EnemyFSM이 FlowFieldManager.Instance.GetDirection(tileX, tileY)로 방향을 조회한다.
///
/// 의존성(Dependencies): 없음
///
/// Script Execution Order: [DefaultExecutionOrder(-75)]
///   GameManager(-80) 직후, VillagerFSM(0)/EnemyFSM(0) 이전 초기화 보장.
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-29
/// </summary>

using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.Core
{
    /// <summary>
    /// Flow Field 싱글턴 관리자.
    /// BFS로 목표(0,0)에서 역방향 전파하여 각 타일의 이동 방향 벡터를 1회 계산한다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-75)] // GameManager(-80) 직후, EnemyFSM(0) 이전 보장
    public sealed class FlowFieldManager : MonoBehaviour
    {
        #region ── 상수 ──

        // ── [13단계] MapConfig로 이전됨 ────────────────────────────────────────
        // MAP_SIZE, OFFSET은 더 이상 상수가 아니다.
        // Awake()에서 MapConfig.Active.mapSize / mapOffset으로 읽어 로컬 변수로 캐시한다.
        // 폴백값: MapConfig가 null일 때 기존 동작을 유지하기 위해 사용한다.
        private const int FALLBACK_MAP_SIZE = 100;
        private const int FALLBACK_OFFSET   = 50;

        #endregion

        #region ── 싱글턴 ──

        /// <summary>
        /// 싱글턴 인스턴스. EnemyFSM이 이 프로퍼티로 접근한다.
        /// ExecutionOrder(-75) 보장으로 EnemyFSM의 Awake/Start 이전에 설정된다.
        /// </summary>
        public static FlowFieldManager Instance { get; private set; }

        #endregion

        #region ── Private Fields ──

        // ── Flow Field 방향 배열 ──────────────────────────────────────────────
        // _flowDir[ax, ay] = 배열 좌표(ax, ay)에서 목표를 향한 이동 방향 벡터.
        // 방향 벡터 성분: x ∈ {-1, 0, 1}, y ∈ {-1, 0, 1}
        // Vector2Int.zero = 이미 목표이거나 도달 불가 타일.
        // ※ 배열 인덱스(0~mapSize-1) 기준 — 타일 좌표가 아님.
        //   GetDirection()에서 타일 좌표를 배열 인덱스로 변환 후 참조한다.
        //
        // [13단계] 필드 선언에서 고정 크기 초기화 제거.
        //   기존: private readonly Vector2Int[,] _flowDir = new Vector2Int[MAP_SIZE, MAP_SIZE];
        //   신규: Awake()에서 MapConfig.Active.mapSize를 읽어 동적으로 생성한다.
        private Vector2Int[,] _flowDir;  // Awake()에서 동적 생성

        // ── 맵 크기 캐시 (Awake에서 설정) ────────────────────────────────────
        // BuildFlowField()와 GetDirection()에서 반복 접근을 피하기 위해 필드에 저장한다.
        private int _mapSize;
        private int _offset;

        // 빌드 완료 여부 (디버그/방어용)
        private bool _isBuilt = false;

        #endregion

        #region ── Unity 생명주기 메서드 ──

        /// <summary>
        /// Unity가 씬 로드 시 가장 먼저 호출한다 (ExecutionOrder -75 보장).
        /// 싱글턴을 설정하고 Flow Field를 1회 빌드한다.
        /// </summary>
        private void Awake()
        {
            // ── 싱글턴 중복 방지 ──────────────────────────────────────────────
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[FlowFieldManager] 중복 인스턴스 감지. 이 GameObject를 비활성화합니다.");
                gameObject.SetActive(false);
                return;
            }
            Instance = this;

            // ── [13단계] MapConfig에서 맵 크기·오프셋 읽기 ──────────────────────
            // GameManager(-80)이 먼저 실행되어 MapConfig.SetActive()를 호출했으므로
            // FlowFieldManager(-75)의 Awake 시점에 MapConfig.Active가 채워져 있다.
            if (MapConfig.Active != null)
            {
                _mapSize = MapConfig.Active.mapSize;
                _offset  = MapConfig.Active.mapOffset;
            }
            else
            {
                Debug.LogWarning("[FlowFieldManager] Awake: MapConfig.Active가 null입니다. " +
                                 $"폴백값 mapSize={FALLBACK_MAP_SIZE}, offset={FALLBACK_OFFSET}을 사용합니다.");
                _mapSize = FALLBACK_MAP_SIZE;
                _offset  = FALLBACK_OFFSET;
            }

            // ── 방향 배열 동적 생성 ───────────────────────────────────────────
            // [13단계] 기존 필드 선언의 고정 크기 제거 → Awake에서 동적 생성
            _flowDir = new Vector2Int[_mapSize, _mapSize];

            // ── Flow Field 1회 빌드 ───────────────────────────────────────────
            // 목표 타일 (0, 0): 플레이어 기지 고정 위치
            // 목표가 고정이므로 Awake에서 1회만 계산 — 이후 재빌드 불필요
            BuildFlowField(targetTileX: 0, targetTileY: 0);

            Debug.Log($"[FlowFieldManager] Flow Field 빌드 완료. 목표=(0,0), " +
                      $"맵={_mapSize}×{_mapSize}, offset={_offset}, 방향=4방향 BFS");
        }

        #endregion

        #region ── 공개 메서드 ──

        /// <summary>
        /// EnemyFSM이 매 틱 호출하여 현재 타일에서 목표(0,0) 방향을 조회한다.
        /// 배열 직접 접근으로 O(1) 동작한다.
        ///
        /// 반환:
        ///   이동 방향 벡터 (각 성분 -1, 0, 1 중 하나)
        ///   Vector2Int.zero — 이미 목표에 있거나 맵 범위 밖이거나 도달 불가
        /// </summary>
        /// <param name="tileX">현재 타일 X (타일 좌표, -50~+49)</param>
        /// <param name="tileY">현재 타일 Y (타일 좌표, -50~+49)</param>
        public Vector2Int GetDirection(int tileX, int tileY)
        {
            if (!_isBuilt)
            {
                Debug.LogWarning("[FlowFieldManager] GetDirection: Flow Field가 아직 빌드되지 않았습니다.");
                return Vector2Int.zero;
            }

            // 타일 좌표 → 배열 인덱스 변환 (공식: arrayIdx = tileCoord + offset)
            // [13단계] OFFSET/MAP_SIZE 상수 → _offset/_mapSize 인스턴스 필드로 교체
            int ax = tileX + _offset;
            int ay = tileY + _offset;

            // 범위 밖 체크
            if (ax < 0 || ax >= _mapSize || ay < 0 || ay >= _mapSize)
                return Vector2Int.zero;

            return _flowDir[ax, ay];
        }

        #endregion

        #region ── 내부 메서드 ──

        /// <summary>
        /// BFS로 목표 타일에서 전체 맵으로 역방향 전파하여 Flow Field를 계산한다.
        ///
        /// 4방향 BFS (상하좌우, 대각선 제외):
        ///   팩션 집단이동에 더 균일한 흐름을 만들기 위해 대각선을 제외한다.
        ///   8방향 BFS는 구석에서 의도치 않은 빠른 경로로 인해 부대가 분산되는 문제가 있다.
        ///
        /// 알고리즘:
        ///   1. 목표 타일을 큐에 추가하고 visited 표시.
        ///   2. 큐에서 타일을 꺼내 4방향 이웃을 탐색.
        ///   3. 미방문 이웃의 방향 = 이웃에서 현재(=목표 방향)를 향하는 벡터.
        ///   4. 큐에 이웃 추가 후 반복.
        /// </summary>
        private void BuildFlowField(int targetTileX, int targetTileY)
        {
            // [13단계] MAP_SIZE/OFFSET 상수 → _mapSize/_offset 필드로 교체
            // 전체 방향 배열 초기화
            for (int x = 0; x < _mapSize; x++)
                for (int y = 0; y < _mapSize; y++)
                    _flowDir[x, y] = Vector2Int.zero;

            // 목표 배열 인덱스 변환 (arrayIdx = tileCoord + offset)
            int tax = targetTileX + _offset;
            int tay = targetTileY + _offset;

            if (tax < 0 || tax >= _mapSize || tay < 0 || tay >= _mapSize)
            {
                Debug.LogError($"[FlowFieldManager] BuildFlowField: 목표 타일({targetTileX},{targetTileY})이 " +
                               $"맵 범위를 벗어났습니다. (mapSize={_mapSize}, offset={_offset})");
                return;
            }

            // ── BFS 초기화 ────────────────────────────────────────────────────
            var visited = new bool[_mapSize, _mapSize];
            var queue   = new Queue<Vector2Int>(_mapSize * _mapSize);

            // 목표 타일 시작 (방향 = zero: 이미 목표에 있음)
            visited[tax, tay]  = true;
            _flowDir[tax, tay] = Vector2Int.zero;
            queue.Enqueue(new Vector2Int(tax, tay));

            // 4방향 오프셋 (상하좌우 — 대각선 제외)
            // 균일한 집단 이동을 위해 대각선 미사용
            int[] ndx = {  0,  0,  1, -1 };
            int[] ndy = {  1, -1,  0,  0 };

            // ── BFS 메인 루프 ─────────────────────────────────────────────────
            while (queue.Count > 0)
            {
                Vector2Int cur = queue.Dequeue();

                for (int i = 0; i < 4; i++)
                {
                    int nx = cur.x + ndx[i];
                    int ny = cur.y + ndy[i];

                    // 범위 밖 또는 이미 방문 → 건너뜀
                    // [13단계] MAP_SIZE → _mapSize 필드로 교체
                    if (nx < 0 || nx >= _mapSize || ny < 0 || ny >= _mapSize) continue;
                    if (visited[nx, ny]) continue;

                    visited[nx, ny] = true;

                    // ── 방향 벡터 계산 ──────────────────────────────────────────
                    // 이웃(nx, ny)에서 cur(목표 방향)을 향하는 방향.
                    // 공식: direction = cur - neighbor (배열 좌표 기준, OFFSET은 상쇄됨)
                    // 예) 이웃=(3,5), cur=(3,6) → direction=(0,1): 위쪽으로 이동해야 목표에 가까워짐
                    _flowDir[nx, ny] = new Vector2Int(cur.x - nx, cur.y - ny);

                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }

            _isBuilt = true;
        }

        #endregion

        #region ── Unity Editor 디버그 헬퍼 ──

#if UNITY_EDITOR
        /// <summary>
        /// 씬 뷰에서 Flow Field 방향을 시각화한다.
        /// 이 GameObject를 선택했을 때만 렌더링된다.
        /// 10칸 간격으로만 표시하여 에디터 성능을 유지한다.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!_isBuilt) return;

            if (_flowDir == null) return; // [13단계] 동적 생성 전 접근 방어
            int mapSz = (_mapSize > 0) ? _mapSize : FALLBACK_MAP_SIZE;
            int off   = (_offset  > 0) ? _offset  : FALLBACK_OFFSET;

            const int step = 10; // 10타일 단위로 시각화 (성능)
            for (int ax = 0; ax < mapSz; ax += step)
            {
                for (int ay = 0; ay < mapSz; ay += step)
                {
                    Vector2Int dir = _flowDir[ax, ay];
                    if (dir == Vector2Int.zero) continue;

                    // 배열 인덱스 → 타일 좌표로 변환하여 월드 위치 계산
                    // [13단계] OFFSET → off 로컬 변수로 교체
                    float tx = ax - off;
                    float ty = ay - off;
                    Vector3 from = new Vector3(tx, ty, 0f);
                    Vector3 to   = new Vector3(tx + dir.x * 0.4f, ty + dir.y * 0.4f, 0f);

                    UnityEditor.Handles.color = Color.cyan;
                    UnityEditor.Handles.DrawLine(from, to);
                }
            }

            // 목표 타일 표시 (초록 원)
            UnityEditor.Handles.color = Color.green;
            UnityEditor.Handles.DrawWireDisc(Vector3.zero, Vector3.forward, 1f);
        }
#endif

        #endregion
    }
}
