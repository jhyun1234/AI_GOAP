"""계기층 격자가 말이 되는가. `bpy` 없이 도는 부분만 본다."""
import json
import os

import instrument

HERE = os.path.dirname(os.path.abspath(__file__))
SPEC = next(s for s in json.load(open(os.path.join(
    HERE, '..', 'episodes', 'ep15s-1', 'scene.json'), encoding='utf-8'))['shots']
    if s['id'] == 'S2')['spec']


def test_grid_has_exactly_the_cells_the_script_promises():
    """대본이 「서른 가지」라고 말한다. 화면에 스물아홉이 뜨면 그 줄이 거짓말이 된다."""
    assert SPEC['cols'] * SPEC['rows'] == 30, SPEC
    assert len(instrument.cells(SPEC['cols'], SPEC['rows'])) == 30


def test_lit_and_sole_are_inside_the_grid():
    n = SPEC['cols'] * SPEC['rows']
    for i in SPEC['litIdx']:
        assert 0 <= i < n, i
    assert SPEC['soleIdx'] in SPEC['litIdx'], '남는 칸이 켜진 적 없는 칸이면 ⑤가 안 읽힌다'


def test_cells_do_not_overlap():
    """테두리가 붙으면 서른 칸이 한 덩어리로 읽힌다 — 「가득한데 닿은 게 셋」이 안 산다."""
    assert instrument.PITCH > instrument.CELL + 0.05, (instrument.PITCH, instrument.CELL)


def test_grid_floats_above_the_village():
    """🔴 계기층은 공중 전용이다. 집 꼭대기(지붕 1.85)보다 위가 아니면 세계층과 같은
    층에 있는 것처럼 보이고, 그 순간 「위에서 내려다본 표」가 아니게 된다."""
    assert instrument.Z > 1.85 + 0.5, instrument.Z


def test_work_block_keeps_six_apart_and_together():
    """여섯이 겹치면 셀 수가 없고, 흩어지면 「똑같다」가 안 읽힌다."""
    pts = instrument.work_block((0.0, 0.0))
    assert len(pts) == 6
    for i in range(6):
        for j in range(i + 1, 6):
            d = ((pts[i][0] - pts[j][0]) ** 2 + (pts[i][1] - pts[j][1]) ** 2) ** 0.5
            assert d > 0.6, (i, j, round(d, 2))       # 주민 폭 0.44
            assert d < 2.6, (i, j, round(d, 2))       # 한 덩어리로 보여야 한다


def test_instrument_colour_is_not_reused_by_the_world():
    """🔴 시안이 세계 물체에 붙으면 초록·한기와 구별이 안 된다(설계 §2)."""
    src = open(os.path.join(HERE, 'render_body2.py'), encoding='utf-8').read()
    assert 'instrument_mat' in src
    assert "world_mat('instrument'" not in src
    assert "meaning_mat('instrument'" not in src


if __name__ == '__main__':
    from _check import run
    run(globals())
