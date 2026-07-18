using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 재해 정의 (M9-C, ADR-M9-4) — 계절 1종당 에셋 1개. 해당 계절마다 확정 발동하며
    /// (확률 없음 — "예고했으면 온다"), 수확 전 파이프라인(자라는 작물·밭 시설)만 때린다
    /// (ADR-M9-5 — 창고 스톡은 성역). 새 재해 추가 = 이 에셋 1개 + WorldConfig.Disasters 등록,
    /// 코드 0줄 (M9-F 홍수 리허설이 증명).
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Disaster", fileName = "Disaster")]
    public sealed class DisasterSO : ScriptableObject
    {
        [Tooltip("한국어 표시명 (예: 폭염)")]
        public string DisplayName;

        [Tooltip("이 계절마다 발동 (SeasonSO 참조 — 이름 문자열 금지, ADR-M0-1). 비면 발동 안 함.")]
        public SeasonSO Season;

        [Tooltip("계절 시작 후 발동 시점(게임일). 제안 1.5 = 계절 중반, 예고 여운.")]
        public float OffsetDays = 1.5f;

        [Tooltip("true=작물 소실(Growing·Ripe→Empty, 시설 유지), false=밭 시설 소실(RemovePlot, 봄 재건 노동세).")]
        public bool TargetCrops = true;

        [Tooltip("기본 소실 비율. 제안 폭염 0.25.")]
        public float BaseLossPct = 0.25f;

        [Tooltip("대상 1개당 가산 비율 — 밭이 늘수록 가혹 (사용자 원안의 자기 균형). 제안 0.03.")]
        public float PerTargetPct = 0.03f;

        [Tooltip("소실 비율 클램프 상한. 제안 0.6.")]
        public float MaxLossPct = 0.6f;

        [Tooltip("저항 건물 사용 여부. false면 ResistanceSlot 무시 (기본).")]
        public bool UseResistance;

        [Tooltip("논리형 BuiltFlag — 완공 시 소실 배율(ResistMult) 적용. UseResistance일 때만 참조.")]
        public SlotId ResistanceSlot;

        [Tooltip("저항 건물 완공 시 소실 배율 (발전 배율 축 금지 — 저항은 건물 플래그만, ADR-M9-6). 제안 0.5.")]
        public float ResistMult = 0.5f;

        [Tooltip("발동 순간 근처 주민 반응 대사 (예: \"밭이 다 타버렸어!\"). 릴레이 아님 — 표현 전용.")]
        public string[] StrikeLines;

        private void OnValidate()
        {
            if (Season == null)
                Debug.LogWarning($"[DisasterSO] {name}: Season 미연결 — 이 재해는 발동하지 않습니다.", this);
            if (MaxLossPct < 0f || MaxLossPct > 1f)
                Debug.LogWarning($"[DisasterSO] {name}: MaxLossPct({MaxLossPct})는 0~1 비율이어야 합니다.", this);
            if (UseResistance && SlotIds.IsNumeric(ResistanceSlot))
                Debug.LogWarning($"[DisasterSO] {name}: ResistanceSlot({ResistanceSlot})은 논리형 BuiltFlag여야 합니다.", this);
        }
    }
}
