using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AIVillage.Core;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M17: JPSPathfinder.FindPathResult()의 반환 계약 3분기 검증.
    /// 방향 ② 이동 실패 first-class 승격 — 결함 C 재발 방지 EditMode 게이트.
    ///
    /// 계약 (ADR-M2):
    ///   - Unreachable  → Waypoints == null
    ///   - AlreadyThere → Waypoints == null
    ///   - PathFound    → Waypoints != null, Count > 0
    ///
    /// 이 계약이 깨지면 호출자 VillagerFSM.StartPathTo가 3분기 switch에서
    /// 잘못된 폴백을 골라 좌표 스냅이 부활하고 결함 C가 되살아난다.
    /// </summary>
    public class M17_PathResultContract
    {
        // MapConfig.Active가 null일 때의 폴백값과 동일하게 세팅.
        // JPSPathfinder는 FALLBACK_MAP_SIZE=100, FALLBACK_OFFSET=50을 사용한다.
        private const int MAP_SIZE = 100;
        private const int OFFSET   = 50;

        /// <summary>완전 통행 가능한 mapSize×mapSize walkable 배열을 생성한다.</summary>
        private static bool[,] MakeFullyWalkable()
        {
            var w = new bool[MAP_SIZE, MAP_SIZE];
            for (int x = 0; x < MAP_SIZE; x++)
                for (int y = 0; y < MAP_SIZE; y++)
                    w[x, y] = true;
            return w;
        }

        // ── AlreadyThere 계약 ────────────────────────────────────────────────

        [Test]
        public void AlreadyThere_when_start_equals_goal()
        {
            var w = MakeFullyWalkable();
            var r = JPSPathfinder.FindPathResult(0, 0, 0, 0, w);

            Assert.AreEqual(PathResultKind.AlreadyThere, r.Kind,
                "시작==목표 시 Kind는 AlreadyThere여야 한다 (ADR-M3).");
            Assert.IsNull(r.Waypoints,
                "AlreadyThere일 때 Waypoints는 null이어야 한다 (ADR-M2).");
        }

        // ── PathFound 계약 ────────────────────────────────────────────────────

        [Test]
        public void PathFound_when_reachable()
        {
            var w = MakeFullyWalkable();
            var r = JPSPathfinder.FindPathResult(0, 0, 5, 5, w);

            Assert.AreEqual(PathResultKind.PathFound, r.Kind,
                "완전 walkable 맵에서 (0,0)→(5,5)는 PathFound여야 한다.");
            Assert.IsNotNull(r.Waypoints,
                "PathFound일 때 Waypoints는 non-null이어야 한다 (ADR-M2).");
            Assert.Greater(r.Waypoints.Count, 0,
                "PathFound일 때 Waypoints Count > 0 (JPS는 시작 제외·목표 포함).");
        }

        // ── Unreachable 3종 ──────────────────────────────────────────────────

        [Test]
        public void Unreachable_when_goal_unwalkable()
        {
            var w = MakeFullyWalkable();
            // 목표 타일 (5,5) → 배열 인덱스 (55,55)를 불가로 설정
            w[5 + OFFSET, 5 + OFFSET] = false;

            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("목표 타일이 이동 불가"));

            var r = JPSPathfinder.FindPathResult(0, 0, 5, 5, w);

            Assert.AreEqual(PathResultKind.Unreachable, r.Kind,
                "목표 타일이 unwalkable이면 Unreachable을 반환해야 한다.");
            Assert.IsNull(r.Waypoints,
                "Unreachable일 때 Waypoints는 null이어야 한다 (ADR-M2). " +
                "빈 리스트 반환 금지 — 호출자가 Count로 분기하려는 유혹 차단.");
        }

        [Test]
        public void Unreachable_when_out_of_bounds()
        {
            var w = MakeFullyWalkable();

            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("맵 범위 초과"));

            var r = JPSPathfinder.FindPathResult(0, 0, 999, 999, w);

            Assert.AreEqual(PathResultKind.Unreachable, r.Kind,
                "목표가 맵 범위 밖이면 Unreachable을 반환해야 한다.");
            Assert.IsNull(r.Waypoints);
        }

        [Test]
        public void Unreachable_when_walkable_null()
        {
            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("walkable 배열이 null"));

            var r = JPSPathfinder.FindPathResult(0, 0, 5, 5, null);

            Assert.AreEqual(PathResultKind.Unreachable, r.Kind,
                "walkable 배열이 null이면 Unreachable을 반환해야 한다.");
            Assert.IsNull(r.Waypoints);
        }
    }
}
