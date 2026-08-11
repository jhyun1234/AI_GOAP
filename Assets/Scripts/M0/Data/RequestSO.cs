using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 주민→주민 부탁 정의 (M8-D) — 의뢰인 조건·대상 직업·주입 goal·판정 문턱·대사가 전부
    /// 필드라 새 부탁 종류 추가 = 에셋 1개 (.cs 0, M8-S6). 판정은 결정적 (ADR-M8-2 — 랜덤 금지),
    /// 대상 지정은 JobSO 참조 매핑 (이름 문자열 분기 금지, ADR-M0-1).
    /// 수행에 에스크로·선검사 없음 — 재료는 수락자의 플랜이 조달한다 (GOAP의 일, 명세 ⚠️②).
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Request", fileName = "Request")]
    public sealed class RequestSO : ScriptableObject
    {
        [Tooltip("상황 표시명 (로그용, 예: 집 부탁)")]
        public string DisplayName;

        [Tooltip("정보줄 할 일 표기 (예: 집 지어주기 → 'A의 집 지어주기'). 비면 DisplayName 사용")]
        public string TaskLabel;

        /// <summary>정보줄 라벨 — TaskLabel 우선, 비면 DisplayName (중립 경로).</summary>
        public string TaskLabelOrDefault
            => string.IsNullOrEmpty(TaskLabel) ? DisplayName : TaskLabel;

        [Tooltip("의뢰인 성립 조건 (스냅샷 대역 — 예: MyHasHome == 0). 비면 항상 성립 (남용 주의)")]
        public SlotCondition[] RequesterConditions;

        [Header("의뢰인 기질 (M12-G — 스톡만 보던 판정에 성향을 들인다)")]
        [Tooltip("의뢰인 성향 조건 (예: Foresight ≥ 30). 비면 성향 무관 = 현행 동작(중립 불변식).\n" +
                 "⚠️ 이것이 성격 페널티 우회 구조의 차단점이다 — GoalBoosts는 goal 순위에만 걸리는데 " +
                 "부탁 성립은 스톡만 읽어서, 게으름뱅이가 여력만 생기면 남들과 똑같이 집을 부탁했다.\n" +
                 "⚠️ GoalSO.TriggerConditions에는 절대 쓰지 않는다 (ADR-M12-2 — 무한 루프).")]
        public TraitCondition[] RequesterTraits;

        [Tooltip("이 조건이 전부 성립하면 RequesterTraits를 **우회**한다 (OR). 예: MyWasStarved == 1 —\n" +
                 "굶어 죽을 뻔한 자는 대비성이 낮아도 집을 원한다. 기질이 하지 않을 선택을 경험이 " +
                 "하게 만드는 지점이라, 성향 문턱이 '영원히 못 하는 사람'을 만들지 않게 하는 안전판이다.\n" +
                 "비면 우회 없음 = 성향 조건이 그대로 최종 판정.")]
        public SlotCondition[] TraitBypassConditions;

        [Tooltip("부탁 대상 직업 (참조 매핑). 비면 아무나 — 능력 조건 없는 부탁")]
        public JobSO TargetJob;

        [Tooltip("대상(수행자/판매자) 성립 조건 (M16-W5 — 스냅샷 대역, 예: MyFoodDaysLeft ≥ 3). " +
                 "비면 무조건 (중립 — 기존 부탁과 동일). 직업 필터(TargetJob)와 AND.")]
        public SlotCondition[] TargetConditions;

        [Header("즉시 교환 (M16-W5 — 직거래: 수락 순간 실물↔돈 쌍방 이전. 완수·빚·정산 없음)")]
        [Tooltip(">0이면 즉시 교환 부탁: 대상이 이 수량의 실물(TradeGiveSlot)을 의뢰인에게 넘기고 " +
                 "값(RewardCostSlot·RewardCostAmount = 기준가)을 받는다. InjectGoal 불필요.\n" +
                 "실가격 = 기준가 × 물가% (코드가 스케일 — 조건 Value는 필터일 뿐, 명세 확정 보완 2)")]
        public int TradeGiveAmount;

        [Tooltip("대상이 넘길 실물 슬롯 (개인 스톡만 — 예: MyRawFood)")]
        public SlotId TradeGiveSlot = SlotId.MyRawFood;

        // TradeInflatedLines(M16-W5 물가 불평 대사)는 2026-08-11 삭제 — 화폐 철거(M19)로
        // 발화 조건("물가 > 100%")이 소멸, 판독자 0건이었다.

        [Tooltip("수락 시 대상의 개인 사다리에 주입할 goal — RelativeToCurrent 재사용 (M1-C 사본 규약)")]
        public GoalSO InjectGoal;

        [Tooltip("성립 반경 (타일, 맨해튼 거리). 제안치 6 — 마을이 좁아 근접 성립만 (명세 ⚠️④)")]
        public int RadiusTiles = 6;

        [Tooltip("대상→의뢰인 친밀이 이 미만이면 거절 (원한이 결과를 만드는 지점). 제안치 -10")]
        public int RefuseAffinityBelow = -10;

        [Header("소유 배정 (완수 시 — M8-C 연동)")]
        [Tooltip("true면 완수 시 부탁자에게 OwnershipSlot의 최근접 무주 건물을 배정. " +
                 "false가 정상 경로인 부탁도 많다 (요리 부탁은 집을 주지 않는다)")]
        public bool GrantOwnership;

        [Tooltip("배정할 수량형 건물 슬롯 (예: HouseCount). GrantOwnership일 때만 사용")]
        public SlotId OwnershipSlot;

        [Header("관계 델타 (전부 제안치 — ADR-M8-6)")]
        [Tooltip("수락 시 의뢰인→대상 (고마움)")]
        public int AcceptDelta = 5;

        [Tooltip("완수 시 쌍방 (신뢰)")]
        public int FulfillDelta = 15;

        [Tooltip("거절 시 의뢰인→대상 (서운함 — 음수)")]
        public int RefusedDelta = -5;

        [Tooltip("보상을 떼먹혔을 때 수행자→의뢰인 (원한 재료 — 음수. ADR-보상1의 결과)")]
        public int StiffedDelta = -10;

        [Header("주민 간 보상 (M8 후속 — 완공 보고 장면에서 의뢰인이 대접. 0 = 보상 없음, 중립)")]
        [Tooltip("보고 시점에 의뢰인 잔고(몸+집)에서 차감해 수행자에게 넘길 개인 슬롯 (M11-H — " +
                 "보상 = 실제 식량 이전. 전역 스톡 차감은 폐지됐다)")]
        public SlotId RewardCostSlot = SlotId.MyRawFood;

        [Tooltip("이전량. 0이면 보상 없음 (감사 대사만). 의뢰인 잔고나 수행자 수령 공간이 " +
                 "모자라면 지급이 아니라 '연기' — 빚은 남고 다음 마주침에 다시 시도한다")]
        public int RewardCostAmount;

        // RewardSatietyGain(M11-H 폐지 잔재)은 2026-08-11 삭제 — 수행자는 받은 식량을 먹어서
        // 포만을 얻는다 (결정 10). 판독자는 자기 자신을 지키는 OnValidate 경고뿐이었다.

        [Tooltip("수락 즉시 지급 (M17-R4) — 성격의 선불 요구(AgentConfig.DemandsUpfront)와 무관하게 " +
                 "**언제나** 선불로 처리한다.\n" +
                 "집 부탁이 이 값을 켠다: 의뢰인 조건이 이미 '돈 50동 이상'이라 부탁하는 순간 지불 능력이 " +
                 "보장되므로, 완수 후 정산으로 미룰 이유가 없다. 미루면 정산이 반경 3의 우연에 걸려 " +
                 "빚이 영영 안 갚아지고 채무자는 빚 goal을 무한히 돈다 (Play 관측 2026-08-01 — 아사).\n" +
                 "⚠️ 지불 능력이 부탁 조건으로 보장되지 않는 부탁에는 켜지 말 것 — 선불 실패 시 " +
                 "기존 후불·연기 경로로 조용히 되돌아간다(손실 없음)지만, 그러면 켠 의미가 없다.")]
        public bool AlwaysUpfront;

        [Header("대사 (배고픔·피로 거절은 성격 RefuseLines 재사용 — 이중 기입 금지)")]
        public string[] AskLines;
        public string[] AcceptLines;
        public string[] RefuseBusyLines;
        public string[] RefuseLowAffinityLines;
        public string[] FulfillLines;

        [Tooltip("보고 장면에서 의뢰인의 보상 대사 (지급 성공 시)")]
        public string[] RewardLines;

        [Tooltip("보고 장면에서 의뢰인의 감사 대사 (보상 없음/재고 부족 시)")]
        public string[] ThanksLines;

        [Tooltip("선불 거절 대사 (M8 보완 — 선불 성격 수행자가 보상 재고 없음/보상 0 부탁을 거절)")]
        public string[] RefuseNoRewardLines;

        [Tooltip("정산 연기 대사 (M16-B — 잔고가 모자라 못 갚을 때 의뢰인의 첫 한마디. " +
                 "비면 AgentConfig.DeferRewardLines. 두 번째 마주침부터는 침묵)")]
        public string[] DeferLines;

        private void OnValidate()
        {
            if (GrantOwnership && !SlotIds.IsNumeric(OwnershipSlot))
                Debug.LogError($"[RequestSO] {name}: OwnershipSlot({OwnershipSlot})은 수량형(수치) 슬롯이어야 합니다.", this);
            // 즉시 교환(M16-W5)은 InjectGoal이 정상적으로 비어 있다 — 일이 아니라 교환이다
            if (InjectGoal == null && TradeGiveAmount <= 0)
                Debug.LogWarning($"[RequestSO] {name}: InjectGoal이 비어 있음 — 성립해도 아무 일도 일어나지 않습니다.", this);
            if (TradeGiveAmount > 0 && !SlotIds.IsPersonalStock(TradeGiveSlot))
                Debug.LogError($"[RequestSO] {name}: TradeGiveSlot({TradeGiveSlot})은 개인 스톡 슬롯이어야 " +
                               "합니다 (M16-W5 — 즉시 교환은 개인 잔고끼리의 이전).", this);
            if (TradeGiveAmount > 0 && InjectGoal != null)
                Debug.LogWarning($"[RequestSO] {name}: 즉시 교환인데 InjectGoal이 설정됨 — 교환 부탁은 " +
                                 "goal을 주입하지 않습니다 (무시됨, M16-W5).", this);
            // M11-H: 보상은 의뢰인 개인 잔고에서 나간다 — 개인 스톡이 정상, 전역 스톡은 휴면 경고.
            if (RewardCostAmount > 0 && !SlotIds.IsPersonalStock(RewardCostSlot))
                Debug.LogError($"[RequestSO] {name}: RewardCostSlot({RewardCostSlot})은 개인 스톡 슬롯이어야 " +
                               "합니다 (M11-H — 보상 = 실제 식량 이전. 전역 스톡은 휴면).", this);
            // M12-G: 우회 조건만 있고 성향 조건이 없으면 우회할 대상이 없다 — 기입 실수 신호
            // (조건이 조용히 무시되면 "설정했는데 왜 안 먹지"가 관측 불가능해진다).
            if ((TraitBypassConditions != null && TraitBypassConditions.Length > 0)
                && (RequesterTraits == null || RequesterTraits.Length == 0))
                Debug.LogWarning($"[RequestSO] {name}: TraitBypassConditions가 있는데 RequesterTraits가 " +
                                 "비었습니다 — 우회할 성향 조건이 없어 아무 효과도 없습니다.", this);
        }
    }
}
