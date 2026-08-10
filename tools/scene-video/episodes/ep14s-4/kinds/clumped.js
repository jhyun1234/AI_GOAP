import {
  disp, ease, clamp, frac, spring,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* clumped — 훅. 집이 모닥불에서 안 떨어진다.

   원문 근거:
   > "집도 성격 다섯 종류와 무관하게 모닥불 옆 빈자리에 차례로 붙어서 지어졌습니다."
   > "성격별 선호 거리를 넣었는데도 집이 생각만큼 멀리 가지 않았습니다."

   🔴 **훅만 가로 한 줄이다.** 본문 넷(S1~S4)은 「가운데 불 하나와 그 둘레의 여섯」이라는
   같은 판을 쓰는데, 훅은 원문이 쓴 낱말(「모닥불 옆 빈자리에 **차례로 붙어서**」)을 그대로
   그린 것이라 **줄**이 맞다. 본문과 한눈에 갈리라는 뜻도 있다 — 훅은 증상(다 붙는다),
   본문은 원인(가운데가 당긴다)이다.

   루프(2.2초): 불 바로 옆에서부터 여섯 칸이 **하나씩 차례로 튀어 올라** 한 줄을 이룬다.
   다 서면 잠깐 그대로 있다가 흐려지고 되풀이된다.
   라벨을 다 가려도 「전부 한 덩어리로 저 하나 옆에 선다」가 읽힌다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **가로 좌표로 갈랐다.**
     불(accent) 글립 x 43~73 · 글로우(blur 최대 13)까지 x 30~86 / 집 칸(ink) 첫 칸 왼쪽 x 96.
     10px 뜬다. **바닥 점선(track)도 x 92 에서 시작한다** — 24 에서 시작하면 불의 밑변과
     같은 픽셀을 지난다(아래 주석). 「모닥불」 라벨은 accent 라 불과 같은 색이다.

   세로 예산(캔버스 307): 최저 = 「모닥불」 글립 바닥 231. 76px 남는다.
   가로: 마지막 칸의 오른쪽 끝 = 슬롯 중심 303 + 반너비 17.85(spring 오버슈트 1.05 포함)
   + 지붕 처마 3 = **324**. 캔버스 352, 여유 28px. 등장에 이동이 0 이라 이 값이 최댓값이다.

   계속 도는 것 = 붙는 주기(2.2초 · 면적이 큰 칸 여섯) + 불꽃 맥동 + 바닥 점선 흐름. */

const GROUND = 200;
const FIRE_X = 58, FIRE_BASE = 200;
const SLOT_X0 = 96, SLOT_GAP = 38, BOX_W = 34, BOX_H = 30;

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

    /* 🔴 긴 가로 점선을 setLineDash 로 그리면 30fps 3패스에서 알파가 갈린다(결정성 반려 전례).
       조각을 fillRect 로 직접 놓고 위상은 양수로 정규화한다. */
    const dashRow = (y, x0, x1, seg, gap, off, alpha) => {
      const P = seg + gap;
      const ph = ((off % P) + P) % P;
      ctx.save();
      ctx.globalAlpha = alpha; ctx.fillStyle = tone('track');
      for (let x = x0 - P + ph; x < x1; x += P) {
        const a = Math.max(x0, x), b = Math.min(x1, x + seg);
        if (b > a) ctx.fillRect(a, y - 1.5, b - a, 3);
      }
      ctx.restore();
    };

    const c0 = cue(spec.clumpCue ?? 0, 0.15, 0.90);
    const st = ease(clamp(c0 / 0.14));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    const n = Math.max(2, spec.houses ?? 6);

    if (spec.title) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
      ctx.restore();
    }

    /* ── 바닥 ───────────────────────────────────────
       🔴 x 92 에서 시작한다. 24 에서 시작하면 바닥 점선(track = 흰색 12%)이 불(accent)의
       삼각형 밑변과 **같은 픽셀을 지난다** — 게이트는 두 색의 혼합을 hue 150~152 로 읽어
       어느 순서든 통과시키므로 게이트 통과가 근거가 못 된다. 불의 글로우(blur 최대 13)가
       닿는 오른쪽 한계가 x 86 이라 6px 뜬다. */
    dashRow(GROUND, 92, w - 24, 12, 10, -t * 20, st * 0.95);

    /* ── 모닥불 : 이 회차에서 강조색은 「불」 하나뿐이다 ── */
    const puff = 0.5 + 0.5 * Math.sin(t * 3.1);
    ctx.save();
    ctx.globalAlpha = st;
    setShadow(ctx, GLOW, 10 + 3 * puff);
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    const fh = 40 + 5 * puff;
    ctx.beginPath();
    ctx.moveTo(FIRE_X, FIRE_BASE - fh);
    ctx.lineTo(FIRE_X + 15, FIRE_BASE);
    ctx.lineTo(FIRE_X - 15, FIRE_BASE);
    ctx.closePath(); ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(FIRE_X, FIRE_BASE - fh * 0.52);
    ctx.lineTo(FIRE_X + 7, FIRE_BASE - 2);
    ctx.lineTo(FIRE_X - 7, FIRE_BASE - 2);
    ctx.closePath(); ctx.stroke();
    clearShadow(ctx);
    ctx.restore();
    if (spec.fireLabel) ctext(spec.fireLabel, FIRE_X, 226, 900, 13.5, tone('accent'), 86, st);

    /* ── 집 여섯 : 왼쪽부터 차례로 딱 붙는다 ─────────── */
    const u = frac(t / (spec.loopSec ?? 2.2));
    for (let i = 0; i < n; i++) {
      const a0 = 0.06 + i * 0.09;
      const k = ease(clamp((u - a0) / 0.13));
      const out = ease(clamp((u - 0.86) / 0.13));
      const al = st * k * (1 - out);
      if (al <= 0.01) continue;
      /* 🔴 **밖에서 밀려 들어오지 않는다.** 이 캔버스(352px)에서 `translate` 등장은 거의
         항상 가장자리를 문다 — 여기서는 오른쪽 끝 칸이 문제였다. 대신 **제자리에서 자란다**
         (`spring` 은 자기 중심에 걸리므로 이동이 0 이다). 뜻도 이쪽이 맞는다: 원문이
         「차례로 붙어서 **지어졌습니다**」라 세워지는 편이 옳다. */
      const sc = spring(clamp((u - a0) / 0.13));   // 0.86 → 1.05 → 1.00
      const bw = BOX_W * sc, bh = BOX_H * sc;
      const cx = SLOT_X0 + i * SLOT_GAP + BOX_W / 2;
      ctx.save();
      ctx.globalAlpha = al;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, cx - bw / 2, GROUND - bh, bw, bh, 4); ctx.stroke();
      /* 지붕 — 집으로 읽히게 하는 최소한의 획 */
      ctx.beginPath();
      ctx.moveTo(cx - bw / 2 - 3, GROUND - bh);
      ctx.lineTo(cx, GROUND - bh - 13 * sc);
      ctx.lineTo(cx + bw / 2 + 3, GROUND - bh);
      ctx.stroke();
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
