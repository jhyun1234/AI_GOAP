import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* snapin — 만든 것은 카탈로그의 빈 칸에 들어가고, .cs 묶음은 봉인된 채 그대로다.

   첫 번째 재현이다. 왼쪽 카탈로그의 점선 슬롯 셋에 에셋 카드가 하나씩 올라와 꽂힌다
   (CookMeal · EatCookedFood · Goal_CookAhead — 원문 3절이 이름을 그대로 부른 셋).
   오른쪽 .cs 묶음에서는 **이 샷 내내 아무 일도 일어나지 않는다.** 봉인 띠가 걸려 있고
   그 안의 빗금만 계속 흐른다 — 잠겨 있지만 죽어 있지는 않다는 뜻이다.

   🔴 '아무 일도 안 일어났다'를 **비어 있는 카운터가 아니라 열리지 않은 봉인**으로 그렸다.
   0 이라는 숫자를 세는 그림은 앞선 회차가 이미 썼고, 그때는 '설계가 이렇게 될 것이다'의
   리허설이었다. 이 편은 실전이라, 화면이 세는 것은 0 이 아니라 **채워지는 슬롯 셋**이고
   .cs 는 세지 않고 그냥 안 열린다.

   🔴 자막이 한 줄뿐인 짧은 샷이라 문턱을 전부 같은 sk 하나에서 갈랐다 — 카드 셋의 착석,
   봉인 딱지가 강조색으로 바뀌는 것, 아래 두 줄이 전부 seatCue 의 진행도다. 다른 자막의
   진행도로 문턱을 만든 곳이 없다.

   계속 도는 것 = 봉인 띠 안을 흐르는 빗금(11px 주기), 아직 빈 슬롯의 점선 흐름,
   봉인 딱지의 흔들림. */

const SEAL = 3.4;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));

    const sk = ease(cue(spec.seatCue ?? 0, 0.15, 0.72));

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

    const on = clamp(sk * 3);          // 판·라벨은 샷이 시작하자마자 선다

    /* ── 무슨 일을 붙이는가 ───────────────────────── */
    if (spec.task) {
      const bx = 12, bw = w - 24, by = 8, bh = 28;
      const s = Math.min(1, spring(clamp(sk * 3)));
      ctx.globalAlpha = on;
      ctx.lineWidth = 4; ctx.strokeStyle = tone('accent');
      setShadow(ctx, GLOW, 12, 0);
      roundRect(ctx, bx + (bw - bw * s) / 2, by + (bh - bh * s) / 2, bw * s, bh * s, 4);
      ctx.stroke();
      clearShadow(ctx);
      ctx.textAlign = 'center';
      const fs = fit(spec.task, 900, 15, bw - 30, 11);
      ctx.font = disp(900, fs * s); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.task, w / 2, by + 20);
      ctx.globalAlpha = 1;
    }

    /* ── 카탈로그 슬롯 ────────────────────────────── */
    const SX = 14, SW = 178, SH = 30, SY = [66, 102, 138];
    const assets = spec.assets || [];

    ctx.globalAlpha = on;
    ctx.textAlign = 'left';
    ctx.font = disp(800, 10.5); ctx.fillStyle = tone('sub');
    ctx.fillText(spec.catalogLabel || '카탈로그', SX + 2, 58);
    ctx.globalAlpha = 1;

    assets.forEach((name, i) => {
      const born = clamp(sk * (assets.length + 0.6) - i);
      const y = SY[i] ?? (SY[0] + i * 36);

      if (born < 0.99) {
        ctx.globalAlpha = on * (1 - born) * 0.9;
        ctx.setLineDash([8, 6]);
        ctx.lineDashOffset = -(t * 12) % 14;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        roundRect(ctx, SX, y, SW, SH, 3); ctx.stroke();
        ctx.setLineDash([]); ctx.lineDashOffset = 0;
        ctx.globalAlpha = 1;
      }

      if (born < 0.02) return;

      const rise = ease(clamp(born / 0.85));
      const cy = lerp(196, y, rise);
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
      const bx = 208, bw = w - bx - 14, by = 66, bh = 102;
      ctx.globalAlpha = on;
      ctx.textAlign = 'left';
      ctx.font = mono(700, 11); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.csLabel || '.cs', bx + 2, 58);

      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      roundRect(ctx, bx, by, bw, bh, 3); ctx.stroke();

      // 안에 든 판들 — 이 샷에서 한 장도 안 늘고 안 준다
      for (let i = 0; i < 5; i++) {
        const py = by + 14 + i * 18;
        ctx.globalAlpha = on * 0.55;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(bx + 10, py); ctx.lineTo(bx + bw - 10, py);
        ctx.stroke();
      }

      /* 봉인 띠 — 계속 도는 것은 이 안의 빗금이다 */
      const tx = bx + bw / 2 - 17, tw = 34, ty = by - 7, th = bh + 14;
      ctx.globalAlpha = on * 0.9;
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.rect(tx, ty, tw, th); ctx.stroke();

      ctx.save();
      ctx.beginPath(); ctx.rect(tx + 1.5, ty + 1.5, tw - 3, th - 3); ctx.clip();
      const off = (t * 16) % 11;
      ctx.globalAlpha = on * 0.55;
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      for (let d = -th; d < tw + th; d += 11) {
        const x0 = tx + d + off;
        ctx.beginPath();
        ctx.moveTo(x0, ty); ctx.lineTo(x0 - th, ty + th);
        ctx.stroke();
      }
      ctx.restore();

      /* 봉인 딱지 — 미세하게 흔들리지만 끝까지 안 열린다 */
      const sway = 1.7 * Math.sin(frac(t / SEAL) * Math.PI * 2);
      const sealY = by + bh / 2 + sway;
      ctx.globalAlpha = on;
      ctx.strokeStyle = sk > 0.55 ? tone('accent') : tone('sub');
      ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(tx + tw / 2, sealY, 11, 0, Math.PI * 2); ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(tx + tw / 2 - 6, sealY); ctx.lineTo(tx + tw / 2 + 6, sealY);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 한 줄도 추가하지 않았습니다 ──────────────── */
    if (sk > 0.5 && spec.csNote) {
      const k = clamp((sk - 0.5) / 0.35);
      const s = Math.min(1, spring(k));
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.csNote, 900, 15, w - 28, 10);
      ctx.font = disp(900, fs * s); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.csNote, w / 2, 240);
      ctx.globalAlpha = 1;
    }

    if (sk > 0.76 && spec.csSub) {
      const k = clamp((sk - 0.76) / 0.24);
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.csSub, 700, 11, w - 34, 8);
      ctx.font = disp(700, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.csSub, w / 2, 266);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
