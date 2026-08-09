import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* bomrecover — 셸 명령 한 줄이 에셋 칸 넷을 한꺼번에 비우고, 그 순간 아래 게이트 램프
   둘이 즉시 걸린다. 그 다음 규칙 토큰이 사람 칸에서 기계 칸으로 옮겨 앉는다.
   게이트 이름 M0-T1 · M10-T2 는 devlog 원문 그대로다.
   연속 모션 = 게이트 램프의 감시 맥박. 걸리기 전에도 램프는 돌고 있었다 — 그래서
   「즉시」 잡을 수 있었다는 게 이 그림의 요지다.
   문자열 출처: devlog/sessions/2026-08-05.md 65~68행 · Docs/개발_방법론_명세서.md 174~178행. */

const GATES = ['M0-T1', 'M10-T2'];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    const kCmd = ease(cue(0));     // 셸 명령 한 줄
    const kWipe = ease(cue(1));    // 참조가 통째로 초기화
    const kCatch = ease(cue(2));   // 게이트 2개가 즉시 잡았다
    const kMove = ease(cue(3));    // 규칙을 기계에 새긴다
    const kTwice = ease(cue(4));   // 같은 오해 2회
    const kThird = ease(cue(5));   // 세 번째에 또 뚫린다

    // ── 위 : 에셋 칸 넷 ────────────────────────────
    const ax = M, ay = 46, aw = 66, ag = 10, ah = 46;

    /* 🔴 2차 개정 (검수 C6): 이 kind 가 정적 측정에서 가장 낮았다(m 0.00015) —
       연속 모션이 램프 두 개의 알파 맥박뿐이라 픽셀이 거의 안 바뀌었다.
       → 에셋 칸과 게이트 램프를 **함께 훑고 내려가는 48px 폭 감시 띠**로 바꾼다.
       뜻도 이쪽이 정확하다: 게이트는 걸리기 전에도 계속 돌고 있었으니 「즉시」 잡았다.
       칸·램프보다 먼저 그린다(강조색 위에 반투명 흰색 금지). */
    {
      const BR = 2.6, bk = (t % BR) / BR;
      const bx0 = lerp(M - 22, w - M + 22, bk);
      ctx.fillStyle = tone('track');
      ctx.fillRect(bx0 - 24, ay - 12, 48, (ay + ah + 22 + 30 + 12) - (ay - 12));
    }

    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('ASSET YAML  x4', M, 32);
    for (let i = 0; i < 4; i++) {
      const cx = ax + i * (aw + ag);
      const wiped = clamp((kWipe - i * 0.04) / 0.3);   // 한꺼번에 비워진다
      ctx.strokeStyle = wiped > 0.5 ? tone('track') : tone('accent');
      ctx.lineWidth = 3;
      roundRect(ctx, cx, ay, aw, ah, 3); ctx.stroke();
      if (wiped < 0.5) {
        ctx.fillStyle = tone('accent');
        ctx.fillRect(cx + 8, ay + 12, aw - 16, 6);
        ctx.fillRect(cx + 8, ay + 26, aw - 26, 6);
      } else {
        ctx.save(); ctx.globalAlpha = clamp((wiped - 0.5) / 0.4);
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        const mx = cx + aw / 2, my = ay + ah / 2;
        ctx.beginPath();
        ctx.moveTo(mx - 11, my - 11); ctx.lineTo(mx + 11, my + 11);
        ctx.moveTo(mx + 11, my - 11); ctx.lineTo(mx - 11, my + 11);
        ctx.stroke();
        ctx.restore();
      }
    }

    // 셸 명령 한 줄
    if (kCmd > 0.03) {
      ctx.save(); ctx.globalAlpha = clamp(kCmd);
      const bx = ax + 4 * (aw + ag) + 6;
      ctx.font = mono(700, 11); ctx.fillStyle = tone('sub');
      const s = 'BOM';
      ctx.fillText(s, bx, ay + 20);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(bx + 4, ay + 30); ctx.lineTo(bx + 4, ay + ah + 4);
      ctx.stroke();
      ctx.restore();
    }

    // ── 가운데 : 게이트 램프 둘 ─────────────────────
    const gy = ay + ah + 22;
    for (let i = 0; i < GATES.length; i++) {
      const gx = M + i * 150;
      // 감시 맥박 — 자막과 무관하게 계속 돈다
      const pulse = 0.35 + 0.65 * (0.5 - 0.5 * Math.cos(t * 3.0 + i * 1.6));
      const caught = kCatch > 0.25;
      ctx.strokeStyle = caught ? tone('accent') : tone('track');
      ctx.lineWidth = 3;
      roundRect(ctx, gx, gy, 132, 30, 3); ctx.stroke();
      ctx.save();
      ctx.globalAlpha = caught ? 1 : pulse;
      ctx.fillStyle = caught ? tone('accent') : tone('sub');
      ctx.beginPath(); ctx.arc(gx + 17, gy + 15, 6, 0, Math.PI * 2); ctx.fill();
      ctx.restore();
      ctx.font = mono(700, 12);
      ctx.fillStyle = caught ? tone('accent') : tone('sub');
      ctx.fillText(GATES[i], gx + 32, gy + 20);
    }
    if (kCatch > 0.35) {
      ctx.save(); ctx.globalAlpha = clamp((kCatch - 0.35) / 0.5);
      ctx.font = disp(900, 16); ctx.fillStyle = tone('accent');
      const s = '즉시 잡았다';
      const sw = ctx.measureText(s).width;
      ctx.fillText(s, Math.min(M + 2 * 150 + 12, w - M - sw), gy + 21);
      ctx.restore();
    }

    // ── 아래 : 규칙이 사람 칸에서 기계 칸으로 ────────
    const by = h - 52;
    const hw = 168, mw = 200;
    const hx = M, mx = w - M - mw;
    ctx.font = mono(700, 9); ctx.fillStyle = tone('sub');
    ctx.fillText('HUMAN ATTENTION', hx, by - 8);
    ctx.fillText('EDITMODE GATE', mx, by - 8);
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, hx, by, hw, 34, 3); ctx.stroke();
    ctx.strokeStyle = kMove > 0.3 ? tone('accent') : tone('track'); ctx.lineWidth = 3;
    roundRect(ctx, mx, by, mw, 34, 3); ctx.stroke();

    if (kMove > 0.02) {
      const k = ease(clamp(kMove));
      const tx = lerp(hx + 12, mx + 12, k);
      ctx.save(); ctx.globalAlpha = clamp(0.35 + 0.65 * k);
      ctx.fillStyle = k > 0.5 ? tone('accent') : tone('ink');
      ctx.fillRect(tx, by + 11, 46, 12);
      ctx.restore();
    }

    // 세 번째에 또 뚫린다 — 사람 칸에 금이 간다
    if (kThird > 0.05) {
      ctx.save(); ctx.globalAlpha = clamp(kThird);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(hx + hw * 0.34, by + 2);
      ctx.lineTo(hx + hw * 0.46, by + 17);
      ctx.lineTo(hx + hw * 0.36, by + 32);
      ctx.stroke();
      ctx.restore();
    }
    if (kTwice > 0.05) {
      ctx.save(); ctx.globalAlpha = clamp(kTwice);
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      const s = 'x2  ->  WIDEN THE GATE';
      const sw = ctx.measureText(s).width;
      ctx.fillText(s, Math.min(hx + hw + 16, mx - sw - 12), by + 22);
      ctx.restore();
    }
  },
};
