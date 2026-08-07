import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* askover — 부탁이 건너와 목표 칸에 앉고, 그 아래에 내가 손으로 절차를 매단다.

   이 회차의 기본 도형은 **목표 칸 하나와 그 아래로 뻗는 사슬**이고, 이 첫 그림이
   그 문법을 세운다. 두 가지만 기억하면 다섯 샷이 전부 읽힌다:

     · **둥근 강조색** = GOAP 이 하는 것 (부탁이 얹힌 칸, 나중의 ACCEPT·사슬·카드)
     · **각진 흰색**   = 내가 손으로 붙인 것 (PRECHECK · RESERVE)

   그리고 **방향**이 뜻을 진다. 여기서 상자는 **위에서 아래로** 내려와 매달린다 —
   내가 얹는다는 뜻이다. 마지막 증명 샷(selfgrow)에서는 정확히 반대로 아래에서
   위로 돋는다. 값도 라벨도 없이 「누가 짰는가」가 방향만으로 갈린다.

   🔴 왼쪽 표식은 t 로 레인을 왕복하고, 말풍선은 **그 표식의 현재 자리에서** 출발한다.
      그래서 경로가 매번 다르다. 다만 말풍선의 알파·진행은 cue 에만 달려 있어
      왕복 위상과 무관하다 — ep09s-2 가 1차 반려된 자리(사건이 t 의 위상에 물려
      있어 자막이 도는 내내 안 보였다)를 구조적으로 피한 것이다.

   🔴 화면에 뜨는 문자열은 전부 spec 경유다. 코드 안에 라벨 하드코딩이 없다. */

const WALK = 4.6;      // 왼쪽 표식이 레인을 한 번 왕복하는 시간(초)
const BOB = 2.9;       // 목수가 제자리에서 흔들리는 주기(초)

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    /* 글자는 상자 안쪽에 가둔다 — 문자열을 자르지 않고 크기로 맞춘다 */
    const fit = (txt, weight, start, max, min = 8) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const ak = ease(cue(spec.askCue ?? 0, 0.15, 0.72));   // 부탁이 건너가 칸에 앉는다
    const pk = ease(cue(spec.planCue ?? 1, 0.15, 0.80));  // 내가 상자를 매단다

    /* ── 자리 ─────────────────────────────────────── */
    const GX = 22, GW = w - 44, GY = 44, GH = 38;          // 목표 칸
    const CX = w / 2;
    const BW = 158, BX = (w - BW) / 2, BH = 32;            // 내가 매다는 상자
    const B1 = 106, B2 = 152;
    const LANE = h - 62;                                   // 두 표식이 선 바닥
    const MX = w - 50;                                     // 목수 자리

    /* ── 상단 라벨 ────────────────────────────────── */
    if (spec.label) {
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.label, 10, GY - 12);
    }

    /* ── 바닥 레일 — 계속 흐른다 ──────────────────── */
    ctx.save();
    ctx.setLineDash([9, 8]);
    ctx.lineDashOffset = -(t * 15) % 17;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(20, LANE + 22); ctx.lineTo(w - 20, LANE + 22); ctx.stroke();
    ctx.restore();

    /* ── 목수 → 목표 칸을 잇는 세로 점선 ──────────── */
    ctx.save();
    ctx.setLineDash([7, 7]);
    ctx.lineDashOffset = (t * 12) % 14;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(MX, LANE - 16); ctx.lineTo(MX, GY + GH); ctx.stroke();
    ctx.restore();

    /* ── 목표 칸 — 둥글다. GOAP 이 읽는 자리다 ────── */
    const filled = clamp((ak - 0.72) / 0.28);
    ctx.save();
    ctx.setLineDash([10, 8]);
    ctx.lineDashOffset = -(t * 14) % 18;
    ctx.strokeStyle = filled > 0.4 ? tone('accent') : tone('sub');
    ctx.lineWidth = filled > 0.4 ? 4 : 3;
    roundRect(ctx, GX, GY, GW, GH, 12); ctx.stroke();
    ctx.restore();

    if (filled > 0.02 && spec.goalCard) {
      ctx.globalAlpha = filled;
      ctx.textAlign = 'center';
      const fs = fit(spec.goalCard, 900, 20, GW - 30, 12);
      ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
      setShadow(ctx, GLOW, 9, 0);
      ctx.fillText(spec.goalCard, CX, GY + GH / 2 + fs * 0.36);
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    /* ── 내가 매다는 각진 상자들 — 위에서 내려온다 ── */
    const steps = spec.steps ?? [];
    for (let i = 0; i < steps.length; i++) {
      const k = ease(clamp(pk * 1.9 - i * 0.48));
      if (k <= 0.02) continue;
      const y = lerp(B1 + i * (B2 - B1) - 30, B1 + i * (B2 - B1), k);

      // 매단 줄 — 위 칸(또는 위 상자)에서 이 상자까지
      ctx.globalAlpha = k;
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(CX, i === 0 ? GY + GH : B1 + BH);
      ctx.lineTo(CX, y);
      ctx.stroke();

      // 각진 상자 — 모서리를 안 굴린다. 내가 붙였다는 표시다.
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.rect(BX, y, BW, BH); ctx.stroke();

      ctx.textAlign = 'center';
      const fs = fit(steps[i], 800, 15, BW - 20, 9);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(steps[i], CX, y + BH / 2 + fs * 0.36);
      ctx.globalAlpha = 1;
    }

    /* ── 두 표식 ──────────────────────────────────── */
    const vx = 34 + 74 * (0.5 - 0.5 * Math.cos(t * Math.PI * 2 / WALK));
    const my = LANE + Math.sin(t * Math.PI * 2 / BOB) * 3;

    // 부탁하는 쪽 — 걸어다닌다
    ctx.fillStyle = tone('ink');
    ctx.beginPath(); ctx.arc(vx, LANE, 8, 0, Math.PI * 2); ctx.fill();

    // 목수 — 제자리에 서 있고 목표 칸이 그의 것이다
    ctx.fillStyle = tone('ink');
    ctx.beginPath(); ctx.arc(MX, my, 9, 0, Math.PI * 2); ctx.fill();
    if (spec.who) {
      ctx.textAlign = 'center';
      ctx.font = disp(800, 13); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.who, MX, my + 30);
    }

    /* ── 부탁 — 표식에서 칸으로 건너간다 ──────────── */
    if (ak > 0.02 && ak < 0.995) {
      const k = ease(ak);
      const bx = lerp(vx, CX, k);
      const by = lerp(LANE - 26, GY + GH / 2, k);
      const a = Math.min(1, (1 - k) * 3.4);
      const bw = 34, bh = 21;

      ctx.globalAlpha = a;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      setShadow(ctx, GLOW, 8, 0);
      roundRect(ctx, bx - bw / 2, by - bh / 2, bw, bh, 8); ctx.stroke();
      clearShadow(ctx);
      ctx.fillStyle = tone('accent');
      for (let d = 0; d < 3; d++) {
        ctx.beginPath();
        ctx.arc(bx - 8 + d * 8, by, 2.4, 0, Math.PI * 2); ctx.fill();
      }
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
