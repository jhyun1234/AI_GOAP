import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* sprout — 밭이 화면에서 실제로 자란다.

   이랑 셋에 밀이 서 있고 씨앗 → 자라는 중 → 수확 가능 → 수확 순서로 계속 자란다.
   셋의 위상을 1/3 씩 어긋내 두었으므로 밭이 한꺼번에 비는 순간이 없고, 구간이
   1/3 보다 넓은 '수확 가능'(u ∈ [0.60, 0.94), 길이 0.34)은 어느 t 에서도 최소 한
   포기가 그 안에 있다.

   🔴 1차 검수 정정(C-1): 처음 주석은 "어느 순간에 멈춰도 세 단계가 동시에 보인다"고
   적었는데 틀렸다. '씨앗'(길이 0.28)과 '자라는 중'(길이 0.32)은 간격 1/3 = 0.3333 보다
   좁아서 각각 시간의 16.0% · 4.0% 동안 0 포기다. 그래도 이 순환은 자막에 물릴 사건이
   아니다 — 어떤 단계도 '처음 등장'하지 않고 계속 순환할 뿐이라 도착 시각이라는 것이
   없기 때문이다. 위 밭은 배경이고, 세 단계에 **이름을 붙이는 것은 아래 단계 띠**다.
   띠의 세 칸은 고정 위상 u = 0.10 · 0.44 · 0.74 로 세 단계를 항상 그려 두므로
   자막(stageCue)이 이름을 부르는 순간 셋이 반드시 화면에 함께 있다.
   문턱 0.28 을 0.34 로 올려 씨앗 구간도 1/3 을 넘기는 안은 쓰지 않았다 — 그러면
   '자라는 중'이 0.26 으로 줄어 이번엔 그쪽이 더 오래 비고, 밀의 모양도 씨앗에 치우친다.

   원문 3절의 "Kenmi 밀 스프라이트 3단계(씨앗→자라는 중→수확 가능)"와 FarmService 다.
   이 시리즈에 아직 없던 그림이라 다른 회차와 부딪힐 자리가 없다.

   계속 도는 것 = 밀 셋의 성장 순환과 잎의 흔들림. */

const GROW = 5.4;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));

    const gk = ease(cue(spec.growCue ?? 0, 0.15, 0.6));
    const sk = ease(cue(spec.stageCue ?? 1, 0.15, 0.62));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    /* 한 포기를 그린다. u = 0~1 성장 위상, sc = 크기 배율 */
    const plant = (cx, ground, u, alpha, sc) => {
      const hk = u < 0.86 ? u / 0.86 : 1 - (u - 0.86) / 0.14;
      const H = 76 * ease(clamp(hk)) * sc;
      const ripe = u >= 0.60 && u < 0.94;
      const sprouted = u >= 0.28;
      const col = ripe ? tone('accent') : tone('ink');
      const sway = 3 * sc * Math.sin(frac(t / 2.2) * Math.PI * 2 + cx * 0.05);

      ctx.globalAlpha = alpha;
      if (!sprouted) {
        // 씨앗 — 흙 바로 위의 낟알 하나
        ctx.fillStyle = col;
        ctx.beginPath();
        ctx.ellipse(cx, ground - 6 * sc, 4.5 * sc, 6 * sc, 0, 0, Math.PI * 2);
        ctx.fill();
        ctx.globalAlpha = 1;
        return;
      }

      // 줄기
      ctx.strokeStyle = col; ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(cx, ground);
      ctx.quadraticCurveTo(cx + sway * 0.5, ground - H * 0.55, cx + sway, ground - H);
      ctx.stroke();

      // 잎 둘
      [0.42, 0.66].forEach((f, i) => {
        const y = ground - H * f, x = cx + sway * f * 0.7;
        const dir = i ? 1 : -1;
        ctx.beginPath();
        ctx.moveTo(x, y);
        ctx.quadraticCurveTo(x + dir * 13 * sc, y - 4 * sc, x + dir * 17 * sc, y + 5 * sc);
        ctx.stroke();
      });

      // 이삭 — 수확 가능일 때 굵어진다
      if (u >= 0.46) {
        const rk = clamp((u - 0.46) / 0.16);
        const tipX = cx + sway, tipY = ground - H;
        ctx.lineWidth = 3;
        for (let i = 0; i < 3; i++) {
          const yy = tipY + 4 * sc + i * 6 * sc;
          const ww = (5 + 4 * rk) * sc;
          ctx.beginPath();
          ctx.moveTo(tipX, yy); ctx.lineTo(tipX - ww, yy + 4 * sc);
          ctx.moveTo(tipX, yy); ctx.lineTo(tipX + ww, yy + 4 * sc);
          ctx.stroke();
        }
      }
      ctx.globalAlpha = 1;
    };

    /* ── FarmService · 스프라이트 출처 ────────────── */
    {
      const k = clamp(gk * 2);
      ctx.globalAlpha = k;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
      roundRect(ctx, 12, 8, 128, 25, 3); ctx.stroke();
      ctx.textAlign = 'left';
      ctx.font = mono(700, 11); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.service || 'FarmService', 22, 25);

      if (spec.sprite) {
        ctx.textAlign = 'right';
        const fs = fit(spec.sprite, 700, 10.5, w - 160, 8);
        ctx.font = disp(700, fs); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.sprite, w - 12, 25);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 밭 ───────────────────────────────────────── */
    const GROUND = 172;
    if (gk > 0.05) {
      const k = clamp(gk * 1.8);
      ctx.globalAlpha = k;
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(24, GROUND); ctx.lineTo(w - 24, GROUND); ctx.stroke();
      // 이랑 자국
      ctx.globalAlpha = k * 0.5;
      ctx.strokeStyle = tone('track');
      for (let i = 0; i < 2; i++) {
        const y = GROUND + 8 + i * 8;
        ctx.beginPath(); ctx.moveTo(34 + i * 8, y); ctx.lineTo(w - 34 - i * 8, y); ctx.stroke();
      }
      ctx.globalAlpha = 1;

      [88, 176, 264].forEach((cx, i) => {
        plant(cx, GROUND, frac(t / GROW + i / 3), k, 1);
      });
    }

    /* ── 단계 띠 ──────────────────────────────────── */
    const stages = spec.stages || [];
    if (sk > 0.02 && stages.length) {
      const cw = 100, gap = 14;
      const total = stages.length * cw + (stages.length - 1) * gap;
      const sx = (w - total) / 2;
      const cy = 206, chh = 52;

      stages.forEach((name, i) => {
        const born = clamp(sk * (stages.length + 0.7) - i);
        if (born < 0.02) return;
        const x = sx + i * (cw + gap);
        const s = Math.min(1, spring(clamp(born)));
        ctx.globalAlpha = clamp(born * 1.8);
        ctx.lineWidth = 3;
        ctx.strokeStyle = i === stages.length - 1 ? tone('accent') : tone('sub');
        roundRect(ctx, x + (cw - cw * s) / 2, cy + (chh - chh * s) / 2, cw * s, chh * s, 3);
        ctx.stroke();

        // 그 단계의 모양을 작게 한 번 더 — 칸마다 위상을 고정해 둔다(항상 세 단계가 보인다)
        const u = [0.10, 0.44, 0.74][i] ?? 0.5;
        plant(x + cw / 2, cy + chh - 8, u, clamp(born * 1.8), 0.42);

        ctx.textAlign = 'center';
        const fs = fit(name, 800, 12, cw - 12, 8.5);
        ctx.font = disp(800, fs);
        ctx.fillStyle = i === stages.length - 1 ? tone('accent') : tone('ink');
        ctx.fillText(name, x + cw / 2, cy + chh + 22);
        ctx.globalAlpha = 1;

        if (i < stages.length - 1) {
          const ak = clamp(sk * (stages.length + 0.7) - i - 0.5);
          if (ak > 0.02) {
            const ax = x + cw + 2, ay = cy + chh / 2;
            ctx.globalAlpha = clamp(ak * 1.6);
            ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
            const len = 10 * ak;
            ctx.beginPath();
            ctx.moveTo(ax, ay); ctx.lineTo(ax + len, ay);
            ctx.moveTo(ax + len, ay); ctx.lineTo(ax + len - 4, ay - 4);
            ctx.moveTo(ax + len, ay); ctx.lineTo(ax + len - 4, ay + 4);
            ctx.stroke();
            ctx.globalAlpha = 1;
          }
        }
      });
    }

    ctx.textAlign = 'left';
  }
};
