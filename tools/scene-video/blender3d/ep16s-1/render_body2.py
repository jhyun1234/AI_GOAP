"""ep16s-1 본문2 — 12 비트 · 16.07 초. **죽는 순간에 넘겨서, 도구가 비석 옆에 선다.**

자막 네 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~4.29 「제가 가진 건 죽은 사람이 하나 늘었다는 것뿐이었어요」
     무덤들 위에 막대 하나가 뜨고 **한 칸 오른다.** 그게 가진 것의 전부다
  4.29~8.68 「그래서 아직 살아 있을 때, 죽는 그 순간에 넘기게 했어요」
     가깝게. 곡괭이를 든 사람이 **일하다 무너지고**, 손에서 곡괭이가 **굴러 나온다** —
     아직 살아 있는 동안 손을 떠나는 것이 이 줄이다
  8.68~12.01 「이제 비석 옆에 그 사람이 쓰던 도구가 서요」
     무덤이 그 자리를 대신하고, 굴러 나온 곡괭이가 **비석 옆에 선다**(뜻층 보라)
  12.01~16.07 「도끼면 나무꾼, 물뿌리개면 밭 보던 사람이고요」
     물러나며 비석마다 도구가 선다 — **도끼 · 물뿌리개 · 곡괭이 · 망치**가 한 프레임에

🔴 **막대는 계기층**(시안)이라 뜻층 예산을 안 쓴다. 보라(도구)만 뜻층이고, 한 프레임
   2색 상한의 절반이다.
🔴 도구 넷 < 죽은 일곱이라 「일곱이 다 다르다」로 그리지 않는다 — 자막이 부르는 것도
   네 종류의 뜻이다(`common.TOOL_SHOWCASE`).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import instrument
import stage
import village

SHOT, SID = 'body2', 'S2'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 12
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

L = [C.line_at(SID, i) for i in range(4)]
HERO = 2                                   # 다시 살려 보여 주는 사람 — 곡괭이(mine)
v, arms, graves = C.build()
HOME = [tuple(a.location) for a in arms]
C.dead_hide(arms)

# ── 계기 · 도구 ────────────────────────────────────────
COUNT = instrument.gauge((0, 0, 0), color='instrument')
instrument.gauge_set(COUNT, 0.34)
HELD = stage.hold(arms[HERO], village.pickaxe(), 'hand.R', (0.0, 0.02, 0.0))
FALLEN = village.pickaxe()                 # 손을 떠나 땅에 구르는 같은 도구(세계층)
FALLEN.rotation_mode = 'XYZ'
STOOD = [C.tool(kind, violet=True) for _job, _p, _s, kind in C.DEAD]
for i, (_job, (x, y), _spot, _kind) in enumerate(C.DEAD):
    C.tool_stand(STOOD[i], x, y, 1.0, face=C.home_face(i))
    C.tool_show(STOOD[i], False)

FALL_AT = L[1] + 1.15                      # 「죽는 그 순간」이 말해지는 동안 무너진다
DROP_AT = FALL_AT + 0.42                   # 손을 떠나는 시각 — 무너짐 도중이다
HX, HY = C.DEAD[HERO][1]

# 🔴 첫 구간을 고정하면 **정적 4초**가 된다(게이트 문턱 3초). 막대만 움직이는 동안
#    카메라가 천천히 흐른다 — 홀드로 시간을 때우지 않는다.
KEY = [(0.00, (C.MID[0][0] + 0.85, C.MID[0][1] - 0.55, C.MID[0][2] + 0.20),
        (C.MID[1][0] - 0.15, C.MID[1][1] + 0.20, C.MID[1][2])),
       (L[1] - 0.20, C.MID[0], C.MID[1]),
       (L[1] + 1.30, C.CLOSE[0], C.CLOSE[1]),
       (L[2] + 0.80, (0.15, -5.55, 1.05), (HX, HY + 0.15, 0.42)),
       (L[3] + 0.50, C.EYE[0], C.EYE[1]),
       (DUR, (2.55, -7.55, 2.80), (-0.35, -1.05, 0.82))]

cam = stage.light_camera()
C.lens(cam, C.MID_LENS)
stage.key_from_view(*C.MID)
print('[body2-16-1] %d비트 · %.2f초 · 줄 %s' % (BEATS, DUR, [round(x, 2) for x in L]))


def draw(fi):
    t = fi / C.FPS
    alive = L[1] - 0.30 <= t < L[2] - 0.25      # 되살려 보여 주는 구간
    for i, g in enumerate(graves):
        C.grave_show(g, not (alive and i == HERO))
        C.grave_rise(g, 1.0)
    hero = arms[HERO]
    hero.hide_render = not alive
    for ob in hero.children:
        ob.hide_render = not alive
    if alive:
        C.fall(hero, HERO, HOME[HERO], t, FALL_AT)
    # 🔴 굴러 나온 도구는 **손에서 떨어져 땅에 눕는다.** 손에 붙은 채 두면 「넘겼다」가
    #    아니라 「들고 죽었다」가 된다 — 이 줄의 사건은 손을 떠나는 것이다
    # 🔴 **부모를 프레임마다 갈아 끼우지 않는다.** 손에 붙은 것과 땅에 구르는 것을 **다른
    #    물건 둘**로 두고 하나만 켠다 — 부모를 바꾸면 행렬이 프레임에 따라 누적돼
    #    결정성 게이트가 죽는다(런북 §7 의 `rotation_euler` 누적과 같은 함정).
    C.tool_show(HELD, alive and t < DROP_AT)
    rolling = alive and t >= DROP_AT
    C.tool_show(FALLEN, rolling)
    if rolling:
        k = C.ease((t - DROP_AT) / 0.55)
        FALLEN.location = (HX + 0.30 * k, HY - 0.34 * k, 0.26 - 0.22 * k)
        FALLEN.rotation_euler = (math.radians(90 - 76 * k), 0.0,
                                 C.home_face(HERO) + 1.15 * k)
    # 비석 옆에 서는 도구 — 주인공 것이 먼저, 나머지는 마지막 줄에서 함께
    for i, root in enumerate(STOOD):
        if i == HERO:
            on = t >= L[2] - 0.10
        else:
            on = t >= L[3] - 0.05 + 0.10 * (i % 4)
        C.tool_show(root, on)
    C.work(arms[C.LIVE_I], C.CAST[C.LIVE_I][0], t)
    # 막대 — 「죽은 사람이 하나 늘었다」. 한 칸만 오른다
    instrument.gauge_set(COUNT, 0.34 + 0.11 * C.ease((t - 1.15) / 0.60))
    instrument.gauge_at(COUNT, -0.85, -2.35, 1.05)
    for key in ('track', 'fill'):
        COUNT[key].hide_render = t > L[1] - 0.15
    C.fly(cam, KEY, t)


stage.bake(OUT, NF, C.FPS, draw, 'body2-16-1')
