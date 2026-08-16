using AIVillage.Core.GOAP;
using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

using static AIVillage.Tests.EditMode.GateHelpers;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M4 성격 게이트 (명세 M4-A~F). 이 파일이 M4-T1~T3의 집이다 —
    /// M4-E(랜덤 목표 Walkable 필터)를 시작으로 A(스키마)·B(중립 불변식·선호 플랜)·
    /// C(거부 오프셋) 게이트가 뒤에 추가된다.
    /// </summary>
    public class M4_PersonalityGates
    {
        private static (PlanStatus status, ActionSO[] plan) RunPlan(WorldSnapshot snap, GoalSO goal, float[] costMult)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ActionCatalog>("Assets/M0Config/ActionCatalog.asset");
            var gw = new PlannerGateway(catalog);
            PlannerGateway.PendingPlan pending = gw.RequestPlan(snap, goal, costMult);
            gw.CompleteNow(pending);
            Assert.IsTrue(gw.TryGetResult(pending, out PlanStatus status, out ActionSO[] plan, out _));
            return (status, plan);
        }

        // Snap → GateHelpers (2026-08-11 2차 감사 통합). RunPlan(costMult)은 게이트웨이를
        // 스스로 만드는 이 파일 고유 변형이라 남긴다.

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
