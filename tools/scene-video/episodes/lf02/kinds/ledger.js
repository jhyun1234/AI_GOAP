import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* ledger — 실측 원장. 다섯 칸이 위에서부터 한 줄씩 채워지고 마지막 게이트 칸만
   강조색으로 잠긴다. 수치는 전부 실측이고 기준 시각은 **2026-08-09 16:20**이다
   (3차 개정 C12 — 같은 날 안에서 세 번 낡았다: 오전 → 15:30 → 16:20).
   여기 없는 수는 그리지 않는다.

   🔴 4차 정정 (검수 C13): 「16:20 기준」을 선언해 놓고 `SO ASSETS · SPECS` 를
   **재보지 않고 「변동 없음」으로 적었다.** 그 시각 트리 실측은 **116 · 32** 였고
   (에셋은 15:46 → 15:57 → 16:06 에 110 → 112 → 114 → 116, 명세서는 15:33 에 31 → 32),
   커밋도 16:19 분 하나가 빠져 **798 / 1009** 였다. 기준 시각을 선언한 수치는
   그 시각 트리에서 **전수 재측정**한다 — 「변동 없음」 자기보고 금지.

   🔴 2차 개정 (검수 R1): 1차본은 라벨(baseline ry+17)과 값(baseline ry+21, 20px)을
   같은 세로 자리에 넣어 `786 / 997` 이 `COMMITS  game / repo` 를 통째로 덮었다
   (증거 `build/stills/120s.png`). 행 높이가 35px 뿐인데 두 줄을 쌓으려 한 것이 원인이다.
   → **라벨은 왼쪽, 값은 오른쪽 정렬로 같은 baseline 에 놓는다.** 세로로 겹칠 수가 없고,
   가로는 값 폰트를 measureText 로 줄여 남는 폭 안에 가둔다.

   🔴 2차 개정 (검수 C6): 연속 모션이 레일 위 삼각 커서(폭 11px)뿐이라 프레임 간
   변화량이 문턱(0.0008) 아래였다. → **행 전체를 덮으며 내려가는 판독 띠**로 바꿨다.
   면적이 통과 선례(`antipattern` 의 40px 띠)보다 크고, 뜻도 그대로다 — 이 원장은
   커밋마다 다시 세어진다. 띠는 행보다 **먼저** 그려 강조색 위에 흰색이 얹히지 않게 한다.

   🔴 3차 개정 (검수 R-1): 그 판독 띠가 `아래 가장자리 잘림 · S5 1673px` 를 냈다.
   `by` 를 `h - 22` 까지 보내 놓고 높이 `rowH - 6` 을 거기서부터 그렸으니 주기마다
   7.6px 이 캔버스 밖으로 나갔다(증거 `build/stills/100.8s.png` — 마지막 칸 아래 띠가
   반만 남아 있다). → **띠의 「바닥」이 레일 끝을 넘지 않도록 `by` 의 상한을
   `railBottom - bandH` 로 물렸다.** 높이를 잘라 클램프하면 띠가 주기 끝에서 납작해지며
   면적이 줄어 C6(정적)이 되살아나므로, 높이는 고정하고 시작점만 물린다. */

const ROWS = [
  { k: 'DAYS', v: '46' },
  { k: 'COMMITS  game / repo', v: '798 / 1009' },
  { k: 'C# SCRIPTS', v: '116 files · 20,365 lines' },
  { k: 'SO ASSETS · SPECS', v: '116 · 32' },
  { k: 'EDITMODE GATES', v: '386 / 386' },
];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    const railX = M + 6;
    const y0 = 46, rowH = (h - y0 - 22) / ROWS.length;
    const bx = railX + 14, bw = w - M - bx;

    // 헤더 — 기준 기간
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('2026-06-24  ->  2026-08-09', M, 26);

    /* ── 연속 모션 : 행을 덮으며 내려가는 판독 띠 (행보다 먼저) ──
       🔴 바닥 클램프(3차 개정 R-1): 띠 「바닥」이 railBottom 을 넘지 않는다.
       railBottom 은 아래 레일 끝(h - 18)보다 4px 안쪽이라, 어떤 t 에서도
       fillRect 가 캔버스 h 를 넘지 않는다. */
    {
      const PR = 4.6, k = (t % PR) / PR;
      const bandH = rowH - 6;
      const railBottom = h - 22;
      const by = lerp(y0 - 10, railBottom - bandH, k);
      ctx.fillStyle = tone('track');
      ctx.fillRect(bx - 10, by, bw + 10, bandH);
    }

    // 레일
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(railX, y0 - 8); ctx.lineTo(railX, h - 18); ctx.stroke();

    for (let i = 0; i < ROWS.length; i++) {
      const ry = y0 + i * rowH;
      const k = ease(clamp(cue(i)));
      const last = i === ROWS.length - 1;
      const on = k > 0.05;
      const bh = rowH - 9;
      const base = ry + bh / 2 + 6;            // 라벨·값이 공유하는 baseline

      // 칸
      ctx.strokeStyle = on ? (last && k > 0.5 ? tone('accent') : tone('ink')) : tone('track');
      ctx.lineWidth = 3;
      roundRect(ctx, bx, ry, bw, bh, 3); ctx.stroke();

      // 라벨 — 왼쪽
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      const lx = bx + 12;
      ctx.fillText(ROWS[i].k, lx, base - 1);
      const lw = ctx.measureText(ROWS[i].k).width;

      if (!on) continue;

      // 값 — 오른쪽 정렬. 라벨이 차지하고 남은 폭 안에 가둔다.
      const avail = bw - 12 - lw - 24 - 12;
      let fs = 20;
      ctx.font = disp(900, fs);
      while (ctx.measureText(ROWS[i].v).width > avail && fs > 11) {
        fs -= 1; ctx.font = disp(900, fs);
      }
      const vw = ctx.measureText(ROWS[i].v).width;
      const vx = bx + bw - 12 - vw;
      // 오른쪽에서 왼쪽으로 드러난다
      ctx.save();
      ctx.beginPath(); ctx.rect(vx + vw * (1 - k), ry + 2, vw * k, bh - 4); ctx.clip();
      ctx.fillStyle = last && k > 0.5 ? tone('accent') : tone('ink');
      ctx.fillText(ROWS[i].v, vx, base + 1);
      ctx.restore();
    }
  },
};
