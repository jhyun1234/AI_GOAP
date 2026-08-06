import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* apart — 같은 겨울선을 둘이 함께 이고 있는데, 한 쪽만 문턱을 넘는다.

   원문: "부지런한 농사꾼은 겨울 준비를 최우선으로 두고, 고집쟁이는 예고가 떠도 끝까지
   하던 밭일을 붙듭니다. 같은 겨울을 봐도 결론이 갈리게 만든 겁니다."

   🔴 첫 샷(oneshove)의 뒤집기다. 거기서 문턱은 '전부 넘는 선'이었고 여기서는
   '한 쪽은 넘고 한 쪽은 안 넘는 선'이다. **같은 y(140)에 같은 점선**을 그린다 —
   회차 안에서 같은 도형을 두 번 쓰되 두 번째가 첫 번째를 뒤집을 때만 되받기가 된다.

   🔴 위쪽 겨울선은 **둘이 함께 이고 있다.** 원문이 '예고가 떠도'라고 썼으므로 고집쟁이
   에게도 예고는 닿았다 — 예고선을 한쪽에만 그으면 '겨울을 못 봤다'는 다른 말이 된다.
   갈리는 것은 그 아래의 값이다.

   🔴 두 막대의 빗금이 **서로 다른 속도**로 흐른다. 둘이 이제 다르다는 것 자체가
   이 샷에서 계속 도는 움직임이다(마지막 샷이라 아웃트로 카드가 뜨기 전 구간이 관건이다).

   🔴 두 꼭대기를 잇는 선은 **흰색**이다. 초록으로 그으면 문턱선(흰 점선)과 교차하는
   한 점에서 두 색이 같은 픽셀에 섞인다. 시작·끝을 막대의 바깥 모서리로 잡아
   막대 안쪽도 침범하지 않는다.

   🔴 **아웃트로 카드가 마지막 2.6초를 덮으므로 판정은 그 전에 끝나야 한다.**
   1차본은 연결선과 판정을 둘 다 마지막 자막(cue 1)에 걸었는데, 이 편의 마지막 자막이
   2.792초로 짧아 **카드가 그 자막의 81% 를 덮었다** — 카드가 뜨는 순간 판정의 알파가
   0.078 이었다(1차 검수 R1). 형제 편 ep08s-1 은 같은 구조인데 마지막 자막이 4.779초라
   통과했다. 갈린 것은 설계가 아니라 **자막 길이**다.
   그래서 연결선을 **첫 자막의 뒤 구간**(cue 0 의 0.70~1.0)으로 옮겨 카드보다 1.30초 먼저
   끝내고, 판정은 cue 1 에 두되 lead 0.55 · span 0.08 로 조여 카드보다 0.32초 먼저 앉힌다.
   🔑 연결선이 첫 자막으로 가는 것이 뜻으로도 맞다 — 갈라진 간격을 만드는 것은
   「농사꾼은 …, 고집쟁이는 …」 이고, 마지막 자막은 그 간격에 이름을 붙일 뿐이다.

   🔴 등장은 전부 cue 에 물렸다(값이 갈리는 것과 기울어진 선 = 0, 판정 = 1). */

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

    const X0 = 16, X1 = w - 16;
    const WY = 52, LINE = 140, BASE = 236, BW = 46;
    const LX = 96, RX = 256;                 // 두 사람의 자리
    const LH = 138, RH = 62;                 // 넘는 쪽 / 안 넘는 쪽

    const c0 = cue(spec.splitCue ?? 0, 0.15, 0.85);
    /* 🔴 lead 0.55 · span 0.08 — 아웃트로 카드보다 먼저 앉히기 위한 값이다(R1). */
    const c1 = cue(spec.verdictCue ?? 1, 0.55, 0.08);
    const lk = ease(clamp(c0 / 0.55));
    const rk = ease(clamp((c0 - 0.30) / 0.42));
    const ck = ease(clamp((c0 - 0.70) / 0.30));   // 연결선 — 첫 자막 뒤 구간

    const bar = (cx, h, up, off) => {
      if (h < 3) return;
      const x = cx - BW / 2, y = BASE - h;
      const col = up ? tone('accent') : tone('ink');
      ctx.fillStyle = tone('bg'); ctx.fillRect(x, y, BW, h);

      ctx.save();
      ctx.beginPath(); ctx.rect(x, y, BW, h); ctx.clip();
      ctx.strokeStyle = col; ctx.lineWidth = 3; ctx.globalAlpha = 0.48;
      for (let k = -h - 13; k < BW + 13; k += 13) {
        const o = k + off;
        ctx.beginPath();
        ctx.moveTo(x + o, y + h); ctx.lineTo(x + o + h, y);
        ctx.stroke();
      }
      ctx.restore();

      ctx.strokeStyle = col; ctx.lineWidth = 3;
      if (up) setShadow(ctx, GLOW, 8, 0);
      ctx.beginPath(); ctx.rect(x, y, BW, h); ctx.stroke();
      clearShadow(ctx);
    };

    /* ── 하나의 겨울 — 둘이 함께 이고 있다 ──────────── */
    const wk = clamp(c0 * 4.0);
    if (wk > 0.02) {
      ctx.globalAlpha = wk;
      ctx.setLineDash([13, 9]);
      ctx.lineDashOffset = (t * 15) % 22;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.beginPath(); ctx.moveTo(X0, WY); ctx.lineTo(X1, WY); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;

      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      for (const cx of [LX, RX]) {
        ctx.beginPath(); ctx.moveTo(cx, WY); ctx.lineTo(cx, WY + 10); ctx.stroke();
      }

      if (spec.winterLabel) {
        ctx.textAlign = 'center';
        const fs = fit(spec.winterLabel, 900, 13, 90, 9);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
        ctx.fillText(spec.winterLabel, w / 2, WY - 14);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 이름 둘 ────────────────────────────────────── */
    const names = [[LX, spec.left?.name, lk], [RX, spec.right?.name, rk]];
    for (const [cx, nm, k] of names) {
      if (!nm) continue;
      const a = clamp(k * 3);
      if (a <= 0.02) continue;
      ctx.globalAlpha = a;
      ctx.textAlign = 'center';
      const fs = fit(nm, 900, 15.5, 130, 10);
      ctx.font = disp(900, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(nm, cx, 88);
      ctx.globalAlpha = 1;
    }

    /* ── 문턱선(막대보다 먼저) ──────────────────────── */
    if (wk > 0.02) {
      ctx.globalAlpha = wk * 0.85;
      ctx.setLineDash([10, 8]);
      ctx.lineDashOffset = -(t * 12) % 18;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(X0, LINE); ctx.lineTo(X1, LINE); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;

      if (spec.lineLabel) {
        ctx.textAlign = 'right';
        const fs = fit(spec.lineLabel, 800, 11, 76, 8);
        ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.lineLabel, X1, LINE - 8);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 값 둘 — 여기서 갈린다 ──────────────────────── */
    bar(LX, LH * lk, BASE - LH * lk < LINE, -(t * 16) % 13);
    bar(RX, RH * rk, BASE - RH * rk < LINE, -(t * 9) % 13);

    if (wk > 0.02) {
      ctx.globalAlpha = wk * 0.8;
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(X0 + 30, BASE); ctx.lineTo(X1 - 30, BASE); ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 지금 무엇을 하고 있는가 ────────────────────── */
    const states = [
      [LX, spec.left?.state, clamp(lk * 2.4 - 1.2), true],
      [RX, spec.right?.state, clamp(rk * 2.4 - 1.2), false]
    ];
    for (const [cx, st, a, on] of states) {
      if (!st || a <= 0.02) continue;
      ctx.globalAlpha = a;
      ctx.textAlign = 'center';
      const fs = fit(st, 900, 12, 130, 9);
      ctx.font = disp(900, fs);
      ctx.fillStyle = on ? tone('accent') : tone('ink');
      ctx.fillText(st, cx, BASE + 22);
      ctx.globalAlpha = 1;
    }

    /* ── 갈라진 만큼 ────────────────────────────────
       두 막대의 바깥 모서리를 잇는다 — 막대 안쪽을 침범하지 않는다. */
    if (ck > 0.02) {
      const ax = LX + BW / 2, ay = BASE - LH;
      const bx = RX - BW / 2, by = BASE - RH;
      ctx.globalAlpha = clamp(ck * 1.6) * 0.9;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(ax, ay);
      ctx.lineTo(lerp(ax, bx, ck), lerp(ay, by, ck));
      ctx.stroke();
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(ax, ay, 4.5, 0, Math.PI * 2); ctx.fill();
      if (ck > 0.96) { ctx.beginPath(); ctx.arc(bx, by, 4.5, 0, Math.PI * 2); ctx.fill(); }
      ctx.globalAlpha = 1;
    }

    /* ── 판정 ───────────────────────────────────────── */
    if (spec.verdict) {
      const k = ease(c1);
      if (k > 0.02) {
        ctx.globalAlpha = k;
        ctx.textAlign = 'center';
        const fs = fit(spec.verdict, 900, 18, w - 30, 12);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
        ctx.fillText(spec.verdict, w / 2, 286 - 12 * (1 - k));
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
