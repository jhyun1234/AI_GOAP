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
    ///   3. 각 노드 위치에 ResourceNodeView GameObject를 코드로 생성
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

        // ── 내부 ─────────────────────────────────────────────────────────────────
        // 모든 자원 타입에 걸쳐 이미 배치된 타일 위치 — 겹침 방지용
        private readonly List<Vector2Int> _allPlacedPositions = new List<Vector2Int>();

        private System.Random _rng;
        private int _mapMinX, _mapMaxX, _mapMinY, _mapMaxY;

        // 에셋 그림이 없는 종류가 쓰는 공용 원형 스프라이트 (SpawnAll마다 새로 생성, static 금지)
        private Sprite _fallbackSprite;

        // 현재 스폰의 노드 등록처 (SpawnAll 진입 시 설정)
        private IResourceNodeSink _sink;

        // 판 시드 덮어쓰기 (M26-1차 W2) — 0 = 안 씀 (에셋 randomSeed 규약 그대로).
        private int _seedOverride;

        // 이 칸에 노드가 설 수 있는가 (M26-2차 W1) — null이면 전부 허용 = 舊 동작.
        // 🔑 **리스폰(DiscoveryService.ConfigureRespawn)이 받는 것과 같은 판정**이다 (ADR-T2-1):
        //    판정이 둘이면 "리스폰은 피하는데 스폰은 서는" 지금의 결함이 다른 모양으로 재발한다.
        private System.Func<int, int, bool> _canHostNode;

        // 이 칸의 지형 (M26-2차 W2) — null이면 지형 조건을 무시한다 = 舊 동작.
        // 🔑 Core 는 M0 의 `TerrainService` 를 **모른다**: 알면 의존이 거꾸로 선다.
        //    그래서 M0 가 조회를 **밀어 넣는다** (W5 의 `TerrainColorSource` 와 같은 규약).
        private System.Func<int, int, AIVillage.M0.TerrainTypeSO> _terrainAt;

        // 팔레트 안의 최대 `NodeDensityMult` (M26-2차 W2) — 밀도를 **상대값**으로 읽기 위한 분모.
        // 🔑 확률로만 쓰면 1을 넘는 값이 전부 "항상 채택"이 되어 **숲 1.5가 평지 1.0과 같아진다.**
        //    최대값으로 나누면 숲:평지:늪 = 1.5:1.0:0.6 의 **비율**이 그대로 산다.
        //    분모를 아는 쪽은 팔레트를 가진 M0 다 — 그래서 여기로 넘어온다.
        private float _densityMax = 1f;

        /// <summary>판 시드를 주입한다 (M26-1차 W2, ADR-T-4) — **SpawnAll 전에** 부른다.
        /// 지형과 노드가 같은 시드에서 나오게 하는 유일한 통로다.</summary>
        public void OverrideSeed(int seed) => _seedOverride = seed;

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
        /// <param name="canHostNode">이 칸에 노드가 설 수 있는가 (M26-2차 W1).
        /// **null이면 전부 허용 = 舊 동작**(중립 불변식). 지형이 있는 판에서는 호출자가
        /// 리스폰과 **같은 식**을 넘긴다 (ADR-T2-1) — 안 넘기면 나무가 호수 위에 선다.</param>
        /// <param name="terrainAt">이 칸의 지형 (M26-2차 W2). **null이면 지형 조건 무시 = 舊 동작.**</param>
        /// <param name="densityMax">팔레트 최대 `NodeDensityMult` — 밀도를 상대값으로 읽는 분모.
        /// 1 이하면 밀도를 안 쓴다.</param>
        public bool SpawnAll(int baseTileX, int baseTileY, int discoveryRadius, IResourceNodeSink sink,
                             System.Func<int, int, bool> canHostNode = null,
                             System.Func<int, int, AIVillage.M0.TerrainTypeSO> terrainAt = null,
                             float densityMax = 1f)
        {
            if (sink == null)
            {
                Debug.LogError("[ResourceNodeSpawner] sink가 null입니다. 노드 스폰 취소.");
                return false;
            }
            _sink = sink;
            _canHostNode = canHostNode;
            _terrainAt = terrainAt;
            _densityMax = Mathf.Max(1f, densityMax);

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

            // RNG 초기화 — 우선순위: 호출자가 넘긴 판 시드 > 에셋 randomSeed > 매 실행 랜덤.
            // 🔑 M26-1차 W2 (ADR-T-4): 판 시드가 오면 **지형과 노드가 같은 시드**에서 나온다.
            //    그전까지 이 시드는 익명이라 같은 판을 다시 볼 수 없었다.
            int seed = _seedOverride != 0
                ? _seedOverride
                : (_config.randomSeed != 0 ? _config.randomSeed
                                           : UnityEngine.Random.Range(1, int.MaxValue));
            _rng = new System.Random(seed);

            _allPlacedPositions.Clear();

            // 에셋 그림이 없는 종류가 쓸 원형 스프라이트를 1회만 생성 (중립 불변식)
            _fallbackSprite = CreateCircleSprite();

            int totalSpawned = 0;
            if (_config.resourceTypes != null)
            {
                foreach (ResourceTypeSpawnData typeData in _config.resourceTypes)
                    totalSpawned += SpawnResourceType(typeData, baseTileX, baseTileY, discoveryRadius);
            }

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
            // ── Step 1: 클러스터 중심점 선정 ──────────────────────────────────────
            var centers = new List<Vector2Int>(typeData.clusterCount);
            for (int c = 0; c < typeData.clusterCount; c++)
            {
                bool found = false;
                int terrainRejects = 0;   // 진단용 — 물이 많은 판을 로그로 구분한다
                for (int attempt = 0; attempt < _config.maxPlacementAttempts; attempt++)
                {
                    Vector2Int cand = RandomTileInBounds();
                    if (Manhattan(cand.x, cand.y, baseTileX, baseTileY) < typeData.minDistanceFromBase)
                        continue;

                    // 설 수 없는 땅 (M26-2차 W1) — 중심이 물에 잠기면 클러스터가 통째로 잠긴다.
                    if (_canHostNode != null && !_canHostNode(cand.x, cand.y))
                    {
                        terrainRejects++;
                        continue;
                    }

                    // 지형 조건 (M26-2차 W2) — 늪 자원의 클러스터는 늪에서 시작해야 늪에 모인다.
                    if (!TerrainAccepts(typeData, cand.x, cand.y))
                    {
                        terrainRejects++;
                        continue;
                    }

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
                                     $"(minDist={typeData.minDistanceFromBase}, spacing={typeData.minClusterSpacing}" +
                                     $", 지형거절={terrainRejects}/{_config.maxPlacementAttempts})");
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
                    int terrainRejects = 0;   // 진단용 — 물 가장자리 클러스터를 로그로 구분한다
                    for (int attempt = 0; attempt < _config.maxPlacementAttempts; attempt++)
                    {
                        int dx = _rng.Next(-typeData.clusterSpreadRadius, typeData.clusterSpreadRadius + 1);
                        int dy = _rng.Next(-typeData.clusterSpreadRadius, typeData.clusterSpreadRadius + 1);
                        int tx = Mathf.Clamp(center.x + dx, _mapMinX, _mapMaxX);
                        int ty = Mathf.Clamp(center.y + dy, _mapMinY, _mapMaxY);

                        // 기지 최소 거리 재확인 (클러스터 spread로 인해 초과할 수 있음)
                        if (Manhattan(tx, ty, baseTileX, baseTileY) < typeData.minDistanceFromBase)
                            continue;

                        // 설 수 없는 땅 (M26-2차 W1) — 🔴 **중심만 걸러서는 안 된다**:
                        // clusterSpreadRadius로 흩어진 노드가 호수 가장자리에 빠진다.
                        if (_canHostNode != null && !_canHostNode(tx, ty))
                        {
                            terrainRejects++;
                            continue;
                        }

                        // 지형 조건 (M26-2차 W2) — 여기가 전수 검사(M26B_T2)가 보는 자리다.
                        // 중심이 늪이어도 spread 로 흩어진 노드는 늪 밖일 수 있다.
                        if (!TerrainAccepts(typeData, tx, ty))
                        {
                            terrainRejects++;
                            continue;
                        }

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
                                         $"노드 배치 실패 (남은={remaining}" +
                                         $", 지형거절={terrainRejects}/{_config.maxPlacementAttempts})");
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
            // RequireComponent 덕분에 AddComponent<ResourceNodeView>() 시 SpriteRenderer 자동 추가
            var go = new GameObject($"NodeView_{node.ResourceType}_{node.TileX}_{node.TileY}");
            go.transform.position = new Vector3(node.TileX, node.TileY, 0f);
            go.transform.SetParent(transform, worldPositionStays: true);

            var view = go.AddComponent<ResourceNodeView>();

            // 종류 변형 (2026-08-10) — 타일 좌표 해시로 고른다. **랜덤이 아니라 해시**인 이유:
            // 같은 자리는 늘 같은 나무여야 한다 (로드·재생성에 숲이 갈아엎어지면 지형 기억이 깨진다).
            int variant = PickVariantIndex(typeData.nodeSpriteVariants, node.TileX, node.TileY);

            // 에셋 그림이 최우선 (2026-08-09) — 없을 때만 프리팹/코드 원 폴백 (중립 불변식).
            // 그림이 있으면 색 보간을 끈다: 실그림을 잔량 색으로 물들이면 아트가 죽는다.
            Sprite full = variant >= 0 ? typeData.nodeSpriteVariants[variant] : typeData.nodeSprite;
            bool hasArt = full != null;
            Sprite sprite = hasArt ? full : _fallbackSprite;
            view.Init(node, typeData.nodeColor, typeData.depletedColor, typeData.nodeSize, sprite,
                      Variant(typeData.depletedSpriteVariants, variant, typeData.depletedSprite),
                      typeData.depletedBelowRatio, tintByAmount: !hasArt,
                      harvestParticle: Variant(typeData.harvestParticleVariants, variant,
                                               typeData.harvestParticle));
        }

        /// <summary>이 노드가 쓸 변형 번호 (없으면 −1 = 단일 그림). 좌표 해시라 결정적이다.
        ///
        /// 🔮 **여기가 지형·계절이 들어올 자리다** (사용자 방향 2026-08-10). 추운 지형은 어두운
        /// 나무, 봄은 밝은 초록, 가을은 노랑, 겨울은 수가 줄거나 어둡게 — 그때 이 함수만
        /// "좌표 해시"에서 "지형·계절이 고르는 표"로 바뀐다. 뷰도 러너도 판정도 어느 그림인지
        /// 모르므로 바깥은 그대로다. 계절이 바뀌면 다시 고르는 경로(재배선)도 이 함수를 지난다.</summary>
        private static int PickVariantIndex(Sprite[] variants, int tileX, int tileY)
        {
            if (variants == null || variants.Length == 0) return -1;
            float v = AIVillage.M0.StableHash.Value01($"{tileX}_{tileY}", "nodevariant");
            return Mathf.Clamp(Mathf.FloorToInt(v * variants.Length), 0, variants.Length - 1);
        }

        /// <summary>변형 배열에서 짝을 꺼낸다 — 배열이 짧으면 단일 값으로 폴백 (반쪽 배선 허용:
        /// 잎 그림이 없는 종류가 있어도 나무 변형은 살아 있어야 한다).</summary>
        private static Sprite Variant(Sprite[] arr, int index, Sprite fallback)
            => arr != null && index >= 0 && index < arr.Length && arr[index] != null ? arr[index] : fallback;

        // ─────────────────────────────────────────────────────────────────────────
        // 유틸리티
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>지형이 이 칸을 받아 주는가 (M26-2차 W2). `_terrainAt`가 null이면 언제나 참 = 舊 동작.
        ///
        /// 세 관문이다: ①설 수 있는 지형인가 ②인접해야 할 지형이 곁에 있는가 ③밀도 추첨을 통과했는가.
        /// 🔑 **①을 건 자원에는 ③을 걸지 않는다**: 설계자가 "여기에만 난다"고 이미 못박았는데
        ///    밀도까지 곱하면 늪 자원이 늪 밀도(0.6)로 **한 번 더** 깎여 의도의 절반만 남는다.
        /// </summary>
        private bool TerrainAccepts(ResourceTypeSpawnData d, int tx, int ty)
        {
            if (_terrainAt == null) return true;

            AIVillage.M0.TerrainTypeSO here = _terrainAt(tx, ty);

            bool hasAllowed = d.allowedTerrain != null && d.allowedTerrain.Length > 0;
            if (hasAllowed && System.Array.IndexOf(d.allowedTerrain, here) < 0) return false;

            if (d.adjacentTerrain != null && d.adjacentTerrain.Length > 0
                && !HasNeighborTerrain(d.adjacentTerrain, tx, ty)) return false;

            // 밀도 — 지정 지형이 있는 자원은 건너뛴다 (위 주석). 상대값이라 최대치는 항상 통과한다.
            if (!hasAllowed && here != null && _densityMax > 1f)
            {
                float p = Mathf.Clamp01(here.NodeDensityMult / _densityMax);
                if (p < 1f && _rng.NextDouble() >= p) return false;
            }
            return true;
        }

        /// <summary>이 자원 타입이 이 칸을 지형 조건상 받아들이는가 (M26-2차 W2 — **리스폰도 읽는다**).
        ///
        /// 🔴 왜 공개인가: 리스폰(`DiscoveryService.TryPickRelocation`)이 이걸 안 보면 은이 고갈 후
        ///    **평지로 걸어 나온다.** 스폰만 지형을 지키고 리스폰은 안 지키는 것이 정확히
        ///    `ADR-T2-1`이 막으려던 이원화다 (W1에서 통행에 대해 고친 것과 같은 병).
        /// ⚠️ **밀도는 여기 없다** — 밀도는 "얼마나 빽빽한가"라 배치 경쟁에만 뜻이 있고,
        ///    이미 있던 노드가 자리를 옮기는 데 추첨을 다시 돌릴 이유가 없다.
        /// ⚠️ **한 타입에 원소가 여럿이면**(RawFood = 기본 + 물가) 어느 원소가 낳았는지 노드가
        ///    기억하지 않는다. 그래서 **조건 없는 원소가 하나라도 있으면 그 타입은 자유**로 본다 —
        ///    없는 근거로 조이는 것보다 낫다 (은은 원소가 하나라 정확히 조인다).
        /// </summary>
        public bool TypeAcceptsTile(ResourceType type, int tx, int ty)
        {
            if (_terrainAt == null || _config?.resourceTypes == null) return true;

            bool sawConditioned = false;
            foreach (ResourceTypeSpawnData d in _config.resourceTypes)
            {
                if (d.resourceType != type || d.nodeCount <= 0) continue;

                bool hasAllowed  = d.allowedTerrain  != null && d.allowedTerrain.Length  > 0;
                bool hasAdjacent = d.adjacentTerrain != null && d.adjacentTerrain.Length > 0;
                if (!hasAllowed && !hasAdjacent) return true;   // 조건 없는 원소가 있다 = 자유

                sawConditioned = true;
                if (hasAllowed && System.Array.IndexOf(d.allowedTerrain, _terrainAt(tx, ty)) < 0) continue;
                if (hasAdjacent && !HasNeighborTerrain(d.adjacentTerrain, tx, ty)) continue;
                return true;   // 이 원소의 조건을 만족한다
            }
            return !sawConditioned;   // 조건부 원소만 있었는데 하나도 못 맞췄다
        }

        /// <summary>8방향 이웃 중 하나라도 이 지형인가 (M26-2차 W2 — "물가"의 뜻).</summary>
        private bool HasNeighborTerrain(AIVillage.M0.TerrainTypeSO[] wanted, int tx, int ty)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = tx + dx, ny = ty + dy;
                    if (nx < _mapMinX || nx > _mapMaxX || ny < _mapMinY || ny > _mapMaxY) continue;
                    if (System.Array.IndexOf(wanted, _terrainAt(nx, ny)) >= 0) return true;
                }
            return false;
        }

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
        /// 원형 안티앨리어싱 스프라이트를 런타임에 생성한다. SpawnAll()에서 1회 호출된다.
        /// 에셋 그림(nodeSprite)이 없는 종류의 폴백 — 중립 불변식.
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
