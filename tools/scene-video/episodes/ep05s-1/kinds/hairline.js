import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* hairline — 두 값의 차이가 정확히 한 칸이고, 그 한 칸에서 실 같은 것이 솟는다.

   이 편(5-1)의 **첫 샷**이다. 0초에 오는 것은 배경이 아니라 숫자다.
   포만감 소비를 재는 블록 탑 둘을 세운다 — 날 것 3칸, 조리 2칸. 높이가 곧 소비량이라
   두 탑의 차이는 눈으로 세도 한 칸이고, 그 한 칸에 「차이 1」 괄호가 물린다.
   마지막 자막에서 그 칸 꼭대기로 가느다란 기둥이 솟는데 **위에는 아직 아무것도 없다.**
   무엇이 얹히는지는 이 편의 마지막 샷(keystone)이 갚는다.

   🔴 판(slab)을 여기서 그리지 않는 것이 이 편의 설계다. 이 샷은 **재는 그림**이고
   keystone 은 **떠받치는 그림**이다. 한 샷 안에서 재고 얹으면 착지가 첫 10초 안에
   다 소모되고, 남은 세 샷이 뒷이야기가 되어 버린다.

   🔴 단위를 화면에서 지우지 마라. 축 이름은 「포만감 소비」이고 값이 클수록 탑이 높다 —
   「포만감 3 / 2」로 줄이면 3 > 2 라 날 것이 더 좋아 보이고 사실관계가 뒤집힌다.
   그래서 accent(정답 표시)는 날 것이 아니라 **조리** 쪽 탑에 붙는다.

   계속 도는 것 = 쌓인 칸 안을 위로 흐르는 빗금(자막 0부터 — 소비되는 양을 뜻한다),
   차이 한 칸의 테두리를 도는 표식(자막 1부터), 기둥 끝 표식의 맥박(자막 2부터). */

const FLOW = 2.1;
const ORBIT = 3.0;
const TIP = 1.6;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));

    const vk = ease(cue(spec.valueCue ?? 0, 0.15, 0.60));   // 두 탑이 쌓인다
    const gk = ease(cue(spec.gapCue ?? 1, 0.15, 0.60));     // 차이 한 칸이 물린다
    const rk = ease(cue(spec.riseCue ?? 2, 0.15, 0.55));    // 기둥이 솟는다

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const BASE = 250, BH = 34, BW = 60;
    const lx = 84, rx = 168;
    const lcx = lx + BW / 2;
    const raw = spec.raw || { label: '날 것', v: 3 };
    const cooked = spec.cooked || { label: '조리', v: 2 };

    /* ── 축 이름 — 이 화면이 재는 것이 무엇인지 ───── */
    if (vk > 0.02 && spec.axisLabel) {
      ctx.globalAlpha = clamp(vk * 2.4);
      ctx.textAlign = 'left';
      const fs = fit(spec.axisLabel, 800, 11, 150, 8);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.axisLabel, 14, 26);
      ctx.globalAlpha = 1;
    }

    /* ── 블록 탑 ──────────────────────────────────── */
    const tower = (x, n, prog, hot, label, value) => {
      const top = BASE - n * BH;
      const col = hot ? tone('accent') : tone('ink');

      /* 계속 도는 것 — 쌓인 칸 안을 위로 흐르는 빗금.
         자막 0 에서 이미 도므로 이 샷은 첫 구간부터 화면이 산다.
         (도는 것을 전부 뒤쪽 자막에 걸어 두면 자막 0 구간이 통째로 멈춘다 —
          같은 결함으로 반려된 전례가 있어 여기서는 값이 쌓이는 순간부터 흘린다.) */
      if (prog > 0.05) {
        ctx.save();
        ctx.beginPath();
        ctx.rect(x + 2, top + 2, BW - 4, BASE - top - 4);
        ctx.clip();
        ctx.strokeStyle = col; ctx.lineWidth = 3;
        ctx.globalAlpha = clamp(prog * 1.8) * 0.28;
        const off = frac(t / FLOW) * 22;
        for (let hy = BASE + 22 - off; hy > top - 24; hy -= 22) {
          ctx.beginPath();
          ctx.moveTo(x - 6, hy); ctx.lineTo(x + BW + 6, hy - 13);
          ctx.stroke();
        }
        ctx.restore();
        ctx.globalAlpha = 1;
      }

      for (let i = 0; i < n; i++) {
        const born = clamp(prog * (n + 0.8) - i);
        if (born < 0.02) continue;
        const y = BASE - (i + 1) * BH;
        const s = Math.min(1, spring(clamp(born * 1.2)));
        ctx.globalAlpha = clamp(born * 1.8);
        ctx.lineWidth = 3;
        ctx.strokeStyle = col;
        roundRect(ctx, x + (BW - BW * s) / 2, y + (BH - BH * s) / 2 + 2,
                  BW * s, (BH - 4) * s, 3);
        ctx.stroke();
        ctx.globalAlpha = 1;
      }

      if (prog > 0.6) {
        const k = clamp((prog - 0.6) / 0.4);
        ctx.globalAlpha = k;
        ctx.textAlign = 'center';
        ctx.font = mono(700, 18);
        ctx.fillStyle = col;
        ctx.fillText(String(value), x + BW / 2, top + BH / 2 + 7);
        const fs = fit(label, 800, 13, BW + 24, 9);
        ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
        ctx.fillText(label, x + BW / 2, 276);
        ctx.globalAlpha = 1;
      }
      return top;
    };

    /* 🔴 조리 쪽이 accent 다. 값이 작은 쪽이 이득이라는 것을 색으로 말한다 —
       높이만 보면 날 것이 커서 좋아 보이므로 색이 그 오독을 막는다. */
    const ltop = tower(lx, raw.v ?? 3, vk, false, raw.label, raw.v ?? 3);
    const rtop = tower(rx, cooked.v ?? 2, vk, gk > 0.3, cooked.label, cooked.v ?? 2);

    /* ── 차이 한 칸 ───────────────────────────────── */
    if (gk > 0.02) {
      const bp = ease(clamp(gk / 0.45));            // 괄호가 물리는 진행 (gk 0.45 에서 완성)
      const bx = 232;
      const mid = (ltop + rtop) / 2;                // 가운데서 한 칸 높이로 벌어진다
      ctx.globalAlpha = bp;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      const y0 = lerp(mid, ltop, bp), y1 = lerp(mid, rtop, bp);
      ctx.beginPath();
      ctx.moveTo(bx - 9, y0); ctx.lineTo(bx, y0);
      ctx.lineTo(bx, y1); ctx.lineTo(bx - 9, y1);
      ctx.stroke();

      if (bp > 0.55) {
        const k = clamp((bp - 0.55) / 0.45);
        ctx.globalAlpha = k;
        ctx.textAlign = 'left';
        const label = spec.gapLabel || '차이 1';
        const fs = fit(label, 900, 15, w - bx - 16, 10);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
        ctx.fillText(label, bx + 8, mid + 5);
      }
      ctx.globalAlpha = 1;

      /* 계속 도는 것 — 차이 한 칸의 테두리를 도는 표식 */
      const pw = BW, ph = rtop - ltop, px = lx, py = ltop;
      const per = 2 * (pw + ph);
      let d = frac(t / ORBIT) * per, mx = px, my = py;
      if (d < pw) { mx = px + d; my = py; }
      else if (d < pw + ph) { mx = px + pw; my = py + (d - pw); }
      else if (d < 2 * pw + ph) { mx = px + pw - (d - pw - ph); my = py + ph; }
      else { mx = px; my = py + ph - (d - 2 * pw - ph); }
      ctx.globalAlpha = bp;
      setShadow(ctx, GLOW, 10, 0);
      ctx.fillStyle = tone('accent');
      ctx.beginPath(); ctx.arc(mx, my, 4, 0, Math.PI * 2); ctx.fill();
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    /* ── 기둥 — 그 한 칸에서 솟는데 꼭대기는 비어 있다 ── */
    if (rk > 0.02) {
      const k = ease(clamp(rk / 0.7));
      const TOP_Y = 44;
      const top = lerp(ltop, TOP_Y, k);
      ctx.globalAlpha = clamp(rk * 2.2);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      setShadow(ctx, GLOW, 10, 0);
      ctx.beginPath();
      ctx.moveTo(lcx, ltop); ctx.lineTo(lcx, top);
      ctx.stroke();
      clearShadow(ctx);

      /* 계속 도는 것 — 기둥 끝 표식의 맥박. 여기 무언가 얹힐 자리라는 신호이고,
         무엇이 얹히는지는 이 편의 마지막 샷이 갚는다. */
      const pulse = 4.2 + 1.6 * Math.sin(frac(t / TIP) * Math.PI * 2);
      setShadow(ctx, GLOW, 12, 0);
      ctx.fillStyle = tone('accent');
      ctx.beginPath(); ctx.arc(lcx, top, pulse * k, 0, Math.PI * 2); ctx.fill();
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
