"""구운 모캡 표를 `motions` 와 **같은 계약**으로 되돌린다 (M32 영상 트랙 · 2026-08-16).

    import mocap
    shoot = mocap.load('shoot', dur=1.6)      # f(t) — MOTIONS 의 함수들과 같은 물건
    stage.pose(arm, shoot(t))

`motions.py` 의 함수들은 **시간의 함수**고 길이가 대본에서 역산된다(`3D_대본_문법.md` §5).
모캡은 반대로 길이가 구워져 있다 — 그래서 여기서 **위상(0~1)으로 정규화**해 되돌린다.
그러면 `dur` 이 자유 인자가 되어 대본이 길이를 정하는 규약이 그대로 산다.

## 양념 (`gain`) — 왜 필요한가

기존 22종은 예비 동작·오버슛·`SQUASH` 로 과장돼 있고 모캡은 안 그렇다. 🔑 **한 사람이
밭일하다 싸우는 게 이 게임의 이야기라, 그 사람 안에서 스타일이 갈리면 안 된다.**
`gain` 은 레스트(0) 로부터의 회전을 비례로 부풀린다 — 1.0 이 모캡 원본이다.

🔴 **`gain` 은 만능이 아니다.** 부풀리면 커지지만 **타이밍은 안 바뀐다**(모캡은 예비 동작이
   짧고 고르다). 파일럿에서 「같은 사람으로 안 보인다」가 나오면 범인은 대개 진폭이 아니라
   타이밍이고, 그때는 이 손잡이가 아니라 손으로 짜는 쪽이 답이다.

## `@root` · `@squash` 가 없는 이유

모캡 표에는 골반 이동이 없고(`mocap2pose.py --root` 전까지), 스쿼시 층은 모캡에 아예 없다.
둘 다 **안 넣는다** — `stage.pose` 가 없는 키를 중립으로 처리하므로 조용히 빠진다.
지어내서 넣는 순간 「실측한 축」(`stage.pose` 주석)이 추측으로 오염된다.
"""
import json
import os

DIR = os.path.dirname(os.path.abspath(__file__))
MOCAP = os.path.join(DIR, 'mocap')

_cache = {}


def table(name):
    """`mocap/<이름>.pose.json` 을 한 번만 읽는다."""
    if name not in _cache:
        with open(os.path.join(MOCAP, f'{name}.pose.json'), encoding='utf-8') as fp:
            _cache[name] = json.load(fp)
    return _cache[name]


def load(name, dur=None, loop=True, gain=1.0):
    """모캡 표 → `f(t) -> {뼈: (x, y, z)}`.

    dur  : 한 번 도는 데 걸리는 초. None 이면 구운 길이 그대로(frames / fps).
    loop : True = 되돌아 반복(걷기·쏘기) · False = 1회 재생 후 마지막 포즈 유지(피격).
    gain : 회전 진폭 배율 (윗글).

    🔴 프레임 사이는 **선형 보간**이다. 그래서 굽는 쪽이 ±π 감김을 미리 펴 둔다
       (`mocap2pose.unwrap`) — 안 펴면 보간이 반대로 한 바퀴 돈다.
    """
    t_ = table(name)
    data, bones = t_['data'], t_['bones']
    n = t_['frames']
    span = dur if dur and dur > 0 else n / float(t_['fps'])

    def f(t):
        u = (t % span) / span if loop else min(max(t, 0.0), span) / span
        x = u * (n - 1)
        i = int(x)
        j = min(i + 1, n - 1)
        k = x - i
        a, b = data[i], data[j]
        out = {}
        for nm in bones:
            pa, pb = a[nm], b[nm]
            out[nm] = tuple(gain * (pa[c] + (pb[c] - pa[c]) * k) for c in range(3))
        return out

    f.__name__ = f'mocap_{name}'
    f.__doc__ = (f'모캡 «{name}» — {n}프레임 @{t_["fps"]}fps, {span:.2f}초로 재생 '
                 f'(loop={loop}, gain={gain}).')
    return f


def demo():
    """자체 점검 — 표가 계약을 지키는가. `python mocap.py` 로 돈다(블렌더 불필요)."""
    import math
    for name in ('shoot', 'flinch', 'limp'):
        t_ = table(name)
        f = load(name, dur=2.0)
        p0, pm, p1 = f(0.0), f(1.0), f(1.999)
        assert set(p0) == set(t_['bones']), f'{name}: 뼈 목록이 다르다'
        for pose in (p0, pm, p1):
            for nm, r in pose.items():
                assert len(r) == 3, f'{name}.{nm}: 성분이 3이 아니다'
                assert all(abs(v) < 4 * math.pi for v in r), f'{name}.{nm}: 각이 튄다 {r}'
        # 보간이 실제로 값을 만드는가 (표가 상수면 파일럿이 무의미하다)
        moved = max(abs(pm[nm][c] - p0[nm][c]) for nm in p0 for c in range(3))
        assert moved > 0.05, f'{name}: 0초와 1초의 포즈가 같다 ({moved:.4f})'
        # loop=False 는 끝에서 멈춘다
        g = load(name, dur=2.0, loop=False)
        assert g(2.5) == g(2.0), f'{name}: loop=False 인데 끝에서 안 멈춘다'
        print(f'✅ {name}: 뼈 {len(t_["bones"])} · {t_["frames"]}프레임 · '
              f'최대 이동 {moved:.2f}rad')


if __name__ == '__main__':
    demo()
