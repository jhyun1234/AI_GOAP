import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect } from '../../../engine/lib.js';

/* gravemark — 무덤이 좌표만 남기던 상태에서 이름을 얻는 자리.
   좌표값은 원문에 없으므로 값이 아니라 형식 `(x, y)` 로 그린다(지어내지 않는다).
   흔적 링은 계속 퍼진다 — 주석이 약속한 '영구 흔적'이 실제로는 이 원 하나뿐이었다는
   뜻이라, 링이 도는 동안 이름표 자리는 비어 있는 것이 이 샷의 대사다.
   문자열 출처: M13 글 13·50행 · 클립 strike-farm-260809-0424-139 스틸(무덤 이름표). */

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    const kGrave = ease(cue(0));
    const kEmpty = ease(cue(1));
    const kNote = ease(cue(2));
    const kName = ease(cue(3));
    const kBack = ease(cue(4));

    const gx = 132, gy = h * 0.44, R = 40;

    // ── 퍼지는 흔적 링 ────────────────────────────
    if (kGrave > 0.2) {
      const P = 2.8, k = (t % P) / P;
      const rr = lerp(R + 6, R + 62, k);
      ctx.globalAlpha = clamp(1 - k) * 0.9;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(gx, gy, rr, 0, Math.PI * 2); ctx.stroke();
      ctx.globalAlpha = 1;
    }

    // ── 무덤 원 ──────────────────────────────────
    ctx.globalAlpha = clamp(0.15 + 0.85 * kGrave);
    ctx.strokeStyle = kName > 0.5 ? tone('accent') : tone('ink');
    ctx.lineWidth = 3;
    ctx.beginPath(); ctx.arc(gx, gy, R, 0, Math.PI * 2); ctx.stroke();
    ctx.globalAlpha = 1;
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('GRAVE', gx - ctx.measureText('GRAVE').width / 2, gy - R - 12);

    // ── 이름표 자리 ───────────────────────────────
    const py = gy + R + 14, pw = 150, ph = 30, px = gx - pw / 2;
    if (kEmpty > 0.05) {
      ctx.globalAlpha = clamp(kEmpty);
      ctx.strokeStyle = kName > 0.5 ? tone('accent') : tone('track');
      ctx.lineWidth = 3;
      roundRect(ctx, px, py, pw, ph, 3); ctx.stroke();
      ctx.globalAlpha = 1;
    }
    if (kName > 0.05) {
      const s = 'C · Day 9';
      ctx.font = disp(900, 17);
      const sw = ctx.measureText(s).width;
      ctx.save();
      ctx.beginPath(); ctx.rect(px + 3, py + 2, (pw - 6) * ease(kName), ph - 4); ctx.clip();
      ctx.fillStyle = tone('accent');
      ctx.fillText(s, gx - sw / 2, py + 21);
      ctx.restore();
    }

    // ── 주석과 남은 것 ────────────────────────────
    const cx = 268, cw = w - M - cx;
    if (kNote > 0.02) {
      ctx.globalAlpha = clamp(kNote);
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, cx, 44, cw, 56, 4); ctx.stroke();
      ctx.font = mono(700, 11); ctx.fillStyle = tone('sub');
      ctx.fillText('// code comment', cx + 14, 66);
      ctx.font = disp(800, 15); ctx.fillStyle = tone('ink');
      ctx.fillText('영구 흔적', cx + 14, 88);

      ctx.font = mono(700, 11); ctx.fillStyle = tone('sub');
      ctx.fillText('ACTUALLY LEFT', cx, 128);
      ctx.font = disp(900, 20); ctx.fillStyle = tone('ink');
      ctx.fillText('( x , y )', cx, 156);
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText('- and nothing else', cx, 174);
      ctx.globalAlpha = 1;
    }

    // ── 맨 처음 장면에 있던 그 무덤 ──────────────────
    if (kBack > 0.02) {
      const fw = 128, fh = 66, fx = w - M - fw, fy = h - 14 - fh;
      ctx.globalAlpha = clamp(kBack);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, fx, fy, fw, fh, 3); ctx.stroke();
      ctx.font = mono(700, 9); ctx.fillStyle = tone('sub');
      ctx.fillText('OPENING SHOT', fx + 8, fy + 16);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(fx + 26, fy + 42, 10, 0, Math.PI * 2); ctx.stroke();
      ctx.font = disp(800, 11); ctx.fillStyle = tone('accent');
      ctx.fillText('C · Day 9', fx + 44, fy + 46);
      ctx.globalAlpha = 1;
    }
  },
};
