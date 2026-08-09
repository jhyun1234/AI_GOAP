using System;
using UnityEngine;

namespace AIVillage.M0
{
    [Serializable]
    public struct ResourceCost
    {
        [Tooltip("차감할 스톡 슬롯 (수치형 슬롯만)")]
        public SlotId StockSlot;
        public int Amount;
    }

    /// <summary>
    /// 건물 정의. 비용·완공 플래그·프리팹의 단일 출처 (ADR-M0-2/M0-3).
    /// BuildActionSO가 플래너 전제/효과를 여기서 파생하고,
    /// W3 ConstructionService가 실제 차감·스폰도 여기서 읽는다 — 이중 기입 금지.
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Building", fileName = "Building")]
    public sealed class BuildingSO : ScriptableObject
    {
        [Tooltip("한국어 표시명 (예: 모닥불)")]
        public string DisplayName;

        [Tooltip("건설 비용. 舊 GOAPActionRegistry BUILD_* 상수 이관값.")]
        public ResourceCost[] Costs;

        [Tooltip("완공 시 1로 세팅되는 논리형 슬롯 (단일형 전용 — IsCountable이면 무시)")]
        public SlotId BuiltFlagSlot;

        [Tooltip("수량형 건물(밭 등, ADR-M2-3): true면 완공마다 CountSlot +1, 중복 완공 거부 없음. " +
                 "BuiltFlagSlot(단일형)과 배타 사용 — 모닥불 등 기존 단일형은 건드리지 않는다.")]
        public bool IsCountable;

        [Tooltip("수량형 카운트 수치 슬롯 (예: FarmPlotCount). IsCountable일 때만 사용.")]
        public SlotId CountSlot;

        [Tooltip("true면 완공 타일이 통행 불가가 된다 (집 등, ADR-M3-3). 모닥불·밭은 false — 밟고 지나다닌다. " +
                 "차단 건물은 건설자가 인접 타일에서 짓는다.")]
        public bool BlocksMovement;

        [Tooltip("구역 반경 (M9-A, ADR-M9-1). 0 = 구역 없음(기존 제자리+링 탐색). " +
                 ">0이면 첫 완공 타일이 앵커가 되고 이후 완공은 앵커 반경 내에만 배치된다 " +
                 "(수량형 전용 — 보이는 소프트 상한). 예: FarmPlot 3 = 7×7 농경지.")]
        public int ZoneRadius;

        [Tooltip("같은 슬롯 기존 완공과의 최소 간격 (체비쇼프 타일, M11-F). 0 = 간격 규칙 없음(중립). " +
                 ">0이면 배치가 택지 선정(HomePicker)으로 넘어간다 — 마을 앵커 반경 안에서 " +
                 "기존 건물과 이 간격 이상 떨어진 자리를 고른다. 집 3 = 밀집·PathBlocked 완화.")]
        public int MinSpacingTiles;

        [Tooltip("완공 시 지은 본인에게 소유를 배정한다 (M11-K, OwnershipService). 집·모닥불이 true. " +
                 "부탁 완수 집은 의뢰인 소유(NotifyFulfilled)라 자가 배정을 건너뛴다(부탁 수행 중이면 스킵). " +
                 "밭은 false — 밭 소유는 FarmService가 별도로 등록한다(축 분리).")]
        public bool OwnedBuilding;

        [Tooltip("true면 짓는 사람의 **내 집 곁**에 배치한다 (M11-E, 반경 = WorldConfig.FarmNearHomeRadius). " +
                 "밭이 이것 — 개인 소유물은 제 집 곁에 모인다. 집이 없으면 배치 실패 (goal 트리거가 " +
                 "MyHasHome==1로 이미 막지만 방어선). ZoneRadius·MinSpacingTiles와 배타.")]
        public bool PlaceNearOwnedHome;

        // ── 방어 (M22, Docs/M22_방어건설_실행명세서.md) ──

        [Tooltip("0보다 크면 내구도를 가진다 (ADR-M22-3). 위협의 StructureDamage로 깎이고 수리로 " +
                 "복원된다. 상태 소유는 DefenseService — 이 값은 최대치의 단일 출처다.")]
        public float MaxDurability;

        [Tooltip("true면 위협만 통행 불가 (문, ADR-M22-5 — 상태 없는 정적 차등 통행). " +
                 "주민 배열에는 등록되지 않는다. BlocksMovement와 배타 — 둘 다 켜면 에러.")]
        public bool BlocksThreatMovement;

        [Tooltip("true면 방어 계획 타일(플레이어 지정 구역 둘레)에 짓는다 (M22-W3/W4). " +
                 "배치 결정자 택1 규칙에 참여 — 다른 배치 필드와 겹치면 에러.")]
        public bool PlaceOnDefensePlan;

        [Tooltip("수리 1회의 Wood 비용 (MaxDurability > 0일 때만 의미). 수리 1회 = 전량 복원 (한 걸음, ADR-M0-12).")]
        public int RepairCost;

        [Tooltip("연결형 조각 16장 (M23-W2, ADR-M23-2) — 4×4 오토타일 시트 순서 (인덱스 = 행×4+열, " +
                 "윗행부터). 채우면 전용 뷰(DefenseFenceView)가 이웃 4방을 보고 조각을 고른다. " +
                 "비면 MarkerSprite/원형 폴백 (중립 — 기존 건물 무변).")]
        public Sprite[] TileSprites;

        [Tooltip("탑승 주민의 교전 사거리 (M22-3차, ADR-M22-11). 0 = 탑승 불가 시설. " +
                 "빈 망루는 탑일 뿐 — 사거리는 탑승 중인 주민에게만 적용되고 시설 자체는 싸우지 않는다.")]
        public int TowerRangeTiles;

        [Tooltip("위협이 밟으면 주는 피해 (M22-3차, ADR-M22-13). 0보다 크면 소모형 함정 — " +
                 "발동 시 소멸하고 계획으로 돌아가지 않는다 (재설치는 플레이어 몫). " +
                 "MaxDurability와 배타 — 함정은 소모지 내구도가 아니다.")]
        public float TrapDamage;

        [Tooltip("탑승한 주민이 올라서는 높이 (타일). 그림의 발판 위치와 짝이다 — " +
                 "스프라이트를 고치면 이 값도 고친다 (M22-3차 W5b: 코드 상수 0.45를 에셋으로 승격. " +
                 "그림이 커지면서 발판이 1.75타일로 올라갔는데 상수는 그대로라 사람이 다리 사이에 " +
                 "떠 있었다 — 표현 수치를 코드에 두면 그림과 반드시 어긋난다).")]
        public float MountOffsetTiles;

        [Tooltip("완공 시 스폰할 프리팹. 비우면 MarkerSprite → 원형 마커 순으로 폴백.")]
        public GameObject Prefab;

        [Tooltip("프리팹 없을 때 쓸 마커 스프라이트 (Kenmi 등). 비우면 원형 마커 폴백.")]
        public Sprite MarkerSprite;

        [Tooltip("원형 마커 폴백 색. 알파 0이면 런타임에서 1로 자동 보정 (舊 BC5 함정 방어). MarkerSprite에는 미적용(원본색).")]
        public Color FallbackColor = new Color(1f, 0.55f, 0.15f, 1f);

        [Tooltip("마커 공통 크기 (타일 단위 스케일) — 스프라이트/원형 모두 적용")]
        public float FallbackSize = 1f;

        [Tooltip("스프라이트 정렬 순서. 음수면 맵 아래로 숨음 — 기본 5 (舊 BuildingSpawner 기본값)")]
        public int SortingOrder = 5;

        private void OnValidate()
        {
            // 수량형 카운트는 수치 슬롯에만 — 논리형에 설정하는 실수 방어 (명세 M2-A ⚠️)
            if (IsCountable && !SlotIds.IsNumeric(CountSlot))
                Debug.LogError($"[BuildingSO] {name}: CountSlot({CountSlot})은 수치형 슬롯이어야 합니다.", this);
            // 구역은 수량형 전용 — 단일형에 반경을 주면 배치 결정자가 없다 (M9-A ⚠️)
            if (!IsCountable && ZoneRadius > 0)
                Debug.LogWarning($"[BuildingSO] {name}: ZoneRadius({ZoneRadius})는 수량형(IsCountable) 건물에만 적용됩니다 — 무시됨.", this);
            // 간격도 수량형 전용 — 기존 완공 목록(CountSlot)이 없으면 비교 대상이 없다 (M11-F)
            if (!IsCountable && MinSpacingTiles > 0)
                Debug.LogWarning($"[BuildingSO] {name}: MinSpacingTiles({MinSpacingTiles})는 수량형(IsCountable) 건물에만 적용됩니다 — 무시됨.", this);
            // 배치 결정자는 하나뿐이어야 한다 (M11-E/F ⚠️, M22 편입) — 겹치면 규칙이 이원화된다
            int placers = (MinSpacingTiles > 0 ? 1 : 0) + (ZoneRadius > 0 ? 1 : 0)
                        + (PlaceNearOwnedHome ? 1 : 0) + (PlaceOnDefensePlan ? 1 : 0);
            if (placers > 1)
                Debug.LogError($"[BuildingSO] {name}: 배치 결정자는 하나만 — MinSpacingTiles(택지)·" +
                               "ZoneRadius(구역)·PlaceNearOwnedHome(집 곁)·PlaceOnDefensePlan(방어 계획) " +
                               "중 하나만 설정하세요.", this);
            // 통행 플래그 배타 (ADR-M22-5 ⚠️) — 둘 다 켜면 "모두 차단"과 "위협만 차단"이 충돌한다
            if (BlocksMovement && BlocksThreatMovement)
                Debug.LogError($"[BuildingSO] {name}: BlocksMovement(모두 차단)와 " +
                               "BlocksThreatMovement(위협만 차단)는 배타입니다 — 하나만 켜세요.", this);
            // 수리 비용은 내구도가 있어야 의미가 있다 (M22-W2 ⚠️)
            if (RepairCost > 0 && MaxDurability <= 0f)
                Debug.LogWarning($"[BuildingSO] {name}: RepairCost({RepairCost})는 MaxDurability > 0일 때만 " +
                                 "쓰입니다 — 무시됨.", this);
            // 함정은 소모지 내구도가 아니다 (M22-3차 W1, ADR-M22-13 ⚠️) — 다회용 함정 금지
            if (TrapDamage > 0f && MaxDurability > 0f)
                Debug.LogError($"[BuildingSO] {name}: TrapDamage(소모형 함정)와 MaxDurability(내구도)는 " +
                               "배타입니다 — 함정은 발동 시 소멸합니다 (ADR-M22-13).", this);
        }
    }
}
