"""ep15s-3 본문3 — 5 비트 · 3.29 초. **집을 못 얻은 주민이 굶어 쓰러진다.**

자막 한 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~2.74 「집을 못 얻은 주민은 굶어 죽었어요」
     클로즈업. 머리 위 **붉은 막대가 줄어든다.** 주민이 배를 움켜쥐고 휘청이다 **무너진다**

🔴 앞 판은 두 가지가 다 틀렸다(2026-08-13 사용자 판정).
   ① 막대가 **거리**(시안)였고 **끝까지 차 있었다** — 굶는 이야기에 배고픔 수치가 없었다.
   ② 쓰러지는 대신 `rest`(앉아 쉰다)로 넘어갔다 — 앉은 사람은 죽은 사람이 아니다.
🔴 그래서 이 샷의 막대만 **뜻층 색(`red` = 이미 벌어진 실패·죽음)**을 쓴다. 수치가 곧
   사건이라 계기 시안으로 그리면 「무언가를 재고 있다」로만 읽힌다.
🔴 뜻층 예산(한 프레임 두 색) 때문에 **이 샷에서는 모닥불을 끈다.** 붉은 막대가 이 샷의
   유일한 유채색이고, 그래야 눈이 그리로 간다.
🔴 문턱 표시를 안 단다 — 여기서 재는 것은 거리가 아니라 **남은 배고픔**이고, 0 이
   문턱이라 따로 그을 선이 없다.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import instrument, motions, stage, village

SHOT, SID = 'body3', 'S3'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 5
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

v, arms, fire, _out = C.build()
HOME = [tuple(a.location) for a in arms]
homeless = arms[C.HOMELESS]
arms[C.CARPENTER].hide_render = True              # 목수는 저 밖에 있다(본문2 가 보여 줬다)
stage.gate(fire, False)                           # 🔴 뜻층 예산 — 이 샷의 유채색은 붉은 막대뿐
G = C.dist_gauge(color='red', mark=False)
RIGHT = C.cam_right(C.CLOSE[0], C.CLOSE[1])
cam = stage.light_camera()
C.lens(cam, C.CLOSE_LENS)
stage.key_from_view(*C.CLOSE)

# 🔴 무너짐이 **자막 안에서 끝나야** 한다. 시각을 손으로 적지 말고 동작 길이에서 역산한다.
FALL_AT = max(0.35, C.line_at(SID, 0) + 0.55)
assert FALL_AT + motions.COLLAPSE_DUR <= DUR + 0.25, \
    '쓰러지다 마는다: %.2f + %.2f > %.2f' % (FALL_AT, motions.COLLAPSE_DUR, DUR)
DRAIN_SPAN = FALL_AT + motions.COLLAPSE_DUR * 0.62   # 무너지는 도중에 0 이 된다
print('[body3-3] %d비트 · %.2f초 · 쓰러짐 %.2f~%.2f'
      % (BEATS, DUR, FALL_AT, FALL_AT + motions.COLLAPSE_DUR))


def draw(fi):
    t = fi / C.FPS
    C.others(arms, t, HOME, watch=HOME[C.HOMELESS], watch_at=FALL_AT + 1.1)

    # 배고픔이 **줄어든다.** 🔑 값이 먼저 바닥나고 그 뒤에 몸이 무너진다 — 순서가 인과다
    # 🔴 어느 쪽을 보게 두느냐가 **카메라에서 결정된다.** 목수가 간 북쪽을 보게 두면
    #    남쪽 카메라에는 뒤통수만 나오고, 그러면 쓰러지는 것이 「아프다」로 안 읽힌다.
    #    카메라 축에서 41° 어긋난 쪽(덤불)을 보게 해 **4분의 3 앞모습**으로 잡는다.
    homeless.location = HOME[C.HOMELESS]
    C.face(homeless, (1.1, -2.9), HOME[C.HOMELESS])
    stage.pose(homeless, motions.stop(t) if t < FALL_AT else
               motions.blend(motions.stop(t), motions.collapse(t - FALL_AT),
                             C.ease((t - FALL_AT) / 0.28)))

    instrument.gauge_set(G, 1.0 - C.ease(t / DRAIN_SPAN))
    # 🔴 클로즈업에서는 머리 높이(1.0)에 두면 막대 꼭대기가 화면 위로 잘린다. 어깨 옆이다.
    instrument.gauge_at(G, HOME[C.HOMELESS][0] + RIGHT[0] * 0.42,
                        HOME[C.HOMELESS][1] + RIGHT[1] * 0.42, 0.72)
    C.dist_show(G, True)

    u = t / DUR
    (sx, sy, sz), (ax, ay, az) = C.CLOSE
    stage.aim(cam, (sx + 0.35 * u, sy - 0.22 * u, sz - 0.18 * u),
              (ax, ay, az - 0.24 * u))            # 🔑 쓰러지는 사람을 따라 시선이 내려간다


stage.bake(OUT, NF, C.FPS, draw, 'body3-3')
