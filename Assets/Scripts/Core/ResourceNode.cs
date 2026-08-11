/// <summary>
/// ResourceNode.cs - 타일 좌표계 자원 노드 데이터 클래스
///
/// 역할(Role): 게임 맵 위에 존재하는 채집 가능한 자원 노드 1개를 표현한다.
///             SensorSystem이 이 데이터를 읽어 VillagerBrain의 환경 플래그를 갱신한다.
///             순수 C# 클래스 — MonoBehaviour가 아니다.
///
/// 사용법(Usage):
///   var node = new ResourceNode("node_guid", ResourceType.Stone, tileX:5, tileY:3, maxAmount:100f);
///   SensorSystem.Instance.AddResourceNode(node);
///
/// 의존성(Dependencies): ResourceType.cs (AIVillage.Core)
///
/// FoW(안개 전쟁) 통합:
///   IsDiscovered == false  → GOAP 플래너가 이 노드를 인식하지 못한다 (미탐험 구역).
///   SensorSystem.DiscoverArea() 또는 VillagerFSM의 Explore Action 완료 시 IsDiscovered = true 로 전환.
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-26
/// </summary>

using System;
using UnityEngine;

namespace AIVillage.Core
{
    /// <summary>
    /// 맵 위의 자원 노드 1개를 나타내는 런타임 데이터.
    /// SensorSystem의 내부 리스트에서 관리되며 GOAP 플래너의 Precondition 판정 기준이 된다.
    /// </summary>
    public class ResourceNode
    {
        // 재생률(per game day)은 M1-B에서 ResourceNodeSpawnConfig 에셋으로 이관됨 (ADR-M1-4).
        // 코드에는 어떤 자원의 재생 수치도 존재하지 않는다 — 생성자 인자로 주입받는다.

        #region ── 식별 정보 ──

        /// <summary>
        /// 노드의 전역 고유 식별자. SensorSystem 딕셔너리의 키로 사용된다.
        /// 생성 시 System.Guid.NewGuid().ToString()으로 자동 생성하거나 맵 에디터에서 지정한다.
        /// </summary>
        public string NodeId { get; private set; }

        /// <summary>
        /// 이 노드에서 채집할 수 있는 자원 종류.
        /// SensorSystem이 NearRock(Stone), NearIronOre(Iron), NearCopperOre(Copper) 플래그를 설정할 때 사용한다.
        /// </summary>
        public ResourceType ResourceType { get; private set; }

        #endregion

        #region ── 위치 정보 (타일 좌표계) ──

        /// <summary>
        /// 노드의 타일 X 좌표. SensorSystem이 맨해튼 거리를 계산할 때 사용한다.
        /// Transform.position.x를 Mathf.RoundToInt()로 변환한 값.
        /// </summary>
        public int TileX { get; set; }

        /// <summary>
        /// 노드의 타일 Y 좌표. Unity의 Z축(앞뒤)에 해당한다.
        /// Transform.position.z를 Mathf.RoundToInt()로 변환한 값.
        /// </summary>
        public int TileY { get; set; }

        #endregion

        #region ── 자원 수량 ──

        /// <summary>
        /// 현재 남은 자원량. 0이면 이 노드에서 더 이상 채집 불가.
        /// SensorSystem.TickResourceRegeneration()에서 매 게임 일수마다 RegenerationRate만큼 증가한다.
        /// </summary>
        public float CurrentAmount { get; set; }

        /// <summary>
        /// 이 노드가 보유할 수 있는 최대 자원량. CurrentAmount의 상한.
        /// </summary>
        public float MaxAmount { get; set; }

        /// <summary>
        /// 게임 일수(game day)당 자원 재생량.
        /// 기획서 수치: Wood=5, Stone=3, Iron=1.5, Copper=0.5, Silver=0.2 / RawFood=0
        /// </summary>
        public float RegenerationRate { get; set; }

        #endregion

        #region ── 상태 플래그 ──

        /// <summary>
        /// FoW(안개 전쟁) 발견 여부.
        /// false이면 GOAP 플래너가 이 노드를 인식하지 못한다 (Brain.NearDiscoveredResource에 영향).
        /// VillagerFSM의 Explore Action 완료 시 SensorSystem.DiscoverArea()가 true로 설정한다.
        /// 기본값 false — 모든 노드는 처음에 미발견 상태다.
        /// TODO: 기획팀 — 게임 시작 시 기지 주변 일정 반경은 IsDiscovered=true로 초기화할지 확인 필요
        /// </summary>
        public bool IsDiscovered { get; set; } = false;

        /// <summary>
        /// 개간되어 세상에서 지워졌는가 (M22-4차).
        ///
        /// 🔴 **고갈과 다르다.** 고갈(CurrentAmount == 0)은 재생이 되살리고 타일도 계속 점유하지만,
        /// 제거는 되돌아오지 않고 타일이 열린다 — 그래야 그 자리에 울타리가 선다.
        ///
        /// 쓰기는 `DiscoveryService.RemoveNode` 한 곳뿐 (ADR-C-2). 뷰는 이 값을 보고 스스로
        /// 사라진다 — Core가 M0 서비스를 알면 의존이 거꾸로 서므로 이벤트가 아니라 플래그다.
        /// </summary>
        public bool IsRemoved { get; set; } = false;

        /// <summary>
        /// 현재 이 노드에서 채집 중인 주민 수. TryOccupy/Release 메서드로만 변경한다.
        /// </summary>
        public int CurrentGatherers { get; private set; } = 0;

        /// <summary>
        /// 동시 채집 허용 최대 인원.
        /// 1명으로 제한 — 여러 주민이 같은 노드에 몰려 콜라이더가 서로 밀어내며
        /// 최전방 유닛만 접근하고 나머지가 튕겨나가는 시각적 문제를 방지한다.
        /// 다른 주민은 FindNearestDiscoveredNode 필터에서 이 노드가 제외되어 다른 노드를 선택한다.
        /// </summary>
        public const int MaxGatherers = 1;

        #endregion

        #region ── 생성자 ──

        /// <summary>
        /// 자주 사용하는 파라미터를 받는 편의 생성자.
        /// 재생률은 호출자(ResourceNodeSpawner가 ResourceNodeSpawnConfig에서 읽음)가 주입한다 (ADR-M1-4).
        /// </summary>
        /// <param name="nodeId">고유 식별자 (null이면 GUID 자동 생성)</param>
        /// <param name="resourceType">자원 종류</param>
        /// <param name="tileX">타일 X 좌표</param>
        /// <param name="tileY">타일 Y 좌표</param>
        /// <param name="maxAmount">최대 자원량</param>
        /// <param name="regenPerDay">게임 1일당 재생량 (기본 0 = 재생 없음)</param>
        /// <param name="isDiscovered">초기 발견 여부 (기본 false)</param>
        public ResourceNode(
            string       nodeId,
            ResourceType resourceType,
            int          tileX,
            int          tileY,
            float        maxAmount,
            float        regenPerDay  = 0f,
            bool         isDiscovered = false)
        {
            // nodeId가 null이면 GUID 자동 생성
            NodeId                = string.IsNullOrEmpty(nodeId) ? Guid.NewGuid().ToString() : nodeId;
            ResourceType          = resourceType;
            TileX                 = tileX;
            TileY                 = tileY;
            MaxAmount             = maxAmount;
            CurrentAmount         = maxAmount;   // 신규 노드는 꽉 찬 상태로 시작
            RegenerationRate      = regenPerDay; // 계절 배율은 SimulationLoop이 regenMult로 별도 적용
            IsDiscovered          = isDiscovered;
        }

        #endregion

        #region ── 공개 헬퍼 메서드 ──

        /// <summary>
        /// 채집 자리를 1개 점유한다. CurrentGatherers >= MaxGatherers이면 false를 반환한다.
        /// </summary>
        /// <param name="villagerId">점유 요청 주민 ID (로그용)</param>
        /// <returns>점유 성공 여부</returns>
        public bool TryOccupy(string villagerId)
        {
            if (CurrentGatherers >= MaxGatherers)
            {
                Debug.Log(
                    $"[ResourceNode] TryOccupy 실패: 포화 상태 ({CurrentGatherers}/{MaxGatherers}). " +
                    $"NodeId={NodeId}, 요청 VillagerId={villagerId}"
                );
                return false;
            }

            CurrentGatherers++;
            Debug.Log(
                $"[ResourceNode] TryOccupy 성공. NodeId={NodeId}, ResourceType={ResourceType}, " +
                $"Tile=({TileX},{TileY}), CurrentGatherers={CurrentGatherers}/{MaxGatherers}, " +
                $"VillagerId={villagerId}"
            );
            return true;
        }

        /// <summary>
        /// 채집 점유를 1개 해제한다. 채집 완료 또는 주민 사망/이탈 시 호출한다.
        /// </summary>
        public void Release()
        {
            CurrentGatherers = Mathf.Max(0, CurrentGatherers - 1);
            Debug.Log(
                $"[ResourceNode] Release. NodeId={NodeId}, Tile=({TileX},{TileY}), " +
                $"CurrentGatherers={CurrentGatherers}/{MaxGatherers}"
            );
        }

        #endregion

    }
}
