"""ep15s-2 본문2 — 8 비트 · 10.81 초. **되풀이. 그리고 물린다.**

  0.00~1.35  ① 눈높이로 컷. 겨울 마을, 빈 밭
  1.35~5.41  ②③④ 먹으려다 만다 · 또 · 또 (겨울이라 먹을 것이 없다)
  5.41~8.11  ⑤⑥ 붉은 덩이가 등 뒤로 다가온다 — 그는 여전히 되풀이한다
  8.11~9.46  ⑦ 닿는다. 몸이 한 번 눌린다(스쿼시)
  9.46~10.81 ⑧ 그 자리에 주저앉는다

🔑 **이 편의 심장이다.** 원문의 결함은 우선순위 역전이 아니라 되풀이다 —
   「배고픔 목표는 실패해도 쿨다운 없이 다시 고를 수 있다」. 자막이 「밥, 밥, 밥」이라고
   말할 때 화면이 정확히 그것을 한다.
🔴 스쿼시는 **타격에만** 쓴다(motions.SQUASH 주석). 여기서는 닿는 한 순간뿐이다.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import instrument
import motions
import stage
import village

SHOT, SID = 'body2', 'S2'
OUT = C.out_dir(SHOT)
DUR = C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 8
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

TRY_FROM, TRY_TO = 1.0 * BEAT, 5.6 * BEAT     # 되풀이 구간
TRY_PERIOD = 1.32                              # 한 번 「먹으려다 마는」 데 걸리는 시간
HIT_AT = 6.0 * BEAT                            # 덩이가 닿는 순간
HOME_H = C.CAST[C.HUNGRY][1]
BEHIND = C.behind(HOME_H, C.CLOSE[0], 0.85)    # 카메라에서 봤을 때 그의 등 뒤

v, arms, fire, flakes = C.build()
HOME = [tuple(a.location) for a in arms]
# 🔴 **막대만 쓴다**(2026-08-13 판정: 원판은 차이가 안 보인다). 배고픔(연노랑)·도망(자홍).
#    되풀이는 **배고픔 막대가 찼다가 툭 떨어지는 것**으로 보인다 — 실패하면 다시 고른다.
G = C.gauges()
RIGHT = C.cam_right(C.CLOSE[0], C.CLOSE[1])
LINES = stage.shot_lines(C.EP, SID)
FOCUS = {0: (0, 1), 1: 0, 2: 0}
d = C.danger()
cam = stage.light_camera()
stage.key_from_view(*C.CLOSE)

print('[body2-2] %d비트 · %.2f초 · 되풀이 %.1f회 · 타격 %.2f초'
      % (BEATS, DUR, (TRY_TO - TRY_FROM) / TRY_PERIOD, HIT_AT))


def draw(fi):
    t = fi / C.FPS

    for i, arm in enumerate(arms):
        if i == C.HUNGRY:
            continue
        C.work(arm, C.CAST[i][0], t)
        arm.location = HOME[i]

    hero = arms[C.HUNGRY]
    # ── ②③④ 먹으려다 만다 ── 🔑 시작했다 그만두는 것이 한 번이고, 그게 계속 반복된다
    u = (t - TRY_FROM) % TRY_PERIOD / TRY_PERIOD if TRY_FROM <= t < TRY_TO else 0.0
    k = math.sin(math.pi * u) ** 1.6 if TRY_FROM <= t < TRY_TO else 0.0
    spec = motions.blend(motions.look_around(t), motions.eat(t), k)

    # ── 막대: 배고픔이 찼다가 **툭 떨어지고 또 찬다**(되풀이). 도망은 그 아래 그대로 ──
    C.gauge_place(G, hero, RIGHT, z=0.95, emph=C.emphasis(LINES, t, FOCUS))
    C.gauge_show(G, t > 0.35 * BEAT)
    if TRY_FROM <= t < TRY_TO:
        u = (t - TRY_FROM) % TRY_PERIOD / TRY_PERIOD
        k = instrument.gauge_k(C.HUNGER) * C.ease(min(u / 0.72, 1.0)) if u < 0.86 else 0.06
    else:
        k = instrument.gauge_k(C.HUNGER)
    instrument.gauge_set(G[0], k)
    instrument.gauge_set(G[1], instrument.gauge_k(C.FLEE_BEFORE))

    # ── ⑦ 닿는다 — 몸이 한 번 눌린다 ──
    if t >= HIT_AT:
        w = C.ease((t - HIT_AT) / 0.30)
        spec = motions.blend(spec, motions.huddle(t), w)
        sq = 0.20 * math.exp(-((t - HIT_AT) / 0.22) ** 2)
        spec = dict(spec)
        spec[motions.SQUASH] = (sq, 0.0, 0.0)
        # ⑧ 주저앉는다 — 골반이 내려간다(회전만으로는 못 만드는 층)
        sit = C.ease((t - HIT_AT - 0.45) / 0.9)
        root = spec.get(motions.ROOT, (0.0, 0.0, 0.0))
        spec[motions.ROOT] = (root[0], root[1] - 0.13 * sit, root[2])
    stage.pose(hero, spec)
    hero.location = HOME[C.HUNGRY]

    # ── ⑤⑥ 덩이가 등 뒤로 ──
    C.danger_at(d, (t - 3.2 * BEAT) / (HIT_AT - 3.2 * BEAT), BEHIND, t)
    d.hide_render = t < 3.0 * BEAT

    # 카메라 — 눈높이에서 아주 조금씩 옆으로 흐른다(홀드 방지)
    (sx, sy, sz), at = C.CLOSE
    u2 = t / DUR
    stage.aim(cam, (sx - 0.55 * u2, sy - 0.22 * u2, sz + 0.10 * u2), at)

    village.flicker(fire, t)
    village.snowfall(flakes, t, wind=0.35)


stage.bake(OUT, NF, C.FPS, draw, 'body2-2')
