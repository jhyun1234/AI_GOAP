using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 도망 계열 액션 (M10-D). 피신처(내 집 → 모닥불, 없으면 M11-G 방향 도피)로 이동하면 완료 —
    /// 효과의 ThreatNear Set 0은 플래너 픽션이다 (실제 해소 = 위협 퇴장·거리 이탈,
    /// EffectApplier는 파생 슬롯 무시). 위협이 아직 근처면 재평가가 다시 도망을 선택한다.
    /// ⚠️ BaseCost는 5 이상 유지 — 카탈로그 최소 비용이 잡 휴리스틱의 눈금이다
    /// (2026-07-22 탐색 폭발 교훈, 게이트 M10-T2가 감시).
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Action/Flee", fileName = "FleeAction")]
    public sealed class FleeActionSO : ActionSO
    {
        [Tooltip("피신처 건물 우선순위 (ADR-M2-2 앵커 패턴) — 내 소유 우선(ResolveAnchor). " +
                 "전부 미완공이면 방향 도피 (M11-G).")]
        public SlotId[] AnchorPriority = { SlotId.HouseCount, SlotId.CampfireCount };

        [Tooltip("피신 위치 산개 반경(타일) — 같은 타일 몰림·예약 충돌 방지 (식사 앵커 패턴).")]
        public int AnchorRadius = 2;

        [Tooltip("노숙자 방향 도피 거리(타일, M11-G) — 소유 집이 없으면 위협 반대 방향 이 거리로 " +
                 "달아난다. 기지 폴백은 없다 (노숙 = 위험이 결정 6·7의 전제).")]
        public int FleeDistanceTiles = 6;

        public override IActionRunner CreateRunner(VillagerAgent agent) => new FleeRunner(this);
    }
}
