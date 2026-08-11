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
    /// 액션 실행자 공통 골격 (소요 시간 타이머 + 기본값). ActionSO.CreateRunner()가 실행
    /// 1회마다 새 인스턴스를 만든다 (SO는 에셋이라 런타임 상태 금지 — 상태는 전부 러너에).
    /// FSM(VillagerAgent)은 이 추상 타입만 알고, 액션 이름 분기는 존재하지 않는다 — 다형
    /// 디스패치(ADR-M0-1). 舊 IActionRunner 인터페이스는 구현 계열이 이 클래스 하나뿐이라
    /// 2026-08-11 2차 감사에서 여기로 합쳤다 (다형성은 추상 클래스가 그대로 제공한다).
    /// </summary>
    public abstract class ActionRunnerBase
    {
        protected readonly ActionSO Action;
        protected float Elapsed;

        /// <summary>이동 목표 타일 (Prepare 성공 후 유효). null = 제자리 실행 (舊 BC4 계승).</summary>
        public Vector2Int? MoveTarget { get; protected set; }

        /// <summary>true = 러너가 세계 반영까지 직접 수행 (BuildRunner → ConstructionService).
        /// false = 에이전트가 SO Effects를 EffectApplier로 일괄 적용.</summary>
        public virtual bool AppliesOwnEffects => false;

        /// <summary>이번 실행에 걸린 직업 효율 배율 (M20-W10) — Prepare에서 1회 확정.
        /// 기본 1 = 중립(무직·비전문), &lt;1 = 남보다 빠름.
        /// **배율을 실제로 적용하는 러너가 보고한다** — HUD가 액션 타입을 분기해 다시 유도하면
        /// 판정이 이원화된다(ADR-M0-1 정신). 표기는 SeasonHud가 플레이어 언어로 옮긴다.</summary>
        public float DurationMult { get; protected set; } = 1f;

        /// <summary>이번 실행에만 쓸 몸짓 (M22-4차) — `None` = 액션 에셋의 `Anim` 그대로 (중립).
        ///
        /// 🔑 액션 하나가 **여러 대상을 상대할 때만** 쓴다: 개간은 나무도 돌도 치우는데
        /// `ActionSO.Anim`은 한 값뿐이라, 돌을 도끼로 패는 그림이 나온다 (사용자 관측 2026-08-10).
        /// ⚠️ **코드가 몸짓을 고르는 자리가 아니다** (`ADR-M23-1`) — 러너는 *다른 에셋이 이미 정해
        /// 둔 값*을 가리킬 뿐이다 (개간은 그 자원을 캐는 채집 액션의 `Anim`을 빌린다).
        /// ClearRunner만 재정의한다.</summary>
        public virtual AnimKind AnimOverride => AnimKind.None;

        public string FailReason { get; protected set; }

        protected ActionRunnerBase(ActionSO action)
        {
            Action = action;
        }

        /// <summary>실행 준비 (대상 노드 탐색·점유 등). false면 FailReason과 함께 플랜 중단.</summary>
        public virtual bool Prepare(VillagerAgent agent) => true;

        /// <summary>도착 후 실행 틱 (0.1초 간격).</summary>
        public abstract RunnerResult Tick(VillagerAgent agent, float dt);

        /// <summary>성공/실패/중단 공통 정리 (노드 점유 해제 등). 에이전트가 반드시 호출한다.</summary>
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
