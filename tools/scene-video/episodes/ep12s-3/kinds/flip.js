import {
  disp, mono, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* flip — 서로 반대를 보던 두 화살표 중 하나가 겨냥을 바꾼다.

   원문: "파종 목표: '식량이 넉넉하니까 밭을 비워두자.' / 재해 시스템: '밭에 자라는 작물을
   태워서 식량을 깎아야지… 어라, 밭이 다 비었네?' 두 시스템이 정확히 반대 방향을 보고
   있었습니다." / "홍수를 '작물을 태우는 재해'에서 '밭 시설 자체를 부수는 재해'로
   전환했습니다." / "이 변경도 에셋 필드 하나를 바꾸는 일이었다는 점입니다."

   기본 도형(밭 칸 한 줄)을 S1 과 같은 폭·같은 칸 수로 다시 세운다. 다른 것은 **칸에
   무슨 일이 일어나는가** 하나다.

   ① 위 줄: SOW 가 오른쪽으로, 아래 줄: FLOOD 가 왼쪽으로. 두 점선 화살표가 정확히
      마주 보는 반대 방향이고, 둘 다 상대에 닿지 않는다 — 서로를 못 만나는 것이 이 사건이다.
   ② 왼쪽 TARGET 슬롯의 값이 갈린다. `CROP` 에 흰 줄이 그어져 지워지고, **완전히 사라진 뒤에야**
      같은 자리에 `FIELD` 가 강조색으로 들어온다. 🔴 두 글자를 겹쳐 띄우지 않는다 —
      흰 글자 위에 강조색 글자를 얹으면 그 픽셀에서 두 색이 섞이고, 팔레트 게이트는
      그 혼합을 정상으로 읽어 아무 말도 해 주지 않는다. 알파 구간을 아예 붙여 놓지 않았다.
   ③ 겨냥이 바뀌자 칸 위에 강조색 타격 자국이 서고, 가운데 두 칸이 **주저앉으며** 흰 테두리가
      꺼지고 그 자리에 강조색 점선 윤곽만 남는다. 작물이 아니라 시설이 없어졌다.

   🔴 타격 자국은 칸 위 검은 틈(y 126~146)에만 그린다. 화살표(y 108)와 칸(y 154~)의 어느
   픽셀도 침범하지 않는다.
   🔴 부서지는 칸을 둘로 한 것은 도식이다 — 원문에 소실 개수가 없어서 화면에 수를 안 적었다.

   계속 도는 것 = 두 화살표 점선의 흐름(서로 반대 방향으로 흐른다) · 바닥 눈금의 흐름 ·
   부서진 칸 점선 윤곽의 회전. */

const ROW_A = 58, ROW_B = 108;
const GRID_Y = 154, CELL_W = 56, CELL_H = 46, CELL_GAP = 8, NCELL = 5;
const RAIL_Y = 218;
const BROKEN = [1, 3];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const fit = (txt, weight, start, max, min = 8, f = disp) => {
      let fs = start; ctx.font = f(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = f(weight, fs); }
      return fs;
    };

    const c0 = cue(spec.flipCue ?? 0, 0.15, 0.85);

    const gridW = NCELL * CELL_W + (NCELL - 1) * CELL_GAP;
    const gx0 = (w - gridW) / 2;

    /* ── 위 줄 : 파종은 밭을 비운다 (→) ─────────────── */
    ctx.textAlign = 'left';
    if (spec.sowLabel) {
      ctx.font = disp(900, fit(spec.sowLabel, 900, 15, 60));
      ctx.fillStyle = tone('ink');
      ctx.fillText(spec.sowLabel, 20, ROW_A + 5);
    }
    ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
    ctx.setLineDash([12, 8]);
    ctx.lineDashOffset = -((t * 34) % 20);
    ctx.beginPath(); ctx.moveTo(68, ROW_A); ctx.lineTo(166, ROW_A); ctx.stroke();
    ctx.setLineDash([]);
    ctx.beginPath();
    ctx.moveTo(158, ROW_A - 6); ctx.lineTo(166, ROW_A); ctx.lineTo(158, ROW_A + 6);
    ctx.stroke();
    if (spec.sowRule) {
      ctx.font = mono(700, fit(spec.sowRule, 700, 11, w - 194, 8, mono));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.sowRule, 176, ROW_A + 4);
    }

    /* ── 아래 줄 : 재해는 반대쪽을 본다 (←) ─────────── */
    ctx.textAlign = 'right';
    if (spec.floodLabel) {
      ctx.font = disp(900, fit(spec.floodLabel, 900, 15, 70));
      ctx.fillStyle = tone('ink');
      ctx.fillText(spec.floodLabel, w - 20, ROW_B + 5);
    }
    ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
    ctx.setLineDash([12, 8]);
    ctx.lineDashOffset = (t * 34) % 20;
    ctx.beginPath(); ctx.moveTo(w - 78, ROW_B); ctx.lineTo(w - 176, ROW_B); ctx.stroke();
    ctx.setLineDash([]);
    ctx.beginPath();
    ctx.moveTo(w - 168, ROW_B - 6); ctx.lineTo(w - 176, ROW_B); ctx.lineTo(w - 168, ROW_B + 6);
    ctx.stroke();

    /* ── 겨냥 슬롯 ─────────────────────────────────── */
    ctx.textAlign = 'left';
    if (spec.targetLabel) {
      ctx.font = disp(900, fit(spec.targetLabel, 900, 11, 90));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.targetLabel, 20, ROW_B - 12);
    }
    const aCrop = 1 - ease(clamp((c0 - 0.32) / 0.16));   // c0 0.48 에서 0
    const aField = ease(clamp((c0 - 0.54) / 0.18));      // c0 0.54 에서야 시작
    if (aCrop > 0.01 && spec.fromTarget) {
      ctx.globalAlpha = aCrop;
      ctx.font = mono(700, fit(spec.fromTarget, 700, 15, 120, 9, mono));
      ctx.fillStyle = tone('ink');
      ctx.fillText(spec.fromTarget, 20, ROW_B + 14);
      const tw = ctx.measureText(spec.fromTarget).width;
      const st = ease(clamp((c0 - 0.24) / 0.14));
      if (st > 0.01) {
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(18, ROW_B + 9); ctx.lineTo(18 + (tw + 4) * st, ROW_B + 9); ctx.stroke();
      }
      ctx.globalAlpha = 1;
    }
    if (aField > 0.01 && spec.toTarget) {
      ctx.globalAlpha = aField;
      ctx.font = mono(700, fit(spec.toTarget, 700, 15, 120, 9, mono));
      setShadow(ctx, GLOW, 12);
      ctx.fillStyle = tone('accent');
      ctx.fillText(spec.toTarget, 20, ROW_B + 14);
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    /* ── 밭 칸 한 줄 ───────────────────────────────── */
    const brk = ease(clamp((c0 - 0.68) / 0.30));
    for (let i = 0; i < NCELL; i++) {
      const x = gx0 + i * (CELL_W + CELL_GAP);
      const edge = (i === 0 || i === NCELL - 1) ? 0.45 : 1;
      const hitCell = BROKEN.includes(i);

      if (!hitCell || brk < 0.001) {
        ctx.globalAlpha = edge;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        roundRect(ctx, x, GRID_Y, CELL_W, CELL_H, 6); ctx.stroke();
      } else {
        /* 흰 테두리가 완전히 꺼진 뒤에 강조색 점선이 들어온다 — 두 색이 겹치지 않게 */
        const wA = 1 - clamp(brk / 0.42);
        const aA = clamp((brk - 0.52) / 0.48);
        const sink = ease(clamp((brk - 0.10) / 0.70));
        const hh = CELL_H * lerp(1, 0.52, sink);
        const yy = GRID_Y + (CELL_H - hh);
        if (wA > 0.01) {
          ctx.globalAlpha = edge * wA;
          ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
          roundRect(ctx, x, yy, CELL_W, hh, 6); ctx.stroke();
        }
        if (aA > 0.01) {
          ctx.globalAlpha = edge * aA;
          ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
          ctx.setLineDash([9, 7]);
          ctx.lineDashOffset = -((t * 20) % 16);
          roundRect(ctx, x, yy, CELL_W, hh, 6); ctx.stroke();
          ctx.setLineDash([]);
        }
      }

      /* 타격 자국 — 칸 위 검은 틈에만 */
      if (hitCell && brk > 0.01) {
        const k = clamp(brk * 1.8);
        ctx.globalAlpha = k;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        const cxx = x + CELL_W / 2;
        ctx.beginPath();
        ctx.moveTo(cxx, 126); ctx.lineTo(cxx, lerp(126, 146, ease(k)));
        ctx.stroke();
      }
    }
    ctx.globalAlpha = 1;

    /* ── 바닥 눈금 — 자막과 무관하게 계속 흐른다 ───── */
    ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
    ctx.globalAlpha = 0.42;
    const step = 15, off = (t * 22) % step;
    for (let x = 20 - step + off; x < w - 20; x += step) {
      if (x < 20) continue;
      ctx.beginPath(); ctx.moveTo(x, RAIL_Y); ctx.lineTo(x, RAIL_Y + 10); ctx.stroke();
    }
    ctx.globalAlpha = 1;

    ctx.textAlign = 'left';
  }
};
