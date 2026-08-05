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

        /// <summary>아사 문턱 (舊 이탈 — ADR-M10-3 개정 2026-07-24: 굶주림의 결말이 이탈에서
        /// 아사로 바뀌었다. M21-W1 재보정: 판정이 누적일 문턱에서 **체력 0**으로 옮겨졌으므로
        /// 여기서 지키는 것은 "문턱 함수"가 아니라 **아사 소요가 에셋 값 그대로인가**다.
        /// 舊 ShouldStarveToDeath를 계속 검사하면 아무도 안 쓰는 함수를 지키게 된다
        /// (값 기반 게이트 역인덱스 — M20 교훈).</summary>
        [Test]
        public void M6_T3_StarveDeath_TimingPreserved()
        {
            var cfg = ScriptableObject.CreateInstance<AgentConfigSO>();
            cfg.DepartAfterStarvingDays = 0.5f;
            cfg.MaxHp = 100f;
            cfg.StarveHpLossPerDay = 0f; // 0 = 자동 환산

            Assert.AreEqual(0.5f, VillagerAgent.DaysToStarveDeath(cfg), 1e-4f,
                "만복에서 굶기만 하면 에셋의 아사 소요(0.5일)와 정확히 같아야 한다 — 이 등식이 " +
                "깨지면 체력 도입이 검증된 밸런스를 조용히 바꾼 것이다");

            // 감쇠를 직접 지정하면 그 값이 이긴다 (자동 환산은 미지정일 때만)
            cfg.StarveHpLossPerDay = 50f;
            Assert.AreEqual(2f, VillagerAgent.DaysToStarveDeath(cfg), 1e-4f, "명시값 우선");

            // 사망 판정의 문턱은 0 하나 — 원인 무관 (M21-W1)
            Assert.IsFalse(VillagerAgent.IsDead(0.01f), "체력이 남아 있으면 산다");
            Assert.IsTrue(VillagerAgent.IsDead(0f), "체력 0 = 사망");

            // 아사 대사는 부상 사망(DieLines)과 분리 — 원인이 다르면 남기는 말도 다르다
            Assert.IsNotNull(cfg.StarveLines);
            Assert.Greater(cfg.StarveLines.Length, 0, "아사 대사 기본값 존재");

            Object.DestroyImmediate(cfg);
        }
    }
}
