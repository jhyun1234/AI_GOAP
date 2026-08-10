using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M23 비주얼 게이트 (Docs/M23_비주얼_실행명세서.md). W1 = 행동 애니메이션 배선 검산 —
    /// fileID 손배선이 한 자리라도 틀리면 스프라이트가 null로 로드되므로 여기서 잡힌다.
    /// </summary>
    public class M23_VisualGates
    {
        private static AgentSpriteSetSO Sprites()
            => AssetDatabase.LoadAssetAtPath<AgentSpriteSetSO>("Assets/M0Config/VillagerSprites.asset");

        [Test]
        public void M23_T1_ActionAnims_WiredAndComplete()
        {
            // 배포 액션 전수 — Anim != None이면 스프라이트 세트에 그 몸짓 칸이 있고 3방 전부 차 있다
            // (침묵 배선 방지: 몸짓을 선언하고 그림이 없으면 "일하는데 가만히 서 있는" 거짓말이 된다)
            AgentSpriteSetSO set = Sprites();
            Assert.IsNotNull(set, "VillagerSprites 에셋 없음");
            Assert.IsTrue(set.Actions != null && set.Actions.Length > 0, "행동 칸(Actions)이 비어 있다");

            var kinds = new System.Collections.Generic.HashSet<AnimKind>();
            foreach (AgentSpriteSetSO.ActionAnim a in set.Actions)
            {
                kinds.Add(a.Kind);
                Assert.AreNotEqual(AnimKind.None, a.Kind, "None 칸은 의미가 없다 (폴백은 미등록으로 표현)");
                foreach ((string label, Sprite[] frames) in
                         new[] { ("Down", a.Down), ("Side", a.Side), ("Up", a.Up) })
                {
                    Assert.IsTrue(frames != null && frames.Length > 0, $"{a.Kind}/{label}: 프레임 0장");
                    for (int i = 0; i < frames.Length; i++)
                        Assert.IsNotNull(frames[i],
                            $"{a.Kind}/{label}[{i}]: 스프라이트 null — fileID 손배선 오류 (meta internalID 재확인)");
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:ActionSO", new[] { "Assets/M0Config/Actions" }))
            {
                var action = AssetDatabase.LoadAssetAtPath<ActionSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (action == null || action.Anim == AnimKind.None) continue;
                Assert.IsTrue(kinds.Contains(action.Anim),
                    $"{action.name}: Anim={action.Anim}인데 스프라이트 세트에 칸이 없다");
            }
        }

        [Test]
        public void M23_T2_FenceAutotile_MaskMappingAndWiring()
        {
            // 조각 선택 순수 함수 (ADR-M23-2) — 시트 판독표를 박제 (실수로 표를 바꾸면 red).
            // 인덱스: 0=세로상단 1=가로좌끝 2=가로중간 3=가로우끝 4=세로중간 5=┌ 6=┬ 7=┐
            //         8=세로하단 9=├ 10=┼ 11=┤ 12=단독 13=└ 14=┴ 15=┘
            Assert.AreEqual(12, DefenseFenceView.PieceOf(false, false, false, false), "고립 = 단독 기둥");
            Assert.AreEqual(10, DefenseFenceView.PieceOf(true, true, true, true), "사방 = 십자");
            Assert.AreEqual(2, DefenseFenceView.PieceOf(false, true, false, true), "동서 = 가로 중간");
            Assert.AreEqual(4, DefenseFenceView.PieceOf(true, false, true, false), "남북 = 세로 중간");
            Assert.AreEqual(1, DefenseFenceView.PieceOf(false, true, false, false), "동만 = 가로 왼끝");
            Assert.AreEqual(3, DefenseFenceView.PieceOf(false, false, false, true), "서만 = 가로 오른끝");
            Assert.AreEqual(0, DefenseFenceView.PieceOf(false, false, true, false), "남만 = 세로 위끝");
            Assert.AreEqual(8, DefenseFenceView.PieceOf(true, false, false, false), "북만 = 세로 아래끝");
            Assert.AreEqual(5, DefenseFenceView.PieceOf(false, true, true, false), "동남 = ┌");
            Assert.AreEqual(7, DefenseFenceView.PieceOf(false, false, true, true), "남서 = ┐");
            Assert.AreEqual(13, DefenseFenceView.PieceOf(true, true, false, false), "북동 = └");
            Assert.AreEqual(15, DefenseFenceView.PieceOf(true, false, false, true), "북서 = ┘");
            Assert.AreEqual(6, DefenseFenceView.PieceOf(false, true, true, true), "동남서 = ┬");
            Assert.AreEqual(14, DefenseFenceView.PieceOf(true, true, false, true), "북동서 = ┴");
            Assert.AreEqual(9, DefenseFenceView.PieceOf(true, true, true, false), "북동남 = ├");
            Assert.AreEqual(11, DefenseFenceView.PieceOf(true, false, true, true), "북남서 = ┤");

            // 거울 대칭 (판독 오류 탐지기): E↔W 반전이면 좌우 조각도 짝으로 뒤집힌다
            Assert.AreEqual(
                DefenseFenceView.PieceOf(false, true, false, false) == 1 ? 3 : -1,
                DefenseFenceView.PieceOf(false, false, false, true), "좌우 거울쌍 1↔3");

            // 에셋 배선 — 조각 16장 전부 실 스프라이트 (fileID 손배선 검증), 문 그림 실재
            var fence = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/Fence.asset");
            var gate = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/Gate.asset");
            Assert.IsTrue(fence.TileSprites != null && fence.TileSprites.Length == 16, "울타리 조각 16장");
            for (int i = 0; i < 16; i++)
                Assert.IsNotNull(fence.TileSprites[i], $"울타리 조각[{i}] null — fileID 손배선 오류");
            Assert.IsNotNull(fence.MarkerSprite, "울타리 대표(폴백) 스프라이트");
            Assert.IsNotNull(gate.MarkerSprite, "문 대표 스프라이트 (고스트·폴백용)");
            // 🔁 2026-08-09 개정 — 舊 규약은 "문도 울타리 조각 16장을 입고 금빛 색조로 구분"이었다
            // (M22-2차 W3, Play 피드백 "문이 뜬금없다"). 그 규약의 전제는 **문에 제 그림이 없다**는
            // 것이었고, 같은 팩의 여닫는 문 그림이 들어오면서 전제가 사라졌다. 이제 문은 제 그림으로
            // 잠금까지 말한다 — 조각을 다시 채우면 그 표현이 덮이므로 여기서 막는다.
            // (문 그림 자체의 전수 검산은 M22_T17 이 맡는다 — 검사 소유를 한 곳에 둔다.)
            Assert.IsTrue(gate.TileSprites == null || gate.TileSprites.Length == 0,
                "문은 이제 울타리 조각을 입지 않는다 — 채우면 문 그림이 덮인다 (M22_T17 참조)");
        }

        [Test]
        public void M23_T1b_CoreActions_AnimMapping()
        {
            // 핵심 매핑 박제 (합의 — 명세 §W1. 괭이(Hoe)는 시트에 없어 심기 = 물뿌리개로 정정):
            // 벌목=Chop · 채석=Mine · 수리=Hammer · 심기=Water · 교전=Attack
            foreach ((string name, AnimKind want) in new[]
            {
                ("ChopWood", AnimKind.Chop), ("MineStone", AnimKind.Mine),
                ("RepairDefense", AnimKind.Hammer), ("BuildFence", AnimKind.Hammer),
                ("PlantCrop", AnimKind.Water), ("Action_Fight", AnimKind.Attack),
            })
            {
                var a = AssetDatabase.LoadAssetAtPath<ActionSO>($"Assets/M0Config/Actions/{name}.asset");
                Assert.IsNotNull(a, $"{name} 에셋 없음");
                Assert.AreEqual(want, a.Anim, $"{name}: 몸짓 매핑이 명세와 다르다");
            }
            // 식사·휴식은 시트에 동작이 없다 — None 유지 (중립: 배선 전과 완전 동일)
            var eat = AssetDatabase.LoadAssetAtPath<ActionSO>("Assets/M0Config/Actions/EatRawFood.asset");
            Assert.AreEqual(AnimKind.None, eat.Anim, "식사는 None (시트에 동작 없음 — 스코프 가드)");
        }

        [Test]
        public void M23_T4_WorldSort_DepthIsY()
        {
            // 2026-08-09 — 앞뒤는 **월드 Y**가 정한다 (사용자 Play 지적: "주민이 집 앞인지 뒤인지
            // 모르겠다"). 舊 고정 층(건물 5 · 주민 10)은 주민을 늘 집 위에 올려놨다.
            // 순수 함수라 전수 검산이 가능하다 — 이 규약이 깨지면 화면에서 앞뒤가 사라진다.

            // ① 아래쪽이 앞 (나중에 그려진다)
            Assert.Greater(WorldSort.Order(3f, 0), WorldSort.Order(4f, 0), "아래 칸이 앞이라야 한다");
            Assert.Greater(WorldSort.Order(-5f, 0), WorldSort.Order(0f, 0), "음수 y도 같은 규칙");

            // ② bias는 **같은 칸 안에서만** 이긴다 — 한 타일을 건너뛰면 규약이 무너진다.
            //    맵 전 범위(±50타일)에서 전수 검산: 어떤 bias도 아래 칸의 최소 bias를 못 넘는다.
            for (int y = -50; y < 50; y++)
                for (int bias = 0; bias <= 15; bias++)
                    Assert.Less(WorldSort.Order(y, bias), WorldSort.Order(y - 1, 0),
                        $"y={y} bias={bias}: 같은 칸 보정이 아래 칸을 이겼다 (YScale={WorldSort.YScale})");

            // ③ 층 표는 전부 bias 범위 안 (하나라도 16 이상이면 ②가 무너진다)
            int[] biases =
            {
                WorldSort.Node, WorldSort.Ghost, WorldSort.Plot, WorldSort.Crop, WorldSort.Trap,
                WorldSort.Build, WorldSort.Damage, WorldSort.Lock, WorldSort.Select,
                WorldSort.Agent, WorldSort.Threat,
            };
            foreach (int b in biases)
                Assert.That(b, Is.InRange(0, WorldSort.YScale - 1), "bias는 0~YScale-1만");

            // ④ 같은 칸의 순서 — 밟히는 것 < 서 있는 것 < 사람 < 짐승
            Assert.Less(WorldSort.Trap, WorldSort.Build, "함정은 건물 아래(밟힌다)");
            Assert.Less(WorldSort.Build, WorldSort.Agent, "같은 칸이면 사람이 건물 위");
            Assert.Less(WorldSort.Agent, WorldSort.Threat, "같은 칸이면 짐승이 사람 위(무는 게 보여야)");
            Assert.Less(WorldSort.Select, WorldSort.Agent, "선택 링은 발밑");

            // ⑤ 지면·데칼·붙박이는 월드 밖 — 맵 끝(±50)에서도 안 섞인다.
            //    🔴 Ground가 여기 있는 이유: Y 정렬은 위쪽 물건에 **음수** order를 준다.
            //    지면 판이 0이던 시절 그대로면 y > 0인 집·주민이 통째로 지면 뒤로 숨는다
            //    (2026-08-09 Play 실측에서 잡은 회귀 — 이 줄이 그 재발을 막는다).
            int farBack = WorldSort.Order(50f, 0), farFront = WorldSort.Order(-50f, 15);
            Assert.Less(WorldSort.Ground, farBack, "지면 판이 맨 뒤 물건보다도 뒤라야 한다");
            Assert.Less(WorldSort.Ground, WorldSort.Decal, "지면 판은 바닥 데칼보다도 아래");
            Assert.Less(WorldSort.Decal, farBack, "바닥 데칼이 맨 뒤 물건보다도 뒤라야 한다");
            Assert.Greater(WorldSort.Overlay, farFront, "이름표·말풍선은 맨 앞 물건보다 앞이라야 한다");
        }

        [Test]
        public void M23_T5_WorldArt_ShippedWiring()
        {
            // 2026-08-09 — 밭·자원 노드 실그림 배선 검산. 색 원으로 되돌아가면 red.
            var plot = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/M0Config/Buildings/FarmPlot.asset");
            Assert.IsNotNull(plot, "FarmPlot 에셋 없음");
            Assert.IsNotNull(plot.MarkerSprite, "밭 흙 그림 미배선 — 갈색 원으로 회귀");
            Assert.AreEqual(WorldSort.Plot, plot.SortingOrder, "밭 흙은 작물 아래(같은 칸 bias)");

            var crops = AssetDatabase.LoadAssetAtPath<CropSpriteSetSO>("Assets/M0Config/CropSprites.asset");
            Assert.IsNotNull(crops, "CropSprites 에셋 없음");
            Assert.IsNotNull(crops.Growing, "작물 새싹 그림 미배선");
            Assert.IsNotNull(crops.Ripe, "작물 결실 그림 미배선");
            Assert.Greater(crops.SortingOrder, plot.SortingOrder, "작물은 흙 위에 얹힌다");

            var cfg = AssetDatabase.LoadAssetAtPath<AIVillage.Core.ResourceNodeSpawnConfig>(
                "Assets/ResourceNodeSpawnConfig/ResourceNodeSpawnConfig.asset");
            Assert.IsNotNull(cfg, "ResourceNodeSpawnConfig 에셋 없음");
            Assert.IsNotNull(cfg.resourceTypes, "resourceTypes 비어 있음");
            foreach (AIVillage.Core.ResourceTypeSpawnData t in cfg.resourceTypes)
            {
                if (t.nodeCount <= 0) continue; // 휴면 항목은 검사 대상 아님
                Assert.IsNotNull(t.nodeSprite, $"{t.resourceType}: 노드 그림 미배선 — 색 원으로 회귀");
                Assert.That(t.depletedBelowRatio, Is.InRange(0f, 1f), $"{t.resourceType}: 고갈 비율은 0~1");
                // 고갈 그림은 선택 — 없으면 흐려질 뿐이라 강제하지 않는다(중립).
                float tiles = t.nodeSprite.rect.height * t.nodeSize / t.nodeSprite.pixelsPerUnit;
                Assert.That(tiles, Is.InRange(0.4f, 4f),
                    $"{t.resourceType}: 화면 높이 {tiles:0.00}타일 — 노드 하나가 아니다");
            }
        }

        // ── T6: 화면 글자가 배포 폰트 안에 있는가 (2026-08-10) ──────────────────

        private const string ShippedFontPath =
            "Assets/TextMesh Pro/Fonts/NeoDunggeunmoPro-Regular SDF.asset";

        private static System.Collections.Generic.HashSet<int> _fontGlyphs;

        /// <summary>배포 폰트가 **실제로 담고 있는** 코드포인트 집합.
        /// TMP API 대신 에셋 YAML 을 직접 읽는다 — 테스트 어셈블리에 TMPro 참조를 새로 달지
        /// 않기 위해서다(참조를 늘리면 게이트가 배선에 얽힌다). 읽는 값은 같은 것:
        /// 정적 아틀라스 폰트의 문자표(`m_Unicode`)가 곧 그릴 수 있는 글자의 전부다
        /// (`m_AtlasPopulationMode: 0` · `m_FallbackFontAssetTable: []`).</summary>
        private static System.Collections.Generic.HashSet<int> FontGlyphs()
        {
            if (_fontGlyphs != null) return _fontGlyphs;
            Assert.IsTrue(System.IO.File.Exists(ShippedFontPath), $"배포 폰트가 없다: {ShippedFontPath}");
            var set = new System.Collections.Generic.HashSet<int>();
            foreach (string raw in System.IO.File.ReadLines(ShippedFontPath))
            {
                string line = raw.Trim();
                if (!line.StartsWith("m_Unicode: ", System.StringComparison.Ordinal)) continue;
                if (int.TryParse(line.Substring("m_Unicode: ".Length), out int cp)) set.Add(cp);
            }
            Assert.Greater(set.Count, 1000, "폰트 문자표를 못 읽었다 — 이 게이트가 통째로 빈 검사가 된다");
            _fontGlyphs = set;
            return _fontGlyphs;
        }

        [Test]
        public void M23_T6_FontSafeTable_MatchesShippedFont()
        {
            // 치환표의 계약 — 왼쪽은 **폰트에 없어야** 하고 오른쪽은 **있어야** 한다.
            // 이 검사가 없으면 표가 조용히 거짓말을 한다: 이미 있는 글자를 바꾸거나(쓸데없는
            // 치환), 없는 글자로 바꾸거나(□ 를 □ 로 바꿀 뿐).
            System.Collections.Generic.HashSet<int> font = FontGlyphs();
            Assert.Greater(FontSafeText.Table.Length, 0, "치환표가 비었다 — 안전망이 값에 없다");

            foreach ((char from, string to) in FontSafeText.Table)
            {
                Assert.IsFalse(font.Contains(from),
                    $"치환표 왼쪽 U+{(int)from:X4} '{from}' 이 폰트에 이미 있다 — 표에서 빼야 한다");
                foreach (char c in to)
                    Assert.IsTrue(font.Contains(c),
                        $"치환표 오른쪽 U+{(int)c:X4} '{c}' 이 폰트에 없다 — □ 를 □ 로 바꾸는 셈이다");
            }

            // 실패 가능성 증명 (M17 교훈) — 이 판정이 실제로 갈리는가.
            Assert.IsTrue(font.Contains('가'), "한글이 폰트에 없다 — 이 게이트는 빈 검사다");
            Assert.IsFalse(font.Contains(0x2694), "없어야 할 글자(⚔)가 있다 — 〃");
        }

        [Test]
        public void M23_T6_ScreenText_GoesThroughTheSanitizer()
        {
            // 우회 금지 — TMP 대입은 SetSafe 한 문만 지난다 (ADR-M0-3 쓰기 단일 지점의 표현판).
            // 직접 `.text =` 를 쓰면 그 문구만 안전망 밖으로 새고, 증상은 한참 뒤 콘솔에서 뜬다.
            foreach (string path in new[]
            {
                "Assets/Scripts/M0/UI/SeasonHud.cs",
                "Assets/Scripts/M0/Presentation/NameTag.cs",
                "Assets/Scripts/M0/Presentation/PlanBubble.cs",
            })
            {
                string[] lines = System.IO.File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = lines[i].Split(new[] { "//" }, System.StringSplitOptions.None)[0];
                    Assert.IsFalse(code.Contains(".text ="),
                        $"{path}:{i + 1} — TMP 대입이 SetSafe 를 우회했다: {code.Trim()}");
                }
            }
            // 실패 가능성 증명 — 정의부(FontSafeText)는 실제로 직접 대입을 갖고 있다.
            // 위 목록이 그 파일을 잘못 포함하면 즉시 red 가 되므로, 검사가 무엇을 보는지 분명하다.
            StringAssert.Contains(".text =",
                System.IO.File.ReadAllText("Assets/Scripts/M0/UI/FontSafeText.cs"),
                "안전망 정의부에 대입이 없다 — 검사 대상 문자열이 틀렸다");
        }

        [Test]
        public void M23_T6_Sanitizer_ReplacesAndPreservesEverythingElse()
        {
            // 원문 보존 — 바꿀 글자가 없으면 **같은 참조**를 돌려준다 (매 틱 호출 자리, 할당 0).
            const string clean = "고블린 — 2일 뒤";
            Assert.AreSame(clean, FontSafeText.Sanitize(clean), "멀쩡한 문구인데 새 문자열을 만들었다");
            Assert.IsNull(FontSafeText.Sanitize(null));
            Assert.AreEqual("", FontSafeText.Sanitize(""));

            // 실제로 갈리는가 — 2026-08-10 경고를 낸 그 문구.
            string fixedUp = FontSafeText.Sanitize("⚔ 고블린 3마리 — 마을 습격 중");
            StringAssert.DoesNotContain("⚔", fixedUp, "치환이 안 됐다");
            StringAssert.Contains("고블린 3마리", fixedUp, "본문이 상했다");

            // 리치텍스트 태그는 그대로 (치환이 문법을 건드리면 색이 깨진다).
            const string rich = "<color=#FF8A65>■ 밭 (3,4)</color>";
            Assert.AreEqual(rich, FontSafeText.Sanitize(rich), "리치텍스트 태그가 변형됐다");

            // 치환 뒤 문구가 **폰트로 전부 그려지는지** 끝까지 확인한다.
            System.Collections.Generic.HashSet<int> font = FontGlyphs();
            foreach (char c in FontSafeText.Sanitize("· × → ⚔ ⚠ ± − ≤ ≥ ≈ ※ ★"))
                if (c != ' ')
                    Assert.IsTrue(font.Contains(c),
                        $"치환 뒤에도 폰트에 없는 글자 U+{(int)c:X4} '{c}' 가 남았다");
        }
    }
}
