/// <summary>
/// ResourceType.cs - 게임 내 자원 종류를 정의하는 열거형
///
/// 역할(Role): 자원 타입을 타입 안전한 열거형으로 정의하여
///             ResourceRegistry, AuthoritativeWorldState, GOAP Action 전반에서 공유한다.
/// 사용법(Usage): ResourceType.Wood 처럼 직접 참조. 별도 MonoBehaviour 불필요.
/// 의존성(Dependencies): 없음 (가장 하위 레이어)
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-25
/// </summary>

namespace AIVillage.Core
{
    /// <summary>
    /// 게임 내 존재하는 모든 자원 종류.
    /// 순서를 변경하면 WorldStateIndices 상수와 불일치가 발생하므로 변경 금지.
    /// 새 자원 추가 시 WorldStateIndices.TOTAL_COUNT도 함께 갱신할 것.
    /// </summary>
    public enum ResourceType
    {
        RawFood    = 0,  // 생 식량 (채집/사냥): 요리 재료, 비상 섭취 가능 (저효율)
        CookedFood = 1,  // 조리된 식량: 요리사가 rawFood 3 → cookedFood 2로 변환
        Wood       = 2,  // 나무: 건설, 도구, 원시 무기 재료
        Stone      = 3,  // 돌: 건설, 원시 무기 재료
        Iron       = 4,  // 철광석: 고급 건설 및 무기
        Copper     = 5,  // 구리: 중급 자원, 침략 트리거 조건 포함
        Silver     = 6   // 은: 희귀 자원, 침략 트리거 조건 포함
    }

    /// <summary>
    /// 주민 사망 시 현재 위치에 드롭되는 아이템 종류.
    /// PickUpDroppedItem GOAP Action의 Precondition 판별에 사용.
    /// </summary>
    public enum ItemType
    {
        Axe,             // 나무꾼 도구
        Pickaxe,         // 광부 도구
        Weapon,          // 전투용 무기 (Forge 제작)
        PrimitiveWeapon, // 원시 무기 (Wood 3 + Stone 2, Forge 불필요)
        Food             // 인벤토리에 보유 중이던 식량
    }

    /// <summary>
    /// 게임 내 계절. 4계절 순환이며 Winter에 식량 위기가 발생한다.
    /// GameTimeManager가 gameDay 기준으로 자동 전환.
    /// </summary>
    public enum Season
    {
        Spring = 0,
        Summer = 1,
        Autumn = 2,  // 가을 시작 시 GatherResources 가중치 ×3 자동 적용
        Winter = 3   // 겨울 경보 임계값: cookedFoodStock < (주민 수 × 3 × 30)
    }
}
