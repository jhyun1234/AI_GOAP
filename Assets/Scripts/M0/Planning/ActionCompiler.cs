using System;
using System.Collections.Generic;
using AIVillage.Core.GOAP;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// ActionCatalog(SO 에셋) → GOAPPlannerJob이 소비하는 GOAPActionDef 배열 컴파일러.
    /// 시작 시 1회 실행. 舊 GOAPActionRegistry.BuildActionDefs(코드 상수)를 대체한다.
    ///
    /// ActionStringHash 필드에는 해시 대신 카탈로그 배열 인덱스를 넣는다 (ADR-M0-6) —
    /// 잡의 Backtrack이 이 값을 ResultActions에 그대로 쓰므로,
    /// 플랜 결과가 곧 카탈로그 인덱스가 되어 이름 해시 역매핑이 필요 없다.
    /// </summary>
    public static class ActionCompiler
    {
        /// <summary>GOAPActionDef의 전제/효과 필드 쌍 수 (Prec0~7, Eff0~7).</summary>
        public const int MAX_CONDITIONS = 8;

        /// <summary>카탈로그 전체를 def 배열로 컴파일한다. 항목 초과·빈 슬롯은 fail-fast (시작 시 1회이므로 예외 정당).
        /// bodyCap/homeCap > 0이면 개인/집 스톡 Add 효과에 상한 전제를 자동 주입한다 (ADR-M11-3 —
        /// 상한의 단일 출처는 AgentConfig, 액션 에셋에 상한 리터럴 기입 금지. 0 = 주입 없음 = 기존 동작).</summary>
        public static GOAPActionDef[] CompileManaged(ActionCatalog catalog, int bodyCap = 0, int homeCap = 0)
        {
            if (catalog == null || catalog.Actions == null || catalog.Actions.Length == 0)
                throw new InvalidOperationException("[ActionCompiler] 카탈로그가 비어 있습니다.");

            var defs  = new GOAPActionDef[catalog.Actions.Length];
            var precs = new List<SlotCondition>(MAX_CONDITIONS);
            var effs  = new List<SlotEffect>(MAX_CONDITIONS);

            for (int i = 0; i < catalog.Actions.Length; i++)
            {
                ActionSO so = catalog.Actions[i];
                if (so == null)
                    throw new InvalidOperationException($"[ActionCompiler] 카탈로그 {i}번 슬롯이 비어 있습니다.");

                precs.Clear();
                effs.Clear();
                so.CollectPreconditions(precs);
                so.CollectEffects(effs);
                InjectWageEffect(effs, so.WagePay);                    // M16-B — 임금을 플래너 시야에
                InjectCapPreconditions(precs, effs, bodyCap, homeCap); // M11-A — 상한 전제 자동 주입

                if (precs.Count > MAX_CONDITIONS || effs.Count > MAX_CONDITIONS)
                    throw new InvalidOperationException(
                        $"[ActionCompiler] '{so.name}': 전제 {precs.Count}/효과 {effs.Count}개 — 잡 한계 {MAX_CONDITIONS} 초과.");

                var def = new GOAPActionDef
                {
                    ActionStringHash = i, // ADR-M0-6: 인덱스 = 신원
                    BaseCost         = so.BaseCost,
                    PrecCount        = precs.Count,
                    EffectCount      = effs.Count,
                };
                for (int p = 0; p < precs.Count; p++) SetPrec(ref def, p, precs[p]);
                for (int e = 0; e < effs.Count; e++)  SetEff(ref def, e, effs[e]);
                defs[i] = def;
            }
            return defs;
        }

        /// <summary>
        /// 개인/집 스톡 상한 전제 자동 주입 (순수 — 게이트 M11-T1, ADR-M11-3): Add 효과의 슬롯이
        /// 개인 스톡이면 (슬롯 ≤ bodyCap-Add값), 집 저장이면 (슬롯 ≤ homeCap-Add값)을 전제에 더한다.
        /// 플래너가 상한을 알아야 "가득 찬 몸에 또 채집"하는 헛노동 플랜이 안 나온다 — 런타임
        /// 선검사(EffectApplier)와 같은 판정이라 어긋날 수 없다 (판정 단일).
        /// cap ≤ 0 = 주입 없음 (중립 — 미배선 테스트·기존 게이트웨이 동작 불변).
        /// </summary>
        /// <summary>
        /// 임금 효과 주입 (M16-B, 순수 — 게이트 M16-T9. ADR-M16-5 개정의 실행부):
        /// WagePay > 0인 액션에 MyMoney Add 효과를 **플래너 입력에만** 더한다.
        ///
        /// 왜 여기인가: 실행 지급은 TickActing의 Mint 한 곳뿐이고(ADR-M16-1), 그 경로는
        /// 원본 CollectEffects만 읽으므로 이중 지급이 구조적으로 불가능하다. 에셋의 단일
        /// 출처(WagePay 필드)도 그대로다 — 플래너만 "이 일을 하면 돈이 생긴다"를 알게 된다.
        ///
        /// 왜 개정했나 (ADR-M16-5 원본은 "화폐는 플래너 시야 밖"): 집을 화폐로만 살 수 있게
        /// 되면서(M16-B) 돈이 "있으면 좋은 것"에서 **집의 유일한 관문**이 됐다. 저축 goal이
        /// 목표를 세워도 돈 버는 길을 플래너가 모르면 NoSolution뿐이다 (Play 관측 2026-08-01:
        /// "돈이 없어도 돈을 벌러 가지 않는다"). 전제가 바뀌었으므로 규칙도 바뀐다.
        /// </summary>
        public static void InjectWageEffect(List<SlotEffect> effs, int wagePay)
        {
            if (wagePay <= 0) return; // 무급 = 중립 (기존 액션 전부 동작 불변)
            effs.Add(new SlotEffect { Slot = SlotId.MyMoney, Op = EffectOp.Add, Value = wagePay });
        }

        public static void InjectCapPreconditions(List<SlotCondition> precs, List<SlotEffect> effs,
                                                  int bodyCap, int homeCap)
        {
            foreach (SlotEffect e in effs)
            {
                if (e.Op != EffectOp.Add) continue;
                // 돈은 부피가 없다 (M16 — VillagerAgent.PersonalCapOf와 같은 판정). 상한을 붙이면
                // "지갑이 8동 넘으면 벌목 불가" 플랜이 나온다 (M16-B 구현 중 발견).
                if (e.Slot == SlotId.MyMoney) continue;
                int cap = SlotIds.IsPersonalStock(e.Slot) ? bodyCap
                        : SlotIds.IsHomeStock(e.Slot)     ? homeCap : 0;
                if (cap <= 0) continue;
                precs.Add(new SlotCondition { Slot = e.Slot, Op = CompareOp.LessOrEqual, Value = cap - e.Value });
            }
        }

        /// <summary>
        /// 슬롯별 최대 Add/Sub 효과량 산출 — 잡의 [S2] 부족량 휴리스틱 입력.
        /// (舊 GOAPActionRegistry.BuildMaxGainDrop을 M0 자립형으로 재작성 — 폐기 의존 축소)
        /// </summary>
        public static void ComputeMaxGainDrop(GOAPActionDef[] defs, int totalSlots, out float[] maxGain, out float[] maxDrop)
        {
            maxGain = new float[totalSlots];
            maxDrop = new float[totalSlots];
            foreach (GOAPActionDef def in defs)
            {
                for (int e = 0; e < def.EffectCount; e++)
                {
                    (int s, int v, int op) = GetEff(def, e);
                    if (s < 0 || s >= totalSlots) continue;
                    if (op == (int)EffectOp.Add       && v > maxGain[s]) maxGain[s] = v;
                    if (op == (int)EffectOp.SubClamp0 && v > maxDrop[s]) maxDrop[s] = v;
                }
            }
        }

        // ── GOAPActionDef 명시적 필드 접근 (Burst 호환 구조체 제약 해소용 인덱스 분기) ──

        private static void SetPrec(ref GOAPActionDef d, int i, SlotCondition c)
        {
            int s = (int)c.Slot, v = c.Value, op = (int)c.Op;
            switch (i)
            {
                case 0: d.Prec0S = s; d.Prec0V = v; d.Prec0Op = op; break;
                case 1: d.Prec1S = s; d.Prec1V = v; d.Prec1Op = op; break;
                case 2: d.Prec2S = s; d.Prec2V = v; d.Prec2Op = op; break;
                case 3: d.Prec3S = s; d.Prec3V = v; d.Prec3Op = op; break;
                case 4: d.Prec4S = s; d.Prec4V = v; d.Prec4Op = op; break;
                case 5: d.Prec5S = s; d.Prec5V = v; d.Prec5Op = op; break;
                case 6: d.Prec6S = s; d.Prec6V = v; d.Prec6Op = op; break;
                case 7: d.Prec7S = s; d.Prec7V = v; d.Prec7Op = op; break;
            }
        }

        private static void SetEff(ref GOAPActionDef d, int i, SlotEffect e)
        {
            int s = (int)e.Slot, v = e.Value, op = (int)e.Op;
            switch (i)
            {
                case 0: d.Eff0S = s; d.Eff0V = v; d.Eff0Op = op; break;
                case 1: d.Eff1S = s; d.Eff1V = v; d.Eff1Op = op; break;
                case 2: d.Eff2S = s; d.Eff2V = v; d.Eff2Op = op; break;
                case 3: d.Eff3S = s; d.Eff3V = v; d.Eff3Op = op; break;
                case 4: d.Eff4S = s; d.Eff4V = v; d.Eff4Op = op; break;
                case 5: d.Eff5S = s; d.Eff5V = v; d.Eff5Op = op; break;
                case 6: d.Eff6S = s; d.Eff6V = v; d.Eff6Op = op; break;
                case 7: d.Eff7S = s; d.Eff7V = v; d.Eff7Op = op; break;
            }
        }

        /// <summary>전제 항목 읽기 (테스트 라운드트립·디버그용).</summary>
        public static (int slot, int value, int op) GetPrec(in GOAPActionDef d, int i)
        {
            switch (i)
            {
                case 0: return (d.Prec0S, d.Prec0V, d.Prec0Op);
                case 1: return (d.Prec1S, d.Prec1V, d.Prec1Op);
                case 2: return (d.Prec2S, d.Prec2V, d.Prec2Op);
                case 3: return (d.Prec3S, d.Prec3V, d.Prec3Op);
                case 4: return (d.Prec4S, d.Prec4V, d.Prec4Op);
                case 5: return (d.Prec5S, d.Prec5V, d.Prec5Op);
                case 6: return (d.Prec6S, d.Prec6V, d.Prec6Op);
                case 7: return (d.Prec7S, d.Prec7V, d.Prec7Op);
                default: throw new ArgumentOutOfRangeException(nameof(i));
            }
        }

        /// <summary>효과 항목 읽기 (테스트 라운드트립·MaxGainDrop·W4 러너 효과 적용용).</summary>
        public static (int slot, int value, int op) GetEff(in GOAPActionDef d, int i)
        {
            switch (i)
            {
                case 0: return (d.Eff0S, d.Eff0V, d.Eff0Op);
                case 1: return (d.Eff1S, d.Eff1V, d.Eff1Op);
                case 2: return (d.Eff2S, d.Eff2V, d.Eff2Op);
                case 3: return (d.Eff3S, d.Eff3V, d.Eff3Op);
                case 4: return (d.Eff4S, d.Eff4V, d.Eff4Op);
                case 5: return (d.Eff5S, d.Eff5V, d.Eff5Op);
                case 6: return (d.Eff6S, d.Eff6V, d.Eff6Op);
                case 7: return (d.Eff7S, d.Eff7V, d.Eff7Op);
                default: throw new ArgumentOutOfRangeException(nameof(i));
            }
        }
    }
}
