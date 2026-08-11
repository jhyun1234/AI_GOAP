using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 탐험 액션. 미발견 노드 방향 또는 랜덤 방향으로 MoveDistanceTiles만큼 이동하며
    /// 경유지마다 FoW 공개 + 자원 발견을 수행한다 (W4 ExploreRunner).
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Action/Explore", fileName = "ExploreAction")]
    public sealed class ExploreActionSO : ActionSO
    {
        [Tooltip("탐험 이동 거리(타일). 舊 FindExplorationTarget의 랜덤 15타일 이관.")]
        public int MoveDistanceTiles = 15;

        public override ActionRunnerBase CreateRunner(VillagerAgent agent) => new ExploreRunner(this);
    }
}
