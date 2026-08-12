import os
import probe

HERE = os.path.dirname(os.path.abspath(__file__))
FIXTURE = os.path.join(HERE, 'fixtures', 'swatch.png')


def test_metrics_reads_a_known_swatch():
    """가로로 4등분한 견본: 투명 · 흰색 · 강조 초록 · 계기 시안."""
    m = probe.metrics(FIXTURE)
    assert abs(m['alpha_cover'] - 0.75) < 0.02, m['alpha_cover']
    assert m['peak_lum'] > 240, m['peak_lum']
    assert m['chroma_hues'] == {'green'}, m['chroma_hues']
    assert m['instrument_px'] > 0, m['instrument_px']


def test_white_is_not_counted_as_a_meaning_color():
    """흰색은 채도가 0 이라 세계층이다. 여기서 새면 팔레트 게이트가 통째로 못 쓰게 된다."""
    m = probe.metrics(FIXTURE)
    assert m['chroma_px'].get('green', 0) < m['alpha_cover'] * 400 * 40 * 0.5


def test_frame_diff_zero_for_same_file():
    assert probe.frame_diff(FIXTURE, FIXTURE) == 0.0


def test_step_sampling_agrees_with_full_scan():
    full = probe.metrics(FIXTURE, step=1)
    fast = probe.metrics(FIXTURE, step=7)
    assert full['chroma_hues'] == fast['chroma_hues']
    assert abs(full['alpha_cover'] - fast['alpha_cover']) < 0.03


if __name__ == '__main__':
    from _check import run
    run(globals())
