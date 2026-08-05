import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* oneheap — 여덟이 일감 하나로 빨려든다.

   이 편의 0초는 "성격을 줬는데도 여덟 명이 똑같은 일만 했다"이다. 직전 편이 그린 어긋남은
   **타이밍이 같다**(다섯이 동시에 배고프다)였고, 이 편의 어긋남은 **일감이 같다**이다.
   그래서 나란한 트랙을 다시 그리지 않는다. 여기서는 여덟이 서로 다른 자리에서 출발해
   **한 카드로 수렴한다.** 도착한 뒤 여덟이 구별되지 않는 것이 사건이다.

   heapCue 에서 표식 여덟이 왼쪽 카드로 하나씩 빨려들고, swapCue 에서 그 덩어리가 통째로
   오른쪽 카드로 옮겨간다. 원문의 인용("나무가 필요하네. 다 같이 나무 하러 가자.")이
   말하는 것이 정확히 이 두 동작이다 — 일감이 바뀌어도 덩어리는 안 갈라진다.

   🔴 몰려 앉은 여덟은 **같은 주기**로 돈다. 위상만 다르고 속도가 같아서 덩어리 모양이
   안 변한다 — 이것이 '구별되지 않는다'의 그림이고, 마지막 샷(eachday)에서 같은 여덟이
   저마다 다른 주기로 왕복하는 것과 정확히 대비된다.

   🔴 등장은 전부 cue 에 물렸다. t 로 도는 것은 궤도 회전과 카드 테두리 dash, 유도선
   dash 뿐이고 이것들은 문턱을 가지지 않는다. */

const ORBIT = 3.2;        // 몰린 표식이 한 바퀴 도는 시간(초) — 여덟이 공유한다
/* 🔴 궤도를 납작한 타원으로 잡는다. 처음엔 반경 9 짜리 원이었는데 표식 여덟(r 5.5)이
   서로 덮여 **꽃 모양 얼룩 하나**로 렌더됐다 — 여덟이라는 것도, 사람이라는 것도 안 읽혔다.
   가로로 벌리면 여덟이 각각 보이면서도 한 무리로 읽힌다. 뭉쳐 있다는 뜻은 겹침이 아니라
   **한 카드 아래 다 모여 있다**는 배치가 진다. */
const ORBIT_RX = 30, ORBIT_RY = 15;

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

    const N = spec.people ?? 8;
    const cards = spec.cards ?? ['WOOD', 'FOOD'];
    const hk = ease(cue(spec.heapCue ?? 0, 0.15, 0.75));
    const sk = ease(cue(spec.swapCue ?? 1, 0.15, 0.8));

    /* 카드 둘 — 왼쪽이 처음 일감, 오른쪽이 바뀐 일감 */
    const CW = 84, CH = 46, CY = 132;
    const cx = i => w * (i === 0 ? 0.29 : 0.71);
    const heapY = CY + CH + 42;

    /* ── 상단 라벨 ─────────────────────────────────── */
    if (spec.poolLabel) {
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.poolLabel, 8, 28);
    }

    /* ── 출발 자리 — 여덟이 흩어져 서 있다 ───────────── */
    const X0 = 26, X1 = w - 26;
    const startX = i => X0 + (X1 - X0) * (i / (N - 1));
    const startY = i => 62 + (i % 2 === 0 ? 0 : 13);

    /* ── 카드 ──────────────────────────────────────── */
    const active = sk > 0.5 ? 1 : 0;
    for (let c = 0; c < cards.length; c++) {
      const a = c === 0 ? clamp(hk * 2.6) : clamp(sk * 2.2);
      if (a <= 0.02) continue;
      const x = cx(c) - CW / 2;
      const on = c === active;

      ctx.globalAlpha = a;
      ctx.setLineDash([9, 7]);
      ctx.lineDashOffset = -(t * 13) % 16;
      ctx.strokeStyle = on ? tone('accent') : tone('track');
      ctx.lineWidth = on ? 4 : 3;
      roundRect(ctx, x, CY, CW, CH, 8); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;

      ctx.textAlign = 'center';
      const fs = fit(cards[c], 900, 19, CW - 16, 11);
      ctx.font = disp(900, fs);
      ctx.fillStyle = on ? tone('accent') : tone('sub');
      ctx.fillText(cards[c], cx(c), CY + CH / 2 + fs * 0.36);
      ctx.globalAlpha = 1;
    }

    /* ── 덩어리의 중심 — 카드가 바뀌면 통째로 옮겨간다 ── */
    const heapX = lerp(cx(0), cx(1), ease(clamp(sk * 1.25)));

    /* ── 표식 여덟 ─────────────────────────────────── */
    const arrive = i => ease(clamp(hk * 2.2 - i * 0.14));

    for (let i = 0; i < N; i++) {
      const k = arrive(i);
      const sx = startX(i), sy = startY(i);

      // 아직 안 간 표식에는 유도선이 붙는다
      if (k > 0.05 && k < 0.98) {
        ctx.globalAlpha = 0.5 * (1 - k);
        ctx.setLineDash([7, 6]);
        ctx.lineDashOffset = -(t * 15) % 13;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.moveTo(sx, sy); ctx.lineTo(heapX, heapY); ctx.stroke();
        ctx.setLineDash([]); ctx.lineDashOffset = 0;
        ctx.globalAlpha = 1;
      }

      // 도착한 뒤에는 같은 주기·다른 위상으로 돈다
      const ang = frac(t / ORBIT) * Math.PI * 2 + i * (Math.PI * 2 / N);
      const ox = heapX + Math.cos(ang) * ORBIT_RX;
      const oy = heapY + Math.sin(ang) * ORBIT_RY;

      const px = lerp(sx, ox, k), py = lerp(sy, oy, k);

      ctx.fillStyle = k > 0.6 ? tone('accent') : tone('ink');
      if (k > 0.6) setShadow(ctx, GLOW, 7, 0);
      ctx.beginPath(); ctx.arc(px, py, 5.5, 0, Math.PI * 2); ctx.fill();
      clearShadow(ctx);
    }

    /* 덩어리 테두리 — 여덟이 하나로 읽히게 */
    const heaped = clamp(hk * 2.2 - (N - 1) * 0.14);
    if (heaped > 0.02) {
      ctx.globalAlpha = heaped * 0.55;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.ellipse(heapX, heapY, ORBIT_RX + 13, ORBIT_RY + 12, 0, 0, Math.PI * 2);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 인용 한 줄 ────────────────────────────────── */
    if (spec.quote) {
      const k = clamp((sk - 0.55) / 0.45);
      if (k > 0.02) {
        ctx.globalAlpha = k;
        ctx.textAlign = 'center';
        const fs = fit(spec.quote, 800, 13.5, w - 22, 9);
        ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
        ctx.fillText(spec.quote, w / 2, 284);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
