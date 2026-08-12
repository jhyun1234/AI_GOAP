using System.Collections.Generic;
using AIVillage.Core;
using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M29 바이옴 축 게이트 (Docs/M29_바이옴축_실행명세서.md §W4).
    ///
    /// 🔴 **배포 에셋으로만 검사한다** (M27 교훈 — `CreateInstance`는 기본값 세계라 배포에서
    /// 값이 어긋나도 green 이 난다). 여기서 만드는 SO는 T3 의 **음성 대조군**뿐이다.
    ///
    /// 🔴 **고정한 축을 적는다** (방법론 M20): 아래 검사가 고정한 것은 배포 `MapConfig.mapSize`·
    /// 배포 `WorldConfig`의 기지 좌표·배포 바이옴 3종의 순서(온대·정글·산악)뿐이다. 문턱 값·
    /// 팔레트 구성·광물 거리는 전부 에셋에서 읽어 온다 — 밸런스를 돌려도 이 파일은 안 바뀐다.
    /// </summary>
    public class M29_BiomeGates
    {
        private const string TEMPERATE = "Assets/M0Config/Biomes/Biome_Temperate.asset";
        private const string JUNGLE    = "Assets/M0Config/Biomes/Biome_Jungle.asset";
        private const string MOUNTAIN  = "Assets/M0Config/Biomes/Biome_Mountain.asset";
        private const string DEEP      = "Assets/M0Config/Biomes/Biome_DeepMountain.asset";

        private static readonly string[] TERRAIN_PALETTE_PATHS =
        {
            // 🔴 2026-08-12 (M31 W3후속): 흙길 신설 — 이 게이트가 **먼저 잡았다**.
            //    "온대 팔레트가 흔들리면 알린다"가 이 목록의 일이므로 값만 갱신한다.
            "Assets/M0Config/Terrain/Terrain_Dirt.asset",
            "Assets/M0Config/Terrain/Terrain_Water.asset",
            "Assets/M0Config/Terrain/Terrain_Cliff.asset",
            "Assets/M0Config/Terrain/Terrain_Swamp.asset",
            "Assets/M0Config/Terrain/Terrain_Forest.asset",
            "Assets/M0Config/Terrain/Terrain_Plain.asset",
        };

        private static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(a, $"배포 에셋 로드 실패: {path}");
            return a;
        }

        /// <summary>배포 바이옴 — 씬에 등록된 것과 **같은 순서**여야 의미가 있다 (0번 = 온대).</summary>
        private static BiomeSO[] DeployedBiomes()
        {
            var biomes = new[] { Load<BiomeSO>(TEMPERATE), Load<BiomeSO>(JUNGLE),
                                 Load<BiomeSO>(MOUNTAIN), Load<BiomeSO>(DEEP) };

            // 씬 배선 확인 — 에셋만 있고 씬에 안 꽂혔으면 게임에서는 바이옴이 없는 판이다.
            // (M28_T3 문형: 코드가 안 건드리는 수동 짝은 게이트가 유일한 감시자다.)
            string scene = System.IO.File.ReadAllText("Assets/Scenes/M0Scene.unity");
            int prev = -1;
            foreach (BiomeSO b in biomes)
            {
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(b));
                int at = scene.IndexOf(guid, System.StringComparison.Ordinal);
                Assert.Greater(at, prev,
                    $"씬 `_biomes`에 {b.name}({guid})이 없거나 순서가 어긋난다 — " +
                    "0번이 온대가 아니면 폴백과 안전지대가 딴 바이옴이 된다");
                prev = at;
            }
            return biomes;
        }

        private static int _mapSize;
        private static Vector2Int _baseTile;

        [SetUp]
        public void LoadDeployedGeometry()
        {
            _mapSize = Load<MapConfig>("Assets/MapConfig.asset").mapSize;
            var w = Load<WorldConfigSO>("Assets/M0Config/WorldConfig.asset");
            _baseTile = new Vector2Int(w.BaseTileX, w.BaseTileY);
        }

        private static int MapSize() => _mapSize;

        private static Vector2Int BaseTile() => _baseTile;

        /// <summary>배포 값으로 세운 지도. 안전반경 0 = **바이옴 층만** 본다 (평지 덮개가 가리면
        /// 온대 보장 검사가 안전반경을 검사하는 것이 되어 버린다).</summary>
        private static TerrainService Service(uint seed, BiomeSO[] biomes)
            => new TerrainService(seed, BaseTile(), 0, null, biomes, MapSize() * 0.5f);

        private static IEnumerable<Vector2Int> AllTiles()
        {
            int half = MapSize() / 2;
            for (int x = -half; x < half; x++)
                for (int y = -half; y < half; y++)
                    yield return new Vector2Int(x, y);
        }

        private static float DistFrac(Vector2Int t)
            => Mathf.Max(Mathf.Abs(t.x - _baseTile.x), Mathf.Abs(t.y - _baseTile.y)) / (_mapSize * 0.5f);

        // ── T1: 중립 불변식 (ADR-M29-3) ─────────────────────────────────────────

        [Test]
        public void M29_T1_Unwired_KeepsOldMap()
        {
            TerrainTypeSO[] palette = System.Array.ConvertAll(TERRAIN_PALETTE_PATHS, Load<TerrainTypeSO>);

            foreach (uint seed in new uint[] { 1u, 12345u, 987654321u })
            {
                var legacy = new TerrainService(seed, BaseTile(), 12, palette);              // 개조 전 생성자
                var wired  = new TerrainService(seed, BaseTile(), 12, palette, null, 0f);    // 바이옴 미배선

                foreach (Vector2Int t in AllTiles())
                    if (legacy.At(t.x, t.y) != wired.At(t.x, t.y))
                        Assert.Fail($"시드 {seed} ({t.x},{t.y}): 바이옴 미배선인데 지형이 바뀌었다 " +
                                    $"({legacy.At(t.x, t.y)?.name} → {wired.At(t.x, t.y)?.name}) — ADR-M29-3 위반");

                Assert.IsNull(legacy.BiomeAt(0, 0), "미배선인데 바이옴이 답해진다 — 舊 판이 아니다");
            }

            // 온대 팔레트 = 배포 지형 5종. 이게 어긋나면 바이옴을 켜는 순간 마을 주변이 달라진다
            // (T1 의 진짜 값어치는 여기다 — 위 두 생성자는 같은 코드를 지난다).
            BiomeSO temperate = Load<BiomeSO>(TEMPERATE);
            CollectionAssert.AreEquivalent(palette, temperate.Palette,
                "온대 팔레트가 배포 지형 팔레트와 다르다 — 바이옴을 켜면 마을 주변 판이 바뀐다");
        }

        // ── T2: 온대 보장 (§0-1) ────────────────────────────────────────────────

        [Test]
        public void M29_T2_NearBase_IsAlwaysTemperate()
        {
            BiomeSO[] biomes = DeployedBiomes();
            float threshold = float.MaxValue;                     // 배포 문턱 중 가장 안쪽
            for (int i = 1; i < biomes.Length; i++) threshold = Mathf.Min(threshold, biomes[i].MinBaseDistFrac);
            Assert.Greater(threshold, 0f, "온대 아닌 바이옴이 문턱 0이다 — 마을 옆에 정글이 선다");

            TerrainService svc = Service(20260812u, biomes);
            int outsideNonTemperate = 0;

            foreach (Vector2Int t in AllTiles())
            {
                BiomeSO b = svc.BiomeAt(t.x, t.y);
                if (DistFrac(t) < threshold)
                {
                    if (b != biomes[0])
                        Assert.Fail($"({t.x},{t.y}) 거리비 {DistFrac(t):F2} < {threshold:F2} 인데 {b.name} — " +
                                    "초반 안전지대가 뚫렸다");
                }
                else if (b != biomes[0]) outsideNonTemperate++;
            }

            // 빈 검사 방지 — 문턱 밖이 전부 온대면 위 전수 검사는 아무것도 증명하지 않는다.
            Assert.Greater(outsideNonTemperate, 0,
                "문턱 밖에 비온대 타일이 하나도 없다 — 온대 보장 검사가 공허하다");
        }

        // ── T3: 지리 성립 (§0-2) ────────────────────────────────────────────────

        [Test]
        public void M29_T3_EveryBiome_AppearsOnEverySeed()
        {
            BiomeSO[] biomes = DeployedBiomes();

            for (uint seed = 1; seed <= 20; seed++)
            {
                var seen = new HashSet<BiomeSO>();
                TerrainService svc = Service(seed, biomes);
                foreach (Vector2Int t in AllTiles()) seen.Add(svc.BiomeAt(t.x, t.y));

                foreach (BiomeSO b in biomes)
                    Assert.IsTrue(seen.Contains(b), $"시드 {seed}: {b.name} 지역이 한 곳도 없다 " +
                                                    "— 그 판에는 그 자원·서사가 통째로 빠진다");
            }

            // 음성 대조군 — 문턱을 1로 올리면 접기가 전부 걸려 온대만 남아야 한다.
            // (통과가 "검사가 살아 있다"는 뜻이 되도록: 이게 없으면 위 검사가 무엇을 재는지 모른다.)
            var strict = new BiomeSO[biomes.Length];
            strict[0] = biomes[0];
            for (int i = 1; i < biomes.Length; i++)
            {
                strict[i] = Object.Instantiate(biomes[i]);
                strict[i].MinBaseDistFrac = 1f;
            }
            TerrainService none = Service(7u, strict);
            foreach (Vector2Int t in AllTiles())
            {
                if (DistFrac(t) >= 1f) continue;   // 맵 모서리는 거리비가 정확히 1 = 문턱 충족(경계 포함)
                Assert.AreEqual(biomes[0], none.BiomeAt(t.x, t.y),
                    $"문턱 1인데 ({t.x},{t.y})가 접히지 않았다 — 거리비 접기가 안 걸린다");
            }
            for (int i = 1; i < strict.Length; i++) Object.DestroyImmediate(strict[i]);
        }

        // ── T4: 광산 티어 (§0-4) ────────────────────────────────────────────────

        [Test]
        public void M29_T4_OreTiers_ClimbIntoTheMountain()
        {
            var cfg = Load<ResourceNodeSpawnConfig>(
                "Assets/ResourceNodeSpawnConfig/ResourceNodeSpawnConfig.asset");
            var mountain = new HashSet<TerrainTypeSO>(Load<BiomeSO>(MOUNTAIN).Palette);
            foreach (TerrainTypeSO t in Load<BiomeSO>(DEEP).Palette) mountain.Add(t);

            // 사다리 순서 = 사용자 확정 (구리 → 철 → 은 → 다이아).
            ResourceType[] ladder = { ResourceType.Copper, ResourceType.Iron,
                                      ResourceType.Silver, ResourceType.Diamond };
            float prev = -1f;
            var shallowOre = new HashSet<TerrainTypeSO>();   // 구리·철이 서는 땅
            var deepOre    = new HashSet<TerrainTypeSO>();   // 은·다이아가 서는 땅

            foreach (ResourceType ore in ladder)
            {
                ResourceTypeSpawnData d = System.Array.Find(cfg.resourceTypes, r => r.resourceType == ore);
                Assert.IsNotNull(d, $"배포 스폰 설정에 {ore}가 없다 — 광물 사다리가 비어 있다");
                Assert.Greater(d.nodeCountPer10kTiles, 0f, $"{ore} 밀도가 0 — 사다리에 이름만 있다");

                Assert.IsNotNull(d.allowedTerrain, $"{ore}: 지형 조건이 없다 — 광석이 들판에 선다");
                Assert.Greater(d.allowedTerrain.Length, 0, $"{ore}: 지형 조건이 비었다 — 광석이 들판에 선다");
                foreach (TerrainTypeSO t in d.allowedTerrain)
                    Assert.IsTrue(mountain.Contains(t),
                        $"{ore}의 허용 지형 {t?.name}이 산악 팔레트 밖이다 — '산에서 캔다'가 깨진다");

                Assert.Greater(d.minDistanceFromBase, prev,
                    $"{ore}의 최소 거리 {d.minDistanceFromBase}가 앞 티어({prev}) 이하다 — " +
                    "깊이 갈수록 귀해진다는 사다리가 무너진다");
                prev = d.minDistanceFromBase;

                var bucket = (ore == ResourceType.Silver || ore == ResourceType.Diamond)
                             ? deepOre : shallowOre;
                foreach (TerrainTypeSO t in d.allowedTerrain) bucket.Add(t);
            }

            // 🔴 **개정 2026-08-12 (사용자 Play 지적)**: 위의 최소 거리 검사만으로는 화면에서
            //    티어가 갈리지 않는다. 최소 거리는 **바닥**일 뿐 천장이 없어서, 실측(시드
            //    1318987291)에서 구리 71~156 · 철 76~175 · 은 88~167 · 다이아 93~151로 완전히
            //    겹쳤다 — 다이아 노드 반경 20 안에 구리 5·철 4·은 1이 같이 서 있었다.
            //    에셋의 사다리는 참인데 판의 사다리는 거짓이었다 (게이트가 재는 자리가 틀렸다).
            //    그래서 **깊은 티어는 다른 땅에 선다**를 강제한다 — 색이 다른 지형이 곧 깊이다.
            Assert.Greater(deepOre.Count, 0, "은·다이아에 허용 지형이 없다");
            deepOre.IntersectWith(shallowOre);
            Assert.AreEqual(0, deepOre.Count,
                "은·다이아가 구리·철과 **같은 지형**에 선다 — 한 광산 안에 네 티어가 섞여 " +
                "'깊이 들어갈수록 은'이 화면에서 읽히지 않는다 (2026-08-12 Play 지적의 재발 방지)");
        }

        // ── T6: 온대가 이 판의 바탕인가 (§0.5 — 사용자 Play 지적) ────────────────

        [Test]
        public void M29_T6_Temperate_StaysTheGround()
        {
            BiomeSO[] biomes = DeployedBiomes();
            int tiles = MapSize() * MapSize();

            foreach (uint seed in new uint[] { 1u, 20260812u, 777u })
            {
                TerrainService svc = Service(seed, biomes);
                var area = new Dictionary<BiomeSO, int>();
                foreach (BiomeSO b in biomes) area[b] = 0;
                foreach (Vector2Int t in AllTiles()) area[svc.BiomeAt(t.x, t.y)]++;

                // 🔴 바이옴을 늘릴수록 온대가 밀려나는 구조다 (선택이 `v × 바이옴 수`라
                //    한 종이 늘면 나머지가 같은 만큼 줄어든다). 3종일 때 실측 온대 25%,
                //    숲은 5%였다 — "첫 10분의 문법은 바뀌지 않는다"(§0.5)가 이미 깨져 있었다.
                //    다섯 번째 바이옴을 넣는 사람은 이 검사에서 그 대가를 먼저 본다.
                float temperate = 100f * area[biomes[0]] / tiles;
                foreach (BiomeSO b in biomes)
                    if (b != biomes[0])
                        Assert.Greater(area[biomes[0]], area[b],
                            $"시드 {seed}: {b.name}({100f * area[b] / tiles:0.#}%)이 온대" +
                            $"({temperate:0.#}%)보다 넓다 — 이 판의 바탕이 바뀌었다");
                Assert.Greater(temperate, 35f,
                    $"시드 {seed}: 온대가 {temperate:0.#}% — 바탕이 아니라 소수가 됐다 " +
                    "(바이옴을 늘렸으면 문턱을 함께 올려야 한다)");

                foreach (BiomeSO b in biomes)
                {
                    float pct = 100f * area[b] / tiles;
                    Assert.Greater(pct, 2f,
                        $"시드 {seed}: {b.name}이 {pct:0.#}% — 있으나 마나 한 넓이다 " +
                        "(등장은 하니 T3는 green이다 — 넓이는 여기서 본다)");
                }
            }
        }

        // ── T7: 장식이 자원 흉내를 내지 않는가 (M29-3차) ─────────────────────────

        [Test]
        public void M29_T7_Decor_NeverLooksLikeAResource()
        {
            // 🔴 실제로 밟을 뻔한 자리 (2026-08-12): 암반 장식으로 회색 바위를 고르려다 보니
            //    그 그림들이 **돌 노드가 이미 쓰는 칸**이었다 (둘 다 Outdoor_Decor 시트).
            //    장식은 판정이 없으므로, 자원과 같은 그림이면 플레이어는 캘 수 없는 돌을 보게 된다.
            var cfg = Load<ResourceNodeSpawnConfig>(
                "Assets/ResourceNodeSpawnConfig/ResourceNodeSpawnConfig.asset");
            var resourceArt = new HashSet<Sprite>();
            foreach (ResourceTypeSpawnData d in cfg.resourceTypes)
            {
                if (d.nodeSprite != null) resourceArt.Add(d.nodeSprite);
                if (d.depletedSprite != null) resourceArt.Add(d.depletedSprite);
                if (d.nodeSpriteVariants != null) foreach (Sprite s in d.nodeSpriteVariants) if (s != null) resourceArt.Add(s);
                if (d.depletedSpriteVariants != null) foreach (Sprite s in d.depletedSpriteVariants) if (s != null) resourceArt.Add(s);
            }
            Assert.Greater(resourceArt.Count, 0, "배포 자원에 그림이 없다 — 이 검사는 빈 검사다");

            int decorTotal = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:TerrainTypeSO", new[] { "Assets/M0Config/Terrain" }))
            {
                var t = AssetDatabase.LoadAssetAtPath<TerrainTypeSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (t == null || t.DecorSprites == null) continue;
                foreach (Sprite s in t.DecorSprites)
                {
                    if (s == null) continue;
                    decorTotal++;
                    Assert.IsFalse(resourceArt.Contains(s),
                        $"{t.name}의 장식 '{s.name}'이 자원 노드 그림과 같다 — " +
                        "플레이어가 캘 수 없는 가짜 자원을 보게 된다 (장식에는 판정이 없다)");
                }
                if (t.DecorSprites.Length > 0)
                    Assert.Greater(t.DecorPerTile, 0f,
                        $"{t.name}: 장식 그림은 꽂혀 있는데 밀도가 0 — 아무 데도 안 선다 (배선 실수)");
            }
            Assert.Greater(decorTotal, 0,
                "배포 지형에 장식이 하나도 없다 — 색만 다른 타일로 돌아갔다 (2026-08-12 Play 지적)");
        }

        // ── T5: 결정성 (ADR-T-5 · ADR-M10R-2) ───────────────────────────────────

        [Test]
        public void M29_T5_SameSeed_SameBiomes()
        {
            BiomeSO[] biomes = DeployedBiomes();
            TerrainService a = Service(4242u, biomes), b = Service(4242u, biomes), c = Service(4243u, biomes);
            int differs = 0;

            foreach (Vector2Int t in AllTiles())
            {
                Assert.AreEqual(a.BiomeAt(t.x, t.y), b.BiomeAt(t.x, t.y),
                    $"({t.x},{t.y}): 같은 시드인데 바이옴이 다르다 — 판을 재현할 수 없다");
                if (a.BiomeAt(t.x, t.y) != c.BiomeAt(t.x, t.y)) differs++;
            }

            Assert.Greater(differs, 0, "시드를 바꿔도 지도가 같다 — 시드가 바이옴에 안 먹는다");
        }
    }
}
