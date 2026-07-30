import {
  disp, mono, ease, clamp, lerp, frac, rnd,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* wardline — 지나가는 것을 채워 보내는 문턱.

   Awake() 라는 이름의 문턱이 하나 그어진다. 위에서 'Personality.None' 딱지가 내려오면
   방어선에 걸려 6종 성격 중 하나로 바뀌어 아래로 나간다. ep01s latch(빗장을 미는 획)와
   방향이 반대다 — 여기서는 못 넘게 막는 게 아니라 채워서 보낸다.

   회차의 기본 도형 안에서 이 그림의 몫은 '문턱 하나가 방향을 채워 준다'.
   계속 도는 것 = 문턱 앞에서 대기하는 None 딱지의 미세한 흔들림. */

const WOBBLE = 1.6;   // 대기 딱지가 흔들리는 초

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const rawOptions = spec.options || [];
    // 원문에 이름이 확인된 성격은 셋뿐(게으름·탐구·용감함).
    // scene.json 의 나머지 세 자리는 원문 미명시라 화면에서는 익명 자리(···)로 둔다.
    // 6칸이 유지되는 근거는 원문의 Random.Range(1,7) = 6종 존재 사실.
    const KNOWN = new Set(['게으름', '탐구', '용감함']);
    const options = rawOptions.map(name => KNOWN.has(name) ? name : '···');
    const pickedIdx = spec.picked ?? 0;
    const picked = options[pickedIdx] || options[0] || '';

    const wk = ease(cue(spec.wardCue ?? 1, 0.15, 0.55));
    const rk = ease(cue(spec.rollCue ?? 2, 0.15, 0.55));

    const gateY = 168;
    const cx = w / 2;

    ctx.textBaseline = 'alphabetic';

    /* ── 옵션 6종 : 문턱 아래 바닥에 놓인 후보들 ─────── */
    const optY = h - 96;
    const gap = (w - 24) / options.length;
    options.forEach((opt, i) => {
      const ox = 12 + gap * i + gap / 2;
      const on = i === (spec.picked ?? 0);
      const scale = on ? spring(rk) : 1;
      const oy = optY;
      const rw = Math.min(gap - 6, 58) * scale;
      const rh = 26 * scale;
      // 배경 상자
      if (on && rk > 0.02) {
        setShadow(ctx, GLOW, 12, 0);
        ctx.fillStyle = tone('accent');
        roundRect(ctx, ox - rw / 2, oy - rh / 2, rw, rh, 4);
        ctx.globalAlpha = clamp(rk * 1.6);
        ctx.fill();
        clearShadow(ctx);
        ctx.globalAlpha = 1;
      } else {
        ctx.lineWidth = 3; ctx.strokeStyle = tone('track');
        roundRect(ctx, ox - rw / 2, oy - rh / 2, rw, rh, 4); ctx.stroke();
      }
      ctx.textAlign = 'center';
      ctx.fillStyle = on ? '#000' : tone('sub');
      let s = 11;
      ctx.font = disp(on ? 900 : 700, s);
      while (s > 8 && ctx.measureText(opt).width > rw - 8) { s -= 0.5; ctx.font = disp(on ? 900 : 700, s); }
      ctx.fillText(opt, ox, oy + 4);
    });

    // 6종 라벨
    ctx.textAlign = 'center';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
    ctx.fillText('6종 성격', cx, optY - 26);

    /* ── 위쪽 : 대기 중인 None 딱지 (계속 도는 흔들림) ─ */
    const nx = cx + Math.sin(t * Math.PI * 2 / WOBBLE) * 4;
    const ny = 44;
    const nw = 112, nh = 30;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.setLineDash([5, 4]);
    roundRect(ctx, nx - nw / 2, ny - nh / 2, nw, nh, 4); ctx.stroke();
    ctx.setLineDash([]);
    ctx.textAlign = 'center';
    ctx.fillStyle = tone('ink'); ctx.font = mono(700, 12);
    ctx.fillText(spec.input || 'None', nx, ny + 4);

    // 위에서 문턱으로 내려가는 획 : wk 로 진행
    if (wk > 0.02) {
      const y1 = ny + nh / 2;
      const y2 = gateY - 6;
      const yc = lerp(y1, y2, ease(wk));
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(cx, y1); ctx.lineTo(cx, yc); ctx.stroke();
      // 화살촉
      ctx.fillStyle = tone('ink');
      ctx.beginPath();
      ctx.moveTo(cx, yc); ctx.lineTo(cx - 5, yc - 8); ctx.lineTo(cx + 5, yc - 8);
      ctx.closePath(); ctx.fill();
    }

    /* ── 방어선 : Awake() ─────────────────────────── */
    // 문턱 선
    const lineAlpha = clamp(wk * 1.4);
    ctx.globalAlpha = Math.max(0.4, lineAlpha);
    setShadow(ctx, GLOW, 14, 0);
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 5;
    ctx.beginPath(); ctx.moveTo(24, gateY); ctx.lineTo(w - 24, gateY); ctx.stroke();
    clearShadow(ctx);
    ctx.globalAlpha = 1;
    // 라벨
    ctx.textAlign = 'left';
    ctx.fillStyle = tone('accent'); ctx.font = mono(700, 11);
    ctx.fillText(spec.gateLabel || 'Awake()', 24, gateY - 8);

    /* ── 아래로 : 채워진 값이 내려간다 ─────────────── */
    if (rk > 0.02) {
      const y1 = gateY + 6;
      const y2 = optY - 18;
      const yc = lerp(y1, y2, ease(rk));
      setShadow(ctx, GLOW, 10, 0);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(cx, y1); ctx.lineTo(cx, yc); ctx.stroke();
      clearShadow(ctx);

      // 채워진 값 표식 : 원 안에 성격 이름
      const s = spring(rk);
      const r = 18 * s;
      const cy = yc;
      ctx.fillStyle = tone('accent');
      setShadow(ctx, GLOW, 14, 0);
      ctx.beginPath(); ctx.arc(cx, cy, r, 0, Math.PI * 2); ctx.fill();
      clearShadow(ctx);
      ctx.textAlign = 'center'; ctx.fillStyle = '#000';
      let s2 = 10;
      ctx.font = disp(900, s2);
      while (s2 > 7 && ctx.measureText(picked).width > r * 1.7) { s2 -= 0.5; ctx.font = disp(900, s2); }
      ctx.fillText(picked, cx, cy + 4);
    }

    ctx.textAlign = 'left';
  }
};
