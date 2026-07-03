/// <summary>
/// AuthoritativeWorldState.cs - 실행 시 월드의 단일 진실 공급원 (Single Source of Truth)
///
/// 역할(Role): 게임의 모든 전역 자원 재고, 월드 플래그, 시간 정보를 보관한다.
///             메인 스레드에서만 쓰기가 허용된다.
///             Job System(GOAP 플래너)은 WorldStateSnapshot을 통해 읽기 전용 복사본을 받는다.
/// 사용법(Usage): GameManager.cs가 생성하고 소유한다.
///               AuthoritativeWorldState.Instance.woodStock += 5f;
/// 의존성(Dependencies): ResourceType.cs (열거형 참조)
///
/// 스레드 안전성 경고:
///   이 클래스의 모든 필드는 메인 스레드에서만 수정해야 한다.
///   C# Job System 워커 스레드에서 직접 쓰지 말 것.
///   읽기는 WorldStateSnapshot.CreateFrom() 을 통해서만 Job에 전달한다.
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-25
/// </summary>

using UnityEngine;
using System.Collections.Generic;
using AIVillage.Core;

namespace AIVillage.Core
{
    /// <summary>
    /// 주민 사망 시 현재 타일에 생성되는 드롭 아이템 데이터.
    /// PickUpDroppedItem GOAP Action이 이 목록을 순회하며 픽업 대상을 탐색한다.
    /// </summary>
    public struct DroppedItem
    {
        public string ItemId;                  // 유일 식별자 (GUID 권장)
        public ItemType ItemType;              // 아이템 종류
        public int TileX;                      // 드롭 위치 X (타일 좌표)
        public int TileY;                      // 드롭 위치 Y (타일 좌표)
        public string OriginalOwnerVillagerId; // 드롭한 주민의 ID (로그/UI용)
    }

    /// <summary>
    /// 게임 월드의 전역 상태를 보관하는 싱글턴 클래스.
    /// MonoBehaviour가 아닌 순수 C# 클래스 — GameManager가 new로 생성하고 생명주기를 관리한다.
    /// </summary>
    public class AuthoritativeWorldState
    {
        // ── 싱글턴 ───────────────────────────────────────────────────────────
        /// <summary>
        /// 전역 접근점. GameManager.Awake()에서 new AuthoritativeWorldState() 후 할당한다.
        /// </summary>
        public static AuthoritativeWorldState Instance { get; private set; }

        // [PR Fix]: SetInstance()를 null 입력(해제)과 non-null 입력(교체)으로 명확히 분기 처리 (F-004)
        /// <summary>
        /// 싱글턴 인스턴스를 설정하거나 해제한다. GameManager 외부에서 호출 금지.
        ///
        /// - instance == null : 기존 Instance를 해제(Debug.Log 기록 후 null 할당)하고 종료.
        /// - instance != null : 기존 Instance 유무를 확인한 후, 있으면 교체 경고 → 새 인스턴스 할당.
        /// </summary>
        public static void SetInstance(AuthoritativeWorldState instance)
        {
            // [PR Fix]: null 전달 시 기존 인스턴스 해제만 수행하고 return (F-004)
            if (instance == null)
            {
                // null이 전달된 경우 = 인스턴스를 해제하려는 의도로 해석한다.
                Debug.Log("[AuthoritativeWorldState] SetInstance(null) 호출 — 기존 인스턴스를 해제합니다.");
                Instance = null;
                return;
            }

            // [PR Fix]: non-null 전달 시 기존 인스턴스 유무를 확인한 후 교체 경고 출력 (F-004)
            if (Instance != null)
            {
                Debug.LogWarning("[AuthoritativeWorldState] 기존 인스턴스가 있는데 새 인스턴스로 교체됩니다. GameManager가 중복 생성되지 않았는지 확인하세요.");
            }

            Instance = instance;
        }

        // ── 자원 재고 (Stock) ─────────────────────────────────────────────────
        // 기획서 수치: 초기값 rawFoodStock=30, woodStock=10, stoneStock=5, 나머지=0

        private float _rawFoodStock    = 30f;  // 기획서 수치: 생 식량 초기 30
        private float _cookedFoodStock = 0f;
        private float _woodStock       = 10f;  // 기획서 수치: 나무 초기 10
        private float _stoneStock      = 5f;   // 기획서 수치: 돌 초기 5
        private float _ironStock       = 0f;
        private float _copperStock     = 0f;
        private float _silverStock     = 0f;

        // ── 자원 예약량 (Reserved) ────────────────────────────────────────────
        // 예약 로직은 ResourceRegistry가 담당한다.
        // 이 필드들은 ResourceRegistry.Commit/Release 시 ResourceRegistry가 직접 갱신한다.
        // 외부에서 직접 수정하지 말 것 — ResourceRegistry를 통해서만 변경한다.

        private float _rawFoodReserved    = 0f;
        private float _cookedFoodReserved = 0f;
        private float _woodReserved       = 0f;
        private float _stoneReserved      = 0f;
        private float _ironReserved       = 0f;
        private float _copperReserved     = 0f;
        private float _silverReserved     = 0f;

        // ── 월드 플래그 ───────────────────────────────────────────────────────
        public bool EnemyNearby     { get; set; } = false;
        public bool BuildingQueued  { get; set; } = false;
        public bool TownHallBuilt   { get; set; } = false;
        public bool ForgeBuilt      { get; set; } = false;
        public bool StorehouseBuilt { get; set; } = false;

        // ── 건물 완성 플래그 ──────────────────────────────────────────────────

        /// <summary>
        /// 모닥불(Campfire) 건설 완료 여부.
        /// VillageAdvisor 우선순위 규칙 1번 조건 판단에 사용한다.
        /// </summary>
        public bool CampfireBuilt { get; set; } = false;

        /// <summary>
        /// 주택(House) 건설 완료 여부.
        /// VillageAdvisor 우선순위 규칙 2번 조건 판단에 사용한다.
        /// </summary>
        public bool HouseBuilt { get; set; } = false;

        // ── 6B단계 추가: 건물 완성 플래그 (FactionAI playerStrength 계산용) ──
        // FactionAI.CalculatePlayerStrength() 공식:
        //   (주민 수 × 10) + (전사 수 × 15) + (무기 보유 수 × 8)
        //   + (Watchtower 완성 × 20) + (Forge 완성 × 15)
        // ForgeBuilt는 위에 이미 있으므로 WatchtowerBuilt만 추가한다.

        /// <summary>
        /// 망루(Watchtower) 건설 완료 여부.
        /// FactionAI의 playerStrength 계산에서 +20 보너스를 적용한다.
        /// BuildingQueue가 Watchtower 완성 시 true로 설정해야 한다.
        /// </summary>
        public bool WatchtowerBuilt { get; set; } = false;

        /// <summary>
        /// Silver Citadel 건설 완료 여부. 최종 승리(번영) 조건.
        /// BuildingQueue가 "SilverCitadel" 완성 시 true로 설정한다.
        /// GameManager.CheckWinLoseConditions()에서 Win3_Prosperity 판정에 사용한다.
        /// </summary>
        public bool SilverCitadelBuilt { get; set; } = false;

        /// <summary>
        /// Town Hall이 이번 게임에서 한 번이라도 완성된 적 있는지 여부.
        /// Town Hall 파괴 패배 조건 판단에 사용한다 (게임 시작 직후 오탐 방지).
        /// TownHallBuilt가 false더라도 이 값이 true이면 Town Hall이 '파괴된' 것으로 간주한다.
        /// BuildingQueue가 TownHall 완성 시 true로 설정하며, 한번 true가 되면 false로 돌아가지 않는다.
        /// </summary>
        public bool TownHallEverBuilt { get; set; } = false;

        /// <summary>
        /// 적 팩션의 실제 침략(RaidDecision) 발생 횟수.
        /// GameManager.OnRaidDecision()에서 TradeProposal을 제외한 침략마다 증가한다.
        /// VillageAdvisor가 Watchtower 건설 조건 판단에 사용한다.
        /// </summary>
        public int RaidCount { get; set; } = 0;

        /// <summary>
        /// 주민 사망 시 생성된 드롭 아이템 목록.
        /// PickUpDroppedItem GOAP Action과 GameManager가 이 목록을 읽고 수정한다.
        /// 메인 스레드에서만 접근할 것.
        /// </summary>
        public List<DroppedItem> DroppedItems { get; private set; } = new List<DroppedItem>();

        // ── 게임 시간 ─────────────────────────────────────────────────────────
        public int    GameDay         { get; set; } = 1;             // 기획서 수치: 시작 Day 1
        public Season Season          { get; set; } = Season.Spring; // 기획서 수치: 시작 Spring
        public float  GameTimeSeconds { get; set; } = 0f;

        // ── Stock 프로퍼티 (음수 방지 클램프 포함) ───────────────────────────
        // 모든 stock 감소 연산에 Mathf.Max(0f, value) 적용 — 기획 규칙

        public float RawFoodStock
        {
            get => _rawFoodStock;
            set => _rawFoodStock = Mathf.Max(0f, value);
        }

        public float CookedFoodStock
        {
            get => _cookedFoodStock;
            set => _cookedFoodStock = Mathf.Max(0f, value);
        }

        public float WoodStock
        {
            get => _woodStock;
            set => _woodStock = Mathf.Max(0f, value);
        }

        public float StoneStock
        {
            get => _stoneStock;
            set => _stoneStock = Mathf.Max(0f, value);
        }

        public float IronStock
        {
            get => _ironStock;
            set => _ironStock = Mathf.Max(0f, value);
        }

        public float CopperStock
        {
            get => _copperStock;
            set => _copperStock = Mathf.Max(0f, value);
        }

        public float SilverStock
        {
            get => _silverStock;
            set => _silverStock = Mathf.Max(0f, value);
        }

        // ── Reserved 프로퍼티 ─────────────────────────────────────────────────
        // [PR Fix]: 모든 Reserved setter에서 Mathf.Max(0f) 대신 Mathf.Clamp(value, 0f, 대응 Stock)으로 변경.
        //           Reserved > Stock 이 되면 GetAvailable()이 음수를 계산하고
        //           GOAP 플래너 스냅샷이 가용량 0으로 오염되어 모든 주민이 행동 불능에 빠지는 버그를 방지한다. (F-001)
        // ResourceRegistry 외부에서 직접 호출하지 말 것.

        // [PR Fix]: Mathf.Clamp(value, 0f, _rawFoodStock) — Stock 상한선 적용 (F-001)
        public float RawFoodReserved
        {
            get => _rawFoodReserved;
            set => _rawFoodReserved = Mathf.Clamp(value, 0f, _rawFoodStock);
        }

        // [PR Fix]: Mathf.Clamp(value, 0f, _cookedFoodStock) — Stock 상한선 적용 (F-001)
        public float CookedFoodReserved
        {
            get => _cookedFoodReserved;
            set => _cookedFoodReserved = Mathf.Clamp(value, 0f, _cookedFoodStock);
        }

        // [PR Fix]: Mathf.Clamp(value, 0f, _woodStock) — Stock 상한선 적용 (F-001)
        public float WoodReserved
        {
            get => _woodReserved;
            set => _woodReserved = Mathf.Clamp(value, 0f, _woodStock);
        }

        // [PR Fix]: Mathf.Clamp(value, 0f, _stoneStock) — Stock 상한선 적용 (F-001)
        public float StoneReserved
        {
            get => _stoneReserved;
            set => _stoneReserved = Mathf.Clamp(value, 0f, _stoneStock);
        }

        // [PR Fix]: Mathf.Clamp(value, 0f, _ironStock) — Stock 상한선 적용 (F-001)
        public float IronReserved
        {
            get => _ironReserved;
            set => _ironReserved = Mathf.Clamp(value, 0f, _ironStock);
        }

        // [PR Fix]: Mathf.Clamp(value, 0f, _copperStock) — Stock 상한선 적용 (F-001)
        public float CopperReserved
        {
            get => _copperReserved;
            set => _copperReserved = Mathf.Clamp(value, 0f, _copperStock);
        }

        // [PR Fix]: Mathf.Clamp(value, 0f, _silverStock) — Stock 상한선 적용 (F-001)
        public float SilverReserved
        {
            get => _silverReserved;
            set => _silverReserved = Mathf.Clamp(value, 0f, _silverStock);
        }

        // ── 생성자 ────────────────────────────────────────────────────────────

        /// <summary>
        /// GDD v0.4 초기값으로 인스턴스를 생성한다.
        /// GameManager.Awake()에서 new AuthoritativeWorldState() 후 SetInstance()를 호출한다.
        /// </summary>
        public AuthoritativeWorldState()
        {
            // 초기값은 프로퍼티 선언부에서 이미 설정됨.
            // 추가 초기화가 필요한 경우 여기에 작성한다.
        }

        // ── 공용 헬퍼 메서드 ──────────────────────────────────────────────────

        /// <summary>
        /// ResourceType 열거형으로 현재 재고를 반환한다.
        /// ResourceRegistry.GetAvailable() 에서 내부적으로 사용한다.
        /// </summary>
        public float GetStock(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.RawFood:    return RawFoodStock;
                case ResourceType.CookedFood: return CookedFoodStock;
                case ResourceType.Wood:       return WoodStock;
                case ResourceType.Stone:      return StoneStock;
                case ResourceType.Iron:       return IronStock;
                case ResourceType.Copper:     return CopperStock;
                case ResourceType.Silver:     return SilverStock;
                default:
                    Debug.LogError($"[AuthoritativeWorldState] GetStock: 알 수 없는 ResourceType '{type}'");
                    return 0f;
            }
        }

        /// <summary>
        /// ResourceType 열거형으로 재고를 설정한다. Mathf.Max(0f, ...) 클램프는 프로퍼티가 처리한다.
        /// </summary>
        public void SetStock(ResourceType type, float value)
        {
            switch (type)
            {
                case ResourceType.RawFood:    RawFoodStock    = value; break;
                case ResourceType.CookedFood: CookedFoodStock = value; break;
                case ResourceType.Wood:       WoodStock       = value; break;
                case ResourceType.Stone:      StoneStock      = value; break;
                case ResourceType.Iron:       IronStock       = value; break;
                case ResourceType.Copper:     CopperStock     = value; break;
                case ResourceType.Silver:     SilverStock     = value; break;
                default:
                    Debug.LogError($"[AuthoritativeWorldState] SetStock: 알 수 없는 ResourceType '{type}'");
                    break;
            }
        }

        /// <summary>
        /// ResourceType 열거형으로 현재 예약량을 반환한다.
        /// </summary>
        public float GetReserved(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.RawFood:    return RawFoodReserved;
                case ResourceType.CookedFood: return CookedFoodReserved;
                case ResourceType.Wood:       return WoodReserved;
                case ResourceType.Stone:      return StoneReserved;
                case ResourceType.Iron:       return IronReserved;
                case ResourceType.Copper:     return CopperReserved;
                case ResourceType.Silver:     return SilverReserved;
                default:
                    Debug.LogError($"[AuthoritativeWorldState] GetReserved: 알 수 없는 ResourceType '{type}'");
                    return 0f;
            }
        }

        /// <summary>
        /// ResourceType 열거형으로 예약량을 설정한다. ResourceRegistry 외에서 직접 호출 금지.
        /// setter가 Mathf.Clamp(value, 0f, Stock)으로 범위를 보장한다.
        /// </summary>
        public void SetReserved(ResourceType type, float value)
        {
            switch (type)
            {
                case ResourceType.RawFood:    RawFoodReserved    = value; break;
                case ResourceType.CookedFood: CookedFoodReserved = value; break;
                case ResourceType.Wood:       WoodReserved       = value; break;
                case ResourceType.Stone:      StoneReserved      = value; break;
                case ResourceType.Iron:       IronReserved       = value; break;
                case ResourceType.Copper:     CopperReserved     = value; break;
                case ResourceType.Silver:     SilverReserved     = value; break;
                default:
                    Debug.LogError($"[AuthoritativeWorldState] SetReserved: 알 수 없는 ResourceType '{type}'");
                    break;
            }
        }
    }
}
