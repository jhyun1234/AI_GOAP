import {
  disp, ease, clamp, frac,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* crossaxis — 축이 하나뿐이던 자리에 두 번째 축이 그어진다.

   원문의 해결은 한 줄이다: "직업을 성격과 완전히 별개의 축으로 얹는 것. 고집쟁이 농부,
   순둥이 탐험가처럼 두 축이 곱해져야 진짜 '그 사람'이 나오니까요."

   🔴 그래서 격자가 처음부터 있으면 안 된다. axisCue 의 앞 절반에서 **세로축(성격)만**
   서고 — 이미 있던 것이다 — 뒤 절반에서 가로축(직업)이 왼쪽에서 오른쪽으로 그어지며
   비로소 칸이 생긴다. 격자가 '생기는 과정'이 이 샷의 사건이다.

   cellCue 에서 대각선 두 칸에 표식이 앉는다. 대각선인 것에 이유가 있다 — 같은 행이나
   같은 열에 두면 한 축만 갈린 것으로 읽힌다. 두 축이 **둘 다** 갈려야 원문의 말이 된다.

   🔴 직전 편의 표(noroll)·명단(roster)과 다른 점: 저것들은 행이 사람이고 열이 속성인
   완성된 표였다. 여기서는 표를 읽는 게 아니라 **표가 만들어지는 것**을 본다.

   🔴 등장은 전부 cue 에 물렸다. t 로 도는 것은 격자선 dash, 앉은 표식의 링 맥동,
   빈 칸의 십자 회전뿐이고 문턱을 가지지 않는다. */

const CELL_PER = [2.9, 3.4];   // 앉은 표식의 링 맥동 주기(초)

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

    const rows = spec.rows ?? [];
    const cols = spec.cols ?? [];
    const picks = spec.picks ?? [];
    const ak = ease(cue(spec.axisCue ?? 0, 0.15, 0.75));
    const ck = ease(cue(spec.cellCue ?? 1, 0.15, 0.8));

    const GX = 112, GY = 108;                 // 격자 왼쪽 위
    const CW = Math.min(96, (w - GX - 18) / Math.max(1, cols.length));
    const CH = 64;
    const colX = j => GX + CW * (j + 0.5);
    const rowY = i => GY + CH * (i + 0.5);
    const GX1 = GX + CW * cols.length;
    const GY1 = GY + CH * rows.length;

    /* 세로축(성격)은 앞 절반, 가로축(직업)은 뒤 절반 */
    const vk = ease(clamp(ak / 0.45));
    const hk = ease(clamp((ak - 0.45) / 0.55));

    /* ── 축 이름 ───────────────────────────────────── */
    ctx.font = disp(800, 12.5);
    if (spec.rowLabel) {
      ctx.globalAlpha = clamp(vk * 2.5); ctx.textAlign = 'left';
      ctx.fillStyle = tone('sub'); ctx.fillText(spec.rowLabel, 8, 28);
      ctx.globalAlpha = 1;
    }
    if (spec.colLabel) {
      ctx.globalAlpha = clamp(hk * 2.5); ctx.textAlign = 'right';
      ctx.fillStyle = tone('accent'); ctx.fillText(spec.colLabel, w - 8, 28);
      ctx.globalAlpha = 1;
    }

    /* ── 세로축 — 이미 있던 것 ─────────────────────── */
    if (vk > 0.02) {
      ctx.globalAlpha = vk;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.moveTo(GX, GY - 14); ctx.lineTo(GX, GY - 14 + (GY1 + 14 - (GY - 14)) * vk);
      ctx.stroke();
      ctx.globalAlpha = 1;

      ctx.textAlign = 'right';
      for (let i = 0; i < rows.length; i++) {
        const a = clamp(vk * 2.6 - i * 0.7);
        if (a <= 0.02) continue;
        ctx.globalAlpha = a;
        const fs = fit(rows[i], 800, 14, GX - 16, 9);
        ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
        ctx.fillText(rows[i], GX - 12, rowY(i) + fs * 0.36);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 가로축 — 새로 그어진다 ────────────────────── */
    if (hk > 0.02) {
      ctx.globalAlpha = hk;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.moveTo(GX, GY1); ctx.lineTo(GX + (GX1 + 12 - GX) * hk, GY1);
      ctx.stroke();
      ctx.globalAlpha = 1;

      ctx.textAlign = 'center';
      for (let j = 0; j < cols.length; j++) {
        const a = clamp(hk * 2.6 - j * 0.7);
        if (a <= 0.02) continue;
        ctx.globalAlpha = a;
        const fs = fit(cols[j], 800, 14, CW - 10, 9);
        ctx.font = disp(800, fs); ctx.fillStyle = tone('accent');
        ctx.fillText(cols[j], colX(j), GY - 22);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 격자 안쪽 선 — 두 축이 만나야 칸이 생긴다 ───── */
    const gk = Math.min(vk, hk);
    if (gk > 0.02) {
      ctx.globalAlpha = gk * 0.9;
      ctx.setLineDash([8, 7]);
      ctx.lineDashOffset = -(t * 12) % 15;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      for (let j = 1; j <= cols.length; j++) {
        ctx.beginPath(); ctx.moveTo(GX + CW * j, GY - 14); ctx.lineTo(GX + CW * j, GY1); ctx.stroke();
      }
      for (let i = 0; i < rows.length; i++) {
        ctx.beginPath(); ctx.moveTo(GX, GY + CH * i); ctx.lineTo(GX1, GY + CH * i); ctx.stroke();
      }
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;
    }

    /* ── 칸 ────────────────────────────────────────── */
    const isPick = (i, j) => picks.some(p => p[0] === i && p[1] === j);
    let pi = 0;
    for (let i = 0; i < rows.length; i++) {
      for (let j = 0; j < cols.length; j++) {
        const x = colX(j), y = rowY(i);

        if (!isPick(i, j)) {
          // 빈 칸 — 옅은 십자가 계속 돈다
          if (gk <= 0.02) continue;
          const a = frac(t / 3.1) * Math.PI * 2;
          ctx.globalAlpha = gk * 0.45;
          ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
          for (let s = 0; s < 2; s++) {
            const ang = a + s * Math.PI / 2;
            ctx.beginPath();
            ctx.moveTo(x - Math.cos(ang) * 7, y - Math.sin(ang) * 7);
            ctx.lineTo(x + Math.cos(ang) * 7, y + Math.sin(ang) * 7);
            ctx.stroke();
          }
          ctx.globalAlpha = 1;
          continue;
        }

        const idx = pi++;
        const k = ease(clamp(ck * 2.3 - idx * 1.0));
        if (k <= 0.02) continue;

        const pulse = 1 + 0.13 * Math.sin(frac(t / CELL_PER[idx % CELL_PER.length]) * Math.PI * 2);
        ctx.globalAlpha = k;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.arc(x, y, 15 * pulse, 0, Math.PI * 2); ctx.stroke();

        setShadow(ctx, GLOW, 8, 0);
        ctx.fillStyle = tone('accent');
        ctx.beginPath(); ctx.arc(x, y, 6.5, 0, Math.PI * 2); ctx.fill();
        clearShadow(ctx);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
