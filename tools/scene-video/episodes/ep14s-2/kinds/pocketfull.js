import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* pocketfull — 막힌 것은 받는 쪽이었다. 목수 주머니에 앉을 칸이 모자란다.

   원문 근거:
   > "탈락한 건 엉뚱하게도 목수 자신의 주머니 여유 공간이었습니다. 품삯이 다섯 알인데
   >  목수 주머니에 이미 곡식이 있으면 상한 8에 걸려 '수령 불가'로 판정된 겁니다."
   > "(몸에 지닐 수 있는 식량은 8개까지입니다)"

   🔴 기본 도형의 두 번째 변주 — 앞 샷에서 사람을 되밀던 벽이 여기서는 **주머니 끝의 벽**이다.
   같은 어휘를 다시 쓰는 것이 폭로가 되도록, 벽의 색·굵기·맥동을 앞 샷과 같은 문법으로 뒀다.

   🔴 어려운 낱말을 화면에서 뺐다 — 「수령 공간」·「수령 불가 판정」 대신 원문이 준 쉬운 말
   (「목수 주머니 여유 공간」·「곡식은 8개까지」)만 올렸다. 「판정」이라는 낱말은 어디에도 없다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — 부등식으로 막았다.
     · 이미 지닌 곡식(흰 알)은 앞 네 칸에만: 최대 x = cellCx(3) + 7 = 166.
     · 품삯(강조색 알)은 뒤 네 칸에만: 최소 x = cellCx(4) - 7 = 186. → 두 무리가 20px 뜬다.
     · 날아오는 동안 품삯은 y 125~139, 이미 지닌 곡식은 y 182~196 → 43px 뜬다.
     · 칸 테두리는 흰색 31%다. 칸 폭 28 · 알 반지름 7 이므로 알과 테두리 사이가 **7px**
       벌어지고, 그래서 **품삯 알에는 글로우를 안 걸었다**(글로우를 걸면 테두리를 먹는다).
       글로우가 붙은 강조색은 벽 하나뿐이고 벽은 격자 오른쪽 10px 밖에 서 있다.

   🔴 캔버스 밖으로 안 나간다: 벽 글로우 최대 x = 319 + 3 + 12 = 334 (캔버스 352, 판정 띠는
      바깥 2열 = 350·351). 화면 밖에서 들어오는 알은 `eg` 로 x 20 아래에서 알파 0 이라
      왼쪽 띠를 한 프레임도 안 칠한다. 세로 최저 = 234 베이스라인(글립 ~238).

   ⏱ 🔴 **막히는 순간을 `since(1)` 위에 세웠다.** `t` 로 돌리면 앞 자막이 한 글자만 늘어도
      사건 시각이 통째로 밀려 `sfx.delayMs` 를 다시 풀어야 한다. `since` 는 그 자막이
      시작된 뒤 흐른 초라 `delayMs` 와 원점이 같다 — 주기 2.2초 · 접촉 위상 0.50 이므로
      **첫 접촉은 자막 1 시작 + 1.100초**이고, 이 수는 TTS 길이와 무관하다.
   계속 도는 것 = 자막 0 구간은 흰 알 넷이 위에서 내려앉는 움직임, 자막 1 부터는 2.2초 루프. */

const N = 8, CW = 28, GAP = 6;
const RAIL_Y = 132, CELL_T = 170, CELL_H = 38;
const CELL_CY = CELL_T + CELL_H / 2;      // 189
const DOT_R = 7;
const HIT = 0.50;                          // 접촉 위상 — sfx.delayMs 의 근거

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const ctext = (txt, x, y, weight, size, color, maxW, alpha) => {
      if (!txt || alpha <= 0.01) return;
      let fs = size; ctx.font = disp(weight, fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(weight, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.textAlign = 'center'; ctx.fillStyle = color;
      ctx.fillText(txt, clamp(x, 12 + tw / 2, w - 12 - tw / 2), y);
      ctx.restore();
    };

    const TOTAL = N * CW + (N - 1) * GAP;          // 266
    const X0 = (w - TOTAL) / 2;                    // 43
    const cellL = i => X0 + i * (CW + GAP);
    const cellCx = i => cellL(i) + CW / 2;         // 57, 91, … 295
    const BAR_X = X0 + TOTAL + 10;                 // 319
    const HITX = BAR_X - 12;                       // 307

    const held = Math.max(0, Math.min(N, spec.held ?? 4));
    const wage = Math.max(1, spec.wage ?? 5);
    const slots = N - held;                        // 앉을 수 있는 칸 수

    /* ── 구획 이름 ─────────────────────────────────── */
    if (spec.title) {
      ctx.save();
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
      ctx.restore();
    }

    const c0 = cue(spec.pocketCue ?? 0, 0.15, 0.62);
    const st = ease(clamp(c0 / 0.16));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    /* ── 여덟 칸 ───────────────────────────────────── */
    ctx.save();
    ctx.globalAlpha = st * 0.45;
    ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
    for (let i = 0; i < N; i++) { roundRect(ctx, cellL(i), CELL_T, CW, CELL_H, 6); ctx.stroke(); }
    ctx.restore();

    /* ── 이미 지니고 있는 곡식 : 위에서 내려앉는다 ─── */
    for (let i = 0; i < held; i++) {
      const hi = ease(clamp((c0 - 0.18 - 0.13 * i) / 0.22));
      if (hi <= 0.01) continue;
      ctx.save();
      ctx.globalAlpha = hi;
      ctx.fillStyle = tone('ink');
      ctx.beginPath();
      ctx.arc(cellCx(i), lerp(CELL_CY - 34, CELL_CY, hi), DOT_R, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();
    }

    /* ── 품삯 : 자막 1 에 물린 2.2초 루프 ───────────── */
    const s1 = since(spec.wageCue ?? 1);
    let fl = 0;
    if (s1 >= 0) {
      const wa = ease(clamp(s1 / 0.35));
      const P = spec.loopSec ?? 2.2;
      const u = frac(s1 / P);
      const cyc = 1 - ease(clamp((u - 0.86) / 0.14));
      fl = (1 - ease(clamp(Math.abs(u - HIT) / 0.09))) * wa;

      for (let j = 0; j < wage; j++) {
        let x, y;
        if (j < slots) {
          const arrive = ease(clamp((u - 0.02 - 0.055 * j) / 0.30));
          const drop = ease(clamp((u - 0.16 - 0.055 * j) / 0.16));
          x = lerp(26 - 40 * j, cellCx(held + j), arrive);
          y = lerp(RAIL_Y, CELL_CY, drop);
        } else {
          /* 앉을 칸이 없는 알 — 벽까지 갔다가 되튕겨 나온다 */
          const a4 = ease(clamp((u - 0.14) / 0.36));      // HIT 에서 1 이 된다
          const b4 = ease(clamp((u - HIT) / 0.16));
          x = lerp(-130, HITX, a4) - 64 * b4;
          y = RAIL_Y;
        }
        /* eg = 왼쪽 가장자리 보호(x 20 아래에서 알파 0) · ease(u/0.05) = 루프가 되감길 때
           첫 알이 알파 0.23 으로 툭 나타나던 것을 없애는 짧은 페이드 */
        const eg = clamp((x - 20) / 26);
        const a = wa * cyc * eg * ease(clamp(u / 0.05));
        if (a <= 0.01) continue;
        ctx.save();
        ctx.globalAlpha = a;
        ctx.fillStyle = tone('accent');
        ctx.beginPath(); ctx.arc(x, y, DOT_R, 0, Math.PI * 2); ctx.fill();
        ctx.restore();
      }
      ctext(spec.wageLabel, w / 2, 106, 900, 14, tone('accent'), w - 90, wa);
    }

    /* ── 주머니 끝의 벽 ────────────────────────────── */
    ctx.save();
    ctx.globalAlpha = st;
    setShadow(ctx, GLOW, 6 + 6 * fl);
    ctx.strokeStyle = tone('accent');
    ctx.lineWidth = 4 + 2 * fl; ctx.lineCap = 'round';
    ctx.beginPath(); ctx.moveTo(BAR_X, CELL_T - 6); ctx.lineTo(BAR_X, CELL_T + CELL_H + 6); ctx.stroke();
    clearShadow(ctx);
    ctx.restore();

    ctext(spec.capLabel, w / 2, 234, 900, 14, tone('ink'), w - 120, st);

    ctx.textAlign = 'left';
  }
};
