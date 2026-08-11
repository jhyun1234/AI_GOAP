using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 수리 액션 (M22-W6, ADR-M22-7). 대상 선정(최다 손상)·비용 검사·복원은 RepairRunner가
    /// DefenseService의 문(Repair)을 지나 수행한다 — 이 SO는 계획 데이터(전제/효과)와 말풍선의
    /// 집이다 (ADR-M0-1 액션 단일 응집). 수리 비용의 단일 출처는 BuildingSO.RepairCost —
    /// 이 에셋의 Preconditions(WoodStock ≥ 1)는 플래너 시야용 최소 전제일 뿐 이중 기입이 아니다.
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Action/Repair", fileName = "RepairAction")]
    public sealed class RepairActionSO : ActionSO
    {
        public override ActionRunnerBase CreateRunner(VillagerAgent agent) => new RepairRunner(this);
    }
}
