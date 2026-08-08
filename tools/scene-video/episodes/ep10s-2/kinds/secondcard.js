import {
  disp, mono, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* secondcard — 두 번째 부탁이 빈 자리에 꽂히고, 그제야 선반이 된다.

   축이 여기서 처음으로 세로에서 가로로 눕는다. 앞 네 그림은 「목표 칸 하나 아래로
   뻗는 사슬」이었고, 이 그림은 그 사슬이 **여러 벌 꽂히는 자리**가 됐다는 이야기다.

   🔴 선반을 처음부터 그려 놓지 않는다. 카드가 먼저 꽂히고, **맨 마지막에 판이
      그어져** 그제야 선반이 된다. 원문의 순서가 그렇다 — '부탁 시스템이 진짜
      "선반"으로 완성됐는지 **증명하려면**, 새 부탁을 코드 없이 추가할 수 있어야
      합니다'. 증명이 먼저고 선반은 결과다.

   🔴 **2026-08-08 개정 — 이 샷은 더 이상 마지막 샷이 아니다**(4단 고정 구조 ADR-V25-8).
      예고 자막이 새 아웃트로 샷(nextpeek)으로 빠져나가면서 이 샷의 lines 가 **한 줄**이
      됐다. 그래서 옛 `shelfCue: 1` 은 무효다 — engine.js 의 cue 는 인덱스를
      `min(i, rel.length-1)` 로 접으므로, 1 을 그대로 두면 **선반 판이 0번 자막 시작과
      동시에 그어져** 카드가 꽂히기도 전에 선반이 완성된다.
      🔑 고친 방식: 선반을 **같은 자막의 뒷구간**으로 옮겼다(`shelfFrom`).
      순서는 그대로 지킨다 — 카드가 꽂히고(0.74) → CODE 값이 뜨고(~1.0) → 그제야 판이 그어진다.

   🔴 아웃트로 카드(마지막 3.0초)는 이제 nextpeek 샷을 덮는다. 이 샷은 덮이지 않으므로
      「페이오프를 예고 자막에 걸지 마라」는 제약이 여기서는 풀렸다. 대신 **순서 제약**
      (값이 먼저, 판이 나중)만 남는다.

   🔴 화면에 뜨는 수(자리 번호 · CODE 값)는 전부 spec 경유다. */

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const fit = (txt, weight, start, max, min = 8, f = disp) => {
      let fs = start; ctx.font = f(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = f(weight, fs); }
      return fs;
    };

    const snapAt = spec.snapAt ?? 0.74;
    const c0 = cue(spec.snapCue ?? 0, 0.15, 0.80);
    /* 🔴 선반 판은 **같은 자막(cue 0)의 뒷구간**에서 그어진다(2026-08-08 구조 개정).
       산수: 이 줄의 실측 rel = 2.961초(말 2.831 + pause 0.130)다.
         · 값(CODE)이 완성되는 시각 = −0.15 + 1.0 × (0.15 + 2.961 × 0.80) = **2.369초**
         · 판이 그어지기 시작하는 시각 = −0.15 + 0.88 × (0.15 + 2.961 × 0.92) = **2.379초**
         · 판이 완성되는 시각 = −0.15 + 1.0 × (0.15 + 2.961 × 0.92) = **2.724초**
       샷은 2.961 + SHOT_TAIL 0.35 = 3.311초까지 가므로 완성 뒤 0.59초가 남는다.
       🔑 `shelfFrom` 을 내리면 판이 값보다 먼저 그어져 순서가 뒤집힌다.
       **손익분기는 0.876** 이고(그 값에서 판 시작 = 값 완성 = 2.369초), 0.88 은 그 바로 위다.
       🔴 초고 주석에 「0.84 면 10ms 먼저」라고 적었던 것은 **틀렸다 — 실제로는 105ms 먼저**다
       (검수 재검산). 0.88 을 내리지 마라. */
    const shelfFrom = spec.shelfFrom ?? 0.88;
    const c1 = cue(spec.shelfCue ?? 0, 0.15, 0.92);

    const dk = ease(clamp(c0 / snapAt));                    // 두 번째 카드가 꽂힌다
    const vk = ease(clamp((c0 - snapAt) / (1 - snapAt)));   // CODE 값이 뜬다
    const sk = ease(clamp((c1 - shelfFrom) / (1 - shelfFrom)));  // 선반 판이 그어진다

    const CW = 148, CH = 84, CY = 74;
    const cx = i => (i === 0 ? 20 : w - 20 - CW);
    const SHELF_Y = CY + CH + 14;                           // 판이 그어지는 높이
    const cards = spec.cards ?? [];
    const slots = spec.slots ?? [];

    /* 판이 그어지면 카드 둘이 그 위로 내려앉는다 */
    const settle = sk * 6;

    /* ── 자리 번호 ────────────────────────────────── */
    for (let i = 0; i < slots.length; i++) {
      const a = i === 0 ? clamp(c0 * 4) : clamp(c0 * 2.2);
      if (a <= 0.02) continue;
      ctx.globalAlpha = a;
      ctx.textAlign = 'center';
      ctx.font = mono(700, 15); ctx.fillStyle = tone('sub');
      ctx.fillText(slots[i], cx(i) + CW / 2, CY - 14 + settle);
      ctx.globalAlpha = 1;
    }

    /* ── 카드 둘 ──────────────────────────────────── */
    for (let i = 0; i < cards.length; i++) {
      const seated = i === 0 ? ease(clamp(c0 * 3.2)) : dk;
      if (seated <= 0.02) continue;
      // 1번은 이미 꽂혀 있고, 2번은 위에서 내려와 꽂힌다
      const y = i === 0 ? CY + settle : lerp(6, CY + settle, seated);
      const x = cx(i);

      ctx.globalAlpha = clamp(seated * 1.8);
      ctx.save();
      ctx.setLineDash([11, 8]);
      ctx.lineDashOffset = -(t * 15 * (i === 0 ? 1 : -1)) % 19;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      setShadow(ctx, GLOW, 9, 0);
      roundRect(ctx, x, y, CW, CH, 12); ctx.stroke();
      clearShadow(ctx);
      ctx.restore();

      /* 카드 이름은 두 낱말이라 두 줄로 앉힌다 — 폭을 재서 안쪽에 가둔다 */
      const parts = String(cards[i]).split(' ');
      ctx.textAlign = 'center';
      const cxm = x + CW / 2;
      if (parts.length > 1) {
        const fs = Math.min(
          fit(parts[0], 900, 21, CW - 22, 11),
          fit(parts[1], 900, 21, CW - 22, 11));
        ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
        ctx.fillText(parts[0], cxm, y + CH / 2 - 2);
        ctx.fillText(parts[1], cxm, y + CH / 2 + fs + 1);
      } else {
        const fs = fit(parts[0], 900, 21, CW - 22, 11);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
        ctx.fillText(parts[0], cxm, y + CH / 2 + fs * 0.36);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 선반 판 — 예고 자막에서 가운데부터 좌우로 그어진다 ── */
    if (sk > 0.01) {
      const half = (w - 24) / 2 * sk;
      ctx.fillStyle = tone('ink');
      ctx.fillRect(w / 2 - half, SHELF_Y, half * 2, 6);

      /* 🔴 문턱 0.45 → **0.20** (검수 지적). 판 그리기가 짧아 0.45 문턱에서는 라벨이
         578ms 만 떠 있었다. 2026-08-08 구조 개정 뒤에도 그대로 둔다 — 판 그리기가
         0.345초(2.379→2.724)라 0.20 문턱이면 라벨이 2.448초에 뜨고 샷 끝(3.311)까지
         **0.86초** 떠 있다. 아웃트로 카드는 이 샷을 더 이상 덮지 않는다. */
      if (spec.shelfLabel && sk > 0.20) {
        ctx.globalAlpha = clamp((sk - 0.20) / 0.40);
        ctx.textAlign = 'right';
        ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.shelfLabel, w - 14, SHELF_Y + 20);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 계기 — 이 부탁이 든 코드 ─────────────────── */
    const MY = h - 54;
    if (spec.meter) {
      ctx.globalAlpha = clamp(c0 * 2.6);
      ctx.textAlign = 'center';
      ctx.font = disp(800, 13); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.meter, w / 2, MY - 34);
      ctx.globalAlpha = 1;
    }

    if (spec.codeValue !== undefined && vk > 0.02) {
      const val = String(spec.codeValue);
      ctx.globalAlpha = clamp(vk * 1.6);

      // 값 둘레의 점선 링 — 계속 돈다
      ctx.save();
      ctx.setLineDash([9, 8]);
      ctx.lineDashOffset = -(t * 17) % 17;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(w / 2, MY - 2, 29, 0, Math.PI * 2); ctx.stroke();
      ctx.restore();

      const sc = lerp(0.7, 1, ease(vk));
      const fs = fit(val, 900, 46 * sc, 46, 16);
      ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
      ctx.textAlign = 'center';
      setShadow(ctx, GLOW, 12, 0);
      ctx.fillText(val, w / 2, MY + fs * 0.36 - 2);
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
