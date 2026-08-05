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
        private static void DestroyAll(params Object[] objs)
        {
            foreach (Object o in objs) Object.DestroyImmediate(o);
        }

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

            own.Assign(t1, SlotId.HouseCount, "B", "테스트"); // 부탁 완수 경로가 빈집을 재배정할 수 있다
            Assert.IsTrue(own.TryGetOwned("B", SlotId.HouseCount, out _), "빈집 재배정 가능");
        }

        // ClaimPass 게이트 2종은 자동 클레임 폐기(2026-07-18 사용자 결정 — 배정 경로는 부탁
        // 완수뿐)와 함께 삭제됨. 빈집 재활용 정책이 생기면 그때 새 게이트와 함께.

        // ── M8-T2: 부탁 판정 (ADR-M8-2 — 결정적, 바쁨→배고픔→피로→친밀) ────────

        private static GoalSO MakeGoal(string name, int priority)
        {
            var g = ScriptableObject.CreateInstance<GoalSO>();
            g.name = name;
            g.DisplayName = name;
            g.Priority = priority;
            return g;
        }

        private static WorldSnapshot Snap()
            => new WorldSnapshot(new int[PlanningConfig.TotalSlots]);

        [Test]
        public void M8_T2_JudgeRequest_ReasonOrder()
        {
            var cfg = ScriptableObject.CreateInstance<AgentConfigSO>();
            var r = ScriptableObject.CreateInstance<RequestSO>();
            r.RefuseAffinityBelow = -10;
            float satLimit = cfg.OrderRefuseSatiety;
            float fatLimit = cfg.OrderRefuseFatigue;

            // 전부 걸리는 입력 — 사유는 바쁨 → 배고픔 → 피로 → 친밀 순으로 하나씩 벗겨진다
            Assert.AreEqual(VillagerAgent.RequestResult.RefusedBusy,
                VillagerAgent.JudgeRequest(true, satLimit - 1f, fatLimit + 1f, -50, cfg, null, r),
                "바쁨이 최우선 사유");
            Assert.AreEqual(VillagerAgent.RequestResult.RefusedHungry,
                VillagerAgent.JudgeRequest(false, satLimit - 1f, fatLimit + 1f, -50, cfg, null, r),
                "배고픔이 피로·친밀에 우선");
            Assert.AreEqual(VillagerAgent.RequestResult.RefusedTired,
                VillagerAgent.JudgeRequest(false, 100f, fatLimit + 1f, -50, cfg, null, r),
                "피로가 친밀에 우선");
            Assert.AreEqual(VillagerAgent.RequestResult.RefusedLowAffinity,
                VillagerAgent.JudgeRequest(false, 100f, 0f, -50, cfg, null, r),
                "몸이 멀쩡하면 마음이 판정");
            Assert.AreEqual(VillagerAgent.RequestResult.Accepted,
                VillagerAgent.JudgeRequest(false, 100f, 0f, 0, cfg, null, r), "전부 통과 — 수락");

            DestroyAll(cfg, r);
        }

        [Test]
        public void M8_T2_JudgeRequest_AffinityBoundary()
        {
            var cfg = ScriptableObject.CreateInstance<AgentConfigSO>();
            var r = ScriptableObject.CreateInstance<RequestSO>();
            r.RefuseAffinityBelow = -10;

            Assert.AreEqual(VillagerAgent.RequestResult.Accepted,
                VillagerAgent.JudgeRequest(false, 100f, 0f, -10, cfg, null, r),
                "경계값 -10은 수락 ('미만'만 거절)");
            Assert.AreEqual(VillagerAgent.RequestResult.RefusedLowAffinity,
                VillagerAgent.JudgeRequest(false, 100f, 0f, -11, cfg, null, r), "-11은 거절");

            DestroyAll(cfg, r);
        }

        [Test]
        public void M8_T2_JudgeRequest_BodyThresholdsMatchJudgeOrder()
        {
            // 배고픔·피로 문턱은 촌장 명령과 동일 규칙 (성격 오프셋 포함) — 이원화 금지의 계약
            var cfg = ScriptableObject.CreateInstance<AgentConfigSO>();
            var p = ScriptableObject.CreateInstance<PersonalitySO>();
            p.RefuseSatietyOffset = 10f;
            var r = ScriptableObject.CreateInstance<RequestSO>();
            float shifted = cfg.OrderRefuseSatiety + 10f;

            Assert.AreEqual(VillagerAgent.OrderResult.RefusedHungry,
                VillagerAgent.JudgeOrder(shifted - 1f, 0f, cfg, p), "명령: 오프셋 문턱 미만 거부");
            Assert.AreEqual(VillagerAgent.RequestResult.RefusedHungry,
                VillagerAgent.JudgeRequest(false, shifted - 1f, 0f, 0, cfg, p, r), "부탁: 같은 문턱");
            Assert.AreEqual(VillagerAgent.RequestResult.Accepted,
                VillagerAgent.JudgeRequest(false, shifted, 0f, 0, cfg, p, r), "부탁: 경계값 수락도 동일");

            DestroyAll(cfg, p, r);
        }

        [Test]
        public void M8_T2_JudgeRequest_UpfrontDemand()
        {
            // 선불 요구 (ADR-보상2·보상4) — 판정 순서는 친밀 뒤 (몸→마음→조건)
            var cfg = ScriptableObject.CreateInstance<AgentConfigSO>();
            var r = ScriptableObject.CreateInstance<RequestSO>();
            r.RefuseAffinityBelow = -10;
            var upfront = ScriptableObject.CreateInstance<PersonalitySO>();
            upfront.DemandsRewardUpfront = true;

            Assert.AreEqual(VillagerAgent.RequestResult.RefusedNoReward,
                VillagerAgent.JudgeRequest(false, 100f, 0f, 0, cfg, upfront, r, upfrontAvailable: false),
                "선불 성격 + 지급 불가 = 거절(선불)");
            Assert.AreEqual(VillagerAgent.RequestResult.Accepted,
                VillagerAgent.JudgeRequest(false, 100f, 0f, 0, cfg, upfront, r, upfrontAvailable: true),
                "선불 성격 + 지급 가능 = 수락");
            Assert.AreEqual(VillagerAgent.RequestResult.RefusedLowAffinity,
                VillagerAgent.JudgeRequest(false, 100f, 0f, -50, cfg, upfront, r, upfrontAvailable: false),
                "친밀이 선불보다 먼저 판정 (사유 순서)");

            // 중립 불변식 (S4): 기본 성격·p null은 upfrontAvailable 무관 수락
            var neutral = ScriptableObject.CreateInstance<PersonalitySO>();
            Assert.AreEqual(VillagerAgent.RequestResult.Accepted,
                VillagerAgent.JudgeRequest(false, 100f, 0f, 0, cfg, neutral, r, upfrontAvailable: false),
                "기본 성격 = 후불 수용 (5ce720e 동작 동일)");
            Assert.AreEqual(VillagerAgent.RequestResult.Accepted,
                VillagerAgent.JudgeRequest(false, 100f, 0f, 0, cfg, null, r, upfrontAvailable: false),
                "성격 없음 = 후불 수용");

            DestroyAll(cfg, r, upfront, neutral);
        }

        [Test]
        public void M8_T2_ShouldStiffReward_BoundaryAndNeutral()
        {
            // 떼먹기 판정 (ADR-보상1) — 결정적, 문턱 '미만'만 떼먹음
            var stingy = ScriptableObject.CreateInstance<PersonalitySO>();
            stingy.SkipRewardBelowAffinity = 5;

            Assert.IsFalse(RequestService.ShouldStiffReward(stingy, 5), "경계값 5 = 지급 ('미만'만 떼먹음)");
            Assert.IsTrue(RequestService.ShouldStiffReward(stingy, 4), "4 = 떼먹음");

            var neutral = ScriptableObject.CreateInstance<PersonalitySO>();
            Assert.IsFalse(RequestService.ShouldStiffReward(neutral, -100),
                "기본값 -100 = 친밀 하한에서도 지급 (중립 불변식 — 절대 안 떼먹음)");
            Assert.IsFalse(RequestService.ShouldStiffReward(null, -100), "성격 없음 = 지급");

            DestroyAll(stingy, neutral);
        }

        [Test]
        public void M8_T2_JudgeRequest_Deterministic100x()
        {
            var cfg = ScriptableObject.CreateInstance<AgentConfigSO>();
            var r = ScriptableObject.CreateInstance<RequestSO>();
            VillagerAgent.RequestResult first =
                VillagerAgent.JudgeRequest(false, 50f, 50f, -3, cfg, null, r);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(first,
                    VillagerAgent.JudgeRequest(false, 50f, 50f, -3, cfg, null, r),
                    "동일 입력 = 동일 판정 (랜덤 0 — ADR-M8-2)");
            DestroyAll(cfg, r);
        }

        [Test]
        public void M8_T2_Select_RequestJoinsLadder_OrderWinsTie()
        {
            var selector = new GoalSelector(new GoalSO[0]);
            GoalSO order = MakeGoal("명령", 10);
            GoalSO request = MakeGoal("부탁", 10);

            Assert.AreSame(order, selector.Select(Snap(), null, order, null, null, request),
                "동률이면 명령 우선 (평가 순서 — ADR-M8-4)");

            GoalSO bigRequest = MakeGoal("급한 부탁", 11);
            Assert.AreSame(bigRequest, selector.Select(Snap(), null, order, null, null, bigRequest),
                "우선순위 높은 부탁은 명령을 이긴다 (사다리 경쟁)");

            Assert.AreSame(order, selector.Select(Snap(), null, order, null, null, null),
                "request null = 기존 동작과 완전 동일 (중립 불변식)");

            DestroyAll(order, request, bigRequest);
        }

        // M8-T4(집 부탁 에셋 정책 2종)는 M20-W12에서 삭제 — 집 부탁 사슬이 에셋째 삭제됐다
        // (ADR-M20-7 "집은 개인이 짓는다"). 부탁 메커니즘 게이트(M8-T2 계열)는 그대로다.

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
