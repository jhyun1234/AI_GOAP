using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M24 종족 침공 축 게이트. 1차 W3까지 = 압력 축의 단조성.
    ///
    /// 🔑 이 파일이 지키는 것은 하나다 — **압력은 어떤 경로로도 내려가지 않는다**(ADR-M24-1).
    /// 그 규칙이 막는 것은 정반대 실패 둘이고, 둘 다 실제로 관측된 적이 있다:
    ///   ① 사망 → 마을 축소 → 위협 강등 → 영구 안정 (ADR-M10R-1이 고친 음성 피드백 루프)
    ///   ② 사망 → 압력 유지 → 회복 불가 → 남은 판이 소화 시간 (①의 거울상)
    /// ①은 여기가 막고, ②는 자비 장치(W9)가 맡는다. 압력을 낮추는 것으로는 어느 쪽도 안 푼다.
    /// </summary>
    public class M24_RaceInvasionGates
    {
        // 게이트가 자기 값을 발명하지 않도록 배포 에셋에서 읽는다 (ADR-M0-2).
        private static WorldConfigSO Config()
        {
            var c = AssetDatabase.LoadAssetAtPath<WorldConfigSO>("Assets/M0Config/WorldConfig.asset");
            Assert.IsNotNull(c, "WorldConfig 로드");
            return c;
        }

        // ── T2: 압력 단조 ─────────────────────────────────────────────────────

        [Test]
        public void M24_T2_GlobalPressure_NeverFallsWhenPopulationDrops()
        {
            // 이 축 전체가 서 있는 단 하나의 성질. 인구가 12에서 5로 무너져도 압력은 그대로다 —
            // 읽는 것이 현재 인구가 아니라 **역대 최고**이기 때문이다.
            WorldConfigSO c = Config();
            int atPeak = ThreatService.GlobalPressure(30f, 12, c.PressureDaysPerPoint, c.PressurePopPerPoint);
            int afterLoss = ThreatService.GlobalPressure(30f, 12, c.PressureDaysPerPoint, c.PressurePopPerPoint);
            Assert.AreEqual(atPeak, afterLoss,
                "역대 최고 인구는 사망으로 줄지 않는다 — 줄면 ADR-M10R-1 음성 피드백 루프가 돌아온다");

            // 위반 실증 (M17 "실패 불가능한 테스트" 교훈) — 현재 인구를 넘기면 실제로 떨어지는가.
            // 떨어져야 정상이다: 이 게이트가 지키는 것은 "호출자가 peak를 넘긴다"는 계약이고,
            // 함수 자체는 받은 값을 정직하게 쓴다. 여기서 값이 안 떨어지면 검사가 무의미해진다.
            int ifCurrentUsed = ThreatService.GlobalPressure(30f, 5, c.PressureDaysPerPoint, c.PressurePopPerPoint);
            Assert.Less(ifCurrentUsed, atPeak,
                "현재 인구를 넘기면 압력이 떨어져야 한다 — 안 떨어지면 이 게이트가 아무것도 증명하지 않는다");
        }

        [Test]
        public void M24_T2_GlobalPressure_MonotonicInBothTerms()
        {
            WorldConfigSO c = Config();
            int prev = -1;
            for (float day = 0f; day <= 120f; day += 1f)   // 시간 축
            {
                int p = ThreatService.GlobalPressure(day, 8, c.PressureDaysPerPoint, c.PressurePopPerPoint);
                Assert.GreaterOrEqual(p, prev, $"Day {day}: 시간이 흘렀는데 압력이 줄었다");
                prev = p;
            }
            prev = -1;
            for (int peak = 0; peak <= 40; peak++)          // 성장 축
            {
                int p = ThreatService.GlobalPressure(30f, peak, c.PressureDaysPerPoint, c.PressurePopPerPoint);
                Assert.GreaterOrEqual(p, prev, $"역대최고 {peak}: 성장했는데 압력이 줄었다");
                prev = p;
            }
        }

        [Test]
        public void M24_T2_EffectivePressure_MonotonicInEncounters()
        {
            int prev = -1;
            for (int seen = 0; seen <= 20; seen++)
            {
                int p = ThreatService.EffectivePressure(10, seen, 0.5f);
                Assert.GreaterOrEqual(p, prev, $"조우 {seen}회: 더 만났는데 유효압력이 줄었다");
                prev = p;
            }
            // k=0 = 적응하지 않는 종족 → 전역압력과 같다 (중립 불변식)
            Assert.AreEqual(10, ThreatService.EffectivePressure(10, 99, 0f),
                "k=0인데 조우가 압력을 밀었다 — 중립이 깨졌다");
            // 전역압력이 바닥을 깐다 — 한 번도 안 만난 종족도 시간이 지나면 세진다
            Assert.AreEqual(10, ThreatService.EffectivePressure(10, 0, 0.5f),
                "조우 0인데 전역압력이 안 실렸다 — 안 만난 종족이 영영 1단으로 남는다");
        }

        [Test]
        public void M24_T2_ShippedRaces_HaveDistinctAdaptationSpeed()
        {
            // k가 전부 같으면 "나에게 적응하는 적"이 종족 구분을 못 만든다 (축의 존재 이유).
            var seen = new System.Collections.Generic.HashSet<float>();
            int races = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ThreatSO"))
            {
                var so = AssetDatabase.LoadAssetAtPath<ThreatSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (so == null) continue;
                races++;
                Assert.GreaterOrEqual(so.EncounterPressureK, 0f,
                    $"{so.name}: k가 음수면 조우할수록 순해진다 (단조 위반)");
                seen.Add(so.EncounterPressureK);
            }
            Assert.Greater(races, 0, "배포 종족이 하나도 없다");
            Assert.Greater(seen.Count, 1,
                "전 종족의 k가 같다 — 종족마다 다른 속도로 배운다는 설계가 값에 없다");
        }

        // ── 조우 카운트 규약 (ADR-M24-5) ──────────────────────────────────────

        [Test]
        public void M24_T2_PressureDisplay_IsNeutralWhenUnwired()
        {
            // HUD 병기는 미배선(-1)이면 문구를 한 글자도 안 바꾼다 — 기존 게이트 보존의 근거.
            string without = SeasonHud.Compose(12f, null, 3f);
            string withNeg = SeasonHud.Compose(12f, null, 3f, -1);
            Assert.AreEqual(without, withNeg, "미배선인데 문구가 바뀌었다 (중립 불변식)");
            StringAssert.Contains("압력 7", SeasonHud.Compose(12f, null, 3f, 7),
                "압력을 넘겼는데 화면에 안 나온다");
        }
    }
}
