using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 월드 공통 설정. 전부 舊 런타임 이관값 (ADR-M0-2 — 발명 금지).
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/WorldConfig", fileName = "WorldConfig")]
    public sealed class WorldConfigSO : ScriptableObject
    {
        [Header("초기 자원 (GDD v0.4)")]
        public int InitialWoodStock = 10;
        public int InitialRawFoodStock = 30;
        public int InitialStoneStock = 5;

        [Header("시간")]
        [Tooltip("게임 시간 배율. 0.01 → 0.1초 틱당 0.001게임일, 1게임일 = 100초 (舊 GameManager._gameTimeScale)")]
        public float GameTimeScale = 0.01f;

        [Header("기지")]
        [Tooltip("기지 타일 좌표 (舊 (0,0))")]
        public int BaseTileX = 0;
        public int BaseTileY = 0;

        [Tooltip("게임 시작 시 기지 주변 자동 발견 반경 (舊 _baseDiscoverRadius 12)")]
        public int BaseDiscoverRadius = 12;

        [Tooltip("마을 반경 = 맵 절반 크기의 이 비율 (M11-K, 0~1). 기지 기준 체비쇼프. 집은 이 안에만 " +
                 "선다. 절대값이 아니라 비율이라 맵이 커지면 마을도 자동으로 넓게 퍼진다(사용자 지시). " +
                 "0.8 = 맵 100이면 반경 ~39(가장자리 여백 남김). 실효 반경 = EffectiveVillageRadius().")]
        [Range(0.1f, 1f)]
        public float VillageRadiusFraction = 0.8f;

        /// <summary>실효 마을 반경(체비쇼프 타일, M11-K) — 맵 절반 크기 × 비율. 맵 커지면 따라 커진다.
        /// 순수 산식은 게이트 대상(HomePlacementMath). 여기선 MapBounds에서 맵 크기를 읽어 곱한다.</summary>
        public int EffectiveVillageRadius()
        {
            MapBounds.Get(out int minX, out int maxX, out int minY, out int maxY);
            int half = Mathf.Min(maxX - minX, maxY - minY) / 2; // 맵 절반 크기
            return WorldConfigMath.VillageRadius(half, VillageRadiusFraction);
        }

        /// <summary>성격 선호 거리(비율) → 실효 절대 거리 (M11-K) — 맵 커지면 함께 스케일.</summary>
        public int PreferredHomeDist(float fraction) => WorldConfigMath.PreferredDist(EffectiveVillageRadius(), fraction);

        [Tooltip("개인 밭이 설 수 있는 내 집 주변 반경 (M11-E, 체비쇼프). 제안치 2 — 집 곁에 " +
                 "제 밭이 모인다. 이 반경이 꽉 차면 더는 못 넓힌다 (개인 밭의 소프트 상한).")]
        public int FarmNearHomeRadius = 2;

        [Header("농사 (M2)")]
        [Tooltip("작물 성장 기간 (게임일). 제안치 1.5 (M2 §4) — 방치 시 사이클이 눈에 2~3회 보이는 속도.")]
        public float FarmGrowthDays = 1.5f;

        [Header("계절 (M6 — ADR-M6-1: 계절이 게임에 개입하는 통로는 배율 3종뿐)")]
        [Tooltip("계절 사이클 (배열 순서로 순환). 비면 계절 없음 — M5 동작과 완전 동일 (중립).")]
        public SeasonSO[] SeasonCycle;

        [Tooltip("위기 계절 예고 시작 (위기 N일 전부터 HUD 경보·주민 술렁임). 제안치 3 (M6 §4).")]
        public float ForecastDays = 3f;

        [Tooltip("첫 겨울까지의 유예 (게임일) — 계절 시계를 이만큼 늦춰 **첫 사이클의 준비 기간만** 늘린다. " +
                 "이후 사이클 간격은 그대로(위기의 리듬 불변). **기본 0 = 중립(사용 안 함)**.\n" +
                 "⚠️ 완충 손잡이일 뿐 상시 켜 두는 값이 아니다 — 겨울은 목수가 있으면(집 곳간 15) 넘기고 " +
                 "없으면 무너지는 것이 의도된 구조다(2026-07-24 사용자 확인). 죽음을 막으려 이 값을 올리는 " +
                 "것은 '방치가 안락하면 실패' 기준에 걸린다. 계절 길이·인원 등 구조가 바뀔 때만 검토할 것.")]
        public float SeasonPrologueDays;

        [Header("재해 (M9-C — ADR-M9-4: 재해 목록의 집. 계절 참조로 발동)")]
        [Tooltip("재해 에셋 목록 (DisasterSO). 비면 재해 없음 — M8 동작과 완전 동일 (중립 불변식).")]
        public DisasterSO[] Disasters;

        [Header("야생 위협 (M10-C — ADR-M10-6: 티어 등록제의 집. 등록된 티어만 존재 = 플래토)")]
        [Tooltip("위협 티어 에셋 목록 (ThreatSO). 비면 위협 없음 — M9 동작과 완전 동일 (중립 불변식). " +
                 "활성 티어 = 마을 규모(주민+밭+집) 충족 중 최대 1개.")]
        public ThreatSO[] Threats;

        [Header("방랑자 (M10-E — 상실의 회복. 전부 제안치)")]
        [Tooltip("방랑자 도착 주기 (게임일). 위협 주기 6과 어긋난 5 — 회복이 상실 직후 오지 않는 위상차. " +
                 "0 이하 = 방랑자 없음 (중립 불변식 — M9 동작).")]
        public float WandererIntervalDays = 5f;

        [Tooltip("수락/거절 응답 대기 (게임일). 초과 시 자동 퇴장 — 결정을 미루는 것도 결정이다.")]
        public float WandererWaitDays = 0.7f;

        [Header("식량 수지 (M9-G — ADR-M9-10: 식량 가치의 유일한 출처는 소비 액션)")]
        [Tooltip("식량 소비 액션 목록 (EatCookedFood·EatRawFood 등). FoodDaysLeft 계산이 이 액션들의 " +
                 "효과(스톡 SubClamp0 + MySatiety Add)에서 1개당 포만을 파생한다 — 가치 이중 기입 금지. " +
                 "비면 FoodDaysLeft 항상 99 (중립 — 기존 동작과 완전 동일).")]
        public ConsumeActionSO[] FoodSources;

        [Header("화폐 (M16-W4 — 물가 산식. Play 리뷰 ① 후 재조정, M16-B)")]
        [Tooltip("물가 분모의 기준가 계수 — 물가 % = 100 × 통화량 ÷ (마을 식량 개수 × 이 값), ADR-M16-3.\n" +
                 "⚠️ 거래 실가격(RequestSO.RewardCostAmount)과는 별개다 — 이 값은 **인플레 속도**만 정한다.\n" +
                 "10 = M16-B 재조정치: 임금 2.5배 인상 + 채집 산출 감소(식량 개수 ↓)를 상쇄해 " +
                 "인플레 곡선을 M16 최초안과 같게 유지한다 (그 전 3).")]
        public int MoneyBasePrice = 10;

        [Tooltip("물가 상한 % (폭주 클램프 — 회복 불능 나선 차단). 하한은 100 고정.")]
        public int PriceCapPct = 400;

        [Header("촌장 금고 (M17-W1 — ADR-M17-1: 금고는 통화량 M 밖이다)")]
        [Tooltip("판 시작 시 금고 잔고(동). 0 = 촌장의 첫 개입이 곧 첫 발행이 된다 (제안치).\n" +
                 "⚠️ 0보다 크면 그 돈도 '무에서 나온 돈'이므로 발행 누적에 함께 실린다 " +
                 "— 안 그러면 폐곡선 검산(§8 D2)이 첫 프레임부터 어긋난다.")]
        public int StartingTreasury = 0;

        [Tooltip("임금 원천징수 세율 단계 % (M17-W2 — 과세 대상은 임금뿐, ADR-M17-5).\n" +
                 "0번이 판 시작 단계. 촌장이 T 키로 순환시킨다.\n" +
                 "제안치 0/15/30 — 실측 근거 없음, Play 재조정 대상 (명세 §9).\n" +
                 "⚠️ 세율은 즉효 버튼이 아니다 (ADR-M17-7): 이미 도는 돈을 회수하지 못하고 " +
                 "통화량 증가를 늦출 뿐이다. 물가는 며칠에 걸쳐 움직인다.")]
        public int[] TaxRatePcts = { 0, 15, 30 };

        [Header("발행 (M17-W3 — 화폐 신뢰. ⚠️ 아래 셋은 짝이다: 방법론 M17)")]
        [Tooltip("한 번 찍을 때의 발행량(동). 제안치 100 — 집값 50동·웃돈 10동 세계에서 " +
                 "웃돈 10회분이라 '비상 수혈' 감각이 된다. 실측 근거 없음, Play 재조정 대상.")]
        public int MintIssueAmount = 100;

        [Tooltip("발행 가산 계수 k (M17-W3) — 물가 분자에 k × 발행부채가 더해진다.\n" +
                 "0 = 마찰 없음(찍어도 안 아프다) · 1 = 액면 전액이 분자에 실린다.\n" +
                 "🔑 **쓰지 않고 금고에 둔 돈도 물가를 올린다** (ADR-M17-4). 현실 경제와 다른 " +
                 "게임적 선택이다 — 그래야 '평시에 미리 무한히 찍어 두기'가 막히고 금고가 " +
                 "의미를 갖는다. 대신 화면이 그 인과를 설명한다(찍은 탓 · N일 뒤 사라짐).\n" +
                 "⚠️ MintDebtDecayPct와 **짝**이다 — k가 커도 하루 만에 잦아들면 안 아프고, " +
                 "k가 작아도 안 잦아들면 영구 페널티다. 하나만 바꾸지 않는다.\n" +
                 "제안치 1.0 — 실측 근거 없음.")]
        public float MintSurchargeK = 1f;

        [Tooltip("발행 부채의 하루 감쇠율 % (M17-W3). 제안치 50 — 100동 발행 시\n" +
                 "100 → 50 → 25 → 12 → 6 → 0 (5일. 5 미만 잔량은 떨어뜨린다).\n" +
                 "⚠️ 곱셈 감쇠라 체감이 직관과 다르다: 20%/일이면 5일 뒤가 0이 아니라 32이고 " +
                 "0에 닿기까지 13일이 걸린다 — 겨울이 4일인 이 게임에서는 사실상 영구 페널티다.\n" +
                 "0 = 감쇠 없음(영구 부채). ⚠️ MintSurchargeK와 짝 — 위 설명 참조.")]
        public int MintDebtDecayPct = 50;

        /// <summary>세율 상한 90 % (M17-W2) — 100%면 실수령이 0이 되어 임금이 플래너 시야에서
        /// 통째로 사라지고 Goal_SaveForHome이 NoSolution이 된다. 손잡이의 사각지대를 에디터에서 막는다.</summary>
        public const int MaxTaxRatePct = 90;

        private void OnValidate()
        {
            if (StartingTreasury < 0) StartingTreasury = 0;
            if (MintIssueAmount < 0) MintIssueAmount = 0;
            MintSurchargeK   = Mathf.Max(0f, MintSurchargeK);
            MintDebtDecayPct = Mathf.Clamp(MintDebtDecayPct, 0, 100);
            if (TaxRatePcts == null) return;
            for (int i = 0; i < TaxRatePcts.Length; i++)
                TaxRatePcts[i] = Mathf.Clamp(TaxRatePcts[i], 0, MaxTaxRatePct);
        }

        [Header("HUD (M13-B — 상태 알림 줄)")]
        [Tooltip("상태 알림 줄 폰트 크기. 초기값 24가 작다는 Play 피드백(2026-07-30)으로 30 제안. " +
                 "0 이하 = 기본 24. 다른 HUD 줄(달력 30·정보줄 24)은 코드 상수 유지 — 조절 요구가 생기면 그때 승격.")]
        public float HudStatusFontSize = 30f;

        [Tooltip("성격별 행동 프로파일 로그 주기(게임일, M12-J). 0 = 계측 끄기(중립). 제안치 7 — " +
                 "계절 길이(8)와 어긋나게 잡아야 특정 계절만 찍히는 편향이 안 생긴다. 읽기 전용 관측.")]
        public float ProfilerIntervalDays = 7f;

        [Header("성향 규칙 (M12-C — 4작용형식 중 계열 단위 유도표의 집)")]
        [Tooltip("성향 → ②비용(노동 4계열)·④대상(택지 거리)의 전역 가중치표. " +
                 "비면 성향이 비용·거리에 개입하지 않는다 (중립 — M11 동작과 완전 동일).")]
        public TraitRulesSO TraitRules;

        [Header("상호대화 (M7 — ADR-M7-6: 대화 목록의 집. 전부 제안치)")]
        [Tooltip("대화 상황 에셋 목록. 비면 상호대화 없음 (중립 — M6 동작).")]
        public ChatterSO[] Chatters;

        [Tooltip("짝 스캔 주기(초, 실시간) — 단일 지점 주기 틱 (ADR-M7-3). 제안치 5.")]
        public float ChatterIntervalSec = 5f;

        [Tooltip("주기당 발화 확률 (성립 쌍 존재 시). 대화는 희소해야 장면이 된다. 제안치 0.5.")]
        public float ChatterChance = 0.5f;

        [Tooltip("발화 후 개인 쿨다운(초, 실시간) — 화자·상대 공용. 동일 화자 연속 스팸 방지. 제안치 30.")]
        public float ChatterCooldownSec = 30f;

        [Header("관계 (M8 — 문턱·주기는 전부 여기, ADR-M8-6. 전부 제안치)")]
        [Tooltip("단짝 문턱 — 쌍방 친밀도가 이 이상이면 단짝 (정보줄 표기). 제안치 20.")]
        public int BuddyThreshold = 20;

        [Tooltip("원한 문턱 — 친밀도가 이 미만이면 원한 (정보줄 표기·거절 판정 기준선). 제안치 -20.")]
        public int GrudgeThreshold = -20;

        [Header("부탁 (M8-D — ADR-M8-7: 부탁 목록의 집. 전부 제안치)")]
        [Tooltip("부탁 상황 에셋 목록. 비면 부탁 없음 (중립 — M7 동작).")]
        public RequestSO[] Requests;

        [Tooltip("부탁 짝 스캔 주기(초, 실시간) — 단일 지점 주기 틱, 한 주기 1건. 제안치 5.")]
        public float RequestIntervalSec = 5f;

        [Tooltip("의뢰인 개인 쿨다운(초, 실시간) — 거절당하면 한동안 다시 조르지 않는다. 제안치 90.")]
        public float RequestCooldownSec = 90f;

        [Tooltip("보상 정산 반경(타일, 맨해튼) — 조각 Y: 목수와 의뢰인이 이 안에서 마주치면 보상 지급. " +
                 "쫓아가지 않고 자연스러운 근접에서 정산. 제안치 3~4.")]
        public int RewardSettleRadiusTiles = 3;

        // [DEPRECATED 2026-07-18] 조각 Y로 보고 심부름(쫓아가기) → 마주치면 정산으로 교체됨.
        // 아래 둘은 더 이상 참조되지 않는다(휴면). 관련 에셋(ReportErrand goal·VisitAction) 정리는
        // 후속 작업 — Docs/퀘스트보드_및_보고심부름정리_후속.md 참조.
        [Tooltip("[DEPRECATED — 조각 Y로 대체, 미사용] 완공 보고 심부름 goal.")]
        public GoalSO ReportErrandGoal;

        [Tooltip("[DEPRECATED — 조각 Y로 대체, 미사용] 보고 심부름 마감 초.")]
        public float ReportTimeoutSec = 60f;
    }

    /// <summary>맵-비례 배치 산식 (M11-K, 순수 — 게이트 대상). 맵이 커지면 마을·선호 거리가
    /// 비율로 따라 커진다(사용자 지시 — 맵 확장 대비). WorldConfigSO가 맵 크기를 먹여 호출.</summary>
    public static class WorldConfigMath
    {
        /// <summary>마을 반경 = 맵 절반 크기 × 비율 (최소 1 — 반경 0 방어).</summary>
        public static int VillageRadius(int mapHalfExtent, float fraction)
            => Mathf.Max(1, Mathf.RoundToInt(mapHalfExtent * Mathf.Clamp01(fraction)));

        /// <summary>선호 거리 = 마을 반경 × 성격 비율 (0 이하 = 중립·최근접, HomePicker가 처리).</summary>
        public static int PreferredDist(int villageRadius, float fraction)
            => fraction <= 0f ? 0 : Mathf.RoundToInt(villageRadius * Mathf.Clamp01(fraction));
    }
}
