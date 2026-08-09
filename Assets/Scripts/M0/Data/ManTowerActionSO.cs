using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 망루 탑승 액션 (M22-3차 W3, ADR-M22-11) — Goal_ManTower의 DirectActionPool 전용.
    /// 타격 수치(피해·간격·무기 배율)의 단일 출처 = CombatSource(Action_Fight 참조) —
    /// 망루가 주는 것은 **사거리와 안전뿐**이다. 화력 필드를 여기 만들면 자동 포탑으로 가는
    /// 문이 열린다 (명세 §3 — 수치 이중 기입 금지).
    /// 사거리의 단일 출처 = 망루 에셋(BuildingSO.TowerRangeTiles) — 러너가 카탈로그 파생으로 읽는다.
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Actions/ManTower", fileName = "ManTower")]
    public sealed class ManTowerActionSO : ActionSO
    {
        [Tooltip("타격 수치의 원천 — Action_Fight 참조 (이중 기입 금지). 비면 요격 불능 (탑에 오르기만 한다)")]
        public FightActionSO CombatSource;

        [Tooltip("감시 대기 상한 (실시간 초) — 위협 감지는 남았는데 사거리에 아무도 안 들어올 때 " +
                 "이 시간이 지나면 내려온다 (FightRunner.AmbushGiveUpSec 동형 — 상한 없는 감시는 아사)")]
        public float WatchGiveUpSec = 90f;

        public override IActionRunner CreateRunner(VillagerAgent agent) => new ManTowerRunner(this);

        private void OnValidate()
        {
            if (CombatSource == null)
                Debug.LogWarning($"[ManTowerActionSO] {name}: CombatSource(Action_Fight) 미배선 — " +
                                 "탑승해도 요격을 못 합니다.", this);
        }
    }
}
