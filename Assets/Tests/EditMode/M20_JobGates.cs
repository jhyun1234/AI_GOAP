using AIVillage.Core;
using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M20 직업 게이트 — "부재가 느껴지는 장치"(ADR-M20-1) 헌장의 자동 감시.
    ///
    /// T1 중립 불변식: 아래 예외표에 없는 직업은 새 효율 배율이 전부 1이어야 한다.
    ///                 명시 없이 생긴 배율은 오염이다 (M5-S3·M19-W2 교훈).
    /// T2 전문가 값: 승격 4종이 실제로 남보다 빠른가 (부재 시나리오의 근거).
    /// T3 식사 불가침: 조리 플래그가 켜진 액션은 조리 2종뿐 (ADR-M5-3 몸값 불가침).
    /// T4 배율 조회: 자원별 배열의 중립·분기 동작.
    /// </summary>
    public class M20_JobGates
    {
        /// <summary>효율 배율이 허용된 직업과 그 값 — 이 게이트의 판정 단일 출처.
        /// 승격을 추가하려면 명세 개정 사유와 함께 여기부터 고친다 (조용한 추가 차단).</summary>
        private struct JobExpect
        {
            public string Name;
            public float Farm;
            public float Cook;
            public ResourceType GatherRes;
            public float GatherMult;   // 0 = 채집 배율 없음

            public JobExpect(string name, float farm, float cook, ResourceType res, float gather)
            {
                Name = name; Farm = farm; Cook = cook; GatherRes = res; GatherMult = gather;
            }
        }

        // W1 시점 = 빈 목록 (에셋은 전부 중립 — 배율 추가는 W3에서).
        private static readonly JobExpect[] Expected = { };

        private static bool TryExpect(string jobName, out JobExpect expect)
        {
            foreach (JobExpect e in Expected)
                if (e.Name == jobName) { expect = e; return true; }
            expect = default;
            return false;
        }

        // ── T1: 중립 불변식 — 예외표 밖은 전부 1 ────────────────────────────

        [Test]
        public void M20_T1_DurationMults_NeutralExceptListed()
        {
            var fresh = ScriptableObject.CreateInstance<JobSO>();
            Assert.AreEqual(1f, fresh.FarmDurationMult, "신규 JobSO 밭일 기본 = 1 (중립)");
            Assert.AreEqual(1f, fresh.CookDurationMult, "신규 JobSO 조리 기본 = 1 (중립)");
            Object.DestroyImmediate(fresh);

            foreach (string guid in AssetDatabase.FindAssets("t:JobSO", new[] { "Assets/M0Config/Jobs" }))
            {
                var job = AssetDatabase.LoadAssetAtPath<JobSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (job == null) continue;
                if (TryExpect(job.name, out _)) continue; // 값 검사는 T2 담당

                Assert.AreEqual(1f, job.FarmDurationMult,
                    $"{job.name}: 예외표에 없는 직업의 밭일 배율은 1 (명시 없는 배율 = 오염)");
                Assert.AreEqual(1f, job.CookDurationMult,
                    $"{job.name}: 예외표에 없는 직업의 조리 배율은 1");
                Assert.IsTrue(job.GatherDurationMults == null || job.GatherDurationMults.Length == 0,
                    $"{job.name}: 예외표에 없는 직업은 자원별 채집 배율을 갖지 않는다");
            }
        }

        // ── T2: 전문가 값 — 승격 직업이 실제로 빠른가 ────────────────────────

        [Test]
        public void M20_T2_SpecialistsAreFaster()
        {
            foreach (JobExpect e in Expected)
            {
                var job = AssetDatabase.LoadAssetAtPath<JobSO>($"Assets/M0Config/Jobs/{e.Name}.asset");
                Assert.IsNotNull(job, $"{e.Name} 에셋 존재");
                Assert.AreEqual(e.Farm, job.FarmDurationMult, $"{e.Name} 밭일 배율");
                Assert.AreEqual(e.Cook, job.CookDurationMult, $"{e.Name} 조리 배율");

                if (e.GatherMult <= 0f) continue;
                Assert.AreEqual(e.GatherMult, job.GatherDurationMultFor(e.GatherRes),
                    $"{e.Name}: {e.GatherRes} 채집 보너스");

                // 자원별 구분이 두 직업(나무꾼·광부)을 가르는 유일한 선 — 남의 자원은 중립이어야 한다
                ResourceType other = e.GatherRes == ResourceType.Wood ? ResourceType.Stone : ResourceType.Wood;
                Assert.AreEqual(1f, job.GatherDurationMultFor(other),
                    $"{e.Name}: {other}는 전문 밖이므로 1 — 한 직업이 모든 채집을 잘하면 구분이 사라진다");
            }
        }

        // ── T3: 식사 불가침 (W3에서 추가 — ConsumeActionSO.IsCookingWork 신설 후) ──

        // ── T4: 자원별 배율 조회 ─────────────────────────────────────────────

        [Test]
        public void M20_T4_GatherMultLookup_NeutralAndBranching()
        {
            var job = ScriptableObject.CreateInstance<JobSO>();

            // 빈 배열 = 전 자원 중립
            Assert.AreEqual(1f, job.GatherDurationMultFor(ResourceType.Wood), "미정의 자원 = 1 (중립)");

            job.GatherDurationMults = new[]
            {
                new GatherDurationMult { Resource = ResourceType.Wood, Mult = 0.5f }
            };
            Assert.AreEqual(0.5f, job.GatherDurationMultFor(ResourceType.Wood), "정의된 자원 = 그 값");
            Assert.AreEqual(1f, job.GatherDurationMultFor(ResourceType.Stone), "미정의 자원은 여전히 1");
            Assert.AreEqual(1f, job.GatherDurationMultFor(ResourceType.Iron),
                "새 자원(격퇴 축 재료)이 생겨도 기존 직업은 자동 중립");

            Object.DestroyImmediate(job);
        }
    }
}
