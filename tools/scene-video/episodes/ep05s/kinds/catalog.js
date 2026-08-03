import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* catalog — 만든 것은 카탈로그의 빈 칸에 들어가고, .cs 묶음은 봉인된 채 그대로다.

   왼쪽은 카탈로그다. 점선 빈 슬롯 셋에 에셋 카드가 하나씩 올라와 꽂힌다
   (CookMeal · EatCookedFood · Goal_CookAhead — 원문 3절이 이름을 그대로 부른 셋).
   오른쪽은 .cs 묶음이고, 이 샷 내내 **아무 일도 일어나지 않는다.** 띠가 그대로 묶여 있다.

   🔴 편집기도 줄 번호도 0 이라는 숫자도 그리지 않았다. 그건 앞선 회차가 이미 쓴 그림이고,
   그때는 '설계가 이렇게 될 것이다'의 리허설이었다. 이 편은 실전이라, 화면이 세는 것은
   0 이 아니라 **채워지는 슬롯 셋**이고 .cs 는 세지 않고 그냥 안 열린다.

   계속 도는 것 = 아직 빈 슬롯의 점선이 흐르는 것, 봉인 딱지가 미세하게 흔들리는 것. */

const SEAL = 3.4;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));

    const tk = ease(cue(spec.taskCue ?? 0, 0.15, 0.6));
    const sk = ease(cue(spec.seatCue ?? 1, 0.15, 0.72));
    const ck = ease(cue(spec.csCue ?? 2, 0.15, 0.6));

    ctx.textBaseline = 'alphabetic';
    const fitM = (txt, weight, start, max, min = 6.5) => {
      let fs = start; ctx.font = mono(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = mono(weight, fs); }
      return fs;
    };
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    /* ── 과제 ─────────────────────────────────────── */
    {
      const bx = 12, bw = w - 24, by = 8, bh = 30;
      const s = Math.min(1, spring(clamp(tk * 1.4)));
      ctx.globalAlpha = clamp(tk * 2);
      ctx.lineWidth = 4; ctx.strokeStyle = tone('accent');
      setShadow(ctx, GLOW, 12, 0);
      roundRect(ctx, bx + (bw - bw * s) / 2, by + (bh - bh * s) / 2, bw * s, bh * s, 4);
      ctx.stroke();
      clearShadow(ctx);
      ctx.textAlign = 'center';
      const fs = fit(spec.task || '', 900, 15.5, bw - 30, 11);
      ctx.font = disp(900, fs * s); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.task || '', w / 2, by + 21);
      ctx.globalAlpha = 1;
    }

    /* ── 카탈로그 슬롯 ────────────────────────────── */
    const SX = 14, SW = 182, SH = 34, SY = [70, 110, 150];
    const assets = spec.assets || [];

    ctx.globalAlpha = clamp(tk * 2);
    ctx.textAlign = 'left';
    ctx.font = disp(800, 10.5); ctx.fillStyle = tone('sub');
    ctx.fillText(spec.catalogLabel || '카탈로그', SX + 2, 62);
    ctx.globalAlpha = 1;

    assets.forEach((name, i) => {
      const born = clamp(sk * (assets.length + 0.6) - i);
      const y = SY[i] ?? (SY[0] + i * 40);

      /* 빈 슬롯 — 아직 안 찼으면 점선이 흐른다 */
      if (born < 0.99) {
        ctx.globalAlpha = clamp(tk * 2) * (1 - born) * 0.9;
        ctx.setLineDash([8, 6]);
        ctx.lineDashOffset = -(t * 12) % 14;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        roundRect(ctx, SX, y, SW, SH, 3); ctx.stroke();
        ctx.setLineDash([]); ctx.lineDashOffset = 0;
        ctx.globalAlpha = 1;
      }

      if (born < 0.02) return;

      /* 카드 — 아래에서 올라와 슬롯에 꽂힌다 */
      const rise = ease(clamp(born / 0.85));
      const cy = lerp(206, y, rise);
      const s = Math.min(1, spring(clamp(born)));
      ctx.globalAlpha = clamp(born * 1.8);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
      if (born > 0.85) setShadow(ctx, GLOW, 10, 0);
      roundRect(ctx, SX + (SW - SW * s) / 2, cy + (SH - SH * s) / 2, SW * s, SH * s, 3);
      ctx.stroke();
      clearShadow(ctx);
      ctx.textAlign = 'left';
      const fs = fitM(name, 700, 10.5, SW - 20, 6.5);
      ctx.font = mono(700, fs); ctx.fillStyle = tone('accent');
      ctx.fillText(name, SX + 11, cy + SH / 2 + 4);
      ctx.globalAlpha = 1;
    });

    /* ── .cs 묶음 — 봉인된 채 그대로 ──────────────── */
    {
      const bx = 210, bw = w - bx - 12, by = 70, bh = 114;
      ctx.globalAlpha = clamp(tk * 2);
      ctx.textAlign = 'left';
      ctx.font = mono(700, 11); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.csLabel || '.cs', bx + 2, 62);

      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      roundRect(ctx, bx, by, bw, bh, 3); ctx.stroke();

      // 안에 든 판들
      for (let i = 0; i < 5; i++) {
        const py = by + 12 + i * 19;
        ctx.globalAlpha = clamp(tk * 2) * 0.55;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(bx + 10, py); ctx.lineTo(bx + bw - 10, py);
        ctx.stroke();
      }

      // 봉인 띠
      const tx = bx + bw / 2 - 9;
      ctx.globalAlpha = clamp(tk * 2) * 0.85;
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(tx, by - 4); ctx.lineTo(tx, by + bh + 4);
      ctx.moveTo(tx + 18, by - 4); ctx.lineTo(tx + 18, by + bh + 4);
      ctx.stroke();

      /* 계속 도는 것 — 봉인 딱지가 미세하게 흔들린다 */
      const sway = 1.6 * Math.sin(frac(t / SEAL) * Math.PI * 2);
      const sealY = by + bh / 2 + sway;
      ctx.globalAlpha = clamp(tk * 2);
      ctx.strokeStyle = ck > 0.3 ? tone('accent') : tone('sub');
      ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(tx + 9, sealY, 11, 0, Math.PI * 2); ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(tx + 3, sealY); ctx.lineTo(tx + 15, sealY);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 한 줄도 추가하지 않았습니다 ──────────────── */
    if (ck > 0.05 && spec.csNote) {
      const k = clamp(ck * 1.6);
      const s = Math.min(1, spring(clamp(ck * 1.3)));
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.csNote, 900, 15, w - 28, 10);
      ctx.font = disp(900, fs * s); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.csNote, w / 2, 268);
      ctx.globalAlpha = 1;
    }

    if (ck > 0.4 && spec.csSub) {
      const k = clamp((ck - 0.4) / 0.5);
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.csSub, 700, 10.5, w - 34, 8);
      ctx.font = disp(700, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.csSub, w / 2, 290);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
