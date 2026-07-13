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

        [Header("이동 표현 (W5 — 전부 제안치, 관찰 후 튜닝)")]
        [Tooltip("출발 가속 시간(초). 0이면 즉시 최고 속도.")]
        public float AccelSec = 0.25f;

        [Tooltip("목적지 근접 시 감속 배율 (1=감속 없음).")]
        public float DecelFactor = 0.45f;

        [Tooltip("감속을 시작하는 목적지까지의 거리(타일).")]
        public float DecelDistance = 0.6f;

        [Header("플랜 말풍선 (W6 — 전부 제안치)")]
        [Tooltip("머리 위 오프셋 (월드 유닛)")]
        public float BubbleOffsetY = 1.1f;

        [Tooltip("WorldSpace TMP 폰트 크기")]
        public float BubbleFontSize = 2.5f;

        [Tooltip("말풍선 랩핑 폭 (월드 유닛). 0 금지 — 버그C7 오버플로우 방어")]
        public float BubbleWidth = 6f;

        [Tooltip("현재 실행 중인 액션의 강조색")]
        public Color BubbleCurrentColor = new Color(1f, 0.83f, 0.31f, 1f);

        [Header("플래닝")]
        [Tooltip("플랜 요청 타임아웃(초). 舊 PLANNING_TIMEOUT_SEC 3.0 이관.")]
        public float PlanningTimeoutSec = 3f;

        [Header("욕구 (초기값은 舊 VillagerBrain 기본값 이관)")]
        public float InitialSatiety = 70f;
        public float InitialFatigue = 20f;

        [Tooltip("게임 1일당 포만감 자연 감소량. 舊 코드에는 자연 감쇠가 없었음 — " +
                 "25는 2026-07-13 기획 승인값 (초기 70 기준 약 2일마다 식사).")]
        public float SatietyDecayPerGameDay = 25f;
    }
}
