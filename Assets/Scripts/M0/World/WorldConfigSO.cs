using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 월드 공통 설정. 전부 舊 런타임 이관값 (ADR-M0-2 — 발명 금지).
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/WorldConfig", fileName = "WorldConfig")]
    public sealed class WorldConfigSO : ScriptableObject
    {
        [Header("초기 자원 (GDD v0.4)")]
        public int InitialWoodStock = 10;
        public int InitialRawFoodStock = 30;
        public int InitialStoneStock = 5;

        [Header("시간")]
        [Tooltip("게임 시간 배율. 0.01 → 0.1초 틱당 0.001게임일, 1게임일 = 100초 (舊 GameManager._gameTimeScale)")]
        public float GameTimeScale = 0.01f;

        [Header("기지")]
        [Tooltip("기지 타일 좌표 (舊 (0,0))")]
        public int BaseTileX = 0;
        public int BaseTileY = 0;

        [Tooltip("게임 시작 시 기지 주변 자동 발견 반경 (舊 _baseDiscoverRadius 12)")]
        public int BaseDiscoverRadius = 12;

        [Header("농사 (M2)")]
        [Tooltip("작물 성장 기간 (게임일). 제안치 1.5 (M2 §4) — 방치 시 사이클이 눈에 2~3회 보이는 속도.")]
        public float FarmGrowthDays = 1.5f;

        [Header("계절 (M6 — ADR-M6-1: 계절이 게임에 개입하는 통로는 배율 3종뿐)")]
        [Tooltip("계절 사이클 (배열 순서로 순환). 비면 계절 없음 — M5 동작과 완전 동일 (중립).")]
        public SeasonSO[] SeasonCycle;

        [Tooltip("위기 계절 예고 시작 (위기 N일 전부터 HUD 경보·주민 술렁임). 제안치 3 (M6 §4).")]
        public float ForecastDays = 3f;

        [Header("재해 (M9-C — ADR-M9-4: 재해 목록의 집. 계절 참조로 발동)")]
        [Tooltip("재해 에셋 목록 (DisasterSO). 비면 재해 없음 — M8 동작과 완전 동일 (중립 불변식).")]
        public DisasterSO[] Disasters;

        [Header("상호대화 (M7 — ADR-M7-6: 대화 목록의 집. 전부 제안치)")]
        [Tooltip("대화 상황 에셋 목록. 비면 상호대화 없음 (중립 — M6 동작).")]
        public ChatterSO[] Chatters;

        [Tooltip("짝 스캔 주기(초, 실시간) — 단일 지점 주기 틱 (ADR-M7-3). 제안치 5.")]
        public float ChatterIntervalSec = 5f;

        [Tooltip("주기당 발화 확률 (성립 쌍 존재 시). 대화는 희소해야 장면이 된다. 제안치 0.5.")]
        public float ChatterChance = 0.5f;

        [Tooltip("발화 후 개인 쿨다운(초, 실시간) — 화자·상대 공용. 동일 화자 연속 스팸 방지. 제안치 30.")]
        public float ChatterCooldownSec = 30f;

        [Header("관계 (M8 — 문턱·주기는 전부 여기, ADR-M8-6. 전부 제안치)")]
        [Tooltip("단짝 문턱 — 쌍방 친밀도가 이 이상이면 단짝 (정보줄 표기). 제안치 20.")]
        public int BuddyThreshold = 20;

        [Tooltip("원한 문턱 — 친밀도가 이 미만이면 원한 (정보줄 표기·거절 판정 기준선). 제안치 -20.")]
        public int GrudgeThreshold = -20;

        [Header("부탁 (M8-D — ADR-M8-7: 부탁 목록의 집. 전부 제안치)")]
        [Tooltip("부탁 상황 에셋 목록. 비면 부탁 없음 (중립 — M7 동작).")]
        public RequestSO[] Requests;

        [Tooltip("부탁 짝 스캔 주기(초, 실시간) — 단일 지점 주기 틱, 한 주기 1건. 제안치 5.")]
        public float RequestIntervalSec = 5f;

        [Tooltip("의뢰인 개인 쿨다운(초, 실시간) — 거절당하면 한동안 다시 조르지 않는다. 제안치 90.")]
        public float RequestCooldownSec = 90f;

        [Tooltip("보상 정산 반경(타일, 맨해튼) — 조각 Y: 목수와 의뢰인이 이 안에서 마주치면 보상 지급. " +
                 "쫓아가지 않고 자연스러운 근접에서 정산. 제안치 3~4.")]
        public int RewardSettleRadiusTiles = 3;

        // [DEPRECATED 2026-07-18] 조각 Y로 보고 심부름(쫓아가기) → 마주치면 정산으로 교체됨.
        // 아래 둘은 더 이상 참조되지 않는다(휴면). 관련 에셋(ReportErrand goal·VisitAction) 정리는
        // 후속 작업 — Docs/퀘스트보드_및_보고심부름정리_후속.md 참조.
        [Tooltip("[DEPRECATED — 조각 Y로 대체, 미사용] 완공 보고 심부름 goal.")]
        public GoalSO ReportErrandGoal;

        [Tooltip("[DEPRECATED — 조각 Y로 대체, 미사용] 보고 심부름 마감 초.")]
        public float ReportTimeoutSec = 60f;
    }
}
