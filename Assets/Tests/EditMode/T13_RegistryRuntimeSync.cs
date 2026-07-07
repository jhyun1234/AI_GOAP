using NUnit.Framework;
using AIVillage.Core.GOAP;
using AIVillage.UI;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// T13: GOAPActionRegistry의 수확/효과 상수가 기획서 원본 리터럴(런타임 실측 수치)과 일치하는지 검증한다.
    /// 어긋나면 플래너가 잘못된 스텝 수를 예측하여 MAX_DEPTH 초과·alreadySatisfied 루프를 유발한다.
    /// (이슈 #1 재발 방지 게이트 — 근본 진단 5개 층 이중 진리 문제의 자동 강제)
    ///
    /// 계약 방향(ADR-1): 기획서 원본 = 런타임(ActionDatabase/VillagerFSM/BuildingCosts).
    /// 플래너 Registry 상수가 런타임을 따라간다. 실패 시 어느 쪽이 원본인지 확인 후 정정한다.
    ///
    /// ADR-T5: 리플렉션 사용 금지(const 인라인 이슈). RUNTIME_* 상수를 하드코딩 = 계약 선언.
    /// 값이 바뀌면 이 상수와 Registry 양쪽을 함께 수정한다.
    /// </summary>
    public class T13_RegistryRuntimeSync
    {
        // ── 런타임 실제 수치 (기획서 원본 리터럴) ────────────────────────────
        // 이 상수들은 GDD v0.4 기획서 값이며 최초 도입 당시 VillagerFSM / ActionDatabase
        // 리터럴로 존재했다. 현재는 Registry 상수를 참조하도록 리팩터됐지만
        // 이 테스트는 "Registry 상수가 기획서 값과 일치한다"는 계약을 강제한다.
        //
        // 값을 바꾸려면 (1) 기획서 개정 승인 (2) 여기와 Registry 양쪽 동시 수정.
        private const int RUNTIME_CHOP_WOOD        = 10;  // VillagerFSM 원본 리터럴: WoodStock += 10f
        private const int RUNTIME_MINE_STONE       = 8;   // VillagerFSM 원본 리터럴: StoneStock += 8f
        private const int RUNTIME_MINE_IRON        = 5;   // VillagerFSM 원본 리터럴: IronStock += 5f
        private const int RUNTIME_MINE_COPPER      = 3;   // ActionDatabase MineCopper GainResource 3
        private const int RUNTIME_HARVEST_BERRIES  = 5;   // ActionDatabase HarvestWildBerries rawFood += 5
        private const int RUNTIME_COOK_RAW_CONSUME = 2;   // CookMeal 생 식량 소비 2 (Registry Precondition GreaterEq 2)
        private const int RUNTIME_COOK_YIELD       = 2;   // VillagerFSM 원본 리터럴: cookedFood += 2f
        private const int RUNTIME_MEDICAL_HEALTH   = 40;  // ActionDatabase GainHealth 40f

        // ── 건물 건설 비용 (BuildingCosts.cs 원본) ───────────────────────────
        // BuildingCosts는 float, Registry는 int. 정수부가 같아야 한다.
        private const int RUNTIME_HOUSE_WOOD       = 20;
        private const int RUNTIME_HOUSE_STONE      = 10;
        private const int RUNTIME_WATCHTOWER_WOOD  = 10;
        private const int RUNTIME_WATCHTOWER_STONE = 30;
        private const int RUNTIME_WATCHTOWER_IRON  = 5;

        // ──────────────────────────────────────────────────────────────────
        // YIELD / 효과 상수 8종
        // ──────────────────────────────────────────────────────────────────

        [Test]
        public void YIELD_CHOP_WOOD_matches_runtime()
            => Assert.AreEqual(
                RUNTIME_CHOP_WOOD, GOAPActionRegistry.YIELD_CHOP_WOOD,
                $"Registry YIELD_CHOP_WOOD({GOAPActionRegistry.YIELD_CHOP_WOOD}) != " +
                $"런타임 원본({RUNTIME_CHOP_WOOD}) — ADR-1 위반. " +
                "어느 쪽이 기획서 원본인지 확인 후 정정하세요. " +
                "위치: GOAPActionRegistry.YIELD_CHOP_WOOD 또는 VillagerFSM ChopWood 처리부.");

        [Test]
        public void YIELD_MINE_STONE_matches_runtime()
            => Assert.AreEqual(
                RUNTIME_MINE_STONE, GOAPActionRegistry.YIELD_MINE_STONE,
                $"Registry YIELD_MINE_STONE({GOAPActionRegistry.YIELD_MINE_STONE}) != " +
                $"런타임 원본({RUNTIME_MINE_STONE}) — ADR-1 위반. " +
                "위치: GOAPActionRegistry.YIELD_MINE_STONE 또는 VillagerFSM MineStone 처리부.");

        [Test]
        public void YIELD_MINE_IRON_matches_runtime()
            => Assert.AreEqual(
                RUNTIME_MINE_IRON, GOAPActionRegistry.YIELD_MINE_IRON,
                $"Registry YIELD_MINE_IRON({GOAPActionRegistry.YIELD_MINE_IRON}) != " +
                $"런타임 원본({RUNTIME_MINE_IRON}) — ADR-1 위반. " +
                "위치: GOAPActionRegistry.YIELD_MINE_IRON 또는 VillagerFSM MineIron 처리부.");

        [Test]
        public void YIELD_MINE_COPPER_matches_runtime()
            => Assert.AreEqual(
                RUNTIME_MINE_COPPER, GOAPActionRegistry.YIELD_MINE_COPPER,
                $"Registry YIELD_MINE_COPPER({GOAPActionRegistry.YIELD_MINE_COPPER}) != " +
                $"런타임 원본({RUNTIME_MINE_COPPER}) — ADR-1 위반. " +
                "위치: GOAPActionRegistry.YIELD_MINE_COPPER 또는 ActionDatabase MineCopper 효과부.");

        [Test]
        public void YIELD_HARVEST_BERRIES_matches_runtime()
            => Assert.AreEqual(
                RUNTIME_HARVEST_BERRIES, GOAPActionRegistry.YIELD_HARVEST_BERRIES,
                $"Registry YIELD_HARVEST_BERRIES({GOAPActionRegistry.YIELD_HARVEST_BERRIES}) != " +
                $"런타임 원본({RUNTIME_HARVEST_BERRIES}) — ADR-1 위반. " +
                "위치: GOAPActionRegistry.YIELD_HARVEST_BERRIES 또는 ActionDatabase HarvestWildBerries 효과부.");

        [Test]
        public void COOK_RAW_CONSUME_matches_runtime()
            => Assert.AreEqual(
                RUNTIME_COOK_RAW_CONSUME, GOAPActionRegistry.COOK_RAW_CONSUME,
                $"Registry COOK_RAW_CONSUME({GOAPActionRegistry.COOK_RAW_CONSUME}) != " +
                $"런타임 원본({RUNTIME_COOK_RAW_CONSUME}) — ADR-1 위반. " +
                "위치: GOAPActionRegistry.COOK_RAW_CONSUME 또는 ActionDatabase CookMeal 소비량.");

        [Test]
        public void COOK_YIELD_matches_runtime()
            => Assert.AreEqual(
                RUNTIME_COOK_YIELD, GOAPActionRegistry.COOK_YIELD,
                $"Registry COOK_YIELD({GOAPActionRegistry.COOK_YIELD}) != " +
                $"런타임 원본({RUNTIME_COOK_YIELD}) — ADR-1 위반. " +
                "위치: GOAPActionRegistry.COOK_YIELD 또는 VillagerFSM CookMeal 처리부.");

        [Test]
        public void MEDICAL_HEALTH_GAIN_matches_runtime()
            => Assert.AreEqual(
                RUNTIME_MEDICAL_HEALTH, GOAPActionRegistry.MEDICAL_HEALTH_GAIN,
                $"Registry MEDICAL_HEALTH_GAIN({GOAPActionRegistry.MEDICAL_HEALTH_GAIN}) != " +
                $"런타임 원본({RUNTIME_MEDICAL_HEALTH}) — ADR-1 위반. " +
                "위치: GOAPActionRegistry.MEDICAL_HEALTH_GAIN 또는 ActionDatabase SeekMedicalAid 효과부.");

        // ──────────────────────────────────────────────────────────────────
        // 건물 건설 비용 5종 (BuildingCosts.cs ↔ Registry)
        // ──────────────────────────────────────────────────────────────────

        [Test]
        public void BUILD_HOUSE_WOOD_matches_BuildingCosts()
        {
            Assert.AreEqual(
                RUNTIME_HOUSE_WOOD, GOAPActionRegistry.BUILD_HOUSE_WOOD,
                $"Registry BUILD_HOUSE_WOOD({GOAPActionRegistry.BUILD_HOUSE_WOOD}) != " +
                $"기획서 원본({RUNTIME_HOUSE_WOOD}) — ADR-1 위반.");
            Assert.AreEqual(
                (float)RUNTIME_HOUSE_WOOD, BuildingCosts.HOUSE_WOOD,
                $"BuildingCosts.HOUSE_WOOD({BuildingCosts.HOUSE_WOOD}) != " +
                $"기획서 원본({RUNTIME_HOUSE_WOOD}f) — BuildingCosts와 Registry가 갈라졌습니다.");
        }

        [Test]
        public void BUILD_HOUSE_STONE_matches_BuildingCosts()
        {
            Assert.AreEqual(
                RUNTIME_HOUSE_STONE, GOAPActionRegistry.BUILD_HOUSE_STONE,
                $"Registry BUILD_HOUSE_STONE({GOAPActionRegistry.BUILD_HOUSE_STONE}) != " +
                $"기획서 원본({RUNTIME_HOUSE_STONE}) — ADR-1 위반.");
            Assert.AreEqual(
                (float)RUNTIME_HOUSE_STONE, BuildingCosts.HOUSE_STONE,
                $"BuildingCosts.HOUSE_STONE({BuildingCosts.HOUSE_STONE}) != " +
                $"기획서 원본({RUNTIME_HOUSE_STONE}f) — BuildingCosts와 Registry가 갈라졌습니다.");
        }

        [Test]
        public void BUILD_WATCHTOWER_WOOD_matches_BuildingCosts()
        {
            Assert.AreEqual(
                RUNTIME_WATCHTOWER_WOOD, GOAPActionRegistry.BUILD_WATCHTOWER_WOOD,
                $"Registry BUILD_WATCHTOWER_WOOD({GOAPActionRegistry.BUILD_WATCHTOWER_WOOD}) != " +
                $"기획서 원본({RUNTIME_WATCHTOWER_WOOD}) — ADR-1 위반.");
            Assert.AreEqual(
                (float)RUNTIME_WATCHTOWER_WOOD, BuildingCosts.WATCHTOWER_WOOD,
                $"BuildingCosts.WATCHTOWER_WOOD({BuildingCosts.WATCHTOWER_WOOD}) != " +
                $"기획서 원본({RUNTIME_WATCHTOWER_WOOD}f).");
        }

        [Test]
        public void BUILD_WATCHTOWER_STONE_matches_BuildingCosts()
        {
            Assert.AreEqual(
                RUNTIME_WATCHTOWER_STONE, GOAPActionRegistry.BUILD_WATCHTOWER_STONE,
                $"Registry BUILD_WATCHTOWER_STONE({GOAPActionRegistry.BUILD_WATCHTOWER_STONE}) != " +
                $"기획서 원본({RUNTIME_WATCHTOWER_STONE}) — ADR-1 위반.");
            Assert.AreEqual(
                (float)RUNTIME_WATCHTOWER_STONE, BuildingCosts.WATCHTOWER_STONE,
                $"BuildingCosts.WATCHTOWER_STONE({BuildingCosts.WATCHTOWER_STONE}) != " +
                $"기획서 원본({RUNTIME_WATCHTOWER_STONE}f).");
        }

        [Test]
        public void BUILD_WATCHTOWER_IRON_matches_BuildingCosts()
        {
            Assert.AreEqual(
                RUNTIME_WATCHTOWER_IRON, GOAPActionRegistry.BUILD_WATCHTOWER_IRON,
                $"Registry BUILD_WATCHTOWER_IRON({GOAPActionRegistry.BUILD_WATCHTOWER_IRON}) != " +
                $"기획서 원본({RUNTIME_WATCHTOWER_IRON}) — ADR-1 위반.");
            Assert.AreEqual(
                (float)RUNTIME_WATCHTOWER_IRON, BuildingCosts.WATCHTOWER_IRON,
                $"BuildingCosts.WATCHTOWER_IRON({BuildingCosts.WATCHTOWER_IRON}) != " +
                $"기획서 원본({RUNTIME_WATCHTOWER_IRON}f).");
        }
    }
}
