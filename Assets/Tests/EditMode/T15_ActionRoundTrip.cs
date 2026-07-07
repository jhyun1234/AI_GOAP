using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using AIVillage.AI;
using AIVillage.Core.GOAP;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// T15: 액션 하나가 4곳(BuildActionDefs 등록 + _actionNames[] 배열 + Debug.Assert 개수 +
    ///      BuildGoalState/effect 슬롯 인식) 모두에 존재하는지 검증한다.
    /// 하나라도 빠지면 NoSolutionFound → Deadlock (이슈 #2 재발 방지 게이트).
    ///
    /// 근본 진단: 액션 추가 시 ActionDef만 추가하고 names[] 갱신을 잊거나, Goal 매핑이
    /// 없는 액션이 남아 있으면 GOAP 플래너가 해당 액션을 활용할 수 없다.
    /// 이 테스트는 액션 추가 3종 세트(ADR-8) 위반을 CI 이전에 강제한다.
    ///
    /// ADR-T5: 리플렉션 사용 금지. GOAPActionRegistry의 _actionNames 배열은
    /// InternalsVisibleTo로 GetActionNamesForTest()를 통해 노출된다.
    /// </summary>
    public class T15_ActionRoundTrip
    {
        // Registry BaseCost 계산에 필요한 컨텍스트. 모든 배율 Identity → 순수 등록 검증만 수행.
        private const float SEASON_MOD = 1.0f;
        private static readonly ContextCostMultipliers CTX_IDENTITY = ContextCostMultipliers.Identity;

        // 역할별 액션 개수는 동일하다(비용만 배율 적용). 대표 역할 6종을 반복 실행한다.
        private static readonly AgentRole[] ROLES = new[]
        {
            AgentRole.None,
            AgentRole.Lumberjack,
            AgentRole.Miner,
            AgentRole.Builder,
            AgentRole.Warrior,
            AgentRole.Cook,
            AgentRole.Medic
        };

        // BuildGoalState가 인식하는 goalId 목록. 알 수 없는 goalId는 경고와 빈 마스크를 반환한다.
        // 이 목록은 GOAPPlanningSlots.BuildGoalState의 switch case와 동기화 유지.
        private static readonly string[] KNOWN_GOAL_IDS = new[]
        {
            "SurviveHunger", "SurviveInjury", "SurviveFatigue",
            "GatherResources", "GatherWood", "GatherStone", "GatherIron", "GatherCopper", "GatherFood",
            "BuildStructure", "DefendVillage", "AttackEnemy",
            "Explore", "CookMeal", "MoveToBase", "MoveToTarget"
        };

        // Goal에 직접 매핑되지 않는 헬퍼/도구 액션. 해당 액션의 Effect 슬롯이
        // 어느 Goal 마스크에도 걸리지 않아도 예외로 인정한다.
        // - CraftPrimitiveWeapon: AttackEnemy의 Prec(HasPrimitiveWeapon)만 충족시키는 도구 액션.
        // - CraftWeapon: HasWeapon 슬롯은 현재 어떤 Goal도 요구하지 않는 상위 티어 도구.
        private static readonly HashSet<string> EXEMPT_ACTIONS = new HashSet<string>
        {
            "CraftPrimitiveWeapon",
            "CraftWeapon"
        };

        // ──────────────────────────────────────────────────────────────────
        // T15.1: BuildActionDefs 등록 개수 == _actionNames 배열 길이
        // ──────────────────────────────────────────────────────────────────

        [Test]
        public void ActionDefs_count_matches_names_array_length()
        {
            string[] names = GOAPActionRegistry.GetActionNamesForTest();

            foreach (var role in ROLES)
            {
                using var defs = GOAPActionRegistry.BuildActionDefs(
                    role, Allocator.Temp, SEASON_MOD, CTX_IDENTITY);

                Assert.AreEqual(
                    names.Length, defs.Length,
                    $"[T15.1 role={role}] Registry ActionDef 개수({defs.Length})와 " +
                    $"_actionNames[] 배열 길이({names.Length})가 다릅니다. " +
                    "액션 추가 3종 세트(ADR-8) 위반 — ActionDef 또는 _actionNames 하나를 " +
                    "누락한 커밋입니다. BuildActionDefs()의 defs[i++] 라인 수와 " +
                    "_actionNames 항목 수를 대조하세요.");
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // T15.2: BuildActionDefs의 Debug.Assert 상수(22)와 실제 배열 크기 일치
        //   → BuildActionDefs 반환 배열이 상수 22로 할당됐고 예외 없이 완료됐다는
        //      사실 자체가 i == 22를 만족했다는 증거다. 여기서는 명시적으로 22를 확인해
        //      Registry 상수 변경 시 즉시 실패로 알린다.
        // ──────────────────────────────────────────────────────────────────

        [Test]
        public void ActionDefs_count_equals_expected_constant()
        {
            const int EXPECTED_COUNT = 22;
            using var defs = GOAPActionRegistry.BuildActionDefs(
                AgentRole.None, Allocator.Temp, SEASON_MOD, CTX_IDENTITY);

            Assert.AreEqual(
                EXPECTED_COUNT, defs.Length,
                $"[T15.2] BuildActionDefs.Length({defs.Length}) != 예상 상수({EXPECTED_COUNT}). " +
                "Debug.Assert(i == 22) 상수와 NativeArray 할당 크기를 함께 갱신하세요.");

            Assert.AreEqual(
                EXPECTED_COUNT, GOAPActionRegistry.GetActionNamesForTest().Length,
                $"[T15.2] _actionNames.Length != 예상 상수({EXPECTED_COUNT}). " +
                "Debug.Assert 상수 갱신 시 _actionNames 배열도 함께 갱신하세요.");
        }

        // ──────────────────────────────────────────────────────────────────
        // T15.3: 모든 Def의 해시가 HashToActionId로 역매핑된다 (Unknown_ 접두어 금지)
        // ──────────────────────────────────────────────────────────────────

        [Test]
        public void All_action_hashes_map_back_to_names()
        {
            foreach (var role in ROLES)
            {
                using var defs = GOAPActionRegistry.BuildActionDefs(
                    role, Allocator.Temp, SEASON_MOD, CTX_IDENTITY);

                for (int i = 0; i < defs.Length; i++)
                {
                    int hash = defs[i].ActionStringHash;
                    string id = GOAPActionRegistry.HashToActionId(hash);

                    Assert.IsFalse(
                        string.IsNullOrEmpty(id),
                        $"[T15.3 role={role}] ActionDef[{i}] 해시({hash})가 빈 문자열로 역매핑됐습니다.");

                    Assert.IsFalse(
                        id.StartsWith("Unknown_"),
                        $"[T15.3 role={role}] ActionDef[{i}] 해시({hash})가 _actionNames에 존재하지 " +
                        $"않습니다 (HashToActionId → '{id}'). BuildActionDefs에는 등록됐지만 " +
                        "_actionNames 배열에서 액션 문자열을 누락한 커밋입니다.");
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // T15.4: 모든 _actionNames가 BuildActionDefs 등록 결과 해시 집합에 존재한다
        //   → names에는 있는데 실제 def가 없는 경우 (반대 방향 정합성)
        // ──────────────────────────────────────────────────────────────────

        [Test]
        public void All_names_appear_in_ActionDefs()
        {
            string[] names = GOAPActionRegistry.GetActionNamesForTest();
            using var defs = GOAPActionRegistry.BuildActionDefs(
                AgentRole.None, Allocator.Temp, SEASON_MOD, CTX_IDENTITY);

            var defHashes = new HashSet<int>();
            for (int i = 0; i < defs.Length; i++)
                defHashes.Add(defs[i].ActionStringHash);

            foreach (var name in names)
            {
                int hash = UnityEngine.Animator.StringToHash(name);
                Assert.IsTrue(
                    defHashes.Contains(hash),
                    $"[T15.4] _actionNames에는 '{name}'이 있지만 BuildActionDefs 결과에는 " +
                    "해당 해시가 없습니다. _actionNames 항목을 추가만 하고 " +
                    "BuildActionDefs의 defs[i++] 라인을 누락한 커밋입니다.");
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // T15.5: 각 액션의 Effect 슬롯 중 최소 하나가 어느 Goal의 mask 슬롯이거나,
        //        예외 목록(EXEMPT_ACTIONS)에 속한다.
        //   → BuildGoalState가 인식하지 못하는 액션(=플래너 목표로 이어질 수 없는 액션)이
        //      실수로 남아있는지 강제로 검증한다.
        //   → useNumericGoals true/false 양쪽에서 마스크 슬롯을 수집해 안전한 상한을 만든다.
        // ──────────────────────────────────────────────────────────────────

        [Test]
        public void Every_action_has_effect_matching_some_goal_mask_or_is_exempt()
        {
            // 1) 알려진 goalId에 대해 goalMask에 등장하는 슬롯 인덱스 집합을 수집.
            var goalSlots = CollectGoalMaskSlots();

            // 2) 각 Def의 Effect 슬롯을 뽑아 goalSlots와 교집합이 있는지 확인.
            using var defs = GOAPActionRegistry.BuildActionDefs(
                AgentRole.None, Allocator.Temp, SEASON_MOD, CTX_IDENTITY);

            for (int i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                string id = GOAPActionRegistry.HashToActionId(def.ActionStringHash);

                if (EXEMPT_ACTIONS.Contains(id))
                    continue;

                var effectSlots = CollectEffectSlots(def);
                bool intersects = false;
                foreach (int s in effectSlots)
                {
                    if (goalSlots.Contains(s))
                    {
                        intersects = true;
                        break;
                    }
                }

                Assert.IsTrue(
                    intersects,
                    $"[T15.5] 액션 '{id}'의 Effect 슬롯 [{string.Join(",", effectSlots)}]가 " +
                    $"어느 Goal의 mask 슬롯에도 걸리지 않습니다. " +
                    "이 액션은 GOAP 플래너의 목표 역추론에서 절대 선택되지 않습니다. " +
                    "① BuildGoalState에 대응 goalId를 추가하거나, " +
                    "② EXEMPT_ACTIONS(도구/헬퍼)에 등재하거나, " +
                    "③ 액션 자체를 제거하세요.");
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // 헬퍼: BuildGoalState가 알려진 goalId에 대해 사용하는 mask 슬롯 인덱스 집합
        // ──────────────────────────────────────────────────────────────────
        private static HashSet<int> CollectGoalMaskSlots()
        {
            var slots = new HashSet<int>();
            foreach (var gid in KNOWN_GOAL_IDS)
            {
                // useNumericGoals 두 경로 모두에서 마스크를 수집한다.
                CollectMaskInto(gid, useNumericGoals: false, slots);
                CollectMaskInto(gid, useNumericGoals: true,  slots);
            }
            return slots;
        }

        private static void CollectMaskInto(string goalId, bool useNumericGoals, HashSet<int> sink)
        {
            GOAPStateUtil.BuildGoalState(
                goalId,
                out NativeArray<int> state,
                out NativeArray<int> mask,
                out NativeArray<int> ops,
                useNumericGoals,
                Allocator.Temp);
            try
            {
                for (int s = 0; s < mask.Length; s++)
                    if (mask[s] == 1) sink.Add(s);
            }
            finally
            {
                state.Dispose();
                mask.Dispose();
                ops.Dispose();
            }
        }

        private static List<int> CollectEffectSlots(GOAPActionDef def)
        {
            var slots = new List<int>(def.EffectCount);
            for (int e = 0; e < def.EffectCount; e++)
            {
                switch (e)
                {
                    case 0: slots.Add(def.Eff0S); break;
                    case 1: slots.Add(def.Eff1S); break;
                    case 2: slots.Add(def.Eff2S); break;
                    case 3: slots.Add(def.Eff3S); break;
                    case 4: slots.Add(def.Eff4S); break;
                    case 5: slots.Add(def.Eff5S); break;
                    case 6: slots.Add(def.Eff6S); break;
                    case 7: slots.Add(def.Eff7S); break;
                }
            }
            return slots;
        }
    }
}
