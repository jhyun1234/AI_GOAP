/// <summary>
/// ResourceRegistry.cs - 자원 예약 관리자
///
/// 역할(Role): GOAP Action이 자원을 "사용하겠다"고 예약(Reserve)하고,
///             실제 사용 완료 시 확정(Commit)하거나 취소 시 해제(Release)하는 중재자.
///             이 시스템이 없으면 여러 주민이 동일 자원을 동시에 사용하려다
///             재고 음수가 발생하는 Race Condition이 생긴다.
///
/// 예약 흐름:
///   1. Action 시작: Reserve(agentId, type, amount) → 가용량 충분하면 예약 등록, 부족하면 false
///   2. Action 완료: Commit(agentId, type, amount) → stock 차감 + reserved 해제, 성공 시 true 반환
///   3. Action 취소: Release(agentId, type, amount) → reserved만 해제 (stock 유지)
///   4. 주민 사망:   ReleaseAll(agentId) → 해당 주민의 모든 예약 해제
///
/// 사용법(Usage): GameManager가 소유. MonoBehaviour 없이 순수 C# 클래스.
///   var registry = new ResourceRegistry(AuthoritativeWorldState.Instance);
///   if (registry.Reserve("villager_01", ResourceType.Wood, 5f)) { ... }
///   bool success = registry.Commit("villager_01", ResourceType.Wood, 5f);
///
/// 의존성(Dependencies): AuthoritativeWorldState.cs, ResourceType.cs
///
/// 스레드 안전성: 메인 스레드에서만 호출한다.
///               GOAP Job은 WorldStateSnapshot을 통해 가용량을 추정하고,
///               실제 Reserve는 Job 완료 후 메인 스레드에서 수행한다.
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
    /// 자원 예약(Reserve) / 해제(Release) / 확정(Commit)을 관리하는 클래스.
    /// GameManager가 생성하고 참조를 보관한다.
    /// </summary>
    public class ResourceRegistry
    {
        // ── 내부 저장 구조 ─────────────────────────────────────────────────────

        /// <summary>
        /// 에이전트별 예약 추적 테이블.
        /// 키: villagerId / 값: (ResourceType → 예약량) 딕셔너리
        /// Release/Commit 시 이 테이블을 기준으로 실제 예약량을 확인한다.
        /// </summary>
        private readonly Dictionary<string, Dictionary<ResourceType, float>> _agentReservations
            = new Dictionary<string, Dictionary<ResourceType, float>>();

        /// <summary>
        /// 자원 종류별 총 예약량 캐시.
        /// GetAvailable() 에서 O(1) 조회를 위해 사용한다.
        /// agentReservations의 합계와 항상 일치해야 한다 (ValidateIntegrity로 검증).
        /// </summary>
        private readonly Dictionary<ResourceType, float> _totalReserved
            = new Dictionary<ResourceType, float>();

        /// <summary>
        /// 자원 재고 및 예약량의 단일 진실 공급원. 생성자에서 주입.
        /// </summary>
        private readonly AuthoritativeWorldState _worldState;

        // ── 생성자 ────────────────────────────────────────────────────────────

        /// <summary>
        /// ResourceRegistry를 생성한다.
        /// GameManager.Awake()에서 AuthoritativeWorldState 생성 직후 호출한다.
        /// </summary>
        /// <param name="worldState">자원 재고를 보관하는 월드 스테이트. null 불가.</param>
        public ResourceRegistry(AuthoritativeWorldState worldState)
        {
            if (worldState == null)
            {
                Debug.LogError("[ResourceRegistry] 생성자: worldState가 null입니다. ResourceRegistry가 정상 동작하지 않습니다.");
                return;
            }

            _worldState = worldState;

            // totalReserved 딕셔너리를 모든 ResourceType에 대해 0으로 초기화
            foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
            {
                _totalReserved[type] = 0f;
            }
        }

        // ── 공개 메서드 ───────────────────────────────────────────────────────

        /// <summary>
        /// 특정 자원의 현재 가용량을 반환한다.
        /// 가용량 = stock - 총 예약량
        /// GOAP 플래너가 Action Precondition 평가 시 이 값을 사용한다.
        /// </summary>
        /// <param name="type">조회할 자원 종류</param>
        /// <returns>현재 사용 가능한 수량 (0 이상 보장)</returns>
        public float GetAvailable(ResourceType type)
        {
            if (_worldState == null)
            {
                Debug.LogError("[ResourceRegistry] GetAvailable: worldState가 null입니다.");
                return 0f;
            }

            float stock    = _worldState.GetStock(type);
            float reserved = _totalReserved.TryGetValue(type, out float r) ? r : 0f;

            return Mathf.Max(0f, stock - reserved);
        }

        /// <summary>
        /// 자원 예약을 시도한다.
        /// 가용량이 충분하면 예약을 등록하고 true를 반환한다.
        /// 가용량이 부족하면 예약하지 않고 false를 반환한다 (예외 throw 없음).
        ///
        /// GOAP Action.CheckProceduralPrecondition() 또는 Action 시작 시점에 호출한다.
        /// </summary>
        /// <param name="agentId">예약하는 주민의 고유 ID</param>
        /// <param name="type">예약할 자원 종류</param>
        /// <param name="amount">예약할 수량 (양수여야 함)</param>
        /// <returns>예약 성공 여부</returns>
        public bool Reserve(string agentId, ResourceType type, float amount)
        {
            if (!ValidateParameters(agentId, amount, nameof(Reserve))) return false;

            // 가용량 확인 — 부족하면 예약 불가
            float available = GetAvailable(type);
            if (available < amount)
            {
                // 정보성 로그 (경고 아님): GOAP는 이 결과로 다른 Action을 탐색한다
                Debug.Log($"[ResourceRegistry] Reserve 실패: {agentId} 가 {type} {amount}를 예약하려 했지만 가용량이 {available:F1} 뿐입니다.");
                return false;
            }

            // 에이전트 예약 테이블에 추가
            EnsureAgentEntry(agentId);
            var agentDict = _agentReservations[agentId];

            if (!agentDict.ContainsKey(type))
                agentDict[type] = 0f;

            // [PR Fix]: 동일 agentId + 동일 type에 이미 예약이 존재하면 중복 예약 경고를 출력 (F-009)
            // GOAP Action 실수로 Reserve가 두 번 호출되면 과다 예약이 발생하므로 사전에 감지한다.
            else if (agentDict[type] > 0f)
            {
                Debug.LogWarning($"[ResourceRegistry] Reserve 중복 경고: agentId '{agentId}'가 {type}에 대해 이미 {agentDict[type]:F1}을 예약한 상태에서 추가로 {amount:F1}을 예약 시도합니다. GOAP Action 코드에서 Reserve 중복 호출이 없는지 확인하세요.");
            }

            agentDict[type]      += amount;
            _totalReserved[type] += amount;

            // AuthoritativeWorldState의 Reserved 필드도 동기화
            _worldState.SetReserved(type, _worldState.GetReserved(type) + amount);

            return true;
        }

        /// <summary>
        /// 기존 예약을 해제한다. Action이 취소 또는 중단될 때 호출한다.
        /// stock은 차감하지 않는다 (자원을 실제로 소비하지 않았으므로).
        /// </summary>
        /// <param name="agentId">예약을 해제할 주민의 고유 ID</param>
        /// <param name="type">해제할 자원 종류</param>
        /// <param name="amount">해제할 수량</param>
        public void Release(string agentId, ResourceType type, float amount)
        {
            if (!ValidateParameters(agentId, amount, nameof(Release))) return;

            if (!_agentReservations.TryGetValue(agentId, out var agentDict))
            {
                Debug.LogWarning($"[ResourceRegistry] Release: agentId '{agentId}'의 예약 기록이 없습니다. 중복 Release 또는 잘못된 ID일 수 있습니다.");
                return;
            }

            if (!agentDict.TryGetValue(type, out float currentReservation))
            {
                Debug.LogWarning($"[ResourceRegistry] Release: agentId '{agentId}'가 {type}에 대한 예약이 없습니다.");
                return;
            }

            // 해제량이 실제 예약량을 초과하는 경우 클램프 처리
            float actualRelease = Mathf.Min(amount, currentReservation);
            if (actualRelease < amount)
            {
                Debug.LogWarning($"[ResourceRegistry] Release: {agentId}의 {type} 예약량({currentReservation:F1})보다 많은 양({amount:F1})을 해제하려 합니다. {actualRelease:F1}만 해제합니다.");
            }

            agentDict[type]      = Mathf.Max(0f, currentReservation - actualRelease);
            _totalReserved[type] = Mathf.Max(0f, _totalReserved[type] - actualRelease);

            // 예약량이 0이 된 항목은 딕셔너리에서 제거 (메모리 절약)
            if (agentDict[type] <= 0f)
                agentDict.Remove(type);

            // AuthoritativeWorldState 동기화
            _worldState.SetReserved(type, Mathf.Max(0f, _worldState.GetReserved(type) - actualRelease));
        }

        /// <summary>
        /// 특정 에이전트의 모든 자원 예약을 해제한다.
        /// 주민 사망 시 즉시 호출하여 예약된 자원이 다른 주민에게 즉시 개방되도록 한다.
        /// Q04 사망 처리 규칙에 따라 메인 스레드에서 사망 이벤트 처리 중 호출한다.
        /// </summary>
        /// <param name="agentId">사망하거나 제거된 주민의 고유 ID</param>
        public void ReleaseAll(string agentId)
        {
            if (string.IsNullOrEmpty(agentId))
            {
                Debug.LogWarning("[ResourceRegistry] ReleaseAll: agentId가 null 또는 빈 문자열입니다.");
                return;
            }

            if (!_agentReservations.TryGetValue(agentId, out var agentDict))
            {
                // 예약이 없는 주민의 사망은 정상 케이스
                return;
            }

            // 해당 에이전트의 모든 예약을 totalReserved에서 차감
            foreach (var kvp in agentDict)
            {
                ResourceType type   = kvp.Key;
                float        amount = kvp.Value;

                _totalReserved[type] = Mathf.Max(0f, _totalReserved[type] - amount);
                _worldState.SetReserved(type, Mathf.Max(0f, _worldState.GetReserved(type) - amount));
            }

            _agentReservations.Remove(agentId);
        }

        // [PR Fix]: 반환형을 void에서 bool로 변경. 실패 시 false, 성공 시 true를 반환.
        //           호출자(GOAP Action)가 성공 여부를 알 수 없어 오류를 조용히 무시하는 문제를 해결한다. (F-005)
        /// <summary>
        /// 예약을 확정한다: AuthoritativeWorldState의 stock을 실제로 차감하고 예약을 해제한다.
        /// Action이 성공적으로 완료되어 자원이 실제로 소비될 때 호출한다.
        ///
        /// 순서:
        ///   1. Reserve() 로 등록된 예약 확인
        ///   2. stock -= amount (Mathf.Max(0f, ...) 클램프는 프로퍼티가 처리)
        ///   3. Release()로 예약 해제
        /// </summary>
        /// <param name="agentId">자원을 소비한 주민의 고유 ID</param>
        /// <param name="type">소비할 자원 종류</param>
        /// <param name="amount">소비할 수량</param>
        /// <returns>Commit 성공 여부. 예약이 없거나 파라미터가 유효하지 않으면 false.</returns>
        public bool Commit(string agentId, ResourceType type, float amount)
        {
            // [PR Fix]: ValidateParameters 실패 시 false 반환 (F-005)
            if (!ValidateParameters(agentId, amount, nameof(Commit))) return false;

            // 예약 확인 (예약 없이 Commit을 시도하는 것은 시스템 오류)
            if (!_agentReservations.TryGetValue(agentId, out var agentDict) ||
                !agentDict.TryGetValue(type, out float reservation))
            {
                Debug.LogError($"[ResourceRegistry] Commit: '{agentId}'가 {type}에 대한 예약 없이 Commit을 시도했습니다. Reserve() 없이 Commit을 호출한 Action 코드를 확인하세요.");
                // [PR Fix]: 예약 없음 → false 반환 (F-005)
                return false;
            }

            // 예약량보다 많이 Commit하는 경우 경고 (예약량만큼만 차감)
            if (amount > reservation + 0.01f)
            {
                Debug.LogWarning($"[ResourceRegistry] Commit: {agentId}의 {type} 예약량({reservation:F1})보다 많은 양({amount:F1})을 Commit하려 합니다. 예약량 기준으로 처리합니다.");
                amount = reservation;
            }

            // stock 실제 차감 (AuthoritativeWorldState 프로퍼티가 Mathf.Max(0f, ...) 처리)
            _worldState.SetStock(type, _worldState.GetStock(type) - amount);

            // 예약 해제
            Release(agentId, type, amount);

            // [PR Fix]: 정상 완료 시 true 반환 (F-005)
            return true;
        }

        /// <summary>
        /// 무결성 검증: agentReservations의 합계가 totalReserved 및 worldState.GetReserved()와
        /// 모두 일치하는지 확인한다. 불일치 발견 시 오류 로그와 함께 자동 복구한다.
        /// GameManager에서 주기적으로 호출한다.
        /// </summary>
        public void ValidateIntegrity()
        {
            // 각 ResourceType에 대해 agentReservations 합계를 직접 계산
            var calculatedTotals = new Dictionary<ResourceType, float>();
            foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
            {
                calculatedTotals[type] = 0f;
            }

            foreach (var agentEntry in _agentReservations)
            {
                foreach (var resourceEntry in agentEntry.Value)
                {
                    calculatedTotals[resourceEntry.Key] += resourceEntry.Value;
                }
            }

            bool hasError = false;
            foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
            {
                float cached     = _totalReserved.TryGetValue(type, out float c) ? c : 0f;
                float calculated = calculatedTotals[type];

                // 부동소수점 오차 허용 (0.01f 이상 차이면 오류로 판정)
                if (Mathf.Abs(cached - calculated) > 0.01f)
                {
                    Debug.LogError($"[ResourceRegistry] ValidateIntegrity 오류: {type} — totalReserved 캐시({cached:F3}) ≠ 실제 합계({calculated:F3}). 자동 복구합니다.");
                    _totalReserved[type] = calculated;
                    _worldState.SetReserved(type, calculated);
                    hasError = true;
                }

                // [PR Fix]: _worldState.GetReserved()와 calculated를 비교하는 검증 블록 추가 (F-002)
                // _totalReserved 캐시와 worldState 저장소가 불일치해도 '통과'가 출력되는 버그를 수정한다.
                float worldStateReserved = _worldState.GetReserved(type);
                if (Mathf.Abs(worldStateReserved - calculated) > 0.01f)
                {
                    Debug.LogError($"[ResourceRegistry] ValidateIntegrity 오류: {type} — worldState.GetReserved({worldStateReserved:F3}) ≠ 실제 합계({calculated:F3}). worldState를 자동 복구합니다.");
                    _worldState.SetReserved(type, calculated);
                    hasError = true;
                }
            }

            // [PR Fix]: 통과 로그를 #if UNITY_EDITOR 블록으로 감싸 주기적 호출 시 콘솔 오염 및 성능 비용을 방지 (F-007)
#if UNITY_EDITOR
            if (!hasError)
            {
                Debug.Log("[ResourceRegistry] ValidateIntegrity 통과 — 모든 예약량 정합성 확인됨.");
            }
#endif
        }

        // ── 비공개 헬퍼 메서드 ────────────────────────────────────────────────

        /// <summary>
        /// agentId가 agentReservations에 없으면 빈 딕셔너리로 초기화한다.
        /// </summary>
        private void EnsureAgentEntry(string agentId)
        {
            if (!_agentReservations.ContainsKey(agentId))
            {
                _agentReservations[agentId] = new Dictionary<ResourceType, float>();
            }
        }

        /// <summary>
        /// 공통 파라미터 검증. 유효하지 않으면 오류 로그 후 false를 반환한다.
        /// 예외 대신 false를 반환하는 이유: 게임 루프 내 호출이므로 예외가 프레임을 깨지 않도록.
        /// </summary>
        private bool ValidateParameters(string agentId, float amount, string callerName)
        {
            if (_worldState == null)
            {
                Debug.LogError($"[ResourceRegistry] {callerName}: worldState가 null입니다.");
                return false;
            }

            if (string.IsNullOrEmpty(agentId))
            {
                Debug.LogError($"[ResourceRegistry] {callerName}: agentId가 null 또는 빈 문자열입니다.");
                return false;
            }

            if (amount <= 0f)
            {
                Debug.LogError($"[ResourceRegistry] {callerName}: amount가 0 이하입니다 ({amount}). agentId='{agentId}'");
                return false;
            }

            return true;
        }
    }
}
