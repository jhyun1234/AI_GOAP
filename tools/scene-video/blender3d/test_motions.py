"""동작 여덟이 실제로 뼈를 움직이는가. `bpy` 없이 돈다 — 그래서 매번 돌릴 수 있다."""
import math
import motions

BONES = {'hips', 'spine', 'neck', 'head'} | {
    '%s.%s' % (p, s) for s in ('L', 'R')
    for p in ('shoulder', 'upperarm', 'forearm', 'hand', 'thigh', 'shin', 'foot')}


def test_all_eight_exist():
    assert set(motions.MOTIONS) == {
        'look_up', 'walk', 'stop', 'farm', 'chop', 'draw', 'reach', 'freeze'}


def test_every_motion_only_names_real_bones():
    """rig.py 가 만든 18본에 없는 이름을 쓰면 stage.pose 가 KeyError 로 죽는다."""
    for name, fn in motions.MOTIONS.items():
        for t in (0.0, 0.37, 0.9, 2.3):
            for bone in fn(t):
                assert bone in BONES, (name, bone)


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


def test_freeze_does_not_move_at_all():
    """굳음은 **한 톨도 안 움직여야** 뜻이 선다. 숨 쉬면 「굳었다」가 아니다."""
    for t in (0.0, 0.4, 1.7, 5.0):
        assert motions.freeze(t) == motions.freeze(0.0), t


def test_cyclic_motions_return_to_the_start():
    """한 주기 뒤 같은 자리로 안 돌아오면 이어 붙일 때 튄다."""
    for name, period in motions.CYCLE.items():
        a, b = motions.MOTIONS[name](0.0), motions.MOTIONS[name](period)
        assert a.keys() == b.keys(), name
        for bone in a:
            for x, y in zip(a[bone], b[bone]):
                assert abs(x - y) < 1e-9, (name, bone, x, y)


def test_limbs_swing_forward_with_negative_x():
    """축 규약(rig.py): 팔다리는 뼈가 아래를 향하므로 **X 음수가 앞**이다.
    뻗기는 팔을 앞으로 보내고, 밭일은 허리를 앞으로 굽힌다."""
    r = motions.reach(0.9)
    assert r['upperarm.L'][0] < -0.3, r['upperarm.L']
    assert motions.farm(0.0)['spine'][0] < -0.3, motions.farm(0.0)['spine']


def test_walk_swings_arms_opposite_to_legs():
    """같은 쪽 팔다리가 같이 나가면 사람이 아니라 인형으로 읽힌다."""
    p = motions.walk(0.25 / motions.WALK_HZ)          # sin 이 최대인 자리
    assert p['thigh.L'][0] * p['upperarm.L'][0] < 0, p


def test_chop_strikes_faster_than_it_lifts():
    """들 때 0.62 · 칠 때 0.38 — 내리치는 쪽이 빨라야 타격으로 읽힌다."""
    lift = abs(motions.chop(0.62 * 1.1)['upperarm.L'][0] - motions.chop(0.0)['upperarm.L'][0])
    strike = abs(motions.chop(1.1 * 0.999)['upperarm.L'][0] - motions.chop(0.62 * 1.1)['upperarm.L'][0])
    assert lift > 0.1 and strike > 0.1, (lift, strike)
    assert strike / 0.38 > lift / 0.62, (lift, strike)


def test_walk_period_matches_the_shared_vocabulary():
    """engine/motions.mjs 의 walk.hz 와 같아야 한다 — 표가 갈라지면 대조가 성립을 안 한다."""
    assert abs(motions.WALK_HZ - 1.15) < 1e-9


if __name__ == '__main__':
    from _check import run
    run(globals())
