using AIVillage.Core.GOAP;
using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M4 성격 게이트 (명세 M4-A~F). 이 파일이 M4-T1~T3의 집이다 —
    /// M4-E(랜덤 목표 Walkable 필터)를 시작으로 A(스키마)·B(중립 불변식·선호 플랜)·
    /// C(거부 오프셋) 게이트가 뒤에 추가된다.
    /// </summary>
    public class M4_PersonalityGates
    {
        [Test]
        public void M4_A_PersonalityAssets_LoadAndPolicy()
        {
            // 신규 인스턴스의 기본값 = 중립 (ADR-M4-2 불변식의 데이터 절반)
            var fresh = ScriptableObject.CreateInstance<PersonalitySO>();
            Assert.AreEqual(0f, fresh.RefuseSatietyOffset);
            Assert.AreEqual(0f, fresh.RefuseFatigueOffset);
            Assert.AreEqual(1f, fresh.GatherCostMult);
            Assert.AreEqual(1f, fresh.FarmCostMult);
            Assert.AreEqual(1f, fresh.BuildCostMult);
            Assert.AreEqual(1f, fresh.ExploreCostMult);

            // 아키타입 4종 로드 + 축 배치 검증 (§4 제안치)
            var docile   = AssetDatabase.LoadAssetAtPath<PersonalitySO>("Assets/M0Config/Personalities/Personality_Docile.asset");
            var stubborn = AssetDatabase.LoadAssetAtPath<PersonalitySO>("Assets/M0Config/Personalities/Personality_Stubborn.asset");
            var farmer   = AssetDatabase.LoadAssetAtPath<PersonalitySO>("Assets/M0Config/Personalities/Personality_Farmer.asset");
            var wanderer = AssetDatabase.LoadAssetAtPath<PersonalitySO>("Assets/M0Config/Personalities/Personality_Wanderer.asset");
            Assert.IsNotNull(docile); Assert.IsNotNull(stubborn); Assert.IsNotNull(farmer); Assert.IsNotNull(wanderer);

            Assert.Less(docile.RefuseSatietyOffset, 0f, "순둥이는 덜 거부 (문턱 하향)");
            Assert.Greater(stubborn.RefuseSatietyOffset, 0f, "고집쟁이는 더 거부 (문턱 상향)");
            Assert.Less(farmer.FarmCostMult, 1f, "농사꾼은 밭 선호");
            Assert.Less(wanderer.GatherCostMult, 1f, "떠돌이는 채집 선호");
        }

        private static (PlanStatus status, ActionSO[] plan) RunPlan(WorldSnapshot snap, GoalSO goal, float[] costMult)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ActionCatalog>("Assets/M0Config/ActionCatalog.asset");
            var gw = new PlannerGateway(catalog);
            PlannerGateway.PendingPlan pending = gw.RequestPlan(snap, goal, costMult);
            gw.CompleteNow(pending);
            Assert.IsTrue(gw.TryGetResult(pending, out PlanStatus status, out ActionSO[] plan, out _));
            return (status, plan);
        }

        private static WorldSnapshot Snap(params (SlotId slot, int value)[] pairs)
        {
            var slots = new int[PlanningConfig.TotalSlots];
            foreach ((SlotId slot, int value) in pairs) slots[(int)slot] = value;
            return new WorldSnapshot(slots);
        }

        [Test]
        public void M4_T1_NeutralInvariant_PlansIdentical()
        {
            // ADR-M4-2: costMult null과 전부 1.0이 기존(M0-T2) 기대 플랜과 완전 동일해야 한다
            var goal = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_P0_Hunger.asset");
            WorldSnapshot snap = Snap((SlotId.MySatiety, 10), (SlotId.RawFoodStock, 5));

            (PlanStatus s0, ActionSO[] p0) = RunPlan(snap, goal, null);
            var catalog = AssetDatabase.LoadAssetAtPath<ActionCatalog>("Assets/M0Config/ActionCatalog.asset");
            var ones = new float[catalog.Actions.Length];
            for (int i = 0; i < ones.Length; i++) ones[i] = 1f;
            (PlanStatus s1, ActionSO[] p1) = RunPlan(snap, goal, ones);

            Assert.AreEqual(PlanStatus.Success, s0);
            Assert.AreEqual(s0, s1);
            Assert.AreEqual(p0.Length, p1.Length, "중립 배열 = null과 동일 플랜 길이");
            for (int i = 0; i < p0.Length; i++)
                Assert.AreSame(p0[i], p1[i], $"중립 불변식 위반 — {i}번째 액션이 다름");
        }

        [Test]
        public void M4_T2_PersonalityMult_ChangesPlanPreference()
        {
            // 식량 조달(RawFood>=15): 익은 밭(수확 +6, 비용 8)과 열매(+5, 비용 10)가 둘 다 가능한 상황 —
            // 농사꾼(Farm 0.7)은 밭 수확, 떠돌이(Gather 0.75)는 열매 채집이 플랜에 선택돼야 한다 (M4-S2)
            var catalog = AssetDatabase.LoadAssetAtPath<ActionCatalog>("Assets/M0Config/ActionCatalog.asset");
            var goal = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_GatherFood.asset");
            var farmer   = AssetDatabase.LoadAssetAtPath<PersonalitySO>("Assets/M0Config/Personalities/Personality_Farmer.asset");
            var wanderer = AssetDatabase.LoadAssetAtPath<PersonalitySO>("Assets/M0Config/Personalities/Personality_Wanderer.asset");
            WorldSnapshot snap = Snap((SlotId.MySatiety, 80), (SlotId.RawFoodStock, 10),
                                      (SlotId.RipeCropAvailable, 1), (SlotId.NearDiscoveredFood, 1));

            (PlanStatus fs, ActionSO[] fp) = RunPlan(snap, goal, PersonalityCost.Build(catalog, farmer, null));
            Assert.AreEqual(PlanStatus.Success, fs);
            Assert.AreEqual("HarvestCrop", fp[0].name, "농사꾼은 밭 수확 선호 (8×0.7=5.6 < 10×1.15)");

            (PlanStatus ws, ActionSO[] wp) = RunPlan(snap, goal, PersonalityCost.Build(catalog, wanderer, null));
            Assert.AreEqual(PlanStatus.Success, ws);
            Assert.AreEqual("HarvestWildBerries", wp[0].name, "떠돌이는 열매 채집 선호 (10×0.75=7.5 < 8×1.2)");
        }

        [Test]
        public void M4_T3_JudgeOrder_PersonalityOffsets()
        {
            // AgentConfig 기본 문턱 실측값: satiety < 35 거부 / fatigue > 70 거부 (M4-C 착수 시 확인)
            var cfg = ScriptableObject.CreateInstance<AgentConfigSO>();
            var docile   = AssetDatabase.LoadAssetAtPath<PersonalitySO>("Assets/M0Config/Personalities/Personality_Docile.asset");
            var stubborn = AssetDatabase.LoadAssetAtPath<PersonalitySO>("Assets/M0Config/Personalities/Personality_Stubborn.asset");

            // 경계 1 (satiety 40, fatigue 60): 기본·순둥이 수락, 고집쟁이는 배고픔 거부 (40 < 35+8)
            Assert.AreEqual(VillagerAgent.OrderResult.Accepted, VillagerAgent.JudgeOrder(40f, 60f, cfg, null));
            Assert.AreEqual(VillagerAgent.OrderResult.Accepted, VillagerAgent.JudgeOrder(40f, 60f, cfg, docile));
            Assert.AreEqual(VillagerAgent.OrderResult.RefusedHungry, VillagerAgent.JudgeOrder(40f, 60f, cfg, stubborn),
                            "같은 상태에서 고집쟁이만 거부 — 명령 대상 선택의 재미 (M4-S1)");

            // 경계 2 (satiety 80, fatigue 60): 고집쟁이는 피로 거부 (60 > 70-15)
            Assert.AreEqual(VillagerAgent.OrderResult.RefusedTired, VillagerAgent.JudgeOrder(80f, 60f, cfg, stubborn));
            Assert.AreEqual(VillagerAgent.OrderResult.Accepted, VillagerAgent.JudgeOrder(80f, 60f, cfg, docile));

            // 경계 3 (satiety 30): 기본은 거부, 순둥이는 수행 (30 >= 35-8 — 지쳐도 따라가는 성격)
            Assert.AreEqual(VillagerAgent.OrderResult.RefusedHungry, VillagerAgent.JudgeOrder(30f, 60f, cfg, null));
            Assert.AreEqual(VillagerAgent.OrderResult.Accepted, VillagerAgent.JudgeOrder(30f, 60f, cfg, docile));

            // 중립 불변식: null 성격 = 기존 3인자 판정과 동일 (M1-T1 계약 유지)
            Assert.AreEqual(VillagerAgent.JudgeOrder(40f, 60f, cfg), VillagerAgent.JudgeOrder(40f, 60f, cfg, null));
            Assert.AreEqual(VillagerAgent.JudgeOrder(20f, 95f, cfg), VillagerAgent.JudgeOrder(20f, 95f, cfg, null));
        }

        [Test]
        public void M4_Fix_BuildSite_AvoidsTilesReservedByOthers()
        {
            // 매몰 방지 (2026-07-16 방치 관측): 주민이 서 있는 타일 위에 차단 건물이 완공되면
            // JPS 출발 불가로 영구 고립 — 건설 위치 후보에서 타인 예약 타일을 제외한다
            AIVillage.AI.TileReservationRegistry.ResetAll();
            var tile = new Vector2Int(7, 7);

            Assert.IsFalse(AIVillage.AI.TileReservationRegistry.IsReservedByOther(tile, "builder"), "빈 타일");
            Assert.IsTrue(AIVillage.AI.TileReservationRegistry.TryReserve(tile, "builder"));
            Assert.IsFalse(AIVillage.AI.TileReservationRegistry.IsReservedByOther(tile, "builder"),
                           "자기 예약은 제자리 건설을 막지 않는다 (기존 동작 무변경)");
            Assert.IsTrue(AIVillage.AI.TileReservationRegistry.IsReservedByOther(tile, "rester"),
                          "타인 예약 타일은 건설 후보 제외");

            AIVillage.AI.TileReservationRegistry.ResetAll();
        }

        [Test]
        public void M4_E_PickWalkableNear_FiltersBlockedTiles()
        {
            // 반경 내 유일한 통행 가능 타일 — 랜덤이 불운해도 링 폴백이 반드시 찾는다 (결정적 성질)
            var only = new Vector2Int(12, 10);
            bool OnlyOne(int x, int y) => x == only.x && y == only.y;
            Assert.AreEqual(only, MapBounds.PickWalkableNear(OnlyOne, 10, 10, 2),
                            "반경 내 walkable이 존재하면 반드시 반환");

            // 전부 통행 불가 → 중심 클램프 (결정적 종료 — 이후는 이동 실패 first-class 처리)
            Assert.AreEqual(new Vector2Int(10, 10), MapBounds.PickWalkableNear((x, y) => false, 10, 10, 2),
                            "전부 막히면 중심 폴백 (무한 재추첨 없음)");

            // 전부 통행 가능 → 반경·경계 내 반환
            Vector2Int t = MapBounds.PickWalkableNear((x, y) => true, 0, 0, 3);
            Assert.LessOrEqual(Mathf.Max(Mathf.Abs(t.x), Mathf.Abs(t.y)), 3, "반경 내");

            // 필터 없음(null) = 기존 랜덤 동작 (중립 경로)
            Vector2Int u = MapBounds.PickWalkableNear(null, 0, 0, 2);
            Assert.LessOrEqual(Mathf.Max(Mathf.Abs(u.x), Mathf.Abs(u.y)), 2);
        }
    }
}
