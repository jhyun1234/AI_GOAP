import {
  disp, mono, ease, easeOut, clamp, spring,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* checkpoint — 앞으로 나올 모든 커밋이 지나야 하는 검문소.

   원문 95·97·113행이 세 개를 말한다. EditMode 테스트 둘("길찾기 결과 3분기 계약 검증",
   "Unreachable 일 때 좌표 스냅 금지"), 그리고 커밋 체크리스트의 검색 한 줄
   (`Brain.TileX = targetX` 가 다시 나오는지 0건 확인).

   1차 씬은 이 자리를 셸 로그 세 줄로 그렸고, 화면에 없던 프롬프트·플래그를 지어내
   그릇을 만들어야 했다. 여기서는 그럴 필요가 없다 — 원문이 말하는 건 로그가 아니라
   **절차**이기 때문이다. 커밋 하나가 아래로 내려가면서 문 셋을 차례로 지난다.
   토큰은 자막과 무관하게 계속 내려간다(이 절차는 앞으로 매번 돈다는 뜻이기도 하다).

   화면에 글자 그대로 남아야 하는 건 `Brain.TileX = targetX` 한 줄뿐이다.
   원문이 그걸 "죽은 버그의 유전자 서열"이라 부르며 지문 대조에 쓰므로,
   다른 그림으로 바꾸면 대조할 대상이 사라진다. S3·S5 에서 두 번 본 그 줄이다. */

function fitFont(ctx, text, maxW, weight, size) {
  let s = size;
  ctx.font = disp(weight, s);
  while (s > 8 && ctx.measureText(text).width > maxW) { s -= 1; ctx.font = disp(weight, s); }
  return s;
}

const RAIL_TOP = 38, RAIL_BOT = 224, RAIL_X = 30;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const gates = spec.gates || [];
    const GX = 52, GW = w - 8 - GX;
    const geom = [{ y: 44, h: 46 }, { y: 98, h: 46 }, { y: 152, h: 64 }];

    const testK = ease(cue(spec.testCue ?? 0, 0.2, 0.5));
    const codeK = ease(cue(spec.codeCue ?? 1, 0.2, 0.55));
    const resK = ease(cue(spec.resultCue ?? 2, 0.2, 0.55));

    ctx.textBaseline = 'alphabetic';

    /* ── 내려가는 커밋 ─────────────────────────── */
    const tokY = RAIL_TOP + ((t * 66) % (RAIL_BOT - RAIL_TOP));

    ctx.lineWidth = 3; ctx.lineCap = 'round';
    ctx.strokeStyle = tone('track');
    ctx.beginPath(); ctx.moveTo(RAIL_X, RAIL_TOP); ctx.lineTo(RAIL_X, RAIL_BOT); ctx.stroke();

    ctx.textAlign = 'left';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
    ctx.fillText(spec.token || '이동 커밋', 8, 22);

    /* ── 문 셋 ──────────────────────────────────── */
    gates.forEach((g, i) => {
      const isCode = !!g.code;
      const k = isCode ? codeK : clamp(testK * 2 - i * 0.7);
      if (k <= 0.02) return;
      const { y, h: gh } = geom[i] || geom[0];
      const s = spring(k);
      const passing = clamp(1 - Math.abs(tokY - (y + gh / 2)) / 26);

      ctx.save();
      ctx.translate(GX, y + gh / 2); ctx.scale(1, s); ctx.translate(-GX, -(y + gh / 2));
      ctx.globalAlpha = clamp(k * 1.5);
      ctx.fillStyle = '#000';
      roundRect(ctx, GX, y, GW, gh, 5); ctx.fill();
      ctx.lineWidth = 3;
      if (passing > 0.05) setShadow(ctx, GLOW, 8 + 14 * passing, 0);
      ctx.strokeStyle = passing > 0.5 ? tone('accent') : tone('ink');
      roundRect(ctx, GX, y, GW, gh, 5); ctx.stroke();
      clearShadow(ctx);

      // 레일에서 문으로 들어가는 짧은 가지
      ctx.lineWidth = 3;
      ctx.strokeStyle = passing > 0.5 ? tone('accent') : 'rgba(255,255,255,0.55)';
      ctx.beginPath();
      ctx.moveTo(RAIL_X, y + gh / 2); ctx.lineTo(GX, y + gh / 2); ctx.stroke();

      ctx.textAlign = 'left'; ctx.textBaseline = 'middle';
      ctx.font = mono(700, 10.5);
      ctx.fillStyle = tone('sub');
      ctx.fillText(g.tag || '', GX + 12, y + (isCode ? 18 : gh / 2));

      fitFont(ctx, g.label, GW - 60, 800, 13.5);
      ctx.fillStyle = tone('ink');
      ctx.fillText(g.label, GX + 40, y + (isCode ? 18 : gh / 2));

      if (isCode) {
        // 등록된 서열 한 줄 — 이 회차에서 이 문자열의 세 번째이자 마지막 등장
        let cs = 13;
        ctx.font = mono(700, cs);
        while (cs > 8 && ctx.measureText(g.code).width > GW - 28) { cs -= 1; ctx.font = mono(700, cs); }
        ctx.fillStyle = tone('accent');
        ctx.textAlign = 'center';
        ctx.fillText(g.code, GX + GW / 2, y + gh - 20);
      }
      ctx.textBaseline = 'alphabetic';
      ctx.globalAlpha = 1;
      ctx.restore();
    });

    // 커밋 토큰은 문 위에 그린다
    ctx.fillStyle = tone('ink');
    roundRect(ctx, RAIL_X - 10, tokY - 10, 20, 20, 4); ctx.fill();

    /* ── 대조 결과 ───────────────────────────────
       원문에 있는 값은 '0건' 하나다. 셸 프롬프트를 지어내지 않는다. */
    if (resK > 0.02) {
      const ry = 240, rh = 48;
      ctx.globalAlpha = clamp(resK * 1.5);
      ctx.fillStyle = '#000';
      roundRect(ctx, GX, ry, GW, rh, 5); ctx.fill();
      setShadow(ctx, GLOW, 8, 0);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
      roundRect(ctx, GX, ry, GW, rh, 5); ctx.stroke();
      clearShadow(ctx);

      ctx.textAlign = 'left'; ctx.textBaseline = 'middle';
      ctx.font = disp(700, 11);
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.resultLabel || '대조 결과', GX + 14, ry + rh / 2);

      ctx.textAlign = 'right';
      ctx.font = disp(900, 26);
      ctx.fillStyle = tone('accent');
      ctx.fillText(spec.result || '0건', GX + GW - 14, ry + rh / 2 + 1);
      ctx.textBaseline = 'alphabetic';
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left'; ctx.textBaseline = 'alphabetic';
  }
};
