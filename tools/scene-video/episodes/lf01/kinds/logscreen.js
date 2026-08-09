import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect, rnd } from '../../../engine/lib.js';

/* logscreen — 이 회차에서 두 번 나오는 그림. 왼쪽 CONSOLE 은 쉬지 않고 채워지고,
   오른쪽 SCREEN 은 비어 있다.

   phase 0 (챕터2) : 줄이 콘솔에만 쌓인다. 전달 계기가 0 에서 안 움직인다.
   phase 1 (챕터4) : 그중 한 줄이 화면으로 **건너간다.** 새로 짓는 게 아니라 옮기는 것이라
                     콘솔 쪽은 그대로 두고 사본이 아니라 그 줄이 이동한다.

   로그 줄은 글자 대신 막대로 그린다 — 실제 로그 문자열을 지어내지 않기 위해서다.
   화면으로 건너간 줄만 원문에 있는 형식(M13 글 60행)을 그대로 쓴다. */

const STATUS = '■ 굶는 주민 {이름} — 식량 {N}일치';

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, nLines }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const moved = spec.phase === 1;
    const M = 16;

    const boxY = 36, boxH = h - 36 - 40;
    const cw = 272;
    const cx = M;
    const sx = w - M - cw;

    // ── 콘솔 ─────────────────────────────────────
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('CONSOLE', cx, boxY - 10);
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, cx, boxY, cw, boxH, 4); ctx.stroke();

    // 줄이 계속 흘러 들어온다 (자막과 무관 — 로그는 멈추지 않는다)
    ctx.save();
    ctx.beginPath(); ctx.rect(cx + 3, boxY + 3, cw - 6, boxH - 6); ctx.clip();
    const step = 15, n = Math.floor(t * 21 / step), scroll = (t * 21) % step;
    const rows = Math.ceil(boxH / step) + 2;
    for (let i = 0; i < rows; i++) {
      // seed 를 i - n 으로 잡아야 스크롤이 감길 때 같은 줄이 같은 자리로 이어진다
      const lw = 60 + rnd((i - n + 10000) * 7 + 3) * (cw - 96);
      ctx.fillStyle = tone('sub');
      ctx.fillRect(cx + 14, boxY + boxH - 12 - i * step - scroll, lw, 7);
    }
    ctx.restore();

    // ── 화면 ─────────────────────────────────────
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('SCREEN', sx, boxY - 10);
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, sx, boxY, cw, boxH, 4); ctx.stroke();

    const kMove = moved ? ease(cue(Math.min(4, nLines - 1))) : 0;

    if (kMove < 0.98) {
      // 빈 액자 — 아무것도 안 걸려 있다
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      const ix = sx + 30, iy = boxY + 26, iw = cw - 60, ih = boxH - 52;
      ctx.beginPath(); ctx.rect(ix, iy, iw, ih); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(ix, iy); ctx.lineTo(ix + iw, iy + ih);
      ctx.moveTo(ix + iw, iy); ctx.lineTo(ix, iy + ih); ctx.stroke();
    }

    // ── 건너가는 줄 (phase 1) ──────────────────────
    if (moved && kMove > 0.01) {
      const y0 = boxY + boxH * 0.44;
      const x = lerp(cx + 14, sx + 16, ease(kMove));
      const yy = lerp(y0, boxY + boxH * 0.34, ease(kMove));
      if (kMove < 0.96) {
        ctx.fillStyle = tone('accent');
        ctx.fillRect(x, yy, 150, 7);
      } else {
        ctx.font = disp(800, 13);
        const tw = ctx.measureText(STATUS).width;
        const scale = Math.min(1, (cw - 32) / tw);
        ctx.save();
        ctx.translate(sx + 16, boxY + boxH * 0.34);
        ctx.scale(scale, scale);
        ctx.fillStyle = tone('accent');
        ctx.fillText(STATUS, 0, 12);
        ctx.restore();
      }
    } else if (moved) {
      // 아직 안 옮겼다 — 콘솔 안에서 그 줄만 강조
      ctx.fillStyle = tone('accent');
      ctx.fillRect(cx + 14, boxY + boxH * 0.44, 150, 7);
    }

    // ── 사이 화살표 ───────────────────────────────
    const mx = (cx + cw + sx) / 2;
    ctx.strokeStyle = moved ? tone('accent') : tone('track'); ctx.lineWidth = 3;
    const ay = boxY + boxH / 2;
    ctx.beginPath();
    ctx.moveTo(mx - 16, ay); ctx.lineTo(mx + 16, ay);
    ctx.moveTo(mx + 6, ay - 10); ctx.lineTo(mx + 16, ay); ctx.lineTo(mx + 6, ay + 10);
    ctx.stroke();
    if (!moved) {
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(mx - 12, ay - 12); ctx.lineTo(mx + 12, ay + 12);
      ctx.stroke();
    }

    // ── 전달 계기 ─────────────────────────────────
    const kMeter = ease(cue(nLines - 1));
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('DELIVERED', M, h - 12);
    const dv = moved ? (kMeter > 0.9 ? '1' : '0') : '0';
    ctx.font = disp(900, 20);
    ctx.fillStyle = (moved && kMeter > 0.9) ? tone('accent') : tone('ink');
    ctx.fillText(dv, M + 74, h - 8);
  },
};
