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


if __name__ == '__main__':
    from _check import run
    run(globals())
