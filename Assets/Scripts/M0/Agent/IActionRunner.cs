using UnityEngine;

namespace AIVillage.M0
{
    public enum RunnerResult
    {
        Running,   // 실행 중 (다음 틱 계속)
        Succeeded, // 완료 — 에이전트가 효과 적용 후 다음 액션으로
        Failed,    // 실패 — 에이전트가 플랜 중단 (FailReason 참조)
    }

    /// <summary>
    /// 액션 실행자. ActionSO.CreateRunner()가 실행 1회마다 새 인스턴스를 만든다
    /// (SO는 에셋이라 런타임 상태 금지 — 상태는 전부 러너에).
    /// FSM(VillagerAgent)은 이 인터페이스만 알고, 액션 이름 분기는 존재하지 않는다.
    /// </summary>
    public interface IActionRunner
    {
        /// <summary>실행 준비 (대상 노드 탐색·점유 등). false면 FailReason과 함께 플랜 중단.</summary>
        bool Prepare(VillagerAgent agent);

        /// <summary>이동 목표 타일 (Prepare 성공 후 유효). null = 제자리 실행 (舊 BC4 계승).</summary>
        Vector2Int? MoveTarget { get; }

        /// <summary>도착 후 실행 틱 (0.1초 간격).</summary>
        RunnerResult Tick(VillagerAgent agent, float dt);

        /// <summary>성공/실패/중단 공통 정리 (노드 점유 해제 등). 에이전트가 반드시 호출한다.</summary>
        void Cleanup(VillagerAgent agent);

        /// <summary>true = 러너가 세계 반영까지 직접 수행 (BuildRunner → ConstructionService).
        /// false = 에이전트가 SO Effects를 EffectApplier로 일괄 적용.</summary>
        bool AppliesOwnEffects { get; }

        string FailReason { get; }
    }

    /// <summary>공통 골격: 소요 시간 타이머 + 기본값.</summary>
    public abstract class ActionRunnerBase : IActionRunner
    {
        protected readonly ActionSO Action;
        protected float Elapsed;

        public Vector2Int? MoveTarget { get; protected set; }
        public virtual bool AppliesOwnEffects => false;
        public string FailReason { get; protected set; }

        protected ActionRunnerBase(ActionSO action)
        {
            Action = action;
        }

        public virtual bool Prepare(VillagerAgent agent) => true;
        public abstract RunnerResult Tick(VillagerAgent agent, float dt);
        public virtual void Cleanup(VillagerAgent agent) { }

        /// <summary>DurationSec 경과 여부. 0이면 즉시 true (舊 도착 즉시 완료 보존).</summary>
        protected bool DurationElapsed(float dt)
        {
            Elapsed += dt;
            return Elapsed >= Action.DurationSec;
        }

        /// <summary>배율 판 (M19 — 효율 전문화, M20에서 노동 전반으로 확대).
        /// 소비처는 **노동 러너 4곳**뿐 — Build/Farm/Gather + Consume의 조리 플래그 분기.
        /// ⚠️ 위반 기준은 러너 목록이 아니라 행동의 성질이다: **식사·휴식·간호에 곱기 시작하면**
        /// "직업이 생존·소비 속도를 바꾸는" ADR-M5-3 위반 신호 (게이트 M20-T3이 조리 쪽을 감시).</summary>
        protected bool DurationElapsed(float dt, float durationMult)
        {
            Elapsed += dt;
            return Elapsed >= Action.DurationSec * Mathf.Max(0.05f, durationMult);
        }

        protected RunnerResult Fail(string reason)
        {
            FailReason = reason;
            return RunnerResult.Failed;
        }
    }
}
