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
    }
}
