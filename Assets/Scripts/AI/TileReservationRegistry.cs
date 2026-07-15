using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.AI
{
    /// <summary>
    /// 이동 예약 전역 registry. 유닛이 자기 현재 타일 + 다음 웨이포인트 타일을 예약해
    /// 같은 타일에 두 유닛이 동시에 들어서지 못하게 한다.
    /// 자원 채집 점유(ResourceNode.OccupiedTiles)와는 분리 병존 — 목적·수명이 다름 (ADR-T7).
    /// 진리 소스는 이 클래스 한 곳 (ADR-T10).
    /// </summary>
    public static class TileReservationRegistry
    {
        private static readonly Dictionary<Vector2Int, string> _reservations
            = new Dictionary<Vector2Int, string>(256);

        /// <summary>현재 등록된 예약 개수. EditMode 게이트 M22 / 디버그 훅용.</summary>
        public static int Count => _reservations.Count;

        /// <summary>
        /// tile이 비어 있거나 ownerId가 이미 소유 중이면 true를 반환하며 예약을 확정한다.
        /// 같은 owner의 재요청은 항상 true (idempotent) — 이동 파이프라인의 반복 확인을 허용.
        /// </summary>
        public static bool TryReserve(Vector2Int tile, string ownerId)
        {
            if (_reservations.TryGetValue(tile, out string existing))
                return existing == ownerId;
            _reservations[tile] = ownerId;
            return true;
        }

        /// <summary>
        /// ownerId가 소유 중일 때만 해제한다. 다른 소유자의 예약은 건드리지 않는다.
        /// </summary>
        public static void Release(Vector2Int tile, string ownerId)
        {
            if (_reservations.TryGetValue(tile, out string existing) && existing == ownerId)
                _reservations.Remove(tile);
        }

        /// <summary>tile이 ownerId 소유인지 여부. 예약이 없거나 다른 소유자면 false.</summary>
        public static bool IsOwnedBy(Vector2Int tile, string ownerId)
        {
            return _reservations.TryGetValue(tile, out string existing) && existing == ownerId;
        }

        /// <summary>
        /// tile이 ownerId가 아닌 다른 소유자에게 예약돼 있는지 — 읽기 전용 질의 (M4 최소 추가).
        /// 통행 차단 건물이 주민이 서 있는 타일 위에 완공되면 그 주민은 JPS 출발 불가로
        /// 영구히 갇힌다 — 건설 위치 회피·완공 대기(BuildRunner)가 이 질의로 방지한다.
        /// </summary>
        public static bool IsReservedByOther(Vector2Int tile, string ownerId)
        {
            return _reservations.TryGetValue(tile, out string existing) && existing != ownerId;
        }

        /// <summary>
        /// OnDestroy·사망 진입 시 호출해 ownerId의 모든 예약을 회수한다 (leak 원천 차단, ADR-T6).
        /// foreach 순회 중 직접 삭제는 InvalidOperationException 유발 — 별도 리스트로 사후 삭제.
        /// </summary>
        public static void ReleaseAllBy(string ownerId)
        {
            var toRemove = new List<Vector2Int>();
            foreach (var kv in _reservations)
                if (kv.Value == ownerId) toRemove.Add(kv.Key);
            for (int i = 0; i < toRemove.Count; i++) _reservations.Remove(toRemove[i]);
        }

        /// <summary>
        /// GameManager 초기화·씬 리로드·EditMode 게이트 setup에서 사용.
        /// 잊으면 이전 세션의 좀비 예약이 남는다.
        /// </summary>
        public static void ResetAll() => _reservations.Clear();
    }
}
