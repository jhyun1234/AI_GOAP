using System.Collections.Generic;
using AIVillage.Core;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 자원 노드 등록·발견·재생 관리 — 舊 SensorSystem(1,112줄) 중 M0 필요분만 (~100줄).
    /// ResourceNodeSpawner의 IResourceNodeSink 구현체로서 스폰 노드를 직접 받는다.
    /// </summary>
    public sealed class DiscoveryService : IResourceNodeSink
    {
        private readonly List<ResourceNode> _nodes = new List<ResourceNode>(64);

        public IReadOnlyList<ResourceNode> Nodes => _nodes;

        public void AddResourceNode(ResourceNode node)
        {
            if (node == null) return;
            _nodes.Add(node);
        }

        // ── 개간 (M22-4차) ────────────────────────────────────────────────────

        /// <summary>개간된 타일 (M22-4차 · ADR-C-3 **세이브 대상**) — M26 지형 축의
        /// **리스폰 금지 목록**이다. 이걸 안 남기면 불러오기·리스폰이 개간한 자리에 노드를
        /// 도로 심어 **오늘 닫은 울타리 구멍이 다시 열린다.**</summary>
        private readonly HashSet<Vector2Int> _cleared = new HashSet<Vector2Int>();

        /// <summary>개간 지정 노드 (M22-4차). 입구는 **계획이 막히는 순간 하나뿐**이다
        /// (ADR-C-5 — 마을이 스스로 숲을 밀지 않는다). 세이브 대상 아님: 계획이 원본이고
        /// 이 목록은 그 그림자다 (두 벌을 저장하면 한쪽만 남는 판이 생긴다).</summary>
        private readonly List<ResourceNode> _clearPending = new List<ResourceNode>(4);

        /// <summary>개간된 타일 목록 (읽기 전용 — 게이트·세이브).</summary>
        public IReadOnlyCollection<Vector2Int> ClearedTiles => _cleared;

        /// <summary>이 타일이 개간된 적 있는가 — M26 리스폰이 물어볼 자리 (지금은 아무도 안 읽는다).</summary>
        public bool WasCleared(int tileX, int tileY) => _cleared.Contains(new Vector2Int(tileX, tileY));

        /// <summary>개간 대기 노드 수 (SlotId.ClearPendingCount 의 원천 — Goal_Clear 트리거).</summary>
        public int ClearPendingCount => _clearPending.Count;

        /// <summary>노드 제거 알림 (제거된 노드, 회수량) — 울타리 계획 승격의 구독 지점.</summary>
        public event System.Action<ResourceNode, int> OnNodeRemoved;

        /// <summary>
        /// 개간 지정 (M22-4차). 이미 지정됐거나 제거된 노드면 false.
        /// 부르는 곳은 **계획이 노드에 막히는 순간** 하나뿐이다 (ADR-C-5).
        /// </summary>
        public bool MarkForClearing(ResourceNode node)
        {
            if (node == null || node.IsRemoved || _clearPending.Contains(node)) return false;
            _clearPending.Add(node);
            Debug.Log($"[Clear] 개간 지정 — ({node.TileX},{node.TileY}) {node.ResourceType}");
            return true;
        }

        /// <summary>
        /// 개간 지정 해제 (M22-4차 W8, 사용자 판정 안 B) — 플레이어가 그 칸의 계획을 취소하면
        /// 나무도 안 벤다. 지정돼 있었으면 true.
        ///
        /// 🔴 **취소는 취소다**: 이미 도끼질 중이던 주민도 완료 시점에 지정이 풀린 걸 보고
        /// 실패로 접는다 (`ClearRunner`). 90% 친 나무가 남는 게 아까워 보여도, "취소했는데
        /// 베어졌다"가 훨씬 나쁘다 — 되돌릴 수 없기 때문이다 (2026-08-10 Play 관측).
        /// </summary>
        public bool UnmarkForClearing(ResourceNode node)
        {
            if (node == null || !_clearPending.Remove(node)) return false;
            Debug.Log($"[Clear] 개간 취소 — ({node.TileX},{node.TileY}) {node.ResourceType}");
            return true;
        }

        /// <summary>아직 개간 대기 중인가 (ClearRunner 의 완료 직전 재확인 — 취소를 놓치지 않는다).</summary>
        public bool IsPendingClear(ResourceNode node) => node != null && _clearPending.Contains(node);

        /// <summary>해당 타일의 노드 (없으면 null) — 계획이 막힌 칸의 노드를 집어 지정할 때 쓴다.</summary>
        public ResourceNode NodeAt(int tileX, int tileY)
        {
            foreach (ResourceNode n in _nodes)
                if (n.TileX == tileX && n.TileY == tileY) return n;
            return null;
        }

        /// <summary>
        /// 가장 가까운 개간 대기 노드 (ClearRunner 전용). 없으면 null. 동률은 좌표순 (결정적).
        ///
        /// ⚠️ `IsHarvestable`을 **묻지 않는다** — 개간은 잔량과 무관하다. 잔량 0인 노드야말로
        /// 반드시 치워야 하는 노드이고, 여기서 거르면 다 캔 나무가 영영 남아 구멍이 안 닫힌다.
        /// 점유(`CurrentGatherers`)도 묻지 않는다: 개간은 그 노드의 마지막 일이다.
        /// </summary>
        public ResourceNode FindNearestClearPending(int fromX, int fromY)
        {
            ResourceNode best = null;
            int bestDist = int.MaxValue;
            foreach (ResourceNode n in _clearPending)
            {
                if (n == null || n.IsRemoved) continue;
                int d = Manhattan(n.TileX, n.TileY, fromX, fromY);
                if (d > bestDist) continue;
                if (d == bestDist && best != null
                    && (n.TileX, n.TileY).CompareTo((best.TileX, best.TileY)) >= 0) continue; // 동률 = 좌표순
                bestDist = d;
                best = n;
            }
            return best;
        }

        /// <summary>
        /// 노드를 세상에서 지우는 **유일한 문** (M22-4차 · ADR-C-2 — `ADR-M0-3` 계승).
        /// 회수량(내림한 잔량, ADR-C-6)을 돌려준다. 이미 없는 노드면 0이고 알림도 안 뜬다.
        ///
        /// 🔴 `_nodes.Remove(...)`를 **다른 곳에서 부르지 말 것**: 제거는 이력 기록·지정 해제·
        /// 점유 해제·뷰 소멸을 한꺼번에 끌고 다닌다. 문이 둘이면 반드시 한쪽이 빠뜨린다
        /// (게이트 `M22_T18`이 전수로 감시한다).
        /// </summary>
        public int RemoveNode(ResourceNode node)
        {
            if (node == null || !_nodes.Remove(node)) return 0;
            int recovered = Mathf.FloorToInt(Mathf.Max(0f, node.CurrentAmount));
            node.IsRemoved = true;                 // 뷰가 다음 Refresh 에 스스로 사라진다
            ResourceNode.OccupiedTiles.Remove(new Vector2Int(node.TileX, node.TileY));
            _clearPending.Remove(node);
            _cleared.Add(new Vector2Int(node.TileX, node.TileY));
            Debug.Log($"[Clear] 개간 — ({node.TileX},{node.TileY}) {node.ResourceType} 제거 · 회수 {recovered}");
            OnNodeRemoved?.Invoke(node, recovered);
            return recovered;
        }

        /// <summary>해당 타일에 자원 노드가 있는가 — 건설 위치 회피(BuildRunner) 질의.
        /// 고갈 노드도 점유로 본다 (재생 시스템이 되살리므로).</summary>
        public bool HasNodeAt(int tileX, int tileY)
        {
            foreach (ResourceNode n in _nodes)
                if (n.TileX == tileX && n.TileY == tileY) return true;
            return false;
        }

        /// <summary>중심 기준 맨해튼 반경 내 미발견 노드를 발견 처리. 발견 수를 반환한다.</summary>
        public int DiscoverArea(int centerX, int centerY, int radius)
        {
            int found = 0;
            foreach (ResourceNode n in _nodes)
            {
                if (n.IsDiscovered) continue;
                if (Manhattan(n.TileX, n.TileY, centerX, centerY) <= radius)
                {
                    n.IsDiscovered = true;
                    found++;
                }
            }
            return found;
        }

        /// <summary>
        /// 채집 가능 판정의 단일 기준: 잔량 ≥ 1 (수확 요구량과 동일 — 재생으로 0.x 차오르는
        /// 노드가 '선택은 되고 수확은 실패'하는 판정 불일치 방지) + 점유 여유.
        /// </summary>
        public static bool IsHarvestable(ResourceNode n)
            => n != null && n.CurrentAmount >= 1f && n.CurrentGatherers < n.MaxGatherers;

        /// <summary>
        /// 채집 가능한(잔량 + 점유 여유) 발견 노드 존재 여부 — 스냅샷 NearDiscovered* 슬롯의 원천.
        /// 점유 중인 노드를 '없음'으로 취급하는 것이 핵심: 덤불이 다 차면 플래너가
        /// Explore 체인으로 대체 계획을 세워 주민이 새 노드를 찾아 나선다 (자연 분산).
        /// </summary>
        public bool HasDiscovered(ResourceType type)
        {
            foreach (ResourceNode n in _nodes)
                if (n.ResourceType == type && n.IsDiscovered && IsHarvestable(n))
                    return true;
            return false;
        }

        /// <summary>가장 가까운 채집 가능(발견 + 잔량 + 점유 여유) 노드. 없으면 null.</summary>
        public ResourceNode FindNearestDiscovered(ResourceType type, int fromX, int fromY)
        {
            ResourceNode best = null;
            int bestDist = int.MaxValue;
            foreach (ResourceNode n in _nodes)
            {
                if (n.ResourceType != type || !n.IsDiscovered || !IsHarvestable(n)) continue;
                int d = Manhattan(n.TileX, n.TileY, fromX, fromY);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = n;
                }
            }
            return best;
        }

        /// <summary>가장 가까운 미발견 노드 (타입 무관) — ExploreRunner의 1순위 목표. 없으면 null.</summary>
        public ResourceNode FindNearestUndiscovered(int fromX, int fromY)
        {
            ResourceNode best = null;
            int bestDist = int.MaxValue;
            foreach (ResourceNode n in _nodes)
            {
                if (n.IsDiscovered) continue;
                int d = Manhattan(n.TileX, n.TileY, fromX, fromY);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = n;
                }
            }
            return best;
        }

        /// <summary>노드 재생 틱 (RegenerationRate = 게임일당, 기획서 수치는 ResourceNode 생성자가 자동 세팅).</summary>
        public void TickRegeneration(float deltaGameDays)
        {
            foreach (ResourceNode n in _nodes)
            {
                if (n.CurrentAmount >= n.MaxAmount) continue;
                // 개간 대기 노드는 재생하지 않는다 (M22-4차 ADR-C-7). 차오르면 회수량이 지연
                // 시간에 비례해 "늦게 칠수록 이득"이라는 이상한 전략이 생긴다.
                if (_clearPending.Contains(n)) continue;
                n.CurrentAmount = Mathf.Min(n.MaxAmount, n.CurrentAmount + n.RegenerationRate * deltaGameDays);
            }
        }

        private static int Manhattan(int x1, int y1, int x2, int y2)
            => Mathf.Abs(x1 - x2) + Mathf.Abs(y1 - y2);
    }
}
