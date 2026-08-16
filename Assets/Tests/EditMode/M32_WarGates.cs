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

        // ── M32-T2: 판 종료 판정 (W2) ─────────────────────────────────────────
        //
        // 이 게이트가 지키는 것은 **끝이 한 번뿐이라는 것**이다. 전멸과 함락은 같은 프레임에
        // 성립할 수 있고(적이 중심에 선 순간 마지막 주민이 죽는다), 그때 둘 다 뜨거나 함락이
        // 이기면 같은 끝이 두 번 세어진다.

        [Test]
        public void M32_T2_ResolveRunEnd_FullTruthTable()
        {
            const M0SimulationLoop.RunEndReason NONE = M0SimulationLoop.RunEndReason.None;
            const M0SimulationLoop.RunEndReason WIPED = M0SimulationLoop.RunEndReason.Wiped;
            const M0SimulationLoop.RunEndReason OVERRUN = M0SimulationLoop.RunEndReason.Overrun;

            // 래치가 이미 내려갔으면 무엇이 참이든 None — 한 판은 한 번 끝난다 (8건 중 4건)
            foreach (bool ever in new[] { false, true })
                foreach (int alive in new[] { 0, 3 })
                    foreach (bool breach in new[] { false, true })
                        Assert.AreEqual(NONE, M0SimulationLoop.ResolveRunEnd(true, ever, alive, breach),
                            $"래치 후 재판정 (ever={ever}, alive={alive}, breach={breach})");

            // 舊 전멸 진리표 (M10-T6과 같은 결과 — 중립 불변식)
            Assert.AreEqual(NONE,  M0SimulationLoop.ResolveRunEnd(false, false, 0, false), "주민이 있던 적 없음 = 시작 전");
            Assert.AreEqual(NONE,  M0SimulationLoop.ResolveRunEnd(false, true, 1, false),  "생존자 있음");
            Assert.AreEqual(WIPED, M0SimulationLoop.ResolveRunEnd(false, true, 0, false),  "있던 마을이 0명 = 전멸");

            // 함락 — 사람이 살아 있는 채로 끝난다
            Assert.AreEqual(OVERRUN, M0SimulationLoop.ResolveRunEnd(false, true, 5, true), "중심 침범 = 함락");
            Assert.AreEqual(OVERRUN, M0SimulationLoop.ResolveRunEnd(false, false, 5, true),
                "주민 이력과 무관하게 중심이 뚫리면 함락 (전멸 전제와 별개 축)");

            // 🔑 동시 성립 = 전멸 우선 (사람이 다 죽은 마을에 「함락」은 두 번 세는 것)
            Assert.AreEqual(WIPED, M0SimulationLoop.ResolveRunEnd(false, true, 0, true),
                "전멸과 함락이 같은 프레임에 성립하면 전멸이다");
        }

        [Test]
        public void M32_T2_ShouldShowGameOver_StillAgreesWithLegacy()
        {
            // 舊 함수는 이제 껍질이다 — 껍질이 본체와 어긋나면 진리표가 두 벌이 된다.
            foreach (bool shown in new[] { false, true })
                foreach (bool ever in new[] { false, true })
                    foreach (int alive in new[] { 0, 1 })
                        Assert.AreEqual(!shown && ever && alive == 0,
                            M0SimulationLoop.ShouldShowGameOver(shown, ever, alive),
                            $"舊 전멸 판정 불변 (shown={shown}, ever={ever}, alive={alive})");
        }

        [Test]
        public void M32_T2_IsCenterBreached_IsManhattanAndInclusive()
        {
            Assert.IsTrue(M0SimulationLoop.IsCenterBreached(0, 0, 3), "중심 그 칸");
            Assert.IsTrue(M0SimulationLoop.IsCenterBreached(3, 0, 3), "경계 = 들어온 것");
            Assert.IsTrue(M0SimulationLoop.IsCenterBreached(-2, 1, 3), "맨해튼 3 = 경계 안");
            Assert.IsFalse(M0SimulationLoop.IsCenterBreached(2, 2, 3), "맨해튼 4 = 밖 (체비쇼프였다면 참이 된다)");
            Assert.IsFalse(M0SimulationLoop.IsCenterBreached(4, 0, 3), "한 칸 밖");
            Assert.IsTrue(M0SimulationLoop.IsCenterBreached(0, 0, 0), "반경 0 = 기지 타일 정확히");
            Assert.IsFalse(M0SimulationLoop.IsCenterBreached(1, 0, 0), "반경 0에서 옆 칸은 아니다");
            Assert.IsFalse(M0SimulationLoop.IsCenterBreached(1, 0, -5), "음수 반경은 0으로 (에셋 사고 방어)");
        }

        [Test]
        public void M32_T3_GameOverText_SplitsByReason()
        {
            var roster = new System.Collections.Generic.List<VillagerRecord>
            {
                new VillagerRecord { ShortName = "가", PersonalityName = "순둥이", JobName = "사냥꾼",
                                     BornDay = 0f, LeftDay = -1f, Cause = ExitCause.Alive },
            };

            string wiped = SeasonHud.ComposeGameOver(12, 2, roster, M0SimulationLoop.RunEndReason.Wiped);
            StringAssert.Contains("아무도 남지 않았다", wiped, "전멸 맺음말");
            StringAssert.DoesNotContain("함락", wiped, "전멸 화면에 함락 문구가 섞이면 사유가 뭉개진다");

            string overrun = SeasonHud.ComposeGameOver(12, 2, roster, M0SimulationLoop.RunEndReason.Overrun);
            StringAssert.Contains("함락", overrun, "함락 머리줄");
            StringAssert.DoesNotContain("아무도 남지 않았다", overrun,
                "함락은 사람이 살아 있는 채로 끝난다 — 전멸 문구를 쓰면 산 사람이 죽은 것으로 읽힌다");
            StringAssert.Contains("가 — 순둥이, 사냥꾼", overrun, "명부는 사유와 무관하게 같다");

            // 인자 3개 오버로드(기존 호출자·게이트)는 전멸 문구를 그대로 낸다 — 중립 불변식
            Assert.AreEqual(wiped, SeasonHud.ComposeGameOver(12, 2, roster), "舊 오버로드 = 전멸 문구");
        }
    }
}
