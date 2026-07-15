using AIVillage.Core;
using NUnit.Framework;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// JPS 장애물 우회 게이트 (2026-07-16) — M3-C에서 통행 차단 건물이 생기며 처음 드러난
    /// 강제 이웃 감지 결함(이전 칸 기준 → 현재 칸 기준 표준형으로 수정)의 재발 방지.
    /// 케이스는 실제 방치 진단 로그의 좌표를 그대로 박제했다 (walkable-walkable Unreachable).
    /// </summary>
    public class JPS_ObstacleGates
    {
        private static bool[,] OpenMap(params Vector2Int[] blockedTiles)
        {
            const int SIZE = 100, OFFSET = 50; // JPS 폴백 값과 동일 (EditMode는 MapConfig 없음)
            var w = new bool[SIZE, SIZE];
            for (int x = 0; x < SIZE; x++)
                for (int y = 0; y < SIZE; y++)
                    w[x, y] = true;
            foreach (Vector2Int t in blockedTiles)
                w[t.x + OFFSET, t.y + OFFSET] = false;
            return w;
        }

        [Test]
        public void JPS_G1_RoutesAroundSingleObstacle()
        {
            // 방치 실측 케이스 E: 출발 (-3,28) → 목표 (2,-9), 장애물 (2,-8) — 목표 바로 위 1칸
            var w = OpenMap(new Vector2Int(2, -8));
            PathResult r = JPSPathfinder.FindPathResult(-3, 28, 2, -9, w);
            Assert.AreEqual(PathResultKind.PathFound, r.Kind, "장애물 1칸은 우회 가능해야 한다");
        }

        [Test]
        public void JPS_G2_RoutesAroundDiagonalPair()
        {
            // 방치 실측 케이스 A: 출발 (0,-12) → 목표 (1,-7), 장애물 (1,-9)+(2,-8) 대각 쌍 (집 군집 배치)
            var w = OpenMap(new Vector2Int(1, -9), new Vector2Int(2, -8));
            PathResult r = JPSPathfinder.FindPathResult(0, -12, 1, -7, w);
            Assert.AreEqual(PathResultKind.PathFound, r.Kind, "대각 쌍 곁 목표도 우회 가능해야 한다");

            // 방치 실측 케이스 D: 출발 (0,-10) → 목표 (5,-6)
            PathResult r2 = JPSPathfinder.FindPathResult(0, -10, 5, -6, w);
            Assert.AreEqual(PathResultKind.PathFound, r2.Kind);
        }

        [Test]
        public void JPS_G3_OpenMapBehaviorUnchanged()
        {
            // 장애물 0개(기존 M0~M3 상태) — 회귀 방지: 직선/대각/제자리
            var w = OpenMap();
            Assert.AreEqual(PathResultKind.PathFound, JPSPathfinder.FindPathResult(0, 0, 10, 7, w).Kind);
            Assert.AreEqual(PathResultKind.AlreadyThere, JPSPathfinder.FindPathResult(3, 3, 3, 3, w).Kind);

            // 진짜 도달 불가(목표 자체가 막힘)는 여전히 Unreachable
            var w2 = OpenMap(new Vector2Int(5, 5));
            Assert.AreEqual(PathResultKind.Unreachable, JPSPathfinder.FindPathResult(0, 0, 5, 5, w2).Kind);
        }
    }
}
