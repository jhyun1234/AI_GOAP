using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 성격 서술 (M25-1차) — 6축 벡터를 사람이 읽는 2~3문장으로 옮긴다. **순수·결정적.**
    ///
    /// 🔑 조합표는 없다 (`ADR-M25-1`). 축마다 적힌 문장(`TraitRulesSO.TraitLines`)을 골라
    /// 순서를 세우고, 어조가 갈리는 첫 지점에서 **한 번만** 역접으로 잇는다. 두 번 이으면
    /// "…인데 …인데 …인데"가 되어 오히려 안 읽힌다.
    ///
    /// 🔑 이 축이 존재하는 이유: 성격은 **실재했지만 감지할 수 없었다.** 화면이 "고집쟁이"
    /// 한 단어로 접어 버려서 플레이어에게는 그냥 랜덤으로 보였다 (2026-08-10 사용자 관측).
    /// 여기서 만드는 것은 새 데이터가 아니라 **접힌 것을 펴는 일**이다.
    ///
    /// ⚠️ 표현 전용이다 — 판정(goal 선택·비용·문턱·직업 배정)은 한 글자도 읽지 않는다 (`ADR-M25-6`).
    /// </summary>
    public static class TraitProse
    {
        /// <summary>축 문장 한 칸 — 정렬·역접 판정에 필요한 것만 들고 다닌다.</summary>
        private struct Picked
        {
            public int Abs;          // |축값| — 1차 정렬 키
            public TraitId Trait;    // 동률 시 2차 키 (결정성 보장)
            public TraitLine Line;
        }

        /// <summary>
        /// 축 문장들 (순수 — 게이트 M25-T1~T5). 미배선(rules·traits null / TraitLines 빈 배열)이면
        /// **빈 문자열** = 배선 전과 동일 (중립 불변식).
        ///
        /// 순서 = |축값| 내림차순. **동률이면 어조가 앞 문장과 다른 쪽을 먼저** 세운다 —
        /// 대조가 사람을 그리기 때문이다. 그래도 갈리지 않으면 `TraitId` 오름차순 (결정성).
        /// </summary>
        public static string FromAxes(TraitValue[] traits, TraitRulesSO rules)
        {
            if (traits == null || rules == null || rules.TraitLines == null
                || rules.TraitLines.Length == 0) return "";

            // ① 침묵 문턱을 넘은 축만 — 문턱 판정은 LineFor 가 소유한다 (판정 이원화 금지)
            var picked = new List<Picked>(6);
            for (int i = 0; i < traits.Length; i++)
            {
                TraitLine? line = rules.LineFor(traits[i].Trait, traits[i].Value);
                if (line.HasValue)
                    picked.Add(new Picked
                    {
                        Abs = Mathf.Abs(traits[i].Value),
                        Trait = traits[i].Trait,
                        Line = line.Value,
                    });
            }
            if (picked.Count == 0) return "";

            // ② |값| 내림차순 · 동률은 TraitId 오름차순 (여기까지가 결정적 뼈대)
            picked.Sort((a, b) => a.Abs != b.Abs ? b.Abs.CompareTo(a.Abs)
                                                 : ((int)a.Trait).CompareTo((int)b.Trait));

            // ③ 대조 우선 — 동률 구간 안에서, 앞 문장과 어조가 다른 것을 앞으로 당긴다.
            //    🔴 이 단계가 없으면 고집쟁이의 역접이 사라진다: 사교 −40(부)과 근면 +40(긍)이
            //    동률인데 TraitId 순으로만 자르면 근면이 먼저 서고, 앞 문장들과 어조가 같아
            //    역접이 안 걸린 채 상한 3에 사교가 잘려 나간다 (명세 §4.3 검산 중 발견).
            //    맞바꾸기는 **동률 구간 안에서만** 하므로 |값| 내림차순은 깨지지 않는다.
            for (int i = 1; i < picked.Count; i++)
            {
                ProseTone prev = picked[i - 1].Line.Tone;
                if (prev == ProseTone.Neutral) continue;
                if (picked[i].Line.Tone != prev) continue;   // 이미 대조 — 손대지 않는다
                for (int j = i + 1; j < picked.Count && picked[j].Abs == picked[i].Abs; j++)
                {
                    ProseTone cand = picked[j].Line.Tone;
                    if (cand == ProseTone.Neutral || cand == prev) continue;
                    Picked swap = picked[i];
                    picked[i] = picked[j];
                    picked[j] = swap;
                    break;
                }
            }

            // ④ 상한 — 여섯 축을 다 읊으면 사람이 안 보인다
            return Join(picked, Mathf.Clamp(rules.ProseMaxAxisLines, 1, picked.Count));
        }

        /// <summary>문장 잇기 — 어조가 갈리는 **첫 지점에서 한 번만** 역접, 나머지는 마침표.</summary>
        private static string Join(List<Picked> picked, int count)
        {
            var sb = new StringBuilder(96);
            bool adversativeUsed = false;
            for (int i = 0; i < count; i++)
            {
                TraitLine cur = picked[i].Line;
                if (i > 0 && !adversativeUsed)
                {
                    TraitLine prev = picked[i - 1].Line;
                    if (prev.Tone != ProseTone.Neutral && cur.Tone != ProseTone.Neutral
                        && cur.Tone != prev.Tone && !string.IsNullOrEmpty(prev.Adversative))
                    {
                        // 앞 문장은 이미 "종결형. " 으로 닫혀 있다 — 그만큼 되감고 역접형으로 다시 쓴다.
                        sb.Length -= prev.Ending.Length + 2;
                        sb.Append(prev.Adversative).Append(", ");
                        adversativeUsed = true;
                    }
                }
                sb.Append(cur.Ending).Append(". ");
            }
            return sb.ToString().TrimEnd();
        }
    }
}
