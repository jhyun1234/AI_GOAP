using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 직업 찾아가기 러너 (M17-R3) — VisitRunner의 재사용 가능 primitive를 직업 대상으로 옮긴 것.
    /// 지정 직업의 가장 가까운 주민 곁까지 이동하고 끝난다. 세계 상태는 건드리지 않는다.
    ///
    /// 조각 Y가 폐기한 "쫓아가기"의 재발이 아니다. 그때 문제는 **동속 추격이 수렴하지 않는 것**
    /// 이었는데, 여기서는 ①한 번 걷고 끝낸다(재추적은 다음 goal 선택이 한다) ②걷는 사이 대상이
    /// 멀어졌으면 실패가 아니라 조용한 성공으로 종료한다(VisitRunner와 동일 규약 — 경고 0·쿨다운 0)
    /// ③도착이 목적이 아니라 **부탁 성립 반경 안으로 들어가는 것**이 목적이라, 근처까지만 가면
    /// RequestService의 주기 스캔이 나머지를 한다.
    /// </summary>
    public sealed class SeekJobRunner : ActionRunnerBase
    {
        private readonly SeekJobActionSO _so;

        public SeekJobRunner(SeekJobActionSO so) : base(so)
        {
            _so = so;
        }

        public override bool Prepare(VillagerAgent agent)
        {
            if (_so.TargetJob == null)
            {
                FailReason = $"{_so.name}: TargetJob 미지정 — 대상 없는 이동";
                return false;
            }

            VillagerAgent target = agent.FindNearestWithJob(_so.TargetJob, _so.SearchRadiusTiles);
            if (target == null)
            {
                // 목수가 없는 판·전부 탐색 반경 밖 — 실패로 남겨 goal 쿨다운이 붙게 한다
                // (매 선택마다 헛도는 것보다 낫다. 직업 랜덤 배정이라 부재 판이 실재한다).
                FailReason = $"{_so.TargetJob.name} 주민이 탐색 반경({_so.SearchRadiusTiles}) 안에 없다";
                return false;
            }

            if (Manhattan(agent, target) <= _so.ArriveRadiusTiles) return true; // 이미 곁 — 제자리

            // 대상 타일 정확히가 아니라 곁의 통행 가능 타일 — 예약 충돌·겹쳐 서기 방지
            // (ConsumeRunner·VisitRunner 공통 패턴)
            MoveTarget = MapBounds.PickWalkableNear(agent.IsWalkable, target.TileX, target.TileY,
                                                   _so.ArriveRadiusTiles);
            return true;
        }

        public override RunnerResult Tick(VillagerAgent agent, float dt)
            => DurationElapsed(dt) ? RunnerResult.Succeeded : RunnerResult.Running;

        private static int Manhattan(VillagerAgent a, VillagerAgent b)
            => Mathf.Abs(a.TileX - b.TileX) + Mathf.Abs(a.TileY - b.TileY);
    }
}
