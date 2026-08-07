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

   🔴 마지막 샷이라 아웃트로 카드가 끝의 2.6초를 덮는다. 그래서
      ⓐ**페이오프(카드 착지 + CODE 값)를 전부 cue 0 안에서 끝내고**
      ⓑ예고 자막(cue 1)에는 lead 0.10 · span 0.10 짜리 **짧고 큰 변화**(선반 판)만 건다.
      페이오프를 예고 자막에 걸면 카드가 덮어 아무도 못 본다.

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
    /* 🔴 예고 자막의 span 은 0.10 → **0.06** 이다(2026-08-07 실측 반영).
       완성 시각 = 자막 시작 + rel × span 이고, 아웃트로 카드는 샷 끝에서 2.6초를 잰다.
       여유 = 0.94 × rel(예고줄) − 2.25 이므로 rel 2.832초에서 span 0.10 이면 **0.30초**,
       0.06 이면 **0.41초**다. 이 값을 늘리는 손잡이는 span 하나뿐이다 — lead 는 시작만
       당길 뿐 완성 시각을 못 바꾼다. */
    const c1 = cue(spec.shelfCue ?? 1, 0.14, 0.06);

    const dk = ease(clamp(c0 / snapAt));                    // 두 번째 카드가 꽂힌다
    const vk = ease(clamp((c0 - snapAt) / (1 - snapAt)));   // CODE 값이 뜬다
    const sk = ease(clamp(c1));                             // 선반 판이 그어진다

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

      /* 🔴 문턱 0.45 → **0.20** (검수 지적). 판 그리기가 0.31초라 0.45 문턱에서는 라벨이
         578ms 만 떠 있었다 — 카드가 덮기 전에 읽히지 않는다. shelfCue 의 lead·span 은
         아웃트로 여유(412ms)가 거기서 나오므로 절대 안 건드리고, 라벨 문턱만 내렸다. */
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
