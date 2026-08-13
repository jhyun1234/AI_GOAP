"""회차 샷을 **여러 프로세스로 동시에** 굽는다.

    python bake.py                   # 샷 여섯 전부
    python bake.py hook body1        # 고른 것만
    python bake.py -j 2              # 동시에 몇 개
    python bake.py --range 0:23      # 샷마다 앞 24프레임만 (재거나 눈으로 볼 때)

한 프로세스 안에서는 못 줄인다 — 렌더가 시간의 99%이고 그건 GPU 일이다.
줄어드는 것은 **벽시계 시간**이다: 샷 여섯이 서로를 안 기다린다.

🔴 `-j` 를 올린다고 계속 빨라지지 않는다. 샷들이 같은 GPU 를 나눠 쓴다.
   기본값은 실측으로 정한 것이다(README 의 「병렬은 어디까지 먹히나」).
"""
import argparse, os, subprocess, sys, time
from concurrent.futures import ThreadPoolExecutor

DIR = os.path.dirname(os.path.abspath(__file__))
BLENDER = os.environ.get(
    'BLENDER_EXE', r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe")
SHOTS = ['intro', 'hook', 'body1', 'body2', 'body3', 'outro']


def shots_for(ep):
    """그 회차가 실제로 쓰는 샷 — **scene.json 의 `spec.frames` 가 정본이다.**
    🔴 목록을 두 군데 적으면 언젠가 한쪽만 고쳐진다. 회차가 샷을 늘리면(ep15s-2 는 본문이
       넷이다) 여기 손대지 않아도 따라온다."""
    import json, re
    p = os.path.join(os.path.dirname(DIR), 'episodes', ep, 'scene.json')
    d = json.load(open(p, encoding='utf-8'))
    out = []
    for s in d['shots']:
        m = re.search(r'/3d/(?:%s/)?([^/]+)/frames' % re.escape(ep), s.get('spec', {}).get('frames', ''))
        if m and m.group(1) not in out and not m.group(1).startswith('_'):
            out.append(m.group(1))
    return out


def script_for(shot, ep):
    """회차 전용 샷 스크립트가 있으면 그것, 없으면 평평한 옛 자리(ep15s-1)."""
    own = os.path.join(DIR, ep, 'render_%s.py' % shot)
    return own if os.path.exists(own) else os.path.join(DIR, 'render_%s.py' % shot)


def one(shot, env, ep='ep15s-1'):
    script = script_for(shot, ep)
    if not os.path.exists(script):
        return shot, 0.0, 1, '', '%s 가 없다' % script
    t0 = time.time()
    p = subprocess.run([BLENDER, '--background', '--factory-startup', '--python', script],
                       capture_output=True, text=True, encoding='utf-8', errors='replace',
                       env=env)
    # 🔴 **블렌더의 종료 코드를 믿지 마라.** 파이썬 스크립트가 단언에서 죽어도 0 을 준다.
    #    그래서 궤적 검사에 걸려 한 프레임도 안 구운 샷을 `ok` 로 보고했고, 앞 판 프레임이
    #    그대로 남아 있어서 **낡은 그림이 최종본으로 나갔다.** 출력에서 직접 잡는다.
    rc = p.returncode
    if rc == 0 and ('Traceback (most recent call last)' in (p.stderr + p.stdout)):
        rc = 1
    return shot, time.time() - t0, rc, p.stdout, p.stderr


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('shots', nargs='*', default=None)
    ap.add_argument('-j', type=int, default=3, help='동시에 굽는 샷 수')
    ap.add_argument('--range', dest='rng', help='프레임 범위 "0:23"')
    ap.add_argument('--ep', default='ep15s-1', help='회차 (기본 ep15s-1)')
    a = ap.parse_args()

    known = SHOTS if a.ep == 'ep15s-1' else ['intro'] + shots_for(a.ep)
    shots = a.shots or [s for s in known if s != 'intro' or a.ep == 'ep15s-1']
    unknown = [s for s in shots if s not in known]
    if unknown:
        sys.exit('모르는 샷: %s  (있는 것: %s)' % (', '.join(unknown), ', '.join(known)))

    env = dict(os.environ)
    if a.rng:
        env['BAKE_RANGE'] = a.rng

    print('[bake] %s · %d 샷 · 동시 %d · %s' % (a.ep, len(shots), a.j, ', '.join(shots)))
    t0 = time.time()
    with ThreadPoolExecutor(max_workers=a.j) as ex:
        results = list(ex.map(lambda s: one(s, env, a.ep), shots))
    wall = time.time() - t0

    bad = 0
    for shot, dt, rc, out, err in sorted(results, key=lambda r: -r[1]):
        mark = 'ok ' if rc == 0 else '실패'
        print('  %s %-7s %6.1f초' % (mark, shot, dt))
        if rc != 0:
            bad += 1
            # 🔴 실패는 통째로 보여 준다. 블렌더는 단언 실패를 stderr 로 뱉는다.
            print((err or out).strip()[-2000:])
    print('[bake] 벽시계 %.1f초 · 합 %.1f초 · 실패 %d'
          % (wall, sum(r[1] for r in results), bad))
    sys.exit(1 if bad else 0)


if __name__ == '__main__':
    main()
