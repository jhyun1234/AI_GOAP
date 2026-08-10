import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* rings — 두 번째 시도. 성격마다 살고 싶은 거리를 따로 줬는데도 집이 안 멀어졌다.

   원문 근거:
   > "그래서 배율이 아니라 성격마다 선호하는 거리를 주고, 그 거리 근처의 후보들 중에서
   >  고르게 했습니다. 성격이 서로 다른 동심원을 갖게 된 거죠."
   > "성격별 선호 거리를 넣었는데도 집이 생각만큼 멀리 가지 않았습니다."

   앞 샷(twoends)이 「극단 둘」이었다면 여기는 **저마다의 고리**다. 고리를 여러 겹 깔아
   놓고 — 집들이 자기 고리를 향해 나갔다가 **안쪽 한 겹으로 도로 눌려 온다.**
   고리는 그대로 비어 남는다. 라벨을 다 가려도 「가라고 그어 둔 자리는 저기인데 실제로는
   여기서 안 나간다」가 읽힌다.

   🔴 **왜 되돌아오는지는 여기서 안 그린다.** 이 샷은 증상이고 원인은 다음 샷(tether)이
   맡는다. 여기에 벽이나 당김줄을 그리면 원문에 없는 장치를 만드는 셈이 된다 —
   원문은 이 시점에서 원인을 모르는 상태다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — twoends 와 같은 규약이다.
     불(accent) 글립 r ≤ 18 · 글로우까지 r ≤ 29 / 흰 것(고리·집·라벨) r ≥ 36. 7px 뜬다.

   🔴 라벨(「살라고 준 거리」)은 **90° 세로 통로**에 놓는다. 여섯 각도가 전부 90°에서
     ±35° 밖이라 어느 애니메이션 값에서도 집이 그 통로에 안 들어온다(twoends 주석 참조).
     세로로도 안 만난다 — 가장 아래에 서는 집(125°, 자기 고리 r 58.1)의 상자 바닥이
     y 209.6 이고 라벨 상자는 y 253~270 이다. 43px 뜬다.

   세로 예산(캔버스 307): 최저 = 라벨 글립 바닥 270(기준선 266). 37px 남는다.
   가로: 5° 집의 오른쪽 끝 261. 캔버스 352, 여유 91px.

   계속 도는 것 = 나갔다 눌리는 주기(2.4초 · 집 여섯이 통째로 이동) + 고리 다섯 겹의
   점선이 서로 다른 속도로 회전 + 불꽃 맥동. */

const CY = 150;
const R_IN = 50, R_OUT = 96;

/* twoends 와 **같은 자리표**다 — 같은 마을을 네 번 다시 보는 것이라 자리가 바뀌면
   시청자가 다른 마을로 읽는다. 원문 비율은 좌표로만 쓰고 수로 적지 않는다. */
const SEATS = [
  { deg: 125, ratio: 0.25 },
  { deg: 173, ratio: 0.80 },
  { deg: 221, ratio: 0.10 },
  { deg: 269, ratio: 0.50 },
  { deg: 317, ratio: 0.95 },
  { deg: 5, ratio: 0.50 }
];
const rOf = ratio => R_IN + (ratio - 0.10) / 0.85 * (R_OUT - R_IN);

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const CX = w / 2;

    const ctext = (txt, x, y, weight, size, color, maxW, alpha) => {
      if (alpha <= 0.01) return;
      let fs = size; ctx.font = disp(weight, fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(weight, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.textAlign = 'center'; ctx.fillStyle = color;
      ctx.fillText(txt, clamp(x, 14 + tw / 2, w - 14 - tw / 2), y);
      ctx.restore();
    };

    /* setLineDash 를 안 쓴다(결정성) — 호 조각을 직접 놓고 위상은 양수로 정규화한다. */
    const dashRing = (r, segDeg, gapDeg, phaseDeg, alpha) => {
      if (alpha <= 0.01) return;
      const P = segDeg + gapDeg;
      const ph = ((phaseDeg % P) + P) % P;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      for (let a = ph - P; a < 360; a += P) {
        ctx.beginPath();
        ctx.arc(CX, CY, r, a * Math.PI / 180, (a + segDeg) * Math.PI / 180);
        ctx.stroke();
      }
      ctx.restore();
    };

    const house = (x, y, alpha, sc) => {
      if (alpha <= 0.01) return;
      const bw = 22 * sc, bh = 18 * sc;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, x - bw / 2, y - bh / 2 + 3, bw, bh, 3); ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(x - bw / 2 - 3, y - bh / 2 + 3);
      ctx.lineTo(x, y - bh / 2 - 9 * sc);
      ctx.lineTo(x + bw / 2 + 3, y - bh / 2 + 3);
      ctx.stroke();
      ctx.restore();
    };

    const c0 = cue(spec.tryCue ?? 0, 0.15, 0.90);
    const st = ease(clamp(c0 / 0.14));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    if (spec.title) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
      ctx.restore();
    }

    /* ── 저마다의 고리 : 다섯 겹(0.5 가 둘이라 자리가 겹친다) ──
       🔴 고리 수를 화면에 글자로 안 적는다. 원문이 준 것은 여섯 성격의 비율이고
       그중 둘이 같은 값이라, 「여섯 겹」이라고 적으면 화면에 없는 수가 된다. */
    for (let i = 0; i < SEATS.length; i++) {
      const r = rOf(SEATS[i].ratio);
      dashRing(r, 8, 9, (i % 2 ? 1 : -1) * t * (7 + i * 1.7), st * 0.85);
    }

    /* ── 가운데 불 ────────────────────────────────── */
    const puff = 0.5 + 0.5 * Math.sin(t * 3.1);
    ctx.save();
    ctx.globalAlpha = st;
    setShadow(ctx, GLOW, 8 + 3 * puff);
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    const fh = 24 + 4 * puff;
    ctx.beginPath();
    ctx.moveTo(CX, CY - fh / 2 - 3);
    ctx.lineTo(CX + 10, CY + fh / 2 - 3);
    ctx.lineTo(CX - 10, CY + fh / 2 - 3);
    ctx.closePath(); ctx.stroke();
    clearShadow(ctx);
    ctx.restore();

    /* ── 나갔다가 도로 눌려 온다 ────────────────────
       ⏱ 주기를 `since()` 위에 세웠다 — 자막 길이가 바뀌어도 사건 시각이 안 밀린다. */
    const s0 = since(spec.tryCue ?? 0);
    const P = spec.loopSec ?? 2.4;
    const u = s0 >= 0 ? frac(s0 / P) : 0;
    const outK = ease(clamp((u - 0.08) / 0.30));          // 자기 고리로 나간다
    const backK = ease(clamp((u - 0.52) / 0.26));         // 안쪽 한 겹으로 눌려 온다
    const k = outK * (1 - backK);

    for (const s of SEATS) {
      const r = lerp(R_IN, rOf(s.ratio), k);
      const a = s.deg * Math.PI / 180;
      house(CX + r * Math.cos(a), CY + r * Math.sin(a), st, 1);
    }

    if (spec.goalLabel) {
      ctext(spec.goalLabel, CX, CY + R_OUT + 20, 900, 14, tone('ink'), 150, st * (0.35 + 0.65 * k));
    }

    ctx.textAlign = 'left';
  }
};
