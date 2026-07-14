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

        [Tooltip("완공 시 스폰할 프리팹. 비우면 아래 Fallback 설정으로 원형 마커를 코드 생성.")]
        public GameObject Prefab;

        [Tooltip("프리팹 없을 때 마커 색. 알파 0이면 런타임에서 1로 자동 보정 (舊 BC5 함정 방어).")]
        public Color FallbackColor = new Color(1f, 0.55f, 0.15f, 1f);

        [Tooltip("프리팹 없을 때 마커 크기 (타일 단위 스케일)")]
        public float FallbackSize = 1f;

        [Tooltip("스프라이트 정렬 순서. 음수면 맵 아래로 숨음 — 기본 5 (舊 BuildingSpawner 기본값)")]
        public int SortingOrder = 5;

        private void OnValidate()
        {
            // 수량형 카운트는 수치 슬롯에만 — 논리형에 설정하는 실수 방어 (명세 M2-A ⚠️)
            if (IsCountable && !SlotIds.IsNumeric(CountSlot))
                Debug.LogError($"[BuildingSO] {name}: CountSlot({CountSlot})은 수치형 슬롯이어야 합니다.", this);
        }
    }
}
