using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 도망 러너 (M10-D) — 피신처 곁 통행 가능 타일로 이동, 도착 즉시 완료.
    /// 별도 인터럽트 경로 없음: 위협 감지(ThreatNear)로 Goal_Flee가 뜨면 기존
    /// "상위 goal 전환"(0.5초 재검사)이 하던 일을 끊는다 (명세 M10-D ⚠️① — 새 경로 금지).
    /// 위협이 여전히 근처면 다음 재평가가 다시 도망을 선택한다 (모닥불 곁 서성임 = 연출).
    /// </summary>
    public sealed class FleeRunner : ActionRunnerBase
    {
        private readonly FleeActionSO _so;

        public FleeRunner(FleeActionSO so) : base(so)
        {
            _so = so;
        }

        public override bool Prepare(VillagerAgent agent)
        {
            int cx, cy;
            if (agent.ResolveAnchor(_so.AnchorPriority, out Vector2Int safe)) // 내 집 우선 (M8-C)
            {
                cx = safe.x; cy = safe.y;
            }
            else if (agent.Threats != null
                     && agent.Threats.TryGetNearestThreatPos(agent.TileX, agent.TileY, out Vector2Int threat))
            {
                // 노숙 방향 도피 (M11-G) — 갈 집도 모닥불도 없으면 위협 반대 방향으로 달아난다.
                // 기지 폴백은 삭제됐다: 노숙이 위험해야 집이 값어치를 갖는다 (결정 7).
                Vector2Int away = EscapeTile(agent.TileX, agent.TileY, threat.x, threat.y,
                                             _so.FleeDistanceTiles);
                cx = away.x; cy = away.y;
            }
            else return true; // 위협 없음(ThreatNear 오탐)·미배선 — 제자리 완료 (⚠️③ 기지 폴백 금지)

            MoveTarget = MapBounds.PickWalkableNear(agent.IsWalkable, cx, cy, _so.AnchorRadius);
            return true;
        }

        /// <summary>
        /// 방향 도피 목적지 (순수 — 게이트 M11-T6): 위협→주민 벡터를 그대로 연장한 distance
        /// 지점, 맵 경계 클램프. 겹친 좌표(벡터 0)면 제자리 (방향을 지어내지 않는다 — 랜덤 금지).
        /// 정수 좌표계라 정규화 대신 긴 축 기준 비례 배분한다 (부동소수 반올림 편향 회피).
        /// </summary>
        public static Vector2Int EscapeTile(int agentX, int agentY, int threatX, int threatY, int distance)
        {
            int dx = agentX - threatX, dy = agentY - threatY;
            if (distance <= 0 || (dx == 0 && dy == 0)) return MapBounds.Clamp(agentX, agentY);

            int major = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)); // 체비쇼프 = 이동 계층의 눈금
            int tx = agentX + Mathf.RoundToInt((float)dx / major * distance);
            int ty = agentY + Mathf.RoundToInt((float)dy / major * distance);
            return MapBounds.Clamp(tx, ty);
        }

        public override RunnerResult Tick(VillagerAgent agent, float dt)
            => RunnerResult.Succeeded; // 도착 즉시 완료 (효과는 플래너 픽션 — 실제 해소는 위협 퇴장)
    }
}
