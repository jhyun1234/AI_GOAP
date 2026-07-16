using AIVillage.M0;
using NUnit.Framework;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M6-D 굶주림 이탈 게이트 (M6-T3) — 최초의 실패 상태.
    /// 판정은 순수 함수 2종(누적·문턱)뿐이다: 대기 가드 없음 (M4 교훈 — 동상·데드락 금지).
    /// </summary>
    public class M6_DepartGates
    {
        [Test]
        public void M6_T3_NextStarvingDays_AccumulatesAndResets()
        {
            // 포만 0 = 누적 (경계 0 포함 — 감쇠가 0 클램프라 정확히 0에 머문다)
            Assert.AreEqual(0.1f, VillagerAgent.NextStarvingDays(0f, 0f, 0.1f), 1e-5f, "굶주림 시작");
            Assert.AreEqual(0.4f, VillagerAgent.NextStarvingDays(0.3f, 0f, 0.1f), 1e-5f, "누적");

            // 포만이 조금이라도 회복되면 리셋 — "한 끼"가 카운트다운을 되돌린다
            Assert.AreEqual(0f, VillagerAgent.NextStarvingDays(0.45f, 5f, 0.1f), "회복 = 리셋");
            Assert.AreEqual(0f, VillagerAgent.NextStarvingDays(0.45f, 0.01f, 0.1f), "미세 회복도 리셋");
        }

        [Test]
        public void M6_T3_ShouldDepart_ThresholdBoundary()
        {
            var cfg = ScriptableObject.CreateInstance<AgentConfigSO>();
            cfg.DepartAfterStarvingDays = 0.5f;

            Assert.IsFalse(VillagerAgent.ShouldDepart(0f, cfg), "굶주림 없음");
            Assert.IsFalse(VillagerAgent.ShouldDepart(0.49f, cfg), "문턱 직전");
            Assert.IsTrue(VillagerAgent.ShouldDepart(0.5f, cfg), "문턱 도달 = 이탈");
            Assert.IsTrue(VillagerAgent.ShouldDepart(2f, cfg), "초과분도 이탈");

            Object.DestroyImmediate(cfg);
        }
    }
}
