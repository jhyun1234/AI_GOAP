using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 위험 기억 (M26-2차 W6) — **당해 보고 안다.**
    ///
    /// 🔑 **개인이 갖는다** (ADR-T2-4): 당한 사람만 안다. 마을 전체가 즉시 알면 정보가 공짜가
    /// 되고 한 명의 불운이 전원의 행동을 잠근다. 주인이 죽으면 기억도 함께 사라진다 —
    /// 그것이 상실의 대가다 (재미 건강 기준). 전달(대화로 퍼짐)은 `ChatterSO`가 있어 3차 몫.
    ///
    /// 🔑 **점 + 반경**으로 기억한다: 정확한 좌표만 기억하면 한 칸 옆으로 비켜서 똑같이 당한다.
    /// 당한 자리 둘레 반경 안이 전부 "위험했던 곳"이다.
    ///
    /// 🔑 **잊는 것은 시간이 한다** (선형 감쇠): 위험 기억이 영원하면 들판이 한 번의 사고로
    /// 영구 봉인된다 — 상주를 잡아 치웠는데도 아무도 안 가는 땅이 된다.
    /// 성격별 지속(겁쟁이는 오래, 용맹은 빨리)은 **3차 몫** — `decayMult` 인자가 그 자리다.
    ///
    /// **세이브 대상** (ADR-M0-10 선언): 욕구·위치와 같은 개인 상태다. 저장하지 않으면 로드
    /// 직후 어제 당한 자리로 태연히 걸어간다. 구현은 세이브 축에서.
    ///
    /// 🔴 슬롯이 아니다 — 플래너는 어느 노드인지 모르고(`ADR-M0-6` 플랜은 인덱스), 이 정보는
    /// 러너의 **노드 선택** 단계에서만 쓰인다 (`FindBestDiscovered`의 dangerAt). 예산 잔여 1칸도
    /// 건드리지 않는다 (ADR-T2-5).
    /// </summary>
    public sealed class DangerMemory
    {
        private struct Incident
        {
            public int X, Y;
            public float Strength;   // 남은 강도 — 감쇠가 깎고, 0 이하면 잊는다
        }

        private readonly List<Incident> _incidents = new List<Incident>(4);
        private readonly int _radiusTiles;
        private readonly float _decayPerDay;

        public DangerMemory(int radiusTiles, float decayPerDay)
        {
            _radiusTiles = Mathf.Max(1, radiusTiles);
            _decayPerDay = Mathf.Max(0f, decayPerDay);
        }

        /// <summary>당했다 — 이 자리를 기억한다. 같은 반경 안에서 또 당하면 강도가 쌓인다
        /// (합치지 않으면 한 자리에서 세 번 물린 기억이 목록 세 칸을 먹는다 — 뜻은 같은데 값만 다르다).</summary>
        public void Remember(int tileX, int tileY, float amount)
        {
            if (amount <= 0f) return;
            for (int i = 0; i < _incidents.Count; i++)
            {
                Incident inc = _incidents[i];
                if (Mathf.Abs(inc.X - tileX) + Mathf.Abs(inc.Y - tileY) > _radiusTiles) continue;
                inc.Strength += amount;
                _incidents[i] = inc;
                return;
            }
            _incidents.Add(new Incident { X = tileX, Y = tileY, Strength = amount });
        }

        /// <summary>이 칸의 위험 벌점 — `FindBestDiscovered`의 dangerAt에 그대로 꽂힌다.
        /// 기억이 없으면 0 = W4의 중립 불변식 그대로 (위험 항이 잠든다).</summary>
        public float PenaltyAt(int tileX, int tileY)
        {
            float sum = 0f;
            foreach (Incident inc in _incidents)
                if (Mathf.Abs(inc.X - tileX) + Mathf.Abs(inc.Y - tileY) <= _radiusTiles)
                    sum += inc.Strength;
            return sum;
        }

        /// <summary>시간이 잊게 한다. <paramref name="decayMult"/> = 성격 자리 (3차 몫 — 1이면 중립).</summary>
        public void Decay(float deltaGameDays, float decayMult = 1f)
        {
            if (deltaGameDays <= 0f || _decayPerDay <= 0f) return;
            float loss = _decayPerDay * deltaGameDays * Mathf.Max(0f, decayMult);
            for (int i = _incidents.Count - 1; i >= 0; i--)
            {
                Incident inc = _incidents[i];
                inc.Strength -= loss;
                if (inc.Strength <= 0f) _incidents.RemoveAt(i);
                else _incidents[i] = inc;
            }
        }

        /// <summary>감쇠 검산 (순수 — 게이트 `M26B_T6`): 시작 강도가 이 시간 뒤 얼마 남는가.
        /// 인스턴스 감쇠와 **같은 산수**여야 한다 — 다르면 게이트가 거짓을 지킨다.</summary>
        public static float RemainingAfter(float strength, float decayPerDay, float days)
            => Mathf.Max(0f, strength - decayPerDay * days);
    }
}
