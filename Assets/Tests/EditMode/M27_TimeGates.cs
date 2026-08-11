using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using AIVillage.M0;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M27 시간 밸런스 게이트 (Docs/M27_시간밸런스축_실행명세서.md §W4).
    ///
    /// 체감 시계 필드는 일수가 아니라 **실시간 초**로 산다 — 눈금(GameTimeScale)이나 일수
    /// 어느 쪽이 바뀌어도 곱이 대역을 벗어나면 여기서 잡는다 (§2 분류의 기계 감시).
    /// W1~W2에서 게이트 3개(M24_T3·M21_T13·M24_T11)가 일수 리터럴에 실시간 의미를 몰래
    /// 박아 두었다가 깨졌다 — 이 파일은 그 계급의 회귀를 전담한다.
    ///
    /// - 대역은 "분류가 뒤집혔는가"를 잡는 그물이지 값 고정이 아니다. 튜닝은 대역 안에서 자유.
    /// - 🔴 배포 에셋만 읽는다 — CreateInstance는 스크립트 기본값(눈금 개정 전 세계)을
    ///   검사하는 빈 검사가 된다 (ADR-M27-3: 기본값은 동결된 이력이다).
    /// </summary>
    public class M27_TimeGates
    {
        private static WorldConfigSO World()
        {
            var c = AssetDatabase.LoadAssetAtPath<WorldConfigSO>("Assets/M0Config/WorldConfig.asset");
            Assert.IsNotNull(c, "WorldConfig 로드");
            return c;
        }

        private static AgentConfigSO Agent()
        {
            var a = AssetDatabase.LoadAssetAtPath<AgentConfigSO>("Assets/M0Config/AgentConfig.asset");
            Assert.IsNotNull(a, "AgentConfig 로드");
            return a;
        }

        private static void AssertBand(string what, float sec, float lo, float hi)
            => Assert.IsTrue(sec >= lo && sec <= hi,
                $"{what} = 실시간 {sec:0.#}초 — 대역 [{lo}~{hi}]초 밖. 체감 시계가 눈금과 어긋났다 " +
                "(GameTimeScale과 일수 중 한쪽만 바뀌었는가? 명세 §2·§3)");

        [Test]
        public void M27_T1_PerceivedClocksHoldRealtimeBands()
        {
            WorldConfigSO w = World();
            Assert.Greater(w.GameTimeScale, 0f, "GameTimeScale이 0 — 시간이 안 흐른다");
            float daySec = 1f / w.GameTimeScale;

            // 실패 가능성 증명 — 舊 세계(웨이브 5일)가 현 눈금에서 대역을 깨는지 먼저 확인한다.
            Assert.Greater(5f * daySec, 900f,
                "舊 5일 웨이브가 대역 안이다 — 이 게이트는 W2 회귀(체감 ÷6 되돌림)를 못 잡는다");

            AssertBand("웨이브 주기", w.WavePeriodDays * daySec, 300f, 900f);           // 5~15분
            AssertBand("재무장(전리품 재생)", w.ResidentRearmCooldownDays * daySec, 240f, 420f); // W5R 판정 5분±
            AssertBand("방랑자 도착 주기", w.WandererIntervalDays * daySec, 300f, 900f);
            AssertBand("압력 1점 소요", w.PressureDaysPerPoint * daySec, 240f, 420f);
            AssertBand("사망 자비 지연", w.ThreatReliefDelayDays * daySec, 120f, 420f);

            // 예고 창 — 배포 위협 에셋 전수 (T5 교훈: 손 값이 아니라 배포 값으로).
            Assert.Greater(w.Threats.Length, 0, "위협 에셋이 없다 — 공허 통과");
            foreach (ThreatSO t in w.Threats)
                AssertBand($"{t.name} 예고 창", t.WarnDays * daySec, 60f, 300f);
        }

        [Test]
        public void M27_T2_HungerRhythmAndWinterStockpileHoldRealtime()
        {
            WorldConfigSO w = World();
            AgentConfigSO a = Agent();
            float daySec = 1f / w.GameTimeScale;

            // 식사 리듬 — 만복(초기 포만)에서 P0 발동선까지의 실시간. 발동선은 배포 goal에서 읽는다.
            var p0 = AssetDatabase.LoadAssetAtPath<GoalSO>("Assets/M0Config/Goals/Goal_P0_Hunger.asset");
            Assert.IsNotNull(p0, "Goal_P0_Hunger 로드");
            int p0Line = -1;
            foreach (SlotCondition c in p0.TriggerConditions)
                if (c.Slot == SlotId.MySatiety) p0Line = c.Value;
            Assert.GreaterOrEqual(p0Line, 0, "P0 트리거에 포만 조건이 없다 — 식사 리듬을 잴 기준이 사라졌다");

            float mealSec = (a.InitialSatiety - p0Line) / a.SatietyDecayPerGameDay * daySec;
            AssertBand("식사 간격(만복→P0선)", mealSec, 120f, 360f); // 율 ×6 되돌림(감쇠 25 복원)이면 1,200초로 튄다

            // 겨울 비축 가능성 — 상한 총량(단위×포만/단위)이 위기 계절의 필요 포만을 덮는가.
            // 단위 환산은 배포 EatRawFood에서 읽는다 (수치 발명 금지, ADR-M0-2).
            SeasonSO winter = null;
            foreach (SeasonSO s in w.SeasonCycle)
                if (s != null && s.IsCrisis) winter = s;
            Assert.IsNotNull(winter, "위기 계절(IsCrisis)이 SeasonCycle에 없다");

            var eat = AssetDatabase.LoadAssetAtPath<ActionSO>("Assets/M0Config/Actions/EatRawFood.asset");
            Assert.IsNotNull(eat, "EatRawFood 로드");
            int unitGain = 0;
            foreach (SlotEffect e in eat.Effects)
                if (e.Slot == SlotId.MySatiety) unitGain = Mathf.Max(unitGain, e.Value);
            Assert.Greater(unitGain, 0, "EatRawFood에 포만 효과가 없다 — 단위 환산 불가");

            float winterNeed = winter.DurationDays * a.SatietyDecayPerGameDay * winter.SatietyDecayMult;
            float capSatiety = (a.BodyCarryCap + a.HomeStorageCap) * unitGain;
            Assert.GreaterOrEqual(capSatiety, winterNeed,
                $"저장 상한 총량({capSatiety:0.#} 포만)이 겨울 필요({winterNeed:0.#})보다 작다 — " +
                "비축이 산술적으로 불가능 (율을 올리고 용량 짝을 빠뜨렸는가? 명세 §W3)");

            // 실패 가능성 증명 — 舊 상한(8+15)이 이 검사를 통과한다면 용량 짝 회귀를 못 잡는 그물이다.
            Assert.Less((8 + 15) * (float)unitGain, winterNeed,
                "舊 상한(몸8+집15)이 겨울 필요를 덮는다 — 이 검사는 빈 그물이다 (겨울·율 값 재확인)");
        }
    }
}
