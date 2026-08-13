"""ep16s-1 본문3 — 12 비트 · 15.62 초. **크기로 두 번 걸렸고, 세 번째에 떼어 냈다.**

자막 다섯 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~2.68 「처음엔 도구가 반토막으로 섰어요」
     가깝게. 비석 옆 도구가 **절반 크기**로 서 있다
  2.68~5.47 「무덤을 반으로 줄여 그리고 있었거든요」
     무덤이 **제 크기로 커지고** 도구도 따라 커진다 — 원인이 도구가 아니라 무덤이었다
  5.47~8.35 「이번엔 너무 작아서 다가가야 보였고요」
     카메라가 **다가가야** 도구가 읽힌다. 다가가는 그 움직임이 이 줄의 사건이다
  8.35~11.90 「그래서 비석 것만 따로 키울 수 있게 떼어 냈어요」
     비석 옆 도구만 커지고, **산 사람 머리 위 표식은 그대로**다(공유 설정값을 뗀 자리)
  11.90~15.62 「이제 무덤 앞에 서면 여기 누가 묻혔는지 알아요」
     살아남은 사람이 무덤 앞에 서서 **끄덕인다**(`nod`)

🔴 이 편에서 **카메라가 크기를 바꾸는 유일한 구간**이고, 그것이 곧 사건이다
   (`Docs/3D_대본_문법.md` §4 — 한 샷 안에서 크기가 바뀌면 카메라가 움직인다).
🔴 머리 위 표식은 **계기층**(시안)이다. 비석 옆 도구(보라)와 색이 갈려야 「따로 떼어
   냈다」가 화면에서도 갈린다.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import instrument
import motions
import stage

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
STAND = (-1.95, -2.75)                     # 본문1 이 데려다 놓은 자리에서 이어 간다
live = arms[C.LIVE_I]
live.location = (STAND[0], STAND[1], 0)
FACE = 3.0                                 # 무덤 셋 쪽을 본다(라디안 · 남서향)
live.rotation_euler = (0, 0, FACE)

STOOD = [C.tool(kind, violet=True) for _job, _p, _s, kind in C.DEAD]
# 🔴 표식은 **머리 위에 얹히는 작은 것**이다. 기본 높이(0.42)로 두면 안테나가 된다
MARK = instrument.gauge((0, 0, 0), h=0.22, color='instrument')
instrument.gauge_set(MARK, 0.62)

GROW_AT, GROW_SPAN = L[1] + 0.35, 0.90     # 무덤이 제 크기로
BIG_AT, BIG_SPAN = L[3] + 0.55, 0.85       # 비석 것만 1.25 배 — 1.6 은 비석보다 커졌다
NOD_AT = L[4] + 0.15

KEY = [(0.00, C.CLOSE[0], C.CLOSE[1]),
       (L[2] - 0.10, (0.20, -5.95, 1.35), (-1.15, -3.30, 0.52)),
       (L[3] - 0.05, (-0.15, -5.55, 1.15), (-1.15, -3.50, 0.45)),
       (L[4] - 0.10, (-0.15, -5.55, 1.15), (-1.15, -3.50, 0.45)),
       (DUR, C.MID[0], (STAND[0] - 0.10, STAND[1] + 0.30, 0.70))]

cam = stage.light_camera()
C.lens(cam, C.CLOSE_LENS)
stage.key_from_view(*C.CLOSE)
print('[body3-16-1] %d비트 · %.2f초 · 줄 %s' % (BEATS, DUR, [round(x, 2) for x in L]))


def draw(fi):
    t = fi / C.FPS
    gs = 0.5 + 0.5 * C.ease((t - GROW_AT) / GROW_SPAN)          # 무덤 크기(0.5 → 1.0)
    ts = gs * (1.0 + 0.25 * C.ease((t - BIG_AT) / BIG_SPAN))    # 도구는 마지막에 더 커진다
    for i, g in enumerate(graves):
        C.grave_scale(g, gs)
        x, y = C.DEAD[i][1]
        C.tool_stand(STOOD[i], x, y, ts, face=C.home_face(i))
        C.tool_show(STOOD[i], True)
    # 🔴 산 사람 머리 위 표식은 **한 번도 안 커진다** — 그게 「떼어 냈다」의 증거다
    instrument.gauge_at(MARK, STAND[0], STAND[1], 1.02)
    if t < NOD_AT:
        stage.pose(live, motions.stop(t))
    else:
        stage.pose(live, motions.blend(motions.stop(t), motions.nod(t - NOD_AT),
                                       C.ease((t - NOD_AT) / 0.30)))
    C.fly(cam, KEY, t)


stage.bake(OUT, NF, C.FPS, draw, 'body3-16-1')
