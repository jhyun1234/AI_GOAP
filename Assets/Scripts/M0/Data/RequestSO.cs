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

        [Tooltip("의뢰인 성립 조건 (스냅샷 대역 — 예: MyHasHome == 0). 비면 항상 성립 (남용 주의)")]
        public SlotCondition[] RequesterConditions;

        [Tooltip("부탁 대상 직업 (참조 매핑). 비면 아무나 — 능력 조건 없는 부탁")]
        public JobSO TargetJob;

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

        [Header("대사 (배고픔·피로 거절은 성격 RefuseLines 재사용 — 이중 기입 금지)")]
        public string[] AskLines;
        public string[] AcceptLines;
        public string[] RefuseBusyLines;
        public string[] RefuseLowAffinityLines;
        public string[] FulfillLines;

        private void OnValidate()
        {
            if (GrantOwnership && !SlotIds.IsNumeric(OwnershipSlot))
                Debug.LogError($"[RequestSO] {name}: OwnershipSlot({OwnershipSlot})은 수량형(수치) 슬롯이어야 합니다.", this);
            if (InjectGoal == null)
                Debug.LogWarning($"[RequestSO] {name}: InjectGoal이 비어 있음 — 성립해도 아무 일도 일어나지 않습니다.", this);
        }
    }
}
