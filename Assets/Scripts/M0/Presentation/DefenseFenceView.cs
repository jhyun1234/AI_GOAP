using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 울타리 오토타일 뷰 (M23-W2, 표현 전용) — 몸(GameObject)은 BuildingVisualizer가 스폰하고
    /// 여기는 **옷(조각 스프라이트)만 입힌다** (ADR-M23-3 — 두 번째 스폰 경로 금지).
    /// 조각은 4방 이웃 마스크의 순수 함수(PieceOf)가 고르고 (ADR-M23-2, 게이트 M23_T2),
    /// 이웃 판정에는 문을 포함한다 — 문 곁 울타리가 끝단으로 보이면 벽이 끊긴 그림이 된다.
    /// 갱신은 변경 타일의 4방만 (맵 전수 스캔 금지 — 명세 §W2 ⚠️). 시뮬 쓰기 0.
    /// </summary>
    public sealed class DefenseFenceView
    {
        private readonly Dictionary<Vector2Int, SpriteRenderer> _fences
            = new Dictionary<Vector2Int, SpriteRenderer>();
        private readonly HashSet<Vector2Int> _gates = new HashSet<Vector2Int>();
        private Sprite[] _pieces; // 조각의 단일 출처 = Fence.asset TileSprites (첫 완공 시 캐시)

        /// <summary>4방 마스크(N=1·E=2·S=4·W=8) → Fences.png 조각 인덱스 (행×4+열, 윗행부터).
        /// 시트 판독 2026-08-09: 0=세로상단끝 1=가로좌끝 2=가로중간 3=가로우끝 / 4=세로중간
        /// 5=┌ 6=┬ 7=┐ / 8=세로하단끝 9=├ 10=┼ 11=┤ / 12=단독기둥 13=└ 14=┴ 15=┘.</summary>
        private static readonly int[] PieceByMask =
            { 12, 8, 1, 13, 0, 4, 5, 9, 3, 15, 2, 14, 7, 11, 6, 10 };

        /// <summary>조각 선택 (순수 — 게이트 M23_T2 16마스크 전수 검산).</summary>
        public static int PieceOf(bool n, bool e, bool s, bool w)
            => PieceByMask[(n ? 1 : 0) | (e ? 2 : 0) | (s ? 4 : 0) | (w ? 8 : 0)];

        /// <summary>완공 옷 입히기 — SimulationLoop 시각 스폰 배선이 호출 (ADR-M23-3).
        /// 방어물이 아니면 무시 (기존 건물 무변 — 중립).</summary>
        public void Dress(BuildingSO b, GameObject body, Vector2Int tile)
        {
            if (b == null || body == null) return;
            if (b.CountSlot == SlotId.FenceCount && b.TileSprites != null && b.TileSprites.Length == 16)
            {
                _pieces = b.TileSprites;
                SpriteRenderer sr = body.GetComponentInChildren<SpriteRenderer>();
                if (sr == null) return;
                _fences[tile] = sr;
                Apply(tile);
                RefreshNeighbors(tile);
            }
            else if (b.CountSlot == SlotId.GateCount)
            {
                // 문은 자기 그림 고정 (MarkerSprite = 닫힌 문) — 이웃 연결에만 참여 (ADR-M23-2)
                _gates.Add(tile);
                RefreshNeighbors(tile);
            }
        }

        /// <summary>제거 통지 — 파괴·철거로 구멍이 나면 양옆이 끝단 조각으로 바뀐다.</summary>
        public void NotifyRemoved(SlotId slot, Vector2Int tile)
        {
            bool changed = slot == SlotId.FenceCount ? _fences.Remove(tile)
                         : slot == SlotId.GateCount && _gates.Remove(tile);
            if (changed) RefreshNeighbors(tile);
        }

        private bool Connects(Vector2Int t) => _fences.ContainsKey(t) || _gates.Contains(t);

        private void Apply(Vector2Int t)
        {
            if (_pieces == null || !_fences.TryGetValue(t, out SpriteRenderer sr) || sr == null) return;
            sr.sprite = _pieces[PieceOf(
                Connects(t + Vector2Int.up), Connects(t + Vector2Int.right),
                Connects(t + Vector2Int.down), Connects(t + Vector2Int.left))];
        }

        private void RefreshNeighbors(Vector2Int t)
        {
            Apply(t + Vector2Int.up);
            Apply(t + Vector2Int.right);
            Apply(t + Vector2Int.down);
            Apply(t + Vector2Int.left);
        }
    }
}
