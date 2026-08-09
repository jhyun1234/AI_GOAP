import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect } from '../../../engine/lib.js';

/* shrink — 24,800 → 5,977. 두 막대의 길이비가 곧 -76% 다(그래서 눈금을 안 적는다).
   위 막대에서 지워질 구간에는 빗금이 계속 흐른다 — 이 회차에서 '지워지는 중'은
   정지한 회색이 아니라 흐르는 빗금이다. 아래 띠의 네모 59개는 삭제된 파일 수다.
   수치 출처: M0 글 3·71행. */

const BEFORE = 24800, AFTER = 5977, FILES = 59;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 18, L = M, R = w - M, FW = R - L;

    const kBefore = ease(cue(0));
    const kNumbers = ease(cue(2));
    const kFiles = ease(cue(3));

    const BH = 38;
    const y1 = 46, y2 = 128;

    // ── BEFORE ────────────────────────────────────
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('BEFORE', L, y1 - 8);
    const bw = FW * clamp(0.08 + 0.92 * kBefore);
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, L, y1, bw, BH, 3); ctx.stroke();

    // 지워질 구간 — 빗금이 계속 흐른다
    const keep = FW * (AFTER / BEFORE);
    const cutX = L + keep, cutW = Math.max(0, bw - keep);
    if (cutW > 4) {
      ctx.save();
      ctx.beginPath(); ctx.rect(cutX, y1 + 2, cutW - 2, BH - 4); ctx.clip();
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      const off = (t * 26) % 20;
      for (let x = cutX - BH - 20 + off; x < cutX + cutW; x += 20) {
        ctx.beginPath(); ctx.moveTo(x, y1 + BH); ctx.lineTo(x + BH, y1); ctx.stroke();
      }
      ctx.restore();
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(cutX, y1); ctx.lineTo(cutX, y1 + BH); ctx.stroke();
    }
    ctx.font = disp(900, 24); ctx.fillStyle = tone('ink');
    ctx.fillText('24,800', L + 10, y1 + BH - 11);

    // ── AFTER ─────────────────────────────────────
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('AFTER', L, y2 - 8);
    const aw = keep * clamp(kNumbers);
    if (aw > 3) {
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, L, y2, aw, BH, 3); ctx.stroke();
    }
    if (kNumbers > 0.35) {
      ctx.font = disp(900, 24); ctx.fillStyle = tone('accent');
      const s = '5,977';
      ctx.fillText(s, L + keep + 14, y2 + BH - 11);
    }

    // 연결 — 위에서 아래로 좁아지는 두 선
    if (kNumbers > 0.05) {
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(L + 1, y1 + BH + 3); ctx.lineTo(L + 1, y2 - 3);
      ctx.moveTo(cutX, y1 + BH + 3); ctx.lineTo(L + keep, y2 - 3);
      ctx.stroke();
    }

    // ── 삭제된 파일 59개 ───────────────────────────
    const gy = h - 26;
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('FILES DELETED', L, gy - 8);
    const gone = Math.round(FILES * clamp(kFiles));
    const cw = Math.min(9, (FW - 90) / FILES - 1);
    for (let i = 0; i < FILES; i++) {
      const x = L + 88 + i * (cw + 1);
      if (i < gone) {
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.moveTo(x, gy + cw); ctx.lineTo(x + cw, gy); ctx.stroke();
      } else {
        ctx.fillStyle = tone('ink');
        ctx.fillRect(x, gy, cw, cw);
      }
    }
    if (kFiles > 0.5) {
      ctx.font = disp(900, 15); ctx.fillStyle = tone('ink');
      const s = '59';
      ctx.fillText(s, L + 88 - 4 - ctx.measureText(s).width, gy + cw);
    }
  },
};
