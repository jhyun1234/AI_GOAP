"""인트로가 45 편 전체의 첫 3.5 초다. 여기가 틀리면 45 번 틀린다."""
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import palette
import probe

BLENDER = r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
FRAMES = os.path.join(
    os.environ.get('SCENE_3D_ROOT', r'D:\AI_GOAP-videos\3d'), '_intro', 'frames')
LAST = 154                       # 5.143초 × 30fps = 154.29 → 0..154


def _rendered():
    if not os.path.exists(os.path.join(FRAMES, '%04d.png' % LAST)):
        subprocess.run([BLENDER, '--background', '--factory-startup', '--python',
                        os.path.join(HERE, 'render_intro.py')], check=True, capture_output=True)
    return FRAMES


def _f(n):
    return os.path.join(_rendered(), '%04d.png' % n)


def test_frame_count_matches_the_engine_timeline():
    """인트로 길이가 어긋나면 뒤 회차 전체가 밀린다. 5.143초 · 30fps."""
    _rendered()
    n = len([f for f in os.listdir(FRAMES) if f.endswith('.png')])
    assert n == LAST + 1, n


def test_every_episode_s0_is_exactly_the_shared_intro():
    """🔴 **인트로는 한 벌뿐이다** — 그림 5.143초에 소리가 다른 편이 하나라도 있으면
    그 편은 인트로→훅 이음새가 어긋난 채 나간다(컷이 없는 자리라 바로 보인다).

    🔑 회차가 인트로에 맞춘다. 맞추는 손잡이는 S0 의 `pauseAfter` 하나다 —
       실측: 말 2993ms 편은 쉼 1800 · 말 4049ms 편은 쉼 744. 대사가 다른데도 S0 는 같다.
    🔴 새 편을 만들면 TTS 를 돌린 **직후** 이 검사가 먼저 걸린다. 그때 고칠 것은
       인트로가 아니라 그 편의 `pauseAfter` 다.
    🔑 재는 대상은 **3D 로 옮긴 편만**이다 — 2D 판 회차(d01s-1 은 4.599초)는 제 인트로를
       그리므로 이 규약 밖이다. 판정 기준은 그 편의 `kinds/intro.js` 가 `scene3d` 를
       재수출하는가 하나다(그게 「이 편은 구운 3D 인트로를 쓴다」의 정의다)."""
    import json
    eps = os.path.join(os.path.dirname(HERE), 'episodes')
    seen = []
    for ep in sorted(os.listdir(eps)):
        timed = os.path.join(eps, ep, 'build', 'timed.json')
        kind = os.path.join(eps, ep, 'kinds', 'intro.js')
        if not os.path.exists(timed) or not os.path.exists(kind):
            continue
        if 'scene3d' not in open(kind, encoding='utf-8').read():
            continue                      # 아직 2D 판 인트로를 그리는 편
        d = json.load(open(timed, encoding='utf-8'))
        s0 = next((x for x in d['shots'] if x['id'] == 'S0'), None)
        if s0 is None:
            continue
        sec = (sum(l['dur'] + l.get('pause', 0) for l in s0['lines']) + 350) / 1000.0
        seen.append((ep, sec))
    assert seen, '길이를 잰 회차가 하나도 없다 — tts 를 안 돌렸다'
    for ep, sec in seen:
        assert abs(sec - stage_intro_sec()) < 0.002, (
            '%s 의 S0 가 %.3f 초다 — 인트로는 %.3f 초다. 그 편의 pauseAfter 를 고쳐라'
            % (ep, sec, stage_intro_sec()))


def stage_intro_sec():
    """`stage.INTRO_SEC` 을 bpy 없이 읽는다 — 검사는 블렌더 없이 돌아야 한다."""
    import re
    src = open(os.path.join(HERE, 'stage.py'), encoding='utf-8').read()
    return float(re.search(r'^INTRO_SEC = ([\d.]+)', src, re.M).group(1))


def test_it_starts_empty_and_ends_with_a_village():
    """빈 땅에서 시작해 마을이 선다 — 그게 이 인트로가 하는 말 전부다.

    🔴 alpha_cover 로 재면 안 된다. 지면이 처음부터 화면을 덮어서 첫 프레임 0.73 ·
       끝 프레임 0.72 로 **거꾸로** 나온다. 세워진 것은 지면보다 밝으므로 밝은 화소로 잰다."""
    first = probe.metrics(_f(0), step=3)
    last = probe.metrics(_f(LAST), step=3)
    assert first['bright_px'] < last['bright_px'] * 0.25, (first['bright_px'], last['bright_px'])


def test_never_more_than_the_meaning_budget():
    """설계 §2 — 한 프레임에 뜻층 유채색은 둘까지. 인트로는 하나(초록)로 지킨다."""
    for n in range(0, LAST + 1, 6):
        m = probe.metrics(_f(n), step=3)
        assert len(m['chroma_hues']) <= 1, (n, m['chroma_hues'])
        assert m['chroma_hues'] <= {'green'}, (n, m['chroma_hues'])


def test_the_code_sweep_is_green_and_leaves_early():
    """초록 획은 0.64 초에 사라진다 — 그 뒤로 유채색이 남아 있으면 예산을 계속 태운다."""
    early = probe.metrics(_f(8), step=3)          # 0.27초
    late = probe.metrics(_f(130), step=3)         # 4.33초
    assert 'green' in early['chroma_hues'], early['chroma_hues']
    assert late['chroma_hues'] == set(), late['chroma_hues']


def test_nothing_clips():
    for n in (0, 40, 80, 120, LAST):
        m = probe.metrics(_f(n), step=3)
        assert m['peak_lum'] < 250, (n, m['peak_lum'])


def test_never_goes_static_for_long():
    """check.mjs 와 같은 잣대. 인트로가 멈춰 있으면 45 편이 다 그 자리에서 멈춘다."""
    run = worst = 0
    for n in range(0, LAST, 2):
        if probe.frame_diff(_f(n), _f(n + 1), step=13) < 0.0008:
            run += 1
            worst = max(worst, run)
        else:
            run = 0
    assert worst * 2 / 30 < 1.0, worst * 2 / 30


def test_palette_green_is_the_code_color():
    assert palette.HEX['green'] == '#00FF88'


if __name__ == '__main__':
    from _check import run
    run(globals())
