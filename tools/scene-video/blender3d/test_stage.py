"""무대 상수가 엔진과 맞는가. 🔴 여기가 어긋나면 게이트가 통째로 헛돈다."""
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
CSS = os.path.join(HERE, '..', 'engine', 'style.css')
# 🔴 `stage.py` 는 bpy 를 물고 있어 블렌더 밖에서 import 가 안 된다. 상수만 읽는다 —
#    이 검사를 블렌더 없이 돌리려는 것이 요점이다(그래야 매번 돌아간다).
_SRC = open(os.path.join(HERE, 'stage.py'), encoding='utf-8').read()
BAND_W, BAND_H, BAND_Y = (int(v) for v in re.search(
    r'BAND_W, BAND_H, BAND_Y = (\d+), (\d+), (\d+)', _SRC).groups())


def test_render_size_is_exactly_the_engine_vis_box():
    """🔴 블렌더가 굽는 크기는 엔진 `.vis` 상자와 **정확히 같아야 한다.**

    한 번 틀렸다: 계획 문서가 「쇼츠 폭 1080 그대로」로 적어 뒀는데 `.vis` 는 좌우 20px
    여백이 있어 970 이다. 1080 으로 구우면 좌우가 잘리거나 위아래에 빈 띠가 생기고,
    그 상태로도 게이트는 **전부 통과한다** — 아무도 못 보는 사고다.
    그래서 CSS 에서 값을 읽어 계산으로 대조한다. 둘 중 하나만 고치면 여기서 걸린다."""
    css = open(CSS, encoding='utf-8').read()
    stage_w = float(re.search(r'\.stage\{width:min\(100%,(\d+)px\)', css).group(1))
    vis = re.search(r'\.vis\{position:absolute;top:([\d.]+)%;left:(\d+)px;'
                    r'right:(\d+)px;bottom:([\d.]+)%\}', css)
    top, left, right, bottom = (float(v) for v in vis.groups())
    dpr = 1080 / stage_w                     # render.mjs: 무대 CSS 폭 × 2.7551 = 1080
    stage_h = stage_w * 16 / 9
    assert round((stage_w - left - right) * dpr) == BAND_W, (
        '가로가 .vis 와 다르다', round((stage_w - left - right) * dpr), BAND_W)
    assert round(stage_h * (100 - top - bottom) / 100 * dpr) == BAND_H, (
        '세로가 .vis 와 다르다', round(stage_h * (100 - top - bottom) / 100 * dpr), BAND_H)
    assert round(stage_h * top / 100 * dpr) == BAND_Y, 'y 자리가 다르다'


if __name__ == '__main__':
    from _check import run
    run(globals())
