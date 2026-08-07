using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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
        public void M21_T7_ShippedSeasons_OnlyWinterFeedsTheTrap()
        {
            // 배포 에셋 대조 — 순수 함수가 옳아도 에셋이 바뀌면 뜻이 달라진다.
            // 특히 "IsCrisis인데 ForageFrozen이 아닌" 계절이 실재함을 고정한다 (그 존재가 함정의 근거).
            string[] guids = AssetDatabase.FindAssets("t:SeasonSO");
            Assert.Greater(guids.Length, 0, "배포 계절 에셋이 하나도 없다");

            int hungry = 0, crisisButNotFrozen = 0;
            foreach (string guid in guids)
            {
                var s = AssetDatabase.LoadAssetAtPath<SeasonSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (ThreatService.IsPredatorHungry(s)) hungry++;
                if (s.IsCrisis && !s.ForageFrozen) crisisButNotFrozen++;
            }
            Assert.AreEqual(1, hungry, "배고픈 계절은 정확히 하나(겨울)여야 한다");
            Assert.Greater(crisisButNotFrozen, 0,
                "IsCrisis인데 봉쇄가 아닌 계절이 사라졌다 — IsCrisis로 갈아타도 게이트가 안 잡게 된다");
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

            // 인자 없는 판 = 에셋 기저 확률판 (기존 호출자·M10 게이트 호환)
            for (int ord = 1; ord <= 50; ord++)
                Assert.AreEqual(ThreatService.RollTargetsVillagers(so, ord),
                                ThreatService.RollTargetsVillagers(so, ord, so.VillagerTargetChance),
                                $"서수 {ord}: 2인자판 = 기저 확률 3인자판");

            // 확률을 올리면 주민 타깃이 늘어난다 (단조) — 겨울 보정이 실제로 기울이는가
            int plain = 0, winter = 0;
            for (int ord = 1; ord <= 200; ord++)
            {
                if (ThreatService.RollTargetsVillagers(so, ord, 0.25f)) plain++;
                if (ThreatService.RollTargetsVillagers(so, ord, 0.5f)) winter++;
            }
            Assert.Greater(winter, plain, "겨울 확률이 높은데 주민 타깃이 늘지 않았다");
        }

        [Test]
        public void M21_T7_Tier1Wolf_TargetsVillagersInPlainSeasons()
        {
            // W2R DoD ④ — Tier1이 **평시에도** 주민을 노리는 출몰이 존재해야 한다.
            // M10 시절 VillagerTargetChance:0("밭 전용")의 개정이고, 0으로 되돌아가면 여기서 잡힌다.
            var wolf = AssetDatabase.LoadAssetAtPath<ThreatSO>(
                "Assets/M0Config/Threats/Threat_Tier1_Wolf.asset");
            Assert.IsNotNull(wolf, "Tier1 늑대 에셋 로드");
            Assert.Greater(wolf.VillagerTargetChance, 0f,
                "늑대가 다시 밭 전용이 됐다 — 평시 주민 타깃이 영영 안 온다");

            int villagerRolls = 0;
            for (int ord = 1; ord <= 100; ord++)
                if (ThreatService.RollTargetsVillagers(wolf, ord)) villagerRolls++;
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
    }
}
