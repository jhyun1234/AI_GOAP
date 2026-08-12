"""군무가 정말 **위상 오차 0** 인가. 이 설계의 핵심 불변식이라 부동소수 수준에서 본다."""
import inspect
import os
import motions
import unison


def test_unison_gives_every_villager_the_identical_pose():
    """「같다」는 위상 오차 0 이다. 눈에 안 보일 만큼 어긋나도 「대충 비슷한 여섯」이 된다."""
    for t in (0.0, 0.21, 0.63, 1.4, 2.77):
        specs = [unison.pose_at(unison.TOGETHER, t) for _ in range(unison.N)]
        first = specs[0]
        for i, s in enumerate(specs[1:], 1):
            assert s.keys() == first.keys(), i
            for bone in first:
                assert s[bone] == first[bone], (t, i, bone)


def test_pose_at_cannot_take_a_villager_index():
    """🔴 번호를 받을 수 있으면 언젠가 누가 위상을 어긋낸다. 서명으로 막는다."""
    params = list(inspect.signature(unison.pose_at).parameters)
    assert params == ['motion_name', 't'], params


def test_break_apart_gives_villagers_different_motions():
    """「갈라진다」는 위상이 아니라 **동작 자체**가 다른 것이다."""
    names = [unison.motion_at(0.0, i, 1.0) for i in range(unison.N)]
    assert len(set(names)) >= 4, names


def test_it_is_still_unison_before_the_break():
    names = [unison.motion_at(0.0, i, 0.0) for i in range(unison.N)]
    assert len(set(names)) == 1, names


def test_break_motions_all_exist_in_the_vocabulary():
    for name in unison.BREAK + [unison.TOGETHER]:
        assert name in motions.MOTIONS, name


def test_six_stands_do_not_overlap():
    """서로 가리면 「여섯이 똑같다」를 셀 수가 없다. 주민 폭이 0.44 라 0.6 은 떨어져야 한다."""
    for i in range(unison.N):
        for j in range(i + 1, unison.N):
            ax, ay = unison.STANDS[i]
            bx, by = unison.STANDS[j]
            d = ((ax - bx) ** 2 + (ay - by) ** 2) ** 0.5
            assert d > 0.6, (i, j, round(d, 2))


HOOK_BEAT = 6.606 / len(unison.HOOK_BEATS)


def test_march_actually_goes_somewhere():
    """🔴 5비트 판이 반려된 이유가 여기다 — 여섯이 다리만 젓고 발밑 좌표가 그대로였다.
    주민 간격이 1.15 라, 적어도 한 사람 몫은 나아가야 「걸어갔다」로 읽힌다."""
    x, y, heading, phase = unison.march(6.606, HOOK_BEAT)
    assert (x * x + y * y) ** 0.5 > 1.2, (x, y)
    assert abs(heading - unison.HOOK_TURN) < 1e-9, heading
    assert phase > 3.0, phase


def test_march_only_advances_while_walking():
    """멈춘 비트에서 자리가 나아가면 선 채로 미끄러지고, 위상이 흐르면 다시 걸을 때 튄다."""
    for i, kind in enumerate(unison.HOOK_BEATS):
        ax, ay, _ah, ap = unison.march(i * HOOK_BEAT + 0.02, HOOK_BEAT)
        bx, by, _bh, bp = unison.march((i + 1) * HOOK_BEAT - 0.02, HOOK_BEAT)
        moved = ((ax - bx) ** 2 + (ay - by) ** 2) ** 0.5
        if kind == 'walk':
            assert moved > 0.1 and bp > ap, (i, kind, moved)
        else:
            assert moved < 1e-12 and abs(bp - ap) < 1e-12, (i, kind, moved)


def _walk_step(span):
    """끊기지 않은 걷기가 `span` 초 동안 만드는 **가장 큰** 관절 변화.
    잣대를 보폭에 매어 둔다 — 진폭을 고쳐도 검사가 같이 따라간다."""
    out = 0.0
    for i in range(120):
        a = motions.walk(i / 120 / motions.WALK_HZ)
        b = motions.walk(i / 120 / motions.WALK_HZ + span)
        out = max(out, max(abs(p - q) for bone in a for p, q in zip(a[bone], b[bone])))
    return out


def test_hook_pose_never_teleports_a_limb():
    """비트 경계에서 다리가 차렷으로 튀면 「동시에 멈췄다」가 아니라 렉으로 읽힌다.
    반 주기에서 끊긴 허벅지는 진폭만큼(0.59 rad) 튀는데, 그건 **그냥 걷는 것의 네 배**다."""
    span = 1 / 30
    limit = 2 * _walk_step(span)
    for i in range(1, len(unison.HOOK_BEATS)):
        a = unison.hook_pose(i * HOOK_BEAT - span / 2, HOOK_BEAT)
        b = unison.hook_pose(i * HOOK_BEAT + span / 2, HOOK_BEAT)
        for bone in set(a) | set(b):
            pa, pb = a.get(bone, (0, 0, 0)), b.get(bone, (0, 0, 0))
            for u, v in zip(pa, pb):
                assert abs(u - v) < limit, (i, bone, u, v, limit)


def test_hook_pose_cannot_take_a_villager_index():
    """🔴 `pose_at` 과 같은 이유 — 번호를 받을 수 있으면 언젠가 누가 위상을 어긋낸다."""
    params = list(inspect.signature(unison.hook_pose).parameters)
    assert params == ['t', 'beat'], params


def test_hook_camera_is_defined_once():
    """🔑 인트로가 착지하는 자리와 훅이 출발하는 자리는 **같은 상수**여야 한다.
    두 파일에 값을 따로 적으면 언젠가 한쪽만 고쳐지고, 그날 컷이 튄다."""
    loc, at = unison.HOOK_CAM
    assert len(loc) == 3 and len(at) == 3, unison.HOOK_CAM
    src = open(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                            'render_intro.py'), encoding='utf-8').read()
    assert 'unison.HOOK_CAM' in src, '인트로가 훅 카메라 상수를 안 쓰고 있다'
    src = open(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                            'render_unison_proof.py'), encoding='utf-8').read()
    assert 'unison.HOOK_CAM' in src, '훅이 자기 카메라 상수를 안 쓰고 있다'


def test_personality_marks_differ_by_shape_not_colour():
    """🔴 설계 §2 — 주민을 색으로 안 나눈다. 여섯이 **한 색 안에서 형태로만** 다르다."""
    assert len(unison.PERSONALITY) >= unison.N, len(unison.PERSONALITY)
    seen = set()
    for pat in unison.PERSONALITY[:unison.N]:
        assert len(pat) == 3, pat
        assert pat not in seen, ('같은 표식이 둘', pat)
        seen.add(pat)
    src = open(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                            'unison.py'), encoding='utf-8').read()
    assert src.count("meaning_mat('") == 1, '표식이 색을 둘 이상 쓴다'
    assert "meaning_mat('violet'" in src, '성격 표식은 보라여야 한다'


if __name__ == '__main__':
    from _check import run
    run(globals())
