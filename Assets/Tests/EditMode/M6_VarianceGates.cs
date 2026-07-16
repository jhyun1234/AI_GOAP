using AIVillage.M0;
using NUnit.Framework;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// 개체 편차 게이트 (2026-07-17 동시 허기 웨이브 근본 수정의 재발 방지).
    /// 사고: GetHashCode()%1000이 꼬리 1글자 차이 이름에서 붕괴 — 씬 주민 5명의
    /// 초기 포만·속도·색 편차가 전부 사실상 동일했음. 이 게이트는 실제 씬 이름으로
    /// StableHash의 흩어짐을 직접 검증한다 — 편차 구현을 바꾸면 여기부터 깨져야 한다.
    /// </summary>
    public class M6_VarianceGates
    {
        // 실제 씬 배치 이름 (M0Scene.unity) — 주민 추가 시 여기도 추가
        private static readonly string[] SceneNames =
        {
            "M0_Villager_A", "M0_Villager_B", "M0_Villager_C", "M0_Villager_D", "M0_Villager_E",
        };

        private static float MaxPairwiseGap(string salt)
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (string name in SceneNames)
            {
                float v = StableHash.Spread(name, salt);
                if (v < min) min = v;
                if (v > max) max = v;
            }
            return max - min;
        }

        [Test]
        public void Variance_SceneNames_SpreadAcrossRange()
        {
            // 붕괴 사고의 재현 조건 그대로: 꼬리 1글자만 다른 5개 이름.
            // 舊 구현은 전 축 gap ≈ 0.008이었다 — 0.3 미만이면 편차가 다시 죽은 것.
            Assert.Greater(MaxPairwiseGap("satiety"), 0.3f, "초기 포만 편차가 뭉침 (웨이브 재발 위험)");
            Assert.Greater(MaxPairwiseGap("decay"),   0.3f, "감쇠율 편차가 뭉침 (영구 동기화 위험)");
            Assert.Greater(MaxPairwiseGap("speed"),   0.3f, "이동 속도 편차가 뭉침");
        }

        [Test]
        public void Variance_DeterministicAndSaltIndependent()
        {
            // 결정성 — 세이브 불필요의 근거 (로드 후 재계산해도 같은 주민은 같은 편차)
            Assert.AreEqual(StableHash.Spread("M0_Villager_A", "satiety"),
                            StableHash.Spread("M0_Villager_A", "satiety"), "재계산 = 동일값");

            // salt 독립 — 같은 주민이라도 축(포만/감쇠/속도)마다 다른 편차를 얻는다
            bool anySaltDiffers = false;
            foreach (string name in SceneNames)
                if (!Mathf.Approximately(StableHash.Spread(name, "satiety"),
                                         StableHash.Spread(name, "decay")))
                    anySaltDiffers = true;
            Assert.IsTrue(anySaltDiffers, "salt가 축을 분리하지 못함");

            // 범위 계약: Spread ∈ [-1, 1], Value01 ∈ [0, 1)
            foreach (string name in SceneNames)
            {
                float s = StableHash.Spread(name, "satiety");
                Assert.That(s, Is.InRange(-1f, 1f));
                float v = StableHash.Value01(name, "hue");
                Assert.That(v, Is.InRange(0f, 0.9999f));
            }
        }
    }
}
