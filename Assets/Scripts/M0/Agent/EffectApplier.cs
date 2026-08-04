using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// SO Effects를 실행 세계에 반영하는 유일한 해석기 —
    /// 플래너(ActionCompiler)와 같은 SlotEffect를 읽으므로 계획·실행 수치가 어긋날 수 없다 (결함 A 차단).
    ///
    /// 라우팅 (M11-A에서 4갈래):
    ///   ① 전역 스톡 슬롯(Wood/RawFood/Stone/Cooked) → WorldModel (Sub는 원자성 선검사)
    ///   ② My욕구 슬롯(MySatiety/MyFatigue) → 에이전트 개인 욕구 (0~100 클램프)
    ///   ③ 개인 스톡(MyRawFood/MyCookedFood) → VillagerAgent.ApplyPersonalStock (ADR-M11-1.
    ///      Sub 부족·Add 상한 초과 모두 선검사 — 부분 성공 금지. 선검사를 통과하고도 2단계가
    ///      실패하면 컴파일러 상한 주입 누락 버그라 경고를 낸다, ADR-M11-3 방어선)
    ///   ④ 집 저장(MyHomeRawFood/MyHomeCookedFood) → HomeStorageService (내 집 타일 경유.
    ///      무주택이면 실패 — 플래너 전제 MyHasHome==1이 정상 차단하므로 도달 = 에셋 결함)
    ///   논리형 슬롯(NearDiscovered*/CampfireBuilt/AtBuildSite) → 무시.
    ///     이들은 플래너 전용이다 — 실행 세계의 원천은 DiscoveryService(발견)와
    ///     ConstructionService(완공)이지 효과 적용이 아니다.
    /// </summary>
    public static class EffectApplier
    {
        /// <summary>원자적 적용: 차감·입고가 하나라도 불가면 아무 것도 반영하지 않고 false.</summary>
        public static bool TryApply(VillagerAgent agent, WorldModel world, List<SlotEffect> effects)
        {
            if (effects == null || effects.Count == 0) return true;

            // 집 저장 효과가 있으면 내 집 타일을 먼저 확정 (무주택 = 전체 실패, 부분 성공 금지)
            bool hasHome = false;
            Vector2Int homeTile = default;
            foreach (SlotEffect e in effects)
            {
                if (!SlotIds.IsHomeStock(e.Slot)) continue;
                hasHome = agent != null && agent.TryGetHomeTile(out homeTile);
                if (!hasHome || agent.HomeStorage == null)
                {
                    Debug.LogWarning($"[EffectApplier] {e.Slot} 효과인데 소유 집/저장 서비스 없음 — " +
                                     "전제 MyHasHome==1 누락 에셋 의심 (전체 실패).");
                    return false;
                }
                break;
            }

            // ── 1단계: 선검사 (전역 Sub / 개인 Sub·Add / 집 Sub·Add) ──
            foreach (SlotEffect e in effects)
            {
                if (SlotIds.IsStock(e.Slot))
                {
                    if (e.Op == EffectOp.SubClamp0 && world.GetStock(e.Slot) < e.Value) return false;
                }
                else if (SlotIds.IsPersonalStock(e.Slot))
                {
                    (bool ok, _) = VillagerAgent.NextPersonalStock(
                        agent.GetPersonalStock(e.Slot), e.Op, e.Value,
                        VillagerAgent.PersonalCapOf(e.Slot, agent.AgentConfig.BodyCarryCap)); // 돈 = 상한 없음 (M16)
                    if (!ok)
                    {
                        // Add 상한 초과로 여기 오면 컴파일러 주입이 있는 한 플랜이 성립하지 않았어야 한다
                        if (e.Op == EffectOp.Add)
                            Debug.LogWarning($"[EffectApplier] {agent.AgentId}: {e.Slot} +{e.Value} 상한 초과 — " +
                                             "컴파일러 상한 전제 주입 누락 의심 (ADR-M11-3 방어선 발동 = 버그).");
                        return false;
                    }
                }
                else if (SlotIds.IsHomeStock(e.Slot))
                {
                    HomeStorageService storage = agent.HomeStorage;
                    int cur = storage.GetStock(homeTile, e.Slot);
                    if (e.Op == EffectOp.SubClamp0 && cur < e.Value) return false;
                    if (e.Op == EffectOp.Add
                        && cur + e.Value > agent.AgentConfig.HomeStorageCap) // 슬롯별 — TryAdd·컴파일러 주입과 판정 단일
                    {
                        Debug.LogWarning($"[EffectApplier] {agent.AgentId}: {e.Slot} +{e.Value} 집 저장 상한 초과 — " +
                                         "컴파일러 상한 전제 주입 누락 의심 (ADR-M11-3 방어선 발동 = 버그).");
                        return false;
                    }
                }
            }

            // ── 2단계: 적용 (선검사 통과 보장 — 이후 실패는 로그로 드러나는 결함) ──
            foreach (SlotEffect e in effects)
            {
                if (SlotIds.IsStock(e.Slot))
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
                else if (SlotIds.IsPersonalStock(e.Slot))
                {
                    if (e.Op == EffectOp.Set)
                        Debug.LogWarning($"[EffectApplier] 개인 스톡 {e.Slot}에 Set 효과 — 지원하지 않아 무시.");
                    else if (agent.ApplyPersonalStock(e.Slot, e.Op, e.Value))
                        Debug.Log($"[Inventory] {agent.AgentId}: {Kr(e.Slot)} " +
                                  $"{(e.Op == EffectOp.Add ? "+" : "-")}{e.Value} → 소지 {agent.MyRaw}/{agent.MyCooked}");
                }
                else if (SlotIds.IsHomeStock(e.Slot))
                {
                    HomeStorageService storage = agent.HomeStorage;
                    bool applied = e.Op == EffectOp.Add
                        ? storage.TryAdd(homeTile, e.Slot, e.Value, agent.AgentConfig.HomeStorageCap)
                        : e.Op == EffectOp.SubClamp0 && storage.TrySpend(homeTile, e.Slot, e.Value);
                    if (applied)
                    {
                        (int raw, int cooked) = storage.Get(homeTile);
                        Debug.Log($"[Inventory] {agent.AgentId}: 집 {Kr(e.Slot)} " +
                                  $"{(e.Op == EffectOp.Add ? "+" : "-")}{e.Value} → 저장 {raw}/{cooked}");
                    }
                    else if (e.Op == EffectOp.Set)
                        Debug.LogWarning($"[EffectApplier] 집 저장 {e.Slot}에 Set 효과 — 지원하지 않아 무시.");
                }
                else if (e.Slot == SlotId.MySatiety || e.Slot == SlotId.MyFatigue)
                {
                    agent.ApplyNeedEffect(e.Slot, e.Op, e.Value);
                }
                // 논리형 슬롯: 플래너 전용 — 무시
            }
            return true;
        }

        private static string Kr(SlotId slot)
            => slot == SlotId.MyRawFood || slot == SlotId.MyHomeRawFood ? "생식" : "조리식";
    }
}
