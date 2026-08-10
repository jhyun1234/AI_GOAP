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
        private static List<ResourceNode> RunSpawn(int seed, System.Func<int, int, bool> canHost)
        {
            var map = AssetDatabase.LoadAssetAtPath<MapConfig>(MapPath);
            Assert.IsNotNull(map, $"MapConfig 에셋 없음: {MapPath}");
            MapConfig.SetActive(map);

            var cfg = AssetDatabase.LoadAssetAtPath<ResourceNodeSpawnConfig>(ConfigPath);
            Assert.IsNotNull(cfg, $"ResourceNodeSpawnConfig 에셋 없음: {ConfigPath}");

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
                try { spawner.SpawnAll(0, 0, 10, sink, canHost); }
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
