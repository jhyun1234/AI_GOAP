"""ep15s-5 훅 — 4 비트 · 4.90 초. **다 잘 돌아가는데, 아무 일도 안 일어난다.**

자막 두 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~2.13 「숫자로는 다 성공이었어요」
     넓게. 여섯이 제각기 다른 일을 한다. 밭·우물·나무·덤불, 그리고 노는 둘.
     하나도 안 멈추고 하나도 안 어긋난다
  2.26~4.40 「그런데 남는 이야기가 없었죠」
     카메라가 마을을 천천히 훑는다. **끝까지 아무 사건도 없다** — 그게 이 줄이다

🔑 크기 판단: 「볼 게 없다」를 말하려면 **볼 것이 다 보이는 크기**여야 한다. 좁히면
   「이 사람 이야기」가 생겨 버리고, 그 순간 이 줄이 거짓말이 된다.
🔴 여기에는 계기를 안 켠다. 수는 자막이 말하고, 화면은 **아무 일도 안 일어남**만 진다.
🔴 이 파이프라인의 규칙(사건이 나면 남이 반응한다)을 **일부러 안 쓴 첫 샷**이다 —
   반응할 사건 자체가 없다.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import stage, village

SHOT, SID = 'hook', 'SH'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 4
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

v, arms, fire = C.build()
HOME = [tuple(a.location) for a in arms]
cam = stage.light_camera()
C.lens(cam, C.WIDE_LENS)
stage.key_from_view(*C.EYE)
print('[hook-5] %d비트 · %.2f초' % (BEATS, DUR))


def draw(fi):
    t = fi / C.FPS
    C.all_work(arms, t, HOME)
    u = C.ease(t / DUR)
    (sx, sy, sz), (ax, ay, az) = C.EYE
    stage.aim(cam, (sx - 1.45 * u, sy + 0.55 * u, sz - 0.40 * u),
              (ax - 0.55 * u, ay - 0.35 * u, az))
    village.flicker(fire, t)


stage.bake(OUT, NF, C.FPS, draw, 'hook-5')
