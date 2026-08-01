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

        [Header("상호대화 연출 (M7-C — 제안치. 2026-07-17 사용자 피드백으로 전체 상향: 너무 빨라 읽기 힘듦)")]
        [Tooltip("임시 대사(혼잣말·거부·대화) 말풍선 노출 시간(초). 舊 하드코딩 2.5의 에셋 승격 (M8 후속).")]
        public float TransientLineSec = 4f;

        [Tooltip("응수 지연(초) — 발화 말풍선 뒤 '대화처럼' 보이는 간격 (ADR-M7-4).")]
        public float ReplyDelaySec = 2.5f;

        [Tooltip("대화 중 멈춰서 마주보는 시간(초) — 발화~응수 장면 전체(지연+노출)를 덮는 값. " +
                 "0이면 멈춤 없이 지나가며 대화 (M7 초기 동작). 2026-07-17 사용자 결정으로 도입.")]
        public float ChatPauseSec = 8f;

        [Tooltip("임시 대사(혼잣말·대화·거부) 만료 후 플랜 말풍선 복원까지의 침묵(초). " +
                 "0이면 만료 즉시 복원 (기존 동작). 대사 직후 '다음 행동' 문구가 바로 튀지 " +
                 "않게 하는 여운 — 제안치 3 (2026-07-18 사용자 지시).")]
        public float PlanResumeDelaySec = 3f;

        [Header("이름표 (M7-A — 전부 제안치)")]
        [Tooltip("머리 아래 오프셋 (월드 유닛, 음수 = 발밑)")]
        public float NameTagOffsetY = -0.55f;

        [Tooltip("이름표 WorldSpace TMP 폰트 크기")]
        public float NameTagFontSize = 1.6f;

        [Tooltip("비석 이름표 폰트 크기 (M13-A) — 주민 이름표와 별개로 조절. " +
                 "제안치 2.4 (주민 1.6의 1.5배 — Play 확인에서 '작아서 확대해야 보임' 피드백, 2026-07-30).")]
        public float GraveTagFontSize = 2.4f;

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

        [Header("성향 문턱 (M12-D — ③문턱. 전부 비면 현행 개별 필드 경로 = 중립)")]
        [Tooltip("배고픔 거부 문턱의 성향 편향. Sensitivity **+**면 문턱이 올라가 '더 쉽게 거부'.\n" +
                 "제안치: 자존 가중치 1.0 · Sensitivity 15 (현행 오프셋 폭 ±15에서 역산).")]
        public TraitBias RefuseSatietyBias;

        [Tooltip("피로 거부 문턱의 성향 편향. ⚠️ 배고픔과 **부호 규약이 반대**다 — 피로는 문턱이 " +
                 "**내려가야** '더 쉽게 거부'라 Sensitivity를 음수로 둔다(제안치 −15). " +
                 "같은 자존 가중치를 쓰면서 소비처가 자기 단위로 환산하는 것이 4작용형식의 요점이다.")]
        public TraitBias RefuseFatigueBias;

        [Tooltip("위협 감지 반경 배율의 성향 편향 (base 1.0에 더해진다). 제안치: 겁 1.0 · Sensitivity 0.4.\n" +
                 "⚠️ 이 값이 ThreatSO가 아니라 여기 사는 이유: 감지 예민함은 **주민의 성질**이지 위협의 " +
                 "성질이 아니고, ThreatSO에 두면 새 위협 에셋마다 이 필드를 채워야 해 O(위협)이 된다 " +
                 "(명세 M12-D 스케치에서 변경, 사유는 커밋에).")]
        public TraitBias FleeRadiusBias;

        [Tooltip("보상 선불을 요구하는 성향 편향. 제안치: 자존 1.0.")]
        public TraitBias UpfrontBias;

        [Range(0f, 1f)]
        [Tooltip("이 편향 이상이면 선불 요구 = true (불린 이산화, ADR-M1-2 결정적 판정 유지 — 랜덤 금지). " +
                 "제안치 0.5 = 자존 50 이상. 경계는 '이상'(>=)이다.")]
        public float UpfrontBiasThreshold = 0.5f;

        /// <summary>
        /// 선불 요구 판정의 단일 지점 (M12-D). 舊 개별 필드(DemandsRewardUpfront)와 성향 유도의 OR —
        /// M12-F에서 개별 필드를 끄면 성향만 남는다. 두 호출처(JudgeRequest·RequestService)가
        /// 각자 이산화하면 규칙이 이원화된다 (ADR-M0-3 정신).
        /// </summary>
        /// <summary>호환 진입점 (게이트·구형 호출 전용 — 벡터 = 성격 원본, 편차 없음. ⚠️W3-⑤).</summary>
        public bool DemandsUpfront(PersonalitySO p)
            => DemandsUpfront(p, p != null ? p.Traits : null);

        public bool DemandsUpfront(PersonalitySO p, TraitValue[] traits)
            => p != null
               && (p.DemandsRewardUpfront
                   // 벡터는 인자 (M14-W3 개체 편차 포함) — SO 직접 읽기는 반쪽 개체 (⚠️W3-⑤)
                   || TraitVector.Bias(traits, UpfrontBias.Weights) >= UpfrontBiasThreshold);

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

        [Tooltip("보상 떼먹기 기본 대사 (M8 보완) — 성격 StiffRewardLines가 비면 사용 (RefuseLines 패턴)")]
        public string[] StiffRewardLines =
        {
            "아 맞다, 보상... 다음에 줄게.",
            "보상은 나중에 얘기하자.",
        };

        [Tooltip("생애 첫 임금 대사 (M16-W2 — 1회만, 임금 시스템의 존재 알림. 이후는 로그·지갑 표기)")]
        public string[] FirstWageLines =
        {
            "첫 품삯이다!",
            "일한 값을 받았어.",
        };

        [Tooltip("중과세 불평 대사 (M17-W5 — 세율이 무거울 때 **임금을 받는 순간** 1회.\n" +
                 "⚠️ 명령 거부에 붙이지 않는다: 세율은 거부 판정에 들어가지 않으므로(웃돈은 " +
                 "비과세, ADR-M17-5) 거부에 세금 탓을 붙이면 거짓말이 된다. 실제로 떼인 자리에서만 말한다.\n" +
                 "세율 단계가 바뀔 때마다 주민당 1회 — 촌장이 세율을 올린 직후 순차로 터진다)")]
        public string[] TaxGrumbleLines =
        {
            "이만큼 떼가는데 뭐 하러 일하나.",
            "품삯이 반토막이군.",
            "촌장 곳간만 부르지.",
        };

        [Tooltip("물가 사유 웃돈 거부 대사 (M16-W3, 공유 시세 — 기준 물가면 수락했을 거부에만. " +
                 "배고픔·피로 대사를 대체한다 — 한 장면 한 말풍선)")]
        public string[] RefusePriceLines =
        {
            "요즘 돈 값이 예전 같지 않잖소.",
            "그 돈으론 어림없어요. 물가를 보세요.",
        };

        [Tooltip("정산 연기 기본 대사 (M16-B) — 부탁 에셋의 DeferLines가 비면 사용. " +
                 "빚은 남고 다음 마주침에 다시 시도한다 (소실 없음)")]
        public string[] DeferRewardLines =
        {
            "미안해... 아직 품삯이 다 안 모였어.",
            "조금만 기다려줘. 꼭 갚을게.",
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

        [Header("개인 인벤토리 (M11-A — 상한의 단일 출처. 전부 제안치, ADR-M11-3)")]
        [Tooltip("몸 소지 상한 (슬롯별 — 생식·조리식 각각). 플래너 전제 자동 주입과 EffectApplier " +
                 "선검사가 같은 값을 읽는다 (판정 단일). 제안치 8 ≈ 생식 4.8일치 — M9 잉여-안락 재발 방지. " +
                 "※ 명세 §4.2의 '합산'은 슬롯별로 개정 (플래너 전제 언어가 단일 슬롯 비교 — M11-A 커밋 사유).")]
        public int BodyCarryCap = 8;

        [Tooltip("집 저장 상한 (슬롯별). 제안치 15 — 몸+집 합계가 20을 넘지 않게 (잉여-안락 경계). " +
                 "주택 사다리(태크트리) 도입 시 BuildingSO 필드로 이관 예정.")]
        public int HomeStorageCap = 15;

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

        [Range(0f, 1f)]
        [Tooltip("[제안치 0.8, M12-G] '죽을 뻔했다'(MyWasStarved)로 기록할 지점 — 아사 문턱의 이 비율만큼 " +
                 "굶주림이 누적되면 표시가 남는다(영구). 비율인 이유: 아사는 문턱에 닿는 순간 즉시 " +
                 "발동하므로 '문턱을 밟고 살아남는다'가 불가능하다 — 죽기 직전까지 갔다 회복한 자만 " +
                 "골라내려면 문턱 **앞**의 지점이어야 한다. 1이면 아무도 기록되지 않고(전원 사망), " +
                 "0이면 굶주림 시작 즉시라 전원 참이 되어 희소성이 사라진다.")]
        public float NearStarvationRatio = 0.8f;

        [Tooltip("이탈 직전 마지막 대사 (랜덤 선택). ⚠️ 2026-07-24 현재 휴면 — 굶주림은 이탈이 아니라 " +
                 "아사로 결말이 바뀌었다(ADR-M10-3 개정, StarveLines 사용). 다른 이탈 사유가 생기면 재사용.")]
        public string[] DepartLines =
        {
            "더는 못 버티겠어… 미안해요.",
            "이 마을엔 겨울을 날 음식이 없어.",
        };

        [Tooltip("명령을 끝내 못 해내고 포기할 때의 대사 (랜덤 선택). 달성 불가 명령을 붙들고 " +
                 "무한 재계획하면 로그·CPU만 갉으므로, 주민이 못 하겠다고 말하고 자율로 돌아간다.")]
        public string[] OrderGiveUpLines =
        {
            "그건 지금 못 하겠어요…",
            "아무리 해도 방법이 없어.",
            "미안, 이건 무리야.",
        };

        [Tooltip("아사 직전 마지막 대사 (랜덤 선택 — ADR-M10-3 개정). 부상 사망(DieLines)과 분리: " +
                 "같은 죽음이라도 원인이 다르면 남기는 말이 다르다.")]
        public string[] StarveLines =
        {
            "배가… 고파…",
            "먹을 게… 조금만 더 있었어도…",
            "미안해… 더는 못 일어나겠어…",
        };

        [Header("부상 (M10-A — 최초의 사망 축. 전부 제안치, 명세 §4.4)")]
        [Tooltip("부상 중 이동 속도 배율 (<1 = 절뚝임). 도망·식사도 이 속도 — 다음 타격의 최우선 후보가 된다.")]
        public float InjuredMoveSpeedMult = 0.4f;

        [Tooltip("간호 누적이 이 기간(게임일)에 도달하면 완치. 자연 회복 없음 — 간호만 진행시킨다 (결정 11).")]
        public float InjuryRecoverDays = 1f;

        [Tooltip("미간호 누적이 이 기간(게임일)에 도달하면 사망. 간호 중엔 정지(홀드 — 리셋 아님). " +
                 "굶주림 0.5일보다 길게 — 발견→간호 판단의 유예 (명세 §4.5 검산).")]
        public float InjuryDeathAfterDays = 1.5f;

        [Tooltip("안정화(일반 주민 응급조치) 1회가 사망 시계를 멈추는 유예(게임일, M11-I). " +
                 "안정화만 받은 부상자는 이 기간이 지나면 악화 재개 → 치료사(Medic) 없으면 사망. " +
                 "1.0 + 사망 문턱 1.5 = 치료사 부재 시 ~2.5일 내 사망 (결정 15).")]
        public float StabilizeGraceDays = 1f;

        [Tooltip("부상 순간 대사 (랜덤 선택). 명령·부탁 거절(부상)도 재사용.")]
        public string[] InjuredLines =
        {
            "다리가… 움직일 수가 없어.",
            "물렸어… 누가 좀 봐줘.",
        };

        [Tooltip("사망 직전 마지막 대사 (랜덤 선택)")]
        public string[] DieLines =
        {
            "먼저 갈게… 마을을 부탁해.",
            "춥다… 미안…",
        };

        [Tooltip("간호 시작 대사 (랜덤 선택 — M10-B TendRunner가 사용)")]
        public string[] TendLines =
        {
            "금방 나을 거야. 곁에 있을게.",
            "상처 좀 보자. 가만히.",
        };
    }
}
