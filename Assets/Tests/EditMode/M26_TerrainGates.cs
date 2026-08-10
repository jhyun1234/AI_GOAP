using System.Collections.Generic;
using AIVillage.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;   // LogAssert — 입력 방어 경고를 무시하고 종류만 본다

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M26 지형 축 게이트 (Docs/M26_1차_지형축_실행명세서.md).
    /// W1(A* 교체)부터 시작해 W2~W6 이 뒤에 붙는다.
    ///
    /// 🔴 **고정한 축을 적는다** (방법론 M20 · W7R 교훈): 아래 검사들이 고정한 것은
    /// **맵 크기(MapConfig 부재 시 폴백 100×100)** 와 **장애물 배치(손으로 세운 3종)** 뿐이다.
    /// 마을 위치·자원 배치는 이 검사와 무관하다 (경로탐색은 좌표만 받는다).
    /// 배포 맵이 100×100 이 아니게 되면 `Bounds()`가 그것을 그대로 따라간다.
    /// </summary>
    public class M26_TerrainGates
    {
        // MapConfig.Active 는 EditMode 에서 보통 null 이라 JPS/A* 둘 다 폴백(100/50)을 쓴다.
        // 두 구현이 **같은 폴백**을 쓰는 것이 비교의 전제다 (다르면 좌표계가 어긋난다).
        private const int N = 100, OFFSET = 50, MIN = -50, MAX = 49;

        private static bool[,] OpenMap()
        {
            var w = new bool[N, N];
            for (int x = 0; x < N; x++) for (int y = 0; y < N; y++) w[x, y] = true;
            return w;
        }

        /// <summary>흩뿌린 장애물 — `Math.Abs` 를 빼먹으면 `^` 결과가 음수라 절반이 막힌다
        /// (2026-08-10 벤치에서 실제로 밟은 자리. 그래서 아래에 통행률 검사를 붙여 둔다).</summary>
        private static bool[,] ScatteredMap()
        {
            bool[,] w = OpenMap();
            for (int x = 0; x < N; x++) for (int y = 0; y < N; y++)
                if (System.Math.Abs((x * 73856093) ^ (y * 19349663)) % 100 < 12) w[x, y] = false;
            w[0 + OFFSET, 0 + OFFSET] = true;
            return w;
        }

        private static bool[,] LakeMap()
        {
            bool[,] w = OpenMap();
            for (int x = 10; x < 90; x++) if (x < 45 || x > 55) { w[x, 30] = false; w[x, 31] = false; }
            for (int y = 40; y < 90; y++) if (y < 60 || y > 70) { w[35, y] = false; w[36, y] = false; }
            w[0 + OFFSET, 0 + OFFSET] = true;
            return w;
        }

        /// <summary>결정적 질의 목록 — 마을(0,0)에서 사방으로 뻗는 반경 5~48 (채집·습격 거리대).</summary>
        private static List<Vector2Int> Queries(int count)
        {
            var qs = new List<Vector2Int>(count);
            for (int i = 0; i < count; i++)
            {
                int r = 5 + (i % 44);
                double a = i * 0.61803398875 * 2.0 * System.Math.PI;
                qs.Add(new Vector2Int(
                    Mathf.Clamp((int)(r * System.Math.Cos(a)), MIN, MAX),
                    Mathf.Clamp((int)(r * System.Math.Sin(a)), MIN, MAX)));
            }
            return qs;
        }

        /// <summary>경로의 실제 비용 (직선 1 · 대각 1.414) — 두 구현의 **답이 같은가**를 재는 자.
        /// 좌표열을 그대로 비교하지 않는 이유: 같은 최단 비용에 여러 길이 있고, 그건 결함이 아니다.</summary>
        private static float PathCost(List<Vector2Int> wp, Vector2Int from)
        {
            float c = 0f;
            Vector2Int prev = from;
            foreach (Vector2Int p in wp)
            {
                int dx = Mathf.Abs(p.x - prev.x), dy = Mathf.Abs(p.y - prev.y);
                c += (dx != 0 && dy != 0) ? 1.414f : 1f;
                prev = p;
            }
            return c;
        }

        [Test]
        public void M26_T2_AStarMatchesJpsWhenUniform()
        {
            // ADR-T-2 의 중립 불변식 — 비용이 균일하면 A* 는 **JPS 와 같은 답**을 내야 한다.
            // 이 검사가 통과해야 "엔진만 갈았다"고 말할 수 있다 (커밋 1의 중간 리뷰 항목).
            var maps = new (string name, bool[,] w)[]
            {
                ("빈 벌판", OpenMap()), ("흩뿌린 장애물", ScatteredMap()), ("호수·절벽", LakeMap()),
            };
            var start = new Vector2Int(0, 0);

            foreach ((string name, bool[,] w) in maps)
            {
                // 실패 가능성 증명 ① — 맵이 실제로 막혀 있는가 (전부 통행이면 장애물 검사가 무의미).
                int blocked = 0;
                for (int x = 0; x < N; x++) for (int y = 0; y < N; y++) if (!w[x, y]) blocked++;
                if (name != "빈 벌판")
                    Assert.Greater(blocked, 0, $"{name}: 막힌 칸이 0 — 이 맵은 빈 벌판과 같다 (빈 검사)");
                Assert.Less(blocked, N * N / 2,
                    $"{name}: 막힌 칸이 절반을 넘는다 — 맵 생성이 의도와 다르다 (Math.Abs 누락 등)");

                var astar = new AStarPathfinder(() => w);            // enterCost 미배선 = 균일
                int compared = 0, reachable = 0;
                foreach (Vector2Int q in Queries(100))
                {
                    if (!w[q.x + OFFSET, q.y + OFFSET]) continue;
                    reachable++;
                    PathResult j = JPSPathfinder.FindPathResult(0, 0, q.x, q.y, w);
                    PathResult a = astar.FindPath(0, 0, q.x, q.y);

                    Assert.AreEqual(j.Kind, a.Kind, $"{name} ({q.x},{q.y}): 결과 종류가 다르다");
                    if (j.Kind != PathResultKind.PathFound) continue;

                    Assert.AreEqual(q, a.Waypoints[a.Waypoints.Count - 1],
                        $"{name} ({q.x},{q.y}): 마지막 웨이포인트가 목표가 아니다 (계약 위반)");
                    Assert.LessOrEqual(PathCost(a.Waypoints, start), PathCost(j.Waypoints, start) + 1e-3f,
                        $"{name} ({q.x},{q.y}): A* 경로가 JPS 보다 길다 — 최단성이 깨졌다");
                    compared++;
                }
                // 실패 가능성 증명 ② — 실제로 비교한 건이 있어야 한다.
                Assert.Greater(compared, 10, $"{name}: 비교한 경로가 {compared}건뿐 — 빈 검사다");
                Assert.Greater(reachable, 10, $"{name}: 도달 가능 질의가 너무 적다");
            }
        }

        [Test]
        public void M26_T2b_AStarKeepsPathResultContract()
        {
            bool[,] w = OpenMap();
            var pf = new AStarPathfinder(() => w);

            Assert.AreEqual(PathResultKind.AlreadyThere, pf.FindPath(3, 3, 3, 3).Kind,
                "시작 == 목표는 AlreadyThere (ADR-M3)");

            w[10 + OFFSET, 10 + OFFSET] = false;
            LogAssert.ignoreFailingMessages = true;   // JPS 와 같은 경고를 낸다 — 여기선 종류만 본다
            Assert.AreEqual(PathResultKind.Unreachable, pf.FindPath(0, 0, 10, 10).Kind,
                "목표가 통행 불가면 Unreachable");
            Assert.AreEqual(PathResultKind.Unreachable, pf.FindPath(0, 0, 999, 0).Kind,
                "맵 밖이면 Unreachable");
            Assert.AreEqual(PathResultKind.Unreachable, new AStarPathfinder(() => null).FindPath(0, 0, 1, 1).Kind,
                "walkable null 이면 Unreachable");
            LogAssert.ignoreFailingMessages = false;

            PathResult ok = pf.FindPath(0, 0, 5, 0);
            Assert.AreEqual(PathResultKind.PathFound, ok.Kind);
            Assert.IsNotNull(ok.Waypoints);
            CollectionAssert.DoesNotContain(ok.Waypoints, new Vector2Int(0, 0), "시작 타일은 빠진다 (계약)");
            Assert.AreEqual(new Vector2Int(5, 0), ok.Waypoints[ok.Waypoints.Count - 1], "목표는 포함된다 (계약)");
        }

        [Test]
        public void M26_T2c_AStarIsDeterministic()
        {
            // ADR-M10R-2 — 같은 입력이면 같은 판. 힙 동률을 좌표순으로 깨지 않으면 여기서 깨진다.
            bool[,] w = LakeMap();
            var a = new AStarPathfinder(() => w);
            var b = new AStarPathfinder(() => w);
            int checkedCount = 0;
            foreach (Vector2Int q in Queries(40))
            {
                if (!w[q.x + OFFSET, q.y + OFFSET]) continue;
                PathResult r1 = a.FindPath(0, 0, q.x, q.y);
                PathResult r2 = b.FindPath(0, 0, q.x, q.y);
                PathResult r3 = a.FindPath(0, 0, q.x, q.y);   // 같은 인스턴스 재호출 (버퍼 재사용 검증)
                if (r1.Kind != PathResultKind.PathFound) continue;
                CollectionAssert.AreEqual(r1.Waypoints, r2.Waypoints, $"({q.x},{q.y}): 인스턴스가 다르면 길이 다르다");
                CollectionAssert.AreEqual(r1.Waypoints, r3.Waypoints, $"({q.x},{q.y}): 재호출에 길이 달라졌다 (버퍼 오염)");
                checkedCount++;
            }
            Assert.Greater(checkedCount, 10, "비교한 경로가 너무 적다 — 빈 검사다");
        }

        [Test]
        public void M26_T3_AStarStaysWithinBudget()
        {
            // 성공 기준 ③ — 1건 평균 ≤ 0.10ms.
            // 🔑 **기준선(2026-08-10 실측, 질의 200건)**: 빈 벌판 JPS 0.224 / A* 0.013 ·
            //    흩뿌린 12% 0.050 / 0.020 · 호수·절벽 0.062 / 0.026 (ms/건).
            //    예산 0.10 은 그 최악값(0.026)의 약 4배 — 가중치가 붙어 휴리스틱이 약해지는 몫을
            //    미리 비워 둔 것이다. 여기가 빨개지면 성능이 아니라 **전제**를 다시 본다.
            // ⚠️ CI 머신 편차를 고려해 예산은 넉넉히 잡았다. 정밀 비교는 벤치로 하고
            //    이 게이트는 **자릿수 회귀**(10배 느려짐)를 잡는 것이 목적이다.
            bool[,] w = LakeMap();
            var pf = new AStarPathfinder(() => w);
            var qs = Queries(200);

            var valid = new List<Vector2Int>();
            foreach (Vector2Int q in qs) if (w[q.x + OFFSET, q.y + OFFSET]) valid.Add(q);
            Assert.Greater(valid.Count, 100, "도달 가능 질의가 너무 적다 — 예산 측정이 무의미하다");

            for (int i = 0; i < 20; i++) pf.FindPath(0, 0, valid[i].x, valid[i].y);   // 워밍업

            var sw = System.Diagnostics.Stopwatch.StartNew();
            foreach (Vector2Int q in valid) pf.FindPath(0, 0, q.x, q.y);
            sw.Stop();

            double perCall = sw.Elapsed.TotalMilliseconds / valid.Count;
            Assert.Less(perCall, 0.10,
                $"A* 1건 평균 {perCall:0.000}ms — 예산 0.10ms 초과 (기준선 0.026ms). " +
                "버퍼 재사용이 빠졌거나 힙이 깨졌을 수 있다");
        }
    }
}
