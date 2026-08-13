"""동작 어휘가 실제로 뼈를 움직이는가. `bpy` 없이 돈다 — 그래서 매번 돌릴 수 있다."""
import math
import os
import motions

HERE = os.path.dirname(os.path.abspath(__file__))

BONES = {'hips', 'spine', 'neck', 'head'} | {
    '%s.%s' % (p, s) for s in ('L', 'R')
    for p in ('shoulder', 'upperarm', 'forearm', 'hand', 'thigh', 'shin', 'foot')}


def test_the_vocabulary_is_exactly_what_the_beat_sheet_uses():
    """🔴 어휘집이 아니라 짐이 되지 않게 — 쓰는 것만 있어야 한다.
    `warm`(불 쬐기)은 본문1 이 처음 쓰면서 들어왔다.
    `huddle`(움츠림)은 한기를 파란 색 면으로 그리던 것을 걷어내면서 들어왔다 —
    한기는 물체가 아니라 사건이고, 사건은 반응으로만 보인다.

    🔴 다섯(`run`·`rest`·`gather`·`attack`·`look_around`)은 **상상해서 넣은 게 아니다** —
    게임이 이미 선언한 것에서 왔다: `Assets/Scripts/M0/Data/ActionSO.cs` 의 `AnimKind`
    (Chop·Mine·Hammer·Water·Attack)와 액션 SO 14종(Rest·Gather·Explore·Flee·Wander…).
    이름을 게임과 같이 두는 것이 규칙이다 — 갈라지면 같은 동작을 두 이름으로 부르게 된다."""
    assert set(motions.MOTIONS) == {
        'look_up', 'walk', 'stop', 'farm', 'chop', 'draw', 'warm', 'eat',
        'huddle', 'reach', 'freeze',
        'run', 'rest', 'gather', 'attack', 'look_around',
        'mine', 'hammer', 'water'}


def test_every_game_anim_kind_has_a_motion():
    """🔴 게임이 `AnimKind` 로 다섯을 선언했다(ActionSO.cs). 영상이 그 다섯을 다른 이름으로
    부르기 시작하면 같은 몸짓을 두 어휘로 관리하게 된다. 이름은 게임이 정본이다."""
    for kind, name in motions.ANIM_KIND.items():
        assert name in motions.MOTIONS, (kind, name)
    assert set(motions.ANIM_KIND) == {'Chop', 'Mine', 'Hammer', 'Water', 'Attack'}


def test_eating_moves_one_hand_and_not_the_other():
    """🔴 받치는 손이 같이 오르내리면 밥이 아니라 박수로 읽힌다. 비대칭이 이 동작의 뜻이다."""
    lo, hi = motions.eat(0.0), motions.eat(1.5 * 0.58)
    assert abs(hi['forearm.R'][0] - lo['forearm.R'][0]) > 0.3, (lo, hi)
    assert abs(hi['forearm.L'][0] - lo['forearm.L'][0]) < 1e-9, (lo, hi)


def _accent(name):
    """최대각속 ÷ 평균각속. 1 에 가까우면 등속(기계), 클수록 강약이 있다."""
    import math
    f, cyc = motions.MOTIONS[name], motions.CYCLE.get(name, 1.0)
    N, prev, speeds = 240, None, []
    for i in range(N + 1):
        p = f(cyc * i / N)
        if prev is not None:
            speeds.append(max(max(abs(x - y) for x, y in zip(p.get(b, (0, 0, 0)),
                                                             prev.get(b, (0, 0, 0))))
                              for b in set(p) | set(prev)))
        prev = p
    return max(speeds) / (sum(speeds) / len(speeds))


def test_work_motions_have_accents_not_constant_speed():
    """🔴 사인으로 적으면 등속이라 아무리 크게 흔들어도 기계로 읽힌다.
    사용자가 「역동적이지 않다」고 한 판의 실측이 walk 1.18 · farm 1.43 · draw 1.32 였고,
    강약이 있던 chop·eat 은 2.55·3.26 이었다. 그 사이에 선을 긋는다."""
    # 🔑 `water` 는 뺀다 — 타격이 아니라 붓는 동작이라 강약이 세면 통을 내던지는 게 된다.
    for name in ('farm', 'draw', 'chop', 'eat', 'warm', 'gather', 'attack',
                 'mine', 'hammer'):
        assert _accent(name) >= 1.8, (name, _accent(name))


def test_work_motions_move_the_body_not_just_the_limbs():
    """🔴 골반이 공간에서 안 움직이면 막대에 꽂힌 인형이다. 무게가 여기서 생긴다."""
    for name in ('walk', 'farm', 'draw', 'chop', 'run', 'gather', 'attack', 'rest',
                 'mine', 'hammer', 'water'):
        cyc = motions.CYCLE.get(name, 1.0)
        span = max(abs(motions.MOTIONS[name](cyc * i / 60).get(motions.ROOT, (0, 0, 0))[j])
                   for i in range(61) for j in range(3))
        assert span > 0.005, (name, span)      # 5mm 미만은 화면에서 안 보인다


def test_run_speed_matches_the_stride_the_legs_actually_take():
    """🔴 걷기에서 배운 것을 뛰기에서 다시 배우지 않는다 — 속도는 다리가 만드는 보폭에서
    역산한 값이라야 발이 안 미끄러진다."""
    expect = 2 * motions.LEG * math.sin(motions.RUN_THIGH) * 2 * motions.RUN_HZ
    assert abs(motions.RUN_SPEED - expect) < 1e-9, (motions.RUN_SPEED, expect)
    assert motions.RUN_SPEED > motions.WALK_SPEED * 1.6, (motions.RUN_SPEED, motions.WALK_SPEED)


def test_sitting_actually_lowers_the_body():
    """🔴 골반을 안 내리면 앉은 게 아니라 공중에 뜬 채 다리만 뻗은 것이다.
    뼈대 골반이 0.300 이므로 20cm 넘게 내려야 바닥에 앉는다."""
    drop = -motions.rest(0.0)[motions.ROOT][1]
    assert drop > 0.18, drop
    assert motions.rest(0.0)['thigh.L'][0] < -1.2, ('다리를 앞으로 안 뻗었다',
                                                    motions.rest(0.0)['thigh.L'])


def test_impacts_squash_and_speed_stretches():
    """🔴 관절 회전만으로는 못 만드는 층이다 — 팔 길이(손 최대 z 0.785 · 머리 0.95)가
    진폭의 천장이라, 더 크게 보이려면 **몸 전체가** 눌리고 늘어나야 한다.
    규칙은 하나: 부딪히면 눌리고(+) 빠르면 늘어난다(−). 둘 다 있어야 층이 산 것이다."""
    for name in ('chop', 'mine', 'hammer', 'run', 'attack'):
        cyc = motions.CYCLE.get(name, 1.0)
        ks = [motions.MOTIONS[name](cyc * i / 90).get(motions.SQUASH, (0, 0, 0))[0]
              for i in range(91)]
        assert max(ks) > 0.05, ('부딪혀도 안 눌린다', name, max(ks))
        assert min(ks) < -0.02, ('빨라도 안 늘어난다', name, min(ks))
        assert max(abs(k) for k in ks) <= stage_squash_max(), ('젤리가 된다', name, ks)


def stage_squash_max():
    """`stage.SQUASH_MAX` 를 bpy 없이 읽는다 — 검사는 블렌더 없이 돌아야 한다."""
    import re
    src = open(os.path.join(HERE, 'stage.py'), encoding='utf-8').read()
    return float(re.search(r'^SQUASH_MAX = ([\d.]+)', src, re.M).group(1))


def test_quiet_motions_do_not_squash():
    """🔴 잔잔한 동작에 넣으면 사람이 상시 출렁여 젤리가 된다. 그리고 성격 표식이 주민에
    묶인 샷(훅·본문1)에서는 표식까지 같이 늘어난다."""
    for name in ('walk', 'stop', 'farm', 'draw', 'warm', 'eat', 'rest', 'look_around'):
        cyc = motions.CYCLE.get(name, 1.0)
        for i in range(31):
            assert motions.SQUASH not in motions.MOTIONS[name](cyc * i / 30), name


def test_warm_is_not_a_frozen_pose():
    """🔴 body1 은 다섯 비트 중 셋이 `warm` 이다. 이게 굳으면 4초가 통째로 죽는다."""
    import math
    peak = max(max(abs(x - y) for x, y in zip(motions.warm(t).get(b, (0, 0, 0)),
                                              motions.warm(t + 1 / 30).get(b, (0, 0, 0))))
               for t in [i / 30 for i in range(67)]
               for b in motions.warm(0.0) if b != motions.ROOT)
    assert math.degrees(peak * 30) > 60, ('불 쬐기가 정지에 가깝다', math.degrees(peak * 30))


def test_every_motion_only_names_real_bones():
    """rig.py 가 만든 18본에 없는 이름을 쓰면 stage.pose 가 KeyError 로 죽는다."""
    for name, fn in motions.MOTIONS.items():
        for t in (0.0, 0.37, 0.9, 2.3):
            for bone in fn(t):
                # `ROOT`·`SQUASH` 는 뼈가 아니라 몸 전체에 걸리는 층이다 — stage.pose 가 따로 읽는다
                assert bone in BONES or bone in (motions.ROOT, motions.SQUASH), (name, bone)


def test_every_motion_returns_three_numbers_per_bone():
    """stage.pose 가 rotation_euler 에 그대로 넣는다 — 셋이 아니면 거기서 죽는다."""
    for name, fn in motions.MOTIONS.items():
        for bone, rot in fn(0.5).items():
            assert len(rot) == 3, (name, bone, rot)
            for v in rot:
                assert isinstance(v, (int, float)) and not isinstance(v, bool), (name, bone, rot)


def test_every_motion_actually_moves_something():
    """0 회전만 돌려주는 동작은 동작이 아니다."""
    for name, fn in motions.MOTIONS.items():
        if name == 'stop':
            continue                      # 숨만 쉰다 — 아래에서 따로 본다
        moved = any(abs(v) > 0.01
                    for t in (0.1, 0.3, 0.5, 0.7, 0.9)
                    for rot in fn(t).values() for v in rot)
        assert moved, name


def test_stop_still_breathes():
    """완전히 굳으면 마네킹으로 읽힌다. 정적 게이트도 여기서 걸린다."""
    a = motions.stop(0.0)['spine'][0]
    b = motions.stop(0.96)['spine'][0]     # 0.26Hz 의 1/4 주기
    assert abs(a - b) > 0.005, (a, b)


def test_freeze_is_not_just_another_reach_pose():
    """🔴 뻗기(굳음의 준비)와 불 쬐기는 **다른 포즈**여야 한다. 같으면 아웃트로에서
    「저쪽을 보다가 굳었다」가 「계속 같은 자세」로 뭉개진다."""
    a, b = motions.warm(0.5), motions.reach(0.5)
    assert any(abs(x - y) > 0.15 for k in set(a) & set(b) for x, y in zip(a[k], b[k])), (a, b)


def test_freeze_does_not_move_at_all():
    """굳음은 **한 톨도 안 움직여야** 뜻이 선다. 숨 쉬면 「굳었다」가 아니다."""
    for t in (0.0, 0.4, 1.7, 5.0):
        assert motions.freeze(t) == motions.freeze(0.0), t


def test_freeze_is_the_pose_reach_has_already_settled_into():
    """🔴 `reach` 를 돌리다 `freeze` 로 갈아타는 샷은 굳음이 **오버슛 중간값**이면 거기서
    팔이 한 프레임 튄다 — 손이 제일 늦게 앉는 것을 놓치기 쉽다.
    (ep15s-1 아웃트로는 2026-08-13 판정으로 굳음을 걷어냈지만, 이 둘은 짝이라 함께 산다.)"""
    late = motions.reach(3.55)
    for bone, rot in motions.freeze(0.0).items():
        for x, y in zip(rot, late[bone]):
            assert abs(x - y) < 1e-9, (bone, x, y)


def test_cyclic_motions_return_to_the_start():
    """한 주기 뒤 같은 자리로 안 돌아오면 이어 붙일 때 튄다."""
    for name, period in motions.CYCLE.items():
        a, b = motions.MOTIONS[name](0.0), motions.MOTIONS[name](period)
        assert a.keys() == b.keys(), name
        for bone in a:
            for x, y in zip(a[bone], b[bone]):
                assert abs(x - y) < 1e-9, (name, bone, x, y)


def test_limbs_swing_forward_with_negative_x():
    """아래를 향한 뼈(팔·다리)는 **X 음수가 앞**이다."""
    r = motions.reach(0.9)
    assert r['upperarm.L'][0] < -0.3, r['upperarm.L']
    assert motions.farm(0.0)['thigh.L'][0] < 0, motions.farm(0.0)['thigh.L']


def test_upward_bones_use_the_opposite_sign():
    """🔴 위를 향한 뼈(척추·목·머리)는 부호가 뒤집힌다 — **X 양수가 위를 봄**이다.
    이걸 놓쳐서 look_up 이 고개를 숙이고 있었고, 검사가 아니라 사람 눈이 잡았다."""
    up = motions.look_up(0.5)
    assert up['head'][0] > 0.1, ('고개를 숙이고 있다', up['head'])
    assert up['neck'][0] > 0.1, ('고개를 숙이고 있다', up['neck'])
    assert up['spine'][0] > 0, up['spine']
    # 반대쪽도 박아 둔다 — 밭일은 허리를 **앞으로 굽힌다**(위 뼈라 음수)
    assert motions.farm(0.0)['spine'][0] < -0.3, motions.farm(0.0)['spine']


def test_walk_swings_arms_opposite_to_legs():
    """같은 쪽 팔다리가 같이 나가면 사람이 아니라 인형으로 읽힌다."""
    p = motions.walk(0.25 / motions.WALK_HZ)          # sin 이 최대인 자리
    assert p['thigh.L'][0] * p['upperarm.L'][0] < 0, p


def test_chop_strikes_faster_than_it_lifts():
    """내리치는 쪽이 빨라야 타격으로 읽힌다. 🔑 잣대를 `CHOP_LIFT` 에 매어 둔다 —
    비율을 고치면 검사가 같이 따라간다(값을 박아 두면 검사가 딴 구간을 재게 된다)."""
    lift_f, strike_f = motions.CHOP_LIFT, 1 - motions.CHOP_LIFT
    top = motions.chop(motions.CHOP_LIFT * 1.1)['upperarm.L'][0]
    lift = abs(top - motions.chop(0.0)['upperarm.L'][0])
    strike = abs(motions.chop(1.1 * 0.999)['upperarm.L'][0] - top)
    assert lift > 0.1 and strike > 0.1, (lift, strike)
    assert strike / strike_f > lift / lift_f, (lift, strike)


def test_chop_overshoots_past_the_block_and_rebounds():
    """🔑 관성 — 내리친 팔은 바닥에서 **지나쳤다 되튄다.** 안 튀면 도끼가 멈춘 게 아니라
    사라진 것으로 읽힌다. 되튀는 구간 안에 시작점(u=0)보다 더 내려간 프레임이 있어야 한다.

    🔴 부등호가 **반대로** 박혀 있었다(2026-08-13). 팔 매핑이 뒤집혀 있던 시절의 검사라
       「더 내려간다」를 X 가 더 작아지는 것으로 봤다. 실측은 반대다 —
       **upperarm X 가 클수록 낮다**(−150° → 손 z 0.785 · −10° → 0.33)."""
    rest = motions.chop(0.0)['upperarm.L'][0]
    hit = motions.CHOP_LIFT + (1 - motions.CHOP_LIFT) * (1 - motions.CHOP_REBOUND)
    assert motions.chop(hit * 1.1)['upperarm.L'][0] > rest + 0.01, (hit, rest)


def test_one_shot_motions_anticipate_before_they_go():
    """🔴 예비 동작 — 본동작 전에 **반대로** 물러난다. 없으면 여섯이 동시에 기계로 튄다.
    🔑 머리는 `LAG_TIP` 만큼 늦으므로 척추가 숙일 때는 아직 0 이다. 뼈마다 제 시각에 본다."""
    assert motions._snappy(0.5 * motions.ANTIC) < -0.01
    lead = motions.look_up(0.5 * motions.ANTIC * motions.LOOK_DUR)
    assert lead['spine'][0] < -0.004, ('먼저 숙이지 않는다', lead['spine'])
    late = motions.look_up((motions.LAG_TIP + 0.5 * motions.ANTIC) * motions.LOOK_DUR)
    assert late['head'][0] < -0.004, ('머리가 예비 동작을 안 한다', late['head'])
    back = motions.reach(0.5 * motions.ANTIC * motions.REACH_DUR)
    assert back['upperarm.L'][0] > 0.004, ('팔을 먼저 빼지 않는다', back['upperarm.L'])


def test_one_shot_motions_overshoot_then_settle():
    """관성 — 목표를 지나쳤다 앉는다. 끝에서는 **정확히** 목표여야 `freeze` 가 안 흔들린다."""
    peak = max(motions._snappy(u / 200) for u in range(201))
    assert peak > 1.0 + motions.OVER * 0.5, peak
    assert motions._snappy(1.0) == 1.0 and motions._snappy(0.0) == 0.0


def test_distal_joints_lag_behind_the_body():
    """겹침 — 손이 어깨와 **같이** 도착하면 팔이 막대기 하나로 읽힌다."""
    u = motions.ANTIC + motions.LAG_TIP + 0.15      # 셋 다 본동작에 들어선 자리
    assert motions._snappy(u - motions.LAG_TIP) < motions._snappy(u - motions.LAG_MID) \
        < motions._snappy(u)
    # 🔑 뼈마다 진폭이 다르므로 각도가 아니라 **진행률**로 견준다
    mid = motions.reach(u * motions.REACH_DUR)
    prog = [mid['upperarm.L'][0] / math.radians(-58), mid['forearm.L'][0] / math.radians(-30),
            mid['hand.L'][0] / math.radians(-14)]
    assert prog[0] > prog[1] > prog[2], prog


def test_joints_travel_on_an_arc_not_a_line():
    """궤적이 한 평면 안에만 있으면 기계다. 뻗기·걷기가 Z(좌우)로 부푸는지 본다."""
    flat = motions.reach(0.5 * motions.REACH_DUR)['upperarm.L'][2]
    ends = motions.reach(motions.REACH_DUR)['upperarm.L'][2]
    assert abs(flat - ends) > 0.03, (flat, ends)
    zs = [motions.walk(i / 60 / motions.WALK_HZ)['upperarm.L'][2] for i in range(60)]
    assert max(zs) - min(zs) > 0.03, (min(zs), max(zs))


def test_walk_speed_matches_the_stride_the_legs_actually_take():
    """🔴 제자리걸음·발 미끄러짐을 막는 검사 그 하나.
    보폭 손잡이(`WALK_THIGH`)를 고치고 속도를 안 고치면(또는 그 반대면) 여기서 걸린다."""
    expect = 2 * motions.WALK_STRIDE * motions.WALK_HZ
    assert abs(motions.WALK_SPEED - expect) < 1e-9, (motions.WALK_SPEED, expect)


# ── 발이 땅에 붙어 있는가 ────────────────────────────────────────────
# 🔴 아래 둘은 **검사가 직접 정기구학을 푼다.** `motions` 의 IK 를 되부르면 그건 검사가
#    아니라 메아리다 — 각을 받아 발이 어디 있는지 여기서 다시 계산해서 견준다.

def _ankle(spec, side):
    """뼈 각에서 되짚은 발목 (앞, 높이). 아마추어 원점 기준이고 앞은 월드 -X 쪽이다."""
    th = -spec['thigh.%s' % side][0]                  # 아래 뼈는 X 음수가 앞
    sh = th - spec['shin.%s' % side][0]               # 정강이는 X 양수가 무릎 굽힘
    root = spec.get(motions.ROOT, (0, 0, 0))
    return (root[0] + motions.THIGH_LEN * math.sin(th) + motions.SHIN_LEN * math.sin(sh),
            motions.HIP_Z + root[1]
            - motions.THIGH_LEN * math.cos(th) - motions.SHIN_LEN * math.cos(sh))


def _contact(spec, side):
    """디딘 자리 (앞, 높이). 발바닥 피치는 세 뼈가 돌린 각의 **합**이다."""
    af, az = _ankle(spec, side)
    psi = -sum(spec['%s.%s' % (b, side)][0] for b in ('thigh', 'shin', 'foot'))
    sf, sz = motions._sole(psi)
    return af - sf, az - sz


def test_the_planted_foot_stays_put():
    """🔴 **자연스러움 1순위.** 앞 판은 순수 FK 라 디딘 발이 고정되는 순간이 아예 없었고
    (실측 최대 23.8cm = 보폭의 76%), 사람 눈이 가장 먼저 잡는 결함이 그것이었다.
    나아가는 것은 몸이고 발은 **세계에 못 박혀** 있어야 한다.
    🔑 잣대는 2cm — 주민 키 0.95 의 2%, 6m 에서 1픽셀 남짓이다."""
    for name, hz, speed, duty in (('walk', motions.WALK_HZ, motions.WALK_SPEED, motions.WALK_DUTY),
                                  ('run', motions.RUN_HZ, motions.RUN_SPEED, motions.RUN_DUTY)):
        fn, N = motions.MOTIONS[name], 400
        for side in 'LR':
            seg, worst, sunk = [], 0.0, 0.0
            for i in range(N + 1):
                t = 2 * i / N / hz
                v = ((t * hz - 0.25) if side == 'L' else (t * hz + 0.25)) % 1.0
                if v >= duty:                          # 흔드는 중 — 붙어 있을 이유가 없다
                    if len(seg) > 2:
                        worst = max(worst, max(seg) - min(seg))
                    seg = []
                    continue
                cf, cz = _contact(fn(t), side)
                seg.append(speed * t + cf)             # 세계 좌표 = 나아간 거리 + 몸 기준 자리
                sunk = max(sunk, abs(cz))
            assert worst < 0.02, ('%s %s 디딘 발이 %.3f m 미끄러진다' % (name, side, worst))
            # 🔑 안 움직이는 것만으로는 모자란다 — 땅에 있어야 「디뎠다」다
            assert sunk < 0.003, ('%s %s 디딘 발이 땅에서 %.3f m 떠 있다/박혀 있다'
                                  % (name, side, sunk))


def test_the_legs_never_reach_past_what_they_have():
    """🔴 IK 는 못 미치면 **잘라 버린다** — 그리고 그 순간 발이 다시 미끄러진다.
    그래서 골반 높이가 계획에서 나와야 한다(`_gait_plan`). 여기가 그 계약이다.
    🔑 무릎 역꺾임도 같이 본다 — 정강이 X 는 **양수(굽힘)** 여야 한다. 순수 함수라 여기서 잡는다."""
    for name, hz in (('walk', motions.WALK_HZ), ('run', motions.RUN_HZ)):
        fn = motions.MOTIONS[name]
        for i in range(401):
            spec = fn(2 * i / 400 / hz)
            for side in 'LR':
                af, az = _ankle(spec, side)
                root = spec.get(motions.ROOT, (0, 0, 0))
                d = math.hypot(af - root[0], motions.HIP_Z + root[1] - az)
                assert d <= motions.LEG_MAX + 1e-6, (name, side, '다리가 모자라 잘렸다', d)
                knee = spec['shin.%s' % side][0]
                assert 0 <= knee < math.radians(150), (name, side, '무릎이 역꺾인다', knee)


def test_the_hips_ride_a_smooth_curve_not_a_clamp():
    """🔴 계획이 틀려도 IK 는 안 죽는다 — `_legs` 가 골반을 **눌러서** 맞춘다. 그래서
    「검사는 통과하는데 그림이 틀린」 꼴이 되기 쉽다: 그 누름이 골반 곡선에 꺾임을 남기고
    화면에서는 걸을 때마다 한 번씩 **툭 떨어지는** 것으로 보인다.
    🔑 그래서 재는 것은 높이가 아니라 **가속도**다(30fps 2차 차분). 실측: 계획대로면
       걷기 0.0035 · 뛰기 0.0134, 골반을 안 내리면 0.0251 이다.
    🔴 문턱을 더 못 조인다 — **중력 자체가 30fps 에서 0.011** 이고(g·dt²), 뛰기의 체공은
       실제로 포물선이라 그만큼은 정상이다. 그래서 1.5g 언저리인 0.016 에 긋는다."""
    for name in ('walk', 'run'):
        hs = [motions.MOTIONS[name](i / 30.0)[motions.ROOT][1] for i in range(60)]
        jerk = max(abs(hs[i + 2] - 2 * hs[i + 1] + hs[i]) for i in range(len(hs) - 2))
        assert jerk < 0.016, (name, '골반이 걸음마다 툭 떨어진다', jerk)


def test_blend_is_the_endpoints_at_zero_and_one():
    """이음새가 끝점에서 원본과 다르면 이은 자리가 오히려 튄다."""
    a, b = motions.walk(0.3), motions.stop(0.3)
    z, o = motions.blend(a, b, 0.0), motions.blend(a, b, 1.0)
    assert set(z) == set(o) == set(a) | set(b)
    for bone in a:
        assert z[bone] == tuple(a[bone]), bone
    for bone in b:
        assert o[bone] == tuple(b[bone]), bone
    only_a = set(a) - set(b)
    assert only_a, '검사가 무의미하다 — 한쪽에만 있는 뼈가 없다'
    for bone in only_a:                     # 안 적힌 뼈는 차렷 — stage.pose 와 같은 규약
        assert o[bone] == (0.0, 0.0, 0.0), bone


def test_walk_period_matches_the_shared_vocabulary():
    """engine/motions.mjs 의 walk.hz 와 같아야 한다 — 표가 갈라지면 대조가 성립을 안 한다."""
    assert abs(motions.WALK_HZ - 1.15) < 1e-9


if __name__ == '__main__':
    from _check import run
    run(globals())
