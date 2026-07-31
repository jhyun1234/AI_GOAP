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

        [Header("화폐 지급 (M16-W3 — >0이면 화폐 모드: 에스크로·재고 검사 없음, 완수 시 발행)")]
        [Tooltip("완수 시 지갑에 발행할 동(銅) — WorldModel.Mint 경유 (ADR-M16-1). 제안치 5.\n" +
                 "화폐 모드에서 CostSlot/CostAmount/SatietyGain은 휴면 — 0으로 둘 것 (OnValidate 경고).\n" +
                 "⚠️ 거부 오프셋은 물가로 스케일된다 (ADR-M16-4 — 남발하면 같은 액면의 설득력이 준다).")]
        public int MoneyGain;

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
            if (MoneyGain < 0) MoneyGain = 0;
            // 화폐 모드 (M16-W3) — 실물 비용·포만 지급 필드는 휴면. 병기하면 어느 쪽이
            // 진짜인지 화면과 판정이 갈린다 (같은 정보 두 형태 금지).
            if (MoneyGain > 0 && (CostAmount > 0 || SatietyGain > 0))
                Debug.LogWarning($"[RewardSO] {name}: 화폐 모드(MoneyGain {MoneyGain})인데 " +
                                 $"CostAmount({CostAmount})/SatietyGain({SatietyGain})이 남아 있음 — 0 권장 (휴면 필드).");
        }
    }
}
