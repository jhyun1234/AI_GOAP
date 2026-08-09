import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect } from '../../../engine/lib.js';

/* fsmpile — 재설계 직전의 코드베이스. 왼쪽엔 한 파일이 혼자 3,476줄까지 차오르고,
   오른쪽엔 조건 분기 55곳이 6개 파일에 흩어져 찍힌다.
   스캔선이 분기 밭을 계속 훑는다 — "세어 보니"가 세는 행위이기 때문이다.
   수치 출처: M0 글 13행(6개 파일 55곳) · 17행(VillagerFSM 3,476줄). */

const PER_FILE = [12, 9, 11, 7, 10, 6];   // 합 55

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    const kBranch = ease(cue(1));
    const kCount = ease(cue(2));
    const kFile = ease(cue(3));

    // ── 오른쪽 : 분기 밭 ──────────────────────────
    const fx = 236, fw = w - M - fx;
    const colW = fw / 6;
    const top = M + 26, bot = h - 34;
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('6 FILES / 55 BRANCHES', fx, M + 12);

    const shown = Math.round(55 * clamp(kBranch * 0.55 + kCount * 0.45));
    let seen = 0;
    for (let c = 0; c < 6; c++) {
      const cx0 = fx + c * colW;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(cx0 + 3, top); ctx.lineTo(cx0 + 3, bot); ctx.stroke();
      for (let i = 0; i < PER_FILE[c]; i++) {
        seen++;
        if (seen > shown) continue;
        const dx = cx0 + 16 + (i % 3) * 15;
        const dy = top + 10 + Math.floor(i / 3) * 17;
        if (dy > bot - 4) continue;
        ctx.fillStyle = tone('ink');
        ctx.fillRect(dx, dy, 9, 9);
      }
    }

    // 스캔선 — 밭을 계속 훑는다
    const sk = (t % 2.6) / 2.6;
    const sxp = lerp(fx + 2, w - M - 2, sk);
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(sxp, top - 6); ctx.lineTo(sxp, bot + 6); ctx.stroke();

    if (kCount > 0.3) {
      ctx.font = disp(900, 26); ctx.fillStyle = tone('accent');
      const s = '55';
      const sw = ctx.measureText(s).width;
      ctx.fillText(s, w - M - sw, h - 10);
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      const t2 = 'BRANCH SITES';
      ctx.fillText(t2, w - M - sw - 10 - ctx.measureText(t2).width, h - 12);
    }

    // ── 왼쪽 : 한 파일이 혼자 커진다 ────────────────
    const bx = M + 14, bw = 96;
    const maxH = h - 34 - (M + 26);
    const bh = maxH * clamp(0.16 + 0.84 * kFile);
    const by = h - 34 - bh;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, bx, by, bw, bh, 3); ctx.stroke();
    // 줄무늬 — 코드 줄
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    for (let y = by + 9; y < by + bh - 5; y += 9) {
      ctx.beginPath(); ctx.moveTo(bx + 8, y); ctx.lineTo(bx + bw - 8, y); ctx.stroke();
    }
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('VillagerFSM.cs', M, M + 12);
    if (kFile > 0.25) {
      ctx.font = disp(900, 24); ctx.fillStyle = tone('ink');
      const s = '3,476';
      const sw2 = ctx.measureText(s).width;
      ctx.fillText(s, M, h - 10);
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText('LINES', M + sw2 + 6, h - 12);
    }
  },
};
