namespace AIVillage.M0
{
    /// <summary>
    /// 플래닝용 read-only 스냅샷 (舊 2레이어 구조 계승 — 플래닝은 복사본, 실행은 WorldModel).
    /// Slots 길이는 PlanningConfig.TotalSlots. M0는 SlotId 0~7만 사용한다.
    /// W3 WorldModel이 매 플랜 요청 시점에 생성한다.
    /// </summary>
    public readonly struct WorldSnapshot
    {
        public readonly int[] Slots;

        public WorldSnapshot(int[] slots)
        {
            Slots = slots;
        }

        public bool IsValid => Slots != null && Slots.Length >= SlotIds.Count;

        public int Get(SlotId id) => Slots[(int)id];
    }
}
