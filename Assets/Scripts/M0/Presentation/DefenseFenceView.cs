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
        // 문도 조각 옷을 입는다 (M22-2차 W3, 사용자 Play 피드백: "문이 뜬금없다" — 舊 고정
        // Fence_Big_Gate 그림은 다른 계열이라 줄과 안 어울렸다). 값 null = 조각 미배선 폴백
        // (고정 그림 유지 — 중립).
        private readonly Dictionary<Vector2Int, SpriteRenderer> _gates
            = new Dictionary<Vector2Int, SpriteRenderer>();
        private Sprite[] _pieces;     // 조각의 단일 출처 = Fence.asset TileSprites (첫 완공 시 캐시)
        private Sprite[] _gatePieces; // 문 조각 = Gate.asset TileSprites (같은 시트 재사용 — 에셋이 정한다)

        // 문 구분 색조 (표현 상수 — ZoneOkColor 선례): 같은 조각을 입되 밝은 금빛으로 물들여
        // "여기가 출입구"가 한눈에 보이게 한다. 잠금 마커(노란 원)와 같은 색 계열.
        private static readonly Color GateTint = new Color(1f, 0.84f, 0.52f, 1f);

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
                // 문도 같은 시트 조각을 입는다 (M22-2차 W3) — 방향·연결이 줄을 따라가고,
                // 구분은 색조가 맡는다. 조각 미배선(TileSprites 비움)이면 舊 고정 그림 유지.
                SpriteRenderer sr = null;
                if (b.TileSprites != null && b.TileSprites.Length == 16)
                {
                    _gatePieces = b.TileSprites;
                    sr = body.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null) sr.color = GateTint;
                }
                _gates[tile] = sr;
                Apply(tile);
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

        private bool Connects(Vector2Int t) => _fences.ContainsKey(t) || _gates.ContainsKey(t);

        private void Apply(Vector2Int t)
        {
            // 울타리·문이 같은 마스크 문법을 공유한다 — 조각 출처만 에셋별로 갈린다 (ADR-M23-2)
            Sprite[] pieces;
            if (_fences.TryGetValue(t, out SpriteRenderer sr)) pieces = _pieces;
            else if (_gates.TryGetValue(t, out sr)) pieces = _gatePieces;
            else return;
            if (sr == null || pieces == null) return; // 조각 미배선 폴백 — 고정 그림 유지 (중립)
            sr.sprite = pieces[PieceOf(
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
