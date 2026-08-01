using System.Collections.Generic;
using AIVillage.Core;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 전역 수치 상태(스톡·완공 플래그)의 유일한 쓰기 지점 (ADR-M0-3).
    /// My*(포만감/피로)는 에이전트 개인 소유라 여기 없다 — 스냅샷 생성 시 인자로 합쳐진다.
    /// 완공 플래그 쓰기는 ConstructionService.Complete()만 호출한다 (그 외 호출 금지).
    /// </summary>
    public sealed class WorldModel
    {
        /// <summary>식량 일수 중립·상한 관례값 (M9-G) — SeasonService.NO_CRISIS와 같은 99.
        /// 인원 0·미배선이면 이 값 = "≤N" 트리거 전부 불발 (기존 동작).</summary>
        public const int NO_ESTIMATE = 99;

        private readonly int[] _slots = new int[SlotIds.Count];
        private readonly DiscoveryService _discovery;
        private readonly FarmService _farm;
        private readonly SeasonService _season;

        // 식량 수지 (M9-G, M11-D 개인화) — 가치표는 생성 시 1회 파생·캐시 (틱마다 액션 스캔 금지).
        // 마을 인원(aliveCount)은 M11-D에서 산식 입력에서 제거됨 — 식량은 개인 단위다.
        private readonly (SlotId slot, int gain)[] _foodValues;
        private readonly float _decayPerDay;            // AgentConfig.SatietyDecayPerGameDay
        private readonly float _farmGrowthDays;         // 심기 창 판정 (M14) — 심고 익는 데 필요한 일수. 미배선 0 = 창 항상 충족(중립)
        private readonly System.Func<int> _injuredCount; // 부상 주민 수 (M10-A) — 원천 = SimulationLoop.CountInjured
        private readonly System.Func<int> _untendedInjuredCount; // 미안정 부상자 수 (M11-I) — SimulationLoop.CountUntendedInjured

        public WorldModel(DiscoveryService discovery, WorldConfigSO config, FarmService farm = null,
                          SeasonService season = null, AgentConfigSO agentCfg = null,
                          System.Func<int> injuredCount = null,
                          System.Func<int> untendedInjuredCount = null)
        {
            _discovery = discovery;
            _farm = farm;
            _season = season;
            _injuredCount = injuredCount;
            _untendedInjuredCount = untendedInjuredCount;
            _decayPerDay = agentCfg != null ? agentCfg.SatietyDecayPerGameDay : 0f;
            if (config != null)
            {
                _slots[(int)SlotId.WoodStock]    = config.InitialWoodStock;
                _slots[(int)SlotId.RawFoodStock] = config.InitialRawFoodStock;
                _slots[(int)SlotId.StoneStock]   = config.InitialStoneStock;
                _foodValues = DeriveFoodValues(config.FoodSources); // ADR-M9-10 — 액션 에셋에서 파생
                _farmGrowthDays = config.FarmGrowthDays;            // 심기 창 판정 입력 (M14-W1)
                // 시작 금고 (M17-W1) — 무에서 온 돈이므로 발행 누적에도 같이 싣는다.
                // 안 그러면 폐곡선(§8 D2)이 첫 프레임부터 어긋난다.
                Treasury    = Mathf.Max(0, config.StartingTreasury);
                MintedTotal = Treasury;
            }
        }

        /// <summary>FoodSources → (스톡 슬롯, 1개당 포만) 가치표 (M9-G). 식량 아닌 항목은 건너뛴다.</summary>
        private static (SlotId slot, int gain)[] DeriveFoodValues(ConsumeActionSO[] sources)
        {
            if (sources == null || sources.Length == 0) return null;
            var list = new List<(SlotId, int)>(sources.Length);
            foreach (ConsumeActionSO a in sources)
                if (TryGetFoodValue(a, out SlotId slot, out int gain)) list.Add((slot, gain));
            return list.Count > 0 ? list.ToArray() : null;
        }

        /// <summary>심기 창 판정 (순수 — 게이트 M14-T1): "지금 심으면 봉쇄 전에 익는다".
        /// 계절 성장 배율(가을 1.2)로 나누는 정밀화는 하지 않는다 — 보수적 기본 성장일수 기준 (명세 W1).
        /// GrowthMult 0(겨울) 차단 항이 없으면 겨울 중 "다음 겨울까지 8일"로 창이 열린다 (명세 ⚠️W1-②).</summary>
        public static bool ComputePlantWindow(float growthMult, float daysToFreeze, float farmGrowthDays)
            => growthMult > 0f && daysToFreeze >= farmGrowthDays;

        public int GetStock(SlotId slot) => _slots[(int)slot];

        /// <summary>스톡 증감. 음수 결과는 0 클램프 + 경고 (舊 B26 계승).</summary>
        public void AddStock(SlotId slot, int amount)
        {
            int result = _slots[(int)slot] + amount;
            if (result < 0)
            {
                Debug.LogWarning($"[WorldModel] {slot} 증감 결과 음수({result}) — 0으로 클램프. 호출 경로 점검 필요.");
                result = 0;
            }
            _slots[(int)slot] = result;
        }

        /// <summary>차감 시도. 부족하면 아무 것도 바꾸지 않고 false (원자성, 舊 ADR-2 계승).</summary>
        public bool TrySpendStock(SlotId slot, int amount)
        {
            if (_slots[(int)slot] < amount) return false;
            _slots[(int)slot] -= amount;
            return true;
        }

        public bool GetFlag(SlotId slot) => _slots[(int)slot] != 0;

        // ── 화폐 회계 (M17-W1 — ADR-M17-2: 쓰기 창구는 아래 네 메서드뿐) ──────────
        // 舊 ADR-M16-1(Mint·Burn 둘)의 개정판이다. 금고가 생기며 "무에서 온 돈"과
        // "금고에서 옮겨온 돈"을 구분해야 폐곡선 검산이 성립한다 (명세 M17 보완 1).

        /// <summary>통화량 M = 주민 지갑의 총합. 물가(P = M÷Q)의 분자. 세이브 대상 (ADR-M0-10).
        /// 주민 간 이동(TransferTo)은 M 불변 — 이 값은 "마을에 도는 돈의 총량"이다.
        /// ⚠️ 금고(Treasury)는 여기 안 들어간다 — 잠긴 돈은 시중에 없는 돈이다 (ADR-M17-1).</summary>
        public int MoneySupply { get; private set; }

        /// <summary>촌장 금고 (M17-W1, ADR-M17-1) — 통화량 밖의 자리. 웃돈은 여기서 나가고
        /// 원천징수·발행이 여기를 채운다. 금고가 비면 촌장은 웃돈을 걸 수 없다 = 압박.
        /// 세이브 대상 (ADR-M0-10).</summary>
        public int Treasury { get; private set; }

        /// <summary>발행·소멸 누적 (M17-W1) — 폐곡선 검산 전용 (명세 §8 D2). 게임 규칙에
        /// 쓰지 않는다: 물가는 M과 MintDebt만 본다. 세이브 대상 (ADR-M0-10).</summary>
        public int MintedTotal { get; private set; }
        public int BurnedTotal { get; private set; }

        /// <summary>촌장이 **찍어서** 만든 돈의 누적 (M17-W3) — 연대기 기록용 (W6).
        /// ⚠️ MintedTotal과 다르다: 그쪽은 임금·원천징수까지 포함하므로 "찍어서 버텼다"를
        /// 왜곡한다. 이 값만이 발행 버튼을 몇 번 눌렀는지를 말한다. 세이브 대상.</summary>
        public int IssuedTotal { get; private set; }

        /// <summary>발행 부채 (M17-W3, ADR-M17-4) — 찍은 돈이 물가에 남기는 여파.
        /// 발행 순간 액수만큼 붙고 하루마다 일정 비율 감쇠한다. **쓰지 않고 쌓아만 둬도 아프다**:
        /// "찍었다"는 사실 자체가 돈값을 떨어뜨린다는 것이 이 축의 전부다.
        /// ⚠️ 실물 돈이 아니다 — 폐곡선 항등식(§8 D2)에 들어가지 않는다. MoneySupply에
        /// 더하지 않는다. 물가 산식의 항으로만 쓰인다 (W4). 세이브 대상.</summary>
        public int MintDebt { get; private set; }

        /// <summary>회계 폐곡선 (순수 — 게이트 M17-T5). 무에서 나온 총량은 언제나
        /// "도는 돈 + 금고 + 소멸한 돈"과 같다. 다섯 번째 쓰기 경로가 생기면 여기서 깨진다.</summary>
        public static bool IsLedgerBalanced(int minted, int supply, int treasury, int burned)
            => minted == supply + treasury + burned;

        /// <summary>금고 지급 가능 판정 (순수 — 게이트 M17-T5). 촌장은 없는 돈을 약속하지 못한다.</summary>
        public static bool CanPayFromTreasury(int treasury, int amount)
            => amount > 0 && treasury >= amount;

        /// <summary>통화량 계단 (순수 — 게이트 M16-T2). 음수 클램프 — 소멸이 발행을 넘으면 0.</summary>
        public static int NextSupply(int current, int delta)
            => current + delta < 0 ? 0 : current + delta;

        /// <summary>발행: 무 → 주민 (ADR-M17-2 ①). 지갑 적립과 M 누적이 한 몸.
        /// 호출처는 임금(TickActing) 1곳뿐 — M17-W1에서 웃돈이 금고 경로로 옮겨 갔다.
        /// 적립 실패(이론상 불가 — 돈은 상한 없음)면 M도 발행 누적도 안 늘린다.</summary>
        public bool Mint(VillagerAgent to, int amount, string why)
        {
            if (to == null || amount <= 0) return false;
            if (!to.ApplyPersonalStock(SlotId.MyMoney, EffectOp.Add, amount))
            {
                Debug.LogWarning($"[Money] 발행 실패 — {why}: 지갑 적립 불가 (상한 예외 누락 의심)");
                return false;
            }
            MoneySupply  = NextSupply(MoneySupply, amount);
            MintedTotal += amount;
            Debug.Log($"[Money] +{amount}동 {to.AgentId} — {why} (통화량 {MoneySupply})");
            return true;
        }

        /// <summary>발행: 무 → 금고 (M17-W1, ADR-M17-2 ②). 임금의 원천징수분과 촌장의 발행이
        /// 이 통로를 지난다. M은 움직이지 않는다 — 시중에 나가지 않았기 때문이다 (ADR-M17-1).</summary>
        public void MintToTreasury(int amount, string why)
        {
            if (amount <= 0) return;
            Treasury    += amount;
            MintedTotal += amount;
            Debug.Log($"[Money] 금고 +{amount}동 — {why} (금고 {Treasury} · 통화량 {MoneySupply})");
        }

        /// <summary>지급: 금고 → 주민 (M17-W1, ADR-M17-2 ③). 웃돈의 유일한 통로.
        /// 무에서 나온 돈이 아니므로 발행 누적은 늘지 않고, 시중에 나가므로 M은 는다.
        /// 잔고 부족이면 false — 조용히 넘기지 않는다(호출자가 알린다, 명세 W1 ⚠️).
        /// ⚠️ 적립 성공 뒤에 차감한다 — 순서를 뒤집으면 적립 실패 시 돈이 증발한다 (ADR-M0-8).</summary>
        public bool PayFromTreasury(VillagerAgent to, int amount, string why)
        {
            if (to == null || amount <= 0) return false;
            if (!CanPayFromTreasury(Treasury, amount))
            {
                Debug.Log($"[Money] 금고 부족 — {why} (필요 {amount}동 · 금고 {Treasury}동)");
                return false;
            }
            if (!to.ApplyPersonalStock(SlotId.MyMoney, EffectOp.Add, amount))
            {
                Debug.LogWarning($"[Money] 금고 지급 실패 — {why}: 지갑 적립 불가");
                return false;
            }
            Treasury   -= amount;
            MoneySupply = NextSupply(MoneySupply, amount);
            Debug.Log($"[Money] 금고 →{to.AgentId} {amount}동 — {why} (금고 {Treasury} · 통화량 {MoneySupply})");
            return true;
        }

        /// <summary>촌장의 발행 (M17-W3) — 무 → 금고. 금고는 M 밖이라 통화량은 안 늘지만
        /// MintDebt가 즉시 붙어 물가(예보)가 그 자리에서 움직인다.
        /// ⚠️ 이 부채는 웃돈으로 나갈 때 **차감하지 않는다** — 차감하면 발행 마찰이 사라져
        /// 브레인스토밍에서 기각한 "마찰 없음" 안으로 되돌아간다. 감쇠만 한다 (ADR-M17-4).</summary>
        public void IssueCurrency(int amount, string why)
        {
            if (amount <= 0) return;
            MintToTreasury(amount, why);   // 회계는 한 통로로 (폐곡선 유지)
            IssuedTotal += amount;
            MintDebt    += amount;
            Debug.Log($"[Money] 발행 여파 +{amount} (누적 {MintDebt}) — 찍은 돈은 값이 더 떨어진다");
        }

        /// <summary>발행 부채 감쇠 (M17-W3, 순수 — 게이트 M17-T3). 하루 1회.
        /// 내림이라 잔량이 1~4에서 영원히 안 사라지므로 5 미만은 0으로 떨군다
        /// (감쇠율 0%면 영구 부채가 의도이므로 그때는 떨구지 않는다).</summary>
        public static int DecayMintDebt(int debt, int decayPct)
        {
            if (debt <= 0) return 0;
            int rate = Mathf.Clamp(decayPct, 0, 100);
            if (rate <= 0) return debt;                 // 감쇠 없음 = 영구 부채 (에셋의 선택)
            int next = Mathf.FloorToInt(debt * (100 - rate) / 100f);
            return next < 5 ? 0 : next;
        }

        /// <summary>감쇠 적용 (M17-W3) — 하루 경계 블록에서 1회 호출. 순수 함수와 쓰기 지점을
        /// 나눈 이유는 게이트가 곡선만 따로 검증할 수 있게 하기 위함이다.</summary>
        public void TickMintDebtDecay(int decayPct)
        {
            if (MintDebt <= 0) return;
            int next = DecayMintDebt(MintDebt, decayPct);
            if (next == MintDebt) return;
            Debug.Log($"[Money] 발행 여파 {MintDebt} → {next} (하루 감쇠 {decayPct}%)");
            MintDebt = next;
        }

        /// <summary>소멸: 주민 → 무 (ADR-M17-2 ④). 호출처는 사망(BurnWalletOnDeath) 1곳뿐.
        /// 지갑 차감은 호출자가 한다 (죽는 자의 지갑 정리 — 여기는 회계만).</summary>
        public void Burn(int walletAmount, string why)
        {
            if (walletAmount <= 0) return;
            MoneySupply  = NextSupply(MoneySupply, -walletAmount);
            BurnedTotal += walletAmount;
            Debug.Log($"[Money] -{walletAmount}동 — {why} (통화량 {MoneySupply})");
        }

        /// <summary>물가 산식 (M16-W4 · M17-W4 확장, 순수 — 게이트 M16-T5·M17-T1).
        /// P% = clamp(100 × (M + k×발행부채) ÷ (Q × 기준가), 100 ~ 상한).
        /// M이 작으면 100(기준가 그대로 — 화폐가 없던 판과 연속).
        /// Q = 마을 식량 합 (거래 대상 재화만 — 자재 제외, 단위 정합).
        ///
        /// **확정 물가도 예보도 이 함수 하나를 지난다** (ADR-M17-3) — 다른 것은 입력 시점뿐이다.
        /// 예보용으로 복사본을 만들면 화면과 판정이 갈린다 (산식 이원화 금지).
        /// mintDebt·surchargeK 기본값 0 = M16 시절과 완전히 동일 (기존 호출·게이트 무수정).</summary>
        public static int ComputePricePct(int moneySupply, int foodCount, int basePrice, int capPct,
                                          int mintDebt = 0, float surchargeK = 0f)
        {
            int denom = Mathf.Max(1, foodCount * Mathf.Max(1, basePrice)); // Q=0 방어 (전멸 직전)
            int numer = moneySupply + Mathf.RoundToInt(Mathf.Max(0, mintDebt) * Mathf.Max(0f, surchargeK));
            return Mathf.Clamp(100 * numer / denom, 100, Mathf.Max(100, capPct));
        }

        /// <summary>남은 발행 여력, 동 단위 (M17-R6, 순수 — 게이트 M17-T10).
        ///
        /// **물가 산식을 역으로 푼 값이다** — 새 개념이 아니다:
        ///   P = 100 × (M + k·D) ÷ (Q × 기준가) ≤ 상한
        ///   → D_max = (상한 × Q × 기준가 ÷ 100 − M) ÷ k
        /// 여력 = D_max − 현재 부채. 이만큼까지만 찍을 수 있다.
        ///
        /// 왜 필요한가 (§8.5 발견 A): 상한이 있으면 그 위로는 발행 페널티가 포화되어 **추가
        /// 발행이 공짜**가 된다. 무한히 찍을 수 있으면 "촌장의 돈은 유한하다"는 M17의 전제가
        /// 무너진다. 부채가 상한을 넘지 못하게 막으면 상한이 비로소 진짜 벽이 된다.
        ///
        /// 따라오는 성질: ①부채가 감쇠하면 여력이 돌아온다 (회복 곡선 = 발행 여유)
        /// ②마을이 커지면(Q↑) 더 찍을 수 있다 (경제 규모에 비례한 발행 여력)
        /// ③금고가 마르고 여력도 0이면 세율 인상 말고 길이 없다 (정책 판단의 강제).
        ///
        /// k ≤ 0(마찰 없음)이면 부채가 물가를 안 밀므로 한도가 없다 — int.MaxValue.</summary>
        public static int MintHeadroom(int moneySupply, int mintDebt, float surchargeK,
                                       int foodCount, int basePrice, int capPct)
        {
            if (surchargeK <= 0f) return int.MaxValue; // 마찰 0 = 무제한 (에셋의 선택)
            long budget = (long)Mathf.Max(1, foodCount) * Mathf.Max(1, basePrice)
                          * Mathf.Max(100, capPct) / 100;          // 분자가 쓸 수 있는 총액
            long maxDebt = (long)((budget - moneySupply) / surchargeK);
            long room = maxDebt - Mathf.Max(0, mintDebt);
            return room <= 0 ? 0 : (int)System.Math.Min(room, int.MaxValue);
        }

        /// <summary>물가의 두 몫 (M17-W4, 순수 — 게이트 M17-T1). 화면의 "원인 분해"가 읽는
        /// 단일 출처: 도는 돈 몫과 발행 여파 몫으로 나눈다.
        /// ⚠️ 발행 몫은 반드시 **잔여**로 구한다 — 따로 계산하면 clamp 때문에 두 숫자의 합이
        /// 화면의 총합과 어긋난다. clamp가 단조라 잔여는 절대 음수가 되지 않는다.
        /// ⚠️ clamp 구간(하한 100·상한)에서는 분해가 실제 기여도와 다르다. 화면은 거짓말을
        /// 하지 않지만(합은 항상 맞다) **이 값을 밸런스 근거로 읽지 않는다.**</summary>
        public static (int moneyPct, int mintPct) SplitPrice(int moneySupply, int mintDebt, float surchargeK,
                                                             int foodCount, int basePrice, int capPct)
        {
            int total = ComputePricePct(moneySupply, foodCount, basePrice, capPct, mintDebt, surchargeK);
            int money = ComputePricePct(moneySupply, foodCount, basePrice, capPct);
            return (money, total - money);
        }

        /// <summary>완공 플래그 세팅 — ConstructionService.Complete() 전용 (단일 완료 지점).</summary>
        internal void SetBuiltFlag(SlotId slot, bool value) => _slots[(int)slot] = value ? 1 : 0;

        /// <summary>
        /// 소비 액션 → (스톡 슬롯, 1개당 포만) 파생 (순수 — M9-T5, ADR-M9-10). 식량 가치의 유일한
        /// 출처는 액션 에셋이다: 효과에서 스톡 SubClamp0 1개 + MySatiety Add를 찾는다. 둘 중 하나라도
        /// 없으면 false (식량 아님 — FoodSources 오등록·Rest류 방어). 50·15 리터럴은 여기 없다.
        /// M11-B: 개인 스톡(MyRawFood/MyCookedFood)도 식량이다 — 소비 액션 개인화 후에도 파생 유지.
        /// </summary>
        public static bool TryGetFoodValue(ActionSO a, out SlotId stockSlot, out int satietyGain)
        {
            stockSlot = default;
            satietyGain = 0;
            if (a == null) return false;

            var effects = new List<SlotEffect>();
            a.CollectEffects(effects);

            bool foundStock = false, foundGain = false;
            foreach (SlotEffect e in effects)
            {
                if (!foundStock && e.Op == EffectOp.SubClamp0 && e.Value == 1
                    && (SlotIds.IsStock(e.Slot) || SlotIds.IsPersonalStock(e.Slot)))
                {
                    stockSlot = e.Slot;
                    foundStock = true;
                }
                else if (!foundGain && e.Op == EffectOp.Add && e.Slot == SlotId.MySatiety)
                {
                    satietyGain = e.Value;
                    foundGain = true;
                }
            }
            return foundStock && foundGain;
        }

        /// <summary>
        /// 내(몸+집) 남은 식량 일수 (순수 — M11-D, 舊 마을 합산 ComputeFoodDaysLeft 대체 —
        /// 마을 산식은 삭제됐다, 판정 이원화 금지): floor(Σ(개인 스톡×1개당 포만) ÷ (감쇠×계절배율)).
        /// 집 저장은 몸과 같은 가치표를 쓴다 (생식=생식, 조리=조리 — 위치는 가치와 무관).
        /// 가치표 없음·일소요 ≤ 0이면 NO_ESTIMATE(99). NO_ESTIMATE 상한 클램프 (트리거 언어 통일).
        /// </summary>
        public static int ComputeMyFoodDays((SlotId slot, int gain)[] foodValues,
            int myRaw, int myCooked, int homeRaw, int homeCooked, float decayPerDay, float seasonMult)
        {
            if (foodValues == null || foodValues.Length == 0) return NO_ESTIMATE;
            long totalSatiety = 0;
            foreach ((SlotId slot, int gain) fv in foodValues)
            {
                if (fv.slot == SlotId.MyRawFood)         totalSatiety += (long)(myRaw + homeRaw) * fv.gain;
                else if (fv.slot == SlotId.MyCookedFood) totalSatiety += (long)(myCooked + homeCooked) * fv.gain;
            }
            float dailyNeed = decayPerDay * seasonMult;
            if (dailyNeed <= 0f) return NO_ESTIMATE;
            return Mathf.Min(NO_ESTIMATE, Mathf.FloorToInt(totalSatiety / dailyNeed));
        }

        private float SeasonDecayMult() => _season != null ? _season.SatietyDecayMult : 1f;

        /// <summary>개인 식량 일수 창구 (M11-D) — 스냅샷·HUD 최솟값 집계가 같은 산식·같은 캐시를
        /// 쓴다 (판정 이원화 금지). 舊 마을 합산 EstimateFoodDaysLeft는 삭제됨.</summary>
        public int EstimatePersonalFoodDays(int myRaw, int myCooked, int homeRaw, int homeCooked)
            => _foodValues != null
                ? ComputeMyFoodDays(_foodValues, myRaw, myCooked, homeRaw, homeCooked,
                                    _decayPerDay, SeasonDecayMult())
                : NO_ESTIMATE;

        /// <summary>
        /// 플래닝용 read-only 스냅샷 생성. 발견 플래그는 "잔량 있는 발견 노드 존재 여부"다
        /// (근접이 아님 — 노드까지의 이동은 W4 러너 담당).
        /// </summary>
        public WorldSnapshot BuildSnapshot(int satiety, int fatigue, bool hasHome = false,
                                           bool threatNear = false,
                                           int myRaw = 0, int myCooked = 0,
                                           int homeRaw = 0, int homeCooked = 0,
                                           bool wasAttacked = false,
                                           int myPlots = 0, int myEmptyPlots = 0, int myRipeCrops = 0,
                                           bool hasCampfire = false,
                                           bool wasStarved = false,
                                           int myMoney = 0, int myDebt = 0)
        {
            var slots = new int[PlanningConfig.TotalSlots];

            // 월드 소유 슬롯은 전부 복사 — "슬롯 추가 시 복사 목록 누락" 함정의 구조적 제거
            // (2026-07-14 HouseCount 누락 사고: goal이 달성을 영영 몰라 집 초과 건설 + NoSolution)
            for (int i = 0; i < SlotIds.Count; i++)
                slots[i] = _slots[i];

            // 파생 슬롯만 덮어쓰기 — 각 원천이 유일한 출처
            slots[(int)SlotId.MySatiety] = satiety; // 에이전트 개인 소유
            slots[(int)SlotId.MyFatigue] = fatigue;
            slots[(int)SlotId.MyHasHome] = hasHome ? 1 : 0; // 원천 = OwnershipService (M8-C)
            slots[(int)SlotId.NearDiscoveredWood]  = Discovered(ResourceType.Wood);   // DiscoveryService
            slots[(int)SlotId.NearDiscoveredFood]  = Discovered(ResourceType.RawFood);
            slots[(int)SlotId.NearDiscoveredStone] = Discovered(ResourceType.Stone);
            slots[(int)SlotId.AtBuildSite] = 0; // W6에서 사용
            // Empty/Ripe의 유일한 원천은 FarmService (ADR-M2-4) — 미배선(테스트 등)이면 0.
            // 값은 개수 (ADR-M3-2) — 익은 밭 N개면 한 플랜에 수확 N회가 담긴다.
            slots[(int)SlotId.EmptyFarmPlot]     = _farm != null ? _farm.CountEmpty() : 0;
            slots[(int)SlotId.RipeCropAvailable] = _farm != null ? _farm.CountRipe()  : 0;
            // 계절의 유일한 원천은 SeasonService (M6-A) — 미배선이면 "위기 없음" 중립 (99/0 = M5 동일 판정)
            slots[(int)SlotId.DaysToCrisis] = _season != null
                ? Mathf.CeilToInt(_season.DaysToCrisis) : (int)SeasonService.NO_CRISIS;
            slots[(int)SlotId.CrisisActive] = _season != null && _season.Current != null
                                              && _season.Current.IsCrisis ? 1 : 0;
            // 겨울 채집 봉쇄 (ADR-M6-1 개정) — 열매가 언 계절엔 1. HarvestWildBerries가 "==0"을 전제로
            // 걸어 플래너 단계에서 빠진다. ⚠️ 어떤 액션도 이 슬롯에 효과를 두지 말 것 — 위조 가능하면
            // Explore가 NearDiscoveredFood를 되세우던 우회가 재현된다. 밭·조리·저장분은 무관(안 막힘).
            slots[(int)SlotId.ForageFrozen] = _season != null && _season.ForageFrozen ? 1 : 0;
            // M14 계절 방아쇠 (ADR-M14-2) — 봉쇄 기준 카운트다운 + 심기 창. 원천 = SeasonService.
            // 미배선 중립 = 99/열림 (계절 없던 시절과 동일 판정 — DaysToCrisis 관례와 짝).
            slots[(int)SlotId.DaysToFreeze] = _season != null
                ? Mathf.CeilToInt(_season.DaysToFreeze) : (int)SeasonService.NO_CRISIS;
            slots[(int)SlotId.PlantWindowOpen] = _season == null
                || ComputePlantWindow(_season.GrowthMult, _season.DaysToFreeze, _farmGrowthDays) ? 1 : 0;
            // 내 식량 일수 (M11-D — 마을 합산에서 개인 파생으로 전환. 트리거 전용 ADR-M9-9 유지).
            slots[(int)SlotId.MyFoodDaysLeft] = EstimatePersonalFoodDays(myRaw, myCooked, homeRaw, homeCooked);
            // 개인 인벤토리 (M11-A) — 몸 소지는 VillagerAgent, 집 저장은 HomeStorageService가 원천.
            // 기본 0 = 중립 (미배선 테스트·개인화 이전 에셋은 이 슬롯을 참조하지 않는다).
            slots[(int)SlotId.MyRawFood]        = myRaw;
            slots[(int)SlotId.MyCookedFood]     = myCooked;
            slots[(int)SlotId.MyHomeRawFood]    = homeRaw;
            slots[(int)SlotId.MyHomeCookedFood] = homeCooked;
            // 피격 경험 (M11-G) — 원천 = VillagerAgent.Injure. 기본 false = 0 (중립: 집 동기
            // goal·긴급 부탁이 영구 불발 = M11-F 동작).
            slots[(int)SlotId.MyWasAttacked] = wasAttacked ? 1 : 0;
            // 아사 직전 경험 (M12-G) — 원천 = VillagerAgent 굶주림 계단. 기본 false = 0 (중립:
            // 성향 문턱 우회가 영구 불발 = 기질만으로 판정하는 M12-F 동작).
            slots[(int)SlotId.MyWasStarved] = wasStarved ? 1 : 0;
            // 개인 밭 (M11-E) — 원천 = FarmService 소유 필터. 전역 EmptyFarmPlot(12)·RipeCropAvailable(13)은
            // 위에서 계속 채워지지만 참조 goal이 0이라 휴면이다 (⚠️① 전역 FarmPlotCount는 위협 규모 분모).
            slots[(int)SlotId.MyFarmPlotCount] = myPlots;
            slots[(int)SlotId.MyEmptyPlot]     = myEmptyPlots;
            slots[(int)SlotId.MyRipeCrop]      = myRipeCrops;
            // 내 모닥불 소유 (M11-K) — 원천 = OwnershipService. 조리 전제·정착 진행 축. 기본 0 = 중립.
            slots[(int)SlotId.MyHasCampfire]   = hasCampfire ? 1 : 0;
            // 부상 주민 수 (M10-A) — 파생 슬롯, 원천은 SimulationLoop 집계뿐. 미배선이면 0 (중립 —
            // Goal_TendInjured 트리거 "≥1" 영구 불발 = M9 동작).
            slots[(int)SlotId.InjuredCount] = _injuredCount != null ? _injuredCount() : 0;
            // 미안정 부상자 수 (M11-I) — Goal_TendInjured(응급조치) 트리거. 미배선이면 0 (중립).
            slots[(int)SlotId.UntendedInjuredCount] = _untendedInjuredCount != null ? _untendedInjuredCount() : 0;
            // 위협 근접 (M10-D) — My* 계열 개인 파생 슬롯 (에이전트가 개인 감지 배율로 계산해
            // 인자로 넘긴다, MySatiety 패턴). 기본 false = 0 (중립 — Goal_Flee 영구 불발).
            slots[(int)SlotId.ThreatNear] = threatNear ? 1 : 0;
            // 지갑 (M16-W5) — 원천 = VillagerAgent. 부탁 스캔 조건 전용 (플래너 전제·효과 금지,
            // ADR-M16-5). 기본 0 = 중립 (직거래 의뢰인 조건 MyMoney ≥ N 영구 불발 = 화폐 이전 동작).
            slots[(int)SlotId.MyMoney] = myMoney;
            // 빚 (M17-W7) — 원천 = RequestService(미정산 보상). Goal_RepayDebt 트리거 전용.
            // 🔴 이 한 줄이 없으면 트리거가 영원히 0을 읽어 goal이 절대 안 뜬다. 컴파일도
            // 게이트도 안 잡는다 — M16-W5에서 MyMoney로 똑같이 밟은 자리다 (명세 W7 DoD 🔴).
            slots[(int)SlotId.MyDebt] = myDebt;
            return new WorldSnapshot(slots);
        }

        private int Discovered(ResourceType type)
            => _discovery != null && _discovery.HasDiscovered(type) ? 1 : 0;
    }
}
