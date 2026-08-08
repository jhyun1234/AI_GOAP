using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 수리 러너 (M22-W6): 최다 손상 방어 시설로 가서 전량 복원한다 (한 걸음, ADR-M0-12).
    /// 내구도 쓰기는 DefenseService.Repair 문 하나 (ADR-M22-3), Wood 차감은 WorldModel 문
    /// (ADR-M0-3) — 그래서 AppliesOwnEffects = true (에셋 효과의 일괄 적용을 건너뛴다,
    /// BuildRunner 규약). 목수 1:5 격차는 RepairDurationMult가 만든다 (ADR-M22-7).
    /// </summary>
    public sealed class RepairRunner : ActionRunnerBase
    {
        private SlotId _slot;
        private Vector2Int _tile;

        public override bool AppliesOwnEffects => true;

        public RepairRunner(RepairActionSO so) : base(so) { }

        public override bool Prepare(VillagerAgent agent)
        {
            DefenseService defense = agent.Defense;
            if (defense == null || !defense.TryGetMostDamaged(out _slot, out _tile))
            {
                FailReason = "수리할 손상 시설 없음"; // goal 트리거(DefenseDamagedCount ≥ 1)가 이미 막지만 방어선
                return false;
            }
            DurationMult = agent.RepairDurationMultOfJob(); // 목수 0.2 = 1:5 (ADR-M22-7)

            MapBounds.Get(out int minX, out int maxX, out int minY, out int maxY);
            bool Occupied(int x, int y)
                => agent.Discovery.HasNodeAt(x, y) || agent.Construction.HasBuildingAt(x, y)
                || AIVillage.AI.TileReservationRegistry.IsReservedByOther(new Vector2Int(x, y), agent.AgentId);

            if (_slot == SlotId.FenceCount)
            {
                // 울타리는 통행 차단 — 곁에서 고친다 (차단 건물 시공 규약 ADR-M3-3 재사용)
                if (!BuildRunner.TryPickStandTile(Occupied, _tile, minX, maxX, minY, maxY, out Vector2Int stand))
                {
                    FailReason = "수리 시설 곁에 설 자리 없음";
                    return false;
                }
                MoveTarget = stand;
            }
            else
            {
                MoveTarget = _tile; // 문은 주민 통행 가능 (ADR-M22-5) — 그 자리에서 고친다
            }
            return true;
        }

        public override RunnerResult Tick(VillagerAgent agent, float dt)
        {
            if (!DurationElapsed(dt, DurationMult)) return RunnerResult.Running;

            DefenseService defense = agent.Defense;
            // 그새 파괴됐거나(공성) 남이 고쳤으면 실패 → 재계획 (대기 금지 — 동상 방지 규약)
            if (defense == null || !defense.TryGetDurability(_slot, _tile, out float cur, out float max)
                || cur >= max)
                return Fail("수리 대상이 사라졌거나 이미 멀쩡함 — 재계획");

            // 자재 선검사 후 일괄 차감 (원자성, ADR-M0-8) — 부족하면 실패, 부분 성공 없음
            defense.TryGetRepairCost(_slot, _tile, out int cost);
            if (cost > 0 && !agent.World.TrySpendStock(SlotId.WoodStock, cost))
                return Fail($"수리 자재 부족 (Wood {cost})");

            defense.Repair(_slot, _tile); // 내구도 쓰기 문 2 (ADR-M22-3) — 전량 복원
            return RunnerResult.Succeeded;
        }
    }
}
