using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 건설 액션. 비용·완공 플래그의 단일 출처는 BuildingSO다 —
    /// 이 SO의 Preconditions/Effects 배열에 건설 비용을 중복 기입하지 않는다.
    /// 플래너용 전제(재고 충분 + 미완공)와 효과(재고 차감 + 완공 플래그)는
    /// BuildingSO에서 파생 생성된다.
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Action/Build", fileName = "BuildAction")]
    public sealed class BuildActionSO : ActionSO
    {
        [Tooltip("지을 건물. 비용/완공 플래그는 이 BuildingSO가 단일 출처.")]
        public BuildingSO Building;

        public override void CollectPreconditions(List<SlotCondition> into)
        {
            base.CollectPreconditions(into);
            if (Building == null) return;

            // 미완공일 때만 후보 (무전제 액션 재적용 함정 방어 — 舊 버그21 계승).
            // 수량형(ADR-M2-3)은 중복 완공이 정상이라 이 전제를 생략 — goal 목표치(FarmPlotCount>=N)와
            // 건설 비용이 상한 역할을 한다.
            if (!Building.IsCountable)
                into.Add(new SlotCondition { Slot = Building.BuiltFlagSlot, Op = CompareOp.Equal, Value = 0 });

            foreach (ResourceCost c in Building.Costs)
                into.Add(new SlotCondition { Slot = c.StockSlot, Op = CompareOp.GreaterOrEqual, Value = c.Amount });
        }

        public override void CollectEffects(List<SlotEffect> into)
        {
            base.CollectEffects(into);
            if (Building == null) return;

            foreach (ResourceCost c in Building.Costs)
                into.Add(new SlotEffect { Slot = c.StockSlot, Op = EffectOp.SubClamp0, Value = c.Amount });

            if (Building.IsCountable)
                into.Add(new SlotEffect { Slot = Building.CountSlot, Op = EffectOp.Add, Value = 1 });
            else
                into.Add(new SlotEffect { Slot = Building.BuiltFlagSlot, Op = EffectOp.Set, Value = 1 });

            // 방어 계획 소진 (M22-W4) — 계획 타일에 짓는 건물은 계획 잔여를 1 줄인다. 플래너
            // 시야용 파생 효과 (goal이 DefensePlannedCount를 노린다) — 런타임 반영은
            // DefenseService.NotifyBuilt 구독 몫 (BuildRunner AppliesOwnEffects, 이중 기입 아님).
            if (Building.PlaceOnDefensePlan)
                into.Add(new SlotEffect { Slot = SlotId.DefensePlannedCount, Op = EffectOp.SubClamp0, Value = 1 });
        }

        public override IActionRunner CreateRunner(VillagerAgent agent) => new BuildRunner(this);

        protected override void OnValidate()
        {
            base.OnValidate(); // ADR-M18-1 Preconditions 검사 — 가리면 게이트 사각지대가 된다
            if (Building == null)
                Debug.LogError($"[BuildActionSO] {name}: Building이 비어 있습니다.", this);
        }
    }
}
