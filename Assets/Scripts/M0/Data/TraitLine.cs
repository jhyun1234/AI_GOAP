using System;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 서술의 어조 (M25-1차) — 대조를 만들 때만 쓰인다.
    /// 🔴 **부호로 유도하지 말 것**: 겁 ＋(조심)는 약점이고 겁 −(담대)는 강점이다.
    /// 축의 부호와 어조는 별개라서 사람이 적는다.
    /// </summary>
    public enum ProseTone
    {
        Neutral  = 0, // 강점도 약점도 아님 (모험 축) — 역접을 걸지 않는다
        Positive = 1,
        Negative = 2,
    }

    /// <summary>
    /// 성격 서술 한 칸 (M25-1차) — "이 축이 이 방향·이 강도일 때 사람은 이렇게 말해진다".
    ///
    /// 🔑 **조합이 아니라 축마다 적는다** (`ADR-M25-1`). 축 6개 × 3상태 = 729가지, 강도까지
    /// 세면 5⁶ = 15,625가지라 조합표는 사람이 적을 수 있는 양이 아니다. 이것은 M12가 이미
    /// 푼 함정과 같은 모양이고(`O(성격×goal)` → `O(성격+goal)`), 답도 같다:
    /// **데이터는 축마다, 조합은 계산이 만든다.** 축을 하나 더해도 4칸이 늘 뿐이다.
    /// </summary>
    [Serializable]
    public struct TraitLine
    {
        public TraitId Trait;

        [Tooltip("참 = 축값이 양수일 때, 거짓 = 음수일 때")]
        public bool Positive;

        [Tooltip("참 = 단정형(|값| ≥ ProseStrongAt), 거짓 = 완곡형")]
        public bool Strong;

        [Tooltip("종결형 — 예: \"남을 돕는 걸 좋아한다\"")]
        public string Ending;

        [Tooltip("역접 연결형 — 예: \"남을 돕는 걸 좋아하는데\".\n" +
                 "🔴 어미는 코드가 만들지 않는다 (ADR-M25-4): \"낫다\"의 역접은 \"낫는데\"가 " +
                 "아니라 \"낫지만\"이다. 한국어 어미를 코드로 굴리면 반드시 어색해진다.")]
        public string Adversative;

        [Tooltip("어조 — 앞 문장과 갈릴 때 역접으로 잇는다. Neutral은 역접을 걸지 않는다.")]
        public ProseTone Tone;
    }
}
