/// <summary>
/// MessageBus.cs - AI 유닛 간 이벤트 통신 Pub-Sub 브로커
///
/// 역할(Role): 모든 AI 유닛 사이의 메시지를 중개하는 싱글턴 브로커.
///             우선순위 큐(High→Medium→Low)를 통해 중요 메시지를 먼저 처리하고,
///             같은 Tick 내 중복 메시지를 병합하여 이벤트 폭주를 방지한다.
/// 사용법(Usage):
///   1. 씬의 GameManager GameObject(또는 별도 _Managers GameObject)에 이 컴포넌트를 추가한다.
///   2. GameManager가 매 Tick(0.1초)마다 MessageBus.Instance.ProcessTick()을 호출한다.
///   3. 구독: MessageBus.Instance.Subscribe(MessageType.VillagerDied, OnVillagerDied);
///   4. 발행: MessageBus.Instance.Publish(new AIMessage { ... });
///   5. 해제: OnDestroy()에서 반드시 Unsubscribe()를 호출한다.
/// 의존성(Dependencies):
///   - VillagerEnums.cs (AIMessage, MessageType, MessagePriority)
///   - ResourceType.cs (ItemType, ResourceType)
///   - VillagerEnums.cs AI 레이어 (OrderType, RefusalReasonCode)
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-26
/// </summary>

using System;
using System.Collections.Generic;
using UnityEngine;
using AIVillage.AI; // OrderType, RefusalReasonCode

namespace AIVillage.Core
{
    /// <summary>
    /// AI 유닛 간 이벤트 통신을 중개하는 Pub-Sub 싱글턴 브로커.
    /// Update()를 사용하지 않으며, GameManager가 Tick마다 ProcessTick()을 직접 호출한다.
    /// </summary>
    public sealed class MessageBus : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════════════
        // Payload 구조체 정의
        // 각 MessageType에 대응하는 전용 struct. object Payload에 박싱하여 전달한다.
        // ══════════════════════════════════════════════════════════════════════════

        #region ── Payload 구조체 ──

        /// <summary>
        /// VillagerDied 메시지 페이로드.
        /// 사망 주민 정보, 드롭 아이템, 근처 주민 ID를 담는다.
        /// freedReservations는 ResourceRegistry가 자체 처리하므로 포함하지 않는다.
        /// </summary>
        public struct VillagerDiedPayload
        {
            /// <summary>사망한 주민의 AgentId.</summary>
            public string VillagerId;

            /// <summary>사망 위치 타일 X 좌표.</summary>
            public int DeathTileX;

            /// <summary>사망 위치 타일 Y 좌표.</summary>
            public int DeathTileY;

            /// <summary>사망 시 드롭된 아이템 목록. 빈 배열이면 드롭 아이템 없음.</summary>
            public DroppedItemInfo[] DroppedItems;

            /// <summary>
            /// 사망 위치 반경 15타일 이내 주민 ID 목록.
            /// 구독자(GameManager)가 채워 넣어야 하므로 Publish 시점에는 빈 배열로 초기화한다.
            /// </summary>
            public string[] NearbyVillagerIds;
        }

        /// <summary>
        /// 사망 시 드롭된 개별 아이템 정보.
        /// VillagerDiedPayload.DroppedItems 배열의 원소 타입.
        /// </summary>
        public struct DroppedItemInfo
        {
            /// <summary>드롭된 아이템 종류 (도끼, 곡괭이, 무기, 식량 등).</summary>
            public ItemType ItemType;

            /// <summary>아이템이 드롭된 타일 X 좌표.</summary>
            public int TileX;

            /// <summary>아이템이 드롭된 타일 Y 좌표.</summary>
            public int TileY;
        }

        /// <summary>
        /// EnemyDetected 메시지 페이로드.
        /// 적 탐지 위치, 팩션, 위협 등급을 담는다.
        /// 같은 Tick 내 동일 EnemyFactionId 메시지는 병합(폐기)된다.
        /// </summary>
        public struct EnemyDetectedPayload
        {
            /// <summary>탐지된 적의 타일 X 좌표.</summary>
            public int EnemyTileX;

            /// <summary>탐지된 적의 타일 Y 좌표.</summary>
            public int EnemyTileY;

            /// <summary>탐지된 적의 팩션 ID. 중복 병합 키로 사용된다.</summary>
            public string EnemyFactionId;

            /// <summary>위협 등급 (1~4). 숫자가 높을수록 즉각 대피/전투가 필요하다.</summary>
            public int ThreatTier;

            /// <summary>최초 탐지한 주민의 AgentId.</summary>
            public string DetectedByVillagerId;

            /// <summary>탐지 시각 (Time.time 기준 게임 시간).</summary>
            public float DetectedAt;
        }

        /// <summary>
        /// ResourceDiscovered 메시지 페이로드.
        /// 탐험 주민이 새 자원 노드를 발견했을 때 발행된다.
        /// </summary>
        public struct ResourceDiscoveredPayload
        {
            /// <summary>이번 탐험에서 발견된 자원 노드 목록.</summary>
            public DiscoveredNodeInfo[] DiscoveredNodes;

            /// <summary>발견한 탐험가 주민의 AgentId.</summary>
            public string ExplorerVillagerId;
        }

        /// <summary>
        /// 새로 발견된 자원 노드 1개의 정보.
        /// ResourceDiscoveredPayload.DiscoveredNodes 배열의 원소 타입.
        /// </summary>
        public struct DiscoveredNodeInfo
        {
            /// <summary>자원 노드 고유 ID (TileMap 기반 문자열 또는 GUID).</summary>
            public string NodeId;

            /// <summary>이 노드에서 수집 가능한 자원 종류.</summary>
            public ResourceType ResourceType;

            /// <summary>노드 위치 타일 X 좌표.</summary>
            public int TileX;

            /// <summary>노드 위치 타일 Y 좌표.</summary>
            public int TileY;

            /// <summary>노드의 현재 잔여 자원량.</summary>
            public float CurrentAmount;
        }

        /// <summary>
        /// ResourceDepleted 메시지 페이로드.
        /// 자원 노드가 고갈되어 수집 불가 상태가 됐을 때 발행된다.
        /// 구독자(VillagerFSM)는 이 메시지를 수신하면 재플래닝을 트리거한다.
        /// </summary>
        public struct ResourceDepletedPayload
        {
            /// <summary>고갈된 자원 노드의 ID.</summary>
            public string NodeId;

            /// <summary>고갈된 자원의 종류.</summary>
            public ResourceType ResourceType;

            /// <summary>고갈 노드의 타일 X 좌표.</summary>
            public int TileX;

            /// <summary>고갈 노드의 타일 Y 좌표.</summary>
            public int TileY;

            /// <summary>
            /// 재생 속도 (단위/초). 0이면 재생 불가(영구 고갈).
            /// TODO: 기획팀 — 자원 타입별 재생 속도 확인 필요
            /// </summary>
            public float RegenerationRate;

            /// <summary>현재 재생 속도 기준 예상 회복 시간 (초). RegenerationRate == 0이면 float.MaxValue.</summary>
            public float EstimatedRecoveryTime;
        }

        /// <summary>
        /// RageKill 메시지 페이로드. [8단계]
        /// Rage 상태 주민이 적을 처치했을 때 발행. 6타일 이내 주민의 RageKillWitness를 증가시킨다.
        /// </summary>
        public struct RageKillPayload
        {
            /// <summary>처치를 수행한 주민의 AgentId.</summary>
            public string KillerVillagerId;

            /// <summary>처치 발생 타일 X 좌표.</summary>
            public int KillTileX;

            /// <summary>처치 발생 타일 Y 좌표.</summary>
            public int KillTileY;
        }

        /// <summary>
        /// OrderIssued 메시지 페이로드.
        /// 플레이어가 특정 주민에게 명령을 내렸을 때 발행된다.
        /// 수신자(VillagerFSM)는 TryExecuteOrder() 또는 CommandConflict 상태로 처리한다.
        /// </summary>
        public struct OrderIssuedPayload
        {
            /// <summary>명령 대상 주민의 AgentId.</summary>
            public string TargetVillagerId;

            /// <summary>명령 종류.</summary>
            public OrderType OrderType;

            /// <summary>명령 목표 타일 X 좌표.</summary>
            public int TargetTileX;

            /// <summary>명령 목표 타일 Y 좌표.</summary>
            public int TargetTileY;

            /// <summary>건설 명령일 때의 건물 타입 ID. BuildStructure 이외의 명령에서는 null.</summary>
            public string BuildingTypeId;

            /// <summary>명령 발행 시각 (Time.time).</summary>
            public float IssuedAt;
        }

        /// <summary>
        /// OrderRefused 메시지 페이로드.
        /// 주민이 플레이어 명령을 거부했을 때 주민이 스스로 발행한다.
        /// UI가 구독하여 거부 이유를 화면에 표시한다.
        /// </summary>
        public struct OrderRefusedPayload
        {
            /// <summary>거부를 결정한 주민의 AgentId.</summary>
            public string VillagerId;

            /// <summary>거부 이유 코드. UI 및 디버그 출력에 사용한다.</summary>
            public RefusalReasonCode RefusalReasonCode;

            /// <summary>사람이 읽을 수 있는 거부 이유 텍스트 (로컬라이징 전 영문 키 또는 한국어).</summary>
            public string RefusalMessage;

            /// <summary>계산된 ConflictScore 값. 디버그 및 UI 표시용.</summary>
            public float ConflictScore;

            /// <summary>이번 판정의 거부 임계값. ConflictScore >= Threshold이면 거부.</summary>
            public float Threshold;

            /// <summary>거부 후 주민이 대신 수행할 Goal ID. null이면 Idle로 복귀.</summary>
            public string AlternativeGoalId;

            /// <summary>거부 결정 시점의 충성도 값 (0~100).</summary>
            public float LoyaltyLevel;
        }

        /// <summary>
        /// SeasonChanged 메시지 페이로드. [14단계]
        /// 계절이 전환될 때 SeasonManager가 발행한다.
        /// </summary>
        public struct SeasonChangedPayload
        {
            /// <summary>이전 계절.</summary>
            public Season PreviousSeason;

            /// <summary>새로 전환된 계절.</summary>
            public Season NewSeason;

            /// <summary>전환 발생 시점의 게임 일수.</summary>
            public float GameDay;
        }

        /// <summary>
        /// WinterCrisis 메시지 페이로드. [14단계]
        /// 겨울 돌입 전 조리된 식량이 임계값 미달일 때 SeasonManager가 발행한다.
        /// </summary>
        public struct WinterCrisisPayload
        {
            /// <summary>현재 조리된 식량 재고.</summary>
            public float CurrentCookedFood;

            /// <summary>위험 판정 임계값 (villagerCount × _winterFoodCostPerDay × _winterCrisisBufferDays).</summary>
            public float Threshold;

            /// <summary>현재 생존 주민 수.</summary>
            public int VillagerCount;

            /// <summary>위기 발생 시점의 게임 일수.</summary>
            public float GameDay;
        }

        /// <summary>
        /// RaidDecision 메시지 페이로드.
        /// 팩션 AI가 다른 팩션을 침략하기로 결정했을 때 발행된다.
        /// </summary>
        public struct RaidDecisionPayload
        {
            /// <summary>침략을 결정한 팩션 ID.</summary>
            public string FactionId;

            /// <summary>침략 대상 팩션 ID.</summary>
            public string TargetFactionId;

            /// <summary>침략 시작 위치 타일 X 좌표.</summary>
            public int RaidStartTileX;

            /// <summary>침략 시작 위치 타일 Y 좌표.</summary>
            public int RaidStartTileY;

            /// <summary>침략에 참여하는 유닛들의 AgentId 목록.</summary>
            public string[] ParticipatingUnitIds;

            /// <summary>침략 부대의 추정 전투력. 방어 측이 대응 강도를 결정할 때 사용한다.</summary>
            public float EstimatedStrength;

            /// <summary>침략 결정의 원인 (예: "ResourcePressure", "CopperThreshold").</summary>
            public string RaidTriggerReason;

            /// <summary>침략 발동 예정 게임 일수.</summary>
            public int ActivationDay;
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════════
        // 우선순위 매핑 테이블
        // ══════════════════════════════════════════════════════════════════════════

        #region ── 우선순위 매핑 ──

        /// <summary>
        /// 각 MessageType에 대응하는 기본 우선순위 테이블.
        ///
        /// [PR Fix R-007]: 이 테이블은 단순 문서 역할에서 실제 강제 적용 테이블로 격상되었다.
        /// Publish() 진입 시 이 테이블을 사용해 message.Priority를 덮어씌운다.
        /// 발행자(VillagerFSM 등)가 Priority를 잘못 설정하는 실수를 시스템 레벨에서 방지한다.
        /// 새 MessageType 추가 시 이 테이블도 함께 갱신해야 Priority가 올바르게 적용된다.
        ///
        /// | MessageType        | Priority |
        /// |--------------------|----------|
        /// | VillagerDied       | High     |
        /// | EnemyDetected      | High     |
        /// | RaidDecision       | High     |
        /// | RageKill           | High     |
        /// | ResourceDiscovered | Medium   |
        /// | ResourceDepleted   | Medium   |
        /// | OrderIssued        | Medium   |
        /// | OrderRefused       | Low      |
        /// </summary>
        private static readonly Dictionary<MessageType, MessagePriority> DEFAULT_PRIORITY_MAP
            = new Dictionary<MessageType, MessagePriority>
            {
                { MessageType.VillagerDied,       MessagePriority.High   },
                { MessageType.EnemyDetected,      MessagePriority.High   },
                { MessageType.RaidDecision,       MessagePriority.High   },
                { MessageType.RageKill,           MessagePriority.High   }, // [8단계] Rage 전염 즉시 처리
                { MessageType.SeasonChanged,      MessagePriority.Medium }, // [14단계] 계절 전환
                { MessageType.WinterCrisis,       MessagePriority.High   }, // [14단계] 겨울 위기 즉시 처리
                { MessageType.ResourceDiscovered, MessagePriority.Medium },
                { MessageType.ResourceDepleted,   MessagePriority.Medium },
                { MessageType.OrderIssued,        MessagePriority.Medium },
                { MessageType.OrderRefused,       MessagePriority.Low    },
            };

        #endregion

        // ══════════════════════════════════════════════════════════════════════════
        // Singleton
        // ══════════════════════════════════════════════════════════════════════════

        #region ── Singleton ──

        /// <summary>
        /// MessageBus 싱글턴 인스턴스.
        /// GameManager 또는 VillagerFSM에서 MessageBus.Instance?.Publish(...)로 참조한다.
        /// null 조건 연산자(?.)를 사용하면 씬 전환 중 null 참조를 안전하게 처리한다.
        /// </summary>
        public static MessageBus Instance { get; private set; }

        #endregion

        // ══════════════════════════════════════════════════════════════════════════
        // Private Fields
        // ══════════════════════════════════════════════════════════════════════════

        #region ── Private Fields ──

        // ── 우선순위 큐 ──────────────────────────────────────────────────────────
        // key = (int)MessagePriority (0=High, 1=Medium, 2=Low), 작을수록 먼저 처리
        // 동일 키 내부는 List<AIMessage>를 FIFO로 유지 (Add() 순서 = IssuedAt 순서)
        // Unity 2021.3 기준 C# PriorityQueue<T, P>는 미지원이므로 SortedList 방식을 사용한다.
        private readonly SortedList<int, List<AIMessage>> _priorityQueue
            = new SortedList<int, List<AIMessage>>();

        // ── 메시지 중복 방지(Dedup) 집합 ─────────────────────────────────────────
        // 같은 Tick 내에 동일 타입 + 동일 키(factionId 등)의 메시지가 이미 큐에 있으면 폐기한다.
        // ProcessTick() 완료 시 초기화된다.
        // 형식: (MessageType, dedupKey)
        private readonly HashSet<(MessageType, string)> _pendingDedupSet
            = new HashSet<(MessageType, string)>();

        // ── 구독자 딕셔너리 ──────────────────────────────────────────────────────
        // key = MessageType, value = 이 타입을 구독 중인 콜백 목록
        private readonly Dictionary<MessageType, List<Action<AIMessage>>> _subscribers
            = new Dictionary<MessageType, List<Action<AIMessage>>>();

        // ── 구독자 콜백 스냅샷 (DispatchToSubscribers 전용 재사용 리스트) ───────────
        // [PR Fix R-001]: 역순 for 루프 대신 스냅샷 순회 방식으로 교체하기 위한 재사용 필드.
        // 매번 new List<>()를 생성하지 않고 이 필드를 Clear + AddRange하여 GC 할당을 줄인다.
        private readonly List<Action<AIMessage>> _dispatchSnapshot
            = new List<Action<AIMessage>>();

        // ── 통계 (Inspector 디버그용) ─────────────────────────────────────────────
        // SerializeField로 노출하여 Play Mode에서 Inspector를 통해 실시간 확인 가능

        [Tooltip("이번 Tick에 처리된 메시지 수 (디버그용 읽기 전용 필드).")]
        [SerializeField] private int _debugLastTickMessageCount = 0;

        [Tooltip("이번 Tick에 중복으로 폐기된 메시지 수 (디버그용 읽기 전용 필드).")]
        [SerializeField] private int _debugLastTickDedupCount = 0;

        [Tooltip("전체 Tick 누적 처리 메시지 수.")]
        [SerializeField] private int _debugTotalProcessed = 0;

        #endregion

        // ══════════════════════════════════════════════════════════════════════════
        // Unity 생명주기
        // ══════════════════════════════════════════════════════════════════════════

        #region ── Unity 생명주기 ──

        /// <summary>
        /// Unity가 GameObject 활성화 시 가장 먼저 호출한다.
        /// 싱글턴 설정 및 우선순위 큐 버킷을 초기화한다.
        /// </summary>
        private void Awake()
        {
            // ── Singleton 처리 ────────────────────────────────────────────────────
            // 이미 다른 인스턴스가 존재하면 이 GameObject를 즉시 제거한다.
            if (Instance != null && Instance != this)
            {
                UnityEngine.Debug.LogWarning(
                    "[MessageBus] 중복 인스턴스 감지. 이 GameObject를 Destroy합니다. " +
                    "씬에 MessageBus가 2개 이상 존재하지 않도록 설정을 확인하세요."
                );
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // DontDestroyOnLoad는 root GameObject에만 동작한다.
            // _Managers 등의 자식으로 배치된 경우 먼저 부모를 해제해야 경고가 사라진다.
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            // ── 우선순위 버킷 사전 생성 ───────────────────────────────────────────
            // 각 우선순위 레벨(0,1,2)에 빈 리스트를 미리 만들어 두어
            // Publish() 호출 시 null 체크 없이 Add()할 수 있다.
            InitializePriorityBuckets();

            UnityEngine.Debug.Log("[MessageBus] 초기화 완료. DontDestroyOnLoad 적용됨.");
        }

        /// <summary>
        /// Unity가 이 컴포넌트를 Destroy할 때 호출한다.
        /// 싱글턴 참조를 null로 초기화하여 이후 접근 시 NullReference를 방지한다.
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════════
        // Public API
        // ══════════════════════════════════════════════════════════════════════════

        #region ── Public API ──

        /// <summary>
        /// 메시지를 우선순위 큐에 추가한다.
        /// 같은 Tick 내 중복 메시지(EnemyDetected — 동일 팩션 등)는 자동으로 폐기된다.
        /// </summary>
        /// <param name="message">발행할 AIMessage. Priority와 Type이 반드시 설정되어야 한다.</param>
        public void Publish(AIMessage message)
        {
            // [PR Fix R-007]: DEFAULT_PRIORITY_MAP으로 메시지 Priority를 강제 설정한다.
            // 발행자(VillagerFSM 등)가 Priority를 잘못 설정하는 실수를 방지하기 위해
            // Publish() 진입 즉시 테이블 값으로 덮어씌운다.
            // 테이블에 없는 MessageType이면 발행자가 설정한 값을 그대로 유지한다.
            if (DEFAULT_PRIORITY_MAP.TryGetValue(message.Type, out MessagePriority mappedPriority))
            {
                message.Priority = mappedPriority;
            }

            // ── 중복 체크(Dedup) ──────────────────────────────────────────────────
            // EnemyDetected: 같은 Tick 내 동일 팩션 메시지 중복 폐기
            string dedupKey = GetDedupKey(message);
            if (dedupKey != null)
            {
                // (MessageType, dedupKey) 조합이 이미 이번 Tick에 등록되어 있으면 폐기
                var dedupEntry = (message.Type, dedupKey);
                if (_pendingDedupSet.Contains(dedupEntry))
                {
                    _debugLastTickDedupCount++;
                    return; // 폐기: 구독자에게 전달하지 않는다
                }

                // 처음 등록되는 메시지: Dedup 집합에 추가
                _pendingDedupSet.Add(dedupEntry);
            }

            // ── 우선순위 버킷에 추가 ──────────────────────────────────────────────
            int priorityKey = (int)message.Priority;

            // SortedList는 Awake에서 버킷을 모두 사전 생성했으므로 키 존재 여부를 체크하지 않는다.
            if (!_priorityQueue.ContainsKey(priorityKey))
            {
                // [PR Fix R-005]: LogWarning → LogError로 격상.
                // Awake 이전 Publish는 초기화 순서 위반으로 설계 오류 수준이다.
                // 경고(Warning)로 처리하면 개발 중 놓칠 수 있으므로 LogError로 명시한다.
                _priorityQueue[priorityKey] = new List<AIMessage>();
                UnityEngine.Debug.LogError(
                    $"[MessageBus] Publish: 우선순위 버킷 {priorityKey}이 없었습니다. 동적으로 생성했습니다. " +
                    "Awake 전에 Publish가 호출된 것은 초기화 순서 위반입니다. Script Execution Order를 확인하세요."
                );
            }

            _priorityQueue[priorityKey].Add(message);
        }

        /// <summary>
        /// 특정 메시지 타입의 구독을 등록한다.
        /// 구독자는 ProcessTick() 내에서 콜백으로 호출된다.
        /// </summary>
        /// <param name="type">구독할 메시지 종류.</param>
        /// <param name="callback">메시지 수신 시 실행할 콜백 함수.</param>
        public void Subscribe(MessageType type, Action<AIMessage> callback)
        {
            if (callback == null)
            {
                UnityEngine.Debug.LogWarning($"[MessageBus] Subscribe: null 콜백은 등록할 수 없습니다. Type={type}");
                return;
            }

            if (!_subscribers.ContainsKey(type))
            {
                _subscribers[type] = new List<Action<AIMessage>>();
            }

            // 동일 콜백 중복 등록 방지
            if (!_subscribers[type].Contains(callback))
            {
                _subscribers[type].Add(callback);
            }
            else
            {
                UnityEngine.Debug.LogWarning(
                    $"[MessageBus] Subscribe: 동일 콜백이 이미 등록되어 있습니다. 무시합니다. Type={type}"
                );
            }
        }

        /// <summary>
        /// 특정 메시지 타입의 구독을 해제한다.
        /// OnDestroy() 또는 에이전트 사망 시 반드시 호출하여 고아 콜백을 방지한다.
        /// </summary>
        /// <param name="type">구독 해제할 메시지 종류.</param>
        /// <param name="callback">제거할 콜백 함수 (Subscribe 시 등록한 것과 동일한 참조여야 함).</param>
        public void Unsubscribe(MessageType type, Action<AIMessage> callback)
        {
            if (callback == null)
            {
                UnityEngine.Debug.LogWarning($"[MessageBus] Unsubscribe: null 콜백은 해제할 수 없습니다. Type={type}");
                return;
            }

            if (!_subscribers.TryGetValue(type, out List<Action<AIMessage>> callbackList))
            {
                // 구독된 적 없는 타입 — 경고 없이 무시 (방어적 호출 허용)
                return;
            }

            callbackList.Remove(callback);
        }

        /// <summary>
        /// 큐에 쌓인 모든 메시지를 우선순위 순으로 처리하고 구독자에게 전달한다.
        /// GameManager가 매 Tick(0.1초)마다 호출해야 한다.
        /// 처리 완료 후 Dedup 집합을 초기화하여 다음 Tick에 동일 메시지가 다시 허용된다.
        /// </summary>
        public void ProcessTick()
        {
            // [PR Fix R-002]: _debugLastTickDedupCount 초기화 타이밍 버그 수정.
            // 이전 코드는 ProcessTick() 시작 시 _debugLastTickDedupCount도 0으로 초기화했는데,
            // Publish()는 ProcessTick() 호출 이전에도 언제든지 실행될 수 있다.
            // 따라서 ProcessTick() 시작 시 초기화하면 이미 이번 Tick에 누적된 Dedup 카운트가
            // 사라져 디버그 통계가 오염된다.
            //
            // 수정: ProcessTick() 시작에서는 메시지 카운트만 초기화하고,
            //       _debugLastTickDedupCount는 ProcessTick() 끝(_pendingDedupSet.Clear()와 함께) 초기화한다.

            // 이번 Tick 메시지 처리 통계만 초기화 (Dedup 카운트는 Tick 끝에서 초기화)
            _debugLastTickMessageCount = 0;

            // ── 우선순위 순으로 버킷을 처리 ──────────────────────────────────────
            // SortedList는 key(0,1,2) 오름차순으로 이미 정렬되어 있다.
            // 따라서 순서대로 순회하면 High(0) → Medium(1) → Low(2) 순서가 보장된다.
            foreach (KeyValuePair<int, List<AIMessage>> bucket in _priorityQueue)
            {
                List<AIMessage> messages = bucket.Value;

                // 이 버킷이 비어 있으면 건너뜀 (대부분의 Tick에서 발생하는 빠른 경로)
                if (messages.Count == 0) continue;

                // ── 각 메시지를 구독자에게 전달 ──────────────────────────────────
                for (int i = 0; i < messages.Count; i++)
                {
                    AIMessage message = messages[i];
                    DispatchToSubscribers(message);
                    _debugLastTickMessageCount++;
                    _debugTotalProcessed++;
                }

                // 이 버킷의 모든 메시지를 처리했으므로 비운다
                messages.Clear();
            }

            // ── Dedup 집합 초기화 및 Dedup 통계 리셋 ────────────────────────────────
            // 다음 Tick에는 동일 메시지가 다시 허용되어야 한다.
            _pendingDedupSet.Clear();

            // [PR Fix R-002]: _debugLastTickDedupCount 초기화를 Tick 끝으로 이동.
            // 이 시점이 "이번 Tick의 Dedup 통계 확정 후 다음 Tick 카운터 초기화" 타이밍으로 올바르다.
            // Inspector에서 Tick 종료 직전까지 이번 Tick의 폐기 수를 읽을 수 있다.
            _debugLastTickDedupCount = 0;
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════════
        // Private 메서드
        // ══════════════════════════════════════════════════════════════════════════

        #region ── Private 메서드 ──

        /// <summary>
        /// 우선순위 버킷 (SortedList의 각 List)을 Awake에서 미리 생성한다.
        /// Publish() 호출 시 null 여부를 매번 체크하지 않아도 된다.
        /// </summary>
        private void InitializePriorityBuckets()
        {
            // [PR Fix R-004]: Enum.GetValues()는 호출마다 새 배열을 힙에 할당한다.
            // Awake에서만 호출되므로 성능 영향은 미미하지만, 명시적 하드코딩이 더 안전하고
            // MessagePriority 열거형 정의와 버킷 생성 사이의 암묵적 결합을 명확히 드러낸다.
            // MessagePriority: High = 0, Medium = 1, Low = 2 (값이 변경되면 이 줄도 함께 수정할 것)
            _priorityQueue.Add((int)MessagePriority.High,   new List<AIMessage>());
            _priorityQueue.Add((int)MessagePriority.Medium, new List<AIMessage>());
            _priorityQueue.Add((int)MessagePriority.Low,    new List<AIMessage>());
        }

        /// <summary>
        /// 메시지를 해당 타입의 구독자 콜백 목록에 전달한다.
        /// 콜백 실행 중 예외가 발생해도 다른 구독자에게는 계속 전달한다.
        /// </summary>
        /// <param name="message">전달할 메시지.</param>
        private void DispatchToSubscribers(AIMessage message)
        {
            if (!_subscribers.TryGetValue(message.Type, out List<Action<AIMessage>> callbackList))
            {
                // 이 타입을 구독 중인 시스템이 없음 — 정상 케이스이므로 경고 없이 통과
                return;
            }

            if (callbackList.Count == 0) return;

            // [PR Fix R-001]: 역순 for 루프의 논리적 허점 수정 — 스냅샷 순회 방식으로 교체.
            //
            // [문제] 역순(i = Count-1 → 0) 순회는 "뒤에서부터 제거"할 때만 안전하다.
            // 그러나 Unsubscribe()는 List.Remove()를 사용하므로 임의의 인덱스 항목을 제거한다.
            // 예: [A, B, C]에서 i=2(C) 실행 중 C가 A를 Unsubscribe하면 → [B, C]가 되어
            //     다음 i=1에서 C를 재실행하는 버그가 발생한다.
            //
            // [수정] 처리 전 callbackList를 _dispatchSnapshot(재사용 클래스 필드)에 복사하고,
            // 각 콜백 실행 전 callbackList.Contains(callback)으로 이미 Unsubscribe된 콜백을 건너뛴다.
            // _dispatchSnapshot은 매번 new하지 않고 Clear + AddRange로 재사용하여 GC 할당을 최소화한다.

            // 스냅샷: 현재 콜백 목록을 재사용 리스트에 복사 (GC 할당 최소화)
            _dispatchSnapshot.Clear();
            _dispatchSnapshot.AddRange(callbackList);

            // 스냅샷을 순서대로 순회 (FIFO 순서 보장)
            for (int i = 0; i < _dispatchSnapshot.Count; i++)
            {
                Action<AIMessage> callback = _dispatchSnapshot[i];
                if (callback == null) continue;

                // 스냅샷 생성 이후 Unsubscribe()로 이미 해제된 콜백인지 확인 후 건너뜀
                // callbackList는 원본 구독자 목록이므로, 여기서 포함 여부를 확인하면
                // 콜백 실행 중 발생한 Unsubscribe를 정확히 반영할 수 있다.
                if (!callbackList.Contains(callback)) continue;

                try
                {
                    callback.Invoke(message);
                }
                catch (Exception ex)
                {
                    // 한 구독자의 예외가 다른 구독자 전달을 막지 않도록 catch
                    UnityEngine.Debug.LogError(
                        $"[MessageBus] 구독자 콜백 실행 중 예외 발생. " +
                        $"MessageType={message.Type}, SenderId={message.SenderId}\n" +
                        $"예외: {ex.Message}\n{ex.StackTrace}"
                    );
                }
            }
        }

        /// <summary>
        /// 메시지의 중복 방지 키를 반환한다.
        /// null을 반환하면 이 메시지는 중복 체크를 수행하지 않는다 (모든 중복 허용).
        ///
        /// 현재 중복 방지 적용 메시지:
        ///   - EnemyDetected: EnemyFactionId를 키로 사용
        ///     → 같은 Tick에 동일 팩션의 적이 여러 주민에게 탐지되는 폭주를 방지
        /// 나머지 타입은 중복 허용 (null 반환).
        /// </summary>
        /// <param name="message">중복 키를 추출할 메시지.</param>
        /// <returns>중복 체크 키 문자열, 또는 체크 불필요 시 null.</returns>
        private string GetDedupKey(AIMessage message)
        {
            switch (message.Type)
            {
                case MessageType.EnemyDetected:
                    // EnemyDetected는 EnemyFactionId를 키로 사용한다.
                    // Payload 언박싱 실패(Payload가 다른 타입인 경우)이면 중복 체크 없이 허용한다.
                    if (message.Payload is EnemyDetectedPayload enemyPayload)
                    {
                        // EnemyFactionId가 비어 있으면 중복 체크 불가 → 허용
                        return string.IsNullOrEmpty(enemyPayload.EnemyFactionId)
                            ? null
                            : enemyPayload.EnemyFactionId;
                    }
                    return null;

                // 나머지 메시지 타입: 중복 체크 없이 모두 허용
                default:
                    return null;
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════════
        // Unity Editor 디버그 헬퍼
        // ══════════════════════════════════════════════════════════════════════════

        #region ── Editor 디버그 헬퍼 ──

#if UNITY_EDITOR
        /// <summary>
        /// Inspector 우클릭 → "DEBUG: 큐 상태 출력" 으로 현재 큐 상태를 Console에 출력한다.
        /// 성능 영향 없이 디버깅 가능하도록 UNITY_EDITOR 블록으로 제한한다.
        /// </summary>
        [ContextMenu("DEBUG: 큐 상태 출력 (Print Queue Status)")]
        private void DebugPrintQueueStatus()
        {
            int total = 0;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("[MessageBus] 현재 큐 상태 ──────────────────────────────");

            foreach (KeyValuePair<int, List<AIMessage>> bucket in _priorityQueue)
            {
                string priorityName = ((MessagePriority)bucket.Key).ToString();
                sb.AppendLine($"  [{priorityName}] 대기 {bucket.Value.Count}개");
                foreach (AIMessage msg in bucket.Value)
                {
                    sb.AppendLine(
                        $"    - Type={msg.Type}, SenderId={msg.SenderId}, IssuedAt={msg.IssuedAt:F2}"
                    );
                }
                total += bucket.Value.Count;
            }

            sb.AppendLine($"  [구독 타입 수] {_subscribers.Count}개");
            sb.AppendLine($"  [이번 Tick 처리] {_debugLastTickMessageCount}개");
            sb.AppendLine($"  [이번 Tick 폐기] {_debugLastTickDedupCount}개");
            sb.AppendLine($"  [전체 처리 누계] {_debugTotalProcessed}개");
            sb.AppendLine($"  [현재 대기 합계] {total}개");
            sb.AppendLine("──────────────────────────────────────────────────────────");

            UnityEngine.Debug.Log(sb.ToString());
        }
#endif

        #endregion
    }
}
