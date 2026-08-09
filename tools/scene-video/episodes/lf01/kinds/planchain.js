import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect } from '../../../engine/lib.js';

/* planchain — 이 회차의 중심 도형. 목표 칩 하나에서 **오른쪽에서 왼쪽으로**(거꾸로)
   액션 카드가 자라난다. GOAP 가 목표에서 역방향으로 계획을 세우므로 방향이 반대다.
   카드 이름은 실제 에셋 파일명이다 (Assets/M0Config/Goals·Actions).

   spec.phase = 0 : 사슬이 선다 (챕터1 — "이게 GOAP")
   spec.phase = 1 : 같은 사슬을 가리개가 덮는다 (챕터3 — "통째로 들고 있으면서 하필
                    가장 안 보이는 상태였다"). 같은 그림을 **뒤집어 되받는** 자리라
                    일부러 같은 kind 를 쓴다 — 이 회차 안의 유일한 되받기다. */

const CARDS = [
  { id: 'HarvestCrop', why: 'crop' },
  { id: 'CookMeal', why: 'meal' },
  { id: 'EatCookedFood', why: 'satiety' },
];
const CW = 118, GAP = 16;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, nLines }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const hidden = spec.phase === 1;
    const M = 16;
    const cy = h * 0.46;

    // ── 목표 칩 (오른쪽 끝) ────────────────────────
    const goal = 'Goal_P0_Hunger';
    ctx.font = disp(800, 13);
    const gw = ctx.measureText(goal).width + 22;
    const gx = w - M - gw;
    ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
    roundRect(ctx, gx, cy - 19, gw, 38, 4); ctx.stroke();
    ctx.fillStyle = tone('accent');
    ctx.fillText(goal, gx + 11, cy + 5);

    // ── 액션 카드 — 목표에서 왼쪽으로 거꾸로 자란다 ──
    const first = gx - GAP - CARDS.length * (CW + GAP) + GAP;
    for (let i = CARDS.length - 1; i >= 0; i--) {
      const k = ease(cue(Math.min(i + 1, nLines - 1)));
      if (k <= 0.01) continue;
      const bx = first + i * (CW + GAP) + (1 - k) * 26;
      ctx.globalAlpha = k;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      roundRect(ctx, bx, cy - 22, CW, 44, 4); ctx.stroke();
      ctx.font = disp(800, 12); ctx.fillStyle = tone('ink');
      ctx.fillText(CARDS[i].id, bx + 10, cy - 2);
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText(CARDS[i].why, bx + 10, cy + 14);
      ctx.strokeStyle = tone('track');
      ctx.beginPath(); ctx.moveTo(bx + CW, cy); ctx.lineTo(bx + CW + GAP, cy); ctx.stroke();
      ctx.globalAlpha = 1;
    }

    // ── 계획이 흐르는 표식 — 사슬 위를 계속 훑는다 ──
    // 🔴 자막과 무관하게 언제나 돈다. 목표가 계획을 당기는 것은 첫 줄부터 이미 참이고,
    //    이걸 cue 로 막으면 첫 자막 구간(약 2.7초)에 캔버스가 통째로 멈춘다.
    {
      const kk = (t % 2.2) / 2.2;
      const px = lerp(first + 12, gx + gw - 12, ease(kk));
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(px - 28, cy); ctx.lineTo(px - 9, cy); ctx.stroke();
      ctx.fillStyle = tone('accent');
      ctx.beginPath(); ctx.arc(px, cy, 7, 0, Math.PI * 2); ctx.fill();
    }

    // ── 라벨 ─────────────────────────────────────
    ctx.font = mono(700, 11); ctx.fillStyle = tone('sub');
    ctx.fillText(hidden ? 'PLAN CHAIN - HELD, NOT SHOWN' : 'PLAN CHAIN - built backwards', M + 2, M + 10);

    // ── phase 1 : 가리개 ──────────────────────────
    if (hidden) {
      const veil = ease(cue(Math.min(3, nLines - 1)));
      if (veil > 0.01) {
        const L = M, R = w - M, B = h - 14;
        const top = lerp(B, cy - 34, veil);
        ctx.save();
        ctx.beginPath(); ctx.rect(L, top, R - L, B - top); ctx.clip();
        // 불투명 검정으로 덮는다 — 반투명 흰 막을 쓰면 강조색과 같은 픽셀에서 섞인다
        ctx.fillStyle = '#000000';
        ctx.fillRect(L, top, R - L, B - top);
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        for (let sx = L - (B - top); sx < R; sx += 22) {
          ctx.beginPath(); ctx.moveTo(sx, B); ctx.lineTo(sx + (B - top), top); ctx.stroke();
        }
        ctx.restore();
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.moveTo(L, top); ctx.lineTo(R, top); ctx.stroke();
        const s = '가장 안 보이는 상태';
        ctx.font = disp(900, 20);
        const sw = ctx.measureText(s).width;
        ctx.globalAlpha = clamp((veil - 0.4) / 0.5);
        ctx.fillStyle = tone('ink');
        ctx.fillText(s, (w - sw) / 2, Math.min(B - 8, top + 36));
        ctx.globalAlpha = 1;
      }
    }
  },
};
