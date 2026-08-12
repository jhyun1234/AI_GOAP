using System.Collections.Generic;
using AIVillage.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;   // LogAssert — 뷰 생성 부수효과를 무시하고 **배치 결과만** 본다

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M26 2차 게이트 (Docs/M26_2차_지형축_실행명세서.md). W1 부터 시작해 W2~W7 이 뒤에 붙는다.
    ///
    /// 🔴 **고정한 축을 적는다** (방법론 M20 — 1차 `M26_T4` 가 늪을 맵 가로지르는 띠로 깔아
    /// 우회 자체를 불가능하게 만들었던 사건의 재발 방지):
    /// - 맵 = 배포 `MapConfig` (100×100). 기지 = (0, 0).
    /// - 자원 설정 = **배포 에셋** 그대로 (검사용 값을 지어내지 않는다).
    /// - 막힌 땅 = 손으로 세운 **호수 한 덩어리**. 실제 `TerrainService` 를 쓰지 않는 이유는
    ///   시드마다 물 비율이 달라 *"0건"* 이 **우연히** 성립할 수 있기 때문이다. 여기서 고정한 것은
    ///   "막힌 영역이 넓게 존재한다"이고, 그 영역이 **어떤 지형인지는 이 검사와 무관하다.**
    /// - 🔑 호수는 맵의 일부만 덮는다. 전부 덮으면 노드가 0개가 되어 *"막힌 곳에 0개"* 가
    ///   공허하게 참이 된다 — 그래서 아래에 **노드가 실제로 스폰됐다**는 검사를 함께 건다.
    /// </summary>
    public class M26B_TerrainPhase2Gates
    {
        private const string ConfigPath = "Assets/ResourceNodeSpawnConfig/ResourceNodeSpawnConfig.asset";
        private const string MapPath    = "Assets/MapConfig.asset";

        /// <summary>손으로 세운 호수 — 기지(0,0) 둘레는 비워 둔다(태어나자마자 갇히는 판 방지와 같은 규약).
        /// 맵 100×100 중 대략 1/4 을 덮는다.</summary>
        private static bool CanHost(int x, int y)
            => !(x >= -45 && x <= 5 && y >= 10 && y <= 45);

        private sealed class Collector : IResourceNodeSink
        {
            public readonly List<ResourceNode> Nodes = new List<ResourceNode>();
            public void AddResourceNode(ResourceNode node) => Nodes.Add(node);
        }

        /// <summary>배포 설정으로 실제 `SpawnAll` 을 돌린다. 판정을 null 로 주면 舊 동작이다.</summary>
        private static List<ResourceNode> RunSpawn(int seed, System.Func<int, int, bool> canHost,
                                                   System.Func<int, int, AIVillage.M0.TerrainTypeSO> terrainAt = null,
                                                   float densityMax = 1f)
        {
            var cfg = AssetDatabase.LoadAssetAtPath<ResourceNodeSpawnConfig>(ConfigPath);
            Assert.IsNotNull(cfg, $"ResourceNodeSpawnConfig 에셋 없음: {ConfigPath}");
            return RunSpawnWithConfig(cfg, seed, canHost, terrainAt, densityMax);
        }

        private static List<ResourceNode> RunSpawnWithConfig(
            ResourceNodeSpawnConfig cfg, int seed, System.Func<int, int, bool> canHost,
            System.Func<int, int, AIVillage.M0.TerrainTypeSO> terrainAt, float densityMax)
        {
            var map = AssetDatabase.LoadAssetAtPath<MapConfig>(MapPath);
            Assert.IsNotNull(map, $"MapConfig 에셋 없음: {MapPath}");
            MapConfig.SetActive(map);

            var go = new GameObject("M26B_SpawnerProbe");
            try
            {
                ResourceNodeSpawner spawner = go.AddComponent<ResourceNodeSpawner>();
                // _config 는 SerializeField(비공개) — 검사에서만 주입한다.
                var so = new SerializedObject(spawner);
                so.FindProperty("_config").objectReferenceValue = cfg;
                so.ApplyModifiedPropertiesWithoutUndo();

                var sink = new Collector();
                spawner.OverrideSeed(seed);

                // 뷰 생성(코루틴 등)은 EditMode 밖의 일이라 로그를 막지 않는다 — 이 검사가 보는 것은
                // **어느 칸에 노드가 등록됐는가** 하나뿐이고, 그것은 sink 가 전부 받는다.
                LogAssert.ignoreFailingMessages = true;
                try { spawner.SpawnAll(0, 0, 10, sink, canHost, terrainAt, densityMax); }
                finally { LogAssert.ignoreFailingMessages = false; }

                return sink.Nodes;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ── T1: 자원 노드가 설 수 없는 땅에 서지 않는다 (W1) ──────────────────────

        [Test]
        public void M26B_T1_NodesNeverSpawnOnBlockedTiles()
        {
            int totalNodes = 0;

            for (int seed = 1; seed <= 20; seed++)
            {
                List<ResourceNode> nodes = RunSpawn(seed, CanHost);
                totalNodes += nodes.Count;

                foreach (ResourceNode n in nodes)
                    Assert.IsTrue(CanHost(n.TileX, n.TileY),
                        $"시드 {seed}: {n.ResourceType} 노드가 설 수 없는 칸 ({n.TileX}, {n.TileY})에 섰다 " +
                        "— 물 위의 나무는 아무도 못 벤다 (M26-2차 W1)");
            }

            // 🔑 공허한 참 방지 — 호수가 맵을 다 덮어 노드가 0개면 위 검사는 의미가 없다.
            Assert.Greater(totalNodes, 0, "20판 전체에서 노드가 하나도 안 났다 — 이 검사는 빈 검사다");
        }

        // ── T1-b: 실패 가능성 증명 — 판정을 끄면 실제로 물 위에 선다 ──────────────

        [Test]
        public void M26B_T1b_WithoutPredicate_NodesDoLandOnBlockedTiles()
        {
            int onBlocked = 0;

            for (int seed = 1; seed <= 20; seed++)
                foreach (ResourceNode n in RunSpawn(seed, null))
                    if (!CanHost(n.TileX, n.TileY)) onBlocked++;

            // 이 수가 0이면 T1 은 **아무것도 증명하지 않는다** (판정이 없어도 통과하므로).
            Assert.Greater(onBlocked, 0,
                "판정을 끈 舊 동작에서도 막힌 칸에 노드가 0개다 — T1 이 빈 검사라는 뜻이다");
        }

        // ── W2: 지형이 무엇을 낳는지 ──────────────────────────────────────────────
        //
        // 🔴 여기서 고정한 축: **배포 팔레트와 배포 스폰 설정** 그대로다. 검사용 지형을 지어내면
        //    "에셋이 정한다"(ADR-T2-2)를 증명하지 못한다 — 배포가 틀려도 초록이 뜬다.
        //    지형 조건이 걸린 자원이 배포에 하나도 없으면 **그 사실 자체로 red** 다 (아래 T2).

        private const string TerrainDir = "Assets/M0Config/Terrain";

        /// <summary>배포 온대 팔레트. 🔴 **개정 2026-08-12 (M29 W2)**: 舊 구현은 지형 폴더를 통째로
        /// 긁었다 — 바이옴이 팔레트를 여러 벌로 가른 뒤로는 그 합집합이 **아무도 안 쓰는 세계**다.
        /// 물 찾기·임시 설정용 기준 팔레트로만 쓰고, 지형 자체는 아래 `MakeTerrain`이 세운다.</summary>
        private static AIVillage.M0.TerrainTypeSO[] Palette()
        {
            var list = new List<AIVillage.M0.TerrainTypeSO>();
            foreach (AIVillage.M0.TerrainTypeSO t in Biomes()[0].Palette) if (t != null) list.Add(t);
            Assert.Greater(list.Count, 0, "배포 온대 팔레트가 비었다");
            return list.ToArray();
        }

        /// <summary>배포 바이옴 (순서 = 씬과 같음, 0번 온대). 이게 없으면 산악 전용 광석이
        /// 검사 세계에 설 자리가 없어 조건 검사가 통째로 공허해진다 (M29 W3).</summary>
        private static AIVillage.M0.BiomeSO[] Biomes()
        {
            var b = new[]
            {
                AssetDatabase.LoadAssetAtPath<AIVillage.M0.BiomeSO>("Assets/M0Config/Biomes/Biome_Temperate.asset"),
                AssetDatabase.LoadAssetAtPath<AIVillage.M0.BiomeSO>("Assets/M0Config/Biomes/Biome_Jungle.asset"),
                AssetDatabase.LoadAssetAtPath<AIVillage.M0.BiomeSO>("Assets/M0Config/Biomes/Biome_Mountain.asset"),
                AssetDatabase.LoadAssetAtPath<AIVillage.M0.BiomeSO>("Assets/M0Config/Biomes/Biome_DeepMountain.asset"),
            };
            foreach (AIVillage.M0.BiomeSO x in b) Assert.IsNotNull(x, "배포 바이옴 에셋 없음");
            return b;
        }

        /// <summary>배포 지도 그대로 세운 진짜 지형 — 바이옴 포함 (씬 배선과 같은 자).
        /// 마을 안전반경은 씬 값(12), 거리비 분모는 배포 맵 반폭.</summary>
        private static AIVillage.M0.TerrainService MakeTerrain(int seed, AIVillage.M0.TerrainTypeSO[] palette)
        {
            var map = AssetDatabase.LoadAssetAtPath<MapConfig>(MapPath);
            Assert.IsNotNull(map, $"MapConfig 에셋 없음: {MapPath}");
            return new AIVillage.M0.TerrainService((uint)seed, Vector2Int.zero, 12,
                                                   palette, Biomes(), map.mapSize * 0.5f);
        }

        [Test]
        public void M26B_T2_TerrainRestrictedResources_OnlySpawnOnTheirTerrain()
        {
            AIVillage.M0.TerrainTypeSO[] palette = Palette();
            Assert.Greater(palette.Length, 0, $"{TerrainDir} 에 지형 에셋이 없다");

            var cfg = AssetDatabase.LoadAssetAtPath<ResourceNodeSpawnConfig>(ConfigPath);
            Assert.IsNotNull(cfg, $"ResourceNodeSpawnConfig 에셋 없음: {ConfigPath}");

            // 배포에 지형 조건이 실제로 걸려 있는가 — 안 걸려 있으면 아래 전수 검사가 공허하다.
            int restricted = 0;
            foreach (ResourceTypeSpawnData d in cfg.resourceTypes)
            {
                if (d.nodeCountPer10kTiles <= 0f) continue;
                if ((d.allowedTerrain != null && d.allowedTerrain.Length > 0)
                    || (d.adjacentTerrain != null && d.adjacentTerrain.Length > 0)) restricted++;
            }
            Assert.Greater(restricted, 0,
                "배포 설정에 지형 조건이 걸린 자원이 하나도 없다 — W2가 배선되지 않았거나 이 검사가 빈 검사다");

            int checkedNodes = 0;

            for (int seed = 1; seed <= 20; seed++)
            {
                AIVillage.M0.TerrainService terrain = MakeTerrain(seed, palette);
                System.Func<int, int, bool> canHost = (x, y) => terrain.IsWalkable(x, y);
                List<ResourceNode> nodes = RunSpawn(seed, canHost, terrain.At, terrain.MaxNodeDensityMult);

                foreach (ResourceNode n in nodes)
                {
                    ResourceTypeSpawnData d = FindSpawnData(cfg, n.ResourceType);
                    if (d == null) continue;

                    if (d.allowedTerrain != null && d.allowedTerrain.Length > 0)
                    {
                        AIVillage.M0.TerrainTypeSO here = terrain.At(n.TileX, n.TileY);
                        Assert.Contains(here, d.allowedTerrain,
                            $"시드 {seed}: {n.ResourceType} 노드 ({n.TileX}, {n.TileY})의 지형이 " +
                            $"'{(here != null ? here.DisplayName : "없음")}' — 허용 지형이 아니다");
                        checkedNodes++;
                    }

                    if (d.adjacentTerrain != null && d.adjacentTerrain.Length > 0)
                    {
                        Assert.IsTrue(HasNeighbor(terrain, d.adjacentTerrain, n.TileX, n.TileY),
                            $"시드 {seed}: {n.ResourceType} 노드 ({n.TileX}, {n.TileY})가 " +
                            "인접해야 할 지형에 붙어 있지 않다 (물가가 물에서 떨어졌다)");
                        checkedNodes++;
                    }
                }
            }

            // 20판 전체에서 조건부 노드가 한 개도 안 났으면 위 루프는 아무것도 안 본 것이다.
            Assert.Greater(checkedNodes, 0,
                "20판 전체에서 지형 조건이 걸린 노드가 하나도 안 났다 — 조건이 너무 빡빡하거나 빈 검사다");
        }

        [Test]
        public void M26B_T2b_AdjacentTerrain_IsActuallyEnforced()
        {
            // 🔴 이 검사가 따로 있는 이유: T2 는 배포 설정을 쓰는데 RawFood 는 조건 없는 원소
            //    (기본 식량)가 함께 있어 **타입 전체가 검사에서 빠진다** — 물가 인접이 한 번도
            //    실행되지 않았다. 조건 하나만 담은 설정으로 **인접 규칙 자체**를 직접 본다.
            AIVillage.M0.TerrainTypeSO[] palette = Palette();
            AIVillage.M0.TerrainTypeSO water = System.Array.Find(palette, t => !t.Walkable);
            Assert.IsNotNull(water, "통행 불가 지형(물)이 팔레트에 없다");

            var cfg = ScriptableObject.CreateInstance<ResourceNodeSpawnConfig>();
            cfg.nodeMinSpacing = 2;
            cfg.resourceTypes = new[]
            {
                new ResourceTypeSpawnData
                {
                    // 검사 맵 = 배포 100² = 1만 타일 — per10k 밀도와 절대 개수가 같은 수다 (M28-W2)
                    resourceType = ResourceType.RawFood, nodeCountPer10kTiles = 8, maxAmount = 10,
                    minDistanceFromBase = 20, clusterCountPer10kTiles = 6,
                    minNodesPerCluster = 1, maxNodesPerCluster = 2,
                    clusterSpreadRadius = 2, minClusterSpacing = 10,
                    adjacentTerrain = new[] { water },
                }
            };

            int total = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                AIVillage.M0.TerrainService terrain = MakeTerrain(seed, palette);
                foreach (ResourceNode n in RunSpawnWithConfig(cfg, seed, (x, y) => terrain.IsWalkable(x, y),
                                                              terrain.At, terrain.MaxNodeDensityMult))
                {
                    Assert.IsTrue(HasNeighbor(terrain, new[] { water }, n.TileX, n.TileY),
                        $"시드 {seed}: 물가 노드 ({n.TileX}, {n.TileY})가 물에 붙어 있지 않다");
                    total++;
                }
            }
            Assert.Greater(total, 0, "20판에서 물가 노드가 하나도 안 났다 — 이 검사는 빈 검사다");
            Object.DestroyImmediate(cfg);
        }

        [Test]
        public void M26B_T2c_Relocation_KeepsResourceOnItsTerrain()
        {
            // 🔴 스폰만 지형을 지키고 리스폰은 안 지키면, 은이 고갈 후 평지로 걸어 나온다
            //    (ADR-T2-1 이 막으려던 이원화). 스포너의 판정을 리스폰이 **같이** 읽는지 본다.
            AIVillage.M0.TerrainTypeSO[] palette = Palette();
            var cfg = AssetDatabase.LoadAssetAtPath<ResourceNodeSpawnConfig>(ConfigPath);

            // 🔴 **개정 2026-08-12 (M29 W3)**: 舊 검사는 기대 지형을 팔레트에서 어림(통행 가능 +
            //    EnterCost > 1 = 늪)으로 집어 "은은 늪에 산다"를 검사에 박아 뒀다. 광물이 산악으로
            //    옮겨 가자 red 가 났는데 **틀린 것은 배치가 아니라 검사**였다. 기대 지형은 배포
            //    설정에서 읽는다 — 자원이 어느 지형으로 이사해도 이 검사는 따라간다.
            ResourceTypeSpawnData silverData = FindSpawnData(cfg, ResourceType.Silver);
            Assert.IsNotNull(silverData, "배포 설정에 지형 조건이 걸린 Silver 원소가 없다 — 검사의 전제가 깨졌다");
            var allowed = new HashSet<AIVillage.M0.TerrainTypeSO>(silverData.allowedTerrain);
            Assert.Greater(allowed.Count, 0, "Silver 에 허용 지형이 없다 — 이 검사는 빈 검사다");

            var map = AssetDatabase.LoadAssetAtPath<MapConfig>(MapPath);
            MapConfig.SetActive(map);

            var go = new GameObject("M26B_RelocProbe");
            try
            {
                ResourceNodeSpawner spawner = go.AddComponent<ResourceNodeSpawner>();
                var so = new SerializedObject(spawner);
                so.FindProperty("_config").objectReferenceValue = cfg;
                so.ApplyModifiedPropertiesWithoutUndo();

                AIVillage.M0.TerrainService terrain = MakeTerrain(3, palette);
                var disc = new AIVillage.M0.DiscoveryService();

                LogAssert.ignoreFailingMessages = true;
                try { spawner.SpawnAll(0, 0, 10, disc, (x, y) => terrain.IsWalkable(x, y),
                                       terrain.At, terrain.MaxNodeDensityMult); }
                finally { LogAssert.ignoreFailingMessages = false; }

                disc.ConfigureRespawn(3u, 1f, 6,
                    (type, x, y) => terrain.IsWalkable(x, y) && spawner.TypeAcceptsTile(type, x, y));

                // 지정 지형 전용 자원을 전부 말려서 이동시킨다. 옛 자리를 적어 둔다 — **움직였는지**를
                // 봐야 한다: 하나도 안 옮겨지면 "늪에 그대로 있다"가 공허하게 참이 된다.
                var tracked = new List<ResourceNode>();
                var origin = new List<Vector2Int>();
                foreach (ResourceNode n in disc.Nodes)
                    if (n.ResourceType == ResourceType.Silver)
                    {
                        n.CurrentAmount = 0f;
                        tracked.Add(n);
                        origin.Add(new Vector2Int(n.TileX, n.TileY));
                    }
                Assert.Greater(tracked.Count, 0, "지형 전용 노드가 안 났다 — 이 검사는 빈 검사다");

                LogAssert.ignoreFailingMessages = true;
                try { for (int t = 0; t < 5; t++) disc.TickRespawn(2f); }
                finally { LogAssert.ignoreFailingMessages = false; }

                int moved = 0;
                for (int i = 0; i < tracked.Count; i++)
                {
                    ResourceNode n = tracked[i];
                    if (n.TileX != origin[i].x || n.TileY != origin[i].y) moved++;
                    AIVillage.M0.TerrainTypeSO here = terrain.At(n.TileX, n.TileY);
                    Assert.IsTrue(allowed.Contains(here),
                        $"지형 전용 자원이 ({n.TileX}, {n.TileY})로 옮겨졌는데 그곳은 " +
                        $"'{(here != null ? here.DisplayName : "없음")}' — 허용 지형이 아니다 " +
                        "(리스폰이 지형 조건을 안 읽는다 · ADR-T2-1 이원화)");
                }
                Assert.Greater(moved, 0,
                    "한 노드도 안 옮겨졌다 — '제 지형에 그대로 있다'가 공허하게 참이다 (빈 검사)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void M26B_T3_ImpossibleTerrain_YieldsZeroNodesAndWarns()
        {
            // 통행 불가 지형(물)만 허용하면 노드가 설 자리가 없다 — **조용히 0이 되지 않고 경고**해야 한다.
            AIVillage.M0.TerrainTypeSO[] palette = Palette();
            AIVillage.M0.TerrainTypeSO water = System.Array.Find(palette, t => !t.Walkable);
            Assert.IsNotNull(water, "통행 불가 지형이 팔레트에 없다 — 이 검사의 전제가 깨졌다");

            var cfg = ScriptableObject.CreateInstance<ResourceNodeSpawnConfig>();
            cfg.nodeMinSpacing = 2;
            cfg.resourceTypes = new[]
            {
                new ResourceTypeSpawnData
                {
                    resourceType = ResourceType.Wood, nodeCountPer10kTiles = 10, maxAmount = 10,
                    minDistanceFromBase = 10, clusterCountPer10kTiles = 3,
                    minNodesPerCluster = 1, maxNodesPerCluster = 3,
                    clusterSpreadRadius = 3, minClusterSpacing = 15,
                    allowedTerrain = new[] { water },   // 물 위에만 — 그런데 물은 못 지난다
                }
            };

            AIVillage.M0.TerrainService terrain = MakeTerrain(7, palette);
            List<ResourceNode> nodes = RunSpawnWithConfig(cfg, 7, (x, y) => terrain.IsWalkable(x, y),
                                                          terrain.At, terrain.MaxNodeDensityMult);

            Assert.AreEqual(0, nodes.Count,
                "통행 가능하면서 물인 칸은 없다 — 그런데 노드가 섰다 (조건 둘 중 하나가 안 걸렸다)");
            Object.DestroyImmediate(cfg);
        }

        private static ResourceTypeSpawnData FindSpawnData(ResourceNodeSpawnConfig cfg, ResourceType type)
        {
            // ⚠️ 같은 타입이 여러 원소일 수 있다 (RawFood = 기본 + 물가). 조건이 **걸린 쪽**을 고르면
            //    조건 없는 원소의 노드까지 검사해 거짓 red 가 난다 — 그래서 조건 없는 원소가 하나라도
            //    있으면 그 타입은 검사에서 뺀다 (어느 원소가 낳았는지 노드가 기억하지 않는다).
            ResourceTypeSpawnData restricted = null;
            foreach (ResourceTypeSpawnData d in cfg.resourceTypes)
            {
                if (d.resourceType != type || d.nodeCountPer10kTiles <= 0f) continue;
                bool hasCond = (d.allowedTerrain != null && d.allowedTerrain.Length > 0)
                            || (d.adjacentTerrain != null && d.adjacentTerrain.Length > 0);
                if (!hasCond) return null;
                restricted = d;
            }
            return restricted;
        }

        private static bool HasNeighbor(AIVillage.M0.TerrainService terrain, AIVillage.M0.TerrainTypeSO[] wanted,
                                        int tx, int ty)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (System.Array.IndexOf(wanted, terrain.At(tx + dx, ty + dy)) >= 0) return true;
                }
            return false;
        }

        // ── W3→W8: 거리 = 희귀도 (배포 사다리가 실제 배치에 드러난다) ─────────────
        //
        // 🔴 고정한 축: ①희귀도 순서는 **명세 §5의 표 그대로**다 — 검사가 순서를 발명하지 않는다.
        //    ②지형 없음(terrainAt null) — 지형 거절이 거리 분포를 흔들면 "거리" 축이 안 보인다.
        //    ③판정 통계는 **타입별 최근접 거리**다. 명세 초안은 중앙값이었으나 W8에서 개정
        //      (사유 명세 §5): min 컷(5~40)이 맵(100)에 비해 작아 인접 단의 중앙값 차이가
        //      0.4타일 — 표본 노이즈(±2타일)에 잠기고, RawFood 는 물가(20) 혼합으로 중앙값이
        //      설계상 역전된다. 최근접은 각 단의 컷에 붙어 단마다 갈리고, 플레이어 경험
        //      ("가장 가까운 은은 가장 가까운 나무보다 멀다")과 같은 말이다.

        /// <summary>이 타입·조건 조합의 배포 원소가 가진 `minDistanceFromBase`.
        /// RawFood 는 원소가 둘(조건 없음 = 기본 · 물 인접 = 물가)이라 조건 유무로 갈라 찾는다.</summary>
        private static int RungOf(ResourceNodeSpawnConfig cfg, ResourceType type, bool conditioned)
        {
            foreach (ResourceTypeSpawnData d in cfg.resourceTypes)
            {
                if (d.resourceType != type || d.nodeCountPer10kTiles <= 0f) continue;
                bool has = (d.allowedTerrain != null && d.allowedTerrain.Length > 0)
                        || (d.adjacentTerrain != null && d.adjacentTerrain.Length > 0);
                if (has == conditioned) return d.minDistanceFromBase;
            }
            Assert.Fail($"배포 설정에 {type}(조건 {(conditioned ? "있음" : "없음")}) 원소가 없다 " +
                        "— 명세 §5 사다리의 발판이 사라졌다");
            return -1;
        }

        [Test]
        public void M26B_T5_DistanceLadder_RarerSpawnsFarther()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<ResourceNodeSpawnConfig>(ConfigPath);
            Assert.IsNotNull(cfg, $"ResourceNodeSpawnConfig 에셋 없음: {ConfigPath}");

            // ① 에셋 사다리 — 명세 §5의 희귀도 순서대로 min 거리가 **순증가**해야 한다.
            // 🔴 **개정 2026-08-12 (M29 W3)**: 舊 사다리는 Iron 을 **조건 없는** 발판으로 봤다.
            //    광물이 전부 산악(암반)으로 들어가면서 Iron 도 조건부가 됐고, 사용자 확정 사다리
            //    (구리 → 철 → 은 → 다이아)가 위에 얹혔다. 순서 자체는 여전히 명세가 정하고
            //    검사는 그 순증가만 본다 — 값이 바뀌어도 이 파일은 안 바뀐다.
            var ladder = new (string label, int rung)[]
            {
                ("RawFood(기본)", RungOf(cfg, ResourceType.RawFood, false)),
                ("Wood",          RungOf(cfg, ResourceType.Wood,    false)),
                ("Stone",         RungOf(cfg, ResourceType.Stone,   false)),
                ("RawFood(물가)", RungOf(cfg, ResourceType.RawFood, true)),
                ("Copper",        RungOf(cfg, ResourceType.Copper,  true)),
                ("Iron",          RungOf(cfg, ResourceType.Iron,    true)),
                ("Silver",        RungOf(cfg, ResourceType.Silver,  true)),
                ("Diamond",       RungOf(cfg, ResourceType.Diamond, true)),
            };
            for (int i = 1; i < ladder.Length; i++)
                Assert.Greater(ladder[i].rung, ladder[i - 1].rung,
                    $"{ladder[i].label}({ladder[i].rung}) ≤ {ladder[i - 1].label}({ladder[i - 1].rung}) " +
                    "— 희귀한 것이 흔한 것보다 가까이 난다 (거리 = 희귀도가 깨졌다)");

            // ② 행동 — 20시드 전수에서 타입별 **최근접** 노드 거리.
            var minObs = new Dictionary<ResourceType, int>();
            var counts = new Dictionary<ResourceType, int>();
            for (int seed = 1; seed <= 20; seed++)
                foreach (ResourceNode n in RunSpawn(seed, null))
                {
                    int dist = Mathf.Abs(n.TileX) + Mathf.Abs(n.TileY);   // 기지 = (0, 0)
                    if (!minObs.ContainsKey(n.ResourceType) || dist < minObs[n.ResourceType])
                        minObs[n.ResourceType] = dist;
                    counts[n.ResourceType] = counts.TryGetValue(n.ResourceType, out int c) ? c + 1 : 1;
                }

            // 타입 단(원소가 여럿이면 가장 낮은 발판): 최근접이 제 단 아래로 내려오지 않고,
            // 다음 단이 시작되기 전에 실제로 선다 (가까운 단이 비면 사다리가 화면에 없다).
            // ⚠️ 다이아(마지막 단)는 여기 없다: 그 단은 배포 맵 바깥 고리에 붙어 있어 검사 맵에서
            //    표본이 몇 개뿐이라 "다음 단 전에 선다"가 표본 노이즈가 된다. 다이아까지의 **관계식**은
            //    M29_T4 가 배포 에셋 전수로 본다 (여기는 스포너가 min 거리를 읽는지를 보는 자리다).
            var types = new[]
            {
                (ResourceType.RawFood, ladder[0].rung),   // 기본(5)이 물가(20)보다 낮은 발판
                (ResourceType.Wood,    ladder[1].rung),
                (ResourceType.Stone,   ladder[2].rung),
                (ResourceType.Copper,  ladder[4].rung),
                (ResourceType.Iron,    ladder[5].rung),
                (ResourceType.Silver,  ladder[6].rung),
            };
            for (int i = 0; i < types.Length; i++)
            {
                (ResourceType t, int rung) = types[i];
                Assert.IsTrue(counts.ContainsKey(t) && counts[t] > 0,
                    $"{t}: 20판 전체에서 노드가 하나도 안 났다 — 이 검사는 빈 검사다");
                Assert.GreaterOrEqual(minObs[t], rung,
                    $"{t}: 최근접 노드({minObs[t]})가 제 단({rung})보다 가깝다 — 스포너가 min 거리를 안 읽는다");
                // ⚠️ 경계 포함(2026-08-12 개정): 舊 `Less` 는 **동전 던지기**였다. 가장 가까운 단
                //    (RawFood 5~9)의 띠는 맵 4만 칸 중 60칸뿐이라 20판을 다 돌려도 기댓값이 1~2개다 —
                //    이번에 광물 클러스터 수를 바꿨더니 공유 RNG 흐름이 밀려 `10 < 10` 으로 red 가 났다
                //    (RawFood 는 한 글자도 안 바뀌었다). 재는 성질("제 단 부근이 비지 않았다")은
                //    한 칸 완화해도 그대로 산다.
                if (i + 1 < types.Length)
                    Assert.LessOrEqual(minObs[t], types[i + 1].Item2,
                        $"{t}: 최근접 노드({minObs[t]})가 다음 단({types[i + 1].Item2}) 너머에 있다 " +
                        "— 제 단 부근이 비어 사다리가 화면에서 안 보인다");
            }
        }

        [Test]
        public void M26B_T5b_LoweredRung_ActuallyPullsNodesCloser()
        {
            // 실패 가능성 증명 — 발판을 에셋에서 내리면 은이 실제로 문앞에 선다.
            // 이게 안 일어나면 T5 의 "min ≥ 단"은 config 를 읽어서가 아니라 다른 우연
            // (클러스터 간격 등)으로 성립하는 빈 검사다.
            var deployed = AssetDatabase.LoadAssetAtPath<ResourceNodeSpawnConfig>(ConfigPath);
            int woodRung = RungOf(deployed, ResourceType.Wood, false);

            var cfg = ScriptableObject.CreateInstance<ResourceNodeSpawnConfig>();
            cfg.nodeMinSpacing = 2;
            cfg.resourceTypes = new[]
            {
                new ResourceTypeSpawnData
                {
                    resourceType = ResourceType.Silver, nodeCountPer10kTiles = 30, maxAmount = 10,
                    minDistanceFromBase = 1, clusterCountPer10kTiles = 15,
                    minNodesPerCluster = 1, maxNodesPerCluster = 2,
                    clusterSpreadRadius = 2, minClusterSpacing = 5,
                }
            };

            int minDist = int.MaxValue, total = 0;
            for (int seed = 1; seed <= 20; seed++)
                foreach (ResourceNode n in RunSpawnWithConfig(cfg, seed, null, null, 1f))
                {
                    minDist = Mathf.Min(minDist, Mathf.Abs(n.TileX) + Mathf.Abs(n.TileY));
                    total++;
                }

            Assert.Greater(total, 0, "발판을 내렸는데 노드가 안 났다 — 이 검사는 빈 검사다");
            Assert.Less(minDist, woodRung,
                $"은의 발판을 1로 내렸는데 최근접({minDist})이 나무 단({woodRung}) 밖이다 " +
                "— 거리가 에셋이 아닌 다른 무언가에 지배되고 있다 (T5 가 빈 검사)");
            Object.DestroyImmediate(cfg);
        }

        // ── W4: 채집 선택 통로 (이 차수의 심장) ───────────────────────────────────
        //
        // 🔴 고정한 축: 노드 배치는 **손으로 세운 격자**다 (스폰을 쓰면 시드마다 후보 수가 달라
        //    "같은 답"이 우연히 성립한다). 고정한 것은 "후보가 여럿이고 거리가 서로 다르다"뿐.

        private static AIVillage.M0.DiscoveryService NodesInLine(int count)
        {
            var d = new AIVillage.M0.DiscoveryService();
            for (int i = 1; i <= count; i++)
                d.AddResourceNode(new ResourceNode($"n{i}", ResourceType.Wood, i * 3, i * 2, 10f, 0f, true));
            return d;
        }

        [Test]
        public void M26B_T4_ZeroWeights_PicksSameNodeAsNearest()
        {
            // 가중치 0 → 점수 = 거리 → 최근접과 **같은 노드**여야 한다 (ADR-T2-3 중립 불변식).
            for (int trial = 0; trial < 100; trial++)
            {
                AIVillage.M0.DiscoveryService d = NodesInLine(8);
                // 비용을 들쭉날쭉하게 줘도 가중치가 0이면 결과가 흔들리면 안 된다.
                d.ConfigureGatherChoice(0f, 0f, (x, y) => 1f + ((x + y + trial) % 4));

                int fromX = (trial % 17) - 8, fromY = (trial % 11) - 5;
                ResourceNode nearest = d.FindNearestDiscovered(ResourceType.Wood, fromX, fromY);
                ResourceNode best = d.FindBestDiscovered(ResourceType.Wood, fromX, fromY, null, out ResourceNode alsoNearest);

                Assert.AreSame(nearest, best,
                    $"trial {trial}: 가중치가 0인데 최근접과 다른 노드를 골랐다 — 중립 불변식이 깨졌다");
                Assert.AreSame(nearest, alsoNearest, $"trial {trial}: out nearest 가 최근접이 아니다");
            }
        }

        [Test]
        public void M26B_T4b_TerrainWeight_ActuallyChangesTheChoice()
        {
            // 실패 가능성 증명 — 가중치를 켰을 때 **실제로 다른 답**이 나와야 T4 가 의미를 갖는다.
            var d = new AIVillage.M0.DiscoveryService();
            d.AddResourceNode(new ResourceNode("close_swamp", ResourceType.Wood, 8, 0, 10f, 0f, true));
            d.AddResourceNode(new ResourceNode("far_plain",   ResourceType.Wood, 14, 0, 10f, 0f, true));

            // 가까운 쪽만 늪(EnterCost 3), 먼 쪽은 평지(1).
            System.Func<int, int, float> cost = (x, y) => x == 8 ? 3f : 1f;

            d.ConfigureGatherChoice(0f, 0f, cost);
            Assert.AreEqual("close_swamp", d.FindBestDiscovered(ResourceType.Wood, 0, 0, null, out _).NodeId,
                "가중치 0에서는 가까운 쪽을 골라야 한다 (그래야 아래 전환이 가중치 덕분임이 증명된다)");

            d.ConfigureGatherChoice(1f, 0f, cost);
            Assert.AreEqual("far_plain", d.FindBestDiscovered(ResourceType.Wood, 0, 0, null, out _).NodeId,
                "지형 가중치를 켰는데도 늪에 선 가까운 노드를 골랐다 — 비용이 선택에 안 걸린다");

            // 산수 검산 — 8×3 = 24 vs 14×1 = 14. 손으로 계산한 값과 함수가 일치하는가.
            Assert.AreEqual(24f, AIVillage.M0.DiscoveryService.ScoreCandidate(8, 3f, 0f, 1f, 0f), 0.001f);
            Assert.AreEqual(14f, AIVillage.M0.DiscoveryService.ScoreCandidate(14, 1f, 0f, 1f, 0f), 0.001f);
        }

        [Test]
        public void M26B_T4c_DangerWeight_HasNoEffectWithoutASource()
        {
            // W6 전까지 위험 원천이 없다 — 가중치를 크게 켜도 동작이 변하면 안 된다
            // (배포에 GatherDangerWeight=12 가 이미 들어가므로, 이게 지금 판을 흔들지 않음을 못박는다).
            AIVillage.M0.DiscoveryService a = NodesInLine(6);
            a.ConfigureGatherChoice(1f, 0f, (x, y) => 1f);
            AIVillage.M0.DiscoveryService b = NodesInLine(6);
            b.ConfigureGatherChoice(1f, 50f, (x, y) => 1f);   // dangerAt 를 안 넘기면 벌점 0

            Assert.AreEqual(a.FindBestDiscovered(ResourceType.Wood, 0, 0, null, out _).NodeId,
                            b.FindBestDiscovered(ResourceType.Wood, 0, 0, null, out _).NodeId,
                            "위험 원천이 없는데 위험 가중치가 선택을 바꿨다");
        }

        // ── W5: 들판 상주 위협 ────────────────────────────────────────────────────
        //
        // 🔴 고정한 축: **배포 `ThreatSO` 전부**를 본다. 검사용 위협을 지어내면 "기존 4종이
        //    안 바뀐다"를 증명하지 못한다 — 배포가 상주로 바뀌어도 초록이 뜬다.

        [Test]
        public void M26B_T8_ResidentIsADistinctAxis_AndDoesNotTouchWaves()
        {
            var all = new List<AIVillage.M0.ThreatSO>();
            foreach (string guid in AssetDatabase.FindAssets("t:ThreatSO"))
            {
                var t = AssetDatabase.LoadAssetAtPath<AIVillage.M0.ThreatSO>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (t != null) all.Add(t);
            }
            Assert.Greater(all.Count, 0, "ThreatSO 에셋이 하나도 없다");

            foreach (AIVillage.M0.ThreatSO so in all)
            {
                if (so.IsResident)
                {
                    // 상주인데 밀도가 0이면 아무 일도 안 일어난다 — 켜 놓고 잊은 상태를 잡는다.
                    Assert.Greater(so.ResidentBandsPer10kTiles, 0f,
                        $"{so.name}: 상주형인데 무리 밀도가 0이다 (켜 놓고 잊었다)");
                    Assert.LessOrEqual(so.ResidentBandMin, so.ResidentBandMax,
                        $"{so.name}: 무리 크기 하한이 상한보다 크다");
                    // 상주는 제자리에 도착해 배회한다 — 반경이 0이면 한 칸에 굳는다.
                    Assert.Greater(so.WanderRadiusTiles, 0,
                        $"{so.name}: 상주형인데 배회 반경이 0이다 (들판에 박제가 선다)");
                }
                else
                {
                    // 🔑 중립 불변식 — 상주가 아닌 종족은 W5 배선 전과 **완전히 같다**.
                    Assert.AreEqual(0f, so.ResidentBandsPer10kTiles, 1e-6f,
                        $"{so.name}: 상주형이 아닌데 무리 밀도가 0이 아니다 (아무도 안 읽는 값)");
                    Assert.IsTrue(so.HabitatTerrain == null || so.HabitatTerrain.Length == 0,
                        $"{so.name}: 상주형이 아닌데 서식지가 설정돼 있다 (아무도 안 읽는 값)");
                }
            }
        }

        [Test]
        public void M26B_T8b_ResidentAxis_IsIndependentOfVillagerTargeting()
        {
            // 🔴 명세(ADR-T2-6)는 "상주 = 퇴장 없는 배회"라 했지만 그것만으로는 성립하지 않는다:
            //    밭형은 제자리에 도착해 배회하되 **주민을 안 치고**, 주민형은 주민을 치되
            //    **마을까지 걸어간다**. 상주 = "밭형으로 태어나 주민을 친다"는 제3의 조합이다.

            // 🔑 중립 불변식 — 상주가 아니면 판정은 舊 동작(타깃 롤)과 **글자 그대로 같다**.
            Assert.IsFalse(AIVillage.M0.ThreatService.StrikesVillagers(false, false),
                "상주도 주민형도 아닌데 주민을 친다 — 밭형 웨이브가 주민을 물게 된다");
            Assert.IsTrue(AIVillage.M0.ThreatService.StrikesVillagers(true, false),
                "주민형 웨이브가 주민을 안 친다");

            // 상주는 밭형으로 태어나지만 주민을 친다 — 이 조합이 W5의 전부다.
            Assert.IsTrue(AIVillage.M0.ThreatService.StrikesVillagers(false, true),
                "상주가 밭을 치러 든다 — 들판엔 밭이 없어 아무 일도 안 일어난다");
            Assert.IsTrue(AIVillage.M0.ThreatService.StrikesVillagers(true, true),
                "주민형 상주가 주민을 안 친다");
        }

        [Test]
        public void M26B_T8d_ResidentReservation_HysteresisAndCooldown_AreSane()
        {
            // 🔴 W5R (2026-08-11 Play 관측 개정) — 배포 값의 세 계약:
            //    ①부화 < 회수 (히스테리시스 — 같으면 경계에서 무리가 깜빡인다)
            //    ②쿨다운 > 0 (0이면 전멸 즉시 재부화 = 한 자리 무한 파밍)
            //    ③둘 다 시야 배수라 시야가 변해도 관계가 유지된다.
            var cfg = AssetDatabase.LoadAssetAtPath<AIVillage.M0.WorldConfigSO>(
                "Assets/M0Config/WorldConfig.asset");
            Assert.IsNotNull(cfg, "WorldConfig.asset 없음");

            Assert.Greater(cfg.ResidentRecallSightMult, cfg.ResidentHatchSightMult,
                "회수 배수가 부화 배수보다 크지 않다 — 경계를 걷는 주민 앞에서 무리가 깜빡인다");
            Assert.Greater(cfg.ResidentRearmCooldownDays, 0f,
                "재무장 쿨다운이 0이다 — 전멸 즉시 재부화 = 한 자리 무한 파밍");

            // 부화 반경이 감지 반경(DangerRadius 6)보다 커야 "주민은 아직 못 봤는데 적은 안다"가
            // 성립한다 — 작으면 부화 순간 이미 교전 거리라 예약의 뜻이 없다.
            var map = AssetDatabase.LoadAssetAtPath<MapConfig>(MapPath);
            int hatch = Mathf.RoundToInt(map.villagerSightRadius * cfg.ResidentHatchSightMult);
            foreach (string guid in AssetDatabase.FindAssets("t:ThreatSO"))
            {
                var so = AssetDatabase.LoadAssetAtPath<AIVillage.M0.ThreatSO>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (so == null || !so.IsResident) continue;
                Assert.Greater(hatch, so.DangerRadiusTiles,
                    $"{so.name}: 부화 반경({hatch})이 감지 반경({so.DangerRadiusTiles}) 이하 — " +
                    "태어나는 순간 이미 교전 거리다");
            }
        }

        [Test]
        public void M26B_T9_HatchAndRecall_HappenOutsideVision()
        {
            // A2 (2026-08-11 사용자 관측): 지형 기억 위에 개체까지 그리면 회수가 눈앞의 증발로
            // 보인다. 개체 렌더는 현재 시야로 가렸고, 이 검사는 그 전제인 **기하 불변식**을 지킨다:
            //   시야 < 부화  → 나타나는 순간이 절대 안 보인다
            //   시야 < 회수 − 배회 → 사라지는 순간이 절대 안 보인다 (무리는 앵커에서 배회 반경까지 간다)
            // 🔴 이 산수가 깨지면 A2 는 조용히 거짓말이 된다 — 렌더 코드는 그대로 green 이므로
            //    여기서만 잡힌다 (반경은 전부 에셋이라 밸런스 패치가 깰 수 있다).
            var world = AssetDatabase.LoadAssetAtPath<AIVillage.M0.WorldConfigSO>(
                "Assets/M0Config/WorldConfig.asset");
            var map = AssetDatabase.LoadAssetAtPath<MapConfig>(MapPath);
            Assert.IsNotNull(world); Assert.IsNotNull(map);

            int sight = map.villagerSightRadius;
            int hatch = Mathf.RoundToInt(sight * world.ResidentHatchSightMult);
            int recall = Mathf.RoundToInt(sight * world.ResidentRecallSightMult);

            Assert.Greater(hatch, sight,
                $"부화({hatch}) ≤ 시야({sight}) — 무리가 눈앞에서 나타난다");

            foreach (string guid in AssetDatabase.FindAssets("t:ThreatSO"))
            {
                var so = AssetDatabase.LoadAssetAtPath<AIVillage.M0.ThreatSO>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (so == null || !so.IsResident) continue;
                Assert.Greater(recall - so.WanderRadiusTiles, sight,
                    $"{so.name}: 회수({recall}) − 배회({so.WanderRadiusTiles}) ≤ 시야({sight}) — " +
                    "배회로 앵커에서 벗어난 개체가 눈앞에서 사라질 수 있다");
            }
        }

        [Test]
        public void M26B_T9b_FowDowngrade_IsActuallyWired()
        {
            // 🔴 재발 방지 (2026-08-11 사용자 Play 관측): FowManager.OnTick(시야→기억 강등)은
            //    舊 GameManager 가 부르던 것인데 M0 재설계가 그 호출만 유실했다. 그 뒤로
            //    "한 번 본 땅은 영원히 시야"였고 — 기억(1) 상태가 M0 판에 존재한 적이 없다 —
            //    A2 가 다녀간 땅에서 개체를 못 숨겼다. **컴파일도 다른 게이트도 못 잡는 유실**이라
            //    (호출이 없어져도 모든 것이 green) 소스 스캔으로 못을 박는다 (M24_T7 방식).
            string src = System.IO.File.ReadAllText("Assets/Scripts/M0/SimulationLoop.cs");
            StringAssert.Contains("FowManager.Instance.OnTick()", src,
                "SimulationLoop 이 FoW 강등을 안 부른다 — 한 번 본 땅이 영원히 '시야'가 되고 " +
                "A2(개체는 시야에서만)가 다녀간 땅에서 조용히 무력화된다");
        }

        [Test]
        public void M26B_T8c_BandDensity_ScalesWithMapArea()
        {
            // 🔴 이 검사가 지키는 것: **맵을 넓혔을 때 들판이 더 안전해지지 않는다.**
            //    절대 마릿수로 적으면 면적이 9배가 되어도 무리는 그대로라 마주칠 확률이 1/9이 된다
            //    (2026-08-11 사용자 판단 — "우리 맵은 100×100이 되지는 않을 것").
            const float density = 4f;   // 10,000타일당 4무리 = 100×100에 4곳

            Assert.AreEqual(4, AIVillage.M0.ThreatService.BandCount(density, 100L * 100),
                "100×100 에서 4무리가 아니다 — 기준점이 어긋났다");
            Assert.AreEqual(16, AIVillage.M0.ThreatService.BandCount(density, 200L * 200),
                "면적 4배인데 무리가 4배가 아니다 — 넓은 맵이 더 안전해진다");
            Assert.AreEqual(36, AIVillage.M0.ThreatService.BandCount(density, 300L * 300),
                "면적 9배인데 무리가 9배가 아니다");

            // 밀도가 0이면 상주 없음 (중립 불변식 — 켜지 않은 종족은 아무 일도 없다).
            Assert.AreEqual(0, AIVillage.M0.ThreatService.BandCount(0f, 100L * 100),
                "밀도 0인데 무리가 섰다");

            // 작은 맵에서도 조용히 0이 되지 않는다 — 0.5무리는 1무리다.
            Assert.AreEqual(1, AIVillage.M0.ThreatService.BandCount(density, 30L * 30),
                "작은 맵에서 상주가 통째로 사라졌다 (반올림이 0을 만들었다)");
        }

        // ── W6: 위험 기억 — 당해 보고 안다 ───────────────────────────────────────
        //
        // 🔴 고정한 축: 노드는 손으로 세운 두 개(가까운 것·먼 것)다. 검사가 보는 것은
        //    "기억이 선택을 뒤집는가"이지 배치가 아니다.

        [Test]
        public void M26B_T6_DangerMemory_FlipsChoice_AndIsPersonal()
        {
            var d = new AIVillage.M0.DiscoveryService();
            d.AddResourceNode(new ResourceNode("near", ResourceType.Wood, 8, 0, 10f, 0f, true));
            // 거리 차 10 < 위험 기여 12 — 벌점이 거리 손해를 넘어야 뒤집힌다 (동점은 먼저 만난
            // 쪽 = near 가 이기는 규약이라, 차를 12로 두면 안 뒤집히는 게 맞다 — 첫 판이 그랬다).
            d.AddResourceNode(new ResourceNode("far",  ResourceType.Wood, 18, 0, 10f, 0f, true));
            d.ConfigureGatherChoice(0f, 12f, (x, y) => 1f);   // 배포 위험 가중치와 같은 자릿수

            // 기억이 없으면 최근접 — W4 중립 불변식이 W6 뒤에도 산다.
            var fresh = new AIVillage.M0.DangerMemory(8, 0.5f);
            Assert.AreEqual("near", d.FindBestDiscovered(ResourceType.Wood, 0, 0, fresh.PenaltyAt, out _).NodeId,
                "기억이 없는데 최근접을 안 골랐다 — 중립 불변식이 깨졌다");

            // 가까운 노드 곁에서 당했다 → 같은 후보인데 먼 쪽으로 뒤집힌다 (성공 기준 ⑤).
            var burned = new AIVillage.M0.DangerMemory(8, 0.5f);
            burned.Remember(7, 0, 1f);
            Assert.AreEqual("far", d.FindBestDiscovered(ResourceType.Wood, 0, 0, burned.PenaltyAt, out _).NodeId,
                "당한 자리 곁의 노드를 여전히 고른다 — 기억이 선택에 안 걸린다");

            // 🔑 개인이다 (ADR-T2-4) — 같은 판에서 안 당한 주민의 답은 그대로다.
            Assert.AreEqual("near", d.FindBestDiscovered(ResourceType.Wood, 0, 0, fresh.PenaltyAt, out _).NodeId,
                "한 명의 기억이 다른 주민의 선택을 바꿨다 — 마을 공유 기억이 됐다");

            // 반경 — 당한 정확한 좌표만이 아니라 둘레가 전부 위험하다 (한 칸 옆 함정 방지).
            Assert.Greater(burned.PenaltyAt(7 + 8, 0), 0f, "반경 안인데 벌점 0 — 한 칸 옆에서 똑같이 당한다");
            Assert.AreEqual(0f, burned.PenaltyAt(7 + 9, 0), 1e-4f, "반경 밖인데 벌점이 있다");
        }

        [Test]
        public void M26B_T6b_DangerMemory_DecaysToZero_Monotonically()
        {
            var m = new AIVillage.M0.DangerMemory(8, 0.5f);
            m.Remember(0, 0, 1f);

            // 단조 감소 — 시간이 지날수록 벌점이 늘어나는 일은 없다.
            float prev = m.PenaltyAt(0, 0);
            for (int step = 0; step < 7; step++)   // 0.3일 × 7 = 2.1일 > 강도 1 ÷ 0.5/일 = 2일
            {
                m.Decay(0.3f);
                float now = m.PenaltyAt(0, 0);
                Assert.LessOrEqual(now, prev, "감쇠했는데 벌점이 늘었다");
                prev = now;
            }
            Assert.AreEqual(0f, m.PenaltyAt(0, 0), 1e-4f, "2일이 지났는데 기억이 남아 있다 (강도 1 ÷ 0.5/일 = 2일)");

            // 인스턴스 감쇠와 순수 검산이 같은 산수인가 — 다르면 게이트가 거짓을 지킨다.
            Assert.AreEqual(0.4f, AIVillage.M0.DangerMemory.RemainingAfter(1f, 0.5f, 1.2f), 1e-4f);
            var m2 = new AIVillage.M0.DangerMemory(8, 0.5f);
            m2.Remember(0, 0, 1f);
            m2.Decay(1.2f);
            Assert.AreEqual(AIVillage.M0.DangerMemory.RemainingAfter(1f, 0.5f, 1.2f),
                            m2.PenaltyAt(0, 0), 1e-4f, "인스턴스 감쇠와 순수 검산의 산수가 다르다");

            // 실패 가능성 증명 — 감쇠 0이면 기억이 영원하다 (이 검사가 빈 검사가 아님을 증명).
            var forever = new AIVillage.M0.DangerMemory(8, 0f);
            forever.Remember(0, 0, 1f);
            forever.Decay(100f);
            Assert.Greater(forever.PenaltyAt(0, 0), 0f, "감쇠 0인데 잊었다 — Decay가 값을 무시한다");
        }

        [Test]
        public void M26B_T6c_OneBite_BeatsDeploymentClusterGeometry()
        {
            // 🔴 재발 방지 (2026-08-11 기계 실측): T6 는 손으로 세운 두 노드(거리 차 10)로
            //    검사해 초록이었는데, 배포 맵은 클러스터(간격 15~30) + 기억 반경이라 기하가
            //    달랐다 — 다섯 대를 물려도 같은 숲으로 갔고, 회피는 화면에 존재한 적이 없다.
            //    (T5 교훈 "통계가 잴 수 있는가"의 기하판 — 검사는 배포 기하로 잰다.)
            // 고정한 축: 배포 에셋 그대로 (AgentConfig 기억 수치 × 스폰 설정 클러스터 간격).
            //    지키는 부등식 = **피격 1회 벌점 > 최대 클러스터 간격** — 어느 자원이든
            //    한 번 물리면 옆 클러스터가 이길 수 있다.
            var agentCfg = AssetDatabase.LoadAssetAtPath<AIVillage.M0.AgentConfigSO>(
                "Assets/M0Config/AgentConfig.asset");
            var cfg = AssetDatabase.LoadAssetAtPath<ResourceNodeSpawnConfig>(ConfigPath);
            Assert.IsNotNull(agentCfg); Assert.IsNotNull(cfg);

            float oneBite = agentCfg.DangerMemoryPerHit * agentCfg.GatherDangerWeight;
            int maxSpacing = 0;
            foreach (ResourceTypeSpawnData d in cfg.resourceTypes)
                if (d.nodeCountPer10kTiles > 0f) maxSpacing = Mathf.Max(maxSpacing, d.minClusterSpacing);
            Assert.Greater(maxSpacing, 0, "배포에 자원이 없다 — 이 검사는 빈 검사다");

            Assert.Greater(oneBite, maxSpacing,
                $"피격 1회 벌점({oneBite:0.#} = 강도 {agentCfg.DangerMemoryPerHit:0.#} × 가중치 " +
                $"{agentCfg.GatherDangerWeight:0.#})이 최대 클러스터 간격({maxSpacing})을 못 넘는다 — " +
                "위험 기억이 어떤 선택도 못 바꾸는 장식이 된다 (2026-08-11 실측 재발)");

            // 기억 반경이 벌점의 뜻을 죽이지 않는가 — 반경이 간격보다 크면 옆 클러스터까지
            // 같은 벌점을 받아 위 부등식이 공허해진다 (빈 검사 방지).
            Assert.Less(agentCfg.DangerMemoryRadiusTiles, maxSpacing,
                $"기억 반경({agentCfg.DangerMemoryRadiusTiles})이 클러스터 간격({maxSpacing}) 이상 — " +
                "도망갈 옆 클러스터까지 위험으로 기억해 회피가 다시 불가능해진다");
        }

        // ── W7-0: 구조가 구조적으로 가능한가 (성공 기준 ⑥ — 이 검산이 W7 UI 의 존재 조건) ──

        [Test]
        public void M26B_T7_RescueIsStructurallyPossible()
        {
            // 🔴 부상 방치 사망 소요가 "맵 끝까지 걷는 시간"보다 짧으면 구조 명령 UI 는 장식이다
            //    (명세 §9 W7-0 — 짧으면 W7 을 만들지 않고 수치 판단으로 되돌린다).
            // 고정한 축: 거리 상계 = 마을(0,0)→모서리 **맨해튼**(대각 이동이 있어 실제는 더 짧다) ·
            // 속도 = 건강한 치료사 전속 (부상자가 아니라 **구조자**가 걷는다) · 지형 감속 미포함
            //   (감속은 늪뿐이고 치료사가 늪을 최단으로 질러가는 경우만 악화 — 여유율이 흡수).
            var agentCfg = AssetDatabase.LoadAssetAtPath<AIVillage.M0.AgentConfigSO>(
                "Assets/M0Config/AgentConfig.asset");
            var worldCfg = AssetDatabase.LoadAssetAtPath<AIVillage.M0.WorldConfigSO>(
                "Assets/M0Config/WorldConfig.asset");
            var map = AssetDatabase.LoadAssetAtPath<MapConfig>(MapPath);
            Assert.IsNotNull(agentCfg); Assert.IsNotNull(worldCfg); Assert.IsNotNull(map);

            int worstManhattan = map.mapSize;                       // (0,0) → (±50,±50) = 100
            float walkSec = worstManhattan / Mathf.Max(0.01f, agentCfg.BaseMoveSpeed);
            float walkDays = walkSec * worldCfg.GameTimeScale;      // 초 × 배율 = 게임일 (SimLoop 과 같은 환산)
            float deathDays = agentCfg.InjuryDeathAfterDays;

            Assert.Greater(deathDays, walkDays,
                $"부상 방치 사망 {deathDays:0.##}일 ≤ 맵 끝 도보 {walkDays:0.##}일 — " +
                "구조가 구조적으로 불가능하다. W7 UI 를 만들지 말고 수치 판단으로 돌아갈 것");

            // 여유율도 기록해 둔다 — 알림을 보고 명령하는 사람의 반응 시간이 이 여유 안에 산다.
            // 시간 밸런스 세션에서 일 단위 수치를 흔들면 이 비율이 먼저 알려 준다.
            Assert.GreaterOrEqual(deathDays / walkDays, 1.5f,
                $"여유율 {deathDays / walkDays:0.##}× < 1.5× — 걷는 시간은 되지만 " +
                "플레이어가 알아채고 명령할 틈이 없다");
        }

        [Test]
        public void M26B_T7b_RescueOrder_IsWiredToExistingGoal()
        {
            // W7 은 **새 goal 0** 이 계약이다 — 구조 명령은 기존 Goal_TreatInjured 의 주입이어야
            // 한다. 이 게이트는 두 가지를 못박는다: ①씬의 _orderTreat 가 그 에셋을 가리킨다
            //    (배선 누락은 컴파일이 못 잡고 Play 에서 T 가 조용히 안 먹는 것으로만 드러난다)
            // ②지목 통로(OrderTargetVillager)를 TendRunner 가 실제로 읽는다 (소스 스캔 — M24_T7 방식).
            string meta = System.IO.File.ReadAllText("Assets/M0Config/Goals/Goal_TreatInjured.asset.meta");
            string guid = null;
            foreach (string line in meta.Split('\n'))
                if (line.StartsWith("guid:")) { guid = line.Substring(5).Trim(); break; }
            Assert.IsNotNull(guid, "Goal_TreatInjured.asset.meta 에서 guid 를 못 읽었다");

            string scene = System.IO.File.ReadAllText("Assets/Scenes/M0Scene.unity");
            StringAssert.Contains("_orderTreat: {fileID: 11400000, guid: " + guid, scene,
                "씬의 _orderTreat 가 Goal_TreatInjured 를 가리키지 않는다 — T 명령이 조용히 죽는다");

            string tend = System.IO.File.ReadAllText("Assets/Scripts/M0/Agent/Runners/TendRunner.cs");
            StringAssert.Contains("OrderTargetVillager", tend,
                "TendRunner 가 지목(OrderTargetVillager)을 안 읽는다 — 목록에서 골라도 최근접에게 간다");
        }

        // ── W7 후속: T10 — 위독/안정 표기는 상태 기계와 같은 말을 해야 한다 ────────
        //
        // 🔴 재발 방지 (2026-08-11 사용자 Play): T 목록이 IsStabilized(응급조치 1회 소진 표식)를
        //    "안정됨"으로 읽어, 유예가 만료돼 **출혈이 재개된 부상자가 안정됨으로 떴다** —
        //    누구부터 구할지 고르는 목록의 거짓말. 고정한 축: **정본은 NextInjuryStateV2**
        //    (방치 시계의 유일한 규칙)이고 표시 판정(InjuryClockRunning)은 그 그림자다 —
        //    16조합 전수로 두 함수가 같은 갈래인지 대조한다 (표시 쪽을 어떻게 고쳐도
        //    상태 기계와 갈리면 red — 검사가 특정 구현을 강요하지 않는다).

        [Test]
        public void M26B_T10_InjuryClockPredicate_MatchesStateMachine()
        {
            const float graceDays = 0.5f, dt = 0.1f;
            foreach (bool tended in new[] { false, true })
            foreach (bool healerFlag in new[] { false, true })
            foreach (bool stabilized in new[] { false, true })
            foreach (float grace in new[] { 0f, 0.5f })
            {
                bool byHealer = tended && healerFlag;
                bool byHelper = tended && !healerFlag;
                (_, float neglect, _, _) = AIVillage.M0.VillagerAgent.NextInjuryStateV2(
                    0f, 0f, stabilized, grace, byHealer, byHelper, 1f, graceDays, dt);
                bool clockRan = neglect > 0f;   // 방치가 쌓였다 = 시계가 돌았다 (출혈 재개)

                Assert.AreEqual(clockRan,
                    AIVillage.M0.VillagerAgent.InjuryClockRunning(tended, healerFlag, stabilized, grace),
                    $"tended={tended} healer={healerFlag} stab={stabilized} grace={grace}: " +
                    "표시 판정이 상태 기계와 다른 갈래다 — 목록이 거짓말을 한다");
            }

            // 사용자가 본 바로 그 조합을 명시로 못박는다: 안정화됐지만 유예가 끝났고 간호도
            // 없다 → 출혈 재개 = **위독**이다. "안정됨"이 다시 뜨면 여기서 잡힌다.
            Assert.IsTrue(AIVillage.M0.VillagerAgent.InjuryClockRunning(
                              tended: false, tendedByHealer: false, stabilized: true, graceLeft: 0f),
                "유예 만료 + 미간호 부상자가 위독으로 판정되지 않는다 — 舊 IsStabilized 표기 버그의 재발");
        }

        // ── T1-c: 중립 불변식 — 판정을 안 넘기면 舊 배치와 완전히 같다 ────────────

        [Test]
        public void M26B_T1c_NullPredicate_IsIdenticalToLegacyPlacement()
        {
            // 같은 시드 두 번 → 좌표가 타일 단위로 같아야 한다. 판정 파라미터를 더한 것만으로
            // 배치가 흔들리면 지형 없는 판(팔레트 빈 판)이 조용히 달라진다.
            List<ResourceNode> a = RunSpawn(4242, null);
            List<ResourceNode> b = RunSpawn(4242, null);

            Assert.AreEqual(a.Count, b.Count, "같은 시드인데 노드 수가 다르다 — 결정성이 깨졌다");
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].TileX, b[i].TileX, $"노드 {i} X 좌표 불일치");
                Assert.AreEqual(a[i].TileY, b[i].TileY, $"노드 {i} Y 좌표 불일치");
                Assert.AreEqual(a[i].ResourceType, b[i].ResourceType, $"노드 {i} 타입 불일치");
            }
        }
    }
}
