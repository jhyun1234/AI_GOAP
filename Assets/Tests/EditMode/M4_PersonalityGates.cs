using AIVillage.M0;
using NUnit.Framework;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M4 성격 게이트 (명세 M4-A~F). 이 파일이 M4-T1~T3의 집이다 —
    /// M4-E(랜덤 목표 Walkable 필터)를 시작으로 A(스키마)·B(중립 불변식·선호 플랜)·
    /// C(거부 오프셋) 게이트가 뒤에 추가된다.
    /// </summary>
    public class M4_PersonalityGates
    {
        [Test]
        public void M4_E_PickWalkableNear_FiltersBlockedTiles()
        {
            // 반경 내 유일한 통행 가능 타일 — 랜덤이 불운해도 링 폴백이 반드시 찾는다 (결정적 성질)
            var only = new Vector2Int(12, 10);
            bool OnlyOne(int x, int y) => x == only.x && y == only.y;
            Assert.AreEqual(only, MapBounds.PickWalkableNear(OnlyOne, 10, 10, 2),
                            "반경 내 walkable이 존재하면 반드시 반환");

            // 전부 통행 불가 → 중심 클램프 (결정적 종료 — 이후는 이동 실패 first-class 처리)
            Assert.AreEqual(new Vector2Int(10, 10), MapBounds.PickWalkableNear((x, y) => false, 10, 10, 2),
                            "전부 막히면 중심 폴백 (무한 재추첨 없음)");

            // 전부 통행 가능 → 반경·경계 내 반환
            Vector2Int t = MapBounds.PickWalkableNear((x, y) => true, 0, 0, 3);
            Assert.LessOrEqual(Mathf.Max(Mathf.Abs(t.x), Mathf.Abs(t.y)), 3, "반경 내");

            // 필터 없음(null) = 기존 랜덤 동작 (중립 경로)
            Vector2Int u = MapBounds.PickWalkableNear(null, 0, 0, 2);
            Assert.LessOrEqual(Mathf.Max(Mathf.Abs(u.x), Mathf.Abs(u.y)), 2);
        }
    }
}
