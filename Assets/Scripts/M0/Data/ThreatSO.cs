using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 야생 위협 정의 (M10-C, ADR-M10R-1) — 밴드 1개당 에셋 1개. 활성 밴드(게임일 래칫 기준)가
    /// PeriodDays마다 확정 출몰한다 (확률 없음 — ADR-M10-1).
    /// **격퇴 가능 (M21-W2, ADR-M21-1이 舊 ADR-M10-8 "격퇴 불가"를 폐기)** — 위협도 체력을 갖고
    /// 도착 후 눌러앉아 반복 타격하며, 도주선·사냥으로만 물러난다. 티어 래칫은 그대로다:
    /// 이겨도 시대가 되돌아가지는 않는다 (ADR-M21-4).
    /// 타격 대상은 주민(피해의 문 TakeDamage) 또는 밭 시설(M9-B RemoveCountableAt 문)뿐 —
    /// 창고 스톡은 성역 (ADR-M10-5). 새 위협/티어 추가 = 이 에셋 1개 + WorldConfig.Threats 등록,
    /// 코드 0줄 (M10-G 리허설이 증명).
    /// </summary>
    [CreateAssetMenu(menuName = "AIVillage/M0/Threat", fileName = "Threat")]
    public sealed class ThreatSO : ScriptableObject
    {
        [Tooltip("한국어 표시명 (예: 고블린 무리)")]
        public string DisplayName;

        [Tooltip("이 밴드가 열리는 최소 게임일(GameTime). 활성 = UnlockDay ≤ 현재 게임일 중 최신 1개 " +
                 "(ADR-M10R-1 시간 래칫 — 게임일은 안 줄어드니 되돌아가지 않는다). 마을 규모 아님.")]
        public float UnlockDay;

        [Tooltip("출몰 주기 (게임일). 이전 발동 시각 + 이 값 = 다음 발동 시각.")]
        public float PeriodDays = 6f;

        [Tooltip("발동 전 예고 (게임일) — HUD 경보 + 주민 술렁임(M10-D) 시작 시점.")]
        public float WarnDays = 1f;

        [Tooltip("매 출몰이 주민을 타깃할 확률(0~1). 나머지 확률은 밭 타격. 출몰 서수 시드로 결정 " +
                 "(ADR-M10R-2·3 — 상위 티어도 <1이면 밭을 칠 수 있다). 0=밭만, 1=주민만.")]
        [Range(0f, 1f)] public float VillagerTargetChance;

        [Tooltip("타격 반경 (타일, 맨해튼) — 도착 지점 기준 이 안의 주민만 부상 후보. " +
                 "도망(M10-D)치지 않아 잔류한 주민이 다친다 (행동의 인과).")]
        public int StrikeRadiusTiles = 3;

        [Tooltip("주민 감지 반경 (타일) — 이 안의 주민에게 ThreatNear=1 (도망 트리거, M10-D). " +
                 "성격 FleeRadiusMult가 개인 배율로 곱해진다.")]
        public int DangerRadiusTiles = 6;

        [Header("위력 곡선 (DisasterService.LossCount 재사용 — 산식 이원화 금지)")]
        [Tooltip("기본 피해 비율. 제안 0.25.")]
        public float BaseLossPct = 0.25f;

        [Tooltip("후보 1개당 가산 비율 — 밀집할수록 가혹 (재해 곡선과 동일 사상). 제안 0.03.")]
        public float PerTargetPct = 0.03f;

        [Tooltip("피해 비율 클램프 상한. 주민 타격은 0.25 (부상자 ≤ 인구 25% — 결정 9).")]
        public float MaxLossPct = 0.25f;

        [Header("전투 (M21-W2 — 마릿수·체력은 에셋이 정의한다, ADR-M21-6)")]
        [Tooltip("개체 체력. 주민 맨손 타격 10 기준 60 = 6대(≈24초). 차감 판정은 CombatService (ADR-M21-8).")]
        public float MaxHp = 60f;

        [Tooltip("개체 도주선 — 체력이 이 비율 아래로 내려가면 물러난다(격퇴 성립). 0.4 제안. " +
                 "무리 전체가 무너지는 선(잔존율)은 별개다 — 이중 도주선.")]
        [Range(0f, 1f)] public float FleeBelowHpPct = 0.4f;

        [Tooltip("주민 1명에게 주는 타격 피해. 만복 100 기준 34 = 3대 사망, 1대로는 부상선(67) 아래. " +
                 "⚠️ 만복−부상선보다 작으면 물려도 안 다치는 위협이 된다 (게이트 M21-T6).")]
        public float StrikeDamage = 34f;

        [Tooltip("체류 중 재타격 주기 (게임일). 0.25 = 하루 네 번. 도착 즉시 1회, 이후 이 간격.")]
        public float RepeatStrikePeriodDays = 0.25f;

        [Tooltip("체류 안전 상한 (게임일) — 이 시간이 지나면 스스로 물러난다. 자비가 아니라 " +
                 "**지형 보루**다: 닿을 수 없는 자리에 눌러앉은 개체가 영영 남는 것을 막는다.\n" +
                 "M21-W2R에서 2 → 0.75로 축소: 재타격 주기 0.25일과 짝지어 최대 3회 타격 " +
                 "(체류 자체가 아니라 그 길이가 W2의 불만이었다).")]
        public float MaxStayDays = 0.75f;

        [Tooltip("사냥 성공 시 **전역** 창고에 들어가는 생고기 수 (M21-W3). 개인이 아니라 공용인 것은 " +
                 "ADR-M20-9(전이 통로) — 사냥꾼의 효율이 마을 비축으로 흘러야 직업이 마을 단위로 " +
                 "느껴진다. 0이면 드랍 없음(격퇴만 있는 위협). 제안 2.\n" +
                 "※ W6(마릿수) 몫이었으나 W3 에서 당겨 왔다 — 없으면 사냥이 로그만 남고 아무 일도 " +
                 "일어나지 않아 '사냥 사건'이 반쪽이 된다.")]
        public int MeatDrop = 2;

        [Header("대규모 적습 (M21-W6 — 마릿수 확장형, ADR-M21-6: 마릿수는 에셋이 정의한다)")]
        [Tooltip("기본 스폰 마릿수. 1 = 단독 출몰 (기존 동작과 동일 — 중립). " +
                 "산식: clamp(Base + ⌊(day − UnlockDay) / GrowthEveryDays⌋ − 완충, 1, Max).")]
        public int SpawnCountBase = 1;

        [Tooltip("마릿수가 1 늘어나는 데 걸리는 게임일. 0 = 성장 없음. 기준일은 UnlockDay — " +
                 "밴드가 열린 뒤 흐른 시간만 센다 (게임일 래칫과 같은 축, ADR-M10R-1).")]
        public float CountGrowthEveryDays = 0f;

        [Tooltip("마릿수 클램프 상한 — 성장이 무한히 쌓이지 않게. Base 미만이면 Base가 사실상 상한이 된다.")]
        public int SpawnCountMax = 1;

        [Tooltip("무리 도주선 (이중 도주선의 바깥쪽) — 같은 출몰의 **아직 싸우는 개체 비율**이 이 값 " +
                 "미만이면 무리 전체가 무너져 전원 도주한다. 판정은 CombatService.ShouldRout (경계는 " +
                 "버틴다 — 정확히 이 비율이면 아직 싸운다). 개체 도주선(FleeBelowHpPct)과 별개.")]
        [Range(0f, 1f)] public float RoutBelowPct = 0.5f;

        [Header("배회 (M21-W2R — 정지의 폐기, ADR-M21-10: 동선은 표현이라 난수 허용)")]
        [Tooltip("배회 반경 (타일) — 도착 지점을 앵커로 이 반경 안의 통행 가능 타일을 오간다. " +
                 "주민 여가 배회(RestByCampfire.WanderRadius 4)보다 한 칸 크게 = 짐승이 더 넓게 돈다. " +
                 "타격 반경(3~4)보다 커야 '돌다가 다가온다'가 성립한다. 0 이하면 배회하지 않는다(정지 회귀).")]
        public int WanderRadiusTiles = 5;

        [Tooltip("배고픈 계절(SeasonSO.ForageFrozen)의 주민 타깃 확률 배율 — 겨울엔 타깃이 주민 쪽으로 " +
                 "기운다. 결과는 [0,1] 클램프. 1 미만은 무시된다(배고픈 계절이 더 순해질 수는 없다).\n" +
                 "⚠️ 판정은 IsCrisis가 아니라 ForageFrozen이다 (ADR-M21-9) — 여름도 IsCrisis라 " +
                 "그걸 쓰면 여름 위협이 사나워진다.")]
        public float HungrySeasonChanceMult = 2f;

        [Tooltip("배고픈 계절의 체류 상한 배율 — 겨울엔 더 집요하게 머문다. 0.75 × 2 = 1.5일(최대 6회 타격). " +
                 "1 미만은 무시된다. 계절이 바꾸는 것은 타깃 확률과 이 둘뿐이고, 출몰 빈도·피해량은 " +
                 "게임일 래칫(ADR-M10R-1)이 그대로 소유한다.")]
        public float HungrySeasonStayMult = 2f;

        /// <summary>개체 등급 한 칸 (M24-1차 W4) — 팩의 종족별 4변종이 그대로 등급이 된다
        /// (고블린 잡졸·정찰병·두목·대두목). 예산이 이 목록에서 개체를 산다.</summary>
        [System.Serializable]
        public struct Grade
        {
            [Tooltip("표시명 (예: 두목). 로그·예고에 쓰인다.")]
            public string DisplayName;

            [Tooltip("이 개체를 사는 데 드는 압력 점수. 클수록 드물게, 대신 강하게 온다.")]
            public int PointCost;

            [Tooltip("체력·타격 피해 배율. 1 = 종족 기본값 그대로. " +
                     "⚠️ 이동·공격 속도에는 안 걸린다 — 개입 창 불변식(I1)을 지키기 위해서다.")]
            public float StatMult;
        }

        [Tooltip("이 종족은 **단독 웨이브로만** 온다 (M24-1차 W4). 정규 편성(예산 구매)에 섞이지 않고, " +
                 "WorldConfig.DemonWaveEveryDays 주기에 그 회차를 통째로 가져간다.\n" +
                 "🔑 코드에 '악마'를 박지 않기 위한 칸이다 (ADR-M24-4: 종족 차이는 데이터다) — " +
                 "나중에 다른 보스 종족이 생겨도 이 체크 하나면 된다.")]
        public bool SoloWave;

        [Header("등급 (M24-1차 W4 — 예산이 여기서 개체를 산다)")]
        [Tooltip("등급 목록. **비어 있으면 종족 기본값 1등급(비용 1·배율 1)으로 취급**한다 — " +
                 "배선 전과 동일한 동작(중립 불변식).\n" +
                 "🔑 구매 절차는 '상위 1(지휘관) + 나머지 하위 채움'이라, 지휘관/졸개 구조가 " +
                 "값에서 저절로 나온다. 비용을 오름차순으로 적을 것.")]
        public Grade[] Grades;

        [Header("침입 특성 (M24-1차 W5 — ADR-M24-4: 종족 차이는 데이터다)")]
        [Tooltip("이 종족이 쓰는 수와 그 해금 압력. **비어 있으면 배선 전과 똑같이 군다**(중립 불변식).\n" +
                 "🔑 새 종족을 만드는 비용이 '에셋 1개'로 유지되는 이유가 이 배열이다 — 행동 차이를 " +
                 "코드 분기로 쓰면 종족을 추가할 때마다 코드를 열어야 한다.\n" +
                 "🔒 해금 압력은 **그 수의 답이 마련 가능해지는 시점보다 뒤**여야 한다 (게이트 M24-T4).")]
        public InvaderTrait[] Traits;

        [Header("압력 (M24-1차 W3 — ADR-M24-1)")]
        [Tooltip("조우 1회가 이 종족의 유효압력에 얹는 점수 (k). 유효압력 = 전역압력 + ⌊조우 × k⌋.\n" +
                 "이 값이 **'나에게 적응하는 적'의 체감 속도**다 — 크면 몇 번만 만나도 새 수를 쓰고, " +
                 "0이면 전역압력만 따라간다(중립 = 적응하지 않는 종족).\n" +
                 "제안: 고블린 0.5(빨리 배운다) · 오크 0.25 · 기사단 0.3 · 악마 0.2.\n" +
                 "⚠️ 조우는 **출몰** 기준이다 (ADR-M24-5) — 격퇴 성공만 세지 않는다. 지는 판에서 " +
                 "압력이 멈추면 약한 마을이 영영 안 배우는 적을 만나게 된다.")]
        public float EncounterPressureK = 0.25f;

        [Header("표현")]
        [Tooltip("개체 이동 속도 (타일/초, 실시간). 주민 기본 2.0보다 약간 빠르게 — 제안 2.5.")]
        public float MoveSpeed = 2.5f;

        [Tooltip("스프라이트 폴백 마커 색 (주민 원형 마커 패턴 — 아트는 후속 에셋 교체).")]
        public Color BodyColor = new Color(0.8f, 0.2f, 0.2f);

        [Tooltip("예고 구간 주민 술렁임 대사 (M10-D — 계절 ForecastLines 패턴). 비면 술렁임 없음.")]
        public string[] ForecastLines;

        [Tooltip("주민이 실제로 다친 순간 **다친 본인**이 내뱉는 대사 (예: 물렸어! 도망쳐!). " +
                 "⚠️ 타깃은 출몰마다 롤되므로(ADR-M10R-3) 밭 대사와 반드시 분리해야 한다 — 한 필드로 두면 " +
                 "밭을 친 침입 무리가 '물렸어!'라고 외친다. 부상 0명이면 재생되지 않는다.")]
        public string[] StrikeLinesVillager;

        [Tooltip("밭이 소실된 순간 **근처 주민**이 내뱉는 대사 (예: 밭이 엉망이 됐어!). " +
                 "다친 사람이 없으므로 화자는 타격 지점 최근접 주민이다. 소실 0개면 재생되지 않는다.")]
        public string[] StrikeLinesFarm;

        [Header("공성 (M22-W5, ADR-M22-2 — 방어 시설은 안전을 팔지 않는다)")]
        [Tooltip("방어 시설 타격당 내구도 차감 (절대값 — StrikeDamage와 같은 층). 티어 압박은 " +
                 "에셋 값 차등으로만 (티어1 34 = 3타로 울타리 100 관통, 최상위 100 = 일격). " +
                 "0 = 시설을 못 부순다 — 막히면 기존 출몰 생략 동작으로 돌아간다 (중립).")]
        public float StructureDamage = 34f;

        [Tooltip("시설을 두드리는 동안 **근처 주민**이 내뱉는 대사 (예: 울타리가 버티고 있어!). " +
                 "StructureDamage > 0인데 비면 침묵 공성이 된다 (W2R '내용 없는 분기' 함정 — 게이트 감시).")]
        public string[] StrikeLinesStructure;

        [Tooltip("이 위협이 함정을 밟았을 때 **근처 주민**이 내뱉는 대사 (M22-3차 W4, 예: 걸렸다!). " +
                 "비면 침묵 발동 — 함정은 한 번 쓰고 사라지므로 놓치면 무슨 일이 있었는지 알 수 없다.")]
        public string[] TrapHitLines;

        [Tooltip("이 위협의 그림 (M22-3차 W5c). 주민과 **같은 스프라이트 세트 스키마**를 쓴다 — " +
                 "방향 3종 × 대기/걷기 + 행동 몸짓(Attack). 비면 기존 색 원 폴백 (중립: 배선 전과 동일).\n" +
                 "🔑 아트 교체는 이 한 칸이다 — 라이선스 있는 팩을 구하면 세트 에셋만 갈아 끼우면 된다.")]
        public AgentSpriteSetSO SpriteSet;

        private void OnValidate()
        {
            if (StructureDamage < 0f)
                Debug.LogWarning($"[ThreatSO] {name}: StructureDamage({StructureDamage})는 음수일 수 없습니다 — 0(공성 없음)으로 취급됩니다.", this);
            if (StructureDamage > 0f && (StrikeLinesStructure == null || StrikeLinesStructure.Length == 0))
                Debug.LogWarning($"[ThreatSO] {name}: 공성이 켜져 있는데(StructureDamage) StrikeLinesStructure가 비어 있습니다 — 침묵 공성이 됩니다.", this);
            if (PeriodDays <= 0f)
                Debug.LogWarning($"[ThreatSO] {name}: PeriodDays({PeriodDays})는 양수여야 합니다 — 스케줄이 멈춥니다.", this);
            if (WarnDays >= PeriodDays)
                Debug.LogWarning($"[ThreatSO] {name}: WarnDays({WarnDays}) ≥ PeriodDays({PeriodDays}) — 예고가 항상 켜져 있게 됩니다.", this);
            if (MaxLossPct < 0f || MaxLossPct > 1f)
                Debug.LogWarning($"[ThreatSO] {name}: MaxLossPct({MaxLossPct})는 0~1 비율이어야 합니다.", this);
            if (VillagerTargetChance < 0f || VillagerTargetChance > 1f)
                Debug.LogWarning($"[ThreatSO] {name}: VillagerTargetChance({VillagerTargetChance})는 0~1 이어야 합니다.", this);
            if (MaxHp <= 0f)
                Debug.LogWarning($"[ThreatSO] {name}: MaxHp({MaxHp})는 양수여야 합니다 — 태어나자마자 죽습니다.", this);
            if (StrikeDamage <= 0f)
                Debug.LogWarning($"[ThreatSO] {name}: StrikeDamage({StrikeDamage})는 양수여야 합니다 — 아무도 다치지 않습니다.", this);
            if (RepeatStrikePeriodDays <= 0f)
                Debug.LogWarning($"[ThreatSO] {name}: RepeatStrikePeriodDays({RepeatStrikePeriodDays})는 양수여야 합니다 — 매 프레임 타격하게 됩니다.", this);
            if (MaxStayDays <= 0f)
                Debug.LogWarning($"[ThreatSO] {name}: MaxStayDays({MaxStayDays})는 양수여야 합니다 — 도착 즉시 물러납니다.", this);
            if (WanderRadiusTiles <= 0)
                Debug.LogWarning($"[ThreatSO] {name}: WanderRadiusTiles({WanderRadiusTiles}) ≤ 0 — 도착 지점에 정지합니다 (M21-W2R 이전 동작).", this);
            else if (WanderRadiusTiles <= StrikeRadiusTiles)
                Debug.LogWarning($"[ThreatSO] {name}: WanderRadiusTiles({WanderRadiusTiles}) ≤ StrikeRadiusTiles({StrikeRadiusTiles}) — " +
                                 "배회해도 사거리 밖으로 안 나가 '돌다가 다가온다'가 보이지 않습니다.", this);
            if (HungrySeasonChanceMult < 1f)
                Debug.LogWarning($"[ThreatSO] {name}: HungrySeasonChanceMult({HungrySeasonChanceMult}) < 1 — 무시됩니다 " +
                                 "(배고픈 계절이 더 순해질 수는 없습니다). 1 = 계절 무관.", this);
            if (HungrySeasonStayMult < 1f)
                Debug.LogWarning($"[ThreatSO] {name}: HungrySeasonStayMult({HungrySeasonStayMult}) < 1 — 무시됩니다. 1 = 계절 무관.", this);
            if (SpawnCountBase < 1)
                Debug.LogWarning($"[ThreatSO] {name}: SpawnCountBase({SpawnCountBase}) < 1 — 출몰이 비게 됩니다 (산식이 1로 클램프).", this);
            if (SpawnCountMax < SpawnCountBase)
                Debug.LogWarning($"[ThreatSO] {name}: SpawnCountMax({SpawnCountMax}) < SpawnCountBase({SpawnCountBase}) — 상한이 기본값을 깎습니다.", this);
            if (CountGrowthEveryDays < 0f)
                Debug.LogWarning($"[ThreatSO] {name}: CountGrowthEveryDays({CountGrowthEveryDays})는 음수일 수 없습니다 — 0(성장 없음)으로 취급됩니다.", this);
        }
    }
}
