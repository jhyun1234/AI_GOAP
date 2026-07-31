import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* runmark — 실제로 돌려 본 한 판.

   devlog/sessions/2026-07-31.md 460행: "사용자 방치(무개입) 1판: 겨울 3번 넘기고 Day 44 전멸
   · 최대 8명". 이 한 줄이 이 편에서 유일한 실측이고, 앞의 여덟 샷이 설명한 성질이 실제로
   어떻게 끝나는지를 보여주는 자리다.

   🔴 가로 날짜축을 일부러 안 그렸다. 축을 그리면 겨울 관문의 x 좌표가 "겨울이 며칠마다
      오는가" 를 주장하게 되는데, 그 값은 devlog 에 없다. 관문 셋과 끊긴 자리 하나,
      그리고 끝에 붙는 'Day 44' 만 주장한다.

   회차의 기본 도형 안에서 이 그림의 몫은 '아래에서 나아가다 끊긴다'.
   계속 도는 것 = 앞머리가 나아갔다 처음으로 돌아오기를 반복한다(위치 이동). */

const LOOP = 4.2;
const BASE = 206;
const X0 = 26, XEND = 254;
const GATES = [86, 152, 218];
const GHOST = 288;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    const rk = ease(cue(spec.runCue ?? 0, 0.15, 0.6));
    const gk = ease(cue(spec.gateCue ?? 1, 0.12, 0.65));
    const pk = ease(cue(spec.peakCue ?? 2, 0.12, 0.5));

    ctx.textBaseline = 'alphabetic';
    ctx.textAlign = 'center';

    /* ── 관문 넷 : 셋은 지났고 넷째는 못 갔다 ──── */
    const drawGate = (x, on, ghost) => {
      ctx.lineWidth = ghost ? 3 : 5;
      ctx.strokeStyle = ghost ? tone('track') : (on ? tone('accent') : tone('ink'));
      if (ghost) ctx.setLineDash([5, 6]);
      if (on && !ghost) setShadow(ctx, GLOW, 12, 0);
      ctx.beginPath();
      ctx.arc(x, BASE, 26, Math.PI, 0);
      ctx.stroke();
      clearShadow(ctx);
      ctx.setLineDash([]);
    };

    GATES.forEach((x, i) => drawGate(x, clamp((gk - i * 0.2) / 0.4) > 0.5, false));
    drawGate(GHOST, false, true);

    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
    ctx.textAlign = 'left';
    ctx.fillText(spec.gateLabel || '겨울', 16, BASE - 44);
    ctx.textAlign = 'center';

    /* ── 지나온 획 ─────────────────────────────────
       🔴 끊김을 runCue(0번 자막)에 물렸더니 **나레이션이 "44일째에 전멸했어요" 라고 말하기
          1.64초 전에 화면이 먼저 끊겼다**(실측, 가이드 한도 ±20프레임 = 0.67초).
          0번 자막은 "손 놓고 한 판 돌려 봤는데요," 일 뿐이라 아직 결말을 부르지 않는다.
          → 획을 두 자막에 걸쳐 나아가게 하고(0번에서 절반, 1번에서 끝까지), 끊김은
          관문 셋이 다 켜진 뒤(gk 0.66~0.774)에 온다. 효과음을 얹으려고 draw 를 풀다가 찾았다. */
    const adv = clamp(rk * 0.52 + gk * 0.62);
    const brk = clamp((adv - 0.93) / 0.07);
    const head = lerp(X0, XEND, adv);
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 5;
    ctx.lineCap = 'round';
    setShadow(ctx, GLOW, 10, 0);
    ctx.beginPath(); ctx.moveTo(X0, BASE); ctx.lineTo(head, BASE); ctx.stroke();
    clearShadow(ctx);

    // 못 간 구간 — 옅은 안내선
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.setLineDash([4, 7]);
    ctx.beginPath(); ctx.moveTo(XEND + 6, BASE); ctx.lineTo(GHOST + 30, BASE); ctx.stroke();
    ctx.setLineDash([]);
    ctx.lineCap = 'butt';

    /* ── 계속 도는 것 : 되풀이하는 앞머리 ──────── */
    {
      const u = frac(t / LOOP);
      const x = lerp(X0, head, u);
      ctx.globalAlpha = 0.85 * (1 - u * 0.55);
      ctx.fillStyle = tone('accent');
      ctx.beginPath(); ctx.arc(x, BASE, 5, 0, Math.PI * 2); ctx.fill();
      ctx.globalAlpha = 1;
    }

    /* ── 끊긴 자리 ─────────────────────────────── */
    if (brk > 0.01) {
      const k = brk;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
      ctx.globalAlpha = k;
      // 지그재그 절단면
      ctx.beginPath();
      ctx.moveTo(XEND, BASE - 16);
      ctx.lineTo(XEND + 8, BASE - 6);
      ctx.lineTo(XEND - 6, BASE + 5);
      ctx.lineTo(XEND + 5, BASE + 16);
      ctx.stroke();
      ctx.globalAlpha = 1;

      ctx.fillStyle = tone('ink'); ctx.font = disp(900, 17);
      ctx.globalAlpha = k;
      ctx.fillText(spec.endDay || '', XEND + 6, BASE + 44);
      ctx.font = disp(800, 13);
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.endLabel || '', XEND + 6, BASE + 64);
      ctx.globalAlpha = 1;
    }

    /* ── 최대 인구 표식 ────────────────────────── */
    if (pk > 0.02) {
      const bw = 138, bh = 50, bx = w / 2 - bw / 2, by = 24;
      const s = spring(clamp(pk * 1.5));
      ctx.save(); ctx.translate(bx + bw / 2, by + bh / 2); ctx.scale(s, s);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink'); ctx.fillStyle = '#000';
      roundRect(ctx, -bw / 2, -bh / 2, bw, bh, 8);
      ctx.fill(); ctx.stroke();
      ctx.restore();
      ctx.globalAlpha = pk;
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
      ctx.fillText(spec.peakLabel || '', bx + bw / 2, by + 18);
      ctx.fillStyle = tone('ink'); ctx.font = disp(900, 22);
      ctx.fillText(spec.peak || '', bx + bw / 2, by + 41);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
