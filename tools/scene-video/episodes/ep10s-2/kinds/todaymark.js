import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* todaymark — 훅 샷(4단 고정 구조 ②). 「오늘 지울 것」에 표식이 찍힌다.

   훅 자막은 「오늘 개발 일지 내용은, 목수한테 시킨 검사 지우기예요.」 한 줄이다.
   그림이 지는 것은 **무엇을 지우는가**이고, 자막이 지는 것은 **그게 오늘의 이야기다**이다.

   🔴 이 회차의 도형 문법을 그대로 쓴다(askover 가 세운 것):
     · **각진 흰색**   = 내가 손으로 시킨 것 (여기서는 CHECK 한 상자)
     · **둥근 강조색** = GOAP 이 하는 것 (상자 아래 눌려 있는 캡슐)

   🔴 **아무것도 없애지 않는다.** 없애는 것은 본문 S3(stripout)의 몫이고 여기서 미리
      치우면 그 샷의 뒤집힘이 죽는다. 여기서는 상자에 **마킹만** 씌우고, 그 밑에
      원래 있던 것이 그만큼 **밝아질** 뿐이다. 훅은 약속이지 이행이 아니다.

   🔑 목수와 상자를 ㄱ자 점선으로 잇는다 — 「목수한테 **시킨**」이 방향으로 읽히게.

   🔴 화면 문자열은 전부 spec 경유다(TODAY · CHECK · GOAP · 목수). */

const BOB = 3.1;        // 목수가 제자리에서 흔들리는 주기(초)

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    /* 글자는 도형 안쪽에 가둔다 — 자르지 않고 크기로 맞춘다 */
    const fit = (txt, weight, start, max, min = 8) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    /* 자막이 한 줄이라 그 줄을 앞뒤로 가른다 — 앞이 「시킨 것」, 뒤가 「지우기」 */
    const split = spec.split ?? 0.52;
    const c0 = cue(spec.markCue ?? 0, 0.15, 0.86);
    const dk = ease(clamp(c0 / split));                     // 상자가 내려와 앉는다
    const mk = ease(clamp((c0 - split) / (1 - split)));     // 마킹이 씌워지고 밑이 밝아진다

    const CX = w / 2;
    const BW = Math.min(206, w - 68), BH = 46;
    const BX = CX - BW / 2;
    const BY = Math.round(h * 0.24);
    const CAPW = BW - 36, CAPH = 38;
    const CAPY = BY + BH + 34 - 7 * mk;                     // 밑에 눌려 있던 것이 살짝 올라온다
    const LANE = h - 52;
    const MX = w - 46;                                      // 목수 자리

    /* ── 상단 라벨 ────────────────────────────────── */
    if (spec.label) {
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.label, 12, BY - 16);
    }

    /* ── 바닥 레일 — 계속 흐른다 ──────────────────── */
    ctx.save();
    ctx.setLineDash([9, 8]);
    ctx.lineDashOffset = -(t * 15) % 17;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(20, LANE + 20); ctx.lineTo(w - 20, LANE + 20); ctx.stroke();
    ctx.restore();

    /* ── 목수 → 상자 ㄱ자 점선 (「시킨」의 방향) ──── */
    const my = LANE + Math.sin(t * Math.PI * 2 / BOB) * 3;
    ctx.save();
    ctx.setLineDash([7, 7]);
    ctx.lineDashOffset = (t * 12) % 14;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(MX, my - 16);
    ctx.lineTo(MX, BY + BH / 2);
    ctx.lineTo(BX + BW, BY + BH / 2);
    ctx.stroke();
    ctx.restore();

    /* ── 밑에 눌려 있는 것 — 둥근 강조색. 지우기 전엔 흐리다 ── */
    if (spec.under) {
      const bright = lerp(0.32, 1, mk);
      ctx.save();
      ctx.globalAlpha = bright;
      ctx.setLineDash([10, 8]);
      ctx.lineDashOffset = -(t * 13) % 18;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = mk > 0.5 ? 4 : 3;
      if (mk > 0.5) setShadow(ctx, GLOW, 10, 0);
      roundRect(ctx, CX - CAPW / 2, CAPY, CAPW, CAPH, CAPH / 2); ctx.stroke();
      clearShadow(ctx);
      ctx.restore();

      ctx.globalAlpha = bright;
      ctx.textAlign = 'center';
      const fs = fit(spec.under, 900, 18, CAPW - 26, 11);
      ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.under, CX, CAPY + CAPH / 2 + fs * 0.36);
      ctx.globalAlpha = 1;
    }

    /* ── 내가 시킨 것 — 각진 흰 상자. 위에서 내려와 앉는다 ── */
    if (spec.mark && dk > 0.02) {
      const y = lerp(BY - 34, BY, dk);
      ctx.globalAlpha = clamp(dk * 1.6);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.rect(BX, y, BW, BH); ctx.stroke();

      ctx.textAlign = 'center';
      const fs = fit(spec.mark, 900, 21, BW - 26, 11);
      ctx.font = disp(900, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(spec.mark, CX, y + BH / 2 + fs * 0.36);
      ctx.globalAlpha = 1;

      /* 마킹 — 상자보다 한 겹 큰 흰 점선 테두리가 씌워진다.
         🔑 상자를 치우지 않는다. 「지울 것으로 찍혔다」까지가 훅이다. */
      if (mk > 0.02) {
        const pad = lerp(16, 8, mk);
        ctx.save();
        ctx.globalAlpha = clamp(mk * 1.8);
        ctx.setLineDash([8, 7]);
        ctx.lineDashOffset = -(t * 18) % 15;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.rect(BX - pad, y - pad, BW + pad * 2, BH + pad * 2);
        ctx.stroke();
        ctx.restore();
        ctx.globalAlpha = 1;
      }
    }

    /* ── 목수 ─────────────────────────────────────── */
    ctx.fillStyle = tone('ink');
    ctx.beginPath(); ctx.arc(MX, my, 9, 0, Math.PI * 2); ctx.fill();
    if (spec.who) {
      ctx.textAlign = 'center';
      ctx.font = disp(800, 13); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.who, MX, my + 30);
    }

    ctx.textAlign = 'left';
  }
};
