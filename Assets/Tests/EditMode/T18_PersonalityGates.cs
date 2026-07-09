using NUnit.Framework;
using Unity.Collections;
using AIVillage.AI;
using AIVillage.Core.GOAP;

namespace AIVillage.Tests
{
    /// <summary>
    /// [T18] F-A 성격 배율 회귀 게이트 (ADR-P1 · ADR-P4 방어).
    /// 근거: Docs/F-A_성격특성_명세서.md §4 FA-7, Docs/CLAUDE.md ADR-11.
    ///
    /// - Case1 (ADR-P4): Glutton 성격이면 HungerLevel 71에서 SurviveHunger가 활성화.
    ///                   비-Glutton은 여전히 81+ 이상에서 활성화.
    /// - Case2 (ADR-P1): PersonalityCostMultipliers.From() 반환 값이 항상
    ///                   [PersonalityData.MULT_MIN, MULT_MAX] = [0.5, 2.0] 클램프.
    /// - Case3 (ADR-P1): Coward × 위험 컨텍스트(AttackEnemy 2.5배) × BuildActionDefs
    ///                   조합에서 AttackEnemy BaseCost가 안전 상한 이내로 유지.
    ///                   MULT_MAX 상한이 유효하다는 정적 증거 — 이 값이 폭주하면
    ///                   admissible 휴리스틱이 A*를 안내하지 못하고 MAX_NODES 4096 소진.
    ///
    /// 회귀 시: 방향 ③(문맥배율 무해 봉합)이 성격 축으로 재발.
    /// </summary>
    public class T18_PersonalityGates
    {
        // ─────────────────────────────────────────────────────────────
        // Case1: Glutton P0 임계값 -10 정상 동작
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void Case1_Glutton_HungerThreshold_Down10()
        {
            // Glutton, HungerLevel=71 → 임계값 70 초과 → P0 활성.
            var glutton = new VillagerBrain { Personality = Personality.Glutton, HungerLevel = 71f };
            Assert.IsTrue(glutton.HasActiveP0Condition(),
                "Glutton은 HungerLevel 71에서 P0가 활성화되어야 한다 (임계값 80-10=70 초과).");
            Assert.AreEqual("SurviveHunger", glutton.GetHighestPriorityGoalId(),
                "Glutton HungerLevel 71에서 최우선 goal은 SurviveHunger.");

            // 비-Glutton, HungerLevel=71 → 임계값 80 이하 → P0 비활성 (다른 조건 모두 정상).
            var nonGlutton = new VillagerBrain { Personality = Personality.Diligent, HungerLevel = 71f };
            Assert.IsFalse(nonGlutton.HasActiveP0Condition(),
                "비-Glutton은 HungerLevel 71에서 P0가 비활성이어야 한다 (임계값 80 이하).");

            // Glutton이라도 HungerLevel=69이면 임계값 70 이하 → 비활성.
            var gluttonSafe = new VillagerBrain { Personality = Personality.Glutton, HungerLevel = 69f };
            Assert.IsFalse(gluttonSafe.HasActiveP0Condition(),
                "Glutton HungerLevel 69는 임계값 70 이하 → 비활성.");
        }

        // ─────────────────────────────────────────────────────────────
        // Case2: 모든 성격의 배율이 [MULT_MIN, MULT_MAX] 클램프 안
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void Case2_All_Personalities_Mults_Within_Clamp()
        {
            foreach (Personality p in System.Enum.GetValues(typeof(Personality)))
            {
                var m = PersonalityCostMultipliers.From(p);
                AssertClamped(p, "ChopWood",       m.ChopWood);
                AssertClamped(p, "MineStone",      m.MineStone);
                AssertClamped(p, "MineIron",       m.MineIron);
                AssertClamped(p, "MineCopper",     m.MineCopper);
                AssertClamped(p, "HarvestBerries", m.HarvestBerries);
                AssertClamped(p, "Explore",        m.Explore);
                AssertClamped(p, "AttackEnemy",    m.AttackEnemy);
                AssertClamped(p, "RestOnGround",   m.RestOnGround);
            }

            // Lazy는 채집 5종을 상한 근처로 밀어야 정상. 값이 뒤바뀌면(예: 0.85 실수 입력) 여기서 실패.
            var lazy = PersonalityCostMultipliers.From(Personality.Lazy);
            Assert.Greater(lazy.ChopWood, 1.0f,
                "Lazy는 ChopWood 배율이 1.0보다 커야 한다(기피 성향).");
        }

        private static void AssertClamped(Personality p, string field, float value)
        {
            Assert.GreaterOrEqual(value, PersonalityData.MULT_MIN,
                $"Personality={p} 필드={field} 배율({value})이 하한 {PersonalityData.MULT_MIN} 미만. ADR-P1 위반.");
            Assert.LessOrEqual(value, PersonalityData.MULT_MAX,
                $"Personality={p} 필드={field} 배율({value})이 상한 {PersonalityData.MULT_MAX} 초과. ADR-P1 위반.");
        }

        // ─────────────────────────────────────────────────────────────
        // Case3: Coward × 위험 컨텍스트 합성 시 AttackEnemy BaseCost 안전 상한
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void Case3_Coward_Danger_Attack_Combined_Cost_Within_SafeBound()
        {
            // 위험 컨텍스트(공격 액션에 2.5배). 실제 상수 DANGER_GATHER_MULT=2.5f를 T16과 동등하게 시뮬레이션.
            const float DANGER_ATTACK_MULT = 2.5f;
            var dangerCtx = ContextCostMultipliers.Identity;
            dangerCtx.AttackEnemy = DANGER_ATTACK_MULT;

            var cowardMult = PersonalityCostMultipliers.From(Personality.Coward);

            // 전 역할에 대해 BuildActionDefs 결과의 AttackEnemy BaseCost가 안전 상한을 넘지 않는지 검사.
            // 상한 근거: 최대 (BaseCost=10) × (Cook 배율 1.8) × (Danger 2.5) × (Personality 2.0) = 90.
            // 100 이하이면 admissible 휴리스틱 파괴 우려 없음.
            const float SAFE_UPPER_BOUND = 100f;
            int attackHash = UnityEngine.Animator.StringToHash("AttackEnemy");

            foreach (AgentRole role in System.Enum.GetValues(typeof(AgentRole)))
            {
                using (var defs = GOAPActionRegistry.BuildActionDefs(
                    role, Allocator.Temp, 1.0f, dangerCtx, cowardMult))
                {
                    for (int i = 0; i < defs.Length; i++)
                    {
                        if (defs[i].ActionStringHash != attackHash) continue;

                        float cost = defs[i].BaseCost;
                        Assert.Greater(cost, 0f,
                            $"Personality=Coward, Role={role}: AttackEnemy BaseCost가 0 이하({cost}). " +
                            "곱셈 폭발 방지 default 치환 실패.");
                        Assert.Less(cost, SAFE_UPPER_BOUND,
                            $"Personality=Coward, Role={role}: AttackEnemy BaseCost={cost} > {SAFE_UPPER_BOUND}. " +
                            "성격×위험 배율 합성이 안전 상한을 초과 — ADR-P1 클램프 재검토.");
                    }
                }
            }
        }
    }
}
