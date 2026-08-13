"""ep15s-2 본문3 — 8 비트 · 11.14 초. **도망을 위로 올렸다. 다섯은 산다, 하나는 안 산다.**

자막 세 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~3.40 「그래서 도망을 105로 올려 배고픔 위에 놨어요」
     가깝게. 밭 앞 주민 한 사람의 막대 둘. 도망(자홍)이 차올라 **문턱 선을 넘어**
     배고픔(연노랑)보다 **위**로 간다. 이 편에서 유일하게 선을 넘는 순간이다
  3.53~6.65 「겁 많은 주민은 누구보다 먼저 도망가고요」
     카메라가 뒤로 물러나 마을 전체를 잡는다. 덩이는 **아직 멀리** 있는데 다섯이
     차례로 **손을 멈추고 돌아본 뒤 뛴다** — 0.20초씩 어긋난 그 시차가 「겁 많을수록 먼저」다
  6.79~10.66 「겁 없는 고집쟁이만 95라, 밥부터 찾다 물려요」
     남은 한 사람 쪽으로 밀고 들어간다. 그의 도망 막대만 **선 아래 그대로**다.
     덩이가 등 뒤로 와서 닿고, 그는 배를 움켜쥐고 **무너진다**

🔑 크기 판단: 한 샷 안에서 필요한 크기가 세 번 바뀐다(수 → 흩어짐 → 한 사람). 컷으로
   자르지 않고 **카메라가 물러났다 다시 들어간다** — 컷으로 자르면 같은 마을이 장소 셋이
   되고, 이 샷의 뜻(같은 자리에서 누구는 살고 누구는 안 산다)이 깨진다.
🔴 훅과 **같은 사건을 다시 그린다.** 다른 것은 하나뿐이다 — 이번엔 다섯이 덩이가 닿기
   한참 전에 뛴다. 그 시차가 「도망을 배고픔 위에 놨다」의 3D 판이다.
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

SHOT, SID = 'body3', 'S3'
OUT = C.out_dir(SHOT)
DUR = C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 8
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT
LENS = 36                              # 🔴 샷 안에서 안 바꾼다 — 크기는 카메라가 움직여서 만든다

CROSS = 0                              # 첫 줄에서 막대가 선을 넘는 사람 — 밭 앞 주민
v, arms, fire, flakes, OBST = C.build()
HOME = [tuple(a.location) for a in arms]
hero = arms[C.HUNGRY]
G_CROSS = C.gauges(ceiling=True)
G_HERO = C.gauges(ceiling=True)
LINES = stage.shot_lines(C.EP, SID)
FOCUS = {0: 1, 1: 1, 2: 1}             # 세 줄 다 **도망**을 부른다(105 · 먼저 · 95)
d = C.danger()
cam = stage.light_camera()
C.lens(cam, LENS)
stage.key_from_view(*C.WIDE)

# ── 카메라 셋(컷 아님 · 이어진 이동) ──
CAM_A = ((-0.30, -5.55, 1.85), (-2.65, -2.20, 0.88))    # 밭 앞 주민 — 막대가 읽히는 크기
CAM_B = C.WIDE                                           # 흩어지는 다섯
CAM_C = ((1.15, -5.45, 2.00), (-0.45, -2.35, 0.80))      # 남은 한 사람
T_AB = C.line_at(SID, 1) - 0.30
T_BC = C.line_at(SID, 2) - 0.20

# 🔑 **시차가 「겁 많다」다.** 값은 안 그리고 순서로만 보인다.
FLEE0 = C.line_at(SID, 1) + 0.32
RUN_AT = [FLEE0 + 0.20 * i for i in range(5)]
COLLAPSE_RATE = 1.20
HIT_AT = DUR - motions.COLLAPSE_DUR / COLLAPSE_RATE - 0.10
assert HIT_AT > C.line_at(SID, 2), '물리는 순간이 그 줄보다 먼저다: %.2f' % HIT_AT
RISE_AT = 0.55                          # 막대가 선을 넘기 시작하는 시각
RISE_SPAN = 1.70
ANG = list(C.flee_angles([HOME[i][:2] for i in range(5)], OBST))
print('[body3-2] %d비트 · %.2f초 · 넘김 %.2f · 첫 뛰기 %.2f · 물림 %.2f'
      % (BEATS, DUR, RISE_AT, RUN_AT[0], HIT_AT))


def view(t):
    """이 시각의 카메라. A → B → C 로 **이어서** 간다."""
    if t < T_AB:
        a, b, u = CAM_A, CAM_B, C.ease((t - (T_AB - 0.85)) / 0.85)
    else:
        a, b, u = CAM_B, CAM_C, C.ease((t - T_BC) / max(DUR - T_BC, 0.01))
    loc = tuple(p + (q - p) * u for p, q in zip(a[0], b[0]))
    at = tuple(p + (q - p) * u for p, q in zip(a[1], b[1]))
    return loc, at


def draw(fi):
    t = fi / C.FPS
    dxy = C.danger_at(d, (t - 0.9) / max(HIT_AT - 0.9, 0.01),
                      C.behind(C.HOME_H, CAM_C[0], 0.85), t)
    d.hide_render = t < 0.9

    # ── 다섯 — 일하다 **멈칫하고 돌아본 뒤** 뛴다 ──
    for i in range(5):
        C.startle(arms[i], i, HOME[i], C.CAST[i][0], t, RUN_AT[i], ANG[i], dxy)

    # ── 남은 한 사람 — 계속 먹다가 물려 무너진다 ──
    hero.location = HOME[C.HUNGRY]
    hero.rotation_euler = (0, 0, C.home_face(C.HUNGRY))
    spec = motions.eat(t)
    if t >= HIT_AT:
        w = C.ease((t - HIT_AT) / 0.26)
        spec = motions.blend(spec, motions.collapse((t - HIT_AT) * COLLAPSE_RATE), w)
        spec = dict(spec)
        sq = 0.20 * math.exp(-((t - HIT_AT) / 0.20) ** 2)
        base = spec.get(motions.SQUASH, (0.0, 0.0, 0.0))[0]
        spec[motions.SQUASH] = (min(0.30, base + sq), 0.0, 0.0)
    stage.pose(hero, spec)

    # ── 막대 둘 — 하나는 선을 넘고, 하나는 못 넘는다 ──
    loc, at = view(t)
    right = C.cam_right(loc, at)
    em = C.emphasis(LINES, t, FOCUS)
    rise = C.ease((t - RISE_AT) / RISE_SPAN)

    # ① 밭 앞 주민 — 도망이 **선을 넘는다.** 뛰기 시작하면 막대를 끈다(막대 숲 방지)
    C.gauge_show(G_CROSS, t < RUN_AT[CROSS] + 0.35)
    C.gauge_place(G_CROSS, arms[CROSS], right, z=0.62, side=0.58, emph=em)
    instrument.gauge_set(G_CROSS[0], instrument.gauge_k(C.HUNGER))
    instrument.gauge_set(G_CROSS[1], instrument.gauge_k(C.FLEE_BEFORE)
                         + (instrument.gauge_k(C.FLEE_AFTER)
                            - instrument.gauge_k(C.FLEE_BEFORE)) * rise)

    # ③ 고집쟁이 — 도망이 **선 아래 그대로**다. 셋째 줄에서만 켠다
    C.gauge_show(G_HERO, t > T_BC - 0.6)
    C.gauge_place(G_HERO, hero, right, z=0.62, side=0.58, emph=em)
    instrument.gauge_set(G_HERO[0], instrument.gauge_k(C.HUNGER))
    instrument.gauge_set(G_HERO[1], instrument.gauge_k(C.FLEE_BEFORE)
                         + (instrument.gauge_k(C.FLEE_STUBBORN)
                            - instrument.gauge_k(C.FLEE_BEFORE)) * rise)

    stage.aim(cam, loc, at)
    village.flicker(fire, t)
    village.snowfall(flakes, t, wind=0.2)


stage.bake(OUT, NF, C.FPS, draw, 'body3-2')
