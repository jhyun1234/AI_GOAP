using AIVillage.M0;
using NUnit.Framework;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M12 성향 축 게이트 (명세 M12-A~).
    ///   M12-T1: 유도기의 중립 불변식·클램프·미등록 축 — 4작용형식의 공통 토대
    ///   M12-T2: 축 추가 내성 (S3) — 7번째 축이 생겨도 기존 벡터의 유도값이 불변
    /// </summary>
    public class M12_TraitGates
    {
        private static TraitValue V(TraitId t, int v) => new TraitValue { Trait = t, Value = v };
        private static TraitWeight W(TraitId t, float w) => new TraitWeight { Trait = t, Weight = w };

        // ── M12-T1: 유도기 ───────────────────────────────────────────────────

        [Test]
        public void M12_T1_Bias_NeutralWhenEitherSideMissing()
        {
            var traits = new[] { V(TraitId.Diligence, 100) };
            var weights = new[] { W(TraitId.Diligence, 1f) };

            Assert.AreEqual(0f, TraitVector.Bias(null, null), 0.0001f, "둘 다 없음 = 중립");
            Assert.AreEqual(0f, TraitVector.Bias(traits, null), 0.0001f, "가중치 없음 = 중립 (통로가 성향을 안 읽는다)");
            Assert.AreEqual(0f, TraitVector.Bias(null, weights), 0.0001f, "벡터 없음 = 중립 (성격 미배선 경로)");
            Assert.AreEqual(0f, TraitVector.Bias(new TraitValue[0], new TraitWeight[0]), 0.0001f, "빈 배열 = 중립");
        }

        [Test]
        public void M12_T1_ValueOf_UnregisteredAxisIsZero()
        {
            // ADR-M12-3의 물리적 근거: 미등록 축 = 0 = 중립.
            var traits = new[] { V(TraitId.Diligence, 100) };

            Assert.AreEqual(100, TraitVector.ValueOf(traits, TraitId.Diligence), "등록된 축은 그 값");
            Assert.AreEqual(0, TraitVector.ValueOf(traits, TraitId.Caution), "미등록 축은 0");
            Assert.AreEqual(0, TraitVector.ValueOf(null, TraitId.Diligence), "벡터 자체가 없어도 0");

            // 등록 안 된 축만 읽는 통로는 이 성격에 대해 완전 중립이어야 한다
            Assert.AreEqual(0f, TraitVector.Bias(traits, new[] { W(TraitId.Caution, 1f) }), 0.0001f,
                "가중치가 미등록 축만 보면 편향 0");
        }

        [Test]
        public void M12_T1_Bias_ClampedToUnitRange()
        {
            // 6축 전부 +100에 가중치 1.0이면 합이 6 — 클램프가 없으면 유도값이 6배로 터진다.
            var all = new[]
            {
                V(TraitId.Diligence, 100), V(TraitId.Foresight, 100), V(TraitId.Sociability, 100),
                V(TraitId.Wanderlust, 100), V(TraitId.Willfulness, 100), V(TraitId.Caution, 100),
            };
            var allW = new[]
            {
                W(TraitId.Diligence, 1f), W(TraitId.Foresight, 1f), W(TraitId.Sociability, 1f),
                W(TraitId.Wanderlust, 1f), W(TraitId.Willfulness, 1f), W(TraitId.Caution, 1f),
            };

            Assert.AreEqual(1f, TraitVector.Bias(all, allW), 0.0001f, "상한 +1");

            var allLow = new[]
            {
                V(TraitId.Diligence, -100), V(TraitId.Foresight, -100), V(TraitId.Sociability, -100),
                V(TraitId.Wanderlust, -100), V(TraitId.Willfulness, -100), V(TraitId.Caution, -100),
            };
            Assert.AreEqual(-1f, TraitVector.Bias(allLow, allW), 0.0001f, "하한 -1");

            // 단일 축 정규화: 값 100 × 가중치 1.0 = 정확히 1.0, 절반이면 절반
            Assert.AreEqual(1f, TraitVector.Bias(new[] { V(TraitId.Caution, 100) },
                                                 new[] { W(TraitId.Caution, 1f) }), 0.0001f);
            Assert.AreEqual(0.5f, TraitVector.Bias(new[] { V(TraitId.Caution, 50) },
                                                   new[] { W(TraitId.Caution, 1f) }), 0.0001f);
            Assert.AreEqual(-0.5f, TraitVector.Bias(new[] { V(TraitId.Caution, 100) },
                                                    new[] { W(TraitId.Caution, -0.5f) }), 0.0001f,
                "음수 가중치는 방향을 뒤집는다");
        }

        [Test]
        public void M12_T1_Threshold_ReturnsBaseWhenNeutral()
        {
            var traits = new[] { V(TraitId.Willfulness, 100) };

            var empty = new TraitBias { Weights = null, Sensitivity = 15f };
            Assert.AreEqual(30f, TraitVector.Threshold(traits, empty, 30f), 0.0001f,
                "가중치가 비면 base 그대로 — 소비처가 이 함수를 통과시켜도 현행 판정과 동일");

            var willful = new TraitBias { Weights = new[] { W(TraitId.Willfulness, 1f) }, Sensitivity = 15f };
            Assert.AreEqual(45f, TraitVector.Threshold(traits, willful, 30f), 0.0001f,
                "자존 +100 → base + 1.0 × 15");

            var sensZero = new TraitBias { Weights = new[] { W(TraitId.Willfulness, 1f) }, Sensitivity = 0f };
            Assert.AreEqual(30f, TraitVector.Threshold(traits, sensZero, 30f), 0.0001f,
                "민감도 0 = 그 소비처는 성향을 안 쓴다");
        }

        [Test]
        public void M12_T1_Meets_EmptyConditionAlwaysPassesAndIsMonotonic()
        {
            var lazy = new[] { V(TraitId.Foresight, -70) };
            var farmer = new[] { V(TraitId.Foresight, 80) };
            var need30 = new[] { new TraitCondition { Trait = TraitId.Foresight, MinValue = 30 } };

            Assert.IsTrue(TraitVector.Meets(lazy, null), "조건 없음 = 항상 성립 (현행 동작)");
            Assert.IsTrue(TraitVector.Meets(lazy, new TraitCondition[0]), "빈 조건 = 항상 성립");
            Assert.IsFalse(TraitVector.Meets(lazy, need30), "게으름뱅이(대비 -70)는 대비 30 조건에 미달");
            Assert.IsTrue(TraitVector.Meets(farmer, need30), "농사꾼(대비 80)은 성립");
            Assert.IsFalse(TraitVector.Meets(null, need30), "벡터 미배선은 0으로 취급 → 미달");
        }

        // ── M12-T2: 축 추가 내성 (성공 기준 S3) ──────────────────────────────

        [Test]
        public void M12_T2_AddingNewAxisLeavesExistingVectorsUnchanged()
        {
            // S3: 7번째 축이 생겨도 기존 에셋을 한 개도 고치지 않고 행동이 불변이어야 한다.
            // 새 축의 스탠드인 = 기존 벡터에 '등록되지 않은' 축 (여기서는 Caution을 그 역할로 쓴다).
            var existing = new[] { V(TraitId.Diligence, 60), V(TraitId.Foresight, -40) };

            var before = new[] { W(TraitId.Diligence, 0.5f), W(TraitId.Foresight, 0.5f) };
            var after = new[] { W(TraitId.Diligence, 0.5f), W(TraitId.Foresight, 0.5f), W(TraitId.Caution, 0.8f) };

            Assert.AreEqual(TraitVector.Bias(existing, before), TraitVector.Bias(existing, after), 0.0001f,
                "새 축을 읽는 통로가 생겨도, 그 축이 없는 기존 성격의 유도값은 불변 (ADR-M12-3)");

            var bias = new TraitBias { Weights = after, Sensitivity = 15f };
            Assert.AreEqual(TraitVector.Threshold(existing, new TraitBias { Weights = before, Sensitivity = 15f }, 30f),
                            TraitVector.Threshold(existing, bias, 30f), 0.0001f,
                "③문턱에서도 동일");
        }
    }
}
