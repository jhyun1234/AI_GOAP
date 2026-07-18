using System.Collections.Generic;
using AIVillage.M0;
using NUnit.Framework;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M9 재해 게이트 (명세 M9-C, M9-T3) — 소실 수 산식의 단조성·클램프·저항, 희생 선택의
    /// 결정성·무중복·전원, 계절당 1회 발동(서수 기반)을 검증한다. 파괴는 M9-B 문을 지나므로
    /// 여기선 발동 판정·산식만 (실제 소멸 정합은 M9-T2가 담당).
    /// </summary>
    public class M9_DisasterGates
    {
        private static DisasterSO Disaster(bool targetCrops = true, float baseP = 0.25f,
            float perP = 0.03f, float maxP = 0.6f, float resistMult = 0.5f, bool useResist = false)
        {
            var d = ScriptableObject.CreateInstance<DisasterSO>();
            d.DisplayName = "테스트재해";
            d.TargetCrops = targetCrops;
            d.BaseLossPct = baseP;
            d.PerTargetPct = perP;
            d.MaxLossPct = maxP;
            d.ResistMult = resistMult;
            d.UseResistance = useResist;
            return d;
        }

        private static WorldConfigSO Config()
        {
            var c = ScriptableObject.CreateInstance<WorldConfigSO>();
            c.InitialWoodStock = 0;
            return c;
        }

        [Test]
        public void M9_T3_LossCount_ZeroTarget_MonotoneRate_Clamp_Resist()
        {
            DisasterSO d = Disaster();

            Assert.AreEqual(0, DisasterService.LossCount(0, d, false), "대상 0 → 소실 0 (발동 로그만)");

            int loss4 = DisasterService.LossCount(4, d, false);   // pct=clamp(0.37)=0.37 → ceil(1.48)=2
            int loss12 = DisasterService.LossCount(12, d, false);  // pct=clamp(0.61)=0.6  → ceil(7.2)=8
            Assert.Less(loss4 / 4f, loss12 / 12f, "대상 많을수록 소실 비율↑ (PerTargetPct 단조)");

            int loss50 = DisasterService.LossCount(50, d, false);  // pct 클램프 0.6 → ceil(30)=30
            Assert.AreEqual(Mathf.CeilToInt(50 * 0.6f), loss50, "MaxLossPct 클램프");

            int lossResisted = DisasterService.LossCount(12, d, true); // 0.6*0.5=0.3 → ceil(3.6)=4
            Assert.Less(lossResisted, loss12, "저항 완공 시 소실 반감");

            Assert.LessOrEqual(DisasterService.LossCount(3, d, false), 3, "소실은 대상 수를 넘지 않는다");
        }

        [Test]
        public void M9_T3_PickVictims_Deterministic_NoDup_AllWhenOverflow()
        {
            var r1 = new List<int>();
            var r2 = new List<int>();
            DisasterService.PickVictims(10, 3, 12345u, r1);
            DisasterService.PickVictims(10, 3, 12345u, r2);
            Assert.AreEqual(3, r1.Count);
            CollectionAssert.AreEqual(r1, r2, "같은 (수, 시드) → 같은 목록 (결정성)");
            Assert.AreEqual(r1.Count, new HashSet<int>(r1).Count, "중복 없음");
            foreach (int i in r1) { Assert.GreaterOrEqual(i, 0); Assert.Less(i, 10); }

            var rAll = new List<int>();
            DisasterService.PickVictims(5, 99, 1u, rAll);
            Assert.AreEqual(5, rAll.Count, "loss ≥ targets → 전원");

            var rZero = new List<int>();
            DisasterService.PickVictims(10, 0, 1u, rZero);
            Assert.AreEqual(0, rZero.Count, "loss 0 → 빈 목록");
        }

        [Test]
        public void M9_T3_Tick_OncePerSeasonOrdinal_RefiresNextCycle()
        {
            var summer = ScriptableObject.CreateInstance<SeasonSO>();
            summer.DisplayName = "여름";
            summer.DurationDays = 3f;

            DisasterSO d = Disaster();
            d.DisplayName = "폭염";
            d.Season = summer;
            d.OffsetDays = 1.5f;

            var farm = new FarmService(growthDays: 1.5f);
            farm.RegisterPlot(1, 1);
            farm.RegisterPlot(2, 2);
            farm.RegisterPlot(3, 3);
            foreach (FarmPlot p in farm.Plots) farm.TryPlant(p); // 전부 Growing (작물 대상)

            var world = new WorldModel(new DiscoveryService(), Config(), farm);
            var construction = new ConstructionService(world);
            var svc = new DisasterService(new[] { d }, farm, construction, world);
            int struck = 0;
            svc.OnStruck += (dd, n) => struck++;

            svc.Tick(summer, 1.0f, 0);                     // 경과일 < offset → 미발동
            Assert.AreEqual(0, struck);

            svc.Tick(summer, 1.5f, 0);                     // offset 도달 → 발동
            Assert.AreEqual(1, struck);

            svc.Tick(summer, 2.0f, 0);                     // 같은 서수 반복 → 재발동 없음
            svc.Tick(summer, 2.9f, 0);
            Assert.AreEqual(1, struck, "같은 계절 서수 내 발동 1회 (ADR-M9-4)");

            svc.Tick(summer, 1.5f, 3);                     // 다음 사이클 여름(서수 증가) → 재발동
            Assert.AreEqual(2, struck, "다음 사이클 같은 계절에 재발동");
        }

        [Test]
        public void M9_T3_Neutral_EmptyDisasters_And_WrongSeason_NoStrike()
        {
            var summer = ScriptableObject.CreateInstance<SeasonSO>();
            summer.DurationDays = 3f;
            var winter = ScriptableObject.CreateInstance<SeasonSO>();
            winter.DurationDays = 4f;

            var farm = new FarmService(1.5f);
            farm.RegisterPlot(1, 1);
            var world = new WorldModel(new DiscoveryService(), Config(), farm);
            var construction = new ConstructionService(world);

            var empty = new DisasterService(System.Array.Empty<DisasterSO>(), farm, construction, world);
            int s1 = 0; empty.OnStruck += (a, b) => s1++;
            empty.Tick(summer, 2f, 0);
            Assert.AreEqual(0, s1, "Disasters 빈 배열 → 무동작 (중립 불변식)");

            DisasterSO d = Disaster();
            d.Season = summer;
            var svc = new DisasterService(new[] { d }, farm, construction, world);
            int s2 = 0; svc.OnStruck += (a, b) => s2++;
            svc.Tick(winter, 2f, 0); // 현재 계절(겨울) != 재해 계절(여름)
            Assert.AreEqual(0, s2, "계절 불일치 → 미발동");
        }
    }
}
