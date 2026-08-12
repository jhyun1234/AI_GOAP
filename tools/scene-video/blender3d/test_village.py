"""마을이 무대로 쓸 만한가 — 눈으로 보기 전에 값으로 먼저 거른다."""
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import palette
import probe

BLENDER = r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
REPORT = os.path.join(HERE, 'fixtures', 'village_report.json')
PROBE = os.path.join(HERE, 'fixtures', 'village_probe.png')


def _report():
    if not os.path.exists(REPORT):
        subprocess.run([BLENDER, '--background', '--factory-startup', '--python',
                        os.path.join(HERE, 'village.py')], check=True, capture_output=True)
    return json.load(open(REPORT, encoding='utf-8'))


def test_village_has_the_pieces():
    r = _report()
    assert len(r['houses']) == 3, r['houses']
    assert len(r['trees']) >= 3, r['trees']


def test_scale_makes_sense_next_to_a_1m_villager():
    """집이 주민보다 훨씬 크거나 작으면 축척이 깨져 마을로 안 읽힌다."""
    r = _report()
    assert 1.4 <= r['house_height'] <= 2.6, r['house_height']


def test_work_spots_are_far_enough_apart():
    """여섯의 궤적이 **겹치는 것**이 이 회차의 그림이다. 자리가 붙어 있으면 안 보인다."""
    r = _report()
    xs = r['spots']
    assert len(xs) == 4, xs
    for i in range(len(xs)):
        for j in range(i + 1, len(xs)):
            d = ((xs[i][0] - xs[j][0]) ** 2 + (xs[i][1] - xs[j][1]) ** 2) ** 0.5
            assert d > 1.8, (i, j, round(d, 2))


def test_village_uses_no_meaning_or_instrument_color():
    """🔴 마을은 세계층이다. 여기서 유채색을 쓰면 뜻층 예산(한 프레임에 둘)을 미리 태운다."""
    r = _report()
    assert r['meaning_materials'] == [], r['meaning_materials']
    assert r['instrument_materials'] == [], r['instrument_materials']


def _probe_shot():
    if not os.path.exists(PROBE):
        subprocess.run([BLENDER, '--background', '--factory-startup', '--python',
                        os.path.join(HERE, 'render_village_probe.py')],
                       check=True, capture_output=True)
    return probe.metrics(PROBE)


def test_rendered_village_shows_no_meaning_color():
    """🔴 재질 이름만 보면 부족하다. 감마가 어두운 쪽에서 채도를 키워 흙색 지붕이
    앰버로 잡힌 적이 있다 — 판정은 **찍은 픽셀**로 한다."""
    m = _probe_shot()
    assert m['chroma_px'] == {}, m['chroma_px']
    assert m['instrument_px'] == 0, m['instrument_px']


def test_rendered_village_stays_darker_than_the_subject():
    """세계는 물러난다. 주민은 sRGB 0.85(217) 근처까지 가고 마을은 그 아래여야 한다 —
    지붕이 주인공보다 밝았던 판이 실제로 있었다."""
    m = _probe_shot()
    assert m['peak_lum'] < 180, m['peak_lum']


def test_spec_budget_constant_is_two():
    assert palette.MAX_MEANING_PER_FRAME == 2


if __name__ == '__main__':
    from _check import run
    run(globals())
