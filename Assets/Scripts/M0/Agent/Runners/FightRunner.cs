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
    /// </summary>
    public sealed class FightRunner : ActionRunnerBase
    {
        private readonly FightActionSO _so;
        private ThreatAgent _target;
        private float _nextHitAt;   // 실시간 초 — 다음 타격 가능 시각
        private bool _spoke;

        public FightRunner(FightActionSO so) : base(so)
        {
            _so = so;
        }

        /// <summary>이번 타격 간격 — 무기 배율은 W5가 배선했다 (`MyHasWeapon` × 에셋 배율).
        /// 직업 배율(`JobSO.CombatDurationMult`)은 W8 몫 — 그때 두 번째 인자만 갈아 끼운다.
        /// ⚠️ 배율을 여기서 발명하지 않는다 — 곱 결합·클램프는 전부 HitInterval(순수, 게이트 T8)이 한다.</summary>
        private float Interval(VillagerAgent agent)
            => CombatService.HitInterval(_so.BaseHitSec, 1f, agent.MyHasWeapon, _so.WeaponHitSecMult);

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

            // 위협 타일 정확히가 아니라 곁 (겹쳐 서기·예약 충돌 방지 — TendRunner 패턴)
            MoveTarget = MapBounds.PickWalkableNear(agent.IsWalkable, _target.TileX, _target.TileY, 1);
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
                return Fail("대상 이동 — 재추적 (재계획이 새 위치로)"); // TendRunner와 같은 통로

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
