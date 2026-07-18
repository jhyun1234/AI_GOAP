/// <summary>
/// IPathfinder.cs - 경로 탐색 창구 (2026-07-18)
///
/// 역할(Role): 호출부(VillagerAgent 등 이동 계층)와 실제 경로 탐색 알고리즘(현재 JPS) 사이의
///             유일한 경계선. 호출부는 이 인터페이스만 알고, 어떤 알고리즘이 어떤 지형 표현으로
///             경로를 푸는지 모른다.
///
/// 왜 존재하나 (게임 후반 대비 경계선 — 반드시 유지):
///   ① 맵이 수백×수백으로 커지고 동시 AI가 수십으로 늘면 → JPS 위에 HPA*(계층 경로) 계층을
///      얹어야 할 수 있다. (HPA*의 클러스터 내부 solver로 JPS를 그대로 재사용하므로 교체가 아닌 증축.)
///   ② 지형에 "느리지만 통과 가능"한 이동 비용 가중치(늪·자갈 등)가 생기면 → JPS의 균일 비용
///      전제가 깨지므로 A*(가중치 네이티브)로 교체해야 한다.
///   두 경우 모두 이 인터페이스 뒤 구현만 갈아끼우고 호출부는 diff 0으로 유지하기 위한 창구다.
///   → 전체 배경·교체 절차: Docs/ADR_경로탐색_확장경계.md (다른 세션은 이 문서를 먼저 읽을 것)
///
/// 계약:
///   - FindPath는 타일 좌표(from, to)만 받는다. 통행 배열·비용 소스는 구현이 보유한다.
///   - 반환은 JPSPathfinder와 동일한 PathResult 계약(Unreachable/AlreadyThere/PathFound).
///
/// Author: (경로탐색 확장 경계 도입 세션)
/// Last Updated: 2026-07-18
/// </summary>

using System;

namespace AIVillage.Core
{
    /// <summary>
    /// 경로 탐색 창구. 호출부가 의존하는 유일한 경로탐색 타입.
    /// PathResult 계약은 JPSPathfinder와 공유한다 (Kind로 3분기 후에만 Waypoints 참조).
    /// </summary>
    public interface IPathfinder
    {
        /// <summary>시작 타일 → 목표 타일 경로. 좌표는 타일 좌표(-mapOffset ~ +mapOffset-1).</summary>
        PathResult FindPath(int startX, int startY, int goalX, int goalY);
    }

    /// <summary>
    /// 오늘의 유일한 IPathfinder 구현 — 균일 비용 JPS 위임.
    /// JPSPathfinder는 동결 검증 라이브러리(수정 금지 규약)이므로 이 어댑터는 위임만 한다.
    ///
    /// ── 가중치 지형 업데이트 시 교체 지점 (ADR_경로탐색_확장경계.md 참조) ──
    ///   지형이 가변 이동 비용을 갖게 되면:
    ///     1. 이 클래스를 AStarPathfinder(가중치 네이티브)로 교체하거나 병존시킨다.
    ///     2. 그 구현은 타일별 이동 비용(EnterCost, 차단=∞)을 소비한다.
    ///     3. 통행 여부 질문(VillagerAgent.IsWalkable)은 "비용 < ∞"로 한 줄만 바꾼다.
    ///   IPathfinder 시그니처와 호출부(VillagerAgent)는 그대로 둔다 → diff 0.
    /// </summary>
    public sealed class JpsPathfinder : IPathfinder
    {
        // 통행 배열을 지연 조회한다 — 건설로 Walkable이 갱신돼도 항상 최신 배열을 본다
        // (M0SimulationLoop.Walkable은 통행 차단 건물 완공 시 갱신됨, ADR-M3-3).
        private readonly Func<bool[,]> _walkableSource;

        public JpsPathfinder(Func<bool[,]> walkableSource)
        {
            _walkableSource = walkableSource ?? throw new ArgumentNullException(nameof(walkableSource));
        }

        public PathResult FindPath(int startX, int startY, int goalX, int goalY)
            => JPSPathfinder.FindPathResult(startX, startY, goalX, goalY, _walkableSource());
    }
}
