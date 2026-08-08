import {
  disp, ease, clamp, spring, frac,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* todaycut — 훅 샷(4단 고정 구조 ②). 「오늘 지울 것」에 표를 찍는다.

   이 편의 기본 도형 그대로다 — **집이 서기까지 지나야 하는 것.**
   여기서는 그중 **짧은 길 하나만** 그린다: 왼쪽 끝에서 출발해 아무 데도 안 들르고
   집으로 바로 들어가는 흰 호(`AUTO`). 표식 셋이 그 위를 쉬지 않고 돌고, 집은
   밑에서부터 빗금이 찬다 — **편의 기능은 멀쩡히 잘 돌아가고 있다**(원문: '편리했죠').
   아래에는 강조색 `REQUEST` 캡슐이 흐리게 앉아 있고, 거기서 나온 점선이 집까지
   못 닿고 끊긴다. 부탁은 아직 아무 데도 안 닿는다.

   🔴 **아무것도 없애지 않는다.** 없애는 것은 S3(rollback)의 몫이고, 훅에서 미리 치우면
   그 샷의 뒤집힘이 죽는다. 훅은 약속이지 이행이 아니다.
   그래서 split 뒤에 일어나는 일은 **호의 양 끝에 흰 브래킷이 걸리고, 그 둘이 안쪽으로
   9px 만 물러나다 멈추는 것**이다 — S3 의 되감김(호가 양 끝에서 안으로 말려 사라진다)의
   **첫 프레임**이다. 시작만 하고 안 끝난다.

   🔤 화면 문자열은 전부 spec 경유다(`TODAY` · `AUTO` · `REQUEST`). fillText 하드코딩 0건.

   ⏱ 등장은 전부 훅 자막 하나의 cue 에 물린다. 자막이 한 줄이라 gnaw 와 같은 방식으로
   **앞뒤 구간**으로 가른다(spec.split) — 앞 절이 「오늘 개발 일지 내용은, 편한 기능」이라
   그동안 호와 집을 세우고, 뒤 절 「일부러 지우기예요」에서 브래킷이 걸린다.
   t 로 도는 것 = 호 위를 도는 표식 셋 · 집 안 빗금 · 끊긴 점선의 흐름. 전부 문턱이 없다. */

const ARC = { x0: 44, y0: 206, cx: 150, cy: 44, x1: 246, y1: 188 };
const HOUSE = { x: 246, y: 162, w: 56, h: 52 };
const MARKS = 3, SPIN = 2.2;

/** 2차 베지에 위의 점 */
function onArc(u, sx) {
  const m = 1 - u;
  return {
    x: sx(m * m * ARC.x0 + 2 * m * u * ARC.cx + u * u * ARC.x1),
    y: m * m * ARC.y0 + 2 * m * u * ARC.cy + u * u * ARC.y1
  };
}

/** 아래에서 차오르는 빗금 — 이 회차 공통 어휘(집은 언제나 밑에서부터 찬다) */
function hatch(ctx, x, y, bw, bh, k, t) {
  if (k <= 0.004) return;
  const fh = bh * k, fy = y + bh - fh;
  ctx.save();
  ctx.beginPath(); ctx.rect(x, fy, bw, fh); ctx.clip();
  ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
  const step = 13, off = (t * 11) % step;
  for (let d = -bh - step; d < bw + step; d += step) {
    ctx.beginPath();
    ctx.moveTo(x + d + off, y + bh);
    ctx.lineTo(x + d + off + bh, y);
    ctx.stroke();
  }
  ctx.restore();
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const sx = v => w * (v / 352);
    const fit = (txt, weight, start, max, min = 8) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const c0 = cue(spec.cutCue ?? 0, 0.15, 0.85);
    const split = spec.split ?? 0.7;
    const arrive = ease(clamp(c0 / 0.34));                     // 호와 집이 선다
    const fill = ease(clamp((c0 - 0.20) / 0.38));              // 집이 저 혼자 찬다
    const cut = ease(clamp((c0 - split) / Math.max(1e-6, 1 - split)));

    /* ── TODAY ───────────────────────────────────── */
    if (spec.label && arrive > 0.02) {
      ctx.globalAlpha = clamp(arrive * 2);
      ctx.textAlign = 'left';
      ctx.font = disp(800, fit(spec.label, 800, 12.5, w - 44, 9));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.label, 20, 32);
      ctx.globalAlpha = 1;
    }

    if (arrive <= 0.02) { ctx.textAlign = 'left'; return; }

    /* ── 짧은 길 — 왼쪽 끝에서 집으로 바로 들어간다 ── */
    const grow = arrive;                                        // 0→1 로 그어진다
    ctx.save();
    ctx.globalAlpha = grow;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.beginPath();
    const p0 = onArc(0, sx);
    ctx.moveTo(p0.x, p0.y);
    const STEP = 26;
    for (let i = 1; i <= STEP; i++) {
      const u = (i / STEP) * grow;
      const p = onArc(u, sx);
      ctx.lineTo(p.x, p.y);
    }
    ctx.stroke();
    ctx.restore();

    /* 호 위를 도는 표식 셋 — 편의 기능은 쉬지 않고 돌아간다 */
    if (grow > 0.98) {
      ctx.fillStyle = tone('ink');
      for (let i = 0; i < MARKS; i++) {
        const p = onArc(frac(t / SPIN + i / MARKS), sx);
        ctx.beginPath(); ctx.arc(p.x, p.y, 5, 0, Math.PI * 2); ctx.fill();
      }
    }

    /* AUTO — 호의 마루 위 */
    if (spec.pathLabel && grow > 0.5) {
      ctx.globalAlpha = clamp((grow - 0.5) * 3);
      ctx.textAlign = 'center';
      ctx.font = disp(800, fit(spec.pathLabel, 800, 13, sx(120), 9));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.pathLabel, sx(ARC.cx), 100);
      ctx.globalAlpha = 1;
    }

    /* ── 집 — 밑에서부터 찬다 ─────────────────────── */
    const hx = sx(HOUSE.x), hw = sx(HOUSE.x + HOUSE.w) - hx;
    hatch(ctx, hx, HOUSE.y, hw, HOUSE.h, fill, t);
    ctx.globalAlpha = clamp(arrive * 2.4);
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, hx, HOUSE.y, hw, HOUSE.h, 5); ctx.stroke();
    ctx.globalAlpha = 1;

    /* ── 아직 아무 데도 안 닿는 부탁 ────────────────
       흐린 강조색으로 앉아 있고, 점선이 집까지 못 가고 끊긴다.
       🔴 흰 것과 같은 픽셀에 겹치지 않는다 — 이 캡슐은 화면 아래 띠에만 있다. */
    if (spec.askLabel) {
      const cy = h - 32, cw = sx(96), ch = 26;
      const cxl = sx(56);
      ctx.save();
      ctx.globalAlpha = clamp(arrive) * 0.55;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, cxl, cy - ch / 2, cw, ch, ch / 2); ctx.stroke();
      ctx.textAlign = 'center';
      ctx.font = disp(900, fit(spec.askLabel, 900, 11.5, cw - 18, 8));
      ctx.fillStyle = tone('accent');
      ctx.fillText(spec.askLabel, cxl + cw / 2, cy + 4);
      /* 끊긴 점선 — 집 밑까지 못 간다 */
      ctx.setLineDash([7, 8]);
      ctx.lineDashOffset = -(t * 15) % 15;
      ctx.beginPath();
      ctx.moveTo(cxl + cw + 8, cy);
      ctx.lineTo(sx(212), cy);
      ctx.stroke();
      ctx.restore();
    }

    /* ── 오늘 지울 구간 — 양 끝에 브래킷이 걸리고 안으로 물러나다 멈춘다 ──
       S3 의 되감김(양 끝 → 안쪽)의 첫 프레임이다. 여기서는 끝까지 안 간다. */
    if (cut > 0.02) {
      const s = spring(clamp(cut / 0.5));
      const pinch = 9 * ease(clamp((cut - 0.5) / 0.5));
      const a = clamp(cut * 3);
      ctx.save();
      ctx.globalAlpha = a;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;

      const arm = 16 * s, stub = 11 * s;
      const L = onArc(0, sx), R = onArc(1, sx);

      ctx.beginPath();
      ctx.moveTo(L.x + pinch, L.y - arm); ctx.lineTo(L.x + pinch, L.y + arm);
      ctx.moveTo(L.x + pinch, L.y - arm); ctx.lineTo(L.x + pinch + stub, L.y - arm);
      ctx.stroke();

      ctx.beginPath();
      ctx.moveTo(R.x - pinch, R.y - arm); ctx.lineTo(R.x - pinch, R.y + arm);
      ctx.moveTo(R.x - pinch, R.y - arm); ctx.lineTo(R.x - pinch - stub, R.y - arm);
      ctx.stroke();
      /* 🔴 두 브래킷을 잇는 선은 안 긋는다. 호가 위로 크게 휘어 있어서 직선으로 이으면
         호 아래를 가로지르는 **두 번째 길**로 읽힌다 — 이 편에서 선은 전부 「길」이다. */
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
