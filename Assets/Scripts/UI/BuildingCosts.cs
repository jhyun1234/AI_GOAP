/// <summary>
/// BuildingCosts.cs - 건물별 건설 자원 비용 상수 테이블
///
/// 역할(Role): GDD v0.4에서 확정된 건물 비용 수치를 단일 위치에서 관리한다.
///             PlayerInputController(자원 충족 확인)와
///             BuildingOrderPanel(버튼 활성/비활성)이 이 클래스를 공유 참조한다.
///             기획서 수치 변경 시 이 파일만 수정하면 된다.
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-28
/// </summary>

namespace AIVillage.UI
{
    /// <summary>
    /// 건물별 건설 자원 비용 상수. GDD v0.4 기획서 확정 수치.
    /// 기획 변경 시 이 클래스만 수정한다.
    /// </summary>
    public static class BuildingCosts
    {
        // ── Campfire ─────────────────────────────────────────────────────────
        public const float CAMPFIRE_WOOD    = 5f;

        // ── House ─────────────────────────────────────────────────────────────
        public const float HOUSE_WOOD       = 20f;
        public const float HOUSE_STONE      = 10f;

        // ── Storehouse ────────────────────────────────────────────────────────
        public const float STORE_WOOD       = 15f;
        public const float STORE_STONE      = 5f;

        // ── TownHall ──────────────────────────────────────────────────────────
        public const float TOWNHALL_WOOD    = 35f;
        public const float TOWNHALL_STONE   = 30f;
        public const float TOWNHALL_IRON    = 6f;

        // ── Forge ─────────────────────────────────────────────────────────────
        public const float FORGE_WOOD       = 20f;
        public const float FORGE_STONE      = 20f;
        public const float FORGE_IRON       = 15f;

        // ── Watchtower ────────────────────────────────────────────────────────
        public const float WATCHTOWER_WOOD  = 10f;
        public const float WATCHTOWER_STONE = 30f;
        public const float WATCHTOWER_IRON  = 5f;
    }
}
