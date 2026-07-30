import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* relight — 껐다 다시 켰더니 그 대사가 떴다.

   원문 51행. 사건 A 의 결말이다. Inspector 에 필드 하나를 열어 값을 직접 넣고,
   Play mode 를 다시 켜자 게으름 주민의 대사가 정상적으로 출력됐다.
   위에서 아래로 세 층이 한 줄로 꿰여 있다 — 필드에 값이 앉고, 그 값이 아래로 내려가
   실행을 켜고, 다시 아래로 내려가 말풍선을 켠다.

   🔴 hush(S1) 의 3행 레이아웃을 되받지 않았다. S9 hidefault 왼쪽이 이미 hush 를
   되받고 있어서, 여기서 또 '빈 말풍선 셋'을 그리면 같은 그림이 세 번 나온다.
   그래서 이 샷은 셋이 아니라 **한 자리**이고, 화면의 위쪽 절반은 말풍선이 아니라
   Inspector 패널과 실행 표시다. 한눈에 다른 그림이어야 결말로 읽힌다.
   🔴 orphan(S2) 과는 일부러 정반대다 — 거기서는 위층과 아래층 사이에 닿는 선이 하나도
   없었고, 여기서는 같은 세로축이 위에서 아래까지 **끊기지 않고** 이어진다.

   계속 도는 것 = ① 실행 표시의 도는 호 ② 그 세로축을 따라 계속 내려가는 값. */

const SPIN = 1.7;     // 실행 표시가 한 바퀴 도는 초
const DROP = 2.1;     // 값이 축을 한 번 내려가는 초

/* 세로 배치 (캔버스 352×307 기준)
     22~86  Inspector 패널 (필드 이름 + 값 칸)
     86~197 세로축 — 값이 계속 내려간다. 가운데 122 가 실행 표시
     172~252 말풍선 / 212 주민 / 244 성격 라벨 — 바닥에서 55px 뜬다 */
const AXIS_X = 36;
const PANEL_T = 22, PANEL_B = 86;
const RUN_Y = 122, RUN_R = 15;
const FACE_Y = 212, FACE_R = 13;
const BUB_T = 172, BUB_B = 252;

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
    const fk = ease(cue(spec.fieldCue ?? 0, 0.15, 0.55));
    const pk = ease(cue(spec.playCue ?? 1, 0.15, 0.5));
    const sk = ease(cue(spec.sayCue ?? 2, 0.15, 0.5));

    const PAD = 14;
    ctx.textBaseline = 'alphabetic';

    /* ── Inspector 패널 ───────────────────────────── */
    {
      const px = PAD, pw = w - PAD * 2;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('sub');
      roundRect(ctx, px, PANEL_T, pw, PANEL_B - PANEL_T, 4); ctx.stroke();

      // 패널 이름
      ctx.textAlign = 'left';
      ctx.fillStyle = tone('sub');
      const pl = spec.panel || '';
      fitFont(ctx, pl, pw - 20, s => mono(700, s), 10, 8);
      ctx.fillText(pl, px + 10, PANEL_T + 16);

      // 필드 이름
      const field = spec.field || '';
      const fs = fitFont(ctx, field, pw * 0.56, s => mono(700, s), 11, 7.5);
      ctx.fillStyle = tone('ink'); ctx.font = mono(700, fs);
      ctx.fillText(field, px + 10, PANEL_T + 42);

      // 값 칸 — fieldCue 에서 값이 앉는다
      const vw = pw * 0.34, vh = 24;
      const vx = px + pw - 10 - vw, vy = PANEL_T + 24;
      const on = fk > 0.1;
      ctx.lineWidth = 3;
      if (on) {
        const s = spring(fk);
        const sw = vw * s, sh = vh * s;
        setShadow(ctx, GLOW, 12, 0);
        ctx.strokeStyle = tone('accent');
        roundRect(ctx, vx + (vw - sw) / 2, vy + (vh - sh) / 2, sw, sh, 3); ctx.stroke();
        clearShadow(ctx);
        ctx.globalAlpha = clamp(fk * 1.5);
        ctx.textAlign = 'center';
        const val = spec.value || '';
        const vs = fitFont(ctx, val, vw - 12, s2 => disp(900, s2), 12, 8);
        ctx.fillStyle = tone('accent'); ctx.font = disp(900, vs);
        ctx.fillText(val, vx + vw / 2, vy + vh / 2 + 4);
        ctx.globalAlpha = 1;
      } else {
        ctx.setLineDash([5, 4]);
        ctx.strokeStyle = tone('track');
        roundRect(ctx, vx, vy, vw, vh, 3); ctx.stroke();
        ctx.setLineDash([]);
      }
    }

    /* ── 세로축 : 값이 끊기지 않고 내려간다 ────────── */
    {
      ctx.lineWidth = 3; ctx.strokeStyle = tone('track');
      ctx.beginPath();
      ctx.moveTo(AXIS_X, PANEL_B); ctx.lineTo(AXIS_X, RUN_Y - RUN_R - 2);
      ctx.moveTo(AXIS_X, RUN_Y + RUN_R + 2); ctx.lineTo(AXIS_X, FACE_Y - FACE_R - 2);
      ctx.stroke();

      // 계속 도는 것 ② — 내려가는 값
      for (let i = 0; i < 3; i++) {
        const u = frac(t / DROP + i / 3);
        const y = lerp(PANEL_B, FACE_Y - FACE_R - 2, u);
        if (Math.abs(y - RUN_Y) < RUN_R + 2) continue;   // 실행 표시 자리는 비켜 간다
        ctx.globalAlpha = 0.65 * (1 - Math.abs(u - 0.5) * 1.2);
        ctx.fillStyle = tone('accent');
        ctx.beginPath(); ctx.arc(AXIS_X, y, 3.6, 0, Math.PI * 2); ctx.fill();
      }
      ctx.globalAlpha = 1;
    }

    /* ── 실행 표시 : Play mode ─────────────────────
       계속 도는 것 ① — 둘레를 도는 호. 켜지면 강조색이 된다. */
    {
      const col = pk > 0.4 ? tone('accent') : tone('sub');
      ctx.lineWidth = 3; ctx.strokeStyle = tone('track');
      ctx.beginPath(); ctx.arc(AXIS_X, RUN_Y, RUN_R, 0, Math.PI * 2); ctx.stroke();

      const a0 = frac(t / SPIN) * Math.PI * 2;
      ctx.strokeStyle = col; ctx.lineWidth = 4;
      if (pk > 0.4) setShadow(ctx, GLOW, 10, 0);
      ctx.beginPath(); ctx.arc(AXIS_X, RUN_Y, RUN_R, a0, a0 + Math.PI * 0.7); ctx.stroke();
      clearShadow(ctx);

      // 재생 삼각
      ctx.fillStyle = col;
      ctx.beginPath();
      ctx.moveTo(AXIS_X + 6, RUN_Y);
      ctx.lineTo(AXIS_X - 4, RUN_Y - 6);
      ctx.lineTo(AXIS_X - 4, RUN_Y + 6);
      ctx.closePath(); ctx.fill();

      // 라벨
      ctx.textAlign = 'left';
      ctx.fillStyle = col;
      const rl = spec.run || '';
      const rs = fitFont(ctx, rl, w - (AXIS_X + RUN_R + 12) - PAD, s => mono(700, s), 12, 8);
      ctx.font = mono(700, rs);
      ctx.fillText(rl, AXIS_X + RUN_R + 10, RUN_Y + 4);
    }

    /* ── 주민 하나 + 켜진 말풍선 ──────────────────── */
    {
      // 주민
      ctx.lineWidth = 3; ctx.strokeStyle = sk > 0.4 ? tone('accent') : tone('ink');
      ctx.beginPath(); ctx.arc(AXIS_X, FACE_Y, FACE_R, 0, Math.PI * 2); ctx.stroke();

      // 성격 라벨 — hush 에서 대시였던 자리가 이름으로 찬다
      if (fk > 0.2) {
        ctx.globalAlpha = clamp((fk - 0.2) * 1.6);
        ctx.textAlign = 'center';
        const nm = spec.value || '';
        const ns = fitFont(ctx, nm, 70, s => disp(800, s), 11, 8);
        ctx.fillStyle = tone('accent'); ctx.font = disp(800, ns);
        const tw = ctx.measureText(nm).width;
        ctx.fillText(nm, clamp(AXIS_X, PAD + tw / 2, w - PAD - tw / 2), FACE_Y + 32);
        ctx.globalAlpha = 1;
      }

      // 말풍선
      const bx = 66, bw = w - PAD - bx;
      const on = sk > 0.1;
      ctx.lineWidth = on ? 4 : 3;
      if (on) {
        setShadow(ctx, GLOW, 12, 0);
        ctx.strokeStyle = tone('accent');
      } else {
        ctx.setLineDash([5, 4]);
        ctx.strokeStyle = tone('track');
      }
      roundRect(ctx, bx, BUB_T, bw, BUB_B - BUB_T, 6); ctx.stroke();
      clearShadow(ctx);
      ctx.setLineDash([]);

      // 꼬리 — 주민 쪽을 가리킨다
      ctx.strokeStyle = on ? tone('accent') : tone('track');
      ctx.lineWidth = on ? 4 : 3;
      ctx.beginPath();
      ctx.moveTo(bx, FACE_Y - 8); ctx.lineTo(bx - 12, FACE_Y); ctx.lineTo(bx, FACE_Y + 8);
      ctx.stroke();

      // 대사 — 회색 기대치였던 것이 강조색 실물로 켜진다
      const line = spec.line || '';
      const ls = fitFont(ctx, line, bw - 28, s => disp(900, s), 17, 10);
      ctx.textAlign = 'center';
      ctx.font = disp(900, ls);
      if (on) {
        ctx.globalAlpha = clamp(sk * 1.5);
        setShadow(ctx, GLOW, 10, 0);
        ctx.fillStyle = tone('accent');
      } else {
        ctx.globalAlpha = 0.45;
        ctx.fillStyle = tone('sub');
      }
      ctx.fillText(line, bx + bw / 2, (BUB_T + BUB_B) / 2 + ls * 0.36);
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
