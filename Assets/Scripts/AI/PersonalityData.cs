/// <summary>
/// PersonalityData.cs - F-A 성격별 상수 테이블 (단일 출처)
///
/// 역할(Role): 성격 6종의 라벨·색상·클램프 범위·임계값 오프셋의 단일 출처.
///             배율 값은 여기 두지 않는다 (배율은 GOAPActionRegistry.PersonalityCostMultipliers).
///             ADR-1(수치 단일 출처) 및 명세 §4 FA-1 ⚠️ 오해 위험 항목 준수.
///
/// 사용법(Usage): 정적 클래스. VillagerOverviewPanel/VillagerBrain에서 직접 참조한다.
/// 의존성(Dependencies): VillagerEnums.cs (Personality enum)
///
/// Author: F-A 성격 특성 (재미 로드맵 P0)
/// Last Updated: 2026-07-09
/// </summary>

namespace AIVillage.AI
{
    /// <summary>F-A: 성격별 상수 테이블. 배율·라벨·색상의 단일 출처.</summary>
    public static class PersonalityData
    {
        // ── 배율 클램프 범위 (ADR-P1: 성격 배율 폭발 방지) ──────────────────
        // 성격 배율은 컨텍스트 배율(FULL_NODE_PENALTY×2=10)과 곱해지므로,
        // 상한 초과 시 admissible 휴리스틱이 A*를 안내하지 못하고 MAX_NODES 소진.
        // 검증: EditMode 게이트 T18.

        /// <summary>PersonalityCostMultipliers.From() 반환값의 하한.</summary>
        public const float MULT_MIN = 0.5f;

        /// <summary>PersonalityCostMultipliers.From() 반환값의 상한.</summary>
        public const float MULT_MAX = 2.0f;

        // ── 대식가 P0 임계값 하향 (ADR-P4) ───────────────────────────────────
        // VillagerBrain.HasActiveP0Condition()에서 HungerLevel > 80f - 이 오프셋
        // 배율 대신 임계값 조정 축을 쓰는 이유: 배율은 SurviveHunger Goal 목표치와
        // 즉시 충돌(ADR-7). 임계값 축은 이런 리스크 없음.

        /// <summary>Glutton일 때 배고픔 P0 임계값을 이 값만큼 낮춘다.</summary>
        public const float GLUTTON_HUNGER_THRESHOLD_OFFSET = 10f;

        // ── 라벨/색상 조회 ────────────────────────────────────────────────────

        /// <summary>성격별 한국어 라벨. UI 표시 전용.</summary>
        public static string KoreanLabel(Personality p)
        {
            switch (p)
            {
                case Personality.Coward:   return "겁쟁이";
                case Personality.Brave:    return "용맹";
                case Personality.Diligent: return "부지런";
                case Personality.Lazy:     return "게으름";
                case Personality.Glutton:  return "대식가";
                case Personality.Curious:  return "호기심";
                default:                   return "없음";
            }
        }

        /// <summary>성격별 라벨 색상 (TMP rich text 16진수).</summary>
        public static string HexColor(Personality p)
        {
            switch (p)
            {
                case Personality.Coward:   return "#88DDFF";
                case Personality.Brave:    return "#FF6666";
                case Personality.Diligent: return "#FFDD44";
                case Personality.Lazy:     return "#AAAAAA";
                case Personality.Glutton:  return "#FF88AA";
                case Personality.Curious:  return "#AAFF88";
                default:                   return "#FFFFFF";
            }
        }
    }
}
