import {
  disp, clamp, lerp, ease, easeOut, frac, span, fitCanvas, mkCanvas, tone } from '../../../engine/lib.js';

/* stage2d — 2D 무대 위의 동그라미.
   위치는 전부 t 의 함수다. 궤적도 과거 시각을 다시 계산해서 그린다.
   그래서 어느 시각으로 뛰어도 같은 그림이 나온다.

   자막 연동: 동그라미는 첫 자막에 등장하고, 걷기는 그 걸음을 말하는 자막에 맞춰 나아간다.
   왕복 카운터: "몇 분째 그 자리"를 숫자로 만든다 — 갈팡질팡이 측정 가능해야 답답함이 전달된다. */

const PAD = 14;
const toPx = (a, w, h) => [PAD + a[0] / 100 * (w - PAD * 2), PAD + a[1] / 100 * (h - PAD * 2)];

/* 도착한 뒤에도 아주 조금 흔들린다.
   🔴 이게 없으면 walk 배우는 걷기가 끝나는 순간 완전히 멈춘다. 걷기는 walkCue 자막
   하나의 길이 안에서 끝나는데 샷은 그보다 훨씬 길어서, 실측으로 마지막 샷의 8초 넘는
   구간이 정지 화면이었다(ep01 F 11.6초 · 프레임 간 변화 0). 가이드가 금지하는
   0.5초 이상 정적 구간의 가장 큰 덩어리였다. 진폭은 무대 0.5칸 — 살아 있다는 신호만. */
const SWAY = 0.5;
const sway = (a, t, i) => a.walk
  ? [Math.sin(t * 1.7 + i * 2.1) * SWAY, Math.cos(t * 1.35 + i * 1.3) * SWAY * 0.8]
  : [0, 0];

/** 배우의 시각 t(초)·걷기 진행 q 에서의 무대 좌표(0~100) */
function pos(a, t, q) {
  if (a.oscillate) {
    const o = a.oscillate, ph = o.phase || 0;
    const u = frac(t / o.period + ph);
    const out = 0.30, hold = out + (o.stall ?? 0.4) * 0.35;
    let k;
    if (u < out) k = ease(u / out);
    else if (u < hold) k = 1 + Math.sin(t * 16 + ph * 9) * 0.014;   // 멈칫거림
    else k = 1 - ease((u - hold) / (1 - hold));
    return [lerp(a.at[0], o.to[0], k), lerp(a.at[1], o.to[1], k)];
  }
  if (a.walk) {
    const pts = [a.at, ...a.walk], seg = pts.length - 1;
    const r = easeOut(clamp(q)) * seg;
    const i = Math.min(seg - 1, Math.floor(r)), f = r - i;
    return [lerp(pts[i][0], pts[i + 1][0], f), lerp(pts[i][1], pts[i + 1][1], f)];
  }
  return a.at;
}

/** t 까지 base 로 되돌아온 횟수 — 한 주기가 곧 한 번의 왕복 */
const trips = (a, t) => a.oscillate ? Math.floor(t / a.oscillate.period + (a.oscillate.phase || 0)) : 0;

/** 무대 좌표 + 미세한 흔들림 */
const at = (a, ai, t, q) => {
  const [x, y] = pos(a, t, q), [dx, dy] = sway(a, t, ai);
  return [x + dx, y + dy];
};

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, p, t, dur, cue, nLines }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const actors = spec.actors || [];
    // appearLead 를 크게 주면 등장 연출을 사실상 건너뛴다 — 콜드 오픈용.
    // 쇼츠 첫 샷은 "무대가 세워지는 것"이 아니라 "이미 고장나 있는 것"부터 보여야 한다.
    const appear = ease(cue(spec.appearCue ?? 0, spec.appearLead ?? 0.2, 0.4));
    // walkPace:"shot" 이면 걷기를 자막 하나가 아니라 샷 전체에 펼친다.
    // 자막 하나에 물리면 첫 2초에 다 걷고 남은 8초가 정지 화면이 된다.
    const walkQ = spec.walkPace === 'shot'
      ? clamp(t / (dur * 0.94))
      : clamp(cue(spec.walkCue ?? 0, 0.15, 0.92));

    // 안개 걷기 — 지나간 자리가 밝아진다
    if (spec.fog?.reveal) {
      ctx.save();
      for (const a of actors) {
        for (let s = 0; s <= 24; s++) {
          const f = s / 24;
          const [x, y] = toPx(at(a, actors.indexOf(a), t * f, walkQ * f), w, h);
          const g = ctx.createRadialGradient(x, y, 0, x, y, 46);
          g.addColorStop(0, 'rgba(0,255,136,.07)');
          g.addColorStop(1, 'rgba(0,255,136,0)');
          ctx.fillStyle = g; ctx.beginPath(); ctx.arc(x, y, 46, 0, 7); ctx.fill();
        }
      }
      ctx.restore();
    }

    // 기지 등 소품 — 검정 위에서 보이려면 보더 3px 이상, 어두운 fill 금지
    for (const pr of spec.props || []) {
      const [x, y] = toPx(pr.at, w, h);
      ctx.fillStyle = '#000000'; ctx.fillRect(x - 17, y - 13, 34, 26);
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.strokeRect(x - 17, y - 13, 34, 26);
      if (pr.label) {
        ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
        ctx.textAlign = 'center'; ctx.fillText(pr.label, x, y + 30);
      }
    }

    // 궤적 — 과거 1.1초를 되짚어 그린다
    const TRAIL = 1.1, N = 16;
    actors.forEach((a, ai) => {
      if (appear < (ai + 0.5) / actors.length) return;
      ctx.lineWidth = 3; ctx.lineCap = 'round';
      for (let s = N; s > 0; s--) {
        const t0 = Math.max(0, t - TRAIL * (s / N));
        const t1 = Math.max(0, t - TRAIL * ((s - 1) / N));
        const q = Math.max(0, walkQ - (walkQ * TRAIL * (s / N)) / Math.max(t, .001));
        const [x0, y0] = toPx(at(a, ai, t0, q), w, h);
        const [x1, y1] = toPx(at(a, ai, t1, q), w, h);
        ctx.strokeStyle = tone(a.color) + Math.round(6 + (1 - s / N) * 40).toString(16).padStart(2, '0');
        ctx.beginPath(); ctx.moveTo(x0, y0); ctx.lineTo(x1, y1); ctx.stroke();
      }
    });

    // 배우 — 첫 자막에서 하나씩 등장한다(동시 등장 금지)
    actors.forEach((a, ai) => {
      const k = clamp(appear * actors.length - ai);
      if (k <= 0) return;
      const [x, y] = toPx(at(a, ai, t, walkQ), w, h);
      const c = tone(a.color), r = lerp(7, 10, ease(k));
      ctx.globalAlpha = ease(k);
      ctx.fillStyle = c + '2b';
      ctx.beginPath(); ctx.arc(x, y, r * 2.1, 0, 7); ctx.fill();
      ctx.fillStyle = c;
      ctx.beginPath(); ctx.arc(x, y, r, 0, 7); ctx.fill();
      ctx.globalAlpha = 1;
    });

    // 왕복 카운터 — "몇 분째 계속 그 자리"를 숫자로 만든다
    const cc = spec.tripCounter;
    if (cc) {
      const k = ease(cue(cc.cue ?? nLines - 1, 0.3, 0.35));
      if (k > 0.02) {
        const n = actors.reduce((s, a) => s + trips(a, t), 0);
        ctx.globalAlpha = k; ctx.textAlign = 'left';
        ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
        ctx.fillText(cc.label || '왕복', 0, 16);
        ctx.fillStyle = tone('ink');
        ctx.font = disp(900, 30);
        ctx.fillText(String(n) + (cc.unit || '회'), 0, 48);
        ctx.globalAlpha = 1;
      }
    }
  }
};
