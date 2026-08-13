"""ep15s-5 본문1 — 5 비트 · 5.93 초. **여섯이 다 다르게 산다.**

자막 두 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~2.65 「성격 여섯을 둘씩 짝지어 보면,」
     넓게. 여섯 위로 막대가 **왼쪽부터 하나씩** 떠오른다
  2.78~5.40 「열다섯 쌍이 전부 다르게 살았어요」
     여섯 개가 다 서 있다 — **색도 길이도 사람마다 다르다.** 같은 마을인데 사는 게 다 다르다

🔑 크기 판단: 여섯이 **한 프레임에** 있어야 「전부 다르다」가 성립한다. 하나씩 보여 주면
   그건 여섯 번의 「이 사람은 이렇다」이지 「전부 다르다」가 아니다.
🔴 열다섯이라는 수는 화면에 안 올린다 — 쌍은 그릴 수 없고, 그릴 필요도 없다.
   화면이 지는 것은 **다름** 하나이고 수는 자막이 진다.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import instrument, stage, village

SHOT, SID = 'body1', 'S1'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 5
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

v, arms, fire = C.build()
HOME = [tuple(a.location) for a in arms]
G = [C.bars([C.MAIN[i]])[0] for i in range(6)]
RIGHT = C.cam_right(C.EYE[0], C.EYE[1])
# 🔑 왼쪽(서쪽) 사람부터 차례로 뜬다 — 훑는 눈길과 같은 순서라야 「하나씩」이 읽힌다
ORDER = sorted(range(6), key=lambda i: C.CAST[i][1][0])
UP = [0.35 + 0.34 * n for n in range(6)]
cam = stage.light_camera()
C.lens(cam, C.WIDE_LENS)
stage.key_from_view(*C.EYE)
print('[body1-5] %d비트 · %.2f초 · 막대 여섯 %s'
      % (BEATS, DUR, ' '.join('%.2f' % u for u in UP)))


def draw(fi):
    t = fi / C.FPS
    C.all_work(arms, t, HOME)
    for n, i in enumerate(ORDER):
        g, val = G[i]
        k = C.ease((t - UP[n]) / 0.45)
        instrument.gauge_emph(g, 1.0)
        instrument.gauge_at(g, HOME[i][0] + RIGHT[0] * 0.26,
                            HOME[i][1] + RIGHT[1] * 0.26, 0.98)
        instrument.gauge_set(g, val * k)
        for key in ('track', 'fill'):
            g[key].hide_render = k <= 0.02
    u = C.ease(t / DUR)
    (sx, sy, sz), (ax, ay, az) = C.EYE
    stage.aim(cam, (sx - 0.85 * u, sy + 0.30 * u, sz - 0.20 * u), (ax, ay, az))
    village.flicker(fire, t)


stage.bake(OUT, NF, C.FPS, draw, 'body1-5')
