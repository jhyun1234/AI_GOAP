import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* turncheck — 같은 길을 검사 켠 채로, 그리고 끈 채로 두 번 보여준다.

   원문: "장애물 옆을 지날 때 '여기서 꺾어야 하나?'를 검사하는 장치가 있습니다."
   그리고: "하필 건물이 대각선으로 배치됐을 때만 길이 사라졌던 거죠."

   checkCue 에서 장애물 옆에 검사 고리가 켜지고 거기서 꺾는 갈래가 하나 돋는다.
   voidCue 에서 그 고리를 끄면 갈래가 접히고 **경로가 통째로 끊긴다** — 원문의
   "길이 사라졌다"가 이 동작이다.

   🔴 앞편(ep07s-1)의 sidechannel 과 겹치지 않게: 저 편은 갈래 둘 중 **안 간 쪽**을
   점선으로 그렸다(가정법이라 그래야 했다). 여기는 실제로 일어난 일이므로 점선을 안 쓰고,
   **같은 갈래 하나를 켰다 껐다** 한다. 가정이 아니라 시연이다.

   🔴 등장은 전부 cue 에 물렸다. t 로 도는 것은 경로 위 표식의 흐름과 고리 맥동뿐이다. */

const FLOW = 2.2;    // 경로 표식 주기(초)
const RING = 3.0;    // 검사 고리 맥동 주기(초)

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const fit = (txt, weight, start, max, min = 8) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const ck = ease(cue(spec.checkCue ?? 0, 0.15, 0.7));
    const vk = ease(cue(spec.voidCue ?? 1, 0.15, 0.75));

    const Y = 150, X0 = 30, X1 = w - 30;
    const OX = X0 + (X1 - X0) * 0.52;          // 장애물 자리
    const on = 1 - clamp(vk * 1.6);            // 검사가 켜져 있는 정도

    /* ── 장애물 ────────────────────────────────────── */
    const ok = clamp(ck * 3);
    if (ok > 0.02) {
      ctx.globalAlpha = ok;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.rect(OX - 22, Y + 14, 44, 40); ctx.stroke();
      ctx.textAlign = 'center';
      ctx.font = disp(800, 11.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.obstacleLabel ?? 'OBSTACLE', OX, Y + 72);
      ctx.globalAlpha = 1;
    }

    /* ── 경로 — 검사가 꺼지면 장애물 뒤가 끊긴다 ────── */
    const cut = OX + 6;
    const endX = lerp(X1, cut, 1 - on);
    ctx.globalAlpha = clamp(ck * 2.4);
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(X0, Y); ctx.lineTo(endX, Y); ctx.stroke();
    ctx.globalAlpha = 1;

    /* 끊긴 자리 표시 — 선이 사라진 쪽은 아무것도 없다 */
    if (on < 0.85) {
      ctx.globalAlpha = (0.85 - on) / 0.85;
      ctx.setLineDash([3, 9]);
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(cut, Y); ctx.lineTo(X1, Y); ctx.stroke();
      ctx.setLineDash([]);
      ctx.globalAlpha = 1;
    }

    /* ── 검사 고리와 꺾는 갈래 ─────────────────────── */
    if (ck > 0.05) {
      const a = clamp(ck * 2) * on;
      if (a > 0.02) {
        const pulse = 1 + 0.14 * Math.sin(frac(t / RING) * Math.PI * 2);
        ctx.globalAlpha = a;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
        ctx.beginPath(); ctx.arc(OX, Y, 15 * pulse, 0, Math.PI * 2); ctx.stroke();

        // 꺾는 갈래 — 위로 돋는다
        const br = ease(clamp(ck * 1.8 - 0.35)) * on;
        if (br > 0.02) {
          ctx.globalAlpha = a * br;
          ctx.beginPath();
          ctx.moveTo(OX, Y);
          ctx.lineTo(OX + 34 * br, Y - 44 * br);
          ctx.stroke();
        }
        ctx.globalAlpha = 1;
      }
    }

    /* ── 검사 이름과 원문 인용 ─────────────────────── */
    if (spec.checkLabel) {
      ctx.globalAlpha = clamp(ck * 2.4) * Math.max(on, 0.35);
      ctx.textAlign = 'center';
      ctx.font = disp(800, 12.5);
      ctx.fillStyle = on > 0.5 ? tone('accent') : tone('sub');
      ctx.fillText(spec.checkLabel, OX, Y - 74);
      ctx.globalAlpha = 1;
    }
    if (spec.askText) {
      const k = clamp(ck * 2 - 0.6) * Math.max(on, 0.3);
      if (k > 0.02) {
        ctx.globalAlpha = k;
        ctx.textAlign = 'center';
        const fs = fit(spec.askText, 900, 14, w - 26, 10);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('ink');
        ctx.fillText(spec.askText, w / 2, 264);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 계속 도는 것 — 경로 위 표식 ───────────────── */
    if (ck > 0.1) {
      for (let s = 0; s < 3; s++) {
        const u = frac(t / FLOW + s / 3);
        const x = X0 + (X1 - X0) * u;
        if (x > endX) continue;                       // 끊긴 뒤로는 못 간다
        ctx.globalAlpha = clamp(ck * 2) * (1 - Math.abs(u - 0.5) * 0.5);
        setShadow(ctx, GLOW, 6, 0);
        ctx.fillStyle = tone('accent');
        ctx.beginPath(); ctx.arc(x, Y, 5, 0, Math.PI * 2); ctx.fill();
        clearShadow(ctx);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
