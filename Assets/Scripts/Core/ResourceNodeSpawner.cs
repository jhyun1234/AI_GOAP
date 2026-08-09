using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.Core
{
    /// <summary>
    /// 스폰된 노드를 받는 등록처. 舊 경로는 SensorSystem, M0 경로는 DiscoveryService가 구현한다.
    /// (M0 W3: SensorSystem 하드 의존을 주입 시임으로 대체 — W8에서 SensorSystem 폐기 시 필연 변경의 선행)
    /// </summary>
    public interface IResourceNodeSink
    {
        void AddResourceNode(ResourceNode node);
    }

    /// <summary>
    /// 게임 시작 시 ResourceNode를 맵에 랜덤 클러스터 배치하는 시스템.
    ///
    /// 역할:
    ///   1. ResourceNodeSpawnConfig 에셋에서 설정을 읽어 시드 기반 랜덤으로 클러스터 중심점 선정
    ///   2. 클러스터 주변에 노드를 배치하고 SensorSystem에 등록
    ///   3. 각 노드 위치에 ResourceNodeView GameObject를 생성 (프리팹 선택, 없으면 코드 생성)
    ///
    /// 호출 순서:
    ///   GameManager.Awake() → ResourceNodeSpawner.SpawnAll()
    ///   (별도 ExecutionOrder 불필요 — GameManager가 직접 호출)
    ///
    /// 하드코딩 없음 — 맵 크기·노드 수·거리는 전부 ResourceNodeSpawnConfig에서 관리한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResourceNodeSpawner : MonoBehaviour
    {
        [Tooltip("자원 노드 스폰 파라미터 전체를 담은 ScriptableObject. 반드시 연결할 것.")]
        [SerializeField] private ResourceNodeSpawnConfig _config;

        [Tooltip("ResourceNodeView 컴포넌트를 가진 커스텀 프리팹. " +
                 "비워두면 원형 마커를 코드로 자동 생성하므로 프리팹 없이도 동작한다.")]
        [SerializeField] private GameObject _nodeViewPrefab;

        // ── 상태 ─────────────────────────────────────────────────────────────────
        public bool HasSpawned { get; private set; } = false;

        // ── 내부 ─────────────────────────────────────────────────────────────────
        // 모든 자원 타입에 걸쳐 이미 배치된 타일 위치 — 겹침 방지용
        private readonly List<Vector2Int> _allPlacedPositions = new List<Vector2Int>();

        private System.Random _rng;
        private int _mapMinX, _mapMaxX, _mapMinY, _mapMaxY;

        // 프리팹이 없을 때 코드로 생성하는 공용 스프라이트 (SpawnAll마다 새로 생성, static 금지)
        private Sprite _fallbackSprite;

        // 현재 스폰의 노드 등록처 (SpawnAll 진입 시 설정)
        private IResourceNodeSink _sink;

        // ─────────────────────────────────────────────────────────────────────────
        // 공개 API
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 전체 자원 노드를 맵에 배치하고 SensorSystem에 등록한다.
        /// GameManager.Awake()의 Step 5에서 호출한다.
        /// </summary>
        /// <param name="baseTileX">기지 타일 X</param>
        /// <param name="baseTileY">기지 타일 Y</param>
        /// <param name="discoveryRadius">기지 반경 내 자동 발견 거리</param>
        /// <returns>성공 여부 (SensorSystem 또는 MapConfig 미준비 시 false)</returns>
        /// <summary>
        /// 노드 등록처를 주입받아 전체 자원 노드를 배치한다 (M0 DiscoveryService 등).
        /// 舊 SensorSystem 전용 시그니처는 W8에서 폐기됨.
        /// </summary>
        public bool SpawnAll(int baseTileX, int baseTileY, int discoveryRadius, IResourceNodeSink sink)
        {
            if (sink == null)
            {
                Debug.LogError("[ResourceNodeSpawner] sink가 null입니다. 노드 스폰 취소.");
                return false;
            }
            _sink = sink;

            if (_config == null)
            {
                Debug.LogError("[ResourceNodeSpawner] _config가 Inspector에 연결되지 않았습니다. " +
                               "Project 창에서 ResourceNodeSpawnConfig 에셋을 생성하고 슬롯에 드래그하세요.");
                return false;
            }
            if (MapConfig.Active == null)
            {
                Debug.LogError("[ResourceNodeSpawner] MapConfig.Active가 null입니다. " +
                               "GameManager.Awake()에서 MapConfig.SetActive()가 먼저 실행되어야 합니다.");
                return false;
            }

            // 맵 경계 계산 — MapConfig 수치 기반이므로 맵 크기 변경 시 자동 반영
            MapConfig map = MapConfig.Active;
            _mapMinX = -map.mapOffset;
            _mapMaxX =  map.mapSize - map.mapOffset - 1;
            _mapMinY = -map.mapOffset;
            _mapMaxY =  map.mapSize - map.mapOffset - 1;

            // RNG 초기화 — 0이면 매 실행 랜덤, 양수면 고정 시드로 동일 배치 재현 가능
            int seed = _config.randomSeed != 0
                ? _config.randomSeed
                : UnityEngine.Random.Range(1, int.MaxValue);
            _rng = new System.Random(seed);

            _allPlacedPositions.Clear();

            // 프리팹 없을 때 사용할 원형 스프라이트를 1회만 생성
            _fallbackSprite = (_nodeViewPrefab == null) ? CreateCircleSprite() : null;

            int totalSpawned = 0;
            if (_config.resourceTypes != null)
            {
                foreach (ResourceTypeSpawnData typeData in _config.resourceTypes)
                    totalSpawned += SpawnResourceType(typeData, baseTileX, baseTileY, discoveryRadius);
            }

            HasSpawned = true;
            Debug.Log($"[ResourceNodeSpawner] 스폰 완료 — 총 {totalSpawned}개 노드 | 시드={seed}");
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 자원 타입별 스폰
        // ─────────────────────────────────────────────────────────────────────────

        private int SpawnResourceType(
            ResourceTypeSpawnData typeData,
            int baseTileX, int baseTileY,
            int discoveryRadius)
        {
            // maxDistanceFromBase == 0 이면 맵 대각선 최대 맨해튼 거리를 상한으로 사용
            int effectiveMaxDist = typeData.maxDistanceFromBase > 0
                ? typeData.maxDistanceFromBase
                : (_mapMaxX - _mapMinX) + (_mapMaxY - _mapMinY);

            // ── Step 1: 클러스터 중심점 선정 ──────────────────────────────────────
            var centers = new List<Vector2Int>(typeData.clusterCount);
            for (int c = 0; c < typeData.clusterCount; c++)
            {
                bool found = false;
                for (int attempt = 0; attempt < _config.maxPlacementAttempts; attempt++)
                {
                    Vector2Int cand = RandomTileInBounds();
                    int dist = Manhattan(cand.x, cand.y, baseTileX, baseTileY);
                    if (dist < typeData.minDistanceFromBase || dist > effectiveMaxDist)
                        continue;

                    // 같은 자원 타입의 다른 클러스터와 최소 간격 확인
                    bool tooClose = false;
                    foreach (Vector2Int existing in centers)
                    {
                        if (Manhattan(cand.x, cand.y, existing.x, existing.y) < typeData.minClusterSpacing)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (tooClose) continue;

                    centers.Add(cand);
                    found = true;
                    break;
                }

                if (!found)
                    Debug.LogWarning($"[ResourceNodeSpawner] {typeData.resourceType} " +
                                     $"클러스터 중심 {c + 1}/{typeData.clusterCount} 배치 실패 " +
                                     $"(minDist={typeData.minDistanceFromBase}, spacing={typeData.minClusterSpacing})");
            }

            // ── Step 2: 클러스터마다 노드 배치 ───────────────────────────────────
            int remaining = typeData.nodeCount;
            int spawned   = 0;

            foreach (Vector2Int center in centers)
            {
                if (remaining <= 0) break;

                int perCluster = Mathf.Clamp(
                    _rng.Next(typeData.minNodesPerCluster, typeData.maxNodesPerCluster + 1),
                    0, remaining);

                for (int n = 0; n < perCluster; n++)
                {
                    bool placed = false;
                    for (int attempt = 0; attempt < _config.maxPlacementAttempts; attempt++)
                    {
                        int dx = _rng.Next(-typeData.clusterSpreadRadius, typeData.clusterSpreadRadius + 1);
                        int dy = _rng.Next(-typeData.clusterSpreadRadius, typeData.clusterSpreadRadius + 1);
                        int tx = Mathf.Clamp(center.x + dx, _mapMinX, _mapMaxX);
                        int ty = Mathf.Clamp(center.y + dy, _mapMinY, _mapMaxY);

                        // 기지 최소 거리 재확인 (클러스터 spread로 인해 초과할 수 있음)
                        if (Manhattan(tx, ty, baseTileX, baseTileY) < typeData.minDistanceFromBase)
                            continue;

                        // 이미 배치된 노드와 최소 간격 확인
                        if (!IsPositionFarEnough(tx, ty))
                            continue;

                        // 노드 등록
                        bool isDiscovered = Manhattan(tx, ty, baseTileX, baseTileY) <= discoveryRadius;
                        string nodeId = $"node_{typeData.resourceType}_{spawned}";
                        var node = new ResourceNode(nodeId, typeData.resourceType, tx, ty,
                                                    typeData.maxAmount, typeData.regenPerDay, isDiscovered);
                        _sink.AddResourceNode(node);

                        // 시각 마커 스폰
                        SpawnView(node, typeData);

                        _allPlacedPositions.Add(new Vector2Int(tx, ty));
                        spawned++;
                        remaining--;
                        placed = true;
                        break;
                    }

                    if (!placed)
                        Debug.LogWarning($"[ResourceNodeSpawner] {typeData.resourceType} " +
                                         $"노드 배치 실패 (남은={remaining})");
                }
            }

            Debug.Log($"[ResourceNodeSpawner] {typeData.resourceType}: {spawned}/{typeData.nodeCount}개 배치");
            return spawned;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 시각 마커 생성
        // ─────────────────────────────────────────────────────────────────────────

        private void SpawnView(ResourceNode node, ResourceTypeSpawnData typeData)
        {
            GameObject go;

            if (_nodeViewPrefab != null)
            {
                // 커스텀 프리팹 사용 — ResourceNodeView 컴포넌트가 포함되어 있어야 함
                go = Instantiate(_nodeViewPrefab);
            }
            else
            {
                // 프리팹 없음 → 코드로 최소 마커 생성
                // RequireComponent 덕분에 AddComponent<ResourceNodeView>() 시 SpriteRenderer 자동 추가
                go = new GameObject($"NodeView_{node.NodeId}");
            }

            go.name = $"NodeView_{node.ResourceType}_{node.TileX}_{node.TileY}";
            go.transform.position = new Vector3(node.TileX, node.TileY, 0f);
            go.transform.SetParent(transform, worldPositionStays: true);

            // ResourceNodeView 획득 (프리팹에 이미 있거나 없으면 AddComponent)
            var view = go.GetComponent<ResourceNodeView>();
            if (view == null) view = go.AddComponent<ResourceNodeView>();

            // 에셋 그림이 최우선 (2026-08-09) — 없을 때만 프리팹/코드 원 폴백 (중립 불변식).
            // 그림이 있으면 색 보간을 끈다: 실그림을 잔량 색으로 물들이면 아트가 죽는다.
            bool hasArt = typeData.nodeSprite != null;
            Sprite sprite = hasArt
                ? typeData.nodeSprite
                : (_fallbackSprite ?? (go.GetComponent<SpriteRenderer>()?.sprite));
            view.Init(node, typeData.nodeColor, typeData.depletedColor, typeData.nodeSize, sprite,
                      typeData.depletedSprite, typeData.depletedBelowRatio, tintByAmount: !hasArt);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 유틸리티
        // ─────────────────────────────────────────────────────────────────────────

        private bool IsPositionFarEnough(int tx, int ty)
        {
            foreach (Vector2Int pos in _allPlacedPositions)
            {
                if (Manhattan(tx, ty, pos.x, pos.y) < _config.nodeMinSpacing)
                    return false;
            }
            return true;
        }

        private Vector2Int RandomTileInBounds()
        {
            return new Vector2Int(
                _rng.Next(_mapMinX, _mapMaxX + 1),
                _rng.Next(_mapMinY, _mapMaxY + 1));
        }

        private static int Manhattan(int x1, int y1, int x2, int y2)
            => Mathf.Abs(x1 - x2) + Mathf.Abs(y1 - y2);

        /// <summary>
        /// 원형 안티앨리어싱 스프라이트를 런타임에 생성한다.
        /// _nodeViewPrefab이 없을 때만 SpawnAll()에서 1회 호출된다.
        /// </summary>
        private static Sprite CreateCircleSprite()
        {
            const int N   = 32;
            const float C = N * 0.5f;
            const float R = C - 1.5f;

            var tex = new Texture2D(N, N, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp
            };

            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float dx = x - C + 0.5f;
                    float dy = y - C + 0.5f;
                    float d  = Mathf.Sqrt(dx * dx + dy * dy);
                    // 경계 부근 1픽셀 알파 그라데이션 → 부드러운 원
                    float a  = Mathf.Clamp01(R - d + 1f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();

            // PPU = N → 1 타일(월드 1유닛) = 스프라이트 전체 크기
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), (float)N);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_config == null || MapConfig.Active == null) return;

            // 기지 위치 (씬 배치 시 자동 반영)
            Vector3 basePos = Vector3.zero; // GameManager의 기지 좌표를 직접 알 수 없으므로 원점 표시

            foreach (ResourceTypeSpawnData typeData in _config.resourceTypes)
            {
                UnityEditor.Handles.color = new Color(
                    typeData.nodeColor.r, typeData.nodeColor.g, typeData.nodeColor.b, 0.15f);

                // 최소 거리 구역 표시
                if (typeData.minDistanceFromBase > 0)
                {
                    UnityEditor.Handles.DrawWireDisc(basePos, Vector3.forward, typeData.minDistanceFromBase);
                }
            }
        }
#endif
    }
}
