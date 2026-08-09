using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 망루 탑승 러너 (M22-3차 W3, ADR-M22-11) — 최근접 완공 망루 곁으로 가서 올라서고,
    /// 망루 사거리 안의 위협을 기존 판정 문(CombatService.VillagerHit)으로 때린다.
    ///
    /// 🔴 **탑승은 표현 이동이다** — 논리 타일·예약은 망루 **인접 칸** 그대로 두고 transform만
    /// 망루 위로 올린다. 망루는 BlocksMovement라 논리 위치를 그 칸에 두면 하강 시 FindPath의
    /// 출발 타일이 통행 불가 = 경로 실패가 난다 (경로·예약 시스템과 싸우지 않는다).
    ///
    /// 하강(성공) = 감지 해소: 트리거(ThreatNear)와 같은 원천(IsNearThreat)을 망루 기준으로
    /// 판정한다 (ADR-M22-12 — 판정 이원화 금지). 망루 소멸 = 즉시 실패(폴링 한 곳 — 이벤트
    /// 배선 없이 단일 판정). 탑승 해제는 성공·실패·중단 전부 Cleanup 한 문으로 (해제 단일 지점).
    /// </summary>
    public sealed class ManTowerRunner : ActionRunnerBase
    {
        private readonly ManTowerActionSO _so;
        private Vector2Int _towerTile;
        private bool _mounted;
        private Vector3 _standPos;  // 내려올 자리 (논리 타일의 표현 위치) — 탑승 전 저장
        private float _nextHitAt;   // 실시간 초 — 다음 타격 가능 시각 (FightRunner 동형)
        private float _idleSec;     // 사거리 내 무대상 누적 — WatchGiveUpSec 상한

        public ManTowerRunner(ManTowerActionSO so) : base(so)
        {
            _so = so;
        }

        public override bool Prepare(VillagerAgent agent)
        {
            if (agent.Defense == null
                || !agent.Defense.TryGetNearestBuiltTile(SlotId.WatchtowerCount,
                        new Vector2Int(agent.TileX, agent.TileY), out _towerTile))
            {
                FailReason = "오를 망루가 없다";
                return false; // 트리거(WatchtowerCount ≥ 1)와 어긋난 순간 = 방금 부서졌다
            }
            // 스탠드 = 망루 인접 걷기 가능 칸 (BuildRunner 차단 건물 시공 규약 동형)
            MoveTarget = MapBounds.PickWalkableNear(agent.IsWalkable, _towerTile.x, _towerTile.y, 1);
            _nextHitAt = Time.time;
            return true;
        }

        public override RunnerResult Tick(VillagerAgent agent, float dt)
        {
            // 망루 소멸 = 즉시 하강 (공성이 부쉈다 — ADR-M22-11: 파괴 시 탑승 해제, 부상 없음)
            if (agent.Defense == null
                || !agent.Defense.HasStructureAt(SlotId.WatchtowerCount, _towerTile))
                return Fail("망루가 무너졌다 — 내려온다");

            if (!_mounted)
            {
                _mounted = true;
                _standPos = agent.transform.position;
                agent.SetOnTower(true); // 희생 선정 제외 (높이 — ADR-M22-11)
                agent.transform.position = new Vector3(_towerTile.x, _towerTile.y + 0.45f, 0f); // 표현만
                Debug.Log($"[Tower] {agent.AgentId} 망루에 올랐다 @ ({_towerTile.x},{_towerTile.y})");
            }

            // 하강 판정 — 감지 해소 (트리거와 같은 원천, 배율 1 = 망루는 성격을 타지 않는 시설 감시)
            if (agent.Threats == null || !agent.Threats.IsNearThreat(_towerTile.x, _towerTile.y, 1f))
                return RunnerResult.Succeeded;

            // 요격 — 사거리·대상 기준은 주민이 아니라 **망루**다 (그게 올라온 이유다)
            FightActionSO src = _so.CombatSource;
            int range = M0SimulationLoop.Instance != null ? M0SimulationLoop.Instance.DefenseTowerRange : 0;
            if (src != null && range > 0 && agent.Combat != null
                && agent.Threats.TryGetNearestFightable(_towerTile.x, _towerTile.y, out ThreatAgent target)
                && Mathf.Abs(target.TileX - _towerTile.x) + Mathf.Abs(target.TileY - _towerTile.y) <= range)
            {
                _idleSec = 0f;
                if (Time.time >= _nextHitAt)
                {
                    // 간격·피해 = 지상 교전과 같은 값·같은 문 (곱 결합은 HitInterval — 발명 금지)
                    _nextHitAt = Time.time + CombatService.HitInterval(
                        src.BaseHitSec, agent.CombatDurationMultOfJob(), agent.MyHasWeapon, src.WeaponHitSecMult);
                    agent.Combat.VillagerHit(agent.AgentId, src.HitDamage, target);
                }
                return RunnerResult.Running;
            }

            // 감지는 남았는데 사거리에 아무도 없다 — 감시 대기 (상한 = 매복 아사 방지)
            _idleSec += dt;
            if (_idleSec >= _so.WatchGiveUpSec)
                return Fail("감시 상한 — 사거리에 아무도 오지 않는다 (쿨다운 후 재계획)");
            return RunnerResult.Running;
        }

        public override void Cleanup(VillagerAgent agent)
        {
            // 해제 단일 지점 — 성공(하강)·실패(붕괴·상한)·중단(상위 전환·부상) 전부 여기를 지난다
            if (_mounted)
            {
                agent.SetOnTower(false);
                agent.transform.position = _standPos; // 표현 복귀 — 논리 타일은 애초에 안 움직였다
            }
        }
    }
}
