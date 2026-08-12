"""렌더 PNG 를 재는 도구. **눈대중을 안 남긴다.**

🔴 왜 있는가. 3D 전환 첫날 「바닥이 사람보다 밝다」를 눈으로 판단하다 세 판을 헛돌았다.
   알베도를 0.17 → 0.035 로 두 번 낮추고도 안 어두워졌는데, 픽셀을 재고 나서야 원인이
   알베도가 아니라 **노출 클리핑**이었음이 나왔다. 이 도구가 그 재발 방지다.

🔴 Pillow 를 안 쓴다. 이 저장소는 의존성 0 이 원칙이고(render.mjs 주석), ffmpeg 는 이미
   파이프라인이 쓰고 있다. raw RGBA 를 뽑아 바이트로 읽는다.
"""
import os
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import palette

# 🔴 색 값을 여기 다시 적지 않는다 — 표는 palette.py 하나뿐이다.
MEANING_HUES = {k: palette.HUES[k] for k in palette.MEANING}
INSTRUMENT_HUE = palette.HUES['instrument']
HUE_TOL = palette.HUE_TOL
# 🔴 채도가 낮으면 세계층이다. 정확한 값 비교로 짜면 안티에일리어싱 경계의 중간값이
#    전부 위반으로 잡힌다 — check.mjs 가 같은 이유로 색상각 판정을 쓴다.
SAT_MIN = palette.SAT_MIN
LUM_MIN = palette.LUM_MIN
ALPHA_MIN = 120
BRIGHT_MIN = 90        # 지면보다 밝은 것 = 세워진 것


def _raw(path):
    dim = subprocess.run(
        ['ffprobe', '-v', 'error', '-select_streams', 'v:0',
         '-show_entries', 'stream=width,height', '-of', 'csv=p=0:s=x', path],
        capture_output=True, check=True, text=True).stdout.strip()
    w, h = (int(v) for v in dim.split('x')[:2])
    rgba = subprocess.run(
        ['ffmpeg', '-v', 'error', '-i', path, '-f', 'rawvideo', '-pix_fmt', 'rgba', '-'],
        capture_output=True, check=True).stdout
    return w, h, rgba


def read_png(path):
    w, h, rgba = _raw(path)
    return {'w': w, 'h': h, 'rgba': rgba}


def _hue_sat(r, g, b):
    mx, mn = max(r, g, b), min(r, g, b)
    if mx == 0:
        return 0.0, 0.0
    sat = (mx - mn) / mx
    if mx == mn:
        return 0.0, sat
    if mx == r:
        hue = 60 * (((g - b) / (mx - mn)) % 6)
    elif mx == g:
        hue = 60 * ((b - r) / (mx - mn) + 2)
    else:
        hue = 60 * ((r - g) / (mx - mn) + 4)
    return hue, sat


def _near(hue, target):
    return abs(((hue - target + 180) % 360) - 180) <= HUE_TOL


def metrics(path, step=1):
    """step 을 올리면 빨라진다. 작은 유채색 조각을 놓칠 수 있으니 게이트는 step=1 로 써라."""
    d = read_png(path)
    rgba, n = d['rgba'], d['w'] * d['h']
    opaque = peak = instrument = bright = 0
    hues = {}
    for i in range(0, n * 4, 4 * step):
        if rgba[i + 3] < ALPHA_MIN:
            continue
        opaque += 1
        r, g, b = rgba[i], rgba[i + 1], rgba[i + 2]
        lum = (r * 299 + g * 587 + b * 114) // 1000
        if lum > peak:
            peak = lum
        # 🔴 지면은 처음부터 화면을 덮는다. 「비었다 → 찼다」를 alpha_cover 로 재면
        #    둘 다 0.73 이라 아무것도 못 본다. **세워진 것**은 지면보다 밝다.
        if lum >= BRIGHT_MIN:
            bright += 1
        if lum < LUM_MIN:          # 어두운 화소의 채도는 색이 아니라 반올림 잡음이다
            continue
        hue, sat = _hue_sat(r, g, b)
        if sat < SAT_MIN:
            continue
        if _near(hue, INSTRUMENT_HUE):
            instrument += 1
            continue
        for name, h0 in MEANING_HUES.items():
            if _near(hue, h0):
                hues[name] = hues.get(name, 0) + 1
                break
    seen = max(1, (n + step - 1) // step)
    return {'alpha_cover': opaque / seen, 'peak_lum': peak, 'bright_px': bright,
            'chroma_hues': set(hues), 'chroma_px': hues, 'instrument_px': instrument}


def frame_diff(a, b, step=1):
    """check.mjs 와 같은 잣대 — 적색 채널 평균 절대차 ÷ 255. 0.0008 미만이면 정적으로 센다."""
    da, db = read_png(a), read_png(b)
    ra, rb = da['rgba'], db['rgba']
    if len(ra) != len(rb):
        raise ValueError('크기가 다르다')
    n = len(ra) // 4
    idx = range(0, n, step)
    return sum(abs(ra[i * 4] - rb[i * 4]) for i in idx) / max(1, len(idx)) / 255
