import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* moved — 파일 한 장에 매달린 자리 표식이 못을 바꿔 걸린다.

   두 번째 재현이다. 위에 고친 파일 한 장(RestByCampfire.asset)이 있고, 그 카드에서
   줄 한 가닥이 내려와 끝에 고리가 달려 있다. 고리에는 이름표가 붙어 있는데
   「모닥불 옆에서 쉬기」다. moveCue 에서 그 고리가 호를 그리며 왼쪽 못(모닥불)에서
   오른쪽 못(집)으로 옮겨 걸리고, 아래 주민 셋의 점선이 따라서 방향을 돌린다.

   🔴 이름표는 안 바뀐다. 파일 이름은 여전히 RestByCampfire 인데 걸린 자리만 집으로
   옮겨 간 것 — 원문이 설명하지 않은 이 농담을 화면만 보여 주고 자막은 건드리지 않는다.

   🔴 값 칸이 굴러 바뀌는 그림(앞선 초고의 방식)을 안 썼다. 굴림은 '값이 바뀌었다'를
   말하지만 이 편이 말해야 하는 것은 **파일 한 장과 마을의 저녁이 한 가닥으로 이어져
   있다**는 것이다. 그래서 카드와 마을을 실제로 줄로 묶었고, 고쳐진 것은 그 줄이 걸린
   못 하나뿐이다.

   🔴 「앵커」라는 단어를 화면에도 자막에도 안 쓴다. 이 편만 본 사람에게 그 말은
   앞 편을 봐야 갚아지는 빚이라, 대신 '자리'로 풀고 그림이 자리를 보여 준다.

   계속 도는 것 = 모닥불 세 줄기, 매달린 줄과 고리의 좌우 흔들림(주기 2.6초),
   주민 셋에서 고리로 가는 점선의 흐름. */

const FLAME = 1.7;
const SWAY = 2.6;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));

    const fk = ease(cue(spec.fileCue ?? 0, 0.15, 0.6));
    const mk = ease(cue(spec.moveCue ?? 1, 0.15, 0.82));
    const slide = ease(clamp(mk / 0.9));          // 0.9 에서 고리가 집에 닿는다 (sfx 근거)

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const CX = 26, CY = 20, CW = w - 52, CH = 44;
    const GY = 236;
    const FIRE = 76, HOME = 274;
    const ANCH = { x: w / 2, y: CY + CH };

    /* ── 고친 파일 한 장 ──────────────────────────── */
    if (fk > 0.02) {
      const k = clamp(fk * 2);
      const s = Math.min(1, spring(clamp(fk * 1.3)));
      ctx.globalAlpha = k;
      ctx.lineWidth = 4; ctx.strokeStyle = tone('accent');
      if (fk > 0.7) setShadow(ctx, GLOW, 12, 0);
      roundRect(ctx, CX + (CW - CW * s) / 2, CY + (CH - CH * s) / 2, CW * s, CH * s, 4);
      ctx.stroke();
      clearShadow(ctx);

      ctx.textAlign = 'left';
      let fs = 13; ctx.font = mono(700, fs);
      const nm = spec.file || '';
      while (fs > 7 && ctx.measureText(nm).width > CW - 32) { fs -= 0.5; ctx.font = mono(700, fs); }
      ctx.fillStyle = tone('accent');
      ctx.fillText(nm, CX + 16, CY + CH / 2 + 5);
      ctx.globalAlpha = 1;
    }

    if (fk > 0.45 && spec.fileNote) {
      const k = clamp((fk - 0.45) / 0.45);
      ctx.globalAlpha = k;
      ctx.textAlign = 'left';
      const fs = fit(spec.fileNote, 700, 11, CW, 8);
      ctx.font = disp(700, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.fileNote, CX, CY + CH + 18);
      ctx.globalAlpha = 1;
    }

    if (fk < 0.25) { ctx.textAlign = 'left'; return; }

    const g = clamp((fk - 0.25) / 0.45);

    /* ── 바닥 ─────────────────────────────────────── */
    ctx.globalAlpha = g * 0.9;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(20, GY); ctx.lineTo(w - 20, GY); ctx.stroke();
    ctx.globalAlpha = 1;

    /* ── 모닥불 (계속 도는 것) ────────────────────── */
    ctx.globalAlpha = g * (1 - 0.5 * slide);
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(FIRE - 16, GY - 2); ctx.lineTo(FIRE + 16, GY - 12);
    ctx.moveTo(FIRE - 16, GY - 12); ctx.lineTo(FIRE + 16, GY - 2);
    ctx.stroke();
    for (let i = 0; i < 3; i++) {
      const u = frac(t / FLAME + i / 3);
      const bx = FIRE + (i - 1) * 9;
      const top = GY - 18 - 22 * Math.sin(Math.PI * u) - i * 2;
      ctx.globalAlpha = g * (1 - 0.5 * slide) * (0.45 + 0.5 * Math.sin(Math.PI * u));
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(bx, GY - 16);
      ctx.quadraticCurveTo(bx + 6, (GY - 16 + top) / 2, bx, top);
      ctx.stroke();
    }
    ctx.globalAlpha = 1;

    /* ── 집 ───────────────────────────────────────── */
    ctx.globalAlpha = g * (0.42 + 0.58 * slide);
    ctx.strokeStyle = slide > 0.5 ? tone('accent') : tone('sub');
    ctx.lineWidth = 4;
    if (slide > 0.85) setShadow(ctx, GLOW, 12, 0);
    roundRect(ctx, HOME - 27, GY - 38, 54, 38, 3); ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(HOME - 34, GY - 38); ctx.lineTo(HOME, GY - 58); ctx.lineTo(HOME + 34, GY - 38);
    ctx.stroke();
    clearShadow(ctx);
    ctx.globalAlpha = 1;

    ctx.textAlign = 'center';
    ctx.font = disp(800, 11);
    ctx.globalAlpha = g * 0.85;
    ctx.fillStyle = tone('sub');
    ctx.fillText(spec.from || '모닥불', FIRE, GY + 18);
    ctx.fillStyle = slide > 0.5 ? tone('accent') : tone('sub');
    ctx.fillText(spec.to || '집', HOME, GY + 18);
    ctx.globalAlpha = 1;

    /* ── 줄과 고리 — 못을 바꿔 건다 ───────────────── */
    const sway = 2.4 * Math.sin(frac(t / SWAY) * Math.PI * 2);
    const rx = lerp(FIRE, HOME, slide) + sway * (1 - 0.5 * slide);
    const ry = 170 - 22 * Math.sin(Math.PI * slide);

    ctx.globalAlpha = g;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(ANCH.x, ANCH.y); ctx.lineTo(rx, ry); ctx.stroke();

    ctx.strokeStyle = slide > 0.5 ? tone('accent') : tone('ink'); ctx.lineWidth = 4;
    setShadow(ctx, GLOW, slide > 0.5 ? 12 : 0, 0);
    ctx.beginPath(); ctx.arc(rx, ry, 9, 0, Math.PI * 2); ctx.stroke();
    clearShadow(ctx);
    ctx.globalAlpha = 1;

    if (spec.tagLabel) {
      ctx.globalAlpha = g;
      ctx.textAlign = 'center';
      const fs = fit(spec.tagLabel, 800, 11, 150, 8);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(spec.tagLabel, rx, ry - 16);
      ctx.globalAlpha = 1;
    }

    /* ── 주민 셋 — 점선이 고리를 따라 돈다 ────────── */
    [148, 176, 204].forEach(vx => {
      ctx.globalAlpha = g * 0.8;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.setLineDash([7, 6]);
      ctx.lineDashOffset = -(t * 10) % 13;
      ctx.beginPath(); ctx.moveTo(vx, GY - 26); ctx.lineTo(rx, ry); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;

      ctx.globalAlpha = g;
      ctx.fillStyle = tone('ink');
      roundRect(ctx, vx - 6, GY - 22, 12, 22, 3); ctx.fill();
      ctx.beginPath(); ctx.arc(vx, GY - 28, 5, 0, Math.PI * 2); ctx.fill();
      ctx.globalAlpha = 1;
    });

    /* ── 착지 (원문 6절 마지막 줄 그대로) ─────────── */
    if (mk > 0.74 && spec.landing) {
      const k = clamp((mk - 0.74) / 0.26);
      const s = Math.min(1, spring(k));
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.landing, 900, 14, w - 24, 9.5);
      ctx.font = disp(900, fs * s); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.landing, w / 2, 292);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
