using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// ConstructionService.OnCompleted 구독자 — 완공 건물의 시각 표현을 스폰한다.
    /// BuildingSO.Prefab이 있으면 그것을, 없으면 원형 마커를 코드 생성한다.
    /// 舊 BC5 함정 방어: 알파 0 자동 보정 + SortingOrder 명시.
    /// M9-B: 스폰한 GameObject를 (slot,tile) 키로 추적해 OnRemoved 시 파괴한다.
    /// </summary>
    public sealed class BuildingVisualizer
    {
        private readonly Transform _parent;
        // 제거(M9-B) 대비 추적 — 키 = (식별 슬롯, 타일). 수량형=CountSlot, 단일형=BuiltFlagSlot.
        private readonly Dictionary<(SlotId slot, Vector2Int tile), GameObject> _spawned = new();

        public BuildingVisualizer(Transform parent)
        {
            _parent = parent;
        }

        public GameObject Spawn(BuildingSO building, int tileX, int tileY)
        {
            GameObject go = SpawnMarker(building, tileX, tileY);
            SlotId key = building.IsCountable ? building.CountSlot : building.BuiltFlagSlot;
            _spawned[(key, new Vector2Int(tileX, tileY))] = go;
            return go;
        }

        /// <summary>완공 건물의 몸 스프라이트 조회 (2026-08-09) — 손상 표현이 **건물 자체를**
        /// 바래게 하려면 그 렌더러가 필요하다. 두 번째 스폰 경로를 만들지 않기 위한 창구
        /// (ADR-M23-3) — 뷰는 옷만 갈아입히고 몸은 여전히 여기 하나가 소유한다.</summary>
        public bool TryGetRenderer(SlotId slot, Vector2Int tile, out SpriteRenderer sr)
        {
            sr = null;
            if (!_spawned.TryGetValue((slot, tile), out GameObject go) || go == null) return false;
            sr = go.GetComponentInChildren<SpriteRenderer>();
            return sr != null;
        }

        /// <summary>수량형 건물 제거 시각 (M9-B) — ConstructionService.OnRemoved 구독. 추적 GameObject 파괴.</summary>
        public void Remove(SlotId countSlot, int tileX, int tileY)
        {
            var key = (countSlot, new Vector2Int(tileX, tileY));
            if (!_spawned.TryGetValue(key, out GameObject go)) return;
            _spawned.Remove(key); // 사전 키 잔류 = 누수 (⚠② 대칭)
            if (go != null) Object.Destroy(go);
        }

        private GameObject SpawnMarker(BuildingSO building, int tileX, int tileY)
        {
            var pos = new Vector3(tileX, tileY, 0f); // ADR-4: 2D X-Y 평면, Z=0

            if (building.Prefab != null)
                return Object.Instantiate(building.Prefab, pos, Quaternion.identity, _parent);

            // ── 폴백 마커 생성 ──
            var go = new GameObject($"Building_{building.name}");
            go.transform.SetParent(_parent, worldPositionStays: false);
            // 그림 위치 보정 (M22-3차 W5d) — 큰 그림은 피벗이 한가운데라 그대로 두면 땅에 파묻힌다.
            // 보정값은 에셋의 몫 (MountOffsetTiles와 같은 규약 — 표현 수치를 코드에 두면 그림과 어긋난다).
            go.transform.position   = pos + (Vector3)(Vector2)building.MarkerOffsetTiles;
            go.transform.localScale = Vector3.one * Mathf.Max(0.1f, building.FallbackSize);

            var sr = go.AddComponent<SpriteRenderer>();
            if (building.MarkerSprite != null)
            {
                sr.sprite = building.MarkerSprite; // 원본색 그대로 (M3-D 시각 보강)
                // 애니 프레임이 있으면 재생기를 얹는다 (모닥불·조리 전환). 없으면 정지 그림 그대로.
                bool anim = (building.MarkerFrames != null && building.MarkerFrames.Length > 0)
                            || (building.AltFrames != null && building.AltFrames.Length > 0);
                if (anim)
                    go.AddComponent<BuildingFlipbook>()
                      .Setup(sr, building.MarkerFrames, building.AltFrames, building.MarkerFps);
            }
            else
            {
                sr.sprite = M0Sprites.Circle;
                Color c = building.FallbackColor;
                if (c.a <= 0f) c.a = 1f; // BC5: 알파 0 에셋 함정 방어
                sr.color = c;
            }
            // 깊이 = 월드 Y (WorldSort) — SortingOrder는 **같은 칸에서의 위아래**만 정한다.
            // 舊 절대 층(5 고정)은 주민이 집 뒤에 서도 집 위에 그려지게 만들었다.
            sr.sortingOrder = WorldSort.Order(tileY, building.SortingOrder);
            return go;
        }
    }
}
