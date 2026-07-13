using AIVillage.Core;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 채집 러너: 최근접 발견 노드로 이동 → 점유 → 수확.
    /// 수확량 = SO Effects의 스톡 Add 값 (플래너와 동일 수치, 단일 출처).
    /// 스톡 귀속(舊 DepositToStock)은 효과 적용에 내장 — 별도 운반 없음 (M0 단순화).
    /// </summary>
    public sealed class GatherRunner : ActionRunnerBase
    {
        private readonly GatherActionSO _so;
        private ResourceNode _node;

        public GatherRunner(GatherActionSO so) : base(so)
        {
            _so = so;
        }

        public override bool Prepare(VillagerAgent agent)
        {
            // 촌장이 특정 노드를 지목한 명령(M1-C)이면 그 노드 우선 — "저거 캐와"의 '저거'
            ResourceNode target = agent.OrderTargetNode;
            if (target != null && target.ResourceType == _so.TargetResource
                && target.IsDiscovered && DiscoveryService.IsHarvestable(target))
                _node = target;
            else
                _node = agent.Discovery.FindNearestDiscovered(_so.TargetResource, agent.TileX, agent.TileY);

            if (_node == null)
            {
                FailReason = $"발견된 {_so.TargetResource} 노드 없음";
                return false;
            }
            if (!_node.TryOccupy(agent.AgentId))
            {
                FailReason = $"{_so.TargetResource} 노드 점유 중 (다른 주민 채집)";
                _node = null;
                return false;
            }
            MoveTarget = new Vector2Int(_node.TileX, _node.TileY);
            return true;
        }

        public override RunnerResult Tick(VillagerAgent agent, float dt)
        {
            if (!DurationElapsed(dt)) return RunnerResult.Running;

            if (_node.CurrentAmount < 1f)
                return Fail($"{_so.TargetResource} 노드 고갈");

            // 노드 차감 — 스톡 증가는 에이전트의 EffectApplier가 같은 값으로 수행한다.
            // 잔량이 수확량보다 적어도 전량 수확 처리 (M0 단순화, 커밋 메시지에 명시).
            _node.CurrentAmount = Mathf.Max(0f, _node.CurrentAmount - HarvestAmount());
            return RunnerResult.Succeeded;
        }

        public override void Cleanup(VillagerAgent agent)
        {
            _node?.Release();
            _node = null;
        }

        private int HarvestAmount()
        {
            if (_so.Effects != null)
                foreach (SlotEffect e in _so.Effects)
                    if (e.Op == EffectOp.Add && SlotIds.IsStock(e.Slot))
                        return e.Value;
            return 0;
        }
    }
}
