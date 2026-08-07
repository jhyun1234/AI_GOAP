import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* rollback — 편의 기능을 걷어내고, 길 하나만 남긴다.

   원문: "그래서 편의성을 포기하고, 집은 오직 목수에게 부탁해서만 짓도록 되돌렸습니다."

   앞 샷(gnaw)의 좌표를 그대로 이어받는다 — 같은 자리에서 이번에는 반대 방향으로 움직인다.
   ① dropCue — 아래 호가 **양 끝에서 안쪽으로 되감겨** 가운데 한 점으로 사라진다.
      호 아래에 걸려 있던 칸 둘(자율 건설 · 자동 배정)이 시차를 두고 **가로로 접힌다.**
      자막 어순(「자율 건설도 → 자동 배정도」)과 접히는 순서가 같다.
   ② restoreCue — 파먹혔던 레일이 다시 실선으로 그어지고, 요청 캡슐이 주민에서 출발해
      **목수 고리를 지나** 집 칸에 닿는다. 캡슐이 닿아야 비로소 빗금이 찬다.

   🔴 이 샷은 집이 **비어 있는 상태로 시작한다.** 앞 샷에서 채워져 있던 그 칸이다 —
   편의 기능을 걷어냈으므로 이제 저절로는 안 채워진다. 그래야 「부탁해야만 지어진다」가
   화면에서 참이 된다. 지우기와 되살리기가 같은 화면 안에서 맞물리는 것이 이 편의 뒤집힘이다.

   🔴 되감기를 **가위표로 그리지 않았다.** 원문의 동사는 '끊었다'가 아니라 '폐기했다'이고,
   지운 것은 자국도 안 남는다. 그래서 호가 끊기는 게 아니라 **양 끝에서 걷혀 없어진다.**

   🔴 화면에 숫자가 없다. 걷히는 칸이 둘인 것은 원문이 이름을 둘 부르기 때문이고
   (자율 건설 · 자동 배정) 그 이름을 라벨로 적었을 뿐, 수를 주장하지 않는다.

   계속 도는 것 = 되살아난 레일 위를 왼쪽에서 오른쪽으로 흐르는 강조색 하이라이트 ·
   목수 고리의 바깥 점선 · 집 칸의 빗금 · 되감기는 호의 점선. */

const RAIL_Y = 112;
const VX = 44, CXP = 176, HXP = 300;
const HW = 36, HH = 30;
const ARC_C = 330;
const BOX = { y: 250, w: 116, h: 26 };

function arcPt(u, w) {
  const x0 = VX, y0 = RAIL_Y + 10;
  const x1 = w * (HXP / 352), y1 = RAIL_Y + 14;
  const cx = w * (172 / 352), cy = ARC_C;
  const m = 1 - u;
  return {
    x: m * m * x0 + 2 * m * u * cx + u * u * x1,
    y: m * m * y0 + 2 * m * u * cy + u * u * y1
  };
}

function hatch(ctx, x, y, bw, bh, k, t, color) {
  if (k <= 0.004) return;
  const fh = bh * k, fy = y + bh - fh;
  ctx.save();
  ctx.beginPath(); ctx.rect(x, fy, bw, fh); ctx.clip();
  ctx.strokeStyle = color; ctx.lineWidth = 3;
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
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const fit = (txt, weight, start, max, min = 8) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const cx = w * (CXP / 352);
    const hx = Math.min(w * (HXP / 352), w - 22 - HW / 2);
    const railL = VX + 9, railR = hx - HW / 2 - 2;

    const d0 = cue(spec.dropCue ?? 0, 0.15, 0.8);
    const r1 = cue(spec.restoreCue ?? 1, 0.15, 0.8);

    const roll = ease(clamp((d0 - 0.35) / 0.45));    // 호가 걷힌다
    const rail = ease(clamp(r1 / 0.42));             // 길이 다시 그어진다
    const run = ease(clamp((r1 - 0.24) / 0.54));     // 캡슐이 지나간다

    /* ── 걷히는 호 ────────────────────────────────── */
    if (roll < 0.995) {
      const a = (1 - roll) * 0.95;
      ctx.globalAlpha = a;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.setLineDash([13, 9]);
      ctx.lineDashOffset = -(t * 22) % 22;
      const u0 = 0.5 * roll, u1 = 1 - 0.5 * roll;
      ctx.beginPath();
      for (let i = 0; i <= 40; i++) {
        const p = arcPt(lerp(u0, u1, i / 40), w);
        if (i === 0) ctx.moveTo(p.x, p.y); else ctx.lineTo(p.x, p.y);
      }
      ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;
    }

    /* ── 접히는 칸 둘 ─────────────────────────────── */
    const labels = spec.dropLabels || [];
    for (let i = 0; i < labels.length; i++) {
      const fold = ease(clamp((d0 - (0.34 + i * 0.20)) / 0.26));
      const a = clamp(d0 * 5) * (1 - fold);
      if (a <= 0.02) continue;
      const bw = BOX.w * (1 - fold);
      const bcx = lerp(w * 0.30, w * 0.70, labels.length === 1 ? 0.5 : i / (labels.length - 1));
      const bx = Math.max(20 + bw / 2, Math.min(w - 20 - bw / 2, bcx));

      ctx.globalAlpha = a;
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      roundRect(ctx, bx - bw / 2, BOX.y - BOX.h / 2, bw, BOX.h, 6); ctx.stroke();
      /* 🔴 접히는 동안 라벨이 상자 밖으로 넘친다. `fit` 은 8px 에서 바닥을 치므로
         폭이 얇아지면 더 못 줄이고, 그 구간(약 0.16초)에 글자가 테두리를 넘어간다.
         문자열을 잘라 도망가지 않고 **상자 안쪽으로 가둔다** — 접히는 것은 칸이지
         이름이 아니므로 이름이 칸과 함께 사라지는 것이 맞다. */
      if (bw > 34) {
        ctx.save();
        ctx.beginPath();
        ctx.rect(bx - bw / 2 + 2, BOX.y - BOX.h / 2, Math.max(0, bw - 4), BOX.h);
        ctx.clip();
        ctx.textAlign = 'center';
        ctx.font = disp(800, fit(labels[i], 800, 12, bw - 16, 8));
        ctx.fillStyle = tone('sub');
        ctx.fillText(labels[i], bx, BOX.y + 4);
        ctx.restore();
      }
      ctx.globalAlpha = 1;
    }

    /* ── 되살아난 레일 ────────────────────────────── */
    if (rail > 0.02) {
      const rx = lerp(railL, railR, rail);
      ctx.globalAlpha = clamp(rail * 3) * 0.5;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 5;
      ctx.beginPath(); ctx.moveTo(railL, RAIL_Y); ctx.lineTo(rx, RAIL_Y); ctx.stroke();
      ctx.globalAlpha = 1;

      /* 흐르는 하이라이트 — 이제 여기로만 다닌다.
         🔴 흰색을 겹치지 않는다. 반투명 흰색을 강조색 위에 얹으면 팔레트 밖의
         옅은 민트가 나온다 — 같은 강조색의 알파 차이로만 밝기를 가른다. */
      const span = railR - railL;
      const hxs = railL + ((t * 78) % (span + 60)) - 60;
      const seg0 = Math.max(railL, hxs), seg1 = Math.min(rx, hxs + 48);
      if (seg1 > seg0) {
        ctx.globalAlpha = clamp(rail * 3);
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 5;
        ctx.beginPath(); ctx.moveTo(seg0, RAIL_Y); ctx.lineTo(seg1, RAIL_Y); ctx.stroke();
        ctx.globalAlpha = 1;
      }
    }

    if (spec.railLabel) {
      const a = clamp(rail * 2.4);
      if (a > 0.02) {
        ctx.globalAlpha = a;
        ctx.textAlign = 'center';
        ctx.font = disp(900, fit(spec.railLabel, 900, 13, 170, 9));
        ctx.fillStyle = tone('accent');
        ctx.fillText(spec.railLabel, cx, 84);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 목수 고리 — 캡슐이 지날 때 밝아진다 ─────────
       🟡 **알려진 예외(2026-08-08 검수·마스터 판정으로 이번 회차는 통과).**
       이 흰 고리가 강조색 레일 **위**에 알파 0.45~1.0 으로 얹혀, 겹치는 픽셀이
       `rgb(115,255,189)` 짜리 옅은 민트가 된다(실측 **98px** = 프레임의 0.012%,
       글자를 훑지 않는다). 팔레트 게이트는 이 혼합을 hue 150~152 로 읽어 통과시키므로
       **게이트 통과는 근거가 아니다** — 사람이 보고 넘긴 것이다.
       고리를 강조색으로 바꾸면 「목수 = 흰색」이라는 이 회차의 색 규칙이 깨지므로 그대로 뒀다.
       🔴 **다음 회차에서는 이 형태를 쓰지 마라.** 원천 해소법은 하나다 —
       **흰 것은 알파 1.0 으로 그리고, 밝기 변화는 알파가 아니라 굵기·반지름·점선 흐름으로 준다.**
       (여기서 알파를 흔든 이유는 「캡슐이 가까워지면 고리가 밝아진다」였는데,
        같은 뜻을 `19 + 3 × near` 처럼 크기로 이미 주고 있어 알파는 없어도 됐다.) */
    const capX = lerp(railL, railR, run);
    const near = clamp(1 - Math.abs(capX - cx) / 46);
    const ringA = clamp(rail * 2) * (0.45 + 0.55 * near);
    if (ringA > 0.02) {
      ctx.globalAlpha = ringA;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(cx, RAIL_Y, 12, 0, Math.PI * 2); ctx.stroke();
      ctx.setLineDash([7, 6]);
      ctx.lineDashOffset = -(t * 16) % 13;
      ctx.beginPath(); ctx.arc(cx, RAIL_Y, 19 + 3 * near, 0, Math.PI * 2); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;
    }

    /* ── 지나가는 요청 캡슐 ───────────────────────── */
    if (run > 0.01 && run < 0.999 && spec.capsuleLabel) {
      const cw = 78, ch = 24;
      const px = Math.max(20 + cw / 2, Math.min(w - 20 - cw / 2, capX));
      ctx.globalAlpha = clamp(run * 8) * clamp((1 - run) * 8);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, px - cw / 2, RAIL_Y - ch / 2, cw, ch, ch / 2); ctx.stroke();
      ctx.textAlign = 'center';
      ctx.font = disp(900, fit(spec.capsuleLabel, 900, 12, cw - 16, 8));
      ctx.fillStyle = tone('accent');
      ctx.fillText(spec.capsuleLabel, px, RAIL_Y + 4);
      ctx.globalAlpha = 1;
    }

    /* ── 주민 · 집 ────────────────────────────────── */
    ctx.globalAlpha = clamp(d0 * 6);
    ctx.fillStyle = tone('ink');
    ctx.beginPath(); ctx.arc(VX, RAIL_Y, 7, 0, Math.PI * 2); ctx.fill();
    ctx.globalAlpha = 1;

    const hxL = hx - HW / 2, hyT = RAIL_Y - HH / 2;
    /* 🔴 빈 칸으로 시작해서 캡슐이 닿아야 찬다. 테두리는 문턱으로 색을 바꾸지 않고
       빈 테두리 위에 흰 테두리가 알파로 겹쳐 오른다 — 하드 컷이 없어야 seek 이
       어느 시각으로 뛰어도 그림이 이어진다. */
    const fill = ease(clamp((run - 0.86) / 0.14));      // 닿아야 찬다
    const ha = clamp(d0 * 6);
    ctx.globalAlpha = ha;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    roundRect(ctx, hxL, hyT, HW, HH, 5); ctx.stroke();
    ctx.globalAlpha = 1;
    hatch(ctx, hxL, hyT, HW, HH, fill, t, tone('ink'));
    if (fill > 0.02) {
      ctx.globalAlpha = ha * clamp(fill * 2.5);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, hxL, hyT, HW, HH, 5); ctx.stroke();
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
