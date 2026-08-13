"""ep15s-2 본문4 — 6 비트 · 7.98 초. **원칙을 고쳐 쓰자 아무도 안 죽는다.**

자막 두 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~3.56 「원칙이라던 그 문장이 주민을 물려 죽게 했어요」
     가깝게. 앞 샷에서 무너진 그 사람이 그대로 누워 있다. 그 위에 막대 둘과 **문턱 선** —
     그 선이 곧 「그 문장」이다. 줄이 끝나갈 때 **선이 꺼진다.** 원칙을 지운 것이다
  3.69~7.48 「배고픔이 아니라, 초 단위로 죽는 게 먼저였어요」
     컷. 넓게. 같은 마을을 **다시 돌린다** — 덩이가 또 들어오고, 이번엔 **여섯이 다**
     손을 멈추고 돌아본 뒤 뛴다. 마지막이 그 사람이다. 아무도 안 맞는다

🔑 크기 판단: 첫 줄은 「이 사람이 죽었다」라 붙어야 하고, 둘째 줄은 「여섯이 다 산다」라
   넓어야 한다. 두 줄이 **다른 시점의 같은 마을**이라 여기서만 컷으로 자른다 —
   이어서 밀면 「죽은 사람이 일어나 뛴다」가 되어 버린다.
🔴 이 샷에서 아무도 안 맞는다. 그게 고쳐졌다는 뜻이고, 자막이 말하는 것과 같다.
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

SHOT, SID = 'body4', 'S4'
OUT = C.out_dir(SHOT)
DUR = C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 6
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

v, arms, fire, flakes, OBST = C.build()
HOME = [tuple(a.location) for a in arms]
hero = arms[C.HUNGRY]
G = C.gauges(ceiling=True)
LINES = stage.shot_lines(C.EP, SID)
FOCUS = {0: 0, 1: 1}
d = C.danger()
cam = stage.light_camera()

CUT = C.line_at(SID, 1) - 0.10          # 죽은 사람 → 다시 돌리는 마을
RULE_OFF = CUT - 0.85                   # 문턱 선이 꺼지는 순간 = 원칙을 지운다
# 🔑 여섯이 **다** 뛴다. 갈리는 것은 시각뿐이고, 마지막이 그 고집쟁이다
RUN0 = C.line_at(SID, 1) + 0.75
RUN_AT = [RUN0 + 0.18 * i for i in range(5)] + [RUN0 + 1.05]
# 누운 사람을 가깝게 — 🔴 겨냥을 **낮춘다.** 서 있는 높이(0.62)를 그대로 쓰면 빈 땅만 잡힌다
# 🔴 누운 사람 + 막대 둘이 **같이** 들어와야 한다. 1.05m 에 2.0m 앞은 막대가 잘렸다.
# 🔴 그리고 **정면에서 잡으면 누운 몸이 흰 공 하나다.** 이 인물은 머리가 커서, 접힌 몸이
#    머리 뒤에 통째로 가린다 — 앞 판 카메라가 그가 보는 쪽(−58°)과 거의 같은 방위였다.
#    **옆(그 방위에서 90° 돌린 자리)**에서 낮게 잡아야 접힌 등과 무릎이 실루엣으로 나온다.
CAM_A = ((1.46, -1.35, 0.82), (-0.50, -2.55, 0.30))
ANG = list(C.flee_angles([HOME[i][:2] for i in range(6)], OBST))
print('[body4-2] %d비트 · %.2f초 · 원칙 꺼짐 %.2f · 컷 %.2f · 첫 뛰기 %.2f'
      % (BEATS, DUR, RULE_OFF, CUT, RUN_AT[0]))


def draw(fi):
    t = fi / C.FPS

    if t < CUT:
        # ── 첫 줄: 무너진 채로 있다. 움직이는 것은 눈과 불뿐이다 ──
        for i, arm in enumerate(arms):
            arm.location = HOME[i]
            arm.rotation_euler = (0, 0, C.home_face(i))
            arm.hide_render = False
            for ob in arm.children:
                ob.hide_render = False
            C.work(arm, C.CAST[i][0], t)
        stage.pose(hero, motions.collapse(motions.COLLAPSE_DUR))
        d.hide_render = True

        right = C.cam_right(CAM_A[0], CAM_A[1])
        # 🔴 강조가 걸리면 막대가 0.8m 라, 0.30 에 세우면 꼭대기가 화면 위로 잘린다
        C.gauge_place(G, hero, right, z=0.14, side=0.72, emph=C.emphasis(LINES, t, FOCUS))
        # 🔑 **선이 꺼지는 것**이 이 줄의 사건이다 — 막대는 남고 규칙만 사라진다
        C.gauge_show(G, True, ceiling=(t < RULE_OFF))
        instrument.gauge_set(G[0], instrument.gauge_k(C.HUNGER))
        instrument.gauge_set(G[1], instrument.gauge_k(C.FLEE_STUBBORN))

        u = t / max(CUT, 0.01)
        stage.key_from_view(*CAM_A)
        C.lens(cam, C.CLOSE_LENS)
        (sx, sy, sz), at = CAM_A
        stage.aim(cam, (sx + 0.30 * u, sy - 0.22 * u, sz + 0.10 * u), at)
    else:
        # ── 둘째 줄: 같은 마을을 다시 돌린다. **여섯이 다 뛴다** ──
        tt = t - CUT
        dxy = C.danger_at(d, (t - CUT - 0.25) / max(DUR - CUT - 0.9, 0.01),
                          C.behind(C.HOME_H, C.WIDE[0], 0.9), t)
        d.hide_render = tt < 0.25
        for i, arm in enumerate(arms):
            C.startle(arm, i, HOME[i], C.CAST[i][0], t, RUN_AT[i], ANG[i], dxy)

        right = C.cam_right(C.WIDE[0], C.WIDE[1])
        # 🔑 이번엔 **고집쟁이 것도 도망이 위**다. 성향은 순서를 못 뒤집고 진폭만 갖는다
        C.gauge_place(G, hero, right, z=0.95, side=0.24, emph=C.emphasis(LINES, t, FOCUS))
        C.gauge_show(G, tt > 0.15 and t < RUN_AT[C.HUNGRY] + 0.4, ceiling=False)
        instrument.gauge_set(G[0], instrument.gauge_k(C.HUNGER))
        instrument.gauge_set(G[1], instrument.gauge_k(C.FLEE_AFTER))

        u = C.ease((t - RUN_AT[0]) / max(DUR - RUN_AT[0], 0.01))
        stage.key_from_view(*C.WIDE)
        C.lens(cam, C.WIDE_LENS)
        (sx, sy, sz), at = C.WIDE
        stage.aim(cam, (sx + 1.05 * u, sy - 1.05 * u, sz + 0.45 * u), at)

    village.flicker(fire, t)
    village.snowfall(flakes, t, wind=0.2)


stage.bake(OUT, NF, C.FPS, draw, 'body4-2')
