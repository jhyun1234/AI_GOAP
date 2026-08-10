import { ease, clamp, lerp, frac, disp, mono, tone, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* overrun — 뒤집힘과 남는 한 줄. 회차 축(「한 칸의 폭」)의 **한계** 배치다.
   S2 의 자를 그대로 되받되 뒤집는다 — 거기서는 폭이 **내려갔고**, 여기서는
   그 폭이 부른 결과를 보여 준 뒤 폭을 **올려 못 내려가게 잠근다.**

   cue 0 : 촘촘한 폭(1)으로 걸음 칸이 오른쪽으로 계속 자라 세로 「탐색 한도」
           선을 넘어간다. 넘어간 칸은 사그라들고 「계획 실패」가 뜬다.
   cue 1 : 값이 1 에서 8 로 올라 칸이 굵어지고 줄이 한도선 **앞에서** 멈춘다.
           그리고 축선 아래에 강조색 「기준선」이 새로 그어진다 — 그 밑에서
           올라오려던 작은 값 칸이 선에 닿기 전에 점선으로 풀려 사라지고,
           그 자리에 「테스트가 먼저 실패」가 선다. 0.8초 주기로 되풀이된다.
           ("고쳤다" 는 값 하나이고, "다시는 안 생기게 했다" 는 이 선이다.)

   🔴 흰 것과 강조색이 닿지 않게 **부등식으로** 막았다.
     · 걸음 칸·한도선(흰) = 축선 **위**(176~216) / 기준선(강조색) = 축선 **아래**(250)
     · 되풀이되는 값 칸(흰)의 윗변은 `GATE_Y + 16 = 266` 이 **최댓값**이다.
       기준선 획(5px)의 아랫변이 252.5 이므로 어떤 위상에서도 13.5px 이 뜬다.
       (움직이는 요소는 가장 밖으로 나가는 순간으로 재라는 규칙 — 여기서는
        가장 위로 올라오는 순간이다.)
     · 값 칸의 아랫변 최댓값은 302 → 캔버스 307 안. 시작 위치 288 에서 재도 같다.
   🔴 넘어간 칸은 한도선에서 40px 안에서 알파가 0 이 된다(오른쪽 끝 310).
      화면 밖으로 나가 잘린 것처럼 보이지 않게 하려는 것이다.
   🔴 글로우는 이 회차에서 한 곳도 안 썼다.

   세로 예산(캔버스 307) — 구획 이름 40 · 값 74 · MAX_NODES 128 · 탐색 한도 146 ·
   한도선 156~216 · 걸음 칸 176~200 · 축선 200 · 판정 232 · 기준선 250 ·
   되풀이 칸 266~302. 최저 302. */

const M = 22;
const AXIS = 200;
const BLK_H = 24;
const GATE_Y = 250;
const CHIP_W = 22, CHIP_H = 14;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    /* 🔴 문자열은 절대 줄이지 않는다(원문 근거를 가진 자산이다).
       폭이 넘치면 글자 크기를 낮추고, 정렬 기준점도 캔버스 안쪽에 가둔다. */
    const put = (txt, cx, by, mk, size, color, maxW) => {
      if (!txt) return;
      let fs = size; ctx.font = mk(fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = mk(fs); }
      const tw = ctx.measureText(txt).width;
      ctx.fillStyle = color;
      ctx.textAlign = 'center';
      ctx.fillText(txt, clamp(cx, M + tw / 2, w - M - tw / 2), by);
      ctx.textAlign = 'left';
    };

    const CAP = w - 88;                              // 탐색 한도 선의 x
    const c0 = cue(spec.overCue ?? 0, 0.15, 0.85);
    const grow = ease(clamp((c0 - 0.08) / 0.52));    // 줄이 자라 한도를 넘는다
    const c1 = cue(spec.fixCue ?? 1, 0.15, 0.80);
    const fix = ease(clamp(c1 / 0.36));              // 값이 1 → 8
    const gate = ease(clamp((c1 - 0.52) / 0.28));    // 기준선이 그어진다

    /* ── 구획 이름 ─────────────────────────────────── */
    ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
    ctx.fillText(spec.panelLabel ?? '', M, 40);

    /* ── 최소 비용 : 1 이 8 로 올라간다 ─────────────── */
    ctx.font = disp(700, 12.5); ctx.fillStyle = tone('sub');
    ctx.fillText(spec.minLabel ?? '', M, 74);
    {
      const vx = M + 4 + ctx.measureText(spec.minLabel ?? '').width + 14;
      ctx.font = disp(900, 26);
      const g = clamp((fix - 0.05) / 0.34);
      ctx.save();
      ctx.globalAlpha = 1 - g;
      ctx.fillStyle = tone('accent');
      ctx.fillText(String(spec.minFrom ?? ''), vx, 74 - 14 * g);
      ctx.restore();
      ctx.save();
      ctx.globalAlpha = g;
      ctx.fillStyle = tone('accent');
      ctx.fillText(String(spec.minTo ?? ''), vx, 74 + 12 * (1 - g));
      ctx.restore();
    }

    /* ── 탐색 한도 (세로선 + 라벨 둘) ────────────────── */
    put(spec.capQuote, CAP, 128, s => mono(700, s), 10, tone('sub'), 132);
    put(spec.capLabel, CAP, 146, s => disp(800, s), 12.5, tone('ink'), 132);
    {
      ctx.save();
      ctx.strokeStyle = tone('ink');
      ctx.lineWidth = 5 + 2 * clamp(grow * (1 - gate));
      ctx.beginPath(); ctx.moveTo(CAP, 156); ctx.lineTo(CAP, 216); ctx.stroke();
      ctx.restore();
    }

    /* ── 축선 (계속 흐른다) ─────────────────────────── */
    ctx.save();
    ctx.setLineDash([9, 8]);
    ctx.lineDashOffset = -((t * 17) % 17);
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(M, AXIS); ctx.lineTo(w - M, AXIS); ctx.stroke();
    ctx.restore();

    /* ── 걸음 칸이 자란다 ───────────────────────────
       폭은 cue 1 에서 굵어지고, 그때 줄 끝은 한도선 앞으로 물러난다. */
    const pitch = lerp(spec.pitchFrom ?? 6, spec.pitchTo ?? 26, fix);
    const endX = lerp(M + (CAP - M) * grow + 46 * grow, CAP - 34, fix);
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    for (let x = M; x + pitch - 2 < endX; x += pitch) {
      const over = x + pitch - 2 - CAP;
      const a = over <= 0 ? 1 : clamp(1 - over / 40);
      if (a <= 0.02) continue;
      ctx.save();
      ctx.globalAlpha = a;
      roundRect(ctx, x, AXIS - BLK_H, Math.max(3, pitch - 2), BLK_H, 2);
      ctx.stroke();
      ctx.restore();
    }

    /* ── 판정 한 줄 ─────────────────────────────────
       cue 0 에서는 흰 「계획 실패」, cue 1 에서는 강조색 「테스트가 먼저 실패」.
       둘은 알파로 교차하고 같은 baseline 을 쓴다. */
    {
      /* 🔴 두 판정은 같은 baseline·같은 왼쪽 끝을 쓴다. 흰 「계획 실패」가 **완전히
         꺼진 뒤에야**(gate 0.30) 강조색 「테스트가 먼저 실패」가 뜨게 구간을 띄웠다
         — 겹치는 순간 흰색과 강조색이 같은 픽셀에 얹힌다(게이트가 못 보는 자리다). */
      const a0 = clamp((grow - 0.72) / 0.20) * (1 - clamp(gate / 0.30));
      if (a0 > 0.02) {
        ctx.save(); ctx.globalAlpha = a0;
        ctx.font = disp(800, 15); ctx.fillStyle = tone('ink');
        ctx.fillText(spec.failLabel ?? '', M, 232);
        ctx.restore();
      }
      if (gate > 0.35) {
        ctx.save(); ctx.globalAlpha = clamp((gate - 0.35) / 0.30);
        ctx.font = disp(800, 15); ctx.fillStyle = tone('accent');
        ctx.fillText(spec.gateVerdict ?? '', M, 232);
        ctx.restore();
      }
    }

    /* ── 기준선 (강조색) + 그 아래에서 올라오다 풀리는 값 ── */
    if (gate > 0.02) {
      ctx.save();
      ctx.globalAlpha = clamp(gate * 1.5);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 5;
      ctx.beginPath();
      ctx.moveTo(M, GATE_Y);
      ctx.lineTo(M + (w - M * 2) * ease(clamp(gate * 1.2)), GATE_Y);
      ctx.stroke();
      ctx.restore();

      if (gate > 0.5) {
        ctx.save();
        ctx.globalAlpha = clamp((gate - 0.5) / 0.3);
        ctx.font = disp(800, 12);
        const s = spec.gateLabel ?? '';
        const tw = ctx.measureText(s).width;
        ctx.fillStyle = tone('accent');
        ctx.fillText(s, w - M - tw, 241);
        ctx.restore();
      }

      /* 되풀이 — 작은 값이 올라오다 선에 닿기 전에 점선으로 풀린다 */
      if (gate > 0.6) {
        const ph = frac(t / 0.8);
        const up = ease(clamp(ph / 0.62));
        const y = lerp(288, GATE_Y + 16, up);       // 윗변 최댓값 266
        const die = clamp((ph - 0.62) / 0.26);
        ctx.save();
        ctx.globalAlpha = clamp((gate - 0.6) / 0.25) * (1 - die);
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        if (die > 0.05) { ctx.setLineDash([5, 5]); ctx.lineDashOffset = -(t * 10) % 10; }
        roundRect(ctx, M + 78, y, CHIP_W, CHIP_H, 3); ctx.stroke();
        ctx.restore();
      }
    }
  }
};
