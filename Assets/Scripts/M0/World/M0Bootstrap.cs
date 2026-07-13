using AIVillage.Core;
using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// M0 씬의 최소 부트스트랩 — 舊 GameManager 중앙 초기화를 대체하는 유일한 진입점.
    ///
    /// 하는 일은 MapConfig.SetActive() 하나다.
    /// MapChunkRenderer(Start)와 FowManager(Awake, ExecutionOrder -55)가
    /// MapConfig.Active를 전제하므로 그보다 먼저(-95) 실행돼야 한다.
    ///
    /// 자원 노드 스폰(ResourceNodeSpawner.SpawnAll)은 여기서 부르지 않는다 —
    /// SpawnAll이 폐기 대상인 舊 SensorSystem.Instance에 의존하기 때문.
    /// W3에서 DiscoveryService가 생기면 스폰 호출을 이 클래스에 추가한다.
    /// </summary>
    [DefaultExecutionOrder(-95)]
    [DisallowMultipleComponent]
    public sealed class M0Bootstrap : MonoBehaviour
    {
        [Tooltip("맵 크기·오프셋의 원본 SO. Assets/MapConfig.asset을 연결할 것.")]
        [SerializeField] private MapConfig _mapConfig;

        private void Awake()
        {
            if (_mapConfig == null)
            {
                Debug.LogError("[M0Bootstrap] _mapConfig가 Inspector에 연결되지 않았습니다. " +
                               "Assets/MapConfig.asset을 슬롯에 드래그하세요.");
                enabled = false;
                return;
            }

            MapConfig.SetActive(_mapConfig);
        }

        /// <summary>
        /// 舊 GameManager.Start()의 초기 FoW 공개를 이관.
        /// 기지 (0,0) 주변 initialRevealRadius(에셋 값, 기본 15타일)를 원형 공개한다.
        /// Start 시점에는 모든 Awake가 끝나 FowManager.Instance가 보장된다.
        /// </summary>
        private void Start()
        {
            if (FowManager.Instance == null)
            {
                Debug.LogError("[M0Bootstrap] FowManager가 씬에 없습니다. _FowManager 오브젝트를 확인하세요.");
                return;
            }

            FowManager.Instance.InitialReveal(0, 0, MapConfig.Active.initialRevealRadius);
        }
    }
}
