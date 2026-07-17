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
            // 문턱(10) 미만 = 누적 — 절벽(포만 정확히 0)이 아니라 계단 (2026-07-17 관측 대응:
            // P0가 20에서 먹으므로 10 밑 지속 = 그 개인이 식량 경쟁에서 계속 밀린다는 뜻)
            const float BELOW = 10f;
            Assert.AreEqual(0.1f, VillagerAgent.NextStarvingDays(0f, 0f, BELOW, 0.1f), 1e-5f, "완전 기아 누적");
            Assert.AreEqual(0.4f, VillagerAgent.NextStarvingDays(0.3f, 9.9f, BELOW, 0.1f), 1e-5f, "문턱 바로 아래도 누적");

            // 문턱 위로 회복되면 리셋 — "한 끼"가 카운트다운을 되돌린다
            Assert.AreEqual(0f, VillagerAgent.NextStarvingDays(0.45f, 10f, BELOW, 0.1f), "문턱 경계 = 리셋 (미만만 굶주림)");
            Assert.AreEqual(0f, VillagerAgent.NextStarvingDays(0.45f, 50f, BELOW, 0.1f), "회복 = 리셋");
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
