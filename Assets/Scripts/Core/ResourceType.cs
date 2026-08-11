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

    // ItemType(폐기된 PickUpDroppedItem용)·Season enum(SeasonSO 에셋 체계 M6가 정본)은
    // 2026-08-11 삭제 — 호출자 0건 (복원은 git 히스토리에서).
}
