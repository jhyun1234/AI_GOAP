import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* bothon — 할 수 있게 된 일이 둘 켜지는데, 각각을 받치는 것은 얇은 카드 한 장뿐이다.

   이 편의 첫 샷이다. 사건은 "기능이 늘었다"이고, 화면이 세는 것은 **켜진 칸의 수**가 아니라
   그 칸을 떠받치고 있는 것의 **두께**다. cookCue 에서 왼쪽 칸에 얇은 카드가 밀려 들어가며
   「요리」가 켜지고, restCue 에서 오른쪽 칸에 같은 일이 한 번 더 일어난다.
   카드는 칸 높이의 3분의 1도 안 되는 막대이고, 그것 말고는 아무것도 안 들어간다.

   🔴 편집기도 줄 번호도 0 이라는 카운터도 그리지 않았다. 앞선 회차가 '코드 0줄'을
   숫자로 그렸고, 이 편은 숫자를 세는 이야기가 아니라 **고친 것의 크기**가 얼마나 작은가의
   이야기다. 그래서 0 은 자막이 말하고 화면은 카드의 두께로 말한다.

   🔴 아래 주민이 두 칸 사이를 오가는 것은 문제가 아니라 결과다(할 수 있는 일이 둘 생겼다).
   그래서 겹침 표시도 ✕ 도 못도 하나 안 썼다 — 이 샷에는 막히는 것이 없다.

   계속 도는 것 = 두 칸 사이를 오가는 주민 표식(주기 6.4초·이동 폭 168px),
   아직 안 켜진 칸의 점선 흐름, 켜진 칸에서 바닥으로 내려가는 점선 흐름. */

const WALK = 6.4;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));

    const names = spec.slots || ['요리', '집에서 쉬기'];
    const cues = [spec.cookCue ?? 0, spec.restCue ?? 1];
    const ks = names.map((_, i) => ease(cue(cues[i] ?? i, 0.15, 0.66)));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const PW = 148, PH = 58, GAP = 20;
    const SX = (w - (PW * 2 + GAP)) / 2;
    const PY = 52;
    const CBY = PY + PH + 12, CBH = 18;          // 칸을 받치는 카드
    const GY = 232;                              // 바닥선
    const cx = i => SX + i * (PW + GAP) + PW / 2;

    const anyK = Math.max(...ks, 0);

    /* ── 위 라벨 ──────────────────────────────────── */
    if (spec.deckLabel && anyK > 0.02) {
      ctx.globalAlpha = clamp(anyK * 2.4);
      ctx.textAlign = 'center';
      const fs = fit(spec.deckLabel, 800, 12, w - 40, 9);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.deckLabel, w / 2, 30);
      ctx.globalAlpha = 1;
    }

    /* ── 칸 둘 ────────────────────────────────────── */
    names.forEach((name, i) => {
      const k = ks[i] ?? 0;
      const px = SX + i * (PW + GAP);

      /* 아직 안 켜진 칸 — 점선이 흐른다 */
      if (k < 0.99) {
        ctx.globalAlpha = (1 - k) * 0.9;
        ctx.setLineDash([9, 7]);
        ctx.lineDashOffset = -(t * 14) % 16;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        roundRect(ctx, px, PY, PW, PH, 4); ctx.stroke();
        ctx.setLineDash([]); ctx.lineDashOffset = 0;
        ctx.globalAlpha = 1;
      }

      if (k < 0.02) return;

      /* 받치는 카드 — 아래에서 밀려 들어와 칸 밑에 깔린다 */
      const rise = ease(clamp(k / 0.8));
      const cby = lerp(CBY + 46, CBY, rise);
      ctx.globalAlpha = clamp(k * 2);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      if (rise > 0.9) setShadow(ctx, GLOW, 10, 0);
      roundRect(ctx, px + 16, cby, PW - 32, CBH, 3); ctx.stroke();
      clearShadow(ctx);
      ctx.textAlign = 'center';
      const cfs = fit(spec.cardLabel || '에셋', 800, 10.5, PW - 44, 8);
      ctx.font = disp(800, cfs); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.cardLabel || '에셋', px + PW / 2, cby + CBH / 2 + 4);
      ctx.globalAlpha = 1;

      /* 켜진 칸 */
      const s = Math.min(1, spring(clamp(k * 1.2)));
      ctx.globalAlpha = clamp(k * 2);
      ctx.lineWidth = 4; ctx.strokeStyle = tone('ink');
      roundRect(ctx, px + (PW - PW * s) / 2, PY + (PH - PH * s) / 2, PW * s, PH * s, 4);
      ctx.stroke();
      ctx.globalAlpha = 1;

      if (k > 0.3) {
        const kk = clamp((k - 0.3) / 0.4);
        const s2 = Math.min(1, spring(kk));
        ctx.globalAlpha = kk;
        ctx.textAlign = 'center';
        const fs = fit(name, 900, 19, PW - 22, 12);
        ctx.font = disp(900, fs * s2); ctx.fillStyle = tone('ink');
        ctx.fillText(name, px + PW / 2, PY + PH / 2 + 7);
        ctx.globalAlpha = 1;
      }

      /* 켜진 칸에서 바닥으로 내려가는 점선 */
      if (rise > 0.5) {
        ctx.globalAlpha = clamp((rise - 0.5) / 0.5) * 0.8;
        ctx.setLineDash([7, 6]);
        ctx.lineDashOffset = -(t * 11) % 13;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(px + PW / 2, CBY + CBH + 8);
        ctx.lineTo(px + PW / 2, GY - 40);
        ctx.stroke();
        ctx.setLineDash([]); ctx.lineDashOffset = 0;
        ctx.globalAlpha = 1;
      }
    });

    /* ── 바닥과 주민 ──────────────────────────────── */
    if (anyK > 0.1) {
      const g = clamp((anyK - 0.1) / 0.4);
      ctx.globalAlpha = g * 0.9;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(22, GY); ctx.lineTo(w - 22, GY); ctx.stroke();
      ctx.globalAlpha = 1;

      /* 계속 도는 것 — 두 칸 사이를 오가는 표식 하나 */
      const u = 0.5 - 0.5 * Math.cos(frac(t / WALK) * Math.PI * 2);
      const vx = lerp(cx(0), cx(names.length - 1), u);
      ctx.globalAlpha = g;
      ctx.fillStyle = tone('ink');
      roundRect(ctx, vx - 6, GY - 22, 12, 22, 3); ctx.fill();
      ctx.beginPath(); ctx.arc(vx, GY - 28, 5.5, 0, Math.PI * 2); ctx.fill();
      ctx.globalAlpha = 1;
    }

    /* ── 받치고 있는 것 ───────────────────────────── */
    if (spec.costLabel && anyK > 0.45) {
      const k = clamp((anyK - 0.45) / 0.4);
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.costLabel, 900, 14, w - 34, 10);
      ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.costLabel, w / 2, 284);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
