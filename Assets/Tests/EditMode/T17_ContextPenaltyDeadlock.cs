using NUnit.Framework;
using AIVillage.AI;

namespace AIVillage.Tests
{
    /// <summary>
    /// [T17] 문맥 배율 무해(無解) 회귀 방지 게이트.
    /// 근거: Docs/방향3_무해_문맥배율_명세서.md, Docs/이슈_GatherIron_초반_무해.md P1-A 진단.
    /// 상위 계층(GatherGoalSelector)이 미발견 자원을 goal 후보에서 제외하는지 검증한다.
    /// 회귀 시: 초반 GatherIron이 플래너에 무해 goal로 넘어가 A* 4096 노드 소진 → Deadlock.
    /// </summary>
    public class T17_ContextPenaltyDeadlock
    {
        [Test]
        public void UndiscoveredIron_should_not_be_selected_as_gather_goal()
        {
            string result = GatherGoalSelector.Select(
                woodStock: 40, stoneStock: 40, ironStock: 0,
                copperStock: 40, rawFoodStock: 40, hasTool: true,
                woodDiscovered: true, stoneDiscovered: true, ironDiscovered: false,
                copperDiscovered: true, foodDiscovered: true);
            Assert.AreNotEqual("GatherIron", result,
                "IronOre 미발견 상태에서 GatherIron이 반환되면 A* 4096 노드 소진 (Docs/이슈_GatherIron_초반_무해.md).");
        }

        [Test]
        public void DiscoveredIron_below_threshold_should_be_selected()
        {
            string result = GatherGoalSelector.Select(
                woodStock: 40, stoneStock: 40, ironStock: 5,
                copperStock: 40, rawFoodStock: 40, hasTool: true,
                woodDiscovered: true, stoneDiscovered: true, ironDiscovered: true,
                copperDiscovered: true, foodDiscovered: true);
            Assert.AreEqual("GatherIron", result);
        }

        [Test]
        public void AllUndiscovered_returns_null()
        {
            string result = GatherGoalSelector.Select(
                woodStock: 0, stoneStock: 0, ironStock: 0,
                copperStock: 0, rawFoodStock: 0, hasTool: true,
                woodDiscovered: false, stoneDiscovered: false, ironDiscovered: false,
                copperDiscovered: false, foodDiscovered: false);
            Assert.IsNull(result,
                "모든 자원 미발견 시 null 반환 → FSM P3 Explore로 폴백해야 한다.");
        }
    }
}
