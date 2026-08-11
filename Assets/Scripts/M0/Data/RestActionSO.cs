using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>휴식 계열 액션. 제자리 실행, Effects의 MyFatigue Sub가 전부다.</summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Action/Rest", fileName = "RestAction")]
    public sealed class RestActionSO : ActionSO
    {
        public override ActionRunnerBase CreateRunner(VillagerAgent agent) => new RestRunner(this);
    }
}
