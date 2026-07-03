/// <summary>
/// SeasonManager.cs - 게임 내 계절 전환 및 계절 효과 적용 관리자
///
/// 역할(Role): 게임 일수(GameTime)에 따라 Spring → Summer → Autumn → Winter를
///             순환하며, 계절 전환 시 자원 노드 재생률 배율을 적용하고,
///             겨울 식량 소모 및 WinterCrisis 경보를 MessageBus를 통해 발행한다.
///
/// 사용법(Usage):
///   1. 씬의 _Managers 하위에 빈 GameObject "_SeasonManager"를 생성한다.
///   2. 이 스크립트를 부착한다.
///   3. GameManager의 GameTickCoroutine에서 SeasonManager.Instance?.OnTick(deltaGameDays)를
///      TickResourceRegeneration 직전에 호출한다.
///   4. Inspector 슬롯에 별도 설정 없음 — 모든 의존성은 싱글턴으로 참조한다.
///
/// 의존성(Dependencies):
///   - AuthoritativeWorldState.cs (자원 재고)
///   - SensorSystem.cs (자원 노드 재생률 조정)
///   - GameManager.cs (생존 주민 수 조회)
///   - MessageBus.cs (SeasonChanged, WinterCrisis 발행)
///   - ResourceType.cs (Season 열거형)
///   - VillagerEnums.cs (MessageType)
///
/// 실행 순서(ExecutionOrder): -75
///   MessageBus(-100) → BuildingQueue(-90) → GameManager(-80) → SeasonManager(-75) → VillagerFSM(0)
///   GameTickCoroutine 안에서 OnTick()으로 매 틱 호출받으므로 Update()는 사용하지 않는다.
///
/// RISK 대응 요약:
///   RISK-2: OnSeasonChanged()에서 배율을 1회만 적용 (매 틱 재적용 금지)
///   RISK-3: _lastSeason을 Awake에서 Spring으로 초기화 → 게임 시작 즉시 오발행 방지
///   RISK-4: Winter 아닌 계절 진입 시 _winterCrisisPublished = false 리셋
///   RISK-5: GameManager null 체크 + count == 0 early return
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-30
/// </summary>

using System.Collections.Generic;
using UnityEngine;
using AIVillage.AI;

namespace AIVillage.Core
{
    /// <summary>
    /// 계절 전환을 관리하고 계절별 효과(재생률, 식량 소모, WinterCrisis 경보)를 적용한다.
    /// GameManager의 틱 루프에서 OnTick()으로 호출받는다. Update()를 사용하지 않는다.
    /// </summary>
    [DefaultExecutionOrder(-75)]
    [DisallowMultipleComponent]
    public sealed class SeasonManager : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════════
        // 싱글턴
        // ══════════════════════════════════════════════════════════════════════

        #region ── 싱글턴 ──

        public static SeasonManager Instance { get; private set; }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Serialized Fields (Inspector 노출)
        // ══════════════════════════════════════════════════════════════════════

        #region ── Serialized Fields ──

        [Tooltip("1계절의 길이(게임 일수). 기획서 수치: 10일.")]
        [SerializeField, Min(1)] private int _seasonLengthDays = 10;

        [Tooltip("겨울 중 주민 1명이 하루 소모하는 조리된 식량 양. 기획서 수치: 0.5f.")]
        [SerializeField] private float _winterFoodCostPerDay = 0.5f;

        [Tooltip("겨울 계절의 자원 노드 재생률 배율. 기획서 수치: 0.5f.")]
        [SerializeField] private float _winterRegenMultiplier = 0.5f;

        [Tooltip("여름 계절 RawFood 노드의 재생률 배율. 기획서 수치: 1.5f.")]
        [SerializeField] private float _summerRegenMultiplier = 1.5f;

        [Tooltip("가을 계절의 채집 비용 배율. 기획서 수치: 0.333f (1/3). GOAP 플래너에 주입된다.")]
        [SerializeField] private float _autumnGatherModifier = 0.333f;

        [Tooltip("WinterCrisis 경보 임계 일수. 주민 수 × _winterFoodCostPerDay × 이 값 미만이면 경보 발행. " +
                 "기본값 30은 주민 5명/CookedFood 0 시작 기준 겨울 진입 즉시 발행됨 — 기획팀 수치 확인 필요.")]
        [SerializeField] private float _winterCrisisBufferDays = 30f;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private Fields
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private Fields ──

        private Season _currentSeason = Season.Spring;

        // RISK-3: Awake에서 Spring으로 초기화 → 첫 OnTick()에서 "Spring→Spring" 오발행 방지
        private Season _lastSeason = Season.Spring;

        // RISK-4: Winter 아닌 계절로 진입 시 false 리셋 → 다음 겨울 경보 허용
        private bool _winterCrisisPublished = false;

        private float _accumulatedTime = 0f;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // 공개 프로퍼티
        // ══════════════════════════════════════════════════════════════════════

        #region ── 공개 프로퍼티 ──

        public Season CurrentSeason => _currentSeason;

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Unity 생명주기
        // ══════════════════════════════════════════════════════════════════════

        #region ── Unity 생명주기 ──

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[SeasonManager] 중복 인스턴스 감지. 이 GameObject를 Destroy합니다.");
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // RISK-3: 명시적 초기화
            _currentSeason = Season.Spring;
            _lastSeason    = Season.Spring;

            Debug.Log("[SeasonManager] Awake 완료. 초기 계절=Spring.");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // 공개 API
        // ══════════════════════════════════════════════════════════════════════

        #region ── 공개 API ──

        /// <summary>
        /// GameManager의 GameTickCoroutine에서 매 틱 호출된다.
        /// TickResourceRegeneration() 직전에 호출하여 배율 변경 → 재생 적용 순서를 보장한다.
        /// </summary>
        public void OnTick(float deltaGameDays)
        {
            _accumulatedTime += deltaGameDays;

            // 계절 공식: floor(gameTime / seasonLength) % 4
            _currentSeason = (Season)(Mathf.FloorToInt(_accumulatedTime / _seasonLengthDays) % 4);

            if (_currentSeason != _lastSeason)
            {
                Season prev = _lastSeason;
                _lastSeason = _currentSeason;
                OnSeasonChanged(prev, _currentSeason);
            }

            if (_currentSeason == Season.Winter)
            {
                TickWinterEffects(deltaGameDays);
            }
        }

        /// <summary>
        /// 현재 계절의 채집 비용 배율. GOAPPlannerScheduler에서 호출한다.
        /// Autumn=0.333f(1/3, 선호도 3배), 나머지=1.0f.
        /// </summary>
        public float GetCurrentGatherCostModifier()
        {
            return _currentSeason == Season.Autumn ? _autumnGatherModifier : 1.0f;
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Private 메서드
        // ══════════════════════════════════════════════════════════════════════

        #region ── Private 메서드 ──

        /// <summary>
        /// 계절 전환 시 1회만 호출된다.
        /// 자원 노드 재생률 배율 적용(RISK-2), 겨울 위기 플래그 리셋(RISK-4), SeasonChanged 발행.
        /// </summary>
        private void OnSeasonChanged(Season prev, Season next)
        {
            Debug.Log($"[SeasonManager] 계절 전환: {prev} → {next}. GameDay={_accumulatedTime:F2}.");

            // RISK-2: 이 메서드는 전환 시 1회만 호출 → 배율 누적 없음
            ApplySeasonRegenMultiplier(next);

            // RISK-4: Winter 아닌 계절 진입 시 위기 플래그 리셋
            if (next != Season.Winter)
            {
                _winterCrisisPublished = false;
            }

            MessageBus.Instance?.Publish(new AIMessage
            {
                Type     = MessageType.SeasonChanged,
                SenderId = "SeasonManager",
                IssuedAt = _accumulatedTime,
                Priority = MessagePriority.Medium,
                Payload  = new MessageBus.SeasonChangedPayload
                {
                    PreviousSeason = prev,
                    NewSeason      = next,
                    GameDay        = _accumulatedTime
                }
            });
        }

        /// <summary>
        /// 새 계절에 맞는 재생률 배율을 모든 ResourceNode에 적용한다.
        /// 계절 전환 시 1회만 실행 (RISK-2).
        ///
        /// Spring:  모든 노드 → BaseRegenerationRate
        /// Summer:  RawFood 노드만 × _summerRegenMultiplier, 나머지 → BaseRegenerationRate
        /// Autumn:  모든 노드 → BaseRegenerationRate (Summer 배율 제거, 채집 비용은 GOAP에서 처리)
        /// Winter:  모든 노드 → BaseRegenerationRate × _winterRegenMultiplier
        /// </summary>
        private void ApplySeasonRegenMultiplier(Season newSeason)
        {
            IReadOnlyList<ResourceNode> nodes = SensorSystem.Instance?.GetAllNodes();
            if (nodes == null)
            {
                Debug.LogWarning("[SeasonManager] ApplySeasonRegenMultiplier: SensorSystem 또는 노드가 null.");
                return;
            }

            int count = nodes.Count;
            for (int i = 0; i < count; i++)
            {
                ResourceNode node = nodes[i];
                if (node == null) continue;

                switch (newSeason)
                {
                    case Season.Spring:
                    case Season.Autumn:
                        node.RegenerationRate = node.BaseRegenerationRate;
                        break;

                    case Season.Summer:
                        node.RegenerationRate = (node.ResourceType == ResourceType.RawFood)
                            ? node.BaseRegenerationRate * _summerRegenMultiplier
                            : node.BaseRegenerationRate;
                        break;

                    case Season.Winter:
                        node.RegenerationRate = node.BaseRegenerationRate * _winterRegenMultiplier;
                        break;
                }
            }

            Debug.Log($"[SeasonManager] 재생률 배율 적용 완료. Season={newSeason}, 노드={count}개.");
        }

        /// <summary>
        /// Winter 계절 중 매 틱 호출. 식량 소모 및 WinterCrisis 경보를 처리한다.
        /// </summary>
        private void TickWinterEffects(float deltaGameDays)
        {
            AuthoritativeWorldState worldState = AuthoritativeWorldState.Instance;
            if (worldState == null) return;

            // RISK-5: null 안전 조회. Villagers는 GameManager의 IReadOnlyList<VillagerFSM> 프로퍼티.
            int aliveCount = GameManager.Instance?.Villagers?.Count ?? 0;
            if (aliveCount == 0) return;

            // 겨울 식량 소모 (CookedFoodStock setter에 0 클램프 있음)
            worldState.CookedFoodStock -= _winterFoodCostPerDay * aliveCount * deltaGameDays;

            // WinterCrisis 중복 방지
            if (_winterCrisisPublished) return;

            float threshold = aliveCount * _winterFoodCostPerDay * _winterCrisisBufferDays;
            float food      = worldState.CookedFoodStock;

            if (food < threshold)
            {
                _winterCrisisPublished = true;

                MessageBus.Instance?.Publish(new AIMessage
                {
                    Type     = MessageType.WinterCrisis,
                    SenderId = "SeasonManager",
                    IssuedAt = _accumulatedTime,
                    Priority = MessagePriority.High,
                    Payload  = new MessageBus.WinterCrisisPayload
                    {
                        CurrentCookedFood = food,
                        Threshold         = threshold,
                        VillagerCount     = aliveCount,
                        GameDay           = _accumulatedTime
                    }
                });

                Debug.LogWarning($"[SeasonManager] WinterCrisis 발행! Food={food:F1}, Threshold={threshold:F1}, " +
                                 $"주민={aliveCount}명.");
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // Unity Editor 디버그 헬퍼
        // ══════════════════════════════════════════════════════════════════════

        #region ── Editor 디버그 헬퍼 ──

#if UNITY_EDITOR
        [ContextMenu("DEBUG: 계절 상태 출력 (Print Season Status)")]
        private void DebugPrintSeasonStatus()
        {
            int villagers   = GameManager.Instance?.Villagers?.Count ?? -1;
            float threshold = villagers > 0
                ? villagers * _winterFoodCostPerDay * _winterCrisisBufferDays : 0f;
            float food = AuthoritativeWorldState.Instance?.CookedFoodStock ?? -1f;

            Debug.Log($"[SeasonManager] 계절={_currentSeason}, GameDay={_accumulatedTime:F2}, " +
                      $"WinterCrisis={_winterCrisisPublished}, Food={food:F1}/{threshold:F1}, " +
                      $"GatherMod={GetCurrentGatherCostModifier():F3}");
        }

        [ContextMenu("DEBUG: 강제 다음 계절 (Force Next Season)")]
        private void DebugForceNextSeason()
        {
            // 주의: _accumulatedTime만 직접 조작하므로 GameManager.GameTime과 동기화가 깨질 수 있음. 디버그 전용.
            int nextIndex  = ((int)_currentSeason + 1) % 4;
            int curCycle   = Mathf.FloorToInt(_accumulatedTime / (_seasonLengthDays * 4));
            int curIndex   = (int)_currentSeason;

            float nextTime = (nextIndex > curIndex)
                ? curCycle * _seasonLengthDays * 4f + nextIndex * _seasonLengthDays
                : (curCycle + 1) * _seasonLengthDays * 4f;

            _accumulatedTime = nextTime + 0.001f;
            OnTick(0f);
            Debug.Log($"[SeasonManager] 강제 계절 전환 → {_currentSeason}");
        }

        [ContextMenu("DEBUG: 강제 WinterCrisis 발행 (Force Winter Crisis)")]
        private void DebugForceWinterCrisis()
        {
            _winterCrisisPublished = false;
            Season saved = _currentSeason;
            _currentSeason = Season.Winter;
            if (AuthoritativeWorldState.Instance != null)
                AuthoritativeWorldState.Instance.CookedFoodStock = 0f;
            TickWinterEffects(0f);
            _currentSeason = saved;
        }
#endif

        #endregion
    }
}
