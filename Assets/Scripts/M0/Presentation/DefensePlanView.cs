using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 방어 계획 마커 (M22-W3R2, 표현 전용) — DefenseService.OnPlanChanged 구독.
    /// 계획된(아직 안 지어진) 울타리·문 칸을 흐린 마커로 보여준다 — 지어지기 전의 계획이
    /// 화면에 안 보이면 "그었는데 아무 일도 없다"가 된다. 시뮬 쓰기 0.
    /// 변경마다 전체 재구축 — 계획은 수백 칸 이하·변경은 드물어(그리기·완공·파괴 순간뿐) 충분하다.
    /// </summary>
    public sealed class DefensePlanView
    {
        private static readonly Color FencePlanColor = new Color(0.55f, 0.4f, 0.2f, 0.35f);  // 울타리 예정 — 흐린 갈색
        private static readonly Color GatePlanColor = new Color(0.9f, 0.75f, 0.35f, 0.45f); // 문 예정 — 흐린 노랑

        private readonly Transform _parent;
        private readonly DefenseService _defense;
        private readonly List<GameObject> _markers = new List<GameObject>();

        public DefensePlanView(Transform parent, DefenseService defense)
        {
            _parent = parent;
            _defense = defense;
            if (_defense != null) _defense.OnPlanChanged += Rebuild;
        }

        private void Rebuild()
        {
            foreach (GameObject go in _markers)
                if (go != null) Object.Destroy(go);
            _markers.Clear();
            foreach (Vector2Int t in _defense.PlannedFenceTiles) Spawn(t, FencePlanColor);
            foreach (Vector2Int t in _defense.PlannedGateTiles) Spawn(t, GatePlanColor);
        }

        private void Spawn(Vector2Int tile, Color color)
        {
            var go = new GameObject($"DefensePlan_{tile.x}_{tile.y}");
            go.transform.SetParent(_parent, worldPositionStays: false);
            go.transform.position = new Vector3(tile.x, tile.y, 0f); // ADR-M0-9 — X-Y 평면
            go.transform.localScale = Vector3.one * 0.45f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = M0Sprites.Circle;
            sr.color = color;
            sr.sortingOrder = 1; // 건물 마커(5) 아래 — 실물이 서면 계획 마커는 사라진다 (Rebuild)
            _markers.Add(go);
        }
    }
}
