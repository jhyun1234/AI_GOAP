using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 간호 계열 액션 (M10-B). 대상 = 최근접 부상 주민 (SimulationLoop.FindNearestInjured —
    /// 미간호 우선·거리순·결정적). 효과의 InjuredCount 차감은 플래너 픽션이다 — 실제 회복은
    /// 부상자 본인의 SimTick만 진행한다 (ADR-M10-2, EffectApplier는 파생 슬롯 무시).
    /// 간호는 직업 전용이 아니다 (결정 11 — 붕괴 스위치 금지): 누구나 실행, 치료사는 배율만.
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Action/Tend", fileName = "TendAction")]
    public sealed class TendActionSO : ActionSO
    {
        [Tooltip("간호 유지 거리 (타일, 맨해튼). 대상이 이 밖으로 벗어나면 재계획으로 재추적 — " +
                 "부상자는 절뚝(감속)이라 이탈이 드물다.")]
        public int TendRangeTiles = 2;

        public override IActionRunner CreateRunner(VillagerAgent agent) => new TendRunner(this);
    }
}
