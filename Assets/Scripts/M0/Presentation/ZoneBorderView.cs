using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 구역 테두리 (M9-A, 표현 전용) — ZoneService.OnZoneEstablished 구독. 확정 순간 앵커 둘레에
    /// LineRenderer 사각 외곽선을 그린다 (앵커 ± radius+0.5, 타일 경계 정렬). 시뮬 쓰기 0 —
    /// "농경지"라는 장소를 화면에 보이게 하는 것이 전부 (M9-S1 육안 판정).
    /// </summary>
    public sealed class ZoneBorderView
    {
        private readonly Transform _parent;
        private static readonly Color FarmZoneColor = new Color(0.55f, 0.38f, 0.18f, 0.5f); // 밭 계열 연갈색 반투명

        public ZoneBorderView(Transform parent)
        {
            _parent = parent;
        }

        /// <summary>사각 영역 테두리 (M22 방어 구역 — ADR-M22-4 개정: 드래그 임의 사각형).</summary>
        public void DrawRect(SlotId countSlot, Vector2Int min, Vector2Int max)
        {
            var go = new GameObject($"ZoneBorder_{countSlot}");
            go.transform.SetParent(_parent, worldPositionStays: false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.positionCount = 4;
            lr.widthMultiplier = 0.12f;
            lr.numCornerVertices = 0;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = FarmZoneColor;
            lr.sortingOrder = 1;
            lr.SetPositions(new[]
            {
                new Vector3(min.x - 0.5f, min.y - 0.5f, 0f),
                new Vector3(max.x + 0.5f, min.y - 0.5f, 0f),
                new Vector3(max.x + 0.5f, max.y + 0.5f, 0f),
                new Vector3(min.x - 0.5f, max.y + 0.5f, 0f),
            });
        }

        public void Draw(SlotId countSlot, Vector2Int anchor, int radius)
        {
            var go = new GameObject($"ZoneBorder_{countSlot}");
            go.transform.SetParent(_parent, worldPositionStays: false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.positionCount = 4;
            lr.widthMultiplier = 0.12f;
            lr.numCornerVertices = 0;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = FarmZoneColor;
            lr.sortingOrder = 1; // 밭 마커(2) 아래 — 테두리가 작물을 가리지 않게

            float e = radius + 0.5f;
            float ax = anchor.x, ay = anchor.y;
            lr.SetPositions(new[]
            {
                new Vector3(ax - e, ay - e, 0f),
                new Vector3(ax + e, ay - e, 0f),
                new Vector3(ax + e, ay + e, 0f),
                new Vector3(ax - e, ay + e, 0f),
            });
        }
    }
}
