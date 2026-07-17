using AIVillage.M0;
using NUnit.Framework;
using UnityEngine;

namespace AIVillage.Tests.EditMode
{
    /// <summary>
    /// M8 사회 축 게이트 (명세 M8-A~D). 이 파일이 M8-T1~T4의 집이다 —
    /// T1(관계 축적·단짝/원한 판정)을 시작으로 T2(부탁 판정)·T3(소유)·T4(에셋 정책)가 뒤에 추가된다.
    /// </summary>
    public class M8_SocialGates
    {
        // ── M8-T1: 관계 축적 (ADR-M8-1 — 쓰기 단일 지점의 계약) ────────────────

        [Test]
        public void M8_T1_Affinity_AccumulateAndClamp()
        {
            var rel = new RelationshipService();

            Assert.AreEqual(0, rel.AffinityOf("A", "B"), "미기록 = 0 (중립)");

            rel.AddAffinity("A", "B", -3, "테스트");
            rel.AddAffinity("A", "B", -3, "테스트");
            Assert.AreEqual(-6, rel.AffinityOf("A", "B"), "누적");
            Assert.AreEqual(0, rel.AffinityOf("B", "A"), "방향성 — 역방향은 독립");

            for (int i = 0; i < 50; i++) rel.AddAffinity("A", "B", -10, "테스트");
            Assert.AreEqual(-100, rel.AffinityOf("A", "B"), "하한 클램프 -100");

            for (int i = 0; i < 50; i++) rel.AddAffinity("C", "D", 10, "테스트");
            Assert.AreEqual(100, rel.AffinityOf("C", "D"), "상한 클램프 +100");
        }

        [Test]
        public void M8_T1_Affinity_ZeroDeltaLeavesNoRecord()
        {
            var rel = new RelationshipService();
            rel.AddAffinity("A", "B", 0, "중립 대화");
            // 중립 불변식: 델타 0 대화는 기록 자체가 없다 — 원한/단짝 판정에 등장 불가
            Assert.IsFalse(rel.TryGetExtreme("A", buddy: false, threshold: 0, out _),
                "델타 0은 기록을 만들지 않는다 (사전 크기 불변)");
            Assert.IsFalse(rel.TryGetExtreme("B", buddy: false, threshold: 0, out _));
        }

        [Test]
        public void M8_T1_Buddy_RequiresMutual()
        {
            var rel = new RelationshipService();
            rel.AddAffinity("A", "B", 25, "테스트");

            Assert.IsFalse(rel.IsBuddy("A", "B", 20), "한쪽 짝사랑은 단짝이 아니다 (상호성)");

            rel.AddAffinity("B", "A", 25, "테스트");
            Assert.IsTrue(rel.IsBuddy("A", "B", 20), "쌍방 문턱 이상 — 단짝 성립");
            Assert.IsTrue(rel.IsBuddy("B", "A", 20), "단짝은 대칭");
        }

        [Test]
        public void M8_T1_Grudge_DirectionalAndExtremes()
        {
            var rel = new RelationshipService();
            rel.AddAffinity("A", "B", -25, "잔소리");
            rel.AddAffinity("A", "C", -10, "잔소리");

            Assert.IsTrue(rel.IsGrudge("A", "B", -20), "-25 < -20 — 원한");
            Assert.IsFalse(rel.IsGrudge("A", "C", -20), "-10 ≥ -20 — 아직 원한 아님");
            Assert.IsFalse(rel.IsGrudge("B", "A", -20), "방향성 — 역방향 무관");

            Assert.IsTrue(rel.TryGetExtreme("A", buddy: false, threshold: -20, out string worst),
                "원한 극단 조회");
            Assert.AreEqual("B", worst, "최저 친밀 상대 = B (-25)");
            Assert.IsFalse(rel.TryGetExtreme("A", buddy: true, threshold: 20, out _),
                "단짝 없음 — false");
        }

        [Test]
        public void M8_T1_ReleaseBy_RemovesBothDirections()
        {
            var rel = new RelationshipService();
            rel.AddAffinity("A", "B", -25, "테스트");
            rel.AddAffinity("B", "A", -5, "테스트");
            rel.AddAffinity("A", "C", 30, "테스트");

            rel.ReleaseBy("B");
            Assert.AreEqual(0, rel.AffinityOf("A", "B"), "B 이탈 — A→B 기록 소거");
            Assert.AreEqual(0, rel.AffinityOf("B", "A"), "B 이탈 — B→A 기록 소거");
            Assert.AreEqual(30, rel.AffinityOf("A", "C"), "무관한 관계는 보존");
        }

        // ── M8-T3: 소유 축 (ADR-M8-3 — 타일 신원·단일 쓰기·클레임 패스) ─────────

        [Test]
        public void M8_T3_Assign_RoundTripAndOnePerAgent()
        {
            var own = new OwnershipService();
            var t1 = new Vector2Int(3, 4);
            var t2 = new Vector2Int(5, 6);

            Assert.IsFalse(own.TryGetOwned("A", SlotId.HouseCount, out _), "배정 전 — 무소유");

            own.Assign(t1, SlotId.HouseCount, "A", "테스트");
            Assert.IsTrue(own.TryGetOwned("A", SlotId.HouseCount, out Vector2Int got));
            Assert.AreEqual(t1, got, "배정 왕복");
            Assert.IsTrue(own.IsOwned(t1));

            own.Assign(t2, SlotId.HouseCount, "A", "테스트"); // 1인 1채 — 두 번째 배정 무시
            own.TryGetOwned("A", SlotId.HouseCount, out got);
            Assert.AreEqual(t1, got, "1인 1채 — 첫 배정 유지");
            Assert.IsFalse(own.IsOwned(t2), "두 번째 집은 무주로 남는다");

            own.Assign(t1, SlotId.HouseCount, "B", "테스트"); // 이미 주인 있는 집 — 무시
            own.TryGetOwned("A", SlotId.HouseCount, out got);
            Assert.AreEqual(t1, got, "선점 소유 유지");
            Assert.IsFalse(own.TryGetOwned("B", SlotId.HouseCount, out _), "가로채기 불가");
        }

        [Test]
        public void M8_T3_ReleaseBy_MakesVacantAndReclaimable()
        {
            var own = new OwnershipService();
            var t1 = new Vector2Int(3, 4);
            own.Assign(t1, SlotId.HouseCount, "A", "테스트");

            own.ReleaseBy("A");
            Assert.IsFalse(own.IsOwned(t1), "이탈 — 빈집");

            own.Assign(t1, SlotId.HouseCount, "B", "테스트");
            Assert.IsTrue(own.TryGetOwned("B", SlotId.HouseCount, out _), "빈집 재배정 가능");
        }

        [Test]
        public void M8_T3_ClaimPass_NearestWithoutCrossing()
        {
            var own = new OwnershipService();
            var houseNear = new Vector2Int(1, 0);  // A(0,0) 최근접
            var houseFar = new Vector2Int(10, 0);  // B(9,0) 최근접
            var candidates = new (string, Vector2Int)[] { ("A", new Vector2Int(0, 0)), ("B", new Vector2Int(9, 0)) };
            var built = new[] { houseNear, houseFar };

            own.ClaimPass(candidates, built, SlotId.HouseCount, requestInFlight: false);

            own.TryGetOwned("A", SlotId.HouseCount, out Vector2Int homeA);
            own.TryGetOwned("B", SlotId.HouseCount, out Vector2Int homeB);
            Assert.AreEqual(houseNear, homeA, "A는 가까운 집");
            Assert.AreEqual(houseFar, homeB, "B는 가까운 집 — 교차 배정 없음");
        }

        [Test]
        public void M8_T3_ClaimPass_DeferredWhileRequestInFlight()
        {
            var own = new OwnershipService();
            var candidates = new (string, Vector2Int)[] { ("A", new Vector2Int(0, 0)) };
            var built = new[] { new Vector2Int(1, 0) };

            own.ClaimPass(candidates, built, SlotId.HouseCount, requestInFlight: true);
            Assert.IsFalse(own.TryGetOwned("A", SlotId.HouseCount, out _),
                "부탁 진행 중 — 클레임 유예 (부탁자 우선권)");

            own.ClaimPass(candidates, built, SlotId.HouseCount, requestInFlight: false);
            Assert.IsTrue(own.TryGetOwned("A", SlotId.HouseCount, out _), "부탁 종료 후 재개");
        }

        // ── M8-T1: 대화 이벤트 → 관계 배선 (ChatterSO 델타 필드의 계약) ─────────

        [Test]
        public void M8_T1_ChatterEvent_AppliesDeltasPerAsset()
        {
            var c = ScriptableObject.CreateInstance<ChatterSO>();
            c.DisplayName = "잔소리";
            c.SpeakerToTargetDelta = -2;
            c.TargetToSpeakerDelta = -3;

            // 구독 핸들러 본체 — SimulationLoop 배선은 ApplyChat 호출 1줄 (ADR-M8-1)
            var rel = new RelationshipService();
            rel.ApplyChat(c, "S", "T");

            Assert.AreEqual(-2, rel.AffinityOf("S", "T"), "화자→상대 델타");
            Assert.AreEqual(-3, rel.AffinityOf("T", "S"), "상대→화자 델타 (듣는 쪽이 더 미워한다)");

            Object.DestroyImmediate(c);
        }
    }
}
