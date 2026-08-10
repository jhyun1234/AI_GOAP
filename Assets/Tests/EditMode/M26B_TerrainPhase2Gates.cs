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

        private static AIVillage.M0.TerrainTypeSO[] Palette()
        {
            var list = new List<AIVillage.M0.TerrainTypeSO>();
            foreach (string guid in AssetDatabase.FindAssets("t:TerrainTypeSO", new[] { TerrainDir }))
            {
                var t = AssetDatabase.LoadAssetAtPath<AIVillage.M0.TerrainTypeSO>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (t != null) list.Add(t);
            }
            return list.ToArray();
        }

        private static float DensityMax(AIVillage.M0.TerrainTypeSO[] palette)
        {
            float m = 1f;
            foreach (AIVillage.M0.TerrainTypeSO t in palette) m = Mathf.Max(m, t.NodeDensityMult);
            return m;
        }

        /// <summary>배포 팔레트로 세운 진짜 지형. 마을 안전반경은 씬 값(12)과 같은 자를 쓴다.</summary>
        private static AIVillage.M0.TerrainService MakeTerrain(int seed, AIVillage.M0.TerrainTypeSO[] palette)
            => new AIVillage.M0.TerrainService((uint)seed, Vector2Int.zero, 12, palette);

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
                if (d.nodeCount <= 0) continue;
                if ((d.allowedTerrain != null && d.allowedTerrain.Length > 0)
                    || (d.adjacentTerrain != null && d.adjacentTerrain.Length > 0)) restricted++;
            }
            Assert.Greater(restricted, 0,
                "배포 설정에 지형 조건이 걸린 자원이 하나도 없다 — W2가 배선되지 않았거나 이 검사가 빈 검사다");

            float dMax = DensityMax(palette);
            int checkedNodes = 0;

            for (int seed = 1; seed <= 20; seed++)
            {
                AIVillage.M0.TerrainService terrain = MakeTerrain(seed, palette);
                System.Func<int, int, bool> canHost = (x, y) => terrain.IsWalkable(x, y);
                List<ResourceNode> nodes = RunSpawn(seed, canHost, terrain.At, dMax);

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
        public void M26B_T3_ImpossibleTerrain_YieldsZeroNodesAndWarns()
        {
            // 통행 불가 지형(물)만 허용하면 노드가 설 자리가 없다 — **조용히 0이 되지 않고 경고**해야 한다.
            AIVillage.M0.TerrainTypeSO[] palette = Palette();
            AIVillage.M0.TerrainTypeSO water = System.Array.Find(palette, t => !t.Walkable);
            Assert.IsNotNull(water, "통행 불가 지형이 팔레트에 없다 — 이 검사의 전제가 깨졌다");

            var cfg = ScriptableObject.CreateInstance<ResourceNodeSpawnConfig>();
            cfg.nodeMinSpacing = 2;
            cfg.maxPlacementAttempts = 50;
            cfg.resourceTypes = new[]
            {
                new ResourceTypeSpawnData
                {
                    resourceType = ResourceType.Wood, nodeCount = 10, maxAmount = 10,
                    minDistanceFromBase = 10, clusterCount = 3,
                    minNodesPerCluster = 1, maxNodesPerCluster = 3,
                    clusterSpreadRadius = 3, minClusterSpacing = 15,
                    allowedTerrain = new[] { water },   // 물 위에만 — 그런데 물은 못 지난다
                }
            };

            AIVillage.M0.TerrainService terrain = MakeTerrain(7, palette);
            List<ResourceNode> nodes = RunSpawnWithConfig(cfg, 7, (x, y) => terrain.IsWalkable(x, y),
                                                          terrain.At, DensityMax(palette));

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
                if (d.resourceType != type || d.nodeCount <= 0) continue;
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
