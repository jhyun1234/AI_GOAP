using System.Linq;
using AIVillage.Core;
using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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
        // 🔑 **끝나는 조건은 하나다 — 마을이 0명이 되는 것.** 함락은 그 죽음이 **전부 적의 손**
        // 이었을 때의 이름이다 (사용자 판정 2026-08-16). 적이 마을에 들어온 것만으로는 아무것도
        // 끝나지 않는다 — 들어온 적은 싸워서 물리칠 수 있어야 게임이다.
        // 이 게이트가 막는 회귀: ①생존자가 있는데 판이 끝나는 것 ②굶어 죽은 사람이 섞였는데
        // 「적에게 함락됐다」고 말하는 것 ③아무도 안 죽은 판이 함락으로 읽히는 것.

        [Test]
        public void M32_T2_ResolveRunEnd_FullTruthTable()
        {
            const M0SimulationLoop.RunEndReason NONE = M0SimulationLoop.RunEndReason.None;
            const M0SimulationLoop.RunEndReason WIPED = M0SimulationLoop.RunEndReason.Wiped;
            const M0SimulationLoop.RunEndReason OVERRUN = M0SimulationLoop.RunEndReason.Overrun;

            // 래치가 이미 내려갔으면 무엇이 참이든 None — 한 판은 한 번 끝난다 (8건 전수)
            foreach (bool ever in new[] { false, true })
                foreach (int alive in new[] { 0, 3 })
                    foreach (bool combat in new[] { false, true })
                        Assert.AreEqual(NONE, M0SimulationLoop.ResolveRunEnd(true, ever, alive, combat),
                            $"래치 후 재판정 (ever={ever}, alive={alive}, combat={combat})");

            // 🔴 생존자가 한 명이라도 있으면 **어떤 사유로도** 판은 안 끝난다
            foreach (bool combat in new[] { false, true })
            {
                Assert.AreEqual(NONE, M0SimulationLoop.ResolveRunEnd(false, true, 1, combat),
                    $"생존자 1명 = 판 계속 (combat={combat})");
                Assert.AreEqual(NONE, M0SimulationLoop.ResolveRunEnd(false, true, 8, combat),
                    $"생존자 8명 = 판 계속 (combat={combat})");
                Assert.AreEqual(NONE, M0SimulationLoop.ResolveRunEnd(false, false, 0, combat),
                    $"주민이 있던 적 없음 = 시작 전 (combat={combat})");
            }

            // 0명 = 끝. 사유는 죽음의 출처로 갈린다
            Assert.AreEqual(WIPED, M0SimulationLoop.ResolveRunEnd(false, true, 0, false),
                "굶주림 등이 섞였다 = 전멸 (적이 다 한 일이 아니다)");
            Assert.AreEqual(OVERRUN, M0SimulationLoop.ResolveRunEnd(false, true, 0, true),
                "전원이 적에게 죽었다 = 함락");
        }

        [Test]
        public void M32_T2_AllDeathsByCombat_NeedsEveryDeathAndAtLeastOne()
        {
            Assert.IsFalse(M0SimulationLoop.AllDeathsByCombat(null), "명부 없음 = 함락 아님");
            Assert.IsFalse(M0SimulationLoop.AllDeathsByCombat(Roster()), "빈 명부 = 함락 아님 (빈 검사 함정)");
            Assert.IsFalse(M0SimulationLoop.AllDeathsByCombat(Roster(ExitCause.Alive)),
                "아무도 안 죽었으면 함락이 아니다");
            Assert.IsTrue(M0SimulationLoop.AllDeathsByCombat(Roster(ExitCause.Combat)), "한 명, 전투사");
            Assert.IsTrue(M0SimulationLoop.AllDeathsByCombat(
                Roster(ExitCause.Combat, ExitCause.Combat, ExitCause.Combat)), "전원 전투사");
            Assert.IsFalse(M0SimulationLoop.AllDeathsByCombat(
                Roster(ExitCause.Combat, ExitCause.Starvation)), "한 명이라도 굶어 죽었으면 전멸이다");
            Assert.IsFalse(M0SimulationLoop.AllDeathsByCombat(
                Roster(ExitCause.Combat, ExitCause.Unknown)), "사유 불명이 섞여도 함락이라 단정하지 않는다");
        }

        private static System.Collections.Generic.List<VillagerRecord> Roster(params ExitCause[] causes)
        {
            var list = new System.Collections.Generic.List<VillagerRecord>();
            for (int i = 0; i < causes.Length; i++)
                list.Add(new VillagerRecord { ShortName = $"주민{i}", PersonalityName = "순둥이",
                                              JobName = "사냥꾼", BornDay = 0f, LeftDay = 5f,
                                              Cause = causes[i] });
            return list;
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

        // ── M32-T4: 병과 (W3) ────────────────────────────────────────────────
        //
        // 재는 것은 **초당 피해**다. 간격·피해를 따로 보면 병과가 갈렸는지 알 수 없다 —
        // 느리고 센 도끼와 빠르고 약한 검이 같은 값일 수 있고, 그러면 무기는 그림만 다른
        // 같은 물건이다 (S3: 최대/최소 1.3배 이상).

        [Test]
        public void M32_T4_ShippedWeapons_FightDifferently()
        {
            var fight = AssetDatabase.LoadAssetAtPath<FightActionSO>("Assets/M0Config/Actions/Action_Fight.asset");
            Assert.IsNotNull(fight, "Action_Fight 로드 — 기준 간격의 출처");

            WeaponSO[] weapons = AssetDatabase.FindAssets("t:WeaponSO", new[] { "Assets/M0Config/Weapons" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<WeaponSO>)
                .Where(w => w != null)
                .ToArray();
            Assert.AreEqual(3, weapons.Length, "배포 무기는 검·활·도끼 셋 (팩에 있는 것만 — ADR-M32-8)");

            float lo = float.MaxValue, hi = 0f;
            foreach (WeaponSO w in weapons)
            {
                float dps = WeaponSO.DamagePerSec(fight.BaseHitSec, 1f, w.HitIntervalMult, w.Damage);
                Assert.Greater(dps, 0f, $"{w.name}: 초당 피해가 0 — 무기가 아니라 장식이다");
                lo = Mathf.Min(lo, dps); hi = Mathf.Max(hi, dps);
                Assert.GreaterOrEqual(w.RangeTiles, 1, $"{w.name}: 사거리 0이면 영영 못 친다");
            }
            Assert.GreaterOrEqual(hi / lo, 1.3f,
                $"초당 피해 최대/최소 = {hi / lo:0.##}배 — 1.3배 미만이면 병과가 그림 차이일 뿐이다 (S3)");

            // 사거리도 갈려야 한다 — 활이 검과 다른 **유일한 구조 인자**다
            int minRange = weapons.Min(w => w.RangeTiles);
            int maxRange = weapons.Max(w => w.RangeTiles);
            Assert.Greater(maxRange, minRange, "사거리가 전부 같으면 활이 검의 별명이다");
        }

        [Test]
        public void M32_T4_BareHands_StillFight()
        {
            // ADR-M21-5·M32-4: 무기는 게이트가 아니라 배율이다. 맨손 간격이 유한해야 한다.
            var fight = AssetDatabase.LoadAssetAtPath<FightActionSO>("Assets/M0Config/Actions/Action_Fight.asset");
            float bare = CombatService.HitInterval(fight.BaseHitSec, 1f, hasWeapon: false, weaponMult: 0.5f);
            Assert.Greater(bare, 0f, "맨손 타격 간격이 0 이하 = 못 싸운다");
            Assert.AreEqual(fight.BaseHitSec, bare, 1e-4f, "맨손은 무기 배율을 안 받는다 (중립)");

            var sword = AssetDatabase.LoadAssetAtPath<WeaponSO>("Assets/M0Config/Weapons/Weapon_Sword.asset");
            Assert.IsNotNull(sword, "검 로드");
            float armed = CombatService.HitInterval(fight.BaseHitSec, 1f, true, sword.HitIntervalMult);
            Assert.Less(armed, bare, "검이 맨손보다 느리면 무장할 이유가 없다");
        }

        [Test]
        public void M32_T4_CraftAction_NamesItsWeapon()
        {
            var craft = AssetDatabase.LoadAssetAtPath<CraftActionSO>("Assets/M0Config/Actions/Action_CraftWeapon.asset");
            Assert.IsNotNull(craft, "Action_CraftWeapon 로드");
            Assert.IsNotNull(craft.Weapon,
                "제작이 만드는 무기가 미지정 — 무장은 되지만 병과 수치가 액션 기본값으로 떨어진다");
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
