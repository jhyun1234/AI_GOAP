using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 여가 배회 액션 (M1-A). 기준점(건물 또는 현재 위치) 반경 내 랜덤 타일로 이동해
    /// DurationSec만큼 머문다. 자원을 생산·소비하지 않는다 (ADR-M1-3) —
    /// Effects는 비워 둔다. 머무는 시간은 별도 필드가 아니라 DurationSec을 그대로 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Action/Wander", fileName = "WanderAction")]
    public sealed class WanderActionSO : ActionSO
    {
        [Tooltip("true면 AnchorFlagSlot 건물의 완공 위치를 기준점으로 (미완공이면 기지 폴백). false면 현재 위치 기준.")]
        public bool AnchorAtBuilding;

        [Tooltip("기준점으로 삼을 건물의 완공 플래그 슬롯 (기본 모닥불)")]
        public SlotId AnchorFlagSlot = SlotId.CampfireBuilt;

        [Tooltip("기준점 반경 (타일). 제안치 — 산책 5, 모닥불 곁 2.")]
        public int WanderRadius = 5;

        public override IActionRunner CreateRunner(VillagerAgent agent) => new WanderRunner(this);
    }
}
