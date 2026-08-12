using AIVillage.Core;
using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M28 맵 대형화 게이트 (Docs/M28_맵대형화_실행명세서.md §W4).
    ///
    /// 맵 크기가 변수가 되는 순간 지켜야 하는 세 가지: ①위협 리패스는 프레임 예산을 넘지
    /// 않는다 (트리거 B) ②공간에 뿌려지는 개수는 면적을 따라간다 (ADR-M28-1) ③맵 정의
    /// 세 값(mapSize·mapOffset·씬 MapQuad 스케일)은 한 몸이다 (ADR-M28-3 — 씬 스케일은
    /// 코드가 안 건드리는 수동 짝이라 여기가 유일한 감시자다).
    /// </summary>
    public class M28_MapGates
    {
        // ── T1: 쿼리 예산 (W1) ──────────────────────────────────────────────────

        [Test]
        public void M28_T1_PathBudget_CapsPerFrame()
        {
            var budget = new PathQueryBudget();

            // 같은 프레임: 상한까지 허용, 그다음부터 거부 — PER_FRAME+1회를 돌려 빈 검사를 막는다.
            for (int i = 0; i < PathQueryBudget.PER_FRAME; i++)
                Assert.IsTrue(budget.TryConsume(100), $"{i + 1}번째 소비가 거부됐다 — 상한 미달인데 막힌다");
            Assert.IsFalse(budget.TryConsume(100),
                $"{PathQueryBudget.PER_FRAME + 1}번째 소비가 허용됐다 — 상한이 장식이다 (스파이크 무방비)");

            // 프레임이 넘어가면 리셋 — 연기가 기아(starvation)가 되지 않는 근거.
            Assert.IsTrue(budget.TryConsume(101), "다음 프레임 첫 소비가 거부됐다 — 리셋이 없다 (영구 기아)");
        }

        // ── T2: 개수 = 면적당 (W2 — T8c BandCount 문형 이식) ────────────────────

        [Test]
        public void M28_T2_NodeDensity_ScalesWithArea()
        {
            // 산식 자체 — 밀도 4/10k 로 알려진 값 검사 (T8c 와 같은 고정 축).
            var d = new ResourceTypeSpawnData { nodeCountPer10kTiles = 4f, clusterCountPer10kTiles = 4f };
            Assert.AreEqual(4,  d.NodeCountFor(100 * 100), "100²에서 밀도 4 ≠ 4개 — per10k 의 뜻이 깨졌다");
            Assert.AreEqual(16, d.NodeCountFor(200 * 200), "200²에서 4배가 아니다 — 넓힌 만큼 희석된다");
            Assert.AreEqual(36, d.NodeCountFor(300 * 300), "300²에서 9배가 아니다");
            Assert.AreEqual(1,  d.NodeCountFor(30 * 30),
                "작은 맵에서 0개 — 양수 밀도는 최소 1이어야 한다 (은광이 검사 맵에서 증발하면 게이트가 공허해진다)");
            d.nodeCountPer10kTiles = 0f;
            Assert.AreEqual(0, d.NodeCountFor(100 * 100), "밀도 0(휴면)이 1개를 낳는다 — 휴면 필터(M23)와 어긋난다");

            // 배포 에셋 — 전 타입이 면적당 의미를 갖는가 (10k 타일 = 밀도 그대로).
            var cfg = AssetDatabase.LoadAssetAtPath<ResourceNodeSpawnConfig>(
                "Assets/ResourceNodeSpawnConfig/ResourceNodeSpawnConfig.asset");
            Assert.IsNotNull(cfg, "배포 스폰 설정 로드");
            Assert.Greater(cfg.resourceTypes.Length, 0, "배포에 자원 타입이 없다 — 공허 통과");
            foreach (ResourceTypeSpawnData t in cfg.resourceTypes)
            {
                if (t.nodeCountPer10kTiles <= 0f) continue;
                Assert.AreEqual(Mathf.RoundToInt(t.nodeCountPer10kTiles), t.NodeCountFor(10000),
                    $"{t.resourceType}: 1만 타일에서 밀도와 개수가 다르다 — 환산식이 뜻을 잃었다");
            }
        }

        // ── T3: 맵 정의 짝 (W4 — ADR-M28-3) ─────────────────────────────────────

        [Test]
        public void M28_T3_MapDefinition_StaysPaired()
        {
            var map = AssetDatabase.LoadAssetAtPath<MapConfig>("Assets/MapConfig.asset");
            Assert.IsNotNull(map, "MapConfig 로드");

            Assert.AreEqual(map.mapSize / 2, map.mapOffset,
                "mapOffset ≠ mapSize/2 — 마을(0,0)이 맵 중심을 벗어난다 (주석에만 있던 불변식의 승격)");

            // 씬 MapQuad 스케일 — 코드가 안 건드리는 수동 짝 (실사 #1: 빠뜨리면 맵이 찌그러진다).
            // ⚠️ 한계: 같은 스케일의 다른 오브젝트가 있으면 위양성 green. 존재 검사가 최소 보장이고,
            //    mapSize 를 바꾸면 舊 스케일 문자열이 남아 있는 한 이 검사가 red 로 알려 준다.
            string scene = System.IO.File.ReadAllText("Assets/Scenes/M0Scene.unity");
            StringAssert.Contains($"m_LocalScale: {{x: {map.mapSize}, y: {map.mapSize}, z: 1}}", scene,
                $"씬에 {map.mapSize}×{map.mapSize} MapQuad 스케일이 없다 — 맵 판이 찌그러져 렌더된다 (ADR-M28-3)");
        }
    }
}
