using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 계절 정의 (M6-A). 계절이 게임에 개입하는 통로는 배율 3종뿐이다 (ADR-M6-1) —
    /// 새 계절 추가 = 이 에셋 1개 + WorldConfig.SeasonCycle 등록 (코드 0줄, M6-F 리허설이 증명).
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Season", fileName = "Season")]
    public sealed class SeasonSO : ScriptableObject
    {
        public string DisplayName;

        [Tooltip("이 계절의 길이 (게임일). 최소 0.1 — OnValidate 클램프 (0이면 시계 계산이 깨진다).")]
        public float DurationDays = 6f;

        [Tooltip("위기 계절 여부 — 예고(DaysToCrisis 슬롯)와 HUD 경보의 대상 (M6-C).")]
        public bool IsCrisis;

        [Header("효과 배율 (ADR-M6-1 — 이 3종이 전부. 1 = 영향 없음)")]
        [Tooltip("자원 노드 재생 배율 (겨울 0 = 열매가 마른다)")]
        public float RegenMult = 1f;

        [Tooltip("작물 성장 배율 (겨울 0 = 밭이 멈춘다)")]
        public float GrowthMult = 1f;

        [Tooltip("포만 감쇠 배율 (겨울 1.5 = 더 빨리 배고파진다)")]
        public float SatietyDecayMult = 1f;

        [Tooltip("예고 기간 주민 술렁임 대사 (위기 계절만 사용 — M6-C). 비면 술렁임 없음.")]
        public string[] ForecastLines;

        private void OnValidate()
        {
            if (DurationDays < 0.1f) DurationDays = 0.1f;
        }
    }
}
