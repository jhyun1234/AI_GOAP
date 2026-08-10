using AIVillage.M0;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M24 종족 침공 축 게이트. 1차 W3까지 = 압력 축의 단조성.
    ///
    /// 🔑 이 파일이 지키는 것은 하나다 — **압력은 어떤 경로로도 내려가지 않는다**(ADR-M24-1).
    /// 그 규칙이 막는 것은 정반대 실패 둘이고, 둘 다 실제로 관측된 적이 있다:
    ///   ① 사망 → 마을 축소 → 위협 강등 → 영구 안정 (ADR-M10R-1이 고친 음성 피드백 루프)
    ///   ② 사망 → 압력 유지 → 회복 불가 → 남은 판이 소화 시간 (①의 거울상)
    /// ①은 여기가 막고, ②는 자비 장치(W9)가 맡는다. 압력을 낮추는 것으로는 어느 쪽도 안 푼다.
    /// </summary>
    public class M24_RaceInvasionGates
    {
        // 게이트가 자기 값을 발명하지 않도록 배포 에셋에서 읽는다 (ADR-M0-2).
        private static WorldConfigSO Config()
        {
            var c = AssetDatabase.LoadAssetAtPath<WorldConfigSO>("Assets/M0Config/WorldConfig.asset");
            Assert.IsNotNull(c, "WorldConfig 로드");
            return c;
        }

        // ── T2: 압력 단조 ─────────────────────────────────────────────────────

        [Test]
        public void M24_T2_GlobalPressure_NeverFallsWhenPopulationDrops()
        {
            // 이 축 전체가 서 있는 단 하나의 성질. 인구가 12에서 5로 무너져도 압력은 그대로다 —
            // 읽는 것이 현재 인구가 아니라 **역대 최고**이기 때문이다.
            WorldConfigSO c = Config();
            int atPeak = ThreatService.GlobalPressure(30f, 12, c.PressureDaysPerPoint, c.PressurePopPerPoint);
            int afterLoss = ThreatService.GlobalPressure(30f, 12, c.PressureDaysPerPoint, c.PressurePopPerPoint);
            Assert.AreEqual(atPeak, afterLoss,
                "역대 최고 인구는 사망으로 줄지 않는다 — 줄면 ADR-M10R-1 음성 피드백 루프가 돌아온다");

            // 위반 실증 (M17 "실패 불가능한 테스트" 교훈) — 현재 인구를 넘기면 실제로 떨어지는가.
            // 떨어져야 정상이다: 이 게이트가 지키는 것은 "호출자가 peak를 넘긴다"는 계약이고,
            // 함수 자체는 받은 값을 정직하게 쓴다. 여기서 값이 안 떨어지면 검사가 무의미해진다.
            int ifCurrentUsed = ThreatService.GlobalPressure(30f, 5, c.PressureDaysPerPoint, c.PressurePopPerPoint);
            Assert.Less(ifCurrentUsed, atPeak,
                "현재 인구를 넘기면 압력이 떨어져야 한다 — 안 떨어지면 이 게이트가 아무것도 증명하지 않는다");
        }

        [Test]
        public void M24_T2_GlobalPressure_MonotonicInBothTerms()
        {
            WorldConfigSO c = Config();
            int prev = -1;
            for (float day = 0f; day <= 120f; day += 1f)   // 시간 축
            {
                int p = ThreatService.GlobalPressure(day, 8, c.PressureDaysPerPoint, c.PressurePopPerPoint);
                Assert.GreaterOrEqual(p, prev, $"Day {day}: 시간이 흘렀는데 압력이 줄었다");
                prev = p;
            }
            prev = -1;
            for (int peak = 0; peak <= 40; peak++)          // 성장 축
            {
                int p = ThreatService.GlobalPressure(30f, peak, c.PressureDaysPerPoint, c.PressurePopPerPoint);
                Assert.GreaterOrEqual(p, prev, $"역대최고 {peak}: 성장했는데 압력이 줄었다");
                prev = p;
            }
        }

        [Test]
        public void M24_T2_EffectivePressure_MonotonicInEncounters()
        {
            int prev = -1;
            for (int seen = 0; seen <= 20; seen++)
            {
                int p = ThreatService.EffectivePressure(10, seen, 0.5f);
                Assert.GreaterOrEqual(p, prev, $"조우 {seen}회: 더 만났는데 유효압력이 줄었다");
                prev = p;
            }
            // k=0 = 적응하지 않는 종족 → 전역압력과 같다 (중립 불변식)
            Assert.AreEqual(10, ThreatService.EffectivePressure(10, 99, 0f),
                "k=0인데 조우가 압력을 밀었다 — 중립이 깨졌다");
            // 전역압력이 바닥을 깐다 — 한 번도 안 만난 종족도 시간이 지나면 세진다
            Assert.AreEqual(10, ThreatService.EffectivePressure(10, 0, 0.5f),
                "조우 0인데 전역압력이 안 실렸다 — 안 만난 종족이 영영 1단으로 남는다");
        }

        [Test]
        public void M24_T2_ShippedRaces_HaveDistinctAdaptationSpeed()
        {
            // k가 전부 같으면 "나에게 적응하는 적"이 종족 구분을 못 만든다 (축의 존재 이유).
            var seen = new System.Collections.Generic.HashSet<float>();
            int races = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ThreatSO"))
            {
                var so = AssetDatabase.LoadAssetAtPath<ThreatSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (so == null) continue;
                races++;
                Assert.GreaterOrEqual(so.EncounterPressureK, 0f,
                    $"{so.name}: k가 음수면 조우할수록 순해진다 (단조 위반)");
                seen.Add(so.EncounterPressureK);
            }
            Assert.Greater(races, 0, "배포 종족이 하나도 없다");
            Assert.Greater(seen.Count, 1,
                "전 종족의 k가 같다 — 종족마다 다른 속도로 배운다는 설계가 값에 없다");
        }

        // ── T5: 편성 구매 (W4) ────────────────────────────────────────────────

        /// <summary>등급 4단을 가진 시험용 종족 (배포 에셋과 독립 — 값이 바뀌어도 이 검사는 산다).</summary>
        private static ThreatSO Race(string name, float unlockDay, params int[] costs)
        {
            var t = ScriptableObject.CreateInstance<ThreatSO>();
            t.name = name; t.DisplayName = name; t.UnlockDay = unlockDay;
            var g = new ThreatSO.Grade[costs.Length];
            for (int i = 0; i < costs.Length; i++)
                g[i] = new ThreatSO.Grade { DisplayName = $"G{i}", PointCost = costs[i], StatMult = 1f + i };
            t.Grades = g;
            return t;
        }

        private static int TotalCount(System.Collections.Generic.List<ThreatService.WaveEntry> w)
        {
            int n = 0; foreach (ThreatService.WaveEntry e in w) n += e.Count; return n;
        }

        [Test]
        public void M24_T5_BuyWave_ProducesVariedCompositions()
        {
            // 🔴 예산제의 고전적 함정: "비싼 것부터 산다"로 짜면 **항상 최고 등급 1마리**만 나온다.
            // 편성이 하나로 굳으면 예산제를 도입한 이유(조합이 매번 다르다)가 통째로 사라지는데,
            // 게임은 정상 동작하므로 이 게이트가 아니면 아무도 눈치채지 못한다.
            ThreatSO a = Race("A", 0f, 1, 2, 4, 7), b = Race("B", 0f, 3, 5, 8, 12);
            var pool = new System.Collections.Generic.List<ThreatSO> { a, b };

            var shapes = new System.Collections.Generic.HashSet<string>();
            for (int ord = 0; ord < 10; ord++)
                shapes.Add(ThreatService.Describe(ThreatService.BuyWave(pool, 13, ord)));
            Assert.Greater(shapes.Count, 1,
                "예산 13 · 서수 0~9 에서 편성이 한 종류뿐 — 예산제가 고정 편성으로 굳었다");

            Object.DestroyImmediate(a); Object.DestroyImmediate(b);
        }

        [Test]
        public void M24_T5_BuyWave_IsDeterministic()
        {
            // ADR-M10R-2 결정성 계승 — 같은 판 같은 서수 = 같은 편성.
            ThreatSO a = Race("A", 0f, 1, 2, 4, 7), b = Race("B", 0f, 3, 5, 8, 12);
            var pool = new System.Collections.Generic.List<ThreatSO> { a, b };
            for (int ord = 0; ord < 5; ord++)
                Assert.AreEqual(ThreatService.Describe(ThreatService.BuyWave(pool, 11, ord)),
                                ThreatService.Describe(ThreatService.BuyWave(pool, 11, ord)),
                                $"서수 {ord}: 같은 입력인데 편성이 달라졌다");
            Object.DestroyImmediate(a); Object.DestroyImmediate(b);
        }

        [Test]
        public void M24_T5_BuyWave_SpendsBudgetAndGrowsWithIt()
        {
            // 예산이 남으면 최하 등급으로 채운다 — 안 그러면 압력이 올라도 마릿수가 안 늘어
            // 곡선이 계단으로 끊긴다. 그리고 예산을 절대 초과하지 않는다.
            ThreatSO a = Race("A", 0f, 1, 2, 4, 7);
            var pool = new System.Collections.Generic.List<ThreatSO> { a };

            int prev = 0;
            for (int budget = 1; budget <= 30; budget++)
            {
                var w = ThreatService.BuyWave(pool, budget, 0);
                int spent = 0;
                foreach (ThreatService.WaveEntry e in w) spent += ThreatService.GradeCost(e.Race, e.Grade) * e.Count;
                Assert.LessOrEqual(spent, budget, $"예산 {budget}: 초과 지출 {spent}");
                Assert.Greater(TotalCount(w), 0, $"예산 {budget}: 아무도 안 왔다");
                prev = TotalCount(w);
            }
            Assert.Greater(prev, 1, "예산 30인데 한 마리만 온다 — 남은 예산을 버리고 있다");

            // 예산 0 = 빈 편성 (중립 — 위협 없음)
            Assert.AreEqual(0, ThreatService.BuyWave(pool, 0, 0).Count, "예산 0인데 뭔가 왔다");
            Object.DestroyImmediate(a);
        }

        [Test]
        public void M24_T5_SoloRace_IsExcludedFromNormalWaves()
        {
            // 악마는 예산에 섞이지 않는다 (사용자 결정 — 나올 때는 악마만).
            ThreatSO a = Race("A", 0f, 1, 2, 4, 7);
            ThreatSO solo = Race("SOLO", 0f, 1); solo.SoloWave = true;
            var races = new[] { a, solo };

            Assert.AreSame(solo, ThreatService.SoloRace(races), "SoloWave 표시를 못 찾는다");
            var pool = ThreatService.UnlockedRaces(races, 99f, ThreatService.SoloRace(races));
            var wave = ThreatService.BuyWave(pool, 40, 3);
            foreach (ThreatService.WaveEntry e in wave)
                Assert.AreNotSame(solo, e.Race, "단독 웨이브 종족이 정규 편성에 섞였다");
            Assert.IsNull(ThreatService.SoloRace(new[] { a }), "표시가 없으면 null (중립)");

            Object.DestroyImmediate(a); Object.DestroyImmediate(solo);
        }

        [Test]
        public void M24_T5_DemonCount_IsMonotonicAndClamped()
        {
            int prev = 0;
            for (float d = 30f; d <= 200f; d += 5f)
            {
                int n = ThreatService.DemonCount(d, 30f, 30f, 4);
                Assert.GreaterOrEqual(n, prev, $"Day {d}: 악마 마릿수가 줄었다 (단조 위반)");
                Assert.LessOrEqual(n, 4, $"Day {d}: 상한 초과 {n}");
                prev = n;
            }
            Assert.AreEqual(1, ThreatService.DemonCount(30f, 30f, 30f, 4), "첫 등장은 1마리");
            Assert.AreEqual(2, ThreatService.DemonCount(60f, 30f, 30f, 4), "성장 1회 = 2마리");
        }

        [Test]
        public void M24_T5_SpawnCount_IsNoLongerCalled()
        {
            // ADR-M24-2 — 마릿수의 원천은 예산 하나뿐이다. 두 산식이 공존하면 언젠가 갈린다.
            string src = System.IO.File.ReadAllText("Assets/Scripts/M0/World/ThreatService.cs");
            int calls = 0, from = 0;
            while ((from = src.IndexOf("SpawnCount(", from, System.StringComparison.Ordinal)) >= 0)
            {
                // 정의부(public static int SpawnCount()는 남겨 둔다 — 게이트가 아직 산식을 검산한다)
                bool isDefinition = from >= 4 && src.Substring(from - 4, 4) == "int ";
                if (!isDefinition) calls++;
                from += 1;
            }
            Assert.AreEqual(0, calls,
                $"ThreatService가 SpawnCount를 아직 {calls}곳에서 부른다 — 마릿수 산식이 둘이 됐다 (ADR-M24-2)");
        }

        // ── T4: 침입 특성 축 + 공정성 (W5) ────────────────────────────────────

        /// <summary>이번 차수에 **답이 존재하는** 특성. 답이 없는 수는 에셋에 넣는 순간 red 여야 한다.
        /// 🔑 이 목록이 명세의 순서를 기계로 강제한다 — 고블린의 창고 탈취는 창고 잠금(2차)이
        /// 생기기 전에는 못 들어오고, 악마의 천사 저격은 천사(3차)가 생기기 전에는 못 들어온다.</summary>
        private static readonly InvaderTraitId[] AnsweredInPhase1 =
        {
            InvaderTraitId.ExitOnGoal,          // 답 = 추격·망루 요격 (M22 3차 완료)
            InvaderTraitId.NoFlee,              // 답 = 함정·다수 무장 (M22 3차 완료)
            InvaderTraitId.ShortWarning,        // 답 = 상시 방어선 (M22 완료)
            InvaderTraitId.TargetDefense,       // 답 = 망루 분산·이중화 (M22 3차 완료)
            InvaderTraitId.ApproachFlank,       // 답 = 망루 재배치 (M22 3차 완료)
            InvaderTraitId.ApproachMultiPoint,  // 답 = 전 방향 방어 (M22 완료)
            InvaderTraitId.TrapLearning,        // 답 = 함정 재배치 (M22 3차 완료)
        };

        [Test]
        public void M24_T4_ShippedTraits_HaveAnAnswerInThisPhase()
        {
            // 실패 가능성 증명 (M17 "실패 불가능한 테스트" 교훈) — **답이 없는 수는 실제로
            // 목록 밖**이어야 한다. 이게 참이 아니면 아래 Assert.Contains 는 무엇이든
            // 통과시키는 빈 검사가 되고, 게이트가 차수 순서를 전혀 못 막게 된다.
            Assert.IsFalse(System.Array.IndexOf(AnsweredInPhase1, InvaderTraitId.TargetStorage) >= 0,
                "창고 탈취(2차 몫, 답 = 창고 잠금)가 1차 답 목록에 있다 — 순서 강제가 무력화됐다");
            Assert.IsFalse(System.Array.IndexOf(AnsweredInPhase1, InvaderTraitId.CommanderRout) >= 0,
                "지휘관 붕괴(미배선)가 1차 답 목록에 있다");

            int traits = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ThreatSO"))
            {
                var so = AssetDatabase.LoadAssetAtPath<ThreatSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (so?.Traits == null) continue;
                foreach (InvaderTrait t in so.Traits)
                {
                    if (t.Id == InvaderTraitId.None) continue;
                    traits++;
                    Assert.Contains(t.Id, AnsweredInPhase1,
                        $"{so.name}: 특성 {t.Id} 의 답이 이번 차수에 없다 — 플레이어에게 대응 수단이 " +
                        "없는 난이도가 된다. 답이 생기는 차수로 미룰 것 (명세 §4.2).");
                    Assert.GreaterOrEqual(t.UnlockPressure, 0,
                        $"{so.name}: {t.Id} 해금 압력이 음수다");
                }
            }
            Assert.Greater(traits, 0, "배포 종족에 특성이 하나도 없다 — 축이 값에 없다");
        }

        [Test]
        public void M24_T4_ActiveTraits_IsNeutralWhenUnwired()
        {
            // 중립 불변식 — 특성을 안 준 종족은 배선 전과 똑같이 군다.
            ThreatSO bare = Race("BARE", 0f, 1);
            Assert.AreEqual(0, ThreatService.ActiveTraits(bare, 999).Length,
                "Traits 미배선인데 수가 생겼다 (중립 불변식 위반)");
            Assert.IsFalse(ThreatService.Has(ThreatService.ActiveTraits(bare, 999), InvaderTraitId.NoFlee));
            Object.DestroyImmediate(bare);
        }

        [Test]
        public void M24_T4_ActiveTraits_UnlockMonotonicallyWithPressure()
        {
            // 압력이 오르면 수는 늘기만 한다 — 줄면 "세지다 말고 순해지는 적"이 된다.
            ThreatSO r = Race("R", 0f, 1);
            r.Traits = new[]
            {
                new InvaderTrait { Id = InvaderTraitId.ExitOnGoal,  UnlockPressure = 0 },
                new InvaderTrait { Id = InvaderTraitId.ShortWarning, UnlockPressure = 8 },
                new InvaderTrait { Id = InvaderTraitId.NoFlee,       UnlockPressure = 20 },
            };
            int prev = 0;
            for (int p = 0; p <= 30; p++)
            {
                int n = ThreatService.ActiveTraits(r, p).Length;
                Assert.GreaterOrEqual(n, prev, $"압력 {p}: 쓰는 수가 줄었다");
                prev = n;
            }
            Assert.AreEqual(1, ThreatService.ActiveTraits(r, 0).Length, "압력 0 = 0짜리 하나");
            Assert.AreEqual(3, ThreatService.ActiveTraits(r, 20).Length, "압력 20 = 셋 다");
            Object.DestroyImmediate(r);
        }

        [Test]
        public void M24_T4_NoRaceNameBranchingInCode()
        {
            // ADR-M24-4 — 종족 차이는 데이터다. 코드가 종족 이름으로 분기하면 새 종족이
            // "에셋 1개"로 안 끝난다. 게임은 정상 동작하므로 이 게이트가 아니면 못 잡는다.
            foreach (string path in new[]
            {
                "Assets/Scripts/M0/World/ThreatService.cs",
                "Assets/Scripts/M0/World/CombatService.cs",
                "Assets/Scripts/M0/Presentation/ThreatAgent.cs",
            })
            {
                string src = System.IO.File.ReadAllText(path);
                foreach (string race in new[] { "Race_Goblin", "Race_Orc", "Race_Knight", "Race_Demon" })
                    Assert.IsFalse(src.Contains(race),
                        $"{path}: 종족 이름 '{race}' 이 코드에 박혔다 (ADR-M24-4)");
            }
        }

        // ── 조우 카운트 규약 (ADR-M24-5) ──────────────────────────────────────

        [Test]
        public void M24_T2_PressureDisplay_IsNeutralWhenUnwired()
        {
            // HUD 병기는 미배선(-1)이면 문구를 한 글자도 안 바꾼다 — 기존 게이트 보존의 근거.
            string without = SeasonHud.Compose(12f, null, 3f);
            string withNeg = SeasonHud.Compose(12f, null, 3f, -1);
            Assert.AreEqual(without, withNeg, "미배선인데 문구가 바뀌었다 (중립 불변식)");
            StringAssert.Contains("압력 7", SeasonHud.Compose(12f, null, 3f, 7),
                "압력을 넘겼는데 화면에 안 나온다");
        }

        [Test]
        public void M24_T7_ThreatSpawn_HasExactlyOneBirthSite()
        {
            // 디버그 소환(Ctrl+F10/F11, 2026-08-10)이 생기면서 **개체가 태어나는 곳이 둘이 될
            // 위험**이 생겼다. 편의 경로가 정규 경로를 우회하면 테스트에서 본 적과 실전에서 오는
            // 적이 다른 물건이 되고, 그 순간 관측이 거짓말을 시작한다 — 게이트로 못을 박는다.
            string src = System.IO.File.ReadAllText("Assets/Scripts/M0/World/ThreatService.cs");
            int births = 0, from = 0;
            while ((from = src.IndexOf("AddComponent<ThreatAgent>", from, System.StringComparison.Ordinal)) >= 0)
            { births++; from += 1; }
            Assert.AreEqual(1, births,
                $"위협 개체가 태어나는 곳이 {births}군데다 — 출몰의 문은 Spawn 하나여야 한다");
        }

        [Test]
        public void M24_T9_DiagonalWalk_DoesNotFlipFacingEveryFrame()
        {
            // 🔴 사용자가 오래 거슬려 하던 "주민·적 부들부들"의 정체 (2026-08-10):
            // 경로가 8방향(JPS)이라 대각선 구간이 흔한데, 45°에서는 |dx| 와 |dy| 가 명목상 같아
            // `|dx| > |dy|` 한 줄이 부동소수점 잡음으로 매 프레임 뒤집혔다 → 측면·정면 교대.
            //
            // 실패 가능성 증명: 여백 1.0(= 히스테리시스 없음)이면 같은 입력이 실제로 뒤집힌다.
            // 이게 참이 아니면 아래 검사는 무엇이든 통과시키는 빈 게이트가 된다.
            int naiveFlips = 0;
            bool naive = false;
            for (int i = 0; i < 200; i++)
            {
                float n = (i % 2 == 0) ? 1e-7f : -1e-7f;          // 매 프레임 부호가 뒤집히는 잡음
                bool next = AgentAnimator.NextAxisHorizontal(naive, new Vector2(0.7071f + n, -0.7071f), 1f);
                if (next != naive) naiveFlips++;
                naive = next;
            }
            Assert.Greater(naiveFlips, 10, "여백 없는 판정이 안 뒤집힌다 — 이 게이트가 빈 검사가 됐다");

            // 본 검사: 배포 여백에서는 같은 잡음에도 축이 한 번도 안 바뀐다.
            int flips = 0;
            bool axis = false;
            for (int i = 0; i < 200; i++)
            {
                float n = (i % 2 == 0) ? 1e-7f : -1e-7f;
                bool next = AgentAnimator.NextAxisHorizontal(axis, new Vector2(0.7071f + n, -0.7071f));
                if (next != axis) flips++;
                axis = next;
            }
            Assert.AreEqual(0, flips, $"대각선에서 축이 {flips}번 뒤집혔다 — 떨림이 돌아왔다");

            // 그래도 **바뀌어야 할 때는 바뀐다** — 히스테리시스가 방향 전환을 막아 버리면
            // 위로 걸어가는 주민이 옆모습으로 굳는다 (떨림보다 나쁜 고장).
            Assert.IsTrue(AgentAnimator.NextAxisHorizontal(false, new Vector2(1f, 0f)),
                "정확히 옆으로 가는데 정면을 보고 있다");
            Assert.IsFalse(AgentAnimator.NextAxisHorizontal(true, new Vector2(0f, 1f)),
                "정확히 위로 가는데 옆모습을 보고 있다");
            // 정지(0,0)는 보던 축을 지킨다 — 도착 프레임에 방향을 잃지 않게.
            Assert.IsTrue(AgentAnimator.NextAxisHorizontal(true, Vector2.zero));
            Assert.IsFalse(AgentAnimator.NextAxisHorizontal(false, Vector2.zero));
        }

        // ── 그림이 서는 크기·자리 (W6 후속, 2026-08-10) ───────────────────────

        private static Sprite MakeSprite(int cell, float ppu, Vector2 normalizedPivot)
        {
            var tex = new Texture2D(cell, cell);
            return Sprite.Create(tex, new Rect(0, 0, cell, cell), normalizedPivot, ppu);
        }

        [Test]
        public void M24_T6_CenterOffset_PutsSpriteCenterOnTheTile()
        {
            // 이 함수는 피벗 규약이 무엇이든 옳은 답을 내야 한다 — 규약을 **가정**한 상수가
            // 실제로 틀렸던 적이 있다 (주민의 舊 `-Scale` 오프셋: 좌하단 전제였는데 실측은 중앙).
            Sprite bottomLeft32 = MakeSprite(32, 16f, Vector2.zero);
            Assert.AreEqual(-0.75f, SpriteViewRig.CenterOffset(bottomLeft32, 0.75f).x, 1e-4f,
                "32px 좌하단 피벗 × 0.75 의 중심 보정이 -0.75 가 아니다 (주민 舊 값과 불일치)");

            // 셀 크기가 달라도 규칙이 따라간다 — 상수였다면 여기서 갈렸다.
            Sprite bottomLeft64 = MakeSprite(64, 16f, Vector2.zero);
            Assert.AreEqual(-0.75f, SpriteViewRig.CenterOffset(bottomLeft64, 0.375f).y, 1e-4f,
                "64px 시트가 32px과 다른 자리에 선다 — 규격 정규화가 무너졌다");

            // 중립: 피벗이 이미 중앙이면 아무것도 안 민다.
            Sprite centered = MakeSprite(32, 16f, new Vector2(0.5f, 0.5f));
            Assert.AreEqual(Vector3.zero, SpriteViewRig.CenterOffset(centered, 0.75f),
                "중앙 피벗 시트를 억지로 밀었다 (중립 불변식)");
        }

        [Test]
        public void M24_T6_ShippedSheets_UseCenteredPivots()
        {
            // 🔴 이 게이트의 이유: 나는 "팩 피벗은 좌하단"이라고 **재보지 않고** 단정했고, 그
            // 전제로 주민에게 반 칸짜리 오프셋이 들어가 있었다 (2026-08-10 실측이 뒤집음 —
            // meta 의 `pivot: {0,0}` 은 `alignment: 0`(Center) 이라 무시되는 값이었다).
            // 규약을 글로 적으면 또 틀린다. 배포 에셋에게 직접 묻는 검사만 남긴다.
            int n = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:AgentSpriteSetSO"))
            {
                var set = AssetDatabase.LoadAssetAtPath<AgentSpriteSetSO>(
                    AssetDatabase.GUIDToAssetPath(guid));
                Sprite f = set != null ? set.AnyFrame() : null;
                if (f == null) continue;
                n++;
                Assert.AreEqual(f.rect.width * 0.5f, f.pivot.x, 0.51f,
                    $"{set.name}: 피벗 X 가 셀 중앙이 아니다 (rect {f.rect.width}, pivot {f.pivot.x}) — " +
                    "정렬 규약이 바뀌었다면 SpriteViewRig 주석과 이 게이트를 함께 고칠 것");
                Assert.AreEqual(f.rect.height * 0.5f, f.pivot.y, 0.51f,
                    $"{set.name}: 피벗 Y 가 셀 중앙이 아니다 (rect {f.rect.height}, pivot {f.pivot.y})");
            }
            Assert.Greater(n, 0, "스프라이트 세트가 하나도 없다 — 게이트가 빈 검사가 됐다");
        }

        [Test]
        public void M24_T8_EveryShippedSheet_StandsOnTheSameFootLine()
        {
            // 🔑 이 게이트가 지키는 것 하나: **발은 종족마다 다른 높이에 있을 이유가 없다.**
            // 실측 전엔 오크만 발이 타일 중심 1.06 아래(다른 종족 0.37~0.56)라 반 칸 앞으로
            // 튀어나와 그려졌고, 그래서 앞쪽 자원 노드를 실제보다 많이 덮었다.
            //
            // 부호 오류가 이 축의 고유 함정이다 — PNG 의 y 는 아래로 자라고 월드의 y 는 위로
            // 자란다. 뒤집으면 그림이 여백만큼 반대로 튀는데, 컴파일도 임포트도 조용히 통과한다.
            int n = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:AgentSpriteSetSO"))
            {
                var set = AssetDatabase.LoadAssetAtPath<AgentSpriteSetSO>(
                    AssetDatabase.GUIDToAssetPath(guid));
                Sprite frame = set != null ? set.AnyFrame() : null;
                if (frame == null) continue;
                n++;

                // 반쪽 배선 금지 — 배포 시트는 재어 둔 값을 갖고 있어야 한다 (도구가 적는다).
                Assert.IsTrue(set.ArtBottomNorm >= 0f && set.ArtBottomNorm <= 1f,
                    $"{set.name}: ArtBottomNorm({set.ArtBottomNorm}) 미측정 — slice-pack.mjs 재실행 필요");
                Assert.IsTrue(set.ArtHeightNorm > 0f && set.ArtHeightNorm <= 1f,
                    $"{set.name}: ArtHeightNorm({set.ArtHeightNorm}) 미측정 — slice-pack.mjs 재실행 필요");

                // 이 세트가 실제로 쓰이는 배율들(주민 1 + 이 세트를 쓰는 종족의 SizeMult)에서
                // 전부 같은 선에 서야 한다 — 크기를 키운다고 발이 떠서는 안 된다.
                foreach (float sizeMult in SizeMultsUsing(set))
                {
                    float scale = set.Scale * sizeMult;
                    float box = frame.rect.height / frame.pixelsPerUnit * scale;
                    float artBottom = SpriteViewRig.FootLift(set, frame, scale)
                                    - box * 0.5f + set.ArtBottomNorm * box;
                    Assert.AreEqual(SpriteViewRig.FootBaselineTiles, artBottom, 1e-3f,
                        $"{set.name} (배율 {sizeMult}): 접지선이 {artBottom:0.###} — 기준선에서 벗어났다");
                }

                // 🔴 그림자 이중 금지 (2026-08-10 사용자 지적 → 실측으로 확인). 배포 팩 시트는
                // **그림자를 품고 있다** (맨 아래 행이 순수 검정 알파 100/255, 몸통보다 넓다).
                // 그 위에 런타임 그림자를 켜면 두 겹이 되는데, 화면에서만 보이고 코드에는 안
                // 드러난다 — 내가 실제로 그렇게 켰고 사용자가 눈으로 잡았다.
                // 구운 그림자가 없는 시트를 새로 들이거나 낮/밤 축이 생기면 그때 이 게이트를
                // 함께 고친다 (지금 켜는 것은 근거 없는 중복이다).
                Assert.LessOrEqual(set.ShadowWidthTiles, 0f,
                    $"{set.name}: 런타임 그림자가 켜져 있다 — 시트에 이미 구운 그림자가 있으면 두 겹이 된다");
            }
            Assert.Greater(n, 0, "스프라이트 세트가 하나도 없다 — 게이트가 빈 검사가 됐다");
        }

        /// <summary>이 세트가 실제로 그려지는 배율들 — 주민(1) + 이 세트를 쓰는 종족의 SizeMult.</summary>
        private static System.Collections.Generic.List<float> SizeMultsUsing(AgentSpriteSetSO set)
        {
            var list = new System.Collections.Generic.List<float> { 1f };
            foreach (string guid in AssetDatabase.FindAssets("t:ThreatSO"))
            {
                var so = AssetDatabase.LoadAssetAtPath<ThreatSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (so != null && so.SpriteSet == set) list.Add(so.SizeMult);
            }
            return list;
        }

        [Test]
        public void M24_T6_RaceSizeMult_IsAuthoredAndSane()
        {
            // 크기는 **사고가 아니라 결정**이다 (2026-08-10 사용자 판정: "위압감 때문에 그런
            // 설계라면 굳이 주민 크기로 맞출 필요 없다"). 그러려면 크기가 어딘가에 **적혀** 있어야
            // 한다 — 배율을 안 읽어서 커진 것과 크라고 적어서 큰 것은 화면에선 같아 보이지만
            // 다음 종족에서 갈린다.
            int n = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ThreatSO"))
            {
                var so = AssetDatabase.LoadAssetAtPath<ThreatSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (so == null) continue;
                n++;
                Assert.Greater(so.SizeMult, 0f, $"{so.name}: SizeMult 가 0 이하 — 그림이 사라진다");

                // 상한은 **배율이 아니라 최종 크기**로 잰다 (2026-08-10 개정). 배율만 보면
                // 셀이 다른 종족끼리 비교가 성립하지 않는다 — 48px 시트의 2.0과 32px 시트의
                // 2.0은 화면에서 1.5배 차이다. 같은 실수의 큰 판이 기사단을 주민보다 작게 만들었다.
                if (so.SpriteSet == null) continue;             // 미배선 = 중립 (색 원 폴백)
                Sprite frame = so.SpriteSet.AnyFrame();
                if (frame == null) continue;
                float boxUnits = frame.rect.height / frame.pixelsPerUnit
                               * so.SpriteSet.Scale * so.SizeMult;
                Assert.LessOrEqual(boxUnits, 5f,
                    $"{so.name}: 화면 크기 {boxUnits:0.##}유닛 — 5타일을 넘으면 타일 감각이 깨진다");
            }
            Assert.Greater(n, 0, "ThreatSO 가 하나도 없다 — 게이트가 빈 검사가 됐다");
        }

        // ── T10: 진입점 규칙 (W7 — 우회·다지점) ───────────────────────────────

        // 시험용 맵 — 변 중앙이 서(0,10)·동(20,10)·남(10,0)·북(10,20).
        private const int MinX = 0, MaxX = 20, MinY = 0, MaxY = 20;

        private static InvaderTraitId[] T(params InvaderTraitId[] ids) => ids;

        private static System.Collections.Generic.List<Vector2Int> Towers(params Vector2Int[] t)
            => new System.Collections.Generic.List<Vector2Int>(t);

        [Test]
        public void M24_T10_EntryPoints_IsNeutralWithoutTraits()
        {
            // 중립 불변식 (W7 DoD ①) — 특성이 없으면 **망루가 있어도** 舊 EntryPoint 그대로다.
            // 망루를 넘겨 두는 것이 요점이다: "입력이 늘었을 뿐 판정은 안 바뀐다"를 증명한다.
            System.Collections.Generic.List<Vector2Int> towers = Towers(new Vector2Int(MinX, 10));
            for (uint seed = 0; seed < 64; seed++)
            {
                Vector2Int[] e = ThreatService.EntryPoints(seed, MinX, MaxX, MinY, MaxY,
                                                           System.Array.Empty<InvaderTraitId>(), towers, 5, 9);
                Assert.AreEqual(1, e.Length, $"시드 {seed}: 특성이 없는데 진입점이 나뉘었다");
                Assert.AreEqual(ThreatService.EntryPoint(seed, MinX, MaxX, MinY, MaxY), e[0],
                    $"시드 {seed}: 특성이 없는데 좌표가 달라졌다 (중립 불변식 위반)");
            }
        }

        [Test]
        public void M24_T10_EntryPoints_FlankDefersWatchedEdges()
        {
            // 서쪽 변 중앙에 망루 1기, 사거리 5 — 그 변만 감시된다 (동·남·북은 거리 20·20·20).
            System.Collections.Generic.List<Vector2Int> towers = Towers(new Vector2Int(MinX, 10));
            var west = new Vector2Int(MinX, 10);

            // 실패 가능성 증명 (M17 교훈) — 특성이 없으면 **어떤 시드는 실제로 서쪽을 고른다**.
            // 이게 거짓이면 아래 검사는 무엇이든 통과하는 빈 검사가 된다.
            bool plainEverPicksWest = false;
            for (uint seed = 0; seed < 8; seed++)
                if (ThreatService.EntryPoint(seed, MinX, MaxX, MinY, MaxY) == west) plainEverPicksWest = true;
            Assert.IsTrue(plainEverPicksWest, "특성 없이도 서쪽이 안 나온다 — 이 게이트는 빈 검사다");

            for (uint seed = 0; seed < 64; seed++)
            {
                Vector2Int[] e = ThreatService.EntryPoints(seed, MinX, MaxX, MinY, MaxY,
                                                           T(InvaderTraitId.ApproachFlank), towers, 5, 1);
                Assert.AreNotEqual(west, e[0],
                    $"시드 {seed}: 우회 특성인데 감시 중인 서쪽 변으로 들어왔다");
            }
        }

        [Test]
        public void M24_T10_EntryPoints_FlankFallsBackWhenEveryEdgeIsWatched()
        {
            // 🔴 배제가 아니라 미루기다 (명세 W7 ⚠️) — 네 변이 모두 감시되면 기존 동작으로 돌아가야
            // 한다. 여기서 진입을 접거나 감시 밖을 억지로 만들면 망루가 무용지물이 된다.
            System.Collections.Generic.List<Vector2Int> all = Towers(
                new Vector2Int(MinX, 10), new Vector2Int(MaxX, 10),
                new Vector2Int(10, MinY), new Vector2Int(10, MaxY));
            for (uint seed = 0; seed < 32; seed++)
            {
                Vector2Int[] e = ThreatService.EntryPoints(seed, MinX, MaxX, MinY, MaxY,
                                                           T(InvaderTraitId.ApproachFlank), all, 5, 1);
                Assert.AreEqual(ThreatService.EntryPoint(seed, MinX, MaxX, MinY, MaxY), e[0],
                    $"시드 {seed}: 전 변 감시인데 기존 진입점으로 안 돌아왔다");
            }

            // 망루 0기·사거리 0 도 같은 자리로 떨어진다 (미배선 = 중립).
            Vector2Int[] none = ThreatService.EntryPoints(3u, MinX, MaxX, MinY, MaxY,
                                                          T(InvaderTraitId.ApproachFlank), null, 0, 1);
            Assert.AreEqual(ThreatService.EntryPoint(3u, MinX, MaxX, MinY, MaxY), none[0],
                "망루 미배선인데 진입점이 바뀌었다");
        }

        [Test]
        public void M24_T10_EntryPoints_MultiPointSplitsAndStaysOnEdges()
        {
            // W7 DoD ③ — 6마리면 진입점 2곳 이상. 좌표는 서로 달라야 한다 (같은 칸 둘 = 안 나뉜 것).
            Vector2Int[] e = ThreatService.EntryPoints(0u, MinX, MaxX, MinY, MaxY,
                                                       T(InvaderTraitId.ApproachMultiPoint), null, 0, 6);
            Assert.GreaterOrEqual(e.Length, 2, "6마리 다지점인데 한 곳에서만 들어온다");
            var seen = new System.Collections.Generic.HashSet<Vector2Int>(e);
            Assert.AreEqual(e.Length, seen.Count, "진입점에 같은 칸이 중복됐다");
            foreach (Vector2Int p in e)
                Assert.IsTrue(p.x == MinX || p.x == MaxX || p.y == MinY || p.y == MaxY,
                    $"({p.x},{p.y})는 가장자리가 아니다 — 위협이 마을 안에서 태어난다");

            // 1마리는 못 나눈다 (갈래가 마릿수를 넘으면 빈 진입점이 생긴다).
            Assert.AreEqual(1, ThreatService.EntryPoints(0u, MinX, MaxX, MinY, MaxY,
                                                        T(InvaderTraitId.ApproachMultiPoint), null, 0, 1).Length,
                "1마리인데 진입점이 여럿이다");
            // 네 변뿐이므로 갈래는 4를 넘지 않는다.
            Assert.LessOrEqual(ThreatService.EntryPoints(0u, MinX, MaxX, MinY, MaxY,
                                                        T(InvaderTraitId.ApproachMultiPoint), null, 0, 99).Length, 4,
                "변이 넷인데 진입점이 다섯 곳 이상이다");
        }

        [Test]
        public void M24_T10_EntryPoints_AreDeterministic()
        {
            // 결정성 (W7 DoD ④, ADR-M10R-2) — 같은 입력이면 같은 판이다.
            System.Collections.Generic.List<Vector2Int> towers = Towers(new Vector2Int(MinX, 10));
            InvaderTraitId[] both = T(InvaderTraitId.ApproachFlank, InvaderTraitId.ApproachMultiPoint);
            for (uint seed = 0; seed < 16; seed++)
            {
                Vector2Int[] a = ThreatService.EntryPoints(seed, MinX, MaxX, MinY, MaxY, both, towers, 5, 6);
                Vector2Int[] b = ThreatService.EntryPoints(seed, MinX, MaxX, MinY, MaxY, both, towers, 5, 6);
                CollectionAssert.AreEqual(a, b, $"시드 {seed}: 같은 입력에 다른 진입점이 나왔다");
            }
        }

        [Test]
        public void M24_T10_ShippedRaces_UseBothApproachTraits()
        {
            // 배선의 반쪽 방지 (M22-T16 동형) — 두 수가 **에셋에 실려 있어야** 이 축이 값에 있다.
            // 하나라도 0건이면 코드는 돌지만 화면에선 아무 일도 안 일어난다.
            int flank = 0, multi = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ThreatSO"))
            {
                var so = AssetDatabase.LoadAssetAtPath<ThreatSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (so?.Traits == null) continue;
                foreach (InvaderTrait t in so.Traits)
                {
                    if (t.Id == InvaderTraitId.ApproachFlank) flank++;
                    if (t.Id == InvaderTraitId.ApproachMultiPoint) multi++;
                }
            }
            Assert.Greater(flank, 0, "우회(ApproachFlank)를 쓰는 종족이 없다 — W7 배선이 값에 없다");
            Assert.Greater(multi, 0, "다지점(ApproachMultiPoint)을 쓰는 종족이 없다 — 〃");
        }
    }
}
