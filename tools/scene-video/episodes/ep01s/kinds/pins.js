import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* pins — 셋 다 같은 자리에서 멈췄다.

   🔴 이 그림은 1차 제출의 `spoke`(방사형 네 갈래)를 버리고 다시 짠 것이다. 반려 사유는
   `ep00s/kinds/channels.js` 와 **장치가 같다**는 것이었고, 실제로 그랬다 —
   "한 출발점 → 네 갈래 → 하나만 채워지고 셋은 빈 채 + 값 3을 움직이는 표식 3개 +
   하단 가운데 mono 로그 캡션". 배치(평행 관 vs 방사 십자)만 달랐다.

   같은 로그를 두 회차가 싣는 것은 강제였지만 그릇 선택은 자유였다. 그래서 **네 갈래라는
   구조 자체를 버렸다.** 이 화면에는 값이 흘러 들어갈 그릇이 네 개 있지 않다.

   0편과 1편이 이 로그로 하는 말이 애초에 다르다.
     0편: "경로 칸이 0이다 → 이건 내 문제가 아니다"    = 갈래를 **가려내는** 이야기
     1편: "셋 다 계획에서 막혔다"                      = 셋이 **같은 벽 앞에 서 있는** 이야기
   그리고 원문의 `FallbackCounter=3` 이 말하는 3 은 갈래에 담긴 값이 아니라 **주민 셋**이다.
   그래서 S1 에서 벽에 튕겨 돌아오던 그 획 셋이 그대로 이 화면으로 넘어온다 —
   같은 레인, 같은 벽 x, 같은 주기. 이번엔 되돌아가지 않고 벽 앞에 멈춰 서서, 각자의 끝에
   **이름표가 박힌다. 세 장이 전부 같은 글자다.**
   나머지 세 이유는 갈래가 아니라 **한 번도 안 쓰인 이름표 더미**로 옆에 쌓여 있다.

   회차의 기본 도형(뻗었다 되돌아오는 획) 안에서 이 그림의 몫은
   '벽을 계속 밀고 있지만 넘지 못하는 획'이다 — 계속 도는 것이 곧 그 밀기다. */

const LEAD = 1.9;   // S1 과 같은 시계 — 앞 샷에서 그대로 이어진다

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const lanes = spec.lanes || [];
    const n = Math.max(1, lanes.length);

    /* 🔴 S1(rebound)의 좌표를 그대로 쓴다. 같은 자리여야 "그 셋"으로 읽힌다. */
    const HEAD = 34, FOOT = 30, GAP = 10;
    const laneH = (h - HEAD - FOOT - GAP * (n - 1)) / n;
    const baseX = 12, baseW = 38;
    const startX = baseX + baseW + 10;
    const outX = w - 26;
    const wallX = Math.round(lerp(startX, outX, 0.56));
    const stopX = wallX - 13;

    const tagK = ease(cue(spec.tagCue ?? 0, 0.2, 0.5));
    const unK = ease(cue(spec.unusedCue ?? 1, 0.2, 0.55));

    ctx.textBaseline = 'alphabetic';

    /* ── 콘솔 한 줄 ───────────────────────────────
       원문 로그. 🔴 0편은 이걸 하단 가운데에 축소맞춤으로 깔았다. 여기서는 자막이
       "콘솔을 열어봤더니"라고 말하는 대로 **위쪽에 왼쪽부터 찍힌다.** */
    if (spec.console) {
      let cs = 11;
      ctx.font = mono(700, cs);
      while (cs > 7 && ctx.measureText(spec.console).width > w - 20) { cs -= 0.5; ctx.font = mono(700, cs); }
      ctx.save();
      ctx.beginPath(); ctx.rect(10, 0, (w - 20) * clamp(tagK * 1.8), 18); ctx.clip();
      ctx.textAlign = 'left';
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.console, 10, 13);
      ctx.restore();
    }

    /* ── 기지 ─────────────────────────────────────
       S1 과 같은 자리에 그대로 있다. 셋은 여기서 나갔다가 못 나간 채다. */
    ctx.lineWidth = 3; ctx.strokeStyle = tone('track');
    roundRect(ctx, baseX, HEAD - 6, baseW, h - HEAD - FOOT + 12, 6);
    ctx.stroke();
    ctx.textAlign = 'center';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
    ctx.fillText(spec.homeLabel || '기지', baseX + baseW / 2, h - FOOT + 16);

    /* ── 멈춘 자리 ────────────────────────────────
       S1 의 벽과 정확히 같은 x. 시청자가 방금 본 그 선이다. */
    ctx.lineWidth = 3; ctx.lineCap = 'butt';
    ctx.strokeStyle = tone('ink');
    ctx.beginPath();
    ctx.moveTo(wallX, HEAD - 8); ctx.lineTo(wallX, h - FOOT + 8);
    ctx.stroke();
    ctx.textAlign = 'right';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
    ctx.fillText(spec.stopLabel || '', wallX - 6, HEAD - 4);

    /* ── 획 셋 ────────────────────────────────────
       되돌아가지 않는다. 벽을 밀었다 물러나기만 한다 — 계속 도는 것이 이것이다. */
    const used = spec.used || {};
    for (let i = 0; i < n; i++) {
      const L = lanes[i] || {};
      const cy = Math.round(HEAD + laneH / 2 + i * (laneH + GAP));
      const P = L.period || 3.4, phase = L.phase || 0;

      const ph = frac((t + LEAD) / P + phase);
      const push = ph < 0.5 ? ease(ph / 0.5) : 1 - ease((ph - 0.5) / 0.5);
      const tipX = stopX - 15 + 15 * push;

      // 레일과 지나온 자국
      ctx.lineCap = 'round'; ctx.lineWidth = 3;
      ctx.strokeStyle = tone('track');
      ctx.beginPath(); ctx.moveTo(startX, cy); ctx.lineTo(wallX - 5, cy); ctx.stroke();
      ctx.strokeStyle = 'rgba(255,255,255,0.55)';
      ctx.beginPath(); ctx.moveTo(startX, cy); ctx.lineTo(tipX, cy); ctx.stroke();

      // 벽에 닿는 순간 — S1 과 같은 신호
      if (push > 0.93) {
        ctx.strokeStyle = tone('accent'); ctx.lineCap = 'butt';
        ctx.beginPath(); ctx.moveTo(wallX, cy - 12); ctx.lineTo(wallX, cy + 12); ctx.stroke();
        ctx.lineCap = 'round';
      }

      // 핀
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(tipX, cy, 7, 0, Math.PI * 2); ctx.fill();

      /* 이름표 — 획 끝에 박힌다. 셋이 전부 같은 글자인 것이 이 샷의 전부다.
         🔴 값 3 은 갈래에 담긴 수가 아니라 **이 이름표가 세 장이라는 사실**이다. */
      const kk = clamp(tagK * 3 - i);
      if (kk > 0.02) {
        const label = used.label || '';
        ctx.font = disp(900, 13);
        const bw = ctx.measureText(label).width + 22;
        const bx = tipX - 12 - bw, by = cy - 13;
        ctx.globalAlpha = clamp(kk * 1.6);
        setShadow(ctx, GLOW, 12, 0);
        ctx.fillStyle = tone('accent');
        roundRect(ctx, bx, by, bw, 26, 4); ctx.fill();
        clearShadow(ctx);
        ctx.fillStyle = '#000';
        ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
        ctx.fillText(label, bx + bw / 2, cy + 1);
        ctx.textBaseline = 'alphabetic';
        ctx.globalAlpha = 1;
      }
    }

    /* ── 쓰인 이유의 값 ───────────────────────────
       이름표가 몇 장인지를 숫자로도 한 번 못박는다(로그의 Plan=3). */
    const sk = clamp(tagK * 3 - 2.2);
    if (sk > 0.02 && used.label != null) {
      ctx.globalAlpha = clamp(sk * 1.6);
      ctx.font = disp(800, 11);
      const lw = ctx.measureText(used.label).width;
      const bw = lw + 44;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
      roundRect(ctx, startX, 20, bw, 24, 3); ctx.stroke();
      ctx.textAlign = 'left'; ctx.textBaseline = 'middle';
      ctx.fillStyle = tone('sub');
      ctx.fillText(used.label, startX + 10, 32);
      ctx.fillStyle = tone('accent'); ctx.font = disp(900, 15);
      ctx.fillText(String(used.value ?? ''), startX + 18 + lw, 32);
      ctx.textBaseline = 'alphabetic';
      ctx.globalAlpha = 1;
    }

    /* ── 안 쓰인 이름표 더미 ──────────────────────
       🔴 그릇 넷을 세우지 않는다. 나머지 셋은 값이 흘러들 자리가 아니라
       **한 번도 꺼내 쓰지 않은 이름표**다. 그래서 어긋나게 겹쳐 쌓아 뒀다. */
    const un = spec.unused || [];
    const CW = 100, CH = 42, CSTEP = 46, CY0 = 100;
    if (unK > 0.02) {
      ctx.globalAlpha = clamp(unK * 1.6);
      ctx.textAlign = 'left';
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
      ctx.fillText(spec.unusedLabel || '', 238, 88);
      ctx.globalAlpha = 1;
    }
    un.forEach((u, i) => {
      const k = clamp(unK * 3 - i);
      if (k <= 0.02) return;
      const x = 238 - i * 8, y = CY0 + i * CSTEP;
      ctx.globalAlpha = clamp(k * 1.6);
      ctx.fillStyle = '#000';
      roundRect(ctx, x, y, CW, CH, 5); ctx.fill();
      ctx.lineWidth = 3; ctx.strokeStyle = tone('track');
      roundRect(ctx, x, y, CW, CH, 5); ctx.stroke();
      ctx.textAlign = 'left'; ctx.textBaseline = 'middle';
      ctx.fillStyle = tone('sub'); ctx.font = disp(800, 12);
      ctx.fillText(u.label || '', x + 12, y + CH / 2 + 1);
      ctx.textAlign = 'right';
      ctx.fillStyle = tone('ink'); ctx.font = disp(900, 20);
      ctx.fillText(String(u.value ?? 0), x + CW - 12, y + CH / 2 + 1);
      ctx.textBaseline = 'alphabetic';
      ctx.globalAlpha = 1;
    });

    ctx.textAlign = 'left'; ctx.lineCap = 'butt';
  }
};
