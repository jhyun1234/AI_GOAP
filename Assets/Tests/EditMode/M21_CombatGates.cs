using System.Linq;
using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

using static AIVillage.Tests.EditMode.GateHelpers;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M21 격퇴 축 게이트. W1(체력 = 생존 단일 척도)분부터 — 이후 항목이 여기에 append 된다.
    ///
    /// 이 파일이 지키는 것은 **환산의 정직함**이다. 체력 도입은 검증된 두 사망 시계
    /// (아사 0.5일 · 부상 방치 1.5일)를 새 눈금으로 옮기는 일이라, 눈금이 조금만 어긋나도
    /// 컴파일도 통과하고 게이트도 통과한 채 몇 판 뒤 전멸로만 드러난다 (M17 수치의 짝).
    /// 그래서 게이트가 검사하는 것은 "체력이 있는가"가 아니라 **"소요 일수가 그대로인가"**다.
    /// </summary>
    public class M21_CombatGates
    {
        private const string CONFIG_PATH = "Assets/M0Config/AgentConfig.asset";

        private static AgentConfigSO NewCfg()
        {
            var c = ScriptableObject.CreateInstance<AgentConfigSO>();
            c.MaxHp = 100f;
            c.InjuredBelowHp = 67f;
            c.DepartAfterStarvingDays = 0.5f;
            c.InjuryDeathAfterDays = 1.5f;
            c.NearStarvationRatio = 0.8f;
            return c;
        }

        // ── M21-T1: 환산 검산 (W1 DoD ①②) ───────────────────────────────────

        [Test]
        public void M21_T1_AutoConversion_PreservesBothDeathClocks()
        {
            var c = NewCfg();

            Assert.AreEqual(c.DepartAfterStarvingDays, VillagerAgent.DaysToStarveDeath(c), 1e-4f,
                "아사 소요가 에셋 값과 어긋나면 굶주림 밸런스가 바뀐 것이다");
            Assert.AreEqual(c.InjuryDeathAfterDays, VillagerAgent.DaysToBleedDeath(c), 1e-4f,
                "부상 방치 사망 소요가 에셋 값과 어긋나면 간호 축(M11-I 결정 15)이 바뀐 것이다");

            // 자동 환산은 에셋 값이 바뀌어도 따라와야 한다 — 상수 하드코딩이면 여기서 깨진다.
            c.DepartAfterStarvingDays = 1.25f;
            c.InjuryDeathAfterDays = 3f;
            Assert.AreEqual(1.25f, VillagerAgent.DaysToStarveDeath(c), 1e-4f, "아사 소요는 에셋을 따른다");
            Assert.AreEqual(3f, VillagerAgent.DaysToBleedDeath(c), 1e-4f, "출혈 소요는 에셋을 따른다");

            Object.DestroyImmediate(c);
        }

        [Test]
        public void M21_T1_ExplicitRatesOverrideConversion()
        {
            var c = NewCfg();
            c.StarveHpLossPerDay = 25f;  // 명시하면 자동 환산을 덮는다
            c.BleedHpLossPerDay = 33.5f;

            Assert.AreEqual(4f, VillagerAgent.DaysToStarveDeath(c), 1e-4f, "명시 감쇠율 우선");
            Assert.AreEqual(2f, VillagerAgent.DaysToBleedDeath(c), 1e-4f, "명시 출혈율 우선");

            Object.DestroyImmediate(c);
        }

        [Test]
        public void M21_T1_FirstStrike_InjuresButDoesNotKill()
        {
            var c = NewCfg();
            // W2에서 뒤집힌 자리: 부상은 이제 **피해의 결과**다 (舊 W1은 Injure가 진입 피해를 줬다).
            // 위협 한 대(제안 34)가 부상선 아래로 내려보내되 즉사·'죽을 뻔'은 아니어야 한다 —
            // 한 대에 죽으면 개입할 시간이 없고, 부상선에 못 닿으면 물려도 멀쩡하다.
            const float STRIKE = 34f; // §4 제안치 (배포 에셋 대조는 M21_T6)
            float after = c.MaxHp - STRIKE;
            Assert.Less(after, c.InjuredBelowHp, "한 대면 부상선 아래 — 아니면 물려도 안 다친다");
            Assert.IsFalse(VillagerAgent.IsDead(after), "한 대로 죽지 않는다 (개입 창)");
            Assert.IsFalse(VillagerAgent.IsNearDeath(after, c), "한 대만으로 '죽을 뻔'은 아니다");
            Assert.IsTrue(VillagerAgent.IsDead(c.MaxHp - STRIKE * 3f), "세 대면 죽는다 (§4 3대=사망)");

            Object.DestroyImmediate(c);
        }

        // ── M21-T2: 사망·저점 판정의 경계 ────────────────────────────────────

        [Test]
        public void M21_T2_IsDead_ZeroIsTheOnlyThreshold()
        {
            Assert.IsFalse(VillagerAgent.IsDead(100f), "만복 생존");
            Assert.IsFalse(VillagerAgent.IsDead(0.01f), "한 톨 남아도 산다");
            Assert.IsTrue(VillagerAgent.IsDead(0f), "0 = 사망");
            Assert.IsTrue(VillagerAgent.IsDead(-5f), "음수도 사망 (클램프 전 값 방어)");
        }

        [Test]
        public void M21_T2_NearDeathLine_MatchesLegacyStarvationPoint()
        {
            var c = NewCfg();
            // 舊 판정(굶주림 누적 0.4일 = 0.5 × 0.8)이 만복 기준 몇 체력이었는지 역산해
            // 새 선과 같은 자리인지 본다 — 같지 않으면 "죽을 뻔했다"의 희소성이 바뀐 것이다.
            float legacyDays = c.DepartAfterStarvingDays * c.NearStarvationRatio;
            float hpAtLegacyPoint = c.MaxHp - c.StarveHpPerDay * legacyDays;
            Assert.IsTrue(VillagerAgent.IsNearDeath(hpAtLegacyPoint, c),
                "舊 기록 지점에서 새 판정도 참이어야 한다 (환산 일치)");
            Assert.IsFalse(VillagerAgent.IsNearDeath(hpAtLegacyPoint + 0.01f, c),
                "그 직전에는 아직 거짓 — 선이 앞당겨지면 전원이 기록돼 희소성이 사라진다");

            Object.DestroyImmediate(c);
        }

        // ── M21-T3: 중립 불변식 — 에셋 기본값이 기존 판을 바꾸지 않는가 ──────

        [Test]
        public void M21_T3_ShippedConfig_KeepsVerifiedTimings()
        {
            var c = AssetDatabase.LoadAssetAtPath<AgentConfigSO>(CONFIG_PATH);
            Assert.IsNotNull(c, "AgentConfig 로드");

            Assert.Greater(c.MaxHp, 0f, "최대 체력이 0이면 전원 즉사한다");
            Assert.Greater(c.InjuredBelowHp, 0f, "부상선이 0이면 부상 진입 피해가 곧 즉사다");
            Assert.Less(c.InjuredBelowHp, c.MaxHp, "부상선이 만복 이상이면 태어나자마자 부상이다");

            // 실제 배포 에셋에서도 두 시계가 보존되는가 (게이트가 테스트용 값에서만 통과하면
            // 정작 게임에서 어긋나 있어도 green이다 — M17이 세 번 반복한 실수의 형태)
            Assert.AreEqual(c.DepartAfterStarvingDays, VillagerAgent.DaysToStarveDeath(c), 1e-3f,
                "배포 에셋의 아사 소요 보존");
            Assert.AreEqual(c.InjuryDeathAfterDays, VillagerAgent.DaysToBleedDeath(c), 1e-3f,
                "배포 에셋의 부상 방치 사망 소요 보존");

            // 회복이 감쇠보다 느리면 문턱 근처를 오가는 주민이 조용히 깎여 죽는다
            // (舊 규칙은 한 끼면 즉시 리셋이었다 — 대칭 이상이어야 그 의도가 보존된다).
            Assert.GreaterOrEqual(c.SatedHpRegen, c.StarveHpPerDay,
                "회복률이 굶주림 감쇠보다 느리면 겨울에 조용한 전멸이 난다 (리뷰① 검산 지점)");
        }

        // ── M21-T4·T5: 체류 판정 (W2 DoD ①②) ────────────────────────────────

        [Test]
        public void M21_T4_ShouldRepeatStrike_PeriodBoundary()
        {
            // 주기 = 게임일. 경계에서 정확히 한 번 열려야 한다 — 미만이면 매 프레임 타격,
            // 초과 조건으로만 열면 dt가 커진 프레임에서 한 박자씩 밀린다.
            Assert.IsFalse(ThreatService.ShouldRepeatStrike(10f, 10.24f, 0.25f), "주기 직전엔 안 친다");
            Assert.IsTrue(ThreatService.ShouldRepeatStrike(10f, 10.25f, 0.25f), "경계에서 친다");
            Assert.IsTrue(ThreatService.ShouldRepeatStrike(10f, 12f, 0.25f), "밀린 틱도 친다");
            // 에셋 사고 방어: 주기 0은 "쉬지 않고"가 아니라 "치지 않음"으로 닫는다
            Assert.IsFalse(ThreatService.ShouldRepeatStrike(10f, 99f, 0f), "주기 0 = 매 프레임 타격 금지");
            Assert.IsFalse(ThreatService.ShouldRepeatStrike(10f, 99f, -1f), "음수 주기도 닫는다");
        }

        [Test]
        public void M21_T5_ShouldGiveUpStay_IsTerrainBoulderNotMercy()
        {
            // 상한은 자비가 아니라 지형 보루다 (닿을 수 없는 자리에 눌러앉은 개체 정리).
            Assert.IsFalse(ThreatService.ShouldGiveUpStay(5f, 6.9f, 2f), "상한 전에는 안 물러난다");
            Assert.IsTrue(ThreatService.ShouldGiveUpStay(5f, 7f, 2f), "상한에서 물러난다");
            Assert.IsFalse(ThreatService.ShouldGiveUpStay(5f, 99f, 0f), "상한 0 = 도착 즉시 퇴장 금지");

            // 상한이 재타격 주기보다 짧으면 위협이 한 대도 못 치고 나간다 (배포 검산은 T6)
            Assert.IsTrue(ThreatService.ShouldGiveUpStay(0f, 0.25f, 0.2f),
                "상한 < 주기면 첫 타격 뒤 곧바로 퇴장 — 에셋에서 이 조합을 만들면 위협이 사라진다");
        }

        // ── M21-T6: 배포 위협 에셋의 전투 수치 정합 (W2 DoD ⑤) ───────────────

        [Test]
        public void M21_T6_ShippedThreats_CombatNumbersAreCoherent()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<AgentConfigSO>(CONFIG_PATH);
            Assert.IsNotNull(cfg, "AgentConfig 로드");

            string[] guids = AssetDatabase.FindAssets("t:ThreatSO");
            Assert.Greater(guids.Length, 0, "배포 위협 에셋이 하나도 없다");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<ThreatSO>(path);
                Assert.IsNotNull(so, $"{path} 로드");

                Assert.Greater(so.MaxHp, 0f, $"{so.name}: 체력 0이면 태어나자마자 죽는다");
                Assert.Greater(so.StrikeDamage, 0f, $"{so.name}: 피해 0이면 아무도 안 다친다");
                Assert.Greater(so.RepeatStrikePeriodDays, 0f, $"{so.name}: 주기 0 = 매 프레임 타격");
                Assert.Greater(so.MaxStayDays, 0f, $"{so.name}: 상한 0 = 도착 즉시 퇴장");
                Assert.IsTrue(so.FleeBelowHpPct >= 0f && so.FleeBelowHpPct <= 1f,
                    $"{so.name}: 도주선은 0~1 비율");

                // 한 대 맞으면 다친다 — 이 선을 못 넘으면 체류형 위협이 "따라다니기만 하는 장식"이 된다
                Assert.Less(cfg.MaxHp - so.StrikeDamage, cfg.InjuredBelowHp,
                    $"{so.name}: 한 대로 부상선에 못 닿는다 (피해 {so.StrikeDamage} / 필요 {cfg.MaxHp - cfg.InjuredBelowHp})");
                // 한 대로 죽지는 않는다 — 개입할 틈이 남아야 M13의 목적("제때 개입")이 성립한다
                Assert.Less(so.StrikeDamage, cfg.MaxHp,
                    $"{so.name}: 한 대에 즉사 — 플레이어가 개입할 창이 사라진다");
                // 상한 안에 최소 한 번은 다시 칠 수 있어야 체류가 체류다
                Assert.Greater(so.MaxStayDays, so.RepeatStrikePeriodDays,
                    $"{so.name}: 체류 상한이 재타격 주기보다 짧으면 눌러앉는 의미가 없다");
            }
        }

        // ── M21-T7: 배회 + 계절 연동 (W2R DoD ③④⑥) ──────────────────────────

        private static SeasonSO NewSeason(string name, bool isCrisis, bool forageFrozen)
        {
            var s = ScriptableObject.CreateInstance<SeasonSO>();
            s.DisplayName = name;
            s.IsCrisis = isCrisis;
            s.ForageFrozen = forageFrozen;
            return s;
        }

        [Test]
        public void M21_T7_IsPredatorHungry_IsForageFrozen_NotIsCrisis()
        {
            // 🔴 이 게이트가 존재하는 이유 (명세 실사 R): "겨울"을 IsCrisis로 판정하면
            // Season_Summer도 IsCrisis:1 이라 **여름 늑대가 사나워진다**.
            Assert.IsTrue(ThreatService.IsPredatorHungry(NewSeason("겨울", true, true)),
                "겨울 = 채집 봉쇄 = 야수도 굶는다");
            Assert.IsFalse(ThreatService.IsPredatorHungry(NewSeason("여름", true, false)),
                "여름은 위기지만 봉쇄가 아니다 — 배율이 걸리면 안 된다 (ADR-M21-9)");
            Assert.IsFalse(ThreatService.IsPredatorHungry(NewSeason("평온", false, false)));
            Assert.IsFalse(ThreatService.IsPredatorHungry(null), "계절 없는 판 = 중립");
        }

        [Test]
        public void M21_T7_SeasonMultipliers_ClampAndIgnoreWeakening()
        {
            // 확률: 배고픈 계절에만 곱하고 [0,1] 클램프
            Assert.AreEqual(0.25f, ThreatService.EffectiveVillagerChance(0.25f, false, 2f), 1e-5f,
                "평시엔 기저 확률 그대로");
            Assert.AreEqual(0.5f, ThreatService.EffectiveVillagerChance(0.25f, true, 2f), 1e-5f,
                "겨울엔 ×2");
            Assert.AreEqual(1f, ThreatService.EffectiveVillagerChance(0.75f, true, 2f), 1e-5f,
                "1 초과는 클램프 — 곰·무리는 겨울에 반드시 주민을 노린다");
            Assert.AreEqual(0f, ThreatService.EffectiveVillagerChance(0f, true, 2f), 1e-5f,
                "기저 0은 겨울에도 0 — 배율은 없는 것을 만들지 않는다");
            Assert.AreEqual(0.25f, ThreatService.EffectiveVillagerChance(0.25f, true, 0.5f), 1e-5f,
                "1 미만 배율은 무시 — 배고픈 계절이 더 순해질 수는 없다");

            // 체류 상한: 같은 규약
            Assert.AreEqual(0.75f, ThreatService.EffectiveStayDays(0.75f, false, 2f), 1e-5f);
            Assert.AreEqual(1.5f, ThreatService.EffectiveStayDays(0.75f, true, 2f), 1e-5f,
                "겨울 체류 0.75 → 1.5일 (최대 6회 타격)");
            Assert.AreEqual(0.75f, ThreatService.EffectiveStayDays(0.75f, true, 0.5f), 1e-5f,
                "1 미만 배율은 무시");
        }

        [Test]
        public void M21_T7_SeasonedRoll_StaysDeterministic()
        {
            // ADR-M21-10 판정 쪽: 계절이 바꾸는 것은 확률값이지 난수원이 아니다.
            var so = ScriptableObject.CreateInstance<ThreatSO>();
            so.DisplayName = "외로운 늑대";
            so.VillagerTargetChance = 0.25f;

            for (int ord = 1; ord <= 50; ord++)
                Assert.AreEqual(ThreatService.RollTargetsVillagers(so, ord, 0.5f),
                                ThreatService.RollTargetsVillagers(so, ord, 0.5f),
                                $"서수 {ord} 재현 — 같은 판이면 같은 결과");

            // (舊 "2인자판 = 3인자판" 동치 단언은 2026-08-11 2차 감사에서 2인자판 철거와 함께 삭제)

            // 확률을 올리면 주민 타깃이 늘어난다 (단조) — 겨울 보정이 실제로 기울이는가
            int plain = 0, winter = 0;
            for (int ord = 1; ord <= 200; ord++)
            {
                if (ThreatService.RollTargetsVillagers(so, ord, 0.25f)) plain++;
                if (ThreatService.RollTargetsVillagers(so, ord, 0.5f)) winter++;
            }
            Assert.Greater(winter, plain, "겨울 확률이 높은데 주민 타깃이 늘지 않았다");
        }

        /// <summary>티어1 위협 = 배포 에셋 중 UnlockDay 최소 (동률은 이름 사전순 — 결정적).
        /// 🔑 **이름으로 붙잡지 않는다** (M24-1차): 舊 게이트는 `Threat_Tier1_Wolf.asset` 경로를
        /// 직접 열어서, 콘텐츠가 늑대에서 종족으로 바뀌자 통째로 red 가 됐다. 지키려던 불변식은
        /// "티어1은 평시에도 주민을 노린다"이지 "늑대라는 파일이 있다"가 아니었다.</summary>
        private static ThreatSO Tier1Threat()
        {
            ThreatSO best = null;
            foreach (string guid in AssetDatabase.FindAssets("t:ThreatSO"))
            {
                var so = AssetDatabase.LoadAssetAtPath<ThreatSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (so == null) continue;
                if (best == null || so.UnlockDay < best.UnlockDay
                    || (so.UnlockDay == best.UnlockDay && string.CompareOrdinal(so.name, best.name) < 0))
                    best = so;
            }
            Assert.IsNotNull(best, "배포 위협 에셋이 하나도 없다 — 티어1을 고를 수 없다");
            return best;
        }

        [Test]
        public void M21_T7_Tier1Threat_TargetsVillagersInPlainSeasons()
        {
            // W2R DoD ④ — Tier1이 **평시에도** 주민을 노리는 출몰이 존재해야 한다.
            // M10 시절 VillagerTargetChance:0("밭 전용")의 개정이고, 0으로 되돌아가면 여기서 잡힌다.
            ThreatSO wolf = Tier1Threat();
            Assert.Greater(wolf.VillagerTargetChance, 0f,
                $"{wolf.name}: 티어1이 다시 밭 전용이 됐다 — 평시 주민 타깃이 영영 안 온다");

            int villagerRolls = 0;
            for (int ord = 1; ord <= 100; ord++)
                if (ThreatService.RollTargetsVillagers(wolf, ord, wolf.VillagerTargetChance)) villagerRolls++;
            Assert.Greater(villagerRolls, 0,
                "100번의 출몰 서수 중 주민 타깃이 한 번도 없다 (결정적 시드 — 확률이 너무 낮다)");

            // 주민을 노리는데 할 말이 없으면 화면에서 아무 일도 안 일어난다.
            // W2R 합의 한 줄이 요구하는 것은 동선 **과 주민의 반응** 둘 다다.
            Assert.IsNotNull(wolf.StrikeLinesVillager, "주민 피격 대사 미배선");
            Assert.Greater(wolf.StrikeLinesVillager.Length, 0,
                "주민을 노리는 위협에 피격 대사가 없다 — 물렸는데 아무도 말이 없다");
        }

        [Test]
        public void M21_T7_ShippedThreats_WanderIsVisible()
        {
            // W2R DoD ⑥ — 배회가 실제로 화면에서 움직임으로 보이는 값인가.
            foreach (string guid in AssetDatabase.FindAssets("t:ThreatSO"))
            {
                var so = AssetDatabase.LoadAssetAtPath<ThreatSO>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.Greater(so.WanderRadiusTiles, 0,
                    $"{so.name}: 배회 반경 0 = 도착 지점 정지 = W2R 이전 동작으로 회귀");
                Assert.Greater(so.WanderRadiusTiles, so.StrikeRadiusTiles,
                    $"{so.name}: 배회 반경({so.WanderRadiusTiles}) ≤ 타격 반경({so.StrikeRadiusTiles}) — " +
                    "사거리 밖으로 안 나가 '돌다가 다가온다'가 안 보인다");
                Assert.GreaterOrEqual(so.HungrySeasonChanceMult, 1f,
                    $"{so.name}: 배고픈 계절 확률 배율이 1 미만 — 겨울에 오히려 순해진다");
                Assert.GreaterOrEqual(so.HungrySeasonStayMult, 1f,
                    $"{so.name}: 배고픈 계절 체류 배율이 1 미만");

                // 겨울 상한(×배율)에서도 재타격이 최소 한 번은 더 들어가야 "더 집요하다"가 성립
                float winterStay = ThreatService.EffectiveStayDays(
                    so.MaxStayDays, true, so.HungrySeasonStayMult);
                Assert.Greater(winterStay, so.MaxStayDays,
                    $"{so.name}: 겨울 체류가 평시와 같다 — 계절 연동이 값에서 사라졌다");
            }
        }

        // ── M21-T8: 전투 판정 순수 함수 (W3 DoD ②) ───────────────────────────

        [Test]
        public void M21_T8_ShouldFlee_IsStrictlyBelowTheLine()
        {
            // 경계에서 **아직 버틴다** — 0.4 도주선에 정확히 40%면 한 대 더 맞아야 물러난다.
            // 이하로 열면 "몰아붙였다"는 감각이 한 타 일찍 끊긴다.
            // 🔴 이 줄이 실제로 red 였다 (2026-08-07): 60f*0.4f = 24.0000003 이라 24 < 24.0000003 이
            // 참이 되어 규약이 반대로 뒤집혔다. 가상의 경계가 아니다 — 체력 60·피해 12면
            // 60→48→36→24 로 **정확히** 닿는다. CombatService.HP_EPSILON 이 이걸 막는다.
            Assert.IsFalse(CombatService.ShouldFlee(24f, 60f, 0.4f), "정확히 40% = 아직 버틴다");
            Assert.IsFalse(CombatService.ShouldFlee(60f, 150f, 0.4f), "곰(150)에서도 경계는 버틴다");
            Assert.IsTrue(CombatService.ShouldFlee(23f, 60f, 0.4f), "40% 미만 = 물러난다");
            Assert.IsTrue(CombatService.ShouldFlee(0f, 60f, 0.4f), "체력 0도 참 (사망 판정이 먼저 가로챈다)");

            // 에셋 사고 방어
            Assert.IsFalse(CombatService.ShouldFlee(30f, 60f, 0f), "도주선 0 = 끝까지 싸운다");
            Assert.IsFalse(CombatService.ShouldFlee(30f, 0f, 0.4f), "최대 체력 0 = 판정 불가, 도주 없음");
            Assert.IsTrue(CombatService.ShouldFlee(59f, 60f, 1f), "도주선 1 = 첫 피격에 물러난다");
        }

        [Test]
        public void M21_T8_ShouldRout_NeedsASurvivingGroup()
        {
            // 이중 도주선의 바깥쪽 — 개체는 자기 체력, 무리는 잔존율.
            Assert.IsTrue(CombatService.ShouldRout(2, 5, 0.5f), "5마리 중 2마리 = 40% < 50% → 무너진다");
            Assert.IsFalse(CombatService.ShouldRout(3, 5, 0.5f), "60%면 아직 싸운다");
            Assert.IsFalse(CombatService.ShouldRout(1, 2, 0.5f), "정확히 50% = 경계는 버틴다");

            // 🔴 전멸에 "무리 도주"를 겹쳐 부르지 않는다 — 사건이 두 번 세어진다
            Assert.IsFalse(CombatService.ShouldRout(0, 5, 0.5f), "잔존 0 = 판정할 무리가 없다");
            Assert.IsFalse(CombatService.ShouldRout(1, 0, 0.5f), "스폰 0 = 무리가 아니다");
            // 마릿수 확장(W6) 전 실제 상태 — 1마리 출몰에서는 절대 발동하지 않아야 한다
            Assert.IsFalse(CombatService.ShouldRout(1, 1, 0.5f), "1마리 출몰 = 무리 도주 없음 (W6 전 현재 상태)");
        }

        [Test]
        public void M21_T8_HitInterval_MultipliersCombineAndClamp()
        {
            const float BASE = 4f; // §4 제안치

            Assert.AreEqual(4f, CombatService.HitInterval(BASE, 1f, false, 0.5f), 1e-4f,
                "맨손·무직 = 기본 간격 (무기 배율은 무기가 없으면 안 걸린다)");
            Assert.AreEqual(2f, CombatService.HitInterval(BASE, 1f, true, 0.5f), 1e-4f,
                "무기 = 2배 빠름");
            Assert.AreEqual(2f, CombatService.HitInterval(BASE, 0.5f, false, 0.5f), 1e-4f,
                "사냥꾼 맨손 = 2배 빠름");
            Assert.AreEqual(1f, CombatService.HitInterval(BASE, 0.5f, true, 0.5f), 1e-4f,
                "무장 사냥꾼 = 곱 결합으로 4배 (M20 배율 문법)");

            // 에셋 사고 방어 — 배율 0/음수는 무시하고 1로 본다 (0이면 간격 0 = 매 프레임 타격)
            Assert.AreEqual(4f, CombatService.HitInterval(BASE, 0f, true, 0f), 1e-4f,
                "배율 0 = 무시하고 1 (프레임 타격 금지)");
            Assert.AreEqual(4f, CombatService.HitInterval(BASE, -1f, false, 1f), 1e-4f, "음수 배율도 무시");
            Assert.GreaterOrEqual(CombatService.HitInterval(0f, 1f, false, 1f), 0.01f,
                "기본 간격 0이어도 최소 0.01초 — 어떤 조합에서도 매 프레임 타격은 없다");
        }

        [Test]
        public void M21_T8_ShippedThreats_CanBeRepelledBeforeDying()
        {
            // 도주선이 체력보다 위에 있으면 격퇴가 사냥보다 먼저 온다 = "이겨도 안 죽일 수 있다".
            // 이게 무너지면 ADR-M21-1("격퇴하며 버티는")이 "몰살하며 버티는"이 된다.
            foreach (string guid in AssetDatabase.FindAssets("t:ThreatSO"))
            {
                var so = AssetDatabase.LoadAssetAtPath<ThreatSO>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.Greater(so.FleeBelowHpPct, 0f,
                    $"{so.name}: 도주선 0 = 죽여야만 끝난다 (격퇴 축이 사라진다)");
                Assert.Less(so.FleeBelowHpPct, 1f,
                    $"{so.name}: 도주선 1 = 첫 피격에 도주 (교전이 성립하지 않는다)");
                Assert.GreaterOrEqual(so.MeatDrop, 0,
                    $"{so.name}: 드랍이 음수 — 사냥이 창고를 깎는다");
            }
        }

        // ── M21-T9: 교전 goal — 피신과의 대칭 (W4 DoD ①③⑤) ─────────────────

        private static WorldSnapshot SnapThreat(int threatNear)
        {
            var slots = new int[PlanningConfig.TotalSlots];
            slots[(int)SlotId.ThreatNear] = threatNear;
            return new WorldSnapshot(slots);
        }

        [Test]
        public void M21_T9_FightAndFlee_AreTheSameTriggerOppositeNerve()
        {
            GoalSO fight = LoadGoal("Goal_Fight"), flee = LoadGoal("Goal_Flee");

            // 🔑 설계의 핵심 — 동률 + 부호 대칭. 높이면 겁쟁이도 싸우고, 낮추면 아무도 안 싸운다
            // (명세 ⚠️ 오해 위험 ①). 승부는 우선순위가 아니라 성향이 가른다.
            Assert.AreEqual(flee.Priority, fight.Priority,
                "교전과 피신은 동률이어야 한다 — 우선순위로 이기면 성향 대칭이 무의미해진다");

            float fightCaution = fight.TraitWeights.First(w => w.Trait == TraitId.Caution).Weight;
            float fleeCaution  = flee.TraitWeights.First(w => w.Trait == TraitId.Caution).Weight;
            Assert.Less(fightCaution, 0f, "교전은 겁에 **음의** 가중치 (담대할수록 싸운다)");
            Assert.Greater(fleeCaution, 0f, "피신은 겁에 **양의** 가중치 (조심할수록 도망친다)");
            Assert.AreEqual(-fleeCaution, fightCaution, 1e-4f, "부호만 반대이고 크기는 같아야 대칭이다");

            // 같은 트리거 — 같은 세계 상태가 둘을 함께 켠다 (ADR-M12-4 ③ⓐ 논증의 근거)
            Assert.AreEqual(flee.TriggerConditions.Length, fight.TriggerConditions.Length);
            Assert.AreEqual(flee.TriggerConditions[0].Slot, fight.TriggerConditions[0].Slot,
                "교전과 피신은 같은 트리거 슬롯(ThreatNear)을 본다");
        }

        [Test]
        public void M21_T9_TraitDecidesWhoFightsAndWhoRuns()
        {
            // W4 DoD ① — 같은 판, 같은 위협, 갈리는 행동. 성향만 다르다.
            GoalSO fight = LoadGoal("Goal_Fight"), flee = LoadGoal("Goal_Flee");
            var selector = new GoalSelector(new[] { fight, flee });
            WorldSnapshot threatNear = SnapThreat(1);

            var timid = new[] { new TraitValue { Trait = TraitId.Caution, Value = 100 } };
            var brave = new[] { new TraitValue { Trait = TraitId.Caution, Value = -100 } };

            System.Func<GoalSO, int> Bias(TraitValue[] t) => g => g.TraitBoost(t);

            Assert.AreSame(flee, selector.Select(threatNear, null, null, Bias(timid)),
                "겁 많은 주민은 피신한다");
            Assert.AreSame(fight, selector.Select(threatNear, null, null, Bias(brave)),
                "담대한 주민은 맞선다");

            // 중립 성격은 동률 — 배열 순서(피신 우선)가 가른다. 갈라지는 것 자체가 요점이라
            // 중립의 승자는 고정하지 않고 "둘 중 하나"만 확인한다.
            GoalSO neutral = selector.Select(threatNear, null, null, null);
            Assert.IsTrue(neutral == fight || neutral == flee, "중립도 둘 중 하나는 고른다");

            // 위협이 없으면 둘 다 안 뜬다 (트리거 = 세계 상태, ADR-M12-4 ③ⓑ 자기 종료)
            Assert.IsNull(selector.Select(SnapThreat(0), null, null, Bias(brave)),
                "위협이 사라지면 교전 goal도 함께 꺼진다");
        }

        [Test]
        public void M21_T9_OrderedFight_BypassesTriggerButAutonomyDoesNot()
        {
            // W4 DoD ⑤ / 재검사 B1 — 원정은 **플레이어 개입의 값**이다.
            // 자율에 열면 전 맵 주민이 밭 늑대 한 마리로 몰려간다 (명세 ⚠️ 오해 위험 ④).
            GoalSO fight = LoadGoal("Goal_Fight");
            var selector = new GoalSelector(new[] { fight });
            WorldSnapshot far = SnapThreat(0); // 위협이 멀다 = 트리거 미발동

            Assert.IsNull(selector.Select(far), "자율: 위협이 멀면 교전 goal이 뜨지 않는다");
            Assert.AreSame(fight, selector.Select(far, null, fight),
                "명령: 트리거를 우회해 원정한다 (빈 플랜 즉시 완료 0건의 근거)");

            Assert.IsTrue(fight.OrderedIgnoresTrigger, "특례 플래그가 꺼지면 명령이 공수표가 된다");
        }

        [Test]
        public void M21_T9_OrderedFight_TraitBiasDoesNotDemoteTheOrder()
        {
            // 리뷰① 관측 (2026-08-08): 겁 많은 주민에게 「싸워라」를 내렸는데 휴식·딴 일을 했다 —
            // 명령에도 성향 보정이 걸려 105 − 24 ≈ 81 로 깎여 피로(90)에 밀린 것.
            // 명령의 무게는 촌장이 정하지 기질이 깎지 않는다 (거부는 수락 시점 JudgeOrder 몫).
            GoalSO fight = LoadGoal("Goal_Fight"), fatigue = LoadGoal("Goal_P0_Fatigue");
            var selector = new GoalSelector(new[] { fight, fatigue });

            var slots = new int[PlanningConfig.TotalSlots];
            slots[(int)SlotId.MyFatigue] = 100; // 피로 트리거(≥90) 발동 — 경쟁자 세우기
            var snap = new WorldSnapshot(slots); // ThreatNear 0 — 명령 원정 특례(B1)로만 후보

            var timid = new[] { new TraitValue { Trait = TraitId.Caution, Value = 100 } };
            Assert.AreSame(fight, selector.Select(snap, null, fight, g => g.TraitBoost(timid)),
                "겁쟁이도 수락한 명령은 수행한다 — 성향 보정이 명령을 피로(90) 아래로 깎으면 안 된다");
        }

        [Test]
        public void M21_T9_FightAsset_IsDirectPoolAndInjuryFiltered()
        {
            GoalSO fight = LoadGoal("Goal_Fight");

            // DirectActionPool 필수 — 도망 액션과 효과(ThreatNear→0)가 같아서 플래너에 맡기면
            // "싸우라"는 goal이 도망을 고를 수 있다 (M12_T4 목록의 사유와 짝).
            Assert.IsTrue(fight.DirectActionPool != null && fight.DirectActionPool.Length > 0,
                "교전은 플래너를 거치지 않는다 — 도망 액션과 효과가 같아 모호하다");
            Assert.IsTrue(fight.GoalConditions == null || fight.GoalConditions.Length == 0,
                "DirectActionPool goal은 GoalConditions를 비운다 ('항상 미달성' 특례)");
            Assert.IsInstanceOf<FightActionSO>(fight.DirectActionPool[0], "풀의 액션은 교전 액션이어야 한다");

            // W4 DoD ③ — 부상자는 후보에서 빠진다 (교전 **중** 부상이 러너를 끊지 않는 것은
            // 별개다: 그건 재검사 A2이고 러너 쪽 규약이다)
            Assert.IsFalse(fight.AllowedWhenInjured,
                "절뚝이며 늑대에게 달려들면 안 된다 — 부상자는 참전 선발에서 빠진다");
        }

        [Test]
        public void M21_T9_ShippedFightAction_NumbersMatchTheThreatItFights()
        {
            // M17 수치의 짝 — 한쪽만 바꾸면 "맨손 솔로는 아프다"는 검산이 조용히 무너진다.
            var act = AssetDatabase.LoadAssetAtPath<FightActionSO>(
                "Assets/M0Config/Actions/Action_Fight.asset");
            Assert.IsNotNull(act, "Action_Fight 에셋 로드");
            ThreatSO wolf = Tier1Threat();

            Assert.Greater(act.HitDamage, 0f, "피해 0 = 때려도 아무 일이 없다");
            Assert.Greater(act.BaseHitSec, 0f, "간격 0 = 매 프레임 타격");
            Assert.Less(act.StrikeRangeTiles, wolf.StrikeRadiusTiles,
                "주민 사거리가 위협보다 넓으면 위협이 닿기 전에 맞는다 — 파고들어야 '맞으면서 때린다'");

            // 티어1을 도주선까지 몰아붙이는 데 필요한 타격 수 — 유한해야 교전이 성립한다
            float toFlee = wolf.MaxHp - wolf.MaxHp * wolf.FleeBelowHpPct;
            int hits = Mathf.CeilToInt(toFlee / act.HitDamage);
            Assert.Less(hits, 12, $"늑대 격퇴에 {hits}대 필요 — 너무 길면 맨손 교전이 자살이 된다");
            Assert.Greater(hits, 1, "한 대에 격퇴되면 위협이 장식이 된다");

            // 매복 상한 (2026-08-08 개정 — 쫓지 않고 매복한다). 0이면 舊 헛걸음 순환으로 회귀,
            // 재타격 주기(0.25일 = 25초)보다 짧으면 늑대가 돌아오기 전에 포기한다.
            Assert.Greater(act.AmbushGiveUpSec, 25f,
                "매복 상한이 재타격 주기(25초)보다 짧다 — 늑대가 돌아오기 전에 포기해 교전이 영영 안 성립한다");
        }

        // ── M21-T10: 원시 무기 (W5 DoD ①②④) ────────────────────────────────

        [Test]
        public void M21_T10_FightNeedsNoWeapon_WeaponIsAMultiplierNotAGate()
        {
            // ADR-M21-5 — 무기는 게이트가 아니라 배율. Goal_Fight 어디에도 MyHasWeapon이
            // 전제로 끼는 순간 "무기 없는 판 = 아무도 안 싸움"이 되어 축이 뒤집힌다.
            GoalSO fight = LoadGoal("Goal_Fight");
            Assert.IsFalse(fight.TriggerConditions.Any(c => c.Slot == SlotId.MyHasWeapon),
                "교전 트리거에 무기 전제 금지 — 맨손도 싸운다");

            var precs = new System.Collections.Generic.List<SlotCondition>();
            foreach (ActionSO a in fight.DirectActionPool)
            {
                precs.Clear();
                a.CollectPreconditions(precs);
                Assert.IsFalse(precs.Exists(c => c.Slot == SlotId.MyHasWeapon),
                    $"{a.name}: 교전 액션에 무기 전제 금지 (ADR-M21-5)");
            }

            // 배율은 에셋에 있고 (0,1) 구간 — 1이면 무기가 무의미, 0이면 간격 클램프에 먹혀 침묵
            var act = AssetDatabase.LoadAssetAtPath<FightActionSO>(
                "Assets/M0Config/Actions/Action_Fight.asset");
            Assert.Greater(act.WeaponHitSecMult, 0f, "배율 0 = HitInterval이 무시해 무기가 침묵한다");
            Assert.Less(act.WeaponHitSecMult, 1f, "배율 1 이상 = 무기를 만들 이유가 없다");
        }

        [Test]
        public void M21_T10_ArmSelf_TriggersOnlyInPeaceAndOnlyOnce()
        {
            GoalSO arm = LoadGoal("Goal_ArmSelf");

            // 전투 중 제작 금지 — 늑대 앞에서 창을 깎기 시작하면 안 된다
            Assert.IsTrue(arm.TriggerConditions.Any(
                    c => c.Slot == SlotId.ThreatNear && c.Op == CompareOp.Equal && c.Value == 0),
                "트리거에 ThreatNear==0 누락 — 교전 중 제작이 열린다");
            // 트리거(무기 없음)와 목표(무기 있음)가 서로소 — ADR-M0-7 무한 루프 방지
            Assert.IsTrue(arm.TriggerConditions.Any(
                    c => c.Slot == SlotId.MyHasWeapon && c.Op == CompareOp.Equal && c.Value == 0),
                "트리거에 MyHasWeapon==0 누락 — 무장한 뒤에도 계속 뜬다");
            Assert.IsTrue(arm.GoalConditions.Any(
                    c => c.Slot == SlotId.MyHasWeapon && c.Op == CompareOp.Equal && c.Value == 1),
                "목표가 MyHasWeapon==1이 아니면 제작 사슬이 성립하지 않는다");
            // 생존 아래 (P0 승인 목록 비대상 — M12_T3 관할 밖임을 고정)
            Assert.Less(arm.Priority, 90, "무장이 수면·식사보다 급하면 안 된다 (여가 위·생존 아래)");
            Assert.IsFalse(arm.AllowedWhenInjured, "부상자는 제작 노동에서 빠진다 (기존 필터 재사용)");
        }

        [Test]
        public void M21_T10_CraftChain_GatherFeedsTheCraft()
        {
            // W5 DoD ① 기구 증명 — 재료가 있으면 제작 한 방, 빈손이면 채집이 앞에 붙는다.
            // (화면의 "플랜 로그: 채집→제작 사슬"은 Play 실측 몫 — 여기는 플래너 기구만 고정한다.)
            var catalog = AssetDatabase.LoadAssetAtPath<ActionCatalog>(
                "Assets/M0Config/ActionCatalog.asset");
            Assert.IsNotNull(catalog, "카탈로그 로드");
            var gw = new PlannerGateway(catalog);
            GoalSO arm = LoadGoal("Goal_ArmSelf");

            // 재료 충족 — 제작 단독 플랜
            (PlanStatus s1, ActionSO[] p1) = RunPlan(gw, Snap(
                (SlotId.WoodStock, 3), (SlotId.StoneStock, 2)), arm);
            Assert.AreEqual(PlanStatus.Success, s1, "재료가 있는데 무장 플랜이 안 나온다");
            Assert.AreEqual(1, p1.Length, "재료가 있으면 제작 한 방이어야 한다");
            Assert.IsInstanceOf<CraftActionSO>(p1[0], "플랜의 끝은 제작 액션");

            // 빈손 + 발견만 — 채집→제작 사슬 (DoD ①의 플래너 쪽 절반)
            (PlanStatus s2, ActionSO[] p2) = RunPlan(gw, Snap(
                (SlotId.NearDiscoveredWood, 1), (SlotId.NearDiscoveredStone, 1)), arm);
            Assert.AreEqual(PlanStatus.Success, s2, "빈손이면 채집해서라도 만들어야 한다");
            Assert.IsTrue(p2.Any(a => a is CraftActionSO), "사슬 끝에 제작이 없다");
            Assert.Greater(p2.Length, 1, "빈손인데 채집 없이 만들었다 — 재료 전제가 뚫렸다");
        }

        [Test]
        public void M21_T10_ShippedCraft_MaterialsAreGuardedByPreconditions()
        {
            // 원자성의 짝 (ADR-M0-8) — Sub 효과마다 같은 슬롯 ≥ 전제가 있어야
            // "차감 실패로 플랜 중단"이 정상 경로가 아니라 경합 예외로만 남는다.
            var act = AssetDatabase.LoadAssetAtPath<CraftActionSO>(
                "Assets/M0Config/Actions/Action_CraftWeapon.asset");
            Assert.IsNotNull(act, "Action_CraftWeapon 에셋 로드");

            foreach (SlotEffect e in act.Effects)
            {
                if (e.Op != EffectOp.SubClamp0) continue;
                Assert.IsTrue(act.Preconditions.Any(
                        p => p.Slot == e.Slot && p.Op == CompareOp.GreaterOrEqual && p.Value >= e.Value),
                    $"{e.Slot} −{e.Value}에 대응하는 ≥ 전제가 없다 — 지불 불가 플랜이 성립한다");
            }
            Assert.IsTrue(act.Effects.Any(
                    e => e.Slot == SlotId.MyHasWeapon && e.Op == EffectOp.Set && e.Value == 1),
                "Set MyHasWeapon 1 픽션이 없으면 플래너가 이 액션으로 무장을 세울 수 없다");
        }

        // ── M21-T11: 대규모 적습 (W6) ────────────────────────────────────────
        // 🔴 게이트 개정 (2026-08-11 2차 감사): 舊 T11 두 판의 마릿수 산식(SpawnCount) 단언은
        // 산식·에셋 3필드 철거와 함께 삭제 — 관측: M24 압력 예산제(ADR-M24-2)가 마릿수의 유일한
        // 원천이 된 뒤 산식도 필드도 프로덕션 독자 0이라, 그 단언들은 "어떤 판에서도 안 도는
        // 수식"을 지키고 있었다. 살아 있는 도주선 검사만 남긴다.

        [Test]
        public void M21_T11_ShippedThreats_RoutLinesAreCoherent()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:ThreatSO"))
            {
                var so = AssetDatabase.LoadAssetAtPath<ThreatSO>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.Greater(so.RoutBelowPct, 0f,
                    $"{so.name}: 무리 도주선 0 = 전멸해야만 끝난다 (격퇴 축 소멸)");
                Assert.Less(so.RoutBelowPct, 1f,
                    $"{so.name}: 무리 도주선 1 = 한 마리만 빠져도 전원 도주 (무리가 장식이 된다)");
            }
        }

        // ── M21-T12: 래칫 양방향 완충 (W7 DoD ②③④) ─────────────────────────

        private static ThreatSO NewTier(float unlockDay, float periodDays, float warnDays)
        {
            var so = ScriptableObject.CreateInstance<ThreatSO>();
            so.DisplayName = "시험 늑대";
            so.UnlockDay = unlockDay;
            so.PeriodDays = periodDays;
            so.WarnDays = warnDays;
            return so;
        }

        [Test]
        public void M21_T12_ReliefStacks_CapBothAxes()
        {
            var cfg = ScriptableObject.CreateInstance<WorldConfigSO>(); // 기본값 3 · 2일
            var svc = new ThreatService(null, null, null, null, cfg, null, null, null);

            Assert.AreEqual(0, svc.ReliefStacks, "초기 스택 0");
            Assert.AreEqual(0f, svc.DelayDays, 1e-4f, "초기 지연 0");

            for (int i = 0; i < 4; i++) svc.NotifyVillagerDeath(); // 4번째는 상한에 흡수
            Assert.AreEqual(3, svc.ReliefStacks, "스택 상한 3 — 연속 상실이 위협을 0으로 만들 수 없다 (DoD ③)");
            Assert.AreEqual(6f, svc.DelayDays, 1e-4f,
                "지연도 같은 상한에 묶인다 (3×2일) — 스택만 막고 지연이 새면 무한 유예가 된다");

            Object.DestroyImmediate(cfg);
        }

        [Test]
        public void M21_T12_DeathDelay_PushesForecastAndStrike()
        {
            // W7 DoD ② 기구 — 사망 후 다음 발동(과 예고)이 +2일 늦다. 예고 시각으로 검산한다:
            // 예고와 발동은 한 시계(Tick)라 예고가 밀리면 발동도 민다 (Spawn까지 가면 경로탐색이
            // 필요해 순수 검산이 안 된다 — 예고가 관측 가능한 가장 이른 지점).
            // 🔁 M24-1차 W4: 주기의 원천이 **종족 PeriodDays 에서 전역 WavePeriodDays 로** 옮겨졌다
            // (밴드 폐지). 舊 판은 늑대의 6일을 손으로 박아 Day 5/7 을 기대했는데, 그러면 계수를
            // 만질 때마다 이 게이트가 red 가 된다. 지키려던 것은 특정 날짜가 아니라
            // **"사망이 예고를 정확히 ReliefDelayDays 만큼 민다"**이므로 그 관계로 다시 쓴다.
            var cfg = ScriptableObject.CreateInstance<WorldConfigSO>();
            var tier = NewTier(0f, 6f, 1f); // PeriodDays 6 은 이제 휴면 — 주기는 cfg 가 준다
            float warn = tier.WarnDays;
            float t0 = cfg.WavePeriodDays - warn;                 // 평시 예고 시각
            float t1 = cfg.WavePeriodDays + cfg.ThreatReliefDelayDays - warn; // 사망 후 예고 시각

            var plain = new ThreatService(new[] { tier }, null, null, null, cfg, null, null, null);
            plain.Tick(t0 - 0.1f);
            Assert.IsNull(plain.Forecasting, $"기준선: Day {t0 - 0.1f} 는 아직 예고 전");
            plain.Tick(t0);
            Assert.IsNotNull(plain.Forecasting, $"기준선: Day {t0} 예고 (주기 − 예고일)");

            var grieving = new ThreatService(new[] { tier }, null, null, null, cfg, null, null, null);
            grieving.NotifyVillagerDeath(); // +ReliefDelayDays
            grieving.Tick(t0);
            Assert.IsNull(grieving.Forecasting, "사망 후엔 평시 예고 시각에 예고가 없다 — 발동이 밀렸다");
            grieving.Tick(t1 - 0.1f);
            Assert.IsNull(grieving.Forecasting, $"Day {t1 - 0.1f} 도 아직");
            grieving.Tick(t1);
            Assert.IsNotNull(grieving.Forecasting,
                $"Day {t1} 예고 — 정확히 +{cfg.ThreatReliefDelayDays}일 (DoD ②)");

            // HUD 카운트다운도 같은 시계를 읽는다 — 판정과 표시가 갈리면 "1일 후"가 거짓말이 된다
            Assert.AreEqual(warn, grieving.DaysToStrike(t1), 1e-3f, "예고 시점 잔여 = WarnDays");

            Object.DestroyImmediate(tier);
            Object.DestroyImmediate(cfg);
        }

        [Test]
        public void M21_T12_ShippedConfig_ReliefIsOn()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<WorldConfigSO>("Assets/M0Config/WorldConfig.asset");
            Assert.IsNotNull(cfg, "WorldConfig 로드");
            Assert.GreaterOrEqual(cfg.ThreatReliefStackMax, 1,
                "완충 상한 0 = W7 축 전체가 꺼진다 (끄려면 명세 개정이 먼저)");
            Assert.Greater(cfg.ThreatReliefDelayDays, 0f,
                "사망 지연 0 = 상실이 리듬을 만들지 못한다 (자비 축 소멸)");
        }

        // ── M21-T13: 사냥꾼·정찰 (W8 DoD ①②④) ──────────────────────────────

        [Test]
        public void M21_T13_ScoutBonus_IsMaxNotSum()
        {
            var scout = ScriptableObject.CreateInstance<JobSO>();
            scout.WarnBonusDays = 1f;
            var scout2 = ScriptableObject.CreateInstance<JobSO>();
            scout2.WarnBonusDays = 1f;
            var farmer = ScriptableObject.CreateInstance<JobSO>();

            Assert.AreEqual(0f, ThreatService.MaxWarnBonus(null), 1e-4f, "미배선 = 중립");
            Assert.AreEqual(0f, ThreatService.MaxWarnBonus(new[] { farmer }), 1e-4f,
                "정찰 없는 판 = 연장 0 (기존 예고 그대로)");
            Assert.AreEqual(1f, ThreatService.MaxWarnBonus(new[] { farmer, scout }), 1e-4f,
                "정찰 1명 = +1일");
            // ⚠️① 인원 비례 금지 — 정찰꾼 몰빵이 예고를 무한정 늘리면 안 된다
            Assert.AreEqual(1f, ThreatService.MaxWarnBonus(new[] { scout, scout2 }), 1e-4f,
                "정찰 2명도 +1일 — 최댓값이지 합산이 아니다 (1명이면 족하다)");

            Object.DestroyImmediate(scout);
            Object.DestroyImmediate(scout2);
            Object.DestroyImmediate(farmer);
        }

        [Test]
        public void M21_T13_ShippedJobs_HunterFightsOthersNeutral()
        {
            var hunter = AssetDatabase.LoadAssetAtPath<JobSO>("Assets/M0Config/Jobs/Job_Hunter.asset");
            Assert.IsNotNull(hunter, "Job_Hunter 에셋 로드");
            Assert.Greater(hunter.CombatDurationMult, 0f, "배율 0 = HitInterval이 무시해 사냥꾼이 침묵");
            Assert.Less(hunter.CombatDurationMult, 1f, "1 이상이면 사냥꾼의 부재 시나리오가 사라진다");
            GoalSO fight = LoadGoal("Goal_Fight");
            Assert.Greater(hunter.BoostFor(fight), 0, "사냥꾼은 교전을 선점해야 한다 (GoalBoost)");

            var explorer = AssetDatabase.LoadAssetAtPath<JobSO>("Assets/M0Config/Jobs/Job_Explorer.asset");
            Assert.IsNotNull(explorer, "Job_Explorer 에셋 로드");
            // (M27 개정: 舊 하한 1일 = 하루 100초 시절의 실시간 100초 — 일수 리터럴은 눈금이
            //  바뀌면 뜻이 바뀐다. 실시간 하한으로 잰다: 현 배포 0.2일 × 600초 = 120초. ADR-M27-1)
            var worldCfg = AssetDatabase.LoadAssetAtPath<WorldConfigSO>("Assets/M0Config/WorldConfig.asset");
            Assert.IsNotNull(worldCfg, "WorldConfig 로드");
            Assert.GreaterOrEqual(explorer.WarnBonusDays / worldCfg.GameTimeScale, 60f,
                "정찰 연장이 실시간 1분 미만 = 재탄생이 껍데기다 (부재 시나리오 소멸)");

            // DoD ④ 중립 불변식 — 사냥꾼 외 전 직업의 전투 간격·비사냥 직업의 정찰 연장 불변
            foreach (string guid in AssetDatabase.FindAssets("t:JobSO"))
            {
                var job = AssetDatabase.LoadAssetAtPath<JobSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (job != hunter)
                    Assert.AreEqual(1f, job.CombatDurationMult, 1e-4f,
                        $"{job.name}: 전투 배율이 1이 아니다 — 사냥꾼만의 축이 흐려진다 (중립 불변식)");
                if (job != explorer)
                    Assert.AreEqual(0f, job.WarnBonusDays, 1e-4f,
                        $"{job.name}: 정찰 연장은 탐험가(정찰)만의 축이다");
            }
        }

        // ── M21-T14: 보상·연대기·신기록 (W9 DoD ①③④) ───────────────────────

        [Test]
        public void M21_T14_NewChronicleEvents_SpeakPlayerLanguage()
        {
            // 침묵 금지 — 새 사건이 enum 이름 그대로 나오면 기록이 있어도 이야기가 안 된다
            string repel = SeasonHud.KrEvent(new ChronicleEvent
            {
                Kind = EventId.Repelled, OtherId = "늑대 무리", Value = 3,
            });
            Assert.AreEqual("늑대 무리 3마리 격퇴", repel, "ADR-M21-1 검증 문장의 재료");
            Assert.AreEqual("외로운 늑대 격퇴", SeasonHud.KrEvent(new ChronicleEvent
            {
                Kind = EventId.Repelled, OtherId = "외로운 늑대", Value = 1,
            }), "단독 위협은 마릿수 생략");
            Assert.AreEqual("큰 곰 사냥(고기 5)", SeasonHud.KrEvent(new ChronicleEvent
            {
                Kind = EventId.Hunted, OtherId = "큰 곰", Value = 5,
            }));
        }

        [Test]
        public void M21_T14_RunRecord_OldFileDeserializesWithZeroRepels()
        {
            // W9 DoD ③ — 기존 파일(BestRepels 없음) 역직렬화 호환. 필드 부재 = 0.
            var old = JsonUtility.FromJson<RunRecordStore.RunRecord>(
                "{\"Version\":1,\"BestWinters\":2,\"BestDay\":30,\"BestPeakPop\":9}");
            Assert.IsNotNull(old, "옛 스키마 역직렬화");
            Assert.AreEqual(2, old.BestWinters, "기존 필드 보존");
            Assert.AreEqual(0, old.BestRepels, "새 필드 부재 = 0 (append-only, ADR-M14-3)");
        }

        [Test]
        public void M21_T14_GameOver_ShowsRepelLineOnlyWhenPresent()
        {
            var roster = new System.Collections.Generic.List<VillagerRecord>
            {
                new VillagerRecord { ShortName = "A", PersonalityName = "순둥이", JobName = "사냥꾼",
                                     BornDay = 0f, LeftDay = 12f, Cause = ExitCause.Combat },
            };
            var best = new RunRecordStore.RunRecord
            {
                BestWinters = 1, BestDay = 20, BestPeakPop = 8, BestRepels = 4,
            };
            string s = SeasonHud.ComposeGameOver(12, 0, roster, 1, 8, best, newRecord: false, repels: 3);
            StringAssert.Contains("격퇴 3회", s, "이번 판 격퇴 줄 (W9 — ComposeGameOver)");
            StringAssert.Contains("격퇴 4회", s, "역대 최다 격퇴 줄");
            StringAssert.Contains("짐승에게 물려 죽음", s, "전멸 명부가 전투 사망을 말한다 (DoD ②)");

            string none = SeasonHud.ComposeGameOver(12, 0, roster, 1, 8,
                new RunRecordStore.RunRecord { BestWinters = 1, BestDay = 20 }, false, repels: 0);
            StringAssert.DoesNotContain("격퇴", none, "격퇴 0회면 줄 생략 (이탈 0 감춤과 같은 규율)");
        }

        // ── M21-T15: MyWasStarved 원인 라우팅 (W10 — 게이트 본대의 마지막 사각지대) ──

        [Test]
        public void M21_T15_StarvedMemory_OnlyFromStarvationNearDeath()
        {
            // M12-G 희소성 — "굶어 죽을 뻔한 기억"은 굶주림 + 죽음 문턱 근처에서만.
            var c = NewCfg(); // MaxHp 100 · NearStarvationRatio 0.8 → 문턱 ≈ 체력 20
            // ⚠️ 정확히 20f는 안 쓴다 — (1 − 0.8f)의 이진 오차로 문턱이 19.9999…라 경계값이
            // 뒤집힌다 (T8 HP_EPSILON과 같은 부류). 경계 정밀도는 T2가 특성화했고,
            // 이 게이트의 질문은 **원인 라우팅**이다.
            Assert.IsTrue(VillagerAgent.ShouldMarkStarved(DamageCause.Starvation, 19.9f, c),
                "굶주림으로 문턱 아래에 닿으면 찍힌다");
            Assert.IsTrue(VillagerAgent.ShouldMarkStarved(DamageCause.Starvation, 5f, c),
                "더 깊어도 찍힌다");
            Assert.IsFalse(VillagerAgent.ShouldMarkStarved(DamageCause.Starvation, 20.1f, c),
                "문턱 위는 아직 아니다 — 선이 앞당겨지면 전원이 기록돼 희소성이 사라진다");

            // 🔴 이 줄이 이 게이트의 존재 이유다: 전투 피해로 체력이 바닥나도 '굶은 기억'은 아니다.
            // 여기가 뚫리면 늑대에게 물릴 때마다 경험 우회(집 부탁 성향 문턱)가 발동한다.
            Assert.IsFalse(VillagerAgent.ShouldMarkStarved(DamageCause.Combat, 5f, c),
                "전투 피해는 아무리 아슬아슬해도 굶은 기억이 아니다 (M12-G 희소성)");

            Object.DestroyImmediate(c);
        }

        // LoadGoal·Snap·RunPlan → GateHelpers (2026-08-11 2차 감사 통합)
    }
}
