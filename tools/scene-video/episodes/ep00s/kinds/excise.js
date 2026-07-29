import {
  disp, mono, ease, easeOut, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* excise — 결함 C 의 몸통이 잘려 나가는 샷.

   S3(dossier)에서 이름표와 함께 세워 둔 그 코드 한 줄이 여기서 죽는다.
   같은 문자열이 같은 자리에 다시 나와야 "통째로 지웠어요"가 성립한다 —
   처음 보는 줄을 지우는 그림은 아무것도 지우지 않은 것과 같다.

   지우는 방식은 파편을 날리지 않는다(그러면 화면 밖으로 나가 잘림 예외를 선언해야 하고,
   무엇보다 코드가 삭제되는 모양이 아니다). 줄이 **접혀 사라진다** —
   편집기에서 줄을 지웠을 때 아래가 위로 당겨 올라오는 그 모양이다.

   그 자리에 들어서는 건 둘이다. 원문 91행: "대신 실패는 실패라고 기록하고,
   '다시 계획을 세워라'라는 요청을 AI 에게 명시적으로 보내도록 바꿨습니다."
   그래서 오른쪽 칸에서 화살표가 위로 되돌아간다 — 실패가 위로 보고되는 게 아니라
   재계획 요청이 위로 올라간다는 뜻이고, 그게 S1 의 거짓 신호와 정확히 대비된다. */

function fitFont(ctx, text, maxW, weight, size) {
  let s = size;
  ctx.font = disp(weight, s);
  while (s > 8 && ctx.measureText(text).width > maxW) { s -= 1; ctx.font = disp(weight, s); }
  return s;
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const cx = w / 2;

    const HX = 76, HW = w - 152, HY = 60, HH = 52;      // 머리 : 갈 수 없음
    const BY = 158, BH = 56, BX = 20, BW = w - 40;      // 몸통 : 코드 한 줄
    const DOWN_X = cx - 52;

    const showK = ease(cue(spec.showCue ?? 0, 0.2, 0.5));
    const cutK = ease(cue(spec.cutCue ?? 1, 0.2, 0.6));
    const repK = ease(cue(spec.replaceCue ?? 2, 0.2, 0.55));

    ctx.textBaseline = 'alphabetic';

    /* ── 머리 ───────────────────────────────────── */
    if (showK > 0.02) {
      ctx.globalAlpha = clamp(showK * 1.5);
      ctx.fillStyle = '#000';
      roundRect(ctx, HX, HY, HW, HH, 5); ctx.fill();
      setShadow(ctx, GLOW, 16, 0);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
      roundRect(ctx, HX, HY, HW, HH, 5); ctx.stroke();
      clearShadow(ctx);
      ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
      fitFont(ctx, spec.head || '', HW - 24, 900, 20);
      ctx.fillStyle = tone('accent');
      ctx.fillText(spec.head || '', cx, HY + HH / 2);
      ctx.textBaseline = 'alphabetic';
      ctx.globalAlpha = 1;
    }

    /* ── 아래로 가는 선 : 이 결과가 무엇을 부르는가 ─
       점 하나가 계속 내려간다. 이 샷에서 자막 내내 도는 움직임. */
    if (showK > 0.3) {
      const y0 = HY + HH + 6, y1 = BY - 6;
      ctx.globalAlpha = clamp(showK * 1.4 - 0.4) * (1 - repK * 0.85);
      ctx.lineWidth = 3; ctx.lineCap = 'round';
      ctx.strokeStyle = 'rgba(255,255,255,0.55)';
      ctx.beginPath(); ctx.moveTo(DOWN_X, y0); ctx.lineTo(DOWN_X, y1); ctx.stroke();
      ctx.fillStyle = tone('ink');
      ctx.beginPath();
      ctx.arc(DOWN_X, lerp(y0, y1, frac(t / 1.35)), 5, 0, Math.PI * 2);
      ctx.fill();
      ctx.globalAlpha = 1;
    }

    /* ── 몸통이 접혀 사라진다 ────────────────────
       0~0.45 : 줄에 금이 그어진다 (왼→오)
       0.45~1 : 줄 높이가 0 으로 접힌다 */
    if (showK > 0.15 && cutK < 0.995) {
      const strike = clamp(cutK / 0.45);
      const fold = clamp((cutK - 0.45) / 0.55);
      const bh = BH * (1 - fold);
      const by = BY + (BH - bh) / 2;

      ctx.globalAlpha = clamp(showK * 1.6 - 0.2) * (1 - fold * 0.9);
      ctx.save();
      ctx.beginPath(); ctx.rect(BX, by, BW, bh); ctx.clip();

      ctx.fillStyle = '#000';
      roundRect(ctx, BX, BY, BW, BH, 5); ctx.fill();
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      roundRect(ctx, BX, BY, BW, BH, 5); ctx.stroke();

      ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
      let cs = 15;
      ctx.font = mono(700, cs);
      while (cs > 8 && ctx.measureText(spec.body || '').width > BW - 34) {
        cs -= 1; ctx.font = mono(700, cs);
      }
      ctx.fillStyle = tone('ink');
      ctx.fillText(spec.body || '', cx, BY + BH / 2 + 1);

      if (strike > 0.01) {
        ctx.lineWidth = 4; ctx.lineCap = 'butt';
        ctx.strokeStyle = tone('accent');
        ctx.beginPath();
        ctx.moveTo(BX + 12, BY + BH / 2);
        ctx.lineTo(BX + 12 + (BW - 24) * easeOut(strike), BY + BH / 2);
        ctx.stroke();
      }
      ctx.restore();

      // 왼쪽 딱지 — 이게 무엇인지. 접힐 때 같이 사라진다
      if (fold < 0.2) {
        ctx.font = disp(900, 10.5);
        const tw = ctx.measureText(spec.bodyTag || '').width + 14;
        ctx.fillStyle = tone('ink');
        roundRect(ctx, BX + 10, BY - 9, tw, 18, 3); ctx.fill();
        ctx.fillStyle = '#000';
        ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
        ctx.fillText(spec.bodyTag || '', BX + 10 + tw / 2, BY);
      }
      ctx.textBaseline = 'alphabetic';
      ctx.globalAlpha = 1;
    }

    /* ── 그 자리에 들어서는 것 둘 ────────────────── */
    const after = spec.after || [];
    if (repK > 0.02 && after.length) {
      const gap = 8, aw = (BW - gap * (after.length - 1)) / after.length, ah = 78;
      after.forEach((a, i) => {
        const k = clamp(repK * after.length - i * 0.5);
        if (k <= 0.02) return;
        const ax = BX + i * (aw + gap);
        const rise = (1 - easeOut(k)) * 18;
        ctx.globalAlpha = clamp(k * 1.5);
        ctx.fillStyle = '#000';
        roundRect(ctx, ax, BY + rise, aw, ah, 5); ctx.fill();
        ctx.lineWidth = 3;
        ctx.strokeStyle = a.returns ? tone('accent') : tone('ink');
        roundRect(ctx, ax, BY + rise, aw, ah, 5); ctx.stroke();

        ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
        fitFont(ctx, a.label, aw - 20, 900, 16);
        ctx.fillStyle = a.returns ? tone('accent') : tone('ink');
        ctx.fillText(a.label, ax + aw / 2, BY + rise + ah / 2);
        ctx.textBaseline = 'alphabetic';
        ctx.globalAlpha = 1;
      });

      /* 되돌아가는 화살표 — 재계획 요청이 위로 올라간다.
         S1 에서 거짓 값이 올라가던 그 방향에 이번엔 정직한 요청이 올라간다. */
      const back = after.findIndex(a => a.returns);
      if (back >= 0) {
        const ax = BX + back * (aw + gap) + aw / 2;
        const y1 = HY + HH + 6, y0 = BY - 4;
        ctx.globalAlpha = clamp(repK * 1.4 - 0.3);
        ctx.lineWidth = 3; ctx.lineCap = 'round';
        ctx.strokeStyle = tone('accent');
        ctx.beginPath(); ctx.moveTo(ax, y0); ctx.lineTo(ax, y1); ctx.stroke();
        ctx.beginPath();
        ctx.moveTo(ax - 6, y1 + 9); ctx.lineTo(ax, y1); ctx.lineTo(ax + 6, y1 + 9);
        ctx.stroke();
        ctx.fillStyle = tone('accent');
        ctx.beginPath();
        ctx.arc(ax, lerp(y0, y1, frac(t / 1.1)), 5, 0, Math.PI * 2);
        ctx.fill();
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left'; ctx.textBaseline = 'alphabetic';
  }
};
