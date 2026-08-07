import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* earshot — 위에서 터진 말이 아래로 못 내려오다가, 아래가 귀를 뻗은 뒤에야 내려온다.

   원문: "대화를 담당하는 코드(ChatterService)는 여전히 관계를 전혀 모르게 두는
   것이었습니다. 대신 '방금 누가 누구에게 말을 걸었다'는 사실만 신호로 쏘게 하고,
   관계를 관리하는 별도의 서비스가 그 신호를 엿듣도록(구독하도록) 만들었습니다." /
   "표현과 계산을 끝까지 분리해두고 싶었습니다."

   🔴 이 그림의 핵심은 **두 상자가 끝까지 실선으로 안 이어진다**는 것이다. 원문이 지킨
   원칙이 "대화 코드는 관계를 전혀 모르게 둔다"이므로, 위에서 아래로 내려가는 배선을
   그리면 원문이 피하려던 바로 그 그림이 된다. 이어지는 것은 **아래에서 위로 뻗은
   점선 하나(구독)** 뿐이고, 그 위로는 신호만 타고 내려온다.

   🔑 기본 도형(두 사람 사이의 굽은 줄)이 여기서는 **아래 상자 안**에 있다. S1 에서
   그어지자마자 지워지던 그 줄이, 여기서 처음으로 굵어진 채 남는다.

   움직임 두 층:
     ① t — 위 상자 안에서 말풍선이 오가고(1.5초 주기), 터질 때마다 조각이 아래로 내려온다.
        조각은 한 바퀴보다 길게(1.15초) 떨어져 늘 한둘이 공중에 있다.
     ② cue — deadCue 동안 조각은 상자 바로 밑에서 멎어 스러지고, hearCue 에서 아래 상자가
        점선을 위로 뻗어 닿은 뒤부터 조각이 끝까지 내려와 아래 줄을 굵게 만든다. */

const TOP_Y0 = 34, TOP_Y1 = 132;
const BOT_Y0 = 186, BOT_Y1 = 288;
const CHAT_Y = 96, ARC_Y = 238;
const DOT = 11;
const GAP_STOP = 160;      // 귀가 안 붙었을 때 조각이 멎는 높이
const PP = 1.5;            // 말이 한 번 오가는 주기(초)
const FALL = 1.15;         // 조각 하나가 내려오는 데 걸리는 시간(초)

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

    const X0 = 18, X1 = w - 18, mid = (X0 + X1) / 2;
    const LX = X0 + 52, RX = X1 - 52;

    const c0 = cue(spec.deadCue ?? 0, 0.15, 0.60);
    const ck = cue(spec.hearCue ?? 1, 0.15, 0.80);
    const app = clamp(c0 * 6);
    if (app <= 0.02) return;

    const attach = ease(clamp(ck / 0.55));                 // 점선이 위 상자에 닿는다
    const deep = ease(clamp((ck - 0.55) / 0.40));          // 그 뒤로 조각이 끝까지 내려온다
    const botA = app * lerp(0.32, 1, ck);

    /* ── 위 상자 — CHATTER ──────────────────────────── */
    ctx.globalAlpha = app * 0.9;
    ctx.textAlign = 'left';
    ctx.font = disp(800, fit(spec.chatLabel ?? '', 800, 10.5, (X1 - X0) - 8, 8));
    ctx.fillStyle = tone('sub');
    ctx.fillText(spec.chatLabel ?? '', X0, TOP_Y0 - 8);
    ctx.globalAlpha = app;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    roundRect(ctx, X0, TOP_Y0, X1 - X0, TOP_Y1 - TOP_Y0, 8); ctx.stroke();
    ctx.globalAlpha = 1;

    for (const px of [LX, RX]) {
      ctx.globalAlpha = app;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(px, CHAT_Y, DOT, 0, Math.PI * 2); ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* 말풍선 — 상자 안에서 왼쪽 주민에게서 오른쪽 주민으로 */
    const vb = frac(t / PP);
    const fly = clamp((vb - 0.02) / 0.40);
    if (fly > 0.001 && fly < 0.999) {
      const bx = lerp(LX + 6, RX - 6, ease(fly));
      const by = CHAT_Y - 34 - 7 * Math.sin(Math.PI * fly);
      const a = app * clamp(fly * 9) * clamp((1 - fly) * 9);
      ctx.globalAlpha = a;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, bx - 14, by - 10, 28, 20, 6); ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(bx - 4, by + 10); ctx.lineTo(bx + 2, by + 17); ctx.lineTo(bx + 7, by + 10);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 아래 상자 — RELATION ───────────────────────── */
    ctx.globalAlpha = botA * 0.9;
    ctx.textAlign = 'left';
    ctx.font = disp(800, fit(spec.relLabel ?? '', 800, 10.5, (X1 - X0) - 8, 8));
    ctx.fillStyle = tone('sub');
    ctx.fillText(spec.relLabel ?? '', X0, BOT_Y0 - 8);
    ctx.globalAlpha = botA;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    roundRect(ctx, X0, BOT_Y0, X1 - X0, BOT_Y1 - BOT_Y0, 8); ctx.stroke();
    ctx.globalAlpha = 1;

    /* 아래 상자 안의 줄 — 조각이 닿는 만큼 굵어진다.
       🔴 강조색을 반투명으로 흰 것 위에 얹지 않으려고, 얇은 흰 줄과 굵은 강조 줄이
       거의 안 겹치게 알파를 갈랐다(겹치는 구간은 deep 0.00~0.08 뿐이고 그때도 강조를 먼저 그린다). */
    const ax0 = LX + 14, ax2 = RX - 14, acy = ARC_Y + 44;
    const arcPath = () => {
      ctx.beginPath();
      const N = 40;
      for (let i = 0; i <= N; i++) {
        const k = i / N, m = 1 - k;
        const x = m * m * ax0 + 2 * m * k * ((ax0 + ax2) / 2) + k * k * ax2;
        const y = m * m * ARC_Y + 2 * m * k * acy + k * k * ARC_Y;
        if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
      }
    };
    if (deep > 0.001) {
      ctx.globalAlpha = botA * clamp(deep * 12);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = lerp(3, 15, deep);
      ctx.lineCap = 'round';
      arcPath(); ctx.stroke();
      ctx.lineCap = 'butt';
      ctx.globalAlpha = 1;
    }
    if (deep < 0.09) {
      ctx.globalAlpha = botA * 0.6 * clamp(1 - deep * 12);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      arcPath(); ctx.stroke();
      ctx.globalAlpha = 1;
    }

    for (const px of [LX, RX]) {
      ctx.globalAlpha = botA;
      ctx.strokeStyle = deep > 0.5 ? tone('accent') : tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(px, ARC_Y, DOT, 0, Math.PI * 2); ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 귀 — 아래에서 위로 뻗은 점선(구독). 방향이 아래→위다 ── */
    if (attach > 0.02) {
      const tipY = lerp(BOT_Y0, TOP_Y1, attach);
      ctx.globalAlpha = app * clamp(attach * 2.4) * 0.95;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.setLineDash([8, 7]);
      ctx.lineDashOffset = (t * 16) % 15;
      ctx.beginPath(); ctx.moveTo(mid, BOT_Y0); ctx.lineTo(mid, tipY); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      /* 화살촉 — 위를 가리킨다 */
      ctx.beginPath();
      ctx.moveTo(mid - 7, tipY + 9); ctx.lineTo(mid, tipY); ctx.lineTo(mid + 7, tipY + 9);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 조각 — 말이 터질 때마다 하나씩 내려온다 ────── */
    const stopY = lerp(GAP_STOP, ARC_Y, deep);
    const nEmit = Math.floor((t - 0.46 * PP) / PP);
    for (let k = 0; k < 2; k++) {
      const te = 0.46 * PP + (nEmit - k) * PP;
      const q = (t - te) / FALL;
      if (q <= 0 || q >= 1) continue;
      const py = lerp(TOP_Y1, stopY, ease(q));
      const a = app * clamp(q * 8) * clamp((1 - q) * 5);
      if (a <= 0.02) continue;
      ctx.globalAlpha = a;
      ctx.fillStyle = deep > 0.3 ? tone('accent') : tone('ink');
      roundRect(ctx, mid - 13, py - 5.5, 26, 11, 4); ctx.fill();
      ctx.globalAlpha = 1;
    }

    /* 귀가 붙은 뒤, 줄이 굵어질 때 아래 상자에 은은한 테두리 빛 */
    if (deep > 0.4) {
      ctx.globalAlpha = botA * (deep - 0.4) * 1.2;
      setShadow(ctx, GLOW, 12, 0);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, X0, BOT_Y0, X1 - X0, BOT_Y1 - BOT_Y0, 8); ctx.stroke();
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
