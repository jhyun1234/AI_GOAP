"""ep15s-5 본문2 — 4 비트 · 5.08 초. **둘이 그냥 죽는다. 아무도 안 돌아본다.**

자막 두 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~1.89 「며칠 뒤 떠올려 봤는데,」
     카메라가 마을에서 **노는 둘 쪽으로** 옮겨 간다. 넷은 계속 일하고 있다
  2.02~4.55 「게으름뱅이 둘은 그냥 죽었더라고요」
     둘이 차례로 배를 움켜쥐고 **무너진다.** 예비 동작도 없고 소리도 없다.
     나머지 넷은 **하던 일을 그대로 한다 — 아무도 돌아보지 않는다**

🔑 **이 샷은 규칙을 일부러 뒤집는다.** ep15s-2·3·4 에서는 「사건이 나면 남이 반응한다」가
   규약이었다. 여기서는 **반응이 없는 것**이 사건이다 — 「별 이야기가 없었다」는 아무도
   그것을 이야기로 안 봤다는 뜻이고, 그건 화면에서 **안 돌아보는 등**으로만 보인다.
🔴 그래서 `startle` 도 `watch` 도 안 쓴다. 쓰면 그 순간 이 편의 뜻이 뒤집힌다.
🔴 죽는 시각은 **무너짐이 자막 안에서 끝나도록** 역산한다.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import motions, stage, village

SHOT, SID = 'body2', 'S2'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 4
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

v, arms, fire = C.build()
HOME = [tuple(a.location) for a in arms]
cam = stage.light_camera()
C.lens(cam, C.MID_LENS)
stage.key_from_view(*C.MID)

FALL = motions.COLLAPSE_DUR / C.COLLAPSE_RATE
DIE0 = DUR - FALL - 0.10                    # 뒤에 쓰러지는 사람이 샷 안에서 다 무너지게
DIE = [DIE0 - 0.42, DIE0]                   # 🔑 **동시에 안 쓰러진다** — 같이 가면 연출이 된다
assert DIE[0] > C.line_at(SID, 1) - 0.4, '「그냥 죽었더라고요」 전에 쓰러진다: %.2f' % DIE[0]
print('[body2-5] %d비트 · %.2f초 · 무너짐 %.2f / %.2f' % (BEATS, DUR, DIE[0], DIE[1]))


def draw(fi):
    t = fi / C.FPS
    C.all_work(arms, t, HOME, skip=C.IDLERS)          # 🔴 넷은 끝까지 제 일만 한다
    for n, i in enumerate(C.IDLERS):
        C.die(arms[i], i, HOME[i], t, DIE[n])
    u = C.ease(t / DUR)
    (sx, sy, sz), (ax, ay, az) = C.MID
    stage.aim(cam, (sx - 0.75 * u, sy + 0.55 * u, sz - 0.35 * u),
              (ax + 0.15 * u, ay - 0.35 * u, az - 0.20 * u))
    village.flicker(fire, t)


stage.bake(OUT, NF, C.FPS, draw, 'body2-5')
