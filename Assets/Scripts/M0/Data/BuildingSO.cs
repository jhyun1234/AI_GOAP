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

        [Tooltip("완공 시 1로 세팅되는 논리형 슬롯")]
        public SlotId BuiltFlagSlot;

        [Tooltip("완공 시 스폰할 프리팹. 비우면 BuildingSpawner의 fallback 스프라이트 사용.")]
        public GameObject Prefab;
    }
}
