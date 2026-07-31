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

        [Tooltip("집이 없으면 **제자리 곁**에 짓는다 (M16-B — 모닥불 전용). false면 집이 없을 때 " +
                 "배치 실패 = 기존 동작(밭 등 집을 전제하는 소유물).\n" +
                 "왜 필요한가: 모닥불 → 조리 → 조리식이 겨울 생존의 사슬인데, 집을 전제하면 " +
                 "집을 못 산 주민은 조리 자체가 불가능해 반드시 아사한다 (2026-08-01 Play 관측: " +
                 "집 있는 목수만 생존). 집은 '더 많이 저장하는 자산'이지 생존 관문이 아니다.")]
        public bool FallbackToSelfIfNoHome;

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
            // 배치 결정자는 하나뿐이어야 한다 (M11-E/F ⚠️) — 택지·구역·집 곁이 겹치면 규칙이 이원화된다
            int placers = (MinSpacingTiles > 0 ? 1 : 0) + (ZoneRadius > 0 ? 1 : 0) + (PlaceNearOwnedHome ? 1 : 0);
            if (placers > 1)
                Debug.LogError($"[BuildingSO] {name}: 배치 결정자는 하나만 — MinSpacingTiles(택지)·" +
                               "ZoneRadius(구역)·PlaceNearOwnedHome(집 곁) 중 하나만 설정하세요.", this);
        }
    }
}
