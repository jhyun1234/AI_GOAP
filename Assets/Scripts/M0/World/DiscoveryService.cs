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

        /// <summary>잔량 있는 발견 노드 존재 여부 — 스냅샷 NearDiscovered* 슬롯의 원천.</summary>
        public bool HasDiscovered(ResourceType type)
        {
            foreach (ResourceNode n in _nodes)
                if (n.ResourceType == type && n.IsDiscovered && n.CurrentAmount >= 1f)
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
                if (n.ResourceType != type || !n.IsDiscovered || !n.IsAvailableForHarvest()) continue;
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
