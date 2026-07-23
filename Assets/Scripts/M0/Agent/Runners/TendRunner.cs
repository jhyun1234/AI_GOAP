using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 간호 러너 (M10-B → M11-I 2단계) — 최근접 부상자 곁으로 이동 후 채널링.
    /// **치료사(Job.CanTreatInjury)는 완치까지, 일반 주민은 안정화 1회까지** (결정 15).
    /// 대상 = healer면 전 부상자, 일반이면 미안정 부상자만 (FindNearestInjured 이원화).
    /// 대상의 부상 상태는 쓰지 않는다: MarkTended만 틱마다 갱신하고 진행·완치는 대상의 SimTick이
    /// 수행한다 (ADR-M10-2 — 쓰기 단일 지점). P0 인터럽트가 자연스러운 손 떼기.
    /// </summary>
    public sealed class TendRunner : ActionRunnerBase
    {
        private readonly TendActionSO _so;
        private VillagerAgent _target;
        private bool _spoke;

        private static bool IsHealer(VillagerAgent agent)
            => agent.Job != null && agent.Job.CanTreatInjury;

        // 간호 표시 유효 여유(초) — 0.1초 시뮬 틱 간격을 덮는 알고리즘 상수 (게임 수치 아님)
        private const float TEND_MARGIN_SEC = 0.3f;

        public TendRunner(TendActionSO so) : base(so)
        {
            _so = so;
        }

        public override bool Prepare(VillagerAgent agent)
        {
            _target = agent.FindNearestInjured(IsHealer(agent));
            if (_target == null)
            {
                FailReason = "간호 대상 없음 (완치·안정화·소멸 — 재평가로 해소)";
                return false;
            }

            int dist = Mathf.Abs(agent.TileX - _target.TileX) + Mathf.Abs(agent.TileY - _target.TileY);
            if (dist <= _so.TendRangeTiles) return true; // 이미 곁 — 제자리 실행

            // 대상 타일 정확히가 아니라 곁의 통행 가능 타일 — 예약 충돌·겹쳐 서기 방지 (VisitRunner 패턴)
            MoveTarget = MapBounds.PickWalkableNear(agent.IsWalkable, _target.TileX, _target.TileY, 1);
            return true;
        }

        public override RunnerResult Tick(VillagerAgent agent, float dt)
        {
            if (_target == null || _target.State == AgentState.Dead)
                return Fail("간호 대상 소멸");
            if (_target.Injury == InjurySeverity.None)
                return RunnerResult.Succeeded; // 완치(누가 했든) — 다음 액션

            bool healer = IsHealer(agent);
            // 일반 간호자는 안정화가 곧 임무 완료 — 채널링을 멈추고 다음 미안정 부상자로 (crowding 해소).
            if (!healer && _target.IsStabilized)
                return RunnerResult.Succeeded;

            int dist = Mathf.Abs(agent.TileX - _target.TileX) + Mathf.Abs(agent.TileY - _target.TileY);
            if (dist > _so.TendRangeTiles)
                return Fail("대상 이동 — 재추적 (재계획이 새 위치로)");

            if (!_spoke)
            {
                _spoke = true;
                string[] lines = agent.AgentConfig.TendLines;
                if (lines != null && lines.Length > 0)
                    agent.ShowTransient(lines[Random.Range(0, lines.Length)]);
                Debug.Log($"[Injury] {agent.AgentId}: {(healer ? "치료" : "응급조치")} 시작 → {_target.AgentId}" +
                          $" (배율 {(agent.Job != null ? agent.Job.TendRecoveryMult : 1f):F1})");
            }

            // 간호 표시 — 틱마다 갱신. 치료사면 회복 진행, 일반이면 안정화(1회성) — 대상 SimTick이 가른다.
            float mult = agent.Job != null ? agent.Job.TendRecoveryMult : 1f;
            _target.MarkTended(Time.time + TEND_MARGIN_SEC, mult, healer);
            // 치료사 = 완치까지 채널링 / 일반 = 다음 틱 IsStabilized면 위에서 Succeeded로 빠진다
            return RunnerResult.Running;
        }
    }
}
