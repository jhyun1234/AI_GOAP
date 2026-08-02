import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* purge — 격자 하나가 통째로 걷히고, 숫자가 굴러 내려간다.

   위쪽 격자의 칸 하나가 파일 하나다(59칸). markCue 에서 맨 아래 줄 끝 칸 하나에만 이름이
   붙는다 — 1막에서 손댔던 그 파일이다. wipeCue 에서 격자가 비고, 걷힌 자리에는 빈 슬롯
   윤곽만 남는다. numCue 에서 아래 눈금이 24,800 에서 5,977 로 굴러 내려간다.

   🔴 걷히는 순서 = '이름 붙은 그 칸에서 먼 것부터'다. 좌상→우하로 훑고 지나가는 앞머리가
   아니라, 바깥 테두리가 그 한 칸 쪽으로 조여든다. 같은 거리의 칸들이 한꺼번에 비므로 획이
   선이 아니라 고리 모양이고, 이름 붙은 칸이 구조적으로 맨 마지막에 남는다.
   ▸ 왜 바꿨나: 1차 검수가 ep02s sweep.js 와의 겹침을 지적했다. 저쪽도 파일 개수만큼의 격자에
     좌상→우하 순차 앞머리였다(headIdx = sk × total). 극성(채움↔비움)만 반대인 같은 장치라
     순서 자체를 갈랐다. 뜻도 이쪽이 낫다 — W8 은 훑고 지나가는 단계가 아니라 '일괄 삭제'이고,
     이 편이 갚아야 할 빚은 '지난 편에 손댄 그 파일이 마지막에 지워진다'이기 때문이다.

   🔴 게이지나 막대로 줄이지 않는다. 앞선 회차가 게이지로 '모자람'을 이미 썼고, 무엇보다
   이 편의 사건은 '양이 줄었다'가 아니라 '자리가 비었다'이다. 그래서 비워지는 격자가 주고,
   숫자는 그 아래에서 따라 내려간다.

   회차의 기본 도형 안에서 이 그림의 몫은 '자리가 통째로 빈다'.
   계속 도는 것 = 격자 위를 계속 내려가며 훑는 선. 걷힌 뒤에도 빈 자리를 계속 훑는다. */

const SCAN = 3.6;

function comma(v) {
  const s = String(v); let out = '';
  for (let i = 0; i < s.length; i++) {
    if (i > 0 && (s.length - i) % 3 === 0) out += ',';
    out += s[i];
  }
  return out;
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const files = spec.files ?? 59;
    const cols = spec.cols ?? 9;
    const markAt = spec.markAt ?? files - 1;

    const ok = ease(cue(spec.openCue ?? 0, 0.15, 0.65));
    const mk = ease(cue(spec.markCue ?? 1, 0.15, 0.55));
    const wk = ease(cue(spec.wipeCue ?? 2, 0.15, 0.75));
    const nk = ease(cue(spec.numCue ?? 3, 0.15, 0.5));

    ctx.textBaseline = 'alphabetic';

    const cellW = 32, cellH = 18, gap = 4;
    const gridW = cols * (cellW + gap) - gap;
    const gx = (w - gridW) / 2, gy = 22;
    const rows = Math.ceil(files / cols);

    /* 걷힘의 순서 — 이름 붙은 칸까지의 거리. 먼 칸이 먼저 빈다.
       칸 사이 간격이 가로 36 · 세로 22 라 세로 거리에 22/36 을 곱해 실제 화면 거리로 맞춘다.
       (안 맞추면 고리가 세로로 늘어진 타원이 되어 '조여든다'로 안 읽힌다.) */
    const mc = markAt % cols, mr = Math.floor(markAt / cols);
    const aspect = (cellH + gap) / (cellW + gap);
    const distOf = i => {
      const dx = (i % cols) - mc, dy = (Math.floor(i / cols) - mr) * aspect;
      return Math.sqrt(dx * dx + dy * dy);
    };
    let dmax = 0;
    for (let i = 0; i < files; i++) dmax = Math.max(dmax, distOf(i));

    /* ── 격자 ─────────────────────────────────────── */
    for (let i = 0; i < files; i++) {
      const c = i % cols, r = Math.floor(i / cols);
      const x = gx + c * (cellW + gap), y = gy + r * (cellH + gap);
      const born = clamp(ok * (files + 12) - i * 0.55);
      if (born < 0.02) continue;
      const gone = clamp((wk * (dmax + 0.7) - (dmax - distOf(i))) * 2);

      // 빈 슬롯 윤곽 — 걷힌 자리에 남는다
      if (gone > 0.1) {
        ctx.globalAlpha = 0.55;
        ctx.lineWidth = 3; ctx.strokeStyle = tone('track');
        roundRect(ctx, x, y, cellW, cellH, 2); ctx.stroke();
        ctx.globalAlpha = 1;
      }
      if (gone > 0.98) continue;

      const marked = i === markAt && mk > 0.15;
      ctx.globalAlpha = clamp(born * 2) * (1 - gone);
      if (marked) setShadow(ctx, GLOW, 10, 0);
      ctx.lineWidth = marked ? 4 : 3;
      ctx.strokeStyle = marked ? tone('accent') : tone('ink');
      roundRect(ctx, x, y, cellW, cellH, 2); ctx.stroke();
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    /* 계속 도는 것 — 격자를 훑고 내려가는 선 */
    {
      const u = frac(t / SCAN);
      const gh = rows * (cellH + gap) - gap;
      const y = gy - 4 + u * (gh + 8);
      ctx.globalAlpha = 0.55 * Math.sin(Math.PI * u);
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(gx - 6, y); ctx.lineTo(gx + gridW + 6, y); ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 이름이 붙은 한 칸 ─────────────────────────── */
    if (mk > 0.05) {
      const c = markAt % cols, r = Math.floor(markAt / cols);
      const mx = gx + c * (cellW + gap) + cellW / 2;
      const my = gy + r * (cellH + gap) + cellH;
      const k = clamp(mk * 1.6);
      ctx.globalAlpha = k;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(mx, my); ctx.lineTo(mx, my + 14 * k); ctx.stroke();

      ctx.textAlign = 'center';
      const txt = spec.markLabel || '';
      let fs = 12; ctx.font = mono(700, fs);
      while (fs > 9 && ctx.measureText(txt).width > w - 60) { fs -= 0.5; ctx.font = mono(700, fs); }
      const tw = ctx.measureText(txt).width;
      const lx = clamp(mx, 18 + tw / 2, w - 18 - tw / 2);
      ctx.fillStyle = tone('accent');
      ctx.fillText(txt, lx, my + 30);
      ctx.fillStyle = tone('sub'); ctx.font = disp(800, 10.5);
      ctx.fillText(spec.markNote || '', lx, my + 46);
      ctx.globalAlpha = 1;
    }

    /* ── 눈금 ─────────────────────────────────────── */
    if (nk > 0.01) {
      const before = spec.before ?? 24800, after = spec.after ?? 5977;
      const v = Math.round(lerp(before, after, ease(nk)));
      ctx.globalAlpha = clamp(nk * 2.5);

      ctx.textAlign = 'center';
      ctx.fillStyle = tone('sub'); ctx.font = disp(800, 10.5);
      ctx.fillText(spec.numLabel || '코드베이스', w / 2, 228);

      ctx.font = disp(900, 42);
      const txt = comma(v);
      const tw = ctx.measureText(txt).width;
      setShadow(ctx, GLOW, 16, 0);
      ctx.fillStyle = tone('accent');
      ctx.fillText(txt, w / 2 - 18, 272);
      clearShadow(ctx);
      ctx.fillStyle = tone('sub'); ctx.font = disp(800, 13);
      ctx.textAlign = 'left';
      ctx.fillText(spec.unit || '줄', w / 2 - 18 + tw / 2 + 6, 272);

      if (nk > 0.82) {
        const k = clamp((nk - 0.82) / 0.18);
        ctx.globalAlpha = k;
        ctx.textAlign = 'center';
        ctx.font = disp(900, 13);
        const pt = spec.pct || '';
        const pw = ctx.measureText(pt).width;
        ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
        roundRect(ctx, w - 26 - pw - 20, 250, pw + 20, 24, 3); ctx.stroke();
        ctx.fillStyle = tone('ink');
        ctx.fillText(pt, w - 26 - pw / 2 - 10, 266);
      }
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
