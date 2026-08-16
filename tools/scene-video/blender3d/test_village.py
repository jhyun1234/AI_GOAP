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


def test_scale_makes_sense_next_to_the_villager():
    """집이 주민보다 훨씬 크거나 작으면 축척이 깨져 마을로 안 읽힌다.

    🔴 **미터가 아니라 비율로 잰다.** 앞 판은 `1.4 <= 집높이 <= 2.6` 이었는데, 주민 키가
       0.95 에서 1.70 으로 바뀌자 그 창은 「집이 사람 허리께」도 통과시킨다 —
       재는 자리가 틀린 게이트다. 짝이 되는 값은 언제나 **그때의 주민 키**다."""
    r = _report()
    ratio = r['house_height'] / r['subject_height']
    assert 1.5 <= ratio <= 2.7, (ratio, r['house_height'], r['subject_height'])


def test_work_spots_are_far_enough_apart():
    """여섯의 궤적이 **겹치는 것**이 이 회차의 그림이다. 자리가 붙어 있으면 안 보인다."""
    r = _report()
    xs = r['spots']
    # 🔴 `== 4` 였는데 `bush`(식량 자원 노드)가 늘면서 이 검사가 조용히 죽어 있었다.
    #    자리 수를 못 박으면 자리를 하나 더 놓는 날 검사부터 고쳐야 한다 — 이 검사가
    #    보는 것은 **개수가 아니라 간격**이다.
    assert len(xs) >= 4, xs
    # 🔴 절대 미터로 재면 마을 축척이 바뀔 때마다 거짓말이 된다 — **주민 키의 배수**다.
    gap = 1.9 * r['subject_height']
    for i in range(len(xs)):
        for j in range(i + 1, len(xs)):
            d = ((xs[i][0] - xs[j][0]) ** 2 + (xs[i][1] - xs[j][1]) ** 2) ** 0.5
            assert d > gap, (i, j, round(d, 2), round(gap, 2))


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


def test_the_fire_pit_reads_as_a_fire_pit_without_any_fire():
    """🔴 「불이 없는 게 제일 이상해」의 진짜 원인은 불이 아니라 그것이 모닥불처럼
    **생기지 않은 것**이었다(돌 상자 하나). 돌 테두리와 장작은 세계층이라 유채색 예산을
    한 톨도 안 쓴다 — 즉 이 고침은 모든 샷에 공짜로 적용된다."""
    r = json.load(open(REPORT, encoding='utf-8'))
    assert r['fire']['stones'] >= 6, r['fire']
    assert r['fire']['logs'] >= 3, r['fire']


def test_the_village_never_lights_the_fire_itself():
    """🔴 앰버는 뜻층이다 — 불을 켜면 그 프레임의 유채색 둘 중 하나를 쓴다.
    마을이 기본으로 켜 버리면 모든 샷이 예산 하나를 잃은 채로 시작한다. **샷이 정한다.**"""
    src = open(os.path.join(HERE, 'village.py'), encoding='utf-8').read()
    body = src[src.index('def build('):]
    assert 'flame(' not in body, 'build() 가 불을 켜고 있다'
    assert "meaning_mat('amber'" in src, '불이 앰버가 아니다 — 앰버는 삶·온기다(설계 §2)'


def test_the_house_has_openings_cut_into_it():
    """민 상자는 꼭짓점이 여덟이다. 문·창을 파면 늘어난다 — 「팠다」를 주장이 아니라 값으로 남긴다.
    🔴 집을 상자로 되돌리는 것은 한 줄이면 되고, 그 한 줄은 검사가 없으면 안 잡힌다."""
    r = json.load(open(REPORT, encoding='utf-8'))
    assert r['house_verts'] > 8, ('문도 창도 없는 상자다', r['house_verts'])


def test_no_prop_is_a_single_box():
    """🔴 소품 판정은 매번 같은 결론이었다 — **상자 하나는 아무것으로도 안 읽힌다.**
    우물은 줄과 두레박이, 밭은 두둑이, 모닥불은 돌 테두리가 그것을 무엇으로 만든다."""
    r = json.load(open(REPORT, encoding='utf-8'))
    for name, least in (('well', 5), ('field', 7), ('fire', 10)):
        assert r['parts'][name] >= least, (name, r['parts'][name])


if __name__ == '__main__':
    from _check import run
    run(globals())
