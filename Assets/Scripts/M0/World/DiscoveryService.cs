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
                n.CurrentAmount = Mathf.Min(n.MaxAmount, n.CurrentAmount + n.RegenerationRate * deltaGameDays);
            }
        }

        private static int Manhattan(int x1, int y1, int x2, int y2)
            => Mathf.Abs(x1 - x2) + Mathf.Abs(y1 - y2);
    }
}
