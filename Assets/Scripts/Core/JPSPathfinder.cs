/// <summary>
/// JPSPathfinder.cs - JPS(Jump Point Search) 경로 탐색기
///
/// 역할(Role): 균일 비용 2D 그리드에서 Jump Point Search 알고리즘으로 최단 경로를 탐색한다.
///             8방향 이동을 지원하며 직선/대각선 점프 규칙을 적용한다.
///             VillagerFSM이 StartPathTo()에서 호출하여 웨이포인트 목록을 받는다.
///
/// 사용법(Usage):
///   List<Vector2Int> path = JPSPathfinder.FindPath(startX, startY, goalX, goalY, walkable);
///   - path == null  : 경로 없음 (목표에 도달 불가)
///   - path.Count==0 : 이미 목표에 있음 (시작 == 목표)
///   - path[0]       : 첫 번째 이동 웨이포인트 (시작 타일 미포함, 목표 타일 포함)
///
/// 의존성(Dependencies): 없음 (UnityEngine.Vector2Int / Mathf만 사용)
///
/// 좌표 규칙:
///   - 외부에서 타일 좌표(-50 ~ +49)로 호출한다.
///   - 내부적으로 arrayIdx = tileCoord + OFFSET(50) 으로 변환하여 배열에 접근한다.
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-29
/// </summary>

using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.Core
{
    /// <summary>
    /// JPS(Jump Point Search) 정적 유틸리티 클래스.
    /// MonoBehaviour가 아니므로 씬에 배치할 필요 없다.
    /// </summary>
    public static class JPSPathfinder
    {
        #region ── 상수 ──

        // ── [13단계] MapConfig로 이전됨 ────────────────────────────────────────
        // MAP_SIZE, OFFSET은 MapConfig.Active.mapSize / mapOffset으로 동적 읽기로 교체됨.
        // FindPath() 호출 시 로컬 변수로 읽어 사용한다.

        // 이동 비용
        private const float DIAGONAL_COST = 1.414f; // sqrt(2) — 대각선 1칸 비용
        private const float STRAIGHT_COST = 1.0f;   // 직선 1칸 비용

        // 유효하지 않은 점프 포인트를 나타내는 센티넬 값 (x == INVALID_SENTINEL)
        private const int INVALID_SENTINEL = -1;

        // 안전 기본값: MapConfig가 null일 때 폴백으로 사용 (기존 하드코딩 값 유지)
        private const int FALLBACK_MAP_SIZE = 100;
        private const int FALLBACK_OFFSET   = 50;

        #endregion

        #region ── 공개 메서드 ──

        /// <summary>
        /// JPS 알고리즘으로 시작 타일에서 목표 타일까지의 경로를 탐색한다.
        ///
        /// 반환값:
        ///   null        - 경로 없음 (목표에 도달 불가)
        ///   Count == 0  - 이미 목표에 있음 (시작 == 목표)
        ///   Count >  0  - 웨이포인트 목록 (시작 제외, 목표 포함, 중간 타일 보간 포함)
        /// </summary>
        /// <param name="startX">시작 타일 X (타일 좌표, -50~+49)</param>
        /// <param name="startY">시작 타일 Y (타일 좌표, -50~+49)</param>
        /// <param name="goalX">목표 타일 X (타일 좌표, -50~+49)</param>
        /// <param name="goalY">목표 타일 Y (타일 좌표, -50~+49)</param>
        /// <param name="walkable">100×100 통행 가능 여부 배열 (배열 인덱스 기준)</param>
        public static List<Vector2Int> FindPath(
            int startX, int startY,
            int goalX,  int goalY,
            bool[,] walkable)
        {
            // ── [13단계] MapConfig에서 맵 크기와 오프셋을 동적으로 읽는다 ────────
            // MapConfig.Active가 null이면 폴백값(100, 50)을 사용하여 기존 동작을 유지한다.
            int mapSize = (MapConfig.Active != null) ? MapConfig.Active.mapSize   : FALLBACK_MAP_SIZE;
            int offset  = (MapConfig.Active != null) ? MapConfig.Active.mapOffset : FALLBACK_OFFSET;

            // MAX_JUMP_STEPS, MAX_PATH_NODES도 동적 계산
            // 기존에는 상수로 고정됐으나 mapSize 변경에 대응하기 위해 로컬 변수로 교체한다.
            int maxJumpSteps = mapSize;            // Jump() 최대 반복 횟수
            int maxPathNodes = mapSize * mapSize;  // ReconstructPath 최대 반복 횟수

            // ── 입력 방어 ──────────────────────────────────────────────────────
            if (walkable == null)
            {
                Debug.LogWarning("[JPSPathfinder] walkable 배열이 null입니다.");
                return null;
            }

            // 이미 목표에 있는 경우 — 이동 불필요
            if (startX == goalX && startY == goalY)
                return new List<Vector2Int>(); // Count == 0

            // 타일 좌표 → 배열 인덱스 변환
            int sax = startX + offset;
            int say = startY + offset;
            int gax = goalX  + offset;
            int gay = goalY  + offset;

            if (!IsInBounds(sax, say, mapSize))
            {
                Debug.LogWarning($"[JPSPathfinder] 시작 위치 맵 범위 초과: tile({startX},{startY})");
                return null;
            }
            if (!IsInBounds(gax, gay, mapSize))
            {
                Debug.LogWarning($"[JPSPathfinder] 목표 위치 맵 범위 초과: tile({goalX},{goalY})");
                return null;
            }
            if (!walkable[gax, gay])
            {
                Debug.LogWarning($"[JPSPathfinder] 목표 타일이 이동 불가: tile({goalX},{goalY})");
                return null;
            }

            // ── 자료구조 초기화 ────────────────────────────────────────────────
            // g비용: 시작에서 각 점프 포인트까지의 실제 이동 거리
            var gCost  = new Dictionary<Vector2Int, float>();
            // 경로 역추적용 부모 맵 (점프 포인트 간 연결)
            var parent = new Dictionary<Vector2Int, Vector2Int>();
            // 처리 완료 집합
            var closed = new HashSet<Vector2Int>();
            // 오픈 리스트: (f비용, 위치) 쌍 — 배열 좌표 기준
            var openList = new List<(float f, Vector2Int pos)>(64);

            var startNode = new Vector2Int(sax, say);
            var goalNode  = new Vector2Int(gax, gay);

            gCost[startNode] = 0f;
            // 시작 노드의 parent를 자기 자신으로 설정 → GetSuccessors에서 "모든 방향 탐색" 분기 진입
            parent[startNode] = startNode;
            openList.Add((Heuristic(sax, say, gax, gay), startNode));

            // ── JPS A* 메인 루프 ──────────────────────────────────────────────
            while (openList.Count > 0)
            {
                // 최소 f비용 노드 추출 (선형 탐색 — 100×100 맵에서 충분)
                int bestIdx = 0;
                for (int i = 1; i < openList.Count; i++)
                {
                    if (openList[i].f < openList[bestIdx].f)
                        bestIdx = i;
                }
                var (_, current) = openList[bestIdx];
                openList.RemoveAt(bestIdx);

                // 목표 도달
                if (current == goalNode)
                    return ReconstructAndExpand(parent, startNode, goalNode, offset, maxPathNodes);

                if (closed.Contains(current)) continue;
                closed.Add(current);

                // 후계자(점프 포인트) 탐색
                List<Vector2Int> successors = GetSuccessors(
                    current.x, current.y,
                    parent[current].x, parent[current].y,
                    walkable, gax, gay, mapSize, maxJumpSteps);

                foreach (Vector2Int jp in successors)
                {
                    if (closed.Contains(jp)) continue;

                    // 현재 → jp 이동 비용
                    int adx = jp.x - current.x;
                    int ady = jp.y - current.y;
                    int diagSteps     = System.Math.Min(System.Math.Abs(adx), System.Math.Abs(ady));
                    int straightSteps = System.Math.Abs(System.Math.Abs(adx) - System.Math.Abs(ady));
                    float dist = diagSteps * DIAGONAL_COST + straightSteps * STRAIGHT_COST;

                    float newG = gCost[current] + dist;

                    if (!gCost.ContainsKey(jp) || newG < gCost[jp])
                    {
                        gCost[jp]  = newG;
                        parent[jp] = current;
                        float f = newG + Heuristic(jp.x, jp.y, gax, gay);
                        openList.Add((f, jp));
                    }
                }
            }

            // 목표에 도달한 경우 경로 역추적 (maxPathNodes도 전달)
            // 이 return은 while 루프 안 "if (current == goalNode)" 분기에서 이미 처리됨
            // 경로 없음
            return null;
        }

        #endregion

        #region ── 내부 메서드 ──

        /// <summary>
        /// 배열 좌표가 맵 범위 내에 있는지 확인한다.
        /// [13단계] mapSize 파라미터로 동적 범위를 지원한다.
        /// </summary>
        private static bool IsInBounds(int ax, int ay, int mapSize)
        {
            return ax >= 0 && ax < mapSize && ay >= 0 && ay < mapSize;
        }

        // [13단계] 이전 IsInBounds(int, int) 오버로드 — MapConfig 기반 동적 호출용
        // GetSuccessors/Jump 내부에서 mapSize를 전달받아 사용한다.
        private static bool IsInBounds(int ax, int ay)
        {
            // 폴백: MapConfig 없을 때 FALLBACK_MAP_SIZE 사용
            int ms = (MapConfig.Active != null) ? MapConfig.Active.mapSize : FALLBACK_MAP_SIZE;
            return ax >= 0 && ax < ms && ay >= 0 && ay < ms;
        }

        /// <summary>
        /// 배열 좌표가 유효하고 통행 가능한지 확인한다.
        /// 범위 밖이면 false를 반환하여 경계를 장애물처럼 처리한다.
        /// </summary>
        private static bool IsWalkable(int ax, int ay, bool[,] walkable)
        {
            if (!IsInBounds(ax, ay)) return false;
            return walkable[ax, ay];
        }

        /// <summary>
        /// 옥타일 거리(Octile Distance) 휴리스틱.
        /// 8방향 이동에서 A*의 허용적(admissible) 휴리스틱으로 사용한다.
        /// 공식: D × (dx + dy) + (D2 - 2D) × min(dx, dy)
        ///   D  = 직선 비용 (1.0f), D2 = 대각선 비용 (1.414f)
        /// </summary>
        private static float Heuristic(int ax, int ay, int gax, int gay)
        {
            int dx = Mathf.Abs(ax - gax);
            int dy = Mathf.Abs(ay - gay);
            return STRAIGHT_COST * (dx + dy) + (DIAGONAL_COST - 2f * STRAIGHT_COST) * Mathf.Min(dx, dy);
        }

        /// <summary>
        /// 현재 노드의 JPS 후계자(Jump Point) 목록을 반환한다.
        /// 부모 방향을 기반으로 불필요한 이웃을 pruning하고 각 방향으로 Jump()를 호출한다.
        ///
        /// JPS Pruning 규칙:
        ///   - 직선 이동: 부모 방향으로만 탐색 + 강제 이웃(forced neighbor) 방향 추가
        ///   - 대각선 이동: 수평/수직 성분 + 대각선 방향 탐색
        ///   - 시작 노드 (parent == self): 모든 8방향 탐색
        /// </summary>
        private static List<Vector2Int> GetSuccessors(
            int cx, int cy,       // 현재 노드 (배열 좌표)
            int px, int py,       // 부모 노드 (배열 좌표)
            bool[,] walkable,
            int gax, int gay,     // 목표 (배열 좌표)
            int mapSize,          // [13단계] 동적 맵 크기
            int maxJumpSteps)     // [13단계] Jump() 최대 반복 횟수
        {
            var successors = new List<Vector2Int>(8);

            // 부모 → 현재 방향 정규화: 각 성분을 -1, 0, 1 중 하나로
            int dx = (cx == px) ? 0 : (cx - px) / System.Math.Abs(cx - px);
            int dy = (cy == py) ? 0 : (cy - py) / System.Math.Abs(cy - py);

            // ── 시작 노드: 모든 8방향 탐색 ────────────────────────────────────
            // parent == self이면 dx==0, dy==0 → 전방위 탐색
            if (dx == 0 && dy == 0)
            {
                int[] ds = { -1, 0, 1 };
                foreach (int ddx in ds)
                {
                    foreach (int ddy in ds)
                    {
                        if (ddx == 0 && ddy == 0) continue;
                        var jp = Jump(cx, cy, ddx, ddy, gax, gay, walkable, mapSize, maxJumpSteps);
                        if (jp.x != INVALID_SENTINEL) successors.Add(jp);
                    }
                }
                return successors;
            }

            // ── 수평 이동 (dy == 0) ────────────────────────────────────────────
            if (dy == 0)
            {
                // 자연 이웃: 직선 방향
                var jp = Jump(cx, cy, dx, 0, gax, gay, walkable, mapSize, maxJumpSteps);
                if (jp.x != INVALID_SENTINEL) successors.Add(jp);

                // 강제 이웃: 상하 장애물이 있으면 대각선 방향 추가
                if (!IsWalkable(cx, cy + 1, walkable) && IsWalkable(cx + dx, cy + 1, walkable))
                {
                    var fjp = Jump(cx, cy, dx, 1, gax, gay, walkable, mapSize, maxJumpSteps);
                    if (fjp.x != INVALID_SENTINEL) successors.Add(fjp);
                }
                if (!IsWalkable(cx, cy - 1, walkable) && IsWalkable(cx + dx, cy - 1, walkable))
                {
                    var fjp = Jump(cx, cy, dx, -1, gax, gay, walkable, mapSize, maxJumpSteps);
                    if (fjp.x != INVALID_SENTINEL) successors.Add(fjp);
                }
                return successors;
            }

            // ── 수직 이동 (dx == 0) ────────────────────────────────────────────
            if (dx == 0)
            {
                // 자연 이웃: 직선 방향
                var jp = Jump(cx, cy, 0, dy, gax, gay, walkable, mapSize, maxJumpSteps);
                if (jp.x != INVALID_SENTINEL) successors.Add(jp);

                // 강제 이웃: 좌우 장애물이 있으면 대각선 방향 추가
                if (!IsWalkable(cx + 1, cy, walkable) && IsWalkable(cx + 1, cy + dy, walkable))
                {
                    var fjp = Jump(cx, cy, 1, dy, gax, gay, walkable, mapSize, maxJumpSteps);
                    if (fjp.x != INVALID_SENTINEL) successors.Add(fjp);
                }
                if (!IsWalkable(cx - 1, cy, walkable) && IsWalkable(cx - 1, cy + dy, walkable))
                {
                    var fjp = Jump(cx, cy, -1, dy, gax, gay, walkable, mapSize, maxJumpSteps);
                    if (fjp.x != INVALID_SENTINEL) successors.Add(fjp);
                }
                return successors;
            }

            // ── 대각선 이동 (dx != 0 && dy != 0) ─────────────────────────────
            // 수평/수직 성분을 먼저 점프 시도하고, 대각선 방향도 점프한다.
            {
                var hjp = Jump(cx, cy, dx, 0,  gax, gay, walkable, mapSize, maxJumpSteps);
                if (hjp.x != INVALID_SENTINEL) successors.Add(hjp);

                var vjp = Jump(cx, cy, 0,  dy, gax, gay, walkable, mapSize, maxJumpSteps);
                if (vjp.x != INVALID_SENTINEL) successors.Add(vjp);

                var djp = Jump(cx, cy, dx, dy, gax, gay, walkable, mapSize, maxJumpSteps);
                if (djp.x != INVALID_SENTINEL) successors.Add(djp);
            }
            return successors;
        }

        /// <summary>
        /// 주어진 방향으로 점프를 수행하여 Jump Point를 찾는다.
        ///
        /// Jump Point 조건:
        ///   1. 목표 노드 자체 → 즉시 반환
        ///   2. 강제 이웃(forced neighbor)이 있는 노드
        ///   3. 대각선 방향: 수평/수직 성분 점프에서 JP가 발견된 노드
        ///
        /// 반환: 발견된 Jump Point의 배열 좌표.
        ///       x == INVALID_SENTINEL(-1)이면 유효한 JP 없음.
        /// </summary>
        private static Vector2Int Jump(
            int cx, int cy,       // 현재 위치 (배열 좌표)
            int dx, int dy,       // 이동 방향 (-1, 0, 1)
            int gax, int gay,     // 목표 (배열 좌표)
            bool[,] walkable,
            int mapSize,          // [13단계] 동적 맵 크기
            int maxJumpSteps)     // [13단계] 최대 반복 횟수 (기존 MAX_JUMP_STEPS 대체)
        {
            var INVALID = new Vector2Int(INVALID_SENTINEL, INVALID_SENTINEL);

            for (int step = 0; step < maxJumpSteps; step++)
            {
                int nx = cx + dx;
                int ny = cy + dy;

                // 경계 밖이거나 이동 불가 → 점프 실패
                // [13단계] IsInBounds에 mapSize 전달
                if (!IsInBounds(nx, ny, mapSize) || !walkable[nx, ny])
                    return INVALID;

                // 목표 발견 → 즉시 반환
                if (nx == gax && ny == gay)
                    return new Vector2Int(nx, ny);

                // ── 강제 이웃 체크: 수평 이동 ──────────────────────────────────
                if (dx != 0 && dy == 0)
                {
                    // 수평 이동 중, 위아래에 장애물 + 대각선 통과 가능 → 강제 이웃
                    bool fUp   = !IsWalkable(cx, ny + 1, walkable) && IsWalkable(nx, ny + 1, walkable);
                    bool fDown = !IsWalkable(cx, ny - 1, walkable) && IsWalkable(nx, ny - 1, walkable);
                    if (fUp || fDown) return new Vector2Int(nx, ny);
                }
                // ── 강제 이웃 체크: 수직 이동 ──────────────────────────────────
                else if (dx == 0 && dy != 0)
                {
                    // 수직 이동 중, 좌우에 장애물 + 대각선 통과 가능 → 강제 이웃
                    bool fRight = !IsWalkable(nx + 1, cy, walkable) && IsWalkable(nx + 1, ny, walkable);
                    bool fLeft  = !IsWalkable(nx - 1, cy, walkable) && IsWalkable(nx - 1, ny, walkable);
                    if (fRight || fLeft) return new Vector2Int(nx, ny);
                }
                // ── 대각선 이동: 수평/수직 성분 점프 시도 ─────────────────────
                else
                {
                    // 수평 또는 수직 방향으로 점프 포인트가 발견되면 현재 위치가 JP
                    // [13단계] mapSize와 maxJumpSteps를 재귀 호출에도 전달한다.
                    var hj = Jump(nx, ny, dx, 0,  gax, gay, walkable, mapSize, maxJumpSteps);
                    var vj = Jump(nx, ny, 0,  dy, gax, gay, walkable, mapSize, maxJumpSteps);
                    if (hj.x != INVALID_SENTINEL || vj.x != INVALID_SENTINEL)
                        return new Vector2Int(nx, ny);
                }

                // 다음 위치로 전진
                cx = nx;
                cy = ny;
            }

            return INVALID;
        }

        /// <summary>
        /// parent 딕셔너리를 역추적하여 Jump Point 목록을 복원한 뒤,
        /// 점프 포인트 사이의 중간 타일을 보간하여 완전한 타일 단위 경로를 반환한다.
        ///
        /// 반환 좌표: 배열 좌표 - OFFSET = 타일 좌표 (-50~+49)
        /// 경로에 시작 타일은 포함되지 않고 목표 타일은 포함된다.
        /// </summary>
        private static List<Vector2Int> ReconstructAndExpand(
            Dictionary<Vector2Int, Vector2Int> parent,
            Vector2Int startNode,
            Vector2Int goalNode,
            int offset,          // [13단계] 동적 오프셋 (MapConfig.Active.mapOffset)
            int maxPathNodes)    // [13단계] 최대 반복 횟수 (기존 MAX_PATH_NODES 대체)
        {
            // ── 1단계: Jump Point 목록 역추적 ────────────────────────────────
            var jumpPoints = new List<Vector2Int>(32);
            var current    = goalNode;
            int safety     = maxPathNodes; // [13단계] 동적 맵 크기 기반 최대 반복 횟수

            while (current != startNode && safety-- > 0)
            {
                // 배열 좌표 → 타일 좌표 변환: arrayIdx - offset = tileCoord
                jumpPoints.Add(new Vector2Int(current.x - offset, current.y - offset));

                if (!parent.ContainsKey(current))
                {
                    Debug.LogWarning("[JPSPathfinder] ReconstructAndExpand: parent에 없는 노드.");
                    break;
                }
                current = parent[current];
            }

            // 역방향 → 정방향
            jumpPoints.Reverse();

            if (jumpPoints.Count == 0) return jumpPoints;

            // ── 2단계: Jump Point 사이 중간 타일 보간 ─────────────────────────
            // JPS는 JP만 반환하고 중간 타일을 건너뛰므로,
            // VillagerFSM에서 Brain.TileX/Y를 웨이포인트 도착 시마다 갱신하려면
            // 중간 타일도 경로에 포함되어야 한다.
            //
            // 보간 방법: 직선 브레젠험(체비쇼프 걸음)
            //   대각선 우선, 나머지 직선 — 1타일씩 이동
            var expanded = new List<Vector2Int>(jumpPoints.Count * 8);

            // 시작 타일(startNode)에서 첫 JP까지 보간
            // startNode는 Brain 현재 위치이므로 경로에 포함하지 않는다.
            int prevX = startNode.x - offset; // 시작 타일 좌표 (arrayIdx - offset = tileCoord)
            int prevY = startNode.y - offset;

            foreach (Vector2Int jp in jumpPoints)
            {
                int toX = jp.x;
                int toY = jp.y;
                int curX = prevX;
                int curY = prevY;

                // curX/curY → toX/toY를 1타일씩 이동하며 중간 타일 추가
                while (curX != toX || curY != toY)
                {
                    if (curX != toX) curX += (toX > curX) ? 1 : -1;
                    if (curY != toY) curY += (toY > curY) ? 1 : -1;
                    expanded.Add(new Vector2Int(curX, curY));
                }

                prevX = toX;
                prevY = toY;
            }

            return expanded;
        }

        #endregion
    }
}
