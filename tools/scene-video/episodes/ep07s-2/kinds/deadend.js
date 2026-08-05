import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* deadend — 뚫려 있는데 못 간다.

   원문: "대각선으로 비스듬히 놓인 건물들 사이에서, 주민이 분명히 갈 수 있어 보이는 길을
   못 찾는 상황이 눈에 띄었습니다."

   🔴 **벽을 그리면 안 되는 샷이다.** 못 가는 이유가 지형이 아니라 코드이기 때문이다.
   그래서 두 건물 사이의 대각선 통로는 **끝까지 뚫린 채로** 보이고(diagCue 에서 강조색
   점선으로 확인시킨다), 막히는 것은 **탐색선** 쪽이다 — 주민에게서 뻗어 나가는 파동이
   통로 입구에서 매번 끊긴다.

   🔴 파동이 t 로 반복되는 것이 이 샷의 뜻이다. 한 번 실패하고 마는 게 아니라 계속
   시도하는데 계속 못 간다. 다만 **'언제 처음 뜨는가'는 cue(0)이 정한다** — 반복 자체는
   사건이 아니라 상태다.

   🔴 앞편(ep07s-1)과 겹치지 않게: 저 편은 여럿이 한 점으로 모이거나 흩어지는 그림이었고
   여기는 **하나가 한 자리에서 못 넘어가는** 그림이다. 세는 대상이 아예 없다. */

const PULSE = 1.9;   // 탐색 파동 주기(초)

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

    const mk = ease(cue(spec.marchCue ?? 0, 0.15, 0.7));
    const bk = ease(cue(spec.diagCue ?? 1, 0.15, 0.7));

    /* ── 상단 라벨 둘 ──────────────────────────────── */
    ctx.font = disp(800, 12.5);
    ctx.textAlign = 'left'; ctx.fillStyle = tone('sub');
    if (spec.sysLabel) ctx.fillText(spec.sysLabel, 8, 28);
    if (spec.fixedLabel) {
      ctx.globalAlpha = clamp(mk * 2.2); ctx.textAlign = 'right';
      ctx.fillText(spec.fixedLabel, w - 8, 28); ctx.globalAlpha = 1;
    }

    /* ── 비스듬히 어긋난 건물 둘 ───────────────────── */
    const CX = w / 2, CY = 176;
    const BW = 96, BH = 62, TILT = 0.30;          // 라디안

    const drawBlock = (ox, oy, a) => {
      ctx.save();
      ctx.translate(CX + ox, CY + oy); ctx.rotate(a);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.rect(-BW / 2, -BH / 2, BW, BH); ctx.stroke();
      ctx.restore();
    };
    const ak = clamp(mk * 3);
    if (ak > 0.02) {
      ctx.globalAlpha = ak;
      drawBlock(-62, -44, TILT);
      drawBlock(64, 46, TILT);
      ctx.globalAlpha = 1;
    }

    /* ── 두 건물 사이의 대각선 통로 — 뚫려 있다 ─────── */
    const GX0 = CX - 46, GY0 = CY + 34, GX1 = CX + 44, GY1 = CY - 36;
    if (bk > 0.02) {
      ctx.globalAlpha = bk;
      ctx.setLineDash([10, 8]);
      ctx.lineDashOffset = -(t * 14) % 18;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.beginPath(); ctx.moveTo(GX0, GY0); ctx.lineTo(GX1, GY1); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;

      if (spec.gapNote) {
        ctx.textAlign = 'center';
        /* 🔴 통로 끝보다 위·오른쪽으로 물렸다. 처음엔 (CX+62, CY−54) 였는데
           통로의 마지막 점선이 글자 왼쪽을 가로질렀다(스틸 실측). */
        const fs = fit(spec.gapNote, 900, 13.5, 124, 9);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
        ctx.fillText(spec.gapNote, CX + 80, CY - 66);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 주민 — 통로 입구까지만 간다 ───────────────── */
    const SX = 34, SY = CY + 76;
    const march = ease(clamp(mk * 1.6));
    const px = lerp(SX, GX0 - 12, march), py = lerp(SY, GY0 + 10, march);

    /* 탐색 파동 — 입구에서 매번 끊긴다 (계속 돈다) */
    /* 🔴 처음엔 이 파동을 track 색(알파 0.12)으로 그렸다. 눈으로는 보였지만
       check.mjs 의 정적 검사는 밝기에 알파를 곱해서 재므로 **움직임으로 안 잡혔고**
       S1 이 정적 4.8초로 걸렸다. 이 샷에서 계속 움직이는 것은 이 파동뿐이라,
       색과 두께와 개수를 올려 실제로 면적을 갖게 했다. */
    if (mk > 0.1) {
      for (let s = 0; s < 3; s++) {
        const u = frac(t / PULSE + s / 3);
        const reach = u * 92;
        const a = (1 - u) * 0.6 * clamp(mk * 2);
        if (a <= 0.02) continue;
        ctx.globalAlpha = a;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
        ctx.beginPath();
        ctx.arc(px, py, 13 + reach, -1.25, 0.30);
        ctx.stroke();
        ctx.globalAlpha = 1;
      }
    }

    setShadow(ctx, GLOW, 7, 0);
    ctx.fillStyle = tone('accent');
    ctx.beginPath(); ctx.arc(px, py, 6.5, 0, Math.PI * 2); ctx.fill();
    clearShadow(ctx);

    /* 막힌 자리 — 입구 앞 짧은 빗장 하나 (벽이 아니라 '여기서 멈춘다'는 표시) */
    const stop = clamp((march - 0.82) / 0.18);
    if (stop > 0.02) {
      ctx.globalAlpha = stop;
      /* 🔴 통로 방향에 **수직**인 빗장을 입구 한가운데 세운다. 처음엔 통로와 나란한
         선이라 표식을 꿰뚫고 지나가는 것처럼 보였다(스틸 실측) — 막는 것이 아니라
         찌르는 그림이 됐다. */
      const dx = GX1 - GX0, dy = GY1 - GY0;
      const len = Math.hypot(dx, dy) || 1;
      const nx = -dy / len, ny = dx / len;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.moveTo(GX0 - nx * 16, GY0 - ny * 16);
      ctx.lineTo(GX0 + nx * 16, GY0 + ny * 16);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
