import {
  disp, ease, clamp, frac,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* twopanes — 훅. 안쪽은 가득 찼는데 화면 쪽은 끝까지 빈다.

   원문 근거:
   > "시스템의 정합성과 사용자에게 도달하는 것은 완전히 별개의 문제입니다.
   >  안쪽이 정교해질수록 만든 사람은 더 뿌듯해지고 …"
   > "화면에서는 그냥 어떤 주민이 걸어다니다 쓰러졌을 뿐이고 …"
   > "밀스톤을 닫으면서 마지막 관측을 돌렸습니다."   ← spec.title 「마지막 관측」의 근거

   🔴 이 회차의 색 규약을 여기서 세운다 — **강조색 = 실제로 만들어져 있는 것(안쪽) /
   흰색 = 화면에 보이는 것.** 보통 회차와 반대인데, 이 편의 사건이 「가득 찬 쪽이 바로
   안 보이는 쪽」이라 색을 뒤집는 것이 곧 논지다. 네 그림(SH·S1·S2·S3)이 이 규약을 지킨다.

   루프(2.0초): 왼쪽에 강조색 가로 막대가 아래에서부터 열두 칸 차오른다. 오른쪽 흰 점선
   그릇은 그동안 한 번도 안 찬다. 다 차면 왼쪽만 흐려지고 되풀이된다.
   라벨을 다 가려도 「한쪽은 꽉 차는데 한쪽은 계속 비어 있다」가 읽힌다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **가로 좌표로 갈랐다.**
     강조색 막대의 오른쪽 최대 x = 167(숨 4px 포함), 글로우(blur 최대 11)까지 178.
     흰 점선 그릇의 왼쪽 선은 `x = 186` 을 중심으로 두께 3px 이라 실제 왼쪽 끝이 **184.5** 다.
     **6.5px** 뜬다. (1차본 주석은 선 두께 반폭을 안 세서 「8px」이라고 적었다 — 검수 재계산값이 맞다.)
     라벨도 같다 — 「안쪽」(accent)은 중심 96, 「화면」(ink)은 중심 256.
     🔑 **왼쪽 칸에는 테두리를 안 그린다.** 흰 테두리를 두르면 강조색 채움과 같은 픽셀을
     지난다(게이트는 두 색의 혼합을 hue 150~152 로 읽어 어느 순서든 통과시키므로 게이트
     통과가 근거가 못 된다). 대신 **차오르는 막대 자체가 칸 모양을 만든다.**

   세로 예산(캔버스 307): 최저 = 라벨 글립 바닥 276. 31px 남는다.
   가로: 오른쪽 점선 바깥선 326. 캔버스 352, 여유 26px. 왼쪽 최소 x = 29.

   계속 도는 것(30fps 기준 프레임당 이동):
     · 차오름 루프 2.0초 — 막대 하나가 0.12초에 켜지고 전체가 0.36초에 꺼진다(면적 큼)
     · 오른쪽 점선 흐름 60px/s → **2.0px/프레임** (둘레 612px)
     · 막대 폭 숨 진폭 4px · 7.4rad/s → **0.99px/프레임** × 막대 열둘 */

const L_X0 = 26, L_X1 = 166, R_X0 = 186, R_X1 = 326;
const TOP = 68, BOT = 250;
const BARS = 12, BAR_H = 12, BAR_GAP = 3;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const ctext = (txt, x, y, weight, size, color, maxW, alpha) => {
      if (alpha <= 0.01) return;
      let fs = size; ctx.font = disp(weight, fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(weight, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.textAlign = 'center'; ctx.fillStyle = color;
      ctx.fillText(txt, clamp(x, 14 + tw / 2, w - 14 - tw / 2), y);
      ctx.restore();
    };

    /* 🔴 긴 점선을 setLineDash 로 그리면 30fps 3패스에서 알파가 갈린다(결정성 반려 전례).
       조각을 fillRect 로 직접 놓고 위상은 양수로 정규화한다. */
    const dashRun = (x0, y0, x1, y1, seg, gap, off, alpha) => {
      const P = seg + gap;
      const len = Math.hypot(x1 - x0, y1 - y0);
      const ux = (x1 - x0) / len, uy = (y1 - y0) / len;
      const ph = ((off % P) + P) % P;
      ctx.save();
      ctx.globalAlpha = alpha; ctx.fillStyle = tone('ink');
      for (let s = -P + ph; s < len; s += P) {
        const a = Math.max(0, s), b = Math.min(len, s + seg);
        if (b <= a) continue;
        /* 🔴 방향이 음수인 변(아래·왼쪽)에서 폭을 그대로 더하면 조각이 상자 **밖**으로
           나간다. 두 끝점을 다 구해 min/max 로 상자를 만든다. */
        const ax = x0 + ux * a, ay = y0 + uy * a;
        const bx = x0 + ux * b, by = y0 + uy * b;
        ctx.fillRect(
          Math.min(ax, bx) - (uy ? 1.5 : 0),
          Math.min(ay, by) - (ux ? 1.5 : 0),
          Math.abs(bx - ax) + (uy ? 3 : 0),
          Math.abs(by - ay) + (ux ? 3 : 0));
      }
      ctx.restore();
    };

    const st = ease(cue(spec.paneCue ?? 0, 0.15, 0.35));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    if (spec.title) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
      ctx.restore();
    }

    /* ── 왼쪽 : 안쪽에 실제로 쌓인 것 (강조색) ───────── */
    const u = frac(t / (spec.loopSec ?? 2.0));
    const out = ease(clamp((u - 0.80) / 0.18));
    for (let i = 0; i < BARS; i++) {
      const k = ease(clamp((u - (0.03 + i * 0.062)) / 0.12));
      const al = st * k * (1 - out);
      if (al <= 0.01) continue;
      const y = BOT - (i + 1) * (BAR_H + BAR_GAP) + BAR_GAP;
      /* 상시 숨 — 사건이 아니라 상시 운동이라 `t` 로 돈다(사건은 자막에 물린다) */
      const bw = (L_X1 - L_X0) - 6 + 4 * (0.5 + 0.5 * Math.sin(t * 7.4 + i * 0.7));
      ctx.save();
      ctx.globalAlpha = al;
      setShadow(ctx, GLOW, 8 + 3 * (0.5 + 0.5 * Math.sin(t * 5.6 + i)));
      ctx.fillStyle = tone('accent');
      ctx.fillRect(L_X0 + 3, y, bw, BAR_H);
      clearShadow(ctx);
      ctx.restore();
    }
    if (spec.innerLabel) ctext(spec.innerLabel, (L_X0 + L_X1) / 2, 272, 900, 13.5, tone('accent'), 120, st);

    /* ── 오른쪽 : 화면에 도달한 것 (흰 점선 그릇 — 끝까지 빈다) ── */
    const off = -t * 60;
    const a = st * 0.95;
    dashRun(R_X0, TOP, R_X1, TOP, 12, 10, off, a);
    dashRun(R_X1, TOP, R_X1, BOT, 12, 10, off, a);
    dashRun(R_X1, BOT, R_X0, BOT, 12, 10, off, a);
    dashRun(R_X0, BOT, R_X0, TOP, 12, 10, off, a);
    if (spec.screenLabel) ctext(spec.screenLabel, (R_X0 + R_X1) / 2, 272, 900, 13.5, tone('ink'), 120, st);

    ctx.textAlign = 'left';
  }
};
