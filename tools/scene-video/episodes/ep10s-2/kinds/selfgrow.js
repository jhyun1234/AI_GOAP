import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* selfgrow — 비워 준 자리 아래로 계획이 스스로 자란다.

   🔴 이 그림은 askover 를 **뒤집어 놓은 것**이고, 그게 이 편 전부다.
      askover 에서 각진 흰 상자는 **위에서 아래로** 내려와 매달렸다(내가 얹었다).
      여기서 둥근 강조색 마디는 **아래에서 위로** 돋아 칸까지 닿는다(스스로 짰다).
      같은 칸, 같은 축, 반대 방향. 라벨을 안 읽어도 누가 짰는지가 갈린다.

   자라는 순서는 원문이 정했다 — '재료가 없을 때 **나무를 하러 가는 것부터** 계획을
   스스로 짜는 게 원래 GOAP이 하는 일'. 그래서 맨 아래 CHOP 이 먼저 생기고 WOOD,
   HOUSE 순으로 올라간다. 역방향 탐색을 설명하는 그림이 아니라 **실행 순서** 그대로다.

   🔴 마디 개수는 뜻이 아니다. 원문에 있는 이름 셋(나무를 하러 감 · 재료 = 나무 · 집)을
      그대로 옮겼을 뿐이고, 「몇 단계짜리 계획인가」는 원문에 없어 주장하지 않는다.

   🔴 계속 도는 것 = 사슬을 타고 올라가는 점 하나 · 마디의 링 점선 · 칸 테두리.
      전부 문턱이 없다. */

const RUN = 2.4;   // 점 하나가 사슬을 타고 올라가는 주기(초)

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

    const c0 = cue(spec.growCue ?? 0, 0.15, 0.82);
    const CX = w / 2;
    const GX = 44, GW = w - 88, GY = 26, GH = 40;          // 목표 칸(ACCEPT 가 앉아 있다)
    const NW = 130, NX = (w - NW) / 2, NH = 30;            // 사슬 마디
    const chain = spec.chain ?? [];
    const nodeY = i => h - 46 - i * 62;                    // i=0 이 맨 아래

    /* ── 상단 라벨 ────────────────────────────────── */
    if (spec.label) {
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.label, 10, GY - 8);
    }

    /* ── 목표 칸 — 이미 앉아 있는 신호 ────────────── */
    const gk = ease(clamp(c0 * 4));
    ctx.save();
    ctx.globalAlpha = gk;
    ctx.setLineDash([10, 8]);
    ctx.lineDashOffset = -(t * 13) % 18;
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
    roundRect(ctx, GX, GY, GW, GH, 13); ctx.stroke();
    ctx.restore();

    if (spec.slot) {
      ctx.globalAlpha = gk;
      ctx.textAlign = 'center';
      const fs = fit(spec.slot, 900, 19, GW - 26, 11);
      ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
      setShadow(ctx, GLOW, 9, 0);
      ctx.fillText(spec.slot, CX, GY + GH / 2 + fs * 0.36);
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    /* ── 사슬 — 맨 아래부터 스스로 돋는다 ─────────── */
    const grow = i => ease(clamp((c0 - 0.10 - i * 0.24) / 0.30));

    for (let i = 0; i < chain.length; i++) {
      const k = grow(i);
      if (k <= 0.02) continue;
      const y = nodeY(i);

      // 이 마디에서 위로 뻗는 줄기 — 다음 마디(또는 칸)까지
      const topOf = i === chain.length - 1 ? GY + GH : nodeY(i + 1) + NH;
      const stem = ease(clamp((k - 0.55) / 0.45));
      if (stem > 0.02) {
        ctx.save();
        ctx.globalAlpha = stem;
        ctx.setLineDash([10, 7]);
        ctx.lineDashOffset = (t * 18) % 17;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
        ctx.beginPath();
        ctx.moveTo(CX, y);
        ctx.lineTo(CX, lerp(y, topOf, stem));
        ctx.stroke();
        ctx.restore();
      }

      // 마디 — 둥글다. GOAP 이 낸 것이다.
      const sc = lerp(0.82, 1, ease(k));
      const nw = NW * sc, nh = NH * sc;
      ctx.globalAlpha = clamp(k * 1.6);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      setShadow(ctx, GLOW, 9, 0);
      roundRect(ctx, CX - nw / 2, y, nw, nh, nh / 2); ctx.stroke();
      clearShadow(ctx);

      ctx.textAlign = 'center';
      const fs = fit(chain[i], 900, 17, nw - 22, 10);
      ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
      ctx.fillText(chain[i], CX, y + nh / 2 + fs * 0.36);
      ctx.globalAlpha = 1;
    }

    /* ── 사슬을 타고 올라가는 점 하나 — 계속 돈다 ─── */
    if (chain.length) {
      const y0 = nodeY(0), y1 = GY + GH;
      const u = frac(t / RUN);
      const py = lerp(y0, y1, u);
      ctx.globalAlpha = Math.sin(Math.PI * u) * 0.9;
      ctx.fillStyle = tone('accent');
      setShadow(ctx, GLOW, 8, 0);
      ctx.beginPath(); ctx.arc(CX, py, 4.5, 0, Math.PI * 2); ctx.fill();
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
