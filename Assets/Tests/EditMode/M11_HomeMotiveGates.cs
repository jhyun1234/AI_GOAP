using AIVillage.M0;
using NUnit.Framework;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M11-G 집 동기 게이트 (M11-T6) — 노숙 방향 도피의 목적지 산출과 피격 경험의 영구성.
    /// 방향 도피는 "기지로 도망가지 않는다"가 핵심이다 (노숙 = 위험, 결정 7의 전제).
    /// 경계는 MapConfig 미활성 폴백(-50~49)을 쓴다 — EditMode에서 씬 없이 도는 값.
    /// </summary>
    public class M11_HomeMotiveGates
    {
        // ── 방향 도피 (FleeRunner.EscapeTile — 순수) ────────────────────────────

        [Test]
        public void M11_T6_EscapeTile_MovesDirectlyAwayFromThreat()
        {
            // 위협 (0,0), 주민 (2,0) → 반대 방향(+x)으로 6타일
            Vector2Int t = FleeRunner.EscapeTile(2, 0, 0, 0, 6);
            Assert.AreEqual(new Vector2Int(8, 0), t, "위협→주민 벡터를 그대로 연장");

            // 반대편이면 부호도 반대
            Assert.AreEqual(new Vector2Int(-8, 0), FleeRunner.EscapeTile(-2, 0, 0, 0, 6));
        }

        [Test]
        public void M11_T6_EscapeTile_DiagonalKeepsDirection()
        {
            // 대각(+1,+1) 방향 → 두 축 모두 거리만큼 (체비쇼프 눈금 = 이동 계층과 동일)
            Assert.AreEqual(new Vector2Int(7, 7), FleeRunner.EscapeTile(1, 1, 0, 0, 6));
        }

        [Test]
        public void M11_T6_EscapeTile_NeverRunsTowardBase()
        {
            // 주민이 기지(0,0) 반대편에 있고 위협이 기지 쪽에서 온다 → 기지에서 더 멀어져야 한다
            // (舊 기지 폴백이면 (0,0)으로 돌아갔다 — 그 회귀를 여기서 잡는다)
            Vector2Int t = FleeRunner.EscapeTile(5, 5, 3, 3, 6);
            Assert.Greater(Mathf.Abs(t.x) + Mathf.Abs(t.y), 5 + 5, "기지 방향 회귀 금지");
        }

        [Test]
        public void M11_T6_EscapeTile_ClampsToMapBounds()
        {
            // 경계(폴백 최대 49) 밖으로 나가는 도피는 잘린다
            Vector2Int t = FleeRunner.EscapeTile(48, 0, 40, 0, 30);
            MapBounds.Get(out _, out int maxX, out _, out _);
            Assert.AreEqual(maxX, t.x, "경계 클램프");
        }

        [Test]
        public void M11_T6_EscapeTile_SameTileStaysPut()
        {
            // 위협과 겹친 좌표 = 방향 없음 → 제자리 (방향을 지어내지 않는다, 랜덤 금지)
            Assert.AreEqual(new Vector2Int(3, 4), FleeRunner.EscapeTile(3, 4, 3, 4, 6));
            // 거리 0 설정도 제자리 (에셋 값 실수 방어)
            Assert.AreEqual(new Vector2Int(3, 4), FleeRunner.EscapeTile(3, 4, 0, 0, 0));
        }

        // ── 피격 경험의 스냅샷 배선 (MyWasAttacked) ──────────────────────────────

        [Test]
        public void M11_T6_Snapshot_WasAttackedIsNeutralByDefault()
        {
            var world = new WorldModel(new DiscoveryService(),
                                       ScriptableObject.CreateInstance<WorldConfigSO>());
            WorldSnapshot snap = world.BuildSnapshot(50, 10);
            Assert.AreEqual(0, snap.Get(SlotId.MyWasAttacked),
                            "미배선 기본 0 = 중립 (집 동기 goal·긴급 부탁 영구 불발 = M11-F 동작)");

            WorldSnapshot hit = world.BuildSnapshot(50, 10, wasAttacked: true);
            Assert.AreEqual(1, hit.Get(SlotId.MyWasAttacked), "피격 주입이 슬롯 26에 도달");
        }

        [Test]
        public void M11_T6_SlotId_WasAttackedIsBooleanSlot()
        {
            Assert.IsFalse(SlotIds.IsNumeric(SlotId.MyWasAttacked), "피격 경험은 논리형");
            Assert.IsFalse(SlotIds.IsPersonalStock(SlotId.MyWasAttacked), "개인 스톡이 아니다");
        }

        /// <summary>피격 경험의 영구성 — 값을 되돌리는 대입이 소스 어디에도 없어야 한다.
        /// 런타임 회복 경로(SimTick·MarkTended)를 인스턴스 없이 검증할 방법이 없으므로
        /// M0-T3 방식의 소스 스캔으로 대신한다 (쓰기 경로 자체를 봉인).</summary>
        [Test]
        public void M11_T6_WasAttacked_HasNoResetPath()
        {
            string root = System.IO.Path.Combine(Application.dataPath, "Scripts/M0");
            var hits = new System.Collections.Generic.List<string>();
            var reset = new System.Text.RegularExpressions.Regex(@"MyWasAttacked\s*=\s*(false|0)\b");
            foreach (string file in System.IO.Directory.GetFiles(root, "*.cs",
                                                                System.IO.SearchOption.AllDirectories))
            {
                string[] lines = System.IO.File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                    if (reset.IsMatch(lines[i]))
                        hits.Add($"{file.Replace('\\', '/')}:{i + 1}: {lines[i].Trim()}");
            }
            Assert.IsEmpty(hits, "피격 경험을 되돌리는 대입 발견 (영구성 위반 — 회복해도 잊지 않는다):\n"
                                 + string.Join("\n", hits));
        }
    }
}
