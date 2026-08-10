using UnityEngine;

namespace AIVillage.M0
{
    /// <summary>
    /// 교전 러너 (M21-W4) — 최근접 위협에게 다가가 타격 간격마다 CombatService를 부른다.
    /// **판정은 하지 않는다** (명세 ⚠️ 오해 위험 ③): 피해·도주선·사냥·드랍은 전부 서비스가 정하고,
    /// 여기는 "누가 누구를 언제 때리는가"만 안다.
    ///
    /// 대상 선정에서 **도주·퇴장 중인 개체는 제외**한다 (DoD ④ "무한 추격 0"):
    /// 이미 물러나는 것을 새 교전 대상으로 삼으면 러너가 마을 밖까지 따라간다.
    /// 격퇴는 그 자체로 이김이므로(ADR-M21-1) 쫓아갈 이유가 없다 — 도망가는 짐승을 잡는 것은
    /// 사냥꾼의 몫이고 그건 W8이다.
    ///
    /// 재검사 A2 (교전 지속): 진행 중인 교전은 **피격·부상으로 중단하지 않는다.** 부상 필터는
    /// 다음 참전 선발에서만 작동한다 — 인접한 위협 앞에서 감속 0.4로 등을 보이는 것이 오히려
    /// 자살이라, 지속이 생존적이다.
    ///
    /// 🔴 **쫓지 않는다 — 매복한다** (2026-08-08 Play가 잡은 개정, M8 보고 심부름의 교훈 이식).
    /// 舊 동작: 위협의 '현재 좌표'로 걷고, 도착 시 사거리 밖이면 즉시 Fail → 재계획이 또
    /// 그 순간의 좌표를 찍는다. 이동은 액션 시작 시 1회뿐이라(러너는 걷는 동안 틱되지 않는다)
    /// 움직이는 대상과 구조적으로 미수렴 — 주민 2.0 < 위협 2.5에 배회 재선정 0.5초가 겹쳐
    /// "주민이 위협의 전 좌표로 걸어간다"가 화면에 그대로 보였다 (사용자 Play 관측).
    /// 新 동작: 이동 목표 = 위협의 **돌아올 자리**(TargetTile — 배회 중심이자 타격 대상 곁,
    /// 정지 좌표다). 도착 후 사거리 밖이면 실패가 아니라 **대기** — 위협은 재타격 주기(0.25일
    /// = 25초)마다 어차피 돌아오고, 주민 타깃 위협은 제 발로 온다. 대기 상한(AmbushGiveUpSec)
    /// 초과 시에만 Fail — 쿨다운이 굶주림 등 하위 goal에 숨 쉴 틈을 준다 (교전 105 > 배고픔
    /// 100이라, 상한 없는 대기는 매복 아사가 된다).
    /// </summary>
    public sealed class FightRunner : ActionRunnerBase
    {
        private readonly FightActionSO _so;
        private ThreatAgent _target;
        private float _nextHitAt;   // 실시간 초 — 다음 타격 가능 시각
        private float _ambushSec;   // 사거리 밖 대기 누적 (실시간 초) — 사거리 안에 들면 리셋
        private bool _spoke;

        public FightRunner(FightActionSO so) : base(so)
        {
            _so = so;
        }

        /// <summary>이번 타격 간격 — 무기 배율은 W5, 직업 배율은 W8이 배선했다 (무장 사냥꾼 = 4배).
        /// ⚠️ 배율을 여기서 발명하지 않는다 — 곱 결합·클램프는 전부 HitInterval(순수, 게이트 T8)이 한다.</summary>
        private float Interval(VillagerAgent agent)
            => CombatService.HitInterval(_so.BaseHitSec, agent.CombatDurationMultOfJob(),
                                         agent.MyHasWeapon, _so.WeaponHitSecMult);

        private static int Dist(VillagerAgent a, ThreatAgent t)
            => Mathf.Abs(a.TileX - t.TileX) + Mathf.Abs(a.TileY - t.TileY);

        /// <summary>
        /// 🔴 **맞설 대상이 없어도 실패하지 않는다** (2026-08-07 Play에서 잡은 좀비 명령의 수정).
        /// 처음엔 대상 없음을 `Failed`로 뒀는데, 그러면 위협이 다 물러난 뒤 「싸워라」 명령이
        /// **영원히 남는다**: 실패 → goal 쿨다운 → 재선택 → 다시 실패의 순환이고, 명령 슬롯은
        /// 완수도 포기도 아니라 안 비워진다. 실제로 D가 노동으로 돌아간 뒤에도 명령을 쥐고 있었다.
        /// 이제 "칠 것이 없다"는 **완료**로 본다 — 싸우라고 했는데 적이 사라졌으면 그 일은 끝난 것이다.
        /// (Tick 첫 분기가 같은 판정을 하므로 규칙은 한 벌이다.)
        /// </summary>
        public override bool Prepare(VillagerAgent agent)
        {
            if (agent.Threats == null || agent.Combat == null) return true; // 위협 없는 판 — Tick이 즉시 완료
            if (!agent.Threats.TryGetNearestFightable(agent.TileX, agent.TileY, out _target))
                return true; // 대상 없음 — Tick이 완료 처리 (실패로 두면 명령이 영영 안 비워진다)

            _nextHitAt = Time.time; // 도착 즉시 첫 대 — 달려와서 4초 기다리면 그 사이에 먼저 맞는다
            if (Dist(agent, _target) <= _so.StrikeRangeTiles) return true; // 이미 사거리 — 제자리

            // 매복 지점 = 위협의 현재 타일이 아니라 **돌아올 자리** (헤더 주석 참조).
            // 곁 1타일 산개 — 겹쳐 서기·예약 충돌 방지 (TendRunner 패턴)
            Vector2Int ambush = _target.TargetTile;
            MoveTarget = MapBounds.PickWalkableNear(agent.IsWalkable, ambush.x, ambush.y, 1);
            return true;
        }

        public override RunnerResult Tick(VillagerAgent agent, float dt)
        {
            // 재타겟 (DoD ④): 대상이 죽거나 물러났으면 다음 위협으로, 없으면 완료.
            // Unity 파괴 비교 포함 — 사냥으로 소멸한 개체가 여기서 걸린다.
            if (_target == null || _target.IsFleeing || _target.IsExiting)
            {
                if (agent.Threats == null
                    || !agent.Threats.TryGetNearestFightable(agent.TileX, agent.TileY, out _target))
                    return RunnerResult.Succeeded; // 맞설 것이 없다 = 이 교전은 끝났다 (명령도 함께 비워진다)
                _nextHitAt = Time.time;
            }

            int dist = Dist(agent, _target);
            if (dist > _so.StrikeRangeTiles)
            {
                // 매복 대기 — 舊 "즉시 Fail → 재계획이 또 옛 좌표로"의 헛걸음 순환을 대체.
                // 기다리는 동안은 몸짓을 끈다 (2026-08-10 Play): 액션은 Running 이지만 아직
                // 아무도 안 치고 있다. 안 끄면 사거리 밖에서 허공에 칼질하는 그림이 되고,
                // 화면에서 "싸우는 중"과 "기다리는 중"이 같아진다.
                agent.SuppressActionGesture = true;
                _ambushSec += dt;
                if (_ambushSec >= _so.AmbushGiveUpSec)
                    return Fail("매복 상한 — 대상이 오지 않는다 (쿨다운 후 재계획)");
                return RunnerResult.Running;
            }
            _ambushSec = 0f;
            agent.SuppressActionGesture = false;
            agent.FaceTowards(_target.transform.position); // 응시 — 싸우는 그림은 서로를 봐야 성립한다

            if (!_spoke)
            {
                _spoke = true;
                Debug.Log($"[Combat] 교전 시작 — {agent.AgentId} vs {_target.So.DisplayName} " +
                          $"(간격 {Interval(agent):0.#}초 · 1대 {_so.HitDamage:0.#})");
            }

            if (Time.time < _nextHitAt) return RunnerResult.Running;
            _nextHitAt = Time.time + Interval(agent);

            HitOutcome outcome = agent.Combat.VillagerHit(agent.AgentId, _so.HitDamage, _target);
            // 격퇴·사냥이면 이 대상과의 교전은 끝 — 다음 틱의 재타겟이 다음 상대를 찾거나 완료한다.
            // 여기서 곧바로 Succeeded로 끝내지 않는 이유: 적이 여럿이면 계속 싸워야 한다 (W6 대비).
            if (outcome == HitOutcome.Missed && (_target == null || _target.IsFleeing || _target.IsExiting))
                return RunnerResult.Running; // 다음 틱 재타겟이 처리
            return RunnerResult.Running;
        }
    }
}
