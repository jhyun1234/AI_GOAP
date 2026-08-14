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


def test_a_handful_of_edge_pixels_is_not_a_colour():
    """🔴 안티에일리어싱 경계는 위반이 아니다. 계기층 시안이 어두운 바닥과 섞여 한기
    대역으로 밀린 372 px 을 「위험색이 떴다」로 세면 게이트를 아무도 안 믿게 된다."""
    seen = 1080 * 846
    got = probe.significant({'violet': 3935, 'chill': 372, 'amber': 17}, seen)
    assert got == {'violet'}, got
    # 진짜 작은 강조는 살아야 한다 — 30×30 px 이면 색이 뜬 것이 맞다
    assert probe.significant({'green': 900}, seen) == set()
    assert probe.significant({'green': 1200}, seen) == {'green'}


if __name__ == '__main__':
    from _check import run
    run(globals())
