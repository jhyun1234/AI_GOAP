using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 방어 계획 마커 (M22-W3R2 → M23-W3 고스트, 표현 전용) — DefenseService.OnPlanChanged 구독.
    /// 계획된(아직 안 지어진) 울타리·문 칸을 **반투명 실물 그림**으로 보여준다 — "여기에 이게
    /// 설 것"이 그림으로 읽힌다. 건물 에셋 미배선이면 흐린 원 폴백 (중립). 시뮬 쓰기 0.
    /// 변경마다 전체 재구축 — 계획은 수백 칸 이하·변경은 드물어(그리기·완공·파괴 순간뿐) 충분하다.
    /// </summary>
    public sealed class DefensePlanView
    {
        private static readonly Color FencePlanColor = new Color(0.55f, 0.4f, 0.2f, 0.35f);  // 폴백 원 — 흐린 갈색
        private static readonly Color GatePlanColor = new Color(0.9f, 0.75f, 0.35f, 0.45f); // 폴백 원 — 흐린 노랑
        private static readonly Color TrapPlanColor = new Color(0.5f, 0.5f, 0.58f, 0.4f);   // 폴백 원 — 흐린 강철 (M22-3차)
        private static readonly Color TowerPlanColor = new Color(0.62f, 0.47f, 0.28f, 0.4f); // 폴백 원 — 흐린 목재 (M22-3차)
        private static readonly Color GhostTint = new Color(1f, 1f, 1f, 0.4f);              // 고스트 — 원본색 반투명
        // 개간 대기 (M22-4차 W5) — 계획과 **다른 색**이라야 "여긴 아직 못 짓는다"가 보인다.
        // 붉은색인 이유: 이건 안내가 아니라 **구멍 경고**다 (그대로 두면 적이 그리로 들어온다).
        private static readonly Color BlockedPlanColor = new Color(0.85f, 0.3f, 0.25f, 0.5f);

        private readonly Transform _parent;
        private readonly DefenseService _defense;
        private readonly BuildingSO _fence, _gate;   // 고스트 그림·스케일의 원천 (M23-W3, 에셋)
        private readonly BuildingSO _trap, _tower;   // M22-3차 W2 — 같은 문법
        private readonly List<GameObject> _markers = new List<GameObject>();

        public DefensePlanView(Transform parent, DefenseService defense,
                               BuildingSO fence = null, BuildingSO gate = null,
                               BuildingSO trap = null, BuildingSO tower = null)
        {
            _parent = parent;
            _defense = defense;
            _fence = fence;
            _gate = gate;
            _trap = trap;
            _tower = tower;
            if (_defense != null) _defense.OnPlanChanged += Rebuild;
        }

        private void Rebuild()
        {
            foreach (GameObject go in _markers)
                if (go != null) Object.Destroy(go);
            _markers.Clear();
            foreach (Vector2Int t in _defense.PlannedFenceTiles) Spawn(t, _fence, FencePlanColor);
            foreach (Vector2Int t in _defense.PlannedGateTiles) Spawn(t, _gate, GatePlanColor);
            foreach (Vector2Int t in _defense.PlannedTrapTiles) Spawn(t, _trap, TrapPlanColor);   // M22-3차
            foreach (Vector2Int t in _defense.PlannedTowerTiles) Spawn(t, _tower, TowerPlanColor); // M22-3차
            // 개간 대기 칸 (M22-4차 W5) — 개간이 끝나면 이 붉은 칸이 갈색 계획으로 바뀐다.
            // 승격이 화면에서 보이는 것이 요점이다 ("치웠더니 담이 이어졌다").
            foreach (Vector2Int t in _defense.BlockedFenceTiles) Spawn(t, _fence, BlockedPlanColor);
        }

        private void Spawn(Vector2Int tile, BuildingSO b, Color fallback)
        {
            var go = new GameObject($"DefensePlan_{tile.x}_{tile.y}");
            go.transform.SetParent(_parent, worldPositionStays: false);
            go.transform.position = new Vector3(tile.x, tile.y, 0f); // ADR-M0-9 — X-Y 평면
            var sr = go.AddComponent<SpriteRenderer>();
            if (b != null && b.MarkerSprite != null)
            {
                // 고스트 = 실물 그림 반투명 (울타리 = 대표 기둥, 문 = 닫힌 문) — 스케일도 실물과 동일
                sr.sprite = b.MarkerSprite;
                sr.color = GhostTint;
                go.transform.localScale = Vector3.one * Mathf.Max(0.1f, b.FallbackSize);
                // 위치 보정도 실물과 같이 (M22-3차 W5d) — 안 따라가면 고스트만 땅에 파묻힌다
                go.transform.position += (Vector3)(Vector2)b.MarkerOffsetTiles;
            }
            else
            {
                sr.sprite = M0Sprites.Circle;
                sr.color = fallback;
                go.transform.localScale = Vector3.one * 0.45f;
            }
            // 아직 없는 것 = 같은 칸 실물 아래 (Rebuild가 지우지만 한 틱 겹칠 수 있다)
            sr.sortingOrder = WorldSort.Order(tile.y, WorldSort.Ghost);
            _markers.Add(go);
        }
    }
}
