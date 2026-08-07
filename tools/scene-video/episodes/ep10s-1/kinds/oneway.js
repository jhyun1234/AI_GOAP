import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* oneway — 하나였던 줄이 둘로 갈라지고, 되돌아가는 쪽만 굵어진다.

   원문: "처음엔 'A와 B의 사이'를 숫자 하나로 저장할까 했는데, 곧 그게 틀렸다는 걸
   깨달았습니다. 잔소리를 들은 쪽이 더 서운한 법이니까요. 그래서 'A가 B를 어떻게
   느끼는가'와 'B가 A를 어떻게 느끼는가'를 따로 기록했습니다. 잔소리를 주고받으면
   듣는 쪽의 원한이 더 크게 쌓이도록 했습니다."

   🔴 이 회차의 기본 도형(두 사람 사이의 굽은 줄)이 여기서 **정반대를 말한다.**
   S1 에서 같은 줄이 그어지자마자 지워졌다면, 여기서는 갈라져서 한쪽이 커진다.
   회차 안에서 같은 어휘를 되받는 것은 뒤집을 때만 허용이고, 이 자리가 그 자리다.

   🔴 굵어지는 것은 **아래 줄(B→A)** 이다. 왼쪽 A 가 잔소리를 했고 오른쪽 B 가 들었으니,
   커지는 값은 "B 가 A 를 어떻게 느끼는가"다. 화살촉이 오른쪽에서 왼쪽으로 되돌아가는
   방향인 것이 그 근거를 지고 있다. 방향을 반대로 그리면 원문과 어긋난다.

   🔴 화면에 적는 수는 갈라지기 전의 `1` 하나뿐이고, 원문 "숫자 하나"에서 왔다.
   갈라진 뒤의 개수는 **적지 않았다** — 원문에 `2` 라는 글자가 없다. 도형이 둘이라는
   사실만 보여 준다.

   움직임 두 층:
     ① t — 두 줄에 걸린 굵은 대시가 각자 제 방향으로 계속 흐른다(위는 A→B, 아래는 B→A).
        굵기 16px 짜리 줄에서 10px 짜리 틈이 다섯 개 미끄러지므로 프레임마다 갱신되는
        면적이 크다 — 이 샷은 자막이 하나뿐이라 여기가 정적 구간 대책이다.
     ② cue — splitCue 안에서 갈라지고(0.22~0.52), 그다음 아래 줄만 굵어진다(0.62~0.94). */

const CY = 152;
const R = 15;
const BOW = 112;     // 갈라진 줄의 제어점이 위아래로 벌어지는 폭(정점은 그 절반)

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    /* 가운데 정렬 글자는 기준점 자체를 캔버스 안쪽에 가둔다 */
    const midText = (txt, weight, px, cx, y, cw, pad = 14) => {
      ctx.font = disp(weight, px);
      const tw = ctx.measureText(txt).width;
      ctx.textAlign = 'center';
      ctx.fillText(txt, clamp(cx, pad + tw / 2, cw - pad - tw / 2), y);
      ctx.textAlign = 'left';
    };

    const X0 = 18, X1 = w - 18;
    const LX = X0 + 44, RX = X1 - 44;
    const ax0 = LX + 18, ax2 = RX - 18, mid = (ax0 + ax2) / 2;

    const c0 = cue(spec.splitCue ?? 0, 0.15, 0.85);
    const app = clamp(c0 * 6);
    if (app <= 0.02) return;

    const sk = ease(clamp((c0 - 0.22) / 0.30));    // 갈라진다
    const gk = ease(clamp((c0 - 0.62) / 0.32));    // 되돌아가는 쪽이 굵어진다

    const ptOn = (cyq, k) => {
      const m = 1 - k;
      return [m * m * ax0 + 2 * m * k * mid + k * k * ax2,
              m * m * CY + 2 * m * k * cyq + k * k * CY];
    };
    const pathOn = cyq => {
      ctx.beginPath();
      const N = 44;
      for (let i = 0; i <= N; i++) {
        const [x, y] = ptOn(cyq, i / N);
        if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
      }
    };
    const head = (cyq, k, back) => {
      const [x, y] = ptOn(cyq, k);
      const [x2, y2] = ptOn(cyq, k + (back ? 0.07 : -0.07));
      const ang = Math.atan2(y - y2, x - x2);
      ctx.beginPath();
      for (const s of [-1, 1]) {
        ctx.moveTo(x, y);
        ctx.lineTo(x - 13 * Math.cos(ang - s * 0.44), y - 13 * Math.sin(ang - s * 0.44));
      }
      ctx.stroke();
    };

    const upCy = lerp(CY, CY - BOW, sk);
    const dnCy = lerp(CY, CY + BOW, sk);
    const splitA = app * clamp((sk - 0.02) / 0.14);

    /* ── 갈라진 두 줄 — 강조색을 먼저 깐다(흰 것보다 아래) ── */
    if (splitA > 0.02) {
      ctx.lineCap = 'round';

      /* 위 — A→B. 얇게 남는다 */
      ctx.globalAlpha = splitA * 0.6;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.setLineDash([22, 10]);
      ctx.lineDashOffset = -(t * 30) % 32;
      pathOn(upCy); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      head(upCy, 1, false);

      /* 아래 — B→A. 듣는 쪽이 되돌려 주는 값이라 이쪽만 굵어진다 */
      ctx.globalAlpha = splitA;
      setShadow(ctx, GLOW, 10 * gk, 0);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = lerp(4, 16, gk);
      ctx.setLineDash([26, 10]);
      ctx.lineDashOffset = (t * 30) % 36;
      pathOn(dnCy); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      clearShadow(ctx);
      ctx.lineWidth = lerp(4, 7, gk);
      head(dnCy, 0, true);

      ctx.lineCap = 'butt';
      ctx.globalAlpha = 1;
    }

    /* ── 갈라지기 전 — 곧은 줄 하나 ─────────────────── */
    const oneA = app * clamp(1 - sk / 0.16);
    if (oneA > 0.02) {
      ctx.globalAlpha = oneA;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 7;
      ctx.lineCap = 'round';
      ctx.beginPath(); ctx.moveTo(ax0, CY); ctx.lineTo(ax2, CY); ctx.stroke();
      ctx.lineCap = 'butt';
      ctx.fillStyle = tone('ink');
      midText(spec.oneLabel ?? '', 900, 15, mid, CY - 18, w);
      ctx.globalAlpha = 1;
    }

    /* ── 라벨 — 갈라진 뒤에 각자 이름을 얻는다 ──────── */
    const labA = app * clamp((sk - 0.45) / 0.35);
    if (labA > 0.02) {
      ctx.globalAlpha = labA * 0.85;
      ctx.fillStyle = tone('sub');
      midText(spec.outLabel ?? '', 800, 12.5, mid, CY - BOW / 2 * sk - 14, w);
      ctx.globalAlpha = labA;
      ctx.fillStyle = tone('ink');
      midText(spec.backLabel ?? '', 900, 13.5, mid, CY + BOW / 2 * sk + 26, w);
      ctx.globalAlpha = 1;
    }

    /* ── 주민 둘 ────────────────────────────────────── */
    for (const px of [LX, RX]) {
      ctx.globalAlpha = app;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
      ctx.beginPath(); ctx.arc(px, CY, R, 0, Math.PI * 2); ctx.stroke();
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
