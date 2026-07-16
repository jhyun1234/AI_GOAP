namespace AIVillage.M0
{
    /// <summary>
    /// 개체 편차용 결정적 해시 (FNV-1a 32bit — 플래너 잡과 같은 계열, ADR-M0-4 참조).
    ///
    /// string.GetHashCode()는 h×31+c 누적이라 꼬리 1글자만 다른 이름("…_A"/"…_B")의
    /// 해시가 1~4밖에 안 벌어져 % 기반 편차가 붕괴한다 — 2026-07-17 "동시 허기 웨이브"의
    /// 근본 원인 (씬 주민 5명의 초기 포만·속도·색 편차가 전부 사실상 동일했음).
    /// FNV-1a는 문자마다 XOR-곱 애벌랜치가 있어 인접 이름도 전 구간으로 흩어진다.
    /// salt로 용도(포만/감쇠/속도/색)를 분리해 같은 주민이라도 축마다 독립 편차를 얻는다.
    /// 결정적(재실행·플랫폼 무관)이므로 편차는 세이브 대상이 아니다 — 로드 후 재계산.
    /// </summary>
    public static class StableHash
    {
        public static uint Fnv1a(string text, string salt = null)
        {
            const uint OFFSET = 2166136261u;
            const uint PRIME = 16777619u;
            uint h = OFFSET;
            unchecked
            {
                if (text != null)
                    for (int i = 0; i < text.Length; i++) { h ^= text[i]; h *= PRIME; }
                if (salt != null)
                    for (int i = 0; i < salt.Length; i++) { h ^= salt[i]; h *= PRIME; }
            }
            return h;
        }

        /// <summary>[0, 1) 결정적 편차 (분해능 1/10000).</summary>
        public static float Value01(string id, string salt)
            => (Fnv1a(id, salt) % 10000u) / 10000f;

        /// <summary>[-1, 1] 결정적 편차.</summary>
        public static float Spread(string id, string salt)
            => Value01(id, salt) * 2f - 1f;
    }
}
