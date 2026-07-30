import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* nextnote — 이 편 표식이 옆으로 밀려나고, 다음 편 표식이 들어와 앉는다.

   이번 편에서 세운 두 표식('빈 말풍선', '거꾸로 게이지')이 왼쪽으로 밀려나고, 그 자리에
   다음 편의 표식('성격 · 8단계')이 오른쪽에서 들어와 앉는다. 아래에는 scene.hook 이 한 줄로
   남는다 — ep01s erasure 가 만들어 둔 자리를 이 편이 이어서 쓴다.

   회차의 기본 도형 안에서 이 그림의 몫은 '표식이 교체된다'.
   계속 도는 것 = 밀려나는 옛 표식과 들어오는 새 표식이 한 방향으로 계속 흐른다. */

const CARRY = 3.6;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, scene, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const prev = spec.prev || [];
    const next = spec.next || {};
    const shk = ease(cue(spec.shiftCue ?? 0, 0.1, 0.55));
    const ak = ease(cue(spec.arriveCue ?? 1, 0.15, 0.55));

    const laneY = h / 2 - 24;

    ctx.textBaseline = 'alphabetic';

    // 가이드 레인 (매우 옅게)
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.setLineDash([4, 6]);
    ctx.beginPath(); ctx.moveTo(12, laneY); ctx.lineTo(w - 12, laneY); ctx.stroke();
    ctx.setLineDash([]);

    /* ── 옛 표식 : 왼쪽으로 밀려난다 ─────────────── */
    const tagW = 96, tagH = 32;
    /* 🔴 두 장이 서로 다른 거리를 가면 뒤엣것이 앞엣것을 들이받는다.
       옛 코드는 출발 간격이 106 인데 도착 간격이 6 이라 i=1 이 100px 더 달렸다.
       간격 = 106 − 100·e 라 e=0.10(44.87s)부터 상자가 겹치고,
       e=0.56(45.32s)부터 '빈 말풍선' 과 '거꾸로 게이지' 가 글자째 포개졌다(실측).
       같은 거리를 가면 간격 106 이 끝까지 유지된다 — 두 장이 끝까지 두 장으로 읽힌다. */
    const SLIDE = 170;
    prev.forEach((label, i) => {
      const startX = w / 2 - 60 + i * (tagW + 10);
      const cx = startX - SLIDE * ease(shk);
      const y = laneY - tagH / 2;

      /* 🔴 옛 투명도 1-0.6*shk 는 최솟값이 0.4 라 끝까지 안 사라졌다. 표식이 사라진 게
         아니라 캔버스 경계가 잘라 준 것이고, 잘린 '게이지' 세 글자가 그 자리에 섰다.
         가장자리에 닿기 전에 0 이 되게 한다: shk=0.526 에서 alpha=0 이고
         그때 왼쪽 끝이 x=24.4 라 띠(바깥 2열)에 닿지 않는다. */
      const alpha = clamp(1 - 1.9 * shk);
      if (alpha < 0.02) return;
      ctx.globalAlpha = alpha;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('sub');
      roundRect(ctx, cx, y, tagW, tagH, 3); ctx.stroke();
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('sub'); ctx.font = disp(800, 11);
      let ls = 11;
      while (ls > 8 && ctx.measureText(label).width > tagW - 10) { ls -= 0.5; ctx.font = disp(800, ls); }
      ctx.fillText(label, cx + tagW / 2, y + tagH / 2 + 4);
      ctx.globalAlpha = 1;
    });

    /* ── 새 표식 : 오른쪽에서 들어와 가운데에 앉는다 ─ */
    {
      const nextW = 152, nextH = 60;
      const startX = w + 20;
      const endX = w / 2 - nextW / 2;
      const cx = lerp(startX, endX, ease(ak));
      const y = laneY - nextH / 2;

      const s = spring(ak);
      const sw = nextW * s, sh = nextH * s;
      const sx = cx + (nextW - sw) / 2, sy = y + (nextH - sh) / 2;

      ctx.globalAlpha = clamp(ak * 1.4);
      setShadow(ctx, GLOW, 14, 0);
      ctx.lineWidth = 5; ctx.strokeStyle = tone('accent');
      roundRect(ctx, sx, sy, sw, sh, 5); ctx.stroke();
      clearShadow(ctx);

      ctx.textAlign = 'center';
      /* 🔴 카드가 오른쪽 가장자리에 걸쳐 있는 동안 글자를 찍으면 '성격'(31.11px)이
         화면 밖에서 반토막 난다. 상자가 들어오는 것은 연출이지만 글자는 아니다. */
      const inK = clamp((w - 6 - (cx + nextW)) / 20);
      ctx.globalAlpha = clamp(ak * 1.4) * inK;
      ctx.fillStyle = tone('accent'); ctx.font = disp(900, 18 * Math.min(1, s));
      ctx.fillText(next.label || '', cx + nextW / 2, y + 24);
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11 * Math.min(1, s));
      ctx.fillText(next.note || '', cx + nextW / 2, y + 46);
      ctx.globalAlpha = 1;
    }

    /* ── 계속 도는 것 : 한 방향으로 흐르는 흔적 ───── */
    for (let i = 0; i < 8; i++) {
      const u = frac(t / CARRY + i / 8);
      const dx = lerp(20, w - 20, u);
      const dy = laneY + 46;
      ctx.globalAlpha = 0.5 * (1 - Math.abs(u - 0.5) * 2);
      ctx.fillStyle = tone('sub');
      ctx.fillRect(dx, dy, 3, 3);
    }
    ctx.globalAlpha = 1;

    /* ── 남는 한 줄 : scene.hook ───────────────── */
    const hook = spec.hook || scene?.hook;
    if (hook && ak > 0.2) {
      const hk = clamp((ak - 0.2) * 1.4);
      const lines = String(hook).split('\n').slice(0, 2);
      let base = 20;
      ctx.font = disp(900, base);
      while (base > 12 && lines.some(l => ctx.measureText(l).width > w - 36)) {
        base -= 1; ctx.font = disp(900, base);
      }
      ctx.globalAlpha = hk;
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('ink');
      const s = spring(hk);
      ctx.font = disp(900, base * s);
      const ty = h - 60;
      lines.forEach((l, i) => ctx.fillText(l, w / 2, ty + i * (base * 1.34)));
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
