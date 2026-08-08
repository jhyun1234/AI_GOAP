using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 문 잠금 자물쇠 마커 (M22-2차 W1, 표현 전용) — DefenseService.OnGateLockChanged 구독.
    /// 잠금 중 서 있는 문 위에 노란 표식을 얹는다 — 잠긴 문이 안 보이면 "L을 눌렀는데 아무 일도
    /// 없다"가 된다 (DefensePlanView와 같은 원리). 내구도 이벤트도 구독한다 — 잠금 중 신규 문
    /// 완공(마커 추가)·파괴(마커 제거)를 따라간다. 시뮬 쓰기 0.
    /// </summary>
    public sealed class GateLockView
    {
        private readonly Transform _parent;
        private readonly DefenseService _defense;
        private readonly List<GameObject> _markers = new List<GameObject>();

        public GateLockView(Transform parent, DefenseService defense)
        {
            _parent = parent;
            _defense = defense;
            if (defense == null) return;
            defense.OnGateLockChanged += locked => Rebuild();
            // 내구도 변화 = 문 생사의 유일한 신호 (등록·소멸이 같은 이벤트로 나간다).
            // 잠금 해제 상태면 Rebuild가 즉시 반환하므로 공성 중 손상 이벤트 비용은 무시 가능.
            defense.OnDurabilityChanged += (slot, tile, cur, max) =>
            {
                if (slot == SlotId.GateCount) Rebuild();
            };
        }

        /// <summary>마커 전량 재구성 — 문은 소수라 부분 갱신의 장부 관리보다 싸고 결정적이다.</summary>
        private void Rebuild()
        {
            foreach (GameObject go in _markers)
                if (go != null) Object.Destroy(go);
            _markers.Clear();
            if (!_defense.GatesLocked) return;

            foreach (Vector2Int t in _defense.BuiltGateTiles)
            {
                var go = new GameObject($"GateLock_{t.x}_{t.y}");
                go.transform.SetParent(_parent, worldPositionStays: false);
                go.transform.position = new Vector3(t.x, t.y, 0f); // ADR-M0-9 — X-Y 평면
                go.transform.localScale = Vector3.one * 0.35f;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = M0Sprites.Circle;
                sr.sortingOrder = 7; // 손상 오버레이(6) 위, 위협(11) 아래
                sr.color = new Color(0.95f, 0.8f, 0.15f, 0.9f); // 자물쇠 노랑 — 손상 검붉음과 구분
                _markers.Add(go);
            }
        }
    }
}
