import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* twoends — 첫 시도. 「바깥을 좋아하는 정도」를 곱했더니 여섯이 둘로 뭉개졌다.

   원문 근거:
   > "첫 시도는 실패했습니다. 성격마다 '바깥을 좋아하는 정도'를 배율로 주고 점수식에
   >  곱했는데, 집이 여전히 모닥불 옆에 일렬로 섰습니다."
   > "점수식의 최댓값이 언제나 극단(가장 중심 아니면 가장 구석)에 있어서, 성격 여섯
   >  종류가 사실상 '안쪽 아니면 바깥쪽' 두 종류로 뭉개진 겁니다."

   본문 넷이 공유하는 판 = **가운데 불 하나와 그 둘레의 여섯.** 묻는 것은 하나다 —
   여섯이 **어느 거리에 서는가.**

   루프(2.6초): 여섯 집이 저마다 다른 거리에서 출발했다가 **가까운 쪽 극단으로 빨려 붙는다.**
   안쪽 고리에 넷, 바깥쪽 고리에 둘이 붙고 **그 사이 띠가 통째로 빈다.**
   라벨을 다 가려도 「저마다 다르던 것이 두 무더기로 뭉친다」가 읽힌다.

   🔴 **어느 집이 어느 쪽으로 가는지는 지어내지 않았다.** 원문이 준 것은 극단 두 곳뿐이고
   개체별 배정은 안 적혀 있다. 그래서 규칙을 하나만 뒀다 — **가까운 극단으로 간다**
   (출발 거리가 두 고리의 한가운데보다 안쪽이면 안쪽). 출발 거리는 원문의 비율
   (순둥이 0.1 … 떠돌이 0.95)을 **좌표로만** 쓴 것이고 수를 글자로 적지 않는다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **반지름으로 갈랐다.**
     불(accent) 글립은 중심에서 r ≤ 18(y 133~161 · x 166~186)이고 글로우(blur 최대 11)를
     다 세도 r ≤ 29 다. 흰 것(고리·집·라벨)은 전부 r ≥ 36 이다(안쪽 고리 50 − 집 반너비 14).
     7px 뜬다.

   🔴 라벨 자리는 좌표로 피한 게 아니라 **구조로 비웠다.** 여섯 각도(125·173·221·269·317·5)가
     전부 **90°(바로 아래)에서 ±35° 밖**이라, 아래로 뻗는 세로 통로에는 어느 애니메이션
     값에서도 집이 안 들어온다. 최악(안쪽 고리에 다 모인 순간)에도 가장 가까운 집(125°)의
     상자가 y 173~203 이고 「안쪽」 라벨 상자는 y 211~228 이다 — 8px 뜬다.

   세로 예산(캔버스 307): 최저 = 「바깥쪽」 글립 바닥 270(기준선 266). 37px 남는다.
   가로: 바깥 고리에 붙은 집(317°)의 오른쪽 끝 260. 캔버스 352, 여유 92px.

   계속 도는 것 = 빨려 붙는 주기(2.6초 · 여섯이 통째로 움직이는 면적) + 두 고리의 점선 회전
   + 불꽃 맥동. */

const CY = 150;
const R_IN = 50, R_OUT = 96;

/* 원문 비율을 **자리로만** 쓴다 — 화면에 수로 적지 않는다.
   각도는 ①서로 안 겹치고 ②90° 통로를 비우도록 잡은 값이고, 이 회차의 본문 네 그림이
   같은 각도를 쓴다(같은 마을을 네 번 다시 보는 것이라 자리가 바뀌면 안 된다). */
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

    /* 🔴 setLineDash 를 쓰지 않는다 — 긴 점선이 30fps 3패스에서 알파가 갈린 전례가 있다.
       호 조각을 직접 그리고 위상은 양수로 정규화한다(결정성). */
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

    /* 집 글립 — 상자 22×18 + 지붕. 세로 y−18~y+12 · 가로 x−14~x+14 */
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

    const c0 = cue(spec.mergeCue ?? 0, 0.15, 0.90);
    const st = ease(clamp(c0 / 0.14));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    if (spec.title) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
      ctx.restore();
    }

    /* ⏱ 뭉개지는 주기는 `since()` 위에 세웠다 — 자막 길이가 바뀌어도 사건 시각이 안 밀린다. */
    const s0 = since(spec.mergeCue ?? 0);
    const P = spec.loopSec ?? 2.6;
    const u = s0 >= 0 ? frac(s0 / P) : 0;
    const pull = ease(clamp((u - 0.18) / 0.30));          // 0 = 흩어진 출발 / 1 = 두 극단
    const relax = ease(clamp((u - 0.84) / 0.14));         // 되돌아가 다시 시작
    const k = pull * (1 - relax);

    dashRing(R_IN, 9, 8, t * 12, st * (0.45 + 0.55 * k));
    dashRing(R_OUT, 9, 8, -t * 9, st * (0.45 + 0.55 * k));

    /* ── 가운데 불 : 이 회차에서 강조색은 「불」 하나뿐이다 ── */
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

    /* ── 여섯 : 가까운 극단으로 빨려 붙는다 ─────────── */
    const MID = (R_IN + R_OUT) / 2;
    for (const s of SEATS) {
      const r0 = rOf(s.ratio);
      const r = lerp(r0, r0 < MID ? R_IN : R_OUT, k);
      const a = s.deg * Math.PI / 180;
      house(CX + r * Math.cos(a), CY + r * Math.sin(a), st, 1);
    }

    /* ── 두 이름은 뭉개지고 나서야 켜진다 ───────────── */
    if (spec.innerLabel) ctext(spec.innerLabel, CX, CY + R_IN + 24, 900, 14, tone('ink'), 108, st * k);
    if (spec.outerLabel) ctext(spec.outerLabel, CX, CY + R_OUT + 20, 900, 14, tone('ink'), 128, st * k);

    ctx.textAlign = 'left';
  }
};
