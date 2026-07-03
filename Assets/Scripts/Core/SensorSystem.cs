/// <summary>
/// SensorSystem.cs - 주민 AI 환경 감지 시스템
///
/// 역할(Role): 매 Tick마다 모든 등록된 VillagerFSM의 VillagerBrain 환경 플래그를 갱신한다.
///             타일 맨해튼 거리를 기준으로 자원 노드, 적, 드롭 아이템, 건물 근접 여부를 판정한다.
///             FoW(안개 전쟁)와 연동하여 미발견 자원은 NearDiscoveredResource를 false로 유지한다.
///
/// 사용법(Usage):
///   1. GameManager GameObject에 이 컴포넌트를 추가한다.
///   2. Inspector에서 각 건물 Transform을 할당한다 (_baseTransform, _storageTransforms 등).
///   3. GameManager.Start()에서 RegisterVillager()로 주민을 등록한다.
///   4. GameManager의 0.1초 Tick 코루틴에서 UpdateAllSensors()를 호출한다.
///   5. 자원 노드는 맵 로드 시 AddResourceNode()로 등록한다.
///
/// 의존성(Dependencies):
///   - VillagerFSM.cs, VillagerBrain.cs (AIVillage.AI)
///   - AuthoritativeWorldState.cs, ResourceNode.cs, ResourceType.cs (AIVillage.Core)
///
/// 성능 예산:
///   UpdateAllSensors()는 GC 할당 없이 동작해야 한다.
///   - List는 모두 필드 캐시 사용
///   - LINQ 금지
///   - 인덱스 기반 for 루프 사용 (DroppedItem struct List에서 foreach 박싱 방지)
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-26
/// </summary>

using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.Core
{
    // ────────────────────────────────────────────────────────────────────────────
    // IEnemyAgent 인터페이스
    // SensorSystem.cs 파일 내에 정의한다.
    // 적 유닛(Enemy MonoBehaviour)이 이 인터페이스를 구현하여 SensorSystem에 등록한다.
    // SensorSystem은 구체 타입을 알 필요 없이 타일 위치와 생존 여부만 읽는다.
    // ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// SensorSystem이 적 유닛을 추적하기 위한 최소 인터페이스.
    /// Enemy MonoBehaviour 클래스에서 구현하여 RegisterEnemy()로 등록한다.
    /// </summary>
    public interface IEnemyAgent
    {
        /// <summary>적 유닛의 현재 타일 X 좌표. 매 프레임 갱신 필요.</summary>
        int TileX { get; }

        /// <summary>적 유닛의 현재 타일 Y 좌표. 매 프레임 갱신 필요.</summary>
        int TileY { get; }

        /// <summary>생존 여부. false이면 SensorSystem이 NearEnemy 판정에서 제외한다.</summary>
        bool IsAlive { get; }
    }

    /// <summary>
    /// 주민 AI의 환경 감지를 담당하는 MonoBehaviour 시스템.
    /// GameManager가 0.1초 간격으로 UpdateAllSensors()를 호출하면
    /// 모든 등록된 주민의 VillagerBrain 환경 플래그를 갱신한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SensorSystem : MonoBehaviour
    {
        #region ── 상수 ──

        // 게임 로그 접두사 — 모든 Debug 메시지에 붙여 Console 검색을 쉽게 한다
        private const string LOG_PREFIX = "[SensorSystem]";

        #endregion

        #region ── 싱글턴 ──

        // [PR Fix]: Critical-2 — SensorSystem.Instance 정적 프로퍼티 추가.
        // DiscoverArea() 주석 등의 사용 예시처럼 외부에서 SensorSystem.Instance로 접근 가능하게 한다.
        /// <summary>
        /// SensorSystem의 씬 내 유일한 인스턴스.
        /// VillagerFSM.ApplyActionEffect() 등 외부에서 DiscoverArea() 호출 시 사용한다.
        /// Awake에서 설정되고 OnDestroy에서 null로 해제된다.
        /// </summary>
        public static SensorSystem Instance { get; private set; }

        #endregion

        #region ── Serialized Fields (Inspector) ──

        // ── 감지 반경 설정 ─────────────────────────────────────────────────────

        [Tooltip("자원 노드(나무/돌/광석) 감지 반경 (타일 단위 맨해튼 거리). 기획서 기본값: 5")]
        [SerializeField] private int _resourceSensingRadius = 5;   // 기획서 수치: 자원 감지 5타일

        [Tooltip("적 유닛 감지 반경 (타일 단위 맨해튼 거리). 기획서 기본값: 8")]
        [SerializeField] private int _enemyDetectionRadius = 8;    // 기획서 수치: 적 감지 8타일

        [Tooltip("건물(창고/모닥불 등) 감지 반경 (타일 단위 맨해튼 거리). 기획서 기본값: 3")]
        [SerializeField] private int _buildingSensingRadius = 3;   // 기획서 수치: 건물 감지 3타일

        [Tooltip("드롭 아이템 감지 반경 (타일 단위 맨해튼 거리). 기획서 기본값: 4")]
        [SerializeField] private int _droppedItemRadius = 4;       // 기획서 수치: 드롭아이템 감지 4타일

        // ── 건물 Transform 참조 ────────────────────────────────────────────────
        // 각 건물 타입의 Transform을 Inspector에서 직접 연결한다.
        // 배열(_storageTransforms 등)은 여러 건물이 있을 때 모두 등록한다.
        // 하나라도 반경 내이면 해당 플래그가 true가 된다.

        [Tooltip("기지(본부) Transform. AtBase 플래그 판정에 사용한다. 반드시 할당할 것.")]
        [SerializeField] private Transform _baseTransform;

        [Tooltip("창고 Transform 목록. 여러 창고가 있을 때 모두 등록한다. NearStorage 플래그 판정.")]
        [SerializeField] private Transform[] _storageTransforms;

        [Tooltip("모닥불/요리장 Transform 목록. NearFireplace 플래그 판정.")]
        [SerializeField] private Transform[] _firehouseTransforms;

        [Tooltip("침대/숙소 Transform 목록. NearBed 플래그 판정.")]
        [SerializeField] private Transform[] _bedTransforms;

        [Tooltip("치료소 Transform 목록. NearHealer 플래그 판정.")]
        [SerializeField] private Transform[] _healerTransforms;

        [Tooltip("망루 Transform 목록. NearWatchtower 플래그 판정.")]
        [SerializeField] private Transform[] _watchtowerTransforms;

        #endregion

        #region ── Private Fields ──

        // ── 등록된 주민 목록 ────────────────────────────────────────────────────
        // GameManager가 RegisterVillager/UnregisterVillager로 관리한다.
        // 키: VillagerId (string), 값: VillagerFSM 인스턴스
        // Dictionary 사용 이유: UnregisterVillager 시 O(1) 제거를 위해
        private readonly Dictionary<string, AIVillage.AI.VillagerFSM> _villagerMap
            = new Dictionary<string, AIVillage.AI.VillagerFSM>();

        // [PR Fix]: Warning-1 — Dictionary + List 동기화 방식으로 변경.
        // 더티 플래그(RebuildVillagerList) 방식은 Dictionary.Values foreach 순회 시 Enumerator 힙 할당이 발생했다.
        // Register/Unregister 시점에 직접 List를 동기화하면 UpdateAllSensors()에서 GC 없이 순회 가능하다.
        // UpdateAllSensors() 내에서 GC 없이 주민을 순회하기 위한 캐시 리스트
        // _villagerMap 변경(등록/해제) 시 즉시 동기화한다.
        private readonly List<AIVillage.AI.VillagerFSM> _villagerList
            = new List<AIVillage.AI.VillagerFSM>();

        // ── 등록된 적 목록 ──────────────────────────────────────────────────────
        // [PR Fix]: Warning-2 — List 단독에서 HashSet + List 병행 패턴으로 교체.
        // 기존 _enemies.Contains()와 _enemies.Remove()는 O(n) 비용이었다.
        // HashSet으로 O(1) 중복 체크 및 제거, List로 인덱스 기반 for 순회(GC 없음)를 병행한다.
        private readonly HashSet<IEnemyAgent> _enemySet  = new HashSet<IEnemyAgent>();
        private readonly List<IEnemyAgent>    _enemyList = new List<IEnemyAgent>();

        // ── 자원 노드 등록소 ────────────────────────────────────────────────────
        // 키: NodeId (string), 값: ResourceNode
        // AddResourceNode/RemoveResourceNode/GetResourceNode API로 관리한다.
        private readonly Dictionary<string, ResourceNode> _nodeMap
            = new Dictionary<string, ResourceNode>();

        // [PR Fix]: Warning-1 — Dictionary + List 동기화 방식으로 변경.
        // _nodeListDirty 더티 플래그 제거 후 Add/Remove 시점에 즉시 리스트를 동기화한다.
        // UpdateAllSensors() 내에서 GC 없이 노드를 순회하기 위한 캐시 리스트
        private readonly List<ResourceNode> _nodeList = new List<ResourceNode>();

        // ── 건물 타일 좌표 캐시 ────────────────────────────────────────────────
        // Inspector Transform을 타일 좌표로 변환한 캐시.
        // 매 UpdateAllSensors() 호출마다 변환하지 않도록 Awake에서 한 번만 계산한다.
        // 건물은 런타임 중 이동하지 않는다고 가정한다.
        // TODO: 기획팀 — 건물이 런타임에 이동할 경우 RefreshBuildingCache() API 노출 필요
        private int _baseTileX;
        private int _baseTileY;

        // 각 배열 건물의 타일 좌표 캐시 (Inspector Transform[] → int[] x2)
        private int[] _storageTileX;
        private int[] _storageTileY;
        private int[] _firehouseTileX;
        private int[] _firehouseTileY;
        private int[] _bedTileX;
        private int[] _bedTileY;
        private int[] _healerTileX;
        private int[] _healerTileY;
        private int[] _watchtowerTileX;
        private int[] _watchtowerTileY;

        // ── AuthoritativeWorldState 참조 캐시 ─────────────────────────────────
        // UpdateAllSensors() 내부에서 싱글턴을 매번 조회하지 않도록 Awake에서 캐시한다.
        private AuthoritativeWorldState _worldState;

        #endregion

        #region ── Unity 생명주기 메서드 ──

        // [PR Fix]: Critical-2 — Awake()를 싱글턴 패턴 포함 형태로 재구성.
        // 중복 인스턴스를 감지하여 파괴하고, 단일 인스턴스만 유효하게 유지한다.
        // Warning-3도 이곳에서 함께 처리: _baseTransform null에 대해 Debug.Assert를 추가한다.
        /// <summary>
        /// Unity가 GameObject 활성화 시 가장 먼저 호출한다.
        /// 싱글턴 인스턴스를 설정하고, AuthoritativeWorldState 싱글턴을 캐시하고,
        /// 건물 타일 좌표를 변환하여 저장한다.
        /// </summary>
        private void Awake()
        {
            // ── 싱글턴 중복 체크 ──────────────────────────────────────────────
            // 씬에 SensorSystem이 두 개 이상 있을 경우 두 번째 이후 인스턴스를 파괴한다.
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning(
                    $"{LOG_PREFIX} 중복 인스턴스 감지. 이 인스턴스를 파괴합니다. " +
                    "씬에 SensorSystem이 하나만 있는지 확인하세요."
                );
                Destroy(this);
                return;
            }
            Instance = this;

            // ── AuthoritativeWorldState 싱글턴 캐시 ───────────────────────────
            // GameManager보다 SensorSystem이 먼저 Awake되면 null일 수 있다.
            // InjectWorldState()로 나중에 주입할 수 있도록 경고만 출력한다.
            _worldState = AuthoritativeWorldState.Instance;
            if (_worldState == null)
            {
                // 정상 동작: SensorSystem(-85)이 GameManager(-80)보다 먼저 Awake되므로
                // 이 시점에 WorldState가 없는 것은 예상된 상황이다.
                // GameManager.Awake()에서 InjectWorldState()로 즉시 보정된다.
                Debug.Log(
                    $"{LOG_PREFIX} Awake: WorldState 미주입 상태. GameManager.Awake()에서 InjectWorldState()로 보정 예정."
                );
            }

            // [PR Fix]: Warning-3 — _baseTransform null에 대해 Debug.Assert 추가.
            // null이면 AtBase 판정이 항상 false가 되므로 개발 단계에서 반드시 Inspector 할당을 강제한다.
            Debug.Assert(
                _baseTransform != null,
                $"{LOG_PREFIX} _baseTransform이 Inspector에 할당되지 않았습니다. AtBase 판정이 동작하지 않습니다."
            );

            // 건물 타일 좌표 캐시 빌드 (Inspector 값이 할당된 경우에만)
            RebuildBuildingCache();
        }

        // [PR Fix]: Critical-2 — OnDestroy() 추가. 씬 전환 또는 GameObject 파괴 시 Instance를 null로 해제한다.
        // 이렇게 해야 다음 씬 로드 시 Instance가 파괴된 오브젝트를 참조하는 것을 방지한다.
        /// <summary>
        /// GameObject가 파괴될 때 호출된다.
        /// 싱글턴 Instance 참조를 null로 해제하여 다음 씬에서 stale 참조를 방지한다.
        /// </summary>
        private void OnDestroy()
        {
            // 이 인스턴스가 실제로 현재 활성 싱글턴일 때만 해제한다.
            // (중복 인스턴스가 Destroy되는 경우를 구분하기 위해)
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region ── 공개 API: 주민 등록/해제 ──

        /// <summary>
        /// 주민 FSM을 SensorSystem에 등록한다.
        /// GameManager.Start() 또는 주민 스폰 시 호출한다.
        /// </summary>
        /// <param name="fsm">등록할 VillagerFSM. null이면 경고 후 무시.</param>
        public void RegisterVillager(AIVillage.AI.VillagerFSM fsm)
        {
            if (fsm == null)
            {
                Debug.LogWarning($"{LOG_PREFIX} RegisterVillager: fsm이 null입니다. 등록을 건너뜁니다.");
                return;
            }

            // Brain 프로퍼티로 VillagerId를 읽는다 (5단계에서 추가된 공개 프로퍼티)
            string id = fsm.Brain?.VillagerId;
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning($"{LOG_PREFIX} RegisterVillager: VillagerId가 비어있습니다. 등록을 건너뜁니다.");
                return;
            }

            if (_villagerMap.ContainsKey(id))
            {
                Debug.LogWarning(
                    $"{LOG_PREFIX} RegisterVillager: VillagerId={id}는 이미 등록되어 있습니다. " +
                    "중복 등록을 건너뜁니다."
                );
                return;
            }

            // [PR Fix]: Warning-1 — 더티 플래그 방식 제거. 등록 즉시 Map과 List를 함께 동기화한다.
            // 이렇게 하면 UpdateAllSensors() 직전에 RebuildVillagerList()를 호출할 필요가 없어
            // Dictionary.Values foreach의 Enumerator 힙 할당을 없앤다.
            _villagerMap[id] = fsm;
            _villagerList.Add(fsm);
            Debug.Log($"{LOG_PREFIX} 주민 등록 완료. VillagerId={id}, 총 주민={_villagerMap.Count}");
        }

        /// <summary>
        /// 주민 FSM을 SensorSystem에서 해제한다.
        /// 주민 사망 또는 씬 전환 시 호출한다.
        /// </summary>
        /// <param name="villagerId">해제할 주민의 고유 ID.</param>
        public void UnregisterVillager(string villagerId)
        {
            if (string.IsNullOrEmpty(villagerId))
            {
                Debug.LogWarning($"{LOG_PREFIX} UnregisterVillager: villagerId가 null/empty입니다.");
                return;
            }

            // [PR Fix]: Warning-1 — Map 제거 성공 시 List에서도 즉시 해당 FSM을 제거한다.
            // 역순 for를 사용하여 RemoveAt 시 인덱스 오염 없이 안전하게 제거한다.
            if (_villagerMap.Remove(villagerId))
            {
                // _villagerList에서 villagerId에 해당하는 FSM을 찾아 제거
                for (int i = _villagerList.Count - 1; i >= 0; i--)
                {
                    if (_villagerList[i]?.Brain?.VillagerId == villagerId)
                    {
                        _villagerList.RemoveAt(i);
                        break; // 동일 ID는 하나뿐이므로 첫 발견 즉시 종료
                    }
                }
                Debug.Log(
                    $"{LOG_PREFIX} 주민 해제 완료. VillagerId={villagerId}, 남은 주민={_villagerMap.Count}"
                );
            }
            else
            {
                Debug.LogWarning($"{LOG_PREFIX} UnregisterVillager: VillagerId={villagerId}를 찾을 수 없습니다.");
            }
        }

        #endregion

        #region ── 공개 API: 적 등록/해제 ──

        // [PR Fix]: Warning-2 — RegisterEnemy/UnregisterEnemy를 HashSet + List 병행 패턴으로 교체.
        // HashSet._enemySet이 O(1) 중복 체크/제거를 담당하고,
        // _enemyList는 UpdateEnemyFlag에서 인덱스 기반 for 순회(GC 없음)를 담당한다.

        /// <summary>
        /// 적 유닛을 SensorSystem에 등록한다.
        /// EnemySpawner 또는 적 유닛 Awake에서 호출한다.
        /// HashSet으로 O(1) 중복 체크를 수행하여 기존 O(n) Contains를 교체한다.
        /// </summary>
        /// <param name="enemy">등록할 적. null이면 경고 후 무시.</param>
        public void RegisterEnemy(IEnemyAgent enemy)
        {
            if (enemy == null)
            {
                Debug.LogWarning($"{LOG_PREFIX} RegisterEnemy: enemy가 null입니다.");
                return;
            }

            // HashSet.Add()는 이미 존재하면 false를 반환한다 — 중복 등록을 O(1)로 방지
            if (_enemySet.Add(enemy))
            {
                _enemyList.Add(enemy);
                Debug.Log($"{LOG_PREFIX} 적 등록 완료. 총 적={_enemyList.Count}");
            }
        }

        /// <summary>
        /// 적 유닛을 SensorSystem에서 해제한다.
        /// 적 사망 또는 씬 전환 시 호출한다.
        /// HashSet으로 O(1) 제거를 수행하여 기존 O(n) Remove를 교체한다.
        /// </summary>
        /// <param name="enemy">해제할 적.</param>
        public void UnregisterEnemy(IEnemyAgent enemy)
        {
            if (enemy == null)
            {
                Debug.LogWarning($"{LOG_PREFIX} UnregisterEnemy: enemy가 null입니다.");
                return;
            }

            // HashSet.Remove()가 성공하면 List에서도 제거한다
            if (_enemySet.Remove(enemy))
            {
                _enemyList.Remove(enemy);
            }
            else
            {
                Debug.LogWarning($"{LOG_PREFIX} UnregisterEnemy: 등록되지 않은 적을 해제하려 했습니다.");
            }
        }

        #endregion

        #region ── 공개 API: 자원 노드 관리 ──

        /// <summary>
        /// 자원 노드를 SensorSystem에 등록한다.
        /// 맵 로드 시 또는 런타임 노드 생성 시 호출한다.
        /// </summary>
        /// <param name="node">등록할 ResourceNode. null이면 경고 후 무시.</param>
        public void AddResourceNode(ResourceNode node)
        {
            if (node == null)
            {
                Debug.LogWarning($"{LOG_PREFIX} AddResourceNode: node가 null입니다.");
                return;
            }

            if (string.IsNullOrEmpty(node.NodeId))
            {
                Debug.LogWarning($"{LOG_PREFIX} AddResourceNode: NodeId가 비어있습니다. 등록 불가.");
                return;
            }

            // 이미 등록된 경우 업데이트 (경고만 출력하고 계속 진행)
            if (_nodeMap.ContainsKey(node.NodeId))
            {
                Debug.LogWarning(
                    $"{LOG_PREFIX} AddResourceNode: NodeId={node.NodeId}가 이미 등록되어 있습니다. " +
                    "기존 노드를 덮어씁니다."
                );
                // 이미 리스트에 있는 기존 항목을 새 node로 교체한다
                for (int i = 0; i < _nodeList.Count; i++)
                {
                    if (_nodeList[i]?.NodeId == node.NodeId)
                    {
                        _nodeList[i] = node;
                        break;
                    }
                }
                _nodeMap[node.NodeId] = node;
                return;
            }

            // [PR Fix]: Warning-1 — 더티 플래그 방식 제거. 추가 즉시 Map과 List를 함께 동기화한다.
            _nodeMap[node.NodeId] = node;
            _nodeList.Add(node);
        }

        /// <summary>
        /// 자원 노드를 SensorSystem에서 제거한다.
        /// 노드가 완전히 고갈되어 삭제될 때 호출한다.
        /// </summary>
        /// <param name="nodeId">제거할 노드의 고유 ID.</param>
        public void RemoveResourceNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                Debug.LogWarning($"{LOG_PREFIX} RemoveResourceNode: nodeId가 null/empty입니다.");
                return;
            }

            // [PR Fix]: Warning-1 — Map 제거 성공 시 List에서도 즉시 해당 노드를 제거한다.
            // 역순 for를 사용하여 RemoveAt 시 인덱스 오염 없이 안전하게 제거한다.
            if (_nodeMap.Remove(nodeId))
            {
                for (int i = _nodeList.Count - 1; i >= 0; i--)
                {
                    if (_nodeList[i]?.NodeId == nodeId)
                    {
                        _nodeList.RemoveAt(i);
                        break; // 동일 NodeId는 하나뿐이므로 첫 발견 즉시 종료
                    }
                }
            }
            else
            {
                Debug.LogWarning($"{LOG_PREFIX} RemoveResourceNode: NodeId={nodeId}를 찾을 수 없습니다.");
            }
        }

        /// <summary>
        /// NodeId로 특정 자원 노드를 반환한다.
        /// FoW 탐험 완료 시 IsDiscovered를 갱신하거나 채집 Action이 노드를 점유할 때 사용한다.
        /// </summary>
        /// <param name="nodeId">조회할 노드 ID.</param>
        /// <returns>등록된 ResourceNode. 없으면 null.</returns>
        public ResourceNode GetResourceNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return null;

            _nodeMap.TryGetValue(nodeId, out ResourceNode node);
            return node; // 없으면 null 반환 — 호출자가 null 체크
        }

        /// <summary>
        /// 등록된 모든 자원 노드의 읽기 전용 리스트를 반환한다.
        /// 외부 시스템(GOAP 플래너, 디버그 UI 등)이 전체 노드를 조회할 때 사용한다.
        /// </summary>
        public IReadOnlyList<ResourceNode> GetAllNodes()
        {
            // [PR Fix]: Warning-1 — 더티 플래그 체크 제거.
            // _nodeList는 AddResourceNode/RemoveResourceNode에서 항상 동기화되므로 즉시 반환 가능하다.
            return _nodeList;
        }

        #endregion

        #region ── 공개 API: 핵심 갱신 메서드 ──

        // [PR Fix]: Warning-4 — UpdateAllSensors() XML 주석에 단일 스레드 전제 remarks 추가.
        /// <summary>
        /// 모든 등록된 주민의 VillagerBrain 환경 플래그를 갱신한다.
        /// GameManager의 0.1초 Tick 코루틴에서 호출한다.
        ///
        /// 성능 규칙:
        ///   - GC 할당 없음 (List 캐시 사용, LINQ 금지)
        ///   - 모든 bool 플래그는 먼저 false로 리셋 후 갱신한다 (누락 방지)
        /// </summary>
        /// <remarks>
        /// 주의: 이 메서드는 Unity 메인 스레드에서만 호출해야 합니다.
        /// RegisterVillager/UnregisterVillager와 동일 스레드에서 순차 실행을 보장해야 합니다.
        /// GameManager 코루틴(0.1초 간격)이 유일한 호출자여야 합니다.
        /// </remarks>
        public void UpdateAllSensors()
        {
            // WorldState 참조 지연 초기화 (Awake 시 null이었던 경우 복구 시도)
            if (_worldState == null)
            {
                _worldState = AuthoritativeWorldState.Instance;
                // 여전히 null이면 드롭 아이템 감지를 건너뛴다 — 나머지는 계속 처리한다.
            }

            // ── 주민별 플래그 갱신 루프 ─────────────────────────────────────────
            // _villagerList는 Register/Unregister 시 항상 동기화되므로 GC 없이 순회 가능하다.
            int villagerCount = _villagerList.Count;
            for (int i = 0; i < villagerCount; i++)
            {
                AIVillage.AI.VillagerFSM fsm = _villagerList[i];

                // null 방어: 씬 전환 중 GC된 컴포넌트 참조 방지
                if (fsm == null) continue;

                AIVillage.AI.VillagerBrain brain = fsm.Brain;
                if (brain == null) continue;

                // 사망한 주민은 감지 처리를 건너뜀 (Dead 상태에서는 플래그 불필요)
                if (!brain.IsAlive) continue;

                int vx = brain.TileX;
                int vy = brain.TileY;

                // ──────────────────────────────────────────────────────────────
                // Step 1: 자원 노드 플래그 갱신
                //   NearRock, NearIronOre, NearCopperOre, NearResource, NearDiscoveredResource
                // ──────────────────────────────────────────────────────────────
                UpdateResourceFlags(brain, vx, vy);

                // ──────────────────────────────────────────────────────────────
                // Step 2: 적 감지 플래그 갱신
                //   NearEnemy
                // ──────────────────────────────────────────────────────────────
                UpdateEnemyFlag(brain, vx, vy);

                // ──────────────────────────────────────────────────────────────
                // Step 3: 드롭 아이템 감지 플래그 갱신
                //   NearDroppedItem
                // ──────────────────────────────────────────────────────────────
                UpdateDroppedItemFlag(brain, vx, vy);

                // ──────────────────────────────────────────────────────────────
                // Step 4: 건물 근접 플래그 갱신
                //   AtBase, NearStorage, NearFireplace, NearBed, NearHealer, NearWatchtower
                // ──────────────────────────────────────────────────────────────
                UpdateBuildingFlags(brain, vx, vy);
            }
        }

        // [PR Fix]: Warning-5 — 파라미터 이름 deltaGameTime → deltaGameDays 로 변경.
        // '시간(time)'보다 '게임 일수(days)' 단위임을 명확하게 표현한다. Debug.Assert도 추가한다.
        /// <summary>
        /// 자원 노드의 시간 경과 재생을 처리한다.
        /// GameManager의 GameTimeManager Tick에서 게임 하루가 경과할 때마다 호출한다.
        ///
        /// 공식: CurrentAmount = Clamp(CurrentAmount + RegenerationRate * deltaGameDays, 0, MaxAmount)
        ///
        /// GDD v0.4 기본 재생률: Wood=5, Stone=3, Iron=1.5, Copper=0.5, Silver=0.2 (per game day)
        /// </summary>
        /// <param name="deltaGameDays">경과한 게임 일수 (일반적으로 1.0f)</param>
        public void TickResourceRegeneration(float deltaGameDays)
        {
            // [PR Fix]: Warning-5 — 비정상적인 deltaGameDays 값을 개발 단계에서 조기 감지한다.
            // 음수이거나 비현실적으로 큰 값(10일 초과)은 호출 측 버그를 의미한다.
            Debug.Assert(
                deltaGameDays >= 0f && deltaGameDays <= 10f,
                $"{LOG_PREFIX} TickResourceRegeneration: deltaGameDays={deltaGameDays}가 비정상입니다."
            );

            int nodeCount = _nodeList.Count;
            for (int i = 0; i < nodeCount; i++)
            {
                ResourceNode node = _nodeList[i];
                if (node == null) continue;

                // 재생률이 0이면 처리 불필요 (RawFood, CookedFood 노드)
                if (node.RegenerationRate <= 0f) continue;

                // 공식: CurrentAmount += RegenerationRate * 경과 게임 일수, 상한 MaxAmount
                float regenerated  = node.RegenerationRate * deltaGameDays;
                node.CurrentAmount = Mathf.Min(node.MaxAmount, node.CurrentAmount + regenerated);
            }
        }

        /// <summary>
        /// 지정 반경 내 모든 자원 노드를 발견 상태(IsDiscovered = true)로 전환한다.
        /// VillagerFSM의 Explore Action 완료 시 ApplyActionEffect()에서 호출한다.
        ///
        /// 사용 예시 (VillagerFSM.ApplyActionEffect):
        ///   if (SensorSystem.Instance != null)
        ///       SensorSystem.Instance.DiscoverArea(brain.TileX, brain.TileY, 5);
        /// </summary>
        /// <param name="centerTileX">탐험 중심 타일 X</param>
        /// <param name="centerTileY">탐험 중심 타일 Y</param>
        /// <param name="radius">발견 반경 (타일 맨해튼 거리)</param>
        public void DiscoverArea(int centerTileX, int centerTileY, int radius)
        {
            int discovered = 0;
            int nodeCount  = _nodeList.Count;

            for (int i = 0; i < nodeCount; i++)
            {
                ResourceNode node = _nodeList[i];
                if (node == null) continue;
                if (node.IsDiscovered) continue; // 이미 발견된 노드는 건너뜀

                if (ManhattanDistance(centerTileX, centerTileY, node.TileX, node.TileY) <= radius)
                {
                    node.IsDiscovered = true;
                    discovered++;
                }
            }

            if (discovered > 0)
            {
                Debug.Log(
                    $"{LOG_PREFIX} DiscoverArea: 중심({centerTileX},{centerTileY}) 반경={radius}. " +
                    $"새로 발견된 노드 {discovered}개."
                );
            }
        }

        /// <summary>
        /// WorldState 싱글턴을 외부에서 주입한다.
        /// GameManager.Start()에서 Awake 초기화 순서 문제를 해결할 때 호출한다.
        /// </summary>
        public void InjectWorldState(AuthoritativeWorldState worldState)
        {
            if (worldState == null)
            {
                Debug.LogWarning($"{LOG_PREFIX} InjectWorldState: null이 전달되었습니다. 무시합니다.");
                return;
            }
            _worldState = worldState;
        }

        /// <summary>
        /// 건물 타일 좌표 캐시를 수동으로 재빌드한다.
        /// 런타임 중 건물 Transform이 변경된 경우에만 호출한다.
        /// 일반적으로 Awake에서 자동 호출되므로 직접 호출할 필요가 없다.
        /// </summary>
        public void RefreshBuildingCache()
        {
            RebuildBuildingCache();
        }

        #endregion

        #region ── 내부 플래그 갱신 메서드 ──

        /// <summary>
        /// 주민 주변 자원 노드를 검사하여 Brain의 자원 관련 플래그를 갱신한다.
        ///
        /// 갱신 대상:
        ///   NearRock               — 반경 내 Stone 노드 존재 AND CurrentAmount > 0
        ///   NearIronOre            — 반경 내 Iron 노드 존재 AND CurrentAmount > 0
        ///   NearCopperOre          — 반경 내 Copper 노드 존재 AND CurrentAmount > 0
        ///   NearResource           — NearRock OR NearIronOre OR NearCopperOre
        ///                            OR 반경 내 Wood/RawFood 노드 존재
        ///   NearDiscoveredResource — 반경 내 자원 노드가 있고 IsDiscovered == true
        /// </summary>
        private void UpdateResourceFlags(AIVillage.AI.VillagerBrain brain, int vx, int vy)
        {
            // ── 모든 자원 플래그를 먼저 false로 초기화 (누락 방지) ──────────────
            // 매 Tick마다 전체를 다시 계산하므로 이전 상태를 완전히 무시한다.
            brain.NearRock               = false;
            brain.NearIronOre            = false;
            brain.NearCopperOre          = false;
            brain.NearResource           = false;
            brain.NearDiscoveredResource = false;

            // ── 노드 순회: GC 없이 인덱스 기반 for 사용 ──────────────────────
            int nodeCount = _nodeList.Count;
            for (int n = 0; n < nodeCount; n++)
            {
                ResourceNode node = _nodeList[n];
                if (node == null) continue;

                // 고갈된 노드는 감지 대상에서 제외 (채집 불가이므로)
                if (node.CurrentAmount <= 0f) continue;

                // 맨해튼 거리가 감지 반경을 초과하면 건너뜀
                int dist = ManhattanDistance(vx, vy, node.TileX, node.TileY);
                if (dist > _resourceSensingRadius) continue;

                // ── 자원 타입별 세부 플래그 갱신 ──────────────────────────────
                // [PR Fix]: Critical-1 — NearDiscoveredResource를 switch-case 내부로 이동.
                // 기존에는 switch 바깥의 별도 if (brain.NearResource && node.IsDiscovered) 블록에서 판정했는데,
                // 이 방식은 이전 노드가 NearResource=true를 설정한 상태에서 현재 노드의 IsDiscovered만 체크하므로
                // 서로 다른 노드의 FoW 상태가 섞이는 오염이 발생했다.
                // 각 case 내부에서 해당 노드의 IsDiscovered를 함께 체크하여 정확히 판정한다.
                switch (node.ResourceType)
                {
                    case ResourceType.Stone:
                        // NearRock: 반경 내 Stone 노드가 있고 채집 가능
                        brain.NearRock     = true;
                        brain.NearResource = true;
                        // 이 Stone 노드가 발견된 경우에만 NearDiscoveredResource를 true로 설정
                        if (node.IsDiscovered) brain.NearDiscoveredResource = true;
                        break;

                    case ResourceType.Iron:
                        // NearIronOre: 반경 내 Iron 노드가 있고 채집 가능
                        brain.NearIronOre  = true;
                        brain.NearResource = true;
                        // 이 Iron 노드가 발견된 경우에만 NearDiscoveredResource를 true로 설정
                        if (node.IsDiscovered) brain.NearDiscoveredResource = true;
                        break;

                    case ResourceType.Copper:
                        // NearCopperOre: 반경 내 Copper 노드가 있고 채집 가능
                        brain.NearCopperOre = true;
                        brain.NearResource  = true;
                        // 이 Copper 노드가 발견된 경우에만 NearDiscoveredResource를 true로 설정
                        if (node.IsDiscovered) brain.NearDiscoveredResource = true;
                        break;

                    case ResourceType.Wood:
                    case ResourceType.RawFood:
                        // 나무와 생 식량도 NearResource에 포함 (세부 플래그는 없음)
                        brain.NearResource = true;
                        // 이 Wood/RawFood 노드가 발견된 경우에만 NearDiscoveredResource를 true로 설정
                        if (node.IsDiscovered) brain.NearDiscoveredResource = true;
                        break;

                    // Silver, CookedFood는 자연 노드에 존재하지 않으므로 무시
                    default:
                        break;
                }
                // [PR Fix]: Critical-1 — switch 바깥의 별도 if (brain.NearResource && node.IsDiscovered) 블록 삭제됨.
                // 위 각 case 안에서 직접 처리하므로 이 위치에 if 블록이 없는 것이 정상이다.
            }
        }

        /// <summary>
        /// 등록된 적 목록을 검사하여 Brain.NearEnemy 플래그를 갱신한다.
        /// IsAlive == false인 적은 제외한다.
        /// 하나라도 반경 내에 있으면 즉시 true를 설정하고 나머지 검사를 건너뛴다.
        /// </summary>
        private void UpdateEnemyFlag(AIVillage.AI.VillagerBrain brain, int vx, int vy)
        {
            // 먼저 false로 초기화 (누락 방지)
            brain.NearEnemy = false;

            // [PR Fix]: Warning-2 — _enemies(List 단독) 대신 _enemyList(HashSet+List 병행 패턴)로 순회.
            // 인덱스 기반 for를 사용하여 GC 없이 순회한다.
            int enemyCount = _enemyList.Count;
            for (int e = 0; e < enemyCount; e++)
            {
                IEnemyAgent enemy = _enemyList[e];

                // null 방어 및 사망한 적 제외
                if (enemy == null) continue;
                if (!enemy.IsAlive) continue;

                int dist = ManhattanDistance(vx, vy, enemy.TileX, enemy.TileY);
                if (dist <= _enemyDetectionRadius)
                {
                    brain.NearEnemy = true;
                    return; // 하나만 찾아도 충분 — 조기 종료로 성능 최적화
                }
            }
        }

        /// <summary>
        /// AuthoritativeWorldState.DroppedItems를 검사하여 Brain.NearDroppedItem 플래그를 갱신한다.
        /// DroppedItem은 struct이지만 List 인덱서 접근을 통해 GC 박싱을 방지한다.
        /// </summary>
        private void UpdateDroppedItemFlag(AIVillage.AI.VillagerBrain brain, int vx, int vy)
        {
            // 먼저 false로 초기화 (누락 방지)
            brain.NearDroppedItem = false;

            // WorldState 미초기화 시 드롭 아이템 감지를 건너뜀
            if (_worldState == null) return;

            List<DroppedItem> droppedItems = _worldState.DroppedItems;
            if (droppedItems == null) return;

            // DroppedItem은 struct이므로 인덱스 기반 for로 GC 박싱 없이 순회한다
            int itemCount = droppedItems.Count;
            for (int d = 0; d < itemCount; d++)
            {
                DroppedItem item = droppedItems[d]; // struct 복사 — 힙 할당 없음
                int dist = ManhattanDistance(vx, vy, item.TileX, item.TileY);
                if (dist <= _droppedItemRadius)
                {
                    brain.NearDroppedItem = true;
                    return; // 하나만 찾아도 충분
                }
            }
        }

        /// <summary>
        /// 건물 타일 좌표 캐시와 주민 위치를 비교하여 건물 근접 플래그를 갱신한다.
        ///
        /// 갱신 대상:
        ///   AtBase         — _baseTransform까지의 거리 <= _buildingSensingRadius
        ///   NearStorage    — _storageTransforms 중 하나라도 반경 내
        ///   NearFireplace  — _firehouseTransforms 중 하나라도 반경 내
        ///   NearBed        — _bedTransforms 중 하나라도 반경 내
        ///   NearHealer     — _healerTransforms 중 하나라도 반경 내
        ///   NearWatchtower — _watchtowerTransforms 중 하나라도 반경 내
        /// </summary>
        private void UpdateBuildingFlags(AIVillage.AI.VillagerBrain brain, int vx, int vy)
        {
            // ── 모든 건물 플래그를 먼저 false로 초기화 (누락 방지) ───────────
            brain.AtBase         = false;
            brain.NearStorage    = false;
            brain.NearFireplace  = false;
            brain.NearBed        = false;
            brain.NearHealer     = false;
            brain.NearWatchtower = false;

            // ── AtBase: 기지 Transform 캐시 비교 ─────────────────────────────
            // _baseTransform이 Inspector에서 할당되었을 때만 판정한다.
            if (_baseTransform != null)
            {
                if (ManhattanDistance(vx, vy, _baseTileX, _baseTileY) <= _buildingSensingRadius)
                {
                    brain.AtBase = true;
                }
            }

            // ── NearStorage ────────────────────────────────────────────────────
            brain.NearStorage = IsNearAnyBuilding(vx, vy, _storageTileX, _storageTileY);

            // ── NearFireplace ──────────────────────────────────────────────────
            brain.NearFireplace = IsNearAnyBuilding(vx, vy, _firehouseTileX, _firehouseTileY);

            // ── NearBed ────────────────────────────────────────────────────────
            brain.NearBed = IsNearAnyBuilding(vx, vy, _bedTileX, _bedTileY);

            // ── NearHealer ─────────────────────────────────────────────────────
            brain.NearHealer = IsNearAnyBuilding(vx, vy, _healerTileX, _healerTileY);

            // ── NearWatchtower ─────────────────────────────────────────────────
            brain.NearWatchtower = IsNearAnyBuilding(vx, vy, _watchtowerTileX, _watchtowerTileY);
        }

        #endregion

        #region ── 내부 헬퍼 메서드 ──

        /// <summary>
        /// 주민이 건물 타일 배열 중 하나라도 감지 반경 내에 있는지 확인한다.
        /// tileXs가 null이거나 비어있으면 false를 반환한다.
        /// </summary>
        /// <param name="vx">주민 타일 X</param>
        /// <param name="vy">주민 타일 Y</param>
        /// <param name="tileXs">건물 타일 X 배열 (Awake 캐시)</param>
        /// <param name="tileYs">건물 타일 Y 배열 (Awake 캐시)</param>
        /// <returns>반경 내 건물 존재 여부</returns>
        private bool IsNearAnyBuilding(int vx, int vy, int[] tileXs, int[] tileYs)
        {
            if (tileXs == null) return false;

            int count = tileXs.Length;
            for (int b = 0; b < count; b++)
            {
                if (ManhattanDistance(vx, vy, tileXs[b], tileYs[b]) <= _buildingSensingRadius)
                {
                    return true; // 하나라도 반경 내이면 즉시 반환
                }
            }
            return false;
        }

        /// <summary>
        /// 두 타일 좌표 사이의 맨해튼 거리를 계산한다.
        /// NavMesh 없이 타일 그리드에서 근접 판정 시 사용한다.
        ///
        /// 공식: |x1-x2| + |y1-y2|
        /// 주의: 맨해튼 거리는 장애물을 고려하지 않는다.
        ///       실제 경로 거리로 교체할 경우 이 메서드만 수정하면 된다.
        /// </summary>
        private static int ManhattanDistance(int x1, int y1, int x2, int y2)
        {
            return Mathf.Abs(x1 - x2) + Mathf.Abs(y1 - y2);
        }

        /// <summary>
        /// Transform world position (Vector3)을 타일 좌표 (int, int)로 변환한다.
        /// Unity Y-up 좌표계에서 XZ 평면이 타일 그리드에 해당한다.
        ///   position.x → tileX
        ///   position.z → tileY  (Z축이 앞뒤 방향이므로 타일 Y로 매핑)
        /// </summary>
        private static void WorldToTile(Vector3 worldPos, out int tileX, out int tileY)
        {
            tileX = Mathf.RoundToInt(worldPos.x);
            tileY = Mathf.RoundToInt(worldPos.z);
        }

        /// <summary>
        /// Inspector Transform 배열을 타일 좌표 int 배열로 변환하여 캐시한다.
        /// Awake에서 한 번, 그리고 RefreshBuildingCache() 호출 시 실행된다.
        /// </summary>
        private static void CacheTransformArray(
            Transform[] transforms,
            out int[]   outTileX,
            out int[]   outTileY)
        {
            if (transforms == null || transforms.Length == 0)
            {
                // 빈 배열로 초기화 — null이 아니므로 IsNearAnyBuilding에서 null 체크 불필요
                outTileX = System.Array.Empty<int>();
                outTileY = System.Array.Empty<int>();
                return;
            }

            int count = transforms.Length;
            outTileX  = new int[count];
            outTileY  = new int[count];

            for (int i = 0; i < count; i++)
            {
                if (transforms[i] == null)
                {
                    // null Transform 슬롯은 (0,0)으로 채워두되 경고 출력
                    // (0,0)은 기지 근처일 수 있으므로 Inspector에서 반드시 수정해야 한다)
                    Debug.LogWarning(
                        $"{LOG_PREFIX} CacheTransformArray: transforms[{i}]가 null입니다. " +
                        "(0,0)으로 처리합니다. Inspector에서 할당을 확인하세요."
                    );
                    outTileX[i] = 0;
                    outTileY[i] = 0;
                    continue;
                }

                WorldToTile(transforms[i].position, out outTileX[i], out outTileY[i]);
            }
        }

        /// <summary>
        /// Inspector에서 할당된 모든 건물 Transform을 타일 좌표로 변환하여 캐시한다.
        /// Awake에서 한 번 호출되며, 런타임 건물 이동 시 RefreshBuildingCache()로 재호출한다.
        /// </summary>
        private void RebuildBuildingCache()
        {
            // 기지 변환
            if (_baseTransform != null)
            {
                WorldToTile(_baseTransform.position, out _baseTileX, out _baseTileY);
            }
            else
            {
                // _baseTransform null은 Awake의 Debug.Assert에서 이미 경고되었으므로 여기서는 조용히 처리
                _baseTileX = 0;
                _baseTileY = 0;
            }

            // 배열 건물 변환
            CacheTransformArray(_storageTransforms,    out _storageTileX,    out _storageTileY);
            CacheTransformArray(_firehouseTransforms,  out _firehouseTileX,  out _firehouseTileY);
            CacheTransformArray(_bedTransforms,        out _bedTileX,        out _bedTileY);
            CacheTransformArray(_healerTransforms,     out _healerTileX,     out _healerTileY);
            CacheTransformArray(_watchtowerTransforms, out _watchtowerTileX, out _watchtowerTileY);
        }

        // [PR Fix]: Warning-1 — RebuildVillagerList() 메서드 삭제.
        // Register/Unregister 시 List를 직접 동기화하므로 이 메서드가 불필요해졌다.
        // 기존 코드: private void RebuildVillagerList() { _villagerList.Clear(); foreach(...) ... }

        // [PR Fix]: Warning-1 — RebuildNodeList() 메서드 삭제.
        // AddResourceNode/RemoveResourceNode 시 List를 직접 동기화하므로 이 메서드가 불필요해졌다.
        // 기존 코드: private void RebuildNodeList() { _nodeList.Clear(); foreach(...) ... }

        #endregion

        #region ── Unity Editor 디버그 헬퍼 ──

#if UNITY_EDITOR
        /// <summary>
        /// 씬 뷰에서 각 건물의 감지 반경을 시각화한다.
        /// SensorSystem GameObject를 선택했을 때만 표시된다.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            // 기지 감지 반경 (노란색)
            if (_baseTransform != null)
            {
                UnityEditor.Handles.color = new Color(1f, 1f, 0f, 0.4f);
                UnityEditor.Handles.DrawWireDisc(_baseTransform.position, Vector3.up, _buildingSensingRadius);
                UnityEditor.Handles.Label(_baseTransform.position + Vector3.up * 2f, "Base");
            }

            // 창고 감지 반경 (파란색)
            DrawBuildingGizmos(_storageTransforms,    new Color(0f, 0.5f, 1f, 0.4f), "Storage");

            // 모닥불 감지 반경 (주황색)
            DrawBuildingGizmos(_firehouseTransforms,  new Color(1f, 0.5f, 0f, 0.4f), "Firehouse");

            // 침대 감지 반경 (보라색)
            DrawBuildingGizmos(_bedTransforms,        new Color(0.7f, 0f, 1f, 0.4f), "Bed");

            // 치료소 감지 반경 (녹색)
            DrawBuildingGizmos(_healerTransforms,     new Color(0f, 1f, 0.3f, 0.4f), "Healer");

            // 망루 감지 반경 (빨간색)
            DrawBuildingGizmos(_watchtowerTransforms, new Color(1f, 0f, 0f, 0.4f),   "Watchtower");
        }

        /// <summary>씬 뷰에서 건물 배열의 감지 반경 원을 그린다.</summary>
        private void DrawBuildingGizmos(Transform[] transforms, Color color, string label)
        {
            if (transforms == null) return;

            UnityEditor.Handles.color = color;
            foreach (Transform t in transforms)
            {
                if (t == null) continue;
                UnityEditor.Handles.DrawWireDisc(t.position, Vector3.up, _buildingSensingRadius);
                UnityEditor.Handles.Label(t.position + Vector3.up * 2f, label);
            }
        }
#endif

        #endregion
    }
}
