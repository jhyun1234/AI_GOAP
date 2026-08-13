"""ep15s-4 본문4 — 7 비트 · 8.50 초. **처음으로 멈춘다. 그리고 집으로 간다.**

자막 두 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~3.85 「주인을 정하는 자리가 누가 바쁜지만 보고 있었어요」
     목수가 불 하나를 다 세우고 — 이번에는 **돌아서지 않는다.** 그 자리에 멈춰 선다.
     옆 막대는 아직 바닥이다
  3.98~7.97 「무엇을 짓는지를 보게 하니까, 목수가 집으로 갑니다」
     막대가 **한 번에 끝까지 찬다.** 그가 몸을 돌려 빈 집터로 **뛰어가고**,
     망치질 한 번에 **집이 땅에서 자라 오른다.** 기다리던 주민이 그쪽으로 돌아선다

🔑 **고친 것은 셈 한 줄인데 화면에서는 「멈춤」이다.** 이 편에서 유일하게 막대가 올라가는
   순간이고, 올라가자마자 되풀이가 끝난다 — 그 인과가 이 샷의 전부다.
🔑 크기 판단: 앞줄은 그가 멈추는 것과 막대를 봐야 해서 중간, 뒷줄은 그가 **건너가는 것**과
   집이 서는 것을 봐야 해서 넓다. 컷 없이 물러난다.
🔴 뛰게 한 것은 재서다 — 걸으면 4.1m 를 5.7초에 가서 집이 설 자리가 안 남는다.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import instrument, motions, stage, village

SHOT, SID = 'body4', 'S4'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 7
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT
LENS = 34

N = 9
v, arms, FIRES, SPOTS = C.build(N)
HOME = [tuple(a.location) for a in arms]
carp, asker = arms[C.CARP], arms[C.ASKER]
HAMMER = stage.hold(carp, village.hammer(), 'hand.R', (0.0, 0.02, 0.0))
G = C.own_gauge()
house = village.house(C.PLOT[0], C.PLOT[1], C.PLOT_ROT)
RING = C.plot_ring()
LINES = stage.shot_lines(C.EP, SID)
cam = stage.light_camera()
C.lens(cam, LENS)

BASE = 7
STAND = (SPOTS[6][0] + 0.62, SPOTS[6][1] - 0.30)
FIX_AT = C.line_at(SID, 1)                 # 「무엇을 짓는지를 보게 하니까」 — 막대가 찬다
GO_AT = FIX_AT + 0.18
# 🔴 뛰는 시간은 **거리에서 역산한다.** 손으로 적으면 집이 샷 밖에서 선다.
_dx, _dy = C.PLOT[0] - STAND[0], C.PLOT[1] - STAND[1]
_d = math.hypot(_dx, _dy)
STOP_GAP = 1.25
PLOT_STAND = (C.PLOT[0] - _dx / _d * STOP_GAP, C.PLOT[1] - _dy / _d * STOP_GAP)
RUN_SPAN = (_d - STOP_GAP) / motions.RUN_SPEED
BUILD_AT = GO_AT + RUN_SPAN
assert BUILD_AT + 0.9 < DUR, '집이 샷 밖에서 선다: %.2f' % BUILD_AT
CAM_A = ((-1.85, -6.30, 2.05), (-4.60, -2.30, 0.88))
CAM_B = ((1.90, -7.30, 2.95), (-2.30, -0.90, 0.95))
stage.key_from_view(*CAM_B)
print('[body4-4] %d비트 · %.2f초 · 고침 %.2f · 뛰기 %.2f(%.2f초) · 집 %.2f'
      % (BEATS, DUR, FIX_AT, GO_AT, RUN_SPAN, BUILD_AT))


def draw(fi):
    t = fi / C.FPS
    C.others(arms, t, HOME, watch=C.PLOT, watch_at=BUILD_AT - 0.3)
    C.fire_all(FIRES, BASE, t)             # 🔴 이 샷에서는 **하나도 안 는다**

    # ── 목수 — 멈춰 있다 → 뛴다 → 집을 짓는다 ──
    if t < GO_AT:
        carp.location = (STAND[0], STAND[1], 0)
        C.face(carp, SPOTS[6], STAND)
        stage.pose(carp, motions.stop(t))
        p = STAND
    else:
        p, wt = C.walk_to(carp, STAND, PLOT_STAND, t, GO_AT, RUN_SPAN, run=True)
        if t < BUILD_AT:
            C.walk_pose(carp, wt, run=True)
        else:
            C.face(carp, C.PLOT, PLOT_STAND)
            stage.pose(carp, motions.blend(motions.run(wt), motions.hammer(t - BUILD_AT),
                                           C.ease((t - BUILD_AT) / 0.26)))

    # ── 기다리던 주민 — 집이 서는 쪽으로 돌아선다 ──
    asker.location = HOME[C.ASKER]
    a0 = C.home_face(C.ASKER)
    a1 = math.atan2(C.PLOT[1] - HOME[C.ASKER][1], C.PLOT[0] - HOME[C.ASKER][0]) + math.pi
    C.turn(asker, a0, a1, C.ease((t - BUILD_AT + 0.4) / 0.6))
    stage.pose(asker, motions.stop(t))

    # ── 「내 불 있음」 — 이 편에서 **처음이자 마지막으로** 찬다 ──
    instrument.gauge_emph(G, C.emphasis_pair(LINES, t, which=(1,)))
    C.gauge_at(G, carp, C.cam_right(CAM_A[0], CAM_A[1]), z=0.62, side=0.62)
    instrument.gauge_set(G, C.ease((t - FIX_AT) / 0.45))
    C.gauge_show(G, t < GO_AT + 0.5)

    grown = C.ease((t - BUILD_AT) / 1.35)
    village.house_grow(house, grown)
    # 🔑 집이 다 서면 자리 표시를 끈다 — 그때부터는 집이 곧 그 자리다
    instrument.ring_at(RING, C.PLOT[0], C.PLOT[1])
    instrument.ring_show(RING, grown < 0.92)

    u = C.ease((t - FIX_AT + 0.6) / max(DUR - FIX_AT, 0.01))
    loc = tuple(a + (b - a) * u for a, b in zip(CAM_A[0], CAM_B[0]))
    at = tuple(a + (b - a) * u for a, b in zip(CAM_A[1], CAM_B[1]))
    stage.aim(cam, loc, at)
    C.flicker_home(t)


stage.bake(OUT, NF, C.FPS, draw, 'body4-4')
