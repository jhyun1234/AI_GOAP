import {
  disp, ease, clamp, lerp, spring,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* zero — 훅. 홍수는 분명히 지나갔고, 계수기는 0이다.

   4단 고정 구조 ②의 자리(ADR-V25-8). 이 편이 맡은 사건을 한 그림으로 **약속**한다.
   원문: "새 재해 '홍수'를 추가하는데, 코드는 한 줄도 안 쓰고…" / "'홍수 발동 — 작물 0개 소실'"
   / "재해는 분명히 시간 맞춰 발동하는데, 정작 태울 게 아무것도 없었습니다."

   🔴 **본문 S1(flood) 의 재방송이 되지 않게 세 축을 갈랐다.**
     ⓐ 홍수의 방향 — S1 은 물띠가 **위에서 내려와 칸 위에 앉는다**. 여기는 물마루가
        **왼쪽에서 오른쪽으로 통째로 지나가 화면 밖으로 빠진다.** 앉는 것과 지나가는 것.
     ⓑ 0 을 담는 그릇 — S1 은 로그 카드 두 줄(RUN 1 / RUN 2), 여기는 **계수기 하나.**
        훅은 「0이었다」만 약속하고 「한 번이 아니라 두 번이었다」는 S1 이 진다.
     ⓒ 순서 — 여기서는 **0 이 먼저 꽂히고 그 뒤에 홍수가 지나간다**(자막 어순이
        「작물 0개 태운 홍수」다). S1 은 반대다. 같은 재료로 반대 순서의 그림이 된다.

   ⏱ 자막 어순에 물렸다.  「작물」→ 칸 줄 · 「0개」→ 큰 0 · 「태운 홍수」→ 물마루 통과.

   🔴 세로 예산(캔버스 307). 흰 계열과 강조색이 같은 픽셀을 나눠 갖는 구간이 없다.
       FLOOD 라벨      ~ 24        (accent, 좌상단)
       물길 레인      52 ~  80     (accent · 세 줄 + 진폭 8)
       밭 칸 한 줄    92 ~ 134     (ink)          ← 물길 최저 80 과 12px
       가르는 점선      152         (track)
       CROPS LOST    168 ~ 181     (sub)
       계수기 테두리   190 ~ 262    (accent 점선)  ← 라벨 바닥 181 과 9px
   🔴 물마루는 칸을 통과하지 않는다. 강조색이 흰 칸 위를 지나면 혼합 픽셀이 생기는데
      팔레트 게이트는 그 혼합을 hue 150~152 로 읽어 **어느 순서든 통과시킨다** —
      게이트 통과가 근거가 못 되는 자리라 좌표 단계에서 레인을 갈랐다.

   🔴 화면의 수는 `0` 하나뿐이다. 칸 개수는 원문에 없으므로 **글자로 안 적고**, 양 끝 칸을
      흐리게 그려 "여기서 끊긴 게 아니라 이어진다"로 읽히게 했다.

   계속 도는 것 = 물길 세 줄의 대시 흐름과 파형 위상(t) · 빈 작물 자리 점선의 회전(t) ·
   계수기 테두리 점선의 흐름(t). 물마루가 빠져나간 뒤에도 셋이 화면 폭 전체에서 돈다. */

const BAND_BASE = 60, BAND_AMP = 8, BAND_ROWS = 3;
const GRID_Y = 92, CELL_W = 56, CELL_H = 42, CELL_GAP = 8;
const DIV_Y = 152;
const LAB_Y = 178;
const BOX_Y = 190, BOX_H = 72;
const CREST = 95;          // 물마루 반폭

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

    const c0 = cue(spec.hookCue ?? 0, 0.15, 0.85);

    /* 훅은 체류가 걸린 자리라 0초 프레임에서 이미 무대가 서 있어야 한다 */
    const st = ease(clamp(c0 / 0.14));
    const zk = ease(clamp((c0 - 0.52) / 0.16));   // 「0개」
    const lk = ease(clamp((c0 - 0.66) / 0.14));   // 잠긴다
    const sw = ease(clamp((c0 - 0.70) / 0.28));   // 「태운 홍수」 — 물마루가 지나간다

    const bx0 = 18, bx1 = w - 18;
    const waveY = (x, r) =>
      BAND_BASE + r * 6 + Math.sin((x - bx0) / 34 + t * 2.1 + r * 1.05) * BAND_AMP * (1 - r * 0.2);

    /* ── FLOOD 라벨 ────────────────────────────────── */
    if (spec.floodLabel) {
      ctx.globalAlpha = st;
      ctx.textAlign = 'left';
      ctx.font = disp(900, fit(spec.floodLabel, 900, 16, 140));
      ctx.fillStyle = tone('accent');
      ctx.fillText(spec.floodLabel, 20, 24);
      ctx.globalAlpha = 1;
    }

    /* ── 물길 : 늘 흐르는 바탕 ─────────────────────── */
    ctx.save();
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineCap = 'butt';
    for (let r = 0; r < BAND_ROWS; r++) {
      ctx.globalAlpha = st * 0.32 * (1 - r * 0.2);
      ctx.setLineDash([15, 11]);
      ctx.lineDashOffset = -((t * 40 + r * 8) % 26);
      ctx.beginPath();
      for (let x = bx0; x <= bx1; x += 5) {
        const y = waveY(x, r);
        if (x === bx0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
      }
      ctx.stroke();
    }
    ctx.restore();

    /* ── 물마루 : 왼쪽에서 오른쪽으로 통째로 지나간다 ──
       🔑 마루 양 끝의 진폭을 바탕 파형과 **같게** 맞춰 두어, 밝기만 갈리고 선은 이어진다.
          (clip 으로 잘라내면 그 경계가 하드 엣지로 읽힌다) */
    if (sw > 0.001 && sw < 0.999) {
      const pk = lerp(bx0 - CREST - 20, bx1 + CREST + 20, sw);
      ctx.save();
      setShadow(ctx, GLOW, 12);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineCap = 'butt';
      for (let r = 0; r < BAND_ROWS; r++) {
        ctx.globalAlpha = st * (1 - r * 0.18);
        ctx.setLineDash([15, 11]);
        ctx.lineDashOffset = -((t * 40 + r * 8) % 26);
        ctx.beginPath();
        let started = false;
        for (let x = Math.max(bx0, pk - CREST); x <= Math.min(bx1, pk + CREST); x += 4) {
          const bump = Math.cos(clamp(Math.abs(x - pk) / CREST) * Math.PI / 2) ** 2;
          const base = waveY(x, r);
          const y = base + Math.sin((x - bx0) / 17 - t * 3.4) * 9 * bump;
          if (!started) { ctx.moveTo(x, y); started = true; } else ctx.lineTo(x, y);
        }
        if (started) ctx.stroke();
      }
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 밭 칸 한 줄 (이 회차의 기본 도형) ──────────── */
    const n = spec.cells ?? 5;
    const gridW = n * CELL_W + (n - 1) * CELL_GAP;
    const gx0 = (w - gridW) / 2;
    for (let i = 0; i < n; i++) {
      const x = gx0 + i * (CELL_W + CELL_GAP);
      const edge = (i === 0 || i === n - 1) ? 0.45 : 1;

      ctx.globalAlpha = st * edge;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, x, GRID_Y, CELL_W, CELL_H, 6); ctx.stroke();

      /* 빈 작물 자리 — 태울 것이 처음부터 없다 */
      ctx.globalAlpha = st * edge * 0.55;
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.setLineDash([5, 6]);
      ctx.lineDashOffset = -((t * 11) % 11);
      ctx.beginPath();
      ctx.arc(x + CELL_W / 2, GRID_Y + CELL_H / 2, 9, 0, Math.PI * 2);
      ctx.stroke();
      ctx.setLineDash([]);
    }
    ctx.globalAlpha = 1;

    /* ── 가르는 점선 ───────────────────────────────── */
    ctx.save();
    ctx.globalAlpha = st;
    ctx.setLineDash([8, 9]);
    ctx.lineDashOffset = (t * 13) % 17;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(28, DIV_Y); ctx.lineTo(w - 28, DIV_Y); ctx.stroke();
    ctx.restore();

    /* ── 계수기 ────────────────────────────────────── */
    if (spec.counterLabel && zk > 0.01) {
      ctx.globalAlpha = clamp(zk * 1.4);
      ctx.textAlign = 'center';
      ctx.font = disp(800, fit(spec.counterLabel, 800, 12.5, w * 0.7, 9));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.counterLabel, w / 2, LAB_Y);
      ctx.globalAlpha = 1;
    }

    const zt = String(spec.zero ?? '0');
    if (zk > 0.01) {
      const fs = fit(zt, 900, 46, w * 0.4, 20);
      ctx.font = disp(900, fs);
      const zw = ctx.measureText(zt).width;
      const bw = Math.min(w - 48, Math.max(100, zw + 58));

      /* 잠긴 것을 두르는 점선 — 숫자는 안 움직이고 테두리만 흐른다 */
      if (lk > 0.01) {
        ctx.save();
        ctx.globalAlpha = lk * 0.65;
        ctx.setLineDash([11, 8]);
        ctx.lineDashOffset = -(t * 22) % 19;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        roundRect(ctx, w / 2 - bw / 2, BOX_Y, bw, BOX_H, 10); ctx.stroke();
        ctx.restore();
      }

      const sc = spring(zk);
      ctx.save();
      ctx.globalAlpha = clamp(zk * 1.5);
      ctx.translate(w / 2, BOX_Y + BOX_H / 2);
      ctx.scale(sc, sc);
      ctx.textAlign = 'center';
      ctx.font = disp(900, fs);
      setShadow(ctx, GLOW, 18);
      ctx.fillStyle = tone('accent');
      ctx.fillText(zt, 0, fs * 0.36);
      clearShadow(ctx);
      ctx.restore();
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
