using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 주민 공통 설정. 게임 수치는 코드 상수가 아니라 이 에셋에만 둔다 (ADR-M0-2).
    /// 전부 舊 런타임 이관값 — 새 값 발명 금지.
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/AgentConfig", fileName = "AgentConfig")]
    public sealed class AgentConfigSO : ScriptableObject
    {
        [Header("이동")]
        [Tooltip("기본 이동 속도 (타일/초). 舊 VILLAGER_MOVE_SPEED 2.0 이관.")]
        public float BaseMoveSpeed = 2f;

        [Tooltip("개체별 속도 편차 비율 (W5 사용). 제안치 ±15% — 기존 값 없음, 관찰 후 튜닝.")]
        public float SpeedVariancePct = 0.15f;

        [Header("이동 표현 (W5 — 전부 제안치, 관찰 후 튜닝)")]
        [Tooltip("출발 가속 시간(초). 0이면 즉시 최고 속도.")]
        public float AccelSec = 0.25f;

        [Tooltip("목적지 근접 시 감속 배율 (1=감속 없음).")]
        public float DecelFactor = 0.45f;

        [Tooltip("감속을 시작하는 목적지까지의 거리(타일).")]
        public float DecelDistance = 0.6f;

        [Header("플랜 말풍선 (W6 — 전부 제안치)")]
        [Tooltip("머리 위 오프셋 (월드 유닛)")]
        public float BubbleOffsetY = 1.1f;

        [Tooltip("WorldSpace TMP 폰트 크기")]
        public float BubbleFontSize = 2.5f;

        [Tooltip("말풍선 랩핑 폭 (월드 유닛). 0 금지 — 버그C7 오버플로우 방어")]
        public float BubbleWidth = 6f;

        [Tooltip("현재 실행 중인 액션의 강조색")]
        public Color BubbleCurrentColor = new Color(1f, 0.83f, 0.31f, 1f);

        [Tooltip("명령 수행 중 말풍선 프리픽스 (TMP 리치 텍스트 허용)")]
        public string OrderBubblePrefix = "<color=#FF8A65>[명령]</color> ";

        [Header("상호대화 연출 (M7-C — 제안치)")]
        [Tooltip("응수 지연(초) — 발화 말풍선 뒤 '대화처럼' 보이는 간격 (ADR-M7-4).")]
        public float ReplyDelaySec = 1.2f;

        [Tooltip("대화 중 멈춰서 마주보는 시간(초) — 발화~응수 장면 전체(지연 1.2+노출 2.5)를 덮는 값. " +
                 "0이면 멈춤 없이 지나가며 대화 (M7 초기 동작). 2026-07-17 사용자 결정으로 도입.")]
        public float ChatPauseSec = 4f;

        [Header("이름표 (M7-A — 전부 제안치)")]
        [Tooltip("머리 아래 오프셋 (월드 유닛, 음수 = 발밑)")]
        public float NameTagOffsetY = -0.55f;

        [Tooltip("이름표 WorldSpace TMP 폰트 크기")]
        public float NameTagFontSize = 1.6f;

        [Header("플래닝")]
        [Tooltip("플랜 요청 타임아웃(초). 舊 PLANNING_TIMEOUT_SEC 3.0 이관.")]
        public float PlanningTimeoutSec = 3f;

        [Tooltip("실패한 goal의 재시도 대기(초). 실패 직후 같은 goal을 재선택하는 공회전을 막고 " +
                 "그동안 하위 goal(여가 등)로 내려가게 한다. 제안치 3.")]
        public float GoalRetryCooldownSec = 3f;

        [Header("명령/거부 (M1-C — 임계값·대사 전부 제안치)")]
        [Tooltip("포만감이 이 값 미만이면 명령 거부. P0 발동선(20)보다 여유 — '아직 안 쓰러졌지만 시키면 싫은' 구간.")]
        public float OrderRefuseSatiety = 35f;

        [Tooltip("피로가 이 값 초과면 명령 거부. P0 발동선(90)보다 여유.")]
        public float OrderRefuseFatigue = 70f;

        [Tooltip("배고픔 거부 대사 (랜덤 선택)")]
        public string[] RefuseHungryLines =
        {
            "지금은... 배가 너무 고파요.",
            "밥부터 먹고요. 진짜로요.",
        };

        [Tooltip("피로 거부 대사 (랜덤 선택)")]
        public string[] RefuseTiredLines =
        {
            "숨 좀 돌리고요. 너무 지쳤어요.",
            "조금만 쉬었다 하면 안 될까요...",
        };

        [Header("욕구 (초기값은 舊 VillagerBrain 기본값 이관)")]
        public float InitialSatiety = 70f;
        public float InitialFatigue = 20f;

        [Tooltip("초기 포만감 개인 편차(±). 전원 동일값 시작 → 동시 배고픔 웨이브를 흩는다. " +
                 "AgentId 해시 기반 결정적. 제안치 15.")]
        public float InitialSatietyVariance = 15f;

        [Tooltip("포만 감쇠율 개체 편차 비율(±). 감쇠율이 전원 동일하면 한 번 뭉친 허기 웨이브가 " +
                 "영구 지속된다 (포만 0 클램프·같은 문턱 식사가 동기화 장치). 제안치 0.1 (2026-07-17).")]
        public float SatietyDecayVariancePct = 0.1f;

        [Tooltip("게임 1일당 포만감 자연 감소량. 舊 코드에는 자연 감쇠가 없었음 — " +
                 "25는 2026-07-13 기획 승인값 (초기 70 기준 약 2일마다 식사).")]
        public float SatietyDecayPerGameDay = 25f;

        [Header("위기 예고 (M6-C — 제안치)")]
        [Tooltip("예고 기간 술렁임 확률 (액션 시작 시점마다 판정). 대사는 SeasonSO.ForecastLines.")]
        public float ForecastMoodChance = 0.15f;

        [Header("굶주림 이탈 (M6-D — 최초의 실패 상태. 제안치)")]
        [Tooltip("포만이 이 값 미만이면 '굶주림' 누적 시작. P0 발동선(20)보다 아래 — 음식이 있으면 " +
                 "P0가 먼저 먹으므로, 이 밑에 머문다는 건 그 개인이 식량 경쟁에서 계속 밀린다는 뜻. " +
                 "0이 아니라 문턱인 이유: 절벽(전멸 아니면 무사)이 아니라 계단(약한 개인부터)을 만들기 위해.")]
        public float StarvingBelowSatiety = 10f;

        [Tooltip("굶주림 상태가 이 기간(게임일) 누적되면 마을을 떠난다. " +
                 "겨울 비축 실패의 결과. 포만이 문턱 위로 회복되면 누적은 리셋.")]
        public float DepartAfterStarvingDays = 0.5f;

        [Tooltip("이탈 직전 마지막 대사 (랜덤 선택)")]
        public string[] DepartLines =
        {
            "더는 못 버티겠어… 미안해요.",
            "이 마을엔 겨울을 날 음식이 없어.",
        };
    }
}
