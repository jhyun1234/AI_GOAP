import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* reach — 뒤집기가 어디까지 닿을지 스스로 그은 선.

   원문 61행. 전부 바꾸면 더 일관되지만 범위가 너무 넓어지므로, 필드 이름만 바꾸고
   goal/effect 이름은 유지하기로 정했다. 34개(S7)는 이 결정의 결과다 — 그래서 이 샷이
   S7 의 앞에 선다.

   그림은 **반경**이다. 왼쪽 뿌리(뒤집힌 필드 이름)에서 뒤집힘이 물결처럼 번져 나가는데,
   호(弧) 하나에서 매번 멎는다. 호 안쪽은 이미 뒤집혔고, 호 너머의 두 이름은 옛 이름
   그대로 서 있다. 손이 닿는 거리를 스스로 정한 그림이다.

   🔴 목록으로 그리지 않았다(ep01s shelf 가 후보 목록이다). 두 선택지를 두 줄로 세우면
   "고를 것이 둘"로 읽히는데, 원문은 이미 고른 뒤의 경계선을 말한다.
   🔴 게이지로도 안 그렸다(ep01s budget 이 한도 소진 게이지다). 범위를 %로 그리면
   원문에 없는 비율이 화면에 생긴다 — 이 샷에는 숫자를 하나도 두지 않았다.
   🔴 문턱·검문소도 아니다(ep00s checkpoint). 지나가는 것이 없다. 번지다 멎을 뿐이다.

   계속 도는 것 = 뿌리에서 계속 새로 나와 호에서 멎어 사라지는 물결. */

const WAVE = 2.6;     // 물결 하나가 뿌리에서 호까지 가는 초

/* 배치 (캔버스 352×307 기준)
     중심 (40,150) · 반경 122 · 각도 ±1.02rad → 호는 x 103~162 · y 48~252
     오른쪽 176~338 이 '호 너머' — 옛 이름 둘이 그대로 서 있는 자리 */
const CX = 40, CY = 150, R = 122, TH = 1.02;
const FAR_X = 176;

function fitFont(ctx, text, maxW, make, start, min) {
  let s = start;
  ctx.font = make(s);
  while (s > min && ctx.measureText(text).width > maxW) { s -= 0.5; ctx.font = make(s); }
  return s;
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const far = spec.far || [];
    const mk = ease(cue(spec.moreCue ?? 0, 0.15, 0.55));
    const wk = ease(cue(spec.wideCue ?? 1, 0.15, 0.5));
    const lk = ease(cue(spec.lineCue ?? 2, 0.15, 0.5));

    const PAD = 14;
    ctx.textBaseline = 'alphabetic';

    /* ── 물결 : 뿌리에서 번지다 호에서 멎는다 ───────
       계속 도는 것. cue 와 무관하게 항상 돈다. */
    for (let i = 0; i < 3; i++) {
      const u = frac(t / WAVE + i / 3);
      const r = lerp(34, R - 4, u);
      ctx.globalAlpha = 0.5 * (1 - u) * clamp(u * 4);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
      ctx.beginPath(); ctx.arc(CX, CY, r, -TH, TH); ctx.stroke();
    }
    ctx.globalAlpha = 1;

    /* ── 경계 호 : 여기까지만 ─────────────────────── */
    {
      ctx.lineWidth = lk > 0.3 ? 5 : 3;
      ctx.strokeStyle = lk > 0.3 ? tone('accent') : tone('sub');
      ctx.globalAlpha = 0.55 + 0.45 * lk;
      if (lk > 0.3) setShadow(ctx, GLOW, 12, 0);
      ctx.beginPath(); ctx.arc(CX, CY, R, -TH, TH); ctx.stroke();
      clearShadow(ctx);
      // 호 끝 캡 — 선이 캔버스 밖으로 이어지는 것처럼 보이지 않게 안쪽으로 접는다
      [-TH, TH].forEach(a => {
        const x = CX + Math.cos(a) * R, y = CY + Math.sin(a) * R;
        ctx.beginPath();
        ctx.moveTo(x, y);
        ctx.lineTo(x - 12, y + (a < 0 ? -8 : 8));
        ctx.stroke();
      });
      ctx.globalAlpha = 1;
    }

    /* ── 뿌리 : 이미 뒤집힌 필드 이름 ─────────────── */
    {
      const bx = PAD, by = 116, bw = 94, bh = 68;
      ctx.fillStyle = '#000';
      roundRect(ctx, bx, by, bw, bh, 5); ctx.fill();
      ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
      setShadow(ctx, GLOW, 10, 0);
      roundRect(ctx, bx, by, bw, bh, 5); ctx.stroke();
      clearShadow(ctx);

      ctx.textAlign = 'center';
      const cx = bx + bw / 2;

      // 옛 이름 — 그어서 지운다
      const oldName = spec.rootBefore || '';
      const os = fitFont(ctx, oldName, bw - 14, s => mono(700, s), 10, 7);
      ctx.fillStyle = tone('sub'); ctx.font = mono(700, os);
      ctx.fillText(oldName, cx, by + 24);
      const ow = ctx.measureText(oldName).width;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('sub');
      ctx.beginPath();
      ctx.moveTo(cx - ow / 2, by + 20); ctx.lineTo(cx + ow / 2, by + 20);
      ctx.stroke();

      // 아래 화살표
      ctx.fillStyle = tone('accent'); ctx.font = disp(900, 12);
      ctx.fillText('↓', cx, by + 40);

      // 새 이름
      const newName = spec.rootAfter || '';
      const ns = fitFont(ctx, newName, bw - 14, s => mono(700, s), 11, 7);
      ctx.fillStyle = tone('accent'); ctx.font = mono(700, ns);
      ctx.fillText(newName, cx, by + 58);
    }

    /* ── 호 안쪽 라벨 : 여기까지만 바꿨다 ───────────── */
    if (lk > 0.02) {
      ctx.globalAlpha = clamp(lk * 1.4);
      ctx.textAlign = 'left';
      const label = spec.inLabel || '';
      const s = fitFont(ctx, label, 150, x => disp(900, x), 12, 9);
      ctx.fillStyle = tone('accent'); ctx.font = disp(900, s);
      ctx.fillText(label, PAD, 34);
      ctx.globalAlpha = 1;
    }

    /* ── 호 너머 : 옛 이름 그대로 ─────────────────── */
    {
      const bw = w - PAD - FAR_X;

      // 이 바깥이 무엇인가
      if (lk > 0.02) {
        ctx.globalAlpha = clamp(lk * 1.4);
        ctx.textAlign = 'left';
        const label = spec.outLabel || '';
        const s = fitFont(ctx, label, bw, x => disp(700, x), 10, 7.5);
        ctx.fillStyle = tone('sub'); ctx.font = disp(700, s);
        ctx.fillText(label, FAR_X, 52);
        ctx.globalAlpha = 1;
      }

      // 안 바뀐 이름들
      far.forEach((name, i) => {
        const k = clamp(wk * 1.6 - i * 0.25);
        if (k <= 0.02) return;
        const by = 66 + i * 48, bh = 38;
        ctx.globalAlpha = clamp(k);
        ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
        roundRect(ctx, FAR_X, by, bw, bh, 4); ctx.stroke();
        ctx.textAlign = 'center';
        const s = fitFont(ctx, name, bw - 14, x => mono(700, x), 11, 7);
        ctx.fillStyle = tone('ink'); ctx.font = mono(700, s);
        ctx.fillText(name, FAR_X + bw / 2, by + bh / 2 + 4);
        ctx.globalAlpha = 1;
      });

      // 더 갔을 때의 이득과 대가 (원문 61행)
      ctx.textAlign = 'left';
      if (mk > 0.02) {
        ctx.globalAlpha = clamp(mk * 1.4);
        const gain = spec.gain || '';
        const s = fitFont(ctx, gain, bw, x => disp(800, x), 13, 9);
        ctx.fillStyle = tone('sub'); ctx.font = disp(800, s);
        ctx.fillText(gain, FAR_X, 196);
        ctx.globalAlpha = 1;
      }
      if (wk > 0.02) {
        ctx.globalAlpha = clamp(wk * 1.4);
        const cost = spec.cost || '';
        const s = fitFont(ctx, cost, bw, x => disp(800, x), 11, 7.5);
        ctx.fillStyle = tone('ink'); ctx.font = disp(800, s);
        ctx.fillText(cost, FAR_X, 224);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
