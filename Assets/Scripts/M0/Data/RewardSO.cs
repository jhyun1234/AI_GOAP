using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 보상 정의 (M6-E — 설득 수단 1호). 거부하는 주민에게 촌장이 걸 수 있는 약속:
    /// 거부 문턱을 옮기고(판정은 여전히 결정적 — ADR-M6-4), 수락 시 비용을 에스크로 차감,
    /// 완수 시 주민 포만으로 지급한다 (ADR-M6-5). 새 보상 종류 = 이 에셋 1개.
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Reward", fileName = "Reward")]
    public sealed class RewardSO : ScriptableObject
    {
        public string DisplayName;

        [Header("비용 (수락 시 에스크로 차감 — ADR-M6-5. 완수=지급, 그 외=반환)")]
        [Tooltip("차감할 전역 스톡 슬롯 (IsStock 슬롯만 허용 — OnValidate 검사)")]
        public SlotId CostSlot = SlotId.CookedFoodStock;
        public int CostAmount = 2;

        [Header("지급 (완수 시 그 주민의 포만으로)")]
        public int SatietyGain = 40;

        [Header("거부 문턱 오프셋 (성격 오프셋과 합산 — ADR-M6-4. P0 대역 불가침 — ADR-M6-6)")]
        [Tooltip("-면 배고픔 거부 문턱 하향 = 웬만한 배고픔은 보상에 넘어온다")]
        public float RefuseSatietyOffset = -20f;
        [Tooltip("+면 피로 거부 문턱 상향 = 웬만한 피로는 보상에 넘어온다")]
        public float RefuseFatigueOffset = 20f;

        [Tooltip("보상 수락 대사 (랜덤 선택)")]
        public string[] PromiseLines = { "좋아, 그거라면 하지.", "약속한 겁니다?" };

        [Tooltip("완수 후 지급 대사 (랜덤 선택)")]
        public string[] PayLines = { "약속한 밥이다. 잘 먹을게!" };

        private void OnValidate()
        {
            if (!SlotIds.IsStock(CostSlot))
                Debug.LogError($"[RewardSO] {name}: CostSlot은 전역 스톡 슬롯이어야 합니다 (현재 {CostSlot}).");
            if (CostAmount < 0) CostAmount = 0;
            // (M19-W4: 화폐 모드(MoneyGain)는 화폐와 함께 철거 — 보상은 실물 원형뿐)
        }
    }
}
