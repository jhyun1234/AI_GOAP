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
            // M20 — 자원별 효율. 나무꾼은 나무만, 광부는 돌만 빠르다 (자원별로 갈라야
            // 두 직업이 구분된다 — ADR-M20-2).
            DurationMult = agent.GatherDurationMultOfJob(_so.TargetResource);
            // 🔴 노드 **곁**에 선다 (2026-08-10 사용자 Play). 舊 코드는 목적지를 노드 타일 그
            // 자체로 줘서, 주민이 나무·바위와 겹친 채 도끼질했다. 오는 방향에서 가장 가까운
            // 옆칸이라 돌아가지 않는다 (전부 막혀 있으면 중심 폴백 = 배선 전과 동일).
            MoveTarget = MapBounds.PickWalkableAdjacent(agent.IsWalkable,
                                                        _node.TileX, _node.TileY,
                                                        agent.TileX, agent.TileY);
            return true;
        }

        public override RunnerResult Tick(VillagerAgent agent, float dt)
        {
            // 대상을 보고 팬다 (표현 전용) — 옆에 서게 됐으니 방향까지 맞아야 그림이 성립한다.
            // 매 틱 부르는 이유: 도착 직전 방향이 남아 있어 등을 돌린 채 도끼질할 수 있다.
            agent.FaceTowards(new Vector2(_node.TileX, _node.TileY));

            if (!DurationElapsed(dt, DurationMult)) return RunnerResult.Running;

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

        /// <summary>수확량 = SO Effects의 Add 값 — 전역 스톡(나무·돌·조리식) 또는 개인 스톡
        /// (열매 = MyRawFood, M11-A 이전분). ⚠️ 개인 스톡을 빠뜨리면 열매 노드가 안 줄어 무한 생성된다
        /// (M11 슬롯 이전 파급 누락 — 노드 고갈 무력화 근본 원인, 2026-07-24 수정).</summary>
        private int HarvestAmount()
        {
            if (_so.Effects != null)
                foreach (SlotEffect e in _so.Effects)
                    if (e.Op == EffectOp.Add && (SlotIds.IsStock(e.Slot) || SlotIds.IsPersonalStock(e.Slot)))
                        return e.Value;
            return 0;
        }
    }
}
