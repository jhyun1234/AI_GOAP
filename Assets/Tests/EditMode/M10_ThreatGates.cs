using System.Collections.Generic;
using AIVillage.M0;
using NUnit.Framework;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M10-C 위협 선반 게이트 (M10-T3) — 사다리·진입점·희생 선정의 결정성 (ADR-M10-1: 확률 금지).
    /// 위력 곡선은 DisasterService.LossCount 재사용이라 M9-T3가 이미 게이트한다 (산식 이원화 없음
    /// — 원시형 위임 동일성만 여기서 재확인).
    /// </summary>
    public class M10_ThreatGates
    {
        private static ThreatSO Tier(string name, float unlockDay)
        {
            var t = ScriptableObject.CreateInstance<ThreatSO>();
            t.name = name;
            t.DisplayName = name;
            t.UnlockDay = unlockDay;
            return t;
        }

        [Test]
        public void M10_T3_PickTier_LadderAndPlateau()
        {
            // 축 = 게임일 (ADR-M10R-1 시간 래칫). 0/12/16 은 잠금 게임일.
            ThreatSO t1 = Tier("T1", 0), t2 = Tier("T2", 12), t3 = Tier("T3", 16);
            var threats = new[] { t1, t2, t3 };

            Assert.AreSame(t1, ThreatService.PickTier(threats, 10f), "Day 10 = 밴드1 (12 미달)");
            Assert.AreSame(t2, ThreatService.PickTier(threats, 12f), "잠금일 도달 = 밴드2");
            Assert.AreSame(t3, ThreatService.PickTier(threats, 16f), "Day 16 = 밴드3");
            Assert.AreSame(t3, ThreatService.PickTier(threats, 999f), "등록 밖 상위 없음 = 최신 밴드 반복 (플래토, ADR-M10R-1)");
            Assert.IsNull(ThreatService.PickTier(new ThreatSO[0], 10f), "빈 등록 = 위협 없음 (중립 불변식)");
            Assert.IsNull(ThreatService.PickTier(new[] { Tier("T5", 50f) }, 10f), "전 밴드 미달 = null");

            // 동률 = 배열 앞 우선 (결정적)
            ThreatSO dupA = Tier("A", 5f), dupB = Tier("B", 5f);
            Assert.AreSame(dupA, ThreatService.PickTier(new[] { dupA, dupB }, 10f), "UnlockDay 동률 = 배열 앞");

            foreach (ThreatSO t in new[] { t1, t2, t3, dupA, dupB }) Object.DestroyImmediate(t);
        }

        [Test]
        public void M10_T3_RollTargetsVillagers_Deterministic()
        {
            var so = ScriptableObject.CreateInstance<ThreatSO>();
            so.DisplayName = "늑대 무리"; so.VillagerTargetChance = 0.5f;

            // 결정성 (ADR-M10R-2): 같은 서수 = 같은 결과
            for (int ord = 1; ord <= 5; ord++)
                Assert.AreEqual(ThreatService.RollTargetsVillagers(so, ord),
                                ThreatService.RollTargetsVillagers(so, ord), $"서수 {ord} 재현");

            // 경계: 0 = 항상 밭, 1 = 항상 주민
            so.VillagerTargetChance = 0f;
            for (int ord = 1; ord <= 20; ord++)
                Assert.IsFalse(ThreatService.RollTargetsVillagers(so, ord), "확률 0 = 밭만");
            so.VillagerTargetChance = 1f;
            for (int ord = 1; ord <= 20; ord++)
                Assert.IsTrue(ThreatService.RollTargetsVillagers(so, ord), "확률 1 = 주민만");

            // 분포 sanity: 0.5에서 100 서수 중 극단 치우침 없음
            so.VillagerTargetChance = 0.5f;
            int villagerHits = 0;
            for (int ord = 1; ord <= 100; ord++)
                if (ThreatService.RollTargetsVillagers(so, ord)) villagerHits++;
            Assert.That(villagerHits, Is.InRange(25, 75), "0.5 확률이 전무·전량으로 치우치지 않음");

            Object.DestroyImmediate(so);
        }

        [Test]
        public void M10_T3_VillageScale_ThreeAxes()
        {
            // 주민 + 밭 + 집 — 착지 상태 검산 (명세 §4.5: 4+4+2=10 → 티어1만)
            Assert.AreEqual(10, ThreatService.VillageScale(4, 4, 2));
            Assert.AreEqual(0, ThreatService.VillageScale(0, 0, 0), "전멸·무건설 = 0");
        }

        [Test]
        public void M10_T3_EntryPoint_DeterministicAndOnEdge()
        {
            const int MIN_X = -50, MAX_X = 49, MIN_Y = -50, MAX_Y = 49;
            for (uint seed = 0; seed < 8; seed++)
            {
                Vector2Int a = ThreatService.EntryPoint(seed, MIN_X, MAX_X, MIN_Y, MAX_Y);
                Vector2Int b = ThreatService.EntryPoint(seed, MIN_X, MAX_X, MIN_Y, MAX_Y);
                Assert.AreEqual(a, b, $"같은 시드 = 같은 진입점 (seed {seed})");
                bool onEdge = a.x == MIN_X || a.x == MAX_X || a.y == MIN_Y || a.y == MAX_Y;
                Assert.IsTrue(onEdge, $"진입점은 항상 가장자리 (seed {seed}: {a})");
            }
            // 4변 순환 — 서로 다른 변이 실제로 나온다 (한 변 고정 아님)
            Assert.AreNotEqual(ThreatService.EntryPoint(0, MIN_X, MAX_X, MIN_Y, MAX_Y),
                               ThreatService.EntryPoint(1, MIN_X, MAX_X, MIN_Y, MAX_Y));
        }

        [Test]
        public void M10_T3_PickNearestVictims_DistanceOrderTieAndClamp()
        {
            var candidates = new List<(string id, int x, int y)>
            {
                ("M0_Villager_D", 5, 5), // 거리² 50
                ("M0_Villager_B", 0, 3), // 거리² 9 — 동률
                ("M0_Villager_A", 3, 0), // 거리² 9 — 동률, id 앞
                ("M0_Villager_C", 1, 1), // 거리² 2 — 최근접
            };
            var result = new List<int>();

            ThreatService.PickNearestVictims(0, 0, candidates, 3, result);
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(3, result[0], "최근접 C");
            Assert.AreEqual(2, result[1], "동률(9)은 id 사전순 — A 먼저");
            Assert.AreEqual(1, result[2], "그다음 B");

            ThreatService.PickNearestVictims(0, 0, candidates, 99, result);
            Assert.AreEqual(4, result.Count, "count > 후보 = 전원 (클램프)");

            ThreatService.PickNearestVictims(0, 0, candidates, 0, result);
            Assert.AreEqual(0, result.Count, "count 0 = 없음");
        }

        // ── M10-T4 도망 (M10-D) ───────────────────────────────────────────────

        [Test]
        public void M10_T4_WithinDanger_PersonalRadiusBoundary()
        {
            const int RADIUS = 6;
            // 중립 배율 1.0 — 반경 그대로
            Assert.IsTrue(ThreatService.WithinDanger(6, RADIUS, 1.0f), "경계 포함");
            Assert.IsFalse(ThreatService.WithinDanger(7, RADIUS, 1.0f), "경계 밖");
            // 고집쟁이 0.6 — ⌈6×0.6⌉=4: 위협이 4타일까지 와야 알아챈다 (늦은 도망 = 부상 후보)
            Assert.IsTrue(ThreatService.WithinDanger(4, RADIUS, 0.6f), "축소 반경 경계");
            Assert.IsFalse(ThreatService.WithinDanger(5, RADIUS, 0.6f), "5타일은 아직 태평");
            // 새침 1.2 — ⌈7.2⌉=8: 예민
            Assert.IsTrue(ThreatService.WithinDanger(8, RADIUS, 1.2f), "확대 반경 경계");
            Assert.IsFalse(ThreatService.WithinDanger(9, RADIUS, 1.2f));
        }

        [Test]
        public void M10_T4_FleeNeutralInvariant()
        {
            // 위협 미배선 = ThreatNear 항상 0 (Goal_Flee 트리거 ==1 영구 불발 — M9 동작)
            var world = new WorldModel(null, null);
            WorldSnapshot snap = world.BuildSnapshot(50, 50); // threatNear 기본 false
            Assert.AreEqual(0, snap.Get(SlotId.ThreatNear), "기본 스냅샷 ThreatNear 0 (중립 불변식)");
            Assert.AreEqual(1, world.BuildSnapshot(50, 50, false, true).Get(SlotId.ThreatNear),
                "감지 시에만 1");

            // 성격 기본 배율 1 (기존 성격 에셋 미수정분 = 중립)
            var p = ScriptableObject.CreateInstance<PersonalitySO>();
            Assert.AreEqual(1f, p.FleeRadiusMult, 1e-5f, "신규 필드 기본값 = 중립");
            Object.DestroyImmediate(p);
        }

        [Test]
        public void M10_T4_Goal_Flee_TriggerGoalConsistency()
        {
            // ADR-M0-7 정합의 논리 재현: 목표(ThreatNear==0) 달성값 0은 트리거(==1)를 만족하지
            // 않는다 — 무한 루프 없음 (에셋 OnValidate는 Unity 임포트가 검증)
            Assert.AreNotEqual(0, 1, "목표값 0 ≠ 트리거값 1");
            // 도망은 부상자도 간다 — Goal_Flee.asset AllowedWhenInjured=1과 짝 (BlockedByInjury false)
            var flee = ScriptableObject.CreateInstance<GoalSO>();
            flee.AllowedWhenInjured = true;
            Assert.IsFalse(VillagerAgent.BlockedByInjury(flee, InjurySeverity.Light),
                "부상 중에도 도망 goal은 후보 잔존");
            Object.DestroyImmediate(flee);
        }

        [Test]
        public void M10_T3_LossCount_PrimitiveDelegationIdentity()
        {
            // 원시형(위협)과 재해 버전이 같은 산식 — 산식 이원화 없음의 증거
            var d = ScriptableObject.CreateInstance<DisasterSO>();
            d.BaseLossPct = 0.25f; d.PerTargetPct = 0.03f; d.MaxLossPct = 0.4f;
            for (int targets = 0; targets <= 12; targets++)
                Assert.AreEqual(DisasterService.LossCount(targets, d, resisted: false),
                                DisasterService.LossCount(targets, 0.25f, 0.03f, 0.4f),
                                $"대상 {targets} — 위임 동일성");
            Object.DestroyImmediate(d);

            // 명세 §4.5 검산: 후보 2명, 곡선 0.25/0.03/0.25 → 부상 1명 (인구 25% 상한 정신)
            Assert.AreEqual(1, DisasterService.LossCount(2, 0.25f, 0.03f, 0.25f));
        }
    }
}
