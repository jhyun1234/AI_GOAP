import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW, FAIL_GLOW
} from '../../../engine/lib.js';

/* sentalone — 하나만 빈 곳으로 곧장 가더니, 거기서 쓰러진다.

   원문 근거 (`devlog/sessions/2026-08-11.md` 12:06 커밋 본문 ②):
   > "F(맞서라) 명령을 내리자 주민이 미발견 고블린의 위치를 정확히 찾아가 혼자 죽었다.
   >  '주민이 맵을 밝힌다' 위반. 이 구멍은 M21부터 있었다 -- TryGetNearestFightable 에
   >  거리·발견 제한이 없는데, 위협이 습격 때만 마을 곁에 있어 안 드러났고 W5 가 그
   >  전제를 깨자 즉시 보였다."

   화면 라벨 「맞서라 명령」은 원문이 **괄호로 스스로 푼 말**(`F(맞서라) 명령`)에서 키
   이름만 뗀 것이고, 「아무도 못 본 자리」는 원문 「미발견」의 풀이인데 그 풀이도 원문
   안에 있다("주민이 맵을 밝힌다"). 🔴 `TryGetNearestFightable`·`WithinDanger` 같은
   코드 식별자는 화면에도 자막에도 없다(`ADR-V-10`).

   🔴 **명령이 그은 화살이 먼저, 표식이 나중이다.** 무리는 처음부터 거기 있지만
   **점선**(아직 아무도 못 봄)이라, 표식이 다 가야 또렷해진다 — 「없는 곳으로 간 것」이
   아니라 **「아무도 못 본 곳을 정확히 찍었다」**가 이 샷이 지는 뜻이다.

   색 규약(이 회차 전체): 흰색 = 판 위에 실제로 있는 것 / 강조색 = 이번 사건에서 새로
   켜지거나 **새로 그어진** 것. 그래서 명령이 그은 화살만 강조색이다.

   🔴 겹침 감사(캔버스 352×307 · 선 반폭 1.5 · 획 4 · 글로우 blur 8 포함):
     축 A: 강조 화살 y 206 · 획 4 → 204~208 · 글로우 **196~216**
       ↔ 걷는 표식 바닥 **148.5** = 47.5px
       ↔ 흰 라벨 「아무도 못 본 자리」 글립 바닥 약 **182** = **14px**
       ↔ 흰 라벨 「맞서라 명령」 글립 꼭대기 약 **228.5** = **12.5px**.
       가로: 화살은 `lineCap round` 라 양끝이 2px 더 나간다 → 글로우 왼쪽 **120**
       ↔ 마을 네모 바깥 오른쪽 **101.5** = **18.5px**. 글로우 오른쪽 **316** ↔ 캔버스 끝 352.
     축 B(서로 다른 것을 가리키는 흰 글자끼리): 두 라벨의 **baseline 이 다르다**
       (178 · 238) — 같은 줄에 안 놓아 축 B 가 애초에 안 생긴다. 세로 간격은
       라벨1 글립 바닥 182 ↔ 라벨2 글립 꼭대기 228.5 = 46.5px.
     흰↔흰(서로 다른 물건): 걷는 표식 오른쪽 끝 **273.5** ↔ 무리 왼쪽 **292** = 18.5px.
       🔴 쓰러질 때 1.35rad 돌아 바깥 반지름이 9.85 로 커지므로 그때가 가장 좁다 —
       오른쪽 끝 **279.5** ↔ 292 = **12.5px**. 세로로도 쓰러진 표식 바닥 **157.4**
       ↔ 라벨1 글립 꼭대기 **168.5** = 11.1px.

   세로 예산: 최저 = 「맞서라 명령」 글립 바닥 약 **242**. 65px 남는다.
   가로: 최우 = 「아무도 못 본 자리」 라벨 오른쪽 약 **332**(폭 상한 108 로 묶었다).

   ⏱ 화살은 `since(lineCue)`, 걷기·또렷해짐·쓰러짐은 `since(walkCue)` 위에 있다.
     걷기 **0.80~2.30초** · 또렷해짐 **2.05~2.45초** · 쓰러짐 **2.60~3.10초**.
     `thud delayMs 2900` 이 그 쓰러짐 안이다(`since` 와 원점이 같아 자막 길이가 바뀌어도 안 움직인다).
   🔴 **1차본은 걷기 0.20~1.30 · 쓰러짐 1.45~1.95 였고 검수가 「말보다 약 1.0초 이르다」고 쟀다.**
     `build/audio/05-0708a2r.wav` 실측(5ms hop · 20ms 창 · 저역 300Hz 분리)으로
     「죽었죠」의 개시가 **2.92~3.07초**다(말 시작 0.466 · 말 끝 3.403 · 후행 무음 0.508 ·
     3.232 파찰 개시 → 마지막 유성 3.292~3.403 = 「죠」). 줄 기준 판정으로는 통과였지만
     낱말 기준(한도 0.67초)을 넘었고, **1.95초 뒤에 2초 가까운 빈 꼬리**까지 남았다.
     → 셋을 통째로 뒤로 밀어 「걸어감(말 앞부분) → 도착해 무리가 드러남 → 쓰러짐(「죽었죠」)」이
     말과 같은 순서로 흐르게 했다. 줄 창(dur 3.911 + pause 0.180 = **4.091초**) 안에 다 들어간다.

   계속 도는 것(30fps 기준):
     · 마을 네모 둘레 336px 가 숨을 쉰다(진폭 1.3 · ω 5.6) → 0.24px/프레임
     · 남은 표식 둘이 반지름 18 궤도를 `t × 0.65` rad/s 로 돈다 → 0.39px/프레임
     · 못 본 무리의 점선이 `-t × 30` 으로 흐른다(둘레 3×약 60px)
     · 걷는 표식이 158px 를 1.50초에 → **3.5px/프레임** */

const TOWN = { x: 16, y: 96, w: 84, h: 84 };
const WALK = { x0: 110, x1: 268, y: 138 };
const FOE = { x: 308, y: 138 };
const OFF3 = [[-11, -6], [2, -9], [11, 3]];
const ARROW = { y: 206, x0: 130, x1: 306 };

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const g0 = ease(cue(spec.lineCue ?? 0, 0.15, 0.30));
    if (g0 <= 0.02) { ctx.textAlign = 'left'; return; }

    const label = (txt, cx, base, maxW) => {
      if (!txt) return;
      let fs = 13; ctx.font = disp(800, fs);
      while (fs > 9 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(800, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.save();
      ctx.globalAlpha = g0;
      ctx.textAlign = 'center'; ctx.fillStyle = tone('ink');
      ctx.fillText(txt, clamp(cx, 14 + tw / 2, w - 14 - tw / 2), base);
      ctx.restore();
    };

    /* ── 마을 네모 + 남은 사람 둘 (흰색) ─────────────── */
    const bre = 1.3 * (1 + Math.sin(t * 5.6));
    ctx.save();
    ctx.globalAlpha = g0;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    roundRect(ctx, TOWN.x + bre, TOWN.y + bre, TOWN.w - bre * 2, TOWN.h - bre * 2, 9);
    ctx.stroke();
    ctx.restore();

    const tcx = TOWN.x + TOWN.w / 2, tcy = TOWN.y + TOWN.h / 2;
    for (let i = 0; i < 2; i++) {
      const a = t * 0.65 + i * Math.PI;
      ctx.save();
      ctx.globalAlpha = g0;
      ctx.translate(tcx + Math.cos(a) * 18, tcy + Math.sin(a) * 18);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, -4, -9, 8, 18, 4);
      ctx.stroke();
      ctx.restore();
    }

    /* ── 명령이 그은 화살 (강조색) ───────────────────
       걷는 길보다 68px 아래에 따로 눕혀 뒀다 — 흰 표식이 강조색 위를 지나가지 않는다. */
    const ln = ease(clamp((since(spec.lineCue ?? 0) - 0.35) / 0.65));
    if (ln > 0.01) {
      const ax = lerp(ARROW.x0, ARROW.x1, ln);
      ctx.save();
      ctx.globalAlpha = g0;
      setShadow(ctx, FAIL_GLOW, 8);
      ctx.strokeStyle = tone('fail'); ctx.lineWidth = 4; ctx.lineCap = 'round';
      ctx.beginPath(); ctx.moveTo(ARROW.x0, ARROW.y); ctx.lineTo(ax, ARROW.y); ctx.stroke();
      if (ln > 0.9) {
        const hk = ease(clamp((ln - 0.9) / 0.1));
        ctx.beginPath();
        ctx.moveTo(ax - 13 * hk, ARROW.y - 9 * hk);
        ctx.lineTo(ax, ARROW.y);
        ctx.lineTo(ax - 13 * hk, ARROW.y + 9 * hk);
        ctx.stroke();
      }
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 아직 아무도 못 본 무리 (흰 점선 → 흰 실선) ──
       같은 물건의 상태 변화라 색이 안 갈린다 — 점선과 실선을 교차로 넘긴다. */
    const wk = ease(clamp((since(spec.walkCue ?? 1) - 0.80) / 1.50));
    const rv = ease(clamp((since(spec.walkCue ?? 1) - 2.05) / 0.40));
    OFF3.forEach(([ox, oy], i) => {
      const br = 0.8 * (1 + Math.sin(t * (6.8 + i * 0.5) + i));
      const cx = FOE.x + ox, cy = FOE.y + oy + br;
      if (rv < 0.99) {                                  // 점선 = 아직 못 봄
        ctx.save();
        ctx.globalAlpha = g0 * 0.55 * (1 - rv);
        ctx.translate(cx, cy);
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
        ctx.setLineDash([5, 5]); ctx.lineDashOffset = -t * 30;
        roundRect(ctx, -3.5, -8, 7, 16, 3.5);
        ctx.stroke();
        ctx.setLineDash([]);
        ctx.restore();
      }
      if (rv > 0.01) {                                  // 실선 = 이제 봤다
        ctx.save();
        ctx.globalAlpha = g0 * rv;
        ctx.translate(cx, cy);
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
        roundRect(ctx, -3.5, -8, 7, 16, 3.5);
        ctx.stroke();
        ctx.restore();
      }
    });

    /* ── 혼자 나간 표식 : 걸어가고 → 쓰러진다 (흰색) ── */
    const fall = ease(clamp((since(spec.walkCue ?? 1) - 2.60) / 0.50));
    ctx.save();
    ctx.globalAlpha = g0 * (1 - 0.5 * fall);
    /* 🔴 상시 숨(0.7px · 7.0rad/s)을 준다 — 걷기 시작(둘째 자막 0.80초) 전과 쓰러진 뒤에
       이 표식이 완전히 멈춰 있으면 그 자리가 이 샷의 정적 최대 위험 구간이 된다. */
    ctx.translate(lerp(WALK.x0, WALK.x1, wk),
                  WALK.y + 8 * fall + 0.7 * Math.sin(t * 7.0));
    /* 🔴 눕히는 것은 회전만으로 한다 — `scale(1, s)` 로 납작하게 만들면 획 두께가
       가로·세로로 갈려(비등방) 검정 배경에서 한쪽이 사라진다. 회전은 두께가 안 변한다. */
    ctx.rotate(1.35 * fall);
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    roundRect(ctx, -4, -9, 8, 18, 4);
    ctx.stroke();
    ctx.restore();

    /* ── 라벨 둘 (baseline 을 갈라 놓았다) ───────────── */
    label(spec.unseenLabel, 280, 178, 108);
    label(spec.cmdLabel, 150, 238, 96);

    ctx.textAlign = 'left';
  }
};
