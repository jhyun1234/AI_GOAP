import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect } from '../../../engine/lib.js';

/* zerocode — 에셋 하나로 액션이 늘어나는 자리. 왼쪽 인스펙터의 칸이 채워지면
   오른쪽 주민이 곧바로 그 일을 하러 간다. 코드 편집기는 화면에 아예 없다 —
   "코드를 한 줄도 쓰지 않았다"를 글자가 아니라 **빈 자리**로 말한다.
   문자열 출처: M0 글 53행(돌 채집 말풍선) · Assets/M0Config/Actions/MineStone.asset. */

const FIELDS = [
  { k: 'Preconditions', v: 'HasTool' },
  { k: 'Effects', v: 'Stone +1' },
  { k: 'Cost', v: '1.0' },
];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    const kAsset = ease(cue(0));
    const kFill = ease(cue(1));
    const kRun = ease(cue(2));
    const kLater = ease(cue(3));   // 나중에 재해·위협도 같은 값으로 들어왔다

    // ── 인스펙터 카드 ─────────────────────────────
    const cx = M, cy = 34, cw = 288, ch = h - 34 - 42;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.globalAlpha = clamp(0.2 + 0.8 * kAsset);
    roundRect(ctx, cx, cy, cw, ch, 4); ctx.stroke();
    ctx.globalAlpha = 1;
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('INSPECTOR', cx, cy - 10);
    ctx.font = disp(800, 14); ctx.fillStyle = tone('ink');
    ctx.fillText('MineStone.asset', cx + 12, cy + 24);
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(cx + 12, cy + 34); ctx.lineTo(cx + cw - 12, cy + 34); ctx.stroke();

    /* 🔴 인스펙터를 훑는 초점 띠 — 자막과 무관하게 언제나 내려간다.
       1차본은 연속 모션이 주민 점 하나뿐이었고 그게 cue(2) 뒤에만 돌아서
       첫 두 자막 구간이 통째로 멈췄다(검수 실측 14.6초). 이 띠는 장식이 아니라
       "인스펙터에서 전제와 효과를 채웠다"는 그 행위 자체다. */
    {
      const P = 3.4, fk = (t % P) / P;
      const fy = cy + 44 + (ch - 88) * fk;   // 카드 안쪽에 가둔다 (아래 테두리를 넘지 않게)
      ctx.fillStyle = tone('track');
      ctx.fillRect(cx + 6, fy, cw - 12, 26);
    }

    for (let i = 0; i < FIELDS.length; i++) {
      const fy = cy + 52 + i * 30;
      const k = clamp((kFill - i * 0.22) / 0.5);
      ctx.font = mono(700, 11); ctx.fillStyle = tone('sub');
      ctx.fillText(FIELDS[i].k, cx + 12, fy + 12);
      const bx = cx + 132, bw = cw - 144;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, bx, fy, bw, 21, 3); ctx.stroke();
      if (k > 0.02) {
        ctx.save();
        ctx.beginPath(); ctx.rect(bx + 2, fy + 2, (bw - 4) * ease(k), 17); ctx.clip();
        ctx.font = mono(700, 11); ctx.fillStyle = tone('accent');
        ctx.fillText(FIELDS[i].v, bx + 9, fy + 15);
        ctx.restore();
      }
    }

    // ── 오른쪽 : 주민이 곧바로 하러 간다 ─────────────
    const px0 = cx + cw + 42, px1 = w - M - 46;
    const py = h * 0.52;
    // 채석 지점
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(px0 - 6, py + 34); ctx.lineTo(px1 + 26, py + 34); ctx.stroke();
    ctx.strokeStyle = tone('ink');
    roundRect(ctx, px1 + 6, py + 6, 26, 26, 3); ctx.stroke();
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('STONE', px1 - 4, py + 52);

    if (kRun > 0.05) {
      const kk = (t % 2.6) / 2.6;
      const x = lerp(px0, px1, ease(Math.min(1, kk * 1.35)));
      ctx.fillStyle = tone('accent');
      ctx.beginPath(); ctx.arc(x, py + 18, 9, 0, Math.PI * 2); ctx.fill();
      // 말풍선
      const s = '돌 채집';
      ctx.font = disp(800, 14);
      const sw = ctx.measureText(s).width;
      const bx = clamp(x - sw / 2 - 11, px0 - 30, w - M - sw - 24);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, bx, py - 30, sw + 22, 26, 4); ctx.stroke();
      ctx.fillStyle = tone('ink');
      ctx.fillText(s, bx + 11, py - 11);
      ctx.globalAlpha = clamp(kRun);
    }
    ctx.globalAlpha = 1;

    // ── 코드 계기 ─────────────────────────────────
    ctx.font = mono(700, 11); ctx.fillStyle = tone('sub');
    ctx.fillText('CODE WRITTEN', M, h - 12);
    ctx.font = disp(900, 22); ctx.fillStyle = tone('accent');
    const z = '0';
    const zw = ctx.measureText(z).width;
    ctx.fillText(z, M + 96, h - 8);
    ctx.font = mono(700, 11); ctx.fillStyle = tone('sub');
    ctx.fillText('LINES', M + 96 + zw + 7, h - 12);

    // 같은 값으로 나중에 들어온 것들
    if (kLater > 0.02) {
      const chips = ['DISASTER', 'THREAT'];
      let x = M + 96 + zw + 7 + ctx.measureText('LINES').width + 18;
      for (let i = 0; i < chips.length; i++) {
        const k = clamp((kLater - i * 0.3) / 0.5);
        if (k <= 0.02) break;
        ctx.font = mono(700, 10);
        const cwid = ctx.measureText(chips[i]).width + 14;
        ctx.globalAlpha = k;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        roundRect(ctx, x, h - 26, cwid, 19, 3); ctx.stroke();
        ctx.fillStyle = tone('accent');
        ctx.fillText(chips[i], x + 7, h - 13);
        ctx.globalAlpha = 1;
        x += cwid + 10;
      }
    }
  },
};
