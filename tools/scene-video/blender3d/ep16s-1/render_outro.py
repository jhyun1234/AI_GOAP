"""ep16s-1 아웃트로 — 4 비트 · 4.07 초. **주저앉은 사람 옆을 그냥 지나간다.**

  0.00~4.07 「다음 편에서는, 굶는다는 알림에 누구인지가 없었어요」
     한 사람이 배를 움켜쥐고 **주저앉는데**, 옆을 지나가는 사람은 **돌아보지 않는다**(0.40).
     누가 굶는지 화면 어디에도 표시가 없다

🔴 **격자를 걷어냈다**(2026-08-14). 첫 판은 이 예고를 공중 격자 칸 하나가 켜졌다 꺼지는
   것으로 그렸는데, 사용자 판정에서 계기가 **뜻이 안 읽히는 것**으로 지목됐다.
   다음 편의 사건(「누구인지가 없다」)은 **사람으로** 그리는 편이 정확하다 — 알림이
   가리키지 못한 그 사람이 화면에 있고, 아무도 그를 못 알아본다.
🔴 엔진이 이 샷 위에 아웃트로 카드를 얹는다 — 그림이 조용해야 카드가 읽힌다.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import motions
import stage

SHOT, SID = 'outro', 'SO'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 4
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

v, arms, graves = C.build(graves=False)
HOME = [tuple(a.location) for a in arms]
# 굶는 사람 = 무리 B 의 물뿌리개(4). 지나가는 사람 = 밭을 갈던 사람.
HUNGRY, PASSER = 4, C.LIVE_I
SINK_AT = 0.85
# 🔴 지나가는 사람은 **화면을 가로질러야** 「지나쳤다」가 된다. 밭에서 걸어오게 두면
#    카메라 밖에서 끝난다(접점 시트) — 굶는 사람 뒤 1.3m 를 스쳐 지나가게 잡았다.
PASS_FROM = (C.spot_of(HUNGRY)[0] + 1.85, C.spot_of(HUNGRY)[1] + 1.25)
PASS_TO = (C.spot_of(HUNGRY)[0] - 1.55, C.spot_of(HUNGRY)[1] - 0.55)
GO_AT, GO_SPAN = 0.30, 3.40

CAM_A = C.cam_at(HUNGRY, frac=0.40, lens_mm=C.LENS_MID, yaw=C.face_yaw(HUNGRY, 30),
                 elev=16, dz=0.62)
CAM_B = C.cam_at(HUNGRY, frac=0.44, lens_mm=C.LENS_MID, yaw=C.face_yaw(HUNGRY, 16),
                 elev=14, dz=0.58)
cam = stage.light_camera()
C.lens(cam, C.LENS_MID)
stage.key_from_view(*CAM_A)
C.report('outro-16-1', [(0, CAM_A[0], CAM_A[1]), (1, CAM_B[0], CAM_B[1])], C.LENS_MID)
print('[outro-16-1] %d비트 · %.2f초' % (BEATS, DUR))


def draw(fi):
    t = fi / C.FPS
    # 굶는 사람 — 일하다 배를 움켜쥐고 주저앉는다(무너짐의 8할까지만 쓴다)
    h = arms[HUNGRY]
    h.location = HOME[HUNGRY]
    h.rotation_euler = (0, 0, C.home_face(HUNGRY))
    if t < SINK_AT:
        C.work(h, C.CAST[HUNGRY][0], t)
    else:
        u = C.ease((t - SINK_AT) / 0.85)
        stage.pose(h, motions.blend(motions.MOTIONS[C.CAST[HUNGRY][0]](t),
                                    motions.collapse((t - SINK_AT) * 0.75), u * 0.82))
    # 지나가는 사람 — 그 옆을 지나면서 **한 번도 안 돌아본다**
    p = arms[PASSER]
    _pos, wt = C.walk_to(p, PASS_FROM, PASS_TO, t, GO_AT, GO_SPAN)
    if t < GO_AT:
        C.work(p, C.CAST[PASSER][0], t)
    else:
        C.walk_pose(p, wt)
    # 🔴 이 샷은 둘만 남긴다 — 카드가 얹히는 자리라 화면이 조용해야 한다
    for i in range(len(C.DEAD)):
        if i != HUNGRY:
            C.hide_person(arms[i], False)
    u = C.ease(t / DUR)
    stage.aim(cam, C.lerp3(CAM_A[0], CAM_B[0], u), C.lerp3(CAM_A[1], CAM_B[1], u))


stage.bake(OUT, NF, C.FPS, draw, 'outro-16-1')
