import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* infoline — 하나를 클로즈업한다. 위가 채워지고 나서야 아래가 존재한다.

   원문: "클릭하면 이름·성격·직업이 뜨는 정보줄을 만들었습니다." / "「농사꾼 고집쟁이」라는
   글자가 뜨는 순간, 점 하나가 비로소 사람이 됐습니다." / "아무리 속이 정교해도, 보는 사람에게
   전달되지 않으면 없는 것과 같습니다."

   🔴 표면선을 네 번째로 쓴다. 앞의 셋이 **넷의 무리**를 다뤘다면 여기서는 **하나**로 좁힌다
   (원문이 '점 하나'라고 쓴 그 하나다). 위에는 정보줄, 아래에는 그 주민의 코드 칸 둘,
   그 사이를 선이 가른다.

   🔴 두 단계로 나뉜다.
     ① fillCue — 정보줄이 NAME → TRAIT → JOB 순서로 차고, 동그라미의 선이 굵어진다.
        원문 순서('이름·성격·직업') 그대로다. **점이 사람이 되는 프레임이 여기다.**
     ② linkCue — 코드 칸 둘에서 줄이 올라와 선을 가로지르고, 그때까지 흐릿하던 코드 칸이
        비로소 또렷해진다. **속은 내내 거기 있었는데, 위가 채워지고 나서야 존재한다** —
        이 편의 남는 한 줄을 글자가 아니라 알파로 말한다(자막이 그 문장을 이미 말하므로
        화면에 같은 문장을 또 적지 않는다).

   🔴 **글자가 든 칸은 TRAIT 「고집쟁이」 하나뿐이다.**
   NAME 은 원문에 주민 개인 이름이 한 번도 안 나와서, JOB 은 원문이 직업 이름을 한 번도
   안 불러서 막대로만 뒀다(S3 이름표의 막대와 같은 어휘이고, NAME 은 그 막대와 같은 폭이라
   같은 주민임이 이어진다).
   🔴 **1차본은 여기에 JOB 「농사꾼」을 적었다가 반려됐다.** 낱말은 원문에 있지만 **분류가
   틀렸다** — `Assets/M0Config/Personalities/Personality_Farmer.asset` 의 DisplayName 이
   「농사꾼」이다. 즉 농사꾼·떠돌이·고집쟁이는 **셋 다 성격**이고, 직업은 목수·요리사·
   탐험가·농부·나무꾼·치료사·광부다. 원문은 셋을 나란히 쓰기만 하고 어느 쪽인지 말하지 않는데
   화면이 그 빈칸을 채운 것이었다.
   🔑 이 회차가 다루는 밀스톤의 명세가 정확히 그 혼동을 막으려고 세워져 있다 —
   `Docs/M7_상호대화_실행명세서.md` ADR-M7-5 「성격 이름(농사꾼·떠돌이)이 직업처럼 들리는
   혼동을 표기 위치로 해소」. 그 DoD 의 예시가 **「성격 농사꾼 · 직업 요리사」** 다. */

const FY0 = 22, FY1 = 190;
const R = 14;
const TAG_W = 46, TAG_H = 18, TAG_BAR = 26;

const ST_X = 26, ST_Y = 112, ST_W = 300, ST_H = 62;
const CELL_Y = 222, CELL_H = 32;

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

    const X0 = 14, X1 = w - 14;
    const fields = spec.fields ?? [];
    const cells = spec.cells ?? [];

    const COLW = (ST_W - 20 - 8 * 2) / 3;
    const colX = i => ST_X + 10 + i * (COLW + 8);
    const cellW = (X1 - 84 - 10) / 2;
    const cellX = i => 84 + i * (cellW + 10);

    const fk = ease(cue(spec.fillCue ?? 0, 0.14, 0.72));
    const lk = ease(cue(spec.linkCue ?? 1, 0.12, 0.45));
    const nk = ease(cue(spec.nextCue ?? 2, 0.05, 0.35));
    const fill = i => ease(clamp((fk - i * 0.16) / 0.52));   // 마지막 칸이 fk = 0.84 에 찬다
    const alive = clamp(fk * 3.4);                            // 그 주민이 또렷해진다

    const dx = w / 2 + 10 * Math.sin(2 * Math.PI * t / 6.4);

    /* ── 화면 프레임(위 세 변) ─────────────────────── */
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(X0, FY1); ctx.lineTo(X0, FY0 + 8);
    ctx.quadraticCurveTo(X0, FY0, X0 + 8, FY0);
    ctx.lineTo(X1 - 8, FY0);
    ctx.quadraticCurveTo(X1, FY0, X1, FY0 + 8);
    ctx.lineTo(X1, FY1);
    ctx.stroke();

    ctx.textAlign = 'left';
    ctx.font = disp(800, 10.5); ctx.fillStyle = tone('sub');
    ctx.fillText(spec.screenLabel ?? '', X0 + 2, FY0 - 6);
    ctx.fillText(spec.codeLabel ?? '', X0 + 2, 214);

    /* ── 뚫린 선 — S3 에서 열어 둔 그대로, 점선이 계속 흐른다(t) ── */
    ctx.globalAlpha = 0.55;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.setLineDash([9, 11]); ctx.lineDashOffset = -(t * 16) % 20;
    ctx.beginPath(); ctx.moveTo(X0, FY1); ctx.lineTo(X1, FY1); ctx.stroke();
    ctx.setLineDash([]); ctx.lineDashOffset = 0;
    ctx.globalAlpha = 1;

    /* ── 코드 칸 둘 — 내내 흐릿하다가 ②에서 또렷해진다 ──
       면적이 큰 변화가 이것이다(칸 둘 + 값 둘 + 밑줄). */
    const ca = lerp(0.25, 1, lk);
    for (let i = 0; i < cells.length; i++) {
      const cx = cellX(i);
      ctx.globalAlpha = ca;
      ctx.textAlign = 'left';
      ctx.font = disp(800, 10.5); ctx.fillStyle = tone('sub');
      ctx.fillText(cells[i].label ?? '', cx + 2, 214);

      if (cells[i].value) {
        ctx.textAlign = 'center';
        const fs = fit(cells[i].value, 900, 16, cellW - 14, 9);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('ink');
        ctx.fillText(cells[i].value, cx + cellW / 2, CELL_Y + CELL_H - 9);
      } else {
        /* 🔴 원문이 직업 이름을 한 번도 안 부른다 — 그래서 이 칸은 끝까지 가려 둔다.
           S2·S3 의 가린 칸과 같은 빗금이고, 빗금은 t 로 계속 흐른다. */
        ctx.save();
        ctx.beginPath(); ctx.rect(cx, CELL_Y + 3, cellW, CELL_H - 6); ctx.clip();
        const shift = (t * 13) % 14;
        ctx.globalAlpha = ca * 0.38;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        const nS = Math.ceil(cellW / 14) + 3;
        for (let k = -2; k < nS; k++) {
          const px = cx + k * 14 - shift;
          ctx.beginPath();
          ctx.moveTo(px + 11, CELL_Y + 3); ctx.lineTo(px, CELL_Y + CELL_H - 3);
          ctx.stroke();
        }
        ctx.restore();
        ctx.globalAlpha = ca;
      }

      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, cx, CELL_Y, cellW, CELL_H, 6); ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ②에서 코드 칸 아래로 밑줄이 그어진다 — 이제 이 아래 전부가 읽힌다 */
    if (lk > 0.02) {
      ctx.globalAlpha = lk;
      ctx.fillStyle = tone('accent');
      ctx.fillRect(X0, 268, (X1 - X0) * lk, 4);
      ctx.globalAlpha = 1;
    }

    /* ── 코드 칸에서 정보줄로 올라오는 줄(②) ───────── */
    for (let i = 0; i < cells.length && lk > 0.02; i++) {
      const sx = cellX(i) + cellW / 2;
      const ex = colX(i + 1) + COLW / 2;
      const y1 = lerp(CELL_Y, ST_Y + ST_H, ease(clamp(lk / 0.85)));
      ctx.globalAlpha = clamp(lk * 2.2) * 0.8;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(sx, CELL_Y);
      ctx.lineTo(lerp(sx, ex, ease(clamp(lk / 0.85))), y1);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 정보줄 — 원문 순서대로 이름·성격·직업이 찬다 ── */
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    roundRect(ctx, ST_X, ST_Y, ST_W, ST_H, 8); ctx.stroke();

    for (let i = 0; i < fields.length; i++) {
      const x = colX(i), k = fill(i);
      ctx.globalAlpha = 0.9;
      ctx.textAlign = 'left';
      ctx.font = disp(800, 9.5); ctx.fillStyle = tone('sub');
      ctx.fillText(fields[i].label ?? '', x, ST_Y + 18);
      ctx.globalAlpha = 1;

      if (k > 0.02) {
        ctx.globalAlpha = clamp(k * 2.4);
        if (fields[i].value) {
          ctx.textAlign = 'left';
          const fs = fit(fields[i].value, 900, 15, COLW, 9);
          ctx.font = disp(900, fs);
          setShadow(ctx, GLOW, 9, 0);
          ctx.fillStyle = tone('accent');
          ctx.fillText(fields[i].value, x, ST_Y + 44);
          clearShadow(ctx);
        } else {
          /* 🔴 글자가 떴다는 것만 알고 그 글자는 모르는 칸이다 — NAME(원문에 주민 개인
             이름이 없다)과 JOB(원문이 직업 이름을 한 번도 안 부른다)이 여기 해당한다.
             🔑 **선 위에서는 막대, 선 아래에서는 빗금**으로 갈랐다. 아래(코드 칸)의 빗금은
             '우리에게 안 보인다'는 뜻이고, 위(정보줄)의 막대는 '화면에는 떴는데 원문이 그
             글자를 안 알려준다'는 뜻이다. S3 이름표 안의 막대와 같은 어휘다. */
          ctx.fillStyle = tone('accent');
          roundRect(ctx, x, ST_Y + 34, (fields[i].bar ?? TAG_BAR) * k, 6, 3); ctx.fill();
        }
        ctx.fillStyle = tone('accent');
        ctx.fillRect(x, ST_Y + 52, COLW * k, 4);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 예고(③) — 말풍선 둘이 겹쳐 서로를 밀어낸다 ──
       다음 편이 여는 자리라 마지막 자막에 물려 있다. */
    if (nk > 0.02) {
      const push = 12 * nk;
      const bw = 62, bh = 22, by = 100;
      const bx = [dx - 52 - push, dx + 24 + push];
      for (let i = 0; i < 2; i++) {
        ctx.globalAlpha = clamp(nk * 2.4) * (i === 0 ? 1 : 0.75);
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        roundRect(ctx, bx[i], by - bh / 2, bw, bh, 8); ctx.stroke();
        ctx.beginPath();
        ctx.moveTo(bx[i] + 16, by + bh / 2);
        ctx.lineTo(bx[i] + 22, by + bh / 2 + 7);
        ctx.lineTo(bx[i] + 28, by + bh / 2);
        ctx.stroke();
        ctx.globalAlpha = 1;
      }
    }

    /* ── 그 주민 — 이름표를 달고 천천히 움직인다 ────── */
    ctx.globalAlpha = 0.55 + 0.45 * alive;
    setShadow(ctx, GLOW, 9, 0);
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
    roundRect(ctx, dx - TAG_W / 2, 40 - TAG_H / 2, TAG_W, TAG_H, 5); ctx.stroke();
    clearShadow(ctx);
    ctx.fillStyle = tone('ink');
    roundRect(ctx, dx - TAG_BAR / 2, 37.5, TAG_BAR, 5, 2.5); ctx.fill();
    ctx.globalAlpha = 1;

    ctx.globalAlpha = 0.55 * (1 - alive) + alive;
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(dx, 49); ctx.lineTo(dx, 72 - R); ctx.stroke();

    ctx.strokeStyle = tone('ink'); ctx.lineWidth = lerp(3, 5, alive);
    ctx.beginPath(); ctx.arc(dx, 72, R, 0, Math.PI * 2); ctx.stroke();
    ctx.globalAlpha = 1;

    ctx.textAlign = 'left';
  }
};
