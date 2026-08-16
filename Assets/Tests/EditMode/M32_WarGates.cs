using AIVillage.Core;
using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M32 전쟁 축 게이트. W7(사냥) 분부터 — 이후 항목이 여기에 append 된다.
    ///
    /// 이 파일이 지키는 것은 **사냥이 열매의 다른 그림이 아니라는 것**이다.
    /// 사냥과 채집이 같은 노드를 보거나 산출이 같으면 "보급 방법을 하나 더"가 아니라
    /// 비싼 열매를 하나 더 만든 것이고, 그 차이는 컴파일로도 게이트로도 안 드러난다.
    /// </summary>
    public class M32_WarGates
    {
        private const string HUNT_PATH   = "Assets/M0Config/Actions/HuntGame.asset";
        private const string BERRY_PATH  = "Assets/M0Config/Actions/HarvestWildBerries.asset";
        private const string CATALOG_PATH = "Assets/M0Config/ActionCatalog.asset";

        private static int EffectValue(ActionSO a, SlotId slot)
        {
            foreach (SlotEffect e in a.Effects)
                if (e.Slot == slot) return e.Value;
            return 0;
        }

        [Test]
        public void M32_T7_Hunt_IsItsOwnSupplyLineNotAReskinnedBerry()
        {
            Assert.AreEqual(SlotId.NearDiscoveredGame, SlotId_DiscoveredOf(ResourceType.Game),
                "사냥감 발견 플래그가 배선되지 않았다 — 전제가 항상 0이라 사냥 플랜이 영영 안 선다");

            var hunt = AssetDatabase.LoadAssetAtPath<GatherActionSO>(HUNT_PATH);
            var berry = AssetDatabase.LoadAssetAtPath<GatherActionSO>(BERRY_PATH);
            Assert.IsNotNull(hunt, "HuntGame 로드");
            Assert.IsNotNull(berry, "HarvestWildBerries 로드");

            Assert.AreEqual(ResourceType.Game, hunt.TargetResource,
                "사냥이 열매와 같은 노드를 보면 '보급 방법 추가'가 아니라 그림 교체다");

            bool gatedOnGame = false;
            foreach (SlotCondition c in hunt.Preconditions)
                if (c.Slot == SlotId.NearDiscoveredGame) gatedOnGame = true;
            Assert.IsTrue(gatedOnGame, "사냥 전제가 사냥감 발견이 아니면 사냥감 없는 판에서도 계획된다");

            int huntGain = EffectValue(hunt, SlotId.MyRawFood);
            int berryGain = EffectValue(berry, SlotId.MyRawFood);
            Assert.Greater(huntGain, 0, "사냥 산출이 MyRawFood 로 들어오지 않는다 — 고기가 어디로도 안 간다");
            Assert.Greater(huntGain, berryGain,
                $"사냥 한 번({huntGain})이 열매 한 번({berryGain}) 이하면 더 비싸고 더 먼 열매일 뿐이다");
            Assert.Greater(hunt.BaseCost, berry.BaseCost,
                "산출만 크고 비용이 같으면 플래너가 열매를 영영 안 고른다 (한쪽이 죽는다)");

            var catalog = AssetDatabase.LoadAssetAtPath<ActionCatalog>(CATALOG_PATH);
            Assert.IsNotNull(catalog, "ActionCatalog 로드");
            Assert.Contains(hunt, catalog.Actions, "카탈로그 미등록 = 플래너가 모르는 액션 (ADR-M0-6)");
        }

        /// <summary>SlotIds.DiscoveredOf 의 얇은 래퍼 — null 이면 테스트 메시지가 읽히게.</summary>
        private static SlotId? SlotId_DiscoveredOf(ResourceType t) => SlotIds.DiscoveredOf(t);
    }
}
