import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* teardown — 헐어야 드러난다.

   원문의 요지는 "옛 함수를 지웠다"가 아니라 **안 지우면 호출부가 숨는다**이다.
   그래서 화면의 주인공은 지워지는 함수가 아니라 **가림막 뒤에서 떠다니는 것**이다.
   위의 옛 함수가 헐리면 균열이 아래로 내려가고, 가림막이 걷히고, 드러난 건 딱 하나다.

   회차의 기본 도형 안에서 이 그림의 몫은 '가림막 뒤에서 맴도는 획'이다 —
   계속 움직이는데 밖으로 못 나온다.

   🔴 제품 마크는 팔레트 예외라 shot.checks.palette 에 사유를 달아 뒀다.
      대본이 그 제품을 부르는 첫 자막에서만 뜨고, 다음 자막이 시작하면 사라진다. */

const DRIFT = 3.4;   // 숨은 것이 가림막 뒤를 한 번 왕복하는 초

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since, images }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const cx = w / 2;
    const rk = ease(cue(spec.removeCue ?? 1, 0.2, 0.5));
    const vk = ease(cue(spec.revealCue ?? 2, 0.2, 0.55));

    const veilX = 34, veilY = 136, veilW = w - 68, veilH = 118;

    ctx.textBaseline = 'alphabetic';

    /* ── 제품 마크 ────────────────────────────────
       상시 노출 금지. 부르는 자막 동안만. */
    const lg = spec.logo;
    if (lg && images && images[lg.asset]) {
      const on = ease(cue(lg.cue ?? 0, 0.2, 0.4));
      const off = clamp(1 - Math.max(0, since(1)) / 0.45);
      const a = clamp(on * off);
      if (a > 0.02) {
        const s = 26 * spring(on);
        ctx.globalAlpha = a;
        ctx.drawImage(images[lg.asset], w - 14 - s, 12, s, s);
        ctx.textAlign = 'right';
        ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
        ctx.fillText(lg.label || '', w - 20 - s, 29);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 옛 함수 ──────────────────────────────────
       헐린다. 접혀 사라지는 것이 코드가 지워지는 모양에 가깝다. */
    const old = spec.old || {};
    const oh = 54 * (1 - ease(clamp(rk / 0.7)));
    if (oh > 1) {
      ctx.globalAlpha = clamp(1 - rk * 1.1);
      ctx.fillStyle = '#000';
      roundRect(ctx, 66, 46, w - 132, oh, 5); ctx.fill();
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      roundRect(ctx, 66, 46, w - 132, oh, 5); ctx.stroke();
      if (oh > 22) {
        ctx.textAlign = 'center';
        ctx.fillStyle = tone('ink'); ctx.font = disp(900, 13);
        ctx.fillText(old.label || '', cx, 46 + oh / 2 - 2);
        ctx.fillStyle = tone('sub'); ctx.font = mono(700, 10);
        ctx.fillText(old.note || '', cx, 46 + oh / 2 + 13);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 균열 ─────────────────────────────────────
       헐린 자리에서 아래로 내려가 가림막에 닿는다. */
    if (rk > 0.25) {
      const g = clamp((rk - 0.25) / 0.75);
      ctx.globalAlpha = clamp(g * 1.4);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
      ctx.beginPath();
      ctx.moveTo(cx, 74);
      const yEnd = lerp(74, veilY, g);
      let py = 74, sgn = 1;
      while (py < yEnd) {
        const ny = Math.min(py + 12, yEnd);
        ctx.lineTo(cx + sgn * 7, ny);
        sgn = -sgn; py = ny;
      }
      ctx.stroke();
      if (spec.breakTag) {
        ctx.textAlign = 'left';
        ctx.fillStyle = tone('accent'); ctx.font = disp(800, 11);
        ctx.fillText(spec.breakTag, cx + 16, lerp(84, veilY - 10, g));
      }
      ctx.globalAlpha = 1;
    }

    /* ── 가림막 ───────────────────────────────────
       걷히기 전에는 안이 안 보인다. */
    const hidden = spec.hidden || {};
    const open = ease(vk);
    ctx.save();
    ctx.setLineDash([8, 7]);
    ctx.lineWidth = 3;
    ctx.strokeStyle = open > 0.7 ? tone('track') : tone('ink');
    ctx.globalAlpha = 1 - open * 0.75;
    roundRect(ctx, veilX, veilY, veilW, veilH, 6); ctx.stroke();
    ctx.restore();
    ctx.globalAlpha = 1;

    if (open < 0.85) {
      ctx.globalAlpha = 1 - open / 0.85;
      ctx.textAlign = 'left';
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
      ctx.fillText(hidden.label || '', veilX + 10, veilY + 20);
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
      ctx.fillText(hidden.note || '', veilX + 10, veilY + 36);
      ctx.globalAlpha = 1;
    }

    /* ── 계속 도는 것 ─────────────────────────────
       숨은 것이 가림막 뒤를 좌우로 떠다닌다. 드러나면 제자리에 선다. */
    const u = frac(t / DRIFT);
    const tri = u < 0.5 ? u * 2 : 2 - u * 2;
    const bw = 128, bh = 46;
    const bx = lerp(lerp(veilX + 14, veilX + veilW - 14 - bw, tri), cx - bw / 2, ease(vk));
    const by = veilY + veilH - 26 - bh;

    ctx.fillStyle = '#000';
    roundRect(ctx, bx, by, bw, bh, 5); ctx.fill();
    if (vk > 0.5) setShadow(ctx, GLOW, 14, 0);
    ctx.lineWidth = 3;
    ctx.strokeStyle = vk > 0.5 ? tone('accent') : tone('track');
    roundRect(ctx, bx, by, bw, bh, 5); ctx.stroke();
    clearShadow(ctx);

    /* 드러난 뒤에도 화면은 안 멎는다 — 호출부가 없어진 함수를 부르러 올라갔다 되돌아온다.
       그게 '컴파일이 깨졌다'의 그림이고, 이 회차의 기본 도형이 마지막으로 한 번 더 나오는 자리다. */
    if (vk > 0.25) {
      const a2 = clamp((vk - 0.25) / 0.5);
      const topY = 96;
      ctx.save();
      ctx.setLineDash([6, 7]);
      ctx.globalAlpha = a2 * 0.8;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('track');
      ctx.beginPath(); ctx.moveTo(cx, by - 6); ctx.lineTo(cx, topY); ctx.stroke();
      ctx.restore();

      const u2 = frac(t / 2.0);
      const up = u2 < 0.5 ? ease(u2 * 2) : 1 - ease((u2 - 0.5) * 2);
      ctx.globalAlpha = a2;
      ctx.fillStyle = tone('accent');
      ctx.beginPath();
      ctx.arc(cx, lerp(by - 8, topY + 4, up), 6, 0, Math.PI * 2);
      ctx.fill();
      ctx.globalAlpha = 1;
    }

    const rev = spec.reveal || {};
    ctx.textAlign = 'center';
    if (vk > 0.15) {
      ctx.globalAlpha = clamp((vk - 0.15) * 1.8);
      ctx.fillStyle = tone('ink'); ctx.font = disp(900, 26);
      ctx.fillText(String(rev.count ?? ''), bx + bw / 2, by + bh / 2 + 2);
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
      ctx.fillText(rev.label || '', bx + bw / 2, by + bh / 2 + 18);
      ctx.globalAlpha = 1;
    } else {
      // 아직 가림막 뒤다 — 몇 곳인지 모른다
      ctx.fillStyle = tone('sub'); ctx.font = disp(900, 26);
      ctx.fillText('?', bx + bw / 2, by + bh / 2 + 8);
    }

    ctx.textAlign = 'left';
  }
};
