using System.Collections.Generic;
using AIVillage.M0;
using NUnit.Framework;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M10-E 방랑자 게이트 (M10-T5) — 런타임 인구 문의 3대 보장:
    /// 제안 해소 1회성(스폰 1회), 이름 발급 결정성·비충돌, 주입 우선(UI가 보여준 그 사람).
    /// </summary>
    public class M10_WandererGates
    {
        [Test]
        public void M10_T5_WandererOffer_ResolveOnceAndTimeout()
        {
            // 이중 해소 방어 — 두 번째 Y는 무시 (스폰 1회 보장)
            WandererOffer offer = WandererOffer.Open(gameTimeDay: 10f, waitDays: 0.7f);
            Assert.IsTrue(offer.Pending, "제안 열림");
            Assert.IsTrue(offer.TryResolve(), "첫 해소 유효");
            Assert.IsFalse(offer.TryResolve(), "이중 해소 무시");
            Assert.IsFalse(offer.Pending, "해소 후 닫힘");

            // 미제안 상태의 해소 — 무효 (프롬프트 없는데 Y 연타)
            var idle = new WandererOffer();
            Assert.IsFalse(idle.TryResolve(), "제안 없음 = 해소 무효");

            // 시간 초과 경계
            WandererOffer wait = WandererOffer.Open(10f, 0.7f);
            Assert.IsFalse(wait.TimedOut(10.69f), "시한 직전");
            Assert.IsTrue(wait.TimedOut(10.7f), "시한 도달 = 자동 퇴장");
            wait.TryResolve();
            Assert.IsFalse(wait.TimedOut(99f), "해소 후엔 시간 초과 없음");
        }

        [Test]
        public void M10_T5_NextSpawnName_DeterministicAndCollisionFree()
        {
            var used = new HashSet<string> { "M0_Villager_A", "M0_Villager_B", "M0_Villager_C", "M0_Villager_D" };
            Assert.AreEqual("M0_Villager_E", M0SimulationLoop.NextSpawnName(used), "기존 4명 다음 = E");
            Assert.AreEqual("M0_Villager_E", M0SimulationLoop.NextSpawnName(used), "같은 집합 = 같은 이름 (결정적 — StableHash 시드)");

            used.Add("M0_Villager_E");
            Assert.AreEqual("M0_Villager_F", M0SimulationLoop.NextSpawnName(used), "발급 후 다음 이름");

            // 구멍 재사용 — 이탈로 빈 B는 사용 집합에 남아 있는 한 재발급되지 않는다 (호출자가 유지)
            var holed = new HashSet<string> { "M0_Villager_A", "M0_Villager_C" };
            Assert.AreEqual("M0_Villager_B", M0SimulationLoop.NextSpawnName(holed), "집합에 없으면 가장 이른 이름");

            // Z 다음 AA — 일련 규칙의 자릿수 확장
            var full = new HashSet<string>();
            for (char c = 'A'; c <= 'Z'; c++) full.Add("M0_Villager_" + c);
            Assert.AreEqual("M0_Villager_AA", M0SimulationLoop.NextSpawnName(full), "Z 소진 = AA");
        }

        [Test]
        public void M10_T5_PresetOrRandom_InjectionBeatsRandom()
        {
            var p = ScriptableObject.CreateInstance<PersonalitySO>();
            var q = ScriptableObject.CreateInstance<PersonalitySO>();

            // 주입 시 — 랜덤 미호출·주입값 그대로 (UI가 보여준 그 사람, ⚠️①)
            int randomCalls = 0;
            PersonalitySO RandomPick() { randomCalls++; return q; }
            Assert.AreSame(p, VillagerAgent.PresetOrRandom(true, p, RandomPick), "주입값 우선");
            Assert.AreEqual(0, randomCalls, "주입 시 랜덤 미호출");

            // 주입값 null도 유효 (중립 성격 방랑자) — 랜덤으로 대체되면 안 된다
            Assert.IsNull(VillagerAgent.PresetOrRandom<PersonalitySO>(true, null, RandomPick), "null 주입 = 중립 유지");
            Assert.AreEqual(0, randomCalls);

            // 미주입 — 기존 랜덤 경로 (중립 불변식: 씬 배치 주민 동작 불변)
            Assert.AreSame(q, VillagerAgent.PresetOrRandom(false, p, RandomPick), "미주입 = 랜덤 경로");
            Assert.AreEqual(1, randomCalls);

            Object.DestroyImmediate(p);
            Object.DestroyImmediate(q);
        }
    }
}
