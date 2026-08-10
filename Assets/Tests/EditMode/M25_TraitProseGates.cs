using System.Collections.Generic;
using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M25 1차 성격 서술 게이트.
    ///
    /// 🔑 이 파일이 지키는 것은 둘이다:
    ///   ① **성격은 닫히지 않는다** (`ADR-M25-2`) — T1이 배포 성격을 **전수 순회**하므로
    ///      새 성격 에셋을 넣으면 자동으로 검사 대상이 되고, 서술이 안 나오면 그 자리에서 red다.
    ///   ② **중립은 침묵한다** (`ADR-M25-3`) — 값이 없는 축이 말을 지어내지 않는다.
    ///
    /// ⚠️ 게이트는 **기구**만 지킨다. "읽히는가"(S2)는 Play가 판정한다 — 여기 green이라고
    /// 방랑자 제안 앞에서 손이 멈추는지는 알 수 없다.
    /// </summary>
    public class M25_TraitProseGates
    {
        private const string RulesPath = "Assets/M0Config/TraitRules.asset";
        private const string FontPath = "Assets/TextMesh Pro/Fonts/NeoDunggeunmoPro-Regular SDF.asset";

        private static TraitRulesSO Rules()
        {
            var r = AssetDatabase.LoadAssetAtPath<TraitRulesSO>(RulesPath);
            Assert.IsNotNull(r, $"TraitRules 로드 실패: {RulesPath}");
            return r;
        }

        private static List<PersonalitySO> Personalities()
        {
            var list = new List<PersonalitySO>();
            foreach (string guid in AssetDatabase.FindAssets("t:PersonalitySO"))
            {
                var p = AssetDatabase.LoadAssetAtPath<PersonalitySO>(AssetDatabase.GUIDToAssetPath(guid));
                if (p != null) list.Add(p);
            }
            return list;
        }

        private static List<GoalSO> Goals()
        {
            var list = new List<GoalSO>();
            foreach (string guid in AssetDatabase.FindAssets("t:GoalSO"))
            {
                var g = AssetDatabase.LoadAssetAtPath<GoalSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (g != null) list.Add(g);
            }
            return list;
        }

        /// <summary>문장 수 — 서술은 언제나 "…다." 로 끝나므로 마침표를 센다.
        /// ⚠️ 역접은 두 문장을 마침표 하나로 합치므로 **이 수는 "읽히는 덩어리 수"**이지
        /// 축 문장 수가 아니다 (W3 프로브가 이 혼동으로 실제 버그를 냈다).</summary>
        private static int Chunks(string prose)
        {
            if (string.IsNullOrEmpty(prose)) return 0;
            int n = 0;
            foreach (char c in prose) if (c == '.') n++;
            return n;
        }

        // ── T1: 배포 전수 (S1·S8) ─────────────────────────────────────────────

        [Test]
        public void M25_T1_EveryShippedPersonality_GetsDistinctProse()
        {
            TraitRulesSO rules = Rules();
            List<PersonalitySO> people = Personalities();
            List<GoalSO> goals = Goals();
            Assert.Greater(people.Count, 0, "배포 성격이 하나도 없다 — 루프가 안 돌아 공허 통과다");

            var seen = new Dictionary<string, string>();
            foreach (PersonalitySO p in people)
            {
                string prose = TraitProse.Compose(p.Traits, rules, goals);
                Assert.IsNotEmpty(prose, $"{p.name}: 서술이 비었다 — 값을 적었는데 화면이 말을 못 한다");
                Assert.GreaterOrEqual(Chunks(prose), 2,
                    $"{p.name}: 서술이 한 덩어리뿐이다 ({prose}) — 사람이 안 그려진다 (S1)");
                Assert.IsFalse(seen.ContainsKey(prose),
                    $"{p.name}과 {(seen.ContainsKey(prose) ? seen[prose] : "")}의 서술이 같다 — " +
                    "성격이 갈리는데 문장이 안 갈린다 (S1)");
                seen[prose] = p.name;
            }

            // 실패 가능성 증명 (M17 교훈) — 이 검사가 실제로 갈리는가.
            // 아무 문장도 못 얻는 벡터가 존재해야 위 Assert.IsNotEmpty 가 의미를 갖는다.
            Assert.IsEmpty(TraitProse.Compose(
                    new[] { new TraitValue { Trait = TraitId.Diligence, Value = 1 } }, rules, goals),
                "문턱 아래 벡터인데 서술이 나왔다 — 이 게이트는 무엇이든 통과시킨다");
        }

        // ── T2: 중립 침묵 (S3) ────────────────────────────────────────────────

        [Test]
        public void M25_T2_NeutralAndUnwired_SaySilence()
        {
            TraitRulesSO rules = Rules();
            List<GoalSO> goals = Goals();

            Assert.IsEmpty(TraitProse.FromAxes(new TraitValue[0], rules), "완전 중립인데 축 문장이 생겼다");
            Assert.IsEmpty(TraitProse.FromAxes(null, rules), "벡터 null인데 문장이 생겼다");
            Assert.IsEmpty(TraitProse.FromAxes(new TraitValue[0], null), "규칙 미배선인데 문장이 생겼다");
            Assert.IsEmpty(TraitProse.Compose(new TraitValue[0], rules, goals),
                "완전 중립인데 서술이 생겼다 — 화면에 줄이 하나 늘어난다 (ADR-M25-3)");
            Assert.IsEmpty(TraitProse.Compose(new TraitValue[0], null, goals), "규칙 미배선인데 서술이 생겼다");

            // 전 축이 문턱 바로 아래여도 침묵한다 (경계 검사)
            int below = rules.ProseSilentBelow - 1;
            var quiet = new TraitValue[6];
            for (int i = 0; i < 6; i++) quiet[i] = new TraitValue { Trait = (TraitId)i, Value = below };
            Assert.IsEmpty(TraitProse.FromAxes(quiet, rules),
                $"전 축 {below}(문턱 {rules.ProseSilentBelow} 바로 아래)인데 말을 했다");

            // 실패 가능성 증명 — 문턱 바로 위는 실제로 말한다 (침묵이 고장이 아님을 보인다)
            var loud = new TraitValue[] {
                new TraitValue { Trait = TraitId.Diligence, Value = rules.ProseSilentBelow } };
            Assert.IsNotEmpty(TraitProse.FromAxes(loud, rules),
                "문턱 정확히 위인데 침묵했다 — T2가 '언제나 빈 문자열'을 통과시키는 검사가 된다");
        }

        // ── T3: 결정성 (S4) ──────────────────────────────────────────────────

        [Test]
        public void M25_T3_SameVector_SameProse()
        {
            TraitRulesSO rules = Rules();
            List<GoalSO> goals = Goals();
            foreach (PersonalitySO p in Personalities())
            {
                string a = TraitProse.Compose(p.Traits, rules, goals);
                string b = TraitProse.Compose(p.Traits, rules, goals);
                Assert.AreEqual(a, b, $"{p.name}: 같은 입력에 다른 서술이 나왔다");
            }

            // 축 순서가 뒤바뀐 같은 벡터도 같은 문장이어야 한다 (에셋 저작 순서에 의존하지 않는다)
            var forward = new[] {
                new TraitValue { Trait = TraitId.Sociability, Value = 80 },
                new TraitValue { Trait = TraitId.Willfulness, Value = -70 } };
            var reversed = new[] {
                new TraitValue { Trait = TraitId.Willfulness, Value = -70 },
                new TraitValue { Trait = TraitId.Sociability, Value = 80 } };
            Assert.AreEqual(TraitProse.FromAxes(forward, rules), TraitProse.FromAxes(reversed, rules),
                "같은 벡터인데 축을 적은 순서가 문장을 바꿨다 — 에셋 저작 순서가 화면에 샌다");
        }

        // ── T4: 역접 1회 상한 (S5) ───────────────────────────────────────────

        [Test]
        public void M25_T4_Adversative_AtMostOnce()
        {
            TraitRulesSO rules = Rules();
            // 6축 × {강+, 약+, 침묵, 약−, 강−} 전수 = 5^6 = 15,625 벡터
            int[] samples = { 90, 45, 0, -45, -90 };
            var v = new TraitValue[6];
            int checkedCount = 0, everAdversative = 0;
            for (int a = 0; a < 5; a++)
            for (int b = 0; b < 5; b++)
            for (int c = 0; c < 5; c++)
            for (int d = 0; d < 5; d++)
            for (int e = 0; e < 5; e++)
            for (int f = 0; f < 5; f++)
            {
                int[] idx = { a, b, c, d, e, f };
                for (int i = 0; i < 6; i++)
                    v[i] = new TraitValue { Trait = (TraitId)i, Value = samples[idx[i]] };
                string prose = TraitProse.FromAxes(v, rules);
                checkedCount++;
                int commas = 0;
                foreach (char ch in prose) if (ch == ',') commas++;
                Assert.LessOrEqual(commas, 1,
                    $"역접이 {commas}회 — 상한 1을 넘었다 (\"…인데 …인데\"는 안 읽힌다): {prose}");
                if (commas == 1) everAdversative++;
            }
            Assert.AreEqual(15625, checkedCount, "전수 조합이 다 안 돌았다");

            // 실패 가능성 증명 — 역접이 실제로 일어나기는 하는가 (0이면 상한 검사가 공허하다)
            Assert.Greater(everAdversative, 0, "전 조합에서 역접이 한 번도 안 났다 — T4는 빈 검사다");
        }

        // ── T5: 문장표 완전성 ────────────────────────────────────────────────

        [Test]
        public void M25_T5_ShippedLineTable_IsCompleteAndUnambiguous()
        {
            TraitRulesSO rules = Rules();
            Assert.IsNotNull(rules.TraitLines, "문장표가 null이다");
            Assert.Greater(rules.TraitLines.Length, 0, "문장표가 비었다 — 서술 축이 값에 없다");

            var keys = new HashSet<string>();
            foreach (TraitLine l in rules.TraitLines)
            {
                string key = $"{(int)l.Trait}/{l.Positive}/{l.Strong}";
                Assert.IsTrue(keys.Add(key),
                    $"같은 (축,방향,강도) 칸이 두 번 있다: {key} — 어느 쪽이 뽑힐지 저작 순서가 정하게 된다");
                Assert.IsNotEmpty(l.Ending, $"{key}: 종결형이 비었다 (빈칸은 침묵이지 빈 문자열이 아니다)");
                Assert.IsNotEmpty(l.Adversative, $"{key}: 역접 연결형이 비었다 — 그 문장은 대조를 못 만든다");
            }

            // 배포 문장이 실제로 쓰이는가 — 문턱·부호 조합이 표를 찾아가는지 확인한다
            Assert.IsTrue(rules.LineFor(TraitId.Sociability, 80).HasValue, "사교 +80이 문장을 못 찾는다");
            Assert.IsTrue(rules.LineFor(TraitId.Caution, -60).HasValue, "겁 −60이 문장을 못 찾는다");
            Assert.IsFalse(rules.LineFor(TraitId.Diligence, 0).HasValue, "중립 0이 문장을 찾았다");

            // 실패 가능성 증명 — 없는 칸은 실제로 null 이다 (LineFor 가 아무거나 돌려주지 않는다)
            Assert.IsFalse(rules.LineFor((TraitId)99, 90).HasValue, "없는 축인데 문장이 나왔다");
        }

        // ── T6: goal 문장 규칙 ───────────────────────────────────────────────

        [Test]
        public void M25_T6_GoalLine_OnlyWhenAxesFallShort()
        {
            TraitRulesSO rules = Rules();
            List<GoalSO> goals = Goals();
            int cap = rules.ProseMaxAxisLines;

            int withGoal = 0, withoutGoal = 0;
            foreach (PersonalitySO p in Personalities())
            {
                string axes = TraitProse.FromAxes(p.Traits, rules);
                string full = TraitProse.Compose(p.Traits, rules, goals);
                bool appended = full.Length > axes.Length;

                // 축 문장 수는 문자열이 아니라 **덩어리와 역접**으로 되짚는다:
                // 역접 1회 = 두 문장이 한 마침표에 들어간다.
                int commas = 0;
                foreach (char ch in axes) if (ch == ',') commas++;
                int axisLines = Chunks(axes) + commas;

                if (axisLines >= cap)
                {
                    Assert.IsFalse(appended,
                        $"{p.name}: 축 {axisLines}문장(상한 {cap})인데 goal 문장이 붙었다 — 같은 말을 두 번 한다");
                    withoutGoal++;
                }
                else
                {
                    Assert.IsTrue(appended,
                        $"{p.name}: 축 {axisLines}문장뿐인데 goal 문장이 안 붙었다 — 사람이 안 그려진다");
                    withGoal++;
                }
            }

            // 실패 가능성 증명 — 두 갈래가 **둘 다 실재**해야 이 검사가 의미를 갖는다.
            Assert.Greater(withGoal, 0, "goal 문장이 붙는 성격이 하나도 없다 — 규칙의 절반이 죽었다");
            Assert.Greater(withoutGoal, 0, "goal 문장이 안 붙는 성격이 하나도 없다 — 〃");

            // PersonaLine 이 하나도 없으면 축 문장만 나온다 (중립 폴백)
            var bare = new List<GoalSO>();
            foreach (PersonalitySO p in Personalities())
                Assert.AreEqual(TraitProse.FromAxes(p.Traits, rules),
                                TraitProse.Compose(p.Traits, rules, bare),
                                $"{p.name}: goal 후보가 없는데 서술이 달라졌다");
        }

        // ── T7: 폰트 안전망 연결 (S6) ────────────────────────────────────────

        private static HashSet<int> _fontGlyphs;

        /// <summary>배포 폰트 문자표 — `M23-T6`과 같은 판독(에셋 YAML 직접).
        /// 테스트 어셈블리에 TMPro 참조를 늘리지 않기 위해서다.</summary>
        private static HashSet<int> FontGlyphs()
        {
            if (_fontGlyphs != null) return _fontGlyphs;
            Assert.IsTrue(System.IO.File.Exists(FontPath), $"배포 폰트가 없다: {FontPath}");
            var set = new HashSet<int>();
            foreach (string raw in System.IO.File.ReadLines(FontPath))
            {
                string line = raw.Trim();
                if (!line.StartsWith("m_Unicode: ", System.StringComparison.Ordinal)) continue;
                if (int.TryParse(line.Substring("m_Unicode: ".Length), out int cp)) set.Add(cp);
            }
            Assert.Greater(set.Count, 1000, "폰트 문자표를 못 읽었다 — 이 게이트가 빈 검사가 된다");
            _fontGlyphs = set;
            return _fontGlyphs;
        }

        [Test]
        public void M25_T7_AllProse_FitsInsideTheShippedFont()
        {
            // 2026-08-10에 `⚔` 하나로 콘솔이 찼다. 서술은 화면 문구가 한 번에 수십 개 느는
            // 자리라, 같은 사고를 여기서 미리 막는다.
            TraitRulesSO rules = Rules();
            HashSet<int> font = FontGlyphs();
            List<GoalSO> goals = Goals();

            foreach (TraitLine l in rules.TraitLines)
            {
                AssertRenderable(font, l.Ending, "축 종결형");
                AssertRenderable(font, l.Adversative, "축 역접형");
            }
            foreach (GoalSO g in goals)
                if (!string.IsNullOrEmpty(g.PersonaLine))
                    AssertRenderable(font, g.PersonaLine, $"{g.name}.PersonaLine");
            foreach (PersonalitySO p in Personalities())
                AssertRenderable(font, TraitProse.Compose(p.Traits, rules, goals), $"{p.name} 완성 서술");

            // 실패 가능성 증명 — 이 검사가 실제로 무언가를 잡는가 (⚔ 는 이 폰트에 없다)
            Assert.IsFalse(font.Contains(0x2694), "없어야 할 글자가 폰트에 있다 — T7은 빈 검사다");
        }

        private static void AssertRenderable(HashSet<int> font, string text, string where)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (char c in text)
            {
                if (c == '\n' || c == '\r') continue;
                Assert.IsTrue(font.Contains(c),
                    $"{where}: 배포 폰트에 없는 글자 U+{(int)c:X4} '{c}' — 화면에 □ 로 뜬다 ({text})");
            }
        }
    }
}
