using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using AIVillage.Core;
using AIVillage.AI;

namespace AIVillage.Tests
{
    /// <summary>
    /// [T19] F-B 침략 예고 스테이지 게이트 (ADR-B1 방어).
    /// 근거: Docs/F-B_침략예고_명세서.md §4 FB-6, §6 ADR-B1, Docs/CLAUDE.md ADR-12.
    ///
    /// - Case1: Rumor(D-3) → Confirmed(D-1) → Raid(D0) 순서·타이밍 유지.
    ///          같은 Tick 이중 전이 금지, 실제 침략까지 최소 WARNING_LEAD_DAYS_RUMOR 경과.
    /// - Case2: EvaluateRaidDecision 결과가 예고 도중 false로 뒤집히면 예고 취소·상태 초기화.
    /// - Case3: 레이드 격퇴 → 쿨다운 진입 시 예고 상태(_warningStage)도 함께 초기화.
    ///
    /// 회귀 시: 예고 없이 침략이 갑자기 개시되거나, Rumor/Confirmed가 뒤바뀌거나,
    /// 예고 유령 상태가 쿨다운 이후 남아 다음 사이클과 섞임.
    ///
    /// EditMode 실행 전략: FactionAI를 GameObject.AddComponent로 생성 (Awake만 발동, Start·
    /// 코루틴 미발동). 리플렉션으로 private 필드 조작 + private EvaluateAndExecuteGoal 호출.
    /// EvaluateRaidDecision 조건은 FOREST 팩션의 rawFoodStock<30 즉시 트리거로 만족.
    /// MessageBus.Instance는 null 상태로 두어 PublishInvasionWarning/PublishRaidDecision이
    /// LogWarning으로 안전하게 폴백하도록 함(각 메서드의 null 가드 참조).
    /// </summary>
    public class T19_InvasionWarningGates
    {
        // ─────────────────────────────────────────────────────────────
        // 상수 미러 (FactionAI private 상수와 값 일치 유지)
        // ─────────────────────────────────────────────────────────────
        private const int   WSTAGE_NONE      = 0;
        private const int   WSTAGE_RUMOR     = 1;
        private const int   WSTAGE_CONFIRMED = 2;
        private const float LEAD_DAYS_RUMOR     = 3f;
        private const float LEAD_DAYS_CONFIRMED = 1f;
        private const int   FACTION_FOREST      = 0;
        private const float FOREST_FOOD_RAID_THRESHOLD = 30f;

        private GameObject _go;
        private FactionAI  _fa;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("T19_FactionAI_TestHost");
            _fa = _go.AddComponent<FactionAI>();

            // 침략 트리거 만족: FOREST 팩션 rawFood<30이면 EvaluateRaidDecision 즉시 true
            SetField(_fa, "_factionId",             FACTION_FOREST);
            SetField(_fa, "_rawFoodStock",          10f);

            // 전력 균형: SurviveFaction/DefendBase 발동 회피
            SetField(_fa, "_factionStrength",        100f);
            SetField(_fa, "_initialFactionStrength", 100f);

            // 정찰 게이트 통과 상태로 시작 (Case별로 필요 시 뒤에서 재설정)
            SetField(_fa, "_playerStrengthKnown",    true);
            SetField(_fa, "_scoutStartDay",          999f);

            // 예고 스테이지 초기값 명시
            SetField(_fa, "_warningStage",           WSTAGE_NONE);
            SetField(_fa, "_expectedRaidDay",       -1f);
            SetField(_fa, "_isRaiding",              false);
            SetField(_fa, "_raidCooldownRemaining",  0f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
                _go = null;
                _fa = null;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Case1: Rumor → Confirmed → Raid 순서·타이밍 유지
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void Case1_Rumor_Then_Confirmed_Then_Raid_Order_Preserved()
        {
            const float DAY_TRIGGER = 10f;

            // ── Step 1: D=10에서 조건 최초 충족 → RUMOR 진입, ExpectedDay=13
            InvokeEval(playerStrength: 5f, currentGameDay: DAY_TRIGGER);
            Assert.AreEqual(WSTAGE_RUMOR, GetInt("_warningStage"),
                "최초 트리거 Tick에서 WSTAGE_RUMOR로 진입해야 한다.");
            Assert.AreEqual(DAY_TRIGGER + LEAD_DAYS_RUMOR, GetFloat("_expectedRaidDay"), 0.001f,
                "ExpectedRaidDay = currentDay + WARNING_LEAD_DAYS_RUMOR(3).");
            Assert.IsFalse(GetBool("_isRaiding"),
                "Rumor 단계에서 실제 침략은 개시되지 않아야 한다.");

            // ── Step 2: 같은 Tick 이중 전이 금지 — 같은 Day에 다시 호출해도 Rumor 유지
            InvokeEval(playerStrength: 5f, currentGameDay: DAY_TRIGGER);
            Assert.AreEqual(WSTAGE_RUMOR, GetInt("_warningStage"),
                "같은 Tick 이중 전이 금지: 첫 진입 즉시 Confirmed로 넘어가면 안 된다.");

            // ── Step 3: D=12 → leadRemain=1 → CONFIRMED 진입 (leadRemain<=LEAD_DAYS_CONFIRMED=1)
            InvokeEval(playerStrength: 5f, currentGameDay: 12f);
            Assert.AreEqual(WSTAGE_CONFIRMED, GetInt("_warningStage"),
                "D-1(leadRemain=1) 시점에 CONFIRMED로 전이해야 한다.");
            Assert.IsFalse(GetBool("_isRaiding"),
                "Confirmed 단계에서도 아직 실제 침략은 개시되지 않아야 한다.");

            // ── Step 4: D=13 → leadRemain=0 → 실제 침략 개시 + ResetWarningState
            InvokeEval(playerStrength: 5f, currentGameDay: 13f);
            Assert.IsTrue(GetBool("_isRaiding"),
                "D0(leadRemain<=0)에 실제 침략이 개시되어야 한다 (_isRaiding=true).");
            Assert.AreEqual(WSTAGE_NONE, GetInt("_warningStage"),
                "실제 침략 개시 후 다음 사이클 대비 _warningStage=NONE으로 초기화되어야 한다.");

            // ── 지연 시간 검증 (S1): Rumor → 실제 침략 지연이 LEAD_DAYS_RUMOR 이상
            const float DELAY = 13f - DAY_TRIGGER;
            Assert.GreaterOrEqual(DELAY, LEAD_DAYS_RUMOR,
                $"예고→실제 침략 지연 {DELAY}일 < LEAD_DAYS_RUMOR({LEAD_DAYS_RUMOR}) — 명세 S1 위반.");
        }

        // ─────────────────────────────────────────────────────────────
        // Case2: 조건 소멸 시 예고 취소
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void Case2_Condition_Cancels_Warning_Before_Raid()
        {
            // Step 1: RUMOR 진입 (D=10)
            InvokeEval(playerStrength: 5f, currentGameDay: 10f);
            Assert.AreEqual(WSTAGE_RUMOR, GetInt("_warningStage"),
                "사전 조건: RUMOR 진입.");

            // Step 2: rawFoodStock을 위협 임계값 위로 되돌림 → EvaluateRaidDecision=false
            // (FOREST 특수 트리거 해제 + 기본 _units=empty로 nearPlayerTerritory=false → 공통 트리거도 실패)
            SetField(_fa, "_rawFoodStock", FOREST_FOOD_RAID_THRESHOLD + 10f); // 40

            // Step 3: 다음 Tick 호출 → "예고 중 조건 소멸" 분기 진입 → ResetWarningState
            InvokeEval(playerStrength: 5f, currentGameDay: 11f);
            Assert.AreEqual(WSTAGE_NONE, GetInt("_warningStage"),
                "조건 소멸 후 _warningStage가 NONE으로 초기화되어야 한다.");
            Assert.AreEqual(-1f, GetFloat("_expectedRaidDay"), 0.001f,
                "_expectedRaidDay도 초기화(-1)되어야 한다.");
            Assert.IsFalse(GetBool("_isRaiding"),
                "실제 침략은 개시되지 않아야 한다.");
        }

        // ─────────────────────────────────────────────────────────────
        // Case3: 쿨다운 진입 시 예고 상태 초기화
        // ─────────────────────────────────────────────────────────────

        [Test]
        public void Case3_Cooldown_Resets_Warning_State()
        {
            // 사전 상태 세팅: 예고 CONFIRMED 도중 유닛 전멸(_units=empty + _isRaiding=true)
            SetField(_fa, "_warningStage",    WSTAGE_CONFIRMED);
            SetField(_fa, "_expectedRaidDay", 15f);
            SetField(_fa, "_isRaiding",       true);
            SetField(_fa, "_units",           new List<EnemyFSM>()); // CountAliveUnits()==0

            // EvaluateAndExecuteGoal 호출 → 첫 분기(레이드 격퇴 감지)에서 쿨다운 진입
            InvokeEval(playerStrength: 5f, currentGameDay: 14f);

            Assert.IsFalse(GetBool("_isRaiding"),
                "쿨다운 진입 시 _isRaiding=false로 리셋.");
            Assert.Greater(GetFloat("_raidCooldownRemaining"), 0f,
                "쿨다운 진입 시 _raidCooldownRemaining이 양수로 설정.");
            Assert.AreEqual(WSTAGE_NONE, GetInt("_warningStage"),
                "쿨다운 진입 시 _warningStage가 NONE으로 초기화되어야 한다 (ADR-B1).");
            Assert.AreEqual(-1f, GetFloat("_expectedRaidDay"), 0.001f,
                "쿨다운 진입 시 _expectedRaidDay도 초기화(-1).");
        }

        // ─────────────────────────────────────────────────────────────
        // Reflection 헬퍼
        // ─────────────────────────────────────────────────────────────

        private static readonly BindingFlags PRIV =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private void InvokeEval(float playerStrength, float currentGameDay)
        {
            typeof(FactionAI).GetMethod("EvaluateAndExecuteGoal", PRIV)
                .Invoke(_fa, new object[] { playerStrength, currentGameDay });
        }

        private void SetField(object target, string name, object value)
        {
            var f = target.GetType().GetField(name, PRIV);
            Assert.IsNotNull(f, $"FactionAI에 private 필드 '{name}'가 존재해야 한다.");
            f.SetValue(target, value);
        }

        private int   GetInt(string name)   => (int)  GetFieldValue(_fa, name);
        private float GetFloat(string name) => (float)GetFieldValue(_fa, name);
        private bool  GetBool(string name)  => (bool) GetFieldValue(_fa, name);

        private static object GetFieldValue(object target, string name)
        {
            var f = target.GetType().GetField(name, PRIV);
            Assert.IsNotNull(f, $"FactionAI에 private 필드 '{name}'가 존재해야 한다.");
            return f.GetValue(target);
        }
    }
}
