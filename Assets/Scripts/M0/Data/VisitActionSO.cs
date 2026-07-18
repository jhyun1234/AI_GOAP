using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// [DEPRECATED 2026-07-18 — 조각 Y] 완공 보고가 "쫓아가기"에서 "마주치면 정산"으로 바뀌며 휴면.
    /// 더 이상 주입되지 않는다(RequestService가 보고 심부름을 안 만든다). 에셋 정리는 후속 작업
    /// (Docs/퀘스트보드_및_보고심부름정리_후속.md). "주민에게 걸어가는" 재사용 가능 primitive라 보존.
    ///
    /// 주민 방문 계열 (M8 후속 — 완공 보고): 지정 주민(VisitTargetAgentId) 곁으로 걸어간다.
    /// 세계 상태를 일절 바꾸지 않는 표현 액션 (여가와 동일 성질) — 도착 통지(CompleteVisit)로
    /// 장면 재생은 RequestService가 담당한다. 대상 지정은 ID 문자열 (에이전트=주민 전제 금지).
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Action/Visit", fileName = "VisitAction")]
    public sealed class VisitActionSO : ActionSO
    {
        [Tooltip("도착 인정 반경 (타일, 맨해튼) — 이 안이면 걷지 않고 즉시 도착. 대화 성립 거리와 맞춘다.")]
        public int ArriveRadiusTiles = 2;

        public override IActionRunner CreateRunner(VillagerAgent agent) => new VisitRunner(this);
    }
}
