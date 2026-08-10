using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.Core
{
    /// <summary>
    /// A* 경로탐색 (M26-1차 W1, ADR-T-2) — **가중치 네이티브**.
    ///
    /// 🔑 **왜 JPS 를 두고 이걸 만드나**: JPS 는 "모든 칸 비용 균일" 대칭성을 전제로 점프
    /// 가지치기를 한다. 지형 축이 늪(느린 땅)을 들이는 순간 그 전제가 깨져 바닐라 JPS 는 못 쓴다
    /// (`Docs/ADR_경로탐색_확장경계.md` 트리거 A).
    ///
    /// 🔑 **그런데 실측상 교체가 손해가 아니라 이득이었다** (2026-08-10, 우리 맵 질의 200건):
    ///   빈 벌판 JPS 0.224ms / A* **0.013ms** · 장애물 12% 0.050 / **0.020** · 호수·절벽 0.062 / **0.026**.
    ///   맵이 100×100 으로 작고 뻥 뚫려 있어 JPS 의 점프 스캔이 맵 끝까지 달려 손해를 보는 반면,
    ///   A* 는 옥타일 휴리스틱이 거의 완벽해 평균 **66칸**만 열고 도착한다.
    ///   → ADR 원문이 요구한 *"알고리즘 최종 선택은 프로파일링 실측 후"* 를 밟은 결과다.
    ///
    /// 🔴 **JPS 는 지우지 않는다** — 되돌릴 길이고, 맵이 커져 HPA* 를 얹을 때 균일 비용 구역의
    /// 내부 solver 로 다시 쓴다 (ADR 원문 "JPS 위에 HPA*").
    ///
    /// ⚠️ 버퍼를 인스턴스가 **재사용**하므로 **스레드 안전하지 않다** (M0 는 단일 스레드).
    /// 재사용은 취향이 아니라 위 실측의 전제다 — 매 호출 할당하면 그 수치가 무엇을 잰 것인지
    /// 모호해진다 (게이트 `M26_T3` 가 예산을 지킨다).
    /// </summary>
    public sealed class AStarPathfinder : IPathfinder
    {
        // 이동 비용 — JPS 와 **같은 값**을 쓴다. 다르면 같은 맵에서 두 구현의 답이 갈린다
        // (게이트 M26_T2 가 그 동일성을 지킨다).
        private const float DIAGONAL_COST = 1.414f;
        private const float STRAIGHT_COST = 1.0f;

        // MapConfig 부재 시 폴백 — JPS 와 같은 수 (배선 전 동작 동일)
        private const int FALLBACK_MAP_SIZE = 100;
        private const int FALLBACK_OFFSET   = 50;

        private const byte NEW = 0, OPEN = 1, CLOSED = 2;

        private readonly Func<bool[,]> _walkableSource;
        private readonly Func<int, int, float> _enterCost; // 타일 좌표 → 진입 비용 배수. null = 균일

        // ── 재사용 버퍼 (맵 크기가 바뀌면 다시 잡는다) ──
        private int _size;
        private float[] _g;
        private int[] _parent;
        private byte[] _state;
        private int[] _heapNode;
        private float[] _heapF;

        /// <param name="walkableSource">통행 배열 지연 조회 — 건설로 갱신돼도 항상 최신을 본다
        /// (JpsPathfinder 와 같은 규약).</param>
        /// <param name="enterCost">그 타일에 **들어가는** 비용 배수 (1 = 평지, 3 = 늪).
        /// null 이면 전부 1 = 균일 = **JPS 와 같은 답** (중립 불변식, 게이트 `M26_T2`).
        /// ⚠️ 1 미만이면 휴리스틱이 과대평가가 되어 최단 경로가 깨진다 — 에셋이 `[Min(1f)]` 로 막는다.</param>
        public AStarPathfinder(Func<bool[,]> walkableSource, Func<int, int, float> enterCost = null)
        {
            _walkableSource = walkableSource ?? throw new ArgumentNullException(nameof(walkableSource));
            _enterCost = enterCost;
        }

        public PathResult FindPath(int startX, int startY, int goalX, int goalY)
        {
            bool[,] walkable = _walkableSource();
            int mapSize = MapConfig.Active != null ? MapConfig.Active.mapSize   : FALLBACK_MAP_SIZE;
            int offset  = MapConfig.Active != null ? MapConfig.Active.mapOffset : FALLBACK_OFFSET;

            // ── 입력 방어 — JPS 와 **같은 분기·같은 경고문**을 낸다 (교체가 조용히 동작을 바꾸지 않게) ──
            if (walkable == null)
            {
                Debug.LogWarning("[AStarPathfinder] walkable 배열이 null입니다.");
                return PathResult.Unreachable();
            }
            if (startX == goalX && startY == goalY) return PathResult.AlreadyThere();

            int sax = startX + offset, say = startY + offset;
            int gax = goalX + offset,  gay = goalY + offset;
            if (!InBounds(sax, say, mapSize))
            {
                Debug.LogWarning($"[AStarPathfinder] 시작 위치 맵 범위 초과: tile({startX},{startY})");
                return PathResult.Unreachable();
            }
            if (!InBounds(gax, gay, mapSize))
            {
                Debug.LogWarning($"[AStarPathfinder] 목표 위치 맵 범위 초과: tile({goalX},{goalY})");
                return PathResult.Unreachable();
            }
            if (!walkable[gax, gay])
            {
                Debug.LogWarning($"[AStarPathfinder] 목표 타일이 이동 불가: tile({goalX},{goalY})");
                return PathResult.Unreachable();
            }

            EnsureBuffers(mapSize);
            Array.Clear(_state, 0, _state.Length);

            int start = sax * mapSize + say, goal = gax * mapSize + gay;
            int heapCount = 0;
            _g[start] = 0f;
            _state[start] = OPEN;
            Push(ref heapCount, start, Heuristic(sax, say, gax, gay));

            while (heapCount > 0)
            {
                int cur = Pop(ref heapCount);
                if (_state[cur] == CLOSED) continue;   // 힙에 남은 낡은 항목 (감소키 대신 재삽입)
                _state[cur] = CLOSED;
                if (cur == goal) return Reconstruct(start, goal, mapSize, offset);

                int cx = cur / mapSize, cy = cur % mapSize;
                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + DX[k], ny = cy + DY[k];
                    if (!InBounds(nx, ny, mapSize) || !walkable[nx, ny]) continue;
                    // 🔑 모서리 자르기를 **막지 않는다** — JPS 의 Jump 가 목표 칸만 보고 대각을
                    //    허용하므로, 여기서 막으면 같은 맵에서 두 구현의 경로 길이가 갈린다.
                    int ni = nx * mapSize + ny;
                    if (_state[ni] == CLOSED) continue;

                    float step = (k >= 4 ? DIAGONAL_COST : STRAIGHT_COST) * EnterCostAt(nx, ny, offset);
                    float ng = _g[cur] + step;
                    if (_state[ni] == OPEN && ng >= _g[ni]) continue;

                    _g[ni] = ng;
                    _parent[ni] = cur;
                    _state[ni] = OPEN;
                    Push(ref heapCount, ni, ng + Heuristic(nx, ny, gax, gay));
                }
            }
            return PathResult.Unreachable();
        }

        /// <summary>진입 비용 (타일 좌표로 물어본다 — 배선 쪽이 배열 인덱스를 몰라도 되게).
        /// 미배선이면 1 = 균일. 1 미만은 1로 올린다 — 휴리스틱 과대평가로 최단성이 깨지는 것을
        /// 여기서 막는다 (에셋 실수 방어).</summary>
        private float EnterCostAt(int ax, int ay, int offset)
        {
            if (_enterCost == null) return 1f;
            float c = _enterCost(ax - offset, ay - offset);
            return c < 1f ? 1f : c;
        }

        /// <summary>옥타일 거리 — 대각 비용이 `DIAGONAL_COST` 인 8방향 격자의 허용 휴리스틱.
        /// 최소 진입 비용이 1 이므로 비용이 붙어도 과대평가가 되지 않는다 (최단성 유지).</summary>
        private static float Heuristic(int x, int y, int gx, int gy)
        {
            int dx = x > gx ? x - gx : gx - x;
            int dy = y > gy ? y - gy : gy - y;
            int lo = dx < dy ? dx : dy;
            return STRAIGHT_COST * (dx + dy) + (DIAGONAL_COST - 2f * STRAIGHT_COST) * lo;
        }

        private PathResult Reconstruct(int start, int goal, int mapSize, int offset)
        {
            var rev = new List<Vector2Int>(64);
            int cur = goal;
            int guard = mapSize * mapSize;   // 부모 사슬이 꼬여도 멈춘다 (JPS 의 maxPathNodes 동형)
            while (cur != start && guard-- > 0)
            {
                rev.Add(new Vector2Int(cur / mapSize - offset, cur % mapSize - offset));
                cur = _parent[cur];
            }
            if (cur != start)
            {
                Debug.LogWarning("[AStarPathfinder] 경로 역추적이 시작점에 닿지 못했습니다.");
                return PathResult.Unreachable();
            }
            rev.Reverse();                    // 계약: 시작 제외 · 목표 포함 · 타일 좌표
            return PathResult.Found(rev);
        }

        private void EnsureBuffers(int mapSize)
        {
            if (_size == mapSize && _g != null) return;
            _size = mapSize;
            int n = mapSize * mapSize;
            _g = new float[n];
            _parent = new int[n];
            _state = new byte[n];
            _heapNode = new int[n + 8];
            _heapF = new float[n + 8];
        }

        // ── 이진 힙 ── 동률은 **노드 번호 오름차순**으로 깬다 (결정성 — ADR-M10R-2).
        //    번호 = ax * mapSize + ay 라 곧 좌표순이다. 이 규칙이 없으면 같은 입력에 다른 길이 나온다.
        private void Push(ref int count, int node, float f)
        {
            _heapNode[count] = node; _heapF[count] = f;
            int c = count++;
            while (c > 0)
            {
                int p = (c - 1) >> 1;
                if (!Less(c, p)) break;
                Swap(c, p); c = p;
            }
        }

        private int Pop(ref int count)
        {
            int best = _heapNode[0];
            count--;
            _heapNode[0] = _heapNode[count]; _heapF[0] = _heapF[count];
            int c = 0;
            while (true)
            {
                int l = 2 * c + 1, r = l + 1, m = c;
                if (l < count && Less(l, m)) m = l;
                if (r < count && Less(r, m)) m = r;
                if (m == c) break;
                Swap(c, m); c = m;
            }
            return best;
        }

        private bool Less(int a, int b)
            => _heapF[a] < _heapF[b] || (_heapF[a] == _heapF[b] && _heapNode[a] < _heapNode[b]);

        private void Swap(int a, int b)
        {
            int tn = _heapNode[a]; _heapNode[a] = _heapNode[b]; _heapNode[b] = tn;
            float tf = _heapF[a]; _heapF[a] = _heapF[b]; _heapF[b] = tf;
        }

        private static bool InBounds(int ax, int ay, int mapSize)
            => ax >= 0 && ay >= 0 && ax < mapSize && ay < mapSize;

        // 직교 4 + 대각 4 (인덱스 ≥ 4 = 대각)
        private static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] DY = { 0, 0, 1, -1, 1, -1, 1, -1 };
    }
}
