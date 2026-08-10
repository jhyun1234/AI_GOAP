using AIVillage.Core;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 개간 러너 (M22-4차 W4) — 담을 막고 선 자원 노드를 치우고, 남은 잔량을 회수한다.
    ///
    /// 제거는 `DiscoveryService.RemoveNode` **한 문**을 지난다 (ADR-C-2) — 이력·지정 해제·
    /// 점유 해제·뷰 소멸이 거기 다 매달려 있다. 스톡 적립은 회수량이 노드마다 달라
    /// 에셋 효과로 못 적으므로 러너가 직접 넣는다 (`AppliesOwnEffects = true`, CombatService
    /// 전리품과 같은 규약).
    ///
    /// 🔴 **소요 시간은 잔량에 비례한다** (ADR-C-6). 회수를 전부 주면서 소요를 채집 1회로 두면
    /// 개간이 **5배 빠른 벌목**이 되어, 플레이어가 담을 세우려고가 아니라 **자원을 캐려고**
    /// 마을 둘레에 줄을 긋는다. 두 수치를 각각 합리적으로 정했는데 **곱해 보니** 무너진 자리다.
    /// </summary>
    public sealed class ClearRunner : ActionRunnerBase
    {
        private ResourceNode _node;
        private int _recovered;

        public override bool AppliesOwnEffects => true;

        public ClearRunner(ClearActionSO so) : base(so) { }

        public override bool Prepare(VillagerAgent agent)
        {
            _node = agent.Discovery != null
                  ? agent.Discovery.FindNearestClearPending(agent.TileX, agent.TileY) : null;
            if (_node == null)
            {
                FailReason = "개간 대기 노드 없음"; // goal 트리거(ClearPendingCount ≥ 1)가 이미 막지만 방어선
                return false;
            }

            // 자원별 직업 효율은 채집과 **같은 자**를 쓴다 (ADR-M20-2) — 나무꾼은 나무를,
            // 광부는 돌을 빨리 친다. 개간이라고 다른 표를 만들면 직업 축이 둘로 갈린다.
            DurationMult = agent.GatherDurationMultOfJob(_node.ResourceType) * WorkMult(agent, _node);

            // 노드 **곁**에 선다 — 채집과 같은 규약 (겹쳐 서기 금지, 2026-08-10 사용자 Play).
            MoveTarget = MapBounds.PickWalkableAdjacent(agent.IsWalkable,
                                                        _node.TileX, _node.TileY,
                                                        agent.TileX, agent.TileY);
            return true;
        }

        public override RunnerResult Tick(VillagerAgent agent, float dt)
        {
            // 대상을 보고 팬다 (표현 전용) — 옆에 서게 됐으니 방향까지 맞아야 그림이 성립한다.
            agent.FaceTowards(new Vector2(_node.TileX, _node.TileY));

            if (!DurationElapsed(dt, DurationMult)) return RunnerResult.Running;

            // 그 사이 남이 먼저 쳤을 수 있다 — 사라진 노드를 친 것은 실패다 (조용한 성공 금지).
            if (_node.IsRemoved) return Fail("이미 개간된 노드 — 재계획");

            _recovered = agent.Discovery.RemoveNode(_node);   // 유일한 문 (ADR-C-2)
            SlotId? stock = SlotIds.StockOf(_node.ResourceType);
            if (stock.HasValue && _recovered > 0)
                agent.World.AddStock(stock.Value, _recovered); // 스톡 쓰기 문 (ADR-M0-3)
            return RunnerResult.Succeeded;
        }

        /// <summary>
        /// 잔량 비례 작업 배수 (ADR-C-6 착취 방지) — 개간 1회의 자원 효율이 채집과 **정확히
        /// 같아지게** 만든다. 꽉 찬 나무는 오래 걸리고 다 캔 그루터기는 금방 치워진다.
        ///
        /// 🔴 배수의 두 항(1회 수확량·1회 소요)은 **에셋에서 읽는다** (ADR-M0-2). 실측상 나무도
        /// 돌도 마침 5회라 상수 5를 적고 싶어지지만, 적는 순간 에셋을 고칠 때 착취가 조용히
        /// 되살아난다 (게이트 `M22_T25`가 이 유혹을 막는다).
        ///
        /// 짝이 되는 채집 액션을 못 찾으면 1배 — 배선 전과 같은 동작(중립)이고, 그 판에서는
        /// 개간이 채집보다 이득이지만 애초에 채집이 없는 자원이라 비교 대상이 없다.
        /// </summary>
        private float WorkMult(VillagerAgent agent, ResourceNode node)
        {
            float ownSec = Action != null ? Action.DurationSec : 0f;
            if (ownSec <= 0f) return 1f;   // 0초짜리 액션 — 나눌 수 없다 (에셋 사고 방어)

            GatherActionSO gather = FindGatherFor(agent, node.ResourceType);
            if (gather == null) return 1f;

            int yield = HarvestAmountOf(gather);
            if (yield <= 0) return 1f;

            int reps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0f, node.CurrentAmount) / yield));
            float gatherSec = gather.DurationSec > 0f ? gather.DurationSec : ownSec;
            // 총 작업시간 = 채집 1회 시간 × 반복수 → 그것을 이 액션의 기준 시간으로 환산.
            return gatherSec * reps / ownSec;
        }

        /// <summary>이 자원을 캐는 채집 액션 (카탈로그에서 찾는다 — 코드에 이름을 안 적는다).</summary>
        private static GatherActionSO FindGatherFor(VillagerAgent agent, ResourceType type)
        {
            ActionCatalog catalog = agent.Catalog;
            if (catalog?.Actions == null) return null;
            foreach (ActionSO a in catalog.Actions)
                if (a is GatherActionSO g && g.TargetResource == type) return g;
            return null;
        }

        /// <summary>채집 1회 수확량 — `GatherRunner.HarvestAmount`와 **같은 읽기 규칙**이다
        /// (규칙이 둘로 갈리면 개간과 채집의 효율이 조용히 어긋난다).</summary>
        private static int HarvestAmountOf(GatherActionSO so)
        {
            if (so.Effects == null) return 0;
            foreach (SlotEffect e in so.Effects)
                if (e.Op == EffectOp.Add && (SlotIds.IsStock(e.Slot) || SlotIds.IsPersonalStock(e.Slot)))
                    return e.Value;
            return 0;
        }
    }
}
