/// <summary>
/// BuildingQueue.cs - 건물 건설 대기열 관리자 및 다중 주민 속도 배율 계산기
///
/// 역할(Role): 건설 대기 중인 건물 목록을 순서대로 보관하고,
///             각 건물에 배정된 주민 수에 따른 건설 속도 배율을 계산한다.
///             ActionDatabase.GetDefaultActionSequence()가 다음 건설 대상을 조회할 때 사용한다.
///
/// 사용법(Usage):
///   1. Scene에 빈 GameObject("BuildingQueue")를 생성하고 이 컴포넌트를 부착한다.
///   2. 코드에서 BuildingQueue.Instance로 접근한다.
///   3. 건설 명령 시 EnqueueBuilding()을 호출하고, 주민 배정 시 TryAssignVillager()를 호출한다.
///   4. 건설 완료 시 CompleteBuilding()을 호출하면 WorldState 플래그가 자동 갱신된다.
///
/// 의존성(Dependencies): AuthoritativeWorldState.cs
///
/// 다중 주민 속도 배율 (GDD v0.4 기획서 수치):
///   1명: ×1.0 | 2명: ×1.5 | 3명: ×1.8 | 4명+: ×2.0 (상한)
///   BuildingQueue 슬롯: 최대 3명
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-26
/// </summary>

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.Core
{
    // ══════════════════════════════════════════════════════════════════════════
    // 건설 대기열 상태 열거형
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 건설 대기열 항목의 현재 진행 상태.
    /// </summary>
    public enum BuildingStatus
    {
        Pending,    // 대기 중: 아직 주민이 배정되지 않았거나 시작 전
        InProgress, // 진행 중: 주민이 1명 이상 배정되어 건설 중
        Completed   // 완료: 건설 완료, WorldState 플래그 반영됨
    }

    // ══════════════════════════════════════════════════════════════════════════
    // BuildingQueueEntry 데이터 클래스
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 건설 대기열의 단일 항목. 건물 ID, 위치, 진행률, 배정 주민 목록을 보관한다.
    /// </summary>
    [Serializable]
    public class BuildingQueueEntry
    {
        [Tooltip("대기열 항목의 고유 ID (GUID). TryAssignVillager 등 API 파라미터로 사용한다.")]
        public string QueueId;

        [Tooltip("건설할 건물 ID. GDD 건물 목록: Campfire, House, Storehouse, TownHall, Forge, Watchtower")]
        public string BuildingId;

        [Tooltip("건설 위치 X (타일 좌표).")]
        public int TargetTileX;

        [Tooltip("건설 위치 Y (타일 좌표).")]
        public int TargetTileY;

        [Tooltip("현재 배정된 주민 ID 목록. 최대 3명 (BuildingQueue.MAX_WORKERS 참조).")]
        public List<string> AssignedVillagerIds = new List<string>(3);

        [Tooltip("건설 진행률 (0 ~ 100). GameManager 또는 건설 시스템에서 매 틱 갱신한다.")]
        public float ProgressPercent = 0f;

        [Tooltip("현재 건설 상태. Pending → InProgress → Completed 순으로 전이한다.")]
        public BuildingStatus Status = BuildingStatus.Pending;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // BuildingQueue MonoBehaviour 싱글턴
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 건물 건설 대기열을 관리하는 싱글턴 MonoBehaviour.
    /// Scene에 정확히 1개만 존재해야 한다.
    /// </summary>
    public class BuildingQueue : MonoBehaviour
    {
        #region ── 상수 ──

        /// <summary>건물당 배정 가능한 최대 주민 수 (기획서 수치: 3명).</summary>
        private const int MAX_WORKERS = 3;

        /// <summary>속도 배율: 주민 2명 (기획서 수치: ×1.5).</summary>
        private const float SPEED_MULT_2 = 1.5f;

        /// <summary>속도 배율: 주민 3명 (기획서 수치: ×1.8).</summary>
        private const float SPEED_MULT_3 = 1.8f;

        /// <summary>속도 배율: 주민 4명 이상 상한 (기획서 수치: ×2.0).</summary>
        private const float SPEED_MULT_4_PLUS = 2.0f;

        #endregion

        #region ── 싱글턴 프로퍼티 ──

        /// <summary>
        /// 전역 접근점. Awake()에서 자동 설정된다.
        /// ActionDatabase.Plan_BuildStructure()에서 건설 대기열 조회에 사용한다.
        /// </summary>
        public static BuildingQueue Instance { get; private set; }

        #endregion

        #region ── Private Fields ──

        // 건설 대기열 (순서 보장: 먼저 추가된 건물을 먼저 건설)
        private List<BuildingQueueEntry> _queue = new List<BuildingQueueEntry>();

        #endregion

        #region ── Unity 생명주기 ──

        /// <summary>
        /// 싱글턴 인스턴스를 설정한다.
        /// 씬에 둘 이상의 BuildingQueue가 있으면 경고 후 중복을 Destroy한다.
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[BuildingQueue] Awake: 싱글턴 중복 감지. 이 인스턴스를 Destroy합니다.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        /// <summary>씬 언로드 또는 Destroy 시 싱글턴 참조를 해제한다.</summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region ── 공개 API ──

        /// <summary>
        /// 새 건물을 건설 대기열 맨 뒤에 추가한다.
        /// 동일 BuildingId가 이미 Pending 또는 InProgress 상태이면 추가하지 않고 false를 반환한다.
        /// </summary>
        /// <param name="buildingId">건설할 건물 ID (예: "TownHall", "Forge")</param>
        /// <param name="targetTileX">건설 위치 X (타일 좌표)</param>
        /// <param name="targetTileY">건설 위치 Y (타일 좌표)</param>
        /// <returns>대기열 추가 성공 여부</returns>
        public bool EnqueueBuilding(string buildingId, int targetTileX, int targetTileY)
        {
            if (string.IsNullOrEmpty(buildingId))
            {
                Debug.LogWarning("[BuildingQueue] EnqueueBuilding: buildingId가 null 또는 빈 문자열입니다.");
                return false;
            }

            // 동일 건물이 이미 대기 또는 진행 중이면 중복 추가 방지
            foreach (BuildingQueueEntry existing in _queue)
            {
                if (existing.BuildingId == buildingId
                    && (existing.Status == BuildingStatus.Pending
                     || existing.Status == BuildingStatus.InProgress))
                {
                    Debug.Log($"[BuildingQueue] EnqueueBuilding: '{buildingId}'가 이미 대기열에 있습니다 " +
                                  $"(QueueId={existing.QueueId}, Status={existing.Status}). 중복 추가 거부.");
                    return false;
                }
            }

            var entry = new BuildingQueueEntry
            {
                QueueId     = Guid.NewGuid().ToString(),
                BuildingId  = buildingId,
                TargetTileX = targetTileX,
                TargetTileY = targetTileY,
                Status      = BuildingStatus.Pending
            };

            _queue.Add(entry);

            Debug.Log($"[BuildingQueue] '{buildingId}' 대기열 추가 완료. " +
                      $"QueueId={entry.QueueId}, 위치=({targetTileX},{targetTileY}), " +
                      $"대기열 총 {_queue.Count}개.");
            return true;
        }

        /// <summary>
        /// 지정한 buildingId가 이미 Pending 또는 InProgress 상태로 대기열에 있는지 확인한다.
        /// 호출자가 EnqueueBuilding을 시도하기 전에 사전 확인하여 중복 거부 로그 스팸을 방지한다.
        /// </summary>
        public bool IsBuildingPending(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId)) return false;

            foreach (BuildingQueueEntry entry in _queue)
            {
                if (entry.BuildingId == buildingId
                    && (entry.Status == BuildingStatus.Pending
                     || entry.Status == BuildingStatus.InProgress))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 가장 오래된 Pending 또는 InProgress 상태의 건설 항목을 반환한다.
        /// ActionDatabase.Plan_BuildStructure()에서 다음 건설 대상을 파악할 때 호출한다.
        /// </summary>
        /// <returns>첫 번째 미완료 BuildingQueueEntry. 없으면 null.</returns>
        public BuildingQueueEntry GetNextPendingBuilding()
        {
            foreach (BuildingQueueEntry entry in _queue)
            {
                if (entry.Status == BuildingStatus.Pending
                 || entry.Status == BuildingStatus.InProgress)
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>
        /// 지정한 대기열 항목에 주민을 배정한다.
        /// 최대 MAX_WORKERS(3명)를 초과하거나 이미 배정된 주민이면 false를 반환한다.
        /// 첫 배정 시 Status를 Pending → InProgress로 전이한다.
        /// </summary>
        /// <param name="buildingQueueId">배정할 대기열 항목의 QueueId</param>
        /// <param name="villagerId">배정할 주민의 AgentId</param>
        /// <returns>배정 성공 여부</returns>
        public bool TryAssignVillager(string buildingQueueId, string villagerId)
        {
            if (string.IsNullOrEmpty(buildingQueueId) || string.IsNullOrEmpty(villagerId))
            {
                Debug.LogWarning("[BuildingQueue] TryAssignVillager: buildingQueueId 또는 villagerId가 null/빈 문자열입니다.");
                return false;
            }

            BuildingQueueEntry entry = FindEntry(buildingQueueId);
            if (entry == null)
            {
                Debug.LogWarning($"[BuildingQueue] TryAssignVillager: QueueId '{buildingQueueId}'를 찾지 못했습니다.");
                return false;
            }

            if (entry.Status == BuildingStatus.Completed)
            {
                Debug.LogWarning($"[BuildingQueue] TryAssignVillager: '{entry.BuildingId}'는 이미 완료된 건물입니다.");
                return false;
            }

            // 슬롯 초과 확인 (기획서 수치: 최대 3명)
            if (entry.AssignedVillagerIds.Count >= MAX_WORKERS)
            {
                Debug.LogWarning($"[BuildingQueue] TryAssignVillager: '{entry.BuildingId}' 슬롯이 " +
                                 $"가득 찼습니다 ({MAX_WORKERS}명 최대). VillagerId={villagerId}");
                return false;
            }

            // 중복 배정 확인
            if (entry.AssignedVillagerIds.Contains(villagerId))
            {
                Debug.LogWarning($"[BuildingQueue] TryAssignVillager: VillagerId '{villagerId}'가 이미 " +
                                 $"'{entry.BuildingId}'에 배정되어 있습니다.");
                return false;
            }

            entry.AssignedVillagerIds.Add(villagerId);

            // 첫 주민 배정 시 Pending → InProgress 전이
            if (entry.Status == BuildingStatus.Pending)
            {
                entry.Status = BuildingStatus.InProgress;
            }

            Debug.Log($"[BuildingQueue] VillagerId '{villagerId}' → '{entry.BuildingId}' 배정 완료. " +
                      $"배정 인원: {entry.AssignedVillagerIds.Count}/{MAX_WORKERS}명. " +
                      $"속도 배율: x{CalculateSpeedMultiplier(entry.AssignedVillagerIds.Count):F1}.");
            return true;
        }

        /// <summary>
        /// 지정한 대기열 항목에서 주민 배정을 해제한다.
        /// 해당 주민이 없으면 경고만 출력하고 종료한다.
        /// 마지막 주민이 해제되면 Status를 InProgress → Pending으로 되돌린다.
        /// </summary>
        /// <param name="buildingQueueId">해제할 대기열 항목의 QueueId</param>
        /// <param name="villagerId">해제할 주민의 AgentId</param>
        public void UnassignVillager(string buildingQueueId, string villagerId)
        {
            if (string.IsNullOrEmpty(buildingQueueId) || string.IsNullOrEmpty(villagerId))
            {
                Debug.LogWarning("[BuildingQueue] UnassignVillager: buildingQueueId 또는 villagerId가 null/빈 문자열입니다.");
                return;
            }

            BuildingQueueEntry entry = FindEntry(buildingQueueId);
            if (entry == null)
            {
                Debug.LogWarning($"[BuildingQueue] UnassignVillager: QueueId '{buildingQueueId}'를 찾지 못했습니다.");
                return;
            }

            bool removed = entry.AssignedVillagerIds.Remove(villagerId);

            if (!removed)
            {
                Debug.LogWarning($"[BuildingQueue] UnassignVillager: VillagerId '{villagerId}'가 " +
                                 $"'{entry.BuildingId}' 배정 목록에 없습니다.");
                return;
            }

            // 마지막 주민이 빠지면 InProgress → Pending 복귀
            if (entry.AssignedVillagerIds.Count == 0 && entry.Status == BuildingStatus.InProgress)
            {
                entry.Status = BuildingStatus.Pending;
            }

            Debug.Log($"[BuildingQueue] VillagerId '{villagerId}' → '{entry.BuildingId}' 배정 해제. " +
                      $"남은 인원: {entry.AssignedVillagerIds.Count}명.");
        }

        /// <summary>
        /// 배정 인원 수에 따른 건설 속도 배율을 반환한다.
        ///
        /// GDD v0.4 기획서 수치:
        ///   0명 → 0.0 (건설 중단)
        ///   1명 → 1.0
        ///   2명 → 1.5
        ///   3명 → 1.8
        ///   4명+→ 2.0 (상한 고정)
        /// </summary>
        /// <param name="buildingQueueId">속도를 계산할 대기열 항목의 QueueId</param>
        /// <returns>현재 배정 인원 수 기반 속도 배율. 항목이 없으면 0f.</returns>
        public float GetSpeedMultiplier(string buildingQueueId)
        {
            if (string.IsNullOrEmpty(buildingQueueId))
            {
                Debug.LogWarning("[BuildingQueue] GetSpeedMultiplier: buildingQueueId가 null/빈 문자열입니다.");
                return 0f;
            }

            BuildingQueueEntry entry = FindEntry(buildingQueueId);
            if (entry == null)
            {
                Debug.LogWarning($"[BuildingQueue] GetSpeedMultiplier: QueueId '{buildingQueueId}'를 찾지 못했습니다.");
                return 0f;
            }

            return CalculateSpeedMultiplier(entry.AssignedVillagerIds.Count);
        }

        /// <summary>
        /// 건설을 완료 처리한다. 진행률을 100으로 설정하고 WorldState 플래그를 갱신한다.
        /// Status를 Completed로 전이하며 완료된 항목은 다음 GetNextPendingBuilding() 호출에서 제외된다.
        /// </summary>
        /// <param name="buildingQueueId">완료할 대기열 항목의 QueueId</param>
        /// <param name="worldState">플래그를 갱신할 월드 상태. null이면 플래그 갱신 건너뜀.</param>
        public void CompleteBuilding(string buildingQueueId, AuthoritativeWorldState worldState)
        {
            if (string.IsNullOrEmpty(buildingQueueId))
            {
                Debug.LogWarning("[BuildingQueue] CompleteBuilding: buildingQueueId가 null/빈 문자열입니다.");
                return;
            }

            BuildingQueueEntry entry = FindEntry(buildingQueueId);
            if (entry == null)
            {
                Debug.LogWarning($"[BuildingQueue] CompleteBuilding: QueueId '{buildingQueueId}'를 찾지 못했습니다.");
                return;
            }

            if (entry.Status == BuildingStatus.Completed)
            {
                Debug.LogWarning($"[BuildingQueue] CompleteBuilding: '{entry.BuildingId}'는 이미 완료 상태입니다.");
                return;
            }

            entry.ProgressPercent = 100f;
            entry.Status          = BuildingStatus.Completed;
            entry.AssignedVillagerIds.Clear(); // 완료 후 주민 배정 해제

            // WorldState 플래그 갱신
            ApplyBuildingCompletedEffect(entry.BuildingId, worldState);

            Debug.Log($"[BuildingQueue] '{entry.BuildingId}' 건설 완료. " +
                      $"위치=({entry.TargetTileX},{entry.TargetTileY}). QueueId={buildingQueueId}.");
        }

        /// <summary>
        /// 현재 대기열 전체를 읽기 전용으로 반환한다.
        /// UI 또는 GameManager에서 대기열 현황을 표시할 때 사용한다.
        /// </summary>
        /// <returns>전체 BuildingQueueEntry 목록 (읽기 전용).</returns>
        public IReadOnlyList<BuildingQueueEntry> GetAllEntries()
        {
            return _queue.AsReadOnly();
        }

        /// <summary>
        /// 대기열에서 완료된(Completed) 항목을 모두 제거한다.
        /// GameManager에서 주기적으로 호출하여 메모리를 정리한다.
        /// </summary>
        public void PurgeCompletedEntries()
        {
            int removed = _queue.RemoveAll(e => e.Status == BuildingStatus.Completed);
            if (removed > 0)
            {
                Debug.Log($"[BuildingQueue] PurgeCompletedEntries: 완료된 항목 {removed}개 제거. " +
                          $"남은 대기열: {_queue.Count}개.");
            }
        }

        #endregion

        #region ── Private 헬퍼 메서드 ──

        /// <summary>
        /// QueueId로 대기열 항목을 찾아 반환한다. 없으면 null.
        /// </summary>
        private BuildingQueueEntry FindEntry(string buildingQueueId)
        {
            foreach (BuildingQueueEntry entry in _queue)
            {
                if (entry.QueueId == buildingQueueId) return entry;
            }
            return null;
        }

        /// <summary>
        /// 배정 인원 수로 건설 속도 배율을 계산한다.
        ///
        /// 공식 (GDD v0.4 기획서 수치):
        ///   0명  → 0.0f (건설 중단)
        ///   1명  → 1.0f
        ///   2명  → 1.5f (SPEED_MULT_2)
        ///   3명  → 1.8f (SPEED_MULT_3)
        ///   4명+ → 2.0f (SPEED_MULT_4_PLUS, 상한 고정)
        /// </summary>
        private static float CalculateSpeedMultiplier(int assignedCount)
        {
            if (assignedCount <= 0) return 0f;
            if (assignedCount == 1) return 1.0f;
            if (assignedCount == 2) return SPEED_MULT_2;    // 기획서 수치: x1.5
            if (assignedCount == 3) return SPEED_MULT_3;    // 기획서 수치: x1.8
            return SPEED_MULT_4_PLUS;                        // 기획서 수치: 4명+ 상한 x2.0
        }

        /// <summary>
        /// 건물 완료 시 AuthoritativeWorldState의 관련 플래그를 갱신한다.
        /// worldState가 null이면 경고만 출력하고 종료한다.
        /// BuildingQueued 플래그는 완료 처리 후 남은 미완료 항목이 있는지를 기준으로 갱신한다.
        /// </summary>
        private void ApplyBuildingCompletedEffect(string buildingId, AuthoritativeWorldState worldState)
        {
            if (worldState == null)
            {
                Debug.LogWarning($"[BuildingQueue] ApplyBuildingCompletedEffect: worldState가 null입니다. " +
                                 $"'{buildingId}' 완료 플래그를 설정할 수 없습니다.");
                return;
            }

            // 남은 미완료 항목 유무로 BuildingQueued 갱신
            bool anyPending = _queue.Exists(e => e.Status != BuildingStatus.Completed);

            switch (buildingId)
            {
                case "Campfire":
                    worldState.CampfireBuilt  = true;
                    worldState.BuildingQueued = anyPending;
                    Debug.Log("[BuildingQueue] Campfire 완료 — worldState.CampfireBuilt = true.");
                    break;

                case "Storehouse":
                    worldState.StorehouseBuilt = true;
                    worldState.BuildingQueued   = anyPending;
                    Debug.Log("[BuildingQueue] Storehouse 완료 — worldState.StorehouseBuilt = true.");
                    break;

                case "TownHall":
                    worldState.TownHallBuilt    = true;
                    worldState.TownHallEverBuilt = true; // 11단계 추가: 한번이라도 완성됐음을 영구 기록 (패배 조건 오탐 방지)
                    worldState.BuildingQueued   = anyPending;
                    Debug.Log("[BuildingQueue] TownHall 완료 — worldState.TownHallBuilt = true, TownHallEverBuilt = true.");
                    break;

                case "SilverCitadel":
                    // 11단계 추가: 최종 승리(번영) 조건 — 완성 즉시 GameManager.CheckWinLoseConditions()가 Win3_Prosperity를 발생시킨다.
                    worldState.SilverCitadelBuilt = true;
                    worldState.BuildingQueued     = anyPending;
                    Debug.Log("[BuildingQueue] SilverCitadel 완료 — worldState.SilverCitadelBuilt = true.");
                    break;

                case "Forge":
                    worldState.ForgeBuilt     = true;
                    worldState.BuildingQueued = anyPending;
                    Debug.Log("[BuildingQueue] Forge 완료 — worldState.ForgeBuilt = true.");
                    break;

                case "House":
                    worldState.HouseBuilt     = true;
                    worldState.BuildingQueued = anyPending;
                    Debug.Log("[BuildingQueue] House 완료 — worldState.HouseBuilt = true.");
                    break;

                case "Watchtower":
                    worldState.WatchtowerBuilt = true;
                    worldState.BuildingQueued  = anyPending;
                    Debug.Log("[BuildingQueue] Watchtower 완료 — worldState.WatchtowerBuilt = true.");
                    break;

                default:
                    worldState.BuildingQueued = anyPending;
                    Debug.LogWarning($"[BuildingQueue] ApplyBuildingCompletedEffect: 알 수 없는 BuildingId '{buildingId}'. " +
                                     $"BuildingQueued만 갱신합니다.");
                    break;
            }
        }

        #endregion

        #region ── Unity Editor 디버그 헬퍼 ──

#if UNITY_EDITOR
        /// <summary>
        /// 씬 뷰에서 대기열 현황 레이블을 출력한다.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            int inProgressCount = 0;
            foreach (BuildingQueueEntry e in _queue)
            {
                if (e.Status == BuildingStatus.InProgress) inProgressCount++;
            }

            UnityEditor.Handles.color = Color.cyan;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 3f,
                $"[BuildingQueue]\n대기열: {_queue.Count}개\n진행중: {inProgressCount}개"
            );
        }
#endif

        #endregion
    }
}
