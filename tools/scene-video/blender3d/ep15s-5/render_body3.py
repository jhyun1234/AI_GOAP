"""ep15s-5 본문3 — 9 비트 · 11.84 초. **몸은 안 움직이는데 막대가 그의 하루를 다 말한다.**

자막 세 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~3.19 「이야기가 없던 게 아니라, 로그에만 있었어요」
     쓰러진 사람에게 가깝게 붙는다. 그 위로 막대가 **하나씩 떠오르기 시작한다**
  3.32~7.97 「일한 건 2%, 나머지는 여가랑 배고픔이랑 간식이었고요」
     넷이 다 선다 — **일 막대만 바닥에 붙어 있고** 여가·배고픔·간식이 길다.
     막대가 뜨는 순서가 자막이 부르는 순서와 같다
  8.11~11.31 「한 줄만 꺼내도 이 사람 일생이 다 나와요」
     카메라가 조금 물러난다. 안 움직이는 몸 위에 막대 넷이 서 있다 —
     **그 넷이 이 사람의 일생이다**

🔑 크기 판단: 막대 넷의 **길이 차**가 이 줄의 전부라 가깝게 잡는다. 넓히면 「2%」가
   그냥 짧은 막대가 되고, 짧은 막대는 아무 말도 안 한다.
🔴 몸을 다시 움직이지 않는다. 이 샷에서 움직이는 것은 **막대와 카메라뿐**이고,
   그게 「화면에는 없었는데 값으로는 다 있었다」의 그림이다.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import instrument, motions, stage, village

SHOT, SID = 'body3', 'S3'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 9
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

DEAD = C.IDLERS[0]
v, arms, fire = C.build()
HOME = [tuple(a.location) for a in arms]
G = C.bars(C.LOG, z=0.30)
RIGHT = C.cam_right(C.CLOSE[0], C.CLOSE[1])
cam = stage.light_camera()
C.lens(cam, C.CLOSE_LENS)
stage.key_from_view(*C.CLOSE)

# 🔑 막대가 뜨는 순서 = 자막이 부르는 순서(일 → 여가 → 배고픔 → 간식)
UP0 = C.line_at(SID, 0) + 1.35
UP = [UP0, C.line_at(SID, 1) + 0.45, C.line_at(SID, 1) + 1.35, C.line_at(SID, 1) + 2.25]
BACK_AT = C.line_at(SID, 2) - 0.30
print('[body3-5] %d비트 · %.2f초 · 막대 %s'
      % (BEATS, DUR, ' '.join('%.2f' % u for u in UP)))


def draw(fi):
    t = fi / C.FPS
    C.all_work(arms, t, HOME, skip=C.IDLERS)
    for i in C.IDLERS:
        arms[i].location = HOME[i]
        arms[i].rotation_euler = (0, 0, C.home_face(i))
        stage.pose(arms[i], motions.collapse(motions.COLLAPSE_DUR))   # 이미 무너진 채

    # 🔴 0.55 에 두니 막대가 **몸에서 떠서** 뒤에 선 밭 사람 것처럼 보였다.
    #    누운 몸 바로 위(0.30)에 놓아야 「이 사람의 기록」으로 읽힌다.
    C.bars_at(G, HOME[DEAD][0], HOME[DEAD][1], RIGHT, z=0.30)
    for n, (g, val) in enumerate(G):
        k = C.ease((t - UP[n]) / 0.45)
        instrument.gauge_emph(g, 1.0)
        instrument.gauge_set(g, val * k)
        for key in ('track', 'fill'):
            g[key].hide_render = k <= 0.02

    u = C.ease((t - BACK_AT) / max(DUR - BACK_AT, 0.01))
    (sx, sy, sz), (ax, ay, az) = C.CLOSE
    d = C.ease(t / DUR)
    stage.aim(cam, (sx + 0.25 * d + 0.55 * u, sy - 0.15 * d - 0.70 * u, sz + 0.10 * d + 0.35 * u),
              (ax, ay, az + 0.06 * u))
    village.flicker(fire, t)


stage.bake(OUT, NF, C.FPS, draw, 'body3-5')
