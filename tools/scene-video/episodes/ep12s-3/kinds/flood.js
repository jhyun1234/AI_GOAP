import {
  disp, mono, ease, clamp, lerp, spring,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* flood — 내려온 것과, 거기 없던 것.

   원문: "새 재해 '홍수'를 추가하는데, 코드는 한 줄도 안 쓰고 에셋 파일 하나만 만들어서 목록에
   등록하는 겁니다." / "그리고 게임을 돌렸는데, 로그에 이런 게 찍혔습니다. '홍수 발동 — 작물 0개
   소실'" / "아니, 0개요? 다시 돌려도 0개."

   🔴 이 회차의 **기본 도형은 「밭 칸 한 줄」** 이다. 이 글의 사건은 전부 그 칸 안에 무엇이
   있는가(작물) 와 칸 자체가 어떻게 되는가(시설) 사이에서 벌어진다. 네 샷이 같은 칸 줄을
   물려받아 각각 다른 일을 한다 — 여기서는 **칸 위에 무언가가 내려앉는다.**

   ① 위에서 강조색 물띠가 격자 바로 위까지 내려온다. 내려오는 동안 물결이 크게 일렁이다가
      착지하는 순간 진폭이 0 이 되어 **격자 위에 얹힌 띠**가 된다. 착지에 맞춰 칸들이 한 번
      눌렸다 되돌아온다 — 홍수는 분명히 왔다.
   ② 그런데 칸 안은 처음부터 **점선 빈 자리**뿐이다. 태울 것이 없다.
   ③ 아래 로그 카드에 같은 줄이 두 번 찍히고, 두 번 다 오른쪽 숫자가 `0` 이다.

   🔴 물띠는 격자를 **통과하지 않는다.** 강조색이 흰 칸 위를 지나가면 같은 픽셀에서 두 색이
   겹치는데, 팔레트 게이트는 그 혼합을 hue 150~152 로 읽어 어느 순서든 통과시킨다 —
   즉 게이트 통과가 근거가 되지 못하는 자리다. 그래서 좌표 단계에서 갈라 놓았다:
   물띠 y ≤ 146 · 격자 y ≥ 150 · 로그 카드 y ≥ 214.

   🔴 화면에 숫자는 `0` 하나뿐이다. 원문이 이 대목에서 부르는 수가 그것뿐이기 때문이다.
   밭 개수는 원문에 없으므로 **칸 수를 글자로 적지 않았고**, 양 끝 칸을 흐리게 그려
   "여기서 끊긴 게 아니라 이어진다"로 읽히게 했다.

   계속 도는 것 = 물띠 세 줄의 대시 흐름(t) · 빈 자리 점선의 회전(t). */

const GRID_Y = 150, CELL_W = 56, CELL_H = 46, CELL_GAP = 8, NCELL = 5;
const BAND_TOP0 = 30, BAND_H = 15;
const CARD_Y = 214, CARD_H = 76;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const fit = (txt, weight, start, max, min = 8, f = disp) => {
      let fs = start; ctx.font = f(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = f(weight, fs); }
      return fs;
    };

    const c0 = cue(spec.floodCue ?? 0, 0.15, 0.90);
    const c1 = cue(spec.zeroCue ?? 1, 0.15, 0.90);

    /* 착지가 c0 = 0.80 이다 — 효과음(thud)의 자리도 여기다 */
    const fall = ease(clamp((c0 - 0.12) / 0.68));
    const hit = clamp((c0 - 0.80) / 0.20);
    const jolt = hit > 0 ? Math.sin(hit * Math.PI * 3) * (1 - hit) * 5 : 0;

    const gridW = NCELL * CELL_W + (NCELL - 1) * CELL_GAP;
    const gx0 = (w - gridW) / 2;
    const bandTop = lerp(BAND_TOP0, GRID_Y - BAND_H - 4, fall);
    const amp = lerp(9, 0, fall);

    /* ── 라벨 ──────────────────────────────────────── */
    /* 콜드 오픈 — 0초 프레임에서 이미 물띠가 보여야 한다. 0.12 로 좁게 잡은 이유다. */
    const lab = ease(clamp(c0 / 0.12));
    if (lab > 0.01 && spec.floodLabel) {
      ctx.globalAlpha = lab;
      ctx.textAlign = 'left';
      ctx.font = disp(900, fit(spec.floodLabel, 900, 16, 140));
      ctx.fillStyle = tone('accent');
      ctx.fillText(spec.floodLabel, 20, 22);
      const lw = ctx.measureText(spec.floodLabel).width;
      /* 🔑 이 회차의 아이러니를 자막을 안 쓰고 화면에 세우는 자리다. 위에 `.CS DIFF 0`
         (코드 0줄로 넣은 성공), 아래 로그에 `CROPS LOST 0`(아무것도 못 때린 실패).
         같은 0 이 한 번은 자랑이고 한 번은 사고다. 나레이션은 아래 0 만 부른다. */
      if (spec.assetNote) {
        const x = 20 + lw + 12;
        ctx.globalAlpha = lab * 0.9;
        ctx.font = mono(700, fit(spec.assetNote, 700, 11, w - 20 - x, 8, mono));
        ctx.fillStyle = tone('sub');
        ctx.fillText(spec.assetNote, x, 22);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 물띠 ──────────────────────────────────────── */
    const bx0 = 20, bx1 = w - 20;
    ctx.save();
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineCap = 'butt';
    for (let r = 0; r < 3; r++) {
      ctx.globalAlpha = lab * (1 - r * 0.22);
      ctx.setLineDash([16, 10]);
      ctx.lineDashOffset = -((t * 46 + r * 9) % 26);
      ctx.beginPath();
      for (let x = bx0; x <= bx1; x += 5) {
        const k = (x - bx0) / (bx1 - bx0);
        const y = bandTop + 3 + r * 5
          + Math.sin(k * 7.5 + t * 2.3 + r * 1.05) * amp * (1 - r * 0.22);
        if (x === bx0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
      }
      ctx.stroke();
    }
    ctx.restore();

    /* ── 밭 칸 한 줄 (이 회차의 기본 도형) ──────────── */
    for (let i = 0; i < NCELL; i++) {
      const x = gx0 + i * (CELL_W + CELL_GAP);
      const edge = (i === 0 || i === NCELL - 1) ? 0.45 : 1;
      const cy = GRID_Y + jolt;

      ctx.globalAlpha = edge;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, x, cy, CELL_W, CELL_H, 6); ctx.stroke();

      /* 빈 작물 자리 — 있어야 할 것이 없다 */
      ctx.globalAlpha = edge * 0.55;
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.setLineDash([5, 6]);
      ctx.lineDashOffset = -((t * 11) % 11);
      ctx.beginPath();
      ctx.arc(x + CELL_W / 2, cy + CELL_H / 2, 10, 0, Math.PI * 2);
      ctx.stroke();
      ctx.setLineDash([]);
    }
    ctx.globalAlpha = 1;

    /* ── 로그 카드 ─────────────────────────────────── */
    const cardIn = ease(clamp(c1 / 0.18));
    if (cardIn > 0.01) {
      const cx0 = 16, cw = w - 32;
      ctx.globalAlpha = cardIn * 0.8;
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      roundRect(ctx, cx0, CARD_Y, cw, CARD_H, 8); ctx.stroke();
      ctx.globalAlpha = 1;

      const rows = [
        { run: spec.run1 ?? 'RUN 1', k: ease(clamp((c1 - 0.06) / 0.30)), y: CARD_Y + 30 },
        { run: spec.run2 ?? 'RUN 2', k: ease(clamp((c1 - 0.48) / 0.30)), y: CARD_Y + 62 }
      ];
      const msg = spec.logLabel ?? 'FLOOD: CROPS LOST';
      for (const r of rows) {
        if (r.k <= 0.01) continue;
        ctx.globalAlpha = clamp(r.k * 1.6);
        ctx.textAlign = 'left';
        ctx.font = mono(700, fit(r.run, 700, 11, 44, 8, mono));
        ctx.fillStyle = tone('sub');
        ctx.fillText(r.run, cx0 + 15, r.y);

        ctx.font = mono(700, fit(msg, 700, 13, cw - 132, 8, mono));
        ctx.fillStyle = tone('ink');
        ctx.fillText(msg, cx0 + 63, r.y);

        const sc = spring(r.k);
        ctx.save();
        ctx.translate(cx0 + cw - 26, r.y - 8);
        ctx.scale(sc, sc);
        ctx.textAlign = 'center';
        ctx.font = disp(900, 26);
        setShadow(ctx, GLOW, 14);
        ctx.fillStyle = tone('accent');
        ctx.fillText(spec.zero ?? '0', 0, 9);
        clearShadow(ctx);
        ctx.restore();
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
