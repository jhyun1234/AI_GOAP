using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 주민 공통 설정. 게임 수치는 코드 상수가 아니라 이 에셋에만 둔다 (ADR-M0-2).
    /// 전부 舊 런타임 이관값 — 새 값 발명 금지.
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/AgentConfig", fileName = "AgentConfig")]
    public sealed class AgentConfigSO : ScriptableObject
    {
        [Header("이동")]
        [Tooltip("기본 이동 속도 (타일/초). 舊 VILLAGER_MOVE_SPEED 2.0 이관.")]
        public float BaseMoveSpeed = 2f;

        [Tooltip("개체별 속도 편차 비율 (W5 사용). 제안치 ±15% — 기존 값 없음, 관찰 후 튜닝.")]
        public float SpeedVariancePct = 0.15f;

        [Header("플래닝")]
        [Tooltip("플랜 요청 타임아웃(초). 舊 PLANNING_TIMEOUT_SEC 3.0 이관.")]
        public float PlanningTimeoutSec = 3f;

        [Header("욕구 (초기값은 舊 VillagerBrain 기본값 이관)")]
        public float InitialSatiety = 70f;
        public float InitialFatigue = 20f;

        [Tooltip("게임 1일당 포만감 자연 감소량. ⚠️ 舊 코드에 자연 감쇠가 존재하지 않아(식사 사이클 사실상 미발동) " +
                 "기본 0 — 실제 값은 기획 결정 필요 (W3 진입 전 확정).")]
        public float SatietyDecayPerGameDay = 0f;
    }
}
