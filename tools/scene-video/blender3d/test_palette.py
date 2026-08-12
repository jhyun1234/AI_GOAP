"""팔레트 3층이 실제로 그 색으로 렌더되는지 — 값이 아니라 **찍어서** 확인한다.

🔴 선형 RGB 상수를 표에 적어 두는 것과 화면에 그 색이 나오는 것은 다른 일이다.
   view_transform 이 Standard 라 선형값이 그대로 sRGB 로 나가는데, 그 전제가 깨지면
   (필믹으로 바뀌거나 발광 강도가 과하면) 색상각이 밀려 probe 가 못 알아본다."""
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import palette
import probe

BLENDER = r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
SHOT = os.path.join(HERE, 'fixtures', 'palette_probe.png')


def _render_once():
    if os.path.exists(SHOT):
        return
    subprocess.run([BLENDER, '--background', '--factory-startup', '--python',
                    os.path.join(HERE, 'render_palette_probe.py')],
                   check=True, capture_output=True)


def test_all_five_meaning_colors_land_on_their_hue():
    _render_once()
    m = probe.metrics(SHOT)
    assert m['chroma_hues'] == set(palette.MEANING), m['chroma_hues']


def test_instrument_is_separable_from_green_and_chill():
    """계기층 시안이 초록·한기로 새면 3층 규약이 통째로 못 쓰게 된다."""
    _render_once()
    m = probe.metrics(SHOT)
    assert m['instrument_px'] > 200, m['instrument_px']


def test_nothing_clips():
    """함정 ④ — Standard 는 롤오프가 없어 발광이 과하면 곧장 흰색으로 뭉갠다."""
    _render_once()
    m = probe.metrics(SHOT)
    assert m['peak_lum'] < 250, m['peak_lum']


def test_meaning_names_match_the_spec():
    assert palette.MEANING == ('green', 'amber', 'chill', 'red', 'violet')
    assert set(probe.MEANING_HUES) == set(palette.MEANING), '색 이름이 갈라졌다'


def test_earth_is_world_layer_not_a_meaning_color():
    """🔴 흙갈색은 앰버와 색상각이 6° 밖에 안 떨어져 있다. **채도로** 갈려야 한다 —
    안 그러면 마을 지붕이 전부 뜻층으로 잡혀 모든 프레임이 규약 위반이 된다."""
    assert palette.SATS['earth'] < palette.SAT_MIN, palette.SATS['earth']
    for name in palette.MEANING:
        assert palette.SATS[name] >= palette.SAT_MIN, (name, palette.SATS[name])


def test_meaning_hues_are_far_enough_apart_to_tell_apart():
    names = list(palette.MEANING) + ['instrument']
    for i, a in enumerate(names):
        for b in names[i + 1:]:
            d = abs(((palette.HUES[a] - palette.HUES[b] + 180) % 360) - 180)
            assert d > palette.HUE_TOL * 2, (a, b, d)


def test_the_baked_table_matches_the_python_one():
    """🔴 `check.mjs`(JS)가 읽는 `palette.json` 은 이 파일에서 구운 것이다.
    구워 두고 안 다시 구우면 표가 둘이 된다 — palette.py 맨 위가 금지한 바로 그것이다."""
    import json
    import palette
    baked = json.load(open(palette.JSON_PATH, encoding='utf-8'))
    assert baked == json.loads(json.dumps(palette.as_json())), (
        'palette.json 이 낡았다 — python blender3d/palette.py 로 다시 구워라')


if __name__ == '__main__':
    from _check import run
    run(globals())
