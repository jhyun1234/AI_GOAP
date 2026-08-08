using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M22 방어 건설 게이트 (Docs/M22_방어건설_실행명세서.md). 이 파일이 M22-T의 집이다 —
    /// W1(개체별 통행 규칙, ADR-M22-1)부터 시작해 W2(데이터 층)·W5(공성·내구도)가 뒤에 추가된다.
    /// </summary>
    public class M22_DefenseGates
    {
        [Test]
        public void M22_T1_PassageRules_ShippedBuildings()
        {
            // ADR-M22-1: 개체별 통행 규칙은 순수 함수 한 쌍이 단일 소유한다.
            // 문(BlocksThreatMovement)이 없는 W1 세계에서 배포 에셋의 두 답은 항상 같다 —
            // 집만 양쪽 차단, 나머지는 양쪽 통행. (여기가 갈라지는 첫 에셋 = W2의 Gate)
            var house = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/House.asset");
            Assert.IsNotNull(house, "House 에셋 없음");
            Assert.IsTrue(M0SimulationLoop.BlocksVillagerPassage(house), "집은 주민 통행 차단 (ADR-M3-3 계승)");
            Assert.IsTrue(M0SimulationLoop.BlocksThreatPassage(house), "집은 위협 통행도 차단");

            foreach (string name in new[] { "Campfire", "FarmPlot", "Irrigation" })
            {
                var b = AssetDatabase.LoadAssetAtPath<BuildingSO>($"Assets/M0Config/Buildings/{name}.asset");
                Assert.IsNotNull(b, $"{name} 에셋 없음");
                Assert.IsFalse(M0SimulationLoop.BlocksVillagerPassage(b), $"{name}은(는) 주민 통행 가능 유지");
                Assert.IsFalse(M0SimulationLoop.BlocksThreatPassage(b), $"{name}은(는) 위협 통행 가능 유지");
            }
        }

        [Test]
        public void M22_T1b_PassageRules_DefaultBuildingBlocksNeither()
        {
            // 새 건물 에셋의 기본값은 "아무도 안 막음" — 플래그를 켜기 전에는 통행이 바뀌지 않는다.
            var b = ScriptableObject.CreateInstance<BuildingSO>();
            Assert.IsFalse(M0SimulationLoop.BlocksVillagerPassage(b), "기본값은 주민 통행 가능");
            Assert.IsFalse(M0SimulationLoop.BlocksThreatPassage(b), "기본값은 위협 통행 가능");
        }
    }
}
