using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 방랑자 후보 마커 (M10-E) — 표현 전용 (ADR-M10-9): 주민이 아니다. RegisterAgent·타일 예약·
    /// FoodDaysLeft 전부 불포함 — 수락 순간 SpawnVillager가 진짜 주민으로 교대한다.
    /// 가장자리 → 마을 어귀 걷기 → 도착 통지 → (해소까지 대기) → 퇴장 걷기 → 소멸.
    /// ThreatAgent와 같은 웨이포인트 보간 — 타격이 없어 서비스 결합도 콜백 하나뿐.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WandererMarker : MonoBehaviour
    {
        private const float ARRIVE_EPSILON_SQR = 0.0001f; // 도착 판정 (알고리즘 상수)
        private const float MOVE_SPEED = 1.8f;            // 지친 나그네 걸음 (표현 상수 — 주민 2.0보다 느리게)

        public Vector2Int EntryTile { get; private set; }
        public int TileX => Mathf.RoundToInt(transform.position.x);
        public int TileY => Mathf.RoundToInt(transform.position.y);

        private List<Vector2Int> _path;
        private int _wp;
        private Action _onArrived; // 1회 통지 후 null
        private bool _leaving;

        public void Init(Vector2Int entry, List<Vector2Int> waypoints, Action onArrived)
        {
            EntryTile = entry;
            _path = waypoints; // null = 이미 어귀 (즉시 도착 통지)
            _wp = 0;
            _onArrived = onArrived;

            var sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = M0Sprites.Circle;
            sr.color = new Color(0.4f, 0.75f, 0.4f, 1f); // 초록 마커 — 우호적 방문자 (아트는 후속 에셋)
            _sr = sr; // 걸어오니까 깊이는 매 프레임 (WorldSort)
            transform.localScale = Vector3.one * 0.8f;
        }

        /// <summary>퇴장 (WandererService 전용) — 왔던 길로 걷다 소멸. waypoints null = 즉시 소멸.</summary>
        public void Leave(List<Vector2Int> waypoints)
        {
            _leaving = true;
            _onArrived = null;
            _path = waypoints;
            _wp = 0;
            if (_path == null) Destroy(gameObject);
        }

        private SpriteRenderer _sr; // 깊이 갱신 대상

        private void Update()
        {
            if (_sr != null) _sr.sortingOrder = WorldSort.Order(transform.position.y, WorldSort.Agent);
            if (_path == null || _wp >= _path.Count)
            {
                if (_leaving) { Destroy(gameObject); return; }
                if (_onArrived != null) { Action cb = _onArrived; _onArrived = null; cb(); }
                return; // 어귀에서 해소를 기다린다 (제자리)
            }

            var target = new Vector3(_path[_wp].x, _path[_wp].y, 0f); // ADR-M0-9 — X-Y 평면
            transform.position = Vector3.MoveTowards(transform.position, target, MOVE_SPEED * Time.deltaTime);
            if ((transform.position - target).sqrMagnitude <= ARRIVE_EPSILON_SQR) _wp++;
        }
    }
}
