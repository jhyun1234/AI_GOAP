"""ep16s-1 본문3 — 12 비트 · 15.62 초. **크기로 두 번 걸렸고, 세 번째에 떼어 냈다.**

  0.00~2.68   「처음엔 도구가 반토막으로 섰어요」
     가깝게(0.45). 비석 옆 도구가 **절반 크기**로 서 있다
  2.68~5.47   「무덤을 반으로 줄여 그리고 있었거든요」
     무덤이 **제 크기로 커지고** 도구도 따라 커진다 — 원인이 도구가 아니라 무덤이었다
  5.47~8.35   「이번엔 너무 작아서 다가가야 보였고요」
     카메라가 0.30 → 0.52 로 **다가간다.** 다가가는 그 움직임이 이 줄의 사건이다
  8.35~11.90  「그래서 무덤 표시만 따로 키웠어요」
     비석 옆 도구만 커지고, **산 사람이 든 망치는 그대로**다 — 둘이 한 프레임에 있다
  11.90~15.62 「이제 무덤 앞에 서면 여기 누가 묻혔는지 알아요」
     그 사람이 무덤 앞에 서서 **끄덕인다**(0.42)

🔴 **머리 위 계기 표식을 걷어냈다**(2026-08-14). 첫 판은 「주민 이름표는 그대로」를
   시안 막대로 그렸는데 뜻이 안 읽혔다. 지금은 **산 사람이 든 망치**가 그 자리를 진다 —
   같은 도구가 하나는 커지고 하나는 안 커지는 것이 곧 「따로 떼어 냈다」다.
🔴 이 편에서 **카메라가 크기를 바꾸는 유일한 구간**이고, 그것이 사건이다(3D_대본_문법 §4).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import motions
import stage
import village

SHOT, SID = 'body3', 'S3'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 12
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

L = [C.line_at(SID, i) for i in range(5)]
v, arms, graves = C.build()
HOME = [tuple(a.location) for a in arms]
C.dead_hide(arms)

NEAR = 1                                    # 크게 잡는 무덤(무리 A 가운데)
STAND = (-1.05, -2.35)                      # 본문1 이 데려다 놓은 자리
live = arms[C.LIVE_I]
live.location = (STAND[0], STAND[1], 0)
FACE = 2.55                                 # 무덤 쪽을 본다(라디안)
live.rotation_euler = (0, 0, FACE)
# 🔑 산 사람은 **망치를 들고 있다** — 커지는 비석 도구와 나란히 놓여 크기 차이를 진다
HELD = stage.hold(live, village.hammer(), 'hand.R', (0.0, 0.02, 0.0))

STOOD = [C.tool(kind, tinted=True) for _job, _p, _s, kind in C.DEAD]
GROW_AT, GROW_SPAN = L[1] + 0.30, 0.85      # 무덤이 제 크기로
BIG_AT, BIG_SPAN = L[3] + 0.45, 0.80        # 비석 것만 1.25 배
NOD_AT = L[4] + 0.20

K0 = C.cam_at(NEAR, frac=0.45, lens_mm=C.LENS_CLOSE, yaw=-70, elev=16, dz=0.45)
K1 = C.cam_at(NEAR, frac=0.30, lens_mm=C.LENS_MID, yaw=-84, elev=19, dz=0.50)
K2 = C.cam_at(NEAR, frac=0.52, lens_mm=C.LENS_CLOSE, yaw=-92, elev=14, dz=0.48)
K3 = C.cam_at(((C.spot_of(NEAR)[0] + STAND[0]) / 2, (C.spot_of(NEAR)[1] + STAND[1]) / 2),
              frac=0.40, lens_mm=C.LENS_MID, yaw=-108, elev=15, dz=0.60)
K4 = C.cam_at(STAND, frac=0.42, lens_mm=C.LENS_MID, yaw=-126, elev=14, dz=0.64)
KEY = [(0.00, K0[0], K0[1]),
       (L[1] + 0.20, K1[0], K1[1]),
       (L[2] + 1.10, K2[0], K2[1]),
       (L[3] + 0.30, K3[0], K3[1]),
       (DUR, K4[0], K4[1])]

cam = stage.light_camera()
C.lens(cam, C.LENS_MID)
stage.key_from_view(*K1)
C.report('body3-16-1', KEY, C.LENS_MID)
print('[body3-16-1] %d비트 · %.2f초 · 줄 %s' % (BEATS, DUR, [round(x, 2) for x in L]))


def draw(fi):
    t = fi / C.FPS
    gs = 0.5 + 0.5 * C.ease((t - GROW_AT) / GROW_SPAN)           # 무덤 0.5 → 1.0
    ts = gs * (1.0 + 0.20 * C.ease((t - BIG_AT) / BIG_SPAN))     # 도구는 마지막에 더
    for i, g in enumerate(graves):
        C.grave_scale(g, gs)
        x, y = C.spot_of(i)
        # 🔴 1.5 배는 확대 구간에서 **비석보다 큰 보라 덩이**가 된다(접점 시트) — 1.2 로
        C.tool_stand(STOOD[i], x, y, s=1.2 * ts, face=C.home_face(i))
        C.tool_show(STOOD[i], True)
    if t < NOD_AT:
        stage.pose(live, motions.stop(t))
    else:
        stage.pose(live, motions.blend(motions.stop(t), motions.nod(t - NOD_AT),
                                       C.ease((t - NOD_AT) / 0.30)))
    C.fly(cam, KEY, t)


stage.bake(OUT, NF, C.FPS, draw, 'body3-16-1')
