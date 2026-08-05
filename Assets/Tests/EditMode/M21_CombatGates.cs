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
        public void M21_T1_InjuryEntryDamage_LandsExactlyOnInjuredLine()
        {
            var c = NewCfg();
            // 부상 진입 피해 = MaxHp − InjuredBelowHp. 이 등식이 깨지면 "다쳤다"와 체력이
            // 어긋나 정보줄이 거짓말을 한다 (W2에서 위협 StrikeDamage가 이 자리를 받는다).
            float entry = c.MaxHp - c.InjuredBelowHp;
            float after = c.MaxHp - entry;
            Assert.AreEqual(c.InjuredBelowHp, after, 1e-4f, "진입 피해는 부상선에 정확히 닿는다");
            Assert.IsFalse(VillagerAgent.IsDead(after), "부상은 즉사가 아니다");
            Assert.IsFalse(VillagerAgent.IsNearDeath(after, c), "부상 진입만으로 '죽을 뻔'은 아니다");

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
    }
}
