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
        private static ThreatSO Tier(string name, int minScale)
        {
            var t = ScriptableObject.CreateInstance<ThreatSO>();
            t.name = name;
            t.DisplayName = name;
            t.MinVillageScale = minScale;
            return t;
        }

        [Test]
        public void M10_T3_PickTier_LadderAndPlateau()
        {
            ThreatSO t1 = Tier("T1", 0), t2 = Tier("T2", 12), t3 = Tier("T3", 16);
            var threats = new[] { t1, t2, t3 };

            Assert.AreSame(t1, ThreatService.PickTier(threats, 10), "규모 10 = 티어1 (12 미달)");
            Assert.AreSame(t2, ThreatService.PickTier(threats, 12), "임계 도달 = 티어2");
            Assert.AreSame(t3, ThreatService.PickTier(threats, 16), "규모 16 = 티어3");
            Assert.AreSame(t3, ThreatService.PickTier(threats, 999), "등록 밖 상위 없음 = 최고 티어 반복 (플래토, ADR-M10-6)");
            Assert.IsNull(ThreatService.PickTier(new ThreatSO[0], 10), "빈 등록 = 위협 없음 (중립 불변식)");
            Assert.IsNull(ThreatService.PickTier(new[] { Tier("T5", 50) }, 10), "전 티어 미달 = null");

            // 동률 = 배열 앞 우선 (결정적)
            ThreatSO dupA = Tier("A", 5), dupB = Tier("B", 5);
            Assert.AreSame(dupA, ThreatService.PickTier(new[] { dupA, dupB }, 10), "MinScale 동률 = 배열 앞");

            foreach (ThreatSO t in new[] { t1, t2, t3, dupA, dupB }) Object.DestroyImmediate(t);
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
