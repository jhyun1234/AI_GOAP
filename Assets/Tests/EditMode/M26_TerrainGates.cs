using System.Collections.Generic;
using AIVillage.Core;
using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
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

        // ── W2·W3·W5·W6 ──────────────────────────────────────────────────────

        /// <summary>배포 지형 팔레트 — 코드가 지형 이름을 모르는 것이 요점이라 **폴더를 통째로 읽는다.**</summary>
        /// <summary>배포 온대 팔레트 (= 마을 주변의 판).
        /// 🔴 **개정 2026-08-12 (M29 W2)**: 舊 구현은 프로젝트의 `t:TerrainTypeSO` 를 **전부** 긁어
        /// 한 팔레트로 묶었다. 지형 에셋 = 배포 팔레트였던 동안은 같은 말이었지만, 바이옴이
        /// 팔레트를 여러 벌로 가른 뒤로는 **아무도 안 쓰는 세계**(정글수풀+산능선+절벽이 한 판에)
        /// 를 만들어 냈다 — 실제로 막힘 33%로 T8 이 red 가 됐다. 배포 온대를 읽는다.
        /// 바이옴 층 자체는 `M29_BiomeGates`가 본다.</summary>
        private static TerrainTypeSO[] ShippedPalette()
        {
            var temperate = AssetDatabase.LoadAssetAtPath<BiomeSO>(
                "Assets/M0Config/Biomes/Biome_Temperate.asset");
            Assert.IsNotNull(temperate, "배포 온대 바이옴 에셋 없음 — 배포 팔레트의 출처가 사라졌다");
            var list = new List<TerrainTypeSO>();
            foreach (TerrainTypeSO t in temperate.Palette) if (t != null) list.Add(t);
            return list.ToArray();
        }

        private static TerrainService ShippedTerrain(uint seed, int safeRadius = 12)
            => new TerrainService(seed, Vector2Int.zero, safeRadius, ShippedPalette());

        [Test]
        public void M26_T1_TerrainIsDeterministicPerSeed()
        {
            // ADR-T-5 · ADR-M10R-2 — 판마다 다르되 **재현은 된다**.
            // ⚠️ 고정한 축: 맵 범위는 MapBounds(배포 폴백 100×100)를 읽고, 마을 중심은 (0,0)으로
            //    고정했다. 중심이 어디든 검사의 뜻은 같다 (지형은 좌표의 함수다).
            MapBounds.Get(out int minX, out int maxX, out int minY, out int maxY);
            TerrainService a1 = ShippedTerrain(12345u), a2 = ShippedTerrain(12345u), b = ShippedTerrain(999u);

            int same = 0, diff = 0, total = 0;
            for (int x = minX; x <= maxX; x += 1)
                for (int y = minY; y <= maxY; y += 1)
                {
                    total++;
                    Assert.AreSame(a1.At(x, y), a2.At(x, y),
                        $"({x},{y}): 같은 시드인데 지형이 다르다 — 같은 판을 다시 볼 수 없다");
                    if (a1.At(x, y) == b.At(x, y)) same++; else diff++;
                }

            // (M28-W3 개정: 舊 리터럴 10000은 "맵 전제 변경 감지기"였다 — 맵 크기가 변수가 된
            //  지금은 정합 감시를 M28_T3(mapOffset·MapQuad 짝)이 맡고, 여기는 산식으로 잰다.)
            Assert.AreEqual((maxX - minX + 1) * (maxY - minY + 1), total,
                "전수 검사가 맵 전체를 덮지 않았다 — 루프 범위와 MapBounds가 어긋났다");
            // 실패 가능성 증명 — 시드가 다르면 **실제로 달라진다** (안 달라지면 시드가 안 먹는 것).
            Assert.Greater(diff, total / 20,
                $"시드를 바꿨는데 다른 칸이 {diff}칸뿐 — 판마다 다른 지형이 아니다");
        }

        [Test]
        public void M26_T8_TerrainMixIsPlayable()
        {
            // 전부 물이면 게임이 안 되고, 막힌 칸이 0이면 지형이 없는 것과 같다.
            // 시드 8개를 훑어 **어느 판에서도** 놀 수 있는지 본다 (한 시드만 보면 운이다).
            // 🔴 기대값 이동 (M31 W1, 2026-08-12): 절벽 전체가 막던 시절 **17%**(절벽 10.7 + 물 6)
            //    였던 막힘이, 절벽이 "위는 땅 · 남쪽 두 줄만 벽"이 되면서 **7~9%**로 내려간다.
            //    대역(1~30%) 안이라 여기는 그대로 green — 정확한 대역은 `M31_HeightGates` T3 몫이다.
            MapBounds.Get(out int minX, out int maxX, out int minY, out int maxY);
            for (uint s = 1; s <= 8; s++)
            {
                TerrainService t = ShippedTerrain(s * 7919u);
                int blocked = 0, total = 0;
                for (int x = minX; x <= maxX; x++)
                    for (int y = minY; y <= maxY; y++) { total++; if (!t.IsWalkable(x, y)) blocked++; }

                float pct = 100f * blocked / total;
                Assert.Greater(pct, 1f, $"시드 {s}: 막힌 칸 {pct:0.0}% — 지형이 사실상 없다");
                Assert.Less(pct, 30f, $"시드 {s}: 막힌 칸 {pct:0.0}% — 맵이 너무 잠겼다");

                // 마을 둘레는 반드시 뚫려 있다 — 태어나자마자 갇히는 판 방지.
                for (int x = -12; x <= 12; x++)
                    for (int y = -12; y <= 12; y++)
                        Assert.IsTrue(t.IsWalkable(x, y), $"시드 {s}: 마을 안전반경에 막힌 칸 ({x},{y})");
            }
        }

        [Test]
        public void M26_T9_TerrainTableLivesInAssets()
        {
            // ADR-M0-2 — 지형 수치표가 코드로 새는 것을 막는다. 서비스는 이름을 하나도 몰라야 한다.
            string src = System.IO.File.ReadAllText("Assets/Scripts/M0/World/TerrainService.cs");
            foreach (string forbidden in new[] { "\"물\"", "\"늪\"", "\"절벽\"", "\"숲\"", "\"평지\"" })
                Assert.IsFalse(src.Contains(forbidden),
                    $"TerrainService 에 지형 이름 {forbidden} 이 박혀 있다 — 팔레트를 늘려도 코드가 따라 바뀐다");

            TerrainTypeSO[] palette = ShippedPalette();
            Assert.GreaterOrEqual(palette.Length, 5, "배포 팔레트가 5종 미만 — 1차 범위와 다르다");

            bool anyBlocking = false, anySlow = false, anyFallback = false;
            foreach (TerrainTypeSO t in palette)
            {
                Assert.IsFalse(string.IsNullOrEmpty(t.DisplayName), $"{t.name}: 표시명 비어 있음");
                Assert.GreaterOrEqual(t.EnterCost, 1f, $"{t.name}: EnterCost < 1 이면 최단 경로가 깨진다");
                if (!t.Walkable) anyBlocking = true;
                if (t.Walkable && t.MoveSpeedMult < 1f) anySlow = true;
                if (t.AppearsAbove <= 0f) anyFallback = true;
            }
            // 실패 가능성 증명 — 팔레트가 실제로 세 성질을 갖고 있어야 W3·W4 가 잴 것이 있다.
            Assert.IsTrue(anyBlocking, "막는 지형이 하나도 없다 — W3 가 검사할 것이 없다");
            Assert.IsTrue(anySlow, "느린 지형이 하나도 없다 — W4 가 검사할 것이 없다");
            Assert.IsTrue(anyFallback, "폴백 지형(AppearsAbove 0)이 없다 — 어떤 칸은 지형이 없게 된다");
        }

        [Test]
        public void M26_T4_PathAvoidsSwamp()
        {
            // 비용이 **경로에 걸리는가**. 늪 **덩어리**를 사이에 두고 마주 선 두 점에서,
            // 비용을 켜면 돌아가느라 통과 칸이 준다.
            // 🔴 고정한 축(방법론 M20): 늪은 **우회가 가능한 모양**이어야 한다. 처음엔 맵을 가로지르는
            //    띠로 깔았는데 그건 **피할 방법이 없어** 비용을 켜도 통과 칸이 안 줄었다 (84 → 84).
            //    "경로가 비용을 안 본다"가 아니라 **검사가 답을 강요한 것**이었다.
            bool[,] w = OpenMap();
            System.Func<int, int, bool> isSwamp = (x, y) => x >= -5 && x <= 5 && y >= -5 && y <= 5;
            var uniform = new AStarPathfinder(() => w);
            var weighted = new AStarPathfinder(() => w, (x, y) => isSwamp(x, y) ? 3f : 1f);

            int swampUniform = 0, swampWeighted = 0, pairs = 0;
            for (int i = 0; i < 12; i++)
            {
                int sy = -3 + (i % 7), gy = 3 - (i % 7);   // 늪 한가운데를 지나는 짝들
                PathResult u = uniform.FindPath(-20, sy, 20, gy);
                PathResult v = weighted.FindPath(-20, sy, 20, gy);
                if (u.Kind != PathResultKind.PathFound || v.Kind != PathResultKind.PathFound) continue;
                foreach (Vector2Int p in u.Waypoints) if (isSwamp(p.x, p.y)) swampUniform++;
                foreach (Vector2Int p in v.Waypoints) if (isSwamp(p.x, p.y)) swampWeighted++;
                pairs++;
            }
            Assert.Greater(pairs, 5, "비교한 경로가 너무 적다 — 빈 검사다");
            // 실패 가능성 증명 — 비용을 안 켰을 때는 늪을 **실제로 지난다**.
            Assert.Greater(swampUniform, 0, "비용 없이도 늪을 안 지난다 — 이 검사는 빈 검사다");
            Assert.Less(swampWeighted, swampUniform,
                $"늪 통과 칸이 안 줄었다 (균일 {swampUniform} → 가중 {swampWeighted}) — 비용이 경로에 안 걸린다");
        }

        [Test]
        public void M26_T5_ThreatsPaySameTerrainCost()
        {
            // ADR-T-3 — 늪은 누구에게나 늪이다. 배열은 둘(주민·위협)이지만 **비용은 한 장**이다.
            string src = System.IO.File.ReadAllText("Assets/Scripts/M0/SimulationLoop.cs");
            Assert.IsTrue(src.Contains("Pathfinder = new AStarPathfinder(() => Walkable, TileEnterCost)"),
                "주민 경로가 비용 창구를 안 본다");
            Assert.IsTrue(src.Contains("ThreatPathfinder = new AStarPathfinder(() => ThreatWalkable, TileEnterCost)"),
                "🔴 위협 경로가 비용 창구를 안 본다 — 늪이 주민만 괴롭히는 장치가 된다 (ADR-T-3)");

            // 속도 감속도 양쪽에 걸려 있는가 (게이트가 못 재는 자리라 배선 존재만 확인).
            Assert.IsTrue(System.IO.File.ReadAllText("Assets/Scripts/M0/Agent/VillagerAgent.cs")
                          .Contains("TerrainSpeedMult()"), "주민 이동에 지형 감속이 없다");
            Assert.IsTrue(System.IO.File.ReadAllText("Assets/Scripts/M0/Presentation/ThreatAgent.cs")
                          .Contains("terrainMult"), "🔴 위협 이동에 지형 감속이 없다 (ADR-T-3)");
        }

        [Test]
        public void M26_T6_WaterBlocksBothAndBuilding()
        {
            // 막는 지형은 **주민·위협 둘 다** 막고, 그 위에는 아무것도 안 선다.
            TerrainService t = ShippedTerrain(4242u);
            MapBounds.Get(out int minX, out int maxX, out int minY, out int maxY);
            int blocked = 0;
            for (int x = minX; x <= maxX; x++)
                for (int y = minY; y <= maxY; y++)
                {
                    if (t.IsWalkable(x, y)) continue;
                    blocked++;
                    TerrainTypeSO tt = t.At(x, y);
                    // 🔴 개정 2026-08-12 (M31 W1): 舊 검사는 `IsWalkable false ⟹ Walkable false`였다.
                    //    이제 막는 출처가 **둘**이다 — 물(`Walkable false`, 어디서나)과 절벽 벽면
                    //    (`IsWallRow`, 남쪽 가장자리만). 셋째 출처가 생기면 이 줄이 red 로 알린다.
                    Assert.IsTrue(!tt.Walkable || t.IsWallRow(x, y),
                        $"({x},{y}): 못 지나는데 물도 벽면도 아니다 — 통행 판정에 세 번째 출처가 생겼다");
                    Assert.AreEqual(1f, t.EnterCost(x, y), 1e-3f,
                        $"({x},{y}): 못 지나는 칸에 비용이 붙어 있다 — 아무도 안 읽는 수다");
                }
            Assert.Greater(blocked, 0, "막힌 칸이 0 — 빈 검사다");
        }

        [Test]
        public void M26_T7_ClearedTilesNeverRespawn()
        {
            // 🔴 M22-4차 ADR-C-3 의 이행. 개간한 자리에 다시 나면 그 축이 닫은 구멍이 부활한다.
            var d = new DiscoveryService();
            var node = new ResourceNode("relocate", ResourceType.Wood, 0, 0, 50f, 1f, true) { CurrentAmount = 0f };
            d.AddResourceNode(node);

            // 원래 자리 둘레를 **전부 개간 이력으로** 만든다 → 옮길 자리가 없어야 한다.
            var cleared = new ResourceNode("seed", ResourceType.Wood, 0, 0, 1f, 0f, true);
            for (int x = -6; x <= 6; x++)
                for (int y = -6; y <= 6; y++)
                {
                    if (x == 0 && y == 0) continue;
                    var tmp = new ResourceNode($"c{x}_{y}", ResourceType.Wood, x, y, 1f, 0f, true);
                    d.AddResourceNode(tmp);
                    d.RemoveNode(tmp);          // 개간 이력에 올린다
                }
            Assert.AreEqual(168, d.ClearedTiles.Count, "개간 이력 준비가 어긋났다 — 빈 검사다");

            d.ConfigureRespawn(1u, 1f, 6, (type, x, y) => true);
            d.TickRespawn(10f);
            Assert.AreEqual(0, node.TileX, "개간한 자리로 옮겨갔다 — 울타리 구멍이 부활한다 (ADR-C-3)");
            Assert.AreEqual(0, node.TileY, "〃");

            // 실패 가능성 증명 — 이력이 없으면 **실제로 옮겨간다**.
            var d2 = new DiscoveryService();
            var free = new ResourceNode("free", ResourceType.Wood, 0, 0, 50f, 1f, true) { CurrentAmount = 0f };
            d2.AddResourceNode(free);
            d2.ConfigureRespawn(1u, 1f, 6, (type, x, y) => true);
            d2.TickRespawn(10f);
            Assert.IsTrue(free.TileX != 0 || free.TileY != 0,
                "막을 이유가 없는데도 안 옮겼다 — 이 게이트는 빈 검사다");
            Assert.LessOrEqual(Mathf.Max(Mathf.Abs(free.TileX), Mathf.Abs(free.TileY)), 6,
                "반경 밖으로 옮겼다 — 숲이 순간이동한다");
            Assert.AreEqual(free.MaxAmount, free.CurrentAmount, 1e-3f, "옮긴 자리에서 잔량이 안 찼다");

            // 못 서는 땅(물)에도 안 난다.
            var d3 = new DiscoveryService();
            var blockedNode = new ResourceNode("blk", ResourceType.Wood, 0, 0, 50f, 1f, true) { CurrentAmount = 0f };
            d3.AddResourceNode(blockedNode);
            d3.ConfigureRespawn(1u, 1f, 6, (type, x, y) => false);   // 어디에도 못 선다
            d3.TickRespawn(10f);
            Assert.AreEqual(0, blockedNode.TileX, "설 수 없는 땅으로 옮겼다");
            Assert.AreEqual(0, blockedNode.TileY, "〃");
        }

        [Test]
        public void M26_T10_NeutralWithoutTerrain()
        {
            // 중립 불변식 전수 — 팔레트가 비면 오늘과 완전히 같은 판이어야 한다.
            var empty = new TerrainService(1u, Vector2Int.zero, 12, null);
            Assert.IsTrue(empty.IsEmpty, "빈 팔레트인데 IsEmpty 가 false");
            Assert.IsNull(empty.At(5, 5), "지형 없는 판인데 지형이 나온다");
            Assert.IsTrue(empty.IsWalkable(5, 5), "지형 없는 판인데 막힌 칸이 있다");
            Assert.AreEqual(1f, empty.EnterCost(5, 5), 1e-4f, "지형 없는 판인데 비용이 1이 아니다 (경로가 바뀐다)");
            Assert.AreEqual(1f, empty.SpeedMult(5, 5), 1e-4f, "지형 없는 판인데 속도가 1이 아니다");
            Assert.IsNull(empty.GroundColor(5, 5), "지형 없는 판인데 색이 나온다 (舊 화면과 달라진다)");

            // 리스폰도 미배선이면 안 돈다.
            var d = new DiscoveryService();
            var n = new ResourceNode("x", ResourceType.Wood, 3, 4, 50f, 1f, true) { CurrentAmount = 0f };
            d.AddResourceNode(n);
            d.TickRespawn(100f);   // ConfigureRespawn 을 안 불렀다
            Assert.AreEqual(3, n.TileX, "리스폰 미배선인데 노드가 움직였다");
            Assert.AreEqual(4, n.TileY, "〃");
        }
    }
}
