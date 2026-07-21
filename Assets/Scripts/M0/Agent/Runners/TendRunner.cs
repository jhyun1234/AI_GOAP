using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 간호 러너 (M10-B) — 최근접 부상자 곁으로 이동 후 완치까지 채널링.
    /// 대상의 부상 상태는 쓰지 않는다: MarkTended(간호 표시)만 틱마다 갱신하고, 회복 진행·완치는
    /// 부상자 본인의 SimTick이 수행한다 (ADR-M10-2 — 쓰기 단일 지점 유지).
    /// P0 인터럽트(간호자의 배고픔)가 자연스러운 손 떼기 — 표시가 만료되면 대상의 사망 계단이
    /// 재개된다 ("손이 모자람"이 유일한 실패 경로, 결정 11).
    /// </summary>
    public sealed class TendRunner : ActionRunnerBase
    {
        private readonly TendActionSO _so;
        private VillagerAgent _target;
        private bool _spoke;

        // 간호 표시 유효 여유(초) — 0.1초 시뮬 틱 간격을 덮는 알고리즘 상수 (게임 수치 아님)
        private const float TEND_MARGIN_SEC = 0.3f;

        public TendRunner(TendActionSO so) : base(so)
        {
            _so = so;
        }

        public override bool Prepare(VillagerAgent agent)
        {
            _target = agent.FindNearestInjured();
            if (_target == null)
            {
                FailReason = "간호 대상 없음 (완치·소멸 — 재평가로 해소)";
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
                return RunnerResult.Succeeded; // 완치 — 효과(InjuredCount 픽션) 적용 후 다음 액션

            int dist = Mathf.Abs(agent.TileX - _target.TileX) + Mathf.Abs(agent.TileY - _target.TileY);
            if (dist > _so.TendRangeTiles)
                return Fail("대상 이동 — 재추적 (재계획이 새 위치로)");

            if (!_spoke)
            {
                _spoke = true;
                string[] lines = agent.AgentConfig.TendLines;
                if (lines != null && lines.Length > 0)
                    agent.ShowTransient(lines[Random.Range(0, lines.Length)]);
                Debug.Log($"[Injury] {agent.AgentId}: 간호 시작 → {_target.AgentId}" +
                          $" (배율 {(agent.Job != null ? agent.Job.TendRecoveryMult : 1f):F1})");
            }

            // 간호 표시 — 틱마다 갱신 = 연속 간호. 배율은 직업 에셋 값 (없으면 1 = 중립, ADR-M5-4 사상)
            float mult = agent.Job != null ? agent.Job.TendRecoveryMult : 1f;
            _target.MarkTended(Time.time + TEND_MARGIN_SEC, mult);
            return RunnerResult.Running; // 완치까지 채널링
        }
    }
}
