import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* roster — 자리가 몇 개인지 정해 두면, 나머지는 돌아선다.

   ① crowdCue — 아래에서 일꾼 다섯이 **각자 다른 줄로** 공사 문 앞까지 올라와 들썩인다.
      겹치지 않는다. 여기서 나쁜 것은 충돌이 아니라 **전부 온다는 것**이다.
   ② quotaCue — 앞선 둘이 문을 지나 현장 안으로 들어가고, 곧바로 빗장이 내려와 문을 닫는다.
      남은 셋은 방향을 틀어 물러난다.

   🔴 앞 회차에 '한 타일에 겹쳐 ✕ → 반경으로 흩어짐'이 있었다. 여기에는 겹침도 ✕ 도 없고
   흩어짐도 없다. 다섯은 처음부터 제 줄로 오고, 사건은 **빗장 하나가 내려오는 것**이며,
   못 들어간 쪽은 흩어지는 게 아니라 **한 걸음 물러나 그 자리에 남는다**(일을 잃은 게 아니라
   이 공사에 못 붙었을 뿐이다).

   🔴 원문은 `GoalSO.MaxWorkers` 의 값을 말하지 않는다. 그래서 화면에 숫자를 하나도 안 적는다.
   일꾼 다섯과 통과 둘은 **도형 파라미터일 뿐** 상한값이 아니고, 현장 안에 칸을 그려 세는
   방식(슬롯 두 칸)도 일부러 쓰지 않았다 — 칸을 그리면 그 개수가 곧 MaxWorkers 값이라는
   주장이 된다.

   계속 도는 것 = ① 다섯의 들썩임(자막 0 부터 끝까지) ② 문턱 점선의 흐름
   ③ 현장에 들어간 둘의 작업 흔들림. */

const SITE = { x: 76, y: 56, w: 200, h: 78 };   // x 76~276 · y 56~134
const GATE_Y = 164, GX0 = 76, GX1 = 276;
const LANES = [30, 103, 176, 249, 322];
const AWAY = [-12, 0, 0, 16, 12];               // 물러나는 방향 (통과 조는 0)
const PASS = [2, 1];                            // 앞선 둘 — 가운데부터 들어간다
const START_Y = 268, WAIT_Y = 214, IN_Y = 124;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));

    const ck = ease(cue(spec.crowdCue ?? 0, 0.15, 0.62));
    const qk = ease(cue(spec.quotaCue ?? 1, 0.15, 0.72));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const app = ease(clamp(ck / 0.8));
    const turn = ease(clamp((qk - 0.45) / 0.4));
    const bar = ease(clamp((qk - 0.45) / 0.35));

    /* ── 공사 현장 ────────────────────────────────── */
    ctx.globalAlpha = 0.85;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.setLineDash([10, 7]);
    ctx.lineDashOffset = -(t * 8) % 17;
    roundRect(ctx, SITE.x, SITE.y, SITE.w, SITE.h, 4); ctx.stroke();
    ctx.setLineDash([]); ctx.lineDashOffset = 0;
    ctx.globalAlpha = 1;

    if (spec.siteLabel) {
      ctx.globalAlpha = 0.9;
      ctx.textAlign = 'left';
      const fs = fit(spec.siteLabel, 800, 11.5, SITE.w - 20, 8);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.siteLabel, SITE.x + 10, SITE.y + 20);
      ctx.globalAlpha = 1;
    }

    /* ── 문턱 ─────────────────────────────────────── */
    ctx.globalAlpha = 0.9;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.setLineDash([8, 6]);
    ctx.lineDashOffset = -(t * 11) % 14;         // 계속 도는 것 — 문턱
    ctx.beginPath(); ctx.moveTo(GX0, GATE_Y); ctx.lineTo(GX1, GATE_Y); ctx.stroke();
    ctx.setLineDash([]); ctx.lineDashOffset = 0;
    ctx.globalAlpha = 1;

    /* ── 일꾼 다섯 ────────────────────────────────── */
    LANES.forEach((lane, i) => {
      const pass = PASS.includes(i);
      const jx = 2.4 * Math.sin(frac(t / 1.7) * Math.PI * 2 + i * 1.1);
      const jy = 4.5 * Math.sin(frac(t / 1.05) * Math.PI * 2 + i * 0.8);
      const base = lerp(START_Y, WAIT_Y, app);

      let x, y, a = 1;
      if (pass) {
        const order = PASS.indexOf(i);
        const p = ease(clamp((qk - order * 0.14) / 0.6));
        y = lerp(base, IN_Y, p) + jy * (1 - 0.55 * p);
        x = lane + jx * (1 - 0.5 * p) + 2.6 * p * Math.sin(frac(t / 0.85) * Math.PI * 2 + i);
      } else {
        x = lane + AWAY[i] * turn + jx;
        y = base + jy;
        a = 1 - 0.3 * turn;
      }

      ctx.globalAlpha = a;
      ctx.fillStyle = pass ? tone('accent') : tone('ink');
      roundRect(ctx, x - 7, y - 20, 14, 20, 3); ctx.fill();
      ctx.beginPath(); ctx.arc(x, y - 26, 5.5, 0, Math.PI * 2); ctx.fill();
      ctx.globalAlpha = 1;
    });

    /* ── 빗장 — 정원이 차면 내려온다 ─────────────── */
    if (bar > 0.01) {
      const by = lerp(GATE_Y - 30, GATE_Y - 6, bar);
      ctx.globalAlpha = clamp(bar * 1.8);
      setShadow(ctx, GLOW, 12, 0);
      ctx.fillStyle = tone('accent');
      roundRect(ctx, GX0, by, GX1 - GX0, 10, 3); ctx.fill();
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    /* ── 이름표 ──────────────────────────────────── */
    ctx.globalAlpha = 0.9;
    ctx.textAlign = 'left';
    let fs = 10; ctx.font = mono(700, fs);
    const nm = spec.quota || '';
    while (fs > 6.5 && ctx.measureText(nm).width > 140) { fs -= 0.5; ctx.font = mono(700, fs); }
    ctx.fillStyle = tone('sub');
    ctx.fillText(nm, 12, 30);
    ctx.globalAlpha = 1;

    if (qk > 0.35 && spec.quotaNote) {
      const k = clamp((qk - 0.35) / 0.45);
      ctx.globalAlpha = k;
      ctx.textAlign = 'right';
      const nfs = fit(spec.quotaNote, 900, 17, 128, 10);
      ctx.font = disp(900, nfs); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.quotaNote, w - 12, 32);
      ctx.globalAlpha = 1;
    }

    if (qk > 0.55 && spec.fullNote) {
      const k = clamp((qk - 0.55) / 0.4);
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const nfs = fit(spec.fullNote, 700, 11.5, w - 28, 8);
      ctx.font = disp(700, nfs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.fullNote, w / 2, 292);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
