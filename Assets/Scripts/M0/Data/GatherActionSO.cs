using AIVillage.Core;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 채집 계열 액션. 어떤 자원 노드로 이동해 캘지를 TargetResource로 지정한다.
    /// 수확량은 별도 필드가 아니라 Effects의 스톡 Add 값 그 자체다 (수치 단일 출처).
    /// 채집 완료 시 스톡 귀속(舊 DepositToStock)은 W4 GatherRunner에 내장된다.
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Action/Gather", fileName = "GatherAction")]
    public sealed class GatherActionSO : ActionSO
    {
        [Tooltip("이동·수확 대상 자원 노드 타입")]
        public ResourceType TargetResource;

        public override ActionRunnerBase CreateRunner(VillagerAgent agent) => new GatherRunner(this);
    }
}
