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
                 "활성 티어 = 게임일 UnlockDay 충족 중 최신 1개 (ADR-M10R-1 시간 래칫 — 舊 '마을 규모' " +
                 "기준은 M10R에서 폐기: 사망→규모↓→강등의 영구 안정 루프를 만들었다. M21-W9 문언 정정).")]
        public ThreatSO[] Threats;

        [Tooltip("래칫 완충 스택 상한 (M21-W7, ADR-M21-4). 격퇴 성공(무리 전체가 전투로 물러남)과 " +
                 "주민 사망이 스택을 쌓고, 다음 출몰 마릿수가 스택만큼 깎인다 (최소 1마리 클램프 — " +
                 "완충이 위협을 0으로 만들 수는 없다). 스폰 시 전부 소모 (1회성). 제안 3.")]
        public int ThreatReliefStackMax = 3;

        [Tooltip("주민 사망 1명당 다음 발동 지연 (게임일, M21-W7 — 자비: 손실이 마모가 아니라 리듬이 " +
                 "되게, 림월드 adaptation의 이중 축). 지연 총량 상한 = 스택 상한 × 이 값. " +
                 "격퇴 보상은 지연 없이 마릿수만 깎는다 (§4 — 이김에는 숨돌릴 틈이 필요 없다). 제안 2.")]
        public float ThreatReliefDelayDays = 2f;

        [Header("들판 상주 예약 (M26-2차 W5R — 좌표만 두고 주민이 오면 부화)")]
        [Tooltip("부화 반경 = 주민 시야 × 이 값. 주민은 아직 적을 못 봤지만 적은 주민이 온 것을 안다. " +
                 "제안 1.5 (시야 10 → 15칸).")]
        [Min(1f)] public float ResidentHatchSightMult = 1.5f;

        [Tooltip("회수 반경 = 주민 시야 × 이 값. 🔴 **부화보다 커야 한다** (히스테리시스) — 같으면 " +
                 "경계를 걷는 주민 앞에서 무리가 깜빡인다. 제안 2.0 (시야 10 → 20칸).")]
        [Min(1f)] public float ResidentRecallSightMult = 2f;

        [Tooltip("전멸당한 무리가 같은 앵커에 다시 서기까지 (게임일) — **전리품(MeatDrop)의 재생 주기**다. " +
                 "1회 부화면 고기 공급원이 마른다 (2026-08-11 사용자 판단).\n" +
                 "⚠️ 제안 3 (자원 리스폰 이동과 같은 시계). 🔴 1년=12일인 현 눈금에서 3일 = 분기 — " +
                 "**시간 밸런스 세션**(별도 명세)에서 일 단위 필드 29개와 함께 재조정한다.")]
        [Min(0f)] public float ResidentRearmCooldownDays = 3f;

        [Header("압력 (M24-1차 W3 — ADR-M24-1: 압력은 단조다)")]
        [Tooltip("압력 1점이 오르는 데 걸리는 게임일. 작을수록 시간이 세게 민다. 제안 3.\n" +
                 "🔑 이 항이 **성장 강제**를 맡는다 — 마을을 안 키우고 버티면 결국 이 곡선에 깔린다.")]
        public float PressureDaysPerPoint = 3f;

        [Tooltip("압력 1점이 오르는 **역대 최고 인구** 수. 작을수록 성장의 대가가 무겁다. 제안 4.\n" +
                 "🔑 이 항이 **성장의 대가**를 맡는다 — 크게 키우면 그만큼 더 맞는다.\n" +
                 "🔴 현재 인구가 아니라 역대 최고다. 현재 인구로 바꾸면 사망이 난이도를 낮춰 " +
                 "ADR-M10R-1이 고친 음성 피드백 루프가 그대로 돌아온다 (게이트 M24-T2가 감시).")]
        public int PressurePopPerPoint = 4;

        [Tooltip("웨이브 주기 (게임일) — **전역 고정**. 압력은 편성 규모만 키우고 빈도는 안 건드린다.\n" +
                 "🔑 사용자 결정(2026-08-10): 舊 밴드는 위로 갈수록 주기가 짧아졌지만(6→5→4) 그 축을 " +
                 "가져오지 않는다. 울타리 수리(1:5)·목수·재건은 **출몰 사이의 시간**을 전제로 만든 " +
                 "장치라, 주기를 압력에 걸면 M22 자산이 후반에 조용히 죽는다.\n" +
                 "제안 5 — 舊 밴드 6/5/5/4의 가운데 (새로 발명한 수가 아니다).")]
        public float WavePeriodDays = 5f;

        [Tooltip("악마 웨이브 간격 (게임일). 이 주기마다 정규 편성 **대신** 악마만 온다 — 예산을 " +
                 "쓰지 않는 단독 편성이다. 첫 등장은 악마 종족의 UnlockDay와 이 값 중 늦은 쪽.\n" +
                 "빈도의 리듬을 여기 하나에 몰아 둔 이유는 위 WavePeriodDays 주석 참조. 제안 25.")]
        public float DemonWaveEveryDays = 25f;

        [Tooltip("악마 마릿수가 1 늘어나는 데 걸리는 게임일 (첫 등장일 기준). 제안 30.")]
        public float DemonCountGrowthDays = 30f;

        [Tooltip("악마 마릿수 상한. 제안 4.")]
        public int DemonCountMax = 4;

        // (M22-W3R의 DefenseZoneMinSide는 W3R2 줄 누적 모델로 폐기=삭제 — 줄 최소 길이 2는
        //  밸런스가 아니라 알고리즘 상수라 코드에 있다. 나무 재고가 자연 상한.)

        [Header("방랑자 (M10-E — 상실의 회복. 전부 제안치)")]
        [Tooltip("방랑자 도착 주기 (게임일). 위협 주기 6과 어긋난 5 — 회복이 상실 직후 오지 않는 위상차. " +
                 "0 이하 = 방랑자 없음 (중립 불변식 — M9 동작).")]
        public float WandererIntervalDays = 5f;

        [Tooltip("수락/거절 응답 대기 (게임일). 초과 시 자동 퇴장 — 결정을 미루는 것도 결정이다.")]
        public float WandererWaitDays = 0.7f;

        [Tooltip("자비 장치 (M24-1차 W9) — 인구가 역대 최고 대비 바닥일 때의 도착 주기 배율. " +
                 "1 = 자비 없음(기존 동작). 0.4 = 최악일 때 주기가 40%로 줄어 방랑자가 2.5배 자주 온다. " +
                 "🔑 이것은 난이도가 아니라 자비다 — 압력은 한 점도 안 내려간다(ADR-M24-1). " +
                 "손을 내미는 것은 방랑자뿐이고, 그 손을 잡을지는 여전히 플레이어가 정한다.")]
        [Range(0.1f, 1f)] public float WandererMercyMinMult = 0.4f;

        [Header("식량 수지 (M9-G — ADR-M9-10: 식량 가치의 유일한 출처는 소비 액션)")]
        [Tooltip("식량 소비 액션 목록 (EatCookedFood·EatRawFood 등). FoodDaysLeft 계산이 이 액션들의 " +
                 "효과(스톡 SubClamp0 + MySatiety Add)에서 1개당 포만을 파생한다 — 가치 이중 기입 금지. " +
                 "비면 FoodDaysLeft 항상 99 (중립 — 기존 동작과 완전 동일).")]
        public ConsumeActionSO[] FoodSources;

        // (M19-W4: 화폐 설정 8종 — 기준가·물가상한·금고·세율·발행량·가산계수·감쇠율·세율상한 —
        //  화폐와 함께 철거 — ADR-M19-1. 에셋의 잔존 필드 값은 다음 임포트에서 자연 소거된다.)

        [Header("HUD (M13-B — 상태 알림 줄)")]
        [Tooltip("상태 알림 줄 폰트 크기. 초기값 24가 작다는 Play 피드백(2026-07-30)으로 30 제안. " +
                 "0 이하 = 기본 24. 다른 HUD 줄(달력 30·정보줄 24)은 코드 상수 유지 — 조절 요구가 생기면 그때 승격.")]
        public float HudStatusFontSize = 30f;

        [Tooltip("좌상단 자원 줄에 표시할 전역 스톡 목록 (M22-2차 W4 — 사용자 Play 피드백 " +
                 "\"보유 자원이 안 보인다\"). 표시 순서 그대로. 새 자원 = 여기 행 추가, 코드 0줄. " +
                 "비면 자원 줄 없음 (중립). 전역 스톡(SlotIds.IsStock)만 — 개인·집 스톡은 정보줄 몫. " +
                 "배포 값 검산은 게이트 M22_T11.")]
        public ResourceHudEntry[] HudResources;

        [System.Serializable]
        public struct ResourceHudEntry
        {
            [Tooltip("표시할 전역 스톡 슬롯 (SlotIds.IsStock 참이어야 한다)")]
            public SlotId Slot;
            [Tooltip("표시 라벨 (예: 나무)")]
            public string Label;
        }

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
