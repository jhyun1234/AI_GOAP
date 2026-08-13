"""ep15s-2 본문2 — 8 비트 · 10.81 초. **되풀이. 그리고 물린다.**

자막 세 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~3.36 「재 보니 배고픔이 100인데 도망은 92였어요」
     가깝게. 그의 옆에 막대 둘이 나란히 선다 — 배고픔이 확실히 **위**다.
     둘이 번갈아 커진다(대본이 두 수를 차례로 부른다). 그는 그냥 먹고 있다
  3.49~6.18 「게다가 실패해도 배고픔을 또 골라요」
     **되풀이.** 배고픔 막대가 찼다가 **툭 떨어지고 또 찬다.** 몸도 같은 박자다 —
     먹으려다 만다, 또, 또. 겨울이라 손에 잡히는 게 없다
  6.31~10.34 「겨울엔 먹을 게 없는데, 밥, 밥, 밥 하다가 물려요」
     되풀이가 빨라진다. 붉은 덩이가 **등 뒤로** 와서 닿는다 — 몸이 한 번 눌리고,
     배를 움켜쥐고 **무너진다**(`collapse`). 막대 둘이 그대로 남는다

🔑 크기 판단: 세 줄이 전부 **한 사람의 수와 몸**이다. 넓힐 이유가 하나도 없다 —
   막대 둘의 높이 차와 「먹으려다 마는」 손이 안 보이면 이 샷은 아무 말도 못 한다.
🔴 **이 편의 심장이다.** 원문의 결함은 우선순위 역전이 아니라 되풀이다 —
   「배고픔 목표는 실패해도 쿨다운 없이 다시 고를 수 있다」.
🔴 앞 판은 물린 뒤에 `huddle`(움츠림) + 골반을 조금 내리는 것으로 때웠다. 그건 「추워한다」다.
   **죽는 것은 `collapse` 다** — 배를 움켜쥐고 휘청이다 접힌다.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import instrument
import motions
import stage
import village

SHOT, SID = 'body2', 'S2'
OUT = C.out_dir(SHOT)
DUR = C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 8
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

v, arms, fire, flakes, _obst = C.build()
HOME = [tuple(a.location) for a in arms]
hero = arms[C.HUNGRY]
# 🔴 **막대만 쓴다**(2026-08-13 판정: 원판은 차이가 안 보인다). 배고픔(연노랑)·도망(자홍).
G = C.gauges()
RIGHT = C.cam_right(C.CLOSE[0], C.CLOSE[1])
LINES = stage.shot_lines(C.EP, SID)
FOCUS = {0: (0, 1), 1: 0, 2: 0}
d = C.danger()
cam = stage.light_camera()
C.lens(cam, C.CLOSE_LENS)
stage.key_from_view(*C.CLOSE)

TRY_FROM = C.line_at(SID, 1)                  # 「실패해도 또 골라요」부터 되풀이
TRY_PERIOD = 1.28                             # 한 번 「먹으려다 마는」 데 걸리는 시간
LAST = C.line_at(SID, 2)                      # 「밥, 밥, 밥 하다가 물려요」
# 🔴 물리는 순간은 **무너짐이 자막 안에서 끝나도록** 역산한다. 손으로 적으면 샷이 끝난 뒤에
#    쓰러지고, 화면에는 「물렸다」까지만 남는다.
COLLAPSE_RATE = 1.20                          # 물어뜯긴 것은 앉는 것보다 빠르다
HIT_AT = DUR - motions.COLLAPSE_DUR / COLLAPSE_RATE - 0.10
assert HIT_AT > LAST, '물리는 순간이 그 줄보다 먼저다: %.2f' % HIT_AT
TRY_TO = HIT_AT - 0.20
BEHIND = C.behind(C.HOME_H, C.CLOSE[0], 0.80)   # 카메라에서 봤을 때 그의 등 뒤
DANGER_IN = LAST - 1.30
print('[body2-2] %d비트 · %.2f초 · 되풀이 %.1f회 · 물림 %.2f초'
      % (BEATS, DUR, (TRY_TO - TRY_FROM) / TRY_PERIOD, HIT_AT))


def try_phase(t):
    """되풀이 위상(0~1). 되풀이 구간 밖이면 None. 🔑 뒤로 갈수록 **빨라진다** —
    「밥, 밥, 밥」은 점점 급해지는 말이다."""
    if not (TRY_FROM <= t < TRY_TO):
        return None
    span = TRY_TO - TRY_FROM
    rate = 1.0 + 0.45 * ((t - TRY_FROM) / span)      # 끝에서 1.45 배 빠르다
    return ((t - TRY_FROM) * rate % TRY_PERIOD) / TRY_PERIOD


def draw(fi):
    t = fi / C.FPS

    for i, arm in enumerate(arms):
        if i == C.HUNGRY:
            continue
        C.work(arm, C.CAST[i][0], t)
        arm.location = HOME[i]
        arm.rotation_euler = (0, 0, C.home_face(i))

    # ── ①②③ 먹는다 → 먹으려다 만다(되풀이) ──
    u = try_phase(t)
    k = math.sin(math.pi * u) ** 1.6 if u is not None else (1.0 if t < TRY_FROM else 0.0)
    spec = motions.blend(motions.look_around(t), motions.eat(t), k)
    hero.location = HOME[C.HUNGRY]
    hero.rotation_euler = (0, 0, C.home_face(C.HUNGRY))

    # ── 막대: 배고픔이 찼다가 **툭 떨어지고 또 찬다**(되풀이). 도망은 그 아래 그대로 ──
    # 🔴 가까운 샷에서 머리 위(0.95)에 두면 **막대 꼭대기가 화면 위로 잘린다**(강조하면
    #    0.8m 라 더 그렇다). 옆으로 밀고 어깨 높이에 세운다 — 높이를 견주는 것이 이 편이다.
    C.gauge_place(G, hero, RIGHT, z=0.56, side=0.62, emph=C.emphasis(LINES, t, FOCUS))
    C.gauge_show(G, True)
    hk = instrument.gauge_k(C.HUNGER)
    if u is not None:
        hk = hk * C.ease(min(u / 0.72, 1.0)) if u < 0.86 else hk * 0.10
    instrument.gauge_set(G[0], hk)
    instrument.gauge_set(G[1], instrument.gauge_k(C.FLEE_BEFORE))

    # ── ④ 닿는다 — 한 번 눌리고 **무너진다** ──
    if t >= HIT_AT:
        w = C.ease((t - HIT_AT) / 0.26)
        spec = motions.blend(spec, motions.collapse((t - HIT_AT) * COLLAPSE_RATE), w)
        spec = dict(spec)
        sq = 0.20 * math.exp(-((t - HIT_AT) / 0.20) ** 2)   # 물린 그 한 순간의 타격
        base = spec.get(motions.SQUASH, (0.0, 0.0, 0.0))[0]
        spec[motions.SQUASH] = (min(0.30, base + sq), 0.0, 0.0)
    stage.pose(hero, spec)

    # ── 덩이가 등 뒤로 ──
    C.danger_at(d, (t - DANGER_IN) / max(HIT_AT - DANGER_IN, 0.01), BEHIND, t)
    d.hide_render = t < DANGER_IN

    # 카메라 — 아주 조금씩 옆으로 흐르다, 쓰러지면 **따라 내려간다**
    (sx, sy, sz), (ax, ay, az) = C.CLOSE
    u2 = t / DUR
    dn = C.ease((t - HIT_AT) / 1.10)
    stage.aim(cam, (sx - 0.45 * u2, sy - 0.18 * u2, sz + 0.08 * u2 - 0.14 * dn),
              (ax, ay, az - 0.26 * dn))

    village.flicker(fire, t)
    village.snowfall(flakes, t, wind=0.35)


stage.bake(OUT, NF, C.FPS, draw, 'body2-2')
