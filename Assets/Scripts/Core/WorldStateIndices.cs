/// <summary>
/// WorldStateIndices.cs - WorldStateSnapshot NativeArray의 인덱스 상수 정의
///
/// 역할(Role): NativeArray<int>로 직렬화된 월드 스테이트의 각 슬롯 번호를 명명된 상수로 관리.
///             매직 넘버 사용을 방지하고, GOAP Job 코드에서 안전하게 참조할 수 있도록 한다.
/// 사용법(Usage): WorldStateIndices.WoodStock 처럼 직접 참조.
///               NativeArray<int> data = snapshot.Data;
///               float wood = BitConverter.Int32BitsToSingle(data[WorldStateIndices.WoodStock]);
/// 의존성(Dependencies): 없음 (WorldStateSnapshot이 이 클래스를 참조)
///
/// 경고: 인덱스 번호를 변경하면 WorldStateSnapshot.CreateFrom()과 모든 GOAP Action이
///      동시에 수정되어야 한다. 번호 순서 변경 금지. 끝에만 추가할 것.
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-25
/// </summary>

namespace AIVillage.Core
{
    /// <summary>
    /// WorldStateSnapshot.Data (NativeArray&lt;int&gt;) 의 슬롯 인덱스 상수 모음.
    /// float 값은 BitConverter.SingleToInt32Bits / Int32BitsToSingle 로 변환하여 저장/읽기.
    /// bool 값은 0(false) / 1(true) 로 저장.
    /// </summary>
    public static class WorldStateIndices
    {
        // ── 자원 재고 (float → int 비트 변환 저장) ──────────────────────────
        public const int RawFoodStock    = 0;
        public const int CookedFoodStock = 1;
        public const int WoodStock       = 2;
        public const int StoneStock      = 3;
        public const int IronStock       = 4;
        public const int CopperStock     = 5;
        public const int SilverStock     = 6;

        // ── 예약량 (float → int 비트 변환 저장) ─────────────────────────────
        public const int RawFoodReserved    = 7;
        public const int CookedFoodReserved = 8;
        public const int WoodReserved       = 9;
        public const int StoneReserved      = 10;
        public const int IronReserved       = 11;
        public const int CopperReserved     = 12;
        public const int SilverReserved     = 13;

        // ── 월드 플래그 (bool → 0 or 1 저장) ────────────────────────────────
        public const int EnemyNearby     = 14;
        public const int BuildingQueued  = 15;
        public const int TownHallBuilt   = 16;
        public const int ForgeBuilt      = 17;
        public const int StorehouseBuilt = 18;

        // ── 게임 시간 (int 직접 저장) ─────────────────────────────────────────
        public const int GameDay = 19;
        public const int Season  = 20;  // Season 열거형 int 값 그대로 저장

        // ── 배열 크기 ─────────────────────────────────────────────────────────
        // 새 인덱스 추가 시 이 값을 반드시 갱신할 것
        public const int TOTAL_COUNT = 21;

        // [PR Fix]: ReservedIndexOf()의 하드코딩 오프셋 7을 명명된 상수 ReservedOffset으로 분리 (F-008)
        // 인덱스 레이아웃 변경 시 이 상수 하나만 수정하면 되므로 조용한 버그를 방지한다.
        // 계산 근거: RawFoodReserved(7) - RawFoodStock(0) = 7
        private const int ReservedOffset = RawFoodReserved - RawFoodStock;

        // ── 헬퍼: ResourceType → Stock 인덱스 변환 ───────────────────────────
        /// <summary>
        /// ResourceType 열거형으로 해당 자원의 Stock 슬롯 인덱스를 반환한다.
        /// GOAP Action에서 ResourceType을 직접 인덱스로 변환할 때 사용.
        /// </summary>
        public static int StockIndexOf(ResourceType type)
        {
            // ResourceType의 int 값(0~6)이 Stock 인덱스(0~6)와 1:1 대응되도록 설계되어 있다.
            return (int)type;
        }

        // [PR Fix]: 하드코딩된 + 7 대신 ReservedOffset 상수를 사용하여 인덱스 변경 시 버그를 방지 (F-008)
        /// <summary>
        /// ResourceType 열거형으로 해당 자원의 Reserved 슬롯 인덱스를 반환한다.
        /// Reserved 슬롯은 Stock 슬롯에서 ReservedOffset만큼 뒤에 위치한다.
        /// </summary>
        public static int ReservedIndexOf(ResourceType type)
        {
            return (int)type + ReservedOffset;
        }
    }
}
