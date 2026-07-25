using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 야생 위협 정의 (M10-C, ADR-M10R-1) — 밴드 1개당 에셋 1개. 활성 밴드(게임일 래칫 기준)가
    /// PeriodDays마다 확정 출몰한다 (확률 없음 — ADR-M10-1). 격퇴 불가 (ADR-M10-8, M11에서 개정).
    /// 타격 대상은 주민(부상 문 Injure) 또는 밭 시설(M9-B RemoveCountableAt 문)뿐 —
    /// 창고 스톡은 성역 (ADR-M10-5). 새 위협/티어 추가 = 이 에셋 1개 + WorldConfig.Threats 등록,
    /// 코드 0줄 (M10-G 리허설이 증명).
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Threat", fileName = "Threat")]
    public sealed class ThreatSO : ScriptableObject
    {
        [Tooltip("한국어 표시명 (예: 외로운 늑대)")]
        public string DisplayName;

        [Tooltip("이 밴드가 열리는 최소 게임일(GameTime). 활성 = UnlockDay ≤ 현재 게임일 중 최신 1개 " +
                 "(ADR-M10R-1 시간 래칫 — 게임일은 안 줄어드니 되돌아가지 않는다). 마을 규모 아님.")]
        public float UnlockDay;

        [Tooltip("출몰 주기 (게임일). 이전 발동 시각 + 이 값 = 다음 발동 시각.")]
        public float PeriodDays = 6f;

        [Tooltip("발동 전 예고 (게임일) — HUD 경보 + 주민 술렁임(M10-D) 시작 시점.")]
        public float WarnDays = 1f;

        [Tooltip("매 출몰이 주민을 타깃할 확률(0~1). 나머지 확률은 밭 타격. 출몰 서수 시드로 결정 " +
                 "(ADR-M10R-2·3 — 곰도 <1이면 밭을 칠 수 있다). 0=밭만, 1=주민만.")]
        [Range(0f, 1f)] public float VillagerTargetChance;

        [Tooltip("타격 반경 (타일, 맨해튼) — 도착 지점 기준 이 안의 주민만 부상 후보. " +
                 "도망(M10-D)치지 않아 잔류한 주민이 다친다 (행동의 인과).")]
        public int StrikeRadiusTiles = 3;

        [Tooltip("주민 감지 반경 (타일) — 이 안의 주민에게 ThreatNear=1 (도망 트리거, M10-D). " +
                 "성격 FleeRadiusMult가 개인 배율로 곱해진다.")]
        public int DangerRadiusTiles = 6;

        [Header("위력 곡선 (DisasterService.LossCount 재사용 — 산식 이원화 금지)")]
        [Tooltip("기본 피해 비율. 제안 0.25.")]
        public float BaseLossPct = 0.25f;

        [Tooltip("후보 1개당 가산 비율 — 밀집할수록 가혹 (재해 곡선과 동일 사상). 제안 0.03.")]
        public float PerTargetPct = 0.03f;

        [Tooltip("피해 비율 클램프 상한. 주민 타격은 0.25 (부상자 ≤ 인구 25% — 결정 9).")]
        public float MaxLossPct = 0.25f;

        [Header("표현")]
        [Tooltip("개체 이동 속도 (타일/초, 실시간). 주민 기본 2.0보다 약간 빠르게 — 제안 2.5.")]
        public float MoveSpeed = 2.5f;

        [Tooltip("스프라이트 폴백 마커 색 (주민 원형 마커 패턴 — 아트는 후속 에셋 교체).")]
        public Color BodyColor = new Color(0.8f, 0.2f, 0.2f);

        [Tooltip("예고 구간 주민 술렁임 대사 (M10-D — 계절 ForecastLines 패턴). 비면 술렁임 없음.")]
        public string[] ForecastLines;

        [Tooltip("타격 순간 근처 주민 반응 대사 (재해 StrikeLines 패턴 — 표현 전용, 릴레이 아님).")]
        public string[] StrikeLines;

        private void OnValidate()
        {
            if (PeriodDays <= 0f)
                Debug.LogWarning($"[ThreatSO] {name}: PeriodDays({PeriodDays})는 양수여야 합니다 — 스케줄이 멈춥니다.", this);
            if (WarnDays >= PeriodDays)
                Debug.LogWarning($"[ThreatSO] {name}: WarnDays({WarnDays}) ≥ PeriodDays({PeriodDays}) — 예고가 항상 켜져 있게 됩니다.", this);
            if (MaxLossPct < 0f || MaxLossPct > 1f)
                Debug.LogWarning($"[ThreatSO] {name}: MaxLossPct({MaxLossPct})는 0~1 비율이어야 합니다.", this);
            if (VillagerTargetChance < 0f || VillagerTargetChance > 1f)
                Debug.LogWarning($"[ThreatSO] {name}: VillagerTargetChance({VillagerTargetChance})는 0~1 이어야 합니다.", this);
        }
    }
}
