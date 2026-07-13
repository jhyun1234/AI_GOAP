using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// ConstructionService.OnCompleted 구독자 — 완공 건물의 시각 표현을 스폰한다.
    /// BuildingSO.Prefab이 있으면 그것을, 없으면 원형 마커를 코드 생성한다.
    /// 舊 BC5 함정 방어: 알파 0 자동 보정 + SortingOrder 명시.
    /// </summary>
    public sealed class BuildingVisualizer
    {
        private readonly Transform _parent;

        public BuildingVisualizer(Transform parent)
        {
            _parent = parent;
        }

        public GameObject Spawn(BuildingSO building, int tileX, int tileY)
        {
            var pos = new Vector3(tileX, tileY, 0f); // ADR-4: 2D X-Y 평면, Z=0

            if (building.Prefab != null)
                return Object.Instantiate(building.Prefab, pos, Quaternion.identity, _parent);

            // ── 폴백 마커 생성 ──
            var go = new GameObject($"Building_{building.name}");
            go.transform.SetParent(_parent, worldPositionStays: false);
            go.transform.position   = pos;
            go.transform.localScale = Vector3.one * Mathf.Max(0.1f, building.FallbackSize);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = M0Sprites.Circle;

            Color c = building.FallbackColor;
            if (c.a <= 0f) c.a = 1f; // BC5: 알파 0 에셋 함정 방어
            sr.color = c;
            sr.sortingOrder = building.SortingOrder;
            return go;
        }
    }
}
