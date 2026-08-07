import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* headon — 내가 위에서 내린 절차와, 아래에서 올라오는 GOAP 이 한복판에서 막힌다.

   원문의 낱말이 '**정면으로** 어긋나는 설계'다. 그래서 축을 세로 한 줄로 두고
   방향만 반대로 줬다 — 앞 샷(askover)에서 위→아래로 매달던 각진 흰 상자가 그대로
   내려오고, 아래에서는 둥근 강조색 캡슐이 줄기를 뻗어 올라온다.

   🔑 여기서 자라는 것은 **줄기뿐이고 GOAP 캡슐은 처음부터 화면에 있다.** 원문이
   '이 게임의 **두뇌인** GOAP' 이라고 이미 있는 것으로 쓰기 때문이다 — 이 편에서
   새로 생긴 것은 GOAP 이 아니라 내 절차다.

   자막이 한 줄이라 그 한 줄을 앞뒤로 가른다. spec.meet 앞은 서로에게 다가가는
   구간이고, 뒤는 막대가 생기고 양쪽이 도로 밀리는 구간이다. meet 0.62 는 그 자막에서
   「정면으로」가 시작되는 자리이고, S2 의 thud 도 같은 지점에 놓여 있다.

   🔴 t 로 도는 것(줄기 점선 · 응력 밴드 · 눌림 진동)은 문턱을 하나도 안 가진다. */

const PRESS = 0.9;    // 막힌 뒤 서로를 미는 진동 주기(초)

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const fit = (txt, weight, start, max, min = 8) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const meet = spec.meet ?? 0.62;
    const c0 = cue(spec.clashCue ?? 0, 0.15, 0.85);
    const gk = ease(clamp(c0 / meet));                 // 서로에게 다가간다
    const hk = ease(clamp((c0 - meet) / 0.28));        // 막히고 도로 밀린다

    const CX = w / 2;
    const MID = h * 0.53;                              // 접점
    const BW = 150, BX = (w - BW) / 2, BH = 30;
    const A1 = 30, A2 = 74;                            // 각진 상자 둘의 y
    const CAPW = 128, CAPH = 34, CAPY = h - 54;        // GOAP 캡슐

    /* 막힌 뒤 서로를 미는 미세한 눌림 — 계속 돈다 */
    const press = hk * (1.6 + Math.sin(t * Math.PI * 2 / PRESS) * 1.2);

    /* ── 위: 내가 내린 각진 상자 둘 ──────────────── */
    const mine = spec.mine ?? [];
    for (let i = 0; i < mine.length; i++) {
      const k = ease(clamp(gk * 1.7 - i * 0.4));
      if (k <= 0.02) continue;
      const y = (i === 0 ? A1 : A2) - press;

      ctx.globalAlpha = k;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.rect(BX, y, BW, BH); ctx.stroke();

      ctx.textAlign = 'center';
      const fs = fit(mine[i], 800, 15, BW - 20, 9);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(mine[i], CX, y + BH / 2 + fs * 0.36);

      if (i > 0) {                                     // 상자 사이를 잇는 짧은 줄
        ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.moveTo(CX, A1 + BH - press); ctx.lineTo(CX, y); ctx.stroke();
      }
      ctx.globalAlpha = 1;
    }

    /* 위 줄기 — 아래로 뻗다가 막대 앞에서 멈춘다 */
    const topEnd = lerp(A2 + BH + 6, MID - 9, ease(clamp(gk))) - press;
    if (gk > 0.02) {
      ctx.save();
      ctx.globalAlpha = clamp(gk * 2);
      ctx.setLineDash([11, 8]);
      ctx.lineDashOffset = -(t * 20) % 19;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
      ctx.beginPath(); ctx.moveTo(CX, A2 + BH - press); ctx.lineTo(CX, topEnd); ctx.stroke();
      ctx.restore();
    }

    /* ── 아래: GOAP 캡슐 — 처음부터 있다 ─────────── */
    if (spec.engine) {
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      setShadow(ctx, GLOW, 10, 0);
      roundRect(ctx, (w - CAPW) / 2, CAPY + press, CAPW, CAPH, CAPH / 2); ctx.stroke();
      clearShadow(ctx);
      ctx.textAlign = 'center';
      const fs = fit(spec.engine, 900, 19, CAPW - 24, 11);
      ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.engine, CX, CAPY + press + CAPH / 2 + fs * 0.36);
    }

    /* 아래 줄기 — 위로 뻗다가 막대 앞에서 멈춘다 */
    const botEnd = lerp(CAPY - 6, MID + 9, ease(clamp(gk))) + press;
    if (gk > 0.02) {
      ctx.save();
      ctx.globalAlpha = clamp(gk * 2);
      ctx.setLineDash([11, 8]);
      ctx.lineDashOffset = (t * 20) % 19;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.beginPath(); ctx.moveTo(CX, CAPY + press); ctx.lineTo(CX, botEnd); ctx.stroke();
      ctx.restore();
    }

    /* ── 접점 — 굵은 막대와 응력 밴드 ─────────────── */
    if (hk > 0.02) {
      const bw = 200 * ease(clamp(hk * 1.5));

      // 응력 밴드 — 막대 양옆으로 빗금이 계속 흐른다(이 샷에서 가장 큰 면적)
      ctx.save();
      ctx.globalAlpha = 0.5 * hk;
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.rect(CX - 112, MID - 9, 224, 18);
      ctx.clip();
      const off = (t * 24) % 20;
      for (let x = -20; x < 244; x += 20) {
        ctx.beginPath();
        ctx.moveTo(CX - 112 + x + off, MID + 11);
        ctx.lineTo(CX - 112 + x + off + 14, MID - 11);
        ctx.stroke();
      }
      ctx.restore();

      // 막대 — 어느 쪽도 이걸 통과하지 못한다
      ctx.fillStyle = tone('ink');
      ctx.fillRect(CX - bw / 2, MID - 3, bw, 6);
    }

    ctx.textAlign = 'left';
  }
};
