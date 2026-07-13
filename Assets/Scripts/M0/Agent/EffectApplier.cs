using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// SO Effects를 실행 세계에 반영하는 유일한 해석기 —
    /// 플래너(ActionCompiler)와 같은 SlotEffect를 읽으므로 계획·실행 수치가 어긋날 수 없다 (결함 A 차단).
    ///
    /// 라우팅:
    ///   스톡 슬롯(WoodStock/RawFoodStock) → WorldModel (Sub는 원자성 선검사)
    ///   My* 슬롯 → 에이전트 개인 욕구 (0~100 클램프)
    ///   논리형 슬롯(NearDiscovered*/CampfireBuilt/AtBuildSite) → 무시.
    ///     이들은 플래너 전용이다 — 실행 세계의 원천은 DiscoveryService(발견)와
    ///     ConstructionService(완공)이지 효과 적용이 아니다.
    /// </summary>
    public static class EffectApplier
    {
        /// <summary>원자적 적용: 스톡 차감이 하나라도 부족하면 아무 것도 반영하지 않고 false.</summary>
        public static bool TryApply(VillagerAgent agent, WorldModel world, List<SlotEffect> effects)
        {
            if (effects == null || effects.Count == 0) return true;

            // ── 1단계: 스톡 차감 선검사 ──
            foreach (SlotEffect e in effects)
            {
                if (!IsStock(e.Slot) || e.Op != EffectOp.SubClamp0) continue;
                if (world.GetStock(e.Slot) < e.Value) return false;
            }

            // ── 2단계: 적용 ──
            foreach (SlotEffect e in effects)
            {
                if (IsStock(e.Slot))
                {
                    switch (e.Op)
                    {
                        case EffectOp.Add:       world.AddStock(e.Slot, e.Value); break;
                        case EffectOp.SubClamp0: world.TrySpendStock(e.Slot, e.Value); break; // 선검사 통과 보장
                        case EffectOp.Set:
                            Debug.LogWarning($"[EffectApplier] 스톡 슬롯 {e.Slot}에 Set 효과 — 지원하지 않아 무시.");
                            break;
                    }
                }
                else if (e.Slot == SlotId.MySatiety || e.Slot == SlotId.MyFatigue)
                {
                    agent.ApplyNeedEffect(e.Slot, e.Op, e.Value);
                }
                // 논리형 슬롯: 플래너 전용 — 무시
            }
            return true;
        }

        private static bool IsStock(SlotId slot)
            => slot == SlotId.WoodStock || slot == SlotId.RawFoodStock;
    }
}
